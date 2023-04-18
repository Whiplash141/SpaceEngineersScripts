/*   
/ //// / Whip's Subgrid Thruster Manager / //// /  
________________________________________________  
Description:  

    This code allows you to control your thrusters that are attached through rotors, connectors, or pistone!  
________________________________________________  
How do I use this?  

    1) Place a seat or remote on the main grid
        - (Optional) Add the name tag "Reference" to the block name
          if you only want specific ship controllers to be considered.
    2) Place a program block with this code  
    3) Attach your off-grid thrusters however you like.  
    4) Enjoy!  

________________________________________________
Arguments

"on":
    Toggles program control of rotor thrusters on

"off":
    Toggles program control of rotor thrusters off

"toggle":
    Toggles program control of rotor thrusters on/off

"dampeners_on":
    Toggles dampener function of rotor thrust on

"dampeners_off":
    Toggles dampener function of rotor thrust off

"dampeners_toggle" or "dampeners_switch":
    Toggles dampener function of rotor thrust on/off

________________________________________________  
Author's Notes  

    I hope y'all enjoy this code. I hope it makes VTOL and vector thrust craft more feasible :)  

- Whiplash141   
*/

const string VERSION = "42.2.2";
const string DATE = "2023/04/18";

//-----------------------------------------------
//         CONFIGURABLE VARIABLES
//-----------------------------------------------

const string
    IniSectionGeneral = "Subgrid Thruster Config",

    IniKeyDetectOverConnectors = "Detect blocks over connectors",
    IniKeyControlSeatNameTag = "Control seat name tag",
    IniKeyIgnoredNameTag = "Ignored thrust name tag",
    IniKeyUseSubgridThrustForDampening = "Use subgrid thrust as dampeners",
    IniKeyDampenerScalingFactor = "Dampener scaling factor",
    IniKeyFullBurnToleranceAngle = "Full burn tolerance angle (deg)",
    IniKeyMaxThrustAngle = "Max off-axis thrust angle (deg)",
    IniKeyMinDampeningAngle = "Min dampening angle (deg)",
    IniKeyDrawRunningScreens = "Draw running screens",

    IniCommentDetectOverConnectors = " Whether the program should look across connectors\n to detect subgrid thrust",
    IniCommentControlSeatNameTag = " Name tag of the reference ship controllers.\n If no name tagged controllers are found\n all ship controllers will be used.",
    IniCommentIgnoredNameTag = " Name tag of thrusters that the code will ignore",
    IniCommentUseSubgridThrustForDampening = " If the rotor thrusters will be used as inertial dampeners",
    IniCommentDampenerScalingFactor = " This controls how responsive the dampeners are.\n Higher numbers mean quicker response but can\n also lead to oscillations",
    IniCommentFullBurnToleranceAngle = " Max angle (in degrees) that a thruster can be off\n axis of input direction and still receive maximum\n thrust output",
    IniCommentMaxThrustAngle = " Max angle (in degrees) that a thruster can deviate\n from the desired travel direction and still be\n controlled with movement keys",
    IniCommentMinDampeningAngle = " Min angle (in degrees) between a thruster's dampening\n direction and desired move direction that is allowed\n for dampener function",
    IniCommentDrawRunningScreens = " If animated running screen should be drawn";

bool detectBlocksOverConnectors = false;
string controlSeatNameTag = "Reference";
string ignoredThrustNameTag = "Ignore";
bool useRotorThrustAsInertialDampeners = true;
bool manualDampenerOverride = true;
double dampenerScalingFactor = 50;
double fullBurnToleranceAngle = 30;
double maxThrustAngle = 90;
double minDampeningAngle = 75;
bool drawRunningScreens = true;
long lastCubeGridId = -1;

//-----------------------------------------------  
//         No touching below this line  
//-----------------------------------------------  
const double updatesPerSecond = 10; 
bool isSetup = false;
bool turnOn = true;
bool dampenersOn = true;

double maxThrustDotProduct;
double minDampeningDotProduct;
double fullBurnDotProduct;

List<IMyShipController> referenceList;

List<IMyShipController> namedReferences = new List<IMyShipController>();
List<IMyShipController> unnamedReferences = new List<IMyShipController>();

List<IMyThrust> offGridThrust = new List<IMyThrust>();
List<IMyThrust> onGridThrust = new List<IMyThrust>();
List<IMyThrust> allThrust = new List<IMyThrust>();

IMyShipController 
    lastReference = null,
    thisReferenceBlock = null;

Scheduler _scheduler;
ScheduledAction _setupScheduled;
SubgridThrustScreenManager _screenManager;
MyIni _ini = new MyIni();

