using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BiometricCommon.Models;
using BiometricCommon.Services;
using BiometricCommon.Fingerprint;

namespace BiometricSuperAdmin.Views
{
    public partial class RegistrationView : Page
    {
        private readonly DatabaseService _databaseService;
        private RegContext? _currentContext;
        private IFingerprintScanner? _scanner;

        public RegistrationView()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            Loaded += RegistrationView_Loaded;
        }

        private void RegistrationView_Loaded(object sender, RoutedEventArgs e)
        {
            _currentContext = RegContext.GetCurrentContext();

            if (_currentContext == null)
            {
                System.Windows.MessageBox.Show(
                    "Registration context not set. Please set the context first.",
                    "Context Required",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            CollegeTextBox.Text = _currentContext.CollegeName;
            TestTextBox.Text = _currentContext.TestName;
            DeviceTextBox.Text = _currentContext.LaptopId;
        }
        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentContext == null)
                {
                    System.Windows.MessageBox.Show(
                        "Registration context not set. Please set the context first.",
                        "Context Required",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                string rollNumber = RollNumberTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(rollNumber))
                {
                    System.Windows.MessageBox.Show(
                        "Please enter a roll number.",
                        "Validation Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    RollNumberTextBox.Focus();
                    return;
                }

                var existingStudent = _databaseService.GetStudentByRollNumber(rollNumber);

                if (existingStudent != null)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Student with roll number '{rollNumber}' is already registered.\n\n" +
                        "Do you want to re-register (update fingerprint)?",
                        "Student Exists",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result == System.Windows.MessageBoxResult.No)
                        return;
                }

                LoadingOverlay.Visibility = Visibility.Visible;
                RegisterButton.IsEnabled = false;

                if (_scanner == null)
                {
                    _scanner = ScannerFactory.GetAvailableScanner();
                    if (_scanner == null)
                    {
                        System.Windows.MessageBox.Show(
                            "No fingerprint scanner detected!\n\n" +
                            "Please ensure:\n" +
                            "1. Scanner is connected via USB\n" +
                            "2. Drivers are installed\n" +
                            "3. Device appears in Device Manager",
                            "Scanner Not Found",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                        return;
                    }

                    bool initialized = await _scanner.InitializeAsync();
                    if (!initialized)
                    {
                        System.Windows.MessageBox.Show(
                            "Failed to initialize fingerprint scanner!",
                            "Initialization Error",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                        _scanner = null;
                        return;
                    }
                }

                System.Windows.MessageBox.Show(
                    "Place your finger on the scanner now...",
                    "Ready to Capture",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                var captureResult = await _scanner.CaptureAsync();

                if (!captureResult.Success)
                {
                    System.Windows.MessageBox.Show(
                        $"Fingerprint capture failed!\n\n{captureResult.ErrorMessage}",
                        "Capture Failed",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                await _databaseService.RegisterStudentAsync(
                    rollNumber,
                    _currentContext.CollegeId,
                    _currentContext.TestId,
                    captureResult.Template!);

                System.Windows.MessageBox.Show(
                    $"Student '{rollNumber}' registered successfully!\n\n" +
                    $"Fingerprint Quality: {captureResult.Quality}",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                RollNumberTextBox.Clear();
                RollNumberTextBox.Focus();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error during registration: {ex.Message}",
                    "Registration Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                RegisterButton.IsEnabled = true;
            }
        }
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            RollNumberTextBox.Clear();
            RollNumberTextBox.Focus();
        }

        public void Dispose()
        {
            _scanner?.Dispose();
        }

        // P/Invoke declarations for direct DLL testing
        [System.Runtime.InteropServices.DllImport("sgfplib.dll", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall)]
        private static extern int SGFPM_Create(ref IntPtr phDevice);

        [System.Runtime.InteropServices.DllImport("sgfplib.dll", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall)]
        private static extern int SGFPM_Terminate(IntPtr hDevice);
    }

    public class RegContext
    {
        public int CollegeId { get; set; }
        public string CollegeName { get; set; } = string.Empty;
        public int TestId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string LaptopId { get; set; } = string.Empty;
        public DateTime SetDate { get; set; }

        private static readonly string ContextFilePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BiometricVerification",
                "registration_context.json");

        public static RegContext? GetCurrentContext()
        {
            try
            {
                if (File.Exists(ContextFilePath))
                {
                    var json = File.ReadAllText(ContextFilePath);
                    return JsonSerializer.Deserialize<RegContext>(json);
                }
            }
            catch { }
            return null;
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(ContextFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ContextFilePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save context: {ex.Message}");
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(ContextFilePath))
                {
                    File.Delete(ContextFilePath);
                }
            }
            catch { }
        }
    }
}