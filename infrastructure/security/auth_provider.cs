using dnd_game.Application.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace dnd_game.Infrastructure.Security
{
    // ---------- ���������� � DTO ----------
    public interface IAuthProvider
    {
        Task<AuthResult> RegisterAsync(AuthRequest request);
        Task<AuthResult> LoginAsync(AuthRequest request);
        Task<AuthResult> RefreshTokenAsync(string refreshToken);
        Task<bool> ValidateTokenAsync(string token);
        Task<UserSecurityContext?> GetUserContextFromTokenAsync(string token);
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public string? ErrorMessage { get; set; }
        public Guid? UserId { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class AuthRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        // Только для регистрации: желаемая роль ("Player" или "GameMaster").
        // "Admin" через публичную регистрацию получить нельзя — см. RegisterAsync.
        public string? Role { get; set; }
    }

    public class JwtSettings
    {
        public string Secret { get; set; } = "change-me";
        public string Issuer { get; set; } = "DnD.Game";
        public string Audience { get; set; } = "DnD.Players";
        public int AccessTokenExpirationMinutes { get; set; } = 60;
        public int RefreshTokenExpirationDays { get; set; } = 7;
    }

    // ---------- ���������� AuthProvider ----------
    public class AuthProvider : IAuthProvider
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<AuthProvider> _logger;

        public AuthProvider(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            JwtSettings jwtSettings,
            ILogger<AuthProvider> logger)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _jwtSettings = jwtSettings;
            _logger = logger;
        }

        public async Task<AuthResult> RegisterAsync(AuthRequest request)
        {
            var existingByUsername = await _userRepository.GetByUsernameAsync(request.Username);
            if (existingByUsername != null)
                return new AuthResult { Success = false, ErrorMessage = "Username already taken." };

            var existingByEmail = await _userRepository.GetByEmailAsync(request.Email);
            if (existingByEmail != null)
                return new AuthResult { Success = false, ErrorMessage = "Email already registered." };

            if (!_passwordHasher.IsStrongPassword(request.Password))
                return new AuthResult { Success = false, ErrorMessage = "Password must be at least 8 characters, contain upper/lower case, digit, and special character." };

            // Публичная регистрация может выдать только Player или GameMaster.
            // Admin (роль разработчика) через эту форму получить нельзя — любое другое
            // значение, включая "Admin" или отсутствие поля, откатывается к Player.
            var role = UserRole.Player;
            if (Enum.TryParse<UserRole>(request.Role, out var requestedRole) && requestedRole == UserRole.GameMaster)
                role = UserRole.GameMaster;

            var user = new UserAccount
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                Email = request.Email,
                PasswordHash = _passwordHasher.Hash(request.Password),
                GlobalRole = role,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                CampaignRoles = new Dictionary<Guid, CampaignRole>()
            };
            await _userRepository.AddAsync(user);

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user);

            return new AuthResult
            {
                Success = true,
                Token = accessToken,
                RefreshToken = refreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes)
            };
        }

        public async Task<AuthResult> LoginAsync(AuthRequest request)
        {
            var user = await _userRepository.GetByUsernameAsync(request.Username);
            if (user == null || !user.IsActive)
                return new AuthResult { Success = false, ErrorMessage = "Invalid credentials." };

            if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
                return new AuthResult { Success = false, ErrorMessage = "Invalid credentials." };

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user);

            return new AuthResult
            {
                Success = true,
                Token = accessToken,
                RefreshToken = refreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes)
            };
        }

        public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
        {
            var newToken = await _tokenService.RefreshAccessTokenAsync(refreshToken);
            if (newToken == null)
                return new AuthResult { Success = false, ErrorMessage = "Invalid or expired refresh token." };

            var principal = _tokenService.ValidateToken(newToken);
            var userIdClaim = principal?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return new AuthResult { Success = false, ErrorMessage = "Invalid token data." };

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || !user.IsActive)
                return new AuthResult { Success = false, ErrorMessage = "User not found or inactive." };

            var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync(user);
            // Ротация refresh-токена: старый токен больше не должен быть валиден,
            // иначе утечка одного refresh-токена даёт бессрочный доступ до истечения TTL.
            await _tokenService.RevokeRefreshTokenAsync(refreshToken);

            return new AuthResult
            {
                Success = true,
                Token = newToken,
                RefreshToken = newRefreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes)
            };
        }

        public Task<bool> ValidateTokenAsync(string token)
        {
            var principal = _tokenService.ValidateToken(token);
            return Task.FromResult(principal != null);
        }

        public async Task<UserSecurityContext?> GetUserContextFromTokenAsync(string token)
        {
            var principal = _tokenService.ValidateToken(token);
            if (principal == null) return null;

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return null;

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return null;

            var roleClaim = principal.FindFirst(ClaimTypes.Role);
            Enum.TryParse<UserRole>(roleClaim?.Value, out var globalRole);

            return new UserSecurityContext
            {
                UserId = user.Id,
                GlobalRole = globalRole,
                OwnedCharacterIds = new List<Guid>(),
                CampaignRoles = user.CampaignRoles ?? new Dictionary<Guid, CampaignRole>()
            };
        }
    }
}