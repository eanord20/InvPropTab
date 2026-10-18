using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Inventor;
using Inv_Property_Tab.Services;
using Inv_Property_Tab.UI.PropUI;

namespace Inv_Property_Tab.UI
{
    public class PropBrowserPaneManager
    {
        private const string PaneDisplayName = "InvPropTab";
        private static bool _shownControlError;

        private readonly SupplierStore _supplierStore;
        private readonly IPropertiesService _propertiesService;

        private BrowserPane? _activePane;

        // Keep per-document references because BrowserPanes are persisted with documents.
        private readonly Dictionary<string, BrowserPane> _paneByDocKey = new Dictionary<string, BrowserPane>(StringComparer.OrdinalIgnoreCase);

        public PropBrowserPaneManager(SupplierStore supplierStore)
        {
            _supplierStore = supplierStore;
            _propertiesService = new IPropertiesService();
        }

        public void EnsurePaneForDocument(Inventor.Application app, Document doc)
        {
            try
            {
                if (app == null || doc == null)
                    return;

                var key = GetDocKey(doc);
                if (!string.IsNullOrWhiteSpace(key) && _paneByDocKey.TryGetValue(key, out var cached))
                {
                    _activePane = cached;
                    TrySetContext(app, doc, cached);
                    return;
                }

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

                if (!string.IsNullOrWhiteSpace(key))
                    _paneByDocKey[key] = pane;

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
            try
            {
                var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "InvPropTab");
                Directory.CreateDirectory(dir);
                var path = System.IO.Path.Combine(dir, "invproptab.log");
                System.IO.File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + System.Environment.NewLine);
            }
            catch
            {
            }
        }

        private static string GetDocKey(Document doc)
        {
            try
            {
                var p = doc.FullFileName;
                if (!string.IsNullOrWhiteSpace(p))
                    return p;
            }
            catch
            {
            }

            try
            {
                return doc.DisplayName;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
