
/*          
/ //// / Whip's Auto Door and Airlock Script / //// /
_______________________________________________________________________          
///DESCRIPTION///   

    This script will automatically close doors after a set amount of time.
    It can also support an arbitrary amount of simple airlock systems.

_______________________________________________________________________          
///AUTO DOOR CLOSER///      

    The script will fetch ALL doors on the grid and automatically close any 
    door that has been fully open for over 3 seconds (10 seconds for hangar doors). 
    
    You can change the door auto close interval:
        GLOBALLY: In the programmable block custom data
        PER DOOR: In each door's custom data
    
    Doors can also be excluded from this feature.

Excluding Doors:       
    * Add the tag "Excluded" to the front or rear of the door(s) name.      
_______________________________________________________________________          
///AIRLOCKS///          

    This script supports the optional feature of simple airlock systems.  
    Airlock systems are composed of AT LEAST one Interior Door AND one Exterior Door.  
    The airlock status light does NOT affect the functionality of the doors  
    so if you don't have space for one, don't fret :)   

Airlock system names should follow these patterns:   

    * Interior Airlock Doors: "[Prefix] Airlock Interior"   

    * Exterior Airlock Doors: "[Prefix] Airlock Exterior"   

    * Airlock Status Lights: "[Prefix] Airlock Light"   

    You can make the [Prefix] whatever you wish, but in order for doors in an airlock   
    system to be linked by the script, they MUST have the same prefix. 
_____________________________________________________________________    

If you have any questions, comments, or concerns, feel free to leave a comment on           
the workshop page: http://steamcommunity.com/sharedfiles/filedetails/?id=416932930          
- Whiplash141   :)   
_____________________________________________________________________      












DO NOT CHANGE VARIABLES IN THE CODE
      USE THE CUSTOM DATA!












DO NOT CHANGE VARIABLES IN THE CODE
      USE THE CUSTOM DATA!













DO NOT CHANGE VARIABLES IN THE CODE
      USE THE CUSTOM DATA!









YES TOEDPEREGRINE4 THAT INCLUDES YOU TOO!








-------------------------------------------------------------------
============ Don't touch anything below here! <3 ==================
-------------------------------------------------------------------
*/
const string VERSION = "42.1.2";
const string DATE = "2021/09/12";

// Ini keys
const string INI_SECTION_GENERAL = "Auto Door and Airlock - General Config";
const string INI_GENERAL_ENABLE_AUTO_DOORS = "Enable automatic door closing";
const string INI_GENERAL_ENABLE_AIRLOCK = "Enable airlock system";
const string INI_GENERAL_IGNORE_ALL_HANGAR_DOORS = "Ignore all hangar doors";
const string INI_GENERAL_REGULAR_DOOR_OPEN_TIME = "Default regular door auto close time (s)";
const string INI_GENERAL_HANGAR_DOOR_OPEN_TIME = "Default hangar door auto close time (s)";
const string INI_GENERAL_DOOR_EXCLUDE_NAME = "Auto door exclusion name tag";
const string INI_GENERAL_INTERIOR_DOOR_NAME = "Interior airlock door name tag";
const string INI_GENERAL_EXTERIOR_DOOR_NAME = "Exterior airlock door name tag";
const string INI_GENERAL_LIGHT_NAME = "Airlock light name tag";
const string INI_GENERAL_SOUND_NAME = "Airlock sound block name tag";
const string INI_GENERAL_DRAW_TITLE_SCREEN = "Draw title screen";
const string INI_GENERAL_AUTOCLOSE_FULLY_OPEN = "Auto close only fully open doors";

// Custom data configurable
public bool AutoCloseOnlyFullyOpen { get; private set; } = true;

bool drawTitleScreen = true;
bool enableAutoDoorCloser = true;
bool enableAirlockSystem = true;
bool ignoreAllHangarDoors = true;
double regularDoorOpenDuration = 3;
double hangarDoorOpenDuration = 10;
string doorExcludeString = "Excluded";

string airlockInteriorDoorNameTag = "Airlock Interior";
string airlockExteriorDoorNameTag = "Airlock Exterior";
string airlockLightNameTag = "Airlock Light";
string airlockSoundNameTag = "Airlock Sound";

const double secondsPerUpdate = 1.0 / 6.0;
const double updateTime = 1.0 / 6.0;
const double refreshTime = 30;

RuntimeTracker _runtimeTracker;
MyIni _ini = new MyIni();
Scheduler _scheduler;
ScheduledAction _scheduledGrabBlocks;
ScheduledAction _scheduledMainExecution;

AutoDoorScreenManager _screenManager;

