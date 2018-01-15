/*
/// Whip's Gravity Alignment Systems v22-1 - revision: 1/15/18 ///    

Written by Whiplash141    
*/

/*    
==============================    
    You can edit these vars   
==============================  
*/

const string referenceName = "Reference"; //name of reference block
const string statusScreenName = "Alignment"; //(Optional) Name of status screen
const string shipName = "\n         [SHIP NAME GOES HERE]"; //(Optional) Name of your ship

bool shouldAlign = true; //If the script should attempt to stabalize by default
bool referenceOnSameGridAsProgram = true; //if true, only searches for reference blocks on
                                          //the same grid as the program block (should help with docking small vessels)

const double angleTolerance = 5; //How many degrees the code will allow before it overrides user control

//---PID Constants
const double proportionalConstant = 5;
const double derivativeConstant = .5;

/*  
====================================================  
    Don't touch anything below this <3 - Whiplash  
====================================================  
*/

const double updatesPerSecond = 10;
const double timeFlashMax = .5; //in seconds  
const double timeLimit = 1 / updatesPerSecond;
double angleRoll; double lastAngleRoll = 0;
double anglePitch; double lastAnglePitch = 0;
double timeElapsed = 0;
double timeFlash = 0;


bool canTolerate = true;
bool flashOn = true;

string stableStatus = ">> Disabled <<";
string gravityMagnitudeString;
string overrideStatus;

List<IMyGyro> gyros = new List<IMyGyro>();
List<IMyShipController> shipControllers = new List<IMyShipController>();

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

void Main(string arg, UpdateType updateSource)
{
    timeElapsed += Runtime.TimeSinceLastRun.TotalSeconds;

    switch (arg.ToLower())
    {
        case "toggle":
            if (!shouldAlign)
            {
                shouldAlign = true;
                stableStatus = "<< Active >>";
            }
            else
            {
                shouldAlign = false;
                stableStatus = ">> Disabled <<";
            }
            break;

        case "on":
            shouldAlign = true;
            stableStatus = "<< Active >>";
            break;

        case "off":
            shouldAlign = false;
            stableStatus = ">> Disabled <<";
            break;

        default:
            break;
    }

    if (timeElapsed >= timeLimit)
    {
        AlignWithGravity();
        StatusScreens();
        timeElapsed = 0;
        Echo("Stabilizers on?: " + shouldAlign.ToString());
    }
}

bool ShouldFetch(IMyTerminalBlock block)
{
    if (!block.CustomName.Contains(referenceName))
        return false;

    if (block is IMyShipController)
    {
        if (referenceOnSameGridAsProgram)
        {
            return block.CubeGrid == Me.CubeGrid;
        }
        else
        {
            return true;
        }
    }
    else
    {
        return false;
    }
}

