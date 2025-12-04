using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using BiometricCollegeVerify.Services;
using BiometricCommon.Services;

namespace BiometricCollegeVerify.Views
{
    public partial class VerificationView : Page
    {
        private readonly VerificationService _verificationService;
        private readonly PackageImportService _importService;
        private FingerprintService? _fingerprintService;
        private byte[]? _capturedTemplate;
        private byte[]? _capturedImageData;
        private int _imageWidth;
        private int _imageHeight;

        public VerificationView()
        {
            InitializeComponent();
            _importService = new PackageImportService();
            _verificationService = new VerificationService(_importService.GetDatabasePath());

            Loaded += VerificationView_Loaded;
        }

        private async void VerificationView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadStatisticsAsync();
            await InitializeScannerAsync();
        }

        private async Task InitializeScannerAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== SCANNER INITIALIZATION DIAGNOSTICS ===");

                // Check DLL files
                string appPath = AppDomain.CurrentDomain.BaseDirectory;
                System.Diagnostics.Debug.WriteLine($"App Directory: {appPath}");

                string[] requiredDlls = {
                    "sgfplib.dll",
                    "SecuGen.FDxSDKPro.Windows.dll",
                    "sgfpamx.dll",
                    "sgwsqlib.dll",
                    "sgfdusdax64.dll",
                    "sgbledev.dll"
                };

                foreach (var dll in requiredDlls)
                {
                    string dllPath = System.IO.Path.Combine(appPath, dll);
                    bool exists = System.IO.File.Exists(dllPath);
                    System.Diagnostics.Debug.WriteLine($"  {dll}: {(exists ? "✓ FOUND" : "✗ MISSING")}");
                }

                // Initialize FingerprintService
                _fingerprintService = new FingerprintService();
                _fingerprintService.RegisterScanner(new BiometricCommon.Scanners.SecuGenScanner());
                _fingerprintService.RegisterScanner(new BiometricCommon.Scanners.MockFingerprintScanner());

                // Show scanner selection dialog FIRST
                var result = System.Windows.MessageBox.Show(
                    "Choose your fingerprint scanner:\n\n" +
                    "YES = SecuGen Hamster Pro 20\n" +
                    "NO = Mock Scanner (for testing)\n\n" +
                    "Note: SecuGen requires sgfplib.dll installed",
                    "Scanner Selection",
                    System.Windows.MessageBoxButton.YesNoCancel,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Cancel)
                {
                    System.Diagnostics.Debug.WriteLine("User cancelled scanner selection");
                    UpdateScannerStatus("Cancelled", false);
                    VerifyButton.IsEnabled = false;
                    return;
                }

                string selectedScanner = result == System.Windows.MessageBoxResult.Yes ? "SecuGen Hamster Pro 20" : "Mock Scanner (Testing)";
                System.Diagnostics.Debug.WriteLine($"Selected scanner: {selectedScanner}");

                UpdateScannerStatus("Initializing...", null);

                // Initialize the scanner
                var initResult = await _fingerprintService.InitializeScannerAsync(selectedScanner);

                System.Diagnostics.Debug.WriteLine($"Init result: Success={initResult.Success}, Message={initResult.Message}");

