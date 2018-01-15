 double GetShipEdgeDistance(IMyTerminalBlock reference, Vector3D direction)
{
    //get grid relative max and min
    var gridMinimum = reference.CubeGrid.Min;
    var gridMaximum = reference.CubeGrid.Max;

    //get dimension of grid cubes
    var gridSize = reference.CubeGrid.GridSize;

    //get worldmatrix for the grid
    var gridMatrix = reference.CubeGrid.WorldMatrix;

    //convert grid coordinates to world coords
    var worldMinimum = Vector3D.Transform(gridMinimum * gridSize, gridMatrix);
    var worldMaximum = Vector3D.Transform(gridMaximum * gridSize, gridMatrix);

    //get reference position
    var origin = reference.GetPosition();

    //compute max and min relative vectors
    var minRelative = worldMinimum - origin;
    var maxRelative = worldMaximum - origin;

    //project relative vectors on desired direction
    var minProjected = Vector3D.Dot(minRelative, direction) / direction.LengthSquared() * direction;
    var maxProjected = Vector3D.Dot(maxRelative, direction) / direction.LengthSquared() * direction;

    //check direction of the projections to determine which is correct
    if (Vector3D.Dot(minProjected, direction) > 0)
        return minProjected.Length();
    else
        return maxProjected.Length();
}