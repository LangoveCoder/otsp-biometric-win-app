using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using BiometricCollegeVerify.Views;
using BiometricCollegeVerify.Services;

namespace BiometricCollegeVerify
{
    public partial class MainWindow : Window
    {
        private readonly PackageImportService _importService;

        public MainWindow()
        {
            InitializeComponent();
            _importService = new PackageImportService();

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Check if college data is already loaded
            if (_importService.IsPackageImported())
            {
                LoadCollegeInfo();
                MainContentFrame.Navigate(new VerificationView());
            }
            else
            {
                // Show welcome message
                MessageBox.Show(
                    "Welcome to the Biometric Verification System!\n\n" +
                    "Please import your college verification package to begin.\n\n" +
                    "Go to: File → Import College Package",
                    "Welcome",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavigationListBox.SelectedIndex < 0)
                return;

            if (!_importService.IsPackageImported())
            {
                MessageBox.Show(
                    "Please import college package first.\n\n" +
                    "Go to: File → Import College Package",
                    "No Package Loaded",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                NavigationListBox.SelectedIndex = 0;
                return;
            }

            switch (NavigationListBox.SelectedIndex)
            {
                case 0:
                    MainContentFrame.Navigate(new VerificationView());
                    break;
                case 1:
                    MainContentFrame.Navigate(new ReportsView());
                    break;
            }
        }

        private async void ImportPackage_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Title = "Select College Verification Package",
                Filter = "ZIP Package|*.zip",
                CheckFileExists = true
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    var result = await _importService.ImportPackageAsync(openDialog.FileName);

                    if (result.Success)
                    {
                        MessageBox.Show(
                            $"✅ Package imported successfully!\n\n" +
                            $"College: {result.CollegeName}\n" +
                            $"Test: {result.TestName}\n" +
                            $"Students: {result.TotalStudents}\n\n" +
                            "You can now start verifying students.",
                            "Import Successful",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        LoadCollegeInfo();
                        MainContentFrame.Navigate(new VerificationView());
                    }
                    else
                    {
                        MessageBox.Show(
                            $"❌ Import failed!\n\n{result.ErrorMessage}",
                            "Import Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error importing package:\n\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void LoadCollegeInfo()
        {
            try
            {
                var info = _importService.GetCollegeInfo();
                if (info != null)
                {
                    CollegeInfoPanel.Visibility = Visibility.Visible;
                    CollegeNameText.Text = info.CollegeName;
                    StudentCountText.Text = $"Students: {info.TotalStudents}";
                    Title = $"Verification System - {info.CollegeName}";
                }
            }
            catch
            {
                // Ignore errors loading college info
            }
        }

        private void UserGuide_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "📖 User Guide\n\n" +
                "1. Import: File → Import College Package\n" +
                "2. Verification: Place finger on scanner\n" +
                "3. Review: Check verification results\n" +
                "4. Reports: View verification logs\n\n" +
                "For detailed help, contact your administrator.",
                "User Guide",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "✓ Biometric Verification System\n\n" +
                "Version: 1.0.0\n" +
                "College Verification Application\n\n" +
                "This application allows colleges to verify\n" +
                "students using biometric fingerprint authentication.\n\n" +
                "© 2024 - All Rights Reserved",
                "About",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}