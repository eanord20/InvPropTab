using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Inventor;
using Inv_Property_Tab.Services;
using Inv_Property_Tab.UI.Fields;

using WinTextBox = System.Windows.Forms.TextBox;

namespace Inv_Property_Tab.UI.PropUI
{
    public class PropBrowserControl : UserControl
    {
        private PropBrowserContext? _ctx;

        private readonly SupplierStore _supplierStore = new SupplierStore("suppliers.json");
        private readonly SupplierStore _projectStore = new SupplierStore("projects.json");
        private readonly SupplierStore _designerStore = new SupplierStore("designers.json");
        private readonly SupplierStore _categoryStore = new SupplierStore("categories.json");
        private readonly IPropertiesService _propertiesService = new IPropertiesService();
        private readonly ConfigurableFieldsPanel _configurableFields = new ConfigurableFieldsPanel();

        private readonly Label _lblEmpty = new Label();

        private readonly Panel _scrollHost = new Panel();
        private readonly TableLayoutPanel _layout = new TableLayoutPanel();

        private readonly WinTextBox _tbLocation = new WinTextBox();
        private readonly WinTextBox _tbFileName = new WinTextBox();
        private readonly Button _btnOpenLocation = new Button();
        private readonly WinTextBox _tbSize = new WinTextBox();
        private readonly WinTextBox _tbCreated = new WinTextBox();
        private readonly WinTextBox _tbSaved = new WinTextBox();

        private readonly WinTextBox _tbPartNumber = new WinTextBox();

        private readonly WinTextBox _tbDescription = new WinTextBox();
        private readonly ComboBox _cbDesigner = new ComboBox();
        private readonly Button _btnDesigners = new Button();
        private readonly ComboBox _cbProject = new ComboBox();
        private readonly Button _btnProjects = new Button();

        private readonly ComboBox _cbCategory = new ComboBox();
        private readonly Button _btnCategories = new Button();

        private readonly ComboBox _cbSupplier = new ComboBox();
        private readonly Button _btnSuppliers = new Button();

        private readonly WinTextBox _tbMass = new WinTextBox();

        private readonly DataGridView _gridCustom = new DataGridView();
        private readonly WinTextBox _tbDims = new WinTextBox();

        private readonly TableLayoutPanel _physicalRow = new TableLayoutPanel();
        private readonly TableLayoutPanel _actionsRow = new TableLayoutPanel();
        private readonly Button _btnSettings = new Button();
        private readonly Button _btnApply = new Button();

        private readonly Dictionary<Control, string> _originalValues = new Dictionary<Control, string>();
        private readonly Dictionary<Control, System.Drawing.Color> _originalBackColors = new Dictionary<Control, System.Drawing.Color>();

        private bool _suppressEvents;

        public PropBrowserControl()
        {
            Dock = DockStyle.Fill;

            _lblEmpty.Dock = DockStyle.Fill;
            _lblEmpty.TextAlign = ContentAlignment.MiddleCenter;
            _lblEmpty.Text = "No active document";

            _scrollHost.Dock = DockStyle.Fill;
            _scrollHost.AutoScroll = true;
            Controls.Add(_scrollHost);

            _layout.Dock = DockStyle.Top;
            _layout.AutoSize = true;
            _layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _layout.ColumnCount = 1;
            _layout.RowCount = 0;
            _layout.Padding = new Padding(8);
            _layout.Margin = new Padding(0);
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _scrollHost.Controls.Add(_layout);

            BuildSinglePageLayout();

            _cbSupplier.DropDownStyle = ComboBoxStyle.DropDown;
            _cbSupplier.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cbSupplier.AutoCompleteSource = AutoCompleteSource.ListItems;

            _cbDesigner.DropDownStyle = ComboBoxStyle.DropDown;
            _cbDesigner.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cbDesigner.AutoCompleteSource = AutoCompleteSource.ListItems;

            _cbProject.DropDownStyle = ComboBoxStyle.DropDown;
            _cbProject.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cbProject.AutoCompleteSource = AutoCompleteSource.ListItems;

            _cbCategory.DropDownStyle = ComboBoxStyle.DropDown;
            _cbCategory.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cbCategory.AutoCompleteSource = AutoCompleteSource.ListItems;

            _tbLocation.ReadOnly = true;
            _tbFileName.ReadOnly = true;
            _tbSize.ReadOnly = true;
            _tbCreated.ReadOnly = true;
            _tbSaved.ReadOnly = true;

            _tbMass.ReadOnly = true;

            _tbDims.ReadOnly = true;
            _tbDims.Dock = DockStyle.Top;

            WireEvents();

            _supplierStore.Load();
            _projectStore.Load();
            _designerStore.Load();
            _categoryStore.Load();
            UpdateEmptyState();
        }

