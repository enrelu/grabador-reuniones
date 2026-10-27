using GrabadorReuniones.Core;

namespace GrabadorReuniones;

internal sealed class MessageWindow : NativeWindow, IDisposable
{
    public event Action? HotkeyPressed;

    public MessageWindow()
    {
        var cp = new CreateParams
        {
            Caption = "GrabadorReunionesHotkey",
            Parent = new IntPtr(-3) // HWND_MESSAGE
        };
        CreateHandle(cp);
    }

    protected override void WndProc(ref Message m)
    {
        const int wmHotkey = 0x0312;
        if (m.Msg == wmHotkey)
            HotkeyPressed?.Invoke();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }
}

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsStore _store;
    private readonly PromptPresetStore _presetStore;
    private readonly MeetingPipelineCoordinator _pipelineCoordinator;
    private readonly MeetingDetector _detector;
    private readonly MeetingRecorder _recorder = new();
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _startItem;
    private readonly ToolStripMenuItem _stopItem;
    private readonly ToolStripMenuItem _presetSubMenu;
    private readonly ToolStripMenuItem _lastMeetingItem;
    private readonly System.Windows.Forms.Timer _tick;
    private readonly MessageWindow _window;
    private readonly HotkeyService _hotkeys;
    private readonly HttpClient _http;
    private readonly WebhookNotifier _webhook;
    private readonly object _busyGate = new();
    private readonly Control _ui;
    private readonly ManualResetEventSlim _saved = new(initialState: true);

    private AppSettings _settings;
    private Icon _idleIcon;
    private Icon _recIcon;
    private bool _finalizing;
    private SettingsForm? _openSettings;
    private MeetingResultForm? _openResultForm;
    private MeetingPromptPopup? _openPopup;

    private string _activeRecordingPresetId = "general";
    private string _detectedMeetingApp = "";
    private MeetingPipelineResult? _lastResult;
    private MeetingContext? _lastContext;

    public TrayApplicationContext()
    {
        _store = new SettingsStore();
        _settings = _store.Load();
        Directory.CreateDirectory(_settings.OutputFolder);

        _presetStore = new PromptPresetStore();
        _activeRecordingPresetId = _settings.ActivePromptPresetId;

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("GrabadorReunionesIA/" + AppPaths.AppVersion);
        _webhook = new WebhookNotifier(_http);

        _pipelineCoordinator = new MeetingPipelineCoordinator(_settings, _presetStore, _http);
        _pipelineCoordinator.ProgressChanged += msg => BeginOnUi(() => UpdateTrayProgress(msg));

        _detector = new MeetingDetector(_settings.EnableAutoMeetingDetection);
        _detector.MeetingDetected += OnMeetingDetected;

        _idleIcon = TrayIconFactory.Create(recording: false);
        _recIcon = TrayIconFactory.Create(recording: true);

        _ui = new Control();
        _ = _ui.Handle;

        _window = new MessageWindow();
        _window.HotkeyPressed += () => BeginOnUi(() => ToggleRecording());
        _hotkeys = new HotkeyService(_window.Handle);

        _startItem = new ToolStripMenuItem(Textos.MenuIniciar, null, (_, _) => StartRecording());
        _stopItem = new ToolStripMenuItem(Textos.MenuDetener, null, (_, _) => StopRecording(blocking: false)) { Enabled = false };
        _presetSubMenu = new ToolStripMenuItem(Textos.MenuTipoReunion);
        _lastMeetingItem = new ToolStripMenuItem(Textos.MenuUltimaReunion, null, (_, _) => OpenLastMeetingResult()) { Enabled = false };

        RebuildPresetSubMenu();

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_startItem);
        _menu.Items.Add(_stopItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_presetSubMenu);
        _menu.Items.Add(_lastMeetingItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem(Textos.MenuCarpeta, null, (_, _) => OpenFolder()));
        _menu.Items.Add(new ToolStripMenuItem(Textos.MenuAjustes, null, (_, _) => OpenSettings()));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem(Textos.MenuSalir, null, (_, _) => ExitApp()));

        _tray = new NotifyIcon
        {
            Icon = _idleIcon,
            Text = Textos.TooltipIdle,
            Visible = true,
            ContextMenuStrip = _menu
        };
        _tray.DoubleClick += (_, _) => ToggleRecording();
        _tray.BalloonTipClicked += (_, _) => OpenLastMeetingResult();

        _tick = new System.Windows.Forms.Timer { Interval = 1000 };
        _tick.Tick += (_, _) => UpdateTooltip();

        ApplyHotkey();
        ShowFirstRunIfNeeded();
        AppLog.Info("Aplicación iniciada.");
    }

    private void RebuildPresetSubMenu()
    {
        _presetSubMenu.DropDownItems.Clear();
        var presets = _presetStore.GetAllPresets();
        foreach (var p in presets)
        {
            var item = new ToolStripMenuItem($"{p.Icon} {p.Name}")
            {
                Checked = p.Id.Equals(_activeRecordingPresetId, StringComparison.OrdinalIgnoreCase)
            };
            var presetId = p.Id;
            item.Click += (_, _) =>
            {
                _activeRecordingPresetId = presetId;
                _settings.ActivePromptPresetId = presetId;
                try { _store.Save(_settings); } catch { }
                RebuildPresetSubMenu();
            };
            _presetSubMenu.DropDownItems.Add(item);
        }
    }

    private void OnMeetingDetected(MeetingDetectionInfo detection)
    {
        BeginOnUi(() =>
        {
            if (_recorder.IsRecording || _finalizing)
                return;

            if (_openPopup != null && !_openPopup.IsDisposed)
                return;

            _detectedMeetingApp = detection.AppName;
            var popup = new MeetingPromptPopup(detection, _presetStore, _settings.AutoDetectPromptPreset);
            _openPopup = popup;

            popup.StartRecordingRequested += presetId =>
            {
                _detector.DismissAppAlert(detection.AppName, TimeSpan.FromHours(2));
                _activeRecordingPresetId = presetId;
                StartRecording();
            };

            popup.FormClosed += (_, _) =>
            {
                _detector.DismissAppAlert(detection.AppName, TimeSpan.FromMinutes(30));
                if (ReferenceEquals(_openPopup, popup))
                    _openPopup = null;
            };

            popup.Show();
        });
    }

    private void ShowFirstRunIfNeeded()
    {
        if (_settings.HasCompletedFirstRun)
            return;

        MessageBox.Show(
            Textos.PrimeraVez,
            Textos.PrimeraVezTitulo,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        _settings.HasCompletedFirstRun = true;
        try { _store.Save(_settings); }
        catch (Exception ex) { AppLog.Error("No se pudo marcar el primer inicio", ex); }
    }

    private void ApplyHotkey()
    {
        if (!HotkeySpec.TryParse(_settings.Hotkey, out var spec))
            spec = HotkeySpec.Default;

        if (!_hotkeys.TryRegister(spec))
        {
            Balloon(string.Format(Textos.ErrorHotkey, spec));
            AppLog.Error("Atajo no registrado: " + spec);
        }
    }

    private void ToggleRecording()
    {
        if (_finalizing)
            return;
        if (_recorder.IsRecording)
            StopRecording(blocking: false);
        else
            StartRecording();
    }

    private void StartRecording(string? presetId = null)
    {
        lock (_busyGate)
        {
            if (_recorder.IsRecording || _finalizing)
                return;
        }

        if (_openPopup != null && !_openPopup.IsDisposed)
        {
            try { _openPopup.Close(); } catch { }
            _openPopup = null;
        }

        if (!string.IsNullOrWhiteSpace(presetId))
            _activeRecordingPresetId = presetId;

        try
        {
            _recorder.Start(_settings);
            _startItem.Enabled = false;
            _stopItem.Enabled = true;
            _tray.Icon = _recIcon;
            _tick.Start();
            UpdateTooltip();
            AppLog.Info($"Grabación iniciada con preset '{_activeRecordingPresetId}'.");
            if (!_recorder.HasLoopback)
                Balloon(Textos.BalloonLoopbackAviso);
            else if (!_recorder.HasMicrophone)
                Balloon(Textos.BalloonSoloSistema);
        }
        catch (Exception ex)
        {
            AppLog.Error("Error al iniciar", ex);
            MessageBox.Show(string.Format(Textos.ErrorIniciar, ex.Message), Textos.AppNombre,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            ResetTrayIdle();
        }
    }

    private void StopRecording(bool blocking)
    {
        lock (_busyGate)
        {
            if (!_recorder.IsRecording || _finalizing)
                return;
            _finalizing = true;
        }

        _tick.Stop();
        _tray.Text = Textos.TooltipGuardando;
        _startItem.Enabled = false;
        _stopItem.Enabled = false;
        _saved.Reset();

        RawRecordingResult raw;
        try
        {
            raw = _recorder.Stop();
        }
        catch (Exception ex)
        {
            _finalizing = false;
            _saved.Set();
            AppLog.Error("Error al detener", ex);
            MessageBox.Show(string.Format(Textos.ErrorDetener, ex.Message), Textos.AppNombre,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            ResetTrayIdle();
            return;
        }

        if (blocking)
            FinalizeRecording(raw);
        else
            ThreadPool.QueueUserWorkItem(_ => FinalizeRecording(raw));
    }

    private void FinalizeRecording(RawRecordingResult raw)
    {
        try
        {
            var paths = raw.Paths;
            string audioPath;
            if (Mp3Encoder.TryEncode(raw.WavPath, paths.Mp3Path, out var encodeError, _settings.Mp3BitrateKbps))
            {
                audioPath = paths.Mp3Path;
                TryDelete(raw.WavPath);
            }
            else
            {
                AppLog.Error("Codificación MP3 no disponible, se guarda WAV. " + encodeError);
                audioPath = paths.WavPath;
                if (File.Exists(audioPath))
                    TryDelete(audioPath);
                File.Move(raw.WavPath, audioPath);
            }

            var sidecar = RecordingSidecarWriter.Create(raw.StartedAt, raw.EndedAt, audioPath);
            var sidecarPath = RecordingSidecarWriter.Write(sidecar, audioPath);
            AppLog.Info("Audio guardado: " + audioPath);

            var duration = sidecar.DurationSec;
            var name = Path.GetFileName(audioPath);
            BeginOnUi(() => Balloon(string.Format(Textos.BalloonListo, $"{name} · {duration}s")));

            // Webhook opcional si está configurado
            if (!string.IsNullOrWhiteSpace(_settings.WebhookUrl))
            {
                var payload = new RecordingReadyPayload
                {
                    Path = audioPath,
                    Sidecar = sidecarPath,
                    EndedAt = sidecar.EndedAt,
                    DurationSec = sidecar.DurationSec
                };
                _ = _webhook.NotifyRecordingReadyAsync(_settings.WebhookUrl, _settings.WebhookBearerToken, payload);
            }

            // Pipeline completo: Transcripción + LLM con Prompt Maestro
            var pipelineResult = _pipelineCoordinator.ProcessRecordingAsync(
                audioPath,
                raw.StartedAt,
                raw.EndedAt,
                _activeRecordingPresetId,
                _detectedMeetingApp).GetAwaiter().GetResult();

            var context = new MeetingContext
            {
                StartedAt = raw.StartedAt,
                EndedAt = raw.EndedAt,
                DurationSec = duration,
                AudioPath = audioPath,
                MeetingApp = _detectedMeetingApp,
                SuggestedTitle = Path.GetFileNameWithoutExtension(audioPath)
            };

            _lastResult = pipelineResult;
            _lastContext = context;

            BeginOnUi(() =>
            {
                _lastMeetingItem.Enabled = true;

                if (pipelineResult.Success)
                {
                    if (_settings.AutoCopyToClipboard && !string.IsNullOrWhiteSpace(pipelineResult.SummaryMarkdown))
                    {
                        try { Clipboard.SetText(pipelineResult.SummaryMarkdown); } catch { }
                    }

                    Balloon(Textos.BalloonAnalisisCompletado);

                    if (_settings.AutoOpenViewerOnFinish)
                    {
                        ShowMeetingResultForm(pipelineResult, context);
                    }
                }
                else
                {
                    Balloon($"Aviso: {pipelineResult.ErrorMessage}");
                    if (!string.IsNullOrWhiteSpace(pipelineResult.TranscriptText))
                    {
                        ShowMeetingResultForm(pipelineResult, context);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            AppLog.Error("Error al finalizar grabación", ex);
            BeginOnUi(() => MessageBox.Show(string.Format(Textos.ErrorDetener, ex.Message), Textos.AppNombre,
                MessageBoxButtons.OK, MessageBoxIcon.Error));
        }
        finally
        {
            _saved.Set();
            BeginOnUi(() =>
            {
                _finalizing = false;
                ResetTrayIdle();
            });
        }
    }

    private void UpdateTrayProgress(string statusMessage)
    {
        _tray.Text = statusMessage.Length > 63 ? statusMessage[..63] : statusMessage;
    }

    private void OpenLastMeetingResult()
    {
        if (_lastResult != null && _lastContext != null)
        {
            ShowMeetingResultForm(_lastResult, _lastContext);
        }
    }

    private void ShowMeetingResultForm(MeetingPipelineResult result, MeetingContext context)
    {
        if (_openResultForm is { IsDisposed: false })
        {
            _openResultForm.BringToFront();
            _openResultForm.Activate();
            return;
        }

        _openResultForm = new MeetingResultForm(result, _pipelineCoordinator, _presetStore, context);
        _openResultForm.Show();
    }

    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(_settings.OutputFolder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _settings.OutputFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Textos.ErrorCarpeta, ex.Message), Textos.AppNombre,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenSettings()
    {
        if (_openSettings is { IsDisposed: false })
        {
            _openSettings.BringToFront();
            _openSettings.Activate();
            return;
        }

        _openSettings = new SettingsForm(_settings, _presetStore);
        _openSettings.FormClosed += (_, _) =>
        {
            if (_openSettings.DialogResult == DialogResult.OK)
            {
                try
                {
                    _settings = _openSettings.ResultSettings;
                    _store.Save(_settings);
                    Directory.CreateDirectory(_settings.OutputFolder);
                    StartupService.Apply(_settings.StartWithWindows);
                    _detector.IsEnabled = _settings.EnableAutoMeetingDetection;
                    ApplyHotkey();
                    RebuildPresetSubMenu();
                    AppLog.Info("Ajustes guardados.");
                }
                catch (Exception ex)
                {
                    AppLog.Error("Error al guardar ajustes", ex);
                    MessageBox.Show(string.Format(Textos.ErrorAjustes, ex.Message), Textos.AppNombre,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            _openSettings.Dispose();
            _openSettings = null;
        };
        _openSettings.Show();
    }

    private void ExitApp()
    {
        if (_recorder.IsRecording || _finalizing)
        {
            var ask = MessageBox.Show(
                Textos.SalirConGrabacion,
                Textos.AppNombre,
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
            if (ask != DialogResult.OK)
                return;

            if (_recorder.IsRecording)
                StopRecording(blocking: true);
            else
                _saved.Wait(TimeSpan.FromSeconds(45));
        }

        ExitThread();
    }

    private void ResetTrayIdle()
    {
        _startItem.Enabled = !_finalizing;
        _stopItem.Enabled = false;
        _tray.Icon = _idleIcon;
        _tray.Text = Textos.TooltipIdle;
        _tick.Stop();
    }

    private void UpdateTooltip()
    {
        if (!_recorder.IsRecording)
            return;
        var e = _recorder.Elapsed;
        var text = e.TotalHours >= 1
            ? e.ToString(@"h\:mm\:ss")
            : e.ToString(@"mm\:ss");
        var tip = string.Format(Textos.TooltipGrabando, text);
        _tray.Text = tip.Length <= 63 ? tip : tip[..63];
    }

    private void Balloon(string text)
    {
        _tray.BalloonTipTitle = Textos.AppNombre;
        _tray.BalloonTipText = text.Length > 250 ? text[..250] : text;
        _tray.BalloonTipIcon = ToolTipIcon.Info;
        _tray.ShowBalloonTip(4500);
    }

    private void BeginOnUi(Action action)
    {
        if (_ui.IsHandleCreated && _ui.InvokeRequired)
            _ui.BeginInvoke(action);
        else
            action();
    }

    protected override void ExitThreadCore()
    {
        _tick.Stop();
        _detector.Dispose();
        _hotkeys.Dispose();
        _window.Dispose();
        _recorder.Dispose();
        _http.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();
        _idleIcon.Dispose();
        _recIcon.Dispose();
        _openSettings?.Dispose();
        _openResultForm?.Dispose();
        _openPopup?.Dispose();
        _ui.Dispose();
        _saved.Dispose();
        AppLog.Info("Aplicación cerrada.");
        base.ExitThreadCore();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }
}
