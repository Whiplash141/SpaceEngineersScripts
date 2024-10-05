
/*
/ //// / Whip's Rotor Angle Controller / //// /
______________________________________________________________
SETUP:
1) Place this code in a programmable block
2) That is all

______________________________________________________________
INSTRUCTIONS:
* Run the code with the following argument syntax:

"<rotor name tag>" <command> <angle>

Where:
    <rotor name tag> is the name tag of the rotor. Names are case insensitive.
    
    <command> is one of the commands listed below.
    
    <angle> angle in degrees to use with the command.

______________________________________________________________
COMMANDS:

set
    Sets the rotor angle to the specified <angle>.

increment
    increments current rotor angle by the specified <angle>.

______________________________________________________________

"Rotor 1" set 45
    Sets EVERY rotor with name "Rotor 1" to 45°.
    
"Potato" increment 69
    Increments EVERY rotor with "Potato" in its name by 69°.
   
*/

const string Version = "2.1.0";
const string Date = "2022/08/19";
const float RotationSpeedGain = 15;
bool _lockRotorAtDestination = true;
bool _setRotorLimitsAtDestination = true;

List<RotorController> activeRotorList = new List<RotorController>();
List<IMyMotorStator> rotors = new List<IMyMotorStator>();

ArgumentParser _argumentParser = new ArgumentParser();

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.None;
    Echo("Whip's Rotor Controller");
    Echo(">>Inactive<<");
}

void Main(string arg, UpdateType updateType)
{
    if (arg != "" && (updateType & (UpdateType.Trigger | UpdateType.Terminal | UpdateType.Script)) != 0) //checks if update source is from user
    {
        ParseArguments(arg);
    }
    
    Echo($"Whip's Rotor Controller\n(Version {Version} - {Date})");
    if (activeRotorList.Count > 0)
    {
        Echo(">>Active<<");
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
    }
    else
    {
        Echo(">>Inactive<<");
        Runtime.UpdateFrequency = UpdateFrequency.None;
    }
    
    if ((updateType & UpdateType.Update10) == 0) //ignore the brunt of the logic if the 
        return;
    
    foreach (var rotor in activeRotorList)
    {
        rotor.MoveRotor();
    }

    activeRotorList.RemoveAll(x => x.DoneMoving); //remove finished rotors 
}

void ParseArguments(string argument)
{
    if (!_argumentParser.TryParse(argument))
    {
        // TODO: Print stuff
        return;
    }
    
    if (_argumentParser.ArgumentCount < 3)
    {
        Echo($">> Error: Too few arguments ({_argumentParser.ArgumentCount}, expected 3).\nRequired format: \"<rotor name tag>\" <command> <angle> .");
        return;
    }
    
    if (_argumentParser.ArgumentCount > 3)
    {
        Echo($">> Error: Too many arguments ({_argumentParser.ArgumentCount}, expected 3).\nRequired format: \"<rotor name tag>\" <command> <angle> .");
        return;
    }
        
    bool setRotorAngle = false;
    float angle = 0;
    string rotorName = _argumentParser.Argument(0);
    string command = _argumentParser.Argument(1);
    string angleString = _argumentParser.Argument(2);

    switch (command)
    {
        case "set":
            setRotorAngle = true;
            break;
        case "increment":
            setRotorAngle = false;
            break;
        default:
            Echo($">> Error: Second argument '{command}' invalid.\nAccepted values are 'set' or 'increment'.");
            return;
    }

    if (!float.TryParse(angleString, out angle))
    {
        Echo($">> Error: Third argument '{command}' invalid.\nArgument must be a number.");
        return;
    }
    
    rotors.Clear();
    GridTerminalSystem.GetBlocksOfType(rotors, block => block.CustomName.ToLower().Contains(rotorName.ToLower()));
    
    if (rotors.Count == 0)
    {
        Echo($">> Error: No rotors named '{rotorName}' were found");
        return;
    }

    if (setRotorAngle) //set rotor angle
    {
        Echo("Setting rotor '{rotorName}' angle to {angle}°...");

        foreach (var block in rotors)
        {
            var existingRotor = activeRotorList.Find(x => x.Rotor == block);
            if (existingRotor != default(RotorController))
            {
                existingRotor.SetAngle(angle);
            }
            else
            {
                var activeRotor = new RotorController(block, angle, _lockRotorAtDestination, _setRotorLimitsAtDestination, false, RotationSpeedGain);
                activeRotorList.Add(activeRotor);
            }

        }
    }
    else //increment rotor angle
    {
        Echo("Incrementing rotor '{rotorName}' angle by {angle}°...");

        foreach (var block in rotors)
        {
            var existingRotor = activeRotorList.Find(x => x.Rotor == block);
            if (existingRotor != default(RotorController))
            {
                existingRotor.IncrementAngle(angle);
            }
            else
            {
                var activeRotor = new RotorController(block, angle, _lockRotorAtDestination, _setRotorLimitsAtDestination, true);
                activeRotorList.Add(activeRotor);
            }
        }
    }
    
}

