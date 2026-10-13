// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>Puts new objects into an open document, as edits that saving writes.</summary>
[DebuggerDisplay("PdfObjectStoreSink")]
public sealed class PdfObjectStoreSink : IPdfObjectSink
{
    /// <summary>Initializes a new instance of the <see cref="PdfObjectStoreSink"/> class.</summary>
    /// <param name="store">The open document.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    public PdfObjectStoreSink(PdfObjectStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Store = store;
    }

    /// <summary>Gets the open document.</summary>
    public PdfObjectStore Store { get; }

    /// <inheritdoc/>
    public PdfNameTable Names => Store.Names;

    /// <inheritdoc/>
    public PdfObjectStore? Owner => Store;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfObjectId Reserve() => StoreEditing.Add(Store, default);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfObjectId Add(PdfValue value) => StoreEditing.Add(Store, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(PdfObjectId id, PdfValue value) => StoreEditing.Replace(Store, id, value);
}
