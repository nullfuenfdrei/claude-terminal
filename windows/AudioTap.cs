using System.Globalization;
using System.Text;
using NAudio.Dsp;
using NAudio.Wave;

/// <summary>
/// Hört mit, was gerade auf dem PC abgespielt wird (WASAPI-Loopback),
/// und berechnet 64 logarithmische Frequenzbänder + 128 Wellenform-Punkte.
/// </summary>
sealed class AudioTap : IDisposable
{
    const int N = 2048, LOG2N = 11, BANDS = 64, WAVE = 128;
    readonly float[] buf = new float[N];
    readonly Complex[] fft = new Complex[N];
    readonly float[] window = new float[N];
    readonly float[] bands = new float[BANDS];
    readonly float[] wave = new float[WAVE];
    readonly object lk = new();
    int pos;
    bool fresh;
    WasapiLoopbackCapture? cap;
    int sampleRate = 48000;

    public AudioTap()
    {
        for (int i = 0; i < N; i++) window[i] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (N - 1))));
    }

    public void Start()
    {
        cap = new WasapiLoopbackCapture();
        var fmt = cap.WaveFormat;
        sampleRate = fmt.SampleRate;
        int ch = fmt.Channels, bytes = fmt.BitsPerSample / 8;
        bool isFloat = fmt.Encoding == WaveFormatEncoding.IeeeFloat ||
                       (fmt.Encoding == WaveFormatEncoding.Extensible && fmt.BitsPerSample == 32);

        cap.DataAvailable += (_, e) =>
        {
            int frame = ch * bytes;
            for (int off = 0; off + frame <= e.BytesRecorded; off += frame)
            {
                float s = 0;
                for (int c = 0; c < ch; c++)
                {
                    int o = off + c * bytes;
                    s += isFloat ? BitConverter.ToSingle(e.Buffer, o)
                       : bytes == 2 ? BitConverter.ToInt16(e.Buffer, o) / 32768f
                       : BitConverter.ToInt32(e.Buffer, o) / 2147483648f;
                }
                buf[pos++] = s / ch;
                if (pos == N)
                {
                    Analyze();
                    Array.Copy(buf, N / 2, buf, 0, N / 2); // 50 % Überlappung
                    pos = N / 2;
                }
            }
        };
        cap.StartRecording();
    }

    void Analyze()
    {
        for (int i = 0; i < N; i++) { fft[i].X = buf[i] * window[i]; fft[i].Y = 0; }
        FastFourierTransform.FFT(true, LOG2N, fft);

        var b = new float[BANDS];
        for (int i = 0; i < BANDS; i++)
        {
            double f0 = 30 * Math.Pow(16000 / 30.0, i / (double)BANDS);
            double f1 = 30 * Math.Pow(16000 / 30.0, (i + 1) / (double)BANDS);
            int k0 = (int)(f0 * N / sampleRate), k1 = Math.Max(k0 + 1, (int)Math.Ceiling(f1 * N / sampleRate));
            double m = 0;
            for (int k = k0; k < k1 && k < N / 2; k++)
                m = Math.Max(m, Math.Sqrt(fft[k].X * fft[k].X + fft[k].Y * fft[k].Y));
            double db = 20 * Math.Log10(m + 1e-9);
            b[i] = (float)Math.Clamp((db + 85) / 70, 0, 1);
        }
        lock (lk)
        {
            Array.Copy(b, bands, BANDS);
            for (int i = 0; i < WAVE; i++) wave[i] = buf[N / 2 + i * (N / 2) / WAVE];
            fresh = true;
        }
    }

    /// <summary>Liefert die neuesten Daten als JSON (oder null, wenn nichts Neues da ist).</summary>
    public string? TakeJson()
    {
        lock (lk)
        {
            if (!fresh) return null;
            fresh = false;
            var sb = new StringBuilder("{\"t\":\"audio\",\"b\":[", 1600);
            for (int i = 0; i < BANDS; i++) { if (i > 0) sb.Append(','); sb.Append(bands[i].ToString("0.###", CultureInfo.InvariantCulture)); }
            sb.Append("],\"w\":[");
            for (int i = 0; i < WAVE; i++) { if (i > 0) sb.Append(','); sb.Append(wave[i].ToString("0.###", CultureInfo.InvariantCulture)); }
            sb.Append("]}");
            return sb.ToString();
        }
    }

    public void Dispose()
    {
        try { cap?.StopRecording(); } catch { }
        cap?.Dispose();
    }
}
