//Whip's Tank Skid Steering System v9 - 1/8/18

const string controlSeatName = "Driver";

const float driveFriction = 50f;
const float turnFriction = 10f;
const float rotationSpeed = .5f;
bool invertSteerWhenReversing = true;

//------------------------------------------------------
// ============== NO TOUCHEY BELOW HERE ================
//------------------------------------------------------

const double updatesPerSecond = 10;
const double updateTime = 1 / updatesPerSecond;
double currentTime = 0;

const double refreshInterval = 10;
double timeSinceRefresh = 141;
bool isSetup = false;

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

void Main(string argument, UpdateType updateType)
{
    try
    {
        if ((updateType & UpdateType.Update1) == 0)
            return;
        //implied else
        
        currentTime += 1.0/60.0;
        timeSinceRefresh += 1.0/60.0;

        if (!isSetup || timeSinceRefresh >= refreshInterval)
        {
            isSetup = GrabBlocks();
            timeSinceRefresh = 0;
        }
        
        if (!isSetup)
            return;
        
        if (currentTime < updateTime)
            return;
        else
            currentTime = 0;
        
        Echo("WMI Skid Steering System Online..." + RunningSymbol());
        Echo($"\nTime until next block refresh:\n{Math.Round(Math.Max(0, refreshInterval - timeSinceRefresh))} seconds\n");

        var inputVec = controller.MoveIndicator;
   
        if (inputVec.Z <= 0 || !invertSteerWhenReversing) //W 
        {
            if (inputVec.X < 0) //A
            {
                TurnRight(leftWheels, rightWheels, gyros, inputVec);
            }
            else if (inputVec.X > 0) //D
            {
                TurnLeft(leftWheels, rightWheels, gyros, inputVec);
            }
            else
            {
                NoTurn(leftWheels, rightWheels, gyros);
            }
        }
        else
        {
            if (inputVec.X < 0) //A
            {
                TurnLeft(leftWheels, rightWheels, gyros, inputVec);
            }
            else if (inputVec.X > 0) //D
            {
                TurnRight(leftWheels, rightWheels, gyros, inputVec);
            }
            else
            {
                NoTurn(leftWheels, rightWheels, gyros);
            }
        }
    }
    catch
    {
        isSetup = false;
    }
}

List<IMyMotorSuspension> wheels = new List<IMyMotorSuspension>();
List<IMyShipController> controllers = new List<IMyShipController>();
List<IMyGyro> gyros = new List<IMyGyro>();
List<IMyMotorSuspension> leftWheels = new List<IMyMotorSuspension>();
List<IMyMotorSuspension> rightWheels = new List<IMyMotorSuspension>();
IMyShipController controller = null;

bool GrabBlocks()
{
    GridTerminalSystem.GetBlocksOfType(controllers, block => block.CustomName.Contains(controlSeatName));
    if (controllers.Count == 0)
    {
        Echo($"Error: No ship controller named '{controlSeatName}' found");
        return false;
    }
    controller = GetControlledShipController(controllers);
    
    GridTerminalSystem.GetBlocksOfType(wheels, block => block.CubeGrid == controller.CubeGrid);
    if (wheels.Count == 0)
    {
        Echo("Error: No wheels found on same grid as controller");
        return false;
    }
    
    GridTerminalSystem.GetBlocksOfType(gyros, block => block.CubeGrid == Me.CubeGrid);
    if (gyros.Count == 0)
    {
        Echo("Optional: No gyros found on same grid as controller");
    }
    else
    {
        GetGyroOrientation(controller, gyros);
    }
    
    leftWheels = new List<IMyMotorSuspension>();
    rightWheels = new List<IMyMotorSuspension>();
    GetWheelSide(controller, wheels, out leftWheels, out rightWheels);
    
    return true;
}

void NoTurn(List<IMyMotorSuspension> leftWheels, List<IMyMotorSuspension> rightWheels, List<IMyGyro> gyros)
{
    InvertWheelPropulsion(leftWheels, false);
    InvertWheelPropulsion(rightWheels, false);
    InvertSteering(leftWheels, false);
    InvertSteering(rightWheels, false);
    SetFriction(leftWheels, driveFriction);
    SetFriction(rightWheels, driveFriction);
    ApplyGyroOverride(0, 0, 0, gyros);
    SetGyroPower(gyros, .1f);
}

void TurnRight(List<IMyMotorSuspension> leftWheels, List<IMyMotorSuspension> rightWheels,  List<IMyGyro> gyros, Vector3D inputVec)
{
    InvertWheelPropulsion(leftWheels, true);
    InvertWheelPropulsion(rightWheels, false);
    InvertSteering(leftWheels, true);
    InvertSteering(rightWheels, false);
    SetFriction(leftWheels, turnFriction);
    SetFriction(rightWheels, turnFriction);
    SetGyroPower(gyros, 1f);
    if (inputVec.Z <= 0)
        ApplyGyroOverride(0, -rotationSpeed, 0, gyros);
    else
        ApplyGyroOverride(0, rotationSpeed, 0, gyros);
}

