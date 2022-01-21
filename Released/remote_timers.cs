
/*
/ //// / Whip's SHART | Script Handling Activation of Remote Timers / //// /
__________________________________________
/ //// / BASIC SETUP INSTRUCTIONS / //// / 

    1. Place this script in a programmable block.

    2. Place a timer block.

    3. Place at least one regular antenna or laser antenna.
       - Laser antenna need to actually be linked to another laser antenna to work

    4. Open the Custom Data of the programmable block and set "Timer to trigger on receive" 
       to the name of a timer block. This timer will get triggered when a SHART message is received.
       - You only need to do this if you want to be a receiver. Senders are not required to have a timer configured.

    5. Recompile the script once you are done configuring.

Note: You must recompile the script to process any setup changes.

__________________________________________
/ //// / ARGUMENTS / //// /

    trigger UID 
        - Sends trigger command to a SHART program with the specified Unique ID (UID)

    trigger UID SUBADDRESS 
        - Sends trigger command to a SHART program with the specified Unique ID (UID)
            and triggers the timer associated with the subaddress (if valid).

    start UID 
        - Sends start command to a SHART program with the specified Unique ID (UID)

    start UID SUBADDRESS 
        - Sends start command to a SHART program with the specified Unique ID (UID)
            and starts the timer associated with the subaddress (if valid).

    stop UID 
        - Sends stop command to a SHART program with the specified Unique ID (UID)

    stop UID SUBADDRESS 
        - Sends stop command to a SHART program with the specified Unique ID (UID)
            and stops the timer associated with the subaddress (if valid).
    rename 
        - Generates a new UID for your SHART program
__________________________________________

DO NOT CHANGE VARIABLES IN THIS SCRIPT
        USE THE CUSTOM DATA

















DO NOT CHANGE VARIABLES IN THIS SCRIPT
        USE THE CUSTOM DATA




















DO NOT CHANGE VARIABLES IN THIS SCRIPT
        USE THE CUSTOM DATA
























DO NOT CHANGE VARIABLES IN THIS SCRIPT
        USE THE CUSTOM DATA






*/

const string VERSION = "1.3.4";
const string DATE = "2022/01/19";

string _id = ""; // If blank use EID
string _timerName = "Timer Block";

const string INI_SECTION_GENERAL = "SHART - General";
const string INI_GENERAL_ID = "Unique Identifier (UID)";
const string INI_GENERAL_NAME = "Timer to trigger on receive";
const string INI_GENERAL_SUBADDRESS = "Receive timer subaddresses";

const string INI_COMMENT_ID = @"
 Unique identifier used to address this particular SHART
 instance. You can change this to whatever you want or
 automatically generate a new name with the ""rename""
 argument.
";

const string INI_COMMENT_NAME = @"
 This is the name of the timer that will be triggered ANY
 time a message addressed to this SHART program is received.
 If specified, this timer will trigger regardless of if a
 subaddress is specified by an incomming message.
";

const string INI_COMMENT_SUBADDRESS = @"
 This allows you to specify a list of ""subaddresses""
 so that you can trigger different timers with a single
 programmable block. If a subaddress is specified for an
 incomming SHART message, the script will trigger the timer 
 associated with that subaddress (if it exists). Leave this
 blank if you don't care about subaddresses.

 Subaddress entries must be on their own line and in the form:
 |<subaddress>,<exact timer name>

 EXAMPLE:
 ----------------------------------------
 Receive timer subaddresses=
 |primary,Timer Block 1
 |secondary,Timer Block 2
 |tertiary,Timer Block 3
";

const string IGC_RESPONSE_TAG = "IGC_SHART_RESP";
const string IGC_CALLBACK = "IGC_CALLBACK";

const char DELIM = ',';

StringBuilder _output = new StringBuilder(1024);
MessageBuffer _messageBuffer = new MessageBuffer(10);

IMyTimerBlock _timer;
int _radioAntennaCount = 0;
int _laserAntennaCount = 0;

IMyBroadcastListener _listener = null;
MyIni _ini = new MyIni();
enum TimerAction { None = 0, Start = 1, Trigger = 2, Stop = 3 }

Dictionary<string, IMyTimerBlock> _subaddressTimers = new Dictionary<string, IMyTimerBlock>();

Random _rng = new Random();

RemoteTimerScreenManager _screenManager;

MyCommandLine _cmd = new MyCommandLine();

