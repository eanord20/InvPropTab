using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Inv_Property_Tab.Configuration
{
    [DataContract]
    public sealed class FieldDefinition
    {
        [DataMember(Name = "id", Order = 1)] public string Id { get; set; } = string.Empty;
        [DataMember(Name = "label", Order = 2)] public string Label { get; set; } = string.Empty;
        [DataMember(Name = "source", Order = 3)] public string Source { get; set; } = FieldSources.Standard;
        [DataMember(Name = "propertySetId", Order = 4, EmitDefaultValue = false)] public string? PropertySetId { get; set; }
        [DataMember(Name = "propertyName", Order = 5, EmitDefaultValue = false)] public string? PropertyName { get; set; }
        [DataMember(Name = "editor", Order = 6)] public string Editor { get; set; } = FieldEditors.Text;
        [DataMember(Name = "visible", Order = 7)] public bool Visible { get; set; } = true;
        [DataMember(Name = "order", Order = 8)] public int Order { get; set; }
        [DataMember(Name = "values", Order = 9, EmitDefaultValue = false)] public List<string> Values { get; set; } = new List<string>();
        [DataMember(Name = "rememberValues", Order = 10)] public bool RememberValues { get; set; }
    }

    public static class FieldSources
    {
        public const string Standard = "standard";
        public const string Custom = "custom";
        public const string System = "system";
    }

    public static class FieldEditors
    {
        public const string Text = "text";
        public const string Multiline = "multiline";
        public const string List = "list";
        public const string Number = "number";
        public const string ReadOnly = "readonly";
    }
}
