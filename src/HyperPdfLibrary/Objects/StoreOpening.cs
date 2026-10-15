// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Objects;

/// <summary>Opens object stores and authenticates their security handlers.</summary>
public static class StoreOpening
{
    /// <summary>How far into the file the header may appear; some writers put junk first.</summary>
    internal const int HeaderSearchLength = 1024;

    /// <summary>The longest version string read from the header.</summary>
    internal const int MaxVersionLength = 3;

    /// <summary>Gets the header signature.</summary>
    private static ReadOnlySpan<byte> HeaderSignature => "%PDF-"u8;

    /// <summary>Gets the header signature of an FDF file.</summary>
    private static ReadOnlySpan<byte> FdfSignature => "%FDF-"u8;

    /// <summary>Opens a file's objects.</summary>
    /// <param name = "file">The file's bytes; kept, not copied.</param>
    /// <param name = "password">The password, or <see langword="null"/>.</param>
    /// <returns>The objects.</returns>
    /// <exception cref = "PdfException">The file is not a readable PDF, or the password is wrong.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfObjectStore Open(byte[] file, string? password) => StoreOpening.Open(file, password, null, PdfOpenContext.Create(PdfOpenOptions.Default));

    /// <summary>Opens a file's objects with options.</summary>
    /// <param name = "file">The file's bytes; kept, not copied.</param>
    /// <param name = "options">The options.</param>
    /// <returns>The objects.</returns>
    /// <exception cref = "PdfException">The file is not a readable PDF, or neither the password nor the certificate opens it.</exception>
    /// <exception cref = "OperationCanceledException">The options' token was cancelled.</exception>
    public static PdfObjectStore OpenWith(byte[] file, PdfOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(options);
        return StoreOpening.Open(new MemoryPdfByteSource(file), true, new(options.Password, options.Certificate, PdfOpenContext.Create(options)));
    }

    /// <summary>Opens the objects of a file read from a source.</summary>
    /// <param name = "source">The file.</param>
    /// <param name = "ownsSource">Whether the store disposes the source, including when opening fails.</param>
    /// <param name = "options">The options.</param>
    /// <returns>The objects.</returns>
    /// <exception cref = "PdfException">The file is not a readable PDF, or neither the password nor the certificate opens it.</exception>
    /// <exception cref = "OperationCanceledException">The options' token was cancelled.</exception>
    public static PdfObjectStore OpenWith(PdfByteSource source, bool ownsSource, PdfOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        return StoreOpening.Open(source, ownsSource, new(options.Password, options.Certificate, PdfOpenContext.Create(options)));
    }

