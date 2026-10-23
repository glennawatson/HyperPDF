// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdf.Core.Tests;

/// <summary>Sets the current culture for a test and restores it on dispose.</summary>
internal sealed class CultureScope : IDisposable
{
    /// <summary>The culture before.</summary>
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

    /// <summary>Initializes a new instance of the <see cref="CultureScope"/> class.</summary>
    /// <param name="name">The culture's name.</param>
    internal CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

    /// <inheritdoc/>
    public void Dispose() => CultureInfo.CurrentCulture = _previous;
}
