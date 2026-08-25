using System;

namespace AirtightInspection.Models
{
    public sealed class StationConfig
    {
        public long Id { get; set; }
        public int StationNo { get; set; }
        public string StationName { get; set; }
        public bool Enabled { get; set; }
        public string Remark { get; set; }
        public DateTime CreateTime { get; set; }
        public override string ToString() => $"{StationNo} - {StationName}";
    }

    public sealed class ProductConfig
    {
        public long Id { get; set; }
        public string ProductName { get; set; }
        public DateTime CreateTime { get; set; }
        public override string ToString() => ProductName;
    }

    public sealed class PendingRecord
    {
        public int StationNo { get; set; }
        public string StationName { get; set; }
        public string Barcode { get; set; }
        public string ProductName { get; set; }
        public DateTime ScanTime { get; set; }
    }

    public sealed class ScanRecord
    {
        public long Id { get; set; }
        public int StationNo { get; set; }
        public string StationName { get; set; }
        public string Barcode { get; set; }
        public string ProductName { get; set; }
        public string AirtightString { get; set; }
        public int Status { get; set; }
        public DateTime RecordTime { get; set; }
        public DateTime? DetectTime { get; set; }
        public string StatusText => Status == 1 ? "上位机已入库" : "入库异常";
    }
}
