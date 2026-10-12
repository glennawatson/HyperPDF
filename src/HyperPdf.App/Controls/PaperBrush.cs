// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Media;
using Avalonia.Media.Immutable;
using HyperPdf.App.Theming;
using HyperPdf.Core.Rendering;

namespace HyperPdf.App.Controls;

/// <summary>The paper colour pages are drawn on before their tiles arrive, cached so drawing a frame allocates nothing.</summary>
internal static class PaperBrush
{
    /// <summary>Plain white paper.</summary>
    private const uint White = 0xFFFFFFU;

    /// <summary>The tone of the cached brush.</summary>
    private static PageTone _tone = PageTone.None;

    /// <summary>The cached brush.</summary>
    private static ImmutableSolidColorBrush _brush = new(DesktopThemeApplier.ToColor(White));

    /// <summary>Gets the brush for a tone. Call on the UI thread.</summary>
    /// <param name="tone">The tone.</param>
    /// <returns>The paper brush.</returns>
    internal static IImmutableSolidColorBrush Get(PageTone tone)
    {
        ArgumentNullException.ThrowIfNull(tone);
        if (!ReferenceEquals(tone, _tone))
        {
            _tone = tone;
            _brush = new(DesktopThemeApplier.ToColor(tone.IsIdentity ? White : tone.Paper));
        }

        return _brush;
    }
}
