using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AirtightInspection.Services
{
    public static class ManualService
    {
        private static readonly HashSet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        public static List<string> FindImages(string folder, string product)
        {
            Directory.CreateDirectory(folder);
            return Directory.EnumerateFiles(folder)
                .Where(x => Extensions.Contains(Path.GetExtension(x)) &&
                            Path.GetFileNameWithoutExtension(x).IndexOf(product, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
