using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BiometricCommon.Services;

namespace BiometricCommon.Scanners
{
    public class SecuGenScanner : IFingerprintScanner
    {
        private const int SG_DEV_AUTO = 0xFFFF;
        private bool _isInitialized = false;

        public string ScannerName => "SecuGen Hamster Pro 20";
        public bool IsConnected => _isInitialized;

        [DllImport("sgfplib.dll")]
        private static extern int SGFPM_Init(int deviceType);

        [DllImport("sgfplib.dll")]
        private static extern int SGFPM_Terminate();

        public Task<ScannerInitResult> InitializeAsync()
        {
            try
            {
                int result = SGFPM_Init(SG_DEV_AUTO);

                if (result == 0)
                {
                    _isInitialized = true;
                    return Task.FromResult(new ScannerInitResult
                    {
                        Success = true,
                        Message = "SecuGen initialized"
                    });
                }

                return Task.FromResult(new ScannerInitResult
                {
                    Success = false,
                    Message = "Scanner not found",
                    ErrorDetails = $"Error code: {result}"
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ScannerInitResult
                {
                    Success = false,
                    Message = "SDK error",
                    ErrorDetails = ex.Message
                });
            }
        }

        public Task<FingerprintCaptureResult> CaptureAsync()
        {
            // TODO: Implement capture
            return Task.FromResult(new FingerprintCaptureResult
            {
                Success = false,
                Message = "Not implemented yet"
            });
        }

        public Task<FingerprintMatchResult> MatchAsync(byte[] template1, byte[] template2)
        {
            bool match = template1.SequenceEqual(template2);
            return Task.FromResult(new FingerprintMatchResult
            {
                IsMatch = match,
                ConfidenceScore = match ? 100 : 0
            });
        }

        public int GetQualityScore(byte[] template) => 85;

        public Task DisconnectAsync()
        {
            if (_isInitialized)
            {
                SGFPM_Terminate();
                _isInitialized = false;
            }
            return Task.CompletedTask;
        }

        public ScannerInfo GetScannerInfo()
        {
            return new ScannerInfo
            {
                Name = "SecuGen Hamster Pro 20",
                Manufacturer = "SecuGen",
                Model = "HU20-A"
            };
        }
    }
}