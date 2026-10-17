# JLVision 1D / 2D Recognition Adapter (managed recovery)

This adds a functional **managed alternative** for the barcode wrapper families
that were removed from the C# API. It does **not** re-enable JLVisionCore native
barcode, DataMatrix or OCR procedures, whose index/slot/lifetime contracts
remain unverified.

## What is available

| Class | Formats | Backend |
|---|---|---|
| `JLVisionLib.Recognition.JlBarCode` | Code128, Code39, Code93, EAN-13/8, UPC-A/E, ITF, Codabar | ZXing.Net 0.16.11 |
| `JLVisionLib.Recognition.JlDataCode2D` | QR Code, DataMatrix | ZXing.Net 0.16.11 |

This is a separate project on .NET 8 and does not introduce ZXing into the
original `JLVisionLib` package or alter the native binary.

### Example: existing JlImage

```csharp
using JLVisionLib;
using JLVisionLib.Recognition;

using var image = new JlImage(@"C:\vision\codes.png");
// For a color image, call Rgb1ToGray() explicitly and dispose that temporary.
IReadOnlyList<JlRecognitionHit> codes = new JlDataCode2D().Find(image);
foreach (JlRecognitionHit code in codes)
    Console.WriteLine($"{code.Format}: {code.Text}");
```

### Example: camera-supplied Gray8 buffer

```csharp
byte[] gray8 = GetSingleChannelPixels();
int width = 640, height = 480;
var reader = new JlBarCode();
var symbols = reader.FindGray8(gray8, width, height);
```

The Gray8 input must be tightly packed row-major with exactly width*height
bytes. `JlImage` inputs must be single-channel, pixel type `byte`; the
adapter copies the native pixel buffer synchronously before ZXing handles it.
Callers must not dispose or mutate the image concurrently. All results contain
text, symbology and result points (not guaranteed to exist for every type).
No confidence score or original HALCON-style symbol region is invented.

### ROI / original-image coordinates

```csharp
// ROI is column(X), row(Y), width and height in ORIGINAL image pixels.
var roi = new JlRecognitionRoi(Column: 120, Row: 80, Width: 360, Height: 240);
var hits = new JlDataCode2D().Find(image, roi);
// ResultPoints are translated back into the original full-image coordinates.
```

The ROI is **cropped before** decoding (not a result filter). Invalid,
out-of-bounds, zero-sized, and integer-overflowing ROIs are rejected. The same
ROI overload is available for `FindGray8(byte[], width, height, roi)`.
An ROI provides an explicit search area but does **not** automatically carry
camera calibration, robot coordinates, a match-confidence score or a quality
certificate. Keep the entire crop and sufficient quiet zone around barcodes.

### Build and test

```powershell
dotnet run --project tests/JLVisionLib.Recognition.Smoke/JLVisionLib.Recognition.Smoke.csproj --configuration Debug
# On Windows x64, also test the native JlImage -> managed adapter bridge:
dotnet run --project tests/JLVisionLib.Recognition.Smoke/JLVisionLib.Recognition.Smoke.csproj --configuration Debug -- --windows-native-bridge
```

The synthetic smoke tests generate Code128, QR and DataMatrix, exercise blank
images, format isolation, buffer-size protection, ROI coordinate offsets and
bounds checks, and (Windows) the `JlImage` bridge. This does not establish industrial decoding rate on real labels. Use
production-image benchmarks with positive/negative labels and real P95 latency
before product release.

### Why original native wrappers were not recreated

`JLVisionCore.dll` contains static name strings mentioning barcode, DataMatrix
and OCR, but native operator IDs and IO contracts have not been verified.
A prior scan of unknown registry indices caused `AccessViolationException`
and has been removed. Invented native calls would risk process corruption.

Future restoration of native `JlBarCode`, `JlDataCode2D` and `JlOCR*`
requires the matching vendor API/schema, verified native model ownership,
license checks and real-image functional tests. The managed classes above have
distinct namespace and do not claim binary compatibility with the original
HALCON-style wrappers.

ZXing.Net is Apache-2.0 licensed; review its license and dependencies when
redistributing. OCR is **not** restored by this project.
