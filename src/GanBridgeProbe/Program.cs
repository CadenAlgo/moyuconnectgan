using System.Drawing;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation.Metadata;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using GanBridgeProbe.GanGen2;
using GanBridgeProbe.MoYu;

namespace GanBridgeProbe;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class CubeAdvertisement
{
    public ulong Address { get; init; }
    public string Name { get; set; } = "(无名称)";
    public short Rssi { get; set; }
    public string Manufacturer { get; set; } = "无";
    public string Services { get; set; } = "无";
    public DateTime SeenAt { get; set; } = DateTime.Now;
    public long DiscoveryOrder { get; init; }
    public string AddressText => MainForm.FormatAddress(Address);
    public bool LooksLikeMoYu => Name.Contains("WCU", StringComparison.OrdinalIgnoreCase)
        || Name.Contains("MOYU", StringComparison.OrdinalIgnoreCase)
        || Name.Contains("AI-CUBE", StringComparison.OrdinalIgnoreCase)
        || Name.Contains("AICUBE", StringComparison.OrdinalIgnoreCase)
        || Services.Contains("0783b03e-7735-b5a0-1760-a305d2795cb0", StringComparison.OrdinalIgnoreCase);
    public override string ToString() => $"{(LooksLikeMoYu ? "[疑似魔域] " : "")}{Name}  {AddressText}  RSSI {Rssi} dBm  厂商数据 {Manufacturer}";
}

internal sealed class MainForm : Form
{
    private static readonly Guid MoYuService = Guid.Parse("0783b03e-7735-b5a0-1760-a305d2795cb0");
    private static readonly Guid MoYuNotify = Guid.Parse("0783b03e-7735-b5a0-1760-a305d2795cb1");
    private static readonly Guid MoYuWrite = Guid.Parse("0783b03e-7735-b5a0-1760-a305d2795cb2");
    private static readonly Guid GanService = Guid.Parse("6e400001-b5a3-f393-e0a9-e50e24dc4179");
    private static readonly Guid GanCommand = Guid.Parse("28be4a4a-cd67-11e9-a32f-2a2ae2dbcce4");
    private static readonly Guid GanState = Guid.Parse("28be4cb6-cd67-11e9-a32f-2a2ae2dbcce4");
    private static readonly Guid DiagnosticService = Guid.Parse("7ec5f2e3-0f50-45d3-9b64-00f1f9bd2e01");
    private static readonly Guid DiagnosticRead = Guid.Parse("7ec5f2e4-0f50-45d3-9b64-00f1f9bd2e01");
    private static readonly Guid DisService = Guid.Parse("0000180a-0000-1000-8000-00805f9b34fb");
    private static readonly Guid DisModel = Guid.Parse("00002a24-0000-1000-8000-00805f9b34fb");
    private static readonly Guid DisFirmware = Guid.Parse("00002a26-0000-1000-8000-00805f9b34fb");
    private static readonly Guid DisHardware = Guid.Parse("00002a27-0000-1000-8000-00805f9b34fb");
    private static readonly Guid DisVendor = Guid.Parse("00002a29-0000-1000-8000-00805f9b34fb");
    // Identity used by the reference cubebridge firmware. The GAN app obtains
    // the crypto MAC from manufacturer data, not from the computer name.
    private const string GanAdvertisedName = "GAN356iCarry";
    private const ushort GanCompanyIdentifier = 0x0101;
    private GattServiceProvider? _disProvider;
    private const int MaxLogLines = 900;

    private readonly Label _adapterStatus = new() { AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly Label _serverStatus = new() { AutoSize = true, Text = "GAN 广播：未启动" };
    private readonly Label _cubeStatus = new() { AutoSize = true, Text = "魔域连接：未连接" };
    private readonly ListBox _devices = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox _log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = false, Font = new Font("Consolas", 9) };
    private readonly Button _startGan = new() { Text = "诊断：启动 GAN 广播", AutoSize = true };
    private readonly Button _startGeneric = new() { Text = "诊断：启动随机 GATT 广播", AutoSize = true };
    private readonly Button _stopServer = new() { Text = "停止广播", AutoSize = true, Enabled = false };
    private readonly Button _scan = new() { Text = "扫描魔域魔方", AutoSize = true };
    private readonly Button _stopScan = new() { Text = "停止扫描", AutoSize = true, Enabled = false };
    private readonly Button _connect = new() { Text = "连接选中的魔方", AutoSize = true };
    private readonly Button _disconnect = new() { Text = "断开魔域", AutoSize = true, Enabled = false };
    private readonly Button _clearLog = new() { Text = "清空日志", AutoSize = true };
    private readonly Button _rawAdv = new() { Text = "诊断：原始广播能力", AutoSize = true };
    private readonly Button _sendTest = new() { Text = "诊断：检查订阅并测试通知", AutoSize = true };
    private readonly Button _phoneTest = new() { Text = "① 手机测试：只广播（停止魔域扫描）", AutoSize = true };
    private readonly Button _exportLog = new() { Text = "导出日志到文件", AutoSize = true };
    private readonly Button _loopback = new() { Text = "诊断：本机回环 GATT 自检", AutoSize = true };
    private readonly Button _cccdProbe = new() { Text = "诊断：探测 CCCD 描述符", AutoSize = true };
    private readonly Button _vendorAdv = new() { Text = "诊断：厂商数据广播（GAN 真机格式）", AutoSize = true };
    private readonly TextBox _ganMacBox = new() { Width = 150, Font = new Font("Consolas", 9) };
    private readonly Button _applyGanMac = new() { Text = "应用 GAN MAC", AutoSize = true };
    private byte[]? _ganCryptoMac;
    private readonly SemaphoreSlim _moyuPacketLock = new(1, 1);
    private byte _lastMoyuMoveCode;
    private readonly Label _modeStatus = new() { AutoSize = true, Text = "模式：正常", ForeColor = Color.DarkGray };
    private bool _phoneTestMode;
    private bool _pureMode;
    private BluetoothLEAdvertisementPublisher? _rawPublisher;
    private GanCrypto? _ganCrypto;
    private byte _moveCounter;
    private bool _ganReady;

    private BluetoothLEAdvertisementWatcher? _watcher;
    private BluetoothLEDevice? _moyuDevice;
    private GattDeviceService? _moyuService;
    private GattCharacteristic? _moyuNotifyCharacteristic;
    private GattCharacteristic? _moyuWriteCharacteristic;
    private MoYuCrypto? _moyuCrypto;
    private byte _moyuLastCounter;
    private bool _moyuHasBaseline;
    private readonly List<(byte Code, ushort TimeMs)> _ganMoveWindow = new();
    private CubeState? _sourceCube;
    private byte? _moyuBattery;
    private bool _ganFaceletsRequested;
    private byte _ganVirtualCounter;
    private readonly SemaphoreSlim _moyuWriteLock = new(1, 1);
    private GattServiceProvider? _provider;
    private GattLocalCharacteristic? _commandCharacteristic;
    private GattLocalCharacteristic? _stateCharacteristic;
    private readonly Dictionary<ulong, CubeAdvertisement> _seen = new();
    private long _nextDiscoveryOrder;
    private SplitContainer? _splitContainer;
    private bool _closing;
    private System.Windows.Forms.Timer? _advWatchdog;
    private GattServiceProviderAdvertisingParameters? _advParameters;
    private bool _advWanted;
    private bool _serverStarting;
    private bool _advertisementRecoveryInProgress;
    private DateTime _nextAdvertisementRecoveryUtc;
    private int _advertisementRecoveryFailures;
    private Guid _activeServiceId;
    private Guid _activeWriteId;
    private Guid _activeReadId;
    private string _activeServerKind = "GAN";
    private int _advHeartbeat;

