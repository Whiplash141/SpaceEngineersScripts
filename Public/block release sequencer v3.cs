/*   
/// Whip's Merge/Rotor/Connector Sequenced Release Script v3 - revision: 5/15/18 ///

- Whiplash141
*/

//---You can modify these
string releaseGroupName = "Release";
const int framesBetweenReleases = 30; //delay between releases in frames (60 = 1 sec)    

//---Don't touch these
int currentFrame = 0;
int releaseIndex = 0;
bool shouldTrigger = false;
List<IMyTerminalBlock> releaseBlocks = new List<IMyTerminalBlock>();
List<IMyWarhead> warheads = new List<IMyWarhead>();

void Main(string arg, UpdateType updateSource)
{
    if (arg.ToLower() == "release" && !shouldTrigger)
    {
        if (GetBlocks())
        {
            currentFrame = framesBetweenReleases;
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            shouldTrigger = true;
            ArmWarheads();
        }
        else
        {
            Echo("Release sequence cancelled...");
            return;
        }
    }

    if ((updateSource & UpdateType.Update1) != 0)
    {
        if (currentFrame % framesBetweenReleases == 0)
        {
            SequenceRelease();
            currentFrame = 0;
        }
        currentFrame++;

        Echo("Releasing: " + shouldTrigger.ToString());
        Echo("Current Frame: " + currentFrame);
        Echo("Max Frame: " + framesBetweenReleases);
        Echo("Current Index: " + releaseIndex);
        Echo("No. of Merges: " + releaseBlocks.Count.ToString());
    }
}

bool GetBlocks()
{
    var group = GridTerminalSystem.GetBlockGroupWithName(releaseGroupName);
    if (group == null)
    {
        Echo($"Error: No block group named '{releaseGroupName}'");
        return false;
    }

    group.GetBlocks(releaseBlocks, x => x is IMyMotorStator || x is IMyShipConnector || x is IMyShipMergeBlock);
    
    group.GetBlocksOfType(warheads);

    if (releaseBlocks.Count == 0)
    {
        Echo($"Error: No merges, connectors, or rotors found in '{releaseGroupName}' group");
        return false;
    }
    
    releaseBlocks.Sort((a, b) => a.CustomName.CompareTo(b.CustomName));

    return true;
}

void SequenceRelease()
{
    if (releaseIndex < releaseBlocks.Count)
    {
        var block = releaseBlocks[releaseIndex];

        var merge = block as IMyShipMergeBlock;
        if (merge != null)
            merge.Enabled = false;

        var rotor = block as IMyMotorStator;
        if (rotor != null)
            rotor.Detach();

        var connector = block as IMyShipConnector;
        if (connector != null)
            connector.Disconnect();

        releaseIndex++;
    }
    else
    {
        Runtime.UpdateFrequency = UpdateFrequency.None;
        releaseIndex = 0;
        shouldTrigger = false;
        ResetReleaseBlocks();
    }
}

void ResetReleaseBlocks()
{
    foreach (var block in releaseBlocks)
    {
        var merge = block as IMyShipMergeBlock;
        if (merge != null)
            merge.Enabled = true;

        var rotor = block as IMyMotorStator;
        if (rotor != null)
            rotor.Attach();

        var connector = block as IMyShipConnector;
        if (connector != null)
            connector.Connect();
    }
}

void ArmWarheads()
{
    foreach (var block in warheads)
    {
        block.IsArmed = true;
    }
}
