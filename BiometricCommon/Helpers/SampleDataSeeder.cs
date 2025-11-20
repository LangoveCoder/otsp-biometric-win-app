using System;
using System.Threading.Tasks;
using BiometricCommon.Services;
using BiometricCommon.Models;

namespace BiometricCommon.Helpers
{
    /// <summary>
    /// Utility to seed sample data into the database
    /// </summary>
    public static class SampleDataSeeder
    {
        /// <summary>
        /// Add sample colleges, tests, and students to the database
        /// </summary>
        public static async Task SeedSampleDataAsync(DatabaseService dbService)
        {
            try
            {
                // Check if data already exists
                var existingColleges = await dbService.GetAllCollegesAsync();
                if (existingColleges.Count > 0)
                {
                    // Data already exists
                    return;
                }

                // Add Colleges
                var college1 = await dbService.AddCollegeAsync(new College
                {
                    Name = "ABC Engineering College",
                    Code = "ABC001",
                    Address = "123 Main Street, Quetta",
                    ContactPerson = "Dr. Ahmed Khan",
                    ContactPhone = "+92-300-1234567",
                    ContactEmail = "ahmed.khan@abc.edu.pk",
                    IsActive = true
                });

                var college2 = await dbService.AddCollegeAsync(new College
                {
                    Name = "XYZ Technical Institute",
                    Code = "XYZ002",
                    Address = "456 University Road, Quetta",
                    ContactPerson = "Prof. Sara Ali",
                    ContactPhone = "+92-300-7654321",
                    ContactEmail = "sara.ali@xyz.edu.pk",
                    IsActive = true
                });

                var college3 = await dbService.AddCollegeAsync(new College
                {
                    Name = "Balochistan Science College",
                    Code = "BSC003",
                    Address = "789 Science Avenue, Quetta",
                    ContactPerson = "Dr. Hassan Malik",
                    ContactPhone = "+92-300-9876543",
                    ContactEmail = "hassan@bsc.edu.pk",
                    IsActive = true
                });

                var college4 = await dbService.AddCollegeAsync(new College
                {
                    Name = "City Medical College",
                    Code = "CMC004",
                    Address = "321 Medical Plaza, Quetta",
                    ContactPerson = "Dr. Fatima Noor",
                    ContactPhone = "+92-300-5555555",
                    ContactEmail = "fatima@cmc.edu.pk",
                    IsActive = true
                });

                var college5 = await dbService.AddCollegeAsync(new College
                {
                    Name = "Punjab Engineering University",
                    Code = "PEU005",
                    Address = "555 Tech Park, Lahore",
                    ContactPerson = "Prof. Imran Sheikh",
                    ContactPhone = "+92-300-1111111",
                    ContactEmail = "imran@peu.edu.pk",
                    IsActive = false // Inactive college
                });

                // Add Tests
                var test1 = await dbService.AddTestAsync(new Test
                {
                    Name = "Engineering Entrance Exam 2024",
                    Code = "EEE2024",
                    Description = "Annual engineering entrance examination for undergraduate admissions",
                    TestDate = new DateTime(2024, 8, 15),
                    RegistrationStartDate = new DateTime(2024, 6, 1),
                    RegistrationEndDate = new DateTime(2024, 7, 31),
                    IsActive = true
                });

                var test2 = await dbService.AddTestAsync(new Test
                {
                    Name = "Medical Entry Test 2024",
                    Code = "MET2024",
                    Description = "Medical college entry test for MBBS and BDS programs",
                    TestDate = new DateTime(2024, 9, 20),
                    RegistrationStartDate = new DateTime(2024, 7, 1),
                    RegistrationEndDate = new DateTime(2024, 8, 31),
                    IsActive = true
                });

                var test3 = await dbService.AddTestAsync(new Test
                {
                    Name = "IT Certification Test 2024",
                    Code = "ICT2024",
                    Description = "Information technology professional certification exam",
                    TestDate = new DateTime(2024, 10, 10),
                    RegistrationStartDate = new DateTime(2024, 8, 1),
                    RegistrationEndDate = new DateTime(2024, 9, 30),
                    IsActive = true
                });

                // Add Sample Students with Simulated Fingerprints
                Random random = new Random();

                // College 1 - ABC Engineering College - Engineering Exam
                for (int i = 1; i <= 25; i++)
                {
                    var student = await dbService.RegisterStudentAsync(
                        $"ABC{i:D4}",
                        college1.Id,
                        test1.Id,
                        GenerateSimulatedFingerprint(random)
                    );

                    // Mark some as verified
                    if (i % 3 == 0)
                    {
                        await dbService.MarkStudentVerifiedAsync(student.Id);
                        
                        // Add verification log
                        await dbService.AddVerificationLogAsync(new VerificationLog
                        {
                            StudentId = student.Id,
                            VerificationDateTime = DateTime.Now.AddDays(-random.Next(1, 5)),
                            IsSuccessful = true,
                            VerificationType = "Biometric",
                            MatchConfidence = random.Next(75, 95),
                            VerifiedBy = "System",
                            Remarks = "Successfully verified"
                        });
                    }
                }

                // College 2 - XYZ Technical Institute - Engineering Exam
                for (int i = 1; i <= 20; i++)
                {
                    var student = await dbService.RegisterStudentAsync(
                        $"XYZ{i:D4}",
                        college2.Id,
                        test1.Id,
                        GenerateSimulatedFingerprint(random)
                    );

                    if (i % 2 == 0)
                    {
                        await dbService.MarkStudentVerifiedAsync(student.Id);
                        
                        await dbService.AddVerificationLogAsync(new VerificationLog
                        {
                            StudentId = student.Id,
                            VerificationDateTime = DateTime.Now.AddDays(-random.Next(1, 3)),
                            IsSuccessful = true,
                            VerificationType = "Biometric",
                            MatchConfidence = random.Next(80, 98),
                            VerifiedBy = "System",
                            Remarks = "Verified successfully"
                        });
                    }
                }

                // College 3 - Balochistan Science College - Engineering Exam
                for (int i = 1; i <= 15; i++)
                {
                    var student = await dbService.RegisterStudentAsync(
                        $"BSC{i:D4}",
                        college3.Id,
                        test1.Id,
                        GenerateSimulatedFingerprint(random)
                    );

                    if (i <= 10)
                    {
                        await dbService.MarkStudentVerifiedAsync(student.Id);
                        
                        await dbService.AddVerificationLogAsync(new VerificationLog
                        {
                            StudentId = student.Id,
                            VerificationDateTime = DateTime.Now.AddDays(-random.Next(1, 7)),
                            IsSuccessful = true,
                            VerificationType = "Biometric",
                            MatchConfidence = random.Next(70, 90),
                            VerifiedBy = "System",
                            Remarks = "Biometric verification successful"
                        });
                    }
                }

                // College 4 - City Medical College - Medical Test
                for (int i = 1; i <= 30; i++)
                {
                    var student = await dbService.RegisterStudentAsync(
                        $"CMC{i:D4}",
                        college4.Id,
                        test2.Id,
                        GenerateSimulatedFingerprint(random)
                    );

                    if (i % 4 == 0)
                    {
                        await dbService.MarkStudentVerifiedAsync(student.Id);
                        
                        await dbService.AddVerificationLogAsync(new VerificationLog
                        {
                            StudentId = student.Id,
                            VerificationDateTime = DateTime.Now.AddDays(-random.Next(1, 4)),
                            IsSuccessful = true,
                            VerificationType = "Biometric",
                            MatchConfidence = random.Next(85, 99),
                            VerifiedBy = "System",
                            Remarks = "Verified"
                        });
                    }
                }

                // College 1 - ABC Engineering - IT Test
                for (int i = 26; i <= 35; i++)
                {
                    await dbService.RegisterStudentAsync(
                        $"ABC{i:D4}",
                        college1.Id,
                        test3.Id,
                        GenerateSimulatedFingerprint(random)
                    );
                }

                // Add some failed verification attempts
                var students = await dbService.GetAllStudentsAsync();
                for (int i = 0; i < 5; i++)
                {
                    await dbService.AddVerificationLogAsync(new VerificationLog
                    {
                        StudentId = students[random.Next(students.Count)].Id,
                        VerificationDateTime = DateTime.Now.AddHours(-random.Next(1, 12)),
                        IsSuccessful = false,
                        VerificationType = "Biometric",
                        MatchConfidence = random.Next(30, 60),
                        VerifiedBy = "System",
                        Remarks = "Fingerprint mismatch - retry required"
                    });
                }

                // Add some manual override verifications
                for (int i = 0; i < 3; i++)
                {
                    var student = students[random.Next(students.Count)];
                    await dbService.MarkStudentVerifiedAsync(student.Id);
                    
                    await dbService.AddVerificationLogAsync(new VerificationLog
                    {
                        StudentId = student.Id,
                        VerificationDateTime = DateTime.Now.AddHours(-random.Next(1, 24)),
                        IsSuccessful = true,
                        VerificationType = "ManualOverride",
                        MatchConfidence = 0,
                        VerifiedBy = "Admin",
                        Remarks = "Manual override - damaged finger"
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error seeding sample data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generate a simulated fingerprint template (512 bytes of random data)
        /// </summary>
        private static byte[] GenerateSimulatedFingerprint(Random random)
        {
            byte[] template = new byte[512];
            random.NextBytes(template);
            
            // Add a signature pattern to make templates somewhat consistent
            for (int i = 0; i < 16; i++)
            {
                template[i] = (byte)(i * 17);
            }
            
            return template;
        }
    }
}
