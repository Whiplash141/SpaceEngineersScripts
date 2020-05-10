/*
/// Whip's Get Rotation Angles Method v17 - 05/09/20 ///
Dependencies: VectorMath
Note: Set desiredUpVector to Vector3D.Zero if you don't care about roll
*/
void GetRotationAngles(Vector3D desiredForwardVector, Vector3D desiredUpVector, MatrixD worldMatrix, out double yaw, out double pitch, out double roll)
{
    var localTargetVector = Vector3D.Rotate(desiredForwardVector, MatrixD.Transpose(worldMatrix));
    var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);

    int yawSign = localTargetVector.X >= 0 ? 1 : -1;
    yaw = VectorMath.AngleBetween(Vector3D.Forward, flattenedTargetVector) * yawSign; //right is positive

    int pitchSign = Math.Sign(localTargetVector.Y);
    if (Vector3D.IsZero(flattenedTargetVector))
    { //check for straight up case
        pitch = MathHelper.PiOver2 * pitchSign;
    }
    else
    {
        pitch = VectorMath.AngleBetween(localTargetVector, flattenedTargetVector) * pitchSign; //up is positive
    }

    if (Vector3D.IsZero(desiredUpVector))
    {
        roll = 0;
        return;
    }

    // Since there is a relationship between roll and the orientation of forward
    // we need to ensure that the up we are comparing is orthagonal to forward.
    Vector3D orthagonalUp;
    Vector3D orthagonalLeft = Vector3D.Cross(desiredUpVector, desiredForwardVector);
    if (Vector3D.Dot(desiredForwardVector, desiredUpVector) == 0)
    {
        orthagonalUp = desiredUpVector;
    }
    else
    {
        orthagonalUp = Vector3D.Cross(desiredForwardVector, orthagonalLeft);
    }

    var localUpVector = Vector3D.Rotate(orthagonalUp, MatrixD.Transpose(worldMatrix));
    int signRoll = Vector3D.Dot(localUpVector, Vector3D.Right) >= 0 ? 1 : -1;

    // We are going to try and construct new intermediate axes where:
    // intermediateUp = Vector3D.Up
    // intermediateFront = flattenedTargetVector
    //
    // If flattenedTargetVector is zero, that means we are pointing either straight up
    // or straight down.
    Vector3D intermediateFront = flattenedTargetVector;
    if (Vector3D.IsZero(flattenedTargetVector)) 
    {
        // Desired forward and current up are parallel
        // This implies pitch is ±90° and yaw is 0.
        var localUpVectorFlattenedY = new Vector3D(localUpVector.X, 0, localUpVector.Z);

        // If straight up, reference direction would be backward,
        // if straight down, reference direction would be forward.
        // This is because we are simply doing a ±90° pitch rotation
        // of the axes.
        var referenceDirection = Vector3D.Dot(Vector3D.Up, localTargetVector) >= 0 ? Vector3D.Backward : Vector3D.Forward;
        
        roll = VectorMath.AngleBetween(localUpVectorFlattenedY, referenceDirection) * signRoll;
        return;
    }

    // Flatten up vector onto the intermediate up-right plane
    var localUpProjOnIntermediateForward = Vector3D.Dot(intermediateFront, localUpVector) / intermediateFront.LengthSquared() * intermediateFront;
    var flattenedUpVector = localUpVector - localUpProjOnIntermediateForward;

    var intermediateRight = Vector3D.Cross(intermediateFront, Vector3D.Up);
    int rollSign = Vector3D.Dot(flattenedUpVector, intermediateRight) >= 0 ? 1 : -1;
    roll = VectorMath.AngleBetween(flattenedUpVector, Vector3D.Up) * rollSign;
}
