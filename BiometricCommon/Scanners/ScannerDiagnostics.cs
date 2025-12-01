using System;
using System.IO;
using System.Runtime.InteropServices;

namespace BiometricCommon.Scanners
{
    public static class ScannerDiagnostics
    {
        public static string RunDiagnostics()
        {
            var report = "=== SecuGen Scanner Diagnostics ===\n\n";

            // Check DLL files
            report += "1. Checking DLL Files:\n";
            string appPath = AppDomain.CurrentDomain.BaseDirectory;

            string[] requiredDlls = {
                "sgfplib.dll",
                "sgbledev.dll",
                "sgfdusdax64.dll",
                "sgfpamx.dll",
                "sgwsqlib.dll",
                "SecuGen.FDxSDKPro.DotNet.Windows.dll"
            };

            foreach (var dll in requiredDlls)
            {
                string path = Path.Combine(appPath, dll);
                bool exists = File.Exists(path);
                report += $"   {dll}: {(exists ? "✓ Found" : "✗ MISSING")}\n";
            }

            report += "\n2. Platform Information:\n";
            report += $"   OS: {Environment.OSVersion}\n";
            report += $"   64-bit OS: {Environment.Is64BitOperatingSystem}\n";
            report += $"   64-bit Process: {Environment.Is64BitProcess}\n";
            report += $"   App Path: {appPath}\n";

            // Try to load DLL
            report += "\n3. DLL Load Test:\n";
            try
            {
                IntPtr handle = LoadLibrary("sgfplib.dll");
                if (handle != IntPtr.Zero)
                {
                    report += "   sgfplib.dll: ✓ Loaded successfully\n";
                    FreeLibrary(handle);
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    report += $"   sgfplib.dll: ✗ Failed to load (Error: {error})\n";
                }
            }
            catch (Exception ex)
            {
                report += $"   sgfplib.dll: ✗ Exception: {ex.Message}\n";
            }

            return report;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);
    }
}