using System;

namespace Pantheon.Game.Audio.Orchestra;

/// <summary>The mix the instruments play into: the dry sound and what is sent to the hall, both sides, looping round.</summary>
internal sealed class Buses(int frames, int rate)
{
    public float[] DryLeft { get; } = new float[frames];

    public float[] DryRight { get; } = new float[frames];

    public float[] SendLeft { get; } = new float[frames];

    public float[] SendRight { get; } = new float[frames];

    public int Frames => frames;

    public int Rate => rate;

    /// <summary>Adds a sample to both sides at a frame, wrapping round the loop, with its share sent to the hall.</summary>
    public void Add(int frame, float left, float right, float send)
    {
        var at = frame % frames;
        DryLeft[at] += left;
        DryRight[at] += right;
        SendLeft[at] += left * send;
        SendRight[at] += right * send;
    }
}

/// <summary>
/// The bowed and blown instruments (ADR-071). Each is an ensemble of band-limited sawtooths, a little
/// out of tune with each other and spread across the stage, with a vibrato that comes in after the
/// attack, shaped by filters whose brightness follows how hard the note is played: so strings swell
/// warmly, brass blares as it opens, a choir sings through its vowels and woodwinds breathe.
/// </summary>
internal static class Voices
{
    /// <summary>Plays a note into the mix.</summary>
    public static void Play(NoteEvent note, Buses buses, uint seed)
    {
        switch (note.Instrument)
        {
            case Instrument.Strings or Instrument.Tremolo or Instrument.LowStrings or Instrument.Spiccato:
                Bowed(note, buses, new Noise(seed));
                break;
            case Instrument.Horns or Instrument.Brass:
                Brass(note, buses, new Noise(seed));
                break;
            case Instrument.Choir:
                Choir(note, buses, new Noise(seed));
                break;
            case Instrument.Flute or Instrument.Oboe:
                Reed(note, buses, new Noise(seed));
                break;
            case Instrument.Harp or Instrument.Celesta or Instrument.Timpani or Instrument.Taiko or Instrument.Cymbal:
                Struck.Play(note, buses, new Noise(seed));
                break;
        }
    }

    /// <summary>A note's frequency.</summary>
    public static double Hertz(float midi) => 440.0 * Math.Pow(2.0, (midi - 69.0) / 12.0);

    /// <summary>Constant-power gains for a place on the stage.</summary>
    public static (float Left, float Right) Gains(float pan)
    {
        var angle = (Math.Clamp(pan, -1f, 1f) + 1f) * MathF.PI / 4f;
        return (MathF.Cos(angle), MathF.Sin(angle));
    }

    /// <summary>How much of each instrument the hall hears.</summary>
    public static float Send(Instrument instrument) => instrument switch
    {
        Instrument.Strings or Instrument.Tremolo => 0.36f,
        Instrument.LowStrings => 0.26f,
        Instrument.Spiccato => 0.24f,
        Instrument.Horns => 0.42f,
        Instrument.Brass => 0.3f,
        Instrument.Choir => 0.55f,
        Instrument.Flute or Instrument.Oboe => 0.4f,
        Instrument.Harp => 0.45f,
        Instrument.Celesta => 0.5f,
        Instrument.Timpani => 0.32f,
        Instrument.Taiko => 0.25f,
        Instrument.Cymbal => 0.35f,
    };

    /// <summary>A rising ease (0..1) over the attack: slow out of silence, smooth into the sustain.</summary>
    public static float Ease(double t, double attack) => t >= attack ? 1f : (float)(0.5 - (0.5 * Math.Cos(Math.PI * t / attack)));

