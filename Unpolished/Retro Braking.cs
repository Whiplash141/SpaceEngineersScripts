/// Whip's Retro Thruster Braking Script v2 - 2/28/17 ///

    const string referenceName = "Forward";
    const double updatesPerSecond = 10;

    const double proportionalConstant = 10;
    const double derivativeConstant = 4;

    const double brakingAngleTolerance = 10;

//---------------------------------------------
//No touch below here!
//---------------------------------------------

    const double timeMaxCycle = 1 / updatesPerSecond;
    double timeCurrentCycle = 0;
    const double rad2deg = 180 / Math.PI;
    bool shouldBrake = false;
    IMyShipController reference = null;

void Main(string arg)
{
    // Argument Handling
    switch (arg.ToLower())
    {
        case "on":
            shouldBrake = true;
            break;

        case "off":
            shouldBrake = false;
            break;

        case "toggle":
            if (shouldBrake)
                shouldBrake = false;
            else
                shouldBrake = true;
            break;

        default:
            break;
    }

    // Braking Mode Handling
    timeCurrentCycle += Runtime.TimeSinceLastRun.TotalSeconds;

    if (timeCurrentCycle >= timeMaxCycle)
    {
        Echo("WMI Retro Braking System Online... " + RunningSymbol());
        runningSymbolVariant++;
        
        var referenceList = new List<IMyShipController>();
        GridTerminalSystem.GetBlocksOfType(referenceList, block => block.CustomName.ToLower().Contains(referenceName.ToLower()));
        
        Echo($"Braking Status: {shouldBrake.ToString()}");
        
        if (referenceList.Count == 0) 
        { 
            Echo($"No ship controller with name tag '{referenceName}' was found");
            return; 
        }

        reference = referenceList[0];
        GetThrusters(reference);
        
        if (shouldBrake)
        {
            StartBraking(reference);
        }
        else
        {
            StopBraking();
        }

        timeCurrentCycle = 0; //reset time count
    }
}


//Whip's Running Symbol Method v6
int runningSymbolVariant = 0;
string RunningSymbol()
{
    string strRunningSymbol = "";
    
    if (runningSymbolVariant < 2)
        strRunningSymbol = "|";
    else if (runningSymbolVariant < 4)
        strRunningSymbol = "/";
    else if (runningSymbolVariant < 6)
        strRunningSymbol = "--";
    else if (runningSymbolVariant < 8)
        strRunningSymbol = "\\";
    else
    {
        strRunningSymbol = "|";
        runningSymbolVariant = 0;
    }

    return strRunningSymbol;
}

List<IMyThrust> mainThrust = new List<IMyThrust>();

void GetThrusters(IMyShipController reference)
{
    var allThrust = new List<IMyThrust>();
    mainThrust.Clear();

    List<IMyTerminalBlock> allThrusters = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyThrust>(allThrusters, block => block.CubeGrid == reference.CubeGrid);

    foreach (IMyThrust thrust in allThrusters)                                                                                                                                                                          ///w.h-i*p
    {
        if (thrust.WorldMatrix.Backward == reference.WorldMatrix.Forward)
        {
            mainThrust.Add(thrust);
        }
    }
}

