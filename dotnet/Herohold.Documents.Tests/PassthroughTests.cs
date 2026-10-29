using Herohold.Core;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Documents.Tests;

[TestClass]
public sealed class PassthroughTests
{
    [TestMethod]
    public void OnlySafeScalarFieldsAreCopiedWithoutChangingTheInput()
    {
        var input=new JsonObject{["PlayerName"]="Fictional Player",["Age"]="120",["GP"]=12.5,["Check Box 12"]=true,["HPMax"]="999",["CHARACTER IMAGE"]="unsafe",["Backstory"]=new JsonObject{["nested"]="unsupported"}};
        var result=PdfPassthrough.Normalize(input);
        Assert.AreEqual(4,result.Count);
        Assert.AreEqual("Fictional Player",result["PlayerName"]!.GetValue<string>());
        Assert.AreEqual("12.5",result["GP"]!.GetValue<string>());
        Assert.IsTrue(result["Check Box 12"]!.GetValue<bool>());
        Assert.IsTrue(input.ContainsKey("HPMax"));
    }
}
