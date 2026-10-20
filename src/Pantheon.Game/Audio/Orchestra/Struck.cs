using System;

namespace Pantheon.Game.Audio.Orchestra;

/// <summary>
/// The plucked and struck instruments (ADR-071), each a few decaying partials and a strike's noise: a
/// harp's ringing string, a celesta's bell, a timpano's tuned skin (its higher partials brighter as it
/// is struck harder, its pitch settling after the blow), a great drum's thud, and a cymbal's metal,
/// crashed or swelled into a downbeat. A roll is strokes in quick succession.
/// </summary>
internal static class Struck
{
    /// <summary>Plays a note into the mix.</summary>
    public static void Play(NoteEvent note, Buses buses, Noise noise)
    {
        switch (note.Instrument)
        {
            case Instrument.Harp:
                Harp(note, buses, noise);
                break;
            case Instrument.Celesta:
                Bell(note, buses, noise);
                break;
            case Instrument.Cymbal:
                Cymbal(note, buses, noise);
                break;
            case Instrument.Timpani or Instrument.Taiko when note.Shape == NoteShape.Plain:
                Drum(note, note.Start, note.Velocity, buses, noise);
                break;
            case Instrument.Timpani or Instrument.Taiko:
                Roll(note, buses, noise);
                break;
            case Instrument.Strings or Instrument.Tremolo or Instrument.LowStrings or Instrument.Spiccato or Instrument.Horns or Instrument.Brass or Instrument.Choir or Instrument.Flute or Instrument.Oboe:
                break;
        }
    }

    /// <summary>A harp: six partials of a plucked string, the higher dying sooner, and the pluck's click.</summary>
    private static void Harp(NoteEvent note, Buses buses, Noise noise)
    {
        var rate = buses.Rate;
        var f0 = Voices.Hertz(note.Midi);
        var ring = Math.Clamp(2.4 * Math.Pow(196.0 / f0, 0.4), 0.5, 4.5);
        var frames = (int)(Math.Min(5.0 * ring, 6.0) * rate);
        var start = (int)(note.Start * rate);
        var (left, right) = Voices.Gains(note.Pan);
        var level = 0.3f * Math.Clamp(note.Velocity, 0f, 1.5f);
        var send = Voices.Send(Instrument.Harp);
        var click = new Svf();
        click.Tune(2500f, 0.7f, rate);
        for (var n = 0; n < frames; n++)
        {
            var t = n / (double)rate;
            var s = 0f;
            for (var k = 1; k <= 6; k++)
            {
                var weight = (k == 2 ? 0.8 : 1.0) / Math.Pow(k, 1.4);
                s += (float)(weight * Math.Exp(-t * (1.0 + (0.9 * (k - 1))) / ring)) * Synth.Sine(f0 * k * t);
            }

            s += t < 0.008 ? 0.25f * click.Low(noise.Next()) * (float)(1.0 - (t / 0.008)) : 0f;
            var amp = Voices.Ease(t, 0.002) * level;
            buses.Add(start + n, s * amp * left, s * amp * right, send);
        }
    }

    /// <summary>A celesta: a bell's fundamental with a quick fourth partial, and the hammer's tick.</summary>
    private static void Bell(NoteEvent note, Buses buses, Noise noise)
    {
        var rate = buses.Rate;
        var f0 = Voices.Hertz(note.Midi);
        var frames = (int)(2.4 * rate);
        var start = (int)(note.Start * rate);
        var (left, right) = Voices.Gains(note.Pan);
        var level = 0.24f * Math.Clamp(note.Velocity, 0f, 1.5f);
        var send = Voices.Send(Instrument.Celesta);
        for (var n = 0; n < frames; n++)
        {
            var t = n / (double)rate;
            var s = (float)((Math.Exp(-t / 0.8) * Synth.Sine(f0 * t)) + (0.3 * Math.Exp(-t / 0.18) * Synth.Sine(f0 * 4.0 * t)) + (0.1 * Math.Exp(-t / 0.3) * Synth.Sine(f0 * 3.0 * t)));
            s += t < 0.003 ? 0.15f * noise.Next() : 0f;
            var amp = Voices.Ease(t, 0.001) * level;
            buses.Add(start + n, s * amp * left, s * amp * right, send);
        }
    }

