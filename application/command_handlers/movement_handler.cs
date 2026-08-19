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

        // ---------- �������� ����������� ----------

        public async Task Handle(MoveCharacter command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");

            // Текущая позиция берётся из самого агрегата (CharacterAggregate.PositionX/PositionY),
            // которые обновляются при применении события CharacterMovedToPosition.
            var currentPos = new Position(character.PositionX, character.PositionY);
            var targetPos = new Position(command.TargetX, command.TargetY);

            if (!_grid.InBounds(targetPos.X, targetPos.Y))
                throw new InvalidAction("Target position out of bounds.");

            var targetCell = _grid.GetCell(targetPos.X, targetPos.Y);
            int costPerCell = MovementRules.GetMovementCostPerCell(targetCell.Terrain);
            if (costPerCell < 0)
                throw new InvalidAction("Target cell is impassable.");

            // Реальная скорость персонажа (а не хардкод), с учётом текущих модификаторов (Speed
            // уже отражает базовую скорость расы/класса и обновляется событием SpeedUpdated).
            int baseSpeed = character.Speed;

            // Честная проверка дистанции: суммарное расстояние по прямой (в футах, с учётом
            // диагоналей по правилам D&D) от текущей позиции до цели не должно превышать
            // доступную скорость персонажа за один ход.
            // Примечание: это НЕ полноценный path-cost по местности вдоль всего маршрута
            // (для этого есть MovementRules.CalculatePathCost с реальным путём по клеткам) и
            // НЕ проверка бюджета движения в текущем раунде боя (для боя за это отвечает
            // CombatAggregate.UseMovement/MovementRemaining — MoveCharacter сейчас с ним не
            // связан, так как команда не содержит CombatId). Это осознанное ограничение
            // текущей реализации, а не скрытая недоработка.
            int distanceFeet = currentPos.ChebyshevDistanceInFeet(targetPos);
            int remainingSpeed = baseSpeed;
            if (remainingSpeed < distanceFeet)
                throw new InvalidAction($"Not enough movement. Required: {distanceFeet}, available: {remainingSpeed}.");
            if (remainingSpeed < costPerCell)
                throw new InvalidAction($"Not enough movement. Required: {costPerCell}, available: {remainingSpeed}.");

            character.MoveToPosition(command.TargetX, command.TargetY, "Walk");
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- ����������� � ��������� ���� (MoveCharacterToPosition) ----------

        public async Task Handle(MoveCharacterToPosition command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");

            // ���������� MoveCharacter, �� � ����� ��������
            var targetPos = new Position(command.TargetX, command.TargetY);
            if (!_grid.InBounds(targetPos.X, targetPos.Y))
                throw new InvalidAction("Target position out of bounds.");

            var cell = _grid.GetCell(targetPos.X, targetPos.Y);
            int cost = MovementRules.GetMovementCostPerCell(cell.Terrain);
            if (cost < 0)
                throw new InvalidAction("Target cell is impassable.");

            // �������� �������� (����������)
            // ��������� ��������
            character.MoveToPosition(command.TargetX, command.TargetY, command.MovementType);
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- �������� Dash, Disengage, Hide ----------

        public async Task Handle(MoveCharacterWithDash command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Dash();
            await _eventStore.Save(character, cancellationToken);
        }

        public async Task Handle(MoveCharacterWithDisengage command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Disengage();
            await _eventStore.Save(character, cancellationToken);
        }

        public async Task Handle(MoveCharacterStealthily command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Hide();
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- ����������� ���� �������� ----------

        public async Task Handle(ClimbCharacter command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Climb(command.DistanceFeet, command.ClimbSpeedUsed);
            await _eventStore.Save(character, cancellationToken);
        }

        public async Task Handle(SwimCharacter command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Swim(command.DistanceFeet, command.SwimSpeedUsed);
            await _eventStore.Save(character, cancellationToken);
        }

        public async Task Handle(FlyCharacter command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Fly(command.DistanceFeet, command.FlySpeedUsed);
            await _eventStore.Save(character, cancellationToken);
        }

        public async Task Handle(BurrowCharacter command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Burrow(command.DistanceFeet, command.BurrowSpeedUsed);
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- ������ ----------

        public async Task Handle(JumpCharacter command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.Jump(command.JumpType, command.StrengthScore, command.RunningStart);
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- ���������� ��������� ----------

        public async Task Handle(SetCharacterSpeed command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.SetTemporarySpeed(command.NewSpeed, command.MovementType);
            await _eventStore.Save(character, cancellationToken);
        }

        public async Task Handle(ResetCharacterSpeed command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.ResetSpeedToBase();
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- ������������ ��������� ----------

        public async Task Handle(ApplyDifficultTerrain command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.ApplyDifficultTerrain(command.Multiplier);
            await _eventStore.Save(character, cancellationToken);
        }

        public async Task Handle(RemoveDifficultTerrain command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.RemoveDifficultTerrain();
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- ����������� �������� (Impaired) ----------

        public async Task Handle(ApplyMovementImpairment command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.ApplyMovementImpairment(command.ImpairmentType, command.SpeedReduction);
            await _eventStore.Save(character, cancellationToken);
        }

        public async Task Handle(RemoveMovementImpairment command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.RemoveMovementImpairment(command.ImpairmentType);
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- �������� ������� ----------

        public async Task Handle(MakeAthleticsCheckForMovement command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.MakeAthleticsCheck(command.DifficultyClass, command.RollResult,
                                         command.ProficiencyBonus, command.StrengthModifier);
            await _eventStore.Save(character, cancellationToken);
        }

        public async Task Handle(MakeAcrobaticsCheckForMovement command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.MakeAcrobaticsCheck(command.DifficultyClass, command.RollResult,
                                          command.ProficiencyBonus, command.DexterityModifier);
            await _eventStore.Save(character, cancellationToken);
        }

        // ---------- ������� ----------

        public async Task Handle(TakeFallDamage command, CancellationToken cancellationToken)
        {
            var character = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                            ?? throw new InvalidAction("Character not found");
            character.TakeFallDamage(command.FallDistanceFeet);
            await _eventStore.Save(character, cancellationToken);
        }
    }
}