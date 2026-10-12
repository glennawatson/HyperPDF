// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>What a field of a PDF/A extension schema description must be.</summary>
internal enum PdfAExtensionFieldForm
{
    /// <summary>Present, simple, with the description schema's prefix.</summary>
    RequiredText = 0,

    /// <summary>When present, an ordered array, with the description schema's prefix or none.</summary>
    OptionalSequence = 1,

    /// <summary>Present, simple, a value type the context knows, with the description schema's prefix.</summary>
    RequiredValueType = 2,

    /// <summary>Present, simple, <c>external</c> or <c>internal</c>, with the description schema's prefix.</summary>
    RequiredCategory = 3,
}
