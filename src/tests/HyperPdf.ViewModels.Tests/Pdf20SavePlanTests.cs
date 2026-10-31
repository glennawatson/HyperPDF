// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Protection;
using HyperPdf.Engine;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Output;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks how a save is rewritten as PDF 2.0: unasked for a plain file, and only on the person's say-so when it costs a password.</summary>
public sealed class Pdf20SavePlanTests
{
    /// <summary>The pages of the test document.</summary>
    private const int Pages = 2;

    /// <summary>The password that opens the protected test file.</summary>
    private const string UserPassword = "user-pass";

    /// <summary>The password of the protected test file's owner.</summary>
    private const string OwnerPassword = "owner-pass";

    /// <summary>The length of the file header checked, such as <c>%PDF-2.0</c>.</summary>
    private const int HeaderLength = 8;

    /// <summary>A plain file with changes is upgraded without asking anything.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainFileIsUpgradedWithoutAsking()
    {
        using var test = new TestServices();
        using var document = new HyperPdfEngine().Open(test.CreateDocument("plan-plain.pdf", Pages), null);
        var asked = 0;
        var confirm = new Interaction<ConfirmRequest, bool>();
        using var handler = confirm.RegisterHandler(context =>
        {
            asked++;
            context.SetOutput(true);
        });

        var plan = await Pdf20SavePlan.PlanAsync(document, true, confirm, CancellationToken.None);

        using (Assert.Multiple())
        {
            await Assert.That(plan).IsNotNull();
            await Assert.That(plan!.Options.RemoveProtection).IsFalse();
            await Assert.That(asked).IsEqualTo(0);
        }
    }

    /// <summary>A file with no changes is not rewritten.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnchangedFileIsNotRewritten()
    {
        using var test = new TestServices();
        using var document = new HyperPdfEngine().Open(test.CreateDocument("plan-unchanged.pdf", Pages), null);

        var plan = await Pdf20SavePlan.PlanAsync(document, false, new(), CancellationToken.None);

        await Assert.That(plan).IsNull();
    }

    /// <summary>A protected file is upgraded, with its protection removed, when the person agrees, and the file written has none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProtectedFileIsUpgradedWhenThePersonAgrees()
    {
        using var test = new TestServices();
        using var document = await OpenProtectedAsync(test, "plan-protected.pdf");
        var question = string.Empty;
        var confirm = new Interaction<ConfirmRequest, bool>();
        using var handler = confirm.RegisterHandler(context =>
        {
            question = context.Input.Message;
            context.SetOutput(true);
        });

        var plan = await Pdf20SavePlan.PlanAsync(document, true, confirm, CancellationToken.None);
        var path = Path.Combine(test.Directory, "plan-protected-saved.pdf");
        var result = await Pdf20Resave.TryAsync(plan!, path, CancellationToken.None);
        var header = new byte[HeaderLength];
        await using (var file = File.OpenRead(path))
        {
            _ = await file.ReadAsync(header);
        }

        using var reopened = new HyperPdfEngine().Open(path, null);
        using (Assert.Multiple())
        {
            await Assert.That(question).Contains("password protection", StringComparison.Ordinal);
            await Assert.That(plan!.Options.RemoveProtection).IsTrue();
            await Assert.That(result.Written).IsTrue().Because(result.Reason ?? string.Empty);
            await Assert.That(System.Text.Encoding.ASCII.GetString(header)).IsEqualTo("%PDF-2.0");
            await Assert.That(reopened.GetMetadata().IsEncrypted).IsFalse();
        }
    }

    /// <summary>A protected file is left in its own version when the person says no.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProtectedFileStaysWhenThePersonSaysNo()
    {
        using var test = new TestServices();
        using var document = await OpenProtectedAsync(test, "plan-declined.pdf");
        var confirm = new Interaction<ConfirmRequest, bool>();
        using var handler = confirm.RegisterHandler(static context => context.SetOutput(false));

        var plan = await Pdf20SavePlan.PlanAsync(document, true, confirm, CancellationToken.None);

        await Assert.That(plan).IsNull();
    }

    /// <summary>Makes a protected copy of a test document and opens it with its password.</summary>
    /// <param name="test">The test services.</param>
    /// <param name="name">The file name.</param>
    /// <returns>The open document, which the caller disposes.</returns>
    /// <exception cref="InvalidOperationException">The protected copy could not be written.</exception>
    private static async Task<IDocument> OpenProtectedAsync(TestServices test, string name)
    {
        var engine = new HyperPdfEngine();
        using var plain = engine.Open(test.CreateDocument(name, Pages), null);
        await using var output = new MemoryStream();
        var request = new ProtectionRequest { Format = ProtectionFormat.Strong, UserPassword = UserPassword, OwnerPassword = OwnerPassword, Permissions = ProtectionPermissions.All };
        var written = await DocumentFeature<IDocumentProtector>.Get(plain)!.ProtectAsync(request, output, CancellationToken.None);
        var path = Path.Combine(test.Directory, $"protected-{name}");
        await File.WriteAllBytesAsync(path, output.ToArray());
        return written.Written ? engine.Open(path, OwnerPassword) : throw new InvalidOperationException(written.Refusal);
    }
}
