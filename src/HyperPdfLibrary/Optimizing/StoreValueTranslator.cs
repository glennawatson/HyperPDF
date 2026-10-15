// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Copies values from one store into another that reads the same file, re-interning names in the target's name table.
/// References keep their numbers, as both stores number objects the same way. Stream data is copied decrypted.
/// </summary>
/// <param name="source">The store the values come from.</param>
/// <param name="target">The store they go into.</param>
[DebuggerDisplay("StoreValueTranslator")]
internal readonly ref struct StoreValueTranslator(PdfObjectStore source, PdfObjectStore target)
{
    /// <summary>Copies the source's unsaved edits and its trailer's /Root and /Info into the target.</summary>
    internal void ReplayEdits()
    {
        foreach (var edited in StoreEditing.GetEditedObjects(source, out _))
        {
            var id = new PdfObjectId(edited.Number, 0);
            if (edited.Deleted)
            {
                StoreEditing.Delete(target, id);
            }
            else
            {
                StoreEditing.Replace(target, id, Copy(edited.Value, 0));
            }
        }

        target.Trailer.Set(KnownName.Root, source.Trailer.GetRaw(KnownName.Root));
        target.Trailer.Set(KnownName.Info, Copy(source.Trailer.GetRaw(KnownName.Info), 0));

        // The target reads with the key already known, so it would decrypt the /Encrypt dictionary's own strings; take
        // the source's copy, which was read before the key existed.
        var encrypt = source.Trailer.GetRaw(KnownName.Encrypt);
        if (encrypt.IsReference)
        {
            StoreEditing.Replace(target, new(encrypt.AsReference().Number, 0), Copy(StoreReading.GetObject(source, encrypt.AsReference()), 0));
        }
    }

    /// <summary>Copies a value.</summary>
    /// <param name="value">The source value.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The copy.</returns>
    private PdfValue Copy(PdfValue value, int depth) => depth > PdfLimits.MaxNesting ? PdfValue.Null : value.Kind switch
    {
        PdfKind.Name => PdfValue.FromName(Name(value.AsName())),
        PdfKind.Array => PdfValue.FromArray(CopyArray(value.AsArray()!, depth + 1)),
        PdfKind.Dictionary => PdfValue.FromDictionary(CopyDictionary(value.AsDictionary()!, depth + 1)),
        PdfKind.Stream => PdfValue.FromStream(CopyStream(value.AsStream()!, depth + 1)),
        _ => value,
    };

    /// <summary>Gets a source name in the target's table.</summary>
    /// <param name="name">The source name.</param>
    /// <returns>The target name.</returns>
    private PdfName Name(PdfName name) => name.IsKnown ? name : target.Names.Intern(source.Names.GetSpelling(name));

    /// <summary>Copies an array.</summary>
    /// <param name="array">The source array.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The copy.</returns>
    private PdfArray CopyArray(PdfArray array, int depth)
    {
        var copy = new PdfArray(target, array.Count);
        foreach (var item in array.Items)
        {
            copy.Add(Copy(item, depth));
        }

        return copy;
    }

    /// <summary>Copies a dictionary.</summary>
    /// <param name="dictionary">The source dictionary.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The copy.</returns>
    private PdfDictionary CopyDictionary(PdfDictionary dictionary, int depth)
    {
        var copy = new PdfDictionary(target, dictionary.Count);
        for (var i = 0; i < dictionary.Count; i++)
        {
            copy.Set(Name(dictionary.GetKeyAt(i)), Copy(dictionary.GetValueAt(i), depth));
        }

        return copy;
    }

    /// <summary>Copies a stream with its data decrypted.</summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The copy.</returns>
    private PdfStream CopyStream(PdfStream stream, int depth)
    {
        using var raw = stream.LeaseRawData();
        var data = PdfObjectWriter.PlainData(stream, raw.Span).ToArray();
        return new(CopyDictionary(stream.Dictionary, depth), data);
    }
}
