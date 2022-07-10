class ArgumentParser
{
    public int ArgumentCount {
        get;
        private set;
    } = 0;

    public string ErrorMessage
    {
        get;
        private set;
    }

    const char Quote = '"';
    List<string> _arguments = new List<string>();
    HashSet<string> _argHash = new HashSet<string>();
    HashSet<string> _switchHash = new HashSet<string>();
    Dictionary<string, int> _switchIndexDict = new Dictionary<string, int>();

    enum ReturnCode { EndOfStream = -1, Nominal = 0, NoArgs = 1, NonAlphaSwitch = 2, NoEndQuote = 3, NoSwitchName = 4 }

    string _raw;

    public bool InRange(int index)
    {
        if (index < 0 || index >= _arguments.Count)
        {
            return false;
        }
        return true;
    }

    public string Argument(int index)
    {
        if (!InRange(index))
        {
            return "";
        }

        return _arguments[index];
    }

    public bool IsSwitch(int index)
    {
        if (!InRange(index))
        {
            return false;
        }

        return _switchHash.Contains(_arguments[index]);
    }

    public int GetSwitchIndex(string switchName)
    {
        int idx;
        if (_switchIndexDict.TryGetValue(switchName, out idx))
        {
            return idx;
        }
        return -1;
    }

    ReturnCode GetArgStartIdx(int startIdx, out int idx, out bool isQuoted, out bool isSwitch)
    {
        idx = -1;
        isQuoted = false;
        isSwitch = false;
        for (int i = startIdx; i < _raw.Length; ++i)
        {
            char c = _raw[i];
            if (c != ' ')
            {
                if (c == Quote)
                {
                    isQuoted = true;
                    idx = i + 1;
                    return ReturnCode.Nominal;
                }
                if (c == '-' && i + 1 < _raw.Length && _raw[i+1] == '-')
                {
                    isSwitch = true;
                    idx = i + 2;
                    return ReturnCode.Nominal;
                }
                idx = i;
                return ReturnCode.Nominal;
            }
        }
        return ReturnCode.NoArgs;
    }

    ReturnCode GetArgLength(int startIdx, bool isQuoted, bool isSwitch, out int length)
    {
        length = 0;
        for (int i = startIdx; i < _raw.Length; ++i)
        {
            char c = _raw[i];
            if (isQuoted)
            {
                if (c == Quote)
                {
                    return ReturnCode.Nominal;
                }
            }
            else
            {
                if (c == ' ')
                {
                    if (isSwitch && length == 0)
                    {
                        return ReturnCode.NoSwitchName;
                    }
                    return ReturnCode.Nominal;
                }

                if (isSwitch)
                {
                    if (!char.IsLetter(c) && c != '_')
                    {
                        return ReturnCode.NonAlphaSwitch;
                    }
                } 
            }
            length++;
        }
        if (isQuoted)
        {
            return ReturnCode.NoEndQuote;
        }
        if (length == 0 && isSwitch)
        {
            return ReturnCode.NoSwitchName;
        }
        return ReturnCode.EndOfStream; // Reached end of stream
    }

    void ClearArguments()
    {
        ArgumentCount = 0;
        _arguments.Clear();
        _switchHash.Clear();
        _argHash.Clear();
        _switchIndexDict.Clear();
    }

    public bool HasArgument(string argName)
    {
        return _argHash.Contains(argName);
    }

    public bool HasSwitch(string switchName)
    {
        return _switchHash.Contains(switchName);
    }

    public bool TryParse(string arg)
    {
        ReturnCode status;

        _raw = arg;
        ClearArguments();

        int idx = 0;
        while (idx < _raw.Length)
        {
            bool isQuoted, isSwitch;
            int startIdx, length;
            string argString;
            status = GetArgStartIdx(idx, out startIdx, out isQuoted, out isSwitch);
            if (status == ReturnCode.NoArgs)
            {
                ErrorMessage = "";
                return true;
            }

            status = GetArgLength(startIdx, isQuoted, isSwitch, out length);
            if (status == ReturnCode.NoEndQuote)
            {
                ErrorMessage = $"No closing quote found! (idx: {startIdx})";
                ClearArguments();
                return false;
            }
            else if (status == ReturnCode.NonAlphaSwitch)
            {
                ErrorMessage = $"Switch can not contain non-alphabet characters! (idx: {startIdx})";
                ClearArguments();
                return false;
            }
            else if (status == ReturnCode.NoSwitchName)
            {
                ErrorMessage = $"Switch does not have a name (idx: {startIdx})";
                ClearArguments();
                return false;
            }
            else if (status == ReturnCode.EndOfStream) // End of stream
            {
                argString = _raw.Substring(startIdx);
                _arguments.Add(argString);
                _argHash.Add(argString);
                if (isSwitch)
                {
                    _switchHash.Add(argString);
                    _switchIndexDict[argString] = ArgumentCount;
                }
                ArgumentCount++;
                ErrorMessage = "";
                return true;
            }

            argString = _raw.Substring(startIdx, length);
            _arguments.Add(argString);
            _argHash.Add(argString);
            if (isSwitch)
            {
                _switchHash.Add(argString);
                _switchIndexDict[argString] = ArgumentCount;
            }
            ArgumentCount++;
            idx = startIdx + length;
            if (isQuoted)
            {
                idx++; // Move past the quote
            }
        }
        ErrorMessage = "";
        return true;
    }
}
