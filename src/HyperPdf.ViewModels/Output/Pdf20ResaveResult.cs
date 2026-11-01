// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Output;

/// <summary>What rewriting a saved file as PDF 2.0 did.</summary>
/// <param name="Written">Whether the file now is a PDF 2.0 file.</param>
/// <param name="Reason">Why the file was left in its own version, in plain words, or <see langword="null"/> when it was written or no reason is known.</param>
[DebuggerDisplay("Pdf20ResaveResult: written {Written}")]
internal sealed record Pdf20ResaveResult(bool Written, string? Reason);
