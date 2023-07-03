
/*     
/ //// / Whip's Multi-Group Weapon Salvo Script / //// /
PUBLIC RELEASE
HOWDY!  
______________________________________________________________________________________    
SETUP INSTRUCTIONS

    1.) Place this script in a programmable block (No timer is needed!)
    2.) Make a group of your weapons with the name "Salvo Group <unique tag>" where <unique tag> is a unique word or phrase
    3.) You can make as many salvo groups as you want!
______________________________________________________________________________________        
ARGUMENTS 

Type in these arguments without quotes. Arguments are case SENSITIVE.
These arguments can be input manually to the program argument field, 
this program's Custom Data, through timers, or through sensors.  

> BASIC ARGUMENT SYNTAX
    "<Group name>" <command 1>

> ADVANCED ARGUMENT SYNTAX
    You can also execute several commands to the same group.
    "<Group name>" <command 1> <command 2> ...

> AVAILABLE COMMANDS

    --rps <number>   
        Changes the rate of fire in rounds per second.
        * [Maximum RPS] = [Standard RPS] * [Number of sequenced weapons] 
            NOTE: The script will round this number, this is not a bug! 

    --rpm <number>    
        Changes the rate of fire in rounds per minute.  
        * [Maximum RPM] = 60 * [Standard RPM] * [Number of sequenced weapons] 
            NOTE: The script will round this number, this is not a bug! 

    --ticks <integer>  
        Sets number of ticks between shots (60 ticks = 1 sec)

    --burst <integer>
        Shoots a burst with the specified number of shots

    --default  
        Lets the script to set the fire rate automatically based on the number of     
        available weapons (using the default fixed rocket rate of fire). The script 
        will attempt to fire ALL sequenced weapons in the span of ONE second with 
        this particular setting.

    --fire_on   
        Toggles fire on only  

    --fire_off  
        Toggles fire off only  

    --fire_toggle 
        Toggles fire on/off  

______________________________________________________________________________________     
EXAMPLES: 

"Salvo Group 1" --fire_on 
    Toggles the weapons' firing on and use default rate of fire for "Salvo Group 1"

"Salvo Group 2" --default 
    Resets the default rate of fire for "Salvo Group 2"

"Salvo Group 3" --rpm 10 --fire_toggle 
    Sets the rate of fire to 10 rounds per minute and toggles firing for "Salvo Group 3" 

______________________________________________________________________________________     
AUTHOR'S NOTES:

If you have any questions feel free to post them on the workshop page!             

- Whiplash141
*/


























//=================================================
/////////DO NOT TOUCH ANYTHING BELOW HERE/////////
//=================================================

#region DONT FREAKING TOUCH THESE
const string VERSION = "45.1.6";
const string DATE = "2023/07/03";
#endregion

string _salvoGroupNameTag = "Salvo Group";

readonly StringBuilder _salvoGroupSB = new StringBuilder(512);
readonly MyIni _ini = new MyIni();

const string IniSectionTag = "Weapon Salvo Config";
const string IniNameTag = "Salvo group nametag";
const string IniRailgunSubtypes = "Railgun subtype names";
const string IniDrawTitleScreen = "Draw title screen";

RuntimeTracker _runtimeTracker;
Scheduler _scheduler;
ScheduledAction _scheduledSetup;
WeaponSalvoScreenManager _screenManager;
ArgumentParser _args = new ArgumentParser();
bool _drawTitleScreen = true;

Dictionary<string, WeaponSalvoGroup> _salvoGroupNameDict = new Dictionary<string, WeaponSalvoGroup>();
List<WeaponSalvoGroup> _salvoGroups = new List<WeaponSalvoGroup>();
List<IMyBlockGroup> _salvoBlockGroups = new List<IMyBlockGroup>();

Program()
{
    _screenManager = new WeaponSalvoScreenManager(VERSION, this);
    _scheduler = new Scheduler(this);

    _scheduledSetup = new ScheduledAction(Setup, 0.1);
    _scheduler.AddScheduledAction(SalvoLogic, 60);
    _scheduler.AddScheduledAction(PrintEcho, 1);
    _scheduler.AddScheduledAction(DrawTitleScreen, 3);
    _scheduler.AddScheduledAction(_screenManager.ForceDraw, 0.5);
    _scheduler.AddScheduledAction(_scheduledSetup);

    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    _runtimeTracker = new RuntimeTracker(this, 120);
    Setup();
}

