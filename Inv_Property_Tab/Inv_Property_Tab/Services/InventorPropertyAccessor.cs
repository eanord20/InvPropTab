using System;
using Inventor;
using Inv_Property_Tab.Configuration;

namespace Inv_Property_Tab.Services
{
    public sealed class InventorPropertyAccessor : IPropertyAccessor
    {
        private readonly IPropertiesService _properties = new IPropertiesService();

        public string Read(Document document, FieldDefinition field)
        {
            if (document == null || field == null)
                return string.Empty;
            if (string.Equals(field.Id, "mass", StringComparison.OrdinalIgnoreCase))
                return _properties.TryGetMassKg(document);
            if (string.Equals(field.Id, "dimensions", StringComparison.OrdinalIgnoreCase))
            {
                var dimensions = _properties.TryGetBoundingBoxMm(document);
                return string.Join(" × ", new[] { dimensions.X, dimensions.Y, dimensions.Z });
            }
            if (string.IsNullOrWhiteSpace(field.PropertyName))
                return string.Empty;
            var propertyName = field.PropertyName!;
            return _properties.TryGetPropertyValue(document, propertyName);
        }

        public PropertyWriteResult Write(Document document, FieldDefinition field, string value)
        {
            if (document == null)
                return PropertyWriteResult.Failed("Document is unavailable.");
            if (field == null || string.IsNullOrWhiteSpace(field.PropertyName))
                return PropertyWriteResult.Failed("Property is not configured.");
            if (_properties.IsReadOnly(document))
                return PropertyWriteResult.Failed("Document is read-only.");

            var propertyName = field.PropertyName!;
            try
            {
                return _properties.TrySetPropertyValue(document, propertyName, value)
                    ? PropertyWriteResult.Succeeded()
                    : PropertyWriteResult.Failed("Property was not found or could not be written: " + propertyName);
            }
            catch (Exception ex)
            {
                return PropertyWriteResult.Failed(ex.Message);
            }
        }
    }
}
