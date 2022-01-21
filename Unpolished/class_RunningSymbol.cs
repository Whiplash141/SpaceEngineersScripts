public class RunningSymbol
{
    int _runningSymbolVariant = 0;
    int _runningSymbolCount = 0;
    int _increment = 1;
    string[] _runningSymbols = new string[] { "−", "\\", "|", "/" };

    public RunningSymbol() { }

    public RunningSymbol(int increment)
    {
        _increment = increment;
    }

    public RunningSymbol(string[] runningSymbols)
    {
        if (runningSymbols.Length != 0)
            _runningSymbols = runningSymbols;
    }

    public RunningSymbol(int increment, string[] runningSymbols)
    {
        _increment = increment;
        if (runningSymbols.Length != 0)
            _runningSymbols = runningSymbols;
    }

    public string Iterate(int ticks = 1)
    {
        if (_runningSymbolCount >= _increment)
        {
            _runningSymbolCount = 0;
            _runningSymbolVariant++;
            _runningSymbolVariant = _runningSymbolVariant++ % _runningSymbols.Length;
        }
        _runningSymbolCount += ticks;

        return this.ToString();
    }

    public override string ToString()
    {
        return _runningSymbols[_runningSymbolVariant];
    }
}