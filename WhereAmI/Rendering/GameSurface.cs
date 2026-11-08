using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WhereAmI.Game;

namespace WhereAmI.Rendering;

/// <summary>Draws the current level. Call <see cref="UIElement.InvalidateVisual"/> once per frame.</summary>
public sealed class GameSurface : FrameworkElement
{
    // The chicken's 128 px frames cover 1.2 Blender units; drawn this size its body is about 64 units tall.
    private const double PlayerFrameSize = 78;
    private const double PlayerFeetOffset = 3;   // the feet stand a little above the bottom of the frame

    private static readonly Typeface TextFace = new("Segoe UI");
    private static readonly Brush ScoreBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE0, 0x9A, 0x1A)));
    private static readonly Brush DustBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)));
    private static readonly Brush SparkBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x3D)));
    private static readonly Brush FeatherBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x20)));
    private static readonly Brush WindBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xF8)));

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
        DrawGusts(dc, world);
        DrawEnemies(dc, world);
        DrawParticles(dc, world, ParticleKind.Dust);
        if (ShowPlayer)
            DrawPlayer(dc, world.Player);
        DrawProjectiles(dc, world);
        DrawParticles(dc, world, ParticleKind.Spark);
        DrawParticles(dc, world, ParticleKind.Feather);
        DrawParticles(dc, world, ParticleKind.Shell);
        DrawParticles(dc, world, ParticleKind.Wind);
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

        var t = player.AnimationTime;
        var frame = player.Animation switch
        {
            PlayerAnimation.Walk => _sprites.PlayerWalk.FrameAt(t),
            PlayerAnimation.Flutter => _sprites.PlayerFlutter.FrameAt(t),
            PlayerAnimation.Throw => _sprites.PlayerThrow.FrameAt(t),
            PlayerAnimation.Jump => _sprites.PlayerJump.Frame(JumpFrame(player)),
            _ => _sprites.PlayerIdle.FrameAt(t),
        };
        var bounds = player.Bounds;

        // Squash and stretch keep the feet planted: wider and flatter on landing, taller on take-off.
        var width = PlayerFrameSize * (1 + player.Squash);
        var height = PlayerFrameSize * (1 - player.Squash);
        var rect = new Rect(bounds.CenterX - width / 2, bounds.Bottom + PlayerFeetOffset - height, width, height);

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

    /// <summary>Jump frames follow the vertical speed: take-off, rising, top, falling.</summary>
    private static int JumpFrame(Player player) =>
        player.AnimationTime < 0.08 && player.VelocityY < 0 ? 0
        : player.VelocityY < -250 ? 1
        : player.VelocityY < 250 ? 2
        : 3;

    private void DrawParticles(DrawingContext dc, GameWorld world, ParticleKind kind)
    {
        foreach (var p in world.Particles)
        {
            if (p.Kind != kind)
                continue;

            var fade = 1 - p.Progress;
            switch (kind)
            {
                case ParticleKind.Shell:
                    dc.PushOpacity(Math.Min(1, fade * 2));
                    dc.PushTransform(new RotateTransform(p.Rotation, p.X, p.Y));
                    dc.DrawImage(_sprites.EggShells.Frame(p.Variant), new Rect(p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size));
                    dc.Pop();
                    dc.Pop();
                    continue;
                case ParticleKind.Feather:
                    dc.PushOpacity(0.8 * fade);
                    dc.PushTransform(new RotateTransform(p.Rotation, p.X, p.Y));
                    dc.DrawEllipse(FeatherBrush, null, new Point(p.X, p.Y), p.Size, p.Size * 0.35);
                    dc.Pop();
                    dc.Pop();
                    continue;
                case ParticleKind.Wind:
                    dc.PushOpacity(0.55 * Math.Sin(p.Progress * Math.PI));
                    dc.DrawRoundedRectangle(WindBrush, null, new Rect(p.X - p.Size / 2, p.Y - 1.5, p.Size, 3), 1.5, 1.5);
                    dc.Pop();
                    continue;
            }

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

            var b = enemy.Bounds;
            var t = enemy.AnimationTime;
            switch (enemy.Kind)
            {
                case EnemyKind.Ghost:
                    // A faded ghost is only a faint shape that cannot be touched.
                    dc.PushOpacity(enemy.Special ? 0.25 : 1);
                    dc.DrawImage(_sprites.Ghost.FrameAt(t), new Rect(b.X, b.Y, b.Width, b.Height));
                    dc.Pop();
                    break;
                case EnemyKind.Scribble:
                    dc.DrawImage(_sprites.Scribble.FrameAt(enemy.Special ? t * 2 : t), new Rect(b.X, b.Y, b.Width, b.Height));
                    break;
                case EnemyKind.Moth:
                    ProceduralArt.DrawHalo(dc, new Point(b.CenterX, b.CenterY + 6), 50, 44, world.Time, 0.35);
                    DrawCreature(dc, _sprites.Moth, _sprites.Moth.FrameAt(t), b.CenterX, b.CenterY + 34, 68, enemy.FacingLeft);
                    break;
                case EnemyKind.Owl:
                    // Idle and flap frames share one scale (the idle owl's height) so the owl does not jump in size.
                    var owl = enemy.Special ? _sprites.OwlFlap.FrameAt(t) : _sprites.OwlIdle.FrameAt(t);
                    DrawCreature(dc, _sprites.OwlIdle, owl, b.CenterX, b.Bottom + 8, 84, enemy.FacingLeft);
                    break;
                case EnemyKind.Burr:
                    DrawCreature(dc, _sprites.BurrWalk, _sprites.BurrWalk.FrameAt(t), b.CenterX, b.Bottom + 2, 46, enemy.FacingLeft);
                    break;
                case EnemyKind.Toad:
                    DrawCreature(dc, _sprites.ToadIdle, ToadFrame(enemy), b.CenterX, b.Bottom + 2, 48, enemy.FacingLeft);
                    break;
            }
        }
    }

    /// <summary>Crouches just before a jump, then rises, falls and squashes on landing.</summary>
    private System.Windows.Media.Imaging.BitmapSource ToadFrame(Enemy toad)
    {
        if (!toad.OnGround)
            return _sprites.ToadJump.Frame(toad.VelocityY < 0 ? 1 : 2);
        if (toad.SinceLanding < 0.15)
            return _sprites.ToadJump.Frame(3);
        if (toad.Timer > Tuning.ToadJumpInterval - 0.25)
            return _sprites.ToadJump.Frame(0);
        return _sprites.ToadIdle.FrameAt(toad.AnimationTime);
    }

    /// <summary>
    /// Draws a frame so the creature itself (not the empty space around it) is <paramref name="height"/>
    /// units tall, standing with its feet at (<paramref name="centerX"/>, <paramref name="bottom"/>).
    /// The scale comes from <paramref name="reference"/> so all frames of a creature share it.
    /// </summary>
    private static void DrawCreature(DrawingContext dc, SpriteAnimation reference, System.Windows.Media.Imaging.BitmapSource frame,
        double centerX, double bottom, double height, bool facingLeft)
    {
        var content = reference.ContentBounds;
        var scale = height / content.Height;
        var contentCenterX = content.X + content.Width / 2;
        var rect = new Rect(
            centerX - contentCenterX * scale,
            bottom - content.Bottom * scale,
            frame.PixelWidth * scale,
            frame.PixelHeight * scale);
        DrawFacing(dc, frame, rect, facingLeft, centerX);
    }

    /// <summary>Blender sprites face right; mirror them for enemies walking left.</summary>
    private static void DrawFacing(DrawingContext dc, ImageSource image, Rect rect, bool facingLeft, double? mirrorX = null)
    {
        if (facingLeft)
            dc.PushTransform(new ScaleTransform(-1, 1, mirrorX ?? rect.X + rect.Width / 2, 0));
        dc.DrawImage(image, rect);
        if (facingLeft)
            dc.Pop();
    }

    private void DrawProjectiles(DrawingContext dc, GameWorld world)
    {
        foreach (var shot in world.Projectiles)
        {
            var b = shot.Bounds;
            if (shot.Kind == ProjectileKind.Egg)
            {
                // The egg tumbles as it flies.
                var spin = shot.Age * 540 * Math.Sign(shot.VelocityX == 0 ? 1 : shot.VelocityX);
                dc.PushTransform(new RotateTransform(spin, b.CenterX, b.CenterY));
                dc.DrawImage(_sprites.Egg, new Rect(b.X, b.Y, b.Width, b.Height));
                dc.Pop();
            }
            else
            {
                ProceduralArt.DrawHalo(dc, new Point(b.CenterX, b.CenterY), 34, 34, world.Time * 3, 0.5);
                var angle = Math.Atan2(shot.VelocityY, shot.VelocityX) * 180 / Math.PI;
                dc.PushTransform(new RotateTransform(angle, b.CenterX, b.CenterY));
                dc.DrawImage(_sprites.Ember.FrameAt(shot.Age), new Rect(b.CenterX - 16, b.CenterY - 16, 32, 32));
                dc.Pop();
            }
        }
    }

    /// <summary>A faint band of moving air in front of a flapping owl.</summary>
    private static void DrawGusts(DrawingContext dc, GameWorld world)
    {
        foreach (var (area, _) in world.Gusts)
        {
            dc.PushOpacity(0.12);
            dc.DrawRoundedRectangle(WindBrush, null, new Rect(area.X, area.Y, area.Width, area.Height), 60, 60);
            dc.Pop();
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
        foreach (var e in world.Enemies) if (e.Alive) Outline(e.Bounds, e.IsTangible ? Colors.Orange : Colors.LightGray);
        foreach (var s in world.Projectiles) Outline(s.Bounds, Colors.Crimson);
        foreach (var (area, _) in world.Gusts) Outline(area, Colors.SkyBlue);
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
