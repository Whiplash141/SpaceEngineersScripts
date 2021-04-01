/*
/ //// / Whip's Free-fall Prediction Code v9.1 - 2021/02/01 / //// /

Setup:
1. Put this code on your ship
2. Make a group named "Freefall" with the following blocks:
    * 2 rotors arranged like a turret
    * A camera on the top rotor
    * (Optional) A text panel
3. You also need at least one ship controller on the grid, the code will find it
   automagically

*/
const string groupName = "Freefall";
const double forwardEstimationTime = 0.0;
const double maxSpeed = 104.38;
const double gravityMultiplier = 1.0;
const double planetCenterDistance = 59224;
const double rotorRotationSpeed = 10.0;
bool useFixedDistanceFromPlanetCenter = false;

//============================================
///////////// No Touch Below /////////////////
//============================================

int updateCount = 6;
bool isSetup = false;

bool hasHitPosition = false;
Vector3D hitPosition = Vector3D.Zero;

Program()
{
    Runtime.UpdateFrequency = (UpdateFrequency.Update1 | UpdateFrequency.Update100);
}

void Main(string arg, UpdateType updateSource)
{  
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
    
    if (!camera.EnableRaycast)
    {
        camera.EnableRaycast = true;
    }

    if (camera.CanScan(4000))
    {
        var info = camera.Raycast(4000);
        if (!info.IsEmpty())
        {
            hitPosition = info.HitPosition.Value;
            hasHitPosition = true;
        }
    }

    double altitude = -69;
    reference.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude);

    if (useFixedDistanceFromPlanetCenter)
    {
        Vector3D planetCenter;
        if (reference.TryGetPlanetPosition(out planetCenter))
        {
            double distanceFromCenter = Vector3D.Distance(planetCenter, Me.GetPosition());
            altitude = distanceFromCenter - planetCenterDistance;
        }
    }
    else if (hasHitPosition)
    {
        Vector3D toHit = camera.GetPosition() - hitPosition;
        Vector3D vertical = VectorMath.Projection(toHit, gravityVec);
        altitude = vertical.Length();
    }
    
    var initialVelocity = reference.GetShipVelocities().LinearVelocity;
    
    Vector3D forwardEstimateDisplacement = Vector3D.Zero;
    if (forwardEstimationTime > 0 && altitude > 0)
    {
        forwardEstimateDisplacement = initialVelocity * forwardEstimationTime;
        double altitudeAdjustment = forwardEstimateDisplacement.Dot(gravityVecNorm); // Towards ground is positive
        altitude -= altitudeAdjustment;
    }
    
    double groundDistance = GetFreefallGroundDistance(
        initialVelocity, 
        gravityVec * gravityMultiplier, 
        altitude, 
        maxSpeed);
    
    var upVec = azimuth.WorldMatrix.Up;
    var leftVec = elevation.WorldMatrix.Up;
    var forwardVec = Vector3D.Cross(leftVec, upVec);
    var matrix = new MatrixD();
    matrix.Up = upVec;
    matrix.Left = leftVec;
    matrix.Forward = forwardVec;
    
    var lateralDirection = initialVelocity - Vector3D.Dot(initialVelocity, gravityVecNorm) * gravityVecNorm;
    if (!Vector3D.IsZero(lateralDirection))
    {
        lateralDirection = Vector3D.Normalize(lateralDirection);
    }
    
    var impactDirection = altitude * gravityVecNorm + groundDistance * lateralDirection + forwardEstimateDisplacement;
    var forwardDirection = camera.WorldMatrix.Forward;

    double targetYaw = 0, targetPitch = 0, currentPitch = 0, yawSpeed = 0, pitchSpeed = 0;
    GetRotationAngles(ref impactDirection, ref matrix, out targetYaw, out targetPitch);
    GetElevationAngle(ref forwardDirection, ref matrix, out currentPitch);
    
    yawSpeed = rotorRotationSpeed * Math.Round(targetYaw, 2); 
    pitchSpeed = rotorRotationSpeed * Math.Round(currentPitch - targetPitch, 2);
    
    if (Math.Abs(MathHelper.PiOver2 - targetPitch) < 1e-2) // To stop spinning at singularity
    {
        yawSpeed = 0;
    }
    
    azimuth.TargetVelocityRad = -(float)yawSpeed;
    elevation.TargetVelocityRad = -(float)pitchSpeed;
    
    WriteToTextPanel($"Altitude: {altitude:N2}\nGround distance: {groundDistance:N2}\nYaw: {(targetYaw)*180/Math.PI:N2}°\nPitch: {(targetPitch)*180/Math.PI:N2}°"+
    $"\nCurrent Pitch: {(currentPitch)*180/Math.PI:N2}°" + 
    $"\n{lateralDirection.X:n2}" +
    $"\n{lateralDirection.Y:n2}" +
    $"\n{lateralDirection.Z:n2}");
}

