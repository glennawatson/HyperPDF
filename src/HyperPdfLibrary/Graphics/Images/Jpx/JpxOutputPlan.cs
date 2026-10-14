// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Decides which colour space the decoded channels are read in and how they become samples. The rules, in order:
/// <list type="number">
/// <item>An image dictionary /ColorSpace decides, and the colour boxes of the file are ignored (ISO 32000-1 section
/// 7.4.9). The file needs at least as many channels as the space has components.</item>
/// <item>Otherwise the colour specification of the file with the highest precedence and best approximation decides
/// (ISO 32000-2 section 7.4.9). An ICC profile that can be read becomes an ICCBased space. An enumerated colour space
/// (T.800 table I.10) is sRGB for DeviceRGB, greyscale for DeviceGray, CMYK for DeviceCMYK, and sYCC and e-sYCC become
/// DeviceRGB after conversion.</item>
/// <item>Otherwise (an unreadable ICC profile, a vendor method, a raw codestream, or an enumerated space that needs more colour
/// channels than the file has) section 7.4.9 applies: the colour channel count picks DeviceGray for one or two,
/// DeviceRGB for three and DeviceCMYK for four or more. The colour channel count is the channel definition box's when
/// it has one, else every channel.</item>
/// <item>Our rule for files that do not say what they hold: three colour channels whose second and third are
/// subsampled while the first is not are taken as YCbCr, because RGB is never stored with reduced chroma.</item>
/// </list>
/// </summary>
/// <param name="ColorSpace">The colour space the samples are read in.</param>
/// <param name="Components">The samples per pixel written.</param>
/// <param name="Mode">How values become samples.</param>
/// <param name="Sycc">Whether the first three channels are YCbCr to convert to RGB.</param>
/// <param name="KeepsDictionarySpace">Whether <see cref="ColorSpace"/> is the image dictionary's own.</param>
[DebuggerDisplay("JpxOutputPlan: {Components} components, {Mode}")]
internal sealed record JpxOutputPlan(PdfColorSpace ColorSpace, int Components, JpxSampleMode Mode, bool Sycc, bool KeepsDictionarySpace)
{
    /// <summary>The most channels read as grey: grey alone, or grey with its opacity.</summary>
    private const int GrayChannels = 2;

    /// <summary>The colour channels of an RGB or YCbCr image.</summary>
    private const int ThreeChannels = 3;

    /// <summary>The colour channels of a CMYK image.</summary>
    private const int FourChannels = 4;

    /// <summary>The channel that carries the first chroma plane of YCbCr.</summary>
    private const int FirstChroma = 1;

    /// <summary>The channel that carries the second chroma plane of YCbCr.</summary>
    private const int SecondChroma = 2;

    /// <summary>Plans the conversion.</summary>
    /// <param name="declared">The colour space the file declares.</param>
    /// <param name="set">The channels in colour order.</param>
    /// <param name="geometry">The codestream geometry, which gives each component's subsampling.</param>
    /// <param name="dictionarySpace">The image dictionary's /ColorSpace, or <see langword="null"/>.</param>
    /// <param name="profileSpace">The colour space of the file's best ICC profile, or <see langword="null"/> when it has none that can be used.</param>
    /// <returns>The plan, or <see langword="null"/> when the file has too few channels for the colour space.</returns>
    internal static JpxOutputPlan? Create(JpxColorSpace declared, in JpxChannelSet set, JpxGeometry geometry, PdfColorSpace? dictionarySpace, PdfColorSpace? profileSpace)
    {
        var available = set.Channels.Length;
        if (dictionarySpace is not null)
        {
            return FromDictionary(dictionarySpace, available);
        }

        if (FromProfile(profileSpace, set) is { } profilePlan)
        {
            return profilePlan;
        }

        var colours = set.ColourCount > 0 ? Math.Min(set.ColourCount, available) : available;
        var components = ColourComponents(declared, colours);
        var sycc = declared is JpxColorSpace.Sycc or JpxColorSpace.Eycc
            ? components == ThreeChannels
            : components == ThreeChannels && declared is JpxColorSpace.Unspecified or JpxColorSpace.Unknown && HasReducedChroma(set.Channels, geometry);
        return new(DeviceSpace(components), components, JpxSampleMode.Scaled, sycc, false);
    }

