using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Inv_Property_Tab.Diagnostics;

namespace Inv_Property_Tab.Services
{
    public class SupplierStore
    {
        private static readonly AppLogger Logger = new AppLogger();
        [DataContract]
        private class SupplierData
        {
            [DataMember] public List<string> Suppliers { get; set; } = new List<string>();
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, SupplierStore> Instances = new Dictionary<string, SupplierStore>(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> _suppliers = new List<string>();

        private readonly string _fileName;
        private DateTime _lastLoadTimeUtc = DateTime.MinValue;

        public static SupplierStore Supplier => ForFile("suppliers.json");
        public static SupplierStore Project => ForFile("projects.json");
        public static SupplierStore Designer => ForFile("designers.json");
        public static SupplierStore Category => ForFile("categories.json");

        public static SupplierStore ForFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name is required.", nameof(fileName));

            lock (Sync)
            {
                if (!Instances.TryGetValue(fileName, out var instance))
                {
                    instance = new SupplierStore(fileName);
                    Instances[fileName] = instance;
                }
                return instance;
            }
        }

        private SupplierStore(string fileName)
        {
            _fileName = fileName;
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

            Load();

            if (_suppliers.Any(s => string.Equals(s, v, StringComparison.OrdinalIgnoreCase)))
                return;

            _suppliers.Add(v);
            _suppliers.Sort(StringComparer.CurrentCultureIgnoreCase);
            Save();
        }

        public void Remove(string? value)
        {
            var v = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(v))
                return;

            Load();
            _suppliers.RemoveAll(s => string.Equals(s, v, StringComparison.OrdinalIgnoreCase));
            Save();
        }

        private string GetStorePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InvPropTab");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, _fileName);
        }

        public void Load()
        {
            var now = DateTime.UtcNow;
            if (now - _lastLoadTimeUtc < TimeSpan.FromSeconds(1))
                return;

            ReloadInternal();
        }

        public void Reload()
        {
            ReloadInternal();
        }

        private void ReloadInternal()
        {
            try
            {
                var path = GetStorePath();
                if (!File.Exists(path))
                {
                    if (_suppliers.Count > 0)
                        SetSuppliers(Array.Empty<string>());
                    _lastLoadTimeUtc = DateTime.UtcNow;
                    return;
                }

                using (var fs = File.OpenRead(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(SupplierData));
                    var data = ser.ReadObject(fs) as SupplierData;
                    var loaded = data?.Suppliers ?? new List<string>();
                    SetSuppliers(loaded);
                }
                _lastLoadTimeUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Logger.Error($"SupplierStore[{_fileName}] ReloadInternal", ex);
                SetSuppliers(Array.Empty<string>());
                _lastLoadTimeUtc = DateTime.UtcNow;
            }
        }

        public void Save()
        {
            try
            {
                var path = GetStorePath();
                var tmpPath = path + ".tmp";
                using (var fs = File.Create(tmpPath))
                {
                    var ser = new DataContractJsonSerializer(typeof(SupplierData));
                    ser.WriteObject(fs, new SupplierData { Suppliers = new List<string>(_suppliers) });
                }

                if (File.Exists(path))
                    File.Replace(tmpPath, path, null);
                else
                    File.Move(tmpPath, path);
            }
            catch
            {
            }
            finally
            {
                try
                {
                    var path = GetStorePath();
                    var tmpPath = path + ".tmp";
                    if (File.Exists(tmpPath))
                        File.Delete(tmpPath);
                }
                catch
                {
                }
            }
        }
    }
}
