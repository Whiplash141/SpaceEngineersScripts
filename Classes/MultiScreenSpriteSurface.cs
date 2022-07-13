MultiScreenSpriteSurface _multiScreen;
IMyTextPanel _anchor;

Program()
{
    _anchor = GridTerminalSystem.GetBlockWithName("Anchor") as IMyTextPanel;
    _multiScreen = new MultiScreenSpriteSurface(_anchor, 3, 3, this);

    DrawSprites(_multiScreen, _multiScreen.TextureSize * 0.5f, 3);

    _multiScreen.Draw();
}

public void DrawSprites(MultiScreenSpriteSurface frame, Vector2 centerPos, float scale = 1f)
{
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(450f, 0f) * scale + centerPos, new Vector2(50f, 1000f) * scale, new Color(150, 150, 0, 255), null, TextAlignment.CENTER, 0.7854f)); // stripe 7
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(300f, 0f) * scale + centerPos, new Vector2(50f, 1000f) * scale, new Color(150, 150, 0, 255), null, TextAlignment.CENTER, 0.7854f)); // stripe 6
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(150f, 0f) * scale + centerPos, new Vector2(50f, 1000f) * scale, new Color(150, 150, 0, 255), null, TextAlignment.CENTER, 0.7854f)); // stripe 5
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f, 0f) * scale + centerPos, new Vector2(50f, 1000f) * scale, new Color(150, 150, 0, 255), null, TextAlignment.CENTER, 0.7854f)); // stripe 4
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-150f, 0f) * scale + centerPos, new Vector2(50f, 1000f) * scale, new Color(150, 150, 0, 255), null, TextAlignment.CENTER, 0.7854f)); // stripe 3
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-450f, 0f) * scale + centerPos, new Vector2(50f, 1000f) * scale, new Color(150, 150, 0, 255), null, TextAlignment.CENTER, 0.7854f)); // stripe 2
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-300f, 0f) * scale + centerPos, new Vector2(50f, 1000f) * scale, new Color(150, 150, 0, 255), null, TextAlignment.CENTER, 0.7854f)); // stripe 1
    frame.Add(new MySprite(SpriteType.TEXT, "CAUTION", new Vector2(-100f, -35f) * scale + centerPos, null, new Color(255, 255, 255, 255), "Debug", TextAlignment.LEFT, 2f * scale)); // text
}

void Main()
{
    
}

public interface ISpriteSurface
{
    Vector2 TextureSize { get; }
    Vector2 SurfaceSize { get; }
    Color ScriptBackgroundColor { get; set; }
    int SpriteCount { get; }
    void Add(MySprite sprite);
    void Draw();
}

public class SingleScreenSpriteSurface : ISpriteSurface
{
    public bool IsValid
    {
        get
        {
            return Surface != null;
        }
    }

    public Vector2 TextureSize { get { return IsValid ? Surface.TextureSize : Vector2.Zero; } }
    public Vector2 SurfaceSize { get { return IsValid ? Surface.SurfaceSize : Vector2.Zero; } }
    public Color ScriptBackgroundColor
    {
        get { return Surface.ScriptBackgroundColor; }
        set { Surface.ScriptBackgroundColor = value; }
    }
    public int SpriteCount { get; private set; } = 0;

    public readonly IMyTextSurface Surface;
    public MySpriteDrawFrame? Frame = null;
    readonly List<MySprite> _sprites = new List<MySprite>(64);

    public void Add(MySprite sprite)
    {
        if (!IsValid)
        {
            return;
        }
        if (Frame == null)
        {
            Frame = Surface.DrawFrame();
        }
        Frame.Value.Add(sprite);
        SpriteCount++;
    }

    public void Draw()
    {
        Draw(Surface.ScriptBackgroundColor);
        SpriteCount = 0;
    }

    public void Draw(Color scriptBackgroundColor)
    {
        if (!IsValid)
        {
            return;
        }
        Surface.ContentType = ContentType.SCRIPT;
        Surface.Script = "";
        Surface.ScriptBackgroundColor = scriptBackgroundColor;
        if (Frame == null)
        {
            Surface.DrawFrame().Dispose();
        }
        else
        {
            Frame.Value.Dispose();
            Frame = null;
        }
    }

    public SingleScreenSpriteSurface(IMyTextSurface surf)
    {
        Surface = surf;
    }

