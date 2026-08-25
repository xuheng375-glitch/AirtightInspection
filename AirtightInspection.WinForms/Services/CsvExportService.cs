using System.Collections.Generic;
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
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("时间,工位号,工位名称,产品名称,条码,气密字符串,状态");
                foreach (var item in records)
                    writer.WriteLine(string.Join(",", Escape(item.DetectTime?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? ""),
                        item.StationNo.ToString(), Escape(item.StationName), Escape(item.ProductName), Escape(item.Barcode),
                        Escape(item.AirtightString), Escape(item.StatusText)));
            }
        });

        private static string Escape(string value)
        {
            value = value ?? string.Empty;
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }
    }
}
