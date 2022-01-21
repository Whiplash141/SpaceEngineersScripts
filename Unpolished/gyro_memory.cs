/*
/ //// / Gyro Memory Script / //// /
Version 1.1.1 - 2021/1/13
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
    GetRotationAnglesSimultaneous(_savedOrientation.Forward, _savedOrientation.Up, Me.WorldMatrix, out yaw, out pitch, out roll);
    ApplyGyroOverride(pitch, yaw, roll, _gyros, Me.WorldMatrix);

    Echo($"\nYaw angle: {MathHelper.ToRadians(yaw):n2}°");
    Echo($"Pitch angle: {MathHelper.ToRadians(pitch):n2}°");
    Echo($"Roll angle: {MathHelper.ToRadians(roll):n2}°");

    FlushEcho();
}

bool IsClosed(IMyTerminalBlock b)
{
    return GridTerminalSystem.GetBlockWithId(b.EntityId) == null;
}

//Whip's ApplyGyroOverride Method v12 - 11/02/2019
void ApplyGyroOverride(double pitchSpeed, double yawSpeed, double rollSpeed, List<IMyGyro> gyroList, MatrixD worldMatrix)
{
    var rotationVec = new Vector3D(pitchSpeed, yawSpeed, rollSpeed); 
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
Whip's GetRotationAnglesSimultaneous - Last modified: 07/05/2020
Gets axis angle rotation and decomposes it upon each cardinal axis.
Has the desired effect of not causing roll oversteer. Does NOT use
sequential rotation angles.
Set desiredUpVector to Vector3D.Zero if you don't care about roll.

Dependencies:
SafeNormalize
*/
void GetRotationAnglesSimultaneous(Vector3D desiredForwardVector, Vector3D desiredUpVector, MatrixD worldMatrix, out double yaw, out double pitch, out double roll)
{
    desiredForwardVector = SafeNormalize(desiredForwardVector);

    MatrixD transposedWm;
    MatrixD.Transpose(ref worldMatrix, out transposedWm);
    Vector3D.Rotate(ref desiredForwardVector, ref transposedWm, out desiredForwardVector);
    Vector3D.Rotate(ref desiredUpVector, ref transposedWm, out desiredUpVector);

    Vector3D leftVector = Vector3D.Cross(desiredUpVector, desiredForwardVector);
    Vector3D axis;
    double angle;
    if (Vector3D.IsZero(desiredUpVector) || Vector3D.IsZero(leftVector))
    {
        axis = new Vector3D(desiredForwardVector.Y, -desiredForwardVector.X, 0);
        angle = Math.Acos(MathHelper.Clamp(-desiredForwardVector.Z, -1.0, 1.0));
    }
    else
    {
        leftVector = SafeNormalize(leftVector);
        Vector3D upVector = Vector3D.Cross(desiredForwardVector, leftVector);

        // Create matrix
        MatrixD targetMatrix = MatrixD.Zero;
        targetMatrix.Forward = desiredForwardVector;
        targetMatrix.Left = leftVector;
        targetMatrix.Up = upVector;

        axis = new Vector3D(targetMatrix.M23 - targetMatrix.M32,
                            targetMatrix.M31 - targetMatrix.M13,
                            targetMatrix.M12 - targetMatrix.M21);

        double trace = targetMatrix.M11 + targetMatrix.M22 + targetMatrix.M33;
        angle = Math.Acos(MathHelper.Clamp((trace - 1) * 0.5, -1, 1));
    }

    if (Vector3D.IsZero(axis))
    {
        angle = desiredForwardVector.Z < 0 ? 0 : Math.PI;
        yaw = angle;
        pitch = 0;
        roll = 0;
        return;
    }

    axis = SafeNormalize(axis);
    yaw = -axis.Y * angle;
    pitch = -axis.X * angle;
    roll = -axis.Z * angle;
}

public static Vector3D SafeNormalize(Vector3D a)
{
    if (Vector3D.IsZero(a))
        return Vector3D.Zero;

    if (Vector3D.IsUnit(ref a))
        return a;

    return Vector3D.Normalize(a);
}
