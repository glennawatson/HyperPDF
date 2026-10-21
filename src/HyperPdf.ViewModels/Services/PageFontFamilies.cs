// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Services;

/// <summary>Retains the render head's screen fonts for one application, on the UI thread. The head owns the font type.</summary>
[DebuggerDisplay("PageFontFamilies")]
public sealed class PageFontFamilies
{
    /// <summary>The families requested by page editors, made on first use.</summary>
    private Dictionary<string, object>? _families;

    /// <summary>Finds a family already requested.</summary>
    /// <param name="name">The requested family.</param>
    /// <param name="family">The screen font, when found.</param>
    /// <returns>Whether the family was retained.</returns>
    public bool TryGet(string name, out object? family)
    {
        if (_families is not null && _families.TryGetValue(name, out var found))
        {
            family = found;
            return true;
        }

        family = null;
        return false;
    }

    /// <summary>Retains a screen font for later page editors.</summary>
    /// <param name="name">The requested family.</param>
    /// <param name="family">The screen font.</param>
    public void Add(string name, object family) =>
        (_families ??= [with(StringComparer.OrdinalIgnoreCase)])[name] = family;
}
