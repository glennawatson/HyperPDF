// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Deep-copies objects from one document into a <see cref="PdfDocumentBuilder"/>. Every reachable object is copied once
/// and renumbered, so shared objects stay shared and reference cycles end. Page and page-tree objects are never copied
/// (a reference to one becomes null) unless they were mapped first, and <c>/Parent</c> entries are dropped, so copying
/// a page does not pull in the rest of its document. Stream data is copied decrypted, so the copy needs no security
/// handler; names are re-interned in the new document's name table.
/// </summary>
/// <remarks>Not thread-safe. The source must stay open until the new document is saved, as stream data is shared, not copied.</remarks>
[DebuggerDisplay("PdfObjectImporter: {MappedCount} objects")]
public sealed class PdfObjectImporter : IPdfCarrySink
{
    /// <summary>The document being built.</summary>
    private readonly PdfDocumentBuilder _target;

    /// <summary>The document copied from.</summary>
    private readonly PdfObjectStore _source;

    /// <summary>The new number of each old number: zero when not yet copied, -1 when left out.</summary>
    private readonly int[] _map;

    /// <summary>The objects reserved but not yet copied, as old and new numbers.</summary>
    private readonly Queue<PendingObject> _pending = new();

    /// <summary>The replacement dictionaries to copy in place of source objects, by source number.</summary>
    private readonly Dictionary<int, ReplacedObject> _overrides = [];

    /// <summary>Whether the top-level <c>/Parent</c> of the dictionary being copied is kept.</summary>
    private bool _keepParent;

