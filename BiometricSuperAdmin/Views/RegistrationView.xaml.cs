using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Models;
using BiometricCommon.Services;
using BiometricCommon.Scanners;

namespace BiometricSuperAdmin.Views
{
    public partial class RegistrationView : Page
    {
        private readonly DatabaseService _databaseService;
        private RegContext? _currentContext;
        private FingerprintService? _fingerprintService;

        public RegistrationView()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            Loaded += RegistrationView_Loaded;
        }

        private void RegistrationView_Loaded(object sender, RoutedEventArgs e)
        {
            // Load registration context
            _currentContext = RegContext.GetCurrentContext();

            if (_currentContext == null)
            {
                System.Windows.MessageBox.Show(
                    "Registration context not set. Please set the context first.",
                    "Context Required",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Display context info
            CollegeTextBox.Text = _currentContext.CollegeName;
            TestTextBox.Text = _currentContext.TestName;
            DeviceTextBox.Text = _currentContext.LaptopId;

            // Initialize fingerprint scanner
            InitializeScannerAsync();
        }

        private async void InitializeScannerAsync()
        {
            try
            {
                _fingerprintService = new FingerprintService();

                // Register SecuGen scanner
                _fingerprintService.RegisterScanner(new SecuGenScanner());

                // Try to initialize
                var result = await _fingerprintService.AutoDetectScannerAsync();

                if (result.Success)
                {
                    System.Windows.MessageBox.Show(
                        $"Scanner connected successfully!\n\n{result.Message}",
                        "Scanner Ready",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    var fallbackResult = System.Windows.MessageBox.Show(
                        $"Real scanner not detected:\n{result.Message}\n\n" +
                        "Do you want to use simulated fingerprints for testing?",
                        "Scanner Not Found",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (fallbackResult == System.Windows.MessageBoxResult.Yes)
                    {
                        // Use mock scanner as fallback
                        _fingerprintService.RegisterScanner(new MockFingerprintScanner());
                        await _fingerprintService.AutoDetectScannerAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Scanner initialization error:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate context
                if (_currentContext == null)
                {
                    System.Windows.MessageBox.Show(
                        "Registration context not set. Please set the context first.",
                        "Context Required",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Validate roll number
                string rollNumber = RollNumberTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(rollNumber))
                {
                    System.Windows.MessageBox.Show(
                        "Please enter a roll number.",
                        "Validation Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    RollNumberTextBox.Focus();
                    return;
                }

                // Check if student already registered
                var existingStudent = _databaseService.GetStudentByRollNumber(rollNumber);

                if (existingStudent != null)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Student with roll number '{rollNumber}' is already registered.\n\n" +
                        "Do you want to re-register (update fingerprint)?",
                        "Student Exists",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result == System.Windows.MessageBoxResult.No)
                        return;
                }

                // Show loading
                LoadingOverlay.Visibility = Visibility.Visible;
                RegisterButton.IsEnabled = false;

                // Capture fingerprint
                byte[] fingerprintTemplate;

                if (_fingerprintService != null && _fingerprintService.IsReady())
                {
                    // Prompt user to place finger
                    System.Windows.MessageBox.Show(
                        "Please place your finger on the scanner.",
                        "Fingerprint Capture",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);

                    // Capture from real scanner
                    var captureResult = await _fingerprintService.CaptureAsync();

                    if (!captureResult.Success)
                    {
                        System.Windows.MessageBox.Show(
                            $"Fingerprint capture failed:\n\n{captureResult.Message}\n\n" +
                            $"Quality Score: {captureResult.QualityScore}\n" +
                            $"Reason: {captureResult.FailureReason}",
                            "Capture Failed",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    fingerprintTemplate = captureResult.Template;

                    System.Windows.MessageBox.Show(
                        $"Fingerprint captured successfully!\n\n" +
                        $"Quality Score: {captureResult.QualityScore}%",
                        "Success",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    // Fallback to simulated fingerprint
                    fingerprintTemplate = SimulateFingerprintCapture();
                }

                // Register student
                await _databaseService.RegisterStudentAsync(
                    rollNumber,
                    _currentContext.CollegeId,
                    _currentContext.TestId,
                    fingerprintTemplate);

                System.Windows.MessageBox.Show(
                    $"Student '{rollNumber}' registered successfully!",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                // Clear form for next student
                RollNumberTextBox.Clear();
                RollNumberTextBox.Focus();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error during registration: {ex.Message}",
                    "Registration Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                RegisterButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Simulate fingerprint capture for testing (fallback)
        /// </summary>
        private byte[] SimulateFingerprintCapture()
        {
            var data = $"FP-{DateTime.Now.Ticks}-{RollNumberTextBox.Text}";
            return System.Text.Encoding.UTF8.GetBytes(data);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            RollNumberTextBox.Clear();
            RollNumberTextBox.Focus();
        }
    }

    /// <summary>
    /// Registration context helper class
    /// </summary>
    public class RegContext
    {
        public int CollegeId { get; set; }
        public string CollegeName { get; set; } = string.Empty;
        public int TestId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string LaptopId { get; set; } = string.Empty;
        public DateTime SetDate { get; set; }

        private static readonly string ContextFilePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BiometricVerification",
                "registration_context.json");

        public static RegContext? GetCurrentContext()
        {
            try
            {
                if (File.Exists(ContextFilePath))
                {
                    var json = File.ReadAllText(ContextFilePath);
                    return JsonSerializer.Deserialize<RegContext>(json);
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(ContextFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ContextFilePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save context: {ex.Message}");
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(ContextFilePath))
                {
                    File.Delete(ContextFilePath);
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}