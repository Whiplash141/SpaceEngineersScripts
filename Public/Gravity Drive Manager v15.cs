/*
/// Whip's Directional Gravity Drive Control Script v15 - 12/20/17 ///
________________________________________________
Description:

    This code allows you to control a gravity drive through normal movement keys.
    The setup is INCREDIBLY simple!

    DISCLAIMER: This code is NOT made for planerary flight as grav drives do not work in natural gravity.
________________________________________________
How do I use this?

    1) Place this program on your main grid (the grid your control seat is on)

    2) Make a timer block with actions:
    - "Run" this program with NO ARGUMENT
    - "Trigger Now" itself 
    - "Start" itself 

    3) Make a group with all of your gravity drive artificial masses and gravity gens. Name it "Gravity Drive"

    4) Trigger the timer

    5) Enjoy!
________________________________________________
Arguments

    on : Turns grav drive on
    off : Turns grav drive off
    toggle : toggles grav drive
    dampeners_on: turns gravity dampeners on
    dampeners_off: turns gravity dampeners off
    dampeners_toggle: toggles gravity dampeners
________________________________________________
Author's Notes

    This code was written pon request of my friend Avalash for his big cool carrier thing. I've decided to polish this code
    and release it to the public. Leave any questions, comments, or converns on the workshop page!

- Whiplash141
*/

const string gravityDriveGroupName = "Gravity Drive";
//place all gravity drive generators and masses in this group

float gravityDriveDampenerScalingFactor = 0.01f;
//larger values will quicken the dampening using gravity gens but will also risk causing oscillations
//The lighter your ship, the smaller this should be

double speedThreshold = 0.01;
//Speed at which the code will stop using gravity drives. Zero means that 
//the drive will never turn off (useful for ships with no thrusters)

bool useGyrosToStabilize = true;
//If the script will override gyros to try and combat torque

bool useGravityDriveAsInertialDampeners = true;
//If the code should treat the gravity drive as an inertial dampeners.
//If no thrusters are found on the ship, this variable is used to determine
//if inertial dampeners should be turned on/off.

bool enableDampenersWhenNotControlled = true;
//This will force gravity dampeners on if no one is controlling the ship

const double fullBurnToleranceAngle = 30;
//Max angle (in degrees) that a thruster can be off axis of input direction and still
//receive maximum thrust output

const double maxThrustAngle = 90;
//Max angle (in degrees) that a thruster can deviate from the desired travel direction 
//and still be controlled with movement keys

const double minDampeningAngle = 75;
//Minimum angle (in degrees) between a thruster's dampening direction and desired move direction
//that is allowed for dampener function

//-------------------------------------------------------------------------
//============ NO TOUCH BELOW HERE!!! =====================================
//-------------------------------------------------------------------------

const double updatesPerSecond = 10;
const double timeMaxCycle = 1 / updatesPerSecond;
//const float stepRatio = 0.1f; //this is the ratio of the max acceleration to add each code cycle
double timeCurrentCycle = 0;

const double refreshInterval = 10;
double refreshTime = 141;
double maxThrustDotProduct = Math.Cos(maxThrustAngle * Math.PI / 180);
double minDampeningDotProduct = Math.Cos(minDampeningAngle * Math.PI / 180);
double fullBurnDotProduct = Math.Cos(fullBurnToleranceAngle * Math.PI / 180);

bool turnOn = true;
bool isSetup = false;

Vector3D lastForwardVector = Vector3D.Zero;
bool hasVector = false;

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

