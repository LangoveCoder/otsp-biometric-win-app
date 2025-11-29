using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using BiometricCollegeVerify.Views;
using BiometricCollegeVerify.Services;
using BiometricCollegeVerify.Views;

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
                NavigationListBox.SelectedIndex = 0;
                MainContentFrame.Navigate(new VerificationView());
            }
            else
            {
                // Set default selection
                NavigationListBox.SelectedIndex = 0;

                // Show welcome message
                System.Windows.MessageBox.Show(
                    "Welcome to the Biometric Verification System!\n\n" +
                    "Please import your college verification package to begin.\n\n" +
                    "Go to: File → Import College Package",
                    "Welcome",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        private void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavigationListBox.SelectedIndex < 0)
                return;

            // Prevent early event firing before initialization
            if (_importService == null)
                return;

            if (!_importService.IsPackageImported())
            {
                System.Windows.MessageBox.Show(
                    "Please import college package first.\n\n" +
                    "Go to: File → Import College Package",
                    "No Package Loaded",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);

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
                        System.Windows.MessageBox.Show(
                            $"✅ Package imported successfully!\n\n" +
                            $"College: {result.CollegeName}\n" +
                            $"Test: {result.TestName}\n" +
                            $"Students: {result.TotalStudents}\n\n" +
                            "You can now start verifying students.",
                            "Import Successful",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);

                        LoadCollegeInfo();
                        MainContentFrame.Navigate(new VerificationView());
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            $"❌ Import failed!\n\n{result.ErrorMessage}",
                            "Import Failed",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Error importing package:\n\n{ex.Message}",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
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
            System.Windows.MessageBox.Show(
                "📖 User Guide\n\n" +
                "1. Import: File → Import College Package\n" +
                "2. Verification: Place finger on scanner\n" +
                "3. Review: Check verification results\n" +
                "4. Reports: View verification logs\n\n" +
                "For detailed help, contact your administrator.",
                "User Guide",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show(
                "✓ Biometric Verification System\n\n" +
                "Version: 1.0.0\n" +
                "College Verification Application\n\n" +
                "This application allows colleges to verify\n" +
                "students using biometric fingerprint authentication.\n\n" +
                "© 2024 - All Rights Reserved",
                "About",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}