void AlignWithGravity()
{
    //---Find our refrence and comparision blocks    
    GridTerminalSystem.GetBlocksOfType(shipControllers, ShouldFetch);

    //---Check for any cases that would lead to code failure
    if (shipControllers.Count == 0)
    {
        Echo($"ERROR: No ship controller named '{referenceName}' was found");
        return;
    }

    //---Assign our reference block
    IMyShipController referenceBlock = shipControllers[0] as IMyShipController;

    //---Populate gyro list
    gyros.Clear();
    GridTerminalSystem.GetBlocksOfType(gyros, block => block.CubeGrid == referenceBlock.CubeGrid);

    if (gyros.Count == 0)
    {
        Echo("ERROR: No gyros found on ship");
        return;
    }

    //---Get gravity vector    
    var referenceOrigin = referenceBlock.GetPosition();
    var gravityVec = referenceBlock.GetNaturalGravity();
    var gravityVecLength = gravityVec.Length();
    gravityMagnitudeString = Math.Round(gravityVecLength, 2).ToString() + " m/s²";
    if (gravityVec.LengthSquared() == 0)
    {
        gravityMagnitudeString = "No Gravity";

        foreach (IMyGyro thisGyro in gyros)
        {
            thisGyro.SetValue("Override", false);
        }
        overrideStatus = "";
        stableStatus = ">> Disabled <<";

        shouldAlign = false;

        angleRoll = 0; angleRoll = 0;
        return;
    }

    //---Dir'n vectors of the reference block     
    var referenceForward = referenceBlock.WorldMatrix.Forward;
    var referenceLeft = referenceBlock.WorldMatrix.Left;
    var referenceUp = referenceBlock.WorldMatrix.Up;

    //---Get Roll and Pitch Angles 
    anglePitch = Math.Acos(MathHelper.Clamp(gravityVec.Dot(referenceForward) / gravityVecLength, -1, 1)) - Math.PI / 2;

    Vector3D planetRelativeLeftVec = referenceForward.Cross(gravityVec);
    angleRoll = VectorAngleBetween(referenceLeft, planetRelativeLeftVec);
    angleRoll *= VectorCompareDirection(VectorProjection(referenceLeft, gravityVec), gravityVec); //ccw is positive 

    anglePitch *= -1; angleRoll *= -1;

    Echo("pitch angle:" + Math.Round((anglePitch / Math.PI * 180), 2).ToString() + " deg");
    Echo("roll angle:" + Math.Round((angleRoll / Math.PI * 180), 2).ToString() + " deg");


    //---Get Raw Deviation angle    
    double rawDevAngle = Math.Acos(MathHelper.Clamp(gravityVec.Dot(referenceForward) / gravityVec.Length() * 180 / Math.PI, -1, 1));

    //---Angle controller    
    double rollSpeed = Math.Round(angleRoll * proportionalConstant + (angleRoll - lastAngleRoll) / timeElapsed * derivativeConstant, 2);
    double pitchSpeed = Math.Round(anglePitch * proportionalConstant + (anglePitch - lastAnglePitch) / timeElapsed * derivativeConstant, 2);                                                                                                                                                            //w.H]i\p

    rollSpeed = rollSpeed / gyros.Count;
    pitchSpeed = pitchSpeed / gyros.Count;

    //store old angles  
    lastAngleRoll = angleRoll;
    lastAnglePitch = anglePitch;

    //---Check if we are inside our tolerances  
    canTolerate = true;

    if (Math.Abs(anglePitch * 180 / Math.PI) > angleTolerance)
    {
        canTolerate = false;
    }

    if (Math.Abs(angleRoll * 180 / Math.PI) > angleTolerance)
    {
        canTolerate = false;
    }

    //---Set appropriate gyro override  
    if (shouldAlign && !canTolerate)
    {
        //do gyros
        ApplyGyroOverride(pitchSpeed, 0, -rollSpeed, gyros, referenceBlock);

        timeFlash += timeElapsed;
        if (timeFlash > timeFlashMax)
        {
            if (flashOn)
            {
                overrideStatus = "\n\n           SAFETY OVERRIDE ACTIVE";
                flashOn = false;
            }
            else
            {
                overrideStatus = "";
                flashOn = true;
            }
            timeFlash = 0;
        }
    }
    else
    {
        foreach (IMyGyro thisGyro in gyros)
        {
            thisGyro.SetValue("Override", false);
        }
        overrideStatus = "";
    }
}

void StatusScreens()
{
    //---get the parts of our string  
    double roll_deg = angleRoll / Math.PI * 180;
    double pitch_deg = -anglePitch / Math.PI * 180;
    string rollStatusString = AngleStatus(roll_deg);
    string pitchStatusString = AngleStatus(pitch_deg);

    //---Construct our final string  
    string statusScreenMessage = shipName
        + "\n            Natural Gravity: " + gravityMagnitudeString
        + "\n            Stabilizer: " + stableStatus
        + "\n\n            Roll Angle: " + Math.Round(roll_deg, 2).ToString() + " degrees\n           " + rollStatusString
        + "\n\n            Pitch Angle: " + Math.Round(pitch_deg, 2).ToString() + " degrees\n           " + pitchStatusString
        + overrideStatus;


    //---Write to screens  
    var screens = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName(statusScreenName, screens, block => block is IMyTextPanel);

    if (screens.Count == 0)
        return;

    foreach (IMyTextPanel thisScreen in screens)
    {
        thisScreen.WritePublicText(statusScreenMessage);
        thisScreen.ShowTextureOnScreen();
        thisScreen.ShowPublicTextOnScreen();
    }
}

