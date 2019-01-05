
/*
/// Whip's Mouse-Aimed Rotor Turret Script v37-1 - 01.05.2019 
*/

//=============================================================
//DO NOT CHANGE VARIABLES HERE!
//CHANGE THEM IN THE CUSTOM DATA OF THIS PROGRAM THEN RECOMPILE!
//=============================================================

//name tag of turret groups
string groupNameTag = "MART";

//name of elevation (vertical) rotor for specific turret
string elevationRotorNameTag = "Elevation";

//name of azimuth (horizontal) rotor for specific turret
string azimuthRotorNameTag = "Azimuth";

//scales mouse input by this factor for elevation rotors
double elevationSpeedModifier = 0.25;

//scales mouse input by this factor for azimuth rotors
double azimuthSpeedModifier = 0.25;

//If the turret should attempt to cancel out unwanted angular velocity
bool stabilizeAzimuth = true;

//If the turret should attempt to cancel out unwanted angular velocity
bool stabilizeElevation = true;

//If the mouse controls should be relative to the cockpit's orientation
bool controlSeatRelativeMouseControl = true;

//If the code should fire detected weapons when you press the crouch key
bool fireWeaponsOnCrouch = true;

//If the turret should return to a rest position if not actively controlled
bool returnToRestPosition = true;


////////////////////////////////////////////////////
//=================================================
//No touchey anything below here
//=================================================
////////////////////////////////////////////////////
const double updatesPerSecond = 60;
const double timeMax = 1 / updatesPerSecond;
const double secondsPerTick = 1.0 / 60.0;
const double equilibriumRotationSpeed = 10;
const double runtimeToRealTime = 1.0 / 0.96;
double timeElapsed = 0;
const double refreshInterval = 10;
double timeSinceRefresh = 141;
bool hasTurrets = false;


