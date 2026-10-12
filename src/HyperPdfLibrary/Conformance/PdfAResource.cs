// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>A named resource found for a content stream.</summary>
/// <param name="Raw">The resource value, possibly a reference; null when missing.</param>
/// <param name="Resources">The resource dictionary it was found in.</param>
internal readonly record struct PdfAResource(PdfValue Raw, PdfDictionary? Resources)
{
    /// <summary>Gets the object the resource is, when indirect.</summary>
    internal PdfObjectId Id => Raw.IsReference ? Raw.AsReference() : default;
}
