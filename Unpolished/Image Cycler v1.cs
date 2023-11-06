//Whip's Image Cycler v1 - 10/22/17

string cyclingLCDGroupName = "Cycle";
string storageLCDGroupName = "Storage";

void Main(string argument)
{
    if (string.IsNullOrWhiteSpace(argument))
    {
        Echo("No argument detected\nSkipping execution");
        return;
    }
    Echo("");

    var cyclingGroup = GridTerminalSystem.GetBlockGroupWithName(cyclingLCDGroupName);
    var storageGroup = GridTerminalSystem.GetBlockGroupWithName(storageLCDGroupName);

    if (storageGroup == null)
    {
        Echo("Toad... read the instructions slut");
        Echo($"Error: No group named '{storageLCDGroupName}' was found");
        return;
    }

    if (cyclingGroup == null)
    {
        Echo("Goddamnit Toad...");
        Echo($"Error: No group named '{cyclingLCDGroupName}' was found");
        return;
    }

    var storageLCDs = new List<IMyTextPanel>();
    storageGroup.GetBlocksOfType(storageLCDs, x => x.CustomName.ToLower().Contains(argument.ToLower()));
    
    if (storageLCDs.Count == 0)
    {
        Echo($"Error: No text panels named '{argument.ToLower()}' found in group '{storageLCDGroupName}'");
        return;
    }

    var cyclingLCDs = new List<IMyTextPanel>();
    cyclingGroup.GetBlocksOfType(cyclingLCDs);

    if (cyclingLCDs.Count == 0)
    {
        Echo($"Error: No text panels found in group '{cyclingLCDGroupName}'");
        return;
    }

    var storagePanel = storageLCDs[0];

    foreach (var block in cyclingLCDs)
    {
        block.WriteText(storagePanel.GetText());
        block.ContentType = ContentType.TEXT_AND_IMAGE;
    }
}
