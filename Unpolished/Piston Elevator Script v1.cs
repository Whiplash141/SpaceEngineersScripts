//Whip's Piston Elevator Script
string elevatorGroupName = "Elevator";

bool isSetup = false;

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    isSetup = GetBlocks();
    /*foreach (var door in doors)
    {
        door.OpenDoor();
    }*/
}

void Main(string arg, UpdateType updateSource)
{
    if ((updateSource & UpdateType.Update10) == 0)
        return;

    //if (!isSetup)
    isSetup = GetBlocks();

    if (!isSetup)
        return;

    DoWork();
}

List<IMyPistonBase> pistons = new List<IMyPistonBase>();
List<IMyDoor> doors = new List<IMyDoor>();
List<IMyTimerBlock> timers = new List<IMyTimerBlock>();
List<IMyBlockGroup> elevatorGroups = new List<IMyBlockGroup>();
List<IMyBlockGroup> allGroups = new List<IMyBlockGroup>();
bool GetBlocks()
{
    elevatorGroups.Clear();
    GridTerminalSystem.GetBlockGroups(allGroups);
    foreach (var group in allGroups)
    {
        if (group.Name.ToUpperInvariant().Contains(elevatorGroupName.ToUpperInvariant()))
        {
            elevatorGroups.Add(group);
        }
    }

    if (elevatorGroups.Count == 0)
    {              
        Echo($">> Error: No groups named '{elevatorGroupName}' found");
        return false;
    }

    return true;
}

enum ElevatorStatus {Idle = 0, Moving = 1};


//Dictionary<IMyDoor, Enum> elevatorDoors = new Dictionary<IMyDoor, Enum>();
List<IMyDoor> movingDoors = new List<IMyDoor>();
void DoWork()
{
    //remove doors that are done moving
    movingDoors.RemoveAll(x => x.Status == (DoorStatus.Open));

    foreach(var group in elevatorGroups)
    {
        CheckDoors(group);
    }
}

void CheckDoors(IMyBlockGroup group)
{
    group.GetBlocksOfType(doors);
    group.GetBlocksOfType(pistons);
    group.GetBlocksOfType(timers);

    var isSetup = true;
    if (doors.Count == 0)
    {
        isSetup = false;
        Echo($">> Error: No door in group '{group.Name}'");
    }

    if (pistons.Count == 0)
    {
        isSetup = false;
        Echo($">> Error: No pistons in group '{group.Name}'");
    }
    if (!isSetup)
        return;

    var door = doors[0];
    if (door.Status == DoorStatus.Closed)
    {
        if (!movingDoors.Contains(door))
        {
            movingDoors.Add(door);
            door.Enabled = false;

            foreach (var piston in pistons)
                piston.Reverse();
                
            foreach (var timer in timers)
                timer.Trigger();
        }
        else
        {
            foreach (var piston in pistons)
            {
                if ((piston.Status == PistonStatus.Extending && piston.Velocity > 0) || (piston.Status == PistonStatus.Retracting && piston.Velocity < 0))
                {
                    Echo($"Pistons for '{door.CustomName}' are moving...");
                    return;
                }
            }

            Echo($"Pistons for '{door.CustomName}' are done moving...");
            movingDoors.Remove(door);
            door.Enabled = true;
            door.OpenDoor();
        }
    }
}

T FindBaseGrid<T>(List<T> basesToSearch, IMyTerminalBlock blockOnSubGrid) where T : class, IMyMechanicalConnectionBlock
{
    var topGrid = blockOnSubGrid.CubeGrid;
    foreach (var baseBlock in basesToSearch)
    {
        if (baseBlock.TopGrid == topGrid)
        {
            return baseBlock;
        }
    }
    return null;
}