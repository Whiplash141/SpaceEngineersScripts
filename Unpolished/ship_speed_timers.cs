/*
/ //// / Whip's Ship Speed Timers / //// /

INSTRUCTIONS:
    1. Add timers to a group named "Speed Timers"
    2. Place this script in a programmable block
    3. Configure the custom data of the timers
    4. Recompile to process any block or custom data changes.
    














===================================
DO NOT EDIT VARIABLES IN THE SCRIPT
       USE THE CUSTOM DATA
===================================














*/


public const string Version = "1.0.0",
                    Date = "2022/07/08";

public const string ScriptName = "Speed Timers";
const string IniSectionGeneral = ScriptName + " - General Config";
const string IniKeyGroupName = "Timer group name";

string _timerGroupName = ScriptName;

MyIni _ini = new MyIni();
List<IMyShipController> _shipControllers = new List<IMyShipController>();
List<SpeedTimer> _speedTimers = new List<SpeedTimer>();
ReturnCode _returnCode;
Vector3D _lastPosition = Vector3D.Zero;
RuntimeTracker _runtimeTracker;

enum ReturnCode { Success = 0, NoShipControllers = 1, NoGroup = 1 << 1, NoTimers = 1 << 2, Errors = NoGroup | NoTimers }

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    _runtimeTracker = new RuntimeTracker(this);
    _returnCode = GetBlocks();
}

void Main(string arg, UpdateType update)
{
    _runtimeTracker.AddRuntime();
    if ((update & UpdateType.Update10) == 0)
        return;

    PrintStatus();

    if ((_returnCode & ReturnCode.Errors) != 0)
        return;
    
    double? speed = null;
    if ((_returnCode & ReturnCode.NoShipControllers) == 0)
    {
        foreach (var sc in _shipControllers)
        {
            if (GridTerminalSystem.CanAccess(sc))
            {
                speed = sc.GetShipSpeed();
                break;
            }
        }
    }

    if (!speed.HasValue)
    {
        var velocity = (Me.GetPosition() - _lastPosition) * 6;
        speed = velocity.Length();
    }

    foreach (var st in _speedTimers)
    {
        st.Update(speed.Value);
    }
    _runtimeTracker.AddInstructions();
}

StringBuilder _echo = new StringBuilder(1024);
void PrintStatus()
{
    _echo.Append($"{ScriptName}\n(Version {Version} - {Date})\n\n");
    _echo.Append("Recompile to process block and custom data changes.\n\n");
    _echo.Append($"Last run: {DateTime.Now}\n");
    _echo.Append($"Speed timers: {_speedTimers.Count}\n");
    _echo.Append($"Precise velocity: {_shipControllers.Count != 0}\n");

    if ((_returnCode & ReturnCode.Errors) != 0)
    {
        _echo.Append("\nERRORS:\n");
        if ((_returnCode & ReturnCode.NoGroup) != 0)
        {
            _echo.Append($"- No block group named '{_timerGroupName}'\n");
        }
        else if ((_returnCode & ReturnCode.NoTimers) != 0)
        {
            _echo.Append($"- No timers in '{_timerGroupName}' group\n");
        }
    }
    _echo.Append("\n");
    _echo.Append(_runtimeTracker.Write());

    Echo(_echo.ToString());
    _echo.Clear();
}

ReturnCode GetBlocks()
{
    _shipControllers.Clear();
    _speedTimers.Clear();

    _ini.Clear();
    if (_ini.TryParse(Me.CustomData))
    {
        _timerGroupName = _ini.Get(IniSectionGeneral, IniKeyGroupName).ToString(_timerGroupName);
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    _ini.Set(IniSectionGeneral, IniKeyGroupName, _timerGroupName);

    string content = _ini.ToString();
    if (content != Me.CustomData)
    {
        Me.CustomData = content;
    }

    _returnCode = ReturnCode.Success;
    GridTerminalSystem.GetBlocksOfType(_shipControllers, b => b.IsSameConstructAs(Me));
    if (_shipControllers.Count == 0)
    {
        _returnCode |= ReturnCode.NoShipControllers;
    }
    var group = GridTerminalSystem.GetBlockGroupWithName(_timerGroupName);
    if (group == null)
    {
        _returnCode |= ReturnCode.NoGroup;
    }
    else
    {
        group.GetBlocksOfType<IMyTimerBlock>(null, b => {
            _speedTimers.Add(new SpeedTimer(b, _ini));
            return false;
        });

        if (_speedTimers.Count == 0)
        {
            _returnCode |= ReturnCode.NoTimers;
        }
    }

    return _returnCode;
}

public class SpeedTimer
{
    const string IniSection = ScriptName + " - Timer Config";
    const string IniKeyThresholdDirection = "Trigger when speed is";
    const string IniCommentThresholdDirection = " Accepted values are BELOW or ABOVE";
    const string IniKeySpeedThreshold = "Speed threshold (m/s)";

    IMyTimerBlock _timer;
    MyIni _ini;
    ThresholdDirection _direction = ThresholdDirection.Above;
    double _speedThreshold = 50;
    enum ThresholdDirection { Below, Above }
    bool _lastThresholdMet = false;

    public SpeedTimer(IMyTimerBlock timer, MyIni ini)
    {
        _timer = timer;
        _ini = ini;
        ProcessIni();
    }

    void ProcessIni()
    {
        _ini.Clear();
        if (_ini.TryParse(_timer.CustomData))
        {
            string dirnStr = _ini.Get(IniSection, IniKeyThresholdDirection).ToString();
            _direction = StringToDirn(dirnStr);
            _speedThreshold = _ini.Get(IniSection, IniKeySpeedThreshold).ToDouble(_speedThreshold);
        }
        else if (!string.IsNullOrWhiteSpace(_timer.CustomData))
        {
            _ini.EndContent = _timer.CustomData;
        }

        _ini.Set(IniSection, IniKeyThresholdDirection, DirnToString(_direction));
        _ini.Set(IniSection, IniKeySpeedThreshold, _speedThreshold);
        _ini.SetComment(IniSection, IniKeyThresholdDirection, IniCommentThresholdDirection);

        string content = _ini.ToString();
        if (content != _timer.CustomData)
        {
            _timer.CustomData = content;
        }
    }

    public void Update(double speed)
    {
        bool thresholdMet = false;
        if (_direction == ThresholdDirection.Above)
        {
            thresholdMet = speed > _speedThreshold;
        }
        else
        {
            thresholdMet = speed < _speedThreshold;
        }

        // Only trigger once per crossing
        if (thresholdMet && thresholdMet != _lastThresholdMet)
        {
            _timer.Trigger();
        }
        _lastThresholdMet = thresholdMet;
    }

    ThresholdDirection StringToDirn(string val)
    {
        switch (val.ToUpperInvariant())
        {
            default:
            case "ABOVE":
                return ThresholdDirection.Above;
            case "BELOW":
                return ThresholdDirection.Below;
        }
    }

    string DirnToString(ThresholdDirection val)
    {
        switch (val)
        {
            default:
            case ThresholdDirection.Above:
                return "ABOVE";
            case ThresholdDirection.Below:
                return "BELOW";
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

