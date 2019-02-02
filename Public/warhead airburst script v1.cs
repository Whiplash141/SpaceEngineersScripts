/*
/ //// / Whip's Warhead Airburst Script / //// /

Description:
This code will constantly check a remote's altitude to determine if it
should detonate attached warheads.

Instructions:
1. Put warheads and a single remote control on each bomb
2. Put this program on each bomb
3. Run this program with the argument 'arm' to arm the bombs
4. (Optional) Run this program with 'safety' to disarm the bombs

*/

double detonationAltitude = 5; //meters above the ground

//==========================================================
/////////////////// NO TOUCHEY BELOW ///////////////////////
//==========================================================

Program()
{
    Echo("Run with argument 'arm' to enable");
    Echo("\nRun with argument 'safety' to disable");
}

const double tick = 1.0 / 60.0;
List<IMyWarhead> warheads = new List<IMyWarhead>();
void Main(string arg, UpdateType updateSource)
{
    if (arg.Equals("arm", StringComparison.OrdinalIgnoreCase))
    {
        Runtime.UpdateFrequency = UpdateFrequency.Update1;
    }
    else if (arg.Equals("safety", StringComparison.OrdinalIgnoreCase))
    {
        Runtime.UpdateFrequency = UpdateFrequency.None;
    }
    
    if ((updateSource & UpdateType.Update1) == 0)
        return;
    
    Echo($"Whip's Warhead Airburst\n Script Online {RunningSymbol()}");
    
    var controller = GetFirstBlockOfType<IMyShipController>();
    if (controller == null)
    {
        Echo("> Error: No ship controller found");
        Runtime.UpdateFrequency = UpdateFrequency.None;
        return;
    }
    
    GridTerminalSystem.GetBlocksOfType(warheads);
    if (warheads.Count == 0)
    {
        Echo("> Error: No warheads found");
        Runtime.UpdateFrequency = UpdateFrequency.None;
        return;
    }
    
    if (Vector3D.IsZero(controller.GetNaturalGravity()))
    {
        Echo("> No gravity...");
        return;
    }
    
    double currentAltitude = 0;
    controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out currentAltitude);
    double shipSpeed = controller.GetShipSpeed();
    double safetyMargin = shipSpeed * tick;
    
    Echo($"\nCurrent altitude: {currentAltitude:n0}");
    Echo($"Detonation altitude: {detonationAltitude:n0}");
    
    if (currentAltitude <= detonationAltitude + safetyMargin)
    {
        Echo("Boom");
        foreach (var block in warheads)
        {
            block.IsArmed = true;
            block.Detonate();
        }
        Runtime.UpdateFrequency = UpdateFrequency.None;
    }
}

List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
T GetFirstBlockOfType<T>(string filterName = "") where T : class, IMyTerminalBlock
{
    blocks.Clear();
    if (filterName == "")
        GridTerminalSystem.GetBlocksOfType<T>(blocks);
    else
        GridTerminalSystem.GetBlocksOfType<T>(blocks, x => x.CustomName.Contains(filterName));

    return blocks.Count > 0 ? blocks[0] as T: null;
}

//Whip's Running Symbol Method v8
//•
int runningSymbolVariant = 0;
int runningSymbolCount = 0;
const int increment = 10;
string[] runningSymbols = new string[] {"−", "\\", "|", "/"};

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