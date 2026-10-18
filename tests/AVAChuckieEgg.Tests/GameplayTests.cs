using Avalonia.Input;
using AVAChuckieEgg.Engine;

namespace AVAChuckieEgg.Tests;

public class GameplayTests
{
    private const double T = Field.Tile;

    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "avachuckieegg-tests-" + Guid.NewGuid().ToString("N"));

    private static GameEngine Engine() => new(HighScoreTable.Load(TempFolder())) { Silent = true };

    /// <summary>
    /// A little henhouse: the ground (row 21), a platform on row 17 from column 4 to 20, a ladder up to it at column 10, an
    /// egg on the platform at column 16, grain on the ground at column 24.
    /// </summary>
    private static HouseDef Little(Action<HouseDef>? build = null)
    {
        var def = new HouseDef { Layout = 0, Start = (2, 21), Eggs = 1 };
        for (int c = 0; c < Field.Cols; c++) def.Grid[c, 21] = Tile.Floor;
        for (int c = 4; c <= 20; c++) def.Grid[c, 17] = Tile.Floor;
        def.Grid[10, 17] = Tile.LadderFloor;
        for (int r = 18; r <= 20; r++) def.Grid[10, r] = Tile.Ladder;
        def.Grid[16, 16] = Tile.Egg;
        def.Grid[24, 20] = Tile.Grain;
        build?.Invoke(def);
        return def;
    }

    private static World In(HouseDef def, double col = 2.5, int row = 21)
    {
        var w = new World();
        Sim.Start(w, def);
        w.Harry.X = col * T;
        w.Harry.Y = row * T;
        return w;
    }

    private static void Run(World w, int steps, Controls c = default)
    {
        for (int i = 0; i < steps; i++) Sim.Step(w, c);
    }

    // ───────────────────────────── the henhouses ─────────────────────────────

    [Fact]
    public void A_henhouse_is_the_same_from_the_same_seed()
    {
        var a = HouseBook.Build(3, 99);
        var b = HouseBook.Build(3, 99);
        Assert.Equal(a.Grid.Cast<Tile>(), b.Grid.Cast<Tile>());
        Assert.Equal(a.Hens, b.Hens);
    }

    [Fact]
    public void Every_henhouse_has_twelve_eggs_all_within_reach_of_the_start()
    {
        for (int n = 0; n < HouseBook.Layouts; n++)
        {
            var def = HouseBook.Layout(n);
            Assert.Equal(HouseBook.EggCount, def.Grid.Cast<Tile>().Count(t => t == Tile.Egg));
            var g = Graph.Build(def.Grid);
            var reach = g.Reach(Spot.Standing(def.Start.Col, def.Start.Row));
            for (int c = 0; c < Field.Cols; c++)
                for (int r = 0; r < Field.Rows; r++)
                    if (def.Grid[c, r] == Tile.Egg) Assert.Contains(new Spot(c, r + 1), reach);
            Assert.InRange(def.Hens.Count, 3, 5);
        }
    }

    // ───────────────────────────── Harry ─────────────────────────────

    [Fact]
    public void Harry_walks_and_jumps_a_fixed_arc()
    {
        var w = In(Little());
        Run(w, 60, new Controls(1));
        Assert.Equal(2.5 * T + Harry.Speed, w.Harry.X, 1);
        Sim.Step(w, new Controls(1, 0, true));
        Assert.True(w.Harry.Airborne);
        double peak = w.Harry.Y;
        for (int i = 0; i < 60 && w.Harry.Airborne; i++) { Sim.Step(w, new Controls(1)); peak = Math.Min(peak, w.Harry.Y); }
        Assert.False(w.Harry.Airborne);
        Assert.Equal(21 * T, w.Harry.Y);
        Assert.InRange(21 * T - peak, 45, 60);
    }

    [Fact]
    public void Up_the_ladder_and_off_onto_the_platform()
    {
        var w = In(Little(), 10.5);
        Sim.Step(w, new Controls(0, -1));
        Assert.True(w.Harry.Climbing);
        Run(w, 80, new Controls(0, -1));
        Assert.Equal(17 * T, w.Harry.Y);
        Run(w, 30, new Controls(1));
        Assert.False(w.Harry.Climbing);
        Assert.False(w.Harry.Airborne);
        Assert.True(w.Harry.X > 11 * T);
    }

    [Fact]
    public void Down_from_the_top_of_a_ladder()
    {
        var w = In(Little(), 10.5, 17);
        Sim.Step(w, new Controls(0, 1));
        Assert.True(w.Harry.Climbing);
        Run(w, 80, new Controls(0, 1));
        Assert.Equal(21 * T, w.Harry.Y);
    }

    [Fact]
    public void Off_the_end_of_a_platform_and_down_he_falls_safely_but_off_the_bottom_is_the_end()
    {
        var w = In(Little(), 19.5, 17);
        Run(w, 60, new Controls(1));
        Run(w, 60);
        Assert.False(w.Harry.Dead);
        Assert.Equal(21 * T, w.Harry.Y);

        var v = In(Little(d => { for (int c = 26; c < Field.Cols; c++) d.Grid[c, 21] = Tile.Empty; }), 25.5);
        Run(v, 120, new Controls(1));
        Assert.True(v.Harry.Dead);
    }

    [Fact]
    public void A_jump_from_below_goes_up_through_a_platform_and_lands_on_it()
    {
        var w = In(Little(d => { for (int c = 22; c <= 26; c++) d.Grid[c, 20] = Tile.Floor; }), 21.5);
        Sim.Step(w, new Controls(1, 0, true));
        Run(w, 60);
        Assert.Equal(20 * T, w.Harry.Y);
        Assert.False(w.Harry.Airborne);
    }

    [Fact]
    public void Eggs_score_and_grain_scores_and_stops_the_clock()
    {
        var w = In(Little(), 15.5, 17);
        Run(w, 20, new Controls(1));
        Assert.Equal(0, w.EggsLeft);
        Assert.Equal(Sim.EggPoints, w.Score);
        var v = In(Little(), 23);
        Run(v, 30, new Controls(1));
        Assert.Equal(Sim.GrainPoints, v.Score);
        int time = v.Time;
        Run(v, 60 * 4);
        Assert.Equal(time, v.Time);
        Run(v, 60 * 3);
        Assert.True(v.Time < time);
    }

    [Fact]
    public void The_time_runs_down_by_tens_and_out_is_the_end()
    {
        var w = In(Little());
        Run(w, 60 * 3);
        Assert.Equal(Sim.Time0 - 30, w.Time);
        w.Time = 10;
        Run(w, 60);
        Assert.True(w.Harry.Dead);
        Assert.Equal("out of time", w.Cause);
    }

    // ───────────────────────────── hens and the duck ─────────────────────────────

    [Fact]
    public void A_hen_walks_her_platform_turning_at_the_ends_and_is_deadly_to_touch()
    {
        // (No ladder down, so she stays on her platform.)
        var def = Little(d => { d.Hens.Add((18, 17)); d.Grid[10, 17] = Tile.Floor; for (int r = 18; r <= 20; r++) d.Grid[10, r] = Tile.Empty; });
        var w = In(def, 2.5);
        double x0 = w.Hens[0].X;
        bool turned = false;
        for (int i = 0; i < 60 * 20; i++)
        {
            int dir = w.Hens[0].Dir;
            Sim.Step(w, default);
            if (w.Hens[0].Dir != dir) turned = true;
            Assert.InRange(w.Hens[0].X, 4 * T, 21 * T);
        }
        Assert.True(turned);
        Assert.NotEqual(x0, w.Hens[0].X);

        var v = In(Little(d => d.Hens.Add((7, 21))), 3.5);
        v.Hens[0].Dir = -1;
        Run(v, 60 * 3);
        Assert.True(v.Harry.Dead);
        Assert.Equal("a hen", v.Cause);
    }

    [Fact]
    public void Hens_climb_ladders_and_eat_grain()
    {
        var w = In(Little(d => d.Hens.Add((12, 17))), 30.5);
        bool climbed = false;
        for (int i = 0; i < 60 * 60 && !climbed; i++) { Sim.Step(w, default); climbed = w.Hens[0].Climb != 0; }
        Assert.True(climbed);

        var v = In(Little(d => d.Hens.Add((22, 21))), 30.5);
        v.Harry.Y = 21 * T;
        v.Hens[0].Dir = 1;
        Run(v, 60 * 3);
        Assert.Equal(Tile.Empty, v.Grid[24, 20]);
        Assert.Equal(0, v.Score);
    }

    [Fact]
    public void From_the_second_round_the_duck_flies_after_Harry()
    {
        var w = new World();
        Sim.Start(w, Little(), round: 1);
        Assert.True(w.DuckFree);
        double d0 = Math.Abs(w.DuckX - w.Harry.X) + Math.Abs(w.DuckY - w.Harry.Y);
        Run(w, 60 * 2);
        Assert.True(Math.Abs(w.DuckX - w.Harry.X) + Math.Abs(w.DuckY - w.Harry.Y) < d0 - 60);
        Run(w, 60 * 30);
        Assert.True(w.Harry.Dead);
        Assert.Equal("the duck", w.Cause);
        var first = new World();
        Sim.Start(first, Little());
        Assert.False(first.DuckFree);
    }

    // ───────────────────────────── the game ─────────────────────────────

    [Fact]
    public void Every_egg_counts_the_time_in_and_goes_on_to_the_next_henhouse()
    {
        var g = Engine();
        g.Go(1);
        var w = g.World;
        for (int c = 0; c < Field.Cols; c++)
            for (int r = 0; r < Field.Rows; r++)
                if (w.Grid[c, r] == Tile.Egg) w.Grid[c, r] = Tile.Empty;
        w.EggsLeft = 0;
        int time = w.Time;
        g.Tick(default);
        Assert.Equal(Phase.Done, g.Phase);
        g.Run(60 * 8);
        Assert.Equal(2, g.Level);
        Assert.True(g.Score >= time);
    }

    [Fact]
    public void A_life_lost_starts_the_henhouse_again_with_the_eggs_taken_still_gone()
    {
        var g = Engine();
        g.Go(1);
        var w = g.World;
        int c0 = -1, r0 = -1;
        for (int c = 0; c < Field.Cols && c0 < 0; c++)
            for (int r = 0; r < Field.Rows; r++)
                if (w.Grid[c, r] == Tile.Egg) { c0 = c; r0 = r; break; }
        w.Grid[c0, r0] = Tile.Empty;
        w.EggsLeft--;
        Sim.Die(w, "test");
        g.Run((int)(60 * GameEngine.DyingSeconds) + 5);
        Assert.Equal(GameEngine.Lives0 - 1, g.Lives);
        Assert.Equal(Tile.Empty, g.World.Grid[c0, r0]);
        Assert.Equal(HouseBook.EggCount - 1, g.World.EggsLeft);
        Assert.Equal((g.World.Def.Start.Col + 0.5) * T, g.World.Harry.X);
    }

    [Fact]
    public void Lives_run_out_and_the_game_is_over()
    {
        var g = Engine();
        g.Go(1);
        for (int i = 0; i < GameEngine.Lives0; i++)
        {
            Sim.Die(g.World, "test");
            g.Run((int)(60 * GameEngine.DyingSeconds) + 5);
        }
        Assert.Equal(GameState.GameOver, g.State);
    }

    [Fact]
    public void Left_alone_the_title_shows_the_computer_playing_then_the_high_scores()
    {
        var g = Engine();
        g.Run((int)(60 * GameEngine.TitleIdleSeconds) + 5);
        Assert.Equal(GameState.Attract, g.State);
        for (int i = 0; i < 60 * (GameEngine.AttractSeconds + 5) && g.State == GameState.Attract; i++) g.Tick(default);
        Assert.Equal(GameState.HighScores, g.State);
    }

    [Fact]
    public void Pause_stops_the_clock()
    {
        var g = Engine();
        g.Go();
        g.Run(60 * 2);
        int time = g.World.Time;
        g.Pause();
        g.Run(60 * 3);
        Assert.Equal(time, g.World.Time);
        g.Resume();
        g.Run(60 * 2);
        Assert.True(g.World.Time < time);
    }

    [Fact]
    public void A_copy_of_the_world_goes_its_own_way()
    {
        var w = In(Little(), 15.5, 17);
        var c = w.Copy();
        Run(c, 20, new Controls(1));
        Assert.Equal(1, w.EggsLeft);
        Assert.Equal(0, c.EggsLeft);
    }

    [Fact]
    public void The_keys_reach_the_controls()
    {
        var input = new InputHandler();
        input.OnKeyDown(Key.Z);
        Assert.Equal(-1, input.Player.Dx);
        input.OnKeyUp(Key.Z);
        input.OnKeyDown(Key.Right);
        Assert.Equal(1, input.Player.Dx);
        input.OnKeyUp(Key.Right);
        input.OnKeyDown(Key.Up);
        Assert.Equal(-1, input.Player.Dy);
        input.OnKeyUp(Key.Up);
        input.OnKeyDown(Key.Space);
        Assert.True(input.Player.Jump);
        input.OnKeyUp(Key.Space);
        Assert.Equal(default, input.Player);
        input.Dispose();

        var g = Engine();
        g.Go();
        double x = g.World.Harry.X;
        g.Run(20, new Controls(1));
        Assert.True(g.World.Harry.X > x + 30);
    }

    // ───────────────────────────── the computer Harry ─────────────────────────────

    [Fact]
    public void The_computer_collects_every_egg_in_the_first_henhouses_and_just_running_about_does_not()
    {
        foreach (int n in new[] { 0, 3, 6 })
        {
            var w = new World();
            Sim.Start(w, HouseBook.Layout(n));
            var p = new Pilot();
            int lives = 5;
            for (int t = 0; t < 60 * 200 && w.EggsLeft > 0 && lives > 0; t++)
            {
                Sim.Step(w, p.Decide(w));
                if (w.Harry.Dead) { lives--; Sim.Reset(w); p.Reset(); }
            }
            Assert.Equal(0, w.EggsLeft);
        }
        var lazy = new World();
        Sim.Start(lazy, HouseBook.Layout(0));
        int dead = 0;
        for (int t = 0; t < 60 * 200 && lazy.EggsLeft > 0 && dead < 5; t++)
        {
            Sim.Step(lazy, new Controls(t / 200 % 2 == 0 ? 1 : -1, t / 90 % 3 - 1, t % 40 == 0));
            if (lazy.Harry.Dead) { dead++; Sim.Reset(lazy); }
        }
        Assert.True(lazy.EggsLeft > 0);
    }
}