void Main(string arg, UpdateType updateType)
{
    if ((updateType & (UpdateType.Trigger | UpdateType.Terminal | UpdateType.Script)) != 0)
        ProcessArguments(arg);


    if ((updateType & UpdateType.Update1) == 0)
        return;

    timeCurrentCycle += 1.0 / 60.0;
    refreshTime += 1.0 / 60.0;

    try
    {
        if (!isSetup || refreshTime >= refreshInterval)
        {
            isSetup = GrabBlocks();
            refreshTime = 0;
        }

        if (!isSetup)
            return;

        if (timeCurrentCycle >= timeMaxCycle)
        {
            Echo("WMI Gravity Drive Manager... " + RunningSymbol());

            Echo($"Next refresh in {Math.Max(refreshInterval - refreshTime, 0):N0} seconds");

            if (turnOn)
                Echo("\nGravity Drive is Enabled");
            else
                Echo("\nGravity Drive is Disabled");

            if (useGravityDriveAsInertialDampeners)
                Echo("\nGravity Dampeners Enabled");
            else
                Echo("\nGravity Dampeners Disabled");

            Echo($"\nGravity Drive Stats:\n Artificial Masses: {artMasses.Count}\n Gravity Generators:\n >Forward: {fowardGens.Count}\n >Backward: {backwardGens.Count}\n >Left: {leftGens.Count}\n >Right: {rightGens.Count}\n >Up: {upGens.Count}\n >Down: {downGens.Count}\n >Other: {otherGens.Count}");

            Echo($"\nGyro Stabilization: {useGyrosToStabilize}\n Gyro count: {gyros.Count}");

            ManageGravDrive(turnOn);
            timeCurrentCycle = 0;

            runningSymbolVariant++;
        }
    }
    catch
    {
        isSetup = false;
    }
}

void ProcessArguments(string arg)
{
    switch (arg.ToLower())
    {
        case "on":
            turnOn = true;
            break;

        case "off":
            turnOn = false;
            break;

        case "toggle":
            turnOn = !turnOn; //switches boolean value
            break;

        case "dampeners_on":
            useGravityDriveAsInertialDampeners = true;
            break;

        case "dampeners_off":
            useGravityDriveAsInertialDampeners = false;
            break;

        case "dampeners_toggle":
            useGravityDriveAsInertialDampeners = !useGravityDriveAsInertialDampeners;
            break;
    }
}

//Whip's Running Symbol Method v6
int runningSymbolVariant = 0;
string RunningSymbol()
{
    string strRunningSymbol = "";

    if (runningSymbolVariant == 0)
        strRunningSymbol = "|";
    else if (runningSymbolVariant == 1)
        strRunningSymbol = "/";
    else if (runningSymbolVariant == 2)
        strRunningSymbol = "--";
    else if (runningSymbolVariant == 3)
    {
        strRunningSymbol = "\\";
        runningSymbolVariant = 0;
    }

    return strRunningSymbol;
}

List<IMyShipController> shipControllers = new List<IMyShipController>();
List<IMyGravityGenerator> gravityGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> upGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> downGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> leftGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> rightGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> fowardGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> backwardGens = new List<IMyGravityGenerator>();
List<IMyGravityGenerator> otherGens = new List<IMyGravityGenerator>();
List<List<IMyGravityGenerator>> gravityList = new List<List<IMyGravityGenerator>>();
List<IMyVirtualMass> artMasses = new List<IMyVirtualMass>();
List<IMyThrust> onGridThrust = new List<IMyThrust>();
List<IMyGyro> gyros = new List<IMyGyro>();
IMyBlockGroup gravityDriveGroup = null;

