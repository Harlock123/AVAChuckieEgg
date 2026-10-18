namespace AVAChuckieEgg.Engine;

/// <summary>The rules, one 60th of a second at a time.</summary>
public static class Sim
{
    public const int EggPoints = 100, GrainPoints = 50, Time0 = 1000;
    /// <summary>The time counts down by ten every this many seconds; grain stops it for a while.</summary>
    public const double TimeStep = 0.9, FedSeconds = 5;
    public const double DuckSpeed = 42;

    public static void Start(World w, HouseDef def, int round = 0)
    {
        w.Def = def;
        w.Grid = (Tile[,])def.Grid.Clone();
        w.Round = round;
        w.HenSpeed = 48 + 10 * Math.Min(round, 4);
        w.DuckFree = round >= 1;
        w.Score = 0;
        w.EggsLeft = def.Eggs;
        w.Dice = new Dice(def.Layout * 17 + round);
        Reset(w);
    }

    /// <summary>Harry back at the start, the hens back where they began, the duck back at her cage, the time full.</summary>
    public static void Reset(World w)
    {
        var def = w.Def;
        w.Harry = new Harry { X = (def.Start.Col + 0.5) * Field.Tile, Y = def.Start.Row * Field.Tile };
        w.Hens = def.Hens.Select((h, i) => new Hen { X = (h.Col + 0.5) * Field.Tile, Y = h.Row * Field.Tile, Dir = i % 2 == 0 ? 1 : -1 }).ToList();
        w.DuckX = 2.5 * Field.Tile;
        w.DuckY = 3 * Field.Tile;
        w.Time = Time0;
        w.TimeTick = 0;
        w.Fed = 0;
    }

    public static int Col(double x) => (int)Math.Floor(x / Field.Tile);

    /// <summary>The ladder in a column at a height: where its top and bottom are (as heights for the feet), or null.</summary>
    public static (double Top, double Bottom)? Ladder(World w, int c, double y)
    {
        int r = (int)Math.Floor((y - 0.5) / Field.Tile);
        int at = HouseBook.IsLadder(w.At(c, r)) ? r : HouseBook.IsLadder(w.At(c, r + 1)) ? r + 1 : -1;
        if (at < 0) return null;
        int a = at, b = at;
        while (HouseBook.IsLadder(w.At(c, a - 1))) a--;
        while (HouseBook.IsLadder(w.At(c, b + 1))) b++;
        return (a * Field.Tile, (b + 1) * Field.Tile);
    }

    private static bool FloorAt(World w, double x, int row) => HouseBook.IsFloor(w.At(Col(x), row));

    public static void Step(World w, Controls c)
    {
        double dt = Field.Step;
        var h = w.Harry;
        if (h.Dead) return;
        if (!w.Probe)
        {
            // The time.
            if (w.Fed > 0) w.Fed -= dt;
            else if ((w.TimeTick += dt) >= TimeStep)
            {
                w.TimeTick = 0;
                w.Time -= 10;
                if (w.Time <= 0) { w.Time = 0; Die(w, "out of time"); return; }
            }
        }
        bool jump = c.Jump && !h.JumpPrev;
        h.JumpPrev = c.Jump;
        if (h.Climbing) ClimbStep(w, c);
        else if (!h.Airborne) GroundStep(w, c, jump);
        else AirStep(w, c);
        if (h.Dead || w.Probe) return;
        Collect(w);
        foreach (var hen in w.Hens) HenStep(w, hen);
        if (w.DuckFree)
        {
            double dx = h.X - w.DuckX, dy = h.Y - Harry.Height / 2 - w.DuckY, d = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            double speed = DuckSpeed + 6 * Math.Min(w.Round, 4);
            w.DuckX += dx / d * speed * dt;
            w.DuckY += dy / d * speed * dt;
            w.DuckFlap += dt;
            if (Math.Abs(dx) < 26 && Math.Abs(dy) < 30) { Die(w, "the duck"); return; }
        }
        foreach (var hen in w.Hens)
            if (Math.Abs(hen.X - h.X) < Hen.HalfWidth + Harry.HalfWidth - 5 && hen.Y - Hen.Height + 4 < h.Y && h.Y - Harry.Height + 4 < hen.Y)
            {
                Die(w, "a hen");
                return;
            }
    }

