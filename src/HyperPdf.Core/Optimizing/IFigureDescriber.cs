// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Optimizing;

/// <summary>Describes a picture in a few words, for the alternate text that tells a screen reader what a figure shows.</summary>
public interface IFigureDescriber
{
    /// <summary>Describes a picture.</summary>
    /// <param name="bgra">The picture's pixels, four bytes each: blue, green, red and alpha, with no gaps between rows.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="cancellationToken">Cancels the description.</param>
    /// <returns>A short description, or <see langword="null"/> when the picture cannot be described.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    string? Describe(ReadOnlySpan<byte> bgra, int width, int height, CancellationToken cancellationToken);
}
