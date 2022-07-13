
/*
/ //// / Whip's Ship Integrity Monitoring Program (Lite) / //// /






















============================================
    DO NOT EDIT VARIABLES IN THE SCRIPT
           USE THE CUSTOM DATA!
============================================
*/

const string VERSION = "1.4.3";
const string DATE = "2022/07/11";

// Configurable
string _textSurfaceGroupName = "SIMPL";
float _textSize = 0.5f;
Color _maxDensityColor = new Color(50, 50, 50);
Color _minDensityColor = new Color(10, 10, 10);
Color _missingColor = new Color(100, 0, 0, 200);
Color _bgColor = new Color(0, 0, 0);
Color _powerColor = new Color(0, 100, 0, 100);
Color _weaponColor = new Color(100, 50, 0, 100);
Color _gyroColor = new Color(100, 100, 0, 100);
Color _thrustColor = new Color(0, 0, 100, 100);
bool _autoscan = true;

const NormalAxis DEFAULT_NORMAL_AXIS = NormalAxis.X;
const float DEFAULT_ROTATION = 0f;
const bool DEFAULT_AUTOSCALE = true;
const float DEFAULT_SCALE = 10f;
const bool DEFAULT_INVERT = false;
const float DEFAULT_LEGEND_SCALE = 1f;

const int BLOCKS_TO_STORE_PER_TICK = 500;
const int BLOCKS_TO_CHECK_PER_TICK = 100;
const int SPRITES_TO_CREATE_PER_TICK = 500;
const int DISCRETE_DENSITY_STEPS = 4;

const string INI_SECTION_NAME = "SIMPL - General Config";
const string INI_KEY_GROUP_NAME = "Group name";
const string INI_KEY_AUTO_SCAN = "Auto scan";

const string INI_SECTION_COLOR = "SIMPL - Colors";
const string INI_COMMENT_COLOR = " Colors are in the format: Red, Green, Blue, Alpha.\n If you do not want to see a particular subsystem color\n simply set the Alpha value to 0 and it will be\n omitted from the legend.";
const string INI_KEY_COLOR_MAX = "Max block density";
const string INI_KEY_COLOR_MIN = "Min block density";
const string INI_KEY_COLOR_MISSING = "Missing block";
const string INI_KEY_COLOR_POWER = "Power";
const string INI_KEY_COLOR_WEAPON = "Weapons";
const string INI_KEY_COLOR_GYRO = "Gyro";
const string INI_KEY_COLOR_THRUST = "Thrust";
const string INI_KEY_COLOR_BG = "Background";

const string INI_SECTION_TEXT_CONFIG = "SIMPL - Display Config";
const string INI_KEY_NORMAL = "Normal axis";
const string INI_KEY_ROTATION = "Rotation (deg)";
const string INI_KEY_AUTOSCALE = "Autoscale layout";
const string INI_KEY_SCALE = "Manual layout scale";
const string INI_KEY_INVERT = "Flip horizontally";
const string INI_KEY_LEGEND_SCALE = "Legend Scale";

const string INI_SECTION_TEXT_SURF = "SIMPL - Text Surface Config";
const string INI_KEY_TEXT_SURF_TEMPLATE = "Show on screen {0}";

const string INI_COMMENT_NORMAL = " Normal axis values: X, Y, or Z";

const string INI_SECTION_MULTISCREEN = "SIMPL - Multiscreen Config";
const string INI_KEY_MULTISCREEN_ROWS = "Screen rows";
const string INI_KEY_MULTISCREEN_COLS = "Screen cols";

List<TextSurfaceConfig> _textSurfaces = new List<TextSurfaceConfig>();
List<BlockInfo> _blockInfoArray = new List<BlockInfo>();
PlanarMap _planarMap;
Scheduler _scheduler;
RuntimeTracker _runtimeTracker;
Legend _legend;
LoadingScreen _loadingScreen = new LoadingScreen($"Loading SIMPL (v{VERSION})", "");
MyIni _ini = new MyIni();
StringBuilder _echoBuilder = new StringBuilder(512),
              _textMeasureBuilder = new StringBuilder(256);

IEnumerator<float> _blockStorageStateMachine = null;
IEnumerator<int> _blockCheckStateMachine = null;
IEnumerator<float> _spriteDrawStateMachine = null;

ScheduledAction _forceDrawTimeout;
bool _drawRefreshSprite = false;
bool _allowForceDraw = true;

string _storageStageStr = "";

bool _spriteDrawRunning = false;
bool _blockInfoStored = false;
bool _blockCheckRunning = false;
int _blocksUpdated = 0;

int _spritesX = 0;
int _spritesY = 0;
int _spritesZ = 0;

public enum NormalAxis { X, Y, Z }
public enum BlockMask { None, Power = 1, Gyro = 2, Thrust = 4, Weapon = 8 }

Program()
{
    InitializeGridBlockStorage();

    _runtimeTracker = new RuntimeTracker(this, 600);
    _scheduler = new Scheduler(this);
    _scheduler.AddScheduledAction(TryStartSpriteDraw, 0.5);
    _scheduler.AddScheduledAction(HandleStateMachines, 60);
    _scheduler.AddScheduledAction(WriteDetailedInfo, 1);

    _forceDrawTimeout = new ScheduledAction(() => _allowForceDraw = true, 1.0 / 30.0, true);

    _planarMap = new PlanarMap(Me.CubeGrid, _minDensityColor, _maxDensityColor, _missingColor, _powerColor, _weaponColor, _gyroColor, _thrustColor);

    _legend = new Legend(_textSize);
    _legend.AddLegendItem(INI_KEY_COLOR_MISSING, "Damage", ref _missingColor);
    _legend.AddLegendItem(INI_KEY_COLOR_WEAPON, "Weapons", ref _weaponColor);
    _legend.AddLegendItem(INI_KEY_COLOR_POWER, "Power", ref _powerColor);
    _legend.AddLegendItem(INI_KEY_COLOR_GYRO, "Gyros", ref _gyroColor);
    _legend.AddLegendItem(INI_KEY_COLOR_THRUST, "Thrust", ref _thrustColor);

    GetScreens();

    Runtime.UpdateFrequency |= UpdateFrequency.Update10;
}

new public void Echo(string text)
{
    _echoBuilder.Append(text).Append("\n");
}

void WriteDetailedInfo()
{
    Echo($"SIMPL Running...\n(Version {VERSION} - {DATE})");
    Echo("\nRun the argument \"refresh\" to refetch\n  screens and parse custom data.\n");
    Echo("Run the argument \"force_draw\" if the\n  screens don't draw properly in\n  multiplayer. Use this SPARINGLY!\n");
    Echo($"Force draw timeout: {(_allowForceDraw ? 0 : _forceDrawTimeout.RunInterval - _forceDrawTimeout.TimeSinceLastRun):n0} seconds\n");
    Echo($"Screens: {_textSurfaces.Count}");
    Echo($"\nTotal Sprites Drawn:\n  X: {_spritesX}\n  Y: {_spritesY}\n  Z: {_spritesZ}\n");

    Echo(_runtimeTracker.Write());

    if (!_blockInfoStored)
    {
        Echo($"Block storage progress: {_blockStorageStateMachine.Current:n0}%");
    }
    else
    {
        Echo($"QuadTree Compression Ratio:");
        Echo($"  X: {(float)_planarMap.QuadTreeXNormal.UncompressedNodeCount / _planarMap.QuadTreeXNormal.FinishedNodes.Count:0.000} (count: {_planarMap.QuadTreeXNormal.FinishedNodes.Count})");
        Echo($"  Y: {(float)_planarMap.QuadTreeYNormal.UncompressedNodeCount / _planarMap.QuadTreeYNormal.FinishedNodes.Count:0.000} (count: {_planarMap.QuadTreeYNormal.FinishedNodes.Count})");
        Echo($"  Z: {(float)_planarMap.QuadTreeZNormal.UncompressedNodeCount / _planarMap.QuadTreeZNormal.FinishedNodes.Count:0.000} (count: {_planarMap.QuadTreeZNormal.FinishedNodes.Count})\n");
        if (_blockCheckStateMachine != null)
        {
            Echo($"Block check status: {100f * _blockCheckStateMachine.Current / _blockInfoArray.Count:n0}%\n    ({_blockCheckStateMachine.Current}/{_blockInfoArray.Count})");
        }
    }

    if (_spriteDrawStateMachine != null)
    {
        Echo($"Sprite draw progress: {_spriteDrawStateMachine.Current:n0}%");
    }

    string output = _echoBuilder.ToString();
    base.Echo(output);
    _echoBuilder.Clear();
}

