using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace NetPulse.App.UI
{
    public static class IconHelper
    {
        private static Icon? _cachedIcon;
        private static Bitmap? _backingBitmap;
        private static readonly object _lock = new object();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Icon? GetAppIcon()
        {
            lock (_lock)
            {
                if (_cachedIcon != null)
                {
                    return _cachedIcon;
                }

                try
                {
                    string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.jpg");
                    if (File.Exists(logoPath))
                    {
                        _backingBitmap = new Bitmap(logoPath);
                    }
                    else
                    {
                        var assembly = typeof(IconHelper).Assembly;
                        using (var stream = assembly.GetManifestResourceStream("NetPulse.App.logo.jpg"))
                        {
                            if (stream != null)
                            {
                                _backingBitmap = new Bitmap(stream);
                            }
                        }
                    }

                    if (_backingBitmap != null)
                    {
                        IntPtr hIcon = _backingBitmap.GetHicon();
                        _cachedIcon = Icon.FromHandle(hIcon);
                    }
                }
                catch (Exception)
                {
                    // Fallback to null
                }

                return _cachedIcon;
            }
        }

        public static void SetAppIcon(this Form form)
        {
            var icon = GetAppIcon();
            if (icon != null)
            {
                form.Icon = icon;
                form.ShowIcon = true;
            }
        }
    }
}
