 
/*  
Whip's GASP Missile Guidance Script v14 - revised: 11/1/15 
/// PUBLIC RELEASE /// 
HOWDY! 
___________________________________________________________________ 
Video Instructions: 
https://www.youtube.com/watch?v=B4cLBJ-D0YQ 
___________________________________________________________________ 
Setup Instructions:   
 
The missile must be attached to the same grid as your firing 
platform via merge block, connector, or rotor. 
     
    Missile Setup 
     1) Name all forward thrusters "Main Thruster" 
     2) Name all side thrusters "Side Thruster" 
     3) Name all missile batteries "Missile Battery" 
     4) Name all missile merges "Missile Merge" 
     5) Make a remote with name "[Missile 1]" facing forward 
     6) Make a gyro with name "[Control 1]" facing forward 
     7) Place this program on the missile 
     8) Place a timer on the missile 
        DO NOT TRIGGER IT YET!!! 
         - Set to trigger now itself 
         - Set to start itself 
         - Set to run this program 
     
    Shooter Ship Setup 
     1) Make a remote with name "[Shooter]" facing forward 
___________________________________________________________________ 
Firing Instructions: 
     
Trigger the timer you made in Step 8 and aim where you want  
the missile to go! :) 
___________________________________________________________________ 
Author's Notes: 
 
If you have any questions, comments, or concerns, feel free to leave a comment on           
the workshop page: http://steamcommunity.com/sharedfiles/filedetails/?id=416932930          
 
- Whiplash141 :)   
*/  
 
/* 
___________________________________________________________________ 
 
========== You can edit these variables to your liking ============ 
___________________________________________________________________ 
*/ 
    string shooterReferenceName = "[Shooter]"; //name tag of shooter's remote 
    string missileReferenceName = "[Missile 1]"; //name tag of missile's remote 
    string gyroName = "[Control 1]"; //name of missile gyro 
    string forwardThrustName = "Main Thruster"; //name tag of forward thrust on the missile          
    string maneuveringThrustersName = "Side Thruster"; //name tag of manevering thrusters on the missile 
    string mergeName = "Missile Merge"; //name tag of missile merge     
    string batteryName = "Missile Battery"; //name tag of missile battery  
    double pre_launch_delay = 2; //time (in seconds) that the missile will delay guidance activation by 
    double max_rotation_degrees = 180; //in degrees per second (360 max for small ships, 180 max for large ships)  
    double max_distance = 10000; //maximum guidance distance in meters; don't enlarge it lol, 10km is super far  
    int tick_limit = 1; //change to higher for less precision  
 
/* 
___________________________________________________________________ 
 
============= Don't touch anything below this :) ================== 
___________________________________________________________________ 
*/ 
    List<IMyTerminalBlock> remotes = new List<IMyTerminalBlock>();   
    List<IMyTerminalBlock> shooterRefrenceList = new List<IMyTerminalBlock>();   
    List<IMyTerminalBlock> missileRefrenceList = new List<IMyTerminalBlock>();   
    List<IMyTerminalBlock> gyroList = new List<IMyTerminalBlock>(); 
    List<IMyTerminalBlock> forwardThrusters = new List<IMyTerminalBlock>();                 
    List<IMyTerminalBlock> mergeBlocks = new List<IMyTerminalBlock>();          
    List<IMyTerminalBlock> maneuveringThrusters = new List<IMyTerminalBlock>();       
    List<IMyTerminalBlock> batteries = new List<IMyTerminalBlock>();       
    IMyRemoteControl shooterRefrence;   
    IMyRemoteControl missileRefrence;   
    bool hasRun = false;  
    double delta_origin;  
    int current_tick = 0;   
    int duration = 0;       
    int timeElapsed = 0;       
  
