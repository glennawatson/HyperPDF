// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>One entry of the structure tree's role map (ISO 32000-2 14.8.3): a custom tag type and the type it stands for.</summary>
/// <param name="Type">The custom type, as the file spells it.</param>
/// <param name="MapsTo">The type it is mapped to; usually a standard type.</param>
[DebuggerDisplay("PdfRoleMapping: {Type} to {MapsTo}")]
public sealed record PdfRoleMapping(string Type, string MapsTo);
