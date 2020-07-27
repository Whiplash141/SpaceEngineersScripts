/*
/ //// / Gyro Memory Script / //// /
Version 1.1.0 - 2020/07/27
Description
    Saves the orientation of a ship so that you can align yourself to that same
    orientation later. (Requested by reddit user u/Thelycan001)
    
Instructions
    1. Place this script in a programmable block on your main grid
    2. Have some gyros on your ship (obviously lol)
    3. Run the code with desired arguments
    
Arguments
    on
    off
    toggle
    save
    
    The above arguments should be pretty straight forward (I hope). Save will store
    your current orientation to the program for later use. It will also save it 
    in such a way that even if you reload the world, the orientation will still
    be saved.
    
Enjoy!
*/


// No touchy below
List<IMyGyro> _gyros = new List<IMyGyro>();
bool _hasSavedOrientation = false;
MatrixD _savedOrientation;
DateTime _savedTime;
bool _active = false;
const double RollTolerance = Math.PI / 18.0; // 10 degrees

Program()
{
    Load();

    GridTerminalSystem.GetBlocksOfType(_gyros, x => x.IsSameConstructAs(Me));
}

// Called automatically on game save
void Save()
{
    if (_hasSavedOrientation)
    {
        Storage = $"{_savedOrientation.Forward}@{_savedOrientation.Left}@{_savedOrientation.Up}@{_active}";
    }
}

void Load()
{
    Vector3D temp = Vector3D.Zero;
    string[] storageSplit = Storage.Split('@');
    if (storageSplit.Length != 4)
        return;
    // Get forward
    bool parsed = Vector3D.TryParse(storageSplit[0], out temp);
    if (!parsed)
        return;
    _savedOrientation.Forward = temp;

    // Get left
    parsed = Vector3D.TryParse(storageSplit[1], out temp);
    if (!parsed)
        return;
    _savedOrientation.Left = temp;

    // Get up
    parsed = Vector3D.TryParse(storageSplit[2], out temp);
    if (!parsed)
        return;
    _savedOrientation.Up = temp;

    // Get active
    parsed = bool.TryParse(storageSplit[3], out _active);
    if (parsed && _active)
    {
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
    }

    _hasSavedOrientation = true;
    _savedTime = DateTime.Now;
}

StringBuilder _echoBuilder = new StringBuilder();
new void Echo(string text)
{
    _echoBuilder.Append(text).Append('\n');
}

void FlushEcho()
{
    string output = _echoBuilder.ToString();
    base.Echo(output);
    _echoBuilder.Clear();
    var surf = Me.GetSurface(0);
    surf.WriteText(output);
    surf.ContentType = ContentType.TEXT_AND_IMAGE;
}

