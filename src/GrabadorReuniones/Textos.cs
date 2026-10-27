namespace GrabadorReuniones;

internal static class Textos
{
    public const string AppNombre = "Grabador de Reuniones IA";

    public const string MenuIniciar = "Iniciar grabación";
    public const string MenuDetener = "Detener grabación";
    public const string MenuTipoReunion = "Tipo de reunión (Prompt)";
    public const string MenuUltimaReunion = "Ver última reunión";
    public const string MenuCarpeta = "Abrir carpeta de grabaciones";
    public const string MenuAjustes = "Ajustes";
    public const string MenuSalir = "Salir";

    public const string TooltipIdle = "Grabador de Reuniones IA";
    public const string TooltipGrabando = "Grabando… {0}";
    public const string TooltipGuardando = "Guardando audio…";
    public const string TooltipTranscribiendo = "Transcribiendo audio…";
    public const string TooltipAnalizando = "Analizando con IA…";

    public const string YaEnEjecucion =
        "El Grabador de Reuniones IA ya está en ejecución.\nMírelo en la bandeja del sistema (junto al reloj).";

    public const string PrimeraVezTitulo = "Grabador de Reuniones IA";
    public const string PrimeraVez =
        "Bienvenido al Grabador de Reuniones IA.\n\n" +
        "Esta aplicación captura:\n" +
        "• Su micrófono (su voz)\n" +
        "• El audio de reproducción del PC (participantes de Teams, Meet, Zoom, etc.)\n\n" +
        "Al finalizar cada reunión:\n" +
        "1. Transcribe automáticamente el audio a texto.\n" +
        "2. Aplica un Prompt Maestro según el tipo de reunión (Comercial, Daily, Técnica, etc.).\n" +
        "3. Guarda el audio, la transcripción y las notas estructuradas en Markdown listas para Notion o correo.\n\n" +
        "Pulse con el botón derecho en el icono de la bandeja para acceder a Ajustes y configurar sus proveedores de IA o el modo CLI.";

    public const string SalirConGrabacion =
        "Hay una grabación en curso. Se detendrá y se guardará antes de salir.";

    public const string ErrorIniciar = "No se pudo iniciar la grabación:\n{0}";
    public const string ErrorDetener = "No se pudo guardar la grabación:\n{0}";
    public const string ErrorCarpeta = "No se pudo abrir la carpeta:\n{0}";
    public const string ErrorAjustes = "No se pudieron guardar los ajustes:\n{0}";
    public const string ErrorHotkey = "No se pudo registrar el atajo {0}. Quizá lo usa otra aplicación.";

    public const string BalloonListo = "Grabación finalizada ({0}). Procesando transcripción y resumen...";
    public const string BalloonAnalisisCompletado = "¡Reunión analizada con éxito! Haga clic para ver las notas.";
    public const string BalloonLoopbackAviso =
        "Se está grabando solo el micrófono. El audio de Teams/navegador puede no incluirse.";
    public const string BalloonSoloSistema =
        "No hay micrófono disponible. Se grabará solo el audio de reproducción del PC.";

    public const string AjustesTitulo = "Ajustes — Grabador de Reuniones IA";
    public const string Carpeta = "Carpeta de grabaciones";
    public const string Examinar = "Examinar…";
    public const string Microfono = "Micrófono";
    public const string Loopback = "Reproducción (audio de videollamadas)";
    public const string DispositivoPredeterminado = "(Predeterminado del sistema)";
    public const string ActualizarDispositivos = "Actualizar lista";
    public const string Atajo = "Atajo global";
    public const string IniciarWindows = "Iniciar con Windows";
    public const string Guardar = "Guardar";
    public const string Cancelar = "Cancelar";
}
