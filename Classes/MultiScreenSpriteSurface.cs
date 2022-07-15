/*	
/ //// / 	
DEMO INSTRUCTIONS	
    1. Add a 3x3 array of screens to your grid	
    2. Name the top left screen "Anchor"	
    3. Recompile this script	
/ //// /	
*/	


MultiScreenSpriteSurface _multiScreen;
IMyTextPanel _anchor;

Program()
{
    _anchor = GridTerminalSystem.GetBlockWithName("Anchor") as IMyTextPanel;
    _multiScreen = new MultiScreenSpriteSurface(_anchor, 3, 3, this);

    DrawSprites(_multiScreen, _multiScreen.TextureSize * 0.5f, 5);

    _multiScreen.Draw();
}

public void DrawSprites(ISpriteSurface frame, Vector2 centerPos, float scale = 1f)
{
    frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(0f,-80f)*scale+centerPos, new Vector2(100f,100f)*scale, new Color(128,0,0,255), null, TextAlignment.CENTER, 0f)); // head
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,-30f)*scale+centerPos, new Vector2(100f,100f)*scale, new Color(128,0,0,255), null, TextAlignment.CENTER, 0f)); // body
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(30f,40f)*scale+centerPos, new Vector2(40f,70f)*scale, new Color(128,0,0,255), null, TextAlignment.CENTER, 0f)); // leg r
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-30f,40f)*scale+centerPos, new Vector2(40f,70f)*scale, new Color(128,0,0,255), null, TextAlignment.CENTER, 0f)); // leg l
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-70f,-41f)*scale+centerPos, new Vector2(40f,80f)*scale, new Color(128,0,0,255), null, TextAlignment.CENTER, 0f)); // backpack
    frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(50f,-65f)*scale+centerPos, new Vector2(40f,40f)*scale, new Color(128,128,128,255), null, TextAlignment.CENTER, 0f)); // visor right
    frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(10f,-65f)*scale+centerPos, new Vector2(40f,40f)*scale, new Color(128,128,128,255), null, TextAlignment.CENTER, 0f)); // visor left
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(30f,-65f)*scale+centerPos, new Vector2(40f,40f)*scale, new Color(128,128,128,255), null, TextAlignment.CENTER, 0f)); // visor center
}

void Main()
{
    
}

#region Multi-screen Sprite Surface
public interface ISpriteSurface
{
    Vector2 TextureSize { get; }
    Vector2 SurfaceSize { get; }
    Color ScriptBackgroundColor { get; set; }
    int SpriteCount { get; }
    void Add(MySprite sprite);
    void Draw();
    Vector2 MeasureStringInPixels(StringBuilder text, string font, float scale);
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
        get { return IsValid ? Surface.ScriptBackgroundColor : Color.Black; }
        set { if (IsValid) { Surface.ScriptBackgroundColor = value; } }
    }
    public int SpriteCount { get; private set; } = 0;
    public Vector2 MeasureStringInPixels(StringBuilder text, string font, float scale)
    {
        return IsValid ? Surface.MeasureStringInPixels(text, font, scale) : Vector2.Zero;
    }

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
    public Vector2 MeasureStringInPixels(StringBuilder text, string font, float scale)
    {
        return _anchor.MeasureStringInPixels(text, font, scale);
    }
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
            }
        }
    }

    public void Add(MySprite sprite)
    {
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
#endregion
