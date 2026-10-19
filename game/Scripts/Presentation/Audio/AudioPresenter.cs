using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Audio;
using Pantheon.Game.Settings;

namespace Pantheon.Client.Presentation.Audio;

/// <summary>
/// Plays the game's sound (spec §60, ADR-021): the sounds of events where they happen (thunder and
/// roars carry across the island), the player's news, the wind and the rain where the camera looks,
/// and music whose mood follows the game. The sounds are synthesized by <see cref="SoundBank"/> and
/// <see cref="MusicComposer"/> on a worker thread at start; until they are ready the game is silent.
/// </summary>
internal sealed partial class AudioPresenter : Node
{
    private const string MusicBus = "Music";
    private const string EffectsBus = "Effects";
    private const string AmbienceBus = "Ambience";
    private const int WorldVoices = 10;
    private const int NewsVoices = 3;

    // The music changes mood at most this often, and fades over this long (real seconds).
    private const double MoodSeconds = 20.0;
    private const float FadeSeconds = 3f;

    // A sound of one kind starts at most this often (real seconds), so spreading fires do not roar.
    private const double KindGapSeconds = 0.4;

    private readonly Dictionary<SoundKind, AudioStreamWav> _sounds = [];
    private readonly Dictionary<MusicMood, AudioStreamWav> _music = [];
    private readonly Dictionary<SoundKind, double> _lastPlayed = [];
    private readonly List<AudioStreamPlayer3D> _world = [];
    private readonly List<AudioStreamPlayer> _news = [];
    private AudioStreamPlayer _musicNow = null!;
    private AudioStreamPlayer _musicNext = null!;
    private AudioStreamPlayer _rain = null!;
    private AudioStreamPlayer _wind = null!;
    private Task<(Dictionary<SoundKind, float[]> Sounds, Dictionary<MusicMood, float[]> Music, double Milliseconds)>? _making;
    private GameSession _session = null!;
    private Func<GroundPosition> _focus = null!;
    private Func<GroundPosition, float> _height = null!;
    private MusicMood? _mood;
    private double _clock;
    private double _moodSince = double.NegativeInfinity;
    private float _fade = 1f;

    /// <summary>Starts making the sounds and connects to the session.</summary>
    /// <param name="session">The session.</param>
    /// <param name="focus">Where the camera looks.</param>
    /// <param name="height">The ground's height at a point, to place sounds on it.</param>
    /// <param name="settings">The player's settings, for the loudness of each part.</param>
    public void Setup(GameSession session, Func<GroundPosition> focus, Func<GroundPosition, float> height, GameSettings settings)
    {
        (_session, _focus, _height) = (session, focus, height);
        EnsureBuses();
        ApplyVolumes(settings);
        for (var i = 0; i < WorldVoices; i++)
        {
            var voice = new AudioStreamPlayer3D { Bus = EffectsBus, UnitSize = 160f, MaxDistance = 3000f, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance, MaxDb = 3f };
            AddChild(voice);
            _world.Add(voice);
        }

        for (var i = 0; i < NewsVoices; i++)
        {
            var voice = new AudioStreamPlayer { Bus = EffectsBus };
            AddChild(voice);
            _news.Add(voice);
        }

        (_musicNow, _musicNext) = (Player(MusicBus), Player(MusicBus));
        (_rain, _wind) = (Player(AmbienceBus), Player(AmbienceBus));
        _making = Task.Run(Make);
    }

    /// <summary>Sets each bus's loudness from the settings.</summary>
    /// <param name="settings">The settings.</param>
    public static void ApplyVolumes(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        EnsureBuses();
        Volume("Master", settings.MasterVolume);
        Volume(MusicBus, settings.MusicVolume);
        Volume(EffectsBus, settings.EffectsVolume);
        Volume(AmbienceBus, settings.AmbienceVolume);
    }

    /// <summary>Plays the sounds of a frame's events.</summary>
    /// <param name="events">The events.</param>
    public void Hear(IReadOnlyList<WorldEvent> events)
    {
        if (_sounds.Count == 0)
        {
            return;
        }

        foreach (var cue in AudioDirector.Cues(events, _session.World, _session.Content.Miracles, _session.LocalGodId))
        {
            if (_lastPlayed.TryGetValue(cue.Kind, out var last) && _clock - last < KindGapSeconds)
            {
                continue;
            }

            _lastPlayed[cue.Kind] = _clock;
            Play(cue);
        }
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        _clock += delta;
        if (_making is { IsCompleted: true })
        {
            Take(_making.Result);
            _making = null;
        }

        if (_session is null || _sounds.Count == 0)
        {
            return;
        }

        var (rain, wind) = AudioDirector.Ambience(_session.Content.Weather, _session.World, _focus());
        Ease(_rain, rain, delta);
        Ease(_wind, wind, delta);
        UpdateMusic((float)delta);
    }

    /// <summary>Makes every sound and the music, off the main thread.</summary>
    private static (Dictionary<SoundKind, float[]>, Dictionary<MusicMood, float[]>, double) Make()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var sounds = new Dictionary<SoundKind, float[]>();
        foreach (var kind in Enum.GetValues<SoundKind>())
        {
            sounds[kind] = SoundBank.Render(kind);
        }

