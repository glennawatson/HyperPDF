// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Features;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// The device colour spaces a document's output intent profile stands in for. DeviceCMYK, DeviceGray and DeviceRGB
/// convert through the profile when its component count matches the family; the other families keep their own conversion.
/// Instances are immutable and safe to use from many threads.
/// </summary>
[DebuggerDisplay("OutputIntentColors: gray {_gray != null}, rgb {_rgb != null}, cmyk {_cmyk != null}")]
internal sealed class OutputIntentColors
{
    /// <summary>The output intent type of PDF/A.</summary>
    private const string PdfASubtype = "GTS_PDFA1";

    /// <summary>The output intent type of PDF/X.</summary>
    private const string PdfXSubtype = "GTS_PDFX";

    /// <summary>The profile components of a gray profile.</summary>
    private const int GrayComponents = 1;

    /// <summary>The substitutes in force on this thread while an image, shading or colour space is parsed, or <see langword="null"/>.</summary>
    [ThreadStatic]
    private static OutputIntentColors? _active;

    /// <summary>The substitute for DeviceGray, or <see langword="null"/>.</summary>
    private readonly PdfColorSpace? _gray;

    /// <summary>The substitute for DeviceRGB, or <see langword="null"/>.</summary>
    private readonly PdfColorSpace? _rgb;

    /// <summary>The substitute for DeviceCMYK, or <see langword="null"/>.</summary>
    private readonly PdfColorSpace? _cmyk;

    /// <summary>Initializes a new instance of the <see cref="OutputIntentColors"/> class.</summary>
    /// <param name="gray">The substitute for DeviceGray, or <see langword="null"/>.</param>
    /// <param name="rgb">The substitute for DeviceRGB, or <see langword="null"/>.</param>
    /// <param name="cmyk">The substitute for DeviceCMYK, or <see langword="null"/>.</param>
    private OutputIntentColors(PdfColorSpace? gray, PdfColorSpace? rgb, PdfColorSpace? cmyk)
    {
        _gray = gray;
        _rgb = rgb;
        _cmyk = cmyk;
    }

    /// <summary>Gets a value indicating whether no device space has a substitute.</summary>
    internal bool IsEmpty => _gray is null && _rgb is null && _cmyk is null;

    /// <summary>Makes substitutes apply to device colour spaces parsed on this thread until the scope is disposed.</summary>
    /// <param name="colors">The substitutes, or <see langword="null"/> for none.</param>
    /// <returns>The scope; dispose it to restore the previous substitutes.</returns>
    internal static OutputIntentColorScope Enter(OutputIntentColors? colors)
    {
        var scope = new OutputIntentColorScope(_active);
        _active = colors;
        return scope;
    }

    /// <summary>Restores the output intent substitutes that were previously active.</summary>
    /// <param name="previous">The earlier substitutes.</param>
    internal static void Restore(OutputIntentColors? previous) => _active = previous;

    /// <summary>Applies the substitutes in force on this thread to a device space.</summary>
    /// <param name="space">The device space, or <see langword="null"/>.</param>
    /// <returns>The substitute, or <paramref name="space"/> when none applies.</returns>
    internal static PdfColorSpace? Apply(PdfColorSpace? space) => space is not null && _active is { } colors ? colors.Map(space) : space;

    /// <summary>Builds the substitutes from the document's output intents.</summary>
    /// <param name="intents">The output intents.</param>
    /// <returns>The substitutes; empty when no PDF/A or PDF/X intent has a usable profile.</returns>
    internal static OutputIntentColors Create(PdfOutputIntent[] intents)
    {
        foreach (var intent in intents)
        {
            if (!IsRenderable(intent) || intent.Profile is not { } profile)
            {
                continue;
            }

            var transform = IccProfile.Get(profile, intent.ComponentCount);
            if (transform is null || transform.IsSrgb)
            {
                continue;
            }

            var device = PdfColorSpace.FromComponents(intent.ComponentCount);
            var space = new OutputIntentColorSpace(device, new IccBasedColorSpace(device, UnitRange(intent.ComponentCount), transform));
            return intent.ComponentCount switch
            {
                GrayComponents => new(space, null, null),
                PdfColorSpace.RgbComponents => new(null, space, null),
                _ => new(null, null, space),
            };
        }

        return new(null, null, null);
    }

    /// <summary>Gets the space a colour set in a device space converts through.</summary>
    /// <param name="space">The space the content named.</param>
    /// <returns>The profile space for DeviceGray, DeviceRGB or DeviceCMYK when the intent supplies one; otherwise <paramref name="space"/>.</returns>
    internal PdfColorSpace Map(PdfColorSpace space) => space.Kind switch
    {
        PdfColorSpaceKind.DeviceGray => _gray ?? space,
        PdfColorSpaceKind.DeviceRgb => _rgb ?? space,
        PdfColorSpaceKind.DeviceCmyk => _cmyk ?? space,
        _ => space,
    };

    /// <summary>Determines whether an output intent describes the device the document was prepared for.</summary>
    /// <param name="intent">The intent.</param>
    /// <returns><see langword="true"/> for PDF/A and PDF/X intents with a supported component count.</returns>
    private static bool IsRenderable(PdfOutputIntent intent) =>
        intent.Subtype is PdfASubtype or PdfXSubtype
        && intent.ComponentCount is GrayComponents or PdfColorSpace.RgbComponents or DeviceCmykColorSpace.ComponentCount;

    /// <summary>Builds the 0 to 1 range of every component.</summary>
    /// <param name="components">The component count.</param>
    /// <returns>Min/max pairs.</returns>
    private static float[] UnitRange(int components)
    {
        var range = new float[PdfColorSpace.PairSize * components];
        for (var i = 1; i < range.Length; i += PdfColorSpace.PairSize)
        {
            range[i] = 1;
        }

        return range;
    }
}