    /// <summary>Plans for the ICC profile of the file's best colour specification, which needs as many colour channels as it has components.</summary>
    /// <param name="profileSpace">The profile's colour space, or <see langword="null"/>.</param>
    /// <param name="set">The channels.</param>
    /// <returns>The plan, or <see langword="null"/> when there is no usable profile or the file has too few colour channels.</returns>
    private static JpxOutputPlan? FromProfile(PdfColorSpace? profileSpace, in JpxChannelSet set)
    {
        var colours = set.ColourCount > 0 ? set.ColourCount : set.Channels.Length;
        return profileSpace is not null && profileSpace.Components <= colours ? new(profileSpace, profileSpace.Components, JpxSampleMode.Scaled, false, false) : null;
    }

    /// <summary>Plans for the image dictionary's colour space.</summary>
    /// <param name="dictionarySpace">The colour space.</param>
    /// <param name="available">The channels in the file.</param>
    /// <returns>The plan, or <see langword="null"/> when the file has too few channels.</returns>
    private static JpxOutputPlan? FromDictionary(PdfColorSpace dictionarySpace, int available)
    {
        var mode = dictionarySpace.Kind == PdfColorSpaceKind.Indexed ? JpxSampleMode.RawIndices : JpxSampleMode.Scaled;
        return dictionarySpace.Components > available ? null : new(dictionarySpace, dictionarySpace.Components, mode, false, true);
    }

    /// <summary>Gets the colour components to write: the declared space's when the file has enough colour channels, else by channel count.</summary>
    /// <param name="declared">The declared colour space.</param>
    /// <param name="colours">The colour channels the file has.</param>
    /// <returns>1, 3 or 4.</returns>
    private static int ColourComponents(JpxColorSpace declared, int colours)
    {
        var wanted = declared switch
        {
            JpxColorSpace.Gray => 1,
            JpxColorSpace.Srgb or JpxColorSpace.Sycc or JpxColorSpace.Eycc => ThreeChannels,
            JpxColorSpace.Cmyk => FourChannels,
            _ => 0,
        };

        return wanted > 0 && wanted <= colours ? wanted : ByCount(colours);
    }

    /// <summary>Gets the components to write for a channel count when the file's colour space does not decide (ISO 32000-1 section 7.4.9).</summary>
    /// <param name="colours">The colour channels.</param>
    /// <returns>1 for up to <see cref="GrayChannels"/> channels, 3 for three, else 4.</returns>
    private static int ByCount(int colours) => colours switch
    {
        <= GrayChannels => 1,
        ThreeChannels => ThreeChannels,
        _ => FourChannels,
    };

    /// <summary>Gets the device colour space with a number of components.</summary>
    /// <param name="components">1, 3 or 4.</param>
    /// <returns>The colour space.</returns>
    private static PdfColorSpace DeviceSpace(int components) => components switch
    {
        1 => PdfColorSpace.DeviceGray,
        ThreeChannels => PdfColorSpace.DeviceRgb,
        _ => PdfColorSpace.DeviceCmyk,
    };

    /// <summary>Determines whether the second and third channels are sampled more coarsely than the first, which is at full resolution.</summary>
    /// <param name="channels">The channels in colour order, at least three.</param>
    /// <param name="geometry">The codestream geometry.</param>
    /// <returns><see langword="true"/> when the chroma looks reduced.</returns>
    private static bool HasReducedChroma(JpxChannel[] channels, JpxGeometry geometry)
    {
        var components = geometry.Components;
        var luma = components[channels[0].Component];
        var first = components[channels[FirstChroma].Component];
        var second = components[channels[SecondChroma].Component];
        return luma is { Dx: 1, Dy: 1 } && IsReduced(first) && IsReduced(second);
    }

    /// <summary>Determines whether a component skips grid points.</summary>
    /// <param name="component">The component.</param>
    /// <returns><see langword="true"/> when either subsampling factor is above one.</returns>
    private static bool IsReduced(in JpxComponentInfo component) => component.Dx > 1 || component.Dy > 1;
}
