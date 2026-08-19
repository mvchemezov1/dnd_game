// infrastructure/security/password_hasher.cs
using System.Security.Cryptography;
using System.Text;

namespace dnd_game.Infrastructure.Security
{
    /// <summary>
    /// Интерфейс сервиса хэширования и проверки паролей.
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// Хэшировать пароль для безопасного хранения.
        /// </summary>
        string Hash(string password);

        /// <summary>
        /// Проверить, соответствует ли пароль сохранённому хэшу.
        /// </summary>
        bool Verify(string password, string hash);

        /// <summary>
        /// Проверить, удовлетворяет ли пароль минимальным требованиям сложности.
        /// </summary>
        bool IsStrongPassword(string password);
    }

    /// <summary>
    /// Реализация на основе PBKDF2 (RFC 2898) с солью. 
    /// Заменяет устаревший прямой возврат пароля.
    /// </summary>
    public class PasswordHasher : IPasswordHasher
    {
        private const int SaltSize = 16;        // 128 бит соли
        private const int HashSize = 32;        // 256 бит хэша
        private const int Iterations = 100_000; // рекомендуемое количество итераций (2023+)

        /// <inheritdoc />
        public string Hash(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = GenerateHash(password, salt, Iterations, HashSize);

            // Формат хранения: {iterations}.{base64(salt)}.{base64(hash)}
            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        /// <inheritdoc />
        public bool Verify(string password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
                return false;

            var parts = hash.Split('.');
            if (parts.Length != 3) return false;

            if (!int.TryParse(parts[0], out int iterations)) return false;

            byte[] salt;
            byte[] storedHash;
            try
            {
                salt = Convert.FromBase64String(parts[1]);
                storedHash = Convert.FromBase64String(parts[2]);
            }
            catch
            {
                return false; // некорректный base64
            }

            byte[] computedHash = GenerateHash(password, salt, iterations, storedHash.Length);
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }

        /// <inheritdoc />
        public bool IsStrongPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password)) return false;
            if (password.Length < 8) return false;                                // минимальная длина
            if (!password.Any(char.IsUpper)) return false;                        // хотя бы одна заглавная
            if (!password.Any(char.IsLower)) return false;                        // хотя бы одна строчная
            if (!password.Any(char.IsDigit)) return false;                        // хотя бы одна цифра
            if (!password.Any(ch => !char.IsLetterOrDigit(ch))) return false;     // хотя бы один спецсимвол
            return true;
        }

        /// <summary>
        /// Генерирует хэш с использованием PBKDF2.
        /// </summary>
        private static byte[] GenerateHash(string password, byte[] salt, int iterations, int outputLength)
        {
            using var deriveBytes = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA512);  // SHA-512 для повышенной стойкости

            return deriveBytes.GetBytes(outputLength);
        }
    }
}