/*   
Whip's Optical Missile Guidance System v62-16 - revised: 5/1/18 
/// PUBLIC RELEASE ///  
/// Stable ///
_______________________________________________________________________           
///DESCRIPTION///    
    This script allows the user to manually guide a missile with the 
    heading of his/her ship! The missiles will behave similar to a TOW missile. 
_______________________________________________________________________           
///FEATURES/// 
    * Configurable spiral trajectory to avoid turrets! (can be turned off) 
    * Configurable detach sequence parameters! 
    * Automatic thruster orientation detection! No more pesky directional naming needed :D 
    * Works in atmosphere! 
    * Runs at 20 Hz (old was 60 Hz) so it is much less laggy! THREE new missiles lag less than ONE of my old ones! 
    * Compensates for unwanted drift! 
    * Automatically shuts off and goes ballistic if vital components are damaged 
    * Safety mechanisms to stop the missile from flying up your tail pipe! 
____________________________________________________________           
///MISSILE SETUP INSTRUCTIONS/// 

===REQUIRED BLOCKS=== 
    * A timer 
    * A Remote Control pointing FORWARD 
    * A program block with this code 
    * A battery or reactor (for power) 
    * A merge, connector, or rotor (for attachment points) 
    * A gyroscope

    OPTIONAL BLOCKS: 
    * An artificial mass block 
    * A beacon 
    * An antenna 

    ALL BLOCKS on the missile should have the name tag "Missile 1" added in their names. 

===ADDITIONAL NAME TAGS=== 
    Some specific blocks need ADDITIONAL name tags 

    * Any detach thrusters need the name tag "Detach" in addition to the missile tag 

===TIMER SETUP=== 
    The timer should be set to: 
    * "Start" itself 
    * "Trigger Now" itself 
    * "Run" the missile program with the argument "fire" 
______________________________________________________________________           
///SHOOTER SHIP SETUP/// 

    The shooter ship needs: 
    * A remote with name tag "Shooter Reference" pointing in the direction you eant the missile to fly.
    * A method to connect to the missile (merge, rotor, or connector)
______________________________________________________________________           
///FIRING INSTRUCTIONS/// 

    The missile must be physically attached to the shooter ship before firing.
    To fire the missile, simply trigger the missile timer :) 

______________________________________________________________________           
///TROUBLESHOOTING/// 

    Find the program block in the ship terminal to check if the code detects any errors. 

    Before firing, type "setup" in the argument field of the program block and hit "run".  
    This will tell you if any vital components are missing! 

______________________________________________________________________           
///AUTHOR'S NOTES/// 

    Make sure to look at the configurable variables in the section below and tweak them to your liking! 

    I have spent a ton of time trying to make this code better than the previous iteration. 
    I hope y'all enjoy :D 

Code by Whiplash141 :) 
*/

/* 
___________________________________________________________________  

//////// DO NOT CHANGE VARIABLES HERE! ////////  
//////// USE THE CUSTOM DATA OF THIS PROGRAM, THEN RECOMPILE! ////////
___________________________________________________________________    
*/

//---Missile Name Tags   
string missileTag = "Missile 1";
string detachThrustTag = "Detach"; //(Optional) tag on detach thrust 

//---Reference Name Tags 
string shooterReferenceName = "Shooter Reference"; //name of the remote on the shooter vessel

//---Runtime variables 
double updatesPerSecond = 20; // self explanatory :P 

//---Missile Detach Parameters
double disconnectDelay = 1; //time (in seconds) that the missile will delay disconnection from firing ship

double guidanceDelay = 1; // time (in seconds) that the missile will delay guidance activation by 

double detachDuration = 0;
// time that the missile will execute detach. During the detach function, the missile 
// will use its detach thrusters and any artificial mass it detects to detach from the ship. 
// Setting this to ZERO will skip this feature and move on to the main ignition delay stage. 

double mainIgnitionDelay = 0;
// time (in seconds) that the missile will delay main engine activation AFTER the 
// detach function has finished. During this time, the missile will drift without 
// any thrusters firing. Set this to ZERO if you do not want this :) 

//---Drift Compensation Parameters 
bool driftCompensation = true;
// this determines if the missile will negate unwanted drift. This allows you to make a  
// missile using just forward thrust! 

//---Spiral Trajectory Parameters
bool enableSpiralTrajectory = false; //determines if missiles will spiral to avoid turret fire 
double spiralDegrees = 5; // angular deviation of the spiral pattern  
double timeMaxSpiral = 3; // time it takes the missile to complete a full spiral cycle 

//---Rotation speed control system 
double proportionalConstant = 50; // proportional gain of gyroscopes 
double integralConstant = 10; // integral gain of gyroscopes 
double derivativeConstant = 30; // derivative gain of gyroscopes 

//---Missile impact point offsets
double offsetUp = 0; // (in meters) Positive numbers offset up, negative offset down
double offsetLeft = 0; // (in meters) Positive numbers offset left, negative offset right

