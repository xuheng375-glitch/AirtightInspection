using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AirtightInspection.Config;
using NLog;

namespace AirtightInspection.Services
{
    public sealed class ScannerService : IDisposable
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly AppConfig _config;
        private readonly object _serialSync = new object();
        private readonly StringBuilder _serialBuffer = new StringBuilder();
        private SerialPort _port;
        private CancellationTokenSource _retryCts;
        private volatile bool _disposed;
        public event EventHandler<string> BarcodeReceived;
        public event EventHandler<string> Message;
        public bool IsKeyboardMode => _config.ScannerMode.Equals("Keyboard", StringComparison.OrdinalIgnoreCase);

        public ScannerService(AppConfig config) { _config = config; }
        public void Start()
        {
            if (_disposed || IsKeyboardMode || _retryCts != null) return;
            _retryCts = new CancellationTokenSource(); Task.Run(() => ConnectLoop(_retryCts.Token));
        }

        private async Task ConnectLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_port == null || !_port.IsOpen)
                    {
                        SerialPort stalePort = null;
                        lock (_serialSync)
                        {
                            if (_port != null && !_port.IsOpen)
                            {
                                stalePort = _port;
                                _port = null;
                                stalePort.DataReceived -= OnDataReceived;
                            }
                        }
                        if (stalePort != null) stalePort.Dispose();
                        SerialPort candidate = null;
                        try
                        {
                            candidate = new SerialPort(_config.ScannerPort, _config.ScannerBaudRate,
                                ParseParity(_config.ScannerParity), _config.ScannerDataBits, ParseStopBits(_config.ScannerStopBits));
                            candidate.DataReceived += OnDataReceived;
                            candidate.Open();
                            if (_disposed || token.IsCancellationRequested) return;
                            lock (_serialSync) _port = candidate;
                            candidate = null;
                            Raise("扫码枪串口已连接：" + _config.ScannerPort);
                        }
                        finally
                        {
                            if (candidate != null)
                            {
                                candidate.DataReceived -= OnDataReceived;
                                candidate.Dispose();
                            }
                        }
                    }
                    await Task.Delay(3000, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Log.Error(ex, "扫码枪串口打开失败"); Raise("扫码枪串口不可用，3 秒后重试：" + ex.Message);
                    try { await Task.Delay(3000, token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                }
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var port = sender as SerialPort;
                if (port == null || !port.IsOpen) return;
                var completed = new System.Collections.Generic.List<string>();
                var overflow = false;
                lock (_serialSync)
                {
                    _serialBuffer.Append(port.ReadExisting());
                    var end = (char)_config.ScannerLineEnd;
                    while (true)
                    {
                        var text = _serialBuffer.ToString(); var index = text.IndexOf(end);
                        if (index < 0) break;
                        completed.Add(text.Substring(0, index).Trim('\r', '\n', ' ', '\t'));
                        _serialBuffer.Remove(0, index + 1);
                    }
                    if (_serialBuffer.Length > _config.MaxBarcodeLength)
                    {
                        _serialBuffer.Clear();
                        overflow = true;
                    }
                }
                if (overflow) Raise("扫码枪串口数据长时间未出现结束符且超过最大长度，缓冲区已清空");
                foreach (var barcode in completed)
                    if (barcode.Length >= _config.MinimumBarcodeLength && barcode.Length <= _config.MaxBarcodeLength)
                        BarcodeReceived?.Invoke(this, barcode);
                    else if (barcode.Length > 0)
                        Raise($"扫码输入长度 {barcode.Length} 不在允许范围 {_config.MinimumBarcodeLength}-{_config.MaxBarcodeLength}，已拒绝");
            }
            catch (Exception ex) { Log.Error(ex, "扫码枪串口接收失败"); Raise("扫码枪接收失败：" + ex.Message); }
        }

        private void Raise(string text) { Message?.Invoke(this, text); }
        private static Parity ParseParity(string value) { Parity result; return Enum.TryParse(value, true, out result) ? result : Parity.None; }
        private static StopBits ParseStopBits(string value)
        {
            if (value == "1") return StopBits.One; if (value == "2") return StopBits.Two;
            StopBits result; return Enum.TryParse(value, true, out result) ? result : StopBits.One;
        }
        public void Dispose()
        {
            _disposed = true;
            _retryCts?.Cancel(); _retryCts?.Dispose(); _retryCts = null;
            SerialPort port;
            lock (_serialSync)
            {
                port = _port;
                _port = null;
                _serialBuffer.Clear();
                if (port != null) port.DataReceived -= OnDataReceived;
            }
            if (port == null) return;
            try { port.Close(); port.Dispose(); } catch { }
        }
    }
}
