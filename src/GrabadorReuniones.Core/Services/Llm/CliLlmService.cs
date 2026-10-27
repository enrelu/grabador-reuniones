using System.Diagnostics;
using System.Text;

namespace GrabadorReuniones.Core;

public sealed class CliLlmService : ILlmService
{
    private readonly string _cliTool; // agy, claude, gemini, custom
    private readonly string _customCommandTemplate;

    public CliLlmService(string cliTool = "agy", string customCommandTemplate = "")
    {
        _cliTool = string.IsNullOrWhiteSpace(cliTool) ? "agy" : cliTool;
        _customCommandTemplate = customCommandTemplate;
    }

    public async Task<LlmAnalysisResult> AnalyzeMeetingAsync(
        string transcriptText,
        PromptPreset preset,
        MeetingContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcriptText))
            return LlmAnalysisResult.Failed("La transcripción está vacía.");

        var systemPrompt = RenderTemplate(preset.SystemPrompt, context);
        var userPrompt = RenderTemplate(preset.UserPromptTemplate, context)
            .Replace("{TRANSCRIPCION}", transcriptText);

        var fullPrompt = $"{systemPrompt}\n\n---\n\n## TRANSCRIPCIÓN DE LA REUNIÓN:\n{userPrompt}";

        // Guardar el prompt en un archivo temporal para evitar límites de longitud de línea de comandos en Windows
        var tempPromptFile = Path.Combine(Path.GetTempPath(), $"meeting_prompt_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempPromptFile, fullPrompt, Encoding.UTF8, cancellationToken);

        try
        {
            var (fileName, arguments, pipeStdin) = ResolveCliExecution(_cliTool, _customCommandTemplate, tempPromptFile);

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = pipeStdin,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            if (pipeStdin)
            {
                await process.StandardInput.WriteAsync(fullPrompt);
                process.StandardInput.Close();
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
            {
                return LlmAnalysisResult.Failed($"El comando CLI '{fileName}' finalizó con código {process.ExitCode}: {error}");
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                return LlmAnalysisResult.Failed($"El comando CLI '{fileName}' no devolvió ninguna salida.");
            }

            return LlmAnalysisResult.Succeeded(output.Trim(), $"CLI ({_cliTool})");
        }
        catch (Exception ex)
        {
            return LlmAnalysisResult.Failed($"Error al ejecutar la herramienta CLI ({_cliTool}): {ex.Message}");
        }
        finally
        {
            try { if (File.Exists(tempPromptFile)) File.Delete(tempPromptFile); } catch { }
        }
    }

    private static (string FileName, string Arguments, bool PipeStdin) ResolveCliExecution(
        string tool,
        string customTemplate,
        string promptFilePath)
    {
        var lower = tool.ToLowerInvariant().Trim();

        if (lower == "claude")
        {
            // Claude CLI admite pasar el prompt como argumento o entrada estándar
            return ("cmd.exe", $"/c claude -p \"$(Get-Content '{promptFilePath}' -Raw)\"", false);
        }

        if (lower == "gemini")
        {
            return ("cmd.exe", $"/c gemini \"$(Get-Content '{promptFilePath}' -Raw)\"", false);
        }

        if (lower == "custom" && !string.IsNullOrWhiteSpace(customTemplate))
        {
            if (customTemplate.Contains("{PROMPT_FILE}"))
            {
                var cmd = customTemplate.Replace("{PROMPT_FILE}", promptFilePath);
                return ("cmd.exe", $"/c {cmd}", false);
            }

            return ("cmd.exe", $"/c {customTemplate}", true);
        }

        // Por defecto: Antigravity CLI (agy)
        // Ejecuta agy pasando las instrucciones por archivo de prompt
        return ("cmd.exe", $"/c agy run \"Analiza la reunión según el archivo adjunto: {promptFilePath}\"", false);
    }

    private static string RenderTemplate(string template, MeetingContext context)
    {
        return template
            .Replace("{TITULO_SUGERIDO}", context.SuggestedTitle)
            .Replace("{CLIENTE_O_PROYECTO}", context.SuggestedTitle)
            .Replace("{TEMA_PRINCIPAL}", context.SuggestedTitle)
            .Replace("{TEMA}", context.SuggestedTitle)
            .Replace("{PARTICIPANTES}", context.MeetingApp.Length > 0 ? $"Vía {context.MeetingApp}" : "Participantes de la reunión")
            .Replace("{FECHA}", context.StartedAt.ToString("yyyy-MM-dd HH:mm"))
            .Replace("{DURACION}", $"{context.DurationSec / 60}m {context.DurationSec % 60}s");
    }
}