        public void SetContext(PropBrowserContext context)
        {
            _ctx = context;
            ReloadFromDocument();
        }

        public void ReloadFromDocument()
        {
            try
            {
                _suppressEvents = true;
                ClearDirtyState();

                _supplierStore.Load();
                _projectStore.Load();
                _designerStore.Load();
                    _categoryStore.Load();

                var doc = _ctx?.Document;
                if (doc == null)
                {
                    SetEnabledState(false);
                    ClearFields();
                    UpdateEmptyState();
                    return;
                }

                SetEnabledState(true);

                // General
                var fullFileName = TryGetFullFileName(doc);
                _tbLocation.Text = string.IsNullOrWhiteSpace(fullFileName) ? string.Empty : System.IO.Path.GetDirectoryName(fullFileName) ?? string.Empty;
                _tbFileName.Text = DocumentDisplayService.GetFileName(fullFileName, TryGetDisplayName(doc));

                // Project iProperties
                _tbPartNumber.Text = _propertiesService.TryGetPropertyValue(doc, "Part Number");

                LoadHistory(_designerStore, _cbDesigner);
                LoadHistory(_projectStore, _cbProject);
                LoadHistory(_categoryStore, _cbCategory);

                _tbDescription.Text = _propertiesService.TryGetPropertyValue(doc, "Description");
                _cbDesigner.Text = _propertiesService.TryGetPropertyValue(doc, "Designer");
                _cbProject.Text = _propertiesService.TryGetPropertyValue(doc, "Project");
                _cbCategory.Text = _propertiesService.TryGetPropertyValue(doc, "Category");

                LoadSupplierList();
                _cbSupplier.Text = _propertiesService.TryGetSupplierExpression(doc);

                // Physical
                var mass = _propertiesService.TryGetMassKg(doc);
                _tbMass.Text = string.IsNullOrWhiteSpace(mass) ? string.Empty : (mass + " kg");

                // Custom
                LoadOtherGrid(doc);

                // Dimensions
                var canShowDims = (doc is PartDocument) || (doc is AssemblyDocument);
                if (canShowDims)
                {
                    var (x, y, z) = _propertiesService.TryGetBoundingBoxMm(doc);
                    _tbDims.Text = FormatDims(x, y, z);
                }
                else
                {
                    _tbDims.Text = string.Empty;
                }

                _configurableFields.LoadDocument(doc);
                ApplyReadOnlyState(_propertiesService.IsReadOnly(doc));
                UpdateEmptyState();

                SnapshotOriginalValues();
            }
            catch
            {
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private void WireEvents()
        {
            _tbPartNumber.TextChanged += (_, __) => MarkDirty(_tbPartNumber);

            _tbDescription.TextChanged += (_, __) => MarkDirty(_tbDescription);

            _cbDesigner.TextChanged += (_, __) => MarkDirty(_cbDesigner);
            _cbDesigner.SelectionChangeCommitted += (_, __) => MarkDirty(_cbDesigner);

            _cbProject.TextChanged += (_, __) => MarkDirty(_cbProject);
            _cbProject.SelectionChangeCommitted += (_, __) => MarkDirty(_cbProject);

            _cbCategory.TextChanged += (_, __) => MarkDirty(_cbCategory);
            _cbCategory.SelectionChangeCommitted += (_, __) => MarkDirty(_cbCategory);

            _cbSupplier.TextChanged += (_, __) => MarkDirty(_cbSupplier);
            _cbSupplier.SelectionChangeCommitted += (_, __) => MarkDirty(_cbSupplier);

            _btnSuppliers.Click += (_, __) => ManageSuppliers();
            _btnDesigners.Click += (_, __) => ManageList("Разработчики", _designerStore);
            _btnProjects.Click += (_, __) => ManageList("Проекты", _projectStore);
            _btnCategories.Click += (_, __) => ManageList("Категории", _categoryStore);
            _btnSettings.Click += (_, __) => EditFieldSettings();
            _btnApply.Click += (_, __) => ApplyChanges();
            _btnOpenLocation.Click += (_, __) => OpenLocationInExplorer();
        }

        private void OpenLocationInExplorer()
        {
            try
            {
                var doc = _ctx?.Document;
                var p = doc?.FullFileName;
                if (string.IsNullOrWhiteSpace(p))
                    return;
                if (!System.IO.File.Exists(p))
                    return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + p + "\"",
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        private void EditFieldSettings()
        {
            try
            {
                if (_configurableFields.EditConfiguration(this))
                    ReloadFromDocument();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "InvPropTab", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ApplyChanges()
        {
            try
            {
                if (_suppressEvents)
                    return;
                var doc = _ctx?.Document;
                if (doc == null)
                    return;

                if (_propertiesService.IsReadOnly(doc))
                    return;

                var changedAny = false;

                changedAny |= TryApplyTextBox(doc, _tbPartNumber, "Part Number");
                changedAny |= TryApplyTextBox(doc, _tbDescription, "Description");
                changedAny |= TryApplyCombo(doc, _cbDesigner, "Designer", _designerStore);
                changedAny |= TryApplyCombo(doc, _cbProject, "Project", _projectStore);
                changedAny |= TryApplyCombo(doc, _cbCategory, "Category", _categoryStore);

                changedAny |= TryApplySupplier(doc);
                var configurableFieldsChanged = _configurableFields.HasChanges;
                var configurableFieldFailures = _configurableFields.Apply(doc);
                changedAny |= configurableFieldsChanged && configurableFieldFailures.Count == 0;
                if (configurableFieldFailures.Count > 0)
                    MessageBox.Show(string.Join(System.Environment.NewLine, configurableFieldFailures.Select(result => result.Error)), "InvPropTab", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                if (changedAny)
                {
                    TryUpdateDocument(doc);
                    ReloadFromDocument();
                }
            }
            catch
            {
            }
        }

        private void ManageList(string title, SupplierStore store)
        {
            try
            {
                using (var dlg = new SupplierManagerForm(title, store))
                {
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.ShowDialog(this);
                }

                store.Save();
            }
            catch
            {
            }
        }

        private bool TryApplyTextBox(Document doc, WinTextBox tb, string propName)
        {
            try
            {
                if (!_originalValues.TryGetValue(tb, out var original))
                    original = string.Empty;
                var current = tb.Text ?? string.Empty;
                if (string.Equals(original, current, StringComparison.Ordinal))
                    return false;

                _propertiesService.TrySetPropertyValue(doc, propName, current);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryApplyCombo(Document doc, ComboBox cb, string propName, SupplierStore store)
        {
            try
            {
                if (!_originalValues.TryGetValue(cb, out var original))
                    original = string.Empty;

                var current = (cb.Text ?? string.Empty).Trim();
                if (string.Equals(original, current, StringComparison.Ordinal))
                    return false;

                if (!string.IsNullOrWhiteSpace(current) && current.Length <= 200)
                {
                    store.AddOrUpdate(current);
                    store.Save();
                }

                _propertiesService.TrySetPropertyValue(doc, propName, current);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryApplySupplier(Document doc)
        {
            try
            {
                if (!_originalValues.TryGetValue(_cbSupplier, out var original))
                    original = string.Empty;
                var current = (_cbSupplier.Text ?? string.Empty).Trim();
                if (string.Equals(original, current, StringComparison.Ordinal))
                    return false;

                // Don't pollute supplier history with formula expressions.
                if (!string.IsNullOrWhiteSpace(current) && !current.Contains("<") && !current.Contains(">") && !current.Contains("=") && current.Length <= 120)
                {
                    _supplierStore.AddOrUpdate(current);
                    _supplierStore.Save();
                }

                var ok = _propertiesService.TrySetSupplierExpression(doc, current);
                if (!ok)
                    ok = _propertiesService.TrySetSupplier(doc, current);
                return ok;
            }
            catch
            {
                return false;
            }
        }

        private void ManageSuppliers()
        {
            try
            {
                using (var dlg = new SupplierManagerForm("Поставщики", _supplierStore))
                {
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.ShowDialog(this);
                }

                _supplierStore.Save();
                LoadSupplierList();
            }
            catch
            {
            }
        }

        private void LoadSupplierList()
        {
            try
            {
                var current = _cbSupplier.Text;
                _cbSupplier.BeginUpdate();
                _cbSupplier.Items.Clear();
                foreach (var s in _supplierStore.Suppliers)
                    _cbSupplier.Items.Add(s);
                _cbSupplier.EndUpdate();
                if (!string.IsNullOrWhiteSpace(current))
                    _cbSupplier.Text = current;
            }
            catch
            {
            }
        }

        private void LoadOtherGrid(Document doc)
        {
            try
            {
                var items = _propertiesService.GetCustomIProperties(doc);
                var dt = new DataTable();
                dt.Columns.Add("Name", typeof(string));
                dt.Columns.Add("Value", typeof(string));
                dt.Columns.Add("Type", typeof(string));

                foreach (var it in items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    dt.Rows.Add(it.Name, it.Value, "Текст");
                }

                _gridCustom.DataSource = dt;
                _gridCustom.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                _gridCustom.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridCustom.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridCustom.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridCustom.Columns[0].FillWeight = 33;
                _gridCustom.Columns[1].FillWeight = 34;
                _gridCustom.Columns[2].FillWeight = 33;
            }
            catch
            {
            }
        }

        private static void TryUpdateDocument(Document doc)
        {
            try
            {
                // Update2(bool) exists in some interop versions. Use reflection to be safe.
                var m = doc.GetType().GetMethod("Update2");
                if (m != null)
                {
                    m.Invoke(doc, new object[] { true });
                    return;
                }

                var m2 = doc.GetType().GetMethod("Update");
                if (m2 != null)
                    m2.Invoke(doc, null);
            }
            catch
            {
            }
        }

        private void UpdateEmptyState()
        {
            try
            {
                var hasDoc = _ctx?.Document != null;
                if (!hasDoc)
                {
                    if (!Controls.Contains(_lblEmpty))
                        Controls.Add(_lblEmpty);
                    _lblEmpty.BringToFront();
                }
                else
                {
                    if (Controls.Contains(_lblEmpty))
                        Controls.Remove(_lblEmpty);
                }
            }
            catch
            {
            }
        }

        private void ApplyReadOnlyState(bool isReadOnly)
        {
            var writable = !isReadOnly;

            _tbPartNumber.ReadOnly = !writable;

            _tbDescription.ReadOnly = !writable;
            _cbDesigner.Enabled = writable;
            _cbProject.Enabled = writable;
            _cbCategory.Enabled = writable;

            _cbSupplier.Enabled = writable;
            _btnSuppliers.Enabled = true;
            _gridCustom.ReadOnly = true;
            _configurableFields.SetReadOnly(isReadOnly);

            _btnApply.Enabled = writable;
        }

        private void SetEnabledState(bool enabled)
        {
            _scrollHost.Enabled = enabled;
        }

        private void ClearFields()
        {
            _tbLocation.Text = string.Empty;
            _tbFileName.Text = string.Empty;
            _tbSize.Text = string.Empty;
            _tbCreated.Text = string.Empty;
            _tbSaved.Text = string.Empty;

            _tbPartNumber.Text = string.Empty;
            _tbDescription.Text = string.Empty;
            _cbDesigner.Text = string.Empty;
            _cbProject.Text = string.Empty;
            _cbCategory.Text = string.Empty;
            _cbSupplier.Text = string.Empty;

            _tbMass.Text = string.Empty;

            _tbDims.Text = string.Empty;
        }

        private static string FormatDims(string x, string y, string z)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(x) || string.IsNullOrWhiteSpace(y) || string.IsNullOrWhiteSpace(z))
                    return string.Empty;
                return x + " х " + y + " х " + z + " мм";
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryGetFullFileName(Document doc)
        {
            try
            {
                return doc.FullFileName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryGetDisplayName(Document doc)
        {
            try
            {
                return doc.DisplayName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryGetCreated(Document doc)
        {
            try
            {
                var p = doc.FullFileName;
                if (string.IsNullOrWhiteSpace(p) || !System.IO.File.Exists(p))
                    return string.Empty;
                return System.IO.File.GetCreationTime(p).ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryGetSaved(Document doc)
        {
            try
            {
                var p = doc.FullFileName;
                if (string.IsNullOrWhiteSpace(p) || !System.IO.File.Exists(p))
                    return string.Empty;
                return System.IO.File.GetLastWriteTime(p).ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryGetSize(Document doc)
        {
            try
            {
                var p = doc.FullFileName;
                if (string.IsNullOrWhiteSpace(p) || !System.IO.File.Exists(p))
                    return string.Empty;
                var fi = new System.IO.FileInfo(p);
                return (fi.Length / 1024.0).ToString("0.#") + " KB";
            }
            catch
            {
                return string.Empty;
            }
        }

        private void BuildSinglePageLayout()
        {
            _btnSuppliers.Text = "...";
            _btnSuppliers.Width = 32;

            _btnOpenLocation.Text = "📂";
            _btnOpenLocation.Width = 32;

            _btnDesigners.Text = "...";
            _btnDesigners.Width = 32;
            _btnProjects.Text = "...";
            _btnProjects.Width = 32;
            _btnCategories.Text = "...";
            _btnCategories.Width = 32;

            var supplierPanel = new Panel { Dock = DockStyle.Top };
            _cbSupplier.Dock = DockStyle.Fill;
            supplierPanel.Controls.Add(_cbSupplier);
            _btnSuppliers.Dock = DockStyle.Right;
            supplierPanel.Controls.Add(_btnSuppliers);
            supplierPanel.Height = _cbSupplier.PreferredHeight;
            _btnSuppliers.Height = _cbSupplier.PreferredHeight;

            var categoryPanel = BuildComboWithButton(_cbCategory, _btnCategories);
            var designerPanel = BuildComboWithButton(_cbDesigner, _btnDesigners);
            var projectPanel = BuildComboWithButton(_cbProject, _btnProjects);

            _gridCustom.Dock = DockStyle.Fill;
            _gridCustom.AllowUserToAddRows = false;
            _gridCustom.AllowUserToDeleteRows = false;
            _gridCustom.MultiSelect = false;
            _gridCustom.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            _gridCustom.Height = 220;

            var locationPanel = new Panel { Dock = DockStyle.Top };
            _tbLocation.Dock = DockStyle.Fill;
            locationPanel.Controls.Add(_tbLocation);
            _btnOpenLocation.Dock = DockStyle.Right;
            locationPanel.Controls.Add(_btnOpenLocation);
            locationPanel.Height = _tbLocation.PreferredHeight;
            _btnOpenLocation.Height = _tbLocation.PreferredHeight;
            AddUnlabeledControl(locationPanel);
            AddUnlabeledControl(_tbFileName);

            AddSeparator();

            AddUnlabeledControl(_configurableFields);
            BuildPhysicalRow();
            AddUnlabeledControl(_physicalRow);
            AddUnlabeledControl(_gridCustom);

            AddSeparator();
            BuildActionsRow();
            AddUnlabeledControl(_actionsRow);
        }

        private void AddUnlabeledControl(Control control)
        {
            control.Dock = DockStyle.Top;
            control.Margin = new Padding(0, 0, 0, 8);
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.Controls.Add(control, 0, _layout.RowCount++);
        }

        private void BuildPhysicalRow()
        {
            _physicalRow.Dock = DockStyle.Top;
            _physicalRow.AutoSize = true;
            _physicalRow.ColumnCount = 2;
            _physicalRow.RowCount = 2;
            _physicalRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _physicalRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _physicalRow.Controls.Add(new Label { Text = "Габариты", Dock = DockStyle.Top, AutoSize = true }, 0, 0);
            _physicalRow.Controls.Add(new Label { Text = "Масса", Dock = DockStyle.Top, AutoSize = true }, 1, 0);
            _tbDims.Dock = DockStyle.Top;
            _tbDims.Margin = new Padding(0, 2, 4, 0);
            _tbMass.Dock = DockStyle.Top;
            _tbMass.Margin = new Padding(4, 2, 0, 0);
            _physicalRow.Controls.Add(_tbDims, 0, 1);
            _physicalRow.Controls.Add(_tbMass, 1, 1);
        }

        private void BuildActionsRow()
        {
            _actionsRow.Dock = DockStyle.Top;
            _actionsRow.AutoSize = true;
            _actionsRow.ColumnCount = 2;
            _actionsRow.RowCount = 1;
            _actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _btnSettings.Text = "Настроить";
            _btnSettings.Height = 32;
            _btnSettings.Dock = DockStyle.Left;
            _btnApply.Text = "Применить";
            _btnApply.Height = 32;
            _btnApply.Dock = DockStyle.Right;
            _actionsRow.Controls.Add(_btnSettings, 0, 0);
            _actionsRow.Controls.Add(_btnApply, 1, 0);
        }

        private void AddControlCentered(Control c)
        {
            var host = new Panel
            {
                Dock = DockStyle.Top,
                Height = c.Height + 6,
                Margin = new Padding(0, 0, 0, 0)
            };

            var targetWidth = Math.Max(c.MinimumSize.Width, (int)(host.ClientSize.Width * 0.66));
            c.Width = targetWidth;
            c.Left = Math.Max(0, (host.ClientSize.Width - c.Width) / 2);
            c.Top = 0;
            c.Anchor = AnchorStyles.Top;
            host.Controls.Add(c);
            host.Resize += (_, __) =>
            {
                try
                {
                    var w = Math.Max(c.MinimumSize.Width, (int)(host.ClientSize.Width * 0.66));
                    c.Width = w;
                    c.Left = Math.Max(0, (host.ClientSize.Width - c.Width) / 2);
                }
                catch
                {
                }
            };

            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.Controls.Add(host, 0, _layout.RowCount);
            _layout.RowCount++;
        }

        private void SnapshotOriginalValues()
        {
            try
            {
                _originalValues[_tbPartNumber] = _tbPartNumber.Text ?? string.Empty;
                _originalValues[_tbDescription] = _tbDescription.Text ?? string.Empty;
                _originalValues[_cbCategory] = _cbCategory.Text ?? string.Empty;
                _originalValues[_cbDesigner] = _cbDesigner.Text ?? string.Empty;
                _originalValues[_cbProject] = _cbProject.Text ?? string.Empty;
                _originalValues[_cbSupplier] = _cbSupplier.Text ?? string.Empty;

                RememberOriginalBackColor(_tbPartNumber);
                RememberOriginalBackColor(_tbDescription);
                RememberOriginalBackColor(_cbCategory);
                RememberOriginalBackColor(_cbDesigner);
                RememberOriginalBackColor(_cbProject);
                RememberOriginalBackColor(_cbSupplier);
            }
            catch
            {
            }
        }

        private static Panel BuildComboWithButton(ComboBox cb, Button btn)
        {
            var p = new Panel { Dock = DockStyle.Top };
            cb.Dock = DockStyle.Fill;
            p.Controls.Add(cb);
            btn.Dock = DockStyle.Right;
            btn.Height = cb.PreferredHeight;
            p.Controls.Add(btn);
            p.Height = cb.PreferredHeight;
            return p;
        }

        private static void LoadHistory(SupplierStore store, ComboBox cb)
        {
            try
            {
                var current = cb.Text;
                cb.BeginUpdate();
                cb.Items.Clear();
                foreach (var s in store.Suppliers)
                    cb.Items.Add(s);
                cb.EndUpdate();
                if (!string.IsNullOrWhiteSpace(current))
                    cb.Text = current;
            }
            catch
            {
            }
        }

        private void RememberOriginalBackColor(Control c)
        {
            if (!_originalBackColors.ContainsKey(c))
                _originalBackColors[c] = c.BackColor;
        }

        private void MarkDirty(Control c)
        {
            try
            {
                if (_suppressEvents)
                    return;

                if (!_originalValues.TryGetValue(c, out var original))
                    original = string.Empty;

                var current = c is ComboBox cb ? (cb.Text ?? string.Empty) : (c.Text ?? string.Empty);
                var dirty = !string.Equals(original, current, StringComparison.Ordinal);
                SetDirtyVisual(c, dirty);
            }
            catch
            {
            }
        }

        private void SetDirtyVisual(Control c, bool dirty)
        {
            RememberOriginalBackColor(c);
            if (dirty)
                c.BackColor = ColorTranslator.FromHtml("#C8EFD4");
            else if (_originalBackColors.TryGetValue(c, out var bc))
                c.BackColor = bc;
        }

        private void ClearDirtyState()
        {
            try
            {
                foreach (var kv in _originalBackColors)
                {
                    try { kv.Key.BackColor = kv.Value; } catch { }
                }
            }
            catch
            {
            }
        }

        private void AddField(string caption, Control editor)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 18,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 6, 0, 0)
            };

            editor.Dock = DockStyle.Top;
            editor.Margin = new Padding(0, 2, 0, 0);

            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.Controls.Add(lbl, 0, _layout.RowCount);
            _layout.RowCount++;

            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.Controls.Add(editor, 0, _layout.RowCount);
            _layout.RowCount++;
        }

        private void AddSeparator()
        {
            var sep = new Panel
            {
                Height = 8,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 8, 0, 0)
            };

            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.Controls.Add(sep, 0, _layout.RowCount);
            _layout.RowCount++;
        }

        private void AddInlineRow(string caption, Control editor)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                Height = 26,
                Margin = new Padding(0, 8, 0, 0),
                Padding = new Padding(0)
            };

            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var lbl = new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };

            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(0);

            row.Controls.Add(lbl, 0, 0);
            row.Controls.Add(editor, 1, 0);

            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.Controls.Add(row, 0, _layout.RowCount);
            _layout.RowCount++;
        }

        private sealed class SupplierManagerForm : Form
        {
            private readonly SupplierStore _store;
            private readonly ListBox _list = new ListBox();
            private readonly WinTextBox _tb = new WinTextBox();
            private readonly Button _add = new Button();
            private readonly Button _rename = new Button();
            private readonly Button _delete = new Button();
            private readonly Button _close = new Button();

            public SupplierManagerForm(string title, SupplierStore store)
            {
                _store = store;

                Text = title;
                Width = 420;
                Height = 360;

                _list.Dock = DockStyle.Fill;
                _tb.Dock = DockStyle.Top;

                _add.Text = "Добавить";
                _rename.Text = "Переименовать";
                _delete.Text = "Удалить";
                _close.Text = "Закрыть";

                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40 };
                buttons.Controls.Add(_add);
                buttons.Controls.Add(_rename);
                buttons.Controls.Add(_delete);
                buttons.Controls.Add(_close);

                Controls.Add(_list);
                Controls.Add(_tb);
                Controls.Add(buttons);

                LoadItems();

                _add.Click += (_, __) =>
                {
                    _store.AddOrUpdate(_tb.Text);
                    LoadItems();
                };

                _rename.Click += (_, __) =>
                {
                    var old = _list.SelectedItem as string;
                    var nv = (_tb.Text ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(old) || string.IsNullOrWhiteSpace(nv))
                        return;
                    _store.Remove(old);
                    _store.AddOrUpdate(nv);
                    LoadItems();
                    _list.SelectedItem = nv;
                };

                _delete.Click += (_, __) =>
                {
                    var v = _list.SelectedItem as string;
                    if (string.IsNullOrWhiteSpace(v))
                        return;
                    _store.Remove(v);
                    LoadItems();
                };

                _close.Click += (_, __) => Close();

                _list.SelectedIndexChanged += (_, __) =>
                {
                    var v = _list.SelectedItem as string;
                    if (!string.IsNullOrWhiteSpace(v))
                        _tb.Text = v;
                };
            }

            private void LoadItems()
            {
                _list.BeginUpdate();
                _list.Items.Clear();
                foreach (var s in _store.Suppliers)
                    _list.Items.Add(s);
                _list.EndUpdate();
            }
        }

    }
}
