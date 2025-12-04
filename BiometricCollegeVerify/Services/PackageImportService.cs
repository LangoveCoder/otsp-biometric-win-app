using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using BiometricCommon.Database;
using BiometricCommon.Encryption;
using Microsoft.EntityFrameworkCore;

namespace BiometricCollegeVerify.Services
{
    /// <summary>
    /// Service for importing college verification packages
    /// </summary>
    public class PackageImportService
    {
        private readonly string _appDataPath;
        private readonly string _databasePath;

        public PackageImportService()
        {
            _appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BiometricVerification",
                "CollegeVerification"
            );

            Directory.CreateDirectory(_appDataPath);

            _databasePath = Path.Combine(_appDataPath, "CollegeData.db");
        }

        /// <summary>
        /// Check if a package has been imported
        /// </summary>
        public bool IsPackageImported()
        {
            return File.Exists(_databasePath) && File.Exists(Path.Combine(_appDataPath, "PackageInfo.json"));
        }

        /// <summary>
        /// Import a college verification package
        /// </summary>
        public async Task<ImportResult> ImportPackageAsync(string packagePath)
        {
            var result = new ImportResult();

            try
            {
                if (!File.Exists(packagePath))
                {
                    result.Success = false;
                    result.ErrorMessage = "Package file not found";
                    return result;
                }

                // Create temp directory for extraction
                string tempDir = Path.Combine(Path.GetTempPath(), $"BiometricImport_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    // Extract ZIP package
                    ZipFile.ExtractToDirectory(packagePath, tempDir);

                    // Find encrypted files
                    string encryptedDbPath = Path.Combine(tempDir, "CollegeData.encrypted");
                    string encryptedInfoPath = Path.Combine(tempDir, "PackageInfo.encrypted");

                    if (!File.Exists(encryptedDbPath) || !File.Exists(encryptedInfoPath))
                    {
                        result.Success = false;
                        result.ErrorMessage = "Invalid package format - missing encrypted files";
                        return result;
                    }

                    // Decrypt package info to get encryption key
                    string packageInfoJson = await DecryptPackageInfoAsync(encryptedInfoPath, tempDir);
                    var packageInfo = JsonSerializer.Deserialize<PackageInfo>(packageInfoJson);

                    if (packageInfo == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Invalid package information";
                        return result;
                    }

                    // Decrypt database
                    string decryptedDbPath = Path.Combine(tempDir, "CollegeData.db");
                    EncryptionService.DecryptFile(encryptedDbPath, decryptedDbPath, packageInfo.EncryptionKey);

                    // Verify database integrity
                    if (!await VerifyDatabaseAsync(decryptedDbPath))
                    {
                        result.Success = false;
                        result.ErrorMessage = "Database verification failed";
                        return result;
                    }

                    // Copy database to app data
                    if (File.Exists(_databasePath))
                        File.Delete(_databasePath);

                    File.Copy(decryptedDbPath, _databasePath);

                    // Save package info
                    File.WriteAllText(Path.Combine(_appDataPath, "PackageInfo.json"), packageInfoJson);

                    // Populate result
                    result.Success = true;
                    result.CollegeName = packageInfo.CollegeName;
                    result.TestName = packageInfo.TestName;
                    result.TotalStudents = packageInfo.TotalStudents;
                    result.PackageDate = packageInfo.PackageDate;
                }
                finally
                {
                    // Cleanup temp directory
                    if (Directory.Exists(tempDir))
                    {
                        try
                        {
                            Directory.Delete(tempDir, true);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Import failed: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Decrypt package info file
        /// </summary>
        private async Task<string> DecryptPackageInfoAsync(string encryptedPath, string tempDir)
        {
            try
            {
                // Check for plain metadata file
                string plainMetadataPath = Path.Combine(tempDir, "PackageMetadata.json");

                if (!File.Exists(plainMetadataPath))
                {
                    throw new Exception("Package metadata not found. This may be an older package format.");
                }

                // Read plain metadata to get codes for key generation
                string metadataJson = await File.ReadAllTextAsync(plainMetadataPath);
                using var metadataDoc = JsonSerializer.Deserialize<JsonDocument>(metadataJson);

                if (metadataDoc == null)
                {
                    throw new Exception("Invalid package metadata format.");
                }

                string collegeCode = metadataDoc.RootElement.GetProperty("CollegeCode").GetString() ?? "";
                string testCode = metadataDoc.RootElement.GetProperty("TestCode").GetString() ?? "";

                if (string.IsNullOrEmpty(collegeCode) || string.IsNullOrEmpty(testCode))
                {
                    throw new Exception("College or test code missing from metadata.");
                }

                // Generate decryption key using the same method as generation
                string decryptionKey = EncryptionService.GenerateCollegeKey(collegeCode, testCode);

                // Decrypt the package info file
                string decryptedPath = Path.Combine(tempDir, "PackageInfo_decrypted.json");
                EncryptionService.DecryptFile(encryptedPath, decryptedPath, decryptionKey);

                // Read and return decrypted JSON
                string decryptedJson = await File.ReadAllTextAsync(decryptedPath);

                // Clean up temp file
                if (File.Exists(decryptedPath))
                    File.Delete(decryptedPath);

                return decryptedJson;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to decrypt package info: {ex.Message}");
            }
        }

        /// <summary>
        /// Verify database integrity - ENHANCED for new Student model compatibility
        /// </summary>
        private async Task<bool> VerifyDatabaseAsync(string dbPath)
        {
            try
            {
                using (var context = new BiometricContext(dbPath))
                {
                    // ✅ Ensure database schema is created and ready
                    await context.Database.EnsureCreatedAsync();

                    // ✅ Verify we can actually connect to the database
                    var canConnect = await context.Database.CanConnectAsync();
                    if (!canConnect)
                    {
                        System.Diagnostics.Debug.WriteLine("❌ Database exists but cannot connect");
                        return false;
                    }

                    // ✅ Open connection for verification
                    await context.Database.OpenConnectionAsync();

                    // ✅ Verify required tables exist and have data
                    var hasColleges = await context.Colleges.AnyAsync();
                    var hasTests = await context.Tests.AnyAsync();

                    // ✅ Explicitly close connection to prevent locks
                    await context.Database.CloseConnectionAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Database verified - Colleges: {hasColleges}, Tests: {hasTests}");

                    // Students can be zero, but colleges and tests must exist
                    return hasColleges && hasTests;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Database verification failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get college information from imported package
        /// </summary>
        public CollegeInfo? GetCollegeInfo()
        {
            try
            {
                string infoPath = Path.Combine(_appDataPath, "PackageInfo.json");
                if (!File.Exists(infoPath))
                    return null;

                string json = File.ReadAllText(infoPath);
                var packageInfo = JsonSerializer.Deserialize<PackageInfo>(json);

                if (packageInfo == null)
                    return null;

                return new CollegeInfo
                {
                    CollegeName = packageInfo.CollegeName,
                    CollegeCode = packageInfo.CollegeCode,
                    TestName = packageInfo.TestName,
                    TotalStudents = packageInfo.TotalStudents,
                    PackageDate = packageInfo.PackageDate
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get database path for use by other services
        /// </summary>
        public string GetDatabasePath()
        {
            return _databasePath;
        }
    }

    #region Helper Classes

    public class ImportResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string CollegeName { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public int TotalStudents { get; set; }
        public DateTime PackageDate { get; set; }
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

    public class CollegeInfo
    {
        public string CollegeName { get; set; } = string.Empty;
        public string CollegeCode { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public int TotalStudents { get; set; }
        public DateTime PackageDate { get; set; }
    }

    #endregion
}