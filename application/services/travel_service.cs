// application/services/travel_service.cs
using dnd_game.Domain.Commands;
using dnd_game.Application.Projections;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Application.Services
{
    /// <summary>
    /// Темп путешествия (Speed Pace). Определяет скорость и возможные модификаторы.
    /// </summary>
    public enum TravelPace
    {
        /// <summary>Медленный темп: возможность скрытности, -5 к пассивной Внимательности для обнаружения угроз.</summary>
        Slow,

        /// <summary>Обычный темп: стандартное перемещение без модификаторов.</summary>
        Normal,

        /// <summary>Быстрый темп: штраф -5 к пассивной Внимательности, скрытность невозможна.</summary>
        Fast
    }

    /// <summary>
    /// Тип местности (Terrain Type) для расчёта стоимости и сложности путешествия.
    /// </summary>
    public enum TerrainType
    {
        /// <summary>Дорога.</summary>
        Road,
        /// <summary>Равнина.</summary>
        Plain,
        /// <summary>Лес.</summary>
        Forest,
        /// <summary>Холмы.</summary>
        Hill,
        /// <summary>Горы.</summary>
        Mountain,
        /// <summary>Болото.</summary>
        Swamp,
        /// <summary>Пустыня.</summary>
        Desert,
        /// <summary>Тундра.</summary>
        Tundra,
        /// <summary>Водная поверхность.</summary>
        Water,
        /// <summary>Воздушное пространство.</summary>
        Air
    }

    /// <summary>
    /// Сервис, управляющий путешествиями и перемещениями по глобальной карте,
    /// а также локальным перемещением персонажей в рамках тактической сцены.
    /// </summary>
    /// <remarks>
    /// Предоставляет методы для отправки соответствующих команд через шину команд.
    /// Бизнес-логика расчёта расстояний, стоимости пути и случайных событий находится
    /// в обработчиках команд и агрегатах.
    /// </remarks>
    public class TravelService(ICommandBus commandBus, CharacterProjection characterProjection)
    {
        // --------------------------------------------------------------------------------------------
        // Локальное перемещение (в рамках тактической карты или текущей сцены)
        // --------------------------------------------------------------------------------------------

        /// <summary>
        /// Переместить персонажа на тактической карте в указанную позицию.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <param name="targetX">Координата X целевой позиции.</param>
        /// <param name="targetY">Координата Y целевой позиции.</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task MoveCharacter(Guid characterId, int targetX, int targetY)
        {
            await commandBus.SendAsync(new MoveCharacterToPosition(characterId, targetX, targetY, "Walk"));
        }

        /// <summary>
        /// Использовать действие «Рывок» (Dash) для удвоения скорости на текущий ход.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task Dash(Guid characterId)
        {
            await commandBus.SendAsync(new MoveCharacterWithDash(characterId));
        }

        /// <summary>
        /// Выполнить перемещение специальным типом (Climb, Swim, Fly, Burrow).
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <param name="distanceFeet">Дистанция перемещения в футах.</param>
        /// <param name="movementType">Тип перемещения (Climb, Swim, Fly, Burrow).</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task SpecialMovement(Guid characterId, int distanceFeet, string movementType)
        {
            switch (movementType)
            {
                case "Climb":
                    await commandBus.SendAsync(new ClimbCharacter(characterId, distanceFeet, 0));
                    break;
                case "Swim":
                    await commandBus.SendAsync(new SwimCharacter(characterId, distanceFeet, 0));
                    break;
                case "Fly":
                    await commandBus.SendAsync(new FlyCharacter(characterId, distanceFeet, 0));
                    break;
                case "Burrow":
                    await commandBus.SendAsync(new BurrowCharacter(characterId, distanceFeet, 0));
                    break;
                default:
                    // Для неизвестного типа используем стандартное перемещение с указанным типом
                    await commandBus.SendAsync(new MoveCharacterToPosition(characterId, distanceFeet, 0, movementType));
                    break;
            }
        }

        /// <summary>
        /// Персонаж пытается скрыться (Hide).
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task Hide(Guid characterId)
        {
            await commandBus.SendAsync(new MoveCharacterStealthily(characterId));
        }

        /// <summary>
        /// Персонаж выполняет прыжок.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <param name="jumpType">Тип прыжка (например, "Long" или "High").</param>
        /// <param name="strengthScore">Значение силы персонажа для расчёта дальности.</param>
        /// <param name="runningStart">Был ли разбег перед прыжком.</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task Jump(Guid characterId, string jumpType, int strengthScore, bool runningStart)
        {
            await commandBus.SendAsync(new JumpCharacter(characterId, jumpType, strengthScore, runningStart));
        }

        // --------------------------------------------------------------------------------------------
        // Глобальное путешествие (overland travel)
        // --------------------------------------------------------------------------------------------

        /// <summary>
        /// Начать путешествие группы по глобальной карте по заданному маршруту.
        /// </summary>
        /// <param name="partyId">Идентификатор группы (партии).</param>
        /// <param name="routeId">Идентификатор маршрута.</param>
        /// <param name="pace">Темп путешествия.</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task StartJourney(Guid partyId, Guid routeId, TravelPace pace)
        {
            await commandBus.SendAsync(new StartJourneyCommand(partyId, routeId, pace.ToString()));
        }

        /// <summary>
        /// Завершить путешествие (прибытие на место или прерывание).
        /// </summary>
        /// <param name="partyId">Идентификатор группы.</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task EndJourney(Guid partyId)
        {
            await commandBus.SendAsync(new EndJourneyCommand(partyId));
        }

        /// <summary>
        /// Пройти один день пути (daily progress) с учётом местности и навигации.
        /// </summary>
        /// <param name="partyId">Идентификатор группы.</param>
        /// <param name="terrain">Тип местности.</param>
        /// <param name="hoursTraveled">Количество часов в пути.</param>
        /// <param name="navigationCheckResult">Результат проверки навигации (по умолчанию 10).</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task TravelDay(Guid partyId, TerrainType terrain, int hoursTraveled, int navigationCheckResult = 10)
        {
            await commandBus.SendAsync(new TravelDayCommand(partyId, terrain.ToString(), hoursTraveled, navigationCheckResult));
        }

        /// <summary>
        /// Установить темп путешествия для группы.
        /// </summary>
        /// <param name="partyId">Идентификатор группы.</param>
        /// <param name="pace">Новый темп путешествия.</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task SetPace(Guid partyId, TravelPace pace)
        {
            await commandBus.SendAsync(new SetTravelPaceCommand(partyId, pace.ToString()));
        }

        /// <summary>
        /// Совершить марш-бросок (forced march) — путешествие сверх 8 часов, грозящее истощением.
        /// </summary>
        /// <param name="partyId">Идентификатор группы.</param>
        /// <param name="additionalHours">Дополнительные часы пути.</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task ForcedMarch(Guid partyId, int additionalHours)
        {
            await commandBus.SendAsync(new ForcedMarchCommand(partyId, additionalHours));
        }

        /// <summary>
        /// Сделать проверку Навигации (Survival) для определения, не сбилась ли группа с пути.
        /// </summary>
        /// <param name="partyId">Идентификатор группы.</param>
        /// <param name="survivalCheckRoll">Результат броска d20.</param>
        /// <param name="wisdomModifier">Модификатор мудрости персонажа, выполняющего проверку.</param>
        /// <param name="isProficient">Владеет ли персонаж навыком Выживание.</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task Navigate(Guid partyId, int survivalCheckRoll, int wisdomModifier, bool isProficient)
        {
            await commandBus.SendAsync(new NavigationCheckCommand(partyId, survivalCheckRoll, wisdomModifier, isProficient));
        }

        /// <summary>
        /// Пометить группу как заблудившуюся (автоматический вызов при провале навигации).
        /// </summary>
        /// <param name="partyId">Идентификатор группы.</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task BecomeLost(Guid partyId)
        {
            await commandBus.SendAsync(new PartyLostCommand(partyId));
        }

        /// <summary>
        /// Потребить провизию и воду за указанное количество дней.
        /// </summary>
        /// <param name="partyId">Идентификатор группы.</param>
        /// <param name="days">Количество дней.</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task ConsumeResources(Guid partyId, int days)
        {
            await commandBus.SendAsync(new ConsumeResourcesCommand(partyId, days));
        }

        /// <summary>
        /// Инициировать проверку случайной встречи на указанной местности.
        /// </summary>
        /// <param name="partyId">Идентификатор группы.</param>
        /// <param name="terrain">Тип местности.</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task CheckRandomEncounter(Guid partyId, TerrainType terrain)
        {
            await commandBus.SendAsync(new RandomEncounterCheckCommand(partyId, terrain.ToString()));
        }

        /// <summary>
        /// Применить усталость (Exhaustion) членам группы из-за долгого путешествия или нехватки ресурсов.
        /// </summary>
        /// <param name="partyId">Идентификатор группы.</param>
        /// <param name="exhaustionLevel">Уровень усталости (0-5).</param>
        /// <returns>Задача, представляющая асинхронную отправку команды.</returns>
        public async Task ApplyExhaustion(Guid partyId, int exhaustionLevel)
        {
            await commandBus.SendAsync(new ApplyExhaustionCommand(partyId, exhaustionLevel));
        }

        // --------------------------------------------------------------------------------------------
        // Вспомогательные методы получения информации (могут использоваться UI)
        // --------------------------------------------------------------------------------------------

        /// <summary>
        /// Получить базовую скорость персонажа в футах.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>Скорость персонажа или 30, если персонаж не найден.</returns>
        public async Task<int> GetCharacterSpeed(Guid characterId)
        {
            var character = await characterProjection.GetById(characterId);
            return character?.Speed ?? 30;
        }
    }
}