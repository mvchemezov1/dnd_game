// application/services/crafting_service.cs
using dnd_game.Domain.Commands;
using dnd_game.Application.Projections; // для CharacterProjection
using dnd_game.Application.Security;   // PermissionChecker, PolicyEnforcer
using System.Linq;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Application.Services
{
    /// <summary>
    /// Рецепт изготовления предмета (обычного, магического, зелья, свитка).
    /// </summary>
    public class CraftingRecipe
    {
        public Guid RecipeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;         // итоговый предмет
        public string ItemName { get; set; } = string.Empty;
        public int GoldCost { get; set; }                           // базовая стоимость в золоте
        public List<CraftingComponent> Components { get; set; } = []; // особые компоненты
        public int CraftingTimeHours { get; set; }                  // время изготовления (часы для обычных, дни/недели для магических)
        public string RequiredTool { get; set; } = string.Empty;    // инструмент: "Herbalism Kit", "Smith's Tools", "Alchemist's Supplies" и т.д.
        public int RequiredProficiencyLevel { get; set; } = 0;      // минимальный бонус мастерства (или уровень персонажа)
        public bool IsMagical { get; set; }
        public string? RequiredSpellId { get; set; }                // заклинание, которое нужно знать/иметь подготовленным для магических предметов/свитков
        public int DifficultyClass { get; set; } = 10;              // СЛ проверки инструмента (если требуется)
        public string? AssociatedSkill { get; set; }                // навык, если вместо инструмента (напр. Arcana для свитков)
    }

    public class CraftingComponent
    {
        public string ComponentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Активный процесс крафта (состояние).
    /// </summary>
    public class ActiveCraftingProcess
    {
        public Guid ProcessId { get; set; }
        public Guid CharacterId { get; set; }
        public Guid RecipeId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EstimatedCompletion { get; set; }
        public int TotalHours { get; set; }
        public int ElapsedHours { get; set; }
    }

    /// <summary>
    /// Репозиторий рецептов (в БД или JSON).
    /// </summary>
    public interface IRecipeRepository
    {
        CraftingRecipe? GetById(Guid recipeId);
        List<CraftingRecipe> GetAll();
        List<CraftingRecipe> GetByTool(string toolName);
        List<CraftingRecipe> GetBySpell(string spellId);
    }

    /// <summary>
    /// Репозиторий активных процессов крафта.
    /// </summary>
    public interface ICraftingProcessRepository
    {
        List<ActiveCraftingProcess> GetActiveForCharacter(Guid characterId);
        ActiveCraftingProcess? GetById(Guid processId);
        void Add(ActiveCraftingProcess process);
        void Remove(Guid processId);
        void Update(ActiveCraftingProcess process);
    }

    public class CraftingService(
        ICommandBus commandBus,
        CharacterProjection characterProjection,
        IRecipeRepository recipeRepository,
        ICraftingProcessRepository processRepository,
        PermissionChecker permissionChecker)
    {

        /// <summary>
        /// Получить все рецепты, доступные персонажу с учётом навыков, инструментов и заклинаний.
        /// </summary>
        public async Task<List<CraftingRecipe>> GetAvailableRecipes(Guid characterId)
        {
            if (!permissionChecker.CanViewCharacter(characterId))
                throw new UnauthorizedAccessException("Cannot view this character.");

            var character = await characterProjection.GetById(characterId)
                            ?? throw new InvalidOperationException("Character not found.");
            var allRecipes = recipeRepository.GetAll();
            var available = new List<CraftingRecipe>();

            foreach (var recipe in allRecipes)
            {
                // Проверка требуемого инструмента
                if (!string.IsNullOrEmpty(recipe.RequiredTool))
                {
                    // Предполагаем, что владение инструментами хранится в проекции (можно расширить)
                    if (!character.SkillProficiencies.ContainsKey(recipe.RequiredTool))
                        continue; // нет proficiency
                }

                // Проверка уровня (персонаж должен быть не ниже требуемого)
                if (recipe.RequiredProficiencyLevel > 0 && character.Level < recipe.RequiredProficiencyLevel)
                    continue;

                // Проверка заклинания (для свитков и магических предметов)
                if (!string.IsNullOrEmpty(recipe.RequiredSpellId))
                {
                    if (!character.KnownSpells.Contains(recipe.RequiredSpellId))
                        continue;
                }

                available.Add(recipe);
            }
            return available;
        }

        /// <summary>
        /// Начать изготовление предмета.
        /// Проверяет наличие ингредиентов, списывает золото и компоненты, создаёт процесс крафта.
        /// </summary>
        public async Task<ActiveCraftingProcess> StartCrafting(Guid characterId, Guid recipeId)
        {
            if (!permissionChecker.CanEditCharacter(characterId))
                throw new UnauthorizedAccessException("Cannot craft with this character.");

            var character = await characterProjection.GetById(characterId)
                            ?? throw new InvalidOperationException("Character not found.");

            var recipe = recipeRepository.GetById(recipeId)
                         ?? throw new InvalidOperationException("Recipe not found.");

            // Проверить доступность рецепта (по правилам)
            var available = await GetAvailableRecipes(characterId);
            if (!available.Any(r => r.RecipeId == recipeId))
                throw new InvalidOperationException("Character does not meet requirements for this recipe.");

            // Проверить наличие компонентов и золота
            // Используем InventoryItemDto и данные о золоте (предположим, золото хранится отдельно)
            // В реальном коде через CharacterProjection или репозиторий
            if (recipe.GoldCost > 0)
            {
                var charGold = await characterProjection.GetById(characterId);
                int goldCheck = charGold?.Gold ?? 0; // метод нужно добавить в проекцию
                if (goldCheck < recipe.GoldCost)
                    throw new InvalidOperationException("Not enough gold.");
            }

            foreach (var comp in recipe.Components)
            {
                var hasItem = character.Inventory.FirstOrDefault(i => i.ItemId == comp.ComponentId);
                if (hasItem == null || hasItem.Quantity < comp.Quantity)
                    throw new InvalidOperationException($"Missing required component: {comp.Name}");
            }

            // Списать золото и компоненты (через команды)
            await commandBus.SendAsync(new SpendGold(characterId, recipe.GoldCost));
            foreach (var comp in recipe.Components)
            {
                for (int i = 0; i < comp.Quantity; i++)
                    await commandBus.SendAsync(new RemoveInventoryItem(characterId, comp.ComponentId));
            }

            // Создать процесс крафта
            var process = new ActiveCraftingProcess
            {
                ProcessId = Guid.NewGuid(),
                CharacterId = characterId,
                RecipeId = recipeId,
                StartedAt = DateTime.UtcNow,
                TotalHours = recipe.CraftingTimeHours,
                ElapsedHours = 0,
                EstimatedCompletion = DateTime.UtcNow.AddHours(recipe.CraftingTimeHours)
            };
            processRepository.Add(process);

            // Отправить событие о начале крафта (опционально)
            // await _commandBus.SendAsync(new CraftingStarted(characterId, recipeId, process.ProcessId));

            return process;
        }

        /// <summary>
        /// Продвинуть время крафта (на заданное количество часов). Обычно вызывается при продвижении игрового времени.
        /// </summary>
        public async Task AdvanceCraftingTime(Guid processId, int hours)
        {
            var process = processRepository.GetById(processId)
                          ?? throw new InvalidOperationException("Crafting process not found.");

            process.ElapsedHours += hours;
            if (process.ElapsedHours >= process.TotalHours)
            {
                await CompleteCrafting(process);
            }
            else
            {
                processRepository.Update(process);
            }
        }

        /// <summary>
        /// Завершить изготовление и выдать предмет.
        /// </summary>
        private async Task CompleteCrafting(ActiveCraftingProcess process)
        {
            var recipe = recipeRepository.GetById(process.RecipeId)
                         ?? throw new InvalidOperationException("Recipe not found.");

            // Проверка навыка/инструмента (если требуется бросок)
            if (recipe.DifficultyClass > 0)
            {
                // Здесь должен быть вызов броска проверки инструмента; для упрощения предполагаем, что проверка уже выполнена
                // В реальности отправляем команду MakeSkillCheck и ждём результата, но это выходит за рамки сервиса.
                // Для магических предметов может быть осложнение (Xanathar's).
            }

            // Выдать предмет
            await commandBus.SendAsync(new AddInventoryItem(process.CharacterId, recipe.ItemId, recipe.ItemName, 1));

            // Удалить процесс
            processRepository.Remove(process.ProcessId);

            // Событие завершения
            // await _commandBus.SendAsync(new CraftingCompleted(process.CharacterId, recipe.ItemId));
        }

        /// <summary>
        /// Отменить крафт и вернуть часть материалов (половина золота, компоненты не возвращаются по правилам).
        /// </summary>
        public async Task CancelCrafting(Guid processId)
        {
            var process = processRepository.GetById(processId)
                          ?? throw new InvalidOperationException("Crafting process not found.");

            var recipe = recipeRepository.GetById(process.RecipeId)
                         ?? throw new InvalidOperationException("Recipe not found.");

            // Возврат половины золота (правила DMG/Xanathar's)
            int refundGold = recipe.GoldCost / 2;
            if (refundGold > 0)
                await commandBus.SendAsync(new AddGold(process.CharacterId, refundGold));

            // Компоненты не возвращаются (по правилам)

            processRepository.Remove(process.ProcessId);
        }

        /// <summary>
        /// Получить список активных процессов крафта для персонажа.
        /// </summary>
        public List<ActiveCraftingProcess> GetActiveCraftingProcesses(Guid characterId)
        {
            if (!permissionChecker.CanViewCharacter(characterId))
                throw new UnauthorizedAccessException("Cannot view this character.");

            return processRepository.GetActiveForCharacter(characterId);
        }
    }
}