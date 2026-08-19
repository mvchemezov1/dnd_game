// shared_kernel/enums.cs
namespace dnd_game.SharedKernel
{
    // ────────────────────────────────────────────────────────────
    // Местность (расширено)
    // ────────────────────────────────────────────────────────────
    public enum Terrain
    {
        Normal,
        Difficult,
        Road,
        ShallowWater,
        DeepWater,
        Lava,
        Wall,
        Window,
        Door,
        HiddenDoor,
        Ice,
        Mud,
        Rubble,
        Thorns
    }

    // ────────────────────────────────────────────────────────────
    // Размер существа
    // ────────────────────────────────────────────────────────────
    public enum CreatureSize
    {
        Tiny,
        Small,
        Medium,
        Large,
        Huge,
        Gargantuan
    }

    // ────────────────────────────────────────────────────────────
    // Мировоззрение (Alignment)
    // ────────────────────────────────────────────────────────────
    public enum Alignment
    {
        LawfulGood,
        NeutralGood,
        ChaoticGood,
        LawfulNeutral,
        TrueNeutral,
        ChaoticNeutral,
        LawfulEvil,
        NeutralEvil,
        ChaoticEvil,
        Unaligned
    }

    // ────────────────────────────────────────────────────────────
    // Характеристики
    // ────────────────────────────────────────────────────────────
    public enum Ability
    {
        Strength,
        Dexterity,
        Constitution,
        Intelligence,
        Wisdom,
        Charisma
    }

    // ────────────────────────────────────────────────────────────
    // Навыки
    // ────────────────────────────────────────────────────────────
    public enum Skill
    {
        Acrobatics,
        AnimalHandling,
        Arcana,
        Athletics,
        Deception,
        History,
        Insight,
        Intimidation,
        Investigation,
        Medicine,
        Nature,
        Perception,
        Performance,
        Persuasion,
        Religion,
        SleightOfHand,
        Stealth,
        Survival
    }

    // ────────────────────────────────────────────────────────────
    // Типы урона
    // ────────────────────────────────────────────────────────────
    public enum DamageType
    {
        Bludgeoning,
        Piercing,
        Slashing,
        Fire,
        Cold,
        Lightning,
        Thunder,
        Acid,
        Poison,
        Radiant,
        Necrotic,
        Psychic,
        Force
    }

    // ────────────────────────────────────────────────────────────
    // Состояния (Conditions)
    // ────────────────────────────────────────────────────────────
    public enum Condition
    {
        Blinded,
        Charmed,
        Deafened,
        Frightened,
        Grappled,
        Incapacitated,
        Invisible,
        Paralyzed,
        Petrified,
        Poisoned,
        Prone,
        Restrained,
        Stunned,
        Unconscious,
        Exhaustion
    }

    // ────────────────────────────────────────────────────────────
    // Уровни усталости
    // ────────────────────────────────────────────────────────────
    public enum ExhaustionLevel
    {
        None = 0,
        One = 1,
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6
    }

    // ────────────────────────────────────────────────────────────
    // Типы действий в бою
    // ────────────────────────────────────────────────────────────
    public enum ActionType
    {
        Attack,
        CastSpell,
        Dash,
        Disengage,
        Dodge,
        Help,
        Hide,
        Ready,
        Search,
        UseObject,
        Shove,
        Grapple,
        Improvise
    }

    // ────────────────────────────────────────────────────────────
    // Типы отдыха
    // ────────────────────────────────────────────────────────────
    public enum RestType
    {
        Short,
        Long
    }

    // ────────────────────────────────────────────────────────────
    // Категории оружия
    // ────────────────────────────────────────────────────────────
    public enum WeaponCategory
    {
        SimpleMelee,
        SimpleRanged,
        MartialMelee,
        MartialRanged
    }

    // ────────────────────────────────────────────────────────────
    // Свойства оружия
    // ────────────────────────────────────────────────────────────
    [Flags]
    public enum WeaponProperty
    {
        None = 0,
        Ammunition = 1 << 0,
        Finesse = 1 << 1,
        Heavy = 1 << 2,
        Light = 1 << 3,
        Loading = 1 << 4,
        Range = 1 << 5,
        Reach = 1 << 6,
        Special = 1 << 7,
        Thrown = 1 << 8,
        TwoHanded = 1 << 9,
        Versatile = 1 << 10
    }

    // ────────────────────────────────────────────────────────────
    // Типы доспехов
    // ────────────────────────────────────────────────────────────
    public enum ArmorType
    {
        None,
        Light,
        Medium,
        Heavy,
        Shield
    }

