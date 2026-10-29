using Herohold.TestSupport;
using System.Text.Json;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestClass]
public sealed class SheetMagicReferenceTests
{
    [TestMethod]
    public async Task LongRestReplacementDefaultsOffAndShowsOnlyOwnedWizardBookChoices()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-rest-reference-"+Guid.NewGuid().ToString("N"));
        try
        {
            var c=Herohold.Web.Services.SampleWorkspace.Create().Character with{Id="wizard-rest-reference",Build=null,ClassName="Wizard",Play=new(){PreparedSpellIds=["owned"],SpellbookIds=["owned"],Spells=[new(){Id="owned",Name="Owned spell",Level=1},new(){Id="unowned",Name="Not in my book",Level=1}],Preparation=new(){CanChangeOnLongRest=true,FromSpellbook=true,PreparedLimit=1,EligibleSpellIds=["owned","unowned"]}}};
            var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c},0);var session=new Herohold.Web.Services.WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);
            using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;var cut=context.Render<GameplayDialogs>(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session));
            await cut.InvokeAsync(()=>cut.Instance.OpenRestAsync(RestKind.Long));Assert.IsFalse(cut.Find("#rest-change-spells").HasAttribute("checked"));Assert.AreEqual(0,cut.FindAll("#rest-spell-picker").Count);
            cut.Find("#rest-change-spells").Change(true);var picker=cut.Find("#rest-spell-picker").TextContent;StringAssert.Contains(picker,"Owned spell");Assert.IsFalse(picker.Contains("Not in my book"),"A Wizard must not be offered unowned spellbook preparations.");
            cut.Find("#rest-dialog .form-footer button").Click();CollectionAssert.AreEqual(new[]{"owned"},session.Character!.Play.PreparedSpellIds);Assert.AreEqual(1L,session.Snapshot!.Revision);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [TestMethod]
    public async Task HeroicInspirationCanReplaceThePhysicalD20AndRequiresTheNewLowerResult()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-inspiration-component-"+Guid.NewGuid().ToString("N"));
        try
        {
            var c=Herohold.Web.Services.SampleWorkspace.Create().Character with{Id="heroic-reference",Build=null,Sheet=new(){HeroicInspiration=true}};
            var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c},0);var session=new Herohold.Web.Services.WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);
            using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;var cut=context.Render<GameplayDialogs>(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session).Add(x=>x.PreferredDiceMode,DiceMode.Physical));
            await cut.InvokeAsync(()=>cut.Instance.OpenRollAsync(new(){Name="Wisdom check",Kind=RollKind.Ability,Ability=Ability.Wisdom},CharacterCalculations.AbilityCheck(c,Ability.Wisdom)));
            cut.Find("#physical-d20").Change("12");cut.Find("[data-testid='roll-digital']").Click();cut.WaitForAssertion(()=>Assert.IsNotNull(session.Character!.Play.LastRoll));
            Assert.IsNotNull(cut.Find("[data-testid='heroic-inspiration-controls']"));cut.Find("#heroic-replacement-die").Change("4");cut.Find("[data-testid='use-heroic-inspiration']").Click();
            cut.WaitForAssertion(()=>Assert.IsFalse(SheetRules.HasHeroicInspiration(session.Character!)));Assert.AreEqual(4,session.Character!.Play.LastRoll!.ChosenD20);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [TestMethod]
    public async Task UnarmedHitDoesNotOfferWeaponOnlyDreadfulStrikeAsAnEnabledChoice()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-unarmed-ui-"+Guid.NewGuid().ToString("N"));
        try
        {
            var c=Herohold.Web.Services.SampleWorkspace.Create().Character with{Id="unarmed-effects-reference",Build=null,Resources=[new(){Id="dread",Name="Dreadful Strike",Maximum=1,Remaining=1}]};
            var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c},0);var session=new Herohold.Web.Services.WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);
            using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;var cut=context.Render<GameplayDialogs>(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session).Add(x=>x.PreferredDiceMode,DiceMode.Physical));
            var request=JsonSerializer.Deserialize<RollRequest>("""{"kind":0,"attackKind":2,"name":"Unarmed strike","ability":0}""",new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
            await cut.InvokeAsync(()=>cut.Instance.OpenRollAsync(request,CharacterCalculations.Attack(c,Ability.Strength,true),baseFixedDamage:1));cut.Find("#physical-d20").Change("12");cut.Find("[data-testid='roll-digital']").Click();cut.WaitForAssertion(()=>Assert.IsNotNull(session.Character!.Play.LastRoll));
            Assert.IsTrue(cut.Find("#effect-dread").HasAttribute("disabled"),"An unarmed hit cannot trigger weapon-only Dreadful Strike.");
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [TestMethod]
    public void CastingSummaryRetainsDifferentSourceAbilitiesAndExplainedTotals()
    {
        using var context=new BunitContext();
        var character=Herohold.Web.Services.SampleWorkspace.Create().Character with{Equipment=[],AbilityScores=new(){[Ability.Strength]=10,[Ability.Dexterity]=10,[Ability.Constitution]=10,[Ability.Intelligence]=18,[Ability.Wisdom]=14,[Ability.Charisma]=10},Derived=new(){SpellcastingSources=[new(){SourceName="Wizard",SourceType="class",Ability=Ability.Intelligence},new(){SourceName="Wisdom gift",SourceType="feat",Ability=Ability.Wisdom}]}};
        var cut=context.Render<MagicCastingSummary>(p=>p.Add(x=>x.Character,character));
        Assert.AreEqual(2,cut.FindAll("[data-casting-source]").Count);StringAssert.Contains(cut.Markup,"Wizard");StringAssert.Contains(cut.Markup,"Wisdom gift");
        var wizard=cut.Find("[data-casting-source='Wizard']").TextContent;var gift=cut.Find("[data-casting-source='Wisdom gift']").TextContent;
        StringAssert.Contains(wizard,"Intelligence");StringAssert.Contains(wizard,"+4");StringAssert.Contains(wizard,"Spell save DC");StringAssert.Contains(wizard,"Spell attack");StringAssert.Contains(gift,"Wisdom");StringAssert.Contains(gift,"+2");
    }
    [TestMethod]
    public async Task SpellInspectorShowsComponentsMaterialsDurationAndSourceWithoutSpendingAnything()
    {
        using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;
        var spell=JsonSerializer.Deserialize<SpellDefinition>("""{"id":"material-spell","name":"Material spell","level":1,"source":"XPHB","castingTime":"Action","range":"Touch","description":"Reference only.","components":["V","S","M"],"componentsKnown":true,"materialDescription":"a gem worth 50 GP","materialCostCp":5000,"materialConsumed":true,"durationDescription":"1 hour"}""",new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        var castRequests=new List<SpellDefinition>();var cut=context.Render<SpellInspector>(p=>p.Add(x=>x.OnCast,s=>castRequests.Add(s)));
        await cut.InvokeAsync(()=>cut.Instance.OpenAsync(spell));
        var facts=cut.Find("[data-testid='spell-reference-facts']").TextContent;
        StringAssert.Contains(facts,"V, S, M");StringAssert.Contains(facts,"50 GP");StringAssert.Contains(facts,"Consumed");StringAssert.Contains(facts,"1 hour");StringAssert.Contains(facts,"XPHB");
        Assert.AreEqual(0,castRequests.Count);
    }
}
