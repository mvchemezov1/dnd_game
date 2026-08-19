// application/security/policy_enforcer.cs
using dnd_game.application.security;
using System.Security;

namespace dnd_game.Application.Security
{
    /// <summary>
    /// Сервис для принудительного применения политик безопасности.
    /// Инкапсулирует вызовы <see cref="PermissionChecker"/> и выбрасывает исключения
    /// при нарушении прав доступа. Используется на уровне обработчиков команд и запросов
    /// для централизованной проверки авторизации.
    /// </summary>
    /// <remarks>
    /// Все методы следуют единому шаблону: проверить разрешение через <see cref="PermissionChecker"/>;
    /// если доступ запрещён — выбросить <see cref="UnauthorizedAccessException"/> с понятным сообщением.
    /// </remarks>
    public class PolicyEnforcer(PermissionChecker checker, ICurrentUserService currentUser)
    {
        /// <summary>
        /// Проверяет, что текущий аутентифицированный пользователь совпадает с ожидаемым идентификатором.
        /// Используется в сценариях, где операция инициируется пользователем, но требуется подтвердить,
        /// что он действует от своего имени.
        /// </summary>
        /// <param name="userId">Ожидаемый идентификатор пользователя.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если текущий пользователь не совпадает с <paramref name="userId"/>.</exception>
        private void EnsureCurrentUserMatches(Guid userId)
        {
            if (currentUser.GetCurrentUserId() != userId)
                throw new UnauthorizedAccessException("User mismatch: operation denied.");
        }

        // ---------- Персонажи ----------

        /// <summary>
        /// Принудительно проверяет право на просмотр информации о персонаже.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если пользователю запрещён просмотр.</exception>
        public void EnforceViewCharacter(Guid characterId)
        {
            if (!checker.CanViewCharacter(characterId))
                throw new UnauthorizedAccessException("You are not allowed to view this character.");
        }

        /// <summary>
        /// Принудительно проверяет право на редактирование персонажа.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если редактирование запрещено.</exception>
        public void EnforceEditCharacter(Guid characterId)
        {
            if (!checker.CanEditCharacter(characterId))
                throw new UnauthorizedAccessException("You are not allowed to edit this character.");
        }

        /// <summary>
        /// Принудительно проверяет право на управление персонажем (атака, движение, заклинания и т.д.).
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если управление запрещено.</exception>
        public void EnforceControlCharacter(Guid characterId)
        {
            if (!checker.CanControlCharacter(characterId))
                throw new UnauthorizedAccessException("You are not allowed to control this character.");
        }

        /// <summary>
        /// Принудительно проверяет право на удаление персонажа.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если удаление запрещено.</exception>
        public void EnforceDeleteCharacter(Guid characterId)
        {
            if (!checker.CanDeleteCharacter(characterId))
                throw new UnauthorizedAccessException("You are not allowed to delete this character.");
        }

        /// <summary>
        /// Принудительно проверяет право на управление инвентарём персонажа.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если управление инвентарём запрещено.</exception>
        public void EnforceManageInventory(Guid characterId)
        {
            if (!checker.CanManageInventory(characterId))
                throw new UnauthorizedAccessException("You are not allowed to manage inventory for this character.");
        }

        /// <summary>
        /// Принудительно проверяет право на использование заклинаний от имени персонажа.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если использование заклинаний запрещено.</exception>
        public void EnforceCastSpell(Guid characterId)
        {
            if (!checker.CanCastSpell(characterId))
                throw new UnauthorizedAccessException("You are not allowed to cast spells as this character.");
        }

        /// <summary>
        /// Принудительно проверяет право на выполнение проверок навыков от имени персонажа.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если проверки навыков запрещены.</exception>
        public void EnforcePerformSkillCheck(Guid characterId)
        {
            if (!checker.CanPerformSkillCheck(characterId))
                throw new UnauthorizedAccessException("You are not allowed to perform skill checks for this character.");
        }

