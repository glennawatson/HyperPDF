// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Objects;

/// <summary>What opening a store needs besides the file: the credentials and the open context.</summary>
/// <param name="Password">The password for the standard security handler, or <see langword="null"/>.</param>
/// <param name="Certificate">A recipient's certificate with its private key for the public-key handler, or <see langword="null"/>.</param>
/// <param name="Context">The cancellation and diagnostics context, or <see langword="null"/>.</param>
/// <param name="IsFdf">Whether the file is an FDF file, whose header reads <c>%FDF-</c> where a PDF file's reads <c>%PDF-</c>.</param>
internal readonly record struct OpenCredentials(string? Password, X509Certificate2? Certificate, PdfOpenContext? Context, bool IsFdf)
{
    /// <summary>Initializes a new instance of the <see cref="OpenCredentials"/> struct for a PDF file.</summary>
    /// <param name="password">The password for the standard security handler, or <see langword="null"/>.</param>
    /// <param name="certificate">A recipient's certificate with its private key, or <see langword="null"/>.</param>
    /// <param name="context">The cancellation and diagnostics context, or <see langword="null"/>.</param>
    internal OpenCredentials(string? password, X509Certificate2? certificate, PdfOpenContext? context)
        : this(password, certificate, context, false)
    {
    }
}
