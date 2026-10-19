// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Reads the attributes that matter for reading from an element's <c>/A</c> attribute objects, then from the classes its
/// <c>/C</c> names in the class map. Own attributes override classes; later objects override earlier objects for each owner.
/// </summary>
[DebuggerDisplay("StructureAttributeReader")]
internal sealed class StructureAttributeReader
{
    /// <summary>Checks cancellation once per batch of 64 entries.</summary>
    private const int CancellationCheckMask = 63;

    /// <summary>The length of the longest standard owner excluded from list attributes.</summary>
    private const int OwnerNameLength = 10;

    /// <summary>The standard Layout attribute owner name.</summary>
    private const string LayoutOwnerName = "Layout";

    /// <summary>The standard Table attribute owner name.</summary>
    private const string TableOwnerName = "Table";

    /// <summary>The standard List attribute owner name.</summary>
    private const string ListOwnerName = "List";

    /// <summary>The standard FENote attribute owner name.</summary>
    private const string NoteOwnerName = "FENote";

    /// <summary>The standard owner of non-interactive form attributes.</summary>
    private const string PrintFieldOwnerName = "PrintField";

    /// <summary>The standard owner of artifact attributes.</summary>
    private const string ArtifactOwnerName = "Artifact";

    /// <summary>The internal sentinel distinguishing absence from an explicit None.</summary>
    private const byte UnspecifiedNoteType = byte.MaxValue;

    /// <summary>The interned structure names.</summary>
    private readonly TaggedNames _names;

    /// <summary>The document's name table.</summary>
    private readonly PdfNameTable _table;

    /// <summary>The class map, or <see langword="null"/>.</summary>
    private readonly PdfDictionary? _classMap;

    /// <summary>Whether the element has any attribute object.</summary>
    private bool _found;

    /// <summary>The Layout placement found so far.</summary>
    private string? _placement;

    /// <summary>The Layout bounding box found so far.</summary>
    private PdfRectangle? _box;

    /// <summary>The Table scope found so far.</summary>
    private PdfTableScope? _scope;

    /// <summary>The Table headers found so far.</summary>
    private string[]? _headers;

    /// <summary>The Table row span found so far.</summary>
    private int? _rowSpan;

    /// <summary>The Table column span found so far.</summary>
    private int? _columnSpan;

    /// <summary>The List numbering found so far.</summary>
    private string? _numbering;

    /// <summary>The explicitly specified non-inherited continuation flag.</summary>
    private bool? _continuedList;

    /// <summary>The previous list's source byte string, without a decoded copy.</summary>
    private PdfValue _continuedFrom;

    /// <summary>The non-inherited non-interactive form attributes selected for the current element.</summary>
    private PdfPrintFieldAttributes _printField;

    /// <summary>The Table summary found so far.</summary>
    private string? _summary;

    /// <summary>The note subtype found so far, including explicit None.</summary>
    private byte _noteType = UnspecifiedNoteType;

    /// <summary>Initializes a new instance of the <see cref="StructureAttributeReader"/> class.</summary>
    /// <param name="names">The interned structure names.</param>
    /// <param name="table">The document's name table.</param>
    /// <param name="classMap">The structure tree root's <c>/ClassMap</c>, or <see langword="null"/>.</param>
    internal StructureAttributeReader(TaggedNames names, PdfNameTable table, PdfDictionary? classMap)
    {
        _names = names;
        _table = table;
        _classMap = classMap;
    }

    /// <summary>Gets the subtype from the most recently read element, without inheritance.</summary>
    internal PdfNoteType NoteType => _noteType == UnspecifiedNoteType ? PdfNoteType.None : (PdfNoteType)_noteType;

    /// <summary>Gets the standard Layout owner's UTF-8 spelling.</summary>
    private static ReadOnlySpan<byte> LayoutOwner => "Layout"u8;

    /// <summary>Gets the standard Table owner's UTF-8 spelling.</summary>
    private static ReadOnlySpan<byte> TableOwner => "Table"u8;