#region Random Names
string[] _adjectives = new string[] {
    "Fluffy",
    "Angry",
    "Excitable",
    "Frustrated",
    "Gargantuan",
    "Large",
    "Small",
    "Tiny",
    "Teeny",
    "Stupid",
    "Dumb",
    "Compatible",
    "Rude",
    "Acrid",
    "Modern",
    "Puffy",
    "Depressed",
    "Itchy",
    "Cereful",
    "Historical",
    "Economic",
    "Chilean",
    "Public",
    "Tranquil",
    "Tremendous",
    "Erratic",
    "Grouchy",
    "Domineering",
    "Aggressive",
    "Arrogant",
    "Amazing",
    "Swift",
    "Unusual",
    "Frightening",
    "Faithful",
    "Verbose",
    "Stable",
    "Decisive",
    "Fertile",
    "Deez",
    "Secret",
    "Thankful",
    "Uber",
    "Fine",
    "Poor",
    "Southern",
    "Boring",
    "Ugly",
    "Gorgeous",
    "Elated",
    "Magical",
    "Concerned",
    "Ancient",
    "Incorrect",
    "Daft",
    "Blushing",
    "Untidy",
    "Painstaking",
    "Sturdy",
    "Pleasant",
    "Traditional",
    "Stapler",
    "Cheesy",
    "Toed",
    "Stiff",
    "Moist",
    "Frothy",
    "Steamed",
    "Dark",
    "Thicc",
    "Dummy",
    "Dank",
    "Sandy",
    "Sith",
    "Delicious",
    "Eternal",
    "Rancid",
    "Hot",
    "Explosive",
    "Hotheaded",
    "Toxic",
    "Spicy",
    "Concealed",
};

string[] _nouns = new string[] {
    "Nuts",
    "Rash",
    "Toilet",
    "Beans",
    "Apple",
    "Bobcat",
    "Chupacabra",
    "Hands",
    "Genius",
    "Barbequeue",
    "Hamburger",
    "Clown",
    "Ranch",
    "Bellybutton",
    "Foot",
    "Shoe",
    "Cupcake",
    "Airplane",
    "Wombat",
    "Potato",
    "Tomato",
    "Hemorrhoids",
    "Pillowcase",
    "Larynx",
    "Sock",
    "Eggplant",
    "Snowflake",
    "Keyboard",
    "Jalapeno",
    "Stapler",
    "Pickles",
    "Piano",
    "Peregrine",
    "Anaconda",
    "Clam",
    "Flower",
    "Melon",
    "Sausage",
    "Beer",
    "Cream",
    "Butt",
    "Turtle",
    "Water",
    "Cat",
    "Doggo",
    "Rat",
    "Star",
    "Spaghetti",
    "Peach",
    "Robot",
    "Saber",
    "Meme",
    "Cannon",
    "Legend",
    "Missile",
    "Pollution",
    "Bird",
    "Paint",
    "Meat",
    "Bread",
    "Taco",
    "Soda",
    "Singles",
    "Creeper",
    "Plant",
    "Snacc",
    "Meatball",
    "Sheep",
    "Glue",
};
#endregion

StringBuilder _nameBuilder = new StringBuilder(64);
string GenerateNewName()
{
    _nameBuilder.Clear();
    _nameBuilder.Append(_adjectives[_rng.Next(_adjectives.Length)]);
    _nameBuilder.Append(_adjectives[_rng.Next(_adjectives.Length)]);
    _nameBuilder.Append(_nouns[_rng.Next(_nouns.Length)]);
    return _nameBuilder.ToString();
}

Program()
{
    ParseIni();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, CollectBlock);
    RegisterBroadcastHandler();
    _screenManager = new RemoteTimerScreenManager(VERSION, _id);
    var surf = Me.GetSurface(0);
    if (surf != null)
    {
        _screenManager.AddSurface(surf);        
    }
    _screenManager.Draw();
    PrintDebugInfo();
}

