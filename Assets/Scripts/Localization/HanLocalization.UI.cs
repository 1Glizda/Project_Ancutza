namespace Han.CheckIn
{
    public static partial class HanLocalization
    {
        private static void RegisterUIStrings()
        {
            // ---- Language Selector Button ----
            Add("ui.language_button",
                "LANGUAGE: ENGLISH",
                "LIMBĂ: ROMÂNĂ",
                "NYELV: MAGYAR");

            // ---- Main Menu ----
            Add("menu.dossier_tag",
                "THE INN ARCHIVES  |  ANNO 1648",
                "ARHIVA HANULUI  |  ANUL 1648",
                "A FOGADÓ LEVÉLTÁRA  |  ANNO 1648");

            Add("menu.game_title",
                "ANCUȚA'S INN",
                "HANUL ANCUȚEI",
                "ANCUȚA FOGADÓJA");

            Add("menu.game_subtitle",
                "THE INNKEEPER'S LEDGER",
                "CONDICA DE VEGHE A HANULUI",
                "A FOGADÓS VENDÉGKÖNYVE");

            Add("menu.play",
                "BEGIN WATCH",
                "ÎNCEPE STRAJA",
                "ŐRSÉG MEGKEZDÉSE");

            Add("menu.settings",
                "LEDGER SETTINGS",
                "SETĂRILE HANULUI",
                "BEÁLLÍTÁSOK");

            Add("menu.credits",
                "ARCHIVAL CREDITS",
                "CRONICA FĂURITORILOR",
                "ALKOTÓK KRÓNIKÁJA");

            Add("menu.exit",
                "CLOSE LEDGER (EXIT)",
                "ÎNCHIDE CONDICA (IEȘIRE)",
                "KILÉPÉS A FOGADÓBÓL");

            Add("menu.footer_note",
                "Use Mouse or [W/S / Arrow Keys + Enter] to Navigate",
                "Folosește mouse-ul sau [W/S / Săgeți + Enter] pentru a alege",
                "Navigálj egérrel vagy a [W/S / Nyilak + Enter] billentyűkkel");

            // ---- Intro Video ----
            Add("intro.skip",
                "SKIP INTRO [SPACE]",
                "SARI PESTE PROLOG [SPACE]",
                "BEVEZETŐ ÁTUGRÁSA [SPACE]");

            // ---- In-Game HUD (GameLoopManager Panel) ----
            Add("hud.objective",
                "Inspect arriving travelers at the ledger.  [ESC] Pause  |  [F6] End Night",
                "Cercetează drumeții sosiți la condică.  [ESC] Răgaz  |  [F6] Încheie noaptea",
                "Vizsgáld meg az érkező utazókat a könyvnél.  [ESC] Szünet  |  [F6] Éjszaka vége");

            // ---- Pause Menu ----
            Add("pause.dossier_tag",
                "DESK WATCH SUSPENDED",
                "STRAJA DE LA TEJGHEA OPRITĂ",
                "ŐRSÉG SZÜNETELTETVE");

            Add("pause.title",
                "PAUSED",
                "RĂGAZ",
                "SZÜNET");

            Add("pause.resume",
                "RESUME WATCH",
                "REIA STRAJA",
                "ŐRSÉG FOLYTATÁSA");

            Add("pause.settings",
                "LEDGER SETTINGS",
                "SETĂRILE HANULUI",
                "BEÁLLÍTÁSOK");

            Add("pause.exit",
                "EXIT GAME",
                "PĂRĂSEȘTE HANUL",
                "KILÉPÉS A JÁTÉKBÓL");

            // ---- Settings Modal ----
            Add("settings.title",
                "LEDGER SETTINGS",
                "SETĂRILE HANULUI",
                "BEÁLLÍTÁSOK");

            Add("settings.master_volume",
                "MASTER AUDIO VOLUME",
                "VOLUM GENERAL",
                "FŐ HANGERŐ");

            Add("settings.look_sensitivity",
                "CAMERA LOOK SENSITIVITY",
                "SENSIBILITATE PRIVIRE",
                "KAMERA ÉRZÉKENYSÉG");

            Add("settings.door_highlight",
                "INTERACTION OUTLINE HIGHLIGHTS",
                "CONTUR OBIECTE INTERACTIVE",
                "INTERAKCIÓS KIJELÖLÉS");

            Add("settings.low_end_mode",
                "LOW-END PC MODE (LOW RAM / NO GPU) [F8]",
                "MOD PC SLAB (RAM MIC / FĂRĂ GPU) [F8]",
                "ALACSONY GÉPIGÉNYŰ MÓD [F8]");

            Add("settings.controls_hint",
                "[ESC] Pause  |  [TAB] Guest Book  |  [F8] Low-End PC Mode  |  [F6] End Night",
                "[ESC] Răgaz  |  [TAB] Condică  |  [F8] Mod PC Slab  |  [F6] Încheie noaptea",
                "[ESC] Szünet  |  [TAB] Vendégkönyv  |  [F8] Alacsony Mód  |  [F6] Éjszaka vége");

            Add("ui.return",
                "RETURN",
                "ÎNAPOI",
                "VISSZA");

            // ---- Credits Modal ----
            Add("credits.title",
                "ARCHIVAL CREDITS",
                "CRONICA FĂURITORILOR",
                "ALKOTÓK KRÓNIKÁJA");

            Add("credits.body",
                "PROJECT ANCUȚA\n\nNarrative, Systems & Bureaucratic Desk Direction\nTransylvanian Inn Mystery Team\n\nArchival Typography & Tactile Interface System",
                "PROIECTUL ANCUȚA\n\nPoveste, Sisteme & Rânduiala Tejghelei\nEchipa Misterului de la Hanul din Codru\n\nTipografie de Arhivă & Interfață Tactilă",
                "ANCUȚA PROJEKT\n\nTörténet, Rendszerek & Pultirányítás\nErdélyi Fogadó Rejtély Csapat\n\nLevéltári Tipográfia & Tapintható Felület");

            // ---- Night Titles & Subtitles (1..7 + fallback) ----
            Add("night.1.title",    "Night One",    "Noaptea Întâi",   "Első éjszaka");
            Add("night.1.subtitle", "good luck",    "noroc bun în codru", "sok szerencsét");
            Add("night.2.title",    "Night Two",    "Noaptea a Doua",  "Második éjszaka");
            Add("night.2.subtitle", "keep going",   "veghează mai departe", "tarts ki");
            Add("night.3.title",    "Night Three",  "Noaptea a Treia", "Harmadik éjszaka");
            Add("night.3.subtitle", "trust no one", "nu te încrede în nimeni", "ne bízz senkiben");
            Add("night.4.title",    "Night Four",   "Noaptea a Patra", "Negyedik éjszaka");
            Add("night.4.subtitle", "watch the doors", "păzește bine ușile", "figyeld az ajtókat");
            Add("night.5.title",    "Night Five",   "Noaptea a Cincea", "Ötödik éjszaka");
            Add("night.5.subtitle", "the wolves are howling", "lupii urlă în codru", "üvöltenek a farkasok");
            Add("night.6.title",    "Night Six",    "Noaptea a Șasea", "Hatodik éjszaka");
            Add("night.6.subtitle", "shadows lengthen", "umbrele se lungesc", "az árnyak megnyúlnak");
            Add("night.7.title",    "Night Seven",  "Noaptea a Șaptea", "Hetedik éjszaka");
            Add("night.7.subtitle", "survive until dawn", "rămâi viu până-n zori", "éld túl hajnalig");

            Add("night.fallback_title_num",
                "Night {0}",
                "Noaptea a {0}-a",
                "{0}. éjszaka");
            Add("night.fallback_combined",
                "{0}, {1}",
                "{0}, {1}",
                "{0}, {1}");

            // ---- Check-In Look-Down Prompt ----
            Add("book.prompt_open",
                "Check-in Book",
                "Condica Hanului",
                "Vendégkönyv");

            // ---- 3D Guest Ledger Book (GuestBookUI) ----
            Add("book.stamp_checked_in",
                "[ CHECKED IN ]",
                "[ ÎNREGISTRAT ]",
                "[ BEJELENTKEZVE ]");
            Add("book.title_header",
                "INNKEEPER'S LEDGER",
                "REGISTRUL HANULUI",
                "A FOGADÓ KÖNYVE");
            Add("book.notes_header",
                "NOTES & OBSERVATIONS",
                "ÎNSEMNĂRI & OBSERVAȚII",
                "FELJEGYZÉSEK");
            Add("book.unknown_traveler",
                "Unknown Traveler",
                "Călător Necunoscut",
                "Ismeretlen Utazó");
            Add("book.room_format",
                "Room #{0}",
                "Odaia #{0}",
                "{0}. szoba");
            Add("book.no_room",
                "No Room",
                "Fără odaie",
                "Nincs szoba");
            Add("book.arrival_format",
                "Arrival: {0}",
                "Sosire: {0}",
                "Érkezés: {0}");
            Add("book.no_notes",
                "No notes recorded.",
                "Nicio însemnare în registru.",
                "Nincs bejegyzés a könyvben.");
            Add("book.no_special_notes",
                "No special notes.",
                "Fără însemnări speciale.",
                "Nincs különös megjegyzés.");
            Add("book.page_counter",
                "Page {0} of {1}",
                "Fila {0} din {1}",
                "{0}. / {1}. lap");
            Add("book.recording_entry",
                "Recording entry...",
                "Înscriere în curs...",
                "Bejegyzés írása...");
            Add("book.page_single",
                "Page {0}",
                "Fila {0}",
                "{0}. lap");
            Add("book.empty_title",
                "Open Ledger",
                "Registru Deschis",
                "Nyitott Könyv");
            Add("book.empty_room",
                "Room: —",
                "Odaia: —",
                "Szoba: —");
            Add("book.empty_arrival",
                "Arrival: —",
                "Sosire: —",
                "Érkezés: —");
            Add("book.empty_notes",
                "Awaiting travelers at the reception desk...",
                "În așteptarea drumeților la tejgheaua hanului...",
                "Várakozás az utazókra a fogadó pultjánál...");
            Add("book.empty_page",
                "Page 0 of 0",
                "Fila 0 din 0",
                "0. / 0. lap");

            // ---- Default Sample Book Entries (when not populated by director) ----
            Add("sample.guest.1.notes",
                "Merchant from Moldavia. Arrived with iron-bound barrels of coarse salt. Insists his bay mare stays locked in the east stall.",
                "Neguțător din Moldova. A sosit cu butoaie ferecate pline cu sare grunjoasă. Cere ca iapa lui murgă să stea zăvorâtă în grajdul de răsărit.",
                "Moldvai kereskedő. Vasalt hordókban kősót hozott magával. Ragaszkodik hozzá, hogy pej kancája a keleti istállóban zárva maradjon.");
            Add("sample.guest.2.notes",
                "Traveled alone on foot through the dark pine woods. Unbothered by the wolves howling outside. Avoids the polished copper mirror.",
                "A străbătut singur, pe jos, codrul întunecat de brazi. Nu s-a tulburat de urletul lupilor dinafară. Se ferește de oglinda de aramă lustruită.",
                "Egyedül, gyalog érkezett a sötét fenyveseken át. A kinti farkasvonyítás sem zavarja. Kerüli a fényes réztükröt.");
            Add("sample.guest.3.notes",
                "Arrived just before midnight coach departed. Refuses to lift her thick black mourning veil. Asked for coarse salt and rowan on the threshold.",
                "A bătut la poartă chiar înainte de miezul nopții. Nu-și ridică marama cernită de pe chip. A cerut sare grunjoasă și crengi de scoruş la prag.",
                "Közvetlenül éjfél előtt érkezett. Nem hajlandó fellebbenteni sűrű fekete gyászfátylát. Sót és berkenyeágat kért a küszöbére.");
        }
    }
}
