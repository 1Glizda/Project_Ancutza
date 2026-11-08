using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogueData", menuName = "NPC/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [System.Serializable]
    public class ConversationExchange
    {
        public string lineA;
        public string lineB;
    }

    [Header("Normal Han Life")]
    public string[] greetings;
    public string[] idleChat;
    public string[] sittingThoughts;
    public string[] sleepyMurmurs;
    public string[] farewells;

    public ConversationExchange[] npcConversations;

    [Header("Murder Investigation (used once a body has been discovered)")]
    [Tooltip("Tokens: {victim} = dead guest's name, {suspect} = someone seen near the body.")]
    public string[] murderGreetings;
    public string[] murderSittingThoughts;
    public string[] murderSleepyMurmurs;
    [Tooltip("Shouted by guests standing close to the body at the moment it is found.")]
    public string[] murderPanicShouts;
    public ConversationExchange[] murderConversations;

    // ---- Built-in fallbacks so the murder state works without authoring any assets ----
    private static readonly string[] DefaultMurderGreetings =
    {
        "{victim} is dead! Dead in their own room! God help us all.",
        "Don't come near me. One of us did this, and I saw {suspect} lurking about.",
        "Innkeeper! Do something! Whatever killed {victim} is still under this roof.",
        "Lock your doors. I would not trust {suspect} tonight.",
        "I heard footsteps before dawn. {suspect} was awake, I know it."
    };

    private static readonly string[] DefaultMurderSitting =
    {
        "I cannot stop thinking about {victim}...",
        "{suspect} keeps watching everyone. Why?",
        "Nobody will sleep tonight."
    };

    private static readonly string[] DefaultMurderSleepy =
    {
        "Don't let it take me too... I heard footsteps.",
        "...{victim}... no... not {victim}..."
    };

    private static readonly string[] DefaultMurderPanic =
    {
        "A body! There is a body!",
        "Murder! Murder in the Han!",
        "Saints preserve us, it's {victim}!"
    };

    private static readonly ConversationExchange[] DefaultMurderConversations =
    {
        new ConversationExchange { lineA = "Did you see {victim}'s body?", lineB = "I did. {suspect} was standing nearby, pale as milk." },
        new ConversationExchange { lineA = "It must be one of the guests.", lineB = "Then trust no one. Not even me." },
        new ConversationExchange { lineA = "I heard screaming before dawn.", lineB = "So did I. Where was {suspect}?" }
    };

    public string GetRandom(string[] pool)
    {
        if (pool == null || pool.Length == 0)
            return string.Empty;
        return pool[Random.Range(0, pool.Length)];
    }

    private static string PickOrDefault(string[] custom, string[] fallback)
    {
        string[] pool = (custom != null && custom.Length > 0) ? custom : fallback;
        return pool[Random.Range(0, pool.Length)];
    }

    public string GetGreeting(bool murderActive)
    {
        return murderActive ? PickOrDefault(murderGreetings, DefaultMurderGreetings) : GetRandom(greetings);
    }

    public string GetSittingThought(bool murderActive)
    {
        return murderActive ? PickOrDefault(murderSittingThoughts, DefaultMurderSitting) : GetRandom(sittingThoughts);
    }

    public string GetSleepyMurmur(bool murderActive)
    {
        return murderActive ? PickOrDefault(murderSleepyMurmurs, DefaultMurderSleepy) : GetRandom(sleepyMurmurs);
    }

    public string GetPanicShout()
    {
        return PickOrDefault(murderPanicShouts, DefaultMurderPanic);
    }

    public ConversationExchange GetRandomConversation()
    {
        if (npcConversations == null || npcConversations.Length == 0)
            return null;
        return npcConversations[Random.Range(0, npcConversations.Length)];
    }

    public ConversationExchange GetConversation(bool murderActive)
    {
        if (!murderActive) return GetRandomConversation();
        var pool = (murderConversations != null && murderConversations.Length > 0) ? murderConversations : DefaultMurderConversations;
        return pool[Random.Range(0, pool.Length)];
    }
}