void TurnLeft(List<IMyMotorSuspension> leftWheels, List<IMyMotorSuspension> rightWheels,  List<IMyGyro> gyros, Vector3D inputVec)
{
    InvertWheelPropulsion(leftWheels, false);
    InvertWheelPropulsion(rightWheels, true);
    InvertSteering(leftWheels, false);
    InvertSteering(rightWheels, true);
    SetFriction(leftWheels, turnFriction);
    SetFriction(rightWheels, turnFriction);
    SetGyroPower(gyros, 1f);
    if (inputVec.Z <= 0)
        ApplyGyroOverride(0, rotationSpeed, 0, gyros);
    else
        ApplyGyroOverride(0, -rotationSpeed, 0, gyros);
}

Vector3D VectorProjection( Vector3D a, Vector3D b )
{
    return a.Dot( b ) / b.LengthSquared() * b;  
}

void InvertWheelPropulsion(List<IMyMotorSuspension> wheels, bool invert)
{
    foreach (var wheel in wheels)
        wheel.SetValue("InvertPropulsion", invert);
}

void InvertSteering(List<IMyMotorSuspension> wheels, bool invert)
{
    foreach (var wheel in wheels)
        wheel.SetValue("InvertSteering", invert);
}

void SetFriction(List<IMyMotorSuspension> wheels, float friction)
{
    foreach (var wheel in wheels)
        wheel.SetValue("Friction", friction);
}

void SetGyroPower(List<IMyGyro> gyros, float power)
{
    foreach (var gyro in gyros)
        gyro.GyroPower = power;
}

void GetWheelSide(IMyTerminalBlock reference, List<IMyMotorSuspension> wheels, out List<IMyMotorSuspension> leftWheels, out List<IMyMotorSuspension> rightWheels)
{
    leftWheels = new List<IMyMotorSuspension>();
    rightWheels = new List<IMyMotorSuspension>();

    foreach (IMyMotorSuspension thisWheel in wheels)
    {
        if (reference.WorldMatrix.Left.Dot(thisWheel.WorldMatrix.Up) > 0.9) //left wheel
            leftWheels.Add(thisWheel);
        else if (-(reference.WorldMatrix.Left.Dot(thisWheel.WorldMatrix.Up)) > 0.9)
            rightWheels.Add(thisWheel);
    }
}

/*void GetWheelSide(IMyTerminalBlock reference, List<IMyMotorSuspension> wheels, out List<IMyMotorSuspension> leftWheels, out List<IMyMotorSuspension> rightWheels)
{
    leftWheels = new List<IMyMotorSuspension>();
    rightWheels = new List<IMyMotorSuspension>();

    foreach (IMyMotorSuspension thisWheel in wheels)
    {
        if (reference.WorldMatrix.Left.Dot(thisWheel.GetPosition() - reference.GetPosition()) > 0) //left wheel
            leftWheels.Add(thisWheel);
        else
            rightWheels.Add(thisWheel);
    }
}*/

IMyShipController GetControlledShipController(List<IMyShipController> SCs)
{
    foreach (IMyShipController thisController in SCs)
    {
        if (thisController.IsUnderControl && thisController.CanControlShip)
            return thisController;
    }

    return SCs[0];
}

//Whip's Gyro Orientation Method

    string[] gyroRelativeYaw;
    string[] gyroRelativePitch;
    string[] gyroRelativeRoll;
    int[] gyroYawSign;
    int[] gyroPitchSign;
    int[] gyroRollSign;

void GetGyroOrientation(IMyTerminalBlock reference_block, List<IMyGyro> gyro_list)
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

void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list)
{
    for (int i = 0; i < gyro_list.Count; i++)
    {
        var thisGyro = gyro_list[i] as IMyGyro;
        if (thisGyro != null)
        {
            thisGyro.SetValue<float>(gyroRelativeYaw[i], (float)yaw_speed * gyroYawSign[i]);
            thisGyro.SetValue<float>(gyroRelativePitch[i], (float)pitch_speed * gyroPitchSign[i]);
            thisGyro.SetValue<float>(gyroRelativeRoll[i], (float)roll_speed * gyroRollSign[i]);
            thisGyro.SetValue( "Override", true ); 
        }
    }
}

//Whip's Running Symbol Method v6
int runningSymbolVariant = 0;
string RunningSymbol()
{
    runningSymbolVariant++;
    string strRunningSymbol = "";

    if (runningSymbolVariant == 0)
        strRunningSymbol = "|";
    else if (runningSymbolVariant == 1)
        strRunningSymbol = "/";
    else if (runningSymbolVariant == 2)
        strRunningSymbol = "--";
    else if (runningSymbolVariant == 3)
    {
        strRunningSymbol = "\\";
        runningSymbolVariant = -1;
    }

    return strRunningSymbol;
}