Program()
{
    BuildConfig();
    config.ParseCustomData();

    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

void Main(string arg, UpdateType updateType)
{
    //-------------------------------------
    //Bandaid for DS bug
    //if ((updateType & UpdateType.Once) != 0)
    //{
    //    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    //}
    //-------------------------------------
    
    if ((updateType & (UpdateType.Terminal | UpdateType.Script | UpdateType.Trigger)) != 0)
    {
        if (arg.Equals("setup", StringComparison.OrdinalIgnoreCase))
        {
            config.ParseCustomData();
            hasTurrets = GrabBlockGroups();
            timeSinceRefresh = 0;
        }
        
        else if (arg.Equals("rest", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var turret in rotorTurretList)
                turret.GoToRest();
        }
    }

    var lastRuntime = runtimeToRealTime * Math.Max(Runtime.TimeSinceLastRun.TotalSeconds, 0);
    timeElapsed += lastRuntime; //secondsPerTick;
    timeSinceRefresh += lastRuntime; //secondsPerTick;

    if (timeElapsed >= timeMax)
    {
        Echo("WMI Mouse-Aimed Rotor\nTurret Systems\nOnline... " + RunningSymbol());

        if (!hasTurrets || timeSinceRefresh >= refreshInterval) //check if we are not setup or if we have hit our refresh interval
        {
            hasTurrets = GrabBlockGroups();
            timeSinceRefresh = 0;
        }

        if (!hasTurrets) //if setup has failed
            return;

        Echo($"\nNext block refresh in {Math.Round(Math.Max(0, refreshInterval - timeSinceRefresh))} seconds");

        try
        {    
            ControlTurrets();
        }
        catch
        {
            Echo("Something broke yo");
            hasTurrets = false;
        }

        //reset time count
        timeElapsed = 0;
    }

}

List<RotorTurret> rotorTurretList = new List<RotorTurret>();

bool GrabBlockGroups()
{
    config.ParseCustomData();
    rotorTurretList.Clear();

    var groups = new List<IMyBlockGroup>();
    GridTerminalSystem.GetBlockGroups(groups);

    foreach (IMyBlockGroup thisGroup in groups)
    {
        if (thisGroup.Name.ToLower().Contains(groupNameTag.ToLower()))
        {
            var thisTurret = new RotorTurret(thisGroup, stabilizeAzimuth, stabilizeElevation, azimuthRotorNameTag, elevationRotorNameTag, azimuthSpeedModifier, elevationSpeedModifier, fireWeaponsOnCrouch,  controlSeatRelativeMouseControl, returnToRestPosition, timeMax, this);
            rotorTurretList.Add(thisTurret);
        }
    }

    if (rotorTurretList.Count == 0)
    {
        Echo($"[ERROR]: No '{groupNameTag}' groups found!");
        return false;
    }
    return true;
}

void ControlTurrets()
{
    foreach (var turret in rotorTurretList)
    {
        turret.Control();
    }
}

public class RotorTurret
{
    IMyMotorStator elevationRotor;
    List<IMyMotorStator> additionalElevationRotors = new List<IMyMotorStator>();
    IMyMotorStator azimuthRotor;
    List<IMyShipController> shipControllers = new List<IMyShipController>();
    List<IMyTerminalBlock> weaponsAndTools = new List<IMyTerminalBlock>(); //need to clear this
    List<IMyTerminalBlock> additionalWeaponsAndTools = new List<IMyTerminalBlock>();
    List<IMyGyro> gyros = new List<IMyGyro>();
    List<IMyGyro> gridGyros = new List<IMyGyro>();
    IMyBlockGroup thisGroup;
    public bool IsSetup { get; private set; }
    Program thisProgram;
    bool fireWeaponsOnCrouch;
    bool controlSeatRelativeMouseControl;
    string azimuthRotorNameTag;
    string elevationRotorNameTag;
    double elevationSpeedModifier;
    double azimuthSpeedModifier;
    double timeInterval;
    const double radsToRPM = 30.0 / Math.PI;
    MatrixD lastAzimuthMatrix;
    MatrixD lastElevationMatrix;
    bool firstRun = true;
    bool shouldStabilizeAz;
    bool shouldStabilizeEl;
    bool returnToRestPosition;
    bool _commandRest = false;

    public RotorTurret(IMyBlockGroup group, bool shouldStabilizeAz, bool shouldStabilizeEl, string azimuthRotorNameTag, string elevationRotorNameTag, double azimuthSpeedModifier, double elevationSpeedModifier, bool fireWeaponsOnCrouch, bool controlSeatRelativeMouseControl, bool returnToRestPosition, double timeInterval, Program program)
    {
        thisGroup = group;
        thisProgram = program;
        this.fireWeaponsOnCrouch = fireWeaponsOnCrouch;
        this.azimuthRotorNameTag = azimuthRotorNameTag;
        this.elevationRotorNameTag = elevationRotorNameTag;
        this.azimuthSpeedModifier = azimuthSpeedModifier;
        this.elevationSpeedModifier = elevationSpeedModifier;
        this.controlSeatRelativeMouseControl = controlSeatRelativeMouseControl;
        this.timeInterval = timeInterval;
        this.shouldStabilizeAz = shouldStabilizeAz;
        this.shouldStabilizeEl = shouldStabilizeEl;
        this.returnToRestPosition = returnToRestPosition;
    }

    public void GrabBlocks()
    {
        var blocks = new List<IMyTerminalBlock>();
        thisGroup.GetBlocks(blocks);

        elevationRotor = null;
        additionalElevationRotors.Clear();
        azimuthRotor = null;
        shipControllers.Clear();
        weaponsAndTools.Clear();
        additionalWeaponsAndTools.Clear();
        gyros.Clear();

        foreach (IMyTerminalBlock thisBlock in blocks)
        {
            if (thisBlock is IMyMotorStator)
            {
                if (thisBlock.CustomName.ToLower().Contains(elevationRotorNameTag.ToLower()))
                {
                    if (thisBlock.CustomName.ToLower().Contains("main"))
                    {
                        if (elevationRotor != null)
                        {
                            additionalElevationRotors.Add(elevationRotor);
                            elevationRotor = null;
                        }
                    }
                    
                    if (elevationRotor == null) //grabs parent elevation rotor first
                    {
                        var thisRotor = thisBlock as IMyMotorStator;

                        if (thisRotor.IsAttached && thisRotor.IsFunctional) //checks if elevation rotor is attached
                        {
                            thisGroup.GetBlocks(weaponsAndTools, block => block.CubeGrid == thisRotor.TopGrid && IsWeaponOrTool(block));
                        }
                        if (weaponsAndTools.Count != 0)
                            elevationRotor = thisRotor;
                        else
                            additionalElevationRotors.Add(thisRotor);
                    }
                    else //then grabs any other elevation rotors it finds
                        additionalElevationRotors.Add(thisBlock as IMyMotorStator);
                }
                else if (thisBlock.CustomName.ToLower().Contains(azimuthRotorNameTag.ToLower())) //grabs azimuth rotor
                {
                    azimuthRotor = thisBlock as IMyMotorStator;
                }
            }
            else if (thisBlock is IMyShipController) //grabs ship controller
            {
                shipControllers.Add(thisBlock as IMyShipController);
            }
            else if (thisBlock is IMyGyro)
            {
                gyros.Add(thisBlock as IMyGyro);
            }
        }

        int numberOfElevationRotors = elevationRotor == null ? additionalElevationRotors.Count : additionalElevationRotors.Count + 1;
        thisProgram.Echo($"> Elevation rotors: {numberOfElevationRotors}");

        bool noErrors = true;
        if (shipControllers.Count == 0)
        {
            thisProgram.Echo("\n[ERROR]: No control seat or remote control found\n");
            noErrors = false;
        }

        if (azimuthRotor == null)
        {
            thisProgram.Echo("\n[ERROR]: No azimuth rotor\n");
            noErrors = false;
        }
        else if (!azimuthRotor.IsAttached)
        {
            thisProgram.Echo($"\n[ERROR]: No rotor head for '{azimuthRotor.CustomName}'\n");
            noErrors = false;
        }

        if (elevationRotor == null)
        {
            noErrors = false;
        }

        if (weaponsAndTools.Count == 0 && additionalElevationRotors.Count != 0)
        {
            thisProgram.Echo($"\n[ERROR]: No weapons or tools found\nfor rotor named '{additionalElevationRotors[0].CustomName}'\n");
            noErrors = false;
        }

        IsSetup = noErrors;
    }

    private bool IsWeaponOrTool(IMyTerminalBlock block)
    {
        if (block is IMyUserControllableGun && !(block is IMyLargeTurretBase))
        {
            return true;
        }
        else if (block is IMyShipToolBase)
        {
            return true;
        }
        else if (block is IMyShipDrill)
        {
            return true;
        }
        else if (block is IMyLightingBlock)
        {
            return true;
        }
        else if (block is IMyCameraBlock)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public void Control()
    {
        thisProgram.Echo($"________________________\nGroup: '{thisGroup.Name}'");

        GrabBlocks();
        if (!IsSetup)
        {
            StopRotorMovement(thisGroup); //stops rotors from spazzing
            if (fireWeaponsOnCrouch)
                TurnOffWeapons(); //turns off any guns
            thisProgram.Echo("Turret is OFFLINE");
        }
        else
        {
            //control rotors
            bool underControl = TurretControl();
            if (additionalElevationRotors.Count != 0)
                thisProgram.Echo($"> Main elevation rotor:\n  '{elevationRotor.CustomName}'");
            thisProgram.Echo($"> Turret is {(underControl ? "active" : "idle")}");  
        }
    }

    private bool TurretControl()
    {
        var turretController = GetControlledShipController(shipControllers);

        if (!turretController.IsUnderControl)
        {
            if (returnToRestPosition)
            {
                ReturnToEquilibrium();
                foreach (var block in gyros)
                {
                    block.GyroOverride = false;
                }
                return false;
            }

            StopRotorMovement(thisGroup);
            foreach (var additionalElevationRotor in additionalElevationRotors)
            {
                if (!additionalElevationRotor.IsAttached) //checks if opposite elevation rotor is attached
                {
                    thisProgram.Echo($"\n> No rotor head for additional elevation\nrotor named '{additionalElevationRotor.CustomName}'\nSkipping this rotor...\n");
                    continue;
                }

                thisGroup.GetBlocks(additionalWeaponsAndTools, block => block.CubeGrid == additionalElevationRotor.TopGrid && IsWeaponOrTool(block));
                if (additionalWeaponsAndTools.Count == 0)
                {
                    thisProgram.Echo($"\n> No weapons or tools for additional elevation\nrotor named '{additionalElevationRotor.CustomName}'\nSkipping this rotor...\n");
                    continue;
                }
            }

            return false;
        }
        
        if (_commandRest)
        {
            returnToRestPosition = _returnToRestPositionCached;
            _commandRest = false;
        }

        //get orientation of turret
        IMyTerminalBlock turretReference = weaponsAndTools[0];
        Vector3D turretFrontVec = turretReference.WorldMatrix.Forward;
        Vector3D absUpVec = azimuthRotor.WorldMatrix.Up;
        Vector3D turretSideVec = elevationRotor.WorldMatrix.Up;
        Vector3D turretFrontCrossSide = turretFrontVec.Cross(turretSideVec);

        //check elevation rotor orientation w.r.t. reference
        //double yawMult = 1;
        double pitchMult = 1;

        if (absUpVec.Dot(turretFrontCrossSide) > 0)
        {
            pitchMult = -1;
        }

        Vector3D WASDinputVec = turretController.MoveIndicator;
        var mouseInput = turretController.RotationIndicator;

        //converting mouse input to angular velocity (simple Proportional controller)
        //rotors have their own inherent damping so Derivative term isnt all that important
        double azimuthSpeed = azimuthSpeedModifier * mouseInput.Y;// * yawMult;
        double elevationSpeed = elevationSpeedModifier * mouseInput.X * pitchMult;

        double adjustedAzimuthSpeed = azimuthSpeed;
        double adjustedElevationSpeed = elevationSpeed;
        var controllerWorldMatrix = turretController.WorldMatrix;
        var shipMatrix = MatrixD.Zero;

        if (controlSeatRelativeMouseControl)
        {
            if (controllerWorldMatrix.Left.Dot(absUpVec) > 0.7071)
            {
                adjustedAzimuthSpeed = -elevationSpeed * pitchMult;
                adjustedElevationSpeed = azimuthSpeed * pitchMult;
            }
            else if (controllerWorldMatrix.Right.Dot(absUpVec) > 0.7071)
            {
                adjustedAzimuthSpeed = elevationSpeed * pitchMult;
                adjustedElevationSpeed = -azimuthSpeed * pitchMult;
            }
            else if (controllerWorldMatrix.Down.Dot(absUpVec) > 0.7071)
            {
                adjustedAzimuthSpeed = -azimuthSpeed;
                adjustedElevationSpeed = -elevationSpeed;
            }
            
            shipMatrix.Up = controllerWorldMatrix.Up;
            var forward = VectorRejection(turretFrontVec, shipMatrix.Up);
            if (!Vector3D.IsUnit(ref forward))
                forward.Normalize(); //mutable structs REEEE
            shipMatrix.Forward = forward;
            shipMatrix.Left = Vector3D.Cross(shipMatrix.Up, shipMatrix.Forward);
            
        }
        else
        {
            shipMatrix.Up = absUpVec;
            var forward = VectorRejection(turretFrontVec, shipMatrix.Up);
            if (!Vector3D.IsUnit(ref forward))
                forward.Normalize(); //mutable structs REEEE
            shipMatrix.Forward = forward;
            shipMatrix.Left = Vector3D.Cross(shipMatrix.Up, shipMatrix.Forward);
        }

        //Compute rotor stabilization velocities
        double azimuthStabilizationVelocity = 0;
        double elevationStabilizationVelocity = 0;
        if (shouldStabilizeAz || shouldStabilizeEl)
        {
            if (firstRun)
            {
                firstRun = false;
                lastElevationMatrix = elevationRotor.WorldMatrix;
                lastAzimuthMatrix = azimuthRotor.WorldMatrix;
            }
            else
            {
                if (shouldStabilizeAz)
                {
                    var azimuthError = CalculateRotorDeviationAngle(azimuthRotor.WorldMatrix.Forward, lastAzimuthMatrix);
                    azimuthStabilizationVelocity = azimuthError / timeInterval * radsToRPM;
                }
                
                if (shouldStabilizeEl)
                {
                    var elevationError = CalculateRotorDeviationAngle(elevationRotor.WorldMatrix.Forward, lastElevationMatrix);
                    elevationStabilizationVelocity = elevationError / timeInterval * radsToRPM;
                }

                lastElevationMatrix = elevationRotor.WorldMatrix;
                lastAzimuthMatrix = azimuthRotor.WorldMatrix;
            }
        }

        //thisProgram.Echo($"shouldStabilizeAz: {shouldStabilizeAz}\nazimuthStabilizationVelocity: {azimuthStabilizationVelocity}\nelevationStabilizationVelocity: {elevationStabilizationVelocity}");

        //apply rotor velocities
        float finalAzimuthSpeed = (float)adjustedAzimuthSpeed + (float)azimuthStabilizationVelocity;
        float finalElevationSpeed = (float)adjustedElevationSpeed + (float)elevationStabilizationVelocity;
        
        azimuthRotor.TargetVelocityRPM = finalAzimuthSpeed;
        elevationRotor.TargetVelocityRPM = finalElevationSpeed;
        
        azimuthRotor.Enabled = true;
        elevationRotor.Enabled = true;

        //gyro assistance
        GetListBlocksOnGrid(azimuthRotor.TopGrid, gyros, gridGyros);
        ApplyGyroOverride(0, azimuthSpeed, 0, gridGyros, shipMatrix);
        
        GetListBlocksOnGrid(elevationRotor.TopGrid, gyros, gridGyros);
        ApplyGyroOverride(elevationSpeed, azimuthSpeed, 0, gridGyros, shipMatrix);
        

        //Determine how to move opposite elevation rotor (if any)
        foreach (var additionalElevationRotor in additionalElevationRotors)
        {

            if (!additionalElevationRotor.IsAttached) //checks if opposite elevation rotor is attached
            {
                thisProgram.Echo($"\n> No rotor head for additional elevation\nrotor named '{additionalElevationRotor.CustomName}'\nSkipping this rotor...\n");
                continue;
            }

            thisGroup.GetBlocks(additionalWeaponsAndTools, block => block.CubeGrid == additionalElevationRotor.TopGrid && IsWeaponOrTool(block));

            if (additionalWeaponsAndTools.Count == 0)
            {
                thisProgram.Echo($"\n> No weapons or tools for additional elevation\nrotor named '{additionalElevationRotor.CustomName}'\nSkipping this rotor...\n");
                continue;
            }

            var oppositeFrontVec = additionalWeaponsAndTools[0].WorldMatrix.Forward;

            float multiplier = -1f;
            if (additionalElevationRotor.WorldMatrix.Up.Dot(elevationRotor.WorldMatrix.Up) > 0)
                multiplier = 1f;

            //flattens the opposite elevation rotor's forward vec onto the rotation plane of the parent elevation rotor
            var oppositePlanar = oppositeFrontVec - VectorProjection(oppositeFrontVec, turretSideVec);

            //Angular difference between elevation and additionalElevation rotor
            var diff = (float)VectorAngleBetween(oppositePlanar, turretFrontVec) * Math.Sign(oppositePlanar.Dot(turretFrontCrossSide)) * 100;                                                                                                                                               //w/h-i+p!l_a#s$h%1^4&1                                                             

            //Apply velocity while compensating for angular error
            //This syncs the movement of all elevation rotors!
            additionalElevationRotor.Enabled = true;
            additionalElevationRotor.TargetVelocityRPM = (multiplier * (float)finalElevationSpeed - multiplier * diff);
            
            GetListBlocksOnGrid(additionalElevationRotor.TopGrid, gyros, gridGyros);
            ApplyGyroOverride(finalElevationSpeed - diff, finalAzimuthSpeed, 0, gyros, shipMatrix);

            if (fireWeaponsOnCrouch)
            {
                //control weapons
                if (WASDinputVec.Y < 0)
                {
                    ControlWeaponsAndTools(additionalWeaponsAndTools, true);
                }
                else
                {
                    ControlWeaponsAndTools(additionalWeaponsAndTools, false);
                }
            }
        }

        //control weapons
        if (fireWeaponsOnCrouch)
        {
            if (WASDinputVec.Y < 0)
            {
                ControlWeaponsAndTools(weaponsAndTools, true);
            }
            else
            {
                ControlWeaponsAndTools(weaponsAndTools, false);
            }
        }
        
        return true;
    }

    //Whip's ApplyGyroOverride Method v10 - 8/19/17
    void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list, MatrixD shipMatrix) 
    { 
        var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed); //because keen does some weird stuff with signs 
        var relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipMatrix); 

        foreach (var thisGyro in gyro_list) 
        { 
            var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(thisGyro.WorldMatrix)); 
     
            thisGyro.Pitch = (float)transformedRotationVec.X;
            thisGyro.Yaw = (float)transformedRotationVec.Y; 
            thisGyro.Roll = (float)transformedRotationVec.Z; 
            thisGyro.GyroOverride = true; 
        } 
    }

    void GetListBlocksOnGrid<T>(IMyCubeGrid grid, List<T> list, List<T> result) where T : class, IMyTerminalBlock
    {
        result.Clear();
        foreach (var block in list)
        {
            if (block.CubeGrid == grid)
                result.Add(block);
        }
    }
    
    private double CalculateRotorDeviationAngle(Vector3D forwardVector, MatrixD lastOrientation)
    {
        var flattenedForwardVector = VectorRejection(forwardVector, lastOrientation.Up);
        return VectorAngleBetween(flattenedForwardVector, lastOrientation.Forward) * Math.Sign(flattenedForwardVector.Dot(lastOrientation.Left));
    }

    private Vector3D VectorProjection(Vector3D a, Vector3D b)
    {
        return a.Dot(b) / b.LengthSquared() * b;
    }

    private Vector3D VectorRejection(Vector3D a, Vector3D b) //reject a on b    
    {
        if (Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a - a.Dot(b) / b.LengthSquared() * b;
    }

    private double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;
        else
            return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
    }

    private void TurnOffWeapons()
    {
        thisGroup.GetBlocks(additionalWeaponsAndTools, block => IsWeaponOrTool(block));
        ControlWeaponsAndTools(additionalWeaponsAndTools, false);
    }

    private void ControlWeaponsAndTools(List<IMyTerminalBlock> weaponsAndTools, bool shouldEnable)
    {
        if (shouldEnable)
        {
            for (int i = 0; i < weaponsAndTools.Count; i++)
            {
                var weapon = weaponsAndTools[i] as IMyUserControllableGun;
                weapon?.ApplyAction("Shoot_On");
                weapon?.ApplyAction("ShootOnce");
                var tool = weaponsAndTools[i] as IMyShipToolBase;
                tool?.ApplyAction("OnOff_On");
                var light = weaponsAndTools[i] as IMyLightingBlock;
                light?.ApplyAction("OnOff_On");
            }
        }
        else
        {
            for (int i = 0; i < weaponsAndTools.Count; i++)
            {
                var weapon = weaponsAndTools[i] as IMyUserControllableGun;
                weapon?.ApplyAction("Shoot_Off");
                var tool = weaponsAndTools[i] as IMyShipToolBase;
                tool?.ApplyAction("OnOff_Off");
                var light = weaponsAndTools[i] as IMyLightingBlock;
                light?.ApplyAction("OnOff_Off");
            }
        }
    }

    private void StopRotorMovement(IMyBlockGroup thisGroup)
    {
        azimuthRotor?.SetValue("Velocity", 0f);
        elevationRotor?.SetValue("Velocity", 0f);

        foreach (var additionalElevationRotor in additionalElevationRotors)
            additionalElevationRotor?.SetValue("Velocity", 0f);

        var blocks = new List<IMyTerminalBlock>();
        thisGroup.GetBlocks(blocks, IsWeaponOrTool);
        ControlWeaponsAndTools(blocks, false);
    }

    private IMyShipController GetControlledShipController(List<IMyShipController> SCs)
    {
        foreach (IMyShipController thisController in SCs)
        {
            if (thisController.IsUnderControl && thisController.CanControlShip)
                return thisController;
        }

        return SCs[0];
    }
    
    bool _returnToRestPositionCached = false;
    public void GoToRest()
    {
        _returnToRestPositionCached = returnToRestPosition;
        returnToRestPosition = true;
        _commandRest = true;
    }
    
    void ReturnToEquilibrium()
    {
        MoveRotorToEquilibrium(azimuthRotor);
        MoveRotorToEquilibrium(elevationRotor);

        foreach (var block in additionalElevationRotors)
        {
            MoveRotorToEquilibrium(block);
        }
    }

    void MoveRotorToEquilibrium(IMyMotorStator rotor)
    {
        if (rotor == null)
            return;

        double restAngle = 0;
        if (!string.IsNullOrEmpty(rotor.CustomData) && double.TryParse(rotor.CustomData, out restAngle))
        {
            var currentAngle = rotor.Angle;
            var restAngleRad = MathHelper.ToRadians((float)restAngle) % MathHelper.TwoPi;
            
            if (rotor.LowerLimitRad >= -MathHelper.TwoPi && rotor.UpperLimitRad <= MathHelper.TwoPi)
            {
                if (restAngleRad > rotor.UpperLimitRad)
                    restAngleRad -= MathHelper.TwoPi;
                else if (restAngleRad < rotor.LowerLimitRad)
                    restAngleRad += MathHelper.TwoPi;
            }
            else
            {
                if (restAngleRad > currentAngle + Math.PI)
                    restAngleRad -= MathHelper.TwoPi;
                else if (restAngleRad < currentAngle - Math.PI)
                    restAngleRad += MathHelper.TwoPi;
            }
            
            
            var angularDeviation = (restAngleRad - currentAngle);
            rotor.TargetVelocityRPM = (float)Math.Round(angularDeviation * equilibriumRotationSpeed, 2);
           
            if (Math.Abs(angularDeviation) < 1e-2)
            {
                if (rotor.Enabled)
                {
                    rotor.Enabled = false;
                    rotor.BrakingTorque = float.MaxValue;
                }
            }
            else
            {
                if (!rotor.Enabled)
                {
                    rotor.Enabled = true;
                }
            }
        }
        else if (rotor.LowerLimitRad >= -MathHelper.TwoPi && rotor.UpperLimitRad <= MathHelper.TwoPi)
        {
            var avgAngle = (rotor.LowerLimitRad + rotor.UpperLimitRad) * 0.5;
            var currentAngle = rotor.Angle;
            avgAngle %= MathHelper.TwoPi;
            currentAngle %= MathHelper.TwoPi;

            var angularDeviation = (avgAngle - currentAngle);
            var targetVelocity = angularDeviation * equilibriumRotationSpeed;
            rotor.TargetVelocityRPM = (float)Math.Round(targetVelocity, 2);
            
            if (Math.Abs(angularDeviation) < 1e-2)
            {
                if (rotor.Enabled)
                {
                    rotor.Enabled = false;
                    rotor.BrakingTorque = float.MaxValue;
                }
            }
            else
            {
                if (!rotor.Enabled)
                {
                    rotor.Enabled = true;
                }
            }
        }
        else
        {
            rotor.TargetVelocityRPM = 0f;
            if (rotor.Enabled)
            {
                rotor.Enabled = false;
                rotor.BrakingTorque = float.MaxValue;
            }
        }
    }
    
    static void WrapAngleAroundPI(ref float angle)
    {
        angle %= MathHelper.TwoPi;
        
        if (angle > Math.PI)
            angle = -MathHelper.TwoPi + angle;
        else if (angle < -Math.PI)
            angle = MathHelper.TwoPi + angle;
    }
    
    static double GetAllowedRotationAngle(double initialAngle, IMyMotorStator rotor)
    {
        if (rotor.LowerLimitRad >= -MathHelper.TwoPi && rotor.UpperLimitRad <= MathHelper.TwoPi && rotor.UpperLimitRad - rotor.LowerLimitRad > Math.PI)
        {
            var currentAngleVector = GetVectorFromRotorAngle(rotor.Angle, rotor);
            var lowerLimitVector = GetVectorFromRotorAngle(rotor.LowerLimitRad, rotor);
            var upperLimitVector = GetVectorFromRotorAngle(rotor.UpperLimitRad, rotor);

            var upAxis = Vector3D.Cross(upperLimitVector, lowerLimitVector);
            var currentCrossLower = Vector3D.Cross(currentAngleVector, lowerLimitVector);
            var currentCrossUpper = Vector3D.Cross(currentAngleVector, lowerLimitVector);

            var angleToLowerLimit = Math.Acos(Vector3D.Dot(lowerLimitVector, currentAngleVector));
            if (Vector3D.Dot(upAxis, currentCrossLower) > 0)
                angleToLowerLimit = MathHelper.TwoPi - angleToLowerLimit;

            var angleToUpperLimit = Math.Acos(Vector3D.Dot(upperLimitVector, currentAngleVector));
            if (Vector3D.Dot(upAxis, currentCrossUpper) < 0)
                angleToUpperLimit = MathHelper.TwoPi - angleToUpperLimit;

            if (initialAngle > 0) //rotating towards lower bounding
            {
                if (angleToLowerLimit < Math.Abs(initialAngle))
                {
                    var newAngle = -MathHelper.TwoPi + initialAngle;
                    if (angleToUpperLimit < Math.Abs(newAngle))
                        return 0;

                    return newAngle; //rotate opposite direction
                }
            }
            else
            {
                if (angleToUpperLimit < Math.Abs(initialAngle))
                {
                    var newAngle = MathHelper.TwoPi + initialAngle;
                    if (angleToLowerLimit < Math.Abs(newAngle))
                        return 0;

                    return newAngle;//rotate opposite direction
                }
            }

            return initialAngle; //conditional fall-through
        }
        else
            return initialAngle;
    }
    
    static Vector3D GetVectorFromRotorAngle(float angle, IMyMotorStator rotor)
    {
        double x = MyMath.FastSin(angle);
        double y = MyMath.FastCos(angle);
        var rotorMatrix = rotor.WorldMatrix;
        return rotorMatrix.Backward * y + rotor.WorldMatrix.Left * x;
    }
}

