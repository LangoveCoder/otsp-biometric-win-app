using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
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

            // Add value converters
            Resources.Add("StatusColorConverter", new StatusColorConverter());
            Resources.Add("StatusTextConverter", new StatusTextConverter());

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
                // Load statistics
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

                // Load logs
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
                System.Windows.MessageBox.Show(
                    $"Error loading reports:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }
    }

    #region Value Converters

    /// <summary>
    /// Converter for verification status color
    /// </summary>
    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSuccessful)
            {
                return isSuccessful
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))  // Green
                    : new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for verification status text
    /// </summary>
    public class StatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSuccessful)
            {
                return isSuccessful ? "SUCCESS" : "FAILED";
            }
            return "UNKNOWN";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}