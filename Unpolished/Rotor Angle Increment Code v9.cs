
/*
// Whip's Rotor Controller v9 /// revision: 12/4/17 
______________________________________________________________
SETUP:
1) Place this code in a programmable block
2) That is all

______________________________________________________________
INSTRUCTIONS:
* Run the code with the following argument syntax:

<rotor name>;<command>

Where:
    <rotor name> is the name of the rotor. Names are case insensitive
    
    <command> is one of the commands listed below

______________________________________________________________
COMMANDS:

set <angle>
    Sets the rotor <angle> to the specified angle

increment <angle>
    increments current rotor angle by the specified <angle>

______________________________________________________________
Examples:

Rotor 1 ; set 45
    Sets EVERY rotor with name "Rotor 1" to 45°
    
Potato ; increment 69
    Increments EVERY rotor with "Potato" in its name by 69°
   
*/

List<RotorController> activeRotorList = new List<RotorController>();

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
    string command = "";
    bool setRotorAngle = false;

    var argumentSplit = argument.ToLower().Split(';');

    if (argumentSplit.Length != 2)
    {
        Echo($">> Error: '{argument}' not understood.\nArguments must be in the form of:\n<rotor name>;<command>");
        return;
    }

    var rotorName = argumentSplit[0].Trim();
    var rotors = new List<IMyMotorStator>();
    GridTerminalSystem.GetBlocksOfType(rotors, block => block.CustomName.ToLower().Contains(rotorName.ToLower()));
    
    if (rotors.Count == 0)
    {
        Echo($">> Error: No rotors named '{rotorName}' were found");
        return;
    }
    
    command = argumentSplit[1].Trim().ToLower();

    if (command.Contains("set "))
    {
        command = argumentSplit[1].ToLower().Replace("set ", "");

        if (command.Contains("+"))
            command = command.Replace("+", "");

        setRotorAngle = true;
    }
    else if (command.Contains("increment "))
    {
        command = argumentSplit[1].Replace("increment ", "");
        if (command.Contains("+"))
            command = command.Replace("+", "");
    }
    else
    {
        Echo($">> Error: '{argument}' not understood.\nArguments must be in the form of:\n<rotor name>;<command>");
        return;
    }

    float thisValue;
    bool isNumeric = Single.TryParse(command, out thisValue);
    if (isNumeric)
    {
        if (setRotorAngle) //set rotor angle
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
        }
        else //increment rotor angle
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

    public RotorController(IMyMotorStator rotor, float angle, bool increment = false, float rotationSpeedGain = 30f, float epsilonDeg = 0.1f)
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
        MathHelper.LimitRadians(ref _targetAngle);

        Rotor.SetValue("LowerLimit", -362f);
        Rotor.SetValue("UpperLimit", 362f);
        Rotor.SetValue("RotorLock", false);

        var rotorVector = Rotor.Top.WorldMatrix.Backward;
        var rotorRightVector = Rotor.Top.WorldMatrix.Left; //bc keen...
        var targetVector = GetVectorFromRotorAngle(_targetAngle, Rotor);

    var err = VectorAngleBetween(rotorVector, targetVector) * Math.Sign(targetVector.Dot(rotorRightVector));

    Rotor.TargetVelocityRPM = (float)err * _rotationSpeedGain;

    if (Math.Abs(err) < _epsilon)
    {
        Rotor.TargetVelocityRPM = 0;
        Rotor.SetValue("LowerLimit", MathHelper.ToDegrees(_targetAngle));
        Rotor.SetValue("UpperLimit", MathHelper.ToDegrees(_targetAngle));
        Rotor.SetValue("RotorLock", true);
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