Program()
{
    _screenManager = new SubgridThrustScreenManager(VERSION, this);
    
    _setupScheduled = new ScheduledAction(GrabBlocks, 0.1);
    
    _scheduler = new Scheduler(this);
    _scheduler.AddScheduledAction(_setupScheduled);
    _scheduler.AddScheduledAction(PrintDetailedInfo, 1);
    _scheduler.AddScheduledAction(HandleSubgridThrust, updatesPerSecond);
    _scheduler.AddScheduledAction(DrawRunningScreen, 6);
    _scheduler.AddScheduledAction(_screenManager.RestartDraw, 1);
    
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    GrabBlocks(); 
    
    maxThrustDotProduct = Math.Cos(maxThrustAngle * Math.PI / 180);
    minDampeningDotProduct = Math.Cos(minDampeningAngle * Math.PI / 180);
    fullBurnDotProduct = Math.Cos(fullBurnToleranceAngle * Math.PI / 180);
}

void Main(string argument, UpdateType updateType)
{
    if ((updateType & (UpdateType.Trigger | UpdateType.Terminal | UpdateType.Script)) != 0)
        ProcessArgument(argument);

    _scheduler.Update();
}

void ProcessIni()
{
    _ini.Clear();
    
    if (_ini.TryParse(Me.CustomData))
    {
        double 
            lastMaxThrust = maxThrustAngle,
            lastMinDampening = minDampeningAngle,
            lastFullBurn = fullBurnToleranceAngle;

        detectBlocksOverConnectors = _ini.Get(IniSectionGeneral, IniKeyDetectOverConnectors).ToBoolean(detectBlocksOverConnectors);
        controlSeatNameTag = _ini.Get(IniSectionGeneral, IniKeyControlSeatNameTag).ToString(controlSeatNameTag);
        ignoredThrustNameTag = _ini.Get(IniSectionGeneral, IniKeyIgnoredNameTag).ToString(ignoredThrustNameTag);
        useRotorThrustAsInertialDampeners = _ini.Get(IniSectionGeneral, IniKeyUseSubgridThrustForDampening).ToBoolean(useRotorThrustAsInertialDampeners);
        dampenerScalingFactor = _ini.Get(IniSectionGeneral, IniKeyDampenerScalingFactor).ToDouble(dampenerScalingFactor);
        fullBurnToleranceAngle = _ini.Get(IniSectionGeneral, IniKeyFullBurnToleranceAngle).ToDouble(fullBurnToleranceAngle);
        maxThrustAngle = _ini.Get(IniSectionGeneral, IniKeyMaxThrustAngle).ToDouble(maxThrustAngle);
        minDampeningAngle = _ini.Get(IniSectionGeneral, IniKeyMinDampeningAngle).ToDouble(minDampeningAngle);
        drawRunningScreens = _ini.Get(IniSectionGeneral, IniKeyDrawRunningScreens).ToBoolean(drawRunningScreens);
        
        if (lastMaxThrust != maxThrustAngle)
        {
            maxThrustDotProduct = Math.Cos(maxThrustAngle * Math.PI / 180);            
        }
        
        if (lastMinDampening != minDampeningAngle)
        {
            minDampeningDotProduct = Math.Cos(minDampeningAngle * Math.PI / 180);            
        }
        
        if (lastFullBurn != fullBurnToleranceAngle)
        {
            fullBurnDotProduct = Math.Cos(fullBurnToleranceAngle * Math.PI / 180);            
        }
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }
    
    _ini.Set(IniSectionGeneral, IniKeyDetectOverConnectors, detectBlocksOverConnectors);
    _ini.Set(IniSectionGeneral, IniKeyControlSeatNameTag, controlSeatNameTag);
    _ini.Set(IniSectionGeneral, IniKeyIgnoredNameTag, ignoredThrustNameTag);
    _ini.Set(IniSectionGeneral, IniKeyUseSubgridThrustForDampening, useRotorThrustAsInertialDampeners);
    _ini.Set(IniSectionGeneral, IniKeyDampenerScalingFactor, dampenerScalingFactor);
    _ini.Set(IniSectionGeneral, IniKeyFullBurnToleranceAngle, fullBurnToleranceAngle);
    _ini.Set(IniSectionGeneral, IniKeyMaxThrustAngle, maxThrustAngle);
    _ini.Set(IniSectionGeneral, IniKeyMinDampeningAngle, minDampeningAngle);
    _ini.Set(IniSectionGeneral, IniKeyDrawRunningScreens, drawRunningScreens);
    
    _ini.SetComment(IniSectionGeneral, IniKeyDetectOverConnectors, IniCommentDetectOverConnectors);
    _ini.SetComment(IniSectionGeneral, IniKeyControlSeatNameTag, IniCommentControlSeatNameTag);
    _ini.SetComment(IniSectionGeneral, IniKeyIgnoredNameTag, IniCommentIgnoredNameTag);
    _ini.SetComment(IniSectionGeneral, IniKeyUseSubgridThrustForDampening, IniCommentUseSubgridThrustForDampening);
    _ini.SetComment(IniSectionGeneral, IniKeyDampenerScalingFactor, IniCommentDampenerScalingFactor);
    _ini.SetComment(IniSectionGeneral, IniKeyFullBurnToleranceAngle, IniCommentFullBurnToleranceAngle);
    _ini.SetComment(IniSectionGeneral, IniKeyMaxThrustAngle, IniCommentMaxThrustAngle);
    _ini.SetComment(IniSectionGeneral, IniKeyMinDampeningAngle, IniCommentMinDampeningAngle);
    _ini.SetComment(IniSectionGeneral, IniKeyDrawRunningScreens, IniCommentDrawRunningScreens);

    string output = _ini.ToString();
    if (output != Me.CustomData)
    {
        Me.CustomData = output;
    }
}

