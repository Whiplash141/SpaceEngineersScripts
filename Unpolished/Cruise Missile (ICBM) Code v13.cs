//Whip's Cruise Missile Code v13 - 3/10/18
//Formerly: ICBM Code

const string missileGroupName = "Missile 1";
const double proportionalGain = 2;
const double integralGain = 0;
const double derivativeGain = 0.1;
const double runsPerSecond = 20;
const double orbitAltitude = 3000;
const double diveAngle = 30;
bool shouldFollowTerrain = false;

const double delayTime = 1 / runsPerSecond;
double currentTime = 0;
double orbitRadius = -1;
double planetRadius = -1;

Vector3D planetCenter = new Vector3D(0, 0, 0);
Vector3D targetPosition = new Vector3D(0, 0, 0);

PID yawPID;
PID pitchPID;

bool isSetup = false;
bool hasLaunched = false;

Program()
{
    //attempt to validate setup on compile
    isSetup = GetBlocks();
    
    if (Load())
    {
        Echo($"Target loaded from storage!\nFiring...");
        Runtime.UpdateFrequency = UpdateFrequency.Update1;
    }
}

bool Load()
{   
    var storageSplit = Storage.Split(';');
    
    if (storageSplit.Length < 2)
        return false;
    
    Vector3D.TryParse(storageSplit[0], out targetPosition);
    bool.TryParse(storageSplit[1], out hasLaunched);
    return hasLaunched;
}

StringBuilder saveSB = new StringBuilder();
void Save()
{
    saveSB.Clear();
    saveSB.Append(targetPosition).Append(";").Append(hasLaunched);
    Storage = saveSB.ToString();
}

double epsilon = 1E-9;
void Main(string argument, UpdateType updateSource)
{
    #region ARGUMENT HANDLING
    if (argument.ToLower() == "setup")
    {
        Echo("Processing setup command...");
        isSetup = GetBlocks();
        if (isSetup)
        {
            Echo("Setup Successful!");
        }
        else
        {
            Echo("Setup Failed :(");
        }
    }
    else
    {
        Vector3D tempTargetPosition = new Vector3D(0, 0, 0);
        bool isGPS = TryParseGPS(argument, out tempTargetPosition);
        if (isGPS)
        {
            Echo($"Target Position '{(argument.Split(':'))[1]}' successfully parsed!\nFiring...");
            targetPosition = tempTargetPosition;
            if (isSetup)
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }
        }
        else if (argument != "")
        {
            Echo($"Invalid command: '{argument}'");
        }
    }
    #endregion

    //Checking if in an update loop
    if ((updateSource & UpdateType.Update1) == 0)
        return;
    
    currentTime += (1.0 / 60.0);

    //validate setup before doing anything
    if (!isSetup)
    {
        isSetup = GetBlocks();
    }
    
    if (currentTime + epsilon < delayTime)
        return;

    currentTime = 0;

    if (!hasLaunched)
        LaunchMissile();

    GuideMissile();
    
    Echo($"shouldDive: {shouldDive}");
    
}

List<IMyShipController> shipControllers = new List<IMyShipController>();
List<IMyGyro> gyros = new List<IMyGyro>();
List<IMyShipMergeBlock> merges = new List<IMyShipMergeBlock>();
List<IMyThrust> forwardThrust = new List<IMyThrust>();
List<IMyThrust> otherThrust = new List<IMyThrust>();
List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
List<IMyReactor> reactors = new List<IMyReactor>();
List<IMyTimerBlock> timers = new List<IMyTimerBlock>();
IMyShipController missileReference = null;

/// <summary>
/// Clears all blocks from memory
/// </summary>
void ClearBlocks()
{
    shipControllers.Clear();
    gyros.Clear();
    merges.Clear();
    forwardThrust.Clear();
    otherThrust.Clear();
    batteries.Clear();
    reactors.Clear();
    timers.Clear();
    missileReference = null;
}

