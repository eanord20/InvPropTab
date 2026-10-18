using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Inv_Property_Tab.UI.Models;
using Inventor;

namespace Inv_Property_Tab.UI.ViewModels
{
    public class PartsSelectionViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<PartRow> Parts { get; }

        private string _outputFolder;
        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                _outputFolder = value;
                OnPropertyChanged();
            }
        }

        private bool _includeFileName = true;
        public bool IncludeFileName
        {
            get => _includeFileName;
            set
            {
                _includeFileName = value;
                OnPropertyChanged();
            }
        }

        private bool _includeDimensions = true;
        public bool IncludeDimensions
        {
            get => _includeDimensions;
            set
            {
                _includeDimensions = value;
                OnPropertyChanged();
            }
        }

        public PartsSelectionViewModel(System.Collections.Generic.IReadOnlyList<Document> docs, string defaultOutputFolder)
        {
            Parts = new ObservableCollection<PartRow>();
            foreach (var d in docs)
                Parts.Add(new PartRow(d));

            _outputFolder = defaultOutputFolder;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
