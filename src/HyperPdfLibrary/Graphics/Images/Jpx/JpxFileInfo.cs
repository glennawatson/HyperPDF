// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>What the JP2 boxes around a codestream say, or the defaults for a raw codestream.</summary>
/// <param name="Codestream">Where the codestream is.</param>
/// <param name="ColorSpace">The declared colour space.</param>
/// <param name="Palette">The palette, or <see langword="null"/>.</param>
/// <param name="Mappings">The component mapping, or <see langword="null"/>.</param>
/// <param name="Definitions">The channel definitions, or <see langword="null"/>.</param>
[DebuggerDisplay("JpxFileInfo: {ColorSpace}")]
internal sealed record JpxFileInfo(JpxDataRange Codestream, JpxColorSpace ColorSpace, JpxPalette? Palette, JpxChannelMapping[]? Mappings, JpxChannelDefinition[]? Definitions)
{
    /// <summary>
    /// Gets where the ICC profile of the best colour specification is, or an empty range when that specification is not a
    /// profile. When it is, <see cref="ColorSpace"/> is the best enumerated space, for use when the profile cannot be read.
    /// </summary>
    internal JpxDataRange Profile { get; init; }

    /// <summary>
    /// Gets the opacity type an Opacity box states (ISO 15444-2 M.11.7.6): 0 when the last channel is opacity, 1 when it is
    /// premultiplied opacity, or -1 when the file has no Opacity box or the box is a chroma-key, which PDF ignores.
    /// </summary>
    internal int OpacityKind { get; init; } = -1;

    /// <summary>Gets the CIELab parameters of the best enumerated colour space when it is CIELab with explicit parameters, otherwise <see langword="null"/> (the defaults apply).</summary>
    internal JpxLabParameters? Lab { get; init; }
}
