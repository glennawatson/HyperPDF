// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace HyperPdfLibrary.Metadata;

/// <summary>Keeps parsed properties read-only while retaining their span lookup.</summary>
/// <param name="values">The detached property values, never changed after publication.</param>
internal sealed class XmpPropertyDictionary(Dictionary<string, string[]> values) : ReadOnlyDictionary<string, string[]>(values)
{
    /// <summary>Gets the concrete dictionary for allocation-free internal lookups.</summary>
    internal Dictionary<string, string[]> Lookup => (Dictionary<string, string[]>)Dictionary;
}
