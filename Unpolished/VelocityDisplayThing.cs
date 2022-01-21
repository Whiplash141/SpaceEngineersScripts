/*
** You can change these to what you want
*/
const string ShipControllerName = "My seat name";
const string TextPanelName = "My text panel name";

/*
** DONT TOUCH ANYTHING BELOW UNLESS YOU KNOW
**         WHAT YOU ARE DOING
*/

IMyShipController _controller = null;
IMyTextPanel _textPanel = null;
bool _isSetup = false;
StringBuilder _output = new StringBuilder();

Program()
{
    Runtime.UpdateFrequency = (UpdateFrequency.Update10 | UpdateFrequency.Update100);
}

void Main(string arg, UpdateType updateSource)
{
    if (!_isSetup && (updateSource & UpdateType.Update100) == 0)
    {
        _isSetup = Setup();
    }

    if ((updateSource & UpdateType.Update10) == 0)
        return;

    Echo("Local Velocity Thingy Running");

    if (!_isSetup)
        return;

    Vector3D worldVelocity = _controller.GetShipVelocities().LinearVelocity;
    Vector3D localVelocity = Vector3D.Rotate(worldVelocity, _controller.WorldMatrix);

    _textPanel.Alignment = TextAlignment.CENTER;
    _textPanel.ContentType = ContentType.TEXT_AND_IMAGE;
    _output.Clear();
    _output.AppendLine("Velocity");
    _output.AppendLine($"Forward: {-localVelocity.Z:000.0}");
    _output.AppendLine($"Right: {localVelocity.X:000.0}");
    _output.AppendLine($"Up: {localVelocity.Y:000.0}");

    _textPanel.WriteText(_output);
}

bool Collect(IMyTerminalBlock x)
{
    var controller = x as IMyShipController;
    if (controller != null && controller.CustomName.Contains(ShipControllerName))
        _controller = controller;

    var text = x as IMyTextPanel;
    if (text != null && text.CustomName.Contains(TextPanelName))
        _textPanel = text;

    return false;
}
bool Setup()
{
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, Collect);

    bool goodToGo = true;
    if (_controller == null)
    {
        goodToGo = false;
        Echo($"ERROR: No ship controller named\n  '{ShipControllerName}'\n");
    }

    if (_textPanel == null)
    {
        goodToGo = false;
        Echo($"ERROR: No text panel named\n  '{TextPanelName}'\n");
    }

    return goodToGo;
}