    /// <summary>The bowed strings: a section of detuned sawtooths, filtered warm, swelling in and dying away; spiccato short, tremolo shivering.</summary>
    private static void Bowed(NoteEvent note, Buses buses, Noise noise)
    {
        var (voices, spread, vibrato, attack, release, gain, brightness, width) = note.Instrument switch
        {
            Instrument.Strings => (5, 0.13, 0.0028, 0.26, 0.55, 0.12f, (Base: 2600f, Velocity: 3600f, Pitch: 7f), 0.45f),
            Instrument.Tremolo => (5, 0.11, 0.0015, 0.07, 0.3, 0.12f, (Base: 2800f, Velocity: 3600f, Pitch: 7f), 0.5f),
            Instrument.LowStrings => (4, 0.09, 0.002, 0.22, 0.6, 0.12f, (Base: 900f, Velocity: 1800f, Pitch: 6f), 0.3f),
            _ => (3, 0.07, 0.0, 0.004, 0.06, 0.18f, (Base: 2400f, Velocity: 3800f, Pitch: 8f), 0.35f),
        };
        var rate = buses.Rate;
        var f0 = Hertz(note.Midi);
        var velocity = Math.Clamp(note.Velocity, 0f, 1.5f);
        var spiccato = note.Instrument == Instrument.Spiccato;
        var held = spiccato ? Math.Min(note.Length, 0.32) : note.Length;
        var attackTime = attack * (1.2 - (0.4 * Math.Min(1f, velocity)));
        var frames = (int)((held + release) * rate);
        var start = (int)(note.Start * rate);
        var cutoff = (float)((f0 * brightness.Pitch) + brightness.Base + (brightness.Velocity * Math.Min(1f, velocity)));
        var filters = new[] { new Svf(), new Svf(), new Svf(), new Svf() };
        filters[0].Tune(cutoff, 0.75f, rate);
        filters[1].Tune(cutoff * 2.2f, 0.6f, rate);
        filters[2].Tune(cutoff, 0.75f, rate);
        filters[3].Tune(cutoff * 2.2f, 0.6f, rate);
        var phases = new double[voices];
        var ratios = new double[voices];
        var rates = new double[voices];
        var vibPhases = new double[voices];
        var pans = new (float Left, float Right)[voices];
        for (var v = 0; v < voices; v++)
        {
            var across = voices == 1 ? 0.0 : ((2.0 * v / (voices - 1)) - 1.0);
            ratios[v] = Math.Pow(2.0, ((spread * across) + (0.03 * noise.Next())) / 12.0);
            phases[v] = 0.5 + (0.5 * noise.Next());
            rates[v] = 5.3 + (0.5 * noise.Next());
            vibPhases[v] = noise.Next();
            pans[v] = Gains(note.Pan + (width * (float)across));
        }

        var level = gain * velocity / MathF.Sqrt(voices);
        var send = Send(note.Instrument);
        for (var n = 0; n < frames; n++)
        {
            var t = n / (double)rate;
            var amp = spiccato
                ? (float)((t < 0.004 ? t / 0.004 : Math.Exp(-(t - 0.004) / 0.13)) * (t > held ? Math.Exp(-(t - held) / 0.02) : 1.0))
                : Ease(t, attackTime) * (t > held ? (float)Math.Exp(-(t - held) * 5.0 / release) : 1f);
            if (note.Instrument == Instrument.Tremolo)
            {
                amp *= 1f - (0.5f * (0.5f + (0.5f * Synth.Sine(11.5 * t))));
            }

            var depth = vibrato * Math.Clamp((t - 0.28) / 0.45, 0.0, 1.0);
            var (left, right) = (0f, 0f);
            for (var v = 0; v < voices; v++)
            {
                var step = f0 * ratios[v] * (1.0 + (depth * Synth.Sine((rates[v] * t) + vibPhases[v]))) / rate;
                phases[v] += step;
                phases[v] -= Math.Floor(phases[v]);
                var s = Blep.Saw(phases[v], step);
                left += s * pans[v].Left;
                right += s * pans[v].Right;
            }

            left = filters[1].Low(filters[0].Low(left)) * amp * level;
            right = filters[3].Low(filters[2].Low(right)) * amp * level;
            buses.Add(start + n, left, right, send);
        }
    }

