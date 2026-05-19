using System.Collections.Generic;

public static class Chapter1
{
    public static IReadOnlyList<DialogueLine> Get(string id)
    {
        switch (id)
        {
            case "Demo_Ling_To_Command":
            case "chapter1_intro":
                return Intro();

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
                text = "调度中心：第三小队，任务报告已确认。",
                emphasis = Normal()
            },
            new DialogueLine
            {
                speaker = DialogueSpeaker.NPC,
                text = "战果评估：东区外围变异体清剿完成。",
                emphasis = Strong()
            },
            new DialogueLine
            {
                speaker = DialogueSpeaker.NPC,
                text = "清理者凌，你的执勤窗口已关闭，请返回住宿区，准备参加明天的表彰大会。",
                emphasis = Normal()
            },
            new DialogueLine
            {
                speaker = DialogueSpeaker.NPC,
                text = "通话结束。",
                emphasis = Strong(1.45f, 0.18f)
            },
            new DialogueLine
            {
                speaker = DialogueSpeaker.Player,
                text = "凌：为了伊甸。",
                emphasis = Normal()
            }
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
