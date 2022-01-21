
public class CompositeBoundingSphere
{
    public double Radius
    {
        get
        {
            return _sphere.Radius;
        }
    }
    
    public Vector3D Center
    {
        get
        {
            return _sphere.Center;
        }
    }
    
    public IMyCubeGrid LargestGrid = null;
    
    BoundingSphereD _sphere;

    Program _program;
    HashSet<IMyCubeGrid> _grids = new HashSet<IMyCubeGrid>();
    Vector3D _compositePosLocal = Vector3D.Zero;
    double _compositeRadius = 0;

    public CompositeBoundingSphere(Program program)
    {
        _program = program;
    }

    public void FetchCubeGrids()
    {
        _grids.Clear();
        _grids.Add(_program.Me.CubeGrid);
        LargestGrid = _program.Me.CubeGrid;
        _program.GridTerminalSystem.GetBlocksOfType<IMyMechanicalConnectionBlock>(null, CollectGrids);
        RecomputeCompositeProperties();
    }

    public void Compute(bool fullCompute = false)
    {
        if (fullCompute)
        {
            RecomputeCompositeProperties();
        }
        Vector3D compositePosWorld = _program.Me.GetPosition() + Vector3D.TransformNormal(_compositePosLocal, _program.Me.WorldMatrix);
        _sphere = new BoundingSphereD(compositePosWorld, _compositeRadius);
    }

    void RecomputeCompositeProperties()
    {
        bool first = true;
        Vector3D compositeCenter = Vector3D.Zero;
        double compositeRadius = 0;
        foreach (var g in _grids)
        {
            Vector3D currentCenter = g.WorldVolume.Center;
            double currentRadius = g.WorldVolume.Radius;
            if (first)
            {
                compositeCenter = currentCenter;
                compositeRadius = currentRadius;
                first = false;
                continue;
            }
            Vector3D diff = currentCenter - compositeCenter;
            double diffLen = diff.Normalize();
            double newDiameter = currentRadius + diffLen + compositeRadius;
            double newRadius = 0.5 * newDiameter;
            if (newRadius > compositeRadius)
            {
                double diffScale = (newRadius - compositeRadius);
                compositeRadius = newRadius;
                compositeCenter += diffScale * diff;
            }
        }
        // Convert to local space
        Vector3D directionToCompositeCenter = compositeCenter - _program.Me.GetPosition();
        _compositePosLocal = Vector3D.TransformNormal(directionToCompositeCenter, MatrixD.Transpose(_program.Me.WorldMatrix));
        _compositeRadius = compositeRadius;
    }

    bool CollectGrids(IMyTerminalBlock b)
    {
        if (!b.IsSameConstructAs(_program.Me))
        {
            return false;
        }
        
        var mech = (IMyMechanicalConnectionBlock)b;
        if (mech.CubeGrid.WorldVolume.Radius > LargestGrid.WorldVolume.Radius)
        {
            LargestGrid = mech.CubeGrid;
        }
        _grids.Add(mech.CubeGrid);
        if (mech.IsAttached)
        {
            _grids.Add(mech.TopGrid);
        }
        return false;
    }
}