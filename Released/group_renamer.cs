
#region Script

/*
/ //// / Whip's Block Renamer / //// /

_____________________________________________________________________________
INSTRUCTIONS

1.) Place this script in a programmable block
2.) Group blocks that you want to rename with the GROUP RENAME TAGS in the section below
3.) Run the program once. This will rename/prefix/suffix all blocks in recognized rename groups.
4.) You can now delete these groups if you desire
5.) Enjoy!

(Optional) Configure script parameters in the Custom Data of this programmable block.

_____________________________________________________________________________
GROUP RENAME TAGS

default
    Renames all blocks in group to their default name
    
default prefix
    Prefixes all blocks with their default names

default suffix    
    Suffixes all blocks with their default names

prefix <desired prefix>
    Prefixes all blocks in group with desired prefix 

suffix <desired suffix>
    Suffixes all blocks in group with the desired suffix

rename <desired name>
    Renames all blocks in group to specified name w/ optional numbering

grid prefix
    Prefixes all blocks in group with the name of their grid

grid suffix
    Suffixes all blocks in group with the name of their grid

replace "<old phrase>" with "<new phrase>"
    Replaces the specified old phrase with the new phrase in all blocks contained within the group
*/

//-------------------------------------------------------------
// NO TOUCH BELOW HERE
//-------------------------------------------------------------

const string VERSION = "7.1.2";
const string DATE = "2023/12/18";

MyIni _ini = new MyIni();

const string IniSectionName = "Whip's Block Renamer";
const string IniKeyNumber = "Number blocks";
const string IniKeyNumberFirst = "Number first block";
const string IniKeyNumberPadding = "Number with leading zeroes";
const string IniKeyPrefixSpace = "Add space after prefixes";
const string IniKeySuffixSpace = "Add space before suffixes";

//User Configurable Variables
bool shouldNumber = true;
bool useNumberOnFirstEntry = true;
bool usePreceedingZeroes = true;
bool addSpaceAfterPrefix = true;
bool addSpaceBeforeSuffix = true;

Program()
{
    PrintScriptName();
    ParseIni();
    Echo("Parsed custom data config");
}

void Main()
{
    PrintScriptName();
    ParseIni();
    Echo("Parsed custom data config");

    ParseGroups();
}

void PrintScriptName()
{
    Echo($"Whip's Block Renamer\n(Version {VERSION} - {DATE})\n");
}

void ParseIni()
{
    _ini.Clear();
    _ini.TryParse(Me.CustomData);

    shouldNumber = _ini.Get(IniSectionName, IniKeyNumber).ToBoolean(shouldNumber);
    useNumberOnFirstEntry = _ini.Get(IniSectionName, IniKeyNumberFirst).ToBoolean(useNumberOnFirstEntry);
    usePreceedingZeroes = _ini.Get(IniSectionName, IniKeyNumberPadding).ToBoolean(usePreceedingZeroes);
    addSpaceAfterPrefix = _ini.Get(IniSectionName, IniKeyPrefixSpace).ToBoolean(addSpaceAfterPrefix);
    addSpaceBeforeSuffix = _ini.Get(IniSectionName, IniKeySuffixSpace).ToBoolean(addSpaceBeforeSuffix);

    _ini.Set(IniSectionName, IniKeyNumber       , shouldNumber);
    _ini.Set(IniSectionName, IniKeyNumberFirst  , useNumberOnFirstEntry);
    _ini.Set(IniSectionName, IniKeyNumberPadding, usePreceedingZeroes);
    _ini.Set(IniSectionName, IniKeyPrefixSpace  , addSpaceAfterPrefix);
    _ini.Set(IniSectionName, IniKeySuffixSpace  , addSpaceBeforeSuffix);

    string output = _ini.ToString();
    if (!string.Equals(output, Me.CustomData))
        Me.CustomData = output;
}

