using System.Numerics;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Atmos;

public class SpatialSoundEngine : IDisposable
{
    private readonly MixingSampleProvider _mixer;
    private readonly IWavePlayer _outputDevice;
    private readonly Dictionary<string, SpatialAudioSourceProvider> _sources = new();

    public Vector3 ListenerPosition { get; set; } = Vector3.Zero;
    public Vector3 ListenerForward { get; set; } = Directions.Forward;

    public SpatialSoundEngine(int sampleRate = 44100, int channels = 2)
    {
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels))
        {
            ReadFully = true
        };

        _outputDevice = new WaveOutEvent();
        _outputDevice.Init(_mixer);
        _outputDevice.Play();
    }

    public void RegisterSource(string id, string filePath, Vector3 initialPosition)
    {
        var audioReader = new AudioFileReader(filePath);

        var spatialSource = new SpatialAudioSourceProvider(
            new SpatialAudioSourceParams
            {
                SourceProvider = audioReader,
                InitialPosition = initialPosition,
                WaveFormat = audioReader.WaveFormat,
            }
        );

        _sources[id] = spatialSource;

        _mixer.AddMixerInput(spatialSource);
    }

    public void UpdateSourcePosition(string id, Vector3 position)
    {
        if (_sources.TryGetValue(id, out var source))
        {
            source.SetPosition(position);
        }
    }

    public void UpdateSpatialCalculations()
    {
        foreach (var source in _sources.Values)
        {
            source.UpdateSpatialAudio(ListenerPosition, ListenerForward);
        }

        Console.WriteLine();
    }

    public void Dispose()
    {
        _outputDevice.Stop();
        _outputDevice.Dispose();
    }
}