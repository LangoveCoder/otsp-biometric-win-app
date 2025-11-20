using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BiometricCommon.Models;
using BiometricCommon.Database;

namespace BiometricCollegeVerify.Services
{
    /// <summary>
    /// Service for student verification
    /// </summary>
    public class VerificationService
    {
        private readonly string _databasePath;

        public VerificationService(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>
        /// Verify a student by fingerprint
        /// </summary>
        public async Task<VerificationResult> VerifyStudentAsync(byte[] fingerprintTemplate, string verifiedBy = "System")
        {
            var result = new VerificationResult
            {
                IsSuccessful = false,
                VerificationDateTime = DateTime.Now,
                VerifiedBy = verifiedBy
            };

            try
            {
                using (var context = new BiometricContext($"Data Source={_databasePath}"))
                {
                    // Get all students
                    var students = await context.Students
                        .Include(s => s.College)
                        .Include(s => s.Test)
                        .ToListAsync();

                    if (students.Count == 0)
                    {
                        result.Message = "No students in database";
                        return result;
                    }

                    // Match fingerprint
                    var matchResult = MatchFingerprint(fingerprintTemplate, students);

                    if (matchResult.Matched)
                    {
                        result.IsSuccessful = true;
                        result.Student = matchResult.Student;
                        result.MatchConfidence = matchResult.Confidence;
                        result.VerificationType = "Biometric";
                        result.Message = "Student verified successfully";

                        // Update student verification status
                        var student = await context.Students.FindAsync(matchResult.Student!.Id);
                        if (student != null)
                        {
                            student.IsVerified = true;
                            student.VerificationDate = DateTime.Now;
                        }

                        // Log verification
                        var log = new VerificationLog
                        {
                            StudentId = matchResult.Student!.Id,
                            VerificationDateTime = DateTime.Now,
                            IsSuccessful = true,
                            VerificationType = "Biometric",
                            MatchConfidence = matchResult.Confidence,
                            VerifiedBy = verifiedBy,
                            Remarks = "Fingerprint matched successfully"
                        };

                        context.VerificationLogs.Add(log);
                        await context.SaveChangesAsync();
                    }
                    else
                    {
                        result.IsSuccessful = false;
                        result.Message = "Fingerprint not matched";
                        result.VerificationType = "Biometric";

                        // Log failed attempt (without student ID)
                        var log = new VerificationLog
                        {
                            StudentId = 0, // No student matched
                            VerificationDateTime = DateTime.Now,
                            IsSuccessful = false,
                            VerificationType = "Biometric",
                            MatchConfidence = 0,
                            VerifiedBy = verifiedBy,
                            Remarks = "Fingerprint not matched in database"
                        };

                        context.VerificationLogs.Add(log);
                        await context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.Message = $"Verification error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Manual override verification
        /// </summary>
        public async Task<VerificationResult> ManualOverrideAsync(string rollNumber, string verifiedBy, string remarks)
        {
            var result = new VerificationResult
            {
                IsSuccessful = false,
                VerificationDateTime = DateTime.Now,
                VerifiedBy = verifiedBy,
                VerificationType = "ManualOverride"
            };

            try
            {
                using (var context = new BiometricContext($"Data Source={_databasePath}"))
                {
                    var student = await context.Students
                        .Include(s => s.College)
                        .Include(s => s.Test)
                        .FirstOrDefaultAsync(s => s.RollNumber == rollNumber);

                    if (student == null)
                    {
                        result.Message = "Student not found with this roll number";
                        return result;
                    }

                    // Update student
                    student.IsVerified = true;
                    student.VerificationDate = DateTime.Now;

                    // Log verification
                    var log = new VerificationLog
                    {
                        StudentId = student.Id,
                        VerificationDateTime = DateTime.Now,
                        IsSuccessful = true,
                        VerificationType = "ManualOverride",
                        MatchConfidence = 0,
                        VerifiedBy = verifiedBy,
                        Remarks = remarks
                    };

                    context.VerificationLogs.Add(log);
                    await context.SaveChangesAsync();

                    result.IsSuccessful = true;
                    result.Student = student;
                    result.Message = "Student verified by manual override";
                }
            }
            catch (Exception ex)
            {
                result.Message = $"Manual override error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Get verification statistics
        /// </summary>
        public async Task<VerificationStats> GetStatisticsAsync()
        {
            var stats = new VerificationStats();

            try
            {
                using (var context = new BiometricContext($"Data Source={_databasePath}"))
                {
                    stats.TotalStudents = await context.Students.CountAsync();
                    stats.VerifiedStudents = await context.Students.CountAsync(s => s.IsVerified);
                    stats.PendingVerification = stats.TotalStudents - stats.VerifiedStudents;

                    stats.TodayVerifications = await context.VerificationLogs
                        .CountAsync(l => l.VerificationDateTime.Date == DateTime.Today && l.IsSuccessful);

                    stats.SuccessfulVerifications = await context.VerificationLogs.CountAsync(l => l.IsSuccessful);
                    stats.FailedVerifications = await context.VerificationLogs.CountAsync(l => !l.IsSuccessful);

                    if (stats.TotalStudents > 0)
                    {
                        stats.VerificationRate = (stats.VerifiedStudents / (double)stats.TotalStudents) * 100;
                    }
                }
            }
            catch (Exception ex)
            {
                stats.ErrorMessage = ex.Message;
            }

            return stats;
        }

        /// <summary>
        /// Get recent verification logs
        /// </summary>
        public async Task<System.Collections.Generic.List<VerificationLog>> GetRecentLogsAsync(int count = 50)
        {
            try
            {
                using (var context = new BiometricContext($"Data Source={_databasePath}"))
                {
                    return await context.VerificationLogs
                        .Include(l => l.Student)
                        .OrderByDescending(l => l.VerificationDateTime)
                        .Take(count)
                        .ToListAsync();
                }
            }
            catch
            {
                return new System.Collections.Generic.List<VerificationLog>();
            }
        }

        /// <summary>
        /// Match fingerprint against database
        /// </summary>
        private FingerprintMatchResult MatchFingerprint(byte[] capturedTemplate, System.Collections.Generic.List<Student> students)
        {
            var result = new FingerprintMatchResult
            {
                Matched = false,
                Confidence = 0
            };

            // TODO: Replace with actual fingerprint matching SDK
            // For now, using simplified comparison

            foreach (var student in students)
            {
                // Simulated fingerprint matching
                // In production, use actual fingerprint SDK matching algorithm
                int confidence = CompareFingerprints(capturedTemplate, student.FingerprintTemplate);

                if (confidence >= 70) // Threshold for match
                {
                    result.Matched = true;
                    result.Student = student;
                    result.Confidence = confidence;
                    return result;
                }
            }

            return result;
        }

        /// <summary>
        /// Compare two fingerprint templates
        /// </summary>
        private int CompareFingerprints(byte[] template1, byte[] template2)
        {
            // PLACEHOLDER: Replace with actual fingerprint SDK comparison
            // This is a simplified simulation

            if (template1 == null || template2 == null)
                return 0;

            if (template1.Length != template2.Length)
                return 0;

            // Simple byte comparison (NOT REAL FINGERPRINT MATCHING)
            int matches = 0;
            for (int i = 0; i < Math.Min(template1.Length, template2.Length); i++)
            {
                if (template1[i] == template2[i])
                    matches++;
            }

            // Calculate percentage match
            return (int)((matches / (double)template1.Length) * 100);
        }

        /// <summary>
        /// Simulate fingerprint capture (for testing without actual scanner)
        /// </summary>
        public byte[] SimulateFingerprintCapture()
        {
            // Generate random fingerprint template for testing
            var random = new Random();
            byte[] template = new byte[512];
            random.NextBytes(template);
            return template;
        }
    }

    #region Helper Classes

    public class VerificationResult
    {
        public bool IsSuccessful { get; set; }
        public string Message { get; set; } = string.Empty;
        public Student? Student { get; set; }
        public int MatchConfidence { get; set; }
        public string VerificationType { get; set; } = string.Empty;
        public DateTime VerificationDateTime { get; set; }
        public string VerifiedBy { get; set; } = string.Empty;
    }

    public class FingerprintMatchResult
    {
        public bool Matched { get; set; }
        public Student? Student { get; set; }
        public int Confidence { get; set; }
    }

    public class VerificationStats
    {
        public int TotalStudents { get; set; }
        public int VerifiedStudents { get; set; }
        public int PendingVerification { get; set; }
        public int TodayVerifications { get; set; }
        public int SuccessfulVerifications { get; set; }
        public int FailedVerifications { get; set; }
        public double VerificationRate { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    #endregion
}