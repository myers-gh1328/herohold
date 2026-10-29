using Bunit;
using Herohold.Core;
using Herohold.TestSupport;
using Herohold.Web.Components.Pages;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Components.Tests;

/// <summary>A named or magic weapon picks the catalog weapon it works as, and attacks as that weapon plus its bonuses.</summary>
[TestCategory("RulesData")]
[TestClass]
public sealed class WeaponItemTests
{
    private string directory = "";
    [TestInitialize] public void Setup() => directory = Path.Combine(Path.GetTempPath(), "herohold-weapon-items-" + Guid.NewGuid().ToString("N"));
    [TestCleanup] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    private static WeaponAttack Attack(CharacterState c, string name) => c.Derived.Attacks.Single(a => a.Name == name);

    [TestMethod]
    public async Task ACustomWeaponSavedWithWorksAsAttacksAsThatWeaponPlusItsBonuses()
    {
        var session = new WorkspaceSession(TestStores.Open(directory), TestStores.Workflow);
        await session.InitializeAsync();
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(session);
        var editor = context.Render<InventoryDefenseDialogs>(p => p.Add(x => x.Session, session));
        await editor.InvokeAsync(() => editor.Instance.OpenItemAsync());
        editor.Find("#item-name").Input("Briarbloom Bow");
        editor.Find("[data-testid='item-base-weapon']").Change("Longbow");
        editor.Find("[data-testid='item-attack-bonus']").Change("1");
        editor.Find("[data-testid='item-damage-bonus']").Change("2");
        editor.FindAll(".equipment-switches label").Single(l => l.TextContent.Trim() == "Equipped").QuerySelector("input")!.Change(true);
        await editor.FindAll("button").Single(b => b.TextContent.Trim() == "Save item").ClickAsync(new());
        Assert.IsNull(session.Error, session.Error);

        var item = session.Character!.Equipment.Single(i => i.Name == "Briarbloom Bow");
        Assert.AreEqual(("Longbow", 1, 2), (item.BaseWeapon, item.AttackBonus, item.DamageBonus));
        var longbow = Attack(session.Character, "Longbow");
        var briarbloom = Attack(session.Character, "Briarbloom Bow");
        Assert.AreEqual(longbow.AttackBonus + 1, briarbloom.AttackBonus);
        Assert.AreEqual(longbow.DamageModifierAdjustment + 2, briarbloom.DamageModifierAdjustment);
        Assert.AreEqual(8, briarbloom.DamageDice!.Sides);

        var home = context.Render<Home>();
        Assert.IsTrue(home.FindAll(".player-attack-row strong").Any(s => s.TextContent == "Briarbloom Bow"), "The sheet lists the custom weapon's attack.");

        await editor.InvokeAsync(() => editor.Instance.OpenItemAsync(item));
        Assert.AreEqual("Longbow", editor.Find("[data-testid='item-base-weapon']").GetAttribute("value"), "Reopening the item shows what it works as.");
    }
}
