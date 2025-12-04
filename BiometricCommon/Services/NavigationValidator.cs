using System.Linq;
using BiometricCommon.Database;
using BiometricCommon.Services;

namespace BiometricSuperAdmin.Services
{
    /// <summary>
    /// Validates navigation prerequisites - ensures features are accessed in correct order
    /// </summary>
    public class NavigationValidator
    {
        private readonly BiometricContext _context;

        public NavigationValidator(BiometricContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Check if at least one college exists
        /// </summary>
        public bool HasColleges()
        {
            return _context.Colleges.Any(c => c.IsActive);
        }

        /// <summary>
        /// Check if at least one test exists
        /// </summary>
        public bool HasTests()
        {
            return _context.Tests.Any(t => t.IsActive);
        }

        /// <summary>
        /// Check if registration context is set
        /// </summary>
        public bool HasContext()
        {
            var context = RegistrationContext.GetCurrentContext();
            return context != null;
        }

        /// <summary>
        /// Check if students are imported for current context
        /// </summary>
        public bool HasStudents()
        {
            var context = RegistrationContext.GetCurrentContext();
            if (context == null) return false;

            return _context.Students.Any(s =>
                s.CollegeId == context.CollegeId &&
                s.TestId == context.TestId);
        }

        /// <summary>
        /// Check if any students have fingerprints registered
        /// </summary>
        public bool HasRegisteredStudents()
        {
            var context = RegistrationContext.GetCurrentContext();
            if (context == null) return false;

            return _context.Students.Any(s =>
                s.CollegeId == context.CollegeId &&
                s.TestId == context.TestId &&
                s.FingerprintTemplate != null &&
                s.FingerprintTemplate.Length > 0);
        }

        /// <summary>
        /// Get count of students for current context
        /// </summary>
        public int GetStudentCount()
        {
            var context = RegistrationContext.GetCurrentContext();
            if (context == null) return 0;

            return _context.Students.Count(s =>
                s.CollegeId == context.CollegeId &&
                s.TestId == context.TestId);
        }

        /// <summary>
        /// Get count of registered students for current context
        /// </summary>
        public int GetRegisteredCount()
        {
            var context = RegistrationContext.GetCurrentContext();
            if (context == null) return 0;

            return _context.Students.Count(s =>
                s.CollegeId == context.CollegeId &&
                s.TestId == context.TestId &&
                s.FingerprintTemplate != null &&
                s.FingerprintTemplate.Length > 0);
        }

        /// <summary>
        /// Can user create a test? (needs college first)
        /// </summary>
        public ValidationResult CanCreateTest()
        {
            if (!HasColleges())
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = "Create a college first before creating tests."
                };
            }

            return new ValidationResult { IsValid = true };
        }

        /// <summary>
        /// Can user set context? (needs college and test)
        /// </summary>
        public ValidationResult CanSetContext()
        {
            if (!HasColleges())
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = "Create a college first."
                };
            }

            if (!HasTests())
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = "Create a test first."
                };
            }

            return new ValidationResult { IsValid = true };
        }

        /// <summary>
        /// Can user import students? (needs context set)
        /// </summary>
        public ValidationResult CanImportStudents()
        {
            if (!HasContext())
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = "Set registration context first (College + Test + Device)."
                };
            }

            return new ValidationResult { IsValid = true };
        }

        /// <summary>
        /// Can user view student list? (needs students imported)
        /// </summary>
        public ValidationResult CanViewStudentList()
        {
            var contextCheck = CanImportStudents();
            if (!contextCheck.IsValid) return contextCheck;

            if (!HasStudents())
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = "Import students first using 'Import Students' button."
                };
            }

            return new ValidationResult { IsValid = true };
        }

        /// <summary>
        /// Can user register students? (needs students imported)
        /// </summary>
        public ValidationResult CanRegisterStudents()
        {
            return CanViewStudentList(); // Same prerequisites
        }

        /// <summary>
        /// Can user generate package? (needs registered students)
        /// </summary>
        public ValidationResult CanGeneratePackage()
        {
            var studentCheck = CanViewStudentList();
            if (!studentCheck.IsValid) return studentCheck;

            if (!HasRegisteredStudents())
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = "Register student fingerprints first before generating package."
                };
            }

            return new ValidationResult { IsValid = true };
        }
    }

    /// <summary>
    /// Result of validation check
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}