//Whip's Running Symbol Method v8
//•
int runningSymbolVariant = 0;
int runningSymbolCount = 0;
const int increment = 20;
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


#region VARIABLE CONFIG
VariableConfig config;
void BuildConfig()
{
    config = new VariableConfig(this);

    /*
    ----------------------------------------------------------------------------------
    Syntax for Adding Variables:
    ----------------------------------------------------------------------------------
    
    config.AddVariable(nameof(VARIABLE), () => VARIABLE, x => { VARIABLE = (TYPE)x; });
    
        > VARIABLE is your variable and TYPE is the variable type
    */
    config.AddVariable(nameof(groupNameTag), () => groupNameTag, x => { groupNameTag = (string)x; });
    config.AddVariable(nameof(elevationRotorNameTag), () => elevationRotorNameTag, x => { elevationRotorNameTag = (string)x; });
    config.AddVariable(nameof(azimuthRotorNameTag), () => azimuthRotorNameTag, x => { azimuthRotorNameTag = (string)x; });
    config.AddVariable(nameof(azimuthSpeedModifier), () => azimuthSpeedModifier, x => { azimuthSpeedModifier = (double)x; });
    config.AddVariable(nameof(elevationSpeedModifier), () => elevationSpeedModifier, x => { elevationSpeedModifier = (double)x; });
    config.AddVariable(nameof(controlSeatRelativeMouseControl), () => controlSeatRelativeMouseControl, x => { controlSeatRelativeMouseControl = (bool)x; });
    config.AddVariable(nameof(fireWeaponsOnCrouch), () => fireWeaponsOnCrouch, x => { fireWeaponsOnCrouch = (bool)x; });
    config.AddVariable(nameof(stabilizeAzimuth), () => stabilizeAzimuth, x => { stabilizeAzimuth = (bool)x; });
    config.AddVariable(nameof(stabilizeElevation), () => stabilizeElevation, x => { stabilizeElevation = (bool)x; });
    config.AddVariable(nameof(returnToRestPosition), () => returnToRestPosition, x => { returnToRestPosition = (bool)x; });
}

