// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.Tests;

/// <summary>Records the step a test reached in the CI diagnostics folder, to find where a stalled run stopped.</summary>
internal static class StepLog
{
    /// <summary>Appends a step to the process's log when the runner has a diagnostics folder.</summary>
    /// <param name="test">The test name.</param>
    /// <param name="step">The step.</param>
    internal static void Mark(string test, string step)
    {
        var directory = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        var folder = Path.Combine(directory, "test-diagnostics");
        _ = Directory.CreateDirectory(folder);
        File.AppendAllText(Path.Combine(folder, $"steps-{Environment.ProcessId}.log"), $"{TimeProvider.System.GetUtcNow():O} {test} {step}\n");
    }
}
