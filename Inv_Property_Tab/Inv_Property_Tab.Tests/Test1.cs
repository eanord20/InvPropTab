using System.Linq;
using Inv_Property_Tab.Configuration;

namespace Inv_Property_Tab.Tests;

[TestClass]
public sealed class FieldsConfigurationValidatorTests
{
    private readonly FieldsConfigurationValidator _validator = new FieldsConfigurationValidator();

    [TestMethod]
    public void ValidateRejectsUnsupportedVersion()
    {
        var configuration = new FieldsConfiguration { Version = 2 };

        var errors = _validator.Validate(configuration);

        Assert.IsTrue(errors.Any(error => error.Contains("version")));
    }

    [TestMethod]
    public void ValidateRejectsDuplicateFieldIds()
    {
        var configuration = ValidConfiguration();
        configuration.Fields.Add(new FieldDefinition { Id = "description", Label = "Другое", PropertyName = "Project" });

        var errors = _validator.Validate(configuration);

        Assert.IsTrue(errors.Any(error => error.Contains("description")));
    }

    [TestMethod]
    public void ValidateAcceptsValidDescriptionWithoutHistory()
    {
        var configuration = ValidConfiguration();

        var errors = _validator.Validate(configuration);

        Assert.IsEmpty(errors);
        Assert.IsFalse(configuration.Fields.Single().RememberValues);
        Assert.AreEqual(FieldEditors.Text, configuration.Fields.Single().Editor);
    }

    private static FieldsConfiguration ValidConfiguration()
    {
        return new FieldsConfiguration
        {
            Fields =
            {
                new FieldDefinition
                {
                    Id = "description",
                    Label = "Наименование",
                    Source = FieldSources.Standard,
                    PropertyName = "Description",
                    Editor = FieldEditors.Text,
                    Order = 20,
                    Visible = true,
                    RememberValues = false
                }
            }
        };
    }
}
