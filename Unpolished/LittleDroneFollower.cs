/*
 * / //// / Whip's Little Drone Follower Script - 2021/07/15 / //// /
 */

const string SensorName = "Sensor";
const string RemoteName = "Remote Control";

const double ClosestApproachDistance = 5; // meters
const float SpeedLimit = 100;

IMyRemoteControl _remote;
IMySensorBlock _sensor;
List<MyDetectedEntityInfo> _detectedEntities = new List<MyDetectedEntityInfo>();

Program()
{
    _sensor = GridTerminalSystem.GetBlockWithName(SensorName) as IMySensorBlock;
    _remote = GridTerminalSystem.GetBlockWithName(RemoteName) as IMyRemoteControl;
    
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

void Main(string arg, UpdateType updateSource)
{
    if ((updateSource & UpdateType.Update10) == 0)
    {
        return;
    }
    
    _detectedEntities.Clear();
    _sensor.DetectedEntities(_detectedEntities);
    
    if (_detectedEntities.Count == 0)
    {
        Echo("No targets detected");
        return;
    }
    
    MyDetectedEntityInfo target = default(MyDetectedEntityInfo);
    foreach (var entityInfo in _detectedEntities)
    {
        if (entityInfo.Type == MyDetectedEntityType.CharacterHuman)
        {
            target = entityInfo;
            break;
        }
    }

    if (target.IsEmpty())
    {
        Echo($"Found {_detectedEntities.Count} entities, but none were players");
        return;
    }
    
    Echo($"Following player: {target.Name}");

    Vector3D tgtToRemote = _remote.GetPosition() - target.Position;
    double distToTgt = tgtToRemote.Length();
    Vector3D targetPosition = target.Position + tgtToRemote / distToTgt * ClosestApproachDistance;
    
    _remote.SpeedLimit = SpeedLimit;
    _remote.Direction = Base6Directions.Direction.Forward;
    if (Math.Abs(distToTgt - ClosestApproachDistance) < 0.1 * ClosestApproachDistance)
    {
        _remote.SpeedLimit = 0;
        targetPosition = target.Position;
    }
    else if (distToTgt < ClosestApproachDistance)
    {
        _remote.Direction = Base6Directions.Direction.Backward;
    }

    _remote.ClearWaypoints();
    _remote.FlightMode = FlightMode.OneWay;
    _remote.AddWaypoint(targetPosition, target.Name);
    _remote.SetAutoPilotEnabled(true);
}
