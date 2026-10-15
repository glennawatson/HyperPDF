// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Benchmarks.Generator;

/// <summary>What a benchmark method returns, which decides how the generated code calls it.</summary>
internal enum ReturnKind
{
    /// <summary>The method returns nothing.</summary>
    Void = 0,

    /// <summary>A task with no result.</summary>
    Task = 1,

    /// <summary>A value task with no result.</summary>
    ValueTask = 2,

    /// <summary>A task with a result.</summary>
    TaskOfResult = 3,

    /// <summary>A value task with a result.</summary>
    ValueTaskOfResult = 4,

    /// <summary>An ordinary value.</summary>
    Value = 5,

    /// <summary>A ref struct, which cannot be passed on.</summary>
    RefLike = 6,
}
