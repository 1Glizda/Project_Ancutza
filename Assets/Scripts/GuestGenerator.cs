using System.Collections.Generic;
using UnityEngine;

namespace Han.CheckIn
{
    /// <summary>
    /// Procedurally creates travelers: name, room, clue notes and a pixel-art body + mugshot.
    /// The mugshot is cropped from the same texture used for the NPC body sprite, so the
    /// face in the ledger always matches the character walking around the Han.
    /// Textures are uncompressed RGBA32 with no mipmaps (WebGL safe, ~8 KB each).
    /// </summary>
    public static class GuestGenerator
    {
        private static readonly string[] FirstNames =
        {
            "Florin", "Gheorghe", "Vasile", "Ion", "Mihai", "Costin", "Radu", "Matei", "Stefan", "Dragos",
            "Bogdan", "Andrei", "Lupu", "Nicolae", "Teodor", "Ilinca", "Maria", "Elena", "Ana", "Ioana",
            "Saveta", "Catinca", "Zamfira", "Ruxandra", "Magda", "Stanca", "Voica", "Doamna Smaranda"
        };

        private static readonly string[] LastNames =
        {
            "din Vatra", "Străinul", "Bălan", "Moldovan", "Munteanu", "Pătrașcu", "Corbu", "Negru",
            "Vulpe", "Zăpadă", "Ursu", "Lăcătuș", "din Sibiu", "din Brașov", "Călătorul", "Cojocaru"
        };

        private static readonly string[] ArrivalTimeKeys =
        {
            "arrival.sunset", "arrival.dusk", "arrival.twilight", "arrival.nightfall"
        };

        private const int NeutralNotesCount = 12;
        private const int MonsterTellsCount = 8;
        private const int HumanDecoysCount = 6;

        private static readonly Color32[] SkinTones =
        {
            new Color32(244, 214, 183, 255), new Color32(226, 180, 140, 255), new Color32(198, 146, 106, 255),
            new Color32(160, 110, 76, 255), new Color32(236, 226, 214, 255), new Color32(210, 170, 130, 255)
        };

        private static readonly Color32[] HairColors =
        {
            new Color32(30, 22, 18, 255), new Color32(74, 48, 28, 255), new Color32(120, 84, 44, 255),
            new Color32(176, 150, 96, 255), new Color32(150, 150, 150, 255), new Color32(110, 36, 24, 255)
        };

        private static readonly Color32[] ClothColors =
        {
            new Color32(94, 56, 40, 255), new Color32(60, 74, 58, 255), new Color32(88, 40, 40, 255),
            new Color32(56, 62, 92, 255), new Color32(112, 96, 68, 255), new Color32(70, 70, 76, 255),
            new Color32(120, 80, 30, 255)
        };

        private static readonly Color32[] PantsColors =
        {
            new Color32(44, 36, 30, 255), new Color32(58, 52, 44, 255), new Color32(36, 40, 52, 255)
        };

        /// <summary>Randomly picks one of the four monster classifications.</summary>
        public static MonsterType RollRandomMonsterType()
        {
            MonsterType[] pool = HanDialogueCatalog.AllMonsterTypes;
            return pool[Random.Range(0, pool.Length)];
        }

