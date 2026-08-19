using dnd_game.Application.Projections;
using dnd_game.Domain.Interfaces;
using dnd_game.domain.value_objects;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace dnd_game.Infrastructure.World
{
    /// <summary>
    /// Типы чувств, используемые при расчёте видимости.
    /// </summary>
    public enum SenseType
    {
        NormalVision,
        Darkvision,
        Blindsight,
        Tremorsense,
        Truesight
    }

    /// <summary>
    /// Параметры зрения существа.
    /// </summary>
    public class VisionProfile
    {
        public List<SenseType> Senses { get; set; } = new() { SenseType.NormalVision };
        public int DarkvisionRange { get; set; } = 60;    // футы
        public int BlindsightRange { get; set; } = 30;
        public int TremorsenseRange { get; set; } = 60;
        public int TruesightRange { get; set; } = 120;
        public bool IsBlinded { get; set; }                // полностью блокирует зрение
    }

    /// <summary>
    /// Результат вычисления видимости.
    /// </summary>
    public class VisibilityResult
    {
        public HashSet<(int x, int y)> VisibleCells { get; set; } = new();
        public HashSet<(int x, int y)> DimlyVisibleCells { get; set; } = new(); // для Darkvision/Dim Light
        public HashSet<(int x, int y)> TremorSensedCells { get; set; } = new();
    }

    /// <summary>
    /// Вычислитель видимости, соответствующий правилам DnD 5e.
    /// </summary>
    public class VisibilityCalculator
    {
        private readonly IGridProvider _grid;
        private readonly ILogger<VisibilityCalculator>? _logger;

        public VisibilityCalculator(IGridProvider grid, ILogger<VisibilityCalculator>? logger = null)
        {
            _grid = grid;
            _logger = logger;
        }

        /// <summary>
        /// Рассчитать поле зрения (FOV) для наблюдателя на сетке с учётом его профиля и освещения.
        /// </summary>
        /// <param name="originX">X координата наблюдателя.</param>
        /// <param name="originY">Y координата наблюдателя.</param>
        /// <param name="visionProfile">Профиль зрения (если null, только базовое зрение 60 футов).</param>
        /// <returns>Результат с наборами видимых клеток.</returns>
        public VisibilityResult CalculateFieldOfView(int originX, int originY, VisionProfile? visionProfile = null)
        {
            visionProfile ??= new VisionProfile();
            var result = new VisibilityResult();

            // Если ослеплён, обычное зрение отключено, но Blindsight/Tremorsense могут работать
            if (!visionProfile.IsBlinded)
            {
                // Truesight видит всё в радиусе, игнорируя препятствия
                if (visionProfile.Senses.Contains(SenseType.Truesight))
                {
                    AddTruesightCells(originX, originY, visionProfile.TruesightRange, result.VisibleCells);
                }
                else
                {
                    // Обычное зрение + Darkvision — raycasting с учётом освещения
                    ProcessVision(originX, originY, visionProfile, result);
                }
            }

            // Blindsight — не зависит от зрения, но блокируется стенами (LineOfSight)
            if (visionProfile.Senses.Contains(SenseType.Blindsight))
            {
                AddBlindsightCells(originX, originY, visionProfile.BlindsightRange, result.VisibleCells);
            }

            // Tremorsense — чувствует вибрации через землю, игнорирует препятствия (упрощённо)
            if (visionProfile.Senses.Contains(SenseType.Tremorsense))
            {
                AddTremorsenseCells(originX, originY, visionProfile.TremorsenseRange, result.TremorSensedCells);
            }

            return result;
        }

        /// <summary>
        /// Проверяет, видит ли наблюдатель цель (Line of Sight) с учётом освещения и препятствий.
        /// </summary>
        public bool HasLineOfSight(Position observer, Position target, VisionProfile visionProfile)
        {
            if (!_grid.InBounds(observer.X, observer.Y) || !_grid.InBounds(target.X, target.Y))
                return false;

            // Truesight игнорирует всё
            if (visionProfile.Senses.Contains(SenseType.Truesight))
                return _grid.GetDistance(observer, target) <= visionProfile.TruesightRange;

            if (visionProfile.IsBlinded)
                return false;

            int distance = _grid.GetDistance(observer, target);

            // Проверка радиуса в зависимости от типа зрения и освещения
            LightLevel light = GetEffectiveLightAt(target);
            if (light == LightLevel.Bright)
            {
                // Нормальное зрение: без ограничения дистанции, кроме препятствий
                if (distance > 1200) return false;
            }
            else if (light == LightLevel.Dim)
            {
                if (!visionProfile.Senses.Contains(SenseType.Darkvision))
                {
                    // В тусклом свете без тёмного зрения дистанция ограничена 60 футами
                    if (distance > 60) return false;
                }
                // С тёмным зрением dim воспринимается как яркий
            }
            else // Darkness
            {
                if (!visionProfile.Senses.Contains(SenseType.Darkvision))
                    return false;
                if (distance > visionProfile.DarkvisionRange)
                    return false;
            }

            // Проверка линии прямой видимости через сетку
            return _grid.LineOfSight(observer, target);
        }

        // ---------- Приватные методы ----------

        /// <summary>
        /// Raycasting по кругу с шагом 1 градус. Останавливается на препятствиях, учитывает освещение.
        /// </summary>
        private void ProcessVision(int ox, int oy, VisionProfile profile, VisibilityResult result)
        {
            int maxRadius = 200; // практический предел видимости
            bool hasDarkvision = profile.Senses.Contains(SenseType.Darkvision);

            for (int angle = 0; angle < 360; angle += 1)
            {
                double rad = angle * Math.PI / 180.0;
                double dx = Math.Cos(rad);
                double dy = Math.Sin(rad);

                for (int step = 1; step <= maxRadius; step++)
                {
                    int newX = ox + (int)Math.Round(dx * step);
                    int newY = oy + (int)Math.Round(dy * step);
                    if (!_grid.InBounds(newX, newY)) break;

                    LightLevel light = GetEffectiveLightAt(new Position(newX, newY));
                    bool cellVisible = false;
                    bool cellDim = false;

                    if (light == LightLevel.Bright)
                    {
                        cellVisible = true;
                    }
                    else if (light == LightLevel.Dim)
                    {
                        if (hasDarkvision)
                            cellVisible = true;  // darkvision видит dim как bright
                        else
                            cellDim = true;      // видит как dim, не полная видимость
                    }
                    else // Darkness
                    {
                        if (hasDarkvision && step <= profile.DarkvisionRange)
                            cellDim = true;      // darkvision в темноте видит как dim
                        else
                            break;              // дальше этой клетки не видно
                    }

                    if (cellVisible)
                        result.VisibleCells.Add((newX, newY));
                    else if (cellDim)
                        result.DimlyVisibleCells.Add((newX, newY));

                    // Если клетка блокирует зрение, дальше по лучу не идём
                    if (_grid.GetCell(newX, newY).BlocksVision)
                    {
                        // Сама клетка стены уже добавлена (если видима), луч останавливается
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Truesight: добавляет все клетки в радиусе, игнорируя препятствия.
        /// </summary>
        private void AddTruesightCells(int ox, int oy, int range, HashSet<(int x, int y)> cells)
        {
            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                {
                    int nx = ox + dx, ny = oy + dy;
                    if (_grid.InBounds(nx, ny) && _grid.GetDistance(new Position(ox, oy), new Position(nx, ny)) <= range)
                        cells.Add((nx, ny));
                }
        }

        /// <summary>
        /// Blindsight: добавляет клетки в радиусе, но только если есть прямая видимость (LineOfSight).
        /// Не зависит от освещения.
        /// </summary>
        private void AddBlindsightCells(int ox, int oy, int range, HashSet<(int x, int y)> cells)
        {
            var observerPos = new Position(ox, oy);
            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                {
                    int nx = ox + dx, ny = oy + dy;
                    if (!_grid.InBounds(nx, ny)) continue;
                    var targetPos = new Position(nx, ny);
                    if (_grid.GetDistance(observerPos, targetPos) <= range)
                    {
                        // Blindsight не проникает сквозь стены (LineOfSight)
                        if (_grid.LineOfSight(observerPos, targetPos))
                            cells.Add((nx, ny));
                    }
                }
        }

        /// <summary>
        /// Tremorsense: добавляет все клетки в радиусе, игнорируя препятствия.
        /// Упрощённо: считаем, что все клетки в радиусе доступны.
        /// </summary>
        private void AddTremorsenseCells(int ox, int oy, int range, HashSet<(int x, int y)> cells)
        {
            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                {
                    int nx = ox + dx, ny = oy + dy;
                    if (_grid.InBounds(nx, ny) && _grid.GetDistance(new Position(ox, oy), new Position(nx, ny)) <= range)
                        cells.Add((nx, ny));
                }
        }

        /// <summary>
        /// Получить уровень освещения в заданной клетке.
        /// В текущей реализации — всегда Bright.
        /// В реальном проекте нужно интегрировать с системой освещения (например, через WorldState).
        /// </summary>
        private LightLevel GetEffectiveLightAt(Position pos)
        {
            // РЈСЂРѕРІРµРЅСЊ РѕСЃРІРµС‰С‘РЅРЅРѕСЃС‚Рё Р±РµСЂС‘С‚СЃСЏ РЅРµРїРѕСЃСЂРµРґСЃС‚РІРµРЅРЅРѕ РёР· РєР»РµС‚РєРё РіСЂРёРґР° (Cell.Light),
            // РєРѕС‚РѕСЂР°СЏ РІС‹СЃС‚Р°РІР»СЏРµС‚СЃСЏ РїСЂРё СЃРѕР·РґР°РЅРёРё/СЂРµРґР°РєС‚РёСЂРѕРІР°РЅРёРё РєР°СЂС‚С‹ (РёСЃС‚РѕС‡РЅРёРєРё СЃРІРµС‚Р°,
            // Р·Р°РєР»РёРЅР°РЅРёСЏ С‚РёРїР° Light/Darkness, РґРµРЅСЊ/РЅРѕС‡СЊ).
            if (!_grid.InBounds(pos.X, pos.Y))
                return LightLevel.Darkness;
            return _grid.GetCell(pos.X, pos.Y).Light;
        }
    }
}