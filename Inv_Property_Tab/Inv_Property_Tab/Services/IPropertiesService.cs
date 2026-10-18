using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Inventor;
using Inv_Property_Tab.Interop;

namespace Inv_Property_Tab.Services
{
    public class IPropertiesService
    {
        private static readonly string[] CustomPropertySetNameCandidates = new[]
        {
            "Inventor User Defined Properties",
            "User Defined Properties",
            "Custom",
            "Пользовательские",
            "Пользовательские свойства",
            "Пользовательские свойства Inventor",
        };

        private static readonly string[] DesignTrackingPropertySetNameCandidates = new[]
        {
            "Design Tracking Properties",
            "Сведения об отслеживании конструкции",
            "Сведения об отслеживании проекта",
        };

        private static readonly string[] SupplierPropertyNameCandidates = new[]
        {
            "Vendor",
            "Поставщик",
            "Supplier",
        };

        public sealed class UserParameterInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
        }

        public IReadOnlyList<UserParameterInfo> GetParametersForOther(Document doc)
        {
            var list = new List<UserParameterInfo>();
            try
            {
                Parameters? ps = null;

                if (doc is PartDocument pd)
                    ps = pd.ComponentDefinition.Parameters;
                else if (doc is AssemblyDocument ad)
                    ps = ad.ComponentDefinition.Parameters;

                if (ps == null)
                    return list;

                // Model parameters (e.g. sketch dimension parameters like d6)
                try
                {
                    var mp = ps.ModelParameters;
                    if (mp != null)
                    {
                        foreach (ModelParameter p in mp)
                            AddParameterInfo(list, p);
                    }
                }
                catch
                {
                }

                // User parameters
                try
                {
                    var up = ps.UserParameters;
                    if (up != null)
                    {
                        foreach (UserParameter p in up)
                            AddParameterInfo(list, p);
                    }
                }
                catch
                {
                }

                // Distinct by name
                return list
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
            }
            catch
            {
                return list;
            }
        }

        private static void AddParameterInfo(List<UserParameterInfo> list, object p)
        {
            try
            {
                var info = new UserParameterInfo();
                var t = p.GetType();

                try
                {
                    var nameProp = t.GetProperty("Name");
                    info.Name = Convert.ToString(nameProp?.GetValue(p, null)) ?? string.Empty;
                }
                catch
                {
                    info.Name = string.Empty;
                }

                try
                {
                    var exprProp = t.GetProperty("Expression");
                    info.Value = Convert.ToString(exprProp?.GetValue(p, null)) ?? string.Empty;
                }
                catch
                {
                    info.Value = string.Empty;
                }
                try
                {
                    var typeProp = t.GetProperty("ParameterType");
                    var typeVal = typeProp?.GetValue(p, null);
                    info.Type = typeVal?.ToString() ?? string.Empty;
                }
                catch
                {
                    info.Type = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(info.Name))
                    list.Add(info);
            }
            catch
            {
            }
        }

        public bool IsReadOnly(Document doc)
        {
            try
            {
                if (doc == null)
                    return true;

                try
                {
                    // Some interop versions expose a ReadOnly property; use reflection to be safe.
                    var roProp = doc.GetType().GetProperty("ReadOnly");
                    if (roProp != null)
                    {
                        var v = roProp.GetValue(doc, null);
                        if (v is bool b)
                            return b;
                    }
                }
                catch
                {
                }

                // Fallback: if it has a file path, check file attributes.
                string p = string.Empty;
                try { p = doc.FullFileName; } catch { }

                if (!string.IsNullOrWhiteSpace(p) && System.IO.File.Exists(p))
                {
                    var attr = System.IO.File.GetAttributes(p);
                    if ((attr & System.IO.FileAttributes.ReadOnly) == System.IO.FileAttributes.ReadOnly)
                        return true;
                }

                return false;
            }
            catch
            {
                return true;
            }
        }

