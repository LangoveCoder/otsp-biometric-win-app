using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Models;
using BiometricCommon.Services;

namespace BiometricSuperAdmin.Views
{
    public partial class RegistrationView : Page
    {
        private readonly DatabaseService _databaseService;
        private RegContext? _currentContext;

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
                MessageBox.Show(
                    "Registration context not set. Please set the context first.",
                    "Context Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Display context info
            CollegeTextBox.Text = _currentContext.CollegeName;
            TestTextBox.Text = _currentContext.TestName;
            DeviceTextBox.Text = _currentContext.LaptopId;
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate context
                if (_currentContext == null)
                {
                    MessageBox.Show(
                        "Registration context not set. Please set the context first.",
                        "Context Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Validate roll number
                string rollNumber = RollNumberTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(rollNumber))
                {
                    MessageBox.Show(
                        "Please enter a roll number.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    RollNumberTextBox.Focus();
                    return;
                }

                // Check if student already registered
                var existingStudent = _databaseService.GetStudentByRollNumber(rollNumber);

                if (existingStudent != null)
                {
                    var result = MessageBox.Show(
                        $"Student with roll number '{rollNumber}' is already registered.\n\n" +
                        "Do you want to re-register (update fingerprint)?",
                        "Student Exists",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                        return;
                }

                // Show loading
                LoadingOverlay.Visibility = Visibility.Visible;
                RegisterButton.IsEnabled = false;

                // Simulate fingerprint capture (for testing)
                byte[] fingerprintTemplate = SimulateFingerprintCapture();

                // Register student
                await _databaseService.RegisterStudentAsync(
                    rollNumber,
                    _currentContext.CollegeId,
                    _currentContext.TestId,
                    fingerprintTemplate);

                MessageBox.Show(
                    $"Student '{rollNumber}' registered successfully!",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Clear form for next student
                RollNumberTextBox.Clear();
                RollNumberTextBox.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error during registration: {ex.Message}",
                    "Registration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                RegisterButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Simulate fingerprint capture for testing
        /// </summary>
        private byte[] SimulateFingerprintCapture()
        {
            // Generate a unique "fingerprint" based on current time
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
    /// Registration context helper class - renamed to avoid conflicts
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