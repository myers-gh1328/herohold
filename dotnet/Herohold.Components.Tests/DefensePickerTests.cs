using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Components.Tests;

[TestClass]
public sealed class DefensePickerTests
{
    private string directory="";
    [TestInitialize]public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"herohold-defense-picker-"+Guid.NewGuid().ToString("N"));
    [TestCleanup]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    private static List<DamageDefense> Defenses()=>[new(){Kind="resistance",Type="Fire",Source="Ring",Note="While worn"},new(){Kind="resistance",Type="Fire",Source="Spell",Note="One hour"},new(){Kind="resistance",Type="Moonlight",Source="Legacy import",Note="Custom table rule"}];
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ManualDefenseChoicesDependOnKindAndPreserveLegacyRowsAndOtherSources(bool target)
    {
        var c=SampleWorkspace.Create().Character with{Id="defense-picker",Build=null,Defenses=Defenses(),KnownTarget=new(){Name="Quarry",Knowledge="recorded",Defenses=Defenses()}};var repo=HandMadeStore.At(directory);await repo.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repo);await session.SwitchCharacterAsync(c.Id);
        using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;var cut=context.Render<InventoryDefenseDialogs>(p=>p.Add(x=>x.Session,session));await cut.InvokeAsync(()=>cut.Instance.OpenDefensesAsync(target));
        Assert.AreEqual("SELECT",cut.Find("#defense-type").TagName);CollectionAssert.AreEqual(SheetRules.DamageTypes.ToArray(),cut.FindAll("#defense-type option[value]:not([value=''])").Select(o=>o.GetAttribute("value")).ToArray());
        cut.Find("#defense-type").Change("Fire");cut.Find("#defense-kind").Change("conditionImmunity");Assert.AreEqual("",cut.Find("#defense-type").GetAttribute("value"));CollectionAssert.AreEqual(SheetRules.ConditionTypes.ToArray(),cut.FindAll("#defense-type option[value]:not([value=''])").Select(o=>o.GetAttribute("value")).ToArray());Assert.IsTrue(cut.Find("[data-testid='stage-defense']").HasAttribute("disabled"));
        var legacy=cut.Find("[data-testid='legacy-defense']");StringAssert.Contains(legacy.TextContent,"Moonlight");StringAssert.Contains(legacy.TextContent,"Custom table rule");Assert.AreEqual(0,legacy.QuerySelectorAll("input,select").Length);
        cut.Find("#defense-type").Change("Poisoned");cut.Find("#defense-source").Change("Venom ward");cut.Find("#defense-note").Change("One minute");await cut.Find("[data-testid='stage-defense']").ClickAsync(new());Assert.AreEqual(3,(target?session.Character!.KnownTarget.Defenses:session.Character!.Defenses).Count);StringAssert.Contains(cut.Find(".defense-editor-list").TextContent,"Condition immunity: Poisoned");Assert.IsFalse(cut.Find(".defense-editor-list").TextContent.Contains("conditionImmunity"));
        await cut.Find("button[aria-label='Remove resistance Fire from Ring']").ClickAsync(new());await cut.Find("[data-testid='save-defenses']").ClickAsync(new());Assert.IsNull(session.Error);
        var saved=target?session.Character!.KnownTarget.Defenses:session.Character!.Defenses;Assert.AreEqual(3,saved.Count);Assert.IsFalse(saved.Any(d=>d.Source=="Ring"));Assert.IsTrue(saved.Any(d=>d.Type=="Fire"&&d.Source=="Spell"));Assert.IsTrue(saved.Any(d=>d.Type=="Moonlight"&&d.Note=="Custom table rule"));Assert.IsTrue(saved.Any(d=>d.Kind=="conditionImmunity"&&d.Type=="Poisoned"&&d.Source=="Venom ward"));
        var untouched=target?session.Character!.Defenses:session.Character!.KnownTarget.Defenses;Assert.IsTrue(untouched.Any(d=>d.Source=="Ring"));await session.ReloadAsync();Assert.IsNull(session.Error);
    }
    [TestMethod]
    public void ItemDefenseChoicesResetToRequiredBlankAndKeepUnknownSourceFactsReadOnly()
    {
        using var context=new BunitContext();var item=new EquipmentItem{Id="legacy-item",Name="Ward",Defenses=[Defenses().Last()]};EquipmentItem? changed=null;var cut=context.Render<ItemFactsEditor>(p=>p.Add(x=>x.Value,item).Add(x=>x.ValueChanged,value=>changed=value));
        Assert.IsTrue(cut.Find("[data-testid='stage-item-defense']").HasAttribute("disabled"));cut.Find("#item-fact-defense-type").Change("Fire");cut.Find("#item-fact-defense-kind").Change("conditionImmunity");Assert.AreEqual("",cut.Find("#item-fact-defense-type").GetAttribute("value"));Assert.IsTrue(cut.Find("[data-testid='stage-item-defense']").HasAttribute("disabled"));
        CollectionAssert.AreEqual(SheetRules.ConditionTypes.ToArray(),cut.FindAll("#item-fact-defense-type option[value]:not([value=''])").Select(o=>o.GetAttribute("value")).ToArray());Assert.AreEqual(1,cut.FindAll("button[aria-label='Remove item defense Moonlight']").Count);
        cut.Find("#item-fact-defense-type").Change("Poisoned");cut.Find("[data-testid='stage-item-defense']").Click();Assert.IsNotNull(changed);Assert.AreEqual(2,changed.Defenses.Count);Assert.AreEqual("Moonlight",changed.Defenses[0].Type);Assert.AreEqual("Legacy import",changed.Defenses[0].Source);Assert.AreEqual(1,item.Defenses.Count);
    }
}