void Main(string arg, UpdateType updateSource)
{
    _runtimeTracker.AddRuntime();

    switch (arg.ToUpperInvariant())
    {
        case "SETUP":
        case "REFRESH":
            GetScreens();
            break;

        case "FORCE_DRAW":
            if (_allowForceDraw)
            {
                _drawRefreshSprite = !_drawRefreshSprite;
                _allowForceDraw = false;
                _scheduler.AddScheduledAction(_forceDrawTimeout);
            }
            break;

        case "SCAN":
            if (!_autoscan)
            {
                TryStartBlockCheck(true);
            }
            break;
    }

    _scheduler.Update();
    _runtimeTracker.AddInstructions();
}

#region Block Fetching
struct TextSurfaceConfig
{
    public readonly ISpriteSurface Surface;
    public readonly NormalAxis Normal;
    public readonly float RotationRad;
    public readonly bool Autoscale;
    public readonly float Scale;
    public readonly float LegendScale;
    public readonly bool Invert;

    public TextSurfaceConfig(ISpriteSurface surface, NormalAxis normal, float rotationDeg, float scale, float legendScale, bool invert, bool autoscale)
    {
        Surface = surface;
        Normal = normal;
        RotationRad = MathHelper.ToRadians(rotationDeg);
        Scale = scale;
        LegendScale = legendScale;
        Invert = invert;
        Autoscale = autoscale;
    }
}

void GetSurfaceConfigValues(bool parsed, IMyTerminalBlock b, bool hasMulltipleScreens, int surfaceIdx, out NormalAxis normal, out float rotation, out float scale, out float legendScale, out bool invert, out bool autoscale, out bool multiscreen, out int rows, out int cols)
{
    normal = DEFAULT_NORMAL_AXIS;
    rotation = DEFAULT_ROTATION;
    scale = DEFAULT_SCALE;
    legendScale = DEFAULT_LEGEND_SCALE;
    invert = DEFAULT_INVERT;
    autoscale = DEFAULT_AUTOSCALE;
    multiscreen = false;
    rows = 1;
    cols = 1;


    string sectionName = INI_SECTION_TEXT_CONFIG;
    if (hasMulltipleScreens)
    {
        sectionName = string.Format("{0} - Screen {1}", INI_SECTION_TEXT_CONFIG, surfaceIdx);
    }
    else if (_ini.ContainsSection(INI_SECTION_MULTISCREEN))
    {
        multiscreen = true;
        rows = _ini.Get(INI_SECTION_MULTISCREEN, INI_KEY_MULTISCREEN_ROWS).ToInt32(rows);
        cols = _ini.Get(INI_SECTION_MULTISCREEN, INI_KEY_MULTISCREEN_COLS).ToInt32(cols);
        rows = Math.Max(rows, 1);
        cols = Math.Max(cols, 1);
        _ini.Set(INI_SECTION_MULTISCREEN, INI_KEY_MULTISCREEN_ROWS, rows);
        _ini.Set(INI_SECTION_MULTISCREEN, INI_KEY_MULTISCREEN_COLS, cols);
    }

    if (parsed)
    {
        string normalAxisStr = _ini.Get(sectionName, INI_KEY_NORMAL).ToString();
        if (!Enum.TryParse(normalAxisStr, true, out normal))
        {
            normal = DEFAULT_NORMAL_AXIS;
        }
        rotation = _ini.Get(sectionName, INI_KEY_ROTATION).ToSingle(rotation);
        autoscale = _ini.Get(sectionName, INI_KEY_AUTOSCALE).ToBoolean(autoscale);
        scale = _ini.Get(sectionName, INI_KEY_SCALE).ToSingle(scale);
        legendScale = _ini.Get(sectionName, INI_KEY_LEGEND_SCALE).ToSingle(legendScale);
        invert = _ini.Get(sectionName, INI_KEY_INVERT).ToBoolean(invert);
    }

    _ini.Set(sectionName, INI_KEY_NORMAL, normal.ToString());
    _ini.Set(sectionName, INI_KEY_ROTATION, rotation);
    _ini.Set(sectionName, INI_KEY_AUTOSCALE, autoscale);
    _ini.Set(sectionName, INI_KEY_SCALE, scale);
    _ini.Set(sectionName, INI_KEY_LEGEND_SCALE, legendScale);
    _ini.Set(sectionName, INI_KEY_INVERT, invert);

    _ini.SetComment(sectionName, INI_KEY_NORMAL, INI_COMMENT_NORMAL);
}

bool CollectScreens(IMyTerminalBlock b)
{
    if (!b.IsSameConstructAs(Me))
        return false;

    NormalAxis normal;
    float rotation;
    float scale;
    float legendScale;
    bool invert;
    bool autoscale;
    bool parsed = false;
    bool multiscreen;
    int rows;
    int cols;


    if (b is IMyTextPanel)
    {
        _ini.Clear();
        parsed = _ini.TryParse(b.CustomData);

        GetSurfaceConfigValues(parsed, b, false, 0, out normal, out rotation, out scale, out legendScale, out invert, out autoscale, out multiscreen, out rows, out cols);
        ISpriteSurface surf;
        var tp = (IMyTextPanel)b;
        if (multiscreen && (rows > 1 || cols > 1))
        {
            surf = new MultiScreenSpriteSurface(tp, rows, cols, this);
        }
        else
        {
            surf = new SingleScreenSpriteSurface(tp);
        }

        _textSurfaces.Add(new TextSurfaceConfig(surf, normal, rotation, scale, legendScale, invert, autoscale));

        if (!parsed && !string.IsNullOrWhiteSpace(b.CustomData))
        {
            _ini.EndContent = b.CustomData;
        }

        string output = _ini.ToString();
        if (!string.Equals(output, b.CustomData))
            b.CustomData = output;

        return false;
    }

    if (b is IMyTextSurfaceProvider)
    {
        _ini.Clear();
        parsed = _ini.TryParse(b.CustomData);

        var tsp = (IMyTextSurfaceProvider)b;

        int surfaceCount = tsp.SurfaceCount;
        for (int i = 0; i < surfaceCount; ++i)
        {
            string iniKey = string.Format(INI_KEY_TEXT_SURF_TEMPLATE, i);
            bool display = _ini.Get(INI_SECTION_TEXT_SURF, iniKey).ToBoolean(i == 0);
            _ini.Set(INI_SECTION_TEXT_SURF, iniKey, display);

            if (display)
            {
                GetSurfaceConfigValues(parsed, b, true, i, out normal, out rotation, out scale, out legendScale, out invert, out autoscale, out multiscreen, out rows, out cols);
                var surf = new SingleScreenSpriteSurface(tsp.GetSurface(i));
                _textSurfaces.Add(new TextSurfaceConfig(surf, normal, rotation, scale, legendScale, invert, autoscale));
            }
        }

        string output = _ini.ToString();
        if (!string.Equals(output, b.CustomData))
            b.CustomData = output;

        return false;
    }

    return false;
}

