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
    public class DatabaseMergeService
    {
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

                using (var context = new BiometricContext(databasePath))
                {
                    await context.Database.OpenConnectionAsync();

                    // CRITICAL: Load with AsNoTracking to avoid EF tracking issues
                    result.Students = await context.Students
                        .Include(s => s.College)
                        .Include(s => s.Test)
                        .AsNoTracking()
                        .ToListAsync();

                    result.Colleges = await context.Colleges.AsNoTracking().ToListAsync();
                    result.Tests = await context.Tests.AsNoTracking().ToListAsync();

                    result.TotalStudents = result.Students.Count;
                    result.Success = true;

                    // DEBUG: Log what we read
                    System.Diagnostics.Debug.WriteLine($"=== READ DATABASE: {Path.GetFileName(databasePath)} ===");
                    System.Diagnostics.Debug.WriteLine($"Colleges: {result.Colleges.Count}");
                    foreach (var c in result.Colleges)
                    {
                        System.Diagnostics.Debug.WriteLine($"  College: {c.Name} (Code: {c.Code}, ID: {c.Id})");
                    }
                    System.Diagnostics.Debug.WriteLine($"Tests: {result.Tests.Count}");
                    foreach (var t in result.Tests)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Test: {t.Name} (Code: {t.Code}, ID: {t.Id})");
                    }
                    System.Diagnostics.Debug.WriteLine($"Students: {result.Students.Count}");
                    foreach (var s in result.Students)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Student: {s.RollNumber} | College: {s.College?.Name ?? "NULL"} (Code: {s.College?.Code ?? "NULL"}) | Test: {s.Test?.Name ?? "NULL"} (Code: {s.Test?.Code ?? "NULL"})");
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error reading database: {ex.Message}";
                result.Success = false;
                System.Diagnostics.Debug.WriteLine($"ERROR reading database: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return result;
        }

        public List<DuplicateGroup> DetectDuplicates(List<Student> allStudents)
        {
            var duplicateGroups = new List<DuplicateGroup>();

            System.Diagnostics.Debug.WriteLine("=== DETECTING DUPLICATES ===");

            // GROUP BY ROLL NUMBER + COLLEGE CODE + TEST CODE (NOT IDs)
            var grouped = allStudents
                .Where(s => s.College != null && s.Test != null) // Safety check
                .GroupBy(s => new {
                    s.RollNumber,
                    CollegeCode = s.College.Code,
                    TestCode = s.Test.Code
                })
                .Where(g => g.Count() > 1);

            foreach (var group in grouped)
            {
                System.Diagnostics.Debug.WriteLine($"Found duplicate group: Roll={group.Key.RollNumber}, College={group.Key.CollegeCode}, Test={group.Key.TestCode}, Count={group.Count()}");

                var duplicateGroup = new DuplicateGroup
                {
                    RollNumber = group.Key.RollNumber,
                    CollegeCode = group.Key.CollegeCode,
                    TestCode = group.Key.TestCode,
                    Students = group.ToList(),
                    Count = group.Count()
                };

                duplicateGroups.Add(duplicateGroup);
            }

            System.Diagnostics.Debug.WriteLine($"Total duplicate groups found: {duplicateGroups.Count}");
            return duplicateGroups;
        }

        public List<Student> ResolveConflicts(List<DuplicateGroup> duplicateGroups)
        {
            var resolvedStudents = new List<Student>();

            foreach (var group in duplicateGroups)
            {
                var latest = group.Students
                    .OrderByDescending(s => s.RegistrationDate)
                    .ThenByDescending(s => s.LastModifiedDate)
                    .First();

                resolvedStudents.Add(latest);
                System.Diagnostics.Debug.WriteLine($"Resolved conflict for {group.RollNumber}: kept record from {latest.RegistrationDate}");
            }

            return resolvedStudents;
        }

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
                System.Diagnostics.Debug.WriteLine("=== STARTING MERGE PROCESS ===");

                var allStudents = new List<Student>();
                var allColleges = new List<College>();
                var allTests = new List<Test>();

                foreach (var dbResult in databaseResults.Where(r => r.Success))
                {
                    System.Diagnostics.Debug.WriteLine($"Processing database: {dbResult.FileName}");
                    allStudents.AddRange(dbResult.Students);
                    allColleges.AddRange(dbResult.Colleges);
                    allTests.AddRange(dbResult.Tests);
                }

                mergeResult.TotalStudentsRead = allStudents.Count;
                System.Diagnostics.Debug.WriteLine($"Total students to process: {allStudents.Count}");

                progress?.Report(new MergeProgress
                {
                    Message = $"Read {allStudents.Count} students from all databases",
                    Percentage = 20
                });

                var duplicateGroups = DetectDuplicates(allStudents);
                mergeResult.DuplicateCount = duplicateGroups.Sum(g => g.Count - 1);
                progress?.Report(new MergeProgress
                {
                    Message = $"Found {mergeResult.DuplicateCount} duplicates",
                    Percentage = 40
                });

                var resolvedDuplicates = ResolveConflicts(duplicateGroups);
                mergeResult.ConflictsResolved = resolvedDuplicates.Count;
                progress?.Report(new MergeProgress
                {
                    Message = $"Resolved {mergeResult.ConflictsResolved} conflicts",
                    Percentage = 60
                });

                // CREATE DUPLICATE KEYS USING CODES
                var duplicateKeys = duplicateGroups
                    .SelectMany(g => g.Students)
                    .Select(s => $"{s.RollNumber}_{s.College?.Code}_{s.Test?.Code}")
                    .ToHashSet();

                var uniqueStudents = allStudents
                    .Where(s => !duplicateKeys.Contains($"{s.RollNumber}_{s.College?.Code}_{s.Test?.Code}"))
                    .ToList();

                uniqueStudents.AddRange(resolvedDuplicates);

                System.Diagnostics.Debug.WriteLine($"Unique students to merge: {uniqueStudents.Count}");

                progress?.Report(new MergeProgress
                {
                    Message = $"Preparing to merge {uniqueStudents.Count} unique students",
                    Percentage = 70
                });

                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BiometricVerification"
                );
                Directory.CreateDirectory(appDataPath);
                var masterDbPath = Path.Combine(appDataPath, "BiometricData.db");

                System.Diagnostics.Debug.WriteLine($"Master database path: {masterDbPath}");

                using (var context = new BiometricContext(masterDbPath))
                {
                    // STEP 1: Import colleges - MATCH BY CODE
                    System.Diagnostics.Debug.WriteLine("=== STEP 1: IMPORTING COLLEGES ===");
                    var uniqueColleges = allColleges
                        .GroupBy(c => c.Code)
                        .Select(g => g.First())
                        .ToList();

                    foreach (var college in uniqueColleges)
                    {
                        var exists = await context.Colleges.AnyAsync(c => c.Code == college.Code);
                        if (!exists)
                        {
                            System.Diagnostics.Debug.WriteLine($"Adding new college: {college.Name} (Code: {college.Code})");
                            var newCollege = new College
                            {
                                Name = college.Name,
                                Code = college.Code,
                                Address = college.Address,
                                ContactPerson = college.ContactPerson,
                                ContactPhone = college.ContactPhone,
                                ContactEmail = college.ContactEmail,
                                IsActive = college.IsActive,
                                CreatedDate = college.CreatedDate
                            };
                            context.Colleges.Add(newCollege);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"College already exists: {college.Name} (Code: {college.Code})");
                        }
                    }

                    await context.SaveChangesAsync();

                    // STEP 2: Import tests - MATCH BY CODE AND COLLEGE CODE
                    System.Diagnostics.Debug.WriteLine("=== STEP 2: IMPORTING TESTS ===");
                    var uniqueTests = allTests
                        .GroupBy(t => t.Code)
                        .Select(g => g.First())
                        .ToList();

                    foreach (var test in uniqueTests)
                    {
                        // Find the source college to get its code
                        var sourceCollege = allColleges.FirstOrDefault(c => c.Id == test.CollegeId);
                        if (sourceCollege == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"WARNING: Test {test.Name} has no source college");
                            continue;
                        }

                        System.Diagnostics.Debug.WriteLine($"Processing test: {test.Name} (Code: {test.Code}) for college code: {sourceCollege.Code}");

                        // Find matching college in master DB by CODE
                        var masterCollege = await context.Colleges.FirstOrDefaultAsync(c => c.Code == sourceCollege.Code);
                        if (masterCollege == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"WARNING: College code {sourceCollege.Code} not found in master DB");
                            continue;
                        }

                        System.Diagnostics.Debug.WriteLine($"Found master college: {masterCollege.Name} (ID: {masterCollege.Id})");

                        var exists = await context.Tests.AnyAsync(t => t.Code == test.Code);
                        if (!exists)
                        {
                            System.Diagnostics.Debug.WriteLine($"Adding new test: {test.Name} (Code: {test.Code}) with CollegeId: {masterCollege.Id}");
                            var newTest = new Test
                            {
                                Name = test.Name,
                                Code = test.Code,
                                Description = test.Description,
                                CollegeId = masterCollege.Id,
                                TestDate = test.TestDate,
                                RegistrationStartDate = test.RegistrationStartDate,
                                RegistrationEndDate = test.RegistrationEndDate,
                                IsActive = test.IsActive,
                                CreatedDate = test.CreatedDate
                            };
                            context.Tests.Add(newTest);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Test already exists: {test.Name} (Code: {test.Code})");
                        }
                    }

                    await context.SaveChangesAsync();

                    // STEP 3: Import students - REMAP IDs USING CODES
                    System.Diagnostics.Debug.WriteLine("=== STEP 3: IMPORTING STUDENTS ===");

                    foreach (var student in uniqueStudents)
                    {
                        System.Diagnostics.Debug.WriteLine($"\n--- Processing Student: {student.RollNumber} ---");
                        System.Diagnostics.Debug.WriteLine($"Student CollegeCode: {student.College?.Code ?? "NULL"}");
                        System.Diagnostics.Debug.WriteLine($"Student TestCode: {student.Test?.Code ?? "NULL"}");

                        // Find matching college and test in master DB by CODE
                        var masterCollege = await context.Colleges
                            .FirstOrDefaultAsync(c => c.Code == student.College.Code);
                        var masterTest = await context.Tests
                            .FirstOrDefaultAsync(t => t.Code == student.Test.Code);

                        if (masterCollege == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"ERROR: Master college not found for code: {student.College?.Code}");
                            mergeResult.StudentsSkipped++;
                            continue;
                        }

                        if (masterTest == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"ERROR: Master test not found for code: {student.Test?.Code}");
                            mergeResult.StudentsSkipped++;
                            continue;
                        }

                        System.Diagnostics.Debug.WriteLine($"Found master college: {masterCollege.Name} (ID: {masterCollege.Id})");
                        System.Diagnostics.Debug.WriteLine($"Found master test: {masterTest.Name} (ID: {masterTest.Id})");

                        // Check if student exists in master DB (using master DB's IDs)
                        var existing = await context.Students
                            .FirstOrDefaultAsync(s => s.RollNumber == student.RollNumber
                                                   && s.CollegeId == masterCollege.Id
                                                   && s.TestId == masterTest.Id);

                        bool incomingHasFingerprint = student.FingerprintTemplate != null && student.FingerprintTemplate.Length > 0;
                        int incomingFpSize = student.FingerprintTemplate?.Length ?? 0;
                        int existingFpSize = existing?.FingerprintTemplate?.Length ?? 0;

                        System.Diagnostics.Debug.WriteLine($"Existing in master: {existing != null}");
                        System.Diagnostics.Debug.WriteLine($"Incoming FP: {incomingFpSize} bytes");
                        System.Diagnostics.Debug.WriteLine($"Existing FP: {existingFpSize} bytes");

                        if (existing == null)
                        {
                            // Add new student with remapped IDs
                            var newStudent = new Student
                            {
                                RollNumber = student.RollNumber,
                                Name = student.Name,
                                CNIC = student.CNIC,
                                CollegeId = masterCollege.Id,  // REMAPPED ID
                                TestId = masterTest.Id,        // REMAPPED ID
                                StudentPhoto = student.StudentPhoto,
                                FingerprintTemplate = student.FingerprintTemplate,
                                FingerprintImage = student.FingerprintImage,
                                FingerprintImageWidth = student.FingerprintImageWidth,
                                FingerprintImageHeight = student.FingerprintImageHeight,
                                RegistrationDate = student.RegistrationDate,
                                LastModifiedDate = student.LastModifiedDate,
                                DeviceId = student.DeviceId,
                                IsVerified = student.IsVerified,
                                VerificationDate = student.VerificationDate
                            };

                            context.Students.Add(newStudent);
                            mergeResult.StudentsImported++;
                            System.Diagnostics.Debug.WriteLine($"✓ IMPORTED");
                        }
                        else
                        {
                            // Check fingerprint data
                            bool existingHasFingerprint = existing.FingerprintTemplate != null && existing.FingerprintTemplate.Length > 0;

                            if (incomingHasFingerprint && !existingHasFingerprint)
                            {
                                // Incoming has fingerprint, existing doesn't - UPDATE
                                existing.FingerprintTemplate = student.FingerprintTemplate;
                                existing.FingerprintImage = student.FingerprintImage;
                                existing.FingerprintImageWidth = student.FingerprintImageWidth;
                                existing.FingerprintImageHeight = student.FingerprintImageHeight;
                                existing.RegistrationDate = student.RegistrationDate;
                                existing.LastModifiedDate = DateTime.Now;
                                existing.DeviceId = student.DeviceId;

                                mergeResult.StudentsUpdated++;
                                System.Diagnostics.Debug.WriteLine($"✓ UPDATED (added fingerprint)");
                            }
                            else if (incomingHasFingerprint && existingHasFingerprint && student.RegistrationDate > existing.RegistrationDate)
                            {
                                // Both have fingerprints, keep newer one
                                existing.FingerprintTemplate = student.FingerprintTemplate;
                                existing.FingerprintImage = student.FingerprintImage;
                                existing.FingerprintImageWidth = student.FingerprintImageWidth;
                                existing.FingerprintImageHeight = student.FingerprintImageHeight;
                                existing.RegistrationDate = student.RegistrationDate;
                                existing.LastModifiedDate = DateTime.Now;
                                existing.DeviceId = student.DeviceId;

                                mergeResult.StudentsUpdated++;
                                System.Diagnostics.Debug.WriteLine($"✓ UPDATED (newer fingerprint)");
                            }
                            else
                            {
                                mergeResult.StudentsSkipped++;
                                System.Diagnostics.Debug.WriteLine($"⊘ SKIPPED (no update needed)");
                            }
                        }
                    }

                    await context.SaveChangesAsync();
                    System.Diagnostics.Debug.WriteLine("=== MERGE COMPLETE ===");
                }

                progress?.Report(new MergeProgress
                {
                    Message = "Merge completed successfully",
                    Percentage = 100
                });

                mergeResult.Success = true;
                mergeResult.EndTime = DateTime.Now;

                System.Diagnostics.Debug.WriteLine($"Final Results: Imported={mergeResult.StudentsImported}, Updated={mergeResult.StudentsUpdated}, Skipped={mergeResult.StudentsSkipped}");
            }
            catch (Exception ex)
            {
                mergeResult.Success = false;
                mergeResult.ErrorMessage = $"Merge failed: {ex.Message}\n\nDetails: {ex.InnerException?.Message}";
                mergeResult.EndTime = DateTime.Now;

                System.Diagnostics.Debug.WriteLine($"MERGE FAILED: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }

            return mergeResult;
        }

        public async Task<string> InspectDatabaseAsync(string databasePath)
        {
            var report = new System.Text.StringBuilder();

            try
            {
                using (var context = new BiometricContext(databasePath))
                {
                    var students = await context.Students
                        .Include(s => s.College)
                        .Include(s => s.Test)
                        .ToListAsync();

                    var colleges = await context.Colleges.ToListAsync();
                    var tests = await context.Tests.ToListAsync();

                    var withFP = students.Count(s => s.FingerprintTemplate != null && s.FingerprintTemplate.Length > 0);
                    var withoutFP = students.Count - withFP;

                    report.AppendLine($"DATABASE: {Path.GetFileName(databasePath)}");
                    report.AppendLine($"PATH: {databasePath}");
                    report.AppendLine();
                    report.AppendLine($"Colleges: {colleges.Count}");
                    foreach (var c in colleges)
                    {
                        report.AppendLine($"  - {c.Name} (Code: {c.Code}, ID: {c.Id})");
                    }
                    report.AppendLine();
                    report.AppendLine($"Tests: {tests.Count}");
                    foreach (var t in tests)
                    {
                        report.AppendLine($"  - {t.Name} (Code: {t.Code}, ID: {t.Id})");
                    }
                    report.AppendLine();
                    report.AppendLine($"Total Students: {students.Count}");
                    report.AppendLine($"With Fingerprint: {withFP}");
                    report.AppendLine($"Without Fingerprint: {withoutFP}");
                    report.AppendLine();
                    report.AppendLine("STUDENT DETAILS:");
                    report.AppendLine("─────────────────────────────────────────");

                    foreach (var s in students.OrderBy(x => x.RollNumber))
                    {
                        var fpSize = s.FingerprintTemplate?.Length ?? 0;
                        var hasFP = fpSize > 0 ? "YES" : "NO";
                        report.AppendLine($"Roll: {s.RollNumber} | College: {s.College?.Name ?? "NULL"} (Code: {s.College?.Code ?? "NULL"}) | " +
                            $"Test: {s.Test?.Name ?? "NULL"} (Code: {s.Test?.Code ?? "NULL"}) | FP: {hasFP} ({fpSize} bytes) | " +
                            $"Date: {s.RegistrationDate:yyyy-MM-dd HH:mm} | Device: {s.DeviceId}");
                    }
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"ERROR: {ex.Message}");
            }

            return report.ToString();
        }

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
            report.AppendLine($"Students Updated:         {result.StudentsUpdated}");
            report.AppendLine($"Students Skipped:         {result.StudentsSkipped}");
            report.AppendLine();
            report.AppendLine($"Total Changes Made:       {result.StudentsImported + result.StudentsUpdated}");
            report.AppendLine();
            report.AppendLine("═══════════════════════════════════════════════════");

            return report.ToString();
        }
    }

    #region Helper Classes

    public class DatabaseReadResult
    {
        public string DatabasePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<Student> Students { get; set; } = new List<Student>();
        public List<College> Colleges { get; set; } = new List<College>();
        public List<Test> Tests { get; set; } = new List<Test>();
        public int TotalStudents { get; set; }
    }

    public class DuplicateGroup
    {
        public string RollNumber { get; set; } = string.Empty;
        public string CollegeCode { get; set; } = string.Empty;
        public string TestCode { get; set; } = string.Empty;
        public List<Student> Students { get; set; } = new List<Student>();
        public int Count { get; set; }
    }

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
        public int StudentsUpdated { get; set; }
        public int StudentsSkipped { get; set; }
    }

    public class MergeProgress
    {
        public string Message { get; set; } = string.Empty;
        public int Percentage { get; set; }
    }

    #endregion
}