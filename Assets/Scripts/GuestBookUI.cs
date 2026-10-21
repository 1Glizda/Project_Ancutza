using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Han.CheckIn
{
    /// <summary>
    /// Binds dynamic guest registry data to the visual ledger page.
    /// Supports multi-guest pagination, fallback visuals, and status indicators.
    /// </summary>
    public class GuestBookUI : MonoBehaviour
    {
        [Header("Text Bindings (TextMeshPro)")]
        [SerializeField] private TMP_Text guestNameText;
        [SerializeField] private TMP_Text roomNumberText;
        [SerializeField] private TMP_Text arrivalTimeText;
        [SerializeField] private TMP_Text notesText;
        [SerializeField] private TMP_Text pageCounterText;

        [Header("Image / 2D Sprite Bindings")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private SpriteRenderer portraitRenderer;
        [SerializeField] private Sprite defaultSilhouetteSprite;
        [SerializeField] private Image horseIcon;
        [SerializeField] private SpriteRenderer horseIconRenderer;
        [SerializeField] private GameObject horseStabledBadge;
        [SerializeField] private GameObject checkedInStamp;

        [Header("Visual Feedback Colors")]
        [SerializeField] private Color horsePresentColor = new Color(0.95f, 0.85f, 0.45f, 1f); // Warm parchment gold
        [SerializeField] private Color horseAbsentColor = new Color(0.35f, 0.35f, 0.35f, 0.3f);  // Faded grey ink

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip pageTurnSound;

        [Header("Sample / Test Data")]
        [SerializeField] private List<GuestData> guestRegistry = new List<GuestData>();
        [SerializeField] private int currentPageIndex = 0;

        public int CurrentPageIndex => currentPageIndex;
        public int TotalGuests => guestRegistry.Count;

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            EnsureDefaultGuests();
        }

        private void Start()
        {
            EnsureDefaultGuests();

            if (guestRegistry.Count > 0)
            {
                DisplayGuest(currentPageIndex);
            }
            else
            {
                DisplayEmpty();
            }
        }

        /// <summary>
        /// Populates authentic 1600s Transylvanian thriller guests if the registry is empty.
        /// </summary>
        private void EnsureDefaultGuests()
        {
            if (guestRegistry != null && guestRegistry.Count > 0) return;

            guestRegistry = new List<GuestData>
            {
                new GuestData
                {
                    guestName = "Florin din Vatra",
                    roomNumber = 3,
                    arrivalTime = "Sunset",
                    hasHorse = true,
                    isCheckedIn = true,
                    notes = "Local merchant. Arrived with a heavy wooden carriage from Moldavia carrying barrels of coarse salt. Insists his mare stays locked in the east stall. Smells faintly of damp earth and incense."
                },
                new GuestData
                {
                    guestName = "Gheorghe Străinul",
                    roomNumber = 6,
                    arrivalTime = "Dusk",
                    hasHorse = false,
                    isCheckedIn = true,
                    notes = "Traveled alone on foot through the dark pine forest. Completely unbothered by the wolves howling outside. Avoids the polished copper mirror in the common room. Carries a rusted silver medallion."
                },
                new GuestData
                {
                    guestName = "Doamna Ilinca",
                    roomNumber = 2,
                    arrivalTime = "Midnight",
                    hasHorse = true,
                    isCheckedIn = false,
                    notes = "Arrived just before midnight coach departed. Refuses to lift her thick black mourning veil. Requested salt and dried rowan branches placed on the chamber threshold before locking the door."
                }
            };
        }

        /// <summary>
        /// Supply a full guest list from quest manager or game director.
        /// </summary>
        public void LoadGuestRegistry(List<GuestData> newGuests, int initialIndex = 0)
        {
            guestRegistry = newGuests ?? new List<GuestData>();
            currentPageIndex = Mathf.Clamp(initialIndex, 0, Mathf.Max(0, guestRegistry.Count - 1));

            if (guestRegistry.Count > 0)
                DisplayGuest(currentPageIndex);
            else
                DisplayEmpty();
        }

        /// <summary>
        /// Populates the ledger with data from a specific guest entry.
        /// </summary>
        public void DisplayGuest(int index)
        {
            if (guestRegistry == null || guestRegistry.Count == 0)
            {
                DisplayEmpty();
                return;
            }

            currentPageIndex = Mathf.Clamp(index, 0, guestRegistry.Count - 1);
            GuestData data = guestRegistry[currentPageIndex];

            // 1. Text Details
            if (guestNameText != null)
                guestNameText.text = string.IsNullOrEmpty(data.guestName) ? "Unknown Traveler" : data.guestName;

            if (roomNumberText != null)
                roomNumberText.text = data.roomNumber > 0 ? $"Odaia #{data.roomNumber}" : "Fără odaie";

            if (arrivalTimeText != null)
                arrivalTimeText.text = $"Sosire: {data.arrivalTime}";

            if (notesText != null)
                notesText.text = string.IsNullOrEmpty(data.notes) ? "Nicio însemnare în registru." : data.notes;

            if (pageCounterText != null)
                pageCounterText.text = $"Înregistrarea {currentPageIndex + 1} din {guestRegistry.Count}";

            // 2. Portrait Mugshot
            Sprite targetPortrait = data.portrait != null ? data.portrait : defaultSilhouetteSprite;
            if (portraitRenderer != null)
            {
                portraitRenderer.sprite = targetPortrait;
                portraitRenderer.color = Color.white;
            }
            if (portraitImage != null)
            {
                portraitImage.sprite = targetPortrait;
                portraitImage.color = targetPortrait != null ? Color.white : new Color(0.12f, 0.08f, 0.05f, 0.9f);
            }

            // 3. Horse Stable Status
            if (horseStabledBadge != null)
                horseStabledBadge.SetActive(data.hasHorse);

            if (horseIconRenderer != null)
                horseIconRenderer.color = data.hasHorse ? horsePresentColor : horseAbsentColor;

            if (horseIcon != null)
                horseIcon.color = data.hasHorse ? horsePresentColor : horseAbsentColor;

            // 4. Checked In Status Stamp
            if (checkedInStamp != null)
                checkedInStamp.SetActive(data.isCheckedIn);
        }

        /// <summary>
        /// Fallback visual when ledger has no registered guests.
        /// </summary>
        public void DisplayEmpty()
        {
            if (guestNameText != null) guestNameText.text = "No Guest Registered";
            if (roomNumberText != null) roomNumberText.text = "-";
            if (arrivalTimeText != null) arrivalTimeText.text = "-";
            if (notesText != null) notesText.text = "The inn registry is currently empty.";
            if (pageCounterText != null) pageCounterText.text = "0 / 0";
            if (horseStabledBadge != null) horseStabledBadge.SetActive(false);
            if (checkedInStamp != null) checkedInStamp.SetActive(false);
            if (portraitImage != null) portraitImage.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            if (portraitRenderer != null) portraitRenderer.sprite = defaultSilhouetteSprite;
            if (horseIconRenderer != null) horseIconRenderer.color = horseAbsentColor;
        }

        /// <summary>
        /// Advances to the next guest record.
        /// </summary>
        public void NextPage()
        {
            if (guestRegistry.Count <= 1 || currentPageIndex >= guestRegistry.Count - 1)
                return;

            currentPageIndex++;
            DisplayGuest(currentPageIndex);
            PlayPageSound();
        }

        /// <summary>
        /// Returns to the previous guest record.
        /// </summary>
        public void PreviousPage()
        {
            if (guestRegistry.Count <= 1 || currentPageIndex <= 0)
                return;

            currentPageIndex--;
            DisplayGuest(currentPageIndex);
            PlayPageSound();
        }

        private void PlayPageSound()
        {
            if (audioSource != null && pageTurnSound != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.08f);
                audioSource.PlayOneShot(pageTurnSound);
            }
        }
    }
}
