// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Creation;

/// <summary>An ICC based colour space added to a document, which any number of images can use.</summary>
/// <param name="Id">The object holding the <c>[/ICCBased stream]</c> array.</param>
/// <param name="Components">The number of colour components the profile describes: 1, 3 or 4.</param>
[DebuggerDisplay("PdfIccColorSpace: {Components} components")]
public readonly record struct PdfIccColorSpace(PdfObjectId Id, int Components)
{
    /// <summary>Gets a value indicating whether this refers to a colour space.</summary>
    public bool IsValid => Id.IsValid && Components > 0;
}
