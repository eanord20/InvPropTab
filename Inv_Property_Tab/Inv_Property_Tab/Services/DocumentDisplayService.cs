using System.IO;

namespace Inv_Property_Tab.Services
{
    public static class DocumentDisplayService
    {
        public static string GetFileName(string fullFileName, string displayName)
        {
            return string.IsNullOrWhiteSpace(fullFileName)
                ? displayName ?? string.Empty
                : Path.GetFileName(fullFileName);
        }
    }
}
