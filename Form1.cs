using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

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

        private readonly int[] _aiRaw = new int[8];
        private readonly ushort[] _aiType = new ushort[8];

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

        private readonly Label _lblAi4Status = new();
        private readonly Label _lblDo8Status = new();
        private readonly Label _lblDio4Status = new();
        private readonly Label _lblDi8Status = new();

        private const int OptaTcpPort = 5000;
        private readonly object _optaIoSync = new();
        private readonly string[] _aiValueText = new string[8];

        private readonly SemaphoreSlim _pollLock = new(1, 1);
        private bool _pollInFlight;

        public Form1()
        {
            InitializeComponent();
            BuildLayout();
            InitializeDemoState();
            ConfigureRefreshTimer();
            _ = TriggerPollAndRefreshAsync();
            AppendConsole("系統啟動完成。輸入 HELP 查看指令。", "INFO");
        }

        private void ConfigureRefreshTimer()
        {
            _refreshTimer.Interval = 500;
            _refreshTimer.Tick += (_, _) =>
            {
                _ = TriggerPollAndRefreshAsync();
            };
            _refreshTimer.Start();
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
            var statusY = 46;
            SetupStatusLabel(_lblAi4Status, "AI4", 760, statusY);
            SetupStatusLabel(_lblDo8Status, "DO8", 880, statusY);
            SetupStatusLabel(_lblDio4Status, "DIO4", 1000, statusY);
            SetupStatusLabel(_lblDi8Status, "DI8", 1120, statusY);

            Controls.AddRange([_lblHeader, _lblSubHeader, _tabDevices, _txtConsole, _txtCommand, _lblAi4Status, _lblDo8Status, _lblDio4Status, _lblDi8Status]);
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

        private void ExecuteAndEcho(string cmd)
        {
            AppendConsole($"> {cmd}", "CMD");
            var result = ExecuteCommand(cmd);
            AppendConsole(result, "RSP");
            RefreshAllViews();
        }

        private string ExecuteCommand(string cmd)
        {
            var p = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (p.Length == 0) return "ERR Empty command";

            if (p[0].Equals("HELP", StringComparison.OrdinalIgnoreCase))
            {
                return "HELP | STATUS (passive) | STATUS PROBE | RESET CONNECTIONS | READ/SET AI4 DO8 DIO4 DI8";
            }

            if (p[0].Equals("STATUS", StringComparison.OrdinalIgnoreCase))
            {
                if (p.Length >= 2 && p[1].Equals("PROBE", StringComparison.OrdinalIgnoreCase))
                {
                    return ProbeStatusFromOpta();
                }

                return $"AI4={(_isAi4Connected ? "OK" : "DISCONNECTED")} DO8={(_isDo8Connected ? "OK" : "DISCONNECTED")} DIO4={(_isDio4Connected ? "OK" : "DISCONNECTED")} DI8={(_isDi8Connected ? "OK" : "DISCONNECTED")}";
            }

            if (p.Length >= 2 && p[0].Equals("RESET", StringComparison.OrdinalIgnoreCase) && p[1].Equals("CONNECTIONS", StringComparison.OrdinalIgnoreCase))
            {
                if (!SendOptaCommand("RESET CONNECTIONS", out var resetRsp)) return "ERR OPTA CONNECT FAILED";
                return resetRsp;
            }

            if (p.Length < 2) return "ERR Invalid command";

            var verb = p[0].ToUpperInvariant();
            var dev = p[1].ToUpperInvariant();
            if ((verb != "READ" && verb != "SET") || (dev != "AI4" && dev != "DO8" && dev != "DIO4" && dev != "DI8"))
            {
                return "ERR Unknown command";
            }

            if (!SendOptaCommand(cmd, out var response))
            {
                return "ERR OPTA CONNECT FAILED";
            }

            UpdateStateFromResponse(cmd, response);
            return response;
        }

        private void InitializeDemoState()
        {
            for (var i = 0; i < 8; i++)
            {
                _aiRaw[i] = 0;
                _aiType[i] = 0x0103;
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

        private void SetupStatusLabel(Label label, string deviceName, int x, int y)
        {
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            label.Location = new Point(x, y);
            label.Size = new Size(110, 24);
            label.Text = $"{deviceName}: CHECK";
            label.BackColor = Color.DarkGray;
            label.ForeColor = Color.White;
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
                        UpdateStatusLabels();
                        RefreshAllViews();
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
            _isDo8Connected = PollDo8();
            _isDio4Connected = PollDio4();
            _isDi8Connected = PollDi8();
        }

        private void UpdateStatusLabels()
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateStatusLabels));
                return;
            }

            SetStatus(_lblAi4Status, "AI4", _isAi4Connected);
            SetStatus(_lblDo8Status, "DO8", _isDo8Connected);
            SetStatus(_lblDio4Status, "DIO4", _isDio4Connected);
            SetStatus(_lblDi8Status, "DI8", _isDi8Connected);
        }

        private static void SetStatus(Label label, string name, bool ok)
        {
            label.Text = ok ? $"{name}: OK" : $"{name}: FAIL";
            label.BackColor = ok ? Color.ForestGreen : Color.Firebrick;
        }

        private bool PollAi4()
        {
            var ok = true;
            for (var ch = 0; ch < 8; ch++)
            {
                if (!SendOptaCommand($"READ AI4 CH{ch}", out var valRsp))
                {
                    ok = false;
                    continue;
                }

                _aiValueText[ch] = TryExtractValueText(valRsp, out var valueText) ? valueText : valRsp;
                if (TryExtractType(valRsp, out var type)) _aiType[ch] = type;

                if (SendOptaCommand($"READ AI4 RAW CH{ch}", out var rawRsp) && TryExtractLastInt(rawRsp, out var raw))
                {
                    _aiRaw[ch] = raw;
                }
                else
                {
                    ok = false;
                }
            }

            return ok;
        }

        private bool PollDo8()
        {
            var ok = true;
            for (var ch = 0; ch < 8; ch++)
            {
                if (!SendOptaCommand($"READ DO8 CH{ch}", out var rsp))
                {
                    ok = false;
                    continue;
                }

                if (TryExtractOnOff(rsp, out var isOn)) _do8[ch] = isOn;
            }

            if (SendOptaCommand("READ DO8 POWERON", out var powerRsp) && TryExtractLastInt(powerRsp, out var powerVal)) _do8PowerOn = powerVal;
            if (SendOptaCommand("READ DO8 ACTIVE", out var activeRsp) && TryExtractLastInt(activeRsp, out var activeVal)) _do8Active = activeVal;
            return ok;
        }

        private bool PollDio4()
        {
            var ok = true;
            for (var ch = 0; ch < 4; ch++)
            {
                if (SendOptaCommand($"READ DIO4 DI{ch}", out var diRsp) && TryExtractLastInt(diRsp, out var diVal))
                    _dio4Di[ch] = diVal != 0;
                else
                    ok = false;

                if (SendOptaCommand($"READ DIO4 COUNT CH{ch}", out var cntRsp) && TryExtractLastInt(cntRsp, out var cntVal))
                    _dio4Count[ch] = cntVal;
                else
                    ok = false;

                if (SendOptaCommand($"READ DIO4 DO{ch}", out var doRsp) && TryExtractOnOff(doRsp, out var doVal))
                    _dio4Do[ch] = doVal;
                else
                    ok = false;
            }

            if (SendOptaCommand("READ DIO4 ACTIVE", out var activeRsp) && TryExtractLastInt(activeRsp, out var activeVal)) _dio4Active = activeVal;
            return ok;
        }

        private bool PollDi8()
        {
            var ok = true;
            for (var ch = 0; ch < 8; ch++)
            {
                if (SendOptaCommand($"READ DI8 CH{ch}", out var diRsp) && TryExtractLastInt(diRsp, out var diVal))
                    _di8[ch] = diVal != 0;
                else
                    ok = false;

                if (SendOptaCommand($"READ DI8 COUNT CH{ch}", out var cntRsp) && TryExtractLastInt(cntRsp, out var cntVal))
                    _di8Count[ch] = cntVal;
                else
                    ok = false;
            }

            if (SendOptaCommand("READ DI8 ACTIVE", out var activeRsp) && TryExtractLastInt(activeRsp, out var activeVal)) _di8Active = activeVal;
            return ok;
        }

        private string ProbeStatusFromOpta()
        {
            if (!SendOptaCommand("STATUS PROBE", out var rsp)) return "ERR OPTA CONNECT FAILED";

            _isAi4Connected = TryExtractDeviceStatus(rsp, "AI4", _isAi4Connected);
            _isDo8Connected = TryExtractDeviceStatus(rsp, "DO8", _isDo8Connected);
            _isDio4Connected = TryExtractDeviceStatus(rsp, "DIO4", _isDio4Connected);
            _isDi8Connected = TryExtractDeviceStatus(rsp, "DI8", _isDi8Connected);
            return rsp;
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
                    using var client = new TcpClient();
                    client.ReceiveTimeout = 1200;
                    client.SendTimeout = 1200;
                    var connectTask = client.ConnectAsync(OptaIp, OptaTcpPort);
                    if (!connectTask.Wait(1200)) return false;

                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
                    {
                        AutoFlush = true,
                        NewLine = "\n"
                    };

                    _ = reader.ReadLine();
                    writer.WriteLine(cmd);
                    response = reader.ReadLine() ?? string.Empty;
                    return !string.IsNullOrWhiteSpace(response);
                }
                catch
                {
                    return false;
                }
            }
        }

        private static bool TryExtractType(string text, out ushort type)
        {
            var m = Regex.Match(text, @"0x([0-9A-Fa-f]{4})");
            if (!m.Success)
            {
                type = 0;
                return false;
            }

            return ushort.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out type);
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

        private static bool TryExtractDeviceStatus(string text, string device, bool fallback)
        {
            var m = Regex.Match(text, $@"\b{device}\s*=\s*(OK|DISCONNECTED|FAIL)\b", RegexOptions.IgnoreCase);
            if (!m.Success) return fallback;
            return m.Groups[1].Value.Equals("OK", StringComparison.OrdinalIgnoreCase);
        }

        private void AppendConsole(string message, string level)
        {
            _txtConsole.AppendText($"[{DateTime.Now:HH:mm:ss}] {level} {message}{Environment.NewLine}");
            _txtConsole.ScrollToCaret();
        }
    }
}