void ParseIni(bool writeOnly = false)
{
    _ini.Clear();
    string subaddressString = "";
    if (_ini.TryParse(Me.CustomData) && !writeOnly)
    {
        _id = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_ID).ToString(_id).Trim();
        _timerName = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_NAME).ToString(_timerName);
        _subaddressTimers.Clear();
        subaddressString = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_SUBADDRESS).ToString("");
        if (!string.IsNullOrWhiteSpace(subaddressString))
        {
            var subaddressSplit = subaddressString.Split('\n');
            foreach (string subaddressEntry in subaddressSplit)
            {
                var subaddressParts = subaddressEntry.Split(DELIM);
                if (subaddressParts.Length == 2)
                {
                    var subName = subaddressParts[0];
                    var timerName = subaddressParts[1];
                    var timer = GridTerminalSystem.GetBlockWithName(timerName) as IMyTimerBlock;
                    _subaddressTimers[subName] = timer;
                }
            }
        }
    }

    if (string.IsNullOrWhiteSpace(_id))
    {
        _id = GenerateNewName();
    }

    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_ID, _id);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_NAME, _timerName);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_SUBADDRESS, subaddressString);
    
    _ini.SetComment(INI_SECTION_GENERAL, INI_GENERAL_ID, INI_COMMENT_ID);
    _ini.SetComment(INI_SECTION_GENERAL, INI_GENERAL_NAME, INI_COMMENT_NAME);
    _ini.SetComment(INI_SECTION_GENERAL, INI_GENERAL_SUBADDRESS, INI_COMMENT_SUBADDRESS);

    string output = _ini.ToString();
    if (output != Me.CustomData)
    {
        Me.CustomData = output;
    }
}

bool CollectBlock(IMyTerminalBlock b)
{
    if (b is IMyTimerBlock && b.CustomName == _timerName)
    {
        if (_timer == null)
        {
            _timer = (IMyTimerBlock)b;
        }
    }
    
    if (b is IMyRadioAntenna)
    {
        _radioAntennaCount++;
    }

    if (b is IMyLaserAntenna)
    {
        _laserAntennaCount++;
    }

    return false;
}

void RegisterBroadcastHandler()
{
    if (_listener != null)
    {
        IGC.DisableBroadcastListener(_listener);
    }
    _listener = IGC.RegisterBroadcastListener(_id);
    _listener.SetMessageCallback(IGC_CALLBACK);
}

void Main(string arg, UpdateType updateSource)
{
    if ((updateSource & UpdateType.IGC) != 0 && arg == IGC_CALLBACK)
    {
        if (ProcessMessages())
        {
            _screenManager.Receive();
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }
    }
    else if (!string.IsNullOrWhiteSpace(arg))
    {
        if (arg == "rename")
        {
            _id = GenerateNewName();
            ParseIni(true);
            RegisterBroadcastHandler();
            _messageBuffer.Add($"> Renamed to '{_id}'");
            _screenManager.Id = _id;
        }
        else if (SendRequest(arg))
        {
            _screenManager.Send();
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }
    }
    
    if ((updateSource & UpdateType.Update10) != 0)
    {
        if (!_screenManager.Draw())
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }
    }

    PrintDebugInfo();
}

bool SendRequest(string arg)
{
    _cmd.Clear();
    _cmd.TryParse(arg);
    
    if (_cmd.ArgumentCount == 0)
    {
        return false; // Empty argument
    }
    
    if (_cmd.ArgumentCount < 2)
    {
        _messageBuffer.Add($"> Must have 2 to 3 arguments!");  
        return false;
    }
    
    if (_cmd.ArgumentCount > 3)
    {
        _messageBuffer.Add($"> Warning: Too many arguments!");  
    }
    
    string destinationTag = _cmd.Argument(1);
    string subaddress = _cmd.ArgumentCount > 2 ? _cmd.Argument(2) : null;
    TimerAction destinationTimerAction = TimerAction.None;
    switch(_cmd.Argument(0).ToLowerInvariant())
    {
        case "start":
            destinationTimerAction = TimerAction.Start;
            break;
        
        case "trigger":
            destinationTimerAction = TimerAction.Trigger;
            break;
        
        case "stop":
            destinationTimerAction = TimerAction.Stop;
            break;
        
        default:
            _messageBuffer.Add($"> '{_cmd.Argument(0)}' in argument '{arg}' not recognized");
            return false;
    }
    
    if (string.IsNullOrWhiteSpace(subaddress))
    {
       IGC.SendBroadcastMessage(destinationTag, (int)destinationTimerAction);
       _messageBuffer.Add($"> Sent '{destinationTimerAction}' to '{destinationTag}'"); 
    }
    else
    {
        IGC.SendBroadcastMessage(destinationTag, new MyTuple<int, string>((int)destinationTimerAction, subaddress));
       _messageBuffer.Add($"> Sent '{destinationTimerAction}' to '{destinationTag}' subaddress '{subaddress}'");
    }

    return destinationTimerAction != TimerAction.None;
}

