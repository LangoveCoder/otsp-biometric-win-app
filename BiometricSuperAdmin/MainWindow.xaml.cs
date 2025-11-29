using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using BiometricSuperAdmin.Views;
using BiometricCommon.Services;
using BiometricCollegeVerify.Views;

namespace BiometricSuperAdmin
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private readonly MasterConfigService _configService;

        public MainWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _configService = new MasterConfigService();

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Step 1: Check if database is empty and auto-import exists
            if (_configService.IsDatabaseEmpty())
            {
                if (MasterConfigService.AutoImportFileExists())
                {
                    var result = System.Windows.MessageBox.Show(
                        "✨ Master configuration file detected!\n\n" +
                        "Do you want to import colleges and tests automatically?\n\n" +
                        "📋 This will setup your laptop with all colleges and tests.",
                        "Auto-Import Configuration",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        try
                        {
                            var importResult = await _configService.ImportMasterConfigAsync(
                                MasterConfigService.GetAutoImportFilePath());

                            System.Windows.MessageBox.Show(
                                importResult.GetSummary(),
                                importResult.Success ? "✅ Import Successful" : "❌ Import Failed",
                                System.Windows.MessageBoxButton.OK,
                                importResult.Success ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Error);
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show(
                                $"Failed to auto-import configuration:\n\n{ex.Message}",
                                "Import Error",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Error);
                        }
                    }
                }
            }

            // Step 2: Check if registration context is set
            var context = RegistrationContext.GetCurrentContext();

            if (context == null)
            {
                MainContentFrame.Navigate(new RegistrationContextView());
            }
            else
            {
                MainContentFrame.Navigate(new DashboardView());
                Title = $"Biometric Verification System - {context.LaptopId} - {context.CollegeName}";
            }
        }

        private void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavigationListBox == null || NavigationListBox.SelectedIndex < 0)
                return;

            if (MainContentFrame == null)
                return;

            if (NavigationListBox.SelectedIndex == 1) // Student Registration
            {
                var context = RegistrationContext.GetCurrentContext();
                if (context == null)
                {
                    System.Windows.MessageBox.Show(
                        "Please set registration context first!\n\n" +
                        "Go to Dashboard and set which college and test you're registering for.",
                        "Context Not Set",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);

                    NavigationListBox.SelectedIndex = 0;
                    return;
                }
            }

            switch (NavigationListBox.SelectedIndex)
            {
                case 0: MainContentFrame.Navigate(new DashboardView()); break;
                case 1: MainContentFrame.Navigate(new RegistrationView()); break;
                case 2: MainContentFrame.Navigate(new CollegeManagementView()); break;
                case 3: MainContentFrame.Navigate(new TestManagementView()); break;
                case 4: MainContentFrame.Navigate(new PackageGeneratorView()); break;
                case 5: MainContentFrame.Navigate(new ReportsView()); break;
            }
        }

        // ==================== MENU HANDLERS ====================

        private async void ExportConfigMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Configuration File|*.bdat",
                    FileName = $"MasterConfig_{DateTime.Now:yyyyMMdd}.bdat",
                    Title = "Export Master Configuration"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var result = await _configService.ExportMasterConfigAsync(saveDialog.FileName);

                    System.Windows.MessageBox.Show(
                        $"✅ {result}\n\n" +
                        $"📁 File saved to:\n{saveDialog.FileName}\n\n" +
                        $"💡 Copy this file to USB and distribute to all registration laptops.",
                        "Export Successful",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to export configuration:\n\n{ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async void ImportConfigMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openDialog = new OpenFileDialog
                {
                    Filter = "Configuration File|*.bdat",
                    Title = "Import Master Configuration"
                };

                if (openDialog.ShowDialog() == true)
                {
                    var importResult = await _configService.ImportMasterConfigAsync(openDialog.FileName);

                    System.Windows.MessageBox.Show(
                        importResult.GetSummary(),
                        importResult.Success ? "✅ Import Successful" : "❌ Import Failed",
                        System.Windows.MessageBoxButton.OK,
                        importResult.Success ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Error);

                    if (importResult.Success)
                    {
                        // Refresh current view
                        if (MainContentFrame.Content is CollegeManagementView)
                            MainContentFrame.Navigate(new CollegeManagementView());
                        else if (MainContentFrame.Content is TestManagementView)
                            MainContentFrame.Navigate(new TestManagementView());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to import configuration:\n\n{ex.Message}",
                    "Import Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void MergeDatabases_Click(object sender, RoutedEventArgs e)
        {
            MainContentFrame.Navigate(new MergeDatabasesView());
        }

        private void SetContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainContentFrame.Navigate(new RegistrationContextView());
        }

        private void ClearContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to clear the registration context?\n\n" +
                "You will need to set it again before registering students.",
                "Confirm Clear Context",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                RegistrationContext.ClearContext();
                Title = "Biometric Verification System - SuperAdmin";
                System.Windows.MessageBox.Show(
                    "Registration context cleared successfully!",
                    "Context Cleared",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                MainContentFrame.Navigate(new RegistrationContextView());
            }
        }

        private void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "SQLite Database|*.db",
                    FileName = $"BiometricBackup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
                    Title = "Backup Database"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    _databaseService.BackupDatabase(saveDialog.FileName);

                    System.Windows.MessageBox.Show(
                        $"✅ Database backed up successfully!\n\n📁 Location:\n{saveDialog.FileName}",
                        "Backup Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to backup database:\n\n{ex.Message}",
                    "Backup Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void UserGuideMenuItem_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show(
                "📖 User Guide\n\n" +
                "1. Setup: Create colleges and tests (or import configuration)\n" +
                "2. Context: Set registration context (College + Test + Laptop ID)\n" +
                "3. Register: Register students with fingerprints\n" +
                "4. Export: Export master config for other laptops\n" +
                "5. Merge: Combine data from multiple laptops\n" +
                "6. Package: Generate verification packages for colleges\n\n" +
                "For detailed help, refer to the documentation.",
                "User Guide",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show(
                "🔐 Biometric Verification System\n\n" +
                "Version: 1.0.0\n" +
                "Build Date: November 2024\n\n" +
                "SuperAdmin Application for:\n" +
                "• College and test management\n" +
                "• Student registration with fingerprints\n" +
                "• Multi-laptop data synchronization\n" +
                "• Verification package generation\n\n" +
                "© 2024 - All Rights Reserved",
                "About",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to exit the application?",
                "Confirm Exit",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            _databaseService?.Dispose();
            base.OnClosing(e);
        }
    }
}