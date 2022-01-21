
#region Script
/* 
/// Whip's Drop Pod Systems v61.1.1 - 12/20/2019 /// 
_______________________________________________________________
SETUP

1.) Place this code in a programmable block on your drop pod.

2.) Add "Drop Pod" to the name of your drop pod's control seat.

(Optional Steps)
3.) Add "Drop Pod" to the name of the merge block, connector, or rotor used to attach 
    your drop pod to the drop platform.

4.) Add "Drop Pod" to the name of a text panel that will serve as the status screen

_______________________________________________________________
OPERATION

1.) Run "drop" as the argument to drop the drop pod















=================================================
    DO NOT MODIFY VARIABLES IN THE SCRIPT!

 USE THE CUSTOM DATA OF THIS PROGRAMMABLE BLOCK!
=================================================


























HEY! DONT EVEN THINK ABOUT TOUCHING BELOW THIS LINE!

*/

#region Fields
//Names and stuff
string referenceName = "Drop Pod"; //reference control seat name tag 
string mergeConnectorOrRotorName = "Drop Pod"; //Name of drop pod merge
string shipName = "WMI Drop Pod"; //name of the ship 
string statusScreenName = "Drop Pod"; //(Optional) name of status screen
string landingTimerName = "Landing"; //(Optional) name of time to trigger on dron
double maxSpeed = 104.4; //Maximum in game speed. (change this if you use speed mods)
double dropDuration = 3; //Seconds
double descentSpeed = 3; //The speed (m/s) that the drop pod will descend at once it has begun braking 
double shutdownTime = 3; //Time (in seconds) that the drop pod must remain stationary before the code shuts down
double altitudeSafetyCushion = 0; //distance (meters) to add to the standard braking distance
bool useDriftCompensation = true; //If the drop pod should compensate for lateral drift when braking
bool requireAttachmentBlock = false; //This will stop the code from executing without a merge, connector, or rotor if set to TRUE
bool attemptToLand = true; //If the code should attempt to land the drop pod or stop slightly above the ground
bool disableThrustOnLanding = false; //If the thrusters will turn off after a successful landing
                                     //This is only considered if attemptToLand is TRUE

const double updatesPerSecond = 10;
const double timeMaxCycle = 1.0 / updatesPerSecond;
const double secondsPerTick = 1.0 / 60.0;
const double burnThrustPercentage = 0.80;
const double SafetyCushionConstant = 0.5;

double timeSpentStationary = 0;
double timeCurrentCycle = 100;
double timeSinceDrop = 0;
double angleRoll = 0;
double anglePitch = 0;
double currentSpeed = 0;
double altitude = 0;
double downSpeed = 0;
double pitch_deg = 0;
double roll_deg = 0;
double brakeAltitudeThreshold = 0;
double stabilizeAltitudeThreshold = 0;
double gravityVecMagnitude = 0;
double shipHeight = 0;

bool shouldDrop = false;
bool successfulDrop = false;
bool shouldBrake = false;
bool shouldStabilize = false;
bool isSetup = false;
bool safeToActivate = false;

string brakeStatus = ">> Disabled <<";
string stableStatus = ">> Disabled <<";

Vector3D shipVelocityVec = new Vector3D(0, 0, 0);
Vector3D gravityVec = new Vector3D(0, 0, 0);

IMyShipController referenceBlock = null;

List<IMyGyro> gyros = new List<IMyGyro>();
List<IMyThrust> allThrusters = new List<IMyThrust>();
List<IMyThrust> brakingThrusters = new List<IMyThrust>();
List<IMyThrust> otherThrusters = new List<IMyThrust>();
List<IMyShipController> shipControllers = new List<IMyShipController>();
List<IMyTextPanel> statusScreens = new List<IMyTextPanel>();
List<IMyShipMergeBlock> merges = new List<IMyShipMergeBlock>();
List<IMyShipConnector> connectors = new List<IMyShipConnector>();
List<IMyTimerBlock> landingTimers = new List<IMyTimerBlock>();

double lastErr = 696969; //giggle
double kP = 5;
double kD = 2;
#endregion

Program()
{
    BuildConfig(Me);
    Runtime.UpdateFrequency = UpdateFrequency.None;
}

