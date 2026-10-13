// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Flattening;

/// <summary>Which parts of a document a flattened copy turns into plain page content.</summary>
public enum FlattenCopyScope
{
    /// <summary>Form fields only. Comments, highlights and links stay as they are.</summary>
    FormFields = 0,

    /// <summary>Comments, highlights, stamps and other markup only. Form fields and links stay as they are.</summary>
    CommentsAndMarkup = 1,

    /// <summary>Form fields, comments and markup. Links stay clickable.</summary>
    EverythingButLinks = 2,

    /// <summary>Everything that can be drawn, including form fields and the outlines of links.</summary>
    Everything = 3,
}
