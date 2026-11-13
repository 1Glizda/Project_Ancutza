using System;
using UnityEngine;

namespace Han.CheckIn
{
    /// <summary>Esmeralda's emotional state at the start of a new day, shaped by the previous night.</summary>
    public enum EsmeraldaDayMood
    {
        PrimaZi = 0,
        Optimist = 1,
        Confuz = 2,
        Speriat = 3
    }

    /// <summary>Structured beat for the Visual Novel Intro sequence.</summary>
    [Serializable]
    public struct IntroNarrativeBeat
    {
        public string chapterTag;
        [TextArea(3, 6)]
        public string bodyText;

        public IntroNarrativeBeat(string chapterTag, string bodyText)
        {
            this.chapterTag = chapterTag;
            this.bodyText = bodyText;
        }
    }

    /// <summary>Single question-and-answer pool entry used during check-in investigation.</summary>
    [Serializable]
    public struct InvestigationPromptSet
    {
        public string question;
        public string[] answers;

        public InvestigationPromptSet(string question, params string[] answers)
        {
            this.question = question;
            this.answers = answers ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Central localized dialogue and narrative text bank for Esmeralda's Inn
    /// (Intro, day opening moods, reception greetings, and monster investigation prompts).
    /// </summary>
    public static class HanDialogueCatalog
    {
        public static IntroNarrativeBeat[] DefaultIntroBeats => new[]
        {
            new IntroNarrativeBeat(HanLocalization.Get("catalog.intro.0.tag"), HanLocalization.Get("catalog.intro.0.body")),
            new IntroNarrativeBeat(HanLocalization.Get("catalog.intro.1.tag"), HanLocalization.Get("catalog.intro.1.body")),
            new IntroNarrativeBeat(HanLocalization.Get("catalog.intro.2.tag"), HanLocalization.Get("catalog.intro.2.body")),
            new IntroNarrativeBeat(HanLocalization.Get("catalog.intro.3.tag"), HanLocalization.Get("catalog.intro.3.body"))
        };

        // ── DAY OPENING LINES BY MOOD ─────────────────────────────────────────
        public static string DayLinePrimaZi  => HanLocalization.Get("catalog.mood.PrimaZi.line");
        public static string DayLineOptimist => HanLocalization.Get("catalog.mood.Optimist.line");
        public static string DayLineConfuz   => HanLocalization.Get("catalog.mood.Confuz.line");
        public static string DayLineSperiat  => HanLocalization.Get("catalog.mood.Speriat.line");

        public static string GetDayOpeningLine(EsmeraldaDayMood mood)
        {
            return HanLocalization.Get($"catalog.mood.{mood}.line");
        }

        public static string GetDayMoodTitle(EsmeraldaDayMood mood)
        {
            return HanLocalization.Get($"catalog.mood.{mood}.title");
        }

        // ── MONSTER CLASSIFICATION ────────────────────────────────────────────
        public static readonly MonsterType[] AllMonsterTypes =
        {
            MonsterType.Vampir,
            MonsterType.Strigoi,
            MonsterType.Capcaun,
            MonsterType.Varcolac
        };

        public static string GetMonsterTypeLabel(MonsterType type)
        {
            return HanLocalization.Get($"catalog.monster.{type}");
        }

        // ── RECEPTION INTERACTIONS ────────────────────────────────────────────
        public static readonly TravelerArchetype[] AllTravelerArchetypes =
        {
            TravelerArchetype.Drumet,
            TravelerArchetype.Negustor,
            TravelerArchetype.Taran,
            TravelerArchetype.Cersetor,
            TravelerArchetype.Nobil,
            TravelerArchetype.Lautar
        };

        public static string GetArchetypeVocative(TravelerArchetype archetype)
        {
            return HanLocalization.Get($"catalog.arch.{archetype}.voc");
        }

        public static string GetArchetypeReason(TravelerArchetype archetype)
        {
            return HanLocalization.Get($"catalog.arch.{archetype}.reason");
        }

        public static string BuildEsmeraldaGreeting(TravelerArchetype archetype)
        {
            string vocative = GetArchetypeVocative(archetype);
            return HanLocalization.Format("catalog.esmeralda.greeting", vocative);
        }

        public static string BuildGuestIntroduction(string guestName, TravelerArchetype archetype)
        {
            string safeName = string.IsNullOrWhiteSpace(guestName)
                ? HanLocalization.Get("catalog.guest.unnamed")
                : guestName.Trim();
            string reason = GetArchetypeReason(archetype);
            return HanLocalization.Format("catalog.guest.intro", safeName, reason);
        }

        public static string EsmeraldaTransitionToQuestionsLine =>
            HanLocalization.Get("catalog.esmeralda.transition");

        // ── VAMPIRE / HUMAN & OGRE / HUMAN INVESTIGATION SETS ─────────────────
        public static InvestigationPromptSet VampirOmNeckQuestion => new InvestigationPromptSet(
            HanLocalization.Get("catalog.inv.0.q"),
            HanLocalization.Get("catalog.inv.0.a.0"),
            HanLocalization.Get("catalog.inv.0.a.1"),
            HanLocalization.Get("catalog.inv.0.a.2")
        );

        public static InvestigationPromptSet VampirOmThresholdQuestion => new InvestigationPromptSet(
            HanLocalization.Get("catalog.inv.1.q"),
            HanLocalization.Get("catalog.inv.1.a.0"),
            HanLocalization.Get("catalog.inv.1.a.1")
        );

        public static InvestigationPromptSet CapcaunOmHeadQuestion => new InvestigationPromptSet(
            HanLocalization.Get("catalog.inv.2.q"),
            HanLocalization.Get("catalog.inv.2.a.0")
        );

        public static InvestigationPromptSet[] AllInvestigationSets => new[]
        {
            VampirOmNeckQuestion,
            VampirOmThresholdQuestion,
            CapcaunOmHeadQuestion
        };

        public static InvestigationPromptSet GetInvestigationSetByIndex(int index)
        {
            InvestigationPromptSet[] sets = AllInvestigationSets;
            if (index < 0 || index >= sets.Length) return sets[0];
            return sets[index];
        }

        // ── DECISIONS & REACTIONS ─────────────────────────────────────────────
        public static string EsmeraldaRejectLine => HanLocalization.Get("catalog.esmeralda.reject");
        public static string EsmeraldaAcceptLine => HanLocalization.Get("catalog.esmeralda.accept");

        public static string[] GuestRejectReactions => new[]
        {
            HanLocalization.Get("catalog.react.reject.0"),
            HanLocalization.Get("catalog.react.reject.1")
        };

        public static string[] GuestAcceptReactions => new[]
        {
            HanLocalization.Get("catalog.react.accept.0"),
            HanLocalization.Get("catalog.react.accept.1")
        };

        public static string PickRandom(string[] pool)
        {
            if (pool == null || pool.Length == 0) return "";
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }
    }
}
