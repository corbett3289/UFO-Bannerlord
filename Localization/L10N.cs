using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using UFO.Setting;


public static class L10N
{
    private static readonly object SyncRoot = new object();

    public static class Keys
    {
        public const string Global = "Global";

        public const string ModName = "ModName";

        public const string CombatPlayerGroupName = "Combat_Player";

        public const string CombatPartyGroupName = "Combat_Party";

        public const string CombatAlliesGroupName = "Combat_Allies";

        public const string CombatEnemiesGroupName = "Combat_Enemies";

        public const string CombatMiscGroupName = "Combat_Misc";

        public const string GeneralGroupName = "General";

        public const string MapGroupName = "Map";

        public const string InventoryGroupName = "Inventory";

        public const string PartyGroupName = "Party";

        public const string ClanGroupName = "Clan";

        public const string KingdomGroupName = "Kingdom";

        public const string ExperienceGroupName = "Experience";

        public const string SiegesGroupName = "Sieges";

        public const string ArmyGroupName = "Army";

        public const string SmithingGroupName = "Smithing";

        public const string SettlementsGroupName = "Settlements";

        public const string CharactersGroupName = "Characters";

        public const string WorkshopsGroupName = "Workshops";
    }

    private static Dictionary<string, string> Values = new Dictionary<string, string>();
    private static bool FallbackLoadAttempted;

    public static void LoadLanguage()
    {
        Setting_Language language = Setting_Language.English;
        try
        {
            language = SettingsManager.LanguageSetting.Value;
        }
        catch
        {
            // MCM can request settings metadata before its global instance exists.
        }

        LoadLanguageFiles(EnumExtensions.ToLanguage(language), "English.resx", "Other.resx");
    }

    private static void EnsureLanguageLoaded()
    {
        if (Values.Count > 0 || FallbackLoadAttempted)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (Values.Count > 0 || FallbackLoadAttempted)
            {
                return;
            }

            FallbackLoadAttempted = true;
            LoadLanguageFiles("English.resx", "Other.resx");
        }
    }

    private static void LoadLanguageFiles(params string[] candidates)
    {
        string moduleDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(moduleDirectory))
        {
            return;
        }

        HashSet<string> attemptedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> loadedValues = null;

        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate) &&
                attemptedFiles.Add(candidate) &&
                TryLoadLanguageFile(Path.Combine(moduleDirectory, candidate), out loadedValues))
            {
                break;
            }
        }

        lock (SyncRoot)
        {
            FallbackLoadAttempted = true;
            Values = loadedValues ?? new Dictionary<string, string>();
        }
    }

    private static bool TryLoadLanguageFile(string path, out Dictionary<string, string> loadedValues)
    {
        loadedValues = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            XDocument document = XDocument.Load(path);
            XElement root = document.Root;
            if (root == null)
            {
                return false;
            }

            Dictionary<string, string> parsedValues = new Dictionary<string, string>();
            foreach (XElement item in root.Descendants("data"))
            {
                string key = item.Attribute("name")?.Value;
                string text = item.Element("value")?.Value;
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(text))
                {
                    parsedValues[key] = text;
                }
            }

            if (parsedValues.Count == 0)
            {
                return false;
            }

            loadedValues = parsedValues;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    public static string GetText(string key)
    {
        EnsureLanguageLoaded();
        string value;
        return Values.TryGetValue(key, out value) ? value : key;
    }

    public static string GetTextFormat(string key, params object[] formatValues)
    {
        EnsureLanguageLoaded();
        if (!Values.TryGetValue(key, out var value))
        {
            return key;
        }
        for (int i = 0; i < formatValues.Length; i++)
        {
            value = value.Replace($"{{{i}}}", formatValues[i].ToString());
        }
        return value;
    }
}
