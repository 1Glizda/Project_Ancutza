using System;
using System.Collections.Generic;
using UnityEngine;

namespace Han.CheckIn
{
    /// <summary>
    /// Central runtime & editor localization registry supporting English, authentic period Romanian,
    /// and Hungarian across uGUI menus, the 3D Ledger Book, SpeechBubbles, IMGUI overlays,
    /// procedural guests, and NPC dialogue.
    /// </summary>
    public static partial class HanLocalization
    {
        public readonly struct Entry
        {
            public readonly string En;
            public readonly string Ro;
            public readonly string Hu;

            public Entry(string en, string ro, string hu)
            {
                En = en;
                Ro = ro;
                Hu = hu;
            }

            public string Resolve(HanLanguage lang)
            {
                switch (lang)
                {
                    case HanLanguage.Romanian:  return !string.IsNullOrEmpty(Ro) ? Ro : En;
                    case HanLanguage.Hungarian: return !string.IsNullOrEmpty(Hu) ? Hu : En;
                    case HanLanguage.English:
                    default:                    return En;
                }
            }
        }

        private const string PlayerPrefsKey = "Han_Language";

        private static readonly Dictionary<string, Entry> Table = new Dictionary<string, Entry>(256);
        private static bool initialized;
        private static bool languageLoaded;
        private static HanLanguage currentLanguage = HanLanguage.Romanian;

        /// <summary>Fired whenever the active language changes.</summary>
        public static event Action<HanLanguage> OnLanguageChanged;

        /// <summary>Currently active language (persisted in PlayerPrefs).</summary>
        public static HanLanguage CurrentLanguage
        {
            get
            {
                EnsureLanguageLoaded();
                return currentLanguage;
            }
        }

        public static IReadOnlyDictionary<string, Entry> Entries
        {
            get
            {
                EnsureInitialized();
                return Table;
            }
        }

        private static void EnsureLanguageLoaded()
        {
            if (languageLoaded) return;
            languageLoaded = true;
            try
            {
                int saved = PlayerPrefs.GetInt(PlayerPrefsKey, (int)HanLanguage.Romanian);
                currentLanguage = Enum.IsDefined(typeof(HanLanguage), saved)
                    ? (HanLanguage)saved
                    : HanLanguage.Romanian;
            }
            catch
            {
                currentLanguage = HanLanguage.Romanian;
            }
        }

        private static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            RegisterUIStrings();
            RegisterGameplayStrings();
        }

        private static void Add(string key, string en, string ro, string hu)
        {
            Table[key] = new Entry(en, ro, hu);
        }

        /// <summary>Switches to the specified language, saves it, and notifies all listeners.</summary>
        public static void SetLanguage(HanLanguage language, bool savePreference = true)
        {
            EnsureLanguageLoaded();
            EnsureInitialized();

            bool changed = currentLanguage != language;
            currentLanguage = language;

            if (savePreference)
            {
                try
                {
                    PlayerPrefs.SetInt(PlayerPrefsKey, (int)language);
                    PlayerPrefs.Save();
                }
                catch
                {
                    // Ignore PlayerPrefs failures outside Unity runtime thread.
                }
            }

            if (changed)
            {
                OnLanguageChanged?.Invoke(currentLanguage);
            }
        }

        /// <summary>Cycles English -> Romanian -> Hungarian -> English and returns the new language.</summary>
        public static HanLanguage CycleLanguage()
        {
            HanLanguage next = (HanLanguage)(((int)CurrentLanguage + 1) % 3);
            SetLanguage(next, savePreference: true);
            return next;
        }

        /// <summary>Looks up a localized string by key for the current language.</summary>
        public static string Get(string key)
        {
            return Get(key, CurrentLanguage);
        }

        /// <summary>Looks up a localized string by key for an explicit language.</summary>
        public static string Get(string key, HanLanguage language)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            EnsureInitialized();
            if (Table.TryGetValue(key, out Entry entry))
                return entry.Resolve(language);
            return key;
        }

        /// <summary>Selects directly among three inline variants for the active language.</summary>
        public static string Pick(string en, string ro, string hu)
        {
            switch (CurrentLanguage)
            {
                case HanLanguage.Romanian:  return !string.IsNullOrEmpty(ro) ? ro : en;
                case HanLanguage.Hungarian: return !string.IsNullOrEmpty(hu) ? hu : en;
                case HanLanguage.English:
                default:                    return en;
            }
        }

        /// <summary>Formats a localized template string safely.</summary>
        public static string Format(string key, params object[] args)
        {
            string template = Get(key);
            if (args == null || args.Length == 0) return template;
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }
    }
}