bool GrabBlocks()
{
    shipControllers.Clear();
    gravityGens.Clear();
    upGens.Clear();
    downGens.Clear();
    leftGens.Clear();
    rightGens.Clear();
    fowardGens.Clear();
    backwardGens.Clear();
    otherGens.Clear();
    artMasses.Clear();
    gravityList.Clear();
    gyros.Clear();
    gravityDriveGroup = null;

    GridTerminalSystem.GetBlocksOfType(shipControllers, block => block.CubeGrid == Me.CubeGrid); //makes sure controller is on same grid
    GridTerminalSystem.GetBlocksOfType(onGridThrust, block => block.CubeGrid == Me.CubeGrid);
    GridTerminalSystem.GetBlocksOfType(gyros, block => block.CubeGrid == Me.CubeGrid);
    gravityDriveGroup = GridTerminalSystem.GetBlockGroupWithName(gravityDriveGroupName);

    #region block_check
    bool critFailure = false;
    if (gravityDriveGroup == null)
    {
        Echo($"Critical Error: No group named {gravityDriveGroupName} was found");
        critFailure = true;
    }
    else
    {
        gravityDriveGroup.GetBlocksOfType(artMasses);
        gravityDriveGroup.GetBlocksOfType(gravityGens, x => x.CubeGrid == Me.CubeGrid);
        gravityDriveGroup.GetBlocksOfType(otherGens, x => x.CubeGrid != Me.CubeGrid);
    }

    if (artMasses.Count == 0)
    {
        Echo($"Critical Error: No artificial masses found in the {gravityDriveGroupName} group");
        critFailure = true;
    }

    if (gravityGens.Count == 0)
    {
        Echo($"Critical Error: No gravity generators found in the {gravityDriveGroupName} group");
        critFailure = true;
    }

    if (shipControllers.Count == 0)
    {
        Echo("Critical Error: No ship controllers found on the grid");
        critFailure = true;
    }
    else
    {
        var controller = shipControllers[0];
        foreach (var block in gravityGens)
        {
            if (controller.WorldMatrix.Forward == block.WorldMatrix.Down)
                fowardGens.Add(block);
            else if (controller.WorldMatrix.Backward == block.WorldMatrix.Down)
                backwardGens.Add(block);
            else if (controller.WorldMatrix.Left == block.WorldMatrix.Down)
                leftGens.Add(block);
            else if (controller.WorldMatrix.Right == block.WorldMatrix.Down)
                rightGens.Add(block);
            else if (controller.WorldMatrix.Up == block.WorldMatrix.Down)
                upGens.Add(block);
            else if (controller.WorldMatrix.Down == block.WorldMatrix.Down)
                downGens.Add(block);
        }

        gravityList.Add(fowardGens);
        gravityList.Add(backwardGens);
        gravityList.Add(leftGens);
        gravityList.Add(rightGens);
        gravityList.Add(upGens);
        gravityList.Add(downGens);
    }

    return !critFailure;
    #endregion
}

void OverrideGyros(bool overrideOn, IMyShipController reference, Vector2 mouseInput, float rollInput)
{
    if (!hasVector)
    {
        hasVector = true;
        lastForwardVector = reference.WorldMatrix.Forward;
    }

    double pitch = 0, yaw = 0, roll = 0;
    GetRotationAngles(lastForwardVector, reference.WorldMatrix.Forward, reference.WorldMatrix.Left, reference.WorldMatrix.Up, out yaw, out pitch);

    var localAngularDeviation = new Vector3D(-pitch, yaw, roll);
    var worldAngularDeviation = Vector3D.TransformNormal(localAngularDeviation, reference.WorldMatrix);
    var worldAngularVelocity = worldAngularDeviation / timeMaxCycle;
    
    var localMouseInput = new Vector3(mouseInput.X, mouseInput.Y, rollInput);

    //var worldMouseInput = Vector3D.TransformNormal(localMouseInput, reference.WorldMatrix);

    if (!Vector3D.IsZero(localMouseInput, 1E-3))
    {
        overrideOn = false;
    }

    foreach (var block in gyros)
    {
        if (overrideOn)
        {
            var gyroAngularVelocity = Vector3D.TransformNormal(worldAngularVelocity, MatrixD.Transpose(block.WorldMatrix));
            //var gyroMouseInput = Vector3D.TransformNormal(worldMouseInput, MatrixD.Transpose(block.WorldMatrix));
            
            block.Pitch = (float)Math.Round(gyroAngularVelocity.X, 2);
            block.Yaw = (float)Math.Round(gyroAngularVelocity.Y, 2);
            block.Roll = (float)Math.Round(gyroAngularVelocity.Z, 2);
            block.GyroOverride = true;
            block.GyroPower = 100f; //im assuming this is a percentage
        }
        else
            block.GyroOverride = false;
    }

    lastForwardVector = reference.WorldMatrix.Forward;
}

/*
/// Whip's Get Rotation Angles Method v9 - 12/1/17 ///
* Fix to solve for zero cases when a vertical target vector is input
* Fixed straight up case
*/
void GetRotationAngles(Vector3D v_target, Vector3D v_front, Vector3D v_left, Vector3D v_up, out double yaw, out double pitch)
{
    //Dependencies: VectorProjection() | VectorAngleBetween()
    var projectTargetUp = VectorProjection(v_target, v_up);
    var projTargetFrontLeft = v_target - projectTargetUp;

    yaw = VectorAngleBetween(v_front, projTargetFrontLeft);

    if (Vector3D.IsZero(projTargetFrontLeft) && !Vector3D.IsZero(projectTargetUp)) //check for straight up case
        pitch = MathHelper.PiOver2;
    else
        pitch = VectorAngleBetween(v_target, projTargetFrontLeft); //pitch should not exceed 90 degrees by nature of this definition

    //---Check if yaw angle is left or right  
    //multiplied by -1 to convert from right hand rule to left hand rule
    yaw = -1 * Math.Sign(v_left.Dot(v_target)) * yaw;

    //---Check if pitch angle is up or down    
    pitch = Math.Sign(v_up.Dot(v_target)) * pitch;

    //---Check if target vector is pointing opposite the front vector
    if (Math.Abs(yaw) <= 1E-6 && v_target.Dot(v_front) < 0)
    {
        yaw = Math.PI;
    }
}