void StartBraking(IMyShipController reference)
{
    var velocityVec = reference.GetShipVelocities().LinearVelocity; //gets current travel vector
    var speedSquared = velocityVec.LengthSquared();
    
    if (speedSquared < 1)
    {
        shouldBrake = false;
        return;
    }
    
    var forwardVec = reference.WorldMatrix.Forward; //gets backwards vector
    var leftVec = reference.WorldMatrix.Left; //gets Right vector
    var upVec = reference.WorldMatrix.Up; //gets up vector
    
    var gyros = new List<IMyTerminalBlock>();

    GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros, block => block.CubeGrid == reference.CubeGrid); //gets all gyros on same grid as reference block
    GetGyroOrientation(reference, gyros); //this gets the relative gyro rotation axes

    double yawAngle = 0, pitchAngle = 0;
    GetRotationAngles(-velocityVec, forwardVec, leftVec, upVec, out yawAngle, out pitchAngle);

    //double yawSpeed = proportionalConstant * yawAngle + Math.Abs(Math.Sign(yawAngle)) * derivativeConstant * (yawAngle - lastYawAngle) / timeCurrentCycle;
    //double pitchSpeed = proportionalConstant * pitchAngle + Math.Abs(Math.Sign(pitchAngle)) * derivativeConstant * (pitchAngle - lastPitchAngle) / timeCurrentCycle;
    
    //double yawSpeed = MathHelper.Clamp(proportionalConstant * yawAngle * timeCurrentCycle, -Math.Abs(yawAngle) * 2, Math.Abs(yawAngle) * 2);
    //double pitchSpeed = MathHelper.Clamp(proportionalConstant * pitchAngle * timeCurrentCycle, -Math.Abs(pitchAngle) * 2, Math.Abs(pitchAngle) * 2);

    double yawSpeed = yawAngle / timeCurrentCycle / 10;
    double pitchSpeed = pitchAngle / timeCurrentCycle / 10;
    
    // Scales the rotation speed to be constant regardless od number of gyros
    //yawSpeed /= gyros.Count;
    //pitchSpeed /= gyros.Count;

    ApplyGyroOverride(pitchSpeed, yawSpeed, 0, gyros);

    double brakingAngle = VectorAngleBetween(forwardVec, -velocityVec);

    if (brakingAngle * rad2deg <= brakingAngleTolerance)
    {
        if (!reference.DampenersOverride)
            reference.SetValue("DampenersOverride", true);
    }
    else
    {
        if (reference.DampenersOverride)
            reference.SetValue("DampenersOverride", false);
    }
}

void StopBraking()
{
    var gyros = new List<IMyGyro>();
    GridTerminalSystem.GetBlocksOfType(gyros); //messy fix later
    
    foreach (IMyGyro thisGyro in gyros)
    {
        thisGyro.SetValue("Override", false);
    }
}

void ApplyThrustOverride(List<IMyThrust> thrusterList, float thrustOverride = 0)
{
    foreach (IMyThrust thisThrust in thrusterList)
    {
        thisThrust.SetValueFloat("Override", thrustOverride);
    }
}

//Whip's Get Rotation Angles Method v4
void GetRotationAngles(Vector3D v_target, Vector3D v_front, Vector3D v_left, Vector3D v_up, out double yaw, out double pitch)
{
    //Dependencies: VectorProjection() | VectorAngleBetween()
    //Keen uses a stupid left hand rule coordSystem, I dont.
    var projTargetFront = VectorProjection(v_target, v_front);
    var projTargetLeft = VectorProjection(v_target, v_left);
    var projTargetUp = VectorProjection(v_target, v_up);
    var projTargetFrontLeft = projTargetFront + projTargetLeft;
    var projTargetFrontUp = projTargetFront + projTargetUp;

    yaw = VectorAngleBetween(v_front, projTargetFrontLeft);
    pitch = VectorAngleBetween(v_front, projTargetFrontUp);

    //---Check if yaw angle is left or right  
    //multiplied by -1 to convert from right hand rule to left hand rule
    yaw = -1 * Math.Sign(v_left.Dot(projTargetLeft)) * yaw;

    //---Check if pitch angle is up or down    
    pitch = Math.Sign(v_up.Dot(projTargetUp)) * pitch;

    //---Check if target vector is pointing opposite the front vector
    if (pitch == 0 && yaw == 0 && v_target.Dot(v_front) < 0)
    {
        yaw = Math.PI;
        pitch = Math.PI;
    }
}

Vector3D VectorProjection(Vector3D a, Vector3D b) //proj a on b   
{
    return a.Dot(b) / b.LengthSquared() * b;
}

double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
{
    if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
        return 0;
    else
        return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
}

//Whip's Gyro Orientation Method 
string[] gyroRelativeYaw;
string[] gyroRelativePitch;
string[] gyroRelativeRoll;
int[] gyroYawSign;
int[] gyroPitchSign;
int[] gyroRollSign;