//---Missile spin parameters
double missileSpinRPM = 0; //this specifies how fast the missile will spin when flying(only in space)

//---Missile vector lock
bool lockVectorOnLaunch = false; //if the code will lock on the the shooter's vector on launch and not actively track

/*    
___________________________________________________________________    

============= Don't touch anything below this :) ==================    
___________________________________________________________________    
*/
//---So many lists... 
List<IMyTerminalBlock> missileBlocks = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> unsortedThrusters = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> mainThrusters = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> sideThrusters = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> detachThrusters = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> artMasses = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> mergeBlocks = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> batteries = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> remotes = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> shooterReferenceList = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> shooterTurretReferenceList = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> gyros = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> timers = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> programs = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> importantBlocks = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> connectors = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> rotors = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> reactors = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> antennas = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> beacons = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> sensors = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> warheads = new List<IMyTerminalBlock>();

//---Yo dawg... I heard u like vectors... 
Vector3D shooterForwardVec;
Vector3D shooterLeftVec;
Vector3D shooterUpVec;
Vector3D originPos;
Vector3D missilePos;
Vector3D headingVec;
Vector3D destinationVec;
Vector3D gravVec;

//---These are kinda super important 
IMyTerminalBlock shooterReference = null;
IMyRemoteControl missileReference = null;

//---These booleans track the status of the missile 
bool isSetup = false;
bool setupFailed = false;
bool firstRun = true;
//bool shouldKill = false;
bool hasPassed = false;
bool killAllowed = false;
bool inGravity = false;
bool missileStage1 = false;
bool missileStage2 = false;
bool missileStage3 = false;
bool missileStage4 = false;

//---Store all this important stuff for computations between methods 
double distanceFromShooter;
double max_kill_time = 3;
double kill_time = 0;
double timeElapsed = 0; //time elapsed over current iterations  
double timeTotal = 0; //total time program has been running  

PID pitchPID;
PID yawPID;

double max_distance = 10000; //static b/c we change it on kill command 
const double degToRad = Math.PI / 180;
const double radToDeg = 180 / Math.PI;
const double max_time_to_guide = 150; //in seconds  

Program()
{
    BuildConfig(Me);
}

double epsilon = 1E-6;
void Main(string arg)
{
    if (arg.ToLower() == "fire")
    {
        if (!isSetup)
        {
            isSetup = GrabBlocks();
        }

        if (!isSetup)
            return;

        //will not run or release missile until has run setup succesfully    
        if (firstRun)
        {
            timeElapsed = 0;
            timeTotal = 0;
            firstRun = false;

            if (lockVectorOnLaunch)
            {
                if (shooterReference is IMyLargeTurretBase)
                {
                    var turret = shooterReference as IMyLargeTurretBase;
                    double azimuth = (double)turret.Azimuth;
                    double elevation = (double)turret.Elevation;

                    shooterForwardVec = Vector3D.Normalize(VectorAzimuthElevation(azimuth, elevation, shooterReference));
                    shooterLeftVec = shooterForwardVec.Cross(turret.WorldMatrix.Down);
                    shooterUpVec = shooterForwardVec.Cross(shooterLeftVec);
                }
                else
                {
                    shooterForwardVec = shooterReference.WorldMatrix.Forward;
                    shooterLeftVec = shooterReference.WorldMatrix.Left;
                    shooterUpVec = shooterReference.WorldMatrix.Up;
                }

                //---Get positions of our blocks with relation to world center 
                originPos = shooterReference.GetPosition() + offsetLeft * shooterLeftVec + offsetUp * shooterUpVec;
            }
        }
        else
        {
            timeElapsed += 1.0 / 60.0;
            timeTotal += 1.0 / 60.0;
            timeSpiral += 1.0 / 60.0;
        }

        LaunchMissile();
        //StatusCheck(); //disabled because this bitch aint working well

        if (timeTotal + epsilon >= guidanceDelay && timeElapsed + epsilon >= (1d / updatesPerSecond))
        {
            Echo("WMI Optical Missile Guidance System Active..."); Echo("Run Time: " + Math.Round(timeTotal).ToString());
            GuideMissile();
            timeElapsed = 0;
        }
    }
    else if (arg.ToLower() == "setup")
    {
        GrabBlocks();
    }
    else if (arg.ToLower() == "kill" && killAllowed)
    {
        KillGuidance();
        //max_distance = double.PositiveInfinity;
    }

    if (timeTotal > max_time_to_guide)
    {
        KillGuidance();
        //max_distance = double.PositiveInfinity;
    }
}

bool isRemote(IMyTerminalBlock block)
{
    var remoteTest = block as IMyRemoteControl;
    return remoteTest != null;
}

bool isTurret(IMyTerminalBlock block)
{
    var testTurret = block as IMyLargeTurretBase;
    return testTurret != null;
}

