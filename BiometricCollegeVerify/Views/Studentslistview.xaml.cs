using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BiometricCollegeVerify.Services;
using BiometricCommon.Database;
using BiometricCommon.Models;
using Microsoft.EntityFrameworkCore;

namespace BiometricCollegeVerify.Views
{
    public partial class StudentsListView : Page
    {
        private readonly PackageImportService _importService;
        private ObservableCollection<StudentDisplayModel> _allStudents;
        private ObservableCollection<StudentDisplayModel> _filteredStudents;

        public StudentsListView()
        {
            InitializeComponent();
            _importService = new PackageImportService();
            _allStudents = new ObservableCollection<StudentDisplayModel>();
            _filteredStudents = new ObservableCollection<StudentDisplayModel>();

            StudentsDataGrid.ItemsSource = _filteredStudents;

            Loaded += StudentsListView_Loaded;
        }

        private async void StudentsListView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadStudentsAsync();
        }

        private async Task LoadStudentsAsync()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingText.Text = "Loading students...";

                _allStudents.Clear();
                _filteredStudents.Clear();

                using (var context = new BiometricContext(_importService.GetDatabasePath()))
                {
                    var students = await context.Students
                        .Include(s => s.College)
                        .Include(s => s.Test)
                        .OrderBy(s => s.RollNumber)
                        .ToListAsync();

                    System.Diagnostics.Debug.WriteLine($"Loaded {students.Count} students from database");

                    foreach (var student in students)
                    {
                        var displayModel = new StudentDisplayModel
                        {
                            Id = student.Id,
                            RollNumber = student.RollNumber,
                            Name = student.Name ?? "N/A",
                            CNIC = student.CNIC ?? "N/A",
                            CollegeName = student.College?.Name ?? "N/A",
                            TestName = student.Test?.Name ?? "N/A",
                            IsVerified = student.IsVerified,
                            HasFingerprint = student.FingerprintTemplate != null && student.FingerprintTemplate.Length > 0,
                            TemplateSize = student.FingerprintTemplate?.Length ?? 0
                        };

                        // Convert student photo
                        if (student.StudentPhoto != null && student.StudentPhoto.Length > 0)
                        {
                            try
                            {
                                var bitmap = new BitmapImage();
                                using (var ms = new MemoryStream(student.StudentPhoto))
                                {
                                    bitmap.BeginInit();
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.StreamSource = ms;
                                    bitmap.EndInit();
                                    bitmap.Freeze();
                                }
                                displayModel.StudentPhotoImage = bitmap;
                                displayModel.StudentPhotoVisibility = Visibility.Visible;
                                displayModel.PhotoPlaceholderVisibility = Visibility.Collapsed;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error loading photo for {student.RollNumber}: {ex.Message}");
                            }
                        }

                        // Convert fingerprint image
                        if (student.FingerprintImage != null && student.FingerprintImage.Length > 0 &&
                            student.FingerprintImageWidth > 0 && student.FingerprintImageHeight > 0)
                        {
                            try
                            {
                                displayModel.FingerprintImage = CreateBitmapFromGrayscale(
                                    student.FingerprintImage,
                                    student.FingerprintImageWidth,
                                    student.FingerprintImageHeight);

                                displayModel.FingerprintImageVisibility = Visibility.Visible;
                                displayModel.PlaceholderVisibility = Visibility.Collapsed;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error creating bitmap for student {student.RollNumber}: {ex.Message}");
                                displayModel.FingerprintImageVisibility = Visibility.Collapsed;
                                displayModel.PlaceholderVisibility = Visibility.Visible;
                            }
                        }
                        else
                        {
                            displayModel.FingerprintImageVisibility = Visibility.Collapsed;
                            displayModel.PlaceholderVisibility = Visibility.Visible;
                        }

                        _allStudents.Add(displayModel);
                    }

                    // Initially show all students
                    foreach (var student in _allStudents)
                    {
                        _filteredStudents.Add(student);
                    }

                    UpdateStatistics();
                }

                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                System.Diagnostics.Debug.WriteLine($"Error loading students: {ex.Message}");
                MessageBox.Show(
                    $"Error loading students:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private BitmapSource CreateBitmapFromGrayscale(byte[] imageData, int width, int height)
        {
            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray8, null);
            Int32Rect rect = new Int32Rect(0, 0, width, height);
            bitmap.WritePixels(rect, imageData, width, 0);
            bitmap.Freeze(); // Make it thread-safe and improve performance
            return bitmap;
        }

