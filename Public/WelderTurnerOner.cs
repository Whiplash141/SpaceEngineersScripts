//Whip's Welder Turner Oner v1 - 1/20/18

string welderGroupName = "Welders";

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

List<IMyShipWelder> welderList = new List<IMyShipWelder>();

void Main(string arg, UpdateType updateSource)
{
    if ((updateSource & UpdateType.Update100) == 0) //only run on update loop
        return;

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
        block.Enabled = true;
    }
}
