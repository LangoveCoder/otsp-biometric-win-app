using System;
using System.IO;
using System.Text.Json;

namespace BiometricCommon.Services
{
    public class RegistrationContext
    {
        public string CollegeName { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public string LaptopId { get; set; } = string.Empty;
        public int CollegeId { get; set; }
        public int TestId { get; set; }
        public DateTime SetDate { get; set; } = DateTime.Now;  // ✅ ADDED MISSING PROPERTY

        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BiometricVerification",
            "context.json");

        public static void SaveContext(RegistrationContext context)
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }

        public static RegistrationContext? GetCurrentContext()
        {
            if (!File.Exists(ConfigPath))
                return null;

            try
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<RegistrationContext>(json);
            }
            catch
            {
                return null;
            }
        }

        public static void ClearContext()
        {
            if (File.Exists(ConfigPath))
                File.Delete(ConfigPath);
        }

        public static bool HasContext()
        {
            return File.Exists(ConfigPath);
        }
    }
}