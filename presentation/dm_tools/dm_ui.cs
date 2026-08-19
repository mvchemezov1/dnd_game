// presentation/dm_tools/dm_ui.cs
using dnd_game.Application.Projections;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Queries;
using dnd_game.Domain.Aggregates;
using dnd_game.Infrastructure.MessageBus;
using dnd_game.Infrastructure.Network;   // IGameClient, если UI работает через сеть
using System.Text.Json;
using ProjQuestStatus = dnd_game.Application.Projections.QuestStatus;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Presentation.DmTools
{
    /// <summary>
    /// Консольный / базовый UI для Мастера подземелий.
    /// Предоставляет инструменты управления кампанией, NPC, монстрами и боем.
    /// </summary>
    public class DmUi
    {
        private readonly ICommandBus _commandBus;
        private readonly IQueryBus _queryBus;
        private readonly CharacterProjection _characterProjection;
        private readonly CombatProjection _combatProjection;
        private readonly CampaignProjection _campaignProjection;

        // Локальное состояние интерфейса
        private Guid _currentCampaignId;

        public DmUi(
            ICommandBus commandBus,
            IQueryBus queryBus,
            CharacterProjection characterProjection,
            CombatProjection combatProjection,
            CampaignProjection campaignProjection)
        {
            _commandBus = commandBus;
            _queryBus = queryBus;
            _characterProjection = characterProjection;
            _combatProjection = combatProjection;
            _campaignProjection = campaignProjection;
        }

        /// <summary>
        /// Отобразить главный экран инструментов Мастера (заглушка для консоли).
        /// </summary>
        public async Task Render()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== DM TOOLS ===");
                Console.WriteLine("1. Campaign Overview");
                Console.WriteLine("2. Party Status");
                Console.WriteLine("3. Combat Tracker");
                Console.WriteLine("4. Quick Actions (damage/heal/condition)");
                Console.WriteLine("5. Spawn Monster / NPC");
                Console.WriteLine("6. Manage Quests");
                Console.WriteLine("7. World State (time/weather/flags)");
                Console.WriteLine("8. Inspect Character");
                Console.WriteLine("9. Exit");
                Console.Write("Select: ");

                var key = Console.ReadKey().Key;
                Console.WriteLine();

                switch (key)
                {
                    case ConsoleKey.D1: await ShowCampaignOverview(); break;
                    case ConsoleKey.D2: await ShowPartyStatus(); break;
                    case ConsoleKey.D3: await ShowCombatTracker(); break;
                    case ConsoleKey.D4: await QuickActionsMenu(); break;
                    case ConsoleKey.D5: await SpawnMenu(); break;
                    case ConsoleKey.D6: await ManageQuests(); break;
                    case ConsoleKey.D7: await WorldStateMenu(); break;
                    case ConsoleKey.D8: await InspectCharacter(); break;
                    case ConsoleKey.D9: return;
                }
            }
        }

        // --------------------------------------------------------------------------
        // 1. Обзор кампании
        // --------------------------------------------------------------------------
        private async Task ShowCampaignOverview()
        {
            if (_currentCampaignId == Guid.Empty)
            {
                Console.Write("Enter Campaign ID: ");
                _currentCampaignId = Guid.Parse(Console.ReadLine()!);
            }

            var state = await _campaignProjection.GetCampaignState(_currentCampaignId);
            if (state == null) { Console.WriteLine("Campaign not found."); Console.ReadKey(); return; }

            Console.WriteLine($"=== {state.CampaignName} ===");
            Console.WriteLine($"Day {state.Day}, {state.Hour}:{state.Minute:D2} | Weather: {state.Weather}");
            Console.WriteLine($"Act: {state.CurrentAct}");
            Console.WriteLine("Regions: " + string.Join(", ", state.DiscoveredRegions));
            Console.WriteLine("Flags: " + string.Join(", ", state.GlobalFlags.Select(kv => $"{kv.Key}={kv.Value}")));
            Console.ReadKey();
        }

        // --------------------------------------------------------------------------
        // 2. Состояние группы
        // --------------------------------------------------------------------------
        private async Task ShowPartyStatus()
        {
            var characters = await _characterProjection.GetAll();
            foreach (var c in characters)
            {
                string status = c.IsDead ? "DEAD" : (c.HitPoints <= 0 ? (c.IsStable ? "Stable" : "Dying") : "Alive");
                Console.WriteLine($"{c.Name} (Lv.{c.Level} {c.Race} {c.Class}) HP: {c.HitPoints}/{c.MaxHitPoints} AC:{c.ArmorClass} | {status}");
                if (c.Conditions.Any()) Console.WriteLine("  Conditions: " + string.Join(", ", c.Conditions));
            }
            Console.ReadKey();
        }

        // --------------------------------------------------------------------------
        // 3. Боевой трекер
        // --------------------------------------------------------------------------
        private async Task ShowCombatTracker()
        {
            Console.Write("Enter Combat ID: ");
            var combatId = Guid.Parse(Console.ReadLine()!);
            var status = await _combatProjection.GetStatus(combatId);
            if (status == null) { Console.WriteLine("Combat not found."); Console.ReadKey(); return; }

            Console.WriteLine($"Combat {status.CombatId} | Round {status.Round} | Active: {status.IsActive}");
            foreach (var p in status.Participants)
            {
                var character = await _characterProjection.GetById(p.CharacterId);
                string name = character?.Name ?? p.CharacterId.ToString();
                string turnMarker = p.IsCurrentTurn ? " <= CURRENT" : "";
                Console.WriteLine($"  {name} Init: {p.Initiative} HP: {character?.HitPoints}/{character?.MaxHitPoints} {turnMarker}");
            }
            Console.ReadKey();
        }

        // --------------------------------------------------------------------------
        // 4. Быстрые действия
        // --------------------------------------------------------------------------
        private async Task QuickActionsMenu()
        {
            Console.Write("Enter Character ID: ");
            var characterId = Guid.Parse(Console.ReadLine()!);
            var character = await _characterProjection.GetById(characterId);
            if (character == null) { Console.WriteLine("Not found."); Console.ReadKey(); return; }
            Console.WriteLine($"Target: {character.Name} (HP {character.HitPoints}/{character.MaxHitPoints})");

            Console.WriteLine("1. Deal Damage");
            Console.WriteLine("2. Heal");
            Console.WriteLine("3. Apply Condition");
            Console.WriteLine("4. Remove Condition");
            Console.Write("Action: ");
            var key = Console.ReadKey().Key;
            Console.WriteLine();

            switch (key)
            {
                case ConsoleKey.D1:
                    Console.Write("Damage amount: "); int dmg = int.Parse(Console.ReadLine()!);
                    Console.Write("Damage type [bludgeoning]: "); string dtype = Console.ReadLine()!;
                    if (string.IsNullOrWhiteSpace(dtype)) dtype = "bludgeoning";
                    await _commandBus.SendAsync(new DealDamage(characterId, dmg, dtype));
                    Console.WriteLine($"Dealt {dmg} {dtype} damage to {character.Name}.");
                    break;
                case ConsoleKey.D2:
                    Console.Write("Healing amount: "); int heal = int.Parse(Console.ReadLine()!);
                    await _commandBus.SendAsync(new HealCharacter(characterId, heal));
                    Console.WriteLine($"Healed {character.Name} for {heal} HP.");
                    break;
                case ConsoleKey.D3:
                    Console.Write("Condition: "); string cond = Console.ReadLine()!;
                    Console.Write("Duration (rounds): "); int dur = int.Parse(Console.ReadLine()!);
                    await _commandBus.SendAsync(new ApplyCondition(characterId, cond, dur));
                    Console.WriteLine($"Applied {cond} to {character.Name}.");
                    break;
                case ConsoleKey.D4:
                    Console.Write("Condition: "); string remCond = Console.ReadLine()!;
                    await _commandBus.SendAsync(new RemoveCondition(characterId, remCond));
                    Console.WriteLine($"Removed {remCond} from {character.Name}.");
                    break;
            }
            Console.ReadKey();
        }

        // --------------------------------------------------------------------------
        // 5. Спавн монстра / NPC
        // --------------------------------------------------------------------------
        private async Task SpawnMenu()
        {
            Console.Write("Enter name: "); string name = Console.ReadLine()!;
            Console.Write("Max HP: "); int hp = int.Parse(Console.ReadLine()!);
            var newId = Guid.NewGuid();
            await _commandBus.SendAsync(new CreateCharacter(newId, name, hp));
            Console.WriteLine($"Spawned {name} (ID: {newId}).");
            Console.ReadKey();
        }

        // --------------------------------------------------------------------------
        // 6. Управление квестами
        // --------------------------------------------------------------------------
        private async Task ManageQuests()
        {
            var quests = await _campaignProjection.GetQuests(_currentCampaignId);
            foreach (var q in quests)
                Console.WriteLine($"[{q.QuestId}] {q.Title} ({q.Status})");

            Console.Write("Enter Quest ID to toggle status: ");
            var qid = Guid.Parse(Console.ReadLine()!);
            var quest = quests.FirstOrDefault(q => q.QuestId == qid);
            if (quest == null) { Console.WriteLine("Not found."); Console.ReadKey(); return; }
            // Упрощённо: переключаем Complete/Fail
            if (quest.Status == ProjQuestStatus.Active)
                await _commandBus.SendAsync(new CompleteQuestCommand(_currentCampaignId, qid));
            else if (quest.Status == ProjQuestStatus.Completed)
                await _commandBus.SendAsync(new FailQuestCommand(_currentCampaignId, qid));
            Console.WriteLine("Status toggled.");
            Console.ReadKey();
        }

        // --------------------------------------------------------------------------
        // 7. Мировое состояние
        // --------------------------------------------------------------------------
        private async Task WorldStateMenu()
        {
            Console.WriteLine("1. Advance Time");
            Console.WriteLine("2. Change Weather");
            Console.Write("Select: ");
            var key = Console.ReadKey().Key;
            Console.WriteLine();
            if (key == ConsoleKey.D1)
            {
                Console.Write("Minutes to advance: "); int mins = int.Parse(Console.ReadLine()!);
                await _commandBus.SendAsync(new AdvanceTimeCommand(_currentCampaignId, mins));
                Console.WriteLine($"Time advanced by {mins} minutes.");
            }
            else if (key == ConsoleKey.D2)
            {
                Console.Write("New weather: "); string w = Console.ReadLine()!;
                await _commandBus.SendAsync(new ChangeWeatherCommand(_currentCampaignId, w));
                Console.WriteLine($"Weather set to {w}.");
            }
            Console.ReadKey();
        }

        // --------------------------------------------------------------------------
        // 8. Инспекция персонажа
        // --------------------------------------------------------------------------
        private async Task InspectCharacter()
        {
            Console.Write("Enter Character ID: ");
            var characterId = Guid.Parse(Console.ReadLine()!);
            var character = await _characterProjection.GetById(characterId);
            if (character == null) { Console.WriteLine("Not found."); Console.ReadKey(); return; }

            Console.WriteLine($"=== {character.Name} (Lv.{character.Level} {character.Race} {character.Class}) ===");
            Console.WriteLine($"HP: {character.HitPoints}/{character.MaxHitPoints} (Temp: {character.TemporaryHitPoints})");
            Console.WriteLine($"AC: {character.ArmorClass}  Speed: {character.Speed} ft");
            Console.WriteLine($"XP: {character.ExperiencePoints}  Proficiency: +{character.ProficiencyBonus}");
            Console.WriteLine("Abilities: " + string.Join(", ", character.AbilityScores.Select(kv => $"{kv.Key}:{kv.Value}")));
            Console.WriteLine("Skills: " + string.Join(", ", character.SkillProficiencies));
            Console.WriteLine("Conditions: " + string.Join(", ", character.Conditions));
            Console.WriteLine("Resistances: " + string.Join(", ", character.Resistances));
            Console.ReadKey();
        }
    }
}