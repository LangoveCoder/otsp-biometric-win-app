using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Services;
using BiometricCommon.Models;
using BiometricCommon.FingerprintSDK;

namespace BiometricSuperAdmin.Views
{
    /// <summary>
    /// Interaction logic for RegistrationView.xaml
    /// </summary>
    public partial class RegistrationView : Page
    {
        private readonly DatabaseService _databaseService;
        private IFingerprintScanner? _scanner;
        private byte[] _capturedTemplate = Array.Empty<byte>();
        private int _selectedCollegeId = 0;
        private int _selectedTestId = 0;

        public RegistrationView()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            Loaded += RegistrationView_Loaded;
        }

        private async void RegistrationView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCollegesAndTestsAsync();
        }

        /// <summary>
        /// Load colleges and tests into combo boxes
        /// </summary>
        private async Task LoadCollegesAndTestsAsync()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                var colleges = await _databaseService.GetActiveCollegesAsync();
                var tests = await _databaseService.GetActiveTestsAsync();

                CollegeComboBox.ItemsSource = colleges;
                TestComboBox.ItemsSource = tests;

                if (colleges.Count == 0)
                {
                    MessageBox.Show(
                        "No colleges found. Please add colleges first.",
                        "No Colleges",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                if (tests.Count == 0)
                {
                    MessageBox.Show(
                        "No tests found. Please create a test first.",
                        "No Tests",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading data:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Initialize fingerprint scanner
        /// </summary>
        private async void InitializeScannerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                // Create scanner instance
                _scanner = ScannerFactory.CreateScanner(ScannerType.Generic);

                // Initialize scanner
                bool initialized = await _scanner.InitializeAsync();

                if (initialized)
                {
                    ScannerStatusIcon.Text = "✅";
                    ScannerStatusText.Text = $"Scanner Ready - {_scanner.ScannerType}";
                    CaptureFingerprintButton.IsEnabled = true;

                    MessageBox.Show(
                        "Scanner initialized successfully!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    throw new Exception("Scanner initialization failed");
                }
            }
            catch (Exception ex)
            {
                ScannerStatusIcon.Text = "❌";
                ScannerStatusText.Text = "Scanner Not Available";

                MessageBox.Show(
                    $"Failed to initialize scanner:\n\n{ex.Message}",
                    "Scanner Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Capture fingerprint from scanner
        /// </summary>
        private async void CaptureFingerprintButton_Click(object sender, RoutedEventArgs e)
        {
            if (_scanner == null)
            {
                MessageBox.Show(
                    "Please initialize the scanner first.",
                    "Scanner Not Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                // Capture fingerprint
                var result = await _scanner.CaptureFingerprintAsync();

                if (result.Success)
                {
                    _capturedTemplate = result.Template;

                    // Show success
                    FingerprintPlaceholder.Visibility = Visibility.Collapsed;
                    FingerprintImage.Visibility = Visibility.Visible;

                    MessageBox.Show(
                        $"Fingerprint captured successfully!\n\nQuality: {result.Quality}%",
                        "Capture Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Enable register button if all fields are filled
                    ValidateForm();
                }
                else
                {
                    MessageBox.Show(
                        $"Failed to capture fingerprint:\n\n{result.Message}",
                        "Capture Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error capturing fingerprint:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Register student with captured fingerprint
        /// </summary>
        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RollNumberTextBox.Text))
            {
                MessageBox.Show(
                    "Please enter roll number.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_capturedTemplate.Length == 0)
            {
                MessageBox.Show(
                    "Please capture fingerprint first.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                // Register student
                var student = await _databaseService.RegisterStudentAsync(
                    RollNumberTextBox.Text.Trim(),
                    _selectedCollegeId,
                    _selectedTestId,
                    _capturedTemplate);

                MessageBox.Show(
                    $"Student registered successfully!\n\nRoll Number: {student.RollNumber}",
                    "Registration Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Clear form
                ClearForm();

                // Refresh list
                await LoadRegisteredStudentsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error registering student:\n\n{ex.Message}",
                    "Registration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Load registered students for selected college and test
        /// </summary>
        private async Task LoadRegisteredStudentsAsync()
        {
            if (_selectedCollegeId == 0 || _selectedTestId == 0)
                return;

            try
            {
                var students = await _databaseService.GetStudentsByCollegeAndTestAsync(
                    _selectedCollegeId,
                    _selectedTestId);

                RegisteredStudentsGrid.ItemsSource = students;
                RegistrationCountText.Text = $"{students.Count} students registered";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading students:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handle college selection change
        /// </summary>
        private async void CollegeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CollegeComboBox.SelectedItem is College college)
            {
                _selectedCollegeId = college.Id;
                await LoadRegisteredStudentsAsync();
                ValidateForm();
            }
        }

        /// <summary>
        /// Handle test selection change
        /// </summary>
        private async void TestComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TestComboBox.SelectedItem is Test test)
            {
                _selectedTestId = test.Id;
                await LoadRegisteredStudentsAsync();
                ValidateForm();
            }
        }

        /// <summary>
        /// Refresh registered students list
        /// </summary>
        private async void RefreshListButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadRegisteredStudentsAsync();
        }

        /// <summary>
        /// Validate form and enable/disable register button
        /// </summary>
        private void ValidateForm()
        {
            RegisterButton.IsEnabled = 
                _selectedCollegeId > 0 &&
                _selectedTestId > 0 &&
                !string.IsNullOrWhiteSpace(RollNumberTextBox.Text) &&
                _capturedTemplate.Length > 0;
        }

        /// <summary>
        /// Clear registration form
        /// </summary>
        private void ClearForm()
        {
            RollNumberTextBox.Clear();
            _capturedTemplate = Array.Empty<byte>();
            FingerprintPlaceholder.Visibility = Visibility.Visible;
            FingerprintImage.Visibility = Visibility.Collapsed;
            RegisterButton.IsEnabled = false;
        }
    }
}
