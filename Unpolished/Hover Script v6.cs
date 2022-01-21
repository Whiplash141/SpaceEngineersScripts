//Whip's Hover Script v6 - 8/27/17 
 
double hoverAltitude = 20; 
double descentSpeed = 10; 
 
bool userDefinedPIDGains = false; 
double proportionalGain = 10; 
double integralGain = 0; 
double derivativeGain = 50; 
double integralLimit = 100; 
 
PID altitudePID; 
PID velocityPID = new PID(5, 0, 2, -10, 10, 1d / 60d); 
bool PIDSet = true; 
bool isSetup = false; 
 
List<IMyShipController> shipControllers = new List<IMyShipController>(); 
List<IMyThrust> thrust = new List<IMyThrust>(); 
List<IMyThrust> upThrust = new List<IMyThrust>(); 
 
Program() 
{ 
    if (userDefinedPIDGains) 
        altitudePID = new PID(proportionalGain, integralGain, derivativeGain, -integralLimit, integralLimit, 1d / 60d); 
    else 
        PIDSet = false; 
 
    isSetup = GrabBlocks(); 
} 
 
bool SetPIDController(IMyShipController reference) 
{ 
    var mass = reference.CalculateShipMass().PhysicalMass; 
    altitudePID = new PID(mass / 250, mass/ 500, mass / 100, 0.25, 1d / 60d); 
    //altitudePID = new PID(100, 0, 50, -100, 100, 1d / 60d); 
 
    return true; 
} 
 
void Main(string arg) 
{ 
    if (!isSetup) 
        isSetup = GrabBlocks(); 
 
    var controller = GetControlledShipController(shipControllers); 
    var velocityVec = controller.GetShipVelocities().LinearVelocity; 
    var mass = controller.CalculateShipMass().PhysicalMass; 
     
    var gravityVec = controller.GetNaturalGravity(); 
    var gravityMagnitude = gravityVec.Length(); 
     
    if (Vector3D.IsZero(gravityVec)) 
    { 
        Echo("Error: No natural gravity found"); 
        return; 
    } 
     
    if (!PIDSet) 
    { 
        PIDSet = SetPIDController(controller); 
    } 
     
    double altitude = 0; 
    controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude); 
 
    var upwardThrustMagnitude = CalculateUpThrust(thrust, controller, gravityVec); 
    var upwardAcceleration = upwardThrustMagnitude / mass; 
     
    var downSpeed = velocityVec.Dot(Vector3D.Normalize(gravityVec)); 
     
    var equillibriumThrust = mass * gravityMagnitude / upwardThrustMagnitude * 100; 
 
    altitudePID.Control(hoverAltitude - altitude); 
    velocityPID.Control(downSpeed - descentSpeed); 
     
    double targetThrust = Math.Max(velocityPID.Value, altitudePID.Value); 
    /*if (altitude > hoverAltitude * 2) 
    { 
 
        targetThrust = equillibriumThrust + velocityPID.Value; 
    } 
    else 
    { 
        targetThrust = equillibriumThrust + altitudePID.Value; 
    }*/ 
 
    foreach (var block in upThrust) 
    { 
        block.SetValue("Override", (float)Math.Max(targetThrust, 1.001)); 
    } 
} 
 
bool GrabBlocks() 
{ 
    GridTerminalSystem.GetBlocksOfType(shipControllers); 
    if (shipControllers.Count == 0) 
    { 
        Echo("Error: No ship controllers found"); 
        return false; 
    } 
 
    GridTerminalSystem.GetBlocksOfType(thrust); 
    if (thrust.Count == 0) 
    { 
        Echo("Error: No thrusters found"); 
        return false; 
    } 
 
    return true; 
} 
 
double CalculateStopAcceleration(double vf, double vi, double d) 
{ 
    //Vf^2 = Vi^2 + 2*a*d 
    return Math.Abs(vf * vf - vi * vi) / (2 * d); 
} 
 
double CalculateUpThrust(List<IMyThrust> thrust, IMyShipController reference, Vector3D gravityVec) 
{ 
    upThrust.Clear(); 
    double thrustSum = 0; 
    double gravityAlignmentCoeff = gravityVec.Dot(reference.WorldMatrix.Down); 
     
    if (gravityAlignmentCoeff <= 0) 
        return -1; 
 
    foreach (var block in thrust) 
    { 
        if (block.WorldMatrix.Backward == reference.WorldMatrix.Up) 
        { 
            upThrust.Add(block); 
            thrustSum += block.MaxEffectiveThrust; 
        } 
    } 
 
    return thrustSum; 
} 
 
IMyShipController GetControlledShipController(List<IMyShipController> SCs) 
{ 
    foreach (IMyShipController thisController in SCs) 
    { 
        if (thisController.IsUnderControl && thisController.CanControlShip) 
            return thisController; 
    } 
 
    return SCs[0]; 
} 
 
//Whip's PID controller class v3 - 8/4/17 
public class PID 
{ 
    double _kP = 0; 
    double _kI = 0; 
    double _kD = 0; 
    double _integralDecayRatio = 0; 
    double _lowerBound = 0; 
    double _upperBound = 0; 
    double _timeStep = 0; 
    double _errorSum = 0; 
    double _lastError = 0; 
    bool _firstRun = true; 
    bool _integralDecay = false; 
    public double Value {get; private set;} 
 
    public PID(double kP, double kI, double kD, double lowerBound, double upperBound, double timeStep) 
    { 
        _kP = kP; 
        _kI = kI; 
        _kD = kD; 
        _lowerBound = lowerBound; 
        _upperBound = upperBound; 
        _timeStep = timeStep; 
        _integralDecay = false; 
    } 
     
    public PID(double kP, double kI, double kD, double integralDecayRatio, double timeStep) 
    { 
        _kP = kP; 
        _kI = kI; 
        _kD = kD; 
        _timeStep = timeStep; 
        _integralDecayRatio = integralDecayRatio; 
        _integralDecay = true; 
    } 
 
    public double Control(double error) 
    { 
        //Compute derivative term 
        var errorDerivative = (error - _lastError) / _timeStep; 
         
        if (_firstRun) 
        { 
            errorDerivative = 0; 
            _firstRun = false; 
        } 
 
        //Compute integral term 
        if (!_integralDecay) 
        { 
            _errorSum += error * _timeStep; 
 
            //Clamp integral term 
            if (_errorSum > _upperBound) 
                _errorSum = _upperBound; 
            else if (_errorSum < _lowerBound) 
                _errorSum = _lowerBound; 
        } 
        else 
        { 
            _errorSum = _errorSum * (1.0 - _integralDecayRatio) + error * _timeStep; 
        } 
 
        //Store this error as last error 
        _lastError = error; 
 
        //Construct output 
        this.Value = _kP * error + _kI * _errorSum + _kD * errorDerivative; 
        return this.Value; 
    } 
 
    public void Reset() 
    { 
        _errorSum = 0; 
        _lastError = 0; 
        _firstRun = true; 
    } 
}