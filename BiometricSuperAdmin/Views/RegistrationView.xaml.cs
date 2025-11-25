using System;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Models;
using BiometricCommon.Services;

namespace BiometricSuperAdmin.Views
{
    public partial class RegistrationView : Page
    {
        private readonly DatabaseService _databaseService;
        private RegistrationContext? _currentContext;

        public RegistrationView()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            Loaded += RegistrationView_Loaded;
        }

        private async void RegistrationView_Loaded(object sender, RoutedEventArgs e)
        {
            // Load registration context
            _currentContext = RegistrationContext.GetCurrentContext();

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
                var existingStudent = await _databaseService.GetStudentByRollNumberAsync(
                    rollNumber,
                    _currentContext.CollegeId,
                    _currentContext.TestId);

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
                // In production, this would call the real fingerprint scanner
                byte[] fingerprintTemplate = SimulateFingerprintCapture();

                // Register or update student
                if (existingStudent != null)
                {
                    // Update existing student
                    existingStudent.FingerprintTemplate = fingerprintTemplate;
                    existingStudent.RegistrationDate = DateTime.Now;
                    existingStudent.LastModifiedDate = DateTime.Now;
                    existingStudent.DeviceId = _currentContext.LaptopId;

                    await _databaseService.UpdateStudentAsync(existingStudent);

                    MessageBox.Show(
                        $"Student '{rollNumber}' re-registered successfully!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Create new student
                    var student = new Student
                    {
                        RollNumber = rollNumber,
                        CollegeId = _currentContext.CollegeId,
                        TestId = _currentContext.TestId,
                        DeviceId = _currentContext.LaptopId,
                        FingerprintTemplate = fingerprintTemplate,
                        RegistrationDate = DateTime.Now,
                        LastModifiedDate = DateTime.Now,
                        IsVerified = false
                    };

                    await _databaseService.RegisterStudentAsync(student);

                    MessageBox.Show(
                        $"Student '{rollNumber}' registered successfully!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

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
        /// In production, this would be replaced with actual scanner code
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
    /// Registration context helper class
    /// </summary>
    public class RegistrationContext
    {
        public int CollegeId { get; set; }
        public string CollegeName { get; set; } = string.Empty;
        public int TestId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string LaptopId { get; set; } = string.Empty;
        public DateTime SetDate { get; set; }

        private static readonly string ContextFilePath =
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BiometricVerification",
                "registration_context.json");

        public static RegistrationContext? GetCurrentContext()
        {
            try
            {
                if (System.IO.File.Exists(ContextFilePath))
                {
                    var json = System.IO.File.ReadAllText(ContextFilePath);
                    return System.Text.Json.JsonSerializer.Deserialize<RegistrationContext>(json);
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
                var directory = System.IO.Path.GetDirectoryName(ContextFilePath);
                if (directory != null && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                System.IO.File.WriteAllText(ContextFilePath, json);
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
                if (System.IO.File.Exists(ContextFilePath))
                {
                    System.IO.File.Delete(ContextFilePath);
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}