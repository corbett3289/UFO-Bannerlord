using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using UFO.Setting;


public static class L10N
{
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

    public static void LoadLanguage()
    {
        string moduleDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string selectedLanguage = EnumExtensions.ToLanguage(SettingsManager.LanguageSetting.Value);
        string[] candidates = { selectedLanguage, "English.resx", "Other.resx" };
        HashSet<string> attemptedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string candidate in candidates)
        {
            if (!attemptedFiles.Add(candidate))
            {
                continue;
            }

            string path = Path.Combine(moduleDirectory, candidate);
            if (TryLoadLanguageFile(path, out Dictionary<string, string> loadedValues))
            {
                Values = loadedValues;
                return;
            }
        }

        Values = new Dictionary<string, string>();
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
        string value;
        return Values.TryGetValue(key, out value) ? value : key;
    }

    public static string GetTextFormat(string key, params object[] formatValues)
    {
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