/*string AngleStatus2( double angle )  
{  
    double maxAngle = 5; //count numner of chars  
    //int strAngleLength = maxAngle.ToString().Length;  

    string strBaseAlign = "-----------0-----------";  
    string strNegMaxAlign = " [-" + maxAngle.ToString() + "]";  
    string strPosMaxAlign = "[+" + maxAngle.ToString() + "]";  

    if( angle > maxAngle )  
    {  
        strPosMaxAlign = "(+" + maxAngle.ToString() + ")";  
        return strNegMaxAlign + strBaseAlign + strPosMaxAlign;  
    }  
    else if( angle < maxAngle && angle > -1 * maxAngle )  
    {  
        int insertionSpot = (int)Math.Round( ( angle + maxAngle ) / maxAngle * 10 );  
        string strSjustedAlig = strBaseAlign.Substring( insertionSpot - 2 , 3 )  
            + "(" + strBaseAlign.Substring( insertionSpot , 3 )  
            + ")" + strBaseAlign.Substring( insertionSpot + 2 , 3 ); //dis is wrong  
    }else{  
        strNegMaxAlign = " [(-" + maxAngle.ToString() + ")";  
        return strNegMaxAlign + strBaseAlign + strPosMaxAlign;  
    }  
}*/

const string align_15 = " [-15](-)-------0----------[+15]";
const string align_14 = " [-15]-(-)------0----------[+15]";
const string align_12 = " [-15]--(-)-----0----------[+15]";
const string align_10 = " [-15]---(-)----0----------[+15]";
const string align_8 = " [-15]----(-)---0----------[+15]";
const string align_6 = " [-15]-----(-)--0----------[+15]";
const string align_4 = " [-15]------(-)-0----------[+15]";
const string align_2 = " [-15]-------(-)0----------[+15]";
const string align0 = " [-15]---------(0)---------[+15]";
const string align2 = " [-15]----------0(-)-------[+15]";
const string align4 = " [-15]----------0-(-)------[+15]";
const string align6 = " [-15]----------0--(-)-----[+15]";
const string align8 = " [-15]----------0---(-)----[+15]";
const string align10 = " [-15]----------0----(-)---[+15]";
const string align12 = " [-15]----------0-----(-)--[+15]";
const string align14 = " [-15]----------0------(-)-[+15]";
const string align15 = " [-15]----------0-------(-)[+15]";

string AngleStatus(double angle)
{
    if (angle > 15)
        return align15;
    else if (angle > 14)
        return align14;
    else if (angle > 12)
        return align12;
    else if (angle > 10)
        return align10;
    else if (angle > 8)
        return align8;
    else if (angle > 6)
        return align6;
    else if (angle > 4)
        return align4;
    else if (angle > 2)
        return align2;
    else if (angle > -2)
        return align0;
    else if (angle > -4)
        return align_2;
    else if (angle > -6)
        return align_4;
    else if (angle > -8)
        return align_6;
    else if (angle > -10)
        return align_8;
    else if (angle > -12)
        return align_10;
    else if (angle > -14)
        return align_12;
    else if (angle > -15)
        return align_14;
    else
        return align_15;
}

Vector3D VectorProjection(Vector3D a, Vector3D b) //proj a on b    
{
    Vector3D projection = a.Dot(b) / b.LengthSquared() * b;
    return projection;
}

int VectorCompareDirection(Vector3D a, Vector3D b) //returns -1 if vectors return negative dot product 
{
    double check = a.Dot(b);
    if (check < 0)
        return -1;
    else
        return 1;
}

double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
{
    if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
        return 0;
    else
        return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
}

//Whip's ApplyGyroOverride Method v9 - 8/19/17
void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list, IMyTerminalBlock reference)
{
    var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed); //because keen does some weird stuff with signs 
    var shipMatrix = reference.WorldMatrix;
    var relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipMatrix);

    foreach (var thisGyro in gyro_list)
    {
        var gyroMatrix = thisGyro.WorldMatrix;
        var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(gyroMatrix));

        thisGyro.Pitch = (float)transformedRotationVec.X;
        thisGyro.Yaw = (float)transformedRotationVec.Y;
        thisGyro.Roll = (float)transformedRotationVec.Z;
        thisGyro.GyroOverride = true;
    }
}

/*
/// WHAT'S CHANGED? ///
*   Ignore offgrid gyros
*   Fixed angles spazzing out
*/