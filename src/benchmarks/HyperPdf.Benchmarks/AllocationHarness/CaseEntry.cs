// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace AllocationHarness;

/// <summary>One benchmark with one combination of its class's parameters, as the generated code lists it.</summary>
/// <param name="FilterName">The class and method name a filter is matched against.</param>
/// <param name="Name">The full name with the parameters.</param>
/// <param name="Run">Runs the benchmark from set-up to clean-up.</param>
[DebuggerDisplay("{Name}")]
internal sealed record CaseEntry(string FilterName, string Name, Func<Task> Run);
