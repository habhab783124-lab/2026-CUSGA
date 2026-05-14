using System.Collections.Generic;

public static class Chapter8
{
    public static IReadOnlyList<DialogueLine> Get(string id)
    {
        switch (id)
        {
            case "chapter8_intro":
            case "chapter8_Intro":
            case "chapter8_Chen":
                return Intro();

            default:
                return null;
        }
    }

    private static IReadOnlyList<DialogueLine> Intro()
    {
        return new List<DialogueLine>
        {
            Npc("紧急情况。变异体有组织地入侵，外墙多点同时受压，规模超过以往任何一次。"),
            Npc("凌，你带领第三小队，死守正面防区。"),
            Npc("这一次，不允许有任何闪失。", Strong()),
            Player("明白！为了伊甸！！！", Strong()),
        };
    }

    private static DialogueLine Npc(
        string text,
        DialogueEmphasis? emphasis = null)
    {
        return new DialogueLine
        {
            speaker = DialogueSpeaker.NPC,
            text = text,
            emphasis = emphasis ?? Normal()
        };
    }

    private static DialogueLine Player(
        string text,
        DialogueEmphasis? emphasis = null)
    {
        return new DialogueLine
        {
            speaker = DialogueSpeaker.Player,
            text = text,
            emphasis = emphasis ?? Normal()
        };
    }

    private static DialogueEmphasis Normal()
    {
        return new DialogueEmphasis { enabled = false, scaleMultiplier = 1.25f, shakeMagnitude = 0.08f };
    }

    private static DialogueEmphasis Strong(float scaleMultiplier = 1.35f, float shakeMagnitude = 0.12f)
    {
        return new DialogueEmphasis { enabled = true, scaleMultiplier = scaleMultiplier, shakeMagnitude = shakeMagnitude };
    }
}
