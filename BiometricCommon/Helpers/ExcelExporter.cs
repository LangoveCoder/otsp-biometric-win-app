using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using BiometricCommon.Models;

namespace BiometricCommon.Helpers
{
    /// <summary>
    /// Excel report generator for verification reports
    /// Uses ClosedXML for Excel file generation
    /// </summary>
    public static class ExcelExporter
    {
        /// <summary>
        /// Generate verification report for a college
        /// </summary>
        public static void GenerateVerificationReport(
            List<Student> students,
            List<VerificationLog> logs,
            College college,
            Test test,
            string outputPath)
        {
            using var workbook = new XLWorkbook();

            // Add Summary Sheet
            AddSummarySheet(workbook, students, logs, college, test);

            // Add Student List Sheet
            AddStudentListSheet(workbook, students);

            // Add Verification Logs Sheet
            AddVerificationLogsSheet(workbook, logs);

            // Save the workbook
            workbook.SaveAs(outputPath);
        }

        /// <summary>
        /// Add summary sheet with statistics
        /// </summary>
        private static void AddSummarySheet(
            IXLWorkbook workbook,
            List<Student> students,
            List<VerificationLog> logs,
            College college,
            Test test)
        {
            var worksheet = workbook.Worksheets.Add("Summary");

            // Title
            worksheet.Cell(1, 1).Value = "Biometric Verification Report";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 16;
            worksheet.Range(1, 1, 1, 2).Merge();

            // Report Details
            var row = 3;
            worksheet.Cell(row, 1).Value = "College:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = college.Name;
            row++;

            worksheet.Cell(row, 1).Value = "College Code:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = college.Code;
            row++;

            worksheet.Cell(row, 1).Value = "Test:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = test.Name;
            row++;

            worksheet.Cell(row, 1).Value = "Test Date:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = test.TestDate.ToString("dd-MMM-yyyy");
            row++;

            worksheet.Cell(row, 1).Value = "Report Generated:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");
            row += 2;

            // Statistics
            var totalStudents = students.Count;
            var verifiedStudents = students.Count(s => s.IsVerified);
            var pendingStudents = totalStudents - verifiedStudents;
            var verificationRate = totalStudents > 0 ? (verifiedStudents / (double)totalStudents) * 100 : 0;

            worksheet.Cell(row, 1).Value = "STATISTICS";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 14;
            worksheet.Range(row, 1, row, 2).Merge();
            row++;

            worksheet.Cell(row, 1).Value = "Total Registered Students:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = totalStudents;
            row++;

            worksheet.Cell(row, 1).Value = "Verified Students:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = verifiedStudents;
            worksheet.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.LightGreen;
            row++;

            worksheet.Cell(row, 1).Value = "Pending Verification:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = pendingStudents;
            worksheet.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.LightYellow;
            row++;

            worksheet.Cell(row, 1).Value = "Verification Rate:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = $"{verificationRate:F2}%";
            row++;

            worksheet.Cell(row, 1).Value = "Total Verification Attempts:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = logs.Count;
            row++;

            var successfulAttempts = logs.Count(l => l.IsSuccessful);
            var failedAttempts = logs.Count - successfulAttempts;

            worksheet.Cell(row, 1).Value = "Successful Attempts:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = successfulAttempts;
            row++;

            worksheet.Cell(row, 1).Value = "Failed Attempts:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = failedAttempts;

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();
        }

