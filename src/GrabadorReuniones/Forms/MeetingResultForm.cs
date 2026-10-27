using System.Diagnostics;
using GrabadorReuniones.Core;

namespace GrabadorReuniones;

internal sealed class MeetingResultForm : Form
{
    private readonly MeetingPipelineCoordinator _coordinator;
    private readonly PromptPresetStore _presetStore;
    private readonly TextBox _summaryBox;
    private readonly TextBox _transcriptBox;
    private readonly ComboBox _presetCombo;
    private readonly Button _reanalyzeBtn;
    private readonly Button _copyBtn;
    private readonly Label _statusLabel;
    private readonly MeetingContext _context;
    private string _summaryFilePath;

    public MeetingResultForm(
        MeetingPipelineResult initialResult,
        MeetingPipelineCoordinator coordinator,
        PromptPresetStore presetStore,
        MeetingContext context)
    {
        _coordinator = coordinator;
        _presetStore = presetStore;
        _context = context;
        _summaryFilePath = initialResult.SummaryPath;

        Text = $"Notas de Reunión — {context.SuggestedTitle}";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(860, 680);
        MinimumSize = new Size(650, 500);
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(248, 250, 252);
        Icon = TrayIconFactory.Create(false);

        // Barra Superior: Información y Acciones
        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 85,
            Padding = new Padding(16, 12, 16, 8),
            ColumnCount = 2,
            RowCount = 2
        };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var titleLabel = new Label
        {
            Text = $"📝 {context.SuggestedTitle}",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Dock = DockStyle.Fill,
            AutoEllipsis = true
        };

        var metaLabel = new Label
        {
            Text = $"📅 {context.StartedAt:yyyy-MM-dd HH:mm}   ⏱️ {context.DurationSec / 60}m {context.DurationSec % 60}s   🎧 Audio guardado",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Dock = DockStyle.Fill
        };

        var actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        _copyBtn = new Button
        {
            Text = "📋 Copiar para Notion",
            AutoSize = true,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        _copyBtn.FlatAppearance.BorderSize = 0;
        _copyBtn.Click += (_, _) => CopyMarkdownToClipboard();

        var openFileBtn = new Button
        {
            Text = "Abrir .md",
            AutoSize = true,
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        openFileBtn.Click += (_, _) => OpenFile(_summaryFilePath);

        var openFolderBtn = new Button
        {
            Text = "Abrir carpeta",
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        openFolderBtn.Click += (_, _) => OpenFolder(Path.GetDirectoryName(_summaryFilePath));

        actionsPanel.Controls.Add(_copyBtn);
        actionsPanel.Controls.Add(openFileBtn);
        actionsPanel.Controls.Add(openFolderBtn);

        topBar.Controls.Add(titleLabel, 0, 0);
        topBar.Controls.Add(actionsPanel, 1, 0);
        topBar.Controls.Add(metaLabel, 0, 1);

        // Barra de Re-análisis con otra plantilla
        var reanalyzeBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(16, 4, 16, 4),
            BackColor = Color.FromArgb(241, 245, 249),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var reanalyzeLabel = new Label
        {
            Text = "Re-analizar con otra plantilla:",
            AutoSize = true,
            Padding = new Padding(0, 6, 8, 0),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        _presetCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 260,
            DisplayMember = nameof(PresetItem.Label)
        };

        var allPresets = _presetStore.GetAllPresets();
        var selectedIdx = 0;
        for (var i = 0; i < allPresets.Count; i++)
        {
            _presetCombo.Items.Add(new PresetItem(allPresets[i].Id, $"{allPresets[i].Icon} {allPresets[i].Name}"));
            if (allPresets[i].Id == initialResult.PresetUsed.Id)
                selectedIdx = i;
        }

        if (_presetCombo.Items.Count > 0)
            _presetCombo.SelectedIndex = selectedIdx;

        _reanalyzeBtn = new Button
        {
            Text = "⚡ Re-analizar",
            AutoSize = true,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0)
        };
        _reanalyzeBtn.Click += async (_, _) => await ReanalyzeAsync();

        _statusLabel = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = Color.FromArgb(37, 99, 235),
            Padding = new Padding(12, 6, 0, 0)
        };

        reanalyzeBar.Controls.Add(reanalyzeLabel);
        reanalyzeBar.Controls.Add(_presetCombo);
        reanalyzeBar.Controls.Add(_reanalyzeBtn);
        reanalyzeBar.Controls.Add(_statusLabel);

        // Pestañas de contenido (Resumen / Transcripción)
        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 6)
        };

        var summaryTab = new TabPage("📋 Resumen y Acciones (Markdown)");
        _summaryBox = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Segoe UI", 10.5f),
            Text = initialResult.SummaryMarkdown,
            BackColor = Color.White,
            ReadOnly = false
        };
        summaryTab.Controls.Add(_summaryBox);

        var transcriptTab = new TabPage("🎙️ Transcripción Completa");
        _transcriptBox = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10f),
            Text = initialResult.TranscriptText,
            BackColor = Color.White,
            ReadOnly = true
        };
        transcriptTab.Controls.Add(_transcriptBox);

        tabControl.TabPages.Add(summaryTab);
        tabControl.TabPages.Add(transcriptTab);

        Controls.Add(tabControl);
        Controls.Add(reanalyzeBar);
        Controls.Add(topBar);
    }

    private void CopyMarkdownToClipboard()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_summaryBox.Text))
            {
                Clipboard.SetText(_summaryBox.Text);
                _copyBtn.Text = "✅ ¡Copiado!";
                var resetTimer = new System.Windows.Forms.Timer { Interval = 2000 };
                resetTimer.Tick += (_, _) =>
                {
                    _copyBtn.Text = "📋 Copiar para Notion";
                    resetTimer.Stop();
                    resetTimer.Dispose();
                };
                resetTimer.Start();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo copiar al portapapeles: " + ex.Message, Textos.AppNombre);
        }
    }

    private async Task ReanalyzeAsync()
    {
        if (_presetCombo.SelectedItem is not PresetItem item)
            return;

        var preset = _presetStore.GetPresetOrDefault(item.Id);
        _reanalyzeBtn.Enabled = false;
        _statusLabel.Text = $"Analizando con {preset.Name}...";

        try
        {
            var result = await _coordinator.ReanalyzeAsync(
                _transcriptBox.Text,
                preset,
                _context,
                _summaryFilePath);

            if (result.Success)
            {
                _summaryBox.Text = result.MarkdownSummary;
                _statusLabel.Text = "✅ ¡Notas actualizadas!";
            }
            else
            {
                _statusLabel.Text = "❌ Error: " + result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "❌ Error: " + ex.Message;
        }
        finally
        {
            _reanalyzeBtn.Enabled = true;
        }
    }

    private static void OpenFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
        }
        catch { }
    }

    private static void OpenFolder(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
        }
        catch { }
    }

    private sealed record PresetItem(string Id, string Label);
}
