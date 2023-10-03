/*
/ //// / Point Ship at GPS - 2023-10-03 / //// /

Simple proof of concept script to point a ship's reference block at
an input GPS coordinate.

Place a ship controller on your ship with the name "Reference", then
place one or more gyros on your ship.

Then run the script with a GPS coordinate as an argument and the ship will
point at that GPS.
*/
const string ReferenceName = "Reference";

List<IMyGyro> _gyros = new List<IMyGyro>();
IMyShipController _reference = null;
Vector3D? _target = null;

Program()
{
    _reference = GridTerminalSystem.GetBlockWithName(ReferenceName) as IMyShipController;
    GridTerminalSystem.GetBlocksOfType(_gyros);

    bool error = false;
    if (_reference == null)
    {
        error = true;
        Echo($"ERROR: No ship controller with name {ReferenceName}");
    }

    if (_gyros.Count == 0)
    {
        error = true;
        Echo("ERROR: No gyros found");
    }
    
    if (!error)
    {
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
    }
}

void Main(string arg, UpdateType source)
{
    if (!string.IsNullOrWhiteSpace(arg))
    {
        Vector3D gpsPos;
        if (TryParseVector3DFromGPS(arg, out gpsPos))
        {
            _target = gpsPos;
        }
    }
    
    if ((source & UpdateType.Update10) == 0)
    {
        return;
    }

    if (!_target.HasValue)
    {
        Echo("No target GPS location entered");
        return;
    }

    Echo($"Aiming at: {_target.Value}");

    Vector3D directionToTarget = _target.Value - _reference.GetPosition();

    double pitch, yaw, roll;
    Vector3D desiredUp = Vector3D.Zero; // Disable up constraint because it isn't needed for this example
    GetRotationAnglesSimultaneous(directionToTarget, desiredUp, _reference.WorldMatrix, out pitch, out yaw, out roll);

    Echo($"Pitch: {pitch}");
    Echo($"Yaw: {yaw}");
    Echo($"Roll: {roll}");

    // Simple unity gain P controller, you could replace this with a PID for pitch, yaw, and roll
    ApplyGyroOverride(pitch, yaw, roll, _gyros, _reference.WorldMatrix);
}


/*
Whip's ApplyGyroOverride - Last modified: 2020/08/27

Takes pitch, yaw, and roll speeds relative to the gyro's backwards
ass rotation axes. 
*/
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

public static class VectorMath
{
    /// <summary>
    /// Normalizes a vector only if it is non-zero and non-unit
    /// </summary>
    public static Vector3D SafeNormalize(Vector3D a)
    {
        if (Vector3D.IsZero(a))
            return Vector3D.Zero;

        if (Vector3D.IsUnit(ref a))
            return a;

        return Vector3D.Normalize(a);
    }

    /// <summary>
    /// Reflects vector a over vector b with an optional rejection factor
    /// </summary>
    public static Vector3D Reflection(Vector3D a, Vector3D b, double rejectionFactor = 1)
    {
        Vector3D proj = Projection(a, b);
        Vector3D rej = a - proj;
        return proj - rej * rejectionFactor;
    }

    /// <summary>
    /// Rejects vector a on vector b
    /// </summary>
    public static Vector3D Rejection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a - a.Dot(b) / b.LengthSquared() * b;
    }

    /// <summary>
    /// Projects vector a onto vector b
    /// </summary>
    public static Vector3D Projection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return Vector3D.Zero;
        
        if (Vector3D.IsUnit(ref b))
            return a.Dot(b) * b;

        return a.Dot(b) / b.LengthSquared() * b;
    }

    /// <summary>
    /// Scalar projection of a onto b
    /// </summary>
    public static double ScalarProjection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;

        if (Vector3D.IsUnit(ref b))
            return a.Dot(b);

        return a.Dot(b) / b.Length();
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

    /// <summary>
    /// Computes cosine of the angle between 2 vectors.
    /// </summary>
    public static double CosBetween(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;
        else
            return MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);
    }

    /// <summary>
    /// Returns if the normalized dot product between two vectors is greater than the tolerance.
    /// This is helpful for determining if two vectors are "more parallel" than the tolerance.
    /// </summary>
    public static bool IsDotProductWithinTolerance(Vector3D a, Vector3D b, double tolerance)
    {
        double dot = Vector3D.Dot(a, b);
        double num = a.LengthSquared() * b.LengthSquared() * tolerance * Math.Abs(tolerance);
        return Math.Abs(dot) * dot > num;
    }
}

