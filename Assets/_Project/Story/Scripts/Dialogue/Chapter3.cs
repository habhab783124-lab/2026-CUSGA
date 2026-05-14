using System.Collections.Generic;

public static class Chapter3
{
    public static IReadOnlyList<DialogueLine> Get(string id)
    {
        switch (id)
        {
        

            case "chapter3_Chen":
                return Chen();

            default:
                return null;
        }
    }



    private static IReadOnlyList<DialogueLine> Chen()
    {
        return new List<DialogueLine>
        {
            Npc("清理者凌，第三小队。昨日东区外围清剿任务，击杀所有变异体。无阵亡。战果位列第一。"),
            Npc("你知道这意味着什么吗？"),
            Player("意味着东区外围的威胁暂时解除，市民可以安全作业。"),
            Npc("不。这意味着在资源极度匮乏的今天，在辐射尘侵蚀我们每一寸土地、变异体侵略我们的家园的时候——有人站出来了。"),
            Npc("清理者用鲜血和生命，保住了这座城市，使百姓正常生活，使文明得以延续。"),
            Npc("这些，都是你们的功劳。"),
            Player("为了伊甸。",Strong()),
            Npc("为了伊甸。说得好。"),
            Npc("这是东区防御委员会颁发的“坚盾勋章”。授予在清剿行动中表现卓越的清理者。"),
            Npc("你是第三小队今年第一个获此荣誉的人。希望你戒骄戒躁，继续努力。"),
            Player("明白。"),
            Npc("很好。"),
            Npc("今天下午休息。明天六时，第四防御单元集合。有新的任务。"),

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

    private static DialogueEmphasis Strong(float scaleMultiplier = 1.6f, float shakeMagnitude = 0.2f)
    {
        return new DialogueEmphasis { enabled = true, scaleMultiplier = scaleMultiplier, shakeMagnitude = shakeMagnitude };
    }

    private static DialogueEmphasis Pulse(float scaleMultiplier = 0.92f, float shakeMagnitude = 0.1f)
    {
        return new DialogueEmphasis
        {
            enabled = true,
            scaleMultiplier = scaleMultiplier,
            shakeMagnitude = shakeMagnitude
        };
    }
}


