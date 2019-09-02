/*
/// Whip's Raycast Tripwire Code v9 - 5/1/18 ///
_____________________________________________________________________________________________________
///DESCRIPTION///

    The code uses camera(s) to serve as tripwires to trigger warheads and timers. The code will 
grab all warheads on the grid as this is designed for torpedo systems. You only need to name
the cameras and (optionally) the timers.

    You can configure the range of the tripwire below in the VARIABLES section. Also the code will
ignore planets and friendly targets by default, but you can change this behavior as well in the 
VARIABLES section.

    This code also has a minimum arming time where the tripwires will be inactive. This time is counted
after the first triggering of this code.
_____________________________________________________________________________________________________
///INSTRUCTIONS///

1.) Place this code in a program block
2.) Add "Tripwire" into the name of your camera
3.) (Optional): Add "Tripwire" to the name of any timer you want triggered when the tripwire is tripped
4.) Run the code with the argument "activate" to begin the arming sequence

*/

//===================================================================================================
// VARIABLES - You can modify these
//===================================================================================================
const string cameraName = "Tripwire"; //name of cameras to serve as tripwires
const string timerName = "Tripwire"; //Name of timers to be triggered on tripwire being crossed
const double range = 5; //range of tripwire (forward of camera's face)
const double minumumArmTime = 2; //time after first triggering that the tripwire cameras will not be armed
const double minimumTargetSize = 10;
const double maximumLiveTime = -1; //seconds, -1 means infinite
const bool ignorePlanetSurface = true; //if the code should ignore planet surfaces
const bool ignoreFriends = false; //if the code should ignore friendlies
const bool useWarheads = true;

//===================================================================================================
// DO NOT TOUCH ANYTHING BELOW // DO NOT TOUCH ANYTHING BELOW // DO NOT TOUCH ANYTHING BELOW //
//===================================================================================================

double currentTimeElapsed = 0;

void Main(string argument, UpdateType update)
{
    if (argument.Equals("activate", StringComparison.OrdinalIgnoreCase))
    {
        Runtime.UpdateFrequency = UpdateFrequency.Update1;
    }

    if ((update & UpdateType.Update1) == 0) //does not contain update1 flag
        return;

    currentTimeElapsed += (1.0 / 60.0);
    
    if (currentTimeElapsed < minumumArmTime)
    {
        Echo($"Arming... \nTime Left: {minumumArmTime - currentTimeElapsed}");
        return;
    }
    
    if (maximumLiveTime > 0 && currentTimeElapsed > maximumLiveTime)
    {
        Detonate();
        Trigger();
    }

    Echo("< Tripwire Armed >");
    Echo($"Range: {range} m");

    var cameras = new List<IMyCameraBlock>();
    GridTerminalSystem.GetBlocksOfType(cameras, block => block.CustomName.Contains(cameraName));

    if (cameras.Count == 0)
    {
        Echo($"Error: No camera named '{cameraName}' was found");
        return;
    }
    Echo($"Camera Count: {cameras.Count}");

    foreach (IMyCameraBlock thisCamera in cameras)
    {
        thisCamera.EnableRaycast = true;
        var targetInfo = thisCamera.Raycast(range);

        if (targetInfo.IsEmpty())
        {
            Echo("No target detected");
            continue;
        }
        else if (ignorePlanetSurface && targetInfo.Type == MyDetectedEntityType.Planet)
        {
            Echo("Planet detected\nIgnoring...");
            continue;
        }
        else if (ignoreFriends && (targetInfo.Relationship == MyRelationsBetweenPlayerAndBlock.FactionShare || targetInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Owner))
        {
            Echo("Friendly detected\nIgnoring...");
            continue;
        }
        else if (targetInfo.BoundingBox.Size.LengthSquared() < minimumTargetSize * minimumTargetSize)
        {
            Echo("Target too small\nIgnoring...");
            continue;
        }
        else
        {
            Echo("Target detected");
            Detonate();
            Trigger();
            return;
        }
    }
}

void Detonate()
{
    if (!useWarheads)
        return;
    var warheads = new List<IMyWarhead>();
    GridTerminalSystem.GetBlocksOfType(warheads);
    foreach (var thisWarhead in warheads)
    {
        thisWarhead.IsArmed = true;

        if (thisWarhead.CustomName.ToLower().Contains("start"))
            thisWarhead.StartCountdown();
        else
            thisWarhead.Detonate();
    }
}

void Trigger()
{
    var timers = new List<IMyTimerBlock>();
    GridTerminalSystem.GetBlocksOfType(timers, block => block.CustomName.Contains(timerName));
    foreach (IMyTimerBlock thisTimer in timers)
    {
        thisTimer.Trigger();
    }
}
