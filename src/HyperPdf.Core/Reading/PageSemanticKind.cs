// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Reading;

/// <summary>What a page semantic item is: something assistive technology needs that the reading blocks leave out.</summary>
public enum PageSemanticKind
{
    /// <summary>A table cell the document's tags give no content, so a reader can say the cell is empty instead of skipping it.</summary>
    EmptyCell = 0,

    /// <summary>Content the document marks as an artifact, such as a running header or a page number, which is not part of what is read.</summary>
    Artifact = 1,

    /// <summary>A form field, with its role, value, state and flags.</summary>
    FormField = 2,

    /// <summary>An annotation with a description, such as a note, a link or a stamp.</summary>
    Annotation = 3,
}
