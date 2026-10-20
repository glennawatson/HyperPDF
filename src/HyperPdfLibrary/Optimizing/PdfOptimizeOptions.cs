// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.PdfUa;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Optimizing;

/// <summary>What <see cref="PdfOptimizer"/> may change; start from a preset and override settings with <c>with</c>.</summary>
[DebuggerDisplay("PdfOptimizeOptions: {ColorImageDpi} dpi, quality {JpegQuality}, lossy {AllowLossyImages}")]
public readonly record struct PdfOptimizeOptions
{
    /// <summary>The image resolution the balanced preset keeps.</summary>
    private const int BalancedDpi = 200;

    /// <summary>The resolution the balanced preset keeps for black-and-white images.</summary>
    private const int BalancedMonochromeDpi = 300;

    /// <summary>The resolution the smaller preset keeps for black-and-white images.</summary>
    private const int SmallerMonochromeDpi = 200;

    /// <summary>The JPEG quality the balanced preset writes.</summary>
    private const int BalancedQuality = 80;

    /// <summary>The image resolution the smaller preset keeps.</summary>
    private const int SmallerDpi = 150;

    /// <summary>The JPEG quality the smaller preset writes.</summary>
    private const int SmallerQuality = 70;

    /// <summary>How far above the target an image's resolution must be before it is downsampled.</summary>
    private const float DefaultThreshold = 1.5F;

    /// <summary>Initializes a new instance of the <see cref="PdfOptimizeOptions"/> struct with the balanced defaults.</summary>
    public PdfOptimizeOptions()
    {
    }

    /// <summary>Gets the preset for the smallest file: colour and grey images at 150 dpi, black-and-white images at 200 dpi, JPEG quality 70.</summary>
    public static PdfOptimizeOptions Smaller { get; } = new() { ColorImageDpi = SmallerDpi, GrayImageDpi = SmallerDpi, MonochromeImageDpi = SmallerMonochromeDpi, JpegQuality = SmallerQuality };

    /// <summary>Gets the balanced preset: colour and grey images at 200 dpi, black-and-white images at 300 dpi, JPEG quality 80.</summary>
    public static PdfOptimizeOptions Balanced { get; } = new();

    /// <summary>Gets the preset that changes no pixel: lossless recompression, deduplication and font subsetting only.</summary>
    public static PdfOptimizeOptions KeepQuality { get; } = new() { AllowLossyImages = false };

    /// <summary>Gets a value indicating whether images are downsampled or re-encoded.</summary>
    public bool OptimizeImages { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether images may lose detail: downsampling and JPEG encoding. When off, images are only
    /// re-encoded losslessly. Masks are never compressed lossily either way.
    /// </summary>
    public bool AllowLossyImages { get; init; } = true;

    /// <summary>Gets the resolution colour images are downsampled to, in pixels per inch at their largest use.</summary>
    public int ColorImageDpi { get; init; } = BalancedDpi;

    /// <summary>Gets the resolution greyscale images are downsampled to, in pixels per inch at their largest use.</summary>
    public int GrayImageDpi { get; init; } = BalancedDpi;

    /// <summary>
    /// Gets the resolution black-and-white (1-bit) images are downsampled to, in pixels per inch at their largest use, or
    /// zero to leave them at their own resolution. A shrunk image is averaged and then made black and white again, so thin
    /// lines may lose a little weight. It applies only when <see cref="AllowLossyImages"/> is on.
    /// </summary>
    public int MonochromeImageDpi { get; init; } = BalancedMonochromeDpi;

    /// <summary>
    /// Gets how many times the target resolution an image must exceed before it is downsampled; 1.5 leaves images up to
    /// half again above the target alone, as resampling them saves little.
    /// </summary>
    public float DownsampleThreshold { get; init; } = DefaultThreshold;

    /// <summary>Gets the JPEG quality, from 1 to 100, for colour and greyscale images re-encoded lossily.</summary>
    public int JpegQuality { get; init; } = BalancedQuality;

    /// <summary>
    /// Gets the codec for continuous-tone grey and RGB images: JPEG by default. JPEG 2000 needs no image codec for encoding
    /// and also compresses losslessly; the result is used only when it is smaller than the image it replaces.
    /// </summary>
    public PdfOptimizeImageCodec ImageCodec { get; init; }

    /// <summary>Gets a value indicating whether black-and-white images are re-encoded as CCITT Group 4 when that is smaller.</summary>
    public bool EncodeBilevelAsCcitt { get; init; } = true;

    /// <summary>
    /// Gets whether black-and-white images may be re-encoded as JBIG2 when that is smaller than the other codings; off by
    /// default. The optimiser keeps the JBIG2 data in the image stream, with no separate /JBIG2Globals stream, and never
    /// writes JBIG2 in a file that claims PDF/A-1.
    /// </summary>
    public PdfJbig2Mode Jbig2 { get; init; } = PdfJbig2Mode.Off;

    /// <summary>Gets a value indicating whether uncompressed streams are compressed and Flate streams recompressed at the best level.</summary>
    public bool RecompressStreams { get; init; } = true;

    /// <summary>Gets a value indicating whether identical streams, fonts and graphics states are merged.</summary>
    public bool RemoveDuplicates { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether objects are packed into object streams with a cross-reference stream. PDF/A-1
    /// files keep a classic table either way.
    /// </summary>
    public bool UseObjectStreams { get; init; } = true;

    /// <summary>Gets a value indicating whether embedded TrueType fonts are cut down to the glyphs the pages show.</summary>
    public bool SubsetFonts { get; init; } = true;

    /// <summary>Gets the optional cleanup steps; none by default.</summary>
    public PdfCleanupItems Cleanup { get; init; }

    /// <summary>
    /// Gets a value indicating whether missing accessibility entries are filled in: /Lang, the title, /DisplayDocTitle,
    /// and /MarkInfo for documents that have a structure tree.
    /// </summary>
    public bool FixAccessibility { get; init; } = true;

    /// <summary>Gets the language written to /Lang when the document has none and its XMP gives none, such as "en-AU".</summary>
    public string? Language { get; init; }

    /// <summary>
    /// Gets a value indicating whether an untagged document gets a basic structure tree inferred from its layout. Figures
    /// get no alternative text; the report counts them so a person can add it.
    /// </summary>
    public bool AddInferredTags { get; init; }

    /// <summary>
    /// Gets a value indicating whether inferred tags carry a <c>/Lang</c> guessed from each element's own text. A guess is
    /// a hint: the tree is marked as inferred in the XMP, and the document language is not changed.
    /// </summary>
    public bool GuessLanguage { get; init; }

    /// <summary>
    /// Gets the hook that describes an inferred figure from its page, box and caption text, or <see langword="null"/> to
    /// leave figures without alternative text for a person to write.
    /// </summary>
    public PdfFigureAltTextProvider? FigureAltText { get; init; }

    /// <summary>
    /// Gets the text recognition hook: given a page index, returns the words found on that page, or
    /// <see langword="null"/>. It is called only for pages that draw images and no text, and the words are added as an
    /// invisible text layer.
    /// </summary>
    public Func<int, IReadOnlyList<PdfOcrWord>?>? OcrWords { get; init; }

    /// <summary>Gets a value indicating whether an encrypted document is written decrypted; by default it keeps its encryption.</summary>
    public bool RemoveEncryption { get; init; }

    /// <summary>
    /// Gets a value indicating whether a signed document may still get additions, such as text layers, appended as an
    /// incremental update. When off, a signed document is copied unchanged.
    /// </summary>
    public bool AllowIncrementalWhenSigned { get; init; } = true;

    /// <summary>
    /// Gets what the optimiser does when the document claims PDF/UA-1 and the optimised file would break the claim in a way
    /// the optimiser cannot repair: refuse with a <see cref="PdfException"/> (<see cref="PdfError.ClaimUnmet"/>) and write
    /// nothing, which is the default, or remove the claim and write the file.
    /// </summary>
    public PdfUaUnmetClaimAction UnmetPdfUaClaim { get; init; }
}
