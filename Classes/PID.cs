#region PID Class

/// <summary>
/// Discrete time PID controller class.
/// (Whiplash141 - 11/22/2018)
/// </summary>
public class PID
{
    readonly double _kP = 0;
    readonly double _kI = 0;
    readonly double _kD = 0;

    double _timeStep = 0;
    double _inverseTimeStep = 0;
    double _errorSum = 0;
    double _lastError = 0;
    bool _firstRun = true;

    public double Value { get; private set; }

    public PID(double kP, double kI, double kD, double timeStep)
    {
        _kP = kP;
        _kI = kI;
        _kD = kD;
        _timeStep = timeStep;
        _inverseTimeStep = 1 / _timeStep;
    }

    protected virtual double GetIntegral(double currentError, double errorSum, double timeStep)
    {
        return errorSum + currentError * timeStep;
    }

    public double Control(double error)
    {
        //Compute derivative term
        var errorDerivative = (error - _lastError) * _inverseTimeStep;

        if (_firstRun)
        {
            errorDerivative = 0;
            _firstRun = false;
        }

        //Get error sum
        _errorSum = GetIntegral(error, _errorSum, _timeStep);

        //Store this error as last error
        _lastError = error;

        //Construct output
        this.Value = _kP * error + _kI * _errorSum + _kD * errorDerivative;
        return this.Value;
    }

    public double Control(double error, double timeStep)
    {
        if (timeStep != _timeStep)
        {
            _timeStep = timeStep;
            _inverseTimeStep = 1 / _timeStep;
        }
        return Control(error);
    }

    public void Reset()
    {
        _errorSum = 0;
        _lastError = 0;
        _firstRun = true;
    }
}

public class DecayingIntegralPID : PID
{
    readonly double _decayRatio;

    public DecayingIntegralPID(double kP, double kI, double kD, double timeStep, double decayRatio) : base(kP, kI, kD, timeStep)
    {
        _decayRatio = decayRatio;
    }

    protected override double GetIntegral(double currentError, double errorSum, double timeStep)
    {
        return errorSum * (1.0 - _decayRatio) + currentError * timeStep;
    }
}

public class ClampedIntegralPID : PID
{
    readonly double _upperBound;
    readonly double _lowerBound;

    public ClampedIntegralPID(double kP, double kI, double kD, double timeStep, double lowerBound, double upperBound) : base (kP, kI, kD, timeStep)
    {
        _upperBound = upperBound;
        _lowerBound = lowerBound;
    }

    protected override double GetIntegral(double currentError, double errorSum, double timeStep)
    {
        errorSum = errorSum + currentError * timeStep;
        return Math.Min(_upperBound, Math.Max(errorSum, _lowerBound));
    }
}

public class BufferedIntegralPID : PID
{
    readonly Queue<double> _integralBuffer = new Queue<double>();
    readonly int _bufferSize = 0;

    public BufferedIntegralPID(double kP, double kI, double kD, double timeStep, int bufferSize) : base(kP, kI, kD, timeStep)
    {
        _bufferSize = bufferSize;
    }

    protected override double GetIntegral(double currentError, double errorSum, double timeStep)
    {
        if (_integralBuffer.Count == _bufferSize)
            _integralBuffer.Dequeue();
        _integralBuffer.Enqueue(currentError * timeStep);
        return _integralBuffer.Sum();
    }
}

#endregion