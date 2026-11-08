using System;
using System.Diagnostics;
using System.Windows.Media;

namespace WhereAmI.Core;

/// <summary>
/// Runs game updates at a fixed rate and renders once per display frame.
/// A fixed step keeps physics identical on every machine and refresh rate.
/// </summary>
public sealed class GameLoop
{
    public const double StepSeconds = 1.0 / 120;
    private const int MaxStepsPerFrame = 8;   // avoid a "spiral of death" after a long hitch

    private readonly Action<double> _update;
    private readonly Action _render;
    private readonly Stopwatch _clock = new();

    private TimeSpan _lastRenderingTime;
    private double _previousSeconds;
    private double _accumulator;

    public GameLoop(Action<double> update, Action render)
    {
        _update = update;
        _render = render;
    }

    public void Start()
    {
        _clock.Restart();
        _previousSeconds = 0;
        _accumulator = 0;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop() => CompositionTarget.Rendering -= OnRendering;

    private void OnRendering(object sender, EventArgs e)
    {
        // WPF can raise Rendering more than once per frame; only handle new frames.
        if (e is RenderingEventArgs args)
        {
            if (args.RenderingTime == _lastRenderingTime)
                return;
            _lastRenderingTime = args.RenderingTime;
        }

        var now = _clock.Elapsed.TotalSeconds;
        _accumulator += Math.Min(now - _previousSeconds, 0.25);
        _previousSeconds = now;

        var steps = 0;
        while (_accumulator >= StepSeconds && steps < MaxStepsPerFrame)
        {
            _update(StepSeconds);
            _accumulator -= StepSeconds;
            steps++;
        }

        if (steps == MaxStepsPerFrame)
            _accumulator = 0;

        _render();
    }
}
