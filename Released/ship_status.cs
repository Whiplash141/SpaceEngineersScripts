
#region SIMPL
/*
/ //// / Whip's Ship Integrity Monitoring Program (Lite) / //// /






















============================================
    DO NOT EDIT VARIABLES IN THE SCRIPT
            USE THE CUSTOM DATA!
============================================
*/

const string VERSION = "1.13.2";
const string DATE = "2024/04/13";

const int BLOCKS_TO_STORE_PER_TICK = 500;
const int BLOCKS_TO_CHECK_PER_TICK = 100;
const int SPRITES_TO_CREATE_PER_TICK = 250;
const int DISCRETE_DENSITY_STEPS = 4;

const string INI_SCREEN_ID_TEMPLATE = " - Screen {0}";
const string INI_SECTION_LEGEND = "SIMPL - Legend Config{0}";
const string INI_SECTION_TEXT_CONFIG_TEMPLATE = "SIMPL - Display Config{0} - View {1}";
const string INI_SECTION_TEXT_CONFIG_COMPAT = "SIMPL - Display Config{0}";
const string INI_SECTION_TEXT_SURF = "SIMPL - Text Surface Config";
const string INI_KEY_TEXT_SURF_TEMPLATE = "Show on screen {0}";
const string INI_KEY_NUM_VIEWS = "Number of views";
const string INI_KEY_NUM_VIEWS_TEMPLATE = "Number of views for screen {0}";


public class GeneralSection : ConfigSection
{
    public ConfigString TextSurfaceGroupName = new ConfigString("Group name", "SIMPL");
    public ConfigBool Autoscan = new ConfigBool("Auto scan", true);

    public GeneralSection() : base("SIMPL - General Config")
    {
        AddValues(TextSurfaceGroupName, Autoscan);
    }
}

public class ColorSection : ConfigSection
{
    public ConfigColor MinDensityColor = new ConfigColor("Min block density", new Color(10, 10, 10));
    public ConfigColor MaxDensityColor = new ConfigColor("Max block density", new Color(50, 50, 50));
    public ConfigColor DamageColor = new ConfigColor("Missing block", new Color(100, 0, 0, 200));
    public ConfigColor BackgroundColor = new ConfigColor("Background", new Color(0, 0, 0));

    const string IniCommentColor = " Colors are in the format: Red, Green, Blue, Alpha";

    public ColorSection() : base("SIMPL - Colors", IniCommentColor)
    {
        AddValues(MinDensityColor, MaxDensityColor, DamageColor, BackgroundColor);
    }
}

public class LegendSection : ConfigSection
{
    public ConfigFloat LegendScale = new ConfigFloat("Legend Scale", 1f);
    public ConfigVector2 LegendPosition = new ConfigVector2("Position", new Vector2(-1f, -1f), " Elements should range from -1 to 1 where 0 indicates centered");

    public LegendSection() : base("")
    {
        AddValues(LegendScale, LegendPosition);
    }
}

public class DisplaySection : ConfigSection
{
    public ConfigEnum<NormalAxis> _normal = new ConfigEnum<NormalAxis>("View axis", NormalAxis.X, " View axis values: X, Y, Z, NegativeX, NegativeY, NegativeZ");
    public ConfigFloat _rotation = new ConfigFloat("Rotation (deg)", 0f);
    public ConfigNullable<float> _screenScale = new ConfigNullable<float>(new ConfigFloat("Scale"), "auto");
    public ConfigVector2 _viewPosition = new ConfigVector2("Position", new Vector2(0, 0), " Elements should range from -1 to 1 where 0 indicates centered");

    protected void RegisterValues()
    {
        AddValues(_normal, _rotation, _screenScale, _viewPosition);
    }

    public DisplaySection() : base("")
    {
        RegisterValues();
    }

    protected struct Deferred { }
    protected static readonly Deferred kDeferred = new Deferred();

    protected DisplaySection(Deferred _) : base("") {}
}

public class CompatDisplaySection : DisplaySection
{
    public ConfigDeprecated<bool> AutoscaleCompat = new ConfigDeprecated<bool>(new ConfigBool("Autoscale layout", true));
    public ConfigDeprecated<float> ScreenScaleCompat = new ConfigDeprecated<float>(new ConfigFloat("Manual layout scale", 1f));
    public ConfigDeprecated<NormalAxis> NormalCompat = new ConfigDeprecated<NormalAxis>(new ConfigEnum<NormalAxis>("Normal axis", NormalAxis.X));
    public ConfigDeprecated<bool> InvertCompat = new ConfigDeprecated<bool>(new ConfigBool("Flip horizontally", false));

