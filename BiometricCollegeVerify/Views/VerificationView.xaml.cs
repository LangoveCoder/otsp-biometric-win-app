using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BiometricCollegeVerify.Services;
using BiometricCommon.Services;

namespace BiometricCollegeVerify.Views
{
    public partial class VerificationView : Page
    {
        private readonly VerificationService _verificationService;
        private readonly PackageImportService _importService;

        public VerificationView()
        {
            InitializeComponent();

            _importService = new PackageImportService();
            _verificationService = new VerificationService(_importService.GetDatabasePath());

            Loaded += VerificationView_Loaded;
        }

        private async void VerificationView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadStatisticsAsync();
        }

        private async System.Threading.Tasks.Task LoadStatisticsAsync()
        {
            try
            {
                var stats = await _verificationService.GetStatisticsAsync();

                TotalStudentsText.Text = stats.TotalStudents.ToString();
                VerifiedStudentsText.Text = stats.VerifiedStudents.ToString();
                PendingStudentsText.Text = stats.PendingVerification.ToString();
                TodayVerificationsText.Text = stats.TodayVerifications.ToString();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error loading statistics:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VerifyButton.IsEnabled = false;
                InstructionText.Text = "Place finger on scanner...";

                byte[]? fingerprintTemplate = await _verificationService.CaptureFingerprintAsync();

                if (fingerprintTemplate == null)
                {
                    System.Windows.MessageBox.Show(
                        "Failed to capture fingerprint!\n\nPlease ensure scanner is connected.",
                        "Capture Failed",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                InstructionText.Text = "Matching fingerprint...";

                var result = await _verificationService.VerifyStudentAsync(fingerprintTemplate, "System");

                ShowVerificationResult(result);

                await LoadStatisticsAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Verification error:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                VerifyButton.IsEnabled = true;
                InstructionText.Text = "Place finger on scanner";
            }
        }

        private void ShowVerificationResult(VerificationResult result)
        {
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            ResultPanel.Visibility = Visibility.Visible;

            if (result.IsSuccessful)
            {
                StatusIcon.Text = "✓";
                StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                StatusText.Text = "VERIFIED";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));

                if (result.Student != null)
                {
                    StudentInfoPanel.Visibility = Visibility.Visible;
                    RollNumberText.Text = result.Student.RollNumber;
                    CollegeText.Text = result.Student.College?.Name ?? "N/A";
                    TestText.Text = result.Student.Test?.Name ?? "N/A";
                    ConfidenceText.Text = $"{result.MatchConfidence}%";
                    TimeText.Text = result.VerificationDateTime.ToString("hh:mm:ss tt");
                }
            }
            else
            {
                StatusIcon.Text = "✗";
                StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                StatusText.Text = "NOT VERIFIED";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));

                StudentInfoPanel.Visibility = Visibility.Collapsed;

                System.Windows.MessageBox.Show(
                    $"Verification Failed\n\n{result.Message}\n\n" +
                    "Please try again or use Manual Override.",
                    "Verification Failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private async void ManualOverrideButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Manual Override",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var rollLabel = new TextBlock { Text = "Roll Number:", Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(rollLabel, 0);

            var rollTextBox = new TextBox
            {
                Height = 35,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 16),
                FontSize = 14
            };
            Grid.SetRow(rollTextBox, 1);

            var remarksLabel = new TextBlock { Text = "Remarks:", Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(remarksLabel, 2);

            var remarksTextBox = new TextBox
            {
                Height = 60,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                FontSize = 14
            };
            Grid.SetRow(remarksTextBox, 3);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(buttonPanel, 4);

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 100,
                Height = 35,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(224, 224, 224))
            };
            cancelButton.Click += (s, ev) => dialog.DialogResult = false;

            var confirmButton = new Button
            {
                Content = "Confirm",
                Width = 100,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                Foreground = Brushes.White
            };
            confirmButton.Click += (s, ev) => dialog.DialogResult = true;

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(confirmButton);

            grid.Children.Add(rollLabel);
            grid.Children.Add(rollTextBox);
            grid.Children.Add(remarksLabel);
            grid.Children.Add(remarksTextBox);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            if (dialog.ShowDialog() == true)
            {
                string rollNumber = rollTextBox.Text.Trim();
                string remarks = remarksTextBox.Text.Trim();

                if (string.IsNullOrEmpty(rollNumber))
                {
                    System.Windows.MessageBox.Show(
                        "Please enter a roll number.",
                        "Validation Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(remarks))
                {
                    System.Windows.MessageBox.Show(
                        "Please enter remarks for manual override.",
                        "Validation Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    var result = await _verificationService.ManualOverrideAsync(rollNumber, "Admin", remarks);
                    ShowVerificationResult(result);
                    await LoadStatisticsAsync();

                    if (!result.IsSuccessful)
                    {
                        System.Windows.MessageBox.Show(
                            result.Message,
                            "Manual Override Failed",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Error performing manual override:\n\n{ex.Message}",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}