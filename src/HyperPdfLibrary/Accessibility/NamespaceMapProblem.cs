// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>What is wrong with a structure type's namespace role map chain.</summary>
internal enum NamespaceMapProblem
{
    /// <summary>Nothing is wrong.</summary>
    None = 0,

    /// <summary>The chain comes back to the same type in the same namespace.</summary>
    Cycle = 1,

    /// <summary>The chain comes back to a namespace it already passed through, with a different type.</summary>
    SameNamespace = 2,
}
