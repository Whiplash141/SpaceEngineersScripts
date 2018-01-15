/*
//Whip's Lift Off Script v9 - 1/14/18

/// TO DO: Make ion and atmos go full burn and make hydro pick up the slack
___________________________________________________________________________________
/// DESCRIPTION ///

This script automatically throttles your FORWARD thrusters to optimize
fuel useage while exiting planetary influence. This script also takes 
control of the gyroscopes on the grid in order to ensure that you are on
the optimal escape trajectory. Once your ship leaves the gravity well, the
code will execute a "Turn 'N Burn" to make you come to a stop. 

Make sure 
to point your reference ship controller in the direction you wish to take off!
___________________________________________________________________________________
/// SETUP ///

1. Place a programmable block with this program loaded in it

2. Place a ship controller (cockpit, flight seat, remote, etc...)
    - add the phrase "Reference" into it's name somewhere
    - The thrusters that propel the craft FORWARD will automatically be grabbed

3. Enter the argument "start" to begin lift-off
___________________________________________________________________________________
/// ARGUMENTS ///

start : starts lift-off procedure

stop : stops lift-off procedure
*/

string shipControllerName = "Reference";
double ascentSpeed = 95;
bool minimizeHydrogenUseage = true;

//===================================================================== 
//                NO TOUCHEY BELOW THIS LINE!!!111!1! 
//===================================================================== 

IMyShipController reference;
List<IMyThrust> mainThrusters = new List<IMyThrust>();
List<IMyThrust> hydroThrusters = new List<IMyThrust>();
List<IMyThrust> ionAndAtmoThrusters = new List<IMyThrust>();
List<IMyGyro> gyros = new List<IMyGyro>();

bool isSetup = false;
bool shouldLiftOff = false;

const double updatesPerSecond = 10;
const double updateTime = 1.0 / updatesPerSecond;
const double refreshInterval = 10;
const double minAlignmentTicks = 60;

double currentRefreshTime = 141;
double timeSinceLastUpdate = 141;
double alignmemtTicks = 0;

PID velocityPID = new PID(1, .2, .4, -10, 10, updateTime);

Program()
{
    isSetup = GrabBlocks();
    Runtime.UpdateFrequency = UpdateFrequency.Once;
}

void Main(string arg, UpdateType updateType)
{
    //Argument handling 
    if ((updateType & (UpdateType.Trigger | UpdateType.Terminal | UpdateType.Script)) != 0)
    {
        switch (arg.ToLower())
        {
            case "start":
                shouldLiftOff = true;
                alignmemtTicks = 0;
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
                break;

            case "stop":
                shouldLiftOff = false;
                DisableGyroOverride(gyros);
                ApplyThrust(mainThrusters, 0);
                alignmemtTicks = 0;
                Runtime.UpdateFrequency = UpdateFrequency.None;
                break;
        }
    }

    Echo("WMI Liftoff Manager\n");
    Echo($"Lift Off?: {shouldLiftOff}");

    if ((updateType & UpdateType.Update1) == 0)
        return;

    currentRefreshTime += 1.0 / 60.0;
    timeSinceLastUpdate += 1.0 / 60.0;

    if (!isSetup || currentRefreshTime >= refreshInterval)
    {
        isSetup = GrabBlocks();
        currentRefreshTime = 0;
    }

    if (!isSetup)
        return;

    if (timeSinceLastUpdate >= updateTime)
    {
        if (shouldLiftOff)
            LiftOff();

        timeSinceLastUpdate = 0;
    }
}