void Main(string arg, UpdateType updateType)
{
    if ((updateType & (UpdateType.Trigger | UpdateType.Terminal | UpdateType.Script)) != 0) //paser argument when it comes from anything other than self update
    {
        if (arg.ToLower() == "drop")
        {
            var shipController = GetClosestBlockOfType<IMyShipController>();
            if (!GetBlocks())
            {
                return;
            }
            else if (shipController.GetNaturalGravity().LengthSquared() == 0)
            {
                Echo("CRITICAL: No natural gravity field detected\n Drop Sequence Aborted");
                return;
            }
            else
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
                shouldDrop = true;
            }
        }
        else if (arg.ToLower() == "setup")
        {
            Echo("Configuration Updated");
            GetBlocks();
        }
    }

    if ((updateType & UpdateType.Update1) == 0) //dont run bulk of code until in update loop
        return;

    if (shouldDrop && !successfulDrop)
    {
        successfulDrop = ExecuteDrop();
    }

    timeCurrentCycle += secondsPerTick;

    if (timeCurrentCycle >= timeMaxCycle)
    {
        runningSymbolVariant++;
        Echo("WMI Drop Pod Systems... " + RunningSymbol());

        if (safeToActivate)
        {
            if (isSetup) //this should be false on first run
            {
                Echo("Drop Initiated");
                StabilizePod();
                StatusScreens();

                if (timeSpentStationary > shutdownTime)
                {
                    ShutownSystems();
                }
            }
            else
            {
                isSetup = GetBlocks();
                Echo("Checking setup");
            }
        }
        else
        {
            Echo("Standing by for drop");
        }

        timeCurrentCycle = 0;
    }

    if (successfulDrop && !safeToActivate)
    {
        if (timeSinceDrop > dropDuration)
        {
            safeToActivate = true;
            GetBlocks(); //refetch blocks to make sure that we are only messing with blocks on this grid
            ActivateBlocks();
            referenceBlock.DampenersOverride = false;
        }
        else
            timeSinceDrop += secondsPerTick;
    }
}

bool ExecuteDrop()
{
    var thisMerge = GetClosestBlockOfType<IMyShipMergeBlock>(mergeConnectorOrRotorName);
    var thisConnector = GetClosestBlockOfType<IMyShipConnector>(mergeConnectorOrRotorName);
    var thisRotor = GetClosestBlockOfType<IMyMotorStator>(mergeConnectorOrRotorName);

    if (thisMerge == null && thisConnector == null && thisRotor == null)
    {
        if (requireAttachmentBlock)
        {
            Echo($"CRITICAL: No merges, connectors, or rotors named '{mergeConnectorOrRotorName}' were found\nDrop Sequence Aborted");
            return false;
        }
        else
        {
            Echo($"Warning: No merges, connectors, or rotors '{mergeConnectorOrRotorName}' were found");
        }
    }

    if (thisMerge != null)
        thisMerge.Enabled = false; //detach the drop pod merge if found

    if (thisRotor != null)
        thisRotor.Detach(); //detach drop pod rotor

    if (thisConnector != null)
        thisConnector.Disconnect(); //detach drop pod connector

    return true;
}

void ShutownSystems()
{
    foreach (IMyThrust thisThrust in brakingThrusters)
    {
        thisThrust.ThrustOverridePercentage = 0f;
        if (disableThrustOnLanding && attemptToLand)
            thisThrust.Enabled = false;
        else
            thisThrust.Enabled = true;
    }

    foreach (IMyGyro thisGyro in gyros)
    {
        thisGyro.GyroOverride = false;
    }

    foreach (IMyTimerBlock thisTimer in landingTimers)
    {
        thisTimer.Trigger();
    }

    foreach (IMyThrust thisThrust in otherThrusters)
    {
        thisThrust.ThrustOverridePercentage = 0f;
        if (disableThrustOnLanding)
            thisThrust.Enabled = false;
        else
            thisThrust.Enabled = true;
    }

    referenceBlock.DampenersOverride = true;

    Echo("Landing successful\nShutting down systems...\n\nGood Luck Pilot!");
    Runtime.UpdateFrequency = UpdateFrequency.None;

    string message = $" {shipName}\n--------------------------------------------------\nLanding successful\nShutting down systems...\n\nGood Luck Pilot!";

    //---Write to screens 
    statusScreens.Clear();
    GridTerminalSystem.GetBlocksOfType(statusScreens, block => block.CustomName.ToLower().Contains(statusScreenName.ToLower()));

    foreach (IMyTextPanel thisScreen in statusScreens)
    {
        thisScreen.WriteText(message);
        thisScreen.ContentType = ContentType.TEXT_AND_IMAGE;
        thisScreen.TextPadding = 0f;
        //thisScreen.SetValue("FontSize", 1.5f);
        thisScreen.Font = "Monospace";
    }
}