Program()
{
    _screenManager = new AutoDoorScreenManager(VERSION, this);
    
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    _runtimeTracker = new RuntimeTracker(this, 12, 0.005);
    
    _scheduledGrabBlocks = new ScheduledAction(GrabBlocks, 1.0 / 30.0);
    _scheduledMainExecution = new ScheduledAction(MainExecutionLoop, 6);
    
    _scheduler = new Scheduler(this);
    _scheduler.AddScheduledAction(_scheduledMainExecution);
    _scheduler.AddScheduledAction(PrintDetailedInfo, 1);
    _scheduler.AddScheduledAction(_scheduledGrabBlocks);
    _scheduler.AddScheduledAction(DrawTitleScreen, 6);
    _scheduler.AddScheduledAction(_screenManager.RestartDraw, 0.2);
    
    GrabBlocks();
}

void ProcessIniConfig()
{
    _ini.Clear();

    // Read
    if (_ini.TryParse(Me.CustomData))
    {
        enableAutoDoorCloser = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_ENABLE_AUTO_DOORS).ToBoolean(enableAutoDoorCloser);
        enableAirlockSystem = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_ENABLE_AIRLOCK).ToBoolean(enableAirlockSystem);
        ignoreAllHangarDoors = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_IGNORE_ALL_HANGAR_DOORS).ToBoolean(ignoreAllHangarDoors);
        regularDoorOpenDuration = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_REGULAR_DOOR_OPEN_TIME).ToDouble(regularDoorOpenDuration);
        hangarDoorOpenDuration = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_HANGAR_DOOR_OPEN_TIME).ToDouble(hangarDoorOpenDuration);
        doorExcludeString = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_DOOR_EXCLUDE_NAME).ToString(doorExcludeString);
        airlockInteriorDoorNameTag = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_INTERIOR_DOOR_NAME).ToString(airlockInteriorDoorNameTag);
        airlockExteriorDoorNameTag = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_EXTERIOR_DOOR_NAME).ToString(airlockExteriorDoorNameTag);
        airlockLightNameTag = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_LIGHT_NAME).ToString(airlockLightNameTag);
        airlockSoundNameTag = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_SOUND_NAME).ToString(airlockSoundNameTag);
        drawTitleScreen = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_DRAW_TITLE_SCREEN).ToBoolean(drawTitleScreen);
        AutoCloseOnlyFullyOpen = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_AUTOCLOSE_FULLY_OPEN).ToBoolean(AutoCloseOnlyFullyOpen);
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    // Write
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_ENABLE_AUTO_DOORS, enableAutoDoorCloser);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_ENABLE_AIRLOCK, enableAirlockSystem);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_IGNORE_ALL_HANGAR_DOORS, ignoreAllHangarDoors);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_REGULAR_DOOR_OPEN_TIME, regularDoorOpenDuration);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_HANGAR_DOOR_OPEN_TIME, hangarDoorOpenDuration);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_DOOR_EXCLUDE_NAME, doorExcludeString);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_INTERIOR_DOOR_NAME, airlockInteriorDoorNameTag);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_EXTERIOR_DOOR_NAME, airlockExteriorDoorNameTag);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_LIGHT_NAME, airlockLightNameTag);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_SOUND_NAME, airlockSoundNameTag);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_DRAW_TITLE_SCREEN, drawTitleScreen);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_AUTOCLOSE_FULLY_OPEN, AutoCloseOnlyFullyOpen);

    string output = _ini.ToString();
    if (output != Me.CustomData)
    {
        Me.CustomData = output;
    }
}

StringBuilder _detailedInfo = new StringBuilder();
void PrintDetailedInfo()
{
    _detailedInfo.Append($"Whip's Auto Door and Airlock\n(Version {VERSION} - {DATE})\n\n");
    _detailedInfo.Append($"Next refresh in {Math.Round(Math.Max(_scheduledGrabBlocks.RunInterval - _scheduledGrabBlocks.TimeSinceLastRun, 0))} seconds\n\n");
    _detailedInfo.Append(_runtimeEcho);
    _detailedInfo.Append("\n").Append(_runtimeTracker.Write());
    Echo(_detailedInfo.ToString());
    _detailedInfo.Clear();
}

void Main(string arg, UpdateType updateType)
{
    _runtimeTracker.AddRuntime();
    _scheduler.Update();
    _runtimeTracker.AddInstructions();
}

void MainExecutionLoop()
{
    _runtimeEcho.Clear();
    
    if (enableAutoDoorCloser)
    {
        AutoDoors(_scheduledMainExecution.TimeSinceLastRun); //controls auto door closing
    }

    if (enableAirlockSystem)
    {
        Airlocks(); //controls airlock system
    }
}

void DrawTitleScreen()
{
    if (drawTitleScreen)
    {
        _screenManager.Draw();
    }
}

bool IsClosed(IMyTerminalBlock b)
{
    return GridTerminalSystem.GetBlockWithId(b.EntityId) == null;
}

HashSet<string> airlockNames = new HashSet<string>();
List<IMyDoor> airlockDoors = new List<IMyDoor>();
List<IMySoundBlock> allSounds = new List<IMySoundBlock>();
List<IMyLightingBlock> allLights = new List<IMyLightingBlock>();
List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();

