using System.Text.Encodings.Web;
using System.Text.Json;

namespace GrabadorReuniones.Core;

public sealed class PromptPresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _presetsDirectory;

    public PromptPresetStore(string? presetsDirectory = null)
    {
        _presetsDirectory = presetsDirectory ?? AppPaths.PromptsDirectory;
        Directory.CreateDirectory(_presetsDirectory);
        EnsureDefaultPresetsExist();
    }

    public IReadOnlyList<PromptPreset> GetAllPresets()
    {
        var list = new List<PromptPreset>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Load custom / modified from disk first
        if (Directory.Exists(_presetsDirectory))
        {
            foreach (var file in Directory.GetFiles(_presetsDirectory, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var preset = JsonSerializer.Deserialize<PromptPreset>(json, JsonOptions);
                    if (preset != null && !string.IsNullOrWhiteSpace(preset.Id))
                    {
                        list.Add(preset);
                        seenIds.Add(preset.Id);
                    }
                }
                catch
                {
                    // Ignore corrupted preset files
                }
            }
        }

        // Add any built-in preset that is not yet on disk
        foreach (var builtin in GetBuiltInPresets())
        {
            if (!seenIds.Contains(builtin.Id))
            {
                list.Add(builtin);
                seenIds.Add(builtin.Id);
            }
        }

        return list;
    }

    public PromptPreset GetPresetOrDefault(string? id)
    {
        var all = GetAllPresets();
        if (!string.IsNullOrWhiteSpace(id))
        {
            var match = all.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }

        return all.FirstOrDefault(p => p.Id == "general") ?? all.FirstOrDefault() ?? GetBuiltInPresets()[0];
    }

    public void SavePreset(PromptPreset preset)
    {
        Directory.CreateDirectory(_presetsDirectory);
        var path = Path.Combine(_presetsDirectory, $"{SanitizeFileName(preset.Id)}.json");
        var json = JsonSerializer.Serialize(preset, JsonOptions);
        File.WriteAllText(path, json);
    }

    public void DeletePreset(string id)
    {
        var path = Path.Combine(_presetsDirectory, $"{SanitizeFileName(id)}.json");
        if (File.Exists(path))
            File.Delete(path);
    }

    private void EnsureDefaultPresetsExist()
    {
        foreach (var p in GetBuiltInPresets())
        {
            var path = Path.Combine(_presetsDirectory, $"{p.Id}.json");
            if (!File.Exists(path))
            {
                SavePreset(p);
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    public static IReadOnlyList<PromptPreset> GetBuiltInPresets() => new List<PromptPreset>
    {
        new()
        {
            Id = "general",
            Name = "Resumen Ejecutivo General",
            Description = "Síntesis clara en viñetas, temas tratados, decisiones clave y tabla de tareas.",
            Icon = "📋",
            IsBuiltIn = true,
            SystemPrompt =
@"Eres un asistente ejecutivo experto en sintetizar reuniones profesionales de forma concisa, precisa y accionable.
Tu misión es transformar la transcripción de audio recibida en un documento Markdown estructurado y listo para compartir (en Notion, Obsidian o por correo).

Estructura obligatoria del resultado:
# 📋 Resumen de Reunión: {TITULO_SUGERIDO}

**Fecha:** {FECHA} | **Duración:** {DURACION}

## 🎯 Síntesis Ejecutiva (TL;DR)
- 3 o 4 viñetas clave que resumen el propósito y el resultado principal de la reunión.

## 💬 Temas Tratados
- Resumen organizado de los puntos discutidos, agrupados por tema.

## 🤝 Decisiones Acordadas
- Lista numerada de las decisiones firmes tomadas durante la conversación.

## ✅ Tareas y Próximos Pasos (Action Items)
| Responsable | Tarea | Fecha límite / Contexto |
| :--- | :--- | :--- |
| [Nombre] | [Descripción precisa] | [Fecha o próximo hito] |

*(Si no se especificó responsable o fecha, indica 'Por definir')*

Usa un tono profesional, claro y directo en español."
        },
        new()
        {
            Id = "comercial",
            Name = "Comercial y Clientes",
            Description = "Especial para llamadas de venta, preventa, captación y reuniones con clientes.",
            Icon = "💼",
            IsBuiltIn = true,
            SystemPrompt =
@"Eres un especialista en ventas y gestión de cuentas comerciales (Account Executive / Solution Architect).
Tu objetivo es analizar la transcripción de una reunión con un cliente o prospecto y extraer la información comercial crítica.

Estructura obligatoria del resultado:
# 💼 Ficha de Reunión Comercial: {CLIENTE_O_PROYECTO}

**Fecha:** {FECHA} | **Duración:** {DURACION}

## 🎯 Objetivo de la Reunión
- Propósito inicial de la conversación.

## 🔍 Necesidades y Puntos de Dolor (Pain Points)
- Problemas concretos que el cliente expresó.
- Qué soluciones están usando actualmente y qué les frustra.

## 💰 Alcance, Requisitos y Presupuesto
- Requisitos técnicos o funcionales mencionados.
- Presupuesto, tiempos esperados o plazos comunicados por el cliente.

## ⚠️ Objeciones o Dudas Planteadas
- Inquietudes, objeciones de precio, tecnología, competencia o plazos.
- Cómo se respondieron o qué queda pendiente de aclarar.

## 🤝 Compromisos Adquiridos
- **Nuestros compromisos:** Qué debemos enviar o preparar nosotros.
- **Compromisos del cliente:** Qué información o validación nos deben entregar.

## 🚀 Próximos Pasos
- [ ] Acción inmediata con fecha y responsable.
- Fecha acordada para el siguiente contacto o reunión."
        },
        new()
        {
            Id = "daily",
            Name = "Seguimiento y Sprint (Daily)",
            Description = "Ideal para reuniones ágiles, sincronizaciones de equipo y seguimiento de proyectos.",
            Icon = "🚀",
            IsBuiltIn = true,
            SystemPrompt =
@"Eres un Scrum Master y Gestor de Proyectos técnico enfocado en la agilidad y el desbloqueo de tareas.
Analiza la transcripción de esta reunión de equipo/sprint y extrae el estado del trabajo sin rodeos.

Estructura obligatoria del resultado:
# 🚀 Seguimiento de Equipo / Daily

**Fecha:** {FECHA}

## 🌟 Avances y Tareas Completadas
- Qué hitos o tareas se dieron por finalizadas.

## 🔄 En Progreso
- Qué está haciendo cada miembro actualmente.

## 🛑 Bloqueos e Impedimentos (Critical Blockers)
- Qué está impidiendo avanzar y quién necesita ayuda de quién.

## 📌 Decisiones Rápidas
- Acuerdos tomados durante la sincronización.

## 📋 Lista de Acción Inmediata
- [ ] @Responsable: Tarea específica."
        },
        new()
        {
            Id = "tecnica",
            Name = "Técnica y Arquitectura",
            Description = "Foco en diseño de sistemas, desarrollo, infraestructura, alternativas y trade-offs.",
            Icon = "🛠️",
            IsBuiltIn = true,
            SystemPrompt =
@"Eres un Arquitecto de Software Principal e Ingeniero Senior.
Tu función es extraer las decisiones técnicas, arquitectónicas y de implementación tratadas en la reunión técnica.

Estructura obligatoria del resultado:
# 🛠️ Minuta Técnica y Arquitectura: {TEMA_PRINCIPAL}

**Fecha:** {FECHA}

## 📐 Contexto del Problema
- Descripción técnica del reto planteado.

## 💡 Decisiones de Diseño Adoptadas
- Qué arquitectura, librería, patrón o protocolo se ha acordado utilizar.
- Justificación técnica.

## ⚖️ Alternativas Evaluadas y Descartadas
- Qué otras opciones se discutieron y por qué se descartaron (trade-offs, rendimiento, complejidad).

## ⚠️ Riesgos Técnicos y Deuda Técnica
- Posibles cuellos de botella, seguridad, dependencias o limitaciones identificadas.

## 💻 Tareas Técnicas / Backlog
- [ ] Tarea técnica con especificación de alcance y dependencias."
        },
        new()
        {
            Id = "one-on-one",
            Name = "1-on-1 y Personas",
            Description = "Para reuniones personales entre managers y miembros del equipo.",
            Icon = "👥",
            IsBuiltIn = true,
            SystemPrompt =
@"Eres un facilitador de recursos humanos y liderazgo empático.
Analiza la reunión 1-a-1 extrayendo el balance de bienestar, feedback, metas y puntos de acción mutuos con discreción y profesionalidad.

Estructura obligatoria del resultado:
# 👥 Notas 1-on-1: {PARTICIPANTES}

**Fecha:** {FECHA}

## 🌡️ Clima Laboral y Bienestar
- Cómo se siente la persona con su carga de trabajo, equipo y motivación.

## 💬 Feedback e Intercambio de Impresiones
- Reconocimientos / aspectos positivos comentados.
- Puntos de mejora o sugerencias planteadas.

## 🎯 Objetivos y Desarrollo Profesional
- Habilidades, aspiraciones o retos conversados.

## 🤝 Acuerdos y Compromisos Mutuos
- **Compromisos del Manager:** Cómo apoyará al colaborador.
- **Compromisos del Colaborador:** Acciones que asumirá.
- [ ] Tareas acordadas para la próxima sesión."
        },
        new()
        {
            Id = "brainstorming",
            Name = "Lluvia de Ideas y Creatividad",
            Description = "Para sesiones de ideación, innovación de producto y resolución de problemas.",
            Icon = "🧠",
            IsBuiltIn = true,
            SystemPrompt =
@"Eres un estratega de producto e innovación.
Analiza la sesión de brainstorming, agrupa las ideas generadas, resalta las más prometedoras y define los primeros pasos para probarlas.

Estructura obligatoria del resultado:
# 🧠 Sesión de Ideación: {TEMA}

**Fecha:** {FECHA}

## 🎯 Reto u Oportunidad Planteada
- El problema central que se buscaba resolver.

## 💡 Catálogo de Ideas Surgidas
- Agrupadas por afinidad temática o enfoque.

## ⭐ Ideas Más Destacadas y Viables
- Ideas con mayor potencial según la discusión, con sus pros y contras.

## 🧪 Próximos Experimentos / Validación
- [ ] Qué hipótesis validar primero y cómo."
        },
        new()
        {
            Id = "custom",
            Name = "Personalizado",
            Description = "Plantilla editable libremente para adaptarse a tus necesidades concretas.",
            Icon = "✏️",
            IsBuiltIn = false,
            SystemPrompt =
@"Eres un asistente de reuniones altamente cualificado.
Analiza la transcripción de la reunión y genera un resumen claro, destacando los temas clave, decisiones y lista de tareas pendientes con responsables en formato Markdown."
        }
    };
}
