/*
Whip's GetRotationAnglesSimultaneous - Last modified: 05/17/2020

Gets axis angle rotation and decomposes it upon each cardinal axis.
Has the desired effect of not causing roll oversteer. Does NOT use
sequential rotation angles.

Dependencies:
VectorMath.SafeNormalize
*/
static void GetRotationAnglesSimultaneous(Vector3D desiredForwardVector, Vector3D desiredUpVector, MatrixD worldMatrix, out double yaw, out double pitch, out double roll)
{
    MatrixD transposedWm;
    MatrixD.Transpose(ref worldMatrix, out transposedWm); 
    Vector3D.Rotate(ref desiredForwardVector, ref transposedWm, out desiredForwardVector);
    Vector3D.Rotate(ref desiredUpVector, ref transposedWm, out desiredUpVector);

    Vector3D leftVector = Vector3D.Cross(desiredUpVector, desiredForwardVector);
    Vector3D axis;
    double angle;
    if (Vector3D.IsZero(desiredUpVector) || Vector3D.IsZero(leftVector))
    {
        desiredForwardVector = VectorMath.SafeNormalize(desiredForwardVector);
        axis = Vector3D.Cross(Vector3D.Forward, desiredForwardVector);
        angle = Math.Asin(axis.Length());
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

        axis = Vector3D.Cross(Vector3D.Backward, targetMatrix.Backward)
             + Vector3D.Cross(Vector3D.Up, targetMatrix.Up)
             + Vector3D.Cross(Vector3D.Right, targetMatrix.Right);

        double trace = targetMatrix.M11 + targetMatrix.M22 + targetMatrix.M33;
        angle = Math.Acos((trace - 1) * 0.5);
    }

    axis = VectorMath.SafeNormalize(axis);
    yaw = -axis.Y * angle;
    pitch = axis.X * angle;
    roll = -axis.Z * angle;
}
