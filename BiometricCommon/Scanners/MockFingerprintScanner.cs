using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BiometricCommon.Services;

namespace BiometricCommon.Scanners
{
    public class MockFingerprintScanner : BiometricCommon.Services.IFingerprintScanner
    {
        private bool _isConnected;
        private Random _random;

        public string ScannerName => "Mock Scanner (Testing)";
        public bool IsConnected => _isConnected;

        public MockFingerprintScanner()
        {
            _random = new Random();
        }

        public Task<BiometricCommon.Services.ScannerInitResult> InitializeAsync()
        {
            Task.Delay(500).Wait();
            _isConnected = true;
            return Task.FromResult(new BiometricCommon.Services.ScannerInitResult
            {
                Success = true,
                Message = "Mock scanner initialized"
            });
        }

        public async Task<BiometricCommon.Services.FingerprintCaptureResult> CaptureAsync()
        {
            await Task.Delay(_random.Next(1000, 2000));
            var template = GenerateRandomTemplate();
            var quality = _random.Next(70, 100);

            return new BiometricCommon.Services.FingerprintCaptureResult
            {
                Success = true,
                Template = template,
                QualityScore = quality,
                Message = $"Captured (Quality: {quality})"
            };
        }

        public Task<BiometricCommon.Services.FingerprintMatchResult> MatchAsync(byte[] template1, byte[] template2)
        {
            if (template1.SequenceEqual(template2))
            {
                return Task.FromResult(new BiometricCommon.Services.FingerprintMatchResult
                {
                    IsMatch = true,
                    ConfidenceScore = 100,
                    Message = "Perfect match"
                });
            }

            var score = _random.Next(40, 95);
            return Task.FromResult(new BiometricCommon.Services.FingerprintMatchResult
            {
                IsMatch = score >= 70,
                ConfidenceScore = score,
                Message = score >= 70 ? "Match" : "No match"
            });
        }

        public int GetQualityScore(byte[] template) => _random.Next(70, 100);

        public Task DisconnectAsync()
        {
            _isConnected = false;
            return Task.CompletedTask;
        }

        public BiometricCommon.Services.ScannerInfo GetScannerInfo()
        {
            return new BiometricCommon.Services.ScannerInfo
            {
                Name = "Mock Scanner",
                Manufacturer = "Test",
                Model = "MOCK-2024"
            };
        }

        private byte[] GenerateRandomTemplate()
        {
            var data = $"FP-{DateTime.Now.Ticks}-{_random.Next(1000000)}";
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        }
    }
}