using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Services;
using BiometricCommon.Models;

namespace BiometricSuperAdmin.Views
{
    public partial class RegistrationContextView : Page
    {
        private readonly DatabaseService _databaseService;
        private College? _selectedCollege;
        private Test? _selectedTest;

        public RegistrationContextView()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            Loaded += RegistrationContextView_Loaded;
        }

        private async void RegistrationContextView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCollegesAsync();
            LoadCurrentContext();
        }

        private async Task LoadCollegesAsync()
        {
            try
            {
                var colleges = await _databaseService.GetActiveCollegesAsync();
                CollegeComboBox.ItemsSource = colleges;

                if (colleges.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "No colleges found!\n\nPlease create colleges first from the 'Manage Colleges' page.",
                        "No Colleges",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error loading colleges:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async void CollegeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CollegeComboBox.SelectedItem is College college)
            {
                _selectedCollege = college;
                await LoadTestsForCollegeAsync(college.Id);
            }
        }

        private async Task LoadTestsForCollegeAsync(int collegeId)
        {
            try
            {
                // Get all tests for this specific college
                var allTests = await _databaseService.GetAllTestsAsync();
                var collegeTests = allTests.Where(t => t.CollegeId == collegeId && t.IsActive).ToList();

                TestComboBox.ItemsSource = collegeTests;
                TestComboBox.IsEnabled = collegeTests.Count > 0;

                if (collegeTests.Count == 1)
                {
                    // Auto-select if only one test available
                    TestComboBox.SelectedIndex = 0;
                    _selectedTest = collegeTests[0];
                }
                else if (collegeTests.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        $"No tests found for {_selectedCollege?.Name}!\n\nPlease create a test for this college first.",
                        "No Tests",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }

                ValidateForm();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error loading tests:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void ValidateForm()
        {
            _selectedTest = TestComboBox.SelectedItem as Test;

            bool isValid = _selectedCollege != null &&
                          _selectedTest != null &&
                          !string.IsNullOrWhiteSpace(LaptopIdTextBox.Text);

            StartButton.IsEnabled = isValid;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCollege == null || _selectedTest == null)
            {
                System.Windows.MessageBox.Show(
                    "Please select both college and test.",
                    "Validation Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(LaptopIdTextBox.Text))
            {
                System.Windows.MessageBox.Show(
                    "Please enter a Laptop ID.",
                    "Validation Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Save registration context
            var context = new RegistrationContext
            {
                CollegeId = _selectedCollege.Id,
                CollegeName = _selectedCollege.Name,
                TestId = _selectedTest.Id,
                TestName = _selectedTest.Name,
                LaptopId = LaptopIdTextBox.Text.Trim(),
                SetDate = DateTime.Now
            };

            RegistrationContext.SaveContext(context);

            // Show success and navigate to registration
            System.Windows.MessageBox.Show(
                $"Registration context set successfully!\n\n" +
                $"College: {context.CollegeName}\n" +
                $"Test: {context.TestName}\n" +
                $"Laptop: {context.LaptopId}\n\n" +
                $"All registrations will now use this context.",
                "Context Set",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            // Navigate to registration page
            NavigationService?.Navigate(new RegistrationView());
        }

        private void LoadCurrentContext()
        {
            var context = RegistrationContext.GetCurrentContext();
            if (context != null)
            {
                CurrentContextPanel.Visibility = Visibility.Visible;
                CurrentContextText.Text = $"College: {context.CollegeName}\n" +
                                        $"Test: {context.TestName}\n" +
                                        $"Laptop: {context.LaptopId}\n" +
                                        $"Set on: {context.SetDate:dd-MMM-yyyy HH:mm}";

                ChangeContextButton.Visibility = Visibility.Visible;
                StartButton.Content = "Continue Registration";
            }
        }

        private void ChangeContextButton_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to change the registration context?\n\n" +
                "This should only be done when moving to a different college or test.",
                "Confirm Change",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                RegistrationContext.ClearContext();
                CurrentContextPanel.Visibility = Visibility.Collapsed;
                ChangeContextButton.Visibility = Visibility.Collapsed;
                StartButton.Content = "🚀 Start Registration";
                
                CollegeComboBox.SelectedIndex = -1;
                TestComboBox.SelectedIndex = -1;
                TestComboBox.IsEnabled = false;
            }
        }
    }

    /// <summary>
    /// Registration context - stores the active college, test, and laptop ID
    /// </summary>
    public class RegistrationContext
    {
        private static RegistrationContext? _currentContext;
        private const string ContextFileName = "registration_context.json";

        public int CollegeId { get; set; }
        public string CollegeName { get; set; } = string.Empty;
        public int TestId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string LaptopId { get; set; } = string.Empty;
        public DateTime SetDate { get; set; }

        public static void SaveContext(RegistrationContext context)
        {
            _currentContext = context;
            
            // Save to file for persistence
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = System.IO.Path.Combine(appDataPath, "BiometricVerification");
            string filePath = System.IO.Path.Combine(appFolder, ContextFileName);

            var json = System.Text.Json.JsonSerializer.Serialize(context);
            System.IO.File.WriteAllText(filePath, json);
        }

        public static RegistrationContext? GetCurrentContext()
        {
            if (_currentContext != null)
                return _currentContext;

            // Try to load from file
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = System.IO.Path.Combine(appDataPath, "BiometricVerification");
            string filePath = System.IO.Path.Combine(appFolder, ContextFileName);

            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(filePath);
                    _currentContext = System.Text.Json.JsonSerializer.Deserialize<RegistrationContext>(json);
                    return _currentContext;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        public static void ClearContext()
        {
            _currentContext = null;
            
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = System.IO.Path.Combine(appDataPath, "BiometricVerification");
            string filePath = System.IO.Path.Combine(appFolder, ContextFileName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}