        // ---------- Кампании ----------

        /// <summary>
        /// Принудительно проверяет право на просмотр информации о кампании.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если просмотр кампании запрещён.</exception>
        public void EnforceViewCampaign(Guid campaignId)
        {
            if (!checker.CanViewCampaign(campaignId))
                throw new UnauthorizedAccessException("You are not allowed to view this campaign.");
        }

        /// <summary>
        /// Принудительно проверяет право на редактирование кампании.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если редактирование кампании запрещено.</exception>
        public void EnforceEditCampaign(Guid campaignId)
        {
            if (!checker.CanEditCampaign(campaignId))
                throw new UnauthorizedAccessException("You are not allowed to edit this campaign.");
        }

        /// <summary>
        /// Принудительно проверяет право на начало боя в кампании.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если начало боя запрещено.</exception>
        public void EnforceStartCombat(Guid campaignId)
        {
            if (!checker.CanStartCombat(campaignId))
                throw new UnauthorizedAccessException("You are not allowed to start combat in this campaign.");
        }

        /// <summary>
        /// Принудительно проверяет право на завершение боя.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если завершение боя запрещено.</exception>
        public void EnforceEndCombat(Guid combatId)
        {
            if (!checker.CanEndCombat(combatId))
                throw new UnauthorizedAccessException("You are not allowed to end this combat.");
        }

        /// <summary>
        /// Принудительно проверяет право на отправку сообщений в кампанию.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если отправка сообщений запрещена.</exception>
        public void EnforceSendMessageToCampaign(Guid campaignId)
        {
            if (!checker.CanSendMessageToCampaign(campaignId))
                throw new UnauthorizedAccessException("You are not allowed to send messages in this campaign.");
        }

        // ---------- Мастерские действия ----------

        /// <summary>
        /// Принудительно проверяет, что пользователь является Мастером игры или администратором.
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если пользователь не является Мастером.</exception>
        public void EnforceGameMasterAction()
        {
            if (!checker.IsGameMaster())
                throw new UnauthorizedAccessException("Only a Game Master can perform this action.");
        }

        /// <summary>
        /// Принудительно проверяет, что пользователь является администратором.
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если пользователь не является администратором.</exception>
        public void EnforceAdminAction()
        {
            if (!checker.IsAdmin())
                throw new UnauthorizedAccessException("Only an administrator can perform this action.");
        }

        /// <summary>
        /// Принудительно проверяет право на управление NPC.
        /// </summary>
        /// <param name="npcId">Идентификатор NPC.</param>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если управление NPC запрещено.</exception>
        public void EnforceManageNpc(Guid npcId)
        {
            if (!checker.CanManageNpc(npcId))
                throw new UnauthorizedAccessException("You are not allowed to manage this NPC.");
        }

        // ---------- Прочее ----------

        /// <summary>
        /// Принудительно проверяет право на бросок костей.
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если бросок костей запрещён для пользователя.</exception>
        public void EnforceRollDice()
        {
            if (!checker.CanRollDice())
                throw new UnauthorizedAccessException("Dice rolling is currently disabled for you.");
        }

        /// <summary>
        /// Принудительно проверяет право на редактирование персонажа с дополнительной проверкой,
        /// что текущий пользователь соответствует указанному идентификатору.
        /// Метод сохранён для обратной совместимости.
        /// </summary>
        /// <param name="userId">Ожидаемый идентификатор пользователя, инициировавшего операцию.</param>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <exception cref="UnauthorizedAccessException">
        /// Выбрасывается, если текущий пользователь не совпадает с <paramref name="userId"/>
        /// или редактирование персонажа запрещено.
        /// </exception>
        public void EnforceEditCharacter(Guid userId, Guid characterId)
        {
            EnsureCurrentUserMatches(userId);
            EnforceEditCharacter(characterId);
        }
    }
}