// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for actions read as data.</summary>
public sealed class ActionDataTests
{
    /// <summary>The submit-form action object.</summary>
    private const int Submit = 4;

    /// <summary>The reset-form action object.</summary>
    private const int Reset = 5;

    /// <summary>The hide action object.</summary>
    private const int Hide = 6;

    /// <summary>The set-OCG-state action object.</summary>
    private const int SetOcg = 7;

    /// <summary>The thread action object.</summary>
    private const int Thread = 8;

    /// <summary>The go-to-document-part action object.</summary>
    private const int GoToDp = 9;

    /// <summary>The unknown action object.</summary>
    private const int Unknown = 10;

    /// <summary>The first action of a chain.</summary>
    private const int ChainStart = 11;

    /// <summary>The JavaScript action object.</summary>
    private const int Script = 20;

    /// <summary>The transition action object.</summary>
    private const int Trans = 21;

    /// <summary>The sound action object.</summary>
    private const int SoundObject = 22;

    /// <summary>The movie action object.</summary>
    private const int Movie = 24;

    /// <summary>The rendition action object.</summary>
    private const int Rendition = 25;

    /// <summary>The 3D view action with a view selector.</summary>
    private const int View3DSelector = 26;

    /// <summary>The 3D view action with a view dictionary.</summary>
    private const int View3DDictionary = 27;

    /// <summary>The rich media execute action object.</summary>
    private const int RichMedia = 28;

    /// <summary>The flags of the submit action.</summary>
    private const int SubmitFlags = 4;

    /// <summary>The group count of the first set-OCG-state step.</summary>
    private const int SecondStepGroups = 2;

    /// <summary>The number of steps in the set-OCG-state action.</summary>
    private const int StepCount = 3;

    /// <summary>The thread index of the thread action.</summary>
    private const int ThreadIndex = 2;

    /// <summary>The direction of the transition.</summary>
    private const int Direction = 90;

    /// <summary>The duration of the transition in seconds.</summary>
    private const double Duration = 2;

    /// <summary>The sampling rate of the sound.</summary>
    private const double SoundRate = 22_050;

    /// <summary>The volume of the sound action.</summary>
    private const double Volume = 0.5;

    /// <summary>The number of actions that follow the chain's first action.</summary>
    private const int ChainLength = 2;

    /// <summary>Form actions are read as data.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormActionsAreRead()
    {
        using var document = Open();
        var submit = (SubmitFormAction)Node(document, Submit).Action.Value!;
        var reset = (ResetFormAction)Node(document, Reset).Action.Value!;
        var hide = (HideAction)Node(document, Hide).Action.Value!;

        await Assert.That(Node(document, Submit).Action.IsNavigation).IsFalse();
        await Assert.That(submit.Url).IsEqualTo("http://x/submit");
        await Assert.That(submit.Fields).IsEquivalentTo(["a", "grp.leaf"]);
        await Assert.That(submit.Flags).IsEqualTo(SubmitFlags);
        await Assert.That(reset.Fields).IsEquivalentTo(["a"]);
        await Assert.That(hide.Targets).IsEquivalentTo(["fld"]);
        await Assert.That(hide.Hide).IsFalse();
    }

    /// <summary>Layer, thread and document part actions are read as data, and an unknown action keeps its name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StructureActionsAreRead()
    {
        using var document = Open();
        var ocg = (SetOcgStateAction)Node(document, SetOcg).Action.Value!;
        var thread = (ThreadAction)Node(document, Thread).Action.Value!;

        await Assert.That(ocg.Changes.Length).IsEqualTo(StepCount);
        await Assert.That(ocg.Changes[0].Mode).IsEqualTo("OFF");
        await Assert.That(ocg.Changes[1].Groups.Length).IsEqualTo(SecondStepGroups);
        await Assert.That(ocg.PreserveRadioButtons).IsFalse();
        await Assert.That(thread.ThreadIndex).IsEqualTo(ThreadIndex);
        await Assert.That(thread.BeadIndex).IsEqualTo(1);
        await Assert.That(((GoToDpAction)Node(document, GoToDp).Action.Value!).Part).IsNotNull();
        await Assert.That(((UnsupportedAction)Node(document, Unknown).Action.Value!).Subtype).IsEqualTo("Frobnicate");
    }

    /// <summary>A next chain is followed, and a loop back to an earlier action is cut.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NextChainsStopAtLoops()
    {
        using var document = Open();
        var start = Node(document, ChainStart);

        await Assert.That(start.Action.Value is UriAction).IsTrue();
        await Assert.That(start.Next.Length).IsEqualTo(ChainLength);
        await Assert.That(start.Next[0].Subtype).IsEqualTo("Named");
        await Assert.That(start.Next[0].Next.Length).IsEqualTo(0);
        await Assert.That(start.Next[1].Action.Value is ImportDataAction { File: "d.fdf" }).IsTrue();
    }