//Whip's Get Closest Block of Type Method variant 2 - 5/26/17
//Added optional ignore name variable
T GetClosestBlockOfType<T>(string name = "", string ignoreName = "") where T : class, IMyTerminalBlock
{
    var allBlocks = new List<T>();

    if (name == "")
    {
        if (ignoreName == "")
            GridTerminalSystem.GetBlocksOfType(allBlocks);
        else
            GridTerminalSystem.GetBlocksOfType(allBlocks, block => !block.CustomName.ToLower().Contains(ignoreName.ToLower()));
    }
    else
    {
        if (ignoreName == "")
            GridTerminalSystem.GetBlocksOfType(allBlocks, block => block.CustomName.ToLower().Contains(name.ToLower()));
        else
            GridTerminalSystem.GetBlocksOfType(allBlocks, block => block.CustomName.ToLower().Contains(name.ToLower()) && !block.CustomName.ToLower().Contains(ignoreName.ToLower()));
    }

    if (allBlocks.Count == 0)
    {
        return null;
    }

    var closestBlock = allBlocks[0];
    var shortestDistance = Vector3D.DistanceSquared(Me.GetPosition(), closestBlock.GetPosition());
    allBlocks.Remove(closestBlock); //remove this block from the list

    foreach (T thisBlock in allBlocks)
    {
        var thisDistance = Vector3D.DistanceSquared(Me.GetPosition(), thisBlock.GetPosition());

        if (thisDistance < shortestDistance)
        {
            closestBlock = thisBlock;
            shortestDistance = thisDistance;
        }
        //otherwise move to next one
    }

    return closestBlock;
}

//Whip's Running Symbol Method v6
int runningSymbolVariant = 0;
string RunningSymbol()
{
    string strRunningSymbol = "";

    if (runningSymbolVariant == 1)
        strRunningSymbol = "|";
    else if (runningSymbolVariant == 2)
        strRunningSymbol = "/";
    else if (runningSymbolVariant == 3)
        strRunningSymbol = "--";
    else
    {
        strRunningSymbol = "\\";
        runningSymbolVariant = 0;
    }

    return strRunningSymbol;
}

double GetBrakingAltitudeThreshold()
{
    double forceSum = 0;

    foreach (IMyThrust thisThrust in brakingThrusters)
    {
        forceSum += thisThrust.MaxEffectiveThrust;
    }

    if (forceSum == 0)
    {
        return 1000d; //some arbitrary number that will stop NaN cases
    }

    //Echo($"Force: {forceSum.ToString()}");
    //Echo($"Speed: {downSpeed.ToString()}");

    double mass = referenceBlock.CalculateShipMass().PhysicalMass;

    //Echo($"Mass: {mass.ToString()}");
    double deceleration = (forceSum / mass - gravityVecMagnitude) * burnThrustPercentage; //Echo($"Decel: {deceleration.ToString()}");

    double safetyCushion = maxSpeed * timeMaxCycle * SafetyCushionConstant; //cushion to account for discrete time errors

    //derived from: vf^2 = vi^2 + 2*a*d
    double distanceToStop = shipVelocityVec.LengthSquared() / (2 * deceleration) + safetyCushion + altitudeSafetyCushion; //added for safety :)

    return distanceToStop;
}

void ActivateBlocks()
{
    foreach (IMyGyro thisGyro in gyros)
    {
        thisGyro.Enabled = true;
    }

    foreach (IMyThrust thisThruster in otherThrusters)
    {
        thisThruster.Enabled = true;
    }
}

