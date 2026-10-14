// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace HyperPdf.App.Views;

/// <summary>Makes the brushes of colours the document tools show.</summary>
internal static class ColourBrushes
{
    /// <summary>Opaque alpha in 0xAARRGGBB.</summary>
    private const uint OpaqueAlpha = 0xFF000000U;

    /// <summary>Makes the brush of an opaque colour, for example a colour swatch.</summary>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The brush.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SolidColorBrush Opaque(uint color) => new(Color.FromUInt32(OpaqueAlpha | color));
}
