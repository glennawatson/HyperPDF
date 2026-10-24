// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Tests.Graphics.Jpeg;
using HyperPdfLibrary.Tests.Optimizing;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Embeds JPEG 2000 images from <see cref="PdfJpxEncoder"/> in PDF/A-2b and PDF/A-3b files and runs the optimiser with JPEG 2000
/// on them, then checks each file with veraPDF. The JPX rules of ISO 19005-2 are clause 6.2.8.3; the CMYK files use a device
/// space the fixtures' RGB output intent does not cover, so those files are checked for the JPX clause only.
/// </summary>
[NotInParallel(nameof(VeraPdfValidator))]
public sealed class PdfAJpxValidationTests
{
    /// <summary>The side of the encoded images.</summary>
    private const int Side = 32;

    /// <summary>The bits per sample.</summary>
    private const int Eight = 8;

    /// <summary>The clause of ISO 19005-2 that holds the JPEG 2000 rules.</summary>
    private const string JpxClause = "6.2.8.3";

    /// <summary>The components of a grey image.</summary>
    private const int Gray = 1;

    /// <summary>The components of an RGB image.</summary>
    private const int Rgb = 3;

    /// <summary>The components of a CMYK image.</summary>
    private const int Cmyk = 4;

    /// <summary>The offset of the profile class in an ICC profile header.</summary>
    private const int ProfileClassOffset = 12;

    /// <summary>The pixels of the photo the optimiser recodes.</summary>
    private const int PhotoPixels = 600;

    /// <summary>The seed of the generated samples.</summary>
    private const uint Seed = 5;

    /// <summary>JPEG 2000 files the encoder writes pass the JPX rules of PDF/A-2 and PDF/A-3.</summary>
    /// <param name="part">The PDF/A part.</param>
    /// <param name="kind">The kind of image: gray, rgb, rgbalpha, cmyk, restrictedicc or anyicc.</param>
    /// <param name="cancellationToken">Cancels the validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(2, "gray")]
    [Arguments(2, "rgb")]
    [Arguments(2, "rgbalpha")]
    [Arguments(2, "cmyk")]
    [Arguments(2, "restrictedicc")]
    [Arguments(2, "anyicc")]
    [Arguments(3, "gray")]
    [Arguments(3, "rgb")]
    [Arguments(3, "rgbalpha")]
    [Arguments(3, "cmyk")]
    [Arguments(3, "restrictedicc")]
    [Arguments(3, "anyicc")]
    public async Task EncoderOutputPassesTheJpxRules(int part, string kind, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        var level = new PdfAFixtureLevel(part, "B");
        using var folder = new PdfAOutputFolder($"jpx-{kind}-{level.Flavour}");
        var path = Path.Combine(folder.Path, "image.pdf");
        await File.WriteAllBytesAsync(path, PdfAFixtures.CreateWithImage(level, Image(kind)), cancellationToken);

        var job = (await VeraPdfValidator.ValidateAsync(home, level.Flavour, [path], cancellationToken))[Path.GetFullPath(path)];
        var jpxFailures = job.Failures.Where(static failure => failure.Clause.StartsWith(JpxClause, StringComparison.Ordinal)).ToList();

        await Assert.That(string.Join("; ", jpxFailures)).IsEmpty().Because($"the JPX rules fail for {kind} in PDF/A-{level.Label}");
        if (kind != "cmyk")
        {
            await Assert.That(job.IsCompliant).IsTrue().Because($"the {kind} file fails {job.FailureSummary}");
        }
    }

    /// <summary>The optimiser with JPEG 2000 chosen keeps PDF/A-2 and PDF/A-3 files compliant, whether it stores JPX or keeps the image.</summary>
    /// <param name="part">The PDF/A part.</param>
    /// <param name="cancellationToken">Cancels the optimisation and validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(2)]
    [Arguments(3)]
    public async Task OptimiserOutputWithJpeg2000StaysCompliant(int part, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        var level = new PdfAFixtureLevel(part, "B");
        using var folder = new PdfAOutputFolder($"optimize-jpx-{level.Flavour}");
        var source = Path.Combine(folder.Path, "source.pdf");
        var photo = new PdfAFixtureImage(OptimizerSamples.PhotoPixels(PhotoPixels, PhotoPixels), PhotoPixels, PhotoPixels, "DeviceRGB", false);
        await File.WriteAllBytesAsync(source, PdfAFixtures.CreateWithImage(level, photo), cancellationToken);
        var optimized = Path.Combine(folder.Path, "optimized.pdf");
        using (var document = SkiaTestRegistration.Open(await File.ReadAllBytesAsync(source, cancellationToken), null))
        {
            await using var stream = File.Create(optimized);
            var options = PdfOptimizeOptions.Smaller with { ImageCodec = PdfOptimizeImageCodec.Jpeg2000 };
            await PdfOptimizer.OptimizeAsync(document, stream, options, null, cancellationToken);
        }

        var results = await VeraPdfValidator.ValidateAsync(home, level.Flavour, [source, optimized], cancellationToken);
        await Assert.That(results[Path.GetFullPath(source)].IsCompliant).IsTrue().Because($"the source fails {results[Path.GetFullPath(source)].FailureSummary}");
        var job = results[Path.GetFullPath(optimized)];
        await Assert.That(job.IsCompliant).IsTrue().Because($"the optimised file fails {job.FailureSummary}");
        await Assert.That(PdfAReadingComparison.ClaimFromProfile(job.ProfileName)).IsEqualTo(level.Label);
        await Assert.That(System.Text.Encoding.Latin1.GetString(await File.ReadAllBytesAsync(optimized, cancellationToken))).Contains("/JPXDecode").Because("the photo is stored as JPEG 2000");
    }

    /// <summary>Makes the image of a kind.</summary>
    /// <param name="kind">The kind: gray, rgb, rgbalpha, cmyk, restrictedicc or anyicc.</param>
    /// <returns>The image, with its JPX file.</returns>
    private static PdfAFixtureImage Image(string kind)
    {
        var components = kind switch
        {
            "gray" => Gray,
            "rgbalpha" or "cmyk" => Cmyk,
            _ => Rgb,
        };
        var samples = new byte[Side * Side * components];
        var random = new JpegTestRandom(Seed);
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)random.Next(0, byte.MaxValue + 1);
        }

        var image = new PdfJpxImage(Side, Side, components, Eight, samples);
        var file = kind switch
        {
            "cmyk" => PdfJpxEncoder.Encode(image with { ColorSpace = PdfJpxColorSpace.Cmyk }),
            "rgbalpha" => PdfJpxEncoder.Encode(image with { HasAlpha = true }),
            "restrictedicc" => PdfJpxEncoder.Encode(image with { IccProfile = PdfAFixtures.RgbProfile() }),
            "anyicc" => PdfJpxEncoder.Encode(image with { IccProfile = NonRestricted() }),
            _ => PdfJpxEncoder.Encode(image),
        };

        // A device space in the dictionary would override the file's own; the cases name none, except CMYK, which has no ICC profile to carry.
        return new(file, Side, Side, kind == "cmyk" ? "DeviceCMYK" : null, true);
    }

    /// <summary>Makes an RGB profile that the restricted ICC method does not allow: the fixtures' display profile with the printer class.</summary>
    /// <returns>The profile bytes.</returns>
    private static byte[] NonRestricted()
    {
        var profile = PdfAFixtures.RgbProfile();
        "prtr"u8.CopyTo(profile.AsSpan(ProfileClassOffset));
        return profile;
    }
}
