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

        private readonly int[] _aiRaw = new int[8];
        private readonly ushort[] _aiType = new ushort[8];

        private readonly bool[] _do8 = new bool[8];
        private int _do8PowerOn = 0;
        private int _do8Active = 1;

        private readonly bool[] _dio4Di = new bool[4];
        private readonly bool[] _dio4Do = new bool[4];
        private readonly int[] _dio4Count = new int[4];
        private int _dio4Active = 1;

        private readonly bool[] _di8 = new bool[8];
        private readonly int[] _di8Count = new int[8];
        private int _di8Active = 1;

        public Form1()
        {
            InitializeComponent();
            BuildLayout();
            InitializeDemoState();
            RefreshAllViews();
            AppendConsole("系統啟動完成。輸入 HELP 查看指令。", "INFO");
        }

        private void BuildLayout()
        {
            Text = "Opta Modbus TCP Multi-Device Demo";
            Width = 1500;
            Height = 900;
            StartPosition = FormStartPosition.CenterScreen;

            _lblHeader.Text = "Opta Modbus TCP Multi-Device Demo";
            _lblHeader.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            _lblHeader.AutoSize = true;
            _lblHeader.Location = new Point(16, 12);

            _lblSubHeader.Text = "Opta 192.168.2.100:5000｜AI4(111) DO8(112) DIO4(113) DI8(114)";
            _lblSubHeader.ForeColor = Color.DimGray;
            _lblSubHeader.AutoSize = true;
            _lblSubHeader.Location = new Point(18, 44);

            _tabDevices.Location = new Point(16, 74);
            _tabDevices.Size = new Size(1060, 760);

            var aiPage = new TabPage("AI4") { BackColor = Color.WhiteSmoke };
            var dio4Page = new TabPage("DIO4") { BackColor = Color.WhiteSmoke };
            var do8Page = new TabPage("DO8") { BackColor = Color.WhiteSmoke };
            var di8Page = new TabPage("DI8") { BackColor = Color.WhiteSmoke };
            _tabDevices.TabPages.AddRange(new[] { aiPage, dio4Page, do8Page, di8Page });

            BuildAiTab(aiPage);
            BuildDio4Tab(dio4Page);
            BuildDo8Tab(do8Page);
            BuildDi8Tab(di8Page);

            BuildConsolePanel();

            Controls.AddRange(new Control[] { _lblHeader, _lblSubHeader, _tabDevices, _txtConsole, _txtCommand });
        }

        private void BuildAiTab(TabPage page)
        {
            _lblAiMode.Text = "AI4 模式：Single-ended（CH0~CH7 全顯示）";
            _lblAiMode.AutoSize = true;
            _lblAiMode.Location = new Point(12, 12);
            _lblAiMode.Font = new Font("Segoe UI", 10, FontStyle.Bold);

            _gridAi.Location = new Point(12, 40);
            _gridAi.Size = new Size(1018, 530);
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

            var y = 590;
            page.Controls.Add(MakeButton("READ AI4 ALL", 12, y, () => ExecuteAndEcho("READ AI4 ALL")));
            page.Controls.Add(MakeButton("SET AI4 TYPE 0x0103", 180, y, () => ExecuteAndEcho("SET AI4 TYPE 0x0103")));
            page.Controls.Add(MakeButton("SET AI4 TYPE 0x0106", 378, y, () => ExecuteAndEcho("SET AI4 TYPE 0x0106")));
            page.Controls.Add(MakeButton("SET AI4 TYPE 0x0203", 576, y, () => ExecuteAndEcho("SET AI4 TYPE 0x0203")));
            page.Controls.Add(MakeButton("SET AI4 CH6 TYPE 0x0108", 774, y, () => ExecuteAndEcho("SET AI4 CH6 TYPE 0x0108")));
            page.Controls.AddRange(new Control[] { _lblAiMode, _gridAi });
        }

        private void BuildDio4Tab(TabPage page)
        {
            _gridDio4.Location = new Point(12, 20);
            _gridDio4.Size = new Size(1018, 550);
            _gridDio4.AllowUserToAddRows = false;
            _gridDio4.ReadOnly = true;
            _gridDio4.RowHeadersVisible = false;
            _gridDio4.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridDio4.Columns.Add("Channel", "CH");
            _gridDio4.Columns.Add("DI", "DI State");
            _gridDio4.Columns.Add("Count", "Count");
            _gridDio4.Columns.Add("DO", "DO State");

            var y = 590;
            page.Controls.Add(MakeButton("READ DIO4 COUNT CH0", 12, y, () => ExecuteAndEcho("READ DIO4 COUNT CH0")));
            page.Controls.Add(MakeButton("SET DIO4 CLEAR CH0", 220, y, () => ExecuteAndEcho("SET DIO4 CLEAR CH0")));
            page.Controls.Add(MakeButton("SET DIO4 DO0 ON", 428, y, () => ExecuteAndEcho("SET DIO4 DO0 ON")));
            page.Controls.Add(MakeButton("SET DIO4 DO0 OFF", 616, y, () => ExecuteAndEcho("SET DIO4 DO0 OFF")));
            page.Controls.Add(MakeButton("READ DIO4 ACTIVE", 804, y, () => ExecuteAndEcho("READ DIO4 ACTIVE")));
            page.Controls.Add(_gridDio4);
        }

        private void BuildDo8Tab(TabPage page)
        {
            _gridDo8.Location = new Point(12, 20);
            _gridDo8.Size = new Size(1018, 550);
            _gridDo8.AllowUserToAddRows = false;
            _gridDo8.ReadOnly = true;
            _gridDo8.RowHeadersVisible = false;
            _gridDo8.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridDo8.Columns.Add("Channel", "CH");
            _gridDo8.Columns.Add("State", "Output");

            var y = 590;
            page.Controls.Add(MakeButton("SET DO8 CH0 ON", 12, y, () => ExecuteAndEcho("SET DO8 CH0 ON")));
            page.Controls.Add(MakeButton("SET DO8 CH0 OFF", 170, y, () => ExecuteAndEcho("SET DO8 CH0 OFF")));
            page.Controls.Add(MakeButton("READ DO8 POWERON", 340, y, () => ExecuteAndEcho("READ DO8 POWERON")));
            page.Controls.Add(MakeButton("SET DO8 POWERON 1", 520, y, () => ExecuteAndEcho("SET DO8 POWERON 1")));
            page.Controls.Add(MakeButton("READ DO8 ACTIVE", 700, y, () => ExecuteAndEcho("READ DO8 ACTIVE")));
            page.Controls.Add(MakeButton("SET DO8 ACTIVE 1", 860, y, () => ExecuteAndEcho("SET DO8 ACTIVE 1")));
            page.Controls.Add(_gridDo8);
        }

        private void BuildDi8Tab(TabPage page)
        {
            _gridDi8.Location = new Point(12, 20);
            _gridDi8.Size = new Size(1018, 550);
            _gridDi8.AllowUserToAddRows = false;
            _gridDi8.ReadOnly = true;
            _gridDi8.RowHeadersVisible = false;
            _gridDi8.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridDi8.Columns.Add("Channel", "CH");
            _gridDi8.Columns.Add("State", "DI State");
            _gridDi8.Columns.Add("Count", "Count");

            var y = 590;
            page.Controls.Add(MakeButton("READ DI8 COUNT CH0", 12, y, () => ExecuteAndEcho("READ DI8 COUNT CH0")));
            page.Controls.Add(MakeButton("SET DI8 CLEAR CH0", 220, y, () => ExecuteAndEcho("SET DI8 CLEAR CH0")));
            page.Controls.Add(MakeButton("READ DI8 ACTIVE", 428, y, () => ExecuteAndEcho("READ DI8 ACTIVE")));
            page.Controls.Add(MakeButton("SET DI8 ACTIVE 1", 616, y, () => ExecuteAndEcho("SET DI8 ACTIVE 1")));
            page.Controls.Add(_gridDi8);
        }

        private void BuildConsolePanel()
        {
            _txtConsole.Location = new Point(1088, 74);
            _txtConsole.Size = new Size(390, 710);
            _txtConsole.ReadOnly = true;
            _txtConsole.BackColor = Color.FromArgb(15, 23, 42);
            _txtConsole.ForeColor = Color.AliceBlue;
            _txtConsole.Font = new Font("Consolas", 10);

            _txtCommand.Location = new Point(1088, 795);
            _txtCommand.Size = new Size(390, 30);
            _txtCommand.KeyDown += TxtCommand_KeyDown;
        }

        private Button MakeButton(string text, int x, int y, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(160, 36),
                BackColor = Color.FromArgb(234, 243, 255)
            };
            btn.Click += (_, _) => onClick();
            return btn;
        }

        private void TxtCommand_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                var cmd = _txtCommand.Text.Trim();
                if (string.IsNullOrWhiteSpace(cmd)) return;
                ExecuteAndEcho(cmd);
                _txtCommand.Clear();
            }
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
                    for (var ch = 0; ch < 8; ch++) sb.Append($"CH{ch}={FormatAiValue(ch)} ");
                    return sb.ToString().Trim();
                }

                if (TryParseChannel(p[2], out var ch, 8))
                {
                    return $"CH{ch}: raw={_aiRaw[ch]}, type=0x{_aiType[ch]:X4}, value={FormatAiValue(ch)}";
                }
                return "ERR AI4 channel";
            }

            if (action == "SET")
            {
                if (p.Length >= 4 && p[2].Equals("TYPE", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseType(p[3], out var type)) return "ERR TYPE format";
                    for (var i = 0; i < 8; i++) _aiType[i] = type;
                    return $"OK AI4 TYPE=0x{type:X4}";
                }

                if (p.Length >= 5 && TryParseChannel(p[2], out var ch, 8) && p[3].Equals("TYPE", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseType(p[4], out var type)) return "ERR TYPE format";
                    _aiType[ch] = type;
                    return $"OK AI4 CH{ch} TYPE=0x{type:X4}";
                }
            }

            return "ERR AI4 command";
        }

        private string HandleDo8(string action, string[] p)
        {
            if (action == "READ")
            {
                if (TryParseChannel(p[2], out var ch, 8)) return $"DO8 CH{ch}={(_do8[ch] ? "ON" : "OFF")}";
                if (p[2].Equals("POWERON", StringComparison.OrdinalIgnoreCase)) return $"DO8 POWERON={_do8PowerOn}";
                if (p[2].Equals("ACTIVE", StringComparison.OrdinalIgnoreCase)) return $"DO8 ACTIVE={_do8Active}";
            }

            if (action == "SET")
            {
                if (TryParseChannel(p[2], out var ch, 8) && p.Length >= 4)
                {
                    _do8[ch] = p[3].Equals("ON", StringComparison.OrdinalIgnoreCase);
                    return $"OK DO8 CH{ch}={(_do8[ch] ? "ON" : "OFF")}";
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
                if (p[2].Equals("COUNT", StringComparison.OrdinalIgnoreCase) && p.Length >= 4 && TryParseChannel(p[3], out var ch, 4)) return $"DIO4 COUNT CH{ch}={_dio4Count[ch]}";
                if (p[2].Equals("ACTIVE", StringComparison.OrdinalIgnoreCase)) return $"DIO4 ACTIVE={_dio4Active}";
                if (p[2].StartsWith("DO") && TryParseIndexAfterPrefix(p[2], "DO", out var d, 4)) return $"DIO4 DO{d}={(_dio4Do[d] ? "ON" : "OFF")}";
            }

            if (action == "SET")
            {
                if (p[2].Equals("CLEAR", StringComparison.OrdinalIgnoreCase) && p.Length >= 4 && TryParseChannel(p[3], out var ch, 4))
                {
                    _dio4Count[ch] = 0;
                    return $"OK DIO4 COUNT CH{ch} CLEARED";
                }
                if (p[2].Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) && p.Length >= 4 && int.TryParse(p[3], out var av))
                {
                    _dio4Active = av;
                    return $"OK DIO4 ACTIVE={av}";
                }
                if (p[2].StartsWith("DO") && TryParseIndexAfterPrefix(p[2], "DO", out var d, 4) && p.Length >= 4)
                {
                    _dio4Do[d] = p[3].Equals("ON", StringComparison.OrdinalIgnoreCase);
                    return $"OK DIO4 DO{d}={(_dio4Do[d] ? "ON" : "OFF")}";
                }
            }

            return "ERR DIO4 command";
        }

        private string HandleDi8(string action, string[] p)
        {
            if (action == "READ")
            {
                if (TryParseChannel(p[2], out var ch, 8)) return $"DI8 CH{ch}={(_di8[ch] ? 1 : 0)}";
                if (p[2].Equals("COUNT", StringComparison.OrdinalIgnoreCase) && p.Length >= 4 && TryParseChannel(p[3], out var c, 8)) return $"DI8 COUNT CH{c}={_di8Count[c]}";
                if (p[2].Equals("ACTIVE", StringComparison.OrdinalIgnoreCase)) return $"DI8 ACTIVE={_di8Active}";
            }

            if (action == "SET")
            {
                if (p[2].Equals("CLEAR", StringComparison.OrdinalIgnoreCase) && p.Length >= 4 && TryParseChannel(p[3], out var ch, 8))
                {
                    _di8Count[ch] = 0;
                    return $"OK DI8 COUNT CH{ch} CLEARED";
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
            var rnd = new Random(42);
            for (var i = 0; i < 8; i++)
            {
                _aiRaw[i] = rnd.Next(3_000_000, 15_000_000);
                _aiType[i] = 0x0103;
                _do8[i] = i % 2 == 0;
                _di8[i] = i % 3 == 0;
                _di8Count[i] = rnd.Next(0, 1000);
            }

            for (var i = 0; i < 4; i++)
            {
                _dio4Di[i] = i % 2 == 1;
                _dio4Do[i] = i % 2 == 0;
                _dio4Count[i] = rnd.Next(0, 500);
            }
        }

        private void RefreshAllViews()
        {
            RefreshAiGrid();
            RefreshDio4Grid();
            RefreshDo8Grid();
            RefreshDi8Grid();
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
            for (var i = 0; i < 4; i++) _gridDio4.Rows.Add($"CH{i}", _dio4Di[i] ? "1" : "0", _dio4Count[i], _dio4Do[i] ? "ON" : "OFF");
        }

        private void RefreshDo8Grid()
        {
            _gridDo8.Rows.Clear();
            for (var i = 0; i < 8; i++) _gridDo8.Rows.Add($"CH{i}", _do8[i] ? "ON" : "OFF");
        }

        private void RefreshDi8Grid()
        {
            _gridDi8.Rows.Clear();
            for (var i = 0; i < 8; i++) _gridDi8.Rows.Add($"CH{i}", _di8[i] ? "1" : "0", _di8Count[i]);
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
                return ushort.TryParse(token[2..], System.Globalization.NumberStyles.HexNumber, null, out type);
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
