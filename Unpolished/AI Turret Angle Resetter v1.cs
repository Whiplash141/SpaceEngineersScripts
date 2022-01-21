// Whip's AI Turret Angle Resetter v1 - 1/15/18

//This is the name that you add to a turret if you dont want its position resetting
string turretExclusionNameTag = "Excluded";

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

List<IMyLargeTurretBase> turrets = new List<IMyLargeTurretBase>();

void Main(string argument, UpdateType updateSource)
{
    if ((updateSource & UpdateType.Update100) == 0)
        return;

    GridTerminalSystem.GetBlocksOfType(turrets, x => !x.CustomName.Contains(turretExclusionNameTag));

    if (turrets.Count == 0)
    {
        Echo(">Error: No turrets found");
        return;
    }

    foreach (var block in turrets)
    {
        if (block.HasTarget)
            continue; //if turret is targeting, ignore it
                
        //disable idne movement
        block.EnableIdleRotation = false;

        //reset turret position
        block.Azimuth = 0;
        block.Elevation = 0;

        //Sync changes
        block.SyncAzimuth();
        block.SyncElevation();
        block.SyncEnableIdleRotation();

        //Re-enable turret AI
        block.ResetTargetingToDefault();
    }
}