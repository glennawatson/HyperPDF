// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Flattening;

/// <summary>Which look of the page a flattened copy keeps.</summary>
public enum FlattenCopyLook
{
    /// <summary>What the page shows on screen. Items marked not to show on screen are left out.</summary>
    Screen = 0,

    /// <summary>What the page prints. Items not marked to print are left out.</summary>
    Print = 1,
}