    public CompatDisplaySection() : base(kDeferred) 
    {
        AddValues(AutoscaleCompat, ScreenScaleCompat, NormalCompat, InvertCompat);
        RegisterValues();
    }
}

public class MultiscreenSection : ConfigSection
{
    public ConfigInt Rows = new ConfigInt("Screen rows", 1);
    public ConfigInt Cols = new ConfigInt("Screen cols", 1);

    const string MultiScreenTemplate = "{0} - Multiscreen Config";

    public MultiscreenSection(string scriptName) : base(string.Format(MultiScreenTemplate, scriptName))
    {
        AddValues(Rows, Cols);
    }
}

public class ConfigDefinitionIdList : ConfigValue<List<MyDefinitionId>>
{
    StringBuilder _buffer = new StringBuilder();

    public ConfigDefinitionIdList(string name, List<MyDefinitionId> defs = null, string comment = null) : base(name, defs ?? new List<MyDefinitionId>(), comment)
    { }

    protected override void InitializeValue()
    {
        _value = new List<MyDefinitionId>();
    }

    protected override void SetDefault()
    {
        _value.Clear();
        if (DefaultValue != null)
        {
            _value.AddRange(DefaultValue);
        }
    }

    protected override bool SetValue(ref MyIniValue pVal)
    {
        bool read = false;
        _value.Clear();
        string[] definitionNames = pVal.ToString().Split(',');
        foreach (var name in definitionNames)
        {
            MyDefinitionId def;
            bool parsed = MyDefinitionId.TryParse(name, out def);
            if (parsed)
            {
                _value.Add(def);
            }
            read |= parsed;
        }
        return read;
    }

    public override string ToString()
    {
        _buffer.Clear();
        for (int ii = 0; ii < _value.Count; ++ii)
        {
            _buffer.Append(_value[ii].TypeId.ToString());
            if (ii < _value.Count - 1)
            {
                _buffer.Append(",");
            }
        }
        return _buffer.ToString();
    }
}

public class ConfigBlockTypeLegendItemList : ConfigValue<List<BlockTypeLegendItem>>
{
    
    class LegendItemSection : ConfigSection
    {
        public ConfigColor ItemColor = new ConfigColor("Color", Color.Transparent);
        public ConfigEnum<BlockType> ItemBlockType = new ConfigEnum<BlockType>("Block type", BlockType.None);
        public ConfigDefinitionIdList ItemBlockDefinitions = new ConfigDefinitionIdList("Block definitions", new List<MyDefinitionId>());

        public LegendItemSection() : base("")
        {
            AddValues(ItemColor, ItemBlockType, ItemBlockDefinitions);
        }
    }

    MyIni _ini = new MyIni();
    List<string> _itemNames = new List<string>();
    LegendItemSection _legendItem = new LegendItemSection();
    StringBuilder _buffer = new StringBuilder();

    public ConfigBlockTypeLegendItemList(string name, List<BlockTypeLegendItem> value = null, string section = null) : base(name, value ?? new List<BlockTypeLegendItem>(), section)
    { }

    protected override void InitializeValue()
    {
        _value = new List<BlockTypeLegendItem>();
    }

    protected override void SetDefault()
    {
        _value.Clear();

        if (DefaultValue == null)
        {
            return;
        }

        foreach (var item in DefaultValue)
        {
            _value.Add(item.Clone());
        }    
    }

    protected override bool SetValue(ref MyIniValue pVal)
    {
        _ini.Clear();
        _ini.TryParse(pVal.ToString());

        _itemNames.Clear();
        _ini.GetSections(_itemNames);

        _value.Clear();
        if (_itemNames.Count == 0)
        {
            SetDefault();
            return false;
        }

        foreach (var categoryName in _itemNames)
        {
            _legendItem.Section = categoryName;
            _legendItem.Update(_ini);

            var category = new BlockTypeLegendItem(categoryName, _legendItem.ItemColor, _legendItem.ItemBlockType, _legendItem.ItemBlockDefinitions.Value);

            _value.Add(category);
        }

        return true;
    }

