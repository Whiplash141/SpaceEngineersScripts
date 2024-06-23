const string kVersion = "1.0.0";
const string kDate = "2024/06/23";

enum ThresholdType { GreaterThanOrEqual, LessThanOrEqual }
enum TriggerType { Start, TriggerNow }

class ProjectorTimer
{
    class TimerConfig : ConfigSection
    {
        public ConfigString ProjectorGroupName = new ConfigString("Projector group name", "", " Name of the projector group to monitor");
        public ConfigEnum<ThresholdType> Threshold = new ConfigEnum<ThresholdType>("Threshold type", ThresholdType.GreaterThanOrEqual, " Valid values: GreaterThanOrEqual or LessThanOrEqual" );
        public ConfigFloat ThresholdValue = new ConfigFloat("Built blocks threshold (%)", 90);
        public ConfigEnum<TriggerType> Trigger = new ConfigEnum<TriggerType>("Trigger type", TriggerType.TriggerNow, " Valid values: Start or TriggerNow");

        public TimerConfig() : base("Projector Timer - Timer Config")
        {
            AddValues(ProjectorGroupName, Threshold, ThresholdValue, Trigger);
        }
    }
    
    public enum State { Idle = 1, Active = 2, MissingGroup = 4, MissingProjectors = 8, Errors = MissingGroup | MissingProjectors }
    TimerConfig mConfig = new TimerConfig();
    List<IMyProjector> mProjectors = new List<IMyProjector>();

    MyIni mIni = new MyIni();
    readonly Program mProgram;
    readonly IMyTimerBlock mTimer;

    public string ProjectorGroupName => mConfig.ProjectorGroupName;
    public StringBuilder StatusBuilder = new StringBuilder();

    State mState;
    public State CurrentState
    {
        get
        {
            return mState;
        }
        set
        {
            if (value != mState)
            {
                if (value == State.Active)
                {
                    TriggerTimer();
                }
                mState = value;
            }
        }
    }

    public ProjectorTimer(Program pProgram, IMyTimerBlock pTimer)
    {
        mProgram = pProgram;
        mTimer = pTimer;

        mIni.TryParse(mTimer.CustomData);

        mConfig.Update(mIni);

        string output = mIni.ToString();
        if (output != mTimer.CustomData)
        {
            mTimer.CustomData = output;
        }

        var group = mProgram.GridTerminalSystem.GetBlockGroupWithName(mConfig.ProjectorGroupName);
        if (group == null)
        {
            CurrentState = State.MissingGroup;
            return;
        }

        group.GetBlocksOfType(mProjectors);
        if (mProjectors.Count == 0)
        {
            CurrentState = State.MissingProjectors;
            return;
        }

        CurrentState = State.Idle;
    }

    public void Update()
    {
        StatusBuilder.Clear();
        StatusBuilder.AppendLine($"=== {mTimer.CustomName} ===\n  Projector group: \"{mConfig.ProjectorGroupName}\"");
        
        if ((CurrentState & State.Errors) != 0)
        {
            switch (CurrentState)
            {
                case ProjectorTimer.State.MissingGroup:
                    StatusBuilder.AppendLine("  Error: Projector group not found");
                    break;
                case ProjectorTimer.State.MissingProjectors:
                    StatusBuilder.AppendLine("  Error: No projectors found in projector group");
                    break;
                default:
                    break;
            }
            return;
        }

        bool active = GetConditionActive();
        CurrentState = active ? State.Active : State.Idle;

        StatusBuilder.AppendLine($"  State: {CurrentState}");
    }

    void TriggerTimer()
    {
        switch (mConfig.Trigger.Value)
        {
            case TriggerType.Start:
                mTimer.StartCountdown();
                break;
            case TriggerType.TriggerNow:
                mTimer.Trigger();
                break;
            default:
                break;
        }
    }

    bool GetConditionActive()
    {
        int totalBlocks = 0;
        int remainingBlocks = 0;
        foreach (var projector in mProjectors)
        {
            totalBlocks += projector.TotalBlocks;
            remainingBlocks += projector.RemainingBlocks;
        }

        float builtPercentage = 100f * (float)(totalBlocks - remainingBlocks) / (float)Math.Max(1, totalBlocks);

        StatusBuilder.AppendLine($"  Build status: {builtPercentage}% ({totalBlocks - remainingBlocks}/{totalBlocks})");
        
        switch (mConfig.Threshold.Value)
        {
            case ThresholdType.GreaterThanOrEqual:
                return builtPercentage >= mConfig.ThresholdValue;
            case ThresholdType.LessThanOrEqual:
            default:
                return builtPercentage <= mConfig.ThresholdValue;
        }

    }
}

const string kConfigSectionGeneral = "Projector Timers - General Config";
ConfigString mTimerGroupName = new ConfigString("Timer group name", "Projector Timers");
List<ProjectorTimer> mProjectorTimers = new List<ProjectorTimer>();
MyIni mIni = new MyIni();
StringBuilder mErrorBuilder = new StringBuilder();
StringBuilder mStatusBuilder = new StringBuilder();
readonly string mErrorString;
int mPrintCount = 0;

Program()
{
    mIni.TryParse(Me.CustomData);
    mTimerGroupName.Update(mIni, kConfigSectionGeneral);
    string output = mIni.ToString();
    if (output != Me.CustomData)
    {
        Me.CustomData = output;
    }

    var group = GridTerminalSystem.GetBlockGroupWithName(mTimerGroupName);
    if (group == null)
    {
        Echo($"Error: Block group named \"{mTimerGroupName}\" not found");
        return;
    }

    group.GetBlocksOfType<IMyTimerBlock>(null, CollectBlocks);
    mErrorString = mErrorBuilder.ToString();

    PrintDetailedInfo();

    if (mProjectorTimers.Count > 0)
    {
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
    }
}

void PrintDetailedInfo()
{
    Echo($"Whip's Projector Timers\n({kVersion} - {kDate})\n");
    Echo("Recompile to process custom data or block changes\n");
    Echo($"{mProjectorTimers.Count} projector timers");
    Echo("");
    Echo(mStatusBuilder.ToString());
    Echo("");
    Echo(mErrorString);
}

bool CollectBlocks(IMyTimerBlock b)
{
    var timer = new ProjectorTimer(this, b);
    mProjectorTimers.Add(timer);
    return false;
}

void Main(string args, UpdateType source)
{
    if ((source & UpdateType.Update10) == 0)
    {
        return;
    }

    mStatusBuilder.Clear();

    foreach (var t in mProjectorTimers)
    {
        t.Update();
        mStatusBuilder.Append(t.StatusBuilder).Append('\n');
    }

    if ((++mPrintCount % 6) == 0)
    {
        PrintDetailedInfo();
    }
}

#region INCLUDES
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
#endregion
