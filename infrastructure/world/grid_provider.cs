using dnd_game.domain.value_objects;
using System.Collections.Generic;
using System;

namespace dnd_game.Infrastructure.World
{
    // ---------- Определения типов (здесь, чтобы не искать в других файлах) ----------

    public enum GridType
    {
        Square,
        Hex
    }

    public enum CellTerrain
    {
        Normal,
        Difficult,
        Road,
        ShallowWater,
        DeepWater,
        Lava,
        Wall,
        Window,
        Door,
        HiddenDoor,
        Ice,
        Mud,
        Rubble,
        Thorns
    }

    public enum LightLevel
    {
        Darkness,
        Dim,
        Bright
    }

    public class Cell
    {
        public CellTerrain Terrain { get; set; } = CellTerrain.Normal;
        public int Height { get; set; }
        public LightLevel Light { get; set; } = LightLevel.Bright;
        public bool BlocksVision { get; set; }
        public bool BlocksMovement { get; set; }
    }

    // ---------- Интерфейс IGridProvider (если он не определён в другом месте) ----------

    public interface IGridProvider
    {
        int Width { get; }
        int Height { get; }
        GridType Type { get; }
        bool InBounds(int x, int y);
        bool IsWalkable(int x, int y);
        bool IsDifficultTerrain(int x, int y);
        Cell GetCell(int x, int y);
        void SetCell(int x, int y, Cell cell);
        int GetDistance(Position from, Position to);
        bool LineOfSight(Position from, Position to);
        List<Position> FindPath(Position from, Position to);
        string GetCoverType(Position attacker, Position target);
    }

    // ---------- Реализация GridProvider ----------

    public class GridProvider : IGridProvider
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public GridType Type { get; set; }

        private Cell[,] _grid;

        public GridProvider(int width = 100, int height = 100, GridType type = GridType.Square)
        {
            Width = width;
            Height = height;
            Type = type;
            _grid = new Cell[width, height];
            InitializeGrid();
        }

        private void InitializeGrid()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    _grid[x, y] = new Cell();
        }

        public Cell GetCell(int x, int y)
        {
            if (!InBounds(x, y))
                throw new ArgumentOutOfRangeException(nameof(x), "Coordinates out of bounds.");
            return _grid[x, y];
        }

        public void SetCell(int x, int y, Cell cell)
        {
            if (!InBounds(x, y))
                throw new ArgumentOutOfRangeException(nameof(x), "Coordinates out of bounds.");
            _grid[x, y] = cell;
        }

        public bool IsWalkable(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            var cell = _grid[x, y];
            if (cell.BlocksMovement) return false;

            switch (cell.Terrain)
            {
                case CellTerrain.Wall:
                case CellTerrain.DeepWater:
                case CellTerrain.Lava:
                    return false;
                default:
                    return true;
            }
        }

        public bool IsDifficultTerrain(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            var terrain = _grid[x, y].Terrain;

            return terrain == CellTerrain.Difficult ||
                   terrain == CellTerrain.ShallowWater ||
                   terrain == CellTerrain.Ice ||
                   terrain == CellTerrain.Mud ||
                   terrain == CellTerrain.Rubble ||
                   terrain == CellTerrain.Thorns;
        }

        public int GetDistance(Position a, Position b)
        {
            if (Type == GridType.Square)
                return a.ChebyshevDistanceInSquares(b);
            else
                return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        public bool LineOfSight(Position from, Position to)
        {
            int x0 = from.X, y0 = from.Y;
            int x1 = to.X, y1 = to.Y;
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            int currentX = x0, currentY = y0;
            while (!(currentX == x1 && currentY == y1))
            {
                int e2 = err * 2;
                if (e2 > -dy) { err -= dy; currentX += sx; }
                if (e2 < dx) { err += dx; currentY += sy; }

                if (!InBounds(currentX, currentY)) return false;
                if (_grid[currentX, currentY].BlocksVision) return false;
            }
            return true;
        }

        public List<Position> FindPath(Position from, Position to)
        {
            if (!IsWalkable(to.X, to.Y)) return new List<Position>();

            var open = new SortedSet<(int f, int x, int y)>();
            var cameFrom = new Dictionary<(int, int), (int, int)>();
            var gScore = new Dictionary<(int, int), int>();
            var fScore = new Dictionary<(int, int), int>();
            var directions = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

            gScore[(from.X, from.Y)] = 0;
            fScore[(from.X, from.Y)] = Heuristic(from, to);
            open.Add((fScore[(from.X, from.Y)], from.X, from.Y));

            while (open.Count > 0)
            {
                var current = open.Min;
                open.Remove(current);
                int cx = current.x, cy = current.y;
                if (cx == to.X && cy == to.Y)
                    return ReconstructPath(cameFrom, (cx, cy));

                foreach (var dir in directions)
                {
                    int nx = cx + dir.Item1, ny = cy + dir.Item2;
                    if (!InBounds(nx, ny) || !IsWalkable(nx, ny)) continue;
                    int moveCost = IsDifficultTerrain(nx, ny) ? 2 : 1;
                    if (dir.Item1 != 0 && dir.Item2 != 0) moveCost = 2;
                    int tentativeG = gScore[(cx, cy)] + moveCost;
                    if (!gScore.ContainsKey((nx, ny)) || tentativeG < gScore[(nx, ny)])
                    {
                        cameFrom[(nx, ny)] = (cx, cy);
                        gScore[(nx, ny)] = tentativeG;
                        int f = tentativeG + Heuristic(new Position(nx, ny), to);
                        fScore[(nx, ny)] = f;
                        open.Add((f, nx, ny));
                    }
                }
            }
            return new List<Position>();
        }

        private int Heuristic(Position a, Position b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

        private List<Position> ReconstructPath(Dictionary<(int, int), (int, int)> cameFrom, (int, int) current)
        {
            var path = new List<Position> { new Position(current.Item1, current.Item2) };
            while (cameFrom.TryGetValue(current, out current))
                path.Add(new Position(current.Item1, current.Item2));
            path.Reverse();
            return path;
        }

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        public string GetCoverType(Position attacker, Position target)
        {
            if (!LineOfSight(attacker, target)) return "Full";
            return "None";
        }
    }
}