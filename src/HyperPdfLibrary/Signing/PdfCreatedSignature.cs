// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signing;

/// <summary>The objects a signature added.</summary>
/// <param name="Signature">The signature dictionary's object id.</param>
/// <param name="Widget">The widget's object id.</param>
[DebuggerDisplay("PdfCreatedSignature: {Signature}")]
internal readonly record struct PdfCreatedSignature(PdfObjectId Signature, PdfObjectId Widget);
