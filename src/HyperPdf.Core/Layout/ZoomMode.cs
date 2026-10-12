// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Layout;

/// <summary>How the zoom level is chosen.</summary>
public enum ZoomMode
{
    /// <summary>The user chose an explicit zoom.</summary>
    Free = 0,

    /// <summary>Pages fill the viewport width.</summary>
    FitWidth = 1,

    /// <summary>A whole page fits in the viewport.</summary>
    FitPage = 2,
}