    /// <summary>Horns and brass: detuned sawtooths that scoop up to pitch and open bright as they are blown, the brass fiercer.</summary>
    private static void Brass(NoteEvent note, Buses buses, Noise noise)
    {
        var horns = note.Instrument == Instrument.Horns;
        var voices = horns ? 4 : 3;
        var rate = buses.Rate;
        var f0 = Hertz(note.Midi);
        var velocity = Math.Clamp(note.Velocity, 0f, 1.5f);
        var (attack, release, scoop, open, opening, q) = horns ? (0.09, 0.35, -0.15, 5.5, 0.075, 0.8f) : (0.045, 0.2, -0.3, 12.0, 0.04, 1.2f);
        var frames = (int)((note.Length + release) * rate);
        var start = (int)(note.Start * rate);
        var (low, lower) = (new Svf(), new Svf());
        var phases = new double[voices];
        var ratios = new double[voices];
        for (var v = 0; v < voices; v++)
        {
            ratios[v] = Math.Pow(2.0, ((horns ? 0.06 : 0.05) * ((2.0 * v / (voices - 1)) - 1.0) / 12.0) + (0.002 * noise.Next()));
            phases[v] = 0.5 + (0.5 * noise.Next());
        }

        var (gainLeft, gainRight) = Gains(note.Pan);
        var level = (horns ? 0.17f : 0.15f) * velocity / MathF.Sqrt(voices);
        var send = Send(note.Instrument);
        var vibratoRate = 5.0 + noise.Next();
        for (var n = 0; n < frames; n++)
        {
            var t = n / (double)rate;
            if ((n & 31) == 0)
            {
                var blown = Math.Min(1.0, velocity) * (1.0 - Math.Exp(-t / opening)) * (0.75 + (0.25 * Math.Exp(-t / 0.4)));
                if (t > note.Length)
                {
                    blown *= Math.Exp(-(t - note.Length) / 0.15);
                }

                var cutoff = (float)((f0 * (1.3 + (open * blown))) + 150.0);
                low.Tune(cutoff, q, rate);
                lower.Tune(cutoff * 2.5f, 0.6f, rate);
            }

            var bend = Math.Pow(2.0, scoop * Math.Exp(-t / 0.035) / 12.0) * (1.0 + (0.0018 * Math.Clamp((t - 0.35) / 0.4, 0.0, 1.0) * Synth.Sine(vibratoRate * t)));
            var sum = 0f;
            for (var v = 0; v < voices; v++)
            {
                var step = f0 * ratios[v] * bend / rate;
                phases[v] += step;
                phases[v] -= Math.Floor(phases[v]);
                sum += Blep.Saw(phases[v], step);
            }

            var swell = horns ? 0.85f + (0.15f * (float)Math.Min(1.0, t / 0.6)) : 1f;
            var amp = Ease(t, attack) * swell * (t > note.Length ? (float)Math.Exp(-(t - note.Length) * 5.0 / release) : 1f);
            var s = lower.Low(low.Low(sum)) * amp * level;
            buses.Add(start + n, s * gainLeft, s * gainRight, send);
        }
    }

