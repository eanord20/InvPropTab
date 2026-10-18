using System;
using System.Windows.Forms;
using Inventor;
using Inv_Property_Tab.Diagnostics;
using Inv_Property_Tab.Interop;
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
                BrowserPanes? panes = null;
                try
                {
                    panes = doc.BrowserPanes;
                    for (var index = 1; index <= panes.Count; index++)
                    {
                        BrowserPane? candidate = null;
                        try
                        {
                            candidate = panes[index];
                            if (!string.Equals(candidate.Name, PaneDisplayName, StringComparison.OrdinalIgnoreCase))
                                continue;
                            pane = candidate;
                            candidate = null;
                            break;
                        }
                        finally
                        {
                            ComRelease.Final(candidate);
                        }
                    }
                    if (pane == null)
                        pane = panes.Add(PaneDisplayName, "InvPropTab.PropBrowser");
                }
                finally
                {
                    ComRelease.Final(panes);
                }

                _activePane = pane;

                TrySetContext(app, doc, pane);

                object? control = null;
                try
                {
                    control = pane.Control;
                    if (control == null)
                    {
                        Log("BrowserPane.Control is null. ActiveX control was not created. Check COM registration for ProgId 'InvPropTab.PropBrowser'.");
                        ShowControlErrorOnce();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Access BrowserPane control", ex);
                }
                finally
                {
                    ComRelease.Final(control);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Ensure BrowserPane", ex);
            }
        }

        public void RefreshActivePane()
        {
            object? control = null;
            try
            {
                if (_activePane == null)
                    return;
                control = _activePane.Control;
                if (control == null)
                    return;
                dynamic dynamicControl = control;
                dynamicControl.RefreshFromDocument();
            }
            catch (Exception ex)
            {
                Logger.Error("Refresh BrowserPane", ex);
            }
            finally
            {
                ComRelease.Final(control);
            }
        }

        private void TrySetContext(Inventor.Application app, Document doc, BrowserPane pane)
        {
            object? control = null;
            try
            {
                control = pane.Control;
                if (control == null)
                {
                    Log("TrySetContext: pane.Control is null");
                    return;
                }
                var context = new PropBrowserContext(app, doc);
                dynamic dynamicControl = control;
                dynamicControl.SetContext(context);
            }
            catch (Exception ex)
            {
                Logger.Error("Set BrowserPane context", ex);
                ShowControlErrorOnce();
            }
            finally
            {
                ComRelease.Final(control);
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
                ComRelease.Final(_activePane);
            }
            finally
            {
                _activePane = null;
            }
        }
    }
}