        /// <summary>Creates a fully populated guest (not yet checked in).</summary>
        public static GuestData Create(
            GuestRole role,
            int night,
            HashSet<string> usedNames,
            HashSet<int> usedRooms,
            int maxRooms,
            MonsterType? forcedMonsterType = null)
        {
            var g = new GuestData();
            g.role = role;
            g.monsterType = role == GuestRole.Monster
                ? (forcedMonsterType.HasValue && forcedMonsterType.Value != MonsterType.None
                    ? forcedMonsterType.Value
                    : RollRandomMonsterType())
                : MonsterType.None;

            TravelerArchetype[] archetypes = HanDialogueCatalog.AllTravelerArchetypes;
            g.archetype = archetypes[Random.Range(0, archetypes.Length)];

            g.arrivedOnNight = night;
            g.guestName = UniqueName(usedNames, out bool isFallbackName, out int fallbackNumber);
            g.usesGeneratedFallbackName = isFallbackName;
            g.generatedFallbackNumber = fallbackNumber;
            g.roomNumber = FreeRoom(usedRooms, maxRooms);
            g.arrivalTimeKey = ArrivalTimeKeys[Random.Range(0, ArrivalTimeKeys.Length)];
            g.hasHorse = Random.value < 0.45f;
            g.isAlive = true;
            g.isCheckedIn = false;

            // Generate personality
            if (role == GuestRole.Monster)
            {
                // Monsters either act unnervingly calm/eerie or mimic pushy/shy
                float mRoll = Random.value;
                g.personality = mRoll < 0.65f ? PersonalityType.MonsterUncanny : (mRoll < 0.85f ? PersonalityType.Pushy : PersonalityType.Shy);
            }
            else
            {
                PersonalityType[] humanTypes = { PersonalityType.Shy, PersonalityType.Pushy, PersonalityType.Jovial, PersonalityType.Stern, PersonalityType.Nervous };
                g.personality = humanTypes[Random.Range(0, humanTypes.Length)];
            }

            // Pick indices once so language changes re-translate existing travelers consistently
            InvestigationPromptSet[] investigationPool = HanDialogueCatalog.AllInvestigationSets;
            g.investigationSetIndex = Random.Range(0, investigationPool.Length);
            InvestigationPromptSet pickedSet = investigationPool[g.investigationSetIndex];
            g.investigationAnswerIndex = pickedSet.answers.Length > 0 ? Random.Range(0, pickedSet.answers.Length) : 0;
            g.acceptReactionIndex = Random.Range(0, HanDialogueCatalog.GuestAcceptReactions.Length);
            g.rejectReactionIndex = Random.Range(0, HanDialogueCatalog.GuestRejectReactions.Length);
            g.roadAnswerIndex = Random.Range(0, HanDialogueCatalog.VampirOmNeckQuestion.answers.Length);
            g.identityAnswerIndex = Random.Range(0, HanDialogueCatalog.VampirOmThresholdQuestion.answers.Length);

            float roll = Random.value;
            g.staysNights = roll < 0.4f ? 1 : (roll < 0.75f ? 2 : 3);

            g.neutralNoteKey = $"guest.note.neutral.{Random.Range(0, NeutralNotesCount)}";
            if (role == GuestRole.Monster && Random.value < 0.75f)
                g.clueNoteKey = $"guest.note.monster.{Random.Range(0, MonsterTellsCount)}";
            else if (role == GuestRole.Human && Random.value < 0.35f)
                g.clueNoteKey = $"guest.note.decoy.{Random.Range(0, HumanDecoysCount)}";

            ApplyLocalization(g);

            BuildSprites(g);
            return g;
        }