List<Airlock> airlockList = new List<Airlock>();
List<AutoDoor> autoDoors = new List<AutoDoor>();
List<IMyDoor> autoDoorsCached = new List<IMyDoor>();

void GrabBlocks()
{
    ProcessIniConfig();

    GridTerminalSystem.GetBlocksOfType(allBlocks, x => x.IsSameConstructAs(Me));

    airlockDoors.Clear();
    allSounds.Clear();
    allLights.Clear();

    // Trim out doors that no-longer exist or are no longer valid
    for (int i = autoDoors.Count - 1; i >= 0; --i)
    {
        var door = autoDoors[i].Door;
        bool shouldRemove = false;
        if (IsClosed(door))
        {
            shouldRemove = true;
        }
        else if (StringExtensions.Contains(door.CustomName, doorExcludeString))
        {
            shouldRemove = true;
        }

        if (shouldRemove)
        {
            autoDoors.RemoveAt(i);
        }
        else
        {
            autoDoors[i].UpdateSettings(door is IMyAirtightHangarDoor ? hangarDoorOpenDuration : regularDoorOpenDuration);
        }
    }

    // Fetch all blocks that the code needs
    foreach (var block in allBlocks)
    {
        if (block is IMyDoor)
        {
            var door = (IMyDoor)block;
            if (StringExtensions.Contains(block.CustomName, airlockInteriorDoorNameTag)
                || StringExtensions.Contains(block.CustomName, airlockExteriorDoorNameTag))
            {
                airlockDoors.Add(door);
            }

            if (ShouldAddAutoDoor(block))
            {
                if (!autoDoorsCached.Contains(door))
                {
                    double autoCloseInterval = door is IMyAirtightHangarDoor ? hangarDoorOpenDuration : regularDoorOpenDuration;
                    autoDoors.Add(new AutoDoor(door, autoCloseInterval, this));
                }
            }
        }
        else if (block is IMyLightingBlock && StringExtensions.Contains(block.CustomName, airlockLightNameTag))
        {
            allLights.Add((IMyLightingBlock)block);
        }
        else if (block is IMySoundBlock && StringExtensions.Contains(block.CustomName, airlockSoundNameTag))
        {
            allSounds.Add((IMySoundBlock)block);
        }
    }

    // Fetch all airlock door names
    // Note: This is inefficient as all hell
    airlockNames.Clear();
    foreach (var thisDoor in airlockDoors)
    {
        string nameLowercased = thisDoor.CustomName.ToLowerInvariant();
        if (StringExtensions.Contains(nameLowercased, airlockInteriorDoorNameTag))//lists all airlockDoors with proper name 
        {
            // Remove airlock tag
            string thisName = nameLowercased.Replace(airlockInteriorDoorNameTag.ToLowerInvariant(), "");

            // Remove exclude string
            thisName = thisName.Replace($"[{doorExcludeString.ToLowerInvariant()}]", "").Replace(doorExcludeString.ToLowerInvariant(), ""); //remove door exclusion string 

            // Remove spaces
            thisName = thisName.Replace(" ", "");

            airlockNames.Add(thisName);
        }
    }

    // Create airlock objects
    foreach (var hashValue in airlockNames)
    {

        bool dupe = false;
        foreach (var airlock in airlockList)
        {
            if (airlock.Name.Equals(hashValue))
            {
                airlock.GetBlocks(hashValue, airlockDoors, allLights, allSounds, airlockInteriorDoorNameTag, airlockExteriorDoorNameTag);
                dupe = true;
                break;
            }
        }

        if (!dupe)
            airlockList.Add(new Airlock(hashValue, airlockDoors, allLights, allSounds, airlockInteriorDoorNameTag, airlockExteriorDoorNameTag));
    }

    autoDoorsCached.Clear();
    foreach (var autoDoor in autoDoors)
    {
        autoDoorsCached.Add(autoDoor.Door);
    }
}

bool ShouldAddAutoDoor(IMyTerminalBlock block)
{
    if (ignoreAllHangarDoors && block is IMyAirtightHangarDoor)
        return false;
    else if (block.CustomName.ToLower().Contains(doorExcludeString.ToLower()))
        return false;
    else
        return true;
}

StringBuilder _runtimeEcho = new StringBuilder(512);
void AutoDoors(double timeElapsed)
{
    foreach (var thisDoor in autoDoors)
    {
        if (CheckInstructions())
        {
            _runtimeEcho.AppendLine("   Instruction limit hit\nAborting...");
            return;
        }

        thisDoor.Update(timeElapsed);
    }

    _runtimeEcho.AppendLine($"Automatic Door Summary:\n   Managed Doors: {autoDoors.Count}");
}

bool CheckInstructions(double proportion = 0.5)
{
    return Runtime.CurrentInstructionCount >= Runtime.MaxInstructionCount * proportion;
}

