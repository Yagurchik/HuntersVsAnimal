public interface IEnemyDefeatHandler
{
    // Return true when this component replaces the normal permanent defeat.
    bool TryHandleDefeat(Brick enemy);
}