    public override string ToString()
    {
        _buffer.Clear();
        for (int ii = 0; ii < _value.Count; ++ii)
        {
            var item = _value[ii];
            _legendItem.Section = item.Name;
            _legendItem.ItemColor.Value = item.Color;
            _legendItem.ItemBlockType.Value = item.BlockType;
            item.GetDefinitions(_legendItem.ItemBlockDefinitions.Value);

            _ini.Clear();
            _legendItem.WriteToIni(_ini);

            _buffer.Append(_ini.ToString());
            if (ii + 1 < _value.Count)
            {
                _buffer.Append('\n');
            }
        }
        return _buffer.ToString();
    }
}

public class LegendCategoriesSection : ConfigSection
{
    static readonly List<BlockTypeLegendItem> _defaultLegendCats = new List<BlockTypeLegendItem>()
    {
        new BlockTypeLegendItem("Weapons", new Color(100, 50, 0, 100), BlockType.Weapons, null),
        new BlockTypeLegendItem("Power", new Color(0, 100, 0, 100), BlockType.Power, null),
        new BlockTypeLegendItem("Gyros", new Color(100, 100, 0, 100), BlockType.Gyros, null),
        new BlockTypeLegendItem("Thrust", new Color(0, 0, 100, 100), BlockType.Thrust, null)
    };

    const string Key = "Legend categories";
    static readonly string LegendComment =
        "\n Every line *must* start with the pipe (|) symbol." +
        "\n To create a new legend category, put the category name in brackets and" +
        "\n add the 'Color', 'Block type', and 'Block definitions' keys as described" +
        "\n below. The order in which you define your custom categories " +
        "\n determines its draw priority. The first legend category has the highest" +
        "\n priority and the last has the lowest priority." +
        "\n" +
        "\n Color" +
        "\n   - Specifies the color associated with this category" +
        "\n   - The format is R, G, B, A" +
        "\n   - 255, 255, 255, 255 is white and 0, 0, 0, 0 is transparent" +
        "\n" +
        "\n Block type" +
        "\n   - Specifies the block type to associate with this legend category" +
        "\n   - Valid values: None, Weapons, Thrust, Gyros, Power, Cargo, Tools," +
        "\n     Doors, or Functional" +
        "\n" +
        "\n Block definitions" +
        "\n   - Can contain one or more comma-separated block MyObjectBuilder" +
        "\n     type definitions" +
        "\n   - For a list of all block tyoe definitions in the vanilla game, see:" +
        "\n     https://github.com/malware-dev/MDK-SE/wiki/Type-Definition-Listing#blocks" +
        "\n   - You can also use this to specify type definitions that are added" +
        "\n     by mods" +
        "\n   - Leave this key blank if you do not wish to use it" +
        "\n" +
        "\n Example:" +
        $"\n   {Key}=" +
        "\n   |[Cargo]" +
        "\n   |Color=0,100,100,100" +
        "\n   |Block type=Cargo" +
        "\n   |Block definitions=" +
        "\n   |" +
        "\n   |[Sample text]" +
        "\n   |Color=100, 0, 50, 100" +
        "\n   |Block type=None" +
        "\n   |Block definitions=MyObjectBuilder_Assembler/LargeAssembler,MyObjectBuilder_Assembler/BasicAssembler,MyObjectBuilder_Refinery/Blast Furnace" +
        "\n\n";

    public ConfigBlockTypeLegendItemList LegendCategories = new ConfigBlockTypeLegendItemList(Key, _defaultLegendCats, LegendComment);

    public LegendCategoriesSection() : base("SIMPL - Legend Categories")
    {        
        AddValue(LegendCategories);
    }
}

public GeneralSection GeneralConfig = new GeneralSection();
public ColorSection ColorConfig = new ColorSection();
public LegendSection LegendConfig = new LegendSection();
public DisplaySection DisplayConfig = new DisplaySection();
public CompatDisplaySection CompatDisplayConfig = new CompatDisplaySection();
public MultiscreenSection MultiscreenConfig = new MultiscreenSection("SIMPL");
public LegendCategoriesSection LegendCategoriesConfig = new LegendCategoriesSection();

void ConfigureIni()
{
    CompatDisplayConfig.ScreenScaleCompat.Callback = (scale) => {
        if (!CompatDisplayConfig.AutoscaleCompat.Value)
        {
            DisplayConfig._screenScale.Value = scale;
        }
    };

    CompatDisplayConfig.NormalCompat.Callback = (normal) =>
    {
        DisplayConfig._normal.Value = normal;
    };

    CompatDisplayConfig.InvertCompat.Callback = (invert) =>
    {
        if (invert)
        {
            DisplayConfig._normal.Value |= NormalAxis.Negative;
        }
    };
}

