using System;
using UnityEngine;

namespace Han.CheckIn
{
    /// <summary>Hidden identity of a traveler. Never printed on the ledger.</summary>
    public enum GuestRole
    {
        Human,
        Monster
    }

    /// <summary>Folklore subtype classification for monsters (Humans stay None).</summary>
    public enum MonsterType
    {
        None,
        Vampir,
        Strigoi,
        Capcaun,
        Varcolac
    }

    /// <summary>Single check-in exchange line with explicit speaker attribution (Esmeralda vs Traveler).</summary>
    [Serializable]
    public struct CheckInDialogueStep
    {
        public bool isEsmeralda;
        [TextArea] public string line;

        public CheckInDialogueStep(bool isEsmeralda, string line)
        {
            this.isEsmeralda = isEsmeralda;
            this.line = line;
        }
    }

    /// <summary>Traveler social archetype used for greeting and check-in dialogue.</summary>
    public enum TravelerArchetype
    {
        Drumet,
        Negustor,
        Taran,
        Cersetor,
        Nobil,
        Lautar
    }

    /// <summary>Affects greeting, small talk, pacing, and tone at check-in.</summary>
    public enum PersonalityType
    {
        Shy,
        Pushy,
        Jovial,
        Stern,
        Nervous,
        MonsterUncanny
    }

    /// <summary>
    /// Represents the dynamic registry record for a guest at the Transylvanian Han.
    /// Used for thriller clues, identity verification, and room allocation.
    /// </summary>
    [System.Serializable]
    public class GuestData
    {
        [Header("Identity")]
        [Tooltip("Full name of the traveler / guest")]
        public string guestName = "Unknown Traveler";

        [Tooltip("Pixel-art / retro mugshot portrait of the guest")]
        public Sprite portrait;

        [Header("Personality & Small Talk (Papers, Please feel)")]
        public PersonalityType personality = PersonalityType.Jovial;
        [Tooltip("Lines spoken in sequence at the desk while the form is being typed live.")]
        public string[] checkInSmallTalk;
        [Tooltip("Spoken when the player stamps and approves the check-in.")]
        public string checkInApprovedLine = "Thank you, innkeeper.";

        [Header("Desk Interrogation Responses")]
        public string responsePurpose;
        public string responseHorse;
        public string responseRoad;
        public string responseIdentity;

        [Header("Lodging Details")]
        [Tooltip("Assigned room number in the Han (e.g. 1 - 8)")]
        public int roomNumber = 1;

        [Tooltip("Estimated arrival time / stage (e.g., 'Sunset', 'Dusk', 'Midnight')")]
        public string arrivalTime = "Dusk";

        [Header("Investigation Clues & Status")]
        [Tooltip("Does the traveler have a horse stabled in the courtyard barn?")]
        public bool hasHorse = false;
        public int horseStallIndex = -1;

        [Tooltip("Has the innkeeper verified this guest's documents / key given")]
        public bool isCheckedIn = false;

        [TextArea(3, 8)]
        [Tooltip("Behavioral notes, physical oddities, luggage contents, rumors")]
        public string notes = "Wears heavy fur cloak. Pale complexion. Avoids mirror in the tavern hall.";

        [Tooltip("Optional custom stamp or signature icon")]
        public Sprite signatureStamp;

        [Tooltip("Traveler archetype reflected in Esmeralda's Romanian check-in greeting.")]
        public TravelerArchetype archetype = TravelerArchetype.Drumet;

        [Tooltip("Guest's reaction line if Esmeralda turns them away from the inn.")]
        [TextArea] public string checkInRejectedLine;

        [Tooltip("Structured check-in conversation beats with speaker attribution.")]
        public CheckInDialogueStep[] checkInDialogueSteps;

        [Header("Night Loop (hidden from the ledger)")]
        [Tooltip("Secret role. Only the director and debugging should read this.")]
        public GuestRole role = GuestRole.Human;

        [Tooltip("Secret monster classification (Vampir, Strigoi, Capcaun, Varcolac). Humans remain None.")]
        public MonsterType monsterType = MonsterType.None;

        [Tooltip("How many nights this guest intends to stay (1-3).")]
        public int staysNights = 1;

        [Tooltip("Nights already spent in the Han.")]
        public int nightsStayed = 0;

        [Tooltip("Night number on which the guest checked in.")]
        public int arrivedOnNight = 1;

        [Tooltip("False once murdered by a monster.")]
        public bool isAlive = true;

        // ---- Localization keys so runtime language switches update existing guests live ----
        [HideInInspector] public int defaultSampleId = 0;
        [HideInInspector] public string arrivalTimeKey;
        [HideInInspector] public string neutralNoteKey;
        [HideInInspector] public string clueNoteKey;
        [HideInInspector] public string horseNoteKey;
        [HideInInspector] public bool isDeceasedInBook;
        [HideInInspector] public int deceasedRoomNumber;
        [HideInInspector] public int investigationSetIndex = -1;
        [HideInInspector] public int investigationAnswerIndex = -1;
        [HideInInspector] public int acceptReactionIndex = -1;
        [HideInInspector] public int rejectReactionIndex = -1;
        [HideInInspector] public int roadAnswerIndex = -1;
        [HideInInspector] public int identityAnswerIndex = -1;
        [HideInInspector] public bool usesGeneratedFallbackName;
        [HideInInspector] public int generatedFallbackNumber;

        // ---- Runtime-only references (never serialized) ----
        [NonSerialized] public NPCController npc;
        [NonSerialized] public Texture2D spriteSheet;
        [NonSerialized] public Sprite bodySprite;
    }
}
