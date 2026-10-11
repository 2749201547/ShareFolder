using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Resources;
using System.Xml;
using NLog;

namespace SyncTrayzor.Services
{
    /// <summary>
    /// Writes Syncthing GUI overlays so branding also appears in external browsers
    /// (not only the embedded CefSharp viewer).
    /// Files go under {syncthingHome}/gui/{theme}/ and overlay compiled-in assets.
    /// </summary>
    public static class IrongooGuiBranding
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly string[] ThemesToOverride = { "default", "dark", "black", "light" };

        public const string ThemeCssFileName = "irongoo.css";

        /// <summary>irongoo pink accent CSS served as /assets/css/irongoo.css</summary>
        public const string ThemeCss =
            "/* irongoo-theme — Cursor-like: #1e1e1e base, #252526 surfaces, #2d2d2d raised */\n" +
            "html,\n" +
            "body,\n" +
            ".container,\n" +
            ".container-fluid {\n" +
            "  background-color: #1e1e1e !important;\n" +
            "  color: #e6e6e6 !important;\n" +
            "}\n" +
            ".navbar,\n" +
            ".navbar-default,\n" +
            ".navbar-inverse {\n" +
            "  background-color: #1e1e1e !important;\n" +
            "  border-color: #2a2a2a !important;\n" +
            "  box-shadow: none !important;\n" +
            "}\n" +
            ".panel,\n" +
            ".panel-default,\n" +
            ".panel-group .panel {\n" +
            "  background-color: #252526 !important;\n" +
            "  border-color: #333333 !important;\n" +
            "  color: #e6e6e6 !important;\n" +
            "}\n" +
            ".panel-default > .panel-heading,\n" +
            ".panel > .btn.panel-heading,\n" +
            "button.btn.panel-heading {\n" +
            "  background-color: #252526 !important;\n" +
            "  border-color: #333333 !important;\n" +
            "  color: #e6e6e6 !important;\n" +
            "}\n" +
            ".panel-body,\n" +
            ".panel-footer {\n" +
            "  background-color: #252526 !important;\n" +
            "  border-color: #333333 !important;\n" +
            "}\n" +
            ".table,\n" +
            ".table > thead > tr > th,\n" +
            ".table > tbody > tr > th,\n" +
            ".table > tbody > tr > td {\n" +
            "  background-color: transparent !important;\n" +
            "  border-color: #333333 !important;\n" +
            "}\n" +
            ".table-striped > tbody > tr:nth-of-type(odd) {\n" +
            "  background-color: #222222 !important;\n" +
            "}\n" +
            ".table-striped > tbody > tr:nth-of-type(even) {\n" +
            "  background-color: #252526 !important;\n" +
            "}\n" +
            ".btn-default {\n" +
            "  background-color: #2d2d2d !important;\n" +
            "  border-color: #3c3c3c !important;\n" +
            "  color: #e6e6e6 !important;\n" +
            "}\n" +
            ".btn-default:hover,\n" +
            ".btn-default:focus {\n" +
            "  background-color: #383838 !important;\n" +
            "  border-color: #4a4a4a !important;\n" +
            "  color: #ffffff !important;\n" +
            "}\n" +
            ".dropdown-menu {\n" +
            "  background-color: #252526 !important;\n" +
            "  border-color: #333333 !important;\n" +
            "}\n" +
            ".dropdown-menu > li > a {\n" +
            "  color: #e6e6e6 !important;\n" +
            "}\n" +
            ".dropdown-menu > li > a:hover,\n" +
            ".dropdown-menu > .active > a {\n" +
            "  background-color: #2d2d2d !important;\n" +
            "  color: #ffffff !important;\n" +
            "}\n" +
            ".form-control,\n" +
            ".well {\n" +
            "  background-color: #1e1e1e !important;\n" +
            "  border-color: #333333 !important;\n" +
            "  color: #e6e6e6 !important;\n" +
            "}\n" +
            /* Focus glow: white, same strength as Bootstrap blue (0.6 / 8px) */
            ".form-control:focus,\n" +
            ".form-control:focus-visible,\n" +
            "input.form-control:focus,\n" +
            "textarea.form-control:focus,\n" +
            "select.form-control:focus,\n" +
            ".input-group .form-control:focus {\n" +
            "  border-color: rgba(255,255,255,.85) !important;\n" +
            "  outline: 0 !important;\n" +
            "  box-shadow: inset 0 1px 1px rgba(0,0,0,.075), 0 0 8px rgba(255,255,255,.6) !important;\n" +
            "  -webkit-box-shadow: inset 0 1px 1px rgba(0,0,0,.075), 0 0 8px rgba(255,255,255,.6) !important;\n" +
            "}\n" +
            ".modal-content {\n" +
            "  background-color: #252526 !important;\n" +
            "  border-color: #333333 !important;\n" +
            "}\n" +
            "hr { border-color: #333333 !important; }\n" +
            ".modal-header.alert,\n" +
            ".modal-header.alert-info,\n" +
            ".alert-info,\n" +
            "#idqr .modal-header,\n" +
            ".panel-info > .panel-heading,\n" +
            ".btn-info,\n" +
            ".btn-info:hover,\n" +
            ".btn-info:focus,\n" +
            ".btn-info:active,\n" +
            ".btn-info:active:hover,\n" +
            ".btn-info.active,\n" +
            ".label-info,\n" +
            ".bg-info,\n" +
            ".progress-bar-info,\n" +
            ".modal-header.alert-success,\n" +
            ".alert-success,\n" +
            ".panel-success > .panel-heading,\n" +
            ".btn-success,\n" +
            ".btn-success:hover,\n" +
            ".btn-success:focus,\n" +
            ".btn-success:active,\n" +
            ".btn-success:active:hover,\n" +
            ".btn-success.active,\n" +
            ".label-success,\n" +
            ".bg-success,\n" +
            ".progress-bar-success,\n" +
            ".modal-header.alert-warning,\n" +
            ".alert-warning,\n" +
            ".panel-warning > .panel-heading,\n" +
            ".panel-warning > .panel-heading .panel-title {\n" +
            "  background-color: #E73F91 !important;\n" +
            "  background-image: none !important;\n" +
            "  border-color: #C9347A !important;\n" +
            "  color: #ffffff !important;\n" +
            "}\n" +
            ".panel-warning {\n" +
            "  border-color: #C9347A !important;\n" +
            "  background-color: #252526 !important;\n" +
            "}\n" +
            /* Pending device/folder notice body stays dark */
            ".panel-warning > .panel-body,\n" +
            ".panel-warning > .panel-footer {\n" +
            "  background-color: #252526 !important;\n" +
            "  border-color: #333333 !important;\n" +
            "  color: #e6e6e6 !important;\n" +
            "}\n" +
            /* Timestamp: normal UI font (Syncthing/theme often forces odd mono digits) */
            ".panel-warning > .panel-heading .pull-right,\n" +
            ".panel-warning > .panel-heading .panel-title .pull-right {\n" +
            "  font-family: \"Segoe UI\", \"Microsoft YaHei UI\", \"Microsoft YaHei\", sans-serif !important;\n" +
            "  font-size: 13px !important;\n" +
            "  font-weight: 400 !important;\n" +
            "  font-variant-numeric: normal !important;\n" +
            "  font-feature-settings: normal !important;\n" +
            "  letter-spacing: 0 !important;\n" +
            "  color: #ffffff !important;\n" +
            "}\n" +
            ".text-info,\n" +
            ".text-success,\n" +
            ".text-primary,\n" +
            "a.text-primary {\n" +
            "  color: #E73F91 !important;\n" +
            "}\n" +
            /* Bootstrap primary blue → pink (Save, tabs, links) */
            ".btn-primary,\n" +
            ".btn-primary:hover,\n" +
            ".btn-primary:focus,\n" +
            ".btn-primary:active,\n" +
            ".btn-primary:active:hover,\n" +
            ".btn-primary.active,\n" +
            ".btn-primary.active:hover,\n" +
            ".open > .dropdown-toggle.btn-primary {\n" +
            "  background-color: #E73F91 !important;\n" +
            "  background-image: none !important;\n" +
            "  border-color: #C9347A !important;\n" +
            "  color: #ffffff !important;\n" +
            "}\n" +
            ".btn-primary:hover,\n" +
            ".btn-primary:focus {\n" +
            "  background-color: #d63582 !important;\n" +
            "  border-color: #b82d70 !important;\n" +
            "}\n" +
            ".nav-tabs > li > a,\n" +
            ".nav-tabs > li > a:hover,\n" +
            ".nav-tabs > li > a:focus,\n" +
            ".nav-tabs > li.active > a,\n" +
            ".nav-tabs > li.active > a:hover,\n" +
            ".nav-tabs > li.active > a:focus,\n" +
            ".nav-tabs > li > a .fas,\n" +
            ".nav-tabs > li > a .fa,\n" +
            ".nav-tabs > li > a .far {\n" +
            "  color: #E73F91 !important;\n" +
            "}\n" +
            ".nav-tabs > li.active > a,\n" +
            ".nav-tabs > li.active > a:hover,\n" +
            ".nav-tabs > li.active > a:focus {\n" +
            "  background-color: #252526 !important;\n" +
            "  border-color: #333333 #333333 #252526 !important;\n" +
            "}\n" +
            ".modal .text-primary,\n" +
            ".modal label .fas.text-primary,\n" +
            ".modal label .fa.text-primary,\n" +
            ".modal h4 .fas,\n" +
            ".modal h4 .fa,\n" +
            ".modal .panel-heading .fas.text-primary,\n" +
            ".modal .panel-heading .fa.text-primary {\n" +
            "  color: #E73F91 !important;\n" +
            "}\n" +
            "* {\n" +
            "  scrollbar-width: thin;\n" +
            "  scrollbar-color: #424242 #1e1e1e;\n" +
            "}\n" +
            "::-webkit-scrollbar { width: 10px !important; height: 10px !important; }\n" +
            "::-webkit-scrollbar-track { background: #1e1e1e !important; }\n" +
            "::-webkit-scrollbar-thumb {\n" +
            "  background: #424242 !important;\n" +
            "  border-radius: 3px !important;\n" +
            "  border: 1px solid #1e1e1e !important;\n" +
            "}\n" +
            "::-webkit-scrollbar-thumb:hover { background: #555555 !important; }\n" +
            "::-webkit-scrollbar-corner { background: #1e1e1e !important; }\n" +
            "#idqr .well,\n" +
            "#idqr .well * {\n" +
            "  cursor: pointer !important;\n" +
            "  user-select: none !important;\n" +
            "  -webkit-user-select: none !important;\n" +
            "  outline: none !important;\n" +
            "}\n" +
            "#idqr .well::selection,\n" +
            "#idqr .well *::selection {\n" +
            "  background: transparent !important;\n" +
            "  color: inherit !important;\n" +
            "}\n" +
            "#idqr .well {\n" +
            "  transition: background-color .15s, color .15s, box-shadow .15s;\n" +
            "}\n" +
            "#idqr .well:hover {\n" +
            "  background-color: #3a2430 !important;\n" +
            "  color: #ffffff !important;\n" +
            "  box-shadow: inset 0 0 0 1px #E73F91 !important;\n" +
            "}\n" +
            "#idqr .well.irongoo-copied {\n" +
            "  background-color: #E73F91 !important;\n" +
            "  color: #ffffff !important;\n" +
            "}\n" +
            /* Modals: header X close; hide footer Close; larger centered Save */
            ".modal .modal-header {\n" +
            "  position: relative !important;\n" +
            "  padding-right: 44px !important;\n" +
            "}\n" +
            ".modal .modal-header .irongoo-modal-close {\n" +
            "  position: absolute !important;\n" +
            "  top: 50% !important;\n" +
            "  right: 10px !important;\n" +
            "  transform: translateY(-50%) !important;\n" +
            "  width: 28px !important;\n" +
            "  height: 28px !important;\n" +
            "  padding: 0 !important;\n" +
            "  margin: 0 !important;\n" +
            "  border: none !important;\n" +
            "  border-radius: 4px !important;\n" +
            "  background: transparent !important;\n" +
            "  color: inherit !important;\n" +
            "  font-size: 18px !important;\n" +
            "  line-height: 28px !important;\n" +
            "  text-align: center !important;\n" +
            "  cursor: pointer !important;\n" +
            "  opacity: .9 !important;\n" +
            "}\n" +
            ".modal .modal-header .irongoo-modal-close:hover {\n" +
            "  background: rgba(255,255,255,.12) !important;\n" +
            "  opacity: 1 !important;\n" +
            "}\n" +
            "#idqr .modal-header .irongoo-modal-close,\n" +
            ".modal .modal-header.alert .irongoo-modal-close {\n" +
            "  color: #ffffff !important;\n" +
            "}\n" +
            "#idqr .modal-header .irongoo-modal-close:hover,\n" +
            ".modal .modal-header.alert .irongoo-modal-close:hover {\n" +
            "  background: rgba(0,0,0,.18) !important;\n" +
            "}\n" +
            "#idqr .modal-footer {\n" +
            "  display: none !important;\n" +
            "}\n" +
            ".modal .modal-footer {\n" +
            "  display: flex !important;\n" +
            "  justify-content: center !important;\n" +
            "  align-items: center !important;\n" +
            "  position: relative !important;\n" +
            "  min-height: 60px !important;\n" +
            "  text-align: center !important;\n" +
            "}\n" +
            /* Hide Close only next to Save; keep Yes/No on confirm dialogs */
            ".modal .modal-footer:has(.btn-primary) > .btn-default[data-dismiss=\"modal\"],\n" +
            ".modal .modal-footer:has(.btn-primary) > button.btn-default[data-dismiss=\"modal\"] {\n" +
            "  display: none !important;\n" +
            "}\n" +
            ".modal .modal-footer .btn-danger {\n" +
            "  font-size: 15px !important;\n" +
            "  padding: 10px 32px !important;\n" +
            "  min-width: 120px !important;\n" +
            "  float: none !important;\n" +
            "  margin: 0 6px !important;\n" +
            "}\n" +
            ".modal .modal-footer:not(:has(.btn-primary)) .btn-default[data-dismiss=\"modal\"] {\n" +
            "  display: inline-block !important;\n" +
            "  font-size: 15px !important;\n" +
            "  padding: 10px 32px !important;\n" +
            "  min-width: 120px !important;\n" +
            "  float: none !important;\n" +
            "  margin: 0 6px !important;\n" +
            "}\n" +
            ".modal .modal-footer .btn-primary {\n" +
            "  font-size: 15px !important;\n" +
            "  padding: 10px 40px !important;\n" +
            "  min-width: 148px !important;\n" +
            "  float: none !important;\n" +
            "  margin: 0 auto !important;\n" +
            "  background-color: #E73F91 !important;\n" +
            "  background-image: none !important;\n" +
            "  border-color: #C9347A !important;\n" +
            "  color: #ffffff !important;\n" +
            "}\n" +
            ".modal .modal-footer .btn-primary:hover,\n" +
            ".modal .modal-footer .btn-primary:focus {\n" +
            "  background-color: #d63582 !important;\n" +
            "  border-color: #b82d70 !important;\n" +
            "  color: #ffffff !important;\n" +
            "}\n" +
            ".modal .modal-footer .pull-left,\n" +
            ".modal .modal-footer .btn-warning.pull-left,\n" +
            ".modal .modal-footer > .pull-left {\n" +
            "  position: absolute !important;\n" +
            "  left: 15px !important;\n" +
            "  top: 50% !important;\n" +
            "  transform: translateY(-50%) !important;\n" +
            "  float: none !important;\n" +
            "  margin: 0 !important;\n" +
            "}\n" +
            /* Shared-device checkboxes: green when checked */
            ".modal input[type=\"checkbox\"],\n" +
            "share-template input[type=\"checkbox\"],\n" +
            ".form-horizontal input[type=\"checkbox\"] {\n" +
            "  accent-color: #2ea043 !important;\n" +
            "}\n" +
            /* Remove buttons: red instead of warning yellow */
            ".modal .modal-footer .btn-warning,\n" +
            "#editFolder .modal-footer .btn-warning,\n" +
            "#editDevice .modal-footer .btn-warning,\n" +
            ".modal .modal-footer button[data-target*=\"remove-\"] {\n" +
            "  background-color: #d9534f !important;\n" +
            "  background-image: none !important;\n" +
            "  border-color: #c9302c !important;\n" +
            "  color: #ffffff !important;\n" +
            "}\n" +
            ".modal .modal-footer .btn-warning:hover,\n" +
            ".modal .modal-footer .btn-warning:focus,\n" +
            "#editFolder .modal-footer .btn-warning:hover,\n" +
            "#editDevice .modal-footer .btn-warning:hover,\n" +
            ".modal .modal-footer button[data-target*=\"remove-\"]:hover {\n" +
            "  background-color: #c9302c !important;\n" +
            "  border-color: #ac2925 !important;\n" +
            "  color: #ffffff !important;\n" +
            "}\n" +
            "#irongoo-copied-toast {\n" +
            "  position: fixed; left: 50%; top: 0; bottom: auto;\n" +
            "  transform: translateX(-50%) translateY(6px);\n" +
            "  background: #E73F91; color: #fff;\n" +
            "  padding: 8px 18px; border-radius: 6px;\n" +
            "  font-size: 13px; font-weight: 600; z-index: 20000;\n" +
            "  opacity: 0; pointer-events: none;\n" +
            "  transition: opacity .18s, transform .18s;\n" +
            "  box-shadow: 0 4px 16px rgba(0,0,0,.35);\n" +
            "  white-space: nowrap;\n" +
            "}\n" +
            "#irongoo-copied-toast.show {\n" +
            "  opacity: 1; transform: translateX(-50%) translateY(0);\n" +
            "}\n" +
            "#editDevice .help-block,\n" +
            "#editFolder .help-block {\n" +
            "  display: none !important;\n" +
            "}\n" +
            "#folder-general.active {\n" +
            "  display: flex !important;\n" +
            "  flex-direction: column !important;\n" +
            "}\n" +
            "#folder-general:not(.active) {\n" +
            "  display: none !important;\n" +
            "}\n" +
            "#folder-general > .form-group:has(#folderID) { order: 1 !important; }\n" +
            "#folder-general > .form-group:has(#folderPath) { order: 2 !important; }\n" +
            ".irongoo-folder-path-row {\n" +
            "  display: flex !important;\n" +
            "  align-items: stretch !important;\n" +
            "  gap: 8px !important;\n" +
            "  width: 100% !important;\n" +
            "}\n" +
            ".irongoo-folder-path-row #folderPath {\n" +
            "  flex: 1 1 auto !important;\n" +
            "  width: auto !important;\n" +
            "  min-width: 0 !important;\n" +
            "}\n" +
            "#folderPathBrowseButton {\n" +
            "  flex: 0 0 auto !important;\n" +
            "  margin: 0 !important;\n" +
            "  white-space: nowrap !important;\n" +
            "  align-self: stretch !important;\n" +
            "}\n" +
            "#folder-general > .form-group:has(#folderLabel) { order: 3 !important; }\n" +
            "#folder-general > .form-group:has(#folderGroup) { order: 4 !important; }\n" +
            "#folder-general > .irongoo-share-host { order: 5 !important; margin-top: 12px !important; }\n" +
            /* Drop "(N)" counts after section titles (Folders / Remote Devices) */
            "#folder_list > span:not(:first-of-type),\n" +
            "#irongoo-remote-header h3 > span:not(:first-of-type),\n" +
            "#irongoo-folder-header h3 > span:not(:first-of-type),\n" +
            "h3 > span[ng-if*=\"otherDevices\"],\n" +
            "h3 > span[ng-if*=\"folderList\"] {\n" +
            "  display: none !important;\n" +
            "}\n" +
            "#editFolder .nav-tabs > li:has(a[href=\"#folder-sharing\"]) {\n" +
            "  display: none !important;\n" +
            "}\n" +
            ".irongoo-share-heading {\n" +
            "  font-size: 14px !important;\n" +
            "  font-weight: 600 !important;\n" +
            "  color: #e6e6e6 !important;\n" +
            "  margin: 0 0 10px !important;\n" +
            "  display: flex !important;\n" +
            "  align-items: center !important;\n" +
            "  gap: 6px !important;\n" +
            "}\n" +
            ".irongoo-share-heading .fas,\n" +
            ".irongoo-share-heading .fa {\n" +
            "  color: #E73F91 !important;\n" +
            "}\n" +
            "[language-select] .dropdown-menu {\n" +
            "  column-count: 1 !important;\n" +
            "  columns: 1 !important;\n" +
            "  width: auto !important;\n" +
            "  min-width: 8em !important;\n" +
            "  max-width: 12em !important;\n" +
            "  right: 0 !important;\n" +
            "  left: auto !important;\n" +
            "  padding: 4px 0 !important;\n" +
            "}\n" +
            "[language-select] .dropdown-menu > li > a {\n" +
            "  padding: 6px 16px !important;\n" +
            "  white-space: nowrap !important;\n" +
            "}\n" +
            /* Syncthing navbar Help (question-circle); keep Actions (cog) */
            ".navbar .dropdown.action-menu:has(.fa-question-circle),\n" +
            ".navbar .dropdown.action-menu:has(.fas.fa-question-circle) {\n" +
            "  display: none !important;\n" +
            "}\n" +
            /* Nav actions: no dropdown-toggle (avoids #222 hover “shadow”); corners on hover only */
            ".navbar li.irongoo-nav-item > a.irongoo-nav-link {\n" +
            "  position: relative !important;\n" +
            "  display: block !important;\n" +
            "  background: transparent !important;\n" +
            "  background-image: none !important;\n" +
            "  border: none !important;\n" +
            "  box-shadow: none !important;\n" +
            "  text-shadow: none !important;\n" +
            "  outline: none !important;\n" +
            "}\n" +
            ".navbar li.irongoo-nav-item > a.irongoo-nav-link:hover,\n" +
            ".navbar li.irongoo-nav-item > a.irongoo-nav-link:focus,\n" +
            ".navbar li.irongoo-nav-item > a.irongoo-nav-link:active {\n" +
            "  background: transparent !important;\n" +
            "  background-image: none !important;\n" +
            "  border: none !important;\n" +
            "  box-shadow: none !important;\n" +
            "  text-shadow: none !important;\n" +
            "  color: #fff !important;\n" +
            "}\n" +
            ".navbar li.irongoo-nav-item .irongoo-corner {\n" +
            "  position: absolute !important;\n" +
            "  width: 8px !important;\n" +
            "  height: 8px !important;\n" +
            "  pointer-events: none !important;\n" +
            "  box-sizing: border-box !important;\n" +
            "  display: none !important;\n" +
            "}\n" +
            ".navbar li.irongoo-nav-item:hover > a.irongoo-nav-link .irongoo-corner {\n" +
            "  display: block !important;\n" +
            "}\n" +
            ".navbar li.irongoo-nav-item .irongoo-corner.tl {\n" +
            "  top: 6px !important; left: 4px !important;\n" +
            "  border-top: 2.5px solid #E73F91 !important;\n" +
            "  border-left: 2.5px solid #E73F91 !important;\n" +
            "}\n" +
            ".navbar li.irongoo-nav-item .irongoo-corner.tr {\n" +
            "  top: 6px !important; right: 4px !important;\n" +
            "  border-top: 2.5px solid #E73F91 !important;\n" +
            "  border-right: 2.5px solid #E73F91 !important;\n" +
            "}\n" +
            ".navbar li.irongoo-nav-item .irongoo-corner.bl {\n" +
            "  bottom: 6px !important; left: 4px !important;\n" +
            "  border-bottom: 2.5px solid #E73F91 !important;\n" +
            "  border-left: 2.5px solid #E73F91 !important;\n" +
            "}\n" +
            ".navbar li.irongoo-nav-item .irongoo-corner.br {\n" +
            "  bottom: 6px !important; right: 4px !important;\n" +
            "  border-bottom: 2.5px solid #E73F91 !important;\n" +
            "  border-right: 2.5px solid #E73F91 !important;\n" +
            "}\n" +
            /* Inline edit — underline only, no box */
            ".navbar p.navbar-text.irongoo-device-name {\n" +
            "  cursor: text !important;\n" +
            "  outline: none !important;\n" +
            "  box-shadow: none !important;\n" +
            "  border: none !important;\n" +
            "  border-radius: 0 !important;\n" +
            "  background: transparent !important;\n" +
            "}\n" +
            ".navbar p.navbar-text.irongoo-device-name:hover:not(.irongoo-name-editing) {\n" +
            "  color: #fff !important;\n" +
            "  box-shadow: inset 0 -1px 0 #E73F91 !important;\n" +
            "}\n" +
            ".navbar p.navbar-text.irongoo-name-editing,\n" +
            ".navbar p.navbar-text.irongoo-name-editing:focus,\n" +
            ".navbar p.navbar-text.irongoo-name-editing:focus-visible,\n" +
            ".navbar p.navbar-text.irongoo-name-editing:active {\n" +
            "  color: #fff !important;\n" +
            "  outline: none !important;\n" +
            "  outline-offset: 0 !important;\n" +
            "  border: none !important;\n" +
            "  border-radius: 0 !important;\n" +
            "  box-shadow: inset 0 -2px 0 #E73F91 !important;\n" +
            "  -webkit-box-shadow: inset 0 -2px 0 #E73F91 !important;\n" +
            "  background: transparent !important;\n" +
            "  -webkit-user-select: text !important;\n" +
            "  user-select: text !important;\n" +
            "  caret-color: #E73F91 !important;\n" +
            "}\n" +
            /* Hide Syncthing first-run prompts (usage report + GUI auth notice) */
            "#ur,\n" +
            "#ur.modal {\n" +
            "  display: none !important;\n" +
            "  visibility: hidden !important;\n" +
            "}\n" +
            /* Header toolbars only — do not change #folders / #devices list layout */
            "#irongoo-folder-header,\n" +
            "#irongoo-remote-header {\n" +
            "  display: flex !important;\n" +
            "  align-items: center !important;\n" +
            "  justify-content: flex-start !important;\n" +
            "  flex-wrap: wrap !important;\n" +
            "  gap: 8px 10px !important;\n" +
            "  margin: 20px 0 10px !important;\n" +
            "  width: 100% !important;\n" +
            "}\n" +
            "#irongoo-folder-header > h3,\n" +
            "#irongoo-remote-header > h3 {\n" +
            "  margin: 0 !important;\n" +
            "  flex: 0 0 auto !important;\n" +
            "}\n" +
            "#irongoo-folder-header .irongoo-folder-actions,\n" +
            "#irongoo-remote-header .irongoo-remote-actions {\n" +
            "  display: inline-flex !important;\n" +
            "  flex-wrap: wrap !important;\n" +
            "  align-items: center !important;\n" +
            "  gap: 6px !important;\n" +
            "  float: none !important;\n" +
            "  margin: 0 !important;\n" +
            "  flex: 0 0 auto !important;\n" +
            "}\n" +
            /* Hide empty group titles so lists align */
            ".irongoo-col-folders > h4:empty,\n" +
            ".irongoo-col-remotes > h4:empty,\n" +
            ".irongoo-col-folders h4.irongoo-empty-group,\n" +
            ".irongoo-col-remotes h4.irongoo-empty-group {\n" +
            "  display: none !important;\n" +
            "  margin: 0 !important;\n" +
            "  height: 0 !important;\n" +
            "}\n" +
            /* Card header: match device row height; icon + name left, status far right */
            "[id^=\"folders\"] > .panel > .panel-heading,\n" +
            "[id^=\"devices\"] > .panel > .panel-heading {\n" +
            "  display: block !important;\n" +
            "  position: relative !important;\n" +
            "  z-index: 2 !important;\n" +
            "  width: 100% !important;\n" +
            "  height: 42px !important;\n" +
            "  min-height: 42px !important;\n" +
            "  max-height: 42px !important;\n" +
            "  padding: 8px 12px !important;\n" +
            "  box-sizing: border-box !important;\n" +
            "  text-align: left !important;\n" +
            "  overflow: hidden !important;\n" +
            "}\n" +
            "[id^=\"folders\"] > .panel > .panel-heading .panel-title,\n" +
            "[id^=\"devices\"] > .panel > .panel-heading .panel-title {\n" +
            "  display: flex !important;\n" +
            "  align-items: center !important;\n" +
            "  width: 100% !important;\n" +
            "  height: 100% !important;\n" +
            "  margin: 0 !important;\n" +
            "  line-height: 26px !important;\n" +
            "}\n" +
            "[id^=\"folders\"] .panel-heading .panel-icon,\n" +
            "[id^=\"devices\"] .panel-heading .panel-icon,\n" +
            "[id^=\"devices\"] .panel-heading identicon.panel-icon {\n" +
            "  order: 1 !important;\n" +
            "  flex: 0 0 auto !important;\n" +
            "  float: none !important;\n" +
            "}\n" +
            "[id^=\"folders\"] .panel-heading .panel-title-text,\n" +
            "[id^=\"devices\"] .panel-heading .panel-title-text {\n" +
            "  order: 2 !important;\n" +
            "  flex: 1 1 auto !important;\n" +
            "  min-width: 0 !important;\n" +
            "  float: none !important;\n" +
            "  text-align: left !important;\n" +
            "}\n" +
            "[id^=\"folders\"] .panel-heading .panel-status,\n" +
            "[id^=\"devices\"] .panel-heading .panel-status {\n" +
            "  order: 3 !important;\n" +
            "  float: none !important;\n" +
            "  position: static !important;\n" +
            "  margin-left: auto !important;\n" +
            "  margin-right: 0 !important;\n" +
            "  flex: 0 0 auto !important;\n" +
            "  text-align: right !important;\n" +
            "  max-width: 45% !important;\n" +
            "  overflow: hidden !important;\n" +
            "  text-overflow: ellipsis !important;\n" +
            "  white-space: nowrap !important;\n" +
            "  z-index: 1 !important;\n" +
            "}\n" +
            "[id^=\"devices\"] .panel-heading .panel-status .reception {\n" +
            "  display: none !important;\n" +
            "}\n" +
            "#irongoo-folder-header .irongoo-folder-actions > button[ng-click=\"addFolder()\"] { order: 1; }\n" +
            "#irongoo-folder-header .irongoo-folder-actions > button[ng-click=\"rescanAllFolders()\"] { order: 2; }\n" +
            "#irongoo-folder-header .irongoo-folder-actions > button[ng-click=\"setAllFoldersPause(true)\"] { order: 3; }\n" +
            "#irongoo-folder-header .irongoo-folder-actions > button[ng-click=\"setAllFoldersPause(false)\"] { order: 4; }\n" +
            /* ST 2.0: #folders/#devices; ST 2.1+: #folders-N / #devices-N */
            "#folders,\n" +
            "#devices,\n" +
            "[id^=\"folders\"],\n" +
            "[id^=\"devices\"] {\n" +
            "  width: 100% !important;\n" +
            "  max-width: 100% !important;\n" +
            "  clear: both !important;\n" +
            "  float: none !important;\n" +
            "}\n" +
            ".clearfix.irongoo-folder-actions-host,\n" +
            ".form-group.irongoo-remote-actions-host { display: none !important; }\n" +
            /* Main layout: Folders | Remotes on top; This Device full-width below */
            ".row.irongoo-layout-row {\n" +
            "  display: flex !important;\n" +
            "  flex-wrap: wrap !important;\n" +
            "}\n" +
            "#irongoo-this-device-wrap {\n" +
            "  flex: 0 0 100% !important;\n" +
            "  max-width: 100% !important;\n" +
            "  width: 100% !important;\n" +
            "  float: none !important;\n" +
            "  padding-left: 15px !important;\n" +
            "  padding-right: 15px !important;\n" +
            "  box-sizing: border-box !important;\n" +
            "  margin: 8px 0 0 !important;\n" +
            "  order: 2 !important;\n" +
            "}\n" +
            "#irongoo-this-device-wrap > h3 { margin: 20px 0 10px !important; }\n" +
            "#irongoo-this-device-wrap > .panel { margin-bottom: 0 !important; }\n" +
            ".irongoo-stats-grid {\n" +
            "  display: grid !important;\n" +
            "  grid-template-columns: 1fr 1fr !important;\n" +
            "  gap: 0 28px !important;\n" +
            "  width: 100% !important;\n" +
            "}\n" +
            ".irongoo-stats-grid > table { width: 100% !important; margin: 0 !important; }\n" +
            "@media (max-width: 767px) {\n" +
            "  .irongoo-stats-grid { grid-template-columns: 1fr !important; gap: 0 !important; }\n" +
            "}\n" +
            ".row.irongoo-layout-row > .irongoo-col-folders,\n" +
            ".row.irongoo-layout-row > .irongoo-col-remotes {\n" +
            "  float: none !important;\n" +
            "  width: 50% !important;\n" +
            "  max-width: 50% !important;\n" +
            "  flex: 1 1 0 !important;\n" +
            "  min-width: 0 !important;\n" +
            "  box-sizing: border-box !important;\n" +
            "  order: 1 !important;\n" +
            "  padding-left: 15px !important;\n" +
            "  padding-right: 15px !important;\n" +
            "}\n" +
            "@media (max-width: 991px) {\n" +
            "  .row.irongoo-layout-row > .irongoo-col-folders,\n" +
            "  .row.irongoo-layout-row > .irongoo-col-remotes {\n" +
            "    width: 100% !important;\n" +
            "    max-width: 100% !important;\n" +
            "    flex: 0 0 100% !important;\n" +
            "  }\n" +
            "}\n" +
            /* Equal-width list cards */
            "[id^=\"folders\"] > .panel,\n" +
            "[id^=\"devices\"] > .panel {\n" +
            "  position: relative !important;\n" +
            "  overflow: visible !important;\n" +
            "  width: 100% !important;\n" +
            "  max-width: 100% !important;\n" +
            "  margin-left: 0 !important;\n" +
            "  margin-right: 0 !important;\n" +
            "  box-sizing: border-box !important;\n" +
            "}\n" +
            /* Detail popup: only .in (never .collapsing — that caused ~350ms dismiss lag) */
            "[id^=\"folders\"] > .panel > .panel-collapse.collapse.in,\n" +
            "[id^=\"devices\"] > .panel > .panel-collapse.collapse.in {\n" +
            "  position: absolute !important;\n" +
            "  left: 0 !important;\n" +
            "  right: 0 !important;\n" +
            "  /* Clear the header so status (e.g. Unshared) never covers Folder Path */\n" +
            "  top: calc(100% + 10px) !important;\n" +
            "  width: 100% !important;\n" +
            "  max-width: 100% !important;\n" +
            "  z-index: 40 !important;\n" +
            "  display: block !important;\n" +
            "  float: none !important;\n" +
            "  height: auto !important;\n" +
            "  max-height: 70vh !important;\n" +
            "  overflow-x: hidden !important;\n" +
            "  overflow-y: auto !important;\n" +
            "  margin: 0 !important;\n" +
            "  padding: 0 !important;\n" +
            "  box-sizing: border-box !important;\n" +
            "  /* Solid fill — transparent body used to show pink Listeners text through as “dots” */\n" +
            "  background: #252526 !important;\n" +
            "  background-clip: padding-box !important;\n" +
            "  isolation: isolate !important;\n" +
            "  border: 1px solid #333333 !important;\n" +
            "  border-radius: 4px !important;\n" +
            "  box-shadow: 0 8px 24px rgba(0,0,0,.55) !important;\n" +
            "  -webkit-transition: none !important;\n" +
            "  transition: none !important;\n" +
            "}\n" +
            "[id^=\"folders\"] > .panel > .panel-collapse.collapse.in > .panel-body,\n" +
            "[id^=\"devices\"] > .panel > .panel-collapse.collapse.in > .panel-body {\n" +
            "  padding-top: 10px !important;\n" +
            "  background: #252526 !important;\n" +
            "}\n" +
            "[id^=\"folders\"] > .panel > .panel-collapse.collapse.in .table,\n" +
            "[id^=\"folders\"] > .panel > .panel-collapse.collapse.in .table > tbody > tr > th,\n" +
            "[id^=\"folders\"] > .panel > .panel-collapse.collapse.in .table > tbody > tr > td,\n" +
            "[id^=\"devices\"] > .panel > .panel-collapse.collapse.in .table,\n" +
            "[id^=\"devices\"] > .panel > .panel-collapse.collapse.in .table > tbody > tr > th,\n" +
            "[id^=\"devices\"] > .panel > .panel-collapse.collapse.in .table > tbody > tr > td {\n" +
            "  background-color: #252526 !important;\n" +
            "}\n" +
            "[id^=\"folders\"] > .panel > .panel-collapse.collapse.in .table-striped > tbody > tr:nth-of-type(odd) > th,\n" +
            "[id^=\"folders\"] > .panel > .panel-collapse.collapse.in .table-striped > tbody > tr:nth-of-type(odd) > td,\n" +
            "[id^=\"devices\"] > .panel > .panel-collapse.collapse.in .table-striped > tbody > tr:nth-of-type(odd) > th,\n" +
            "[id^=\"devices\"] > .panel > .panel-collapse.collapse.in .table-striped > tbody > tr:nth-of-type(odd) > td {\n" +
            "  background-color: #222222 !important;\n" +
            "}\n" +
            "[id^=\"folders\"] > .panel > .panel-collapse.collapse.in .table > tbody > tr:first-child > th,\n" +
            "[id^=\"folders\"] > .panel > .panel-collapse.collapse.in .table > tbody > tr:first-child > td,\n" +
            "[id^=\"devices\"] > .panel > .panel-collapse.collapse.in .table > tbody > tr:first-child > th,\n" +
            "[id^=\"devices\"] > .panel > .panel-collapse.collapse.in .table > tbody > tr:first-child > td {\n" +
            "  padding-top: 8px !important;\n" +
            "}\n" +
            "[id^=\"folders\"] > .panel > .panel-collapse.collapsing,\n" +
            "[id^=\"devices\"] > .panel > .panel-collapse.collapsing {\n" +
            "  display: none !important;\n" +
            "  height: 0 !important;\n" +
            "  overflow: hidden !important;\n" +
            "  -webkit-transition: none !important;\n" +
            "  transition: none !important;\n" +
            "}\n" +
            "[id^=\"folders\"] > .panel > .panel-collapse .panel-body,\n" +
            "[id^=\"devices\"] > .panel > .panel-collapse .panel-body {\n" +
            "  background: #252526 !important;\n" +
            "}\n" +
            "[id^=\"folders\"] > .panel > .panel-collapse .panel-footer,\n" +
            "[id^=\"devices\"] > .panel > .panel-collapse .panel-footer {\n" +
            "  background: #252526 !important;\n" +
            "  border-top: 1px solid #333333 !important;\n" +
            "}\n" +
            "[id^=\"folders\"] > .panel > .panel-heading.irongoo-float-active,\n" +
            "[id^=\"devices\"] > .panel > .panel-heading.irongoo-float-active {\n" +
            "  border-color: #E73F91 !important;\n" +
            "}\n" +
            /* Tooltips must sit above clipped float panels */
            ".tooltip {\n" +
            "  z-index: 10000 !important;\n" +
            "}\n" +
            ".tooltip .tooltip-inner {\n" +
            "  max-width: 320px !important;\n" +
            "  text-align: left !important;\n" +
            "}\n" +
            /* Remote device: replace identicon with users icon */
            "[id^=\"devices\"] identicon.panel-icon.irongoo-users-icon {\n" +
            "  display: inline-flex !important;\n" +
            "  align-items: center !important;\n" +
            "  justify-content: center !important;\n" +
            "  background: transparent !important;\n" +
            "}\n" +
            "[id^=\"devices\"] identicon.panel-icon.irongoo-users-icon > canvas,\n" +
            "[id^=\"devices\"] identicon.panel-icon.irongoo-users-icon > img,\n" +
            "[id^=\"devices\"] identicon.panel-icon.irongoo-users-icon > svg:not(.irongoo-users-svg) {\n" +
            "  display: none !important;\n" +
            "}\n" +
            "[id^=\"devices\"] identicon.panel-icon.irongoo-users-icon > svg.irongoo-users-svg {\n" +
            "  display: block !important;\n" +
            "  width: 1.35em !important;\n" +
            "  height: 1.35em !important;\n" +
            "  fill: currentColor !important;\n" +
            "  color: inherit !important;\n" +
            "}\n" +
            /* Folder list: replace FA folder icons with open-folder SVG */
            "[id^=\"folders\"] .panel-heading .panel-icon.irongoo-folder-icon {\n" +
            "  display: inline-flex !important;\n" +
            "  align-items: center !important;\n" +
            "  justify-content: center !important;\n" +
            "  cursor: pointer !important;\n" +
            "  position: relative !important;\n" +
            "  z-index: 2 !important;\n" +
            "  box-shadow: none !important;\n" +
            "}\n" +
            "[id^=\"folders\"] .panel-heading .panel-icon.irongoo-folder-icon > .fa,\n" +
            "[id^=\"folders\"] .panel-heading .panel-icon.irongoo-folder-icon > .fas,\n" +
            "[id^=\"folders\"] .panel-heading .panel-icon.irongoo-folder-icon > .far,\n" +
            "[id^=\"folders\"] .panel-heading .panel-icon.irongoo-folder-icon > span {\n" +
            "  display: none !important;\n" +
            "}\n" +
            "[id^=\"folders\"] .panel-heading .panel-icon.irongoo-folder-icon > svg.irongoo-folder-svg {\n" +
            "  display: block !important;\n" +
            "  width: 1.35em !important;\n" +
            "  height: 1.35em !important;\n" +
            "  fill: currentColor !important;\n" +
            "  pointer-events: none !important;\n" +
            "  transition: filter .15s ease !important;\n" +
            "  filter: none !important;\n" +
            "}\n" +
            /* Edge-only white glow (same strength family as input focus rgba .6) */
            "[id^=\"folders\"] .panel-heading .panel-icon.irongoo-folder-icon:hover > svg.irongoo-folder-svg {\n" +
            "  filter: drop-shadow(0 0 0.5px rgba(255,255,255,.95))\n" +
            "          drop-shadow(0 0 2px rgba(255,255,255,.75))\n" +
            "          drop-shadow(0 0 6px rgba(255,255,255,.6)) !important;\n" +
            "}\n" +
            "[id^=\"folders\"] .panel-heading .panel-icon.irongoo-folder-icon:active > svg.irongoo-folder-svg {\n" +
            "  filter: drop-shadow(0 0 0.5px rgba(255,255,255,.85))\n" +
            "          drop-shadow(0 0 4px rgba(255,255,255,.5)) !important;\n" +
            "}\n" +
            /* This Device: home-computer icon */
            "#irongoo-this-device-wrap identicon.panel-icon.irongoo-home-icon,\n" +
            "button[data-target=\"#device-this\"] identicon.panel-icon.irongoo-home-icon {\n" +
            "  display: inline-flex !important;\n" +
            "  align-items: center !important;\n" +
            "  justify-content: center !important;\n" +
            "  background: transparent !important;\n" +
            "}\n" +
            "#irongoo-this-device-wrap identicon.panel-icon.irongoo-home-icon > canvas,\n" +
            "#irongoo-this-device-wrap identicon.panel-icon.irongoo-home-icon > img,\n" +
            "#irongoo-this-device-wrap identicon.panel-icon.irongoo-home-icon > svg:not(.irongoo-home-svg),\n" +
            "button[data-target=\"#device-this\"] identicon.panel-icon.irongoo-home-icon > canvas,\n" +
            "button[data-target=\"#device-this\"] identicon.panel-icon.irongoo-home-icon > img,\n" +
            "button[data-target=\"#device-this\"] identicon.panel-icon.irongoo-home-icon > svg:not(.irongoo-home-svg) {\n" +
            "  display: none !important;\n" +
            "}\n" +
            "#irongoo-this-device-wrap identicon.panel-icon.irongoo-home-icon > svg.irongoo-home-svg,\n" +
            "button[data-target=\"#device-this\"] identicon.panel-icon.irongoo-home-icon > svg.irongoo-home-svg {\n" +
            "  display: block !important;\n" +
            "  width: 1.35em !important;\n" +
            "  height: 1.35em !important;\n" +
            "  fill: currentColor !important;\n" +
            "  color: inherit !important;\n" +
            "}\n" +
            /* This Device: hide Identification + Version rows for now */
            "#irongoo-this-device-wrap tr.irongoo-hide-row,\n" +
            "#device-this tr.irongoo-hide-row {\n" +
            "  display: none !important;\n" +
            "}\n";

