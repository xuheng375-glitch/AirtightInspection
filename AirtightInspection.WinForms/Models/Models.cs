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
        public string ProgramNo { get; set; }
        public double? LeakValue { get; set; }
        public string LeakValueText { get; set; }
        public string LeakUnit { get; set; }
        public double? PressureValue { get; set; }
        public string PressureValueText { get; set; }
        public string PressureUnit { get; set; }
        public string ResultCode { get; set; }
        public string ResultText { get; set; }
        public string LeakDisplay => LeakValue.HasValue
            ? (string.IsNullOrWhiteSpace(LeakValueText) ? LeakValue.Value.ToString("0.########") : LeakValueText) +
              (string.IsNullOrWhiteSpace(LeakUnit) ? string.Empty : " " + LeakUnit)
            : "--";
        public string PressureDisplay => PressureValue.HasValue
            ? (string.IsNullOrWhiteSpace(PressureValueText) ? PressureValue.Value.ToString("0.########") : PressureValueText) +
              (string.IsNullOrWhiteSpace(PressureUnit) ? string.Empty : " " + PressureUnit)
            : "--";
        public string ProgramDisplay => string.IsNullOrWhiteSpace(ProgramNo) ? "--" : ProgramNo;
        public string InstrumentStatusText => string.IsNullOrWhiteSpace(ResultText) ? "历史数据未解析" : ResultText;
        public string StatusText => Status == 1 ? "上位机已入库" : "入库异常";
    }

    public sealed class AirtightResult
    {
        public bool IsParsed { get; set; }
        public string RawFrame { get; set; }
        public string ProgramNo { get; set; }
        public double? LeakValue { get; set; }
        public string LeakValueText { get; set; }
        public string LeakUnit { get; set; }
        public double? PressureValue { get; set; }
        public string PressureValueText { get; set; }
        public string PressureUnit { get; set; }
        public string ResultCode { get; set; }
        public string ResultText { get; set; }
    }
}
