using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WhereAmI.Game;

namespace WhereAmI.Rendering;

/// <summary>Draws the current level. Call <see cref="UIElement.InvalidateVisual"/> once per frame.</summary>
public sealed class GameSurface : FrameworkElement
{
    private const double PlayerSpriteScale = 0.64;   // original frames are ~110 px tall

    private static readonly Typeface TextFace = new("Segoe UI");
    private static readonly Brush ScoreBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE0, 0x9A, 0x1A)));
    private static readonly Brush DustBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)));
    private static readonly Brush SparkBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x3D)));

    private Sprites _sprites;

    public GameSurface()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        ClipToBounds = true;
    }

    /// <summary>The level to draw: the running game, or a level shown behind the menus.</summary>
    public GameWorld World { get; set; }

    /// <summary>False behind the menus, where the level is only scenery.</summary>
    public bool ShowPlayer { get; set; } = true;
    public Camera Camera { get; set; }
    public bool ShowHitboxes { get; set; }

    /// <summary>World units are multiplied by this to get screen units (DIPs).</summary>
    public double Scale { get; private set; } = 1;

    /// <summary>Left edge of the visible part of the world, in world units.</summary>
    public double CameraX { get; private set; }

    /// <summary>Where a world point appears on this element, in DIPs.</summary>
    public Point WorldToScreen(double x, double y) => new((x - CameraX) * Scale, y * Scale);

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (World is null || Camera is null || ActualHeight <= 0)
            return;

        _sprites ??= new Sprites();
        var world = World;

        // The whole level height always fills the window; wider windows simply see more of the level.
        Scale = ActualHeight / world.Level.Height;
        CameraX = VisibleLeft(world, ActualWidth / Scale);

        var level = world.Level;
        var visibleWidth = ActualWidth / Scale;
        dc.PushTransform(new ScaleTransform(Scale, Scale));

        // Sky and distant layers are drawn in view space; each layer follows the camera at its own speed.
        ProceduralArt.DrawSky(dc, visibleWidth, level.Height);
        DrawParallax(dc, level, visibleWidth);

        dc.PushTransform(new TranslateTransform(-CameraX + Camera.ShakeX, Camera.ShakeY));

        if (level.Background is { } backgroundPath)
        {
            var background = SpriteCache.Get(backgroundPath);
            dc.DrawImage(background, new Rect(0, 0, background.PixelWidth, background.PixelHeight));
        }
        else
        {
            ProceduralArt.DrawStaticLevel(dc, level);
            foreach (var goal in world.Goals)
                DrawGoalGate(dc, goal, world.Time);
        }

        foreach (var checkpoint in world.Checkpoints)
            DrawCheckpoint(dc, checkpoint, world.Time);
        foreach (var platform in world.Platforms)
            ProceduralArt.DrawPlatform(dc, platform);

        DrawRewardBoxes(dc, world);
        DrawCoins(dc, world);
        DrawEnemies(dc, world);
        DrawParticles(dc, world, ParticleKind.Dust);
        if (ShowPlayer)
            DrawPlayer(dc, world.Player);
        DrawParticles(dc, world, ParticleKind.Spark);
        DrawEffects(dc, world);

        if (ShowHitboxes)
            DrawHitboxes(dc, world);

        dc.Pop();
        dc.Pop();
    }

    /// <summary>Left edge of the view around the camera centre, without showing anything past the level edges.</summary>
    private double VisibleLeft(GameWorld world, double visibleWidth)
    {
        var levelWidth = world.Level.Width;

        // A window wider than the whole level: centre the level instead.
        if (visibleWidth >= levelWidth)
            return -(visibleWidth - levelWidth) / 2;

        var x = Math.Clamp(Camera.CenterX - visibleWidth / 2, 0, levelWidth - visibleWidth);

        // Snap to whole device pixels so the background does not shimmer while scrolling.
        var pixelScale = Scale * VisualTreeHelper.GetDpi(this).DpiScaleX;
        return Math.Round(x * pixelScale) / pixelScale;
    }

    private void DrawPlayer(DrawingContext dc, Player player)
    {
        // Blink while invulnerable after respawning.
        if (player.Invulnerable > 0 && (int)(player.Invulnerable * 12) % 2 == 0)
            return;

        var frame = _sprites.ForPlayer(player.Animation).FrameAt(player.AnimationTime);
        var bounds = player.Bounds;

        // Squash and stretch keep the feet planted: wider and flatter on landing, taller on take-off.
        var scaleX = PlayerSpriteScale * (1 + player.Squash);
        var scaleY = PlayerSpriteScale * (1 - player.Squash);
        var width = frame.PixelWidth * scaleX;
        var height = frame.PixelHeight * scaleY;
        var rect = new Rect(bounds.CenterX - width / 2, bounds.Bottom - height, width, height);

        var pushes = 0;
        if (player.IsDead)
        {
            // Upside down while falling off the screen.
            dc.PushTransform(new ScaleTransform(1, -1, bounds.CenterX, bounds.CenterY));
            pushes++;
        }

        if (player.FacingLeft)
        {
            dc.PushTransform(new ScaleTransform(-1, 1, bounds.CenterX, 0));
            pushes++;
        }

        dc.DrawImage(frame, rect);

        for (var i = 0; i < pushes; i++)
            dc.Pop();
    }

    private static void DrawParticles(DrawingContext dc, GameWorld world, ParticleKind kind)
    {
        foreach (var p in world.Particles)
        {
            if (p.Kind != kind)
                continue;

            var fade = 1 - p.Progress;
            var brush = kind == ParticleKind.Dust ? DustBrush : SparkBrush;
            var radius = p.Size / 2 * (kind == ParticleKind.Dust ? 0.6 + 0.4 * fade : fade);

            dc.PushOpacity(kind == ParticleKind.Dust ? 0.55 * fade : fade);
            dc.DrawEllipse(brush, null, new Point(p.X, p.Y), radius, radius);
            dc.Pop();
        }
    }

    private void DrawCoins(DrawingContext dc, GameWorld world)
    {
        var frame = _sprites.CoinSpin.FrameAt(world.Time);
        foreach (var coin in world.Coins)
        {
            if (coin.Collected)
                continue;

            // Spin frames have different widths; keep them centred on the coin.
            var b = coin.Bounds;
            dc.DrawImage(frame, new Rect(b.X, b.Y, b.Width, b.Height));
        }
    }

    /// <summary>Repeats each parallax layer across the view, scrolled by a fraction of the camera position.</summary>
    private void DrawParallax(DrawingContext dc, Levels.LevelDefinition level, double visibleWidth)
    {
        foreach (var (image, speed) in _sprites.Parallax)
        {
            double width = image.PixelWidth;
            double height = image.PixelHeight;
            var offset = CameraX * speed % width;
            if (offset < 0)
                offset += width;

            for (var x = -offset; x < visibleWidth; x += width)
                dc.DrawImage(image, new Rect(x, level.Height - height, width, height));
        }
    }

    /// <summary>The stone gate is 80x120 around a 64x96 goal area, standing on its bottom edge.</summary>
    private void DrawGoalGate(DrawingContext dc, Game.Box goal, double time)
    {
        ProceduralArt.DrawHalo(dc, new Point(goal.CenterX, goal.CenterY), goal.Width * 1.7, goal.Height * 1.2, time);
        var scale = goal.Width / 64;
        dc.DrawImage(_sprites.GoalGate,
            new Rect(goal.X - 8 * scale, goal.Bottom - 120 * scale, 80 * scale, 120 * scale));
    }

    private void DrawCheckpoint(DrawingContext dc, Checkpoint checkpoint, double time)
    {
        var b = checkpoint.Bounds;
        if (checkpoint.IsLit)
            ProceduralArt.DrawHalo(dc, new Point(b.CenterX, b.Top + 22), 90, 90, time, 0.6);

        var image = checkpoint.IsLit ? _sprites.CheckpointLit : _sprites.CheckpointUnlit;
        dc.DrawImage(image, new Rect(b.X, b.Y, b.Width, b.Height));
    }

    private void DrawRewardBoxes(DrawingContext dc, GameWorld world)
    {
        foreach (var box in world.RewardBoxes)
        {
            if (!box.IsRevealed)
                continue;

            var image = box.CoinsLeft > 0 ? _sprites.RewardBoxFull : _sprites.RewardBoxEmpty;
            var b = box.Bounds;
            dc.DrawImage(image, new Rect(b.X, b.Y, b.Width, b.Height));
        }
    }

    private void DrawEnemies(DrawingContext dc, GameWorld world)
    {
        foreach (var enemy in world.Enemies)
        {
            if (!enemy.Alive)
                continue;

            var frame = _sprites.ForEnemy(enemy.Kind).FrameAt(enemy.AnimationTime);
            dc.DrawImage(frame, new Rect(enemy.X, enemy.Y, enemy.Width, enemy.Height));
        }
    }

    private void DrawEffects(DrawingContext dc, GameWorld world)
    {
        foreach (var effect in world.Effects)
        {
            var t = effect.Progress;
            dc.PushOpacity(1 - t * t);

            switch (effect.Kind)
            {
                case EffectKind.PoppedCoin:
                    var image = _sprites.CoinPopped;
                    dc.DrawImage(image, new Rect(effect.X, effect.Y - 48 * t, image.PixelWidth, image.PixelHeight));
                    break;
                case EffectKind.ScorePopup:
                    dc.DrawText(Text(effect.Text, 22, ScoreBrush, FontWeights.Bold), new Point(effect.X, effect.Y - 40 * t));
                    break;
            }

            dc.Pop();
        }
    }

    private void DrawHitboxes(DrawingContext dc, GameWorld world)
    {
        void Outline(Box b, Color color) =>
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(color), 2), new Rect(b.X, b.Y, b.Width, b.Height));

        foreach (var b in world.Ground) Outline(b, Colors.DodgerBlue);
        foreach (var b in world.Spikes) Outline(b, Colors.Red);
        foreach (var b in world.Goals) Outline(b, Colors.LimeGreen);
        foreach (var b in world.RewardBoxes) Outline(b.Bounds, b.IsRevealed ? Colors.SaddleBrown : Colors.Gray);
        foreach (var c in world.Coins) if (!c.Collected) Outline(c.Bounds, Colors.Gold);
        foreach (var e in world.Enemies) if (e.Alive) Outline(e.Bounds, Colors.Orange);
        foreach (var p in world.Platforms) Outline(p.Bounds, Colors.DodgerBlue);
        foreach (var c in world.Checkpoints) Outline(c.Bounds, Colors.Teal);
        foreach (var h in world.Hints) Outline(h.Area, Colors.Plum);
        Outline(world.Player.Bounds, Colors.Magenta);
    }

    private FormattedText Text(string text, double size, Brush brush, FontWeight? weight = null)
    {
        var face = weight is null ? TextFace : new Typeface(TextFace.FontFamily, FontStyles.Normal, weight.Value, FontStretches.Normal);
        return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
