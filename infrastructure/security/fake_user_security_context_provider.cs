// infrastructure/security/fake_user_security_context_provider.cs
using dnd_game.Application.Security;

/// <summary>
/// Управляемый фейк для юнит/интеграционных тестов, где не нужен реальный HTTP-контекст.
/// НЕ регистрируется в основной DI-конфигурации приложения (см. dependencies.cs, там
/// используется HttpUserSecurityContextProvider) — только для тестов, где можно явно
/// задать нужный UserId/роль через конструктор.
/// </summary>
public class FakeUserSecurityContextProvider(
    Guid? userId = null,
    UserRole globalRole = UserRole.Player,
    List<Guid>? ownedCharacterIds = null,
    Dictionary<Guid, CampaignRole>? campaignRoles = null) : IUserSecurityContextProvider
{
    public Guid UserId { get; } = userId ?? Guid.NewGuid();

    public UserSecurityContext GetCurrentContext() => new()
    {
        UserId = UserId,
        GlobalRole = globalRole,
        OwnedCharacterIds = ownedCharacterIds ?? [],
        CampaignRoles = campaignRoles ?? []
    };
}
