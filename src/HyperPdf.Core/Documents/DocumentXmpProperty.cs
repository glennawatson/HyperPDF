// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>One property of the document's XMP metadata.</summary>
/// <param name="Name">The property's name with its schema prefix, such as "dc:title"; the full address in braces for a schema with no common prefix.</param>
/// <param name="Value">The value; the items of an array are joined with semicolons.</param>
[DebuggerDisplay("DocumentXmpProperty: {Name} = {Value}")]
public sealed record DocumentXmpProperty(string Name, string Value);
