// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Security;

/// <summary>How one stream of an encrypted document is protected.</summary>
/// <param name="Clear">Whether the stream is stored unencrypted.</param>
/// <param name="Filter">The named crypt filter that protects it, or none for the document's stream filter (/StmF).</param>
[DebuggerDisplay("StreamCrypt: clear {Clear}, filter {Filter}")]
internal readonly record struct StreamCrypt(bool Clear, PdfName Filter)
{
    /// <summary>
    /// Chooses the crypt filter of a stream (ISO 32000-2 7.6.6): cross-reference streams, streams whose own /Crypt filter names
    /// none or Identity, and metadata streams when metadata is not encrypted are stored in the clear; a /Crypt filter with a
    /// /Name uses that filter; an embedded file stream with no filter of its own uses /EFF; others use /StmF.
    /// </summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="security">The security handler.</param>
    /// <returns>The choice.</returns>
    internal static StreamCrypt Choose(PdfDictionary dictionary, PdfSecurityHandler security)
    {
        if (TryGetOwnFilter(dictionary, out var own))
        {
            return Named(own);
        }

        if (dictionary.IsName(KnownName.Type, KnownName.XRef)
            || (!security.EncryptMetadata && dictionary.IsName(KnownName.Type, KnownName.Metadata)))
        {
            return new(true, default);
        }

        return security.TryGetEmbeddedFileFilter(dictionary, out var embedded) ? Named(embedded) : new(false, default);
    }

    /// <summary>Finds the crypt filter a stream names itself: its first filter is /Crypt, and /DecodeParms may give a /Name.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="name">The crypt filter's name; none when the stream names no filter, which means Identity.</param>
    /// <returns><see langword="true"/> when the stream's first filter is /Crypt.</returns>
    private static bool TryGetOwnFilter(PdfDictionary dictionary, out PdfName name)
    {
        var filters = dictionary.Get(KnownName.Filter);
        name = default;
        if (!(filters.AsArray()?.GetName(0) ?? filters.AsName()).Is(KnownName.Crypt))
        {
            return false;
        }

        var parameters = dictionary.Get(KnownName.DecodeParms);
        name = (parameters.AsArray()?.GetDictionary(0) ?? parameters.AsDictionary())?.GetName(KnownName.Name) ?? default;
        return true;
    }

    /// <summary>Gets the choice for a named filter, where no name and Identity pass the data through.</summary>
    /// <param name="name">The filter name.</param>
    /// <returns>The choice.</returns>
    private static StreamCrypt Named(PdfName name) => new(name.IsNone || name.Is(KnownName.Identity), name);
}
