void Main()
{
    var gyros = new List<IMyGyro>();
    GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros);

    double pitchSpeed = 0; //these are broke
    double yawSpeed = .1;
    double rollSpeed = 0;

    var reference = GridTerminalSystem.GetBlockWithName("ref");

    //GetGyroOrientation(reference, gyros);
    ApplyGyroOverride(pitchSpeed, yawSpeed, rollSpeed, gyros, reference);
}

//Whip's ApplyGyroOverride Method v9 - 8/19/17
void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list, IMyTerminalBlock reference) 
{ 
    var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed); //because keen does some weird stuff with signs 
    var shipMatrix = reference.WorldMatrix;
    var relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipMatrix); 

    foreach (var thisGyro in gyro_list) 
    { 
        var gyroMatrix = thisGyro.WorldMatrix;
        var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(gyroMatrix)); 
 
        thisGyro.Pitch = (float)transformedRotationVec.X;
        thisGyro.Yaw = (float)transformedRotationVec.Y; 
        thisGyro.Roll = (float)transformedRotationVec.Z; 
        thisGyro.GyroOverride = true; 
    } 
}

/*
//Some magic conjured up by Inflex
void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list, IMyTerminalBlock reference) 
{ 
    var rotationVec = new Vector4D(-pitch_speed, yaw_speed, roll_speed, 0);
    
    var shipLocalMatrix = reference.WorldMatrix;
    var relativeRotationVec = Vector4D.Transform(rotationVec, shipLocalMatrix); 

    foreach (var thisGyro in gyro_list) 
    { 
        var localMatrix = thisGyro.WorldMatrix;
        var transformedRotationVec = Vector4D.Transform(relativeRotationVec, Matrix.Transpose(localMatrix)); 
 
        thisGyro.Pitch = (float)transformedRotationVec.X; //because keen does some weird stuff with signs 
        thisGyro.Yaw = (float)transformedRotationVec.Y; 
        thisGyro.Roll = (float)transformedRotationVec.Z; 
        thisGyro.GyroOverride = true; 
    } 
}
*/




