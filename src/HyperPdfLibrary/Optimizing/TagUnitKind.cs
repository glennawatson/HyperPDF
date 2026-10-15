// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Optimizing;

/// <summary>What a page-level operator wrapped by inferred tagging draws.</summary>
internal enum TagUnitKind
{
    /// <summary>A text-showing operator.</summary>
    Text = 0,

    /// <summary>An image XObject or inline image: a figure.</summary>
    Figure = 1,

    /// <summary>A form XObject, tagged by the text it draws or else marked as an artifact.</summary>
    Form = 2,

    /// <summary>A painted path or shading: always marked as an artifact, because it carries no text.</summary>
    Path = 3,
}