    // ────────────────────────────────────────────────────────────
    // Школы магии
    // ────────────────────────────────────────────────────────────
    public enum MagicSchool
    {
        Abjuration,
        Conjuration,
        Divination,
        Enchantment,
        Evocation,
        Illusion,
        Necromancy,
        Transmutation
    }

    // ────────────────────────────────────────────────────────────
    // Время накладывания заклинания
    // ────────────────────────────────────────────────────────────
    public enum CastingTime
    {
        Action,
        BonusAction,
        Reaction,
        OneMinute,
        TenMinutes,
        OneHour,
        EightHours,
        TwentyFourHours,
        Special
    }

    // ────────────────────────────────────────────────────────────
    // Компоненты заклинания
    // ────────────────────────────────────────────────────────────
    [Flags]
    public enum SpellComponent
    {
        None = 0,
        Verbal = 1 << 0,
        Somatic = 1 << 1,
        Material = 1 << 2
    }

    // ────────────────────────────────────────────────────────────
    // Дальность заклинания
    // ────────────────────────────────────────────────────────────
    public enum SpellRangeType
    {
        Self,
        Touch,
        Ranged,
        Sight,
        Unlimited
    }

    // ────────────────────────────────────────────────────────────
    // Редкость предмета
    // ────────────────────────────────────────────────────────────
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        VeryRare,
        Legendary,
        Artifact
    }

    // ────────────────────────────────────────────────────────────
    // Тип магического предмета
    // ────────────────────────────────────────────────────────────
    public enum MagicItemType
    {
        Armor,
        Weapon,
        Potion,
        Ring,
        Rod,
        Scroll,
        Staff,
        Wand,
        WondrousItem
    }

    // ────────────────────────────────────────────────────────────
    // Валюта (типы монет)
    // ────────────────────────────────────────────────────────────
    public enum CoinType
    {
        Copper,
        Silver,
        Electrum,
        Gold,
        Platinum
    }

    // ────────────────────────────────────────────────────────────
    // Тип существа (для монстров)
    // ────────────────────────────────────────────────────────────
    public enum CreatureType
    {
        Aberration,
        Beast,
        Celestial,
        Construct,
        Dragon,
        Elemental,
        Fey,
        Fiend,
        Giant,
        Humanoid,
        Monstrosity,
        Ooze,
        Plant,
        Undead
    }

    // ────────────────────────────────────────────────────────────
    // Языки
    // ────────────────────────────────────────────────────────────
    public enum Language
    {
        Common,
        Dwarvish,
        Elvish,
        Giant,
        Gnomish,
        Goblin,
        Halfling,
        Orc,
        Abyssal,
        Celestial,
        Draconic,
        DeepSpeech,
        Infernal,
        Primordial,
        Sylvan,
        Undercommon
    }

    // ────────────────────────────────────────────────────────────
    // Чувства (senses)
    // ────────────────────────────────────────────────────────────
    public enum Sense
    {
        NormalVision,
        Darkvision,
        Blindsight,
        Tremorsense,
        Truesight
    }

    // ────────────────────────────────────────────────────────────
    // Статус квеста
    // ────────────────────────────────────────────────────────────
    public enum QuestStatus
    {
        Available,
        Active,
        Completed,
        Failed,
        Abandoned
    }

    // ────────────────────────────────────────────────────────────
    // Отношение фракции / NPC
    // ────────────────────────────────────────────────────────────
    public enum Attitude
    {
        Hostile,
        Unfriendly,
        Indifferent,
        Friendly,
        Helpful
    }

    // ────────────────────────────────────────────────────────────
    // Роль в кампании
    // ────────────────────────────────────────────────────────────
    public enum CampaignRole
    {
        Player,
        GameMaster,
        Spectator
    }

    // ────────────────────────────────────────────────────────────
    // Глобальная роль пользователя
    // ────────────────────────────────────────────────────────────
    public enum UserRole
    {
        Player,
        GameMaster,
        Admin
    }

    // ────────────────────────────────────────────────────────────
    // Тип интерактивного объекта
    // ────────────────────────────────────────────────────────────
    public enum InteractiveObjectType
    {
        Door,
        Chest,
        Lever,
        Button,
        Trap,
        Altar,
        Portal,
        Sign,
        Container,
        Campfire,
        Throne,
        Well,
        Statue,
        Bookcase,
        HiddenPassage
    }

    // ────────────────────────────────────────────────────────────
    // Тип освещения клетки (отдельно от Terrain, если нужно)
    // ────────────────────────────────────────────────────────────
    public enum LightLevel
    {
        Darkness,
        Dim,
        Bright
    }

    // ────────────────────────────────────────────────────────────
    // Тип укрытия
    // ────────────────────────────────────────────────────────────
    public enum CoverType
    {
        None,
        HalfCover,
        ThreeQuartersCover,
        FullCover
    }
}