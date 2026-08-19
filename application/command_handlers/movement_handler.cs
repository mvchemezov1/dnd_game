using dnd_game.Domain.Commands;
using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Rules;
using dnd_game.Infrastructure.EventStore;
using dnd_game.Infrastructure.World;
using dnd_game.Domain.ValueObjects;
using dnd_game.Domain.Exceptions;
using dnd_game.domain.value_objects;

namespace dnd_game.application.command_handlers
{
    /// <summary>
    /// Обрабатывает команды, связанные с перемещением персонажа, загружая агрегат <see cref="CharacterAggregate"/> из хранилища событий,
    /// проверяя правила движения и вызывая соответствующее поведение домена.
    /// Реализует паттерн обработчика команд с использованием событийного сорсинга и провайдера игровой сетки.
    /// </summary>
    /// <remarks>
    /// Большинство обработчиков следуют стандартному потоку:
    /// 1. Загрузить агрегат персонажа по идентификатору.
    /// 2. Если персонаж не найден, выбросить исключение <see cref="InvalidAction"/>.
    /// 3. При необходимости выполнить проверки игровых правил (границы сетки, стоимость движения и т.д.).
    /// 4. Вызвать метод агрегата, соответствующий команде.
    /// 5. Сохранить агрегат, что приводит к добавлению новых событий в хранилище событий.
    /// </remarks>
    public class MovementHandler(
        IEventStore eventStore,
        IGridProvider gridProvider) : ICommandHandler<MoveCharacter>,
                                      ICommandHandler<MoveCharacterToPosition>,
                                      ICommandHandler<MoveCharacterWithDash>,
                                      ICommandHandler<MoveCharacterWithDisengage>,
                                      ICommandHandler<MoveCharacterStealthily>,
                                      ICommandHandler<ClimbCharacter>,
                                      ICommandHandler<SwimCharacter>,
                                      ICommandHandler<FlyCharacter>,
                                      ICommandHandler<BurrowCharacter>,
                                      ICommandHandler<JumpCharacter>,
                                      ICommandHandler<SetCharacterSpeed>,
                                      ICommandHandler<ResetCharacterSpeed>,
                                      ICommandHandler<ApplyDifficultTerrain>,
                                      ICommandHandler<RemoveDifficultTerrain>,
                                      ICommandHandler<ApplyMovementImpairment>,
                                      ICommandHandler<RemoveMovementImpairment>,
                                      ICommandHandler<MakeAthleticsCheckForMovement>,
                                      ICommandHandler<MakeAcrobaticsCheckForMovement>,
                                      ICommandHandler<TakeFallDamage>
    {
        private readonly IEventStore _eventStore = eventStore;
        private readonly IGridProvider _grid = gridProvider;

        // ---------- Основное перемещение ----------

        /// <summary>
        /// Обрабатывает команду <see cref="MoveCharacter"/>, перемещая персонажа на указанную позицию с учётом стоимости клетки и доступной скорости.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа, координаты цели и тип движения.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">
        /// Выбрасывается, если персонаж не найден, целевая позиция за пределами сетки,
        /// клетка непроходима или недостаточно скорости для перемещения.
        /// </exception>
        public async Task Handle(MoveCharacter command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");

            // Текущая позиция берётся из агрегата (обновляется событиями перемещения)
            var currentPos = new Position(character.PositionX, character.PositionY);
            var targetPos = new Position(command.TargetX, command.TargetY);

            if (!_grid.InBounds(targetPos.X, targetPos.Y))
                throw new InvalidAction("Target position out of bounds.");

            var targetCell = _grid.GetCell(targetPos.X, targetPos.Y);
            int costPerCell = MovementRules.GetMovementCostPerCell(targetCell.Terrain);
            if (costPerCell < 0)
                throw new InvalidAction("Target cell is impassable.");

            // Реальная скорость персонажа (учитывает модификаторы, обновляется событием SpeedUpdated)
            int baseSpeed = character.Speed;

            // Проверка дистанции: расстояние по Чебышёву (в футах) от текущей позиции до цели
            // не должно превышать доступную скорость. Полноценный расчёт стоимости пути не выполняется —
            // для этого есть отдельные методы в MovementRules, но данная команда не привязана к боевой сцене.
            int distanceFeet = currentPos.ChebyshevDistanceInFeet(targetPos);
            int remainingSpeed = baseSpeed;
            if (remainingSpeed < distanceFeet)
                throw new InvalidAction($"Not enough movement. Required: {distanceFeet}, available: {remainingSpeed}.");
            if (remainingSpeed < costPerCell)
                throw new InvalidAction($"Not enough movement. Required: {costPerCell}, available: {remainingSpeed}.");

            character.MoveToPosition(command.TargetX, command.TargetY, "Walk");
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- Перемещение с указанием типа (MoveCharacterToPosition) ----------

        /// <summary>
        /// Обрабатывает команду <see cref="MoveCharacterToPosition"/>, перемещая персонажа с явно указанным типом движения.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа, координаты цели и тип движения.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">
        /// Выбрасывается, если персонаж не найден, целевая позиция за пределами сетки или клетка непроходима.
        /// </exception>
        public async Task Handle(MoveCharacterToPosition command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");

            var targetPos = new Position(command.TargetX, command.TargetY);
            if (!_grid.InBounds(targetPos.X, targetPos.Y))
                throw new InvalidAction("Target position out of bounds.");

            var cell = _grid.GetCell(targetPos.X, targetPos.Y);
            int cost = MovementRules.GetMovementCostPerCell(cell.Terrain);
            if (cost < 0)
                throw new InvalidAction("Target cell is impassable.");

            character.MoveToPosition(command.TargetX, command.TargetY, command.MovementType);
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- Действия Dash, Disengage, Hide ----------

        /// <summary>
        /// Обрабатывает команду <see cref="MoveCharacterWithDash"/>, заставляя персонажа использовать действие «Рывок».
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(MoveCharacterWithDash command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Dash();
            await _eventStore.Save(character, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="MoveCharacterWithDisengage"/>, позволяя персонажу избежать провоцированных атак.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(MoveCharacterWithDisengage command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Disengage();
            await _eventStore.Save(character, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="MoveCharacterStealthily"/>, заставляя персонажа скрыться.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(MoveCharacterStealthily command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Hide();
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- Специальные виды движения ----------

        /// <summary>
        /// Обрабатывает команду <see cref="ClimbCharacter"/>, заставляя персонажа лазать.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа, дистанцию и использованную скорость лазания.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(ClimbCharacter command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Climb(command.DistanceFeet, command.ClimbSpeedUsed);
            await _eventStore.Save(character, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="SwimCharacter"/>, заставляя персонажа плыть.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа, дистанцию и использованную скорость плавания.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(SwimCharacter command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Swim(command.DistanceFeet, command.SwimSpeedUsed);
            await _eventStore.Save(character, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="FlyCharacter"/>, заставляя персонажа лететь.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа, дистанцию и использованную скорость полёта.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(FlyCharacter command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Fly(command.DistanceFeet, command.FlySpeedUsed);
            await _eventStore.Save(character, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="BurrowCharacter"/>, заставляя персонажа копать.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа, дистанцию и использованную скорость копания.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(BurrowCharacter command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Burrow(command.DistanceFeet, command.BurrowSpeedUsed);
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- Прыжки ----------

        /// <summary>
        /// Обрабатывает команду <see cref="JumpCharacter"/>, заставляя персонажа совершить прыжок.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа, тип прыжка, силу и признак разбега.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(JumpCharacter command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Jump(command.JumpType, command.StrengthScore, command.RunningStart);
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- Управление скоростью ----------

        /// <summary>
        /// Обрабатывает команду <see cref="SetCharacterSpeed"/>, временно изменяя скорость персонажа.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа, новую скорость и тип движения.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(SetCharacterSpeed command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.SetTemporarySpeed(command.NewSpeed, command.MovementType);
            await _eventStore.Save(character, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="ResetCharacterSpeed"/>, сбрасывая скорость персонажа к базовой.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(ResetCharacterSpeed command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.ResetSpeedToBase();
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- Модификаторы местности ----------

        /// <summary>
        /// Обрабатывает команду <see cref="ApplyDifficultTerrain"/>, применяя штраф за труднопроходимую местность.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа и множитель стоимости движения.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(ApplyDifficultTerrain command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.ApplyDifficultTerrain(command.Multiplier);
            await _eventStore.Save(character, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="RemoveDifficultTerrain"/>, снимая штраф за труднопроходимую местность.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(RemoveDifficultTerrain command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.RemoveDifficultTerrain();
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- Ограничения движения (Impaired) ----------

        /// <summary>
        /// Обрабатывает команду <see cref="ApplyMovementImpairment"/>, накладывая ограничение на движение.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа, тип ограничения и величину снижения скорости.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(ApplyMovementImpairment command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.ApplyMovementImpairment(command.ImpairmentType, command.SpeedReduction);
            await _eventStore.Save(character, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="RemoveMovementImpairment"/>, снимая ограничение на движение.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа и тип ограничения.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(RemoveMovementImpairment command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.RemoveMovementImpairment(command.ImpairmentType);
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- Проверки навыков ----------

        /// <summary>
        /// Обрабатывает команду <see cref="MakeAthleticsCheckForMovement"/>, выполняя проверку Атлетики для движения.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа, сложность, бросок, бонус мастерства и модификатор силы.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(MakeAthleticsCheckForMovement command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.MakeAthleticsCheck(command.DifficultyClass, command.RollResult,
                                         command.ProficiencyBonus, command.StrengthModifier);
            await _eventStore.Save(character, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="MakeAcrobaticsCheckForMovement"/>, выполняя проверку Акробатики для движения.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа, сложность, бросок, бонус мастерства и модификатор ловкости.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(MakeAcrobaticsCheckForMovement command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.MakeAcrobaticsCheck(command.DifficultyClass, command.RollResult,
                                          command.ProficiencyBonus, command.DexterityModifier);
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- Падение ----------

        /// <summary>
        /// Обрабатывает команду <see cref="TakeFallDamage"/>, нанося урон от падения.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор персонажа и высоту падения в футах.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
        public async Task Handle(TakeFallDamage command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.TakeFallDamage(command.FallDistanceFeet);
            await _eventStore.Save(character, cancellationToken);
        }
    }
}