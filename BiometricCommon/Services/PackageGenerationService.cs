using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BiometricCommon.Models;
using BiometricCommon.Database;
using BiometricCommon.Encryption;

namespace BiometricCommon.Services
{
    /// <summary>
    /// Service for generating college-specific verification packages
    /// </summary>
    public class PackageGenerationService
    {
        private readonly BiometricContext _context;

        public PackageGenerationService()
        {
            _context = new BiometricContext();
        }

        /// <summary>
        /// Get list of all colleges with student counts
        /// </summary>
        public async Task<List<CollegePackageInfo>> GetCollegesWithStudentCountsAsync()
        {
            var colleges = await _context.Colleges
                .Where(c => c.IsActive)
                .Select(c => new CollegePackageInfo
                {
                    CollegeId = c.Id,
                    CollegeName = c.Name,
                    CollegeCode = c.Code,
                    StudentCount = _context.Students.Count(s => s.CollegeId == c.Id)
                })
                .ToListAsync();

            return colleges;
        }

        /// <summary>
        /// Get tests for a specific college
        /// </summary>
        public async Task<List<TestPackageInfo>> GetTestsForCollegeAsync(int collegeId)
        {
            var tests = await _context.Tests
                .Where(t => t.CollegeId == collegeId && t.IsActive)
                .Select(t => new TestPackageInfo
                {
                    TestId = t.Id,
                    TestName = t.Name,
                    TestCode = t.Code,
                    StudentCount = _context.Students.Count(s => s.TestId == t.Id)
                })
                .ToListAsync();

            return tests;
        }

        /// <summary>
        /// Generate a verification package for a college
        /// </summary>
        public async Task<PackageGenerationResult> GeneratePackageAsync(
            int collegeId,
            int testId,
            string outputPath,
            IProgress<PackageProgress>? progress = null)
        {
            var result = new PackageGenerationResult
            {
                StartTime = DateTime.Now
            };

            try
            {
                progress?.Report(new PackageProgress { Message = "Loading college and test information...", Percentage = 5 });

                // Get college and test info
                var college = await _context.Colleges.FindAsync(collegeId);
                var test = await _context.Tests.FindAsync(testId);

                if (college == null || test == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "College or test not found";
                    return result;
                }

                result.CollegeName = college.Name;
                result.TestName = test.Name;

                progress?.Report(new PackageProgress { Message = "Filtering students...", Percentage = 15 });

                // Get students for this college and test
                var students = await _context.Students
                    .Where(s => s.CollegeId == collegeId && s.TestId == testId)
                    .ToListAsync();

                result.TotalStudents = students.Count;

                if (students.Count == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "No students found for this college and test";
                    return result;
                }

                progress?.Report(new PackageProgress { Message = $"Found {students.Count} students...", Percentage = 25 });

                // Create temporary directory for package contents
                string tempDir = Path.Combine(Path.GetTempPath(), $"BiometricPackage_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    progress?.Report(new PackageProgress { Message = "Creating college database...", Percentage = 35 });

                    // Create college-specific database
                    string collegeDbPath = Path.Combine(tempDir, "CollegeData.db");
                    await CreateCollegeDatabaseAsync(college, test, students, collegeDbPath, progress);

                    progress?.Report(new PackageProgress { Message = "Encrypting database...", Percentage = 60 });

                    // Encrypt the database
                    string encryptedDbPath = Path.Combine(tempDir, "CollegeData.encrypted");
                    string encryptionKey = EncryptionService.GenerateCollegeKey(college.Code, test.Code);
                    EncryptionService.EncryptFile(collegeDbPath, encryptedDbPath, encryptionKey);

                    // Delete unencrypted database
                    File.Delete(collegeDbPath);

                    progress?.Report(new PackageProgress { Message = "Creating package metadata...", Percentage = 70 });

                    // Create package info file
                    var packageInfo = new PackageInfo
                    {
                        CollegeId = college.Id,
                        CollegeName = college.Name,
                        CollegeCode = college.Code,
                        TestId = test.Id,
                        TestName = test.Name,
                        TestCode = test.Code,
                        TotalStudents = students.Count,
                        PackageDate = DateTime.Now,
                        EncryptionKey = encryptionKey,
                        Version = "1.0"
                    };

                    string packageInfoJson = JsonSerializer.Serialize(packageInfo, new JsonSerializerOptions { WriteIndented = true });
                    string encryptedInfoPath = Path.Combine(tempDir, "PackageInfo.encrypted");
                    File.WriteAllText(Path.Combine(tempDir, "PackageInfo.json"), packageInfoJson);

                    // Encrypt package info
                    EncryptionService.EncryptFile(
                        Path.Combine(tempDir, "PackageInfo.json"),
                        encryptedInfoPath,
                        encryptionKey);
                    File.Delete(Path.Combine(tempDir, "PackageInfo.json"));

                    progress?.Report(new PackageProgress { Message = "Creating README file...", Percentage = 75 });

                    // Create README file
                    CreateReadmeFile(tempDir, college, test, students.Count);

                    progress?.Report(new PackageProgress { Message = "Creating installation script...", Percentage = 80 });

                    // Create installation script
                    CreateInstallScript(tempDir, college.Code);

                    progress?.Report(new PackageProgress { Message = "Creating ZIP package...", Percentage = 85 });

                    // Create ZIP file
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);

                    ZipFile.CreateFromDirectory(tempDir, outputPath);

                    result.PackagePath = outputPath;
                    result.PackageSize = new FileInfo(outputPath).Length;

                    progress?.Report(new PackageProgress { Message = "Package created successfully!", Percentage = 100 });

                    result.Success = true;
                }
                finally
                {
                    // Cleanup temp directory
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }

                result.EndTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Package generation failed: {ex.Message}";
                result.EndTime = DateTime.Now;
            }

