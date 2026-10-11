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
    internal static PdfObjectStore Open(PdfByteSource source, bool ownsSource, OpenCredentials credentials)
    {
        var store = new PdfObjectStore(source, ownsSource, new XrefTable());
        try
        {
            store.Context = credentials.Context;
            StoreOpening.ReadHeader(store);
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

        return Encoding.UTF8.GetString(bytes[..length]);
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

            default:
                {
                    return;
                }
        }
    }

    /// <summary>Finds the <c>%PDF-</c> header near the start of the file and reads the version after it.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <exception cref = "PdfException">There is no header.</exception>
    internal static void ReadHeader(PdfObjectStore self)
    {
        using var start = self.Source.Lease(0, (int)Math.Min(self.Source.Length, StoreParsing.InitialWindow));
        var span = start.Span;
        var header = span[..Math.Min(span.Length, StoreOpening.HeaderSearchLength)].IndexOf(StoreOpening.HeaderSignature);
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

        self.Version = StoreOpening.ReadVersion(span[(header + StoreOpening.HeaderSignature.Length)..]);
    }

    /// <summary>Reads the cross-reference sections, rebuilding them by scanning when they are damaged.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <exception cref = "PdfException">No trailer could be found.</exception>
    internal static void ReadStructure(PdfObjectStore self)
    {
        var table = new XrefTable();
        self.XrefState = table;
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

        self.CacheState = new object?[self.XrefState.Size];
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

        self.CacheState = new object?[table.Size];
        return StoreReading.Load(self, number).Kind == PdfKind.Dictionary;
    }

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
        self.Security = handler;

        // Objects parsed before the key was known hold encrypted strings; parse them again.
        self.CacheState = new object?[self.CacheState.Length];
        self.ObjectStreamsState.Clear();
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
