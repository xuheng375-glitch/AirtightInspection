using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AirtightInspection.Config;
using AirtightInspection.Utilities;
using NModbus;
using NLog;

namespace AirtightInspection.Services
{
    public sealed class PlcResultEventArgs : EventArgs
    {
        public int StationNo { get; set; }
        public string AirtightString { get; set; }
    }

    public sealed class ModbusService : IDisposable
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly AppConfig _config;
        private readonly object _sync = new object();
        private CancellationTokenSource _cts;
        private TcpClient _client;
        private IModbusMaster _master;
        private readonly ModbusFactory _factory = new ModbusFactory();
        private bool _connected;

        public event EventHandler<bool> ConnectionChanged;
        public event EventHandler<int> StationChanged;
        public event EventHandler<PlcResultEventArgs> ResultReady;
        public event EventHandler<string> Message;

        public ModbusService(AppConfig config) { _config = config; }

        public void Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            Task.Run(() => PollLoop(_cts.Token));
        }

        public void Stop()
        {
            var cts = _cts; _cts = null;
            if (cts != null) { cts.Cancel(); cts.Dispose(); }
            Disconnect();
        }

        public void WriteAck(bool success)
        {
            if (!_config.EnableWriteAck) return;
            var words = EncodingHelper.EncodeInt32(success ? _config.AckValue : _config.AckValueFail,
                _config.WordOrder, _config.ByteOrder);
            Exception lastError = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    EnsureConnected();
                    lock (_sync)
                    {
                        if (_master == null) throw new InvalidOperationException("PLC 未连接");
                        _master.WriteMultipleRegisters(_config.SlaveId, _config.AckAddr, words);
                    }
                    if (attempt > 1) RaiseMessage($"PLC 应答第 {attempt} 次写入成功");
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Log.Warn(ex, "PLC 应答第 {0} 次写入失败", attempt);
                    Disconnect();
                    if (attempt < 3) Thread.Sleep(200);
                }
            }
            throw new InvalidOperationException("PLC 应答连续 3 次写入失败", lastError);
        }

        private async Task PollLoop(CancellationToken token)
        {
            int? previousFlag = null;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    EnsureConnected();
                    int flag, station;
                    lock (_sync)
                    {
                        flag = EncodingHelper.DecodeInt32(_master.ReadHoldingRegisters(_config.SlaveId, _config.FlagAddr, 2), _config.WordOrder, _config.ByteOrder);
                        station = EncodingHelper.DecodeInt32(_master.ReadHoldingRegisters(_config.SlaveId, _config.StationNoAddr, 2), _config.WordOrder, _config.ByteOrder);
                    }
                    StationChanged?.Invoke(this, station);
                    if (!previousFlag.HasValue)
                    {
                        RaiseMessage($"PLC 寄存器初值：标志位={flag}，工位号={station}");
                    }
                    else if (flag != previousFlag.Value)
                    {
                        RaiseMessage($"PLC 标志位变化：{previousFlag} → {flag}，当前工位={station}");
                    }
                    // 首次读到 1 即处理；持续为 1 时不重复处理。成功/失败后会写回应答值 2/3。
                    if (flag == 1 && previousFlag != 1)
                    {
                        ushort[] words;
                        lock (_sync) words = _master.ReadHoldingRegisters(_config.SlaveId, _config.AirtightStrAddr, _config.AirtightStrLen);
                        var text = EncodingHelper.DecodeString(words, _config.CharsPerRegister, _config.StringByteOrder,
                            _config.StringEncoding, _config.StringHeaderBytes);
                        RaiseMessage($"检测到 PLC 上升沿，工位 {station}");
                        ResultReady?.Invoke(this, new PlcResultEventArgs { StationNo = station, AirtightString = text });
                    }
                    previousFlag = flag;
                    await Task.Delay(_config.PollIntervalMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Log.Error(ex, "PLC 轮询失败"); RaiseMessage("PLC 通信异常：" + ex.Message);
                    // Keep the last flag across reconnects. If communication is lost
                    // while D4000 is still 1, treating the first reconnect read as a
                    // new edge can process the same PLC result twice or overwrite a
                    // successful acknowledgement with failure value 3.
                    Disconnect();
                    try { await Task.Delay(_config.ReconnectIntervalMs, token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        private void EnsureConnected()
        {
            if (_master != null && _client != null && _client.Connected) return;
            Disconnect();
            var client = new TcpClient { ReceiveTimeout = _config.TimeoutMs, SendTimeout = _config.TimeoutMs };
            var task = client.ConnectAsync(_config.PlcIp, _config.PlcPort);
            if (!task.Wait(_config.TimeoutMs)) { client.Close(); throw new TimeoutException("PLC 连接超时"); }
            lock (_sync) { _client = client; _master = _factory.CreateMaster(client); }
            SetConnected(true); RaiseMessage("PLC 已连接");
        }

        private void Disconnect()
        {
            lock (_sync)
            {
                try { _master?.Dispose(); } catch { }
                try { _client?.Close(); } catch { }
                _master = null; _client = null;
            }
            SetConnected(false);
        }

        private void SetConnected(bool value)
        {
            if (_connected == value) return;
            _connected = value; ConnectionChanged?.Invoke(this, value);
        }
        private void RaiseMessage(string value) { Log.Info(value); Message?.Invoke(this, value); }
        public void Dispose() => Stop();
    }
}
