/* 
/// Whip's Speed Matcher v25 - 7/31/18 /// 
_______________________________________________________________________            
///DESCRIPTION///    
 
    This code allows you to scan a ship with Raycast and match the velocity vector  
    of a grid using your inertial dampeners. The dampening is handled by the code  
    and is completely automatic. This allows the user to fly their ship as if 
    the ship they are matching speed with is stationary. This makes landing 
    on moving carriers much easier! 
    
    The script will only search for control seats and thrusters on the SAME GRID as the program!
_______________________________________________________________________            
///SETUP///    
 
    1.) Load this script into a programmable block 
 
    2.) Add "Speed Match" into the name of any cameras that you want to use for scanning 
 
    3.) Add "Speed Match" into the name of any text panels you want to display target 
        data on. 
        
    4.) (Optional) Add "Ignore" into the name of thrusters that you don't want the script to touch.
 
_______________________________________________________________________            
///BASIC USEAGE///  
 
    1.) Point at the ship you wish to scan using your "Speed Match" camera(s). 
 
    2.) Run the program with the argument "match" to scan in front of the camera(s) and 
        speed match the target it finds. 
*/ 
 
string raycastCameraNameTag = "Speed Match"; 
string textPanelNameTag = "Speed Match"; 
string ignoreThrustNameTag = "Ignore"; 
double raycastScanRange = 5000; //in meters 
 
//===================================================== 
//         NO TOUCH BELOW HERE!!!1!!11!!! 
//===================================================== 
 
double currentTime = 141; 
double timeSinceLastScan = 0; 
 
Vector3D targetVelocityVec = new Vector3D(0, 0, 0); 
 
bool shouldScan = false; 
bool shouldMatch = false; 
bool matchSelf = false; 
bool hasMatchedSelf = false; 
bool isSetup = false; 
bool successfulScan = false; 

const double runtimeToRealTime = 1.0 / 0.96;
const double updatesPerSecond = 10; 
const double updateTime = 1 / updatesPerSecond; 
 
const double refreshInterval = 10; 
double timeSinceRefresh = 141;
 
string scanTargetName = "empty"; 
string scanTargetType = "empty"; 
string scanTargetSpeed = "empty"; 
string scanTargetRelation = "empty"; 
 
const string enabled = ">>ENABLED<<"; 
const string disabled = "<<DISABLED>>"; 

Program()
{
    GrabBlocks();
    
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    Echo("If you can read this\nclick the 'Run' button!");
}

const double secondsPerTick = 1.0 / 60.0;
void Main(string arg, UpdateType updateType)
{   
    //------------------------------------------
    //This is a bandaid
    //if ((Runtime.UpdateFrequency & UpdateFrequency.Update1) == 0)
    //    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    //------------------------------------------
    
    if ((updateType & (UpdateType.Script | UpdateType.Trigger | UpdateType.Terminal)) != 0)
    {
        var argTrim = arg.ToLower().Trim();
        switch (argTrim) 
        { 
            case "scan": 
                shouldScan = !shouldScan; 
                break; 
     
            case "match":
                shouldScan = !shouldScan;
                shouldMatch = shouldScan;
                break; 
     
            case "on": 
                shouldMatch = true; 
                break; 
     
            case "off": 
                shouldMatch = false; 
                shouldScan = false; 
                matchSelf = false; 
                break; 
     
            case "toggle": 
                shouldMatch = !shouldMatch; 
                if (!shouldMatch) 
                { 
                    shouldScan = false; 
                    matchSelf = false; 
                } 
                break; 
     
            case "self": 
                matchSelf = true; 
                hasMatchedSelf = false; 
                shouldMatch = true; 
                shouldScan = false; 
                break; 
     
            case "clear": 
                successfulScan = false; 
                matchSelf = false; 
                shouldMatch = false; 
                shouldScan = false; 
                break; 
                
            default:
                IncrementMatchedSpeed(argTrim);
                break;
        }
    }
    
    //if ((updateType & UpdateType.Update1) == 0)
    //{
    //    return;
    //}
    //implied else

    var lastRuntime = runtimeToRealTime * Math.Max(Runtime.TimeSinceLastRun.TotalSeconds, 0);
    currentTime += lastRuntime; //secondsPerTick; 
    timeSinceRefresh += lastRuntime; //secondsPerTick; 
    timeSinceLastScan += lastRuntime; //secondsPerTick; 
 
    try 
    { 
        if (!isSetup || refreshInterval <= timeSinceRefresh) 
        { 
            isSetup = GrabBlocks(); 
            timeSinceRefresh = 0; 
        } 
 
        if (!isSetup) 
            return; 
 
        if (currentTime >= updateTime) 
        { 
            if (matchSelf && !hasMatchedSelf) 
            { 
                MatchSelf(); 
                hasMatchedSelf = true; 
            } 
 
            if (shouldScan) 
                Raycaster(); 
 
            SpeedMatcher(); 
            BuildOutputText(); 
        }
        Echo("WMI Speed Matching Script\nOnline...\n"); 
        Echo($"Next refresh in {Math.Max(refreshInterval - timeSinceRefresh, 0):N0} seconds");
    } 
    catch (Exception e)
    { 
        Echo("Exception in Main!");
        Me.CustomData += $"> Speed Matcher Exception\n{e.StackTrace}\n";
        isSetup = false; 
    } 
} 
 