    public MainForm()
    {
        Text = "魔域 → GAN BLE 桥接验证器 v0.25-local";
        MinimumSize = new Size(1000, 700);
        Size = new Size(1240, 850);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9);
        BuildLayout();
        _startGan.Click += async (_, _) => await StartServerAsync(GanService, GanCommand, GanState, "GAN");
        _startGeneric.Click += async (_, _) => await StartServerAsync(DiagnosticService, Guid.Empty, DiagnosticRead, "随机 GATT");
        _rawAdv.Click += (_, _) => ToggleRawAdvertisement();
        _sendTest.Click += async (_, _) => await SendTestNotificationAsync();
        _stopServer.Click += (_, _) => StopServer();
        _scan.Click += (_, _) => StartScan();
        _stopScan.Click += (_, _) => StopScan();
        _connect.Click += async (_, _) => await ConnectSelectedAsync();
        _disconnect.Click += async (_, _) => await DisconnectMoYuAsync();
        _clearLog.Click += (_, _) => _log.Clear();
        _phoneTest.Click += (_, _) => TogglePhoneTestMode();
        _exportLog.Click += (_, _) => ExportLog();
        _loopback.Click += async (_, _) => await RunLoopbackGattCheckAsync();
        _cccdProbe.Click += async (_, _) => await ProbeCccdAsync();
        _vendorAdv.Click += (_, _) => ToggleVendorAdvertisement();
        _applyGanMac.Click += async (_, _) => await ApplyGanMacAsync();
        Shown += async (_, _) => { ConfigureSplitLayout(); await CheckAdapterAsync(); };
        FormClosing += async (_, _) => await CleanupAsync();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        Controls.Add(root);
        root.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(1180, 0),
            Text = "本轮要验证：① 电脑能否持续对外广播 GAN 服务；② 手机 Cube Station 能否靠名称找到并连上它；③ 之后再连魔域。正确顺序：先点“诊断：启动 GAN 广播”→只用手机扫描（先别点本程序的扫描）→手机连上后再点“扫描魔域魔方”→“连接选中的魔方”。",
            ForeColor = Color.DarkSlateGray,
            Padding = new Padding(0, 0, 0, 10)
        }, 0, 0);
        var status = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 0, 0, 8) };
        status.Controls.Add(_adapterStatus);
        status.Controls.Add(new Label { Text = "    |    ", AutoSize = true });
        status.Controls.Add(_serverStatus);
        status.Controls.Add(new Label { Text = "    |    ", AutoSize = true });
        status.Controls.Add(_cubeStatus);
        status.Controls.Add(new Label { Text = "    |    ", AutoSize = true });
        status.Controls.Add(_modeStatus);
        root.Controls.Add(status, 0, 1);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 540, FixedPanel = FixedPanel.Panel1 };
        _splitContainer = split;
        root.Controls.Add(split, 0, 2);
        var devicePanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(0, 4, 8, 4) };
        devicePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        devicePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        devicePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        devicePanel.Controls.Add(new Label { Text = "附近 BLE 广播（唤醒 V10 AI；不要让魔域 App 同时连接）", AutoSize = true, Padding = new Padding(0, 0, 0, 6) }, 0, 0);
        devicePanel.Controls.Add(_devices, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 6, 0, 0) };
        buttons.Controls.AddRange(new Control[] { _scan, _stopScan, _connect, _disconnect, _phoneTest });
        devicePanel.Controls.Add(buttons, 0, 2);
        split.Panel1.Controls.Add(devicePanel);
        var logPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(8, 4, 0, 4) };
        logPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var logButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        logButtons.Controls.Add(new Label { Text = "运行日志", AutoSize = true, Padding = new Padding(0, 5, 8, 0) });
        logButtons.Controls.Add(_startGan);
        logButtons.Controls.Add(_startGeneric);
        logButtons.Controls.Add(_stopServer);
        logButtons.Controls.Add(_clearLog);
        logButtons.Controls.Add(_rawAdv);
        logButtons.Controls.Add(_sendTest);
        logButtons.Controls.Add(_exportLog);
        logButtons.Controls.Add(_loopback);
        logButtons.Controls.Add(_cccdProbe);
        logButtons.Controls.Add(_vendorAdv);
        logButtons.Controls.Add(new Label { Text = "GAN MAC（手机看到的地址）:", AutoSize = true, Padding = new Padding(8, 5, 2, 0) });
        logButtons.Controls.Add(_ganMacBox);
        logButtons.Controls.Add(_applyGanMac);
        logPanel.Controls.Add(logButtons, 0, 0);
        logPanel.Controls.Add(_log, 0, 1);
        split.Panel2.Controls.Add(logPanel);
        root.Controls.Add(new Label { Dock = DockStyle.Fill, AutoSize = true, Text = "诊断重点：日志里的“广播心跳”每 2 秒一条。如果心跳突然停或变成 Aborted，而当时正在扫描魔域，就说明这个 Realtek 适配器无法同时扮演 Central（扫描）和 Peripheral（广播），需要调整操作顺序或更换适配器。", ForeColor = Color.DimGray, Padding = new Padding(0, 8, 0, 0) }, 0, 3);
    }

    private void ConfigureSplitLayout()
    {
        if (_splitContainer is null) return;
        var width = _splitContainer.ClientSize.Width;
        if (width < 700) return;
        var minPanel = Math.Clamp((width - _splitContainer.SplitterWidth - 20) / 2, 360, 500);
        _splitContainer.Panel1MinSize = minPanel;
        _splitContainer.Panel2MinSize = minPanel;
        var maxDistance = Math.Max(minPanel, width - _splitContainer.SplitterWidth - minPanel);
        _splitContainer.SplitterDistance = Math.Clamp(540, minPanel, maxDistance);
        Log($"界面分栏已校正：左侧宽度={_splitContainer.SplitterDistance}px；“连接选中的魔方”按钮应可见。");
    }
    private async Task CheckAdapterAsync()
    {
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter is null) { FailStatus("未找到 Windows 蓝牙适配器"); Log("BluetoothAdapter.GetDefaultAsync() 返回 null。"); return; }
            _adapterStatus.Text = $"Windows BLE：Central={adapter.IsCentralRoleSupported} / Peripheral={adapter.IsPeripheralRoleSupported} / {adapter.BluetoothAddress:X12}";
            _adapterStatus.ForeColor = adapter.IsPeripheralRoleSupported ? Color.DarkGreen : Color.DarkRed;
            Log($"适配器：Address={FormatAddress(adapter.BluetoothAddress)}; Central={adapter.IsCentralRoleSupported}; Peripheral={adapter.IsPeripheralRoleSupported}; ApiUniversal={ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 10)}; OS={Environment.OSVersion.Version}; Package={Environment.ProcessPath}");
            Log("当前交付程序应通过 MSIX 启动；如果这里的 ProcessPath 位于 C:\\Program Files\\WindowsApps，说明包身份有效。");
            Log($"本机名称={Environment.MachineName}；手机 Cube Station 按名称前缀 GAN/MG/AiCube 过滤，所以电脑名必须以 GAN 开头。");
            if (string.IsNullOrWhiteSpace(_ganMacBox.Text)) _ganMacBox.Text = FormatAddress(adapter.BluetoothAddress);
            try
            {
                foreach (var item in BridgeSelfTests.Run()) Log($"协议自检：{item}");
            }
            catch (Exception ex) { Log($"协议自检失败：{Describe(ex)}"); }
        }
        catch (Exception ex) { FailStatus("蓝牙能力检测失败"); Log($"适配器检测异常：{Describe(ex)}"); }
    }

    private static byte[]? TryParseMac(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Trim().Replace('-', ':').Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 6) return null;
        var mac = new byte[6];
        for (var i = 0; i < 6; i++)
            if (!byte.TryParse(parts[i], System.Globalization.NumberStyles.HexNumber, null, out mac[i])) return null;
        return mac;
    }

    private static byte[] ParseMacOrFallback(string text, byte[] fallback) => TryParseMac(text) ?? fallback;

    private static string FormatMac(byte[] mac) => string.Join(":", mac.Select(b => b.ToString("X2")));

    /// <summary>
    /// Cube Station derives its AES key from the BLE address it sees (or from
    /// manufacturer data). Windows advertises with a private/random LE address,
    /// so the adapter's public address may be the wrong salt. This lets the user
    /// paste the address shown by nRF Connect / Chrome without rebuilding.
    /// </summary>
    private async Task ApplyGanMacAsync()
    {
        var mac = TryParseMac(_ganMacBox.Text);
        if (mac is null) { Log("GAN MAC 格式无效，应为 6 组十六进制，例如 4C:44:7B:A3:6B:4D。"); return; }
        try
        {
            _ganCrypto?.Dispose();
            _ganCryptoMac = mac;
            _ganCrypto = new GanCrypto(mac);
            Log($"GAN 加密 MAC 已切换为 {FormatMac(mac)}；key[0..3]={Convert.ToHexString(_ganCrypto.Key.AsSpan(0, 4))}; iv[0..3]={Convert.ToHexString(_ganCrypto.Iv.AsSpan(0, 4))}");
            if (_stateCharacteristic is not null && _stateCharacteristic.SubscribedClients.Count > 0)
                await SendGanStartupStateAsync("手动切换 GAN 加密 MAC");
        }
        catch (Exception ex) { Log($"切换 GAN 加密 MAC 失败：{Describe(ex)}"); }
    }

    /// <summary>
    /// A valid GAN command frame starts with 0x04/0x05/0x09/0x0A and is almost
    /// entirely zero. Random AES output almost never satisfies this.
    /// </summary>
    private static bool LooksLikeGanCommand(byte[] plain)
    {
        if (plain.Length < 16) return false;
        if (plain[0] is not (0x04 or 0x05 or 0x09 or 0x0A)) return false;
        var zeros = 0;
        for (var i = 1; i < plain.Length; i++) if (plain[i] == 0) zeros++;
        return zeros >= 12;
    }

    private async Task StartServerAsync(Guid serviceId, Guid writeId, Guid readId, string kind)
    {
        if (_serverStarting)
        {
            Log($"{kind} GATT 服务启动正在进行，请稍候；忽略重复启动请求。");
            return;
        }

        _serverStarting = true;
        _activeServiceId = serviceId;
        _activeWriteId = writeId;
        _activeReadId = readId;
        _activeServerKind = kind;
        _startGan.Enabled = false;
        _startGeneric.Enabled = false;
        try
        {
            if (_provider is not null && IsAdvertisingStarted(_provider))
            {
                Log("已有 GATT 服务正在广播，请先停止当前广播。");
                return;
            }

            if (_provider is not null)
            {
                Log($"发现未处于广播状态的旧 GATT provider：Status={_provider.AdvertisementStatus}；先完整释放旧服务。");
                StopServer();
                await Task.Delay(700);
            }

            if (_watcher is not null)
            {
                Log("启动 GATT 外设前先停止本机 BLE 扫描，避免同一适配器 Central 扫描与 Peripheral 广播争用。");
                StopScan();
                await Task.Delay(300);
            }
            if (_rawPublisher is not null)
            {
                Log("启动 GATT 外设前停止诊断用原始广播，释放适配器广播资源。");
                StopRawAdvertisement();
            }
            StopVendorAdvertisement();

            Log($"正在创建 {kind} GATT 服务：Service={serviceId}");
            var result = await GattServiceProvider.CreateAsync(serviceId);
            Log($"CreateAsync：Error={result.Error}; ProviderNull={result.ServiceProvider is null}");
            if (result.Error != BluetoothError.Success || result.ServiceProvider is null)
                throw new InvalidOperationException($"CreateAsync 失败：BluetoothError={result.Error}");

            var provider = result.ServiceProvider;
            _provider = provider; // publish immediately so a later setup failure can release it
            GattLocalCharacteristic? write = null;
            if (writeId != Guid.Empty)
            {
                var writeResult = await provider.Service.CreateCharacteristicAsync(writeId, new GattLocalCharacteristicParameters
                {
                    CharacteristicProperties = GattCharacteristicProperties.Write | GattCharacteristicProperties.WriteWithoutResponse,
                    WriteProtectionLevel = GattProtectionLevel.Plain,
                    UserDescription = "GAN diagnostic command"
                });
                Log($"命令特征 CreateCharacteristicAsync：Error={writeResult.Error}; Null={writeResult.Characteristic is null}");
                if (writeResult.Error != BluetoothError.Success || writeResult.Characteristic is null)
                    throw new InvalidOperationException($"命令特征创建失败：BluetoothError={writeResult.Error}");
                write = writeResult.Characteristic;
                _commandCharacteristic = write;
                write.WriteRequested += OnGattWriteRequested;
                write.ReadRequested += OnGattReadRequested;
            }

            var readResult = await provider.Service.CreateCharacteristicAsync(readId, new GattLocalCharacteristicParameters
            {
                CharacteristicProperties = GattCharacteristicProperties.Read | GattCharacteristicProperties.Notify,
                ReadProtectionLevel = GattProtectionLevel.Plain,
                UserDescription = $"{kind} diagnostic state"
            });
            Log($"状态特征 CreateCharacteristicAsync：Error={readResult.Error}; Null={readResult.Characteristic is null}");
            if (readResult.Error != BluetoothError.Success || readResult.Characteristic is null)
                throw new InvalidOperationException($"状态特征创建失败：BluetoothError={readResult.Error}");
            var read = readResult.Characteristic;
            _stateCharacteristic = read;
            Log($"GAN 命令特征诊断：{(write is null ? "无（诊断服务）" : DescribeCharacteristic(write))}");
            Log($"GAN 状态特征诊断：{DescribeCharacteristic(read)}");
            Log("如果手机已完成 GATT 通知订阅，状态特征的 SubscribedClients 应大于 0；扫描列表本身不代表已连接。");

            read.ReadRequested += OnGattReadRequested;
            read.SubscribedClientsChanged += OnGattSubscribersChanged;
            provider.AdvertisementStatusChanged += OnAdvertisementStatusChanged;

            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter is null)
                throw new InvalidOperationException("BluetoothAdapter.GetDefaultAsync() 返回 null，无法发布 GATT 广播。");
            var adv = new GattServiceProviderAdvertisingParameters
            {
                IsConnectable = adapter.IsPeripheralRoleSupported,
                IsDiscoverable = true
            };
            _advParameters = adv;
            _advWanted = true;
            Log($"BLE 外设诊断：Peripheral={adapter.IsPeripheralRoleSupported}; Central={adapter.IsCentralRoleSupported}; Address={FormatAddress(adapter.BluetoothAddress)}");
            if (!adv.IsConnectable)
                throw new InvalidOperationException("当前蓝牙适配器报告 Peripheral=False，不能创建可连接的 GATT 外设广播。");
            Log($"StartAdvertising 参数：IsConnectable={adv.IsConnectable}; IsDiscoverable={adv.IsDiscoverable}; ServiceData=null");
            var advertisingStarted = await StartAdvertisingWithFallbackAsync(provider, adv);
            Log($"StartAdvertising 最终状态：AdvertisementStatus={provider.AdvertisementStatus}");
            if (!advertisingStarted)
                throw new InvalidOperationException($"广播多次启动失败，最终状态={provider.AdvertisementStatus}。请查看每次尝试对应的状态与错误码。");

            try
            {
                var adapter2 = await BluetoothAdapter.GetDefaultAsync();
                if (adapter2 is not null)
                {
                    var mac = new byte[6];
                    var addr = adapter2.BluetoothAddress;
                    for (var i = 0; i < 6; i++) mac[i] = (byte)((addr >> ((5 - i) * 8)) & 0xff);
                    _ganCrypto?.Dispose();
                    var cryptoMac = ParseMacOrFallback(_ganMacBox.Text, mac);
                    _ganCryptoMac = cryptoMac;
                    _ganCrypto = new GanCrypto(cryptoMac);
                    _ganReady = true;
                    Log($"GAN Gen2 加密器已初始化：加密 MAC={FormatMac(cryptoMac)}（适配器地址={FormatAddress(addr)}）。手机端必须用同一个 MAC 派生密钥，否则通知内容无法正确解密。");
                    Log($"GAN 派生密钥指纹：key[0..3]={Convert.ToHexString(_ganCrypto.Key.AsSpan(0, 4))}; iv[0..3]={Convert.ToHexString(_ganCrypto.Iv.AsSpan(0, 4))}");
                }
            }
            catch (Exception ex) { Log($"加密器初始化失败：{Describe(ex)}"); _ganReady = false; }

            _serverStatus.Text = $"{kind} 广播：{provider.AdvertisementStatus}";
            _stopServer.Enabled = true;
            Log($"{kind} GATT 广播成功。若手机能连接，日志会出现读取、写入或通知订阅。");
            if (kind == "GAN")
            {
                await PublishGanDeviceInfoServiceAsync();
                Log($"GAN 参考身份：name={GanAdvertisedName}; hardware=GAN356iC; firmware=1.0; CIC=0x{GanCompanyIdentifier:X4}。Windows GattServiceProvider 的可连接广播名称仍由电脑名决定（当前 {Environment.MachineName}）。");
                Log("GAN 真机辅助广播不会自动启动；如需单独诊断，请手动点击“诊断：厂商数据广播（GAN 真机格式）”。");
            }

            _nextAdvertisementRecoveryUtc = DateTime.MinValue;
            if (_pureMode) { StopAdvWatchdog(); Log("纯净模式：不启动广播心跳，也不会自动重启广播；本程序当前没有任何主动发送动作。"); }
            else StartAdvWatchdog();
            Log($"开始对外广播，本机名称={Environment.MachineName}。请只用手机 Cube Station 扫描，不要同时启动本机魔域扫描。");
        }
        catch (Exception ex)
        {
            Log($"{kind} GATT 启动失败：{Describe(ex)}");
            StopServer();
        }
        finally
        {
            _serverStarting = false;
            if (!_closing)
            {
                _startGan.Enabled = _provider is null || !IsAdvertisingStarted(_provider);
                _startGeneric.Enabled = _provider is null || !IsAdvertisingStarted(_provider);
            }
        }
    }
    private static bool IsAdvertisingStarted(GattServiceProvider provider) =>
        provider.AdvertisementStatus is GattServiceProviderAdvertisementStatus.Started or
        GattServiceProviderAdvertisementStatus.StartedWithoutAllAdvertisementData;

    /// <summary>
    /// Windows sometimes aborts a connectable GATT advertisement while still
    /// accepting CreateAsync and characteristic creation. The common trigger is
    /// the adapter refusing to fit Flags + a 128-bit service UUID + local name in
    /// the discoverable legacy advertisement. Retry the same service with
    /// IsDiscoverable=false; the service remains connectable and can still be
    /// found by active BLE scanners using its UUID/name.
    /// </summary>
    private async Task<bool> StartAdvertisingWithFallbackAsync(
        GattServiceProvider provider,
        GattServiceProviderAdvertisingParameters requested)
    {
        var attempts = new[]
        {
            (Discoverable: requested.IsDiscoverable, TimeoutMs: 3500),
            (Discoverable: false, TimeoutMs: 4500),
            (Discoverable: false, TimeoutMs: 5500)
        };

        for (var i = 0; i < attempts.Length; i++)
        {
            var attempt = attempts[i];
            var parameters = new GattServiceProviderAdvertisingParameters
            {
                IsConnectable = requested.IsConnectable,
                IsDiscoverable = attempt.Discoverable
            };
            _advParameters = parameters;

            if (provider.AdvertisementStatus != GattServiceProviderAdvertisementStatus.Stopped)
            {
                try { provider.StopAdvertising(); }
                catch (Exception ex) { Log($"广播第 {i + 1} 次尝试前停止旧请求提示：{Describe(ex)}"); }
                if (!await WaitForAdvertisementStoppedAsync(provider, 2500))
                {
                    Log($"广播第 {i + 1} 次尝试取消失败：停止等待超时，Provider={provider.AdvertisementStatus}；不叠加新的 StartAdvertising 请求。");
                    continue;
                }
            }

            try
            {
                Log($"GATT 广播启动尝试 {i + 1}/{attempts.Length}：IsConnectable={parameters.IsConnectable}; IsDiscoverable={parameters.IsDiscoverable}; 当前状态={provider.AdvertisementStatus}");
                provider.StartAdvertising(parameters);
            }
            catch (Exception ex)
            {
                Log($"GATT 广播启动尝试 {i + 1} 的 StartAdvertising 抛出异常：{Describe(ex)}");
                continue;
            }

            var started = await WaitForAdvertisingStartedAsync(provider, attempt.TimeoutMs);
            Log($"GATT 广播启动尝试 {i + 1} 结果：Status={provider.AdvertisementStatus}; Started={started}");
            if (started)
            {
                _advParameters = parameters;
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> WaitForAdvertisingStartedAsync(GattServiceProvider provider, int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (IsAdvertisingStarted(provider)) return true;
            if (provider.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Aborted) return false;
            await Task.Delay(100);
        }
        return IsAdvertisingStarted(provider);
    }

    private static async Task<bool> WaitForAdvertisementStoppedAsync(GattServiceProvider provider, int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (provider.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Stopped) return true;
            await Task.Delay(100);
        }
        return provider.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Stopped;
    }

    /// <summary>
    /// cubebridge exposes a standard Device Information Service carrying GAN identity
    /// strings. Cube Station may read these to decide whether the peer is a known cube.
    /// </summary>
    private async Task PublishGanDeviceInfoServiceAsync()
    {
        try
        {
            var created = await GattServiceProvider.CreateAsync(DisService);
            Log($"GAN 设备信息服务 CreateAsync：Error={created.Error}; ProviderNull={created.ServiceProvider is null}");
            if (created.Error != BluetoothError.Success || created.ServiceProvider is null)
            {
                Log("GAN 设备信息服务未能创建（可能与系统自带 0x180A 冲突）。");
                return;
            }

            var provider = created.ServiceProvider;
            var entries = new (Guid Uuid, string Name, string Value)[]
            {
                (DisModel, "型号", "GAN356iCarry"),
                (DisFirmware, "固件", "1.0"),
                (DisHardware, "硬件", "1.1"),
                (DisVendor, "厂商", "GANCUBE")
            };

            var ok = 0;
            foreach (var entry in entries)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(entry.Value);
                var result = await provider.Service.CreateCharacteristicAsync(entry.Uuid, new GattLocalCharacteristicParameters
                {
                    CharacteristicProperties = GattCharacteristicProperties.Read,
                    ReadProtectionLevel = GattProtectionLevel.Plain,
                    StaticValue = CryptographicBuffer.CreateFromByteArray(bytes),
                    UserDescription = $"GAN DIS {entry.Name}"
                });
                if (result.Error == BluetoothError.Success && result.Characteristic is not null)
                {
                    ok++;
                    Log($"  DIS {entry.Name} = \"{entry.Value}\" 已发布");
                }
                else
                {
                    Log($"  DIS {entry.Name} 创建失败：Error={result.Error}");
                }
            }

            _disProvider = provider;
            Log($"GAN 设备信息服务发布完成：{ok}/4 个特征。Cube Station 读取型号时应看到 GAN356iCarry。");
        }
        catch (Exception ex)
        {
            Log($"GAN 设备信息服务异常：{Describe(ex)}");
        }
    }

    private void OnAdvertisementStatusChanged(GattServiceProvider sender, GattServiceProviderAdvertisementStatusChangedEventArgs args)
    {
        SafeUi(() =>
        {
            Log($"AdvertisementStatusChanged：Status={args.Status}; Error={args.Error}; ProviderStatus={sender.AdvertisementStatus}");
            if (args.Status == GattServiceProviderAdvertisementStatus.Aborted &&
                ReferenceEquals(sender, _provider) && _advWanted && !_serverStarting)
            {
                RequestAdvertisementRecovery("状态事件报告 Aborted");
            }
        });
    }

    private async void OnGattWriteRequested(GattLocalCharacteristic sender, GattWriteRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var request = await args.GetRequestAsync();
            if (request is null) return;
            var reader = DataReader.FromBuffer(request.Value);
            var raw = new byte[(int)reader.UnconsumedBufferLength];
            reader.ReadBytes(raw);
            SafeUi(() => Log($"远端写入 {sender.Uuid}（加密 {raw.Length} 字节）：{Convert.ToHexString(raw)}; Option={request.Option}"));

            if (_ganCrypto is null)
            {
                SafeUi(() => Log("加密器未初始化，无法解析命令；请确认适配器地址可用。"));
                if (request.Option == GattWriteOption.WriteWithResponse) request.Respond();
                return;
            }

            byte[] plain;
            try { plain = _ganCrypto.Decode(raw); }
            catch (Exception ex) { SafeUi(() => Log($"解密失败：{Describe(ex)}")); plain = Array.Empty<byte>(); }

            if (plain.Length > 0 && !LooksLikeGanCommand(plain))
            {
                SafeUi(() => Log($"解密结果不像 GAN 命令（首字节=0x{plain[0]:X2}），说明手机使用了不同的 MAC 派生密钥；请把 nRF Connect 里 GAN356IC 的地址填到“GAN MAC”并点“应用 GAN MAC”。"));
                // Diagnostic: if the adapter address differs from the current
                // salt, show whether it would have produced a valid command.
                try
                {
                    var adapter = await BluetoothAdapter.GetDefaultAsync();
                    if (adapter is not null)
                    {
                        var am = new byte[6];
                        for (var i = 0; i < 6; i++) am[i] = (byte)((adapter.BluetoothAddress >> ((5 - i) * 8)) & 0xff);
                        if (_ganCryptoMac is null || !am.SequenceEqual(_ganCryptoMac))
                        {
                            using var probe = new GanCrypto(am);
                            var alt = probe.Decode(raw);
                            SafeUi(() => Log($"适配器 MAC {FormatAddress(adapter.BluetoothAddress)} 解密结果：{Convert.ToHexString(alt)}（像 GAN 命令={LooksLikeGanCommand(alt)}）"));
                        }
                        // Some app builds fall back to an all-zero MAC when the
                        // advertisement carries no manufacturer data.
                        var zero = new byte[6];
                        if (_ganCryptoMac is null || !zero.SequenceEqual(_ganCryptoMac))
                        {
                            using var probe = new GanCrypto(zero);
                            var alt = probe.Decode(raw);
                            SafeUi(() => Log($"全零 MAC 解密结果：{Convert.ToHexString(alt)}（像 GAN 命令={LooksLikeGanCommand(alt)}）"));
                        }
                        // Also try the reverse byte order, in case the app uses
                        // little-endian address bytes rather than display order.
                        if (_ganCryptoMac is not null)
                        {
                            var reversed = _ganCryptoMac.Reverse().ToArray();
                            using var probe = new GanCrypto(reversed);
                            var alt = probe.Decode(raw);
                            SafeUi(() => Log($"反向 MAC {FormatMac(reversed)} 解密结果：{Convert.ToHexString(alt)}（像 GAN 命令={LooksLikeGanCommand(alt)}）"));
                        }
                    }
                }
                catch { }
            }

            if (plain.Length > 0)
            {
                var opcode = plain[0];
                SafeUi(() => Log($"解密后明文（{plain.Length} 字节）：{Convert.ToHexString(plain)}；命令码=0x{opcode:X2}"));
                await RespondToCommandAsync(opcode, plain);
            }

            if (request.Option == GattWriteOption.WriteWithResponse) request.Respond();
        }
        catch (Exception ex) { SafeUi(() => Log($"处理远端写入失败：{Describe(ex)}")); }
        finally { deferral.Complete(); }
    }

    private async Task RespondToCommandAsync(byte opcode, byte[] plain)
    {
        try
        {
            if (opcode == 0x04)
            {
                _ganFaceletsRequested = true;
                if (_sourceCube is null)
                {
                    Log("Cube Station 请求面状态，但魔域尚未同步；先挂起请求，连接并同步魔域后再回应，避免发错误的已复原状态。");
                    return;
                }
                await SendGanFaceletsAsync();
                return;
            }

            byte[] payload = opcode switch
            {
                0x05 => GanNotifications.HardwareInfo(1, 1, 1, 0, "GAN356iC", gyroEnabled: false),
                0x09 => GanNotifications.Battery(_moyuBattery ?? 80),
                _ => Array.Empty<byte>()
            };

            if (payload.Length == 0)
            {
                SafeUi(() => Log($"命令 0x{opcode:X2} 暂无对应响应（仅记录）。"));
                return;
            }

            await SendGanNotificationAsync(payload, opcode == 0x05 ? "硬件信息" : "电量");
        }
        catch (Exception ex) { SafeUi(() => Log($"发送通知失败：{Describe(ex)}")); }
    }

    private async Task SendTestNotificationAsync()
    {
        try
        {
            if (_ganCrypto is null || _stateCharacteristic is null) { Log("请先启动 GAN 广播。"); return; }
            Log($"通知诊断按钮：{DescribeCharacteristic(_stateCharacteristic)}");
            Log($"通知前客户端数={_stateCharacteristic.SubscribedClients.Count}；{DescribeSubscribedClients(_stateCharacteristic.SubscribedClients)}");
            var payload = GanNotifications.Battery(80);
            var encrypted = _ganCrypto.Encode(payload);
            var results = await _stateCharacteristic.NotifyValueAsync(CryptographicBuffer.CreateFromByteArray(encrypted));
            Log($"已发送测试电量通知（{DescribeNotifyResults(results)}）。");
        }
        catch (Exception ex) { Log($"发送测试通知失败：{Describe(ex)}"); }
    }

    private async void OnGattReadRequested(GattLocalCharacteristic sender, GattReadRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var request = await args.GetRequestAsync();
            if (request is null) return;
            var value = sender == _stateCharacteristic ? new byte[20] : Array.Empty<byte>();
            request.RespondWithValue(CryptographicBuffer.CreateFromByteArray(value));
            SafeUi(() => Log($"远端读取 {sender.Uuid}；返回 {value.Length} 字节占位数据。"));
        }
        catch (Exception ex) { SafeUi(() => Log($"处理远端读取失败：{Describe(ex)}")); }
        finally { deferral.Complete(); }
    }

    private async void OnGattSubscribersChanged(GattLocalCharacteristic sender, object args)
    {
        var clients = sender.SubscribedClients;
        var count = clients.Count;
        var detail = DescribeSubscribedClients(clients);
        SafeUi(() => Log($"远端通知订阅变化：Characteristic={sender.Uuid}; Count={count}; {detail}"));
        if (count > 0 && sender == _stateCharacteristic)
            await SendGanStartupStateAsync("手机订阅状态特征");
    }

    private static string DescribeCharacteristic(GattLocalCharacteristic characteristic)
    {
        var descriptors = characteristic.Descriptors.Select(d => d.Uuid.ToString()).ToArray();
        var cccd = descriptors.Any(id => id.Equals("00002902-0000-1000-8000-00805f9b34fb", StringComparison.OrdinalIgnoreCase));
        return $"UUID={characteristic.Uuid}; Properties={characteristic.CharacteristicProperties}; DescriptorCount={descriptors.Length}; CCCDListed={cccd}; Descriptors=[{string.Join(",", descriptors)}]";
    }

    private static string DescribeSubscribedClients(IReadOnlyList<GattSubscribedClient> clients)
    {
        if (clients.Count == 0) return "[]";
        return "[" + string.Join("; ", clients.Select(c =>
            $"DeviceId={c.Session.DeviceId.Id}; Session={c.Session.SessionStatus}; MaxNotify={c.MaxNotificationSize}; Maintain={c.Session.CanMaintainConnection}")) + "]";
    }

    private BluetoothLEAdvertisementPublisher? _vendorPublisher;

    /// <summary>
    /// Adds a GAN-shaped manufacturer-data advertisement while keeping the
    /// connectable GattServiceProvider advertisement active. Windows' raw publisher
    /// has no IsConnectable option and may be a separate advertisement; therefore
    /// it is diagnostic only and is not assumed to be coalesced with the GATT advert.
    /// </summary>
    private void ToggleVendorAdvertisement()
    {
        if (_vendorPublisher is not null)
        {
            StopVendorAdvertisement();
            return;
        }
        StartGanCompatibilityAdvertisement();
    }

    /// <summary>
    /// Starts the closest Windows-only approximation of cubebridge's exact GAN
    /// advertisement. Windows GattServiceProvider owns the connectable GATT
    /// advertisement and does not expose manufacturer data, so a second raw
    /// advertisement carries the GAN CIC, MAC payload and GAN356iCarry name.
    /// </summary>
    private void StartGanCompatibilityAdvertisement()
    {
        if (_provider is null)
        {
            Log("GAN 真机辅助广播：请先启动 GAN GATT 广播（服务必须已注册）。");
            return;
        }
        if (_vendorPublisher is not null)
        {
            Log("GAN 真机辅助广播已经在运行。");
            return;
        }

        try
        {
            var adapter = BluetoothAdapter.GetDefaultAsync().AsTask().GetAwaiter().GetResult();
            if (adapter is null) { Log("GAN 真机辅助广播：没有可用适配器。"); return; }
            var address = adapter.BluetoothAddress;
            var mac = new byte[6];
            for (var i = 0; i < 6; i++) mac[i] = (byte)((address >> ((5 - i) * 8)) & 0xff);
            // cubebridge uses an on-air manufacturer payload of:
            // 00 00 00 + display MAC reversed. The company identifier is 0x0101.
            var manufacturer = new byte[9];
            for (var i = 0; i < 6; i++) manufacturer[3 + i] = mac[5 - i];

            Log($"保持 GattServiceProvider 广播不动（{_provider.AdvertisementStatus}），启动 GAN 真机格式辅助广播；Name={GanAdvertisedName}; CIC=0x{GanCompanyIdentifier:X4}; MAC payload={FormatAddress(address)}; Data={Convert.ToHexString(manufacturer)}。");
            _vendorPublisher = TryCreateVendorPublisher(manufacturer);
            _vendorAdv.Text = "诊断：停止 GAN 真机辅助广播";
            Log("GAN 真机格式辅助广播已启动。它本身不可设置为 Connectable；可连接 GATT 仍由上方 GattServiceProvider 提供。若 Cube Station 仍不订阅，请用 Chrome/nRF Connect确认手机看到的 Name、CIC=0x0101 与 9 字节数据。");
        }
        catch (Exception ex)
        {
            // Do not tear down the working GATT service if Windows rejects the
            // optional raw advertisement (for example because the controller
            // cannot schedule two advertisement sets).
            Log($"GAN 真机辅助广播启动失败（GATT 广播保持原状）：{Describe(ex)}");
        }
    }

    private BluetoothLEAdvertisementPublisher TryCreateVendorPublisher(byte[] manufacturer)
    {
        BluetoothLEAdvertisementPublisher? publisher = null;
        var stage = "create publisher";
        try
        {
            Log($"创建 GAN 兼容广播：name={GanAdvertisedName}; CIC=0x{GanCompanyIdentifier:X4}; manufacturer payload={manufacturer.Length} bytes。");
            publisher = new BluetoothLEAdvertisementPublisher();
            stage = "set flags and local name";
            publisher.Advertisement.Flags = BluetoothLEAdvertisementFlags.GeneralDiscoverableMode | BluetoothLEAdvertisementFlags.ClassicNotSupported;
            publisher.Advertisement.LocalName = GanAdvertisedName;
            stage = "add manufacturer data";
            publisher.Advertisement.ManufacturerData.Add(new BluetoothLEManufacturerData(GanCompanyIdentifier, CryptographicBuffer.CreateFromByteArray(manufacturer)));
            stage = "attach status event";
            publisher.StatusChanged += OnVendorAdvStatusChanged;
            stage = "Start()";
            publisher.Start();
            Log($"GAN 兼容广播 Start() 已调用；同步状态={publisher.Status}。Name={GanAdvertisedName}; CIC=0x{GanCompanyIdentifier:X4}。");
            return publisher;
        }
        catch (Exception ex)
        {
            if (publisher is not null)
            {
                try { publisher.Stop(); publisher.StatusChanged -= OnVendorAdvStatusChanged; } catch { }
            }
            throw new InvalidOperationException($"阶段={stage}; {ex.GetType().Name}: {ex.Message}; HRESULT=0x{ex.HResult:X8}", ex);
        }
    }

    private void StopVendorAdvertisement()
    {
        if (_vendorPublisher is null) return;
        try { _vendorPublisher.Stop(); _vendorPublisher.StatusChanged -= OnVendorAdvStatusChanged; }
        catch (Exception ex) { Log($"停止厂商数据广播时提示：{Describe(ex)}"); }
        _vendorPublisher = null;
        _vendorAdv.Text = "诊断：厂商数据广播（GAN 真机格式）";
        Log("GAN 真机辅助广播已停止；GattServiceProvider 广播保持原状。");
    }
    private void OnVendorAdvStatusChanged(BluetoothLEAdvertisementPublisher sender, BluetoothLEAdvertisementPublisherStatusChangedEventArgs args)
    {
        SafeUi(() => Log($"厂商数据广播状态：{args.Status}; Error={args.Error}"));
    }

    private void ToggleRawAdvertisement()
    {
        try
        {
            if (_rawPublisher is not null)
            {
                StopRawAdvertisement();
                return;
            }
            var publisher = new BluetoothLEAdvertisementPublisher();
            Log("步骤1：BluetoothLEAdvertisementPublisher 已创建。");
            Log("步骤2：保持默认广播内容；这里只诊断 Windows 原始 BLE 广播 API，不提供可连接的 GATT 服务，也不等同于 GAN 广播。");
            publisher.StatusChanged += OnRawAdvStatusChanged;
            publisher.Start();
            _rawPublisher = publisher;
            Log("步骤3：已调用 Start()，等待状态事件。本测试不使用任何自定义内容，只验证本机适配器能否对外发布 BLE 广播。");
        }
        catch (Exception ex) { Log($"原始 BLE 广播启动失败：{Describe(ex)}"); }
    }

    private void StopRawAdvertisement()
    {
        if (_rawPublisher is null) return;
        try { _rawPublisher.Stop(); _rawPublisher.StatusChanged -= OnRawAdvStatusChanged; }
        catch (Exception ex) { Log($"停止原始 BLE 广播时提示：{Describe(ex)}"); }
        _rawPublisher = null;
        _rawAdv.Text = "诊断：原始广播能力";
        Log("原始 BLE 广播已停止。");
    }

    private void OnRawAdvStatusChanged(BluetoothLEAdvertisementPublisher sender, BluetoothLEAdvertisementPublisherStatusChangedEventArgs args)
    {
        SafeUi(() =>
        {
            Log($"原始广播状态：{args.Status}; Error={args.Error}");
            _rawAdv.Text = $"原始广播：{args.Status}";
        });
    }

    private void StopServer()
    {
        _advWanted = false;
        _advertisementRecoveryInProgress = false;
        _nextAdvertisementRecoveryUtc = DateTime.MinValue;
        StopAdvWatchdog();
        var provider = _provider;
        if (provider is not null)
        {
            try
            {
                if (provider.AdvertisementStatus != GattServiceProviderAdvertisementStatus.Stopped) provider.StopAdvertising();
            }
            catch (Exception ex) { Log($"停止 GATT 广播时提示：{Describe(ex)}"); }
            try { if (_commandCharacteristic is not null) { _commandCharacteristic.WriteRequested -= OnGattWriteRequested; _commandCharacteristic.ReadRequested -= OnGattReadRequested; } } catch { }
            try { if (_stateCharacteristic is not null) { _stateCharacteristic.ReadRequested -= OnGattReadRequested; _stateCharacteristic.SubscribedClientsChanged -= OnGattSubscribersChanged; } } catch { }
            try { provider.AdvertisementStatusChanged -= OnAdvertisementStatusChanged; } catch { }
        }
        _provider = null;
        _commandCharacteristic = null;
        _stateCharacteristic = null;
        _ganReady = false;
        try { _ganCrypto?.Dispose(); } catch { }
        _ganCrypto = null;
        _ganCryptoMac = null;
        _advParameters = null;
        _disProvider = null;
        StopVendorAdvertisement();
        StopRawAdvertisement();
        _serverStatus.Text = "广播：未启动";
        _stopServer.Enabled = false;
        _startGan.Enabled = !_closing && !_serverStarting;
        _startGeneric.Enabled = !_closing && !_serverStarting;
        Log("GATT 服务和广播资源已停止并解除事件订阅。");
    }

    private void StartAdvWatchdog()
    {
        StopAdvWatchdog();
        _advHeartbeat = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 2000 };
        timer.Tick += OnAdvWatchdogTick;
        timer.Start();
        _advWatchdog = timer;
    }

    private void StopAdvWatchdog()
    {
        if (_advWatchdog is null) return;
        try { _advWatchdog.Stop(); _advWatchdog.Tick -= OnAdvWatchdogTick; _advWatchdog.Dispose(); }
        catch { }
        _advWatchdog = null;
    }

    private void OnAdvWatchdogTick(object? sender, EventArgs e)
    {
        if (_closing || !_advWanted || _serverStarting || _advertisementRecoveryInProgress) return;
        var provider = _provider;
        if (provider is null) return;
        var status = provider.AdvertisementStatus;
        _advHeartbeat++;
        var scanning = _watcher is not null && _watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started;
        if (IsAdvertisingStarted(provider))
        {
            _advertisementRecoveryFailures = 0;
            _nextAdvertisementRecoveryUtc = DateTime.MinValue;
            var notifyClients = _stateCharacteristic?.SubscribedClients.Count ?? 0;
            var vendorStatus = _vendorPublisher?.Status.ToString() ?? "off";
            Log($"广播心跳 #{_advHeartbeat}：GATT={status}；厂商广播={vendorStatus}；GAN通知订阅数={notifyClients}；本机扫描中={scanning}；魔域={(_moyuDevice is null ? "未连接" : _moyuDevice.ConnectionStatus.ToString())}");
            return;
        }
        Log($"⚠ 广播心跳 #{_advHeartbeat}：状态={status}；本机扫描中={scanning}。GATT 广播已停止/中止。");
        RequestAdvertisementRecovery($"心跳检测到 {status}");
    }

    private void RequestAdvertisementRecovery(string reason)
    {
        if (_closing || !_advWanted || _serverStarting || _advertisementRecoveryInProgress) return;
        if (DateTime.UtcNow < _nextAdvertisementRecoveryUtc) return;
        _ = RecoverAdvertisementAsync(reason);
    }

    private async Task RecoverAdvertisementAsync(string reason)
    {
        if (_closing || !_advWanted || _serverStarting || _advertisementRecoveryInProgress) return;
        var provider = _provider;
        if (provider is null) return;
        _advertisementRecoveryInProgress = true;
        try
        {
            Log($"开始 BLE 广播恢复：原因={reason}; Provider={provider.AdvertisementStatus}。");
            if (_watcher is not null) { Log("广播异常期间停止本机 BLE 扫描，先释放 Central radio。"); StopScan(); }
            if (_rawPublisher is not null) StopRawAdvertisement();
            StopVendorAdvertisement();
            await Task.Delay(700);
            if (!ReferenceEquals(provider, _provider) || !_advWanted || _closing) return;
            var parameters = _advParameters ?? new GattServiceProviderAdvertisingParameters { IsConnectable = true, IsDiscoverable = true };
            if (await StartAdvertisingWithFallbackAsync(provider, parameters))
            {
                _advertisementRecoveryFailures = 0;
                _nextAdvertisementRecoveryUtc = DateTime.MinValue;
                _serverStatus.Text = $"GAN 广播：{provider.AdvertisementStatus}";
                Log($"BLE 广播恢复成功：{provider.AdvertisementStatus}。");
                return;
            }
            _advertisementRecoveryFailures = Math.Min(_advertisementRecoveryFailures + 1, 6);
            if (_advertisementRecoveryFailures >= 3 && _activeServiceId != Guid.Empty)
            {
                Log($"BLE 广播连续 {_advertisementRecoveryFailures} 次恢复失败；释放旧 GATT provider 并重建 {_activeServerKind} 服务，避免 Windows 保留已中止的旧广播句柄。");
                StopServer();
                await Task.Delay(1000);
                if (!_closing) await StartServerAsync(_activeServiceId, _activeWriteId, _activeReadId, _activeServerKind);
                return;
            }
            var cooldownSeconds = Math.Min(5 * (1 << (_advertisementRecoveryFailures - 1)), 60);
            _nextAdvertisementRecoveryUtc = DateTime.UtcNow.AddSeconds(cooldownSeconds);
            Log($"BLE 广播恢复失败（连续 {_advertisementRecoveryFailures} 次）；{cooldownSeconds} 秒后再尝试。请检查适配器状态、Windows 蓝牙服务和其他程序占用。");
        }
        catch (Exception ex)
        {
            _advertisementRecoveryFailures = Math.Min(_advertisementRecoveryFailures + 1, 6);
            var cooldownSeconds = Math.Min(5 * (1 << (_advertisementRecoveryFailures - 1)), 60);
            _nextAdvertisementRecoveryUtc = DateTime.UtcNow.AddSeconds(cooldownSeconds);
            Log($"BLE 广播恢复异常：{Describe(ex)}；{cooldownSeconds} 秒后再尝试。");
        }
        finally { _advertisementRecoveryInProgress = false; }
    }

    private void TogglePhoneTestMode()
    {
        if (_phoneTestMode) ExitPhoneTestMode();
        else EnterPhoneTestMode();
    }

    private void EnterPhoneTestMode()
    {
        _phoneTestMode = true;
        _pureMode = true;
        StopScan();
        StopAdvWatchdog();
        if (_provider is null)
        {
            Log("手机测试模式：GAN 广播尚未启动，正在启动……");
            _ = StartServerAsync(GanService, GanCommand, GanState, "GAN");
        }
        else
        {
            Log("手机测试模式：保留当前 GAN 广播，已停止广播心跳和自动重启。");
        }
        _scan.Enabled = false; _stopScan.Enabled = false;
        _phoneTest.Text = "① 退出手机测试模式（恢复魔域扫描）";
        _modeStatus.Text = "模式：手机测试（魔域扫描已停止）";
        _modeStatus.ForeColor = Color.DarkOrange;
        Log("手机测试模式已开启：本机不再扫描魔域，没有心跳，没有自动重广播，也没有主动发送任何通知。");
        Log("现在请用手机 Cube Station 扫描 GAN-TEST；如果 Cube Station 有搜索框，请先清空它，看“全部”列表。不要从 Windows 设置里配对。");
    }

    private void ExitPhoneTestMode()
    {
        _phoneTestMode = false;
        _pureMode = false;
        _scan.Enabled = !_closing;
        _phoneTest.Text = "① 手机测试：只广播（停止魔域扫描）";
        _modeStatus.Text = "模式：正常";
        _modeStatus.ForeColor = Color.DarkGray;
        Log("已退出手机测试模式，现在可以点击“扫描魔域魔方”。");
        if (_provider is not null && _advWanted && _advWatchdog is null) StartAdvWatchdog();
    }

    private static readonly Guid CccdUuid = Guid.Parse("00002902-0000-1000-8000-00805f9b34fb");

    /// <summary>
    /// Asks Windows whether the state characteristic already owns a CCCD. Creating a
    /// duplicate would corrupt the service, so this only reports what the stack says.
    /// </summary>
    private async Task ProbeCccdAsync()
    {
        if (_stateCharacteristic is null)
        {
            Log("CCCD 探测：请先启动 GAN 广播。");
            return;
        }

        Log($"CCCD 探测：状态特征当前描述符数={_stateCharacteristic.Descriptors.Count}；Properties={_stateCharacteristic.CharacteristicProperties}。");
        try
        {
            var existing = _stateCharacteristic.Descriptors.FirstOrDefault(d => d.Uuid == CccdUuid);
            if (existing is not null)
            {
                Log("CCCD 探测：已存在 CCCD 描述符。");
                return;
            }

            var result = await _stateCharacteristic.CreateDescriptorAsync(CccdUuid, new GattLocalDescriptorParameters
            {
                ReadProtectionLevel = GattProtectionLevel.Plain,
                WriteProtectionLevel = GattProtectionLevel.Plain
            });
            Log($"CCCD 探测：CreateDescriptorAsync 结果 Error={result.Error}; 描述符为空={result.Descriptor is null}");
            Log($"CCCD 探测：创建后描述符数={_stateCharacteristic.Descriptors.Count}。");
            if (result.Error == BluetoothError.Success)
                Log("CCCD 探测：Windows 允许显式创建 CCCD；如果这造成重复描述符，请停止广播后重新启动。");
            else
                Log("CCCD 探测：创建被拒绝，通常表示 Windows 已自动维护 CCCD，问题不在描述符缺失。");
        }
        catch (Exception ex)
        {
            Log($"CCCD 探测异常：{Describe(ex)}");
        }
    }

    /// <summary>
    /// Connects to our own GATT server as a client so the service database and the
    /// notification subscription path can be verified without a phone.
    /// </summary>
    private async Task RunLoopbackGattCheckAsync()
    {
        if (_provider is null || _stateCharacteristic is null)
        {
            Log("回环自检：请先启动 GAN 广播。");
            return;
        }

        BluetoothLEDevice? device = null;
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter is null) { Log("回环自检：没有可用适配器。"); return; }
            var address = adapter.BluetoothAddress;
            Log($"回环自检开始：以本机身份连接自己的 GATT 服务 {FormatAddress(address)}。");
            device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
            if (device is null)
            {
                Log("回环自检：FromBluetoothAddressAsync 返回 null，Windows 拒绝本机回环连接；此路不可用，需要手机侧验证。");
                return;
            }

            var services = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            Log($"回环自检：服务发现 Status={services.Status}; Count={services.Services.Count}");
            if (services.Status != GattCommunicationStatus.Success)
            {
                Log($"回环自检：服务发现失败（{services.Status}），Windows 本机回环可能不被支持。");
                return;
            }

            foreach (var service in services.Services)
            {
                Log($"  回环服务：{service.Uuid}");
                var chars = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                Log($"    特征数={chars.Characteristics.Count}; Status={chars.Status}");
                foreach (var ch in chars.Characteristics)
                {
                    var descriptors = await ch.GetDescriptorsAsync(BluetoothCacheMode.Uncached);
                    var descriptorIds = descriptors.Descriptors.Select(d => d.Uuid.ToString()).ToArray();
                    Log($"    回环特征：{ch.Uuid}; Properties={ch.CharacteristicProperties}; 描述符数={descriptorIds.Length}; 描述符=[{string.Join(",", descriptorIds)}]");

                    if (ch.Uuid != GanState) continue;
                    var subscribe = await ch.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    Log($"    回环订阅状态特征结果：{subscribe}");
                    await Task.Delay(600);
                    Log($"    回环订阅后服务端 SubscribedClients={_stateCharacteristic.SubscribedClients.Count}");
                    var encoded = _ganCrypto is null ? Array.Empty<byte>() : _ganCrypto.Encode(GanNotifications.Battery(77));
                    if (encoded.Length > 0)
                    {
                        var results = await _stateCharacteristic.NotifyValueAsync(CryptographicBuffer.CreateFromByteArray(encoded));
                        Log($"    回环测试通知：{DescribeNotifyResults(results)}");
                    }
                    var unsubscribe = await ch.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                    Log($"    回环取消订阅结果：{unsubscribe}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"回环自检失败：{Describe(ex)}");
        }
        finally
        {
            try { device?.Dispose(); } catch { }
            Log("回环自检结束。");
        }
    }

    private void ExportLog()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GanBridge日志");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"ganbridge-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(file, _log.Text, new System.Text.UTF8Encoding(false));
            Log($"日志已导出：{file}");
        }
        catch (Exception ex) { Log($"导出日志失败：{Describe(ex)}"); }
    }

    private void StartScan()
    {
        if (_phoneTestMode) ExitPhoneTestMode();
        StopScan(); _devices.Items.Clear(); _seen.Clear(); _nextDiscoveryOrder = 0;
        try
        {
            var scanMode = _provider is not null && IsAdvertisingStarted(_provider)
                ? BluetoothLEScanningMode.Passive
                : BluetoothLEScanningMode.Active;
            var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = scanMode, AllowExtendedAdvertisements = false };
            watcher.Received += OnAdvertisementReceived; watcher.Stopped += OnWatcherStopped; _watcher = watcher; watcher.Start();
            _scan.Enabled = false; _stopScan.Enabled = true;
            Log($"开始 BLE {scanMode} 扫描；未按服务 UUID 过滤。GAN 广播活动时使用被动扫描，避免主动 Scan Request 与 Peripheral 广播争用；未广播时使用主动扫描以获取完整设备名称。请唤醒 V10 AI，先退出魔域官方 App。注意观察上面“广播心跳”是否继续。");
        }
        catch (Exception ex) { Log($"BLE 扫描启动失败：{Describe(ex)}"); StopScan(); }
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var manufacturer = "无";
        try
        {
            manufacturer = string.Join("; ", args.Advertisement.ManufacturerData.Select(m =>
            {
                var reader = DataReader.FromBuffer(m.Data); var bytes = new byte[(int)reader.UnconsumedBufferLength]; reader.ReadBytes(bytes);
                return $"0x{m.CompanyId:X4}:{Convert.ToHexString(bytes)}";
            }));
        }
        catch { manufacturer = "读取失败"; }
        var services = string.Join(",", args.Advertisement.ServiceUuids.Select(x => x.ToString()));
        var item = new CubeAdvertisement { Address = args.BluetoothAddress, Name = string.IsNullOrWhiteSpace(args.Advertisement.LocalName) ? "(无名称)" : args.Advertisement.LocalName, Rssi = args.RawSignalStrengthInDBm, Manufacturer = manufacturer, Services = string.IsNullOrWhiteSpace(services) ? "无" : services, DiscoveryOrder = ++_nextDiscoveryOrder };
        SafeUi(() =>
        {
            if (_seen.TryGetValue(item.Address, out var existing))
            {
                existing.Name = item.Name; existing.Rssi = item.Rssi; existing.Manufacturer = item.Manufacturer; existing.Services = item.Services;
                ReorderDeviceList(existing);
            }
            else
            {
                _seen[item.Address] = item;
                _devices.Items.Add(item);
                ReorderDeviceList(item);
                if (IsLikelyMoYu(item)) Log($"发现疑似魔域设备：{item}");
            }
        });
    }

    private static bool IsLikelyMoYu(CubeAdvertisement item) =>
        item.LooksLikeMoYu || item.Services.Contains(MoYuService.ToString(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Keep all likely MoYu devices in a stable first group. Within each group,
    /// preserve discovery order instead of sorting by RSSI, because RSSI changes
    /// on every advertisement and otherwise makes the selected item jump.
    /// </summary>
    private void ReorderDeviceList(CubeAdvertisement changed)
    {
        var selected = _devices.SelectedItem as CubeAdvertisement;
        var ordered = _seen.Values
            .OrderByDescending(IsLikelyMoYu)
            .ThenBy(x => x.DiscoveryOrder)
            .ThenBy(x => x.Address)
            .ToList();
        _devices.BeginUpdate();
        try
        {
            _devices.Items.Clear();
            foreach (var item in ordered) _devices.Items.Add(item);
            if (selected is not null && _devices.Items.Contains(selected)) _devices.SelectedItem = selected;
            else if (_devices.Items.Contains(changed)) _devices.SelectedItem = changed;
        }
        finally { _devices.EndUpdate(); }
    }

    private void OnWatcherStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args) => SafeUi(() => Log($"BLE 扫描停止：{args.Error}"));

    private void StopScan()
    {
        try
        {
            if (_watcher is not null) { _watcher.Received -= OnAdvertisementReceived; _watcher.Stopped -= OnWatcherStopped; if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started) _watcher.Stop(); _watcher = null; }
        }
        catch (Exception ex) { Log($"停止扫描时提示：{Describe(ex)}"); }
        _scan.Enabled = !_closing; _stopScan.Enabled = false;
    }

    private async Task ConnectSelectedAsync()
    {
        if (_devices.SelectedItem is not CubeAdvertisement selected)
        {
            var candidates = _seen.Values.Where(x => x.LooksLikeMoYu).ToList();
            if (candidates.Count == 1)
            {
                selected = candidates[0];
                _devices.SelectedItem = selected;
                Log($"未手动选择，自动选中唯一疑似魔域设备：{selected}");
            }
            else
            {
                Log("请先在上方列表点选一个疑似魔域设备，再点“连接选中的魔方”。");
                MessageBox.Show(this, "请先选择一个疑似魔域设备。", "未选择设备", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }
        try
        {
            _connect.Enabled = false; StopScan(); await DisconnectMoYuAsync(); Log($"连接 {selected.Name} ({selected.AddressText})，查找 MoYu 服务……");
            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(selected.Address); if (device is null) throw new InvalidOperationException("无法打开 BLE 地址。");
            _moyuDevice = device; device.ConnectionStatusChanged += OnMoYuConnectionStatusChanged;
            var serviceResult = await device.GetGattServicesForUuidAsync(MoYuService, BluetoothCacheMode.Uncached);
            Log($"魔域主服务查询：Status={serviceResult.Status}; Count={serviceResult.Services.Count}");
            if (serviceResult.Status != GattCommunicationStatus.Success || serviceResult.Services.Count == 0) throw new InvalidOperationException($"未找到主服务：{serviceResult.Status}");
            _moyuService = serviceResult.Services[0];
            var notifyResult = await _moyuService.GetCharacteristicsForUuidAsync(MoYuNotify, BluetoothCacheMode.Uncached);
            var writeResult = await _moyuService.GetCharacteristicsForUuidAsync(MoYuWrite, BluetoothCacheMode.Uncached);
            Log($"魔域通知特征查询：Status={notifyResult.Status}; Count={notifyResult.Characteristics.Count}；写入特征：Status={writeResult.Status}; Count={writeResult.Characteristics.Count}");
            if (notifyResult.Status != GattCommunicationStatus.Success || notifyResult.Characteristics.Count == 0) throw new InvalidOperationException("找不到魔域通知特征。");
            if (writeResult.Status != GattCommunicationStatus.Success || writeResult.Characteristics.Count == 0) throw new InvalidOperationException("找不到魔域写入特征。");
            _moyuNotifyCharacteristic = notifyResult.Characteristics[0];
            _moyuWriteCharacteristic = writeResult.Characteristics[0];
            _moyuNotifyCharacteristic.ValueChanged += OnMoYuNotification;
            _moyuCrypto?.Dispose();
            _moyuCrypto = new MoYuCrypto(selected.Address);
            _moyuHasBaseline = false;
            _sourceCube = null;
            _moyuBattery = null;
            var cccd = await _moyuNotifyCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
            Log($"魔域通知订阅：{cccd}");
            if (cccd != GattCommunicationStatus.Success) throw new InvalidOperationException($"通知订阅失败：{cccd}");
            _cubeStatus.Text = $"魔域：已连接 {selected.Name}"; _cubeStatus.ForeColor = Color.DarkGreen; _disconnect.Enabled = true;
            Log("魔域 V10 AI GATT 连接与通知订阅成功。开始发送 A1/A3/A4 初始化请求并等待解密数据。");
            await SendMoYuCommandAsync(0xA1);
            await Task.Delay(120);
            await SendMoYuCommandAsync(0xA3);
            await Task.Delay(120);
            await SendMoYuCommandAsync(0xA4);
            await Task.Delay(120);
            await SendMoYuCommandAsync(0xAC, 0); // turn off gyro to avoid needless notifications
        }
        catch (Exception ex) { Log($"魔域连接失败：{Describe(ex)}"); await DisconnectMoYuAsync(); }
        finally { _connect.Enabled = true; }
    }

    private void OnMoYuConnectionStatusChanged(BluetoothLEDevice sender, object args) => SafeUi(() => { Log($"魔域连接状态：{sender.ConnectionStatus}"); if (sender.ConnectionStatus != BluetoothConnectionStatus.Connected) { _cubeStatus.Text = "魔域连接：已断开"; _disconnect.Enabled = false; } });
    private async void OnMoYuNotification(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        try
        {
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            var bytes = new byte[(int)reader.UnconsumedBufferLength];
            reader.ReadBytes(bytes);
            if (_moyuCrypto is null) return;
            var plain = _moyuCrypto.Decode(bytes);
            SafeUi(() => Log($"收到魔域通知：加密={Convert.ToHexString(bytes)}；明文={Convert.ToHexString(plain)}"));
            await _moyuPacketLock.WaitAsync();
            try { await HandleMoYuPacketAsync(plain); }
            finally { _moyuPacketLock.Release(); }
        }
        catch (Exception ex) { SafeUi(() => Log($"处理魔域通知失败：{Describe(ex)}")); }
    }

    private async Task SendMoYuCommandAsync(byte opcode, byte? argument = null)
    {
        if (_moyuWriteCharacteristic is null || _moyuCrypto is null) throw new InvalidOperationException("魔域写入通道尚未准备好。");
        await _moyuWriteLock.WaitAsync();
        try
        {
            var plain = MoYuPacket.Command(opcode);
            if (argument.HasValue) plain[2] = argument.Value;
            var encoded = _moyuCrypto.Encode(plain);
            var status = await _moyuWriteCharacteristic.WriteValueAsync(CryptographicBuffer.CreateFromByteArray(encoded), GattWriteOption.WriteWithoutResponse);
            Log($"发送魔域命令 0x{opcode:X2}：Status={status}; 明文={Convert.ToHexString(plain)}");
            if (status != GattCommunicationStatus.Success) throw new InvalidOperationException($"魔域命令写入失败：{status}");
        }
        finally { _moyuWriteLock.Release(); }
    }

    private async Task HandleMoYuPacketAsync(byte[] plain)
    {
        if (MoYuPacket.TryParseA1(plain, out var model, out var version))
        {
            Log($"魔域设备信息：Model={model}; Version={version}");
            return;
        }
        if (MoYuPacket.TryParseA3Facelets(plain, out var stateCounter, out var sourceFacelets))
        {
            if (!CubeState.TryFromFacelet(sourceFacelets, out var cube))
            {
                Log($"魔域 A3 面状态非法，拒绝进入 GAN 转发。Facelet={sourceFacelets}");
                return;
            }
            _sourceCube = cube;
            _moyuLastCounter = stateCounter;
            _lastMoyuMoveCode = 0xFF;
            _moyuHasBaseline = true;
            _ganVirtualCounter = stateCounter;
            _ganFaceletsRequested = true;
            Log($"魔域面状态同步成功：counter={stateCounter}; facelet={sourceFacelets}");
            // Cube Station's GAN v2 parser ignores mode-2 MOVE packets until it
            // has accepted a mode-4 face-state packet and initialized prevMoveCnt.
            // Send the initial state proactively because native Cube Station builds
            // may not expose a visible command write for cube_sync/cube_get_info.
            if (_provider is not null && _ganCrypto is not null)
                await SendGanStartupStateAsync("魔域 A3 面状态同步");
            return;
        }
        if (MoYuPacket.TryParseA4(plain, out var battery))
        {
            _moyuBattery = battery;
            Log($"魔域电量：{battery}%");
            if (_provider is not null && _ganCrypto is not null) await SendGanBatteryAsync(battery);
            return;
        }
        if (MoYuPacket.TryParseA5(plain, out var counter, out var moves))
        {
            if (!_moyuHasBaseline || _sourceCube is null)
            {
                Log($"收到魔域 A5 转动包但尚无合法 A3 状态基线；请求重新同步状态。counter={counter}");
                await SendMoYuCommandAsync(0xA3);
                return;
            }
            var delta = (counter - _moyuLastCounter) & 0xff;
            if (delta == 0)
            {
                // MoYu V10 sometimes emits a new move without incrementing the
                // counter (observed B at the same counter). A duplicate packet
                // has the same newest move code, so only a changed newest code
                // is treated as one extra move.
                var newest = moves[0];
                if (newest.IsValid && newest.Code != _lastMoyuMoveCode)
                {
                    _lastMoyuMoveCode = newest.Code;
                    var ganCode = newest.ToGanCode();
                    _sourceCube.ApplyMove(ganCode >> 1, (ganCode & 1) == 0 ? 0 : 2);
                    Log($"魔域转动（同计数新动作）：counter={counter}; move={newest.Name}; interval={newest.TimeMs}ms; GAN-code={ganCode}; facelet={_sourceCube.ToFacelet()}");
                    await SendGanMoveAsync(newest);
                }
                return;
            }
            if (delta > 5)
            {
                Log($"魔域转动计数跳跃超过历史窗：{_moyuLastCounter} -> {counter}（delta={delta}），暂停转发并重新请求 A3。");
                _moyuHasBaseline = false;
                await SendMoYuCommandAsync(0xA3);
                return;
            }

            // MoYu A5 slots are newest-first. GAN also consumes slots newest-first,
            // while applying the newly observed moves chronologically old-to-new.
            for (var i = delta - 1; i >= 0; i--)
            {
                var move = moves[i];
                if (!move.IsValid)
                {
                    Log($"魔域 A5 含非法移动编码 {move.Code}，暂停转发并重新请求 A3。");
                    _moyuHasBaseline = false;
                    await SendMoYuCommandAsync(0xA3);
                    return;
                }

                var ganCode = move.ToGanCode();
                var face = ganCode >> 1;            // GAN face order URFDLB
                var turn = (ganCode & 1) == 0 ? 0 : 2; // CW / CCW; MoYu MY3 reports quarter turns
                _sourceCube.ApplyMove(face, turn);
                Log($"魔域转动：counter={counter}; move={move.Name}; interval={move.TimeMs}ms; GAN-code={ganCode}; facelet={_sourceCube.ToFacelet()}");
                await SendGanMoveAsync(move);
            }
            _moyuLastCounter = counter;
            _lastMoyuMoveCode = moves[0].Code;
        }
        else if (plain.Length > 0)
        {
            Log($"忽略未处理的魔域消息 0x{plain[0]:X2}。");
        }
    }

    private async Task SendGanStartupStateAsync(string reason)
    {
        if (_ganCrypto is null || _stateCharacteristic is null)
        {
            Log($"GAN 初始化通知暂缓（{reason}）：加密器或状态特征尚未准备好。");
            return;
        }

        Log($"开始发送 GAN 初始化状态（原因={reason}；当前订阅数={_stateCharacteristic.SubscribedClients.Count}）。");
        await SendGanNotificationAsync(GanNotifications.HardwareInfo(1, 1, 1, 0, "GAN356iC", gyroEnabled: false), "硬件信息（自动）");
        if (_sourceCube is not null)
            await SendGanFaceletsAsync();
        if (_moyuBattery.HasValue)
            await SendGanBatteryAsync(_moyuBattery.Value);
    }

    private static string DescribeNotifyResults(IReadOnlyList<GattClientNotificationResult> results)
    {
        if (results.Count == 0) return "clients=0（没有可报告的订阅客户端）";
        var details = results.Select(r =>
        {
            var protocol = r.ProtocolError.HasValue ? $"0x{r.ProtocolError.Value:X2}" : "-";
            var deviceId = r.SubscribedClient?.Session.DeviceId.Id ?? "unknown";
            return $"DeviceId={deviceId};Status={r.Status};Bytes={r.BytesSent};ProtocolError={protocol}";
        });
        return $"clients={results.Count}; {string.Join(" | ", details)}";
    }

    private async Task SendGanFaceletsAsync()
    {
        if (_ganCrypto is null || _stateCharacteristic is null || _sourceCube is null) return;
        var encoded = _ganCrypto.Encode(GanNotifications.Facelets(_ganVirtualCounter, _sourceCube));
        var results = await _stateCharacteristic.NotifyValueAsync(CryptographicBuffer.CreateFromByteArray(encoded));
        Log($"已发送 GAN 面状态（serial={_ganVirtualCounter}; facelet={_sourceCube.ToFacelet()}；{DescribeNotifyResults(results)}）。");
    }

    private async Task SendGanNotificationAsync(byte[] payload, string label)
    {
        if (_ganCrypto is null || _stateCharacteristic is null) return;
        var encrypted = _ganCrypto.Encode(payload);
        var results = await _stateCharacteristic.NotifyValueAsync(CryptographicBuffer.CreateFromByteArray(encrypted));
        Log($"已通过状态特征发送加密{label}通知（{encrypted.Length} 字节；{DescribeNotifyResults(results)}）。");
    }

    private async Task SendGanBatteryAsync(byte level)
    {
        if (_ganCrypto is null || _stateCharacteristic is null) return;
        var encoded = _ganCrypto.Encode(GanNotifications.Battery(level));
        var results = await _stateCharacteristic.NotifyValueAsync(CryptographicBuffer.CreateFromByteArray(encoded));
        Log($"已发送 GAN 电量通知 {level}%（{DescribeNotifyResults(results)}）。");
    }

    private async Task SendGanMoveAsync(MoYuMove move)
    {
        if (_ganCrypto is null || _stateCharacteristic is null) return;
        _ganVirtualCounter++;
        _ganMoveWindow.Insert(0, (move.ToGanCode(), move.TimeMs)); // slot 0 is newest
        while (_ganMoveWindow.Count > 7) _ganMoveWindow.RemoveAt(_ganMoveWindow.Count - 1);
        var encoded = _ganCrypto.Encode(GanNotifications.Move(_ganVirtualCounter, _ganMoveWindow.Select(x => x.Code).ToArray(), _ganMoveWindow.Select(x => x.TimeMs).ToArray()));
        var results = await _stateCharacteristic.NotifyValueAsync(CryptographicBuffer.CreateFromByteArray(encoded));
        Log($"已发送 GAN 转动通知：virtualCounter={_ganVirtualCounter}; {DescribeNotifyResults(results)}");
    }

    private async Task DisconnectMoYuAsync()
    {
        try { if (_moyuNotifyCharacteristic is not null) { _moyuNotifyCharacteristic.ValueChanged -= OnMoYuNotification; await _moyuNotifyCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None); _moyuNotifyCharacteristic = null; } } catch { _moyuNotifyCharacteristic = null; }
        try { if (_moyuDevice is not null) { _moyuDevice.ConnectionStatusChanged -= OnMoYuConnectionStatusChanged; _moyuDevice.Dispose(); _moyuDevice = null; } } catch { _moyuDevice = null; }
        _moyuWriteCharacteristic = null;
        try { _moyuCrypto?.Dispose(); } catch { }
        _moyuCrypto = null;
        _moyuHasBaseline = false;
        _sourceCube = null;
        _moyuBattery = null;
        _moyuService = null; _cubeStatus.Text = "魔域连接：未连接"; _cubeStatus.ForeColor = SystemColors.ControlText; _disconnect.Enabled = false;
    }

    private async Task CleanupAsync() { _closing = true; StopScan(); StopAdvWatchdog(); await DisconnectMoYuAsync(); StopServer(); }
    private void FailStatus(string message) { _adapterStatus.Text = message; _adapterStatus.ForeColor = Color.DarkRed; }
    private void SafeUi(Action action) { if (IsDisposed || Disposing || _closing) return; if (InvokeRequired) { try { BeginInvoke(action); } catch (InvalidOperationException) { } } else action(); }
    private void Log(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke((Action)(() => Log(message))); } catch (InvalidOperationException) { } return; }
        _log.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        var lines = _log.Lines; if (lines.Length > MaxLogLines) _log.Lines = lines[^MaxLogLines..]; _log.SelectionStart = _log.TextLength; _log.ScrollToCaret();
    }
    internal static string FormatAddress(ulong address) => string.Join(":", Enumerable.Range(0, 6).Select(i => ((address >> ((5 - i) * 8)) & 0xff).ToString("X2")));
    private static string Describe(Exception ex) => $"{ex.GetType().Name}: {ex.Message}{(ex.HResult == 0 ? "" : $" HRESULT=0x{ex.HResult:X8}")}";
}


