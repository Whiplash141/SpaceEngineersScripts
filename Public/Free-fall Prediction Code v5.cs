/*
/ //// / Whip's Free-fall Prediction Code v5 - 5/22/18 / //// /

Setup:
1. Put this code on your ship
2. Make a group named "Freefall" with the following blocks:
    * A cockpit or remote control
    * A rotor
    * A camera on that rotor
    * (Optional) A text panel

*/
string groupName = "Freefall";

//============================================
///////////// No Touch Below /////////////////
//============================================

const double estimateTimeStep = 1d / 60d;
VectorIntegrator distanceIntegrator;
int updateCount = 6;
bool isSetup = false;

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Once;
    Echo("If you can read this\nclick the 'Run' button!");
}

void Main(string arg, UpdateType updateSource)
{
    //------------------------------------------
    //This is a bandaid
    if ((Runtime.UpdateFrequency & UpdateFrequency.Update1) == 0)
        Runtime.UpdateFrequency = (UpdateFrequency.Update1 | UpdateFrequency.Update100);
    //------------------------------------------
    
    if ((updateSource & UpdateType.Update100) != 0)
        updateCount++;
    
    if (updateCount >= 6 || !isSetup)
    {
        isSetup = GrabBlocks();
        updateCount = 0;
    }
    
    if ((updateSource & UpdateType.Update1) == 0)
        return;
    
    Echo($"Whip's Freefall Prediction\n Script {RunningSymbol()}");
    Echo("\nLast setup results:");
    Echo(setupSB.ToString());
    
    if (!isSetup)
        return;
    
    FreefallPrediction();
}

void FreefallPrediction()
{
    var gravityVec = reference.GetNaturalGravity();
    if (Vector3D.IsZero(gravityVec))
    {
        Echo("No gravity");
        return;
    }

    var gravityVecNorm = Vector3D.Normalize(gravityVec);

    double altitude = -69;
    reference.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude);

    var initialVelocity = reference.GetShipVelocities().LinearVelocity;

    distanceIntegrator = new VectorIntegrator(initialVelocity, estimateTimeStep);

    int numberOfIterations = 0;

    var displacementVec = Vector3D.Zero;
    var lastVelocity = initialVelocity;
    for (int i = 0; i < 5000; i++)
    {
        var nextVelocity = PredictNextVelocity(lastVelocity, gravityVec, estimateTimeStep);
        displacementVec = distanceIntegrator.Integrate(nextVelocity);
        numberOfIterations = i;
        lastVelocity = nextVelocity;

        if (displacementVec.Dot(gravityVecNorm) >= altitude)
        {
            break;
        }
    }

    var upVec = -gravityVecNorm;
    var leftVec = Vector3D.Cross(upVec, reference.WorldMatrix.Forward);
    var forwardVec = Vector3D.Cross(leftVec, upVec);
    var matrix = new MatrixD();
    matrix.Up = upVec;
    matrix.Left = leftVec;
    matrix.Forward = forwardVec;
    
    var verticalDistance = displacementVec.Dot(gravityVecNorm);
    var horizontalDistance = verticalDistance * verticalDistance >= displacementVec.LengthSquared() ? 0 : Math.Sqrt(displacementVec.LengthSquared() - verticalDistance * verticalDistance);
    var gunsightAngle = Math.Atan(verticalDistance / horizontalDistance);
    
    var gunsightVector = VectorAzimuthElevation(0, -gunsightAngle, matrix);
    
    var angleDiff = VectorMath.AngleBetween(camera.WorldMatrix.Forward, gunsightVector) * Math.Sign(Vector3D.Cross(camera.WorldMatrix.Forward, gunsightVector).Dot(reference.WorldMatrix.Right));
    
    angleDiff = Math.Round(angleDiff, 2) * Math.Sign(leftVec.Dot(rotor.WorldMatrix.Up));
    
    rotor.TargetVelocityRPM = 20f * (float)angleDiff;
    
    WriteToTextPanel($"Altitude: {altitude:N2}\nLength:{displacementVec.Length():N2}\nVertical Disp: {verticalDistance:N2}\nHorizontalDisp: {horizontalDistance:N2}\nIteratons: {numberOfIterations}\n Drop angle: {MathHelper.ToDegrees(gunsightAngle):N2}°");
}


IMyShipController reference = null;
IMyMotorStator rotor = null;
IMyCameraBlock camera = null;
List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
List<IMyTextPanel> listScreens = new List<IMyTextPanel>();
StringBuilder setupSB = new StringBuilder();

bool GrabBlocks()
{
    setupSB.Clear();
    var group = GridTerminalSystem.GetBlockGroupWithName(groupName);
    if (group == null)
    {
        setupSB.AppendLine($">Error: No group named '{groupName}'");
        return false;
    }
    
    group.GetBlocksOfType(listScreens);
    rotor = GetFirstBlockOfType<IMyMotorStator>("", group);
    camera = GetFirstBlockOfType<IMyCameraBlock>("", group);
    reference = GetFirstBlockOfType<IMyShipController>("", group);
    
    bool passedSetup = true;
    if (reference == null)
    {
        setupSB.AppendLine(">Error: No ship controller in group");
        passedSetup = false;
    }
    
    if (camera == null)
    {
        setupSB.AppendLine(">Error: No camera in group");
        passedSetup = false;
    }
    
    if (rotor == null)
    {
        setupSB.AppendLine(">Error: No rotor in group");
        passedSetup = false;
    }
    
    if (listScreens.Count == 0)
    {
        setupSB.AppendLine(">Warning: No screens in group");
    }
    
    if (passedSetup)
        setupSB.AppendLine(">Setup Successful!");
    else
        setupSB.AppendLine(">Setup Dailed!");
    
    return passedSetup;
}

