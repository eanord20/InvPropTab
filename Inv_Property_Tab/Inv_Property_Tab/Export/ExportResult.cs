using System.Collections.Generic;

namespace Inv_Property_Tab.Export
{
    public class ExportResult
    {
        public int ExportedCount { get; set; }
        public int SkippedNotSheetMetal { get; set; }
        public int SkippedExisting { get; set; }
        public int ErrorsCount { get; set; }

        public List<string> Errors { get; } = new List<string>();
    }
}
