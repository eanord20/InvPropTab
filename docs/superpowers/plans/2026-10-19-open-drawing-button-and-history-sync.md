# План: кнопка «Открыть чертеж» и синхронизация списков

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Добавить в панель InvPropTab кнопку открытия связанного чертежа и устранить потерю значений исторических списков при работе с несколькими открытыми документами Inventor.

**Architecture:** Логика открытия чертежа уходит в отдельный stateless-сервис `DrawingDocumentService`; хранилище исторических значений (`SupplierStore`) переименовывается в `HistoryStore` и становится application-level singleton с принудительной перезагрузкой из файла перед чтением. `PropBrowserControl` отвечает только за UI: добавляет кнопку, подсказку, двойной клик и делегирует действия сервисам.

**Tech Stack:** C# 12 (latest), .NET Framework 4.8, Windows Forms, Autodesk Inventor 2020 Interop, MSTest.

**Global Constraints**

- Не изменять формат пользовательских json-файлов (`fields.json`, `suppliers.json`, `projects.json`, `designers.json`, `categories.json`).
- Не изменять COM GUID и ProgId.
- COM-объекты освобождать через `ComRelease.Final` в `finally`.
- Пути к файлам не хардкодить; использовать `Environment.SpecialFolder.ApplicationData` и `Path.Combine`.
- Не добавлять новые зависимости без согласования.

---

## File Map

| Файл | Ответственность |
|------|-----------------|
| `Services/DrawingDocumentService.cs` (новый) | Поиск связанного чертежа `.idw`/`.dwg` и открытие через Inventor API. |
| `Services/HistoryStore.cs` (переименованный `SupplierStore.cs`) | Singleton-хранилище исторических значений с перезагрузкой из файла. |
| `UI/PropUI/PropBrowserControl.cs` | Добавить кнопку «Открыть чертеж», двойной клик по имени файла, включить перезагрузку store. |
| `UI/Settings/FieldsSettingsForm.cs` | Перезагружать `fields.json` перед редактированием списков, чтобы не затирать чужие изменения. |
| `Inv_Property_Tab.Tests/Test1.cs` | Тесты для `DrawingDocumentService`, `HistoryStore` синхронизации, наличия кнопки. |
| `CHANGELOG.md`, `README.md` | Описать изменения. |
| `Inv_Property_Tab.csproj` | Обновить версию `1.0.2` → `1.1.0`. |

---

## Task 1: Create `DrawingDocumentService`

**Files:**
- Create: `Services/DrawingDocumentService.cs`
- Test: `Inv_Property_Tab.Tests/Test1.cs`

**Interfaces:**
- Produces: `public static class DrawingDocumentService` with `public static string? FindRelatedDrawing(string modelPath)` and `public static void OpenDrawing(Inventor.Application app, string drawingPath)`.

- [ ] **Step 1: Write the failing test**

```csharp
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
        var model = Path.Combine(_directory, "Part1.ipt");
        var idw = Path.Combine(_directory, "Part1.idw");
        var dwg = Path.Combine(_directory, "Part1.dwg");
        File.WriteAllText(model, string.Empty);
        File.WriteAllText(idw, string.Empty);
        File.WriteAllText(dwg, string.Empty);

        var result = Inv_Property_Tab.Services.DrawingDocumentService.FindRelatedDrawing(model);

        Assert.AreEqual(idw, result);
    }

    [TestMethod]
    public void FindRelatedDrawingReturnsNullWhenMissing()
    {
        var model = Path.Combine(_directory, "Part1.ipt");
        File.WriteAllText(model, string.Empty);

        var result = Inv_Property_Tab.Services.DrawingDocumentService.FindRelatedDrawing(model);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void FindRelatedDrawingReturnsNullForEmptyPath()
    {
        Assert.IsNull(Inv_Property_Tab.Services.DrawingDocumentService.FindRelatedDrawing(string.Empty));
        Assert.IsNull(Inv_Property_Tab.Services.DrawingDocumentService.FindRelatedDrawing(" "));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "Inv_Property_Tab/Inv_Property_Tab.slnx" --filter "FullyQualifiedName~DrawingDocumentServiceTests"`

Expected: FAIL — type/method not found.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using System.IO;
using Inventor;
using Inv_Property_Tab.Diagnostics;

namespace Inv_Property_Tab.Services
{
    public static class DrawingDocumentService
    {
        private static readonly string[] DrawingExtensions = { ".idw", ".dwg" };
        private static readonly AppLogger Logger = new AppLogger();

