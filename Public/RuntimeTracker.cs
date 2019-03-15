/// <summary>
/// Class that tracks runtime history.
/// </summary>
public class RuntimeTracker
{
    public int Capacity { get; set; }
    public double Sensitivity { get; set; }
    public double MaxRuntime {get; private set;}
    public double MaxInstructions {get; private set;}
    public double AverageRuntime {get; private set;}
    public double AverageInstructions {get; private set;}
    
    private readonly Queue<double> _runtimes = new Queue<double>();
    private readonly Queue<double> _instructions = new Queue<double>();
    private readonly StringBuilder _sb = new StringBuilder();
    private readonly int _instructionLimit;
    private readonly Program _program;

    public RuntimeTracker(Program program, int capacity = 100, double sensitivity = 0.01)
    {
        _program = program;
        Capacity = capacity;
        Sensitivity = sensitivity;
        _instructionLimit = _program.Runtime.MaxInstructionCount;
    }

    public void AddRuntime()
    {
        double runtime = _program.Runtime.LastRunTimeMs;
        AverageRuntime = Sensitivity * (runtime - AverageRuntime) + AverageRuntime;
        
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
        _sb.Clear();
        _sb.AppendLine("\n_____________________________\nGeneral Runtime Info\n");
        _sb.AppendLine($"Avg instructions: {AverageInstructions:n2}");
        _sb.AppendLine($"Max instructions: {MaxInstructions:n0}");
        _sb.AppendLine($"Avg complexity: {MaxInstructions / _instructionLimit:0.000}%");
        _sb.AppendLine($"Avg runtime: {AverageRuntime:n4} ms");
        _sb.AppendLine($"Max runtime: {MaxRuntime:n4} ms");
        return _sb.ToString();
    }
}