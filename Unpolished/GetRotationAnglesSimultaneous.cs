
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
void GetRotationAnglesSimultaneous(Vector3D desiredForwardVector, Vector3D desiredUpVector, MatrixD worldMatrix, out double pitch, out double yaw, out double roll)
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
