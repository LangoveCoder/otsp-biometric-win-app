using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BiometricCommon.Database;
using BiometricCommon.Models;
using BiometricCommon.Services;
using Microsoft.EntityFrameworkCore;

namespace BiometricSuperAdmin.Views
{
    public partial class StudentsListView : Page
    {
        private List<StudentDisplayModel> _allStudents = new List<StudentDisplayModel>();

        public StudentsListView()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadStudentsAsync();
        }

        private async System.Threading.Tasks.Task LoadStudentsAsync()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                var context = RegistrationContext.GetCurrentContext();
                if (context == null)
                {
                    MessageBox.Show("Context not set!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                CollegeText.Text = context.CollegeName;
                TestText.Text = context.TestName;

                using (var db = new BiometricContext())
                {
                    var students = await db.Students
    .Where(s => s.CollegeId == context.CollegeId && s.TestId == context.TestId)  // ← Ensure both filters
    .OrderBy(s => s.RollNumber)
    .ToListAsync();

                    if (students.Count == 0)
                    {
                        NoDataPanel.Visibility = Visibility.Visible;
                        StudentsDataGrid.Visibility = Visibility.Collapsed;
                        TotalCountText.Text = "0";
                        return;
                    }

                    _allStudents = students.Select(student => new StudentDisplayModel
                    {
                        RollNumber = student.RollNumber,
                        Name = student.Name ?? "-",
                        CNIC = student.CNIC ?? "-",
                        StudentPhotoImage = LoadImage(student.StudentPhoto),
                        StudentPhotoVisibility = (student.StudentPhoto != null && student.StudentPhoto.Length > 0) ? Visibility.Visible : Visibility.Collapsed,
                        PhotoPlaceholderVisibility = (student.StudentPhoto == null || student.StudentPhoto.Length == 0) ? Visibility.Visible : Visibility.Collapsed,
                        FingerprintStatus = (student.FingerprintTemplate != null && student.FingerprintTemplate.Length > 0) ? "Registered ✓" : "Pending",
                        StatusBackground = (student.FingerprintTemplate != null && student.FingerprintTemplate.Length > 0) ? new SolidColorBrush(Color.FromRgb(232, 245, 233)) : new SolidColorBrush(Color.FromRgb(255, 243, 224)),
                        StatusForeground = (student.FingerprintTemplate != null && student.FingerprintTemplate.Length > 0) ? new SolidColorBrush(Color.FromRgb(46, 125, 50)) : new SolidColorBrush(Color.FromRgb(245, 124, 0)),
                        FingerprintImage = LoadFingerprintImage(student.FingerprintImage, student.FingerprintImageWidth, student.FingerprintImageHeight),
                        FingerprintVisibility = (student.FingerprintImage != null && student.FingerprintImage.Length > 0) ? Visibility.Visible : Visibility.Collapsed,
                        FingerprintPlaceholderVisibility = (student.FingerprintImage == null || student.FingerprintImage.Length == 0) ? Visibility.Visible : Visibility.Collapsed,
                        TemplateSize = (student.FingerprintTemplate?.Length ?? 0) > 0 ? $"{student.FingerprintTemplate.Length} bytes" : "0 bytes",
                        RegistrationDate = student.RegistrationDate.ToString("dd-MMM-yyyy HH:mm")
                    }).ToList();

                    StudentsDataGrid.ItemsSource = _allStudents;
                    StudentsDataGrid.Visibility = Visibility.Visible;
                    NoDataPanel.Visibility = Visibility.Collapsed;
                    TotalCountText.Text = _allStudents.Count.ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading students:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private BitmapImage? LoadImage(byte[]? imageData)
        {
            if (imageData == null || imageData.Length == 0)
                return null;

            try
            {
                var bitmap = new BitmapImage();
                using (var ms = new MemoryStream(imageData))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private BitmapImage? LoadFingerprintImage(byte[]? imageData, int width, int height)
        {
            if (imageData == null || imageData.Length == 0 || width == 0 || height == 0)
                return null;

            try
            {
                var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray8, null);
                Int32Rect rect = new Int32Rect(0, 0, width, height);
                bitmap.WritePixels(rect, imageData, width, 0);
                bitmap.Freeze();

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using (var ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    ms.Position = 0;

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = ms;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    return bitmapImage;
                }
            }
            catch
            {
                return null;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                StudentsDataGrid.ItemsSource = _allStudents;
                TotalCountText.Text = _allStudents.Count.ToString();
            }
            else
            {
                var filtered = _allStudents.Where(s =>
                    s.RollNumber.ToLower().Contains(searchText) ||
                    s.Name.ToLower().Contains(searchText) ||
                    s.CNIC.ToLower().Contains(searchText)
                ).ToList();

                StudentsDataGrid.ItemsSource = filtered;
                TotalCountText.Text = filtered.Count.ToString();
            }
        }

        public class StudentDisplayModel
        {
            public string RollNumber { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string CNIC { get; set; } = string.Empty;
            public BitmapImage? StudentPhotoImage { get; set; }
            public Visibility StudentPhotoVisibility { get; set; }
            public Visibility PhotoPlaceholderVisibility { get; set; }
            public string FingerprintStatus { get; set; } = string.Empty;
            public SolidColorBrush StatusBackground { get; set; } = new SolidColorBrush(Colors.Gray);
            public SolidColorBrush StatusForeground { get; set; } = new SolidColorBrush(Colors.White);
            public BitmapImage? FingerprintImage { get; set; }
            public Visibility FingerprintVisibility { get; set; }
            public Visibility FingerprintPlaceholderVisibility { get; set; }
            public string TemplateSize { get; set; } = "0 bytes";
            public string RegistrationDate { get; set; } = string.Empty;
        }
    }
}