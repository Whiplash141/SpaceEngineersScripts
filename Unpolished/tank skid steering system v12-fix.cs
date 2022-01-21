//Whip's Tank Skid Steering System v12-fix - 3/19/18

const float driveFriction = 50f;
const float turnFriction = 10f;
const float rotationSpeed = 1f;
bool invertSteerWhenReversing = true;
bool useGyros = true;

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
    Runtime.UpdateFrequency = UpdateFrequency.Once;
}

void Main(string argument, UpdateType updateType)
{
    //Bigass bandaid bc of keen
    if ((updateType & UpdateType.Once) != 0)
        Runtime.UpdateFrequency = UpdateFrequency.Update1;
    
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
    else
    {
        controller = GetControlledShipController(controllers);
    }

    if (!isSetup)
        return;

    if (currentTime < updateTime)
        return;
    else
        currentTime = 0;

    Echo("WMI Skid Steering \nSystem Online..." + RunningSymbol());
    Echo($"\nTime until next block refresh:\n{Math.Round(Math.Max(0, refreshInterval - timeSinceRefresh))} seconds\n");

    try
    {    
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
    GridTerminalSystem.GetBlocksOfType(controllers);
    if (controllers.Count == 0)
    {
        Echo($"Error: No ship controller named found");
        return false;
    }
    controller = GetControlledShipController(controllers);
    
    GridTerminalSystem.GetBlocksOfType(wheels, block => block.CubeGrid == controller.CubeGrid);
    if (wheels.Count == 0)
    {
        Echo("Error: No wheels found on same grid as controller");
        return false;
    }
    
    if (useGyros)
    {
        GridTerminalSystem.GetBlocksOfType(gyros, block => block.CubeGrid == Me.CubeGrid);
        if (gyros.Count == 0)
        {
            Echo("Optional: No gyros found on same grid as controller");
        }
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
    ApplyGyroOverride(0, 0, 0, gyros, controller);
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
        ApplyGyroOverride(0, -rotationSpeed, 0, gyros, controller);
    else
        ApplyGyroOverride(0, rotationSpeed, 0, gyros, controller);
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
        ApplyGyroOverride(0, rotationSpeed, 0, gyros, controller);
    else
        ApplyGyroOverride(0, -rotationSpeed, 0, gyros, controller);
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

IMyShipController GetControlledShipController(List<IMyShipController> SCs)
{
    foreach (IMyShipController thisController in SCs)
    {
        if (thisController.IsUnderControl && thisController.CanControlShip)
            return thisController;
    }

    return SCs[0];
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
