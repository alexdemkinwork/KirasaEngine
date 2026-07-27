namespace KirasaEngine.Editor.Infrastructure.Services;

[RegisterSingleton]
public class LanguageService
{
    public void SetCulture(CultureType cultureType)
    {
        Resources.Culture = CultureInfo.GetCultureInfo(Enum.GetName(cultureType)!.Replace("_", "-"));
    }
}
