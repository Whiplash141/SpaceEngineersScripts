//#exclude
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


    Runtime.UpdateFrequency = UpdateFrequency.Update100;
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
    _multiScreen.UpdateRotation();
    DrawSprites(_multiScreen, _multiScreen.TextureSize * 0.5f, 5);
    _multiScreen.Draw();
    Echo($"Last Update: {DateTime.Now}");
}
//#endexclude
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

    public readonly IMyCubeBlock CubeBlock;
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
            CubeBlock = slim.FatBlock;
            var surf = CubeBlock as IMyTextSurface;
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
    public bool Initialized { get; private set; } = false;

    float Rotation
    {
        get
        {
            return _rotationAngle.HasValue ? _rotationAngle.Value : 0f;
        }
        set
        {
            bool newAngle = !_rotationAngle.HasValue || _rotationAngle.Value != value;
            _rotationAngle = value;
            if (!newAngle)
            {
                return;
            }

            _spanVector = RotateToDisplayOrientation(new Vector2(Cols, Rows), RotationRads);
            _spanVector *= Vector2.SignNonZero(_spanVector);
            _textureSize = null;
            _basePanelSizeNoRotation = null;
            _textureSizeNoRotation = null;
            for (int r = 0; r < Rows; ++r)
            {
                for (int c = 0; c < Cols; ++c)
                {
                    UpdateSurfaceRotation(r, c);
                }
            }
        }
    }
    float RotationRads
    {
        get
        {
            return MathHelper.ToRadians(Rotation);
        }
    }
    public Vector2 TextureSize
    {
        get
        {
            if (!_textureSize.HasValue)
            {
                _textureSize = BasePanelSize * _spanVector;
            }
            return _textureSize.Value;
        }
    }
    public Vector2 SurfaceSize
    {
        get { return TextureSize; }
    }
    public int SpriteCount { get; private set; } = 0;
    public Vector2 MeasureStringInPixels(StringBuilder text, string font, float scale)
    {
        return _anchor.MeasureStringInPixels(text, font, scale);
    }
    Vector2 BasePanelSize
    {
        get { return _anchor.TextureSize; }
    }
    Vector2 BasePanelSizeNoRotation
    {
        get
        {
            if (!_basePanelSizeNoRotation.HasValue)
            {
                Vector2 size = RotateToBaseOrientation(BasePanelSize, RotationRads);
                size *= Vector2.SignNonZero(size);
                _basePanelSizeNoRotation = size;
            }
            return _basePanelSizeNoRotation.Value;
        }
    }
    Vector2 TextureSizeNoRotation
    {
        get
        {
            if (!_textureSizeNoRotation.HasValue)
            {
                _textureSizeNoRotation = BasePanelSizeNoRotation * new Vector2(Cols, Rows);
            }
            return _textureSizeNoRotation.Value;
        }
    }
    public readonly int Rows;
    public readonly int Cols;

    public Color ScriptBackgroundColor { get; set; } = Color.Black;
    StringBuilder _stringBuilder = new StringBuilder(128);
    Program _p;
    IMyTextPanel _anchor;
    ITerminalProperty<float> _rotationProp;
    float? _rotationAngle;
    Vector2? _textureSize;
    Vector2? _basePanelSizeNoRotation;
    Vector2? _textureSizeNoRotation;
    Vector2 _spanVector;

    readonly SingleScreenSpriteSurface[,] _surfaces;
    readonly Vector2[,] _screenOrigins;

    public MultiScreenSpriteSurface(IMyTextPanel anchor, int rows, int cols, Program p)
    {
        _anchor = anchor;
        _p = p;
        _surfaces = new SingleScreenSpriteSurface[rows, cols];
        _screenOrigins = new Vector2[rows, cols];
        Rows = rows;
        Cols = cols;

        _rotationProp = _anchor.GetProperty("Rotate").Cast<float>();

        Vector3I anchorPos = _anchor.Position;
        Vector3I anchorRight = -Base6Directions.GetIntVector(_anchor.Orientation.Left);
        Vector3I anchorDown = -Base6Directions.GetIntVector(_anchor.Orientation.Up);
        Vector3I anchorBlockSize = _anchor.Max - _anchor.Min + Vector3I.One;
        Vector3I stepRight = Math.Abs(Vector3I.Dot(anchorBlockSize, anchorRight)) * anchorRight;
        Vector3I stepDown = Math.Abs(Vector3I.Dot(anchorBlockSize, anchorDown)) * anchorDown;
        IMyCubeGrid grid = _anchor.CubeGrid;
        for (int r = 0; r < Rows; ++r)
        {
            for (int c = 0; c < Cols; ++c)
            {
                Vector3I blockPosition = anchorPos + r * stepDown + c * stepRight;
                var surf = new SingleScreenSpriteSurface(grid, blockPosition);
                _surfaces[r, c] = surf;
            }
        }

        UpdateRotation();
    }

    public void UpdateRotation()
    {
        Rotation = _rotationProp.GetValue(_anchor);
    }

    void UpdateSurfaceRotation(int r, int c)
    {
        SingleScreenSpriteSurface surf = _surfaces[r, c];
        if (surf.CubeBlock != null)
        {
            _rotationProp.SetValue(surf.CubeBlock, Rotation);
        }

        // Calc screen coords
        Vector2 screenCenter = BasePanelSizeNoRotation * new Vector2(c + 0.5f, r + 0.5f);
        Vector2 fromCenter = screenCenter - 0.5f * TextureSizeNoRotation;
        Vector2 fromCenterRotated = RotateToDisplayOrientation(fromCenter, RotationRads);
        Vector2 screenCenterRotated = fromCenterRotated + 0.5f * TextureSize;
        _screenOrigins[r, c] = screenCenterRotated - 0.5f * BasePanelSize;
    }

    Vector2 RotateToDisplayOrientation(Vector2 vec, float angleRad)
    {
        int caseIdx = (int)Math.Round(angleRad / MathHelper.ToRadians(90));
        switch (caseIdx)
        {
            default:
            case 0:
                return vec;
            case 1: // 90 deg
                return new Vector2(vec.Y, -vec.X);
            case 2: // 180 deg
                return -vec;
            case 3: // 270 deg
                return new Vector2(-vec.Y, vec.X);
        }
    }

    Vector2 RotateToBaseOrientation(Vector2 vec, float angleRad)
    {
        int caseIdx = (int)Math.Round(angleRad / MathHelper.ToRadians(90));
        switch (caseIdx)
        {
            default:
            case 0:
                return vec;
            case 1: // 90 deg
                return new Vector2(-vec.Y, vec.X);
            case 2: // 180 deg
                return -vec;
            case 3: // 270 deg
                return new Vector2(vec.Y, -vec.X);
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
        float rad = spriteSize.Length() * 0.5f;


        Vector2 fromCenter = pos - (TextureSize * 0.5f);
        Vector2 fromCenterRotated = RotateToBaseOrientation(fromCenter, RotationRads);
        Vector2 basePos = TextureSizeNoRotation * 0.5f + fromCenterRotated;

        var lowerCoords = Vector2I.Floor((basePos - rad) / BasePanelSizeNoRotation);
        var upperCoords = Vector2I.Floor((basePos + rad) / BasePanelSizeNoRotation);

        int lowerCol = Math.Max(0, lowerCoords.X);
        int upperCol = Math.Min(Cols - 1, upperCoords.X);

        int lowerRow = Math.Max(0, lowerCoords.Y);
        int upperRow = Math.Min(Rows - 1, upperCoords.Y);

        for (int r = lowerRow; r <= upperRow; ++r)
        {
            for (int c = lowerCol; c <= upperCol; ++c)
            {
                sprite.Position = pos - _screenOrigins[r, c];
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
