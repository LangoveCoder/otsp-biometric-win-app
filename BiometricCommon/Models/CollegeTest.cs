using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiometricCommon.Models
{
    /// <summary>
    /// Represents the relationship between a College and a Test
    /// A college can participate in multiple tests
    /// </summary>
    public class CollegeTest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int CollegeId { get; set; }

        [Required]
        public int TestId { get; set; }

        public DateTime EnrollmentDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Number of students registered from this college for this test
        /// </summary>
        public int RegisteredStudentsCount { get; set; } = 0;

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
}
