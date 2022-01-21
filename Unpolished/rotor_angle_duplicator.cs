/*
/ //// / Whip's Rotor Angle Duplicator / //// /
________________________
    INSTRUCTIONS
    
1. Place this script in a programmable block
2. Group all rotors that are going to follow another rotor
    in a group named "Child Rotors".
3. Run this code with the argument "refresh". This will look
    for new rotors, and parse custom data.
4. Open the custom data of each child rotor and configure:
    - The name of the parent rotor that this rotor will follow.
    - The speed multiplier of the child rotor.
    - The angle offset to add to the child rotor angle when computing
        the difference in target and current angles.
5. Run the code with argument "refresh".


The script should take care of the rest :)



===============================================
        NO TOUCHEY BELOW THIS LINE
===============================================











*/

const string VERSION = "1.0.6";
const string DATE = "2021/01/24";

string _childRotorGroupName = "Child Rotors";

const string INI_SECTION_ROTOR = "Child Rotor Config";
const string INI_KEY_ROTOR_PARENT = "Parent name";
const string INI_KEY_ROTOR_GAIN = "Rotation speed multiplier";
const string INI_KEY_ROTOR_OFFSET = "Child rotor angle offset (deg)";
const string INI_KEY_ROTOR_MULT = "Child rotor angle multiplier";

const string DEFAULT_PARENT_NAME = "";
const float DEFAULT_ROTOR_GAIN = 10f;
const float DEFAULT_ROTOR_OFFSET = 0f;
const float DEFAULT_ROTOR_MULT = 1f;

Dictionary<string, IMyMotorStator> _rotorNameDict = new Dictionary<string, IMyMotorStator>();
List<ChildRotor> _childRotors = new List<ChildRotor>();
MyCommandLine _cmdLine = new MyCommandLine();
MyIni _ini = new MyIni();
StringBuilder _errorBuilder = new StringBuilder();
StringBuilder _argBuilder = new StringBuilder();
string _errorOutput = "";
string _argOutput = "";
int _errorCount = 0;
int _updateCount = 141;
int _argumentAge = 141;

class ChildRotor
{
    IMyMotorStator _parent;
    IMyMotorStator _child;
    float _rotationalGain;
    float _offsetRads;
    float _angleMultiplier;

    public ChildRotor(IMyMotorStator child, IMyMotorStator parent, float rotationalGain, float offsetDegs, float angleMultiplier)
    {
        _child = child;
        _parent = parent;
        _rotationalGain = rotationalGain;
        _offsetRads = MathHelper.ToRadians(offsetDegs);
        _angleMultiplier = angleMultiplier;
    }
    
    bool BlocksMissing()
    {
        return _child.WorldMatrix.Translation == Vector3D.Zero || _parent.WorldMatrix.Translation == Vector3D.Zero;
    }

    public void DuplicateParentAngle()
    {
        if (BlocksMissing())
            return;
        
        float currentAngle = _child.Angle + _offsetRads;
        float targetAngle = _angleMultiplier * _parent.Angle;
        float difference = targetAngle - currentAngle;

        difference %= MathHelper.TwoPi; // Wrap around 2*PI and get the remainder such that the difference: (-2PI, PI)

        // Compute smallest angle between target and current
        if (difference > MathHelper.Pi)
        {
            difference -= MathHelper.TwoPi; 
        }
        else if (difference < -MathHelper.Pi)
        {
            difference += MathHelper.TwoPi;
        }

        _child.TargetVelocityRad = difference * _rotationalGain;
    }
}

void Setup()
{
    _errorBuilder.Clear();
    _rotorNameDict.Clear();
    _childRotors.Clear();
    _errorCount = 0;

    _errorBuilder.Append("Setup errors:\n");

    IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(_childRotorGroupName);
    GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(null, RotorNameCollect);
    if (group == null)
    {
        _errorBuilder.Append($"- No group named '{_childRotorGroupName}'\n   was found!");
        _errorCount++;
    }
    else
    {
        group.GetBlocksOfType<IMyMotorStator>(null, ChildRotorCollect);
    }

    if (_errorCount == 0)
    {
        _errorBuilder.Append("  No errors found!\n\n");
    }

    _errorOutput = _errorBuilder.ToString();
}

