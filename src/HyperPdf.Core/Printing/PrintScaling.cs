// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Printing;

/// <summary>How a page is sized on the paper when it prints one to a sheet.</summary>
public enum PrintScaling
{
    /// <summary>Enlarge or shrink the page to fill the paper inside its margins.</summary>
    FitToPaper = 0,

    /// <summary>Print the page at its true size, centred; anything wider than the paper is cut off.</summary>
    ActualSize = 1,

    /// <summary>Print at true size, shrinking only pages too large for the paper.</summary>
    ShrinkOversized = 2,

    /// <summary>Print at a chosen percentage of the true size, centred.</summary>
    Custom = 3,
}
