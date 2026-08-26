using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;
using AirtightInspection.Models;

namespace AirtightInspection.Data
{
    public sealed class Database
    {
        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
        private readonly string _connectionString;

        public Database(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            _connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
        }

        private SqliteConnection Open()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL;";
                command.ExecuteNonQuery();
            }
            return connection;
        }

        public void Initialize()
        {
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
CREATE TABLE IF NOT EXISTS StationConfig (
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 StationNo INTEGER NOT NULL UNIQUE,
 StationName TEXT,
 Enabled INTEGER DEFAULT 1,
 Remark TEXT,
 CreateTime TEXT
);
CREATE TABLE IF NOT EXISTS ScanRecord (
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 StationNo INTEGER,
 StationName TEXT,
 Barcode TEXT,
 ProductName TEXT,
 AirtightString TEXT,
 Status INTEGER,
 RecordTime TEXT,
 DetectTime TEXT,
 ProgramNo TEXT,
 LeakValue REAL,
 LeakValueText TEXT,
 LeakUnit TEXT,
 PressureValue REAL,
 PressureValueText TEXT,
 PressureUnit TEXT,
 ResultCode TEXT,
 ResultText TEXT
);
CREATE TABLE IF NOT EXISTS ProductConfig (
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 ProductName TEXT NOT NULL UNIQUE,
 CreateTime TEXT
);
CREATE INDEX IF NOT EXISTS IX_ScanRecord_RecordTime ON ScanRecord(RecordTime DESC);
CREATE INDEX IF NOT EXISTS IX_ScanRecord_DetectTime ON ScanRecord(DetectTime DESC);
CREATE INDEX IF NOT EXISTS IX_ScanRecord_StationNo ON ScanRecord(StationNo);";
                command.ExecuteNonQuery();
                EnsureColumn(connection, "ProgramNo", "TEXT");
                EnsureColumn(connection, "LeakValue", "REAL");
                EnsureColumn(connection, "LeakValueText", "TEXT");
                EnsureColumn(connection, "LeakUnit", "TEXT");
                EnsureColumn(connection, "PressureValue", "REAL");
                EnsureColumn(connection, "PressureValueText", "TEXT");
                EnsureColumn(connection, "PressureUnit", "TEXT");
                EnsureColumn(connection, "ResultCode", "TEXT");
                EnsureColumn(connection, "ResultText", "TEXT");
            }
        }

        public List<StationConfig> GetStations(bool enabledOnly = false)
        {
            var result = new List<StationConfig>();
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id,StationNo,StationName,Enabled,Remark,CreateTime FROM StationConfig" +
                                      (enabledOnly ? " WHERE Enabled=1" : string.Empty) + " ORDER BY StationNo";
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) result.Add(new StationConfig
                    {
                        Id = reader.GetInt64(0), StationNo = reader.GetInt32(1), StationName = AsString(reader, 2),
                        Enabled = reader.GetInt32(3) == 1, Remark = AsString(reader, 4), CreateTime = AsTime(reader, 5)
                    });
            }
            return result;
        }

        public void AddStation(StationConfig station)
        {
            Execute("INSERT INTO StationConfig(StationNo,StationName,Enabled,Remark,CreateTime) VALUES(@no,@name,@enabled,@remark,@time)",
                P("@no", station.StationNo), P("@name", station.StationName), P("@enabled", station.Enabled ? 1 : 0),
                P("@remark", station.Remark), P("@time", DateTime.Now.ToString(TimeFormat)));
        }

        public void UpdateStation(StationConfig station)
        {
            Execute("UPDATE StationConfig SET StationNo=@no,StationName=@name,Enabled=@enabled,Remark=@remark WHERE Id=@id",
                P("@no", station.StationNo), P("@name", station.StationName), P("@enabled", station.Enabled ? 1 : 0),
                P("@remark", station.Remark), P("@id", station.Id));
        }

        public void DeleteStation(long id) => Execute("DELETE FROM StationConfig WHERE Id=@id", P("@id", id));

        public List<ProductConfig> GetProducts()
        {
            var result = new List<ProductConfig>();
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id,ProductName,CreateTime FROM ProductConfig ORDER BY ProductName COLLATE NOCASE";
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) result.Add(new ProductConfig
                    { Id = reader.GetInt64(0), ProductName = AsString(reader, 1), CreateTime = AsTime(reader, 2) });
            }
            return result;
        }

        public long AddProduct(string name)
        {
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO ProductConfig(ProductName,CreateTime) VALUES(@name,@time); SELECT last_insert_rowid();";
                command.Parameters.Add(P("@name", name)); command.Parameters.Add(P("@time", DateTime.Now.ToString(TimeFormat)));
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        public void RenameProduct(long id, string name) =>
            Execute("UPDATE ProductConfig SET ProductName=@name WHERE Id=@id", P("@name", name), P("@id", id));
        public void DeleteProduct(long id) => Execute("DELETE FROM ProductConfig WHERE Id=@id", P("@id", id));

        public void InsertRecord(ScanRecord record)
        {
            Execute(@"INSERT INTO ScanRecord(StationNo,StationName,Barcode,ProductName,AirtightString,Status,RecordTime,DetectTime,
ProgramNo,LeakValue,LeakValueText,LeakUnit,PressureValue,PressureValueText,PressureUnit,ResultCode,ResultText)
VALUES(@no,@station,@barcode,@product,@airtight,@status,@recordTime,@detectTime,
@programNo,@leakValue,@leakValueText,@leakUnit,@pressureValue,@pressureValueText,@pressureUnit,@resultCode,@resultText)",
                P("@no", record.StationNo), P("@station", record.StationName), P("@barcode", record.Barcode),
                P("@product", record.ProductName), P("@airtight", record.AirtightString), P("@status", record.Status),
                P("@recordTime", record.RecordTime.ToString(TimeFormat)),
                P("@detectTime", record.DetectTime.HasValue ? (object)record.DetectTime.Value.ToString(TimeFormat) : DBNull.Value),
                P("@programNo", record.ProgramNo), P("@leakValue", record.LeakValue), P("@leakValueText", record.LeakValueText),
                P("@leakUnit", record.LeakUnit), P("@pressureValue", record.PressureValue), P("@pressureValueText", record.PressureValueText),
                P("@pressureUnit", record.PressureUnit), P("@resultCode", record.ResultCode), P("@resultText", record.ResultText));
        }

        public List<ScanRecord> GetRecords(int limit = 0)
        {
            var result = new List<ScanRecord>();
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT Id,StationNo,StationName,Barcode,ProductName,AirtightString,Status,RecordTime,DetectTime,
ProgramNo,LeakValue,LeakValueText,LeakUnit,PressureValue,PressureValueText,PressureUnit,ResultCode,ResultText
FROM ScanRecord ORDER BY Id DESC" + (limit > 0 ? " LIMIT @limit" : string.Empty);
                if (limit > 0) command.Parameters.Add(P("@limit", limit));
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) result.Add(ReadRecord(reader));
            }
            return result;
        }

        public IEnumerable<ScanRecord> EnumerateRecords()
        {
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT Id,StationNo,StationName,Barcode,ProductName,AirtightString,Status,RecordTime,DetectTime,
ProgramNo,LeakValue,LeakValueText,LeakUnit,PressureValue,PressureValueText,PressureUnit,ResultCode,ResultText
FROM ScanRecord ORDER BY Id DESC";
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        yield return ReadRecord(reader);
            }
        }

        public IEnumerable<ScanRecord> EnumerateRecords(DateTime startTime, DateTime endTimeExclusive,
            int? stationNo, string productName)
        {
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                var conditions = new List<string>
                {
                    "DetectTime>=@startTime",
                    "DetectTime<@endTime"
                };
                command.Parameters.Add(P("@startTime", startTime.ToString(TimeFormat)));
                command.Parameters.Add(P("@endTime", endTimeExclusive.ToString(TimeFormat)));
                if (stationNo.HasValue)
                {
                    conditions.Add("StationNo=@stationNo");
                    command.Parameters.Add(P("@stationNo", stationNo.Value));
                }
                if (!string.IsNullOrWhiteSpace(productName))
                {
                    conditions.Add("ProductName=@productName");
                    command.Parameters.Add(P("@productName", productName.Trim()));
                }
                command.CommandText = @"SELECT Id,StationNo,StationName,Barcode,ProductName,AirtightString,Status,RecordTime,DetectTime,
ProgramNo,LeakValue,LeakValueText,LeakUnit,PressureValue,PressureValueText,PressureUnit,ResultCode,ResultText
FROM ScanRecord WHERE " + string.Join(" AND ", conditions) + " ORDER BY Id DESC";
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        yield return ReadRecord(reader);
            }
        }

        public string CheckIntegrity()
        {
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA integrity_check;";
                var result = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                return string.IsNullOrWhiteSpace(result) ? "unknown" : result;
            }
        }

        public void BackupTo(string backupPath)
        {
            var directory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = backupPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var source = Open())
                using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temporaryPath, Pooling = false }.ToString()))
                {
                    destination.Open();
                    source.BackupDatabase(destination);
                    using (var command = destination.CreateCommand())
                    {
                        command.CommandText = "PRAGMA integrity_check;";
                        var result = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("数据库备份完整性检查失败：" + result);
                    }
                }
                File.Move(temporaryPath, backupPath, false);
            }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
        }

        public List<ScanRecord> QueryRecords(DateTime startTime, DateTime endTimeExclusive,
            int? stationNo, string productName, string barcodeKeyword, int limit = 5000)
        {
            var result = new List<ScanRecord>();
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                var conditions = new List<string>
                {
                    "DetectTime>=@startTime",
                    "DetectTime<@endTime"
                };
                command.Parameters.Add(P("@startTime", startTime.ToString(TimeFormat)));
                command.Parameters.Add(P("@endTime", endTimeExclusive.ToString(TimeFormat)));
                if (stationNo.HasValue)
                {
                    conditions.Add("StationNo=@stationNo");
                    command.Parameters.Add(P("@stationNo", stationNo.Value));
                }
                if (!string.IsNullOrWhiteSpace(productName))
                {
                    conditions.Add("ProductName=@productName");
                    command.Parameters.Add(P("@productName", productName.Trim()));
                }
                if (!string.IsNullOrWhiteSpace(barcodeKeyword))
                {
                    conditions.Add("Barcode LIKE @barcode ESCAPE '\\'");
                    var escaped = barcodeKeyword.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
                    command.Parameters.Add(P("@barcode", "%" + escaped + "%"));
                }
                command.CommandText = @"SELECT Id,StationNo,StationName,Barcode,ProductName,AirtightString,Status,RecordTime,DetectTime,
ProgramNo,LeakValue,LeakValueText,LeakUnit,PressureValue,PressureValueText,PressureUnit,ResultCode,ResultText
FROM ScanRecord WHERE " + string.Join(" AND ", conditions) + " ORDER BY Id DESC LIMIT @limit";
                command.Parameters.Add(P("@limit", Math.Max(1, limit)));
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) result.Add(ReadRecord(reader));
            }
            return result;
        }

        private static ScanRecord ReadRecord(IDataRecord reader) => new ScanRecord
        {
            Id = reader.GetInt64(0),
            StationNo = reader.GetInt32(1),
            StationName = AsString(reader, 2),
            Barcode = AsString(reader, 3),
            ProductName = AsString(reader, 4),
            AirtightString = AsString(reader, 5),
            Status = reader.GetInt32(6),
            RecordTime = AsTime(reader, 7),
            DetectTime = reader.IsDBNull(8) ? (DateTime?)null : AsTime(reader, 8),
            ProgramNo = AsString(reader, 9),
            LeakValue = AsNullableDouble(reader, 10),
            LeakValueText = AsString(reader, 11),
            LeakUnit = AsString(reader, 12),
            PressureValue = AsNullableDouble(reader, 13),
            PressureValueText = AsString(reader, 14),
            PressureUnit = AsString(reader, 15),
            ResultCode = AsString(reader, 16),
            ResultText = AsString(reader, 17)
        };

        private static void EnsureColumn(SqliteConnection connection, string columnName, string columnType)
        {
            var exists = false;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(ScanRecord);";
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
            }
            if (exists) return;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "ALTER TABLE ScanRecord ADD COLUMN \"" + columnName + "\" " + columnType + ";";
                command.ExecuteNonQuery();
            }
        }

        private void Execute(string sql, params SqliteParameter[] parameters)
        {
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction())
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction; command.CommandText = sql;
                command.Parameters.AddRange(parameters); command.ExecuteNonQuery(); transaction.Commit();
            }
        }

        private static SqliteParameter P(string name, object value) => new SqliteParameter(name, value ?? DBNull.Value);
        private static string AsString(IDataRecord reader, int index) => reader.IsDBNull(index) ? string.Empty : reader.GetString(index);
        private static double? AsNullableDouble(IDataRecord reader, int index) =>
            reader.IsDBNull(index) ? (double?)null : Convert.ToDouble(reader.GetValue(index), CultureInfo.InvariantCulture);
        private static DateTime AsTime(IDataRecord reader, int index)
        {
            DateTime time;
            return DateTime.TryParseExact(AsString(reader, index), TimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out time) ? time : DateTime.MinValue;
        }
    }
}