Vector3D VectorProjection(Vector3D a, Vector3D b)
{
    if (Vector3D.IsZero(b))
        return Vector3D.Zero;

    return a.Dot(b) / b.LengthSquared() * b;
}

double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
{
    if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
        return 0;
    else
        return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
}

void ManageGravDrive(bool turnOn)
{
    IMyShipController reference = GetControlledShipController(shipControllers);

    bool hasPilot = reference != null;

    if (!hasPilot)
    {
        reference = shipControllers[0];
    }

    //Desired travel vector construction
    var referenceMatrix = reference.WorldMatrix;
    var inputVec = reference.MoveIndicator; //raw input vector
    var rollVec = reference.RollIndicator;
    var mouseInputVec = reference.RotationIndicator;

    if (useGyrosToStabilize)
    {
        OverrideGyros(turnOn, reference, mouseInputVec, rollVec);
    }

    var desiredDirection = Vector3D.TransformNormal(inputVec, referenceMatrix);

    if (!Vector3D.IsZero(desiredDirection))
    {
        desiredDirection = Vector3D.Normalize(desiredDirection);
    }

    var velocityVec = reference.GetShipVelocities().LinearVelocity;

    bool hasThrust = true;
    if (onGridThrust.Count == 0)
        hasThrust = false;

    bool dampenersOn = hasThrust ? useGravityDriveAsInertialDampeners ? reference.DampenersOverride : false : useGravityDriveAsInertialDampeners;

    Echo($"Dampeners: {dampenersOn}");

    if ((velocityVec.LengthSquared() <= speedThreshold * speedThreshold && Vector3D.IsZero(desiredDirection)) || !turnOn)
    {
        ToggleMass(artMasses, false);
        ToggleDirectionalGravity(desiredDirection, velocityVec, false, dampenersOn);
    }
    else if (!hasPilot)
    {
        if (enableDampenersWhenNotControlled)
        {
            ToggleMass(artMasses, true); //default all masses to turn on
            ToggleDirectionalGravity(Vector3D.Zero, velocityVec, turnOn, true); //force dampeners on
        }
        else
        {
            ToggleMass(artMasses, true); //default all masses to turn on
            ToggleDirectionalGravity(Vector3D.Zero, velocityVec, turnOn, dampenersOn); //input is forced to zero
        }
    }
    else
    {
        ToggleMass(artMasses, true); //default all masses to turn on
        ToggleDirectionalGravity(desiredDirection, velocityVec, turnOn, dampenersOn);
    }
}

IMyShipController GetControlledShipController(List<IMyShipController> SCs)
{
    foreach (IMyShipController thisController in SCs)
    {
        if (thisController.IsUnderControl && thisController.CanControlShip)
            return thisController;
    }

    return null;
}

