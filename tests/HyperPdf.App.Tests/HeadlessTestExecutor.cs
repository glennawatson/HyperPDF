// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.Tests;
using TUnit.Core.Executors;
using TUnit.Core.Interfaces;

[assembly: NotInParallel]
[assembly: TestExecutor<HeadlessTestExecutor>]

namespace HyperPdf.App.Tests;

/// <summary>Runs every test on the headless Avalonia UI thread.</summary>
public sealed class HeadlessTestExecutor : ITestExecutor
{
    /// <inheritdoc/>
    public async ValueTask ExecuteTest(TestContext context, Func<ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _ = await HeadlessSession.Instance.Dispatch(
            async () =>
            {
                await action();
                return true;
            },
            CancellationToken.None);
    }
}
