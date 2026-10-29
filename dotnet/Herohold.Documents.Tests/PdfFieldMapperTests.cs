using Herohold.Core;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Documents.Tests;

[TestClass]
public sealed class PdfFieldMapperTests
{
    internal static CharacterState FictionalCharacter()=>new(){Id="fictional-pdf",Name="Éowyn Ω",Species="Elf",ClassName="Ranger",Level=5,MaximumHp=44,CurrentHp=31,TemporaryHp=3,ArmorClass=16,Speed=40,
        AbilityScores=Enum.GetValues<Ability>().ToDictionary(a=>a,a=>a==Ability.Dexterity?18:a==Ability.Wisdom?16:a==Ability.Constitution?14:10),
        SkillProficiencies=["Insight"],Expertise=["Insight"],SaveProficiencies=[Ability.Dexterity],
        Equipment=[new(){Id="charm",Name="Fictional charm",Equipped=true,ModifierBonuses=new(){[Ability.Wisdom]=1}}],
        Resources=[new(){Id="slot1",Name="Level1 slots",SlotLevel=1,Maximum=4,Remaining=2}],
        Play=new(){SpellcastingAbility=Ability.Wisdom,HitDieSize=10,PreparedSpellIds=["cure-wounds"],Spells=[new(){Id="cure-wounds",Name="Cure Wounds",Level=1}]}};
    [TestMethod]
    public void VisiblePdfFieldsUseFinalNativeStatsAndCurrentCountersInsteadOfStaleCanonicalNumbers()
    {
        var fields=PdfFieldMapper.Create(FictionalCharacter());
        Assert.AreEqual("Éowyn Ω",fields["CharacterName"]!.GetValue<string>());
        Assert.AreEqual("Ranger 5",fields["ClassLevel"]!.GetValue<string>());
        Assert.AreEqual("16",fields["WIS"]!.GetValue<string>());
        Assert.AreEqual("+4",fields["WISmod"]!.GetValue<string>());
        Assert.AreEqual("+10",fields["Insight"]!.GetValue<string>());
        Assert.AreEqual("16",fields["AC"]!.GetValue<string>());
        Assert.AreEqual("44",fields["HPMax"]!.GetValue<string>());
        Assert.AreEqual("31",fields["HPCurrent"]!.GetValue<string>());
        Assert.AreEqual("3",fields["HPTemp"]!.GetValue<string>());
        Assert.AreEqual("4",fields["SlotsTotal 19"]!.GetValue<string>());
        Assert.AreEqual("2",fields["SlotsRemaining 19"]!.GetValue<string>());
        Assert.AreEqual("15",fields["SpellSaveDC  2"]!.GetValue<string>());
        Assert.AreEqual("Cure Wounds",fields["Spells 1023"]!.GetValue<string>());
    }
    [TestMethod]
    public void PrintedFeaturesRetainSelectedFeatChoicesAndCurrentResourceCounts()
    {
        var build=new BuildFacts{Feats=[new(){Name="Skill Expert",Choices=new(){["ability"]=ChoiceValue.Of("Wisdom"),["skill_proficiency"]=ChoiceValue.Of("Insight"),["expertise"]=ChoiceValue.Of("Stealth")}}],WeaponMasteries=["Longbow"]};
        var fields=PdfFieldMapper.Create(FictionalCharacter() with{Build=build,Features=["Skill Expert"],Derived=new(){FeatureDescriptions=new(){["Skill Expert"]="Fictional rules reference."}}});
        var text=System.Text.RegularExpressions.Regex.Replace(fields["Features and Traits"]!.GetValue<string>(),@"\s+"," ");
        Assert.IsTrue(text.Contains("Skill Proficiency: Insight"));
        Assert.IsTrue(text.Contains("Expertise: Stealth"));
        Assert.IsTrue(text.Contains("Wisdom"));
        Assert.IsTrue(text.Contains("Level1 slots: 2/4"));
        Assert.IsTrue(text.Contains("Weapon mastery: Longbow"));
    }
    [TestMethod]
    public void NativeBuilderFeatureReferencesDoNotRequireLegacyDerivedDescriptionFields()
    {
        var character=FictionalCharacter() with{Features=["Fictional native feature"],Derived=new(){FeatureDetails=[new(){Name="Fictional native feature",Description="A native catalog description.",RelatedFeatures=[new(){Name="Related choice",Description="Selected native reference."}]}]}};
        var fields=PdfFieldMapper.Create(character);
        var text=System.Text.RegularExpressions.Regex.Replace(fields["Features and Traits"]!.GetValue<string>(),@"\s+"," ");
        Assert.IsTrue(text.Contains("native catalog description."));
        Assert.IsTrue(text.Contains("Selected native reference."));
        Assert.IsFalse(text.Contains("Cost:"));
    }

    [TestMethod]
    public void MulticlassPdfShowsActualClassLevelsAndMixedHitDice()
    {
        var c=FictionalCharacter() with{ClassName="Ranger",Level=9,Build=new(){ClassLevels=new(){["Ranger"]=5,["Wizard"]=4}},Play=FictionalCharacter().Play with{HitDiePools=[new(){Id="d10",Sides=10,Maximum=5,Remaining=3},new(){Id="d6",Sides=6,Maximum=4,Remaining=2}]}};
        var fields=PdfFieldMapper.Create(c);
        Assert.AreEqual("Ranger 5 / Wizard 4",fields["ClassLevel"]!.GetValue<string>());
        Assert.AreEqual("4d6 + 5d10",fields["HDTotal"]!.GetValue<string>());
        Assert.AreEqual("2d6 + 3d10",fields["HD"]!.GetValue<string>());
    }

    [TestMethod]
    public void PdfDoesNotPromoteUnownedCantripReferenceRecordsToKnownSpells()
    {
        var c=FictionalCharacter() with{Play=FictionalCharacter().Play with{KnownSpellIds=["light"],Spells=[new(){Id="light",Name="Light",Level=0},new(){Id="fire-bolt",Name="Fire Bolt",Level=0}]}};
        var fields=PdfFieldMapper.Create(c);
        StringAssert.Contains(fields.ToJsonString(),"Light");
        Assert.IsFalse(fields.ToJsonString().Contains("Fire Bolt",StringComparison.Ordinal),"Available reference records are not spell ownership.");
    }
}
