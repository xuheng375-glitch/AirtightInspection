using System.Collections.Generic;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AirtightInspection.Models;

namespace AirtightInspection.Services
{
    public static class CsvExportService
    {
        public static Task ExportAsync(string path, IEnumerable<ScanRecord> records) => Task.Run(() =>
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory;
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var writer = new StreamWriter(temporaryPath, false, new UTF8Encoding(true)))
                {
                    writer.WriteLine("时间,工位号,工位名称,产品名称,条码,程序号,测试压力,泄漏值,仪器状态,气密原始字符串,入库状态");
                    foreach (var item in records)
                        writer.WriteLine(string.Join(",", Escape(item.DetectTime?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? ""),
                            item.StationNo.ToString(), Escape(item.StationName), Escape(item.ProductName), Escape(item.Barcode),
                            Escape(item.ProgramDisplay), Escape(item.PressureDisplay), Escape(item.LeakDisplay), Escape(item.InstrumentStatusText),
                            Escape(item.AirtightString), Escape(item.StatusText)));
                }
                File.Move(temporaryPath, fullPath, true);
            }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
        });

        private static string Escape(string value)
        {
            value = value ?? string.Empty;
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }
    }
}
