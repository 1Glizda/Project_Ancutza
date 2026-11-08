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

        [Header("Night Loop (hidden from the ledger)")]
        [Tooltip("Secret role. Only the director and debugging should read this.")]
        public GuestRole role = GuestRole.Human;

        [Tooltip("How many nights this guest intends to stay (1-3).")]
        public int staysNights = 1;

        [Tooltip("Nights already spent in the Han.")]
        public int nightsStayed = 0;

        [Tooltip("Night number on which the guest checked in.")]
        public int arrivedOnNight = 1;

        [Tooltip("False once murdered by a monster.")]
        public bool isAlive = true;

        // ---- Runtime-only references (never serialized) ----
        [NonSerialized] public NPCController npc;
        [NonSerialized] public Texture2D spriteSheet;
        [NonSerialized] public Sprite bodySprite;
    }
}
