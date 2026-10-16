// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Objects;

/// <summary>Reads compressed objects and controls the decoded stream cache.</summary>
public static class StoreObjectStreams
{
    /// <summary>Gets an object packed in an object stream.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <param name = "container">The object stream's number.</param>
    /// <param name = "index">The object's index in the stream.</param>
    /// <param name = "value">The value; null when missing.</param>
    /// <returns><see langword="true"/> when the object stream holds the object.</returns>
    /// <exception cref="PdfException">Object-stream headers resolve through a recursive chain beyond the parsing limit.</exception>
    internal static bool TryLoadFromObjectStream(PdfObjectStore self, int number, int container, int index, out PdfValue value)
    {
        if (self.ObjectStreamsState.TryGetValue(container, out var cached))
        {
            return cached.TryParse(number, index, self, out value);
        }

        // An object stream inside an object stream is invalid, so the container must be a plain object.
        if (self.XrefState.GetType(container) != XrefEntryType.InFile || StoreReading.GetObjectLocked(self, container).AsStream() is not { } stream)
        {
            value = default;
            return false;
        }

        // Reading the header and filters can resolve compressed references while this container's index is not cached.
        if (self.ParseDepthState >= StoreParsing.MaxParseDepth)
        {
            PdfOpenContext.Report(self.Context, PdfDiagnosticCode.RecursionLimit, "Object stream headers referred to each other too deeply.", container, -1);
            throw new PdfException(PdfError.Format, "Object stream headers contain a recursive reference chain.");
        }

        self.ParseDepthState++;
        try
        {
            var objects = ObjectStreamIndex.Read(stream);
            self.ObjectStreamsState.Add(container, objects);
            return objects.TryParse(number, index, self, out value);
        }
        finally
        {
            self.ParseDepthState--;
        }
    }

    /// <summary>Changes how many decoded object streams are kept.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "maxStreams">The most streams to keep; at least one is always kept.</param>
    /// <param name = "maxBytes">The most decoded bytes to keep, apart from the newest stream.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetObjectStreamLimits(PdfObjectStore self, int maxStreams, long maxBytes) => self.ObjectStreamsState.SetLimits(maxStreams, maxBytes);
}
