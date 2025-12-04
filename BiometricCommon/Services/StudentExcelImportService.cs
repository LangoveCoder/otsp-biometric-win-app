using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using BiometricCommon.Models;

namespace BiometricSuperAdmin.Services
{
    public class StudentExcelImportService
    {
        public class ImportResult
        {
            public bool Success { get; set; }
            public int TotalRows { get; set; }
            public int ImportedCount { get; set; }
            public int SkippedCount { get; set; }
            public List<string> Errors { get; set; } = new();
            public List<StudentImportData> Students { get; set; } = new();

            public string GetSummary()
            {
                return $"Total: {TotalRows}\n" +
                       $"Imported: {ImportedCount}\n" +
                       $"Skipped: {SkippedCount}\n" +
                       $"Errors: {Errors.Count}";
            }
        }

        public class StudentImportData
        {
            public string RollNumber { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string CNIC { get; set; } = string.Empty;
            public byte[]? StudentPhoto { get; set; }
        }

        public ImportResult ImportFromExcel(string filePath, int collegeId, int testId)
        {
            var result = new ImportResult();

            try
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1);

                // Find header row
                var headerRow = worksheet.FirstRowUsed();
                if (headerRow == null)
                {
                    result.Errors.Add("Excel file is empty");
                    return result;
                }

                // Find column indices
                int rollCol = -1, nameCol = -1, cnicCol = -1, photoCol = -1;

                for (int col = 1; col <= headerRow.LastCellUsed().Address.ColumnNumber; col++)
                {
                    var header = headerRow.Cell(col).GetString().Trim().ToLower();

                    if (header.Contains("roll")) rollCol = col;
                    else if (header.Contains("name")) nameCol = col;
                    else if (header.Contains("cnic")) cnicCol = col;
                    else if (header.Contains("picture") || header.Contains("photo") || header.Contains("image")) photoCol = col;
                }

                if (rollCol == -1 || nameCol == -1 || cnicCol == -1)
                {
                    result.Errors.Add("Required columns not found. Need: Roll Number, Name, CNIC");
                    return result;
                }

                // Read data rows
                var dataRows = worksheet.RowsUsed().Skip(1); // Skip header
                result.TotalRows = dataRows.Count();

                foreach (var row in dataRows)
                {
                    try
                    {
                        var rollNumber = row.Cell(rollCol).GetString().Trim();
                        var name = row.Cell(nameCol).GetString().Trim();
                        var cnic = row.Cell(cnicCol).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(rollNumber))
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        var studentData = new StudentImportData
                        {
                            RollNumber = rollNumber,
                            Name = name,
                            CNIC = cnic
                        };

                        // Extract embedded image from photo column if exists
                        if (photoCol != -1)
                        {
                            try
                            {
                                var cell = row.Cell(photoCol);
                                var pictures = worksheet.Pictures
                                    .Where(p => p.TopLeftCell.Address.RowNumber == row.RowNumber() &&
                                               p.TopLeftCell.Address.ColumnNumber == photoCol);

                                if (pictures.Any())
                                {
                                    var picture = pictures.First();
                                    using var ms = new MemoryStream();
                                    picture.ImageStream.CopyTo(ms);
                                    studentData.StudentPhoto = ms.ToArray();
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Errors.Add($"Row {row.RowNumber()}: Failed to extract photo - {ex.Message}");
                            }
                        }

                        result.Students.Add(studentData);
                        result.ImportedCount++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Row {row.RowNumber()}: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                result.Success = result.ImportedCount > 0;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to read Excel: {ex.Message}");
                result.Success = false;
            }

            return result;
        }

        public ImportResult SaveToDatabase(BiometricCommon.Database.BiometricContext context,
            List<StudentImportData> students, int collegeId, int testId)
        {
            var result = new ImportResult
            {
                TotalRows = students.Count
            };

            try
            {
                foreach (var studentData in students)
                {
                    try
                    {
                        // Check if student already exists
                        var existing = context.Students
                            .FirstOrDefault(s => s.RollNumber == studentData.RollNumber);

                        if (existing != null)
                        {
                            result.Errors.Add($"{studentData.RollNumber}: Already exists");
                            result.SkippedCount++;
                            continue;
                        }

                        // Create new student
                        var student = new Student
                        {
                            RollNumber = studentData.RollNumber,
                            Name = studentData.Name,
                            CNIC = studentData.CNIC,
                            StudentPhoto = studentData.StudentPhoto,
                            CollegeId = collegeId,
                            TestId = testId,
                            IsVerified = false,
                        };

                        context.Students.Add(student);
                        result.ImportedCount++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"{studentData.RollNumber}: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                context.SaveChanges();
                result.Success = result.ImportedCount > 0;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Database save failed: {ex.Message}");
                result.Success = false;
            }

            return result;
        }
    }
}