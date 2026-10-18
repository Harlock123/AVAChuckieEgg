namespace AVAChuckieEgg.Engine;

public enum GameState { Title, Attract, Playing, Paused, GameOver, EnterInitials, HighScores }

public enum Phase
{
    /// <summary>The level's number, a moment before it starts.</summary>
    Start,
    Play,
    /// <summary>Lost: a moment, then back to the start of the henhouse (eggs taken stay taken), or the game is over.</summary>
    Dying,
    /// <summary>Every egg collected: the time left counted into the score.</summary>
    Done,
}

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Jump, Land, Egg, Grain, Die, Tick, Cluck, Quack, Done,
    SayDone,
    Music, MusicStop, TitleTune, GameOver, Victory,
}

/// <summary>
/// The whole game with no UI. Call <see cref="Update"/> with the elapsed time and the controls; it runs in fixed
/// 60 Hz steps, so it plays the same at any frame rate.
/// </summary>
public sealed class GameEngine
{
    public const double TitleIdleSeconds = 16;
    public const double AttractSeconds = 50;
    public const double HighScoreSeconds = 9;
    public const double StartSeconds = 1.6, DyingSeconds = 1.8, DoneSeconds = 4, OverSeconds = 4;
    public const int Lives0 = 5, ExtraLifeEvery = 10000;

    private double _accumulator, _idle, _cluck;
    private bool _startPrev, _tunePending = true, _music;
    private int _nextExtra = ExtraLifeEvery, _attractLevel;

