// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>A colour space as PDF/A validation reads it: the direct value, the object it came from and how to read the names inside it.</summary>
/// <param name="Value">The direct value: a name, an array, or a pattern dictionary or stream.</param>
/// <param name="Id">The indirect object the value came from; not valid when the value was direct.</param>
/// <param name="Resources">The resource dictionary that names inside the value are looked up in, or null.</param>
/// <param name="WasDefault">Whether it was reached as a DefaultGray, DefaultRGB or DefaultCMYK space, so device names inside it are not replaced again.</param>
internal readonly record struct PdfAColourSpace(PdfValue Value, PdfObjectId Id, PdfDictionary? Resources, bool WasDefault);
