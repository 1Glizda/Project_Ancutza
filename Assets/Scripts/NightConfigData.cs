using System;
using System.Collections.Generic;
using UnityEngine;

namespace Han.CheckIn
{
    [Serializable]
    public class NightConfigEntry
    {
        [Tooltip("1-based night number (e.g. 1 for Night One, 2 for Night Two).")]
        public int nightNumber = 1;

        [Tooltip("Primary night title (e.g. 'Night One', 'Night Two').")]
        public string title = "Night One";

        [Tooltip("Night teaser subtitle (e.g. 'good luck', 'keep going').")]
        public string subtitle = "good luck";

        [Tooltip("Optional explicit combined headline. If left empty, formats automatically as '{title}, {subtitle}'.")]
        public string combinedTeaserText = "Night One, good luck";

        [Tooltip("How long (in seconds) the night teaser remains on screen before fading into Desk POV.")]
        [Range(1f, 10f)]
        public float displayDuration = 3.0f;

        [Tooltip("Optional audio stinger played when the night teaser appears.")]
        public AudioClip teaserStinger;

        /// <summary>
        /// Returns the localized title for this night entry.
        /// </summary>
        public string GetLocalizedTitle()
        {
            if (nightNumber >= 1)
                return HanLocalization.GetNightTitle(nightNumber);
            return title;
        }

        /// <summary>
        /// Returns the localized subtitle for this night entry.
        /// </summary>
        public string GetLocalizedSubtitle()
        {
            if (nightNumber >= 1)
                return HanLocalization.GetNightSubtitle(nightNumber);
            return subtitle;
        }

        /// <summary>
        /// Returns the full localized teaser line (e.g. "Night One, good luck").
        /// </summary>
        public string GetFullTeaserLine()
        {
            if (nightNumber >= 1)
                return HanLocalization.GetNightHeadline(nightNumber);

            if (!string.IsNullOrWhiteSpace(combinedTeaserText))
                return combinedTeaserText;

            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(subtitle))
                return $"{title}, {subtitle}";

            if (!string.IsNullOrWhiteSpace(title))
                return title;

