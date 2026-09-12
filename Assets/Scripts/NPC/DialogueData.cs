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

    public string[] greetings;
    public string[] idleChat;
    public string[] sittingThoughts;
    public string[] sleepyMurmurs;
    public string[] farewells;

    public ConversationExchange[] npcConversations;

    public string GetRandom(string[] pool)
    {
        if (pool == null || pool.Length == 0)
            return string.Empty;
        return pool[Random.Range(0, pool.Length)];
    }

    public ConversationExchange GetRandomConversation()
    {
        if (npcConversations == null || npcConversations.Length == 0)
            return null;
        return npcConversations[Random.Range(0, npcConversations.Length)];
    }
}