/// <summary>
/// This gets the blocks that the code needs to function
/// </summary>
/// <returns>If the setup was successful</returns>
bool GetBlocks()
{
    ClearBlocks();
    
    yawPID = new PID(proportionalGain, integralGain, derivativeGain, 0.1, delayTime);
    pitchPID = new PID(proportionalGain, integralGain, derivativeGain, 0.1, delayTime);
    
    Echo($"Setup results for '{missileGroupName}'\n-----------------------------");
    
    bool setup = true;
    var missileGroup = GridTerminalSystem.GetBlockGroupWithName(missileGroupName);
    if (missileGroup == null)
    {
        Echo($"Error: No group named '{missileGroupName}' was found");
        return false;
    }
    
    missileGroup.GetBlocksOfType(shipControllers);
    missileGroup.GetBlocksOfType(gyros);
    missileGroup.GetBlocksOfType(reactors);
    missileGroup.GetBlocksOfType(batteries);
    missileGroup.GetBlocksOfType(merges);
    missileGroup.GetBlocksOfType(timers);

    if (shipControllers.Count == 0)
    {
        setup = false;
        Echo("Error: No ship controllers found");
    }
    else
    {
        missileReference = shipControllers[0];
    }

    if (gyros.Count == 0)
    {
        setup = false;
        Echo("Error: No gyros found");
    }

    if (timers.Count == 0)
    {
        Echo("Optional: No timers found");
    }

    if (merges.Count == 0)
    {
        Echo("Optional: No merges found");
    }

    if (batteries.Count == 0)
    {
        Echo("Optional: No batteries found");
    }

    if (reactors.Count == 0)
    {
        Echo("Optional: No reactors found");
    }

    if (missileReference != null)
    {
        missileGroup.GetBlocksOfType(forwardThrust, block => block.WorldMatrix.Forward == missileReference.WorldMatrix.Backward);
        missileGroup.GetBlocksOfType(otherThrust, block => block.WorldMatrix.Forward != missileReference.WorldMatrix.Backward);

        if (forwardThrust.Count == 0)
        {
            Echo("Error: No forward thrust was found");
            setup = false;
        }

        bool inPlanet = missileReference.TryGetPlanetPosition(out planetCenter);

        if (!inPlanet)
        {
            setup = false;
            Echo("Error: Not in planet atmosphere");
        }

        double distanceFromPlanetCenter = Vector3D.Distance(missileReference.GetPosition(), planetCenter);
        double altitude;
        missileReference.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out altitude);
          
        planetRadius = distanceFromPlanetCenter - altitude;
        orbitRadius = UpdateFlightRadius(missileReference, planetRadius, orbitAltitude);
        lastOrbitRadius = orbitRadius;
        
        Echo($"radius: {Math.Round(planetRadius)}\norbit radius: {Math.Round(orbitRadius)}");
    }

    return setup;
}

bool LaunchMissile()
{
    foreach(var thisThrust in forwardThrust)
    {
        thisThrust.ThrustOverridePercentage = 1f;
        thisThrust.Enabled = true;
    }

    foreach (var thisThrust in otherThrust)
    {
        thisThrust.Enabled = true;
    }

    foreach (var thisReactor in reactors)
    {
        thisReactor.Enabled = true;
    }

    foreach (var thisBattery in batteries)
    {
        thisBattery.Enabled = true;
        thisBattery.SemiautoEnabled = false;
        thisBattery.OnlyRecharge = false;
        thisBattery.OnlyDischarge = true;
    }

    foreach (var thisMerge in merges)
    {
        thisMerge.Enabled = false;
    }

    foreach (var thisGyro in gyros)
    {
        thisGyro.Enabled = true;
    }

    return true;
}

bool shouldDive = false;

