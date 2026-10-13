// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// Builds the document security store (ISO 32000-2, Table 261) by adding to the one the document has: what is already there stays,
/// and a certificate, response or list that is already stored is referenced rather than stored again.
/// </summary>
[DebuggerDisplay("PdfSecurityStoreBuilder")]
internal ref struct PdfSecurityStoreBuilder
{
    /// <summary>The length of a SHA-1 hash in bytes.</summary>
    private const int Sha1Length = 20;

    /// <summary>The bytes reserved to format the time of an entry, more than any PDF date takes.</summary>
    private const int DateBufferLength = 32;

    /// <summary>The store's objects.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The open edit.</summary>
    private readonly PdfEditTransaction _transaction;

    /// <summary>The certificates, with the references stored and a lookup by hash.</summary>
    private PdfStoredKind _certs;

    /// <summary>The OCSP responses, with the references stored and a lookup by hash.</summary>
    private PdfStoredKind _ocsps;

    /// <summary>The revocation lists, with the references stored and a lookup by hash.</summary>
    private PdfStoredKind _crls;

    /// <summary>The validation-related information entries by key; <see langword="null"/> until there is one.</summary>
    private Dictionary<string, PdfDictionary>? _vri;

    /// <summary>Initializes a new instance of the <see cref="PdfSecurityStoreBuilder"/> struct that starts from the store the document has.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="transaction">The open edit.</param>
    internal PdfSecurityStoreBuilder(PdfObjectStore store, PdfEditTransaction transaction)
    {
        _store = store;
        _transaction = transaction;
        var existing = store.Catalog.GetDictionary(KnownName.DSS);
        Load(existing, store.Names.Intern("Certs"u8), ref _certs);
        Load(existing, store.Names.Intern("OCSPs"u8), ref _ocsps);
        Load(existing, store.Names.Intern("CRLs"u8), ref _crls);
        if (existing?.GetDictionary(KnownName.VRI) is not { } vri)
        {
            return;
        }

        for (var i = 0; i < vri.Count; i++)
        {
            if (vri.Get(vri.GetKeyAt(i)).AsDictionary() is not { } entry)
            {
                continue;
            }

            _vri ??= [with(StringComparer.Ordinal)];
            _vri[store.Names.GetString(vri.GetKeyAt(i)).ToUpperInvariant()] = entry.Clone();
        }
    }

    /// <summary>Adds the validation data of a signature.</summary>
    /// <param name="data">The data.</param>
    /// <param name="includeVri">Whether to add a validation-related information entry for the signature.</param>
    /// <param name="time">The time to record in the entry.</param>
    internal void Add(PdfSignatureValidationData data, bool includeVri, DateTimeOffset time)
    {
        var collect = includeVri && data.Contents.Length > 0;
        var certs = Intern(ref _certs, data.Certificates, collect);
        var ocsps = Intern(ref _ocsps, data.OcspResponses, collect);
        var crls = Intern(ref _crls, data.Crls, collect);
        if (!collect)
        {
            return;
        }

        var key = Vri(data.Contents);
        _vri ??= [with(StringComparer.Ordinal)];
        var entry = _vri.TryGetValue(key, out var found) ? found : new PdfDictionary(_store);
        entry.Set(KnownName.Type, PdfValue.FromName(KnownName.VRI));
        Merge(entry, KnownName.Cert, certs);
        Merge(entry, _store.Names.Intern("OCSP"u8), ocsps);
        Merge(entry, _store.Names.Intern("CRL"u8), crls);
        Span<byte> date = stackalloc byte[DateBufferLength];
        entry.Set(KnownName.TU, PdfValue.FromString(date[..PdfDate.Format(time, date)].ToArray()));
        _vri[key] = entry;
    }

    /// <summary>Writes the store into the catalog, replacing the object of an indirect store and adding an object for a direct or new one.</summary>
    internal void Write()
    {
        var existing = _store.Catalog.GetDictionary(KnownName.DSS);
        var dss = existing?.Clone() ?? new PdfDictionary(_store);
        dss.Set(KnownName.Type, PdfValue.FromName(KnownName.DSS));
        Set(dss, _store.Names.Intern("Certs"u8), _certs.References);
        Set(dss, _store.Names.Intern("OCSPs"u8), _ocsps.References);
        Set(dss, _store.Names.Intern("CRLs"u8), _crls.References);
        if (_vri is { Count: > 0 })
        {
            var vri = new PdfDictionary(_store, _vri.Count);
            foreach (var (key, entry) in _vri)
            {
                vri.Set(_store.Names.Intern(key), PdfValue.FromDictionary(entry));
            }

            dss.Set(KnownName.VRI, PdfValue.FromDictionary(vri));
        }

        var raw = _store.Catalog.GetRaw(KnownName.DSS);
        if (raw.IsReference)
        {
            _transaction.Replace(raw.AsReference(), PdfValue.FromDictionary(dss));
            return;
        }

        var catalog = _store.Catalog.CloneWithRoom(1);
        catalog.Set(KnownName.DSS, PdfValue.FromReference(_transaction.Add(PdfValue.FromDictionary(dss))));
        PdfPageTreeWriter.ReplaceCatalog(_transaction, _store, catalog);
    }

    /// <summary>Gives the pooled arrays back.</summary>
    internal void Dispose()
    {
        _certs.Release();
        _ocsps.Release();
        _crls.Release();
    }

    /// <summary>Gets the key of a signature's validation-related information entry: the base-16 SHA-1 hash of its /Contents (ISO 32000-2, Table 261).</summary>
    /// <param name="contents">The /Contents value.</param>
    /// <returns>The upper-case hexadecimal key.</returns>
    private static string Vri(byte[] contents)
    {
        // The format fixes SHA-1 for this key; it only names the entry and protects nothing.
        Span<byte> hash = stackalloc byte[Sha1Length];
        _ = CryptographicOperations.HashData(HashAlgorithmName.SHA1, contents, hash);
        return Convert.ToHexString(hash);
    }

    /// <summary>Reads one array of a store: the references it holds and the hash of each stream's data.</summary>
    /// <param name="dss">The existing store, or <see langword="null"/>.</param>
    /// <param name="key">The array's key.</param>
    /// <param name="kind">The kind to fill with what is already stored; left alone when the store has no such array.</param>
    private static void Load(PdfDictionary? dss, PdfName key, ref PdfStoredKind kind)
    {
        if (dss?.GetArray(key) is not { } array)
        {
            return;
        }

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        for (var i = 0; i < array.Count; i++)
        {
            var raw = array.GetRaw(i);
            kind.AddReference(raw);
            if (array.Get(i).AsStream() is not { } stream)
            {
                continue;
            }

            _ = SHA256.HashData(stream.DecodeToArray(), hash);
            kind.AddHash(hash, raw);
        }
    }

    /// <summary>Sets an array of a store when it holds anything.</summary>
    /// <param name="dss">The store.</param>
    /// <param name="key">The array's key.</param>
    /// <param name="references">The references.</param>
    private static void Set(PdfDictionary dss, PdfName key, ReadOnlySpan<PdfValue> references)
    {
        if (!references.IsEmpty)
        {
            dss.Set(key, PdfValue.FromArray(new(dss.Owner, references)));
        }
    }

    /// <summary>Determines whether an array holds a reference.</summary>
    /// <param name="array">The array.</param>
    /// <param name="reference">The reference.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool Contains(PdfArray array, PdfValue reference)
    {
        for (var i = 0; i < array.Count; i++)
        {
            if (array.GetRaw(i).AsReference() == reference.AsReference())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Adds references to an entry's array, leaving out those already there.</summary>
    /// <param name="entry">The validation-related information entry.</param>
    /// <param name="key">The array's key.</param>
    /// <param name="references">The references to add, or <see langword="null"/> for none.</param>
    private void Merge(PdfDictionary entry, PdfName key, List<PdfValue>? references)
    {
        if (references is not { Count: > 0 })
        {
            return;
        }

        var array = entry.GetArray(key)?.CloneWithRoom(references.Count) ?? new PdfArray(_store, references.Count);
        foreach (var reference in references)
        {
            if (!Contains(array, reference))
            {
                array.Add(reference);
            }
        }

        entry.Set(key, PdfValue.FromArray(array));
    }

    /// <summary>Stores items that are not stored yet and returns the references to all of them.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="items">The DER items.</param>
    /// <param name="collect">Whether the caller wants the references of every item, including those already stored.</param>
    /// <returns>A reference to each item, or <see langword="null"/> when the caller did not ask for them or there are no items.</returns>
    private List<PdfValue>? Intern(ref PdfStoredKind kind, byte[][] items, bool collect)
    {
        if (items.Length == 0)
        {
            return null;
        }

        var references = collect ? new List<PdfValue>(items.Length) : null;
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        foreach (var item in items)
        {
            _ = SHA256.HashData(item, hash);
            if (!kind.TryGetHash(hash, out var reference))
            {
                reference = PdfValue.FromReference(_transaction.Add(PdfValue.FromStream(new(new PdfDictionary(_store, 0), item))));
                kind.AddHash(hash, reference);
                kind.AddReference(reference);
            }

            references?.Add(reference);
        }

        return references;
    }
}
