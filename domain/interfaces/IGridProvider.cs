// domain/interfaces/IGridProvider.cs (примерное расположение)
using dnd_game.domain.value_objects;
using dnd_game.Domain.ValueObjects;

namespace dnd_game.Domain.Interfaces  // или ваше пространство имён
{
    public interface IGridProvider
    {
        bool InBounds(int x, int y);
        int GetDistance(Position from, Position to);
        bool LineOfSight(Position from, Position to);
        GridCell GetCell(int x, int y);
    }

    // Вспомогательный тип клетки (если ещё не определён)
    public class GridCell
    {
        public bool BlocksVision { get; set; }
        // другие свойства клетки при необходимости
    }

}