MyCommandLine _cmdLine = new MyCommandLine();
void Main(string arg, UpdateType updateSource)
{
    _cmdLine.TryParse(arg);
    for (int i = 0; i < _cmdLine.ArgumentCount; ++i)
    {
        switch (_cmdLine.Argument(i))
        {
            case "on":
                if (_hasSavedOrientation)
                {
                    _active = true;
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    _savedTime = DateTime.Now;
                }
                else
                {
                    Echo("Warning: Can't turn on without\nsaved orientation!\n");
                }
                break;
            case "off":
                _active = false;
                Runtime.UpdateFrequency = UpdateFrequency.None;
                break;
            case "toggle":
                _active = !_active;
                if (_active)
                {
                    if (_hasSavedOrientation)
                    {
                        Runtime.UpdateFrequency = UpdateFrequency.Update10;
                        _savedTime = DateTime.Now;
                    }
                    else
                    {
                        Echo("Warning: Can't turn on without\nsaved orientation!\n");
                        _active = false;
                    }
                }
                else
                {
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                }
                break;
            case "save":
                _savedOrientation = Me.WorldMatrix;
                _hasSavedOrientation = true;
                _savedTime = DateTime.Now;
                Echo("Saved!");
                break;
            default:
                break;
        }
    }

    if ((updateSource & UpdateType.Update10) == 0)
    {
        Echo("Gyro Memory: Idle");
        if (_hasSavedOrientation)
        {
            Echo($"\nSaved orientation at {_savedTime}");
        }
        else
        {
            Echo("\nNo orientation saved yet");
        }

        // Remove destroyed blocks and disable override
        for (int i = _gyros.Count - 1; i >= 0; --i)
        {
            var gyro = _gyros[i];
            if (IsClosed(gyro))
            {
                _gyros.RemoveAt(i);
                continue;
            }

            if (gyro.GyroOverride)
                gyro.GyroOverride = false;
        }

        FlushEcho();
        return;
    }

    Echo("Gyro Memory: Active");
    Echo($"\nSaved orientation at {_savedTime}");
    Echo($"\nGyro count: {_gyros.Count}");

    double pitch, yaw, roll = 0;
    GetRotationAnglesSimultaneous(_savedOrientation, Me.WorldMatrix, out yaw, out pitch, out roll);

    Echo($"\nYaw angle: {MathHelper.ToRadians(yaw):n2}°");
    Echo($"Pitch angle: {MathHelper.ToRadians(pitch):n2}°");
    Echo($"Roll angle: {MathHelper.ToRadians(roll):n2}°");

    // Remove destroyed blocks
    for (int i = _gyros.Count - 1; i >= 0; --i)
    {
        var gyro = _gyros[i];
        if (IsClosed(gyro))
        {
            _gyros.RemoveAt(i);
            continue;
        }
    }

    if (Math.Abs(pitch) > RollTolerance || Math.Abs(yaw) > RollTolerance)
    {
        //roll = 0;
    }
    ApplyGyroOverride(pitch, yaw, roll, _gyros, Me.WorldMatrix);

    FlushEcho();
}

bool IsClosed(IMyTerminalBlock b)
{
    return GridTerminalSystem.GetBlockWithId(b.EntityId) == null;
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
Whip's GetRotationAnglesSimultaneous - Last modified: 07/27/2020

Gets axis angle rotation and decomposes it upon each cardinal axis.
Has the desired effect of not causing roll oversteer. Does NOT use
sequential rotation angles.

MODIFIED:
Uses a desiredWorldMatrix instead of desired forward and up vectors now

Dependencies:
VectorMath.SafeNormalize
*/
void GetRotationAnglesSimultaneous(MatrixD desiredWorldMatrix, MatrixD worldMatrix, out double yaw, out double pitch, out double roll)
{
    MatrixD transposedWm;
    MatrixD.Transpose(ref worldMatrix, out transposedWm);
    Vector3D axis;
    double angle;
    MatrixD targetMatrix = desiredWorldMatrix * transposedWm;
    axis = new Vector3D(targetMatrix.M23 - targetMatrix.M32,
                        targetMatrix.M31 - targetMatrix.M13,
                        targetMatrix.M12 - targetMatrix.M21);

    double trace = targetMatrix.M11 + targetMatrix.M22 + targetMatrix.M33;
    angle = Math.Acos(MathHelper.Clamp((trace - 1) * 0.5, -1, 1));

    if (Vector3D.IsZero(axis))
    {
        angle = targetMatrix.Forward.Z < 0 ? 0 : Math.PI;
        yaw = angle;
        pitch = 0;
        roll = 0;
        return;
    }

    if (!Vector3D.IsZero(axis) && !Vector3D.IsUnit(ref axis))
    {
        axis = Vector3D.Normalize(axis); 
    }
    yaw = -axis.Y * angle;
    pitch = axis.X * angle;
    roll = -axis.Z * angle;
}

// Computes angle between 2 vectors in radians.
public static double VectorAngleBetween(Vector3D a, Vector3D b)
{
    if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
        return 0;
    else
        return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
}