void Main()   
{    
    Echo("Tick: " + current_tick.ToString());  
    MissileSystems(); 
     
    if (!hasRun)  
        GrabRemotes();  
     
    if (duration < Math.Ceiling(pre_launch_delay * 60)) 
    {  
        duration++;  
        return;  
    }  
    else  
    {  
        if((current_tick % tick_limit) == 0) 
        { 
            Echo("Guidance Active");   
            GuideMissile();   
            current_tick = 0;   
        } 
         
    }        
    current_tick++;  
    Echo("Has run?: " + hasRun); 
}       
  
void GrabRemotes()  
{  
    GridTerminalSystem.SearchBlocksOfName(gyroName, gyroList);  
    GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(remotes);  
    for(int i = 0 ; i < remotes.Count ; i++)   
        {   
            var thisRemote = remotes[i] as IMyRemoteControl;  
            if(thisRemote.CustomName.Contains(shooterReferenceName))   
            {     
                shooterRefrenceList.Add(thisRemote as IMyRemoteControl);   
                Echo("Found Shooter");   
            }   
  
            if(thisRemote.CustomName.Contains(missileReferenceName))   
            {   
                missileRefrenceList.Add(thisRemote as IMyRemoteControl);   
                Echo("Found Missile");   
            }   
        }  
 
//---Check if we do not have an shooter remote  
    if(shooterRefrenceList.Count == 0)   
    {   
        Echo("No shooter refrence block found");   
        hasRun = false;   
        return;   
    }  
//---Check if we do not have a missile remote  
    else if(missileRefrenceList.Count == 0)   
    {   
        Echo("No missile refrence block found");   
        hasRun = false;   
        return;   
    }   
    else if(gyroList.Count == 0)  
    {  
        Echo("No control gyro found");  
        hasRun = false;  
        return;  
    }  
    else  
    {     
        Echo("Ready to run");  
        shooterRefrence = shooterRefrenceList[0] as IMyRemoteControl;   
        missileRefrence = missileRefrenceList[0] as IMyRemoteControl;   
        hasRun = true;   
    }  
}  
 