void Airlocks()
{
    _runtimeEcho.AppendLine("\nAirlock Summary:");

    if (airlockList.Count == 0)
    {
        _runtimeEcho.AppendLine("  No airlock groups found");
        return;
    }

    //Iterate through our airlock groups
    _runtimeEcho.AppendLine($"  Airlock count: {airlockList.Count}\n\nDetailed Airlock Info:");
    foreach (var airlock in airlockList)
    {
        if (CheckInstructions())
        {
            _runtimeEcho.AppendLine("  Instruction limit hit\nAborting...");
            return;
        }

        airlock.DoLogic();
        _runtimeEcho.AppendLine($"  Airlock group '{airlock.Name}' found\n{airlock.Info}");
    }
}

public class AutoDoor
{
    public IMyDoor Door { get; private set; } = null;
    double _doorOpenTime = 0;
    double _defaultAutoCloseTime;
    double _autoCloseTime = 0;
    bool _wasOpen = false;
    MyIni _ini = new MyIni();
    Program _p;

    const string INI_SECTION_DOOR = "Auto Door and Airlock - Door Config";
    const string INI_DOOR_USE_DEFAULT_AUTO_CLOSE = "Use default auto close time";
    const string INI_DOOR_CUSTOM_AUTO_CLOSE_TIME = "Custom auto close time (s)";
    readonly string INI_COMMENT_DOOR_CUSTOM_AUTO_CLOSE_TIME = $" To use a custom auto close time, set \"{INI_DOOR_USE_DEFAULT_AUTO_CLOSE}\" to false";

    public AutoDoor(IMyDoor door, double defaultDoorCloseTime, Program program)
    {
        Door = door;
        _defaultAutoCloseTime = defaultDoorCloseTime;
        _p = program;
        ParseIni();
    }

    public void UpdateSettings(double defaultDoorCloseTime)
    {
        _defaultAutoCloseTime = defaultDoorCloseTime;
        ParseIni();
    }

    void ParseIni()
    {
        // Read
        _ini.Clear();
        bool useDefault = true;
        double customAutoCloseTime = _defaultAutoCloseTime;
        if (_ini.TryParse(Door.CustomData))
        {
            useDefault = _ini.Get(INI_SECTION_DOOR, INI_DOOR_USE_DEFAULT_AUTO_CLOSE).ToBoolean(useDefault);
            customAutoCloseTime = _ini.Get(INI_SECTION_DOOR, INI_DOOR_CUSTOM_AUTO_CLOSE_TIME).ToDouble(customAutoCloseTime);
        }
        else if (!string.IsNullOrWhiteSpace(Door.CustomData))
        {
            _ini.EndContent = Door.CustomData;
        }

        // Write
        _ini.Set(INI_SECTION_DOOR, INI_DOOR_USE_DEFAULT_AUTO_CLOSE, useDefault);
        _ini.Set(INI_SECTION_DOOR, INI_DOOR_CUSTOM_AUTO_CLOSE_TIME, customAutoCloseTime);
        _ini.SetComment(INI_SECTION_DOOR, INI_DOOR_CUSTOM_AUTO_CLOSE_TIME, INI_COMMENT_DOOR_CUSTOM_AUTO_CLOSE_TIME);

        string output = _ini.ToString();
        if (output != Door.CustomData)
        {
            Door.CustomData = output;
        }

        // Process
        _autoCloseTime = useDefault ? _defaultAutoCloseTime : customAutoCloseTime;
    }

    public void Update(double time)
    {
        // We add small epsilons here to account for potential FPE.
        float threshold = _p.AutoCloseOnlyFullyOpen ? 0.999f : 0.001f;
        if (Door.OpenRatio < threshold) // Not yet "open"
        {
            _doorOpenTime = 0;
            _wasOpen = false;
            return;
        }
        else if (!_wasOpen) //begin new count
        {
            _wasOpen = true;
            _doorOpenTime = 0;
            return;
        }
        else //if _wasOpen
        {
            _doorOpenTime += time;
        }

        if (_autoCloseTime <= _doorOpenTime)
        {
            Door.CloseDoor();
            _doorOpenTime = 0;
            _wasOpen = false;
        }
    }
}

public class Airlock
{
    List<IMyDoor> _airlockInteriorList = new List<IMyDoor>();
    List<IMyDoor> _airlockExteriorList = new List<IMyDoor>();
    List<LightConfig> _airlockLightList = new List<LightConfig>();
    List<IMySoundBlock> _airlockSoundList = new List<IMySoundBlock>();
    private const string _soundBlockPlayingString = "%Playing sound...%";
    public string Name { get; private set; }
    public string Info { get; private set; }

    MyIni _ini = new MyIni();
    const string INI_SECTION_LIGHT = "Auto Door and Airlock - Light Config";

