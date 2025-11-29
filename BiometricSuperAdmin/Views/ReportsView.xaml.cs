using System;
using System.Windows;
using System.Windows.Controls;
using BiometricCollegeVerify.Services;

namespace BiometricCollegeVerify.Views
{
    public partial class ReportsView : Page
    {
        private readonly VerificationService _verificationService;
        private readonly PackageImportService _importService;

        public ReportsView()
        {
            InitializeComponent();

            _importService = new PackageImportService();
            _verificationService = new VerificationService(_importService.GetDatabasePath());

            Loaded += ReportsView_Loaded;
        }

        private async void ReportsView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                var stats = await _verificationService.GetStatisticsAsync();

                TotalVerificationsText.Text = (stats.SuccessfulVerifications + stats.FailedVerifications).ToString();
                SuccessfulText.Text = stats.SuccessfulVerifications.ToString();
                FailedText.Text = stats.FailedVerifications.ToString();

                double successRate = 0;
                if (stats.SuccessfulVerifications + stats.FailedVerifications > 0)
                {
                    successRate = (stats.SuccessfulVerifications / (double)(stats.SuccessfulVerifications + stats.FailedVerifications)) * 100;
                }
                SuccessRateText.Text = $"{successRate:F1}%";

                var logs = await _verificationService.GetRecentLogsAsync(100);

                if (logs.Count > 0)
                {
                    LogsDataGrid.ItemsSource = logs;
                    LogsDataGrid.Visibility = Visibility.Visible;
                    EmptyStatePanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    LogsDataGrid.Visibility = Visibility.Collapsed;
                    EmptyStatePanel.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading reports:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }
    }
}