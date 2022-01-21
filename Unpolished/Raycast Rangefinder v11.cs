
/* 
///Whip's Raycast Rangefinder v11 - 1/19/18///
///HOWDY!///
___________________________________________________________________
///SETUP///

1.) Place a programmable block with this code on your ship
    
2.) Place at least one camera on your ship with the name tag "Raycast" 
    in the name somewhere
    
3.) Place at least one LCD or text panel on your ship with the name tag
    "Raycast" somewhere in the name
    
4.) Run the code with the argument "start" to start scanning!
    (See the argument section for more arguments)
___________________________________________________________________
///ARGUMENTS///

start : Starts the scanning procedure. If the camera is not charged
        the program will begin charging the camera and will scan the
        desired distance once fully charged
        
stop : Stops the scanning procedure. The cameras will continue to 
       charge, but the program will not run any scans.
       
range <number> : Sets the desired range (in meters) to the specified <number>

range default : Sets the desired range back to the 50,000 meter default
     
*/

/* 
___________________________________________________________________  

///////////////CONFIGURABLE VARIABLES/////////////////   

========== You can edit these variables to your liking ============    
___________________________________________________________________    
*/

//Name tag of cameras to use
const string cameraNameTag = "Raycast"; 

//Name tag of text panels to write on
const string textPanelNameTag = "Raycast";

//Default scan range in meters    
const double defaultScanRange = 50000;

//Determines if the code will automatically begin another scan
//after the current one has finished
bool autoScan = false;

//Determines if code should automatically set the font size
bool autoSetFontSize = true;
  
/*    
___________________________________________________________________    

============= Don't touch anything below this :) ==================    
___________________________________________________________________    
*/

double scanRange = defaultScanRange;
double timeSinceLastScan = 0;
bool shouldScan = false;

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

void Main(string arg, UpdateType updateSource)
{
    //Argument handling
    #region Argument Handling
    string[] arguments = arg.ToLower().Split(';');

    if (arguments.Length == 0)
    {
        arguments[0] = arg;
    }

    foreach (string thisArg in arguments)
    {
        if (thisArg.Contains("range"))
        {
            if (thisArg.Contains("default"))
            {
                scanRange = defaultScanRange;
                continue;
            }

            //Remove the keyword and any spaces
            var trimmedArg = thisArg.Replace("range", "").Replace(" ", "");

            //Attempt to convert remain argument to a double
            double range = 0;
            bool canConvert = Double.TryParse(trimmedArg, out range);
            if (range <= 0)
            {
                Echo($"Error: Range of {range} m is too small");
                range = 30;
            }

            if (canConvert)
            {
                scanRange = range;
                Echo($"Range changed to {range} m");
            }
            else
            {
                Echo($"Error: unrecognized command '{thisArg}'");
            }
        }
        else if (thisArg.Contains("start"))
        {
            shouldScan = true;
            Echo("Scanning Started...");
        }
        else if (thisArg.Contains("stop"))
        {
            shouldScan = false;
            Echo("Scanning Stopped");
        }
        else if (thisArg.Contains("auto"))
        {
            //Remove the keyword and any spaces
            var trimmedArg = thisArg.Replace("auto", "").Replace(" ", "");
            
            if (trimmedArg.Contains("on"))
            {
                autoScan = true;
                Echo("Auto Scaning Enabled");
            }
            else if (trimmedArg.Contains("off"))
            {
                autoScan = false;
                Echo("Auto Scaning Disabled");
            }
            else if (trimmedArg.Contains("toggle"))
            {
                if (autoScan)
                {
                    autoScan = false;
                    Echo("Auto Scaning Disabled");
                }
                else
                {
                    autoScan = true;
                    Echo("Auto Scaning Enabled");
                }
            }
            else
            {
                Echo($"Error: unrecognized command '{thisArg}'");
            }
        }
    }
    #endregion
    
    if ((updateSource & UpdateType.Update1) == 0)
        return;
    
    RangeFinder(scanRange);
}