        // Loaded for every theme (including external browsers) via theme.css @import
        private const string LanguageSelectDirectiveJs =
            "angular.module('syncthing.core')\n" +
            "    .directive('languageSelect', function (LocaleService) {\n" +
            "        'use strict';\n" +
            "        var allow = function (code) {\n" +
            "            code = (code || '').toLowerCase();\n" +
            "            return code === 'en' || code === 'zh' || code === 'zh-cn' || code === 'zh-hans' ||\n" +
            "                code.indexOf('zh-hans') === 0 || code.indexOf('zh-cn') === 0;\n" +
            "        };\n" +
            "        var displayName = function (code, fallback) {\n" +
            "            code = (code || '').toLowerCase();\n" +
            "            if (code === 'en' || code.indexOf('en-') === 0) return 'English';\n" +
            "            if (code.indexOf('zh') === 0) return '简体中文';\n" +
            "            return fallback;\n" +
            "        };\n" +
            "        return {\n" +
            "            restrict: 'EA',\n" +
            "            template:\n" +
            "                '<a ng-if=\"visible\" href=\"#\" class=\"dropdown-toggle\" data-toggle=\"dropdown\" aria-expanded=\"false\">" +
            "<span class=\"fas fa-globe\"></span><span class=\"hidden-xs\">&nbsp;{{displayCurrent}}</span> <span class=\"caret\"></span></a>' +\n" +
            "                '<ul ng-if=\"visible\" class=\"dropdown-menu\">' +\n" +
            "                '<li ng-repeat=\"name in localesNamesInvKeys\" ng-class=\"{active: localesNamesInv[name]==currentLocale}\">' +\n" +
            "                '<a href=\"#\" data-ng-click=\"changeLanguage(localesNamesInv[name])\">{{name}}</a>' +\n" +
            "                '</li>' +\n" +
            "                '</ul>',\n" +
            "            link: function ($scope) {\n" +
            "                var availableLocales = LocaleService.getAvailableLocales();\n" +
            "                var localeNames = LocaleService.getLocalesDisplayNames();\n" +
            "                var availableLocaleNames = {};\n" +
            "                for (var i = 0; i < availableLocales.length; i++) {\n" +
            "                    var a = availableLocales[i];\n" +
            "                    if (!allow(a)) continue;\n" +
            "                    availableLocaleNames[a] = displayName(a, localeNames[a] || ('[' + a + ']'));\n" +
            "                }\n" +
            "                $scope.localesNames = availableLocaleNames;\n" +
            "                var invert = function (obj) {\n" +
            "                    var new_obj = {};\n" +
            "                    for (var prop in obj) {\n" +
            "                        if (obj.hasOwnProperty(prop)) new_obj[obj[prop]] = prop;\n" +
            "                    }\n" +
            "                    return new_obj;\n" +
            "                };\n" +
            "                $scope.localesNamesInv = invert($scope.localesNames);\n" +
            "                $scope.localesNamesInvKeys = Object.keys($scope.localesNamesInv).sort();\n" +
            "                $scope.visible = !!$scope.localesNames['en'] || Object.keys($scope.localesNames).length > 0;\n" +
            "                $scope.displayCurrent = 'English';\n" +
            "                var remove_watch = $scope.$watch(LocaleService.getCurrentLocale, function (newValue) {\n" +
            "                    if (newValue) {\n" +
            "                        $scope.currentLocale = newValue;\n" +
            "                        $scope.displayCurrent = displayName(newValue, $scope.localesNames[newValue] || 'English');\n" +
            "                        remove_watch();\n" +
            "                    }\n" +
            "                });\n" +
            "                $scope.changeLanguage = function (locale) {\n" +
            "                    LocaleService.useLocale(locale, true);\n" +
            "                    $scope.currentLocale = locale;\n" +
            "                    $scope.displayCurrent = displayName(locale, $scope.localesNames[locale] || locale);\n" +
            "                };\n" +
            "            }\n" +
            "        };\n" +
            "    });\n";

