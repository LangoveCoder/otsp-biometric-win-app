using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BiometricCommon.Encryption
{
    /// <summary>
    /// Provides encryption, decryption, and hashing services for the biometric system
    /// Uses AES-256 encryption and SHA-256 hashing
    /// </summary>
    public static class EncryptionService
    {
        private const int KeySize = 256;
        private const int IvSize = 128;
        private const int SaltSize = 32;
        private const int Iterations = 10000;

        /// <summary>
        /// Encrypts a string using AES-256 encryption
        /// </summary>
        /// <param name="plainText">Text to encrypt</param>
        /// <param name="key">Encryption key</param>
        /// <returns>Base64 encoded encrypted string</returns>
        public static string Encrypt(string plainText, string key)
        {
            if (string.IsNullOrEmpty(plainText))
                throw new ArgumentNullException(nameof(plainText));

            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            byte[] encrypted;
            byte[] salt = GenerateRandomBytes(SaltSize);

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.BlockSize = IvSize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                // Derive key from password
                using (var deriveBytes = new Rfc2898DeriveBytes(key, salt, Iterations, HashAlgorithmName.SHA256))
                {
                    aes.Key = deriveBytes.GetBytes(KeySize / 8);
                    aes.IV = deriveBytes.GetBytes(IvSize / 8);
                }

                using (var encryptor = aes.CreateEncryptor())
                using (var msEncrypt = new MemoryStream())
                {
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (var swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                    encrypted = msEncrypt.ToArray();
                }
            }

            // Prepend salt to encrypted data
            byte[] result = new byte[salt.Length + encrypted.Length];
            Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
            Buffer.BlockCopy(encrypted, 0, result, salt.Length, encrypted.Length);

            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// Decrypts an AES-256 encrypted string
        /// </summary>
        /// <param name="cipherText">Base64 encoded encrypted text</param>
        /// <param name="key">Decryption key</param>
        /// <returns>Decrypted plain text</returns>
        public static string Decrypt(string cipherText, string key)
        {
            if (string.IsNullOrEmpty(cipherText))
                throw new ArgumentNullException(nameof(cipherText));

            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            byte[] cipherBytes = Convert.FromBase64String(cipherText);

            // Extract salt from the beginning
            byte[] salt = new byte[SaltSize];
            byte[] encrypted = new byte[cipherBytes.Length - SaltSize];
            Buffer.BlockCopy(cipherBytes, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(cipherBytes, SaltSize, encrypted, 0, encrypted.Length);

            string plaintext;

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.BlockSize = IvSize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                // Derive key from password
                using (var deriveBytes = new Rfc2898DeriveBytes(key, salt, Iterations, HashAlgorithmName.SHA256))
                {
                    aes.Key = deriveBytes.GetBytes(KeySize / 8);
                    aes.IV = deriveBytes.GetBytes(IvSize / 8);
                }

                using (var decryptor = aes.CreateDecryptor())
                using (var msDecrypt = new MemoryStream(encrypted))
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (var srDecrypt = new StreamReader(csDecrypt))
                {
                    plaintext = srDecrypt.ReadToEnd();
                }
            }

            return plaintext;
        }

        /// <summary>
        /// Encrypts a byte array (e.g., fingerprint template)
        /// </summary>
        /// <param name="data">Data to encrypt</param>
        /// <param name="key">Encryption key</param>
        /// <returns>Encrypted byte array</returns>
        public static byte[] EncryptBytes(byte[] data, string key)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentNullException(nameof(data));

            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            byte[] encrypted;
            byte[] salt = GenerateRandomBytes(SaltSize);

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.BlockSize = IvSize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var deriveBytes = new Rfc2898DeriveBytes(key, salt, Iterations, HashAlgorithmName.SHA256))
                {
                    aes.Key = deriveBytes.GetBytes(KeySize / 8);
                    aes.IV = deriveBytes.GetBytes(IvSize / 8);
                }

                using (var encryptor = aes.CreateEncryptor())
                using (var msEncrypt = new MemoryStream())
                {
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(data, 0, data.Length);
                        csEncrypt.FlushFinalBlock();
                    }
                    encrypted = msEncrypt.ToArray();
                }
            }

            // Prepend salt to encrypted data
            byte[] result = new byte[salt.Length + encrypted.Length];
            Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
            Buffer.BlockCopy(encrypted, 0, result, salt.Length, encrypted.Length);

            return result;
        }

        /// <summary>
        /// Decrypts a byte array
        /// </summary>
        /// <param name="encryptedData">Encrypted data</param>
        /// <param name="key">Decryption key</param>
        /// <returns>Decrypted byte array</returns>
        public static byte[] DecryptBytes(byte[] encryptedData, string key)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                throw new ArgumentNullException(nameof(encryptedData));

            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            // Extract salt from the beginning
            byte[] salt = new byte[SaltSize];
            byte[] encrypted = new byte[encryptedData.Length - SaltSize];
            Buffer.BlockCopy(encryptedData, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(encryptedData, SaltSize, encrypted, 0, encrypted.Length);

            byte[] decrypted;

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.BlockSize = IvSize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var deriveBytes = new Rfc2898DeriveBytes(key, salt, Iterations, HashAlgorithmName.SHA256))
                {
                    aes.Key = deriveBytes.GetBytes(KeySize / 8);
                    aes.IV = deriveBytes.GetBytes(IvSize / 8);
                }

                using (var decryptor = aes.CreateDecryptor())
                using (var msDecrypt = new MemoryStream(encrypted))
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (var msPlain = new MemoryStream())
                {
                    csDecrypt.CopyTo(msPlain);
                    decrypted = msPlain.ToArray();
                }
            }

            return decrypted;
        }

        /// <summary>
        /// Encrypts a file
        /// </summary>
        /// <param name="inputFile">Source file path</param>
        /// <param name="outputFile">Destination encrypted file path</param>
        /// <param name="key">Encryption key</param>
        public static void EncryptFile(string inputFile, string outputFile, string key)
        {
            if (!File.Exists(inputFile))
                throw new FileNotFoundException("Input file not found", inputFile);

            byte[] fileData = File.ReadAllBytes(inputFile);
            byte[] encryptedData = EncryptBytes(fileData, key);
            File.WriteAllBytes(outputFile, encryptedData);
        }

        /// <summary>
        /// Decrypts a file
        /// </summary>
        /// <param name="inputFile">Encrypted file path</param>
        /// <param name="outputFile">Destination decrypted file path</param>
        /// <param name="key">Decryption key</param>
        public static void DecryptFile(string inputFile, string outputFile, string key)
        {
            if (!File.Exists(inputFile))
                throw new FileNotFoundException("Input file not found", inputFile);

            byte[] encryptedData = File.ReadAllBytes(inputFile);
            byte[] decryptedData = DecryptBytes(encryptedData, key);
            File.WriteAllBytes(outputFile, decryptedData);
        }

        /// <summary>
        /// Hashes a password using SHA-256 with salt
        /// </summary>
        /// <param name="password">Password to hash</param>
        /// <returns>Base64 encoded hash</returns>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            byte[] salt = GenerateRandomBytes(SaltSize);

            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = deriveBytes.GetBytes(32); // 256 bits

                // Combine salt and hash
                byte[] hashBytes = new byte[SaltSize + 32];
                Buffer.BlockCopy(salt, 0, hashBytes, 0, SaltSize);
                Buffer.BlockCopy(hash, 0, hashBytes, SaltSize, 32);

                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Verifies a password against a hash
        /// </summary>
        /// <param name="password">Password to verify</param>
        /// <param name="hashedPassword">Hashed password to compare against</param>
        /// <returns>True if password matches</returns>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            if (string.IsNullOrEmpty(hashedPassword))
                throw new ArgumentNullException(nameof(hashedPassword));

            byte[] hashBytes = Convert.FromBase64String(hashedPassword);

            // Extract salt
            byte[] salt = new byte[SaltSize];
            Buffer.BlockCopy(hashBytes, 0, salt, 0, SaltSize);

            // Hash the input password with the extracted salt
            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = deriveBytes.GetBytes(32);

                // Compare the hashes
                for (int i = 0; i < 32; i++)
                {
                    if (hashBytes[i + SaltSize] != hash[i])
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Generates a unique encryption key for a college
        /// </summary>
        /// <param name="collegeCode">College code</param>
        /// <param name="testCode">Test code</param>
        /// <returns>Unique encryption key</returns>
        public static string GenerateCollegeKey(string collegeCode, string testCode)
        {
            string baseKey = $"{collegeCode}-{testCode}-{DateTime.Now.Ticks}";
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(baseKey));
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Generates a checksum for data integrity verification
        /// </summary>
        /// <param name="data">Data to checksum</param>
        /// <returns>Checksum string</returns>
        public static string GenerateChecksum(string data)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Generates a checksum for byte array data
        /// </summary>
        /// <param name="data">Byte array to checksum</param>
        /// <returns>Checksum string</returns>
        public static string GenerateChecksum(byte[] data)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(data);
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Generates cryptographically secure random bytes
        /// </summary>
        /// <param name="size">Number of bytes to generate</param>
        /// <returns>Random byte array</returns>
        private static byte[] GenerateRandomBytes(int size)
        {
            byte[] randomBytes = new byte[size];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return randomBytes;
        }

        /// <summary>
        /// Generates a random password for college admin
        /// </summary>
        /// <param name="length">Password length (default: 12)</param>
        /// <returns>Random password</returns>
        public static string GenerateRandomPassword(int length = 12)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789@#$%";
            byte[] randomBytes = GenerateRandomBytes(length);
            char[] password = new char[length];

            for (int i = 0; i < length; i++)
            {
                password[i] = chars[randomBytes[i] % chars.Length];
            }

            return new string(password);
        }
    }
}