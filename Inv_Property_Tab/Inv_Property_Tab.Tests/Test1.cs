using System;
using System.IO;
using System.Linq;
using Inv_Property_Tab.Configuration;
using Inv_Property_Tab.Services;

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
    public void CompactLayoutHasPhysicalAndActionRows()
    {
        var type = typeof(Inv_Property_Tab.UI.PropUI.PropBrowserControl);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        Assert.IsNotNull(type.GetField("_physicalRow", flags));
        Assert.IsNotNull(type.GetField("_actionsRow", flags));
    }

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
    public void RemovedPropertiesDoNotReturnFromLegacyConfiguration()
    {
        var removed = new[] { "checkingDate", "creationDate", "webLink", "subject", "itemQuantity", "baseQuantity", "componentType", "representation", "structure", "stage", "counter", "volume", "fileName", "filePath" };
        Assert.IsFalse(InventorPropertyCatalog.All.Any(field => removed.Contains(field.Id)));
        var legacy = new FieldsConfiguration
        {
            Fields = removed.Select(id => new FieldDefinition { Id = id, Label = id, Editor = FieldEditors.Text, PropertyName = id }).ToList()
        };

        var merged = Inv_Property_Tab.UI.Settings.FieldSettingsMerger.Merge(InventorPropertyCatalog.All, legacy);

        Assert.IsFalse(merged.Fields.Any(field => removed.Contains(field.Id)));
    }

    [TestMethod]
    public void CatalogContainsRevisionAndInventoryNumberLabel()
    {
        var revision = InventorPropertyCatalog.All.Single(field => field.Id == "revisionNumber");
        Assert.AreEqual("Редакция", revision.Label);
        Assert.AreEqual("Revision Number", revision.PropertyName);
        Assert.AreEqual("Инвентарный номер", InventorPropertyCatalog.All.Single(field => field.Id == "stockNumber").Label);
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
public sealed class FieldSettingsMergerTests
{
    [TestMethod]
    public void DescriptionCanEnableRememberMode()
    {
        var field = InventorPropertyCatalog.All.Single(item => item.Id == "description");

        Inv_Property_Tab.UI.Settings.FieldSettingsMerger.ApplyRememberMode(field, true);

        Assert.AreEqual(FieldEditors.List, field.Editor);
        Assert.IsTrue(field.RememberValues);
    }

    [TestMethod]
    public void HiddenTextFieldCanToggleRememberModeWithoutLosingValues()
    {
        var field = InventorPropertyCatalog.All.Single(item => item.Id == "stockNumber");
        field.Visible = false;
        field.Values.Add("A-01");
        var method = typeof(Inv_Property_Tab.UI.Settings.FieldSettingsMerger).GetMethod("ApplyRememberMode");
        Assert.IsNotNull(method);

        method.Invoke(null, new object[] { field, true });
        Assert.AreEqual(FieldEditors.List, field.Editor);
        Assert.IsTrue(field.RememberValues);
        method.Invoke(null, new object[] { field, false });

        Assert.AreEqual(FieldEditors.Text, field.Editor);
        Assert.IsFalse(field.RememberValues);
        CollectionAssert.AreEqual(new[] { "A-01" }, field.Values);
    }

    [TestMethod]
    public void MergePreservesSupplierSettingsAndUnknownFields()
    {
        var current = new FieldsConfiguration
        {
            Fields =
            {
                new FieldDefinition { Id = "supplier", Label = "Старый поставщик", Visible = false, Order = 10, Editor = FieldEditors.List, Values = { "Bosch" }, RememberValues = true },
                new FieldDefinition { Id = "legacy", Label = "Legacy", Visible = true, Order = 20, Editor = FieldEditors.Text, PropertyName = "Legacy" }
            }
        };
        var type = typeof(FieldDefinition).Assembly.GetType("Inv_Property_Tab.UI.Settings.FieldSettingsMerger");
        Assert.IsNotNull(type);
        var result = (FieldsConfiguration)type.GetMethod("Merge")!.Invoke(null, new object[] { InventorPropertyCatalog.All, current })!;

        var supplier = result.Fields.Single(field => field.Id == "supplier");
        Assert.IsFalse(supplier.Visible);
        Assert.IsTrue(supplier.RememberValues);
        CollectionAssert.AreEqual(new[] { "Bosch" }, supplier.Values);
        Assert.IsTrue(result.Fields.Any(field => field.Id == "legacy"));
    }
}

[TestClass]
public sealed class ListValuesModelTests
{
    [TestMethod]
    public void ValuesAreTrimmedDeduplicatedAndSorted()
    {
        var type = typeof(FieldDefinition).Assembly.GetType("Inv_Property_Tab.UI.Settings.ListValuesModel");
        Assert.IsNotNull(type);
        var result = (System.Collections.Generic.List<string>)type.GetMethod("Normalize")!.Invoke(null, new object[] { new[] { " Beta ", "Alpha", "alpha", "" } })!;

        CollectionAssert.AreEqual(new[] { "Alpha", "Beta" }, result);
    }
}

[TestClass]
public sealed class DocumentDisplayServiceTests
{
    [TestMethod]
    public void FileNameIncludesExtensionAndFallsBackToDisplayName()
    {
        var type = typeof(FieldDefinition).Assembly.GetType("Inv_Property_Tab.Services.DocumentDisplayService");
        Assert.IsNotNull(type);
        var method = type.GetMethod("GetFileName")!;

        Assert.AreEqual("6675.01.01.001.ipt", method.Invoke(null, new object[] { @"C:\\Parts\\6675.01.01.001.ipt", "ignored" }));
        Assert.AreEqual("Part1", method.Invoke(null, new object[] { string.Empty, "Part1" }));
    }
}

[TestClass]
public sealed class DrawingDocumentServiceTests
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
    public void FindRelatedDrawingPrefersIdwOverDwg()
    {
        var modelPath = Path.Combine(_directory, "Part1.ipt");
        var idwPath = Path.Combine(_directory, "Part1.idw");
        var dwgPath = Path.Combine(_directory, "Part1.dwg");
        File.WriteAllText(modelPath, string.Empty);
        File.WriteAllText(idwPath, string.Empty);
        File.WriteAllText(dwgPath, string.Empty);

        var result = DrawingDocumentService.FindRelatedDrawing(modelPath);

        Assert.AreEqual(idwPath, result);
    }

    [TestMethod]
    public void FindRelatedDrawingReturnsNullWhenDrawingMissing()
    {
        var modelPath = Path.Combine(_directory, "Part1.ipt");
        File.WriteAllText(modelPath, string.Empty);

        var result = DrawingDocumentService.FindRelatedDrawing(modelPath);

        Assert.IsNull(result);
    }
}

