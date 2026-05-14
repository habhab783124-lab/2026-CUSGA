using System.Collections.Generic;

public static class Chapter2
{
    public static IReadOnlyList<DialogueLine> Get(string id)
    {
        switch (id)
        {
            case "chapter2_intro":
                return Intro();

            default:
                return null;
        }
    }

    private static IReadOnlyList<DialogueLine> Intro()
    {
        return new List<DialogueLine>
        {
            Npc("第三小队，[pause]<size=150%>重点</size>任务报告已确认。"),
            Npc("战果评估：东区外围变异体清剿完成正常速度正常速度正常速度正常速度正常速度正常速度正常速度正常速度正常速度正常速度正常速度[speed=0.01]<size=150%>突然加速突然加速突然加速突然加速突然加速突然加速突然加速突然加速突然加速突然加速突然加速突然加速突然加速！！！。</size>"),
            Npc("清理者凌，[pause]你的执勤窗口已关闭，请返回住宿区，准备参加明天的表彰大会。"),
            Npc("<size=150%>通话结束。</size>"),
            Player("[speed=0.5]<size=150%>为了伊甸!</size>", Strong()),
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