class RotorController
{
    public IMyMotorStator Rotor { get; private set; } = null;
    float _initialAngle;
    float _targetAngle;
    float _epsilon;
    float _rotationSpeedGain;
    bool _shouldLock;
    bool _shouldLimit;
    public bool DoneMoving { get; private set; } = false;

    public RotorController(IMyMotorStator rotor, float angle, bool shouldLock, bool shouldLimit, bool increment, float rotationSpeedGain = 30f, float epsilonDeg = 0.1f)
    {
        Rotor = rotor;
        _initialAngle = Rotor.Angle;
        if (increment)
            _targetAngle = _initialAngle + MathHelper.ToRadians(angle);
        else
            _targetAngle = MathHelper.ToRadians(angle);

        _epsilon = MathHelper.ToRadians(epsilonDeg);
        _rotationSpeedGain = rotationSpeedGain;
        _shouldLock = shouldLock;
        _shouldLimit = shouldLimit;
    }

    public void IncrementAngle(float angle)
    {
        _targetAngle += MathHelper.ToRadians(angle);
    }

    public void SetAngle(float angle)
    {
        _targetAngle = MathHelper.ToRadians(angle);
    }

    public void MoveRotor()
    {
        MathHelper.LimitRadians(ref _targetAngle);

        Rotor.LowerLimitDeg = -362f;
        Rotor.UpperLimitDeg = 362f;
        Rotor.RotorLock = false;

        var rotorVector = Rotor.Top.WorldMatrix.Backward;
        var rotorRightVector = Rotor.Top.WorldMatrix.Left; //bc keen...
        var targetVector = GetVectorFromRotorAngle(_targetAngle, Rotor);

        var err = VectorAngleBetween(rotorVector, targetVector) * Math.Sign(targetVector.Dot(rotorRightVector));

        Rotor.TargetVelocityRPM = (float)err * _rotationSpeedGain;

        if (Math.Abs(err) < _epsilon)
        {
            Rotor.TargetVelocityRPM = 0;
            if (_shouldLimit)
            {
                Rotor.LowerLimitRad = _targetAngle;
                Rotor.UpperLimitRad = _targetAngle;
            }
            if (_shouldLock)
            {
                Rotor.RotorLock = true;                
            }
            this.DoneMoving = true;
        }
    }

    Vector3D GetVectorFromRotorAngle(float angle, IMyMotorStator rotor)
    {
        double x = Math.Sin(angle);
        double y = Math.Cos(angle);
        var rotorMatrix = rotor.WorldMatrix;
        return rotorMatrix.Backward * y + rotor.WorldMatrix.Left * x;
    }

    private double VectorAngleBetween(Vector3D a, Vector3D b, bool useSmallestAngle = false) //returns radians 
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;
        else if (useSmallestAngle)
            return Math.Acos(MathHelper.Clamp(Math.Abs(a.Dot(b)) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        else
            return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
    }
}


#region Argument Parser
class ArgumentParser
{
    public int ArgumentCount {
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
                if (c == '-' && i + 1 < _raw.Length && _raw[i+1] == '-')
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
#endregion
