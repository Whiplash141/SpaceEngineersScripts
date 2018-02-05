
//Whip's Wheel Pitching Code v1 - 2/5/18
//This is not churro's code
const double mouseSensitivity = 0.01;

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

List<IMyShipController> shipControllers = new List<IMyShipController>();
List<IMyMotorSuspension> wheels = new List<IMyMotorSuspension>();
double savedAngle = 0;

void Main(string arg, UpdateType updateSource)
{
    if ((updateSource & UpdateType.Update10) == 0)
        return;

    Echo($"Whip's Wheel Pitch Code{RunningSymbol()}");

    GridTerminalSystem.GetBlocksOfType(shipControllers);
    if (shipControllers.Count == 0)
        return;

    GridTerminalSystem.GetBlocksOfType(wheels);
    if (wheels.Count == 0)
        return;

    var controller = GetControlledShipController(shipControllers);
    var minHeight = wheels[0].GetMinimum<float>("Height");
    var maxHeight = wheels[0].GetMaximum<float>("Height");

    var minWheel = GetFurthestBlockInDirection(controller.GetPosition(), controller.WorldMatrix.Backward, wheels);
    var maxWheel = GetFurthestBlockInDirection(controller.GetPosition(), controller.WorldMatrix.Forward, wheels);

    var wheelSpacing = VectorProjection(maxWheel.GetPosition() - minWheel.GetPosition(), controller.WorldMatrix.Forward).Length();

    var maxAngle = Math.Atan((maxHeight - minHeight) / wheelSpacing);

    var mouseInput = controller.RotationIndicator;
    var angleStep = -mouseInput.X * mouseSensitivity;

    savedAngle += angleStep;
    savedAngle = MathHelper.Clamp(savedAngle, -maxAngle, maxAngle);

    InclineWheels(savedAngle, minHeight, controller.WorldMatrix.Forward, minWheel, maxWheel, wheels);
}

void InclineWheels(double angle, double minHeight, Vector3D direction, IMyMotorSuspension minWheel, IMyMotorSuspension maxWheel, List<IMyMotorSuspension> wheels)
{
    var origin = angle >= 0 ? maxWheel.GetPosition() : minWheel.GetPosition();
    angle = Math.Abs(angle);
    var tan = Math.Tan(angle);

    foreach (var block in wheels)
    {
        var relativeDirection = block.GetPosition() - origin;
        var distance = VectorProjection(relativeDirection, direction).Length();
        var height = distance * tan;
        block.SetValue("Height", (float)(height + minHeight));
    }
}

T GetFurthestBlockInDirection<T>(Vector3D origin, Vector3D direction, List<T> blocks) where T : class, IMyTerminalBlock
{
    double maxDistance = double.MinValue;
    T desiredBlock = default(T);
    foreach (T thisBlock in blocks)
    {
        var thisDirection = thisBlock.GetPosition() - origin;
        var sign = Math.Sign(Vector3D.Dot(thisDirection, direction));
        var thisDistance = sign * VectorProjection(thisDirection, direction).LengthSquared();

        if (thisDistance > maxDistance)
        {
            desiredBlock = thisBlock;
            maxDistance = thisDistance;
        }
    }

    return desiredBlock;
}

Vector3D VectorProjection(Vector3D a, Vector3D b)
{
    if (Vector3D.IsZero(b))
        return Vector3D.Zero;

    return a.Dot(b) / b.LengthSquared() * b;
}

IMyShipController GetControlledShipController(List<IMyShipController> SCs)
{
    foreach (IMyShipController thisController in SCs)
    {
        if (thisController.IsUnderControl && thisController.CanControlShip)
            return thisController;
    }

    return SCs[0];
}

//Whip's Running Symbol Method v8
//•
int runningSymbolVariant = 0;
int runningSymbolCount = 0;
const int increment = 1;
string[] runningSymbols = new string[] { "", ".", "..", "..." };

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