using System;
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

        public void AcceptValue(string value)
        {
            Value = value;
            OriginalValue = Value;
        }
    }
}