    /// <summary>The open action, additional actions and document scripts are read as text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenActionTriggersAndScriptsAreRead()
    {
        using var document = Open();
        var scripts = PdfDocumentActions.GetDocumentScripts(document);

        await Assert.That(PdfDocumentActions.GetOpenAction(document)!.Action.Value is JavaScriptAction { Script: "app.alert(1)" }).IsTrue();
        await Assert.That(PdfDocumentActions.GetTriggers(document)[0].Event).IsEqualTo("WC");
        await Assert.That(PdfDocumentActions.GetTriggers(document, PdfDocumentPages.GetPage(document, 0))[0].Event).IsEqualTo("O");
        await Assert.That(scripts.Length).IsEqualTo(1);
        await Assert.That(scripts[0].Name).IsEqualTo("init");
        await Assert.That(scripts[0].Script).IsEqualTo("app.alert(1)");
    }

    /// <summary>Transition, sound, movie, rendition, 3D view and rich media actions are read as data.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MediaActionsAreRead()
    {
        using var document = Open();
        var trans = (TransAction)Node(document, Trans).Action.Value!;
        var sound = (SoundAction)Node(document, SoundObject).Action.Value!;
        var movie = (MovieAction)Node(document, Movie).Action.Value!;
        var rendition = (RenditionAction)Node(document, Rendition).Action.Value!;
        var selector = (GoTo3DViewAction)Node(document, View3DSelector).Action.Value!;
        var view = (GoTo3DViewAction)Node(document, View3DDictionary).Action.Value!;
        var rich = (RichMediaExecuteAction)Node(document, RichMedia).Action.Value!;

        await Assert.That(trans.Transition.Style).IsEqualTo("Wipe");
        await Assert.That(trans.Transition.Duration).IsEqualTo(Duration);
        await Assert.That(trans.Transition.Direction).IsEqualTo(Direction);
        await Assert.That(sound.Sound!.Rate).IsEqualTo(SoundRate);
        await Assert.That(sound.Volume).IsEqualTo(Volume);
        await Assert.That(sound.Repeat).IsTrue();
        await Assert.That(movie.Operation).IsEqualTo("Pause");
        await Assert.That(movie.Title).IsEqualTo("mv");
        await Assert.That(rendition.Operation).IsEqualTo(0);
        await Assert.That(rendition.Rendition!.Subtype).IsEqualTo("MR");
        await Assert.That(rendition.Script).IsEqualTo("x");
        await Assert.That(selector.ViewSelector).IsEqualTo("F");
        await Assert.That(selector.Command).IsEqualTo("Linear");
        await Assert.That(view.View!.ExternalName).IsEqualTo("Front");
        await Assert.That(rich.Command).IsEqualTo("play");
        await Assert.That(rich.Arguments!.Count).IsEqualTo(ChainLength);
        await Assert.That(Node(document, Script).Action.Value is JavaScriptAction).IsTrue();
    }

    /// <summary>Reads an action node from the sample document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="number">The action object number.</param>
    /// <returns>The node.</returns>
    private static PdfActionNode Node(PdfDocument document, int number) => PdfDocumentActions.ReadActionNode(document, StructureDocuments.Dictionary(document, number));

    /// <summary>Opens the sample document that holds every action.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument Open() => StructureDocuments.OpenPage(
        "/OpenAction 20 0 R /AA << /WC 20 0 R >> /Names << /JavaScript << /Names [(init) 20 0 R] >> >>",
        "/AA << /O 4 0 R >>",
        "<< /S /SubmitForm /F (http://x/submit) /Fields [(a) 14 0 R] /Flags 4 >>",
        "<< /S /ResetForm /Fields [(a)] /Flags 1 >>",
        "<< /S /Hide /T (fld) /H false >>",
        "<< /S /SetOCGState /State [/OFF 15 0 R /ON 16 0 R 17 0 R /Toggle 15 0 R] /PreserveRB false >>",
        "<< /S /Thread /D 2 /B 1 >>",
        "<< /S /GoToDp /Dp 18 0 R >>",
        "<< /S /Frobnicate >>",
        "<< /S /URI /URI (http://a) /Next [12 0 R 13 0 R] >>",
        "<< /S /Named /N /NextPage /Next 11 0 R >>",
        "<< /S /ImportData /F (d.fdf) >>",
        "<< /T (leaf) /Parent 19 0 R >>",
        "<< /Type /OCG /Name (L1) >>",
        "<< /Type /OCG /Name (L2) >>",
        "<< /Type /OCG /Name (L3) >>",
        "<< /Type /DPart >>",
        "<< /T (grp) >>",
        "<< /S /JavaScript /JS (app.alert\\(1\\)) >>",
        "<< /S /Trans /Trans << /S /Wipe /D 2 /Di 90 >> >>",
        "<< /S /Sound /Sound 23 0 R /Volume 0.5 /Repeat true >>",
        MiniPdf.Stream("/R 22050 /C 2", "ab"),
        "<< /S /Movie /T (mv) /Operation /Pause >>",
        "<< /S /Rendition /OP 0 /R << /Type /Rendition /S /MR /N (r) >> /JS (x) >>",
        "<< /S /GoTo3DView /TA 4 0 R /V /F /C /Linear >>",
        "<< /S /GoTo3DView /V << /XN (Front) >> >>",
        "<< /S /RichMediaExecute /TA 4 0 R /CMD << /C (play) /A [1 2] >> >>");
}