bool ProcessMessages()
{
    bool messageReceived = false;
    while (_listener.HasPendingMessage)
    {
        TimerAction requestedTimerAction = TimerAction.None;
        var message = _listener.AcceptMessage();
        long sender = message.Source;
        string subaddress = null;
        object data = message.Data;
        if (data is int)
        {
            var payload = (int)data;
            requestedTimerAction = (TimerAction)payload;
            _messageBuffer.Add($"> Received '{requestedTimerAction}' message");
        }
        else if (data is MyTuple<int, string>)
        {
            var payload = (MyTuple<int, string>)data;
            requestedTimerAction = (TimerAction)payload.Item1;
            subaddress = payload.Item2;
            _messageBuffer.Add($"> Received '{requestedTimerAction}' message (subaddress: '{subaddress}')");
        }
        else
        {
            continue;
        }

        messageReceived = true;
        SendResponse(sender);
        
        IMyTimerBlock subaddressTimer = null;
        if (!string.IsNullOrWhiteSpace(subaddress))
        {
            if (_subaddressTimers.TryGetValue(subaddress, out subaddressTimer))
            {
                if (subaddressTimer == null)
                {
                    _messageBuffer.Add($"> Timer for subaddress '{subaddress}' not found");
                }
            }
            else
            {
                _messageBuffer.Add($"> Unknown subaddress '{subaddress}'");
            }
        }

        if (requestedTimerAction == TimerAction.Start)
        {
            _timer?.StartCountdown();
            subaddressTimer?.StartCountdown();
        }

        if (requestedTimerAction == TimerAction.Trigger)
        {
            _timer?.Trigger();
            subaddressTimer?.Trigger();
        }
        
        if (requestedTimerAction == TimerAction.Stop)
        {
            _timer?.StopCountdown();
            subaddressTimer?.StopCountdown();
        }
    }

    return messageReceived;
}

void SendResponse(long id)
{
    // TODO
    //IGC.SendUnicastMessage(id, IGC_RESPONSE_TAG, "");
}

void PrintDebugInfo()
{
    _output.Append($"Whip's SHART - Script Handling\nActivation of Remote Timers\n(Version {VERSION} - {DATE})\n\n");
    _output.Append($"Unique Identifier (UID):\n  '{_id}'\n\n");
    _output.Append("Arguments:\n  trigger <UID>\n  start <UID>\n  stop <UID>\n  rename\n\n");
    _output.Append("Recompile to process Custom Data\nor block changes.\n\n");

    if (_timer == null)
    {
        _output.Append($"[Warning] No timer named\n '{_timerName}' found.\n\n");
    }

    if (_radioAntennaCount == 0 && _laserAntennaCount == 0)
    {
        _output.Append($"[Warning] No antenna(s) detected\non the grid. This script will\nlikely not work.\n\n");
    }

    _output.Append($"Message Log:\n{_messageBuffer}");


    Echo(_output.ToString()); 
    _output.Clear();
}

/// <summary>
/// Strng message buffer that keeps track of duplicates.
///
/// Dependencies:
///   OrderedCircularBuffer
/// </summary>
class MessageBuffer
{
    public int Capacity { get { return _buffer.Capacity; } }

    public string this[int index]
    {
        get
        {
            return _buffer[index];
        }
        set
        {
            _buffer[index] = value;
        }
    }

    OrderedCircularBuffer<string> _buffer;
    string _lastMsg;
    StringBuilder _builder = new StringBuilder(256);
    int _count = 0;

    public MessageBuffer(int capacity)
    {
        _buffer = new OrderedCircularBuffer<string>(capacity);
    }

    public void Add(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg))
        {
            return;
        }

        if (string.Equals(msg, _lastMsg))
        {
            _count++;
            _buffer[0] = $"{msg} (x{_count})";
        }
        else
        {
            _count = 1;
            _lastMsg = msg;
            _buffer.Add(msg);
        }
    }
    
    public override string ToString()
    {
        _builder.Clear();
        for (int i = 0; i < Capacity; ++i)
        {
            string msg = _buffer[i];
            if (!string.IsNullOrWhiteSpace(msg))
            {
                _builder.AppendLine(msg);
            }
        }
        return _builder.ToString();
    }
}