void DrawRunningScreen()
{
    if (drawRunningScreens)
    {
        _screenManager.Draw();
    }
}

StringBuilder _detailedInfo = new StringBuilder();
void PrintDetailedInfo()
{
    _detailedInfo.Append($"Whip's Subgrid Thruster Manager\n(Version {VERSION} - {DATE})\n\n");
    string codeStatus = turnOn ? "Enabled" : "Disabled";
    string dampenerStatus = dampenersOn ? "Enabled" : "Disabled";
    _detailedInfo.Append($"Code status: {codeStatus}\nDampeners: {dampenerStatus}\n\n");
    
    _detailedInfo.Append($"Reference controller:\n   '{(thisReferenceBlock == null ? "(null)" : thisReferenceBlock.CustomName)}'\n");
    _detailedInfo.Append(
        namedReferences.Count != 0
            ? "   > Using name tagged ship controllers.\n"
                : "   > Using all ship controllers.\n");
    
    _detailedInfo.Append($"Total Subgrid Thrusters: {offGridThrust.Count}\n");
    int workingThrusters = offGridThrust.Count(x => x.IsWorking);
    _detailedInfo.Append($"Active Subgrid Thrusters: {workingThrusters}\n\n");
    _detailedInfo.Append($"Next block refresh in {Math.Max(0, _setupScheduled.RunInterval - _setupScheduled.TimeSinceLastRun):N0} seconds\n\n");

    Echo(_detailedInfo.ToString());
    _detailedInfo.Clear();
}

void HandleSubgridThrust()
{
    if (!isSetup)
        return;

    //Gets reference block that is under control 
    thisReferenceBlock = GetControlledShipController(referenceList, lastReference);
    if (thisReferenceBlock == null)
    {
        if (lastReference != null)
            thisReferenceBlock = lastReference;
        else
            thisReferenceBlock = referenceList[0];
    }
    
    GetOffGridThrust(thisReferenceBlock.CubeGrid, allThrust, offGridThrust, onGridThrust);

    var shipSpeed = thisReferenceBlock.GetShipSpeed();
    var travelVec = thisReferenceBlock.GetShipVelocities().LinearVelocity;
    if (travelVec.LengthSquared() > 0)
    {
        travelVec = Vector3D.Normalize(travelVec);
    }

    //Desired travel vector construction
    var inputVec = thisReferenceBlock.MoveIndicator; //raw input vector     
    var desiredDirection = Vector3D.TransformNormal(inputVec, thisReferenceBlock.WorldMatrix); //world relative input vector
    if (desiredDirection.LengthSquared() > 0)
    {
        desiredDirection = Vector3D.Normalize(desiredDirection);
    }

    dampenersOn = useRotorThrustAsInertialDampeners && thisReferenceBlock.DampenersOverride;
    if (onGridThrust.Count == 0)
    {
        dampenersOn = useRotorThrustAsInertialDampeners && manualDampenerOverride;
    }

    if (dampenersOn)
    {
        CancelGravity(offGridThrust, onGridThrust, thisReferenceBlock);
    }

    ApplyThrust(offGridThrust, travelVec, shipSpeed, desiredDirection, dampenersOn, turnOn);
}