void ClearLists()
{
    missileBlocks.Clear();
    unsortedThrusters.Clear();
    mainThrusters.Clear();
    sideThrusters.Clear();
    detachThrusters.Clear();
    artMasses.Clear();
    mergeBlocks.Clear();
    batteries.Clear();
    remotes.Clear();
    shooterReferenceList.Clear();
    shooterTurretReferenceList.Clear();
    gyros.Clear();
    timers.Clear();
    programs.Clear();
    importantBlocks.Clear();
    connectors.Clear();
    rotors.Clear();
    reactors.Clear();
    antennas.Clear();
    beacons.Clear();
    sensors.Clear();
    warheads.Clear();
}

bool GrabBlocks()
{
    UpdateConfig(Me);
    ClearLists();

    pitchPID = new PID(proportionalConstant, integralConstant, derivativeConstant, .25, 1.0 / updatesPerSecond);
    yawPID = new PID(proportionalConstant, integralConstant, derivativeConstant, .25, 1.0 / updatesPerSecond);

    setupFailed = false;

    GridTerminalSystem.SearchBlocksOfName(missileTag, missileBlocks);
    GridTerminalSystem.SearchBlocksOfName(shooterReferenceName, shooterReferenceList, isRemote);
    GridTerminalSystem.SearchBlocksOfName(shooterReferenceName, shooterTurretReferenceList, isTurret);

    for (int i = 0; i < shooterReferenceList.Count; i++)
    {
        importantBlocks.Add(shooterReferenceList[i]);
    }

    for (int i = 0; i < shooterTurretReferenceList.Count; i++)
    {
        importantBlocks.Add(shooterTurretReferenceList[i]);
    }

    //---Sort through all blocks with the missile tag 
    for (int i = 0; i < missileBlocks.Count; i++)
    {
        var thisBlock = missileBlocks[i] as IMyTerminalBlock;

        if (thisBlock is IMyThrust)
        {
            if (thisBlock.CustomName.Contains(detachThrustTag))
            {
                detachThrusters.Add(thisBlock);
                unsortedThrusters.Add(thisBlock);
            }
            else
            {
                unsortedThrusters.Add(thisBlock);
            }
        }
        else if (thisBlock is IMyVirtualMass)
        {
            artMasses.Add(thisBlock);
        }
        else if (thisBlock is IMyBatteryBlock)
        {
            batteries.Add(thisBlock);
        }
        else if (thisBlock is IMyGyro)
        {
            gyros.Add(thisBlock);
            importantBlocks.Add(thisBlock);
        }
        else if (thisBlock is IMyShipMergeBlock)
        {
            mergeBlocks.Add(thisBlock);
        }
        else if (thisBlock is IMyTimerBlock)
        {
            timers.Add(thisBlock);
            importantBlocks.Add(thisBlock);
        }
        else if (thisBlock is IMyProgrammableBlock)
        {
            programs.Add(thisBlock);
            importantBlocks.Add(thisBlock);
        }
        else if (thisBlock is IMyRemoteControl)
        {
            remotes.Add(thisBlock);
        }
        else if (thisBlock is IMyShipConnector)
        {
            connectors.Add(thisBlock);
        }
        else if (thisBlock is IMyMotorStator)
        {
            rotors.Add(thisBlock);
        }
        else if (thisBlock is IMyReactor)
        {
            reactors.Add(thisBlock);
        }
        else if (thisBlock is IMyRadioAntenna)
        {
            antennas.Add(thisBlock);
        }
        else if (thisBlock is IMyBeacon)
        {
            beacons.Add(thisBlock);
        }
        else if (thisBlock is IMySensorBlock)
        {
            sensors.Add(thisBlock);
        }
        else if (thisBlock is IMyWarhead)
        {
            warheads.Add(thisBlock);
        }
    }

    Echo("Setup results for " + missileTag);
    //---Check what we have and display any missing blocks 
    if (artMasses.Count == 0)
    {
        Echo("[OPTIONAL] No artificial masses found");
    }

    if (sensors.Count == 0)
    {
        Echo("[OPTIONAL] No sensors found");
    }

    if (warheads.Count == 0)
    {
        Echo("[OPTIONAL] No warheads found");
    }

    if (beacons.Count == 0)
    {
        Echo("[OPTIONAL] No beacons found");
    }

    if (antennas.Count == 0)
    {
        Echo("[OPTIONAL] No antennas found");
    }

    if (shooterReferenceList.Count == 0 && shooterTurretReferenceList.Count == 0)
    {
        Echo("[FAILED] No remote or turret named '" + shooterReferenceName + "'found");
        setupFailed = true;
    }

    if (gyros.Count == 0)
    {
        Echo("[FAILED] No control gyros found");
        setupFailed = true;
    }

    if (remotes.Count == 0)
    {
        Echo("[FAILED] No remotes found");
        setupFailed = true;
    }
    else
    {
        GetThrusterOrientation(remotes[0]);
    }

    if (sideThrusters.Count == 0)
    {
        Echo("[OPTIONAL] No side thrusters found");
    }

    if (detachThrusters.Count == 0)
    {
        Echo("[OPTIONAL] No detach thrusters found");
    }

    if (mainThrusters.Count == 0)
    {
        Echo("[FAILED] No main thrusters found");
        setupFailed = true;
    }

    if (batteries.Count == 0 && reactors.Count == 0)
    {
        Echo("[FAILED] No batteries or reactors found");
        setupFailed = true;
    }

    if (mergeBlocks.Count == 0 && rotors.Count == 0 && connectors.Count == 0)
    {
        Echo("[WARNING] No merge blocks, rotors, or connectors found");
        //setupFailed = true;
    }

    if (!setupFailed)
    {
        Echo("[SUCCESS] Ready to run");

        if (shooterReferenceList.Count != 0)
            shooterReference = shooterReferenceList[0];
        else
            shooterReference = shooterTurretReferenceList[0];

        missileReference = remotes[0] as IMyRemoteControl;
    }

    return (!setupFailed);
}

