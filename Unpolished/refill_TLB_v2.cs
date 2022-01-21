
//-----------------------------------------
//Configurable variables
//-----------------------------------------

const int dispenseTime = 10; //minutes
string restockName = "Fill"; //name tag of block to refill
string transferFromTag = "Transfer"; //name tag of blocks to pull resources FROM
string beaconCustomDataTag = "Transfer"; //Name of beacon to display status on
string sorterName = "Cork"; //like what toed sticks in his butt lol

/*
/ //// / Whip's Cargo Refill TLB - version 2 - 2/25/18 / //// /

Based off Utilibot code by Dainw: http://steamcommunity.com/sharedfiles/filedetails/?id=437345638
____________________________________________________________________________________________

/// Instructions /// 

1.) Name cargo you want to PULL stuff from with the name tag "Transfer"
2.) Name a SINGLE cargo that you want to put stuff INTO with the name tag "Fill"
3.) Name a conveyor sorter that links the transfer cargo to the fill cargo with name tag "Cork"
4.) Put "Transfer" in the custom data of the BEACON that you want to broadcast status with
5.) Run the code with arguments
____________________________________________________________________________________________

/// Info /// 

* Item log data is stored in this program's custom data
* Code runtime data is also stored in the custom data. The code will remember where it left off
    after map loads

____________________________________________________________________________________________

/// Arguments /// 
    cooldown - <cooldown time>; <item1> - <amountOfItem>; <item2> - <amountOfItem>

    Example:
    cooldown - 5;Steel Plate - 500;uranium - 24

        This will set the resource cooldown to 5 minutes and restock the container with 500 steel plates
        and 24 ingots of uranium once the dispenseTime has elapsed
____________________________________________________________________________________________

/// LIST OF RECOGNISED COMPONENT NAMES ///
    Items listed like: "Banana(s)" means that both "Banana" and "Bananas" 
    would be valid names

    Ores/Ingots
        * Organic or Point(s)
        * Uranium or U or Fuel or UraniumIngot(s) or Uranium Ingot(s)
        * Stone
        * Ice
        * Cobalt or Co
        * Gold or Au
        * Iron or Fe
        * Magnesium or Mg
        * Nickel or Ni
        * Platinum or Pt
        * Silicon or Si
        * Silver or Ag

    Components
        * Steel or SteelPlate(s) or Steel Plate(s)
        * InteriorPlate(s) or Interior Plate(s)
        * Construction  or Construction Component(s)
        * Motor(s)
        * MetalGrid(s) or Metal Grid(s)
        * LargeTube(s) or Large Steel Tube(s) or LST
        * SmallTube(s) or Small Steel Tube(s) or SST
        * Display(s)
        * Computer(s) or CPU(s)
        * Medical or Medical Component(s)
        * SolarCell(s) or Solar Cell(s)
        * PowerCell(s) or Power Cell(s)
        * Detector(s) or Detector Component(s)
        * Girder(s)
        * Thrust or Thruster(s) or Thruster Component(s)
        * Reactor or Reactor Component(s)
        * BulletproofGlass or Bullerproof Glass or Glass
        * RadioCommunication or Radio(s) or Radio Component(s)
            or Radio Communication Component(s)
        * Gravity Generator Component(s) or Gravity or Grav
            or Gravity Generator or Gravity Component(s)
        * OxygenBottle(s) or Oxygen Bottle(s) or Oxygen or O2 
            or O2 Bottle(s)
        * Superconductor(s) or Super Conductor(s)
        * Hydrogen or HydrogenBottle(s) or Hyrdogen Bottle(s)

    Ammo
        * Missile200mm  or Missile(s) or Rocket(s)
        * NATO_25x184mm or 25mm or 25mm NATO
        * NATO_5p56x45mm or 5.56mm or 5.56mm NATO

Todo:
* make idiot proof
* add more comments

- Whiplash141   
*/

//-----------------------------------------
//No touchey below here
//-----------------------------------------

enum Status { Idle, Countdown, Cooldown };
Status currentStatus = Status.Idle;
TimeSpan dispenseCountdown;
TimeSpan cooldown;
const int millisecondsPerCycle = 1667;
TimeSpan minutesPerCycle = new TimeSpan(0, 0, 0, 0, millisecondsPerCycle);
List<IMyBeacon> beacons = new List<IMyBeacon>();
List<IMyConveyorSorter> sorters = new List<IMyConveyorSorter>();
Dictionary<string, int> itemLog = new Dictionary<string, int>();
Dictionary<string, int> ItemsToTransfer = new Dictionary<string, int>();

