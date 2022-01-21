
#region In-game Script
/*
 * / //// / Whip's Wheel Status Monitor / //// /
 */

const string Version = "1.0.0";
const string Date = "08/01/2019";

const string TextPanelNameTag = "Wheel Status";
const int UpdatesPerRefresh = 6;
bool _isSetup = false;
bool _mySurfaceIsUsed = false;
int _currentUpdateCount = 141;

const string INI_SECTION_TEXT_SURF = "Wheel Status Monitor - Text Surface Config";
const string INI_TEXT_SURF_TEMPLATE = "Show on screen {0}";

List<IMyMotorSuspension> _wheels = new List<IMyMotorSuspension>();
List<IMyTextSurface> _surfaces = new List<IMyTextSurface>();

IMyTextSurface _mySurface;
StringBuilder _outptBuilder = new StringBuilder();

new void Echo(string content)
{
    base.Echo(content);
    _outptBuilder.AppendLine(content);
}

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    _mySurface = Me.GetSurface(0);
}

void Main(string arg, UpdateType updateSource)
{
    _outptBuilder.Clear();

    Echo($"Whip's Wheel Status Monitor\n(Version {Version} - {Date})\n");

    if (!_isSetup || _currentUpdateCount > UpdatesPerRefresh)
    {
        _currentUpdateCount = 0;
        _isSetup = Setup();
    }

    Echo($"Update: {_currentUpdateCount}\n");

    if (!_isSetup)
    {
        PrintScreens();
        return;
    }
    
    _currentUpdateCount++;

    CheckWheelStatus();
    PrintScreens();
}

void CheckWheelStatus()
{
    foreach (var wheel in _wheels)
    {
        if (!wheel.IsAttached)
        {
            Echo($"INFO | '{wheel.CustomName}'\n    is detached\b");
        }
    }
}

void PrintScreens()
{
    foreach (var surf in _surfaces)
    {
        surf.ContentType = ContentType.TEXT_AND_IMAGE;
        surf.WriteText(_outptBuilder);
    }
    
    if (!_mySurfaceIsUsed)
    {
        _mySurface.ContentType = ContentType.TEXT_AND_IMAGE;
        _mySurface.WriteText(_outptBuilder);
    }
}

bool Setup()
{
    _surfaces.Clear();
    _wheels.Clear();
    _mySurfaceIsUsed = false;
    
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, CollectFunction);

    bool setup = true;
    if (_surfaces.Count == 0)
    {
        Echo($"INFO | No text panels or text\n    surfaces named '{TextPanelNameTag}'\n");
        //setup = false;
    }

    if (_wheels.Count == 0)
    {
        Echo($"ERROR | No wheels found\n");
        setup = false;
    }

    return setup;
}

bool CollectFunction(IMyTerminalBlock block)
{
    if (!block.IsSameConstructAs(Me))
        return false;
        
    var wheel = block as IMyMotorSuspension;
    if (wheel != null)
    {
        _wheels.Add(wheel);
        return false;
    }
    
    if (block.CustomName.Contains(TextPanelNameTag))
        AddTextSurfaces(block, _surfaces);
    return false;
}

MyIni _textSurfaceIni = new MyIni();

void AddTextSurfaces(IMyTerminalBlock block, List<IMyTextSurface> textSurfaces)
{
    var textSurface = block as IMyTextSurface;
    if (textSurface != null)
    {
        textSurfaces.Add(textSurface);
        return;
    }

    var surfaceProvider = block as IMyTextSurfaceProvider;
    if (surfaceProvider == null)
        return;

    _textSurfaceIni.Clear();
    _textSurfaceIni.TryParse(block.CustomData);

    int surfaceCount = surfaceProvider.SurfaceCount;
    for (int i = 0; i < surfaceCount; ++i)
    {
        string iniKey = string.Format(INI_TEXT_SURF_TEMPLATE, i);
        bool display = _textSurfaceIni.Get(INI_SECTION_TEXT_SURF, iniKey).ToBoolean(i == 0 && !(block is IMyProgrammableBlock));
        if (display)
        {
            var surface = surfaceProvider.GetSurface(i);
            textSurfaces.Add(surface);
            if (surface == _mySurface)
                _mySurfaceIsUsed = true;
        }

        _textSurfaceIni.Set(INI_SECTION_TEXT_SURF, iniKey, display);
    }

    string output = _textSurfaceIni.ToString();
    if (!string.Equals(output, block.CustomData))
        block.CustomData = output;
}
#endregion
