
/// <summary>
/// Draws a line of specified width and color between two points.
/// </summary>
void DrawLine(MySpriteDrawFrame frame, Vector2 point1, Vector2 point2, float width, Color color)
{
    Vector2 position = 0.5f * (point1 + point2);
    Vector2 diff = point1 - point2;
    float length = diff.Length();
    if (length > 0)
        diff /= length;

    Vector2 size = new Vector2(length, width);
    float angle = (float)Math.Acos(Vector2.Dot(diff, Vector2.UnitX));
    angle *= Math.Sign(Vector2.Dot(diff, Vector2.UnitY));

    MySprite sprite = MySprite.CreateSprite("SquareSimple", position, size);
    sprite.RotationOrScale = angle;
    sprite.Color = color;
    frame.Add(sprite);
}
