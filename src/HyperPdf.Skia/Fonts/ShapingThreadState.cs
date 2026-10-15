// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HarfBuzzSharp;
using ShapingBuffer = HarfBuzzSharp.Buffer;

namespace HyperPdf.Skia.Fonts;

/// <summary>Owns one registered text shaper thread's native working buffer and current language.</summary>
internal sealed class ShapingThreadState : IDisposable
{
    /// <summary>The language selected for the last layout.</summary>
    private Language? _language;

    /// <summary>The culture name used to create the selected language.</summary>
    private string? _languageName;

    /// <summary>One after the host releases this state.</summary>
    private int _disposed;

    /// <summary>Gets the reusable native shaping buffer.</summary>
    internal ShapingBuffer Buffer { get; } = new();

    /// <summary>Gets or sets the reusable exact-length array of native OpenType feature overrides.</summary>
    private Feature[] Features { get; set; } = [];

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _language?.Dispose();
        Buffer.Dispose();
    }

    /// <summary>Reuses the language while the thread's culture remains unchanged.</summary>
    /// <param name="culture">The shaping culture.</param>
    /// <returns>The cached native language.</returns>
    internal Language GetLanguage(CultureInfo culture)
    {
        if (_language is not null && _languageName == culture.Name)
        {
            return _language;
        }

        _language?.Dispose();
        _languageName = culture.Name;
        return _language = new(culture);
    }

    /// <summary>Gets the exact-length conversion storage for one feature list.</summary>
    /// <param name="count">The number of feature overrides.</param>
    /// <returns>The reusable conversion array.</returns>
    internal Feature[] GetFeatures(int count)
    {
        if (Features.Length != count)
        {
            Features = new Feature[count];
        }

        return Features;
    }
}
