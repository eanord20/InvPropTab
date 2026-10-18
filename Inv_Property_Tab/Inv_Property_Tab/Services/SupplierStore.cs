using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Inv_Property_Tab.Services
{
    public class SupplierStore
    {
        [DataContract]
        private class SupplierData
        {
            [DataMember] public List<string> Suppliers { get; set; } = new List<string>();
        }

        private readonly List<string> _suppliers = new List<string>();

        private readonly string _fileName;

        public SupplierStore(string? fileName = null)
        {
            _fileName = string.IsNullOrWhiteSpace(fileName) ? "suppliers.json" : fileName;
        }

        public IReadOnlyList<string> Suppliers => _suppliers;

        public void SetSuppliers(IEnumerable<string> suppliers)
        {
            _suppliers.Clear();
            foreach (var s in suppliers)
            {
                var v = (s ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(v))
                    continue;
                if (_suppliers.Any(x => string.Equals(x, v, StringComparison.OrdinalIgnoreCase)))
                    continue;
                _suppliers.Add(v);
            }

            _suppliers.Sort(StringComparer.CurrentCultureIgnoreCase);
        }

        public void AddOrUpdate(string value)
        {
            var v = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(v))
                return;

            foreach (var s in _suppliers)
            {
                if (string.Equals(s, v, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            _suppliers.Add(v);
            _suppliers.Sort(StringComparer.CurrentCultureIgnoreCase);
        }

        public void Remove(string? value)
        {
            var v = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(v))
                return;

            _suppliers.RemoveAll(s => string.Equals(s, v, StringComparison.OrdinalIgnoreCase));
        }

        private string GetStorePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InvPropTab");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, _fileName);
        }

        public void Load()
        {
            try
            {
                var path = GetStorePath();
                if (!File.Exists(path))
                {
                    SetSuppliers(Array.Empty<string>());
                    return;
                }

                using (var fs = File.OpenRead(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(SupplierData));
                    var data = ser.ReadObject(fs) as SupplierData;
                    SetSuppliers(data?.Suppliers ?? new List<string>());
                }
            }
            catch
            {
                SetSuppliers(Array.Empty<string>());
            }
        }

        public void Save()
        {
            try
            {
                var path = GetStorePath();
                using (var fs = File.Create(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(SupplierData));
                    ser.WriteObject(fs, new SupplierData { Suppliers = new List<string>(_suppliers) });
                }
            }
            catch
            {
            }
        }
    }
}
