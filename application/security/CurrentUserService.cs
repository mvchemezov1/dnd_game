using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using dnd_game.application.security;

namespace dnd_game.Application.Security
{
    public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        /// <summary>
        /// Возвращает Id текущего аутентифицированного пользователя.
        /// Бросает UnauthorizedAccessException, если запрос не аутентифицирован —
        /// вызывающий код (например, PolicyEnforcer) предполагает, что до его вызова
        /// пользователь уже прошёл аутентификацию, поэтому "пустого" случая тут не бывает
        /// в норме, и явная ошибка лучше, чем незаметный NotImplementedException.
        /// </summary>
        public Guid GetCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("No authenticated user in the current context.");
            return userId;
        }

        /// <summary>
        /// Мягкий вариант для мест, где отсутствие пользователя — ожидаемый случай
        /// (например, опциональная персонализация), а не ошибка.
        /// </summary>
        public Guid? TryGetCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null && Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
