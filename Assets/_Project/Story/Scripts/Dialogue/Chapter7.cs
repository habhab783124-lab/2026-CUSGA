using System.Collections.Generic;

public static class Chapter7
{
    public static IReadOnlyList<DialogueLine> Get(string id)
    {
        switch (id)
        {
            case "chapter7_intro":
                return Intro();

            default:
                return null;
        }
    }

    private static IReadOnlyList<DialogueLine> Intro()
    {
        return new List<DialogueLine>
        {
            Npc("就是这里。"),
            Player("天文馆？"),
            Npc("嗯。从前的人建的.用来给小孩看星星的地方。"),
            Player("好黑。"),
            Npc("嗯。没有电。"),
            Player("什么都没有。"),
            Npc("设备还在。就是开不了。"),
            Player("……"),
            Player("你带我来一个看不了星星的地方？"),
            Npc("我没带终端,供不了电。有终端就能看了。"),
            Player("……我带了。"),
            Player("终端要随身携带，万一有紧急任务怎么办？"),
            Npc("……好"),
            Npc("……"),
            Player("亮了。"),
            Npc("别高兴太早。还得找卫星。"),
            Player("你会操作？"),
            Npc("不会，但可以试试。"),
            Player("这是什么。"),
            Npc("轨道上的卫星列表。"),
            Npc("伊甸有辐射屏蔽层，地面看不到天空。只能靠接收卫星传回的图像来看星星。"),
            Player("还能连上吗。"),
            Npc("大部分都掉线了。几百年前的东西。"),
            Player("……"),
            Npc("有一颗！不过信号很弱。"),
            Player("能连上吗。"),
            Npc("我试试。"),
            Npc("……连上了！"),
            Player("这是……？"),
            Npc("卫星传回来的。现在的天空。"),
            Player("好多。"),
            Player("这就是星星？"),
            Npc("嗯。这就是星星。"),
            Player("所以，真正的天空……是这样的。"),
            Npc("嗯，这就是真实的世界。"),
            Npc("……"),
            Npc("你觉得墙外面是什么？"),
            Player("变异体。辐射。死亡。"),
            Npc("也许吧。"),
            Npc("但也许……不全是，至少还有星星。"),
            Player("……"),
            Player("你有没有觉得……它们在看你。"),
            Npc("星星？"),
            Player("嗯。像眼睛一样。"),
            Npc("……"),
            Npc("星星是黑夜的眼睛。"),
            Npc("谢谢你带我来。"),
            Player("是你带我来。"),
            Player("……"),
            Player("下次……还来吗。"),
            Npc("嗯。下次再一起来吧。"),
            Player("在那之前……这颗星星会一直挂着吧。"),
            Npc("嗯。它哪也去不了。"),
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
