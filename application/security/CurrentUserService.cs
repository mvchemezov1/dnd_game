using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using dnd_game.application.security;

namespace dnd_game.Application.Security
{
    /// <summary>
    /// Сервис для получения информации о текущем аутентифицированном пользователе.
    /// Извлекает идентификатор пользователя из контекста HTTP-запроса (claims).
    /// </summary>
    /// <remarks>
    /// Используется в слое безопасности для авторизации и персонализации данных.
    /// Предполагается, что вызывающий код гарантирует наличие аутентифицированного пользователя,
    /// поэтому метод <see cref="GetCurrentUserId"/> выбрасывает исключение при его отсутствии.
    /// </remarks>
    public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        /// <summary>
        /// Возвращает идентификатор текущего аутентифицированного пользователя.
        /// </summary>
        /// <returns>GUID идентификатор пользователя.</returns>
        /// <exception cref="UnauthorizedAccessException">
        /// Выбрасывается, если контекст отсутствует, пользователь не аутентифицирован
        /// или claim <see cref="ClaimTypes.NameIdentifier"/> не содержит корректный GUID.
        /// </exception>
        /// <remarks>
        /// Явная ошибка лучше, чем незаметное некорректное поведение:
        /// вызывающий код (например, <c>PolicyEnforcer</c>) предполагает, что до его вызова
        /// пользователь уже прошёл аутентификацию, поэтому "пустого" случая в норме не бывает.
        /// </remarks>
        public Guid GetCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("No authenticated user in the current context.");
            return userId;
        }

        /// <summary>
        /// Мягкий вариант получения идентификатора текущего пользователя.
        /// Возвращает <c>null</c>, если пользователь не аутентифицирован или claim отсутствует.
        /// </summary>
        /// <returns>
        /// GUID идентификатор пользователя или <c>null</c>, если его невозможно определить.
        /// </returns>
        /// <remarks>
        /// Используется в местах, где отсутствие пользователя — ожидаемый случай
        /// (например, опциональная персонализация), а не ошибка.
        /// </remarks>
        public Guid? TryGetCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null && Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}