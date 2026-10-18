using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAChuckieEgg.Engine;

namespace AVAChuckieEgg.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAChuckieEgg/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width, lineCap: PenLineCap.Round);
        return p;
    }

    private const uint White = 0xFFF2F2FF, Yellow = 0xFFFFE14A, Red = 0xFFFF4A3A, Dim = 0xFF8A9AC8, Cyan = 0xFF6AE8FF, Orange = 0xFFFF9A30;

    private static void Text(DrawingContext dc, string text, Point at, double size, uint colour, bool center = false, bool right = false, bool shadow = true)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        if (shadow) dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(0xC0000000)), new Point(x + 2, at.Y - ft.Height / 2 + 2));
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    private static void Poly(DrawingContext dc, uint fill, uint? edge, double width, params Point[] pts)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], true);
            foreach (var p in pts.Skip(1)) c.LineTo(p);
            c.EndFigure(true);
        }
        dc.DrawGeometry(fill == 0 ? null : Brush(fill), edge is { } e ? Pen(e, width) : null, g);
    }

    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }

    private static uint Alpha(uint colour, double a) => (uint)(Math.Clamp(a, 0, 1) * 255) << 24 | (colour & 0x00FFFFFF);


    private bool InGame => Engine.State is GameState.Attract or GameState.Playing or GameState.Paused;

    private const double T = Field.Tile, Top = Field.Top;

    /// <summary>The platforms' colours, a pair for each henhouse.</summary>
    private static readonly (uint Face, uint Edge)[] Bricks =
    [
        (0xFF40C040, 0xFF208020), (0xFFE0A040, 0xFF905018), (0xFF40A0E0, 0xFF185890), (0xFFE06060, 0xFF902828),
        (0xFFC0C040, 0xFF707018), (0xFFA070E0, 0xFF503090), (0xFF40D0B0, 0xFF187060), (0xFFE080C0, 0xFF904070),
    ];

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        switch (g.State)
        {
            case GameState.Title: DrawTitle(dc); break;
            case GameState.HighScores: DrawBarn(dc); DrawHighScores(dc); break;
            case GameState.EnterInitials: DrawBarn(dc); DrawInitials(dc); break;
            case GameState.GameOver: DrawGameOver(dc); break;
            default:
                DrawHouse(dc, g.World);
                DrawTopLine(dc, g.World);
                if (g.Message is { } msg && g.Phase is Phase.Start or Phase.Done)
                {
                    dc.FillRectangle(Brush(0xC0000000), new Rect(0, 330, Field.Width, 76));
                    Text(dc, msg, new Point(512, 368), 60, Yellow, center: true);
                }
                break;
        }
        if (g.State == GameState.Paused)
        {
            dc.FillRectangle(Brush(0xD0000000), new Rect(0, 0, Field.Width, Field.Height));
            Text(dc, "PAUSED", new Point(512, 300), 80, Yellow, center: true);
            Text(dc, "ANY KEY TO CARRY ON - ESC TO QUIT", new Point(512, 360), 26, White, center: true);
        }
        if (g.State == GameState.Attract)
            Text(dc, "DEMONSTRATION - PRESS ENTER TO PLAY", new Point(512, Field.Height - 14), 22, Yellow, center: true);
    }

    // ───────────────────────────── the henhouse ─────────────────────────────

    /// <summary>The barn behind: dark planks.</summary>
    private static void DrawBarn(DrawingContext dc)
    {
        dc.FillRectangle(Brush(0xFF0C0A14), new Rect(0, 0, Field.Width, Field.Height));
        for (int k = 0; k < 16; k++)
        {
            double x = k * 64;
            dc.FillRectangle(Brush(k % 2 == 0 ? 0xFF14101C : 0xFF100C18), new Rect(x, Top, 64, Field.Height - Top));
            dc.DrawLine(Pen(0xFF08060C, 2), new Point(x, Top), new Point(x, Field.Height));
        }
    }

    private void DrawHouse(DrawingContext dc, World w)
    {
        DrawBarn(dc);
        var (face, edge) = Bricks[w.Def.Layout % Bricks.Length];
        for (int c = 0; c < Field.Cols; c++)
            for (int r = 0; r < Field.Rows; r++)
            {
                var t = w.Grid[c, r];
                double x = c * T, y = Top + r * T;
                if (HouseBook.IsLadder(t)) DrawLadder(dc, x, y, t == Tile.LadderFloor);
                if (HouseBook.IsFloor(t))
                {
                    dc.FillRectangle(Brush(face), new Rect(x, y, T, T * 0.55));
                    dc.FillRectangle(Brush(edge), new Rect(x, y + T * 0.55, T, T * 0.15));
                    dc.DrawLine(Pen(edge, 2), new Point(x + (r % 2 == 0 ? 8 : 24), y), new Point(x + (r % 2 == 0 ? 8 : 24), y + T * 0.55));
                    if (t == Tile.LadderFloor) DrawLadder(dc, x, y, true);
                }
                if (t == Tile.Egg) DrawEgg(dc, x + T / 2, y + T);
                if (t == Tile.Grain) DrawGrain(dc, x + T / 2, y + T);
            }
        DrawCage(dc, w);
        foreach (var hen in w.Hens) DrawHen(dc, hen.X, Top + hen.Y, hen.Dir, hen.Climb != 0, hen.Peck > 0, w.Harry.Walk + hen.X * 0.05);
        DrawHarry(dc, w.Harry);
        if (w.DuckFree) DrawDuck(dc, w.DuckX, Top + w.DuckY, Engine.World.Harry.X > w.DuckX ? 1 : -1, w.DuckFlap);
    }

    private static void DrawLadder(DrawingContext dc, double x, double y, bool top)
    {
        const uint rail = 0xFFD050D0;
        double y0 = top ? y + T * 0.7 : y;
        dc.DrawLine(Pen(rail, 3), new Point(x + 7, y0), new Point(x + 7, y + T));
        dc.DrawLine(Pen(rail, 3), new Point(x + T - 7, y0), new Point(x + T - 7, y + T));
        for (double ry = y0 + 6; ry < y + T; ry += 10) dc.DrawLine(Pen(rail, 2), new Point(x + 7, ry), new Point(x + T - 7, ry));
    }

    private static void DrawEgg(DrawingContext dc, double x, double floor)
    {
        dc.DrawEllipse(Brush(0xFFF8F4E8), Pen(0xFFC0B8A0, 1), new Point(x, floor - 11), 8, 11);
        dc.FillEllipse(x - 3, floor - 15, 2.5, 0xFFFFFFFF);
    }

    private static void DrawGrain(DrawingContext dc, double x, double floor)
    {
        Poly(dc, 0xFFD0A030, null, 0, new Point(x - 13, floor), new Point(x, floor - 13), new Point(x + 13, floor));
        for (int k = 0; k < 7; k++) dc.FillEllipse(x - 8 + Hash(k, 1, 4) % 16, floor - 3 - Hash(k, 2, 4) % 8, 1.6, 0xFFF0D070);
    }

    /// <summary>The big duck's cage, top left: she's in it until she gets out.</summary>
    private void DrawCage(DrawingContext dc, World w)
    {
        double x0 = 0.3 * T, y0 = Top + 1 * T, x1 = 4.7 * T, y1 = Top + 5 * T;
        dc.FillRectangle(Brush(0xFF6A4A20), new Rect(x0, y0 - 6, x1 - x0, 8), 3);
        if (!w.DuckFree) DrawDuck(dc, (x0 + x1) / 2, y1 - 4, 1, _clock * 0.2);
        for (double x = x0 + 4; x < x1; x += 14) dc.DrawLine(Pen(0xFFB0B0B0, 2), new Point(x, y0), new Point(x, y1));
        if (w.DuckFree) dc.DrawLine(Pen(0xFF0C0A14, 6), new Point(x1 - 4, y0 + 10), new Point(x1 - 4, y1));
    }

    private static void DrawDuck(DrawingContext dc, double x, double feet, int f, double flap)
    {
        const uint body = 0xFF7050D0, dark = 0xFF40288A;
        double wing = Math.Sin(flap * 12) * 14;
        dc.DrawEllipse(Brush(body), Pen(dark, 2), new Point(x, feet - 30), 30, 22);
        Poly(dc, dark, null, 0, new Point(x - 8, feet - 34), new Point(x - 40 * f, feet - 46 - wing), new Point(x - 20 * f, feet - 22));
        dc.DrawLine(Pen(body, 10), new Point(x + 18 * f, feet - 40), new Point(x + 24 * f, feet - 62));
        dc.FillEllipse(x + 26 * f, feet - 68, 12, body);
        Poly(dc, 0xFFF0A020, null, 0, new Point(x + 34 * f, feet - 72), new Point(x + 52 * f, feet - 66), new Point(x + 34 * f, feet - 62));
        dc.FillEllipse(x + 30 * f, feet - 71, 3, 0xFFFFFFFF);
        dc.FillEllipse(x + 31 * f, feet - 71, 1.5, 0xFF000000);
        dc.DrawLine(Pen(0xFFF0A020, 3), new Point(x - 6, feet - 10), new Point(x - 8, feet));
        dc.DrawLine(Pen(0xFFF0A020, 3), new Point(x + 6, feet - 10), new Point(x + 8, feet));
    }

    private static void DrawHen(DrawingContext dc, double x, double feet, int f, bool climbing, bool pecking, double walk)
    {
        const uint body = 0xFFF0E040, dark = 0xFFA09018;
        double step = Math.Sin(walk * 10) * 4;
        dc.DrawLine(Pen(0xFFF08020, 2.5), new Point(x - 3, feet - 10), new Point(x - 3 - step, feet));
        dc.DrawLine(Pen(0xFFF08020, 2.5), new Point(x + 3, feet - 10), new Point(x + 3 + step, feet));
        dc.DrawEllipse(Brush(body), Pen(dark, 1.5), new Point(x, feet - 16), 12, 9);
        Poly(dc, body, dark, 1.5, new Point(x - 10 * f, feet - 20), new Point(x - 18 * f, feet - 30), new Point(x - 8 * f, feet - 14));
        double hx = pecking ? x + 12 * f : x + 9 * f, hy = pecking ? feet - 8 : feet - 26;
        if (climbing) { hx = x; hy = feet - 28; }
        dc.FillEllipse(hx, hy, 6, body);
        Poly(dc, 0xFFF08020, null, 0, new Point(hx + 5 * f, hy - 2), new Point(hx + 12 * f, hy), new Point(hx + 5 * f, hy + 2));
        Poly(dc, 0xFFE02020, null, 0, new Point(hx - 3, hy - 5), new Point(hx, hy - 10), new Point(hx + 3, hy - 5));
        dc.FillEllipse(hx + 2 * f, hy - 1, 1.4, 0xFF000000);
    }

    /// <summary>Hen-House Harry: a flat cap, a red jumper, blue trousers, feet on (x, y).</summary>
    private void DrawHarry(DrawingContext dc, Harry h)
    {
        if (h.Dead && (int)(Engine.PhaseTime * 10) % 2 == 0) return;
        double x = h.X, y = Top + h.Y;
        int f = h.Facing;
        const uint skin = 0xFFF0C090, jumper = 0xFFE03030, trousers = 0xFF3050C0, cap = 0xFFF0F0F0;
        double step = h.Climbing ? Math.Sin(h.Walk * 3) * 5 : h.Airborne ? 4 : Math.Sin(h.Walk * 2) * 6;
        if (h.Climbing)
        {
            dc.DrawLine(Pen(trousers, 6), new Point(x - 4, y - 16), new Point(x - 4, y - 2 + step));
            dc.DrawLine(Pen(trousers, 6), new Point(x + 4, y - 16), new Point(x + 4, y - 2 - step));
            dc.FillRectangle(Brush(jumper), new Rect(x - 9, y - 32, 18, 17), 4);
            dc.DrawLine(Pen(jumper, 5), new Point(x - 8, y - 28), new Point(x - 11, y - 40 - step));
            dc.DrawLine(Pen(jumper, 5), new Point(x + 8, y - 28), new Point(x + 11, y - 40 + step));
            dc.FillEllipse(x, y - 36, 7, skin);
            dc.DrawEllipse(Brush(cap), null, new Point(x, y - 41), 8, 4);
            return;
        }
        dc.DrawLine(Pen(trousers, 6), new Point(x, y - 16), new Point(x + step, y - 1));
        dc.DrawLine(Pen(trousers, 6), new Point(x, y - 16), new Point(x - step, y - 1));
        dc.FillRectangle(Brush(0xFF402010), new Rect(x + step - 4 + f * 2, y - 3, 9, 3));
        dc.FillRectangle(Brush(0xFF402010), new Rect(x - step - 4 + f * 2, y - 3, 9, 3));
        dc.FillRectangle(Brush(jumper), new Rect(x - 9, y - 32, 18, 17), 4);
        dc.DrawLine(Pen(jumper, 5), new Point(x, y - 28), new Point(x + f * 9 + (h.Airborne ? 0 : -step), y - (h.Airborne ? 36 : 20)));
        dc.FillEllipse(x + f * 2, y - 36, 7, skin);
        dc.DrawEllipse(Brush(cap), null, new Point(x, y - 42), 8, 3.5);
        dc.FillRectangle(Brush(cap), new Rect(x + (f > 0 ? 2 : -12), y - 42, 10, 3), 1.5);
        dc.FillEllipse(x + f * 5, y - 37, 1.4, 0xFF000000);
    }

    // ───────────────────────────── the top line ─────────────────────────────

    private void DrawTopLine(DrawingContext dc, World w)
    {
        var g = Engine;
        dc.FillRectangle(Brush(0xFF000000), new Rect(0, 0, Field.Width, Top));
        Text(dc, "SCORE", new Point(20, 18), 22, Dim);
        Text(dc, $"{g.Score:000000}", new Point(20, 44), 34, White);
        Text(dc, $"LEVEL {g.Level}", new Point(330, 32), 32, Cyan, center: true);
        Text(dc, "TIME", new Point(560, 18), 22, Dim, center: true);
        bool fed = w.Fed > 0;
        Text(dc, $"{w.Time}", new Point(560, 44), 34, fed ? 0xFF40E040 : w.Time < 200 && (int)(_clock * 4) % 2 == 0 ? Red : Yellow, center: true);
        Text(dc, $"EGGS {w.Def.Eggs - w.EggsLeft}/{w.Def.Eggs}", new Point(760, 32), 30, White, center: true);
        for (int i = 0; i < Math.Min(g.Lives - 1, 6); i++)
        {
            double x = 1000 - i * 24;
            dc.DrawEllipse(Brush(0xFFF0F0F0), null, new Point(x, 30), 9, 4);
            dc.FillEllipse(x, 38, 6, 0xFFF0C090);
        }
    }

    // ───────────────────────────── title, game over and scores ─────────────────────────────

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2;
        DrawBarn(dc);
        // A platform with hens strutting along it, and eggs.
        double floor = 650;
        var (face, edge) = Bricks[0];
        dc.FillRectangle(Brush(face), new Rect(0, floor, Field.Width, 18));
        dc.FillRectangle(Brush(edge), new Rect(0, floor + 18, Field.Width, 5));
        for (int k = 0; k < 6; k++) DrawEgg(dc, 90 + k * 170, floor);
        for (int k = 0; k < 3; k++)
        {
            double x = (_clock * (40 + k * 12) + k * 340) % 1100 - 40;
            DrawHen(dc, x, floor, 1, false, false, _clock + k);
        }
        DrawDuck(dc, 880, 560, -1, _clock);
        string title = "AVA CHUCKIE EGG";
        for (int i = 0; i < title.Length; i++)
        {
            double tx = cx - title.Length * 28 + 28 + i * 56, ty = 112 + Math.Sin(_clock * 2.4 + i * 0.45) * 5;
            uint[] colours = [0xFFF0E040, 0xFFFFFFFF, 0xFFE03030, 0xFF40C040];
            Text(dc, title[i].ToString(), new Point(tx, ty), 104, colours[i % colours.Length], center: true);
        }
        Text(dc, "AFTER NIGEL ALDERTON'S 1983 GAME", new Point(cx, 180), 26, White, center: true);
        dc.FillRectangle(Brush(0xC0000000), new Rect(cx - 400, 210, 800, 160), 10);
        Text(dc, "HEN-HOUSE HARRY MUST COLLECT ALL TWELVE EGGS IN EACH HENHOUSE", new Point(cx, 236), 24, White, center: true);
        Text(dc, "BEFORE THE TIME RUNS OUT. RUN, JUMP AND CLIMB THE LADDERS; KEEP", new Point(cx, 264), 24, White, center: true);
        Text(dc, "CLEAR OF THE HENS. GRAIN STOPS THE CLOCK FOR A WHILE. AFTER EIGHT", new Point(cx, 292), 24, White, center: true);
        Text(dc, "HENHOUSES THE BIG DUCK GETS OUT OF HER CAGE, AND COMES AFTER YOU.", new Point(cx, 320), 24, White, center: true);
        if ((int)(_clock * 2) % 2 == 0) Text(dc, "PRESS ENTER TO START", new Point(cx, 420), 44, Yellow, center: true);
        dc.FillRectangle(Brush(0xD0000000), new Rect(0, 700, Field.Width, 68));
        Text(dc, "LEFT/RIGHT (OR Z/X): WALK   UP/DOWN: CLIMB   SPACE: JUMP   P: PAUSE   M: SOUND", new Point(cx, 718), 20, 0xFFE0D0B0, center: true);
        Text(dc, "MUSIC: THE IRISH WASHERWOMAN (TRADITIONAL) AND A TUNE OF ITS OWN", new Point(cx, 746), 18, Dim, center: true);
    }

    private void DrawGameOver(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        DrawBarn(dc);
        dc.FillRectangle(Brush(0xC0000000), new Rect(cx - 360, 150, 720, 230), 12);
        Text(dc, "GAME OVER", new Point(cx, 220), 100, (int)(_clock * 4) % 2 == 0 ? Red : Yellow, center: true);
        Text(dc, $"SCORE {g.Score}", new Point(cx, 300), 46, White, center: true);
        Text(dc, $"YOU REACHED LEVEL {g.Level}", new Point(cx, 352), 30, Cyan, center: true);
        DrawDuck(dc, cx, 600, 1, _clock);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0xD0000000), new Rect(cx - 330, 70, 660, 640), 12);
        Text(dc, "THE BEST EGG COLLECTORS", new Point(cx, 120), 54, Yellow, center: true);
        Text(dc, "RANK NAME    SCORE   LEVEL", new Point(cx, 190), 30, Dim, center: true);
        var entries = g.HighScores.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool mine = i == g.NewEntryIndex && (int)(_clock * 4) % 2 == 0;
            Text(dc, $"{i + 1,2}.  {e.Initials}  {e.Score,8}   {e.Stage,4}", new Point(cx, 236 + i * 42), 34, mine ? Red : i == 0 ? Yellow : White, center: true);
        }
        Text(dc, "PRESS ENTER TO PLAY", new Point(cx, 680), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0xD0000000), new Rect(cx - 330, 140, 660, 460), 12);
        Text(dc, "A NEW HIGH SCORE!", new Point(cx, 200), 64, Yellow, center: true);
        Text(dc, g.Score.ToString(), new Point(cx, 270), 52, White, center: true);
        Text(dc, "ENTER YOUR INITIALS", new Point(cx, 350), 34, Dim, center: true);
        if (g.Initials is { } entry)
        {
            string s = entry.Initials;
            for (int i = 0; i < s.Length; i++)
            {
                bool cur = i == entry.Cursor && (int)(_clock * 3) % 2 == 0;
                Text(dc, s[i].ToString(), new Point(cx - 60 + i * 60, 440), 80, cur ? Red : Yellow, center: true);
                dc.FillRectangle(Brush(i == entry.Cursor ? Red : Dim), new Rect(cx - 82 + i * 60, 480, 44, 4));
            }
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE; ENTER WHEN DONE", new Point(cx, 560), 26, White, center: true);
    }
}

internal static class DrawingExtensions
{
    public static void FillEllipse(this DrawingContext dc, double x, double y, double r, uint colour) =>
        dc.DrawEllipse(GameCanvas.Brush(colour), null, new Point(x, y), r, r);
    public static void FillRectangle(this DrawingContext dc, IBrush brush, Rect rect, double radius) => dc.FillRectangle(brush, rect, (float)radius);
}
