
static class TerminalPropertiesHelper
{
    static Dictionary<string, ITerminalAction> _terminalActionDict = new Dictionary<string, ITerminalAction>();
    static Dictionary<string, ITerminalProperty> _terminalPropertyDict = new Dictionary<string, ITerminalProperty>();

    public static void ApplyAction(IMyTerminalBlock block, string actionName)
    {
        ITerminalAction act;
        if (_terminalActionDict.TryGetValue(actionName, out act))
        {
            act.Apply(block);
            return;
        }

        act = block.GetActionWithName(actionName);
        _terminalActionDict[actionName] = act;
        act.Apply(block);
    }

    public static void SetValue<T>(IMyTerminalBlock block, string propertyName, T value)
    {
        ITerminalProperty prop;
        if (_terminalPropertyDict.TryGetValue(propertyName, out prop))
        {
            prop.Cast<T>().SetValue(block, value);
            return;
        }

        prop = block.GetProperty(propertyName);
        _terminalPropertyDict[propertyName] = prop;
        prop.Cast<T>().SetValue(block, value);
    }

    public static T GetValue<T>(IMyTerminalBlock block, string propertyName)
    {
        ITerminalProperty prop;
        if (_terminalPropertyDict.TryGetValue(propertyName, out prop))
        {
            return prop.Cast<T>().GetValue(block);
        }

        prop = block.GetProperty(propertyName);
        _terminalPropertyDict[propertyName] = prop;
        return prop.Cast<T>().GetValue(block);
    }
}
