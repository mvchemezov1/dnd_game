// application/security/permission_checker.cs
using dnd_game.Domain.Aggregates; // для доступа к типам, если необходимо
using System.Collections.Concurrent;

namespace dnd_game.Application.Security
{
    /// <summary>
    /// Роли пользователей в системе DnD.
    /// Определяют глобальные права доступа, не зависящие от конкретной кампании.
    /// </summary>
    public enum UserRole
    {
        /// <summary>Обычный игрок — участвует в кампаниях как владелец персонажа.</summary>
        Player = 1,

        /// <summary>Мастер игры — управляет кампанией и NPC.</summary>
        GameMaster = 2,

        /// <summary>Администратор — имеет неограниченные права.</summary>
        Admin = 3
    }

    /// <summary>
    /// Роль пользователя в конкретной кампании.
    /// Уточняет, какие действия пользователь может выполнять в рамках отдельной кампании.
    /// </summary>
    public enum CampaignRole
    {
        /// <summary>Игрок — участвует в кампании одним или несколькими персонажами.</summary>
        Player,

        /// <summary>Мастер игры — ведёт кампанию, управляет NPC и событиями.</summary>
        GameMaster,

        /// <summary>Наблюдатель — может просматривать, но не влияет на ход игры.</summary>
        Spectator
    }

    /// <summary>
    /// Информация о пользователе, необходимая для проверок безопасности.
    /// Собирается из различных источников (сессия, БД) и используется <see cref="PermissionChecker"/>.
    /// </summary>
    public class UserSecurityContext
    {
        /// <summary>Идентификатор пользователя.</summary>
        public Guid UserId { get; set; }

        /// <summary>Глобальная роль пользователя (по умолчанию — игрок).</summary>
        public UserRole GlobalRole { get; set; } = UserRole.Player;

        /// <summary>
        /// Список идентификаторов персонажей, которыми владеет пользователь как игрок.
        /// Заполняется из репозитория владения персонажами.
        /// </summary>
        public List<Guid> OwnedCharacterIds { get; set; } = [];

        /// <summary>
        /// Роль пользователя в каждой из кампаний, где он состоит.
        /// Ключ — идентификатор кампании, значение — роль в этой кампании.
        /// </summary>
        public Dictionary<Guid, CampaignRole> CampaignRoles { get; set; } = [];
    }

    /// <summary>
    /// Интерфейс получения контекста безопасности текущего пользователя.
    /// Реализация должна извлекать данные из HTTP-контекста, сессии или токена аутентификации.
    /// </summary>
    public interface IUserSecurityContextProvider
    {
        /// <summary>
        /// Возвращает контекст безопасности текущего пользователя.
        /// </summary>
        /// <returns>Объект <see cref="UserSecurityContext"/> с данными пользователя.</returns>
        UserSecurityContext GetCurrentContext();
    }

    /// <summary>
    /// Репозиторий для получения дополнительной информации о персонажах:
    /// владелец, принадлежность к кампании, является ли персонаж NPC.
    /// Используется проверками безопасности для определения прав доступа.
    /// </summary>
    public interface ICharacterOwnershipRepository
    {
        /// <summary>Получить идентификатор владельца персонажа (игрока).</summary>
        Guid? GetOwnerId(Guid characterId);

        /// <summary>Получить идентификатор кампании, в которой находится персонаж (если есть).</summary>
        Guid? GetCampaignId(Guid characterId);

        /// <summary>Проверить, является ли персонаж NPC (неигровым персонажем).</summary>
        bool IsNonPlayerCharacter(Guid characterId);

        /// <summary>Получить список идентификаторов персонажей, принадлежащих данному пользователю.</summary>
        List<Guid> GetOwnedCharacterIds(Guid userId);
    }