//Whip's PredictNextVelocity Method v1 - 8/16/17
Vector3D PredictNextVelocity(Vector3D velocityVec, Vector3D accelerationVec, double timeStep, double maxSpeed = 104.38)
{
    var nextVelocityVec = velocityVec + timeStep * accelerationVec;

    double speed = nextVelocityVec.Length();

    if (speed >= maxSpeed)
    {
        nextVelocityVec *= maxSpeed / speed;
    }

    return nextVelocityVec;
}

class VectorIntegrator
{
    Vector3D _lastVector = new Vector3D(0, 0, 0);
    public Vector3D IntegralVector { get; private set; }
    public double TimeStep { get; private set; }

    public VectorIntegrator(Vector3D initialVector, double timeStep)
    {
        _lastVector = initialVector;
        IntegralVector = Vector3D.Zero;
        TimeStep = timeStep;
    }

    public Vector3D Integrate(Vector3D currentVector)
    {
        IntegralVector += (currentVector + _lastVector) / 2 * TimeStep;
        _lastVector = currentVector;
        return IntegralVector;
    }

    public void Clear()
    {
        IntegralVector = Vector3D.Zero;
        _lastVector = Vector3D.Zero;
    }
}

class VectorMath
{
    /// <summary>
    /// Reflects vector a over vector b with an optional rejection factor
    /// </summary>
    public static Vector3D Reflection(Vector3D a, Vector3D b, double rejectionFactor = 1) //reflect a over b    
    {
        Vector3D project_a = Projection(a, b);
        Vector3D reject_a = a - project_a;
        return project_a - reject_a * rejectionFactor;
    }

    /// <summary>
    /// Rejects vector a on vector b
    /// </summary>
    public static Vector3D Rejection(Vector3D a, Vector3D b) //reject a on b    
    {
        if (Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a - a.Dot(b) / b.LengthSquared() * b;
    }

    /// <summary>
    /// Projects vector a onto vector b
    /// </summary>
    public static Vector3D Projection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a.Dot(b) / b.LengthSquared() * b;  
    }
    
    /// <summary>
    /// Scalar projection of a onto b
    /// </summary>
    public static double ScalarProjection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(b))
            return 0;
        
        if (Vector3D.IsUnit(ref b))
            return a.Dot(b);

        return a.Dot(b) / b.Length();  
    }

    /// <summary>
    /// Computes angle between 2 vectors
    /// </summary>
    public static double AngleBetween(Vector3D a, Vector3D b, bool useSmallestAngle = false) //returns radians 
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;
        else if (useSmallestAngle)
            return Math.Acos(MathHelper.Clamp(Math.Abs(a.Dot(b)) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        else
            return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
    }
}

//Whip's Vector from Elevation and Azimuth v6
Vector3D VectorAzimuthElevation(double az, double el, MatrixD worldMatrix)
{   
    el = el % (2 * Math.PI);
    az = az % (2 * Math.PI);

    if (az != Math.Abs(az))
    {
        az = 2 * Math.PI + az;
    }

    int x_mult = 1;

    if (az > Math.PI / 2 && az < Math.PI)
    {
        az = Math.PI - (az % Math.PI);
        x_mult = -1;
    }
    else if (az > Math.PI && az < Math.PI * 3 / 2)
    {
        az = 2 * Math.PI - (az % Math.PI);
        x_mult = -1;
    }

    double x; double y; double z;

    if (el == Math.PI / 2)
    {
        x = 0;
        y = 0;
        z = 1;
    }
    else if (az == Math.PI / 2)
    {
        x = 0;
        y = 1;
        z = y * Math.Tan(el);
    }
    else {
        x = 1 * x_mult;
        y = Math.Tan(az);
        double v_xy = Math.Sqrt(1 + y * y);
        z = v_xy * Math.Tan(el);
    }
    
    return worldMatrix.Forward * x + worldMatrix.Left * y + worldMatrix.Up * z;
}

void WriteToTextPanel(string textToWrite, bool append = false)
{
    if (listScreens.Count == 0)
    {
        Echo("Error: No text panels found");
        return;
    }
    else
    {
        for (int i = 0; i < listScreens.Count; i++)
        {
            var thisScreen = listScreens[i] as IMyTextPanel;
            if (thisScreen != null)
            {
                thisScreen.WritePublicText(textToWrite, append);
                thisScreen.ShowPublicTextOnScreen();
            }
        }
    }
}

T GetFirstBlockOfType<T>(string filterName = "", IMyBlockGroup group = null) where T : class, IMyTerminalBlock
{
    var blocks = new List<T>();
    if (group != null)
    {
        if (filterName == "")
            group.GetBlocksOfType(blocks);
        else
            group.GetBlocksOfType(blocks, x => x.CustomName.Contains(filterName));
    }
    else
    {
        if (filterName == "")
            GridTerminalSystem.GetBlocksOfType(blocks);
        else
            GridTerminalSystem.GetBlocksOfType(blocks, x => x.CustomName.Contains(filterName));
    }

    return blocks.Count > 0 ? blocks[0] : null;
}

//Whip's Running Symbol Method v8
//•
int runningSymbolVariant = 0;
int runningSymbolCount = 0;
const int increment = 10;
string[] runningSymbols = new string[] {"−", "\\", "|", "/"};

string RunningSymbol()
{
    if (runningSymbolCount >= increment)
    {
        runningSymbolCount = 0;
        runningSymbolVariant++;
        if (runningSymbolVariant >= runningSymbols.Length)
            runningSymbolVariant = 0;
    }
    runningSymbolCount++;
    return runningSymbols[runningSymbolVariant];
}