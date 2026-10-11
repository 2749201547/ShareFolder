using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using SyncTrayzor.Properties;

namespace SyncTrayzor.Localization
{
    /// <summary>
    /// Runtime UI culture for WPF chrome (menus, console). Bump <see cref="Revision"/> to refresh Loc bindings.
    /// </summary>
    public sealed class UiLanguage : INotifyPropertyChanged
    {
        public static UiLanguage Instance { get; } = new();

        public event PropertyChangedEventHandler PropertyChanged;

        public int Revision { get; private set; }

        public CultureInfo Culture { get; private set; } = CultureInfo.CurrentUICulture;

        public void Apply(string cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
                return;

            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(cultureName);
            }
            catch (CultureNotFoundException)
            {
                return;
            }

            ApplyToThread(culture);
            var same = Culture != null &&
                       string.Equals(Culture.Name, culture.Name, StringComparison.OrdinalIgnoreCase);
            Culture = culture;
            if (same)
                return;

            Revision++;
            OnPropertyChanged(nameof(Culture));
            OnPropertyChanged(nameof(Revision));
        }

        public static void ApplyToThread(CultureInfo culture)
        {
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            Resources.Culture = culture;
        }

        public static string NormalizeGuiLocale(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
                return null;

            var code = locale.Trim().ToLowerInvariant().Replace('_', '-');
            if (code.StartsWith("zh"))
                return "zh-CN";
            if (code == "en" || code.StartsWith("en-"))
                return "en-US";
            return null;
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            var handler = PropertyChanged;
            if (handler == null)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.BeginInvoke(handler, this, new PropertyChangedEventArgs(name));
            else
                handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