/// <summary>
/// An ordered, generic circular buffer class with a fixed capacity.
/// The newest item is always the first element in the buffer and the oldest is always the last.
/// </summary>
/// <typeparam name="T"></typeparam>
public class OrderedCircularBuffer<T>
{
    public readonly int Capacity;

    readonly T[] _array = null;

    public T this[int index]
    {
        get
        {
            return _array[index];
        }
        set
        {
            _array[index] = value;
        }
    }

    /// <summary>
    /// OrderedCircularBuffer ctor.
    /// </summary>
    /// <param name="capacity">Capacity of the CircularBuffer.</param>
    public OrderedCircularBuffer(int capacity)
    {
        if (capacity < 2)
            throw new Exception($"Capacity of CircularBuffer ({capacity}) can not be less than 2");
        Capacity = capacity;
        _array = new T[Capacity];
    }

    /// <summary>
    /// Adds an item to the front of the buffer and shifts all existing values down an index.
    /// </summary>
    /// <param name="item"></param>
    public void Add(T item)
    {
        // Shift all items down an index, overwriting the last element
        // and preparing to overwrite the first element
        Array.Copy(_array, 0, _array, 1, Capacity - 1);

        // Set the first item to the new item
        _array[0] = item;
    }
}

class RemoteTimerScreenManager
{
    string _id;
    public string Id
    {
        get
        {
            return _id;
        }
        set
        {
            if (value != _id)
            {
                _id = value;
                ForceDraw();
                Draw();
            }
        }
    }
    
    readonly Vector2 _antennaLinePos = new Vector2(150f, 30f);
    readonly Vector2 _timerPos = new Vector2(-150f, 135f);
    readonly Vector2 _antennaPos = new Vector2(0f, 60f);
    readonly Color _timerBlinkColor = new Color(0, 100, 255, 255);
    readonly Color _timerIdleColor = new Color(0, 255, 0);
    readonly Color _topBarColor = new Color(25, 25, 25);
    readonly Color _titleTextColor = new Color(255, 255, 255);
    readonly Color _baseColor = new Color(255, 255, 255);

    const float AntennaSpriteScale = 0.75f;
    const float TimerSpriteScale = 1.25f;
    const float AntennaLinesSpriteScale = 1.25f;
    const float TitleBarHeightPx = 64f;
    const float TextSize = 1.5f;
    const float TextSizeSecondary = 1.0f;
    const float BaseTextHeightPx = 37f;
    const string Font = "DEBUG";
    const string TitleFormat = "SHART - v{0}";
    readonly string _titleText;

    bool _clearSpriteCache = false;

    Queue<Action<MySpriteDrawFrame, Vector2, float>> _frameQueue = new Queue<Action<MySpriteDrawFrame, Vector2, float>>();
    List<IMyTextSurface> _surfaces = new List<IMyTextSurface>();

    public RemoteTimerScreenManager(string version, string id)
    {
        _titleText = string.Format(TitleFormat, version);
        Id = id;
    }

    public void ForceDraw()
    {
        _clearSpriteCache = !_clearSpriteCache;
    }

    public void AddSurface(IMyTextSurface s)
    {
        _surfaces.Add(s);
    }

    public void ClearSurfaces()
    {
        _surfaces.Clear();
    }

    public bool Draw()
    {
        Action<MySpriteDrawFrame, Vector2, float> frameFunc = null;
        bool framesLeft = _frameQueue.Count > 0;
        if (framesLeft)
        {
            frameFunc = _frameQueue.Dequeue();
        }
        
        foreach (var surface in _surfaces)
        {
            surface.ContentType = ContentType.SCRIPT;
            surface.Script = "";

            Vector2 textureSize = surface.TextureSize;
            Vector2 screenCenter = textureSize * 0.5f;
            Vector2 viewportSize = surface.SurfaceSize;
            Vector2 scale = viewportSize / 512f;
            float minScale = Math.Min(scale.X, scale.Y);
            

            using (var frame = surface.DrawFrame())
            {
                SetupDrawSurface(surface);

                if (_clearSpriteCache)
                {
                    frame.Add(new MySprite());
                }

                if (frameFunc != null)
                {
                    
                    frameFunc.Invoke(frame, screenCenter, minScale);
                    framesLeft = true;
                }
                else
                {
                    DrawBaseFrame(frame, screenCenter, minScale);
                    framesLeft = false;
                }

                DrawTitleBar(surface, frame, minScale);
            }
        }
        
        return framesLeft;
    }

