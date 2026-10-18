namespace AVAChuckieEgg.Engine;

/// <summary>The screen: the score line along the top, then the henhouse, 32 squares by 22.</summary>
public static class Field
{
    public const double Step = 1.0 / 60;
    public const double Width = 1024, Height = 768;
    public const int Tile = 32, Cols = 32, Rows = 22;
    /// <summary>Where the henhouse starts down the screen.</summary>
    public const double Top = 64;
}

/// <summary>What a square of the henhouse is.</summary>
public enum Tile : byte
{
    Empty,
    /// <summary>A platform: stood on from above; jumped up through from below.</summary>
    Floor,
    /// <summary>A ladder.</summary>
    Ladder,
    /// <summary>A ladder where it comes up through a platform: both.</summary>
    LadderFloor,
    Egg,
    Grain,
}

/// <summary>A henhouse: its squares, where Harry starts, where the hens start.</summary>
public sealed class HouseDef
{
    public int Layout;
    public Tile[,] Grid = new Tile[Field.Cols, Field.Rows];
    public (int Col, int Row) Start;
    public List<(int Col, int Row)> Hens = [];
    public int Eggs;

    public Tile At(int c, int r) => c < 0 || c >= Field.Cols || r < 0 || r >= Field.Rows ? Tile.Empty : Grid[c, r];
}

/// <summary>
/// The eight henhouses. Each is built from a seed: the ground, four platform floors above it broken into stretches,
/// ladders between them, twelve eggs, piles of grain, and the hens. It's kept only if the map of Harry's moves (tried
/// in the game's own physics) shows every egg can be reached from the start; otherwise it's built again.
/// </summary>
public static class HouseBook
{
    public const int Layouts = 8, EggCount = 12;
    public static readonly int[] FloorRows = [21, 17, 13, 9, 5];

    private static readonly HouseDef?[] Built = new HouseDef?[Layouts];
    private static readonly Lock Gate = new();

    /// <summary>Henhouse n (0 to 7).</summary>
    public static HouseDef Layout(int n)
    {
        lock (Gate) return Built[n] ??= Prove(n);
    }

    public static bool IsFloor(Tile t) => t is Tile.Floor or Tile.LadderFloor;
    public static bool IsLadder(Tile t) => t is Tile.Ladder or Tile.LadderFloor;

    private static HouseDef Prove(int n)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            var def = Build(n, 1983 + n * 101 + attempt * 7);
            var g = Graph.Build(def.Grid);
            var reach = g.Reach(Spot.Standing(def.Start.Col, def.Start.Row));
            bool ok = true;
            for (int c = 0; c < Field.Cols && ok; c++)
                for (int r = 0; r < Field.Rows && ok; r++)
                    if (def.Grid[c, r] is Tile.Egg or Tile.Grain && !reach.Contains(Spot.Standing(c, r + 1))) ok = false;
            if (ok) return def;
        }
        throw new InvalidOperationException($"henhouse {n} could not be built");
    }

    public static HouseDef Build(int n, int seed)
    {
        var dice = new Dice(seed);
        var def = new HouseDef { Layout = n };
        var g = def.Grid;
        for (int c = 0; c < Field.Cols; c++) g[c, 21] = Tile.Floor;
        // The platforms: each floor broken into stretches with gaps between.
        foreach (int r in FloorRows.Skip(1))
        {
            int c = r == 5 ? 0 : dice.Next(3);
            while (c < Field.Cols)
            {
                int len = 5 + dice.Next(9);
                for (int k = c; k < Math.Min(Field.Cols, c + len); k++) g[k, r] = Tile.Floor;
                c += len + 1 + dice.Next(3);
            }
        }
        // The duck's cage stands at the top left: its floor is whole.
        for (int k = 0; k <= 4; k++) g[k, 5] = Tile.Floor;
        // Ladders between each floor and the next, where both have platform.
        for (int i = 0; i < FloorRows.Length - 1; i++)
        {
            int lo = FloorRows[i], hi = FloorRows[i + 1];
            var spots = Enumerable.Range(1, Field.Cols - 2)
                .Where(c => g[c, lo] == Tile.Floor && g[c, hi] == Tile.Floor && g[c - 1, hi] == Tile.Floor && g[c + 1, hi] == Tile.Floor && !(hi == 5 && c <= 5))
                .ToList();
            int want = 2 + dice.Next(2), placed = 0;
            for (int tries = 0; tries < 40 && placed < want && spots.Count > 0; tries++)
            {
                int c = spots[dice.Next(spots.Count)];
                if (Enumerable.Range(c - 3, 7).Any(k => k >= 0 && k < Field.Cols && IsLadder(g[k, hi + 1]))) continue;
                g[c, hi] = Tile.LadderFloor;
                for (int r = hi + 1; r < lo; r++) g[c, r] = Tile.Ladder;
                placed++;
            }
        }
        def.Start = (2, 21);
        // Eggs and grain on the platforms, not on a ladder, not where Harry starts.
        var places = new List<(int C, int R)>();
        foreach (int r in FloorRows)
            for (int c = 0; c < Field.Cols; c++)
                if (g[c, r] == Tile.Floor && g[c, r - 1] == Tile.Empty && !(r == 21 && c < 6) && !(r == 5 && c <= 4))
                    places.Add((c, r - 1));
        for (int k = places.Count - 1; k > 0; k--)
        {
            int j = dice.Next(k + 1);
            (places[k], places[j]) = (places[j], places[k]);
        }
        var used = new List<(int C, int R)>();
        foreach (var p in places)
        {
            if (used.Any(u => u.R == p.R && Math.Abs(u.C - p.C) < 2)) continue;
            g[p.C, p.R] = used.Count < EggCount ? Tile.Egg : Tile.Grain;
            used.Add(p);
            if (used.Count >= EggCount + 4) break;
        }
        def.Eggs = EggCount;
        // The hens, on the upper floors to start with.
        int hens = 3 + n % 3;
        for (int k = 0; k < hens; k++)
        {
            int r = FloorRows[1 + k % 4];
            var free = Enumerable.Range(5, Field.Cols - 6).Where(c => g[c, r] == Tile.Floor && g[c, r - 1] == Tile.Empty && !def.Hens.Contains((c, r))).ToList();
            if (free.Count > 0) def.Hens.Add((free[dice.Next(free.Count)], r));
        }
        return def;
    }
}