void LiftOff()
{
    var mass = reference.CalculateShipMass().PhysicalMass;
    var gravityVec = reference.GetNaturalGravity();

    double thrustForce = 0;
    var shipWeight = mass * gravityVec.Length();
    
    bool useHydrogen = false;
    double ionThrustSum = 0, hydroThrustSum = 0, atmoThrustSum = 0;

    if (minimizeHydrogenUseage)
    {
        //Calculate thrust sums
        
        CalculateMaxThrustByType(mainThrusters, out ionThrustSum, out hydroThrustSum, out atmoThrustSum, out hydroThrusters, out ionAndAtmoThrusters);

        useHydrogen = ionThrustSum + atmoThrustSum <= shipWeight;
        
        foreach (var block in hydroThrusters)
        {
            block.Enabled = useHydrogen;
        }

        foreach (var block in ionAndAtmoThrusters)
        {
            block.Enabled = true;
            block.ThrustOverridePercentage = 1f; //full override
            
            shipWeight -= block.IsFunctional && useHydrogen ? block.MaxEffectiveThrust : 0.0;
        }
    }
    else
    {
        foreach (var block in mainThrusters)
        {
            block.Enabled = true;
        }
    }

    if (useHydrogen)
        thrustForce = hydroThrustSum;
    else
        thrustForce = CalculateMaxThrust(mainThrusters);

    //var maxAcceleration = thrustForce / mass;
    var velocityVec = reference.GetShipVelocities().LinearVelocity;
    var speed = velocityVec.Length();

    Vector3D alignmentVector = new Vector3D(0, 0, 0);

    if (gravityVec.LengthSquared() == 0) //outside gravity well 
    {
        //execute retro-burn 
        alignmentVector = -velocityVec;

        double deviationAngle = VectorAngleBetween(reference.WorldMatrix.Forward, alignmentVector);
        Echo($"{deviationAngle}");

        ApplyThrust(mainThrusters, 0);

        if (deviationAngle < 5.0 / 180.0 * Math.PI)
        {
            if (alignmemtTicks > minAlignmentTicks)
            {
                reference.DampenersOverride = true;
            }
            else
            {
                reference.DampenersOverride = false;
                alignmemtTicks++;
            }
        }
        else
            reference.DampenersOverride = false;

        if (speed < 1)
        {
            DisableGyroOverride(gyros);
            reference.DampenersOverride = true;
            
            foreach (var block in mainThrusters)
            {
                block.Enabled = true;
            }
            
            shouldLiftOff = false;
            alignmemtTicks = 0;
            return;
        }
    }
    else
    {
        reference.DampenersOverride = false;
        alignmentVector = CalculateHeadingVector(-gravityVec, velocityVec, true, 0.5);

        //var equilibriumThrustPercentage = gravityVec.Length() / maxAcceleration;
        var equilibriumThrustPercentage = shipWeight / thrustForce;
        var thrustAdjustment = velocityPID.Control(ascentSpeed - speed * Math.Sign(velocityVec.Dot(-gravityVec)));
        var finalThrustOverride = equilibriumThrustPercentage + thrustAdjustment * 0.01;
        
        if (useHydrogen)
            ApplyThrust(hydroThrusters, finalThrustOverride);
        else
            ApplyThrust(mainThrusters, finalThrustOverride);
    }

    double pitch = 0, yaw = 0;
    GetRotationAngles(alignmentVector, reference.WorldMatrix.Forward, reference.WorldMatrix.Left, reference.WorldMatrix.Up, out yaw, out pitch);

    double pitchSpeed = Math.Round(pitch, 2);
    double yawSpeed = Math.Round(yaw, 2);
    ApplyGyroOverride(pitchSpeed, yawSpeed, 0, gyros, reference);
}

void ApplyThrust(List<IMyThrust> thrusters, double thrustOverride)
{
    foreach (var block in thrusters)
        block.ThrustOverridePercentage = (float)thrustOverride;
}

bool GrabBlocks()
{
    List<IMyShipController> shipControllers = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType(shipControllers, x => x.CustomName.Contains(shipControllerName));

    if (shipControllers.Count == 0)
    {
        Echo($"Error: No ship controller named '{shipControllerName}' were found!");
        return false;
    }

    reference = shipControllers[0];

    GridTerminalSystem.GetBlocksOfType(mainThrusters, x => x.WorldMatrix.Forward == reference.WorldMatrix.Backward);
    if (mainThrusters.Count == 0)
    {
        Echo($"Error: No lift-off thrusters were found!");
        return false;
    }

    GridTerminalSystem.GetBlocksOfType(gyros);
    if (gyros.Count == 0)
    {
        Echo($"Error: No gyros were found!");
        return false;
    }

    return true;
}

void CalculateMaxThrustByType(List<IMyThrust> thrust, out double ionSum, out double hydroSum, out double atmoSum, out List<IMyThrust> hydroThrust, out List<IMyThrust> ionAndAtmosThrust)
{
    ionSum = 0; hydroSum = 0; atmoSum = 0;
    hydroThrust = new List<IMyThrust>();
    ionAndAtmosThrust = new List<IMyThrust>();

    foreach (var block in thrust)
    {
        var definitionName = block.BlockDefinition.SubtypeId;

        if (definitionName.ToUpperInvariant().Contains("HYDROGEN"))
        {
            hydroSum += block.IsFunctional ? block.MaxEffectiveThrust : 0.0;
            hydroThrust.Add(block);
        }
        else if (definitionName.ToUpperInvariant().Contains("ATMOSPHERIC"))
        {
            atmoSum += block.IsFunctional ? block.MaxEffectiveThrust : 0.0;
            ionAndAtmosThrust.Add(block);
        }
        else
        {
            ionSum += block.IsFunctional ? block.MaxEffectiveThrust : 0.0;
            ionAndAtmosThrust.Add(block);
        }
    }
}

double CalculateMaxThrust(List<IMyThrust> thrusters)
{
    double thrustSum = 0;
    foreach (var block in thrusters)
    {
        thrustSum += block.IsWorking ? block.MaxEffectiveThrust : 0.0;
    }
    return thrustSum;
}

