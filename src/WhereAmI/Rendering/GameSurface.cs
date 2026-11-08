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
    private static readonly Brush TutorialBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)));
    private static readonly Brush ScoreBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE0, 0x9A, 0x1A)));

    private Sprites _sprites;

    public GameSurface()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        ClipToBounds = true;
    }

    public GameSession Session { get; set; }
    public bool ShowHitboxes { get; set; }

    /// <summary>Top-left corner of the visible part of the world.</summary>
    public Point Camera { get; private set; }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (Session is null)
            return;

        _sprites ??= new Sprites();
        var world = Session.World;

        Camera = FollowPlayer(world);
        dc.PushTransform(new TranslateTransform(-Camera.X, -Camera.Y));

        var level = world.Level;
        var background = SpriteCache.Get(level.Background);
        dc.DrawImage(background, new Rect(0, 0, background.PixelWidth, background.PixelHeight));

        if (level.ShowTutorial)
            DrawTutorial(dc);

        DrawRewardBoxes(dc, world);
        DrawCoins(dc, world);
        DrawEnemies(dc, world);
        DrawPlayer(dc, world.Player);
        DrawEffects(dc, world);

        if (ShowHitboxes)
            DrawHitboxes(dc, world);

        dc.Pop();
    }

    private Point FollowPlayer(GameWorld world)
    {
        var p = world.Player.Bounds;
        var x = Math.Clamp(p.CenterX - ActualWidth / 2, 0, Math.Max(0, world.Level.Width - ActualWidth));
        var y = Math.Clamp(p.CenterY - ActualHeight * 0.55, 0, Math.Max(0, world.Level.Height - ActualHeight));
        return new Point(Math.Round(x), Math.Round(y));
    }

    private void DrawPlayer(DrawingContext dc, Player player)
    {
        var frame = _sprites.ForPlayer(player.Animation).FrameAt(player.AnimationTime);
        var width = frame.PixelWidth * PlayerSpriteScale;
        var height = frame.PixelHeight * PlayerSpriteScale;
        var bounds = player.Bounds;
        var rect = new Rect(bounds.CenterX - width / 2, bounds.Bottom - height, width, height);

        if (player.IsDead)
            dc.PushOpacity(0.35);
        if (player.FacingLeft)
            dc.PushTransform(new ScaleTransform(-1, 1, bounds.CenterX, 0));

        dc.DrawImage(frame, rect);

        if (player.FacingLeft)
            dc.Pop();
        if (player.IsDead)
            dc.Pop();
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
            dc.DrawImage(frame, new Rect(b.CenterX - frame.PixelWidth / 2.0, b.Y, frame.PixelWidth, frame.PixelHeight));
        }
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

    private void DrawTutorial(DrawingContext dc)
    {
        DrawHint(dc, _sprites.IconLeft, 128, "Left arrow / A  –  walk left");
        DrawHint(dc, _sprites.IconRight, 192, "Right arrow / D  –  walk right");
        DrawHint(dc, _sprites.IconJump, 256, "Up arrow / W / Space  –  jump");
        dc.DrawText(Text("M  –  music on/off      Esc  –  quit", 18, TutorialBrush), new Point(20, 316));
    }

    private void DrawHint(DrawingContext dc, ImageSource icon, double y, string text)
    {
        dc.DrawImage(icon, new Rect(20, y, 32, 32));
        dc.DrawText(Text(text, 20, TutorialBrush), new Point(64, y));
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