        private void UpdateStatistics()
        {
            int total = _allStudents.Count;
            int withFingerprint = _allStudents.Count(s => s.HasFingerprint);
            int verified = _allStudents.Count(s => s.IsVerified);
            int pending = total - verified;

            TotalStudentsText.Text = total.ToString();
            WithFingerprintText.Text = withFingerprint.ToString();
            VerifiedStudentsText.Text = verified.ToString();
            PendingStudentsText.Text = pending.ToString();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FilterComboBox.SelectedItem == null) return;
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            string searchText = SearchTextBox?.Text?.ToLower() ?? "";
            var selectedFilter = (FilterComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Students";

            if (_allStudents == null)
            {
                _filteredStudents = new ObservableCollection<StudentDisplayModel>();
                return;
            }

            _filteredStudents.Clear();

            var filtered = _allStudents.AsEnumerable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(s =>
                    s.RollNumber.ToLower().Contains(searchText) ||
                    s.Name.ToLower().Contains(searchText) ||
                    s.CollegeName.ToLower().Contains(searchText));
            }

            // Apply category filter
            switch (selectedFilter)
            {
                case "With Fingerprint":
                    filtered = filtered.Where(s => s.HasFingerprint);
                    break;
                case "Without Fingerprint":
                    filtered = filtered.Where(s => !s.HasFingerprint);
                    break;
                case "Verified":
                    filtered = filtered.Where(s => s.IsVerified);
                    break;
                case "Not Verified":
                    filtered = filtered.Where(s => !s.IsVerified);
                    break;
            }

            foreach (var student in filtered)
            {
                _filteredStudents.Add(student);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadStudentsAsync();
        }
    }

    // Display model for DataGrid binding
    public class StudentDisplayModel
    {
        public int Id { get; set; }
        public string RollNumber { get; set; } = "";
        public string Name { get; set; } = "";
        public string CNIC { get; set; } = "";
        public string CollegeName { get; set; } = "";
        public string TestName { get; set; } = "";
        public bool IsVerified { get; set; }
        public bool HasFingerprint { get; set; }
        public int TemplateSize { get; set; }

        // Student photo (from Excel)
        public BitmapSource? StudentPhotoImage { get; set; }
        public Visibility StudentPhotoVisibility { get; set; } = Visibility.Collapsed;
        public Visibility PhotoPlaceholderVisibility { get; set; } = Visibility.Visible;

        // Fingerprint image
        public BitmapSource? FingerprintImage { get; set; }
        public Visibility FingerprintImageVisibility { get; set; } = Visibility.Collapsed;
        public Visibility PlaceholderVisibility { get; set; } = Visibility.Visible;

        // Status display properties
        public string StatusText => IsVerified ? "✓ Verified" : "Pending";

        public Brush StatusBackground => IsVerified
            ? new SolidColorBrush(Color.FromRgb(232, 245, 233))  // Light green
            : new SolidColorBrush(Color.FromRgb(255, 243, 224)); // Light orange

        public Brush StatusForeground => IsVerified
            ? new SolidColorBrush(Color.FromRgb(76, 175, 80))    // Green
            : new SolidColorBrush(Color.FromRgb(255, 152, 0));   // Orange

        // Template size display
        public string TemplateSizeText => HasFingerprint
            ? $"{TemplateSize} bytes"
            : "No template";
    }
}