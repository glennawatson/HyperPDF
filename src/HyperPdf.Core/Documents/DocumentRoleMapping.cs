// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A custom tag type of the document and the standard type it stands for.</summary>
/// <param name="Type">The custom type, as the file spells it.</param>
/// <param name="MapsTo">The type it stands for.</param>
[DebuggerDisplay("DocumentRoleMapping: {Type} to {MapsTo}")]
public sealed record DocumentRoleMapping(string Type, string MapsTo);
