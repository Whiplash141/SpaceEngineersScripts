//Whip's Ackerman Steering Program v4 - 1/13/18

double turningRadius = 50;
bool enableSteeringOnRearWheels = true;

const double rad2deg = 180.0 / Math.PI;

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

List<IMyShipController> shipControllers = new List<IMyShipController>();

void Main(string arg, UpdateType updateType)
{
    if ((updateType & UpdateType.Update10) == 0)
        return;
    
    Echo($"WMI Neutral Steering System{RunningSymbol()}");
    
    GridTerminalSystem.GetBlocksOfType(shipControllers, x => x.CubeGrid == Me.CubeGrid);
    var reference = GetControlledShipController(shipControllers);
    if (reference == null)
    {
        Echo("> Error: No controlled ship controller");
        return;
    }

    var wheels = new List<IMyMotorSuspension>();
    GridTerminalSystem.GetBlocksOfType(wheels);

    AckermanSteering(reference, wheels, turningRadius);
}

//Whip's Running Symbol Method v7
//•
int runningSymbolVariant = 0;
const int increment = 1;
string strRunningSymbol = "";
string RunningSymbol()
{
    runningSymbolVariant++;

    switch(runningSymbolVariant)
    {
        case 0:
            strRunningSymbol = "";
            break;
        case 1 * increment:
            strRunningSymbol = ".";
            break;
        case 2 * increment:
            strRunningSymbol = "..";
            break;
        case 3 * increment:
            strRunningSymbol = "...";
            break;
        case 4 * increment: //resets symbol
            runningSymbolVariant = -1;
            break;  
    }

    return strRunningSymbol;
}

IMyShipController GetControlledShipController(List<IMyShipController> controllers)
{
    foreach (IMyShipController thisController in controllers)
    {
        if (thisController.IsUnderControl && thisController.CanControlShip)
            return thisController;
    }

    return null;
}

void AckermanSteering(IMyShipController reference, List<IMyMotorSuspension> wheels, double turningRadius)
{
    var COM = reference.CenterOfMass;
    var inputVec = reference.MoveIndicator;

    if (inputVec.X == 0)
        return;

    var convergancePoint = COM + reference.WorldMatrix.Left * turningRadius * Math.Sign(inputVec.X);

    foreach (var wheelBase in wheels)
    {
        var wheel = wheelBase.Top;
        var wheelPos = wheel.GetPosition();
        var wheelToConvergance = convergancePoint - wheelPos;
        var angle = VectorAngleBetween(reference.WorldMatrix.Left * Math.Sign(inputVec.X), wheelToConvergance);
        if (angle > MathHelper.PiOver2)
        {
            angle -= MathHelper.PiOver2;
        }
        wheelBase.SetValueFloat("MaxSteerAngle", (float)(angle * rad2deg));
        //wheelBase.SetValueBool("Steering", true);
        
        if (reference.WorldMatrix.Forward.Dot(wheelPos - COM) < 0 && !enableSteeringOnRearWheels)
        {
            wheelBase.SetValueBool("Steering", false);
        }
    }
}

/// <summary>
/// Computes angle between 2 vectors
/// </summary>
/// <param name="a"></param>
/// <param name="b"></param>
/// <returns>Angle between vectors in radians</returns>
double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
{
    if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
        return 0;
    else
        return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
}
