// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;

namespace HyperPdfLibrary.Fonts;

/// <summary>Owns the base font name strings shared by one drawing service.</summary>
[DebuggerDisplay("Base font names: {Known.Count}")]
internal sealed class BaseFontNameState
{
    /// <summary>Gets the names already decoded.</summary>
    internal ConcurrentDictionary<string, string> Known { get; } = new(StringComparer.Ordinal);
}
