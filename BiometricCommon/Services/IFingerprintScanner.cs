using System;
using System.Threading.Tasks;

namespace BiometricCommon.Services
{
    /// <summary>
    /// Interface that all fingerprint scanner implementations must follow
    /// This allows the system to work with any fingerprint scanner brand
    /// </summary>
    public interface IFingerprintScanner
    {
        /// <summary>
        /// Scanner name/model
        /// </summary>
        string ScannerName { get; }

        /// <summary>
        /// Initialize the fingerprint scanner device
        /// </summary>
        /// <returns>True if successful, false if failed</returns>
        Task<ScannerInitResult> InitializeAsync();

        /// <summary>
        /// Check if scanner is connected and ready
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Capture a fingerprint and return the template
        /// </summary>
        /// <returns>Fingerprint capture result with template data</returns>
        Task<FingerprintCaptureResult> CaptureAsync();

        /// <summary>
        /// Match two fingerprint templates
        /// </summary>
        /// <param name="template1">First template (from database)</param>
        /// <param name="template2">Second template (newly captured)</param>
        /// <returns>Match result with confidence score</returns>
        Task<FingerprintMatchResult> MatchAsync(byte[] template1, byte[] template2);

        /// <summary>
        /// Get the quality score of a fingerprint template
        /// </summary>
        /// <param name="template">Fingerprint template</param>
        /// <returns>Quality score (0-100)</returns>
        int GetQualityScore(byte[] template);

        /// <summary>
        /// Clean up resources and disconnect scanner
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Get scanner information
        /// </summary>
        ScannerInfo GetScannerInfo();
    }

    #region Result Classes

    /// <summary>
    /// Result of scanner initialization
    /// </summary>
    public class ScannerInitResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorDetails { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of fingerprint capture
    /// </summary>
    public class FingerprintCaptureResult
    {
        public bool Success { get; set; }
        public byte[] Template { get; set; } = Array.Empty<byte>();
        public int QualityScore { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorDetails { get; set; } = string.Empty;
        public CaptureFailureReason FailureReason { get; set; }
    }

    /// <summary>
    /// Result of fingerprint matching
    /// </summary>
    public class FingerprintMatchResult
    {
        public bool IsMatch { get; set; }
        public int ConfidenceScore { get; set; } // 0-100
        public string Message { get; set; } = string.Empty;
        public MatchQuality Quality { get; set; }
    }

    /// <summary>
    /// Scanner information
    /// </summary>
    public class ScannerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string FirmwareVersion { get; set; } = string.Empty;
        public ScannerType Type { get; set; }
    }

    /// <summary>
    /// Reasons why capture might fail
    /// </summary>
    public enum CaptureFailureReason
    {
        None,
        DeviceNotConnected,
        PoorQuality,
        Timeout,
        UserCancelled,
        DeviceError,
        Unknown
    }

    /// <summary>
    /// Match quality levels
    /// </summary>
    public enum MatchQuality
    {
        Poor,
        Fair,
        Good,
        Excellent
    }

    /// <summary>
    /// Scanner types
    /// </summary>
    public enum ScannerType
    {
        Optical,
        Capacitive,
        Ultrasonic,
        Thermal,
        Unknown
    }

    #endregion
}