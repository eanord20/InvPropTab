using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Inv_Property_Tab.UI.Settings
{
    public sealed class ListValuesEditorForm : Form
    {
        private readonly ListBox _list = new ListBox();
        private readonly TextBox _value = new TextBox();

        public ListValuesEditorForm(string fieldLabel, IEnumerable<string> values)
        {
            Text = "Значения — " + fieldLabel;
            Width = 460;
            Height = 480;
            StartPosition = FormStartPosition.CenterParent;
            Values = ListValuesModel.Normalize(values);
            BuildLayout();
            ReloadList();
        }

        public List<string> Values { get; private set; }

        private void BuildLayout()
        {
            _list.Dock = DockStyle.Fill;
            _list.SelectedIndexChanged += (_, __) => _value.Text = _list.SelectedItem?.ToString() ?? string.Empty;
            Controls.Add(_list);

            var editPanel = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 36, ColumnCount = 3, Padding = new Padding(4) };
            editPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            editPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            editPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _value.Dock = DockStyle.Fill;
            editPanel.Controls.Add(_value, 0, 0);
            editPanel.Controls.Add(CreateButton("Добавить", AddValue), 1, 0);
            editPanel.Controls.Add(CreateButton("Изменить", EditValue), 2, 0);
            Controls.Add(editPanel);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(4), FlowDirection = FlowDirection.LeftToRight };
            actions.Controls.Add(CreateButton("Удалить", DeleteValue));
            actions.Controls.Add(CreateButton("Очистить", ClearValues));
            actions.Controls.Add(CreateButton("Сохранить", SaveValues));
            actions.Controls.Add(CreateButton("Отмена", () => { DialogResult = DialogResult.Cancel; Close(); }));
            Controls.Add(actions);
        }

        private void AddValue()
        {
            Values.Add(_value.Text);
            NormalizeAndReload();
        }

        private void EditValue()
        {
            if (_list.SelectedIndex < 0)
                return;
            Values[_list.SelectedIndex] = _value.Text;
            NormalizeAndReload();
        }

        private void DeleteValue()
        {
            if (_list.SelectedIndex < 0)
                return;
            Values.RemoveAt(_list.SelectedIndex);
            ReloadList();
        }

        private void ClearValues()
        {
            Values.Clear();
            ReloadList();
        }

        private void SaveValues()
        {
            Values = ListValuesModel.Normalize(Values);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void NormalizeAndReload()
        {
            Values = ListValuesModel.Normalize(Values);
            _value.Clear();
            ReloadList();
        }

        private void ReloadList()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            _list.Items.AddRange(Values.Cast<object>().ToArray());
            _list.EndUpdate();
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (_, __) => action();
            return button;
        }
    }
}
