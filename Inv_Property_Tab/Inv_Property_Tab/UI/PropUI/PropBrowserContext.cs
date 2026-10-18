using System.Runtime.InteropServices;
using Inventor;

namespace Inv_Property_Tab.UI.PropUI
{
    [ComVisible(true)]
    [Guid("5D0E3E57-2C77-4B6B-8ED7-3B840F7DAD36")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class PropBrowserContext
    {
        public Inventor.Application App { get; }
        public Document Document { get; }

        public PropBrowserContext(Inventor.Application app, Document document)
        {
            App = app;
            Document = document;
        }
    }
}
