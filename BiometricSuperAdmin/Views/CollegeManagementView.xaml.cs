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
                System.Windows.MessageBox.Show(
                    $"Error loading colleges:\n\n{ex.Message}", 
                    "Error", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
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
                    
                    System.Windows.MessageBox.Show(
                        $"College '{dialog.College.Name}' added successfully!",
                        "Success",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);

                    await LoadCollegesAsync();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Error adding college:\n\n{ex.Message}",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
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
                        
                        System.Windows.MessageBox.Show(
                            $"College '{dialog.College.Name}' updated successfully!",
                            "Success",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);

                        await LoadCollegesAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"Error updating college:\n\n{ex.Message}",
                            "Error",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Please select a college to edit.",
                    "No Selection",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (CollegesDataGrid.SelectedItem is College college)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete '{college.Name}'?\n\nThis will mark the college as inactive.",
                    "Confirm Delete",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        await _databaseService.DeleteCollegeAsync(college.Id);
                        
                        System.Windows.MessageBox.Show(
                            $"College '{college.Name}' has been deactivated.",
                            "Success",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);

                        await LoadCollegesAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"Error deleting college:\n\n{ex.Message}",
                            "Error",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Please select a college to delete.",
                    "No Selection",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }
    }
}
