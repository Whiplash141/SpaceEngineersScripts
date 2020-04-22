
/*
/ //// / Whip's Velocity Alignment Script / //// /

v1.0.0 - 04/21/2020

This aligns a ship towards the velocity vector.

*/

//==========================================================
/////////////////// NO TOUCHEY BELOW ///////////////////////
//==========================================================

const double RotationGain = 1;
const double RollToleranceAngle = 10.0 / 180.0 * Math.PI;
bool _isSetup = false;
bool _error = true;

IMyShipController _controller;
List<IMyGyro> _gyros = new List<IMyGyro>();

Program()
{
    Echo("Run with argument 'on' to enable");
    Echo("\nRun with argument 'off' to disable");

    _controller = GetFirstBlockOfType<IMyShipController>();
}

void Setup()
{
    if (_isSetup)
        return;
    _controller = GetFirstBlockOfType<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType(_gyros);
    _isSetup = true;
    _error = false;
    if (_gyros.Count == 0)
    {
        Echo("> ERROR: No gyros");
        _error = true;
    }

    if (_controller == null)
    {
        Echo("> ERROR: No ship controller");
        _error = true;
    }
}

const double tick = 1.0 / 60.0;
void Main(string arg, UpdateType updateSource)
{
    if (arg.Equals("on", StringComparison.OrdinalIgnoreCase))
    {
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
    }
    else if (arg.Equals("off", StringComparison.OrdinalIgnoreCase))
    {
        Runtime.UpdateFrequency = UpdateFrequency.None;
    }
    else if (arg.Equals("setup", StringComparison.OrdinalIgnoreCase))
    {
        _isSetup = false;
        Setup();
    }

    if ((updateSource & UpdateType.Update10) == 0)
        return;

    Setup();

    Echo($"Whip's Velocity Alignment\n Script Online {RunningSymbol()}");

    if (_error)
    {
        Echo($"> There is an error!");
        return;
    }

    Vector3D shipVelocity = _controller.GetShipVelocities().LinearVelocity;
    double yaw = 0, pitch = 0, roll = 0;
    GetRotationAngles(shipVelocity, _controller.WorldMatrix.Up, _controller.WorldMatrix, out yaw, out pitch, out roll);

    double yawSpeed, pitchSpeed, rollSpeed;
    yawSpeed = yaw * RotationGain;
    pitchSpeed = pitch * RotationGain;
    rollSpeed = roll * RotationGain;
    if (Math.Abs(yaw) + Math.Abs(pitch) > RollToleranceAngle)
    {
        rollSpeed = 0;
    }

    ApplyGyroOverride(pitchSpeed, yawSpeed, rollSpeed, _gyros, _controller.WorldMatrix);
}

//Whip's ApplyGyroOverride Method v12 - 11/02/2019
void ApplyGyroOverride(double pitchSpeed, double yawSpeed, double rollSpeed, List<IMyGyro> gyroList, MatrixD worldMatrix)
{
    var rotationVec = new Vector3D(-pitchSpeed, yawSpeed, rollSpeed); //because keen does some weird stuff with signs 
    var relativeRotationVec = Vector3D.TransformNormal(rotationVec, worldMatrix);

    foreach (var thisGyro in gyroList)
    {
        var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(thisGyro.WorldMatrix));

        thisGyro.Pitch = (float)transformedRotationVec.X;
        thisGyro.Yaw = (float)transformedRotationVec.Y;
        thisGyro.Roll = (float)transformedRotationVec.Z;
        thisGyro.GyroOverride = true;
    }
}

/*
/// Whip's Get Rotation Angles Method v16 - 9/25/18 ///
Dependencies: VectorMath.AngleBetween
Note: Set desiredUpVector to Vector3D.Zero if you don't care about roll
*/
void GetRotationAngles(Vector3D desiredForwardVector, Vector3D desiredUpVector, MatrixD worldMatrix, out double yaw, out double pitch, out double roll)
{
    var localTargetVector = Vector3D.Rotate(desiredForwardVector, MatrixD.Transpose(worldMatrix));
    var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);

    yaw = AngleBetween(Vector3D.Forward, flattenedTargetVector) * Math.Sign(localTargetVector.X); //right is positive
    if (Math.Abs(yaw) < 1E-6 && localTargetVector.Z > 0) //check for straight back case
        yaw = Math.PI;

    if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
        pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
    else
        pitch = AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive

    if (Vector3D.IsZero(desiredUpVector))
    {
        roll = 0;
        return;
    }
    var localUpVector = Vector3D.Rotate(desiredUpVector, MatrixD.Transpose(worldMatrix));
    var flattenedUpVector = new Vector3D(localUpVector.X, localUpVector.Y, 0);
    roll = AngleBetween(flattenedUpVector, Vector3D.Up) * Math.Sign(Vector3D.Dot(Vector3D.Right, flattenedUpVector));
}

/// <summary>
/// Computes angle between 2 vectors in radians.
/// </summary>
public static double AngleBetween(Vector3D a, Vector3D b)
{
    if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
        return 0;
    else
        return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
}

List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
T GetFirstBlockOfType<T>(string filterName = "") where T : class, IMyTerminalBlock
{
    blocks.Clear();
    if (filterName == "")
        GridTerminalSystem.GetBlocksOfType<T>(blocks);
    else
        GridTerminalSystem.GetBlocksOfType<T>(blocks, x => x.CustomName.Contains(filterName));

    return blocks.Count > 0 ? blocks[0] as T : null;
}

//Whip's Running Symbol Method v8
//•
int runningSymbolVariant = 0;
int runningSymbolCount = 0;
const int increment = 10;
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
