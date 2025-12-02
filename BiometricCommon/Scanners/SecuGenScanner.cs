using System;
using System.Threading.Tasks;
using BiometricCommon.Services;
using SecuGen.FDxSDKPro.Windows;

namespace BiometricCommon.Scanners
{
    public class SecuGenScanner : IFingerprintScanner
    {
        private SGFingerPrintManager? _fpDevice;
        private bool _isInitialized = false;
        private int _imageWidth = 0;
        private int _imageHeight = 0;
        private int _imageDPI = 0;

        public string ScannerName => "SecuGen Hamster Pro 20";
        public bool IsConnected => _isInitialized && _fpDevice != null;

        public async Task<ScannerInitResult> InitializeAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("=== SecuGen Scanner Init (SGFingerPrintManager) ===");

                    // Create device instance
                    _fpDevice = new SGFingerPrintManager();

                    // Initialize with FDU08 (U20-A device)
                    int err = _fpDevice.Init(SGFPMDeviceName.DEV_FDU08);

                    System.Diagnostics.Debug.WriteLine($"Init result: {err}");

                    if (err != (int)SGFPMError.ERROR_NONE)
                    {
                        string errorMsg = GetErrorMessage(err);
                        System.Diagnostics.Debug.WriteLine($"ERROR: Init failed - {errorMsg}");

                        return new ScannerInitResult
                        {
                            Success = false,
                            Message = $"Scanner initialization failed",
                            ErrorDetails = $"Error Code: {err}\n" +
                                         $"Error: {errorMsg}\n\n" +
                                         $"Checklist:\n" +
                                         $"✓ Scanner plugged into USB?\n" +
                                         $"✓ Driver installed?\n" +
                                         $"✓ Close SecuGen Diagnostic Tool\n" +
                                         $"✓ Try unplugging and replugging scanner"
                        };
                    }

                    // Open device (port 0 = first USB device)
                    err = _fpDevice.OpenDevice(0);

                    System.Diagnostics.Debug.WriteLine($"OpenDevice result: {err}");

                    if (err != (int)SGFPMError.ERROR_NONE)
                    {
                        string errorMsg = GetErrorMessage(err);
                        System.Diagnostics.Debug.WriteLine($"ERROR: OpenDevice failed - {errorMsg}");

                        return new ScannerInitResult
                        {
                            Success = false,
                            Message = $"Failed to open scanner",
                            ErrorDetails = $"Error Code: {err}\n" +
                                         $"Error: {errorMsg}\n\n" +
                                         $"Scanner found but couldn't be opened.\n" +
                                         $"Is another app using it?"
                        };
                    }

                    // Get device info (image dimensions)
                    SGFPMDeviceInfoParam deviceInfo = new SGFPMDeviceInfoParam();
                    err = _fpDevice.GetDeviceInfo(deviceInfo);

                    if (err == (int)SGFPMError.ERROR_NONE)
                    {
                        _imageWidth = deviceInfo.ImageWidth;
                        _imageHeight = deviceInfo.ImageHeight;
                        _imageDPI = deviceInfo.ImageDPI;

                        System.Diagnostics.Debug.WriteLine($"Device Info: {_imageWidth}x{_imageHeight} @ {_imageDPI} DPI");
                    }

                    _isInitialized = true;
                    System.Diagnostics.Debug.WriteLine("✓✓✓ Scanner initialized successfully!");

                    return new ScannerInitResult
                    {
                        Success = true,
                        Message = $"✓ SecuGen scanner ready!\n\n" +
                                $"Model: Hamster Pro 20 (U20-A)\n" +
                                $"Image: {_imageWidth}x{_imageHeight}\n" +
                                $"DPI: {_imageDPI}"
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"EXCEPTION: {ex.Message}");

