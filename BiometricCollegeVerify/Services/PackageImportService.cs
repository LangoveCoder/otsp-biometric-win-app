using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
                string tempDir = Path.Combine(Path.GetTempPath(), $"BiometricImport_{Guid.NewGuid()}");
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
                    string packageInfoJson = await DecryptPackageInfoAsync(encryptedInfoPath);
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
                        Directory.Delete(tempDir, true);
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
        private async Task<string> DecryptPackageInfoAsync(string encryptedPath)
        {
            // Try common encryption keys (college code based)
            // In a real implementation, you'd have a way to get the correct key
            try
            {
                byte[] encryptedData = await File.ReadAllBytesAsync(encryptedPath);

                // Try to decrypt with various keys
                // For now, we'll use a known key format
                // In production, the key would be provided or derived

                // Read the encrypted file and try to decrypt
                // This is simplified - real implementation would need proper key management
                string tempDecrypted = Path.GetTempFileName();

                try
                {
                    // Try to decrypt with standard key
                    // Note: This would need to match the encryption key used in PackageGenerationService
                    var content = File.ReadAllText(encryptedPath);
                    return content; // Simplified for now
                }
                catch
                {
                    // If decryption fails, try alternative approaches
                    throw new Exception("Could not decrypt package information");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to decrypt package info: {ex.Message}");
            }
        }

        /// <summary>
        /// Verify database integrity
        /// </summary>
        private async Task<bool> VerifyDatabaseAsync(string dbPath)
        {
            try
            {
                using (var context = new BiometricContext($"Data Source={dbPath}"))
                {
                    // Check if database can be opened
                    await context.Database.OpenConnectionAsync();

                    // Verify required tables exist
                    var hasStudents = await context.Students.AnyAsync();
                    var hasColleges = await context.Colleges.AnyAsync();
                    var hasTests = await context.Tests.AnyAsync();

                    return hasColleges && hasTests; // Students can be zero
                }
            }
            catch
            {
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
        /// Get database connection string
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