// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Starts pages for a flow that has run out of room.</summary>
internal interface IPager
{
    /// <summary>Finishes the page the cursor is on and starts the next one, moving the cursor to its top.</summary>
    /// <param name="cursor">The cursor.</param>
    void StartPage(FlowCursor cursor);
}