void GetOffGridThrust(IMyCubeGrid grid, List<IMyThrust> sourceList, List<IMyThrust> offGridList, List<IMyThrust> onGridList)
{
    if (lastCubeGridId == grid.EntityId)
    {
        // We have already processed off-grid gyros for this grid
        return;
    }
    
    offGridList.Clear();
    onGridList.Clear();
    foreach (var t in sourceList)
    {
        if (!GridTerminalSystem.CanAccess(t))
        {
            continue;
        }
        if (grid != t.CubeGrid && !t.CustomName.Contains(ignoredThrustNameTag))
        {
            offGridList.Add(t);
        }
        else
        {
            onGridList.Add(t);
            t.ThrustOverridePercentage = 0f;
        }
    }
    lastCubeGridId = grid.EntityId;
}

void ProcessArgument(string arg)
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
            turnOn = !turnOn;
            break;
        case "dampeners_on":
            manualDampenerOverride = true;
            break;
        case "dampeners_off":
            manualDampenerOverride = false;
            break;
        case "dampeners_toggle":
        case "dampeners_switch":
            manualDampenerOverride = !manualDampenerOverride;
            break;
    }
}

bool ShouldGetBlock(IMyTerminalBlock block)
{
    if (detectBlocksOverConnectors)
        return true;
    else
        return Me.IsSameConstructAs(block);
}

bool CollectBlocks(IMyTerminalBlock b)
{
    if (!ShouldGetBlock(b))
    {
        return false;
    }

    if (b is IMyShipController)
    {
        var sc = (IMyShipController)b;
        if (sc.CustomName.ToLower().Contains(controlSeatNameTag.ToLower()))
        {
            namedReferences.Add(sc);
        }
        else
        {
            unnamedReferences.Add(sc);
        }
    }
    else if (b is IMyThrust)
    {
        var t = (IMyThrust)b;
        allThrust.Add(t);
    }
    
    return false;
}

void GrabBlocks()
{
    ProcessIni();
    
    lastCubeGridId = -1;
    unnamedReferences.Clear();
    namedReferences.Clear();
    allThrust.Clear();
    
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, CollectBlocks);

    isSetup = true;

    if (namedReferences.Count == 0 && unnamedReferences.Count == 0)
    {
        Echo($">> Error: No remote or control seat with name\ntag '{controlSeatNameTag}'!");
        isSetup = false;
    }
    else if (namedReferences.Count != 0)
    {
        referenceList = namedReferences;
    }
    else if (unnamedReferences.Count != 0)
    {
        referenceList = unnamedReferences;
    }
}

IMyShipController GetControlledShipController(List<IMyShipController> controllers, IMyShipController lastControlled = null)
{
    /*
    Priority:
    1. Main controller
    2. Oldest controlled ship controller
    */
    IMyShipController firstControlled = null;
    foreach (IMyShipController ctrl in controllers)
    {
        if (ctrl.IsMainCockpit)
        {
            return ctrl;
        }

        if (ctrl.IsUnderControl && ctrl.CanControlShip)
        {
            // Grab the first seat that has a player sitting in it
            // and save it away in-case we don't have a main contoller
            if (firstControlled == null)
            {
                firstControlled = ctrl;
            }
        }
    }
    
    // We did not find a main controller, so if the first controlled controller
    // from last cycle if it is still controlled
    if (lastControlled != null && (lastControlled.IsUnderControl && lastControlled.CanControlShip))
    {
        return lastControlled;
    }

    // Otherwise we return the first ship controller that we 
    // found that was controlled.
    return firstControlled;
}

List<IMyThrust> upwardThrusters = new List<IMyThrust>();

const double sqrt2Inv = 1 / MathHelper.Sqrt2;