bool GetBlocks()
{
    UpdateConfig(Me);

    bool setupFailed = false;
    gyros.Clear();
    allThrusters.Clear();
    brakingThrusters.Clear();
    otherThrusters.Clear();
    shipControllers.Clear();
    statusScreens.Clear();
    merges.Clear();
    landingTimers.Clear();

    GridTerminalSystem.GetBlocksOfType(shipControllers, block => block.CustomName.ToLower().Contains(referenceName.ToLower()));
    GridTerminalSystem.GetBlocksOfType(gyros);
    GridTerminalSystem.GetBlocksOfType(allThrusters);
    GridTerminalSystem.GetBlocksOfType(landingTimers, block => block.CustomName.ToLower().Contains(landingTimerName.ToLower()));

    if (shipControllers.Count == 0)
    {
        setupFailed = true;
        Echo($"CRITICAL: No ship controllers with the name '{referenceName}' were found");
    }
    else
    {
        //---Assign our remote control
        referenceBlock = shipControllers[0] as IMyShipController;
        GetThrusterOrientation(referenceBlock);
    }

    if (brakingThrusters.Count == 0)
    {
        setupFailed = true;
        Echo("CRITICAL: No downwards thrusters were found");
    }

    if (gyros.Count == 0)
    {
        setupFailed = true;
        Echo($"CRITICAL: No gyroscopes were found");
    }

    if (landingTimers.Count == 0)
    {
        Echo($"Optional: No landing timers found");
    }

    if (setupFailed)
    {
        Echo("Setup Failed!");
        return false;
    }
    else
    {
        Echo("Setup Successful!");
        var edgeDirection = GetShipEdgeVector(referenceBlock, referenceBlock.WorldMatrix.Down);
        var edgePos = referenceBlock.GetPosition() + edgeDirection;
        shipHeight = Vector3D.Distance(referenceBlock.CenterOfMass, edgePos);
        return true;
    }
}

void BrakingOn()
{
    foreach (IMyThrust thisThrust in brakingThrusters)
    {
        thisThrust.Enabled = true;
        thisThrust.ThrustOverridePercentage = 1f;
    }

    foreach (IMyThrust thisThrust in otherThrusters)
    {
        thisThrust.ThrustOverridePercentage = 0.00001f;
    }
}

void BrakingOff()
{
    foreach (IMyThrust thisThrust in brakingThrusters)
    {
        thisThrust.ThrustOverridePercentage = 0.00001f;
    }

    foreach (IMyThrust thisThrust in otherThrusters)
    {
        thisThrust.ThrustOverridePercentage = 0f;
    }
}

void BrakingThrust()
{
    double forceSum = 0;

    foreach (IMyThrust thisThrust in brakingThrusters)
    {
        forceSum += thisThrust.MaxEffectiveThrust;
    }

    //Calculate equillibrium thrust ratio
    var mass = referenceBlock.CalculateShipMass().PhysicalMass;
    var equillibriumThrustPercentage = mass * gravityVecMagnitude / forceSum * 100;

    //PD controller
    var err = downSpeed - descentSpeed;
    double errDerivative = (err - lastErr) / timeMaxCycle;
    if (lastErr == 696969)
        errDerivative = 0;

    //This is the thing we will add to correct our speed
    var deltaThrustPercentage = kP * err + kD * errDerivative;
    lastErr = err;

    foreach (IMyThrust thisThrust in brakingThrusters)
    {
        thisThrust.ThrustOverridePercentage = (float)(equillibriumThrustPercentage + deltaThrustPercentage) / 100f;
        thisThrust.Enabled = true;
    }

    foreach (IMyThrust thisThrust in otherThrusters)
    {
        thisThrust.Enabled = false;
    }
}