    const string INI_LIGHT_OPEN_ENABLE = "Turn on when airlock is open";
    const string INI_LIGHT_OPEN_COLOR = "Airlock open - Color (R,G,B)";
    const string INI_LIGHT_OPEN_INTERVAL = "Airlock open - Blink interval (seconds)";
    const string INI_LIGHT_OPEN_LENGTH = "Airlock open - Blink length (%)";

    const string INI_LIGHT_CLOSED_ENABLE = "Turn on when airlock is closed";
    const string INI_LIGHT_CLOSED_COLOR = "Airlock closed - Color (R,G,B)";
    const string INI_LIGHT_CLOSED_INTERVAL = "Airlock closed - Blink interval (seconds)";
    const string INI_LIGHT_CLOSED_LENGTH = "Airlock closed - Blink length (%)";

    class LightConfig
    {
        public readonly IMyLightingBlock Light;

        public bool OpenLightEnabled;
        public Color OpenColor;
        public float OpenBlinkInterval;
        public float OpenBlinkLength;

        public bool ClosedLightEnabled;
        public Color ClosedColor;
        public float ClosedBlinkInterval;
        public float ClosedBlinkLength;

        public LightConfig(IMyLightingBlock l)
        {
            Light = l;

            // Defaults
            OpenLightEnabled = true;
            ClosedLightEnabled = true;
            OpenColor = new Color(255, 40, 40);
            ClosedColor = new Color(80, 160, 255);
            OpenBlinkLength = 50f;
            ClosedBlinkLength = 100f;
            OpenBlinkInterval = .8f;
            ClosedBlinkInterval = .8f;
        }

        public void SetColor(bool isOpen)
        {
            Light.Enabled = isOpen ? OpenLightEnabled : ClosedLightEnabled;
            Light.Color = isOpen ? OpenColor : ClosedColor;
            Light.BlinkIntervalSeconds = isOpen ? OpenBlinkInterval : ClosedBlinkInterval;
            Light.BlinkLength = isOpen ? OpenBlinkLength : ClosedBlinkLength;
        }
    }

    public Airlock(string airlockName, List<IMyDoor> airlockDoors, List<IMyLightingBlock> allLights, List<IMySoundBlock> allSounds, string airlockInteriorDoorNameTag, string airlockExteriorDoorNameTag)
    {
        Name = airlockName;

        GetBlocks(this.Name, airlockDoors, allLights, allSounds, airlockInteriorDoorNameTag, airlockExteriorDoorNameTag);
        Info = $"    Interior Doors: {_airlockInteriorList.Count}\n    Exterior Doors: {_airlockExteriorList.Count}\n    Lights: {_airlockLightList.Count}\n    Sound Blocks: {_airlockSoundList.Count}";
    }

    void ProcessLightBlock(IMyLightingBlock l)
    {
        LightConfig light = new LightConfig(l);

        // Read
        _ini.Clear();
        if (_ini.TryParse(l.CustomData))
        {
            light.OpenLightEnabled = _ini.Get(INI_SECTION_LIGHT, INI_LIGHT_OPEN_ENABLE).ToBoolean(light.OpenLightEnabled);
            light.OpenColor = MyIniHelper.GetColor(INI_SECTION_LIGHT, INI_LIGHT_OPEN_COLOR, _ini, light.OpenColor);
            light.OpenBlinkInterval = _ini.Get(INI_SECTION_LIGHT, INI_LIGHT_OPEN_INTERVAL).ToSingle(light.OpenBlinkInterval);
            light.OpenBlinkLength = _ini.Get(INI_SECTION_LIGHT, INI_LIGHT_OPEN_LENGTH).ToSingle(light.OpenBlinkLength);
            light.ClosedLightEnabled = _ini.Get(INI_SECTION_LIGHT, INI_LIGHT_CLOSED_ENABLE).ToBoolean(light.ClosedLightEnabled);
            light.ClosedColor = MyIniHelper.GetColor(INI_SECTION_LIGHT, INI_LIGHT_CLOSED_COLOR, _ini, light.ClosedColor);
            light.ClosedBlinkInterval = _ini.Get(INI_SECTION_LIGHT, INI_LIGHT_CLOSED_INTERVAL).ToSingle(light.ClosedBlinkInterval);
            light.ClosedBlinkLength = _ini.Get(INI_SECTION_LIGHT, INI_LIGHT_CLOSED_LENGTH).ToSingle(light.ClosedBlinkLength);
        }
        else if (!string.IsNullOrWhiteSpace(l.CustomData))
        {
            _ini.EndContent = l.CustomData;
        }

        // Write
        _ini.Set(INI_SECTION_LIGHT, INI_LIGHT_OPEN_ENABLE, light.OpenLightEnabled);
        MyIniHelper.SetColor(INI_SECTION_LIGHT, INI_LIGHT_OPEN_COLOR, light.OpenColor, _ini, false);
        _ini.Set(INI_SECTION_LIGHT, INI_LIGHT_OPEN_INTERVAL, light.OpenBlinkInterval);
        _ini.Set(INI_SECTION_LIGHT, INI_LIGHT_OPEN_LENGTH,light.OpenBlinkLength);
        _ini.Set(INI_SECTION_LIGHT, INI_LIGHT_CLOSED_ENABLE, light.ClosedLightEnabled);
        MyIniHelper.SetColor(INI_SECTION_LIGHT, INI_LIGHT_CLOSED_COLOR, light.ClosedColor, _ini, false);
        _ini.Set(INI_SECTION_LIGHT, INI_LIGHT_CLOSED_INTERVAL, light.ClosedBlinkInterval);
        _ini.Set(INI_SECTION_LIGHT, INI_LIGHT_CLOSED_LENGTH, light.ClosedBlinkLength);

        string output = _ini.ToString();
        if (output != l.CustomData)
        {
            l.CustomData = output;
        }

        _airlockLightList.Add(light);
    }

