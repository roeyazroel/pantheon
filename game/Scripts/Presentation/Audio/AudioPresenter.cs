using System;
using System.Collections.Concurrent;
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
/// and the orchestra's music, whose mood follows the game (ADR-071): the Theme on the title screen. The
/// sounds are synthesized by <see cref="SoundBank"/> on a worker thread at start, and the music's pieces
/// by <see cref="Pantheon.Game.Audio.Orchestra.Orchestra"/> on worker threads, the Theme first; each
/// plays once it is ready.
/// </summary>
internal sealed partial class AudioPresenter : Node
{
    private const string MusicBus = "Music";
    private const string EffectsBus = "Effects";
    private const string AmbienceBus = "Ambience";
    private const int WorldVoices = 10;
    private const int NewsVoices = 3;

    // The music stays in a mood at least this long before it calms, and fades over this long (real
    // seconds); a more urgent mood (war, disaster, triumph) comes in at once and quicker.
    private const double MoodSeconds = 20.0;
    private const float FadeSeconds = 3f;
    private const float UrgentFadeSeconds = 1.5f;

    // A sound of one kind starts at most this often (real seconds), so spreading fires do not roar.
    private const double KindGapSeconds = 0.4;

    private readonly Dictionary<SoundKind, AudioStreamWav> _sounds = [];
    private readonly Dictionary<MusicMood, AudioStreamWav> _music = [];
    private readonly ConcurrentDictionary<MusicMood, byte[]> _pieces = new();
    private readonly Dictionary<SoundKind, double> _lastPlayed = [];
    private readonly List<AudioStreamPlayer3D> _world = [];
    private readonly List<AudioStreamPlayer> _news = [];
    private AudioStreamPlayer _musicNow = null!;
    private AudioStreamPlayer _musicNext = null!;
    private AudioStreamPlayer _rain = null!;
    private AudioStreamPlayer _wind = null!;
    private Task<(Dictionary<SoundKind, float[]> Sounds, double Milliseconds)>? _making;
    private Task? _composing;
    private Pantheon.Game.Audio.Orchestra.MusicContent? _score;
    private Func<bool> _title = static () => false;
    private MusicMood? _forced;
    private string? _export;
    private float _fadeSeconds = FadeSeconds;
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
    /// <param name="options">Launch options: a mood forced (<c>--music</c>) or the music written out (<c>--music-export</c>).</param>
    /// <param name="title">Whether the title screen is open, when the Theme plays.</param>
    public void Setup(GameSession session, Func<GroundPosition> focus, Func<GroundPosition, float> height, GameSettings settings, Pantheon.Game.Launch.LaunchOptions options, Func<bool> title)
    {
        ArgumentNullException.ThrowIfNull(options);
        (_session, _focus, _height, _title, _forced, _export) = (session, focus, height, title, options.Music, options.MusicExport);
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
        _score = Bootstrap.Startup.TryLoadMusic();
        if (_score is { } score)
        {
            _composing = Task.Run(() => Compose(score));
        }
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

        TakePieces();

        if (_session is null)
        {
            return;
        }

        UpdateMusic((float)delta);
        if (_sounds.Count == 0)
        {
            return;
        }

        var (rain, wind) = AudioDirector.Ambience(_session.Content.Weather, _session.World, _focus());
        Ease(_rain, rain, delta);
        Ease(_wind, wind, delta);
    }

    /// <summary>Makes every sound, off the main thread.</summary>
    private static (Dictionary<SoundKind, float[]>, double) Make()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var sounds = new Dictionary<SoundKind, float[]>();
        foreach (var kind in Enum.GetValues<SoundKind>())
        {
            sounds[kind] = SoundBank.Render(kind);
        }

