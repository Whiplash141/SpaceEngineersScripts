/*
/ //// / Whip's Image Converter Helper / //// /

Description:
Simple utility to help print out the monospace pixel resolutions of the screens on a specified block.

Instructions:
1. Configure the custom data to point to the name of the block of interest
2. Click run
*/
const string IniSection = "Image Converter Helper";
const string IniKeyBlockName = "Block name";

MyIni _ini = new MyIni();

MyIniKey _blockNameKey = new MyIniKey(IniSection, IniKeyBlockName);

Program()
{
    Main();
}

void Main()
{
    Echo("Whip's Image Converter Helper\n\nConfigure custom data and click run.\n--------------------------------");

    _ini.TryParse(Me.CustomData);
    string name = _ini.Get(_blockNameKey).ToString(null);
    _ini.Set(_blockNameKey, name);
    Me.CustomData = _ini.ToString();

    if (string.IsNullOrEmpty(name))
    {
        Echo("Error: Block name is empty");
        return;
    }

    IMyTerminalBlock block = GridTerminalSystem.GetBlockWithName(name);
    if (block == null)
    {
        Echo($"Error: no block with name \"{name}\" found");
        return;
    }

    var tsp = block as IMyTextSurfaceProvider;
    if (tsp == null)
    {
        Echo($"Error: block named \"{name}\" is not a text surface provider");
        return;
    }

    if (tsp.SurfaceCount == 0)
    {
        Echo($"Error: block named \"{name}\" nas no screens");
        return;
    }

    Echo($"\"{name}\"");
    for (int screen = 0; screen < tsp.SurfaceCount; ++screen)
    {
        IMyTextSurface surf = tsp.GetSurface(screen);
        Vector2 resolution = ImageConverterHelper.GetBitmapSize(surf);
        Echo($"Screen {screen} - Resolution: {resolution.X:n0}x{resolution.Y:n0}");
    }
}

static class ImageConverterHelper
{
    const float TextSize = 0.1f;
    const string Font = "Monospace";
    const string Reference = "";
    static StringBuilder _measure = new StringBuilder(Reference);

    public static Vector2 GetBitmapSize(IMyTextSurface surface)
    {
        if (surface == null)
        {
            return Vector2.Zero;
        }

        Vector2 surfaceSize = surface.SurfaceSize;
        float textScaleFactor = (float)Math.Min(surface.TextureSize.X, surface.TextureSize.Y) / 512f; // Magic number that keen scales by
        float pixelSize = surface.MeasureStringInPixels(_measure, Font, TextSize * textScaleFactor).Y;
        return surfaceSize / pixelSize;
    }
}
