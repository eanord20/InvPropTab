using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Inv_Property_Tab.UI.PropUI
{
    // This class is instantiated by Inventor inside BrowserPane (Document.BrowserPanes.Add)
    // via its ProgId.
    [ComVisible(true)]
    [Guid("A9F9E447-5A11-4A0C-9B8E-5A6C8B2F2D34")]
    [ProgId("InvPropTab.PropBrowser")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class PropBrowserActiveX : UserControl
    {
        private readonly PropBrowserControl? _control;

        public PropBrowserActiveX()
        {
            try
            {
                Dock = DockStyle.Fill;
                _control = new PropBrowserControl { Dock = DockStyle.Fill };
                Controls.Add(_control);
            }
            catch
            {
            }
        }

        public void SetContext(PropBrowserContext context)
        {
            _control?.SetContext(context);
        }

        public void RefreshFromDocument()
        {
            _control?.ReloadFromDocument();
        }

        [ComRegisterFunction]
        public static void RegisterClass(string key)
        {
            try
            {
                // Mark as ActiveX control.
                using (var k = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(key + "\\Control", true) ??
                               Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(key + "\\Control"))
                {
                    k?.Close();
                }

                using (var k = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(key + "\\MiscStatus", true) ??
                               Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(key + "\\MiscStatus"))
                {
                    k?.SetValue("", "131457");
                    k?.Close();
                }
            }
            catch
            {
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterClass(string key)
        {
            try
            {
                Microsoft.Win32.Registry.ClassesRoot.DeleteSubKeyTree(key + "\\Control", false);
                Microsoft.Win32.Registry.ClassesRoot.DeleteSubKeyTree(key + "\\MiscStatus", false);
            }
            catch
            {
            }
        }
    }
}
