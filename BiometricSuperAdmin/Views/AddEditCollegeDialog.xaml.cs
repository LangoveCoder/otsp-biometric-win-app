using System;
using System.Windows;
using BiometricCommon.Models;

namespace BiometricSuperAdmin.Views
{
    /// <summary>
    /// Dialog for adding or editing college information
    /// </summary>
    public partial class AddEditCollegeDialog : Window
    {
        public College? College { get; private set; }
        private readonly bool _isEditMode;

        /// <summary>
        /// Constructor for adding new college
        /// </summary>
        public AddEditCollegeDialog()
        {
            InitializeComponent();
            _isEditMode = false;
            DialogTitle.Text = "Add New College";
            College = null;
        }

        /// <summary>
        /// Constructor for editing existing college
        /// </summary>
        public AddEditCollegeDialog(College college)
        {
            InitializeComponent();
            _isEditMode = true;
            DialogTitle.Text = "Edit College";
            College = college;
            LoadCollegeData(college);
        }

        /// <summary>
        /// Load college data into form fields
        /// </summary>
        private void LoadCollegeData(College college)
        {
            CollegeNameTextBox.Text = college.Name;
            CollegeCodeTextBox.Text = college.Code;
            AddressTextBox.Text = college.Address;
            ContactPersonTextBox.Text = college.ContactPerson;
            ContactPhoneTextBox.Text = college.ContactPhone;
            ContactEmailTextBox.Text = college.ContactEmail;
            IsActiveCheckBox.IsChecked = college.IsActive;

            // Make code read-only in edit mode
            if (_isEditMode)
            {
                CollegeCodeTextBox.IsReadOnly = true;
                CollegeCodeTextBox.Background = System.Windows.Media.Brushes.LightGray;
            }
        }

        /// <summary>
        /// Save button click handler
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm())
                return;

            if (_isEditMode && College != null)
            {
                // Update existing college
                College.Name = CollegeNameTextBox.Text.Trim();
                College.Address = AddressTextBox.Text.Trim();
                College.ContactPerson = ContactPersonTextBox.Text.Trim();
                College.ContactPhone = ContactPhoneTextBox.Text.Trim();
                College.ContactEmail = ContactEmailTextBox.Text.Trim();
                College.IsActive = IsActiveCheckBox.IsChecked ?? true;
                College.LastModifiedDate = DateTime.Now;
            }
            else
            {
                // Create new college
                College = new College
                {
                    Name = CollegeNameTextBox.Text.Trim(),
                    Code = CollegeCodeTextBox.Text.Trim().ToUpper(),
                    Address = AddressTextBox.Text.Trim(),
                    ContactPerson = ContactPersonTextBox.Text.Trim(),
                    ContactPhone = ContactPhoneTextBox.Text.Trim(),
                    ContactEmail = ContactEmailTextBox.Text.Trim(),
                    IsActive = IsActiveCheckBox.IsChecked ?? true,
                    CreatedDate = DateTime.Now
                };
            }

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Cancel button click handler
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Validate form inputs
        /// </summary>
        private bool ValidateForm()
        {
            // College Name validation
            if (string.IsNullOrWhiteSpace(CollegeNameTextBox.Text))
            {
                System.Windows.MessageBox.Show(
                    "Please enter college name.",
                    "Validation Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                CollegeNameTextBox.Focus();
                return false;
            }

            if (CollegeNameTextBox.Text.Trim().Length < 3)
            {
                System.Windows.MessageBox.Show(
                    "College name must be at least 3 characters long.",
                    "Validation Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                CollegeNameTextBox.Focus();
                return false;
            }

            // College Code validation
            if (string.IsNullOrWhiteSpace(CollegeCodeTextBox.Text))
            {
                System.Windows.MessageBox.Show(
                    "Please enter college code.",
                    "Validation Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                CollegeCodeTextBox.Focus();
                return false;
            }

            if (CollegeCodeTextBox.Text.Trim().Length < 3)
            {
                System.Windows.MessageBox.Show(
                    "College code must be at least 3 characters long.",
                    "Validation Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                CollegeCodeTextBox.Focus();
                return false;
            }

            // Email validation (if provided)
            if (!string.IsNullOrWhiteSpace(ContactEmailTextBox.Text))
            {
                string email = ContactEmailTextBox.Text.Trim();
                if (!IsValidEmail(email))
                {
                    System.Windows.MessageBox.Show(
                        "Please enter a valid email address.",
                        "Validation Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    ContactEmailTextBox.Focus();
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Simple email validation
        /// </summary>
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
