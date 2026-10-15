// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Media;

namespace HyperPdf.App.Services;

/// <summary>Retains screen font families for one application, on the UI thread.</summary>
internal sealed class PageFontFamilies
{
    /// <summary>The families requested by page editors, made on first use.</summary>
    private Dictionary<string, FontFamily>? _families;

    /// <summary>Finds a family already requested.</summary>
    /// <param name="name">The requested family.</param>
    /// <param name="family">The screen font, when found.</param>
    /// <returns>Whether the family was retained.</returns>
    internal bool TryGet(string name, out FontFamily? family)
    {
        if (_families is not null && _families.TryGetValue(name, out family))
        {
            return true;
        }

        family = null;
        return false;
    }

    /// <summary>Retains a screen font for later page editors.</summary>
    /// <param name="name">The requested family.</param>
    /// <param name="family">The screen font.</param>
    internal void Add(string name, FontFamily family) =>
        (_families ??= [with(StringComparer.OrdinalIgnoreCase)])[name] = family;
}
