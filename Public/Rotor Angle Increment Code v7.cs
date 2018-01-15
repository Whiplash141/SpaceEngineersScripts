// Whip's Rotor Controller v7 /// revision: 11/17/17 
/* 
*/

List<RotorController> activeRotorList = new List<RotorController>();

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.None;
}

void Main(string arg, UpdateType updateType)
{
    if (arg != "" && (updateType & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) == 0)
    {
        ParseArguments(arg);
    }

    foreach (var rotor in activeRotorList)
    {
        rotor.MoveRotor();
    }

    activeRotorList.RemoveAll(x => x.DoneMoving); //remove finished rotors 

    Echo("Whip's Rotor Controller");
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
}

void ParseArguments(string argument)
{
    string command = "";
    bool setRotorAngle = false;

    var argumentSplit = argument.ToLower().Split(';');

    if (argumentSplit.Length != 2)
    {
        Echo($"Error: '{argument}' not understood.\nArguments must be in the form of:\n<block name>;<command>");
        return;
    }

    var rotorName = argumentSplit[0];
    var rotors = new List<IMyMotorStator>();
    GridTerminalSystem.GetBlocksOfType(rotors, block => block.CustomName.ToLower().Contains(rotorName.ToLower()));
    //activeRotors.Add(rotorName); 

    if (argumentSplit[1].ToLower().Contains("set "))
    {
        command = argumentSplit[1].ToLower().Replace("set ", "");

        if (command.Contains("+"))
            command = command.Replace("+", "");

        setRotorAngle = true;
    }
    else if (argumentSplit[1].Contains("increment "))
    {
        command = argumentSplit[1].Replace("increment ", "");
        if (command.Contains("+"))
            command = command.Replace("+", "");
    }

    float thisValue;
    bool isNumeric = Single.TryParse(command, out thisValue);
    if (isNumeric)
    {
        if (setRotorAngle)
        {
            Echo("Argument parsed as: " + thisValue.ToString());
            Echo("Setting rotor angle...");

            foreach (var block in rotors)
            {
                var existingRotor = activeRotorList.Find(x => x.Rotor == block);
                if (existingRotor != default(RotorController))
                {
                    existingRotor.SetAngle(thisValue);
                }
                else
                {
                    var activeRotor = new RotorController(block, thisValue, false);
                    activeRotorList.Add(activeRotor);
                }

            }

            //SetRotorAngles(rotorName, thisValue); 
        }
        else
        {
            Echo("Argument parsed as: " + thisValue.ToString());
            Echo("Incrementing rotors...");

            foreach (var block in rotors)
            {
                var existingRotor = activeRotorList.Find(x => x.Rotor == block);
                if (existingRotor != default(RotorController))
                {
                    existingRotor.IncrementAngle(thisValue);
                }
                else
                {
                    var activeRotor = new RotorController(block, thisValue, true);
                    activeRotorList.Add(activeRotor);
                }
            }

            //IncrementRotor(rotorName, thisValue); 
        }
    }
    else
    {
        Echo("Error: Argument '" + argument + "' could not be parsed");
    }
}

class RotorController
{
    public IMyMotorStator Rotor { get; private set; } = null;
    float _initialAngle;
    float _targetAngle;
    float _epsilon;
    float _rotationSpeedGain;
    public bool DoneMoving { get; private set; } = false;

    public RotorController(IMyMotorStator rotor, float angle, bool increment = false, float rotationSpeedGain = 50f, float epsilonDeg = 0.1f)
    {
        Rotor = rotor;
        _initialAngle = Rotor.Angle;
        if (increment)
            _targetAngle = _initialAngle + MathHelper.ToRadians(angle);
        else
            _targetAngle = MathHelper.ToRadians(angle);
        
        _epsilon = MathHelper.ToRadians(epsilonDeg);
        _rotationSpeedGain = rotationSpeedGain;
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
        var rotorAngle = Rotor.Angle;

        MathHelper.LimitRadiansPI(ref rotorAngle);
        MathHelper.LimitRadiansPI(ref _targetAngle);

        var err = _targetAngle - rotorAngle;
        MathHelper.LimitRadiansPI(ref err);
        _targetAngle = rotorAngle + err;
        
        /*
        if (err > MathHelper.Pi)
        {
            MathHelper.LimitRadiansPI(ref err);
            _targetAngle = rotorAngle + err;
            err = _targetAngle - rotorAngle;
        }
        else if (err < -MathHelper.Pi)
        {
            //_targetAngle -= MathHelper.TwoPi;
            err = _targetAngle - rotorAngle;
        }*/
        
        Rotor.LowerLimitRad = rotorAngle < _targetAngle ? rotorAngle : _targetAngle;
        Rotor.UpperLimitRad = rotorAngle > _targetAngle ? rotorAngle : _targetAngle;

        /*
        if (err > 0)
        {
            Rotor.LowerLimitRad = rotorAngle;
            Rotor.UpperLimitRad = _targetAngle;
        }
        else
        {
            Rotor.UpperLimitRad = rotorAngle;
            Rotor.LowerLimitRad = _targetAngle;
        }*/
        
        Rotor.TargetVelocityRPM = err * _rotationSpeedGain;

        if (Math.Abs(err) < _epsilon)
        {
            Rotor.TargetVelocityRPM = 0;
            this.DoneMoving = true;
        }
    }
}