/// <summary>
/// Projects a world position to a location on the screen in pixels.
/// </summary>
/// <param name="worldPosition"></param>
/// <param name="cam"></param>
/// <param name="screen"></param>
/// <param name="screenPositionPx"></param>
/// <param name="screenWidthInMeters"></param>
/// <returns>True if the solution can be displayed on the screen.</returns>
bool WorldPositionToScreenPosition(Vector3D worldPosition, IMyCameraBlock cam, IMyTextPanel screen, out Vector2 screenPositionPx)
{
    screenPositionPx = Vector2.Zero;

    Vector3D cameraPos = cam.GetPosition() + cam.WorldMatrix.Forward * 0.25; // There is a ~0.25 meter forward offset for the view origin of cameras
    Vector3D screenPosition = screen.GetPosition() + screen.WorldMatrix.Forward * 0.5 * screen.CubeGrid.GridSize;
    Vector3D normal = screen.WorldMatrix.Forward;
    Vector3D cameraToScreen = screenPosition - cameraPos;
    double distanceToScreen = Math.Abs(Vector3D.Dot(cameraToScreen, normal));
    
    Vector3D viewCenterWorld = distanceToScreen * cam.WorldMatrix.Forward;

    // Project direction onto the screen plane (world coords)
    Vector3D direction = worldPosition - cameraPos;
    Vector3D directionParallel = direction.Dot(normal) * normal;
    double distanceRatio = distanceToScreen / directionParallel.Length();

    Vector3D directionOnScreenWorld = distanceRatio * direction;

    // If we are pointing backwards, ignore
    if (directionOnScreenWorld.Dot(screen.WorldMatrix.Forward) < 0)
    {
        return false;
    }
    
    Vector3D planarCameraToScreen = cameraToScreen - Vector3D.Dot(cameraToScreen, normal) * normal;
    directionOnScreenWorld -= planarCameraToScreen;

    // Convert location to be screen local (world coords)
    Vector2 directionOnScreenLocal = new Vector2(
        (float)directionOnScreenWorld.Dot(screen.WorldMatrix.Right),
        (float)directionOnScreenWorld.Dot(screen.WorldMatrix.Down));

    // ASSUMPTION:
    // The screen is square
    double screenWidthInMeters = screen.CubeGrid.GridSize * 0.855f; // My magic number for large grid
    float metersToPx = (float)(screen.TextureSize.X / screenWidthInMeters);
            
    // Convert dorection to be screen local (pixel coords)
    directionOnScreenLocal *= metersToPx;

    // Get final location on screen
    Vector2 screenCenterPx = screen.TextureSize * 0.5f;
    screenPositionPx = screenCenterPx + directionOnScreenLocal;
    return true;
}
