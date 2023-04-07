//#exclude
IMyFunctionalBlock b = null;

void Main()
{
    try
    {
        Foo();
    }
    catch (Exception e)
    {
        BlueScreenOfDeath.Show(Me.GetSurface(0), "Test script", "1.2.4", e);
        throw e;
    }
}

void Foo()
{
    Bar();
}

void Bar()
{
    b.Enabled = false;
}
//#endexclude
static class BlueScreenOfDeath 
{
    const int MAX_BSOD_WIDTH = 50;
    const string BSOD_TEMPLATE =
    "{0} - v{1}\n\n"+ 
    "A fatal exception has occured at\n"+
    "{2}. The current\n"+
    "program will be terminated.\n"+
    "\n"+ 
    "EXCEPTION:\n"+
    "{3}\n"+
    "\n"+
    "* Please REPORT this crash message to\n"+ 
    "  the Bug Reports discussion of this script\n"+ 
    "\n"+
    "* Press RECOMPILE to restart the program";

    static StringBuilder bsodBuilder = new StringBuilder(256);
    
    public static void Show(IMyTextSurface surface, string scriptName, string version, Exception e)
    {
        if (surface == null) 
        { 
            return;
        }
        surface.ContentType = ContentType.TEXT_AND_IMAGE;
        surface.Alignment = TextAlignment.LEFT;
        float scaleFactor = 512f / (float)Math.Min(surface.TextureSize.X, surface.TextureSize.Y);
        surface.FontSize = scaleFactor * surface.TextureSize.X / (19.5f * MAX_BSOD_WIDTH);
        surface.FontColor = Color.White;
        surface.BackgroundColor = Color.Blue;
        surface.Font = "Monospace";
        string exceptionStr = e.ToString();
        string[] exceptionLines = exceptionStr.Split('\n');
        bsodBuilder.Clear();
        foreach (string line in exceptionLines)
        {
            if (line.Length <= MAX_BSOD_WIDTH)
            {
                bsodBuilder.Append(line).Append("\n");
            }
            else
            {
                string[] words = line.Split(' ');
                int lineLength = 0;
                foreach (string word in words)
                {
                    lineLength += word.Length;
                    if (lineLength >= MAX_BSOD_WIDTH)
                    {
                        bsodBuilder.Append("\n");
                        lineLength = word.Length;
                    }
                    bsodBuilder.Append(word).Append(" ");
                    lineLength += 1;
                }
                bsodBuilder.Append("\n");
            }
        }

        surface.WriteText(string.Format(BSOD_TEMPLATE, 
                                        scriptName.ToUpperInvariant(),
                                        version,
                                        DateTime.Now, 
                                        bsodBuilder));
    }
}