    /// <summary>Gets the standard List owner's UTF-8 spelling.</summary>
    private static ReadOnlySpan<byte> ListOwner => "List"u8;

    /// <summary>Gets the standard FENote owner's UTF-8 spelling.</summary>
    private static ReadOnlySpan<byte> NoteOwner => "FENote"u8;

    /// <summary>Gets the standard PrintField owner's UTF-8 spelling.</summary>
    private static ReadOnlySpan<byte> PrintFieldOwner => "PrintField"u8;

    /// <summary>Gets the standard Artifact owner's UTF-8 spelling.</summary>
    private static ReadOnlySpan<byte> ArtifactOwner => "Artifact"u8;

    /// <summary>Reads an element's attributes.</summary>
    /// <param name="element">The element's dictionary.</param>
    /// <returns>The attributes; <see cref="PdfStructureAttributes.None"/> when it gives none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfStructureAttributes Read(PdfDictionary element) => Read(element, CancellationToken.None);

    /// <summary>Reads attributes with cancellation between attribute objects and entry batches.</summary>
    /// <param name="element">The element's dictionary.</param>
    /// <param name="cancellationToken">Cancels attribute decoding.</param>
    /// <returns>The effective attribute snapshot.</returns>
    internal PdfStructureAttributes Read(PdfDictionary element, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _noteType = UnspecifiedNoteType;
        var own = element.Get(KnownName.A);
        var classes = element.Get(KnownName.C);
        if (own.IsNull && (classes.IsNull || _classMap is null))
        {
            return PdfStructureAttributes.None;
        }

        Reset();
        ApplyAll(own, cancellationToken);
        ApplyClasses(classes, cancellationToken);
        return Build();
    }

    /// <summary>Reads a positive span, using the default for invalid non-positive values.</summary>
    /// <param name="attributes">The attribute dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The span, or <see langword="null"/> when not given.</returns>
    private static int? ReadSpan(PdfDictionary attributes, PdfName key)
    {
        var value = attributes.Get(key);
        return value.IsNumber ? Math.Max(value.AsInt32(), 1) : null;
    }

