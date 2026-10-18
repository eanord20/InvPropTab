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
        private readonly BindingList<FieldRow> _rows = new BindingList<FieldRow>();
        private readonly FieldsConfigurationValidator _validator = new FieldsConfigurationValidator();
        private readonly Func<FieldsConfiguration>? _loadDefault;
        private List<FieldDefinition> _unknownFields = new List<FieldDefinition>();

        public FieldsSettingsForm(FieldsConfiguration configuration, Func<FieldsConfiguration>? loadDefault = null)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            _loadDefault = loadDefault;
            Configuration = configuration;
            Text = "Настройка полей";
            Width = 650;
            Height = 620;
            StartPosition = FormStartPosition.CenterParent;
            BuildGrid();
            BuildButtons();
            LoadRows(configuration);
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
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(FieldRow.Visible), HeaderText = "Показывать", Width = 80 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(FieldRow.Label), HeaderText = "Свойство", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(FieldRow.RememberValues), HeaderText = "Запоминать", Width = 90 });
            _grid.CellBeginEdit += Grid_CellBeginEdit;
            _grid.CellFormatting += Grid_CellFormatting;
            Controls.Add(_grid);
        }

        private void BuildButtons()
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 76, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6), AutoScroll = true };
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

        private void Grid_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            if (_grid.Columns[e.ColumnIndex].DataPropertyName == nameof(FieldRow.RememberValues) && !_rows[e.RowIndex].CanRemember)
                e.Cancel = true;
        }

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_grid.Columns[e.ColumnIndex].DataPropertyName == nameof(FieldRow.RememberValues) && !_rows[e.RowIndex].CanRemember)
                e.CellStyle.ForeColor = System.Drawing.SystemColors.GrayText;
        }

        private void MoveSelected(int offset)
        {
            var index = _grid.CurrentRow?.Index ?? -1;
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
                    LoadRows(configuration);
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
                using (var stream = File.Create(dialog.FileName))
                    new DataContractJsonSerializer(typeof(FieldsConfiguration)).WriteObject(stream, configuration);
            }
        }

        private void RestoreDefaults()
        {
            if (_loadDefault == null || MessageBox.Show("Восстановить заводской набор полей?", "InvPropTab", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            LoadRows(_loadDefault());
        }

        private void LoadRows(FieldsConfiguration configuration)
        {
            var merged = FieldSettingsMerger.Merge(InventorPropertyCatalog.All, configuration);
            var catalogIds = new HashSet<string>(InventorPropertyCatalog.All.Select(field => field.Id), StringComparer.OrdinalIgnoreCase);
            _unknownFields = merged.Fields.Where(field => !catalogIds.Contains(field.Id)).Select(FieldSettingsMerger.Clone).ToList();
            _rows.RaiseListChangedEvents = false;
            _rows.Clear();
            foreach (var field in merged.Fields.Where(field => catalogIds.Contains(field.Id)).OrderBy(field => field.Order))
                _rows.Add(FieldRow.FromDefinition(field));
            _rows.RaiseListChangedEvents = true;
            _rows.ResetBindings();
        }

        private FieldsConfiguration BuildConfiguration()
        {
            _grid.EndEdit();
            var fields = _rows.Select((row, index) => row.ToDefinition((index + 1) * 10)).ToList();
            fields.AddRange(_unknownFields.Select(FieldSettingsMerger.Clone));
            var configuration = new FieldsConfiguration { Version = 1, Fields = fields };
            var errors = _validator.Validate(configuration);
            if (errors.Count > 0)
                throw new InvalidDataException(string.Join(System.Environment.NewLine, errors));
            return configuration;
        }

        private void SaveConfiguration()
        {
            Configuration = BuildConfiguration();
            DialogResult = DialogResult.OK;
            Close();
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (_, __) => action();
            return button;
        }

        private sealed class FieldRow
        {
            public FieldDefinition Definition { get; set; } = new FieldDefinition();
            public bool Visible { get; set; }
            public string Label { get; set; } = string.Empty;
            public bool RememberValues { get; set; }
            public bool CanRemember => Definition.Editor == FieldEditors.List && !Definition.Id.Equals("description", StringComparison.OrdinalIgnoreCase);

            public static FieldRow FromDefinition(FieldDefinition definition)
            {
                return new FieldRow
                {
                    Definition = FieldSettingsMerger.Clone(definition),
                    Visible = definition.Visible,
                    Label = definition.Label,
                    RememberValues = definition.RememberValues
                };
            }

            public FieldDefinition ToDefinition(int order)
            {
                var result = FieldSettingsMerger.Clone(Definition);
                result.Visible = Visible;
                result.Order = order;
                result.RememberValues = CanRemember && RememberValues;
                return result;
            }
        }
    }
}