        private const string IrongooExtrasJs =
            "(function(){\n" +
            "  if (window.__irongooExtrasV === 83) return; window.__irongooExtrasV = 83;\n" +
            "  if (typeof window.__irongooShowThisDevice === 'undefined') window.__irongooShowThisDevice = true;\n" +
            "  window.__irongooFloatWired = false;\n" +
            "  try{\n" +
            "    if(window.jQuery && jQuery.fn && jQuery.fn.collapse && jQuery.fn.collapse.Constructor)\n" +
            "      jQuery.fn.collapse.Constructor.TRANSITION_DURATION = 0;\n" +
            "  }catch(e){}\n" +
            "  // Load brand CSS in external browsers too (embedded Cef also injects; idempotent).\n" +
            "  (function ensureBrandCss(){\n" +
            "    var l = document.getElementById('irongoo-theme-link');\n" +
            "    if (!l) {\n" +
            "      l = document.createElement('link');\n" +
            "      l.id = 'irongoo-theme-link'; l.rel = 'stylesheet';\n" +
            "      (document.head || document.documentElement).appendChild(l);\n" +
            "    }\n" +
            "    l.href = 'assets/css/irongoo.css?v=83';\n" +
            "  })();\n" +
            "  var IRONGOO_USERS_SVG = '<svg class=\"irongoo-users-svg\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M12 10C14.2091 10 16 8.20914 16 6 16 3.79086 14.2091 2 12 2 9.79086 2 8 3.79086 8 6 8 8.20914 9.79086 10 12 10ZM5.5 13C6.88071 13 8 11.8807 8 10.5 8 9.11929 6.88071 8 5.5 8 4.11929 8 3 9.11929 3 10.5 3 11.8807 4.11929 13 5.5 13ZM21 10.5C21 11.8807 19.8807 13 18.5 13 17.1193 13 16 11.8807 16 10.5 16 9.11929 17.1193 8 18.5 8 19.8807 8 21 9.11929 21 10.5ZM12 11C14.7614 11 17 13.2386 17 16V22H7V16C7 13.2386 9.23858 11 12 11ZM5 15.9999C5 15.307 5.10067 14.6376 5.28818 14.0056L5.11864 14.0204C3.36503 14.2104 2 15.6958 2 17.4999V21.9999H5V15.9999ZM22 21.9999V17.4999C22 15.6378 20.5459 14.1153 18.7118 14.0056 18.8993 14.6376 19 15.307 19 15.9999V21.9999H22Z\"></path></svg>';\n" +
            "  var IRONGOO_FOLDER_SVG = '<svg class=\"irongoo-folder-svg\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M3 21C2.44772 21 2 20.5523 2 20V4C2 3.44772 2.44772 3 3 3H10.4142L12.4142 5H20C20.5523 5 21 5.44772 21 6V9H4V18.996L6 11H22.5L20.1894 20.2425C20.0781 20.6877 19.6781 21 19.2192 21H3Z\"></path></svg>';\n" +
            "  var IRONGOO_HOME_SVG = '<svg class=\"irongoo-home-svg\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M20.7999 9H12V17H11V21.0001H5C4.44772 21.0001 4 20.5524 4 20.0001V11.0001L1 11.0001L11.3273 1.61162C11.7087 1.26488 12.2913 1.26488 12.6727 1.61162L20.7999 9ZM14 11H23V18H14V11ZM13 21H24V19H13V21Z\"></path></svg>';\n" +
            "  function ensureRemoteUsersIcons(){\n" +
            "    try{\n" +
            "      var icons=document.querySelectorAll('[id^=\"devices\"] identicon.panel-icon');\n" +
            "      for(var i=0;i<icons.length;i++){\n" +
            "        var el=icons[i];\n" +
            "        if(el.closest && el.closest('button[data-target=\"#device-this\"]')) continue;\n" +
            "        el.classList.add('irongoo-users-icon');\n" +
            "        if(!el.querySelector('svg.irongoo-users-svg')){\n" +
            "          var tmp=document.createElement('span');\n" +
            "          tmp.innerHTML=IRONGOO_USERS_SVG;\n" +
            "          el.appendChild(tmp.firstChild);\n" +
            "        }\n" +
            "        var kids=el.children;\n" +
            "        for(var k=0;k<kids.length;k++){\n" +
            "          if(kids[k].classList && kids[k].classList.contains('irongoo-users-svg')) continue;\n" +
            "          kids[k].style.display='none';\n" +
            "        }\n" +
            "      }\n" +
            "    }catch(e){}\n" +
            "  }\n" +
            "  function ensureThisDeviceIcon(){\n" +
            "    try{\n" +
            "      var icons=document.querySelectorAll('button[data-target=\"#device-this\"] identicon.panel-icon, #irongoo-this-device-wrap identicon.panel-icon');\n" +
            "      for(var i=0;i<icons.length;i++){\n" +
            "        var el=icons[i];\n" +
            "        el.classList.remove('irongoo-users-icon');\n" +
            "        el.classList.add('irongoo-home-icon');\n" +
            "        var oldUsers=el.querySelector('svg.irongoo-users-svg');\n" +
            "        if(oldUsers && oldUsers.parentNode) oldUsers.parentNode.removeChild(oldUsers);\n" +
            "        if(!el.querySelector('svg.irongoo-home-svg')){\n" +
            "          var tmp=document.createElement('span');\n" +
            "          tmp.innerHTML=IRONGOO_HOME_SVG;\n" +
            "          el.appendChild(tmp.firstChild);\n" +
            "        }\n" +
            "        var kids=el.children;\n" +
            "        for(var k=0;k<kids.length;k++){\n" +
            "          if(kids[k].classList && kids[k].classList.contains('irongoo-home-svg')) continue;\n" +
            "          kids[k].style.display='none';\n" +
            "        }\n" +
            "      }\n" +
            "    }catch(e){}\n" +
            "  }\n" +
            "  function folderInfoFromIcon(el){\n" +
            "    var info={id:'', path:''};\n" +
            "    if(!el) return info;\n" +
            "    info.id=el.getAttribute('data-folder-id')||'';\n" +
            "    info.path=el.getAttribute('data-folder-path')||'';\n" +
            "    try{\n" +
            "      if(window.angular){\n" +
            "        var sc=angular.element(el).scope();\n" +
            "        while(sc){\n" +
            "          if(sc.folder){\n" +
            "            if(sc.folder.id) info.id=sc.folder.id;\n" +
            "            if(sc.folder.path) info.path=sc.folder.path;\n" +
            "            break;\n" +
            "          }\n" +
            "          sc=sc.$parent;\n" +
            "        }\n" +
            "      }\n" +
            "    }catch(e){}\n" +
            "    if(!info.id){\n" +
            "      try{\n" +
            "        var panel=el.closest ? el.closest('.panel') : null;\n" +
            "        if(panel){\n" +
            "          var tip=panel.querySelector('.panel-title-text [data-original-title], .panel-title-text [title]');\n" +
            "          if(tip){\n" +
            "            var t=tip.getAttribute('data-original-title')||tip.getAttribute('title')||'';\n" +
            "            if(t) info.id=t;\n" +
            "          }\n" +
            "          var idCell=panel.querySelector('td.no-overflow-ellipse');\n" +
            "          if(!info.id && idCell && (idCell.textContent||'').trim()) info.id=idCell.textContent.trim();\n" +
            "          // Folder Path row value\n" +
            "          if(!info.path){\n" +
            "            var rows=panel.querySelectorAll('.panel-collapse td.text-right, .panel-body td.text-right');\n" +
            "            for(var r=0;r<rows.length;r++){\n" +
            "              var txt=(rows[r].textContent||'').trim();\n" +
            "              if(/^[A-Za-z]:[\\\\/]/.test(txt) || txt.indexOf('\\\\\\\\')===0 || txt.indexOf('/')===0){ info.path=txt; break; }\n" +
            "            }\n" +
            "          }\n" +
            "        }\n" +
            "      }catch(e2){}\n" +
            "    }\n" +
            "    return info;\n" +
            "  }\n" +
            "  function openLocalFolderFromIcon(el){\n" +
            "    var info=folderInfoFromIcon(el);\n" +
            "    if(info.id) el.setAttribute('data-folder-id', info.id);\n" +
            "    if(info.path) el.setAttribute('data-folder-path', info.path);\n" +
            "    var go=function(){\n" +
            "      try{\n" +
            "        var cb=window.callbackObject;\n" +
            "        if(cb){\n" +
            "          if(info.path && typeof cb.openFolderPath==='function'){ cb.openFolderPath(info.path); return; }\n" +
            "          if(info.id && typeof cb.openFolder==='function'){ cb.openFolder(info.id); return; }\n" +
            "        }\n" +
            "      }catch(e){}\n" +
            "      try{\n" +
            "        if(window.CefSharp && CefSharp.BindObjectAsync){\n" +
            "          CefSharp.BindObjectAsync('callbackObject').then(function(){\n" +
            "            var cb2=window.callbackObject;\n" +
            "            if(!cb2) return;\n" +
            "            if(info.path && cb2.openFolderPath) cb2.openFolderPath(info.path);\n" +
            "            else if(info.id && cb2.openFolder) cb2.openFolder(info.id);\n" +
            "          });\n" +
            "        }\n" +
            "      }catch(e2){}\n" +
            "    };\n" +
            "    setTimeout(go, 0);\n" +
            "  }\n" +
            "  function ensureFolderIcons(){\n" +
            "    try{\n" +
            "      var icons=document.querySelectorAll('[id^=\"folders\"] .panel-heading .panel-icon');\n" +
            "      for(var i=0;i<icons.length;i++){\n" +
            "        var el=icons[i];\n" +
            "        el.classList.add('irongoo-folder-icon');\n" +
            "        if(!el.querySelector('svg.irongoo-folder-svg')){\n" +
            "          var tmp=document.createElement('span');\n" +
            "          tmp.innerHTML=IRONGOO_FOLDER_SVG;\n" +
            "          el.appendChild(tmp.firstChild);\n" +
            "        }\n" +
            "        var kids=el.children;\n" +
            "        for(var k=0;k<kids.length;k++){\n" +
            "          if(kids[k].classList && kids[k].classList.contains('irongoo-folder-svg')) continue;\n" +
            "          kids[k].style.display='none';\n" +
            "        }\n" +
            "        var info=folderInfoFromIcon(el);\n" +
            "        if(info.id) el.setAttribute('data-folder-id', info.id);\n" +
            "        if(info.path) el.setAttribute('data-folder-path', info.path);\n" +
            "        var tip=isZh()?'打开本地文件夹':'Open local folder';\n" +
            "        if(el.getAttribute('title')!==tip) el.setAttribute('title', tip);\n" +
            "        el.setAttribute('data-irongoo-open','1');\n" +
            "      }\n" +
            "    }catch(e){}\n" +
            "  }\n" +
            "  function wireFolderIconOpenOnly(){\n" +
            "    if(window.__irongooFolderIconWired) return;\n" +
            "    window.__irongooFolderIconWired=true;\n" +
            "    function isFolderIconEvent(ev){\n" +
            "      var t=ev && ev.target;\n" +
            "      if(!t || !t.closest) return null;\n" +
            "      return t.closest('[id^=\"folders\"] .panel-heading .panel-icon.irongoo-folder-icon, [id^=\"folders\"] .irongoo-folder-icon');\n" +
            "    }\n" +
            "    function blockCollapse(ev){\n" +
            "      var icon=isFolderIconEvent(ev);\n" +
            "      if(!icon) return false;\n" +
            "      if(ev.button!=null && ev.button!==0) return false;\n" +
            "      ev.preventDefault();\n" +
            "      ev.stopPropagation();\n" +
            "      if(ev.stopImmediatePropagation) ev.stopImmediatePropagation();\n" +
            "      window.__irongooBlockCollapse=Date.now();\n" +
            "      return icon;\n" +
            "    }\n" +
            "    function openOnce(icon){\n" +
            "      // One open per gesture (pointerdown/mousedown/click + dual handlers)\n" +
            "      var now=Date.now();\n" +
            "      if(window.__irongooOpenAt && (now-window.__irongooOpenAt)<700) return;\n" +
            "      window.__irongooOpenAt=now;\n" +
            "      openLocalFolderFromIcon(icon);\n" +
            "    }\n" +
            "    // Block collapse on press; open only once on click\n" +
            "    document.addEventListener('pointerdown', function(ev){ blockCollapse(ev); }, true);\n" +
            "    document.addEventListener('mousedown', function(ev){ blockCollapse(ev); }, true);\n" +
            "    document.addEventListener('mouseup', function(ev){ blockCollapse(ev); }, true);\n" +
            "    document.addEventListener('click', function(ev){\n" +
            "      var icon=blockCollapse(ev);\n" +
            "      if(icon) openOnce(icon);\n" +
            "    }, true);\n" +
            "    document.addEventListener('show.bs.collapse', function(ev){\n" +
            "      if(window.__irongooBlockCollapse && (Date.now()-window.__irongooBlockCollapse)<800){\n" +
            "        ev.preventDefault();\n" +
            "        ev.stopPropagation();\n" +
            "        if(ev.stopImmediatePropagation) ev.stopImmediatePropagation();\n" +
            "      }\n" +
            "    }, true);\n" +
            "    document.addEventListener('hide.bs.collapse', function(ev){\n" +
            "      if(window.__irongooBlockCollapse && (Date.now()-window.__irongooBlockCollapse)<800){\n" +
            "        ev.preventDefault();\n" +
            "        ev.stopPropagation();\n" +
            "        if(ev.stopImmediatePropagation) ev.stopImmediatePropagation();\n" +
            "      }\n" +
            "    }, true);\n" +
            "  }\n" +
            "  function hideUrModal(){\n" +
            "    try{\n" +
            "      var ur=document.getElementById('ur');\n" +
            "      if(ur){ ur.classList.remove('in','show'); ur.style.display='none'; ur.setAttribute('aria-hidden','true'); }\n" +
            "      var backs=document.querySelectorAll('.modal-backdrop');\n" +
            "      for(var i=0;i<backs.length;i++){ if(backs[i].parentNode) backs[i].parentNode.removeChild(backs[i]); }\n" +
            "      if(document.body){ document.body.classList.remove('modal-open'); document.body.style.removeProperty('padding-right'); }\n" +
            "    }catch(e){}\n" +
            "  }\n" +
            "  function clearSel(){ var s=window.getSelection&&window.getSelection(); if(s&&s.removeAllRanges)s.removeAllRanges(); }\n" +
            "  function toast(anchor){\n" +
            "    var t=document.getElementById('irongoo-copied-toast');\n" +
            "    if(!t){ t=document.createElement('div'); t.id='irongoo-copied-toast'; t.textContent='已复制';\n" +
            "      (document.body||document.documentElement).appendChild(t); }\n" +
            "    var el=anchor||document.querySelector('#idqr .well');\n" +
            "    if(el){ var r=el.getBoundingClientRect();\n" +
            "      t.style.left=(r.left+r.width/2)+'px'; t.style.top=(r.bottom+8)+'px'; t.style.bottom='auto'; }\n" +
            "    t.classList.add('show'); clearTimeout(t._h);\n" +
            "    t._h=setTimeout(function(){ t.classList.remove('show'); },1400);\n" +
            "  }\n" +
            "  function copyText(text){\n" +
            "    if(!text) return;\n" +
            "    if(navigator.clipboard&&navigator.clipboard.writeText){\n" +
            "      navigator.clipboard.writeText(text).then(clearSel).catch(function(){ fallback(text); });\n" +
            "    } else fallback(text);\n" +
            "  }\n" +
            "  function fallback(text){\n" +
            "    var ta=document.createElement('textarea'); ta.value=text; ta.style.position='fixed'; ta.style.left='-9999px';\n" +
            "    document.body.appendChild(ta); ta.select(); try{document.execCommand('copy');}catch(e){}\n" +
            "    document.body.removeChild(ta); clearSel();\n" +
            "  }\n" +
            "  function wire(el){\n" +
            "    if(!el||el.getAttribute('data-irongoo-copy')==='1') return;\n" +
            "    el.setAttribute('data-irongoo-copy','1'); el.classList.remove('select-on-click');\n" +
            "    el.setAttribute('title','点击复制');\n" +
            "    el.addEventListener('mousedown',function(ev){ ev.preventDefault(); clearSel(); },true);\n" +
            "    el.addEventListener('click',function(ev){\n" +
            "      ev.preventDefault(); ev.stopPropagation();\n" +
            "      if(ev.stopImmediatePropagation) ev.stopImmediatePropagation();\n" +
            "      clearSel();\n" +
            "      var text=(el.textContent||'').replace(/\\s+/g,'').trim();\n" +
            "      copyText(text); el.classList.add('irongoo-copied'); toast(el);\n" +
            "      setTimeout(function(){ el.classList.remove('irongoo-copied'); clearSel(); },800);\n" +
            "    },true);\n" +
            "  }\n" +
            "  function currentLang(){\n" +
            "    try{\n" +
            "      var c=(localStorage.getItem('SYN_LANG')||'').toLowerCase();\n" +
            "      if(c) return c;\n" +
            "      var root=document.querySelector('[language-select]');\n" +
            "      if(root&&window.angular){\n" +
            "        var rs=angular.element(root).isolateScope()||angular.element(root).scope();\n" +
            "        if(rs&&rs.currentLocale) return (rs.currentLocale||'').toLowerCase();\n" +
            "      }\n" +
            "    }catch(e){}\n" +
            "    return '';\n" +
            "  }\n" +
            "  function isZh(){\n" +
            "    var c=currentLang();\n" +
            "    if(c.indexOf('zh')===0) return true;\n" +
            "    if(c==='en'||c.indexOf('en-')===0) return false;\n" +
            "    return true;\n" +
            "  }\n" +
            "  function syncLangToggle(){\n" +
            "    var root=document.querySelector('[language-select]');\n" +
            "    if(!root) return;\n" +
            "    var want=isZh()?'简体中文':'English';\n" +
            "    try{\n" +
            "      if(window.angular){\n" +
            "        var rs=angular.element(root).isolateScope()||angular.element(root).scope();\n" +
            "        if(rs){\n" +
            "          if(rs.displayCurrent!==want){\n" +
            "            rs.displayCurrent=want;\n" +
            "            if(rs.$applyAsync) rs.$applyAsync(); else if(!(rs.$root&&rs.$root.$$phase)) rs.$apply(function(){});\n" +
            "          }\n" +
            "          return;\n" +
            "        }\n" +
            "      }\n" +
            "    }catch(e){}\n" +
            "    var lab=root.querySelector('.dropdown-toggle .hidden-xs');\n" +
            "    if(lab&&lab.textContent.indexOf(want)<0) lab.innerHTML='&nbsp;'+want;\n" +
            "  }\n" +
            "  function refreshNavLabels(){\n" +
            "    syncLangToggle();\n" +
            "    var zh=isZh();\n" +
            "    var idLi=document.getElementById('irongoo-nav-show-id');\n" +
            "    var addLi=document.getElementById('irongoo-nav-add-device');\n" +
            "    if(idLi) setLbl(idLi, zh?'&nbsp;显示 ID':'&nbsp;Show ID');\n" +
            "    if(addLi) setLbl(addLi, zh?'&nbsp;添加设备':'&nbsp;Add Device');\n" +
            "  }\n" +
            "  function syncthingScope(){\n" +
            "    try{\n" +
            "      if(!window.angular) return null;\n" +
            "      var el=document.querySelector('[ng-controller=\"SyncthingController\"]')||document.body;\n" +
            "      var s=angular.element(el).scope();\n" +
            "      while(s&&typeof s.showDeviceIdentification!=='function') s=s.$parent;\n" +
            "      return s||null;\n" +
            "    }catch(e){ return null; }\n" +
            "  }\n" +
            "  function navClick(ev){\n" +
            "    var a=ev.target.closest ? ev.target.closest('a[data-act]') : null;\n" +
            "    if(!a) return;\n" +
            "    ev.preventDefault(); ev.stopPropagation();\n" +
            "    if(ev.stopImmediatePropagation) ev.stopImmediatePropagation();\n" +
            "    var act=a.getAttribute('data-act');\n" +
            "    // Prefer Syncthing's own controls — same path as menu, avoids extra digest flash\n" +
            "    try{\n" +
            "      if(act==='show-id'){\n" +
            "        var sid=document.querySelector('.navbar a[ng-click*=\"showDeviceIdentification\"]');\n" +
            "        if(sid){ sid.click(); return; }\n" +
            "      }\n" +
            "      if(act==='add-device'){\n" +
            "        var add=document.querySelector('button[ng-click=\"addDevice()\"], button[ng-click*=\"addDevice()\"]');\n" +
            "        if(add){ add.click(); return; }\n" +
            "      }\n" +
            "    }catch(e){}\n" +
            "    var s=syncthingScope(); if(!s) return;\n" +
            "    var run=function(){\n" +
            "      if(act==='show-id' && s.thisDevice) s.showDeviceIdentification(s.thisDevice());\n" +
            "      else if(act==='add-device' && s.addDevice) s.addDevice();\n" +
            "    };\n" +
            "    try{ if(s.$root && s.$root.$$phase) run(); else s.$apply(run); }catch(e){ run(); }\n" +
            "  }\n" +
            "  function makeNavItem(id, act, iconCls, labelHtml){\n" +
            "    var li=document.createElement('li');\n" +
            "    li.id=id;\n" +
            "    li.className='dropdown irongoo-nav-item';\n" +
            "    var a=document.createElement('a');\n" +
            "    a.href='javascript:void(0)';\n" +
            "    a.className='irongoo-nav-link';\n" +
            "    a.setAttribute('data-act', act);\n" +
            "    a.innerHTML=\n" +
            "      '<span class=\"irongoo-corner tl\"></span><span class=\"irongoo-corner tr\"></span>' +\n" +
            "      '<span class=\"irongoo-corner bl\"></span><span class=\"irongoo-corner br\"></span>' +\n" +
            "      '<span class=\"'+iconCls+'\"></span><span class=\"hidden-xs lbl\">'+labelHtml+'</span>';\n" +
            "    a.addEventListener('click', navClick, true);\n" +
            "    li.appendChild(a);\n" +
            "    return li;\n" +
            "  }\n" +
            "  function setLbl(li, html){\n" +
            "    var lbl=li&&li.querySelector('.lbl');\n" +
            "    if(lbl && lbl.innerHTML!==html) lbl.innerHTML=html;\n" +
            "  }\n" +
            "  function ensureNav(){\n" +
            "    var langLi=document.querySelector('li[language-select], .navbar li.dropdown[language-select]');\n" +
            "    if(!langLi||!langLi.parentNode) return;\n" +
            "    var old=document.getElementById('irongoo-nav-actions');\n" +
            "    if(old&&old.parentNode) old.parentNode.removeChild(old);\n" +
            "    var zh=isZh();\n" +
            "    var idHtml=zh?'&nbsp;显示 ID':'&nbsp;Show ID';\n" +
            "    var addHtml=zh?'&nbsp;添加设备':'&nbsp;Add Device';\n" +
            "    var idLi=document.getElementById('irongoo-nav-show-id');\n" +
            "    var addLi=document.getElementById('irongoo-nav-add-device');\n" +
            "    if(idLi && !idLi.querySelector('a.irongoo-nav-link')){ idLi.parentNode&&idLi.parentNode.removeChild(idLi); idLi=null; }\n" +
            "    if(addLi && !addLi.querySelector('a.irongoo-nav-link')){ addLi.parentNode&&addLi.parentNode.removeChild(addLi); addLi=null; }\n" +
            "    var parent=langLi.parentNode;\n" +
            "    if(!idLi){ idLi=makeNavItem('irongoo-nav-show-id','show-id','fa fa-qrcode', idHtml); parent.insertBefore(idLi, langLi); }\n" +
            "    else setLbl(idLi, idHtml);\n" +
            "    if(!addLi){ addLi=makeNavItem('irongoo-nav-add-device','add-device','fa fa-plus', addHtml); parent.insertBefore(addLi, langLi); }\n" +
            "    else setLbl(addLi, addHtml);\n" +
            "    // Only reorder if misplaced (avoid MutationObserver thrash / flicker)\n" +
            "    if(idLi.nextElementSibling!==addLi || addLi.nextElementSibling!==langLi){\n" +
            "      parent.insertBefore(idLi, langLi);\n" +
            "      parent.insertBefore(addLi, langLi);\n" +
            "    }\n" +
            "  }\n" +
            "  function saveDeviceName(name){\n" +
            "    name=(name||'').replace(/\\s+/g,' ').trim();\n" +
            "    if(!name) return false;\n" +
            "    var s=syncthingScope(); if(!s) return false;\n" +
            "    var run=function(){\n" +
            "      var local=s.thisDevice && s.thisDevice();\n" +
            "      if(local) local.name=name;\n" +
            "      if(s.config && s.config.devices && s.thisDeviceIn){\n" +
            "        var d=s.thisDeviceIn(s.config.devices);\n" +
            "        if(d) d.name=name;\n" +
            "      }\n" +
            "      if(local && local.deviceID && s.devices && s.devices[local.deviceID])\n" +
            "        s.devices[local.deviceID].name=name;\n" +
            "      if(typeof s.saveConfig==='function') s.saveConfig();\n" +
            "    };\n" +
            "    try{ if(s.$root&&s.$root.$$phase) run(); else s.$apply(run); }catch(e){ try{ run(); }catch(e2){ return false; } }\n" +
            "    return true;\n" +
            "  }\n" +
            "  function endInlineNameEdit(p, restoreText){\n" +
            "    window.__irongooNameEditing=false;\n" +
            "    if(!p) return;\n" +
            "    p.classList.remove('irongoo-name-editing');\n" +
            "    p.removeAttribute('contenteditable');\n" +
            "    p.removeAttribute('spellcheck');\n" +
            "    if(p._irongooTitle!=null){ p.setAttribute('title', p._irongooTitle); delete p._irongooTitle; }\n" +
            "    else p.removeAttribute('title');\n" +
            "    if(typeof restoreText==='string') p.textContent=restoreText;\n" +
            "  }\n" +
            "  function startInlineNameEdit(p){\n" +
            "    if(!p || window.__irongooNameEditing) return;\n" +
            "    if(p.classList.contains('irongoo-name-editing')) return;\n" +
            "    var prev=(p.textContent||'').trim();\n" +
            "    if(!prev || prev.charAt(0)==='(') prev='';\n" +
            "    window.__irongooNameEditing=true;\n" +
            "    p._irongooTitle=p.getAttribute('title')||'';\n" +
            "    p.removeAttribute('title');\n" +
            "    p.setAttribute('data-irongoo-prev', prev);\n" +
            "    p.classList.add('irongoo-name-editing');\n" +
            "    p.setAttribute('contenteditable','plaintext-only');\n" +
            "    if(p.getAttribute('contenteditable')!=='plaintext-only') p.setAttribute('contenteditable','true');\n" +
            "    p.setAttribute('spellcheck','false');\n" +
            "    var done=false;\n" +
            "    var finish=function(save){\n" +
            "      if(done) return; done=true;\n" +
            "      p.removeEventListener('keydown', onKey, true);\n" +
            "      p.removeEventListener('blur', onBlur, true);\n" +
            "      var val=(p.textContent||'').replace(/\\s+/g,' ').trim();\n" +
            "      var fallback=p.getAttribute('data-irongoo-prev')||prev||'Device';\n" +
            "      if(save && val && val!==fallback){\n" +
            "        if(saveDeviceName(val)) endInlineNameEdit(p, val);\n" +
            "        else endInlineNameEdit(p, fallback);\n" +
            "      } else if(save && val){\n" +
            "        endInlineNameEdit(p, val);\n" +
            "      } else {\n" +
            "        endInlineNameEdit(p, fallback);\n" +
            "      }\n" +
            "    };\n" +
            "    var onKey=function(ev){\n" +
            "      if(ev.key==='Enter'){ ev.preventDefault(); finish(true); }\n" +
            "      else if(ev.key==='Escape'){ ev.preventDefault(); finish(false); }\n" +
            "      ev.stopPropagation();\n" +
            "    };\n" +
            "    var onBlur=function(){ setTimeout(function(){ finish(true); }, 0); };\n" +
            "    p.addEventListener('keydown', onKey, true);\n" +
            "    p.addEventListener('blur', onBlur, true);\n" +
            "    setTimeout(function(){\n" +
            "      try{\n" +
            "        p.focus();\n" +
            "        var sel=window.getSelection(); var range=document.createRange();\n" +
            "        range.selectNodeContents(p); sel.removeAllRanges(); sel.addRange(range);\n" +
            "      }catch(e){}\n" +
            "    }, 0);\n" +
            "  }\n" +
            "  function wireDeviceName(){\n" +
            "    if(window.__irongooNameEditing) return;\n" +
            "    var els=document.querySelectorAll('.navbar p.navbar-text');\n" +
            "    for(var i=0;i<els.length;i++){\n" +
            "      var el=els[i];\n" +
            "      if(el.classList.contains('irongoo-name-editing')) continue;\n" +
            "      el.classList.add('irongoo-device-name');\n" +
            "      var tip=isZh()?'\\u70b9\\u51fb\\u76f4\\u63a5\\u4fee\\u6539':'Click to rename';\n" +
            "      if(el.getAttribute('title')!==tip) el.setAttribute('title', tip);\n" +
            "    }\n" +
            "  }\n" +
            "  function brandTab(){\n" +
            "    try{\n" +
            "      var titleEl=document.querySelector('title');\n" +
            "      if(titleEl){\n" +
            "        var bind=titleEl.getAttribute('ng-bind')||'';\n" +
            "        if(bind.indexOf('Syncthing')>=0)\n" +
            "          titleEl.setAttribute('ng-bind', bind.replace(/Syncthing/g,'irongoo'));\n" +
            "      }\n" +
            "      if(document.title && document.title.indexOf('Syncthing')>=0)\n" +
            "        document.title=document.title.replace(/Syncthing/g,'irongoo');\n" +
            "      var icons=document.querySelectorAll('link[rel*=\"icon\"], link[rel=\"shortcut icon\"]');\n" +
            "      for(var i=0;i<icons.length;i++){\n" +
            "        var href=icons[i].getAttribute('href')||icons[i].href||'';\n" +
            "        if(href.indexOf('favicon')<0) continue;\n" +
            "        var base=href.split('?')[0];\n" +
            "        var next=base+'?v=irongoo2';\n" +
            "        if((icons[i].getAttribute('href')||'')!==next) icons[i].setAttribute('href', next);\n" +
            "      }\n" +
            "    }catch(e){}\n" +
            "  }\n" +
            "  function setActionLabel(btn, text){\n" +
            "    if(!btn||!text) return;\n" +
            "    var label=btn.querySelector('span[data-irongoo-action-lbl], span[translate]');\n" +
            "    if(!label){\n" +
            "      var spans=btn.querySelectorAll('span');\n" +
            "      for(var i=0;i<spans.length;i++){\n" +
            "        var cls=spans[i].className||'';\n" +
            "        if(cls.indexOf('fa')>=0) continue;\n" +
            "        label=spans[i]; break;\n" +
            "      }\n" +
            "    }\n" +
            "    if(!label) return;\n" +
            "    if(label.getAttribute('data-irongoo-action-lbl')===text && (label.textContent||'').trim()===text) return;\n" +
            "    label.removeAttribute('translate');\n" +
            "    label.setAttribute('data-irongoo-action-lbl', text);\n" +
            "    label.textContent=text;\n" +
            "  }\n" +
            "  function hideTitleCountSpans(h3){\n" +
            "    if(!h3) return;\n" +
            "    var spans=h3.querySelectorAll(':scope > span');\n" +
            "    for(var i=0;i<spans.length;i++){\n" +
            "      if(i===0 && !spans[i].getAttribute('ng-if')) continue;\n" +
            "      var ng=spans[i].getAttribute('ng-if')||'';\n" +
            "      var t=(spans[i].textContent||'').replace(/\\s+/g,' ').trim();\n" +
            "      if(ng.indexOf('length')>=0 || /^\\(\\d+\\)$/.test(t))\n" +
            "        spans[i].style.setProperty('display','none','important');\n" +
            "    }\n" +
            "  }\n" +
            "  function ensureSectionToolbars(){\n" +
            "    try{\n" +
            "      var zh=isZh();\n" +
            "      // Folders: wrap title+buttons only — leave #folders list in normal flow (full width)\n" +
            "      var folderH3=document.getElementById('folder_list');\n" +
            "      if(folderH3){\n" +
            "        var folderTitle=zh?'共享目录':'Shared Folders';\n" +
            "        var titleSpan=folderH3.querySelector('span[translate]');\n" +
            "        if(!titleSpan){\n" +
            "          var sps=folderH3.children;\n" +
            "          for(var si=0;si<sps.length;si++){\n" +
            "            if(sps[si].tagName==='SPAN' && !sps[si].getAttribute('ng-if')){ titleSpan=sps[si]; break; }\n" +
            "          }\n" +
            "        }\n" +
            "        if(titleSpan && (titleSpan.textContent||'').trim()!==folderTitle) titleSpan.textContent=folderTitle;\n" +
            "        hideTitleCountSpans(folderH3);\n" +
            "      }\n" +
            "      var folderCol=folderH3 && folderH3.closest ? folderH3.closest('.col-md-6') : null;\n" +
            "      if(folderH3 && folderCol){\n" +
            "        var addFolderBtn=folderCol.querySelector('button[ng-click=\"addFolder()\"]');\n" +
            "        if(addFolderBtn && addFolderBtn.parentElement){\n" +
            "          var fBar=addFolderBtn.parentElement;\n" +
            "          var fRow=document.getElementById('irongoo-folder-header');\n" +
            "          if(!fRow){\n" +
            "            fRow=document.createElement('div');\n" +
            "            fRow.id='irongoo-folder-header';\n" +
            "            if(folderH3.parentNode) folderH3.parentNode.insertBefore(fRow, folderH3);\n" +
            "            fRow.appendChild(folderH3);\n" +
            "          } else if(folderH3.parentNode!==fRow){\n" +
            "            fRow.insertBefore(folderH3, fRow.firstChild);\n" +
            "          }\n" +
            "          if(fBar.parentNode!==fRow) fRow.appendChild(fBar);\n" +
            "          fBar.classList.remove('pull-right');\n" +
            "          fBar.classList.add('irongoo-folder-actions');\n" +
            "          var clear=folderCol.querySelector(':scope > .clearfix');\n" +
            "          if(clear) clear.classList.add('irongoo-folder-actions-host');\n" +
            "          var fRescan=fBar.querySelector('button[ng-click=\"rescanAllFolders()\"]');\n" +
            "          var fPause=fBar.querySelector('button[ng-click=\"setAllFoldersPause(true)\"]');\n" +
            "          var fResume=fBar.querySelector('button[ng-click=\"setAllFoldersPause(false)\"]');\n" +
            "          if(addFolderBtn) fBar.appendChild(addFolderBtn);\n" +
            "          if(fRescan) fBar.appendChild(fRescan);\n" +
            "          if(fPause) fBar.appendChild(fPause);\n" +
            "          if(fResume) fBar.appendChild(fResume);\n" +
            "          setActionLabel(addFolderBtn, zh?'添加':'Add');\n" +
            "          setActionLabel(fRescan, zh?'扫描':'Scan');\n" +
            "          setActionLabel(fPause, zh?'暂停':'Pause');\n" +
            "          setActionLabel(fResume, zh?'继续':'Resume');\n" +
            "        }\n" +
            "      }\n" +
            "      var recent=document.querySelector('button[ng-click=\"globalChanges()\"]');\n" +
            "      if(!recent||!recent.parentElement) return;\n" +
            "      var bar=recent.parentElement;\n" +
            "      var col=recent.closest ? recent.closest('.col-md-6') : null;\n" +
            "      if(!col) return;\n" +
            "      var remoteH3=null;\n" +
            "      var row=document.getElementById('irongoo-remote-header');\n" +
            "      if(row) remoteH3=row.querySelector('h3');\n" +
            "      if(!remoteH3){\n" +
            "        var topH3=col.querySelectorAll(':scope > h3');\n" +
            "        if(topH3.length>=2) remoteH3=topH3[1];\n" +
            "      }\n" +
            "      if(!remoteH3){\n" +
            "        var allH3=col.querySelectorAll('h3');\n" +
            "        for(var hi=0;hi<allH3.length;hi++){\n" +
            "          var ht=(allH3[hi].textContent||'').trim();\n" +
            "          if(ht.indexOf('远程设备')===0||ht.indexOf('Remote Devices')===0){ remoteH3=allH3[hi]; break; }\n" +
            "        }\n" +
            "      }\n" +
            "      if(!remoteH3) return;\n" +
            "      var remoteTitle=zh?'远程设备':'Remote Devices';\n" +
            "      var rTitleSpan=remoteH3.querySelector('span[translate]');\n" +
            "      if(!rTitleSpan){\n" +
            "        var rs=remoteH3.children;\n" +
            "        for(var ri=0;ri<rs.length;ri++){\n" +
            "          if(rs[ri].tagName==='SPAN' && !rs[ri].getAttribute('ng-if')){ rTitleSpan=rs[ri]; break; }\n" +
            "        }\n" +
            "      }\n" +
            "      if(rTitleSpan && (rTitleSpan.textContent||'').trim()!==remoteTitle) rTitleSpan.textContent=remoteTitle;\n" +
            "      hideTitleCountSpans(remoteH3);\n" +
            "      var host=bar.closest('.form-group');\n" +
            "      if(!row){\n" +
            "        row=document.createElement('div');\n" +
            "        row.id='irongoo-remote-header';\n" +
            "        if(remoteH3.parentNode) remoteH3.parentNode.insertBefore(row, remoteH3);\n" +
            "        row.appendChild(remoteH3);\n" +
            "      } else if(remoteH3.parentNode!==row){\n" +
            "        row.insertBefore(remoteH3, row.firstChild);\n" +
            "      }\n" +
            "      if(bar.parentNode!==row) row.appendChild(bar);\n" +
            "      bar.classList.remove('pull-right');\n" +
            "      bar.classList.add('irongoo-remote-actions');\n" +
            "      if(host) host.classList.add('irongoo-remote-actions-host');\n" +
            "      var add=bar.querySelector('button[ng-click=\"addDevice()\"]');\n" +
            "      var pause=bar.querySelector('button[ng-click=\"setAllDevicesPause(true)\"]');\n" +
            "      var resume=bar.querySelector('button[ng-click=\"setAllDevicesPause(false)\"]');\n" +
            "      if(add) bar.appendChild(add);\n" +
            "      if(recent) bar.appendChild(recent);\n" +
            "      if(pause) bar.appendChild(pause);\n" +
            "      if(resume) bar.appendChild(resume);\n" +
            "      setActionLabel(add, zh?'添加':'Add');\n" +
            "      setActionLabel(recent, zh?'最近':'Recent');\n" +
            "      setActionLabel(pause, zh?'暂停':'Pause');\n" +
            "      setActionLabel(resume, zh?'继续':'Resume');\n" +
            "    }catch(e){}\n" +
            "  }\n" +
            "  function classifyThisDeviceRow(tr){\n" +
            "    var html=tr.innerHTML||'';\n" +
            "    // Right: listeners, discovery, uptime (id/version are hidden)\n" +
            "    if(html.indexOf('fa-sitemap')>=0||html.indexOf('fa-map-signs')>=0||html.indexOf('fa-clock')>=0||html.indexOf('fa-qrcode')>=0||html.indexOf('fa-tag')>=0)\n" +
            "      return 'right';\n" +
            "    var t=(tr.textContent||'');\n" +
            "    if(/Listeners|Discovery|Uptime|Identification|\\bVersion\\b|监听|设备发现|运行时间|标识|版本/.test(t) && !/Local State|本地状态|本地/.test(t))\n" +
            "      return 'right';\n" +
            "    return 'left';\n" +
            "  }\n" +
            "  function splitThisDeviceStats(panel){\n" +
            "    if(!panel) return;\n" +
            "    var body=panel.querySelector('#device-this .panel-body')||panel.querySelector('.panel-body');\n" +
            "    if(!body) return;\n" +
            "    var wrap=body.querySelector('.irongoo-stats-grid');\n" +
            "    var table=wrap ? wrap.querySelector('table.irongoo-stats-left') : null;\n" +
            "    if(!table){\n" +
            "      for(var bi=0;bi<body.children.length;bi++){\n" +
            "        if(body.children[bi].tagName==='TABLE'){ table=body.children[bi]; break; }\n" +
            "      }\n" +
            "      if(!table) table=body.querySelector('table');\n" +
            "    }\n" +
            "    if(!table||!table.tBodies||!table.tBodies[0]) return;\n" +
            "    if(!wrap){\n" +
            "      wrap=document.createElement('div');\n" +
            "      wrap.className='irongoo-stats-grid';\n" +
            "      table.parentNode.insertBefore(wrap, table);\n" +
            "      wrap.appendChild(table);\n" +
            "    }\n" +
            "    var tableRight=wrap.querySelector('table.irongoo-stats-right');\n" +
            "    if(!tableRight){\n" +
            "      tableRight=table.cloneNode(false);\n" +
            "      tableRight.className=(table.className||'').replace(/irongoo-stats-left/g,'')+' irongoo-stats-right';\n" +
            "      var tbR=document.createElement('tbody');\n" +
            "      tableRight.appendChild(tbR);\n" +
            "      wrap.appendChild(tableRight);\n" +
            "    }\n" +
            "    table.classList.add('irongoo-stats-left','irongoo-stats-done');\n" +
            "    tableRight.classList.add('irongoo-stats-right','irongoo-stats-done');\n" +
            "    var tbody=table.tBodies[0];\n" +
            "    var tbodyRight=tableRight.tBodies[0];\n" +
            "    var all=Array.prototype.slice.call(tbody.rows).concat(Array.prototype.slice.call(tbodyRight.rows));\n" +
            "    for(var i=0;i<all.length;i++){\n" +
            "      var tr=all[i];\n" +
            "      var html=tr.innerHTML||'';\n" +
            "      var txt=tr.textContent||'';\n" +
            "      if(html.indexOf('fa-qrcode')>=0||html.indexOf('fa-tag')>=0||/Identification|\\bVersion\\b|标识|版本/.test(txt))\n" +
            "        tr.classList.add('irongoo-hide-row');\n" +
            "      var side=classifyThisDeviceRow(tr);\n" +
            "      if(side==='right' && tr.parentNode!==tbodyRight) tbodyRight.appendChild(tr);\n" +
            "      else if(side==='left' && tr.parentNode!==tbody) tbody.appendChild(tr);\n" +
            "    }\n" +
            "  }\n" +
            "  function closestCol(el){\n" +
            "    if(!el) return null;\n" +
            "    if(el.closest){\n" +
            "      var c=el.closest('.col-md-6');\n" +
            "      if(c) return c;\n" +
            "    }\n" +
            "    var p=el.parentElement;\n" +
            "    while(p){\n" +
            "      if(p.classList&&p.classList.contains('col-md-6')) return p;\n" +
            "      var cn=(' '+((p.className&&p.className.baseVal!=null)?p.className.baseVal:p.className)+' ');\n" +
            "      if(cn.indexOf(' col-md-6 ')>=0) return p;\n" +
            "      p=p.parentElement;\n" +
            "    }\n" +
            "    return null;\n" +
            "  }\n" +
            "  function findFolderCol(){\n" +
            "    return closestCol(document.getElementById('folder_list'))\n" +
            "      || closestCol(document.querySelector('[id^=\"folders\"]'))\n" +
            "      || closestCol(document.getElementById('folders'));\n" +
            "  }\n" +
            "  function findDeviceCol(){\n" +
            "    return closestCol(document.getElementById('device-this'))\n" +
            "      || closestCol(document.querySelector('[id^=\"devices\"]'))\n" +
            "      || closestCol(document.getElementById('devices'));\n" +
            "  }\n" +
            "  function ensureMainLayout(){\n" +
            "    try{\n" +
            "      var folderCol=findFolderCol();\n" +
            "      var deviceCol=findDeviceCol();\n" +
            "      if(!folderCol||!deviceCol||folderCol===deviceCol) return;\n" +
            "      var row=folderCol.parentElement;\n" +
            "      if(!row||deviceCol.parentElement!==row) return;\n" +
            "      row.classList.add('irongoo-layout-row');\n" +
            "      var top=document.getElementById('irongoo-this-device-wrap');\n" +
            "      if(!top){\n" +
            "        top=document.createElement('div');\n" +
            "        top.id='irongoo-this-device-wrap';\n" +
            "        row.appendChild(top);\n" +
            "      } else if(top.parentNode!==row){\n" +
            "        row.appendChild(top);\n" +
            "      }\n" +
            "      var thisH3=null;\n" +
            "      var searchRoots=[top, deviceCol];\n" +
            "      for(var si=0;si<searchRoots.length && !thisH3;si++){\n" +
            "        var h3s=searchRoots[si].getElementsByTagName('h3');\n" +
            "        for(var i=0;i<h3s.length;i++){\n" +
            "          var ht=(h3s[i].textContent||'').replace(/^\\s+|\\s+$/g,'');\n" +
            "          if(ht.indexOf('此设备')===0||ht.indexOf('This Device')===0){ thisH3=h3s[i]; break; }\n" +
            "        }\n" +
            "      }\n" +
            "      if(!thisH3){\n" +
            "        var dh=deviceCol.getElementsByTagName('h3');\n" +
            "        if(dh.length) thisH3=dh[0];\n" +
            "      }\n" +
            "      var thisPanel=null;\n" +
            "      if(thisH3){\n" +
            "        var el=thisH3.nextElementSibling;\n" +
            "        while(el && !(el.classList&&el.classList.contains('panel'))) el=el.nextElementSibling;\n" +
            "        thisPanel=el;\n" +
            "      }\n" +
            "      if(!thisPanel){\n" +
            "        for(var di=0;di<deviceCol.children.length;di++){\n" +
            "          var ch=deviceCol.children[di];\n" +
            "          if(ch.classList&&ch.classList.contains('panel')){ thisPanel=ch; break; }\n" +
            "        }\n" +
            "      }\n" +
            "      if(thisH3 && thisH3.parentNode!==top) top.appendChild(thisH3);\n" +
            "      if(thisPanel && thisPanel.parentNode!==top) top.appendChild(thisPanel);\n" +
            "      splitThisDeviceStats(thisPanel);\n" +
            "      folderCol.classList.add('irongoo-col-folders');\n" +
            "      deviceCol.classList.add('irongoo-col-remotes');\n" +
            "      // Folders left, remotes right on top; This Device full-width below\n" +
            "      if(folderCol.parentNode!==row) row.insertBefore(folderCol, top);\n" +
            "      else if(folderCol.compareDocumentPosition(top) & Node.DOCUMENT_POSITION_FOLLOWING){ /* already above */ }\n" +
            "      else row.insertBefore(folderCol, top);\n" +
            "      if(deviceCol.parentNode!==row) row.insertBefore(deviceCol, top);\n" +
            "      if(folderCol.nextElementSibling!==deviceCol) row.insertBefore(deviceCol, folderCol.nextElementSibling===top ? top : folderCol.nextSibling);\n" +
            "      if(deviceCol.nextElementSibling!==top) row.appendChild(top);\n" +
            "      var groupH4=document.querySelectorAll('.irongoo-col-folders h4, .irongoo-col-remotes h4');\n" +
            "      for(var gi=0;gi<groupH4.length;gi++){\n" +
            "        var gt=(groupH4[gi].textContent||'').replace(/\\s+/g,' ').trim();\n" +
            "        if(!gt || gt==='()' || /^\\(\\s*\\d*\\s*\\)$/.test(gt))\n" +
            "          groupH4[gi].classList.add('irongoo-empty-group');\n" +
            "        else groupH4[gi].classList.remove('irongoo-empty-group');\n" +
            "      }\n" +
            "      applyThisDeviceVisibility();\n" +
            "    }catch(e){ try{ console.warn('irongoo layout', e); }catch(e2){} }\n" +
            "  }\n" +
            "  function applyThisDeviceVisibility(){\n" +
            "    var top=document.getElementById('irongoo-this-device-wrap');\n" +
            "    if(!top) return;\n" +
            "    if(window.__irongooShowThisDevice===false)\n" +
            "      top.style.setProperty('display','none','important');\n" +
            "    else top.style.removeProperty('display');\n" +
            "  }\n" +
            "  window.__irongooEnsureMainLayout=ensureMainLayout;\n" +
            "  window.__irongooApplyThisDeviceVisibility=applyThisDeviceVisibility;\n" +
            "  function floatPanelRoots(){\n" +
            "    return document.querySelectorAll('[id^=\"folders\"] .panel-collapse, [id^=\"devices\"] .panel-collapse');\n" +
            "  }\n" +
            "  function isFloatPanelOpen(el){\n" +
            "    return el && el.classList && (el.classList.contains('in') || el.classList.contains('collapsing'));\n" +
            "  }\n" +
            "  function panelIsFolder(el){\n" +
            "    if(!el) return false;\n" +
            "    if(el.closest && el.closest('[id^=\"folders\"]')) return true;\n" +
            "    var id=(el.id||'');\n" +
            "    if(id.indexOf('folder-')===0) return true;\n" +
            "    var p=el.parentElement;\n" +
            "    while(p){\n" +
            "      var pid=p.id||'';\n" +
            "      if(pid.indexOf('folders')===0) return true;\n" +
            "      if(pid.indexOf('devices')===0) return false;\n" +
            "      p=p.parentElement;\n" +
            "    }\n" +
            "    return false;\n" +
            "  }\n" +
            "  function positionFloatPanel(el){\n" +
            "    if(!el || !isFloatPanelOpen(el) || el.id==='device-this') return;\n" +
            "    try{\n" +
            "      // CSS anchors popup under the card at 100% width; only cap height here\n" +
            "      el.style.removeProperty('left');\n" +
            "      el.style.removeProperty('top');\n" +
            "      el.style.removeProperty('width');\n" +
            "      el.style.left='0';\n" +
            "      el.style.right='0';\n" +
            "      el.style.width='100%';\n" +
            "      el.style.maxWidth='100%';\n" +
            "      el.style.boxSizing='border-box';\n" +
            "      var panel=el.parentElement;\n" +
            "      while(panel && !(panel.classList&&panel.classList.contains('panel'))) panel=panel.parentElement;\n" +
            "      var top=56;\n" +
            "      if(panel){\n" +
            "        var pr=panel.getBoundingClientRect();\n" +
            "        top=Math.round(pr.bottom + 6);\n" +
            "      }\n" +
            "      el.style.maxHeight=Math.max(160, window.innerHeight - top - 16)+'px';\n" +
            "      el.style.height='auto';\n" +
            "      el.classList.add('irongoo-float-panel');\n" +
            "      var heading=el.previousElementSibling;\n" +
            "      if(heading && heading.classList && heading.classList.contains('panel-heading'))\n" +
            "        heading.classList.add('irongoo-float-active');\n" +
            "    }catch(e){}\n" +
            "  }\n" +
            "  function clearFloatPanelStyles(el){\n" +
            "    if(!el) return;\n" +
            "    el.classList.remove('irongoo-float-panel');\n" +
            "    el.style.removeProperty('left');\n" +
            "    el.style.removeProperty('right');\n" +
            "    el.style.removeProperty('top');\n" +
            "    el.style.removeProperty('width');\n" +
            "    el.style.removeProperty('max-width');\n" +
            "    el.style.removeProperty('max-height');\n" +
            "    el.style.removeProperty('height');\n" +
            "    el.style.removeProperty('box-sizing');\n" +
            "    var heading=el.previousElementSibling;\n" +
            "    if(heading && heading.classList) heading.classList.remove('irongoo-float-active');\n" +
            "  }\n" +
            "  function repositionOpenFloatPanels(){\n" +
            "    var nodes=floatPanelRoots();\n" +
            "    for(var i=0;i<nodes.length;i++){\n" +
            "      if(isFloatPanelOpen(nodes[i])) positionFloatPanel(nodes[i]);\n" +
            "      else clearFloatPanelStyles(nodes[i]);\n" +
            "    }\n" +
            "  }\n" +
            "  function forceHideFloat(el){\n" +
            "    if(!el) return;\n" +
            "    try{\n" +
            "      if(window.jQuery){\n" +
            "        var $el=window.jQuery(el);\n" +
            "        try{ $el.stop(true,true); }catch(e0){}\n" +
            "        try{\n" +
            "          var data=$el.data('bs.collapse');\n" +
            "          if(data) data.transitioning=0;\n" +
            "        }catch(e1){}\n" +
            "        $el.removeClass('in collapsing show').addClass('collapse').css({display:'none',height:''});\n" +
            "      }\n" +
            "    }catch(e){}\n" +
            "    el.classList.remove('in','collapsing','show');\n" +
            "    el.classList.add('collapse');\n" +
            "    el.style.display='none';\n" +
            "    el.style.height='';\n" +
            "    el.setAttribute('aria-expanded','false');\n" +
            "    var heading=el.previousElementSibling;\n" +
            "    if(heading && heading.classList && heading.classList.contains('panel-heading')){\n" +
            "      heading.classList.add('collapsed');\n" +
            "      heading.classList.remove('irongoo-float-active');\n" +
            "      heading.setAttribute('aria-expanded','false');\n" +
            "    }\n" +
            "    clearFloatPanelStyles(el);\n" +
            "  }\n" +
            "  function hideOpenFloatPanels(except){\n" +
            "    var nodes=floatPanelRoots();\n" +
            "    for(var i=0;i<nodes.length;i++){\n" +
            "      if(nodes[i]===except) continue;\n" +
            "      if(!isFloatPanelOpen(nodes[i]) && !(nodes[i].classList&&nodes[i].classList.contains('irongoo-float-panel'))) continue;\n" +
            "      forceHideFloat(nodes[i]);\n" +
            "    }\n" +
            "  }\n" +
            "  function ensureTooltipBodyContainer(){\n" +
            "    try{\n" +
            "      if(window.jQuery && jQuery.fn && jQuery.fn.tooltip && jQuery.fn.tooltip.Constructor){\n" +
            "        jQuery.fn.tooltip.Constructor.DEFAULTS.container = 'body';\n" +
            "      }\n" +
            "    }catch(e){}\n" +
            "    try{\n" +
            "      var nodes=document.querySelectorAll('[tooltip], [data-toggle=\"tooltip\"], [data-original-title]');\n" +
            "      for(var i=0;i<nodes.length;i++){\n" +
            "        if(!nodes[i].getAttribute('data-container')) nodes[i].setAttribute('data-container','body');\n" +
            "      }\n" +
            "    }catch(e2){}\n" +
            "    if(!window.__irongooTipBodyWired){\n" +
            "      window.__irongooTipBodyWired=true;\n" +
            "      document.addEventListener('show.bs.tooltip', function(ev){\n" +
            "        try{\n" +
            "          var t=ev.target;\n" +
            "          if(t && !t.getAttribute('data-container')) t.setAttribute('data-container','body');\n" +
            "          if(window.jQuery){\n" +
            "            var $t=jQuery(t);\n" +
            "            var tip=$t.data('bs.tooltip');\n" +
            "            if(tip){\n" +
            "              tip.options.container='body';\n" +
            "              if(tip.$tip && tip.$tip.length && tip.$tip.parent()[0]!==document.body)\n" +
            "                tip.$tip.appendTo(document.body);\n" +
            "            }\n" +
            "          }\n" +
            "        }catch(e3){}\n" +
            "      }, true);\n" +
            "      document.addEventListener('inserted.bs.tooltip', function(ev){\n" +
            "        try{\n" +
            "          if(!window.jQuery) return;\n" +
            "          var tip=jQuery(ev.target).data('bs.tooltip');\n" +
            "          if(tip && tip.$tip && tip.$tip.length) tip.$tip.appendTo(document.body);\n" +
            "        }catch(e4){}\n" +
            "      }, true);\n" +
            "    }\n" +
            "  }\n" +
            "  function wireFloatPanels(){\n" +
            "    if(window.__irongooFloatWired) return;\n" +
            "    window.__irongooFloatWired=true;\n" +
            "    document.addEventListener('show.bs.collapse', function(ev){\n" +
            "      var el=ev.target;\n" +
            "      if(!el || !el.classList || !el.classList.contains('panel-collapse')) return;\n" +
            "      if(el.id==='device-this') return;\n" +
            "      var inList = el.closest && (el.closest('[id^=\"folders\"]') || el.closest('[id^=\"devices\"]'));\n" +
            "      if(!inList) return;\n" +
            "      setTimeout(function(){ positionFloatPanel(el); }, 0);\n" +
            "      setTimeout(function(){ positionFloatPanel(el); }, 50);\n" +
            "    }, true);\n" +
            "    document.addEventListener('shown.bs.collapse', function(ev){\n" +
            "      var el=ev.target;\n" +
            "      if(!el || el.id==='device-this') return;\n" +
            "      positionFloatPanel(el);\n" +
            "    }, true);\n" +
            "    document.addEventListener('hide.bs.collapse', function(ev){\n" +
            "      var el=ev.target;\n" +
            "      if(!el || el.id==='device-this') return;\n" +
            "      clearFloatPanelStyles(el);\n" +
            "    }, true);\n" +
            "    document.addEventListener('hidden.bs.collapse', function(ev){\n" +
            "      var el=ev.target;\n" +
            "      if(!el || el.id==='device-this') return;\n" +
            "      clearFloatPanelStyles(el);\n" +
            "    }, true);\n" +
            "    document.addEventListener('click', function(ev){\n" +
            "      var t=ev.target;\n" +
            "      if(!t) return;\n" +
            "      if(t.closest){\n" +
            "        // Edit / Pause / Rescan etc. inside the float → dismiss it\n" +
            "        if(t.closest('[id^=\"folders\"] .panel-collapse .panel-footer, [id^=\"devices\"] .panel-collapse .panel-footer') ||\n" +
            "           t.closest('[id^=\"folders\"] .panel-collapse .btn, [id^=\"devices\"] .panel-collapse .btn') ||\n" +
            "           t.closest('[id^=\"folders\"] .panel-collapse a[ng-click], [id^=\"devices\"] .panel-collapse a[ng-click]')){\n" +
            "          hideOpenFloatPanels(null);\n" +
            "          return;\n" +
            "        }\n" +
            "        if(t.closest('[id^=\"folders\"] > .panel > .panel-heading, [id^=\"devices\"] > .panel > .panel-heading')) return;\n" +
            "        if(t.closest('[id^=\"folders\"] .panel-collapse.in, [id^=\"devices\"] .panel-collapse.in, [id^=\"folders\"] .panel-collapse.collapsing, [id^=\"devices\"] .panel-collapse.collapsing')) return;\n" +
            "        if(t.closest('.dropdown-menu, .btn-group.open')) return;\n" +
            "      }\n" +
            "      hideOpenFloatPanels(null);\n" +
            "    }, true);\n" +
            "    document.addEventListener('show.bs.modal', function(){ hideOpenFloatPanels(null); }, true);\n" +
            "    window.addEventListener('resize', function(){ repositionOpenFloatPanels(); });\n" +
            "    window.addEventListener('scroll', function(){ repositionOpenFloatPanels(); }, true);\n" +
            "  }\n" +
            "  function suppressStartupDialogs(){\n" +
            "    try{\n" +
            "      hideUrModal();\n" +
            "      var s=syncthingScope(); if(!s||!s.config||!s.config.options) return;\n" +
            "      var opts=s.config.options; var changed=false; var savedViaDismiss=false;\n" +
            "      if(typeof opts.urAccepted==='number' && opts.urAccepted>-1){\n" +
            "        opts.urAccepted=-1; changed=true;\n" +
            "        if(s.system&&s.system.urVersionMax) opts.urSeen=s.system.urVersionMax;\n" +
            "        else if(!(opts.urSeen>0)) opts.urSeen=999;\n" +
            "      }\n" +
            "      if(opts.unackedNotificationIDs&&opts.unackedNotificationIDs.indexOf('authenticationUserAndPassword')>=0){\n" +
            "        if(typeof s.dismissNotification==='function'){\n" +
            "          s.dismissNotification('authenticationUserAndPassword');\n" +
            "          savedViaDismiss=true;\n" +
            "        } else {\n" +
            "          var idx=opts.unackedNotificationIDs.indexOf('authenticationUserAndPassword');\n" +
            "          if(idx>-1){ opts.unackedNotificationIDs.splice(idx,1); changed=true; }\n" +
            "        }\n" +
            "      }\n" +
            "      if(changed && !savedViaDismiss && typeof s.saveConfig==='function'){\n" +
            "        try{ if(s.$root&&s.$root.$$phase) s.saveConfig(); else s.$apply(function(){ s.saveConfig(); }); }\n" +
            "        catch(e){ try{ s.saveConfig(); }catch(e2){} }\n" +
            "      }\n" +
            "    }catch(e){}\n" +
            "  }\n" +
            "  function dismissModal(modal){\n" +
            "    if(!modal) return;\n" +
            "    try{\n" +
            "      if(window.jQuery) window.jQuery(modal).modal('hide');\n" +
            "      else {\n" +
            "        modal.classList.remove('in','show');\n" +
            "        modal.style.display='none';\n" +
            "        modal.setAttribute('aria-hidden','true');\n" +
            "        var backs=document.querySelectorAll('.modal-backdrop');\n" +
            "        for(var bi=0;bi<backs.length;bi++){ if(backs[bi].parentNode) backs[bi].parentNode.removeChild(backs[bi]); }\n" +
            "        if(document.body) document.body.classList.remove('modal-open');\n" +
            "      }\n" +
            "    }catch(e){}\n" +
            "  }\n" +
            "  function ensureModalHeaderClose(modal){\n" +
            "    if(!modal) return;\n" +
            "    var header=modal.querySelector('.modal-header');\n" +
            "    if(!header) return;\n" +
            "    var btn=header.querySelector('.irongoo-modal-close');\n" +
            "    if(!btn){\n" +
            "      btn=document.createElement('button');\n" +
            "      btn.type='button';\n" +
            "      btn.className='irongoo-modal-close';\n" +
            "      btn.setAttribute('aria-label','Close');\n" +
            "      btn.setAttribute('title', isZh()?'关闭':'Close');\n" +
            "      btn.innerHTML='<span class=\"fas fa-times\" aria-hidden=\"true\"></span>';\n" +
            "      btn.addEventListener('click', function(ev){\n" +
            "        ev.preventDefault(); ev.stopPropagation();\n" +
            "        dismissModal(modal);\n" +
            "      });\n" +
            "      header.appendChild(btn);\n" +
            "    }\n" +
            "  }\n" +
            "  function ensureAllModalHeaderCloses(){\n" +
            "    var modals=document.querySelectorAll('.modal');\n" +
            "    for(var i=0;i<modals.length;i++) ensureModalHeaderClose(modals[i]);\n" +
            "  }\n" +
            "  function ensureIdqrHeaderClose(){ ensureModalHeaderClose(document.getElementById('idqr')); }\n" +
            "  function ensureDeviceGroupOptionalLabel(){\n" +
            "    var lab=document.querySelector('label[for=\"deviceGroup\"]');\n" +
            "    if(!lab) return;\n" +
            "    var opt=lab.querySelector('.irongoo-optional');\n" +
            "    var want=isZh()?'（选填）':' (optional)';\n" +
            "    if(opt){\n" +
            "      if((opt.textContent||'')!==want) opt.textContent=want;\n" +
            "      return;\n" +
            "    }\n" +
            "    opt=document.createElement('span');\n" +
            "    opt.className='irongoo-optional';\n" +
            "    opt.textContent=want;\n" +
            "    lab.appendChild(opt);\n" +
            "  }\n" +
            "  function ensureFolderGeneralFieldOrder(){\n" +
            "    var pane=document.getElementById('folder-general');\n" +
            "    if(!pane) return;\n" +
            "    function fg(id){\n" +
            "      var el=document.getElementById(id);\n" +
            "      return el&&el.closest?el.closest('.form-group'):null;\n" +
            "    }\n" +
            "    var idg=fg('folderID'), pathg=fg('folderPath'), labg=fg('folderLabel'), grpg=fg('folderGroup');\n" +
            "    if(!idg||!pathg) return;\n" +
            "    var kids=[];\n" +
            "    for(var i=0;i<pane.children.length;i++){\n" +
            "      var c=pane.children[i];\n" +
            "      if(c.classList&&c.classList.contains('form-group')) kids.push(c);\n" +
            "    }\n" +
            "    if(kids[0]===idg && kids[1]===pathg) return;\n" +
            "    var first=pane.firstChild;\n" +
            "    pane.insertBefore(idg, first);\n" +
            "    pane.insertBefore(pathg, idg.nextSibling);\n" +
            "    if(labg) pane.insertBefore(labg, pathg.nextSibling);\n" +
            "    if(grpg) pane.insertBefore(grpg, (labg||pathg).nextSibling);\n" +
            "  }\n" +
            "  function ensureFolderSharingOnGeneral(){\n" +
            "    var general=document.getElementById('folder-general');\n" +
            "    var sharing=document.getElementById('folder-sharing');\n" +
            "    if(!general||!sharing) return;\n" +
            "    // Hide Sharing tab — content lives under General\n" +
            "    var tabA=document.querySelector('#editFolder a[href=\"#folder-sharing\"]');\n" +
            "    if(tabA){\n" +
            "      var tabLi=tabA.closest ? tabA.closest('li') : tabA.parentElement;\n" +
            "      if(tabLi) tabLi.style.display='none';\n" +
            "    }\n" +
            "    var host=general.querySelector('.irongoo-share-host');\n" +
            "    if(!host){\n" +
            "      host=document.createElement('div');\n" +
            "      host.className='irongoo-share-host';\n" +
            "      general.appendChild(host);\n" +
            "    }\n" +
            "    var heading=host.querySelector('.irongoo-share-heading');\n" +
            "    var title=isZh()?'共享':'Sharing';\n" +
            "    if(!heading){\n" +
            "      heading=document.createElement('div');\n" +
            "      heading.className='irongoo-share-heading';\n" +
            "      heading.innerHTML='<span class=\"fas fa-share-alt\"></span><span class=\"irongoo-share-title\"></span>';\n" +
            "      host.insertBefore(heading, host.firstChild);\n" +
            "    }\n" +
            "    var titleEl=heading.querySelector('.irongoo-share-title');\n" +
            "    if(titleEl && (titleEl.textContent||'')!==title) titleEl.textContent=title;\n" +
            "    // Move all sharing blocks under the heading (Currently Shared + Unshared)\n" +
            "    var move=[];\n" +
            "    for(var i=0;i<sharing.children.length;i++){\n" +
            "      var ch=sharing.children[i];\n" +
            "      if(ch.classList&&ch.classList.contains('form-horizontal')) move.push(ch);\n" +
            "    }\n" +
            "    for(var m=0;m<move.length;m++){\n" +
            "      if(move[m].parentElement!==host) host.appendChild(move[m]);\n" +
            "    }\n" +
            "    if(heading.parentElement===host && host.firstChild!==heading) host.insertBefore(heading, host.firstChild);\n" +
            "    if(host.parentElement===general && general.lastElementChild!==host) general.appendChild(host);\n" +
            "  }\n" +
            "  function ensureOptionalLabel(forId){\n" +
            "    var lab=document.querySelector('label[for=\"'+forId+'\"]');\n" +
            "    if(!lab) return;\n" +
            "    // Syncthing may already include optional text in the label — avoid doubling\n" +
            "    var raw=(lab.textContent||'').replace(/\\s+/g,' ');\n" +
            "    if(/选填|optional/i.test(raw) && !lab.querySelector('.irongoo-optional')) return;\n" +
            "    var opt=lab.querySelector('.irongoo-optional');\n" +
            "    var want=isZh()?'（选填）':' (optional)';\n" +
            "    if(opt){\n" +
            "      if((opt.textContent||'')!==want) opt.textContent=want;\n" +
            "      return;\n" +
            "    }\n" +
            "    opt=document.createElement('span');\n" +
            "    opt.className='irongoo-optional';\n" +
            "    opt.textContent=want;\n" +
            "    lab.appendChild(opt);\n" +
            "  }\n" +
            "  function ensureFolderGroupOptionalLabel(){\n" +
            "    ensureOptionalLabel('folderLabel');\n" +
            "    ensureOptionalLabel('folderGroup');\n" +
            "  }\n" +
            "  function ensureFolderPathBrowse(){\n" +
            "    var input=document.getElementById('folderPath');\n" +
            "    if(!input) return;\n" +
            "    var btn=document.getElementById('folderPathBrowseButton');\n" +
            "    var row=input.parentElement;\n" +
            "    if(!row || !row.classList.contains('irongoo-folder-path-row')){\n" +
            "      row=document.createElement('div');\n" +
            "      row.className='irongoo-folder-path-row';\n" +
            "      if(input.parentNode) input.parentNode.insertBefore(row, input);\n" +
            "      row.appendChild(input);\n" +
            "    }\n" +
            "    if(!btn){\n" +
            "      btn=document.createElement('button');\n" +
            "      btn.type='button';\n" +
            "      btn.id='folderPathBrowseButton';\n" +
            "      btn.className='btn btn-sm btn-default';\n" +
            "      btn.addEventListener('click', function(ev){\n" +
            "        ev.preventDefault(); ev.stopPropagation();\n" +
            "        try{\n" +
            "          if(window.callbackObject && typeof callbackObject.browseFolderPath==='function')\n" +
            "            callbackObject.browseFolderPath();\n" +
            "        }catch(e){}\n" +
            "      });\n" +
            "    }\n" +
            "    // Browse button at the start (left of path input)\n" +
            "    if(row.firstChild!==btn) row.insertBefore(btn, row.firstChild);\n" +
            "    if(input.parentElement===row && btn.nextSibling!==input) row.insertBefore(input, btn.nextSibling);\n" +
            "    var label=isZh()?'浏览':'Browse';\n" +
            "    if(btn.getAttribute('data-label')!==label){\n" +
            "      btn.setAttribute('data-label', label);\n" +
            "      btn.innerHTML='<span class=\"fas fa-folder-open\"></span>&nbsp;'+label;\n" +
            "    }\n" +
            "    if(input.hasAttribute('readonly') || input.disabled) btn.setAttribute('disabled','disabled');\n" +
            "    else btn.removeAttribute('disabled');\n" +
            "    input.removeAttribute('list');\n" +
            "    var dl=document.getElementById('directory-list');\n" +
            "    if(dl && dl.parentNode) dl.parentNode.removeChild(dl);\n" +
            "  }\n" +
            "  function editFolderOpen(){\n" +
            "    var m=document.getElementById('editFolder');\n" +
            "    return !!(m && (m.classList.contains('in')||m.classList.contains('show')||m.style.display==='block'));\n" +
            "  }\n" +
            "  function lightScan(){\n" +
            "    ensureNav();\n" +
            "    ensureSectionToolbars();\n" +
            "    ensureRemoteUsersIcons();\n" +
            "    ensureThisDeviceIcon();\n" +
            "    ensureFolderIcons();\n" +
            "    wireFolderIconOpenOnly();\n" +
            "    ensureAllModalHeaderCloses();\n" +
            "    ensureTooltipBodyContainer();\n" +
            "    if(editFolderOpen()){\n" +
            "      ensureDeviceGroupOptionalLabel();\n" +
            "      ensureFolderGroupOptionalLabel();\n" +
            "      ensureFolderGeneralFieldOrder();\n" +
            "      ensureFolderSharingOnGeneral();\n" +
            "      ensureFolderPathBrowse();\n" +
            "    }\n" +
            "  }\n" +
            "  function scan(){\n" +
            "    var nodes=document.querySelectorAll('#idqr .well');\n" +
            "    for(var i=0;i<nodes.length;i++) wire(nodes[i]);\n" +
            "    ensureNav();\n" +
            "    ensureSectionToolbars();\n" +
            "    ensureMainLayout();\n" +
            "    ensureRemoteUsersIcons();\n" +
            "    ensureThisDeviceIcon();\n" +
            "    ensureFolderIcons();\n" +
            "    wireFolderIconOpenOnly();\n" +
            "    ensureAllModalHeaderCloses();\n" +
            "    ensureTooltipBodyContainer();\n" +
            "    if(editFolderOpen()){\n" +
            "      ensureDeviceGroupOptionalLabel();\n" +
            "      ensureFolderGroupOptionalLabel();\n" +
            "      ensureFolderGeneralFieldOrder();\n" +
            "      ensureFolderSharingOnGeneral();\n" +
            "      ensureFolderPathBrowse();\n" +
            "    }\n" +
            "    wireFloatPanels();\n" +
            "    repositionOpenFloatPanels();\n" +
            "    wireDeviceName();\n" +
            "    brandTab();\n" +
            "    suppressStartupDialogs();\n" +
            "  }\n" +
            "  scan();\n" +
            "  var __irongooSuppressN=0;\n" +
            "  var __irongooSuppressT=setInterval(function(){\n" +
            "    __irongooSuppressN++; suppressStartupDialogs();\n" +
            "    if(__irongooSuppressN===1||__irongooSuppressN%4===0){ ensureMainLayout(); ensureRemoteUsersIcons(); ensureThisDeviceIcon(); ensureFolderIcons(); }\n" +
            "    if(__irongooSuppressN>=40) clearInterval(__irongooSuppressT);\n" +
            "  }, 500);\n" +
            "  setInterval(brandTab, 5000);\n" +
            "  setInterval(function(){\n" +
            "    if(document.hidden) return;\n" +
            "    ensureMainLayout(); ensureRemoteUsersIcons(); ensureThisDeviceIcon(); ensureFolderIcons();\n" +
            "  }, 4000);\n" +
            "  if(!window.__irongooNameDeleg){\n" +
            "    window.__irongooNameDeleg=true;\n" +
            "    document.addEventListener('click', function(ev){\n" +
            "      var t=ev.target;\n" +
            "      if(!t) return;\n" +
            "      var nameEl=t.closest ? t.closest('.navbar p.navbar-text') : null;\n" +
            "      if(nameEl && nameEl.classList.contains('irongoo-name-editing')){\n" +
            "        ev.stopPropagation();\n" +
            "        return;\n" +
            "      }\n" +
            "      if(nameEl && !nameEl.classList.contains('irongoo-name-editing')){\n" +
            "        ev.preventDefault(); ev.stopPropagation();\n" +
            "        if(ev.stopImmediatePropagation) ev.stopImmediatePropagation();\n" +
            "        startInlineNameEdit(nameEl);\n" +
            "        return;\n" +
            "      }\n" +
            "      var langA=t.closest ? t.closest('[language-select] .dropdown-menu a') : null;\n" +
            "      if(langA){\n" +
            "        setTimeout(function(){ refreshNavLabels(); ensureSectionToolbars(); }, 50);\n" +
            "      }\n" +
            "    }, true);\n" +
            "  }\n" +
            "  document.addEventListener('shown.bs.modal', function(ev){\n" +
            "    if(!ev || !ev.target) return;\n" +
            "    ensureModalHeaderClose(ev.target);\n" +
            "    if(ev.target.id==='editFolder'){\n" +
            "      ensureFolderGeneralFieldOrder();\n" +
            "      ensureFolderSharingOnGeneral();\n" +
            "      ensureFolderGroupOptionalLabel();\n" +
            "      ensureDeviceGroupOptionalLabel();\n" +
            "      ensureFolderPathBrowse();\n" +
            "    }\n" +
            "  }, true);\n" +
            "  document.addEventListener('show.bs.modal', function(ev){\n" +
            "    if(ev && ev.target) ensureModalHeaderClose(ev.target);\n" +
            "  }, true);\n" +
            "  new MutationObserver(function(){\n" +
            "    if(window.__irongooNameEditing) return;\n" +
            "    if(window.__irongooScanBusy) return;\n" +
            "    clearTimeout(window.__irongooScanT);\n" +
            "    window.__irongooScanT=setTimeout(function(){\n" +
            "      if(window.__irongooNameEditing) return;\n" +
            "      window.__irongooScanBusy=true;\n" +
            "      try{\n" +
            "        var nodes=document.querySelectorAll('#idqr .well');\n" +
            "        for(var i=0;i<nodes.length;i++) wire(nodes[i]);\n" +
            "        wireDeviceName();\n" +
            "        if(!document.getElementById('irongoo-nav-show-id') || !document.getElementById('irongoo-nav-add-device'))\n" +
            "          ensureNav();\n" +
            "        else refreshNavLabels();\n" +
            "        lightScan();\n" +
            "      }finally{ window.__irongooScanBusy=false; }\n" +
            "    },350);\n" +
            "  }).observe(document.documentElement,{childList:true,subtree:true});\n" +
            "})();\n";

