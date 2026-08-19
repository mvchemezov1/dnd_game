namespace dnd_game.application.security
{
    public interface ICurrentUserService
    {
        Guid GetCurrentUserId();
    }
}