void ParseGroups()
{
    var groupList = new List<IMyBlockGroup>();
    GridTerminalSystem.GetBlockGroups(groupList);

    var groupBlocks = new List<IMyTerminalBlock>();

    foreach (var group in groupList)
    {
        group.GetBlocks(groupBlocks);
        
        string groupName = group.Name.Trim();

        if (groupName.Equals("default"))
        {
            RenameBlocksToDefault(groupBlocks, shouldNumber);
        }
        else if (groupName.StartsWith("prefix", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = groupName.Remove(0, 6).Trim();
            PrefixBlockName(groupBlocks, prefix);
        }
        else if (groupName.StartsWith("suffix", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = groupName.Remove(0, 6).Trim();
            SuffixBlockName(groupBlocks, suffix);
        }
        else if (groupName.StartsWith("rename", StringComparison.OrdinalIgnoreCase))
        {
            var name = groupName.Remove(0, 6).Trim();
            RenameBlocks(groupBlocks, name, shouldNumber);
        }
        else if (groupName.StartsWith("grid suffix", StringComparison.OrdinalIgnoreCase))
        {
            SuffixWithGridName(groupBlocks);
        }
        else if (groupName.StartsWith("grid prefix", StringComparison.OrdinalIgnoreCase))
        {
            PrefixWithGridName(groupBlocks);
        }
        else if (groupName.StartsWith("default suffix", StringComparison.OrdinalIgnoreCase))
        {
            SuffixWithDefaultName(groupBlocks);
        }
        else if (groupName.StartsWith("default prefix", StringComparison.OrdinalIgnoreCase))
        {
            PrefixWithDefaultName(groupBlocks);
        }
        else if (groupName.StartsWith("replace", StringComparison.OrdinalIgnoreCase))
        {
            ReplaceName(groupBlocks, groupName);
        }
    }
}

Dictionary<string, List<IMyTerminalBlock>> blockNames = new Dictionary<string, List<IMyTerminalBlock>>();
void RenameBlocksToDefault(List<IMyTerminalBlock> blocks, bool shouldNumber = true)
{
    blockNames.Clear();

    var list = new List<IMyTerminalBlock>();
    foreach (var block in blocks)
    {
        var baseName = block.DefinitionDisplayNameText;

        if (blockNames.TryGetValue(baseName, out list))
        {
            list.Add(block); //add to list
        }
        else
        {
            list = new List<IMyTerminalBlock>() { block }; //because default of List is null
            blockNames[baseName] = list;
        }
    }

    foreach (var keyValuePair in blockNames)
    {
        string name = keyValuePair.Key;
        list = keyValuePair.Value;
        RenameBlocks(list, name, shouldNumber, true);
    }

    Echo($"{blocks.Count} blocks renamed to default");
}

void PrefixBlockName(List<IMyTerminalBlock> blocks, string prefixName)
{
    string space = addSpaceAfterPrefix ? " " : "";
    foreach (var block in blocks)
    {
        if (!block.CustomName.StartsWith(prefixName))
        {
            block.CustomName = $"{prefixName}{space}{block.CustomName}";
        }
    }

    Echo($"{blocks.Count} blocks prefixed with '{prefixName}'");
}

void SuffixBlockName(List<IMyTerminalBlock> blocks, string suffixName)
{
    string space = addSpaceBeforeSuffix ? " " : "";
    foreach (var block in blocks)
    {
        if (!block.CustomName.EndsWith(suffixName))
        {
            block.CustomName = $"{block.CustomName}{space}{suffixName}";
        }
    }

    Echo($"{blocks.Count} blocks suffixed with '{suffixName}'");
}

void RenameBlocks(List<IMyTerminalBlock> blocks, string blockName, bool shouldNumber = true, bool renameDefault = false)
{
    string format = "";
    if (usePreceedingZeroes)
        format = GetNumberFormat(blocks.Count);

    for (int i = 0; i < blocks.Count; i++)
    {
        var block = blocks[i];
        if (!block.CustomName.Contains(blockName) || renameDefault)
        {
            if (usePreceedingZeroes)
            {
                string num = (i + 1).ToString(format);
                block.CustomName = shouldNumber ? useNumberOnFirstEntry ? $"{blockName} {num}" : i > 0 ? $"{blockName} {num}" : blockName : blockName;
            }
            else
                block.CustomName = shouldNumber ? useNumberOnFirstEntry ? $"{blockName} {i + 1}" : i > 0 ? $"{blockName} {i + 1}" : blockName : blockName;
        }
    }

    Echo($"{blocks.Count} blocks renamed to '{blockName}'");
}

void PrefixWithGridName(List<IMyTerminalBlock> blocks)
{
    string space = addSpaceAfterPrefix ? " " : "";
    foreach (var block in blocks)
    {
        if (!block.CustomName.StartsWith(block.CubeGrid.CustomName))
        {
            block.CustomName = $"[{block.CubeGrid.CustomName}]{space}{block.CustomName}";
        }
    }

    Echo($"{blocks.Count} blocks prefixed with grid name");
}

void SuffixWithGridName(List<IMyTerminalBlock> blocks)
{
    string space = addSpaceBeforeSuffix ? " " : "";
    foreach (var block in blocks)
    {
        if (!block.CustomName.EndsWith(block.CubeGrid.CustomName))
        {
            block.CustomName = $"{block.CustomName}{space}[{block.CubeGrid.CustomName}]";
        }
    }

    Echo($"{blocks.Count} blocks suffixed with grid name");
}

void PrefixWithDefaultName(List<IMyTerminalBlock> blocks)
{
    string space = addSpaceAfterPrefix ? " " : "";
    foreach (var block in blocks)
    {
        string baseName = $"({block.DefinitionDisplayNameText})";
        if (!block.CustomName.StartsWith(baseName))
        {
            block.CustomName = $"{baseName}{space}{block.CustomName}";
        }
    }
    Echo($"{blocks.Count} blocks prefixed with default names");
}

void SuffixWithDefaultName(List<IMyTerminalBlock> blocks)
{
    string space = addSpaceBeforeSuffix ? " " : "";
    foreach (var block in blocks)
    {
        string baseName = $"({block.DefinitionDisplayNameText})";
        if (!block.CustomName.EndsWith(baseName))
        {
            block.CustomName = $"{block.CustomName}{space}{baseName}";
        }
    }
    Echo($"{blocks.Count} blocks suffixed with default names");
}

const string replacePattern = "(?i)( )*?replace( )*?\".*?\"( )*?with( )*?\".*?\"( )*?";
void ReplaceName(List<IMyTerminalBlock> blocks, string argument)
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(argument, replacePattern))
    {
        Echo($">> Error: Argument '{argument}' is not in proper format\n\nFormat should be:\nreplace \"<old string>\" with \"<new string>\"");
        return;
    }

    var split = argument.Split('"');
    if (split.Length > 5)
    {
        Echo($">> Error: Argument had too many quotation marks");
        return;
    }
    else if (split.Length < 5)
    {
        Echo(">> Error: Argument had too few quotation marks");
        return;
    }

    string oldString = split[1];
    string newString = split[3];

    int replaceCount = 0;
    foreach (var block in blocks)
    {
        if (!block.CustomName.Contains(oldString))
            continue;

        block.CustomName = block.CustomName.Replace(oldString, newString);
        replaceCount++;
    }

    Echo($"{replaceCount} blocks' names replaced '{oldString}' with '{newString}'");
}

string GetNumberFormat(int number)
{
    string numStr = number.ToString();
    int length = numStr.Length;
    return new string('0', length);
}
#endregion
