using System;
using AirtightInspection.Data;
using AirtightInspection.Models;
using NLog;

namespace AirtightInspection.Services
{
    public sealed class InspectionService
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly Database _database;
        private readonly PendingRecordService _pending;
        private readonly ModbusService _modbus;
        public event EventHandler RecordsChanged;
        public event EventHandler<string> Message;

        public InspectionService(Database database, PendingRecordService pending, ModbusService modbus)
        {
            _database = database; _pending = pending; _modbus = modbus;
            _modbus.ResultReady += OnResultReady;
        }

        private void OnResultReady(object sender, PlcResultEventArgs e)
        {
            PendingRecord pending;
            if (!_pending.TryGet(e.StationNo, out pending))
            {
                Raise($"PLC 工位 {e.StationNo} 无匹配的待检测条码，本次不入库");
                TryAck(false); return;
            }
            try
            {
                var parsed = AirtightResultParser.Parse(e.AirtightString);
                _database.InsertRecord(new ScanRecord
                {
                    StationNo = pending.StationNo, StationName = pending.StationName, Barcode = pending.Barcode,
                    ProductName = pending.ProductName, AirtightString = e.AirtightString, Status = 1,
                    RecordTime = pending.ScanTime, DetectTime = DateTime.Now,
                    ProgramNo = parsed.ProgramNo,
                    LeakValue = parsed.LeakValue,
                    LeakValueText = parsed.LeakValueText,
                    LeakUnit = parsed.LeakUnit,
                    PressureValue = parsed.PressureValue,
                    PressureValueText = parsed.PressureValueText,
                    PressureUnit = parsed.PressureUnit,
                    ResultCode = parsed.ResultCode,
                    ResultText = parsed.ResultText
                });
                _pending.TryRemove(e.StationNo, pending);
                Raise($"工位 {e.StationNo} 条码 {pending.Barcode} 检测记录入库成功：" +
                      (parsed.IsParsed
                          ? $"程序 {parsed.ProgramNo}，泄漏值 {(parsed.LeakValue.HasValue ? parsed.LeakValueText + " " + parsed.LeakUnit : "--")}，{parsed.ResultText}"
                          : "气密结果未能解析，已保留原始字符串"));
                RecordsChanged?.Invoke(this, EventArgs.Empty);
                TryAck(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "检测记录入库失败"); Raise($"工位 {e.StationNo} 检测记录入库失败：{ex.Message}");
                TryAck(false);
            }
        }

        private void TryAck(bool success)
        {
            try { _modbus.WriteAck(success); }
            catch (Exception ex) { Log.Error(ex, "写 PLC 应答失败"); Raise("PLC 应答写入失败：" + ex.Message); }
        }
        private void Raise(string value) { Log.Info(value); Message?.Invoke(this, value); }
    }
}