    /// <summary>One stroke of a timpano or the great drum.</summary>
    private static void Drum(NoteEvent note, double at, float velocity, Buses buses, Noise noise)
    {
        var rate = buses.Rate;
        var taiko = note.Instrument == Instrument.Taiko;
        var f0 = taiko ? 58.0 : Voices.Hertz(note.Midi);
        var hard = Math.Clamp(velocity, 0f, 1.5f);
        (double Ratio, double Weight, double Decay)[] partials = taiko
            ? [(1.0, 1.0, 0.55), (1.52, 0.35, 0.22)]
            : [(1.0, 1.0, 1.4), (1.5, 0.45, 0.8), (1.98, 0.3, 0.6), (2.44, 0.16, 0.45), (2.9, 0.08, 0.3)];
        var frames = (int)((taiko ? 3.2 : 4.5) * rate);
        var start = (int)(at * rate);
        var (left, right) = Voices.Gains(note.Pan);
        var level = (taiko ? 0.32f : 0.2f) * hard;
        var send = Voices.Send(note.Instrument);
        var skin = new Svf();
        skin.Tune(taiko ? 350f : 900f, 0.7f, rate);
        var phases = new double[partials.Length];
        for (var n = 0; n < frames; n++)
        {
            var t = n / (double)rate;
            var glide = taiko ? 1.0 + (0.8 * Math.Exp(-t / 0.035)) : 1.0 + (0.025 * Math.Exp(-t / 0.06));
            var s = 0f;
            for (var k = 0; k < partials.Length; k++)
            {
                var (ratio, weight, decay) = partials[k];
                phases[k] += f0 * ratio * glide / rate;
                var bright = k == 0 ? 1.0 : 0.5 + (0.5 * Math.Min(1f, hard));
                s += (float)(weight * bright * Math.Exp(-t / decay)) * Synth.Sine(phases[k]);
            }

            s += (taiko ? 0.6f : 0.5f) * skin.Low(noise.Next()) * (float)Math.Exp(-t / (taiko ? 0.05 : 0.018));
            var amp = Voices.Ease(t, 0.0015) * level;
            buses.Add(start + n, s * amp * left, s * amp * right, send);
        }
    }

    /// <summary>A roll: strokes fifteen a second through the note, swelling into its end and struck there once more when it is a swell.</summary>
    private static void Roll(NoteEvent note, Buses buses, Noise noise)
    {
        var strokes = Math.Max(2, (int)(note.Length * 15.0));
        for (var i = 0; i < strokes; i++)
        {
            var share = i / (double)strokes;
            var swell = note.Shape == NoteShape.Swell ? 0.12 + (0.88 * share * share) : 1.0;
            var velocity = (float)(note.Velocity * (0.55 + (0.12 * noise.Next())) * swell);
            Drum(note, note.Start + (share * note.Length), velocity * 0.6f, buses, noise);
        }

        if (note.Shape == NoteShape.Swell)
        {
            Drum(note, note.Start + note.Length, note.Velocity, buses, noise);
        }
    }

    /// <summary>A suspended cymbal: bright noise and inharmonic metal, crashed; or swelling up to the end of the note and crashing there.</summary>
    private static void Cymbal(NoteEvent note, Buses buses, Noise noise)
    {
        var rate = buses.Rate;
        double[] metal = [3130.0, 4240.0, 5390.0, 6110.0, 7270.0, 8440.0];
        var swell = note.Shape == NoteShape.Swell ? note.Length : 0.0;
        var frames = (int)((swell + 3.5) * rate);
        var start = (int)(note.Start * rate);
        var (left, right) = Voices.Gains(note.Pan);
        var level = 0.22f * Math.Clamp(note.Velocity, 0f, 1.5f);
        var send = Voices.Send(Instrument.Cymbal);
        var shimmer = new Svf();
        shimmer.Tune(6500f, 0.8f, rate);
        for (var n = 0; n < frames; n++)
        {
            var t = n / (double)rate;
            var bright = 0f;
            foreach (var hertz in metal)
            {
                bright += Synth.Sine(hertz * t) > 0f ? 0.12f : -0.12f;
            }

            shimmer.Low((0.7f * noise.Next()) + bright);
            var s = shimmer.High + (0.3f * shimmer.Band);
            var envelope = t < swell
                ? (float)Math.Exp((t - swell) / 0.55)
                : (float)Math.Exp(-(t - swell) / 1.3) * Voices.Ease(t - swell, 0.002);
            var amp = envelope * level;
            buses.Add(start + n, s * amp * left, s * amp * right, send);
        }
    }
}
