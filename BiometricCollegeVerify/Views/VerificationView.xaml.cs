using System;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Services;
using BiometricCommon.Scanners;

namespace BiometricCollegeVerify.Views
{
    public partial class VerificationView : Page
    {
        private FingerprintService? _fingerprintService;
        private readonly DatabaseService _databaseService;

        public VerificationView()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            Loaded += VerificationView_Loaded;
        }

        private async void VerificationView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize fingerprint scanner
                _fingerprintService = new FingerprintService();
                _fingerprintService.RegisterScanner(new SecuGenScanner());
                _fingerprintService.RegisterScanner(new MockFingerprintScanner());

                var result = await _fingerprintService.AutoDetectScannerAsync();

                if (result.Success)
                {
                    StatusText.Text = $"Scanner Ready: {_fingerprintService.GetCurrentScannerInfo()?.Name}";
                }
                else
                {
                    StatusText.Text = "Scanner not detected - Using mock scanner";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private async void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string rollNumber = RollNumberTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(rollNumber))
                {
                    System.Windows.MessageBox.Show("Please enter roll number", "Validation",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Find student
                var student = _databaseService.GetStudentByRollNumber(rollNumber);

                if (student == null)
                {
                    System.Windows.MessageBox.Show("Student not found", "Not Found",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Prompt for fingerprint
                System.Windows.MessageBox.Show("Place finger on scanner", "Scan",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                // Capture fingerprint
                if (_fingerprintService == null || !_fingerprintService.IsReady())
                {
                    System.Windows.MessageBox.Show("Scanner not ready", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                var captureResult = await _fingerprintService.CaptureAsync();

                if (!captureResult.Success)
                {
                    System.Windows.MessageBox.Show($"Capture failed: {captureResult.Message}", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                // Match fingerprint
                var matchResult = await _fingerprintService.VerifyAsync(student.FingerprintTemplate, captureResult.Template);

                if (matchResult.IsMatch)
                {
                    System.Windows.MessageBox.Show(
                        $"✅ VERIFIED\n\nRoll: {student.RollNumber}\nConfidence: {matchResult.ConfidenceScore}%",
                        "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        $"❌ NOT VERIFIED\n\nConfidence: {matchResult.ConfidenceScore}%",
                        "Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }

                RollNumberTextBox.Clear();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}