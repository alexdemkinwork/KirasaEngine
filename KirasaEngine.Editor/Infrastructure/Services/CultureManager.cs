namespace KirasaEngine.Editor.Infrastructure.Services;

public static class CultureManager
{
    public static void SetCulture(CultureType cultureType)
    {
        Resources.Culture = CultureInfo.GetCultureInfo(Enum.GetName(cultureType)!.Replace("_", "-"));
    }
}
