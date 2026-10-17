using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using JLVisionLib;
using JLVisionLib.Recognition;
using JLVisionLib.Recognition.TesseractBackend;

if (args.Length != 1 || !Directory.Exists(args[0]))
    throw new ArgumentException("Expected tessdata directory as the sole argument.");

const int width = 900, height = 180;
using var bitmap = new Bitmap(width, height);
using (Graphics g = Graphics.FromImage(bitmap))
{
    g.Clear(Color.White);
    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    using var font = new Font("Arial", 88f, FontStyle.Bold, GraphicsUnit.Pixel);
    g.DrawString("PART 42", font, Brushes.Black, new PointF(22, 26));
}
var gray = new byte[width * height];
for (int row = 0; row < height; row++)
for (int col = 0; col < width; col++)
    gray[row * width + col] = bitmap.GetPixel(col, row).R;

using var ocr = new JlOcr(args[0]);
var sample = ocr.RecognizeGray8(gray, width, height);
Console.WriteLine("Recognized: " + sample.Text + "; confidence: " + sample.Confidence);
string normalized = new string(sample.Text.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
if (!normalized.Contains("PART42"))
    throw new Exception("OCR failed to decode synthetic PART 42 label.");

const int fullWidth = 1024, fullHeight = 360, originCol = 65, originRow = 78;
var canvas = Enumerable.Repeat((byte)255, fullWidth * fullHeight).ToArray();
for (int row = 0; row < height; row++)
    Buffer.BlockCopy(gray, row * width, canvas, (originRow + row) * fullWidth + originCol, width);
var roiRead = ocr.RecognizeGray8(canvas, fullWidth, fullHeight,
    new JlRecognitionRoi(originCol, originRow, width, height));
string roiNormalized = new string(roiRead.Text.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
if (!roiNormalized.Contains("PART42"))
    throw new Exception("OCR ROI failed to decode original synthetic label.");

bool rejected = false;
try { ocr.RecognizeGray8(canvas, fullWidth, fullHeight, new JlRecognitionRoi(-1, 0, 100, 100)); }
catch (ArgumentOutOfRangeException) { rejected = true; }
if (!rejected) throw new Exception("Invalid OCR ROI was accepted.");

var pinned = GCHandle.Alloc(gray, GCHandleType.Pinned);
try
{
    using var image = new JlImage();
    image.GenImage1Rect(pinned.AddrOfPinnedObject(), width, height,
        width, 8, 8, "true", IntPtr.Zero);
    var nativeImageResult = ocr.Recognize(image);
    var converted = new string(nativeImageResult.Text.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    if (!converted.Contains("PART42"))
        throw new Exception("OCR native JlImage bridge lost text.");
}
finally { pinned.Free(); }
Console.WriteLine("PASS OCR synthetic text, ROI, invalid ROI and JlImage bridge");
Console.WriteLine("Synthetic OCR smoke is not an industrial recognition certification.");
