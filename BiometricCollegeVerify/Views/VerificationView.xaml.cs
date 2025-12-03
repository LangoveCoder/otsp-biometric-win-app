using System;
using System.Windows;
using System.Windows.Controls;
using BiometricCollegeVerify.Services;

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
                StatusText.Text = $"Error loading stats: {ex.Message}";
            }
        }

        // ADDED: LoadStudentButton_Click event handler
        private async void LoadStudentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string rollNumber = RollNumberTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(rollNumber))
                {
                    System.Windows.MessageBox.Show("Please enter roll number", "Validation",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Clear previous results
                ResultPanel.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;

                StatusText.Text = $"Ready to verify: {rollNumber}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        // ADDED: VerifyButton_Click (renamed from original)
        private async void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string rollNumber = RollNumberTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(rollNumber))
                {
                    System.Windows.MessageBox.Show("Please enter roll number", "Validation",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                StatusText.Text = "Capturing fingerprint...";
                InstructionText.Text = "Place finger on scanner";

                // Capture fingerprint
                var fingerprintTemplate = await _verificationService.CaptureFingerprintAsync();

                if (fingerprintTemplate == null)
                {
                    System.Windows.MessageBox.Show("Fingerprint capture failed", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    StatusText.Text = "Capture failed";
                    return;
                }

                StatusText.Text = "Verifying...";

                // Verify student
                var result = await _verificationService.VerifyStudentAsync(fingerprintTemplate, "System");

                // Display result
                if (result.IsSuccessful && result.Student != null)
                {
                    // Show success
                    StatusIcon.Text = "✓";
                    StatusIcon.Foreground = System.Windows.Media.Brushes.Green;
                    StatusText.Text = "VERIFIED";
                    StatusText.Foreground = System.Windows.Media.Brushes.Green;

                    // Fill student info
                    StudentInfoPanel.Visibility = Visibility.Visible;
                    RollNumberText.Text = result.Student.RollNumber;
                    CollegeText.Text = result.Student.College?.Name ?? "N/A";
                    TestText.Text = result.Student.Test?.Name ?? "N/A";
                    ConfidenceText.Text = $"{result.MatchConfidence}%";
                    TimeText.Text = result.VerificationDateTime.ToString("HH:mm:ss");

                    ResultPanel.Visibility = Visibility.Visible;
                    EmptyStatePanel.Visibility = Visibility.Collapsed;

                    System.Windows.MessageBox.Show(
                        $"✅ VERIFIED\n\nRoll: {result.Student.RollNumber}\nConfidence: {result.MatchConfidence}%",
                        "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    // Show failure
                    StatusIcon.Text = "✗";
                    StatusIcon.Foreground = System.Windows.Media.Brushes.Red;
                    StatusText.Text = "NOT VERIFIED";
                    StatusText.Foreground = System.Windows.Media.Brushes.Red;
                    StudentInfoPanel.Visibility = Visibility.Collapsed;

                    ResultPanel.Visibility = Visibility.Visible;
                    EmptyStatePanel.Visibility = Visibility.Collapsed;

                    System.Windows.MessageBox.Show(
                        "❌ NOT VERIFIED\n\nFingerprint not matched",
                        "Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }

                await LoadStatisticsAsync();
                RollNumberTextBox.Clear();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                StatusText.Text = "Error occurred";
            }
        }

        // ADDED: ManualOverrideButton_Click event handler
        private async void ManualOverrideButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string rollNumber = RollNumberTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(rollNumber))
                {
                    System.Windows.MessageBox.Show("Please enter roll number", "Validation",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Prompt for remarks
                var remarksDialog = new Window
                {
                    Title = "Manual Override",
                    Width = 400,
                    Height = 250,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize
                };

                var stackPanel = new StackPanel { Margin = new Thickness(20) };
                stackPanel.Children.Add(new TextBlock
                {
                    Text = "Reason for manual override:",
                    Margin = new Thickness(0, 0, 0, 10)
                });

                var remarksTextBox = new TextBox
                {
                    Height = 80,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
                stackPanel.Children.Add(remarksTextBox);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 20, 0, 0)
                };

                var okButton = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
                okButton.Click += (s, args) => { remarksDialog.DialogResult = true; remarksDialog.Close(); };

                var cancelButton = new Button { Content = "Cancel", Width = 80 };
                cancelButton.Click += (s, args) => { remarksDialog.DialogResult = false; remarksDialog.Close(); };

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                stackPanel.Children.Add(buttonPanel);

                remarksDialog.Content = stackPanel;

                if (remarksDialog.ShowDialog() == true)
                {
                    string remarks = remarksTextBox.Text.Trim();
                    if (string.IsNullOrWhiteSpace(remarks))
                    {
                        System.Windows.MessageBox.Show("Please provide a reason for manual override", "Validation",
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    var result = await _verificationService.ManualOverrideAsync(rollNumber, "System", remarks);

                    if (result.IsSuccessful)
                    {
                        System.Windows.MessageBox.Show(
                            $"✅ Manual override successful\n\nRoll: {rollNumber}",
                            "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                        await LoadStatisticsAsync();
                        RollNumberTextBox.Clear();
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            $"❌ Manual override failed\n\n{result.Message}",
                            "Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}