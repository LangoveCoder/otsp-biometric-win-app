using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using BiometricCommon.Models;

namespace BiometricCommon.Helpers
{
    /// <summary>
    /// PDF report generator for verification reports
    /// Uses QuestPDF for PDF generation
    /// </summary>
    public static class PdfExporter
    {
        static PdfExporter()
        {
            // Set license for QuestPDF (Community license is free for open source)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>
        /// Generate verification report PDF
        /// </summary>
        public static void GenerateVerificationReport(
            List<Student> students,
            List<VerificationLog> logs,
            College college,
            Test test,
            string outputPath)
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header()
                        .Element(c => ComposeHeader(c, college, test));

                    page.Content()
                        .Element(c => ComposeContent(c, students, logs, college, test));

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                });
            })
            .GeneratePdf(outputPath);
        }

        /// <summary>
        /// Compose header section
        /// </summary>
        private static void ComposeHeader(IContainer container, College college, Test test)
        {
            container.Column(column =>
            {
                column.Item().BorderBottom(1).PaddingBottom(5).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Biometric Verification Report")
                            .FontSize(16)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);

                        col.Item().Text($"{college.Name} ({college.Code})")
                            .FontSize(12)
                            .SemiBold();

                        col.Item().Text($"Test: {test.Name}")
                            .FontSize(10);
                    });

                    row.ConstantItem(150).Column(col =>
                    {
                        col.Item().AlignRight().Text($"Date: {DateTime.Now:dd-MMM-yyyy}");
                        col.Item().AlignRight().Text($"Time: {DateTime.Now:HH:mm:ss}");
                    });
                });

                column.Item().PaddingTop(10);
            });
        }

        /// <summary>
        /// Compose main content
        /// </summary>
        private static void ComposeContent(
            IContainer container,
            List<Student> students,
            List<VerificationLog> logs,
            College college,
            Test test)
        {
            container.Column(column =>
            {
                // Statistics Section
                column.Item().Element(c => ComposeStatistics(c, students, logs));

                column.Item().PaddingTop(15);

                // Student List Section
                column.Item().Element(c => ComposeStudentList(c, students));

                column.Item().PageBreak();

                // Verification Logs Section
                column.Item().Element(c => ComposeVerificationLogs(c, logs));
            });
        }

        /// <summary>
        /// Compose statistics section
        /// </summary>
        private static void ComposeStatistics(IContainer container, List<Student> students, List<VerificationLog> logs)
        {
            var totalStudents = students.Count;
            var verifiedStudents = students.Count(s => s.IsVerified);
            var pendingStudents = totalStudents - verifiedStudents;
            var verificationRate = totalStudents > 0 ? (verifiedStudents / (double)totalStudents) * 100 : 0;
            var successfulAttempts = logs.Count(l => l.IsSuccessful);
            var failedAttempts = logs.Count - successfulAttempts;

            container.Column(column =>
            {
                column.Item().Text("Summary Statistics")
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Blue.Darken2);

                column.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                    });

                    // Table rows
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                        .Text("Total Registered Students").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                        .AlignRight().Text(totalStudents.ToString());

                    table.Cell().Padding(5).Text("Verified Students");
                    table.Cell().Background(Colors.Green.Lighten3).Padding(5)
                        .AlignRight().Text(verifiedStudents.ToString()).Bold();

                    table.Cell().Background(Colors.Grey.Lighten4).Padding(5)
                        .Text("Pending Verification");
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(5)
                        .AlignRight().Text(pendingStudents.ToString()).Bold();

                    table.Cell().Padding(5).Text("Verification Rate");
                    table.Cell().Padding(5).AlignRight()
                        .Text($"{verificationRate:F2}%").Bold();

                    table.Cell().Background(Colors.Grey.Lighten4).Padding(5)
                        .Text("Total Verification Attempts");
                    table.Cell().Background(Colors.Grey.Lighten4).Padding(5)
                        .AlignRight().Text(logs.Count.ToString());

                    table.Cell().Padding(5).Text("Successful Attempts");
                    table.Cell().Padding(5).AlignRight()
                        .Text(successfulAttempts.ToString());

                    table.Cell().Background(Colors.Grey.Lighten4).Padding(5)
                        .Text("Failed Attempts");
                    table.Cell().Background(Colors.Grey.Lighten4).Padding(5)
                        .AlignRight().Text(failedAttempts.ToString());
                });
            });
        }

        /// <summary>
        /// Compose student list section
        /// </summary>
        private static void ComposeStudentList(IContainer container, List<Student> students)
        {
            container.Column(column =>
            {
                column.Item().Text("Registered Students")
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Blue.Darken2);

                column.Item().PaddingTop(5).Table(table =>
                {
                    // Define columns
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40);  // S.No
                        columns.RelativeColumn(2);   // Roll Number
                        columns.RelativeColumn(2);   // Registration Date
                        columns.RelativeColumn(1);   // Status
                        columns.RelativeColumn(2);   // Verification Date
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("S.No").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Roll Number").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Registration Date").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Status").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Verification Date").FontColor(Colors.White).Bold();
                    });

                    // Data rows
                    int sno = 1;
                    foreach (var student in students.OrderBy(s => s.RollNumber))
                    {
                        var bgColor = sno % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                        table.Cell().Background(bgColor).Padding(3)
                            .AlignCenter().Text(sno.ToString());
                        table.Cell().Background(bgColor).Padding(3)
                            .Text(student.RollNumber);
                        table.Cell().Background(bgColor).Padding(3)
                            .Text(student.RegistrationDate.ToString("dd-MMM-yyyy HH:mm"));
                        table.Cell().Background(student.IsVerified ? Colors.Green.Lighten3 : Colors.Yellow.Lighten3)
                            .Padding(3).Text(student.IsVerified ? "Verified ✓" : "Pending");
                        table.Cell().Background(bgColor).Padding(3)
                            .Text(student.VerificationDate?.ToString("dd-MMM-yyyy HH:mm") ?? "-");

                        sno++;
                    }
                });
            });
        }

        /// <summary>
        /// Compose verification logs section
        /// </summary>
        private static void ComposeVerificationLogs(IContainer container, List<VerificationLog> logs)
        {
            container.Column(column =>
            {
                column.Item().Text("Verification Logs")
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Blue.Darken2);

                column.Item().PaddingTop(5).Table(table =>
                {
                    // Define columns
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);   // Date & Time
                        columns.RelativeColumn(1);   // Roll Number
                        columns.RelativeColumn(1);   // Status
                        columns.RelativeColumn(1);   // Type
                        columns.RelativeColumn(1);   // Score
                        columns.RelativeColumn(2);   // Remarks
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Date & Time").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Roll No").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Status").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Type").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Score").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Remarks").FontColor(Colors.White).Bold();
                    });

                    // Data rows
                    int count = 0;
                    foreach (var log in logs.OrderByDescending(l => l.VerificationDateTime))
                    {
                        count++;
                        var bgColor = count % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                        table.Cell().Background(bgColor).Padding(3)
                            .Text(log.VerificationDateTime.ToString("dd-MMM-yy HH:mm"));
                        table.Cell().Background(bgColor).Padding(3)
                            .Text(log.Student?.RollNumber ?? "N/A");
                        table.Cell().Background(log.IsSuccessful ? Colors.Green.Lighten3 : Colors.Red.Lighten3)
                            .Padding(3).Text(log.IsSuccessful ? "Success" : "Failed");
                        table.Cell().Background(log.VerificationType == "ManualOverride" ? Colors.Orange.Lighten3 : bgColor)
                            .Padding(3).Text(log.VerificationType);
                        table.Cell().Background(bgColor).Padding(3)
                            .AlignCenter().Text(log.MatchConfidence > 0 ? $"{log.MatchConfidence}%" : "-");
                        table.Cell().Background(bgColor).Padding(3)
                            .Text(log.Remarks ?? "").FontSize(8);
                    }
                });
            });
        }

        /// <summary>
        /// Generate simple student list PDF
        /// </summary>
        public static void GenerateStudentListPdf(List<Student> students, College college, string outputPath)
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);

                    page.Header().Text($"Student List - {college.Name}")
                        .FontSize(16).Bold().FontColor(Colors.Blue.Darken2);

                    page.Content().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(40);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                .Text("S.No").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                .Text("Roll Number").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                .Text("Registration Date").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                .Text("Status").FontColor(Colors.White).Bold();
                        });

                        int sno = 1;
                        foreach (var student in students.OrderBy(s => s.RollNumber))
                        {
                            var bgColor = sno % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                            table.Cell().Background(bgColor).Padding(3).AlignCenter().Text(sno.ToString());
                            table.Cell().Background(bgColor).Padding(3).Text(student.RollNumber);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(student.RegistrationDate.ToString("dd-MMM-yyyy"));
                            table.Cell().Background(student.IsVerified ? Colors.Green.Lighten3 : Colors.Yellow.Lighten3)
                                .Padding(3).Text(student.IsVerified ? "Verified" : "Pending");

                            sno++;
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                    });
                });
            })
            .GeneratePdf(outputPath);
        }
    }
}
