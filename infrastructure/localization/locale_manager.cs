// infrastructure/localization/locale_manager.cs
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace dnd_game.Infrastructure.Localization
{
    /// <summary>
    /// Интерфейс менеджера локализации для игры DnD.
    /// </summary>
    public interface ILocaleManager
    {
        /// <summary>
        /// Получить строку по ключу для текущей локали.
        /// </summary>
        string GetString(string key);

        /// <summary>
        /// Получить строку по ключу для заданной локали.
        /// </summary>
        string GetString(string key, string locale);

        /// <summary>
        /// Получить строку с подстановкой параметров.
        /// </summary>
        string Format(string key, params object[] args);

        /// <summary>
        /// Получить строку с учётом множественного числа.
        /// </summary>
        string Pluralize(string key, int count, params object[] args);

        /// <summary>
        /// Установить текущую локаль.
        /// </summary>
        void SetLocale(string locale);

        /// <summary>
        /// Текущая локаль.
        /// </summary>
        string CurrentLocale { get; }
    }

    /// <summary>
    /// Провайдер переводов для загрузки локализационных файлов.
    /// </summary>
    public interface ILocaleProvider
    {
        Task<Dictionary<string, string>> LoadTranslationsAsync(string locale);
    }

    /// <summary>
    /// Локализационные константы для распространённых игровых ключей.
    /// </summary>
    public static class LocaleKeys
    {
        public const string CharacterStrength = "character.strength";
        public const string CharacterDexterity = "character.dexterity";
        public const string CharacterConstitution = "character.constitution";
        public const string CharacterIntelligence = "character.intelligence";
        public const string CharacterWisdom = "character.wisdom";
        public const string CharacterCharisma = "character.charisma";
        public const string ActionAttack = "action.attack";
        public const string ActionDash = "action.dash";
        public const string ActionDisengage = "action.disengage";
        public const string ActionDodge = "action.dodge";
        public const string ActionHelp = "action.help";
        public const string ActionHide = "action.hide";
        public const string ActionReady = "action.ready";
        public const string ActionSearch = "action.search";
        public const string ActionUseObject = "action.use_object";
        public const string ConditionBlinded = "condition.blinded";
        public const string ConditionCharmed = "condition.charmed";
        public const string ConditionDeafened = "condition.deafened";
        public const string ConditionFrightened = "condition.frightened";
        public const string ConditionGrappled = "condition.grappled";
        public const string ConditionIncapacitated = "condition.incapacitated";
        public const string ConditionInvisible = "condition.invisible";
        public const string ConditionParalyzed = "condition.paralyzed";
        public const string ConditionPetrified = "condition.petrified";
        public const string ConditionPoisoned = "condition.poisoned";
        public const string ConditionProne = "condition.prone";
        public const string ConditionRestrained = "condition.restrained";
        public const string ConditionStunned = "condition.stunned";
        public const string ConditionUnconscious = "condition.unconscious";
        public const string ConditionExhaustion = "condition.exhaustion";
        public const string DamageTypeBludgeoning = "damage.bludgeoning";
        public const string DamageTypePiercing = "damage.piercing";
        public const string DamageTypeSlashing = "damage.slashing";
        public const string DamageTypeFire = "damage.fire";
        public const string DamageTypeCold = "damage.cold";
        public const string DamageTypeLightning = "damage.lightning";
        public const string DamageTypeThunder = "damage.thunder";
        public const string DamageTypeAcid = "damage.acid";
        public const string DamageTypePoison = "damage.poison";
        public const string DamageTypeRadiant = "damage.radiant";
        public const string DamageTypeNecrotic = "damage.necrotic";
        public const string DamageTypePsychic = "damage.psychic";
        public const string DamageTypeForce = "damage.force";
        public const string SkillAcrobatics = "skill.acrobatics";
        public const string SkillAnimalHandling = "skill.animal_handling";
        public const string SkillArcana = "skill.arcana";
        public const string SkillAthletics = "skill.athletics";
        public const string SkillDeception = "skill.deception";
        public const string SkillHistory = "skill.history";
        public const string SkillInsight = "skill.insight";
        public const string SkillIntimidation = "skill.intimidation";
        public const string SkillInvestigation = "skill.investigation";
        public const string SkillMedicine = "skill.medicine";
        public const string SkillNature = "skill.nature";
        public const string SkillPerception = "skill.perception";
        public const string SkillPerformance = "skill.performance";
        public const string SkillPersuasion = "skill.persuasion";
        public const string SkillReligion = "skill.religion";
        public const string SkillSleightOfHand = "skill.sleight_of_hand";
        public const string SkillStealth = "skill.stealth";
        public const string SkillSurvival = "skill.survival";
    }

    /// <summary>
    /// Основная реализация менеджера локализации.
    /// </summary>
    public class LocaleManager : ILocaleManager
    {
        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _translations = new();
        private readonly ILocaleProvider _provider;
        private string _currentLocale = "en";

        public string CurrentLocale => _currentLocale;

        public LocaleManager(ILocaleProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            // Предзагрузка дефолтной локали
            LoadLocaleAsync(_currentLocale).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Установить текущую локаль и загрузить переводы при необходимости.
        /// </summary>
        public void SetLocale(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
                throw new ArgumentException("Locale cannot be empty.");
            _currentLocale = locale.ToLowerInvariant();
            LoadLocaleAsync(_currentLocale).GetAwaiter().GetResult();
        }

        public string GetString(string key) => GetString(key, _currentLocale);

        public string GetString(string key, string locale)
        {
            if (string.IsNullOrEmpty(key)) return key;
            locale = locale?.ToLowerInvariant() ?? _currentLocale;

            if (_translations.TryGetValue(locale, out var dict))
            {
                if (dict.TryGetValue(key, out var value))
                    return value;
                // Fallback к английскому, если ключ не найден
                if (locale != "en" && _translations.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enValue))
                    return enValue;
            }
            // Абсолютный fallback: возвращаем сам ключ
            return key;
        }

        public string Format(string key, params object[] args)
        {
            var template = GetString(key);
            return args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, template, args) : template;
        }

        public string Pluralize(string key, int count, params object[] args)
        {
            string pluralKey;
            if (count == 1)
                pluralKey = $"{key}.one";
            else
                pluralKey = $"{key}.other";

            var template = GetString(pluralKey);
            // Если конкретная форма не найдена, используем базовый ключ
            if (template == pluralKey)
                template = GetString(key);

            var allArgs = new object[args.Length + 1];
            allArgs[0] = count;
            Array.Copy(args, 0, allArgs, 1, args.Length);
            return string.Format(CultureInfo.InvariantCulture, template, allArgs);
        }

        /// <summary>
        /// Принудительно загрузить переводы для указанной локали.
        /// </summary>
        private async Task LoadLocaleAsync(string locale)
        {
            if (!_translations.ContainsKey(locale))
            {
                try
                {
                    var dict = await _provider.LoadTranslationsAsync(locale);
                    _translations[locale] = dict ?? new Dictionary<string, string>();
                }
                catch
                {
                    // Если загрузка не удалась, оставляем пустой словарь, чтобы не пытаться снова немедленно
                    _translations[locale] = new Dictionary<string, string>();
                }
            }
        }
    }

    /// <summary>
    /// Простой провайдер переводов из JSON-файлов (для демонстрации).
    /// </summary>
    public class JsonFileLocaleProvider : ILocaleProvider
    {
        private readonly string _resourcesPath;

        public JsonFileLocaleProvider(string resourcesPath)
        {
            _resourcesPath = resourcesPath;
        }

        public async Task<Dictionary<string, string>> LoadTranslationsAsync(string locale)
        {
            var filePath = Path.Combine(_resourcesPath, $"{locale}.json");
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Translation file not found: {filePath}");

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
    }
}