Program()
{
    //Runtime.UpdateFrequency = UpdateFrequency.Update100;
    GridTerminalSystem.GetBlocksOfType(sorters, x => x.CustomName.Contains(sorterName));
    foreach (var block in sorters)
    {
        block.Enabled = false;
    }

    GridTerminalSystem.GetBlocksOfType(beacons, x => x.CustomData.Contains(beaconCustomDataTag));
    foreach (var block in beacons)
    {
        block.CustomName = $"Resources Ready";
    }

    ParseStorage();

    if (currentStatus != Status.Idle)
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

void ParseStorage()
{
    var storageSplit = Me.CustomData.Split('\n');
    foreach (var line in storageSplit)
    {
        if (line.StartsWith("@"))
        {
            var newLine = line.Replace("@", ""); //remove leading char

            if (newLine.StartsWith("countdown"))
            {
                newLine = newLine.Replace("countdown", "").Trim();
                TimeSpan.TryParse(newLine, out dispenseCountdown);
            }
            else if (newLine.StartsWith("cooldown"))
            {
                newLine = newLine.Replace("cooldown", "").Trim();
                TimeSpan.TryParse(newLine, out cooldown);
            }
            else if (newLine.StartsWith("status"))
            {
                newLine = newLine.Replace("status", "").Trim();
                int value = 0;
                int.TryParse(newLine, out value);
                currentStatus = (Status)value;
            }

            continue;
        }

            var lineSplit = line.Split('-');
        var itemName = lineSplit[0].Trim();

        if (string.IsNullOrWhiteSpace(itemName))
            continue;

        if (lineSplit.Length < 2)
            continue;

        int count = 0;
        bool isInt = int.TryParse(lineSplit[1].Trim(), out count);

        if (!isInt)
            continue;

        itemLog[itemName] = count;
    }

    WriteItemLog();

    Echo("Storage parsed");
}

void Main(string arg, UpdateType updateSource)
{
    if (currentStatus == Status.Idle)
        GetCargoQuota(arg);

    if ((updateSource & UpdateType.Update100) != 0)
    {
        GridTerminalSystem.GetBlocksOfType(beacons, x => x.CustomData.Contains(beaconCustomDataTag));

        switch (currentStatus)
        {
            case Status.Countdown:
                dispenseCountdown = dispenseCountdown - minutesPerCycle;

                if (dispenseCountdown.TotalSeconds <= 0)
                {
                    GridTerminalSystem.GetBlocksOfType(sorters, x => x.CustomName.Contains(sorterName));
                    foreach (var block in sorters)
                    {
                        block.Enabled = true; //sorters on
                    }

                    //Deploy resources
                    TransferCargo();
                    currentStatus = Status.Cooldown; //switch to cooldown

                    foreach (var block in sorters)
                    {
                        block.Enabled = false; //sorters off
                    }

                    foreach (var block in beacons)
                    {
                        block.CustomName = "Resources Deployed!";
                    }
                }
                else
                {
                    foreach (var block in beacons)
                    {
                        block.CustomName = $"Resources Incoming: {GetComponentString()} (T - {dispenseCountdown.Hours}:{dispenseCountdown.Minutes:00}:{dispenseCountdown.Seconds:00})";
                    }
                }

                break;

            case Status.Cooldown:
                cooldown = cooldown - minutesPerCycle;

                if (cooldown.TotalSeconds <= 0)
                {
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    currentStatus = Status.Idle;
                }

                foreach (var block in beacons)
                {
                    block.CustomName = $"Resource Cooldown (T - {cooldown.Hours}:{cooldown.Minutes:00}:{cooldown.Seconds:00})";
                }

                break;
        }

        //Check state after
        if (currentStatus == Status.Idle)
        {
            foreach (var block in beacons)
            {
                block.CustomName = $"Resources Ready";
            }
        }
    }

    WriteItemLog();

    Echo(RunningSymbol());
    Echo($"Status: {currentStatus}");
    Echo($"arg: '{arg}'");
    Echo($"dispense - {dispenseCountdown.Hours}:{dispenseCountdown.Minutes:00}:{dispenseCountdown.Seconds:00}");
    Echo($"cooldown - {cooldown.Hours}:{cooldown.Minutes:00}:{cooldown.Seconds:00}");
    Echo("Transfer cargo blocks: " + transferCargo.Count.ToString());
}

StringBuilder components = new StringBuilder();

string GetComponentString()
{
    components.Clear();
    bool first = true;
    foreach (var kvp in ItemsToTransfer)
    {
        if (first)
        {
            components.Append(kvp.Key);
            first = false;
        }
        else
            components.Append($"/{kvp.Key}");
    }
    return components.ToString();
}

void WriteItemLog()
{
    Me.CustomData = $"@countdown {dispenseCountdown.ToString()}\n@cooldown {cooldown.ToString()}\n@status {(int)currentStatus}\n";

    foreach (var kvp in itemLog)
    {
        Me.CustomData += $"{kvp.Key} - {kvp.Value}\n";
    }

    Storage = Me.CustomData;
}

void GetCargoQuota(string argument)
{
    argument = argument.Replace(" ", "").ToLower(); //force lowercase and remove spaces
    string[] argumentSplit = argument.Split(';'); //split at semicolons
    bool successfulInput = false;

    for (int j = 0; j < argumentSplit.Length; j++)
    {
        string[] item_split = argumentSplit[j].Split('-'); //seperate item from amount
        if (item_split.Length < 2)
        {
            if (!string.IsNullOrWhiteSpace(argumentSplit[j]))
                Echo($"Error: Argugument '{argumentSplit[j]}' is not in the correct format");
            continue;
        }
        var itemDesired = item_split[0].ToLower(); //convert to lowercase
        var amountDesired = Convert.ToInt32(item_split[1]); //convert to a number
        bool isValidItem = true;

        switch (itemDesired) //compare block names to valid components
        {
            case "cooldown":
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
                cooldown = new TimeSpan(0, amountDesired, 0);
                dispenseCountdown = new TimeSpan(0, dispenseTime, 0);
                currentStatus = Status.Countdown;
                isValidItem = false;
                successfulInput = true;
                break;

            case "organic":
            case "point":
            case "points":
            case "xp":
                itemDesired = "Organic";
                break;

            #region component names
            case "co":
            case "cobalt":
                itemDesired = "Cobalt";
                break;

            case "au":
            case "gold":
                itemDesired = "Gold";
                break;

            case "fe":
            case "iron":
                itemDesired = "Iron";
                break;

            case "mg":
            case "magnesium":
                itemDesired = "Magnesium";
                break;

            case "ni":
            case "nickel":
                itemDesired = "Nickel";
                break;

            case "pt":
            case "platinum":
                itemDesired = "Platinum";
                break;

            case "si":
            case "silicon":
                itemDesired = "Silicon";
                break;

            case "ag":
            case "silver":
                itemDesired = "Silver";
                break;

            //ore and ingots
            case "uraniumingot":
            case "uraniumingots":
            case "uranium":
            case "fuel":
            case "u":
                itemDesired = "Uranium";
                break;

            case "stone":
            case "rock":
                itemDesired = "Stone";
                break;

            case "ice":
            case "frozenwater":
                itemDesired = "Ice";
                break;

            //Components
            case "steel":
            case "steelplate":
            case "steelplates":
                itemDesired = "SteelPlate";
                break;

            case "interiorplate":
            case "interiorplates":
                itemDesired = "InteriorPlate";
                break;

            case "construction":
            case "constructioncomponent":
            case "constructioncomponents":
            case "cc":
                itemDesired = "Construction";
                break;

            case "motor":
            case "motors":
                itemDesired = "Motor";
                break;

            case "metalgrid":
            case "metalgrids":
                itemDesired = "MetalGrid";
                break;

            case "largetube":
            case "largetubes":
            case "largesteeltube":
            case "largesteeltubes":
            case "lst":
                itemDesired = "LargeTube";
                break;

            case "smalltube":
            case "smalltubes":
            case "smallsteeltube":
            case "smallsteeltubes":
            case "sst":
                itemDesired = "SmallTube";
                break;

            case "display":
            case "displays":
                itemDesired = "Display";
                break;

            case "computer":
            case "computers":
            case "cpu":
            case "cpus":
                itemDesired = "Computer";
                break;

            case "medical":
            case "medicalcomponent":
            case "medicalcomponents":
                itemDesired = "Medical";
                break;

            case "solarcell":
            case "solarcells":
            case "solar":
                itemDesired = "SolarCell";
                break;

            case "powercell":
            case "powercells":
            case "power":
                itemDesired = "PowerCell";
                break;

            case "detector":
            case "detectors":
            case "detectorcomponent":
            case "detectorcomponents":
                itemDesired = "Detector";
                break;

            case "girder":
            case "girders":
                itemDesired = "Girder";
                break;

            case "thrust":
            case "thruster":
            case "thrusters":
            case "thrustercomponent":
            case "thrustercomponents":
                itemDesired = "Thrust";
                break;

            case "reactor":
            case "reactorcomponent":
            case "reactorcomponents":
                itemDesired = "Reactor";
                break;

            case "bulletproofglass":
            case "glass":
                itemDesired = "BulletproofGlass";
                break;

            case "radiocommunication":
            case "radio":
            case "radiocomponent":
            case "radiocomponents":
            case "radiocommunicationcomponent":
            case "radiocommunicationcomponents":
                itemDesired = "RadioCommunication";
                break;

            case "grav":
            case "gravity":
            case "gravitygenerator":
            case "gravitygeneratorcomponent":
            case "gravitygeneratorcomponents":
            case "gravitycomponent":
            case "gravitycomponents":
                itemDesired = "GravityGenerator";
                break;

            case "oxygenbottle":
            case "oxygenbottles":
            case "oxygen":
            case "o2bottle":
            case "o2bottles":
            case "o2":
                itemDesired = "OxygenBottle";
                break;

            case "superconductor":
            case "superconductors":
                itemDesired = "Superconductor";
                break;

            case "hydrogenbottle":
            case "hydrogenbottles":
            case "hydrogen":
                itemDesired = "HydrogenBottle";
                break;

            //ammo
            case "missile200mm":
            case "missile":
            case "missiles":
            case "rocket":
            case "rockets":
                itemDesired = "Missile200mm";
                break;

            case "nato_25x184mm":
            case "25mm":
            case "25mm NATO":
                itemDesired = "NATO_25x184mm";
                break;

            case "nato_5p56x45mm":
            case "5.56mm":
            case "5.56mm NATO":
                itemDesired = "NATO_5p56x45mm";
                break;
            #endregion

            default: //this means it is none of the above 
                Echo($"Error: '{itemDesired}' is not a valid input");
                isValidItem = false;
                break;
        }

        if (isValidItem)
        {
            ItemsToTransfer.Add(itemDesired, amountDesired);
        }
    }

    if (successfulInput)
    {
        int count = 0;
        itemLog.TryGetValue(GetComponentString(), out count);
        itemLog[GetComponentString()] = count + 1;
    }
}

List<IMyTerminalBlock> transferCargo = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> restockCargo = new List<IMyTerminalBlock>();

void TransferCargo()
{
    GridTerminalSystem.GetBlocksOfType(transferCargo, block => block.HasInventory && block.CustomName.Contains(transferFromTag));
    GridTerminalSystem.GetBlocksOfType(restockCargo, block => block.HasInventory && block.CustomName.Contains(restockName));
    //foreach (IMyTerminalBlock  in restockCargo)
    // {

    if (restockCargo.Count == 0)
    {
        Echo($"ERROR: No cargo named '{restockName}'");
    }

    var thisCargo = restockCargo[0];
    var targetInventory = thisCargo.GetInventory(0);
    FillCargoQuota(targetInventory, transferCargo);
    //}
}

void FillCargoQuota(IMyInventory targetInventory, List<IMyTerminalBlock> transferCargo)
{
    foreach (IMyTerminalBlock transferContainer in transferCargo)
    {

        var items = transferContainer.GetInventory(0).GetItems(); //see what we have in the container
        for (int j = 0; j < items.Count; j++)
        {
            var itemName = items[j].Content.SubtypeName;
            Echo("Checking Item:" + itemName);
            if (ItemsToTransfer.ContainsKey(itemName) && ItemsToTransfer[itemName] > 0)
            {
                int itemAmount = Math.Min((int)ItemsToTransfer[itemName], (int)items[j].Amount);
                //if want > have then we take all we can from what we have currently and try the next container
                Echo("I need " + itemAmount);
                bool isTransferrable = transferContainer.GetInventory(0).TransferItemTo(targetInventory, j, null, true, itemAmount);
                Echo("Items can transfer: " + isTransferrable.ToString());
                if (isTransferrable == true) ItemsToTransfer[itemName] -= itemAmount;
            }
        }
    }

    ItemsToTransfer.Clear();
    Echo("Done");
}

//Whip's Running Symbol Method v8
//•
int runningSymbolVariant = 0;
int runningSymbolCount = 0;
const int increment = 1;
string[] runningSymbols = new string[] { "−", "\\", "|", "/" };

string RunningSymbol()
{
    if (runningSymbolCount >= increment)
    {
        runningSymbolCount = 0;
        runningSymbolVariant++;
        if (runningSymbolVariant >= runningSymbols.Length)
            runningSymbolVariant = 0;
    }
    runningSymbolCount++;
    return runningSymbols[runningSymbolVariant];
}