            return HanLocalization.Format("night.fallback_title_num", nightNumber);
        }
    }

    [Serializable]
    public class NightConfigJsonWrapper
    {
        public List<NightConfigEntry> nights = new List<NightConfigEntry>();
    }

    /// <summary>
    /// Data-driven ScriptableObject asset holding all Night Teaser titles, subtitles, and timing configs.
    /// Allows editing night numbers and teaser text lines outside the scene without modifying UI prefabs or hierarchies.
    /// Also supports loading from an optional JSON TextAsset override.
    /// </summary>
    [CreateAssetMenu(fileName = "NightConfigData", menuName = "Han/Night Config Data", order = 1)]
    public class NightConfigData : ScriptableObject
    {
        [Header("Night Progression Entries")]
        [Tooltip("Ordered list of night teaser configurations (Index 0 = Night 1, Index 1 = Night 2, etc.).")]
        [SerializeField]
        private List<NightConfigEntry> nights = new List<NightConfigEntry>();

        [Header("Optional External JSON Override")]
        [Tooltip("If assigned, night entries will be parsed from this JSON file at runtime.")]
        [SerializeField]
        private TextAsset jsonOverrideAsset;

        [NonSerialized]
        private List<NightConfigEntry> runtimeJsonEntries;

        public IReadOnlyList<NightConfigEntry> Nights
        {
            get
            {
                if (jsonOverrideAsset != null)
                {
                    if (runtimeJsonEntries == null || runtimeJsonEntries.Count == 0)
                        LoadFromJson(jsonOverrideAsset.text);
                    if (runtimeJsonEntries != null && runtimeJsonEntries.Count > 0)
                        return runtimeJsonEntries;
                }

                if (nights == null || nights.Count == 0)
                    EnsureDefaultEntries();

                return nights;
            }
        }

        public int NightCount => Nights.Count;

        private void OnEnable()
        {
            if (nights == null || nights.Count == 0)
            {
                EnsureDefaultEntries();
            }
        }

        /// <summary>
        /// Retrieves the configuration for a 0-based night index (0 = Night 1, 1 = Night 2, etc.).
        /// Clamps safely or generates a fallback if the requested index exceeds configured entries.
        /// </summary>
        public NightConfigEntry GetNightByIndex(int nightIndex)
        {
            var list = Nights;
            if (list == null || list.Count == 0)
            {
                EnsureDefaultEntries();
                list = nights;
            }

            if (nightIndex >= 0 && nightIndex < list.Count)
                return list[nightIndex];

            int fallbackNightNum = Mathf.Max(1, nightIndex + 1);
            string localizedTitle = HanLocalization.GetNightTitle(fallbackNightNum);
            string localizedSubtitle = HanLocalization.GetNightSubtitle(fallbackNightNum);
            return new NightConfigEntry
            {
                nightNumber = fallbackNightNum,
                title = localizedTitle,
                subtitle = localizedSubtitle,
                combinedTeaserText = HanLocalization.GetNightHeadline(fallbackNightNum),
                displayDuration = 3.0f
            };
        }

        /// <summary>
        /// Retrieves the configuration for a 1-based night number (1 = Night One, 2 = Night Two, etc.).
        /// </summary>
        public NightConfigEntry GetNightByNumber(int nightNumber)
        {
            var list = Nights;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].nightNumber == nightNumber)
                    return list[i];
            }
            return GetNightByIndex(Mathf.Max(0, nightNumber - 1));
        }

        /// <summary>
        /// Populates default Transylvanian Han night progression entries if the asset is empty.
        /// </summary>
        [ContextMenu("Reset to Default Night Entries")]
        public void EnsureDefaultEntries()
        {
            nights = new List<NightConfigEntry>
            {
                new NightConfigEntry
                {
                    nightNumber = 1,
                    title = "Night One",
                    subtitle = "good luck",
                    combinedTeaserText = "Night One, good luck",
                    displayDuration = 3.0f
                },
                new NightConfigEntry
                {
                    nightNumber = 2,
                    title = "Night Two",
                    subtitle = "keep going",
                    combinedTeaserText = "Night Two, keep going",
                    displayDuration = 3.0f
                },
                new NightConfigEntry
                {
                    nightNumber = 3,
                    title = "Night Three",
                    subtitle = "trust no one",
                    combinedTeaserText = "Night Three, trust no one",
                    displayDuration = 3.0f
                },
                new NightConfigEntry
                {
                    nightNumber = 4,
                    title = "Night Four",
                    subtitle = "watch the doors",
                    combinedTeaserText = "Night Four, watch the doors",
                    displayDuration = 3.0f
                },
                new NightConfigEntry
                {
                    nightNumber = 5,
                    title = "Night Five",
                    subtitle = "the wolves are howling",
                    combinedTeaserText = "Night Five, the wolves are howling",
                    displayDuration = 3.0f
                },
                new NightConfigEntry
                {
                    nightNumber = 6,
                    title = "Night Six",
                    subtitle = "shadows lengthen",
                    combinedTeaserText = "Night Six, shadows lengthen",
                    displayDuration = 3.0f
                },
                new NightConfigEntry
                {
                    nightNumber = 7,
                    title = "Night Seven",
                    subtitle = "survive until dawn",
                    combinedTeaserText = "Night Seven, survive until dawn",
                    displayDuration = 3.5f
                }
            };
        }

        /// <summary>
        /// Parses night entries from a JSON string matching NightConfigJsonWrapper.
        /// </summary>
        public void LoadFromJson(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return;
            try
            {
                var wrapper = JsonUtility.FromJson<NightConfigJsonWrapper>(jsonText);
                if (wrapper != null && wrapper.nights != null && wrapper.nights.Count > 0)
                {
                    runtimeJsonEntries = wrapper.nights;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NightConfigData] Failed to parse JSON override: {ex.Message}");
            }
        }

        /// <summary>
        /// Exports current night entries to formatted JSON.
        /// </summary>
        public string ExportToJson(bool prettyPrint = true)
        {
            if (nights == null || nights.Count == 0) EnsureDefaultEntries();
            var wrapper = new NightConfigJsonWrapper { nights = nights };
            return JsonUtility.ToJson(wrapper, prettyPrint);
        }

        private static string NumberToWord(int number)
        {
            switch (number)
            {
                case 1: return "One";
                case 2: return "Two";
                case 3: return "Three";
                case 4: return "Four";
                case 5: return "Five";
                case 6: return "Six";
                case 7: return "Seven";
                case 8: return "Eight";
                case 9: return "Nine";
                case 10: return "Ten";
                default: return number.ToString();
            }
        }
    }
}
