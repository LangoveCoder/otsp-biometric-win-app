using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using BiometricCommon.Services;

namespace BiometricSuperAdmin.Views
{
    public partial class PackageGeneratorView : Page
    {
        private readonly PackageGenerationService _packageService;
        private PackageGenerationResult? _lastResult;

        public PackageGeneratorView()
        {
            InitializeComponent();
            _packageService = new PackageGenerationService();
            Loaded += PackageGeneratorView_Loaded;
        }

        private async void PackageGeneratorView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCollegesAsync();
        }

        private async Task LoadCollegesAsync()
        {
            try
            {
                var colleges = await _packageService.GetCollegesWithStudentCountsAsync();

                if (colleges.Count == 0)
                {
                    MessageBox.Show(
                        "No colleges found in the database.\n\n" +
                        "Please add colleges first in 'Manage Colleges'.",
                        "No Colleges",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                CollegeComboBox.ItemsSource = colleges;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading colleges:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void CollegeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CollegeComboBox.SelectedItem is CollegePackageInfo selectedCollege)
            {
                try
                {
                    // Load tests for selected college
                    var tests = await _packageService.GetTestsForCollegeAsync(selectedCollege.CollegeId);

                    TestComboBox.ItemsSource = tests;
                    TestComboBox.IsEnabled = tests.Count > 0;

                    if (tests.Count == 0)
                    {
                        MessageBox.Show(
                            $"No tests found for {selectedCollege.CollegeName}.\n\n" +
                            "Please add a test for this college first.",
                            "No Tests",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        UpdateSummary();
                        return;
                    }

                    // Auto-select first test
                    if (tests.Count > 0)
                        TestComboBox.SelectedIndex = 0;

                    UpdateSummary();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error loading tests:\n\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            else
            {
                TestComboBox.ItemsSource = null;
                TestComboBox.IsEnabled = false;
                UpdateSummary();
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            if (CollegeComboBox.SelectedItem is not CollegePackageInfo selectedCollege)
            {
                MessageBox.Show(
                    "Please select a college first.",
                    "No College Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Title = "Save Verification Package",
                Filter = "ZIP Package|*.zip",
                FileName = $"{selectedCollege.CollegeCode}_VerificationPackage_{DateTime.Now:yyyyMMdd}.zip",
                DefaultExt = "zip"
            };

            if (saveDialog.ShowDialog() == true)
            {
                OutputPathTextBox.Text = saveDialog.FileName;
                UpdateSummary();
            }
        }

        private async void GeneratePackageButton_Click(object sender, RoutedEventArgs e)
        {
            if (CollegeComboBox.SelectedItem is not CollegePackageInfo selectedCollege)
            {
                MessageBox.Show(
                    "Please select a college.",
                    "No College Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (TestComboBox.SelectedItem is not TestPackageInfo selectedTest)
            {
                MessageBox.Show(
                    "Please select a test.",
                    "No Test Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPathTextBox.Text))
            {
                MessageBox.Show(
                    "Please select an output location.",
                    "No Output Location",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var confirmResult = MessageBox.Show(
                $"Generate verification package?\n\n" +
                $"College: {selectedCollege.CollegeName}\n" +
                $"Test: {selectedTest.TestName}\n" +
                $"Students: {selectedTest.StudentCount}\n\n" +
                $"This will create an encrypted package ready for distribution.",
                "Confirm Package Generation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            await GeneratePackageAsync(selectedCollege.CollegeId, selectedTest.TestId, OutputPathTextBox.Text);
        }

        private async Task GeneratePackageAsync(int collegeId, int testId, string outputPath)
        {
            try
            {
                // Disable controls
                CollegeComboBox.IsEnabled = false;
                TestComboBox.IsEnabled = false;
                BrowseButton.IsEnabled = false;
                GeneratePackageButton.IsEnabled = false;
                LoadingOverlay.Visibility = Visibility.Visible;
                ProgressSection.Visibility = Visibility.Visible;
                ViewReportButton.Visibility = Visibility.Collapsed;
                OpenFolderButton.Visibility = Visibility.Collapsed;

                // Reset progress
                GenerateProgressBar.Value = 0;
                ProgressPercentageTextBlock.Text = "0%";

                // Create progress reporter
                var progress = new Progress<PackageProgress>(p =>
                {
                    GenerateProgressBar.Value = p.Percentage;
                    ProgressPercentageTextBlock.Text = $"{p.Percentage}%";
                    ProgressTextBlock.Text = p.Message;
                });

                // Generate package
                _lastResult = await _packageService.GeneratePackageAsync(collegeId, testId, outputPath, progress);

                // Show result
                LoadingOverlay.Visibility = Visibility.Collapsed;

                if (_lastResult.Success)
                {
                    ViewReportButton.Visibility = Visibility.Visible;
                    OpenFolderButton.Visibility = Visibility.Visible;

                    MessageBox.Show(
                        $"✅ Package generated successfully!\n\n" +
                        $"College: {_lastResult.CollegeName}\n" +
                        $"Test: {_lastResult.TestName}\n" +
                        $"Students: {_lastResult.TotalStudents}\n" +
                        $"Package Size: {_lastResult.PackageSize / 1024.0 / 1024.0:F2} MB\n\n" +
                        $"Location:\n{_lastResult.PackagePath}\n\n" +
                        $"You can now distribute this package to the college.",
                        "Package Generated",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"❌ Package generation failed!\n\n{_lastResult.ErrorMessage}",
                        "Generation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show(
                    $"An error occurred:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable controls
                CollegeComboBox.IsEnabled = true;
                TestComboBox.IsEnabled = true;
                BrowseButton.IsEnabled = true;
                UpdateSummary(); // This will re-enable the generate button if needed
            }
        }

        private void ViewReportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult == null)
            {
                MessageBox.Show(
                    "No package report available.",
                    "No Report",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var report = _packageService.GeneratePackageReport(_lastResult);

            var reportWindow = new Window
            {
                Title = "Package Generation Report",
                Width = 700,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20)
            };

            var textBlock = new TextBlock
            {
                Text = report,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };

            scrollViewer.Content = textBlock;
            reportWindow.Content = scrollViewer;

            reportWindow.ShowDialog();
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult == null || string.IsNullOrEmpty(_lastResult.PackagePath))
            {
                MessageBox.Show(
                    "No package location available.",
                    "No Location",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                string folderPath = Path.GetDirectoryName(_lastResult.PackagePath);
                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                {
                    Process.Start("explorer.exe", $"/select,\"{_lastResult.PackagePath}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error opening folder:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateSummary()
        {
            var selectedCollege = CollegeComboBox.SelectedItem as CollegePackageInfo;
            var selectedTest = TestComboBox.SelectedItem as TestPackageInfo;
            var hasOutputPath = !string.IsNullOrWhiteSpace(OutputPathTextBox.Text);

            if (selectedCollege != null && selectedTest != null && hasOutputPath)
            {
                SummaryTextBlock.Text =
                    $"📦 Ready to generate package for:\n" +
                    $"College: {selectedCollege.CollegeName} ({selectedCollege.CollegeCode})\n" +
                    $"Test: {selectedTest.TestName}\n" +
                    $"Students: {selectedTest.StudentCount}\n" +
                    $"Output: {Path.GetFileName(OutputPathTextBox.Text)}";

                GeneratePackageButton.IsEnabled = true;
            }
            else
            {
                var missing = new System.Collections.Generic.List<string>();
                if (selectedCollege == null) missing.Add("College");
                if (selectedTest == null) missing.Add("Test");
                if (!hasOutputPath) missing.Add("Output location");

                SummaryTextBlock.Text = $"Please select: {string.Join(", ", missing)}";
                GeneratePackageButton.IsEnabled = false;
            }

            // Hide result buttons when selection changes
            ViewReportButton.Visibility = Visibility.Collapsed;
            OpenFolderButton.Visibility = Visibility.Collapsed;
        }
    }
}