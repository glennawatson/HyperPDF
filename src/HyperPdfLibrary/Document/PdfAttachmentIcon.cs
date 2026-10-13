// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>The icon of a file attachment annotation (ISO 32000-2 12.5.6.15, Table 184 <c>/Name</c>).</summary>
public enum PdfAttachmentIcon
{
    /// <summary>A push pin (<c>PushPin</c>); the default.</summary>
    PushPin = 0,

    /// <summary>A chart (<c>Graph</c>).</summary>
    Graph = 1,

    /// <summary>A paper clip (<c>Paperclip</c>).</summary>
    Paperclip = 2,

    /// <summary>A tag (<c>Tag</c>).</summary>
    Tag = 3,
}