void GetThrusterOrientation(IMyTerminalBlock refBlock)
{
    var forwardDirn = refBlock.WorldMatrix.Forward;

    foreach (IMyThrust thisThrust in unsortedThrusters)
    {
        var thrustDirn = thisThrust.WorldMatrix.Backward;
        bool sameDirn = thrustDirn == forwardDirn;

        if (sameDirn)
        {
            mainThrusters.Add(thisThrust);
        }
        else
        {
            sideThrusters.Add(thisThrust);
        }
    }
}

void StatusCheck()
{
    for (int k = 0; k < importantBlocks.Count; k++)
    {
        IMyTerminalBlock block = importantBlocks[k];
        IMySlimBlock slim = block.CubeGrid.GetCubeBlock(block.Position);
        if (slim.CurrentDamage > 0)
        {
            Echo("Damage");
            kill_time = max_kill_time;
            KillGuidance();
            return;
        }
    }
}

void LaunchMissile()
{
    //Stage 1: Discharge 
    if (!missileStage1) //set battery to discharge 
    {
        foreach (IMyBatteryBlock thisBattery in batteries)
        {
            thisBattery.ApplyAction("OnOff_On"); //make sure our battery is on  
            thisBattery.SetValue("Recharge", false);
            thisBattery.SetValue("Discharge", false);
        }

        foreach (IMyReactor thisReactor in reactors)
        {
            thisReactor.ApplyAction("OnOff_On"); //make sure our reactors are on  
        }

        foreach (IMySensorBlock thisSensor in sensors)
        {
            thisSensor.ApplyAction("OnOff_Off");
        }

        foreach (IMyWarhead thisWarhead in warheads)
        {
            thisWarhead.SetValue("Safety", false); //this is backwards... because Keen
        }

        missileStage1 = true;
    }
    //Stage 2: Release 
    else if (timeTotal >= disconnectDelay && !missileStage2) //detach missile 
    {
        foreach (IMyVirtualMass thisMass in artMasses)
        {
            thisMass.ApplyAction("OnOff_On");
        }

        foreach (IMyGyro thisGyro in gyros)
        {
            thisGyro.ApplyAction("OnOff_On");
        }

        foreach (IMyShipMergeBlock thisMerge in mergeBlocks)
        {
            thisMerge.ApplyAction("OnOff_Off");
        }

        foreach (IMyShipConnector thisConnector in connectors)
        {
            thisConnector.ApplyAction("Unlock");
        }

        foreach (IMyMotorStator thisRotor in rotors)
        {
            //thisRotor.SetValue("Force weld", false);
            thisRotor.ApplyAction("Detach");
        }

        foreach (IMyRadioAntenna thisAntenna in antennas)
        {
            thisAntenna.SetValue("Radius", 800f);
            thisAntenna.ApplyAction("OnOff_Off");
            thisAntenna.SetValue("EnableBroadCast", true);
            thisAntenna.ApplyAction("OnOff_On");
            thisAntenna.CustomName = "";
        }

        foreach (IMyBeacon thisBeacon in beacons)
        {
            thisBeacon.SetValue("Radius", 800f);
            thisBeacon.ApplyAction("OnOff_On");
            thisBeacon.CustomName = "";
        }

        ManeuveringThrust(false);

        foreach (IMyThrust thisThrust in detachThrusters)
        {
            thisThrust.ApplyAction("OnOff_On");
            thisThrust.SetValue("Override", float.MaxValue);
        }

        killAllowed = true;
        missileStage2 = true;

    }
    //Stage 3: Drift 
    else if (timeTotal >= disconnectDelay + detachDuration && !missileStage3)
    {
        foreach (IMyThrust thisThrust in detachThrusters)
        {
            thisThrust.SetValue("Override", float.MinValue);
        }
        missileStage3 = true;
    }
    //Stage 4: Ignition 
    else if (timeTotal >= disconnectDelay + mainIgnitionDelay + detachDuration && !missileStage4) //fire missile     
    {
        ManeuveringThrust(true);

        foreach (IMyVirtualMass thisMass in artMasses)
        {
            thisMass.ApplyAction("OnOff_Off");
        }

        foreach (IMySensorBlock thisSensor in sensors)
        {
            thisSensor.ApplyAction("OnOff_On");
        }

        foreach (IMyWarhead thisWarhead in warheads)
        {
            thisWarhead.SetValue("Safety", true); //arms warheads... Keen is drunk
        }

        MainThrustOverride();
        missileStage4 = true;
    }
}

