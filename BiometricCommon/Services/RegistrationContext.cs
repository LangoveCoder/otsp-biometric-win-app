using System;
using System.IO;
using System.Text.Json;

namespace BiometricCommon.Services  // ✅ CORRECT
{
    public class RegistrationContext
    {
        public string CollegeName { get; set; }
        public string TestName { get; set; }
        public string LaptopId { get; set; }
        public int CollegeId { get; set; }
        public int TestId { get; set; }

        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BiometricVerification",
            "context.json");

        public static void SaveContext(RegistrationContext context)
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(context);
            File.WriteAllText(ConfigPath, json);
        }

        public static RegistrationContext GetCurrentContext()
        {
            if (!File.Exists(ConfigPath))
                return null;

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<RegistrationContext>(json);
        }

        public static void ClearContext()
        {
            if (File.Exists(ConfigPath))
                File.Delete(ConfigPath);
        }
    }
}