using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Services;
using BiometricCommon.Models;

namespace BiometricSuperAdmin.Views
{
    public partial class CollegeManagementView : Page
    {
        private readonly DatabaseService _databaseService;

        public CollegeManagementView()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            Loaded += CollegeManagementView_Loaded;
        }

        private async void CollegeManagementView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCollegesAsync();
        }

        private async Task LoadCollegesAsync()
        {
            try
            {
                var colleges = await _databaseService.GetAllCollegesAsync();
                CollegesDataGrid.ItemsSource = colleges;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading colleges:\n\n{ex.Message}", 
                    "Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }

        private async void AddCollegeButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddEditCollegeDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && dialog.College != null)
            {
                try
                {
                    await _databaseService.AddCollegeAsync(dialog.College);
                    
                    MessageBox.Show(
                        $"College '{dialog.College.Name}' added successfully!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    await LoadCollegesAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error adding college:\n\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadCollegesAsync();
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (CollegesDataGrid.SelectedItem is College college)
            {
                var dialog = new AddEditCollegeDialog(college)
                {
                    Owner = Window.GetWindow(this)
                };

                if (dialog.ShowDialog() == true && dialog.College != null)
                {
                    try
                    {
                        await _databaseService.UpdateCollegeAsync(dialog.College);
                        
                        MessageBox.Show(
                            $"College '{dialog.College.Name}' updated successfully!",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        await LoadCollegesAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Error updating college:\n\n{ex.Message}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show(
                    "Please select a college to edit.",
                    "No Selection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (CollegesDataGrid.SelectedItem is College college)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete '{college.Name}'?\n\nThis will mark the college as inactive.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _databaseService.DeleteCollegeAsync(college.Id);
                        
                        MessageBox.Show(
                            $"College '{college.Name}' has been deactivated.",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        await LoadCollegesAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Error deleting college:\n\n{ex.Message}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show(
                    "Please select a college to delete.",
                    "No Selection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}
