using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BiometricCommon.Models;
using BiometricCommon.Database;

namespace BiometricCommon.Services
{
    /// <summary>
    /// Service for merging multiple databases from different laptops into master database
    /// </summary>
    public class DatabaseMergeService
    {
        /// <summary>
        /// Read all students from an external database file
        /// </summary>
        public async Task<DatabaseReadResult> ReadDatabaseAsync(string databasePath)
        {
            var result = new DatabaseReadResult
            {
                DatabasePath = databasePath,
                FileName = Path.GetFileName(databasePath),
                Success = false,
                ErrorMessage = string.Empty
            };

            try
            {
                if (!File.Exists(databasePath))
                {
                    result.ErrorMessage = "Database file not found";
                    return result;
                }

                // Create connection string for external database
                var externalConnectionString = $"Data Source={databasePath}";

                using (var context = new BiometricContext(externalConnectionString))
                {
                    // Verify database can be opened
                    await context.Database.OpenConnectionAsync();

                    // Read all students
                    result.Students = await context.Students
                        .Include(s => s.College)
                        .Include(s => s.Test)
                        .ToListAsync();

                    result.TotalStudents = result.Students.Count;
                    result.Success = true;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error reading database: {ex.Message}";
                result.Success = false;
            }

            return result;
        }

        /// <summary>
        /// Detect duplicates across all students from multiple databases
        /// </summary>
        public List<DuplicateGroup> DetectDuplicates(List<Student> allStudents)
        {
            var duplicateGroups = new List<DuplicateGroup>();

            // Group by RollNumber + CollegeId + TestId (unique key)
            var grouped = allStudents
                .GroupBy(s => new { s.RollNumber, s.CollegeId, s.TestId })
                .Where(g => g.Count() > 1); // Only groups with duplicates

            foreach (var group in grouped)
            {
                var duplicateGroup = new DuplicateGroup
                {
                    RollNumber = group.Key.RollNumber,
                    CollegeId = group.Key.CollegeId,
                    TestId = group.Key.TestId,
                    Students = group.ToList(),
                    Count = group.Count()
                };

                duplicateGroups.Add(duplicateGroup);
            }

            return duplicateGroups;
        }

        /// <summary>
        /// Resolve conflicts by keeping the most recent registration
        /// </summary>
        public List<Student> ResolveConflicts(List<DuplicateGroup> duplicateGroups)
        {
            var resolvedStudents = new List<Student>();

            foreach (var group in duplicateGroups)
            {
                // Keep the student with the latest RegistrationDate
                var latest = group.Students
                    .OrderByDescending(s => s.RegistrationDate)
                    .First();

                resolvedStudents.Add(latest);
            }

            return resolvedStudents;
        }

        /// <summary>
        /// Merge all students into the master database
        /// </summary>
        public async Task<MergeResult> MergeIntoMasterAsync(
            List<DatabaseReadResult> databaseResults,
            IProgress<MergeProgress>? progress = null)
        {
            var mergeResult = new MergeResult
            {
                StartTime = DateTime.Now,
                DatabaseResults = databaseResults,
                ErrorMessage = string.Empty
            };

            try
            {
                // Combine all students from all databases
                var allStudents = new List<Student>();
                foreach (var dbResult in databaseResults.Where(r => r.Success))
                {
                    allStudents.AddRange(dbResult.Students);
                }

                mergeResult.TotalStudentsRead = allStudents.Count;
                progress?.Report(new MergeProgress
                {
                    Message = $"Read {allStudents.Count} students from all databases",
                    Percentage = 20
                });

                // Detect duplicates
                var duplicateGroups = DetectDuplicates(allStudents);
                mergeResult.DuplicateCount = duplicateGroups.Sum(g => g.Count - 1); // Count extras
                progress?.Report(new MergeProgress
                {
                    Message = $"Found {mergeResult.DuplicateCount} duplicates",
                    Percentage = 40
                });

                // Resolve conflicts (keep latest)
                var resolvedDuplicates = ResolveConflicts(duplicateGroups);
                mergeResult.ConflictsResolved = resolvedDuplicates.Count;
                progress?.Report(new MergeProgress
                {
                    Message = $"Resolved {mergeResult.ConflictsResolved} conflicts",
                    Percentage = 60
                });

                // Get unique students (remove duplicates, keep resolved ones)
                var duplicateKeys = duplicateGroups
                    .SelectMany(g => g.Students)
                    .Select(s => $"{s.RollNumber}_{s.CollegeId}_{s.TestId}")
                    .ToHashSet();

                var uniqueStudents = allStudents
                    .Where(s => !duplicateKeys.Contains($"{s.RollNumber}_{s.CollegeId}_{s.TestId}"))
                    .ToList();

                // Add resolved duplicates
                uniqueStudents.AddRange(resolvedDuplicates);

                progress?.Report(new MergeProgress
                {
                    Message = $"Preparing to merge {uniqueStudents.Count} unique students",
                    Percentage = 70
                });

                // Get master database connection string
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BiometricVerification"
                );
                Directory.CreateDirectory(appDataPath);
                var masterConnectionString = $"Data Source={Path.Combine(appDataPath, "BiometricData.db")}";

                // Insert into master database
                using (var context = new BiometricContext(masterConnectionString))
                {
                    foreach (var student in uniqueStudents)
                    {
                        // Check if student already exists in master database
                        var exists = await context.Students
                            .AnyAsync(s => s.RollNumber == student.RollNumber
                                        && s.CollegeId == student.CollegeId
                                        && s.TestId == student.TestId);

                        if (!exists)
                        {
                            // Reset ID to let database auto-increment
                            student.Id = 0;

                            // Add to master database
                            context.Students.Add(student);
                            mergeResult.StudentsImported++;
                        }
                        else
                        {
                            mergeResult.StudentsSkipped++;
                        }
                    }

                    await context.SaveChangesAsync();
                }

                progress?.Report(new MergeProgress
                {
                    Message = "Merge completed successfully",
                    Percentage = 100
                });

                mergeResult.Success = true;
                mergeResult.EndTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                mergeResult.Success = false;
                mergeResult.ErrorMessage = $"Merge failed: {ex.Message}";
                mergeResult.EndTime = DateTime.Now;
            }

            return mergeResult;
        }

