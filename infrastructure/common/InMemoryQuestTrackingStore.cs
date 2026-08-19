// infrastructure/common/InMemoryQuestTrackingStore.cs
using System.Collections.Concurrent;
using dnd_game.Domain.Interfaces;

namespace dnd_game.Infrastructure.Common;

public class InMemoryQuestTrackingStore : IQuestTrackingStore
{
    // questId -> список characterId
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _questParticipants = new();
    // characterId -> список questId
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _participantQuests = new();
    // questId -> список itemId
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _questRequiredItems = new();
    // itemId -> список questId
    private readonly ConcurrentDictionary<string, HashSet<Guid>> _itemQuests = new();
    // questId -> campaignId (для событий вроде CharacterDied, не несущих CampaignId напрямую)
    private readonly ConcurrentDictionary<Guid, Guid> _questCampaigns = new();

    public void AddParticipant(Guid questId, Guid characterId)
    {
        _questParticipants.AddOrUpdate(questId,
            _ => new HashSet<Guid> { characterId },
            (_, set) => { set.Add(characterId); return set; });

        _participantQuests.AddOrUpdate(characterId,
            _ => new HashSet<Guid> { questId },
            (_, set) => { set.Add(questId); return set; });
    }

    public IEnumerable<Guid> GetQuestsForCharacter(Guid characterId)
    {
        return _participantQuests.TryGetValue(characterId, out var set) ? set : Enumerable.Empty<Guid>();
    }

    public IEnumerable<Guid> GetQuestsForItem(string itemId)
    {
        return _itemQuests.TryGetValue(itemId, out var set) ? set : Enumerable.Empty<Guid>();
    }

    public void RemoveQuest(Guid questId)
    {
        // Удаляем из всех словарей
        if (_questParticipants.TryRemove(questId, out var participants))
        {
            foreach (var cid in participants)
            {
                if (_participantQuests.TryGetValue(cid, out var quests))
                {
                    quests.Remove(questId);
                    if (quests.Count == 0) _participantQuests.TryRemove(cid, out _);
                }
            }
        }

        if (_questRequiredItems.TryRemove(questId, out var items))
        {
            foreach (var itemId in items)
            {
                if (_itemQuests.TryGetValue(itemId, out var quests))
                {
                    quests.Remove(questId);
                    if (quests.Count == 0) _itemQuests.TryRemove(itemId, out _);
                }
            }
        }

        _questCampaigns.TryRemove(questId, out _);
    }

    public void AddRequiredItem(Guid questId, string itemId)
    {
        _questRequiredItems.AddOrUpdate(questId,
            _ => new HashSet<string> { itemId },
            (_, set) => { set.Add(itemId); return set; });

        _itemQuests.AddOrUpdate(itemId,
            _ => new HashSet<Guid> { questId },
            (_, set) => { set.Add(questId); return set; });
    }

    public void SetCampaign(Guid questId, Guid campaignId)
    {
        _questCampaigns[questId] = campaignId;
    }

    public Guid? GetCampaign(Guid questId)
    {
        return _questCampaigns.TryGetValue(questId, out var campaignId) ? campaignId : null;
    }
}