void GuideMissile()    
{    
//---Get positions of our blocks with relation to world center  
    var originPos = shooterRefrence.GetPosition();   
    var missilePos = missileRefrence.GetPosition();   
 
//---Find current distance from shooter to missile  
    delta_origin = Vector3D.Distance(originPos, missilePos);  
 
//---Check if we are in range      
    if(delta_origin < max_distance) //change this later to be larger  
    {  
    //---Get forward vector from our shooter vessel  
        var shooterForward = shooterRefrence.Position + Base6Directions.GetIntVector(shooterRefrence.Orientation.TransformDirection(Base6Directions.Direction.Forward));    
        var targetVector = shooterRefrence.CubeGrid.GridIntegerToWorld(shooterForward);    
        var targetVectorNorm = Vector3D.Normalize(targetVector - shooterRefrence.GetPosition());   
 
    //---Find vector from shooter to missile  
        var missileVector = Vector3D.Subtract(missilePos, originPos);  
 
    //---Calculate angle between shooter vector and missile vector  
        double dotProduct; Vector3D.Dot(ref targetVectorNorm, ref missileVector, out dotProduct);  
        double x = dotProduct / missileVector.Length();       
        double rawDevAngle = Math.Acos(x) * 180f / Math.PI; //angle between shooter vector and missile  
 
    //---Calculate perpendicular distance from shooter vector  
        var projectionVector = dotProduct * targetVectorNorm;  
        double deviationDistance = Vector3D.Distance(projectionVector,missileVector);  
        Echo("Angular Dev: " + rawDevAngle.ToString());  
 
    //---Determine scaling factor 
        double scalingFactor;  
        if(rawDevAngle < 90)  
        {  
            if(deviationDistance > 200)  
            {  
                scalingFactor = delta_origin; //if we are too far from the beam, dont add any more distance till we are closer  
            }  
            else  
            {  
                scalingFactor = (delta_origin + 200); //travel approx. 200m from current position in direction of target vector  
            }  
        }  
        else  
        {  
            scalingFactor = 200; //if missile is behind the shooter, goes 200m directly infront of shooter for better accuracy  
        }  
        var destination = shooterRefrence.GetPosition() + scalingFactor * targetVectorNorm;   
        Echo(destination.ToString()); //debug  
 
    //---Find front left and top vectors of our missileVector  
        var missileGridX = missileRefrence.Position + Base6Directions.GetIntVector(missileRefrence.Orientation.TransformDirection(Base6Directions.Direction.Forward));  
        var missileWorldX = missileRefrence.CubeGrid.GridIntegerToWorld(missileGridX) - missilePos;  
 
        var missileGridY = missileRefrence.Position + Base6Directions.GetIntVector(missileRefrence.Orientation.TransformDirection(Base6Directions.Direction.Left));  
        var missileWorldY = missileRefrence.CubeGrid.GridIntegerToWorld(missileGridY) - missilePos;  
 
        var missileGridZ = missileRefrence.Position + Base6Directions.GetIntVector(missileRefrence.Orientation.TransformDirection(Base6Directions.Direction.Up));  
        var missileWorldZ = missileRefrence.CubeGrid.GridIntegerToWorld(missileGridZ) - missilePos;  
 
    //---Find vector from missile to destination  
        var shipToTarget = Vector3D.Subtract(destination, missilePos);  
 
    //---Project target vector onto our top left and up vectors  
        double dotX; Vector3D.Dot(ref shipToTarget, ref missileWorldX, out dotX);  
        double dotY; Vector3D.Dot(ref shipToTarget, ref missileWorldY, out dotY);  
        double dotZ; Vector3D.Dot(ref shipToTarget, ref missileWorldZ, out dotZ);  
        var projTargetX = dotX / (missileWorldX.Length() * missileWorldX.Length()) * missileWorldX;  
        var projTargetY = dotY / (missileWorldY.Length() * missileWorldY.Length()) * missileWorldY;  
        var projTargetZ = dotZ / (missileWorldZ.Length() * missileWorldZ.Length()) * missileWorldZ;  
 
    //---Get Yaw and Pitch Angles  
        double angleYaw = Math.Atan(projTargetY.Length() / projTargetX.Length());  
        double anglePitch = Math.Atan(projTargetZ.Length() / projTargetX.Length());  
 
    //---Check if x is positive or negative  
        double checkPositiveX; Vector3D.Dot(ref missileWorldX, ref projTargetX, out checkPositiveX); Echo("check x:" + checkPositiveX.ToString());  
        if(checkPositiveX < 0)  
        {  
            angleYaw += Math.PI/2; //we only change one value so it doesnt spaz  
        }  
 
    //---Check if yaw angle is left or right  
        double checkYaw; Vector3D.Dot(ref missileWorldY, ref projTargetY, out checkYaw); Echo("check yaw:" + checkYaw.ToString());  
        if(checkYaw > 0) //yaw is backwards for what ever reason  
            angleYaw = -angleYaw;  
        Echo("yaw angle:" + angleYaw.ToString());  
 
    //---Check if pitch angle is up or down  
        double checkPitch; Vector3D.Dot(ref missileWorldZ, ref projTargetZ, out checkPitch); Echo("check pitch:" + checkPitch.ToString());  
        if(checkPitch < 0)  
            anglePitch = -anglePitch;  
        Echo("pitch angle:" + anglePitch.ToString());  
 
    //---Angle controller  
        double max_rotation_radians = max_rotation_degrees * (Math.PI / 180); 
        double yawSpeed = max_rotation_radians * angleYaw / Math.Abs(angleYaw); 
        double pitchSpeed = max_rotation_radians * anglePitch / Math.Abs(anglePitch); 
 
        //Alt method 1: Proportional Control 
        //(small ship gyros too weak for this to be effective) 
        /* 
            double yawSpeed = angleYaw / Math.PI * max_rotation_radians;  
            double pitchSpeed = anglePitch / Math.PI * max_rotation_radians; 
        */ 
         
        //Alt method 2: Proportional Control with bounds 
        //(Small gyros still too weak :/) 
            /*if (angleYaw < Math.PI/4)  
            {  
                yawSpeed = angleYaw * max_rotation_radians / (Math.PI/4);  
            }  
            else  
            {  
                yawSpeed = max_rotation_radians;  
            }  
              
            if (anglePitch < Math.PI/4)  
            {  
                pitchSpeed = anglePitch * max_rotation_radians / (Math.PI/4);  
            }  
            else  
            {  
                pitchSpeed = max_rotation_radians;  
            }*/  
 
    //---Set appropriate gyro override 
        for(int i = 0; i < gyroList.Count; i++)  
        {  
            var thisGyro = gyroList[i] as IMyGyro;  
            thisGyro.SetValue<float>("Yaw", (float)yawSpeed);  
            thisGyro.SetValue<float>("Pitch", (float)pitchSpeed);  
            thisGyro.SetValue("Override", true);  
        }  
    }  
    else  
    {  
        Echo("Out of range");  
        for(int i = 0; i < gyroList.Count; i++)  
        {  
            var thisGyro = gyroList[i] as IMyGyro;  
            thisGyro.SetValue<float>("Yaw", 0f);  
            thisGyro.SetValue<float>("Pitch", 0f);  
            thisGyro.SetValue("Override", true);  
        }  
    }  
}  
 