        /// <summary>
        /// Populates or refreshes all localized strings on a GuestData instance from its stored keys/indices.
        /// Safe to call at runtime whenever the player switches language.
        /// </summary>
        public static void ApplyLocalization(GuestData g)
        {
            if (g == null) return;

            if (g.defaultSampleId > 0)
            {
                if (!string.IsNullOrEmpty(g.arrivalTimeKey))
                    g.arrivalTime = HanLocalization.Get(g.arrivalTimeKey);
                string sampleNotes = HanLocalization.Get($"sample.guest.{g.defaultSampleId}.notes");
                g.notes = AppendDynamicNoteSuffixes(g, sampleNotes);
                return;
            }

            if (g.usesGeneratedFallbackName)
                g.guestName = HanLocalization.Get("guest.fallback_prefix") + g.generatedFallbackNumber;

            if (!string.IsNullOrEmpty(g.arrivalTimeKey))
                g.arrivalTime = HanLocalization.Get(g.arrivalTimeKey);

            if (!string.IsNullOrEmpty(g.neutralNoteKey))
            {
                string baseNote = HanLocalization.Get(g.neutralNoteKey);
                if (!string.IsNullOrEmpty(g.clueNoteKey))
                    baseNote += " " + HanLocalization.Get(g.clueNoteKey);
                g.notes = AppendDynamicNoteSuffixes(g, baseNote);
            }

            if (g.investigationSetIndex >= 0)
            {
                InvestigationPromptSet pickedInvestigation = HanDialogueCatalog.GetInvestigationSetByIndex(g.investigationSetIndex);
                string pickedAnswer = pickedInvestigation.answers.Length > 0
                    ? pickedInvestigation.answers[Mathf.Clamp(g.investigationAnswerIndex, 0, pickedInvestigation.answers.Length - 1)]
                    : string.Empty;

                var steps = new[]
                {
                    new CheckInDialogueStep(true, HanDialogueCatalog.BuildEsmeraldaGreeting(g.archetype)),
                    new CheckInDialogueStep(false, HanDialogueCatalog.BuildGuestIntroduction(g.guestName, g.archetype)),
                    new CheckInDialogueStep(true, HanDialogueCatalog.EsmeraldaTransitionToQuestionsLine),
                    new CheckInDialogueStep(true, pickedInvestigation.question),
                    new CheckInDialogueStep(false, pickedAnswer)
                };

                g.checkInDialogueSteps = steps;
                g.checkInSmallTalk = new string[steps.Length];
                for (int i = 0; i < steps.Length; i++)
                    g.checkInSmallTalk[i] = steps[i].line;
            }

            string[] acceptPool = HanDialogueCatalog.GuestAcceptReactions;
            if (g.acceptReactionIndex >= 0 && acceptPool.Length > 0)
                g.checkInApprovedLine = acceptPool[Mathf.Clamp(g.acceptReactionIndex, 0, acceptPool.Length - 1)];

            string[] rejectPool = HanDialogueCatalog.GuestRejectReactions;
            if (g.rejectReactionIndex >= 0 && rejectPool.Length > 0)
                g.checkInRejectedLine = rejectPool[Mathf.Clamp(g.rejectReactionIndex, 0, rejectPool.Length - 1)];

            g.responsePurpose = HanDialogueCatalog.BuildGuestIntroduction(g.guestName, g.archetype);
            g.responseHorse = HanLocalization.Get(g.hasHorse ? "guest.response.horse_yes" : "guest.response.horse_no");

            string[] roadPool = HanDialogueCatalog.VampirOmNeckQuestion.answers;
            if (roadPool.Length > 0)
                g.responseRoad = roadPool[Mathf.Clamp(g.roadAnswerIndex, 0, roadPool.Length - 1)];

            string[] idPool = HanDialogueCatalog.VampirOmThresholdQuestion.answers;
            if (idPool.Length > 0)
                g.responseIdentity = idPool[Mathf.Clamp(g.identityAnswerIndex, 0, idPool.Length - 1)];
        }

        private static string AppendDynamicNoteSuffixes(GuestData g, string coreNote)
        {
            string result = coreNote ?? string.Empty;

            if (!string.IsNullOrEmpty(g.horseNoteKey))
            {
                result += g.horseNoteKey == "guest.note.horse_stall"
                    ? HanLocalization.Format("guest.note.horse_stall", g.horseStallIndex + 1)
                    : HanLocalization.Get(g.horseNoteKey);
            }

            if (g.isDeceasedInBook)
            {
                string prefix = g.deceasedRoomNumber > 0
                    ? HanLocalization.Format("guest.note.deceased_room", g.deceasedRoomNumber)
                    : HanLocalization.Get("guest.note.deceased_generic");
                result = prefix + result;
            }

            return result;
        }

        private static string UniqueName(HashSet<string> used, out bool isFallback, out int fallbackNumber)
        {
            for (int i = 0; i < 40; i++)
            {
                string n = FirstNames[Random.Range(0, FirstNames.Length)] + " " + LastNames[Random.Range(0, LastNames.Length)];
                if (used.Add(n))
                {
                    isFallback = false;
                    fallbackNumber = 0;
                    return n;
                }
            }
            fallbackNumber = Random.Range(100, 999);
            string fallback = HanLocalization.Get("guest.fallback_prefix") + fallbackNumber;
            used.Add(fallback);
            isFallback = true;
            return fallback;
        }

        private static int FreeRoom(HashSet<int> usedRooms, int maxRooms)
        {
            var free = new List<int>();
            for (int r = 1; r <= maxRooms; r++)
                if (!usedRooms.Contains(r)) free.Add(r);
            int room = free.Count > 0 ? free[Random.Range(0, free.Count)] : Random.Range(1, maxRooms + 1);
            usedRooms.Add(room);
            return room;
        }