void CancelGravity(List<IMyThrust> offGridThrusters, List<IMyThrust> onGridThrusters, IMyShipController controller)
{
    var gravityVec = controller.GetNaturalGravity();

    if (Vector3D.IsZero(gravityVec))
    {
        foreach (var block in offGridThrusters)
        {
            if (!GridTerminalSystem.CanAccess(block)) { continue; }
            SetThrusterOverride(block, 0f); //.0001f
        }
        return;
    }

    upwardThrusters.Clear();

    var gravityVecNorm = Vector3D.Normalize(gravityVec);
    var mass = controller.CalculateShipMass().PhysicalMass;
    var velocityDot = gravityVecNorm.Dot(controller.GetShipVelocities().LinearVelocity);
    double num = MathHelper.Clamp(velocityDot / 5.0 + 1.0, 0.0, 1.0);

    var requiredHoverForce = mass * gravityVec.Length() * num;
    //Echo($"requiredHoverForce: {requiredHoverForce/num:n1}");
    //Echo($"velocityDot: {velocityDot}\nnum: {num}");

    double maxThrustSum = 0;
    foreach (var block in offGridThrusters)
    {
        if (!GridTerminalSystem.CanAccess(block)) { continue; }
        if (!block.IsWorking || !block.Enabled)
            continue;

        var dot = block.WorldMatrix.Forward.Dot(gravityVecNorm);
        if (dot > sqrt2Inv && dot < 1)
        {
            upwardThrusters.Add(block);
            maxThrustSum += dot * block.MaxEffectiveThrust;
        }
        else
        {
            SetThrusterOverride(block, 0f); //this should be sorted by the next method
        }
    }

    foreach (var block in onGridThrusters)
    {
        if (!block.IsWorking)
            continue;

        var dot = block.WorldMatrix.Forward.Dot(gravityVecNorm);
        if (dot > 0)
            requiredHoverForce -= dot * block.CurrentThrust;
    }

    requiredHoverForce = Math.Max(requiredHoverForce, 0);
    //Echo($"requiredHoverForce: {requiredHoverForce:n1}");

    //Echo($"requiredHoverForce: {requiredHoverForce:N1}" );
    //Echo($"maxThrustSum: {maxThrustSum:N1}" );

    var thrustProportion = requiredHoverForce / maxThrustSum;
    //Echo($"Hover Thrust Output: {(thrustProportion * 100):N1}%");
    foreach (var block in upwardThrusters)
    {
        if (!GridTerminalSystem.CanAccess(block)) { continue; }
        SetThrusterOverride(block, 100f * (float)thrustProportion);
    }
}

void ApplyThrust(List<IMyThrust> thrusters, Vector3D travelVec, double speed, Vector3D desiredDirectionVec, bool dampenersOn, bool turnOn)
{
    if (!turnOn)
    {
        foreach (IMyThrust thisThrust in thrusters)
        {
            if (!GridTerminalSystem.CanAccess(thisThrust)) { continue; }
            SetThrusterOverride(thisThrust, 0.000001f);
        }
        return;
    }

    foreach (IMyThrust thisThrust in thrusters)
    {
        if (!GridTerminalSystem.CanAccess(thisThrust)) { continue; }
        var thrustDirection = thisThrust.WorldMatrix.Forward; //gets the direction that the thruster flame fires
        float scale = -(float)thrustDirection.Dot(desiredDirectionVec); //projection of the thruster's direction onto the desired direction 

        if (scale > maxThrustDotProduct)
        {
            scale /= (float)fullBurnDotProduct; //scales it so that the thruster output ramps down after the fullBurnToleranceAngle is exceeded

            //Dampener approximations
            var velocityInThrustDirection = thrustDirection.Dot(travelVec) * speed;
            double targetOverride = 0;
            targetOverride = velocityInThrustDirection * dampenerScalingFactor;

            if (dampenersOn)
                SetThrusterOverride(thisThrust, (float)Math.Max(scale * 100f, targetOverride + GetThrusterOverride(thisThrust)));
            else
                SetThrusterOverride(thisThrust, (float)(scale * 100f));
        }
        /* Dampener approximations
         * Checks if :
         * - dampeners are allowed
         * - thruster is opposing the motion of the vessel
         * - thruster is within the dampening angle tolerance
         */
        else if (dampenersOn && thrustDirection.Dot(travelVec) > 0 && thrustDirection.Dot(desiredDirectionVec) <= minDampeningDotProduct)
        {
            var velocityInThrustDirection = thrustDirection.Dot(travelVec) * speed;
            double targetOverride = 0;
            targetOverride = velocityInThrustDirection * dampenerScalingFactor;
            SetThrusterOverride(thisThrust, (float)targetOverride + GetThrusterOverride(thisThrust));
        }
        else //disables thruster
        {
            if (!upwardThrusters.Contains(thisThrust) || thrustDirection.Dot(desiredDirectionVec) > minDampeningDotProduct || !dampenersOn)
                SetThrusterOverride(thisThrust, 0.000001f);
        }
    }
}

void SetThrusterOverride(IMyThrust thruster, float overrideValue)
{
    thruster.ThrustOverridePercentage = overrideValue * 0.01f;
    //thruster.Enabled = true;  
}

float GetThrusterOverride(IMyThrust thruster)
{
    return thruster.ThrustOverridePercentage * 100f;
}

#region Scheduler
/// <summary>
/// Class for scheduling actions to occur at specific frequencies. Actions can be updated in parallel or in sequence (queued).
/// </summary>
public class Scheduler
{
    public double CurrentTimeSinceLastRun = 0;
    
    ScheduledAction _currentlyQueuedAction = null;
    bool _firstRun = true;
    bool _inUpdate = false;
    
