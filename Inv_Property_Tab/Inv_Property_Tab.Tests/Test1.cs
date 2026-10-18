using System;
using System.IO;
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

[TestClass]
public sealed class FieldsConfigurationStoreTests
{
    private string _directory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "InvPropTab.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [TestMethod]
    public void MissingFileLoadsDefaultDescriptionWithoutHistory()
    {
        var store = CreateStore();

        var configuration = store.Load();
        var description = configuration.Fields.Single(field => field.Id == "description");

        Assert.AreEqual(FieldEditors.Text, description.Editor);
        Assert.IsFalse(description.RememberValues);
        Assert.IsFalse(File.Exists(Path.Combine(_directory, "fields.json")));
        Assert.IsNotNull(typeof(FieldsConfigurationStore).GetMethod("LoadDefault"));
        Assert.IsTrue(store.LoadDefault().Fields.Any(field => field.Id == "material"));
    }

    [TestMethod]
    public void SaveAndLoadRoundTripsConfiguration()
    {
        var store = CreateStore();
        var configuration = new FieldsConfiguration
        {
            Fields =
            {
                new FieldDefinition
                {
                    Id = "project",
                    Label = "Проект",
                    Source = FieldSources.Standard,
                    PropertyName = "Project",
                    Editor = FieldEditors.List,
                    Values = { "A", "B" },
                    RememberValues = true
                }
            }
        };

        store.Save(configuration);
        var loaded = store.Load();

        Assert.AreEqual("project", loaded.Fields.Single().Id);
        CollectionAssert.AreEqual(new[] { "A", "B" }, loaded.Fields.Single().Values);
    }

    [TestMethod]
    public void InvalidFileFallsBackWithoutOverwritingSource()
    {
        var path = Path.Combine(_directory, "fields.json");
        File.WriteAllText(path, "{invalid json");
        var store = CreateStore();

        var configuration = store.Load();

        Assert.IsTrue(configuration.Fields.Any(field => field.Id == "description"));
        Assert.AreEqual("{invalid json", File.ReadAllText(path));
    }

    private FieldsConfigurationStore CreateStore()
    {
        return (FieldsConfigurationStore)Activator.CreateInstance(typeof(FieldsConfigurationStore), _directory)!;
    }
}

