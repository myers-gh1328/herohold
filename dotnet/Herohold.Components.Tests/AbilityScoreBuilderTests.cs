using Herohold.Web.Services;
using System.Text.Json.Nodes;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;

[TestClass]
public sealed class AbilityScoreBuilderTests
{
    [TestMethod]
    public void RenderingOrSelectingRollMethodNeverGeneratesScoresUntilTheExplicitButton()
    {
        using var context=new BunitContext();var character=new DraftSummary{AbilityMethod="Manual entry"};var generated=0;string? selected=null;
        var cut=context.Render<AbilityScoreBuilder>(p=>p.Add(c=>c.Character,character).Add(c=>c.MethodChanged,value=>selected=value).Add(c=>c.GenerateRequested,_=>generated++));
        Assert.AreEqual(0,generated);cut.Find("[data-testid=ability-method]").Change("Roll 4d6");Assert.AreEqual("Roll 4d6",selected);Assert.AreEqual(0,generated);
        character=character with{AbilityMethod="Roll 4d6"};cut.Render(p=>p.Add(c=>c.Character,character));
        Assert.AreEqual(0,generated);Assert.AreEqual(0,cut.FindAll("[data-ability-assignment]").Count);
        cut.Find("[data-testid=generate-ability-scores]").Click();Assert.AreEqual(1,generated);
        cut.Render();Assert.AreEqual(1,generated,"A render must retain the last set instead of rolling again.");
    }
    [TestMethod]
    public void DuplicateValuedScoresKeepDistinctPositionsAndEmitOneAssignmentWithoutMutatingTheDraft()
    {
        var c=new DraftSummary{AbilityMethod="Roll 4d6",RolledScores=[15,15,12,10,8,7],Assignments=new(){[Ability.Strength]=0,[Ability.Dexterity]=1,[Ability.Constitution]=2,[Ability.Intelligence]=3,[Ability.Wisdom]=4,[Ability.Charisma]=5}};var before=System.Text.Json.JsonSerializer.Serialize(c);
        using var context=new BunitContext();(Ability Ability,int Position)? assigned=null;var cut=context.Render<AbilityScoreBuilder>(p=>p.Add(x=>x.Character,c).Add(x=>x.AssignmentChanged,value=>assigned=value));
        Assert.AreEqual(6,cut.FindAll("[data-ability-assignment]").Count);var strength=cut.Find("[data-ability-assignment=Strength]");
        Assert.AreEqual("15 · score 1",strength.QuerySelector("option[value='0']")!.TextContent);Assert.AreEqual("15 · score 2",strength.QuerySelector("option[value='1']")!.TextContent);
        strength.Change("1");Assert.AreEqual((Ability.Strength,1),assigned!.Value);Assert.AreEqual(before,System.Text.Json.JsonSerializer.Serialize(c));
        cut.Render(p=>p.Add(x=>x.Busy,true));Assert.IsTrue(cut.FindAll("select,button").All(e=>e.HasAttribute("disabled")));
    }
}
