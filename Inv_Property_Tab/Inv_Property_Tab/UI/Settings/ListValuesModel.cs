using System;
using System.Collections.Generic;
using System.Linq;

namespace Inv_Property_Tab.UI.Settings
{
    public static class ListValuesModel
    {
        public static List<string> Normalize(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }
}
