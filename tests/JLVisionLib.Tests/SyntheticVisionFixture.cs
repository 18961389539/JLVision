using System;
using System.IO;

namespace JLVisionLib.Tests;

/// <summary>
/// Creates deterministic 8-bit grayscale BMPs without third-party imaging dependencies.
/// The scene contains exactly one copy of an asymmetric, high-contrast 80x80 target.
/// Synthetic patterns are for API regression only, not production accuracy certification.
/// </summary>
internal sealed class SyntheticVisionFixture : IDisposable
{
    public const int TargetWidth = 80;
    public const int TargetHeight = 80;
    public const int SceneWidth = 256;
    public const int SceneHeight = 192;
    public const int TargetLeft = 94;
    public const int TargetTop = 58;

    private readonly string directory = Path.Combine(Path.GetTempPath(), "JLVision-benchmark-" + Guid.NewGuid().ToString("N"));

    public string TemplatePath { get; }
    public string ScenePath { get; }
    public string NegativeScenePath { get; }

    public SyntheticVisionFixture()
    {
        Directory.CreateDirectory(directory);
        TemplatePath = Path.Combine(directory, "template.bmp");
        ScenePath = Path.Combine(directory, "scene.bmp");
        NegativeScenePath = Path.Combine(directory, "negative-scene.bmp");
        byte[] target = BuildTarget();
        byte[] scene = new byte[SceneWidth * SceneHeight];
        Array.Fill(scene, (byte)216);
        for (int row = 0; row < TargetHeight; row++)
        {
            Array.Copy(target, row * TargetWidth, scene,
                (row + TargetTop) * SceneWidth + TargetLeft, TargetWidth);
        }
        WriteGrayBmp(TemplatePath, TargetWidth, TargetHeight, target);
        WriteGrayBmp(ScenePath, SceneWidth, SceneHeight, scene);
        var negative = new byte[SceneWidth * SceneHeight];
        Array.Fill(negative, (byte)216);
        WriteGrayBmp(NegativeScenePath, SceneWidth, SceneHeight, negative);
    }

    private static byte[] BuildTarget()
    {
        byte[] pixels = new byte[TargetWidth * TargetHeight];
        Array.Fill(pixels, (byte)216);
        for (int y = 9; y < 71; y++)
        {
            for (int x = 9; x < 71; x++)
            {
                byte value = 216;
                // Asymmetrical L silhouette with cutouts and graded texture.
                if ((x >= 15 && x <= 28 && y >= 16 && y <= 63) ||
                    (x >= 15 && x <= 64 && y >= 50 && y <= 63))
                    value = 30;
                if (x >= 38 && x <= 59 && y >= 19 && y <= 40)
                    value = ((x / 5 + y / 5) % 2 == 0) ? (byte)73 : (byte)162;
                if ((x - 50) * (x - 50) + (y - 29) * (y - 29) < 36)
                    value = 245;
                if (x >= 34 && x <= 43 && y >= 44 && y <= 51)
                    value = 111;
                pixels[y * TargetWidth + x] = value;
            }
        }
        return pixels;
    }

    private static void WriteGrayBmp(string path, int width, int height, byte[] pixels)
    {
        if (pixels.Length != checked(width * height))
            throw new ArgumentException("Pixel count does not match image size.", nameof(pixels));
        int stride = (width + 3) & ~3;
        int pixelOffset = 14 + 40 + 256 * 4;
        int length = checked(pixelOffset + stride * height);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0x4d42);
        writer.Write(length);
        writer.Write(0);
        writer.Write(pixelOffset);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((ushort)1);
        writer.Write((ushort)8);
        writer.Write(0);
        writer.Write(stride * height);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(256);
        writer.Write(0);
        for (int value = 0; value < 256; value++)
        {
            byte gray = (byte)value;
            writer.Write(gray);
            writer.Write(gray);
            writer.Write(gray);
            writer.Write((byte)0);
        }
        byte[] row = new byte[stride];
        for (int y = height - 1; y >= 0; y--)
        {
            Array.Clear(row, 0, row.Length);
            Array.Copy(pixels, y * width, row, 0, width);
            writer.Write(row);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