void Main(string arg, UpdateType updateSource)
{
    _runtimeTracker.AddRuntime();

    if (!string.IsNullOrWhiteSpace(arg))
    {
        ProcessArguments(arg);
    }

    _scheduler.Update();
    _runtimeTracker.AddInstructions();
}

void DrawTitleScreen()
{
    if (_drawTitleScreen)
    {
        _screenManager.Draw();
    }
}

void PrintEcho()
{
    Echo(_salvoGroupSB.ToString());
    if (_salvoBlockGroups.Count == 0)
    {
        Echo($"ERROR: No groups containing the\n  name tag '{_salvoGroupNameTag}' were found\n");
    }
    Echo(_runtimeTracker.Write());
}

void ProcessArguments(string args)
{
    _args.TryParse(args);
    if (_args.ArgumentCount < 0)
    {
        return;
    }

    string groupName = _args.Argument(0);
    WeaponSalvoGroup salvoGroup;
    if (!_salvoGroupNameDict.TryGetValue(groupName, out salvoGroup))
        return;

    int idx = -1;
    if ((idx = _args.GetSwitchIndex("rps")) >= 0 && idx + 1 < _args.ArgumentCount)
    {
        float rps;
        if (float.TryParse(_args.Argument(idx + 1), out rps))
        {
            salvoGroup.SetRoundsPerSecond(rps);
        }
    }
    if ((idx = _args.GetSwitchIndex("rpm")) >= 0 && idx + 1 < _args.ArgumentCount)
    {
        float rpm;
        if (float.TryParse(_args.Argument(idx + 1), out rpm))
        {
            salvoGroup.SetRoundsPerMinute(rpm);
        }
    }
    if ((idx = _args.GetSwitchIndex("ticks")) >= 0 && idx + 1 < _args.ArgumentCount)
    {
        int ticks;
        if (int.TryParse(_args.Argument(idx + 1), out ticks))
        {
            salvoGroup.SetTickDelay(ticks);
        }
    }
    if ((idx = _args.GetSwitchIndex("burst")) >= 0 && idx + 1 < _args.ArgumentCount)
    {
        int burstLength;
        if (int.TryParse(_args.Argument(idx + 1), out burstLength))
        {
            salvoGroup.SetBurstLength(burstLength);
        }
    }
    if (_args.HasSwitch("fire_on"))
    {
        salvoGroup.Shoot = true;
    }
    if (_args.HasSwitch("fire_off"))
    {
        salvoGroup.Shoot = false;
    }
    if (_args.HasSwitch("fire_toggle"))
    {
        salvoGroup.Shoot = !salvoGroup.Shoot;
    }
    if (_args.HasSwitch("default"))
    {
        salvoGroup.SetDefaults();
    }
}

void SalvoLogic()
{
    _salvoGroupSB.Clear();
    _salvoGroupSB.AppendLine($"Whip's Weapon Salvo Code\n(Version {VERSION} - {DATE})\n\nNext block refresh in {Math.Max(0, _scheduledSetup.RunInterval - _scheduledSetup.TimeSinceLastRun):N0} seconds\n");

    foreach (var thisGroup in _salvoGroups)
    {
        thisGroup.SequenceWeapons();
        _salvoGroupSB.Append(thisGroup.EchoBuilder);
    }
}

