
/*
/// Whip's Thruster Speed Limiter v3 /// - 9/22/16

________________________________________________________
///DESCRIPTION///

    This code limits your ship's top speed by shutting off any thrusters
    that would speed you up beyond the limit you set. This means that if
    you are traveling backwards at the speed limit, ONLY the thrusters 
    pushing you backwards are shut off. This means that you can use this 
    on planets without fear of your thrusters shutting off ar the wrong time!
    
________________________________________________________
///HOW DO I USE THIS?///

===Required===

    1) Put this code in a program block
    
    2) Make a timer and set it to:
        - "Run with no arguments" the program
        - "Start" itself
        - "Trigger Now" itself
        
    3) Start the timer
    
    4) Enjoy!
    
===Optional===

    - To make the code limit only the thrusters that you specify, set manageAllThrust
      to false and put the name tag "Limit" in the thrusters that you want speed limited

________________________________________________________
///AUTHOR'S NOTES///   

    I made this code a while back to try and avoid the rotor safety lock bugs and decided
    that I might as well release it to the public. If you have any questions, be sure to 
    leave a comment on the workshop page!
    
    - Whiplash141
*/




//------------------------------------------------------
//============ Configurable Variables ================
//------------------------------------------------------

    const double maxSpeed = 98; // this is the max speed that you want your ship to go
    
    bool manageAllThrust = true; //this tells the code whether to limit all thrusters
    
    // If manageAllThrust is set to FALSE, the code will only limit thrusters with the following tag
    //----------------------------------------
        string thrustToManageTag = "Limit";
    //----------------------------------------

//------------------------------------------------------
//============ No touch below this line ================
//------------------------------------------------------

    List<IMyTerminalBlock> shipControllers = new List<IMyTerminalBlock>();
    List<IMyTerminalBlock> thrusters = new List<IMyTerminalBlock>();

    const int updatesPerSecond = 10;
    const double timeMaxCycle = 1 / (double)updatesPerSecond;

    double timeCurrentCycle = 0;

void Main(string arg)
{
    timeCurrentCycle += Runtime.TimeSinceLastRun.TotalSeconds;
    timeSymbol += Runtime.TimeSinceLastRun.TotalSeconds;

    if (timeCurrentCycle >= timeMaxCycle)
    {
        Echo("WMI Thrust Manager Active... " + RunningSymbol());
        ManageThrusters();
        timeCurrentCycle = 0;
    }
}

void ManageThrusters()
{
    bool critError = false;

    GridTerminalSystem.GetBlocksOfType<IMyShipController>(shipControllers);
    if (shipControllers.Count == 0)
    {
        Echo("Critical Error: No ship controllers were found");
        critError = true;
    }

    if (manageAllThrust)
        GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters);
    else
        GridTerminalSystem.SearchBlocksOfName(thrustToManageTag, thrusters, block => block is IMyThrust)
    
    
    if (thrusters.Count == 0)
    {
        Echo("Critical Error: No thrusters were found");
        critError = true;
    }

    if (critError) return;

    var thisController = shipControllers[0] as IMyShipController;

    Vector3D velocityVec = thisController.GetShipVelocities().LinearVelocity;
    double speed = velocityVec.Length();

    Echo("Current Speed: " + speed.ToString() + "\nMax Speed: " + maxSpeed.ToString());

    if (speed >= maxSpeed)
    {
        Echo("Speed is over max...\nManaging thrust");

        foreach (IMyThrust thisThrust in thrusters)
        {
            Vector3D thrustVec = thisThrust.WorldMatrix.Backward;
            bool isSameDirection = VectorSameDirection(thrustVec, velocityVec);

            if (isSameDirection)
            {
                ThrusterTurnOff(thisThrust);
            }
            else
            {
                ThrusterTurnOn(thisThrust);
            }
        }
    }
    else
    {
        Echo("Speed is under max");
        
        for (int i = 0; i < thrusters.Count; i++)
        {
            var thisThrust = thrusters[i] as IMyThrust;
            ThrusterTurnOn(thisThrust);
        }
    }
}

bool VectorSameDirection(Vector3D a, Vector3D b)
{
    double check = a.Dot(b);
    if (check <= 0)
        return false;
    else
        return true;
}

void ThrusterTurnOn(IMyThrust thruster_block)
{
    bool isOn = thruster_block.GetValue<bool>("OnOff");
    if (!isOn)
    {
        thruster_block.ApplyAction("OnOff_On");
    }
}

void ThrusterTurnOff(IMyThrust thruster_block)
{
    bool isOn = thruster_block.GetValue<bool>("OnOff");
    if (isOn)
    {
        thruster_block.ApplyAction("OnOff_Off");
    }
}

//Whip's Running Symbol Method v3
double timeSymbol = 0;
string strRunningSymbol = "";

string RunningSymbol()
{
    if (timeSymbol < .1d)
        strRunningSymbol = "|";
    else if (timeSymbol < .2d)
        strRunningSymbol = "/";
    else if (timeSymbol < .3d)
        strRunningSymbol = "--";
    else if (timeSymbol < .4d)
        strRunningSymbol = "\\";
    else
    {
        timeSymbol = 0;
        strRunningSymbol = "|";
    }

    return strRunningSymbol;
}