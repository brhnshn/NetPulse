using System;
using System.IO;
using PdfSharp.Fonts;

namespace NetPulse.App.Infrastructure
{
    public class WindowsFontResolver : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Eğer Arial veya benzeri temel bir font istenirse Windows altındaki ismini dönüyoruz
            if (familyName.Equals("Arial", StringComparison.OrdinalIgnoreCase))
            {
                string name = "Arial";
                if (isBold && isItalic) name += "bd";
                else if (isBold) name += "bd";
                else if (isItalic) name += "i";
                
                return new FontResolverInfo(name);
            }
            return null;
        }

        public byte[]? GetFont(string faceName)
        {
            // Windows'un standart font klasöründen Arial .ttf dosyasını byte dizisi olarak okuyoruz
            string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", faceName + ".ttf");
            
            if (File.Exists(fontPath))
            {
                return File.ReadAllBytes(fontPath);
            }

            // Eğer tam adıyla bulunamazsa düz Arial.ttf dosyasını fallback (yedek) olarak dön
            string fallbackPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "arial.ttf");
            if (File.Exists(fallbackPath))
            {
                return File.ReadAllBytes(fallbackPath);
            }

            return null;
        }
    }
}