// application/projections/materialized_views/player_overview.cs
namespace dnd_game.application.projections.materialized_views
{
    /// <summary>
    /// Обзорная информация об игроке и его персонажах.
    /// Представляет собой DTO read-модели, используемое для отображения в пользовательском интерфейсе.
    /// </summary>
    public class PlayerOverview
    {
        /// <summary>Идентификатор игрока.</summary>
        public Guid PlayerId { get; set; }

        /// <summary>Отображаемое имя игрока.</summary>
        public string PlayerName { get; set; } = string.Empty;

        /// <summary>Сводка по каждому персонажу, принадлежащему игроку.</summary>
        public List<CharacterSummary> Characters { get; set; } = [];
    }

    /// <summary>
    /// Краткая сводка о персонаже для списка персонажей игрока.
    /// Содержит основные характеристики, необходимые для быстрого ознакомления.
    /// </summary>
    public class CharacterSummary
    {
        /// <summary>Идентификатор персонажа.</summary>
        public Guid CharacterId { get; set; }

        /// <summary>Имя персонажа.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Текущий уровень.</summary>
        public int Level { get; set; }

        /// <summary>Класс персонажа.</summary>
        public string Class { get; set; } = string.Empty;

        /// <summary>Раса персонажа.</summary>
        public string Race { get; set; } = string.Empty;

        /// <summary>Предыстория (background).</summary>
        public string Background { get; set; } = string.Empty;

        /// <summary>Текущие хиты.</summary>
        public int CurrentHitPoints { get; set; }

        /// <summary>Максимальные хиты.</summary>
        public int MaxHitPoints { get; set; }

        /// <summary>Временные хиты.</summary>
        public int TemporaryHitPoints { get; set; }

        /// <summary>Жив ли персонаж.</summary>
        public bool IsAlive { get; set; }

        /// <summary>Класс брони.</summary>
        public int ArmorClass { get; set; }

        /// <summary>Скорость передвижения в футах.</summary>
        public int Speed { get; set; }

        /// <summary>Накопленный опыт.</summary>
        public int ExperiencePoints { get; set; }

        /// <summary>Опыт, необходимый для следующего уровня.</summary>
        public int ExperienceToNextLevel { get; set; }

        /// <summary>Бонус мастерства.</summary>
        public int ProficiencyBonus { get; set; }

        /// <summary>Активные состояния (строка для краткого отображения).</summary>
        public string ConditionsSummary { get; set; } = string.Empty;

        /// <summary>Успешные спасброски от смерти.</summary>
        public int DeathSaveSuccesses { get; set; }

        /// <summary>Проваленные спасброски от смерти.</summary>
        public int DeathSaveFailures { get; set; }

        /// <summary>Стабилизирован ли персонаж (при 0 хитов).</summary>
        public bool IsStable { get; set; }

        /// <summary>Краткое перечисление активных квестов персонажа.</summary>
        public List<string> ActiveQuestNames { get; set; } = [];

        /// <summary>Идентификатор текущей кампании, если есть.</summary>
        public Guid? CampaignId { get; set; }
    }
}