void StabilizePod()
{
    //---Get speed
    currentSpeed = referenceBlock.GetShipSpeed();

    //---Dir'n vectors of the reference block 
    var referenceMatrix = referenceBlock.WorldMatrix;
    var referenceForward = referenceMatrix.Forward;
    var referenceLeft = referenceMatrix.Left;
    var referenceUp = referenceMatrix.Up;
    var referenceOrigin = referenceMatrix.Translation;

    //---Get gravity vector    
    gravityVec = referenceBlock.GetNaturalGravity();
    gravityVecMagnitude = gravityVec.Length();
    if (gravityVec.LengthSquared() == 0)
    {
        foreach (IMyGyro thisGyro in gyros)
        {
            thisGyro.GyroOverride = false;
        }
        shouldStabilize = false;

        angleRoll = 0; angleRoll = 0;
        downSpeed = 0;
        return;
    }

    shipVelocityVec = referenceBlock.GetShipVelocities().LinearVelocity;
    if (shipVelocityVec.LengthSquared() > maxSpeed * maxSpeed)
        maxSpeed = shipVelocityVec.Length();
    
    downSpeed = VectorProjection(shipVelocityVec, gravityVec).Length() * Math.Sign(shipVelocityVec.Dot(gravityVec));

    //---Determine if we should manually override brake controls
    altitude = 0;
    referenceBlock.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude);
    altitude -= shipHeight; //adjusts for height of the ship

    brakeAltitudeThreshold = GetBrakingAltitudeThreshold();
    stabilizeAltitudeThreshold = brakeAltitudeThreshold + 10 * currentSpeed; //this gives us a good safety cushion for stabilization procedures

    //Echo($"Braking distance: {Math.Round(brakeAltitudeThreshold).ToString()}");

    if (altitude < 100 && currentSpeed < 1)
    {
        timeSpentStationary += timeCurrentCycle;
    }
    else
    {
        timeSpentStationary = 0;

        if (altitude <= stabilizeAltitudeThreshold)
        {
            shouldStabilize = true;
        }
        else
        {
            shouldStabilize = false;
        }

        if (altitude <= brakeAltitudeThreshold)
        {
            shouldBrake = true;

            //kills dampeners to stop their interference with landing procedures
            referenceBlock.DampenersOverride = false;
        }
        else
        {
            shouldBrake = false;
        }
    }

    if (shouldBrake)
    {
        if (downSpeed > descentSpeed)
            BrakingOn();
        else
        {
            if (attemptToLand)
                BrakingThrust();
            else
                ShutownSystems();
        }
    }
    else
    {
        BrakingOff();
    }

    Vector3D alignmentVec = new Vector3D(0, 0, 0);
    //--Check if drift compensation is on
    if (useDriftCompensation && downSpeed > descentSpeed)
    {
        alignmentVec = shipVelocityVec;
    }
    else
    {
        alignmentVec = gravityVec;
    }

    //---Get Roll and Pitch Angles 
    anglePitch = Math.Acos(MathHelper.Clamp(alignmentVec.Dot(referenceForward) / alignmentVec.Length(), -1, 1)) - Math.PI / 2;

    Vector3D planetRelativeLeftVec = referenceForward.Cross(alignmentVec);                                                                                                                   //w.H.i.p.L.A.s.h.1.4.1
    angleRoll = Math.Acos(MathHelper.Clamp(referenceLeft.Dot(planetRelativeLeftVec) / planetRelativeLeftVec.Length(), -1, 1));
    angleRoll *= Math.Sign(VectorProjection(referenceLeft, alignmentVec).Dot(alignmentVec)); //ccw is positive 

    anglePitch *= -1; angleRoll *= -1;

    roll_deg = Math.Round(angleRoll / Math.PI * 180);
    pitch_deg = Math.Round(anglePitch / Math.PI * 180);

    //---Angle controller    
    double rollSpeed = Math.Round(angleRoll, 2);
    double pitchSpeed = Math.Round(anglePitch, 2);

    //---Enforce rotation speed limit
    if (Math.Abs(rollSpeed) + Math.Abs(pitchSpeed) > 2 * Math.PI)
    {
        double scale = 2 * Math.PI / (Math.Abs(rollSpeed) + Math.Abs(pitchSpeed));
        rollSpeed *= scale;
        pitchSpeed *= scale;
    }

    if (shouldStabilize)
    {
        ApplyGyroOverride(pitchSpeed, 0, -rollSpeed, gyros, referenceBlock);
    }
    else
    {
        foreach (IMyGyro thisGyro in gyros)
        {
            thisGyro.GyroOverride = false;
        }
    }
}

