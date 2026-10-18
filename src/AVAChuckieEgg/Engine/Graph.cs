namespace AVAChuckieEgg.Engine;

/// <summary>A move from one place to another: walk a square, jump (left, up or right), or climb a square.</summary>
public readonly record struct Move(int Dx, int Dy, bool Jump);

/// <summary>Where Harry is: the column his middle is in, and the row his feet are at (standing on it, or on a ladder).</summary>
public readonly record struct Spot(int Col, int Row)
{
    public double X => (Col + 0.5) * Field.Tile;
    public double Feet => Row * Field.Tile;
    public static Spot Of(Harry h) => new(Sim.Col(h.X), (int)Math.Round(h.Y / Field.Tile));
    public static Spot Standing(int col, int row) => new(col, row);
}

/// <summary>
/// Every place Harry can stop in a henhouse (on a platform, or on a ladder at a square's height), and where each move
/// takes him from there, found by trying the moves out (with no hens about) in the game's own physics. The henhouses
/// are kept only if this shows every egg can be reached; the computer player plans its route by it.
/// </summary>
public sealed class Graph
{
    public static readonly Move[] Moves =
        [new(-1, 0, false), new(1, 0, false), new(-1, 0, true), new(0, 0, true), new(1, 0, true), new(0, -1, false), new(0, 1, false)];
    public readonly Dictionary<Spot, List<(Move Move, Spot To, int Ticks)>> Edges = [];

    public static Graph Build(Tile[,] grid)
    {
        var g = new Graph();
        var probe = Probe(grid);
        for (int c = 0; c < Field.Cols; c++)
            for (int r = 1; r < Field.Rows; r++)
            {
                var s = new Spot(c, r);
                if (!CanStop(probe, s)) continue;
                var list = new List<(Move, Spot, int)>();
                foreach (var mv in Moves)
                    if (Try(probe, s, mv) is { } to) list.Add((mv, to.Spot, to.Ticks));
                g.Edges[s] = list;
            }
        return g;
    }

    public static World Probe(Tile[,] grid) => new() { Probe = true, Grid = grid, Def = new HouseDef { Grid = grid } };

    /// <summary>On a platform, or on a ladder.</summary>
    public static bool OnFloor(World w, Spot s) => HouseBook.IsFloor(w.At(s.Col, s.Row));

    public static bool CanStop(World w, Spot s) => OnFloor(w, s) || Sim.Ladder(w, s.Col, s.Feet) is { } l && s.Feet >= l.Top && s.Feet <= l.Bottom;

    /// <summary>Puts Harry at a spot: standing, or on the ladder.</summary>
    public static void Place(World w, Spot s) => w.Harry = new Harry { X = s.X, Y = s.Feet, Climbing = !OnFloor(w, s) };

    /// <summary>A move tried from a spot: where it ends (stopped, alive), and how long it took.</summary>
    public static (Spot Spot, int Ticks)? Try(World probe, Spot s, Move mv)
    {
        var w = probe;
        Place(w, s);
        int ticks = 0;
        foreach (var c in Script(w, mv))
        {
            Sim.Step(w, c);
            ticks++;
            if (w.Harry.Dead || ticks > 300) return null;
        }
        if (w.Harry.Airborne) return null;
        var end = Spot.Of(w.Harry);
        if (end == s || !CanStop(w, end)) return null;
        return (end, ticks);
    }

    /// <summary>
    /// The controls for a move, one step at a time, ending once he's stopped: a walk goes to the middle of the next
    /// square (or off the edge and down); a jump goes until he lands; a climb goes one square up or down the ladder.
    /// </summary>
    public static IEnumerable<Controls> Script(World w, Move mv)
    {
        var h = w.Harry;
        if (mv.Jump)
        {
            if (h.Climbing) yield break;
            yield return new Controls(mv.Dx, 0, true);
            for (int i = 0; i < 300 && h.Airborne && !h.Dead; i++) yield return new Controls(mv.Dx);
            yield break;
        }
        if (mv.Dy != 0)
        {
            double target = (Math.Round(h.Y / Field.Tile) + mv.Dy) * Field.Tile;
            for (int i = 0; i < 120 && !h.Dead; i++)
            {
                if (mv.Dy < 0 ? h.Y <= target + 0.01 : h.Y >= target - 0.01) yield break;
                double before = h.Y;
                yield return new Controls(0, mv.Dy);
                if (Math.Abs(h.Y - before) < 0.01) yield break;
            }
            yield break;
        }
        double tx = (Math.Floor(h.X / Field.Tile) + mv.Dx + 0.5) * Field.Tile;
        for (int i = 0; i < 300 && !h.Dead; i++)
        {
            if (h.Airborne) { yield return default; continue; }
            if (mv.Dx > 0 ? h.X >= tx - 1.1 : h.X <= tx + 1.1) yield break;
            double before = h.X;
            yield return new Controls(mv.Dx);
            if (Math.Abs(h.X - before) < 0.01 && !h.Airborne) yield break;
        }
    }

    /// <summary>Every place that can be reached from one.</summary>
    public HashSet<Spot> Reach(Spot from)
    {
        var seen = new HashSet<Spot> { from };
        var q = new Queue<Spot>(seen);
        while (q.Count > 0)
        {
            var s = q.Dequeue();
            if (!Edges.TryGetValue(s, out var list)) continue;
            foreach (var (_, to, _) in list)
                if (seen.Add(to)) q.Enqueue(to);
        }
        return seen;
    }
}
