using System;
using UnityEngine;

namespace Han.CheckIn
{
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

        [Header("Lodging Details")]
        [Tooltip("Assigned room number in the Han (e.g. 1 - 8)")]
        public int roomNumber = 1;

        [Tooltip("Estimated arrival time / stage (e.g., 'Sunset', 'Dusk', 'Midnight')")]
        public string arrivalTime = "Dusk";

        [Header("Investigation Clues & Status")]
        [Tooltip("Does the traveler have a horse stabled in the courtyard barn?")]
        public bool hasHorse = false;

        [Tooltip("Has the innkeeper verified this guest's documents / key given")]
        public bool isCheckedIn = false;

        [TextArea(3, 8)]
        [Tooltip("Behavioral notes, physical oddities, luggage contents, rumors")]
        public string notes = "Wears heavy fur cloak. Pale complexion. Avoids mirror in the tavern hall.";

        [Tooltip("Optional custom stamp or signature icon")]
        public Sprite signatureStamp;
    }
}
