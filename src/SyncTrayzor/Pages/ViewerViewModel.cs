#nullable enable
using Stylet;
using SyncTrayzor.Syncthing;
using SyncTrayzor.Utils;
using System;
using System.Globalization;
using CefSharp;
using CefSharp.Wpf;
using SyncTrayzor.Services.Config;
using System.IO;
using System.Threading;
using System.Windows;
using SyncTrayzor.Services;
using SyncTrayzor.Properties;
using Microsoft.WindowsAPICodePack.Dialogs;
using CefSharp.Handler;
using SyncTrayzor.Localization;

namespace SyncTrayzor.Pages
{
    public class ViewerViewModel : Screen, IResourceRequestHandlerFactory, ILifeSpanHandler, IContextMenuHandler,
        IDisposable
    {
        private const int ErrorSharingViolation = unchecked((int)0x80070020);

        private readonly IWindowManager windowManager;
        private readonly ISyncthingManager syncthingManager;
        private readonly IProcessStartProvider processStartProvider;
        private readonly IConfigurationProvider configurationProvider;
        private readonly IApplicationPathsProvider pathsProvider;

        private readonly CustomResourceRequestHandler customResourceRequestHandler;

        private readonly object cultureLock = new(); // This can be read from many threads
        private readonly Lock _initLock = new();
        private CultureInfo? culture;
        private double zoomLevel;
        private string? brandLogoDataUri;

        public string? Location
        {
            get => WebBrowser?.Address;
            private set
            {
                if (WebBrowser != null)
                    WebBrowser.Address = value;
            }
        }

        private SyncthingState syncthingState { get; set; }
        public bool ShowSyncthingStarting => syncthingState == SyncthingState.Starting;
        public bool ShowSyncthingStopped => syncthingState == SyncthingState.Stopped;

        public ChromiumWebBrowser? WebBrowser { get; set; }

        private JavascriptCallbackObject callback;

        public ViewerViewModel(
            IWindowManager windowManager,
            ISyncthingManager syncthingManager,
            IConfigurationProvider configurationProvider,
            IProcessStartProvider processStartProvider,
            IApplicationPathsProvider pathsProvider)
        {
            this.windowManager = windowManager;
            this.syncthingManager = syncthingManager;
            this.processStartProvider = processStartProvider;
            this.configurationProvider = configurationProvider;
            this.pathsProvider = pathsProvider;

            var configuration = this.configurationProvider.Load();
            zoomLevel = configuration.SyncthingWebBrowserZoomLevel;

            this.syncthingManager.StateChanged += SyncthingStateChanged;

            customResourceRequestHandler = new CustomResourceRequestHandler(this);
            callback = new JavascriptCallbackObject(this);

            SetCulture(configuration);
            configurationProvider.ConfigurationChanged += ConfigurationChanged;
        }

        private void SyncthingStateChanged(object? sender, SyncthingStateChangedEventArgs e)
        {
            syncthingState = e.NewState;
            RefreshBrowser();
        }

        private void ConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            SetCulture(e.NewConfiguration);
        }