void StatusScreens()
{
    //---Left Screen Output
    if (shouldStabilize)
        stableStatus = "<< Active >>";
    else
        stableStatus = ">> Disabled <<";

    if (shouldBrake)
        brakeStatus = "<< Active >>";
    else
        brakeStatus = ">> Disabled <<";

    //---Right Screen Output 
    string rightScreenMessage = $" {shipName}\n--------------------------------------------------\n"
        + $" Speed: {Math.Round(currentSpeed, 2)} m/s"
        + $"\n\n Altidude: {Math.Round(altitude)} m"
        + $"\n\n Braking Altitude: {Math.Round(brakeAltitudeThreshold)} m"
        + string.Format("\n\n Pitch: {0:000}° | Roll: {1:000}°", -pitch_deg, roll_deg)
        + $"\n\n Stabilizer: {stableStatus} \n\n Brakes: {brakeStatus}";

    //---Write to screens 
    statusScreens.Clear();
    GridTerminalSystem.GetBlocksOfType(statusScreens, block => block.CustomName.ToLower().Contains(statusScreenName.ToLower()));

    foreach (IMyTextPanel thisScreen in statusScreens)
    {
        thisScreen.WriteText(rightScreenMessage);
        thisScreen.ContentType = ContentType.TEXT_AND_IMAGE;
        thisScreen.TextPadding = 0f;
        //thisScreen.SetValue("FontSize", 1.5f);
        thisScreen.SetValue<long>("Font", 1147350002);
    }
}

Vector3D VectorProjection(Vector3D a, Vector3D b) //proj a on b    
{
    Vector3D projection = a.Dot(b) / b.LengthSquared() * b;
    return projection;
}

Vector3D VectorRejection(Vector3D a, Vector3D b) //proj a on b    
{
    return a - VectorProjection(a, b);
}

#region NEW GYRO Orientation
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
#endregion

Vector3D GetShipEdgeVector(IMyTerminalBlock reference, Vector3D direction)
{
    //get grid relative max and min
    var gridMinimum = reference.CubeGrid.Min;
    var gridMaximum = reference.CubeGrid.Max;

    //get dimension of grid cubes
    var gridSize = reference.CubeGrid.GridSize;

    //get worldmatrix for the grid
    var gridMatrix = reference.CubeGrid.WorldMatrix;

    //convert grid coordinates to world coords
    var worldMinimum = Vector3D.Transform(gridMinimum * gridSize, gridMatrix);
    var worldMaximum = Vector3D.Transform(gridMaximum * gridSize, gridMatrix);

    //get reference position
    var origin = reference.GetPosition();

    //compute max and min relative vectors
    var minRelative = worldMinimum - origin;
    var maxRelative = worldMaximum - origin;

    //project relative vectors on desired direction
    var minProjected = Vector3D.Dot(minRelative, direction) / direction.LengthSquared() * direction;
    var maxProjected = Vector3D.Dot(maxRelative, direction) / direction.LengthSquared() * direction;

    //check direction of the projections to determine which is correct
    if (Vector3D.Dot(minProjected, direction) > 0)
        return minProjected;
    else
        return maxProjected;
}

void GetThrusterOrientation(IMyTerminalBlock refBlock)
{
    brakingThrusters.Clear();
    var downDirn = refBlock.WorldMatrix.Down;

    foreach (IMyThrust thisThrust in allThrusters)
    {
        var thrustDirn = thisThrust.WorldMatrix.Forward;
        bool sameDirn = thrustDirn == downDirn;

        if (sameDirn)
        {
            brakingThrusters.Add(thisThrust);
        }
        else
        {
            otherThrusters.Add(thisThrust);
        }
    }
}

#region VARIABLE CONFIG
Dictionary<string, string> configDict = new Dictionary<string, string>();

