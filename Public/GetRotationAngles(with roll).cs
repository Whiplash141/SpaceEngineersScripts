/*
/// Whip's Get Rotation Angles Method v17 - 05/09/20 ///
Dependencies: VectorMath
Note: Set desiredUpVector to Vector3D.Zero if you don't care about roll
*/
void GetRotationAngles(Vector3D desiredForwardVector, Vector3D desiredUpVector, MatrixD worldMatrix, out double yaw, out double pitch, out double roll)
{
    var localTargetVector = Vector3D.Rotate(desiredForwardVector, MatrixD.Transpose(worldMatrix));
    var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);

    int yawSign = Math.Sign(localTargetVector.X);
    if (yawSign == 0) { // Fixes sign for straight back case
        yawSign = 1;
    }
    yaw = VectorMath.AngleBetween(Vector3D.Forward, flattenedTargetVector) * yawSign; //right is positive

    int pitchSign = Math.Sign(localTargetVector.Y);
    if (Vector3D.IsZero(flattenedTargetVector)) { //check for straight up case
        pitch = MathHelper.PiOver2 * pitchSign;
    }
    else {
        pitch = VectorMath.AngleBetween(localTargetVector, flattenedTargetVector) * pitchSign; //up is positive
    }

    if (Vector3D.IsZero(desiredUpVector)) 
        roll = 0;
        return;
    }

    // Since there is a relationship between roll and the orientation of forward
    // we need to ensure that the up we are comparing is orthagonal to forward.
    Vector3D orthagonalUp;
    if (Vector3D.Dot(desiredForwardVector, desiredUpVector) == 0) {
        orthagonalUp = desiredUpVector;
    } else {
        var intermediateLeft = Vector3D.Cross(desiredUpVector, desiredForwardVector);
        orthagonalUp = Vector3D.Cross(desiredForwardVector, intermediateLeft);
    }

    var localUpVector = Vector3D.Rotate(orthagonalUp, MatrixD.Transpose(worldMatrix));
    var flattenedUpVector = new Vector3D(localUpVector.X, localUpVector.Y, 0);
    int rollSign = Math.Sign(flattenedUpVector.X);
    if (rollSign == 0) {
        rollSign = 1;
    }
    if (Vector3D.IsZero(flattenedUpVector)) {
        var sign = Math.Sign(localTargetVector.X) * Math.Sign(localUpVector.Z);
        if (sign == 0) {
            var sign2 = Math.Sign(localTargetVector.Y) * Math.Sign(localUpVector.Z);
            if (sign2 < 0) {
                roll = Math.PI;
            } else {
                roll = 0;
            }
        } else {
            roll = MathHelper.PiOver2 * sign;
        }
    } else {
        roll = VectorMath.AngleBetween(flattenedUpVector, Vector3D.Up) * rollSign;
        if (localTargetVector.Z > 0) {
            roll *= -1;
        }
    }
}