    /// <summary>Opens the objects of a file read from a source, with a token that replaces the options' token.</summary>
    /// <param name = "source">The file.</param>
    /// <param name = "ownsSource">Whether the store disposes the source, including when opening fails.</param>
    /// <param name = "options">The options.</param>
    /// <param name = "cancellationToken">The token for the open and later decoding of the document's streams.</param>
    /// <returns>The objects.</returns>
    /// <exception cref = "PdfException">The file is not a readable PDF, or neither the password nor the certificate opens it.</exception>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    internal static PdfObjectStore OpenWith(PdfByteSource source, bool ownsSource, PdfOpenOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        return StoreOpening.Open(source, ownsSource, new(options.Password, options.Certificate, PdfOpenContext.Create(options, cancellationToken)));
    }

    /// <summary>Opens a file's objects.</summary>
    /// <param name = "file">The file's bytes; kept, not copied.</param>
    /// <param name = "password">The password for the standard security handler, or <see langword="null"/>.</param>
    /// <param name = "certificate">A recipient's certificate with its private key for the public-key handler, or <see langword="null"/>.</param>
    /// <param name = "context">The cancellation and diagnostics context, or <see langword="null"/>.</param>
    /// <returns>The objects.</returns>
    /// <exception cref = "PdfException">The file is not a readable PDF, or neither the password nor the certificate opens it.</exception>
    internal static PdfObjectStore Open(byte[] file, string? password, X509Certificate2? certificate, PdfOpenContext? context)
    {
        ArgumentNullException.ThrowIfNull(file);
        return StoreOpening.Open(new MemoryPdfByteSource(file), true, new(password, certificate, context));
    }

    /// <summary>Opens the objects of a file read from a source, disposing an owned source when opening fails.</summary>
    /// <param name = "source">The file.</param>
    /// <param name = "ownsSource">Whether the store owns the source.</param>
    /// <param name = "credentials">The password, certificate and context.</param>
    /// <returns>The objects.</returns>
    /// <exception cref = "PdfException">The file is not a readable PDF, or neither the password nor the certificate opens it.</exception>
    internal static PdfObjectStore Open(PdfByteSource source, bool ownsSource, in OpenCredentials credentials)
    {
        var store = new PdfObjectStore(source, ownsSource, new XrefTable());
        try
        {
            store.Context = credentials.Context;
            StoreOpening.ReadHeader(store, credentials.IsFdf);
            StoreOpening.ReadStructure(store);
            StoreOpening.Authenticate(store, credentials.Password, credentials.Certificate);
            store.Catalog = StoreRepairs.FindCatalog(store) ?? throw new PdfException(PdfError.Format, "The document has no catalog.");
            return store;
        }
        catch
        {
            store.Dispose();
            throw;
        }
    }

    /// <summary>Opens the objects of an FDF file, which are written as a PDF file's are but start with <c>%FDF-</c>.</summary>
    /// <param name = "file">The file's bytes; kept, not copied.</param>
    /// <returns>The objects.</returns>
    /// <exception cref = "PdfException">The file is not a readable FDF file.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfObjectStore OpenFdf(byte[] file) =>
        StoreOpening.Open(new MemoryPdfByteSource(file), true, new(null, null, PdfOpenContext.Create(PdfOpenOptions.Default), true));

    /// <summary>Reads the version digits after the header signature.</summary>
    /// <param name = "bytes">The bytes after "%PDF-".</param>
    /// <returns>The version, for example "1.7".</returns>
    internal static string ReadVersion(ReadOnlySpan<byte> bytes)
    {
        var length = 0;
        while (length < Math.Min(bytes.Length, StoreOpening.MaxVersionLength) && bytes[length] is (>= (byte)'0' and <= (byte)'9') or (byte)'.')
        {
            length++;
        }

        // Every version the standards define is a shared constant; only an unusual header allocates.
        return bytes[..length] switch
        {
            [(byte)'1', (byte)'.', (byte)'0'] => "1.0",
            [(byte)'1', (byte)'.', (byte)'1'] => "1.1",
            [(byte)'1', (byte)'.', (byte)'2'] => "1.2",
            [(byte)'1', (byte)'.', (byte)'3'] => "1.3",
            [(byte)'1', (byte)'.', (byte)'4'] => "1.4",
            [(byte)'1', (byte)'.', (byte)'5'] => "1.5",
            [(byte)'1', (byte)'.', (byte)'6'] => "1.6",
            [(byte)'1', (byte)'.', (byte)'7'] => "1.7",
            [(byte)'2', (byte)'.', (byte)'0'] => "2.0",
            _ => Encoding.UTF8.GetString(bytes[..length]),
        };
    }

    /// <summary>Throws the exception for a failed authentication.</summary>
    /// <param name = "result">The outcome.</param>
    /// <exception cref = "PdfException">The outcome is not success.</exception>
    internal static void ThrowIfFailed(PdfSecurityResult result)
    {
        switch (result)
        {
            case PdfSecurityResult.UnsupportedHandler:
                {
                    throw new PdfException(PdfError.Security, "The document uses an unsupported security handler.");
                }

            case PdfSecurityResult.UnsupportedCipher:
                {
                    throw new PdfException(PdfError.Security, "The document uses AES-GCM encryption (ISO/TS 32003), which this computer's cryptography library does not support.");
                }

            case PdfSecurityResult.WrongPassword:
                {
                    throw new PdfException(PdfError.Password, "The document is password protected.");
                }

            case PdfSecurityResult.CertificateRequired:
                {
                    throw new PdfException(PdfError.Certificate, "The document is encrypted for certificates; open it with a recipient's certificate and private key.");
                }

            case PdfSecurityResult.Damaged:
                {
                    throw new PdfException(PdfError.Format, "The document's encryption dictionary is damaged.");
                }

            case PdfSecurityResult.PrivateKeyRequired:
                {
                    throw new PdfException(PdfError.Certificate, "This certificate can open the document, but its private key is missing. Choose a certificate file that includes the private key.");
                }

            case PdfSecurityResult.UnsupportedRecipient:
                {
                    throw new PdfException(PdfError.Certificate, "This certificate can open the document, but the document locks it with a key type or encryption method that is not supported.");
                }

            case PdfSecurityResult.RecipientDamaged:
                {
                    throw new PdfException(PdfError.Certificate, "The document's certificate encryption data is damaged, or it does not match this certificate's private key.");
                }

            default:
                {
                    return;
                }
        }
    }

    /// <summary>Finds the <c>%PDF-</c> header near the start of the file and reads the version after it.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "isFdf">Whether to look for the FDF header <c>%FDF-</c>, which has the length of the PDF one.</param>
    /// <exception cref = "PdfException">There is no header.</exception>
    internal static void ReadHeader(PdfObjectStore self, bool isFdf)
    {
        using var start = self.Source.Lease(0, (int)Math.Min(self.Source.Length, StoreParsing.InitialWindow));
        var span = start.Span;
        var signature = isFdf ? StoreOpening.FdfSignature : StoreOpening.HeaderSignature;
        var header = span[..Math.Min(span.Length, StoreOpening.HeaderSearchLength)].IndexOf(signature);
        if (header < 0)
        {
            throw new PdfException(PdfError.Format, "The file is not a PDF document.");
        }

        self.HeaderOffset = header;
        self.MightBeLinearized = header == 0 && span.IndexOf("/Linearized"u8) >= 0;
        if (header > 0)
        {
            PdfOpenContext.Report(self.Context, PdfDiagnosticCode.HeaderOffset, "The PDF header is not at the start of the file.", 0, header);
        }

        self.Version = StoreOpening.ReadVersion(span[(header + signature.Length)..]);
    }

    /// <summary>Reads the cross-reference sections, rebuilding them by scanning when they are damaged.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <exception cref = "PdfException">No trailer could be found.</exception>
    internal static void ReadStructure(PdfObjectStore self)
    {
        // The store is new and its table is still empty, so the sections are read into it.
        var table = self.XrefState;
        if (!XrefReader.TryRead(self.Source, self, table) || !StoreOpening.RootResolves(self, table))
        {
            if (self.Context is { Recovery: false })
            {
                throw new PdfException(PdfError.Format, "The cross-reference table is missing or damaged and recovery is off.");
            }

            self.RepairTriedState = true;
            PdfOpenContext.Report(self.Context, PdfDiagnosticCode.XrefRebuilt, "The cross-reference table was missing or wrong and was rebuilt.", 0, -1);
            var rebuilt = XrefRepair.Rebuild(self.Source, self);
            rebuilt.Trailer ??= table.Trailer;
            self.XrefState = rebuilt;
        }

        if (self.XrefState.Trailer is null)
        {
            throw new PdfException(PdfError.Format, "The document is too damaged to read.");
        }

        // Objects the check of /Root parsed stay cached when the table was read as written and nothing is encrypted; a
        // rebuilt table or a key that is not known yet could make them wrong, so those start again.
        if (self.XrefState.Repaired || self.XrefState.Trailer!.ContainsKey(KnownName.Encrypt))
        {
            StoreOpening.ResetCache(self, self.XrefState.Size);
        }
        else
        {
            StoreCache.Grow(ref self.CacheState, self.XrefState.Size);
        }

        self.NextNumberState = self.XrefState.Size;
    }

    /// <summary>Checks that a table's /Root points at a dictionary.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "table">The table.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool RootResolves(PdfObjectStore self, XrefTable table)
    {
        var root = table.Trailer?.GetRaw(KnownName.Root) ?? default;
        if (!root.IsReference)
        {
            return root.Kind == PdfKind.Dictionary;
        }

        // An encrypted file's object streams cannot be read before the key is known, so trust an entry pointing into one.
        var number = root.AsReference().Number;
        if (table.GetType(number) == XrefEntryType.Compressed && table.Trailer!.ContainsKey(KnownName.Encrypt))
        {
            return true;
        }

        StoreOpening.ResetCache(self, table.Size);
        var value = StoreReading.Load(self, number);
        if (value.Kind != PdfKind.Dictionary)
        {
            return false;
        }

        if ((uint)number < (uint)self.CacheState.Length)
        {
            self.CacheState[number] = StoreReading.Box(value);
        }

        return true;
    }

    /// <summary>Empties the parsed object cache, or replaces it when the table has a different size.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "size">The number of object slots wanted.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ResetCache(PdfObjectStore self, int size) => StoreCache.Reset(ref self.CacheState, size);

    /// <summary>Creates the security handler when the document is encrypted.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "password">The password.</param>
    /// <param name = "certificate">A recipient's certificate, or <see langword="null"/>.</param>
    /// <exception cref = "PdfException">The password or certificate does not open the document, or the security handler is unsupported.</exception>
    internal static void Authenticate(PdfObjectStore self, string? password, X509Certificate2? certificate)
    {
        var encrypt = self.Trailer.GetDictionary(KnownName.Encrypt);
        if (encrypt is null)
        {
            return;
        }

        var firstId = self.Trailer.GetArray(KnownName.ID) is { } ids ? ids.Get(0).AsStringBytes() : [];
        var result = PdfSecurityHandler.TryCreate(encrypt, firstId, password, certificate, out var handler);
        StoreOpening.ThrowIfFailed(result);
        StoreOpening.Adopt(self, handler, encrypt);
    }

    /// <summary>Starts decrypting a store that read its structure without a key, and parses again what it parsed before.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "handler">The security handler with the key.</param>
    /// <param name = "encrypt">The /Encrypt dictionary parsed without a key.</param>
    internal static void Adopt(PdfObjectStore self, PdfSecurityHandler? handler, PdfDictionary encrypt)
    {
        self.Security = handler;

        // Objects parsed before the key was known hold encrypted strings; parse them again.
        StoreOpening.ResetCache(self, self.CacheState.Length);
        self.ObjectStreamsIfCreated?.Clear();
        StoreRepairs.IndexCompressedAfterAuthentication(self);
        if (!self.Trailer.GetRaw(KnownName.Encrypt).IsReference)
        {
            return;
        }

        // The /Encrypt dictionary's own strings are never encrypted; keep the copy parsed without a key.
        var reference = self.Trailer.GetRaw(KnownName.Encrypt).AsReference().Number;
        if ((uint)reference < (uint)self.CacheState.Length)
        {
            self.CacheState[reference] = encrypt;
        }
    }
}
