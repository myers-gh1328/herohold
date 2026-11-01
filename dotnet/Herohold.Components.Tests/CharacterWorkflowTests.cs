using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Components.Tests;

[TestClass]
public sealed class CharacterWorkflowTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "herohold-component-review-" + Guid.NewGuid().ToString("N"));
        public WorkspaceSession Session { get; }
        public Fixture(JsonObject result, ICharacterRules? workflow=null) => Session = new(TestStores.Open(folder), workflow??new FakeRules(_ => result.DeepClone().AsObject()));
        public async Task LoadAsync() { await Session.InitializeAsync(); await Session.ReviewUploadAsync("fictional.pdf", "%PDF-synthetic"u8.ToArray()); Assert.IsNull(Session.Error); }
        public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
    private static JsonObject Review() => new()
    {
        ["character"] = WorkspaceProjection.ToCanonical(SampleWorkspace.Create().Character),
        ["validation"] = new JsonObject { ["complete"] = true, ["missing"] = new JsonArray(), ["warnings"] = new JsonArray() },
        ["choices"] = new JsonArray(),
        ["importReview"] = new JsonObject
        {
            ["sourceKind"] = "compatible_form", ["canAdopt"] = false,
            ["message"] = "Review recovered values before adoption.",
            ["issues"] = new JsonArray(new JsonObject { ["code"] = "ability_baseline_unconfirmed", ["severity"] = "blocker", ["section"] = "abilities", ["label"] = "Confirm final ability scores.", ["remedy"] = "Use the ability confirmation control." }),
            ["comparisons"] = new JsonArray(new JsonObject { ["key"] = "armor_class", ["label"] = "Armor Class", ["observed"] = 17, ["calculated"] = 16, ["status"] = "conflict" }),
            ["evidence"] = new JsonObject { ["fields"] = new JsonObject(Enumerable.Range(1, 334).Select(i => KeyValuePair.Create<string, JsonNode?>("Synthetic field " + i, JsonValue.Create("Fictional value " + i)))) }
        }
    };

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task PdfReviewShowsActionableIssuesAndKeepsOriginalFormEvidenceCollapsed()
    {
        using var fixture = new Fixture(Review()); await fixture.LoadAsync(); using var context = new BunitContext();
        var cut = context.Render<CharacterWorkflow>(p => p.Add(c => c.Session, fixture.Session));
        StringAssert.Contains(cut.Find("[data-testid=import-issues]").TextContent, "Confirm final ability scores.");
        StringAssert.Contains(cut.Find("[data-testid=import-issues]").TextContent, "Use the ability confirmation control.");
        var evidence = cut.Find("details[data-testid=import-evidence]");
        Assert.IsFalse(evidence.HasAttribute("open")); StringAssert.Contains(evidence.QuerySelector("summary")!.TextContent, "334");
        Assert.AreEqual(334, evidence.QuerySelectorAll("tbody tr").Length);
        Assert.AreEqual("0",evidence.QuerySelector("[role=region]")?.GetAttribute("tabindex"),"A keyboard user must be able to focus and scroll the long evidence table.");
        Assert.IsTrue(cut.Find("[data-testid=commit-workflow]").HasAttribute("disabled"));
        var comparison = cut.Find("details[data-testid=import-comparisons]");
        StringAssert.Contains(comparison.TextContent, "Armor Class"); StringAssert.Contains(comparison.TextContent, "17"); StringAssert.Contains(comparison.TextContent, "16");
        Assert.IsFalse(comparison.HasAttribute("open"));
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task BothSourceReviewAndBuildValidationMustPassBeforeExplicitAdoptionCreatesASeparateCopy()
    {
        var result=Review();result["validation"]!["complete"]=false;result["importReview"]!["canAdopt"]=true;
        using var fixture=new Fixture(result);await fixture.LoadAsync();using var context=new BunitContext();var finished=0;
        var cut=context.Render<CharacterWorkflow>(p=>p.Add(c=>c.Session,fixture.Session).Add(c=>c.OnFinished,()=>finished++));
        var originalId=fixture.Session.Character!.Id;
        Assert.IsTrue(cut.Find("[data-testid=commit-workflow]").HasAttribute("disabled"));
        result["validation"]!["complete"]=true;result["importReview"]!["canAdopt"]=false;await fixture.LoadAsync();cut.Render();
        Assert.IsTrue(cut.Find("[data-testid=commit-workflow]").HasAttribute("disabled"));
        result["importReview"]!["canAdopt"]=true;result["importReview"]!["issues"]=new JsonArray();await fixture.LoadAsync();cut.Render();
        Assert.IsFalse(cut.Find("[data-testid=commit-workflow]").HasAttribute("disabled"));
        Assert.AreEqual(originalId,fixture.Session.Character!.Id);Assert.AreEqual(1,fixture.Session.Library.Count);
        await cut.Find("[data-testid=commit-workflow]").ClickAsync(new());
        Assert.IsNull(fixture.Session.Error);Assert.IsNull(fixture.Session.Workflow);Assert.AreNotEqual(originalId,fixture.Session.Character.Id);Assert.AreEqual(2,fixture.Session.Library.Count);Assert.AreEqual(1,finished);
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task UploadedIssueLabelsAndRawValuesRemainTextInsteadOfExecutableMarkup()
    {
        var result=Review();result["importReview"]!["issues"]![0]!["label"]="<script>window.bad=true</script>";result["importReview"]!["evidence"]!["fields"]!["CharacterName"]="<img src=x onerror=bad()>";
        using var fixture=new Fixture(result);await fixture.LoadAsync();using var context=new BunitContext();var cut=context.Render<CharacterWorkflow>(p=>p.Add(c=>c.Session,fixture.Session));
        Assert.AreEqual(0,cut.FindAll("script,img").Count);StringAssert.Contains(cut.Find("[data-testid=import-issues]").TextContent,"<script>");StringAssert.Contains(cut.Find("[data-testid=import-evidence]").TextContent,"<img src=x");
    }
    private sealed class HeldPreview(JsonObject result) : FakeRules(_ => result.DeepClone().AsObject())
    {
        public TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override void Step(string operation) { if(operation=="levelup.preview")Release.Task.Wait(); }
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task PendingHpPreviewLocksItsPolicyControlsAndPreservesOptInCurrentHpDefault()
    {
        var result=Review();result.Remove("importReview");result["draft"]=new JsonObject{["transaction_id"]="fictional-levelup",["current_level"]=5,["target_level"]=6,["hit_die"]=10,["automatic_features"]=new JsonArray()};
        var held=new HeldPreview(result);using var fixture=new Fixture(result,held);await fixture.Session.InitializeAsync();await fixture.Session.StartLevelUpAsync();using var context=new BunitContext();
        var cut=context.Render<CharacterWorkflow>(p=>p.Add(c=>c.Session,fixture.Session));await cut.FindAll(".luf-steps button")[1].ClickAsync(new());
        Assert.IsFalse(cut.Find("#level-add-current-hp").HasAttribute("checked"));
        var pending=fixture.Session.PreviewLevelUpHpAsync(new(){Mode="fixed",AddGainedHp=false});
        try { cut.Render();Assert.IsTrue(fixture.Session.IsBusy);Assert.IsTrue(cut.FindAll("input").All(i=>i.HasAttribute("disabled")),"A pending preview must not allow its HP policy to change underneath the result."); }
        finally { held.Release.TrySetResult();await pending; }
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task OptionalUnselectedChoicesDoNotAppearAsRequiredWorkInDraftSummary()
    {
        var result=Review();result["choices"]=new JsonArray(new JsonObject{["id"]="optional-replacement",["label"]="Optional spell replacement",["type"]="single",["section"]="spells",["required"]=false,["status"]="unresolved",["value"]="",["options"]=new JsonArray()});
        using var fixture=new Fixture(result);await fixture.LoadAsync();using var context=new BunitContext();var cut=context.Render<CharacterWorkflow>(p=>p.Add(c=>c.Session,fixture.Session));
        Assert.AreEqual("0 remaining",cut.FindAll(".workflow-summary .change-row strong").Last().TextContent);
    }
    [TestMethod]
    public void ExistingFeatRepairKeepsItsIdentityReadOnlyAndEditsOnlyItsChildren()
    {
        var choice=JsonNode.Parse("""{"id":"existing_feat:owned","label":"Skill Expert choices","type":"feat_selection","value":{"mode":"feat","feat_id":"skill_expert_xphb","feat_choices":{"skill_proficiency":"Insight","expertise":"Stealth"}},"details":{"feat_options":[{"id":"skill_expert_xphb","name":"Skill Expert","children":[{"id":"expertise","label":"Expertise","type":"single","path":["feat_choices","expertise"],"options":[{"value":"Insight","label":"Insight"},{"value":"Stealth","label":"Stealth"}]}]}]}}""")!.AsObject();
        using var context=new BunitContext();ChoiceValue? changed=null;var cut=context.Render<ChoiceEditor>(p=>p.Add(c=>c.Choice,JsonWorkflowViews.Choice(choice)).Add(c=>c.ValueChanged,value=>changed=value));
        Assert.AreEqual("Skill Expert",cut.Find("[data-testid=fixed-feat-name]").TextContent.Trim());
        Assert.AreEqual(1,cut.FindAll("select").Count,"Only the child choice is selectable; the owned feat identity stays fixed.");
        cut.Find("[data-choice-id=expertise] select").Change("0");
        Assert.AreEqual("skill_expert_xphb",changed!.At(["feat_id"])!.Text);Assert.AreEqual("Insight",changed.At(["feat_choices","expertise"])!.Text);
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task AbilityGenerationAppearsInCreationWhileImportKeepsItsOriginalManualScoreReview()
    {
        var result=Review();result["character"]!["ability_score_method"]="Manual entry";result["choices"]=new JsonArray(new JsonObject{["id"]="base_ability_scores",["label"]="Ability scores",["type"]="ability_scores",["section"]="abilities",["path"]=new JsonArray("base_ability_scores"),["value"]=result["character"]!["base_ability_scores"]!.DeepClone(),["status"]="resolved",["required"]=true});
        using var fixture=new Fixture(result);await fixture.Session.InitializeAsync();await fixture.Session.StartCreateAsync();using var context=new BunitContext();var cut=context.Render<CharacterWorkflow>(p=>p.Add(c=>c.Session,fixture.Session));
        Assert.AreEqual(1,cut.FindAll("[data-testid=ability-method]").Count);Assert.AreEqual(6,cut.FindAll(".ability-editor-grid input").Count);
        await fixture.LoadAsync();cut.Render();Assert.AreEqual(0,cut.FindAll("[data-testid=ability-method]").Count);Assert.AreEqual(6,cut.FindAll(".ability-editor-grid input").Count);
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task InventorySuggestsOfficialItemsLocksTheirDetailsAndAllowsACustomItemName()
    {
        using var fixture=new Fixture(new JsonObject());await fixture.Session.InitializeAsync();using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;
        context.Services.AddSingleton<IItemCatalog>(new Items(
            new CatalogItem{Id="XPHB|Rope",Source="XPHB",Definition=new EquipmentItem{Name="Rope",Description="Fifty feet of hempen rope.",UnitWeightLb=5}},
            new CatalogItem{Id="XPHB|Longsword",Source="XPHB",Kind="weapon",Definition=new EquipmentItem{Name="Longsword",Description="A blade."}}));
        var cut=context.Render<InventoryDefenseDialogs>(p=>p.Add(c=>c.Session,fixture.Session));await cut.InvokeAsync(()=>cut.Instance.OpenItemAsync());
        var input=cut.Find("#item-name");Assert.IsFalse(string.IsNullOrWhiteSpace(input.GetAttribute("list")));
        var homebrew=(await fixture.Session.HomebrewItemsAsync()).Select(h=>h.Definition.Name).ToArray();Assert.IsNotEmpty(homebrew,"The sample's own items are homebrew.");
        CollectionAssert.AreEquivalent(new[]{"Longsword","Rope"}.Concat(homebrew).ToArray(),cut.FindAll("datalist option").Select(e=>e.GetAttribute("value")).ToArray(),"Official items and the owner's homebrew items.");
        input.Input("Rope");
        StringAssert.Contains(cut.Find("[data-testid=item-catalog-note]").TextContent,"Official item (XPHB)");
        Assert.IsTrue(cut.Find("#equipment-dialog textarea").HasAttribute("disabled"),"An official item's text comes from the rules.");
        Assert.AreEqual("Fifty feet of hempen rope.",cut.Find("#equipment-dialog textarea").GetAttribute("value")??cut.Find("#equipment-dialog textarea").TextContent);
        Assert.IsTrue(cut.Find("#item-unit-weight").HasAttribute("disabled"));
        cut.Find("#item-name").Input("Fictional Moonrope");Assert.AreEqual("Fictional Moonrope",cut.Find("#item-name").GetAttribute("value"));
        StringAssert.Contains(cut.Find("[data-testid=item-catalog-note]").TextContent,"enter a custom item");
        Assert.IsFalse(cut.Find("#equipment-dialog textarea").HasAttribute("disabled"),"A custom item's text is the player's.");
        Assert.IsFalse(cut.Find("#equipment-dialog .button.primary").HasAttribute("disabled"));
    }

    private sealed class Items(params CatalogItem[] official) : IItemCatalog
    {
        public IReadOnlyList<CatalogItem> Official => official;
        public ItemMatch? Match(string name) => official.FirstOrDefault(i => i.Definition.Name == name) is { } item ? new(item, "", 1) : null;
    }
}