public class VariableConfig
{
    Program _program = null;
    public Dictionary<string, VariableReference> configurationDict = new Dictionary<string, VariableReference>();
    StringBuilder outputSB = new StringBuilder();
    StringBuilder customDataSB = new StringBuilder();
    bool _verbose = false;

    public VariableConfig(Program program, bool verbose = false)
    {
        this._program = program;
        _verbose = verbose;
    }

    public void AddVariable(string variableName, Func<object> getter, Action<object> setter) //ref T thisVariable) where T : struct
    {
        var referenceToVariable = new VariableReference(getter, setter);
        configurationDict.Add(variableName, referenceToVariable);
    }

    public bool UpdateVariable(string variableName, object newVariable)
    {
        if (!configurationDict.ContainsKey(variableName))
            return false;

        configurationDict[variableName].Set(newVariable);
        return true;
    }

    public void ParseCustomData()
    {
        outputSB.Clear();
        outputSB.Append("Parsing Config...\n");
        var customDataSplit = _program.Me.CustomData.Split('\n');

        foreach (var line in customDataSplit)
        {
            var words = line.Split('=');
            if (words.Length == 2)
            {
                var variableName = words[0].Trim();
                var variableValue = words[1].Trim();

                VariableReference reference;
                bool hasValue = configurationDict.TryGetValue(variableName, out reference);
                if (hasValue)
                {
                    var variable = reference.Get();
                    bool parsed = ParseString(variableValue, ref variable);
                    configurationDict[variableName].Set(variable);
                    if (parsed)
                    {
                        outputSB.Append($"> Parsed {variableName}\n");
                        configurationDict[variableName].Set(variable);
                    }
                    else
                    {
                        outputSB.Append($">> ERROR: Failed to parse {variableName} (value: {variableValue})\n");
                    }
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(line))
                outputSB.Append($">> Error: '{line}' not correct length\n");
            }
        }
        outputSB.Append("Parsing Complete!");

        if (_verbose)
            _program.Echo(outputSB.ToString());
        Write();
    }