        return (sounds, watch.Elapsed.TotalMilliseconds);
    }

    /// <summary>Plays every mood's piece on worker threads, the Theme first, keeping each as it is done; and writes them out when asked.</summary>
    private void Compose(Pantheon.Game.Audio.Orchestra.MusicContent score)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var played = new ConcurrentDictionary<MusicMood, float[]>();
        var order = new List<MusicMood> { MusicMood.Theme, _forced ?? MusicMood.Calm };
        order.AddRange(Array.FindAll(Enum.GetValues<MusicMood>(), m => !order.Contains(m)));
        Parallel.ForEach(order, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, System.Environment.ProcessorCount - 2) }, mood =>
        {
            var samples = Pantheon.Game.Audio.Orchestra.Orchestra.Render(score.Score(mood), score);
            played[mood] = samples;
            _pieces[mood] = WavFile.Pcm16(samples);
        });
        GD.Print(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Music ready: {played.Count} pieces for the orchestra made in {watch.Elapsed.TotalMilliseconds:0} ms"));
        if (_export is { } folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            foreach (var (mood, samples) in played)
            {
                var path = System.IO.Path.Combine(folder, $"{(int)mood + 1}-{mood}.wav");
                System.IO.File.WriteAllBytes(path, WavFile.Write(samples, score.SampleRate, 2));
                GD.Print($"Music written: {path} ({score.Score(mood).Name})");
            }
        }
    }

    /// <summary>Turns the pieces made so far into streams, on the main thread; quits once the music is written out, when asked.</summary>
    private void TakePieces()
    {
        if (_score is not { } score)
        {
            return;
        }

        foreach (var (mood, data) in _pieces)
        {
            if (!_music.ContainsKey(mood))
            {
                _music[mood] = new AudioStreamWav
                {
                    Format = AudioStreamWav.FormatEnum.Format16Bits,
                    MixRate = score.SampleRate,
                    Stereo = true,
                    Data = data,
                    LoopMode = AudioStreamWav.LoopModeEnum.Forward,
                    LoopBegin = 0,
                    LoopEnd = data.Length / 4,
                };
            }
        }

        if (_export is not null && _composing is { IsCompleted: true })
        {
            _export = null;
            GetTree().Quit();
        }
    }

    /// <summary>Turns the made samples into streams and starts the loops.</summary>
    private void Take((Dictionary<SoundKind, float[]> Sounds, double Milliseconds) made)
    {
        GD.Print(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Audio ready: {made.Sounds.Count} sounds made in {made.Milliseconds:0} ms"));
        foreach (var (kind, samples) in made.Sounds)
        {
            _sounds[kind] = Stream(samples, stereo: false, loop: SoundBank.Loops(kind));
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

    /// <summary>
    /// Fades the music towards the game's mood: the Theme on the title screen; a more urgent mood at once
    /// and quickly; a calmer one only after the last has held a while, so the music does not flicker.
    /// </summary>
    private void UpdateMusic(float delta)
    {
        var (wanted, why) = _forced is { } forced ? (forced, "chosen at launch") : _title() ? (MusicMood.Theme, "the title screen") : AudioDirector.Explain(_session.World, _session.LocalGodId);
        var urgent = _mood is not { } now || Urgency(wanted) > Urgency(now) || now == MusicMood.Theme || wanted == MusicMood.Theme;
        if (wanted != _mood && (urgent || _clock - _moodSince >= MoodSeconds) && _music.TryGetValue(wanted, out var stream))
        {
            (_musicNow, _musicNext) = (_musicNext, _musicNow);
            _musicNow.Stream = stream;
            _musicNow.VolumeDb = -60f;
            _musicNow.Play();
            GD.Print($"Music: {wanted} ({why})");
            _fadeSeconds = urgent && _mood is not null ? UrgentFadeSeconds : FadeSeconds;
            _mood = wanted;
            _moodSince = _clock;
            _fade = 0f;
        }

        if (_fade < 1f)
        {
            _fade = Math.Min(1f, _fade + (delta / _fadeSeconds));
            _musicNow.VolumeDb = Mathf.LinearToDb(Math.Max(0.001f, _fade));
            _musicNext.VolumeDb = Mathf.LinearToDb(Math.Max(0.001f, 1f - _fade));
            if (_fade >= 1f)
            {
                _musicNext.Stop();
            }
        }
    }

    /// <summary>How urgent a mood is: a disaster before war and triumph, before tension, before peace.</summary>
    private static int Urgency(MusicMood mood) => mood switch
    {
        MusicMood.Theme or MusicMood.Calm or MusicMood.Dark => 0,
        MusicMood.Tension => 1,
        MusicMood.War or MusicMood.Triumph => 2,
        MusicMood.Disaster => 3,
    };

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
