/*
/ //// / Whip's Subgrid Wheel Control Script / //// /

Instructions:
1.) Place this script on your ship

2.) Place some wheels on subgrids

3.) Place a cockpit

4.) Enjoy :)

All subgrid wheels to the left of the seat you are controlling will be treated as left wheels. Same thing goes for the right side.
*/

float brakingConstant = 0.1f; //Increase this if your brakes are not strong enough!

/*
//=============================================
// THERE IS NO REASON TO TOUCH ANYTHING BELOW
// DOING SO WILL VOID YOUR WARRANTY
//=============================================
*/
const string VERSION = "10.1";
const string DATE = "01/20/19";

bool isSetup = false;
const double runtimeToRealTime = 1.0 / 0.96;
const double updatesPerSecond = 10;
const double updateTime = 1.0 / updatesPerSecond;
const double refreshInterval = 10;
double timeSinceRefresh = 141;
double currentTime = 141;
IMyShipController lastControlledSeat = null;
string lastException = "";

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

void Main(string arg, UpdateType updateSource)
{
    var lastRuntime = runtimeToRealTime * Math.Max(Runtime.TimeSinceLastRun.TotalSeconds, 0);
    currentTime += lastRuntime;
    timeSinceRefresh += lastRuntime;

    if (currentTime >= updateTime)
    {
        Echo($"Whip's Subgrid Wheel Control\nScript{RunningSymbol()}\n(Version {VERSION} - {DATE})\n");
        if (isSetup)
        {
            try
            {
                ControlSubgridWheels();
            }
            catch (Exception e)
            {
                isSetup = false;
                Echo("ERROR: Exception");
                lastException = e.StackTrace;
            }
        }
        currentTime = 0;

        Echo($"\nNext refresh in {Math.Ceiling(Math.Max(refreshInterval - timeSinceRefresh, 0))} seconds\n");
        Echo($"Last setup results:\n{setupBuilder}{(isSetup ? "> Setup Successful!" : "> Setup Failed!")}");
        
        if (!string.IsNullOrEmpty(lastException))
        {
            Echo($"\nLast exception:\n{lastException}");
        }

    }

    if (timeSinceRefresh >= refreshInterval || !isSetup)
    {
        isSetup = GetBlocks();
        timeSinceRefresh = 0;
    }
}

void ControlSubgridWheels()
{
    var referenceController = GetControlledShipController(shipControllers);
    if (referenceController == null)
    {
        Echo("> No driver detected");
        
        if (lastControlledSeat != null)
            referenceController = lastControlledSeat;
        else
            referenceController = shipControllers[0];
    }
    else
    {
        Echo("> Wheels are being controlled");
        lastControlledSeat = referenceController;
    }
    
    var avgWheelPosition = GetSubgridWheels(referenceController);

    if (subgridWheels.Count == 0)
    {
        Echo("> No subgrid wheels found\n> Pausing execution...");
        return;
    }

    var wasdInput = referenceController.MoveIndicator;
    bool brakes = wasdInput.Y > 0 || referenceController.HandBrake;
    var velocity = Vector3D.TransformNormal(referenceController.GetShipVelocities().LinearVelocity, MatrixD.Transpose(referenceController.WorldMatrix)) * brakingConstant;
    
    Echo($"> Brakes toggled: {brakes}");
    Echo($"> Reference controller: {referenceController.CustomName}");
    
    foreach (var wheel in subgridWheels)
    {
        var steerMult = Math.Sign(Math.Round(Vector3D.Dot(wheel.WorldMatrix.Forward, referenceController.WorldMatrix.Up), 2)) * Math.Sign(Vector3D.Dot(wheel.GetPosition() - avgWheelPosition, referenceController.WorldMatrix.Forward));
        var propulsionMult = -Math.Sign(Math.Round(Vector3D.Dot(wheel.WorldMatrix.Up, referenceController.WorldMatrix.Right), 2));
        var steerValue = steerMult * wasdInput.X;
        var power = wheel.Power * 0.01f;
        if (brakes)
        {
            var propulsionValue = propulsionMult * (float)velocity.Z;
            wheel.SetValue("Propulsion override", propulsionValue);
            wheel.SetValue("Steer override", steerValue);
        }
        else
        {
            //steer + is right if up == up
            //steer + is left if up == down
            var propulsionValue = propulsionMult * power * -wasdInput.Z;
            wheel.SetValue("Propulsion override", propulsionValue);
            wheel.SetValue("Steer override", steerValue);
        }
    }
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

Vector3D GetSubgridWheels(IMyTerminalBlock reference)
{
    var summedWheelPosition = Vector3D.Zero;
    subgridWheels.Clear();
    foreach (var block in wheels)
    {
        summedWheelPosition += block.GetPosition();

        if (reference.CubeGrid != block.CubeGrid)
            subgridWheels.Add(block);
    }

    return summedWheelPosition / wheels.Count;
}

List<IMyMotorSuspension> subgridWheels = new List<IMyMotorSuspension>();
List<IMyMotorSuspension> wheels = new List<IMyMotorSuspension>();
List<IMyShipController> shipControllers = new List<IMyShipController>();
List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();
StringBuilder setupBuilder = new StringBuilder();
bool GetBlocks()
{
    setupBuilder.Clear();

    GridTerminalSystem.GetBlocksOfType(allBlocks, x => Me.IsSameConstructAs(x));

    wheels.Clear();
    shipControllers.Clear();
    foreach (var block in allBlocks)
    {
        if (block is IMyMotorSuspension)
            wheels.Add(block as IMyMotorSuspension);
        else if (block is IMyShipController)
            shipControllers.Add(block as IMyShipController);
    }

    if (shipControllers.Count == 0)
    {
        setupBuilder.AppendLine($">> Error: No ship controllers");
        return false;
    }

    if (wheels.Count == 0)
    {
        setupBuilder.AppendLine(">> Error: No wheels");
        return false;
    }

    return true;
}

//Whip's Running Symbol Method v8
//•
int runningSymbolVariant = 0;
int runningSymbolCount = 0;
const int increment = 1;
string[] runningSymbols = new string[] { ".", "..", "...", "....", "...", "..", ".", "" };

string RunningSymbol()
{
    if (runningSymbolCount >= increment)
    {
        runningSymbolCount = 0;
        runningSymbolVariant++;
        if (runningSymbolVariant >= runningSymbols.Length)
            runningSymbolVariant = 0;
    }
    runningSymbolCount++;
    return runningSymbols[runningSymbolVariant];
}
