using System;
using System.Drawing;
using System.Windows.Forms;
using Inv_Property_Tab.Configuration;

namespace Inv_Property_Tab.UI.Fields
{
    public sealed class FieldEditorFactory
    {
        public FieldEditorBinding Create(FieldDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            Control control;
            switch (definition.Editor)
            {
                case FieldEditors.Multiline:
                    control = new TextBox { Multiline = true, Height = 64, ScrollBars = ScrollBars.Vertical };
                    break;
                case FieldEditors.List:
                    var comboBox = new ComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDown,
                        AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                        AutoCompleteSource = AutoCompleteSource.ListItems
                    };
                    if (definition.Values != null)
                        comboBox.Items.AddRange(definition.Values.ToArray());
                    control = comboBox;
                    break;
                case FieldEditors.ReadOnly:
                    control = new TextBox { ReadOnly = true };
                    break;
                case FieldEditors.Number:
                case FieldEditors.Text:
                    control = new TextBox();
                    break;
                default:
                    throw new ArgumentException("Unsupported editor: " + definition.Editor, nameof(definition));
            }

            control.Dock = DockStyle.Top;
            control.Margin = new Padding(0);
            control.Font = SystemFonts.MessageBoxFont;
            var binding = new FieldEditorBinding(definition, control);
            control.TextChanged += (_, __) => binding.RefreshDirtyState();
            if (control is ComboBox comboBoxControl)
                comboBoxControl.SelectionChangeCommitted += (_, __) => binding.RefreshDirtyState();
            return binding;
        }
    }
}
