// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>What the appearance generator needs to know about the field a widget belongs to.</summary>
/// <param name="Type">The field type.</param>
/// <param name="Flags">The field flags.</param>
/// <param name="Field">The field dictionary the widget inherits from.</param>
/// <param name="OnState">The widget's on-state name for a check box or radio button; <see langword="null"/> for other kinds.</param>
/// <param name="CancellationToken">Cancels the drawing.</param>
[DebuggerDisplay("FormAppearanceRequest: {Type}")]
internal sealed record FormAppearanceRequest(PdfFieldType Type, PdfFieldFlags Flags, PdfDictionary Field, string? OnState, CancellationToken CancellationToken);
