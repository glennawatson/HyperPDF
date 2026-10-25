// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The ways the tree of elements can change.</summary>
internal enum StructureChangeType
{
    /// <summary>A child was added.</summary>
    ChildAdded = 0,

    /// <summary>A child was removed.</summary>
    ChildRemoved = 1,

    /// <summary>The children changed in a way that cannot be told in detail.</summary>
    ChildrenInvalidated = 2,
}