void ParseGeneralConfig()
{
    _ini.Clear();
    bool parsed = _ini.TryParse(Me.CustomData);
    if (parsed)
    {
        _textSurfaceGroupName = _ini.Get(INI_SECTION_NAME, INI_KEY_GROUP_NAME).ToString(_textSurfaceGroupName);
        _autoscan = _ini.Get(INI_SECTION_NAME, INI_KEY_AUTO_SCAN).ToBoolean(_autoscan);
        _maxDensityColor = MyIniHelper.GetColor(INI_SECTION_COLOR, INI_KEY_COLOR_MAX, _ini, _maxDensityColor);
        _minDensityColor = MyIniHelper.GetColor(INI_SECTION_COLOR, INI_KEY_COLOR_MIN, _ini, _minDensityColor);
        _missingColor = MyIniHelper.GetColor(INI_SECTION_COLOR, INI_KEY_COLOR_MISSING, _ini, _missingColor);
        _bgColor = MyIniHelper.GetColor(INI_SECTION_COLOR, INI_KEY_COLOR_BG, _ini, _bgColor);

        _weaponColor = MyIniHelper.GetColor(INI_SECTION_COLOR, INI_KEY_COLOR_WEAPON, _ini, _weaponColor);
        _powerColor = MyIniHelper.GetColor(INI_SECTION_COLOR, INI_KEY_COLOR_POWER, _ini, _powerColor);
        _gyroColor = MyIniHelper.GetColor(INI_SECTION_COLOR, INI_KEY_COLOR_GYRO, _ini, _gyroColor);
        _thrustColor = MyIniHelper.GetColor(INI_SECTION_COLOR, INI_KEY_COLOR_THRUST, _ini, _thrustColor);

        _planarMap.UpdateColors(ref _minDensityColor, ref _maxDensityColor, ref _missingColor, ref _powerColor, ref _weaponColor, ref _gyroColor, ref _thrustColor);
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    _ini.Set(INI_SECTION_NAME, INI_KEY_GROUP_NAME, _textSurfaceGroupName);
    _ini.Set(INI_SECTION_NAME, INI_KEY_AUTO_SCAN, _autoscan);
    MyIniHelper.SetColor(INI_SECTION_COLOR, INI_KEY_COLOR_MAX, _maxDensityColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLOR, INI_KEY_COLOR_MIN, _minDensityColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLOR, INI_KEY_COLOR_MISSING, _missingColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLOR, INI_KEY_COLOR_BG, _bgColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLOR, INI_KEY_COLOR_WEAPON, _weaponColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLOR, INI_KEY_COLOR_POWER, _powerColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLOR, INI_KEY_COLOR_GYRO, _gyroColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLOR, INI_KEY_COLOR_THRUST, _thrustColor, _ini);
    _ini.SetSectionComment(INI_SECTION_COLOR, INI_COMMENT_COLOR);

    _legend.UpdateLegendItemColor(INI_KEY_COLOR_MISSING, ref _missingColor);
    _legend.UpdateLegendItemColor(INI_KEY_COLOR_WEAPON, ref _weaponColor);
    _legend.UpdateLegendItemColor(INI_KEY_COLOR_POWER, ref _powerColor);
    _legend.UpdateLegendItemColor(INI_KEY_COLOR_GYRO, ref _gyroColor);
    _legend.UpdateLegendItemColor(INI_KEY_COLOR_THRUST, ref _thrustColor);

    string output = _ini.ToString();

    if (!string.Equals(output, Me.CustomData))
    {
        Me.CustomData = output;
    }
}

void GetScreens()
{
    _textSurfaces.Clear();
    ParseGeneralConfig();

    var group = GridTerminalSystem.GetBlockGroupWithName(_textSurfaceGroupName);
    if (group == null)
        return;

    group.GetBlocks(null, CollectScreens);
}
#endregion

#region Functions 
public static Matrix CreateRotMatrix(float rotation)
{
    float sin = MyMath.FastSin(rotation);
    float cos = MyMath.FastCos(rotation);
    return new Matrix
    {
        M11 = cos,
        M12 = sin,
        M21 = -sin,
        M22 = cos,
    };
}

public static int GetPositionIndex(ref Vector3I diff, ref Vector3I span)
{
    return diff.X
         + diff.Y * (span.X + 1)
         + diff.Z * ((span.X + 1) * (span.Y + 1));
}
#endregion

#region State Machines
void HandleStateMachines()
{
    if (!_blockInfoStored) // Run the storage state machine
    {
        StoreGridBlocks();
    }
    else
    {
        TryStartBlockCheck(false);
    }

    if (_spriteDrawRunning)
    {
        UpdateSpriteDrawStateMachine();
    }
}

#region Block Storage
public IEnumerator<float> GridSpaceStorageIterator()
{
    Vector3I start = Me.CubeGrid.Min;
    Vector3I end = Me.CubeGrid.Max;

    int count = (end.X - start.X + 1) * (end.Y - start.Y + 1) * (end.Z - start.Z + 1);
    _blockInfoArray = new List<BlockInfo>(count);

    int index = 0;
    Vector3I_RangeIterator rangeIter = new Vector3I_RangeIterator(ref start, ref end);
    _storageStageStr = "Storing blocks...";

    while (rangeIter.IsValid())
    {
        if (Me.CubeGrid.CubeExists(rangeIter.Current))
        {
            BlockInfo blockInfo = new BlockInfo(ref rangeIter.Current, Me.CubeGrid);
            _blockInfoArray.Add(blockInfo);
            _planarMap.StoreBlockInfo(blockInfo);
        }

        index++;
        rangeIter.MoveNext();

        if (index % BLOCKS_TO_STORE_PER_TICK == 0)
        {
            yield return 90f * index / count;
        }
    }

    _planarMap.CreateQuadTrees();
    yield return 90f;

    _storageStageStr = "Processing X-axis Quad Tree...";
    while (!_planarMap.QuadTreeXNormal.Finished)
    {
        _planarMap.QuadTreeXNormal.Subdivide();
        if (Runtime.CurrentInstructionCount > 5000)
            yield return 90f + 3.333f * _planarMap.QuadTreeXNormal.ProcessedNodeCount / _planarMap.QuadTreeXNormal.TotalNodeCount;
    }

    _storageStageStr = "Processing Y-axis Quad Tree...";
    while (!_planarMap.QuadTreeYNormal.Finished)
    {
        _planarMap.QuadTreeYNormal.Subdivide();
        if (Runtime.CurrentInstructionCount > 5000)
            yield return 93.333f + 3.333f * _planarMap.QuadTreeYNormal.ProcessedNodeCount / _planarMap.QuadTreeYNormal.TotalNodeCount;
    }

    _storageStageStr = "Processing Z-axis Quad Tree...";
    while (!_planarMap.QuadTreeZNormal.Finished)
    {
        _planarMap.QuadTreeZNormal.Subdivide();
        if (Runtime.CurrentInstructionCount > 5000)
            yield return 96.666f + 3.333f * _planarMap.QuadTreeZNormal.ProcessedNodeCount / _planarMap.QuadTreeZNormal.TotalNodeCount;
    }

    yield return 100f;
}

void InitializeGridBlockStorage()
{
    if (_blockStorageStateMachine != null)
    {
        _blockStorageStateMachine.Dispose();
        _blockStorageStateMachine = null;
    }
    _blockStorageStateMachine = GridSpaceStorageIterator();
}

void StoreGridBlocks()
{
    bool moreInstructions = _blockStorageStateMachine.MoveNext();
    if (moreInstructions) // More work to do
    {
        return;
    }

    _blockStorageStateMachine.Dispose();
    _blockStorageStateMachine = null;
    _blockInfoStored = true;
}
#endregion

#region Block Checking
void TryStartBlockCheck(bool commanded)
{
    if (!_blockCheckRunning)
    {
        // Restart block checking
        if (commanded || _autoscan)
        {
            InitializeGridBlockChecking();
        }
    }
    else
    {
        CheckGridBlocks();
    }
}


public IEnumerator<int> GridSpaceCheckingIterator()
{
    for (int ii = 0; ii < _blockInfoArray.Count; ++ii)
    {
        BlockInfo blockInfo = _blockInfoArray[ii];

        bool wasMissing = blockInfo.IsMissing;
        blockInfo.IsMissing = !Me.CubeGrid.CubeExists(blockInfo.GridPosition);

        if (wasMissing != blockInfo.IsMissing)
        {
            _planarMap.UpdateForDamage(blockInfo);
        }

        if (++_blocksUpdated > BLOCKS_TO_CHECK_PER_TICK)
        {
            yield return ii;
        }
    }
    yield return _blockInfoArray.Count;
}

void InitializeGridBlockChecking()
{
    if (_blockCheckStateMachine != null)
    {
        _blockCheckStateMachine.Dispose();
        _blockCheckStateMachine = null;
    }
    _blockCheckStateMachine = GridSpaceCheckingIterator();

    _blockCheckRunning = true;
}

void CheckGridBlocks()
{
    _blocksUpdated = 0;
    bool moreInstructions = _blockCheckStateMachine.MoveNext();
    if (moreInstructions) // More work to do
    {
        return;
    }

    _blockCheckStateMachine.Dispose();
    _blockCheckStateMachine = null;
    _blockCheckRunning = false;
}
#endregion

#region Sprite Draw
void TryStartSpriteDraw()
{
    if (_spriteDrawRunning)
        return;

    _spriteDrawRunning = true;
    if (_spriteDrawStateMachine != null)
        _spriteDrawStateMachine.Dispose();
    _spriteDrawStateMachine = SpriteDrawStateMachine();
}

void UpdateSpriteDrawStateMachine()
{
    bool moreInstructions = _spriteDrawStateMachine.MoveNext();
    if (moreInstructions)
    {
        return;
    }

    _spriteDrawStateMachine.Dispose();
    _spriteDrawStateMachine = null;
    _spriteDrawRunning = false;
}

public IEnumerator<float> SpriteDrawStateMachine()
{
    _spritesX = _spritesY = _spritesZ = 0;
    for (int jj = 0; jj < _textSurfaces.Count; ++jj)
    {
        TextSurfaceConfig config = _textSurfaces[jj];
        ISpriteSurface surf = config.Surface;
        NormalAxis normal = config.Normal;
        float rotation = config.RotationRad;
        float scale = config.Scale;
        float legendScale = config.LegendScale;
        bool invert = config.Invert;
        bool autoscale = config.Autoscale;
        surf.ScriptBackgroundColor = _bgColor;

        Vector2 screenCenter = surf.TextureSize * 0.5f;
        Matrix rotationMatrix = CreateRotMatrix(rotation);

        if (!_blockInfoStored)
        {
            _loadingScreen.Draw(surf, _blockStorageStateMachine.Current * 0.01f, $"{_storageStageStr} ({_blockStorageStateMachine.Current:n0}%)");
            yield return 100f * jj / _textSurfaces.Count;
            continue;
        }

        // Adding or removing this sprite will force an entire resync of the sprite cache
        if (_drawRefreshSprite)
        {
            surf.Add(new MySprite());
        }

        List<BlockStatusSpriteCreator> statusSpriteCreators = null;
        QuadTree quadTree = null;
        switch (normal)
        {
            case NormalAxis.X:
                statusSpriteCreators = _planarMap.StatusSpriteCreatorsX;
                quadTree = _planarMap.QuadTreeXNormal;
                break;
            case NormalAxis.Y:
                statusSpriteCreators = _planarMap.StatusSpriteCreatorsY;
                quadTree = _planarMap.QuadTreeYNormal;
                break;
            case NormalAxis.Z:
                statusSpriteCreators = _planarMap.StatusSpriteCreatorsZ;
                quadTree = _planarMap.QuadTreeZNormal;
                break;
        }

        if (autoscale)
        {
            float x = (float)quadTree.MaxRows;
            float y = (float)quadTree.MaxColumns;
            float cos = Math.Abs(rotationMatrix.M11);
            float sin = Math.Abs(rotationMatrix.M12);

            float width = x * cos + y * sin;
            float height = x * sin + y * cos;
            Vector2 baseSize = new Vector2(width, height);
            Vector2 scaleVec = surf.SurfaceSize / baseSize;
            scale = (float)Math.Floor(Math.Min(scaleVec.X, scaleVec.Y));
        }

        for (int ii = 0; ii < quadTree.FinishedNodes.Count; ++ii)
        {
            var leaf = quadTree.FinishedNodes[ii];
            quadTree.AddSpriteFromQuadTreeLeaf(surf, normal, invert, scale, rotation, _planarMap, leaf, ref screenCenter, ref rotationMatrix);

            if ((ii + 1) % SPRITES_TO_CREATE_PER_TICK == 0)
            {
                yield return 100f * (jj + (float)(ii + 1) / (quadTree.FinishedNodes.Count + statusSpriteCreators.Count)) / _textSurfaces.Count;
            }
        }

        for (int ii = 0; ii < statusSpriteCreators.Count; ++ii)
        {
            statusSpriteCreators[ii].CreateSprite(surf, normal, scale, rotation, invert, ref screenCenter, ref rotationMatrix);

            if ((ii + 1) % SPRITES_TO_CREATE_PER_TICK == 0)
            {
                yield return 100f * (jj + (float)(ii + 1 + quadTree.FinishedNodes.Count) / (quadTree.FinishedNodes.Count + statusSpriteCreators.Count)) / _textSurfaces.Count;
            }
        }

        _legend.GenerateSprites(surf, legendScale);

        switch (normal)
        {
            case NormalAxis.X:
                _spritesX += (1 + surf.SpriteCount);
                break;
            case NormalAxis.Y:
                _spritesY += (1 + surf.SpriteCount);
                break;
            case NormalAxis.Z:
                _spritesZ += (1 + surf.SpriteCount);
                break;
        }

        surf.Draw();

        // Only one screen per tick
        yield return 100f * (jj + 1) / _textSurfaces.Count;
    }

    yield return 100f;
}
#endregion
#endregion

#region Classes and Structs
public class LoadingScreen
{
    readonly string _title;
    string _subtitle;

    const float TitleSize = 1.5f;
    const float SubtitleSize = 1f;

    readonly Vector2 LoadingBarSize = new Vector2(384, 32);
    readonly Vector2 TitleLocation = new Vector2(0, -80);
    readonly Vector2 SubtitleLocation = new Vector2(0, -35);
    readonly Vector2 LoadingBarLocation = new Vector2(0, 25);
    readonly Color TextColor = new Color(100, 100, 100);
    readonly Color LoadingBarColor = new Color(100, 100, 100);
    readonly Color LoadingBarBackgroundColor = new Color(10, 10, 10);
    readonly Color BackgroundColor = new Color(0, 0, 0);

    public LoadingScreen(string title, string subtitle)
    {
        _title = title;
        _subtitle = subtitle;
    }

    public void Draw(ISpriteSurface surf, float progress, string subtitle)
    {
        _subtitle = subtitle;
        Draw(surf, progress);
    }

    public void Draw(ISpriteSurface surf, float progress)
    {
        Vector2 screenCenter = surf.TextureSize * 0.5f;
        Vector2 scaleVec = surf.TextureSize / 512f;
        float scale = Math.Min(scaleVec.X, scaleVec.Y);

        // Background
        MySprite background = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: BackgroundColor);
        surf.Add(background);

        // Title
        MySprite title = MySprite.CreateText(_title, "Debug", TextColor, TitleSize * scale, TextAlignment.CENTER);
        title.Position = screenCenter + TitleLocation * scale;
        surf.Add(title);

        // Subtitle
        MySprite subtitle = MySprite.CreateText(_subtitle, "Debug", TextColor, SubtitleSize * scale, TextAlignment.CENTER);
        subtitle.Position = screenCenter + SubtitleLocation * scale;
        surf.Add(subtitle);

        // Status bar background
        Vector2 loadingBarSize = scale * LoadingBarSize;
        MySprite barBackground = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: LoadingBarBackgroundColor, size: loadingBarSize);
        barBackground.Position = screenCenter + LoadingBarLocation * scale;
        surf.Add(barBackground);

        // Status bar
        Vector2 statusBarSize = loadingBarSize * new Vector2(progress, 1f);
        MySprite bar = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: LoadingBarColor, size: statusBarSize);
        bar.Position = screenCenter + LoadingBarLocation * scale + new Vector2(-0.5f * (loadingBarSize.X - statusBarSize.X), 0);
        surf.Add(bar);

        surf.Draw();
    }
}

