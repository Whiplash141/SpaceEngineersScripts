/*
Whip's GetRotationAnglesSimultaneous - Last modified: 2020/08/27

Gets axis angle rotation and decomposes it upon each cardinal axis.
Has the desired effect of not causing roll oversteer. Does NOT use
sequential rotation angles.

NOTE: 
This is designed for use with Keen's gyroscopes where:
  + pitch = -X rotation
  + yaw   = -Y rotation
  + roll  = -Z rotation

As such, the resulting angles are negated since they are computed
using the opposite sign assumption.

Dependencies:
VectorMath.SafeNormalize
*/
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
        axis = new Vector3D(desiredForwardVector.Y, -desiredForwardVector.X, 0);
        angle = Math.Acos(MathHelper.Clamp(-desiredForwardVector.Z, -1.0, 1.0));
    }
    else
    {
        leftVector = VectorMath.SafeNormalize(leftVector);
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

    axis = VectorMath.SafeNormalize(axis);
    // Because gyros rotate about -X -Y -Z, we need to negate our angles
    yaw = -axis.Y * angle;
    pitch = -axis.X * angle;
    roll = -axis.Z * angle;
}