void DisableGyroOverride(List<IMyGyro> gyros)
{
    foreach (var block in gyros)
        block.GyroOverride = false;
}

//Whip's ApplyGyroOverride Method v9 - 8/19/17
void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list, IMyTerminalBlock reference)
{
    var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed); //because keen does some weird stuff with signs 
    var shipMatrix = reference.WorldMatrix;
    var relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipMatrix);

    foreach (var thisGyro in gyro_list)
    {
        var gyroMatrix = thisGyro.WorldMatrix;
        var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(gyroMatrix));

        thisGyro.Pitch = (float)transformedRotationVec.X;
        thisGyro.Yaw = (float)transformedRotationVec.Y;
        thisGyro.Roll = (float)transformedRotationVec.Z;
        thisGyro.GyroOverride = true;
    }
}

//Whip's Get Rotation Angles Method v5 - 5/30/17 
void GetRotationAngles(Vector3D v_target, Vector3D v_front, Vector3D v_left, Vector3D v_up, out double yaw, out double pitch)
{
    //Dependencies: VectorProjection() | VectorAngleBetween() 
    var projectTargetUp = VectorProjection(v_target, v_up);
    var projTargetFrontLeft = v_target - projectTargetUp;

    yaw = VectorAngleBetween(v_front, projTargetFrontLeft);
    pitch = VectorAngleBetween(v_target, projTargetFrontLeft);

    //---Check if yaw angle is left or right   
    //multiplied by -1 to convert from right hand rule to left hand rule 
    yaw = -1 * Math.Sign(v_left.Dot(v_target)) * yaw;

    //---Check if pitch angle is up or down     
    pitch = Math.Sign(v_up.Dot(v_target)) * pitch;

    //---Check if target vector is pointing opposite the front vector 
    if (pitch == 0 && yaw == 0 && v_target.Dot(v_front) < 0)
    {
        yaw = Math.PI;
    }
}

Vector3D CalculateHeadingVector(Vector3D targetVec, Vector3D velocityVec, bool driftComp, double rejectionFactor)
{
    if (!driftComp)
    {
        return targetVec;
    }

    if (velocityVec.LengthSquared() < 100)
    {
        return targetVec;
    }

    if (targetVec.Dot(velocityVec) > 0)
    {
        return VectorReflection(velocityVec, targetVec, rejectionFactor);
    }
    else
    {
        return -velocityVec;
    }
}

Vector3D VectorProjection(Vector3D a, Vector3D b) //proj a on b    
{
    Vector3D projection = a.Dot(b) / b.LengthSquared() * b;
    return projection;
}

double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians  
{
    if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
        return 0;
    else
        return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
}

//Whip's Vector Reflection Method
Vector3D VectorReflection(Vector3D a, Vector3D b, double rejectionFactor = 1) //reflect a over b    
{
    Vector3D project_a = VectorProjection(a, b);
    Vector3D reject_a = a - project_a;
    return project_a - reject_a * rejectionFactor;
}

//Whip's PID controller class v4 - 8/27/17
public class PID
{
    double _kP = 0;
    double _kI = 0;
    double _kD = 0;
    double _integralDecayRatio = 0;
    double _lowerBound = 0;
    double _upperBound = 0;
    double _timeStep = 0;
    double _errorSum = 0;
    double _lastError = 0;
    bool _firstRun = true;
    bool _integralDecay = false;
    public double Value { get; private set; }

    public PID(double kP, double kI, double kD, double lowerBound, double upperBound, double timeStep)
    {
        _kP = kP;
        _kI = kI;
        _kD = kD;
        _lowerBound = lowerBound;
        _upperBound = upperBound;
        _timeStep = timeStep;
        _integralDecay = false;
    }

    public PID(double kP, double kI, double kD, double integralDecayRatio, double timeStep)
    {
        _kP = kP;
        _kI = kI;
        _kD = kD;
        _timeStep = timeStep;
        _integralDecayRatio = integralDecayRatio;
        _integralDecay = true;
    }

    public double Control(double error)
    {
        //Compute derivative term
        var errorDerivative = (error - _lastError) / _timeStep;

        if (_firstRun)
        {
            errorDerivative = 0;
            _firstRun = false;
        }

        //Compute integral term
        if (!_integralDecay)
        {
            _errorSum += error * _timeStep;

            //Clamp integral term
            if (_errorSum > _upperBound)
                _errorSum = _upperBound;
            else if (_errorSum < _lowerBound)
                _errorSum = _lowerBound;
        }
        else
        {
            _errorSum = _errorSum * (1.0 - _integralDecayRatio) + error * _timeStep;
        }

        //Store this error as last error
        _lastError = error;

        //Construct output
        this.Value = _kP * error + _kI * _errorSum + _kD * errorDerivative;
        return this.Value;
    }

    public void Reset()
    {
        _errorSum = 0;
        _lastError = 0;
        _firstRun = true;
    }
}