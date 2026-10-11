// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>Restores output intent substitutes after parsing a colour space.</summary>
/// <param name="Previous">The earlier substitutes.</param>
[DebuggerDisplay("OutputIntentColorScope")]
internal readonly record struct OutputIntentColorScope(OutputIntentColors? Previous) : IDisposable
{
    /// <summary>Restores the earlier substitutes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => OutputIntentColors.Restore(Previous);
}
