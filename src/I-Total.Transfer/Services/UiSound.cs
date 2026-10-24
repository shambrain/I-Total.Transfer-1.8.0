using System.Media;
using System.Text;

namespace ITotalTransfer.Services;

/// <summary>
/// The app's audible feedback. The tones are synthesised in memory — no WAV assets ship with the app —
/// and they are deliberately short, quiet and low-pass-ish so a click never becomes a nuisance during a
/// long transfer. Every sound respects <see cref="Enabled"/>, which is the "Interface sounds" setting.
/// </summary>
public static class UiSound
{
    private const int SampleRate = 44100;

    private static readonly Lazy<SoundPlayer> ClickPlayer = new(() => Build([1620d], 0.028, 0.30, decay: 26));
    private static readonly Lazy<SoundPlayer> TogglePlayer = new(() => Build([1180d], 0.030, 0.24, decay: 24));
    private static readonly Lazy<SoundPlayer> SuccessPlayer = new(() => Build([784d, 1175d], 0.20, 0.26, decay: 6));
    private static readonly Lazy<SoundPlayer> NotifyPlayer = new(() => Build([988d, 988d], 0.13, 0.22, decay: 9, gap: true));
    private static readonly Lazy<SoundPlayer> ErrorPlayer = new(() => Build([392d, 196d], 0.24, 0.30, decay: 5));

    /// <summary>Whether interface sounds are played. Mirrors the persisted setting.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>A soft tick for a button press.</summary>
    public static void Click() => Play(ClickPlayer);

    /// <summary>A slightly lower tick for a toggle.</summary>
    public static void Toggle() => Play(TogglePlayer);

    /// <summary>A rising two-note chime for a completed action.</summary>
    public static void Success() => Play(SuccessPlayer);

    /// <summary>A short attention blip for a warning or an unexpected state.</summary>
    public static void Notify() => Play(NotifyPlayer);

    /// <summary>A falling two-note tone for a failure.</summary>
    public static void Error() => Play(ErrorPlayer);

    /// <summary>
    /// Pre-loads the players off the UI thread. <see cref="SoundPlayer"/> loads its stream on first use,
    /// which would otherwise stall the first click the user makes.
    /// </summary>
    public static void WarmUp() => Task.Run(() =>
    {
        try
        {
            _ = ClickPlayer.Value; _ = TogglePlayer.Value; _ = SuccessPlayer.Value;
            _ = NotifyPlayer.Value; _ = ErrorPlayer.Value;
        }
        catch { /* audio is a nicety; a machine without a sound device must not fail. */ }
    });

    private static void Play(Lazy<SoundPlayer> player)
    {
        if (!Enabled) return;
        try { player.Value.Play(); }
        catch { /* a sound device that disappears mid-session must not surface as an error. */ }
    }

    /// <summary>
    /// Synthesises a 16-bit mono PCM tone. <paramref name="frequencies"/> is glided from first to last;
    /// <paramref name="gap"/> inserts silence between two repeated notes so a double blip reads as two
    /// events rather than one long tone.
    /// </summary>
    private static SoundPlayer Build(double[] frequencies, double durationSeconds, double amplitude, double decay, bool gap = false)
    {
        int total = (int)(SampleRate * (gap ? durationSeconds * 2 : durationSeconds));
        var pcm = new byte[total * 2];
        int noteLength = total / (gap ? 2 : 1);

        for (int note = 0; note < (gap ? 2 : 1); note++)
        {
            double phase = 0;
            for (int i = 0; i < noteLength; i++)
            {
                double progress = i / (double)noteLength;
                double frequency = frequencies.Length == 1
                    ? frequencies[0]
                    : frequencies[0] + (frequencies[^1] - frequencies[0]) * progress;
                phase += 2 * Math.PI * frequency / SampleRate;
                // A very short attack removes the click of a hard start; the exponential tail keeps it dry.
                double attack = progress < 0.03 ? progress / 0.03 : 1;
                double envelope = attack * Math.Exp(-decay * progress);
                short sample = (short)(Math.Clamp(Math.Sin(phase) * envelope * amplitude, -1, 1) * short.MaxValue);
                int index = (note * noteLength + i) * 2;
                pcm[index] = (byte)(sample & 0xFF);
                pcm[index + 1] = (byte)((sample >> 8) & 0xFF);
            }
        }

        return new SoundPlayer(new MemoryStream(ToWav(pcm, SampleRate)));
    }

    private static byte[] ToWav(byte[] pcm, int sampleRate)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + pcm.Length);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);                       // PCM chunk size
        writer.Write((short)1);                 // PCM
        writer.Write((short)1);                 // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);           // byte rate
        writer.Write((short)2);                 // block align
        writer.Write((short)16);                // bits per sample
        writer.Write("data"u8.ToArray());
        writer.Write(pcm.Length);
        writer.Write(pcm);
        writer.Flush();
        return stream.ToArray();
    }
}
