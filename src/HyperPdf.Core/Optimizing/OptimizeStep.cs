// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Optimizing;

/// <summary>The step an optimisation is on.</summary>
public enum OptimizeStep
{
    /// <summary>Checking signatures, encryption and conformance claims.</summary>
    Checking = 0,

    /// <summary>Reading the pages to find how images and fonts are used.</summary>
    Analysing = 1,

    /// <summary>Changing pages and accessibility entries.</summary>
    Editing = 2,

    /// <summary>Finding identical objects.</summary>
    Deduplicating = 3,

    /// <summary>Writing the new file.</summary>
    Writing = 4,

    /// <summary>The run has finished.</summary>
    Done = 5,
}
