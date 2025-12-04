using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
            if (_importService.IsPackageImported())
            {
                LoadCollegeInfo();
                NavigateToVerification();
            }
            else
            {
                ShowWelcomePrompt();
            }
        }

        private void ShowWelcomePrompt()
        {
            var result = MessageBox.Show(
                "📦 Welcome to BACT Biometric Verification System!\n\n" +
                "No college package has been loaded yet.\n\n" +
                "To get started:\n" +
                "  1. Click the orange 'Import Package' button\n" +
                "  2. Select your verification package (.zip file)\n" +
                "  3. Begin verifying students\n\n" +
                "Would you like to import a package now?",
                "Import College Package",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _ = ImportPackageAsync();
            }
        }

        #region Package Import

        private async void ImportPackageButton_Click(object sender, MouseButtonEventArgs e)
        {
            await ImportPackageAsync();
        }

        private async System.Threading.Tasks.Task ImportPackageAsync()
        {
            var openDialog = new OpenFileDialog
            {
                Title = "Select College Verification Package",
                Filter = "ZIP Package (*.zip)|*.zip|All Files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    ShowLoading("Importing package...");

                    var result = await _importService.ImportPackageAsync(openDialog.FileName);

                    HideLoading();

                    if (result.Success)
                    {
                        MessageBox.Show(
                            $"✅ Package imported successfully!\n\n" +
                            $"College: {result.CollegeName}\n" +
                            $"Test: {result.TestName}\n" +
                            $"Students: {result.TotalStudents:N0}\n" +
                            $"Package Date: {result.PackageDate:yyyy-MM-dd}\n\n" +
                            "You can now start verifying students.",
                            "Import Successful",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        LoadCollegeInfo();
                        NavigateToVerification();
                    }
                    else
                    {
                        MessageBox.Show(
                            $"❌ Package import failed!\n\n{result.ErrorMessage}\n\n" +
                            "Please ensure:\n" +
                            "  • The package file is valid\n" +
                            "  • The file is not corrupted\n" +
                            "  • You have selected the correct package",
                            "Import Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    HideLoading();
                    MessageBox.Show(
                        $"❌ An error occurred while importing:\n\n{ex.Message}\n\n" +
                        "Please contact technical support if this problem persists.",
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
                    TestNameText.Text = info.TestName;
                    StudentCountText.Text = info.TotalStudents.ToString("N0");
                    Title = $"BACT - {info.CollegeName} - Biometric Verification";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"⚠️ Warning: Could not load college information.\n\n{ex.Message}",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        #endregion

        #region Navigation

        private void VerificationNav_Click(object sender, MouseButtonEventArgs e)
        {
            NavigateToVerification();
        }

        private void ReportsNav_Click(object sender, MouseButtonEventArgs e)
        {
            NavigateToReports();
        }

        private void StudentsListNav_Click(object sender, MouseButtonEventArgs e)
        {
            NavigateToStudentsList();
        }

        private void NavigateToVerification()
        {
            if (!ValidatePackageLoaded())
                return;

            try
            {
                ContentFrame.Navigate(new VerificationView());
                SetActiveNav(VerificationNav);
                PageTitleText.Text = "Biometric Verification";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation error:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToReports()
        {
            if (!ValidatePackageLoaded())
                return;

            try
            {
                ContentFrame.Navigate(new ReportsView());
                SetActiveNav(ReportsNav);
                PageTitleText.Text = "Verification Reports";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation error:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToStudentsList()
        {
            if (!ValidatePackageLoaded())
                return;

            try
            {
                ContentFrame.Navigate(new StudentsListView());
                SetActiveNav(StudentsListNav);
                PageTitleText.Text = "Students List";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation error:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidatePackageLoaded()
        {
            if (!_importService.IsPackageImported())
            {
                var result = MessageBox.Show(
                    "⚠️ No college package loaded!\n\n" +
                    "Please import a verification package to continue.\n\n" +
                    "Would you like to import a package now?",
                    "Package Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _ = ImportPackageAsync();
                }

                return false;
            }

            return true;
        }

        private void SetActiveNav(System.Windows.Controls.Border navBorder)
        {
            VerificationNav.Background = Brushes.Transparent;
            ReportsNav.Background = Brushes.Transparent;
            StudentsListNav.Background = Brushes.Transparent;

            var activeColor = (Color)ColorConverter.ConvertFromString("#0D47A1")!;
            navBorder.Background = new SolidColorBrush(activeColor);
        }

        #endregion

        #region Menu Handlers

        private void UserGuide_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "📖 BACT Verification System - User Guide\n\n" +
                "═══════════════════════════════════════\n\n" +
                "1️⃣ IMPORT PACKAGE\n" +
                "   • Click the orange 'Import Package' button\n" +
                "   • Select the .zip package file from BACT\n" +
                "   • Wait for the import to complete\n\n" +
                "2️⃣ VERIFY STUDENTS\n" +
                "   • Go to 'Verification' section\n" +
                "   • Enter student roll number\n" +
                "   • Click 'Load Student' to view details\n" +
                "   • Place finger on scanner\n" +
                "   • Click 'Scan & Verify' to authenticate\n\n" +
                "3️⃣ MANUAL OVERRIDE\n" +
                "   • Use only when fingerprint fails\n" +
                "   • Requires supervisor authorization\n" +
                "   • Must provide reason for override\n\n" +
                "4️⃣ VIEW REPORTS\n" +
                "   • Go to 'Reports' section\n" +
                "   • View verification logs\n" +
                "   • Export statistics as needed\n\n" +
                "═══════════════════════════════════════\n\n" +
                "For technical support:\n" +
                "Contact BACT Administration",
                "User Guide",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "🎓 BACT Biometric Verification System\n\n" +
                "═══════════════════════════════════════\n\n" +
                "Version: 1.0.0\n" +
                "Application: College Verification\n\n" +
                "This application enables colleges to verify\n" +
                "student identities using biometric fingerprint\n" +
                "authentication during examinations.\n\n" +
                "Features:\n" +
                "  ✓ Secure package import\n" +
                "  ✓ Fingerprint verification\n" +
                "  ✓ Manual override capability\n" +
                "  ✓ Comprehensive reporting\n" +
                "  ✓ Offline operation\n\n" +
                "═══════════════════════════════════════\n\n" +
                "Balochistan Academy for College Teachers\n" +
                "© 2024 - All Rights Reserved\n\n" +
                "Developed for secure student verification",
                "About BACT System",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to exit the application?\n\n" +
                "All unsaved changes will be lost.",
                "Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }

            base.OnClosing(e);
        }

        #endregion

        #region Loading Overlay

        private void ShowLoading(string message = "Loading...")
        {
            Dispatcher.Invoke(() =>
            {
                LoadingText.Text = message;
                LoadingOverlay.Visibility = Visibility.Visible;
            });
        }

        private void HideLoading()
        {
            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            });
        }

        #endregion
    }
}