//Whip's Profiler Code
double maxTimeToRun = 0;
int resetTicks = 0;
void Profiler()
{
    double timeToRunCode = Runtime.LastRunTimeMs;
    if (timeToRunCode > maxTimeToRun)
    {
        maxTimeToRun = timeToRunCode;
    }

    if (resetTicks >= 60)
    {
        maxTimeToRun = 0;
        resetTicks = 0;
    }

    resetTicks++;

    int currentInstructions = Runtime.CurrentInstructionCount;
    int maxInstructions = Runtime.MaxInstructionCount;
    int currentMethodCalls = Runtime.CurrentMethodCallCount;
    int maxMethodCalls = Runtime.MaxMethodCallCount;
    double percentMaxInstructions = Math.Round((double)currentInstructions / (double)maxInstructions * 100d, 2);
    double percentMaxMethodCalls = Math.Round((double)currentMethodCalls / (double)maxMethodCalls * 100d, 2);

    string profile = $"Time to run: {timeToRunCode.ToString()} ms \nMax time to run: {maxTimeToRun.ToString()} ms \n"
        + $"Current Instructions:\n{currentInstructions} | {percentMaxInstructions}% of max\n"
        + $"Current Method Calls:\n{currentMethodCalls} | {percentMaxMethodCalls}% of max";

    var screen = GridTerminalSystem.GetBlockWithName("DEBUG") as IMyTextPanel;
    screen?.WritePublicText(profile);
    screen?.ShowPublicTextOnScreen();
}

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
        screen?.WritePublicText(profile.ToString());
        screen?.ShowPublicTextOnScreen();
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