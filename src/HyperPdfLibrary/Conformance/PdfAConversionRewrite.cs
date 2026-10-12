// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>One run of <see cref="PdfAConversionRewriter"/>: the conversion, the fix and its state.</summary>
/// <typeparam name="TState">The fix's state.</typeparam>
/// <param name="Session">The conversion.</param>
/// <param name="State">The state passed to the fix.</param>
/// <param name="Fix">Returns a changed copy of a dictionary, or <see langword="null"/> to leave it.</param>
[DebuggerDisplay("PdfAConversionRewrite: {Session}")]
internal sealed record PdfAConversionRewrite<TState>(PdfAConversionSession Session, TState State, Func<PdfAConversionSession, TState, PdfDictionary, PdfDictionary?> Fix);
