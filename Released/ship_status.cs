
#region SIMPL
/*
/ //// / Whip's Ship Integrity Monitoring Program (Lite) / //// /






















============================================
    DO NOT EDIT VARIABLES IN THE SCRIPT
            USE THE CUSTOM DATA!
============================================
*/

const string VERSION = "1.10.2";
const string DATE = "2024/03/01";


const NormalAxis DEFAULT_NORMAL_AXIS = NormalAxis.X;
const float DEFAULT_ROTATION = 0f;
const bool DEFAULT_INVERT = false;
const float DEFAULT_LEGEND_SCALE = 1f;

const int BLOCKS_TO_STORE_PER_TICK = 500;
const int BLOCKS_TO_CHECK_PER_TICK = 100;
const int SPRITES_TO_CREATE_PER_TICK = 250;
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

const string INI_SCREEN_ID_TEMPLATE = " - Screen {0}";

const string INI_SECTION_LEGEND = "SIMPL - Legend Config{0}";
const string INI_SECTION_TEXT_CONFIG_TEMPLATE = "SIMPL - Display Config{0} - View {1}";

const string INI_SECTION_TEXT_CONFIG_COMPAT = "SIMPL - Display Config{0}";

const string INI_KEY_LEGEND_SCALE = "Legend Scale";
const string INI_KEY_LEGEND_POS = "Position";

const string INI_KEY_NORMAL = "View axis";
const string INI_KEY_ROTATION = "Rotation (deg)";
const string INI_KEY_SCALE = "Scale";
const string INI_KEY_VIEW_POS = "Position";

const string INI_KEY_NORMAL_COMPAT = "Normal axis";
const string INI_KEY_AUTOSCALE_COMPAT = "Autoscale layout";
const string INI_KEY_SCALE_COMPAT = "Manual layout scale";
const string INI_KEY_INVERT_COMPAT = "Flip horizontally";

const string INI_SECTION_TEXT_SURF = "SIMPL - Text Surface Config";
const string INI_KEY_TEXT_SURF_TEMPLATE = "Show on screen {0}";
const string INI_KEY_NUM_VIEWS = "Number of views";
const string INI_KEY_NUM_VIEWS_TEMPLATE = "Number of views for screen {0}";

const string INI_COMMENT_NORMAL = " View axis values: X, Y, Z, NegativeX, NegativeY, NegativeZ";

const string INI_SECTION_MULTISCREEN = "SIMPL - Multiscreen Config";
const string INI_KEY_MULTISCREEN_ROWS = "Screen rows";
const string INI_KEY_MULTISCREEN_COLS = "Screen cols";

// Configurable
ConfigString _textSurfaceGroupName = new ConfigString(INI_KEY_GROUP_NAME, "SIMPL");
ConfigBool _autoscan = new ConfigBool(INI_KEY_AUTO_SCAN, true);

ConfigColor _bgColor = new ConfigColor(INI_KEY_COLOR_BG, new Color(0, 0, 0));

ConfigFloat _legendScale = new ConfigFloat(INI_KEY_LEGEND_SCALE, DEFAULT_LEGEND_SCALE);
ConfigVector2 _legendPosition = new ConfigVector2(INI_KEY_LEGEND_POS, new Vector2(-1f, -1f), " Elements should range from -1 to 1 where 0 indicates centered");

ConfigEnum<NormalAxis> _normal = new ConfigEnum<NormalAxis>(INI_KEY_NORMAL, DEFAULT_NORMAL_AXIS, INI_COMMENT_NORMAL);
ConfigFloat _rotation = new ConfigFloat(INI_KEY_ROTATION, DEFAULT_ROTATION);
ConfigNullable<float, ConfigFloat> _screenScale = new ConfigNullable<float, ConfigFloat>(new ConfigFloat(INI_KEY_SCALE), "auto");
ConfigVector2 _viewPosition = new ConfigVector2(INI_KEY_VIEW_POS, new Vector2(0, 0), " Elements should range from -1 to 1 where 0 indicates centered");

ConfigDeprecated<bool, ConfigBool> _autoscaleCompat = new ConfigDeprecated<bool, ConfigBool>(new ConfigBool(INI_KEY_AUTOSCALE_COMPAT, true));
ConfigDeprecated<float, ConfigFloat> _screenScaleCompat = new ConfigDeprecated<float, ConfigFloat>(new ConfigFloat(INI_KEY_SCALE_COMPAT, 1f));
ConfigDeprecated<NormalAxis, ConfigEnum<NormalAxis>> _normalCompat = new ConfigDeprecated<NormalAxis, ConfigEnum<NormalAxis>>(new ConfigEnum<NormalAxis>(INI_KEY_NORMAL_COMPAT, NormalAxis.X));
ConfigDeprecated<bool, ConfigBool> _invertCompat = new ConfigDeprecated<bool, ConfigBool>(new ConfigBool(INI_KEY_INVERT_COMPAT, false));

ConfigInt _rows = new ConfigInt(INI_KEY_MULTISCREEN_ROWS, 1);
ConfigInt _cols = new ConfigInt(INI_KEY_MULTISCREEN_COLS, 1);

ConfigSection _sectionGeneral = new ConfigSection(INI_SECTION_NAME);
ConfigSection _sectionColors = new ConfigSection(INI_SECTION_COLOR, INI_COMMENT_COLOR);
ConfigSection 
    _sectionLegend = new ConfigSection(""),
    _sectionDisplay = new ConfigSection(""),
    _sectionDisplayCompat = new ConfigSection("");
ConfigSection _sectionMultiscreen = new ConfigSection(INI_SECTION_MULTISCREEN);

void ConfigureIni()
{
    _screenScaleCompat.Callback = (scale) => {
        if (!_autoscaleCompat.Implementation.Value)
        {
            _screenScale.Value = scale;
        }
    };

    _normalCompat.Callback = (normal) =>
    {
        _normal.Value = normal;
    };

    _invertCompat.Callback = (invert) =>
    {
        if (invert)
        {
            _normal.Value |= NormalAxis.Negative;
        }
    };

    _sectionGeneral.AddValues(_textSurfaceGroupName, _autoscan);

    _sectionColors.AddValues(
        _planarMap.ColorMaxDensity,
        _planarMap.ColorMinDensity,
        _planarMap.ColorMissing,
        _bgColor,
        _planarMap.ColorWeapon,
        _planarMap.ColorPower,
        _planarMap.ColorGyro,
        _planarMap.ColorThrust
    );

    _sectionLegend.AddValues(
        _legendScale,
        _legendPosition
    );

    _sectionDisplay.AddValues(
        _normal, 
        _rotation, 
        _screenScale, 
        _viewPosition
    );

    _sectionDisplayCompat.AddValues(
        _autoscaleCompat, 
        _screenScaleCompat, 
        _normalCompat, 
        _invertCompat, 
        _normal, 
        _rotation,
        _screenScale,
        _viewPosition
    );

    _sectionMultiscreen.AddValues(_rows, _cols);
}

