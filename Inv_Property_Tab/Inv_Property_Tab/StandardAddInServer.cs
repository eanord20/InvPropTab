using System;
using System.Runtime.InteropServices;
using Inventor;
using Inv_Property_Tab.Diagnostics;
using Inv_Property_Tab.Interop;

namespace Inv_Property_Tab.AddIn
{
    [Guid("A199A936-8AAA-4DDC-8BED-A41DB48D894C")]
    [ComVisible(true)]
    public class AddInServer : ApplicationAddInServer
    {
        private static readonly AppLogger Logger = new AppLogger();
        private Inventor.Application? _invApp;
        private ApplicationEvents? _appEvents;
        private DocumentEvents? _activeDocEvents;

        private UI.PropBrowserPaneManager? _paneManager;

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            _invApp = addInSiteObject.Application;

            _paneManager = new UI.PropBrowserPaneManager();

            _appEvents = _invApp.ApplicationEvents;
            _appEvents.OnActivateDocument += AppEvents_OnActivateDocument;

            TryAttachActiveDocumentEvents(_invApp.ActiveDocument);
            _paneManager.EnsurePaneForDocument(_invApp, _invApp.ActiveDocument);
        }

        public void Deactivate()
        {
            try
            {
                if (_activeDocEvents != null)
                {
                    _activeDocEvents.OnSave -= ActiveDocEvents_OnSave;
                    ComRelease.Final(_activeDocEvents);
                    _activeDocEvents = null;
                }

                if (_appEvents != null)
                {
                    _appEvents.OnActivateDocument -= AppEvents_OnActivateDocument;
                    ComRelease.Final(_appEvents);
                    _appEvents = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Deactivate add-in", ex);
            }

            _paneManager?.Dispose();
            _paneManager = null;
            _invApp = null;
        }

        public void ExecuteCommand(int commandID)
        {
        }

        public object Automation => null!;

        private void AppEvents_OnActivateDocument(_Document DocumentObject, EventTimingEnum BeforeOrAfter, NameValueMap Context, out HandlingCodeEnum HandlingCode)
        {
            HandlingCode = HandlingCodeEnum.kEventNotHandled;
            if (BeforeOrAfter != EventTimingEnum.kAfter)
                return;

            try
            {
                var doc = (Document)DocumentObject;
                var app = _invApp;
                if (app == null)
                    return;
                TryAttachActiveDocumentEvents(doc);
                _paneManager?.EnsurePaneForDocument(app, doc);
            }
            catch (Exception ex)
            {
                Logger.Error("Activate document", ex);
            }
        }

        private void TryAttachActiveDocumentEvents(Document doc)
        {
            try
            {
                if (_activeDocEvents != null)
                {
                    _activeDocEvents.OnSave -= ActiveDocEvents_OnSave;
                    ComRelease.Final(_activeDocEvents);
                    _activeDocEvents = null;
                }

                if (doc == null)
                    return;

                _activeDocEvents = doc.DocumentEvents;
                _activeDocEvents.OnSave += ActiveDocEvents_OnSave;
            }
            catch (Exception ex)
            {
                Logger.Error("Attach document events", ex);
            }
        }

        private void ActiveDocEvents_OnSave(EventTimingEnum BeforeOrAfter, NameValueMap Context, out HandlingCodeEnum HandlingCode)
        {
            HandlingCode = HandlingCodeEnum.kEventNotHandled;
            if (BeforeOrAfter != EventTimingEnum.kAfter)
                return;

            try
            {
                _paneManager?.RefreshActivePane();
            }
            catch (Exception ex)
            {
                Logger.Error("Refresh saved document", ex);
            }
        }
    }
}