                    return new ScannerInitResult
                    {
                        Success = false,
                        Message = "Unexpected error",
                        ErrorDetails = $"{ex.GetType().Name}: {ex.Message}\n\n" +
                                     $"Make sure SecuGen.FDxSDKPro.Windows.dll and all native DLLs are present."
                    };
                }
            });
        }

        public async Task<FingerprintCaptureResult> CaptureAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_fpDevice == null || !_isInitialized)
                    {
                        return new FingerprintCaptureResult
                        {
                            Success = false,
                            Message = "Scanner not initialized",
                            FailureReason = CaptureFailureReason.DeviceNotConnected
                        };
                    }

                    System.Diagnostics.Debug.WriteLine("Capturing fingerprint...");

                    // Create buffer for raw image
                    byte[] imageBuffer = new byte[_imageWidth * _imageHeight];

                    // Capture fingerprint image
                    int err = _fpDevice.GetImage(imageBuffer);

                    if (err != (int)SGFPMError.ERROR_NONE)
                    {
                        string errorMsg = GetErrorMessage(err);
                        System.Diagnostics.Debug.WriteLine($"Capture failed: {errorMsg}");

                        return new FingerprintCaptureResult
                        {
                            Success = false,
                            Message = $"Capture failed: {errorMsg}",
                            FailureReason = err == 4 ? CaptureFailureReason.Timeout : CaptureFailureReason.PoorQuality
                        };
                    }

                    // Get image quality
                    int quality = 0;
                    err = _fpDevice.GetImageQuality(_imageWidth, _imageHeight, imageBuffer, ref quality);

                    System.Diagnostics.Debug.WriteLine($"✓ Captured! Quality: {quality}");

                    // Convert image to template for matching
                    byte[] template = new byte[400]; // SecuGen template size
                    int templateSize = 400;

                    err = _fpDevice.CreateTemplate(null, imageBuffer, template);

                    if (err != (int)SGFPMError.ERROR_NONE)
                    {
                        System.Diagnostics.Debug.WriteLine($"Template creation failed: {err}");
                        // Still return the image even if template fails
                        Array.Resize(ref template, templateSize);
                    }

                    return new FingerprintCaptureResult
                    {
                        Success = true,
                        Template = template,
                        QualityScore = quality,
                        Message = $"Fingerprint captured (Quality: {quality}%)"
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Capture exception: {ex.Message}");

                    return new FingerprintCaptureResult
                    {
                        Success = false,
                        Message = $"Error: {ex.Message}",
                        FailureReason = CaptureFailureReason.DeviceError
                    };
                }
            });
        }

        public async Task<FingerprintMatchResult> MatchAsync(byte[] template1, byte[] template2)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_fpDevice == null)
                    {
                        return new FingerprintMatchResult
                        {
                            IsMatch = false,
                            ConfidenceScore = 0,
                            Message = "Scanner not initialized"
                        };
                    }

                    // Use SecuGen matching
                    bool matched = false;
                    int err = _fpDevice.MatchTemplate(template1, template2, 0, ref matched);

                    if (err != (int)SGFPMError.ERROR_NONE)
                    {
                        System.Diagnostics.Debug.WriteLine($"Match failed with error: {err}");
                        return new FingerprintMatchResult
                        {
                            IsMatch = false,
                            ConfidenceScore = 0,
                            Message = $"Match error: {GetErrorMessage(err)}"
                        };
                    }

                    int confidence = matched ? 95 : 0; // SecuGen doesn't give scores, just yes/no

                    System.Diagnostics.Debug.WriteLine($"Match result: {matched} (Confidence: {confidence})");

                    return new FingerprintMatchResult
                    {
                        IsMatch = matched,
                        ConfidenceScore = confidence,
                        Message = matched ? "Fingerprint matched!" : "No match",
                        Quality = matched ? MatchQuality.Excellent : MatchQuality.Poor
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Match exception: {ex.Message}");

                    return new FingerprintMatchResult
                    {
                        IsMatch = false,
                        ConfidenceScore = 0,
                        Message = $"Error: {ex.Message}"
                    };
                }
            });
        }

        public int GetQualityScore(byte[] template)
        {
            // SecuGen doesn't provide quality from template alone
            // Quality is checked during capture
            return 75; // Default estimate
        }

        public async Task DisconnectAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (_fpDevice != null)
                    {
                        _fpDevice.CloseDevice();
                        System.Diagnostics.Debug.WriteLine("Scanner disconnected");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Disconnect error: {ex.Message}");
                }
                finally
                {
                    _fpDevice = null;
                    _isInitialized = false;
                }
            });
        }

        public ScannerInfo GetScannerInfo()
        {
            return new ScannerInfo
            {
                Name = "SecuGen Hamster Pro 20",
                Manufacturer = "SecuGen Corporation",
                Model = "HU20-A (FDU08)",
                SerialNumber = "N/A",
                FirmwareVersion = "N/A",
                Type = ScannerType.Optical
            };
        }

        private string GetErrorMessage(int errorCode)
        {
            return errorCode switch
            {
                0 => "ERROR_NONE (Success)",
                1 => "ERROR_CREATION_FAILED",
                2 => "ERROR_FUNCTION_FAILED",
                3 => "ERROR_INVALID_PARAM",
                4 => "ERROR_TIMEOUT (No finger detected)",
                5 => "ERROR_DLLLOAD_FAILED",
                6 => "ERROR_DLLLOAD_FAILED_DRV",
                7 => "ERROR_DLLLOAD_FAILED_ALGO",
                51 => "ERROR_SYSLOAD_FAILED",
                52 => "ERROR_INITIALIZE_FAILED",
                55 => "ERROR_DEVICE_NOT_FOUND",
                56 => "ERROR_DEVICE_ALREADY_OPEN",
                _ => $"UNKNOWN_ERROR ({errorCode})"
            };
        }
    }
}