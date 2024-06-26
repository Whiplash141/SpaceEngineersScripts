public static class MyIniHelper
{
    #region List<string>
    /// <summary>
    /// Deserializes a List<string> from MyIni
    /// </summary>
    public static void GetStringList(string section, string name, MyIni ini, List<string> list)
    {
        string raw = ini.Get(section, name).ToString(null);
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Preserve contents
            return;
        }

        list.Clear();
        string[] split = raw.Split('\n');
        foreach (var s in split)
        {
            list.Add(s);
        }
    }

    /// <summary>
    /// Serializes a List<string> to MyIni
    /// </summary>
    public static void SetStringList(string section, string name, MyIni ini, List<string> list)
    {
        string output = string.Join($"\n", list);
        ini.Set(section, name, output);
    }
    #endregion
    
    #region List<int>
    const char LIST_DELIMITER = ',';

    /// <summary>
    /// Deserializes a List<int> from MyIni
    /// </summary>
    public static void GetListInt(string section, string name, MyIni ini, List<int> list)
    {
        list.Clear();
        string raw = ini.Get(section, name).ToString();
        string[] split = raw.Split(LIST_DELIMITER);
        foreach (var s in split)
        {
            int i;
            if (int.TryParse(s, out i))
            {
                list.Add(i);
            }
        }
    }
    
    /// <summary>
    /// Serializes a List<int> to MyIni
    /// </summary>
    public static void SetListInt(string section, string name, MyIni ini, List<int> list)
    {
        string output = string.Join($"{LIST_DELIMITER}", list);
        ini.Set(section, name, output);
    }
    #endregion

    #region Vector2
    /// <summary>
    /// Adds a Vector3D to a MyIni object
    /// </summary>
    public static void SetVector2(string sectionName, string vectorName, ref Vector2 vector, MyIni ini)
    {
        ini.Set(sectionName, vectorName, vector.ToString());
    }

    /// <summary>
    /// Parses a MyIni object for a Vector3D
    /// </summary>
    public static Vector2 GetVector2(string sectionName, string vectorName, MyIni ini, Vector2? defaultVector = null)
    {
        var vector = Vector2.Zero;
        if (TryParseVector2(ini.Get(sectionName, vectorName).ToString(), out vector))
            return vector;
        else if (defaultVector.HasValue)
            return defaultVector.Value;
        return default(Vector2);
    }

    static bool TryParseVector2(string source, out Vector2 vec)
    {
        // Source formatting {X:{0} Y:{1}}
        vec = default(Vector2);
        var fragments = source.Split(':', ' ', '{', '}');
        if (fragments.Length < 5)
            return false;
        if (!float.TryParse(fragments[2], out vec.X))
        {
            return false;
        }
        if (!float.TryParse(fragments[4], out vec.Y))
        {
            return false;
        }
        return true;
    }
    #endregion

    #region Vector2 Compat
    /// <summary>
    /// Adds a Vector3D to a MyIni object
    /// </summary>
    public static void DeprecatedSetVector2(string sectionName, string vectorName, ref Vector2 vector, MyIni ini)
    {
        string vectorString = string.Format("{0}, {1}", vector.X, vector.Y);
        ini.Set(sectionName, vectorName, vectorString);
    }

    /// <summary>
    /// Parses a MyIni object for a Vector3D
    /// </summary>
    public static Vector2 DeprecatedGetVector2(string sectionName, string vectorName, MyIni ini, Vector2? defaultVector = null)
    {
        string vectorString = ini.Get(sectionName, vectorName).ToString("null");
        string[] stringSplit = vectorString.Split(',');

        float x, y;
        if (stringSplit.Length != 2)
        {
            if (defaultVector.HasValue)
                return defaultVector.Value;
            else
                return default(Vector2);
        }

        float.TryParse(stringSplit[0].Trim(), out x);
        float.TryParse(stringSplit[1].Trim(), out y);

        return new Vector2(x, y);
    }
    #endregion

    #region Vector3D
    /// <summary>
    /// Adds a Vector3D to a MyIni object
    /// </summary>
    public static void SetVector3D(string sectionName, string vectorName, ref Vector3D vector, MyIni ini)
    {
        ini.Set(sectionName, vectorName, vector.ToString());
    }

    /// <summary>
    /// Parses a MyIni object for a Vector3D
    /// </summary>
    public static Vector3D GetVector3D(string sectionName, string vectorName, MyIni ini, Vector3D? defaultVector = null)
    {
        var vector = Vector3D.Zero;
        if (Vector3D.TryParse(ini.Get(sectionName, vectorName).ToString(), out vector))
            return vector;
        else if (defaultVector.HasValue)
            return defaultVector.Value;
        return default(Vector3D);
    }
    #endregion

    #region ColorChar
    /// <summary>
    /// Adds a color character to a MyIni object
    /// </summary>
    public static void SetColorChar(string sectionName, string charName, char colorChar, MyIni ini)
    {
        int rgb = (int)colorChar - 0xe100;
        int b = rgb & 7;
        int g = rgb >> 3 & 7;
        int r = rgb >> 6 & 7;
        string colorString = $"{r}, {g}, {b}";

        ini.Set(sectionName, charName, colorString);
    }

    /// <summary>
    /// Parses a MyIni for a color character 
    /// </summary>
    public static char GetColorChar(string sectionName, string charName, MyIni ini, char defaultChar = (char)(0xe100))
    {
        string rgbString = ini.Get(sectionName, charName).ToString("null");
        string[] rgbSplit = rgbString.Split(',');

        int r = 0, g = 0, b = 0;
        if (rgbSplit.Length != 3)
            return defaultChar;

        int.TryParse(rgbSplit[0].Trim(), out r);
        int.TryParse(rgbSplit[1].Trim(), out g);
        int.TryParse(rgbSplit[2].Trim(), out b);

        r = MathHelper.Clamp(r, 0, 7);
        g = MathHelper.Clamp(g, 0, 7);
        b = MathHelper.Clamp(b, 0, 7);

        return (char)(0xe100 + (r << 6) + (g << 3) + b);
    }
    #endregion

    #region Color
    /// <summary>
    /// Adds a Color to a MyIni object
    /// </summary>
    public static void SetColor(string sectionName, string itemName, Color color, MyIni ini, bool writeAlpha = true)
    {
        if (writeAlpha)
        {
            ini.Set(sectionName, itemName, string.Format("{0}, {1}, {2}, {3}", color.R, color.G, color.B, color.A));
        }
        else
        {
            ini.Set(sectionName, itemName, string.Format("{0}, {1}, {2}", color.R, color.G, color.B));
        }
    }

    /// <summary>
    /// Parses a MyIni for a Color
    /// </summary>
    public static Color GetColor(string sectionName, string itemName, MyIni ini, Color? defaultChar = null)
    {
        string rgbString = ini.Get(sectionName, itemName).ToString("null");
        string[] rgbSplit = rgbString.Split(',');

        int r = 0, g = 0, b = 0, a = 0;
        if (rgbSplit.Length < 3)
        {
            if (defaultChar.HasValue)
                return defaultChar.Value;
            else
                return Color.Transparent;
        }

        int.TryParse(rgbSplit[0].Trim(), out r);
        int.TryParse(rgbSplit[1].Trim(), out g);
        int.TryParse(rgbSplit[2].Trim(), out b);
        bool hasAlpha = rgbSplit.Length >= 4 && int.TryParse(rgbSplit[3].Trim(), out a);
        if (!hasAlpha)
            a = 255;

        r = MathHelper.Clamp(r, 0, 255);
        g = MathHelper.Clamp(g, 0, 255);
        b = MathHelper.Clamp(b, 0, 255);
        a = MathHelper.Clamp(a, 0, 255);

        return new Color(r, g, b, a);
    }
    #endregion
}