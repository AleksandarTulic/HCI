using System;
using WhereAmI.Game;

namespace WhereAmI.Rendering;

/// <summary>
/// Follows the player smoothly, looks a little ahead in the walking direction and can shake.
/// Updated with the game's fixed step so it behaves the same at any frame rate.
/// </summary>
public sealed class Camera
{
    private const double LookAhead = 140;      // world units shown in front of the player
    private const double FollowSharpness = 6;  // higher follows tighter
    private const double MaxShake = 14;        // world units at full trauma
    private const double TraumaDecay = 1.8;    // per second

    private readonly Random _random = new();
    private GameWorld _world;
    private double _centerX;
    private double _lookAhead;
    private double _trauma;

    /// <summary>World x shown in the middle of the screen.</summary>
    public double CenterX => _centerX;

    public double ShakeX { get; private set; }
    public double ShakeY { get; private set; }

    /// <summary>Set to false to disable screen shake (e.g. for players sensitive to motion).</summary>
    public bool ShakeEnabled { get; set; } = true;

    /// <summary>Slowly drifts back and forth across a level, for the scenery behind the menus.</summary>
    public void UpdateDrift(double dt, GameWorld world)
    {
        const double speed = 45;
        const double margin = 900;

        if (!ReferenceEquals(world, _world))
        {
            _world = world;
            _centerX = margin;
            _driftDirection = 1;
        }

        _centerX += _driftDirection * speed * dt;
        if (_centerX > world.Level.Width - margin || _centerX < margin)
            _driftDirection = -_driftDirection;

        _trauma = 0;
        ShakeX = ShakeY = 0;
    }

    private int _driftDirection = 1;

    /// <summary>Adds screen shake; 0.3 is a small bump, 1 is the strongest.</summary>
    public void AddTrauma(double amount) => _trauma = Math.Min(1, _trauma + amount);

    public void Update(double dt, GameWorld world)
    {
        var player = world.Player;

        // New level or respawn: jump straight to the player.
        if (!ReferenceEquals(world, _world))
        {
            _world = world;
            _centerX = player.Bounds.CenterX;
            _lookAhead = 0;
        }

        if (!player.IsDead)
        {
            var targetLookAhead = Math.Abs(player.VelocityX) > 20 ? Math.Sign(player.VelocityX) * LookAhead : _lookAhead;
            _lookAhead += (targetLookAhead - _lookAhead) * (1 - Math.Exp(-2.5 * dt));

            var target = player.Bounds.CenterX + _lookAhead;
            _centerX += (target - _centerX) * (1 - Math.Exp(-FollowSharpness * dt));
        }

        _trauma = Math.Max(0, _trauma - TraumaDecay * dt);
        var shake = ShakeEnabled ? _trauma * _trauma * MaxShake : 0;
        ShakeX = (_random.NextDouble() * 2 - 1) * shake;
        ShakeY = (_random.NextDouble() * 2 - 1) * shake;
    }
}
