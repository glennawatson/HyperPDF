// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>A conversion fix with its session and state, passed through the shared rewriter as one value.</summary>
/// <typeparam name="TState">The fix's state.</typeparam>
/// <param name="Session">The conversion.</param>
/// <param name="State">The state passed to the fix.</param>
/// <param name="Fix">Returns a changed copy of a dictionary, or <see langword="null"/> to leave it.</param>
internal readonly record struct PdfAConversionSessionFix<TState>(PdfAConversionSession Session, TState State, Func<PdfAConversionSession, TState, PdfDictionary, PdfDictionary?> Fix);