    public void Send()
    {
        ForceDraw();
        _frameQueue.Enqueue(DrawSendFrame1);
        _frameQueue.Enqueue(DrawSendFrame2);
        _frameQueue.Enqueue(DrawSendFrame3);
        _frameQueue.Enqueue(DrawSendFrame4);
        _frameQueue.Enqueue(DrawBaseFrame);
    }

    public void Receive()
    {
        ForceDraw();
        _frameQueue.Enqueue(DrawReceiveFrame1);
        _frameQueue.Enqueue(DrawReceiveFrame2);
        _frameQueue.Enqueue(DrawReceiveFrame3);
        _frameQueue.Enqueue(DrawReceiveFrame4);
        _frameQueue.Enqueue(DrawBaseFrame);
    }

    void DrawBaseFrame(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        DrawAntenna(frame, centerPos + scale * _antennaPos, scale * AntennaSpriteScale);
        DrawTimer(frame, centerPos + scale * _timerPos, _timerIdleColor, scale * TimerSpriteScale);
    }

    #region Send
    void DrawSendFrame1(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        DrawAntennaLines1(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, 0);
        DrawAntennaLinesClip(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, 0);
        DrawAntenna(frame, centerPos + scale * _antennaPos, scale * AntennaSpriteScale);
        DrawTimer(frame, centerPos + scale * _timerPos, _timerIdleColor, scale * TimerSpriteScale);
    }

    void DrawSendFrame2(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        DrawAntennaLines2(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, 0);
        DrawAntennaLinesClip(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, 0);
        DrawAntenna(frame, centerPos + scale * _antennaPos, scale * AntennaSpriteScale);
        DrawTimer(frame, centerPos + scale * _timerPos, _timerIdleColor, scale * TimerSpriteScale);
    }

    void DrawSendFrame3(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        DrawAntennaLines3(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, 0);
        DrawAntennaLinesClip(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, 0);
        DrawAntenna(frame, centerPos + scale * _antennaPos, scale * AntennaSpriteScale);
        DrawTimer(frame, centerPos + scale * _timerPos, _timerIdleColor, scale * TimerSpriteScale);
    }

    void DrawSendFrame4(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        DrawSendFrame3(frame, centerPos, scale);
    }
    #endregion

    #region Receive
    void DrawReceiveFrame1(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        DrawAntennaLines1(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, MathHelper.Pi);
        DrawAntennaLinesClip(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, MathHelper.Pi);
        DrawAntenna(frame, centerPos + scale * _antennaPos, scale * AntennaSpriteScale);
        DrawTimer(frame, centerPos + scale * _timerPos, _timerIdleColor, scale * TimerSpriteScale);
    }

    void DrawReceiveFrame2(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        DrawAntennaLines2(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, MathHelper.Pi);
        DrawAntennaLinesClip(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, MathHelper.Pi);
        DrawAntenna(frame, centerPos + scale * _antennaPos, scale * AntennaSpriteScale);
        DrawTimer(frame, centerPos + scale * _timerPos, _timerIdleColor, scale * TimerSpriteScale);
    }

    void DrawReceiveFrame3(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        DrawAntennaLines3(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, MathHelper.Pi);
        DrawAntennaLinesClip(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, MathHelper.Pi);
        DrawAntenna(frame, centerPos + scale * _antennaPos, scale * AntennaSpriteScale);
        DrawTimer(frame, centerPos + scale * _timerPos, _timerBlinkColor, scale * TimerSpriteScale);
    }

    void DrawReceiveFrame4(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        DrawAntennaLines3(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, MathHelper.Pi);
        DrawAntennaLinesClip(frame, centerPos + scale * _antennaLinePos, scale * AntennaLinesSpriteScale, MathHelper.Pi);
        DrawAntenna(frame, centerPos + scale * _antennaPos, scale * AntennaSpriteScale);
        DrawTimer(frame, centerPos + scale * _timerPos, _timerBlinkColor, scale * TimerSpriteScale);
    }
    #endregion

