using System.Drawing;
using System.Numerics;
using Atmos;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;

using var engine = new SpatialSoundEngine();

engine.RegisterSource("music", "./bad-apple.wav", new Vector3(0, 0, 5));

Console.WriteLine("Starting to simulate...");

int cameraIndex = 0;

using VideoCapture capture = new VideoCapture(cameraIndex);

capture.Set(CapProp.FrameWidth, 1920);
capture.Set(CapProp.FrameHeight, 1080);

string cascadePath = "haarcascade_frontalface_default.xml";

if (!File.Exists(cascadePath))
{
    Console.WriteLine($"Please ensure '{cascadePath}' is downloaded and placed in your build directory.");
    return;
}

using CascadeClassifier faceDetector = new CascadeClassifier(cascadePath);

Console.WriteLine("Streaming webcam... Press any key to exit.");

string windowName = "Webcam Face Tracker";
CvInvoke.NamedWindow(windowName, WindowFlags.Normal);

using Mat frame = new Mat();

while (CvInvoke.WaitKey(1) < 0)
{
    capture.Read(frame);
    if (frame.IsEmpty) continue;

    using Mat grayFrame = new Mat();
    CvInvoke.CvtColor(frame, grayFrame, ColorConversion.Bgr2Gray);
    CvInvoke.EqualizeHist(grayFrame, grayFrame);

    Rectangle[] faces = faceDetector.DetectMultiScale(
        grayFrame,
        scaleFactor: 1.1,
        minNeighbors: 5,
        minSize: new Size(150, 150)
    );

    // 4. Extract only the LARGEST face using LINQ
    // We calculate Area = Width * Height and take the biggest one.
    Rectangle largestFace = faces
        .OrderByDescending(f => f.Width * f.Height)
        .FirstOrDefault();

    // If a face was found (largestFace won't be empty/default)
    if (largestFace != Rectangle.Empty)
    {
        int faceX = largestFace.X;
        int faceY = largestFace.Y;
        int width = largestFace.Width;
        int height = largestFace.Height;

        int centerX = faceX + (width / 2);
        int centerY = faceY + (height / 2);

        Console.Clear();
        Console.WriteLine($"[Largest Face Detected]");
        Console.WriteLine($"Top-Left Corner: X={faceX}, Y={faceY}");
        Console.WriteLine($"Center of Face : X={centerX}, Y={centerY}");
        Console.WriteLine($"Face Size      : {width}x{height} pixels (Area: {width * height})");

        // Draw a bounding box around the largest face
        CvInvoke.Rectangle(frame, largestFace, new MCvScalar(0, 255, 0), 2);

        // Draw a dot at the center
        CvInvoke.Circle(frame, new Point(centerX, centerY), 4, new MCvScalar(0, 0, 255), -1);
        
        var centerXRelativeToFrameCenter = centerX - frame.Width / 2.0f;
        var a = (centerXRelativeToFrameCenter / (frame.Width / 2.0f)) * 10;
        
        var centerYRelativeToFrameCenter = centerY - frame.Height / 2.0f;
        var b = (centerYRelativeToFrameCenter / (frame.Height / 2.0f)) * 10;

        var listenerPosition = new Vector3(a * 2, b * 2, 0);

        Console.WriteLine("Updating listener position to " + listenerPosition);
        engine.UpdateSourcePosition("music", listenerPosition);
        engine.UpdateSpatialCalculations();
    }
    else
    {
        // Optional: Clear or report when no faces are visible
        Console.Clear();
        Console.WriteLine("Searching for faces...");
    }

    CvInvoke.Imshow(windowName, frame);
}

CvInvoke.DestroyAllWindows();