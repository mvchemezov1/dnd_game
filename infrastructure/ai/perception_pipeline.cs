// infrastructure/ai/perception_pipeline.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Rules;
using dnd_game.Domain.ValueObjects;
using dnd_game.Application.Projections;
using dnd_game.Infrastructure.AI;
using dnd_game.Infrastructure.MessageBus;
using System.Linq;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Infrastructure.AI
{
    /// <summary>
    /// Тип чувства, используемого для восприятия.
    /// </summary>
    public enum SenseType
    {
        NormalVision,
        Darkvision,
        Blindsight,
        Tremorsense,
        Truesight,
        Hearing,
        Smell
    }

    /// <summary>
    /// Уровень освещённости.
    /// </summary>
    public enum LightLevel
    {
        Bright,
        Dim,
        Darkness
    }

    /// <summary>
    /// Результат восприятия конкретной сущности.
    /// </summary>
    public class PerceptionResult
    {
        public Guid EntityId { get; set; }
        public bool IsDetected { get; set; }
        public string DetectionMethod { get; set; } = string.Empty; // "sight", "hearing", "smell", "darkvision"
        public int PerceptionCheckResult { get; set; }
        public int StealthCheckResult { get; set; }
    }

    /// <summary>
    /// Конвейер восприятия, моделирующий правила DnD 5e для обнаружения существ.
    /// </summary>
    public class PerceptionPipeline
    {
        private readonly CharacterProjection _characterProjection;
        private readonly IBlackboardStore _blackboard;
        private readonly ICommandBus? _commandBus; // для публикации событий обнаружения
        private readonly IEventBus? _eventBus;

        // Константы D&D
        private const int NormalVisionRangeFeet = 1200; // практически без ограничений в ясную погоду
        private const int DimLightVisionRangeFeet = 60;
        private const int DarkvisionRangeFeet = 60;
        private const int BlindsightRangeFeet = 30;
        private const int TremorsenseRangeFeet = 60;
        private const int TruesightRangeFeet = 120;
        private const int HearingRangeFeet = 60; // можно услышать бой/разговор
        private const int SmellRangeFeet = 30;

        public PerceptionPipeline(
            CharacterProjection characterProjection,
            IBlackboardStore blackboard,
            ICommandBus? commandBus = null,
            IEventBus? eventBus = null)
        {
            _characterProjection = characterProjection;
            _blackboard = blackboard;
            _commandBus = commandBus;
            _eventBus = eventBus;
        }

        /// <summary>
        /// Получить список идентификаторов сущностей, видимых/слышимых для данного наблюдателя.
        /// </summary>
        public async Task<List<Guid>> GetVisibleEntities(Guid observerId)
        {
            var results = await PerceiveAllEntities(observerId);
            return results.Where(r => r.IsDetected).Select(r => r.EntityId).ToList();
        }

        /// <summary>
        /// Выполнить полное восприятие всех потенциальных целей вокруг наблюдателя.
        /// </summary>
        public async Task<List<PerceptionResult>> PerceiveAllEntities(Guid observerId)
        {
            var observer = await _characterProjection.GetById(observerId);
            if (observer == null) return new List<PerceptionResult>();

            // Получить всех потенциальных существ, о которых наблюдатель может иметь информацию
            // (в реальной системе – ограничиваем по региону или расстоянию)
            var allCharacters = await _characterProjection.GetAll();
            var results = new List<PerceptionResult>();

            // Собственные сенсорные способности
            var senses = GetSenses(observer);

            // Пассивная Внимательность наблюдателя
            int passivePerception = CalculatePassivePerception(observer);

            foreach (var target in allCharacters)
            {
                if (target.Id == observerId) continue; // себя не проверяем

                // Определить расстояние (упрощённо – используем позицию, если доступна)
                int distanceFeet = EstimateDistance(observerId, target.Id);

                // Получить информацию о скрытности цели (пассивная Скрытность или активная проверка)
                int targetStealth = await GetTargetStealth(target.Id);
                bool isInvisible = target.Conditions.Contains("Invisible");
                bool isHidden = await IsActivelyHiding(target.Id);

                PerceptionResult result = new PerceptionResult
                {
                    EntityId = target.Id,
                    StealthCheckResult = targetStealth
                };

                // Проверяем все чувства
                bool detected = false;
                string method = "";

                // 1. Зрение (нормальное) – зависит от освещения
                if (senses.Contains(SenseType.NormalVision) && !isInvisible)
                {
                    LightLevel light = GetLightLevelAt(observerId, target.Id);
                    if (light == LightLevel.Bright && distanceFeet <= NormalVisionRangeFeet)
                    {
                        detected = true;
                        method = "sight (bright light)";
                    }
                    else if (light == LightLevel.Dim && distanceFeet <= DimLightVisionRangeFeet)
                    {
                        // В тусклом свете – disadvantage на Perception (пассивная -5)
                        int effectivePassive = passivePerception - 5;
                        if (effectivePassive >= targetStealth && !isHidden)
                            detected = true;
                        method = "sight (dim light)";
                    }
                }

                // 2. Тёмное зрение (Darkvision) – видит в темноте как в тусклом свете
                if (!detected && senses.Contains(SenseType.Darkvision) && distanceFeet <= DarkvisionRangeFeet)
                {
                    LightLevel light = GetLightLevelAt(observerId, target.Id);
                    if (light == LightLevel.Darkness && !isInvisible)
                    {
                        // Темнота видна как тусклый свет: -5 к пассивной Внимательности
                        int effectivePassive = passivePerception - 5;
                        if (effectivePassive >= targetStealth && !isHidden)
                        {
                            detected = true;
                            method = "darkvision";
                        }
                    }
                }

                // 3. Истинное зрение (Truesight) – видит невидимое, игнорирует иллюзии
                if (!detected && senses.Contains(SenseType.Truesight) && distanceFeet <= TruesightRangeFeet)
                {
                    detected = true;
                    method = "truesight";
                }

                // 4. Слепое зрение (Blindsight) – не зависит от зрения
                if (!detected && senses.Contains(SenseType.Blindsight) && distanceFeet <= BlindsightRangeFeet)
                {
                    detected = true;
                    method = "blindsight";
                }

                // 5. Чувство вибрации (Tremorsense)
                if (!detected && senses.Contains(SenseType.Tremorsense) && distanceFeet <= TremorsenseRangeFeet)
                {
                    // Работает только если цель касается той же поверхности
                    bool sameSurface = await IsOnSameSurface(observerId, target.Id);
                    if (sameSurface)
                    {
                        detected = true;
                        method = "tremorsense";
                    }
                }

                // 6. Слух – слышимость с проверкой
                if (!detected && senses.Contains(SenseType.Hearing) && distanceFeet <= HearingRangeFeet)
                {
                    // Базовый DC для слышимости зависит от активности цели
                    int hearingDC = 10; // спокойное перемещение – DC 10
                    if (isHidden) hearingDC = targetStealth;
                    if (passivePerception >= hearingDC)
                    {
                        detected = true;
                        method = "hearing";
                    }
                }

                // 7. Обоняние (например, у волков)
                if (!detected && senses.Contains(SenseType.Smell) && distanceFeet <= SmellRangeFeet)
                {
                    // Можно почуять запах, если цель не перекрыла его
                    detected = true;
                    method = "smell";
                }

                result.IsDetected = detected;
                result.DetectionMethod = method;
                result.PerceptionCheckResult = passivePerception;

                // Обновляем Blackboard
                if (detected)
                {
                    await _blackboard.SetFact(observerId, $"Detected_{target.Id}", true, FactType.EntityState, expiration: TimeSpan.FromSeconds(30));
                    await _blackboard.SetFact(observerId, $"Target_{target.Id}_Distance", distanceFeet, FactType.Location, expiration: TimeSpan.FromSeconds(10));
                    // Публикуем событие обнаружения (если это новое обнаружение)
                    if (_eventBus != null && !await IsAlreadyDetected(observerId, target.Id))
                    {
                        await _eventBus.PublishAsync(new EntityDetectedEvent(observerId, target.Id, method));
                    }
                }
                else
                {
                    await _blackboard.RemoveFact(observerId, $"Detected_{target.Id}");
                }

                results.Add(result);
            }

            return results;
        }

        // --------------------------------------------------------------------------------
        // Вспомогательные методы
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Возвращает набор чувств, которыми обладает персонаж (по расе/классу/заклинаниям).
        /// </summary>
        private List<SenseType> GetSenses(CharacterDto character)
        {
            var senses = new List<SenseType> { SenseType.NormalVision, SenseType.Hearing };
            // Упрощённо: проверяем расу
            if (character.Race.Contains("Elf") || character.Race.Contains("Dwarf") ||
                character.Race.Contains("Gnome") || character.Race.Contains("Half-Orc") ||
                character.Race.Contains("Tiefling"))
            {
                senses.Add(SenseType.Darkvision);
            }
            // Можно добавить Blindsight/Truesight по заклинаниям или монстрам
            return senses;
        }

        /// <summary>
        /// Вычисляет пассивную Внимательность: 10 + модификатор Мудрости + бонус мастерства (если proficient).
        /// </summary>
        private int CalculatePassivePerception(CharacterDto character)
        {
            int wisMod = (character.AbilityScores.GetValueOrDefault("Wisdom", 10) - 10) / 2;
            bool proficient = character.SkillProficiencies.ContainsKey("Perception");
            int profBonus = proficient ? character.ProficiencyBonus : 0;
            // Если есть преимущество на проверки Внимательности – +5 (в системе это можно учесть через факты)
            return 10 + wisMod + profBonus;
        }

        /// <summary>
        /// Определить расстояние между двумя персонажами (упрощённо – через позиции или факты).
        /// </summary>
        private int EstimateDistance(Guid observerId, Guid targetId)
        {
            // Можно получать из Blackboard позиции или из отдельного сервиса. Здесь затычка.
            return 30; // 30 футов
        }

        /// <summary>
        /// Получить эффективное значение скрытности цели (пассивная Ловкость (Скрытность)).
        /// </summary>
        private async Task<int> GetTargetStealth(Guid targetId)
        {
            var target = await _characterProjection.GetById(targetId);
            if (target == null) return 10;
            int dexMod = (target.AbilityScores.GetValueOrDefault("Dexterity", 10) - 10) / 2;
            bool proficient = target.SkillProficiencies.ContainsKey("Stealth");
            int profBonus = proficient ? target.ProficiencyBonus : 0;
            return 10 + dexMod + profBonus; // пассивная скрытность
        }

        private Task<bool> IsActivelyHiding(Guid targetId)
        {
            // Проверяем факт на доске: скрывается ли активно (использовал действие Hide)
            return _blackboard.GetFact(targetId, "IsHiding").ContinueWith(t => t.Result?.Value as bool? ?? false);
        }

        private LightLevel GetLightLevelAt(Guid observerId, Guid targetId)
        {
            // Упрощение: получаем текущее время суток и погоду из кампании
            return LightLevel.Bright; // заглушка
        }

        private Task<bool> IsOnSameSurface(Guid observerId, Guid targetId)
        {
            return Task.FromResult(true); // упрощение
        }

        private async Task<bool> IsAlreadyDetected(Guid observerId, Guid targetId)
        {
            var fact = await _blackboard.GetFact(observerId, $"Detected_{targetId}");
            return fact != null;
        }
    }

    /// <summary>
    /// Событие: сущность обнаружена.
    /// </summary>
    public record EntityDetectedEvent(Guid ObserverId, Guid DetectedId, string Method) : IDomainEvent;
}