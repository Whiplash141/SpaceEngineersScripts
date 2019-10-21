/*
Whip's Guidance Script v1 - revised: 10/15/15

Todo: 
- persistent argument tracking
*/
List<IMyTerminalBlock> remotes = new List<IMyTerminalBlock>(); 
List<IMyTerminalBlock> origin_list = new List<IMyTerminalBlock>(); 
List<IMyTerminalBlock> follower_list = new List<IMyTerminalBlock>(); 
 
string origin_name = "[Origin]"; 
string follower_name = "[Follower]"; 
IMyRemoteControl origin_block; 
IMyRemoteControl follower_block; 
bool hasRun = false; 
double delta_origin; 
int tick_limit = 15; 
int current_tick = 0; 
 
 
void Main(string argument) 
{  
    Echo("Tick: " + current_tick.ToString()); 
    Echo("Has run?: " + hasRun.ToString());
    
    if((current_tick % tick_limit) == 0) 
    { 
        switch(argument.ToLower()) 
        { 
            case "fire": 
                Echo("Guidance Mode"); 
                GuideMissile(); 
                break; 
             
            default: 
                Echo("Idle"); 
                break; 
        } 
        current_tick = 0; 
    } 
     
    current_tick++; 
}     
 
void GuideMissile()  
{  
    GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(remotes);  
    if(hasRun == false) 
    { 
        for(int i = 0 ; i < remotes.Count ; i++) 
        { 
            var thisRemote = remotes[i] as IMyRemoteControl;
            if(thisRemote.CustomName.Contains(origin_name)) 
            {   
                origin_list.Add(thisRemote as IMyRemoteControl); 
                Echo("Found Origin"); 
            } 
             
            if(thisRemote.CustomName.Contains(follower_name)) 
            { 
                follower_list.Add(thisRemote as IMyRemoteControl); 
                Echo("Found Follower"); 
            } 
        } 
//Echo("or " + origin_list.Count + " fol " + follower_list.Count);
        if(origin_list.Count == 0) 
        { 
            Echo("No origin block found"); 
            hasRun = false; 
            return; 
        } 
        else if(follower_list.Count == 0) 
        { 
            Echo("No follower block found"); 
            hasRun = false; 
            return; 
        } 
        else 
        {   
            Echo("REady to run");
            origin_block = origin_list[0] as IMyRemoteControl; 
            follower_block = follower_list[0] as IMyRemoteControl; 
            hasRun = true; 
        }
        
    } 
    else 
    { 
     
        var reference = origin_block; //so messy 
        var forwardPos = reference.Position + Base6Directions.GetIntVector(reference.Orientation.TransformDirection(Base6Directions.Direction.Forward));  
        var forward = reference.CubeGrid.GridIntegerToWorld(forwardPos);  
        var forwardVectorNorm = Vector3D.Normalize(forward - reference.GetPosition()); 
         
        var originPos = origin_block.GetPosition(); 
        var followerPos = follower_block.GetPosition(); 
         
        delta_origin = Vector3D.Distance(originPos, followerPos); 
         
        var destination = reference.GetPosition() + (delta_origin + 200) * forwardVectorNorm; 
         
        Echo(destination.ToString()); //debug 
    } 
}
