// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Objects;

/// <summary>Resolves indirect objects and reads the store's cached values.</summary>
public static class StoreReading
{
    /// <summary>Follows a reference, and any chain of references, to its value.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "value">The value.</param>
    /// <returns>The resolved value; null when the object is missing.</returns>
    public static PdfValue Resolve(PdfObjectStore self, PdfValue value)
    {
        for (var i = 0; value.IsReference && i < PdfLimits.MaxReferenceChain; i++)
        {
            value = StoreReading.GetObject(self, value.AsReference());
        }

        if (value.IsReference)
        {
            PdfOpenContext.Report(self.Context, PdfDiagnosticCode.RecursionLimit, "A reference chain was too long and was cut off.", 0, -1);
            return default;
        }

        return value;
    }

    /// <summary>Gets an indirect object.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "id">The object id; the generation is not checked, as damaged files often get it wrong.</param>
    /// <returns>The value; null when missing.</returns>
    /// <exception cref = "ObjectDisposedException">The store has been disposed.</exception>
    public static PdfValue GetObject(PdfObjectStore self, PdfObjectId id)
    {
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        var number = id.Number;
        var cache = Volatile.Read(ref self.CacheState);
        if ((uint)number < (uint)cache.Length && Volatile.Read(ref cache[number]) is { } cached)
        {
            // Disposal returns the array to the pool; an object read from an array that was recycled meanwhile must not be handed out.
            ObjectDisposedException.ThrowIf(self.IsDisposed, self);
            return StoreReading.Unbox(cached);
        }

        lock (self.Gate)
        {
            return StoreReading.GetObjectLocked(self, number);
        }
    }

    /// <summary>Gets an indirect object as a dictionary, including a stream's dictionary.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "id">The object id.</param>
    /// <returns>The dictionary, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfDictionary? GetDictionary(PdfObjectStore self, PdfObjectId id) => StoreReading.GetObject(self, id).AsDictionary();

    /// <summary>Converts a cached object back to a value.</summary>
    /// <param name = "cached">The cached object.</param>
    /// <returns>The value.</returns>
    internal static PdfValue Unbox(object cached) => cached switch
    {
        PdfDictionary dictionary => PdfValue.FromDictionary(dictionary),
        PdfStream stream => PdfValue.FromStream(stream),
        PdfArray array => PdfValue.FromArray(array),
        ValueBox box => box.Value,
        _ => default,
    };

    /// <summary>Wraps a value for the cache.</summary>
    /// <param name = "value">The value.</param>
    /// <returns>The object to cache.</returns>
    internal static object Box(PdfValue value) => value.Kind switch
    {
        PdfKind.Dictionary => value.AsDictionary()!,
        PdfKind.Stream => value.AsStream()!,
        PdfKind.Array => value.AsArray()!,
        PdfKind.Null => StoreReading.ValueBox.NullValue,
        _ => new ValueBox(value),
    };

    /// <summary>Gets an object while holding the lock.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <returns>The value.</returns>
    internal static PdfValue GetObjectLocked(PdfObjectStore self, int number)
    {
        if (StoreEditing.TryGetEdited(self, number, out var edited))
        {
            return edited;
        }

        if ((uint)number < (uint)self.CacheState.Length && self.CacheState[number] is { } cached)
        {
            return StoreReading.Unbox(cached);
        }

        var found = StoreReading.TryLoad(self, number, out var value);
        if (!found && !self.RepairTriedState && self.Context is not { Recovery: false } && self.XrefState.GetType(number) != XrefEntryType.Free)
        {
            // The table pointed somewhere wrong: rebuild it once from a scan of the file, then try again.
            PdfOpenContext.Report(self.Context, PdfDiagnosticCode.BrokenObject, "An object was not where the cross-reference table said.", number, self.XrefState.GetLocation(number));
            PdfOpenContext.Report(self.Context, PdfDiagnosticCode.XrefRebuilt, "The cross-reference table was rebuilt after a broken object.", 0, -1);
            StoreRepairs.Repair(self);
            value = StoreReading.Load(self, number);
        }

        if ((uint)number < (uint)self.CacheState.Length)
        {
            Volatile.Write(ref self.CacheState[number], StoreReading.Box(value));
        }

        return value;
    }

    /// <summary>Parses an object from wherever the table says it is.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <returns>The value; null when missing or unreadable.</returns>
    internal static PdfValue Load(PdfObjectStore self, int number) => StoreReading.TryLoad(self, number, out var value) ? value : default;

    /// <summary>Parses an object, telling a missing object apart from one whose value is null.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <param name = "value">The value; null when not found.</param>
    /// <returns><see langword="false"/> when the table's location does not hold the expected object.</returns>
    internal static bool TryLoad(PdfObjectStore self, int number, out PdfValue value)
    {
        switch (self.XrefState.GetType(number))
        {
            case XrefEntryType.InFile:
                {
                    return StoreReading.TryLoadFromFile(self, number, self.XrefState.GetLocation(number), out value);
                }

            case XrefEntryType.Compressed:
                {
                    return StoreObjectStreams.TryLoadFromObjectStream(self, number, (int)self.XrefState.GetLocation(number), self.XrefState.GetDetail(number), out value);
                }

            default:
                {
                    value = default;
                    return true;
                }
        }
    }

    /// <summary>Parses an object at a file offset, checking it is the object expected.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <param name = "offset">The offset.</param>
    /// <param name = "value">The value; null when the offset does not hold that object.</param>
    /// <returns><see langword="true"/> when the offset holds the object's header.</returns>
    internal static bool TryLoadFromFile(PdfObjectStore self, int number, long offset, out PdfValue value)
    {
        // Offsets count from the header; some writers count from the start of the file instead.
        if (self.XrefState.OffsetBase != 0 && StoreParsing.TryParseObjectAt(self, offset + self.XrefState.OffsetBase, out var shiftedId, out var shifted) && shiftedId.Number == number)
        {
            value = shifted;
            return true;
        }

        var found = StoreParsing.TryParseObjectAt(self, offset, out var id, out value) && id.Number == number;
        if (!found)
        {
            value = default;
        }

        return found;
    }

    /// <summary>A cached value that is not a dictionary, array or stream.</summary>
    /// <param name = "value">The value.</param>
    private sealed class ValueBox(PdfValue value)
    {
        /// <summary>Gets the shared box for null objects.</summary>
        internal static ValueBox NullValue { get; } = new(default);

        /// <summary>Gets the value.</summary>
        internal PdfValue Value { get; } = value;
    }
}
