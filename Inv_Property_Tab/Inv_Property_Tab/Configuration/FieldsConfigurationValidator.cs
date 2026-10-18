using System;
using System.Collections.Generic;
using System.Linq;

namespace Inv_Property_Tab.Configuration
{
    public sealed class FieldsConfigurationValidator
    {
        private static readonly HashSet<string> Sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FieldSources.Standard,
            FieldSources.Custom,
            FieldSources.System
        };

        private static readonly HashSet<string> Editors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FieldEditors.Text,
            FieldEditors.Multiline,
            FieldEditors.List,
            FieldEditors.Number,
            FieldEditors.ReadOnly
        };

        public IReadOnlyList<string> Validate(FieldsConfiguration configuration)
        {
            var errors = new List<string>();
            if (configuration == null)
            {
                errors.Add("configuration is required");
                return errors;
            }

            if (configuration.Version != 1)
                errors.Add("version must be 1");

            var fields = configuration.Fields ?? new List<FieldDefinition>();
            foreach (var group in fields.Where(field => field != null).GroupBy(field => field.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (group.Count() > 1)
                    errors.Add("duplicate field id: " + group.Key);
            }

            foreach (var field in fields)
            {
                if (field == null)
                {
                    errors.Add("field is required");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(field.Id))
                    errors.Add("field id is required");
                if (string.IsNullOrWhiteSpace(field.Label))
                    errors.Add("field label is required: " + field.Id);
                if (!Sources.Contains(field.Source ?? string.Empty))
                    errors.Add("unknown field source: " + field.Id);
                if (!Editors.Contains(field.Editor ?? string.Empty))
                    errors.Add("unknown field editor: " + field.Id);
                if (!string.Equals(field.Source, FieldSources.System, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(field.PropertyName))
                    errors.Add("propertyName is required: " + field.Id);
                if (!string.Equals(field.Editor, FieldEditors.List, StringComparison.OrdinalIgnoreCase) && field.Values != null && field.Values.Count > 0)
                    errors.Add("values require list editor: " + field.Id);
                if (field.RememberValues && !string.Equals(field.Editor, FieldEditors.List, StringComparison.OrdinalIgnoreCase))
                    errors.Add("rememberValues requires list editor: " + field.Id);
            }

            return errors;
        }
    }
}
