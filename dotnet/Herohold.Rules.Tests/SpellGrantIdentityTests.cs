using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class SpellGrantIdentityTests
{
    [TestMethod] public void ActualLegacySourceFormattingPreservesExistingGrantAndSpentPoolIds()
    {
        var source=JsonNode.Parse("""{"type":"feat","instance_id":"fictional-feat","feat_id":"fey_touched_xphb","source_name":"Fey-Touched"}""")!.AsObject();
        var typed=new SpellGrantSource{Type="feat",InstanceId="fictional-feat",FeatId="fey_touched_xphb",SourceName="Fey-Touched"};
        Assert.AreEqual(SpellGrantIdentity.SerializeSource(source),SpellGrantIdentity.SerializeSource(typed),"A typed source writes the same identity as the document it replaced.");
        var ids=SpellGrantIdentity.ForSource(typed,"Misty Step");
        Assert.AreEqual("grant-31566731f99c94f74d6f9221",ids.GrantId);Assert.AreEqual("grant-uses-31566731f99c94f74d6f9221",ids.ResourceId);
        Assert.AreEqual("{\"feat_id\": \"fey_touched_xphb\", \"instance_id\": \"fictional-feat\", \"source_name\": \"Fey-Touched\", \"type\": \"feat\"}",SpellGrantIdentity.SerializeSource(source));
    }
    [TestMethod] public void UnicodeJsonSourcesAndPlainSpeciesLabelsUseDifferentExactLegacyEncodings()
    {
        var source=JsonNode.Parse("""{"source_name":"Mýthic > Blessing","type":"feat","level":3}""")!.AsObject();
        Assert.AreEqual("grant-08ffdc906021724d08bb51f7",SpellGrantIdentity.ForSource(new SpellGrantSource{SourceName="Mýthic > Blessing",Type="feat",Level=3},"Cure Wounds").GrantId);
        Assert.AreEqual("grant-685fc515953953501159899e",SpellGrantIdentity.ForLabel("Elf · Drow","Darkness").GrantId);
        Assert.AreEqual("{\"level\": 3, \"source_name\": \"M\\u00fdthic > Blessing\", \"type\": \"feat\"}",SpellGrantIdentity.SerializeSource(source));
    }
    [TestMethod] public void NestedSourceOrderingAndEscapesAreDeterministicAndInputIsUnchanged()
    {
        var source=JsonNode.Parse("""{"z":[true,null,"quote\"\n"],"a":{"z":2,"a":1}}""")!.AsObject();var before=source.ToJsonString();
        Assert.AreEqual("{\"a\": {\"a\": 1, \"z\": 2}, \"z\": [true, null, \"quote\\\"\\n\"]}",SpellGrantIdentity.SerializeSource(source));
        Assert.AreEqual(before,source.ToJsonString());
    }
}
