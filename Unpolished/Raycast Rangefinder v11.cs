
/* 
/ //// / Whip's Raycast Rangefinder v11 - 2023/11/16 / //// /
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

setup : Refetches all blocks

auto on : Turns autoscan on

auto off : Turns autoscan off

auto toggle : Toggles autoscan on/off
___________________________________________________________________
///USING MULTIPLE ARGUMENTS///

You can run multiple arguments at the same time by separating them
with a semicolon (;).

For example:
    setup;range 5000;start

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

List<IMyTextPanel> textPanelList = new List<IMyTextPanel>();
List<IMyCameraBlock> cameraList = new List<IMyCameraBlock>();
StringBuilder finalOutputBuilder = new StringBuilder();
StringBuilder scanInfoBuilder = new StringBuilder();
StringBuilder targetInfoBuilder = new StringBuilder();
MyDetectedEntityInfo targetInfo = new MyDetectedEntityInfo();

Program()
{
    Setup(); 
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

void Setup()
{
    textPanelList.Clear();
    cameraList.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, CollectBlocks);
}

void Main(string arg, UpdateType updateSource)
{
    ArgumentHandling(arg);

    if ((updateSource & UpdateType.Update10) == 0)
        return;

    RangeFinder();
}

void ArgumentHandling(string arg)
{
    if (string.IsNullOrWhiteSpace(arg))
    {
        return;
    }

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
        }
        else if (thisArg.Contains("setup"))
        {
            Echo("Running setup...");
            Setup();
        }
        else
        {
            Echo($"Error: unrecognized command \"{thisArg}\"");
        }
    }
}

bool CollectBlocks(IMyTerminalBlock block)
{
    var panel = block as IMyTextPanel;
    if (panel != null && block.CustomName.IndexOf(textPanelNameTag, StringComparison.OrdinalIgnoreCase) != -1)
    {
        textPanelList.Add(panel);
        return false;
    }

    var cam = block as IMyCameraBlock;
    if (cam != null && block.CustomName.IndexOf(cameraNameTag, StringComparison.OrdinalIgnoreCase) != -1)
    {
        cameraList.Add(cam);
        cam.EnableRaycast = true;
        return false;
    }

    return false;
}

void RangeFinder()
{
    double secondsTillScan = 0;
    double availableScanRange = 0;
    double autoScanInterval = 0;

    bool failed = false;

    //Check if camera list is empty
    if (cameraList.Count == 0)
    {
        Echo($"ERROR: No cameras with name tag '{cameraNameTag}' found!");
        failed = true;
    }

    if (textPanelList.Count == 0)
    {
        Echo($"Warning: No text panels with name tag {textPanelNameTag} found! Fix then recompile!");
        failed = true;
    }

    if (failed)
    {
        Echo("Fix then recompile or run the argument \"setup\"!");
        return;
    }

    IMyCameraBlock thisCamera = GetCameraWithMaxRange(cameraList);

    availableScanRange = thisCamera.AvailableScanRange;

    int availableScans = GetAvailableScans(cameraList, scanRange);

    secondsTillScan = Math.Max((scanRange - availableScanRange) / 2000, 0);

    timeSinceLastScan += Runtime.TimeSinceLastRun.TotalSeconds;
    autoScanInterval = scanRange / 2000 / cameraList.Count / Math.Max(availableScans, 1);

    //Attempt to scan range in front of camera
    if (availableScanRange >= scanRange && shouldScan)
    {
        if (!autoScan)
        {
            targetInfo = thisCamera.Raycast(scanRange);
            shouldScan = false;
            Echo("Scanning Finished");
            timeSinceLastScan = 0;
        }
        else if (timeSinceLastScan >= autoScanInterval && secondsTillScan <= autoScanInterval)
        {
            targetInfo = thisCamera.Raycast(scanRange);
            timeSinceLastScan = 0;
        }
    }

    string scanProgress;
    if (shouldScan)
    {
        if (autoScan)
        {
            scanProgress = "<< AutoScan Active... >>";
        }
        else
        {
            scanProgress = "< Scan in progress... >";
        }
    }
    else
    {
        scanProgress = "> No scans in progress <";
    }

    double timeUntilScan;
    double percentageCurrent;
    double percentageMax;
    if (!autoScan)
    {
        timeUntilScan = Math.Round(secondsTillScan, 2);
        percentageCurrent = availableScanRange;
        percentageMax = scanRange;
    }
    else
    {
        if (secondsTillScan <= autoScanInterval)
        {
            timeUntilScan = Math.Round(Math.Max(autoScanInterval - timeSinceLastScan, 0), 2);
            percentageCurrent = timeSinceLastScan;
            percentageMax = autoScanInterval;
        }
        else
        {
            timeUntilScan = Math.Round(secondsTillScan - autoScanInterval, 2);
            percentageCurrent = timeSinceLastScan;
            percentageMax = secondsTillScan - autoScanInterval;
        }
    }

    MyWaypointInfo? targetWaypoint = null;

    scanInfoBuilder.Clear();
    targetInfoBuilder.Clear();

    scanInfoBuilder
        .Append("/// WMI Raycast Rangefinder /// \n\n")
        .Append("Scan Range: ")
        .Append(scanRange)
        .Append(" m\nAvailable Scans: ")
        .Append(availableScans)
        .Append("\nStatus: ")
        .Append(scanProgress)
        .Append("\nNext scan ready in: ")
        .Append(timeUntilScan)
        .Append(" s\n");

    if (!targetInfo.IsEmpty())
    {
        Vector3D hitPosition = new Vector3D(0, 0, 0);
        if (targetInfo.HitPosition.HasValue)
            hitPosition = (Vector3D)targetInfo.HitPosition;

        targetWaypoint = new MyWaypointInfo(targetInfo.Name, hitPosition);

        double targetSize = Math.Round(targetInfo.BoundingBox.Size.Length());

        targetInfoBuilder
            .Append(" Target Info:\n    Range: ")
            .Append(Math.Round(Vector3D.Distance(hitPosition, Me.GetPosition())))
            .Append(" m \n    Velocity: ")
            .Append(Math.Round(targetInfo.Velocity.Length(), 2))
            .Append("\n    Size: ")
            .Append(targetSize)
            .Append(" m \n    Type: ")
            .Append(targetInfo.Type)
            .Append("\n    Relation: ")
            .Append(targetInfo.Relationship)
            .Append("\n    ")
            .Append(targetWaypoint.Value.ToString());

        if (targetInfo.Type == MyDetectedEntityType.Planet)
        {
            Vector3D targetCenter = targetInfo.Position;
            Vector3D targetCenterToHitPosVec = hitPosition - targetCenter;

            if (targetCenterToHitPosVec.LengthSquared() > 0)
            {
                targetCenterToHitPosVec = Vector3D.Normalize(targetCenterToHitPosVec);
            }

            Vector3D safetyOffsetVec = targetCenterToHitPosVec * 50000;

            Vector3D safeJumpPos = hitPosition + safetyOffsetVec;

            targetInfoBuilder
                .Append("\n    ")
                .Append(new MyWaypointInfo("GPS:Safe Jump Pos", safeJumpPos).ToString());
        }
    }
    else
    {
        targetInfoBuilder.Append("No target detected");
    }

    foreach (IMyTextPanel thisPanel in textPanelList)
    {
        //Set font size if allowed
        if (autoSetFontSize)
        {
            thisPanel.FontSize = 1.15f;
        }

        //Get max text panel width and scale our progress bar accordingly
        int panelWidth = GetMaxHorizontalChars(thisPanel);

        finalOutputBuilder.Clear();
        finalOutputBuilder
            .Append(scanInfoBuilder)
            .Append(PercentageBar(percentageCurrent, percentageMax, panelWidth))
            .Append("\n\n")
            .Append(targetInfoBuilder);

        thisPanel.WriteText(finalOutputBuilder.ToString());
        thisPanel.ContentType = ContentType.TEXT_AND_IMAGE;

        if (targetWaypoint.HasValue)
        {
            thisPanel.WritePublicTitle(targetWaypoint.Value.ToString());
        }
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
    double textSize = panel.FontSize;
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
