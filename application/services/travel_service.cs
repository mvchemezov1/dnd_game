// application/services/travel_service.cs
using dnd_game.Domain.Commands;
using dnd_game.Application.Projections;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Application.Services
{
    /// <summary>
    /// Темп путешествия (Speed Pace).
    /// </summary>
    public enum TravelPace
    {
        Slow,    // возможность скрытности, -5 к пассивной Внимательности для обнаружения угроз
        Normal,  // стандартное перемещение
        Fast     // штраф -5 к пассивной Внимательности, нельзя скрытно
    }

    /// <summary>
    /// Тип местности (Terrain Type).
    /// </summary>
    public enum TerrainType
    {
        Road,
        Plain,
        Forest,
        Hill,
        Mountain,
        Swamp,
        Desert,
        Tundra,
        Water,
        Air
    }

    /// <summary>
    /// Сервис, управляющий путешествиями и перемещениями по глобальной карте.
    /// </summary>
    public class TravelService(ICommandBus commandBus, CharacterProjection characterProjection)
    {

        // --------------------------------------------------------------------------------------------
        // Локальное перемещение (в рамках тактической карты или текущей сцены)
        // --------------------------------------------------------------------------------------------

        /// <summary>
        /// Переместить персонажа на тактической карте (в футах).
        /// </summary>
        public async Task MoveCharacter(Guid characterId, int targetX, int targetY)
        {
            await commandBus.SendAsync(new MoveCharacterToPosition(characterId, targetX, targetY, "Walk"));
        }

        /// <summary>
        /// Использовать действие Dash (удвоение скорости на текущий ход).
        /// </summary>
        public async Task Dash(Guid characterId)
        {
            await commandBus.SendAsync(new MoveCharacterWithDash(characterId));
        }

        /// <summary>
        /// Перемещение специальным типом (Climb, Swim, Fly, Burrow).
        /// </summary>
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
                    await commandBus.SendAsync(new MoveCharacterToPosition(characterId, distanceFeet, 0, movementType));
                    break;
            }
        }

        /// <summary>
        /// Персонаж пытается скрыться (Hide).
        /// </summary>
        public async Task Hide(Guid characterId)
        {
            await commandBus.SendAsync(new MoveCharacterStealthily(characterId));
        }

        /// <summary>
        /// Персонаж выполняет прыжок.
        /// </summary>
        public async Task Jump(Guid characterId, string jumpType, int strengthScore, bool runningStart)
        {
            await commandBus.SendAsync(new JumpCharacter(characterId, jumpType, strengthScore, runningStart));
        }

        // --------------------------------------------------------------------------------------------
        // Глобальное путешествие (overland travel)
        // --------------------------------------------------------------------------------------------

        /// <summary>
        /// Начать путешествие группы по глобальной карте.
        /// </summary>
        public async Task StartJourney(Guid partyId, Guid routeId, TravelPace pace)
        {
            await commandBus.SendAsync(new StartJourneyCommand(partyId, routeId, pace.ToString()));
        }

        /// <summary>
        /// Завершить путешествие (прибыли на место или прервали).
        /// </summary>
        public async Task EndJourney(Guid partyId)
        {
            await commandBus.SendAsync(new EndJourneyCommand(partyId));
        }

        /// <summary>
        /// Пройти один день пути (daily progress).
        /// </summary>
        public async Task TravelDay(Guid partyId, TerrainType terrain, int hoursTraveled, int navigationCheckResult = 10)
        {
            await commandBus.SendAsync(new TravelDayCommand(partyId, terrain.ToString(), hoursTraveled, navigationCheckResult));
        }

        /// <summary>
        /// Установить темп путешествия.
        /// </summary>
        public async Task SetPace(Guid partyId, TravelPace pace)
        {
            await commandBus.SendAsync(new SetTravelPaceCommand(partyId, pace.ToString()));
        }

        /// <summary>
        /// Совершить марш-бросок (forced march) – путешествие сверх 8 часов, грозит истощением.
        /// </summary>
        public async Task ForcedMarch(Guid partyId, int additionalHours)
        {
            await commandBus.SendAsync(new ForcedMarchCommand(partyId, additionalHours));
        }

        /// <summary>
        /// Сделать проверку Навигации (Survival) для определения, не сбилась ли группа с пути.
        /// </summary>
        public async Task Navigate(Guid partyId, int survivalCheckRoll, int wisdomModifier, bool isProficient)
        {
            await commandBus.SendAsync(new NavigationCheckCommand(partyId, survivalCheckRoll, wisdomModifier, isProficient));
        }

        /// <summary>
        /// Группа теряется (автоматический вызов при провале навигации).
        /// </summary>
        public async Task BecomeLost(Guid partyId)
        {
            await commandBus.SendAsync(new PartyLostCommand(partyId));
        }

        /// <summary>
        /// Потребление провизии и воды за указанное количество дней.
        /// </summary>
        public async Task ConsumeResources(Guid partyId, int days)
        {
            await commandBus.SendAsync(new ConsumeResourcesCommand(partyId, days));
        }

        /// <summary>
        /// Инициировать проверку случайной встречи.
        /// </summary>
        public async Task CheckRandomEncounter(Guid partyId, TerrainType terrain)
        {
            await commandBus.SendAsync(new RandomEncounterCheckCommand(partyId, terrain.ToString()));
        }

        /// <summary>
        /// Применить усталость (Exhaustion) членам группы из-за долгого путешествия или нехватки воды/еды.
        /// </summary>
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
        public async Task<int> GetCharacterSpeed(Guid characterId)
        {
            var character = await characterProjection.GetById(characterId);
            return character?.Speed ?? 30;
        }
    }
}