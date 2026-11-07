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

        private static readonly string[] ArrivalTimes = { "Sunset", "Dusk", "Twilight", "Nightfall" };

        private static readonly string[] NeutralNotes =
        {
            "Merchant travelling with barrels of coarse salt. Pays in old silver coin.",
            "Pilgrim on the road to a distant monastery. Speaks softly, prays before eating.",
            "Wool trader from Moldavia. Complains loudly about the price of the room.",
            "Young scribe carrying sealed letters. Refuses to say who they are for.",
            "Widow in mourning black. Asked for a room far from the stairs.",
            "Blacksmith's apprentice, tired and dusty. Asked for hot broth and a bench.",
            "Hunter with a worn crossbow. Says the woods are unusually quiet.",
            "Travelling healer with jars of dried herbs. Offers remedies to the staff.",
            "Soldier returning from the border. Sleeps with boots on and a knife at hand.",
            "Cheese seller with a creaking cart. Insisted the horse sleeps close to the door.",
            "Minstrel with a cracked fiddle. Hums the same sad tune all evening.",
            "Quiet farmer who counts the exits of every room he enters."
        };

        private static readonly string[] MonsterTells =
        {
            "Avoids the polished copper mirror in the common room.",
            "Did not touch the garlic bread. Smelled it and turned pale.",
            "Hands are ice cold. Asked to keep the shutters closed all night.",
            "Eyes the stable horses and the horses will not stop trembling.",
            "Refused to cross the threshold until the innkeeper said 'enter'.",
            "Left no footprints in the muddy courtyard, the stable boy swears.",
            "Wolves howled outside the moment this guest arrived.",
            "Did not eat or drink anything, and asked for no candle in the room."
        };

        private static readonly string[] HumanDecoys =
        {
            "Startled by the church bell and spilled their cup.",
            "Wears a rusted silver medallion. Says it was a gift from a late mother.",
            "Asked for the shutters to be closed, afraid of the draught.",
            "Has a harsh cough and dislikes bright candles.",
            "Skips meals out of a long fast, not out of habit.",
            "Avoids mirrors out of old superstition, or so they say."
        };

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

        /// <summary>Creates a fully populated guest (not yet checked in).</summary>
        public static GuestData Create(GuestRole role, int night, HashSet<string> usedNames, HashSet<int> usedRooms, int maxRooms)
        {
            var g = new GuestData();
            g.role = role;
            g.arrivedOnNight = night;
            g.guestName = UniqueName(usedNames);
            g.roomNumber = FreeRoom(usedRooms, maxRooms);
            g.arrivalTime = ArrivalTimes[Random.Range(0, ArrivalTimes.Length)];
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

            BuildSmallTalk(g);
            BuildQuestionResponses(g);

            float roll = Random.value;
            g.staysNights = roll < 0.4f ? 1 : (roll < 0.75f ? 2 : 3);

            string note = NeutralNotes[Random.Range(0, NeutralNotes.Length)];
            if (role == GuestRole.Monster && Random.value < 0.75f)
                note += " " + MonsterTells[Random.Range(0, MonsterTells.Length)];
            else if (role == GuestRole.Human && Random.value < 0.35f)
                note += " " + HumanDecoys[Random.Range(0, HumanDecoys.Length)];
            g.notes = note;

            BuildSprites(g);
            return g;
        }

        private static void BuildSmallTalk(GuestData g)
        {
            switch (g.personality)
            {
                case PersonalityType.Shy:
                    g.checkInSmallTalk = new[]
                    {
                        "U-um... good evening, sir. If you have a small bed left? Just somewhere quiet... if possible.",
                        g.hasHorse ? "I tied my tired mare outside by the post... she had a hard trek through the mountain pass."
                                   : "I came on foot all the way from the valley... my boots are soaked through.",
                        "I won't be any trouble, innkeeper... I'll keep to my corner."
                    };
                    g.checkInApprovedLine = "T-thank you... may the saints bless your warm hearth.";
                    break;

                case PersonalityType.Pushy:
                    g.checkInSmallTalk = new[]
                    {
                        "Hurry it up, innkeeper! My cloak is soaked with snow and mud, and my silver coin is good.",
                        g.hasHorse ? "I brought my prized horse too—make sure your stable has fresh hay and clean oats!"
                                   : "My carriage broke an axle miles back, so don't ask! Just give me a warm bed.",
                        "Best room you have, right now! Stop staring and write down my name!"
                    };
                    g.checkInApprovedLine = "Finally! Keep the hearth burning, I expect hot broth at sunrise!";
                    break;

                case PersonalityType.Jovial:
                    g.checkInSmallTalk = new[]
                    {
                        "Ah! Greetings, good master of the house! What a magnificent fire you keep burning tonight!",
                        g.hasHorse ? "My sturdy mare is hitched at the stall; she loves oats almost as much as I love spiced ale!"
                                   : "Walked all the way from Sibiu, and my knees are singing for a soft mattress!",
                        "Tell me, innkeeper, have you any of that smoked Transylvanian cheese left?"
                    };
                    g.checkInApprovedLine = "Much obliged, friend! Pour me a tankard later by the fire!";
                    break;

                case PersonalityType.Stern:
                    g.checkInSmallTalk = new[]
                    {
                        "Good evening. I require lodging for the night. Single occupancy.",
                        g.hasHorse ? "My mount is hitched at the stall outside. Stable it properly."
                                   : "Travelling light on foot. No beast to tend to.",
                        "I value silence and privacy above all else. Here are my credentials."
                    };
                    g.checkInApprovedLine = "Understood. Good night, innkeeper.";
                    break;

                case PersonalityType.Nervous:
                    g.checkInSmallTalk = new[]
                    {
                        "I... I need a room immediately. Please lock the courtyard gate behind me.",
                        g.hasHorse ? "My horse went wild near the pine trees... it smelled something foul in the wind."
                                   : "I ran the last mile. Shadows... unnatural shadows moving among the trees.",
                        "Do your room doors have sturdy iron bolts? Tell me they do."
                    };
                    g.checkInApprovedLine = "The bolts work, yes? God preserve us tonight. Thank you.";
                    break;

                case PersonalityType.MonsterUncanny:
                default:
                    g.checkInSmallTalk = new[]
                    {
                        "Good evening, innkeeper... The woods were so quiet tonight. Almost... breathless.",
                        g.hasHorse ? "My beast... refused to cross your gate. Its heart was beating so rapidly. Fascinating."
                                   : "The darkness carries me swiftly. I have no need for weary beasts.",
                        "Such vibrant warmth in your inn tonight. So many pulses beating under one roof..."
                    };
                    g.checkInApprovedLine = "Sleep soundly, innkeeper. Sleep very soundly...";
                    break;
            }
        }

        private static void BuildQuestionResponses(GuestData g)
        {
            bool isMonster = g.role == GuestRole.Monster;

            switch (g.personality)
            {
                case PersonalityType.Shy:
                    g.responsePurpose = isMonster
                        ? "I... I only travel when the sun is down. The cold night air is gentle on my skin... mortals shouldn't worry about me."
                        : "Just... heading to see my sister in the lower valley before the mountain passes freeze over with snow.";

                    g.responseHorse = g.hasHorse
                        ? "Yes, a gentle mare... she is waiting outside. Please, make sure she gets a warm stall in the barn."
                        : (isMonster ? "No animals... beasts shriek and thrash when I touch them. I move much swifter on my own feet anyway."
                                     : "On foot, sir... walked from dawn. My knees are trembling and my boots are worn through.");

                    g.responseRoad = isMonster
                        ? "The road was peaceful... the dark forest smells like fresh iron. I mean... sweet pine needles."
                        : "I heard heavy thumping near the ravine... I didn't dare look back into the trees. I ran all the way!";

                    g.responseIdentity = isMonster
                        ? "Names... a name is just what mortal prey call each other, isn't it? But yes, write whatever you wish on your ledger..."
                        : "M-my name is written true on your parchment, innkeeper. I am an honest traveler, I swear it.";
                    break;

                case PersonalityType.Pushy:
                    g.responsePurpose = isMonster
                        ? "None of your blasted business! What I do under the dark moon doesn't concern an innkeeper who values their throat!"
                        : "Merchant trade! I have sealed contracts to deliver to the boyars by tomorrow noon! Stop wasting my time!";

                    g.responseHorse = g.hasHorse
                        ? "Of course I brought a horse! A thoroughbred stallion—if he catches a chill in your stables, I'll deduct it from your pay!"
                        : (isMonster ? "I don't need nags to carry me! Beasts are frail, cowardly food... err, cattle!"
                                     : "My carriage snapped an axle on your wretched rocky road! Had to hike the last two leagues on foot!");

                    g.responseRoad = isMonster
                        ? "Nothing out there dared cross my path. The wolves scampered with their tails between their legs when they caught my scent."
                        : "The road is a disgrace! Wolves circling the tree line and shadowy shapes darting between the snowdrifts!";

                    g.responseIdentity = isMonster
                        ? "You dare question me?! Write down whatever name you want, it won't change what happens tonight!"
                        : "I am who I say I am! My purse is full of good silver coin, look for yourself and hand over my key!";
                    break;

                case PersonalityType.Jovial:
                    g.responsePurpose = isMonster
                        ? "Ha! Hunting for merriment! So many tender, lively souls gathered under one roof tonight... truly mouth-watering!"
                        : "Ha! A pitcher of spiced wine and a roaring fire! Singing songs and trading tales from the Danube!";

                    g.responseHorse = g.hasHorse
                        ? "A sturdy chestnut beast! Loves sugar beets and hates mountain wolves. Tuck him into a dry stall, friend!"
                        : (isMonster ? "Ha-ha! Why feed a horse oats when you can outrun the wind on your own two paws... err, boots!"
                                     : "No horse, friend, but two good legs and a cheerful tune! Keeps the frost out of the marrow!");

                    g.responseRoad = isMonster
                        ? "Oh, the road was glorious! Cold shadows dancing under the moon! A perfect night for a feast!"
                        : "Eerie mist near the old crossroad! Felt like a pair of glowing red eyes was watching from the brambles!";

                    g.responseIdentity = isMonster
                        ? "Call me whatever pleases your ear, friend! In the dark, we all sound the same when we laugh!"
                        : "Known from here to Brașov as an honest trader! Put my name in your ledger with pride, innkeeper!";
                    break;

                case PersonalityType.Stern:
                    g.responsePurpose = isMonster
                        ? "I follow the scent of travelers. Inns are such convenient places for the hungry to gather."
                        : "Official courier business. Traveling on urgent decree. I require quiet lodging and no unnecessary interruptions.";

                    g.responseHorse = g.hasHorse
                        ? "Black mare, well-trained. Stabled in the courtyard. Do not let disorderly travelers agitate her."
                        : (isMonster ? "Horses are hysterical creatures. They smell death before it strikes. I move on foot."
                                     : "Traveled on foot with a heavy soldier's pack. A disciplined march builds endurance.");

                    g.responseRoad = isMonster
                        ? "The mountain is silent. The living would do well to lock their doors tight before midnight."
                        : "A predator stalked parallel to the trail for two miles. Large tracks in the fresh snow. Keep your courtyard gates barred.";

                    g.responseIdentity = isMonster
                        ? "My identity is eternal. The ink on your paper will rot away long before I do."
                        : "My papers bear the magistrate's wax seal. Inspect the ledger and confirm my chamber.";
                    break;

                case PersonalityType.Nervous:
                    g.responsePurpose = isMonster
                        ? "Is the front door barred?! Are there silver mirrors here?! Tell me nobody in this inn carries silver!"
                        : "I... I had to leave my village in a hurry. Strange things were happening in the woods... people going missing...";

                    g.responseHorse = g.hasHorse
                        ? "My horse was foaming at the bit with terror... something was tracking us from the ridge above!"
                        : (isMonster ? "Horses kick and bite... they know what I am... they always know..."
                                     : "No horse... I kept looking over my shoulder every step. Every shadow looked like reaching claws!");

                    g.responseRoad = isMonster
                        ? "Blood... so much blood on the snow. But it wasn't mine... none of it was mine..."
                        : "I saw a silhouette standing on top of the ruined chapel... standing completely motionless in the howling wind!";

                    g.responseIdentity = isMonster
                        ? "Don't look at my eyes! Just write the name! Write the name and look away!"
                        : "I have my papers right here, sir! Please don't cast me back out into the night!";
                    break;

                case PersonalityType.MonsterUncanny:
                default:
                    g.responsePurpose = "I was drawn to the warmth of your Han. So many beating hearts inside these wooden walls... irresistible.";
                    g.responseHorse = "No horse. Animals have an unfortunate habit of screaming when they look into my eyes.";
                    g.responseRoad = "The woods are wide awake tonight, innkeeper. The hunt has already begun. You can smell the iron in the air, can't you?";
                    g.responseIdentity = "You write names on paper... as if ink and parchment could protect you from what comes in the dark.";
                    break;
            }
        }

        private static string UniqueName(HashSet<string> used)
        {
            for (int i = 0; i < 40; i++)
            {
                string n = FirstNames[Random.Range(0, FirstNames.Length)] + " " + LastNames[Random.Range(0, LastNames.Length)];
                if (used.Add(n)) return n;
            }
            string fallback = "Traveler #" + Random.Range(100, 999);
            used.Add(fallback);
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
            tex.Apply(false, false);

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
            if (g.portrait != null) Object.Destroy(g.portrait);
            if (g.bodySprite != null) Object.Destroy(g.bodySprite);
            if (g.spriteSheet != null) Object.Destroy(g.spriteSheet);
            g.portrait = null;
            g.bodySprite = null;
            g.spriteSheet = null;
        }
    }
}
