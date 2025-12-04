using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Database;
using BiometricCommon.Helpers;
using BiometricCommon.Models;
using BiometricCommon.Services;
using BiometricSuperAdmin.Services;
using BiometricSuperAdmin.Views;
using Microsoft.Win32;

namespace BiometricSuperAdmin.Views
{
    public partial class DashboardView : Page
    {
        private readonly DatabaseService _databaseService;

        public DashboardView()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            Loaded += DashboardView_Loaded;
        }

        private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            var context = RegistrationContext.GetCurrentContext();

            if (ImportStudentsButton != null)
            {
                ImportStudentsButton.IsEnabled = (context != null);
            }

            if (ViewStudentsButton != null && context != null)
            {
                using (var db = new BiometricContext())
                {
                    var hasStudents = db.Students.Any(s =>
                        s.CollegeId == context.CollegeId &&
                        s.TestId == context.TestId);
                    ViewStudentsButton.IsEnabled = hasStudents;
                }
            }
            else if (ViewStudentsButton != null)
            {
                ViewStudentsButton.IsEnabled = false;
            }

            await LoadDashboardDataAsync();
        }

        private async Task LoadDashboardDataAsync()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                var colleges = await _databaseService.GetAllCollegesAsync();
                if (colleges.Count == 0)
                {
                    var result = System.Windows.MessageBox.Show(
                        "The database is empty. Would you like to add sample data?\n\n" +
                        "This will create:\n" +
                        "• 5 colleges\n" +
                        "• 3 tests\n" +
                        "• 100+ sample students\n" +
                        "• Verification logs",
                        "Add Sample Data?",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        await SeedSampleDataAsync();
                    }
                }

                var stats = await _databaseService.GetDashboardStatsAsync();

                TotalStudentsText.Text = stats.TotalStudents.ToString();
                VerifiedStudentsText.Text = stats.VerifiedStudents.ToString();
                ActiveCollegesText.Text = stats.ActiveColleges.ToString();
                ActiveTestsText.Text = stats.ActiveTests.ToString();

                await LoadRecentActivityAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error loading dashboard data:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async Task SeedSampleDataAsync()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                await SampleDataSeeder.SeedSampleDataAsync(_databaseService);

                System.Windows.MessageBox.Show(
                    "Sample data added successfully!\n\n" +
                    "• 5 Colleges created\n" +
                    "• 3 Tests created\n" +
                    "• 100+ Students registered\n" +
                    "• Verification logs added",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error adding sample data:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadRecentActivityAsync()
        {
            try
            {
                var logs = await _databaseService.GetTodayVerificationLogsAsync();

                if (logs.Count > 0)
                {
                    var activityList = new List<RecentActivity>();

                    foreach (var log in logs)
                    {
                        activityList.Add(new RecentActivity
                        {
                            DateTime = log.VerificationDateTime.ToString("dd-MMM HH:mm"),
                            Activity = $"{log.Student?.RollNumber} - {(log.IsSuccessful ? "Verified ✓" : "Failed ✗")}",
                            User = log.VerifiedBy
                        });
                    }

                    RecentActivityGrid.ItemsSource = activityList;
                    RecentActivityGrid.Visibility = Visibility.Visible;
                    NoActivityText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    RecentActivityGrid.Visibility = Visibility.Collapsed;
                    NoActivityText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error loading recent activity:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        // ✅ FIX: Use sidebar navigation instead of NavigationService
        private void RegisterStudentButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.NavigationListBox != null)
            {
                mainWindow.NavigationListBox.SelectedIndex = 1;
            }
        }

        private void ViewStudentsButton_Click(object sender, RoutedEventArgs e)
        {
            var context = RegistrationContext.GetCurrentContext();
            if (context == null)
            {
                MessageBox.Show("Set context first!", "Context Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var db = new BiometricContext())
            {
                var hasStudents = db.Students.Any(s =>
                    s.CollegeId == context.CollegeId &&
                    s.TestId == context.TestId);

                if (!hasStudents)
                {
                    MessageBox.Show(
                        "No students imported yet!\n\nImport students first using 'Import Students' button.",
                        "No Students",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
            }

            NavigationService?.Navigate(new StudentsListView());
        }

        // ✅ FIX: Use sidebar navigation
        private void AddCollegeButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.NavigationListBox != null)
            {
                mainWindow.NavigationListBox.SelectedIndex = 2;
            }
        }

        // ✅ FIX: Use sidebar navigation
        private void CreateTestButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.NavigationListBox != null)
            {
                mainWindow.NavigationListBox.SelectedIndex = 3;
            }
        }

        // ✅ FIX: Use sidebar navigation with validation
        private void GeneratePackageButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.NavigationListBox != null)
            {
                mainWindow.NavigationListBox.SelectedIndex = 4;
            }
        }

        // ✅ FIX: Use sidebar navigation
        private void ViewReportsButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.NavigationListBox != null)
            {
                mainWindow.NavigationListBox.SelectedIndex = 5;
            }
        }

        public async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDashboardDataAsync();
        }

        private async void ImportStudentsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var context = RegistrationContext.GetCurrentContext();
                if (context == null)
                {
                    MessageBox.Show(
                        "Registration context not set!\n\n" +
                        "Steps:\n" +
                        "1. Create a college (Manage Colleges)\n" +
                        "2. Create a test (Manage Tests)\n" +
                        "3. Set context (Tools → Set Context)\n" +
                        "4. Then import students",
                        "Context Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                using (var db = new BiometricContext())
                {
                    var college = db.Colleges.Find(context.CollegeId);
                    var test = db.Tests.Find(context.TestId);

                    if (college == null || test == null)
                    {
                        MessageBox.Show(
                            "College or Test not found in database!\n\n" +
                            "The context may be outdated.\n" +
                            "Please set context again.",
                            "Invalid Context",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                }

                var openDialog = new OpenFileDialog
                {
                    Filter = "Excel Files|*.xlsx;*.xls",
                    Title = "Select Student List Excel File"
                };

                if (openDialog.ShowDialog() == true)
                {
                    LoadingOverlay.Visibility = Visibility.Visible;

                    var importService = new StudentExcelImportService();
                    var result = importService.ImportFromExcel(
                        openDialog.FileName,
                        context.CollegeId,
                        context.TestId);

                    LoadingOverlay.Visibility = Visibility.Collapsed;

                    if (result.Errors.Count > 0)
                    {
                        string errorList = string.Join("\n", result.Errors.Take(5));
                        MessageBox.Show($"Errors:\n\n{errorList}", "Import Errors",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (result.Success)
                    {
                        using (var dbContext = new BiometricContext())
                        {
                            var saveResult = importService.SaveToDatabase(
                                dbContext, result.Students,
                                context.CollegeId, context.TestId);

                            MessageBox.Show($"Import Complete!\n\n{saveResult.GetSummary()}",
                                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                            var mainWindow = Application.Current.MainWindow as MainWindow;
                            mainWindow?.RefreshNavigationState();

                            if (ViewStudentsButton != null)
                            {
                                ViewStudentsButton.IsEnabled = true;
                            }

                            await LoadDashboardDataAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Error: {ex.Message}\n\nType: {ex.GetType().Name}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public class RecentActivity
        {
            public string DateTime { get; set; } = string.Empty;
            public string Activity { get; set; } = string.Empty;
            public string User { get; set; } = string.Empty;
        }
    }
}