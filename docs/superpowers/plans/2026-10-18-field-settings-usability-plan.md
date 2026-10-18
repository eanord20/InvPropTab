# Field Settings Usability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Исправить запоминание списочных значений, упростить настройку полей до каталога свойств Inventor, вернуть dirty-подсветку и показать имя файла.

**Architecture:** Новый `InventorPropertyCatalog` является единственным источником технических определений стандартных свойств. Окно настройки редактирует только видимость, порядок и разрешённое запоминание. `ConfigurableFieldsPanel` сохраняет новые list values только после успешной записи, а `FieldEditorBinding` инкапсулирует dirty-состояние и цвет.

**Tech Stack:** C#, .NET Framework 4.8, WinForms, Autodesk Inventor 2020 Interop, MSTest 4, DataContractJsonSerializer.

## Global Constraints

- Поддерживается Autodesk Inventor 2020 x64.
- Description никогда не запоминает введённые значения.
- Окно настройки не показывает ID, source, PropertySet ID, propertyName и editor.
- Имя файла отображается с расширением.
- Ошибка одного свойства не блокирует остальные поля.
- COM-объекты освобождаются в `finally`.
- Release-сборка должна проходить без предупреждений.

---

### Task 1: Каталог стандартных свойств Inventor

**Files:**
- Create: `Inv_Property_Tab/Inv_Property_Tab/Configuration/InventorPropertyCatalog.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/Configuration/FieldsConfigurationStore.cs`
- Test: `Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs`

**Interfaces:**
- Produces: `InventorPropertyCatalog.All: IReadOnlyList<FieldDefinition>`
- Produces: `InventorPropertyCatalog.Find(string id): FieldDefinition?`
- Consumes: `FieldDefinition`, `FieldSources`, `FieldEditors`

- [ ] **Step 1: Write failing catalog tests**

```csharp
[TestMethod]
public void CatalogHasUniqueIdsAndSupplierList()
{
    var fields = InventorPropertyCatalog.All;
    Assert.AreEqual(fields.Count, fields.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    var supplier = fields.Single(x => x.Id == "supplier");
    Assert.AreEqual(FieldEditors.List, supplier.Editor);
    Assert.IsTrue(supplier.RememberValues);
}

[TestMethod]
public void CatalogNeverAllowsDescriptionHistory()
{
    var description = InventorPropertyCatalog.All.Single(x => x.Id == "description");
    Assert.AreEqual(FieldEditors.Text, description.Editor);
    Assert.IsFalse(description.RememberValues);
}
```

- [ ] **Step 2: Run tests and verify RED**

Run: `dotnet test "Inv_Property_Tab/Inv_Property_Tab.Tests/Inv_Property_Tab.Tests.csproj" -c Release --no-restore`

Expected: FAIL because `InventorPropertyCatalog` does not exist.

- [ ] **Step 3: Implement immutable catalog**

Create definitions for Part Number, Description and the specification properties approved in the design. Return cloned definitions so settings cannot mutate catalog entries:

```csharp
public static IReadOnlyList<FieldDefinition> All => Definitions.Select(Clone).ToList();

public static FieldDefinition? Find(string id)
{
    var definition = Definitions.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
    return definition == null ? null : Clone(definition);
}
```

Use stable English IDs and explicit iProperty names. System-calculated fields use `source = system` and `editor = readonly`.

- [ ] **Step 4: Build default configuration from catalog**

Change `LoadDefault()` to load catalog definitions or verify embedded defaults against the catalog. Preserve user order, visibility, values and `rememberValues`, but force Description history off.

- [ ] **Step 5: Run tests and Release build**

Run:

```text
dotnet test "Inv_Property_Tab/Inv_Property_Tab.Tests/Inv_Property_Tab.Tests.csproj" -c Release --no-restore
dotnet build "Inv_Property_Tab/Inv_Property_Tab.slnx" -c Release --no-restore
```

Expected: PASS, 0 warnings.

- [ ] **Step 6: Commit**

```text
git add Inv_Property_Tab/Inv_Property_Tab/Configuration Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs
git commit -m "feat: add Inventor property catalog"
```

### Task 2: Запоминание значений списочных полей

**Files:**
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/Fields/FieldEditorBinding.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/Fields/ConfigurableFieldsPanel.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/Configuration/FieldsConfigurationStore.cs`
- Test: `Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs`

**Interfaces:**
- Produces: `FieldEditorBinding.RememberAppliedValue(): bool`
- Produces: `FieldsConfigurationStore.Save(FieldsConfiguration configuration): void`
- Consumes: `PropertyWriteResult.Success`

- [ ] **Step 1: Write failing history tests**

```csharp
[TestMethod]
public void AppliedSupplierValueIsRememberedOnce()
{
    var definition = new FieldDefinition { Id = "supplier", Editor = FieldEditors.List, RememberValues = true };
    var binding = CreateBindingOnSta(definition);
    binding.Value = "  Bosch  ";
    Assert.IsTrue(binding.RememberAppliedValue());
    binding.Value = "bosch";
    Assert.IsFalse(binding.RememberAppliedValue());
    CollectionAssert.AreEqual(new[] { "Bosch" }, definition.Values);
}

