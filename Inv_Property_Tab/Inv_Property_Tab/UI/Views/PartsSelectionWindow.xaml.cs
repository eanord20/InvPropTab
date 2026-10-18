using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Inv_Property_Tab.Export;
using Inv_Property_Tab.UI.ViewModels;
using Inventor;

namespace Inv_Property_Tab.UI.Views
{
    public partial class PartsSelectionWindow : Window
    {
        private readonly Inventor.Application _invApp;
        private readonly PartsSelectionViewModel _vm;
        private readonly List<DataGridColumn> _dynamicColumns = new List<DataGridColumn>();

        public PartsSelectionWindow(
            Inventor.Application invApp,
            System.Collections.Generic.IReadOnlyList<Document> parts,
            string defaultOutputFolder)
        {
            InitializeComponent();

            _invApp = invApp;
            _vm = new PartsSelectionViewModel(parts, defaultOutputFolder);
            DataContext = _vm;

            ApplyColumnConfig(LoadColumnConfig());

            try
            {
                var hwnd = new IntPtr(_invApp.MainFrameHWND);
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                helper.Owner = hwnd;
            }
            catch
            {
            }

        }

        [DataContract]
        private class ColumnConfig
        {
            [DataMember] public List<string> VisibleKeys { get; set; } = new List<string>();
        }

        private static readonly (string key, string header, string bindingPath, double width)[] OptionalColumns =
        {
            ("location", "Location", "Location", 220),
            ("size", "Size", "Size", 150),
            ("created", "Created", "Created", 130),
            ("saved", "Saved", "Saved", 130),
            ("developedBy", "Developed by", "DevelopedBy", 140),
            ("project", "Project", "Project", 120),
            ("mass", "Mass", "Mass", 90),
            ("supplier", "Supplier", "Supplier", 160),
            ("vendor", "Vendor", "Vendor", 140),
            ("material", "Material", "Material", 140),
        };

        private static string GetColumnsConfigPath()
        {
            var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Inv_Property_Tab");
            System.IO.Directory.CreateDirectory(dir);
            return System.IO.Path.Combine(dir, "columns.json");
        }

        private static ColumnConfig LoadColumnConfig()
        {
            try
            {
                var path = GetColumnsConfigPath();
                if (!System.IO.File.Exists(path))
                {
                    // Default: Export, Preview, Name, Plane are fixed; optional defaults below.
                    return new ColumnConfig
                    {
                        VisibleKeys = new List<string> { "size", "mass", "project", "supplier" }
                    };
                }

                using (var fs = System.IO.File.OpenRead(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(ColumnConfig));
                    var cfg = ser.ReadObject(fs) as ColumnConfig;
                    if (cfg != null && cfg.VisibleKeys != null && cfg.VisibleKeys.Count > 0)
                        return cfg;
                }
            }
            catch
            {
            }

            return new ColumnConfig
            {
                VisibleKeys = new List<string> { "size", "mass", "project", "supplier" }
            };
        }

        private static void SaveColumnConfig(ColumnConfig cfg)
        {
            try
            {
                var path = GetColumnsConfigPath();
                using (var fs = System.IO.File.Create(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(ColumnConfig));
                    ser.WriteObject(fs, cfg);
                }
            }
            catch
            {
            }
        }

        private void ApplyColumnConfig(ColumnConfig cfg)
        {
            try
            {
                if (PartsGrid == null)
                    return;

                // Remove previously added optional columns
                foreach (var c in _dynamicColumns)
                {
                    try { PartsGrid.Columns.Remove(c); } catch { }
                }
                _dynamicColumns.Clear();

                var visible = new HashSet<string>(cfg.VisibleKeys ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                foreach (var col in OptionalColumns)
                {
                    if (!visible.Contains(col.key))
                        continue;

                    var dc = new DataGridTextColumn
                    {
                        Header = col.header,
                        Binding = new System.Windows.Data.Binding(col.bindingPath),
                        Width = col.width,
                    };
                    PartsGrid.Columns.Add(dc);
                    _dynamicColumns.Add(dc);
                }
            }
            catch
            {
            }
        }

        private ColumnConfig GetCurrentConfigFromGrid()
        {
            var keys = new List<string>();
            foreach (var c in _dynamicColumns)
            {
                if (c is DataGridTextColumn tc)
                {
                    var header = tc.Header?.ToString() ?? string.Empty;
                    var match = OptionalColumns.FirstOrDefault(o => string.Equals(o.header, header, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(match.key))
                        keys.Add(match.key);
                }
            }

            return new ColumnConfig { VisibleKeys = keys };
        }

        private void ColumnHeader_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            try
            {
                var header = sender as DataGridColumnHeader;
                if (header == null)
                    return;

                var cfg = GetCurrentConfigFromGrid();
                var visible = new HashSet<string>(cfg.VisibleKeys ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

                var menu = new System.Windows.Controls.ContextMenu();
                foreach (var col in OptionalColumns)
                {
                    var mi = new System.Windows.Controls.MenuItem
                    {
                        Header = col.header,
                        IsCheckable = true,
                        IsChecked = visible.Contains(col.key),
                    };

                    mi.Click += (_, __) =>
                    {
                        var newCfg = LoadColumnConfig();
                        var set = new HashSet<string>(newCfg.VisibleKeys ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                        if (mi.IsChecked)
                            set.Add(col.key);
                        else
                            set.Remove(col.key);

                        newCfg.VisibleKeys = set.ToList();
                        SaveColumnConfig(newCfg);
                        ApplyColumnConfig(newCfg);
                    };

                    menu.Items.Add(mi);
                }

                header.ContextMenu = menu;
            }
            catch
            {
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "Select output folder for DXF";
                try
                {
                    if (!string.IsNullOrWhiteSpace(_vm.OutputFolder) && System.IO.Directory.Exists(_vm.OutputFolder))
                        dlg.SelectedPath = _vm.OutputFolder;
                }
                catch
                {
                }
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    _vm.OutputFolder = dlg.SelectedPath;
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedDocs = _vm.Parts.Where(p => p.IsSelected).Select(p => p.Document).ToList();
                var selectedRows = _vm.Parts.Where(p => p.IsSelected).ToList();
                if (!selectedRows.Any())
                {
                    System.Windows.MessageBox.Show("Select at least one part.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_vm.OutputFolder))
                {
                    System.Windows.MessageBox.Show("Select output folder.");
                    return;
                }

                var exporter = new DxfExportService();
                bool ConfirmOverwrite(string path)
                {
                    try
                    {
                        if (!System.IO.File.Exists(path))
                            return true;
                    }
                    catch
                    {
                        return true;
                    }

                    var r = System.Windows.MessageBox.Show(
                        $"File already exists:\n{path}\n\nOverwrite?",
                        "DXF Export",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    return r == MessageBoxResult.Yes;
                }

                var requests = selectedRows
                    .Select(r => new PartExportRequest(r.Document, r.Orientation))
                    .ToList();

                var report = exporter.ExportToDxf(
                    _invApp,
                    requests,
                    _vm.OutputFolder,
                    includeFileName: _vm.IncludeFileName,
                    includeDimensions: _vm.IncludeDimensions,
                    confirmOverwrite: ConfirmOverwrite);

                System.Windows.MessageBox.Show(
                    $"Done.\nExported: {report.ExportedCount}\nSkipped (not sheet metal): {report.SkippedNotSheetMetal}\nSkipped (exists): {report.SkippedExisting}\nErrors: {report.ErrorsCount}",
                    "DXF Export");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString(), "DXF Export");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
