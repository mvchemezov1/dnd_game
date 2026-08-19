// application/security/permission_checker.cs
using dnd_game.Domain.Aggregates; // для доступа к типам, если необходимо
using System.Collections.Concurrent;

namespace dnd_game.Application.Security
{
    /// <summary>
    /// Роли пользователей в системе DnD.
    /// </summary>
    public enum UserRole
    {
        Player = 1,
        GameMaster = 2,
        Admin = 3
    }

    /// <summary>
    /// Роль пользователя в конкретной кампании.
    /// </summary>
    public enum CampaignRole
    {
        Player,
        GameMaster,
        Spectator
    }

    /// <summary>
    /// Информация о пользователе для проверок безопасности.
    /// </summary>
    public class UserSecurityContext
    {
        public Guid UserId { get; set; }
        public UserRole GlobalRole { get; set; } = UserRole.Player;
        /// <summary>
        /// Список идентификаторов персонажей, которыми владеет пользователь (как игрок).
        /// </summary>
        public List<Guid> OwnedCharacterIds { get; set; } = [];
        /// <summary>
        /// Роль пользователя в каждой из кампаний, где он состоит.
        /// </summary>
        public Dictionary<Guid, CampaignRole> CampaignRoles { get; set; } = [];
    }

    /// <summary>
    /// Интерфейс получения контекста безопасности текущего пользователя.
    /// </summary>
    public interface IUserSecurityContextProvider
    {
        UserSecurityContext GetCurrentContext();
    }

    /// <summary>
    /// Репозиторий для получения дополнительной информации о персонажах (владелец, статус, кампания).
    /// </summary>
    public interface ICharacterOwnershipRepository
    {
        /// <summary>Получить владельца персонажа (игрока).</summary>
        Guid? GetOwnerId(Guid characterId);
        /// <summary>Получить идентификатор кампании, в которой находится персонаж (если есть).</summary>
        Guid? GetCampaignId(Guid characterId);
        /// <summary>Проверить, является ли персонаж NPC (неигровым).</summary>
        bool IsNonPlayerCharacter(Guid characterId);
        /// <summary>РЎРїРёСЃРѕРє Id РїРµСЂСЃРѕРЅР°Р¶РµР№, РїСЂРёРЅР°РґР»РµР¶Р°С‰РёС… РґР°РЅРЅРѕРјСѓ РїРѕР»СЊР·РѕРІР°С‚РµР»СЋ.</summary>
        List<Guid> GetOwnedCharacterIds(Guid userId);
    }

    public class PermissionChecker(
        IUserSecurityContextProvider contextProvider,
        ICharacterOwnershipRepository characterRepo)
    {
        private UserSecurityContext Context => contextProvider.GetCurrentContext();

        // ---------- Глобальные проверки ----------
        public bool IsGameMaster()
        {
            var ctx = Context;
            return ctx.GlobalRole == UserRole.GameMaster || ctx.GlobalRole == UserRole.Admin;
        }

        public bool IsAdmin() => Context.GlobalRole == UserRole.Admin;

        /// <summary>
        /// Является ли пользователь Мастером в указанной кампании (либо глобальным администратором).
        /// </summary>
        public bool IsGameMasterOfCampaign(Guid campaignId)
        {
            var ctx = Context;
            if (ctx.GlobalRole == UserRole.Admin) return true;
            return ctx.CampaignRoles.TryGetValue(campaignId, out var role) && role == CampaignRole.GameMaster;
        }

        /// <summary>
        /// Состоит ли пользователь в кампании (любая роль).
        /// </summary>
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
        public bool CanViewCharacter(Guid characterId)
        {
            if (IsAdmin()) return true;
            var ctx = Context;

            // Владелец персонажа
            if (ctx.OwnedCharacterIds.Contains(characterId))
                return true;

            // Мастер может просматривать персонажей, если они в его кампании
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
        /// Редактирование включает изменение характеристик, уровня, экипировки.
        /// Игроки могут редактировать только своих персонажей (если разрешено правилами кампании).
        /// Мастера могут редактировать любых персонажей в своей кампании (включая NPC).
        /// </summary>
        public bool CanEditCharacter(Guid characterId)
        {
            if (IsAdmin()) return true;
            var ctx = Context;

            // Владелец-игрок может редактировать своего персонажа (если не в боевом состоянии, но это проверка не здесь)
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
        /// Только администратор или Мастер (с осторожностью).
        /// Игроки не могут удалять.
        /// </summary>
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
        /// Игрок управляет только своими персонажами, если они не мертвы/не ошеломлены (проверка состояния вне).
        /// Мастер управляет NPC в своих кампаниях.
        /// </summary>
        public bool CanControlCharacter(Guid characterId)
        {
            if (IsAdmin()) return true;
            var ctx = Context;

            // Игрок может управлять своим персонажем
            if (ctx.OwnedCharacterIds.Contains(characterId))
                return true;

            // Мастер может управлять NPC (или даже персонажами игроков при необходимости, по правилам)
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
        /// Аналогично управлению.
        /// </summary>
        public bool CanCastSpell(Guid characterId) => CanControlCharacter(characterId);

        /// <summary>
        /// Может ли пользователь взаимодействовать с инвентарём персонажа.
        /// </summary>
        public bool CanManageInventory(Guid characterId) => CanEditCharacter(characterId);

        /// <summary>
        /// Может ли пользователь совершать проверки навыков от имени персонажа.
        /// </summary>
        public bool CanPerformSkillCheck(Guid characterId) => CanControlCharacter(characterId);

        // ---------- Проверки для кампании ----------
        public bool CanViewCampaign(Guid campaignId) => IsMemberOfCampaign(campaignId) || IsAdmin();

        public bool CanEditCampaign(Guid campaignId) => IsGameMasterOfCampaign(campaignId);

        public bool CanStartCombat(Guid campaignId) => IsGameMasterOfCampaign(campaignId);
        public bool CanEndCombat() => IsGameMaster(); // упрощённо, можно уточнить через загрузку боя

        // ---------- NPC ----------
        public bool CanManageNpc() => IsGameMaster(); // упрощённо
        public bool CanEndCombat(Guid combatId) => IsGameMaster();
        public bool CanManageNpc(Guid npcId) => IsGameMaster();

        // ---------- Другие действия ----------
        public bool CanSendMessageToCampaign(Guid campaignId) => IsMemberOfCampaign(campaignId);
        public bool CanRollDice() => true; // все могут, но можно ограничить в некоторых случаях
    }
}