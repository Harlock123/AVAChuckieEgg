namespace AVAChuckieEgg.Engine;

/// <summary>
/// The computer Harry, for the demonstration and the tests. It knows every place it can stop in the henhouse and where
/// each walk, jump and climb takes it (the henhouse's <see cref="Graph"/>), and plans the quickest way to the nearest
/// egg (or grain) left. Before each move it plays the move out on a copy of the henhouse, hens and duck and all, and
/// if one would get it, it waits for the moment it wouldn't, or takes another way.
/// </summary>
public sealed class Pilot
{
    private readonly Queue<Controls> _plan = new();
    private Graph? _graph;
    private Dictionary<Spot, List<(Spot From, int Ticks)>>? _back;
    private Tile[,]? _for;
    public const int WaitStep = 6, MaxWait = 240, After = 20;

    public string Doing { get; private set; } = "";

    public void Reset()
    {
        _plan.Clear();
        Doing = "";
    }

    public Controls Decide(GameEngine g) => g.Phase != Phase.Play ? default : Decide(g.World);

    public Controls Decide(World w)
    {
        var h = w.Harry;
        if (h.Dead) { _plan.Clear(); return default; }
        if (_plan.Count > 0) return _plan.Dequeue();
        if (h.Airborne) return default;
        if (_for != w.Def.Grid)
        {
            _for = w.Def.Grid;
            _graph = Graph.Build(Clean(w.Def.Grid));
            _back = [];
            foreach (var (from, list) in _graph.Edges)
                foreach (var (_, to, ticks) in list)
                {
                    if (!_back.TryGetValue(to, out var l)) _back[to] = l = [];
                    l.Add((from, ticks));
                }
        }
        var here = Spot.Of(h);
        if (!h.Climbing && Math.Abs(h.X - here.X) > 2)
        {
            // Into the middle of the square.
            Doing = "lining up";
            int dx = Math.Sign(here.X - h.X);
            for (int i = 0; i < 16 && Math.Abs(h.X + dx * (i + 1) * Harry.Speed * Field.Step - here.X) < Math.Abs(h.X + dx * i * Harry.Speed * Field.Step - here.X); i++)
                _plan.Enqueue(new Controls(dx));
            if (_plan.Count > 0) return _plan.Dequeue();
        }
        var far = Distances(w);
        int now = far.TryGetValue(here, out int d0) ? d0 : int.MaxValue;
        var options = new List<(Move Move, int Cost)>();
        if (_graph!.Edges.TryGetValue(here, out var edges))
            foreach (var (mv, to, ticks) in edges)
                if (far.TryGetValue(to, out int d) && d < now) options.Add((mv, ticks + d));
        options.Sort((a, b) => a.Cost.CompareTo(b.Cost));
        int best = int.MaxValue;
        List<Controls>? pick = null;
        foreach (var (mv, cost) in options.Take(4))
            for (int wait = 0; wait <= MaxWait && wait + cost < best; wait += WaitStep)
            {
                if (Rehearse(w, mv, wait) is not { } run) continue;
                best = wait + cost;
                pick = run;
                break;
            }
        if (pick != null)
        {
            Doing = best > options[0].Cost + WaitStep ? "waiting for a hen" : "after an egg";
            foreach (var c in pick) _plan.Enqueue(c);
            return _plan.Dequeue();
        }
        // Nothing safe: any move at all that keeps him alive (away from the hen), or stand still.
        if (_graph.Edges.TryGetValue(here, out var all))
            foreach (var (mv, _, _) in all.OrderBy(_ => w.Dice.Next()))
                if (Rehearse(w, mv, 0) is { } run)
                {
                    Doing = "getting out of the way";
                    foreach (var c in run) _plan.Enqueue(c);
                    return _plan.Dequeue();
                }
        Doing = "trapped";
        return default;
    }

    /// <summary>The henhouse without its eggs and grain (they don't change where Harry can go).</summary>
    private static Tile[,] Clean(Tile[,] grid)
    {
        var g = (Tile[,])grid.Clone();
        for (int c = 0; c < Field.Cols; c++)
            for (int r = 0; r < Field.Rows; r++)
                if (g[c, r] is Tile.Egg or Tile.Grain) g[c, r] = Tile.Empty;
        return g;
    }

    /// <summary>How long it takes from each place to reach the nearest egg (or grain) left.</summary>
    private Dictionary<Spot, int> Distances(World w)
    {
        var dist = new Dictionary<Spot, int>();
        var q = new PriorityQueue<Spot, int>();
        for (int c = 0; c < Field.Cols; c++)
            for (int r = 0; r < Field.Rows; r++)
                if (w.Grid[c, r] is Tile.Egg or Tile.Grain)
                {
                    // Grain is worth a detour, but eggs come first.
                    var s = new Spot(c, r + 1);
                    int start = w.Grid[c, r] == Tile.Egg ? 0 : 120;
                    if (!dist.TryGetValue(s, out int old) || start < old) { dist[s] = start; q.Enqueue(s, start); }
                }
        while (q.TryDequeue(out var s, out int d))
        {
            if (d > dist[s] || !_back!.TryGetValue(s, out var froms)) continue;
            foreach (var (from, ticks) in froms)
            {
                int nd = d + ticks;
                if (dist.TryGetValue(from, out int old) && old <= nd) continue;
                dist[from] = nd;
                q.Enqueue(from, nd);
            }
        }
        return dist;
    }

    /// <summary>Plays a move out (after a wait) on a copy of the henhouse: the controls if Harry comes through it, else null.</summary>
    private static List<Controls>? Rehearse(World w, Move mv, int wait)
    {
        var c = w.Copy();
        var run = new List<Controls>();
        for (int i = 0; i < wait; i++)
        {
            run.Add(default);
            Sim.Step(c, default);
            if (c.Harry.Dead) return null;
        }
        foreach (var ctl in Graph.Script(c, mv))
        {
            run.Add(ctl);
            Sim.Step(c, ctl);
            if (c.Harry.Dead) return null;
        }
        if (run.Count == 0) return null;
        for (int i = 0; i < After; i++)
        {
            Sim.Step(c, default);
            if (c.Harry.Dead) return null;
        }
        return run;
    }
}