    readonly bool _ignoreFirstRun;
    readonly List<ScheduledAction> _actionsToAdd = new List<ScheduledAction>();
    readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
    readonly List<ScheduledAction> _actionsToDispose = new List<ScheduledAction>();
    readonly Queue<ScheduledAction> _queuedActions = new Queue<ScheduledAction>();
    readonly Program _program;
    
    const double RUNTIME_TO_REALTIME = (1.0 / 60.0) / 0.0166666;

    /// <summary>
    /// Constructs a scheduler object with timing based on the runtime of the input program.
    /// </summary>
    public Scheduler(Program program, bool ignoreFirstRun = false)
    {
        _program = program;
        _ignoreFirstRun = ignoreFirstRun;
    }

    /// <summary>
    /// Updates all ScheduledAcions in the schedule and the queue.
    /// </summary>
    public void Update()
    {
        _inUpdate = true;
        double deltaTime = Math.Max(0, _program.Runtime.TimeSinceLastRun.TotalSeconds * RUNTIME_TO_REALTIME);
        
        if (_ignoreFirstRun && _firstRun)
            deltaTime = 0;

        _firstRun = false;
        _actionsToDispose.Clear();
        foreach (ScheduledAction action in _scheduledActions)
        {
            CurrentTimeSinceLastRun = action.TimeSinceLastRun + deltaTime;
            action.Update(deltaTime);
            if (action.JustRan && action.DisposeAfterRun)
            {
                _actionsToDispose.Add(action);
            }
        }

        // Remove all actions that we should dispose
        _scheduledActions.RemoveAll((x) => _actionsToDispose.Contains(x));

        if (_currentlyQueuedAction == null)
        {
            // If queue is not empty, populate current queued action
            if (_queuedActions.Count != 0)
                _currentlyQueuedAction = _queuedActions.Dequeue();
        }

        // If queued action is populated
        if (_currentlyQueuedAction != null)
        {
            _currentlyQueuedAction.Update(deltaTime);
            if (_currentlyQueuedAction.JustRan)
            {
                // Set the queued action to null for the next cycle
                _currentlyQueuedAction = null;
            }
        }
        _inUpdate = false;
        
        if (_actionsToAdd.Count > 0)
        {
            _scheduledActions.AddRange(_actionsToAdd);
            _actionsToAdd.Clear();
        }
    }