    private static void GroundStep(World w, Controls c, bool jump)
    {
        var h = w.Harry;
        int col = Col(h.X), row = (int)Math.Round(h.Y / Field.Tile);
        double cx = (col + 0.5) * Field.Tile;
        // Onto a ladder: up one going up from here, or down one starting under his feet.
        if (c.Dy != 0 && Math.Abs(h.X - cx) < 12)
        {
            bool up = c.Dy < 0 && HouseBook.IsLadder(w.At(col, row - 1)), down = c.Dy > 0 && HouseBook.IsLadder(w.At(col, row));
            if (up || down)
            {
                h.X = cx;
                h.Climbing = true;
                ClimbStep(w, c);
                return;
            }
        }
        if (jump)
        {
            h.Airborne = true;
            h.Vy = -Harry.JumpSpeed;
            h.Vx = c.Dx * Harry.Speed;
            if (c.Dx != 0) h.Facing = c.Dx;
            w.Heard.Add(Sfx.Jump);
            return;
        }
        if (c.Dx != 0)
        {
            h.Facing = c.Dx;
            h.X = Math.Clamp(h.X + c.Dx * Harry.Speed * Field.Step, Harry.HalfWidth, Field.Width - Harry.HalfWidth);
            h.Walk += Field.Step * 8;
        }
        if (!FloorAt(w, h.X, row))
        {
            // Off the edge: down he goes.
            h.Airborne = true;
            h.Vx = 0;
            h.Vy = 0;
        }
    }

    private static void ClimbStep(World w, Controls c)
    {
        var h = w.Harry;
        int col = Col(h.X);
        if (Ladder(w, col, h.Y) is not { } span) { h.Climbing = false; h.Airborne = true; h.Vx = h.Vy = 0; return; }
        if (c.Dy != 0)
        {
            h.Y = Math.Clamp(h.Y + c.Dy * Harry.Climb * Field.Step, span.Top, span.Bottom);
            h.Walk += Field.Step * 6;
        }
        // Off sideways where there's floor at his feet.
        if (c.Dx != 0)
        {
            int row = (int)Math.Round(h.Y / Field.Tile);
            if (Math.Abs(h.Y - row * Field.Tile) < 4 && (FloorAt(w, h.X, row) || FloorAt(w, h.X + c.Dx * Field.Tile, row)))
            {
                h.Y = row * Field.Tile;
                h.Climbing = false;
                h.Facing = c.Dx;
                h.X = Math.Clamp(h.X + c.Dx * Harry.Speed * Field.Step, Harry.HalfWidth, Field.Width - Harry.HalfWidth);
                if (!FloorAt(w, h.X, row)) { h.Airborne = true; h.Vx = h.Vy = 0; }
            }
        }
    }

    private static void AirStep(World w, Controls c)
    {
        var h = w.Harry;
        double y0 = h.Y;
        h.Vy = Math.Min(h.Vy + Harry.Gravity * Field.Step, Harry.MaxFall);
        h.X += h.Vx * Field.Step;
        if (h.X < Harry.HalfWidth || h.X > Field.Width - Harry.HalfWidth) { h.X = Math.Clamp(h.X, Harry.HalfWidth, Field.Width - Harry.HalfWidth); h.Vx = 0; }
        h.Y += h.Vy * Field.Step;
        // Catching hold of a ladder on the way past.
        if (c.Dy != 0)
        {
            int col = Col(h.X);
            double cx = (col + 0.5) * Field.Tile;
            if (Math.Abs(h.X - cx) < 9 && Ladder(w, col, h.Y) is { } span && h.Y > span.Top - 2 && h.Y < span.Bottom + 2)
            {
                h.X = cx;
                h.Y = Math.Clamp(h.Y, span.Top, span.Bottom);
                h.Airborne = false;
                h.Climbing = true;
                h.Vx = h.Vy = 0;
                return;
            }
        }
        if (h.Vy > 0)
        {
            int r = (int)Math.Floor(h.Y / Field.Tile);
            if (r * Field.Tile > y0 - 0.001 && FloorAt(w, h.X, r))
            {
                h.Y = r * Field.Tile;
                h.Airborne = false;
                h.Vx = h.Vy = 0;
                w.Heard.Add(Sfx.Land);
            }
        }
        if (h.Y > Field.Rows * Field.Tile + 60) Die(w, "a fall");
    }