public class QuadTreeLeaf
{
    static int TEMP_count;
    int TEMP_idx;
    public readonly Vector2I Min;
    public readonly Vector2I Max;
    public int Value; // -2 = not evaluated, -1 = mixed, 0 = empty
    readonly public Vector2I Span;
    QuadTree _quadTreePtr;

    public QuadTreeLeaf(Vector2I min, Vector2I max, QuadTree quadTreePtr)
    {
        Min = min;
        Max = max;
        Span = max - min + Vector2I.One;
        Value = -2;
        _quadTreePtr = quadTreePtr;
        TEMP_idx = TEMP_count;
        TEMP_count += 1;
    }

    public bool Evaluate()
    {
        if (!_quadTreePtr.IndexInRange(Min.X, Min.Y))
        {
            Value = 0;
            return true;
        }

        bool firstValueSet = false;
        for (int c = Min.Y; c <= Max.Y; ++c)
        {
            for (int r = Min.X; r <= Max.X; ++r)
            {
                if (!firstValueSet)
                {
                    Value = _quadTreePtr.GetValue(r, c);
                    firstValueSet = true;
                    continue;
                }

                if (!_quadTreePtr.IndexInRange(r, c))
                {
                    if (Value != -2)
                    {
                        Value = -1;
                        return false;
                    }
                    Value = 0;
                    return true;
                }

                if (_quadTreePtr.GetValue(r, c) != Value)
                {
                    Value = -1;
                    return false;
                }
            }
        }

        return true;
    }