        /// <summary>
        /// Add student list sheet
        /// </summary>
        private static void AddStudentListSheet(IXLWorkbook workbook, List<Student> students)
        {
            var worksheet = workbook.Worksheets.Add("Student List");

            // Headers
            worksheet.Cell(1, 1).Value = "Roll Number";
            worksheet.Cell(1, 2).Value = "Registration Date";
            worksheet.Cell(1, 3).Value = "Verification Status";
            worksheet.Cell(1, 4).Value = "Verification Date";

            // Style headers
            var headerRange = worksheet.Range(1, 1, 1, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Data rows
            var row = 2;
            foreach (var student in students.OrderBy(s => s.RollNumber))
            {
                worksheet.Cell(row, 1).Value = student.RollNumber;
                worksheet.Cell(row, 2).Value = student.RegistrationDate.ToString("dd-MMM-yyyy HH:mm");
                worksheet.Cell(row, 3).Value = student.IsVerified ? "Verified ✓" : "Pending";
                worksheet.Cell(row, 4).Value = student.VerificationDate?.ToString("dd-MMM-yyyy HH:mm") ?? "-";

                // Color code verification status
                if (student.IsVerified)
                {
                    worksheet.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.LightGreen;
                }
                else
                {
                    worksheet.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.LightYellow;
                }

                row++;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            // Add filters
            worksheet.RangeUsed().SetAutoFilter();
        }

        /// <summary>
        /// Add verification logs sheet
        /// </summary>
        private static void AddVerificationLogsSheet(IXLWorkbook workbook, List<VerificationLog> logs)
        {
            var worksheet = workbook.Worksheets.Add("Verification Logs");

            // Headers
            worksheet.Cell(1, 1).Value = "Date & Time";
            worksheet.Cell(1, 2).Value = "Roll Number";
            worksheet.Cell(1, 3).Value = "Status";
            worksheet.Cell(1, 4).Value = "Type";
            worksheet.Cell(1, 5).Value = "Match Score";
            worksheet.Cell(1, 6).Value = "Verified By";
            worksheet.Cell(1, 7).Value = "Remarks";

            // Style headers
            var headerRange = worksheet.Range(1, 1, 1, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Data rows
            var row = 2;
            foreach (var log in logs.OrderByDescending(l => l.VerificationDateTime))
            {
                worksheet.Cell(row, 1).Value = log.VerificationDateTime.ToString("dd-MMM-yyyy HH:mm:ss");
                worksheet.Cell(row, 2).Value = log.Student?.RollNumber ?? "Unknown";
                worksheet.Cell(row, 3).Value = log.IsSuccessful ? "Success ✓" : "Failed ✗";
                worksheet.Cell(row, 4).Value = log.VerificationType;
                worksheet.Cell(row, 5).Value = log.MatchConfidence > 0 ? $"{log.MatchConfidence}%" : "-";
                worksheet.Cell(row, 6).Value = log.VerifiedBy;
                worksheet.Cell(row, 7).Value = log.Remarks;

                // Color code status
                if (log.IsSuccessful)
                {
                    worksheet.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.LightGreen;
                }
                else
                {
                    worksheet.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.LightPink;
                }

                // Highlight manual overrides
                if (log.VerificationType == "ManualOverride")
                {
                    worksheet.Cell(row, 4).Style.Fill.BackgroundColor = XLColor.Orange;
                }

                row++;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            // Add filters
            worksheet.RangeUsed().SetAutoFilter();
        }

        /// <summary>
        /// Generate simple student list (for quick export)
        /// </summary>
        public static void GenerateStudentList(List<Student> students, string outputPath)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Students");

            // Headers
            worksheet.Cell(1, 1).Value = "Roll Number";
            worksheet.Cell(1, 2).Value = "College";
            worksheet.Cell(1, 3).Value = "Test";
            worksheet.Cell(1, 4).Value = "Registration Date";
            worksheet.Cell(1, 5).Value = "Status";

            // Style headers
            var headerRange = worksheet.Range(1, 1, 1, 5);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

            // Data
            var row = 2;
            foreach (var student in students)
            {
                worksheet.Cell(row, 1).Value = student.RollNumber;
                worksheet.Cell(row, 2).Value = student.College?.Name ?? "";
                worksheet.Cell(row, 3).Value = student.Test?.Name ?? "";
                worksheet.Cell(row, 4).Value = student.RegistrationDate.ToString("dd-MMM-yyyy");
                worksheet.Cell(row, 5).Value = student.IsVerified ? "Verified" : "Pending";
                row++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(outputPath);
        }
    }
}
