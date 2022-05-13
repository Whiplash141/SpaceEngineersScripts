/*
/ //// / Simple Reactor Status Light Script / //// /

Niche script written for hOtDoG 7 by Whiplash141

INSTRUCTIONS
1) Add reactors and lights to a group named "Reactor Status"
2) Place this script in a programmable block
3) Profit!

BEHAVIOR
When any of the reactors are on, the status lights will be turned on.
When the reactors are ALL off, the status lights will turn off.
*/

const string _reactorStatusGroupName = "Reactor Status";

enum ReturnCode { Success = 0, NoGroup = 1, NoLights = 2, NoReactors = 4 }

List<IMyReactor> _reactors = new List<IMyReactor>();
List<IMyLightingBlock> _lights = new List<IMyLightingBlock>();

ReturnCode _setupResult;

ReturnCode Setup()
{
    _reactors.Clear();
    _lights.Clear();
    
    IMyBlockGroup grp = GridTerminalSystem.GetBlockGroupWithName(_reactorStatusGroupName);
    if (grp == null)
    {
        return ReturnCode.NoGroup;
    }
    
    grp.GetBlocksOfType(_reactors, b => b.IsSameConstructAs(Me));
    grp.GetBlocksOfType(_lights, b => b.IsSameConstructAs(Me));
    
    ReturnCode returnCode = ReturnCode.Success;
    if (_lights.Count == 0)
    {
        returnCode |= ReturnCode.NoLights;
    }
    if (_reactors.Count == 0)
    {
        returnCode |= ReturnCode.NoReactors;
    }
    
    return returnCode;
}

Program()
{
    _setupResult = Setup();
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

void Main(string arg, UpdateType source)
{
    if (arg == "setup")
    {
        _setupResult = Setup();
    }
    
    if ((source & UpdateType.Update100) != 0)
    {
        Echo($"Whip's Simple Reactor Status Lights\nLast run: {DateTime.Now}\n");
        if (_setupResult == ReturnCode.Success)
        {
            Echo("Running...");
            bool reactorsOn = false;
            foreach (var r in _reactors)
            {
                if (r.IsWorking)
                {
                    reactorsOn = true;
                    break;
                }
            }
            
            foreach (var l in _lights)
            {
                l.Enabled = reactorsOn;
            }
        }
        else
        {
            if (_setupResult == ReturnCode.NoGroup) 
            {
                Echo($"ERROR: No group named '{_reactorStatusGroupName}' was found\n");
            }
            if ((_setupResult & ReturnCode.NoLights) != 0)
            {
                Echo($"ERROR: No lights found in group '{_reactorStatusGroupName}'\n");
            }
            if ((_setupResult & ReturnCode.NoReactors) != 0)
            {
                Echo($"ERROR: No reactors found in group '{_reactorStatusGroupName}'\n");
            }
        }
        Echo("Run the script with the argument 'setup' to refetch blocks.");
    }
}
