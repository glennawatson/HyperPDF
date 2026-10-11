// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Documents;

/// <summary>The reason a document failed to open.</summary>
public enum DocumentOpenError
{
    /// <summary>An unknown failure.</summary>
    Unknown = 0,

    /// <summary>The file could not be found or read.</summary>
    File = 1,

    /// <summary>The file is not a valid document.</summary>
    Format = 2,

    /// <summary>A password is required or the supplied password is wrong.</summary>
    Password = 3,

    /// <summary>The document uses an unsupported security handler.</summary>
    Security = 4,

    /// <summary>A recipient certificate or its password is required, or the supplied certificate is wrong.</summary>
    Certificate = 5,
}
