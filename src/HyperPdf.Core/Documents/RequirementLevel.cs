// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>How far a document's declared requirements are met.</summary>
public enum RequirementLevel
{
    /// <summary>The document has no requirements, or the viewer meets all of them.</summary>
    None = 0,

    /// <summary>Some requirements are unmet, but the penalties are low enough to show the document with the normal fallbacks.</summary>
    Fallback = 1,

    /// <summary>The unmet penalties add up to 100 or more, so the document cannot look or work as its author meant.</summary>
    Critical = 2,
}
