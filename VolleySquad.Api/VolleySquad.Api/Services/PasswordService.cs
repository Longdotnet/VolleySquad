// ============================================================
// PASSWORD SERVICE - PBKDF2 password hashing
// ============================================================
// Interview note:
//   "Tại sao không dùng SHA256(password)?"
//   -> Vì hash nhanh quá, attacker brute-force được rất nhanh.
//   -> Password hashing cần thuật toán CHẬM + có salt, ví dụ PBKDF2, bcrypt, scrypt, Argon2.
//
// PBKDF2 flow:
//   password + random salt + nhiều vòng lặp (iterations)
//   => tạo derived key khó brute-force hơn hash thường.
using System.Security.Cryptography;
using VolleySquad.Api.Services.Interfaces;

namespace VolleySquad.Api.Services
{
    public class PasswordService : IPasswordService
    {
        private const int SaltSize = 16;          // 128-bit salt
        private const int KeySize = 32;           // 256-bit derived key
        private const int Iterations = 100_000;   // Demo/prod small app: đủ tốt hơn hash thường rất nhiều
        private const string FormatMarker = "pbkdf2-sha256";

        public string HashPassword(string password)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(password);

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

            // Lưu cả metadata để sau này đổi iterations/algorithm vẫn verify được account cũ.
            return string.Join('.',
                FormatMarker,
                Iterations,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        public bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
                return false;

            var parts = storedHash.Split('.');
            if (parts.Length != 4 || parts[0] != FormatMarker || !int.TryParse(parts[1], out var iterations))
                return false;

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expectedHash = Convert.FromBase64String(parts[3]);
                var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

                // FixedTimeEquals tránh timing attack khi so sánh byte array.
                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}