        // ------------------------------------------------------------------
        //  Pixel art
        // ------------------------------------------------------------------
        private const int W = 32;
        private const int H = 64;

        private static void Fill(Color32[] px, int x0, int y0, int w, int h, Color32 c)
        {
            for (int y = y0; y < y0 + h && y < H; y++)
                for (int x = x0; x < x0 + w && x < W; x++)
                    if (x >= 0 && y >= 0) px[y * W + x] = c;
        }

        private static void Clear(Color32[] px, int x, int y)
        {
            px[y * W + x] = new Color32(0, 0, 0, 0);
        }

        private static void BuildSprites(GuestData g)
        {
            var px = new Color32[W * H];
            Color32 skin = SkinTones[Random.Range(0, SkinTones.Length)];
            Color32 hair = HairColors[Random.Range(0, HairColors.Length)];
            Color32 cloth = ClothColors[Random.Range(0, ClothColors.Length)];
            Color32 pants = PantsColors[Random.Range(0, PantsColors.Length)];
            Color32 dark = new Color32(24, 18, 16, 255);
            Color32 mouth = new Color32((byte)(skin.r * 0.65f), (byte)(skin.g * 0.5f), (byte)(skin.b * 0.5f), 255);
            Color32 belt = new Color32(40, 28, 20, 255);

            // legs + boots
            Fill(px, 10, 0, 5, 16, pants);
            Fill(px, 17, 0, 5, 16, pants);
            Fill(px, 9, 0, 6, 3, dark);
            Fill(px, 17, 0, 6, 3, dark);
            // torso, arms, hands
            Fill(px, 7, 16, 18, 24, cloth);
            Fill(px, 7, 24, 18, 2, belt);
            Fill(px, 4, 18, 3, 18, cloth);
            Fill(px, 25, 18, 3, 18, cloth);
            Fill(px, 4, 15, 3, 3, skin);
            Fill(px, 25, 15, 3, 3, skin);
            // neck + head
            Fill(px, 14, 38, 4, 3, skin);
            Fill(px, 10, 40, 12, 16, skin);
            Clear(px, 10, 40); Clear(px, 21, 40);
            // hair (random style)
            Fill(px, 9, 52, 14, 5, hair);
            if (Random.value < 0.5f)
            {
                Fill(px, 9, 42, 2, 12, hair);
                Fill(px, 21, 42, 2, 12, hair);
            }
            else
            {
                Fill(px, 9, 50, 2, 4, hair);
                Fill(px, 21, 50, 2, 4, hair);
            }
            // hat (optional)
            if (Random.value < 0.35f)
            {
                Color32 hat = new Color32(50, 40, 34, 255);
                Fill(px, 8, 56, 16, 2, hat);
                Fill(px, 11, 58, 10, 4, hat);
            }
            // beard (optional)
            if (Random.value < 0.35f) Fill(px, 11, 40, 10, 3, hair);
            // face
            Fill(px, 13, 48, 2, 2, dark);
            Fill(px, 18, 48, 2, 2, dark);
            Fill(px, 12, 51, 3, 1, hair);
            Fill(px, 18, 51, 3, 1, hair);
            Fill(px, 15, 44, 3, 1, mouth);

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.name = "Guest_" + g.guestName;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, !Application.isEditor);

            g.spriteSheet = tex;
            g.bodySprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 32f);
            g.bodySprite.name = tex.name + "_Body";
            g.portrait = Sprite.Create(tex, new Rect(0, 24, W, H - 24), new Vector2(0.5f, 0.5f), 32f);
            g.portrait.name = tex.name + "_Mugshot";
        }

        /// <summary>Frees textures and sprites created for a guest.</summary>
        public static void Release(GuestData g)
        {
            if (g == null) return;
            if (g.portrait != null)
            {
                if (g.portrait.texture != null && g.portrait.texture != g.spriteSheet)
                {
                    Object.Destroy(g.portrait.texture);
                }
                Object.Destroy(g.portrait);
            }
            if (g.bodySprite != null) Object.Destroy(g.bodySprite);
            if (g.spriteSheet != null) Object.Destroy(g.spriteSheet);
            g.portrait = null;
            g.bodySprite = null;
            g.spriteSheet = null;
        }
    }
}