/// <summary>
/// Guides the missile to target
/// </summary>
void GuideMissile()
{
    Vector3D missilePosition = missileReference.GetPosition(); //missile pos
    Vector3D missileVelocity = missileReference.GetShipVelocities().LinearVelocity;
    Vector3D centerToMissile = missilePosition - planetCenter; //planet relative missile pos
    Vector3D centerToTarget = targetPosition - planetCenter; //planet relative target pos

    Vector3D missileToTarget = new Vector3D(0,0,0);
    if (VectorAngleBetween(centerToMissile, missilePosition - targetPosition) > diveAngle / 180d * Math.PI && !shouldDive)
    { 
        Vector3D relativeLeft = centerToMissile.Cross(centerToTarget);
        Vector3D relativeForward = relativeLeft.Cross(centerToMissile);
        relativeForward = Vector3D.Normalize(relativeForward) * Math.Max(200, 2 * missileVelocity.Length());

        var thisOrbitRadius = UpdateFlightRadius(missileReference, planetRadius, orbitAltitude);
        if (shouldFollowTerrain)
        {
            if (thisOrbitRadius < lastOrbitRadius)
            {
                orbitRadius = (thisOrbitRadius + lastOrbitRadius) / 2;
            }
            else
                orbitRadius = thisOrbitRadius;
        }
        else if (thisOrbitRadius > lastOrbitRadius)
        {
            orbitRadius = thisOrbitRadius;
        }
        lastOrbitRadius = orbitRadius;
        
        Vector3D nextWaypoint = Vector3D.Normalize(centerToMissile) * orbitRadius + relativeForward + planetCenter;
        missileToTarget = nextWaypoint - missilePosition; //direction missile needs to travel
    }
    else
    {
        shouldDive = true;

        var descentVectorNorm = Vector3D.IsZero(centerToTarget) ? Vector3D.Zero : Vector3D.Normalize(centerToTarget);
        var missilePosProjected = VectorProjection(centerToMissile, centerToTarget);
        missileToTarget = planetCenter + missilePosProjected - descentVectorNorm * Math.Max(200, 2 * missileVelocity.Length()) - missilePosition;
        
        //missileToTarget = targetPosition - missilePosition;       
    }

    Vector3D headingVec = CalculateHeadingVector(missileToTarget, missileVelocity, true);
    double pitch = 0;
    double yaw = 0;
    GetRotationAngles(headingVec, missileReference, out yaw, out pitch);

    //Asymptotic decay angular control
    double pitchSpeed = pitchPID.Control(pitch);
    double yawSpeed = yawPID.Control(yaw);

    //aligning bottom of missile to gravity
    double rollAngle = 0; double rollSpeed = 0;
    var gravityVec = missileReference.GetNaturalGravity();
    var planetRelativeLeftVec = missileReference.WorldMatrix.Forward.Cross(gravityVec);
    if (gravityVec.LengthSquared() > 0)
    {
        rollAngle = VectorAngleBetween(missileReference.WorldMatrix.Left, planetRelativeLeftVec);
        rollAngle *= Math.Sign(missileReference.WorldMatrix.Left.Dot(gravityVec)); 

        rollSpeed = rollAngle;
    }

    //Apply gyro override
    ApplyGyroOverride(pitchSpeed, yawSpeed, rollSpeed, gyros, missileReference);
}

/// <summary>
/// Calculates a heading that will compensate for drift
/// </summary>
/// <param name="targetVec">Desired travel direction</param>
/// <param name="velocityVec">Current velocity direction</param>
/// <param name="driftComp">Drift compensation scaling factor</param>
/// <returns>Compensated heading vector</returns>
Vector3D CalculateHeadingVector(Vector3D targetVec, Vector3D velocityVec, bool driftComp)
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
        return VectorReflection(velocityVec, targetVec, 1);
    }
    else
    {
        return -velocityVec;
    }
}

double lastOrbitRadius = -1;
/// <summary>
///Gets necessary radius from planet center to fly at a certain altitude
/// </summary>
/// <param name="reference">Ship controller to gather information with</param>
/// <param name="planetRadius">Radius of planet</param>
/// <param name="desiredAltitude">Desired altitude</param>
/// <returns>Updated flight radius from planet center</returns>
double UpdateFlightRadius(IMyShipController reference, double planetRadius, double desiredAltitude)
{
    double seaLevel;
    reference.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out seaLevel);
    
    double surface;
    reference.TryGetPlanetElevation(MyPlanetElevation.Surface, out surface);
    
    Echo($"sea: {seaLevel}\nsurf: {surface}");
    
    double difference = seaLevel - surface;
    if (difference < 0)
    {
        return planetRadius + desiredAltitude;
    }
    else
    {
        return planetRadius + difference + desiredAltitude;
    }
}

