using System.Globalization;
using System.Text;

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
        private readonly Random _rnd = new(42);

        public Form1()
        {
            InitializeComponent();
            BuildLayout();
            InitializeDemoState();
            ConfigureRefreshTimer();
            RefreshAllViews();
            AppendConsole("系統啟動完成。輸入 HELP 查看指令。", "INFO");
        }

        private void ConfigureRefreshTimer()
        {
            _refreshTimer.Interval = 500;
            _refreshTimer.Tick += (_, _) =>
            {
                SimulateInputUpdate();
                RefreshAllViews();
            };
            _refreshTimer.Start();
        }

        private void SimulateInputUpdate()
        {
            for (var i = 0; i < 8; i++)
            {
                var delta = _rnd.Next(-180000, 180001);
                _aiRaw[i] = Math.Clamp(_aiRaw[i] + delta, 0, 16_777_215);

                if (_rnd.NextDouble() < 0.15)
                {
                    _di8[i] = !_di8[i];
                }
                if (_di8[i]) _di8Count[i] += _rnd.Next(0, 5);
            }

            for (var i = 0; i < 4; i++)
            {
                if (_rnd.NextDouble() < 0.2)
                {
                    _dio4Di[i] = !_dio4Di[i];
                }
                if (_dio4Di[i]) _dio4Count[i] += _rnd.Next(0, 4);
            }
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

            _lblSubHeader.Text = "Opta 192.168.2.100:5000｜AI4(111) DO8(112) DIO4(113) DI8(114)｜輸入每 0.5 秒更新";
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

            Controls.AddRange([_lblHeader, _lblSubHeader, _tabDevices, _txtConsole, _txtCommand]);
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
                _dio4DiToggleButtons[chIndex] = MakeButton("Toggle DI", 70, y, () =>
                {
                    _dio4Di[chIndex] = !_dio4Di[chIndex];
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
                _di8DiToggleButtons[chIndex] = MakeButton("Toggle DI", x + 46, y + 2, () =>
                {
                    _di8[chIndex] = !_di8[chIndex];
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
                return "HELP/STATUS/READ/SET supported for AI4, DIO4, DO8, DI8.";
            }

            if (p[0].Equals("STATUS", StringComparison.OrdinalIgnoreCase))
            {
                return $"AI4=OK DO8=OK DIO4=OK DI8=OK | DO8_ACTIVE={_do8Active} DIO4_ACTIVE={_dio4Active} DI8_ACTIVE={_di8Active}";
            }

            if (p.Length < 3) return "ERR Invalid command";

            var action = p[0].ToUpperInvariant();
            var dev = p[1].ToUpperInvariant();

            return dev switch
            {
                "AI4" => HandleAi4(action, p),
                "DO8" => HandleDo8(action, p),
                "DIO4" => HandleDio4(action, p),
                "DI8" => HandleDi8(action, p),
                _ => "ERR Unknown device"
            };
        }

        private string HandleAi4(string action, string[] p)
        {
            if (action == "READ")
            {
                if (p[2].Equals("ALL", StringComparison.OrdinalIgnoreCase))
                {
                    var sb = new StringBuilder();
                    for (var i = 0; i < 8; i++) sb.Append($"CH{i}={FormatAiValue(i)} ");
                    return sb.ToString().Trim();
                }

                if (TryParseChannel(p[2], out var readCh, 8))
                {
                    return $"CH{readCh}: raw={_aiRaw[readCh]}, type=0x{_aiType[readCh]:X4}, value={FormatAiValue(readCh)}";
                }
                return "ERR AI4 channel";
            }

            if (action == "SET")
            {
                if (p.Length >= 4 && p[2].Equals("TYPE", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseType(p[3], out var globalType)) return "ERR TYPE format";
                    for (var i = 0; i < 8; i++) _aiType[i] = globalType;
                    return $"OK AI4 TYPE=0x{globalType:X4}";
                }

                if (p.Length >= 5 && TryParseChannel(p[2], out var setCh, 8) && p[3].Equals("TYPE", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseType(p[4], out var channelType)) return "ERR TYPE format";
                    _aiType[setCh] = channelType;
                    return $"OK AI4 CH{setCh} TYPE=0x{channelType:X4}";
                }
            }

            return "ERR AI4 command";
        }

        private string HandleDo8(string action, string[] p)
        {
            if (action == "READ")
            {
                if (TryParseChannel(p[2], out var readCh, 8)) return $"DO8 CH{readCh}={(_do8[readCh] ? "ON" : "OFF")}";
                if (p[2].Equals("POWERON", StringComparison.OrdinalIgnoreCase)) return $"DO8 POWERON={_do8PowerOn}";
                if (p[2].Equals("ACTIVE", StringComparison.OrdinalIgnoreCase)) return $"DO8 ACTIVE={_do8Active}";
            }

            if (action == "SET")
            {
                if (TryParseChannel(p[2], out var setCh, 8) && p.Length >= 4)
                {
                    _do8[setCh] = p[3].Equals("ON", StringComparison.OrdinalIgnoreCase);
                    return $"OK DO8 CH{setCh}={(_do8[setCh] ? "ON" : "OFF")}";
                }

                if (p[2].Equals("POWERON", StringComparison.OrdinalIgnoreCase) && p.Length >= 4 && int.TryParse(p[3], out var pw))
                {
                    _do8PowerOn = pw;
                    return $"OK DO8 POWERON={pw}";
                }

                if (p[2].Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) && p.Length >= 4 && int.TryParse(p[3], out var av))
                {
                    _do8Active = av;
                    return $"OK DO8 ACTIVE={av}";
                }
            }

            return "ERR DO8 command";
        }

        private string HandleDio4(string action, string[] p)
        {
            if (action == "READ")
            {
                if (p[2].StartsWith("DI") && TryParseIndexAfterPrefix(p[2], "DI", out var di, 4)) return $"DIO4 DI{di}={(_dio4Di[di] ? 1 : 0)}";
                if (p[2].Equals("COUNT", StringComparison.OrdinalIgnoreCase) && p.Length >= 4 && TryParseChannel(p[3], out var countCh, 4)) return $"DIO4 COUNT CH{countCh}={_dio4Count[countCh]}";
                if (p[2].Equals("ACTIVE", StringComparison.OrdinalIgnoreCase)) return $"DIO4 ACTIVE={_dio4Active}";
                if (p[2].StartsWith("DO") && TryParseIndexAfterPrefix(p[2], "DO", out var dout, 4)) return $"DIO4 DO{dout}={(_dio4Do[dout] ? "ON" : "OFF")}";
            }

            if (action == "SET")
            {
                if (p[2].Equals("CLEAR", StringComparison.OrdinalIgnoreCase) && p.Length >= 4 && TryParseChannel(p[3], out var clearCh, 4))
                {
                    _dio4Count[clearCh] = 0;
                    return $"OK DIO4 COUNT CH{clearCh} CLEARED";
                }

                if (p[2].Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) && p.Length >= 4 && int.TryParse(p[3], out var av))
                {
                    _dio4Active = av;
                    return $"OK DIO4 ACTIVE={av}";
                }

                if (p[2].StartsWith("DO") && TryParseIndexAfterPrefix(p[2], "DO", out var dout, 4) && p.Length >= 4)
                {
                    _dio4Do[dout] = p[3].Equals("ON", StringComparison.OrdinalIgnoreCase);
                    return $"OK DIO4 DO{dout}={(_dio4Do[dout] ? "ON" : "OFF")}";
                }
            }

            return "ERR DIO4 command";
        }

        private string HandleDi8(string action, string[] p)
        {
            if (action == "READ")
            {
                if (TryParseChannel(p[2], out var readCh, 8)) return $"DI8 CH{readCh}={(_di8[readCh] ? 1 : 0)}";
                if (p[2].Equals("COUNT", StringComparison.OrdinalIgnoreCase) && p.Length >= 4 && TryParseChannel(p[3], out var countCh, 8)) return $"DI8 COUNT CH{countCh}={_di8Count[countCh]}";
                if (p[2].Equals("ACTIVE", StringComparison.OrdinalIgnoreCase)) return $"DI8 ACTIVE={_di8Active}";
            }

            if (action == "SET")
            {
                if (p[2].Equals("CLEAR", StringComparison.OrdinalIgnoreCase) && p.Length >= 4 && TryParseChannel(p[3], out var clearCh, 8))
                {
                    _di8Count[clearCh] = 0;
                    return $"OK DI8 COUNT CH{clearCh} CLEARED";
                }

                if (p[2].Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) && p.Length >= 4 && int.TryParse(p[3], out var av))
                {
                    _di8Active = av;
                    return $"OK DI8 ACTIVE={av}";
                }
            }

            return "ERR DI8 command";
        }

        private void InitializeDemoState()
        {
            for (var i = 0; i < 8; i++)
            {
                _aiRaw[i] = _rnd.Next(3_000_000, 15_000_000);
                _aiType[i] = 0x0103;
                _do8[i] = i % 2 == 0;
                _di8[i] = i % 3 == 0;
                _di8Count[i] = _rnd.Next(0, 1000);
            }

            for (var i = 0; i < 4; i++)
            {
                _dio4Di[i] = i % 2 == 1;
                _dio4Do[i] = i % 2 == 0;
                _dio4Count[i] = _rnd.Next(0, 500);
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
                    _di8DiToggleButtons[i].Text = $"Toggle DI ({(_di8[i] ? 1 : 0)})";

                if (_aiTypeEditors[i] != null)
                {
                    var target = $"0x{_aiType[i]:X4}";
                    if (!_aiTypeEditors[i].DroppedDown && !string.Equals(_aiTypeEditors[i].SelectedItem?.ToString(), target, StringComparison.OrdinalIgnoreCase))
                    {
                        _aiTypeEditors[i].SelectedItem = target;
                    }
                }
            }

            for (var i = 0; i < 4; i++)
            {
                if (_dio4DoToggleButtons[i] != null)
                    _dio4DoToggleButtons[i].Text = $"DO Toggle ({(_dio4Do[i] ? "ON" : "OFF")})";

                if (_dio4DiToggleButtons[i] != null)
                    _dio4DiToggleButtons[i].Text = $"Toggle DI ({(_dio4Di[i] ? 1 : 0)})";
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
            for (var i = 0; i < 8; i++)
            {
                _gridDi8.Rows.Add($"CH{i}", _di8[i] ? "1" : "0", _di8Count[i]);
            }
        }

        private string FormatAiValue(int ch)
        {
            var type = _aiType[ch];
            var isDiff = IsDifferentialType(type);

            if (isDiff && ch % 2 == 1)
            {
                return $"N/A (paired with CH{ch - 1})";
            }

            var raw = isDiff ? Math.Max(0, _aiRaw[ch - ch % 2] - _aiRaw[ch - ch % 2 + 1]) : _aiRaw[ch];
            var fs = 16_777_215d;

            return type switch
            {
                0x0101 => $"{(10d / fs * raw):0.###} V",
                0x0102 => $"{(5d / fs * raw):0.###} V",
                0x0103 => $"{(1d / fs * raw):0.###} V",
                0x0104 => $"{(0.5d / fs * raw):0.###} V",
                0x0105 => $"{(0.1d / fs * raw):0.###} V",
                0x0106 => $"{(20d / fs * raw - 10):0.###} V",
                0x0107 => $"{(10d / fs * raw - 5):0.###} V",
                0x0108 => $"{(2d / fs * raw - 1):0.###} V",
                0x0109 => $"{(1d / fs * raw - 0.5):0.###} V",
                0x010A => $"{(0.2d / fs * raw - 0.1):0.###} V",
                0x0201 => $"{(16d / fs * raw + 4):0.###} mA",
                0x0202 => $"{(20d / fs * raw):0.###} mA",
                0x0203 => $"{(40d / fs * raw - 20):0.###} mA",
                _ => "Unknown"
            };
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

        private void AppendConsole(string message, string level)
        {
            _txtConsole.AppendText($"[{DateTime.Now:HH:mm:ss}] {level} {message}{Environment.NewLine}");
            _txtConsole.ScrollToCaret();
        }
    }
}
