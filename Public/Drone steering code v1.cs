/*
/ //// / Whip's Drone Steering Code v1 - 5/10/18

Instructions:
* Place this program on your drone
* Make a group named "Drone Blocks" with the following blocks:
    - One or more turrets
    - One or more gyros
    - One or more fixed guns
    - A remote or control seat (This tells the drone which way is forward)
*/

const string droneGroupName = "Drone Blocks";
const double fixedWeaponMuzzleVelocity = 400; // m/s

const double kP = 2;
const double kI = 0;
const double kD = 0.1;

const double maxWeaponDeviationDeg = 5;
double maxWeaponDeviationRad = 0;

PID pitchPID = new PID(kP, kI, kD, 0.1, 1.0 / 6.0);
PID yawPID = new PID(kP, kI, kD, 0.1, 1.0 / 6.0);
PID rollPID = new PID(kP, kI, kD, 0.1, 1.0 / 6.0);

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Once; //workaround to a DS bug
    maxWeaponDeviationRad = MathHelper.ToRadians(maxWeaponDeviationDeg);
}

bool isSetup = false;

void Main(string arg, UpdateType updateSource)
{
    //This is a workarounddddddd
    if ((Runtime.UpdateFrequency & UpdateFrequency.Update10) == 0)
    {
        Runtime.UpdateFrequency = (UpdateFrequency.Update10 | UpdateFrequency.Update100);
    }

    if ((updateSource & UpdateType.Update100) != 0 || !isSetup)
        isSetup = GrabBlocks();

    if ((updateSource & UpdateType.Update10) == 0)
        return;

    if (!isSetup)
         return;

    Echo($"WMI Drone Steering System\n Online{RunningSymbol()}");

    try
    {
        SteerDrone();
    }
    catch
    {
        isSetup = false;
    }
}

void SteerDrone()
{
    GetTargetingTurrets(turrets);
    if (targetingTurrets.Count == 0)
    {
        Echo("No targets detected...");
        return;
    }

    Echo("Target detected!");
    var target = targetingTurrets[0].GetTargetedEntity();
    var targetPosition = target.Position;
    var targetVelocity = target.Velocity;

    var thisController = controllers[0];
    var dronePosition = thisController.GetPosition();
    var droneVelocity = thisController.GetShipVelocities().LinearVelocity;

    //Calculate intercept
    double timeToIntercept = 0;
    var interceptPoint = CalculateProjectileIntercept(fixedWeaponMuzzleVelocity, droneVelocity, dronePosition, targetVelocity, targetPosition, out timeToIntercept);

    var interceptHeading = interceptPoint - dronePosition;

    //Calculate rotation angles
    double pitch = 0;
    double yaw = 0;
    GetRotationAngles(interceptHeading, thisController.WorldMatrix, out yaw, out pitch);

    //Calculate rotation speed
    double pitchSpeed = pitchPID.Control(pitch);
    double yawSpeed = yawPID.Control(yaw);

    //Apply rotation to gyros
    ApplyGyroOverride(pitchSpeed, yawSpeed, 0, gyros, thisController);

    //Check deviation angle and fire weapons if within tolerance
    var deviation = VectorAngleBetween(interceptHeading, thisController.WorldMatrix.Forward);
    FireWeapons(fixedGuns, deviation < maxWeaponDeviationRad);
}

void FireWeapons(List<IMyUserControllableGun> weapons, bool fire)
{
    foreach (var block in weapons)
    {
        if (fire)
            block.ApplyAction("Shoot_On");
        else
            block.ApplyAction("Shoot_Off");
    }
}

Vector3D CalculateProjectileIntercept(double projectileSpeed, Vector3D shooterVelocity, Vector3D shooterPosition, Vector3D targetVelocity, Vector3D targetPosition, out double timeToIntercept)
{
    var directHeading = targetPosition - shooterPosition;
    var directHeadingNorm = Vector3D.Normalize(directHeading);

    var relativeVelocity = targetVelocity - shooterVelocity;

    var parallelVelocity = relativeVelocity.Dot(directHeadingNorm) * directHeadingNorm;
    var normalVelocity = relativeVelocity - parallelVelocity;

    var diff = projectileSpeed * projectileSpeed - normalVelocity.LengthSquared();
    if (diff < 0)
    {
        timeToIntercept = 0;
        return targetPosition;
    }

    timeToIntercept = Math.Abs(directHeading.Dot(directHeadingNorm) / (projectileSpeed - parallelVelocity.Dot(directHeadingNorm)));// * -Math.Sign(Vector3D.Dot(directHeading, relativeVelocity)));
    var interceptPoint = timeToIntercept * (Math.Sqrt(diff) * directHeadingNorm + normalVelocity) + shooterPosition;

    //---------------------
    return interceptPoint;
    //---------------------
}

List<IMyUserControllableGun> fixedGuns = new List<IMyUserControllableGun>();
List<IMyLargeTurretBase> turrets = new List<IMyLargeTurretBase>();
List<IMyGyro> gyros = new List<IMyGyro>();
List<IMyShipController> controllers = new List<IMyShipController>();
List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();

