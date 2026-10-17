using JLVisionLib.Recognition;
using JLVisionLib;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.Common;

static byte[] Generate(BarcodeFormat format, string message, int width, int height)
{
    var writer = new BarcodeWriterPixelData
    {
        Format = format,
        Options = new EncodingOptions { Width = width, Height = height, Margin = 16, PureBarcode = true }
    };
    var bitmap = writer.Write(message);
    var gray = new byte[checked(bitmap.Width * bitmap.Height)];
    for (int i = 0; i < gray.Length; i++)
    {
        // ZXing's PixelData uses BGRA bytes. Generated symbols are grayscale.
        gray[i] = bitmap.Pixels[4 * i];
    }
    return gray;
}

static void AssertContains(IReadOnlyList<JlRecognitionHit> hits, string text, string format)
{
    if (!hits.Any(x => x.Text == text && x.Format == format))
        throw new Exception($"Expected {format}: {text}; received [{string.Join(", ", hits.Select(x => x.Format + ":" + x.Text))}]");
}

var oneD = new JlBarCode();
var twoD = new JlDataCode2D();
var code128 = Generate(BarcodeFormat.CODE_128, "JLVISION-128-42", 480, 120);
AssertContains(oneD.FindGray8(code128, 480, 120), "JLVISION-128-42", "CODE_128");
Console.WriteLine("PASS Code128");

var qr = Generate(BarcodeFormat.QR_CODE, "JLVISION-QR-42", 256, 256);
AssertContains(twoD.FindGray8(qr, 256, 256), "JLVISION-QR-42", "QR_CODE");
Console.WriteLine("PASS QR");

var matrix = Generate(BarcodeFormat.DATA_MATRIX, "JLVISION-DM-42", 256, 256);
AssertContains(twoD.FindGray8(matrix, 256, 256), "JLVISION-DM-42", "DATA_MATRIX");
Console.WriteLine("PASS DataMatrix");

// Format restrictions must not silently treat 2D codes as 1D.
if (oneD.FindGray8(qr, 256, 256).Count != 0)
    throw new Exception("1D reader incorrectly accepted a QR code.");

var white = Enumerable.Repeat((byte)255, 256 * 256).ToArray();
if (twoD.FindGray8(white, 256, 256).Count != 0)
    throw new Exception("Blank grayscale image must produce no detections.");

bool threw = false;
try { twoD.FindGray8(new byte[99], 10, 10); }
catch (ArgumentException) { threw = true; }
if (!threw) throw new Exception("Invalid pixel buffer size was accepted.");

threw = false;
try { twoD.FindGray8(new byte[1], int.MaxValue, int.MaxValue); }
catch (ArgumentOutOfRangeException) { threw = true; }
if (!threw) throw new Exception("Oversized image was accepted.");

// Validate ROI cropping, original-image result coordinates and no detections outside ROI.
var canvas = Enumerable.Repeat((byte)255, 512 * 400).ToArray();
const int offsetCol = 88, offsetRow = 72;
for (int row = 0; row < 256; row++)
    Buffer.BlockCopy(qr, row * 256, canvas, (offsetRow + row) * 512 + offsetCol, 256);
var imageRoi = new JlRecognitionRoi(offsetCol, offsetRow, 256, 256);
var roiHits = twoD.FindGray8(canvas, 512, 400, imageRoi);
AssertContains(roiHits, "JLVISION-QR-42", "QR_CODE");
var baseHits = twoD.FindGray8(qr, 256, 256);
var localHit = baseHits.First(x => x.Text == "JLVISION-QR-42");
var roiHit = roiHits.First(x => x.Text == "JLVISION-QR-42");
if (localHit.Points.Length == 0 || localHit.Points.Length != roiHit.Points.Length)
    throw new Exception("QR result points were not preserved by ROI decoding.");
for (int i = 0; i < localHit.Points.Length; i++)
{
    if (Math.Abs(localHit.Points[i].Column + offsetCol - roiHit.Points[i].Column) > 0.01 ||
        Math.Abs(localHit.Points[i].Row + offsetRow - roiHit.Points[i].Row) > 0.01)
        throw new Exception("ROI result point was not expressed in original-image coordinates.");
}
if (twoD.FindGray8(canvas, 512, 400, new JlRecognitionRoi(0, 0, 80, 70)).Count != 0)
    throw new Exception("A blank ROI must not detect a symbol from outside the crop.");

foreach (var invalid in new[]
{
    new JlRecognitionRoi(-1, 0, 10, 10),
    new JlRecognitionRoi(0, 0, 0, 10),
    new JlRecognitionRoi(0, 0, 10, 0),
    new JlRecognitionRoi(510, 0, 10, 10),
    new JlRecognitionRoi(0, 399, 10, 10),
    new JlRecognitionRoi(int.MaxValue, 0, 1, 1),
})
{
    bool rejected = false;
    try { twoD.FindGray8(canvas, 512, 400, invalid); }
    catch (ArgumentOutOfRangeException) { rejected = true; }
    if (!rejected) throw new Exception("Invalid or overflowing ROI accepted: " + invalid);
}
Console.WriteLine("PASS ROI crop, global result points, blank ROI and bounds validation");

if (args.Contains("--windows-native-bridge"))
{
    if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        throw new PlatformNotSupportedException("Native JlImage bridge test needs Windows x64.");
    var pinned = GCHandle.Alloc(qr, GCHandleType.Pinned);
    try
    {
        using var image = new JlImage();
        // Copy the pinned buffer into runtime-owned memory before unpinning it.
        image.GenImage1Rect(pinned.AddrOfPinnedObject(), 256, 256,
            256, 8, 8, "true", IntPtr.Zero);
        AssertContains(twoD.Find(image), "JLVISION-QR-42", "QR_CODE");
        var bridgeRoiHits = twoD.Find(image, new JlRecognitionRoi(0, 0, 256, 256));
        AssertContains(bridgeRoiHits, "JLVISION-QR-42", "QR_CODE");
    }
    finally { pinned.Free(); }
    Console.WriteLine("PASS native JlImage Gray8 bridge");
}

Console.WriteLine("PASS format isolation, empty image and input validation");
Console.WriteLine("Managed recognition smoke: ALL PASSED. Not a native DLL barcode/OCR certification.");