[TestClass]
public sealed class SupplierStoreSyncTests
{
    [TestMethod]
    public void AddOrUpdateFromOneStoreIsVisibleInAnotherStore()
    {
        var fileName = "sync-" + Guid.NewGuid().ToString("N") + ".json";
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InvPropTab", fileName);
        try
        {
            var first = SupplierStore.ForFile(fileName);
            var second = SupplierStore.ForFile(fileName);
            first.AddOrUpdate("Bosch");

            second.Load();

            Assert.IsTrue(second.Suppliers.Any(v => string.Equals(v, "Bosch", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}

[TestClass]
public sealed class OpenDrawingButtonTests
{
    [TestMethod]
    public void PropBrowserControlHasOpenDrawingButtonField()
    {
        var type = typeof(Inv_Property_Tab.UI.PropUI.PropBrowserControl);
        var field = type.GetField("_btnOpenDrawing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        Assert.AreEqual("System.Windows.Forms.Button", field.FieldType.FullName);
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
    public void DescriptionValueIsRememberedWhenEnabled()
    {
        var definition = new FieldDefinition { Id = "description", Label = "Наименование", Editor = FieldEditors.List, RememberValues = true };
        var results = InvokeRememberOnSta(definition, "Корпус", "корпус");

        CollectionAssert.AreEqual(new[] { true, false }, results);
        CollectionAssert.AreEqual(new[] { "Корпус" }, definition.Values);
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