List<IMyCameraBlock> raycastCameras = new List<IMyCameraBlock>(); 
List<IMyThrust> allThrust = new List<IMyThrust>(); 
List<IMyShipController> allShipControllers = new List<IMyShipController>(); 
List<IMyTextPanel> textPanels = new List<IMyTextPanel>();
 
bool GrabBlocks() 
{ 
    GetAllowedGrids(Me, 5000);
    if (!allowedGridsFinished)
        return false;
    
    bool successfulSetup = true; 
    GridTerminalSystem.GetBlocksOfType(raycastCameras, x => x.CustomName.ToLower().Contains(raycastCameraNameTag.ToLower()) && IsAllowedGrid(x)); 
    if (raycastCameras.Count == 0) 
    { 
        Echo($"Error: No cameras named '{raycastCameraNameTag}' were found"); 
        successfulSetup = false; 
    }
    else
    {
        foreach (var block in raycastCameras)
            block.EnableRaycast = true;
    }
 
    GridTerminalSystem.GetBlocksOfType(textPanels, x => x.CustomName.ToLower().Contains(textPanelNameTag.ToLower()) && IsAllowedGrid(x)); 
    if (textPanels.Count == 0) 
    { 
        Echo($"Warning: No text panels named '{textPanelNameTag}' were found"); 
    }
 
    GridTerminalSystem.GetBlocksOfType(allThrust, x => !x.CustomName.ToLower().Contains(ignoreThrustNameTag.ToLower()) && IsAllowedGrid(x));
    if (allThrust.Count == 0) 
    { 
        Echo("Error: No thrusters on grid or subgrids"); 
        successfulSetup = false; 
    }
 
    GridTerminalSystem.GetBlocksOfType(allShipControllers, x => IsAllowedGrid(x)); 
    if (allShipControllers.Count == 0) 
    { 
        Echo("Error: No ship controllers on grid or subgrids"); 
        successfulSetup = false; 
    } 
 
    return successfulSetup; 
} 
 
void MatchSelf() 
{ 
    targetVelocityVec = allShipControllers[0].GetShipVelocities().LinearVelocity; 
 
    scanTargetName = Me.CubeGrid.CustomName; 
    scanTargetType = "SELF"; 
    scanTargetRelation = "SELF"; 
    scanTargetSpeed = $"{targetVelocityVec.Length():N2}"; 
} 
 
double autoScanInterval = 0; 
double secondsTillScan = 0; 
 
