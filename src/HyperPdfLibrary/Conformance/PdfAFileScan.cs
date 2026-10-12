// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The state of one pass over a file's bytes for the PDF/A file structure rules.</summary>
/// <param name="context">The validation.</param>
[DebuggerDisplay("PdfAFileScan: {Source.Length} bytes")]
internal sealed class PdfAFileScan(PdfAValidationContext context) : IDisposable
{
    /// <summary>Gets the validation.</summary>
    internal PdfAValidationContext Context { get; } = context;

    /// <summary>Gets the document's objects.</summary>
    internal PdfObjectStore Store { get; } = context.Document.Objects;

    /// <summary>Gets the file's bytes.</summary>
    internal PdfByteSource Source => Store.Source;

    /// <summary>Gets the buffer the syntax reader decodes tokens into.</summary>
    internal PdfASyntaxBuffer Buffer { get; } = new();

    /// <summary>Gets the rules the object being read breaks.</summary>
    internal PdfAObjectFindings Findings { get; } = new();

    /// <summary>Gets the token that cancels the validation.</summary>
    internal CancellationToken CancellationToken => Context.CancellationToken;

    /// <summary>Returns the pooled buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Buffer.Dispose();
}