    bool ParseString(string variableString, ref object variable)
    {
        if (variable is bool)
        {
            bool temp;
            if (bool.TryParse(variableString, out temp))
            {
                variable = temp;
                return true;
            }
        }
        else if (variable is float)
        {
            float temp;
            if (float.TryParse(variableString, out temp))
            {
                variable = temp;
                return true;
            }
        }
        else if (variable is double)
        {
            double temp;
            if (double.TryParse(variableString, out temp))
            {
                variable = temp;
                return true;
            }
        }
        else if (variable is int)
        {
            int temp;
            if (int.TryParse(variableString, out temp))
            {
                variable = temp;
                return true;
            }
        }
        else if (variable is Vector3D)
        {
            Vector3D temp;
            if (Vector3D.TryParse(variableString, out temp))
            {
                variable = temp;
                return true;
            }
        }
        else if (variable is string)
        {
            variable = variableString;
            return true;
        }
        return false;
    }

    public void Write()
    {
        customDataSB.Clear();
        foreach (var kvp in configurationDict)
        {
            customDataSB.Append($"{kvp.Key} = {kvp.Value.Get()}\n");
        }
        _program.Me.CustomData = customDataSB.ToString();
    }
}

//https://stackoverflow.com/questions/24329012/store-reference-to-an-object-in-dictionary
public sealed class VariableReference
{
    public Func<object> Get { get; private set; }
    public Action<object> Set { get; private set; }
    public VariableReference(Func<object> getter, Action<object> setter)
    {
        Get = getter;
        Set = setter;
    }
}
#endregion