void ToggleDirectionalGravity(Vector3D direction, Vector3D velocityVec, bool turnOn, bool dampenersOn = true)
{
    //Handle on grid grav gens
    foreach (var list in gravityList)
    {
        if (list.Count == 0)
            continue;

        var referenceGen = list[0];

        if (turnOn)
        {
            SetGravityPower(list, true);

            double gravThrustRatio = referenceGen.WorldMatrix.Down.Dot(direction);
            double gravDampingRatio = dampenersOn ? referenceGen.WorldMatrix.Up.Dot(velocityVec) : 0;

            if (Math.Abs(gravThrustRatio) > maxThrustDotProduct)
            {
                gravThrustRatio /= fullBurnDotProduct;
                SetGravityAcceleration(list, (float)gravThrustRatio * 9.81f);
            }

            if (gravThrustRatio == 0 && gravDampingRatio == 0)
            {
                SetGravityAcceleration(list, 0f);
                continue;
            }

            //if (dampenersOn)
            //{
            double dampenerOverride = 0;

                if (Math.Abs(gravDampingRatio) < 1)
                    dampenerOverride = gravDampingRatio * gravityDriveDampenerScalingFactor;
                else
                    dampenerOverride = Math.Sign(gravDampingRatio) * gravDampingRatio * gravDampingRatio * gravityDriveDampenerScalingFactor;

                if (dampenerOverride < 0 && gravThrustRatio <= 0 && Math.Abs(dampenerOverride) > Math.Abs(gravThrustRatio * 9.81f))
                    SetGravityAcceleration(list, (float)dampenerOverride);
                else if (dampenerOverride > 0 && gravThrustRatio >= 0 && Math.Abs(dampenerOverride) > Math.Abs(gravThrustRatio * 9.81f))
                    SetGravityAcceleration(list, (float)dampenerOverride);
            //}
        }
        else
        {
            SetGravityPower(list, false);
        }

    }

    //---Handle the rest of the off-grid gravity gens
    foreach (IMyGravityGenerator thisGravGen in otherGens)
    {
        if (turnOn)
        {
            double gravThrustRatio = thisGravGen.WorldMatrix.Down.Dot(direction);
            double gravDampingRatio = dampenersOn ? thisGravGen.WorldMatrix.Up.Dot(velocityVec) : 0;

            thisGravGen.GravityAcceleration = (float)gravThrustRatio * 9.81f;
            thisGravGen.Enabled = true;

            if (gravThrustRatio == 0 && gravDampingRatio == 0)
            {
                thisGravGen.GravityAcceleration =  0f;
                continue;
            }

            //if (dampenersOn)
            //{
            double dampenerOverride = 0;

            if (Math.Abs(gravDampingRatio) < 1)
                dampenerOverride = gravDampingRatio * gravityDriveDampenerScalingFactor;
            else
                dampenerOverride = Math.Sign(gravDampingRatio) * gravDampingRatio * gravDampingRatio * gravityDriveDampenerScalingFactor;

            if (dampenerOverride < 0 && gravThrustRatio <= 0 && Math.Abs(dampenerOverride) > Math.Abs(gravThrustRatio * 9.81f))
                thisGravGen.GravityAcceleration = (float)dampenerOverride;
            else if (dampenerOverride > 0 && gravThrustRatio >= 0 && Math.Abs(dampenerOverride) > Math.Abs(gravThrustRatio * 9.81f))
                thisGravGen.GravityAcceleration = (float)dampenerOverride;
            //}
        }
        else
        {
            thisGravGen.Enabled = false;
        }
    }
}

void SetGravityAcceleration(List<IMyGravityGenerator> list, float value)
{
    foreach (var block in list)
        block.GravityAcceleration = value;
}

void SetGravityPower(List<IMyGravityGenerator> list, bool value)
{
    foreach (var block in list)
        block.Enabled = value;
}

void ToggleMass(List<IMyVirtualMass> artMasses, bool toggleOn)
{
    foreach (IMyVirtualMass thisMass in artMasses)
    {
        bool isOn = thisMass.GetValue<bool>("OnOff");
        if (isOn == toggleOn) //state is same
        {
            continue;
        }
        else if (toggleOn) //is off but should be on
        {
            thisMass.ApplyAction("OnOff_On");
        }
        else //is on but should be off
        {
            thisMass.ApplyAction("OnOff_Off");
        }
    }
}

/*
/// CHANGE LOG ///
* Rewrote entire code to use direct user inputs - v5
* Added OnOff argument handling - v6
* Fixed dampeners not acting the same way in different directions - v7
* Optimized gravity gen calcs - v8
* Reduced block refreshing from 10 Hz to 0.1 Hz - v8
* Added gyro locking when user is not applying inputs - v9
* Added useGravityDriveAsInertialDampeners varialbe and arguments - v11
* Added variable enableDampenersWhenNotControlled - v11
* Added additional check for player input - v12
* Added self triggering - v13
* Implemented actual rotational dampening - v14
* Fixed issue where dampeners fired when they shouldn't - v15
*/