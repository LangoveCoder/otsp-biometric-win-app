using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiometricCommon.Models
{
    /// <summary>
    /// Represents a student registered in the biometric verification system
    /// </summary>
    public class Student
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string RollNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string CNIC { get; set; } = string.Empty;

        /// <summary>
        /// Student photo from Excel (ID photo taken before test)
        /// </summary>
        public byte[]? StudentPhoto { get; set; }

        [Required]
        public int CollegeId { get; set; }

        [Required]
        public int TestId { get; set; }

        /// <summary>
        /// Fingerprint template stored as byte array (scanner-specific format)
        /// </summary>
        [Required]
        public byte[] FingerprintTemplate { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Fingerprint image for display purposes (grayscale pixel data)
        /// </summary>
        public byte[]? FingerprintImage { get; set; }

        public int FingerprintImageWidth { get; set; }

        public int FingerprintImageHeight { get; set; }

        public DateTime RegistrationDate { get; set; } = DateTime.Now;

        public DateTime? LastModifiedDate { get; set; }

        public bool IsVerified { get; set; } = false;

        public DateTime? VerificationDate { get; set; }

        /// <summary>
        /// Which laptop/device registered this student
        /// </summary>
        [MaxLength(50)]
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Navigation property to College
        /// </summary>
        [ForeignKey("CollegeId")]
        public virtual College? College { get; set; }

        /// <summary>
        /// Navigation property to Test
        /// </summary>
        [ForeignKey("TestId")]
        public virtual Test? Test { get; set; }
    }

    /// <summary>
    /// Represents a college/institution in the system
    /// </summary>
    public class College
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Address { get; set; } = string.Empty;

        [MaxLength(100)]
        public string ContactPerson { get; set; } = string.Empty;

        [MaxLength(20)]
        public string ContactPhone { get; set; } = string.Empty;

        [MaxLength(100)]
        public string ContactEmail { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? LastModifiedDate { get; set; }

        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Represents a test/exam in the system
    /// Each test belongs to exactly ONE college
    /// </summary>
    public class Test
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The college this test belongs to (One-to-One relationship)
        /// </summary>
        [Required]
        public int CollegeId { get; set; }

        public DateTime TestDate { get; set; }

        public DateTime RegistrationStartDate { get; set; } = DateTime.Now;

        public DateTime RegistrationEndDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? LastModifiedDate { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Navigation property to College
        /// </summary>
        [ForeignKey("CollegeId")]
        public virtual College? College { get; set; }
    }

    /// <summary>
    /// Represents a college administrator account
    /// </summary>
    public class CollegeAdmin
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public int CollegeId { get; set; }

        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? LastLoginDate { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Navigation property to College
        /// </summary>
        [ForeignKey("CollegeId")]
        public virtual College? College { get; set; }
    }

    /// <summary>
    /// Represents a verification attempt log entry
    /// </summary>
    public class VerificationLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public DateTime VerificationDateTime { get; set; } = DateTime.Now;

        [Required]
        public bool IsSuccessful { get; set; }

        [Required]
        [MaxLength(50)]
        public string VerificationType { get; set; } = string.Empty; // "Biometric" or "ManualOverride"

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        [MaxLength(100)]
        public string VerifiedBy { get; set; } = string.Empty;

        public int MatchConfidence { get; set; } = 0; // 0-100 percentage

        /// <summary>
        /// Navigation property to Student
        /// </summary>
        [ForeignKey("StudentId")]
        public virtual Student? Student { get; set; }
    }

    /// <summary>
    /// Represents system configuration settings
    /// </summary>
    public class SystemSettings
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime ModifiedDate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Represents data package for college distribution
    /// </summary>
    public class ExportPackage
    {
        public int CollegeId { get; set; }
        public string CollegeName { get; set; } = string.Empty;
        public string CollegeCode { get; set; } = string.Empty;
        public int TestId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string TestCode { get; set; } = string.Empty;
        public DateTime ExportDate { get; set; } = DateTime.Now;
        public string EncryptionKey { get; set; } = string.Empty;
        public List<ExportStudent> Students { get; set; } = new List<ExportStudent>();
        public string PackageVersion { get; set; } = "1.0";
        public string Checksum { get; set; } = string.Empty;
        public CollegeAdminCredentials? AdminCredentials { get; set; }
    }

    /// <summary>
    /// Student data for export package
    /// </summary>
    public class ExportStudent
    {
        public string RollNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CNIC { get; set; } = string.Empty;
        public byte[]? StudentPhoto { get; set; }
        public byte[] FingerprintTemplate { get; set; } = Array.Empty<byte>();
        public byte[]? FingerprintImage { get; set; }
        public int FingerprintImageWidth { get; set; }
        public int FingerprintImageHeight { get; set; }
        public DateTime RegistrationDate { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    /// <summary>
    /// College admin credentials for export package
    /// </summary>
    public class CollegeAdminCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Dashboard statistics model
    /// </summary>
    public class DashboardStats
    {
        public int TotalColleges { get; set; }
        public int ActiveColleges { get; set; }
        public int TotalTests { get; set; }
        public int ActiveTests { get; set; }
        public int TotalStudents { get; set; }
        public int VerifiedStudents { get; set; }
        public int PendingVerification { get; set; }
        public int TodayVerifications { get; set; }
        public double VerificationRate { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Verification result model
    /// </summary>
    public class VerificationResult
    {
        public bool IsSuccessful { get; set; }
        public string Message { get; set; } = string.Empty;
        public Student? Student { get; set; }
        public int MatchConfidence { get; set; }
        public string VerificationType { get; set; } = string.Empty;
    }
}