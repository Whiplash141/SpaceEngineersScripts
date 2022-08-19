
/// <summary>
/// Draws a simple status bar with a varying fill proportion.
/// </summary>
/// <param name="frame">Frame to draw the sprite to</param>
/// <param name="position">Location of the center of the status bar</param>
/// <param name="size">Total size of the status bar</param>
/// <param name="fillProportion">Fill proportion of the status bar (0 to 1)</param>
/// <param name="backgroundColor">Bar background color</param>
/// <param name="fillColor">Bar fill color</param>
/// <param name="fillFrom">Direction bar fill will grow from</param>
void DrawStatusBar(MySpriteDrawFrame frame, Vector2 position, Vector2 size, float fillProportion, Color backgroundColor, Color fillColor, TextAlignment fillFrom)
{
    fillProportion = MathHelper.Clamp(fillProportion, 0, 1);

    var backgroundSprite = MySprite.CreateSprite("SquareSimple", position, size);
    backgroundSprite.Color = backgroundColor;
    frame.Add(backgroundSprite);

    Vector2 fillSize = size * new Vector2(fillProportion, 1f);
    Vector2 fillPosition;
    switch (fillFrom)
    {
        default:
        case TextAlignment.CENTER:
            fillPosition = position;
            break;
        case TextAlignment.LEFT:
            fillPosition = position + new Vector2(-0.5f * (size.X - fillSize.X), 0);
            break;
        case TextAlignment.RIGHT:
            fillPosition = position + new Vector2(0.5f * (size.X - fillSize.X), 0);
            break;
    }
    var fillSprite = MySprite.CreateSprite("SquareSimple", fillPosition, fillSize);
    fillSprite.Color = fillColor;
    frame.Add(fillSprite);
}
