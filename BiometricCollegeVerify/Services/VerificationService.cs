using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BiometricCommon.Models;
using BiometricCommon.Database;
using BiometricCommon.Fingerprint;

namespace BiometricCollegeVerify.Services
{
    public class VerificationService
    {
        private readonly string _databasePath;
        private IFingerprintScanner? _scanner;

        public VerificationService(string databasePath)
        {
            _databasePath = databasePath;
        }

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
                using (var context = new BiometricContext(_databasePath))
                {
                    var students = await context.Students
                        .Include(s => s.College)
                        .Include(s => s.Test)
                        .ToListAsync();

                    if (students.Count == 0)
                    {
                        result.Message = "No students in database";
                        return result;
                    }

                    if (_scanner == null)
                    {
                        _scanner = ScannerFactory.GetAvailableScanner();
                        if (_scanner != null)
                        {
                            await _scanner.InitializeAsync();
                        }
                    }

                    Student? matchedStudent = null;
                    int bestScore = 0;

                    if (_scanner != null)
                    {
                        foreach (var student in students)
                        {
                            if (student.FingerprintTemplate == null) continue;

                            var matchResult = await _scanner.MatchAsync(fingerprintTemplate, student.FingerprintTemplate);

                            if (matchResult.Success && matchResult.IsMatch)
                            {
                                matchedStudent = student;
                                bestScore = matchResult.MatchScore;
                                break;
                            }
                        }
                    }

                    if (matchedStudent != null)
                    {
                        result.IsSuccessful = true;
                        result.Student = matchedStudent;
                        result.MatchConfidence = bestScore;
                        result.VerificationType = "Biometric";
                        result.Message = "Student verified successfully";

                        matchedStudent.IsVerified = true;
                        matchedStudent.VerificationDate = DateTime.Now;

                        var log = new VerificationLog
                        {
                            StudentId = matchedStudent.Id,
                            VerificationDateTime = DateTime.Now,
                            IsSuccessful = true,
                            VerificationType = "Biometric",
                            MatchConfidence = bestScore,
                            VerifiedBy = verifiedBy,
                            Remarks = "Fingerprint matched successfully"
                        };

                        context.VerificationLogs.Add(log);
                        await context.SaveChangesAsync();
                    }
                    else
                    {
                        result.Message = "Fingerprint not matched";

                        var log = new VerificationLog
                        {
                            StudentId = 0,
                            VerificationDateTime = DateTime.Now,
                            IsSuccessful = false,
                            VerificationType = "Biometric",
                            MatchConfidence = 0,
                            VerifiedBy = verifiedBy,
                            Remarks = "Fingerprint not matched"
                        };

                        context.VerificationLogs.Add(log);
                        await context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = $"Verification error: {ex.Message}";
            }

            return result;
        }

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
                using (var context = new BiometricContext(_databasePath))
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

                    student.IsVerified = true;
                    student.VerificationDate = DateTime.Now;

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

        public async Task<VerificationStats> GetStatisticsAsync()
        {
            var stats = new VerificationStats();

            try
            {
                using (var context = new BiometricContext(_databasePath))
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

        public async Task<System.Collections.Generic.List<VerificationLog>> GetRecentLogsAsync(int count = 50)
        {
            try
            {
                using (var context = new BiometricContext(_databasePath))
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

        public async Task<byte[]?> CaptureFingerprintAsync()
        {
            if (_scanner == null)
            {
                _scanner = ScannerFactory.GetAvailableScanner();
                if (_scanner != null)
                {
                    await _scanner.InitializeAsync();
                }
            }

            if (_scanner == null) return null;

            var result = await _scanner.CaptureAsync();
            return result.Success ? result.Template : null;
        }

        public void Dispose()
        {
            _scanner?.Dispose();
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