namespace GrabadorReuniones.Core;

public sealed class PromptPreset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "📋";
    public string SystemPrompt { get; set; } = "";
    public string UserPromptTemplate { get; set; } = "{TRANSCRIPCION}";
    public bool IsBuiltIn { get; set; }

    public PromptPreset Clone() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        Icon = Icon,
        SystemPrompt = SystemPrompt,
        UserPromptTemplate = UserPromptTemplate,
        IsBuiltIn = IsBuiltIn
    };
}
