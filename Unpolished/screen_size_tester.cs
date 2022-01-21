/*
/ //// / Screen Size Tester v1.0.0 - 2020-03-20 / //// /

Instructions:
Place this in a programmable block and it will draw the screen sizes on any text
surface or text surface providers it finds. Run the script manually to update
for added blocks.
*/

Program()
{
    DebugScreens();
}

void Main()
{
    DebugScreens();
}

List<IMyTextSurface> _textSurfaces = new List<IMyTextSurface>();
List<IMyTextSurfaceProvider> _tsp = new List<IMyTextSurfaceProvider>();
List<IMyTextSurface> _ts = new List<IMyTextSurface>();
void DebugScreens()
{
    _textSurfaces.Clear();
    GridTerminalSystem.GetBlocksOfType(_tsp);
    GridTerminalSystem.GetBlocksOfType(_ts);

    foreach(var tsp in _tsp)
    {
        for (int i = 0; i < tsp.SurfaceCount; ++i)
        {
            Draw(tsp.GetSurface(i));
        }
    }

    foreach (var surf in _ts)
    {
        Draw(surf);
    }

    Echo("Drew screen size sprites\nRun again to pickup block changes!");
}

float _textScale = 0.5f;
float _crossWidth = 10;
Vector2 _screenMarkerSize = new Vector2(40, 40);
void Draw(IMyTextSurface surface)
{
    surface.ContentType = ContentType.SCRIPT;
    surface.Script = "";
    var textureSize = surface.TextureSize;
    var surfaceSize = surface.SurfaceSize;
    var center = textureSize * 0.5f;
    var offset = (textureSize - surfaceSize) * 0.5f;

    var textSize = _textScale; // * 512f / surfaceSize.Y;

    using (var frame = surface.DrawFrame())
    {
        // Draw background
        MySprite sprite;
        sprite = MySprite.CreateSprite("SquareSimple", center, textureSize);
        sprite.Color = Color.Black;
        frame.Add(sprite);
        
        sprite = MySprite.CreateSprite("SquareSimple", center, 2f * _screenMarkerSize);
        sprite.Color = new Color(75,75,75);
        frame.Add(sprite);

        // Draw cross
        DrawLine(frame, Vector2.Zero, textureSize, _crossWidth, new Color(50,50,50));
        DrawLine(frame, new Vector2(textureSize.X, 0), new Vector2(0, textureSize.Y), _crossWidth, new Color(50, 50, 50));

        // Draw corners
        sprite = MySprite.CreateSprite("SquareSimple", offset + _screenMarkerSize * 0.5f + new Vector2(0,0), _screenMarkerSize);
        sprite.Color = Color.Red;
        frame.Add(sprite);

        sprite = MySprite.CreateSprite("SquareSimple", offset + _screenMarkerSize * 0.5f + new Vector2(surfaceSize.X - _screenMarkerSize.X, 0), _screenMarkerSize);
        sprite.Color = Color.Green;
        frame.Add(sprite);

        sprite = MySprite.CreateSprite("SquareSimple", offset + _screenMarkerSize * 0.5f + new Vector2(0, surfaceSize.Y - _screenMarkerSize.Y), _screenMarkerSize);
        sprite.Color = Color.Cyan;
        frame.Add(sprite);

        sprite = MySprite.CreateSprite("SquareSimple", offset + _screenMarkerSize * 0.5f + new Vector2(surfaceSize.X - _screenMarkerSize.X, surfaceSize.Y - _screenMarkerSize.Y), _screenMarkerSize);
        sprite.Color = Color.Magenta;
        frame.Add(sprite);

        // Draw text
        sprite = MySprite.CreateText($"Texture Size: {textureSize}", "Monospace", Color.White, textSize);
        sprite.Position = center;
        frame.Add(sprite);

        sprite = MySprite.CreateText($"Surface Size: {surfaceSize}", "Monospace", Color.White, textSize);
        sprite.Position = center - new Vector2(0, 40) * textSize;
        frame.Add(sprite);
    }
}

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