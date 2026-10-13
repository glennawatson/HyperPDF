// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary;

/// <summary>Why a document could not be opened.</summary>
public enum PdfError
{
    /// <summary>An unexpected failure.</summary>
    Unknown = 0,

    /// <summary>The file could not be read.</summary>
    File = 1,

    /// <summary>The file is not a PDF, or is too damaged to read.</summary>
    Format = 2,

    /// <summary>A password is needed, or the one given is wrong.</summary>
    Password = 3,

    /// <summary>The document uses an unsupported security handler.</summary>
    Security = 4,

    /// <summary>
    /// The document is encrypted for certificates, and the certificate given cannot open it: none was given, it is not a
    /// recipient, its private key is missing, its recipient uses an unsupported algorithm, or its recipient data is damaged.
    /// The message says which.
    /// </summary>
    Certificate = 5,
}
