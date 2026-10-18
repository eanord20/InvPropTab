using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;

namespace Inv_Property_Tab.Configuration
{
    public sealed class FieldsConfigurationStore
    {
        private const string FileName = "fields.json";
        private const string DefaultResourceName = "Inv_Property_Tab.Configuration.default-fields.json";
        private readonly string _directory;
        private readonly FieldsConfigurationValidator _validator = new FieldsConfigurationValidator();

        public FieldsConfigurationStore()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InvPropTab"))
        {
        }

        public FieldsConfigurationStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Configuration directory is required.", nameof(directory));
            _directory = directory;
        }

        public FieldsConfiguration Load()
        {
            var path = Path.Combine(_directory, FileName);
            if (!File.Exists(path))
                return LoadDefault();

            try
            {
                using (var stream = File.OpenRead(path))
                    return NormalizeAndValidate(Deserialize(stream));
            }
            catch
            {
                return LoadDefault();
            }
        }

        public void Save(FieldsConfiguration configuration)
        {
            var normalized = NormalizeAndValidate(configuration);
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, FileName);
            var temporaryPath = path + ".tmp";

            try
            {
                using (var stream = File.Create(temporaryPath))
                    CreateSerializer().WriteObject(stream, normalized);

                using (var stream = File.OpenRead(temporaryPath))
                    NormalizeAndValidate(Deserialize(stream));

                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private FieldsConfiguration LoadDefault()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(DefaultResourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException("Default fields configuration resource is missing.");
                return NormalizeAndValidate(Deserialize(stream));
            }
        }

        private FieldsConfiguration NormalizeAndValidate(FieldsConfiguration configuration)
        {
            var errors = _validator.Validate(configuration);
            if (errors.Count > 0)
                throw new InvalidDataException(string.Join(Environment.NewLine, errors));

            configuration.Fields = configuration.Fields
                .OrderBy(field => field.Order)
                .ThenBy(field => field.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return configuration;
        }

        private static FieldsConfiguration Deserialize(Stream stream)
        {
            var configuration = CreateSerializer().ReadObject(stream) as FieldsConfiguration;
            if (configuration == null)
                throw new InvalidDataException("Fields configuration is empty.");
            return configuration;
        }

        private static DataContractJsonSerializer CreateSerializer()
        {
            return new DataContractJsonSerializer(typeof(FieldsConfiguration));
        }
    }
}
