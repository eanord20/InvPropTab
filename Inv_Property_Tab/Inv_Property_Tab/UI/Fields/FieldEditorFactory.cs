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
            Control displayControl = control;
            Button? valuesButton = null;
            if (control is ComboBox)
            {
                var host = new Panel { Dock = DockStyle.Top, Height = control.PreferredSize.Height };
                control.Dock = DockStyle.Fill;
                valuesButton = new Button { Text = "...", Dock = DockStyle.Right, Width = 32, Height = control.PreferredSize.Height };
                host.Controls.Add(control);
                host.Controls.Add(valuesButton);
                displayControl = host;
            }
            var binding = new FieldEditorBinding(definition, control, displayControl);
            control.TextChanged += (_, __) => binding.RefreshDirtyState();
            if (control is ComboBox comboBoxControl)
                comboBoxControl.SelectionChangeCommitted += (_, __) => binding.RefreshDirtyState();
            if (valuesButton != null)
                valuesButton.Click += (_, __) => binding.RequestEditValues();
            return binding;
        }
    }
}