        public static string? FindRelatedDrawing(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
                return null;

            try
            {
                var fullPath = Path.GetFullPath(modelPath);
                if (!File.Exists(fullPath))
                    return null;

                var directory = Path.GetDirectoryName(fullPath);
                var baseName = Path.GetFileNameWithoutExtension(fullPath);
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(baseName))
                    return null;

                foreach (var ext in DrawingExtensions)
                {
                    var candidate = Path.Combine(directory, baseName + ext);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Find related drawing", ex);
            }

            return null;
        }

        public static void OpenDrawing(Application app, string drawingPath)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (string.IsNullOrWhiteSpace(drawingPath))
                throw new ArgumentException("Drawing path is required.", nameof(drawingPath));
            if (!File.Exists(drawingPath))
                throw new FileNotFoundException("Drawing not found.", drawingPath);

            try
            {
                app.Documents.Open(drawingPath);
            }
            catch (Exception ex)
            {
                Logger.Error("Open drawing: " + drawingPath, ex);
                throw;
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test "Inv_Property_Tab/Inv_Property_Tab.slnx" --filter "FullyQualifiedName~DrawingDocumentServiceTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Inv_Property_Tab/Inv_Property_Tab/Services/DrawingDocumentService.cs Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs
git commit -m "feat: add DrawingDocumentService with related drawing lookup

Adds service to find and open .idw/.dwg drawings matching the current model.
Includes unit tests for extension priority and missing files."
```

---

## Task 2: Convert `SupplierStore` to application-level `HistoryStore`

**Files:**
- Rename/Modify: `Services/SupplierStore.cs` → `Services/HistoryStore.cs`
- Modify: `UI/PropUI/PropBrowserControl.cs`
- Test: `Inv_Property_Tab.Tests/Test1.cs`

**Interfaces:**
- Consumes: existing json format (unchanged).
- Produces: `public sealed class HistoryStore` with `public static HistoryStore ForFile(string fileName)`, `public void Load()`, `public void Save()`, `public void AddOrUpdate(string value)`, `public void Remove(string? value)`, `public IReadOnlyList<string> Values { get; }`, and `public void SetSuppliers(IEnumerable<string> values)` (kept for compatibility during rename).

- [ ] **Step 1: Rename file and class, add shared instances and LastWriteTime cache**

Replace the entire content of `Services/SupplierStore.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Inv_Property_Tab.Diagnostics;

namespace Inv_Property_Tab.Services
{
    public sealed class HistoryStore
    {
        [DataContract]
        private class HistoryData
        {
            [DataMember] public List<string> Suppliers { get; set; } = new List<string>();
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, HistoryStore> Stores = new Dictionary<string, HistoryStore>(StringComparer.OrdinalIgnoreCase);
        private static readonly AppLogger Logger = new AppLogger();

        private readonly List<string> _values = new List<string>();
        private readonly string _fileName;
        private DateTime _lastLoadUtc = DateTime.MinValue;

        public static HistoryStore Suppliers => ForFile("suppliers.json");
        public static HistoryStore Projects => ForFile("projects.json");
        public static HistoryStore Designers => ForFile("designers.json");
        public static HistoryStore Categories => ForFile("categories.json");

        public static HistoryStore ForFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name is required.", nameof(fileName));

            lock (Sync)
            {
                if (!Stores.TryGetValue(fileName, out var store))
                {
                    store = new HistoryStore(fileName);
                    Stores[fileName] = store;
                }
                return store;
            }
        }

        private HistoryStore(string fileName)
        {
            _fileName = fileName;
        }

        public IReadOnlyList<string> Values => _values;

        // Compatibility alias used by existing code/tests.
        public IReadOnlyList<string> Suppliers => Values;

        public void SetSuppliers(IEnumerable<string> values)
        {
            _values.Clear();
            foreach (var s in values)
            {
                var v = (s ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(v))
                    continue;
                if (_values.Any(x => string.Equals(x, v, StringComparison.OrdinalIgnoreCase)))
                    continue;
                _values.Add(v);
            }
            _values.Sort(StringComparer.CurrentCultureIgnoreCase);
        }

        public void AddOrUpdate(string value)
        {
            var v = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(v))
                return;

            Load();
            if (_values.Any(x => string.Equals(x, v, StringComparison.OrdinalIgnoreCase)))
                return;

            _values.Add(v);
            _values.Sort(StringComparer.CurrentCultureIgnoreCase);
            Save();
        }

        public void Remove(string? value)
        {
            var v = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(v))
                return;

            Load();
            _values.RemoveAll(s => string.Equals(s, v, StringComparison.OrdinalIgnoreCase));
            Save();
        }

        public void Load()
        {
            try
            {
                var path = GetStorePath();
                if (!File.Exists(path))
                {
                    if (_values.Count > 0)
                        SetSuppliers(Array.Empty<string>());
                    return;
                }

                var lastWrite = File.GetLastWriteTimeUtc(path);
                if (lastWrite <= _lastLoadUtc && _values.Count >= 0)
                    return;

                using (var fs = File.OpenRead(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(HistoryData));
                    var data = ser.ReadObject(fs) as HistoryData;
                    SetSuppliers(data?.Suppliers ?? new List<string>());
                }
                _lastLoadUtc = lastWrite;
            }
            catch (Exception ex)
            {
                Logger.Error("Load history store " + _fileName, ex);
                SetSuppliers(Array.Empty<string>());
            }
        }

        public void Save()
        {
            try
            {
                var path = GetStorePath();
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                var temporaryPath = path + ".tmp";

                using (var fs = File.Create(temporaryPath))
                {
                    var ser = new DataContractJsonSerializer(typeof(HistoryData));
                    ser.WriteObject(fs, new HistoryData { Suppliers = new List<string>(_values) });
                }

                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);

                _lastLoadUtc = File.GetLastWriteTimeUtc(path);
            }
            catch (Exception ex)
            {
                Logger.Error("Save history store " + _fileName, ex);
            }
        }

