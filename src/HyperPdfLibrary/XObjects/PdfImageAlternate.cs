// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.XObjects;

/// <summary>An alternate version of an image, from the image's <c>/Alternates</c> array (ISO 32000-2 §8.9.5.4, Table 89).</summary>
/// <param name="Image">The alternate image XObject.</param>
/// <param name="DefaultForPrinting">Whether this alternate is the default version for printing.</param>
/// <param name="OptionalContent">The <c>/OC</c> optional content group that controls the alternate, or null.</param>
[DebuggerDisplay("PdfImageAlternate: printing default {DefaultForPrinting}")]
public sealed record PdfImageAlternate(PdfStream Image, bool DefaultForPrinting, PdfDictionary? OptionalContent);
