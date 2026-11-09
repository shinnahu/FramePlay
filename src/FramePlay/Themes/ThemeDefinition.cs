namespace FramePlay.Themes;

/// <summary>
/// A colour theme. Uses the same token names and JSON shape as MarkNotes themes, so .mntheme files
/// move between the two apps.
/// </summary>
public sealed class ThemeDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "FramePlay";
    /// <summary>"dark" or "light".</summary>
    public string Base { get; set; } = "dark";
    /// <summary>Token overrides, e.g. "bg-0" -> "#1e1f22". Missing tokens come from the base.</summary>
    public Dictionary<string, string> Vars { get; set; } = new();
    /// <summary>MarkNotes custom CSS. Kept so exported themes round-trip; FramePlay doesn't use it.</summary>
    public string Css { get; set; } = "";
    public bool Custom { get; set; }

    public ThemeDefinition Clone() => new()
    {
        Id = Id, Name = Name, Author = Author, Base = Base, Css = Css, Custom = Custom,
        Vars = new Dictionary<string, string>(Vars),
    };
}