MyDetectedEntityInfo targetInfo = new MyDetectedEntityInfo(); 
MyDetectedEntityInfo storedTargetInfo = new MyDetectedEntityInfo(); 
void Raycaster() 
{ 
    foreach (var camera in raycastCameras) 
    { 
        camera.EnableRaycast = true; 
    } 
 
    //string targetStatus = "///WMI Speed Matcher///\nNo target selected"; 
    if (shouldScan) 
    { 
        var thisCamera = GetCameraWithMaxRange(raycastCameras); 
 
        if (thisCamera.AvailableScanRange < raycastScanRange) 
        { 
            //Echo("Waiting for scan..."); 
            return; 
        } 

        autoScanInterval = raycastScanRange / 2000 / raycastCameras.Count; 
        secondsTillScan = Math.Max((raycastScanRange - thisCamera.AvailableScanRange) / 2000, 0); 

        if (timeSinceLastScan >= autoScanInterval && secondsTillScan == 0) 
        { 
            targetInfo = thisCamera.Raycast(raycastScanRange); 
            timeSinceLastScan = 0; 
        } 

        if (!targetInfo.IsEmpty()) 
        { 
            targetVelocityVec = targetInfo.Velocity; 
            storedTargetInfo = targetInfo; 
            shouldScan = false; //successful scan has completed 
        } 
    } 
 
    successfulScan = !storedTargetInfo.IsEmpty(); 
    if (successfulScan) 
    { 
        //targetStatus = $"///WMI Speed Matcher///\n {status}\n Target Info\n Name: {storedTargetInfo.Name}\n Type: {storedTargetInfo.Type}\n Velocity: {storedTargetInfo.Velocity.Length():N2}\n Relation: {storedTargetInfo.Relationship}"; 
        scanTargetName = storedTargetInfo.Name; 
        scanTargetSpeed = $"{storedTargetInfo.Velocity.Length():N2}"; 
        scanTargetType = storedTargetInfo.Type.ToString(); 
        scanTargetRelation = storedTargetInfo.Relationship.ToString(); 
    } 
} 
 
void BuildOutputText() 
{ 
    string status = shouldMatch ? enabled : disabled; 
    string scanStatus = shouldScan ? "Searching..." : "Idle"; 
 
    string percentageBar = secondsTillScan <= autoScanInterval ? PercentageBar(timeSinceLastScan, autoScanInterval, GetMaxHorizontalChars(1.6f)) : PercentageBar(timeSinceLastScan, secondsTillScan - autoScanInterval, GetMaxHorizontalChars(1.6f)); ; 
 
    string targetStatus; 
    if ((successfulScan || matchSelf) && !shouldScan) 
        targetStatus = $"///WMI Speed Matcher///\n Matching: {status}\n\n Scan Status: {scanStatus}\n{percentageBar}\n\n Scan Info\n Name: {scanTargetName}\n Type: {scanTargetType}\n Velocity: {scanTargetSpeed} m/s\n Relation: {scanTargetRelation}"; 
    else 
        targetStatus = $"///WMI Speed Matcher///\n Matching: {disabled}\n\n Scan Status: {scanStatus}\n{percentageBar}\n\n No target found"; 
 
    WriteToTextPanel(targetStatus); 
} 
 
void SpeedMatcher()
{
    if (!shouldMatch)
    {
        foreach (var thisThrust in allThrust)
        {
            SetThrusterOverride(thisThrust, 0f);
        }
        return;
    }

    var thisController = GetControlledShipController(allShipControllers);
    var myVelocityVec = thisController.GetShipVelocities().LinearVelocity;
    var inputVec = thisController.MoveIndicator;
    var desiredDirectionVec = Vector3D.TransformNormal(inputVec, thisController.WorldMatrix); //world relative input vector 
    var relativeVelocity = myVelocityVec - targetVelocityVec;

    //if (!shouldScan) //if the scan is finished 
        //thisController.DampenersOverride = false;

    ApplyThrust(allThrust, relativeVelocity, desiredDirectionVec, thisController);
} 
 