    public void GetBlocks(string airlockName, List<IMyDoor> airlockDoors, List<IMyLightingBlock> allLights, List<IMySoundBlock> allSounds, string airlockInteriorDoorNameTag, string airlockExteriorDoorNameTag)
    {
        //sort through all doors
        _airlockInteriorList.Clear();
        _airlockExteriorList.Clear();
        _airlockLightList.Clear();
        _airlockSoundList.Clear();

        airlockInteriorDoorNameTag = airlockInteriorDoorNameTag.ToLowerInvariant().Replace(" ", "");
        airlockExteriorDoorNameTag = airlockExteriorDoorNameTag.ToLowerInvariant().Replace(" ", "");

        foreach (var d in airlockDoors)
        {
            string thisDoorName = d.CustomName.ToLowerInvariant().Replace(" ", "");
            if (StringExtensions.Contains(thisDoorName, airlockName))
            {
                if (StringExtensions.Contains(thisDoorName, airlockInteriorDoorNameTag))
                {
                    _airlockInteriorList.Add(d);
                }
                else if (StringExtensions.Contains(thisDoorName, airlockExteriorDoorNameTag))
                {
                    _airlockExteriorList.Add(d);
                }
            }
        }

        //sort through all lights 
        foreach (var l in allLights)
        {
            if (l.CustomName.Replace(" ", "").ToLowerInvariant().Contains(airlockName))
            {
                ProcessLightBlock(l);
            }
        }

        //sort through all lights 
        foreach (var s in allSounds)
        {
            if (s.CustomName.Replace(" ", "").ToLowerInvariant().Contains(airlockName))
            {
                _airlockSoundList.Add(s);
            }
        }

        Info = $"    Interior Doors: {_airlockInteriorList.Count}\n    Exterior Doors: {_airlockExteriorList.Count}\n    Lights: {_airlockLightList.Count}\n    Sound Blocks: {_airlockSoundList.Count}";
    }

    public void DoLogic()
    {
        bool isInteriorClosed;
        bool isExteriorClosed;

        //Start checking airlock status   
        if (_airlockInteriorList.Count != 0 && _airlockExteriorList.Count != 0) //if we have both door types    
        {
            //we assume the airlocks are closed until proven otherwise        
            isInteriorClosed = true;
            isExteriorClosed = true;

            //Door Interior Check          
            foreach (var airlockInterior in _airlockInteriorList)
            {
                if (airlockInterior.OpenRatio > 0)
                {
                    Lock(_airlockExteriorList);
                    isInteriorClosed = false;
                    break;
                    //if any doors yield false, bool will persist until comparison    
                }
            }

            //Door Exterior Check           
            foreach (var airlockExterior in _airlockExteriorList)
            {
                if (airlockExterior.OpenRatio > 0)
                {
                    Lock(_airlockInteriorList);
                    isExteriorClosed = false;
                    break;
                }
            }

            bool isOpen = !isInteriorClosed || !isExteriorClosed;
            PlaySound(isOpen, _airlockSoundList);
            foreach (var l in _airlockLightList)
            {
                l.SetColor(isOpen);
            }

            //if all Interior doors closed 
            if (isInteriorClosed)
                Unlock(_airlockExteriorList);

            //if all Exterior doors closed     
            if (isExteriorClosed)
                Unlock(_airlockInteriorList);
        }
    }

    private void Lock(List<IMyDoor> doorList)
    {
        //locks all doors with the input list
        foreach (IMyDoor lock_door in doorList)
        {
            //if door is open, then close
            if (lock_door.OpenRatio > 0)
                lock_door.CloseDoor();

            //if door is fully closed, then lock
            if (lock_door.OpenRatio == 0 && lock_door.Enabled)
                lock_door.Enabled = false;
        }
    }

    private void Unlock(List<IMyDoor> doorList)
    {
        //unlocks all doors with input list
        foreach (IMyDoor unlock_door in doorList)
            unlock_door.Enabled = true;
    }

