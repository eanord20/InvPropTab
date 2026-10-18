using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Inventor;
using Inv_Property_Tab.Diagnostics;
using Inv_Property_Tab.UI.PropUI;

namespace Inv_Property_Tab.UI
{
    public class PropBrowserPaneManager : IDisposable
    {
        private const string PaneDisplayName = "InvPropTab";
        private static readonly AppLogger Logger = new AppLogger();
        private static bool _shownControlError;

        private BrowserPane? _activePane;

        public PropBrowserPaneManager()
        {
        }

        public void EnsurePaneForDocument(Inventor.Application app, Document doc)
        {
            try
            {
                if (app == null || doc == null)
                    return;

                ReleaseActivePane();
                BrowserPane? pane = null;
                try
                {
                    // Search for existing pane by name.
                    foreach (BrowserPane p in doc.BrowserPanes)
                    {
                        if (string.Equals(p.Name, PaneDisplayName, StringComparison.OrdinalIgnoreCase))
                        {
                            pane = p;
                            break;
                        }
                    }
                }
                catch
                {
                }

                if (pane == null)
                {
                    // Create a new BrowserPane hosting our COM-visible WinForms control.
                    pane = doc.BrowserPanes.Add(PaneDisplayName, "InvPropTab.PropBrowser");
                }

                _activePane = pane;

                TrySetContext(app, doc, pane);

                try
                {
                    if (pane.Control == null)
                    {
                        Log("BrowserPane.Control is null. ActiveX control was not created. Check COM registration for ProgId 'InvPropTab.PropBrowser'.");
                        ShowControlErrorOnce();
                    }
                }
                catch (Exception ex)
                {
                    Log("Error accessing BrowserPane.Control: " + ex);
                }
            }
            catch
            {
            }
        }

        public void RefreshActivePane()
        {
            try
            {
                if (_activePane == null)
                    return;

                var ctrl = _activePane.Control;
                if (ctrl == null)
                    return;

                dynamic d = ctrl;
                d.RefreshFromDocument();
            }
            catch
            {
            }
        }

        private void TrySetContext(Inventor.Application app, Document doc, BrowserPane pane)
        {
            try
            {
                if (pane == null)
                    return;

                var ctrl = pane.Control;
                if (ctrl == null)
                {
                    Log("TrySetContext: pane.Control is null");
                    return;
                }

                var ctx = new PropBrowserContext(app, doc);

                dynamic d = ctrl;
                d.SetContext(ctx);
            }
            catch (Exception ex)
            {
                Log("TrySetContext failed: " + ex);
                ShowControlErrorOnce();
            }
        }

        private static void ShowControlErrorOnce()
        {
            try
            {
                if (_shownControlError)
                    return;
                _shownControlError = true;

                MessageBox.Show(
                    "InvPropTab: не удалось создать ActiveX-контрол в BrowserPane.\n\n" +
                    "Обычно причина: ProgId 'InvPropTab.PropBrowser' не зарегистрирован (COM) или регистрация была без прав и не попала в реестр.\n\n" +
                    "Решение: зарегистрируйте Inv_Property_Tab.dll через regasm (лучше per-user HKCU) или соберите проект из VS от администратора, затем перезапустите Inventor.\n\n" +
                    "Лог: %APPDATA%\\InvPropTab\\invproptab.log",
                    "InvPropTab",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch
            {
            }
        }

        private static void Log(string text)
        {
            Logger.Warning(text);
        }

        public void Dispose()
        {
            ReleaseActivePane();
        }

        private void ReleaseActivePane()
        {
            if (_activePane == null)
                return;

            try
            {
                if (Marshal.IsComObject(_activePane))
                    Marshal.FinalReleaseComObject(_activePane);
            }
            finally
            {
                _activePane = null;
            }
        }
    }
}
