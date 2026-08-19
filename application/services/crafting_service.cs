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
    /// Содержит все данные, необходимые для определения возможности крафта и его стоимости.
    /// </summary>
    public class CraftingRecipe
    {
        /// <summary>Уникальный идентификатор рецепта.</summary>
        public Guid RecipeId { get; set; }

        /// <summary>Название рецепта.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Описание рецепта.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Идентификатор итогового предмета.</summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>Название итогового предмета.</summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>Базовая стоимость изготовления в золотых монетах.</summary>
        public int GoldCost { get; set; }

        /// <summary>Список особых компонентов, необходимых для изготовления.</summary>
        public List<CraftingComponent> Components { get; set; } = [];

        /// <summary>
        /// Время изготовления в часах. Для обычных предметов — часы, для магических — дни/недели
        /// (в таком случае значение задаётся в часах, например 24*7 для недели).
        /// </summary>
        public int CraftingTimeHours { get; set; }

        /// <summary>
        /// Требуемый инструмент (например, "Herbalism Kit", "Smith's Tools", "Alchemist's Supplies").
        /// Пустая строка означает, что инструмент не требуется.
        /// </summary>
        public string RequiredTool { get; set; } = string.Empty;

        /// <summary>
        /// Минимальный бонус мастерства или уровень персонажа (0 — без требования).
        /// </summary>
        public int RequiredProficiencyLevel { get; set; } = 0;

        /// <summary>Является ли предмет магическим.</summary>
        public bool IsMagical { get; set; }

        /// <summary>
        /// Идентификатор заклинания, которое нужно знать или иметь подготовленным
        /// для изготовления магических предметов или свитков.
        /// </summary>
        public string? RequiredSpellId { get; set; }

        /// <summary>Сложность проверки инструмента/навыка (если требуется).</summary>
        public int DifficultyClass { get; set; } = 10;

        /// <summary>
        /// Навык, используемый вместо инструмента (например, Arcana для свитков).
        /// </summary>
        public string? AssociatedSkill { get; set; }
    }

    /// <summary>
    /// Компонент, необходимый для изготовления предмета.
    /// </summary>
    public class CraftingComponent
    {
        /// <summary>Идентификатор компонента.</summary>
        public string ComponentId { get; set; } = string.Empty;

        /// <summary>Название компонента.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Требуемое количество.</summary>
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Активный процесс крафта (состояние).
    /// Отслеживает прогресс изготовления предмета персонажем.
    /// </summary>
    public class ActiveCraftingProcess
    {
        /// <summary>Уникальный идентификатор процесса.</summary>
        public Guid ProcessId { get; set; }

        /// <summary>Идентификатор персонажа, выполняющего крафт.</summary>
        public Guid CharacterId { get; set; }

        /// <summary>Идентификатор используемого рецепта.</summary>
        public Guid RecipeId { get; set; }

        /// <summary>Время начала крафта (UTC).</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>Ожидаемое время завершения (UTC).</summary>
        public DateTime EstimatedCompletion { get; set; }

        /// <summary>Общее время изготовления в часах.</summary>
        public int TotalHours { get; set; }

        /// <summary>Прошедшее время изготовления в часах.</summary>
        public int ElapsedHours { get; set; }
    }

    /// <summary>
    /// Репозиторий рецептов крафта.
    /// Предоставляет доступ к рецептам по идентификатору, списку, инструменту или заклинанию.
    /// </summary>
    public interface IRecipeRepository
    {
        /// <summary>Получить рецепт по идентификатору.</summary>
        CraftingRecipe? GetById(Guid recipeId);

        /// <summary>Получить все рецепты.</summary>
        List<CraftingRecipe> GetAll();

        /// <summary>Получить рецепты, требующие указанный инструмент.</summary>
        List<CraftingRecipe> GetByTool(string toolName);

        /// <summary>Получить рецепты, требующие указанное заклинание.</summary>
        List<CraftingRecipe> GetBySpell(string spellId);
    }

    /// <summary>
    /// Репозиторий активных процессов крафта.
    /// Хранит и управляет состоянием выполняющихся изготовлений.
    /// </summary>
    public interface ICraftingProcessRepository
    {
        /// <summary>Получить все активные процессы для указанного персонажа.</summary>
        List<ActiveCraftingProcess> GetActiveForCharacter(Guid characterId);

        /// <summary>Получить процесс по идентификатору.</summary>
        ActiveCraftingProcess? GetById(Guid processId);

        /// <summary>Добавить новый процесс.</summary>
        void Add(ActiveCraftingProcess process);

        /// <summary>Удалить процесс.</summary>
        void Remove(Guid processId);

        /// <summary>Обновить состояние процесса.</summary>
        void Update(ActiveCraftingProcess process);
    }

    /// <summary>
    /// Сервис крафта, предоставляющий функциональность по созданию предметов.
    /// Отвечает за проверку требований, списание ресурсов, управление процессами
    /// и выдачу готовых предметов. Использует командную шину для изменения состояния.
    /// </summary>
    /// <remarks>
    /// Паттерн: Application Service, координирующий выполнение бизнес-операции.
    /// Проверки прав доступа выполняются через <see cref="PermissionChecker"/>.
    /// Информация о персонаже берётся из проекции <see cref="CharacterProjection"/>.
    /// </remarks>
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
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>Список доступных рецептов.</returns>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если пользователю запрещён просмотр персонажа.</exception>
        /// <exception cref="InvalidOperationException">Выбрасывается, если персонаж не найден.</exception>
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
        /// <param name="characterId">Идентификатор персонажа, выполняющего крафт.</param>
        /// <param name="recipeId">Идентификатор выбранного рецепта.</param>
        /// <returns>Созданный активный процесс крафта.</returns>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если пользователю запрещено редактирование персонажа.</exception>
        /// <exception cref="InvalidOperationException">
        /// Выбрасывается, если персонаж не найден, рецепт не найден, требования не выполнены,
        /// недостаточно золота или отсутствуют компоненты.
        /// </exception>
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
            if (recipe.GoldCost > 0)
            {
                // Используем данные о золоте из проекции
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
        /// Если время истекло, процесс завершается и предмет выдаётся.
        /// </summary>
        /// <param name="processId">Идентификатор процесса крафта.</param>
        /// <param name="hours">Количество часов для продвижения.</param>
        /// <exception cref="InvalidOperationException">Выбрасывается, если процесс не найден.</exception>
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
        /// Внутренний метод, вызывается при достижении достаточного времени.
        /// </summary>
        /// <param name="process">Активный процесс крафта.</param>
        /// <exception cref="InvalidOperationException">Выбрасывается, если рецепт не найден.</exception>
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
        /// <param name="processId">Идентификатор процесса крафта.</param>
        /// <exception cref="InvalidOperationException">Выбрасывается, если процесс или рецепт не найдены.</exception>
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
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>Список активных процессов.</returns>
        /// <exception cref="UnauthorizedAccessException">Выбрасывается, если пользователю запрещён просмотр персонажа.</exception>
        public List<ActiveCraftingProcess> GetActiveCraftingProcesses(Guid characterId)
        {
            if (!permissionChecker.CanViewCharacter(characterId))
                throw new UnauthorizedAccessException("Cannot view this character.");

            return processRepository.GetActiveForCharacter(characterId);
        }
    }
}