    /// <summary>
    /// Централизованный сервис проверки прав доступа.
    /// Содержит методы для проверки, может ли текущий пользователь выполнять те или иные действия
    /// с персонажами, кампаниями и NPC. Все проверки основаны на ролях (глобальных и в кампании)
    /// и данных о владении персонажами.
    /// </summary>
    /// <remarks>
    /// Паттерн: Policy-based authorization. Проверки делегируются этому классу из команд и запросов.
    /// Контекст безопасности загружается при каждом обращении к <see cref="Context"/>.
    /// </remarks>
    public class PermissionChecker(
        IUserSecurityContextProvider contextProvider,
        ICharacterOwnershipRepository characterRepo)
    {
        /// <summary>
        /// Ленивое получение контекста безопасности текущего пользователя.
        /// </summary>
        private UserSecurityContext Context => contextProvider.GetCurrentContext();

        // ---------- Глобальные проверки ----------

        /// <summary>
        /// Является ли пользователь Мастером игры (глобально) или администратором.
        /// </summary>
        /// <returns>True, если пользователь имеет роль GameMaster или Admin.</returns>
        public bool IsGameMaster()
        {
            var ctx = Context;
            return ctx.GlobalRole == UserRole.GameMaster || ctx.GlobalRole == UserRole.Admin;
        }

        /// <summary>
        /// Является ли пользователь администратором.
        /// </summary>
        /// <returns>True, если пользователь имеет роль Admin.</returns>
        public bool IsAdmin() => Context.GlobalRole == UserRole.Admin;

        /// <summary>
        /// Является ли пользователь Мастером в указанной кампании либо глобальным администратором.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <returns>True, если пользователь — администратор или имеет роль GameMaster в этой кампании.</returns>
        public bool IsGameMasterOfCampaign(Guid campaignId)
        {
            var ctx = Context;
            if (ctx.GlobalRole == UserRole.Admin) return true;
            return ctx.CampaignRoles.TryGetValue(campaignId, out var role) && role == CampaignRole.GameMaster;
        }

        /// <summary>
        /// Состоит ли пользователь в кампании (любая роль: игрок, мастер или наблюдатель).
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <returns>True, если пользователь имеет какую-либо роль в указанной кампании.</returns>
        public bool IsMemberOfCampaign(Guid campaignId)
        {
            var ctx = Context;
            return ctx.CampaignRoles.ContainsKey(campaignId);
        }

        // ---------- Проверки для персонажей ----------

        /// <summary>
        /// Может ли пользователь просматривать детали персонажа.
        /// Игроки видят только своих персонажей и известных NPC в рамках своих кампаний.
        /// Мастер/админ видят всех.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>True, если пользователю разрешён просмотр.</returns>
        public bool CanViewCharacter(Guid characterId)
        {
            if (IsAdmin()) return true;
            var ctx = Context;

            // Владелец персонажа всегда может его просматривать
            if (ctx.OwnedCharacterIds.Contains(characterId))
                return true;

            // Мастер может просматривать персонажей в своих кампаниях
            if (IsGameMaster())
            {
                var campId = characterRepo.GetCampaignId(characterId);
                if (campId.HasValue && IsGameMasterOfCampaign(campId.Value))
                    return true;
            }

            // Игрок может видеть NPC, если он в той же кампании
            if (characterRepo.IsNonPlayerCharacter(characterId))
            {
                var campId = characterRepo.GetCampaignId(characterId);
                if (campId.HasValue && IsMemberOfCampaign(campId.Value))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Может ли пользователь редактировать персонажа.
        /// Игроки могут редактировать только своих персонажей.
        /// Мастера могут редактировать любых персонажей в своей кампании (включая NPC).
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>True, если редактирование разрешено.</returns>
        public bool CanEditCharacter(Guid characterId)
        {
            if (IsAdmin()) return true;
            var ctx = Context;

            // Владелец-игрок может редактировать своего персонажа
            if (ctx.OwnedCharacterIds.Contains(characterId))
                return true;

            // Мастер кампании может редактировать любого персонажа в ней
            var campId = characterRepo.GetCampaignId(characterId);
            if (campId.HasValue && IsGameMasterOfCampaign(campId.Value))
                return true;

            return false;
        }

        /// <summary>
        /// Может ли пользователь удалить персонажа.
        /// Только администратор или Мастер (с осторожностью). Игроки не могут удалять.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>True, если удаление разрешено.</returns>
        public bool CanDeleteCharacter(Guid characterId)
        {
            // Игроки не могут удалять персонажей совсем
            if (!IsGameMaster() && !IsAdmin()) return false;

            if (IsAdmin()) return true;
            var campId = characterRepo.GetCampaignId(characterId);
            return campId.HasValue && IsGameMasterOfCampaign(campId.Value);
        }

        /// <summary>
        /// Может ли пользователь управлять персонажем (атаковать, колдовать, двигаться).
        /// Игрок управляет только своими персонажами; Мастер управляет NPC в своих кампаниях.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>True, если управление разрешено.</returns>
        public bool CanControlCharacter(Guid characterId)
        {
            if (IsAdmin()) return true;
            var ctx = Context;

            // Игрок может управлять своим персонажем
            if (ctx.OwnedCharacterIds.Contains(characterId))
                return true;

            // Мастер может управлять NPC или персонажами игроков в своей кампании
            if (IsGameMaster())
            {
                var campId = characterRepo.GetCampaignId(characterId);
                if (campId.HasValue && IsGameMasterOfCampaign(campId.Value))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Может ли пользователь использовать заклинание от имени персонажа.
        /// Аналогично управлению персонажем.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>True, если использование заклинаний разрешено.</returns>
        public bool CanCastSpell(Guid characterId) => CanControlCharacter(characterId);

        /// <summary>
        /// Может ли пользователь взаимодействовать с инвентарём персонажа.
        /// Аналогично редактированию персонажа.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>True, если управление инвентарём разрешено.</returns>
        public bool CanManageInventory(Guid characterId) => CanEditCharacter(characterId);

        /// <summary>
        /// Может ли пользователь совершать проверки навыков от имени персонажа.
        /// Аналогично управлению персонажем.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>True, если проверки навыков разрешены.</returns>
        public bool CanPerformSkillCheck(Guid characterId) => CanControlCharacter(characterId);

        // ---------- Проверки для кампании ----------

        /// <summary>
        /// Может ли пользователь просматривать информацию о кампании.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <returns>True, если пользователь является членом кампании или администратором.</returns>
        public bool CanViewCampaign(Guid campaignId) => IsMemberOfCampaign(campaignId) || IsAdmin();

        /// <summary>
        /// Может ли пользователь редактировать информацию о кампании.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <returns>True, если пользователь — Мастер в этой кампании или администратор.</returns>
        public bool CanEditCampaign(Guid campaignId) => IsGameMasterOfCampaign(campaignId);

        /// <summary>
        /// Может ли пользователь начать бой в кампании.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <returns>True, если пользователь — Мастер в этой кампании.</returns>
        public bool CanStartCombat(Guid campaignId) => IsGameMasterOfCampaign(campaignId);

        /// <summary>
        /// Может ли пользователь завершить бой (упрощённая проверка, без привязки к конкретному бою).
        /// </summary>
        /// <returns>True, если пользователь — Мастер или администратор.</returns>
        public bool CanEndCombat() => IsGameMaster(); // упрощённо, можно уточнить через загрузку боя

        // ---------- NPC ----------

        /// <summary>
        /// Может ли пользователь управлять NPC (общая проверка).
        /// </summary>
        /// <returns>True, если пользователь — Мастер или администратор.</returns>
        public bool CanManageNpc() => IsGameMaster(); // упрощённо

        /// <summary>
        /// Может ли пользователь завершить конкретный бой.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <returns>True, если пользователь — Мастер или администратор.</returns>
        public bool CanEndCombat(Guid combatId) => IsGameMaster();

        /// <summary>
        /// Может ли пользователь управлять конкретным NPC.
        /// </summary>
        /// <param name="npcId">Идентификатор NPC.</param>
        /// <returns>True, если пользователь — Мастер или администратор.</returns>
        public bool CanManageNpc(Guid npcId) => IsGameMaster();

        // ---------- Другие действия ----------

        /// <summary>
        /// Может ли пользователь отправлять сообщения в кампанию.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <returns>True, если пользователь является членом кампании.</returns>
        public bool CanSendMessageToCampaign(Guid campaignId) => IsMemberOfCampaign(campaignId);

        /// <summary>
        /// Может ли пользователь бросать кости. В базовой реализации разрешено всем.
        /// </summary>
        /// <returns>Всегда True в текущей реализации.</returns>
        public bool CanRollDice() => true; // все могут, но можно ограничить в некоторых случаях
    }
}