using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAChuckieEgg.Engine;
using AVAChuckieEgg.Views;
using Controls = AVAChuckieEgg.Engine.Controls;

namespace AVAChuckieEgg.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set EGG_SHOTS to a folder to
/// save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open()
    {
        var g = new GameEngine(HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "avachuckieegg-ui-" + Guid.NewGuid().ToString("N")))) { Silent = true };
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("EGG_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAChuckieEgg-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>Lets the computer Harry play until something worth a picture is on screen.</summary>
    private static bool PlayUntil(GameEngine g, GameCanvas c, Func<GameEngine, bool> done, int maxSeconds = 200)
    {
        for (int t = 0; t < 60 * maxSeconds && g.State == GameState.Playing; t++)
        {
            if (done(g)) { c.Advance(0.016); return true; }
            g.Tick(g.Pilot.Decide(g));
            if (t % 3 == 0) c.Advance(1 / 20.0);
        }
        return false;
    }

    private static void Moment(int level, string name, Func<GameEngine, bool> moment)
    {
        var (w, c, g) = Open();
        g.Go(level);
        g.SetLives(20);
        Assert.True(PlayUntil(g, c, moment), $"never saw the moment for {name}");
        Shot(w, c, name);
    }

    [AvaloniaFact]
    public void The_title_and_the_high_scores()
    {
        var (w, c, g) = Open();
        g.Idle(30);
        c.Advance(2.1);
        Shot(w, c, "title");
        g.HighScores.Insert("AVA", 41270, 9);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + GameEngine.AttractSeconds)) + 10);
        for (int i = 0; i < 60 * 200 && g.State != GameState.HighScores; i++) g.Idle(1);
        Assert.Equal(GameState.HighScores, g.State);
        Shot(w, c, "high-scores");
    }

    [AvaloniaFact]
    public void The_first_henhouse() => Moment(1, "henhouse", g => g.World.EggsLeft <= 9 && !g.World.Harry.Airborne && !g.World.Harry.Climbing);

    [AvaloniaFact]
    public void Climbing() => Moment(2, "ladder", g => g.World.Harry.Climbing && g.World.Harry.Y % Field.Tile > 8 && g.World.EggsLeft < 11);

    [AvaloniaFact]
    public void Jumping() => Moment(3, "jump", g => g.World.Harry.Airborne && g.World.Harry.Vy is > -120 and < 0 && g.World.EggsLeft < 11);

    [AvaloniaFact]
    public void A_hen_close() => Moment(4, "hen", g => g.World.Hens.Any(h => Math.Abs(h.X - g.World.Harry.X) < 80 && Math.Abs(h.Y - g.World.Harry.Y) < 10) && g.World.EggsLeft < 11);

    [AvaloniaFact]
    public void The_duck_is_out() => Moment(9, "duck", g => g.World.EggsLeft <= 8 && Math.Abs(g.World.DuckY - g.World.Harry.Y) < 260);

    [AvaloniaFact]
    public void Every_egg() => Moment(5, "all-eggs", g => g.Phase == Phase.Done && g.PhaseTime > 0.8);

    [AvaloniaFact]
    public void Game_over()
    {
        var (w, c, g) = Open();
        g.Go(6);
        g.SetLives(1);
        g.World.Score = 8340;
        Sim.Die(g.World, "test");
        for (int i = 0; i < 600 && !(g.State == GameState.GameOver && g.StateTime > 1.5); i++) { g.Tick(default); if (i % 3 == 0) c.Advance(0.05); }
        Assert.Equal(GameState.GameOver, g.State);
        Shot(w, c, "game-over");
    }
}