    private static void Collect(World w)
    {
        var h = w.Harry;
        int c0 = Col(h.X - Harry.HalfWidth + 6), c1 = Col(h.X + Harry.HalfWidth - 6);
        int r0 = (int)Math.Floor((h.Y - Harry.Height + 6) / Field.Tile), r1 = (int)Math.Floor((h.Y - 2) / Field.Tile);
        for (int c = c0; c <= c1; c++)
            for (int r = r0; r <= r1; r++)
            {
                var t = w.At(c, r);
                if (t is not (Tile.Egg or Tile.Grain)) continue;
                w.Grid[c, r] = Tile.Empty;
                if (t == Tile.Egg)
                {
                    w.EggsLeft--;
                    w.Score += EggPoints * (1 + Math.Min(w.Round, 9) / 2);
                    w.Heard.Add(Sfx.Egg);
                }
                else
                {
                    w.Score += GrainPoints;
                    w.Fed = FedSeconds;
                    w.Heard.Add(Sfx.Grain);
                }
            }
    }

    private static void HenStep(World w, Hen hen)
    {
        double dt = Field.Step, v = w.HenSpeed;
        hen.Peck = Math.Max(0, hen.Peck - dt);
        int col = Col(hen.X), row = (int)Math.Round(hen.Y / Field.Tile);
        if (hen.Climb != 0)
        {
            if (Ladder(w, col, hen.Y) is not { } span) { hen.Climb = 0; return; }
            hen.Y += hen.Climb * v * 0.8 * dt;
            if (hen.Climb > 0 && hen.Y >= span.Bottom) { hen.Y = span.Bottom; hen.Climb = 0; hen.Dir = w.Dice.Next(2) == 0 ? 1 : -1; }
            else if (hen.Climb < 0 && hen.Y <= span.Top) { hen.Y = span.Top; hen.Climb = 0; hen.Dir = w.Dice.Next(2) == 0 ? 1 : -1; }
            return;
        }
        if (hen.Peck > 0) return;
        double cx = (col + 0.5) * Field.Tile;
        if (Math.Abs(hen.X - cx) <= v * dt / 2 + 0.01 && col != hen.LastLadder)
        {
            bool up = HouseBook.IsLadder(w.At(col, row - 1)), down = HouseBook.IsLadder(w.At(col, row));
            if ((up || down) && w.Dice.Next(2) == 0)
            {
                hen.X = cx;
                hen.Climb = up && down ? (w.Dice.Next(2) == 0 ? -1 : 1) : up ? -1 : 1;
                hen.LastLadder = col;
                return;
            }
        }
        if (Math.Abs(hen.X - (hen.LastLadder + 0.5) * Field.Tile) > Field.Tile) hen.LastLadder = -1;
        // Grain underfoot: she stops to eat it.
        if (w.At(col, row - 1) == Tile.Grain && Math.Abs(hen.X - cx) < 4)
        {
            w.Grid[col, row - 1] = Tile.Empty;
            hen.Peck = 1.2;
            return;
        }
        double ahead = hen.X + hen.Dir * (Hen.HalfWidth + 2);
        if (ahead < 0 || ahead > Field.Width || !FloorAt(w, ahead, row)) hen.Dir = -hen.Dir;
        hen.X += hen.Dir * v * dt;
    }

    public static void Die(World w, string cause)
    {
        w.Harry.Dead = true;
        w.Cause = cause;
        w.Heard.Add(Sfx.Die);
    }
}
