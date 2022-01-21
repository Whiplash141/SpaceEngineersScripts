/*
/ //// / Whip's Seat Status Timers / //// /

INSTRUCTIONS
1. Place this script in a programmable block
2. Place all status seats in a group named "Status Seats"
3. Recompile the code to process changes
4. Open the custom data of each seat in the "Status Seats" group 
   and set the names of the timers that should be triggered. Leave
   them blank if you don't want a timer triggered.
5. Recompile the code to process changes
*/

const string VERSION = "0.0.1";
const string DATE = "2020/12/04";

const string INI_SECTION_GENERAL = "Seat Status Timers - General Config";
const string INI_KEY_GROUP_NAME = "Group name";

const string INI_SECTION_TIMER = "Seat Status Timers - Seat Config";
const string INI_KEY_TIMER_ENTRY = "Timer to trigger on entry";
const string INI_KEY_TIMER_EXIT = "Timer to trigger on exit";

bool _isSetup = false;
string _groupName = "Status Seats";
MyIni _ini = new MyIni();
List<StatusTimer> _statusTimers = new List<StatusTimer>();

class StatusTimer
{
    IMyShipController _controller;
    IMyTimerBlock _entryTimer;
    IMyTimerBlock _exitTimer;
    MyIni _ini = new MyIni();
    Program _p;

    bool _firstRun = true;
    bool _wasControlled = false;

    public StatusTimer(IMyShipController controller, Program program)
    {
        _p = program;
        _controller = controller;
        string entryTimerName = "", exitTimerName = "";
        _ini.Clear();
        if (_ini.TryParse(_controller.CustomData))
        {
            entryTimerName = _ini.Get(INI_SECTION_TIMER, INI_KEY_TIMER_ENTRY).ToString();
            exitTimerName = _ini.Get(INI_SECTION_TIMER, INI_KEY_TIMER_EXIT).ToString();
            _entryTimer = program.GridTerminalSystem.GetBlockWithName(entryTimerName) as IMyTimerBlock;
            _exitTimer = program.GridTerminalSystem.GetBlockWithName(exitTimerName) as IMyTimerBlock;
        }
        _ini.Set(INI_SECTION_TIMER, INI_KEY_TIMER_ENTRY, entryTimerName);
        _ini.Set(INI_SECTION_TIMER, INI_KEY_TIMER_EXIT, exitTimerName);

        string output = _ini.ToString();
        if (output != _controller.CustomData)
        {
            _controller.CustomData = output;
        }
    }

    public void Update()
    {
        bool controlled = _controller.IsUnderControl;
        if (!_firstRun && controlled != _wasControlled) // New state
        {
            if (controlled)
            {
                if (_entryTimer != null)
                    _entryTimer.Trigger();
            }
            else
            {
                if (_exitTimer != null)
                    _exitTimer.Trigger();
            }
        }

        _wasControlled = controlled;
        _firstRun = false;
    }
}

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    GetBlocks();
}

void Main(string arg, UpdateType updateSource)
{
    if (!_isSetup)
        return;

    if ((updateSource & UpdateType.Update10) == 0)
        return;

    foreach (var x in _statusTimers)
    {
        x.Update();
    }

    Echo($"Seat Status Timers Running...\n(Version {VERSION} - {DATE})\n");
    Echo($"Found {_statusTimers.Count} status seats");
}

void GetBlocks()
{
    _ini.Clear();
    if (_ini.TryParse(Me.CustomData))
    {
        _groupName = _ini.Get(INI_SECTION_GENERAL, INI_KEY_GROUP_NAME).ToString(_groupName);
    }
    _ini.Set(INI_SECTION_GENERAL, INI_KEY_GROUP_NAME, _groupName);

    string output = _ini.ToString();
    if (output != Me.CustomData)
    {
        Me.CustomData = output;
    }

    _statusTimers.Clear();
    var group = GridTerminalSystem.GetBlockGroupWithName(_groupName);
    if (group == null)
    {
        _isSetup = false;
        Echo($"ERROR: No group named\n'{_groupName}' found");
        return;
    }
    group.GetBlocksOfType<IMyShipController>(null, CollectBlocks);

    _isSetup = true;
}

bool CollectBlocks(IMyTerminalBlock b)
{
    var sc = (IMyShipController)b;
    _statusTimers.Add(new StatusTimer(sc, this));
    return false;
}
