namespace Han.CheckIn
{
    public static partial class HanLocalization
    {
        private static void RegisterGameplayStrings()
        {
            // ---- Director Top HUD, Toasts & End / Accusation IMGUI ----
            Add("director.hud_checkin",
                "Night {0}  |  Evening check-ins ({1}/{2})  |  [F4] Fast-Forward to Night",
                "Noaptea {0}  |  Primirea drumeților ({1}/{2})  |  [F4] Grăbește spre noapte",
                "{0}. éjszaka  |  Esti bejelentkezés ({1}/{2})  |  [F4] Ugrás éjszakára");
            Add("director.hud_night",
                "Night {0}  |  Time left: {1}s  |  Guests: {2}  |  [N / F6] End Night",
                "Noaptea {0}  |  Timp rămas: {1}s  |  Drumeți: {2}  |  [N / F6] Încheie noaptea",
                "{0}. éjszaka  |  Hátralévő idő: {1} mp  |  Vendégek: {2}  |  [N / F6] Éjszaka vége");
            Add("director.hud_accusation",
                "Night {0}  |  Accusation",
                "Noaptea {0}  |  Judecata hanului",
                "{0}. éjszaka  |  Számonkérés");

            Add("director.fast_forward_btn",
                ">> Fast-Forward Check-Ins [F4]",
                ">> Treci peste primire [F4]",
                ">> Bejelentkezés átugrása [F4]");

            Add("director.toast_arrivals",
                "Night {0}: travelers arrive at the Han desk...",
                "Noaptea {0}: drumeții bat la poarta hanului...",
                "{0}. éjszaka: utazók érkeznek a fogadó pultjához...");
            Add("director.toast_click_approve",
                "{0}: Click \"Approve\" to check them in (Free Look unlocked).",
                "{0}: Apasă «Aprobă» când ești gata să-l primești în han (privire liberă).",
                "{0}: Kattints a «Jóváhagyás» gombra a beléptetéshez (szabad körülnézés).");
            Add("director.toast_all_checked_in",
                "All {0} travelers checked in! Night {1} begins...",
                "Toți cei {0} drumeți au fost trecuți în condică! Se lasă Noaptea {1}...",
                "Mind a(z) {0} utazó bejelentkezett! Kezdődik a(z) {1}. éjszaka...");
            Add("director.toast_night_falls",
                "Night {0} falls. Watch the guests, carry the ledger, press N to end the night.",
                "Se lasă Noaptea {0} peste codru. Păzește drumeții, poartă condica și apasă N la sfârșit.",
                "Leszáll a(z) {0}. éjszaka. Tartsd szemmel a vendégeket, vidd a könyvet, nyomj N-t a befejezéshez.");
            Add("director.toast_door_murder",
                "You opened the door to Room #{0}... and found {1} DEAD! Panic erupts across the Han!",
                "Ai deschis ușa de la Odaia #{0}... și l-ai găsit pe {1} FĂRĂ SUFLARE! Groaza cuprinde hanul!",
                "Kinyitottad a(z) {0}. szoba ajtaját... és {1} HOLTAN fekszik ott! Pánik tör ki a fogadóban!");
            Add("director.toast_body_discovered",
                "You found the body of {0}. Everyone in the Han is on edge.",
                "Ai descoperit trupul neînsuflețit al lui {0}. Spaima plutește peste tot hanul.",
                "Megtaláltad {0} holttestét. A fogadóban mindenki retteg.");
            Add("director.toast_dawn",
                "Dawn. Some guests have quietly left. A new evening approaches...",
                "Se crapă de ziuă. Unii drumeți au plecat în tăcere prin pădure. O nouă seară se apropie...",
                "Hajnalodik. Néhány vendég csendben továbbállt. Újabb este közeleg...");

            Add("director.fallback_checkin_line",
                "Good evening, innkeeper. I seek a warm room for the night.",
                "Bună seara, hangiță. Caut un culcuș cald până se luminează de ziuă.",
                "Jó estét, fogadós! Meleg szobát keresek éjszakára.");

            Add("director.btn_go_on",
                "Go on >>",
                "Continuă >>",
                "Tovább >>");
            Add("director.btn_approve",
                "Approve >>",
                "Aprobă >>",
                "Jóváhagyás >>");

            Add("director.ask_fallback_purpose",
                "Just looking for a warm bed tonight, innkeeper.",
                "Caut doar un pat cald și adăpost de fiarele codrului, hangiță.",
                "Csak egy meleg ágyat keresek éjszakára, fogadós.");
            Add("director.ask_fallback_road",
                "The mountain road was bitterly cold and dark.",
                "Drumul prin pădurea de pe culme a fost cumplit de rece și întunecos.",
                "A hegyi erdei út dermesztően hideg és sötét volt.");
            Add("director.ask_fallback_identity",
                "My name is written truthfully on your ledger.",
                "Numele meu e scris cu cinste în condica hanului.",
                "A nevem igaz módon szerepel a könyvedben.");

            Add("director.lure_killer",
                "Walk with me to your room... I have something urgent to show you.",
                "Vino cu mine până-n odaia ta... am să-ți arăt ceva de mare taină.",
                "Gyere velem a szobádba... sürgősen mutatnom kell valamit.");
            Add("director.lure_victim",
                "To my room? Alright... what is this about?",
                "În odaia mea? Fie... despre ce taină vorbești?",
                "A szobámba? Rendben... miről van szó?");

            Add("director.panic_monster",
                "Terrible... terrible. Who could do this?",
                "Cumplit... cumplită nelegiuire. Cine-a putut face una ca asta?",
                "Szörnyű... szörnyűség. Ki tehetett ilyet?");
            Add("director.panic_human",
                "A body! There is a body!",
                "Un mort! Zace cineva ucis aici!",
                "Egy holttest! Valakit megöltek!");

            Add("director.interrogate_header",
                "* INTERROGATION: {0} ({1})",
                "* CERCETARE LA TEJGHEA: {0} ({1})",
                "* KIHALLGATÁS: {0} ({1})");
            Add("director.interrogate_meta",
                "Room #{0} | Horse stabled: {1} | Arrival: {2}",
                "Odaia #{0} | Cal la grajd: {1} | Sosire: {2}",
                "{0}. szoba | Ló az istállóban: {1} | Érkezés: {2}");
            Add("director.yes", "YES", "DA", "IGEN");
            Add("director.no", "NO", "NU", "NEM");
            Add("director.focus_traveler",
                "[Right-Click / Q] Focus: TRAVELER (Click for Ledger)",
                "[Click Drept / Q] Privire: DRUMEȚ (Click pt Condică)",
                "[Jobb klikk / Q] Fókusz: UTAZÓ (Kattints a könyvhöz)");
            Add("director.focus_ledger",
                "[Right-Click / Q] Focus: LEDGER (Click for Traveler)",
                "[Click Drept / Q] Privire: CONDICĂ (Click pt Drumeț)",
                "[Jobb klikk / Q] Fókusz: VENDÉGKÖNYV (Kattints az utazóhoz)");
            Add("director.q1",
                "[1] \"What brings you to our inn tonight?\"",
                "[1] «Cu ce treburi ai poposit la hanul nostru în noaptea asta?»",
                "[1] \"Mi járatban vagy ma éjjel a fogadónkban?\"");
            Add("director.q2",
                "[2] \"Did you arrive on horseback or on foot?\"",
                "[2] «Ai sosit călare sau pe jos prin codru?»",
                "[2] \"Lóháton vagy gyalog érkeztél?\"");
            Add("director.q3",
                "[3] \"What did you see on the mountain road?\"",
                "[3] «Ce-ai zărit pe potecile muntelui?»",
                "[3] \"Mit láttál a hegyi ösvényeken?\"");
            Add("director.q4",
                "[4] \"Check their name and papers.\"",
                "[4] «Cercetează numele și zapisul de drum.»",
                "[4] \"Ellenőrizd a nevét és az iratait.\"");
            Add("director.approve_stamp_btn",
                "[SPACE]  [x] APPROVE & STAMP LEDGER",
                "[SPACE]  [x] APROBĂ ȘI APLICĂ PECETEA",
                "[SPACE]  [x] JÓVÁHAGYÁS ÉS PECSÉTELÉS");

            Add("director.accusation_title",
                "The night is over. Whom do you throw out?",
                "Noaptea s-a sfârșit. Pe cine alungi din han în întuneric?",
                "Vége az éjszakának. Kit küldesz el a fogadóból?");
            Add("director.accusation_select",
                "Select exactly {0}  ({1}/{0})",
                "Alege exact {0} drumeți  ({1}/{0})",
                "Válassz ki pontosan {0} vendéget  ({1}/{0})");
            Add("director.accusation_candidate",
                "{0}{1}   (Room #{2})",
                "{0}{1}   (Odaia #{2})",
                "{0}{1}   ({2}. szoba)");
            Add("director.accusation_confirm",
                "Throw them out",
                "Alungă-i pe poartă",
                "Zavard el őket");

            Add("director.end_victory_title", "VICTORY", "IZBÂNDĂ", "GYŐZELEM");
            Add("director.end_defeat_title", "DEFEAT", "PIERZANIE", "VERESÉG");
            Add("director.end_play_again", "Play again", "Începe o nouă veghe", "Új játék");
            Add("director.end_victory_msg",
                "All monsters have been driven out of the Han. The village is safe!",
                "Toate arătările nopții au fost izgonite din han. Călătorii și satele din jur au scăpat!",
                "Minden szörnyet kiűztél a fogadóból. A környék megmenekült!");
            Add("director.end_defeat_outnumbered",
                "The monsters now outnumber the living. The Han belongs to them.",
                "Arătările codrului sunt acum mai multe decât oamenii vii. Hanul le aparține.",
                "A szörnyek már többen vannak, mint az élők. A fogadó immár az övék.");
            Add("director.end_defeat_timeout",
                "{0} nights have passed and the monsters still lurk among your guests.",
                "Au trecut {0} nopți, iar jivinele nopții încă pândesc printre drumeții hanului.",
                "{0} éjszaka telt el, és a szörnyek még mindig a vendégek között rejtőzködnek.");

            // ---- SpeechBubble & NPCController ----
            Add("bubble.default_header",
                "[ TRAVELER STATEMENT ]",
                "[ MĂRTURIA DRUMEȚULUI ]",
                "[ UTAZÓ NYILATKOZATA ]");
            Add("npc.someone", "someone", "cineva", "valaki");
            Add("npc.poor_soul", "the poor soul", "bietul drumeț", "szegény lélek");
            Add("npc.fallback_hello", "Hello!", "Bună seara!", "Jó estét!");
            Add("npc.fallback_hi", "Hi there!", "Noroc bun!", "Üdvözlet!");

            // ---- DialogueData Normal & Murder Lines ----
            Add("dlg.norm.greet.0", "Greetings, traveler!", "Noroc bun, călătorule!", "Üdvözöllek, utazó!");
            Add("dlg.norm.greet.1", "Hello there.", "Să trăiești, drumețule.", "Jó estét!");
            Add("dlg.norm.greet.2", "Nice to see you.", "Mă bucur să te văd teafăr.", "Örülök, hogy látlak.");

            Add("dlg.norm.idle.0", "Lovely weather we're having.", "E liniște în codru... parcă prea mare liniște.", "Csend van odakint az erdőben... talán túl nagy is.");
            Add("dlg.norm.idle.1", "I wonder what's for dinner.", "Oare ce-o fierbe hangița la ceaun diseară?", "Vajon mi készül ma vacsorára a konyhán?");
            Add("dlg.norm.idle.2", "It's peaceful around here.", "E cald lângă vatră, departe de negura pădurii.", "Jó meleg van itt a tűz mellett.");

            Add("dlg.norm.sit.0", "Ah, my back...", "Of, mă dor șalele de atâta drum...", "Jaj, sajog a hátam a hosszú úttól...");
            Add("dlg.norm.sit.1", "This seat isn't too bad.", "E bună lavița asta după o zi prin glod.", "Egész kényelmes ez a pad.");
            Add("dlg.norm.sit.2", "Much better...", "Așa mai zic și eu odihnă...", "Így már sokkal jobb...");

            Add("dlg.norm.sleep.0", "Zzz...", "Zzz...", "Zzz...");
            Add("dlg.norm.sleep.1", "*mumbles*", "*bolborosește prin somn*", "*álmában motyog*");
            Add("dlg.norm.sleep.2", "Just five more minutes...", "Încă un ceas de somn până la cântatul cocoșilor...", "Csak még egy kis pihenést hajnalig...");

            Add("dlg.norm.bye.0", "Goodbye!", "Rămâi cu bine!", "Viszlát!");
            Add("dlg.norm.bye.1", "See you later.", "Ne-om mai vedea în zori.", "Találkozunk később.");
            Add("dlg.norm.bye.2", "Take care.", "Păzește-te de noapte.", "Vigyázz magadra.");

            Add("dlg.norm.conv.0.a", "Did you hear the news?", "Ai auzit ce se zvonește prin sate?", "Hallottad a híreket?");
            Add("dlg.norm.conv.0.b", "No, what happened?", "Nu, ce s-a mai întâmplat în codru?", "Nem, mi történt?");
            Add("dlg.norm.conv.1.a", "It's quite busy today.", "E plină sala hanului în seara asta.", "Elég sokan vannak ma a fogadóban.");
            Add("dlg.norm.conv.1.b", "Indeed it is.", "Așa e, lumea fuge de întunericul pădurii.", "Valóban így van.");
            Add("dlg.norm.conv.2.a", "I'm feeling a bit tired.", "Mă răpune osteneala drumului.", "Kicsit fáradtnak érzem magam.");
            Add("dlg.norm.conv.2.b", "You should get some rest.", "Mergi și trage zăvorul la odaie.", "Pihenned kellene egy keveset.");

            Add("dlg.murd.greet.0",
                "{victim} is dead! Dead in their own room! God help us all.",
                "{victim} e mort! Ucis chiar în odaia lui! Să ne păzească Cel de Sus!",
                "{victim} halott! A saját szobájában ölték meg! Isten irgalmazzon nekünk!");
            Add("dlg.murd.greet.1",
                "Don't come near me. One of us did this, and I saw {suspect} lurking about.",
                "Nu te apropia de mine! Unul dintre noi e arătarea, și pe {suspect} l-am zărit pândind pe hol.",
                "Ne gyere közelebb! Közülünk tette valaki, és láttam, hogy {suspect} itt ólálkodott.");
            Add("dlg.murd.greet.2",
                "Innkeeper! Do something! Whatever killed {victim} is still under this roof.",
                "Hangiță! Fă ceva! Dihania care l-a răpus pe {victim} se află încă sub acest acoperiș!",
                "Fogadós! Tegyél valamit! {victim} gyilkosa még mindig e fedél alatt van!");
            Add("dlg.murd.greet.3",
                "Lock your doors. I would not trust {suspect} tonight.",
                "Trageți zăvoarele grele! Eu unul nu m-aș încrede în {suspect} în noaptea asta.",
                "Zárjátok be az ajtókat! Én ma éjjel semmiképp nem bíznék ebben: {suspect}.");
            Add("dlg.murd.greet.4",
                "I heard footsteps before dawn. {suspect} was awake, I know it.",
                "Am auzit pași pe scânduri înainte de zori. {suspect} umbla treaz, știu sigur.",
                "Lépéseket hallottam hajnal előtt. {suspect} ébren volt, biztosan tudom.");

            Add("dlg.murd.sit.0",
                "I cannot stop thinking about {victim}...",
                "Nu-mi pot lua gândul de la bietul {victim}...",
                "Nem megy ki a fejemből {victim} sorsa...");
            Add("dlg.murd.sit.1",
                "{suspect} keeps watching everyone. Why?",
                "{suspect} se uită țintă la fiecare dintre noi. De ce?",
                "{suspect} mindenkit figyel. Vajon miért?");
            Add("dlg.murd.sit.2",
                "Nobody will sleep tonight.",
                "Nimeni n-o să mai pună geană peste geană până la ziuă.",
                "Ma éjjel senki nem fog aludni.");

            Add("dlg.murd.sleep.0",
                "Don't let it take me too... I heard footsteps.",
                "Nu lăsa să vină și după mine... aud iarăși pași la ușă.",
                "Ne hagyd, hogy engem is elvigyen... lépteket hallottam.");
            Add("dlg.murd.sleep.1",
                "...{victim}... no... not {victim}...",
                "...{victim}... nu... nu și pe {victim}...",
                "...{victim}... ne... csak őt ne...");

            Add("dlg.murd.panic.0",
                "A body! There is a body!",
                "Un mort! E cineva ucis în han!",
                "Egy holttest! Valakit megöltek!");
            Add("dlg.murd.panic.1",
                "Murder! Murder in the Han!",
                "Moarte! Crima a pătruns în han!",
                "Gyilkosság! Gyilkos van a fogadóban!");
            Add("dlg.murd.panic.2",
                "Saints preserve us, it's {victim}!",
                "Doamne ferește-ne, e chiar {victim}!",
                "Szentek őrizzenek minket, ez {victim}!");

            Add("dlg.murd.conv.0.a",
                "Did you see {victim}'s body?",
                "Ai văzut cum zăcea trupul lui {victim}?",
                "Láttad {victim} holttestét?");
            Add("dlg.murd.conv.0.b",
                "I did. {suspect} was standing nearby, pale as milk.",
                "L-am văzut. {suspect} stătea prin preajmă, alb ca varul.",
                "Láttam. {suspect} ott állt a közelben, sápadtan, mint a fal.");
            Add("dlg.murd.conv.1.a",
                "It must be one of the guests.",
                "Nu poate fi decât unul dintre cei primiți în han.",
                "Biztosan a vendégek közül való az egyik.");
            Add("dlg.murd.conv.1.b",
                "Then trust no one. Not even me.",
                "Atunci nu te încrede în nimeni. Nici măcar în mine.",
                "Akkor senkiben ne bízz. Még bennem sem.");
            Add("dlg.murd.conv.2.a",
                "I heard screaming before dawn.",
                "Am auzit un țipăt înăbușit înainte de zori.",
                "Sikolyt hallottam hajnal előtt.");
            Add("dlg.murd.conv.2.b",
                "So did I. Where was {suspect}?",
                "Și eu. Unde umbla {suspect} în ceasul acela?",
                "Én is. Hol volt akkor {suspect}?");

            // ---- GuestGenerator Arrival Times, Notes, Tells, Decoys, Responses ----
            Add("arrival.sunset",    "Sunset",    "Asfințit",       "Naplemente");
            Add("arrival.dusk",      "Dusk",      "Amurg",          "Alkonyat");
            Add("arrival.twilight",  "Twilight",  "Înserare",       "Szürkület");
            Add("arrival.nightfall", "Nightfall", "Căderea nopții", "Éj leszállta");

            Add("guest.fallback_prefix", "Traveler #", "Drumeț #", "Utazó #");

            Add("guest.personality.Shy",            "Shy",     "Sfios",      "Félénk");
            Add("guest.personality.Pushy",          "Pushy",   "Îndrăzneț",  "Tolakodó");
            Add("guest.personality.Jovial",         "Jovial",  "Vesel",      "Kedélyes");
            Add("guest.personality.Stern",          "Stern",   "Posac",      "Komor");
            Add("guest.personality.Nervous",        "Nervous", "Neliniștit", "Ideges");
            Add("guest.personality.MonsterUncanny", "Uncanny", "Straniu",    "Hátborzongató");

            Add("guest.note.neutral.0",
                "Merchant travelling with barrels of coarse salt. Pays in old silver coin.",
                "Neguțător pornit cu butoaie de sare grunjoasă. Plătește în arginți vechi.",
                "Kősós hordókkal utazó kereskedő. Régi ezüstpénzzel fizet.");
            Add("guest.note.neutral.1",
                "Pilgrim on the road to a distant monastery. Speaks softly, prays before eating.",
                "Pelerin pornit spre o mănăstire depărtată din munți. Vorbește încet și se închină înainte de masă.",
                "Távoli kolostorba tartó zarándok. Halkan beszél, és étkezés előtt imádkozik.");
            Add("guest.note.neutral.2",
                "Wool trader from Moldavia. Complains loudly about the price of the room.",
                "Neguțător de lână venit din Țara Moldovei. Se tocmește zgomotos la prețul odăii.",
                "Moldvai gyapjúkereskedő. Hangosan sokallja a szoba árát.");
            Add("guest.note.neutral.3",
                "Young scribe carrying sealed letters. Refuses to say who they are for.",
                "Tânăr grămătic ce poartă răvașe cu pecete de ceară. Nu destăinuie cui îi sunt trimise.",
                "Fiatal írnok lepecsételt levelekkel. Nem árulja el, kinek szólnak.");
            Add("guest.note.neutral.4",
                "Widow in mourning black. Asked for a room far from the stairs.",
                "Văduvă îmbrăcată în straie cernite. A cerut o odaie dosnică, departe de scară.",
                "Gyászruhás özvegy. A lépcsőtől távoli szobát kért.");
            Add("guest.note.neutral.5",
                "Blacksmith's apprentice, tired and dusty. Asked for hot broth and a bench.",
                "Ucenic de fierar, colbuit și frânt de drum. A cerut o ciorbă caldă și un colț de laviță.",
                "Kovácsinas, fáradt és poros. Meleg levest és egy padot kért.");
            Add("guest.note.neutral.6",
                "Hunter with a worn crossbow. Says the woods are unusually quiet.",
                "Vânător cu arbaletă veche pe umăr. Spune că pădurea a amuțit nefiresc de devreme.",
                "Vadász kopott számszeríjjal. Azt mondja, szokatlanul néma ma az erdő.");
            Add("guest.note.neutral.7",
                "Travelling healer with jars of dried herbs. Offers remedies to the staff.",
                "Vraci călător cu ulcele pline de buruieni uscate. Îmbie oamenii hanului cu leacuri.",
                "Vándorgyógyító szárított gyógynövényekkel teli csuprokkal. Főzeteket kínál.");
            Add("guest.note.neutral.8",
                "Soldier returning from the border. Sleeps with boots on and a knife at hand.",
                "Oștean întors de la marginea țării. Doarme încălțat în cizme și cu pumnalul lângă el.",
                "Határőr katona. Csizmában alszik, és a tőre mindig keze ügyében van.");
            Add("guest.note.neutral.9",
                "Cheese seller with a creaking cart. Insisted the horse sleeps close to the door.",
                "Cioban cu car scârțâitor plin de burdufuri. A cerut ca calul să stea aproape de poartă.",
                "Sajtárus nyikorgó szekérrel. Ragaszkodott hozzá, hogy a lova az ajtóhoz közel legyen.");
            Add("guest.note.neutral.10",
                "Minstrel with a cracked fiddle. Hums the same sad tune all evening.",
                "Lăutar cu scripca crăpată. Îngână aceeași doină de jale toată seara.",
                "Vándorzenész repedt hegedűvel. Egész este ugyanazt a bús dallamot dúdolja.");
            Add("guest.note.neutral.11",
                "Quiet farmer who counts the exits of every room he enters.",
                "Gospodar tăcut care numără ieșirile fiecărei odăi în care pune piciorul.",
                "Csendes földműves, aki minden szobában megszámolja a kijáratokat.");

            Add("guest.note.monster.0",
                "Avoids the polished copper mirror in the common room.",
                "Se ferește cu orice chip de oglinda de aramă lustruită din sala cea mare.",
                "Kerüli a nagyterem fényesre csiszolt réztükrét.");
            Add("guest.note.monster.1",
                "Did not touch the garlic bread. Smelled it and turned pale.",
                "N-a pus gura pe pâinea frecată cu usturoi. A mirosit-o și s-a făcut alb ca varul.",
                "Hozzá sem nyúlt a fokhagymás kenyérhez. Megszagolta, és sápadt lett, mint a fal.");
            Add("guest.note.monster.2",
                "Hands are ice cold. Asked to keep the shutters closed all night.",
                "Are mâinile reci ca sloiul. A poruncit să rămână obloanele trase toată noaptea.",
                "Jéghideg a keze. Kérte, hogy egész éjjel zárva maradjanak a zsalugáterek.");
            Add("guest.note.monster.3",
                "Eyes the stable horses and the horses will not stop trembling.",
                "Privește fix spre caii din grajd, iar caii nu se mai opresc din tremurat.",
                "A lovakat nézi az istállóban, és az állatok azóta sem tudnak megnyugodni.");
            Add("guest.note.monster.4",
                "Refused to cross the threshold until the innkeeper said 'enter'.",
                "N-a vrut să calce pragul până nu i-a spus hangița răspicat «poftește înăuntru».",
                "Addig nem lépte át a küszöböt, amíg a fogadós be nem tessékelte.");
            Add("guest.note.monster.5",
                "Left no footprints in the muddy courtyard, the stable boy swears.",
                "N-a lăsat nicio urmă în noroiul din ogradă, se jură argatul de la grajd.",
                "Egyetlen lábnyomot sem hagyott az udvar sarában, esküszik a lovászfiú.");
            Add("guest.note.monster.6",
                "Wolves howled outside the moment this guest arrived.",
                "Lupii din codru au prins să urle chiar în clipa când a bătut la poartă.",
                "A farkasok azon nyomban üvölteni kezdtek odakint, amint megérkezett.");
            Add("guest.note.monster.7",
                "Did not eat or drink anything, and asked for no candle in the room.",
                "N-a gustat nici bucate, nici vin, și a cerut să nu i se lase lumină în odaie.",
                "Semmit nem evett és nem ivett, és gyertyát sem kért a szobájába.");

            Add("guest.note.decoy.0",
                "Startled by the church bell and spilled their cup.",
                "A tresărit la dangătul clopotului din vale și și-a vărsat cana de lut.",
                "Összerezzent a harangszóra, és kiöntötte a kupáját.");
            Add("guest.note.decoy.1",
                "Wears a rusted silver medallion. Says it was a gift from a late mother.",
                "Poartă la gât o salbă veche de argint coclit. Zice că-i rămasă de la maica lui.",
                "Rozsdás ezüstmedált visel. Azt mondja, elhunyt édesanyja hagyta rá.");
            Add("guest.note.decoy.2",
                "Asked for the shutters to be closed, afraid of the draught.",
                "A rugat să fie trase obloanele, temându-se de curentul rece dinspre codru.",
                "Kérte, hogy csukják be a zsalukat, mert fél a huzattól.");
            Add("guest.note.decoy.3",
                "Has a harsh cough and dislikes bright candles.",
                "Tușește sec și-l supără la ochi lumina puternică a opaițelor.",
                "Erősen köhög, és zavarja az éles gyertyafény.");
            Add("guest.note.decoy.4",
                "Skips meals out of a long fast, not out of habit.",
                "Nu se atinge de bucatele de frupt fiindcă ține post aspru.",
                "Hosszú böjt miatt nem eszik, nem szokásból.");
            Add("guest.note.decoy.5",
                "Avoids mirrors out of old superstition, or so they say.",
                "Se ferește de oglinzi din bătrână superstiție, cel puțin așa mărturisește.",
                "Régi babonából kerüli a tükröket, legalábbis ezt állítja.");

            Add("guest.note.horse_stall",
                " (Horse stabled: Stall #{0})",
                " (Cal înregistrat la grajd: Boxa #{0})",
                " (Ló elszállásolva: {0}. állás)");
            Add("guest.note.horse_full",
                " (Stable full - horse tied outside)",
                " (Grajdul e plin — cal priponit în ogradă)",
                " (Az istálló megtelt — ló kint kikötve)");
            Add("guest.note.deceased_room",
                "[DECEASED - found dead in Room #{0}] ",
                "[RĂPOSAT - găsit fără suflare în Odaia #{0}] ",
                "[ELHUNYT - holtan találva a(z) {0}. szobában] ");
            Add("guest.note.deceased_generic",
                "[DECEASED - found dead] ",
                "[RĂPOSAT - găsit fără suflare] ",
                "[ELHUNYT - holtan találva] ");

            Add("guest.response.horse_yes",
                "I tied my horse in the courtyard stable; I hope there is dry hay for the night.",
                "Mi-am legat calul la grajdul din ogradă; sper să aibă fân uscat peste noapte.",
                "Bekötöttem a lovamat az udvari istállóba; remélem, lesz száraz szénája éjszakára.");
            Add("guest.response.horse_no",
                "I walked here along the cold forest paths, without a horse.",
                "Am venit pe jos pe cărările reci ale codrului, fără cal.",
                "Gyalog jöttem a hideg erdei ösvényeken, ló nélkül.");

            // ---- HanDialogueCatalog (Intro Beats, Esmeralda Moods, Monsters, Archetypes, Interrogation) ----
            Add("catalog.intro.0.tag",
                "OLD WALLACHIA · PROLOGUE",
                "VECHEA VALAHIE · PROLOG",
                "RÉGI HAVASALFÖLD · PROLÓGUS");
            Add("catalog.intro.0.body",
                "Somewhere at the foot of the Carpathians, back when Old Wallachia was still named Wallachia, an inn stood hidden deep within the northern woods atop a hill, sheltering travelers from the night. None dared wander the forest after sunset, for who knows what creatures of darkness roam the mortal realm in secret.",
                "Undeva la poalele munților Carpați, pe vremea când Vechea Valahie se numea încă Valahia, un han se ascundea printre pădurile dese ale nordului, pe-un vârf de deal, oferind drumeților refugiu pe timpul nopții. Nimeni nu îndrăznea să cutreiere pădurile după asfințit, căci cine știe ce creaturi ale nopții mișună pe tărâmul muritorilor, ascunse în întuneric.",
                "Valahol a Kárpátok lábánál, amikor Ó-Havasalföldet még Havasalföldnek hívták, az északi sűrű erdők mélyén, egy dombtetőn megbújt egy fogadó, éjszakai menedéket nyújtva a vándoroknak. Napnyugta után senki sem mert az erdőben járni, mert ki tudja, miféle éjszakai lények járnak a halandók földjén a sötétbe rejtőzve.");

            Add("catalog.intro.1.tag",
                "SHADOWS OF THE NIGHT",
                "UMBRELE NOPȚII",
                "AZ ÉJSZAKA ÁRNYAI");
            Add("catalog.intro.1.body",
                "By night, mortals no longer rule the earth; it belongs only to the dead and the undead, and not even Esmeralda's Inn can keep travelers out of harm's way forever—nor protect Esmeralda herself. Every dawn brings a new burden she must carry to the end.",
                "Noaptea oamenii nu mai stăpânesc pământul, aparține doar morților ori morților vii, iar nici măcar Hanul Esmeraldei nu poate ține pe veci călătorii departe de necaz și nici măcar pe ea însăși. Fiecare început de zi este o nouă povare ce trebuie să o poarte până la capăt.",
                "Éjjel már nem az emberek uralják a földet, csak a holtaké és az élőholtaké, és még Esmeralda fogadója sem óvhatja meg örökké az utazókat a bajtól—sőt még őt magát sem. Minden új hajnal újabb terhet hoz, amelyet végig kell cipelnie.");

            Add("catalog.intro.2.tag",
                "CHRONICLE OF ESMERALDA",
                "CRONICA ESMERALDEI",
                "ESMERALDA KRÓNIKÁJA");
            Add("catalog.intro.2.body",
                "From here on we follow Esmeralda's tale together, and across these chapters her fate rests solely in your hands. Whether she lives to see another dawn depends on you and every choice you make ahead.",
                "De acum vom urmări împreună povestea Esmeraldei și, timp de câteva simple capitole, soarta ei va sta doar în mâinile tale. De va trăi să mai vadă un nou început depinde doar de tine și de deciziile ce le vei lua pe mai departe.",
                "Innentől együtt követjük Esmeralda történetét, és néhány fejezeten át a sorsa egyedül a te kezedben van. Hogy megér-e még egy hajnalt, csak rajtad és a következő döntéseiden múlik.");

            Add("catalog.intro.3.tag",
                "FATE OF THE INN",
                "DESTINUL HANULUI",
                "A FOGADÓ SORSA");
            Add("catalog.intro.3.body",
                "Good luck to you, weaver of fates, for you shall sorely need it!",
                "Baftă ție, jucător al destinelor, căci mare nevoie vei mai avea!",
                "Sok szerencsét neked, sorsok kovácsa, mert nagy szükséged lesz rá!");

            Add("catalog.mood.PrimaZi.title",    "ESMERALDA · FIRST DAY",   "ESMERALDA · PRIMA ZI",   "ESMERALDA · ELSŐ NAP");
            Add("catalog.mood.Optimist.title",   "ESMERALDA · HOPEFUL",     "ESMERALDA · OPTIMISTĂ",  "ESMERALDA · BIZAKODÓ");
            Add("catalog.mood.Confuz.title",     "ESMERALDA · UNCERTAIN",   "ESMERALDA · CONFUZĂ",    "ESMERALDA · BIZONYTALAN");
            Add("catalog.mood.Speriat.title",    "ESMERALDA · TERRIFIED",   "ESMERALDA · SPERIATĂ",   "ESMERALDA · RÉMÜLT");

            Add("catalog.mood.PrimaZi.line",
                "A new sunrise, a new chance! I hope I can make it count!",
                "Un nou răsărit, o nouă șansă! Sper că o pot face să conteze!",
                "Új napkelte, új esély! Remélem, élni tudok vele!");
            Add("catalog.mood.Optimist.line",
                "I lived to see another dawn, another chance! And I feel I can make today count too!",
                "Am prins un nou răsărit, o nouă șansă! Și simt că o pot face și astăzi să conteze!",
                "Megértem egy újabb hajnalt, egy újabb esélyt! Érzem, hogy ma is helytállok!");
            Add("catalog.mood.Confuz.line",
                "I do not know what to make of this dawn... something tells me I must do better than yesterday.",
                "Nu știu ce să cred despre acest nou răsărit… ceva îmi spune că va trebui să o duc la capăt mai bine decât ieri.",
                "Nem tudom, mit gondoljak erről az új napkeltéről… valami azt súgja, jobban kell helytállnom, mint tegnap.");
            Add("catalog.mood.Speriat.line",
                "I feel I cannot hold out much longer. Dawn used to bring me hope, yet now... whether this is my last day depends on me alone.",
                "Simt că nu mai rezist foarte mult. Înainte răsăritul îmi dădea speranță, dar acum… Depinde doar de mine dacă asta-mi va fi ultima zi ori nu.",
                "Érzem, hogy nem bírom már sokáig. Régen a hajnal reményt adott, de most… egyedül rajtam múlik, hogy ez lesz-e az utolsó napom.");

            Add("catalog.monster.None",     "HUMAN",     "OM",       "EMBER");
            Add("catalog.monster.Vampir",   "VAMPIRE",   "VAMPIR",   "VÁMPÍR");
            Add("catalog.monster.Strigoi",  "STRIGOI",   "STRIGOI",  "LIDÉRC");
            Add("catalog.monster.Capcaun",  "OGRE",      "CĂPCĂUN",  "EMBERSZÖRNY");
            Add("catalog.monster.Varcolac", "WEREWOLF",  "VÂRCOLAC", "VÉRFARKAS");

            Add("catalog.arch.Drumet.voc",   "traveler",  "drumețule",    "vándor");
            Add("catalog.arch.Negustor.voc", "merchant",  "negustorule",  "kereskedő");
            Add("catalog.arch.Taran.voc",    "peasant",   "țărane",       "földműves");
            Add("catalog.arch.Cersetor.voc", "beggar",    "cerșetorule",  "koldus");
            Add("catalog.arch.Nobil.voc",    "good lord", "nobile domn",  "nemes úr");
            Add("catalog.arch.Lautar.voc",   "minstrel",  "lăutare",      "zenész");

            Add("catalog.arch.Drumet.reason",
                "I come from afar seeking shelter for the night",
                "vin de departe și caut refugiu pe timp de noapte",
                "messziről jöttem, és éjszakai menedéket keresek");
            Add("catalog.arch.Negustor.reason",
                "weighted with wares and overtaken by night on the road",
                "cu treburi multe și m-a prins noaptea pe cărări",
                "sok dolgom akadt, és utolért az éj az úton");
            Add("catalog.arch.Taran.reason",
                "from who knows where, lost in the woods as night closes in with no shelter",
                "de nu știu unde, căci sunt tare rătăcit, dar noaptea se apropie și n-am adăpost",
                "magam sem tudom, honnan, mert eltévedtem, az éj pedig közelít és nincs fedelem");
            Add("catalog.arch.Cersetor.reason",
                "for the forest is no longer kind once darkness falls",
                "căci pădurea nu-mi mai e primitoare noaptea",
                "mert sötétedés után az erdő már nem barátságos");
            Add("catalog.arch.Nobil.reason",
                "travelling on official business, caught unexpectedly by night along the forest trails until dawn",
                "cu treburi oficiale, însă noaptea m-a luat prin surprindere printre poteci și mi-aș dori refugiu până la răsărit",
                "hivatalos ügyben járok, de váratlanul rám tört az éj az erdei ösvényeken, így pirkadatig menedéket kérek");
            Add("catalog.arch.Lautar.reason",
                "ready to play a song at your table if you grant me shelter until sunrise",
                "să cânt la masa ta dacă mi-ai da o șansă, apoi poate voi ședea și pân' la răsărit de soare",
                "hogy muzsikáljak az asztalodnál, ha helyet adsz nekem napfelkeltéig");

            Add("catalog.esmeralda.greeting",
                "Good heart to you, {0}, leave your troubles at the door and let us speak a moment before you step into my humble inn! I am Esmeralda—now tell me, who are you?",
                "Bună să-ți fie inima, {0}, lasă necazurile la intrare și hai să vorbim nițel înainte să intri în al meu umil han! Eu sunt Esmeralda, spune-mi acum, cine ești tu?",
                "Jó szívvel köszöntelek, {0}, hagyd a gondjaidat a küszöbön, és váltsunk pár szót, mielőtt belépsz szerény fogadómba! Esmeralda vagyok—mondd hát, ki vagy te?");

            Add("catalog.guest.intro",
                "Pleased to meet you, gracious Esmeralda! I am {0}, {1}.",
                "Încântat, umilă Esmeralda! Eu sunt {0}, {1}.",
                "Örömömre szolgál, kedves Esmeralda! A nevem {0}, és {1}.");
            Add("catalog.guest.unnamed", "a wandering traveler", "un călător", "egy vándor");

            Add("catalog.esmeralda.transition",
                "I see—yet I must ask you a few questions before you set foot inside my inn!",
                "Înțeleg, însă niște întrebări aș avea pentru matale înainte să calci în al meu han!",
                "Értem, ám előbb néhány kérdést fel kell tennem, mielőtt beteszed a lábad a fogadómba!");

            Add("catalog.inv.0.q",
                "Why do you keep your neck covered?",
                "De ce-ți ții gâtul acoperit?",
                "Miért takarod el a nyakadat?");
            Add("catalog.inv.0.a.0",
                "It is freezing outside; fierce winds howl through the forest!",
                "E tare frig afară, vânturile bat puternic prin pădure!",
                "Odakint fagy van, metsző szél süvít az erdőn át!");
            Add("catalog.inv.0.a.1",
                "I am unwell—the chills are bitter tonight!",
                "Sunt bolnav, frigurile sunt crâncene!",
                "Beteg vagyok, keserves hidegrázás gyötör!");
            Add("catalog.inv.0.a.2",
                "I bear old scars I would rather keep hidden.",
                "Am niște cicatrici pe care prefer să le țin ascunse.",
                "Régi sebhelyeket rejtegetek, jobb, ha nem látszanak.");

            Add("catalog.inv.1.q",
                "If I do not invite you inside, can you still enter?",
                "De nu te chem înăuntru, poți intra?",
                "Ha nem hívlak beljebb, be tudsz lépni?");
            Add("catalog.inv.1.a.0",
                "Of course I could, though it would hardly be polite!",
                "Desigur că aș putea, dar nu este prea politicos!",
                "Persze hogy be tudnék, de hát nem volna illendő!");
            Add("catalog.inv.1.a.1",
                "I would rather be invited in, as proper custom dictates!",
                "Aș prefera dacă aș fi invitat, căci așa este frumos!",
                "Jobban szeretem, ha behívnak, hiszen így illik!");

            Add("catalog.inv.2.q",
                "Why are you wearing that thing upon your head?",
                "De ce ții acel lucru pe cap?",
                "Miért hordod azt a dolgot a fejeden?");
            Add("catalog.inv.2.a.0",
                "It is bitterly cold out there, and I am unwell!",
                "Este frig afară, sunt bolnav!",
                "Odakint hideg van, betegeskedem!");

            Add("catalog.esmeralda.reject",
                "I cannot take you in at my inn tonight—forgive me!",
                "Nu te pot primi în această seară la hanul meu, cer iertare!",
                "Ma este nem fogadhatlak be a fogadómba, kérlek bocsáss meg!");
            Add("catalog.esmeralda.accept",
                "Step inside—and remember, leave your troubles at the door!",
                "Intră, dar nu uita, lasă necazurile la intrare!",
                "Lépj be, de ne feledd: a gondjaidat hagyd az ajtón kívül!");

            Add("catalog.react.reject.0", "You will regret this!", "Îți va părea rău!", "Megbánod még ezt!");
            Add("catalog.react.reject.1", "You have doomed me!", "M-ai condamnat!", "Halálra ítéltél!");

            Add("catalog.react.accept.0", "Thank you, fair Esmeralda!", "Mulțumesc, frumoasă Smaralda!", "Köszönöm, szép Esmeralda!");
            Add("catalog.react.accept.1", "At last!", "În sfârșit!", "Végre!");
        }
    }
}