[TestMethod]
public void DescriptionValueIsNeverRemembered()
{
    var definition = new FieldDefinition { Id = "description", Editor = FieldEditors.Text, RememberValues = true };
    var binding = CreateBindingOnSta(definition);
    binding.Value = "Корпус";
    Assert.IsFalse(binding.RememberAppliedValue());
    Assert.IsEmpty(definition.Values);
}
```

- [ ] **Step 2: Run tests and verify RED**

Expected: FAIL because `RememberAppliedValue` does not exist.

- [ ] **Step 3: Implement remembered-value rule**

```csharp
public bool RememberAppliedValue()
{
    if (Definition.Id.Equals("description", StringComparison.OrdinalIgnoreCase) ||
        Definition.Editor != FieldEditors.List || !Definition.RememberValues)
        return false;
    var value = Value.Trim();
    if (value.Length == 0 || Definition.Values.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
        return false;
    Definition.Values.Add(value);
    Definition.Values.Sort(StringComparer.CurrentCultureIgnoreCase);
    if (Control is ComboBox comboBox)
    {
        comboBox.Items.Clear();
        comboBox.Items.AddRange(Definition.Values.ToArray());
    }
    return true;
}
```

- [ ] **Step 4: Persist only successful values**

In `ConfigurableFieldsPanel.Apply`, call `RememberAppliedValue()` only after `result.Success`. Track whether any list changed and call `_store.Save(configuration)` once after all writes. Keep failed bindings dirty and do not remember them.

- [ ] **Step 5: Run tests and build**

Expected: Supplier persists across a store round-trip; Description history remains empty.

- [ ] **Step 6: Commit**

```text
git add Inv_Property_Tab/Inv_Property_Tab/UI/Fields Inv_Property_Tab/Inv_Property_Tab/Configuration Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs
git commit -m "fix: persist remembered list values"
```

### Task 3: Dirty-подсветка динамических редакторов

**Files:**
- Create: `Inv_Property_Tab/Inv_Property_Tab/UI/Fields/FieldEditorColors.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/Fields/FieldEditorBinding.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/Fields/FieldEditorFactory.cs`
- Test: `Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs`

**Interfaces:**
- Produces: `FieldEditorBinding.RefreshDirtyState(): void`
- Produces: `FieldEditorBinding.AcceptValue(string value): void`
- Produces: `FieldEditorColors.Changed: Color`

- [ ] **Step 1: Write failing dirty-state tests**

Run controls on an STA thread:

```csharp
[TestMethod]
public void UserChangeHighlightsAndAcceptClearsHighlight()
{
    var binding = CreateBindingOnSta(new FieldDefinition { Id = "project", Editor = FieldEditors.Text });
    binding.AcceptValue("A");
    var original = binding.Control.BackColor;
    binding.Value = "B";
    binding.RefreshDirtyState();
    Assert.AreEqual(FieldEditorColors.Changed, binding.Control.BackColor);
    binding.AcceptValue("B");
    Assert.AreEqual(original, binding.Control.BackColor);
}
```

- [ ] **Step 2: Run test and verify RED**

Expected: FAIL because `RefreshDirtyState` and `FieldEditorColors` do not exist.

- [ ] **Step 3: Implement color ownership in binding**

Capture `_originalBackColor` in the constructor. `RefreshDirtyState()` sets Changed only when `IsChanged`; `AcceptValue()` suppresses event effects, updates original value and restores original color.

- [ ] **Step 4: Wire editor events**

In `FieldEditorFactory.Create` subscribe text controls to `TextChanged`. For ComboBox subscribe both `TextChanged` and `SelectionChangeCommitted`; each invokes `binding.RefreshDirtyState()`. Construct binding before subscriptions or use a local initialized binding.

- [ ] **Step 5: Verify success and failure behavior**

Add a panel test with a fake `IPropertyAccessor`: success invokes `AcceptValue`; failure leaves Changed color. Run all tests.

- [ ] **Step 6: Commit**

```text
git add Inv_Property_Tab/Inv_Property_Tab/UI/Fields Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs
git commit -m "feat: restore dirty field highlighting"
```

### Task 4: Простое окно настройки из каталога

**Files:**
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/Settings/FieldsSettingsForm.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/Fields/ConfigurableFieldsPanel.cs`
- Test: `Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs`

**Interfaces:**
- Consumes: `InventorPropertyCatalog.All`
- Produces: `FieldsSettingsForm.Configuration: FieldsConfiguration`

- [ ] **Step 1: Write failing merge tests outside Form**

Extract testable static method into `FieldsSettingsForm` or a nested-independent `FieldSettingsMerger`:

```csharp
[TestMethod]
public void SettingsMergePreservesKnownVisibilityOrderAndValues()
{
    var current = new FieldsConfiguration { Fields = { new FieldDefinition { Id = "supplier", Visible = false, Order = 10, Values = { "Bosch" }, RememberValues = true } } };
    var merged = FieldSettingsMerger.Merge(InventorPropertyCatalog.All, current);
    var supplier = merged.Single(x => x.Id == "supplier");
    Assert.IsFalse(supplier.Visible);
    CollectionAssert.Contains(supplier.Values, "Bosch");
}
```

- [ ] **Step 2: Run test and verify RED**

Expected: FAIL because merger does not exist.

- [ ] **Step 3: Implement simple row model**

Rows expose only:

```csharp
public bool Visible { get; set; }
public string Label { get; set; }
public bool RememberValues { get; set; }
```

Build rows by merging catalog definitions with current settings. Preserve unknown imported definitions outside the visible row list.

- [ ] **Step 4: Replace grid columns**

Keep only checkbox «Показывать», read-only «Свойство», checkbox «Запоминать». Disable «Запоминать» for non-list fields and Description using `CellBeginEdit`. Retain Up, Down, Import, Export, Defaults, Save and Cancel. Remove Add/Delete and all technical columns.

- [ ] **Step 5: Save merged configuration**

On save, assign normalized order in increments of 10, copy visibility and allowed `rememberValues`, preserve remembered `Values`, append unknown imported definitions unchanged and validate.

- [ ] **Step 6: Run tests and manual UI check**

Verify no technical column is visible and the catalog matches the approved specification list.

- [ ] **Step 7: Commit**

```text
git add Inv_Property_Tab/Inv_Property_Tab/UI/Settings Inv_Property_Tab/Inv_Property_Tab/UI/Fields Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs
git commit -m "feat: simplify field settings"
```

### Task 5: Имя файла под расположением

**Files:**
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/PropUI/PropBrowserControl.cs`
- Create: `Inv_Property_Tab/Inv_Property_Tab/Services/DocumentDisplayService.cs`
- Test: `Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs`

**Interfaces:**
- Produces: `DocumentDisplayService.GetFileName(string fullFileName, string displayName): string`

- [ ] **Step 1: Write failing filename tests**

```csharp
[TestMethod]
public void FileNameIncludesExtensionAndFallsBackToDisplayName()
{
    Assert.AreEqual("6675.01.01.001.ipt", DocumentDisplayService.GetFileName(@"C:\Parts\6675.01.01.001.ipt", "ignored"));
    Assert.AreEqual("Part1", DocumentDisplayService.GetFileName(string.Empty, "Part1"));
}
```

- [ ] **Step 2: Run test and verify RED**

Expected: FAIL because service does not exist.

- [ ] **Step 3: Implement pure filename service**

```csharp
public static string GetFileName(string fullFileName, string displayName)
{
    return string.IsNullOrWhiteSpace(fullFileName) ? displayName ?? string.Empty : Path.GetFileName(fullFileName);
}
```

- [ ] **Step 4: Add read-only UI field**

Add `_tbFileName`, place `AddField("Имя файла", _tbFileName)` immediately after «Расположение», populate it during `ReloadFromDocument`, and clear it in `ClearFields`. Read `FullFileName` and `DisplayName` with COM-safe exception handling.

- [ ] **Step 5: Run tests and build**

Expected: all tests pass, 0 warnings.

- [ ] **Step 6: Commit**

```text
git add Inv_Property_Tab/Inv_Property_Tab/Services/DocumentDisplayService.cs Inv_Property_Tab/Inv_Property_Tab/UI/PropUI/PropBrowserControl.cs Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs
git commit -m "feat: show active document filename"
```

### Task 6: Regression verification and installer rebuild

**Files:**
- Modify: `README.md`
- Verify: `installer/InvPropTab.iss`

- [ ] **Step 1: Run complete automated verification**

```text
dotnet test "Inv_Property_Tab/Inv_Property_Tab.Tests/Inv_Property_Tab.Tests.csproj" -c Release --no-restore
dotnet build "Inv_Property_Tab/Inv_Property_Tab.slnx" -c Release --no-restore
```

Expected: all tests PASS, 0 warnings, 0 errors.

- [ ] **Step 2: Run manual Inventor 2020 scenarios**

Verify part, assembly, read-only document, document switching, Supplier persistence, Description non-persistence, dirty highlight, apply failure, field order, defaults, import/export and filename display.

- [ ] **Step 3: Update user documentation**

Document the simple settings list, visibility, order, Remember option, Description exception, JSON location and restore defaults.

- [ ] **Step 4: Compile installer**

Run:

```text
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "installer\InvPropTab.iss"
```

Expected: successful compile to `installer/output/InvPropTab-1.0.2-x64.exe`.

- [ ] **Step 5: Verify upgrade**

Close Inventor, install over the previous version, validate the generated manifest as XML, start Inventor and repeat Supplier/history smoke tests.

- [ ] **Step 6: Commit documentation**

```text
git add README.md docs
git commit -m "docs: explain configurable property panel"
```
