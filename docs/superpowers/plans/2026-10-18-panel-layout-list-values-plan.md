# Panel Layout and List Values Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Уплотнить панель, добавить общий редактор сохранённых значений и убрать восемь ненужных свойств.

**Architecture:** Компоновка меняется локально в существующих WinForms-контролах. Один `ListValuesEditorForm` редактирует копию списка и вызывается из `ConfigurableFieldsPanel` и `FieldsSettingsForm`. Каталог остаётся единственным источником доступных свойств.

**Tech Stack:** C#, .NET Framework 4.8, WinForms, MSTest 4, Autodesk Inventor 2020.

## Global Constraints

- Габариты слева, масса справа; логика значений не меняется.
- Путь, имя файла и динамические свойства показываются без внешних заголовков.
- «Настроить» слева, «Применить» справа в одной строке.
- Description не получает историю.
- Изменения списков сохраняются в `%APPDATA%\InvPropTab\fields.json`.
- Release-сборка проходит без предупреждений.

---

### Task 1: Сократить каталог и мигрировать старую конфигурацию

**Files:**
- Modify: `Inv_Property_Tab/Inv_Property_Tab/Configuration/InventorPropertyCatalog.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/Settings/FieldSettingsMerger.cs`
- Test: `Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs`

**Interfaces:**
- Produces: `FieldSettingsMerger.RemovedCatalogIds: IReadOnlyCollection<string>`
- Consumes: `InventorPropertyCatalog.All`

- [ ] Написать failing test:

```csharp
[TestMethod]
public void RemovedPropertiesDoNotReturnFromLegacyConfiguration()
{
    var removed = new[] { "checkingDate", "creationDate", "webLink", "subject", "itemQuantity", "baseQuantity", "componentType", "representation" };
    Assert.IsFalse(InventorPropertyCatalog.All.Any(x => removed.Contains(x.Id)));
    var legacy = new FieldsConfiguration { Fields = removed.Select(id => new FieldDefinition { Id = id, Label = id, Editor = FieldEditors.Text, PropertyName = id }).ToList() };
    var merged = FieldSettingsMerger.Merge(InventorPropertyCatalog.All, legacy);
    Assert.IsFalse(merged.Fields.Any(x => removed.Contains(x.Id)));
}
```

- [ ] Запустить `dotnet test "Inv_Property_Tab/Inv_Property_Tab.Tests/Inv_Property_Tab.Tests.csproj" -c Release --no-restore`; ожидается FAIL.
- [ ] Удалить восемь определений из каталога.
- [ ] Добавить точный `HashSet<string>` удалённых ID и исключать их из ветки сохранения неизвестных полей.
- [ ] Запустить тесты; ожидается PASS.
- [ ] Закоммитить: `git commit -m "refactor: trim Inventor property catalog"`.

### Task 2: Общий редактор сохранённых значений

**Files:**
- Create: `Inv_Property_Tab/Inv_Property_Tab/UI/Settings/ListValuesModel.cs`
- Create: `Inv_Property_Tab/Inv_Property_Tab/UI/Settings/ListValuesEditorForm.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/Fields/FieldEditorBinding.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/Fields/FieldEditorFactory.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/Fields/ConfigurableFieldsPanel.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/Settings/FieldsSettingsForm.cs`
- Test: `Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs`

**Interfaces:**
- `ListValuesModel.Normalize(IEnumerable<string> values): List<string>`
- `FieldEditorBinding.ReplaceValues(IEnumerable<string> values): void`
- `ConfigurableFieldsPanel.EditValues(FieldDefinition definition, IWin32Window owner): bool`

- [ ] Написать failing tests нормализации:

```csharp
[TestMethod]
public void ListValuesAreTrimmedDeduplicatedAndSorted()
{
    CollectionAssert.AreEqual(new[] { "Alpha", "Beta" }, ListValuesModel.Normalize(new[] { " Beta ", "Alpha", "alpha", "" }));
}
```

