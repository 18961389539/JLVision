using System.Runtime.InteropServices;
using JLVisionLib;
using JLVisionLib.Recognition;
using Tesseract;

namespace JLVisionLib.Recognition.TesseractBackend;

/// <summary>OCR output from the configured Tesseract language data; confidence is 0..1.</summary>
public sealed record JlOcrResult(string Text, float Confidence)
{
    public bool HasText => !string.IsNullOrWhiteSpace(Text);
}

public enum JlOcrPageMode { Auto, SingleLine, SingleWord }

/// <summary>
/// Optional managed OCR adapter. This is NOT the unverified JLVisionCore OCR
/// algorithm. The caller supplies traineddata and disposes this engine.
/// A shared engine is guarded by a lock because TesseractEngine is not thread-safe.
/// </summary>
public sealed class JlOcr : IDisposable
{
    private const int MaxPixels = 20_000_000;
    private readonly object gate = new();
    private readonly TesseractEngine engine;
    private bool disposed;

    public JlOcr(string tessdataDirectory, string language = "eng")
    {
        if (string.IsNullOrWhiteSpace(tessdataDirectory))
            throw new ArgumentException("A tessdata directory is required.", nameof(tessdataDirectory));
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("At least one trained language is required.", nameof(language));
        foreach (var code in language.Split('+'))
        {
            if (code.Length == 0 || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
                throw new ArgumentException("Language must be explicit traineddata names such as eng+chi_sim.", nameof(language));
            if (!File.Exists(Path.Combine(tessdataDirectory, code + ".traineddata")))
                throw new FileNotFoundException("Required OCR traineddata missing.", code + ".traineddata");
        }
        engine = new TesseractEngine(tessdataDirectory, language, EngineMode.Default);
    }

    /// <summary>Read tightly packed, single-channel Gray8 pixels, optionally cropping to ROI.</summary>
    public JlOcrResult RecognizeGray8(byte[] pixels, int width, int height,
        JlRecognitionRoi? roi = null, JlOcrPageMode mode = JlOcrPageMode.SingleLine)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ValidateSize(width, height);
        if (pixels.Length != width * height)
            throw new ArgumentException("Expected exactly width*height Gray8 bytes.", nameof(pixels));

        int originColumn = 0, originRow = 0, localWidth = width, localHeight = height;
        if (roi is JlRecognitionRoi region)
        {
            if (region.Column < 0 || region.Row < 0 || region.Width <= 0 || region.Height <= 0 ||
                region.Column > width - region.Width || region.Row > height - region.Height)
                throw new ArgumentOutOfRangeException(nameof(roi), "ROI must fit entirely inside the original image.");
            originColumn = region.Column;
            originRow = region.Row;
            localWidth = region.Width;
            localHeight = region.Height;
        }
        byte[] bmp = EncodeGray8Bmp(pixels, width, originColumn, originRow, localWidth, localHeight);
        lock (gate)
        {
            if (disposed) throw new ObjectDisposedException(nameof(JlOcr));
            using var pix = Pix.LoadFromMemory(bmp);
            using var page = engine.Process(pix, mode switch
            {
                JlOcrPageMode.Auto => PageSegMode.Auto,
                JlOcrPageMode.SingleLine => PageSegMode.SingleLine,
                JlOcrPageMode.SingleWord => PageSegMode.SingleWord,
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            });
            return new JlOcrResult((page.GetText() ?? "").Trim(), page.GetMeanConfidence());
        }
    }

    /// <summary>Copy pixel data from an existing single-channel byte JlImage; caller retains ownership.</summary>
    public JlOcrResult Recognize(JlImage image, JlRecognitionRoi? roi = null,
        JlOcrPageMode mode = JlOcrPageMode.SingleLine)
    {
        ArgumentNullException.ThrowIfNull(image);
        using (var channels = image.CountChannels())
        {
            if (channels.Length != 1 || channels[0].I != 1)
                throw new NotSupportedException("OCR requires single-channel Gray8 JlImage.");
        }
        IntPtr pointer = image.GetImagePointer1(out string type, out int width, out int height);
        if (!string.Equals(type, "byte", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("OCR requires byte pixel type.");
        int count = ValidateSize(width, height);
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("Native image pixel pointer was null.");
        byte[] copy = new byte[count];
        // The input JlImage may not be mutated/disposed concurrently.
        Marshal.Copy(pointer, copy, 0, count);
        GC.KeepAlive(image);
        return RecognizeGray8(copy, width, height, roi, mode);
    }

    private static int ValidateSize(int width, int height)
    {
        long count = (long)width * height;
        if (width <= 0 || height <= 0 || count > MaxPixels)
            throw new ArgumentOutOfRangeException(nameof(width), "OCR image dimensions must be positive and at most 20 MP.");
        return (int)count;
    }

    // Valid 8-bit indexed BMP with a grayscale palette. Leptonica/Tesseract
    // reads it from memory: no temporary production-image files are created.
    private static byte[] EncodeGray8Bmp(byte[] pixels, int stride, int x, int y, int width, int height)
    {
        int rowStride = checked((width + 3) & ~3);
        int pixelBytes = checked(rowStride * height);
        const int offset = 14 + 40 + 256 * 4;
        using var buffer = new MemoryStream(offset + pixelBytes);
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write((byte)'B'); writer.Write((byte)'M');
            writer.Write(offset + pixelBytes);
            writer.Write((short)0); writer.Write((short)0);
            writer.Write(offset);
            writer.Write(40);
            writer.Write(width); writer.Write(height);
            writer.Write((short)1); writer.Write((short)8);
            writer.Write(0);
            writer.Write(pixelBytes);
            writer.Write(2835); writer.Write(2835);
            writer.Write(256); writer.Write(0);
            for (int i = 0; i < 256; i++)
            {
                writer.Write((byte)i); writer.Write((byte)i);
                writer.Write((byte)i); writer.Write((byte)0);
            }
            for (int row = height - 1; row >= 0; row--)
            {
                writer.Write(pixels, checked((y + row) * stride + x), width);
                for (int pad = width; pad < rowStride; pad++) writer.Write((byte)0);
            }
        }
        return buffer.ToArray();
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            engine.Dispose();
        }
    }
}
