using System.Collections.Generic;

public static class Chapter6
{
    public static IReadOnlyList<DialogueLine> Get(string id)
    {
        switch (id)
        {
            case "chapter6_intro":
                return Intro();

            default:
                return null;
        }
    }

    private static IReadOnlyList<DialogueLine> Intro()
    {
        return new List<DialogueLine>
        {
            Npc("又见面了。"),
            Player("……嗯。"),
            Npc("今天下班比上次要早呢。"),
            Player("今天的进攻不是很猛烈。"),
            Npc("雨又下大了。"),
            Player("……"),
            Player("你每天都在这个时间等车？"),
            Npc("也不是吧。"),
            Player("你一般什么时候来。"),
            Npc("想来的那天。"),
            Player("……这样吗。"),
            Npc("车还有多久到啊？"),
            Player("不知道，最近的车都不太准时。"),
            Npc("天快要黑了呢，这个季节天黑得特别早。"),
            Npc("但冬至已经过了，白天已经开始变长了。"),
            Player("……"),
            Npc("你见过星星吗。"),
            Player("什么？"),
            Npc("星星。每晚都高高地挂在天上。"),
            Player("没见过。"),
            Npc("伊甸里面，看不见星星呢。"),
            Player("嗯。我从来没亲眼看过，教科书里倒是看到过。"),
            Npc("我知道一个地方。"),
            Npc("北墙外面，有一座废弃的天文馆。"),
            Npc("你想去吗？"),
            Player("……"),
            Npc("车来了。"),
            Player("……"),
            Npc("你不上去吗。"),
            Player("……"),
            Player("不。"),
            Npc("那车要开走了哦。"),
            Player("你刚才说的地方。"),
            Npc("什么？"),
            Player("那个天文馆。现在就去吧。"),
            Npc("现在？可是……"),
            Player("你说有空带我去。我现在有空。"),
            Npc("天快黑了，外面很危险。"),
            Player("你怕？"),
            Npc("不是怕。"),
            Player("那就走吧。"),
            Npc("……"),
            Npc("好。"),
            Npc("一起撑伞吧。"),
            Player("嗯。"),
         };
    }

    private static DialogueLine Npc(string text, DialogueEmphasis? emphasis = null)
    {
        return new DialogueLine
        {
            speaker = DialogueSpeaker.NPC,
            text = text,
            emphasis = emphasis ?? Normal()
        };
    }

    private static DialogueLine Player(string text, DialogueEmphasis? emphasis = null)
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