MyDetectedEntityInfo targetInfo = new MyDetectedEntityInfo();
void RangeFinder(double range)
{
    double secondsTillScan = 0;
    double availableScanRange = 0;
    double autoScanInterval = 0;

    //Get cameras
    var cameraList = new List<IMyCameraBlock>();
    GridTerminalSystem.GetBlocksOfType(cameraList, block => block.CustomName.ToLower().Contains(cameraNameTag.ToLower()));

    //Check if camera list is empty
    if (cameraList.Count == 0)
    {
        Echo($"Error: No cameras with name tag '{cameraNameTag}' found");
        return;
    }

    //Set raycast on for all our cameras
    foreach (IMyCameraBlock camera in cameraList)
    {
        if (!camera.EnableRaycast)
            camera.EnableRaycast = true;
    }

    //Get camera with maximum available range
    var thisCamera = GetCameraWithMaxRange(cameraList);

    availableScanRange = thisCamera.AvailableScanRange;
    
    var availableScans = GetAvailableScans(cameraList, range);
    
    //Get time until next scan
    secondsTillScan = Math.Max((range - availableScanRange) / 2000, 0);
    
    timeSinceLastScan += Runtime.TimeSinceLastRun.TotalSeconds;
    autoScanInterval = range / 2000 / cameraList.Count / Math.Max(availableScans, 1);

    //Attempt to scan range in front of camera
    if (availableScanRange >= range && shouldScan)
    {
        if (!autoScan)
        {
            targetInfo = thisCamera.Raycast(range);
            shouldScan = false;
            Echo("Scanning Finished");
            timeSinceLastScan = 0;
        }
        else if (timeSinceLastScan >= autoScanInterval && secondsTillScan <= autoScanInterval)
        {
            targetInfo = thisCamera.Raycast(range);
            timeSinceLastScan = 0;
        }
    }
    
    //Construct output text
    string targetStatus = "No target detected";
    if (!targetInfo.IsEmpty())
    {
        Vector3D hitPosition = new Vector3D(0, 0, 0);
        if (targetInfo.HitPosition.HasValue)
            hitPosition = (Vector3D)targetInfo.HitPosition;
        
        double targetSize = Math.Round(targetInfo.BoundingBox.Size.Length());
        
        targetStatus = $" Target Info:\n Range: {Math.Round(Vector3D.Distance(hitPosition, Me.GetPosition()))} m \n Velocity: {Math.Round(targetInfo.Velocity.Length(),2)}\n Size: {targetSize} m \n    Type: {targetInfo.Type}\n    Relation: {targetInfo.Relationship}"
            + $"\n GPS:{targetInfo.Name}:{Math.Round(hitPosition.X)}:{Math.Round(hitPosition.Y)}:{Math.Round(hitPosition.Z)}:";
            
        if (targetInfo.Type.ToString() == "Planet")
        {
            var targetCenter = (Vector3D)targetInfo.Position;
            var targetCenterToHitPosVec = hitPosition - targetCenter;
            
            if (targetCenterToHitPosVec.LengthSquared() > 0)
            {
                targetCenterToHitPosVec = Vector3D.Normalize(targetCenterToHitPosVec);
            }
            
            var safetyOffsetVec = targetCenterToHitPosVec * 50000;
            
            var safeJumpPos = hitPosition + safetyOffsetVec;
            
            targetStatus += $"\n GPS:Safe Jump Pos:{Math.Round(safeJumpPos.X)}:{Math.Round(safeJumpPos.Y)}:{Math.Round(safeJumpPos.Z)}:";
        }
    }

    //Get text panels
    var textPanelList = new List<IMyTextPanel>();
    GridTerminalSystem.GetBlocksOfType(textPanelList, block => block.CustomName.ToLower().Contains(textPanelNameTag.ToLower()));
    
    if (textPanelList.Count == 0)
    {
        Echo($"Error: No text panels with name tag {textPanelNameTag} found");
    }

    string scanProgress = "> No scans in progress <";
    if (shouldScan)
    {
        if (autoScan)
            scanProgress = "<< AutoScan Active... >>";
        else
        {
            scanProgress = "< Scan in progress... >"; 
        }
    }
    
    string scanCount = "";
    if (!autoScan)
    {
        scanCount = $"Available Scans: {availableScans}\n";
    }

    foreach (IMyTextPanel thisPanel in textPanelList)
    {
        //Set font size if allowed
        if (autoSetFontSize)
            thisPanel.SetValue("FontSize", 1.15f);
        
        //Get max text panel width and scale our progress bar accordingly
        int panelWidth = GetMaxHorizontalChars(thisPanel);
        string scanPercentageBar = "";
        
        if (!autoScan)
            scanPercentageBar = $"Status: {scanProgress}\nNext scan ready in: {Math.Round(secondsTillScan, 2)} s\n" + PercentageBar(availableScanRange, range, panelWidth);
        else
        {
            if (secondsTillScan <= autoScanInterval)
                scanPercentageBar = $"Status: {scanProgress}\nNext AutoScan ready in: {Math.Round(Math.Max(autoScanInterval - timeSinceLastScan, 0), 2)} s\n" + PercentageBar(timeSinceLastScan, autoScanInterval, panelWidth);
            else
                scanPercentageBar = $"Status: {scanProgress}\nNext AutoScan ready in: {Math.Round(secondsTillScan  - autoScanInterval, 2)} s\n" + PercentageBar(timeSinceLastScan, secondsTillScan - autoScanInterval, panelWidth);
        }
        string finalOutput = "/// WMI Raycast Rangefinder /// \n\n" + $"Scan Range: {range} m\n" + scanCount + scanPercentageBar + "\n\n" + targetStatus;

        thisPanel.WritePublicText(finalOutput);
        thisPanel.ShowPublicTextOnScreen();
    }
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

int GetAvailableScans(List<IMyCameraBlock> cameraList, double range)
{
    int scans = 0;
    
    foreach (IMyCameraBlock thisCamera in cameraList)
    {
        var availableScanRange = thisCamera.AvailableScanRange;
        
        scans += (int)Math.Floor(availableScanRange / range);
    }
    
    return scans;
}

int GetMaxHorizontalChars(IMyTextPanel panel)
{
    double textSize = panel.GetValue<float>("FontSize");
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

    return "[" + new String('|', percentFullLength) + new String('\'', percentEmptyLength) + "]"; // + String.Format("{0:000.0}%", Math.Round(percent, 2));
}