float _textSize = 0.5f;

List<TextSurfaceConfig> _textSurfaces = new List<TextSurfaceConfig>();
List<BlockInfo> _blockInfoArray = new List<BlockInfo>();
PlanarMap _planarMap;
Scheduler _scheduler;
RuntimeTracker _runtimeTracker;
public Legend LegendInstance;
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
public enum BlockStatus { Nominal = 0, Damaged = 1, Missing = 2 };
public enum BlockType
{
    None = 0,
    Weapons = 1 << 0,
    Thrust = 1 << 1,
    Gyros = 1 << 2,
    Power = 1 << 3,
    Cargo = 1 << 4,
    Tools = 1 << 5,
    Functional = 1 << 6,
    Doors = 1 << 7,
}

public static BlockType GetBlockType<T>(T block) where T : IMyCubeBlock
{
    BlockType blockType = BlockType.None;
    if (block is IMyUserControllableGun)
    {
        blockType |= BlockType.Weapons;
    }
    if (block is IMyThrust)
    {
        blockType |= BlockType.Thrust;
    }
    if (block is IMyGyro)
    {
        blockType |= BlockType.Gyros;
    }
    if (block is IMyPowerProducer)
    {
        blockType |= BlockType.Power;
    }
    if (block is IMyCargoContainer)
    {
        blockType |= BlockType.Cargo;
    }
    if (block is IMyShipToolBase)
    {
        blockType |= BlockType.Tools;
    }
    if (block is IMyFunctionalBlock)
    {
        blockType |= BlockType.Functional;
    }
    if (block is IMyDoor)
    {
        blockType |= BlockType.Doors;
    }
    return blockType;
}

Program()
{
    OldEcho = Echo;
    Echo = NewEcho;

    _planarMap = new PlanarMap(this, Me.CubeGrid);
    ConfigureIni();

    InitializeGridBlockStorage();

    _runtimeTracker = new RuntimeTracker(this, 600);
    _scheduler = new Scheduler(this);
    _scheduler.AddScheduledAction(TryStartSpriteDraw, 0.5);
    _scheduler.AddScheduledAction(HandleStateMachines, 60);
    _scheduler.AddScheduledAction(WriteDetailedInfo, 1);

    _forceDrawTimeout = new ScheduledAction(() => _allowForceDraw = true, 1.0 / 30.0, true);

    LegendInstance = new Legend(_textSize);

    GetScreens();

    Runtime.UpdateFrequency |= UpdateFrequency.Update10;
}

public Action<string> OldEcho;