/// Whip's GetRotationAnglesSimultaneous - Last modified: 2022/08/10
/// <summary>
/// <para>
///     This method computes the axis-angle rotation required to align the
///     reference world matrix with the desired forward and up vectors.
/// </para>
/// <para>
///     The desired forward and up vectors are used to construct the desired
///     target orientation relative to the current world matrix orientation.
///     The current orientation of the craft with respect to itself will be the
///     identity matrix, thus the error between our desired orientation and our
///     target orientation is simply the target orientation itself:
///     M_target = M_current * M_error =>
///     M_target = I * M_error =>
///     M_target = M_error
/// </para>
/// <para>
///     This is designed for use with Keen's gyroscopes where:
///     + pitch = -X rotation,
///     + yaw   = -Y rotation,
///     + roll  = -Z rotation
/// </para>
/// </summary>
/// <remarks>
///     Dependencies: <c>VectorMath.SafeNormalize</c>
/// </remarks>
/// <param name="desiredForwardVector">
///     Desired forward direction in world frame.
///     This is the primary constraint used to allign pitch and yaw.
/// </param>
/// <param name="desiredUpVector">
///     Desired up direction in world frame.
///     This is the secondary constraint used to align roll. 
///     Set to <c>Vector3D.Zero</c> if roll control is not desired.
/// </param>
/// <param name="worldMatrix">
///     World matrix describing current orientation.
///     The translation part of the matrix is ignored; only the orientation matters.
/// </param>
/// <param name="pitch">Pitch angle to desired orientation (rads).</param>
/// <param name="yaw">Yaw angle to desired orientation (rads).</param>
/// <param name="roll">Roll angle to desired orientation (rads).</param>
public static void GetRotationAnglesSimultaneous(Vector3D desiredForwardVector, Vector3D desiredUpVector, MatrixD worldMatrix, out double pitch, out double yaw, out double roll)
{
    desiredForwardVector = VectorMath.SafeNormalize(desiredForwardVector);

    MatrixD transposedWm;
    MatrixD.Transpose(ref worldMatrix, out transposedWm);
    Vector3D.Rotate(ref desiredForwardVector, ref transposedWm, out desiredForwardVector);
    Vector3D.Rotate(ref desiredUpVector, ref transposedWm, out desiredUpVector);

    Vector3D leftVector = Vector3D.Cross(desiredUpVector, desiredForwardVector);
    Vector3D axis;
    double angle;
    
    if (Vector3D.IsZero(desiredUpVector) || Vector3D.IsZero(leftVector))
    {
        /*
         * Simple case where we have no valid roll constraint:
         * We merely cross the current forward vector (Vector3D.Forward) on the 
         * desiredForwardVector.
         */
        axis = new Vector3D(-desiredForwardVector.Y, desiredForwardVector.X, 0);
        angle = Math.Acos(MathHelper.Clamp(-desiredForwardVector.Z, -1.0, 1.0));
    }
    else
    {
        /*
         * Here we need to construct the target orientation matrix so that we
         * can extract the error from it in axis-angle representation.
         */
        leftVector = VectorMath.SafeNormalize(leftVector);
        Vector3D upVector = Vector3D.Cross(desiredForwardVector, leftVector);
        MatrixD targetOrientation = new MatrixD()
        {
            Forward = desiredForwardVector,
            Left = leftVector,
            Up = upVector,
        };

        axis = new Vector3D(targetOrientation.M32 - targetOrientation.M23,
                            targetOrientation.M13 - targetOrientation.M31,
                            targetOrientation.M21 - targetOrientation.M12);

        double trace = targetOrientation.M11 + targetOrientation.M22 + targetOrientation.M33;
        angle = Math.Acos(MathHelper.Clamp((trace - 1) * 0.5, -1.0, 1.0));
    }

    if (Vector3D.IsZero(axis))
    {
        /*
         * Degenerate case where we get a zero axis. This means we are either
         * exactly aligned or exactly anti-aligned. In the latter case, we just
         * assume the yaw is PI to get us away from the singularity.
         */
        angle = desiredForwardVector.Z < 0 ? 0 : Math.PI;
        yaw = angle;
        pitch = 0;
        roll = 0;
        return;
    }

    Vector3D axisAngle = VectorMath.SafeNormalize(axis) * angle;
    yaw = axisAngle.Y;
    pitch = axisAngle.X;
    roll = axisAngle.Z;
}
/*
Parses a GPS string as a Vector3D. 

Accepted formats:
    GPS:[^:]*:X:Y:Z:.*
*/
static bool TryParseVector3DFromGPS(string gps, out Vector3D vec)
{
    vec = Vector3D.Zero;
    string[] segments = gps.Split(':');

    if (segments.Length < 6) // Because terminated with a colon
    {
        return false;
    }

    if (segments[0] != "GPS")
    {
        return false;
    }

    if (!double.TryParse(segments[2], out vec.X) || !double.TryParse(segments[3], out vec.Y) || !double.TryParse(segments[4], out vec.Z))
    {
        return false;
    }

    return true;
}
