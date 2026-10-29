using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestClass]
public sealed class EffectiveConditionsTests
{
    [TestMethod]
    [DataRow(0,true)] [DataRow(1,false)] [DataRow(1,true)]
    public void SharedAndCombatConditionPanelsNeverClaimEmptyWhileEffectiveConditionsExist(int exhaustion,bool itemPoisoned)
    {
        var c=SampleWorkspace.Create().Character with{Exhaustion=exhaustion,Effects=[],Equipment=itemPoisoned?[new(){Id="poisoned-item",Name="Cursed item",Quantity=1,Equipped=true,PassiveEffects=[new(){Id="poison",Name="Poisoned"}]}]:[]};
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.Enter,NewEncounter=true,EncounterId="test",OwnTurnNow=true,TableBoundaryConfirmed=true,BoundaryId="turn"});
        using var ctx=new BunitContext();var shared=ctx.Render<CharacterStatusPanels>(p=>p.Add(x=>x.Character,c));var combat=ctx.Render<CombatWorkspace>(p=>p.Add(x=>x.Character,c));
        var conditions=shared.Find("[data-testid='conditions-panel']").TextContent;var effects=combat.Find("[data-combat-section='effects']").TextContent;
        Assert.IsFalse(conditions.Contains("No conditions or effects tracked",StringComparison.Ordinal));Assert.IsFalse(effects.Contains("No effects tracked",StringComparison.Ordinal));
        if(itemPoisoned){Assert.IsFalse(conditions.Contains("No other effects tracked",StringComparison.Ordinal));Assert.AreEqual(1,shared.FindAll(".derived-effect").Count);StringAssert.Contains(conditions,"Poisoned");StringAssert.Contains(effects,"Poisoned");}
        if(exhaustion>0){StringAssert.Contains(conditions,"Exhaustion 1");StringAssert.Contains(effects,"Exhaustion 1");}
    }
}
