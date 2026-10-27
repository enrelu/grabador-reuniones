using GrabadorReuniones.Core;

namespace GrabadorReuniones;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _working;
    private readonly PromptPresetStore _presetStore;

    // General
    private readonly TextBox _folder;
    private readonly CheckBox _ctrl;
    private readonly CheckBox _shift;
    private readonly CheckBox _alt;
    private readonly ComboBox _key;
    private readonly CheckBox _startup;
    private readonly CheckBox _autoOpenViewer;
    private readonly CheckBox _autoCopyToClipboard;

    // Audio
    private readonly ComboBox _mic;
    private readonly ComboBox _loopback;
    private readonly ComboBox _bitrateCombo;

    // Detección
    private readonly CheckBox _enableDetection;
    private readonly ComboBox _detectPresetCombo;

    // Transcripción
    private readonly RadioButton _transModeApi;
    private readonly RadioButton _transModeCli;
    private readonly Panel _transApiPanel;
    private readonly Panel _transCliPanel;
    private readonly ComboBox _transcriptionProvider;
    private readonly TextBox _transcriptionApiKey;
    private readonly TextBox _transcriptionModel;
    private readonly TextBox _transcriptionEndpoint;
    private readonly TextBox _transcriptionLanguage;
    private readonly ComboBox _transCliTool;
    private readonly TextBox _transCliCustomCommand;

    // LLM
    private readonly RadioButton _modeApi;
    private readonly RadioButton _modeCli;
    private readonly Panel _apiPanel;
    private readonly Panel _cliPanel;
    private readonly ComboBox _llmProvider;
    private readonly TextBox _llmApiKey;
    private readonly TextBox _llmModel;
    private readonly TextBox _llmEndpoint;
    private readonly ComboBox _cliTool;
    private readonly TextBox _cliCustomCommand;

    // Prompts
    private readonly ComboBox _promptSelector;
    private readonly TextBox _promptName;
    private readonly TextBox _promptIcon;
    private readonly TextBox _promptDescription;
    private readonly TextBox _promptSystem;
    private PromptPreset? _currentEditingPreset;

    public AppSettings ResultSettings { get; private set; }

    public SettingsForm(AppSettings current, PromptPresetStore? presetStore = null)
    {
        _working = current.Clone();
        ResultSettings = current.Clone();
        _presetStore = presetStore ?? new PromptPresetStore();

        Text = Textos.AjustesTitulo;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(760, 720);
        MinimumSize = new Size(700, 640);
        Font = new Font("Segoe UI", 9.5f);
        Icon = TrayIconFactory.Create(false);

        // Control de Pestañas con Multiline activado para que las pestañas se apilen abajo si desbordan
        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Multiline = true, // Apilar pestañas abajo en lugar de flechas de desplazamiento
            Padding = new Point(12, 6)
        };

        // ---------------- TAB 1: GENERAL ----------------
        var tabGeneral = new TabPage("⚙️ General") { Padding = new Padding(16), AutoScroll = true };
        _folder = new TextBox { Dock = DockStyle.Fill, Text = _working.OutputFolder };
        var folderRow = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, Height = 32 };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var browse = new Button { Text = Textos.Examinar, AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        browse.Click += (_, _) => BrowseFolder();
        folderRow.Controls.Add(_folder, 0, 0);
        folderRow.Controls.Add(browse, 1, 0);

        _ctrl = new CheckBox { Text = "Ctrl", AutoSize = true };
        _shift = new CheckBox { Text = "Shift", AutoSize = true };
        _alt = new CheckBox { Text = "Alt", AutoSize = true };
        _key = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        foreach (var k in HotkeySpec.SelectableKeys())
            _key.Items.Add(new KeyItem(k));

        if (!HotkeySpec.TryParse(_working.Hotkey, out var hotkey))
            hotkey = HotkeySpec.Default;
        _ctrl.Checked = hotkey.Control;
        _shift.Checked = hotkey.Shift;
        _alt.Checked = hotkey.Alt;
        SelectKey(hotkey.Key);

        var hotkeyRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, AutoSize = true, WrapContents = false };
        hotkeyRow.Controls.Add(_ctrl);
        hotkeyRow.Controls.Add(_shift);
        hotkeyRow.Controls.Add(_alt);
        hotkeyRow.Controls.Add(_key);

        _startup = new CheckBox { Text = Textos.IniciarWindows, AutoSize = true, Checked = _working.StartWithWindows };
        _autoOpenViewer = new CheckBox { Text = "Abrir ventana de notas y resumen al finalizar la reunión", AutoSize = true, Checked = _working.AutoOpenViewerOnFinish };
        _autoCopyToClipboard = new CheckBox { Text = "Copiar automáticamente el Markdown de las notas al portapapeles", AutoSize = true, Checked = _working.AutoCopyToClipboard };

        var generalLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        generalLayout.Controls.Add(HeaderLabel("Carpeta de almacenamiento de grabaciones y notas:"));
        generalLayout.Controls.Add(folderRow);
        generalLayout.Controls.Add(HeaderLabel("Atajo de teclado global para iniciar/detener:"));
        generalLayout.Controls.Add(hotkeyRow);
        generalLayout.Controls.Add(new Label { Height = 10 });
        generalLayout.Controls.Add(_startup);
        generalLayout.Controls.Add(_autoOpenViewer);
        generalLayout.Controls.Add(_autoCopyToClipboard);
        tabGeneral.Controls.Add(generalLayout);

        // ---------------- TAB 2: AUDIO ----------------
        var tabAudio = new TabPage("🎙️ Audio") { Padding = new Padding(16), AutoScroll = true };
        _mic = NewCombo();
        _loopback = NewCombo();
        var refresh = new Button { Text = Textos.ActualizarDispositivos, AutoSize = true };
        refresh.Click += (_, _) => ReloadDevices();

        _bitrateCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
        _bitrateCombo.Items.Add(new ComboItem("64", "64 kbps (Recomendado para voz - menor tamaño)"));
        _bitrateCombo.Items.Add(new ComboItem("96", "96 kbps (Excelente calidad de voz)"));
        _bitrateCombo.Items.Add(new ComboItem("128", "128 kbps (Alta fidelidad estéreo)"));
        _bitrateCombo.Items.Add(new ComboItem("192", "192 kbps (Máxima calidad)"));
        SelectComboByValue(_bitrateCombo, _working.Mp3BitrateKbps.ToString());

        var audioLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        audioLayout.Controls.Add(HeaderLabel("Micrófono (su voz):"));
        audioLayout.Controls.Add(_mic);
        audioLayout.Controls.Add(HeaderLabel("Reproducción (loopback / audio del resto de participantes):"));
        audioLayout.Controls.Add(_loopback);
        audioLayout.Controls.Add(refresh);
        audioLayout.Controls.Add(HeaderLabel("Calidad de compresión MP3:"));
        audioLayout.Controls.Add(_bitrateCombo);
        tabAudio.Controls.Add(audioLayout);

        // ---------------- TAB 3: DETECCIÓN AUTOMÁTICA ----------------
        var tabDetection = new TabPage("🔔 Detección") { Padding = new Padding(16), AutoScroll = true };
        _enableDetection = new CheckBox
        {
            Text = "Detectar automáticamente reuniones (Google Meet, Teams, Zoom, Webex)",
            AutoSize = true,
            Checked = _working.EnableAutoMeetingDetection
        };

        _detectPresetCombo = NewCombo();
        var allPresets = _presetStore.GetAllPresets();
        foreach (var p in allPresets)
            _detectPresetCombo.Items.Add(new ComboItem(p.Id, $"{p.Icon} {p.Name}"));
        SelectComboByValue(_detectPresetCombo, _working.AutoDetectPromptPreset);

        var detectHelp = new Label
        {
            Text = "Cuando inicies una videollamada en Google Meet, Microsoft Teams o Zoom, aparecerá un aviso discreto sobre la barra de tareas ofreciéndote iniciar la grabación con la plantilla elegida.",
            AutoSize = true,
            MaximumSize = new Size(620, 0),
            ForeColor = Color.FromArgb(70, 80, 95),
            Margin = new Padding(0, 8, 0, 16)
        };

        var detectLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        detectLayout.Controls.Add(_enableDetection);
        detectLayout.Controls.Add(detectHelp);
        detectLayout.Controls.Add(HeaderLabel("Plantilla predeterminada al detectar reunión:"));
        detectLayout.Controls.Add(_detectPresetCombo);
        tabDetection.Controls.Add(detectLayout);

        // ---------------- TAB 4: TRANSCRIPCIÓN ----------------
        var tabTranscription = new TabPage("📝 Transcripción") { Padding = new Padding(16), AutoScroll = true };

        var transModeBox = new Panel { Width = 660, Height = 64, Margin = new Padding(0, 4, 0, 8) };
        _transModeApi = new RadioButton
        {
            Text = "🌐 Conexión por API en la nube (Groq Whisper, Gemini, OpenAI)",
            AutoSize = true,
            Location = new Point(4, 4),
            Font = new Font("Segoe UI", 9.5f),
            Checked = _working.TranscriptionExecutionMode != "Cli"
        };
        _transModeCli = new RadioButton
        {
            Text = "💻 Herramienta CLI de terminal (Antigravity agy, Claude CLI, Whisper local - Sin claves)",
            AutoSize = true,
            Location = new Point(4, 32),
            Font = new Font("Segoe UI", 9.5f),
            Checked = _working.TranscriptionExecutionMode == "Cli"
        };
        transModeBox.Controls.Add(_transModeApi);
        transModeBox.Controls.Add(_transModeCli);

        // Panel Transcripción API
        _transApiPanel = new Panel { Width = 660, Height = 340, AutoScroll = true, Visible = _transModeApi.Checked };
        _transcriptionProvider = NewCombo();
        _transcriptionProvider.Items.Add(new ComboItem("Groq", "⚡ Groq Whisper (Recomendado: Gratuito, transcribe 30m en 5s)"));
        _transcriptionProvider.Items.Add(new ComboItem("Gemini", "✨ Google Gemini (Gratuito con AI Studio)"));
        _transcriptionProvider.Items.Add(new ComboItem("OpenAI", "🌐 OpenAI Whisper-1"));
        _transcriptionProvider.Items.Add(new ComboItem("Custom", "💻 Servidor Local / Compatible OpenAI (Whisper.cpp / LocalAI)"));
        SelectComboByValue(_transcriptionProvider, _working.TranscriptionProvider);

        _transcriptionApiKey = new TextBox { Width = 450, UseSystemPasswordChar = true, Text = _working.TranscriptionApiKey };
        _transcriptionModel = new TextBox { Width = 450, Text = _working.TranscriptionModel };
        _transcriptionEndpoint = new TextBox { Width = 450, Text = _working.TranscriptionEndpoint };
        _transcriptionLanguage = new TextBox { Width = 100, Text = _working.TranscriptionLanguage };

        _transcriptionProvider.SelectedIndexChanged += (_, _) =>
        {
            if (_transcriptionProvider.SelectedItem is ComboItem ci)
            {
                if (ci.Value == "Groq" && string.IsNullOrWhiteSpace(_transcriptionModel.Text))
                    _transcriptionModel.Text = "whisper-large-v3-turbo";
                else if (ci.Value == "Gemini" && string.IsNullOrWhiteSpace(_transcriptionModel.Text))
                    _transcriptionModel.Text = "gemini-2.0-flash";
                else if (ci.Value == "OpenAI" && string.IsNullOrWhiteSpace(_transcriptionModel.Text))
                    _transcriptionModel.Text = "whisper-1";
            }
        };

        var transHelp = new Label
        {
            Text = "💡 Consejo: Groq ofrece nivel gratuito muy generoso con Whisper Large v3 Turbo, capaz de transcribir reuniones de 30 minutos en apenas 4 segundos.",
            AutoSize = true,
            MaximumSize = new Size(640, 0),
            ForeColor = Color.FromArgb(37, 99, 235),
            Margin = new Padding(0, 0, 0, 8)
        };

        var transApiLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        transApiLayout.Controls.Add(transHelp);
        transApiLayout.Controls.Add(HeaderLabel("Proveedor de transcripción:"));
        transApiLayout.Controls.Add(_transcriptionProvider);
        transApiLayout.Controls.Add(HeaderLabel("Clave de API (se guarda protegida con DPAPI):"));
        transApiLayout.Controls.Add(_transcriptionApiKey);
        transApiLayout.Controls.Add(HeaderLabel("Modelo de transcripción:"));
        transApiLayout.Controls.Add(_transcriptionModel);
        transApiLayout.Controls.Add(HeaderLabel("Endpoint personalizado (opcional para servidor local):"));
        transApiLayout.Controls.Add(_transcriptionEndpoint);
        transApiLayout.Controls.Add(HeaderLabel("Idioma del audio:"));
        transApiLayout.Controls.Add(_transcriptionLanguage);
        _transApiPanel.Controls.Add(transApiLayout);

        // Panel Transcripción CLI
        _transCliPanel = new Panel { Width = 660, Height = 340, AutoScroll = true, Visible = _transModeCli.Checked };
        _transCliTool = NewCombo();
        _transCliTool.Items.Add(new ComboItem("agy", "Google Antigravity CLI (agy)"));
        _transCliTool.Items.Add(new ComboItem("claude", "Claude Code CLI (claude)"));
        _transCliTool.Items.Add(new ComboItem("whisper", "Whisper CLI local (whisper)"));
        _transCliTool.Items.Add(new ComboItem("custom", "Comando CLI personalizado"));
        SelectComboByValue(_transCliTool, _working.TranscriptionCliTool);

        var transCliStatusLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 140, 90),
            Margin = new Padding(0, 8, 0, 8)
        };

        var transCliCustomPanel = new FlowLayoutPanel
        {
            Width = 640,
            Height = 110,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Visible = (_transCliTool.SelectedItem as ComboItem)?.Value == "custom"
        };
        _transCliCustomCommand = new TextBox { Width = 500, Text = _working.TranscriptionCliCustomCommand };
        var transCliHint = new Label
        {
            Text = "Marcadores disponibles: {AUDIO_FILE}, {OUTPUT_DIR}, {LANGUAGE}",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.Gray
        };
        transCliCustomPanel.Controls.Add(HeaderLabel("Plantilla de comando personalizado:"));
        transCliCustomPanel.Controls.Add(_transCliCustomCommand);
        transCliCustomPanel.Controls.Add(transCliHint);

        void UpdateTransCliUi()
        {
            var val = (_transCliTool.SelectedItem as ComboItem)?.Value ?? "agy";
            if (val == "agy")
                transCliStatusLabel.Text = "✅ Todo listo: Se ejecutará directamente 'agy' (Google Antigravity) usando tu sesión activa.";
            else if (val == "claude")
                transCliStatusLabel.Text = "✅ Todo listo: Se ejecutará directamente 'claude' (Claude CLI) usando tu sesión activa.";
            else if (val == "whisper")
                transCliStatusLabel.Text = "✅ Todo listo: Se ejecutará 'whisper' en tu equipo de forma 100% local.";
            else
                transCliStatusLabel.Text = "⚙️ Configure a continuación su comando CLI personalizado:";

            transCliCustomPanel.Visible = val == "custom";
        }

        _transCliTool.SelectedIndexChanged += (_, _) => UpdateTransCliUi();
        UpdateTransCliUi();

        var transCliHelp = new Label
        {
            Text = "💡 El modo CLI transcribe invocando tu herramienta instalada en la terminal (Antigravity o Claude), utilizando tu sesión activa sin necesidad de crear claves ni pagar costes de API.",
            AutoSize = true,
            MaximumSize = new Size(640, 0),
            ForeColor = Color.FromArgb(16, 185, 129),
            Margin = new Padding(0, 6, 0, 8)
        };

        var transCliLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        transCliLayout.Controls.Add(transCliHelp);
        transCliLayout.Controls.Add(HeaderLabel("Herramienta CLI para transcripción:"));
        transCliLayout.Controls.Add(_transCliTool);
        transCliLayout.Controls.Add(transCliStatusLabel);
        transCliLayout.Controls.Add(transCliCustomPanel);
        _transCliPanel.Controls.Add(transCliLayout);

        _transModeApi.CheckedChanged += (_, _) =>
        {
            _transApiPanel.Visible = _transModeApi.Checked;
            _transCliPanel.Visible = _transModeCli.Checked;
        };

        var transOuter = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        transOuter.Controls.Add(HeaderLabel("Método de transcripción:"));
        transOuter.Controls.Add(transModeBox);
        transOuter.Controls.Add(_transApiPanel);
        transOuter.Controls.Add(_transCliPanel);
        tabTranscription.Controls.Add(transOuter);

        // ---------------- TAB 5: LLM & INTELIGENCIA ARTIFICIAL ----------------
        var tabLlm = new TabPage("🤖 LLM e Inteligencia Artificial") { Padding = new Padding(16), AutoScroll = true };

        var llmModeBox = new Panel { Width = 660, Height = 64, Margin = new Padding(0, 4, 0, 8) };
        _modeApi = new RadioButton
        {
            Text = "🌐 Conexión por API en la nube (Google Gemini, Groq, OpenAI, Ollama)",
            AutoSize = true,
            Location = new Point(4, 4),
            Font = new Font("Segoe UI", 9.5f),
            Checked = _working.LlmExecutionMode != "Cli"
        };
        _modeCli = new RadioButton
        {
            Text = "💻 Herramienta CLI de terminal (Google Antigravity agy, Claude CLI - Sin claves API)",
            AutoSize = true,
            Location = new Point(4, 32),
            Font = new Font("Segoe UI", 9.5f),
            Checked = _working.LlmExecutionMode == "Cli"
        };
        llmModeBox.Controls.Add(_modeApi);
        llmModeBox.Controls.Add(_modeCli);

        // Panel API
        _apiPanel = new Panel { Width = 660, Height = 340, AutoScroll = true, Visible = _modeApi.Checked };
        _llmProvider = NewCombo();
        _llmProvider.Items.Add(new ComboItem("Gemini", "✨ Google Gemini 2.0/1.5 Flash (Recomendado: 100% Gratis en AI Studio)"));
        _llmProvider.Items.Add(new ComboItem("Groq", "⚡ Groq (Llama 3.3 70B - Gratis y ultra-rápido)"));
        _llmProvider.Items.Add(new ComboItem("OpenAI", "🌐 OpenAI (GPT-4o-mini / GPT-4o)"));
        _llmProvider.Items.Add(new ComboItem("Ollama", "💻 Ollama local / Compatible OpenAI"));
        SelectComboByValue(_llmProvider, _working.LlmProvider);

        _llmApiKey = new TextBox { Width = 450, UseSystemPasswordChar = true, Text = _working.LlmApiKey };
        _llmModel = new TextBox { Width = 450, Text = _working.LlmModel };
        _llmEndpoint = new TextBox { Width = 450, Text = _working.LlmEndpoint };

        _llmProvider.SelectedIndexChanged += (_, _) =>
        {
            if (_llmProvider.SelectedItem is ComboItem ci)
            {
                if (ci.Value == "Gemini" && string.IsNullOrWhiteSpace(_llmModel.Text))
                    _llmModel.Text = "gemini-2.0-flash";
                else if (ci.Value == "Groq" && string.IsNullOrWhiteSpace(_llmModel.Text))
                    _llmModel.Text = "llama-3.3-70b-versatile";
                else if (ci.Value == "OpenAI" && string.IsNullOrWhiteSpace(_llmModel.Text))
                    _llmModel.Text = "gpt-4o-mini";
                else if (ci.Value == "Ollama")
                {
                    if (string.IsNullOrWhiteSpace(_llmEndpoint.Text)) _llmEndpoint.Text = "http://localhost:11434/v1/chat/completions";
                    if (string.IsNullOrWhiteSpace(_llmModel.Text)) _llmModel.Text = "llama3";
                }
            }
        };

        var apiLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        apiLayout.Controls.Add(HeaderLabel("Proveedor LLM:"));
        apiLayout.Controls.Add(_llmProvider);
        apiLayout.Controls.Add(HeaderLabel("Clave de API:"));
        apiLayout.Controls.Add(_llmApiKey);
        apiLayout.Controls.Add(HeaderLabel("Modelo:"));
        apiLayout.Controls.Add(_llmModel);
        apiLayout.Controls.Add(HeaderLabel("Endpoint personalizado (para Ollama o proxy):"));
        apiLayout.Controls.Add(_llmEndpoint);
        _apiPanel.Controls.Add(apiLayout);

        // Panel CLI
        _cliPanel = new Panel { Width = 660, Height = 340, AutoScroll = true, Visible = _modeCli.Checked };
        _cliTool = NewCombo();
        _cliTool.Items.Add(new ComboItem("agy", "Google Antigravity CLI (agy)"));
        _cliTool.Items.Add(new ComboItem("claude", "Claude Code CLI (claude)"));
        _cliTool.Items.Add(new ComboItem("gemini", "Gemini CLI"));
        _cliTool.Items.Add(new ComboItem("custom", "Comando CLI personalizado"));
        SelectComboByValue(_cliTool, _working.CliTool);

        var cliStatusLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 140, 90),
            Margin = new Padding(0, 8, 0, 8)
        };

        var cliCustomPanel = new FlowLayoutPanel
        {
            Width = 640,
            Height = 110,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Visible = (_cliTool.SelectedItem as ComboItem)?.Value == "custom"
        };
        _cliCustomCommand = new TextBox { Width = 500, Text = _working.CliCustomCommand };
        var cliHint = new Label
        {
            Text = "Marcador disponible: {PROMPT_FILE} donde se inyectará el prompt y transcripción.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.Gray
        };
        cliCustomPanel.Controls.Add(HeaderLabel("Plantilla de comando personalizado:"));
        cliCustomPanel.Controls.Add(_cliCustomCommand);
        cliCustomPanel.Controls.Add(cliHint);

        void UpdateCliUi()
        {
            var val = (_cliTool.SelectedItem as ComboItem)?.Value ?? "agy";
            if (val == "agy")
                cliStatusLabel.Text = "✅ Todo listo: Se ejecutará directamente 'agy' (Google Antigravity) usando tu sesión activa.";
            else if (val == "claude")
                cliStatusLabel.Text = "✅ Todo listo: Se ejecutará directamente 'claude' (Claude CLI) usando tu sesión activa.";
            else if (val == "gemini")
                cliStatusLabel.Text = "✅ Todo listo: Se ejecutará directamente 'gemini' en la terminal.";
            else
                cliStatusLabel.Text = "⚙️ Configure a continuación su comando CLI personalizado:";

            cliCustomPanel.Visible = val == "custom";
        }

        _cliTool.SelectedIndexChanged += (_, _) => UpdateCliUi();
        UpdateCliUi();

        var cliHelp = new Label
        {
            Text = "💡 El modo CLI ejecuta la herramienta instalada en tu terminal (Antigravity o Claude), utilizando tu sesión y cuenta ya autenticada sin necesidad de crear claves ni pagar costes de API.",
            AutoSize = true,
            MaximumSize = new Size(640, 0),
            ForeColor = Color.FromArgb(16, 185, 129),
            Margin = new Padding(0, 6, 0, 8)
        };

        var cliLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        cliLayout.Controls.Add(cliHelp);
        cliLayout.Controls.Add(HeaderLabel("Herramienta CLI:"));
        cliLayout.Controls.Add(_cliTool);
        cliLayout.Controls.Add(cliStatusLabel);
        cliLayout.Controls.Add(cliCustomPanel);
        _cliPanel.Controls.Add(cliLayout);

        _modeApi.CheckedChanged += (_, _) =>
        {
            _apiPanel.Visible = _modeApi.Checked;
            _cliPanel.Visible = _modeCli.Checked;
        };

        var llmOuter = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        llmOuter.Controls.Add(HeaderLabel("Método de ejecución del LLM:"));
        llmOuter.Controls.Add(llmModeBox);
        llmOuter.Controls.Add(_apiPanel);
        llmOuter.Controls.Add(_cliPanel);
        tabLlm.Controls.Add(llmOuter);

        // ---------------- TAB 6: PROMPTS MAESTROS ----------------
        var tabPrompts = new TabPage("📋 Prompts Maestros") { Padding = new Padding(16), AutoScroll = true };
        _promptSelector = NewCombo();
        foreach (var p in allPresets)
            _promptSelector.Items.Add(new ComboItem(p.Id, $"{p.Icon} {p.Name}"));

        _promptName = new TextBox { Width = 280 };
        _promptIcon = new TextBox { Width = 60 };
        _promptDescription = new TextBox { Width = 480 };
        _promptSystem = new TextBox { Multiline = true, Width = 600, Height = 220, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9.5f) };

        var promptSaveBtn = new Button { Text = "💾 Guardar cambios en esta plantilla", AutoSize = true, Margin = new Padding(0, 8, 8, 0) };
        promptSaveBtn.Click += (_, _) => SaveCurrentEditingPreset();

        var promptRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Height = 34, WrapContents = false };
        promptRow.Controls.Add(new Label { Text = "Icono:", AutoSize = true, Padding = new Padding(0, 4, 4, 0) });
        promptRow.Controls.Add(_promptIcon);
        promptRow.Controls.Add(new Label { Text = "Nombre:", AutoSize = true, Padding = new Padding(8, 4, 4, 0) });
        promptRow.Controls.Add(_promptName);

        _promptSelector.SelectedIndexChanged += (_, _) => LoadPresetToEditor();

        var promptsLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        promptsLayout.Controls.Add(HeaderLabel("Seleccionar plantilla para ver o editar:"));
        promptsLayout.Controls.Add(_promptSelector);
        promptsLayout.Controls.Add(promptRow);
        promptsLayout.Controls.Add(HeaderLabel("Descripción:"));
        promptsLayout.Controls.Add(_promptDescription);
        promptsLayout.Controls.Add(HeaderLabel("Prompt Maestro del Sistema (Instrucciones de análisis):"));
        promptsLayout.Controls.Add(_promptSystem);
        promptsLayout.Controls.Add(promptSaveBtn);
        tabPrompts.Controls.Add(promptsLayout);

        if (_promptSelector.Items.Count > 0)
            _promptSelector.SelectedIndex = 0;

        // Añadir todas las pestañas
        tabControl.TabPages.Add(tabGeneral);
        tabControl.TabPages.Add(tabAudio);
        tabControl.TabPages.Add(tabDetection);
        tabControl.TabPages.Add(tabTranscription);
        tabControl.TabPages.Add(tabLlm);
        tabControl.TabPages.Add(tabPrompts);

        // Barra inferior fija con botones Guardar y Cancelar (NUNCA SE CORTA)
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            Padding = new Padding(16, 12, 16, 12),
            BackColor = Color.FromArgb(243, 244, 246)
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var cancel = new Button
        {
            Text = Textos.Cancelar,
            DialogResult = DialogResult.Cancel,
            Size = new Size(100, 34),
            Cursor = Cursors.Hand
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        var save = new Button
        {
            Text = Textos.Guardar,
            Size = new Size(110, 34),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        save.FlatAppearance.BorderSize = 0;
        save.Click += (_, _) => SaveAndClose();
        AcceptButton = save;
        CancelButton = cancel;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        bottomPanel.Controls.Add(buttons);

        // IMPORTANTE: Primero añadir bottomPanel y luego tabControl para que DockStyle.Fill no sobreponga el pie
        Controls.Add(bottomPanel);
        Controls.Add(tabControl);
        bottomPanel.BringToFront();

        ReloadDevices();
    }

    private static Label HeaderLabel(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 3),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

    private static ComboBox NewCombo() =>
        new()
        {
            Width = 400,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = nameof(ComboItem.Label)
        };

    private void ReloadDevices()
    {
        FillDevices(_mic, AudioDeviceCatalog.ListCaptureDevices(), _working.MicrophoneDeviceId);
        FillDevices(_loopback, AudioDeviceCatalog.ListRenderDevices(), _working.LoopbackDeviceId);
    }

    private static void FillDevices(ComboBox combo, IReadOnlyList<AudioDeviceInfo> devices, string selectedId)
    {
        combo.Items.Clear();
        combo.Items.Add(new ComboItem("", Textos.DispositivoPredeterminado));
        var index = 0;
        for (var i = 0; i < devices.Count; i++)
        {
            combo.Items.Add(new ComboItem(devices[i].Id, devices[i].ToString()));
            if (!string.IsNullOrEmpty(selectedId) && devices[i].Id == selectedId)
                index = i + 1;
        }

        combo.SelectedIndex = combo.Items.Count > 0 ? index : -1;
    }

    private void SelectKey(Keys key)
    {
        for (var i = 0; i < _key.Items.Count; i++)
        {
            if (_key.Items[i] is KeyItem item && item.Key == key)
            {
                _key.SelectedIndex = i;
                return;
            }
        }

        _key.SelectedIndex = _key.Items.Count > 0 ? 0 : -1;
    }

    private static void SelectComboByValue(ComboBox combo, string value)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboItem item && item.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = Textos.Carpeta,
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_folder.Text) ? _folder.Text : AppPaths.DefaultOutputFolder
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _folder.Text = dlg.SelectedPath;
    }

    private void LoadPresetToEditor()
    {
        if (_promptSelector.SelectedItem is not ComboItem ci)
            return;

        var preset = _presetStore.GetPresetOrDefault(ci.Value);
        _currentEditingPreset = preset;
        _promptName.Text = preset.Name;
        _promptIcon.Text = preset.Icon;
        _promptDescription.Text = preset.Description;
        _promptSystem.Text = preset.SystemPrompt;
    }

    private void SaveCurrentEditingPreset()
    {
        if (_currentEditingPreset == null)
            return;

        _currentEditingPreset.Name = _promptName.Text.Trim();
        _currentEditingPreset.Icon = _promptIcon.Text.Trim();
        _currentEditingPreset.Description = _promptDescription.Text.Trim();
        _currentEditingPreset.SystemPrompt = _promptSystem.Text.Trim();

        _presetStore.SavePreset(_currentEditingPreset);
        MessageBox.Show(this, "Plantilla guardada correctamente.", Textos.AppNombre, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveAndClose()
    {
        var folder = _folder.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            MessageBox.Show(this, "Indique una carpeta de grabaciones.", Textos.AppNombre, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try { Directory.CreateDirectory(folder); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo crear la carpeta:\n" + ex.Message, Textos.AppNombre, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var key = _key.SelectedItem is KeyItem ki ? ki.Key : Keys.R;
        var spec = new HotkeySpec(_ctrl.Checked, _shift.Checked, _alt.Checked, key);
        if (!spec.Control && !spec.Shift && !spec.Alt)
        {
            MessageBox.Show(this, "El atajo debe incluir Ctrl, Shift o Alt.", Textos.AppNombre, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int.TryParse((_bitrateCombo.SelectedItem as ComboItem)?.Value ?? "64", out var bitrate);

        ResultSettings = new AppSettings
        {
            OutputFolder = folder,
            MicrophoneDeviceId = (_mic.SelectedItem as ComboItem)?.Value ?? "",
            LoopbackDeviceId = (_loopback.SelectedItem as ComboItem)?.Value ?? "",
            Mp3BitrateKbps = bitrate > 0 ? bitrate : 64,
            EnableAutoMeetingDetection = _enableDetection.Checked,
            AutoDetectPromptPreset = (_detectPresetCombo.SelectedItem as ComboItem)?.Value ?? "general",
            TranscriptionExecutionMode = _transModeCli.Checked ? "Cli" : "CloudApi",
            TranscriptionCliTool = (_transCliTool.SelectedItem as ComboItem)?.Value ?? "agy",
            TranscriptionCliCustomCommand = _transCliCustomCommand.Text.Trim(),
            TranscriptionProvider = (_transcriptionProvider.SelectedItem as ComboItem)?.Value ?? "Groq",
            TranscriptionApiKey = _transcriptionApiKey.Text.Trim(),
            TranscriptionModel = _transcriptionModel.Text.Trim(),
            TranscriptionEndpoint = _transcriptionEndpoint.Text.Trim(),
            TranscriptionLanguage = _transcriptionLanguage.Text.Trim(),
            LlmExecutionMode = _modeCli.Checked ? "Cli" : "CloudApi",
            LlmProvider = (_llmProvider.SelectedItem as ComboItem)?.Value ?? "Gemini",
            LlmApiKey = _llmApiKey.Text.Trim(),
            LlmModel = _llmModel.Text.Trim(),
            LlmEndpoint = _llmEndpoint.Text.Trim(),
            CliTool = (_cliTool.SelectedItem as ComboItem)?.Value ?? "agy",
            CliCustomCommand = _cliCustomCommand.Text.Trim(),
            AutoOpenViewerOnFinish = _autoOpenViewer.Checked,
            AutoCopyToClipboard = _autoCopyToClipboard.Checked,
            Hotkey = spec.ToString(),
            StartWithWindows = _startup.Checked,
            HasCompletedFirstRun = true
        };

        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed record ComboItem(string Value, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record KeyItem(Keys Key)
    {
        public override string ToString() => HotkeySpec.FormatKey(Key);
    }
}
