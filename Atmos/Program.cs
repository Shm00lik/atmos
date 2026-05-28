using System.Numerics;
using Atmos;

using var engine = new SpatialSoundEngine();

engine.RegisterSource("music", "./bad-apple.wav", new Vector3(0, 0, 1));

Console.WriteLine("Starting to simulate...");

float angle = 0;

while (true)
{
    // 5 meters radius
    var x = MathF.Cos(angle) * 5.0f;
    var z = MathF.Sin(angle) * 5.0f;

    engine.UpdateSourcePosition("music", new Vector3(x, 0, z));

    Console.WriteLine("Angle: " + angle);

    engine.UpdateSpatialCalculations();

    angle += 0.05f;
    Thread.Sleep(50);
}