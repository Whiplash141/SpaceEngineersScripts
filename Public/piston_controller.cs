/*
/ //// / Whip's Piston Controller  / //// /
v1.0.2 - 05/20/2020 
______________________________________________________________
SETUP:
1) Place this code in a programmable block
2) That is all

______________________________________________________________
INSTRUCTIONS:
Run the code with the following argument syntax:

"<piston name>" <command> "<extension>"
        -- OR --
"<piston name>" <command> "<extension>" "<speed>"

Where:
    <piston name>
        The name of the piston. Names are case insensitive.
        Names NEED quotation marks around them. (" ")

    <command>
        One of the commands listed below (either set or increment)

    <extension>
        The extension you want in meters. Use quotes around the number!

    <speed> 
        The extension speed of the piston. This parameter is OPTIONAL.
        Use quotes around the number.

______________________________________________________________
COMMANDS:

set
    Sets the piston position to the specified <extension>.

increment
    increments current piston position by the specified <extension>.

______________________________________________________________
Examples:

"Piston 1" set "2"
    Sets EVERY piston with name "Piston 1" to 2 meter extension with default
    spee

 "Piston 1" set "2" "4"
    Sets EVERY piston with name "Piston 1" to 2 meter extension with speed
    gain of 4.

"Potato" increment "-1.5"
    Decreases position of EVERY piston with "Potato" in its name by 1.5
    meters using default speed.

*/

const float MAX_PISTON_SPEED = 5f;
const float EXTENSION_GAIN = 5f;

Dictionary<IMyPistonBase, PistonController> activePistonControllers = new Dictionary<IMyPistonBase, PistonController>();
List<IMyPistonBase> pistonsToRemove = new List<IMyPistonBase>();
List<IMyPistonBase> pistons = new List<IMyPistonBase>();
MyCommandLine cmdLine = new MyCommandLine();
enum MovementMode { None, Set, Increment }

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.None;
    Echo("Whip's Piston Controller");
    Echo("\nStatus:\n>>Inactive<<");
}

void Main(string arg, UpdateType updateType)
{
    Echo("Whip's Piston Controller");

    if ((updateType & (UpdateType.Trigger | UpdateType.Terminal | UpdateType.Script)) != 0 && !string.IsNullOrWhiteSpace(arg)) //checks if update source is from user
    {
        ParseArguments(arg);
    }

    Echo("\nStatus:");

    if (activePistonControllers.Count > 0)
    {
        Echo(">>Active<<");
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
    }
    else
    {
        Echo(">>Inactive<<");
        Runtime.UpdateFrequency = UpdateFrequency.None;
    }

    if ((updateType & UpdateType.Update10) == 0) //ignore the brunt of the logic if the 
        return;

    pistonsToRemove.Clear();
    foreach (var pistonControllerPair in activePistonControllers)
    {
        IMyPistonBase piston = pistonControllerPair.Key;
        PistonController controller = pistonControllerPair.Value;
        controller.ExtendPiston();
        if (controller.IsDone)
        {
            pistonsToRemove.Add(piston);
        }
    }

    foreach(var piston in pistonsToRemove)
    {
        activePistonControllers.Remove(piston);
    }
}

void ParseArguments(string argument)
{
    MovementMode movementMode = MovementMode.None;

    cmdLine.TryParse(argument);

    if (cmdLine.ArgumentCount < 3)
    {
        Echo($">> Error: Not enough arguments!\nArguments must be in the form of:\n    \"<piston name>\" <command>\n    \"<value>\" \"<speed>\"\n(Speed is optional)");
        return;
    }

    var pistonName = cmdLine.Argument(0);

    pistons.Clear();
    GridTerminalSystem.GetBlocksOfType(pistons, block => block.CustomName.IndexOf(pistonName, StringComparison.OrdinalIgnoreCase) >= 0);

    if (pistons.Count == 0)
    {
        Echo($">> Error: No piston named '{pistonName}' were found!");
        return;
    }

    switch (cmdLine.Argument(1))
    {
        case "set":
            movementMode = MovementMode.Set;
            break;

        case "increment":
            movementMode = MovementMode.Increment;
            break;

        default:
            movementMode = MovementMode.None;
            Echo($">> Error: Second argument '{cmdLine.Argument(1)}'\nis not an accepted command!\nAccepted commands are:\n  set\n  increment");
            return;
    }

    float extension = 0f;
    if (!float.TryParse(cmdLine.Argument(2), out extension))
    {
        Echo($">> Error: Third argument '{cmdLine.Argument(2)}'\nis not an number!");
        return;
    }

    float speed = EXTENSION_GAIN;
    if (cmdLine.ArgumentCount >= 4)
    {
        if (!float.TryParse(cmdLine.Argument(3), out speed))
        {
            speed = EXTENSION_GAIN;
            Echo($">> Warning: Fourth argument '{cmdLine.Argument(3)}'\nis not an number!\nUsing default speed.");
        }
    }
    
    speed = Math.Abs(speed);

    foreach (var block in pistons)
    {
        PistonController controller;
        if (!activePistonControllers.TryGetValue(block, out controller))
        {
            controller = new PistonController(block);
            activePistonControllers[block] = controller; // Classes are accessed via reference so we only need to add for new entries
        }

        controller.ExtensionSpeedGain = speed;
        switch (movementMode)
        {
            case MovementMode.Set:
                controller.SetExtension(extension);
                break;
            case MovementMode.Increment:
                controller.IncrementExtension(extension);
                break;
        }
    }
}

class PistonController
{
    public IMyPistonBase Piston { get; private set; } = null;
    public float ExtensionSpeedGain = 10f;
    float _initialExtension;
    float _targetExtension;
    float _epsilon;
    public bool IsDone { get; private set; } = false;

    public PistonController(IMyPistonBase piston, float epsilon = 0.01f)
    {
        Piston = piston;
        _initialExtension = Piston.CurrentPosition;
        _targetExtension = Piston.CurrentPosition;
        _epsilon = epsilon;
    }

    public void IncrementExtension(float extension)
    {
        _targetExtension = MathHelper.Clamp(extension + _targetExtension, Piston.LowestPosition, Piston.HighestPosition);
    }

    public void SetExtension(float extension)
    {
        _targetExtension = MathHelper.Clamp(extension, Piston.LowestPosition, Piston.HighestPosition);
    }

    public void ExtendPiston()
    {
        float err = _targetExtension - Piston.CurrentPosition;

        if (Math.Abs(err) < _epsilon)
        {
            Piston.Velocity = 0;
            this.IsDone = true;
            return;
        }

        Piston.Velocity = MathHelper.Clamp(ExtensionSpeedGain * err, -MAX_PISTON_SPEED, MAX_PISTON_SPEED);
    }
}
