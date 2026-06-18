namespace Brackets.Core.Generation;

internal static class Seeding
{
    /// <summary>Smallest power of two greater than or equal to <paramref name="n"/>.</summary>
    public static int NextPowerOfTwo(int n)
    {
        int p = 1;
        while (p < n)
        {
            p <<= 1;
        }

        return p;
    }

    /// <summary>
    /// Standard single-elimination seed order for a bracket of size <paramref name="size"/> (a power of two).
    /// Position i and i+1 (for even i) form a first-round matchup, so seed 1 meets seed <c>size</c>, etc.
    /// Built recursively: each round doubles the field, mirroring every seed s with (2m+1-s).
    /// </summary>
    public static int[] SeedOrder(int size)
    {
        int[] order = { 1 };
        int m = 1;
        while (m < size)
        {
            int next = m * 2;
            var expanded = new int[next];
            for (int i = 0; i < m; i++)
            {
                expanded[2 * i] = order[i];
                expanded[2 * i + 1] = next + 1 - order[i];
            }

            order = expanded;
            m = next;
        }

        return order;
    }
}
