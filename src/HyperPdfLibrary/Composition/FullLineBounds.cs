// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Composition;

/// <summary>Bounds that leave every line the whole width of the text area.</summary>
internal sealed class FullLineBounds : ILineBounds
{
    /// <summary>Gets the bounds.</summary>
    internal static FullLineBounds Instance { get; } = new();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LineSpan GetSpan(float top, float height) => LineSpan.None;
}