void ApplyThrust(List<IMyThrust> thrusters, Vector3D travelVec, Vector3D desiredDirectionVec, IMyShipController thisController) 
{ 
    var mass = thisController.CalculateShipMass().PhysicalMass;
    var gravity = thisController.GetNaturalGravity();
    
    var desiredThrust = mass * (2 * travelVec + gravity);
    var thrustToApply = desiredThrust;
    if (!Vector3D.IsZero(desiredDirectionVec))
    {
        thrustToApply = VectorRejection(desiredThrust, desiredDirectionVec);
    }
    
    //convert desired thrust vector to local
    //thrustToApply = Vector3D.TransformNormal(thrustToApply, MatrixD.Transpose(thisController.WorldMatrix));
    
    foreach(IMyThrust thisThrust in thrusters)
    {
        if (Vector3D.Dot(thisThrust.WorldMatrix.Backward, desiredDirectionVec) > .7071) //thrusting in desired direction
        {
            thisThrust.ThrustOverridePercentage = 1f;
        }
        else if (Vector3D.Dot(thisThrust.WorldMatrix.Forward, thrustToApply) > 0 && thisController.DampenersOverride)
        {
            var neededThrust = Vector3D.Dot(thrustToApply, thisThrust.WorldMatrix.Forward);
            var outputProportion = MathHelper.Clamp(neededThrust / thisThrust.MaxEffectiveThrust, 0, 1);
            thisThrust.ThrustOverridePercentage = (float)outputProportion;
            thrustToApply -= thisThrust.WorldMatrix.Forward * outputProportion * thisThrust.MaxEffectiveThrust;
        }
        else
        {
            thisThrust.ThrustOverridePercentage = 0.000001f;
        }
    }
} 

void IncrementMatchedSpeed(string arg)
{
    if (Vector3D.IsZero(targetVelocityVec, 1e-3))
        return;
    
    if (!arg.StartsWith("increment", StringComparison.OrdinalIgnoreCase))
        return;
    
    arg = arg.Replace("increment", "").Trim();
    double speedIncrement = 0;
    if (!double.TryParse(arg, out speedIncrement))
        return;

    var targetTravel = Vector3D.Normalize(targetVelocityVec); //get current direction of target's travel
    targetVelocityVec += targetTravel * speedIncrement;
}

Vector3D VectorRejection(Vector3D a, Vector3D b) //reject a on b    
{
    if (Vector3D.IsZero(b))
        return Vector3D.Zero;

    return a - a.Dot(b) / b.LengthSquared() * b;
}
 
void SetThrusterOverride(IMyThrust thruster, float overrideValue) 
{ 
    thruster.ThrustOverridePercentage = overrideValue * 0.01f; 
} 