#region Setup
void ProcessIni()
{
    _ini.Clear();
    if (_ini.TryParse(Me.CustomData))
    {
        _salvoGroupNameTag = _ini.Get(IniSectionTag, IniNameTag).ToString(_salvoGroupNameTag);
        _drawTitleScreen = _ini.Get(IniSectionTag, IniDrawTitleScreen).ToBoolean(_drawTitleScreen);
        MyIniHelper.GetStringList(IniSectionTag, IniRailgunSubtypes, _ini, SequencedWeapon.RailgunSubtypes);
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    _ini.Set(IniSectionTag, IniNameTag, _salvoGroupNameTag);
    _ini.Set(IniSectionTag, IniDrawTitleScreen, _drawTitleScreen);
    MyIniHelper.SetStringList(IniSectionTag, IniRailgunSubtypes, _ini, SequencedWeapon.RailgunSubtypes);
}

void WriteIni()
{
    string output = _ini.ToString();
    if (output != Me.CustomData)
    {
        Me.CustomData = output;
    }
}

void Setup()
{
    ProcessIni();
    GrabBlockGroups();
    WriteIni();
}

void GrabBlockGroups()
{
    _salvoBlockGroups.Clear();
    _salvoGroupNameDict.Clear();

    GridTerminalSystem.GetBlockGroups(_salvoBlockGroups, x => x.Name.ToLower().Contains(_salvoGroupNameTag.ToLower()));

    if (_salvoBlockGroups.Count == 0)
    {
        return;
    }

    // Removes salvo groups that dont exist any more
    _salvoGroups.RemoveAll(x => !_salvoBlockGroups.Contains(x.BlockGroup));

    // Update existing salvo groups
    foreach (var salvoGroup in _salvoGroups)
    {
        salvoGroup.GetBlocks(_ini);
        _salvoGroupNameDict[salvoGroup.BlockGroup.Name] = salvoGroup;
    }

    // Add NEW salvo groups to list
    foreach (var group in _salvoBlockGroups)
    {
        if (!_salvoGroupNameDict.ContainsKey(group.Name))
        {
            var salvoGroup = new WeaponSalvoGroup(group, _ini, this);
            _salvoGroups.Add(salvoGroup);
            _salvoGroupNameDict.Add(group.Name, salvoGroup);
        }
    }
}
#endregion

class WeaponSalvoGroup
{
    #region Fields
    public readonly StringBuilder EchoBuilder = new StringBuilder();
    readonly Program _program = null;

    MyIni _ini = new MyIni();

    enum RofUnits { None, Ticks, RPM, RPS }
    RofUnits _customRofUnits = RofUnits.RPM;

    const string
        IniUseCustomRof = "Use custom rate of fire",
        IniCustomRofUnits = "Custom rate of fire units",
        IniCommentCustomRofUnits = " Valid units are: Ticks, RPM, or RPS",
        IniCustomRof = "Custom rate of fire";

    int _weaponCount = 0;
    int _ticksSinceLastShot = 0;
    int _ticksBetweenShots = 1;
    int _burstCount = 0;
    bool _readyToFire = false;
    bool _isShooting = false;
    bool _manualOverride = false;
    bool _shouldBurst = false;
    bool _init = false;
    float _customRof = 0;

    SwapList<IMyUserControllableGun> _gunsToProcess = new SwapList<IMyUserControllableGun>();
    List<SequencedWeapon> _weapons = new List<SequencedWeapon>();
    #endregion

    #region Properties
    public IMyBlockGroup BlockGroup { get; private set; } = null;

    public bool Shoot { get; set; } = false;

    int TicksBetweenShots
    {
        get
        {
            return _ticksBetweenShots;
        }
        set
        {
            _ticksBetweenShots = Math.Max(1, value);
        }
    }

    float DesiredRPM
    {
        get
        {
            return 3600f / TicksBetweenShots;
        }
    }
    #endregion

    #region Set Methods
    public void SetRoundsPerMinute(float rpm)
    {
        float ticks = 3600f / rpm;
        TicksBetweenShots = (int)Math.Ceiling(ticks);
        _manualOverride = true;
        _customRof = rpm;
        _customRofUnits = RofUnits.RPM;
        ProcessIni();
    }

    public void SetRoundsPerSecond(float rps)
    {
        float ticks = 60f / rps;
        TicksBetweenShots = (int)Math.Ceiling(ticks);
        _manualOverride = true;
        _customRof = rps;
        _customRofUnits = RofUnits.RPS;
        ProcessIni();
    }

    public void SetTickDelay(int delayTicks)
    {
        TicksBetweenShots = delayTicks;
        _manualOverride = true;
        _customRof = delayTicks;
        _customRofUnits = RofUnits.Ticks;
        ProcessIni();
    }

    public void SetBurstLength(int burstLength)
    {
        _burstCount = burstLength;
        Shoot = true;
        _shouldBurst = true;
    }

    public void SetDefaults()
    {
        _manualOverride = false;

        if (_weapons.Count > 0)
        {
            float defaultRpm = GetDefaultWeaponRateOfFireRPM(_weapons[0].Weapon) * _weapons.Count;
            float ticks = 3600f / defaultRpm;
            TicksBetweenShots = (int)Math.Ceiling(ticks);
        }
        ProcessIni();
    }

    static float GetDefaultWeaponRateOfFireRPM(IMyUserControllableGun gun)
    {
        switch (gun.BlockDefinition.SubtypeId)
        {
            case "SmallBlockAutoCannon":
                return 150f;
            case "SmallBlockMediumCalibreGun":
                return 10f;
            case "LargeBlockLargeCalibreGun":
                return 5f;
            case "LargeRailgun":
                return 1.0133f;
            case "SmallRailgun":
                return 3f;
            default:
                if (gun is IMySmallGatlingGun)
                {
                    return 700f;
                }
                else if (gun is IMySmallMissileLauncher)
                {
                    return gun.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 120f : 60f;
                }
                return 60f;
        }
    }
    #endregion

    public WeaponSalvoGroup(IMyBlockGroup group, MyIni ini, Program program)
    {
        BlockGroup = group;
        _program = program;
        GetBlocks(ini);
        _init = true;
        _ticksSinceLastShot = TicksBetweenShots;
        _readyToFire = true;
    }

    void Echo(string echoStr)
    {
        EchoBuilder.AppendLine(echoStr);
    }

    #region Ini
    public void ProcessIni()
    {
        _ini.Clear();
        _ini.TryParse(_program.Me.CustomData);
        ProcessIni(_ini, false);

        string output = _ini.ToString();
        if (output != _program.Me.CustomData)
        {
            _program.Me.CustomData = output;
        }
    }

    public void ProcessIni(MyIni ini, bool read)
    {
        string sectionName = BlockGroup.Name;

        // Read
        if (read)
        {
            _manualOverride = ini.Get(sectionName, IniUseCustomRof).ToBoolean(_manualOverride);
            _customRof = ini.Get(sectionName, IniCustomRof).ToSingle(_customRof);
            string customRofUnitsString = ini.Get(sectionName, IniCustomRofUnits).ToString("");
            if (!Enum.TryParse(customRofUnitsString, out _customRofUnits))
            {
                _customRofUnits = RofUnits.RPM;
            }
            if (_manualOverride)
            {
                switch (_customRofUnits)
                {
                    case RofUnits.RPM:
                        SetRoundsPerMinute(_customRof);
                        break;
                    case RofUnits.RPS:
                        SetRoundsPerSecond(_customRof);
                        break;
                    case RofUnits.Ticks:
                        _customRof = (float)Math.Round(_customRof);
                        SetTickDelay((int)_customRof);
                        break;
                }
            }
        }

        // Write
        ini.Set(sectionName, IniUseCustomRof, _manualOverride);
        ini.Set(sectionName, IniCustomRof, _customRof);
        ini.Set(sectionName, IniCustomRofUnits, _customRofUnits.ToString());
        ini.SetComment(sectionName, IniCustomRofUnits, IniCommentCustomRofUnits);
    }
    #endregion

    bool CollectBlocks(IMyTerminalBlock x)
    {
        var gun = (IMyUserControllableGun)x;
        if (!(x is IMyLargeTurretBase) && _program.Me.IsSameConstructAs(x))
        {
            _gunsToProcess.Active.Add(gun);
        }
        return false;
    }

    public void GetBlocks(MyIni ini)
    {
        _gunsToProcess.Swap();
        _gunsToProcess.Active.Clear();

        // Get current guns
        BlockGroup.GetBlocksOfType<IMyUserControllableGun>(null, CollectBlocks);

        // Remove no-longer existing guns
        _weapons.RemoveAll(x => !_gunsToProcess.Active.Contains(x.Weapon));

        // Add new guns
        foreach (var g in _gunsToProcess.Active)
        {
            if (!_gunsToProcess.Inactive.Contains(g))
            {
                _weapons.Add(new SequencedWeapon(g));
            }
        }

        // Sorting alphabetically
        _weapons.Sort((gun1, gun2) => gun1.Weapon.CustomName.CompareTo(gun2.Weapon.CustomName));

        if (!_manualOverride)
        {
            SetDefaults();
        }

        if (!_init)
        {
            _customRof = DesiredRPM;
        }

        ProcessIni(ini, true);
    }

    public void SequenceWeapons()
    {
        EchoBuilder.Clear();
        Echo($"'{BlockGroup.Name}'");
        if (_weapons.Count == 0)
        {
            Echo("  - ERROR: No weapons in group!\n");
            return;
        }

        if (!_readyToFire && _ticksSinceLastShot >= TicksBetweenShots)
        {
            _weaponCount = ++_weaponCount % _weapons.Count;
            _readyToFire = true;
        }
        _ticksSinceLastShot++;

        var weaponToFire = _weapons[_weaponCount % _weapons.Count];
        weaponToFire.Weapon.Enabled = true;

        if (_readyToFire)
        {
            //Checks if guns are being fired
            _isShooting = false;

            if (weaponToFire.Weapon.IsShooting)
            {
                _isShooting = true;
                _ticksSinceLastShot = 0;
                _readyToFire = false;
                weaponToFire.OnShoot();
            }
            else if (Shoot)
            {
                _isShooting = true;
                _ticksSinceLastShot = 0;
                _readyToFire = false;
                weaponToFire.OnShoot();
                weaponToFire.Weapon.ShootOnce();
            }

            if (_shouldBurst)
            {
                _burstCount--;
                if (_burstCount <= 0)
                {
                    _shouldBurst = false;
                    Shoot = false;
                    _burstCount = 0; 
                }
            }
        }
        
        if (weaponToFire.Weapon.Closed)
        {
            _weapons.Remove(weaponToFire);
        }
        else if (!weaponToFire.Weapon.IsFunctional)
        {
            _weaponCount = ++_weaponCount % _weapons.Count;
        }

        // Turn other weapons off
        foreach (var weapon in _weapons)
        {
            if (weapon.Weapon != weaponToFire.Weapon && weapon.CanDisable)
            {
                weapon.Weapon.Enabled = false;
            }
        }

        string output = $"  - No. Weapons: {_weapons.Count}\n  - Weapon Index: {_weaponCount}\n  - Rate of Fire: {DesiredRPM} -> {3600f / TicksBetweenShots:N1} RPM\n  - Delay Between Shots: {TicksBetweenShots} tick(s)\n  - Current Tick: {Math.Min(TicksBetweenShots, _ticksSinceLastShot)} tick(s)\n  - Burst Count: {_burstCount}\n  - Shooting: {_isShooting}\n  - Toggle Fire: {Shoot}\n  - Using Defaults: {!_manualOverride}\n";
        Echo(output);
    }
}

class SequencedWeapon
{
    public readonly IMyUserControllableGun Weapon;
    public readonly bool IsRailgun;

    static readonly MyDefinitionId ElectricityId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
    readonly MyResourceSinkComponent _sink;
    const float IdlePowerDraw = 0.0002f;
    const float Epsilon = 1e-6f;
    bool _waitingForRecharge = false;

    public static List<string> RailgunSubtypes { get; private set; } = new List<string>()
{
"SmallRailgun",
"LargeRailgun",
};

    public SequencedWeapon(IMyUserControllableGun gun)
    {
        Weapon = gun;
        _sink = Weapon.Components.Get<MyResourceSinkComponent>();
        IsRailgun = RailgunSubtypes.Contains(gun.BlockDefinition.SubtypeId);
    }

    public void OnShoot()
    {
        _waitingForRecharge = IsRailgun;
    }

    public bool CanDisable
    {
        get 
        {
            if (!IsRailgun)
            {
                return true;
            }
            if (IsRecharging(Weapon))
            {
                _waitingForRecharge = false;
            }
            return !IsRecharging(Weapon) && !_waitingForRecharge;   
        }
        
    }

    bool IsRecharging(IMyUserControllableGun gun)
    {
        if (_sink == null)
        {
            return false;
        }
        return _sink.MaxRequiredInputByType(ElectricityId) > (IdlePowerDraw + Epsilon);
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

class ArgumentParser
{
    public int ArgumentCount
    {
        get;
        private set;
    } = 0;

    public string ErrorMessage
    {
        get;
        private set;
    }

    const char Quote = '"';
    List<string> _arguments = new List<string>();
    HashSet<string> _argHash = new HashSet<string>();
    HashSet<string> _switchHash = new HashSet<string>();
    Dictionary<string, int> _switchIndexDict = new Dictionary<string, int>();

    enum ReturnCode { EndOfStream = -1, Nominal = 0, NoArgs = 1, NonAlphaSwitch = 2, NoEndQuote = 3, NoSwitchName = 4 }

    string _raw;

    public bool InRange(int index)
    {
        if (index < 0 || index >= _arguments.Count)
        {
            return false;
        }
        return true;
    }

    public string Argument(int index)
    {
        if (!InRange(index))
        {
            return "";
        }

        return _arguments[index];
    }

    public bool IsSwitch(int index)
    {
        if (!InRange(index))
        {
            return false;
        }

        return _switchHash.Contains(_arguments[index]);
    }

    public int GetSwitchIndex(string switchName)
    {
        int idx;
        if (_switchIndexDict.TryGetValue(switchName, out idx))
        {
            return idx;
        }
        return -1;
    }

    ReturnCode GetArgStartIdx(int startIdx, out int idx, out bool isQuoted, out bool isSwitch)
    {
        idx = -1;
        isQuoted = false;
        isSwitch = false;
        for (int i = startIdx; i < _raw.Length; ++i)
        {
            char c = _raw[i];
            if (c != ' ')
            {
                if (c == Quote)
                {
                    isQuoted = true;
                    idx = i + 1;
                    return ReturnCode.Nominal;
                }
                if (c == '-' && i + 1 < _raw.Length && _raw[i + 1] == '-')
                {
                    isSwitch = true;
                    idx = i + 2;
                    return ReturnCode.Nominal;
                }
                idx = i;
                return ReturnCode.Nominal;
            }
        }
        return ReturnCode.NoArgs;
    }

    ReturnCode GetArgLength(int startIdx, bool isQuoted, bool isSwitch, out int length)
    {
        length = 0;
        for (int i = startIdx; i < _raw.Length; ++i)
        {
            char c = _raw[i];
            if (isQuoted)
            {
                if (c == Quote)
                {
                    return ReturnCode.Nominal;
                }
            }
            else
            {
                if (c == ' ')
                {
                    if (isSwitch && length == 0)
                    {
                        return ReturnCode.NoSwitchName;
                    }
                    return ReturnCode.Nominal;
                }

                if (isSwitch)
                {
                    if (!char.IsLetter(c) && c != '_')
                    {
                        return ReturnCode.NonAlphaSwitch;
                    }
                }
            }
            length++;
        }
        if (isQuoted)
        {
            return ReturnCode.NoEndQuote;
        }
        if (length == 0 && isSwitch)
        {
            return ReturnCode.NoSwitchName;
        }
        return ReturnCode.EndOfStream; // Reached end of stream
    }

    void ClearArguments()
    {
        ArgumentCount = 0;
        _arguments.Clear();
        _switchHash.Clear();
        _argHash.Clear();
        _switchIndexDict.Clear();
    }

    public bool HasArgument(string argName)
    {
        return _argHash.Contains(argName);
    }

    public bool HasSwitch(string switchName)
    {
        return _switchHash.Contains(switchName);
    }

    public bool TryParse(string arg)
    {
        ReturnCode status;

        _raw = arg;
        ClearArguments();

        int idx = 0;
        while (idx < _raw.Length)
        {
            bool isQuoted, isSwitch;
            int startIdx, length;
            string argString;
            status = GetArgStartIdx(idx, out startIdx, out isQuoted, out isSwitch);
            if (status == ReturnCode.NoArgs)
            {
                ErrorMessage = "";
                return true;
            }

            status = GetArgLength(startIdx, isQuoted, isSwitch, out length);
            if (status == ReturnCode.NoEndQuote)
            {
                ErrorMessage = $"No closing quote found! (idx: {startIdx})";
                ClearArguments();
                return false;
            }
            else if (status == ReturnCode.NonAlphaSwitch)
            {
                ErrorMessage = $"Switch can not contain non-alphabet characters! (idx: {startIdx})";
                ClearArguments();
                return false;
            }
            else if (status == ReturnCode.NoSwitchName)
            {
                ErrorMessage = $"Switch does not have a name (idx: {startIdx})";
                ClearArguments();
                return false;
            }
            else if (status == ReturnCode.EndOfStream) // End of stream
            {
                argString = _raw.Substring(startIdx);
                _arguments.Add(argString);
                _argHash.Add(argString);
                if (isSwitch)
                {
                    _switchHash.Add(argString);
                    _switchIndexDict[argString] = ArgumentCount;
                }
                ArgumentCount++;
                ErrorMessage = "";
                return true;
            }

            argString = _raw.Substring(startIdx, length);
            _arguments.Add(argString);
            _argHash.Add(argString);
            if (isSwitch)
            {
                _switchHash.Add(argString);
                _switchIndexDict[argString] = ArgumentCount;
            }
            ArgumentCount++;
            idx = startIdx + length;
            if (isQuoted)
            {
                idx++; // Move past the quote
            }
        }
        ErrorMessage = "";
        return true;
    }
}

class WeaponSalvoScreenManager
{
    readonly Color _topBarColor = new Color(25, 25, 25);
    readonly Color _white = new Color(200, 200, 200);
    readonly Color _black = Color.Black;

    const TextAlignment Center = TextAlignment.CENTER;
    const SpriteType Texture = SpriteType.TEXTURE;
    const float ArtillerySpriteScale = 0.8f;
    const float TitleBarHeightPx = 64f;
    const float TextSize = 1.3f;
    const float BaseTextHeightPx = 37f;
    const string Font = "DEBUG";
    const string TitleFormat = "Whip's Weapon Salvo - v{0}";
    readonly string _titleText;

    Program _program;

    int _idx = 0;
    Vector2 _topArtillery = new Vector2(220, -80);
    Vector2 _midArtillery = new Vector2(220, 20);
    Vector2 _bottomArtillery = new Vector2(220, 120);

    bool _clearSpriteCache = false;
    IMyTextSurface _surface = null;

    public WeaponSalvoScreenManager(string version, Program program)
    {
        _titleText = string.Format(TitleFormat, version);
        _program = program;
        _surface = _program.Me.GetSurface(0);
    }

    public void ForceDraw()
    {
        _clearSpriteCache = !_clearSpriteCache;
    }

    public void Draw()
    {
        if (_surface == null)
            return;

        _idx = ++_idx % 3;

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

            DrawArtilleryCannon(frame, screenCenter + _topArtillery * minScale, minScale * ArtillerySpriteScale, _idx == 0);
            DrawArtilleryCannon(frame, screenCenter + _midArtillery * minScale, minScale * ArtillerySpriteScale, _idx == 1);
            DrawArtilleryCannon(frame, screenCenter + _bottomArtillery * minScale, minScale * ArtillerySpriteScale, _idx == 2);

            DrawTitleBar(_surface, frame, minScale);
        }
    }

    public void Animate()
    {
        ForceDraw();
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

    public void DrawArtilleryCannon(MySpriteDrawFrame frame, Vector2 centerPos, float scale, bool drawMuzzleFlash)
    {
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-90f, 0f) * scale + centerPos, new Vector2(90f, 80f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f)); // gun body
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(-175f, -25f) * scale + centerPos, new Vector2(30f, 80f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, -1.5708f)); // gun front slope
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-150f, 12f) * scale + centerPos, new Vector2(50f, 45f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f)); // gun bottom
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-195f, 2f) * scale + centerPos, new Vector2(40f, 25f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f)); // gun mid
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(-185f, 24f) * scale + centerPos, new Vector2(20f, 20f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 3.1416f)); // gun bottom slope
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-345f, 2f) * scale + centerPos, new Vector2(200f, 10f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f)); // barrel
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-230f, 2f) * scale + centerPos, new Vector2(20f, 16f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f)); // barrel base
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-88f, 2f) * scale + centerPos, new Vector2(60f, 60f) * scale, new Color(0, 0, 0, 255), null, TextAlignment.CENTER, 0f)); // conveyor outline
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-88f, 2f) * scale + centerPos, new Vector2(50f, 50f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f)); // conveyor center
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-88f, -3f) * scale + centerPos, new Vector2(50f, 5f) * scale, new Color(0, 0, 0, 255), null, TextAlignment.CENTER, 0f)); // conveyor crossCopy
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-88f, 7f) * scale + centerPos, new Vector2(50f, 5f) * scale, new Color(0, 0, 0, 255), null, TextAlignment.CENTER, 0f)); // conveyor cross
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(-134f, 50f) * scale + centerPos, new Vector2(10f, 10f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, -1.5708f)); // gun bottom slope
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-90f, 50f) * scale + centerPos, new Vector2(80f, 10f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f)); // gun bottom
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-174f, 10f) * scale + centerPos, new Vector2(60f, 5f) * scale, new Color(0, 0, 0, 255), null, TextAlignment.CENTER, -0.3491f)); // stripe diag
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-141f, 0f) * scale + centerPos, new Vector2(12f, 5f) * scale, new Color(0, 0, 0, 255), null, TextAlignment.CENTER, 0f)); // stripe horizontal
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-134f, 0f) * scale + centerPos, new Vector2(5f, 82f) * scale, new Color(0, 0, 0, 255), null, TextAlignment.CENTER, 0f)); // stripe vertical
        if (drawMuzzleFlash)
        {
            frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(-480f, 7f) * scale + centerPos, new Vector2(60f, 20f) * scale, new Color(255, 128, 0, 255), null, TextAlignment.CENTER, -0.1745f)); // muzzle flash bottom
            frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(-482f, -3f) * scale + centerPos, new Vector2(60f, 20f) * scale, new Color(255, 128, 0, 255), null, TextAlignment.CENTER, 0.1745f)); // muzzle flash top
            frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(-501f, 2f) * scale + centerPos, new Vector2(100f, 20f) * scale, new Color(255, 128, 0, 255), null, TextAlignment.CENTER, 0f)); // muzzle flash main
            frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(-470f, 2f) * scale + centerPos, new Vector2(40f, 20f) * scale, new Color(255, 255, 0, 255), null, TextAlignment.CENTER, 0f)); // muzzle flash center            
        }
    }

    #endregion
}

public static class MyIniHelper
{
    #region List<string>
    /// <summary>
    /// Deserializes a List<string> from MyIni
    /// </summary>
    public static void GetStringList(string section, string name, MyIni ini, List<string> list)
    {
        string raw = ini.Get(section, name).ToString(null);
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Preserve contents
            return;
        }

        list.Clear();
        string[] split = raw.Split('\n');
        foreach (var s in split)
        {
            list.Add(s);
        }
    }

    /// <summary>
    /// Serializes a List<string> to MyIni
    /// </summary>
    public static void SetStringList(string section, string name, MyIni ini, List<string> list)
    {
        string output = string.Join($"\n", list);
        ini.Set(section, name, output);
    }
    #endregion
}

class SwapList<T>
{
    public List<T> Active
    {
        get
        {
            return _swap ? _list1 : _list2;
        }
    }

    public List<T> Inactive
    {
        get
        {
            return _swap ? _list2 : _list1;
        }
    }

    bool _swap = false;
    readonly List<T> _list1;
    readonly List<T> _list2;

    public SwapList(int capacity = 16)
    {
        _list1 = new List<T>(capacity);
        _list2 = new List<T>(capacity);
    }

    public void Swap()
    {
        _swap = !_swap;
    }
}