    #region Draw Helper Functions
    void DrawTitleBar(IMyTextSurface surface, MySpriteDrawFrame frame, float scale)
    {
        float titleBarHeight = scale * TitleBarHeightPx;
        Vector2 topLeft = 0.5f * (surface.TextureSize - surface.SurfaceSize);
        Vector2 titleBarSize = new Vector2(surface.TextureSize.X, titleBarHeight);
        Vector2 titleBarPos = topLeft + new Vector2(surface.TextureSize.X * 0.5f, titleBarHeight * 0.5f);
        Vector2 titleBarTextPos = topLeft + new Vector2(surface.TextureSize.X * 0.5f, 0.5f * (titleBarHeight - scale * BaseTextHeightPx));
        Vector2 uidTextPos = titleBarTextPos + new Vector2(0, titleBarHeight);

        // Title bar
        frame.Add(new MySprite()
        {
            // mask bottom
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = titleBarPos,
            Size = titleBarSize,
            Color = _topBarColor,
            RotationOrScale = 0,
        });

        // Title bar text
        frame.Add(new MySprite()
        {
            // mask bottom
            Type = SpriteType.TEXT,
            Alignment = TextAlignment.CENTER,
            Data = _titleText,
            Position = titleBarTextPos,
            Color = _titleTextColor,
            RotationOrScale = TextSize * scale,
            FontId = Font,
        });
        
        // Id text
        frame.Add(new MySprite()
        {
            // mask bottom
            Type = SpriteType.TEXT,
            Alignment = TextAlignment.CENTER,
            Data = $"UID: {Id}",
            Position = uidTextPos,
            Color = _titleTextColor,
            RotationOrScale = TextSizeSecondary * scale,
            FontId = Font,
        });
    }

    void SetupDrawSurface(IMyTextSurface surface)
    {
        // Draw background color
        surface.ScriptBackgroundColor = new Color(0, 0, 0, 255);

        // Set content type
        surface.ContentType = ContentType.SCRIPT;

        // Set script to none
        surface.Script = "";
    }