void GuideMissile()
{
    //---Get orientation vectors of our shooter vessel     
    if (!lockVectorOnLaunch)
    {
        if (shooterReference is IMyLargeTurretBase)
        {
            var turret = shooterReference as IMyLargeTurretBase;
            double azimuth = turret.Azimuth;
            double elevation = turret.Elevation;

            shooterForwardVec = Vector3D.Normalize(VectorAzimuthElevation(azimuth, elevation, shooterReference));
            shooterLeftVec = shooterForwardVec.Cross(turret.WorldMatrix.Down);
            shooterUpVec = shooterForwardVec.Cross(shooterLeftVec);
        }
        else
        {
            shooterForwardVec = shooterReference.WorldMatrix.Forward;
            shooterLeftVec = shooterReference.WorldMatrix.Left;
            shooterUpVec = shooterReference.WorldMatrix.Up;
        }

        //---Get positions of our blocks with relation to world center 
        originPos = shooterReference.GetPosition() + offsetLeft * shooterLeftVec + offsetUp * shooterUpVec;
    }


    missilePos = missileReference.GetPosition();

    //---Find current distance from shooter to missile     
    distanceFromShooter = Vector3D.Distance(originPos, missilePos);
    ScaleAntennaRange(distanceFromShooter);

    //---Check if we are in range 
    if (distanceFromShooter > max_distance)
    {
        Echo("Out of range");
        KillGuidance();
    }

    //---Get orientation vectors of our missile 
    Vector3D missileFrontVec = missileReference.WorldMatrix.Forward;
    Vector3D missileLeftVec = missileReference.WorldMatrix.Left;
    Vector3D missileUpVec = missileReference.WorldMatrix.Up;

    //---Find vector from shooter to missile     
    var shooterToMissileVec = missilePos - originPos;

    //---Calculate angle between shooter vector and missile vector     
    double rawDevAngle = VectorAngleBetween(shooterForwardVec, shooterToMissileVec) * radToDeg;

    //---Calculate perpendicular distance from shooter vector     
    var projectionVec = VectorProjection(shooterToMissileVec, shooterForwardVec);
    double deviationDistance = Vector3D.DistanceSquared(projectionVec, shooterToMissileVec);

    //---Check if we have gravity 
    double rollAngle = 0; double rollSpeed = 0;

    inGravity = false;
    gravVec = missileReference.GetNaturalGravity();
    double gravMagSquared = gravVec.LengthSquared();
    if (gravMagSquared != 0)
    {
        if (gravVec.Dot(missileUpVec) < 0)
        {
            rollAngle = Math.PI / 2 - Math.Acos(MathHelper.Clamp(gravVec.Dot(missileLeftVec) / gravVec.Length(), -1, 1));
        }
        else
        {
            rollAngle = Math.PI + Math.Acos(MathHelper.Clamp(gravVec.Dot(missileLeftVec) / gravVec.Length(), -1, 1));
        }
        rollSpeed = rollAngle;
        inGravity = true;
    }
    else
    {
        if (missileStage4)
            rollSpeed = missileSpinRPM * Math.PI / 30; //converts RPM to rad/s
    }

    var missileSpeed = missileReference.GetShipSpeed();

    //---Determine scaling factor    
    double scalingFactor;
    if (rawDevAngle < 90)
    {
        scalingFactor = projectionVec.Length() + Math.Max(2 * missileSpeed, 200); //travel approx. 200m from current position in direction of target vector  
        destinationVec = originPos + scalingFactor * shooterForwardVec;
        if (!hasPassed)
            hasPassed = true;
    }
    else if (hasPassed)
    {
        //---Determine where missile is in relation to shooter  
        int signLeft = Math.Sign(shooterToMissileVec.Dot(shooterLeftVec));
        int signUp = Math.Sign(shooterToMissileVec.Dot(shooterUpVec));

        scalingFactor = -projectionVec.Length() + Math.Max(2 * missileSpeed, 200); //added the Math.Max part for modded speed worlds
        destinationVec = originPos + scalingFactor * shooterForwardVec + signLeft * 50 * shooterLeftVec + signUp * 50 * shooterUpVec;
    }
    else
    {
        scalingFactor = -projectionVec.Length() + Math.Max(2 * missileSpeed, 200);
        destinationVec = originPos + scalingFactor * shooterForwardVec;
    }

    //---Find vector from missile to destinationVec    
    var missileToTargetVec = destinationVec - missilePos;                                                                                                                                                                                                                                                                                                     //w.H/i-P*L+a.s^H 

    //---Get travel vector 
    var missileVelocityVec = missileReference.GetShipVelocities().LinearVelocity;

    //---Calc our new heading based upon our travel vector    
    if (missileStage3)
    {
        headingVec = CalculateHeadingVector(missileToTargetVec, missileVelocityVec, driftCompensation);
    }
    else
    {
        headingVec = CalculateHeadingVector(missileToTargetVec, missileVelocityVec, false);
    }

    //---Calc spiral trajectory 
    if (missileStage4 && enableSpiralTrajectory)
    {
        headingVec = SpiralTrajectory(headingVec, shooterForwardVec, shooterUpVec);
    }

    //---Get pitch and yaw angles 
    double yawAngle; double pitchAngle;
    GetRotationAngles(headingVec, missileFrontVec, missileLeftVec, missileUpVec, out yawAngle, out pitchAngle);

    //---Angle controller
    double yawSpeed = yawPID.Control(yawAngle);
    double pitchSpeed = pitchPID.Control(pitchAngle);

    //---Set appropriate gyro override 
    ApplyGyroOverride(pitchSpeed, yawSpeed, rollSpeed, gyros, missileReference);
}