void MissileSystems()               
{             
    GridTerminalSystem.SearchBlocksOfName(mergeName,mergeBlocks);      
    GridTerminalSystem.SearchBlocksOfName(batteryName,batteries); 
  
//---Check if we have merges  
    if(mergeBlocks.Count == 0) 
        Echo("No missile merges found"); 
 
//---Check if we have batteries 
    if(mergeBlocks.Count == 0) 
        Echo("No missile batteries found");  
 
//---Activate battery, detach merge, and activate thrust 
    if (timeElapsed == 0)          
    {          
        for(int i = 0 ; i < batteries.Count ; i++)    
        {    
            var thisBattery = batteries[i] as IMyBatteryBlock;     
            thisBattery.ApplyAction("OnOff_On"); 
            thisBattery.SetValue("Recharge", false);   
            thisBattery.SetValue("Discharge", true); 
        }          
    }    
    else if(timeElapsed == 60) //delay release by 1 second so power systems activate   
    {      
        for(int i = 0 ; i < mergeBlocks.Count ; i++)    
        {    
            var thisMerge = mergeBlocks[i] as IMyShipMergeBlock;    
            thisMerge.ApplyAction("OnOff_Off");    
        }  
        ThrusterOverride();          
        ManeuveringThrust();         
        Echo("Detach Thrust Off");          
        Echo("Thruster Override On");         
    }           
 
    if (timeElapsed < 60) 
        timeElapsed++;          
} 
 
void ManeuveringThrust()         
{         
    if(maneuveringThrusters.Count == 0) 
        Echo("No side thrust found"); 
     
    GridTerminalSystem.SearchBlocksOfName(maneuveringThrustersName,maneuveringThrusters);         
    for(int i = 0 ; i < maneuveringThrusters.Count ; i++)         
    {         
        IMyThrust Thrust = maneuveringThrusters[i] as IMyThrust; 
        Thrust.ApplyAction("OnOff_On");          
    }         
}         
              
void ThrusterOverride()          
{          
    if(forwardThrusters.Count == 0) 
        Echo("No forward thrust found"); 
     
    GridTerminalSystem.SearchBlocksOfName(forwardThrustName,forwardThrusters);               
    for(int i = 0; i < forwardThrusters.Count;i++)               
    {                
        IMyThrust Thrust = forwardThrusters[i] as IMyThrust; 
        Thrust.ApplyAction("OnOff_On"); 
        Thrust.SetValue<float>("Override", float.MaxValue);               
    }           
} 
 
