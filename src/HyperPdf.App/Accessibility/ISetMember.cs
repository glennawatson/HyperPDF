// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.Accessibility;

/// <summary>Implemented by an automation peer that knows its own place in a set, such as a list item on a page.</summary>
internal interface ISetMember
{
    /// <summary>Gets the peer's place from 1, or 0 when it is not in a set.</summary>
    int PositionInSet { get; }

    /// <summary>Gets the number of peers in the set, or 0 when the peer is not in a set.</summary>
    int SetSize { get; }
}
