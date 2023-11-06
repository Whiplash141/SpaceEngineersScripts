
//Whip's Profiler Graph Code
int count = 1;
int maxSeconds = 30;
StringBuilder profile = new StringBuilder();
bool hasWritten = false;
void ProfilerGraph()
{
    if (count <= maxSeconds * 60)
    {
        double timeToRunCode = Runtime.LastRunTimeMs;

        profile.Append(timeToRunCode.ToString()).Append("\n");
        count++;
    }
    else if (!hasWritten)
    {
        var screen = GridTerminalSystem.GetBlockWithName("DEBUG") as IMyTextPanel;
        screen?.WriteText(profile.ToString());
        screen?.ContentType = ContentType.TEXT_AND_IMAGE;
        if (screen != null)
            hasWritten = true;
    }
}

//Example usage
void Main()
{
    ProfilerGraph();
    //other shit
}