    public override string ToString()
    {
        return $"Min: {Min}, Max: {Max}, Value: {Value}, idx: {TEMP_idx}";
    }
}

public class QuadTree
{
    public readonly List<QuadTreeLeaf> FinishedNodes = new List<QuadTreeLeaf>();
    Queue<QuadTreeLeaf> _workingNodes = new Queue<QuadTreeLeaf>();
    public int TotalNodeCount;
    public int ProcessedNodeCount;
    public int UncompressedNodeCount;
    public int MaxRows;
    public int MaxColumns;
    Vector2I _min;
    Vector2I _max;
    Vector2 _center;
    public int MaxValue = 1;
    int _maxSteps;


    int[,] _buffer;

    public bool Finished { get; private set; } = false;

    public void AddSpriteFromQuadTreeLeaf(ISpriteSurface surf, NormalAxis normal, bool invert, float scale, float rotation, PlanarMap _planarMapPtr, QuadTreeLeaf leaf, ref Vector2 screenCenter, ref Matrix rotationMatrix)
    {
        Vector2 leafCenter = (Vector2)(leaf.Max + leaf.Min) * 0.5f;
        Vector2 fromCenterPlanar = leafCenter - _center;
        Vector2 rotatedFromCenterPlanar;
        Vector2.TransformNormal(ref fromCenterPlanar, ref rotationMatrix, out rotatedFromCenterPlanar);

        float sign = invert ? -1f : 1f;
        rotatedFromCenterPlanar.X *= sign;

        float lerpScale = (float)(leaf.Value - 1) / _maxSteps;
        Color spriteColor = _planarMapPtr.GetColor(lerpScale);

        surf.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", screenCenter + rotatedFromCenterPlanar * scale, (Vector2)leaf.Span * scale, spriteColor, rotation: rotation * sign));
    }

    public bool IndexInRange(int row, int column)
    {
        return (row < MaxRows) && (column < MaxColumns);
    }

    public int GetValue(int row, int column)
    {
        int value = _buffer[row, column];
        if (value == 0)
            return 0;
        return 1 + (int)(Math.Round((float)value * _maxSteps / MaxValue)); // Need to ensure never zero
    }

    public void Initialize(int[,] buffer, int maxSteps)
    {
        _maxSteps = maxSteps;
        _buffer = buffer;
        MaxRows = _buffer.GetLength(0);
        MaxColumns = _buffer.GetLength(1);
        _min = Vector2I.Zero;
        var maxDim = Math.Max(MaxRows, MaxColumns) - 1;
        _max = new Vector2I(maxDim, maxDim);
        _center = new Vector2(MaxRows - 1, MaxColumns - 1) * 0.5f;

        var span = _max - _min;
        TotalNodeCount = span.X * span.Y;
        ProcessedNodeCount = 0;

        Finished = false;
        FinishedNodes.Clear();
        _workingNodes.Clear();
        _workingNodes.Enqueue(new QuadTreeLeaf(_min, _max, this));
    }

    public void Subdivide()
    {
        if (Finished)
            return;

        if (_workingNodes.Count == 0)
        {
            Finished = true;
            return;
        }

        var currentNode = _workingNodes.Dequeue();
        if (currentNode.Evaluate())
        {
            int numNodesInLeaf = (currentNode.Span.X * currentNode.Span.Y);
            ProcessedNodeCount += numNodesInLeaf;
            if (currentNode.Value > 0)
            {
                FinishedNodes.Add(currentNode);
                UncompressedNodeCount += numNodesInLeaf;
            }
            return;
        }

        var halfSpan = currentNode.Span / 2;
        if (halfSpan.X == 0 && halfSpan.Y == 0) // This should never occur
        {
            throw new Exception("Half span was zero!");
        }
        else if (halfSpan.X == 0)
        {
            var vec = new Vector2I(0, halfSpan.Y - 1);
            _workingNodes.Enqueue(new QuadTreeLeaf(currentNode.Min, currentNode.Min + vec, this));
            _workingNodes.Enqueue(new QuadTreeLeaf(currentNode.Min + halfSpan, currentNode.Max, this));
            return;
        }
        else if (halfSpan.Y == 0)
        {
            var vec = new Vector2I(halfSpan.X - 1, 0);
            _workingNodes.Enqueue(new QuadTreeLeaf(currentNode.Min, currentNode.Min + vec, this));
            _workingNodes.Enqueue(new QuadTreeLeaf(currentNode.Min + halfSpan, currentNode.Max, this));
            return;
        }

        _workingNodes.Enqueue(new QuadTreeLeaf(currentNode.Min, currentNode.Min + halfSpan - Vector2I.One, this));
        _workingNodes.Enqueue(new QuadTreeLeaf(currentNode.Min + new Vector2I(0, halfSpan.Y), new Vector2I(currentNode.Min.X + halfSpan.X - 1, currentNode.Max.Y), this));
        _workingNodes.Enqueue(new QuadTreeLeaf(currentNode.Min + halfSpan, currentNode.Max, this));
        _workingNodes.Enqueue(new QuadTreeLeaf(currentNode.Min + new Vector2I(halfSpan.X, 0), new Vector2I(currentNode.Max.X, currentNode.Min.Y + halfSpan.Y - 1), this));
    }
}

