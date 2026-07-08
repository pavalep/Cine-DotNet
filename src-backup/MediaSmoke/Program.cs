using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Cine.Media.Implementations;

static byte[] CreatePcm16MonoWav(int sampleRate, double seconds, double frequencyHz)
{
    int numSamples = (int)(sampleRate * seconds);
    short[] pcm = new short[numSamples];
    for (int i = 0; i < numSamples; i++)
    {
        double t = (double)i / sampleRate;
        double s = Math.Sin(2 * Math.PI * frequencyHz * t);
        pcm[i] = (short)(s * short.MaxValue * 0.2);
    }

    int subchunk2Size = numSamples * 2;
    int chunkSize = 36 + subchunk2Size;
    using var ms = new MemoryStream(44 + subchunk2Size);
    using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

    bw.Write(Encoding.ASCII.GetBytes("RIFF"));
    bw.Write(chunkSize);
    bw.Write(Encoding.ASCII.GetBytes("WAVE"));

    bw.Write(Encoding.ASCII.GetBytes("fmt "));
    bw.Write(16);
    bw.Write((short)1);
    bw.Write((short)1);
    bw.Write(sampleRate);
    bw.Write(sampleRate * 2);
    bw.Write((short)2);
    bw.Write((short)16);

    bw.Write(Encoding.ASCII.GetBytes("data"));
    bw.Write(subchunk2Size);
    for (int i = 0; i < numSamples; i++)
        bw.Write(pcm[i]);

    bw.Flush();
    return ms.ToArray();
}

string? path = args.Length > 0 ? args[0] : null;
string? tempWavPath = null;

if (string.Equals(path, "--d3d", StringComparison.OrdinalIgnoreCase))
{
    using var form = new Form
    {
        Text = "Cine D3D Smoke",
        Width = 800,
        Height = 600
    };
    form.Show();
    Application.DoEvents();

    using var r = new D3D11Renderer(form.Handle);
    r.SetVideoDimensions(1280, 720);
    r.UseNv12ShaderPath = true;
    r.Initialize();
    Thread.Sleep(500);
    return;
}

if (string.IsNullOrWhiteSpace(path))
{
    tempWavPath = Path.Combine(Path.GetTempPath(), $"cine_smoke_{Guid.NewGuid():N}.wav");
    File.WriteAllBytes(tempWavPath, CreatePcm16MonoWav(sampleRate: 48000, seconds: 1.0, frequencyHz: 440));
    path = tempWavPath;
}

try
{
    using var mf = new MfHelper();
    mf.Initialize();
    mf.OpenFile(path);

    if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
    {
        using var gotVideo = new ManualResetEventSlim(false);
        using var gotAudio = new ManualResetEventSlim(false);

        mf.SampleReady += (_, __) => gotVideo.Set();
        mf.AudioSampleReady += (_, __) => gotAudio.Set();

        mf.StartPlayback();

        bool ok = WaitHandle.WaitAny([gotVideo.WaitHandle, gotAudio.WaitHandle], millisecondsTimeout: 3000) != WaitHandle.WaitTimeout;
        mf.StopPlayback();

        if (!ok)
            throw new InvalidOperationException("Opened media, but no samples arrived within timeout.");
    }
}
finally
{
    if (tempWavPath is not null)
    {
        try { File.Delete(tempWavPath); } catch { }
    }
}