- [ ] Запустить тест и подтвердить FAIL из-за отсутствующего типа.
- [ ] Реализовать `Normalize`: trim, remove empty, distinct `OrdinalIgnoreCase`, sort `CurrentCultureIgnoreCase`.
- [ ] Реализовать `ListValuesEditorForm`, редактирующий отдельную `BindingList<string>`; исходный список меняется только после `DialogResult.OK`.
- [ ] Добавить `ReplaceValues`, который заменяет `Definition.Values` и элементы ComboBox, не меняя текущее введённое значение.
- [ ] В `FieldEditorFactory` для `list` создать горизонтальный host с ComboBox и кнопкой `…`; передать вызов редактирования через событие binding `EditValuesRequested`.
- [ ] В `ConfigurableFieldsPanel` открыть диалог, после OK вызвать `ReplaceValues` и `_store.Save(_configuration)`.
- [ ] В `FieldsSettingsForm` разрешить «Запоминать» для любого несистемного и не-read-only свойства, кроме Description; видимость не влияет на доступность.
- [ ] При включении запоминания менять `Editor` на `list`, при выключении возвращать заводской editor из `InventorPropertyCatalog`, не очищая `Values`.
- [ ] Добавить кнопку «Значения…», включённую только при активном `RememberValues`; после OK обновить `Definition.Values` строки.
- [ ] Запустить тесты и вручную проверить скрытое поле, переключение text/list, отмену, добавление, изменение, удаление и очистку.
- [ ] Закоммитить: `git commit -m "feat: edit remembered list values"`.

### Task 3: Компактная компоновка панели

**Files:**
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/PropUI/PropBrowserControl.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/Fields/ConfigurableFieldsPanel.cs`
- Test: `Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs`

**Interfaces:**
- Produces internal layout helpers: `AddUnlabeledControl(Control)`, `BuildPhysicalRow()`, `BuildActionsRow()`.

- [ ] Добавить reflection-тесты структуры: в `PropBrowserControl` существуют `_tbLocation`, `_tbFileName`, `_tbDims`, `_tbMass`; кнопки находятся в одном host; `_configurableFields` добавляется без `AddField("Свойства", ...)`.
- [ ] Запустить тесты и подтвердить FAIL на новой структуре host-полей.
- [ ] Добавить путь и имя файла через `AddUnlabeledControl`, сохранив кнопку открытия папки.
- [ ] Добавить `_configurableFields` напрямую в `_layout` без Label.
- [ ] Добавить `_gridCustom` напрямую без заголовка «Прочее» и убрать лишний разделитель, чтобы между блоками оставался один стандартный отступ.
- [ ] Построить физическую строку двухколоночным `TableLayoutPanel`: колонка 0 — label «Габариты» и `_tbDims`, колонка 1 — label «Масса» и `_tbMass`; ширина 50/50.
- [ ] Исключить `mass` и `dimensions` из динамического рендера `ConfigurableFieldsPanel`, чтобы значения не дублировались.
- [ ] Построить строку кнопок двухколоночным `TableLayoutPanel`: `_btnSettings` слева, `_btnApply` справа, одинаковая высота.
- [ ] Запустить тесты и Release build; ожидается PASS и 0 warnings.
- [ ] Закоммитить: `git commit -m "feat: compact property panel layout"`.

### Task 4: Регрессия и установщик

**Files:**
- Modify: `README.md`
- Verify: `installer/InvPropTab.iss`

- [ ] Запустить:

```text
dotnet test "Inv_Property_Tab/Inv_Property_Tab.Tests/Inv_Property_Tab.Tests.csproj" -c Release --no-restore
dotnet build "Inv_Property_Tab/Inv_Property_Tab.slnx" -c Release --no-restore
```

- [ ] Проверить в Inventor 2020: layout, отсутствие подписей, габариты/массу, кнопки, оба входа редактора списков, Supplier persistence и Description non-persistence.
- [ ] Обновить README с описанием кнопки `…` и «Значения…».
- [ ] Собрать установщик:

```text
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "installer\InvPropTab.iss"
```

- [ ] Переустановить поверх версии 1.0.2, проверить валидность manifest и запуск add-in.
- [ ] Закоммитить документацию: `git commit -m "docs: explain remembered value editing"`.