        public static void ApplyToSyncthingHome(string syncthingHomeDir)
        {
            if (string.IsNullOrWhiteSpace(syncthingHomeDir))
                return;

            try
            {
                var pngBytes = LoadLogoPngBytes();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    logger.Warn("irongoo logo resource missing; skipping Syncthing GUI branding");
                    return;
                }

                // Square tray mark for tab favicon (not the wide wordmark)
                var faviconBytes = LoadTrayLogoPngBytes() ?? pngBytes;

                var svg = BuildSvgWithEmbeddedPng(pngBytes);
                // Tab favicon names used by Syncthing (favicon-{{syncthingStatus()}}.png)
                var faviconNames = new[]
                {
                    "favicon-default.png",
                    "favicon-sync.png",
                    "favicon-notify.png",
                    "favicon-pause.png",
                    "favicon-ok.png",
                    "favicon-offline.png",
                    "favicon-error.png",
                    "favicon-idle.png",
                };
                foreach (var theme in ThemesToOverride)
                {
                    var imgDir = Path.Combine(syncthingHomeDir, "gui", theme, "assets", "img");
                    Directory.CreateDirectory(imgDir);
                    File.WriteAllText(Path.Combine(imgDir, "logo-horizontal.svg"), svg);

                    var pngPath = Path.Combine(imgDir, "logo-irongoo.png");
                    File.WriteAllBytes(pngPath, pngBytes);

                    foreach (var fav in faviconNames)
                        File.WriteAllBytes(Path.Combine(imgDir, fav), faviconBytes);

                    var cssDir = Path.Combine(syncthingHomeDir, "gui", theme, "assets", "css");
                    Directory.CreateDirectory(cssDir);
                    // No BOM — leading EF BB BF breaks CSS @import in some browsers
                    WriteUtf8(Path.Combine(cssDir, ThemeCssFileName), ThemeCss);

                    // Never overlay dark/light/black theme.css — that replaces theme-assets and
                    // a re-@import of the same path loops, so Syncthing falls back to white.
                    // Only shim default (auto light/dark) and append irongoo.css.
                    var themeCssPath = Path.Combine(cssDir, "theme.css");
                    if (theme == "default")
                        WriteUtf8(themeCssPath, BuildThemeCssShim(theme));
                    else if (File.Exists(themeCssPath))
                        File.Delete(themeCssPath);
                }

                // Shared JS overlays (served from default theme tree for all themes)
                var coreDir = Path.Combine(syncthingHomeDir, "gui", "default", "syncthing", "core");
                Directory.CreateDirectory(coreDir);
                WriteUtf8(
                    Path.Combine(coreDir, "languageSelectDirective.js"),
                    LanguageSelectDirectiveJs + "\n" + IrongooExtrasJs);

                logger.Info("Applied irongoo Syncthing GUI branding under {0}", syncthingHomeDir);
            }
            catch (Exception e)
            {
                logger.Warn(e, "Failed to apply irongoo Syncthing GUI branding");
            }
        }

        /// <summary>
        /// Decline anonymous usage reporting and dismiss the GUI-auth notice before Syncthing starts.
        /// Generates a default config first when the home directory is brand new.
        /// </summary>
        public static void SuppressStartupPrompts(string syncthingHomeDir, string syncthingExePath = null)
        {
            if (string.IsNullOrWhiteSpace(syncthingHomeDir))
                return;

            try
            {
                Directory.CreateDirectory(syncthingHomeDir);
                var configPath = Path.Combine(syncthingHomeDir, "config.xml");

                if (!File.Exists(configPath)
                    && !string.IsNullOrWhiteSpace(syncthingExePath)
                    && File.Exists(syncthingExePath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = syncthingExePath,
                        Arguments = $"generate --home=\"{syncthingHomeDir}\" --no-port-probing",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null && !proc.WaitForExit(90000))
                    {
                        try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                        logger.Warn("syncthing generate timed out for {0}", syncthingHomeDir);
                    }
                    else if (proc != null && proc.ExitCode != 0)
                    {
                        logger.Warn("syncthing generate exited with {0}", proc.ExitCode);
                    }
                }

                if (!File.Exists(configPath))
                    return;

                PatchConfigToSuppressPrompts(configPath);
            }
            catch (Exception e)
            {
                logger.Warn(e, "Failed to suppress Syncthing startup prompts");
            }
        }

        private static void PatchConfigToSuppressPrompts(string configPath)
        {
            var doc = new XmlDocument { PreserveWhitespace = true };
            doc.Load(configPath);

            var options = doc.SelectSingleNode("/configuration/options")
                ?? doc.SelectSingleNode("//options");
            if (options == null)
                return;

            SetOrCreateChild(doc, options, "urAccepted", "-1");
            SetOrCreateChild(doc, options, "urSeen", "999");

            var removed = false;
            var noticeNodes = options.SelectNodes("unackedNotificationID");
            if (noticeNodes != null)
            {
                // Copy first — XmlNodeList mutates while removing
                var doomed = new XmlNode[noticeNodes.Count];
                for (var i = 0; i < noticeNodes.Count; i++)
                    doomed[i] = noticeNodes[i]!;
                foreach (var node in doomed)
                {
                    if (node?.ParentNode != options)
                        continue;
                    if (string.Equals(node.InnerText?.Trim(), "authenticationUserAndPassword", StringComparison.Ordinal))
                    {
                        options.RemoveChild(node);
                        removed = true;
                    }
                }
            }

            doc.Save(configPath);
            logger.Info(
                "Patched {0}: urAccepted=-1, urSeen=999{1}",
                configPath,
                removed ? ", removed authenticationUserAndPassword notice" : "");
        }

        private static void SetOrCreateChild(XmlDocument doc, XmlNode parent, string name, string value)
        {
            var node = parent.SelectSingleNode(name);
            if (node == null)
            {
                node = doc.CreateElement(name);
                parent.AppendChild(node);
            }
            node.InnerText = value;
        }

        private static string BuildThemeCssShim(string theme)
        {
            // Always load dark Syncthing theme + irongoo.css.
            // Do NOT gate on prefers-color-scheme: CefSharp's embedded Chromium often
            // reports "light" (or no-preference→light), so packaged builds showed a white GUI
            // even when Windows / the shell chrome were dark.
            if (theme == "default")
            {
                return
                    "/* irongoo shim — force dark theme + brand CSS */\n" +
                    "@import \"../../theme-assets/dark/assets/css/theme.css\";\n" +
                    "@import url(\"irongoo.css\");\n";
            }

            return
                "/* irongoo shim */\n" +
                $"@import url(\"../../theme-assets/{theme}/assets/css/theme.css\");\n" +
                "@import url(\"irongoo.css\");\n";
        }

        private static void WriteUtf8(string path, string contents)
        {
            File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static byte[] LoadLogoPngBytes() =>
            LoadPngResource("Resources/logo-white-pink.png", "logo-white-pink.png");

        private static byte[] LoadTrayLogoPngBytes() =>
            LoadPngResource("Resources/tray-logo-pink.png", "tray-logo-pink.png");

        private static byte[] LoadPngResource(string packRelativePath, string fileName)
        {
            try
            {
                StreamResourceInfo resource = Application.GetResourceStream(
                    new Uri("pack://application:,,,/" + packRelativePath));
                if (resource?.Stream != null)
                {
                    using var ms = new MemoryStream();
                    resource.Stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch
            {
                // fall through
            }

            var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var candidates = new[]
            {
                Path.Combine(asmDir, "Resources", fileName),
                Path.Combine(asmDir, fileName),
            };
            foreach (var path in candidates)
            {
                if (File.Exists(path))
                    return File.ReadAllBytes(path);
            }

            return null;
        }

        private static string BuildSvgWithEmbeddedPng(byte[] pngBytes)
        {
            var b64 = Convert.ToBase64String(pngBytes);
            return
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
                "width=\"200\" height=\"48\" viewBox=\"0 0 200 48\" role=\"img\" aria-label=\"irongoo\">\n" +
                "  <image x=\"0\" y=\"4\" width=\"200\" height=\"40\" preserveAspectRatio=\"xMidYMid meet\" " +
                "xlink:href=\"data:image/png;base64," + b64 + "\"/>\n" +
                "</svg>\n";
        }
    }
}