float _textSize = 0.5f;

List<TextSurfaceConfig> _textSurfaces = new List<TextSurfaceConfig>();
List<BlockInfo> _blockInfoArray = new List<BlockInfo>();
PlanarMap _planarMap;
Scheduler _scheduler;
RuntimeTracker _runtimeTracker;
Legend _legend;
LoadingScreen _loadingScreen = new LoadingScreen($"Loading SIMPL (v{VERSION})", "");
MyIni _ini = new MyIni();
StringBuilder _echoBuilder = new StringBuilder(512);

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

public enum NormalAxis { X = 0, Y = 1, Z = 2, Axes = X | Y | Z, Negative = 4, NegativeX = Negative | X, NegativeY = Negative | Y, NegativeZ = Negative | Z };
public enum BlockMask { None, Power = 1, Gyro = 2, Thrust = 4, Weapon = 8 }
public enum BlockStatus { Nominal = 0, Damaged = 1, Missing = 2 };

Program()
{
    _planarMap = new PlanarMap(Me.CubeGrid);
    ConfigureIni();

    InitializeGridBlockStorage();

    _runtimeTracker = new RuntimeTracker(this, 600);
    _scheduler = new Scheduler(this);
    _scheduler.AddScheduledAction(TryStartSpriteDraw, 0.5);
    _scheduler.AddScheduledAction(HandleStateMachines, 60);
    _scheduler.AddScheduledAction(WriteDetailedInfo, 1);

    _forceDrawTimeout = new ScheduledAction(() => _allowForceDraw = true, 1.0 / 30.0, true);

    _legend = new Legend(_textSize);
    _legend.AddLegendItem(INI_KEY_COLOR_MISSING, "Damage", _planarMap.ColorMissing);
    _legend.AddLegendItem(INI_KEY_COLOR_WEAPON, "Weapons", _planarMap.ColorWeapon);
    _legend.AddLegendItem(INI_KEY_COLOR_POWER, "Power", _planarMap.ColorPower);
    _legend.AddLegendItem(INI_KEY_COLOR_GYRO, "Gyros", _planarMap.ColorGyro);
    _legend.AddLegendItem(INI_KEY_COLOR_THRUST, "Thrust", _planarMap.ColorThrust);

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
        Echo($"\nBlock storage progress: {_blockStorageStateMachine.Current:n0}%");
    }
    else
    {
        Echo($"\nQuadTree Compression Ratio:");
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
class TextSurfaceConfig
{
    public struct ViewConfig
    {
        public NormalAxis Normal;
        public float RotationRad;
        public float? Scale;
        public Vector2 RelativePosition;
    }

    public readonly ISpriteSurface Surface;
    public readonly List<ViewConfig> Views = new List<ViewConfig>();
    public float LegendScale;
    public Vector2 LegendRelativePos;

    public TextSurfaceConfig(ISpriteSurface surface)
    {
        Surface = surface;
    }

    public void AddView(NormalAxis normal, float rotationDeg, float? scale, Vector2 relativePosition)
    {
        var view = new ViewConfig
        {
            Normal = normal,
            RotationRad = MathHelper.ToRadians(rotationDeg),
            Scale = scale,
            RelativePosition = relativePosition
        };

        Views.Add(view);
    }
}

void GetSurfaceConfigValues(IMyTerminalBlock b, int? surfaceIdx, int numViews, TextSurfaceConfig config)
{
    string surfName;
    if (surfaceIdx.HasValue)
    {
        surfName = string.Format(INI_SCREEN_ID_TEMPLATE, surfaceIdx.Value);
    }
    else
    {
        surfName = "";
    }
    
    string legendSection = string.Format(INI_SECTION_LEGEND, surfName);


    for (int ii = 1; ii <= numViews; ++ii)
    {
        string displaySection = string.Format(INI_SECTION_TEXT_CONFIG_TEMPLATE, surfName, ii);
        _sectionDisplay.Section = displaySection;

        if (ii == 1)
        {
            string compatName = string.Format(INI_SECTION_TEXT_CONFIG_COMPAT, surfName);

            if (_ini.ContainsSection(compatName))
            {
                _legendScale.ReadFromIni(ref _ini, compatName);
                _sectionDisplayCompat.Section = compatName;
                _sectionDisplayCompat.ReadFromIni(ref _ini);

                _ini.DeleteSection(compatName);

                _legendScale.WriteToIni(ref _ini, legendSection);
                _sectionDisplayCompat.Section = displaySection;
                _sectionDisplayCompat.WriteToIni(ref _ini);
            }
        }

        _sectionLegend.Section = legendSection;
        _sectionDisplay.Update(ref _ini);

        config.AddView(_normal, _rotation, _screenScale.HasValue ? _screenScale.Value : (float?)null, _viewPosition);
    }

    _sectionLegend.Update(ref _ini);

    config.LegendScale = _legendScale;
    config.LegendRelativePos = _legendPosition;
}

bool CollectScreens(IMyTerminalBlock b)
{
    if (!b.IsSameConstructAs(Me))
        return false;

    var tp = b as IMyTextPanel;
    var tsp = b as IMyTextSurfaceProvider;
    if (tp == null && tsp == null)
    {
        return false;
    }

    _ini.Clear();
    bool parsed = _ini.TryParse(b.CustomData);
    if (!parsed && !string.IsNullOrWhiteSpace(b.CustomData))
    {
        _ini.EndContent = b.CustomData;
    }

    if (tp != null)
    {
        string viewsKey = INI_KEY_NUM_VIEWS;
        int numViews = _ini.Get(INI_SECTION_TEXT_SURF, INI_KEY_NUM_VIEWS).ToInt32(1);
        _ini.Set(INI_SECTION_TEXT_SURF, viewsKey, numViews);

        if (numViews > 0)
        {
            bool multiscreen = _ini.ContainsSection(INI_SECTION_MULTISCREEN);
            if (multiscreen)
            {
                _sectionMultiscreen.Update(ref _ini); // TODO: clamp
            }

            ISpriteSurface surf;
            if (multiscreen && (_rows > 1 || _cols > 1))
            {
                surf = new MultiScreenSpriteSurface(tp, _rows, _cols, this);
            }
            else
            {
                surf = new SingleScreenSpriteSurface(tp);
            }

            var config = new TextSurfaceConfig(surf);
            GetSurfaceConfigValues(b, null, numViews, config);

            _textSurfaces.Add(config);
        }

    }
    else if (tsp != null)
    {
        int surfaceCount = tsp.SurfaceCount;
        for (int i = 0; i < surfaceCount; ++i)
        {
            int numViews = i == 0 ? 1 : 0;

            // Compatability code
            string displayKey = string.Format(INI_KEY_TEXT_SURF_TEMPLATE, i);
            bool legacyDisplay = _ini.Get(INI_SECTION_TEXT_SURF, displayKey).ToBoolean(i == 0);
            _ini.Delete(INI_SECTION_TEXT_SURF, displayKey);
            if (legacyDisplay && numViews < 1)
            {
                numViews = 1;
            }

            string viewsKey = string.Format(INI_KEY_NUM_VIEWS_TEMPLATE, i);
            numViews = _ini.Get(INI_SECTION_TEXT_SURF, viewsKey).ToInt32(numViews);
            _ini.Set(INI_SECTION_TEXT_SURF, viewsKey, numViews);

            if (numViews > 0)
            {
                var surf = new SingleScreenSpriteSurface(tsp.GetSurface(i));
                var config = new TextSurfaceConfig(surf);
                GetSurfaceConfigValues(b, i, numViews, config);
                _textSurfaces.Add(config);
            }
        }

    }

    string output = _ini.ToString();
    if (!string.Equals(output, b.CustomData))
    {
        b.CustomData = output;
    }

    return false;
}

void ParseGeneralConfig()
{
    _ini.Clear();
    bool parsed = _ini.TryParse(Me.CustomData);

    _sectionGeneral.Update(ref _ini);
    _sectionColors.Update(ref _ini);

    if (!parsed && !string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    _legend.UpdateLegendItemColor(INI_KEY_COLOR_MISSING, _planarMap.ColorMissing);
    _legend.UpdateLegendItemColor(INI_KEY_COLOR_WEAPON, _planarMap.ColorWeapon);
    _legend.UpdateLegendItemColor(INI_KEY_COLOR_POWER, _planarMap.ColorPower);
    _legend.UpdateLegendItemColor(INI_KEY_COLOR_GYRO, _planarMap.ColorGyro);
    _legend.UpdateLegendItemColor(INI_KEY_COLOR_THRUST, _planarMap.ColorThrust);

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
HashSet<Vector3I> _enqueuedPositions = new HashSet<Vector3I>();
Queue<Vector3I> _cellsToStore = new Queue<Vector3I>();
Vector3I _maxCheckedPos = Vector3I.MinValue;
Vector3I _minCheckedPos = Vector3I.MaxValue;

void EnqueuePositionIfUnique(Vector3I pos)
{
    if (!_enqueuedPositions.Contains(pos))
    {
        _cellsToStore.Enqueue(pos);
        _enqueuedPositions.Add(pos);
    }
}

public IEnumerator<float> GridSpaceStorageIterator()
{
    int volume = (Me.CubeGrid.Max - Me.CubeGrid.Min + Vector3I.One).Volume();
    EnqueuePositionIfUnique(Me.Position);

    _storageStageStr = "Storing blocks...";

    int blocksStored = 0;
    while (_cellsToStore.Count > 0)
    {
        Vector3I pos = _cellsToStore.Dequeue();
        _maxCheckedPos = Vector3I.Max(pos, _maxCheckedPos);
        _minCheckedPos = Vector3I.Min(pos, _minCheckedPos);
        int checkedVolume = (_maxCheckedPos - _minCheckedPos + Vector3I.One).Volume();
        if (Me.CubeGrid.CubeExists(pos))
        {
            BlockInfo blockInfo = new BlockInfo(ref pos, Me.CubeGrid);
            _blockInfoArray.Add(blockInfo);
            _planarMap.StoreBlockInfo(blockInfo);

            // Step towards neighbors
            EnqueuePositionIfUnique(pos + Vector3I.UnitX);
            EnqueuePositionIfUnique(pos + Vector3I.UnitY);
            EnqueuePositionIfUnique(pos + Vector3I.UnitZ);
            EnqueuePositionIfUnique(pos - Vector3I.UnitX);
            EnqueuePositionIfUnique(pos - Vector3I.UnitY);
            EnqueuePositionIfUnique(pos - Vector3I.UnitZ);
        }

        blocksStored++;
        if (blocksStored % BLOCKS_TO_STORE_PER_TICK == 0)
        {
            yield return 70f * Math.Min(1f, checkedVolume / volume);
        }
    }

    _planarMap.CreateQuadTrees();
    yield return 70f;

    _storageStageStr = "Processing X-axis Quad Tree...";
    while (!_planarMap.QuadTreeXNormal.Finished)
    {
        _planarMap.QuadTreeXNormal.Subdivide();
        if (Runtime.CurrentInstructionCount > 5000)
            yield return 70f + 10f * _planarMap.QuadTreeXNormal.ProcessedNodeCount / _planarMap.QuadTreeXNormal.TotalNodeCount;
    }

    _storageStageStr = "Processing Y-axis Quad Tree...";
    while (!_planarMap.QuadTreeYNormal.Finished)
    {
        _planarMap.QuadTreeYNormal.Subdivide();
        if (Runtime.CurrentInstructionCount > 5000)
            yield return 80f + 10f * _planarMap.QuadTreeYNormal.ProcessedNodeCount / _planarMap.QuadTreeYNormal.TotalNodeCount;
    }

    _storageStageStr = "Processing Z-axis Quad Tree...";
    while (!_planarMap.QuadTreeZNormal.Finished)
    {
        _planarMap.QuadTreeZNormal.Subdivide();
        if (Runtime.CurrentInstructionCount > 5000)
            yield return 90f + 10f * _planarMap.QuadTreeZNormal.ProcessedNodeCount / _planarMap.QuadTreeZNormal.TotalNodeCount;
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
    _planarMap.ResetStatus();
    yield return 0;

    for (int ii = 0; ii < _blockInfoArray.Count; ++ii)
    {
        BlockInfo blockInfo = _blockInfoArray[ii];
        _planarMap.UpdateStatus(blockInfo);

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

        surf.ScriptBackgroundColor = _bgColor;

        Vector2 screenCenter = surf.TextureSize * 0.5f;
        Vector2 halfSurface = surf.SurfaceSize * 0.5f;

        if (!_blockInfoStored)
        {
            _loadingScreen.Draw(surf, _blockStorageStateMachine.Current * 0.01f, $"{_storageStageStr} ({Math.Ceiling(_blockStorageStateMachine.Current)}%)");
            yield return 100f * jj / _textSurfaces.Count;
            continue;
        }

        // Adding or removing this sprite will force an entire resync of the sprite cache
        if (_drawRefreshSprite)
        {
            surf.Add(new MySprite());
        }

        foreach (var view in config.Views)
        {
            // TODO: Fix the percentages

            NormalAxis normal = view.Normal & NormalAxis.Axes;
            float rotation = view.RotationRad;
            bool autoscale = !view.Scale.HasValue;
            float scale = view.Scale.HasValue ? view.Scale.Value : 1;
            bool invert = (view.Normal & NormalAxis.Negative) != 0;
            Vector2 position = screenCenter + view.RelativePosition * halfSurface;

            Matrix rotationMatrix = CreateRotMatrix(rotation);

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
                quadTree.AddSpriteFromQuadTreeLeaf(surf, normal, invert, scale, rotation, _planarMap, leaf, ref position, ref rotationMatrix);

                if ((ii + 1) % SPRITES_TO_CREATE_PER_TICK == 0)
                {
                    yield return 100f * (jj + (float)(ii + 1) / (quadTree.FinishedNodes.Count + statusSpriteCreators.Count)) / _textSurfaces.Count;
                }
            }

            for (int ii = 0; ii < statusSpriteCreators.Count; ++ii)
            {
                statusSpriteCreators[ii].CreateSprite(surf, normal, scale, rotation, invert, ref position, ref rotationMatrix);

                if ((ii + 1) % SPRITES_TO_CREATE_PER_TICK == 0)
                {
                    yield return 100f * (jj + (float)(ii + 1 + quadTree.FinishedNodes.Count) / (quadTree.FinishedNodes.Count + statusSpriteCreators.Count)) / _textSurfaces.Count;
                }
            }

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

        }

        _legend.GenerateSprites(surf, screenCenter + config.LegendRelativePos * halfSurface, config.LegendScale);

        // Draw max of one surface per tick
        surf.Draw();
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

    public LegendItem(string name, Color color)
    {
        Name = name;
        Color = color;
    }
}

public class Legend
{
    Dictionary<string, LegendItem> _legendItems = new Dictionary<string, LegendItem>();

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

    public void GenerateSprites(ISpriteSurface surf, Vector2 topLeftPos, float scale)
    {
        Vector2 textVerticalOffset = TEXT_OFFSET_BASE * _legendFontSize * scale;
        Vector2 legendPosition = topLeftPos + Vector2.One * (_legendSquareSize * scale * 0.5f + 4f);
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

    public void AddLegendItem(string key, string name, Color color)
    {
        _legendItems[key] = new LegendItem(name, color);
    }

    public void UpdateLegendItemColor(string key, Color color)
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

    public BlockStatus Status
    {
        get
        {
            BlockStatus status = BlockStatus.Nominal;

            if (!_grid.CubeExists(GridPosition))
            {
                status = BlockStatus.Missing;
            }
            else if (_cube != null && !_cube.IsFunctional)
            {
                status = BlockStatus.Damaged;
            }

            return status;
        }
    }

    public BlockMask BlockMask;

    IMyCubeGrid _grid;
    IMyCubeBlock _cube;

    public BlockInfo(ref Vector3I gridPosition, IMyCubeGrid grid)
    {
        GridPosition = gridPosition;
        _grid = grid;

        var slim = grid.GetCubeBlock(gridPosition);
        if (slim == null)
        {
            return;
        }

        _cube = slim.FatBlock;
        if (_cube == null)
        {
            return;
        }

        if (_cube as IMyPowerProducer != null)
            BlockMask |= BlockMask.Power;

        if (_cube as IMyGyro != null)
            BlockMask |= BlockMask.Gyro;

        if (_cube as IMyThrust != null)
            BlockMask |= BlockMask.Thrust;

        if (_cube as IMyUserControllableGun != null)
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
        string spriteName;
        if (_planarMapPtr.DrawBlockMaskSprite(normal, ref _gridPosition, out functionalSpriteColor, out spriteName) && functionalSpriteColor.A > 0)
        {
            surf.Add(new MySprite(SpriteType.TEXTURE, spriteName, screenCenter + rotatedFromCenterPlanar * scale, Vector2.One * scale, functionalSpriteColor, rotation: rotation * sign));
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
    readonly Swappable<BlockStatus[,]> _statusXNormal;
    readonly Swappable<BlockStatus[,]> _statusYNormal;
    readonly Swappable<BlockStatus[,]> _statusZNormal;
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

    public ConfigColor ColorMinDensity = new ConfigColor(INI_KEY_COLOR_MIN, new Color(10, 10, 10));
    public ConfigColor ColorMaxDensity = new ConfigColor(INI_KEY_COLOR_MAX, new Color(50, 50, 50));
    public ConfigColor ColorMissing = new ConfigColor(INI_KEY_COLOR_MISSING, new Color(100, 0, 0, 200));
    public ConfigColor ColorPower = new ConfigColor(INI_KEY_COLOR_POWER, new Color(0, 100, 0, 100));
    public ConfigColor ColorWeapon = new ConfigColor(INI_KEY_COLOR_WEAPON, new Color(100, 50, 0, 100));
    public ConfigColor ColorGyro = new ConfigColor(INI_KEY_COLOR_GYRO, new Color(100, 100, 0, 100));
    public ConfigColor ColorThrust = new ConfigColor(INI_KEY_COLOR_THRUST, new Color(0, 0, 100, 100));

    public PlanarMap(IMyCubeGrid grid)
    {
        _min = grid.Min;
        _max = grid.Max;
        _center = (Vector3)(_min + _max) * 0.5f;

        var diff = _max - _min;
        _densityXNormal = new int[diff.Y + 1, diff.Z + 1];
        _densityYNormal = new int[diff.Z + 1, diff.X + 1];
        _densityZNormal = new int[diff.X + 1, diff.Y + 1];

        _statusXNormal = new Swappable<BlockStatus[,]>(new BlockStatus[diff.Y + 1, diff.Z + 1], new BlockStatus[diff.Y + 1, diff.Z + 1]);
        _statusYNormal = new Swappable<BlockStatus[,]>(new BlockStatus[diff.Z + 1, diff.X + 1], new BlockStatus[diff.Z + 1, diff.X + 1]);
        _statusZNormal = new Swappable<BlockStatus[,]>(new BlockStatus[diff.X + 1, diff.Y + 1], new BlockStatus[diff.X + 1, diff.Y + 1]);

        _masksXNormal = new BlockMask[diff.Y + 1, diff.Z + 1];
        _masksYNormal = new BlockMask[diff.Z + 1, diff.X + 1];
        _masksZNormal = new BlockMask[diff.X + 1, diff.Y + 1];
    }

    public void CreateQuadTrees()
    {
        QuadTreeXNormal.Initialize(_densityXNormal, DISCRETE_DENSITY_STEPS);
        QuadTreeYNormal.Initialize(_densityYNormal, DISCRETE_DENSITY_STEPS);
        QuadTreeZNormal.Initialize(_densityZNormal, DISCRETE_DENSITY_STEPS);
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

        UpdateStatus(info);

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

    public void ResetStatus()
    {
        _statusXNormal.Swap();
        _statusYNormal.Swap();
        _statusZNormal.Swap();
        Array.Clear(_statusXNormal.Inactive, 0, _statusXNormal.Inactive.Length);
        Array.Clear(_statusYNormal.Inactive, 0, _statusYNormal.Inactive.Length);
        Array.Clear(_statusZNormal.Inactive, 0, _statusZNormal.Inactive.Length);
    }

    public void UpdateStatus(BlockInfo info)
    {
        var diff = info.GridPosition - _min;
        _statusXNormal.Active[diff.Y, diff.Z] |= info.Status;
        _statusYNormal.Active[diff.Z, diff.X] |= info.Status;
        _statusZNormal.Active[diff.X, diff.Y] |= info.Status;

        _statusXNormal.Inactive[diff.Y, diff.Z] |= info.Status;
        _statusYNormal.Inactive[diff.Z, diff.X] |= info.Status;
        _statusZNormal.Inactive[diff.X, diff.Y] |= info.Status;
    }

    public bool DrawBlockMaskSprite(NormalAxis normal, ref Vector3I blockPosition, out Color functionalSpriteColor, out string spriteName)
    {
        blockPosition = Vector3I.Max(_min, Vector3I.Min(_max, blockPosition));
        var diff = blockPosition - _min;

        BlockStatus status;
        BlockMask blockMasks;
        if (NormalAxis.X == normal)
        {
            blockMasks = _masksXNormal[diff.Y, diff.Z];
            status = _statusXNormal.Active[diff.Y, diff.Z];
        }
        else if (NormalAxis.Y == normal)
        {
            blockMasks = _masksYNormal[diff.Z, diff.X];
            status = _statusYNormal.Active[diff.Z, diff.X];
        }
        else
        {
            blockMasks = _masksZNormal[diff.X, diff.Y];
            status = _statusZNormal.Active[diff.X, diff.Y];
        }

        spriteName = "SquareSimple";
        if ((status & (BlockStatus.Missing | BlockStatus.Damaged)) != 0)
        {
            functionalSpriteColor = ColorMissing;
        }
        else if (ColorWeapon.Value.A > 0 && (blockMasks & BlockMask.Weapon) != 0)
        {
            functionalSpriteColor = ColorWeapon;
        }
        else if (ColorPower.Value.A > 0 && (blockMasks & BlockMask.Power) != 0)
        {
            functionalSpriteColor = ColorPower;
        }
        else if (ColorGyro.Value.A > 0 && (blockMasks & BlockMask.Gyro) != 0)
        {
            functionalSpriteColor = ColorGyro;
        }
        else if (ColorThrust.Value.A > 0 && (blockMasks & BlockMask.Thrust) != 0)
        {
            functionalSpriteColor = ColorThrust;
        }
        else
        {
            functionalSpriteColor = Color.Transparent;
        }

        return status == BlockStatus.Missing || status == BlockStatus.Damaged || blockMasks != BlockMask.None;
    }

    public Color GetColor(float lerpScale)
    {
        return Color.Lerp(ColorMinDensity, ColorMaxDensity, lerpScale);
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
        return Color.Lerp(ColorMinDensity, ColorMaxDensity, lerpScale);
    }
}
#endregion

class Swappable<T> where T : class
{
    public T Active
    {
        get
        {
            return _swap ? _inst1 : _inst2;
        }
    }

    public T Inactive
    {
        get
        {
            return _swap ? _inst2 : _inst1;
        }
    }

    bool _swap = false;
    readonly T _inst1;
    readonly T _inst2;


    public Swappable(T pri, T sec)
    {
        _inst1 = pri;
        _inst2 = sec;
    }

    public void Swap()
    {
        _swap = !_swap;
    }
}

#endregion

#region INCLUDES

/// <summary>
/// Class that tracks runtime history.
/// </summary>
public class RuntimeTracker
{
    public int Capacity { get; set; }
    public double Sensitivity { get; set; }
    public double MaxRuntime {get; private set;}
    public double MaxInstructions {get; private set;}
    public double AverageRuntime {get; private set;}
    public double AverageInstructions {get; private set;}
    public double LastRuntime {get; private set;}
    public double LastInstructions {get; private set;}
    
    readonly Queue<double> _runtimes = new Queue<double>();
    readonly Queue<double> _instructions = new Queue<double>();
    readonly int _instructionLimit;
    readonly Program _program;
    const double MS_PER_TICK = 16.6666;
    
    const string Format = "General Runtime Info\n"
            + "- Avg runtime: {0:n4} ms\n"
            + "- Last runtime: {1:n4} ms\n"
            + "- Max runtime: {2:n4} ms\n"
            + "- Avg instructions: {3:n2}\n"
            + "- Last instructions: {4:n0}\n"
            + "- Max instructions: {5:n0}\n"
            + "- Avg complexity: {6:0.000}%";

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
        return string.Format(
            Format,
            AverageRuntime,
            LastRuntime,
            MaxRuntime,
            AverageInstructions,
            LastInstructions,
            MaxInstructions,
            AverageInstructions / _instructionLimit);
    }
}

#region Scheduler
/// <summary>
/// Class for scheduling actions to occur at specific frequencies. Actions can be updated in parallel or in sequence (queued).
/// </summary>
public class Scheduler
{
    public double CurrentTimeSinceLastRun { get; private set; } = 0;
    public long CurrentTicksSinceLastRun { get; private set; } = 0;

    QueuedAction _currentlyQueuedAction = null;
    bool _firstRun = true;
    bool _inUpdate = false;

    readonly bool _ignoreFirstRun;
    readonly List<ScheduledAction> _actionsToAdd = new List<ScheduledAction>();
    readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
    readonly List<ScheduledAction> _actionsToDispose = new List<ScheduledAction>();
    readonly Queue<QueuedAction> _queuedActions = new Queue<QueuedAction>();
    readonly Program _program;

    public const long TicksPerSecond = 60;
    public const double TickDurationSeconds = 1.0 / TicksPerSecond;
    const long ClockTicksPerGameTick = 166666L;

    /// <summary>
    /// Constructs a scheduler object with timing based on the runtime of the input program.
    /// </summary>
    public Scheduler(Program program, bool ignoreFirstRun = false)
    {
        _program = program;
        _ignoreFirstRun = ignoreFirstRun;
    }

    /// <summary>
    /// Updates all ScheduledAcions in the schedule and the queue.
    /// </summary>
    public void Update()
    {
        _inUpdate = true;
        long deltaTicks = Math.Max(0, _program.Runtime.TimeSinceLastRun.Ticks / ClockTicksPerGameTick);

        if (_firstRun)
        {
            if (_ignoreFirstRun)
            {
                deltaTicks = 0;
            }
            _firstRun = false;
        }

        _actionsToDispose.Clear();
        foreach (ScheduledAction action in _scheduledActions)
        {
            CurrentTicksSinceLastRun = action.TicksSinceLastRun + deltaTicks;
            CurrentTimeSinceLastRun = action.TimeSinceLastRun + deltaTicks * TickDurationSeconds;
            action.Update(deltaTicks);
            if (action.JustRan && action.DisposeAfterRun)
            {
                _actionsToDispose.Add(action);
            }
        }

        if (_actionsToDispose.Count > 0)
        {
            _scheduledActions.RemoveAll((x) => _actionsToDispose.Contains(x));
        }

        if (_currentlyQueuedAction == null)
        {
            // If queue is not empty, populate current queued action
            if (_queuedActions.Count != 0)
                _currentlyQueuedAction = _queuedActions.Dequeue();
        }

        // If queued action is populated
        if (_currentlyQueuedAction != null)
        {
            _currentlyQueuedAction.Update(deltaTicks);
            if (_currentlyQueuedAction.JustRan)
            {
                if (!_currentlyQueuedAction.DisposeAfterRun)
                {
                    _queuedActions.Enqueue(_currentlyQueuedAction);
                }
                // Set the queued action to null for the next cycle
                _currentlyQueuedAction = null;
            }
        }
        _inUpdate = false;

        if (_actionsToAdd.Count > 0)
        {
            _scheduledActions.AddRange(_actionsToAdd);
            _actionsToAdd.Clear();
        }
    }

    /// <summary>
    /// Adds an Action to the schedule. All actions are updated each update call.
    /// </summary>
    public void AddScheduledAction(Action action, double updateFrequency, bool disposeAfterRun = false, double timeOffset = 0)
    {
        ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun, timeOffset);
        if (!_inUpdate)
            _scheduledActions.Add(scheduledAction);
        else
            _actionsToAdd.Add(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the schedule. All actions are updated each update call.
    /// </summary>
    public void AddScheduledAction(ScheduledAction scheduledAction)
    {
        if (!_inUpdate)
            _scheduledActions.Add(scheduledAction);
        else
            _actionsToAdd.Add(scheduledAction);
    }

    /// <summary>
    /// Adds an Action to the queue. Queue is FIFO.
    /// </summary>
    public void AddQueuedAction(Action action, double updateInterval, bool removeAfterRun = false)
    {
        if (updateInterval <= 0)
        {
            updateInterval = 0.001; // avoids divide by zero
        }
        QueuedAction scheduledAction = new QueuedAction(action, updateInterval, removeAfterRun);
        _queuedActions.Enqueue(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the queue. Queue is FIFO.
    /// </summary>
    public void AddQueuedAction(QueuedAction scheduledAction)
    {
        _queuedActions.Enqueue(scheduledAction);
    }
}

public class QueuedAction : ScheduledAction
{
    public QueuedAction(Action action, double runInterval, bool removeAfterRun = false)
        : base(action, 1.0 / runInterval, removeAfterRun: removeAfterRun, timeOffset: 0)
    { }
}

public class ScheduledAction
{
    public bool JustRan { get; private set; } = false;
    public bool DisposeAfterRun { get; private set; } = false;
    public double TimeSinceLastRun { get { return TicksSinceLastRun * Scheduler.TickDurationSeconds; } }
    public long TicksSinceLastRun { get; private set; } = 0;
    public double RunInterval
    {
        get
        {
            return RunIntervalTicks * Scheduler.TickDurationSeconds;
        }
        set
        {
            RunIntervalTicks = (long)Math.Round(value * Scheduler.TicksPerSecond);
        }
    }
    public long RunIntervalTicks
    {
        get
        {
            return _runIntervalTicks;
        }
        set
        {
            if (value == _runIntervalTicks)
                return;

            _runIntervalTicks = value < 0 ? 0 : value;
            _runFrequency = value == 0 ? double.MaxValue : Scheduler.TicksPerSecond / _runIntervalTicks;
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
                RunIntervalTicks = long.MaxValue;
            else
                RunIntervalTicks = (long)Math.Round(Scheduler.TicksPerSecond / value);
        }
    }

    long _runIntervalTicks;
    double _runFrequency;
    readonly Action _action;

    /// <summary>
    /// Class for scheduling an action to occur at a specified frequency (in Hz).
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <param name="runFrequency">How often to run in Hz</param>
    public ScheduledAction(Action action, double runFrequency, bool removeAfterRun = false, double timeOffset = 0)
    {
        _action = action;
        RunFrequency = runFrequency; // Implicitly sets RunInterval
        DisposeAfterRun = removeAfterRun;
        TicksSinceLastRun = (long)Math.Round(timeOffset * Scheduler.TicksPerSecond);
    }

    public void Update(long deltaTicks)
    {
        TicksSinceLastRun += deltaTicks;

        if (TicksSinceLastRun >= RunIntervalTicks)
        {
            _action.Invoke();
            TicksSinceLastRun = 0;

            JustRan = true;
        }
        else
        {
            JustRan = false;
        }
    }
}
#endregion

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
            var cube = slim.FatBlock;
            var surf = cube as IMyTextSurface;
            if (surf != null)
            {
                CubeBlock = cube;
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
            return _rotationAngle ?? 0f;
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

    Vector2 GetRotatedSize(Vector2 size, float angleRad)
    {
        if (Math.Abs(angleRad) < 1e-3)
        {
            return size;
        }

        float cos = Math.Abs(MyMath.FastCos(angleRad));
        float sin = Math.Abs(MyMath.FastSin(angleRad));
        
        Vector2 rotated = Vector2.Zero;
        rotated.X = size.X * cos + size.Y * sin;
        rotated.Y = size.X * sin + size.Y * cos;
        return rotated;
    }

    public void Add(MySprite sprite)
    {
        Vector2 pos = sprite.Position ?? TextureSize * 0.5f;
        bool isText = sprite.Type == SpriteType.TEXT;
        int lowerCol, upperCol, lowerRow, upperRow;
        if (sprite.Type == SpriteType.CLIP_RECT)
        {
            lowerCol = lowerRow = 0;
            upperCol = Cols - 1;
            upperRow = Rows - 1;
        }
        else
        {
            Vector2 spriteSize;
            if (sprite.Size != null)
            {
                spriteSize = sprite.Size.Value;
            }
            else if (isText)
            {
                _stringBuilder.Clear();
                _stringBuilder.Append(sprite.Data);
                spriteSize = _anchor.MeasureStringInPixels(_stringBuilder, sprite.FontId, sprite.RotationOrScale);
            }
            else
            {
                spriteSize = TextureSize;
                sprite.Size = spriteSize;
            }

            Vector2 fromCenter = pos - (TextureSize * 0.5f);
            Vector2 fromCenterRotated = RotateToBaseOrientation(fromCenter, RotationRads);
            Vector2 basePos = TextureSizeNoRotation * 0.5f + fromCenterRotated;

            // Determine span of the sprite used for culling
            Vector2 rotatedSize = (sprite.Type == SpriteType.TEXTURE ? GetRotatedSize(spriteSize, sprite.RotationOrScale) : spriteSize);
            Vector2 topLeft, bottomRight;
            switch (sprite.Alignment)
            {
                case TextAlignment.LEFT:
                    if (isText)
                    {
                        topLeft = Vector2.Zero;
                        bottomRight = rotatedSize;
                    }
                    else
                    {
                        topLeft = new Vector2(0f, 0.5f) * rotatedSize;
                        bottomRight = new Vector2(1f, 0.5f) * rotatedSize;
                    }
                    break;
                case TextAlignment.RIGHT:
                    if (isText)
                    {
                        topLeft = new Vector2(1f, 0f) * rotatedSize;
                        bottomRight = new Vector2(0f, 1f) * rotatedSize;
                    }
                    else
                    {
                        topLeft = new Vector2(1f, 0.5f) * rotatedSize;
                        bottomRight = new Vector2(0f, 0.5f) * rotatedSize;
                    }
                    break;
                
                default:
                case TextAlignment.CENTER:
                    if (isText)
                    {
                        topLeft = new Vector2(0.5f, 0f) * rotatedSize;
                        bottomRight = new Vector2(0.5f, 1f) * rotatedSize;
                    }
                    else
                    {
                        topLeft = bottomRight = 0.5f * rotatedSize;
                    }
                    break;
            }
            topLeft = RotateToBaseOrientation(topLeft, RotationRads);
            topLeft *= Vector2.SignNonZero(topLeft);
            bottomRight = RotateToBaseOrientation(bottomRight, RotationRads);
            bottomRight *= Vector2.SignNonZero(bottomRight);

            var lowerCoords = Vector2I.Floor((basePos - topLeft) / BasePanelSizeNoRotation);
            var upperCoords = Vector2I.Floor((basePos + bottomRight) / BasePanelSizeNoRotation);

            lowerCol = Math.Max(0, lowerCoords.X);
            upperCol = Math.Min(Cols - 1, upperCoords.X);
            lowerRow = Math.Max(0, lowerCoords.Y);
            upperRow = Math.Min(Rows - 1, upperCoords.Y);
        }

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

public interface IConfigValue
{
    void WriteToIni(ref MyIni ini, string section);
    bool ReadFromIni(ref MyIni ini, string section);
    bool Update(ref MyIni ini, string section);
    void Reset();
    string Name { get; set; }
    string Comment { get; set; }
}

public interface IConfigValue<T> : IConfigValue
{
    T Value { get; set; }
}

public abstract class ConfigValue<T> : IConfigValue<T>
{
    public string Name { get; set; }
    public string Comment { get; set; }
    protected T _value;
    public T Value
    {
        get { return _value; }
        set
        {
            _value = value;
            _skipRead = true;
        }
    }
    readonly T _defaultValue;
    bool _skipRead = false;

    public static implicit operator T(ConfigValue<T> cfg)
    {
        return cfg.Value;
    }

    public ConfigValue(string name, T defaultValue, string comment)
    {
        Name = name;
        _value = defaultValue;
        _defaultValue = defaultValue;
        Comment = comment;
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    public bool Update(ref MyIni ini, string section)
    {
        bool read = ReadFromIni(ref ini, section);
        WriteToIni(ref ini, section);
        return read;
    }

    public bool ReadFromIni(ref MyIni ini, string section)
    {
        if (_skipRead)
        {
            _skipRead = false;
            return true;
        }
        MyIniValue val = ini.Get(section, Name);
        bool read = !val.IsEmpty;
        if (read)
        {
            read = SetValue(ref val);
        }
        else
        {
            SetDefault();
        }
        return read;
    }

    public void WriteToIni(ref MyIni ini, string section)
    {
        ini.Set(section, Name, this.ToString());
        if (!string.IsNullOrWhiteSpace(Comment))
        {
            ini.SetComment(section, Name, Comment);
        }
        _skipRead = false;
    }

    public void Reset()
    {
        SetDefault();
        _skipRead = false;
    }

    protected abstract bool SetValue(ref MyIniValue val);

    protected virtual void SetDefault()
    {
        _value = _defaultValue;
    }
}

class ConfigSection
{
    public string Section { get; set; }
    public string Comment { get; set; }
    List<IConfigValue> _values = new List<IConfigValue>();

    public ConfigSection(string section, string comment = null)
    {
        Section = section;
        Comment = comment;
    }

    public void AddValue(IConfigValue value)
    {
        _values.Add(value);
    }

    public void AddValues(List<IConfigValue> values)
    {
        _values.AddRange(values);
    }

    public void AddValues(params IConfigValue[] values)
    {
        _values.AddRange(values);
    }

    void SetComment(ref MyIni ini)
    {
        if (!string.IsNullOrWhiteSpace(Comment))
        {
            ini.SetSectionComment(Section, Comment);
        }
    }

    public void ReadFromIni(ref MyIni ini)
    {    
        foreach (IConfigValue c in _values)
        {
            c.ReadFromIni(ref ini, Section);
        }
    }

    public void WriteToIni(ref MyIni ini)
    {    
        foreach (IConfigValue c in _values)
        {
            c.WriteToIni(ref ini, Section);
        }
        SetComment(ref ini);
    }

    public void Update(ref MyIni ini)
    {    
        foreach (IConfigValue c in _values)
        {
            c.Update(ref ini, Section);
        }
        SetComment(ref ini);
    }
}
public class ConfigString : ConfigValue<string>
{
    public ConfigString(string name, string value = "", string comment = null) : base(name, value, comment) { }
    protected override bool SetValue(ref MyIniValue val)
    {
        if (!val.TryGetString(out _value))
        {
            SetDefault();
            return false;
        }
        return true;
    }
}

public class ConfigBool : ConfigValue<bool>
{
    public ConfigBool(string name, bool value = false, string comment = null) : base(name, value, comment) { }
    protected override bool SetValue(ref MyIniValue val)
    {
        if (!val.TryGetBoolean(out _value))
        {
            SetDefault();
            return false;
        }
        return true;
    }
}

public class ConfigColor : ConfigValue<Color>
{
    public ConfigColor(string name, Color value = default(Color), string comment = null) : base(name, value, comment) { }
    public override string ToString()
    {
        return string.Format("{0}, {1}, {2}, {3}", Value.R, Value.G, Value.B, Value.A);
    }
    protected override bool SetValue(ref MyIniValue val)
    {
        string rgbString = val.ToString("");
        string[] rgbSplit = rgbString.Split(',');

        int r = 0, g = 0, b = 0, a = 0;
        if (rgbSplit.Length != 4 ||
            !int.TryParse(rgbSplit[0].Trim(), out r) ||
            !int.TryParse(rgbSplit[1].Trim(), out g) ||
            !int.TryParse(rgbSplit[2].Trim(), out b))
        {
            SetDefault();
            return false;
        }

        bool hasAlpha = int.TryParse(rgbSplit[3].Trim(), out a);
        if (!hasAlpha)
        {
            a = 255;
        }

        r = MathHelper.Clamp(r, 0, 255);
        g = MathHelper.Clamp(g, 0, 255);
        b = MathHelper.Clamp(b, 0, 255);
        a = MathHelper.Clamp(a, 0, 255);
        _value = new Color(r, g, b, a);
        return true;
    }
}

public class ConfigFloat : ConfigValue<float>
{
    public ConfigFloat(string name, float value = 0, string comment = null) : base(name, value, comment) { }
    protected override bool SetValue(ref MyIniValue val)
    {
        if (!val.TryGetSingle(out _value))
        {
            SetDefault();
            return false;
        }
        return true;
    }
}

public class ConfigInt : ConfigValue<int>
{
    public ConfigInt(string name, int value = 0, string comment = null) : base(name, value, comment) { }
    protected override bool SetValue(ref MyIniValue val)
    {
        if (!val.TryGetInt32(out _value))
        {
            SetDefault();
            return false;
        }
        return true;
    }
}

public class ConfigEnum<TEnum> : ConfigValue<TEnum> where TEnum : struct
{
    public ConfigEnum(string name, TEnum defaultValue = default(TEnum), string comment = null)
    : base (name, defaultValue, comment)
    {}

    protected override bool SetValue(ref MyIniValue val)
    {
        string enumerationStr;
        if (!val.TryGetString(out enumerationStr) ||
            !Enum.TryParse(enumerationStr, true, out _value) ||
            !Enum.IsDefined(typeof(TEnum), _value))
        {
            SetDefault();
            return false;
        }
        return true;
    }
}
public class ConfigDeprecated<T, ConfigImplementation> : IConfigValue where ConfigImplementation : IConfigValue<T>, IConfigValue
{
    public readonly ConfigImplementation Implementation;
    public Action<T> Callback;

    public string Name 
    { 
        get { return Implementation.Name; }
        set { Implementation.Name = value; }
    }

    public string Comment 
    { 
        get { return Implementation.Comment; } 
        set { Implementation.Comment = value; } 
    }

    public ConfigDeprecated(ConfigImplementation impl)
    {
        Implementation = impl;
    }

    public bool ReadFromIni(ref MyIni ini, string section)
    {
        bool read = Implementation.ReadFromIni(ref ini, section);
        if (read)
        {
            Callback?.Invoke(Implementation.Value);
        }
        return read;
    }

    public void WriteToIni(ref MyIni ini, string section)
    {
        ini.Delete(section, Implementation.Name);
    }

    public bool Update(ref MyIni ini, string section)
    {
        bool read = ReadFromIni(ref ini, section);
        WriteToIni(ref ini, section);
        return read;
    }

    public void Reset() {}
}

public class ConfigNullable<T, ConfigImplementation> : IConfigValue<T>, IConfigValue
    where ConfigImplementation : IConfigValue<T>, IConfigValue
    where T : struct
{
    public string Name 
    { 
        get { return Implementation.Name; }
        set { Implementation.Name = value; }
    }

    public string Comment 
    { 
        get { return Implementation.Comment; } 
        set { Implementation.Comment = value; } 
    }
    
    public string NullString;
    public T Value
    {
        get { return Implementation.Value; }
        set 
        { 
            Implementation.Value = value;
            HasValue = true;
            _skipRead = true;
        }
    }
    public readonly ConfigImplementation Implementation;
    public bool HasValue { get; private set; }
    bool _skipRead = false;

    public ConfigNullable(ConfigImplementation impl, string nullString = "none")
    {
        Implementation = impl;
        NullString = nullString;
        HasValue = false;
    }

    public void Reset()
    {
        HasValue = false;
        _skipRead = true;
    }

    public bool ReadFromIni(ref MyIni ini, string section)
    {
        if (_skipRead)
        {
            _skipRead = false;
            return true;
        }
        bool read = Implementation.ReadFromIni(ref ini, section);
        if (read)
        {
            HasValue = true;
        }
        else
        {
            HasValue = false;
        }
        return read;
    }

    public void WriteToIni(ref MyIni ini, string section)
    {
        Implementation.WriteToIni(ref ini, section);
        if (!HasValue)
        {
            ini.Set(section, Implementation.Name, NullString);
        }
    }

    public bool Update(ref MyIni ini, string section)
    {
        bool read = ReadFromIni(ref ini, section);
        WriteToIni(ref ini, section);
        return read;
    }

    public override string ToString()
    {
        return HasValue ? Value.ToString() : NullString;
    }
}

public class ConfigVector2 : ConfigValue<Vector2>
{
    public ConfigVector2(string name, Vector2 value = default(Vector2), string comment = null) : base(name, value, comment) { }
    protected override bool SetValue(ref MyIniValue val)
    {
        // Source formatting example: {X:2.75 Y:-14.4}
        string source = val.ToString("");
        int xIndex = source.IndexOf("X:");
        int yIndex = source.IndexOf("Y:");
        int closingBraceIndex = source.IndexOf("}");
        if (xIndex == -1 || yIndex == -1 || closingBraceIndex == -1)
        {
            SetDefault();
            return false;
        }

        Vector2 vec = default(Vector2);
        string xStr = source.Substring(xIndex + 2, yIndex - (xIndex + 2));
        if (!float.TryParse(xStr, out vec.X))
        {
            SetDefault();
            return false;
        }
        string yStr = source.Substring(yIndex + 2, closingBraceIndex - (yIndex + 2));
        if (!float.TryParse(yStr, out vec.Y))
        {
            SetDefault();
            return false;
        }
        _value = vec;
        return true;
    }
}
#endregion
