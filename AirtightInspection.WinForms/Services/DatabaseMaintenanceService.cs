using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AirtightInspection.Config;
using AirtightInspection.Data;
using NLog;

namespace AirtightInspection.Services
{
    public sealed class DatabaseMaintenanceService : IDisposable
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly AppConfig _config;
        private readonly Database _database;
        private readonly Timer _timer;
        private int _running;
        private DateTime _lastIntegrityCheck = DateTime.MinValue;
        private DateTime _lastDiskWarning = DateTime.MinValue;
        private bool _disposed;

        public event EventHandler<string> Message;
        public event EventHandler<string> Warning;

        public DatabaseMaintenanceService(AppConfig config, Database database)
        {
            _config = config;
            _database = database;
            _timer = new Timer(_ => RunInBackground(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            if (_disposed) return;
            RunInBackground();
            _timer.Change(TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        private void RunInBackground()
        {
            if (_disposed || Interlocked.Exchange(ref _running, 1) == 1) return;
            Task.Run(() =>
            {
                try
                {
                    var hasEnoughDiskSpace = CheckDiskSpace();
                    CheckIntegrityIfDue();
                    if (hasEnoughDiskSpace) BackupIfDue();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "数据库自动维护失败");
                    OnWarning("数据库自动维护失败：" + ex.Message);
                }
                finally { Interlocked.Exchange(ref _running, 0); }
            });
        }

        private bool CheckDiskSpace()
        {
            var fullPath = Path.GetFullPath(_config.DatabasePath);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root)) return true;
            var drive = new DriveInfo(root);
            var freeMb = drive.AvailableFreeSpace / 1024L / 1024L;
            if (freeMb >= _config.MinimumFreeSpaceMb) return true;
            if (DateTime.Now - _lastDiskWarning >= TimeSpan.FromHours(6))
            {
                _lastDiskWarning = DateTime.Now;
                OnWarning($"数据库所在磁盘剩余空间仅 {freeMb:N0} MB，低于设定值 {_config.MinimumFreeSpaceMb:N0} MB，已暂停自动备份，请及时清理磁盘。");
            }
            return false;
        }

        private void CheckIntegrityIfDue()
        {
            if (!_config.EnableIntegrityCheck || DateTime.Now - _lastIntegrityCheck < TimeSpan.FromHours(_config.IntegrityCheckIntervalHours)) return;
            var result = _database.CheckIntegrity();
            _lastIntegrityCheck = DateTime.Now;
            if (string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                OnMessage("数据库完整性检查通过");
                return;
            }
            OnWarning("数据库完整性检查异常：" + result);
        }

        private void BackupIfDue()
        {
            if (!_config.EnableAutoBackup) return;
            Directory.CreateDirectory(_config.DatabaseBackupFolder);
            var backups = new DirectoryInfo(_config.DatabaseBackupFolder)
                .GetFiles("AirtightInspection_*.db")
                .OrderByDescending(file => file.LastWriteTime)
                .ToArray();
            if (backups.Length > 0 && DateTime.Now - backups[0].LastWriteTime < TimeSpan.FromHours(_config.DatabaseBackupIntervalHours))
                return;

            var backupPath = Path.Combine(_config.DatabaseBackupFolder,
                "AirtightInspection_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".db");
            _database.BackupTo(backupPath);
            OnMessage("数据库自动备份完成：" + backupPath);
            DeleteExpiredBackups();
        }

        private void DeleteExpiredBackups()
        {
            var cutoff = DateTime.Now.AddDays(-_config.DatabaseBackupRetentionDays);
            foreach (var file in new DirectoryInfo(_config.DatabaseBackupFolder).GetFiles("AirtightInspection_*.db"))
            {
                if (file.LastWriteTime >= cutoff) continue;
                try
                {
                    file.Delete();
                    OnMessage("已清理过期数据库备份：" + file.Name);
                }
                catch (Exception ex) { Log.Warn(ex, "清理过期数据库备份失败: {0}", file.FullName); }
            }
        }

        private void OnMessage(string text)
        {
            Log.Info(text);
            Message?.Invoke(this, text);
        }

        private void OnWarning(string text)
        {
            Log.Warn(text);
            Warning?.Invoke(this, text);
        }

        public void Dispose()
        {
            _disposed = true;
            _timer.Dispose();
        }
    }
}
