/// <summary>
/// This estimates an inertial tensor relative to a reference block
/// Help provided by Equinox :)
/// </summary>
/// <param name="reference">Ship controller block to base tensor off of</param>
/// <returns>Inertia tensor of the grid</returns>
MatrixD EstimateInertiaTensor(IMyShipController reference)
{
    Vector3I start = reference.CubeGrid.Min;
    Vector3I end = reference.CubeGrid.Max;
    Vector3I_RangeIterator iterator = new Vector3I_RangeIterator(ref start, ref end);

    Vector3D centerOfMassGrid = Vector3D.TransformNormal(reference.CenterOfMass - reference.CubeGrid.GetPosition(), MatrixD.Transpose(reference.CubeGrid.WorldMatrix));
    double shipMass = reference.CalculateShipMass().PhysicalMass;
    double gridSize = reference.CubeGrid.GridSize; //get size of blocks on grid
    
    MatrixD inertiaTensor = MatrixD.Zero;
    Vector3I current;
    int count = 0;
    while (iterator.IsValid())
    {
        iterator.GetNext(out current);
        if (!reference.CubeGrid.CubeExists(current))
            continue;
        
        count++;
        Vector3D diagonal = centerOfMassGrid - (Vector3D)current * gridSize;
        double x = diagonal.X;
        double y = diagonal.Y;
        double z = diagonal.Z;
        
        double mom = (2.0 * gridSize * gridSize) / 12.0;
        
        double I_xx = mom + (y*y+z*z);
        double I_yy = mom + (x*x+z*z);
        double I_zz = mom + (x*x+y*y);
        
        double I_xy = -x * y;
        double I_yz = -y * z;
        double I_xz = -x * z;
        
        var localTensor = new MatrixD(I_xx, I_xy, I_xz,
                                      I_xy, I_yy, I_yz,
                                      I_xz, I_yz, I_zz);
                                      
        inertiaTensor += localTensor;                              
    }
    inertiaTensor *= (shipMass / count);
    
    return inertiaTensor;
}