    /// <summary>
    /// Adds an Action to the schedule. All actions are updated each update call.
    /// </summary>
    public void AddScheduledAction(Action action, double updateFrequency, bool disposeAfterRun = false, double timeOffset = 0)
    {
        ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun, timeOffset);
        if (!_inUpdate)
            _scheduledActions.Add(scheduledAction);
        else
            _actionsToAdd.Add(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the schedule. All actions are updated each update call.
    /// </summary>
    public void AddScheduledAction(ScheduledAction scheduledAction)
    {
        if (!_inUpdate)
            _scheduledActions.Add(scheduledAction);
        else
            _actionsToAdd.Add(scheduledAction);
    }

    /// <summary>
    /// Adds an Action to the queue. Queue is FIFO.
    /// </summary>
    public void AddQueuedAction(Action action, double updateInterval)
    {
        if (updateInterval <= 0)
        {
            updateInterval = 0.001; // avoids divide by zero
        }
        ScheduledAction scheduledAction = new ScheduledAction(action, 1.0 / updateInterval, true);
        _queuedActions.Enqueue(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the queue. Queue is FIFO.
    /// </summary>
    public void AddQueuedAction(ScheduledAction scheduledAction)
    {
        _queuedActions.Enqueue(scheduledAction);
    }
}

public class ScheduledAction
{
    public bool JustRan { get; private set; } = false;
    public bool DisposeAfterRun { get; private set; } = false;
    public double TimeSinceLastRun { get; private set; } = 0;
    public double RunInterval
    {
        get
        {
            return _runInterval;
        }
        set
        {
            if (value == _runInterval)
                return;

            _runInterval = value < Epsilon ? 0 : value;
            _runFrequency = value == 0 ? double.MaxValue : 1.0 / _runInterval;
        }
    }
    public double RunFrequency
    {
        get
        {
            return _runFrequency;
        }
        set
        {
            if (value == _runFrequency)
                return;

            if (value == 0)
                RunInterval = double.MaxValue;
            else
                RunInterval = 1.0 / value;
        }
    }

    double _runInterval = -1e9;
    double _runFrequency = -1e9;
    readonly Action _action;

    const double Epsilon = 1e-12;

    /// <summary>
    /// Class for scheduling an action to occur at a specified frequency (in Hz).
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <param name="runFrequency">How often to run in Hz</param>
    public ScheduledAction(Action action, double runFrequency = 0, bool removeAfterRun = false, double timeOffset = 0)
    {
        _action = action;
        RunFrequency = runFrequency; // Implicitly sets RunInterval
        DisposeAfterRun = removeAfterRun;
        TimeSinceLastRun = timeOffset;
    }

    public void Update(double deltaTime)
    {
        TimeSinceLastRun += deltaTime;

        if (TimeSinceLastRun + Epsilon >= RunInterval)
        {
            _action.Invoke();
            TimeSinceLastRun = 0;

            JustRan = true;
        }
        else
        {
            JustRan = false;
        }
    }
}
#endregion

#region Screen Manager
class SubgridThrustScreenManager
{
    readonly Color _topBarColor = new Color(25, 25, 25);
    readonly Color _white = new Color(200, 200, 200);
    readonly Color _pressedColor = new Color(100, 100, 100);
    readonly Color _thrustFlameColor = new Color(0,128,255,255);
    readonly Color _black = Color.Black;

    const TextAlignment Center = TextAlignment.CENTER;
    const SpriteType Texture = SpriteType.TEXTURE;
    const float TitleBarHeightPx = 64f;
    const float TextSize = 1.3f;
    const float BaseTextHeightPx = 37f;
    const string Font = "Debug";
    const string TitleFormat = "Whip's Subgrid Thrust - v{0}";
    readonly string _titleText;

    Program _program;
    int _idx = 0;
    
    enum WasdKey { None, W, A, S, D }
    const float RotorScale = 0.7f;
    const float WasdScale = 1.4f;
    readonly Vector2 _rotorPosition = new Vector2(60, 60);
    readonly Vector2 _thrustPosition  = new Vector2(60, -20);
    readonly Vector2 _wasdPosition  = new Vector2(-120, 60);

    struct AnimationParams
    {
        public readonly WasdKey PressedKey;
        public readonly float FlameWidthScale;

        public AnimationParams(WasdKey pressedKey, float flameWidthScale)
        {
            PressedKey = pressedKey;
            FlameWidthScale = flameWidthScale;
        }
    }

    AnimationParams[] _animSequence = new AnimationParams[] {
        new AnimationParams(WasdKey.A, 1f),
        new AnimationParams(WasdKey.A, 1.2f),
        new AnimationParams(WasdKey.A, 1.1f),
        new AnimationParams(WasdKey.A, 1.4f),
        new AnimationParams(WasdKey.A, 1.3f),
        new AnimationParams(WasdKey.A, 1.2f),
        new AnimationParams(WasdKey.None, 0f),
        new AnimationParams(WasdKey.None, 0f),
        new AnimationParams(WasdKey.None, 0f),
        new AnimationParams(WasdKey.None, 0f),
        new AnimationParams(WasdKey.None, 0f),
        new AnimationParams(WasdKey.None, 0f),
    };

    bool _clearSpriteCache = false;
    IMyTextSurface _surface = null;

    public SubgridThrustScreenManager(string version, Program program)
    {
        _titleText = string.Format(TitleFormat, version);
        _program = program;
        _surface = _program.Me.GetSurface(0);
    }

    public void Draw()
    {
        if (_surface == null)
            return;

        AnimationParams anim = _animSequence[_idx];
        _idx = ++_idx % _animSequence.Length;
        
        SetupDrawSurface(_surface);
        Vector2 screenCenter = _surface.TextureSize * 0.5f;
        Vector2 scale = _surface.SurfaceSize / 512f;
        float minScale = Math.Min(scale.X, scale.Y);

        using (var frame = _surface.DrawFrame())
        {
            if (_clearSpriteCache)
            {
                frame.Add(new MySprite());
            }
            
            Vector2 rotorPos = _rotorPosition * minScale + screenCenter;
            Vector2 thrustPos = _thrustPosition * minScale + screenCenter;
            Vector2 wasdPos = _wasdPosition * minScale + screenCenter;
            
            DrawWasdIcon(frame, wasdPos, anim.PressedKey, WasdScale * minScale);
            DrawRotor(frame, rotorPos, RotorScale * minScale);
            DrawThruster(frame, thrustPos, RotorScale * minScale, anim.FlameWidthScale);

            DrawTitleBar(_surface, frame, minScale);
        }
    }

    public void RestartDraw()
    {
        _clearSpriteCache = !_clearSpriteCache;
    }

    #region Draw Helper Functions
    void DrawTitleBar(IMyTextSurface surface, MySpriteDrawFrame frame, float scale)
    {
        float titleBarHeight = scale * TitleBarHeightPx;
        Vector2 topLeft = 0.5f * (surface.TextureSize - surface.SurfaceSize);
        Vector2 titleBarSize = new Vector2(surface.TextureSize.X, titleBarHeight);
        Vector2 titleBarPos = topLeft + new Vector2(surface.TextureSize.X * 0.5f, titleBarHeight * 0.5f);
        Vector2 titleBarTextPos = topLeft + new Vector2(surface.TextureSize.X * 0.5f, 0.5f * (titleBarHeight - scale * BaseTextHeightPx));

        // Title bar
        frame.Add(new MySprite(
            Texture,
            "SquareSimple",
            titleBarPos,
            titleBarSize,
            _topBarColor,
            null,
            Center));

        // Title bar text
        frame.Add(new MySprite(
            SpriteType.TEXT,
            _titleText,
            titleBarTextPos,
            null,
            _white,
            Font,
            Center,
            TextSize * scale));
    }

    void SetupDrawSurface(IMyTextSurface surface)
    {
        surface.ScriptBackgroundColor = _black;
        surface.ContentType = ContentType.SCRIPT;
        surface.Script = "";
    }

    void DrawWasdIcon(MySpriteDrawFrame frame, Vector2 centerPos, WasdKey pressedKey = WasdKey.None, float scale = 1f)
    {
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(55f,0f)*scale+centerPos, new Vector2(50f,50f)*scale, pressedKey == WasdKey.D ? _pressedColor : _white, null, TextAlignment.CENTER, 0f)); // d key
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,0f)*scale+centerPos, new Vector2(50f,50f)*scale, pressedKey == WasdKey.S ? _pressedColor : _white, null, TextAlignment.CENTER, 0f)); // s key
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-55f,0f)*scale+centerPos, new Vector2(50f,50f)*scale, pressedKey == WasdKey.A ? _pressedColor : _white, null, TextAlignment.CENTER, 0f)); // a key
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,-55f)*scale+centerPos, new Vector2(50f,50f)*scale, pressedKey == WasdKey.W ? _pressedColor : _white, null, TextAlignment.CENTER, 0f)); // w key
        frame.Add(new MySprite(SpriteType.TEXT, "D", new Vector2(47f,-15f)*scale+centerPos, null, _black, "DEBUG", TextAlignment.LEFT, 1f*scale)); // d
        frame.Add(new MySprite(SpriteType.TEXT, "S", new Vector2(-9f,-15f)*scale+centerPos, null, _black, "DEBUG", TextAlignment.LEFT, 1f*scale)); // s
        frame.Add(new MySprite(SpriteType.TEXT, "A", new Vector2(-65f,-15f)*scale+centerPos, null, _black, "DEBUG", TextAlignment.LEFT, 1f*scale)); // a
        frame.Add(new MySprite(SpriteType.TEXT, "W", new Vector2(-13f,-68f)*scale+centerPos, null, _black, "DEBUG", TextAlignment.LEFT, 1f*scale)); // w
    }
    
    void DrawRotor(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,-15f)*scale+centerPos, new Vector2(80f,10f)*scale, _white, null, TextAlignment.CENTER, 0f)); // stator top
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,20f)*scale+centerPos, new Vector2(100f,60f)*scale, _white, null, TextAlignment.CENTER, 0f)); // stator
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,-35f)*scale+centerPos, new Vector2(40f,20f)*scale, _white, null, TextAlignment.CENTER, 0f)); // rotor shaft
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,-50f)*scale+centerPos, new Vector2(100f,10f)*scale, _white, null, TextAlignment.CENTER, 0f)); // rotor top
    }

    void DrawThruster(MySpriteDrawFrame frame, Vector2 centerPos, float scale, float flameScale)
    {
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,0f)*scale+centerPos, new Vector2(100f,100f)*scale, _white, null, TextAlignment.CENTER, 0f)); // thruster base
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(85f,0f)*scale+centerPos, new Vector2(60f,80f)*scale, _white, null, TextAlignment.CENTER, 0f)); // thruster shaft
        frame.Add(new MySprite(SpriteType.TEXTURE, "SemiCircle", new Vector2(113f,0f)*scale+centerPos, new Vector2(80f,120f)*scale, _white, null, TextAlignment.CENTER, 1.5708f)); // thruster nozzle
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(200f,0f)*scale+centerPos, new Vector2(100f,100f)*scale, _black, null, TextAlignment.CENTER, 0f)); // nozzle mask
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(175f,0f)*scale+centerPos, new Vector2(150f*flameScale,50f)*scale, _thrustFlameColor, null, TextAlignment.CENTER, 0f)); // flame
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(110f,0f)*scale+centerPos, new Vector2(80f,60f)*scale, _white, null, TextAlignment.CENTER, 0f)); // flame mask
    }

    #endregion
}
#endregion