public struct LegendItem
{
    public string Name;
    public Color Color;

    public LegendItem(string name, ref Color color)
    {
        Name = name;
        Color = color;
    }
}

public class Legend
{
    Dictionary<string, LegendItem> _legendItems = new Dictionary<string, LegendItem>();
    StringBuilder _sb = new StringBuilder();

    Color _textColor = new Color(100, 100, 100);
    float _legendSquareSize;
    float _legendFontSize;

    readonly Vector2 TEXT_OFFSET_BASE = new Vector2(0, -0.5f * BASE_TEXT_HEIGHT_PX);
    const float BASE_TEXT_HEIGHT_PX = 28.8f;
    const float HORIZONTAL_SPACING = 16f;
    const float VERTICAL_SPACING = 4f;

    public Legend(float fontSize)
    {
        _legendFontSize = fontSize;
        _legendSquareSize = fontSize * BASE_TEXT_HEIGHT_PX;
    }

    public void GenerateSprites(ISpriteSurface surf, float scale)
    {
        Vector2 textVerticalOffset = TEXT_OFFSET_BASE * _legendFontSize * scale;
        Vector2 legendPosition = Vector2.One * (_legendSquareSize * scale * 0.5f + 4f);
        legendPosition += (surf.TextureSize - surf.SurfaceSize) * 0.5f;
        foreach (var kvp in _legendItems)
        {
            var item = kvp.Value;

            if (item.Color.A == 0)
                continue;

            // Add colored square
            surf.Add(new MySprite(
                SpriteType.TEXTURE,
                "SquareSimple",
                legendPosition,
                Vector2.One * _legendSquareSize * scale,
                item.Color));

            Vector2 textOffset = legendPosition + Vector2.UnitX * (HORIZONTAL_SPACING * scale + _legendSquareSize * scale * 0.5f) + textVerticalOffset;

            surf.Add(new MySprite(
                SpriteType.TEXT,
                data: item.Name,
                position: textOffset,
                color: _textColor,
                fontId: "DEBUG",
                rotation: _legendFontSize * scale,
                alignment: TextAlignment.LEFT
            ));

            legendPosition.Y += BASE_TEXT_HEIGHT_PX * scale * _legendFontSize + Math.Max(VERTICAL_SPACING, VERTICAL_SPACING * scale);
        }
    }

    public void AddLegendItem(string key, string name, ref Color color)
    {
        _legendItems[key] = new LegendItem(name, ref color);
    }

    public void UpdateLegendItemColor(string key, ref Color color)
    {
        LegendItem item;
        if (!_legendItems.TryGetValue(key, out item))
            return;

        item.Color = color;
        _legendItems[key] = item;
    }

}

public class BlockInfo
{
    public readonly Vector3I GridPosition;
    public bool IsMissing;
    public BlockMask BlockMask;

    public BlockInfo(ref Vector3I gridPosition, IMyCubeGrid grid)
    {
        GridPosition = gridPosition;
        IsMissing = false;

        var slim = grid.GetCubeBlock(gridPosition);
        if (slim == null)
        {
            return;
        }

        var cube = slim.FatBlock;
        if (cube == null)
        {
            return;
        }

        if (cube as IMyPowerProducer != null)
            BlockMask |= BlockMask.Power;

        if (cube as IMyGyro != null)
            BlockMask |= BlockMask.Gyro;

        if (cube as IMyThrust != null)
            BlockMask |= BlockMask.Thrust;

        if (cube as IMyUserControllableGun != null)
            BlockMask |= BlockMask.Weapon;
    }
}

public class BlockStatusSpriteCreator
{
    readonly Vector2 _fromCenter;
    readonly PlanarMap _planarMapPtr;
    Vector3I _gridPosition;

    public BlockStatusSpriteCreator(Vector3I gridPosition, Vector3 positionFromCenter, PlanarMap planarMapPtr, NormalAxis normal)
    {
        _gridPosition = gridPosition;
        switch (normal)
        {
            case NormalAxis.X:
                _fromCenter.X = positionFromCenter.Y;
                _fromCenter.Y = positionFromCenter.Z;
                break;

            case NormalAxis.Y:
                _fromCenter.X = positionFromCenter.Z;
                _fromCenter.Y = positionFromCenter.X;
                break;

            case NormalAxis.Z:
                _fromCenter.X = positionFromCenter.X;
                _fromCenter.Y = positionFromCenter.Y;
                break;
        }
        _planarMapPtr = planarMapPtr;
    }

    public void CreateSprite(ISpriteSurface surf, NormalAxis normal, float scale, float rotation, bool invert, ref Vector2 screenCenter, ref Matrix rotationMatrix)
    {
        Vector2 fromCenterPlanar = _fromCenter;

        Vector2 rotatedFromCenterPlanar;
        Vector2.TransformNormal(ref fromCenterPlanar, ref rotationMatrix, out rotatedFromCenterPlanar);

        float sign = invert ? -1f : 1f;
        rotatedFromCenterPlanar.X *= sign;

        Color functionalSpriteColor;
        if (_planarMapPtr.DrawBlockMaskSprite(normal, ref _gridPosition, out functionalSpriteColor) && functionalSpriteColor.A > 0)
        {
            surf.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", screenCenter + rotatedFromCenterPlanar * scale, Vector2.One * scale, functionalSpriteColor, rotation: rotation * sign));
        }
    }
}

// Stores block densities and which ones are missing
public class PlanarMap
{
    public List<BlockStatusSpriteCreator> StatusSpriteCreatorsX = new List<BlockStatusSpriteCreator>();
    public List<BlockStatusSpriteCreator> StatusSpriteCreatorsY = new List<BlockStatusSpriteCreator>();
    public List<BlockStatusSpriteCreator> StatusSpriteCreatorsZ = new List<BlockStatusSpriteCreator>();


    readonly int[,] _densityXNormal;
    readonly int[,] _densityYNormal;
    readonly int[,] _densityZNormal;
    readonly bool[,] _missingXNormal;
    readonly bool[,] _missingYNormal;
    readonly bool[,] _missingZNormal;
    readonly BlockMask[,] _masksXNormal;
    readonly BlockMask[,] _masksYNormal;
    readonly BlockMask[,] _masksZNormal;
    public QuadTree QuadTreeXNormal = new QuadTree();
    public QuadTree QuadTreeYNormal = new QuadTree();
    public QuadTree QuadTreeZNormal = new QuadTree();
    readonly HashSet<Vector2> _positionsXNormal = new HashSet<Vector2>();
    readonly HashSet<Vector2> _positionsYNormal = new HashSet<Vector2>();
    readonly HashSet<Vector2> _positionsZNormal = new HashSet<Vector2>();

    readonly Vector3I _min;
    readonly Vector3I _max;
    readonly Vector3 _center;

    int _maxDensityX = 0;
    int _maxDensityY = 0;
    int _maxDensityZ = 0;

    Color _colorMinDensity;
    Color _colorMaxDensity;
    Color _colorMissing;
    Color _colorPower;
    Color _colorWeapon;
    Color _colorGyro;
    Color _colorThrust;