void GetGyroOrientation(IMyTerminalBlock reference_block, List<IMyTerminalBlock> gyro_list)
{
    gyroRelativeYaw = new string[gyro_list.Count];
    gyroRelativePitch = new string[gyro_list.Count];
    gyroRelativeRoll = new string[gyro_list.Count];

    gyroYawSign = new int[gyro_list.Count];
    gyroPitchSign = new int[gyro_list.Count];
    gyroRollSign = new int[gyro_list.Count];

    var reference_up = reference_block.WorldMatrix.Up; //assuming rot right 
    var reference_right = reference_block.WorldMatrix.Right; //assuming rot up 
    var reference_forward = reference_block.WorldMatrix.Forward; //assuming rot up 

    for (int i = 0; i < gyro_list.Count; i++)
    {
        var gyro_forward = gyro_list[i].WorldMatrix.Forward;
        var gyro_backward = gyro_list[i].WorldMatrix.Backward;
        var gyro_up = gyro_list[i].WorldMatrix.Up;
        var gyro_down = gyro_list[i].WorldMatrix.Down;
        var gyro_left = gyro_list[i].WorldMatrix.Left;
        var gyro_right = gyro_list[i].WorldMatrix.Right;

        /// Pitch Fields ///    
        if (reference_right == gyro_forward)
        {
            gyroRelativePitch[i] = "Roll";
            gyroPitchSign[i] = 1;
        }
        else if (reference_right == gyro_backward)
        {
            gyroRelativePitch[i] = "Roll";
            gyroPitchSign[i] = -1;
        }
        else if (reference_right == gyro_right)
        {
            gyroRelativePitch[i] = "Pitch";
            gyroPitchSign[i] = 1;
        }
        else if (reference_right == gyro_left)
        {
            gyroRelativePitch[i] = "Pitch";
            gyroPitchSign[i] = -1;
        }
        else if (reference_right == gyro_up)
        {
            gyroRelativePitch[i] = "Yaw";
            gyroPitchSign[i] = -1;
        }
        else if (reference_right == gyro_down)
        {
            gyroRelativePitch[i] = "Yaw";
            gyroPitchSign[i] = 1;
        }

        /// Yaw Fields /// 
        if (reference_up == gyro_forward)
        {
            gyroRelativeYaw[i] = "Roll";
            gyroYawSign[i] = -1;
        }
        else if (reference_up == gyro_backward)
        {
            gyroRelativeYaw[i] = "Roll";
            gyroYawSign[i] = 1;
        }
        else if (reference_up == gyro_right)
        {
            gyroRelativeYaw[i] = "Pitch";
            gyroYawSign[i] = -1;
        }
        else if (reference_up == gyro_left)
        {
            gyroRelativeYaw[i] = "Pitch";
            gyroYawSign[i] = 1;
        }
        else if (reference_up == gyro_up)
        {
            gyroRelativeYaw[i] = "Yaw";
            gyroYawSign[i] = 1;
        }
        else if (reference_up == gyro_down)
        {
            gyroRelativeYaw[i] = "Yaw";
            gyroYawSign[i] = -1;
        }

        /// Roll Fields /// 
        if (reference_forward == gyro_forward)
        {
            gyroRelativeRoll[i] = "Roll";
            gyroRollSign[i] = 1;
        }
        else if (reference_forward == gyro_backward)
        {
            gyroRelativeRoll[i] = "Roll";
            gyroRollSign[i] = -1;
        }
        else if (reference_forward == gyro_right)
        {
            gyroRelativeRoll[i] = "Pitch";
            gyroRollSign[i] = 1;
        }
        else if (reference_forward == gyro_left)
        {
            gyroRelativeRoll[i] = "Pitch";
            gyroRollSign[i] = -1;
        }
        else if (reference_forward == gyro_up)
        {
            gyroRelativeRoll[i] = "Yaw";
            gyroRollSign[i] = -1;
        }
        else if (reference_forward == gyro_down)
        {
            gyroRelativeRoll[i] = "Yaw";
            gyroRollSign[i] = 1;
        }
    }
}

void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyTerminalBlock> gyro_list)
{
    for (int i = 0; i < gyro_list.Count; i++)
    {
        var thisGyro = gyro_list[i] as IMyGyro;
        if (thisGyro != null)
        {
            thisGyro.SetValue<float>(gyroRelativeYaw[i], (float)yaw_speed * gyroYawSign[i]);
            thisGyro.SetValue<float>(gyroRelativePitch[i], (float)pitch_speed * gyroPitchSign[i]);
            thisGyro.SetValue<float>(gyroRelativeRoll[i], (float)roll_speed * gyroRollSign[i]);
            thisGyro.SetValue("Override", true);
        }
    }
}
