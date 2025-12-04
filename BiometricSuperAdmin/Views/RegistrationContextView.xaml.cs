using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Database;
using BiometricCommon.Models;
using BiometricCommon.Services;
using Microsoft.EntityFrameworkCore;

namespace BiometricSuperAdmin.Views
{
    public partial class RegistrationContextView : Page
    {
        public RegistrationContextView()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadColleges();
            LoadSavedContext();

            // Wire up college selection event
            CollegeComboBox.SelectionChanged += CollegeComboBox_SelectionChanged;
        }

        private void LoadColleges()
        {
            using var context = new BiometricContext();
            var colleges = context.Colleges
                .Where(c => c.IsActive)
                .OrderBy(c => c.Code)
                .ToList();
            CollegeComboBox.ItemsSource = colleges;
            CollegeComboBox.DisplayMemberPath = "Name";
            CollegeComboBox.SelectedValuePath = "Id";
        }

        private async void CollegeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CollegeComboBox.SelectedValue == null) return;

            int collegeId = (int)CollegeComboBox.SelectedValue;

            using var context = new BiometricContext();
            var tests = await context.Tests
                .Where(t => t.CollegeId == collegeId && t.IsActive)
                .OrderBy(t => t.Name)
                .ToListAsync();

            TestComboBox.ItemsSource = tests;
            TestComboBox.DisplayMemberPath = "Name";
            TestComboBox.SelectedValuePath = "Id";
        }

        private void LoadSavedContext()
        {
            var savedContext = RegistrationContext.GetCurrentContext();
            if (savedContext != null)
            {
                CollegeComboBox.SelectedValue = savedContext.CollegeId;
                TestComboBox.SelectedValue = savedContext.TestId;
                LaptopIdTextBox.Text = savedContext.LaptopId;
            }
            else
            {
                LaptopIdTextBox.Text = Environment.MachineName;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (CollegeComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a college!", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (TestComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a test!", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(LaptopIdTextBox.Text))
            {
                MessageBox.Show("Please enter a laptop ID!", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedCollege = (College)CollegeComboBox.SelectedItem;
            var selectedTest = (Test)TestComboBox.SelectedItem;

            var context = new RegistrationContext
            {
                CollegeId = selectedCollege.Id,
                CollegeName = selectedCollege.Name,
                TestId = selectedTest.Id,
                TestName = selectedTest.Name,
                LaptopId = LaptopIdTextBox.Text.Trim(),
                SetDate = DateTime.Now
            };

            RegistrationContext.SaveContext(context);

            MessageBox.Show(
                $"✅ Context saved successfully!\n\n" +
                $"College: {context.CollegeName}\n" +
                $"Test: {context.TestName}\n" +
                $"Device: {context.LaptopId}",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.Title = $"Biometric Verification System - {context.LaptopId} - {context.CollegeName}";
                mainWindow.RefreshNavigationState();
            }

            NavigationService?.Navigate(new DashboardView());
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear the current registration context?",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                RegistrationContext.ClearContext();
                CollegeComboBox.SelectedIndex = -1;
                TestComboBox.SelectedIndex = -1;
                LaptopIdTextBox.Text = Environment.MachineName;
                MessageBox.Show("Context cleared!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}