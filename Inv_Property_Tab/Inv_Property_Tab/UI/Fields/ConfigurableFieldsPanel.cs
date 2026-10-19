using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Inventor;
using Inv_Property_Tab.Configuration;
using Inv_Property_Tab.Services;
using Inv_Property_Tab.UI.Settings;

namespace Inv_Property_Tab.UI.Fields
{
    public sealed class ConfigurableFieldsPanel : UserControl
    {
        private readonly FieldsConfigurationStore _store;
        private readonly IPropertyAccessor _accessor;
        private readonly FieldEditorFactory _factory;
        private readonly TableLayoutPanel _layout = new TableLayoutPanel();
        private readonly List<FieldEditorBinding> _bindings = new List<FieldEditorBinding>();
        private FieldsConfiguration _configuration = new FieldsConfiguration();

        public ConfigurableFieldsPanel()
            : this(new FieldsConfigurationStore(), new InventorPropertyAccessor(), new FieldEditorFactory())
        {
        }

        public ConfigurableFieldsPanel(FieldsConfigurationStore store, IPropertyAccessor accessor, FieldEditorFactory factory)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Dock = DockStyle.Top;
            AutoSize = true;
            _layout.Dock = DockStyle.Top;
            _layout.AutoSize = true;
            _layout.ColumnCount = 1;
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(_layout);
            Rebuild();
        }

        public void Rebuild()
        {
            _layout.SuspendLayout();
            try
            {
                _layout.Controls.Clear();
                _layout.RowStyles.Clear();
                _layout.RowCount = 0;
                _bindings.Clear();
                _configuration = _store.Load();
                foreach (var definition in _configuration.Fields.Where(field => field.Visible && field.Id != "mass" && field.Id != "dimensions").OrderBy(field => field.Order).ThenBy(field => field.Id, StringComparer.OrdinalIgnoreCase))
                {
                    var binding = _factory.Create(definition);
                    binding.EditValuesRequested += (_, __) => EditValues(binding, this);
                    _bindings.Add(binding);
                    AddField(definition.Label, binding.DisplayControl);
                }
            }
            finally
            {
                _layout.ResumeLayout(true);
            }
        }

        public void LoadDocument(Document document)
        {
            foreach (var binding in _bindings)
                binding.AcceptValue(_accessor.Read(document, binding.Definition));
        }

        public bool HasChanges => _bindings.Any(item => item.IsChanged && item.Definition.Editor != FieldEditors.ReadOnly);

        public bool EditConfiguration(IWin32Window owner)
        {
            using (var form = new FieldsSettingsForm(_store.Load(), _store.LoadDefault))
            {
                if (form.ShowDialog(owner) != DialogResult.OK)
                    return false;
                _store.Save(form.Configuration);
            }
            Rebuild();
            return true;
        }

        private bool EditValues(FieldEditorBinding binding, IWin32Window owner)
        {
            if (binding.Definition.Editor != FieldEditors.List)
                return false;
            using (var form = new ListValuesEditorForm(binding.Definition.Label, binding.Definition.Values, () => LoadLatestValues(binding.Definition.Id)))
            {
                if (form.ShowDialog(owner) != DialogResult.OK)
                    return false;
                var latest = _store.Load();
                var target = latest.Fields.FirstOrDefault(field => string.Equals(field.Id, binding.Definition.Id, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                    return false;
                target.Values = ListValuesModel.Normalize(form.Values);
                _store.Save(latest);
                _configuration = latest;
                binding.ReplaceValues(target.Values);
                return true;
            }
        }

        private IReadOnlyList<string> LoadLatestValues(string fieldId)
        {
            var latest = _store.Load();
            var target = latest.Fields.FirstOrDefault(field => string.Equals(field.Id, fieldId, StringComparison.OrdinalIgnoreCase));
            return target?.Values ?? new List<string>();
        }

        public void SetReadOnly(bool readOnly)
        {
            foreach (var binding in _bindings)
            {
                if (binding.Control is TextBoxBase textBox)
                    textBox.ReadOnly = readOnly || binding.Definition.Editor == FieldEditors.ReadOnly;
                else
                    binding.Control.Enabled = !readOnly && binding.Definition.Editor != FieldEditors.ReadOnly;
            }
        }

        public IReadOnlyList<PropertyWriteResult> Apply(Document document)
        {
            var failures = new List<PropertyWriteResult>();
            var historyChanged = false;
            foreach (var binding in _bindings.Where(item => item.IsChanged && item.Definition.Editor != FieldEditors.ReadOnly))
            {
                var result = _accessor.Write(document, binding.Definition, binding.Value);
                if (result.Success)
                {
                    historyChanged |= binding.RememberAppliedValue();
                    binding.AcceptValue(binding.Value);
                }
                else
                {
                    failures.Add(result);
                }
            }
            if (historyChanged)
            {
                try
                {
                    _store.Save(_configuration);
                }
                catch (Exception ex)
                {
                    failures.Add(PropertyWriteResult.Failed("Не удалось сохранить список значений: " + ex.Message));
                }
            }
            return failures;
        }

        private void AddField(string labelText, Control editor)
        {
            var host = new Panel { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
            var label = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Text = labelText,
                Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 3)
            };
            host.Controls.Add(editor);
            host.Controls.Add(label);
            editor.BringToFront();
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.Controls.Add(host, 0, _layout.RowCount++);
        }
    }
}
