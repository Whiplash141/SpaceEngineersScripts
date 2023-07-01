
static class TerminalPropertiesHelper
{
    static Dictionary<Type, Dictionary<string, ITerminalAction>> _terminalActionDict = new Dictionary<Type, Dictionary<string, ITerminalAction>>();
    static Dictionary<Type, Dictionary<string, ITerminalProperty>> _terminalPropertyDict = new Dictionary<Type, Dictionary<string, ITerminalProperty>>();

    public static ITerminalAction GetAction(IMyTerminalBlock block, string actionName)
    {
        Type type = block.GetType();
        Dictionary<string, ITerminalAction> dict;
        ITerminalAction act;

        if (!_terminalActionDict.TryGetValue(type, out dict))
        {
            dict = new Dictionary<string, ITerminalAction>();
        }

        if (dict.TryGetValue(actionName, out act))
        {
            return act;
        }

        act = block.GetActionWithName(actionName);
        dict[actionName] = act;
        _terminalActionDict[type] = dict;
        return act;
    }

    public static void ApplyAction(IMyTerminalBlock block, string actionName)
    {
        ITerminalAction act = GetAction(block, actionName);
        if (act != null)
            act.Apply(block);
    }

    public static ITerminalProperty<T> GetProperty<T>(IMyTerminalBlock block, string propertyName)
    {
        Type type = block.GetType();
        Dictionary<string, ITerminalProperty> dict;
        ITerminalProperty prop;

        if (!_terminalPropertyDict.TryGetValue(type, out dict))
        {
            dict = new Dictionary<string, ITerminalProperty>();
        }

        if (dict.TryGetValue(propertyName, out prop))
        {
            return prop.Cast<T>();
        }

        prop = block.GetProperty(propertyName);
        dict[propertyName] = prop;
        _terminalPropertyDict[type] = dict;
        if (prop == null)
            return null;
        return prop.Cast<T>();
    }

    public static void SetValue<T>(IMyTerminalBlock block, string propertyName, T value)
    {
        ITerminalProperty<T> prop = GetProperty<T>(block, propertyName);

        if (prop != null)
            prop.SetValue(block, value);
    }

    public static T GetValue<T>(IMyTerminalBlock block, string propertyName)
    {
        ITerminalProperty<T> prop = GetProperty<T>(block, propertyName);

        if (prop != null)
            return prop.GetValue(block);
        return default(T);
    }
}
