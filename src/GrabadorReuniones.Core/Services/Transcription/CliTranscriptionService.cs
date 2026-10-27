using System.Diagnostics;
using System.Text;

namespace GrabadorReuniones.Core;

public sealed class CliTranscriptionService : ITranscriptionService
{
    private readonly string _cliTool; // agy, claude, whisper, custom
    private readonly string _customCommandTemplate;
    private readonly string _language;

    public CliTranscriptionService(
        string cliTool = "agy",
        string customCommandTemplate = "",
        string language = "es")
    {
        _cliTool = string.IsNullOrWhiteSpace(cliTool) ? "agy" : cliTool;
        _customCommandTemplate = customCommandTemplate;
        _language = string.IsNullOrWhiteSpace(language) ? "es" : language;
    }

    public async Task<TranscriptionResult> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
            return TranscriptionResult.Failed($"El archivo de audio no existe: {audioFilePath}");

        var tempOutputDir = Path.Combine(Path.GetTempPath(), $"transcribe_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempOutputDir);

        try
        {
            var (fileName, arguments) = ResolveCliExecution(_cliTool, _customCommandTemplate, audioFilePath, tempOutputDir, _language);

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            // Para whisper local, si generó un archivo .txt en tempOutputDir, leerlo
            var expectedTxt = Path.Combine(tempOutputDir, Path.GetFileNameWithoutExtension(audioFilePath) + ".txt");
            if (File.Exists(expectedTxt))
            {
                var txt = await File.ReadAllTextAsync(expectedTxt, Encoding.UTF8, cancellationToken);
                if (!string.IsNullOrWhiteSpace(txt))
                    return TranscriptionResult.Succeeded(txt.Trim(), language: _language);
            }

            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
            {
                return TranscriptionResult.Failed($"El comando CLI '{fileName}' finalizó con código {process.ExitCode}: {error}");
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                return TranscriptionResult.Failed($"El comando CLI de transcripción '{_cliTool}' no devolvió ninguna salida.");
            }

            return TranscriptionResult.Succeeded(output.Trim(), language: _language);
        }
        catch (Exception ex)
        {
            return TranscriptionResult.Failed($"Error al ejecutar la herramienta CLI de transcripción ({_cliTool}): {ex.Message}");
        }
        finally
        {
            try { if (Directory.Exists(tempOutputDir)) Directory.Delete(tempOutputDir, true); } catch { }
        }
    }

    private static (string FileName, string Arguments) ResolveCliExecution(
        string tool,
        string customTemplate,
        string audioFilePath,
        string outputDir,
        string language)
    {
        var lower = tool.ToLowerInvariant().Trim();

        if (lower == "claude")
        {
            var prompt = $"Transcribe de forma literal y completa en {language} el audio del archivo '{audioFilePath}'. Devuelve únicamente el texto plano de la transcripción, sin introducciones ni comentarios.";
            return ("cmd.exe", $"/c claude -p \"{prompt}\"");
        }

        if (lower == "whisper")
        {
            return ("cmd.exe", $"/c whisper \"{audioFilePath}\" --language {language} --output_format txt --output_dir \"{outputDir}\"");
        }

        if (lower == "custom" && !string.IsNullOrWhiteSpace(customTemplate))
        {
            var cmd = customTemplate
                .Replace("{AUDIO_FILE}", audioFilePath)
                .Replace("{AUDIO_PATH}", audioFilePath)
                .Replace("{OUTPUT_DIR}", outputDir)
                .Replace("{LANGUAGE}", language);
            return ("cmd.exe", $"/c {cmd}");
        }

        // Por defecto: Google Antigravity CLI (agy)
        var agyPrompt = $"Transcribe de forma literal y completa en {language} el audio del archivo '{audioFilePath}'. Devuelve únicamente el texto plano de la transcripción.";
        return ("cmd.exe", $"/c agy run \"{agyPrompt}\"");
    }
}
