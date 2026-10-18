using System;
using System.Linq;
using System.Windows.Forms;
using Inv_Property_Tab.Configuration;

namespace Inv_Property_Tab.UI.Fields
{
    public sealed class FieldEditorBinding
    {
        public FieldEditorBinding(FieldDefinition definition, Control control)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Control = control ?? throw new ArgumentNullException(nameof(control));
        }

        public FieldDefinition Definition { get; }
        public Control Control { get; }
        public string OriginalValue { get; private set; } = string.Empty;

        public string Value
        {
            get => Control.Text ?? string.Empty;
            set => Control.Text = value ?? string.Empty;
        }

        public bool IsChanged => !string.Equals(OriginalValue, Value, StringComparison.Ordinal);

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

        public void AcceptValue(string value)
        {
            Value = value;
            OriginalValue = Value;
        }
    }
}
