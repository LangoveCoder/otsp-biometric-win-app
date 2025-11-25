using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BiometricCommon.Models;
using BiometricCommon.Database;

namespace BiometricCommon.Services
{
    /// <summary>
    /// Service class for database operations
    /// Provides high-level methods for all database interactions
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private readonly BiometricContext _context;
        private bool _disposed = false;

        public DatabaseService()
        {
            _context = new BiometricContext();
            _context.InitializeDatabase();
        }

        public DatabaseService(string databasePath)
        {
            _context = new BiometricContext(databasePath);
            _context.InitializeDatabase();
        }

        public Student? GetStudentByRollNumber(string rollNumber)
        {
            return _context.Students
                .Include(s => s.College)
                .Include(s => s.Test)
                .FirstOrDefault(s => s.RollNumber == rollNumber);
        }

        #region College Operations

        /// <summary>
        /// Get all colleges
        /// </summary>
        public async Task<List<College>> GetAllCollegesAsync()
        {
            return await _context.Colleges
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Get active colleges only
        /// </summary>
        public async Task<List<College>> GetActiveCollegesAsync()
        {
            return await _context.Colleges
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Get college by ID
        /// </summary>
        public async Task<College?> GetCollegeByIdAsync(int id)
        {
            return await _context.Colleges.FindAsync(id);
        }

        /// <summary>
        /// Get college by code
        /// </summary>
        public async Task<College?> GetCollegeByCodeAsync(string code)
        {
            return await _context.Colleges
                .FirstOrDefaultAsync(c => c.Code == code);
        }

        /// <summary>
        /// Add new college
        /// </summary>
        public async Task<College> AddCollegeAsync(College college)
        {
            // Check if code already exists
            var existing = await GetCollegeByCodeAsync(college.Code);
            if (existing != null)
            {
                throw new InvalidOperationException($"College with code '{college.Code}' already exists.");
            }

            college.CreatedDate = DateTime.Now;
            _context.Colleges.Add(college);
            await _context.SaveChangesAsync();
            return college;
        }

        /// <summary>
        /// Update college
        /// </summary>
        public async Task<College> UpdateCollegeAsync(College college)
        {
            college.LastModifiedDate = DateTime.Now;
            _context.Colleges.Update(college);
            await _context.SaveChangesAsync();
            return college;
        }

        /// <summary>
        /// Delete college (soft delete - set IsActive to false)
        /// </summary>
        public async Task<bool> DeleteCollegeAsync(int id)
        {
            var college = await GetCollegeByIdAsync(id);
            if (college == null)
                return false;

            college.IsActive = false;
            college.LastModifiedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        #endregion

        #region Test Operations

        /// <summary>
        /// Get all tests
        /// </summary>
        public async Task<List<Test>> GetAllTestsAsync()
        {
            return await _context.Tests
                .OrderByDescending(t => t.TestDate)
                .ToListAsync();
        }

        /// <summary>
        /// Get active tests only
        /// </summary>
        public async Task<List<Test>> GetActiveTestsAsync()
        {
            return await _context.Tests
                .Where(t => t.IsActive)
                .OrderByDescending(t => t.TestDate)
                .ToListAsync();
        }

        /// <summary>
        /// Get test by ID
        /// </summary>
        public async Task<Test?> GetTestByIdAsync(int id)
        {
            return await _context.Tests.FindAsync(id);
        }

        /// <summary>
        /// Get test by code
        /// </summary>
        public async Task<Test?> GetTestByCodeAsync(string code)
        {
            return await _context.Tests
                .FirstOrDefaultAsync(t => t.Code == code);
        }

        /// <summary>
        /// Add new test
        /// </summary>
        public async Task<Test> AddTestAsync(Test test)
        {
            // Check if code already exists
            var existing = await GetTestByCodeAsync(test.Code);
            if (existing != null)
            {
                throw new InvalidOperationException($"Test with code '{test.Code}' already exists.");
            }

            test.CreatedDate = DateTime.Now;
            _context.Tests.Add(test);
            await _context.SaveChangesAsync();
            return test;
        }

        /// <summary>
        /// Update test
        /// </summary>
        public async Task<Test> UpdateTestAsync(Test test)
        {
            test.LastModifiedDate = DateTime.Now;
            _context.Tests.Update(test);
            await _context.SaveChangesAsync();
            return test;
        }

        /// <summary>
        /// Delete test (soft delete)
        /// </summary>
        public async Task<bool> DeleteTestAsync(int id)
        {
            var test = await GetTestByIdAsync(id);
            if (test == null)
                return false;

            test.IsActive = false;
            test.LastModifiedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        #endregion

        #region Student Operations

        /// <summary>
        /// Get all students
        /// </summary>
        public async Task<List<Student>> GetAllStudentsAsync()
        {
            return await _context.Students
                .Include(s => s.College)
                .Include(s => s.Test)
                .OrderBy(s => s.RollNumber)
                .ToListAsync();
        }

        /// <summary>
        /// Get students by college and test
        /// </summary>
        public async Task<List<Student>> GetStudentsByCollegeAndTestAsync(int collegeId, int testId)
        {
            return await _context.Students
                .Include(s => s.College)
                .Include(s => s.Test)
                .Where(s => s.CollegeId == collegeId && s.TestId == testId)
                .OrderBy(s => s.RollNumber)
                .ToListAsync();
        }

        /// <summary>
        /// Get student by roll number, college and test
        /// </summary>
        public async Task<Student?> GetStudentAsync(string rollNumber, int collegeId, int testId)
        {
            return await _context.Students
                .Include(s => s.College)
                .Include(s => s.Test)
                .FirstOrDefaultAsync(s => s.RollNumber == rollNumber 
                    && s.CollegeId == collegeId 
                    && s.TestId == testId);
        }

        /// <summary>
        /// Add or update student (for registration)
        /// </summary>
        public async Task<Student> RegisterStudentAsync(string rollNumber, int collegeId, int testId, byte[] fingerprintTemplate)
        {
            // Check if student already exists
            var existing = await GetStudentAsync(rollNumber, collegeId, testId);

            if (existing != null)
            {
                // Update existing student
                existing.FingerprintTemplate = fingerprintTemplate;
                existing.LastModifiedDate = DateTime.Now;
                existing.IsVerified = false; // Reset verification status
                await _context.SaveChangesAsync();
                return existing;
            }
            else
            {
                // Add new student
                var student = new Student
                {
                    RollNumber = rollNumber,
                    CollegeId = collegeId,
                    TestId = testId,
                    FingerprintTemplate = fingerprintTemplate,
                    RegistrationDate = DateTime.Now
                };

                _context.Students.Add(student);
                await _context.SaveChangesAsync();
                return student;
            }
        }

        /// <summary>
        /// Mark student as verified
        /// </summary>
        public async Task<bool> MarkStudentVerifiedAsync(int studentId)
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
                return false;

            student.IsVerified = true;
            student.VerificationDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Get verification count for college and test
        /// </summary>
        public async Task<(int Total, int Verified, int Pending)> GetVerificationCountAsync(int collegeId, int testId)
        {
            var students = await GetStudentsByCollegeAndTestAsync(collegeId, testId);
            var total = students.Count;
            var verified = students.Count(s => s.IsVerified);
            var pending = total - verified;

            return (total, verified, pending);
        }

        #endregion

        #region CollegeAdmin Operations

        /// <summary>
        /// Get college admin by username
        /// </summary>
        public async Task<CollegeAdmin?> GetCollegeAdminAsync(string username)
        {
            return await _context.CollegeAdmins
                .Include(ca => ca.College)
                .FirstOrDefaultAsync(ca => ca.Username == username);
        }

        /// <summary>
        /// Get admins by college
        /// </summary>
        public async Task<List<CollegeAdmin>> GetCollegeAdminsByCollegeAsync(int collegeId)
        {
            return await _context.CollegeAdmins
                .Where(ca => ca.CollegeId == collegeId)
                .ToListAsync();
        }

        /// <summary>
        /// Add college admin
        /// </summary>
        public async Task<CollegeAdmin> AddCollegeAdminAsync(CollegeAdmin admin)
        {
            // Check if username already exists
            var existing = await GetCollegeAdminAsync(admin.Username);
            if (existing != null)
            {
                throw new InvalidOperationException($"Admin with username '{admin.Username}' already exists.");
            }

            admin.CreatedDate = DateTime.Now;
            _context.CollegeAdmins.Add(admin);
            await _context.SaveChangesAsync();
            return admin;
        }

        /// <summary>
        /// Update last login date
        /// </summary>
        public async Task UpdateAdminLastLoginAsync(int adminId)
        {
            var admin = await _context.CollegeAdmins.FindAsync(adminId);
            if (admin != null)
            {
                admin.LastLoginDate = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        #endregion

        #region VerificationLog Operations

        /// <summary>
        /// Add verification log entry
        /// </summary>
        public async Task<VerificationLog> AddVerificationLogAsync(VerificationLog log)
        {
            log.VerificationDateTime = DateTime.Now;
            _context.VerificationLogs.Add(log);
            await _context.SaveChangesAsync();
            return log;
        }

        /// <summary>
        /// Get verification logs for a student
        /// </summary>
        public async Task<List<VerificationLog>> GetStudentVerificationLogsAsync(int studentId)
        {
            return await _context.VerificationLogs
                .Include(vl => vl.Student)
                .Where(vl => vl.StudentId == studentId)
                .OrderByDescending(vl => vl.VerificationDateTime)
                .ToListAsync();
        }

        /// <summary>
        /// Get all verification logs for a college and test
        /// </summary>
        public async Task<List<VerificationLog>> GetVerificationLogsByCollegeTestAsync(int collegeId, int testId)
        {
            return await _context.VerificationLogs
                .Include(vl => vl.Student)
                .ThenInclude(s => s.College)
                .Include(vl => vl.Student)
                .ThenInclude(s => s.Test)
                .Where(vl => vl.Student!.CollegeId == collegeId && vl.Student.TestId == testId)
                .OrderByDescending(vl => vl.VerificationDateTime)
                .ToListAsync();
        }

        /// <summary>
        /// Get today's verification logs
        /// </summary>
        public async Task<List<VerificationLog>> GetTodayVerificationLogsAsync()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            return await _context.VerificationLogs
                .Include(vl => vl.Student)
                .ThenInclude(s => s.College)
                .Include(vl => vl.Student)
                .ThenInclude(s => s.Test)
                .Where(vl => vl.VerificationDateTime >= today && vl.VerificationDateTime < tomorrow)
                .OrderByDescending(vl => vl.VerificationDateTime)
                .ToListAsync();
        }

        #endregion

        #region Dashboard Statistics

        /// <summary>
        /// Get dashboard statistics
        /// </summary>
        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            var totalColleges = await _context.Colleges.CountAsync();
            var activeColleges = await _context.Colleges.CountAsync(c => c.IsActive);
            var totalTests = await _context.Tests.CountAsync();
            var activeTests = await _context.Tests.CountAsync(t => t.IsActive);
            var totalStudents = await _context.Students.CountAsync();
            var verifiedStudents = await _context.Students.CountAsync(s => s.IsVerified);
            var pendingVerification = totalStudents - verifiedStudents;

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var todayVerifications = await _context.VerificationLogs
                .CountAsync(vl => vl.VerificationDateTime >= today && vl.VerificationDateTime < tomorrow);

            var verificationRate = totalStudents > 0 
                ? (verifiedStudents / (double)totalStudents) * 100 
                : 0;

            return new DashboardStats
            {
                TotalColleges = totalColleges,
                ActiveColleges = activeColleges,
                TotalTests = totalTests,
                ActiveTests = activeTests,
                TotalStudents = totalStudents,
                VerifiedStudents = verifiedStudents,
                PendingVerification = pendingVerification,
                TodayVerifications = todayVerifications,
                VerificationRate = Math.Round(verificationRate, 2),
                LastUpdated = DateTime.Now
            };
        }

        #endregion

        #region SystemSettings Operations

        /// <summary>
        /// Get setting value
        /// </summary>
        public async Task<string?> GetSettingAsync(string key)
        {
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == key);
            return setting?.Value;
        }

        /// <summary>
        /// Update setting value
        /// </summary>
        public async Task UpdateSettingAsync(string key, string value)
        {
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            if (setting != null)
            {
                setting.Value = value;
                setting.ModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Get all settings
        /// </summary>
        public async Task<List<SystemSettings>> GetAllSettingsAsync()
        {
            return await _context.SystemSettings.ToListAsync();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Backup database
        /// </summary>
        public void BackupDatabase(string backupPath)
        {
            _context.BackupDatabase(backupPath);
        }

        /// <summary>
        /// Optimize database
        /// </summary>
        public void OptimizeDatabase()
        {
            _context.OptimizeDatabase();
        }

        /// <summary>
        /// Get database path
        /// </summary>
        public string GetDatabasePath()
        {
            return _context.GetDatabasePath();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context?.Dispose();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
