using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using BiometricCommon.Services;

namespace BiometricSuperAdmin.Views
{
    public partial class MergeDatabasesView : Page
    {
        private readonly DatabaseMergeService _mergeService;
        private List<DatabaseFileInfo> _selectedFiles;
        private MergeResult? _lastMergeResult;

        public MergeDatabasesView()
        {
            InitializeComponent();
            _mergeService = new DatabaseMergeService();
            _selectedFiles = new List<DatabaseFileInfo>();
        }

        private void SelectFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Database Files to Merge",
                Filter = "Database Files (*.db)|*.db|All Files (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var filePath in openFileDialog.FileNames)
                {
                    if (_selectedFiles.Any(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    _selectedFiles.Add(new DatabaseFileInfo
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath)
                    });
                }

                RefreshFilesList();
            }
        }

        private void ClearFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiles.Count == 0)
                return;

            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to clear all selected files?",
                "Clear Files",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _selectedFiles.Clear();
                RefreshFilesList();
            }
        }

        private async void StartMergeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiles.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "Please select at least one database file to merge.",
                    "No Files Selected",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var confirmResult = System.Windows.MessageBox.Show(
                $"You are about to merge {_selectedFiles.Count} database file(s) into the master database.\n\n" +
                "This operation will:\n" +
                "• Read all students from selected databases\n" +
                "• Detect and resolve duplicates\n" +
                "• Import unique students into master database\n\n" +
                "Do you want to continue?",
                "Confirm Merge",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (confirmResult != System.Windows.MessageBoxResult.Yes)
                return;

            await PerformMergeAsync();
        }

        private async Task PerformMergeAsync()
        {
            try
            {
                SelectFilesButton.IsEnabled = false;
                ClearFilesButton.IsEnabled = false;
                StartMergeButton.IsEnabled = false;
                LoadingOverlay.Visibility = Visibility.Visible;
                ProgressSection.Visibility = Visibility.Visible;
                ViewReportButton.Visibility = Visibility.Collapsed;

                MergeProgressBar.Value = 0;
                ProgressPercentageTextBlock.Text = "0%";
                ProgressTextBlock.Text = "Reading database files...";

                var databaseResults = new List<DatabaseReadResult>();

                for (int i = 0; i < _selectedFiles.Count; i++)
                {
                    var file = _selectedFiles[i];
                    ProgressTextBlock.Text = $"Reading {file.FileName}... ({i + 1}/{_selectedFiles.Count})";

                    var readResult = await _mergeService.ReadDatabaseAsync(file.FilePath);
                    databaseResults.Add(readResult);

                    int readProgress = (int)((i + 1) / (double)_selectedFiles.Count * 30);
                    MergeProgressBar.Value = readProgress;
                    ProgressPercentageTextBlock.Text = $"{readProgress}%";
                }

                var failedReads = databaseResults.Where(r => !r.Success).ToList();
                if (failedReads.Any())
                {
                    var failedNames = string.Join("\n", failedReads.Select(f => $"• {f.FileName}: {f.ErrorMessage}"));
                    System.Windows.MessageBox.Show(
                        $"Failed to read the following database files:\n\n{failedNames}\n\n" +
                        "These files will be skipped. Do you want to continue with the remaining files?",
                        "Read Errors",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }

                var progress = new Progress<MergeProgress>(p =>
                {
                    int totalProgress = 30 + (int)(p.Percentage * 0.7);
                    MergeProgressBar.Value = totalProgress;
                    ProgressPercentageTextBlock.Text = $"{totalProgress}%";
                    ProgressTextBlock.Text = p.Message;
                });

                _lastMergeResult = await _mergeService.MergeIntoMasterAsync(databaseResults, progress);

                if (_lastMergeResult.Success)
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    ViewReportButton.Visibility = Visibility.Visible;

                    System.Windows.MessageBox.Show(
                        $"✅ Database merge completed successfully!\n\n" +
                        $"Total Students Read: {_lastMergeResult.TotalStudentsRead}\n" +
                        $"Duplicates Found: {_lastMergeResult.DuplicateCount}\n" +
                        $"Students Imported: {_lastMergeResult.StudentsImported}\n" +
                        $"Students Skipped: {_lastMergeResult.StudentsSkipped}\n\n" +
                        "Click 'View Detailed Report' for more information.",
                        "Merge Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);

                    _selectedFiles.Clear();
                    RefreshFilesList();
                }
                else
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    System.Windows.MessageBox.Show(
                        $"❌ Merge failed!\n\n{_lastMergeResult.ErrorMessage}",
                        "Merge Failed",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                System.Windows.MessageBox.Show(
                    $"An error occurred during merge:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                SelectFilesButton.IsEnabled = true;
                ClearFilesButton.IsEnabled = true;
                StartMergeButton.IsEnabled = _selectedFiles.Count > 0;
            }
        }

        private void ViewReportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastMergeResult == null)
            {
                System.Windows.MessageBox.Show(
                    "No merge report available.",
                    "No Report",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var report = _mergeService.GenerateMergeReport(_lastMergeResult);

            var reportWindow = new Window
            {
                Title = "Merge Report",
                Width = 700,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20)
            };

            var textBlock = new TextBlock
            {
                Text = report,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };

            scrollViewer.Content = textBlock;
            reportWindow.Content = scrollViewer;

            reportWindow.ShowDialog();
        }

        private async void InspectDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiles.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "Please select a database file first.",
                    "No File Selected",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            InspectionPanel.Visibility = Visibility.Visible;
            InspectionResults.Text = "Loading...";

            try
            {
                var report = await _mergeService.InspectDatabaseAsync(_selectedFiles[0].FilePath);
                InspectionResults.Text = report;
            }
            catch (Exception ex)
            {
                InspectionResults.Text = $"Error: {ex.Message}";
            }
        }

        private async void InspectMasterDB_Click(object sender, RoutedEventArgs e)
        {
            InspectionPanel.Visibility = Visibility.Visible;
            InspectionResults.Text = "Loading...";

            try
            {
                var masterPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BiometricVerification", "BiometricData.db");

                if (!File.Exists(masterPath))
                {
                    InspectionResults.Text = "Master database not found at:\n" + masterPath;
                    return;
                }

                var report = await _mergeService.InspectDatabaseAsync(masterPath);
                InspectionResults.Text = report;
            }
            catch (Exception ex)
            {
                InspectionResults.Text = $"Error: {ex.Message}";
            }
        }

        private void RefreshFilesList()
        {
            FilesListBox.ItemsSource = null;
            FilesListBox.ItemsSource = _selectedFiles;

            if (_selectedFiles.Count == 0)
            {
                FileCountTextBlock.Text = "No files selected";
                StartMergeButton.IsEnabled = false;
            }
            else
            {
                FileCountTextBlock.Text = $"{_selectedFiles.Count} file(s) selected";
                StartMergeButton.IsEnabled = true;
            }

            ProgressSection.Visibility = Visibility.Collapsed;
            ViewReportButton.Visibility = Visibility.Collapsed;
            MergeProgressBar.Value = 0;
        }
    }

    public class DatabaseFileInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

}