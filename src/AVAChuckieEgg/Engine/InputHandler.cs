using Avalonia.Input;

namespace AVAChuckieEgg.Engine;

/// <summary>
/// Keyboard and gamepad boiled down to the controls: walk, climb, and jump.
/// </summary>
public sealed class InputHandler : IDisposable
{
    private readonly List<Key> _held = [];
    private readonly ControllerManager _pad = new(0);

    public void OnKeyDown(Key key)
    {
        _held.Remove(key);
        _held.Add(key);
    }

    public void OnKeyUp(Key key) => _held.Remove(key);
    public bool Has(Key key) => _held.Contains(key);
    private bool Any(params Key[] keys) => keys.Any(_held.Contains);

    public void Update()
    {
        _pad.Update();
    }

    /// <summary>
    /// Left and right (or Z and X, as on the BBC Micro, or A and D) to walk; up and down (or W, S, or the slash and
    /// apostrophe) to climb; Space, Ctrl or Enter's neighbour Shift to jump. On a gamepad: the stick or D-pad, and A or B
    /// to jump.
    /// </summary>
    public Controls Player
    {
        get
        {
            var pad = _pad;
            int dx = (Any(Key.Right, Key.X, Key.D, Key.NumPad6) ? 1 : 0) - (Any(Key.Left, Key.Z, Key.A, Key.NumPad4) ? 1 : 0);
            int dy = (Any(Key.Down, Key.S, Key.OemQuestion, Key.NumPad2) ? 1 : 0) - (Any(Key.Up, Key.W, Key.OemQuotes, Key.NumPad8) ? 1 : 0);
            if (dx == 0) dx = pad.DPadLeft || pad.LeftStickX < -0.4f ? -1 : pad.DPadRight || pad.LeftStickX > 0.4f ? 1 : 0;
            if (dy == 0) dy = pad.DPadUp || pad.LeftStickY < -0.5f ? -1 : pad.DPadDown || pad.LeftStickY > 0.5f ? 1 : 0;
            bool jump = Any(Key.Space, Key.LeftCtrl, Key.RightCtrl, Key.LeftShift, Key.RightShift) || pad.ButtonA || pad.ButtonB;
            return new Controls(dx, dy, jump);
        }
    }

    /// <summary>Enter or 1.</summary>
    public bool Start => Any(Key.Enter, Key.D1, Key.NumPad1) || _pad.ButtonStart;

    public bool PadUp => _pad.DPadUp || _pad.LeftStickY < -0.5f;
    public bool PadDown => _pad.DPadDown || _pad.LeftStickY > 0.5f;
    public bool PadLeft => _pad.DPadLeft || _pad.LeftStickX < -0.5f;
    public bool PadRight => _pad.DPadRight || _pad.LeftStickX > 0.5f;
    public bool PadAccept => _pad.ButtonA || _pad.ButtonX || _pad.ButtonB;
    public bool PadDone => _pad.ButtonStart;
    public bool PadPause => _pad.ButtonStart;
    public bool PadBack => _pad.ButtonBack;

    public void Dispose() => _pad.Dispose();
}