        internal void ApplyGuiLanguageFromWeb(string? locale)
        {
            var cultureName = UiLanguage.NormalizeGuiLocale(locale);
            if (cultureName == null)
                return;

            void Apply()
            {
                var current = UiLanguage.Instance.Culture?.Name;
                if (string.Equals(current, cultureName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(configurationProvider.Load().UiCulture, cultureName, StringComparison.OrdinalIgnoreCase))
                    return;

                UiLanguage.Instance.Apply(cultureName);
                configurationProvider.AtomicLoadAndSave(c =>
                {
                    c.UiCulture = cultureName;
                    // Explicit GUI choice must win over OS culture on next launch
                    c.UseComputerCulture = false;
                });
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.BeginInvoke(Apply);
            else
                Apply();
        }

        private void SetCulture(Configuration configuration)
        {
            lock (cultureLock)
            {
                culture = configuration.UseComputerCulture
                    ? CultureInfo.CurrentUICulture
                    : CultureInfo.GetCultureInfoByIetfLanguageTag("en-US");
            }
        }

        protected override void OnInitialActivate()
        {
            if (!(Cef.IsInitialized ?? false))
            {
                var configuration = configurationProvider.Load();

                string language;
                lock (cultureLock)
                {
                    language = culture!.Name;
                }

                lock (_initLock)
                {
                    // It may happen that we have another SyncTrazyor instance using the same CEF cache path
                    // This will cause a generic CEF initialization failure. Try to catch this early
                    // by acquiring the log file.
                    var cefCachePath = pathsProvider.CefCachePath;
                    var cefLock = cefCachePath + @"\lockfile";
                    try
                    {
                        using var lockFile = new FileStream(cefLock, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                            FileShare.None);
                    }
                    catch (IOException e) when (e.HResult == ErrorSharingViolation)
                    {
                        var msg = Localizer.Translate("ViewerView_CefCacheLocked", cefCachePath);
                        var title = Localizer.Translate("ViewerView_CefCacheLocked_Title");
                        windowManager.ShowMessageBox(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
                        Environment.Exit(1);
                    }
                    catch (Exception)
                    {
                        // Ignore potentially unrelated errors
                    }

                    var cefLogFile = pathsProvider.LogFilePath + @"\cef.log";
                    var settings = new CefSettings()
                    {
                        RemoteDebuggingPort = AppSettings.Instance.CefRemoteDebuggingPort,
                        // We really only want to set the LocalStorage path, but we don't have that level of control....
                        CachePath = cefCachePath,
                        LogFile = cefLogFile,
                        IgnoreCertificateErrors = true,
                        LogSeverity = LogSeverity.Warning,
                        AcceptLanguageList = language,
                        Locale = language
                    };

                    // CEF will attempt de-elevating the process when launched as admin. This is done by relaunching
                    // the current process in unprivileged mode. However, we're not equipped to handle such a
                    // process restart cleanly, so ask CEF to not relaunch. Running as admin is not ideal for security,
                    // but the user wants it...
                    settings.CefCommandLineArgs.Add("do-not-de-elevate");

                    // System proxy settings (which also specify a proxy for localhost) shouldn't affect us
                    settings.CefCommandLineArgs.Add("no-proxy-server", "1");
                    settings.CefCommandLineArgs.Add("disable-cache", "1");
                    settings.CefCommandLineArgs.Add("disable-extensions", "1");

                    if (configuration.DisableHardwareRendering)
                    {
                        settings.CefCommandLineArgs.Add("disable-gpu");
                        settings.CefCommandLineArgs.Add("disable-gpu-vsync");
                        try
                        {
                            settings.CefCommandLineArgs.Add("disable-gpu-compositing");
                        }
                        catch (ArgumentException)
                        {
                            // CefSharp 139+ may set this by default, ignore if unable to set the flag.
                        }

                        settings.CefCommandLineArgs.Add("disable-application-cache");
                    }

                    try
                    {
                        if (!Cef.Initialize(settings))
                        {
                            throw new InvalidOperationException("Unable to initialize CEF.");
                        }
                    }
                    catch (InvalidOperationException e)
                    {
                        throw new AggregateException(
                            $"SyncTrayzor was unable to initialize its embedded browser.\nWhen reporting this issue, please attach the CEF logfile @ \n{cefLogFile}\n in addition to this message and the normal SyncTrayzor logfile.",
                            e);
                    }
                }
            }

            var webBrowser = new ChromiumWebBrowser();
            InitializeBrowser(webBrowser);
            WebBrowser = webBrowser;
            RefreshBrowser();
        }

        private void InitializeBrowser(ChromiumWebBrowser webBrowser)
        {
            webBrowser.RequestHandler = new CustomRequestHandler();
            webBrowser.ResourceRequestHandlerFactory = this;
            webBrowser.LifeSpanHandler = this;
            webBrowser.MenuHandler = this;
            webBrowser.JavascriptObjectRepository.Settings.LegacyBindingEnabled = true;
            webBrowser.JavascriptObjectRepository.Register("callbackObject", callback);

            // So. Fun story. From https://github.com/cefsharp/CefSharp/issues/738#issuecomment-91099199, we need to set the zoom level
            // in the FrameLoadStart event. However, the IWpfWebBrowser's ZoomLevel is a DependencyProperty, and it wraps
            // the SetZoomLevel method on the unmanaged browser (which is exposed directly by ChromiumWebBrowser, but not by IWpfWebBrowser).
            // Now, FrameLoadState and FrameLoadEnd are called on a background thread, and since ZoomLevel is a DP, it can only be changed
            // from the UI thread (it's "helpful" and does a dispatcher check for us). But, if we dispatch back to the UI thread to call
            // ZoomLevel = xxx, then CEF seems to hit threading issues, and can sometimes render things entirely badly (massive icons, no
            // localization, bad spacing, no JavaScript at all, etc).
            // So, in this case, we need to call SetZoomLevel directly, as we can do that from the thread on which FrameLoadStart is called,
            // and everything's happy.
            // However, this means that the DP value isn't updated... Which means we can't use the DP at all. We have to call SetZoomLevel
            // *everywhere*, and that means keeping a local field zoomLevel to track the current zoom level. Such is life

            webBrowser.FrameLoadStart += (o, e) => webBrowser.SetZoomLevel(zoomLevel);
            webBrowser.FrameLoadEnd += (o, e) =>
            {
                if (e.Frame.IsMain && e.Url != "about:blank")
                {
                    // Prefer frame API so theme CSS runs reliably on the Syncthing document
                    void RunJs(string script) => e.Frame.ExecuteJavaScriptAsync(script);

                    // I tried to do this using Syncthing's events, but it's very painful - the DOM is updated some time
                    // after the event is fired. It's a lot easier to just watch for changes on the DOM.
                    var addOpenFolderButton =
                        @"var syncTrayzorAddOpenFolderButton = function(elem) {" +
                        @"    var $buttonContainer = elem.find('.panel-footer .pull-right');" +
                        @"    $buttonContainer.find('.panel-footer .synctrayzor-add-folder-button').remove();" +
                        @"    $buttonContainer.prepend(" +
                        @"      '<button class=""btn btn-sm btn-default synctrayzor-add-folder-button"" onclick=""callbackObject.openFolder(angular.element(this).scope().folder.id)"">" +
                        @"          <span class=""fa fa-folder-open""></span>" +
                        @"          <span style=""margin-left: 3px"">" + Resources.ViewerView_OpenFolder + @"</span>" +
                        @"      </button>');" +
                        @"};" +
                        @"new MutationObserver(function(mutations, observer) {" +
                        @"  for (var i = 0; i < mutations.length; i++) {" +
                        @"    for (var j = 0; j < mutations[i].addedNodes.length; j++) {" +
                        @"      syncTrayzorAddOpenFolderButton($(mutations[i].addedNodes[j]));" +
                        @"    }" +
                        @"  }" +
                        @"}).observe(document.getElementById('folders'), {" +
                        @"  childList: true" +
                        @"});" +
                        @"syncTrayzorAddOpenFolderButton($('#folders'));" +
                        @"";
                    RunJs(addOpenFolderButton);

                    var addFolderBrowse =
                        @"$('#folderPath').wrap($('<div/>').css('display', 'flex'));" +
                        @"$('#folderPath').after(" +
                        @"  $('<button>').attr('id', 'folderPathBrowseButton')" +
                        @"               .addClass('btn btn-sm btn-default')" +
                        @"               .html('" + Resources.ViewerView_BrowseToFolder + @"')" +
                        @"               .css({'flex-grow': 1, 'margin': '0 0 0 5px'})" +
                        @"               .on('click', function() { callbackObject.browseFolderPath() })" +
                        @");" +
                        @"$('#folderPath').removeAttr('list');" +
                        @"$('#directory-list').remove();" +
                        @"$('#editFolder').on('shown.bs.modal', function() {" +
                        @"  if ($('#folderPath').is('[readonly]')) {" +
                        @"      $('#folderPathBrowseButton').attr('disabled', 'disabled');" +
                        @"  }" +
                        @"  else {" +
                        @"      $('#folderPathBrowseButton').removeAttr('disabled');" +
                        @"  }" +
                        @"});";
                    RunJs(addFolderBrowse);

                    // Theme pink + device-ID click-to-copy (no text-selection blue highlight)
                    RunJs(
                        @"(function(){" +
                        @"  var clearSel = function(){" +
                        @"    var s = window.getSelection && window.getSelection();" +
                        @"    if (s && s.removeAllRanges) s.removeAllRanges();" +
                        @"  };" +
                        @"  var fallbackCopy = function(text){" +
                        @"    var ta = document.createElement('textarea');" +
                        @"    ta.value = text; ta.style.position='fixed'; ta.style.left='-9999px';" +
                        @"    document.body.appendChild(ta); ta.select();" +
                        @"    try { document.execCommand('copy'); } catch(e) {}" +
                        @"    document.body.removeChild(ta); clearSel();" +
                        @"  };" +
                        @"  var copyText = function(text){" +
                        @"    if (!text) return;" +
                        @"    if (navigator.clipboard && navigator.clipboard.writeText) {" +
                        @"      navigator.clipboard.writeText(text).then(clearSel).catch(function(){ fallbackCopy(text); });" +
                        @"    } else { fallbackCopy(text); }" +
                        @"  };" +
                        @"  var showCopiedToast = function(anchor){" +
                        @"    var t = document.getElementById('irongoo-copied-toast');" +
                        @"    if (!t) {" +
                        @"      t = document.createElement('div'); t.id = 'irongoo-copied-toast';" +
                        @"      t.textContent = '已复制';" +
                        @"      (document.body||document.documentElement).appendChild(t);" +
                        @"    }" +
                        @"    var el = anchor || document.querySelector('#idqr .well');" +
                        @"    if (el) {" +
                        @"      var r = el.getBoundingClientRect();" +
                        @"      t.style.left = (r.left + r.width / 2) + 'px';" +
                        @"      t.style.top = (r.bottom + 8) + 'px';" +
                        @"      t.style.bottom = 'auto';" +
                        @"    }" +
                        @"    t.classList.add('show');" +
                        @"    clearTimeout(t._hideTimer);" +
                        @"    t._hideTimer = setTimeout(function(){ t.classList.remove('show'); }, 1400);" +
                        @"  };" +
                        @"  var wireDeviceId = function(el){" +
                        @"    if (!el || el.getAttribute('data-irongoo-copy') === '1') return;" +
                        @"    el.setAttribute('data-irongoo-copy','1');" +
                        @"    el.classList.remove('select-on-click');" +
                        @"    el.setAttribute('title','点击复制');" +
                        @"    el.addEventListener('mousedown', function(ev){ ev.preventDefault(); clearSel(); }, true);" +
                        @"    el.addEventListener('click', function(ev){" +
                        @"      ev.preventDefault(); ev.stopPropagation();" +
                        @"      if (ev.stopImmediatePropagation) ev.stopImmediatePropagation();" +
                        @"      clearSel();" +
                        @"      var text = (el.textContent || '').replace(/\\s+/g,'').trim();" +
                        @"      copyText(text);" +
                        @"      el.classList.add('irongoo-copied');" +
                        @"      showCopiedToast(el);" +
                        @"      setTimeout(function(){ el.classList.remove('irongoo-copied'); clearSel(); }, 800);" +
                        @"    }, true);" +
                        @"  };" +
                        @"  var isAllowedLang = function(code, name){" +
                        @"    code = (code || '').toLowerCase();" +
                        @"    name = (name || '').trim();" +
                        @"    if (code === 'en' || code === 'zh' || code === 'zh-cn' || code === 'zh-hans') return true;" +
                        @"    if (name === 'English') return true;" +
                        @"    if (name.indexOf('Chinese (Simplified') === 0 || name.indexOf('简体') >= 0) return true;" +
                        @"    return false;" +
                        @"  };" +
                        @"  var isZhName = function(name){" +
                        @"    name = (name || '').trim();" +
                        @"    return name.indexOf('Chinese (Simplified') === 0 || name.indexOf('简体') >= 0;" +
                        @"  };" +
                        @"  var busy = false;" +
                        @"  var filterLanguages = function(){" +
                        @"    var roots = document.querySelectorAll('[language-select]');" +
                        @"    for (var r = 0; r < roots.length; r++) {" +
                        @"      var root = roots[r];" +
                        @"      var items = root.querySelectorAll('.dropdown-menu > li');" +
                        @"      for (var i = 0; i < items.length; i++) {" +
                        @"        var li = items[i];" +
                        @"        var a = li.querySelector('a');" +
                        @"        var name = a ? (a.textContent || '').trim() : '';" +
                        @"        var code = '';" +
                        @"        try {" +
                        @"          var s = window.angular && angular.element(li).scope();" +
                        @"          if (s && s.name && s.localesNamesInv) code = s.localesNamesInv[s.name] || '';" +
                        @"        } catch (e) {}" +
                        @"        var keep = isAllowedLang(code, name);" +
                        @"        var want = keep ? '' : 'none';" +
                        @"        if (li.style.display !== want) li.style.display = want;" +
                        @"        if (keep && a && isZhName(name) && a.textContent.trim() !== '简体中文') a.textContent = '简体中文';" +
                        @"      }" +
                        @"      var curCode = '';" +
                        @"      var rs = null;" +
                        @"      try {" +
                        @"        rs = angular.element(root).isolateScope() || angular.element(root).scope();" +
                        @"        if (rs && rs.currentLocale) curCode = rs.currentLocale;" +
                        @"      } catch (e) {}" +
                        @"      if (!curCode) { try { curCode = localStorage.getItem('SYN_LANG') || ''; } catch (e) {} }" +
                        @"      curCode = (curCode || '').toLowerCase();" +
                        @"      var langWant = (curCode.indexOf('zh') === 0) ? '简体中文' : 'English';" +
                        @"      try {" +
                        @"        if (rs && rs.displayCurrent !== langWant) {" +
                        @"          rs.displayCurrent = langWant;" +
                        @"          if (rs.$applyAsync) rs.$applyAsync();" +
                        @"        }" +
                        @"      } catch (e) {}" +
                        @"      var label = root.querySelector('.dropdown-toggle .hidden-xs');" +
                        @"      if (label && label.textContent.indexOf(langWant) < 0) label.innerHTML = '&nbsp;' + langWant;" +
                        @"    }" +
                        @"  };" +
                        @"  var isModalVisible = function(){" +
                        @"    var modals = document.querySelectorAll('.modal');" +
                        @"    for (var i = 0; i < modals.length; i++) {" +
                        @"      var m = modals[i];" +
                        @"      if (m.classList.contains('in') || m.classList.contains('show')) return true;" +
                        @"      if (m.style.display === 'block') return true;" +
                        @"      if (m.getAttribute('aria-hidden') === 'false') return true;" +
                        @"    }" +
                        @"    return false;" +
                        @"  };" +
                        @"  var clearStuckOverlay = function(){" +
                        /* Backdrop is added before .in — never strip during open. Only clear leftover backdrop. */
                        @"    if (isModalVisible()) return;" +
                        @"    var backs = document.querySelectorAll('.modal-backdrop');" +
                        @"    if (!backs.length) return;" +
                        @"    for (var b = 0; b < backs.length; b++) backs[b].parentNode.removeChild(backs[b]);" +
                        @"    if (document.body) {" +
                        @"      document.body.classList.remove('modal-open');" +
                        @"      document.body.style.removeProperty('padding-right');" +
                        @"      document.body.style.removeProperty('overflow');" +
                        @"    }" +
                        @"  };" +
                        @"  var styleCss = '.modal-header.alert,.modal-header.alert-info,.alert-info,#idqr .modal-header,' +" +
                        @"'.modal-header.alert-success,.alert-success,.panel-success>.panel-heading,' +" +
                        @"'.btn-success,.btn-success:hover,.btn-success:focus,.btn-success:active,' +" +
                        @"'.btn-success:active:hover,.btn-success.active,.label-success,.bg-success,.progress-bar-success{' +" +
                        @"'background-color:#E73F91!important;background-image:none!important;' +" +
                        @"'border-color:#C9347A!important;color:#fff!important;}' +" +
                        @"'.text-success{color:#E73F91!important;}' +" +
                        @"'#ur,#ur.modal{display:none!important;visibility:hidden!important;}' +" +
                        @"'#irongoo-folder-header,#irongoo-remote-header{display:flex!important;align-items:center!important;' +" +
                        @"'justify-content:flex-start!important;flex-wrap:wrap!important;gap:8px 10px!important;' +" +
                        @"'margin:20px 0 10px!important;width:100%!important;}' +" +
                        @"'#irongoo-folder-header>h3,#irongoo-remote-header>h3{margin:0!important;flex:0 0 auto!important;}' +" +
                        @"'#irongoo-folder-header .irongoo-folder-actions,#irongoo-remote-header .irongoo-remote-actions{' +" +
                        @"'display:inline-flex!important;flex-wrap:wrap!important;align-items:center!important;gap:6px!important;' +" +
                        @"'float:none!important;margin:0!important;flex:0 0 auto!important;}' +" +
                        @"'#irongoo-folder-header .irongoo-folder-actions>button[ng-click=""addFolder()""]{order:1;}' +" +
                        @"'#irongoo-folder-header .irongoo-folder-actions>button[ng-click=""rescanAllFolders()""]{order:2;}' +" +
                        @"'#irongoo-folder-header .irongoo-folder-actions>button[ng-click=""setAllFoldersPause(true)""]{order:3;}' +" +
                        @"'#irongoo-folder-header .irongoo-folder-actions>button[ng-click=""setAllFoldersPause(false)""]{order:4;}' +" +
                        @"'#folders,#devices{width:100%!important;max-width:100%!important;clear:both!important;float:none!important;}' +" +
                        @"'.clearfix.irongoo-folder-actions-host,.form-group.irongoo-remote-actions-host{display:none!important;}' +" +
                        @"'.row.irongoo-layout-row{display:flex!important;flex-wrap:wrap!important;}' +" +
                        @"'#irongoo-this-device-wrap{flex:0 0 100%!important;max-width:100%!important;width:100%!important;float:none!important;' +" +
                        @"'padding-left:15px!important;padding-right:15px!important;box-sizing:border-box!important;margin:8px 0 0!important;order:2!important;}' +" +
                        @"'.irongoo-stats-grid{display:grid!important;grid-template-columns:1fr 1fr!important;gap:0 28px!important;width:100%!important;}' +" +
                        @"'.irongoo-stats-grid>table{width:100%!important;margin:0!important;}' +" +
                        @"'.row.irongoo-layout-row>.irongoo-col-folders,.row.irongoo-layout-row>.irongoo-col-remotes{' +" +
                        @"'float:none!important;width:50%!important;max-width:50%!important;flex:0 0 50%!important;box-sizing:border-box!important;order:1!important;}' +" +
                        @"'#idqr .well,#idqr .well *{' +" +
                        @"'cursor:pointer!important;user-select:none!important;-webkit-user-select:none!important;' +" +
                        @"'-moz-user-select:none!important;outline:none!important;}' +" +
                        @"'#idqr .well::selection,#idqr .well *::selection{background:transparent!important;color:inherit!important;}' +" +
                        @"'#idqr .well::-moz-selection,#idqr .well *::-moz-selection{background:transparent!important;color:inherit!important;}' +" +
                        @"'#idqr .well{transition:background-color .15s,color .15s,box-shadow .15s;}' +" +
                        @"'#idqr .well:hover{background-color:#3a2430!important;color:#fff!important;box-shadow:inset 0 0 0 1px #E73F91!important;}' +" +
                        @"'#idqr .well.irongoo-copied{background-color:#E73F91!important;color:#fff!important;}' +" +
                        @"'#irongoo-copied-toast{position:fixed;left:50%;top:0;bottom:auto;transform:translateX(-50%) translateY(6px);' +" +
                        @"'background:#E73F91;color:#fff;padding:8px 18px;border-radius:6px;font-size:13px;font-weight:600;' +" +
                        @"'z-index:20000;opacity:0;pointer-events:none;transition:opacity .18s,transform .18s;' +" +
                        @"'box-shadow:0 4px 16px rgba(0,0,0,.35);white-space:nowrap;}' +" +
                        @"'#irongoo-copied-toast.show{opacity:1;transform:translateX(-50%) translateY(0);}' +" +
                        @"'#editDevice .help-block,#editFolder .help-block{display:none!important;}' +" +
                        @"'#folder-general.active{display:flex!important;flex-direction:column!important;}' +" +
                        @"'#folder-general:not(.active){display:none!important;}' +" +
                        @"'#folder-general>.form-group:has(#folderID){order:1!important;}' +" +
                        @"'#folder-general>.form-group:has(#folderPath){order:2!important;}' +" +
                        @"'#folder-general>.form-group:has(#folderLabel){order:3!important;}' +" +
                        @"'#folder-general>.form-group:has(#folderGroup){order:4!important;}' +" +
                        @"'#folder-general>.irongoo-share-host{order:5!important;margin-top:12px!important;}' +" +
                        @"'#folder_list>span:not(:first-of-type),#irongoo-remote-header h3>span:not(:first-of-type),' +" +
                        @"'#irongoo-folder-header h3>span:not(:first-of-type),' +" +
                        @"'h3>span[ng-if*=folderList],h3>span[ng-if*=otherDevices]{display:none!important;}' +" +
                        @"'#editFolder .nav-tabs>li:has(a[href=""#folder-sharing""]){display:none!important;}' +" +
                        @"'.irongoo-share-heading{font-size:14px!important;font-weight:600!important;color:#e6e6e6!important;' +" +
                        @"'margin:0 0 10px!important;display:flex!important;align-items:center!important;gap:6px!important;}' +" +
                        @"'.irongoo-share-heading .fas,.irongoo-share-heading .fa{color:#5bc0de!important;}' +" +
                        @"'[language-select] .dropdown-menu{column-count:1!important;columns:1!important;' +" +
                        @"'width:auto!important;min-width:8em!important;max-width:12em!important;' +" +
                        @"'right:0!important;left:auto!important;padding:4px 0!important;}' +" +
                        @"'[language-select] .dropdown-menu>li>a{padding:6px 16px!important;white-space:nowrap!important;}' +" +
                        @"'.navbar .dropdown.action-menu:has(.fa-question-circle),' +" +
                        @"'.navbar .dropdown.action-menu:has(.fas.fa-question-circle){display:none!important;}' +" +
                        @"'.navbar li.irongoo-nav-item>a.irongoo-nav-link{' +" +
                        @"'position:relative!important;display:block!important;background:transparent!important;background-image:none!important;' +" +
                        @"'border:none!important;box-shadow:none!important;text-shadow:none!important;outline:none!important;}' +" +
                        @"'.navbar li.irongoo-nav-item>a.irongoo-nav-link:hover,' +" +
                        @"'.navbar li.irongoo-nav-item>a.irongoo-nav-link:focus,' +" +
                        @"'.navbar li.irongoo-nav-item>a.irongoo-nav-link:active{' +" +
                        @"'background:transparent!important;background-image:none!important;border:none!important;' +" +
                        @"'box-shadow:none!important;text-shadow:none!important;color:#fff!important;}' +" +
                        @"'.navbar li.irongoo-nav-item .irongoo-corner{position:absolute!important;width:8px!important;height:8px!important;' +" +
                        @"'pointer-events:none!important;box-sizing:border-box!important;display:none!important;}' +" +
                        @"'.navbar li.irongoo-nav-item:hover>a.irongoo-nav-link .irongoo-corner{display:block!important;}' +" +
                        @"'.navbar li.irongoo-nav-item .irongoo-corner.tl{top:6px!important;left:4px!important;' +" +
                        @"'border-top:2.5px solid #E73F91!important;border-left:2.5px solid #E73F91!important;}' +" +
                        @"'.navbar li.irongoo-nav-item .irongoo-corner.tr{top:6px!important;right:4px!important;' +" +
                        @"'border-top:2.5px solid #E73F91!important;border-right:2.5px solid #E73F91!important;}' +" +
                        @"'.navbar li.irongoo-nav-item .irongoo-corner.bl{bottom:6px!important;left:4px!important;' +" +
                        @"'border-bottom:2.5px solid #E73F91!important;border-left:2.5px solid #E73F91!important;}' +" +
                        @"'.navbar li.irongoo-nav-item .irongoo-corner.br{bottom:6px!important;right:4px!important;' +" +
                        @"'border-bottom:2.5px solid #E73F91!important;border-right:2.5px solid #E73F91!important;}' +" +
                        @"'.navbar p.navbar-text.irongoo-device-name{cursor:text!important;outline:none!important;box-shadow:none!important;' +" +
                        @"'border:none!important;border-radius:0!important;background:transparent!important;}' +" +
                        @"'.navbar p.navbar-text.irongoo-device-name:hover:not(.irongoo-name-editing){color:#fff!important;box-shadow:inset 0 -1px 0 #E73F91!important;}' +" +
                        @"'.navbar p.navbar-text.irongoo-name-editing,' +" +
                        @"'.navbar p.navbar-text.irongoo-name-editing:focus,' +" +
                        @"'.navbar p.navbar-text.irongoo-name-editing:focus-visible,' +" +
                        @"'.navbar p.navbar-text.irongoo-name-editing:active{' +" +
                        @"'color:#fff!important;outline:none!important;outline-offset:0!important;border:none!important;border-radius:0!important;' +" +
                        @"'box-shadow:inset 0 -2px 0 #E73F91!important;-webkit-box-shadow:inset 0 -2px 0 #E73F91!important;' +" +
                        @"'background:transparent!important;-webkit-user-select:text!important;user-select:text!important;' +" +
                        @"'caret-color:#E73F91!important;}';" +
                        @"  var ensure = function(){" +
                        @"    if (busy) return;" +
                        @"    busy = true;" +
                        @"    try {" +
                        @"      if (!document.getElementById('irongoo-theme-link')) {" +
                        @"        var l = document.createElement('link');" +
                        @"        l.id = 'irongoo-theme-link'; l.rel = 'stylesheet';" +
                        @"        l.href = 'assets/css/irongoo.css?v=62';" +
                        @"        (document.head||document.documentElement).appendChild(l);" +
                        @"      }" +
                        @"      var st = document.getElementById('irongoo-theme-style');" +
                        @"      if (!st) {" +
                        @"        st = document.createElement('style'); st.id = 'irongoo-theme-style';" +
                        @"        st.setAttribute('data-v','62');" +
                        @"        (document.head||document.documentElement).appendChild(st);" +
                        @"        st.textContent = styleCss;" +
                        @"      } else if (st.getAttribute('data-v') !== '62') {" +
                        @"        st.setAttribute('data-v','62'); st.textContent = styleCss;" +
                        @"      }" +
                        @"      filterLanguages();" +
                        @"      var helpMenus = document.querySelectorAll('.navbar .dropdown.action-menu');" +
                        @"      for (var hm = 0; hm < helpMenus.length; hm++) {" +
                        @"        if (helpMenus[hm].querySelector('.fa-question-circle')) helpMenus[hm].style.display = 'none';" +
                        @"      }" +
                        @"      var ids = document.querySelectorAll('#idqr .well');" +
                        @"      for (var j = 0; j < ids.length; j++) wireDeviceId(ids[j]);" +
                        /* Layout: inject here so Cef cache of Syncthing overlay JS cannot block it */
                        @"      if (typeof window.__irongooEnsureMainLayout === 'function') window.__irongooEnsureMainLayout();" +
                        @"      else { try { " +
                        @"        var folderAnchor=document.getElementById('folder_list')||document.querySelector('[id^=folders]')||document.getElementById('folders');" +
                        @"        var deviceAnchor=document.getElementById('device-this')||document.querySelector('[id^=devices]')||document.getElementById('devices');" +
                        @"        if(folderAnchor&&deviceAnchor){" +
                        @"          var folderCol=folderAnchor.closest?folderAnchor.closest('.col-md-6'):null;" +
                        @"          var deviceCol=deviceAnchor.closest?deviceAnchor.closest('.col-md-6'):null;" +
                        @"          if(folderCol&&deviceCol&&folderCol!==deviceCol){" +
                        @"            var row=folderCol.parentElement;" +
                        @"            if(row&&deviceCol.parentElement===row){" +
                        @"              row.classList.add('irongoo-layout-row');" +
                        @"              var top=document.getElementById('irongoo-this-device-wrap');" +
                        @"              if(!top){ top=document.createElement('div'); top.id='irongoo-this-device-wrap'; row.appendChild(top); }" +
                        @"              else if(top.parentNode!==row) row.appendChild(top);" +
                        @"              var thisH3=null, h3s=deviceCol.getElementsByTagName('h3');" +
                        @"              for(var hi=0;hi<h3s.length;hi++){ var ht=(h3s[hi].textContent||'').trim();" +
                        @"                if(ht.indexOf('此设备')===0||ht.indexOf('This Device')===0){ thisH3=h3s[hi]; break; } }" +
                        @"              if(!thisH3&&h3s.length) thisH3=h3s[0];" +
                        @"              var thisPanel=null;" +
                        @"              if(thisH3){ var el=thisH3.nextElementSibling; while(el&&!(el.classList&&el.classList.contains('panel'))) el=el.nextElementSibling; thisPanel=el; }" +
                        @"              if(!thisPanel){ for(var di=0;di<deviceCol.children.length;di++){ if(deviceCol.children[di].classList&&deviceCol.children[di].classList.contains('panel')){ thisPanel=deviceCol.children[di]; break; } } }" +
                        @"              if(thisH3&&thisH3.parentNode!==top) top.appendChild(thisH3);" +
                        @"              if(thisPanel&&thisPanel.parentNode!==top) top.appendChild(thisPanel);" +
                        @"              folderCol.classList.add('irongoo-col-folders'); deviceCol.classList.add('irongoo-col-remotes');" +
                        @"              if(folderCol.nextElementSibling!==deviceCol) row.appendChild(deviceCol);" +
                        @"              if(thisPanel){" +
                        @"                var body=thisPanel.querySelector('#device-this .panel-body')||thisPanel.querySelector('.panel-body');" +
                        @"                if(body){ var wrap=body.querySelector('.irongoo-stats-grid'); var table=wrap?wrap.querySelector('table.irongoo-stats-left'):null;" +
                        @"                  if(!table){ for(var bi=0;bi<body.children.length;bi++){ if(body.children[bi].tagName==='TABLE'){ table=body.children[bi]; break; } } }" +
                        @"                  if(table&&table.tBodies&&table.tBodies[0]){" +
                        @"                    if(!wrap){ wrap=document.createElement('div'); wrap.className='irongoo-stats-grid'; table.parentNode.insertBefore(wrap,table); wrap.appendChild(table); }" +
                        @"                    var tableRight=wrap.querySelector('table.irongoo-stats-right');" +
                        @"                    if(!tableRight){ tableRight=table.cloneNode(false); tableRight.className=(table.className||'')+' irongoo-stats-right'; var tbR=document.createElement('tbody'); tableRight.appendChild(tbR); wrap.appendChild(tableRight); }" +
                        @"                    table.classList.add('irongoo-stats-left'); var tbody=table.tBodies[0], tbodyRight=tableRight.tBodies[0];" +
                        @"                    var rows=Array.prototype.slice.call(tbody.rows);" +
                        @"                    for(var ri=0;ri<rows.length;ri++){ var html=rows[ri].innerHTML||'';" +
                        @"                      if(html.indexOf('fa-sitemap')>=0||html.indexOf('fa-map-signs')>=0||html.indexOf('fa-clock')>=0||html.indexOf('fa-qrcode')>=0||html.indexOf('fa-tag')>=0) tbodyRight.appendChild(rows[ri]); }" +
                        @"                  }" +
                        @"                }" +
                        @"              }" +
                        @"            }" +
                        @"          }" +
                        @"        }" +
                        @"      } catch(layoutErr) {} }" +
                        @"    } finally { busy = false; }" +
                        @"  };" +
                        @"  var timer = null;" +
                        @"  var stuckTimer = null;" +
                        @"  var schedule = function(){" +
                        @"    if (timer) clearTimeout(timer);" +
                        @"    timer = setTimeout(ensure, 120);" +
                        @"    if (stuckTimer) clearTimeout(stuckTimer);" +
                        @"    stuckTimer = setTimeout(clearStuckOverlay, 800);" +
                        @"  };" +
                        @"  ensure();" +
                        @"  new MutationObserver(schedule).observe(document.documentElement,{childList:true,subtree:true});" +
                        @"})();");

                    RunJs(
                        @"(function(){" +
                        @"  var last = '';" +
                        @"  var notify = function(code){" +
                        @"    if (!code) { try { code = localStorage.getItem('SYN_LANG') || ''; } catch(e) { code = ''; } }" +
                        @"    if (!code || code === last) return;" +
                        @"    last = code;" +
                        @"    try { callbackObject.guiLanguageChanged(code); } catch(e) {}" +
                        @"  };" +
                        @"  var wrap = function(){" +
                        @"    try {" +
                        @"      var inj = window.angular && angular.element(document.body).injector();" +
                        @"      if (!inj) return false;" +
                        @"      var ls = inj.get('LocaleService');" +
                        @"      if (!ls || ls._irongooWrapped) return !!ls;" +
                        @"      ls._irongooWrapped = true;" +
                        @"      var orig = ls.useLocale.bind(ls);" +
                        @"      ls.useLocale = function(locale, save){" +
                        @"        var r = orig(locale, save);" +
                        @"        notify(locale);" +
                        @"        return r;" +
                        @"      };" +
                        @"      try { notify(ls.getCurrentLocale && ls.getCurrentLocale()); } catch(e) {}" +
                        @"      return true;" +
                        @"    } catch(e) { return false; }" +
                        @"  };" +
                        @"  if (!wrap()) {" +
                        @"    var n = 0;" +
                        @"    var t = setInterval(function(){ if (wrap() || ++n > 50) clearInterval(t); }, 200);" +
                        @"  }" +
                        @"  notify();" +
                        @"})();");

                    // Replace Syncthing navbar logo+wordmark with irongoo branding
                    var logoDataUri = GetBrandLogoDataUri();
                    if (!string.IsNullOrEmpty(logoDataUri))
                    {
                        var replaceBrandLogo =
                            @"(function(){" +
                            @"  var src = '" + logoDataUri + @"';" +
                            @"  var st = document.getElementById('irongoo-brand-style');" +
                            @"  if (!st) { st = document.createElement('style'); st.id = 'irongoo-brand-style'; document.head.appendChild(st); }" +
                            // Avoid Syncthing's .logo { top:-5px }. Wordmark optical center sits high (g descender) — nudge down.
                            @"  st.textContent = '.navbar-brand{display:inline-flex!important;align-items:center!important;" +
                            @"height:50px!important;padding:0 15px 0 0!important;float:left!important;}' +" +
                            @"'.navbar-brand img.irongoo-logo{position:static!important;top:auto!important;" +
                            @"margin:0!important;padding:0!important;height:24px!important;width:auto!important;" +
                            @"max-width:200px!important;display:block!important;background:transparent!important;" +
                            @"transform:translateY(3px)!important;}';" +
                            @"  var apply = function(){" +
                            @"    var brand = document.querySelector('.navbar-brand');" +
                            @"    if (!brand) return;" +
                            @"    var img = brand.querySelector('img.irongoo-logo');" +
                            @"    if (img && img.getAttribute('src') === src) return;" +
                            @"    brand.innerHTML = '<img class=""irongoo-logo"" src=""' + src + '"" alt=""irongoo""/>';" +
                            @"  };" +
                            @"  apply();" +
                            @"  new MutationObserver(apply).observe(document.documentElement, {childList:true, subtree:true});" +
                            @"})();";
                        RunJs(replaceBrandLogo);
                    }

                    // Re-apply This Device visibility after GUI scripts finish
                    ApplyThisDeviceVisibilityScript();
                }
            };

            // Chinese IME workaround, copied from
            // https://github.com/cefsharp/CefSharp/commit/c7c90581da7ed3dda80fd8304a856462d133d9a7
            webBrowser.PreviewTextInput += (o, e) =>
            {
                var host = webBrowser.GetBrowser().GetHost();
                var keyEvent = new KeyEvent();
                foreach (var character in e.Text)
                {
                    keyEvent.WindowsKeyCode = character;
                    keyEvent.Type = KeyEventType.Char;
                    host.SendKeyEvent(keyEvent);
                }

                e.Handled = true;
            };
        }

        public void RefreshBrowserNukeCache()
        {
            if (Location == GetSyncthingAddress().ToString())
            {
                WebBrowser?.Reload(ignoreCache: true);
            }
            else if (syncthingManager.State == SyncthingState.Running)
            {
                Location = GetSyncthingAddress().ToString();
            }
        }

        public void RefreshBrowser()
        {
            Location = "about:blank";
            if (syncthingManager.State == SyncthingState.Running)
            {
                Location = GetSyncthingAddress().ToString();
            }
        }

        private bool showThisDevice = true;

        public void ApplyShowThisDevice(bool show)
        {
            showThisDevice = show;
            ApplyThisDeviceVisibilityScript();
        }

        private void ApplyThisDeviceVisibilityScript()
        {
            if (WebBrowser == null)
                return;

            var showJs = showThisDevice ? "true" : "false";
            var script =
                @"(function(){" +
                @"  window.__irongooShowThisDevice=" + showJs + ";" +
                @"  if(typeof window.__irongooApplyThisDeviceVisibility==='function'){ window.__irongooApplyThisDeviceVisibility(); return; }" +
                @"  var el=document.getElementById('irongoo-this-device-wrap');" +
                @"  if(!el) return;" +
                @"  if(window.__irongooShowThisDevice===false){ el.style.setProperty('display','none','important'); }" +
                @"  else { el.style.removeProperty('display'); }" +
                @"})();";
            try { WebBrowser.ExecuteScriptAsync(script); } catch { /* browser not ready */ }
        }

        public void ZoomIn()
        {
            ZoomTo(zoomLevel + 0.2);
        }

        public void ZoomOut()
        {
            ZoomTo(zoomLevel - 0.2);
        }

        public void ZoomReset()
        {
            ZoomTo(0.0);
        }

        private void ZoomTo(double zoomLevel)
        {
            if (WebBrowser == null || syncthingState != SyncthingState.Running)
                return;

            this.zoomLevel = zoomLevel;
            WebBrowser.SetZoomLevel(zoomLevel);
            configurationProvider.AtomicLoadAndSave(c => c.SyncthingWebBrowserZoomLevel = zoomLevel);
        }

        private void OpenFolder(string folderId)
        {
            if (!syncthingManager.Folders.TryFetchById(folderId, out var folder))
                return;

            processStartProvider.ShowFolderInExplorer(folder.Path);
        }

        private void BrowseFolderPath()
        {
            Execute.OnUIThread(() =>
            {
                var dialog = new CommonOpenFileDialog()
                {
                    IsFolderPicker = true,
                };
                var result = dialog.ShowDialog();
                if (result == CommonFileDialogResult.Ok)
                {
                    var script =
                        @"$('#folderPath').val('" + dialog.FileName.Replace("\\", "\\\\").Replace("'", "\\'") + "');" +
                        @"$('#folderPath').change();";
                    WebBrowser.ExecuteScriptAsync(script);
                }
            });
        }

        protected override void OnClose()
        {
            WebBrowser?.Dispose();
            WebBrowser = null;

            // This is such a dirty, horrible, hacky thing to do...
            // So it turns out that doesn't like being shut down, then re-initialized, see http://www.magpcss.org/ceforum/viewtopic.php?f=6&t=10807&start=10
            // and others. However, if we wait a little while (presumably for the WebBrowser to die and all open connections to the subprocess
            // to close), then kill it in a very dirty way (by killing the process rather than calling Cef.Shutdown), it springs back to life
            // when Cef.Initialize is called again.
            // I'm not 100% it's not leaking something somewhere, but it seems to work, and saves 50MB of idle memory usage
            // However, I'm not comfortable enough with this to enable it permanently yet
            //await Task.Delay(5000);
            //CefSharpHelper.TerminateCefSharpProcess();
        }

        public async void Start()
        {
            await syncthingManager.StartWithErrorDialogAsync(windowManager);
        }

        private Uri GetSyncthingAddress()
        {
            // SyncthingManager will always request over HTTPS, whether Syncthing enforces this or not.
            // However in an attempt to avoid #201 we'll use HTTP if available, and if not Syncthing will redirect us.
            var uriBuilder = new UriBuilder(syncthingManager.Address.NormalizeZeroHost())
            {
                Scheme = "http"
            };
            return uriBuilder.Uri;
        }

        private string GetBrandLogoDataUri()
        {
            if (brandLogoDataUri != null)
                return brandLogoDataUri;

            try
            {
                var resource = Application.GetResourceStream(
                    new Uri("pack://application:,,,/Resources/logo-white-pink.png"));
                if (resource?.Stream == null)
                    return string.Empty;

                using var stream = resource.Stream;
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                brandLogoDataUri = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                return brandLogoDataUri;
            }
            catch
            {
                return string.Empty;
            }
        }

        bool IResourceRequestHandlerFactory.HasHandlers => true;

        IResourceRequestHandler IResourceRequestHandlerFactory.GetResourceRequestHandler(IWebBrowser chromiumWebBrowser,
            IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload,
            string requestInitiator, ref bool disableDefaultHandling)
        {
            return customResourceRequestHandler;
        }

        private CefReturnValue OnBeforeResourceLoad(IWebBrowser browserControl, IBrowser browser, IFrame frame,
            IRequest request, IRequestCallback callback)
        {
            var uri = new Uri(request.Url);
            // We can get http requests just after changing Syncthing's address: after we've navigated to about:blank but before navigating to
            // the new address (Which we do when Syncthing hits the 'running' State).
            // Therefore only open external browsers if Syncthing is actually running
            if (syncthingManager.State == SyncthingState.Running && (uri.Scheme == "http" || uri.Scheme == "https") &&
                uri.Host != GetSyncthingAddress().Host)
            {
                processStartProvider.StartDetached(request.Url);
                return CefReturnValue.Cancel;
            }

            // See https://github.com/canton7/SyncTrayzor/issues/13
            // and https://github.com/cefsharp/CefSharp/issues/534#issuecomment-60694502
            var headers = request.Headers;
            headers["X-API-Key"] = syncthingManager.ApiKey;

            // I don't know why it adds these, even when we explicitly disable caching.
            headers.Remove("Cache-Control");
            headers.Remove("If-None-Match");
            headers.Remove("If-Modified-Since");

            request.Headers = headers;

            return CefReturnValue.Continue;
        }

        void ILifeSpanHandler.OnBeforeClose(IWebBrowser browserControl, IBrowser browser)
        {
        }

        bool ILifeSpanHandler.OnBeforePopup(IWebBrowser browserControl, IBrowser browser, IFrame frame,
            string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture,
            IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings,
            ref bool noJavascriptAccess, out IWebBrowser? newBrowser)
        {
            processStartProvider.StartDetached(targetUrl);
            newBrowser = null;
            return true;
        }

        void ILifeSpanHandler.OnAfterCreated(IWebBrowser browserControl, IBrowser browser)
        {
        }

        bool ILifeSpanHandler.DoClose(IWebBrowser browserControl, IBrowser browser)
        {
            return false;
        }

        void IContextMenuHandler.OnBeforeContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame,
            IContextMenuParams parameters, IMenuModel model)
        {
            // Clear the default menu, just leaving our custom one
            model.Clear();
        }

        bool IContextMenuHandler.OnContextMenuCommand(IWebBrowser browserControl, IBrowser browser, IFrame frame,
            IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
        {
            return false;
        }

        void IContextMenuHandler.OnContextMenuDismissed(IWebBrowser browserControl, IBrowser browser, IFrame frame)
        {
        }

        bool IContextMenuHandler.RunContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame,
            IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            return false;
        }

        public void Dispose()
        {
            syncthingManager.StateChanged -= SyncthingStateChanged;
            configurationProvider.ConfigurationChanged -= ConfigurationChanged;
        }

        private class CustomRequestHandler : RequestHandler
        {
            protected override bool OnCertificateError(IWebBrowser chromiumWebBrowser, IBrowser browser,
                CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
            {
                // We shouldn't hit this because IgnoreCertificateErrors is true, but we do
                callback.Continue(true);
                return true;
            }
        }

        private class CustomResourceRequestHandler : ResourceRequestHandler
        {
            private readonly ViewerViewModel parent;

            public CustomResourceRequestHandler(ViewerViewModel parent)
            {
                this.parent = parent;
            }

            protected override CefReturnValue OnBeforeResourceLoad(IWebBrowser chromiumWebBrowser, IBrowser browser,
                IFrame frame, IRequest request, IRequestCallback callback)
            {
                return parent.OnBeforeResourceLoad(chromiumWebBrowser, browser, frame, request, callback);
            }
        }

        private class JavascriptCallbackObject
        {
            private readonly ViewerViewModel parent;

            public JavascriptCallbackObject(ViewerViewModel parent)
            {
                this.parent = parent;
            }

            public void OpenFolder(string folderId)
            {
                parent.OpenFolder(folderId);
            }

            public void BrowseFolderPath()
            {
                parent.BrowseFolderPath();
            }

            public void GuiLanguageChanged(string locale)
            {
                parent.ApplyGuiLanguageFromWeb(locale);
            }
        }
    }
}