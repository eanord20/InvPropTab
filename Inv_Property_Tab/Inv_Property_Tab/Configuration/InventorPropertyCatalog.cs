using System;
using System.Collections.Generic;
using System.Linq;

namespace Inv_Property_Tab.Configuration
{
    public static class InventorPropertyCatalog
    {
        public const string DesignTrackingSetId = "{32853F0F-3444-11D1-9E93-0060B03C1CA6}";
        public const string SummarySetId = "{F29F85E0-4FF9-1068-AB91-08002B27B3D9}";
        public const string DocumentSummarySetId = "{D5CDD502-2E9C-101B-9397-08002B2CF9AE}";

        private static readonly List<FieldDefinition> Definitions = new List<FieldDefinition>
        {
            Standard("partNumber", "Обозначение", "Part Number", 10),
            Standard("description", "Наименование", "Description", 20, FieldEditors.Text, false),
            Standard("category", "Категория", "Category", 30, FieldEditors.List, true),
            Standard("designer", "Разработал", "Designer", 40, FieldEditors.List, true),
            Standard("project", "Проект", "Project", 50, FieldEditors.List, true),
            Standard("supplier", "Поставщик", "Vendor", 60, FieldEditors.List, true),
            Standard("material", "Материал", "Material", 70, FieldEditors.List, true),
            Standard("stockNumber", "Инвентарный номер", "Stock Number", 80),
            Standard("revisionNumber", "Редакция", "Revision Number", 85),
            Standard("cost", "Сметная стоимость", "Cost", 90, FieldEditors.Number),
            Standard("status", "Статус", "Design Status", 100, FieldEditors.List, true),
            Standard("engineer", "Проверил", "Engineer", 110, FieldEditors.List, true),
            Standard("authority", "Утвердил", "Authority", 120, FieldEditors.List, true),
            Standard("manager", "Нач. отдела", "Manager", 130, FieldEditors.List, true),
            Summary("keywords", "Ключевые слова", "Keywords", 170),
            Summary("comments", "Пояснения", "Comments", 180, FieldEditors.Multiline),
            DocumentSummary("organization", "Организация", "Company", 200),
            DocumentSummary("managerName", "Нормоконтроль", "Manager", 210, FieldEditors.List, true),
            System("mass", "Масса", 240)
        };

        public static IReadOnlyList<FieldDefinition> All => Definitions.Select(Clone).ToList();

        public static FieldDefinition? Find(string id)
        {
            var definition = Definitions.FirstOrDefault(field => string.Equals(field.Id, id, StringComparison.OrdinalIgnoreCase));
            return definition == null ? null : Clone(definition);
        }

        private static FieldDefinition Standard(string id, string label, string propertyName, int order, string editor = FieldEditors.Text, bool rememberValues = false)
        {
            return Property(id, label, DesignTrackingSetId, propertyName, order, editor, rememberValues);
        }

        private static FieldDefinition Summary(string id, string label, string propertyName, int order, string editor = FieldEditors.Text)
        {
            return Property(id, label, SummarySetId, propertyName, order, editor, false);
        }

        private static FieldDefinition DocumentSummary(string id, string label, string propertyName, int order, string editor = FieldEditors.Text, bool rememberValues = false)
        {
            return Property(id, label, DocumentSummarySetId, propertyName, order, editor, rememberValues);
        }

        private static FieldDefinition Property(string id, string label, string propertySetId, string propertyName, int order, string editor, bool rememberValues)
        {
            return new FieldDefinition
            {
                Id = id,
                Label = label,
                Source = FieldSources.Standard,
                PropertySetId = propertySetId,
                PropertyName = propertyName,
                Editor = editor,
                Visible = order <= 60,
                Order = order,
                RememberValues = rememberValues
            };
        }

        private static FieldDefinition System(string id, string label, int order)
        {
            return new FieldDefinition
            {
                Id = id,
                Label = label,
                Source = FieldSources.System,
                Editor = FieldEditors.ReadOnly,
                Visible = id == "mass",
                Order = order
            };
        }

        private static FieldDefinition Clone(FieldDefinition source)
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
                Values = new List<string>(source.Values),
                RememberValues = source.Id.Equals("description", StringComparison.OrdinalIgnoreCase) ? false : source.RememberValues
            };
        }
    }
}
