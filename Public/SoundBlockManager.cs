
class SoundBlockManager
{
    public bool ShouldLoop = true;

    public float SoundDuration
    {
        get
        {
            return _soundDuration;
        }
        set
        {
            if (Math.Abs(value - _soundDuration) < 1e-3)
            {
                return;
            }
            _soundDuration = value;
            _settingsDirty = true;
        }
    }

    public float LoopDuration;

    public bool ShouldPlay
    {
        get
        {
            return _shouldPlay;
        }
        set
        {
            if (value == _shouldPlay)
                return;
            _shouldPlay = value;
            _hasPlayed = false;
        }
    }

    public string SoundName
    {
        get
        {
            return _soundName;
        }
        set
        {
            if (value == _soundName)
            {
                return;
            }
            _soundName = value;
            _settingsDirty = true;
        }
    }

    public List<IMySoundBlock> SoundBlocks;

    bool _settingsDirty = false;
    bool _shouldPlay = false;
    float _soundDuration;
    string _soundName;
    bool _hasPlayed = false;
    float _loopTime;
    float _soundPlayTime;

    enum SoundBlockAction { None = 0, UpdateSettings = 1, Play = 2, Stop = 4 }

    public void Update(float dt)
    {
        SoundBlockAction action = SoundBlockAction.None;

        if (_settingsDirty)
        {
            action |= SoundBlockAction.UpdateSettings;
            _settingsDirty = false;
        }

        if (ShouldPlay)
        {
            if (!_hasPlayed)
            {
                action |= SoundBlockAction.Play;
                _hasPlayed = true;
                _soundPlayTime = 0;
                _loopTime = 0;
            }
            else
            {
                _loopTime += dt;
                _soundPlayTime += dt;
                if (_soundPlayTime >= SoundDuration)
                {
                    action |= SoundBlockAction.Stop;
                    if (!ShouldLoop)
                    {
                        ShouldPlay = false;
                    }
                }

                if (ShouldLoop && _loopTime >= LoopDuration && _hasPlayed)
                {
                    _hasPlayed = false;
                }
            }
        }
        else
        {
            action |= SoundBlockAction.Stop;
        }

        // Apply sound block action
        if (action != SoundBlockAction.None && SoundBlocks != null)
        {
            foreach (var sb in SoundBlocks)
            {
                if ((action & SoundBlockAction.UpdateSettings) != 0)
                {
                    sb.LoopPeriod = 100f;
                    sb.SelectedSound = SoundName;
                }
                if ((action & SoundBlockAction.Play) != 0)
                {
                    sb.Play();
                }
                if ((action & SoundBlockAction.Stop) != 0)
                {
                    sb.Stop();
                }
            }
        }
    }
}
