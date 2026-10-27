using GrabadorReuniones.Core;

namespace GrabadorReuniones;

internal sealed class MeetingPromptPopup : Form
{
    private readonly System.Windows.Forms.Timer _autoDismissTimer;
    private readonly ComboBox _presetCombo;
    private readonly PromptPresetStore _presetStore;
    public event Action<string>? StartRecordingRequested;

    public MeetingPromptPopup(MeetingDetectionInfo detection, PromptPresetStore presetStore, string defaultPresetId)
    {
        _presetStore = presetStore;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(380, 160);
        BackColor = Color.FromArgb(248, 249, 250);
        Font = new Font("Segoe UI", 9.5f);

        // Posicionar en la esquina inferior derecha sobre la barra de tareas
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);
        Location = new Point(workingArea.Right - Width - 16, workingArea.Bottom - Height - 16);

        var borderPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(14)
        };

        var titleLabel = new Label
        {
            Text = $"🔔 {detection.AppName} en curso",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Dock = DockStyle.Top,
            Height = 26
        };

        var descLabel = new Label
        {
            Text = $"Se ha detectado '{detection.MeetingTitle}'. ¿Desea iniciar la grabación?",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock = DockStyle.Top,
            Height = 36
        };

        var comboPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var promptLabel = new Label
        {
            Text = "Plantilla:",
            AutoSize = true,
            Padding = new Padding(0, 4, 6, 0),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        _presetCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 240,
            DisplayMember = nameof(PresetItem.Label)
        };

        var presets = _presetStore.GetAllPresets();
        var selectedIdx = 0;
        for (var i = 0; i < presets.Count; i++)
        {
            _presetCombo.Items.Add(new PresetItem(presets[i].Id, $"{presets[i].Icon} {presets[i].Name}"));
            if (presets[i].Id.Equals(defaultPresetId, StringComparison.OrdinalIgnoreCase))
                selectedIdx = i;
        }

        if (_presetCombo.Items.Count > 0)
            _presetCombo.SelectedIndex = selectedIdx;

        comboPanel.Controls.Add(promptLabel);
        comboPanel.Controls.Add(_presetCombo);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var recordBtn = new Button
        {
            Text = "🔴 Grabar",
            AutoSize = true,
            BackColor = Color.FromArgb(220, 38, 38),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(6, 0, 0, 0)
        };
        recordBtn.FlatAppearance.BorderSize = 0;
        recordBtn.Click += (_, _) =>
        {
            var selectedId = _presetCombo.SelectedItem is PresetItem pi ? pi.Id : defaultPresetId;
            StartRecordingRequested?.Invoke(selectedId);
            Close();
        };

        var dismissBtn = new Button
        {
            Text = "Ignorar",
            AutoSize = true,
            FlatStyle = FlatStyle.Standard,
            Cursor = Cursors.Hand
        };
        dismissBtn.Click += (_, _) => Close();

        buttonPanel.Controls.Add(recordBtn);
        buttonPanel.Controls.Add(dismissBtn);

        borderPanel.Controls.Add(buttonPanel);
        borderPanel.Controls.Add(comboPanel);
        borderPanel.Controls.Add(descLabel);
        borderPanel.Controls.Add(titleLabel);

        Controls.Add(borderPanel);

        // Descarte automático a los 18 segundos
        _autoDismissTimer = new System.Windows.Forms.Timer { Interval = 18000 };
        _autoDismissTimer.Tick += (_, _) =>
        {
            _autoDismissTimer.Stop();
            Close();
        };
        _autoDismissTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _autoDismissTimer.Dispose();
        base.OnFormClosed(e);
    }

    private sealed record PresetItem(string Id, string Label);
}
