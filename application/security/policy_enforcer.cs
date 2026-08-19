// application/security/policy_enforcer.cs
using dnd_game.application.security;
using System.Security;

namespace dnd_game.Application.Security
{
    /// <summary>
    /// Сервис для получения идентификатора текущего пользователя (из аутентификации).
    /// </summary>
    public class PolicyEnforcer(PermissionChecker checker, ICurrentUserService currentUser)
    {

        // Удостовериться, что текущий пользователь совпадает с ожидаемым (если требуется)
        private void EnsureCurrentUserMatches(Guid userId)
        {
            if (currentUser.GetCurrentUserId() != userId)
                throw new UnauthorizedAccessException("User mismatch: operation denied.");
        }

        // ---------- Персонажи ----------
        public void EnforceViewCharacter(Guid characterId)
        {
            if (!checker.CanViewCharacter(characterId))
                throw new UnauthorizedAccessException("You are not allowed to view this character.");
        }

        public void EnforceEditCharacter(Guid characterId)
        {
            if (!checker.CanEditCharacter(characterId))
                throw new UnauthorizedAccessException("You are not allowed to edit this character.");
        }

        public void EnforceControlCharacter(Guid characterId)
        {
            if (!checker.CanControlCharacter(characterId))
                throw new UnauthorizedAccessException("You are not allowed to control this character.");
        }

        public void EnforceDeleteCharacter(Guid characterId)
        {
            if (!checker.CanDeleteCharacter(characterId))
                throw new UnauthorizedAccessException("You are not allowed to delete this character.");
        }

        public void EnforceManageInventory(Guid characterId)
        {
            if (!checker.CanManageInventory(characterId))
                throw new UnauthorizedAccessException("You are not allowed to manage inventory for this character.");
        }

        public void EnforceCastSpell(Guid characterId)
        {
            if (!checker.CanCastSpell(characterId))
                throw new UnauthorizedAccessException("You are not allowed to cast spells as this character.");
        }

        public void EnforcePerformSkillCheck(Guid characterId)
        {
            if (!checker.CanPerformSkillCheck(characterId))
                throw new UnauthorizedAccessException("You are not allowed to perform skill checks for this character.");
        }

        // ---------- Кампании ----------
        public void EnforceViewCampaign(Guid campaignId)
        {
            if (!checker.CanViewCampaign(campaignId))
                throw new UnauthorizedAccessException("You are not allowed to view this campaign.");
        }

        public void EnforceEditCampaign(Guid campaignId)
        {
            if (!checker.CanEditCampaign(campaignId))
                throw new UnauthorizedAccessException("You are not allowed to edit this campaign.");
        }

        public void EnforceStartCombat(Guid campaignId)
        {
            if (!checker.CanStartCombat(campaignId))
                throw new UnauthorizedAccessException("You are not allowed to start combat in this campaign.");
        }

        public void EnforceEndCombat(Guid combatId)
        {
            if (!checker.CanEndCombat(combatId))
                throw new UnauthorizedAccessException("You are not allowed to end this combat.");
        }

        public void EnforceSendMessageToCampaign(Guid campaignId)
        {
            if (!checker.CanSendMessageToCampaign(campaignId))
                throw new UnauthorizedAccessException("You are not allowed to send messages in this campaign.");
        }

        // ---------- Мастерские действия ----------
        public void EnforceGameMasterAction()
        {
            if (!checker.IsGameMaster())
                throw new UnauthorizedAccessException("Only a Game Master can perform this action.");
        }

        public void EnforceAdminAction()
        {
            if (!checker.IsAdmin())
                throw new UnauthorizedAccessException("Only an administrator can perform this action.");
        }

        public void EnforceManageNpc(Guid npcId)
        {
            if (!checker.CanManageNpc(npcId))
                throw new UnauthorizedAccessException("You are not allowed to manage this NPC.");
        }

        // ---------- Прочее ----------
        public void EnforceRollDice()
        {
            if (!checker.CanRollDice())
                throw new UnauthorizedAccessException("Dice rolling is currently disabled for you.");
        }

        // Сохранённый для обратной совместимости метод, использующий текущего пользователя
        public void EnforceEditCharacter(Guid userId, Guid characterId)
        {
            EnsureCurrentUserMatches(userId);
            EnforceEditCharacter(characterId);
        }
    }
}