Vector3D CalculateHeadingVector(Vector3D targetVec, Vector3D velocityVec, bool driftComp)
{
    if (!driftComp)
    {
        return targetVec;
    }

    if (velocityVec.LengthSquared() < 100)
    {
        return targetVec;
    }

    if (targetVec.Dot(velocityVec) > 0)
    {
        return VectorReflection(velocityVec, targetVec, 5);
    }
    else
    {
        return -velocityVec;
    }
}

//Whip's Vector from Elevation and Azimuth v6
Vector3D VectorAzimuthElevation(double az, double el, IMyTerminalBlock reference)
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
    else
    {
        x = 1 * x_mult;
        y = Math.Tan(az);
        double v_xy = Math.Sqrt(1 + y * y);
        z = v_xy * Math.Tan(el);
    }

    return reference.WorldMatrix.Forward * x + reference.WorldMatrix.Left * y + reference.WorldMatrix.Up * z;
}

void ScaleAntennaRange(double dist)
{
    foreach (IMyRadioAntenna thisAntenna in antennas)
    {
        thisAntenna.EnableBroadcasting = true;
        thisAntenna.Radius = (float)dist + 100f;
    }

    foreach (IMyBeacon thisBeacon in beacons)
    {
        thisBeacon.Radius = (float)dist + 100f;
    }
}

void ManeuveringThrust(bool turnOn)
{
    foreach (IMyThrust thisThrust in sideThrusters)
    {
        if (thisThrust.Enabled != turnOn)
            thisThrust.Enabled = turnOn;
    }
}

void MainThrustOverride()
{
    foreach (IMyThrust thisThrust in mainThrusters)
    {
        if (thisThrust.Enabled != true)
            thisThrust.Enabled = true;

        thisThrust.ThrustOverridePercentage = 1f;
    }
}

void KillGuidance()
{
    foreach (IMyWarhead block in warheads)
    {
        block.Detonate();
    }

    foreach (var block in missileBlocks)
    {
        var functionalBlock = block as IMyFunctionalBlock;
        if (functionalBlock != null)
        {
            functionalBlock.Enabled = false;
        }
    }
}

void KillGuidance(double angleOfDeviation, double distanceOfDeviation)
{
    if (enableSpiralTrajectory)
        enableSpiralTrajectory = false;

    if (kill_time >= max_kill_time || timeTotal >= max_time_to_guide)
    {
        for (int i = 0; i < gyros.Count; i++)
        {
            var thisGyro = gyros[i] as IMyGyro;
            if (thisGyro != null)
            {
                if (thisGyro.GyroOverride == false)
                    thisGyro.GyroOverride = true;

                thisGyro.Pitch = 0f;
                thisGyro.Yaw = 0f;
                thisGyro.Roll = 0f;
            }
        }

        if (!inGravity)
        {
            for (int i = 0; i < mainThrusters.Count; i++)
            {
                var thisThruster = mainThrusters[i] as IMyThrust;
                if (thisThruster != null)
                    thisThruster.Enabled = false;
            }

            for (int i = 0; i < sideThrusters.Count; i++)
            {
                var thisThruster = sideThrusters[i] as IMyThrust;
                if (thisThruster != null)
                    thisThruster.Enabled = false;
            }
        }

        for (int i = 0; i < importantBlocks.Count; i++)
        {
            var thisBlock = importantBlocks[i] as IMyFunctionalBlock;
            if (thisBlock != null)
            {
                thisBlock.Enabled = false;
            }
        }
    }

    if (angleOfDeviation < 5 && distanceOfDeviation < 1)
    {
        kill_time += timeElapsed;
    }
    else
    {
        kill_time = 0;
    }
}

Vector3D VectorProjection(Vector3D a, Vector3D b) //proj a on b    
{
    Vector3D projection = a.Dot(b) / b.LengthSquared() * b;
    return projection;
}

Vector3D VectorReflection(Vector3D a, Vector3D b, double rejectionFactor = 1) //reflect a over b    
{
    Vector3D project_a = VectorProjection(a, b);
    Vector3D reject_a = a - project_a;
    Vector3D reflect_a = project_a - reject_a * rejectionFactor;
    return reflect_a;
}

