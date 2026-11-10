using System.Collections.Generic;

// Each nested damage callback owns its snapshot until it returns.
public static class CombatSnapshotPool<T>
{
    private static readonly Stack<List<T>> Available = new Stack<List<T>>(8);
    public static List<T> Rent() => Available.Count > 0 ? Available.Pop() : new List<T>(16);
    public static void Return(List<T> values)
    {
        if (values == null)
        {
            return;
        }

        values.Clear();
        if (Available.Count < 16)
        {
            Available.Push(values);
        }
    }
}
