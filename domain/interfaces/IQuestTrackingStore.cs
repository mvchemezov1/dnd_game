// domain/interfaces/IQuestTrackingStore.cs
namespace dnd_game.Domain.Interfaces;

/// <summary>
/// Хранилище связей между персонажами/предметами и активными квестами.
/// Используется для маршрутизации событий (CharacterDied, ItemAcquired) к соответствующим сагам.
/// </summary>
public interface IQuestTrackingStore
{
    /// <summary>
    /// Зарегистрировать участника (персонажа) в квесте.
    /// </summary>
    void AddParticipant(Guid questId, Guid characterId);

    /// <summary>
    /// Получить все квесты, в которых участвует персонаж.
    /// </summary>
    IEnumerable<Guid> GetQuestsForCharacter(Guid characterId);

    /// <summary>
    /// Получить все квесты, требующие определённый предмет.
    /// (Для ItemAcquired – если предмет связан с целями квеста).
    /// </summary>
    IEnumerable<Guid> GetQuestsForItem(string itemId);

    /// <summary>
    /// Удалить квест из отслеживания (при завершении/провале).
    /// </summary>
    void RemoveQuest(Guid questId);

    /// <summary>
    /// Зарегистрировать, что квест требует получения предмета.
    /// </summary>
    void AddRequiredItem(Guid questId, string itemId);

    /// <summary>
    /// Запомнить, в какой кампании находится квест — нужно, чтобы обработчики событий,
    /// не несущих CampaignId напрямую (например, CharacterDied), могли отправить команду
    /// вроде FailQuestCommand(CampaignId, QuestId), для которой CampaignId обязателен.
    /// </summary>
    void SetCampaign(Guid questId, Guid campaignId);

    /// <summary>
    /// Получить CampaignId квеста, ранее зарегистрированный через SetCampaign.
    /// Возвращает null, если квест не отслеживается (например, уже завершён/провален
    /// и удалён через RemoveQuest).
    /// </summary>
    Guid? GetCampaign(Guid questId);
}