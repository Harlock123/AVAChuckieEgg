namespace AVAChuckieEgg.Engine;

/// <summary>A small random number generator whose state can be copied.</summary>
public struct Dice
{
    private ulong _s;
    public Dice(int seed) => _s = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;

    public double Next()
    {
        _s ^= _s << 13;
        _s ^= _s >> 7;
        _s ^= _s << 17;
        return (_s >> 11) * (1.0 / (1UL << 53));
    }

    public int Next(int n) => (int)(Next() * n);
}

/// <summary>The controls: left or right, up or down (up the screen is -1), and jump.</summary>
public readonly record struct Controls(int Dx = 0, int Dy = 0, bool Jump = false);

/// <summary>Hen-House Harry: where his feet are (x is his middle), and whether he's walking, in the air or on a ladder.</summary>
public sealed class Harry
{
    public const double HalfWidth = 10, Height = 40, Speed = 130, JumpSpeed = 360, Gravity = 1200, MaxFall = 480, Climb = 110;
    public double X, Y, Vx, Vy, Walk;
    public int Facing = 1;
    public bool Airborne, Climbing, Dead, JumpPrev;
    public Harry Copy() => (Harry)MemberwiseClone();
}

/// <summary>A hen: walking a platform, or going up or down a ladder to another.</summary>
public sealed class Hen
{
    public const double HalfWidth = 12, Height = 30;
    public double X, Y;
    public int Dir = 1;
    /// <summary>On a ladder: +1 going down, -1 going up, 0 walking.</summary>
    public int Climb;
    /// <summary>The ladder last climbed (so she doesn't turn straight back up it).</summary>
    public int LastLadder = -1;
    public double Peck;
    public Hen Copy() => (Hen)MemberwiseClone();
}

/// <summary>
/// Everything in the henhouse that moves or changes, kept together so it can be copied whole: Harry, the hens, the
/// duck, the eggs and grain left, the time, the score.
/// </summary>
public sealed class World
{
    public HouseDef Def = null!;
    public Tile[,] Grid = new Tile[Field.Cols, Field.Rows];
    public Harry Harry = new();
    public List<Hen> Hens = [];
    /// <summary>The big duck: out of her cage from the second round of henhouses, flying after Harry.</summary>
    public bool DuckFree;
    public double DuckX, DuckY, DuckFlap;
    public int Round;
    public double HenSpeed = 50;
    /// <summary>The time left (it counts down by tens), and how long the grain has stopped it for.</summary>
    public int Time = 1000;
    public double TimeTick, Fed;
    public int Score;
    public int EggsLeft;
    public Dice Dice = new(1);
    public string Cause = "";
    public List<Sfx> Heard = [];
    /// <summary>A world used only to try moves out: the hens and duck are left out.</summary>
    public bool Probe;

    public World Copy() => new()
    {
        Def = Def, Grid = (Tile[,])Grid.Clone(), Harry = Harry.Copy(), Hens = Hens.Select(h => h.Copy()).ToList(), DuckFree = DuckFree,
        DuckX = DuckX, DuckY = DuckY, DuckFlap = DuckFlap, Round = Round, HenSpeed = HenSpeed, Time = Time, TimeTick = TimeTick, Fed = Fed,
        Score = Score, EggsLeft = EggsLeft, Dice = Dice, Cause = Cause, Probe = Probe,
    };

    public Tile At(int c, int r) => c < 0 || c >= Field.Cols || r < 0 || r >= Field.Rows ? Tile.Empty : Grid[c, r];
}
