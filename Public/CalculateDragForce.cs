const double reefAtmosphere = 0.6;
const double radiusMult = 8.0;
const double dragCoeff = 1.0;

#region SINGLE PARACHUTE
double CalculateDragForce(IMyParachute parachute)
{
    var velocityVec = parachute.GetVelocity();
    return CalculateDragCoefficient(parachute) * velocityVec.LengthSquared() ;
}

double CalculateParachuteDescentSpeed(IMyParachute parachute, IMyShipController reference)
{
    double mass = reference.CalculateShipMass().PhysicalMass;
    double gravity = reference.GetNaturalGravity().Length();
    return Math.Sqrt(mass * gravity / CalculateDragCoefficient(parachute));
}

double CalculateDragCoefficient(IMyParachute parachute)
{
    double currentAtmosphere = parachute.Atmosphere;
    double gridSize = parachute.CubeGrid.GridSize;
    double num = 10.0 * (currentAtmosphere - reefAtmosphere);
    num = num < 5 ? 5 : Math.Max(Math.Log(num - 0.99) + 5.0, 5.0);
    double chuteRadius = num * radiusMult * gridSize / 2;
    double chuteArea = Math.PI * chuteRadius * chuteRadius;
    return 2.5 * (currentAtmosphere * 1.225)* chuteArea * dragCoeff;
}
#endregion

#region LIST OF PARACHUTES
double CalculateParachuteDescentSpeed(List<IMyParachute> parachutes, IMyShipController reference)
{
    double mass = reference.CalculateShipMass().PhysicalMass;
    double gravity = reference.GetNaturalGravity().Length();
    return Math.Sqrt(mass * gravity / CalculateDragCoefficient(parachutes));
}

double CalculateDragForce(List<IMyParachute> parachutes)
{
    if (parachutes.Count == 0)
        return 0;
    var parachute = parachutes[0];

    int count = parachutes.Count;
    var velocityVec = parachute.GetVelocity();
    
    return CalculateDragCoefficient(parachutes) * velocityVec.LengthSquared();
}

double CalculateDragCoefficient(List<IMyParachute> parachutes)
{
    if (parachutes.Count == 0)
        return 0;
    var parachute = parachutes[0];

    int count = parachutes.Count;
    double currentAtmosphere = parachute.Atmosphere;
    double gridSize = parachute.CubeGrid.GridSize;
    double num = 10.0 * (currentAtmosphere - reefAtmosphere);
    num = num < 5 ? 5 : Math.Max(Math.Log(num - 0.99) + 5.0, 5.0);
    double chuteRadius = num * radiusMult * gridSize / 2;
    double chuteArea = Math.PI * chuteRadius * chuteRadius;
    return 2.5 * (currentAtmosphere * 1.225) * chuteArea * dragCoeff * count;
}
#endregion