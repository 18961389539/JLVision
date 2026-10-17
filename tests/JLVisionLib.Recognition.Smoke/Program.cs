using JLVisionLib.Recognition;
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

Console.WriteLine("PASS format isolation, empty image and input validation");
Console.WriteLine("Managed recognition smoke: ALL PASSED. Not a native DLL barcode/OCR certification.");
