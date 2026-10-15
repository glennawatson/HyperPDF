// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>What re-encoding one image produced.</summary>
/// <param name="Stream">The new image, or <see langword="null"/> when it is left to lossless recompression.</param>
/// <param name="Note">What was done, or why the image was left alone; empty when there is nothing to say.</param>
[DebuggerDisplay("ImageOutcome: {Note}")]
internal readonly record struct ImageOutcome(PdfStream? Stream, string Note)
{
    /// <summary>Gets the outcome of an image left to lossless recompression with nothing to report.</summary>
    internal static ImageOutcome Unchanged => new(null, string.Empty);

    /// <summary>Gets a value indicating whether the image was left alone for a reason worth reporting.</summary>
    internal bool IsSkip => Stream is null && Note.Length > 0;
}