    public PlanarMap(IMyCubeGrid grid, Color minDensityColor, Color maxDensityColor, Color missingColor, Color powerColor, Color weaponColor, Color gyroColor, Color thrustColor)
    {
        _min = grid.Min;
        _max = grid.Max;
        _center = (Vector3)(_min + _max) * 0.5f;

        var diff = _max - _min;
        _densityXNormal = new int[diff.Y + 1, diff.Z + 1];
        _densityYNormal = new int[diff.Z + 1, diff.X + 1];
        _densityZNormal = new int[diff.X + 1, diff.Y + 1];

        _missingXNormal = new bool[diff.Y + 1, diff.Z + 1];
        _missingYNormal = new bool[diff.Z + 1, diff.X + 1];
        _missingZNormal = new bool[diff.X + 1, diff.Y + 1];

        _masksXNormal = new BlockMask[diff.Y + 1, diff.Z + 1];
        _masksYNormal = new BlockMask[diff.Z + 1, diff.X + 1];
        _masksZNormal = new BlockMask[diff.X + 1, diff.Y + 1];

        UpdateColors(ref minDensityColor, ref maxDensityColor, ref missingColor, ref powerColor, ref weaponColor, ref gyroColor, ref thrustColor);
    }

    public void CreateQuadTrees()
    {
        QuadTreeXNormal.Initialize(_densityXNormal, DISCRETE_DENSITY_STEPS);
        QuadTreeYNormal.Initialize(_densityYNormal, DISCRETE_DENSITY_STEPS);
        QuadTreeZNormal.Initialize(_densityZNormal, DISCRETE_DENSITY_STEPS);
    }

    public void UpdateColors(ref Color minDensityColor, ref Color maxDensityColor, ref Color missingColor, ref Color colorPower, ref Color weaponColor, ref Color gyroColor, ref Color thrustColor)
    {
        _colorMinDensity = minDensityColor;
        _colorMaxDensity = maxDensityColor;
        _colorMissing = missingColor;
        _colorPower = colorPower;
        _colorWeapon = weaponColor;
        _colorGyro = gyroColor;
        _colorThrust = thrustColor;
    }

    public void StoreBlockInfo(BlockInfo info)
    {
        var diff = info.GridPosition - _min;
        var fromCenter = info.GridPosition - _center;

        _densityXNormal[diff.Y, diff.Z] += 1;
        _densityYNormal[diff.Z, diff.X] += 1;
        _densityZNormal[diff.X, diff.Y] += 1;

        _masksXNormal[diff.Y, diff.Z] |= info.BlockMask;
        _masksYNormal[diff.Z, diff.X] |= info.BlockMask;
        _masksZNormal[diff.X, diff.Y] |= info.BlockMask;

        _maxDensityX = Math.Max(_maxDensityX, _densityXNormal[diff.Y, diff.Z]);
        _maxDensityY = Math.Max(_maxDensityY, _densityYNormal[diff.Z, diff.X]);
        _maxDensityZ = Math.Max(_maxDensityZ, _densityZNormal[diff.X, diff.Y]);

        QuadTreeXNormal.MaxValue = _maxDensityX;
        QuadTreeYNormal.MaxValue = _maxDensityY;
        QuadTreeZNormal.MaxValue = _maxDensityZ;

        Vector2 posX = new Vector2(diff.Y, diff.Z);
        Vector2 posY = new Vector2(diff.Z, diff.X);
        Vector2 posZ = new Vector2(diff.X, diff.Y);
        if (!_positionsXNormal.Contains(posX))
        {
            StatusSpriteCreatorsX.Add(new BlockStatusSpriteCreator(info.GridPosition, fromCenter, this, NormalAxis.X));
            _positionsXNormal.Add(posX);
        }
        if (!_positionsYNormal.Contains(posY))
        {
            StatusSpriteCreatorsY.Add(new BlockStatusSpriteCreator(info.GridPosition, fromCenter, this, NormalAxis.Y));
            _positionsYNormal.Add(posY);
        }
        if (!_positionsZNormal.Contains(posZ))
        {
            StatusSpriteCreatorsZ.Add(new BlockStatusSpriteCreator(info.GridPosition, fromCenter, this, NormalAxis.Z));
            _positionsZNormal.Add(posZ);
        }

    }

    public void UpdateForDamage(BlockInfo info)
    {
        var diff = info.GridPosition - _min;
        _missingXNormal[diff.Y, diff.Z] = info.IsMissing;
        _missingYNormal[diff.Z, diff.X] = info.IsMissing;
        _missingZNormal[diff.X, diff.Y] = info.IsMissing;
    }

    public bool DrawBlockMaskSprite(NormalAxis normal, ref Vector3I blockPosition, out Color functionalSpriteColor)
    {
        blockPosition = Vector3I.Max(_min, Vector3I.Min(_max, blockPosition));
        var diff = blockPosition - _min;

        bool missing;
        BlockMask blockMasks;
        if (NormalAxis.X == normal)
        {
            blockMasks = _masksXNormal[diff.Y, diff.Z];
            missing = _missingXNormal[diff.Y, diff.Z];
        }
        else if (NormalAxis.Y == normal)
        {
            blockMasks = _masksYNormal[diff.Z, diff.X];
            missing = _missingYNormal[diff.Z, diff.X];
        }
        else
        {
            blockMasks = _masksZNormal[diff.X, diff.Y];
            missing = _missingZNormal[diff.X, diff.Y];
        }


        if (missing)
        {
            functionalSpriteColor = _colorMissing;
        }
        else if (_colorWeapon.A > 0 && (blockMasks & BlockMask.Weapon) != 0)
        {
            functionalSpriteColor = _colorWeapon;
        }
        else if (_colorPower.A > 0 && (blockMasks & BlockMask.Power) != 0)
        {
            functionalSpriteColor = _colorPower;
        }
        else if (_colorGyro.A > 0 && (blockMasks & BlockMask.Gyro) != 0)
        {
            functionalSpriteColor = _colorGyro;
        }
        else if (_colorThrust.A > 0 && (blockMasks & BlockMask.Thrust) != 0)
        {
            functionalSpriteColor = _colorThrust;
        }
        else
        {
            functionalSpriteColor = Color.Transparent;
        }

        return missing || blockMasks != BlockMask.None;
    }

    public Color GetColor(float lerpScale)
    {
        return Color.Lerp(_colorMinDensity, _colorMaxDensity, lerpScale);
    }

    public Color GetColor(NormalAxis normal, ref Vector3I blockPosition)
    {
        blockPosition = Vector3I.Max(_min, Vector3I.Min(_max, blockPosition));
        var diff = blockPosition - _min;

        int density;
        int maxDensity;
        if (NormalAxis.X == normal)
        {
            density = _densityXNormal[diff.Y, diff.Z];
            maxDensity = _maxDensityX;
        }
        else if (NormalAxis.Y == normal)
        {
            density = _densityYNormal[diff.Z, diff.X];
            maxDensity = _maxDensityY;
        }
        else
        {
            density = _densityZNormal[diff.X, diff.Y];
            maxDensity = _maxDensityZ;
        }

        float lerpScale = (float)density / maxDensity;
        lerpScale = (float)(Math.Round(lerpScale * DISCRETE_DENSITY_STEPS) / DISCRETE_DENSITY_STEPS);
        return Color.Lerp(_colorMinDensity, _colorMaxDensity, lerpScale);
    }
}

/// <summary>
/// Class that tracks runtime history.
/// </summary>
public class RuntimeTracker
{
    public int Capacity { get; set; }
    public double Sensitivity { get; set; }
    public double MaxRuntime { get; private set; }
    public double MaxInstructions { get; private set; }
    public double AverageRuntime { get; private set; }
    public double AverageInstructions { get; private set; }
    public double LastRuntime { get; private set; }
    public double LastInstructions { get; private set; }

    readonly Queue<double> _runtimes = new Queue<double>();
    readonly Queue<double> _instructions = new Queue<double>();
    readonly StringBuilder _sb = new StringBuilder();
    readonly int _instructionLimit;
    readonly Program _program;
    const double MS_PER_TICK = 16.6666;

    public RuntimeTracker(Program program, int capacity = 100, double sensitivity = 0.005)
    {
        _program = program;
        Capacity = capacity;
        Sensitivity = sensitivity;
        _instructionLimit = _program.Runtime.MaxInstructionCount;
    }