bool GrabBlocks()
{
    fixedGuns.Clear();
    turrets.Clear();
    gyros.Clear();
    controllers.Clear();

    var droneGroup = GridTerminalSystem.GetBlockGroupWithName(droneGroupName);
    if (droneGroup == null)
    {
        Echo($"Error: No block group named '{droneGroupName}'");
        return false;
    }

    droneGroup.GetBlocks(allBlocks);

    foreach (var block in allBlocks)
    {
        if (block is IMyUserControllableGun)
        {
            if (block is IMyLargeTurretBase)
                turrets.Add(block as IMyLargeTurretBase);
            else
                fixedGuns.Add(block as IMyUserControllableGun);
        }
        else if (block is IMyGyro)
        {
            gyros.Add(block as IMyGyro);
        }
        else if (block is IMyShipController)
        {
            controllers.Add(block as IMyShipController);
        }
    }

    //Error handling
    bool error = false;
    if (turrets.Count == 0)
    {
        Echo("Error: No turrets in drone group");
        error = true;
    }

    if (gyros.Count == 0)
    {
        Echo("Error: No gyros in drone group");
        error = true;
    }

    if (controllers.Count == 0)
    {
        Echo("Error: No remotes or control seats in drone group");
        error = true;
    }

    if (fixedGuns.Count == 0)
    {
        Echo("Warning: No fixed guns in drone group");
    }

    return !error;
}

List<IMyLargeTurretBase> targetingTurrets = new List<IMyLargeTurretBase>();

void GetTargetingTurrets(List<IMyLargeTurretBase> allDesignators)
{
    targetingTurrets.Clear();
    foreach (var block in allDesignators)
    {
        if (block.HasTarget && !block.IsUnderControl)
        {
            targetingTurrets.Add(block);
        }
    }
}

/*
/// Whip's Get Rotation Angles Method v13 - 2/16/18 ///
Dependencies: VectorAngleBetween()
* Fix to solve for zero cases when a vertical target vector is input
* Fixed straight up case
* Fixed sign on straight up case
* Converted math to local space
*/
void GetRotationAngles(Vector3D targetVector, MatrixD worldMatrix, out double yaw, out double pitch)
{
    var localTargetVector = Vector3D.TransformNormal(targetVector, MatrixD.Transpose(worldMatrix));
    var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);

    yaw = VectorAngleBetween(Vector3D.Forward, flattenedTargetVector) * Math.Sign(localTargetVector.X); //right is positive
    if (Math.Abs(yaw) < 1E-6 && localTargetVector.Z > 0) //check for straight back case
        yaw = Math.PI;

    if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
        pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
    else
        pitch = VectorAngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
}

double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
{
    if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
        return 0;
    else
        return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
}

//Whip's ApplyGyroOverride Method v10 - 8/19/17
void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list, IMyTerminalBlock reference)
{
    var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed); //because keen does some weird stuff with signs 
    var shipMatrix = reference.WorldMatrix;
    var relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipMatrix);

    foreach (var thisGyro in gyro_list)
    {
        var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(thisGyro.WorldMatrix));

        thisGyro.Pitch = (float)transformedRotationVec.X;
        thisGyro.Yaw = (float)transformedRotationVec.Y;
        thisGyro.Roll = (float)transformedRotationVec.Z;
        thisGyro.GyroOverride = true;
    }
}

//Whip's PID controller class v6 - 11/22/17
public class PID
{
    double _kP = 0;
    double _kI = 0;
    double _kD = 0;
    double _integralDecayRatio = 0;
    double _lowerBound = 0;
    double _upperBound = 0;
    double _timeStep = 0;
    double _inverseTimeStep = 0;
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
        _inverseTimeStep = 1 / _timeStep;
        _integralDecay = false;
    }

    public PID(double kP, double kI, double kD, double integralDecayRatio, double timeStep)
    {
        _kP = kP;
        _kI = kI;
        _kD = kD;
        _timeStep = timeStep;
        _inverseTimeStep = 1 / _timeStep;
        _integralDecayRatio = integralDecayRatio;
        _integralDecay = true;
    }

    public double Control(double error)
    {
        //Compute derivative term
        var errorDerivative = (error - _lastError) * _inverseTimeStep;

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

    public double Control(double error, double timeStep)
    {
        _timeStep = timeStep;
        _inverseTimeStep = 1 / _timeStep;
        return Control(error);
    }

    public void Reset()
    {
        _errorSum = 0;
        _lastError = 0;
        _firstRun = true;
    }
}

//Whip's Running Symbol Method v8
//•
int runningSymbolVariant = 0;
int runningSymbolCount = 0;
const int increment = 1;
string[] runningSymbols = new string[] { "−", "\\", "|", "/" };

string RunningSymbol()
{
    if (runningSymbolCount >= increment)
    {
        runningSymbolCount = 0;
        runningSymbolVariant++;
        if (runningSymbolVariant >= runningSymbols.Length)
            runningSymbolVariant = 0;
    }
    runningSymbolCount++;
    return runningSymbols[runningSymbolVariant];
}