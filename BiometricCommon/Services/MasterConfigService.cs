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
    /// Service for exporting and importing master configuration (colleges and tests)
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
        /// Export master configuration (colleges and tests) to encrypted file
        /// </summary>
        public async Task<string> ExportMasterConfigAsync(string outputPath)
        {
            try
            {
                // Get all colleges and tests (no students)
                var colleges = await Task.Run(() => _context.Colleges.ToList());
                var tests = await Task.Run(() => _context.Tests.ToList());

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
                        TestDate = t.TestDate,
                        RegistrationStartDate = t.RegistrationStartDate,
                        RegistrationEndDate = t.RegistrationEndDate,
                        IsActive = t.IsActive
                    }).ToList()
                };

                // Serialize to JSON
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = false
                });

                // Encrypt and save (Encrypt returns Base64 string)
                var encrypted = EncryptionService.Encrypt(json, "MasterConfig2024!");
                await File.WriteAllTextAsync(outputPath, encrypted);

                return $"Exported {colleges.Count} colleges and {tests.Count} tests successfully!";
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export master configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Import master configuration from encrypted file
        /// </summary>
        public async Task<ImportResult> ImportMasterConfigAsync(string filePath)
        {
            try
            {
                // Read and decrypt file
                var encrypted = await File.ReadAllTextAsync(filePath);
                var json = EncryptionService.Decrypt(encrypted, "MasterConfig2024!");

                // Deserialize
                var config = JsonSerializer.Deserialize<MasterConfiguration>(json);
                if (config == null)
                    throw new Exception("Invalid configuration file");

                var result = new ImportResult
                {
                    Success = true,
                    ImportDate = DateTime.Now
                };

                // Check if database already has data
                var existingColleges = _context.Colleges.ToList();
                var existingTests = _context.Tests.ToList();

                if (existingColleges.Count > 0 || existingTests.Count > 0)
                {
                    // Database not empty - merge/update mode
                    result.WasUpdate = true;
                    await MergeConfigurationAsync(config, result);
                }
                else
                {
                    // Empty database - fresh import
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
            // Import colleges
            foreach (var collegeConfig in config.Colleges)
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

            await _context.SaveChangesAsync();

            // Now import tests (need college IDs from database)
            var collegeMapping = _context.Colleges.ToDictionary(c => c.Code, c => c.Id);

            foreach (var testConfig in config.Tests)
            {
                // Find the college by code
                var college = _context.Colleges.FirstOrDefault(c => c.Id == testConfig.CollegeId);
                if (college == null)
                {
                    result.Warnings.Add($"Test '{testConfig.Name}' skipped - college not found");
                    continue;
                }

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

        /// <summary>
        /// Merge/update configuration with existing database
        /// </summary>
        private async Task MergeConfigurationAsync(MasterConfiguration config, ImportResult result)
        {
            // Update existing colleges and add new ones
            foreach (var collegeConfig in config.Colleges)
            {
                var existing = _context.Colleges.FirstOrDefault(c => c.Code == collegeConfig.Code);

                if (existing != null)
                {
                    // Update existing
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
                    // Add new
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

            // Update existing tests and add new ones
            foreach (var testConfig in config.Tests)
            {
                var college = _context.Colleges.FirstOrDefault(c => c.Id == testConfig.CollegeId);
                if (college == null)
                {
                    result.Warnings.Add($"Test '{testConfig.Name}' skipped - college not found");
                    continue;
                }

                var existing = _context.Tests.FirstOrDefault(t => t.Code == testConfig.Code);

                if (existing != null)
                {
                    // Update existing
                    existing.Name = testConfig.Name;
                    existing.Description = testConfig.Description;
                    existing.TestDate = testConfig.TestDate;
                    existing.RegistrationStartDate = testConfig.RegistrationStartDate;
                    existing.RegistrationEndDate = testConfig.RegistrationEndDate;
                    existing.IsActive = testConfig.IsActive;
                    existing.LastModifiedDate = DateTime.Now;
                    result.TestsUpdated++;
                }
                else
                {
                    // Add new
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
        public DateTime TestDate { get; set; }
        public DateTime RegistrationStartDate { get; set; }
        public DateTime RegistrationEndDate { get; set; }
        public bool IsActive { get; set; }
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