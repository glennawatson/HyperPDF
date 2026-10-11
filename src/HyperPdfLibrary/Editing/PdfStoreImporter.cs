// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Deep-copies objects from one document into an open document's store, inside a transaction. Every reachable object
/// is copied once and renumbered, so shared objects stay shared and cycles end. Page and page-tree objects are left out
/// unless mapped first, <c>/Parent</c> entries are dropped, and <c>/StructParent</c> and <c>/StructParents</c> are dropped
/// unless <see cref="SetStructParent"/> gave the object a key in the target's parent tree (see
/// <see cref="PdfStructureImporter"/>, which copies the structure elements). Streams are copied decrypted; the writer encrypts them for the
/// target when it is encrypted. Names move to the target's name table, and copies resolve references in the target.
/// Form widgets are the exception to the <c>/Parent</c> rule: <see cref="PdfDocumentCarrier"/> maps their fields first and
/// asks for the widget's <c>/Parent</c> to be kept.
/// </summary>
/// <remarks>Not thread-safe. The source must stay open until the target is saved, as stream data is shared, not copied.</remarks>
[DebuggerDisplay("PdfStoreImporter: {_pending.Count} pending")]
internal sealed class PdfStoreImporter : IPdfCarrySink
{
    /// <summary>The transaction receiving the copies.</summary>
    private readonly PdfEditTransaction _transaction;

    /// <summary>The store receiving the copies.</summary>
    private readonly PdfObjectStore _target;

    /// <summary>The store copied from.</summary>
    private readonly PdfObjectStore _source;

    /// <summary>The new number of each old number: zero when not yet copied, -1 when left out.</summary>
    private readonly int[] _map;

    /// <summary>The objects reserved but not yet copied.</summary>
    private readonly Queue<NumberPair> _pending = new();

    /// <summary>The new <c>/StructParent</c> key of each source object, by source number.</summary>
    private readonly Dictionary<int, int> _structParents = [];

    /// <summary>The replacement dictionaries to copy in place of source objects, by source number.</summary>
    private readonly Dictionary<int, ReplacedObject> _overrides = [];

    /// <summary>The source number of the object being copied, or 0 for a value copied directly.</summary>
    private int _current;

    /// <summary>Whether the top-level <c>/Parent</c> of the dictionary being copied is kept.</summary>
    private bool _keepParent;

    /// <summary>Initializes a new instance of the <see cref="PdfStoreImporter"/> class.</summary>
    /// <param name="transaction">The transaction receiving the copies.</param>
    /// <param name="target">The store receiving the copies.</param>
    /// <param name="source">The store copied from.</param>
    internal PdfStoreImporter(PdfEditTransaction transaction, PdfObjectStore target, PdfObjectStore source)
    {
        _transaction = transaction;
        _target = target;
        _source = source;
        _map = new int[source.Size];
    }

    /// <summary>Makes references to a source object point at an object in the target, such as a reserved page.</summary>
    /// <param name="source">The source object.</param>
    /// <param name="target">The target object.</param>
    internal void MapObject(PdfObjectId source, PdfObjectId target)
    {
        if ((uint)source.Number < (uint)_map.Length && source.Number != 0)
        {
            _map[source.Number] = target.Number;
        }
    }

    /// <summary>Copies a value and everything it refers to.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The value in the target; a reference stays a reference.</returns>
    internal PdfValue Import(PdfValue value)
    {
        PdfCancellation.ThrowIfCancelled();
        _current = 0;
        var copy = Copy(value, 0);
        while (_pending.TryDequeue(out var item))
        {
            PdfCancellation.ThrowIfCancelled();
            _current = item.Source;
            var replaced = _overrides.TryGetValue(item.Source, out var replacement);
            _keepParent = replaced && replacement.KeepParent;
            var original = replaced ? PdfValue.FromDictionary(replacement.Dictionary) : StoreReading.GetObject(_source, new(item.Source, 0));
            _transaction.Replace(new(item.Target, 0), Copy(original, 0));
            _keepParent = false;
        }

        _current = 0;
        return copy;
    }

    /// <summary>Gives a copied object a new <c>/StructParent</c> key instead of dropping the entry.</summary>
    /// <param name="sourceNumber">The source object number, such as an annotation's.</param>
    /// <param name="key">The key in the target's parent tree.</param>
    internal void SetStructParent(int sourceNumber, int key) => _structParents[sourceNumber] = key;

    /// <summary>Gets a source name in the target's name table.</summary>
    /// <param name="name">The source name.</param>
    /// <returns>The name.</returns>
    internal PdfName ImportName(PdfName name) => name.IsKnown ? name : _target.Names.Intern(_source.Names.GetSpelling(name));