    void DrawAntennaLinesClip(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite()
        {
            // mask bottom
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SemiCircle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(360f, 360f) * scale,
            Color = new Color(0, 0, 0, 255),
            RotationOrScale = -2.2689f + rotation,
        });
        frame.Add(new MySprite()
        {
            // mask top
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SemiCircle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(360f, 360f) * scale,
            Color = new Color(0, 0, 0, 255),
            RotationOrScale = -0.8727f + rotation,
        });
    }

    void DrawAntennaLines1(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite()
        {
            // circ2
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "Circle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(140f, 140f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f + rotation,
        });
        frame.Add(new MySprite()
        {
            // circ1
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "Circle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(100f, 100f) * scale,
            Color = new Color(0, 0, 0, 255),
            RotationOrScale = 0f + rotation,
        });
        DrawAntennaLinesClip(frame, centerPos, scale, rotation);
    }

    void DrawAntennaLines2(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite()
        {
            // circ4
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "Circle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(240f, 240f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f + rotation,
        });
        frame.Add(new MySprite()
        {
            // circ3
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "Circle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(200f, 200f) * scale,
            Color = new Color(0, 0, 0, 255),
            RotationOrScale = 0f + rotation,
        });
        frame.Add(new MySprite()
        {
            // circ2
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "Circle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(140f, 140f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f + rotation,
        });
        frame.Add(new MySprite()
        {
            // circ1
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "Circle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(100f, 100f) * scale,
            Color = new Color(0, 0, 0, 255),
            RotationOrScale = 0f + rotation,
        });
        DrawAntennaLinesClip(frame, centerPos, scale, rotation);
    }

    void DrawAntennaLines3(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite()
        {
            // circ6
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "Circle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(340f, 340f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f + rotation,
        });
        frame.Add(new MySprite()
        {
            // circ5
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "Circle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(300f, 300f) * scale,
            Color = new Color(0, 0, 0, 255),
            RotationOrScale = 0f + rotation,
        });
        frame.Add(new MySprite()
        {
            // circ4
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "Circle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(240f, 240f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f + rotation,
        });
        frame.Add(new MySprite()
        {
            // circ3
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "Circle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(200f, 200f) * scale,
            Color = new Color(0, 0, 0, 255),
            RotationOrScale = 0f + rotation,
        });
        frame.Add(new MySprite()
        {
            // circ2
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "Circle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(140f, 140f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f + rotation,
        });
        frame.Add(new MySprite()
        {
            // circ1
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "Circle",
            Position = new Vector2(cos * -100f - sin * 0f, sin * -100f + cos * 0f) * scale + centerPos,
            Size = new Vector2(100f, 100f) * scale,
            Color = new Color(0, 0, 0, 255),
            RotationOrScale = 0f + rotation,
        });
        DrawAntennaLinesClip(frame, centerPos, scale, rotation);
    }

    void DrawAntenna(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        frame.Add(new MySprite()
        {
            // pole top offcenter
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(20f, -130f) * scale + centerPos,
            Size = new Vector2(10f, 250f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f,
        });
        frame.Add(new MySprite()
        {
            // pole top center
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(0f, -105f) * scale + centerPos,
            Size = new Vector2(10f, 200f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f,
        });
        frame.Add(new MySprite()
        {
            // pole junction slope
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "RightTriangle",
            Position = new Vector2(24f, 40f) * scale + centerPos,
            Size = new Vector2(50f, 20f) * scale,
            Color = _baseColor,
            RotationOrScale = 1.5708f,
        });
        frame.Add(new MySprite()
        {
            // pole junction top
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(25f, 5f) * scale + centerPos,
            Size = new Vector2(20f, 20f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f,
        });
        frame.Add(new MySprite()
        {
            // pole junction
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(0f, 30f) * scale + centerPos,
            Size = new Vector2(30f, 70f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f,
        });
        frame.Add(new MySprite()
        {
            // pole bottom
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(0f, 115f) * scale + centerPos,
            Size = new Vector2(20f, 100f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f,
        });
        frame.Add(new MySprite()
        {
            // base elevation
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(0f, 170f) * scale + centerPos,
            Size = new Vector2(40f, 10f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f,
        });
        frame.Add(new MySprite()
        {
            // base
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(0f, 180f) * scale + centerPos,
            Size = new Vector2(150f, 10f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f,
        });
        frame.Add(new MySprite()
        {
            // side antenna square
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(-30f, 14f) * scale + centerPos,
            Size = new Vector2(30f, 16f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f,
        });
        frame.Add(new MySprite()
        {
            // side antenna triCopy
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(-56f, 13f) * scale + centerPos,
            Size = new Vector2(20f, 5f) * scale,
            Color = _baseColor,
            RotationOrScale = 3.1416f,
        });
        frame.Add(new MySprite()
        {
            // side antenna tri
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "RightTriangle",
            Position = new Vector2(-30f, 29f) * scale + centerPos,
            Size = new Vector2(30f, 16f) * scale,
            Color = _baseColor,
            RotationOrScale = 3.1416f,
        });
    }

    void DrawTimer(MySpriteDrawFrame frame, Vector2 centerPos, Color color, float scale = 1f)
    {
        frame.Add(new MySprite()
        {
            // block
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(0f, 0f) * scale + centerPos,
            Size = new Vector2(100f, 100f) * scale,
            Color = _baseColor,
            RotationOrScale = 0f,
        });
        frame.Add(new MySprite()
        {
            // right stripe
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(20f, 0f) * scale + centerPos,
            Size = new Vector2(10f, 100f) * scale,
            Color = new Color(0, 0, 0, 255),
            RotationOrScale = 0f,
        });
        frame.Add(new MySprite()
        {
            // bottom stripe
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(0f, 20f) * scale + centerPos,
            Size = new Vector2(50f, 10f) * scale,
            Color = new Color(0, 0, 0, 255),
            RotationOrScale = 0f,
        });
        frame.Add(new MySprite()
        {
            // top stripe
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(0f, -20f) * scale + centerPos,
            Size = new Vector2(50f, 10f) * scale,
            Color = new Color(0, 0, 0, 255),
            RotationOrScale = 0f,
        });
        frame.Add(new MySprite()
        {
            // left stripe
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(-20f, 0f) * scale + centerPos,
            Size = new Vector2(10f, 100f) * scale,
            Color = new Color(0, 0, 0, 255),
            RotationOrScale = 0f,
        });
        frame.Add(new MySprite()
        {
            // blinky bit
            Type = SpriteType.TEXTURE,
            Alignment = TextAlignment.CENTER,
            Data = "SquareSimple",
            Position = new Vector2(0f, 0f) * scale + centerPos,
            Size = new Vector2(30f, 30f) * scale,
            Color = color,
            RotationOrScale = 0f,
        });
    }
    #endregion
}
