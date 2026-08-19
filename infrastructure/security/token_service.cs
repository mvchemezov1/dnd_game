using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Threading.Tasks;

namespace dnd_game.Infrastructure.Security
{
    public interface ITokenService
    {
        string GenerateAccessToken(UserAccount user);
        Task<string> GenerateRefreshTokenAsync(UserAccount user, string? deviceInfo = null);
        ClaimsPrincipal? ValidateToken(string token);
        Task<string?> RefreshAccessTokenAsync(string refreshToken);
        Task RevokeRefreshTokenAsync(string refreshToken);

        /// <summary>
        /// Отзывает все refresh-токены пользователя сразу (выход на всех устройствах,
        /// принудительный logout при подозрении на компрометацию аккаунта).
        /// </summary>
        Task RevokeAllRefreshTokensAsync(Guid userId);
    }

    public class TokenSettings
    {
        public string Secret { get; set; } = "change-me";
        public string Issuer { get; set; } = "DnD.Game";
        public string Audience { get; set; } = "DnD.Players";
        public int AccessTokenLifetimeMinutes { get; set; } = 60;
        public int RefreshTokenLifetimeDays { get; set; } = 7;
        public bool ValidateIssuerSigningKey { get; set; } = true;
        public bool ValidateLifetime { get; set; } = true;
    }

    public class RefreshTokenEntry
    {
        public string TokenHash { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string? DeviceInfo { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
    }

    /// <summary>
    /// Выдаёт и проверяет access/refresh токены. Refresh-токены хранятся не в памяти процесса,
    /// а в IRefreshTokenStore (PostgreSQL) — это переживает рестарт сервиса и работает
    /// одинаково при нескольких запущенных инстансах API за балансировщиком.
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly TokenSettings _settings;
        private readonly IUserRepository _userRepository;
        private readonly IRefreshTokenStore _refreshTokenStore;
        private readonly ILogger<TokenService> _logger;

        public TokenService(
            IOptions<TokenSettings> settings,
            IUserRepository userRepository,
            IRefreshTokenStore refreshTokenStore,
            ILogger<TokenService> logger)
        {
            _settings = settings.Value;
            _userRepository = userRepository;
            _refreshTokenStore = refreshTokenStore;
            _logger = logger;
        }

        public string GenerateAccessToken(UserAccount user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("role", user.GlobalRole.ToString())
            };

            if (user.CampaignRoles != null && user.CampaignRoles.Any())
            {
                var campaignRolesJson = System.Text.Json.JsonSerializer.Serialize(user.CampaignRoles);
                claims.Add(new Claim("campaign_roles", campaignRolesJson));
            }

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenLifetimeMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<string> GenerateRefreshTokenAsync(UserAccount user, string? deviceInfo = null)
        {
            var randomBytes = RandomNumberGenerator.GetBytes(64);
            var refreshToken = Convert.ToBase64String(randomBytes);
            var hash = ComputeSha256Hash(refreshToken);
            var entry = new RefreshTokenEntry
            {
                TokenHash = hash,
                UserId = user.Id,
                DeviceInfo = deviceInfo,
                ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenLifetimeDays),
                IsRevoked = false
            };
            await _refreshTokenStore.SaveAsync(entry);

            _logger.LogDebug("Refresh token generated for user {UserId}", user.Id);
            return refreshToken;
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_settings.Secret);

            try
            {
                var validationParams = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _settings.Audience,
                    ValidateLifetime = _settings.ValidateLifetime,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParams, out var validatedToken);
                return principal;
            }
            catch (SecurityTokenExpiredException)
            {
                _logger.LogWarning("Access token expired.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed.");
                return null;
            }
        }

        public async Task<string?> RefreshAccessTokenAsync(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken)) return null;
            var hash = ComputeSha256Hash(refreshToken);
            var entry = await _refreshTokenStore.GetByHashAsync(hash);
            if (entry == null)
            {
                _logger.LogWarning("Refresh token not found.");
                return null;
            }

            if (entry.IsRevoked || entry.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh token revoked or expired.");
                return null;
            }

            var user = await _userRepository.GetByIdAsync(entry.UserId);
            if (user == null || !user.IsActive)
            {
                _logger.LogWarning("User not found or inactive for refresh token.");
                return null;
            }

            return GenerateAccessToken(user);
        }

        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken)) return;
            var hash = ComputeSha256Hash(refreshToken);
            await _refreshTokenStore.RevokeAsync(hash);
            _logger.LogInformation("Refresh token revoked.");
        }

        public async Task RevokeAllRefreshTokensAsync(Guid userId)
        {
            await _refreshTokenStore.RevokeAllForUserAsync(userId);
            _logger.LogInformation("All refresh tokens revoked for user {UserId}", userId);
        }

        private static string ComputeSha256Hash(string rawData)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToBase64String(bytes);
        }
    }
}
