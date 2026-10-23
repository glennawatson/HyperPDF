// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Tells the line breaker how much room a line has at a height on the page.</summary>
internal interface ILineBounds
{
    /// <summary>Gets the space taken from the edges of the text area at a height.</summary>
    /// <param name="top">The y of the top of the line.</param>
    /// <param name="height">The height of the line.</param>
    /// <returns>The space taken on each side.</returns>
    LineSpan GetSpan(float top, float height);
}
