using Microsoft.EntityFrameworkCore;
using BiometricCommon.Models;
using BiometricCommon.Encryption;
using System;
using System.IO;

namespace BiometricCommon.Database
{
    /// <summary>
    /// Entity Framework Core Database Context for Biometric Verification System
    /// Uses SQLite for offline, portable database storage
    /// </summary>
    public class BiometricContext : DbContext
    {
        /// <summary>
        /// Students registered in the system
        /// </summary>
        public DbSet<Student> Students { get; set; }

        /// <summary>
        /// Colleges participating in the verification system
        /// </summary>
        public DbSet<College> Colleges { get; set; }

        /// <summary>
        /// Tests/Exams configured in the system
        /// Each test belongs to one college
        /// </summary>
        public DbSet<Test> Tests { get; set; }

        /// <summary>
        /// College administrator accounts
        /// </summary>
        public DbSet<CollegeAdmin> CollegeAdmins { get; set; }

        /// <summary>
        /// Verification attempt logs for audit trail
        /// </summary>
        public DbSet<VerificationLog> VerificationLogs { get; set; }

        /// <summary>
        /// System-wide configuration settings
        /// </summary>
        public DbSet<SystemSettings> SystemSettings { get; set; }

        private readonly string _databasePath;

        /// <summary>
        /// Default constructor - uses default database location
        /// </summary>
        public BiometricContext()
        {
            _databasePath = GetDefaultDatabasePath();
        }

        /// <summary>
        /// Constructor with custom database path
        /// </summary>
        /// <param name="databasePath">Full path to SQLite database file</param>
        public BiometricContext(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>
        /// Configure database connection
        /// </summary>
        /// <param name="optionsBuilder">Options builder</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Ensure directory exists
                string? directory = Path.GetDirectoryName(_databasePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                optionsBuilder.UseSqlite($"Data Source={_databasePath}");
            }
        }

        /// <summary>
        /// Configure entity relationships and constraints
        /// </summary>
        /// <param name="modelBuilder">Model builder for configuration</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Student entity
            modelBuilder.Entity<Student>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.RollNumber)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.FingerprintTemplate)
                    .IsRequired();

                entity.Property(e => e.DeviceId)
                    .HasMaxLength(50);

                entity.HasOne(e => e.College)
                    .WithMany()
                    .HasForeignKey(e => e.CollegeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Test)
                    .WithMany()
                    .HasForeignKey(e => e.TestId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Create composite unique index (one student per roll number per college per test)
                entity.HasIndex(e => new { e.RollNumber, e.CollegeId, e.TestId })
                    .IsUnique();

                // Create index for verification queries
                entity.HasIndex(e => e.IsVerified);
                
                // Create index for device tracking
                entity.HasIndex(e => e.DeviceId);
            });

            // Configure College entity
            modelBuilder.Entity<College>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.HasIndex(e => e.Code)
                    .IsUnique();

                entity.HasIndex(e => e.IsActive);
            });

            // Configure Test entity
            modelBuilder.Entity<Test>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Code)
                    .IsRequired()
                    .HasMaxLength(50);

                // Each test belongs to one college
                entity.HasOne(e => e.College)
                    .WithMany()
                    .HasForeignKey(e => e.CollegeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.Code)
                    .IsUnique();

                entity.HasIndex(e => e.IsActive);

                entity.HasIndex(e => e.TestDate);
                
                entity.HasIndex(e => e.CollegeId);
            });

            // Configure CollegeAdmin entity
            modelBuilder.Entity<CollegeAdmin>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Username)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.PasswordHash)
                    .IsRequired();

                entity.HasOne(e => e.College)
                    .WithMany()
                    .HasForeignKey(e => e.CollegeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.Username)
                    .IsUnique();

                entity.HasIndex(e => e.IsActive);
            });

            // Configure VerificationLog entity
            modelBuilder.Entity<VerificationLog>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Student)
                    .WithMany()
                    .HasForeignKey(e => e.StudentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.VerificationDateTime);

                entity.HasIndex(e => e.IsSuccessful);

                entity.HasIndex(e => new { e.StudentId, e.VerificationDateTime });
            });

            // Configure SystemSettings entity
            modelBuilder.Entity<SystemSettings>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Key)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.HasIndex(e => e.Key)
                    .IsUnique();
            });

            // Seed initial data
            SeedInitialData(modelBuilder);
        }

        /// <summary>
        /// Seed initial system settings
        /// </summary>
        /// <param name="modelBuilder">Model builder</param>
        private void SeedInitialData(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SystemSettings>().HasData(
                new SystemSettings
                {
                    Id = 1,
                    Key = "MaxRetryAttempts",
                    Value = "3",
                    Description = "Maximum fingerprint verification retry attempts",
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                },
                new SystemSettings
                {
                    Id = 2,
                    Key = "FingerprintMatchThreshold",
                    Value = "70",
                    Description = "Fingerprint match confidence threshold (0-100)",
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                },
                new SystemSettings
                {
                    Id = 3,
                    Key = "ApplicationVersion",
                    Value = "1.0.0",
                    Description = "Current application version",
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                },
                new SystemSettings
                {
                    Id = 4,
                    Key = "SuperAdminPassword",
                    Value = EncryptionService.HashPassword("admin123"),
                    Description = "Superadmin master password (hashed)",
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                },
                new SystemSettings
                {
                    Id = 5,
                    Key = "ManualOverridePassword",
                    Value = EncryptionService.HashPassword("override123"),
                    Description = "Manual override password (hashed)",
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                }
            );
        }

        /// <summary>
        /// Get database file path
        /// </summary>
        /// <returns>Full path to database file</returns>
        public string GetDatabasePath()
        {
            return _databasePath;
        }

        /// <summary>
        /// Get default database path in AppData
        /// </summary>
        /// <returns>Default database path</returns>
        private static string GetDefaultDatabasePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appDataPath, "BiometricVerification");

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            return Path.Combine(appFolder, "BiometricData.db");
        }

        /// <summary>
        /// Backup database to specified location
        /// </summary>
        /// <param name="backupPath">Destination path for backup</param>
        public void BackupDatabase(string backupPath)
        {
            if (File.Exists(_databasePath))
            {
                File.Copy(_databasePath, backupPath, true);
            }
        }

        /// <summary>
        /// Optimize database (VACUUM command)
        /// </summary>
        public void OptimizeDatabase()
        {
            Database.ExecuteSqlRaw("VACUUM");
        }

        /// <summary>
        /// Initialize database - create if doesn't exist
        /// </summary>
        public void InitializeDatabase()
        {
            Database.EnsureCreated();
        }

        /// <summary>
        /// Get setting value by key
        /// </summary>
        /// <param name="key">Setting key</param>
        /// <returns>Setting value or null</returns>
        public string? GetSettingValue(string key)
        {
            var setting = SystemSettings.FirstOrDefault(s => s.Key == key);
            return setting?.Value;
        }

        /// <summary>
        /// Update setting value
        /// </summary>
        /// <param name="key">Setting key</param>
        /// <param name="value">New value</param>
        public void UpdateSettingValue(string key, string value)
        {
            var setting = SystemSettings.FirstOrDefault(s => s.Key == key);
            if (setting != null)
            {
                setting.Value = value;
                setting.ModifiedDate = DateTime.Now;
                SaveChanges();
            }
        }
    }
}
