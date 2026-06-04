using System.Numerics;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Atmos;

public class SpatialSoundEngine : IDisposable
{
    private readonly MixingSampleProvider _mixer;
    private readonly IWavePlayer _outputDevice;
    private readonly Dictionary<string, SpatialAudioSourceProvider> _sources = new();

    private Vector3 _listenerPosition = Vector3.Zero;
    private Vector3 _listenerForward = Directions.Forward;

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

    public void RegisterSource(
        string id,
        string filePath,
        Vector3 initialPosition,
        SpatialAudioSourceParams? audioSourceParams = null
    )
    {
        var audioReader = new AudioFileReader(filePath);

        var newParams = audioSourceParams != null
            ? audioSourceParams with
            {
                SourceProvider = audioReader,
                InitialPosition = initialPosition,
                WaveFormat = audioReader.WaveFormat
            }
            : new SpatialAudioSourceParams
            {
                SourceProvider = audioReader,
                InitialPosition = initialPosition,
                WaveFormat = audioReader.WaveFormat,
            };

        var spatialSource = new SpatialAudioSourceProvider(newParams);

        _sources[id] = spatialSource;

        _mixer.AddMixerInput(spatialSource);
    }

    public void RegisterSource(string id, SpatialAudioSourceParams audioSourceParams)
    {
        var spatialSource = new SpatialAudioSourceProvider(audioSourceParams);

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
            source.UpdateSpatialAudio(_listenerPosition, _listenerForward);
        }
    }

    public void UpdateListenerPosition(Vector3 listenerPosition)
    {
        UpdateListenerPosition(listenerPosition, Directions.Forward);
    }

    public void UpdateListenerPosition(Vector3 listenerPosition, Vector3 listenerForward)
    {
        _listenerPosition = listenerPosition;
        _listenerForward = listenerForward;
    }

    public void Dispose()
    {
        _outputDevice.Stop();
        _outputDevice.Dispose();
    }
}