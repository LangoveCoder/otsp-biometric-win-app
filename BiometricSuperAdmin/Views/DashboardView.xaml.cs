using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Services;
using BiometricCommon.Models;
using BiometricCommon.Helpers;
using BiometricCommon.Services;
//using BiometricCollegeVerify.Views;

namespace BiometricSuperAdmin.Views
{
    /// <summary>
    /// Interaction logic for DashboardView.xaml
    /// </summary>
    public partial class DashboardView : Page
    {
        private readonly DatabaseService _databaseService;

        public DashboardView()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            Loaded += DashboardView_Loaded;
        }

        /// <summary>
        /// Load dashboard data when page is loaded
        /// </summary>
        private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDashboardDataAsync();
        }

        /// <summary>
        /// Load all dashboard statistics and recent activity
        /// </summary>
        private async Task LoadDashboardDataAsync()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                // Check if database is empty and offer to add sample data
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

                // Get dashboard statistics
                var stats = await _databaseService.GetDashboardStatsAsync();

                // Update statistics cards
                TotalStudentsText.Text = stats.TotalStudents.ToString();
                VerifiedStudentsText.Text = stats.VerifiedStudents.ToString();
                ActiveCollegesText.Text = stats.ActiveColleges.ToString();
                ActiveTestsText.Text = stats.ActiveTests.ToString();

                // Load recent verification logs
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

        /// <summary>
        /// Seed sample data into the database
        /// </summary>
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

        /// <summary>
        /// Load recent verification activity
        /// </summary>
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

        /// <summary>
        /// Navigate to Registration page
        /// </summary>
        private void RegisterStudentButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            NavigationService?.Navigate(new RegistrationView(mainWindow?._scannerService, mainWindow?._context));
        }

        /// <summary>
        /// Navigate to College Management page
        /// </summary>
        private void AddCollegeButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new CollegeManagementView());
        }

        /// <summary>
        /// Navigate to Test Management page
        /// </summary>
        private void CreateTestButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new TestManagementView());
        }

        /// <summary>
        /// Navigate to Package Generator page
        /// </summary>
        private void GeneratePackageButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new PackageGeneratorView());
        }

        /// <summary>
        /// Navigate to Reports page
        /// </summary>
        private void ViewReportsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new ReportsView());
        }

        /// <summary>
        /// Refresh dashboard data
        /// </summary>
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDashboardDataAsync();
        }
    }

    /// <summary>
    /// Model for recent activity display
    /// </summary>
    public class RecentActivity
    {
        public string DateTime { get; set; } = string.Empty;
        public string Activity { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
    }
}