    /// <summary>A choir: detuned sawtooths sung through the formants of "ah", or of "oo" for low voices, with a slow vibrato and breath.</summary>
    private static void Choir(NoteEvent note, Buses buses, Noise noise)
    {
        const int Voices = 4;
        var rate = buses.Rate;
        var f0 = Hertz(note.Midi);
        var velocity = Math.Clamp(note.Velocity, 0f, 1.5f);
        var high = note.Midi >= 72 ? 1.1f : 1f;
        (float Frequency, float Gain, float Q)[] formants = note.Midi < 55
            ? [(330f, 1f, 5f), (860f, 0.5f, 7f), (2350f, 0.16f, 12f)]
            : [(760f * high, 1f, 6f), (1180f * high, 0.6f, 8f), (2850f * high, 0.28f, 14f)];
        var bands = new Svf[2 * formants.Length];
        for (var k = 0; k < bands.Length; k++)
        {
            bands[k] = new Svf();
            var formant = formants[k % formants.Length];
            bands[k].Tune(formant.Frequency, formant.Q, rate);
        }

        var phases = new double[Voices];
        var ratios = new double[Voices];
        var rates = new double[Voices];
        var pans = new (float Left, float Right)[Voices];
        for (var v = 0; v < Voices; v++)
        {
            var across = (2.0 * v / (Voices - 1)) - 1.0;
            ratios[v] = Math.Pow(2.0, ((0.14 * across) + (0.03 * noise.Next())) / 12.0);
            phases[v] = 0.5 + (0.5 * noise.Next());
            rates[v] = 5.0 + (0.6 * noise.Next());
            pans[v] = Gains(note.Pan + (0.4f * (float)across));
        }

        var attack = 0.45 * (1.2 - (0.4 * Math.Min(1f, velocity)));
        const double Release = 0.8;
        var frames = (int)((note.Length + Release) * rate);
        var start = (int)(note.Start * rate);
        var level = 0.55f * velocity / MathF.Sqrt(Voices);
        var send = Send(Instrument.Choir);
        for (var n = 0; n < frames; n++)
        {
            var t = n / (double)rate;
            var depth = 0.004 * Math.Clamp((t - 0.3) / 0.5, 0.0, 1.0);
            var (left, right) = (0f, 0f);
            for (var v = 0; v < Voices; v++)
            {
                var step = f0 * ratios[v] * (1.0 + (depth * Synth.Sine(rates[v] * t))) / rate;
                phases[v] += step;
                phases[v] -= Math.Floor(phases[v]);
                var s = Blep.Saw(phases[v], step);
                left += s * pans[v].Left;
                right += s * pans[v].Right;
            }

            var breath = 0.04f * noise.Next();
            var (sungLeft, sungRight) = (0f, 0f);
            for (var k = 0; k < formants.Length; k++)
            {
                bands[k].Low(left + breath);
                bands[k + formants.Length].Low(right + breath);
                sungLeft += bands[k].Band * formants[k].Gain;
                sungRight += bands[k + formants.Length].Band * formants[k].Gain;
            }

            var amp = Ease(t, attack) * (t > note.Length ? (float)Math.Exp(-(t - note.Length) * 5.0 / Release) : 1f) * level;
            buses.Add(start + n, sungLeft * amp, sungRight * amp, send);
        }
    }

    /// <summary>The woodwinds: a flute's breathy near-sine with a chiff, or an oboe's nasal narrow pulse.</summary>
    private static void Reed(NoteEvent note, Buses buses, Noise noise)
    {
        var flute = note.Instrument == Instrument.Flute;
        var rate = buses.Rate;
        var f0 = Hertz(note.Midi);
        var velocity = Math.Clamp(note.Velocity, 0f, 1.5f);
        var (attack, release, vibrato, vibratoRate) = flute ? (0.08, 0.18, 0.0035, 5.1) : (0.05, 0.15, 0.003, 5.6);
        var frames = (int)((note.Length + release) * rate);
        var start = (int)(note.Start * rate);
        var (body, nasal, airy) = (new Svf(), new Svf(), new Svf());
        body.Tune(flute ? 4000f : 3500f, 0.7f, rate);
        nasal.Tune(1300f, 2f, rate);
        airy.Tune(2600f, 0.7f, rate);
        var phase = 0.0;
        var (gainLeft, gainRight) = Gains(note.Pan);
        var level = (flute ? 0.3f : 0.2f) * velocity;
        var send = Send(note.Instrument);
        for (var n = 0; n < frames; n++)
        {
            var t = n / (double)rate;
            var step = f0 * (1.0 + (vibrato * Math.Clamp((t - 0.25) / 0.4, 0.0, 1.0) * Synth.Sine(vibratoRate * t))) / rate;
            phase += step;
            phase -= Math.Floor(phase);
            float s;
            if (flute)
            {
                var air = airy.Low(noise.Next());
                s = Synth.Sine(phase) + (0.25f * Synth.Sine(2.0 * phase)) + (0.07f * Synth.Sine(3.0 * phase)) + (0.05f * air) + (0.3f * air * (float)Math.Exp(-t / 0.025));
            }
            else
            {
                var pulse = Blep.Saw(phase, step) - Blep.Saw((phase + 0.3) % 1.0, step);
                var lowPassed = body.Low(pulse);
                nasal.Low(pulse);
                s = (0.5f * lowPassed) + (0.7f * nasal.Band);
            }

            var amp = Ease(t, attack) * (t > note.Length ? (float)Math.Exp(-(t - note.Length) * 5.0 / release) : 1f) * level;
            buses.Add(start + n, s * amp * gainLeft, s * amp * gainRight, send);
        }
    }
}