public void NewEcho(string text)
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
    OldEcho(output);
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
            if (!GeneralConfig.Autoscan)
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
        DisplayConfig.Section = displaySection;

        if (ii == 1)
        {
            string compatName = string.Format(INI_SECTION_TEXT_CONFIG_COMPAT, surfName);

            if (_ini.ContainsSection(compatName))
            {
                LegendConfig.LegendScale.ReadFromIni(_ini, compatName);
                CompatDisplayConfig.Section = compatName;
                CompatDisplayConfig.ReadFromIni(_ini);

                _ini.DeleteSection(compatName);

                LegendConfig.LegendScale.WriteToIni(_ini, legendSection);
                CompatDisplayConfig.Section = displaySection;
                CompatDisplayConfig.WriteToIni(_ini);
            }
        }

        LegendConfig.Section = legendSection;
        DisplayConfig.Update(_ini);

        config.AddView(DisplayConfig._normal, DisplayConfig._rotation, DisplayConfig._screenScale.HasValue ? DisplayConfig._screenScale.Value : (float?)null, DisplayConfig._viewPosition);
    }

    LegendConfig.Update(_ini);

    config.LegendScale = LegendConfig.LegendScale;
    config.LegendRelativePos = LegendConfig.LegendPosition;
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
            bool multiscreen = _ini.ContainsSection(MultiscreenConfig.Section);
            if (multiscreen)
            {
                MultiscreenConfig.Update(_ini); // TODO: clamp
            }

            ISpriteSurface surf;
            if (multiscreen && (MultiscreenConfig.Rows > 1 || MultiscreenConfig.Cols > 1))
            {
                surf = new MultiScreenSpriteSurface(tp, MultiscreenConfig.Rows, MultiscreenConfig.Cols, this);
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

    GeneralConfig.Update(_ini);
    ColorConfig.Update(_ini);
    LegendCategoriesConfig.Update(_ini);

    LegendInstance.DamageCategory.Color = ColorConfig.DamageColor;
    LegendInstance.BlockCategories.Clear();
    LegendInstance.BlockCategories.AddRange(LegendCategoriesConfig.LegendCategories.Value);

    if (!parsed && !string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

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

    var group = GridTerminalSystem.GetBlockGroupWithName(GeneralConfig.TextSurfaceGroupName);
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
            BlockInfo blockInfo = new BlockInfo(this, ref pos, Me.CubeGrid);
            _blockInfoArray.Add(blockInfo);
            _planarMap.StoreBlockInfo(blockInfo);

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
        if (commanded || GeneralConfig.Autoscan)
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
    _spriteDrawStateMachine?.Dispose();
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

        surf.ScriptBackgroundColor = ColorConfig.BackgroundColor;

        Vector2 screenCenter = surf.TextureSize * 0.5f;
        Vector2 halfSurface = surf.SurfaceSize * 0.5f;

        if (!_blockInfoStored)
        {
            _loadingScreen.Draw(surf, _blockStorageStateMachine.Current * 0.01f, $"{_storageStageStr} ({Math.Ceiling(_blockStorageStateMachine.Current)}%)");
            yield return 100f * jj / _textSurfaces.Count;
            continue;
        }

        if (_drawRefreshSprite)
        {
            surf.Add(new MySprite());
        }

        foreach (var view in config.Views)
        {

            NormalAxis normal = view.Normal & NormalAxis.Axes;
            float rotation = view.RotationRad;
            bool autoscale = !view.Scale.HasValue;
            float scale = view.Scale ?? 1;
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
                quadTree.AddSpriteFromQuadTreeLeaf(surf, invert, scale, rotation, _planarMap, leaf, ref position, ref rotationMatrix);

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

        LegendInstance.GenerateSprites(surf, screenCenter + config.LegendRelativePos * halfSurface, config.LegendScale);

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

        MySprite background = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: BackgroundColor);
        surf.Add(background);

        MySprite title = MySprite.CreateText(_title, "Debug", TextColor, TitleSize * scale, TextAlignment.CENTER);
        title.Position = screenCenter + TitleLocation * scale;
        surf.Add(title);

        MySprite subtitle = MySprite.CreateText(_subtitle, "Debug", TextColor, SubtitleSize * scale, TextAlignment.CENTER);
        subtitle.Position = screenCenter + SubtitleLocation * scale;
        surf.Add(subtitle);

        Vector2 loadingBarSize = scale * LoadingBarSize;
        MySprite barBackground = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: LoadingBarBackgroundColor, size: loadingBarSize);
        barBackground.Position = screenCenter + LoadingBarLocation * scale;
        surf.Add(barBackground);

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

    public void AddSpriteFromQuadTreeLeaf(ISpriteSurface surf, bool invert, float scale, float rotation, PlanarMap _planarMapPtr, QuadTreeLeaf leaf, ref Vector2 screenCenter, ref Matrix rotationMatrix)
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

public abstract class LegendItem
{
    public string Name;
    public Color Color;

    public LegendItem(string name, Color color)
    {
        Name = name;
        Color = color;
    }
}

public class DamageLegendItem : LegendItem
{
    public DamageLegendItem() : base("Damage", Color.Black) { }
}

public class BlockTypeLegendItem : LegendItem
{
    List<MyDefinitionId> _definitions = new List<MyDefinitionId>();
    
    BlockType _blockType = BlockType.None;
    public BlockType BlockType => _blockType;

    public BlockTypeLegendItem(string name, Color color, BlockType blockType, List<MyDefinitionId> list) : base(name, color)
    {
        _blockType = blockType;
        if (list != null)
        {
            _definitions.AddRange(list);
        }
    }

    public int DefinitionCount
    {
        get { return _definitions.Count; }
    }

    public void AddDefinition(MyDefinitionId def)
    {
        _definitions.Add(def);
    }

    public void GetDefinitions(List<MyDefinitionId> list)
    {
        list?.Clear();
        list?.AddRange(_definitions);
    }

    public bool Matches(IMyCubeBlock block)
    {
        if ((GetBlockType(block) & _blockType) != 0)
        {
            return true;
        }

        foreach (var def in _definitions)
        {
            if (def.TypeId == block.BlockDefinition.TypeId)
            {
                return true;
            }
        }
        return false;
    }

    public BlockTypeLegendItem Clone()
    {
        var clone = new BlockTypeLegendItem(Name, Color, _blockType, _definitions);
        return clone;
    }
}

public class Legend
{
    public DamageLegendItem DamageCategory = new DamageLegendItem();
    public List<BlockTypeLegendItem> BlockCategories = new List<BlockTypeLegendItem>();

    Color _textColor = new Color(100, 100, 100); // TODO: Make configurable
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

        DrawItem(surf, DamageCategory, ref legendPosition, textVerticalOffset, scale);
        foreach (var item in BlockCategories)
        {
            DrawItem(surf, item, ref legendPosition, textVerticalOffset, scale);
        }
    }

    void DrawItem(ISpriteSurface surf, LegendItem item, ref Vector2 legendPosition, Vector2 textVerticalOffset, float scale)
    {
        if (item.Color.A == 0)
            return; // TODO: Remove?

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

    public int? BlockColorIndex = null; // indicates no special color

    IMyCubeGrid _grid;
    IMyCubeBlock _cube;

    public BlockInfo(Program program, ref Vector3I gridPosition, IMyCubeGrid grid)
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

        for (int ii = 0; ii < program.LegendInstance.BlockCategories.Count; ++ii)
        {
            if (program.LegendInstance.BlockCategories[ii].Matches(_cube))
            {
                BlockColorIndex = ii;
                return;
            }
        }
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
    readonly int?[,] _colorIndexXNormal;
    readonly int?[,] _colorIndexYNormal;
    readonly int?[,] _colorIndexZNormal;
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

    Program _program;

    public PlanarMap(Program program, IMyCubeGrid grid)
    {
        _program = program;

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

        _colorIndexXNormal = new int?[diff.Y + 1, diff.Z + 1];
        _colorIndexYNormal = new int?[diff.Z + 1, diff.X + 1];
        _colorIndexZNormal = new int?[diff.X + 1, diff.Y + 1];
    }

    public void CreateQuadTrees()
    {
        QuadTreeXNormal.Initialize(_densityXNormal, DISCRETE_DENSITY_STEPS);
        QuadTreeYNormal.Initialize(_densityYNormal, DISCRETE_DENSITY_STEPS);
        QuadTreeZNormal.Initialize(_densityZNormal, DISCRETE_DENSITY_STEPS);
    }

    void UpdateColorIndices(int?[,] colorIndices, int x, int y, int? newColorIndex)
    {
        int? colorIndex = colorIndices[x, y];
        if (!colorIndex.HasValue)
        {
            colorIndex = newColorIndex;
        }
        else if (newColorIndex.HasValue)
        {
            colorIndex = Math.Min(colorIndex.Value, newColorIndex.Value);
        }
        colorIndices[x, y] = colorIndex;
    }

    public void StoreBlockInfo(BlockInfo info)
    {
        var diff = info.GridPosition - _min;
        var fromCenter = info.GridPosition - _center;

        _densityXNormal[diff.Y, diff.Z] += 1;
        _densityYNormal[diff.Z, diff.X] += 1;
        _densityZNormal[diff.X, diff.Y] += 1;

        UpdateColorIndices(_colorIndexXNormal, diff.Y, diff.Z, info.BlockColorIndex);
        UpdateColorIndices(_colorIndexYNormal, diff.Z, diff.X, info.BlockColorIndex);
        UpdateColorIndices(_colorIndexZNormal, diff.X, diff.Y, info.BlockColorIndex);

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
        int? colorIndex;
        if (NormalAxis.X == normal)
        {
            colorIndex = _colorIndexXNormal[diff.Y, diff.Z];
            status = _statusXNormal.Active[diff.Y, diff.Z];
        }
        else if (NormalAxis.Y == normal)
        {
            colorIndex = _colorIndexYNormal[diff.Z, diff.X];
            status = _statusYNormal.Active[diff.Z, diff.X];
        }
        else
        {
            colorIndex = _colorIndexZNormal[diff.X, diff.Y];
            status = _statusZNormal.Active[diff.X, diff.Y];
        }

        spriteName = "SquareSimple";
        functionalSpriteColor = Color.Transparent;

        if ((status & (BlockStatus.Missing | BlockStatus.Damaged)) != 0)
        {
            functionalSpriteColor = _program.LegendInstance.DamageCategory.Color;
        }
        else if (colorIndex.HasValue)
        {
            functionalSpriteColor = _program.LegendInstance.BlockCategories[colorIndex.Value].Color;
        }

        return status == BlockStatus.Missing || status == BlockStatus.Damaged || colorIndex.HasValue;
    }

    public Color GetColor(float lerpScale)
    {
        return Color.Lerp(_program.ColorConfig.MinDensityColor, _program.ColorConfig.MaxDensityColor, lerpScale);
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
        return GetColor(lerpScale);
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

public class RuntimeTracker
{
    public int Capacity;
    public double Sensitivity;
    public double MaxRuntime;
    public double MaxInstructions;
    public double AverageRuntime;
    public double AverageInstructions;
    public double LastRuntime;
    public double LastInstructions;
    
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

    public Scheduler(Program program, bool ignoreFirstRun = false)
    {
        _program = program;
        _ignoreFirstRun = ignoreFirstRun;
    }

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
            if (_queuedActions.Count != 0)
                _currentlyQueuedAction = _queuedActions.Dequeue();
        }

        if (_currentlyQueuedAction != null)
        {
            _currentlyQueuedAction.Update(deltaTicks);
            if (_currentlyQueuedAction.JustRan)
            {
                if (!_currentlyQueuedAction.DisposeAfterRun)
                {
                    _queuedActions.Enqueue(_currentlyQueuedAction);
                }
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

    public void AddScheduledAction(Action action, double updateFrequency, bool disposeAfterRun = false, double timeOffset = 0)
    {
        ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun, timeOffset);
        if (!_inUpdate)
            _scheduledActions.Add(scheduledAction);
        else
            _actionsToAdd.Add(scheduledAction);
    }

    public void AddScheduledAction(ScheduledAction scheduledAction)
    {
        if (!_inUpdate)
            _scheduledActions.Add(scheduledAction);
        else
            _actionsToAdd.Add(scheduledAction);
    }

    public void AddQueuedAction(Action action, double updateInterval, bool removeAfterRun = false)
    {
        if (updateInterval <= 0)
        {
            updateInterval = 0.001; // avoids divide by zero
        }
        QueuedAction scheduledAction = new QueuedAction(action, updateInterval, removeAfterRun);
        _queuedActions.Enqueue(scheduledAction);
    }

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

public class MultiScreenSpriteSurface : ISpriteSurface
{
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

    static List<MyDefinitionId> _insetScreenDefs = new List<MyDefinitionId>()
    {
        MyDefinitionId.Parse("MyObjectBuilder_TextPanel/LargeFullBlockLCDPanel"),
        MyDefinitionId.Parse("MyObjectBuilder_TextPanel/SmallFullBlockLCDPanel"),
    };

    static List<MyDefinitionId> _diagonalScreenDefs = new List<MyDefinitionId>()
    {
        MyDefinitionId.Parse("MyObjectBuilder_TextPanel/LargeCurvedLCDPanel"),
        MyDefinitionId.Parse("MyObjectBuilder_TextPanel/SmallCurvedLCDPanel"),
        MyDefinitionId.Parse("MyObjectBuilder_TextPanel/LargeDiagonalLCDPanel"),
        MyDefinitionId.Parse("MyObjectBuilder_TextPanel/SmallDiagonalLCDPanel"),
    };

    public MultiScreenSpriteSurface(IMyTextPanel anchor, int rows, int cols, Program p)
    {
        _anchor = anchor;
        _p = p;
        _surfaces = new SingleScreenSpriteSurface[rows, cols];
        _screenOrigins = new Vector2[rows, cols];
        Rows = rows;
        Cols = cols;

        _rotationProp = _anchor.GetProperty("Rotate").Cast<float>();

        Vector3 anchorRight, anchorDown;
        GetAnchorDirections(anchor, out anchorRight, out anchorDown);
        Vector3 anchorBlockSize = new Vector3(_anchor.Max - _anchor.Min) + Vector3.One;
        Vector3I stepRight = Vector3I.Round(Math.Abs(Vector3.Dot(anchorBlockSize, anchorRight)) * anchorRight);
        Vector3I stepDown = Vector3I.Round(Math.Abs(Vector3.Dot(anchorBlockSize, anchorDown)) * anchorDown);
        Vector3I anchorPos = _anchor.Position;
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

    static void GetAnchorDirections(IMyTextPanel anchor, out Vector3 anchorRight, out Vector3 anchorDown)
    {
        var def = anchor.BlockDefinition;
        if (_insetScreenDefs.Contains(def))
        {
            anchorRight = Base6Directions.GetVector(anchor.Orientation.Forward);
        }
        else if (_diagonalScreenDefs.Contains(def))
        {
            anchorRight = Base6Directions.GetVector(anchor.Orientation.Forward) + Base6Directions.GetVector(anchor.Orientation.Left);
            anchorRight.Normalize();
        }
        else
        {
            anchorRight = -Base6Directions.GetVector(anchor.Orientation.Left);
        }

        anchorDown = -Base6Directions.GetVector(anchor.Orientation.Up);
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
    void WriteToIni(MyIni ini, string section);
    bool ReadFromIni(MyIni ini, string section);
    bool Update(MyIni ini, string section);
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
    protected T DefaultValue => _defaultValue;
    bool _skipRead = false;

    public static implicit operator T(ConfigValue<T> cfg)
    {
        return cfg.Value;
    }

    protected virtual void InitializeValue()
    {
        _value = default(T);
    }

    public ConfigValue(string name, T defaultValue, string comment)
    {
        Name = name;
        InitializeValue();
        _defaultValue = defaultValue;
        Comment = comment;
        SetDefault();
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    public bool Update(MyIni ini, string section)
    {
        bool read = ReadFromIni(ini, section);
        WriteToIni(ini, section);
        return read;
    }

    public bool ReadFromIni(MyIni ini, string section)
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

    public void WriteToIni(MyIni ini, string section)
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

public class ConfigSection
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

    void SetComment(MyIni ini)
    {
        if (!string.IsNullOrWhiteSpace(Comment))
        {
            ini.SetSectionComment(Section, Comment);
        }
    }

    public void ReadFromIni(MyIni ini)
    {    
        foreach (IConfigValue c in _values)
        {
            c.ReadFromIni(ini, Section);
        }
    }

    public void WriteToIni(MyIni ini)
    {    
        foreach (IConfigValue c in _values)
        {
            c.WriteToIni(ini, Section);
        }
        SetComment(ini);
    }

    public void Update(MyIni ini)
    {    
        foreach (IConfigValue c in _values)
        {
            c.Update(ini, Section);
        }
        SetComment(ini);
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
public class ConfigDeprecated<T> : IConfigValue<T>
{
    public Action<T> Callback;
    readonly IConfigValue<T> _impl;

    public string Name
    {
        get { return _impl.Name; }
        set { _impl.Name = value; }
    }

    public string Comment
    {
        get { return _impl.Comment; }
        set { _impl.Comment = value; }
    }

    public T Value
    {
        get { return _impl.Value; }
        set { _impl.Value = value; }
    }

    public ConfigDeprecated(IConfigValue<T> impl)
    {
        _impl = impl;
    }

    public bool ReadFromIni(MyIni ini, string section)
    {
        bool read = _impl.ReadFromIni(ini, section);
        if (read)
        {
            Callback?.Invoke(_impl.Value);
        }
        return read;
    }

    public void WriteToIni(MyIni ini, string section)
    {
        ini.Delete(section, _impl.Name);
    }

    public bool Update(MyIni ini, string section)
    {
        bool read = ReadFromIni(ini, section);
        WriteToIni(ini, section);
        return read;
    }

    public void Reset() { }
}

public class ConfigNullable<T> : IConfigValue<T> where T : struct
{
    public string Name
    {
        get { return _impl.Name; }
        set { _impl.Name = value; }
    }

    public string Comment
    {
        get { return _impl.Comment; }
        set { _impl.Comment = value; }
    }

    public string NullString;

    public T Value
    {
        get { return _impl.Value; }
        set
        {
            _impl.Value = value;
            HasValue = true;
            _skipRead = true;
        }
    }
    
    public bool HasValue { get; private set; }
    readonly IConfigValue<T> _impl;
    bool _skipRead = false;

    public ConfigNullable(IConfigValue<T> impl, string nullString = "none")
    {
        _impl = impl;
        NullString = nullString;
        HasValue = false;
    }

    public void Reset()
    {
        HasValue = false;
        _skipRead = true;
    }

    public bool ReadFromIni(MyIni ini, string section)
    {
        if (_skipRead)
        {
            _skipRead = false;
            return true;
        }
        bool read = _impl.ReadFromIni(ini, section);
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

    public void WriteToIni(MyIni ini, string section)
    {
        _impl.WriteToIni(ini, section);
        if (!HasValue)
        {
            ini.Set(section, _impl.Name, NullString);
        }
    }

    public bool Update(MyIni ini, string section)
    {
        bool read = ReadFromIni(ini, section);
        WriteToIni(ini, section);
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
