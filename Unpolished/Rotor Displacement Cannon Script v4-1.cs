// Whip's Rotor Displacement Cannon v4-1 - 2/10/18

bool shareInertiaTensor = true;
string displacementRotorGroupName = "Displacement";
string displacementRotorName = "Displacement";
string detachRotorName = "Detach";
int fireStage = 0;
bool isFiring = false;

void Main(string argument, UpdateType updateType)
{
    if ((updateType & (UpdateType.Trigger | UpdateType.Terminal | UpdateType.Script | UpdateType.Antenna)) != 0 && !isFiring)
    {
        if (argument.ToLower().Contains("fire"))
        {
            if (GrabBlocks())
            {
                fireStage = 1;
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
                isFiring = true;
            }
        }
    }

    Echo($"fireStage: {fireStage}");
    Echo($"{detachRotors.Count}");

    switch (fireStage)
    {
        case 1:
            SetRotorDisplacement(rotors, -0.4f);
            break;
        case 2:
            SetRotorDisplacement(rotors, 0.2f);
            break;
        case 3:
            SetMergePower(merges, false);
             AttachDetachRotors(detachRotors, false);
            break;
        case 6:
            //GrabBlocks();
            //SetRotorDisplacement(rotors, -0.4f);
            AttachDetachRotors(detachRotors, true);
            SetMergePower(merges, true);
            Runtime.UpdateFrequency = UpdateFrequency.None;
            isFiring = false;
            break;
    }

    if ((updateType & UpdateType.Update1) != 0)
        fireStage++;
}

IMyBlockGroup thisGroup = default(IMyBlockGroup);
List<IMyMotorStator> rotors = new List<IMyMotorStator>();
List<IMyMotorStator> detachRotors = new List<IMyMotorStator>();
List<IMyShipMergeBlock> merges = new List<IMyShipMergeBlock>();
List<IMyMotorStator> groupedRotors = new List<IMyMotorStator>();
List<IMyShipMergeBlock> groupedMerges = new List<IMyShipMergeBlock>();

bool GrabBlocks()
{
    thisGroup = GridTerminalSystem.GetBlockGroupWithName(displacementRotorGroupName);
    if (thisGroup == null)
    {
        Echo($"> Info: No group named '{displacementRotorGroupName}' was found");
    }
    else
    {
        thisGroup.GetBlocksOfType(groupedRotors);
        thisGroup.GetBlocksOfType(groupedMerges);
    }

    GridTerminalSystem.GetBlocksOfType(detachRotors, x => x.CustomName.Contains(detachRotorName));
    
    GridTerminalSystem.GetBlocksOfType(merges, x => x.CustomName.Contains(displacementRotorName));
    GridTerminalSystem.GetBlocksOfType(rotors, x => x.CustomName.Contains(displacementRotorName));
    
    rotors.AddRange(groupedRotors);
    merges.AddRange(groupedMerges);
    
    foreach(var block in rotors)
    {
        block.SetValue("ShareInertiaTensor", shareInertiaTensor);
        block.SetValue("RotorLock", true);
    }
    
    foreach(var block in detachRotors)
    {
        block.SetValue("ShareInertiaTensor", shareInertiaTensor);
        block.SetValue("RotorLock", true);
    }

    if (rotors.Count == 0)
    {
        Echo($">> Error: No rotors named '{displacementRotorName}'\nor in group named '{displacementRotorGroupName}' were found");
        return false;
    }

    if (merges.Count == 0)
    {
        Echo($"> Warning: No merges named '{displacementRotorName}'\nor in group named '{displacementRotorGroupName}' were found");
    }

    return true;
}

void SetRotorDisplacement(List<IMyMotorStator> rotors, float displacement)
{
    foreach (var block in rotors)
    {
        block.SetValueFloat("Displacement", displacement);
    }
}

void AttachDetachRotors(List<IMyMotorStator> rotors, bool attach)
{
    foreach (var block in rotors)
    {
        var command = attach ? "Add Top Part" : "Detach";
        block.ApplyAction(command);
    }
}

void SetMergePower(List<IMyShipMergeBlock> merges, bool onOff)
{
    foreach (var block in merges)
    {
        block.Enabled = onOff;
    }
}