                if (initResult.Success)
                {
                    UpdateScannerStatus($"Connected: {selectedScanner}", true);
                    VerifyButton.IsEnabled = true;

                    System.Windows.MessageBox.Show(
                        $"✅ Scanner Ready!\n\n{initResult.Message}",
                        "Scanner Connected",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    UpdateScannerStatus($"Failed: {initResult.Message}", false);
                    VerifyButton.IsEnabled = false;

                    System.Windows.MessageBox.Show(
                        $"❌ Scanner initialization failed!\n\n{initResult.Message}\n\n" +
                        $"{initResult.ErrorDetails ?? "No additional details"}",
                        "Scanner Failed",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EXCEPTION in InitializeScannerAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                UpdateScannerStatus("Error", false);
                VerifyButton.IsEnabled = false;

                System.Windows.MessageBox.Show(
                    $"Critical error during scanner initialization:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void UpdateScannerStatus(string message, bool? isConnected)
        {
            ScannerStatusText.Text = message;

            if (isConnected == true)
            {
                ScannerStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            }
            else if (isConnected == false)
            {
                ScannerStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
            }
            else
            {
                ScannerStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
            }
        }

        private async Task LoadStatisticsAsync()
        {
            try
            {
                var stats = await _verificationService.GetStatisticsAsync();

                TotalStudentsText.Text = stats.TotalStudents.ToString();
                VerifiedStudentsText.Text = stats.VerifiedStudents.ToString();
                PendingStudentsText.Text = stats.PendingVerification.ToString();
                TodayVerificationsText.Text = stats.TodayVerifications.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading statistics: {ex.Message}");
            }
        }

        private async void LoadStudentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string rollNumber = RollNumberTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(rollNumber))
                {
                    System.Windows.MessageBox.Show(
                        "Please enter roll number",
                        "Validation",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Clear previous results
                ResultPanel.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;
                ClearFingerprintDisplay();

                System.Diagnostics.Debug.WriteLine($"Ready to verify roll number: {rollNumber}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string rollNumber = RollNumberTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(rollNumber))
                {
                    System.Windows.MessageBox.Show(
                        "Please enter roll number",
                        "Validation",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (_fingerprintService == null || !_fingerprintService.IsReady())
                {
                    System.Windows.MessageBox.Show(
                        "Scanner not ready. Please restart the application.",
                        "Scanner Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                // ✅ STEP 1: Check if student exists in imported college package FIRST
                System.Diagnostics.Debug.WriteLine($"Looking for student with roll number: {rollNumber}");

                using (var context = new BiometricCommon.Database.BiometricContext(_importService.GetDatabasePath()))
                {
                    // Use FirstOrDefaultAsync instead of Task.Run
                    var student = await context.Students
                        .Include(s => s.College)
                        .Include(s => s.Test)
                        .FirstOrDefaultAsync(s => s.RollNumber == rollNumber);

                    if (student == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Student with roll number '{rollNumber}' NOT FOUND in college package");

                        System.Windows.MessageBox.Show(
                            $"❌ Student Not Found\n\n" +
                            $"Roll Number: {rollNumber}\n\n" +
                            $"This roll number does not exist in the imported college package.\n\n" +
                            $"Please verify:\n" +
                            $"• Roll number is correct\n" +
                            $"• Student was registered in this college\n" +
                            $"• Correct package was imported",
                            "Student Not Found",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"✓ Found student: {student.RollNumber} (College: {student.College?.Name})");

                    // Check if already verified
                    if (student.IsVerified)
                    {
                        var reVerify = System.Windows.MessageBox.Show(
                            $"⚠️ Already Verified\n\n" +
                            $"Roll Number: {student.RollNumber}\n" +
                            $"College: {student.College?.Name}\n" +
                            $"Verified on: {student.VerificationDate:yyyy-MM-dd HH:mm:ss}\n\n" +
                            $"Do you want to verify again?",
                            "Already Verified",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Question);

                        if (reVerify != System.Windows.MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }

                    // ✅ STEP 2: Show loading and capture fingerprint
                    LoadingOverlay.Visibility = Visibility.Visible;
                    LoadingText.Text = "Place finger on scanner...";

                    System.Diagnostics.Debug.WriteLine("Starting fingerprint capture...");

                    var captureResult = await _fingerprintService.CaptureAsync();

                    System.Diagnostics.Debug.WriteLine($"Capture result: Success={captureResult.Success}, Quality={captureResult.QualityScore}");

                    if (!captureResult.Success)
                    {
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                        System.Windows.MessageBox.Show(
                            $"Fingerprint capture failed!\n\n{captureResult.Message}",
                            "Capture Failed",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    // Store captured data
                    _capturedTemplate = captureResult.Template;
                    _capturedImageData = captureResult.ImageData;
                    _imageWidth = captureResult.ImageWidth;
                    _imageHeight = captureResult.ImageHeight;

                    // Display fingerprint image
                    if (_capturedImageData != null && _imageWidth > 0 && _imageHeight > 0)
                    {
                        DisplayFingerprintImage(_capturedImageData, _imageWidth, _imageHeight);
                    }

                    // Display capture info
                    DisplayCaptureInfo(captureResult.QualityScore, captureResult.Template?.Length ?? 0, _imageWidth, _imageHeight);

                    LoadingText.Text = "Matching fingerprint...";

                    // ✅ STEP 3: Match against THIS SPECIFIC STUDENT's fingerprint template
                    System.Diagnostics.Debug.WriteLine($"Matching against student {student.RollNumber}'s stored template...");

                    if (student.FingerprintTemplate == null || student.FingerprintTemplate.Length == 0)
                    {
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                        System.Windows.MessageBox.Show(
                            $"❌ No Fingerprint Template\n\n" +
                            $"Roll Number: {student.RollNumber}\n\n" +
                            $"This student has no fingerprint template stored.\n" +
                            $"They may not have been registered properly.",
                            "Template Missing",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                        return;
                    }

                    // Match the captured fingerprint against the stored template
                    var matchResult = await _fingerprintService.VerifyAsync(
                        student.FingerprintTemplate,
                        _capturedTemplate!
                    );

                    LoadingOverlay.Visibility = Visibility.Collapsed;

                    System.Diagnostics.Debug.WriteLine($"Match result: IsMatch={matchResult.IsMatch}, Confidence={matchResult.ConfidenceScore}%");

                    // ✅ STEP 4: Display result
                    if (matchResult.IsMatch)
                    {
                        // SUCCESS - Fingerprint matched!
                        StatusIcon.Text = "✓";
                        StatusIcon.Foreground = Brushes.Green;
                        StatusText.Text = "VERIFIED";
                        StatusText.Foreground = Brushes.Green;

                        // Fill student info
                        StudentInfoPanel.Visibility = Visibility.Visible;
                        RollNumberText.Text = student.RollNumber;
                        CollegeText.Text = student.College?.Name ?? "N/A";
                        TestText.Text = student.Test?.Name ?? "N/A";
                        ConfidenceText.Text = $"{matchResult.ConfidenceScore}%";
                        TimeText.Text = DateTime.Now.ToString("HH:mm:ss");

                        ResultPanel.Visibility = Visibility.Visible;
                        EmptyStatePanel.Visibility = Visibility.Collapsed;

                        // Mark student as verified in database
                        student.IsVerified = true;
                        student.VerificationDate = DateTime.Now;

                        // Add verification log
                        var log = new BiometricCommon.Models.VerificationLog
                        {
                            StudentId = student.Id,
                            VerificationDateTime = DateTime.Now,
                            IsSuccessful = true,
                            VerificationType = "Biometric",
                            MatchConfidence = matchResult.ConfidenceScore,
                            VerifiedBy = "System",
                            Remarks = "Fingerprint matched successfully"
                        };

                        context.VerificationLogs.Add(log);
                        await context.SaveChangesAsync();

                        System.Diagnostics.Debug.WriteLine($"✓✓✓ Student {student.RollNumber} verified and saved to database");

                        System.Windows.MessageBox.Show(
                            $"✅ VERIFIED\n\n" +
                            $"Roll Number: {student.RollNumber}\n" +
                            $"College: {student.College?.Name}\n" +
                            $"Test: {student.Test?.Name}\n" +
                            $"Confidence: {matchResult.ConfidenceScore}%\n" +
                            $"Time: {DateTime.Now:HH:mm:ss}",
                            "Success",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                    else
                    {
                        // FAILURE - Fingerprint did NOT match
                        StatusIcon.Text = "✗";
                        StatusIcon.Foreground = Brushes.Red;
                        StatusText.Text = "NOT VERIFIED";
                        StatusText.Foreground = Brushes.Red;
                        StudentInfoPanel.Visibility = Visibility.Collapsed;

                        ResultPanel.Visibility = Visibility.Visible;
                        EmptyStatePanel.Visibility = Visibility.Collapsed;

                        // Add failed verification log
                        var log = new BiometricCommon.Models.VerificationLog
                        {
                            StudentId = student.Id,
                            VerificationDateTime = DateTime.Now,
                            IsSuccessful = false,
                            VerificationType = "Biometric",
                            MatchConfidence = matchResult.ConfidenceScore,
                            VerifiedBy = "System",
                            Remarks = $"Fingerprint mismatch (confidence: {matchResult.ConfidenceScore}%)"
                        };

                        context.VerificationLogs.Add(log);
                        await context.SaveChangesAsync();

                        System.Diagnostics.Debug.WriteLine($"❌ Fingerprint did NOT match for student {student.RollNumber}");

                        System.Windows.MessageBox.Show(
                            $"❌ NOT VERIFIED\n\n" +
                            $"Roll Number: {student.RollNumber}\n" +
                            $"Match Confidence: {matchResult.ConfidenceScore}%\n\n" +
                            $"The fingerprint does not match the registered fingerprint for this student.\n\n" +
                            $"Possible reasons:\n" +
                            $"• Wrong finger placed on scanner\n" +
                            $"• Poor finger placement\n" +
                            $"• Incorrect roll number entered",
                            "Failed",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                    }

                    await LoadStatisticsAsync();
                    RollNumberTextBox.Clear();
                    ClearFingerprintDisplay();
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                System.Diagnostics.Debug.WriteLine($"Error in VerifyButton_Click: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                System.Windows.MessageBox.Show(
                    $"Error during verification:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void DisplayFingerprintImage(byte[] imageData, int width, int height)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Displaying fingerprint image: {width}x{height}");

                // Create bitmap from raw grayscale data
                var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray8, null);

                // Write pixel data
                Int32Rect rect = new Int32Rect(0, 0, width, height);
                bitmap.WritePixels(rect, imageData, width, 0);

                // Display image
                FingerprintImage.Source = bitmap;
                FingerprintImage.Visibility = Visibility.Visible;
                FingerprintPlaceholder.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error displaying fingerprint: {ex.Message}");
            }
        }

        private void DisplayCaptureInfo(int quality, int templateSize, int width, int height)
        {
            CaptureInfoPanel.Visibility = Visibility.Visible;

            QualityText.Text = $"{quality}%";

            // Color code quality
            if (quality >= 80)
            {
                QualityText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                CaptureInfoPanel.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233)); // Light green
            }
            else if (quality >= 60)
            {
                QualityText.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
                CaptureInfoPanel.Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)); // Light orange
            }
            else
            {
                QualityText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                CaptureInfoPanel.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238)); // Light red
            }

            TemplateSizeText.Text = $"{templateSize} bytes";
            ImageSizeText.Text = $"{width} x {height} pixels";
        }

        private void ClearFingerprintDisplay()
        {
            FingerprintImage.Source = null;
            FingerprintImage.Visibility = Visibility.Collapsed;
            FingerprintPlaceholder.Visibility = Visibility.Visible;
            CaptureInfoPanel.Visibility = Visibility.Collapsed;
            QualityText.Text = "-";
            TemplateSizeText.Text = "-";
            ImageSizeText.Text = "-";

            _capturedTemplate = null;
            _capturedImageData = null;
            _imageWidth = 0;
            _imageHeight = 0;
        }

        private async void ManualOverrideButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string rollNumber = RollNumberTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(rollNumber))
                {
                    System.Windows.MessageBox.Show(
                        "Please enter roll number",
                        "Validation",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Check if student exists first
                using (var context = new BiometricCommon.Database.BiometricContext(_importService.GetDatabasePath()))
                {
                    var student = await context.Students
                        .Include(s => s.College)
                        .Include(s => s.Test)
                        .FirstOrDefaultAsync(s => s.RollNumber == rollNumber);

                    if (student == null)
                    {
                        System.Windows.MessageBox.Show(
                            $"❌ Student Not Found\n\n" +
                            $"Roll Number: {rollNumber}\n\n" +
                            $"This roll number does not exist in the imported college package.",
                            "Student Not Found",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    // Prompt for remarks
                    var remarksDialog = new Window
                    {
                        Title = "Manual Override",
                        Width = 400,
                        Height = 250,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ResizeMode = ResizeMode.NoResize
                    };

                    var stackPanel = new StackPanel { Margin = new Thickness(20) };
                    stackPanel.Children.Add(new TextBlock
                    {
                        Text = "Reason for manual override:",
                        Margin = new Thickness(0, 0, 0, 10)
                    });

                    var remarksTextBox = new TextBox
                    {
                        Height = 80,
                        TextWrapping = TextWrapping.Wrap,
                        AcceptsReturn = true,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    };
                    stackPanel.Children.Add(remarksTextBox);

                    var buttonPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 20, 0, 0)
                    };

                    var okButton = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
                    okButton.Click += (s, args) => { remarksDialog.DialogResult = true; remarksDialog.Close(); };

                    var cancelButton = new Button { Content = "Cancel", Width = 80 };
                    cancelButton.Click += (s, args) => { remarksDialog.DialogResult = false; remarksDialog.Close(); };

                    buttonPanel.Children.Add(okButton);
                    buttonPanel.Children.Add(cancelButton);
                    stackPanel.Children.Add(buttonPanel);

                    remarksDialog.Content = stackPanel;

                    if (remarksDialog.ShowDialog() == true)
                    {
                        string remarks = remarksTextBox.Text.Trim();
                        if (string.IsNullOrWhiteSpace(remarks))
                        {
                            System.Windows.MessageBox.Show(
                                "Please provide a reason for manual override",
                                "Validation",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                            return;
                        }

                        // Mark student as verified
                        student.IsVerified = true;
                        student.VerificationDate = DateTime.Now;

                        // Add verification log
                        var log = new BiometricCommon.Models.VerificationLog
                        {
                            StudentId = student.Id,
                            VerificationDateTime = DateTime.Now,
                            IsSuccessful = true,
                            VerificationType = "ManualOverride",
                            MatchConfidence = 0,
                            VerifiedBy = "System",
                            Remarks = remarks
                        };

                        context.VerificationLogs.Add(log);
                        await context.SaveChangesAsync();

                        System.Windows.MessageBox.Show(
                            $"✅ Manual override successful\n\n" +
                            $"Roll Number: {rollNumber}\n" +
                            $"College: {student.College?.Name}\n" +
                            $"Remarks: {remarks}",
                            "Success",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);

                        await LoadStatisticsAsync();
                        RollNumberTextBox.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
}