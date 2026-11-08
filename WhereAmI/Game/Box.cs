namespace WhereAmI.Game;

/// <summary>Axis-aligned rectangle in world units (Y grows downwards).</summary>
public readonly record struct Box(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Right => X + Width;
    public double Top => Y;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;

    /// <summary>True when the boxes share area; touching edges do not count.</summary>
    public bool Overlaps(Box other) =>
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
}
