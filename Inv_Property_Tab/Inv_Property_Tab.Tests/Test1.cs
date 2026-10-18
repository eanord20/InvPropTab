using System;

namespace Inv_Property_Tab.Tests;

[TestClass]
public sealed class ConfigurationContractTests
{
    [TestMethod]
    public void ProductionAssemblyExposesFieldsConfigurationStore()
    {
        var type = Type.GetType("Inv_Property_Tab.Configuration.FieldsConfigurationStore, Inv_Property_Tab");

        Assert.IsNotNull(type);
    }
}