            return result;
        }

        /// <summary>
        /// Create a college-specific database with filtered students
        /// </summary>
        private async Task CreateCollegeDatabaseAsync(
            College college,
            Test test,
            List<Student> students,
            string dbPath,
            IProgress<PackageProgress>? progress = null)
        {
            using (var collegeContext = new BiometricContext($"Data Source={dbPath}"))
            {
                // Ensure database is created
                await collegeContext.Database.EnsureCreatedAsync();

                progress?.Report(new PackageProgress { Message = "Copying college information...", Percentage = 40 });

                // Add college
                collegeContext.Colleges.Add(new College
                {
                    Id = college.Id,
                    Name = college.Name,
                    Code = college.Code,
                    Address = college.Address,
                    ContactPerson = college.ContactPerson,
                    ContactPhone = college.ContactPhone,
                    ContactEmail = college.ContactEmail,
                    IsActive = college.IsActive,
                    CreatedDate = college.CreatedDate
                });

                progress?.Report(new PackageProgress { Message = "Copying test information...", Percentage = 45 });

                // Add test
                collegeContext.Tests.Add(new Test
                {
                    Id = test.Id,
                    Name = test.Name,
                    Code = test.Code,
                    Description = test.Description,
                    CollegeId = test.CollegeId,
                    TestDate = test.TestDate,
                    RegistrationStartDate = test.RegistrationStartDate,
                    RegistrationEndDate = test.RegistrationEndDate,
                    IsActive = test.IsActive,
                    CreatedDate = test.CreatedDate
                });

                await collegeContext.SaveChangesAsync();

                progress?.Report(new PackageProgress { Message = $"Copying {students.Count} students...", Percentage = 50 });

                // Add students
                foreach (var student in students)
                {
                    collegeContext.Students.Add(new Student
                    {
                        RollNumber = student.RollNumber,
                        CollegeId = student.CollegeId,
                        TestId = student.TestId,
                        FingerprintTemplate = student.FingerprintTemplate,
                        RegistrationDate = student.RegistrationDate,
                        DeviceId = student.DeviceId,
                        IsVerified = false,
                        VerificationDate = null
                    });
                }

                await collegeContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Create README file with installation instructions
        /// </summary>
        private void CreateReadmeFile(string tempDir, College college, Test test, int studentCount)
        {
            string readmeContent = $@"
╔══════════════════════════════════════════════════════════════════╗
║     BIOMETRIC VERIFICATION PACKAGE - INSTALLATION GUIDE          ║
╚══════════════════════════════════════════════════════════════════╝

College: {college.Name}
Test:    {test.Name}
Date:    {DateTime.Now:yyyy-MM-dd}
Students: {studentCount}

═══════════════════════════════════════════════════════════════════

INSTALLATION INSTRUCTIONS:
═══════════════════════════════════════════════════════════════════

Windows Installation:
1. Double-click 'Install.bat' to install automatically
   OR
2. Copy all files to a folder on your computer
3. Run BiometricCollegeVerify.exe

═══════════════════════════════════════════════════════════════════

PACKAGE CONTENTS:
═══════════════════════════════════════════════════════════════════

• CollegeData.encrypted    - Encrypted student database
• PackageInfo.encrypted    - Package metadata
• Install.bat              - Automatic installer (Windows)
• README.txt               - This file

═══════════════════════════════════════════════════════════════════

SYSTEM REQUIREMENTS:
═══════════════════════════════════════════════════════════════════

• Windows 10 or later
• .NET 8.0 Runtime
• Fingerprint scanner (compatible with system)
• 100 MB free disk space

═══════════════════════════════════════════════════════════════════

SUPPORT:
═══════════════════════════════════════════════════════════════════

For technical support, contact your system administrator.

College Contact: {college.ContactPerson}
Email: {college.ContactEmail}
Phone: {college.ContactPhone}

═══════════════════════════════════════════════════════════════════

IMPORTANT NOTES:
═══════════════════════════════════════════════════════════════════

• This package contains encrypted data for {college.Name} ONLY
• Do not share this package with other colleges
• All verification data is stored locally (no internet required)
• Keep this package in a secure location

═══════════════════════════════════════════════════════════════════
Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
Version: 1.0
═══════════════════════════════════════════════════════════════════
";

            File.WriteAllText(Path.Combine(tempDir, "README.txt"), readmeContent);
        }

        /// <summary>
        /// Create installation batch script
        /// </summary>
        private void CreateInstallScript(string tempDir, string collegeCode)
        {
            string installScript = $@"@echo off
echo ╔══════════════════════════════════════════════════════════════════╗
echo ║     BIOMETRIC VERIFICATION SYSTEM - INSTALLER                    ║
echo ╚══════════════════════════════════════════════════════════════════╝
echo.
echo Installing verification system for {collegeCode}...
echo.

REM Create installation directory
set INSTALL_DIR=%APPDATA%\BiometricVerification\{collegeCode}
if not exist ""%INSTALL_DIR%"" mkdir ""%INSTALL_DIR%""

REM Copy files
echo Copying files...
copy CollegeData.encrypted ""%INSTALL_DIR%\"" >nul
copy PackageInfo.encrypted ""%INSTALL_DIR%\"" >nul

echo.
echo ✓ Installation complete!
echo.
echo Files installed to:
echo %INSTALL_DIR%
echo.
echo To run the verification system:
echo 1. Run BiometricCollegeVerify.exe
echo 2. Import the college package
echo 3. Start verifying students
echo.
pause
";

            File.WriteAllText(Path.Combine(tempDir, "Install.bat"), installScript);
        }

        /// <summary>
        /// Generate a report of package generation
        /// </summary>
        public string GeneratePackageReport(PackageGenerationResult result)
        {
            var report = new System.Text.StringBuilder();

            report.AppendLine("═══════════════════════════════════════════════════");
            report.AppendLine("       PACKAGE GENERATION REPORT");
            report.AppendLine("═══════════════════════════════════════════════════");
            report.AppendLine();
            report.AppendLine($"Status:           {(result.Success ? "SUCCESS ✓" : "FAILED ✗")}");
            report.AppendLine($"College:          {result.CollegeName}");
            report.AppendLine($"Test:             {result.TestName}");
            report.AppendLine($"Students:         {result.TotalStudents}");
            report.AppendLine($"Package Size:     {result.PackageSize / 1024.0 / 1024.0:F2} MB");
            report.AppendLine($"Generation Time:  {(result.EndTime - result.StartTime).TotalSeconds:F2} seconds");
            report.AppendLine();

            if (result.Success)
            {
                report.AppendLine($"Package Location: {result.PackagePath}");
            }
            else
            {
                report.AppendLine($"Error: {result.ErrorMessage}");
            }

            report.AppendLine();
            report.AppendLine("═══════════════════════════════════════════════════");

            return report.ToString();
        }
    }

    #region Helper Classes

    public class CollegePackageInfo
    {
        public int CollegeId { get; set; }
        public string CollegeName { get; set; } = string.Empty;
        public string CollegeCode { get; set; } = string.Empty;
        public int StudentCount { get; set; }
    }

    public class TestPackageInfo
    {
        public int TestId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string TestCode { get; set; } = string.Empty;
        public int StudentCount { get; set; }
    }

    public class PackageInfo
    {
        public int CollegeId { get; set; }
        public string CollegeName { get; set; } = string.Empty;
        public string CollegeCode { get; set; } = string.Empty;
        public int TestId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string TestCode { get; set; } = string.Empty;
        public int TotalStudents { get; set; }
        public DateTime PackageDate { get; set; }
        public string EncryptionKey { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }

    public class PackageGenerationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string CollegeName { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public int TotalStudents { get; set; }
        public string PackagePath { get; set; } = string.Empty;
        public long PackageSize { get; set; }
    }

    public class PackageProgress
    {
        public string Message { get; set; } = string.Empty;
        public int Percentage { get; set; }
    }

    #endregion
}