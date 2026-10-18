using Inventor;
using Inv_Property_Tab.Configuration;

namespace Inv_Property_Tab.Services
{
    public interface IPropertyAccessor
    {
        string Read(Document document, FieldDefinition field);
        PropertyWriteResult Write(Document document, FieldDefinition field, string value);
    }
}