double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
{
    if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
        return 0;
    else
        return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
}

//Whip's Get Rotation Angles Method v5 - 5/30/17
void GetRotationAngles(Vector3D v_target, Vector3D v_front, Vector3D v_left, Vector3D v_up, out double yaw, out double pitch)
{
    //Dependencies: VectorProjection() | VectorAngleBetween()
    var projectTargetUp = VectorProjection(v_target, v_up);
    var projTargetFrontLeft = v_target - projectTargetUp;

    yaw = VectorAngleBetween(v_front, projTargetFrontLeft);
    pitch = VectorAngleBetween(v_target, projTargetFrontLeft);

    //---Check if yaw angle is left or right  
    //multiplied by -1 to convert from right hand rule to left hand rule
    yaw = -1 * Math.Sign(v_left.Dot(v_target)) * yaw;

    //---Check if pitch angle is up or down    
    pitch = Math.Sign(v_up.Dot(v_target)) * pitch;

    //---Check if target vector is pointing opposite the front vector
    if (pitch == 0 && yaw == 0 && v_target.Dot(v_front) < 0)
    {
        yaw = Math.PI;
    }
}

//Whip's ApplyGyroOverride Method v9 - 8/19/17
void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyTerminalBlock> gyro_list, IMyTerminalBlock reference)
{
    var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed);

    var shipMatrix = reference.WorldMatrix;
    var relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipMatrix);

    foreach (IMyGyro thisGyro in gyro_list)
    {
        var gyroMatrix = thisGyro.WorldMatrix;
        var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(gyroMatrix));

        thisGyro.Pitch = (float)transformedRotationVec.X; //because keen does some weird stuff with signs 
        thisGyro.Yaw = (float)transformedRotationVec.Y;
        thisGyro.Roll = (float)transformedRotationVec.Z;
        thisGyro.GyroOverride = true;
    }
}

//Whip's Spiral Trajectory Method v2

double timeSpiral = 0;

Vector3D SpiralTrajectory(Vector3D v_target, Vector3D v_front, Vector3D v_up)
{
    double spiralRadius = Math.Tan(spiralDegrees * degToRad);
    Vector3D v_targ_norm = Vector3D.Normalize(v_target);

    if (timeSpiral > timeMaxSpiral)
        timeSpiral = 0;

    double angle_theta = 2 * Math.PI * timeSpiral / timeMaxSpiral;

    if (v_front.Dot(v_targ_norm) > 0)
    {
        Vector3D v_x = Vector3D.Normalize(v_up.Cross(v_targ_norm));
        Vector3D v_y = Vector3D.Normalize(v_x.Cross(v_targ_norm));
        Vector3D v_target_adjusted = v_targ_norm + spiralRadius * (v_x * Math.Cos(angle_theta) + v_y * Math.Sin(angle_theta));
        return v_target_adjusted;
    }
    else
    {
        return v_targ_norm;
    }
}

#region VARIABLE CONFIG
Dictionary<string, string> configDict = new Dictionary<string, string>();

void BuildConfig(IMyTerminalBlock block)
{
    configDict.Clear();
    configDict.Add("missileTag", missileTag.ToString());
    configDict.Add("detachThrustTag", detachThrustTag.ToString());
    configDict.Add("shooterReferenceName", shooterReferenceName.ToString());
    configDict.Add("updatesPerSecond", updatesPerSecond.ToString());
    configDict.Add("disconnectDelay", disconnectDelay.ToString());
    configDict.Add("guidanceDelay", guidanceDelay.ToString());
    configDict.Add("detachDuration", detachDuration.ToString());
    configDict.Add("mainIgnitionDelay", mainIgnitionDelay.ToString());
    configDict.Add("driftCompensation", driftCompensation.ToString());
    configDict.Add("enableSpiralTrajectory", enableSpiralTrajectory.ToString());
    configDict.Add("spiralDegrees", spiralDegrees.ToString());
    configDict.Add("timeMaxSpiral", timeMaxSpiral.ToString());
    configDict.Add("proportionalConstant", proportionalConstant.ToString());
    configDict.Add("integralConstant", integralConstant.ToString());
    configDict.Add("derivativeConstant", derivativeConstant.ToString());
    configDict.Add("offsetUp", offsetUp.ToString());
    configDict.Add("offsetLeft", offsetLeft.ToString());
    configDict.Add("missileSpinRPM", missileSpinRPM.ToString());
    configDict.Add("lockVectorOnLaunch", lockVectorOnLaunch.ToString());


    UpdateConfig(block, true);
}

