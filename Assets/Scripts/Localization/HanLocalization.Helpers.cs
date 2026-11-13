using System.Collections.Generic;

namespace Han.CheckIn
{
    public static partial class HanLocalization
    {
        private static Dictionary<string, string> builtInDialogueToKey;

        /// <summary>Returns the localized night title (e.g. "Night One" / "Noaptea Întâi" / "Első éjszaka").</summary>
        public static string GetNightTitle(int nightNumber)
        {
            if (nightNumber >= 1 && nightNumber <= 7)
                return Get($"night.{nightNumber}.title");
            return Format("night.fallback_title_num", nightNumber);
        }

        /// <summary>Returns the localized night teaser subtitle for a 1-based night number.</summary>
        public static string GetNightSubtitle(int nightNumber)
        {
            if (nightNumber >= 1 && nightNumber <= 7)
                return Get($"night.{nightNumber}.subtitle");
            return Get("night.2.subtitle");
        }

        /// <summary>Returns the localized combined night teaser headline.</summary>
        public static string GetNightHeadline(int nightNumber)
        {
            return Format("night.fallback_combined", GetNightTitle(nightNumber), GetNightSubtitle(nightNumber));
        }

        /// <summary>Returns the localized personality name.</summary>
        public static string GetPersonalityLabel(PersonalityType personality)
        {
            return Get($"guest.personality.{personality}");
        }

        /// <summary>
        /// Maps built-in English lines from DefaultDialogue.asset and DialogueData fallbacks
        /// to their localized equivalent in the active language, leaving unknown custom text untouched.
        /// </summary>
        public static string LocalizeBuiltInDialogueLine(string rawLine)
        {
            if (string.IsNullOrEmpty(rawLine)) return rawLine;
            EnsureInitialized();
            EnsureDialogueReverseLookup();

            if (builtInDialogueToKey.TryGetValue(rawLine, out string key))
                return Get(key);

            return rawLine;
        }

        private static void EnsureDialogueReverseLookup()
        {
            if (builtInDialogueToKey != null) return;
            builtInDialogueToKey = new Dictionary<string, string>(64);

            foreach (var kvp in Table)
            {
                if (kvp.Key.StartsWith("dlg.norm.") || kvp.Key.StartsWith("dlg.murd."))
                {
                    if (!string.IsNullOrEmpty(kvp.Value.En) && !builtInDialogueToKey.ContainsKey(kvp.Value.En))
                        builtInDialogueToKey[kvp.Value.En] = kvp.Key;
                }
            }
        }
    }
}
