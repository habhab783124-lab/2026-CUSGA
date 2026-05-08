using System.Collections.Generic;

public static class Chapter4
{
    public static IReadOnlyList<DialogueLine> Get(string id)
    {
        switch (id)
        {
            case "chapter4_intro":
                return Intro();

            case "chapter4_Chen":
                return Chen();

            default:
                return null;
        }
    }

    private static IReadOnlyList<DialogueLine> Intro()
    {
        return new List<DialogueLine>
        {
            new DialogueLine
            {
                speaker = DialogueSpeaker.NPC,
                text = "第四章台词占位：在这里替换为本场景 NPC 的对白。",
                emphasis = Normal()
            }
        };
    }

    private static IReadOnlyList<DialogueLine> Chen()
    {
        return new List<DialogueLine>
        {
            Npc("今天的任务地点在这里。"),
            Npc("七号废料厂，位于东区防御墙外三点二公里处。"),
            Npc("昨天遭遇变异体袭击，十六名工人死亡，生产任务中断。"),
            Npc("废料厂供应着东区百分之七十的能源，如果不尽快恢复运转，伊甸将会瘫痪。"),
            Npc("凌，你的任务是清剿废料厂区域内的所有变异体。明白？"),
            Player("明白。为了伊甸。", Strong()),
            Npc("凌，你也知道净水减配的事了吧？"),
            Player("知道。"),
            Npc("你有没有想过，如果你是做决定的人，当资源不够所有人使用，你会怎么办？"),
            Player("资源优先分配给最能创造价值的人。这是伊甸的原则。"),
            Npc("这是教条。不是你自己的想法。"),
            Npc("我问的是，如果有一天，需要你来决定谁活谁死，你怎么办？"),
            Player("我……不知道。", Pulse()),
            Npc("我给你讲个故事。"),
            Npc("有一艘船，在海上漂。船上有十个人，但食物只够五个人撑到岸边。"),
            Npc("你是船长，你会怎么做？"),
            Player("让五个人活。五个人死。"),
            Npc("选哪五个？"),
            Player("最能活下去的。最能帮别人活下去的。"),
            Npc("这是第一种方案：择优。让最强壮、最有能力的人活下来。"),
            Npc("这样剩下的人能划船、能捕鱼、能修船。"),
            Npc("这是最理性的选择。但被选去死的人，会问：凭什么？"),
            Player("……凭什么？", Pulse()),
            Npc("好。那第二种方案，抽签。完全随机。"),
            Npc("谁抽到死签谁死。公平吗？"),
            Player("……公平。但可能抽到的是老人、病人，然后船会撑不到岸边。"),
            Npc("对。公平的代价是，所有人都可能死得更快。"),
            Npc("那第三种方案，平分食物。"),
            Npc("十个人分五人的口粮。每个人都分到一半。"),
            Player("那所有人都会饿死。每个人分到的都不够。"),
            Npc("对。平均分配的代价是全军覆没。"),
            Player("……", Pulse()),
            Player("所以没有完美的方案。"),
            Npc("没有。"),
            Npc("但如果你必须选一个呢？"),
            Npc("你是船长，你会选哪个？"),
            Player("……", Pulse()),
            Player("第一种。择优。"),
            Npc("为什么？"),
            Player("因为至少能活下来一部分人。至少船不会沉。"),
            Npc("这就是伊甸在做的事。"),
            Npc("在所有这些方案里，它的代价最小，是相对最优的选择。"),
            Npc("当然，“相对最优”的意思就是，它依然是错的，只是错得没那么离谱。"),
            Npc("好了，休息时间结束了。准备下一次任务去吧。")
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

    private static DialogueEmphasis Strong(float scaleMultiplier = 2.0f, float shakeMagnitude = 0.2f)
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
