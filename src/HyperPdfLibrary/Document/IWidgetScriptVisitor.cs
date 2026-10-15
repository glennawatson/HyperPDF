// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>Receives the field scripts of a page one at a time as characters, without a string for each.</summary>
internal interface IWidgetScriptVisitor
{
    /// <summary>Handles one non-empty script.</summary>
    /// <param name="script">The script's characters; valid only during the call.</param>
    /// <returns><see langword="true"/> to stop visiting.</returns>
    bool Visit(ReadOnlySpan<char> script);
}
