using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Inv_Property_Tab.Configuration
{
    [DataContract]
    public sealed class FieldsConfiguration
    {
        [DataMember(Name = "version", Order = 1)] public int Version { get; set; } = 1;
        [DataMember(Name = "fields", Order = 2)] public List<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();
    }
}