    /// <summary>Reads the ids of a cell's header cells.</summary>
    /// <param name="array">The <c>/Headers</c> array of byte strings.</param>
    /// <param name="cancellationToken">Cancels header decoding.</param>
    /// <returns>The ids, or <see langword="null"/> when not given.</returns>
    private static string[]? ReadHeaders(PdfArray? array, CancellationToken cancellationToken)
    {
        if (array is null)
        {
            return null;
        }

        var ids = array.Count == 0 ? [] : new string[array.Count];
        var written = 0;
        for (var i = 0; i < array.Count; i++)
        {
            if ((i & CancellationCheckMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var item = array.Get(i);
            if (item.Kind != PdfKind.String)
            {
                continue;
            }

            ids[written] = PdfText.Decode(item.AsStringBytes());
            written++;
        }

        if (written != ids.Length)
        {
            Array.Resize(ref ids, written);
        }

        return ids;
    }

    /// <summary>Recognises standard owners and uses NSO to identify other valid namespace owners without allocating.</summary>
    /// <param name="bytes">The encoded namespace name.</param>
    /// <returns>The owner spelling, NSO for private namespaces, or empty for an absent name.</returns>
    private static ReadOnlySpan<byte> ReadNamespaceOwner(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return [];
        }

        if (PdfText.GetDecodedLength(bytes) > OwnerNameLength)
        {
            return "NSO"u8;
        }

        Span<char> buffer = stackalloc char[OwnerNameLength];
        var text = buffer[..PdfText.Decode(bytes, buffer)];
        if (text.IsEmpty)
        {
            return [];
        }

        if (text.SequenceEqual(LayoutOwnerName))
        {
            return LayoutOwner;
        }

        if (text.SequenceEqual(TableOwnerName))
        {
            return TableOwner;
        }

        if (text.SequenceEqual(ListOwnerName))
        {
            return ListOwner;
        }

        if (text.SequenceEqual(NoteOwnerName))
        {
            return NoteOwner;
        }

        if (text.SequenceEqual(PrintFieldOwnerName))
        {
            return PrintFieldOwner;
        }

        return text.SequenceEqual(ArtifactOwnerName) ? ArtifactOwner : "NSO"u8;
    }

    /// <summary>Applies the List owner exclusions in ISO 32000-2, 14.8.5.5, including private owners.</summary>
    /// <param name="owner">The resolved owner spelling, or empty for an invalid owner.</param>
    /// <returns>Whether this owner can define standard list attributes.</returns>
    private static bool AllowsListAttributes(ReadOnlySpan<byte> owner) =>
        !owner.IsEmpty
        && !owner.SequenceEqual(LayoutOwner)
        && !owner.SequenceEqual(TableOwner)
        && !owner.SequenceEqual(PrintFieldOwner)
        && !owner.SequenceEqual(ArtifactOwner);

    /// <summary>Applies the owner exclusions in ISO 32000-2, 14.8.5.6.</summary>
    /// <param name="owner">The resolved owner spelling, or empty when invalid.</param>
    /// <returns>Whether this owner permits non-interactive field attributes.</returns>
    private static bool AllowsPrintFieldAttributes(ReadOnlySpan<byte> owner) =>
        !owner.IsEmpty
        && !owner.SequenceEqual(LayoutOwner)
        && !owner.SequenceEqual(ListOwner)
        && !owner.SequenceEqual(TableOwner)
        && !owner.SequenceEqual(ArtifactOwner);

    /// <summary>Clears what the previous element set.</summary>
    private void Reset()
    {
        _found = false;
        _placement = null;
        _box = null;
        _scope = null;
        _headers = null;
        _rowSpan = null;
        _columnSpan = null;
        _numbering = null;
        _continuedList = null;
        _continuedFrom = default;
        _printField = default;
        _summary = null;
    }

    /// <summary>Makes the attributes from what was found.</summary>
    /// <returns>The attributes.</returns>
    private PdfStructureAttributes Build() => !_found
        ? PdfStructureAttributes.None
        : new(_placement ?? string.Empty, _box, _scope ?? PdfTableScope.None, _headers ?? [], _rowSpan ?? 1, _columnSpan ?? 1, _numbering ?? string.Empty, _summary ?? string.Empty)
        {
            ContinuedList = _continuedList,
            ContinuedFromValue = _continuedFrom,
            PrintField = _printField,
        };

    /// <summary>Applies the classes an element names.</summary>
    /// <param name="classes">The <c>/C</c> value: a name, or an array of names and revision numbers.</param>
    /// <param name="cancellationToken">Cancels class decoding.</param>
    private void ApplyClasses(PdfValue classes, CancellationToken cancellationToken)
    {
        if (_classMap is null)
        {
            return;
        }

        if (classes.TryGetName(out var single))
        {
            ApplyAll(_classMap.Get(single), cancellationToken);
            return;
        }

        if (classes.AsArray() is not { } array)
        {
            return;
        }

        for (var i = array.Count - 1; i >= 0; i--)
        {
            if ((i & CancellationCheckMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (array.Get(i).TryGetName(out var name))
            {
                ApplyAll(_classMap.Get(name), cancellationToken);
            }
        }
    }

    /// <summary>Applies an attribute object, or each one of an array of them; revision numbers in the array are skipped.</summary>
    /// <param name="value">A dictionary, a stream or an array.</param>
    /// <param name="cancellationToken">Cancels attribute decoding.</param>
    private void ApplyAll(PdfValue value, CancellationToken cancellationToken)
    {
        if (value.AsArray() is { } array)
        {
            for (var i = array.Count - 1; i >= 0; i--)
            {
                if ((i & CancellationCheckMask) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (array.Get(i).AsDictionary() is { } item)
                {
                    Apply(item, cancellationToken);
                }
            }

            return;
        }

        if (value.AsDictionary() is { } dictionary)
        {
            Apply(dictionary, cancellationToken);
        }
    }

    /// <summary>Applies one attribute object.</summary>
    /// <param name="attributes">The attribute dictionary.</param>
    /// <param name="cancellationToken">Cancels attribute decoding.</param>
    private void Apply(PdfDictionary attributes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var unowned = attributes.Get(KnownName.O).IsNull;
        var owner = ReadOwner(attributes);
        if (unowned || owner.SequenceEqual(LayoutOwner))
        {
            ApplyLayout(attributes);
        }

        if (unowned || owner.SequenceEqual(TableOwner))
        {
            ApplyTable(attributes, cancellationToken);
        }

        if (unowned || AllowsListAttributes(owner))
        {
            ApplyList(attributes, cancellationToken);
        }

        if (unowned || owner.SequenceEqual(NoteOwner))
        {
            ApplyNote(attributes, cancellationToken);
        }

        ApplyPrintField(attributes, unowned, owner, cancellationToken);
    }

    /// <summary>Retains permitted non-interactive field attributes without inheriting parent values.</summary>
    /// <param name="attributes">The attribute object.</param>
    /// <param name="unowned">Whether the required owner is missing and can be recovered.</param>
    /// <param name="owner">The resolved owner spelling.</param>
    /// <param name="cancellationToken">Cancels attribute lookup.</param>
    private void ApplyPrintField(PdfDictionary attributes, bool unowned, ReadOnlySpan<byte> owner, CancellationToken cancellationToken)
    {
        if (!unowned && !AllowsPrintFieldAttributes(owner))
        {
            return;
        }

        var printField = PrintFieldAttributeReader.Read(attributes, _table, _printField, cancellationToken);
        if (printField == default)
        {
            return;
        }

        _found = true;
        _printField = printField;
    }

    /// <summary>Reads an owner's spelling, resolving NSO through its namespace dictionary.</summary>
    /// <param name="attributes">The attribute object.</param>
    /// <returns>The owner bytes, or an empty span for an invalid owner.</returns>
    private ReadOnlySpan<byte> ReadOwner(PdfDictionary attributes)
    {
        if (!attributes.Get(KnownName.O).TryGetName(out var owner))
        {
            return [];
        }

        if (!_table.NameEquals(owner, "NSO"u8))
        {
            return _table.GetSpelling(owner);
        }

        // ISO 32000-2, 14.7.4 makes matching namespace names equivalent to standard attribute owners.
        return attributes.GetDictionary(_names.NS) is { } ns ? ReadNamespaceOwner(ns.Get(_names.NS).AsStringBytes()) : [];
    }

    /// <summary>Applies Layout-owned reading attributes.</summary>
    /// <param name="attributes">The attribute object.</param>
    private void ApplyLayout(PdfDictionary attributes)
    {
        _found = true;
        _placement ??= ReadName(attributes, _names.Placement);
        if (_box is null && attributes.TryGetRectangle(KnownName.BBox, out var box))
        {
            _box = box;
        }
    }

    /// <summary>Applies Table-owned reading attributes.</summary>
    /// <param name="attributes">The attribute object.</param>
    /// <param name="cancellationToken">Cancels header decoding.</param>
    private void ApplyTable(PdfDictionary attributes, CancellationToken cancellationToken)
    {
        _found = true;
        _summary ??= attributes.GetText(_names.Summary);
        _scope ??= ReadScope(attributes);
        _headers ??= ReadHeaders(attributes.GetArray(_names.Headers), cancellationToken);
        _rowSpan ??= ReadSpan(attributes, _names.RowSpan);
        _columnSpan ??= ReadSpan(attributes, _names.ColSpan);
    }

    /// <summary>Applies List-owned reading attributes.</summary>
    /// <param name="attributes">The attribute object.</param>
    /// <param name="cancellationToken">Cancels continuation lookup.</param>
    private void ApplyList(PdfDictionary attributes, CancellationToken cancellationToken)
    {
        _found = true;
        _numbering ??= ReadName(attributes, _names.ListNumbering);
        if (_continuedList is not null && !_continuedFrom.IsNull)
        {
            return;
        }

        for (var index = 0; index < attributes.Count; index++)
        {
            if ((index & CancellationCheckMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            ApplyListContinuation(attributes, attributes.GetKeyAt(index));
        }
    }

    /// <summary>Retains the nearest valid continuation values independently, without interning absent keys.</summary>
    /// <param name="attributes">The list attribute object.</param>
    /// <param name="key">The entry being inspected.</param>
    private void ApplyListContinuation(PdfDictionary attributes, PdfName key)
    {
        if (_continuedList is null && _table.NameEquals(key, "ContinuedList"u8))
        {
            var value = attributes.Get(key);
            if (value.Kind == PdfKind.Boolean)
            {
                _continuedList = value.AsBoolean();
            }
        }
        else if (_continuedFrom.IsNull && _table.NameEquals(key, "ContinuedFrom"u8))
        {
            var value = attributes.Get(key);
            if (value.Kind == PdfKind.String)
            {
                _continuedFrom = value;
            }
        }
    }

    /// <summary>Applies a recognised FENote subtype without overriding a later or own value.</summary>
    /// <param name="attributes">The attribute object.</param>
    /// <param name="cancellationToken">Cancels subtype lookup.</param>
    private void ApplyNote(PdfDictionary attributes, CancellationToken cancellationToken)
    {
        if (_noteType == UnspecifiedNoteType)
        {
            _noteType = ReadNoteType(attributes, cancellationToken);
        }
    }

    /// <summary>Reads a name value as text.</summary>
    /// <param name="attributes">The attribute dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The name, or <see langword="null"/>.</returns>
    private string? ReadName(PdfDictionary attributes, PdfName key) =>
        attributes.Get(key).TryGetName(out var name) ? _table.GetString(name) : null;

    /// <summary>Reads a recognised note subtype; malformed or private values remain unspecified.</summary>
    /// <param name="attributes">The FENote attribute object.</param>
    /// <param name="cancellationToken">Cancels subtype lookup.</param>
    /// <returns>The subtype, or the absence sentinel when no recognised name is given.</returns>
    private byte ReadNoteType(PdfDictionary attributes, CancellationToken cancellationToken)
    {
        var type = ReadNoteTypeName(attributes, cancellationToken);
        if (type.IsNone)
        {
            return UnspecifiedNoteType;
        }

        if (_table.NameEquals(type, "Footnote"u8))
        {
            return (byte)PdfNoteType.Footnote;
        }

        if (_table.NameEquals(type, "Endnote"u8))
        {
            return (byte)PdfNoteType.Endnote;
        }

        return _table.NameEquals(type, "None"u8) ? (byte)PdfNoteType.None : UnspecifiedNoteType;
    }

    /// <summary>Finds the subtype name without storing a key or interning an absent attribute.</summary>
    /// <param name="attributes">The FENote attribute object.</param>
    /// <param name="cancellationToken">Cancels subtype lookup.</param>
    /// <returns>The subtype name, or the empty name for an absent or malformed value.</returns>
    private PdfName ReadNoteTypeName(PdfDictionary attributes, CancellationToken cancellationToken)
    {
        for (var index = 0; index < attributes.Count; index++)
        {
            if ((index & CancellationCheckMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var key = attributes.GetKeyAt(index);
            if (_table.NameEquals(key, "NoteType"u8))
            {
                return attributes.Get(key).AsName();
            }
        }

        return default;
    }

    /// <summary>Reads a header cell's scope.</summary>
    /// <param name="attributes">The attribute dictionary.</param>
    /// <returns>The scope, or <see langword="null"/> when not given.</returns>
    private PdfTableScope? ReadScope(PdfDictionary attributes)
    {
        if (!attributes.Get(_names.Scope).TryGetName(out var scope))
        {
            return null;
        }

        if (scope == _names.Row)
        {
            return PdfTableScope.Row;
        }

        if (scope == _names.Column)
        {
            return PdfTableScope.Column;
        }

        return scope == _names.Both ? PdfTableScope.Both : null;
    }
}
