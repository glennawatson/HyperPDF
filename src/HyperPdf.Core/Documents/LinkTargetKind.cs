// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>The kind of a <see cref="LinkTarget"/>.</summary>
public enum LinkTargetKind
{
    /// <summary>No target.</summary>
    None = 0,

    /// <summary>A page in the same document.</summary>
    Page = 1,

    /// <summary>An external URI.</summary>
    Uri = 2,

    /// <summary>A page in another PDF file, named by <see cref="LinkTarget.Uri"/> relative to this one.</summary>
    OtherDocument = 3,

    /// <summary>A file to open with another app, named by <see cref="LinkTarget.Uri"/> relative to this document.</summary>
    LaunchFile = 4,

    /// <summary>A document embedded in this one.</summary>
    EmbeddedDocument = 5,
}
