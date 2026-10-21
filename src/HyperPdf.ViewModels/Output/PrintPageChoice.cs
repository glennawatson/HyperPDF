// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Output;

/// <summary>Which pages print.</summary>
public enum PrintPageChoice
{
    /// <summary>Every page.</summary>
    All = 0,

    /// <summary>The page being viewed.</summary>
    Current = 1,

    /// <summary>Pages typed as ranges, such as 1-3, 7.</summary>
    Custom = 2,
}
