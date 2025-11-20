using System;
using System.Threading.Tasks;

namespace BiometricCommon.FingerprintSDK
{
    /// <summary>
    /// Result of a fingerprint capture operation
    /// </summary>
    public class CaptureResult
    {
        public bool Success { get; set; }
        public byte[] Template { get; set; } = Array.Empty<byte>();
        public string Message { get; set; } = string.Empty;
        public int Quality { get; set; } = 0; // 0-100
        public byte[]? ImageData { get; set; }
    }

    /// <summary>
    /// Result of a fingerprint match operation
    /// </summary>
    public class MatchResult
    {
        public bool IsMatch { get; set; }
        public int MatchScore { get; set; } = 0; // 0-100
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Scanner device information
    /// </summary>
    public class ScannerInfo
    {
        public string DeviceName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
    }

    /// <summary>
    /// Interface for fingerprint scanner operations
    /// Provides abstraction for multiple scanner SDKs
    /// </summary>
    public interface IFingerprintScanner : IDisposable
    {
        /// <summary>
        /// Gets the scanner type/name
        /// </summary>
        string ScannerType { get; }

        /// <summary>
        /// Initialize the scanner device
        /// </summary>
        /// <returns>True if initialization successful</returns>
        Task<bool> InitializeAsync();

        /// <summary>
        /// Check if scanner device is connected
        /// </summary>
        /// <returns>True if device is connected</returns>
        bool IsDeviceConnected();

        /// <summary>
        /// Get scanner device information
        /// </summary>
        /// <returns>Scanner information</returns>
        Task<ScannerInfo> GetDeviceInfoAsync();

        /// <summary>
        /// Capture fingerprint from the scanner
        /// </summary>
        /// <returns>Capture result with template data</returns>
        Task<CaptureResult> CaptureFingerprintAsync();

        /// <summary>
        /// Match a captured fingerprint against a stored template
        /// </summary>
        /// <param name="capturedTemplate">Newly captured fingerprint template</param>
        /// <param name="storedTemplate">Stored fingerprint template to compare against</param>
        /// <param name="threshold">Match threshold (0-100), default 70</param>
        /// <returns>Match result</returns>
        Task<MatchResult> MatchFingerprintAsync(byte[] capturedTemplate, byte[] storedTemplate, int threshold = 70);

        /// <summary>
        /// Start LED feedback on scanner (if supported)
        /// </summary>
        /// <param name="isSuccess">True for success (green), false for error (red)</param>
        void SetLED(bool isSuccess);

        /// <summary>
        /// Uninitialize and release scanner resources
        /// </summary>
        void Uninitialize();

        /// <summary>
        /// Event raised when finger is placed on scanner
        /// </summary>
        event EventHandler<EventArgs>? FingerDetected;

        /// <summary>
        /// Event raised when finger is removed from scanner
        /// </summary>
        event EventHandler<EventArgs>? FingerRemoved;
    }

    /// <summary>
    /// Scanner initialization exception
    /// </summary>
    public class ScannerException : Exception
    {
        public ScannerException(string message) : base(message) { }
        public ScannerException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Scanner not found exception
    /// </summary>
    public class ScannerNotFoundException : ScannerException
    {
        public ScannerNotFoundException(string message) : base(message) { }
    }

    /// <summary>
    /// Capture failed exception
    /// </summary>
    public class CaptureFailedException : ScannerException
    {
        public CaptureFailedException(string message) : base(message) { }
        public CaptureFailedException(string message, Exception innerException) : base(message, innerException) { }
    }
}
