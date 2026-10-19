using System;
using System.IO;
namespace Inv_Property_Tab.Services
{
    public static class DrawingDocumentService
    {
        public static string? FindRelatedDrawing(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
                return null;

            try
            {
                if (!File.Exists(modelPath))
                    return null;

                var directory = Path.GetDirectoryName(modelPath);
                var baseName = Path.GetFileNameWithoutExtension(modelPath);
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(baseName))
                    return null;

                var idwPath = Path.Combine(directory, baseName + ".idw");
                if (File.Exists(idwPath))
                    return idwPath;

                var dwgPath = Path.Combine(directory, baseName + ".dwg");
                if (File.Exists(dwgPath))
                    return dwgPath;
            }
            catch
            {
            }

            return null;
        }

        public static void OpenDrawing(Inventor.Application app, string drawingPath)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (string.IsNullOrWhiteSpace(drawingPath))
                throw new ArgumentException("Drawing path is required.", nameof(drawingPath));
            if (!File.Exists(drawingPath))
                throw new FileNotFoundException("Drawing file was not found.", drawingPath);

            app.Documents.Open(drawingPath);
        }
    }
}
