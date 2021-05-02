/*
/ //// / Whip's Ramping Thrust Script - v1.0.0 - 2020/05/02 / //// /

Place this on the same grid as the seat you control the ship from.
This will NOT work with subgrid thrust.
*/

const string IniSection = "Ramping Thrust",
    IniTimeToMaxThrust = "Time to max thrust (sec)";

double _timeToMaxThrust = 2.0;
Vector3D _elapsedTime = Vector3D.Zero;
Vector3I _lastCommand = Vector3I.Zero;
List<IMyShipController> _controllers = new List<IMyShipController>();
List<IMyThrust> _thrusters = new List<IMyThrust>();
MyIni _ini = new MyIni();
RuntimeTracker _runtimeTracker;

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    GridTerminalSystem.GetBlocksOfType(_controllers, b => b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType(_thrusters, b => b.IsSameConstructAs(Me));
    ProcessIni();
    _runtimeTracker = new RuntimeTracker(this);
}

void ProcessIni()
{
    _ini.Clear();
    if (_ini.TryParse(Me.CustomData))
    {
        _timeToMaxThrust = _ini.Get(IniSection, IniTimeToMaxThrust).ToDouble(_timeToMaxThrust);
    }
    else if (!string.IsNullOrEmpty(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    _ini.Set(IniSection, IniTimeToMaxThrust, _timeToMaxThrust);

    string output = _ini.ToString();
    if (output != Me.CustomData)
    {
        Me.CustomData = output;
    }
}

void Main(string arg, UpdateType updateSource)
{
    _runtimeTracker.AddRuntime();
    _elapsedTime += Math.Max(0, Math.Round(Runtime.TimeSinceLastRun.TotalSeconds, 5));

    var controller = GetControllerShipController();
    if (controller == null)
    {
        Echo("No ship controllers!");
        _runtimeTracker.AddInstructions();
        return;
    }

    if (_thrusters.Count == 0)
    {
        Echo("No thrusters!");
        _runtimeTracker.AddInstructions();
        return;
    }

    Echo("Ramping Thrust Running...\n");

    // Process inputs
    Vector3I currentCommand = controller.IsUnderControl ? Vector3I.Sign(controller.MoveIndicator) : Vector3I.Zero;
    if (_lastCommand.X != currentCommand.X)
    {
        _elapsedTime.X = 0;
    }

    if (_lastCommand.Y != currentCommand.Y)
    {
        _elapsedTime.Y = 0;
    }

    if (_lastCommand.Z != currentCommand.Z)
    {
        _elapsedTime.Z = 0;
    }

    _lastCommand = currentCommand;
    Echo($"Current command: {currentCommand}");
    Echo($"Time elapsed: {_elapsedTime.ToString("N2")}");
    Echo($"Thrusters: {_thrusters.Count}");
    Echo($"Controllers: {_controllers.Count}");
    Echo($"Controlled controller:\n> '{controller.CustomName}'");
    Echo($"\n{_runtimeTracker.Write()}");

    // Set thrust override
    Vector3D worldCommand = Vector3D.Rotate(currentCommand, controller.WorldMatrix);
    Vector3D velocity = controller.GetShipVelocities().LinearVelocity;
    Vector3D desiredDampeningForce = -controller.CalculateShipMass().PhysicalMass * (2 * velocity + controller.GetNaturalGravity());
    Vector3D thrustProportion = Vector3D.Clamp(_elapsedTime / _timeToMaxThrust, Vector3D.Zero, Vector3D.One);
    MatrixD controllerWmTpose = MatrixD.Transpose(controller.WorldMatrix);
    foreach (var t in _thrusters)
    {
        Vector3D forwardThrustDirection = t.WorldMatrix.Backward;
        double proportion = 0f;

        if (Vector3D.Dot(worldCommand, forwardThrustDirection) > 0.1)
        {
            Vector3D thrustDirnLocal;
            Vector3D.Rotate(ref forwardThrustDirection, ref controllerWmTpose, out thrustDirnLocal);
            if (Math.Abs(thrustDirnLocal.Z) > 0.9)
                proportion = thrustProportion.Z;
            else if (Math.Abs(thrustDirnLocal.X) > 0.9)
                proportion = thrustProportion.X;
            else if (Math.Abs(thrustDirnLocal.Y) > 0.9)
                proportion = thrustProportion.Y;
            t.ThrustOverridePercentage = Math.Max((float)proportion, 0.000000001f);
        }
        else // Dampening
        {
            double neededThrust = Vector3D.Dot(forwardThrustDirection, desiredDampeningForce);
            if (neededThrust > 0 && Vector3D.Dot(forwardThrustDirection, worldCommand) >= 0)
            {
                float outputProportion = (float)MathHelper.Clamp(neededThrust / t.MaxEffectiveThrust, 0, 1);
                t.ThrustOverridePercentage = outputProportion;
                desiredDampeningForce -= forwardThrustDirection * outputProportion * t.MaxEffectiveThrust;
            }
            else
            {
                t.ThrustOverridePercentage = 0.000000001f;
            }
        }
    }
    _runtimeTracker.AddInstructions();
}

IMyShipController GetControllerShipController()
{
    if (_controllers.Count == 0)
        return null;

    IMyShipController firstControlled = null;
    foreach (var sc in _controllers)
    {
        if (sc.IsUnderControl && sc.CanControlShip && firstControlled == null)
        {
            firstControlled = sc;
        }

        if (sc.IsMainCockpit)
        {
            return sc;
        }
    }
    if (firstControlled != null)
    {
        return firstControlled;
    }
    return _controllers[0];
}

/// <summary>
/// Class that tracks runtime history.
/// </summary>
public class RuntimeTracker
{
    public int Capacity { get; set; }
    public double Sensitivity { get; set; }
    public double MaxRuntime { get; private set; }
    public double MaxInstructions { get; private set; }
    public double AverageRuntime { get; private set; }
    public double AverageInstructions { get; private set; }
    public double LastRuntime { get; private set; }
    public double LastInstructions { get; private set; }

    readonly Queue<double> _runtimes = new Queue<double>();
    readonly Queue<double> _instructions = new Queue<double>();
    readonly int _instructionLimit;
    readonly Program _program;
    const double MS_PER_TICK = 16.6666;

    const string Format = "General Runtime Info\n"
            + "- Avg runtime: {0:n4} ms\n"
            + "- Last runtime: {1:n4} ms\n"
            + "- Max runtime: {2:n4} ms\n"
            + "- Avg instructions: {3:n2}\n"
            + "- Last instructions: {4:n0}\n"
            + "- Max instructions: {5:n0}\n"
            + "- Avg complexity: {6:0.000}%";

    public RuntimeTracker(Program program, int capacity = 100, double sensitivity = 0.005)
    {
        _program = program;
        Capacity = capacity;
        Sensitivity = sensitivity;
        _instructionLimit = _program.Runtime.MaxInstructionCount;
    }

    public void AddRuntime()
    {
        double runtime = _program.Runtime.LastRunTimeMs;
        LastRuntime = runtime;
        AverageRuntime += (Sensitivity * runtime);
        int roundedTicksSinceLastRuntime = (int)Math.Round(_program.Runtime.TimeSinceLastRun.TotalMilliseconds / MS_PER_TICK);
        if (roundedTicksSinceLastRuntime == 1)
        {
            AverageRuntime *= (1 - Sensitivity);
        }
        else if (roundedTicksSinceLastRuntime > 1)
        {
            AverageRuntime *= Math.Pow((1 - Sensitivity), roundedTicksSinceLastRuntime);
        }

        _runtimes.Enqueue(runtime);
        if (_runtimes.Count == Capacity)
        {
            _runtimes.Dequeue();
        }

        MaxRuntime = _runtimes.Max();
    }

    public void AddInstructions()
    {
        double instructions = _program.Runtime.CurrentInstructionCount;
        LastInstructions = instructions;
        AverageInstructions = Sensitivity * (instructions - AverageInstructions) + AverageInstructions;

        _instructions.Enqueue(instructions);
        if (_instructions.Count == Capacity)
        {
            _instructions.Dequeue();
        }

        MaxInstructions = _instructions.Max();
    }

    public string Write()
    {
        return string.Format(Format,
            AverageRuntime,
            LastRuntime,
            MaxRuntime,
            AverageInstructions,
            LastInstructions,
            MaxInstructions,
            AverageInstructions / _instructionLimit);
    }
}
