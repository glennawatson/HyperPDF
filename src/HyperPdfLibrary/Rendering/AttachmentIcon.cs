// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>The icons a file attachment annotation can have (its <c>/Name</c>, ISO 32000-2 12.5.6.15).</summary>
internal enum AttachmentIcon
{
    /// <summary>A push pin, the default.</summary>
    PushPin = 0,

    /// <summary>A bar graph.</summary>
    Graph = 1,

    /// <summary>A paper clip.</summary>
    Paperclip = 2,

    /// <summary>A tag.</summary>
    Tag = 3,
}
