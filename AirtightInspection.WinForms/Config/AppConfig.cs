using System;
using System.IO;

namespace AirtightInspection.Config
{
    public sealed class AppConfig
    {
        private readonly IniFile _ini;
        public string BaseDirectory { get; }
        public AppConfig(string baseDirectory)
        {
            BaseDirectory = baseDirectory;
            _ini = new IniFile(Path.Combine(baseDirectory, "Config.ini"));
        }

        public int WaitTimeoutSec => Math.Max(0, _ini.GetInt("App", "WaitTimeoutSec", 0));
        public int DisplayRecordLimit => Math.Max(1, _ini.GetInt("App", "DisplayRecordLimit", 1000));
        public string PlcIp => _ini.Get("Modbus", "IpAddress", "192.168.0.10");
        public int PlcPort => _ini.GetInt("Modbus", "Port", 502);
        public byte SlaveId => (byte)_ini.GetInt("Modbus", "SlaveId", 1);
        public int TimeoutMs => Math.Max(100, _ini.GetInt("Modbus", "TimeoutMs", 1000));
        public int PollIntervalMs => Math.Max(100, _ini.GetInt("Modbus", "PollIntervalMs", 500));
        public int ReconnectIntervalMs => Math.Max(500, _ini.GetInt("Modbus", "ReconnectIntervalMs", 3000));
        public bool EnableWriteAck => _ini.GetBool("Modbus", "EnableWriteAck", true);
        public ushort StationNoAddr => (ushort)_ini.GetInt("Register", "StationNoAddr", 4002);
        public ushort FlagAddr => (ushort)_ini.GetInt("Register", "FlagAddr", 4000);
        public ushort AirtightStrAddr => (ushort)_ini.GetInt("Register", "AirtightStrAddr", 5000);
        public ushort AirtightStrLen => (ushort)Math.Max(1, _ini.GetInt("Register", "AirtightStrLen", 20));
        public ushort AckAddr => (ushort)_ini.GetInt("Register", "AckAddr", 4000);
        public int AckValue => _ini.GetInt("Register", "AckValue", 2);
        public int AckValueFail => _ini.GetInt("Register", "AckValueFail", 3);
        public string WordOrder => _ini.Get("Register", "WordOrder", "LowHigh");
        public string ByteOrder => _ini.Get("Register", "ByteOrder", "BigEndian");
        public int CharsPerRegister => _ini.GetInt("Register", "CharsPerRegister", 2);
        public string StringByteOrder => _ini.Get("Register", "StringByteOrder", "LowHigh");
        public string StringEncoding => _ini.Get("Register", "StringEncoding", "ASCII");
        public int StringHeaderBytes => Math.Max(0, _ini.GetInt("Register", "StringHeaderBytes", 0));
        public string DatabasePath => Resolve(_ini.Get("Database", "FilePath", @"Data\mydb.db"));
        public bool EnableAutoBackup => _ini.GetBool("Database", "EnableAutoBackup", true);
        public string DatabaseBackupFolder => Resolve(_ini.Get("Database", "BackupFolder", @"Data\Backups"));
        public int DatabaseBackupIntervalHours => Math.Max(1, _ini.GetInt("Database", "BackupIntervalHours", 24));
        public int DatabaseBackupRetentionDays => Math.Max(1, _ini.GetInt("Database", "BackupRetentionDays", 7));
        public bool EnableIntegrityCheck => _ini.GetBool("Database", "EnableIntegrityCheck", true);
        public int IntegrityCheckIntervalHours => Math.Max(1, _ini.GetInt("Database", "IntegrityCheckIntervalHours", 24));
        public int MinimumFreeSpaceMb => Math.Max(256, _ini.GetInt("Database", "MinimumFreeSpaceMB", 10240));
        public string ScannerMode => _ini.Get("Scanner", "Mode", "Keyboard");
        public string ScannerPort => _ini.Get("Scanner", "PortName", "COM3");
        public int ScannerBaudRate => _ini.GetInt("Scanner", "BaudRate", 9600);
        public int ScannerDataBits => _ini.GetInt("Scanner", "DataBits", 8);
        public string ScannerStopBits => _ini.Get("Scanner", "StopBits", "1");
        public string ScannerParity => _ini.Get("Scanner", "Parity", "None");
        public int ScannerLineEnd => _ini.GetInt("Scanner", "LineEnd", 13);
        public int MaxBarcodeLength => Math.Max(1, _ini.GetInt("Scanner", "MaxBarcodeLength", 200));
        public int KeyboardCharTimeoutMs => Math.Max(20, _ini.GetInt("Scanner", "KeyboardCharTimeoutMs", 100));
        public string Password => _ini.Get("Security", "Password", "123456");
        public string ManualFolder => Resolve(_ini.Get("Manual", "Folder", "ProductManual"));

        private string Resolve(string path) => Path.IsPathRooted(path) ? path : Path.Combine(BaseDirectory, path);
    }
}
