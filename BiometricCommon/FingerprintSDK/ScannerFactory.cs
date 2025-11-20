using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BiometricCommon.FingerprintSDK
{
    /// <summary>
    /// Available scanner types
    /// </summary>
    public enum ScannerType
    {
        Generic,
        SecuGen,
        Mantra,
        DigitalPersona,
        AutoDetect
    }

    /// <summary>
    /// Factory class for creating fingerprint scanner instances
    /// </summary>
    public static class ScannerFactory
    {
        /// <summary>
        /// Create a scanner instance of specified type
        /// </summary>
        /// <param name="scannerType">Type of scanner to create</param>
        /// <returns>Scanner instance</returns>
        public static IFingerprintScanner CreateScanner(ScannerType scannerType = ScannerType.Generic)
        {
            return scannerType switch
            {
                ScannerType.Generic => new GenericScanner(),
                ScannerType.SecuGen => CreateSecuGenScanner(),
                ScannerType.Mantra => CreateMantraScanner(),
                ScannerType.DigitalPersona => CreateDigitalPersonaScanner(),
                ScannerType.AutoDetect => AutoDetectScanner(),
                _ => new GenericScanner()
            };
        }

        /// <summary>
        /// Auto-detect available scanner and create appropriate instance
        /// </summary>
        /// <returns>Scanner instance</returns>
        public static IFingerprintScanner AutoDetectScanner()
        {
            // Try to detect available scanners in order of preference
            var scannerTypes = new[]
            {
                ScannerType.SecuGen,
                ScannerType.Mantra,
                ScannerType.DigitalPersona,
                ScannerType.Generic
            };

            foreach (var type in scannerTypes)
            {
                try
                {
                    var scanner = CreateScanner(type);
                    if (scanner != null && scanner.IsDeviceConnected())
                    {
                        return scanner;
                    }
                }
                catch
                {
                    // Continue to next scanner type
                    continue;
                }
            }

            // Fallback to generic scanner
            return new GenericScanner();
        }

        /// <summary>
        /// Get list of available scanner types
        /// </summary>
        /// <returns>List of available scanner types with their status</returns>
        public static async Task<List<ScannerAvailability>> GetAvailableScannersAsync()
        {
            var availableScanner = new List<ScannerAvailability>();

            var scannerTypes = new[]
            {
                ScannerType.Generic,
                ScannerType.SecuGen,
                ScannerType.Mantra,
                ScannerType.DigitalPersona
            };

            foreach (var type in scannerTypes)
            {
                try
                {
                    using var scanner = CreateScanner(type);
                    await scanner.InitializeAsync();
                    var isConnected = scanner.IsDeviceConnected();
                    
                    availableScanner.Add(new ScannerAvailability
                    {
                        Type = type,
                        Name = scanner.ScannerType,
                        IsAvailable = isConnected,
                        Status = isConnected ? "Connected" : "Not Connected"
                    });
                }
                catch (Exception ex)
                {
                    availableScanner.Add(new ScannerAvailability
                    {
                        Type = type,
                        Name = type.ToString(),
                        IsAvailable = false,
                        Status = $"Error: {ex.Message}"
                    });
                }
            }

            return availableScanner;
        }

        /// <summary>
        /// Create SecuGen scanner instance
        /// </summary>
        private static IFingerprintScanner CreateSecuGenScanner()
        {
            // TODO: Implement SecuGen scanner when SDK is available
            // For now, return generic scanner
            // return new SecuGenScanner();
            throw new NotImplementedException("SecuGen scanner not implemented yet. Use Generic scanner or add SecuGen SDK.");
        }

        /// <summary>
        /// Create Mantra scanner instance
        /// </summary>
        private static IFingerprintScanner CreateMantraScanner()
        {
            // TODO: Implement Mantra scanner when SDK is available
            // For now, return generic scanner
            // return new MantraScanner();
            throw new NotImplementedException("Mantra scanner not implemented yet. Use Generic scanner or add Mantra SDK.");
        }

        /// <summary>
        /// Create Digital Persona scanner instance
        /// </summary>
        private static IFingerprintScanner CreateDigitalPersonaScanner()
        {
            // TODO: Implement Digital Persona scanner when SDK is available
            // For now, return generic scanner
            // return new DigitalPersonaScanner();
            throw new NotImplementedException("Digital Persona scanner not implemented yet. Use Generic scanner or add DP SDK.");
        }
    }

    /// <summary>
    /// Scanner availability information
    /// </summary>
    public class ScannerAvailability
    {
        public ScannerType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