        private string GetStorePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InvPropTab");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, _fileName);
        }
    }
}
```

- [ ] **Step 2: Update references in `PropBrowserControl.cs`**

Replace field declarations:

```csharp
private readonly HistoryStore _supplierStore = HistoryStore.Suppliers;
private readonly HistoryStore _projectStore = HistoryStore.Projects;
private readonly HistoryStore _designerStore = HistoryStore.Designers;
private readonly HistoryStore _categoryStore = HistoryStore.Categories;
```

Update method `LoadHistory` to call `store.Load()` before reading values:

```csharp
private static void LoadHistory(HistoryStore store, ComboBox cb)
{
    try
    {
        var current = cb.Text;
        store.Load();
        cb.BeginUpdate();
        cb.Items.Clear();
        foreach (var s in store.Values)
            cb.Items.Add(s);
        cb.EndUpdate();
        if (!string.IsNullOrWhiteSpace(current))
            cb.Text = current;
    }
    catch
    {
    }
}
```

Update `ManageList` and `ManageSuppliers` to call `store.Load()` before opening the form and `store.Save()` after.

Update constructor initialization block to remove the explicit `.Load()` calls on the four stores (they now load on demand).

- [ ] **Step 3: Add synchronization test**

Append to `Inv_Property_Tab.Tests/Test1.cs`:

```csharp
[TestClass]
public sealed class HistoryStoreSynchronizationTests
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
    public void SharedStoreSeesValuesAddedByAnotherInstance()
    {
        var store1 = (HistoryStore)typeof(HistoryStore).GetMethod("ForFile", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { "test-sync.json" })!;
        var store2 = (HistoryStore)typeof(HistoryStore).GetMethod("ForFile", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { "test-sync.json" })!;

        store1.AddOrUpdate("Bosch");

        Assert.IsTrue(store2.Values.Contains("Bosch", StringComparer.OrdinalIgnoreCase));
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test "Inv_Property_Tab/Inv_Property_Tab.slnx"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Inv_Property_Tab/Inv_Property_Tab/Services/SupplierStore.cs Inv_Property_Tab/Inv_Property_Tab/Services/HistoryStore.cs Inv_Property_Tab/Inv_Property_Tab/UI/PropUI/PropBrowserControl.cs Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs
git commit -m "refactor: convert SupplierStore to application-level HistoryStore

Makes history stores singleton per file and reloads from disk before every
read so changes made in one document are visible in others."
```

---

## Task 3: Add «Открыть чертеж» button and double-click behavior

**Files:**
- Modify: `UI/PropUI/PropBrowserControl.cs`
- Test: `Inv_Property_Tab.Tests/Test1.cs`

**Interfaces:**
- Consumes: `DrawingDocumentService.FindRelatedDrawing`, `DrawingDocumentService.OpenDrawing`.
- Produces: private field `_btnOpenDrawing`, private method `OpenRelatedDrawing()`.

- [ ] **Step 1: Add reflection test for the new button**

Append to `Inv_Property_Tab.Tests/Test1.cs`:

```csharp
[TestClass]
public sealed class OpenDrawingButtonTests
{
    [TestMethod]
    public void PropBrowserControlHasOpenDrawingButton()
    {
        var type = typeof(Inv_Property_Tab.UI.PropUI.PropBrowserControl);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var field = type.GetField("_btnOpenDrawing", flags);
        Assert.IsNotNull(field);
        Assert.AreEqual(typeof(System.Windows.Forms.Button), field.FieldType);
    }
}
```

- [ ] **Step 2: Add button and tooltip fields**

In `PropBrowserControl.cs` field declarations, after `_btnOpenLocation`:

```csharp
private readonly Button _btnOpenDrawing = new Button();
private readonly ToolTip _toolTip = new ToolTip();
```

- [ ] **Step 3: Build the file name panel in `BuildSinglePageLayout()`**

Replace:

```csharp
_btnOpenLocation.Text = "📂";
_btnOpenLocation.Width = 32;
```

with:

```csharp
_btnOpenLocation.Text = "📂";
_btnOpenLocation.Width = 32;

_btnOpenDrawing.Text = "📄";
_btnOpenDrawing.Width = 32;
```

Then replace:

```csharp
AddUnlabeledControl(_tbFileName);
```

with:

```csharp
var fileNamePanel = new Panel { Dock = DockStyle.Top };
_tbFileName.Dock = DockStyle.Fill;
fileNamePanel.Controls.Add(_tbFileName);
_btnOpenDrawing.Dock = DockStyle.Right;
fileNamePanel.Controls.Add(_btnOpenDrawing);
fileNamePanel.Height = _tbFileName.PreferredHeight;
_btnOpenDrawing.Height = _tbFileName.PreferredHeight;
AddUnlabeledControl(fileNamePanel);
```

- [ ] **Step 4: Wire events and set tooltip**

In `WireEvents()`, add:

```csharp
_btnOpenDrawing.Click += (_, __) => OpenRelatedDrawing();
_tbFileName.DoubleClick += (_, __) => OpenRelatedDrawing();
```

In constructor, after `WireEvents();` or in `BuildSinglePageLayout`, add:

```csharp
_toolTip.SetToolTip(_btnOpenLocation, "Открыть расположение");
_toolTip.SetToolTip(_btnOpenDrawing, "Открыть чертёж");
```

- [ ] **Step 5: Add `OpenRelatedDrawing()` method**

```csharp
private void OpenRelatedDrawing()
{
    try
    {
        var doc = _ctx?.Document;
        var modelPath = doc?.FullFileName;
        if (string.IsNullOrWhiteSpace(modelPath) || !System.IO.File.Exists(modelPath))
            return;

        var drawingPath = DrawingDocumentService.FindRelatedDrawing(modelPath);
        if (string.IsNullOrWhiteSpace(drawingPath))
        {
            System.Windows.Forms.MessageBox.Show(
                "Связанный чертёж не найден.",
                "InvPropTab",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        DrawingDocumentService.OpenDrawing(_ctx!.App, drawingPath);
    }
    catch (Exception ex)
    {
        Logger.Error("Open related drawing", ex);
        System.Windows.Forms.MessageBox.Show(
            "Не удалось открыть чертёж:\n" + ex.Message,
            "InvPropTab",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }
}
```

Add field to `PropBrowserControl`:

```csharp
private static readonly AppLogger Logger = new AppLogger();
```

- [ ] **Step 6: Enable/disable button based on document state and reduce layout lag**

In `ReloadFromDocument()`, after setting `_tbFileName.Text`, add:

```csharp
var hasModelPath = !string.IsNullOrWhiteSpace(fullFileName) && System.IO.File.Exists(fullFileName);
_btnOpenDrawing.Enabled = hasModelPath;
```

Also add `SuspendLayout` / `ResumeLayout` around the body of `ReloadFromDocument` to prevent WinForms from redrawing on every field update:

```csharp
public void ReloadFromDocument()
{
    _suppressEvents = true;
    _layout.SuspendLayout();
    try
    {
        // existing body
    }
    catch
    {
    }
    finally
    {
        _layout.ResumeLayout(true);
        _suppressEvents = false;
    }
}
```

- [ ] **Step 7: Run tests**

Run: `dotnet test "Inv_Property_Tab/Inv_Property_Tab.slnx"`

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add Inv_Property_Tab/Inv_Property_Tab/UI/PropUI/PropBrowserControl.cs Inv_Property_Tab/Inv_Property_Tab.Tests/Test1.cs
git commit -m "feat: add open related drawing button next to file name

Adds a small button and double-click handler on the file name field that
finds and opens the matching .idw/.dwg drawing in Inventor."
```

---

## Task 4: Reload field configuration before editing lists

**Files:**
- Modify: `UI/Settings/FieldsSettingsForm.cs`
- Modify: `Configuration/FieldsConfigurationStore.cs` (if needed to expose reload)

**Interfaces:**
- Consumes: `FieldsConfigurationStore.Load()`.
- Produces: no new public members.

- [ ] **Step 1: Locate list value editing flow**

Read `UI/Settings/FieldsSettingsForm.cs` and identify where list values are edited and saved. If the form holds its own copy of configuration, ensure it reloads the latest version from disk when shown.

- [ ] **Step 2: Add reload before show**

In `ConfigurableFieldsPanel.EditConfiguration`, before creating `FieldsSettingsForm`, call `_store.Load()`:

```csharp
public bool EditConfiguration(IWin32Window owner)
{
    try
    {
        var current = _store.Load();
        using (var form = new FieldsSettingsForm(current, _store.LoadDefault))
        {
            if (form.ShowDialog(owner) != DialogResult.OK)
                return false;
            _store.Save(form.Configuration);
        }
        Rebuild();
        return true;
    }
    catch (Exception ex)
    {
        MessageBox.Show(ex.Message, "InvPropTab", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }
}
```

- [ ] **Step 3: Verify save is atomic**

Confirm `FieldsConfigurationStore.Save` already writes to `*.tmp` and uses `File.Replace`. If not, update it to match `HistoryStore.Save` pattern.

- [ ] **Step 4: Run tests**

Run: `dotnet test "Inv_Property_Tab/Inv_Property_Tab.slnx"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Inv_Property_Tab/Inv_Property_Tab/UI/Fields/ConfigurableFieldsPanel.cs
git commit -m "fix: reload field configuration before editing to avoid overwriting

Ensures the latest list values are loaded from disk before the settings
form opens, preventing loss of values added in another document."
```

---

## Task 5: Update documentation and version

**Files:**
- Modify: `CHANGELOG.md`
- Modify: `README.md`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/Inv_Property_Tab.csproj`

- [ ] **Step 1: Update CHANGELOG.md**

Add a new section at the top:

```markdown
## 1.1.0

- Добавлена кнопка «Открыть чертёж» рядом с именем файла; двойной клик по имени файла также открывает связанный .idw/.dwg.
- Исправлена синхронизация исторических списков при работе с несколькими открытыми документами.
```

- [ ] **Step 2: Update README.md**

Add a short paragraph under the feature list or in the «Возможности» section:

```markdown
- быстрое открытие связанного чертежа (.idw/.dwg) из панели;
```

- [ ] **Step 3: Bump version in csproj**

Update:

```xml
<Version>1.1.0</Version>
<AssemblyVersion>1.1.0.0</AssemblyVersion>
<FileVersion>1.1.0.0</FileVersion>
```

- [ ] **Step 4: Run build**

Run: `dotnet build "Inv_Property_Tab/Inv_Property_Tab.slnx" -c Release`

Expected: Build succeeds with no errors.

- [ ] **Step 5: Commit**

```bash
git add CHANGELOG.md README.md Inv_Property_Tab/Inv_Property_Tab/Inv_Property_Tab.csproj
git commit -m "docs: document 1.1.0 changes and bump version"
```

---

## Task 6: Final verification

- [ ] **Step 1: Run all tests**

Run: `dotnet test "Inv_Property_Tab/Inv_Property_Tab.slnx"`

Expected: All tests pass.

- [ ] **Step 2: Run release build**

Run: `dotnet build "Inv_Property_Tab/Inv_Property_Tab.slnx" -c Release`

Expected: Build succeeds.

- [ ] **Step 3: Commit any remaining changes**

```bash
git status
git add -A
git commit -m "chore: final verification for open drawing and history sync"
```

---

## Self-Review Checklist

- [ ] Spec coverage: каждое требование FR-1 и FR-2 покрыто задачей.
- [ ] Placeholder scan: в плане нет «TBD», «TODO», «fill later».
- [ ] Type consistency: `HistoryStore.Values` используется вместо `Suppliers` в новом коде; старые тесты, использующие `Suppliers`, продолжают работать через alias.
- [ ] COM safety: `DrawingDocumentService.OpenDrawing` перехватывает исключения Inventor API.
- [ ] Backward compatibility: формат json не меняется, пути к файлам не меняются.
