using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WhereAmI.Core;
using WhereAmI.Levels;

namespace WhereAmI.UI;

/// <summary>Levels shown as lanterns along a winding trail through the fog.</summary>
public partial class LevelSelectScreen : Screen
{
    private readonly IReadOnlyList<LevelDefinition> _levels;
    private readonly Progress _progress;
    private readonly List<Button> _nodes = new();
    private int _selected;

    public LevelSelectScreen(IReadOnlyList<LevelDefinition> levels, Progress progress, int selected)
    {
        InitializeComponent();
        _levels = levels;
        _progress = progress;
        _selected = Math.Clamp(selected, 0, levels.Count - 1);

        for (var i = 0; i < levels.Count; i++)
            _nodes.Add(CreateNode(i));

        Trail.SizeChanged += (_, _) => LayoutTrail();
        PreviewKeyDown += OnPreviewKeyDown;
        ShowDetails(_selected);
    }

    public event Action<int> PlayRequested;
    public event Action BackRequested;

    protected override IInputElement InitialFocus => _nodes[_selected];

    public override void OnBack() => BackRequested?.Invoke();

    private bool IsUnlocked(int index) => index < _progress.UnlockedLevels;

    private Button CreateNode(int index)
    {
        var unlocked = IsUnlocked(index);
        var record = _progress.RecordFor(index);

        var node = new Button
        {
            Style = (Style)Resources["TrailNode"],
            Tag = !unlocked ? "locked" : record is not null ? "done" : "open",
            Content = unlocked
                ? new TextBlock
                {
                    Text = (index + 1).ToString(),
                    FontFamily = (FontFamily)FindResource("HeadingFont"),
                    FontSize = 40,
                    Foreground = (Brush)FindResource("TextBrush"),
                }
                : LockIcon(),
        };

        node.GotKeyboardFocus += (_, _) =>
        {
            _selected = index;
            ShowDetails(index);
        };
        node.Click += (_, _) => Play(index);
        return node;
    }

    private void Play(int index)
    {
        if (IsUnlocked(index))
            PlayRequested?.Invoke(index);
    }

    private void Play_Click(object sender, RoutedEventArgs e) => Play(_selected);

    private void ShowDetails(int index)
    {
        var level = _levels[index];
        var unlocked = IsUnlocked(index);
        var record = _progress.RecordFor(index);
        var totalCoins = level.Coins.Count + level.RewardBoxes.Sum(b => b.Coins);

        DetailNumber.Text = $"LEVEL {index + 1}";
        DetailName.Text = unlocked ? level.Name : "Hidden in the fog";
        DetailStars.SetStars(record?.BestStars ?? 0);
        DetailStats.Text = !unlocked ? "Finish the previous level to light this lantern."
            : record is null ? $"Not finished yet  ·  {totalCoins} coins to find"
            : $"Best score {record.BestScore:N0}  ·  Coins {record.BestCoins}/{totalCoins}";
        PlayButton.IsEnabled = unlocked;
        PlayButton.Content = record is null ? "Play" : "Play again";
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var step = e.Key switch
        {
            Key.Left or Key.A => -1,
            Key.Right or Key.D => 1,
            _ => 0,
        };

        if (step != 0)
        {
            var next = Math.Clamp(_selected + step, 0, _nodes.Count - 1);
            Keyboard.Focus(_nodes[next]);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && _nodes.Contains(Keyboard.FocusedElement))
        {
            Keyboard.Focus(PlayButton);
            e.Handled = true;
        }
        else if (e.Key == Key.Up && ReferenceEquals(Keyboard.FocusedElement, PlayButton))
        {
            Keyboard.Focus(_nodes[_selected]);
            e.Handled = true;
        }
    }

    /// <summary>Places lanterns along a gentle wave and draws the dotted trail between them.</summary>
    private void LayoutTrail()
    {
        Trail.Children.Clear();
        var width = Trail.ActualWidth;
        var height = Trail.ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        // One extra, unreachable lantern hints that the trail continues.
        var count = _nodes.Count + 1;
        var points = Enumerable.Range(0, count)
            .Select(i =>
            {
                var t = count == 1 ? 0.5 : (double)i / (count - 1);
                var x = width * (0.18 + 0.64 * t);
                var y = height * (0.5 + 0.28 * Math.Sin(t * Math.PI * 1.6 + 0.6));
                return new Point(x, y);
            })
            .ToList();

        for (var i = 0; i < count - 1; i++)
            Trail.Children.Add(TrailSegment(points[i], points[i + 1], lit: IsUnlocked(i + 1)));

        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            Canvas.SetLeft(node, points[i].X - node.Width / 2);
            Canvas.SetTop(node, points[i].Y - node.Height / 2);
            Trail.Children.Add(node);
            Trail.Children.Add(NodeLabel(i, points[i]));
        }

        Trail.Children.Add(MysteryNode(points[^1]));
    }

    private static Path TrailSegment(Point from, Point to, bool lit)
    {
        var mid = (to.X - from.X) / 2;
        var figure = new PathFigure { StartPoint = from };
        figure.Segments.Add(new BezierSegment(new Point(from.X + mid, from.Y), new Point(to.X - mid, to.Y), to, true));

        return new Path
        {
            Data = new PathGeometry(new[] { figure }),
            Stroke = lit ? new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xB5, 0x47)) : new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = 4,
            StrokeDashArray = new DoubleCollection { 0.1, 3 },
            StrokeDashCap = PenLineCap.Round,
            IsHitTestVisible = false,
        };
    }

    private TextBlock NodeLabel(int index, Point at)
    {
        var label = new TextBlock
        {
            Text = IsUnlocked(index) ? _levels[index].Name : "???",
            Style = (Style)FindResource("LabelText"),
            Width = 220,
            TextAlignment = TextAlignment.Center,
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(label, at.X - 110);
        Canvas.SetTop(label, at.Y + 64);
        return label;
    }

    private UIElement MysteryNode(Point at)
    {
        var grid = new Grid { Width = 72, Height = 72, Opacity = 0.45, IsHitTestVisible = false };
        grid.Children.Add(new Ellipse
        {
            Stroke = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 2, 2 },
        });
        grid.Children.Add(new TextBlock
        {
            Text = "?",
            FontFamily = (FontFamily)FindResource("HeadingFont"),
            FontSize = 34,
            Foreground = (Brush)FindResource("TextDimBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Canvas.SetLeft(grid, at.X - 36);
        Canvas.SetTop(grid, at.Y - 36);
        return grid;
    }

    private UIElement LockIcon() => new Path
    {
        // A small padlock: shackle arc plus body.
        Data = Geometry.Parse("M 6,12 L 6,8 A 8,8 0 0 1 22,8 L 22,12 M 2,12 L 26,12 L 26,30 L 2,30 Z"),
        Stroke = (Brush)FindResource("TextDimBrush"),
        StrokeThickness = 2.5,
        Width = 28,
        Height = 32,
        Stretch = Stretch.Uniform,
    };
}