    public void AddRuntime()
    {
        double runtime = _program.Runtime.LastRunTimeMs;
        LastRuntime = runtime;
        AverageRuntime += (Sensitivity * runtime);
        int roundedTicksSinceLastRuntime = (int)Math.Round(_program.Runtime.TimeSinceLastRun.TotalMilliseconds / MS_PER_TICK);
        if (roundedTicksSinceLastRuntime == 1)
        {
            AverageRuntime *= (1 - Sensitivity);
        }
        else if (roundedTicksSinceLastRuntime > 1)
        {
            AverageRuntime *= Math.Pow((1 - Sensitivity), roundedTicksSinceLastRuntime);
        }

        _runtimes.Enqueue(runtime);
        if (_runtimes.Count == Capacity)
        {
            _runtimes.Dequeue();
        }

        MaxRuntime = _runtimes.Max();
    }

    public void AddInstructions()
    {
        double instructions = _program.Runtime.CurrentInstructionCount;
        LastInstructions = instructions;
        AverageInstructions = Sensitivity * (instructions - AverageInstructions) + AverageInstructions;

        _instructions.Enqueue(instructions);
        if (_instructions.Count == Capacity)
        {
            _instructions.Dequeue();
        }

        MaxInstructions = _instructions.Max();
    }

    public string Write()
    {
        _sb.Clear();
        _sb.AppendLine("General Runtime Info");
        _sb.AppendLine($"  Avg instructions: {AverageInstructions:n2}");
        _sb.AppendLine($"  Last instructions: {LastInstructions:n0}");
        _sb.AppendLine($"  Max instructions: {MaxInstructions:n0}");
        _sb.AppendLine($"  Avg complexity: {MaxInstructions / _instructionLimit:0.000}%");
        _sb.AppendLine($"  Avg runtime: {AverageRuntime:n4} ms");
        _sb.AppendLine($"  Last runtime: {LastRuntime:n4} ms");
        _sb.AppendLine($"  Max runtime: {MaxRuntime:n4} ms");
        return _sb.ToString();
    }
}

#region Scheduler
/// <summary>
/// Class for scheduling actions to occur at specific frequencies. Actions can be updated in parallel or in sequence (queued).
/// </summary>
public class Scheduler
{
    ScheduledAction _currentlyQueuedAction = null;
    bool _firstRun = true;

    readonly bool _ignoreFirstRun;
    readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
    readonly List<ScheduledAction> _actionsToDispose = new List<ScheduledAction>();
    readonly Queue<ScheduledAction> _queuedActions = new Queue<ScheduledAction>();
    readonly Program _program;

    const double RUNTIME_TO_REALTIME = (1.0 / 60.0) / 0.0166666;

    public Scheduler(Program program, bool ignoreFirstRun = false)
    {
        _program = program;
        _ignoreFirstRun = ignoreFirstRun;
    }

    public void Update()
    {
        double deltaTime = Math.Max(0, _program.Runtime.TimeSinceLastRun.TotalSeconds * RUNTIME_TO_REALTIME);

        if (_ignoreFirstRun && _firstRun)
            deltaTime = 0;

        _firstRun = false;
        _actionsToDispose.Clear();
        foreach (ScheduledAction action in _scheduledActions)
        {
            action.Update(deltaTime);
            if (action.JustRan && action.DisposeAfterRun)
            {
                _actionsToDispose.Add(action);
            }
        }

        // Remove all actions that we should dispose
        _scheduledActions.RemoveAll((x) => _actionsToDispose.Contains(x));

        if (_currentlyQueuedAction == null)
        {
            // If queue is not empty, populate current queued action
            if (_queuedActions.Count != 0)
                _currentlyQueuedAction = _queuedActions.Dequeue();
        }

        // If queued action is populated
        if (_currentlyQueuedAction != null)
        {
            _currentlyQueuedAction.Update(deltaTime);
            if (_currentlyQueuedAction.JustRan)
            {
                // Set the queued action to null for the next cycle
                _currentlyQueuedAction = null;
            }
        }
    }

    public void AddScheduledAction(Action action, double updateFrequency, bool disposeAfterRun = false, double timeOffset = 0)
    {
        ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun, timeOffset);
        _scheduledActions.Add(scheduledAction);
    }

    public void AddScheduledAction(ScheduledAction scheduledAction)
    {
        _scheduledActions.Add(scheduledAction);
    }

    public void AddQueuedAction(Action action, double updateInterval)
    {
        if (updateInterval <= 0)
        {
            updateInterval = 0.001; // avoids divide by zero
        }
        ScheduledAction scheduledAction = new ScheduledAction(action, 1.0 / updateInterval, true);
        _queuedActions.Enqueue(scheduledAction);
    }

    public void AddQueuedAction(ScheduledAction scheduledAction)
    {
        _queuedActions.Enqueue(scheduledAction);
    }
}

public class ScheduledAction
{
    public bool JustRan { get; private set; } = false;
    public bool DisposeAfterRun { get; private set; } = false;
    public double TimeSinceLastRun { get; private set; } = 0;
    public double RunInterval
    {
        get
        {
            return _runInterval;
        }
        set
        {
            if (value == _runInterval)
                return;

            _runInterval = value < Epsilon ? 0 : value;
            _runFrequency = value == 0 ? double.MaxValue : 1.0 / _runInterval;
        }
    }
    public double RunFrequency
    {
        get
        {
            return _runFrequency;
        }
        set
        {
            if (value == _runFrequency)
                return;

            if (value == 0)
                RunInterval = double.MaxValue;
            else
                RunInterval = 1.0 / value;
        }
    }

    double _runInterval = -1e9;
    double _runFrequency = -1e9;
    readonly Action _action;

    const double Epsilon = 1e-12;

    public ScheduledAction(Action action, double runFrequency = 0, bool removeAfterRun = false, double timeOffset = 0)
    {
        _action = action;
        RunFrequency = runFrequency; // Implicitly sets RunInterval
        DisposeAfterRun = removeAfterRun;
        TimeSinceLastRun = timeOffset;
    }

    public void Update(double deltaTime)
    {
        TimeSinceLastRun += deltaTime;

        if (TimeSinceLastRun + Epsilon >= RunInterval)
        {
            _action.Invoke();
            TimeSinceLastRun = 0;

            JustRan = true;
        }
        else
        {
            JustRan = false;
        }
    }
}
#endregion

public static class MyIniHelper
{
    public static void SetColor(string sectionName, string itemName, Color color, MyIni ini)
    {
        string colorString = string.Format("{0}, {1}, {2}, {3}", color.R, color.G, color.B, color.A);
        ini.Set(sectionName, itemName, colorString);
    }

    public static Color GetColor(string sectionName, string itemName, MyIni ini, Color? defaultChar = null)
    {
        string rgbString = ini.Get(sectionName, itemName).ToString("null");
        string[] rgbSplit = rgbString.Split(',');

        int r = 0, g = 0, b = 0, a = 0;
        if (rgbSplit.Length != 4)
        {
            if (defaultChar.HasValue)
                return defaultChar.Value;
            else
                return Color.Transparent;
        }

        int.TryParse(rgbSplit[0].Trim(), out r);
        int.TryParse(rgbSplit[1].Trim(), out g);
        int.TryParse(rgbSplit[2].Trim(), out b);
        bool hasAlpha = int.TryParse(rgbSplit[3].Trim(), out a);
        if (!hasAlpha)
            a = 255;

        r = MathHelper.Clamp(r, 0, 255);
        g = MathHelper.Clamp(g, 0, 255);
        b = MathHelper.Clamp(b, 0, 255);
        a = MathHelper.Clamp(a, 0, 255);

        return new Color(r, g, b, a);
    }
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
#endregion
