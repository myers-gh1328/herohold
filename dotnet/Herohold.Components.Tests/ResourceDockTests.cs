using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Components.Tests;

[TestClass]
public sealed class ResourceDockTests
{
    [TestMethod]
    public void VerticalDockShowsEveryPoolAndForwardsGuardedCounterAndUndoCommands()
    {
        using var context=new BunitContext();var c=SampleWorkspace.Create().Character;var requests=new List<(string,int,string)>();var undos=0;
        var cut=context.Render<PlayerResourceDock>(p=>p.Add(x=>x.Character,c).Add(x=>x.OnAdjust,v=>requests.Add(v)).Add(x=>x.OnUndo,()=>undos++));
        Assert.AreEqual(1,cut.FindAll("[data-testid='resource-strip']").Count);Assert.AreEqual(c.Resources.Count,cut.FindAll(".resource-strip--vertical .resource-counter").Count);
        foreach(var pool in c.Resources)Assert.AreEqual(pool.Remaining.ToString(),cut.Find($"[data-testid='resource-{pool.Id}']").TextContent);
        var poolToSpend=c.Resources.First(r=>r.Remaining>0);cut.Find($"button[aria-label='Use one {poolToSpend.Name}']").Click();CollectionAssert.AreEqual(new[]{(poolToSpend.Id,-1,c.Id)},requests);Assert.AreEqual(poolToSpend.Remaining,c.Resources.Single(r=>r.Id==poolToSpend.Id).Remaining);
        Assert.IsTrue(cut.Find("[data-testid='undo-play']").HasAttribute("disabled"));
        var changed=PlayRules.ChangeHealth(new(){Character=c,Revision=1},new(){SourceCharacterId=c.Id,SourceRevision=1,Kind=HealthChangeKind.Damage,Amount=1});cut.Render(p=>p.Add(x=>x.Character,changed));cut.Find("[data-testid='undo-play']").Click();Assert.AreEqual(1,undos);
        cut.Render(p=>p.Add(x=>x.Busy,true));Assert.IsTrue(cut.FindAll(".resource-counter button,[data-testid='undo-play']").All(b=>b.HasAttribute("disabled")));
    }
    [TestMethod]
    public void AppearanceSettingsKeepOneRealSelectInsideAnExplicitMenuAndForwardChanges()
    {
        using var context=new BunitContext();string? selected=null;var cut=context.Render<PlayerResourceDock>(p=>p.Add(x=>x.Character,SampleWorkspace.Create().Character).Add(x=>x.Theme,"dark").Add(x=>x.OnThemeChange,e=>selected=e.Value?.ToString()));
        var menu=cut.Find("details.appearance-menu");Assert.IsFalse(menu.HasAttribute("open"));Assert.AreEqual("Appearance",menu.QuerySelector("summary")!.TextContent.Trim());var select=menu.QuerySelector("[data-testid='theme']")!;
        Assert.AreEqual(1,cut.FindAll("select").Count);Assert.AreEqual("dark",select.GetAttribute("value"));CollectionAssert.AreEqual(new[]{"system","light","dark"},select.QuerySelectorAll("option").Select(x=>x.GetAttribute("value")).ToArray());
        cut.Find("[data-testid='theme']").Change("light");Assert.AreEqual("light",selected);
        cut.Render(p=>p.Add(x=>x.ShowAppearance,false));Assert.AreEqual(0,cut.FindAll("[data-testid='theme']").Count);
    }
    [TestMethod]
    [DataRow(false,0)]
    [DataRow(true,0)]
    [DataRow(false,2)]
    [DataRow(true,2)]
    public void ConditionNoticePersistsUntilActualEffectsAndExhaustionAreRemoved(bool hasEffect,int exhaustion)
    {
        using var context=new BunitContext();var manages=0;var c=SampleWorkspace.Create().Character with{Exhaustion=exhaustion,Effects=hasEffect?[new(){Id="poison",Name="Poisoned",Source="Trap"}]:[]};
        var cut=context.Render<PersistentConditions>(p=>p.Add(x=>x.Character,c).Add(x=>x.OnManage,()=>manages++));
        if(!hasEffect&&exhaustion==0){Assert.AreEqual(0,cut.FindAll("[data-testid='persistent-conditions']").Count);return;}
        var notice=cut.Find("[data-testid='persistent-conditions']");if(hasEffect)StringAssert.Contains(notice.TextContent,"Poisoned");if(exhaustion>0)StringAssert.Contains(notice.TextContent,"Exhaustion 2");
        Assert.AreEqual(1,notice.QuerySelectorAll("button").Length);cut.Find("[data-testid='manage-persistent-conditions']").Click();Assert.AreEqual(1,manages);
        cut.Render(p=>p.Add(x=>x.Character,c));Assert.AreEqual(1,cut.FindAll("[data-testid='persistent-conditions']").Count,"Opening management does not dismiss the reminder.");
        cut.Render(p=>p.Add(x=>x.Character,c with{Effects=[],Exhaustion=0}));Assert.AreEqual(0,cut.FindAll("[data-testid='persistent-conditions']").Count);
    }
}
