// infrastructure/security/user_security_context_provider.cs
using Microsoft.AspNetCore.Http;
using dnd_game.Application.Security;
using dnd_game.application.security;

namespace dnd_game.Infrastructure.Security
{
    /// <summary>
    /// Строит UserSecurityContext для текущего аутентифицированного HTTP-запроса на основе
    /// реального пользователя (ICurrentUserService) и его данных (IUserRepository,
    /// ICharacterOwnershipRepository).
    ///
    /// Раньше в DI был зарегистрирован FakeUserSecurityContextProvider, который возвращал
    /// случайный Guid как UserId и бросал NotImplementedException из GetCurrentContext() —
    /// то есть PermissionChecker (а значит вообще все проверки прав доступа в игре)
    /// гарантированно падал на каждом вызове. Это заменяет его на рабочую реализацию.
    ///
    /// Примечание: IUserSecurityContextProvider.GetCurrentContext() — синхронный метод
    /// (так исторически сложился интерфейс PermissionChecker), поэтому здесь используется
    /// блокирующий вызов к репозиторию. Чтобы не делать этот блокирующий вызов на каждую
    /// отдельную проверку прав (их обычно несколько на один запрос), результат кешируется
    /// на время одного HTTP-запроса через HttpContext.Items. Полный переход PermissionChecker
    /// на async — отдельная, более крупная задача.
    /// </summary>
    public class HttpUserSecurityContextProvider(
        IHttpContextAccessor httpContextAccessor,
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ICharacterOwnershipRepository ownershipRepository) : IUserSecurityContextProvider
    {
        private const string CacheKey = "__UserSecurityContext";

        public UserSecurityContext GetCurrentContext()
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext != null && httpContext.Items.TryGetValue(CacheKey, out var cached) && cached is UserSecurityContext cachedContext)
                return cachedContext;

            var userId = currentUserService.GetCurrentUserId(); // бросает UnauthorizedAccessException, если запрос не аутентифицирован

            var user = userRepository.GetByIdAsync(userId).GetAwaiter().GetResult()
                ?? throw new UnauthorizedAccessException("Authenticated user not found.");

            var context = new UserSecurityContext
            {
                UserId = userId,
                GlobalRole = user.GlobalRole,
                OwnedCharacterIds = ownershipRepository.GetOwnedCharacterIds(userId),
                CampaignRoles = user.CampaignRoles ?? new Dictionary<Guid, CampaignRole>()
            };

            if (httpContext != null)
                httpContext.Items[CacheKey] = context;

            return context;
        }
    }
}