        public string TryGetPropertyValue(Document doc, string propertyName)
        {
            PropertySets? sets = null;
            try
            {
                if (doc == null)
                    return string.Empty;
                sets = doc.PropertySets;
                for (var setIndex = 1; setIndex <= sets.Count; setIndex++)
                {
                    PropertySet? set = null;
                    try
                    {
                        set = sets[setIndex];
                        for (var propertyIndex = 1; propertyIndex <= set.Count; propertyIndex++)
                        {
                            Inventor.Property? property = null;
                            try
                            {
                                property = set[propertyIndex];
                                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                                    return property.Value?.ToString() ?? string.Empty;
                            }
                            finally
                            {
                                ComRelease.Final(property);
                            }
                        }
                    }
                    finally
                    {
                        ComRelease.Final(set);
                    }
                }
            }
            catch
            {
            }
            finally
            {
                ComRelease.Final(sets);
            }

            return string.Empty;
        }

        public string TryGetPropertyValue(Document doc, IEnumerable<string> propertySetNameCandidates, IEnumerable<string> propertyNameCandidates)
        {
            try
            {
                if (doc == null)
                    return string.Empty;

                foreach (var setName in propertySetNameCandidates)
                {
                    var ps = TryGetPropertySet(doc, setName);
                    if (ps == null)
                        continue;

                    foreach (var propName in propertyNameCandidates)
                    {
                        var p = TryGetProperty(ps, propName);
                        if (p == null)
                            continue;
                        return p.Value?.ToString() ?? string.Empty;
                    }
                }

                // fallback: search in all sets, but prefer the first candidate property name found
                foreach (var propName in propertyNameCandidates)
                {
                    var v = TryGetPropertyValue(doc, propName);
                    if (!string.IsNullOrWhiteSpace(v))
                        return v;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        public bool TrySetPropertyValue(Document doc, string propertyName, string value)
        {
            PropertySets? sets = null;
            try
            {
                if (doc == null || IsReadOnly(doc))
                    return false;
                sets = doc.PropertySets;
                for (var setIndex = 1; setIndex <= sets.Count; setIndex++)
                {
                    PropertySet? set = null;
                    try
                    {
                        set = sets[setIndex];
                        for (var propertyIndex = 1; propertyIndex <= set.Count; propertyIndex++)
                        {
                            Inventor.Property? property = null;
                            try
                            {
                                property = set[propertyIndex];
                                if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                property.Value = value ?? string.Empty;
                                return true;
                            }
                            finally
                            {
                                ComRelease.Final(property);
                            }
                        }
                    }
                    finally
                    {
                        ComRelease.Final(set);
                    }
                }
            }
            catch
            {
            }
            finally
            {
                ComRelease.Final(sets);
            }

            return false;
        }

        public bool TrySetPropertyValue(Document doc, IEnumerable<string> propertySetNameCandidates, IEnumerable<string> propertyNameCandidates, string value)
        {
            try
            {
                if (doc == null)
                    return false;
                if (IsReadOnly(doc))
                    return false;

                foreach (var setName in propertySetNameCandidates)
                {
                    var ps = TryGetPropertySet(doc, setName);
                    if (ps == null)
                        continue;

                    foreach (var propName in propertyNameCandidates)
                    {
                        var p = TryGetProperty(ps, propName);
                        if (p == null)
                            continue;

                        p.Value = value ?? string.Empty;
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        public string TryGetSupplier(Document doc)
        {
            return TryGetPropertyValue(doc, DesignTrackingPropertySetNameCandidates, SupplierPropertyNameCandidates);
        }

        public bool TrySetSupplier(Document doc, string value)
        {
            // Prefer Design Tracking Properties (matches Inventor standard Properties dialog)
            return TrySetPropertyValue(doc, DesignTrackingPropertySetNameCandidates, SupplierPropertyNameCandidates, value);
        }

        public string TryGetSupplierExpression(Document doc)
        {
            try
            {
                if (doc == null)
                    return string.Empty;

                foreach (var setName in DesignTrackingPropertySetNameCandidates)
                {
                    var ps = TryGetPropertySet(doc, setName);
                    if (ps == null)
                        continue;

                    foreach (var propName in SupplierPropertyNameCandidates)
                    {
                        var p = TryGetProperty(ps, propName);
                        if (p == null)
                            continue;

                        // Some Inventor properties expose Expression (string). Use reflection for compatibility.
                        try
                        {
                            var exprProp = p.GetType().GetProperty("Expression");
                            var expr = Convert.ToString(exprProp?.GetValue(p, null)) ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(expr))
                                return expr;
                        }
                        catch
                        {
                        }

                        return p.Value?.ToString() ?? string.Empty;
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        public bool TrySetSupplierExpression(Document doc, string expressionOrValue)
        {
            try
            {
                if (doc == null)
                    return false;
                if (IsReadOnly(doc))
                    return false;

                foreach (var setName in DesignTrackingPropertySetNameCandidates)
                {
                    var ps = TryGetPropertySet(doc, setName);
                    if (ps == null)
                        continue;

                    foreach (var propName in SupplierPropertyNameCandidates)
                    {
                        var p = TryGetProperty(ps, propName);
                        if (p == null)
                            continue;

                        // Prefer Expression when available (supports formulas/variables).
                        try
                        {
                            var exprProp = p.GetType().GetProperty("Expression");
                            if (exprProp != null && exprProp.CanWrite)
                            {
                                exprProp.SetValue(p, expressionOrValue ?? string.Empty, null);
                                return true;
                            }
                        }
                        catch
                        {
                        }

                        // Fallback to Value
                        try
                        {
                            p.Value = expressionOrValue ?? string.Empty;
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        public List<(string Name, string Value, bool CanWrite)> GetCustomIProperties(Document doc)
        {
            var result = new List<(string, string, bool)>();
            try
            {
                if (doc == null)
                    return result;

                PropertySet? custom = null;
                foreach (var n in CustomPropertySetNameCandidates)
                {
                    custom = TryGetPropertySet(doc, n);
                    if (custom != null)
                        break;
                }

                // Fallback: some localizations have different names; pick the first set that contains at least one non-empty property.
                if (custom == null)
                {
                    foreach (PropertySet ps in doc.PropertySets)
                    {
                        try
                        {
                            // Heuristic: user-defined set usually has small count and arbitrary names.
                            if (ps == null)
                                continue;

                            int nonEmpty = 0;
                            foreach (Inventor.Property p in ps)
                            {
                                var pn = p?.Name ?? string.Empty;
                                if (string.IsNullOrWhiteSpace(pn))
                                    continue;
                                if (pn.Equals("Part Number", StringComparison.OrdinalIgnoreCase) ||
                                    pn.Equals("Description", StringComparison.OrdinalIgnoreCase) ||
                                    pn.Equals("Designer", StringComparison.OrdinalIgnoreCase) ||
                                    pn.Equals("Project", StringComparison.OrdinalIgnoreCase) ||
                                    pn.Equals("Vendor", StringComparison.OrdinalIgnoreCase) ||
                                    pn.Equals("Поставщик", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                nonEmpty++;
                                if (nonEmpty >= 1)
                                    break;
                            }

                            if (nonEmpty >= 1)
                            {
                                custom = ps;
                                break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                if (custom == null)
                    return result;

                foreach (Inventor.Property p in custom)
                {
                    var n = p.Name ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(n))
                        continue;

                    var v = p.Value?.ToString() ?? string.Empty;
                    result.Add((n, v, !IsReadOnly(doc)));
                }
            }
            catch
            {
            }

            return result;
        }

        public bool TryAddOrUpdateCustomIProperty(Document doc, string name, string value)
        {
            try
            {
                if (doc == null)
                    return false;
                if (IsReadOnly(doc))
                    return false;

                PropertySet? custom = null;
                foreach (var n in CustomPropertySetNameCandidates)
                {
                    custom = TryGetPropertySet(doc, n);
                    if (custom != null)
                        break;
                }

                if (custom == null)
                    return false;

                foreach (Inventor.Property p in custom)
                {
                    if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        p.Value = value ?? string.Empty;
                        return true;
                    }
                }

                custom.Add(value ?? string.Empty, name);
                return true;
            }
            catch
            {
            }

            return false;
        }

        public bool TryDeleteCustomIProperty(Document doc, string name)
        {
            try
            {
                if (doc == null)
                    return false;
                if (IsReadOnly(doc))
                    return false;

                PropertySet? custom = null;
                foreach (var n in CustomPropertySetNameCandidates)
                {
                    custom = TryGetPropertySet(doc, n);
                    if (custom != null)
                        break;
                }

                if (custom == null)
                    return false;

                for (int i = custom.Count; i >= 1; i--)
                {
                    var p = custom[i];
                    if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        p.Delete();
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static PropertySet? TryGetPropertySet(Document doc, string propertySetName)
        {
            try
            {
                foreach (PropertySet ps in doc.PropertySets)
                {
                    if (ps != null && string.Equals(ps.Name, propertySetName, StringComparison.OrdinalIgnoreCase))
                        return ps;
                }
            }
            catch
            {
            }

            return null;
        }

        private static Inventor.Property? TryGetProperty(PropertySet ps, string propertyName)
        {
            try
            {
                foreach (Inventor.Property p in ps)
                {
                    if (p != null && string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                        return p;
                }
            }
            catch
            {
            }

            return null;
        }

        public string TryGetMassKg(Document doc)
        {
            try
            {
                if (doc is PartDocument pd)
                {
                    var mass = pd.ComponentDefinition.MassProperties.Mass;
                    return mass.ToString("0.###", CultureInfo.InvariantCulture);
                }

                if (doc is AssemblyDocument ad)
                {
                    var mass = ad.ComponentDefinition.MassProperties.Mass;
                    return mass.ToString("0.###", CultureInfo.InvariantCulture);
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        public (string X, string Y, string Z) TryGetBoundingBoxMm(Document doc)
        {
            try
            {
                if (!(doc is PartDocument) && !(doc is AssemblyDocument))
                    return (string.Empty, string.Empty, string.Empty);

                if (doc is PartDocument pd)
                {
                    return GetRangeBoxMm(pd.ComponentDefinition.RangeBox, pd.UnitsOfMeasure);
                }

                if (doc is AssemblyDocument ad)
                {
                    return GetRangeBoxMm(ad.ComponentDefinition.RangeBox, ad.UnitsOfMeasure);
                }
            }
            catch
            {
                return (string.Empty, string.Empty, string.Empty);
            }

            return (string.Empty, string.Empty, string.Empty);
        }

        public IReadOnlyList<UserParameterInfo> GetUserParameters(Document doc)
        {
            var list = new List<UserParameterInfo>();
            try
            {
                Parameters? ps = null;

                if (doc is PartDocument pd)
                    ps = pd.ComponentDefinition.Parameters;
                else if (doc is AssemblyDocument ad)
                    ps = ad.ComponentDefinition.Parameters;

                if (ps == null)
                    return list;

                var ups = ps.UserParameters;
                if (ups == null)
                    return list;

                foreach (UserParameter p in ups)
                {
                    var info = new UserParameterInfo();
                    try
                    {
                        info.Name = p.Name ?? string.Empty;

                        // Expression is user-friendly and includes units when applicable (e.g. "6 mm").
                        var expr = p.Expression;
                        info.Value = expr ?? string.Empty;

                        // Try to infer a readable type.
                        try
                        {
                            info.Type = p.ParameterType.ToString();
                        }
                        catch
                        {
                            info.Type = string.Empty;
                        }
                    }
                    catch
                    {
                    }

                    if (!string.IsNullOrWhiteSpace(info.Name))
                        list.Add(info);
                }

                return list;
            }
            catch
            {
                return list;
            }
        }

        private static (string X, string Y, string Z) GetRangeBoxMm(Box rb, UnitsOfMeasure uom)
        {
            var dx = Math.Abs(rb.MaxPoint.X - rb.MinPoint.X);
            var dy = Math.Abs(rb.MaxPoint.Y - rb.MinPoint.Y);
            var dz = Math.Abs(rb.MaxPoint.Z - rb.MinPoint.Z);

            var x = uom.ConvertUnits(dx, UnitsTypeEnum.kDatabaseLengthUnits, UnitsTypeEnum.kMillimeterLengthUnits);
            var y = uom.ConvertUnits(dy, UnitsTypeEnum.kDatabaseLengthUnits, UnitsTypeEnum.kMillimeterLengthUnits);
            var z = uom.ConvertUnits(dz, UnitsTypeEnum.kDatabaseLengthUnits, UnitsTypeEnum.kMillimeterLengthUnits);

            return (
                x.ToString("0.###", CultureInfo.InvariantCulture),
                y.ToString("0.###", CultureInfo.InvariantCulture),
                z.ToString("0.###", CultureInfo.InvariantCulture));
        }

        public bool IsEditable(Document doc) => doc != null && !IsReadOnly(doc);

        public static readonly string[] SummaryProperties =
        {
            "Location",
            "Size",
            "Created",
            "Saved"
        };

        public static readonly string[] ProjectProperties =
        {
            "Part Number",
            "Description",
            "Designer",
            "Project",
            "Supplier"
        };
    }
}
