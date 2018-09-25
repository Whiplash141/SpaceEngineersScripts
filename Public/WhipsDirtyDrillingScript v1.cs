/*
/ //// / Whip's Quick 'n Dirty Drilling Script v1 - 9/24/2018 / //// / 
*/

// This should be the name of a group that contains your drills, pistons, and LCDs.
// Feel free to customize.
const string drillingSystemGroupName = "Drill System";
const double extendSpeed = 0.2; // meters per second
const double retractSpeed = 1.0; // meters per second

// No touchey
bool _isSetup = false;
bool _shouldDrill = false;
List<IMyTerminalBlock> _groupBlocks = new List<IMyTerminalBlock>();
List<IMyPistonBase> _pistons = new List<IMyPistonBase>();
List<IMyShipDrill> _drills = new List<IMyShipDrill>();
List<IMyTextPanel> _textPanels = new List<IMyTextPanel>();

void Main(string argument, UpdateType updateSource)
{
    #region Argument Handling
    switch (argument.ToLowerInvariant())
    {
        case "start":
            _shouldDrill = true;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            _isSetup = false;
            break;
        case "stop":
            _shouldDrill = false;
            break;
        case "toggle":
            _shouldDrill = !_shouldDrill;
            if (_shouldDrill)
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
                _isSetup = false;
            }
            break;
    }
    #endregion

    // If in an update loop
    if ((Runtime.UpdateFrequency & UpdateFrequency.Update10) != 0)
    {
        if (!_isSetup)
            _isSetup = GrabBlocks();

        // If grabbing blocks failed, stop execution here
        if (!_isSetup)
            return;

        double currentExtension;
        double maxExtension;
        double minExtension;
        GetPistonExtensions(_pistons, out currentExtension, out maxExtension, out minExtension);

        if (_shouldDrill)
        {
            ToggleDrillPower(_drills, true);
            SetPistonVelocity(_pistons, extendSpeed);

            if (currentExtension == maxExtension)
                _shouldDrill = false;
        }
        else
        {
            ToggleDrillPower(_drills, false);
            SetPistonVelocity(_pistons, -retractSpeed);

            if (currentExtension == minExtension)
                Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        WriteToTextPanels(_textPanels, currentExtension, maxExtension, minExtension, _shouldDrill);
    }
}

bool GrabBlocks()
{
    // Get group
    var group = GridTerminalSystem.GetBlockGroupWithName(drillingSystemGroupName);
    if (group == null)
    {
        Echo($"Error: No group with name '{drillingSystemGroupName}'");
        return false;
    }

    // Get group blocks
    group.GetBlocks(_groupBlocks);

    // Clear old lists
    _pistons.Clear();
    _drills.Clear();
    _textPanels.Clear();

    // Sort through group blocks
    foreach (var block in _groupBlocks)
    {
        var piston = block as IMyPistonBase;
        if (piston != null)
        {
            _pistons.Add(piston);
            continue;
        }

        var drill = block as IMyShipDrill;
        if (drill != null)
        {
            _drills.Add(drill);
            continue;
        }

        var textPanel = block as IMyTextPanel;
        if (textPanel != null)
        {
            _textPanels.Add(textPanel);
            continue;
        }
    }

    if (_drills.Count == 0)
    {
        Echo("Error: No drills found in group");
        return false;
    }

    if (_pistons.Count == 0)
    {
        Echo("Error: No pistons found in group");
        return false;
    }

    if (_textPanels.Count == 0)
    {
        Echo("Info: No text panels found in group");
    }

    return true;
}

void ToggleDrillPower(List<IMyShipDrill> drills, bool toggleOn)
{
    foreach (IMyShipDrill block in drills)
    {
        block.Enabled = toggleOn;
    }
}

void SetPistonVelocity(List<IMyPistonBase> pistons, double velocity)
{
    foreach (IMyPistonBase block in pistons)
    {
        block.Velocity = (float)velocity;
    }
}

void GetPistonExtensions(List<IMyPistonBase> pistons, out double currentExtension, out double maxExtension, out double minExtension)
{
    // Sum up total piston extensions
    currentExtension = 0;
    maxExtension = 0;
    minExtension = 0;
    foreach (IMyPistonBase block in pistons)
    {
        currentExtension += block.CurrentPosition;
        maxExtension += block.HighestPosition;
        minExtension += block.LowestPosition;
    }
}

void WriteToTextPanels(List<IMyTextPanel> textPanels, double currentExtension, double maxExtension, double minExtension, bool shouldDrill)
{
    string status = shouldDrill ? "Drilling..." : currentExtension == maxExtension ? "Retracted" : "Retracting...";
    string progress = shouldDrill ? $"{currentExtension / maxExtension:n0}%" : $"{(maxExtension - currentExtension) / (maxExtension - minExtension):n0}%";

    string output = $"Status: {status}\nProgress: {currentExtension:000.00} meters ({progress})";

    foreach (IMyTextPanel block in textPanels)
    {
        block.WritePublicText(output);
        if (!block.ShowText)
            block.ShowPublicTextOnScreen();
    }
}
