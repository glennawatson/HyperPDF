// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
namespace HyperPdf.Engine;

/// <summary>Implements Attachments over the document's owned state.</summary>
internal static class HyperPdfAttachments
{
    /// <summary>The maximum attachment listing size in bytes.</summary>
    internal const int MaxListingDecode = 64 * 1024 * 1024;

    /// <summary>
    /// Gets an embedded file's size: the /Params /Size the file records, else the decoded length. A stream too big to
    /// decode for a listing, or one that fails to decode, reports 0 so one bad file never breaks the list.
    /// </summary>
    /// <param name="data">The embedded file stream.</param>
    /// <returns>The size in bytes, or 0 when there is no data or it cannot be read.</returns>
    internal static long AttachmentSize(PdfStream? data)
    {
        if (data is null)
        {
            return 0;
        }

        var recorded = data.Dictionary.GetDictionary(KnownName.Params)?.GetInteger(KnownName.Size, -1) ?? -1;
        if (recorded >= 0)
        {
            return recorded;
        }

        return data.RawLength > MaxListingDecode ? 0 : DecodedLength(data);
    }

    /// <summary>Decodes an embedded file to measure it.</summary>
    /// <param name="data">The embedded file stream.</param>
    /// <returns>The decoded length, or 0 when decoding fails.</returns>
    internal static long DecodedLength(PdfStream data)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = data.Decode(ref output);
            return output.Length;
        }
        catch (Exception ex) when (ex is PdfException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        {
            return 0;
        }
        finally
        {
            output.Dispose();
        }
    }
}