    public GameEngine(HighScoreTable? table = null)
    {
        HighScores = table ?? HighScoreTable.Load();
        NewGame(1);
        State = GameState.Title;
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public Phase Phase { get; private set; } = Phase.Start;
    public double StateTime { get; private set; }
    public double PhaseTime { get; private set; }
    public HighScoreTable HighScores { get; }
    public InitialsEntry? Initials { get; private set; }
    public int NewEntryIndex { get; private set; } = -1;
    public bool Silent { get; set; }
    public World World { get; private set; } = new();
    public Pilot Pilot { get; } = new();
    public int Lives { get; private set; }
    public int Level { get; private set; } = 1;
    public int Score { get; private set; }
    public string? Message { get; private set; }
    public double MessageTime { get; private set; }

    private void Play(Sfx s)
    {
        if (!Silent) Sound?.Invoke(s);
    }

    public void Update(double dt, Controls c, bool start)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Field.Step)
        {
            _accumulator -= Field.Step;
            Tick(c, start);
        }
    }

    public void Tick(Controls c, bool start = false)
    {
        double dt = Field.Step;
        StateTime += dt;
        bool startPressed = start && !_startPrev;
        _startPrev = start;
        if (MessageTime > 0) { MessageTime -= dt; if (MessageTime <= 0) Message = null; }
        switch (State)
        {
            case GameState.Title:
                if (_tunePending) { _tunePending = false; Play(Sfx.TitleTune); }
                _idle += dt;
                if (startPressed || c.Jump && StateTime > 0.5) StartGame();
                else if (_idle > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Attract:
                Simulate(dt, Pilot.Decide(this));
                if (startPressed) { StartGame(); break; }
                if (State == GameState.Attract && StateTime > AttractSeconds) { Music(false); ShowHighScores(-1); }
                break;
            case GameState.Playing:
                Simulate(dt, c);
                break;
            case GameState.GameOver:
                if (StateTime > OverSeconds)
                {
                    if (HighScores.Qualifies(Score))
                    {
                        State = GameState.EnterInitials;
                        StateTime = 0;
                        Initials = new InitialsEntry(HighScores.LastInitials);
                    }
                    else ShowHighScores(-1);
                }
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    int place = HighScores.Insert(done.Initials, Score, Level);
                    HighScores.Save();
                    ShowHighScores(place);
                }
                break;
            case GameState.HighScores:
                if (startPressed) StartGame();
                else if (StateTime > HighScoreSeconds) ReturnToTitle();
                break;
        }
    }

    private void NewGame(int level)
    {
        Lives = Lives0;
        Score = 0;
        _nextExtra = ExtraLifeEvery;
        Load(level);
    }

    private void Load(int level)
    {
        Level = level;
        World = new World();
        Sim.Start(World, HouseBook.Layout((level - 1) % HouseBook.Layouts), (level - 1) / HouseBook.Layouts);
        World.Score = Score;
        SetPhase(Phase.Start);
        Message = $"LEVEL {level}";
        MessageTime = StartSeconds;
        Pilot.Reset();
    }

    public void StartGame(int level = 1)
    {
        NewGame(level);
        State = GameState.Playing;
        StateTime = 0;
        NewEntryIndex = -1;
    }

    private void StartAttract()
    {
        NewGame(1 + _attractLevel++ % 3 * 4);
        State = GameState.Attract;
        StateTime = 0;
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        _idle = 0;
        _tunePending = true;
        Music(false);
        Message = null;
        NewGame(1);
    }

    private void ShowHighScores(int place)
    {
        State = GameState.HighScores;
        StateTime = 0;
        NewEntryIndex = place;
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        State = GameState.Paused;
        Music(false);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        State = GameState.Playing;
        if (Phase == Phase.Play) Music(true);
    }

    private void Music(bool on)
    {
        if (on == _music) return;
        _music = on;
        Play(on ? Sfx.Music : Sfx.MusicStop);
    }

    private void SetPhase(Phase p)
    {
        Phase = p;
        PhaseTime = 0;
    }

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        var w = World;
        switch (Phase)
        {
            case Phase.Start:
                if (PhaseTime > StartSeconds) { SetPhase(Phase.Play); Music(true); }
                break;
            case Phase.Play:
                Sim.Step(w, c);
                Score = w.Score;
                if (Score >= _nextExtra) { Lives++; _nextExtra += ExtraLifeEvery; }
                // Now and then a hen clucks, and the free duck quacks.
                if ((_cluck -= dt) <= 0)
                {
                    _cluck = 3 + w.Dice.Next() * 4;
                    Play(w.DuckFree && w.Dice.Next(2) == 0 ? Sfx.Quack : Sfx.Cluck);
                }
                if (w.EggsLeft == 0)
                {
                    SetPhase(Phase.Done);
                    Music(false);
                    Play(Sfx.Done);
                    Play(Sfx.SayDone);
                    Message = "ALL THE EGGS!";
                    MessageTime = DoneSeconds;
                }
                else if (w.Harry.Dead) { SetPhase(Phase.Dying); Music(false); }
                break;
            case Phase.Dying:
                if (PhaseTime > DyingSeconds)
                {
                    Lives--;
                    if (Lives <= 0) { End(); return; }
                    Sim.Reset(w);
                    Pilot.Reset();
                    SetPhase(Phase.Play);
                    Music(true);
                }
                break;
            case Phase.Done:
                // The time left, counted into the score.
                if (PhaseTime > 0.6 && w.Time > 0)
                {
                    w.Time -= 10;
                    w.Score += 10;
                    if (w.Time % 50 == 0) Play(Sfx.Tick);
                }
                Score = w.Score;
                if (Score >= _nextExtra) { Lives++; _nextExtra += ExtraLifeEvery; }
                if (PhaseTime > DoneSeconds && w.Time <= 0)
                {
                    if (State == GameState.Attract) { ShowHighScores(-1); return; }
                    Load(Level + 1);
                    return;
                }
                break;
        }
        foreach (var s in w.Heard) Play(s);
        w.Heard.Clear();
    }

    private void End()
    {
        Play(Sfx.GameOver);
        Music(false);
        if (State == GameState.Attract) { ShowHighScores(-1); return; }
        State = GameState.GameOver;
        StateTime = 0;
    }

    // ───────────────────────────── for tests ─────────────────────────────

    public void Run(int steps, Controls c = default)
    {
        for (int i = 0; i < steps; i++) Tick(c);
    }

    public void Idle(int steps) => Run(steps);

    /// <summary>Straight into a level, past the opening moment.</summary>
    public void Go(int level = 1)
    {
        StartGame(level);
        SetPhase(Phase.Play);
        Message = null;
    }

    public void SetLives(int n) => Lives = n;
}
