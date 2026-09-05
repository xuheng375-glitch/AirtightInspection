using AirtightInspection.Data;
using AirtightInspection.Config;
using AirtightInspection.Models;
using AirtightInspection.Services;
using Microsoft.Data.Sqlite;

var temporaryRoot = Path.Combine(Path.GetTempPath(), "AirtightInspectionSmoke_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporaryRoot);

try
{
    VerifyAirtightResultParser();
    VerifyBindingFlagConfiguration(temporaryRoot);
    await VerifyDatabaseAndExportAsync(temporaryRoot);
    Console.WriteLine("全部烟雾测试通过。");
    return 0;
}
finally
{
    SqliteConnection.ClearAllPools();
    Directory.Delete(temporaryRoot, true);
}

static void VerifyAirtightResultParser()
{
    var ok = AirtightResultParser.Parse("<04>:8.01 bar:(OK):0.003 bar<04>:8.01 bar");
    Assert(ok.IsParsed, "OK 帧应成功解析");
    Assert(ok.RawFrame == "<04>:8.01 bar:(OK):0.003 bar", "粘连帧应只截取第一条结果");
    Assert(ok.ProgramNo == "04", "OK 帧程序号错误");
    Assert(ok.PressureValue == 8.01D && ok.PressureUnit == "bar", "OK 帧测试压力错误");
    Assert(ok.LeakValue == 0.003D && ok.LeakUnit == "bar", "OK 帧泄漏值错误");
    Assert(ok.ResultCode == "OK" && ok.ResultText == "合格", "OK 帧状态错误");

    var alarm = AirtightResultParser.Parse("<04>:0.083 bar:(AL):PRESSURE LOW<04>:0.0");
    Assert(alarm.IsParsed, "AL 帧应成功解析");
    Assert(alarm.ProgramNo == "04" && alarm.PressureValue == 0.083D, "AL 帧基本字段错误");
    Assert(!alarm.LeakValue.HasValue, "压力低报警帧不应产生虚假泄漏值");
    Assert(alarm.ResultCode == "AL" && alarm.ResultText == "报警 - 压力低", "AL 帧状态错误");
}

static void VerifyBindingFlagConfiguration(string temporaryRoot)
{
    var config = new AppConfig(temporaryRoot);
    Assert(config.GetStationBindingFlagAddress(1) == 3000, "工位 1 绑定点位错误");
    Assert(config.GetStationBindingFlagAddress(2) == 3002, "工位 2 绑定点位错误");
    Assert(config.GetStationBindingFlagAddress(3) == 3004, "工位 3 绑定点位错误");
    try
    {
        config.GetStationBindingFlagAddress(4);
        throw new InvalidOperationException("未配置工位不应获得绑定点位");
    }
    catch (ArgumentOutOfRangeException)
    {
        // Expected: only the three confirmed PLC stations are mapped.
    }
}

static async Task VerifyDatabaseAndExportAsync(string temporaryRoot)
{
    var database = new Database(Path.Combine(temporaryRoot, "smoke.db"));
    database.Initialize();
    var today = DateTime.Today;
    database.InsertRecord(CreateRecord(1, "工位1", "产品A", "BAR-A", today.AddHours(8)));
    database.InsertRecord(CreateRecord(2, "工位2", "产品B", "BAR-B", today.AddDays(-1).AddHours(8)));

    var filtered = database.EnumerateRecords(today, today.AddDays(1), 1, "产品A").ToList();
    Assert(filtered.Count == 1 && filtered[0].Barcode == "BAR-A", "流式组合筛选结果错误");
    Assert(!database.EnumerateRecords(today, today.AddDays(1), 2, "产品A").Any(), "不匹配工位不应导出记录");

    var queried = database.QueryRecords(today.AddDays(-2), today.AddDays(1), null, null, "BAR-B");
    Assert(queried.Count == 1 && queried[0].StationNo == 2, "条码查询结果错误");

    var csvPath = Path.Combine(temporaryRoot, "records.csv");
    await CsvExportService.ExportAsync(csvPath, filtered);
    var lines = File.ReadAllLines(csvPath);
    Assert(lines.Length == 2, "CSV 应包含标题和一条数据");
    Assert(lines[0].Contains("程序号") && lines[1].Contains("OK / 合格"), "CSV 解析字段缺失");
}

static ScanRecord CreateRecord(int stationNo, string stationName, string productName, string barcode, DateTime detectTime) =>
    new ScanRecord
    {
        StationNo = stationNo,
        StationName = stationName,
        ProductName = productName,
        Barcode = barcode,
        AirtightString = "<04>:8.01 bar:(OK):0.003 bar",
        Status = 1,
        RecordTime = detectTime.AddMinutes(-1),
        DetectTime = detectTime,
        ProgramNo = "04",
        PressureValue = 8.01D,
        PressureValueText = "8.01",
        PressureUnit = "bar",
        LeakValue = 0.003D,
        LeakValueText = "0.003",
        LeakUnit = "bar",
        ResultCode = "OK",
        ResultText = "合格"
    };

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
