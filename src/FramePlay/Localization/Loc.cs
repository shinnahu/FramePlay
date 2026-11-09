using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace FramePlay.Localization;

/// <summary>UI strings in English and Spanish. XAML binds through {l:Tr key}, so switching language updates every window live.</summary>
public sealed partial class Loc : INotifyPropertyChanged
{
    public static Loc I { get; } = new();

    private string _lang = "en";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;

    public string Language => _lang;

    public string this[string key] => Get(key);

    public static string T(string key) => I.Get(key);

    public static string F(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, I.Get(key), args);

    public string Get(string key)
    {
        if (_lang == "es" && (Es.TryGetValue(key, out var es) || SettingsEs.TryGetValue(key, out es))) return es;
        return En.TryGetValue(key, out var en) || SettingsEn.TryGetValue(key, out en) ? en : key;
    }

    /// <summary>"auto" follows the Windows display language (Spanish if it's Spanish, English otherwise).</summary>
    public static string Resolve(string setting) => setting switch
    {
        "en" or "es" => setting,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es" : "en",
    };

    public void SetLanguage(string setting)
    {
        _lang = Resolve(setting);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        Changed?.Invoke();
    }
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]") { Source = Loc.I, Mode = BindingMode.OneWay };
        return binding.ProvideValue(serviceProvider);
    }
}