    public SingleScreenSpriteSurface(IMyCubeGrid grid, Vector3I position)
    {
        var slim = grid.GetCubeBlock(position);
        if (slim != null && slim.FatBlock != null)
        {
            var surf = slim.FatBlock as IMyTextSurface;
            if (surf != null)
            {
                Surface = surf;
            }
        }
    }
}

// Assumes that all text panels are the same size
public class MultiScreenSpriteSurface : ISpriteSurface
{
    readonly SingleScreenSpriteSurface[,] _surfaces;

    public bool Initialized { get; private set; } = false;

    public Vector2 TextureSize { get; private set; }
    public Vector2 SurfaceSize
    {
        get { return TextureSize; }
    }
    public int SpriteCount { get; private set; } = 0;
    public readonly Vector2 BasePanelSize;
    public readonly int Rows;
    public readonly int Cols;

    public Color ScriptBackgroundColor { get; set; } = Color.Black;
    StringBuilder _stringBuilder = new StringBuilder(128);
    Program _p;
    IMyTextPanel _anchor;

    public MultiScreenSpriteSurface(IMyTextPanel anchor, int rows, int cols, Program p)
    {
        _anchor = anchor;
        _p = p;
        _surfaces = new SingleScreenSpriteSurface[rows, cols];
        BasePanelSize = anchor.TextureSize;
        TextureSize = anchor.TextureSize * new Vector2(cols, rows);
        Rows = rows;
        Cols = cols;

        Vector3I anchorPos = anchor.Position;
        Vector3I anchorRight = -Base6Directions.GetIntVector(anchor.Orientation.Left);
        Vector3I anchorDown = -Base6Directions.GetIntVector(anchor.Orientation.Up);
        Vector3I anchorBlockSize = anchor.Max - anchor.Min + Vector3I.One;
        Vector3I stepRight = Math.Abs(Vector3I.Dot(anchorBlockSize, anchorRight)) * anchorRight;
        Vector3I stepDown = Math.Abs(Vector3I.Dot(anchorBlockSize, anchorDown)) * anchorDown;
        IMyCubeGrid grid = anchor.CubeGrid;
        for (int r = 0; r < Rows; ++r)
        {
            for (int c = 0; c < Cols; ++c)
            {
                Vector3I blockPosition = anchorPos + r * stepDown + c * stepRight;
                _surfaces[r, c] = new SingleScreenSpriteSurface(grid, blockPosition);
                //_p.Echo($"({r},{c}): Pos {blockPosition} | Valid: {_surfaces[r, c].IsValid}");
            }
        }
    }

    public void Add(MySprite sprite)
    {
        //_p.Echo("---\nSprite");
        Vector2 pos = sprite.Position ?? TextureSize * 0.5f;
        Vector2 spriteSize;
        if (sprite.Size != null)
        {
            spriteSize = sprite.Size.Value;
        }
        else if (sprite.Type == SpriteType.TEXT)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(sprite.Data);
            spriteSize = _anchor.MeasureStringInPixels(_stringBuilder, sprite.FontId, sprite.RotationOrScale);
            //_p.Echo($"Text size:{spriteSize}\nScale:{sprite.RotationOrScale}\nFont:{sprite.FontId}\nData:{sprite.Data}");
        }
        else
        {
            spriteSize = TextureSize;
        }
        float rad = spriteSize.Length();

        var lowerCoords = Vector2I.Floor((pos - rad) / BasePanelSize);
        var upperCoords = Vector2I.Floor((pos + rad) / BasePanelSize);

        int lowerCol = Math.Max(0, lowerCoords.X);
        int upperCol = Math.Min(Cols - 1, upperCoords.X);

        int lowerRow = Math.Max(0, lowerCoords.Y);
        int upperRow = Math.Min(Rows - 1, upperCoords.Y);

        for (int r = lowerRow; r <= upperRow; ++r)
        {
            for (int c = lowerCol; c <= upperCol; ++c)
            {
                Vector2 adjustedPos = pos - BasePanelSize * new Vector2(c, r);
                //_p.Echo($"({r},{c}) {adjustedPos}");
                sprite.Position = adjustedPos;
                _surfaces[r, c].Add(sprite);
                SpriteCount++;
            }
        }
    }

    public void Draw()
    {
        for (int r = 0; r < Rows; ++r)
        {
            for (int c = 0; c < Cols; ++c)
            {
                _surfaces[r, c].Draw(ScriptBackgroundColor);
            }
        }
        SpriteCount = 0;
    }
}  