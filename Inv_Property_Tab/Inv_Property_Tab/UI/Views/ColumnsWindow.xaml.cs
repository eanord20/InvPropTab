using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Inv_Property_Tab.UI.Views
{
    public partial class ColumnsWindow : Window
    {
        private class ColumnItem
        {
            public string Key { get; }
            public string Header { get; }
            public bool IsSelected { get; set; }

            public ColumnItem(string key, string header, bool selected)
            {
                Key = key;
                Header = header;
                IsSelected = selected;
            }
        }

        private readonly ObservableCollection<ColumnItem> _items;

        public IReadOnlyCollection<string> SelectedKeys => _items.Where(i => i.IsSelected).Select(i => i.Key).ToList();

        public ColumnsWindow(List<(string key, string header)> availableColumns, List<string> selectedKeys)
        {
            InitializeComponent();

            var selected = new HashSet<string>(selectedKeys ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            _items = new ObservableCollection<ColumnItem>(
                availableColumns.Select(c => new ColumnItem(c.key, c.header, selected.Contains(c.key))));

            ColumnsList.ItemsSource = _items;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