IMyCameraBlock GetCameraWithMaxRange(List<IMyCameraBlock> cameraList) 
{ 
    //Assumes that cameraList is NOT empty 
    double maxRange = 0; 
 
    IMyCameraBlock maxRangeCamera = cameraList[0]; 
 
    foreach (IMyCameraBlock thisCamera in cameraList) 
    { 
        if (thisCamera.AvailableScanRange > maxRange) 
        { 
            maxRangeCamera = thisCamera; 
            maxRange = maxRangeCamera.AvailableScanRange; 
        } 
    }

    return maxRangeCamera; 
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
 
void WriteToTextPanel(string textToWrite, bool append = false) 
{ 
    foreach (var thisScreen in textPanels) 
    { 
        thisScreen.WritePublicText(textToWrite, append); 
        thisScreen.ShowPublicTextOnScreen(); 
        thisScreen.SetValue("FontSize", 1.6f); 
    } 
} 
 
int GetMaxHorizontalChars(float textSize) 
{ 
    int pixelWidth = (int)Math.Floor(650 / textSize); 
    int startAndEndWidth = 9 + 1 + 9; 
    int charWidth = 6 + 1; 
 
    pixelWidth -= startAndEndWidth; 
 
    if (pixelWidth <= 0) 
        return 0; 
    else 
        return (int)Math.Floor((double)pixelWidth / (double)charWidth); 
} 
 
//Whip's Percentage Bar Method 
string PercentageBar(double current, double max, int maxBarTicks = 50) 
{ 
    double percent = current / max * 100; 
 
    if (percent > 100) 
        percent = 100; 
    else if (percent < 0) 
        percent = 0; 
 
    double percentIncrement = 100 / (double)maxBarTicks; 
 
    int percentFullLength = (int)MathHelper.Clamp(Math.Round(percent / percentIncrement), 0, maxBarTicks); 
    int percentEmptyLength = MathHelper.Clamp(maxBarTicks - percentFullLength, 0, maxBarTicks); 
 
    return "[" + new String('|', percentFullLength) + new String('\'', percentEmptyLength) + "]"; 
} 

/*
/ //// / Whip's GetAllowedGrids method v1 - 3/17/18 / //// /
Derived from Digi's GetShipGrids() method - https://pastebin.com/MQUHQTg2
*/
List<IMyMechanicalConnectionBlock> allMechanical = new List<IMyMechanicalConnectionBlock>();
HashSet<IMyCubeGrid> allowedGrids = new HashSet<IMyCubeGrid>();
bool allowedGridsFinished = true;
void GetAllowedGrids(IMyTerminalBlock reference, int instructionLimit = 1000)
{
    if (allowedGridsFinished)
    {
        allowedGrids.Clear();
        allowedGrids.Add(reference.CubeGrid);
    }

    GridTerminalSystem.GetBlocksOfType(allMechanical, x => x.TopGrid != null);

    bool foundStuff = true;
    while (foundStuff)
    {
        foundStuff = false;

        for (int i = allMechanical.Count - 1; i >= 0; i--)
        {
            var block = allMechanical[i];
            if (allowedGrids.Contains(block.CubeGrid))
            {
                allowedGrids.Add(block.TopGrid);
                allMechanical.RemoveAt(i);
                foundStuff = true;
            }
            else if (allowedGrids.Contains(block.TopGrid))
            {
                allowedGrids.Add(block.CubeGrid);
                allMechanical.RemoveAt(i);
                foundStuff = true;
            }
        }

        if (Runtime.CurrentInstructionCount >= instructionLimit)
        {
            Echo("Instruction limit reached\nawaiting next run");
            allowedGridsFinished = false;
            return;
        }
    }

    allowedGridsFinished = true;
}

bool IsAllowedGrid(IMyTerminalBlock block)
{
    return allowedGrids.Contains(block.CubeGrid);
}

/* 
///CHANGELOG/// 
* Removed GetWorldMatrix() method since world matricies were fixed - v6 
* Removed a bunch of unused angle constants and hard coded them - v6 
* Cleaned up arguments - v7 
* Redesigned refresh function to be more efficient - v8 
* Added in variable config code - v9 
* Added "clear" command - v9 
* Touched up output text - v9 
* Added percentage bar - v10 
* Added target relation and type to scan info - v10 
* Fixed dampeners turning off when no successful scan has been completed - v11
* Fixed some formatting issues - v12
* Removed unused methods - v13
* Simplified some math and removed unused methods - v14
* Added thrust ignore name tag - v15
* Code now checks for thrusters only on the same grid - 15
* Code nolonger needs a timer to trigger a loop - v18
* Added a speed increment method - v19
* Removed an unnecessary .Length() call - v19
* Changed dampening method to use the algorithm that keen uses - v20
* Changed "scan" and "match" commands into toggle functions - v21
* Added dampener status recognition - v21
* Fixed issue where codes would trigger multiple times per tick in DS - v22
* Reduced update frequency - v23
* Added exception outout - v23
* Updated update frequency bandaid - v23
* Added GetAllowedGrids method to allow program to placed on subgrids - v23
* Changed how "match" command behaves - v23
* Changed update frequency workaround - v24
* Fix for keen's stupid negative runtime bug - v25
*/