    /// <summary>Initializes a new instance of the <see cref="PdfObjectImporter"/> class.</summary>
    /// <param name="target">The document being built.</param>
    /// <param name="source">The objects copied from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public PdfObjectImporter(PdfDocumentBuilder target, PdfObjectStore source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        _target = target;
        _source = source;
        _map = new int[source.Size];
    }

    /// <summary>Gets the number of source objects copied so far.</summary>
    public int MappedCount { get; private set; }

    /// <summary>Makes references to a source object point at an object already in the new document.</summary>
    /// <param name="source">The source object.</param>
    /// <param name="target">The object in the new document, for example a page reserved before it is filled in.</param>
    public void MapObject(PdfObjectId source, PdfObjectId target)
    {
        if ((uint)source.Number >= (uint)_map.Length || source.Number == 0)
        {
            return;
        }

        if (_map[source.Number] == 0)
        {
            MappedCount++;
        }

        _map[source.Number] = target.Number;
    }

    /// <summary>Copies a value and everything it refers to.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The value in the new document; a reference stays a reference.</returns>
    /// <exception cref="PdfException">The source cannot be read.</exception>
    public PdfValue Import(PdfValue value)
    {
        PdfCancellation.ThrowIfCancelled();
        var copy = Copy(value, 0);
        while (_pending.TryDequeue(out var item))
        {
            PdfCancellation.ThrowIfCancelled();
            var replaced = _overrides.TryGetValue(item.Old, out var replacement);
            _keepParent = replaced && replacement.KeepParent;
            var original = replaced ? PdfValue.FromDictionary(replacement.Dictionary) : StoreReading.GetObject(_source, new(item.Old, 0));
            _target.Set(new(item.New, 0), Copy(original, 0));
            _keepParent = false;
        }

        return copy;
    }

    /// <summary>Gets a source name in the new document's name table.</summary>
    /// <param name="name">The source name.</param>
    /// <returns>The name.</returns>
    public PdfName ImportName(PdfName name) => name.IsKnown ? name : _target.Names.Intern(_source.Names.GetSpelling(name));

    /// <summary>Determines whether a value is a page or a page-tree node.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for a dictionary typed /Page or /Pages.</returns>
    private static bool IsPageTreeNode(PdfValue value) =>
        value.Kind == PdfKind.Dictionary
        && value.AsDictionary()!.GetName(KnownName.Type) is var type
        && (type.Is(KnownName.Page)
        || type.Is(KnownName.Pages));

    /// <summary>Copies a value.</summary>
    /// <param name="value">The source value.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The copy.</returns>
    private PdfValue Copy(PdfValue value, int depth)
    {
        // The writer refuses values nested deeper than the limit.
        return depth > PdfLimits.MaxNesting ? default : value.Kind switch
        {
            PdfKind.Reference => CopyReference(value.AsReference().Number),
            PdfKind.Array => CopyArray(value.AsArray()!, depth),
            PdfKind.Dictionary => PdfValue.FromDictionary(CopyDictionary(value.AsDictionary()!, false, depth)),
            PdfKind.Stream => PdfValue.FromStream(CopyStream(value.AsStream()!, depth)),
            PdfKind.Name => PdfValue.FromName(ImportName(value.AsName())),
            _ => value,
        };
    }

    /// <summary>Copies a reference, reserving a number for the object the first time it is reached.</summary>
    /// <param name="number">The source object number.</param>
    /// <returns>A reference into the new document, or null for a missing or left-out object.</returns>
    private PdfValue CopyReference(int number)
    {
        if ((uint)number >= (uint)_map.Length || number == 0 || _map[number] < 0)
        {
            return default;
        }

        if (_map[number] > 0)
        {
            return PdfValue.FromReference(new(_map[number], 0));
        }

        var value = StoreReading.GetObject(_source, new(number, 0));
        if (value.IsNull || IsPageTreeNode(value))
        {
            _map[number] = -1;
            return default;
        }

        var id = _target.Reserve();
        _map[number] = id.Number;
        MappedCount++;
        _pending.Enqueue(new(number, id.Number));
        return PdfValue.FromReference(id);
    }

    /// <summary>Copies an array.</summary>
    /// <param name="array">The source array.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The copy.</returns>
    private PdfValue CopyArray(PdfArray array, int depth)
    {
        var copy = new PdfArray(null, array.Count);
        foreach (var item in array.Items)
        {
            copy.Add(Copy(item, depth + 1));
        }

        return PdfValue.FromArray(copy);
    }

    /// <summary>Copies a dictionary, leaving out /Parent and, for a stream, /Length.</summary>
    /// <param name="dictionary">The source dictionary.</param>
    /// <param name="isStream">Whether it is a stream dictionary.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The copy.</returns>
    private PdfDictionary CopyDictionary(PdfDictionary dictionary, bool isStream, int depth)
    {
        var copy = new PdfDictionary(null, dictionary.Count);
        for (var i = 0; i < dictionary.Count; i++)
        {
            var key = dictionary.GetKeyAt(i);

            // A form widget keeps its /Parent: its field is a target object mapped before the copy, so the reference stays valid.
            var keepsParent = depth == 0 && _keepParent;
            if ((key.Is(KnownName.Parent) && !keepsParent) || (isStream && key.Is(KnownName.Length)))
            {
                continue;
            }

            var value = Copy(dictionary.GetValueAt(i), depth + 1);
            if (!value.IsNull)
            {
                copy.Add(ImportName(key), value);
            }
        }

        return copy;
    }

    /// <summary>Copies a stream, sharing its data bytes when they are not encrypted.</summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The copy.</returns>
    private PdfStream CopyStream(PdfStream stream, int depth)
    {
        var dictionary = CopyDictionary(stream.Dictionary, true, depth);

        // Data held in memory is shared; data read from a file is copied, so the copy does not depend on that file staying open.
        if (!stream.IsEncrypted && stream.TryGetArray(out var segment) && segment.Array is not null)
        {
            dictionary.Set(KnownName.Length, PdfValue.FromInteger(stream.RawLength));
            return new(dictionary, segment.Array, segment.Offset, segment.Count, default, false);
        }

        using var raw = stream.LeaseRawData();
        return new(dictionary, PdfObjectWriter.PlainData(stream, raw.Span).ToArray());
    }

    /// <inheritdoc/>
    PdfObjectStore IPdfCarrySink.Source => _source;

    /// <inheritdoc/>
    PdfObjectStore? IPdfCarrySink.TargetStore => null;

    /// <inheritdoc/>
    PdfNameTable IPdfCarrySink.TargetNames => _target.Names;

    /// <inheritdoc/>
    PdfDictionary IPdfCarrySink.TargetCatalog => _target.CatalogEntries;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    PdfValue IPdfCarrySink.Import(PdfValue value) => Import(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    PdfName IPdfCarrySink.ImportName(PdfName name) => ImportName(name);

    /// <inheritdoc/>
    int IPdfCarrySink.GetMapped(int sourceNumber) => (uint)sourceNumber < (uint)_map.Length ? _map[sourceNumber] : 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IPdfCarrySink.MapObject(PdfObjectId source, PdfObjectId target) => MapObject(source, target);

    /// <inheritdoc/>
    void IPdfCarrySink.Override(int sourceNumber, PdfDictionary replacement, bool keepParent) => _overrides[sourceNumber] = new(replacement, keepParent);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    PdfObjectId IPdfCarrySink.Reserve() => _target.Reserve();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IPdfCarrySink.Set(PdfObjectId id, PdfValue value) => _target.Set(id, value);

    /// <inheritdoc/>
    void IPdfCarrySink.SetCatalog(PdfDictionary catalog) => _target.CatalogEntries = catalog;

    /// <summary>A dictionary copied in place of a source object.</summary>
    /// <param name="Dictionary">The replacement.</param>
    /// <param name="KeepParent">Whether its top-level <c>/Parent</c> is kept.</param>
    [DebuggerDisplay("ReplacedObject: KeepParent={KeepParent}")]
    private readonly record struct ReplacedObject(PdfDictionary Dictionary, bool KeepParent);

    /// <summary>An object reserved in the new document that is not yet copied.</summary>
    /// <param name="Old">The object number in the source.</param>
    /// <param name="New">The object number in the new document.</param>
    private readonly record struct PendingObject(int Old, int New);
}
