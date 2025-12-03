using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Database;
using BiometricCommon.Models;
using BiometricCommon.Services;

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
            LoadTests();
            LoadSavedContext();
        }

        private void LoadColleges()
        {
            using var context = new BiometricContext();
            var colleges = context.Colleges.OrderBy(c => c.Code).ToList();
            CollegeComboBox.ItemsSource = colleges;
            CollegeComboBox.DisplayMemberPath = "Name";  // ✅ CORRECT: Uses "Name" property
            CollegeComboBox.SelectedValuePath = "Id";     // ✅ CORRECT: Uses "Id" property
        }

        private void LoadTests()
        {
            using var context = new BiometricContext();
            var tests = context.Tests.OrderBy(t => t.Name).ToList();
            TestComboBox.ItemsSource = tests;
            TestComboBox.DisplayMemberPath = "Name";  // ✅ CORRECT: Uses "Name" property
            TestComboBox.SelectedValuePath = "Id";     // ✅ CORRECT: Uses "Id" property
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
                SetDate = DateTime.Now  // ✅ NOW WORKS - Property exists
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

            // Update main window title
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.Title = $"Biometric Verification System - {context.LaptopId} - {context.CollegeName}";
            }

            // Navigate to dashboard
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