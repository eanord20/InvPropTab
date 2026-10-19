using System;
using System.Collections.Generic;
using System.Linq;
using Inv_Property_Tab.Configuration;

namespace Inv_Property_Tab.UI.Settings
{
    public static class FieldSettingsMerger
    {
        public static readonly IReadOnlyCollection<string> RemovedCatalogIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "checkingDate", "creationDate", "webLink", "subject", "itemQuantity", "baseQuantity", "componentType", "representation",
            "structure", "stage", "counter", "volume", "fileName", "filePath"
        };

        public static FieldsConfiguration Merge(IReadOnlyList<FieldDefinition> catalog, FieldsConfiguration current)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (current == null)
                throw new ArgumentNullException(nameof(current));

            var currentById = current.Fields.ToDictionary(field => field.Id, StringComparer.OrdinalIgnoreCase);
            var result = new List<FieldDefinition>();
            foreach (var catalogField in catalog)
            {
                currentById.TryGetValue(catalogField.Id, out var saved);
                var merged = Clone(catalogField);
                if (saved != null)
                {
                    merged.Visible = saved.Visible;
                    merged.Order = saved.Order;
                    merged.Values = new List<string>(saved.Values ?? new List<string>());
                    merged.RememberValues = merged.Editor == FieldEditors.List &&
                        !merged.Id.Equals("description", StringComparison.OrdinalIgnoreCase) &&
                        saved.RememberValues;
                }
                result.Add(merged);
            }

            var catalogIds = new HashSet<string>(catalog.Select(field => field.Id), StringComparer.OrdinalIgnoreCase);
            result.AddRange(current.Fields.Where(field => !catalogIds.Contains(field.Id) && !RemovedCatalogIds.Contains(field.Id)).Select(Clone));
            return new FieldsConfiguration
            {
                Version = 1,
                Fields = result.OrderBy(field => field.Order).ThenBy(field => field.Id, StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        public static bool CanRemember(FieldDefinition field)
        {
            return field != null &&
                field.Source != FieldSources.System &&
                field.Editor != FieldEditors.ReadOnly;
        }

        public static void ApplyRememberMode(FieldDefinition field, bool enabled)
        {
            if (!CanRemember(field))
            {
                field.RememberValues = false;
                return;
            }
            field.RememberValues = enabled;
            if (enabled)
            {
                field.Editor = FieldEditors.List;
                return;
            }
            var catalogField = InventorPropertyCatalog.Find(field.Id);
            field.Editor = catalogField?.Editor ?? FieldEditors.Text;
        }

        public static FieldDefinition Clone(FieldDefinition source)
        {
            return new FieldDefinition
            {
                Id = source.Id,
                Label = source.Label,
                Source = source.Source,
                PropertySetId = source.PropertySetId,
                PropertyName = source.PropertyName,
                Editor = source.Editor,
                Visible = source.Visible,
                Order = source.Order,
                Values = new List<string>(source.Values ?? new List<string>()),
                RememberValues = source.RememberValues
            };
        }
    }
}
