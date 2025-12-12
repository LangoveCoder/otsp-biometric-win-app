using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BiometricCommon.Models;
using BiometricCommon.Database;
using BiometricCommon.Encryption;

namespace BiometricCommon.Services
{
    /// <summary>
    /// Service for exporting and importing master configuration (colleges, tests, and students)
    /// This allows distributing the same setup to multiple laptops
    /// </summary>
    public class MasterConfigService
    {
        private readonly BiometricContext _context;

        public MasterConfigService()
        {
            _context = new BiometricContext();
        }

        /// <summary>
        /// Export master configuration (colleges, tests, and students) to encrypted file
        /// </summary>
        public async Task<string> ExportMasterConfigAsync(string outputPath)
        {
            try
            {
                var colleges = await Task.Run(() => _context.Colleges.ToList());
                var tests = await Task.Run(() => _context.Tests.ToList());
                var students = await Task.Run(() => _context.Students.ToList());

                var config = new MasterConfiguration
                {
                    ExportDate = DateTime.Now,
                    Version = "1.0",
                    Colleges = colleges.Select(c => new CollegeConfig
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Code = c.Code,
                        Address = c.Address,
                        ContactPerson = c.ContactPerson,
                        ContactPhone = c.ContactPhone,
                        ContactEmail = c.ContactEmail,
                        IsActive = c.IsActive
                    }).ToList(),
                    Tests = tests.Select(t => new TestConfig
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Code = t.Code,
                        Description = t.Description,
                        CollegeId = t.CollegeId,
                        CollegeCode = _context.Colleges.FirstOrDefault(c => c.Id == t.CollegeId)?.Code ?? "", // ADD COLLEGE CODE
                        TestDate = t.TestDate,
                        RegistrationStartDate = t.RegistrationStartDate,
                        RegistrationEndDate = t.RegistrationEndDate,
                        IsActive = t.IsActive
                    }).ToList(),
                    Students = students.Select(s => new StudentConfig
                    {
                        Id = s.Id,
                        RollNumber = s.RollNumber,
                        Name = s.Name,
                        CNIC = s.CNIC,
                        CollegeId = s.CollegeId,
                        CollegeCode = _context.Colleges.FirstOrDefault(c => c.Id == s.CollegeId)?.Code ?? "", // ADD COLLEGE CODE
                        TestId = s.TestId,
                        TestCode = _context.Tests.FirstOrDefault(t => t.Id == s.TestId)?.Code ?? "", // ADD TEST CODE
                        StudentPhoto = s.StudentPhoto,
                        FingerprintTemplate = s.FingerprintTemplate,
                        FingerprintImage = s.FingerprintImage,
                        FingerprintImageWidth = s.FingerprintImageWidth,
                        FingerprintImageHeight = s.FingerprintImageHeight,
                        RegistrationDate = s.RegistrationDate,
                        DeviceId = s.DeviceId
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = false });
                var encrypted = EncryptionService.Encrypt(json, "MasterConfig2024!");
                await File.WriteAllTextAsync(outputPath, encrypted);

                return $"Exported {colleges.Count} colleges, {tests.Count} tests, and {students.Count} students!";
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Import master configuration from encrypted file
        /// </summary>
        public async Task<ImportResult> ImportMasterConfigAsync(string filePath)
        {
            try
            {
                var encrypted = await File.ReadAllTextAsync(filePath);
                var json = EncryptionService.Decrypt(encrypted, "MasterConfig2024!");

                var config = JsonSerializer.Deserialize<MasterConfiguration>(json);
                if (config == null)
                    throw new Exception("Invalid configuration file");

                var result = new ImportResult
                {
                    Success = true,
                    ImportDate = DateTime.Now
                };

                var existingColleges = _context.Colleges.ToList();
                var existingTests = _context.Tests.ToList();

                if (existingColleges.Count > 0 || existingTests.Count > 0)
                {
                    result.WasUpdate = true;
                    await MergeConfigurationAsync(config, result);
                }
                else
                {
                    result.WasUpdate = false;
                    await ImportFreshConfigurationAsync(config, result);
                }

                await _context.SaveChangesAsync();
                return result;
            }
            catch (Exception ex)
            {
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to import configuration: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Import configuration into empty database
        /// </summary>
        private async Task ImportFreshConfigurationAsync(MasterConfiguration config, ImportResult result)
        {
            // Import colleges - MATCH BY CODE, NOT ID
            foreach (var collegeConfig in config.Colleges)
            {
                var existingCollege = _context.Colleges.FirstOrDefault(c => c.Code == collegeConfig.Code);

                if (existingCollege == null)
                {
                    var college = new College
                    {
                        // Don't set ID - let it auto-generate
                        Name = collegeConfig.Name,
                        Code = collegeConfig.Code,
                        Address = collegeConfig.Address,
                        ContactPerson = collegeConfig.ContactPerson,
                        ContactPhone = collegeConfig.ContactPhone,
                        ContactEmail = collegeConfig.ContactEmail,
                        IsActive = collegeConfig.IsActive,
                        CreatedDate = DateTime.Now
                    };

                    _context.Colleges.Add(college);
                    result.CollegesImported++;
                }
            }

            await _context.SaveChangesAsync();

            // Import tests - MATCH BY CODE AND COLLEGE CODE
            foreach (var testConfig in config.Tests)
            {
                // Find college by CODE (not ID)
                var college = _context.Colleges.FirstOrDefault(c => c.Code == testConfig.CollegeCode);

                if (college == null)
                {
                    result.Warnings.Add($"Test '{testConfig.Name}' skipped - college code '{testConfig.CollegeCode}' not found");
                    continue;
                }

                var existingTest = _context.Tests.FirstOrDefault(t => t.Code == testConfig.Code);

                if (existingTest == null)
                {
                    var test = new Test
                    {
                        // Don't set ID - let it auto-generate
                        Name = testConfig.Name,
                        Code = testConfig.Code,
                        Description = testConfig.Description,
                        CollegeId = college.Id, // Use current DB's college ID
                        TestDate = testConfig.TestDate,
                        RegistrationStartDate = testConfig.RegistrationStartDate,
                        RegistrationEndDate = testConfig.RegistrationEndDate,
                        IsActive = testConfig.IsActive,
                        CreatedDate = DateTime.Now
                    };

                    _context.Tests.Add(test);
                    result.TestsImported++;
                }
            }

            await _context.SaveChangesAsync();

            // Import students - MATCH BY COLLEGE CODE AND TEST CODE
            if (config.Students != null && config.Students.Count > 0)
            {
                foreach (var studentConfig in config.Students)
                {
                    // Find college and test by CODE
                    var college = _context.Colleges.FirstOrDefault(c => c.Code == studentConfig.CollegeCode);
                    var test = _context.Tests.FirstOrDefault(t => t.Code == studentConfig.TestCode);

                    if (college == null || test == null)
                    {
                        result.Warnings.Add($"Student '{studentConfig.RollNumber}' skipped - college or test not found");
                        continue;
                    }

                    var existingStudent = _context.Students.FirstOrDefault(s => s.RollNumber == studentConfig.RollNumber
                                                                              && s.CollegeId == college.Id
                                                                              && s.TestId == test.Id);

                    if (existingStudent == null)
                    {
                        var student = new Student
                        {
                            RollNumber = studentConfig.RollNumber,
                            Name = studentConfig.Name,
                            CNIC = studentConfig.CNIC,
                            CollegeId = college.Id, // Use current DB's IDs
                            TestId = test.Id,
                            StudentPhoto = studentConfig.StudentPhoto,
                            FingerprintTemplate = studentConfig.FingerprintTemplate,
                            FingerprintImage = studentConfig.FingerprintImage,
                            FingerprintImageWidth = studentConfig.FingerprintImageWidth,
                            FingerprintImageHeight = studentConfig.FingerprintImageHeight,
                            RegistrationDate = studentConfig.RegistrationDate,
                            DeviceId = studentConfig.DeviceId,
                            IsVerified = false
                        };

                        _context.Students.Add(student);
                        result.StudentsImported++;
                    }
                }

                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Merge/update configuration with existing database
        /// </summary>
        private async Task MergeConfigurationAsync(MasterConfiguration config, ImportResult result)
        {
            // Update existing colleges and add new ones - MATCH BY CODE
            foreach (var collegeConfig in config.Colleges)
            {
                var existing = _context.Colleges.FirstOrDefault(c => c.Code == collegeConfig.Code);

                if (existing != null)
                {
                    existing.Name = collegeConfig.Name;
                    existing.Address = collegeConfig.Address;
                    existing.ContactPerson = collegeConfig.ContactPerson;
                    existing.ContactPhone = collegeConfig.ContactPhone;
                    existing.ContactEmail = collegeConfig.ContactEmail;
                    existing.IsActive = collegeConfig.IsActive;
                    existing.LastModifiedDate = DateTime.Now;
                    result.CollegesUpdated++;
                }
                else
                {
                    var college = new College
                    {
                        Name = collegeConfig.Name,
                        Code = collegeConfig.Code,
                        Address = collegeConfig.Address,
                        ContactPerson = collegeConfig.ContactPerson,
                        ContactPhone = collegeConfig.ContactPhone,
                        ContactEmail = collegeConfig.ContactEmail,
                        IsActive = collegeConfig.IsActive,
                        CreatedDate = DateTime.Now
                    };
                    _context.Colleges.Add(college);
                    result.CollegesImported++;
                }
            }

            await _context.SaveChangesAsync();

            // Update existing tests and add new ones - MATCH BY CODE AND COLLEGE CODE
            foreach (var testConfig in config.Tests)
            {
                // Find college by CODE
                var college = _context.Colleges.FirstOrDefault(c => c.Code == testConfig.CollegeCode);

                if (college == null)
                {
                    result.Warnings.Add($"Test '{testConfig.Name}' skipped - college code '{testConfig.CollegeCode}' not found");
                    continue;
                }

                var existing = _context.Tests.FirstOrDefault(t => t.Code == testConfig.Code);

                if (existing != null)
                {
                    existing.Name = testConfig.Name;
                    existing.Description = testConfig.Description;
                    existing.CollegeId = college.Id; // Update to current DB's college ID
                    existing.TestDate = testConfig.TestDate;
                    existing.RegistrationStartDate = testConfig.RegistrationStartDate;
                    existing.RegistrationEndDate = testConfig.RegistrationEndDate;
                    existing.IsActive = testConfig.IsActive;
                    existing.LastModifiedDate = DateTime.Now;
                    result.TestsUpdated++;
                }
                else
                {
                    var test = new Test
                    {
                        Name = testConfig.Name,
                        Code = testConfig.Code,
                        Description = testConfig.Description,
                        CollegeId = college.Id,
                        TestDate = testConfig.TestDate,
                        RegistrationStartDate = testConfig.RegistrationStartDate,
                        RegistrationEndDate = testConfig.RegistrationEndDate,
                        IsActive = testConfig.IsActive,
                        CreatedDate = DateTime.Now
                    };
                    _context.Tests.Add(test);
                    result.TestsImported++;
                }
            }

            await _context.SaveChangesAsync();

            // Import students - MATCH BY ROLL NUMBER + COLLEGE CODE + TEST CODE
            if (config.Students != null && config.Students.Count > 0)
            {
                foreach (var studentConfig in config.Students)
                {
                    // Find college and test by CODE
                    var college = _context.Colleges.FirstOrDefault(c => c.Code == studentConfig.CollegeCode);
                    var test = _context.Tests.FirstOrDefault(t => t.Code == studentConfig.TestCode);

                    if (college == null || test == null)
                    {
                        result.Warnings.Add($"Student '{studentConfig.RollNumber}' skipped - college or test not found");
                        continue;
                    }

                    var existing = _context.Students.FirstOrDefault(s => s.RollNumber == studentConfig.RollNumber
                                                                      && s.CollegeId == college.Id
                                                                      && s.TestId == test.Id);

                    if (existing == null)
                    {
                        var student = new Student
                        {
                            RollNumber = studentConfig.RollNumber,
                            Name = studentConfig.Name,
                            CNIC = studentConfig.CNIC,
                            CollegeId = college.Id,
                            TestId = test.Id,
                            StudentPhoto = studentConfig.StudentPhoto,
                            FingerprintTemplate = studentConfig.FingerprintTemplate,
                            FingerprintImage = studentConfig.FingerprintImage,
                            FingerprintImageWidth = studentConfig.FingerprintImageWidth,
                            FingerprintImageHeight = studentConfig.FingerprintImageHeight,
                            RegistrationDate = studentConfig.RegistrationDate,
                            DeviceId = studentConfig.DeviceId,
                            IsVerified = false
                        };

                        _context.Students.Add(student);
                        result.StudentsImported++;
                    }
                }

                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Check if auto-import file exists
        /// </summary>
        public static bool AutoImportFileExists()
        {
            var appPath = AppDomain.CurrentDomain.BaseDirectory;
            var configFile = Path.Combine(appPath, "MasterConfig.bdat");
            return File.Exists(configFile);
        }

        /// <summary>
        /// Get auto-import file path
        /// </summary>
        public static string GetAutoImportFilePath()
        {
            var appPath = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appPath, "MasterConfig.bdat");
        }

        /// <summary>
        /// Check if database is empty (needs configuration)
        /// </summary>
        public bool IsDatabaseEmpty()
        {
            return !_context.Colleges.Any() && !_context.Tests.Any();
        }
    }

    /// <summary>
    /// Master configuration model
    /// </summary>
    public class MasterConfiguration
    {
        public string Version { get; set; } = "1.0";
        public DateTime ExportDate { get; set; }
        public List<CollegeConfig> Colleges { get; set; } = new();
        public List<TestConfig> Tests { get; set; } = new();
        public List<StudentConfig> Students { get; set; } = new();
    }

    /// <summary>
    /// College configuration
    /// </summary>
    public class CollegeConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Test configuration
    /// </summary>
    public class TestConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CollegeId { get; set; }
        public string CollegeCode { get; set; } = string.Empty; // ADDED FOR CODE-BASED MATCHING
        public DateTime TestDate { get; set; }
        public DateTime RegistrationStartDate { get; set; }
        public DateTime RegistrationEndDate { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Student configuration
    /// </summary>
    public class StudentConfig
    {
        public int Id { get; set; }
        public string RollNumber { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? CNIC { get; set; }
        public int CollegeId { get; set; }
        public string CollegeCode { get; set; } = string.Empty; // ADDED FOR CODE-BASED MATCHING
        public int TestId { get; set; }
        public string TestCode { get; set; } = string.Empty; // ADDED FOR CODE-BASED MATCHING
        public byte[]? StudentPhoto { get; set; }
        public byte[]? FingerprintTemplate { get; set; }
        public byte[]? FingerprintImage { get; set; }
        public int FingerprintImageWidth { get; set; }
        public int FingerprintImageHeight { get; set; }
        public DateTime RegistrationDate { get; set; }
        public string? DeviceId { get; set; }
    }

    /// <summary>
    /// Import result
    /// </summary>
    public class ImportResult
    {
        public bool Success { get; set; }
        public bool WasUpdate { get; set; }
        public DateTime ImportDate { get; set; }
        public int CollegesImported { get; set; }
        public int CollegesUpdated { get; set; }
        public int TestsImported { get; set; }
        public int TestsUpdated { get; set; }
        public int StudentsImported { get; set; }
        public List<string> Warnings { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;

        public string GetSummary()
        {
            if (!Success)
                return $"Import failed: {ErrorMessage}";

            var message = WasUpdate ? "Configuration Updated:\n\n" : "Configuration Imported:\n\n";

            if (CollegesImported > 0)
                message += $"✅ {CollegesImported} new colleges added\n";
            if (CollegesUpdated > 0)
                message += $"🔄 {CollegesUpdated} colleges updated\n";
            if (TestsImported > 0)
                message += $"✅ {TestsImported} new tests added\n";
            if (TestsUpdated > 0)
                message += $"🔄 {TestsUpdated} tests updated\n";
            if (StudentsImported > 0)
                message += $"✅ {StudentsImported} students imported\n";

            if (Warnings.Count > 0)
            {
                message += $"\n⚠️ {Warnings.Count} warnings:\n";
                foreach (var warning in Warnings)
                {
                    message += $"  • {warning}\n";
                }
            }

            return message;
        }
    }
}