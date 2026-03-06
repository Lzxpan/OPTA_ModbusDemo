using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace OPTA_ModbusDemo
{
    public partial class Form1 : Form
    {
        private readonly TabControl _tabDevices = new();
        private readonly RichTextBox _txtConsole = new();
        private readonly TextBox _txtCommand = new();
        private readonly Label _lblHeader = new();
        private readonly Label _lblSubHeader = new();
        private readonly Label _lblAiMode = new();
        private readonly Button _btnOptaConnect = new();
        private readonly Button _btnPollingToggle = new();
        private readonly Label _lblPollingState = new();
        private readonly Label _lblIoState = new();
        private readonly Label _lblLastCycle = new();
        private readonly Label _lblOptaConn = new();

        private readonly DataGridView _gridAi = new();
        private readonly DataGridView _gridDio4 = new();
        private readonly DataGridView _gridDo8 = new();
        private readonly DataGridView _gridDi8 = new();

        private readonly ComboBox[] _aiTypeEditors = new ComboBox[8];
        private static readonly string[] AiTypeOptions =
        [
            "0x0101", "0x0102", "0x0103", "0x0104", "0x0105",
            "0x0106", "0x0107", "0x0108", "0x0109", "0x010A",
            "0x0201", "0x0202", "0x0203"
        ];
        private readonly Button[] _do8ToggleButtons = new Button[8];
        private readonly Button[] _dio4DoToggleButtons = new Button[4];
        private readonly Button[] _dio4DiToggleButtons = new Button[4];
        private readonly Button[] _di8DiToggleButtons = new Button[8];

        private readonly ushort[] _aiType = new ushort[8];
        private readonly int[] _aiRaw = new int[8];

        private readonly bool[] _do8 = new bool[8];
        private int _do8PowerOn;
        private int _do8Active = 1;

        private readonly bool[] _dio4Di = new bool[4];
        private readonly bool[] _dio4Do = new bool[4];
        private readonly int[] _dio4Count = new int[4];
        private int _dio4Active = 1;

        private readonly bool[] _di8 = new bool[8];
        private readonly int[] _di8Count = new int[8];
        private int _di8Active = 1;

        private readonly System.Windows.Forms.Timer _refreshTimer = new();

        private bool _isAi4Connected;
        private bool _isDo8Connected;
        private bool _isDio4Connected;
        private bool _isDi8Connected;

        private const string OptaIp = "192.168.2.100";

        private const int OptaTcpPort = 5000;
        private const int OptaIoIntervalMs = 50;
        private readonly object _optaIoSync = new();
        private TcpClient? _optaClient;
        private NetworkStream? _optaStream;
        private StreamReader? _optaReader;
        private StreamWriter? _optaWriter;
        private readonly ConcurrentQueue<string> _pendingSetCommands = new();
        private readonly string[] _aiValueText = new string[8];
        private DateTime _lastOptaIoUtc = DateTime.MinValue;
        private string _lastIoCommand = "-";
        private string _lastIoKind = "IDLE";
        private string _lastIoError = "-";
        private bool _optaConnected;
        private bool _pollingEnabled;

        private readonly SemaphoreSlim _pollLock = new(1, 1);
        private bool _pollInFlight;
        private readonly CancellationTokenSource _pollCts = new();
        private readonly object _uiRefreshSync = new();
        private DateTime _lastUiRefreshUtc = DateTime.MinValue;
        private const int UiRefreshMinIntervalMs = 100;

        public Form1()
        {
            InitializeComponent();
            BuildLayout();
            InitializeDemoState();
            RefreshAllViews();
            ConfigureRefreshTimer();
            AppendConsole("系統啟動完成。輸入 HELP 查看指令。", "INFO");
            FormClosing += (_, _) =>
            {
                _pollCts.Cancel();
                DisconnectOpta();
            };
        }

        private void ConfigureRefreshTimer()
        {
            _ = Task.Run(async () =>
            {
                while (!_pollCts.IsCancellationRequested)
                {
                    if (_pollingEnabled)
                    {
                        await TriggerPollAndRefreshAsync();
                    }

                    try
                    {
                        await Task.Delay(250, _pollCts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            });
        }

        private void TogglePolling()
        {
            if (!_optaConnected)
            {
                AppendConsole("請先連線 OPTA。", "WARN");
                return;
            }

            _pollingEnabled = !_pollingEnabled;
            _btnPollingToggle.Text = _pollingEnabled ? "停止輪詢" : "開始輪詢";
            UpdateRuntimeStatusUi();
            AppendConsole(_pollingEnabled ? "Polling started." : "Polling stopped.", "INFO");

            if (_pollingEnabled)
            {
                _ = TriggerPollAndRefreshAsync();
            }
        }

        private void ConnectOptaNow()
        {
            if (_optaConnected)
            {
                DisconnectOpta();
                _pollingEnabled = false;
                _btnPollingToggle.Text = "開始輪詢";
                AppendConsole("OPTA disconnected.", "NET");
                UpdateRuntimeStatusUi();
                return;
            }

            lock (_optaIoSync)
            {
                DisconnectOpta();
                try
                {
                    var client = new TcpClient
                    {
                        NoDelay = true,
                        ReceiveTimeout = 1200,
                        SendTimeout = 1200
                    };
                    var connectTask = client.ConnectAsync(OptaIp, OptaTcpPort);
                    if (!connectTask.Wait(1200))
                    {
                        _lastIoError = "Connect timeout";
                        _optaConnected = false;
                    }
                    else
                    {
                        _optaClient = client;
                        _optaStream = client.GetStream();
                        _optaReader = new StreamReader(_optaStream, Encoding.UTF8, leaveOpen: true);
                        _optaWriter = new StreamWriter(_optaStream, new UTF8Encoding(false), leaveOpen: true)
                        {
                            AutoFlush = true,
                            NewLine = "\r\n"
                        };

                        // Consume any server greeting immediately after connect
                        _ = ReadIncomingLines(600, 120);
                        _optaConnected = true;
                        _lastIoError = "-";
                    }
                }
                catch (Exception ex)
                {
                    _lastIoError = ex.Message;
                    _optaConnected = false;
                }
            }

            if (!_optaConnected)
            {
                AppendConsole($"OPTA connect failed: {_lastIoError}", "ERR");
                UpdateRuntimeStatusUi();
                return;
            }

            AppendConsole("OPTA connected.", "NET");
            RefreshAllViews();

            UpdateRuntimeStatusUi();
        }

        private void DisconnectOpta()
        {
            try { _optaWriter?.Dispose(); } catch { }
            try { _optaReader?.Dispose(); } catch { }
            try { _optaStream?.Dispose(); } catch { }
            try { _optaClient?.Close(); } catch { }

            _optaWriter = null;
            _optaReader = null;
            _optaStream = null;
            _optaClient = null;
            _optaConnected = false;
            while (_pendingSetCommands.TryDequeue(out _)) { }
        }

        private void UpdateRuntimeStatusUi()
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateRuntimeStatusUi));
                return;
            }

            _lblPollingState.Text = _pollingEnabled ? "Polling: RUN" : "Polling: STOP";
            _lblPollingState.ForeColor = _pollingEnabled ? Color.ForestGreen : Color.Firebrick;
            _lblOptaConn.Text = _optaConnected ? "OPTA: CONNECTED" : "OPTA: DISCONNECTED";
            _lblOptaConn.ForeColor = _optaConnected ? Color.ForestGreen : Color.Firebrick;
            _btnOptaConnect.Text = _optaConnected ? "斷線 OPTA" : "連線 OPTA";
            _lblIoState.Text = $"I/O: {_lastIoKind} | CMD: {_lastIoCommand}";
            _lblLastCycle.Text = $"Last Poll: {DateTime.Now:HH:mm:ss} | Queue: {_pendingSetCommands.Count} | Err: {_lastIoError}";
        }

        private void BuildLayout()
        {
            Text = "Opta Modbus TCP Multi-Device Demo";
            Width = 1520;
            Height = 920;
            StartPosition = FormStartPosition.CenterScreen;

            _lblHeader.Text = "Opta Modbus TCP Multi-Device Demo";
            _lblHeader.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            _lblHeader.AutoSize = true;
            _lblHeader.Location = new Point(16, 12);

            _lblSubHeader.Text = $"TCP Client -> Opta {OptaIp}:{OptaTcpPort}｜AI4(111) DO8(112) DIO4(113) DI8(114)";
            _lblSubHeader.ForeColor = Color.DimGray;
            _lblSubHeader.AutoSize = true;
            _lblSubHeader.Location = new Point(18, 44);

            _btnOptaConnect.Text = "連線 OPTA";
            _btnOptaConnect.Location = new Point(810, 38);
            _btnOptaConnect.Size = new Size(110, 28);
            _btnOptaConnect.BackColor = Color.FromArgb(234, 243, 255);
            _btnOptaConnect.Click += (_, _) => ConnectOptaNow();

            _btnPollingToggle.Text = "開始輪詢";
            _btnPollingToggle.Location = new Point(930, 38);
            _btnPollingToggle.Size = new Size(110, 28);
            _btnPollingToggle.BackColor = Color.FromArgb(234, 243, 255);
            _btnPollingToggle.Click += (_, _) => TogglePolling();

            _lblPollingState.AutoSize = true;
            _lblPollingState.Location = new Point(1050, 44);
            _lblPollingState.Font = new Font("Segoe UI", 9, FontStyle.Bold);

            _lblIoState.AutoSize = true;
            _lblIoState.Location = new Point(1180, 44);
            _lblIoState.Font = new Font("Segoe UI", 9);

            _lblLastCycle.AutoSize = true;
            _lblLastCycle.Location = new Point(1050, 64);
            _lblLastCycle.Font = new Font("Segoe UI", 8);

            _lblOptaConn.AutoSize = true;
            _lblOptaConn.Location = new Point(1050, 24);
            _lblOptaConn.Font = new Font("Segoe UI", 9, FontStyle.Bold);

            _tabDevices.Location = new Point(16, 74);
            _tabDevices.Size = new Size(1080, 790);

            var aiPage = new TabPage("AI4") { BackColor = Color.WhiteSmoke };
            var dio4Page = new TabPage("DIO4") { BackColor = Color.WhiteSmoke };
            var do8Page = new TabPage("DO8") { BackColor = Color.WhiteSmoke };
            var di8Page = new TabPage("DI8") { BackColor = Color.WhiteSmoke };
            _tabDevices.TabPages.AddRange([aiPage, dio4Page, do8Page, di8Page]);

            BuildAiTab(aiPage);
            BuildDio4Tab(dio4Page);
            BuildDo8Tab(do8Page);
            BuildDi8Tab(di8Page);
            BuildConsolePanel();

            Controls.AddRange([
                _lblHeader,
                _lblSubHeader,
                _btnOptaConnect,
                _btnPollingToggle,
                _lblOptaConn,
                _lblPollingState,
                _lblIoState,
                _lblLastCycle,
                _tabDevices,
                _txtConsole,
                _txtCommand
            ]);

            UpdateRuntimeStatusUi();
        }

        private void BuildAiTab(TabPage page)
        {
            _lblAiMode.Text = "AI4 模式：Single-ended（CH0~CH7 全顯示）";
            _lblAiMode.AutoSize = true;
            _lblAiMode.Location = new Point(12, 12);
            _lblAiMode.Font = new Font("Segoe UI", 10, FontStyle.Bold);

            _gridAi.Location = new Point(12, 40);
            _gridAi.Size = new Size(1040, 460);
            _gridAi.AllowUserToAddRows = false;
            _gridAi.ReadOnly = true;
            _gridAi.RowHeadersVisible = false;
            _gridAi.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridAi.Columns.Add("Channel", "CH");
            _gridAi.Columns.Add("Port", "Port");
            _gridAi.Columns.Add("Mode", "Mode");
            _gridAi.Columns.Add("Pair", "Pair");
            _gridAi.Columns.Add("Owner", "Output Owner");
            _gridAi.Columns.Add("Raw", "Raw/Calc");
            _gridAi.Columns.Add("Type", "Type");
            _gridAi.Columns.Add("Value", "Value");

            var grpType = new GroupBox
            {
                Text = "AI4 Type 個別設定（每 CH 可獨立設定）",
                Location = new Point(12, 510),
                Size = new Size(1040, 170)
            };

            for (var i = 0; i < 8; i++)
            {
                var chIndex = i;
                var row = chIndex / 4;
                var col = chIndex % 4;
                var x = 12 + col * 255;
                var y = 28 + row * 62;

                var lbl = new Label { Text = $"CH{chIndex}", Location = new Point(x, y + 8), AutoSize = true };
                var cmb = new ComboBox
                {
                    Location = new Point(x + 40, y + 4),
                    Size = new Size(100, 26),
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                cmb.Items.AddRange(AiTypeOptions);
                cmb.SelectedItem = "0x0103";
                _aiTypeEditors[chIndex] = cmb;
                var btn = MakeButton("套用", x + 146, y + 2, () =>
                {
                    var selected = _aiTypeEditors[chIndex].SelectedItem?.ToString() ?? "0x0103";
                    ExecuteAndEcho($"SET AI4 CH{chIndex} TYPE {selected}");
                }, 70, 30);

                grpType.Controls.AddRange([lbl, cmb, btn]);
            }

            var btnReadAll = MakeButton("READ AI4 ALL", 930, 130, () => ExecuteAndEcho("READ AI4 ALL"), 100, 30);
            grpType.Controls.Add(btnReadAll);

            page.Controls.AddRange([_lblAiMode, _gridAi, grpType]);
        }

        private void BuildDio4Tab(TabPage page)
        {
            _gridDio4.Location = new Point(12, 20);
            _gridDio4.Size = new Size(1040, 420);
            _gridDio4.AllowUserToAddRows = false;
            _gridDio4.ReadOnly = true;
            _gridDio4.RowHeadersVisible = false;
            _gridDio4.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridDio4.Columns.Add("Channel", "CH");
            _gridDio4.Columns.Add("DI", "DI State");
            _gridDio4.Columns.Add("Count", "Count");
            _gridDio4.Columns.Add("DO", "DO State");

            var grp = new GroupBox
            {
                Text = "DIO4 每 CH 控制",
                Location = new Point(12, 450),
                Size = new Size(1040, 230)
            };

            for (var i = 0; i < 4; i++)
            {
                var chIndex = i;
                var y = 30 + chIndex * 46;
                grp.Controls.Add(new Label { Text = $"CH{chIndex}", Location = new Point(12, y + 7), AutoSize = true });
                _dio4DiToggleButtons[chIndex] = MakeButton("READ DI", 70, y, () =>
                {
                    ExecuteAndEcho($"READ DIO4 DI{chIndex}");
                }, 100, 30);
                var clear = MakeButton("Clear Count", 176, y, () => ExecuteAndEcho($"SET DIO4 CLEAR CH{chIndex}"), 110, 30);
                _dio4DoToggleButtons[chIndex] = MakeButton("DO Toggle", 292, y, () => ExecuteAndEcho($"SET DIO4 DO{chIndex} {(_dio4Do[chIndex] ? "OFF" : "ON")}"), 110, 30);
                grp.Controls.AddRange([_dio4DiToggleButtons[chIndex], clear, _dio4DoToggleButtons[chIndex]]);
            }

            grp.Controls.Add(MakeButton("READ DIO4 ACTIVE", 420, 30, () => ExecuteAndEcho("READ DIO4 ACTIVE"), 140, 32));
            grp.Controls.Add(MakeButton("SET DIO4 ACTIVE 1", 570, 30, () => ExecuteAndEcho("SET DIO4 ACTIVE 1"), 140, 32));
            grp.Controls.Add(MakeButton("SET DIO4 ACTIVE 0", 720, 30, () => ExecuteAndEcho("SET DIO4 ACTIVE 0"), 140, 32));

            page.Controls.AddRange([_gridDio4, grp]);
        }

        private void BuildDo8Tab(TabPage page)
        {
            _gridDo8.Location = new Point(12, 20);
            _gridDo8.Size = new Size(1040, 420);
            _gridDo8.AllowUserToAddRows = false;
            _gridDo8.ReadOnly = true;
            _gridDo8.RowHeadersVisible = false;
            _gridDo8.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridDo8.Columns.Add("Channel", "CH");
            _gridDo8.Columns.Add("State", "Output");

            var grp = new GroupBox
            {
                Text = "DO8 每 CH 控制（單一 Toggle 按鈕）",
                Location = new Point(12, 450),
                Size = new Size(1040, 230)
            };

            for (var i = 0; i < 8; i++)
            {
                var chIndex = i;
                var row = chIndex / 4;
                var col = chIndex % 4;
                var x = 16 + col * 250;
                var y = 34 + row * 62;
                grp.Controls.Add(new Label { Text = $"CH{chIndex}", Location = new Point(x, y + 8), AutoSize = true });
                _do8ToggleButtons[chIndex] = MakeButton("Toggle", x + 46, y + 2, () => ExecuteAndEcho($"SET DO8 CH{chIndex} {(_do8[chIndex] ? "OFF" : "ON")}"), 180, 32);
                grp.Controls.Add(_do8ToggleButtons[chIndex]);
            }

            grp.Controls.Add(MakeButton("READ DO8 POWERON", 16, 165, () => ExecuteAndEcho("READ DO8 POWERON"), 180, 32));
            grp.Controls.Add(MakeButton("SET DO8 POWERON 1", 210, 165, () => ExecuteAndEcho("SET DO8 POWERON 1"), 180, 32));
            grp.Controls.Add(MakeButton("READ DO8 ACTIVE", 404, 165, () => ExecuteAndEcho("READ DO8 ACTIVE"), 180, 32));
            grp.Controls.Add(MakeButton("SET DO8 ACTIVE 1", 598, 165, () => ExecuteAndEcho("SET DO8 ACTIVE 1"), 180, 32));

            page.Controls.AddRange([_gridDo8, grp]);
        }

        private void BuildDi8Tab(TabPage page)
        {
            _gridDi8.Location = new Point(12, 20);
            _gridDi8.Size = new Size(1040, 420);
            _gridDi8.AllowUserToAddRows = false;
            _gridDi8.ReadOnly = true;
            _gridDi8.RowHeadersVisible = false;
            _gridDi8.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridDi8.Columns.Add("Channel", "CH");
            _gridDi8.Columns.Add("State", "DI State");
            _gridDi8.Columns.Add("Count", "Count");

            var grp = new GroupBox
            {
                Text = "DI8 每 CH 控制",
                Location = new Point(12, 450),
                Size = new Size(1040, 230)
            };

            for (var i = 0; i < 8; i++)
            {
                var chIndex = i;
                var row = chIndex / 4;
                var col = chIndex % 4;
                var x = 16 + col * 250;
                var y = 34 + row * 62;
                grp.Controls.Add(new Label { Text = $"CH{chIndex}", Location = new Point(x, y + 8), AutoSize = true });
                _di8DiToggleButtons[chIndex] = MakeButton("READ DI", x + 46, y + 2, () =>
                {
                    ExecuteAndEcho($"READ DI8 CH{chIndex}");
                }, 94, 32);
                var clear = MakeButton("Clear", x + 146, y + 2, () => ExecuteAndEcho($"SET DI8 CLEAR CH{chIndex}"), 80, 32);
                grp.Controls.AddRange([_di8DiToggleButtons[chIndex], clear]);
            }

            grp.Controls.Add(MakeButton("READ DI8 ACTIVE", 16, 165, () => ExecuteAndEcho("READ DI8 ACTIVE"), 180, 32));
            grp.Controls.Add(MakeButton("SET DI8 ACTIVE 1", 210, 165, () => ExecuteAndEcho("SET DI8 ACTIVE 1"), 180, 32));

            page.Controls.AddRange([_gridDi8, grp]);
        }

        private void BuildConsolePanel()
        {
            _txtConsole.Location = new Point(1108, 74);
            _txtConsole.Size = new Size(390, 750);
            _txtConsole.ReadOnly = true;
            _txtConsole.BackColor = Color.FromArgb(15, 23, 42);
            _txtConsole.ForeColor = Color.AliceBlue;
            _txtConsole.Font = new Font("Consolas", 10);

            _txtCommand.Location = new Point(1108, 834);
            _txtCommand.Size = new Size(390, 30);
            _txtCommand.KeyDown += TxtCommand_KeyDown;
        }

        private Button MakeButton(string text, int x, int y, Action onClick, int width = 160, int height = 36)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = Color.FromArgb(234, 243, 255)
            };
            btn.Click += (_, _) => onClick();
            return btn;
        }

        private void TxtCommand_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            var cmd = _txtCommand.Text.Trim();
            if (string.IsNullOrWhiteSpace(cmd)) return;
            ExecuteAndEcho(cmd);
            _txtCommand.Clear();
        }

        private async void ExecuteAndEcho(string cmd)
        {
            AppendConsole($"> {cmd}", "CMD");
            var result = await Task.Run(() => ExecuteCommand(cmd));
            AppendConsole(result, "RSP");
            RefreshAllViews();
            UpdateRuntimeStatusUi();
        }

        private string ExecuteCommand(string cmd)
        {
            var p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (p.Length == 0) return "ERR Empty command";

            if (p.Length == 1 && p[0].Equals("HELP", StringComparison.OrdinalIgnoreCase))
            {
                if (!SendOptaCommand("HELP", out var helpRsp))
                {
                    return $"ERR OPTA CONNECT FAILED: {_lastIoError}";
                }

                return helpRsp;
            }

            if (p.Length < 2) return "ERR Invalid command";

            var verb = p[0].ToUpperInvariant();
            var dev = p[1].ToUpperInvariant();
            if ((verb != "READ" && verb != "SET") || (dev != "AI4" && dev != "DO8" && dev != "DIO4" && dev != "DI8"))
            {
                return "ERR Unknown command";
            }

            if (verb == "SET" && _pollingEnabled)
            {
                _pendingSetCommands.Enqueue(cmd);
                UpdateRuntimeStatusUi();
                return "QUEUED";
            }

            if (!SendOptaCommand(cmd, out var response))
            {
                return $"ERR OPTA CONNECT FAILED: {_lastIoError}";
            }

            UpdateStateFromResponse(cmd, response);
            return response;
        }

        private void InitializeDemoState()
        {
            for (var i = 0; i < 8; i++)
            {
                _aiType[i] = 0x0103;
                _aiRaw[i] = 0;
                _aiValueText[i] = "N/A";
                _do8[i] = false;
                _di8[i] = false;
                _di8Count[i] = 0;
            }

            for (var i = 0; i < 4; i++)
            {
                _dio4Di[i] = false;
                _dio4Do[i] = false;
                _dio4Count[i] = 0;
            }
        }

        private void ProcessPendingSetCommandIfAny()
        {
            if (!_pendingSetCommands.TryDequeue(out var setCmd)) return;
            if (!SendOptaCommand(setCmd, out var response)) return;
            UpdateStateFromResponse(setCmd, response);
        }

        private void RefreshAllViews()
        {
            RefreshAiGrid();
            RefreshDio4Grid();
            RefreshDo8Grid();
            RefreshDi8Grid();
            RefreshButtonTexts();
        }

        private void RefreshButtonTexts()
        {
            for (var i = 0; i < 8; i++)
            {
                if (_do8ToggleButtons[i] != null)
                    _do8ToggleButtons[i].Text = $"Toggle ({(_do8[i] ? "ON" : "OFF")})";

                if (_di8DiToggleButtons[i] != null)
                {
                    _di8DiToggleButtons[i].Enabled = _isDi8Connected;
                    _di8DiToggleButtons[i].Text = _isDi8Connected ? $"READ DI ({(_di8[i] ? 1 : 0)})" : "DI8 Offline";
                }

            }

            for (var i = 0; i < 4; i++)
            {
                if (_dio4DoToggleButtons[i] != null)
                    _dio4DoToggleButtons[i].Text = $"DO Toggle ({(_dio4Do[i] ? "ON" : "OFF")})";

                if (_dio4DiToggleButtons[i] != null)
                    _dio4DiToggleButtons[i].Text = $"READ DI ({(_dio4Di[i] ? 1 : 0)})";
            }
        }

        private void RefreshAiGrid()
        {
            _gridAi.Rows.Clear();
            var anyDiff = _aiType.Any(IsDifferentialType);
            _lblAiMode.Text = anyDiff
                ? "AI4 模式：Differential（差動 type 只由較小編號通道輸出）"
                : "AI4 模式：Single-ended（CH0~CH7 全顯示）";

            for (var ch = 0; ch < 8; ch++)
            {
                var type = _aiType[ch];
                var isDiff = IsDifferentialType(type);
                var isOwner = !isDiff || ch % 2 == 0;
                var pair = isDiff ? $"CH{ch - ch % 2}-CH{ch - ch % 2 + 1}" : "-";
                var owner = isDiff ? (isOwner ? "Owner (Low CH)" : "Follower") : "Owner";
                var rawOrCalc = isDiff && !isOwner ? "N/A" : (isDiff ? $"AI{ch - ch % 2}-AI{ch - ch % 2 + 1}" : _aiRaw[ch].ToString());
                var value = FormatAiValue(ch);

                _gridAi.Rows.Add($"CH{ch}", $"AI{ch}", isDiff ? "Diff" : "Single", pair, owner, rawOrCalc, $"0x{type:X4}", value);
            }
        }

        private void RefreshDio4Grid()
        {
            _gridDio4.Rows.Clear();
            for (var i = 0; i < 4; i++)
            {
                _gridDio4.Rows.Add($"CH{i}", _dio4Di[i] ? "1" : "0", _dio4Count[i], _dio4Do[i] ? "ON" : "OFF");
            }
        }

        private void RefreshDo8Grid()
        {
            _gridDo8.Rows.Clear();
            for (var i = 0; i < 8; i++)
            {
                _gridDo8.Rows.Add($"CH{i}", _do8[i] ? "ON" : "OFF");
            }
        }

        private void RefreshDi8Grid()
        {
            _gridDi8.Rows.Clear();
            if (!_isDi8Connected)
            {
                _gridDi8.Rows.Add("-", "DISCONNECTED", "-");
                return;
            }
            for (var i = 0; i < 8; i++)
            {
                _gridDi8.Rows.Add($"CH{i}", _di8[i] ? "1" : "0", _di8Count[i]);
            }
        }

        private string FormatAiValue(int ch)
        {
            if (!_isAi4Connected) return "DISCONNECTED";
            return _aiValueText[ch];
        }

        private static bool TryParseType(string token, out ushort type)
        {
            token = token.Trim();
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.TryParse(token[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out type);
            }
            return ushort.TryParse(token, out type);
        }

        private static bool IsDifferentialType(ushort type) => (type >= 0x0106 && type <= 0x010A) || type == 0x0203;

        private static bool TryParseChannel(string token, out int ch, int maxExclusive)
        {
            ch = -1;
            token = token.ToUpperInvariant();
            if (!token.StartsWith("CH")) return false;
            return int.TryParse(token[2..], out ch) && ch >= 0 && ch < maxExclusive;
        }

        private static bool TryParseIndexAfterPrefix(string token, string prefix, out int index, int maxExclusive)
        {
            index = -1;
            token = token.ToUpperInvariant();
            if (!token.StartsWith(prefix)) return false;
            return int.TryParse(token[prefix.Length..], out index) && index >= 0 && index < maxExclusive;
        }

        private void QueueUiRefreshFromBackground(bool force = false)
        {
            if (IsDisposed || !IsHandleCreated) return;

            lock (_uiRefreshSync)
            {
                var elapsedMs = (DateTime.UtcNow - _lastUiRefreshUtc).TotalMilliseconds;
                if (!force && _lastUiRefreshUtc != DateTime.MinValue && elapsedMs < UiRefreshMinIntervalMs)
                {
                    return;
                }

                _lastUiRefreshUtc = DateTime.UtcNow;
            }

            BeginInvoke(new Action(() =>
            {
                RefreshAllViews();
                UpdateRuntimeStatusUi();
            }));
        }

        private async Task TriggerPollAndRefreshAsync()
        {
            if (_pollInFlight) return;
            _pollInFlight = true;
            try
            {
                await _pollLock.WaitAsync();
                try
                {
                    await Task.Run(PollAllDevices);
                }
                finally
                {
                    _pollLock.Release();
                }

                if (!IsDisposed)
                {
                    BeginInvoke(new Action(() =>
                    {
                        RefreshAllViews();
                        UpdateRuntimeStatusUi();
                    }));
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(new Action(() => AppendConsole($"Poll error: {ex.Message}", "ERR")));
            }
            finally
            {
                _pollInFlight = false;
            }
        }

        private void PollAllDevices()
        {
            _isAi4Connected = PollAi4();
            QueueUiRefreshFromBackground(force: true);

            _isDo8Connected = PollDo8();
            QueueUiRefreshFromBackground(force: true);

            _isDio4Connected = PollDio4();
            QueueUiRefreshFromBackground(force: true);

            _isDi8Connected = PollDi8();
            QueueUiRefreshFromBackground(force: true);
        }

        private bool PollAi4()
        {
            var successCount = 0;
            for (var ch = 0; ch < 8; ch++)
            {
                ProcessPendingSetCommandIfAny();

                if (!SendOptaCommand($"READ AI4 CH{ch}", out var valRsp))
                {
                    continue;
                }

                successCount++;
                _aiValueText[ch] = TryExtractValueText(valRsp, out var valueText) ? valueText : valRsp;
                if (TryExtractType(valRsp, out var type)) _aiType[ch] = type;
                if (TryExtractAiRaw(valRsp, out var raw)) _aiRaw[ch] = raw;
                QueueUiRefreshFromBackground();
            }

            return successCount > 0;
        }

        private bool PollDo8()
        {
            var successCount = 0;
            for (var ch = 0; ch < 8; ch++)
            {
                ProcessPendingSetCommandIfAny();

                if (!SendOptaCommand($"READ DO8 CH{ch}", out var rsp))
                {
                    continue;
                }

                successCount++;
                if (TryExtractOnOff(rsp, out var isOn)) _do8[ch] = isOn;
                QueueUiRefreshFromBackground();
            }

            if (SendOptaCommand("READ DO8 POWERON", out var powerRsp) && TryExtractLastInt(powerRsp, out var powerVal)) _do8PowerOn = powerVal;
            if (SendOptaCommand("READ DO8 ACTIVE", out var activeRsp) && TryExtractLastInt(activeRsp, out var activeVal)) _do8Active = activeVal;
            return successCount > 0;
        }

        private bool PollDio4()
        {
            var successCount = 0;
            for (var ch = 0; ch < 4; ch++)
            {
                ProcessPendingSetCommandIfAny();

                if (SendOptaCommand($"READ DIO4 DI{ch}", out var diRsp) && TryExtractDigitalState(diRsp, out var diVal))
                {
                    _dio4Di[ch] = diVal;
                    successCount++;
                    QueueUiRefreshFromBackground();
                }

                if (SendOptaCommand($"READ DIO4 COUNT CH{ch}", out var cntRsp) && TryExtractLastInt(cntRsp, out var cntVal))
                {
                    _dio4Count[ch] = cntVal;
                    successCount++;
                    QueueUiRefreshFromBackground();
                }

                if (SendOptaCommand($"READ DIO4 DO{ch}", out var doRsp) && TryExtractOnOff(doRsp, out var doVal))
                {
                    _dio4Do[ch] = doVal;
                    successCount++;
                    QueueUiRefreshFromBackground();
                }
            }

            if (SendOptaCommand("READ DIO4 ACTIVE", out var activeRsp) && TryExtractLastInt(activeRsp, out var activeVal)) _dio4Active = activeVal;
            return successCount > 0;
        }

        private bool PollDi8()
        {
            var successCount = 0;
            for (var ch = 0; ch < 8; ch++)
            {
                ProcessPendingSetCommandIfAny();

                if (SendOptaCommand($"READ DI8 CH{ch}", out var diRsp) && TryExtractDigitalState(diRsp, out var diVal))
                {
                    _di8[ch] = diVal;
                    successCount++;
                    QueueUiRefreshFromBackground();
                }

                if (SendOptaCommand($"READ DI8 COUNT CH{ch}", out var cntRsp) && TryExtractLastInt(cntRsp, out var cntVal))
                {
                    _di8Count[ch] = cntVal;
                    successCount++;
                    QueueUiRefreshFromBackground();
                }
            }

            if (SendOptaCommand("READ DI8 ACTIVE", out var activeRsp) && TryExtractLastInt(activeRsp, out var activeVal)) _di8Active = activeVal;
            return successCount > 0;
        }

        private void UpdateStateFromResponse(string command, string response)
        {
            var upper = command.ToUpperInvariant();
            if (upper.StartsWith("SET AI4 TYPE ") && TryParseType(command[(command.LastIndexOf(' ') + 1)..], out var globalType))
            {
                for (var i = 0; i < 8; i++) _aiType[i] = globalType;
                return;
            }

            if (upper.StartsWith("SET AI4 CH") && TryParseChannel(command.Split(' ', StringSplitOptions.RemoveEmptyEntries)[2], out var ch, 8) && TryExtractType(response, out var type))
            {
                _aiType[ch] = type;
            }
        }

        private bool SendOptaCommand(string cmd, out string response)
        {
            response = string.Empty;
            lock (_optaIoSync)
            {
                try
                {
                    if (!_optaConnected || _optaClient == null || _optaStream == null || _optaReader == null || _optaWriter == null)
                    {
                        _lastIoError = "OPTA not connected";
                        return false;
                    }

                    _lastIoCommand = cmd;
                    _lastIoKind = cmd.StartsWith("SET", StringComparison.OrdinalIgnoreCase) ? "WRITE" : "READ";

                    var elapsedMs = (DateTime.UtcNow - _lastOptaIoUtc).TotalMilliseconds;
                    if (_lastOptaIoUtc != DateTime.MinValue && elapsedMs < OptaIoIntervalMs)
                    {
                        Thread.Sleep(OptaIoIntervalMs - (int)elapsedMs);
                    }

                    _optaWriter.WriteLine(cmd);
                    var (totalTimeoutMs, idleGapMs) = GetResponseTiming(cmd);
                    var lines = ReadIncomingLines(totalTimeoutMs, idleGapMs);

                    response = CollapseResponseForCommand(cmd, lines);
                    _lastOptaIoUtc = DateTime.UtcNow;
                    _lastIoKind = "IDLE";
                    _lastIoError = lines.Count == 0 ? "Empty response" : "-";
                    _optaConnected = true;
                    return lines.Count > 0;
                }
                catch (Exception ex)
                {
                    _lastOptaIoUtc = DateTime.UtcNow;
                    _lastIoKind = "ERR";
                    _lastIoError = ex.Message;
                    DisconnectOpta();
                    UpdateRuntimeStatusUi();
                    return false;
                }
            }
        }

        private List<string> ReadIncomingLines(int totalTimeoutMs, int idleGapMs)
        {
            var lines = new List<string>();
            if (_optaStream == null || _optaReader == null) return lines;

            var totalWait = Stopwatch.StartNew();
            var idleWait = Stopwatch.StartNew();
            while (totalWait.ElapsedMilliseconds < totalTimeoutMs)
            {
                if (!_optaStream.DataAvailable)
                {
                    if (lines.Count > 0 && idleWait.ElapsedMilliseconds >= idleGapMs)
                    {
                        break;
                    }

                    Thread.Sleep(10);
                    continue;
                }

                var line = _optaReader.ReadLine();
                if (line == null) break;

                var trimmed = line.Trim();
                if (trimmed.Equals("<br>", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("<br/>", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                lines.Add(line);
                idleWait.Restart();
            }

            return lines;
        }

        private static string CollapseResponseForCommand(string cmd, List<string> lines)
        {
            if (lines.Count == 0) return string.Empty;

            var sanitized = lines
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Where(l => !l.StartsWith("Connected to", StringComparison.OrdinalIgnoreCase))
                .Where(l => !l.StartsWith("Type HELP", StringComparison.OrdinalIgnoreCase))
                .Where(l => !l.StartsWith(">", StringComparison.OrdinalIgnoreCase))
                .Where(l => !l.Equals("<br>", StringComparison.OrdinalIgnoreCase))
                .Where(l => !l.Equals("<br/>", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sanitized.Count == 0)
            {
                sanitized = lines.Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                if (sanitized.Count == 0) return string.Empty;
            }

            var upper = cmd.ToUpperInvariant();
            if (upper == "HELP" || upper == "READ AI4 ALL")
            {
                return string.Join('\n', sanitized);
            }

            var terminals = sanitized
                .Select((line, idx) => new { line, idx })
                .Where(x => x.line.StartsWith("OK", StringComparison.OrdinalIgnoreCase)
                         || x.line.StartsWith("ERR", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (terminals.Count == 0)
            {
                return sanitized[^1];
            }

            var expected = BuildExpectedResponseHints(cmd);
            var best = terminals
                .Select(x => new
                {
                    x.line,
                    x.idx,
                    score = ScoreResponseLine(x.line, expected)
                })
                .OrderByDescending(x => x.score)
                .ThenByDescending(x => x.idx)
                .First();

            return best.line;
        }

        private static string[] BuildExpectedResponseHints(string cmd)
        {
            var p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToUpperInvariant())
                .ToArray();

            var hints = new List<string>();
            if (p.Length > 1) hints.Add(p[1]); // AI4 / DO8 / DIO4 / DI8

            for (var i = 2; i < p.Length; i++)
            {
                var token = p[i];
                if (token.StartsWith("CH") && token.Length > 2)
                {
                    hints.Add(token);
                    continue;
                }

                if ((token.StartsWith("DI") || token.StartsWith("DO")) && token.Length > 2 && int.TryParse(token[2..], out var chIndex))
                {
                    hints.Add($"CH{chIndex}");
                    continue;
                }

                if (token is "ACTIVE" or "POWERON" or "TYPE" or "RAW" or "ALL")
                {
                    hints.Add(token);
                }
            }

            return hints.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static int ScoreResponseLine(string line, string[] hints)
        {
            var score = 0;
            foreach (var hint in hints)
            {
                if (line.Contains(hint, StringComparison.OrdinalIgnoreCase)) score += 10;
            }

            if (line.StartsWith("OK", StringComparison.OrdinalIgnoreCase)) score += 2;
            if (line.StartsWith("ERR", StringComparison.OrdinalIgnoreCase)) score += 1;
            return score;
        }

        private static (int totalTimeoutMs, int idleGapMs) GetResponseTiming(string cmd)
        {
            var upper = cmd.ToUpperInvariant();

            if (upper == "HELP")
            {
                return (5000, 250);
            }

            if (upper == "READ AI4 ALL")
            {
                return (3500, 180);
            }

            return (1500, 60);
        }

        private static bool TryExtractType(string text, out ushort type)
        {
            var m = Regex.Match(text, @"0[xX]([0-9A-Fa-f]{1,4})");
            if (!m.Success)
            {
                type = 0;
                return false;
            }

            return ushort.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out type);
        }

        private static bool TryExtractAiRaw(string text, out int raw)
        {
            var m = Regex.Match(text, @"RAW\s*=\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                raw = 0;
                return false;
            }

            return int.TryParse(m.Groups[1].Value, out raw);
        }

        private static bool TryExtractDigitalState(string text, out bool on)
        {
            if (TryExtractOnOff(text, out on)) return true;

            var m = Regex.Match(text, @"(?:=|\b)([01])\b\s*$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                on = m.Groups[1].Value == "1";
                return true;
            }

            on = false;
            return false;
        }

        private static bool TryExtractLastInt(string text, out int value)
        {
            var matches = Regex.Matches(text, @"-?\d+");
            if (matches.Count == 0)
            {
                value = 0;
                return false;
            }

            return int.TryParse(matches[^1].Value, out value);
        }

        private static bool TryExtractOnOff(string text, out bool on)
        {
            if (text.Contains("ON", StringComparison.OrdinalIgnoreCase))
            {
                on = true;
                return true;
            }

            if (text.Contains("OFF", StringComparison.OrdinalIgnoreCase))
            {
                on = false;
                return true;
            }

            on = false;
            return false;
        }

        private static bool TryExtractValueText(string text, out string value)
        {
            var idx = text.IndexOf("value=", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                value = string.Empty;
                return false;
            }

            value = text[(idx + 6)..].Trim();
            return true;
        }

        private void AppendConsole(string message, string level)
        {
            _txtConsole.AppendText($"[{DateTime.Now:HH:mm:ss}] {level} {message}{Environment.NewLine}");
            _txtConsole.ScrollToCaret();
        }
    }
}