void UpdateConfig(IMyTerminalBlock block, bool isBuilding = false)
{
    string customData = block.CustomData;
    var lines = customData.Split('\n');

    foreach (var thisLine in lines)
    {
        var words = thisLine.Split('=');
        if (words.Length == 2)
        {
            var variableName = words[0].Trim();
            var variableValue = words[1].Trim();
            string dictValue;
            if (configDict.TryGetValue(variableName, out dictValue))
            {
                configDict[variableName] = variableValue;
            }
        }
    }

    GetVariableFromConfig("missileTag", ref missileTag);
    GetVariableFromConfig("detachThrustTag", ref detachThrustTag);
    GetVariableFromConfig("shooterReferenceName", ref shooterReferenceName);
    GetVariableFromConfig("updatesPerSecond", ref updatesPerSecond);
    GetVariableFromConfig("disconnectDelay", ref disconnectDelay);
    GetVariableFromConfig("guidanceDelay", ref guidanceDelay);
    GetVariableFromConfig("detachDuration", ref detachDuration);
    GetVariableFromConfig("mainIgnitionDelay", ref mainIgnitionDelay);
    GetVariableFromConfig("driftCompensation", ref driftCompensation);
    GetVariableFromConfig("enableSpiralTrajectory", ref enableSpiralTrajectory);
    GetVariableFromConfig("spiralDegrees", ref spiralDegrees);
    GetVariableFromConfig("timeMaxSpiral", ref timeMaxSpiral);
    GetVariableFromConfig("proportionalConstant", ref proportionalConstant);
    GetVariableFromConfig("integralConstant", ref integralConstant);
    GetVariableFromConfig("derivativeConstant", ref derivativeConstant);
    GetVariableFromConfig("offsetUp", ref offsetUp);
    GetVariableFromConfig("offsetLeft", ref offsetLeft);
    GetVariableFromConfig("missileSpinRPM", ref missileSpinRPM);
    GetVariableFromConfig("lockVectorOnLaunch", ref lockVectorOnLaunch);

    WriteConfig(block);

    if (isBuilding)
        Echo("Config Loaded");
    else
        Echo("Config Updated");
}

StringBuilder configSB = new StringBuilder();
void WriteConfig(IMyTerminalBlock block)
{
    configSB.Clear();
    foreach (var keyValue in configDict)
    {
        configSB.AppendLine($"{keyValue.Key} = {keyValue.Value}");
    }

    block.CustomData = configSB.ToString();
}

void GetVariableFromConfig(string name, ref bool variableToUpdate)
{
    string valueStr;
    if (configDict.TryGetValue(name, out valueStr))
    {
        bool thisValue;
        if (bool.TryParse(valueStr, out thisValue))
        {
            variableToUpdate = thisValue;
        }
    }
}

void GetVariableFromConfig(string name, ref int variableToUpdate)
{
    string valueStr;
    if (configDict.TryGetValue(name, out valueStr))
    {
        int thisValue;
        if (int.TryParse(valueStr, out thisValue))
        {
            variableToUpdate = thisValue;
        }
    }
}

void GetVariableFromConfig(string name, ref float variableToUpdate)
{
    string valueStr;
    if (configDict.TryGetValue(name, out valueStr))
    {
        float thisValue;
        if (float.TryParse(valueStr, out thisValue))
        {
            variableToUpdate = thisValue;
        }
    }
}

void GetVariableFromConfig(string name, ref double variableToUpdate)
{
    string valueStr;
    if (configDict.TryGetValue(name, out valueStr))
    {
        double thisValue;
        if (double.TryParse(valueStr, out thisValue))
        {
            variableToUpdate = thisValue;
        }
    }
}

void GetVariableFromConfig(string name, ref string variableToUpdate)
{
    string valueStr;
    if (configDict.TryGetValue(name, out valueStr))
    {
        variableToUpdate = valueStr;
    }
}
#endregion

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
    public double Value { get; private set; }

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

/*
CHANGELOG:
- Redesigned pitch and yaw determination method
- Added sensor support, sensors turn on when main ignition is triggered
- Optimized math operations
- Re-added "hasPassed" boolean
- Made sure lists properly clear when setup is run for a second time
- Removed fudge number
- Added warhead support
- Fixed issue that caused missiles to spaz out.
- Clamped arccosine inputs to account for floating point errors
- Fixed missile spin being activated before main ignition fires
- Changed PD gains to be more responsive
- Fixed obsolete method calls
- Changed time count to use 1/60 seconds as an assumed value instead of using runtime- this causes the code to run 10 times a simspeed second instead of a real second
- Added variable configuration to custom data
- Removed statics
- Tweaked the GetRotationAngles() method
- Optimized VectorAngleBetween() method
- Improved variable config functions
- Fixed antenna not broadcasting
- Fixed readme (thanks DarKovalord)
- Implemented workaround for bug where turning off the safety of a warhead would disarm it... thanks keen
- Fixed battery discharge bug
- Changed drift compensation to kick in after detach function is done
- Added in my new ApplyGyroOverride method!
- Added PID controller class
- Guidance kills instantly after the max runtime has been exceeded
- Added lockVectorOnLaunch variable and added it to custom data config
- Moved maneuvering thrust activation to the main ignition sequence
- Spiral trajectory is now only enabled after main thrust ignition
- Added integral constant to the PID controller
*/
