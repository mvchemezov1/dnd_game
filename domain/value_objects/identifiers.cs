// domain/value_objects/identifiers.cs
namespace dnd_game.domain.value_objects
{
    /// <summary>
    /// Базовый идентификатор агрегата (общего назначения).
    /// </summary>
    public record AggregateId(Guid Value)
    {
        public static readonly AggregateId Empty = new(Guid.Empty);
        public override string ToString() => Value.ToString();
    }

    // ---------- Идентификаторы персонажей и игроков ----------

    public record CharacterId(Guid Value)
    {
        public static readonly CharacterId Empty = new(Guid.Empty);
        public static implicit operator Guid(CharacterId id) => id.Value;
        public static implicit operator CharacterId(Guid id) => new(id);
        public override string ToString() => $"Character({Value})";
    }

    public record PlayerId(Guid Value)
    {
        public static readonly PlayerId Empty = new(Guid.Empty);
        public override string ToString() => $"Player({Value})";
    }

    public record NpcId(Guid Value)
    {
        public static readonly NpcId Empty = new(Guid.Empty);
        public override string ToString() => $"NPC({Value})";
    }

    // ---------- Идентификаторы игровых сессий и кампаний ----------

    public record CampaignId(Guid Value)
    {
        public static readonly CampaignId Empty = new(Guid.Empty);
        public override string ToString() => $"Campaign({Value})";
    }

    public record GameSessionId(Guid Value)
    {
        public static readonly GameSessionId Empty = new(Guid.Empty);
        public override string ToString() => $"Session({Value})";
    }

    // ---------- Боевые идентификаторы ----------

    public record CombatId(Guid Value)
    {
        public static readonly CombatId Empty = new(Guid.Empty);
        public override string ToString() => $"Combat({Value})";
    }

    // ---------- Квесты ----------

    public record QuestId(Guid Value)
    {
        public static readonly QuestId Empty = new(Guid.Empty);
        public override string ToString() => $"Quest({Value})";
    }

    // ---------- Предметы, заклинания, черты (строковые ключи из игровых данных) ----------

    public record ItemId(string Value)
    {
        public static readonly ItemId Empty = new(string.Empty);
        public override string ToString() => $"Item({Value})";
    }

    public record SpellId(string Value)
    {
        public static readonly SpellId Empty = new(string.Empty);
        public override string ToString() => $"Spell({Value})";
    }

    public record FeatId(string Value)
    {
        public static readonly FeatId Empty = new(string.Empty);
        public override string ToString() => $"Feat({Value})";
    }

    // ---------- Фракции ----------

    public record FactionId(string Value)
    {
        public static readonly FactionId Empty = new(string.Empty);
        public override string ToString() => $"Faction({Value})";
    }

    // ---------- Диалоги ----------

    public record DialogueId(Guid Value)
    {
        public static readonly DialogueId Empty = new(Guid.Empty);
        public override string ToString() => $"Dialogue({Value})";
    }

    // ---------- Рецепты крафта ----------

    public record RecipeId(Guid Value)
    {
        public static readonly RecipeId Empty = new(Guid.Empty);
        public override string ToString() => $"Recipe({Value})";
    }

    // ---------- Торговые предложения ----------

    public record TradeOfferId(Guid Value)
    {
        public static readonly TradeOfferId Empty = new(Guid.Empty);
        public override string ToString() => $"TradeOffer({Value})";
    }

    // ---------- Характеристики и навыки (строковые константы) ----------

    /// <summary>
    /// Идентификатор характеристики (Strength, Dexterity, etc.).
    /// </summary>
    public record AbilityId(string Value)
    {
        public static readonly AbilityId Strength = new("Strength");
        public static readonly AbilityId Dexterity = new("Dexterity");
        public static readonly AbilityId Constitution = new("Constitution");
        public static readonly AbilityId Intelligence = new("Intelligence");
        public static readonly AbilityId Wisdom = new("Wisdom");
        public static readonly AbilityId Charisma = new("Charisma");

        public override string ToString() => Value;
    }

    /// <summary>
    /// Идентификатор навыка (Acrobatics, Athletics, etc.).
    /// </summary>
    public record SkillId(string Value)
    {
        public static readonly SkillId Acrobatics = new("Acrobatics");
        public static readonly SkillId AnimalHandling = new("Animal Handling");
        public static readonly SkillId Arcana = new("Arcana");
        public static readonly SkillId Athletics = new("Athletics");
        public static readonly SkillId Deception = new("Deception");
        public static readonly SkillId History = new("History");
        public static readonly SkillId Insight = new("Insight");
        public static readonly SkillId Intimidation = new("Intimidation");
        public static readonly SkillId Investigation = new("Investigation");
        public static readonly SkillId Medicine = new("Medicine");
        public static readonly SkillId Nature = new("Nature");
        public static readonly SkillId Perception = new("Perception");
        public static readonly SkillId Performance = new("Performance");
        public static readonly SkillId Persuasion = new("Persuasion");
        public static readonly SkillId Religion = new("Religion");
        public static readonly SkillId SleightOfHand = new("Sleight of Hand");
        public static readonly SkillId Stealth = new("Stealth");
        public static readonly SkillId Survival = new("Survival");

        public override string ToString() => Value;
    }

    /// <summary>
    /// Идентификатор состояния (Blinded, Charmed, etc.).
    /// </summary>
    public record ConditionId(string Value)
    {
        public static readonly ConditionId Blinded = new("Blinded");
        public static readonly ConditionId Charmed = new("Charmed");
        public static readonly ConditionId Deafened = new("Deafened");
        public static readonly ConditionId Frightened = new("Frightened");
        public static readonly ConditionId Grappled = new("Grappled");
        public static readonly ConditionId Incapacitated = new("Incapacitated");
        public static readonly ConditionId Invisible = new("Invisible");
        public static readonly ConditionId Paralyzed = new("Paralyzed");
        public static readonly ConditionId Petrified = new("Petrified");
        public static readonly ConditionId Poisoned = new("Poisoned");
        public static readonly ConditionId Prone = new("Prone");
        public static readonly ConditionId Restrained = new("Restrained");
        public static readonly ConditionId Stunned = new("Stunned");
        public static readonly ConditionId Unconscious = new("Unconscious");
        public static readonly ConditionId Exhaustion = new("Exhaustion");

        public override string ToString() => Value;
    }
}