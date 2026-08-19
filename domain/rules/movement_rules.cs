using dnd_game.domain.value_objects;
using dnd_game.Domain.ValueObjects;
using dnd_game.Infrastructure.World;

namespace dnd_game.Domain.Rules
{
    public static class MovementRules
    {
        // --------------------------------------------------------------------------------
        // Базовая скорость
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Возвращает эффективную скорость с учётом модификаторов (состояния, экипировка).
        /// </summary>
        public static int GetEffectiveSpeed(int baseSpeed, bool isEncumbered = false, bool isHeavilyEncumbered = false)
        {
            int speed = baseSpeed;
            if (isEncumbered) speed -= 10;
            if (isHeavilyEncumbered) speed -= 20;
            return Math.Max(0, speed);
        }

        // --------------------------------------------------------------------------------
        // Стоимость перемещения по местности
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Возвращает стоимость перемещения на одну клетку (в футах) для заданного типа местности.
        /// </summary>
        public static int GetMovementCostPerCell(CellTerrain terrain)
        {
            return terrain switch
            {
                CellTerrain.Normal => 5,
                CellTerrain.Road => 5,
                CellTerrain.Difficult => 10,      // труднопроходимая: стоимость удвоена
                CellTerrain.ShallowWater => 10,
                CellTerrain.Ice => 10,
                CellTerrain.Mud => 10,
                CellTerrain.Rubble => 10,
                CellTerrain.Thorns => 10,
                CellTerrain.DeepWater => -1,      // непроходимо
                CellTerrain.Lava => -1,
                CellTerrain.Wall => -1,
                // двери, окна — проходимы, если открыты (стоимость обычная)
                CellTerrain.Door => 5,
                CellTerrain.Window => 5,
                CellTerrain.HiddenDoor => 5,
                _ => 5
            };
        }

        /// <summary>
        /// Проверяет, можно ли войти в клетку (проходимость + достаточность скорости).
        /// </summary>
        public static bool CanEnterCell(CellTerrain terrain, int remainingSpeed)
        {
            int cost = GetMovementCostPerCell(terrain);
            if (cost < 0) return false;            // непроходимо
            return remainingSpeed >= cost;
        }

        // --------------------------------------------------------------------------------
        // Расчёт пути и стоимости
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Вычисляет общую стоимость перемещения по заданному пути (в футах) с учётом местности.
        /// </summary>
        public static int CalculatePathCost(IGridProvider grid, List<Position> path)
        {
            if (path == null || path.Count < 2) return 0;
            int totalCost = 0;
            // Начинаем со второй клетки (первая — текущая позиция)
            for (int i = 1; i < path.Count; i++)
            {
                var pos = path[i];
                var cell = grid.GetCell(pos.X, pos.Y);
                int cost = GetMovementCostPerCell(cell.Terrain);
                if (cost < 0) return -1; // путь содержит непроходимую клетку
                totalCost += cost;
            }
            return totalCost;
        }

        /// <summary>
        /// Проверяет, может ли персонаж переместиться по заданному пути с учётом оставшейся скорости.
        /// </summary>
        public static bool CanTraversePath(IGridProvider grid, List<Position> path, int remainingSpeed)
        {
            int cost = CalculatePathCost(grid, path);
            if (cost < 0) return false;
            return cost <= remainingSpeed;
        }

        // --------------------------------------------------------------------------------
        // Действия, связанные с движением
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Действие Dash удваивает эффективную скорость на текущий ход.
        /// </summary>
        public static int ApplyDash(int baseSpeed) => baseSpeed * 2;

        /// <summary>
        /// Действие Disengage позволяет покинуть угрожаемую зону без провоцирования атак.
        /// </summary>
        public static bool CanDisengage(bool hasAction, bool isInMelee) => hasAction && isInMelee;

        /// <summary>
        /// Действие Hide требует проверки Скрытности и наличия укрытия.
        /// </summary>
        public static bool CanHide(bool hasAction, bool hasCover) => hasAction && hasCover;

        // --------------------------------------------------------------------------------
        // Проверки навыков, связанные с движением
        // --------------------------------------------------------------------------------

        public static bool AthleticsCheckSuccess(int roll, int dc) => roll >= dc;
        public static bool AcrobaticsCheckSuccess(int roll, int dc) => roll >= dc;

        // --------------------------------------------------------------------------------
        // Падение
        // --------------------------------------------------------------------------------

        public static int FallDamage(int fallDistanceFeet)
        {
            int diceCount = Math.Min(fallDistanceFeet / 10, 20);
            // Средний урон 3.5 за кубик, но возвращаем количество кубиков для расчёта
            return diceCount;
        }
    }
}