    private void PlaySound(bool shouldPlay, List<IMySoundBlock> soundList)
    {
        foreach (var block in soundList)
        {
            if (shouldPlay)
            {
                if (!block.CustomData.Contains(_soundBlockPlayingString))
                {
                    block.Play();
                    block.LoopPeriod = 100f;
                    block.CustomData += _soundBlockPlayingString;
                }
            }
            else
            {
                block.Stop();
                block.CustomData = block.CustomData.Replace(_soundBlockPlayingString, "");
            }
        }
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

public static class StringExtensions
{
    public static bool Contains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }
}

public static class MyIniHelper
{
    #region Color
    /// <summary>
    /// Adds a Color to a MyIni object
    /// </summary>
    public static void SetColor(string sectionName, string itemName, Color color, MyIni ini, bool writeAlpha = true)
    {
        if (writeAlpha)
        {
            ini.Set(sectionName, itemName, string.Format("{0}, {1}, {2}, {3}", color.R, color.G, color.B, color.A));
        }
        else
        {
            ini.Set(sectionName, itemName, string.Format("{0}, {1}, {2}", color.R, color.G, color.B));
        }
    }

    /// <summary>
    /// Parses a MyIni for a Color
    /// </summary>
    public static Color GetColor(string sectionName, string itemName, MyIni ini, Color? defaultChar = null)
    {
        string rgbString = ini.Get(sectionName, itemName).ToString("null");
        string[] rgbSplit = rgbString.Split(',');

        int r = 0, g = 0, b = 0, a = 0;
        if (rgbSplit.Length < 3)
        {
            if (defaultChar.HasValue)
                return defaultChar.Value;
            else
                return Color.Transparent;
        }

        int.TryParse(rgbSplit[0].Trim(), out r);
        int.TryParse(rgbSplit[1].Trim(), out g);
        int.TryParse(rgbSplit[2].Trim(), out b);
        bool hasAlpha = rgbSplit.Length >= 4 && int.TryParse(rgbSplit[3].Trim(), out a);
        if (!hasAlpha)
            a = 255;

        r = MathHelper.Clamp(r, 0, 255);
        g = MathHelper.Clamp(g, 0, 255);
        b = MathHelper.Clamp(b, 0, 255);
        a = MathHelper.Clamp(a, 0, 255);

        return new Color(r, g, b, a);
    }
    #endregion
}


#region Scheduler
/// <summary>
/// Class for scheduling actions to occur at specific frequencies. Actions can be updated in parallel or in sequence (queued).
/// </summary>
public class Scheduler
{
    public double CurrentTimeSinceLastRun = 0;
    
    ScheduledAction _currentlyQueuedAction = null;
    bool _firstRun = true;
    bool _inUpdate = false;
    
    readonly bool _ignoreFirstRun;
    readonly List<ScheduledAction> _actionsToAdd = new List<ScheduledAction>();
    readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
    readonly List<ScheduledAction> _actionsToDispose = new List<ScheduledAction>();
    readonly Queue<ScheduledAction> _queuedActions = new Queue<ScheduledAction>();
    readonly Program _program;
    
    const double RUNTIME_TO_REALTIME = (1.0 / 60.0) / 0.0166666;

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
        double deltaTime = Math.Max(0, _program.Runtime.TimeSinceLastRun.TotalSeconds * RUNTIME_TO_REALTIME);
        
        if (_ignoreFirstRun && _firstRun)
            deltaTime = 0;

