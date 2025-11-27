using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using BiometricCommon.Models;
using BiometricCommon.Database;
using BiometricCommon.Encryption;

namespace BiometricCommon.Services
{
    public class PackageGenerationService
    {
        private readonly BiometricContext _context;

        public PackageGenerationService()
        {
            _context = new BiometricContext();
        }

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

                // Clean up the output path properly
                try
                {
                    outputPath = Path.GetFullPath(outputPath);

                    // Validate the path doesn't contain invalid characters
                    char[] invalidChars = Path.GetInvalidPathChars();
                    if (outputPath.Any(c => invalidChars.Contains(c)))
                    {
                        throw new ArgumentException("Output path contains invalid characters");
                    }
                }
                catch
                {
                    // If path is invalid, use Desktop as default
                    string defaultDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    outputPath = Path.Combine(defaultDir, $"{college.Code}_Package_{DateTime.Now:yyyyMMddHHmmss}.zip");
                }

                string outputDirectory = Path.GetDirectoryName(outputPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                // Ensure output directory exists
                try
                {
                    if (!Directory.Exists(outputDirectory))
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }
                }
                catch
                {
                    // Fall back to Desktop if we can't create the directory
                    outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    outputPath = Path.Combine(outputDirectory, $"{college.Code}_Package_{DateTime.Now:yyyyMMddHHmmss}.zip");
                }

                // Create temp directory with simple, clean name - SAFER VERSION
                string tempBasePath = Path.GetTempPath();

                // Validate temp path
                if (string.IsNullOrEmpty(tempBasePath) || !Directory.Exists(tempBasePath))
                {
                    // Fallback to user's temp folder
                    tempBasePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Temp"
                    );

                    if (!Directory.Exists(tempBasePath))
                    {
                        Directory.CreateDirectory(tempBasePath);
                    }
                }

                // Create unique folder name
                string uniqueFolderName = $"BiomPkg{DateTime.Now:yyyyMMddHHmmss}";
                string tempFolder = Path.Combine(tempBasePath, uniqueFolderName);

                // Create the directory
                Directory.CreateDirectory(tempFolder);

                try
                {
                    progress?.Report(new PackageProgress { Message = "Creating college database...", Percentage = 35 });

                    string collegeDbPath = Path.Combine(tempFolder, "CollegeData.db");
                    await CreateCollegeDatabaseAsync(college, test, students, collegeDbPath, progress);

                    // CRITICAL: Force SQLite to release all connections
                    SqliteConnection.ClearAllPools();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    // Additional wait to ensure file is released
                    await Task.Delay(500);

                    progress?.Report(new PackageProgress { Message = "Encrypting database...", Percentage = 60 });

                    string encryptedDbPath = Path.Combine(tempFolder, "CollegeData.encrypted");
                    string encryptionKey = EncryptionService.GenerateCollegeKey(college.Code, test.Code);

                    // Try encryption with retry logic
                    int retryCount = 0;
                    bool encrypted = false;
                    while (!encrypted && retryCount < 3)
                    {
                        try
                        {
                            EncryptionService.EncryptFile(collegeDbPath, encryptedDbPath, encryptionKey);
                            encrypted = true;
                        }
                        catch (IOException)
                        {
                            retryCount++;
                            if (retryCount < 3)
                            {
                                await Task.Delay(1000);
                                SqliteConnection.ClearAllPools();
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }

                    if (File.Exists(collegeDbPath))
                        File.Delete(collegeDbPath);

                    progress?.Report(new PackageProgress { Message = "Creating package metadata...", Percentage = 70 });

                    var packageInfo = new PackageInfoData
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
                    string packageInfoPath = Path.Combine(tempFolder, "PackageInfo.json");
                    File.WriteAllText(packageInfoPath, packageInfoJson);

                    string encryptedInfoPath = Path.Combine(tempFolder, "PackageInfo.encrypted");
                    EncryptionService.EncryptFile(packageInfoPath, encryptedInfoPath, encryptionKey);

                    if (File.Exists(packageInfoPath))
                        File.Delete(packageInfoPath);

                    progress?.Report(new PackageProgress { Message = "Creating README file...", Percentage = 75 });

                    CreateReadmeFile(tempFolder, college, test, students.Count);

                    progress?.Report(new PackageProgress { Message = "Creating installation script...", Percentage = 80 });

                    CreateInstallScript(tempFolder, college.Code);

                    progress?.Report(new PackageProgress { Message = "Creating ZIP package...", Percentage = 85 });

                    // Delete existing file if it exists
                    if (File.Exists(outputPath))
                    {
                        try
                        {
                            File.Delete(outputPath);
                        }
                        catch
                        {
                            // If we can't delete, use a different filename
                            outputPath = Path.Combine(outputDirectory, $"{college.Code}_Package_{DateTime.Now:yyyyMMddHHmmss_fff}.zip");
                        }
                    }

                    ZipFile.CreateFromDirectory(tempFolder, outputPath);

                    result.PackagePath = outputPath;
                    result.PackageSize = new FileInfo(outputPath).Length;

                    progress?.Report(new PackageProgress { Message = "Package created successfully!", Percentage = 100 });

                    result.Success = true;
                }
                finally
                {
                    // Cleanup temp directory
                    if (Directory.Exists(tempFolder))
                    {
                        try
                        {
                            Directory.Delete(tempFolder, true);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
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

        private async Task CreateCollegeDatabaseAsync(
            College college,
            Test test,
            List<Student> students,
            string dbPath,
            IProgress<PackageProgress>? progress = null)
        {
            // CRITICAL FIX: Pass only the path, NOT "Data Source={dbPath}"
            // BiometricContext constructor expects a file path and adds "Data Source=" itself
            using (var collegeContext = new BiometricContext(dbPath))
            {
                await collegeContext.Database.EnsureCreatedAsync();

                progress?.Report(new PackageProgress { Message = "Copying college information...", Percentage = 40 });

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

                // CRITICAL: Explicitly close the database connection
                await collegeContext.Database.CloseConnectionAsync();

            } // using block ends here - context is disposed

            // CRITICAL: Add a small delay to ensure file handles are fully released
            await Task.Delay(100);

            // Force garbage collection to release any lingering handles
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

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
2. OR copy all files to a folder and run BiometricCollegeVerify.exe

═══════════════════════════════════════════════════════════════════

PACKAGE CONTENTS:
═══════════════════════════════════════════════════════════════════

- CollegeData.encrypted    - Encrypted student database
- PackageInfo.encrypted    - Package metadata
- Install.bat              - Automatic installer (Windows)
- README.txt               - This file

═══════════════════════════════════════════════════════════════════

SYSTEM REQUIREMENTS:
═══════════════════════════════════════════════════════════════════

- Windows 10 or later
- .NET 8.0 Runtime
- 100 MB free disk space

═══════════════════════════════════════════════════════════════════

SUPPORT:

College Contact: {college.ContactPerson}
Email: {college.ContactEmail}
Phone: {college.ContactPhone}

═══════════════════════════════════════════════════════════════════

IMPORTANT NOTES:
═══════════════════════════════════════════════════════════════════

- This package contains encrypted data for {college.Name} ONLY
- Do not share this package with other colleges
- All verification data is stored locally (no internet required)
- Keep this package in a secure location

═══════════════════════════════════════════════════════════════════
Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
Version: 1.0
═══════════════════════════════════════════════════════════════════
";

            File.WriteAllText(Path.Combine(tempDir, "README.txt"), readmeContent);
        }

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

    public class PackageInfoData
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
}