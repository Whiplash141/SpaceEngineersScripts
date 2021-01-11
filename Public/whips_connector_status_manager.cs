/*
 * / //// / Whip's Connector Status Manager / //// /
 *
 * INSTRUCTIONS
 * 1) Add connectors, ship controllers (cockpits), and blocks you want to be turned off
 *    when docked to a group named "Connector Status"
 * 2) When you lock a connector that is in this group, dampeners will be disabled and anu
 *    functional blocks (excluding the connector) will be turned off.
 * 3) When you unlock all connectors in this group, functional blocks in the  group will 
 *    be turned back on.
 */
const string VERSION = "1.0.0";
const string DATE = "2021/01/10";

string _groupName = "Connector Status";
ErrorCode _setupStatus = ErrorCode.None;

List<IMyShipConnector> _connectors = new List<IMyShipConnector>();
List<IMyFunctionalBlock> _functional = new List<IMyFunctionalBlock>();
List<IMyShipController> _controllers = new List<IMyShipController>();

enum ErrorCode { None = 0, NoGroup = 1, NoConnectors = 2, NoFunctional = 4, NoController = 8 }

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    _setupStatus = Setup();
}

void Main(string arg, UpdateType updateSource)
{
    if ((updateSource & UpdateType.Update100) != 0)
    {
        Echo($"WMI Connector Status Manager\n(Version {VERSION} - {DATE})\n");
        if (_setupStatus == ErrorCode.None)
        {
            if (HandleStatus())
            {
                Echo("Status: Connected");
            }
            else
            {
                Echo("Status: Disconnected");
            }
        }
        else
        {
            Echo("ERRORS:");
            if ((_setupStatus & ErrorCode.NoGroup) != 0)
            {
                Echo($"- No group named '{_groupName}'!");
            }
            if ((_setupStatus & ErrorCode.NoConnectors) != 0)
            {
                Echo($"- No connectors in '{_groupName}' group");
            }
            if ((_setupStatus & ErrorCode.NoFunctional) != 0)
            {
                Echo($"- No functional blocks in '{_groupName}' group");
            }
            if ((_setupStatus & ErrorCode.NoController) != 0)
            {
                Echo($"- No ship controllers in '{_groupName}' group");
            }
            return;
        }
    }
}

bool HandleStatus()
{
    bool connected = false;
    foreach (var c in _connectors)
    {
        if (c.Status == MyShipConnectorStatus.Connected)
        {
            connected = true;
            break;
        }
    }

    foreach (var f in _functional)
    {
        f.Enabled = !connected;
    }

    if (connected)
    {
        _controllers[0].DampenersOverride = false;
    }
    return connected;
}

ErrorCode Setup()
{
    var errorCode = ErrorCode.None;

    var group = GridTerminalSystem.GetBlockGroupWithName(_groupName);
    if (group == null)
    {
        errorCode = ErrorCode.NoGroup;
        return errorCode;
    }
    _connectors.Clear();
    _functional.Clear();

    group.GetBlocksOfType(_connectors, b => b.IsSameConstructAs(Me));
    group.GetBlocksOfType(_functional, b => b.IsSameConstructAs(Me) && !(b is IMyShipConnector));
    group.GetBlocksOfType(_controllers, b => b.IsSameConstructAs(Me));

    if (_connectors.Count == 0)
    {
        errorCode |= ErrorCode.NoConnectors;
    }

    if (_functional.Count == 0)
    {
        errorCode |= ErrorCode.NoFunctional;
    }

    if (_controllers.Count == 0)
    {
        errorCode |= ErrorCode.NoController;
    }
    return errorCode;
}
