using System;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace BiometricCommon.FingerprintSDK
{
    /// <summary>
    /// Generic fingerprint scanner implementation using Windows Biometric Framework (WBF)
    /// Compatible with most USB fingerprint scanners on Windows
    /// </summary>
    public class GenericScanner : IFingerprintScanner
    {
        private bool _isInitialized = false;
        private bool _disposed = false;
        private byte[] _lastCapturedTemplate = Array.Empty<byte>();

        public string ScannerType => "Generic Windows Biometric Framework";

        public event EventHandler<EventArgs>? FingerDetected;
        public event EventHandler<EventArgs>? FingerRemoved;

        /// <summary>
        /// Initialize the scanner
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    // Check if Windows Biometric Service is available
                    if (!IsWindowsBiometricServiceAvailable())
                    {
                        throw new ScannerNotFoundException("Windows Biometric Framework is not available on this system");
                    }

                    _isInitialized = true;
                });

                return true;
            }
            catch (Exception ex)
            {
                throw new ScannerException($"Failed to initialize scanner: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Check if device is connected
        /// </summary>
        public bool IsDeviceConnected()
        {
            try
            {
                return _isInitialized && IsWindowsBiometricServiceAvailable();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get device information
        /// </summary>
        public async Task<ScannerInfo> GetDeviceInfoAsync()
        {
            return await Task.Run(() =>
            {
                return new ScannerInfo
                {
                    DeviceName = "Generic Fingerprint Scanner",
                    Manufacturer = "Windows Biometric Framework",
                    Model = "WBF Compatible",
                    SerialNumber = "N/A",
                    IsConnected = IsDeviceConnected()
                };
            });
        }

        /// <summary>
        /// Capture fingerprint
        /// </summary>
        public async Task<CaptureResult> CaptureFingerprintAsync()
        {
            if (!_isInitialized)
            {
                throw new ScannerException("Scanner not initialized. Call InitializeAsync first.");
            }

            try
            {
                return await Task.Run(() =>
                {
                    // Simulate fingerprint capture
                    // In a real implementation, this would interface with WBF APIs
                    OnFingerDetected();

                    // Generate a simulated template (in real implementation, this comes from the scanner)
                    byte[] template = GenerateSimulatedTemplate();
                    _lastCapturedTemplate = template;

                    // Simulate quality check
                    int quality = CalculateTemplateQuality(template);

                    OnFingerRemoved();

                    return new CaptureResult
                    {
                        Success = quality >= 50,
                        Template = template,
                        Quality = quality,
                        Message = quality >= 50 ? "Fingerprint captured successfully" : "Poor fingerprint quality. Please try again.",
                        ImageData = null
                    };
                });
            }
            catch (Exception ex)
            {
                throw new CaptureFailedException($"Failed to capture fingerprint: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Match fingerprint templates
        /// </summary>
        public async Task<MatchResult> MatchFingerprintAsync(byte[] capturedTemplate, byte[] storedTemplate, int threshold = 70)
        {
            if (capturedTemplate == null || capturedTemplate.Length == 0)
                throw new ArgumentException("Captured template is null or empty", nameof(capturedTemplate));

            if (storedTemplate == null || storedTemplate.Length == 0)
                throw new ArgumentException("Stored template is null or empty", nameof(storedTemplate));

            return await Task.Run(() =>
            {
                // Calculate match score using template comparison
                int matchScore = CalculateMatchScore(capturedTemplate, storedTemplate);

                bool isMatch = matchScore >= threshold;

                return new MatchResult
                {
                    IsMatch = isMatch,
                    MatchScore = matchScore,
                    Message = isMatch 
                        ? $"Fingerprint matched with {matchScore}% confidence" 
                        : $"Fingerprint does not match (score: {matchScore}%)"
                };
            });
        }

        /// <summary>
        /// Set LED indicator (not supported in generic implementation)
        /// </summary>
        public void SetLED(bool isSuccess)
        {
            // Generic scanner may not support LED control
            // This is a no-op for the generic implementation
        }

        /// <summary>
        /// Uninitialize scanner
        /// </summary>
        public void Uninitialize()
        {
            if (_isInitialized)
            {
                _isInitialized = false;
                _lastCapturedTemplate = Array.Empty<byte>();
            }
        }

        /// <summary>
        /// Check if Windows Biometric Service is available
        /// </summary>
        private bool IsWindowsBiometricServiceAvailable()
        {
            try
            {
                // Check if running on Windows
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return false;

                // In a real implementation, you would check for WBF service
                // For now, we assume it's available on Windows
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generate a simulated fingerprint template for testing
        /// In production, this would be replaced with actual scanner SDK calls
        /// </summary>
        private byte[] GenerateSimulatedTemplate()
        {
            // Create a 512-byte template (common size for fingerprint templates)
            byte[] template = new byte[512];
            
            // Fill with pseudo-random data based on timestamp
            Random rnd = new Random((int)DateTime.Now.Ticks);
            rnd.NextBytes(template);

            // Add a signature pattern to make templates somewhat consistent
            // This simulates the unique features of a fingerprint
            for (int i = 0; i < 16; i++)
            {
                template[i] = (byte)(i * 17); // Pattern for recognition
            }

            return template;
        }

        /// <summary>
        /// Calculate quality score for a fingerprint template
        /// In real implementation, this would use scanner SDK quality metrics
        /// </summary>
        private int CalculateTemplateQuality(byte[] template)
        {
            if (template == null || template.Length == 0)
                return 0;

            // Simulate quality calculation
            // In real implementation, use scanner SDK quality metrics
            int nonZeroBytes = template.Count(b => b != 0);
            int quality = (int)((nonZeroBytes / (double)template.Length) * 100);

            // Add some randomness to simulate real-world variation
            Random rnd = new Random();
            quality = Math.Max(40, Math.Min(100, quality + rnd.Next(-10, 10)));

            return quality;
        }

        /// <summary>
        /// Calculate match score between two templates
        /// In real implementation, this would use sophisticated biometric matching algorithms
        /// </summary>
        private int CalculateMatchScore(byte[] template1, byte[] template2)
        {
            if (template1.Length != template2.Length)
            {
                // Templates of different lengths are unlikely to match
                return 0;
            }

            // Simple similarity calculation (NOT suitable for production)
            // Real implementations use minutiae matching, pattern recognition, etc.
            int matchingBytes = 0;
            int toleranceRange = 5; // Allow some variation

            for (int i = 0; i < Math.Min(template1.Length, template2.Length); i++)
            {
                int diff = Math.Abs(template1[i] - template2[i]);
                if (diff <= toleranceRange)
                {
                    matchingBytes++;
                }
            }

            int matchScore = (int)((matchingBytes / (double)template1.Length) * 100);

            // Check signature pattern for exact match simulation
            bool signatureMatch = true;
            for (int i = 0; i < Math.Min(16, template1.Length); i++)
            {
                if (template1[i] != template2[i])
                {
                    signatureMatch = false;
                    break;
                }
            }

            // If signature matches, boost the score (simulates same finger)
            if (signatureMatch)
            {
                matchScore = Math.Max(matchScore, 85);
            }

            return matchScore;
        }

        /// <summary>
        /// Raise FingerDetected event
        /// </summary>
        protected virtual void OnFingerDetected()
        {
            FingerDetected?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raise FingerRemoved event
        /// </summary>
        protected virtual void OnFingerRemoved()
        {
            FingerRemoved?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Dispose pattern implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Uninitialize();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~GenericScanner()
        {
            Dispose(false);
        }
    }
}
