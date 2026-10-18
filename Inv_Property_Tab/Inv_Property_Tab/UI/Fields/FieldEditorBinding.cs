using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Inv_Property_Tab.Configuration;

namespace Inv_Property_Tab.UI.Fields
{
    public sealed class FieldEditorBinding
    {
        private readonly Color _originalBackColor;
        private bool _suppressEvents;

        public FieldEditorBinding(FieldDefinition definition, Control control, Control? displayControl = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Control = control ?? throw new ArgumentNullException(nameof(control));
            DisplayControl = displayControl ?? control;
            _originalBackColor = control.BackColor;
        }

        public event EventHandler? EditValuesRequested;

        public FieldDefinition Definition { get; }
        public Control Control { get; }
        public Control DisplayControl { get; }
        public string OriginalValue { get; private set; } = string.Empty;

        public string Value
        {
            get => Control.Text ?? string.Empty;
            set => Control.Text = value ?? string.Empty;
        }

        public bool IsChanged => !string.Equals(OriginalValue, Value, StringComparison.Ordinal);

        public void RequestEditValues()
        {
            EditValuesRequested?.Invoke(this, EventArgs.Empty);
        }

        public void ReplaceValues(System.Collections.Generic.IEnumerable<string> values)
        {
            var current = Value;
            Definition.Values = Inv_Property_Tab.UI.Settings.ListValuesModel.Normalize(values);
            if (Control is ComboBox comboBox)
            {
                comboBox.Items.Clear();
                comboBox.Items.AddRange(Definition.Values.ToArray());
            }
            Value = current;
        }

        public bool RememberAppliedValue()
        {
            if (string.Equals(Definition.Id, "description", StringComparison.OrdinalIgnoreCase) ||
                Definition.Editor != FieldEditors.List || !Definition.RememberValues)
                return false;
            var value = Value.Trim();
            if (value.Length == 0 || Definition.Values.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
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

        public void RefreshDirtyState()
        {
            if (!_suppressEvents)
                Control.BackColor = IsChanged ? FieldEditorColors.Changed : _originalBackColor;
        }

        public void AcceptValue(string value)
        {
            _suppressEvents = true;
            try
            {
                Value = value;
                OriginalValue = Value;
                Control.BackColor = _originalBackColor;
            }
            finally
            {
                _suppressEvents = false;
            }
        }
    }
}