        /// <summary>
        /// Generate a detailed merge report
        /// </summary>
        public string GenerateMergeReport(MergeResult result)
        {
            var report = new System.Text.StringBuilder();

            report.AppendLine("═══════════════════════════════════════════════════");
            report.AppendLine("       DATABASE MERGE REPORT");
            report.AppendLine("═══════════════════════════════════════════════════");
            report.AppendLine();
            report.AppendLine($"Start Time:       {result.StartTime:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"End Time:         {result.EndTime:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Duration:         {(result.EndTime - result.StartTime).TotalSeconds:F2} seconds");
            report.AppendLine($"Status:           {(result.Success ? "SUCCESS ✓" : "FAILED ✗")}");
            report.AppendLine();

            if (!result.Success)
            {
                report.AppendLine($"Error: {result.ErrorMessage}");
                return report.ToString();
            }

            report.AppendLine("───────────────────────────────────────────────────");
            report.AppendLine("DATABASES PROCESSED:");
            report.AppendLine("───────────────────────────────────────────────────");

            foreach (var dbResult in result.DatabaseResults)
            {
                var status = dbResult.Success ? "✓" : "✗";
                report.AppendLine($"{status} {dbResult.FileName}");
                report.AppendLine($"   Students: {dbResult.TotalStudents}");
                if (!dbResult.Success)
                {
                    report.AppendLine($"   Error: {dbResult.ErrorMessage}");
                }
                report.AppendLine();
            }

            report.AppendLine("───────────────────────────────────────────────────");
            report.AppendLine("SUMMARY:");
            report.AppendLine("───────────────────────────────────────────────────");
            report.AppendLine($"Total Students Read:      {result.TotalStudentsRead}");
            report.AppendLine($"Duplicates Found:         {result.DuplicateCount}");
            report.AppendLine($"Conflicts Resolved:       {result.ConflictsResolved}");
            report.AppendLine($"Students Imported:        {result.StudentsImported}");
            report.AppendLine($"Students Skipped:         {result.StudentsSkipped}");
            report.AppendLine();
            report.AppendLine($"Final Count in Master DB: {result.StudentsImported}");
            report.AppendLine();
            report.AppendLine("═══════════════════════════════════════════════════");

            return report.ToString();
        }
    }

    #region Helper Classes

    /// <summary>
    /// Result of reading a single database file
    /// </summary>
    public class DatabaseReadResult
    {
        public string DatabasePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<Student> Students { get; set; } = new List<Student>();
        public int TotalStudents { get; set; }
    }

    /// <summary>
    /// Group of duplicate students
    /// </summary>
    public class DuplicateGroup
    {
        public string RollNumber { get; set; } = string.Empty;
        public int CollegeId { get; set; }
        public int TestId { get; set; }
        public List<Student> Students { get; set; } = new List<Student>();
        public int Count { get; set; }
    }

    /// <summary>
    /// Overall merge result
    /// </summary>
    public class MergeResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<DatabaseReadResult> DatabaseResults { get; set; } = new List<DatabaseReadResult>();
        public int TotalStudentsRead { get; set; }
        public int DuplicateCount { get; set; }
        public int ConflictsResolved { get; set; }
        public int StudentsImported { get; set; }
        public int StudentsSkipped { get; set; }
    }

    /// <summary>
    /// Progress reporting for merge operation
    /// </summary>
    public class MergeProgress
    {
        public string Message { get; set; } = string.Empty;
        public int Percentage { get; set; }
    }

    #endregion
}