bool RotorNameCollect(IMyTerminalBlock b)
{
    IMyMotorStator rotor = (IMyMotorStator)b;
    _rotorNameDict[b.CustomName] = rotor;
    return false;
}

bool ChildRotorCollect(IMyTerminalBlock b)
{
    IMyMotorStator rotor = (IMyMotorStator)b;
    string parentName = DEFAULT_PARENT_NAME;
    float gain = DEFAULT_ROTOR_GAIN;
    float offset = DEFAULT_ROTOR_OFFSET;
    float mult = DEFAULT_ROTOR_MULT;

    _ini.Clear();
    if (_ini.TryParse(b.CustomData))
    {
        parentName = _ini.Get(INI_SECTION_ROTOR, INI_KEY_ROTOR_PARENT).ToString(DEFAULT_PARENT_NAME);
        gain = _ini.Get(INI_SECTION_ROTOR, INI_KEY_ROTOR_GAIN).ToSingle(DEFAULT_ROTOR_GAIN);
        offset = _ini.Get(INI_SECTION_ROTOR, INI_KEY_ROTOR_OFFSET).ToSingle(DEFAULT_ROTOR_OFFSET);
        mult = _ini.Get(INI_SECTION_ROTOR, INI_KEY_ROTOR_MULT).ToSingle(DEFAULT_ROTOR_MULT);
    }

    _ini.Set(INI_SECTION_ROTOR, INI_KEY_ROTOR_PARENT, parentName);
    _ini.Set(INI_SECTION_ROTOR, INI_KEY_ROTOR_GAIN, gain);
    _ini.Set(INI_SECTION_ROTOR, INI_KEY_ROTOR_OFFSET, offset);
    _ini.Set(INI_SECTION_ROTOR, INI_KEY_ROTOR_MULT, mult);


    string output = _ini.ToString();
    if (output != b.CustomData)
    {
        b.CustomData = output;
    }

    IMyMotorStator parent;
    if (_rotorNameDict.TryGetValue(parentName, out parent))
    {
        _childRotors.Add(new ChildRotor(rotor, parent, gain, offset, mult));
    }
    else
    {
        _errorBuilder.Append($"- No rotor named\n   '{parentName}'\n   specified by child rotor\n   '{rotor.CustomName}'\n   found on ship!\n   Check the child rotor's custom\n   data!\n\n");
        _errorCount++;
    }

    return false;
}

void ParseArgs(string arg)
{
    _argBuilder.Clear();
    _cmdLine.TryParse(arg);
    for (int ii = 0; ii < _cmdLine.ArgumentCount; ++ii)
    {
        switch (_cmdLine.Argument(ii))
        {
            case "refresh":
                Setup();
                _argBuilder.Append($"Refreshed child rotors and reparsed\n custom data.\n");
                break;

            default:
                _argBuilder.Append($"Argument '{_cmdLine.Argument(ii)}' was not understood.\n");
                break;
        }
        _argumentAge = 0;
    }

    _argOutput = _argBuilder.ToString();
}

void DuplicateRotorAngles()
{
    foreach (var childRotor in _childRotors)
    {
        childRotor.DuplicateParentAngle();
    }
}

void PrintEcho()
{
    Echo($"Whip's Rotor Angle Duplicator\n(Version {VERSION} - {DATE})\n\n{_childRotors.Count} child rotor(s) found\n\nRun the argument 'refresh' to refetch\nblocks and reparse custom data.{(_argumentAge < 18 ? "\n\n" + _argOutput: "\n")}\n{_errorOutput}");
}

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    Setup();
}

void Main(string arg, UpdateType updateSource)
{
    if (!string.IsNullOrEmpty(arg))
    {
        ParseArgs(arg);
        _updateCount = 5; // Print now
    }

    if ((UpdateType.Update10 & updateSource) != 0)
    {
        DuplicateRotorAngles();

        ++_argumentAge;
        if (++_updateCount % 6 == 0)
        {
            PrintEcho();
        }
    }
}