// Empirical solution derived from andrukha74#3658 on discord :)
double GetFreefallGroundDistance(Vector3D initialVelocity, Vector3D gravityVector, double altitude, double maxSpeed)
{   
    double gravityMag = gravityVector.Length();
    Vector3D gravityNorm = gravityVector / gravityMag;

    Vector3D forwardVelocity = Vector3D.Dot(initialVelocity, gravityNorm) * gravityNorm;
    Vector3D lateralVelocity = initialVelocity - forwardVelocity;

    double vy = forwardVelocity.Length() * Math.Sign(forwardVelocity.Dot(gravityNorm));
    double vx = lateralVelocity.Length();
    
    double A = gravityMag; // Accel
    double c = maxSpeed; // Max speed
    
    double vy_max = Math.Sqrt((c * c) - (vx * vx));
    double t = (vy_max - vy) / A;
    // vf^2 - vi^2 + 2*a*d
    // d = (vf^2 - vi^2)/(2*a)
    double verticalDistance = (vy_max * vy_max - vy * vy) / (2 * A);
    double horizontalDistance = t * vx;
    double y = altitude - verticalDistance;
    
    double theta = Math.Acos(vx / c);
    double k = MathHelper.PiOver2 - theta;

    return horizontalDistance + (c * c) / A * (k - Math.Asin(vx / c * Math.Exp(-A / (c * c) * y)));
}

static void GetRotationAngles(ref Vector3D targetVector, ref MatrixD matrix, out double yaw, out double pitch)
{
    MatrixD matrixTpose;
    MatrixD.Transpose(ref matrix, out matrixTpose);
    Vector3D localTargetVector;
    Vector3D.TransformNormal(ref targetVector, ref matrixTpose, out localTargetVector);
    Vector3D flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);

    yaw = AngleBetween(ref Vector3D.Forward, ref flattenedTargetVector) * -Math.Sign(localTargetVector.X); //right is positive
    if (Math.Abs(yaw) < 1E-6 && localTargetVector.Z > 0) //check for straight back case
        yaw = Math.PI;

    if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
        pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
    else
        pitch = AngleBetween(ref localTargetVector, ref flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
}

static void GetElevationAngle(ref Vector3D targetVector, ref MatrixD matrix, out double pitch)
{
    MatrixD matrixTpose;
    MatrixD.Transpose(ref matrix, out matrixTpose);
    Vector3D localTargetVector;
    Vector3D.TransformNormal(ref targetVector, ref matrixTpose, out localTargetVector);
    var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);

    if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
        pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
    else
        pitch = AngleBetween(ref localTargetVector, ref flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
}

public static double AngleBetween(ref Vector3D a, ref Vector3D b)
{
    double cosBtwn = CosBetween(ref a, ref b);
    return Math.Acos(cosBtwn);
}

public static double CosBetween(ref Vector3D a, ref Vector3D b)
{
    if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
    {
        return 0;
    }
    double dot;
    Vector3D.Dot(ref a, ref b, out dot);
    return MathHelper.Clamp(dot / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);
}

IMyShipController reference = null;
IMyMotorStator azimuth = null;
IMyMotorStator elevation = null;
IMyCameraBlock camera = null;
List<IMyMotorStator> rotors = new List<IMyMotorStator>();
List<IMyTextPanel> listScreens = new List<IMyTextPanel>();
Dictionary<IMyCubeGrid, IMyMotorStator> rotorGrids = new Dictionary<IMyCubeGrid, IMyMotorStator>();
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
    
    rotorGrids.Clear();
    rotors.Clear();
    listScreens.Clear();
    camera = null;
    elevation = null;
    azimuth = null;
    reference = null;
    
    reference = GetFirstBlockOfType<IMyShipController>("");

    group.GetBlocks(null, (b) => {
        if (b is IMyMotorStator)
        {
            var r = (IMyMotorStator)b;
            rotors.Add(r);
            if (r.IsAttached)
            {
                rotorGrids[r.TopGrid] = r;
            }
            return false;
        }
        if (b is IMyTextPanel)
        {
            listScreens.Add((IMyTextPanel)b);
            return false;
        }
        if (b is IMyCameraBlock)
        {
            camera = (IMyCameraBlock)b;
        }
        return false;
    });
    
    if (camera != null)
    {
        // Sort rotors
        bool exists = rotorGrids.TryGetValue(camera.CubeGrid, out elevation);
        if (exists)
        {
            rotorGrids.TryGetValue(elevation.CubeGrid, out azimuth);
        }    
    }
    
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
    if (azimuth == null)
    {
        setupSB.AppendLine(">Error: No azimuth rotor in group");
        passedSetup = false;
    }
    if (elevation == null)
    {
        setupSB.AppendLine(">Error: No elevation rotor in group");
        passedSetup = false;
    }
    if (listScreens.Count == 0)
    {
        setupSB.AppendLine(">Info: No screens in group");
    }
    
    if (passedSetup)
        setupSB.AppendLine(">Setup Successful!");
    else
        setupSB.AppendLine(">Setup Failed!");
    
    return passedSetup;
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
        Echo("Info: No text panels found");
        return;
    }
    else
    {
        for (int i = 0; i < listScreens.Count; i++)
        {
            var thisScreen = listScreens[i] as IMyTextPanel;
            if (thisScreen != null)
            {
                thisScreen.WriteText(textToWrite, append);
                thisScreen.ContentType = ContentType.TEXT_AND_IMAGE;
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