using SyncTrayzor.Services.Config;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NLog;

namespace SyncTrayzor.Xaml
{
    public class WindowPlacementBehaviour : DetachingBehaviour<Window>
    {
        private const int taskbarHeight = 40; // Max height
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public WindowPlacement Placement
        {
            get => (WindowPlacement)GetValue(PlacementProperty);
            set => SetValue(PlacementProperty, value);
        }

        public static readonly DependencyProperty PlacementProperty =
            DependencyProperty.Register("Placement", typeof(WindowPlacement), typeof(WindowPlacementBehaviour), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        protected override void AttachHandlers()
        {
            AssociatedObject.SourceInitialized += SourceInitialized;
            AssociatedObject.Closing += Closing;

            SetDefaultSize();
        }

        protected override void DetachHandlers()
        {
            AssociatedObject.SourceInitialized -= SourceInitialized;
            AssociatedObject.Closing -= Closing;
        }

        private void SetDefaultSize()
        {
            // Beware, we may be duplicating functionality in NoSizeBelowScreenBehaviour

            AssociatedObject.Height = Math.Min(AssociatedObject.Height, SystemParameters.VirtualScreenHeight - taskbarHeight);
            AssociatedObject.Width = Math.Min(AssociatedObject.Width, SystemParameters.VirtualScreenWidth - taskbarHeight);
        }

        private void SourceInitialized(object sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(AssociatedObject).Handle;

            // Hide Windows accent-colored window border (ColorPrevalence), for a cleaner edge like modern apps.
            DisableAccentWindowBorder(hwnd);

            if (Placement == null)
                return;

            var nativePlacement = new WINDOWPLACEMENT()
            {
                length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)),
                flags = 0,
                showCmd = Placement.IsMaximised ? SW_SHOWMAXIMIZED : SW_SHOWNORMAL,
                maxPosition = new POINT(Placement.MaxPosition.X, Placement.MaxPosition.Y),
                minPosition = new POINT(Placement.MinPosition.X, Placement.MinPosition.Y),
                normalPosition = new RECT(
                    Placement.NormalPosition.Left,
                    Placement.NormalPosition.Top,
                    Placement.NormalPosition.Right,
                    Placement.NormalPosition.Bottom),
            };

            if (!NativeMethods.SetWindowPlacement(hwnd, ref nativePlacement))
            {
                logger.Warn("Call to SetWindowPlacement failed", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            // Not 100% sure why this is needed, but if the window is minimzed before being closed to tray,
            // then is restored, we can end up in a state where we aren't active
            AssociatedObject.Activate();
        }

        private static void DisableAccentWindowBorder(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return;

            // Win10 1809+: prefer dark title bar when system chrome is still used
            const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
            const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
            var useDark = 1;
            NativeMethods.DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            NativeMethods.DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDark, sizeof(int));

            // Win11+: remove accent-colored border / caption
            // https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute
            const int DWMWA_BORDER_COLOR = 34;
            const int DWMWA_CAPTION_COLOR = 35;
            const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);
            var none = DWMWA_COLOR_NONE;
            NativeMethods.DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref none, sizeof(int));
            // Dark caption fallback if system chrome is visible
            var caption = unchecked((int)0x002B2B2B); // COLORREF 0x00BBGGRR
            NativeMethods.DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
        }

        private void Closing(object sender, CancelEventArgs e)
        {
            WindowPlacement placement = null;

            if (NativeMethods.GetWindowPlacement(new WindowInteropHelper(AssociatedObject).Handle, out var nativePlacement))
            {
                placement = new WindowPlacement()
                {
                    IsMaximised = (nativePlacement.flags & SW_SHOWMAXIMIZED) > 0,
                    MaxPosition = new System.Drawing.Point(nativePlacement.maxPosition.X, nativePlacement.maxPosition.Y),
                    MinPosition = new System.Drawing.Point(nativePlacement.minPosition.X, nativePlacement.minPosition.Y),
                    NormalPosition = System.Drawing.Rectangle.FromLTRB(
                        nativePlacement.normalPosition.Left,
                        nativePlacement.normalPosition.Top,
                        nativePlacement.normalPosition.Right,
                        nativePlacement.normalPosition.Bottom
                    ),
                };
            }
            else
            {
                logger.Warn("Call to SetWindowPlacement failed", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            if (placement != null && !placement.Equals(Placement))
                Placement = placement;
        }

        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMAXIMIZED = 3;

        // RECT structure required by WINDOWPLACEMENT structure
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }
        }

        // POINT structure required by WINDOWPLACEMENT structure
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        // WINDOWPLACEMENT stores the position, size, and state of a window
        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT minPosition;
            public POINT maxPosition;
            public RECT normalPosition;
        }

        private class NativeMethods
        {
            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

            [DllImport("dwmapi.dll")]
            public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
        }
    }
}
