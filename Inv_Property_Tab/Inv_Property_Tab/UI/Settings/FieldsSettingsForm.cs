using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;
using Inv_Property_Tab.Configuration;

namespace Inv_Property_Tab.UI.Settings
{
    public sealed class FieldsSettingsForm : Form
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly BindingList<FieldRow> _rows;
        private readonly FieldsConfigurationValidator _validator = new FieldsConfigurationValidator();
        private readonly Func<FieldsConfiguration>? _loadDefault;

        public FieldsSettingsForm(FieldsConfiguration configuration, Func<FieldsConfiguration>? loadDefault = null)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            Configuration = configuration;
            _loadDefault = loadDefault;
            _rows = new BindingList<FieldRow>(configuration.Fields.OrderBy(field => field.Order).Select(FieldRow.FromDefinition).ToList());
            Text = "Настройка полей";
            Width = 1100;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            BuildGrid();
            BuildButtons();
        }

        public FieldsConfiguration Configuration { get; private set; }

        private void BuildGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.DataSource = _rows;
            _grid.Columns.Add(TextColumn(nameof(FieldRow.Id), "ID", 110));
            _grid.Columns.Add(TextColumn(nameof(FieldRow.Label), "Подпись", 140));
            _grid.Columns.Add(ComboColumn(nameof(FieldRow.Source), "Источник", new[] { FieldSources.Standard, FieldSources.Custom, FieldSources.System }, 90));
            _grid.Columns.Add(TextColumn(nameof(FieldRow.PropertySetId), "PropertySet ID", 130));
            _grid.Columns.Add(TextColumn(nameof(FieldRow.PropertyName), "Свойство", 120));
            _grid.Columns.Add(ComboColumn(nameof(FieldRow.Editor), "Редактор", new[] { FieldEditors.Text, FieldEditors.Multiline, FieldEditors.List, FieldEditors.Number, FieldEditors.ReadOnly }, 90));
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(FieldRow.Visible), HeaderText = "Видимое", Width = 65 });
            _grid.Columns.Add(TextColumn(nameof(FieldRow.Values), "Значения списка", 160));
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(FieldRow.RememberValues), HeaderText = "Запоминать", Width = 80 });
            Controls.Add(_grid);
        }

        private void BuildButtons()
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6) };
            panel.Controls.Add(CreateButton("Добавить", AddField));
            panel.Controls.Add(CreateButton("Удалить", DeleteField));
            panel.Controls.Add(CreateButton("Вверх", () => MoveSelected(-1)));
            panel.Controls.Add(CreateButton("Вниз", () => MoveSelected(1)));
            panel.Controls.Add(CreateButton("Импорт", ImportConfiguration));
            panel.Controls.Add(CreateButton("Экспорт", ExportConfiguration));
            panel.Controls.Add(CreateButton("По умолчанию", RestoreDefaults));
            panel.Controls.Add(CreateButton("Сохранить", SaveConfiguration));
            panel.Controls.Add(CreateButton("Отмена", () => { DialogResult = DialogResult.Cancel; Close(); }));
            Controls.Add(panel);
            panel.BringToFront();
        }

        private void AddField()
        {
            var id = "field" + (_rows.Count + 1);
            _rows.Add(new FieldRow { Id = id, Label = id, Source = FieldSources.Custom, Editor = FieldEditors.Text, Visible = true });
            _grid.CurrentCell = _grid.Rows[_rows.Count - 1].Cells[0];
        }

        private void DeleteField()
        {
            var index = SelectedIndex();
            if (index >= 0)
                _rows.RemoveAt(index);
        }

        private void MoveSelected(int offset)
        {
            var index = SelectedIndex();
            var target = index + offset;
            if (index < 0 || target < 0 || target >= _rows.Count)
                return;
            var item = _rows[index];
            _rows.RemoveAt(index);
            _rows.Insert(target, item);
            _grid.CurrentCell = _grid.Rows[target].Cells[0];
        }

        private void ImportConfiguration()
        {
            using (var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                using (var stream = File.OpenRead(dialog.FileName))
                {
                    var configuration = new DataContractJsonSerializer(typeof(FieldsConfiguration)).ReadObject(stream) as FieldsConfiguration;
                    if (configuration == null)
                        throw new InvalidDataException("Файл конфигурации пуст.");
                    var errors = _validator.Validate(configuration);
                    if (errors.Count > 0)
                        throw new InvalidDataException(string.Join(System.Environment.NewLine, errors));
                    ReplaceRows(configuration);
                }
            }
        }

        private void ExportConfiguration()
        {
            using (var dialog = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "fields.json", OverwritePrompt = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                var configuration = BuildConfiguration();
                var errors = _validator.Validate(configuration);
                if (errors.Count > 0)
                    throw new InvalidDataException(string.Join(System.Environment.NewLine, errors));
                using (var stream = File.Create(dialog.FileName))
                    new DataContractJsonSerializer(typeof(FieldsConfiguration)).WriteObject(stream, configuration);
            }
        }

        private void RestoreDefaults()
        {
            if (_loadDefault == null || MessageBox.Show("Восстановить заводской набор полей?", "InvPropTab", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            ReplaceRows(_loadDefault());
        }

        private void ReplaceRows(FieldsConfiguration configuration)
        {
            _rows.RaiseListChangedEvents = false;
            _rows.Clear();
            foreach (var field in configuration.Fields.OrderBy(field => field.Order))
                _rows.Add(FieldRow.FromDefinition(field));
            _rows.RaiseListChangedEvents = true;
            _rows.ResetBindings();
        }

        private FieldsConfiguration BuildConfiguration()
        {
            _grid.EndEdit();
            return new FieldsConfiguration
            {
                Version = 1,
                Fields = _rows.Select((row, index) => row.ToDefinition((index + 1) * 10)).ToList()
            };
        }

        private void SaveConfiguration()
        {
            var configuration = BuildConfiguration();
            var errors = _validator.Validate(configuration);
            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join(System.Environment.NewLine, errors), "InvPropTab", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Configuration = configuration;
            DialogResult = DialogResult.OK;
            Close();
        }

        private int SelectedIndex()
        {
            return _grid.CurrentRow?.Index ?? -1;
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (_, __) => action();
            return button;
        }

        private static DataGridViewTextBoxColumn TextColumn(string property, string title, int width)
        {
            return new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = title, Width = width };
        }

        private static DataGridViewComboBoxColumn ComboColumn(string property, string title, IEnumerable<string> values, int width)
        {
            var column = new DataGridViewComboBoxColumn { DataPropertyName = property, HeaderText = title, Width = width, FlatStyle = FlatStyle.Flat };
            column.Items.AddRange(values.Cast<object>().ToArray());
            return column;
        }

        private sealed class FieldRow
        {
            public string Id { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public string Source { get; set; } = FieldSources.Standard;
            public string PropertySetId { get; set; } = string.Empty;
            public string PropertyName { get; set; } = string.Empty;
            public string Editor { get; set; } = FieldEditors.Text;
            public bool Visible { get; set; }
            public string Values { get; set; } = string.Empty;
            public bool RememberValues { get; set; }

            public static FieldRow FromDefinition(FieldDefinition definition)
            {
                return new FieldRow
                {
                    Id = definition.Id,
                    Label = definition.Label,
                    Source = definition.Source,
                    PropertySetId = definition.PropertySetId ?? string.Empty,
                    PropertyName = definition.PropertyName ?? string.Empty,
                    Editor = definition.Editor,
                    Visible = definition.Visible,
                    Values = string.Join("; ", definition.Values ?? new List<string>()),
                    RememberValues = definition.RememberValues
                };
            }

            public FieldDefinition ToDefinition(int order)
            {
                return new FieldDefinition
                {
                    Id = (Id ?? string.Empty).Trim(),
                    Label = (Label ?? string.Empty).Trim(),
                    Source = Source,
                    PropertySetId = string.IsNullOrWhiteSpace(PropertySetId) ? null : PropertySetId.Trim(),
                    PropertyName = string.IsNullOrWhiteSpace(PropertyName) ? null : PropertyName.Trim(),
                    Editor = Editor,
                    Visible = Visible,
                    Order = order,
                    Values = (Values ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    RememberValues = RememberValues
                };
            }
        }
    }
}
