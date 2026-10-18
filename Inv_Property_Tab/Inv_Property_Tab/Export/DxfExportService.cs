using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Inventor;

namespace Inv_Property_Tab.Export
{
    public class PartExportRequest
    {
        public Document Document { get; }
        public string Orientation { get; }

        public PartExportRequest(Document document, string orientation)
        {
            Document = document;
            Orientation = string.IsNullOrWhiteSpace(orientation) ? "XZ" : orientation;
        }
    }

    public class DxfExportService
    {
        public ExportResult ExportToDxf(
            Inventor.Application invApp,
            IReadOnlyList<PartExportRequest> partDocuments,
            string outputFolder,
            bool includeFileName = true,
            bool includeDimensions = true,
            Func<string, bool>? confirmOverwrite = null)
        {
            Directory.CreateDirectory(outputFolder);

            var result = new ExportResult();

            foreach (var doc in partDocuments)
            {
                try
                {
                    if (!(doc.Document is PartDocument partDoc))
                    {
                        result.SkippedNotSheetMetal++;
                        continue;
                    }

                    var compDef = partDoc.ComponentDefinition;

                    var baseName = System.IO.Path.GetFileNameWithoutExtension(partDoc.FullFileName);
                    if (string.IsNullOrWhiteSpace(baseName))
                        baseName = partDoc.DisplayName;

                    var outPath = System.IO.Path.Combine(outputFolder, baseName + ".dxf");

                    try
                    {
                        if (confirmOverwrite != null && System.IO.File.Exists(outPath))
                        {
                            if (!confirmOverwrite(outPath))
                            {
                                result.SkippedExisting++;
                                continue;
                            }
                        }
                    }
                    catch
                    {
                    }

                    if (compDef is SheetMetalComponentDefinition smDef)
                    {
                        ExportSheetMetalFlatPatternToDxfWithText(partDoc, smDef, outPath, includeFileName, includeDimensions);
                        result.ExportedCount++;
                    }
                    else
                    {
                        ExportRegularPartTopViewToDxfWithText(invApp, partDoc, outPath, includeFileName, includeDimensions, doc.Orientation);
                        result.ExportedCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorsCount++;
                    result.Errors.Add(ex.ToString());
                }
            }

            return result;
        }

        private static void ExportSheetMetalFlatPatternToDxfWithText(
            PartDocument partDoc,
            SheetMetalComponentDefinition smDef,
            string outPath,
            bool includeFileName,
            bool includeDimensions)
        {
            FlatPattern flat;
            if (smDef.HasFlatPattern)
            {
                flat = smDef.FlatPattern;
            }
            else
            {
                smDef.Unfold();
                flat = smDef.FlatPattern;
            }

            var dataIo = flat.DataIO;

            string options =
                "FLAT PATTERN DXF?AcadVersion=R12" +
                "&OuterProfileLayer=IV_OUTER_PROFILE" +
                "&InnerProfileLayer=IV_INTERIOR_PROFILES" +
                "&BendUpLayer=IV_BEND" +
                "&BendDownLayer=IV_BEND_DOWN" +
                "&InvisibleLayers=IV_TANGENT;IV_ARC_CENTERS";

            dataIo.WriteDataToFile(options, outPath);

            // Добавляем текст в DXF (R12) через простую вставку сущностей TEXT.
            var rb = flat.RangeBox;
            var lengthDb = Math.Abs(rb.MaxPoint.X - rb.MinPoint.X);
            var heightDb = Math.Abs(rb.MaxPoint.Y - rb.MinPoint.Y);
            var thicknessDb = Convert.ToDouble(smDef.Thickness.Value);

            var uom = partDoc.UnitsOfMeasure;
            var lengthMm = ToMm(uom, lengthDb);
            var heightMm = ToMm(uom, heightDb);
            var thicknessMm = ToMm(uom, thicknessDb);

            var fileName = GetDocBaseName(partDoc);

            if (includeFileName || includeDimensions)
            {
                InjectTextIntoDxfR12(
                    outPath,
                    rb.MaxPoint.X + lengthDb * 0.05,
                    rb.MaxPoint.Y,
                    includeFileName ? fileName : string.Empty,
                    includeDimensions ? lengthMm : (double?)null,
                    includeDimensions ? heightMm : (double?)null,
                    includeDimensions ? thicknessMm : (double?)null);
            }
        }

        private static void ExportRegularPartTopViewToDxfWithText(
            Inventor.Application invApp,
            PartDocument partDoc,
            string outPath,
            bool includeFileName,
            bool includeDimensions,
            string orientation)
        {
            // Создаём временный Drawing и экспортируем DXF через DXF Translator.
            var template = invApp.FileManager.GetTemplateFile(DocumentTypeEnum.kDrawingDocumentObject);
            var drawingDoc = (DrawingDocument)invApp.Documents.Add(DocumentTypeEnum.kDrawingDocumentObject, template, true);

            try
            {
                var sheet = drawingDoc.Sheets[1];
                var tg = invApp.TransientGeometry;

                ClearSheetTemplateContent(sheet);

                // Размещаем вид по плоскости XZ (Front).
                var viewPos = tg.CreatePoint2d(25, 20);

                var viewOrientation = ViewOrientationTypeEnum.kFrontViewOrientation;
                try
                {
                    var o = (orientation ?? string.Empty).Trim().ToUpperInvariant();
                    if (o == "XY") viewOrientation = ViewOrientationTypeEnum.kTopViewOrientation;
                    else if (o == "YZ") viewOrientation = ViewOrientationTypeEnum.kRightViewOrientation;
                    else viewOrientation = ViewOrientationTypeEnum.kFrontViewOrientation;
                }
                catch
                {
                    viewOrientation = ViewOrientationTypeEnum.kFrontViewOrientation;
                }

                // Сигнатура AddBaseView отличается между версиями Interop.
                // Используем late-binding, чтобы не зависеть от точного набора параметров.
                dynamic drawingViews = sheet.DrawingViews;
                dynamic view = drawingViews.AddBaseView(
                    (dynamic)partDoc,
                    viewPos,
                    1.0,
                    viewOrientation,
                    DrawingViewStyleEnum.kHiddenLineRemovedDrawingViewStyle,
                    "FRONT");

                // Габариты по RangeBox модели (в см во внутренних единицах).
                var rb = partDoc.ComponentDefinition.RangeBox;
                var lengthDb = Math.Abs(rb.MaxPoint.X - rb.MinPoint.X);
                var heightDb = Math.Abs(rb.MaxPoint.Y - rb.MinPoint.Y);
                var thicknessDb = Math.Abs(rb.MaxPoint.Z - rb.MinPoint.Z);

                var uom = partDoc.UnitsOfMeasure;
                var lengthMm = ToMm(uom, lengthDb);
                var heightMm = ToMm(uom, heightDb);
                var thicknessMm = ToMm(uom, thicknessDb);

                // Текст рядом с видом (в координатах листа): справа сверху от вида.
                var x = view.Position.X + view.Width / 2.0 + 2.0;
                var y = view.Position.Y + view.Height / 2.0;
                var fileName = GetDocBaseName(partDoc);
                if (includeFileName || includeDimensions)
                {
                    var lines = new List<string>();
                    if (includeFileName && !string.IsNullOrWhiteSpace(fileName))
                        lines.Add(fileName);
                    if (includeDimensions)
                    {
                        lines.Add($"Length - {lengthMm:0.###} mm");
                        lines.Add($"Height - {heightMm:0.###} mm");
                        lines.Add($"Thickness - {thicknessMm:0.###} mm");
                    }

                    var text = string.Join("\n", lines);
                    sheet.DrawingNotes.GeneralNotes.AddFitted(tg.CreatePoint2d(x, y), text);
                }

                SaveDrawingAsDxf(invApp, drawingDoc, outPath);
            }
            finally
            {
                drawingDoc.Close(true);
            }
        }

        private static void ClearSheetTemplateContent(Sheet sheet)
        {
            try
            {
                try
                {
                    if (sheet.Border != null)
                        sheet.Border.Delete();
                }
                catch
                {
                }

                try
                {
                    if (sheet.TitleBlock != null)
                        sheet.TitleBlock.Delete();
                }
                catch
                {
                }

                try
                {
                    for (int i = sheet.DrawingViews.Count; i >= 1; i--)
                        sheet.DrawingViews[i].Delete();
                }
                catch
                {
                }

                // Эскизные обозначения / символы (часто лежат в отдельной коллекции, не в Sketches).
                TryDeleteCollectionItems(sheet, "SketchedSymbols");
                TryDeleteCollectionItems(sheet, "SketchSymbols");

                // Часто обозначения/символы лежат в DrawingNotes, а не на sheet напрямую.
                try
                {
                    var dn = sheet.DrawingNotes;
                    if (dn != null)
                    {
                        TryDeleteCollectionItems(dn, "SketchedSymbols");
                        TryDeleteCollectionItems(dn, "SketchSymbols");

                        // Если в шаблоне используются другие типы обозначений (leaders, symbols и т.д.),
                        // пробуем пройти по наиболее распространённым коллекциям.
                        var commonNoteCollections = new[]
                        {
                            "Symbols",
                            "LeaderNotes",
                            "HoleTables",
                            "PartsLists",
                            "Balloons",
                            "RevisionTables",
                            "TextBoxes"
                        };

                        foreach (var c in commonNoteCollections)
                            TryDeleteCollectionItems(dn, c);

                        // Удаляем конкретные элементы по имени (ГОСТ доп. графы)
                        var gostNames = new[]
                        {
                            "ГОСТ - Доп. графы 1",
                            "ГОСТ - Доп. графы 2",
                            "ГОСТ - Доп. графы 3"
                        };

                        var commonNameCollections = new[]
                        {
                            "SketchedSymbols",
                            "SketchSymbols",
                            "Symbols",
                            "TextBoxes",
                            "GeneralNotes"
                        };

                        foreach (var c in commonNameCollections)
                            TryDeleteItemsByName(dn, c, gostNames);
                    }
                }
                catch
                {
                }

                // На некоторых шаблонах эти элементы могут быть в листовых Sketches.
                try
                {
                    var gostNames = new[]
                    {
                        "ГОСТ - Доп. графы 1",
                        "ГОСТ - Доп. графы 2",
                        "ГОСТ - Доп. графы 3"
                    };

                    TryDeleteItemsByName(sheet, "Sketches", gostNames);
                    TryDeleteItemsByName(sheet, "SketchedSymbols", gostNames);
                    TryDeleteItemsByName(sheet, "SketchSymbols", gostNames);
                }
                catch
                {
                }

                try
                {
                    for (int i = sheet.Sketches.Count; i >= 1; i--)
                        sheet.Sketches[i].Delete();
                }
                catch
                {
                }

                try
                {
                    var notes = sheet.DrawingNotes?.GeneralNotes;
                    if (notes != null)
                    {
                        for (int i = notes.Count; i >= 1; i--)
                            notes[i].Delete();
                    }
                }
                catch
                {
                }
            }
            catch
            {
            }
        }

        private static void TryDeleteItemsByName(object owner, string propertyName, IReadOnlyList<string> names)
        {
            try
            {
                var collection = owner.GetType().InvokeMember(
                    propertyName,
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    owner,
                    null);
                if (collection == null)
                    return;

                dynamic coll = collection;
                int count;
                try
                {
                    count = (int)coll.Count;
                }
                catch
                {
                    return;
                }

                for (int i = count; i >= 1; i--)
                {
                    try
                    {
                        dynamic item = coll[i];
                        string itemName = "";
                        string defName = "";
                        try
                        {
                            var nameObj = item.GetType().InvokeMember(
                                "Name",
                                System.Reflection.BindingFlags.GetProperty,
                                null,
                                item,
                                null);
                            itemName = nameObj as string ?? "";
                        }
                        catch
                        {
                            itemName = "";
                        }

                        try
                        {
                            var defObj = item.GetType().InvokeMember(
                                "Definition",
                                System.Reflection.BindingFlags.GetProperty,
                                null,
                                item,
                                null);
                            if (defObj != null)
                            {
                                var defNameObj = defObj.GetType().InvokeMember(
                                    "Name",
                                    System.Reflection.BindingFlags.GetProperty,
                                    null,
                                    defObj,
                                    null);
                                defName = defNameObj as string ?? "";
                            }
                        }
                        catch
                        {
                            defName = "";
                        }

                        if (names.Any(n => string.Equals(n, itemName, StringComparison.OrdinalIgnoreCase)) ||
                            names.Any(n => string.Equals(n, defName, StringComparison.OrdinalIgnoreCase)))
                        {
                            try { item.Delete(); } catch { }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static void TryDeleteCollectionItems(object owner, string propertyName)
        {
            try
            {
                var collection = owner.GetType().InvokeMember(
                    propertyName,
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    owner,
                    null);
                if (collection == null)
                    return;

                dynamic coll = collection;
                int count = 0;
                try
                {
                    count = (int)coll.Count;
                }
                catch
                {
                    return;
                }

                for (int i = count; i >= 1; i--)
                {
                    try
                    {
                        dynamic item = coll[i];
                        item.Delete();
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static void SaveDrawingAsDxf(Inventor.Application invApp, DrawingDocument drawingDoc, string outPath)
        {
            // GUID DXF Translator AddIn.
            var dxfAddIn = (TranslatorAddIn)invApp.ApplicationAddIns.ItemById["{C24E3AC4-122E-11D5-8E91-0010B541CD80}"];

            var context = invApp.TransientObjects.CreateTranslationContext();
            context.Type = IOMechanismEnum.kFileBrowseIOMechanism;

            var options = invApp.TransientObjects.CreateNameValueMap();
            if (dxfAddIn.HasSaveCopyAsOptions[drawingDoc, context, options])
            {
                // оставляем дефолтные опции
            }

            var dataMedium = invApp.TransientObjects.CreateDataMedium();
            dataMedium.FileName = outPath;

            dxfAddIn.SaveCopyAs(drawingDoc, context, options, dataMedium);
        }

        private static void InjectTextIntoDxfR12(
            string dxfPath,
            double baseX,
            double baseY,
            string fileName,
            double? length,
            double? height,
            double? thickness)
        {
            var lines = System.IO.File.ReadAllLines(dxfPath).ToList();

            // Ищем конец секции ENTITIES: вставляем перед ENDSEC после SECTION/ENTITIES.
            int entitiesStart = -1;
            for (int i = 0; i < lines.Count - 1; i++)
            {
                if (lines[i].Trim() == "2" && lines[i + 1].Trim().Equals("ENTITIES", StringComparison.OrdinalIgnoreCase))
                {
                    entitiesStart = i;
                    break;
                }
            }

            if (entitiesStart < 0)
                return;

            int insertAt = -1;
            for (int i = entitiesStart; i < lines.Count - 1; i++)
            {
                if (lines[i].Trim() == "0" && lines[i + 1].Trim().Equals("ENDSEC", StringComparison.OrdinalIgnoreCase))
                {
                    insertAt = i;
                    break;
                }
            }

            if (insertAt < 0)
                return;

            var maxDim = Math.Max(length ?? 0.0, height ?? 0.0);
            if (maxDim <= 0.0)
                maxDim = 1.0;

            var textHeight = maxDim * 0.03;
            var dy = textHeight * 1.5;

            var textLinesList = new List<string>();
            if (!string.IsNullOrWhiteSpace(fileName))
                textLinesList.Add(fileName);

            if (length.HasValue && height.HasValue && thickness.HasValue)
            {
                textLinesList.Add($"Length - {length.Value:0.###} mm");
                textLinesList.Add($"Height - {height.Value:0.###} mm");
                textLinesList.Add($"Thickness - {thickness.Value:0.###} mm");
            }

            if (textLinesList.Count == 0)
                return;

            var textLines = textLinesList.ToArray();

            var toInsert = new List<string>();
            for (int idx = 0; idx < textLines.Length; idx++)
            {
                var y = baseY - idx * dy;
                toInsert.AddRange(new[]
                {
                    "0",
                    "TEXT",
                    "8",
                    "IV_DIM",
                    "10",
                    baseX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "20",
                    y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "30",
                    "0.0",
                    "40",
                    textHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "1",
                    textLines[idx],
                    "50",
                    "0.0"
                });
            }

            lines.InsertRange(insertAt, toInsert);
            System.IO.File.WriteAllLines(dxfPath, lines);
        }

        private static double ToMm(UnitsOfMeasure uom, double databaseLengthValue)
        {
            try
            {
                return uom.ConvertUnits(
                    databaseLengthValue,
                    UnitsTypeEnum.kDatabaseLengthUnits,
                    UnitsTypeEnum.kMillimeterLengthUnits);
            }
            catch
            {
                // Fallback: database units обычно сантиметры
                return databaseLengthValue * 10.0;
            }
        }

        private static string GetDocBaseName(object doc)
        {
            try
            {
                var fullFileNameObj = doc.GetType().InvokeMember(
                    "FullFileName",
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    doc,
                    null);

                var fullFileName = fullFileNameObj as string;
                var baseName = string.IsNullOrWhiteSpace(fullFileName)
                    ? string.Empty
                    : System.IO.Path.GetFileNameWithoutExtension(fullFileName);
                if (!string.IsNullOrWhiteSpace(baseName))
                    return baseName;
            }
            catch
            {
            }

            try
            {
                var displayNameObj = doc.GetType().InvokeMember(
                    "DisplayName",
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    doc,
                    null);
                var displayName = displayNameObj as string;
                if (!string.IsNullOrWhiteSpace(displayName))
                    return displayName;
            }
            catch
            {
            }

            return "";
        }
    }
}
