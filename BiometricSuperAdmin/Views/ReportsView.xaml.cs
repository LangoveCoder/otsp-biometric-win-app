using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Database;
using Microsoft.EntityFrameworkCore;

namespace BiometricSuperAdmin.Views
{
    public partial class ReportsView : Page
    {
        private readonly BiometricContext _context;

        public ReportsView()
        {
            InitializeComponent();
            _context = new BiometricContext();
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
                var totalStudents = await System.Threading.Tasks.Task.Run(() => _context.Students.Count());
                var totalVerified = await System.Threading.Tasks.Task.Run(() => _context.Students.Count(s => s.IsVerified));
                var totalPending = totalStudents - totalVerified;

                TotalVerificationsText.Text = totalStudents.ToString();
                SuccessfulText.Text = totalVerified.ToString();
                FailedText.Text = totalPending.ToString();

                double successRate = 0;
                if (totalStudents > 0)
                {
                    successRate = (totalVerified / (double)totalStudents) * 100;
                }
                SuccessRateText.Text = $"{successRate:F1}%";

                // Get verification logs directly
                var logs = await System.Threading.Tasks.Task.Run(() =>
                    _context.VerificationLogs
                        .Include(l => l.Student)
                        .OrderByDescending(l => l.VerificationDateTime)
                        .Take(100)
                        .ToList()
                );

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