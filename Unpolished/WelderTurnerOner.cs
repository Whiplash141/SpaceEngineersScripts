string welderGroupName = "Welders";

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

List<IMyShipWelder> welderList = new List<IMyShipWelder>();
bool turnOn = true;

void Main(string arg, UpdateType updateSource)
{
    switch (arg.ToUpperInvariant())
    {
        case "ON":
            turnOn = true;
            break;

        case "OFF":
            turnOn = false;
            break;

        case "TOGGLE":
            turnOn = !turnOn;
            break;
    }

    if ((updateSource & UpdateType.Update100) == 0) //only run on update loop
        return;

    Echo("Whip's Welder Turner Oner...");
    string status = turnOn ? "Enabled" : "Disabled";
    Echo($"\n  Status: {turnOn}");

    var group = GridTerminalSystem.GetBlockGroupWithName(welderGroupName);

    if (group == null)
    {
        Echo($"Error: No group named '{welderGroupName}' was found");
        return;
    }

    group.GetBlocksOfType(welderList);

    if (welderList.Count == 0)
    {
        Echo("Error: No welders in welder group");
        return;
    }

    foreach (var block in welderList)
    {
        block.Enabled = turnOn;
    }
}
