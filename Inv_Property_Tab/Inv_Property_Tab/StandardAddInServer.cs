using System;
using System.Runtime.InteropServices;
using Inventor;

namespace Inv_Property_Tab.AddIn
{
    [Guid("A199A936-8AAA-4DDC-8BED-A41DB48D894C")]
    [ComVisible(true)]
    public class AddInServer : ApplicationAddInServer
    {
        private Inventor.Application? _invApp;
        private ApplicationEvents? _appEvents;
        private DocumentEvents? _activeDocEvents;

        private Services.SupplierStore? _supplierStore;
        private UI.PropBrowserPaneManager? _paneManager;

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            _invApp = addInSiteObject.Application;

            _supplierStore = new Services.SupplierStore();
            _supplierStore.Load();

            _paneManager = new UI.PropBrowserPaneManager(_supplierStore);

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
                    Marshal.FinalReleaseComObject(_activeDocEvents);
                    _activeDocEvents = null;
                }

                if (_appEvents != null)
                {
                    _appEvents.OnActivateDocument -= AppEvents_OnActivateDocument;
                    Marshal.FinalReleaseComObject(_appEvents);
                    _appEvents = null;
                }
            }
            catch
            {
            }

            _paneManager = null;
            _supplierStore = null;
            _invApp = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void ExecuteCommand(int commandID)
        {
        }

        public object Automation => null;

        private void AppEvents_OnActivateDocument(_Document DocumentObject, EventTimingEnum BeforeOrAfter, NameValueMap Context, out HandlingCodeEnum HandlingCode)
        {
            HandlingCode = HandlingCodeEnum.kEventNotHandled;
            if (BeforeOrAfter != EventTimingEnum.kAfter)
                return;

            try
            {
                var doc = (Document)DocumentObject;
                TryAttachActiveDocumentEvents(doc);
                _paneManager?.EnsurePaneForDocument(_invApp, doc);
            }
            catch
            {
            }
        }

        private void TryAttachActiveDocumentEvents(Document doc)
        {
            try
            {
                if (_activeDocEvents != null)
                {
                    _activeDocEvents.OnSave -= ActiveDocEvents_OnSave;
                    Marshal.FinalReleaseComObject(_activeDocEvents);
                    _activeDocEvents = null;
                }

                if (doc == null)
                    return;

                _activeDocEvents = doc.DocumentEvents;
                _activeDocEvents.OnSave += ActiveDocEvents_OnSave;
            }
            catch
            {
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
            catch
            {
            }
        }
    }
}
