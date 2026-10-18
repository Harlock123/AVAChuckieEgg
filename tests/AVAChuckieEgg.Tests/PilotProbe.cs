using AVAChuckieEgg.Engine;

namespace AVAChuckieEgg.Tests;

/// <summary>How the computer Harry gets on, level by level (set EGG_PROBE to run it; EGG_PROBE=lazy just walks and jumps).</summary>
public sealed class PilotProbe(ITestOutputHelper output)
{
    [Fact]
    public void How_the_computer_Harry_does()
    {
        if (Environment.GetEnvironmentVariable("EGG_PROBE") is not { } mode) return;
        int total = 0;
        for (int n = 1; n <= 16; n++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var def = HouseBook.Layout((n - 1) % 8);
            var w = new World();
            Sim.Start(w, def, (n - 1) / 8);
            var p = new Pilot();
            var deaths = new List<string>();
            int t = 0;
            for (; t < 60 * 400 && w.EggsLeft > 0; t++)
            {
                var c = mode == "lazy" ? new Controls(t / 200 % 2 == 0 ? 1 : -1, t / 90 % 3 - 1, t % 40 == 0) : p.Decide(w);
                Sim.Step(w, c);
                if (w.Harry.Dead)
                {
                    deaths.Add($"{w.Cause} at {Spot.Of(w.Harry)} [{p.Doing}]");
                    if (deaths.Count >= 8) break;
                    Sim.Reset(w);
                    p.Reset();
                }
            }
            total += deaths.Count;
            output.WriteLine($"{n,2}: {(w.EggsLeft == 0 ? "ALL EGGS" : $"{w.EggsLeft} eggs left")} in {t / 60}s, time left {w.Time}, {deaths.Count} deaths: {string.Join("; ", deaths.Take(3))} [{p.Doing}] {sw.ElapsedMilliseconds} ms");
        }
        output.WriteLine($"{total} deaths in all");
    }
}
