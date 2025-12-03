using System;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Database;
using BiometricCommon.Models;
using BiometricCommon.Scanners;
using BiometricCommon.Services;
using BiometricSuperAdmin.Views;
using Microsoft.Win32;

namespace BiometricSuperAdmin
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private readonly MasterConfigService _configService;
        public readonly IFingerprintScanner _scannerService;
        public readonly BiometricContext _context;

        public MainWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _configService = new MasterConfigService();
            _context = new BiometricContext();
            _scannerService = new SecuGenScanner();

            Loaded += MainWindow_Loaded;
            InitializeScannerAsync();
        }

        private async void InitializeScannerAsync()
        {
            var result = await _scannerService.InitializeAsync();
            if (!result.Success)
            {
                MessageBox.Show($"Scanner init failed:\n{result.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_configService.IsDatabaseEmpty())
            {
                if (MasterConfigService.AutoImportFileExists())
                {
                    var result = MessageBox.Show(
                        "✨ Master configuration file detected!\n\nDo you want to import colleges and tests automatically?",
                        "Auto-Import Configuration",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            var importResult = await _configService.ImportMasterConfigAsync(
                                MasterConfigService.GetAutoImportFilePath());

                            MessageBox.Show(
                                importResult.GetSummary(),
                                importResult.Success ? "✅ Import Successful" : "❌ Import Failed",
                                MessageBoxButton.OK,
                                importResult.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to auto-import:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }

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

            // Check context for Registration page
            if (NavigationListBox.SelectedIndex == 1)
            {
                var context = RegistrationContext.GetCurrentContext();
                if (context == null)
                {
                    MessageBox.Show("Please set registration context first!", "Context Not Set",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    NavigationListBox.SelectedIndex = 0;
                    return;
                }

                // Also check if college exists
                using (var db = new BiometricContext())
                {
                    if (!db.Colleges.Any())
                    {
                        MessageBox.Show("No colleges found! Please create a college first.",
                            "No Colleges", MessageBoxButton.OK, MessageBoxImage.Warning);
                        NavigationListBox.SelectedIndex = 2; // Go to College Management
                        return;
                    }
                }
            }

            switch (NavigationListBox.SelectedIndex)
            {
                case 0: MainContentFrame.Navigate(new DashboardView()); break;
                case 1: MainContentFrame.Navigate(new RegistrationView(_scannerService, _context)); break;
                case 2: MainContentFrame.Navigate(new CollegeManagementView()); break;
                case 3: MainContentFrame.Navigate(new TestManagementView()); break;
                case 4: MainContentFrame.Navigate(new PackageGeneratorView()); break;
                case 5: MainContentFrame.Navigate(new ReportsView()); break;
            }
        }
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
                    MessageBox.Show($"✅ {result}\n\n📁 {saveDialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show(importResult.GetSummary(), importResult.Success ? "Success" : "Failed", MessageBoxButton.OK,
                        importResult.Success ? MessageBoxImage.Information : MessageBoxImage.Error);

                    if (importResult.Success)
                    {
                        if (MainContentFrame.Content is CollegeManagementView)
                            MainContentFrame.Navigate(new CollegeManagementView());
                        else if (MainContentFrame.Content is TestManagementView)
                            MainContentFrame.Navigate(new TestManagementView());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var result = MessageBox.Show("Clear registration context?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                RegistrationContext.ClearContext();
                Title = "Biometric Verification System - SuperAdmin";
                MessageBox.Show("Context cleared!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show($"✅ Backup complete!\n{saveDialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Backup failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UserGuideMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("📖 Quick Guide:\n1. Create colleges/tests\n2. Set context\n3. Register students\n4. Export packages",
                "User Guide", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("🔐 Biometric System v1.0.0\n© 2024", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            var result = MessageBox.Show("Exit application?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            _databaseService?.Dispose();
            base.OnClosing(e);
        }
    }
}