        var music = new Dictionary<MusicMood, float[]>();
        foreach (var mood in Enum.GetValues<MusicMood>())
        {
            music[mood] = MusicComposer.Render(mood);
        }

        return (sounds, music, watch.Elapsed.TotalMilliseconds);
    }

    /// <summary>Turns the made samples into streams and starts the loops.</summary>
    private void Take((Dictionary<SoundKind, float[]> Sounds, Dictionary<MusicMood, float[]> Music, double Milliseconds) made)
    {
        GD.Print(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Audio ready: {made.Sounds.Count} sounds and {made.Music.Count} moods of music made in {made.Milliseconds:0} ms"));
        foreach (var (kind, samples) in made.Sounds)
        {
            _sounds[kind] = Stream(samples, stereo: false, loop: SoundBank.Loops(kind));
        }

        foreach (var (mood, samples) in made.Music)
        {
            _music[mood] = Stream(samples, stereo: true, loop: true);
        }

        foreach (var (player, kind) in new[] { (_rain, SoundKind.Rain), (_wind, SoundKind.Wind) })
        {
            player.Stream = _sounds[kind];
            player.VolumeDb = -60f;
            player.Play();
        }
    }

    /// <summary>Plays a cue where it happens, or as the player's news.</summary>
    private void Play(SoundCue cue)
    {
        var db = Mathf.LinearToDb(Math.Max(0.001f, cue.Volume));
        if (cue.Position is { } where)
        {
            var voice = _world.Find(v => !v.Playing) ?? _world[0];
            voice.Stream = _sounds[cue.Kind];
            voice.VolumeDb = db;
            voice.GlobalPosition = new Vector3(where.X, _height(where) + 4f, where.Z);
            voice.Play();
            return;
        }

        var news = _news.Find(v => !v.Playing) ?? _news[0];
        news.Stream = _sounds[cue.Kind];
        news.VolumeDb = db - 3f;
        news.Play();
    }

    /// <summary>Fades the music towards the game's mood, holding a mood for a while so it does not flicker.</summary>
    private void UpdateMusic(float delta)
    {
        var wanted = AudioDirector.Mood(_session.World, _session.LocalGodId);
        if (wanted != _mood && _clock - _moodSince >= MoodSeconds && _music.TryGetValue(wanted, out var stream))
        {
            (_musicNow, _musicNext) = (_musicNext, _musicNow);
            _musicNow.Stream = stream;
            _musicNow.VolumeDb = -60f;
            _musicNow.Play();
            _mood = wanted;
            _moodSince = _clock;
            _fade = 0f;
        }

        if (_fade < 1f)
        {
            _fade = Math.Min(1f, _fade + (delta / FadeSeconds));
            _musicNow.VolumeDb = Mathf.LinearToDb(Math.Max(0.001f, _fade));
            _musicNext.VolumeDb = Mathf.LinearToDb(Math.Max(0.001f, 1f - _fade));
            if (_fade >= 1f)
            {
                _musicNext.Stop();
            }
        }
    }

    /// <summary>Moves a loop's loudness smoothly towards a level (0..1).</summary>
    private static void Ease(AudioStreamPlayer player, float level, double delta)
    {
        var now = Mathf.DbToLinear(player.VolumeDb);
        var next = now + ((level - now) * (float)Math.Min(1.0, delta * 1.5));
        player.VolumeDb = Mathf.LinearToDb(Math.Max(0.001f, next));
    }

    /// <summary>A non-positional player on a bus.</summary>
    private AudioStreamPlayer Player(string bus)
    {
        var player = new AudioStreamPlayer { Bus = bus };
        AddChild(player);
        return player;
    }

    /// <summary>16-bit samples as a stream.</summary>
    private static AudioStreamWav Stream(float[] samples, bool stereo, bool loop)
    {
        var data = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var value = (short)Math.Clamp((int)(samples[i] * short.MaxValue), short.MinValue, short.MaxValue);
            data[2 * i] = (byte)(value & 0xff);
            data[(2 * i) + 1] = (byte)((value >> 8) & 0xff);
        }

        var frames = stereo ? samples.Length / 2 : samples.Length;
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = SoundBank.SampleRate,
            Stereo = stereo,
            Data = data,
            LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
            LoopBegin = 0,
            LoopEnd = loop ? frames : 0,
        };
    }

    /// <summary>Adds the music, effects and ambience buses, sending to the master bus, when they are missing.</summary>
    private static void EnsureBuses()
    {
        foreach (var name in new[] { MusicBus, EffectsBus, AmbienceBus })
        {
            if (AudioServer.GetBusIndex(name) < 0)
            {
                AudioServer.AddBus();
                var index = AudioServer.BusCount - 1;
                AudioServer.SetBusName(index, name);
                AudioServer.SetBusSend(index, "Master");
            }
        }
    }

    /// <summary>Sets a bus's loudness (0..1); silent at 0.</summary>
    private static void Volume(string bus, float level)
    {
        var index = AudioServer.GetBusIndex(bus);
        if (index >= 0)
        {
            AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Math.Max(0.0001f, level)));
            AudioServer.SetBusMute(index, level <= 0f);
        }
    }
}
