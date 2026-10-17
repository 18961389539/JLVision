using System.Runtime.InteropServices;
using JLVisionLib;
using ZXing;
using ZXing.Common;

namespace JLVisionLib.Recognition;

/// <summary>A decoded 1D/2D symbol; pixel coordinates are image columns (X) and rows (Y).</summary>
public sealed record JlRecognitionHit(string Text, string Format, JlRecognitionPoint[] Points);

public sealed record JlRecognitionPoint(float Column, float Row);

/// <summary>
/// Functional managed barcode recovery adapter backed by ZXing.Net, NOT JLVisionCore's
/// unverified native barcode model. A fresh reader is created per call for thread isolation.
/// </summary>
public sealed class JlBarCode
{
    private static readonly BarcodeFormat[] Formats =
    {
        BarcodeFormat.CODE_128, BarcodeFormat.CODE_39, BarcodeFormat.CODE_93,
        BarcodeFormat.EAN_13, BarcodeFormat.EAN_8,
        BarcodeFormat.UPC_A, BarcodeFormat.UPC_E,
        BarcodeFormat.ITF, BarcodeFormat.CODABAR
    };

    public IReadOnlyList<JlRecognitionHit> Find(JlImage image) =>
        JlRecognitionDecoder.DecodeImage(image, Formats);

    public IReadOnlyList<JlRecognitionHit> FindGray8(byte[] pixels, int width, int height) =>
        JlRecognitionDecoder.DecodeBytes(pixels, width, height, Formats);
}

/// <summary>
/// Functional QR/DataMatrix adapter backed by ZXing.Net, NOT JLVisionCore's
/// unverified native 2D code API.
/// </summary>
public sealed class JlDataCode2D
{
    private static readonly BarcodeFormat[] Formats =
    {
        BarcodeFormat.QR_CODE, BarcodeFormat.DATA_MATRIX
    };

    public IReadOnlyList<JlRecognitionHit> Find(JlImage image) =>
        JlRecognitionDecoder.DecodeImage(image, Formats);

    public IReadOnlyList<JlRecognitionHit> FindGray8(byte[] pixels, int width, int height) =>
        JlRecognitionDecoder.DecodeBytes(pixels, width, height, Formats);
}

internal static class JlRecognitionDecoder
{
    private const int MaxPixels = 100_000_000;

    internal static IReadOnlyList<JlRecognitionHit> DecodeImage(JlImage image, IList<BarcodeFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(image);
        // Validate the image is single-channel. GetImagePointer1 otherwise silently
        // selects only channel 1 from a color image, potentially hiding a configuration error.
        using (var channels = image.CountChannels())
        {
            if (channels.Length != 1 || channels[0].I != 1)
                throw new NotSupportedException("Only single-channel byte JlImage is supported. Convert RGB to grayscale explicitly.");
        }

        IntPtr ptr = image.GetImagePointer1(out string pixelType, out int width, out int height);
        if (!string.Equals(pixelType, "byte", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Expected a byte/Gray8 JlImage, got: " + pixelType);
        int count = ValidateSize(width, height);
        if (ptr == IntPtr.Zero)
            throw new InvalidOperationException("Native image returned a null pixel pointer.");

        // Pixel memory belongs to JlImage: copy it synchronously, without storing the pointer.
        // Caller must not mutate or dispose the input image concurrently.
        var copy = new byte[count];
        Marshal.Copy(ptr, copy, 0, count);
        GC.KeepAlive(image);
        return DecodeBytes(copy, width, height, formats);
    }

    internal static IReadOnlyList<JlRecognitionHit> DecodeBytes(
        byte[] pixels, int width, int height, IList<BarcodeFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        int count = ValidateSize(width, height);
        if (pixels.Length != count)
            throw new ArgumentException($"Expected exactly {count} Gray8 bytes, received {pixels.Length}.", nameof(pixels));

        // A new decoder is used each time; no mutable ZXing reader is shared across threads.
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new List<BarcodeFormat>(formats)
            }
        };
        var results = reader.DecodeMultiple(pixels, width, height, RGBLuminanceSource.BitmapFormat.Gray8);
        if (results == null || results.Length == 0)
        {
            var single = reader.Decode(pixels, width, height, RGBLuminanceSource.BitmapFormat.Gray8);
            return single == null
                ? Array.Empty<JlRecognitionHit>()
                : new[] { Convert(single) };
        }
        return results.Select(Convert).ToArray();
    }

    private static int ValidateSize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Image width and height must be positive.");
        long area = (long)width * height;
        if (area > MaxPixels)
            throw new ArgumentOutOfRangeException(nameof(width), "Image is larger than the configured 100 MP safety limit.");
        return (int)area;
    }

    private static JlRecognitionHit Convert(Result result) =>
        new(result.Text, result.BarcodeFormat.ToString(),
            result.ResultPoints?.Select(x => new JlRecognitionPoint(x.X, x.Y)).ToArray()
                ?? Array.Empty<JlRecognitionPoint>());
}