void BuildConfig(IMyTerminalBlock block)
{
    configDict.Clear();
    configDict.Add("referenceName", referenceName.ToString());
    configDict.Add("shipName", shipName.ToString());
    configDict.Add("statusScreenName", statusScreenName.ToString());
    configDict.Add("landingTimerName", landingTimerName.ToString());
    configDict.Add("maxSpeed", maxSpeed.ToString());
    configDict.Add("useDriftCompensation", useDriftCompensation.ToString());
    configDict.Add("requireAttachmentBlock", requireAttachmentBlock.ToString());
    configDict.Add("mergeConnectorOrRotorName", mergeConnectorOrRotorName.ToString());
    configDict.Add("attemptToLand", attemptToLand.ToString());
    configDict.Add("dropDuration", dropDuration.ToString());
    configDict.Add("descentSpeed", descentSpeed.ToString());
    configDict.Add("disableThrustOnLanding", disableThrustOnLanding.ToString());
    configDict.Add("shutdownTime", shutdownTime.ToString());
    configDict.Add("altitudeSafetyCushion", altitudeSafetyCushion.ToString());

    UpdateConfig(block);
}

void UpdateConfig(IMyTerminalBlock block)
{
    string customData = block.CustomData;
    var lines = customData.Split('\n');

    foreach (var thisLine in lines)
    {
        var words = thisLine.Split('=');
        if (words.Length == 2)
        {
            var variableName = words[0].Trim();
            var variableValue = words[1].Trim();
            string dictValue;
            if (configDict.TryGetValue(variableName, out dictValue))
            {
                configDict[variableName] = variableValue;
            }
        }
    }

    GetVariableFromConfig("referenceName", ref referenceName);
    GetVariableFromConfig("shipName", ref shipName);
    GetVariableFromConfig("statusScreenName", ref statusScreenName);
    GetVariableFromConfig("landingTimerName", ref landingTimerName);
    GetVariableFromConfig("maxSpeed", ref maxSpeed);
    GetVariableFromConfig("useDriftCompensation", ref useDriftCompensation);
    GetVariableFromConfig("requireAttachmentBlock", ref requireAttachmentBlock);
    GetVariableFromConfig("mergeConnectorOrRotorName", ref mergeConnectorOrRotorName);
    GetVariableFromConfig("attemptToLand", ref attemptToLand);
    GetVariableFromConfig("dropDuration", ref dropDuration);
    GetVariableFromConfig("descentSpeed", ref descentSpeed);
    GetVariableFromConfig("disableThrustOnLanding", ref disableThrustOnLanding);
    GetVariableFromConfig("shutdownTime", ref shutdownTime);
    GetVariableFromConfig("altitudeSafetyCushion", ref altitudeSafetyCushion);

    WriteConfig(block);
}

StringBuilder configSB = new StringBuilder();
void WriteConfig(IMyTerminalBlock block)
{
    configSB.Clear();
    foreach (var keyValue in configDict)
    {
        configSB.AppendLine($"{keyValue.Key} = {keyValue.Value}");
    }

    block.CustomData = configSB.ToString();
}

void GetVariableFromConfig(string name, ref bool variableToUpdate)
{
    string valueStr;
    if (configDict.TryGetValue(name, out valueStr))
    {
        bool thisValue;
        if (bool.TryParse(valueStr, out thisValue))
        {
            variableToUpdate = thisValue;
        }
    }
}

void GetVariableFromConfig(string name, ref int variableToUpdate)
{
    string valueStr;
    if (configDict.TryGetValue(name, out valueStr))
    {
        int thisValue;
        if (int.TryParse(valueStr, out thisValue))
        {
            variableToUpdate = thisValue;
        }
    }
}

void GetVariableFromConfig(string name, ref float variableToUpdate)
{
    string valueStr;
    if (configDict.TryGetValue(name, out valueStr))
    {
        float thisValue;
        if (float.TryParse(valueStr, out thisValue))
        {
            variableToUpdate = thisValue;
        }
    }
}

void GetVariableFromConfig(string name, ref double variableToUpdate)
{
    string valueStr;
    if (configDict.TryGetValue(name, out valueStr))
    {
        double thisValue;
        if (double.TryParse(valueStr, out thisValue))
        {
            variableToUpdate = thisValue;
        }
    }
}

void GetVariableFromConfig(string name, ref string variableToUpdate)
{
    string valueStr;
    if (configDict.TryGetValue(name, out valueStr))
    {
        variableToUpdate = valueStr;
    }
}
#endregion

#endregion
