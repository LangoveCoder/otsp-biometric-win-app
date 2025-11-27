using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Services;
using BiometricCommon.Models;

namespace BiometricSuperAdmin.Views
{
    public partial class TestManagementView : Page
    {
        private readonly DatabaseService _databaseService;
        private Test? _selectedTest;
        private bool _isEditMode = false;

        public TestManagementView()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            Loaded += TestManagementView_Loaded;
        }

        private async void TestManagementView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCollegesAsync();
            await LoadTestsAsync();
        }

        private async Task LoadCollegesAsync()
        {
            try
            {
                var colleges = await _databaseService.GetActiveCollegesAsync();
                CollegeComboBox.ItemsSource = colleges;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading colleges:\n\n{ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task LoadTestsAsync()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                var tests = await _databaseService.GetAllTestsAsync();
                TestsDataGrid.ItemsSource = tests;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading tests:\n\n{ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void TestsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TestsDataGrid.SelectedItem is Test test)
            {
                _selectedTest = test;
                EditButton.IsEnabled = true;
                DeleteButton.IsEnabled = true;
                ShowTestDetails(test);
            }
            else
            {
                _selectedTest = null;
                EditButton.IsEnabled = false;
                DeleteButton.IsEnabled = false;
            }
        }

        private void ShowTestDetails(Test test)
        {
            FormTitle.Text = "Test Details";
            
            // Set college first
            if (test.College != null)
            {
                CollegeComboBox.SelectedItem = test.College;
            }
            
            TestNameTextBox.Text = test.Name;
            TestCodeTextBox.Text = test.Code;
            DescriptionTextBox.Text = test.Description;
            TestDatePicker.SelectedDate = test.TestDate;
            RegStartDatePicker.SelectedDate = test.RegistrationStartDate;
            RegEndDatePicker.SelectedDate = test.RegistrationEndDate;
            IsActiveCheckBox.IsChecked = test.IsActive;

            // Make read-only
            SetFormReadOnly(true);
            SaveButton.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;
            InfoPanel.Visibility = Visibility.Collapsed;
        }

        private void AddTestButton_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = false;
            _selectedTest = null;
            FormTitle.Text = "Add New Test";
            ClearForm();
            SetFormReadOnly(false);
            SaveButton.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Visible;
            InfoPanel.Visibility = Visibility.Collapsed;

            // Set default dates
            RegStartDatePicker.SelectedDate = DateTime.Now;
            RegEndDatePicker.SelectedDate = DateTime.Now.AddMonths(2);
            TestDatePicker.SelectedDate = DateTime.Now.AddMonths(3);
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTest == null) return;

            _isEditMode = true;
            FormTitle.Text = "Edit Test";
            SetFormReadOnly(false);
            SaveButton.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Visible;
            InfoPanel.Visibility = Visibility.Collapsed;
            
            // College cannot be changed in edit mode
            CollegeComboBox.IsEnabled = false;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm())
                return;

            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                var selectedCollege = CollegeComboBox.SelectedItem as College;
                if (selectedCollege == null)
                {
                    System.Windows.MessageBox.Show("Please select a college.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (_isEditMode && _selectedTest != null)
                {
                    // Update existing test
                    _selectedTest.Name = TestNameTextBox.Text.Trim();
                    _selectedTest.Code = TestCodeTextBox.Text.Trim();
                    _selectedTest.Description = DescriptionTextBox.Text.Trim();
                    _selectedTest.TestDate = TestDatePicker.SelectedDate ?? DateTime.Now;
                    _selectedTest.RegistrationStartDate = RegStartDatePicker.SelectedDate ?? DateTime.Now;
                    _selectedTest.RegistrationEndDate = RegEndDatePicker.SelectedDate ?? DateTime.Now;
                    _selectedTest.IsActive = IsActiveCheckBox.IsChecked ?? true;
                    // Note: CollegeId cannot be changed in edit mode

                    await _databaseService.UpdateTestAsync(_selectedTest);

                    System.Windows.MessageBox.Show("Test updated successfully!", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    // Add new test
                    var newTest = new Test
                    {
                        Name = TestNameTextBox.Text.Trim(),
                        Code = TestCodeTextBox.Text.Trim(),
                        Description = DescriptionTextBox.Text.Trim(),
                        CollegeId = selectedCollege.Id,
                        TestDate = TestDatePicker.SelectedDate ?? DateTime.Now,
                        RegistrationStartDate = RegStartDatePicker.SelectedDate ?? DateTime.Now,
                        RegistrationEndDate = RegEndDatePicker.SelectedDate ?? DateTime.Now,
                        IsActive = IsActiveCheckBox.IsChecked ?? true
                    };

                    await _databaseService.AddTestAsync(newTest);

                    System.Windows.MessageBox.Show($"Test created for {selectedCollege.Name}!", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }

                await LoadTestsAsync();
                CancelButton_Click(sender, e);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving test:\n\n{ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTest == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete the test '{_selectedTest.Name}'?\n\nThis will mark it as inactive.",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    await _databaseService.DeleteTestAsync(_selectedTest.Id);
                    System.Windows.MessageBox.Show("Test deactivated successfully!", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    await LoadTestsAsync();
                    ClearForm();
                    InfoPanel.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error deleting test:\n\n{ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            SetFormReadOnly(true);
            SaveButton.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;
            InfoPanel.Visibility = Visibility.Visible;
            FormTitle.Text = "Test Details";
            _isEditMode = false;
            _selectedTest = null;
            TestsDataGrid.SelectedItem = null;
            CollegeComboBox.IsEnabled = true;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadTestsAsync();
        }

        private void ClearForm()
        {
            CollegeComboBox.SelectedIndex = -1;
            TestNameTextBox.Clear();
            TestCodeTextBox.Clear();
            DescriptionTextBox.Clear();
            TestDatePicker.SelectedDate = null;
            RegStartDatePicker.SelectedDate = null;
            RegEndDatePicker.SelectedDate = null;
            IsActiveCheckBox.IsChecked = true;
        }

        private void SetFormReadOnly(bool isReadOnly)
        {
            CollegeComboBox.IsEnabled = !isReadOnly;
            TestNameTextBox.IsReadOnly = isReadOnly;
            TestCodeTextBox.IsReadOnly = isReadOnly;
            DescriptionTextBox.IsReadOnly = isReadOnly;
            TestDatePicker.IsEnabled = !isReadOnly;
            RegStartDatePicker.IsEnabled = !isReadOnly;
            RegEndDatePicker.IsEnabled = !isReadOnly;
            IsActiveCheckBox.IsEnabled = !isReadOnly;
        }

        private bool ValidateForm()
        {
            if (CollegeComboBox.SelectedItem == null)
            {
                System.Windows.MessageBox.Show("Please select a college.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(TestNameTextBox.Text))
            {
                System.Windows.MessageBox.Show("Please enter test name.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(TestCodeTextBox.Text))
            {
                System.Windows.MessageBox.Show("Please enter test code.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return false;
            }

            if (TestDatePicker.SelectedDate == null)
            {
                System.Windows.MessageBox.Show("Please select test date.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return false;
            }

            if (RegStartDatePicker.SelectedDate == null)
            {
                System.Windows.MessageBox.Show("Please select registration start date.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return false;
            }

            if (RegEndDatePicker.SelectedDate == null)
            {
                System.Windows.MessageBox.Show("Please select registration end date.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return false;
            }

            if (RegEndDatePicker.SelectedDate < RegStartDatePicker.SelectedDate)
            {
                System.Windows.MessageBox.Show("Registration end date must be after start date.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}
