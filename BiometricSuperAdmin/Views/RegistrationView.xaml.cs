using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BiometricCommon.Database;
using BiometricCommon.Models;
using BiometricCommon.Services;
using Microsoft.EntityFrameworkCore;

namespace BiometricSuperAdmin.Views
{
    public partial class RegistrationView : Page
    {
        private readonly IFingerprintScanner _scannerService;
        private readonly BiometricContext? _context;

        public RegistrationView(IFingerprintScanner scannerService, BiometricContext? context)
        {
            InitializeComponent();
            _scannerService = scannerService;
            _context = context;

            Loaded += Page_Loaded;

            // ✅ ADD INPUT VALIDATION - Only allow digits
            RollNumberTextBox.PreviewTextInput += RollNumberTextBox_PreviewTextInput;
            RollNumberTextBox.TextChanged += RollNumberTextBox_TextChanged;

            // ✅ Prevent paste of non-numeric content
            DataObject.AddPastingHandler(RollNumberTextBox, RollNumberTextBox_Pasting);
        }

        // ✅ VALIDATION: Only allow numeric input
        private void RollNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow digits 0-9
            e.Handled = !IsNumeric(e.Text);
        }

        // ✅ VALIDATION: Restrict to 5 digits maximum
        private void RollNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (RollNumberTextBox.Text.Length > 5)
            {
                // Truncate to 5 digits
                RollNumberTextBox.Text = RollNumberTextBox.Text.Substring(0, 5);
                RollNumberTextBox.CaretIndex = 5; // Move cursor to end
            }
        }

        // ✅ VALIDATION: Prevent pasting non-numeric content
        private void RollNumberTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!IsNumeric(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        // ✅ Helper method to check if string contains only digits
        private bool IsNumeric(string text)
        {
            return Regex.IsMatch(text, "^[0-9]+$");
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadContext();
        }

        private void LoadContext()
        {
            var context = RegistrationContext.GetCurrentContext();

            if (context != null)
            {
                CollegeTextBox.Text = context.CollegeName;
                TestTextBox.Text = context.TestName;
                DeviceTextBox.Text = context.LaptopId;
            }
            else
            {
                CollegeTextBox.Text = "Not Set";
                TestTextBox.Text = "Not Set";
                DeviceTextBox.Text = Environment.MachineName;
            }
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check context first
                var registrationContext = RegistrationContext.GetCurrentContext();
                if (registrationContext == null)
                {
                    MessageBox.Show(
                        "Registration context not set!\n\nPlease go to Dashboard and set context first.",
                        "Context Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                System.Diagnostics.Debug.WriteLine("=== Register Button Clicked ===");

                // Validate context
                if (_context == null)
                {
                    MessageBox.Show("Database context is not available!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // ✅ VALIDATE ROLL NUMBER - Must be exactly 5 digits
                string rollNumber = RollNumberTextBox.Text?.Trim();

                if (string.IsNullOrEmpty(rollNumber))
                {
                    MessageBox.Show(
                        "Please enter a roll number!",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    RollNumberTextBox.Focus();
                    return;
                }

                // ✅ CHECK: Must be exactly 5 digits
                if (rollNumber.Length != 5)
                {
                    MessageBox.Show(
                        $"Roll number must be exactly 5 digits!\n\n" +
                        $"You entered: {rollNumber.Length} digit(s)\n" +
                        $"Required: 5 digits\n\n" +
                        $"Example: 12345",
                        "Invalid Roll Number",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    RollNumberTextBox.Focus();
                    RollNumberTextBox.SelectAll();
                    return;
                }

                // ✅ CHECK: Must be numeric only
                if (!Regex.IsMatch(rollNumber, "^[0-9]{5}$"))
                {
                    MessageBox.Show(
                        "Roll number must contain only digits (0-9)!\n\n" +
                        "Please enter a valid 5-digit roll number.",
                        "Invalid Roll Number",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    RollNumberTextBox.Focus();
                    RollNumberTextBox.SelectAll();
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Roll Number: {rollNumber} (Valid 5-digit format)");

                // Check if student already exists
                var existingStudent = await _context.Students
                    .FirstOrDefaultAsync(s => s.RollNumber == rollNumber
                                           && s.CollegeId == registrationContext.CollegeId
                                           && s.TestId == registrationContext.TestId);

                if (existingStudent != null)
                {
                    MessageBox.Show(
                        $"A student with roll number '{rollNumber}' already exists!\n\n" +
                        $"College: {registrationContext.CollegeName}\n" +
                        $"Test: {registrationContext.TestName}\n" +
                        $"Registered on: {existingStudent.RegistrationDate:yyyy-MM-dd HH:mm:ss}",
                        "Duplicate Roll Number",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Check scanner
                if (!_scannerService.IsConnected)
                {
                    MessageBox.Show(
                        "Fingerprint scanner is not connected!\n\n" +
                        "Please ensure:\n" +
                        "1. Scanner is plugged in\n" +
                        "2. Drivers are installed\n" +
                        "3. Application was restarted after driver installation",
                        "Scanner Not Ready",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Scanner is connected, prompting user...");

                // Show loading overlay
                LoadingOverlay.Visibility = Visibility.Visible;
                RegisterButton.IsEnabled = false;

                // Prompt user to place finger
                MessageBox.Show(
                    "Place your RIGHT INDEX finger on the scanner NOW!\n\n" +
                    "Instructions:\n" +
                    "• Clean the scanner surface\n" +
                    "• Place finger flat on the glass\n" +
                    "• Apply firm, even pressure\n" +
                    "• Keep finger still until capture completes\n\n" +
                    "Click OK when ready, then place your finger.",
                    "Ready to Capture",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                System.Diagnostics.Debug.WriteLine("User acknowledged, starting capture...");

                // Capture fingerprint
                var captureResult = await _scannerService.CaptureAsync();

                // Hide loading overlay
                LoadingOverlay.Visibility = Visibility.Collapsed;
                RegisterButton.IsEnabled = true;

                System.Diagnostics.Debug.WriteLine($"Capture result: Success={captureResult.Success}, Quality={captureResult.QualityScore}");

                if (!captureResult.Success)
                {
                    MessageBox.Show(
                        $"Failed to capture fingerprint!\n\n{captureResult.Message}\n\nPlease try again.",
                        "Capture Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Display the fingerprint image
                if (captureResult.ImageData != null && captureResult.ImageWidth > 0 && captureResult.ImageHeight > 0)
                {
                    DisplayFingerprintImage(
                        captureResult.ImageData,
                        captureResult.ImageWidth,
                        captureResult.ImageHeight,
                        captureResult.QualityScore,
                        captureResult.Template?.Length ?? 0
                    );
                }

                System.Diagnostics.Debug.WriteLine($"Template length: {captureResult.Template?.Length ?? 0} bytes");

                // Show capture success with details
                var confirmResult = MessageBox.Show(
                    $"✅ Fingerprint CAPTURED successfully!\n\n" +
                    $"Roll Number: {rollNumber}\n" +
                    $"Quality Score: {captureResult.QualityScore}%\n" +
                    $"Template Size: {captureResult.Template?.Length ?? 0} bytes\n" +
                    $"Image Size: {captureResult.ImageWidth}x{captureResult.ImageHeight}\n\n" +
                    $"Review the fingerprint image on the right panel.\n\n" +
                    $"Click YES to save and register this student.\n" +
                    $"Click NO to cancel and try again.",
                    "Confirm Registration",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmResult != MessageBoxResult.Yes)
                {
                    System.Diagnostics.Debug.WriteLine("User cancelled registration");
                    ClearFingerprintDisplay();
                    return;
                }

                // Create student record using context from RegistrationContext
                var student = new Student
                {
                    RollNumber = rollNumber,
                    CollegeId = registrationContext.CollegeId,
                    TestId = registrationContext.TestId,
                    FingerprintTemplate = captureResult.Template,
                    RegistrationDate = DateTime.Now,
                    DeviceId = registrationContext.LaptopId,
                    IsVerified = false
                };

                // Save to database
                _context.Students.Add(student);
                await _context.SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine($"✓✓✓ Student saved to database: {rollNumber}");

                // Show success message
                MessageBox.Show(
                    $"✅ Registration Complete!\n\n" +
                    $"Roll Number: {rollNumber}\n" +
                    $"College: {registrationContext.CollegeName}\n" +
                    $"Test: {registrationContext.TestName}\n" +
                    $"Fingerprint Quality: {captureResult.QualityScore}%\n" +
                    $"Registered at: {student.RegistrationDate:yyyy-MM-dd HH:mm:ss}",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Clear form for next student
                ClearForm();
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                RegisterButton.IsEnabled = true;

                System.Diagnostics.Debug.WriteLine($"EXCEPTION in RegisterButton_Click: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                MessageBox.Show(
                    $"An error occurred during registration:\n\n{ex.Message}\n\nPlease try again.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DisplayFingerprintImage(byte[] imageData, int width, int height, int quality, int templateSize)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Displaying fingerprint image: {width}x{height}");

                // Create bitmap from raw image data
                var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray8, null);

                // Write pixel data
                Int32Rect rect = new Int32Rect(0, 0, width, height);
                bitmap.WritePixels(rect, imageData, width, 0);

                // Display image
                FingerprintImage.Source = bitmap;
                FingerprintImage.Visibility = Visibility.Visible;
                PlaceholderText.Visibility = Visibility.Collapsed;

                // Show capture info
                CaptureInfoPanel.Visibility = Visibility.Visible;
                QualityText.Text = $"{quality}%";

                // Color code quality
                if (quality >= 80)
                {
                    QualityText.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Green
                }
                else if (quality >= 60)
                {
                    QualityText.Foreground = new SolidColorBrush(Color.FromRgb(251, 140, 0)); // Orange
                }
                else
                {
                    QualityText.Foreground = new SolidColorBrush(Color.FromRgb(211, 47, 47)); // Red
                }

                TemplateSizeText.Text = $"{templateSize} bytes";
                ImageSizeText.Text = $"{width} x {height} pixels";

                System.Diagnostics.Debug.WriteLine($"✓ Image displayed successfully - Quality: {quality}%");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error displaying fingerprint image: {ex.Message}");
                MessageBox.Show(
                    $"Could not display fingerprint image: {ex.Message}",
                    "Display Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void ClearFingerprintDisplay()
        {
            FingerprintImage.Source = null;
            FingerprintImage.Visibility = Visibility.Collapsed;
            PlaceholderText.Visibility = Visibility.Visible;
            CaptureInfoPanel.Visibility = Visibility.Collapsed;
            QualityText.Text = "-";
            TemplateSizeText.Text = "-";
            ImageSizeText.Text = "-";
        }

        private void ClearForm()
        {
            RollNumberTextBox.Clear();
            ClearFingerprintDisplay();
            RollNumberTextBox.Focus();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }
    }
}