/*
/// WHAT'S CHANGED ///
v9
* Optimized block group searching to only occur every 5 seconds.
v10
* Added support for as many elevation rotors as you want per turret
v11
* Fixed algorithm for picking the correct parent elevation rotor
v12
* Fixed weapons not firing on command on when you had more than 2 elevation rotors
v13
* Added option to control turrets relative to the controlling cockpit
v14
* Added cockpitRelativeMouseControl variable to customData config
v15
* Added option to disable firing with [crouch] key
v16
* Added the modified group name to the error messages for better debugging
v17
* Changed code to use class based turret sorting to improve performance
* Added drill support
v18
* Removed excess GrabBlocks() call within the initialization of the rotor turret class
* Changed error messages to cater to those who don't read instructions
v19
* Added rotor name to debug displays
* Redesigned error output for better clarity
v20
* Added better debug output for rotors with no heads
v21
* Fixed custom data parsing
v22
* Removed need for a timer to loop this code
v23
* Added separate pitch and yaw sensitivity settings
* Added turret stabilization feature
v24
* Made code fetch variables from custom data on each refresh cycle
v25
* Enabled controlSeatRelativeMouseControl by default so ppl stop asking for it XD
* Changed name of cockpitRelativeMouseControl to controlSeatRelativeMouseControl to force this change to take effect
v26
* Changed how weapons are handled
v27
* Fixed bug where azimuth rotors used elevation mouse multiplier
v28
* Fixed issue where cockpit relative mouse control was inverted at certain elevation rotor orientations
v29
* Fixed keen bug where program would run multiple times per tick
* Restructured try catch block
v30
* Added return to equilibrium method when user exits the control seat
v31
* Updated custom data variable config code
v32
* Added manual rest command
v32-1
* First implementation of gyro support
v32-2
* Second pass of gyro support
v32-3
* Changed how gyro override is applied based on the grid it is on
v32-4
* Fixed turret stabilization with gyro assist
v33
* Changed time keeping to runtime
* removed old bandaid for this new one.
v34
* Added main elevation rotor
* Added independent stabilization terms
v35
* changed direction of time estimation
v36
* simplified equilibrium function
v37
* Fix for keen's stupid negative runtime bug
v37-1
[*] Fix for additional elevation rotor velocity bug when both rotors were attached to the same grid (Found by ExteviousV)
*/
