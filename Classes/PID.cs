
#region PID Class

/// <summary>
/// Discrete time PID controller class.
/// (Whiplash141 - 11/22/2018)
/// </summary>
public class PID
{
    public double Kp { get; set; } = 0;
    public double Ki { get; set; } = 0;
    public double Kd { get; set; } = 0;
    public double Value { get; private set; }

    double _timeStep = 0;
    double _inverseTimeStep = 0;
    double _errorSum = 0;
    double _lastError = 0;
    bool _firstRun = true;

    public PID(double kp, double ki, double kd, double timeStep)
    {
        Kp = kp;
        Ki = ki;
        Kd = kd;
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
        double errorDerivative = (error - _lastError) * _inverseTimeStep;

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
        Value = Kp * error + Ki * _errorSum + Kd * errorDerivative;
        return Value;
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

    public DecayingIntegralPID(double kp, double ki, double kd, double timeStep, double decayRatio) : base(kp, ki, kd, timeStep)
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

    public ClampedIntegralPID(double kp, double ki, double kd, double timeStep, double lowerBound, double upperBound) : base(kp, ki, kd, timeStep)
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

    public BufferedIntegralPID(double kp, double ki, double kd, double timeStep, int bufferSize) : base(kp, ki, kd, timeStep)
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