/// <summary>
/// This attempts to parse a string as a GPS coordinate
/// </summary>
/// <param name="gpsString">GPS coordinate string</param>
/// <param name="vector">Output position vector</param>
/// <returns>If parse was successful</returns>
bool TryParseGPS(string gpsString, out Vector3D vector)
{
    vector = new Vector3D(0, 0, 0);

    var gpsStringSplit = gpsString.Split(':');

    double x, y, z;

    if (gpsStringSplit.Length != 6)
        return false;

    bool passX = double.TryParse(gpsStringSplit[2], out x);
    bool passY = double.TryParse(gpsStringSplit[3], out y);
    bool passZ = double.TryParse(gpsStringSplit[4], out z);

    //Echo($"{x},{y},{z}");

    if (passX && passY && passZ)
    {
        vector = new Vector3D(x, y, z);
        return true;
    }
    else
        return false;
}

/*
/// Whip's Get Rotation Angles Method v12 - 2/16/18 ///
Dependencies: VectorAngleBetween()
* Fix to solve for zero cases when a vertical target vector is input
* Fixed straight up case
* Fixed sign on straight up case
* Converted math to local space
*/
void GetRotationAngles(Vector3D targetVector, IMyTerminalBlock reference, out double yaw, out double pitch)
{
    var localTargetVector = Vector3D.TransformNormal(targetVector, MatrixD.Transpose(reference.WorldMatrix));
    var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);
    
    yaw = VectorAngleBetween(Vector3D.Forward, flattenedTargetVector) * Math.Sign(localTargetVector.X); //right is positive
    if (Math.Abs(yaw) < 1E-6 && localTargetVector.Z > 0) //check for straight back case
        yaw = Math.PI;
    
    if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
        pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
    else
        pitch = VectorAngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
}

//Whip's ApplyGyroOverride Method v9 - 8/19/17
void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list, IMyTerminalBlock reference) 
{ 
    var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed);
    
    var shipMatrix = reference.WorldMatrix;
    var relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipMatrix); 

    foreach (var thisGyro in gyro_list) 
    { 
        var gyroMatrix = thisGyro.WorldMatrix;
        var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(gyroMatrix)); 
 
        thisGyro.Pitch = (float)transformedRotationVec.X; //because keen does some weird stuff with signs 
        thisGyro.Yaw = (float)transformedRotationVec.Y; 
        thisGyro.Roll = (float)transformedRotationVec.Z; 
        thisGyro.GyroOverride = true; 
    } 
}

#region VECTOR FUNCTIONS
/// <summary>
/// Projects vector a onto vector b
/// </summary>
/// <param name="a">Vector to project</param>
/// <param name="b">Vector being projected on</param>
/// <returns>a projected on b</returns>
Vector3D VectorProjection( Vector3D a, Vector3D b )
{
    if (Vector3D.IsZero(b))
        return Vector3D.Zero;

    return a.Dot( b ) / b.LengthSquared() * b;  
}

/// <summary>
/// Computes angle between 2 vectors
/// </summary>
/// <param name="a"></param>
/// <param name="b"></param>
/// <returns>Angle between vectors in radians</returns>
double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
{
    if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
        return 0;
    else
        return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
}

/// <summary>
/// Reflects a vector over another
/// (Optional): Can also scale the rejection of the vector by a certain factor if desired
/// </summary>
/// <param name="a">Vector to reflect</param>
/// <param name="b">Vector to reflect over</param>
/// <param name="rejectionFactor">Rejection multiplication factor</param>
/// <returns>Reflected vector</returns>
Vector3D VectorReflection(Vector3D a, Vector3D b, double rejectionFactor = 1) //reflect a over b    
{
    Vector3D project_a = VectorProjection(a, b);
    Vector3D reject_a = a - project_a;
    Vector3D reflect_a = project_a - reject_a * rejectionFactor;
    return reflect_a;
}
#endregion

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
    public double Value {get; private set;}

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