[TestClass]
public sealed class DescriptionEditorTests
{
    [TestMethod]
    public void DescriptionUsesTextBoxWithoutHistoryStore()
    {
        var type = typeof(Inv_Property_Tab.UI.PropUI.PropBrowserControl);
        var descriptionField = type.GetField("_tbDescription", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var historyField = type.GetField("_descriptionStore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.IsNotNull(descriptionField);
        Assert.AreEqual("System.Windows.Forms.TextBox", descriptionField.FieldType.FullName);
        Assert.IsNull(historyField);
    }
}

[TestClass]
public sealed class ComOwnershipTests
{
    [TestMethod]
    public void PaneManagerDoesNotRetainPerDocumentComObjects()
    {
        var type = typeof(Inv_Property_Tab.UI.PropBrowserPaneManager);
        var cache = type.GetField("_paneByDocKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.IsNull(cache);
        Assert.IsTrue(typeof(IDisposable).IsAssignableFrom(type));
    }

    [TestMethod]
    public void PropertyWriteResultExposesSuccessAndError()
    {
        var assembly = typeof(Inv_Property_Tab.Services.IPropertiesService).Assembly;
        var resultType = assembly.GetType("Inv_Property_Tab.Services.PropertyWriteResult");
        var accessorType = assembly.GetType("Inv_Property_Tab.Services.IPropertyAccessor");

        Assert.IsNotNull(resultType);
        Assert.IsNotNull(accessorType);
        Assert.IsNotNull(resultType.GetProperty("Success"));
        Assert.IsNotNull(resultType.GetProperty("Error"));
    }

    [TestMethod]
    public void ComReleaseIgnoresNullAndManagedObjects()
    {
        var type = typeof(Inv_Property_Tab.Services.IPropertiesService).Assembly.GetType("Inv_Property_Tab.Interop.ComRelease");
        Assert.IsNotNull(type);
        var method = type.GetMethod("Final");
        Assert.IsNotNull(method);

        method.Invoke(null, new object?[] { null });
        method.Invoke(null, new object[] { new object() });
    }
}

[TestClass]
public sealed class InventorPropertyCatalogTests
{
    [TestMethod]
    public void CatalogHasUniqueIdsAndRememberedSupplier()
    {
        var type = typeof(FieldDefinition).Assembly.GetType("Inv_Property_Tab.Configuration.InventorPropertyCatalog");
        Assert.IsNotNull(type);
        var fields = ((System.Collections.Generic.IEnumerable<FieldDefinition>)type.GetProperty("All")!.GetValue(null)!).ToList();

        Assert.AreEqual(fields.Count, fields.Select(field => field.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var supplier = fields.Single(field => field.Id == "supplier");
        Assert.AreEqual(FieldEditors.List, supplier.Editor);
        Assert.IsTrue(supplier.RememberValues);
    }

    [TestMethod]
    public void CatalogNeverAllowsDescriptionHistory()
    {
        var type = typeof(FieldDefinition).Assembly.GetType("Inv_Property_Tab.Configuration.InventorPropertyCatalog");
        Assert.IsNotNull(type);
        var fields = ((System.Collections.Generic.IEnumerable<FieldDefinition>)type.GetProperty("All")!.GetValue(null)!).ToList();

        var description = fields.Single(field => field.Id == "description");
        Assert.AreEqual(FieldEditors.Text, description.Editor);
        Assert.IsFalse(description.RememberValues);
    }
}

[TestClass]
public sealed class AppLoggerTests
{
    [TestMethod]
    public void LoggerWritesMessageWithoutExceptionDetailsFromUserData()
    {
        var directory = Path.Combine(Path.GetTempPath(), "InvPropTab.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var loggerType = typeof(Inv_Property_Tab.Services.IPropertiesService).Assembly.GetType("Inv_Property_Tab.Diagnostics.AppLogger");
            Assert.IsNotNull(loggerType);
            var logger = Activator.CreateInstance(loggerType, directory);
            loggerType.GetMethod("Error")!.Invoke(logger, new object[] { "Read property failed", new InvalidOperationException("COM failure") });

            var text = File.ReadAllText(Path.Combine(directory, "invproptab.log"));
            StringAssert.Contains(text, "Read property failed");
            StringAssert.Contains(text, "COM failure");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}

[TestClass]
public sealed class FieldEditorFactoryTests
{
    [TestMethod]
    public void TextFieldCreatesTextBox()
    {
        var typeName = CreateControlTypeName(new FieldDefinition
        {
            Id = "description",
            Label = "Наименование",
            Editor = FieldEditors.Text
        });

        Assert.AreEqual("System.Windows.Forms.TextBox", typeName);
    }

    [TestMethod]
    public void ListFieldCreatesComboBox()
    {
        var typeName = CreateControlTypeName(new FieldDefinition
        {
            Id = "project",
            Label = "Проект",
            Editor = FieldEditors.List,
            Values = { "A" }
        });

        Assert.AreEqual("System.Windows.Forms.ComboBox", typeName);
    }

    [TestMethod]
    public void SupplierValueIsRememberedWithoutCaseDuplicates()
    {
        var definition = new FieldDefinition { Id = "supplier", Label = "Поставщик", Editor = FieldEditors.List, RememberValues = true };
        var results = InvokeRememberOnSta(definition, "  Bosch  ", "bosch");

        CollectionAssert.AreEqual(new[] { true, false }, results);
        CollectionAssert.AreEqual(new[] { "Bosch" }, definition.Values);
    }

    [TestMethod]
    public void DescriptionValueIsNeverRemembered()
    {
        var definition = new FieldDefinition { Id = "description", Label = "Наименование", Editor = FieldEditors.List, RememberValues = true };
        var results = InvokeRememberOnSta(definition, "Корпус");

        CollectionAssert.AreEqual(new[] { false }, results);
        Assert.IsEmpty(definition.Values);
    }

    [TestMethod]
    public void ChangedValueHighlightsAndAcceptedValueRestoresColor()
    {
        var result = InvokeDirtyStateOnSta();

        Assert.IsTrue(result.ChangedColorDiffers);
        Assert.IsTrue(result.AcceptedColorRestored);
    }

    [TestMethod]
    public void ConfigurablePanelExposesDocumentOperations()
    {
        var type = typeof(FieldDefinition).Assembly.GetType("Inv_Property_Tab.UI.Fields.ConfigurableFieldsPanel");

        Assert.IsNotNull(type);
        Assert.IsNotNull(type.GetMethod("LoadDocument"));
        Assert.IsNotNull(type.GetMethod("Apply"));
        Assert.IsNotNull(type.GetMethod("SetReadOnly"));
    }

    [TestMethod]
    public void SettingsFormExposesValidatedConfiguration()
    {
        var type = typeof(FieldDefinition).Assembly.GetType("Inv_Property_Tab.UI.Settings.FieldsSettingsForm");

        Assert.IsNotNull(type);
        Assert.IsNotNull(type.GetProperty("Configuration"));
    }

    private static (bool ChangedColorDiffers, bool AcceptedColorRestored) InvokeDirtyStateOnSta()
    {
        (bool ChangedColorDiffers, bool AcceptedColorRestored) result = default;
        Exception? failure = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var factoryType = typeof(FieldDefinition).Assembly.GetType("Inv_Property_Tab.UI.Fields.FieldEditorFactory")!;
                var factory = Activator.CreateInstance(factoryType);
                var definition = new FieldDefinition { Id = "project", Label = "Проект", Editor = FieldEditors.Text };
                var binding = factoryType.GetMethod("Create")!.Invoke(factory, new object[] { definition })!;
                var control = binding.GetType().GetProperty("Control")!.GetValue(binding)!;
                var backColor = control.GetType().GetProperty("BackColor")!;
                var originalColor = backColor.GetValue(control);
                binding.GetType().GetMethod("AcceptValue")!.Invoke(binding, new object[] { "A" });
                binding.GetType().GetProperty("Value")!.SetValue(binding, "B");
                binding.GetType().GetMethod("RefreshDirtyState")!.Invoke(binding, null);
                var changedColor = backColor.GetValue(control);
                binding.GetType().GetMethod("AcceptValue")!.Invoke(binding, new object[] { "B" });
                var acceptedColor = backColor.GetValue(control);
                result = (!Equals(originalColor, changedColor), Equals(originalColor, acceptedColor));
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null)
            throw failure;
        return result;
    }

    private static bool[] InvokeRememberOnSta(FieldDefinition definition, params string[] values)
    {
        bool[]? results = null;
        Exception? failure = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var factoryType = typeof(FieldDefinition).Assembly.GetType("Inv_Property_Tab.UI.Fields.FieldEditorFactory")!;
                var factory = Activator.CreateInstance(factoryType);
                var binding = factoryType.GetMethod("Create")!.Invoke(factory, new object[] { definition })!;
                var valueProperty = binding.GetType().GetProperty("Value")!;
                var rememberMethod = binding.GetType().GetMethod("RememberAppliedValue")!;
                results = values.Select(value =>
                {
                    valueProperty.SetValue(binding, value);
                    return (bool)rememberMethod.Invoke(binding, null)!;
                }).ToArray();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null)
            throw failure;
        return results!;
    }

    private static string CreateControlTypeName(FieldDefinition definition)
    {
        string? result = null;
        Exception? failure = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var factoryType = typeof(FieldDefinition).Assembly.GetType("Inv_Property_Tab.UI.Fields.FieldEditorFactory")!;
                var factory = Activator.CreateInstance(factoryType);
                var binding = factoryType.GetMethod("Create")!.Invoke(factory, new object[] { definition });
                var control = binding!.GetType().GetProperty("Control")!.GetValue(binding);
                result = control!.GetType().FullName;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null)
            throw failure;
        return result!;
    }
}
