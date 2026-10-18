using System.Collections.Generic;
using System.Linq;
using Inventor;

namespace Inv_Property_Tab.Export
{
    public class InventorAssemblyReader
    {
        public IReadOnlyList<Document> GetAllReferencedPartDocuments(AssemblyDocument asmDoc)
        {
            var dict = new Dictionary<string, Document>();

            var allDocs = asmDoc.AllReferencedDocuments;
            for (int i = 1; i <= allDocs.Count; i++)
            {
                var doc = allDocs[i];

                if (doc.DocumentType != DocumentTypeEnum.kPartDocumentObject)
                    continue;

                var key = (doc.FullFileName ?? doc.DisplayName).ToLowerInvariant();
                if (!dict.ContainsKey(key))
                    dict[key] = doc;
            }

            return dict.Values.ToList();
        }
    }
}
