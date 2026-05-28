using System.Numerics;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Atmos;

public class SpatialAudioSourceProvider(
    SpatialAudioSourceParams parameters
) : ISampleProvider
{
    private readonly ISampleProvider _sourceProvider = GetMonoSourceProvider(parameters.SourceProvider);

    public Vector3 Position { get; set; } = parameters.InitialPosition;

    private float _desiredLeftVolume = 1;
    private float _desiredRightVolume = 1;

    private float _currentLeftVolume = 1.0f;
    private float _currentRightVolume = 1.0f;

    public WaveFormat WaveFormat => parameters.WaveFormat;

    public void CalculateSpatialAudio(Vector3 listenerPos, Vector3 listenerForward)
    {
        var distance = Vector3.Distance(Position, listenerPos);
        var distanceFactor = 1.0f;

        if (distance > parameters.MinDistanceForMaxVolume)
        {
            distanceFactor = parameters.DistanceToVolumeInterpolation(distance, parameters.MinDistanceForMaxVolume);
        }

        if (distance > parameters.MaxDistanceForMinVolume)
        {
            distanceFactor = parameters.MinVolume;
        }

        var directionToSource = Vector3.Normalize(Position - listenerPos);
        var listenerRight = Vector3.Normalize(Vector3.Cross(Directions.Up, listenerForward));
        var listenerUp = Directions.Up;

        // Calculate left/right factor, -1 means fully left, +1 means fully right
        var horizontalFactor = Vector3.Dot(directionToSource, listenerRight);

        var leftFactor = Math.Min(1.0f - horizontalFactor, 1.0f);
        var rightFactor = Math.Min(1.0f + horizontalFactor, 1.0f);

        // Calculate front/back factor, +1 means directly in front, -1 means directly behind
        var depthFactor = Vector3.Dot(directionToSource, listenerForward);

        if (depthFactor < 0)
        {
            // Sound is behind, lower the volume if perfectly behind (horizontal-wise)
            depthFactor = parameters.DepthVolumeMuffleFactor +
                          (1 - parameters.DepthVolumeMuffleFactor) *
                          (1.0f + depthFactor);
        }
        else
        {
            depthFactor = 1f;
        }

        // Calculate above/below factor, +1 means directly above, -1 means directly below
        var verticalFactor = Vector3.Dot(directionToSource, listenerUp);

        if (verticalFactor < 0)
        {
            // Sound is below
            verticalFactor = parameters.BelowVolumeMuffleFactor;
        }
        else if (verticalFactor > parameters.AboveHighVolumeFactorThreshold)
        {
            // Sound is high above
            verticalFactor = parameters.AboveVolumeMuffleFactor;
        }
        else
        {
            verticalFactor = 1f;
        }

        // Calculate the actual desired volume
        var calculatedLeftVolume = leftFactor * distanceFactor * depthFactor * verticalFactor;
        var calculatedRightVolume = rightFactor * distanceFactor * depthFactor * verticalFactor;

        _desiredLeftVolume = ClampVolume(calculatedLeftVolume);
        _desiredRightVolume = ClampVolume(calculatedRightVolume);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var monoCount = count / parameters.WaveFormat.Channels;
        var monoBuffer = new float[monoCount];

        var samplesRead = _sourceProvider.Read(monoBuffer, 0, monoCount);

        if (samplesRead == 0)
        {
            return 0;
        }

        var index = offset;

        for (var i = 0; i < samplesRead; i++)
        {
            _currentLeftVolume += GetInterpolatedVolume(_desiredLeftVolume, _currentLeftVolume);
            _currentRightVolume += GetInterpolatedVolume(_desiredRightVolume, _currentRightVolume);

            buffer[index++] = monoBuffer[i] * _currentLeftVolume;
            buffer[index++] = monoBuffer[i] * _currentRightVolume;
        }

        return samplesRead * parameters.WaveFormat.Channels;
    }

    private float ClampVolume(float volume)
    {
        return Math.Clamp(volume, parameters.MinVolume, parameters.MaxVolume);
    }

    private float GetInterpolatedVolume(float desiredVolume, float currentVolume)
    {
        return (desiredVolume - currentVolume) * parameters.CurrentToDesiredVolumeInterpolationMultiplier;
    }

    private static ISampleProvider GetMonoSourceProvider(ISampleProvider sourceProvider)
    {
        return sourceProvider.WaveFormat.Channels == 1
            ? sourceProvider
            : new StereoToMonoSampleProvider(sourceProvider);
    }
}

public record SpatialAudioSourceParams
{
    public required ISampleProvider SourceProvider { get; set; }
    public required WaveFormat WaveFormat { get; set; }
    public required Vector3 InitialPosition { get; set; } = Directions.Forward;

    public float MinDistanceForMaxVolume { get; set; } = 1f;
    public float MaxDistanceForMinVolume { get; set; } = 10f;

    public Func<float, float, float> DistanceToVolumeInterpolation { get; set; } = (d, minD) =>
        minD / (minD + 1.0f * (d - minD));

    public float MinVolume { get; set; } = 0.025f;
    public float MaxVolume { get; set; } = 1f;

    public float DepthVolumeMuffleFactor { get; set; } = 0.6f;
    public float BelowVolumeMuffleFactor { get; set; } = 0.85f;
    public float AboveVolumeMuffleFactor { get; set; } = 0.95f;

    public float AboveHighVolumeFactorThreshold { get; set; } = 0.5f;

    public float CurrentToDesiredVolumeInterpolationMultiplier { get; set; } = 0.001f;
}