        _firstRun = false;
        _actionsToDispose.Clear();
        foreach (ScheduledAction action in _scheduledActions)
        {
            CurrentTimeSinceLastRun = action.TimeSinceLastRun + deltaTime;
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
    public void AddQueuedAction(Action action, double updateInterval)
    {
        if (updateInterval <= 0)
        {
            updateInterval = 0.001; // avoids divide by zero
        }
        ScheduledAction scheduledAction = new ScheduledAction(action, 1.0 / updateInterval, true);
        _queuedActions.Enqueue(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the queue. Queue is FIFO.
    /// </summary>
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

    /// <summary>
    /// Class for scheduling an action to occur at a specified frequency (in Hz).
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <param name="runFrequency">How often to run in Hz</param>
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

class AutoDoorScreenManager
{
    readonly Color _topBarColor = new Color(25, 25, 25);
    readonly Color _white = new Color(200, 200, 200);
    readonly Color _black = Color.Black;

    const TextAlignment Center = TextAlignment.CENTER;
    const SpriteType Texture = SpriteType.TEXTURE;
    const float DoorSpriteScale = 1.5f;
    const float TitleBarHeightPx = 64f;
    const float TextSize = 1.5f;
    const float BaseTextHeightPx = 37f;
    const string Font = "DEBUG";
    const string TitleFormat = "Whip's Auto Doors - v{0}";
    readonly string _titleText;

    readonly Vector2 _doorSpritePos = new Vector2(0, 20);

    Program _program;
    
    int _idx = 0;
    float[] _openRatios = new float[] {1f, 0.83f, 0.67f, 0.50f, 0.33f, 0.16f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.16f, 0.33f, 0.50f, 0.67f, 0.83f, 1f};

    bool _clearSpriteCache = false;
    IMyTextSurface _surface = null;

    public AutoDoorScreenManager(string version, Program program)
    {
        _titleText = string.Format(TitleFormat, version);
        _program = program;
        _surface = _program.Me.GetSurface(0);
    }

    public void RestartDraw()
    {
        _clearSpriteCache = !_clearSpriteCache;
        _idx = 0;
    }

    public void Draw()
    {
        if (_surface == null)
            return;
        
        float ratio = 1f;
        bool framesLeft = _idx < _openRatios.Length;
        if (framesLeft)
        {
            ratio = _openRatios[_idx];
            _idx++;
        }

        SetupDrawSurface(_surface);

        Vector2 screenCenter = _surface.TextureSize * 0.5f;
        Vector2 scale = _surface.SurfaceSize / 512f;
        float minScale = Math.Min(scale.X, scale.Y);

        using (var frame = _surface.DrawFrame())
        {
            if (_clearSpriteCache)
            {
                frame.Add(new MySprite());
            }
            DrawDoorSprites(frame, screenCenter + _doorSpritePos, minScale * DoorSpriteScale, ratio);
            DrawTitleBar(_surface, frame, minScale);
        }
    }

    #region Draw Helper Functions
    void DrawTitleBar(IMyTextSurface _surface, MySpriteDrawFrame frame, float scale)
    {
        float titleBarHeight = scale * TitleBarHeightPx;
        Vector2 topLeft = 0.5f * (_surface.TextureSize - _surface.SurfaceSize);
        Vector2 titleBarSize = new Vector2(_surface.TextureSize.X, titleBarHeight);
        Vector2 titleBarPos = topLeft + new Vector2(_surface.TextureSize.X * 0.5f, titleBarHeight * 0.5f);
        Vector2 titleBarTextPos = topLeft + new Vector2(_surface.TextureSize.X * 0.5f, 0.5f * (titleBarHeight - scale * BaseTextHeightPx));

        // Title bar
        frame.Add(new MySprite(
            Texture,
            "SquareSimple",
            titleBarPos,
            titleBarSize,
            _topBarColor,
            null,
            Center));

        // Title bar text
        frame.Add(new MySprite(
            SpriteType.TEXT,
            _titleText,
            titleBarTextPos,
            null,
            _white,
            Font,
            Center,
            TextSize * scale));
    }

    void SetupDrawSurface(IMyTextSurface _surface)
    {
        _surface.ScriptBackgroundColor = _black;
        _surface.ContentType = ContentType.SCRIPT;
        _surface.Script = "";
    }

    void DrawDoorSprites(MySpriteDrawFrame frame, Vector2 centerPos, float scale, float doorOpenRatio)
    {
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(50f,17f)*scale+centerPos, new Vector2(95f*doorOpenRatio,165f)*scale, _white, null, Center, 0f)); // door right
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(-50f,17f)*scale+centerPos, new Vector2(95f*doorOpenRatio,165f)*scale, _white, null, Center, 0f)); // door left
        frame.Add(new MySprite(Texture, "RightTriangle", new Vector2(-37f,-52f)*scale+centerPos, new Vector2(40f,40f)*scale, _black, null, Center, 1.5708f)); // door left cornerCopy
        frame.Add(new MySprite(Texture, "RightTriangle", new Vector2(37f,-52f)*scale+centerPos, new Vector2(40f,40f)*scale, _black, null, Center, -3.1416f)); // door right cornerCopy
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(80f,0f)*scale+centerPos, new Vector2(60f,210f)*scale, _black, null, Center, 0f)); // door frame right outline
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(-80f,0f)*scale+centerPos, new Vector2(60f,210f)*scale, _black, null, Center, 0f)); // door frame left outline
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(75f,0f)*scale+centerPos, new Vector2(40f,200f)*scale, _white, null, Center, 0f)); // door frame right
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(-75f,0f)*scale+centerPos, new Vector2(40f,200f)*scale, _white, null, Center, 0f)); // door frame left
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f,-85f)*scale+centerPos, new Vector2(110f,30f)*scale, _white, null, Center, 0f)); // door frame top
        frame.Add(new MySprite(Texture, "RightTriangle", new Vector2(40f,-55f)*scale+centerPos, new Vector2(30f,30f)*scale, _white, null, Center, -3.1416f)); // door right corner
        frame.Add(new MySprite(Texture, "RightTriangle", new Vector2(-40f,-55f)*scale+centerPos, new Vector2(30f,30f)*scale, _white, null, Center, 1.5708f)); // door left corner
    }
    #endregion
}