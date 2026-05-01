using System;

[Serializable]
public sealed class DialogueLine
{
    public DialogueSpeaker speaker = DialogueSpeaker.NPC;
    public string text;
    public DialogueEmphasis emphasis;
}

public enum DialogueSpeaker
{
    Player = 0,
    NPC = 1
}