    /// <summary>Determines whether a value is a page or a page-tree node.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for a dictionary typed /Page or /Pages.</returns>
    private static bool IsPageTreeNode(PdfValue value) =>
        value.Kind == PdfKind.Dictionary
        && value.AsDictionary()!.GetName(KnownName.Type) is var type
        && (type.Is(KnownName.Page)
        || type.Is(KnownName.Pages));

    /// <summary>Determines whether a key is left out of every copied dictionary.</summary>
    /// <param name="key">The key.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>
    /// <see langword="true"/> for /Parent, /StructParent and /StructParents. A form widget keeps its /Parent: its field is a
    /// target object mapped before the copy, so the reference stays valid.
    /// </returns>
    private bool IsDropped(PdfName key, int depth) => key.Is(KnownName.Parent) ? depth != 0 || !_keepParent : key.Is(KnownName.StructParent) || key.Is(KnownName.StructParents);

    /// <summary>Copies a value.</summary>
    /// <param name="value">The source value.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The copy.</returns>
    private PdfValue Copy(PdfValue value, int depth) => depth > PdfLimits.MaxNesting ? default : value.Kind switch
    {
        PdfKind.Reference => CopyReference(value.AsReference().Number),
        PdfKind.Array => CopyArray(value.AsArray()!, depth),
        PdfKind.Dictionary => PdfValue.FromDictionary(CopyDictionary(value.AsDictionary()!, false, depth)),
        PdfKind.Stream => PdfValue.FromStream(CopyStream(value.AsStream()!, depth)),
        PdfKind.Name => PdfValue.FromName(ImportName(value.AsName())),
        _ => value,
    };

    /// <summary>Copies a reference, reserving a target number the first time the object is reached.</summary>
    /// <param name="number">The source object number.</param>
    /// <returns>A reference in the target, or null for a missing or left-out object.</returns>
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

        var id = _transaction.Add(default);
        _map[number] = id.Number;
        _pending.Enqueue(new(number, id.Number));
        return PdfValue.FromReference(id);
    }

    /// <summary>Copies an array.</summary>
    /// <param name="array">The source array.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The copy.</returns>
    private PdfValue CopyArray(PdfArray array, int depth)
    {
        var copy = new PdfArray(_target, array.Count);
        foreach (var item in array.Items)
        {
            copy.Add(Copy(item, depth + 1));
        }

        return PdfValue.FromArray(copy);
    }

    /// <summary>Copies a dictionary, leaving out the dropped keys and, for a stream, /Length.</summary>
    /// <param name="dictionary">The source dictionary.</param>
    /// <param name="isStream">Whether it is a stream dictionary.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The copy.</returns>
    private PdfDictionary CopyDictionary(PdfDictionary dictionary, bool isStream, int depth)
    {
        var copy = new PdfDictionary(_target, dictionary.Count);
        for (var i = 0; i < dictionary.Count; i++)
        {
            var key = dictionary.GetKeyAt(i);
            if (depth == 0 && key.Is(KnownName.StructParent) && _structParents.TryGetValue(_current, out var structParent))
            {
                copy.Add(key, PdfValue.FromInteger(structParent));
                continue;
            }

            if (IsDropped(key, depth) || (isStream && key.Is(KnownName.Length)))
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
    PdfObjectStore? IPdfCarrySink.TargetStore => _target;

    /// <inheritdoc/>
    PdfNameTable IPdfCarrySink.TargetNames => _target.Names;

    /// <inheritdoc/>
    PdfDictionary IPdfCarrySink.TargetCatalog => _target.Catalog;

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
    PdfObjectId IPdfCarrySink.Reserve() => _transaction.Add(default);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IPdfCarrySink.Set(PdfObjectId id, PdfValue value) => _transaction.Replace(id, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IPdfCarrySink.SetCatalog(PdfDictionary catalog) => PdfPageTreeWriter.ReplaceCatalog(_transaction, _target, catalog);

    /// <summary>A dictionary copied in place of a source object.</summary>
    /// <param name="Dictionary">The replacement.</param>
    /// <param name="KeepParent">Whether its top-level <c>/Parent</c> is kept.</param>
    [DebuggerDisplay("ReplacedObject: KeepParent={KeepParent}")]
    private readonly record struct ReplacedObject(PdfDictionary Dictionary, bool KeepParent);

    /// <summary>A source object and the target number reserved for it.</summary>
    /// <param name="Source">The source number.</param>
    /// <param name="Target">The target number.</param>
    [DebuggerDisplay("NumberPair: {Source} -> {Target}")]
    private readonly record struct NumberPair(int Source, int Target);
}
