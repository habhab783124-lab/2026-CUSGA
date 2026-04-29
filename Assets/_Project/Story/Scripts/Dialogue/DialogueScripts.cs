using System.Collections.Generic;

public static class DialogueScripts
{
    // 以后你把《伊甸之雨docx》拆分成多段对话时，
    // 建议用统一命名：Chapter01_XXX, Chapter02_YYY...

    public static IReadOnlyList<DialogueLine> Get(string id)
    {
        switch (id)
        {
            case "Demo_Ling_To_Command":
                return Demo_Ling_To_Command();

            default:
                return Empty();
        }
    }

    private static IReadOnlyList<DialogueLine> Empty()
    {
        return new List<DialogueLine>();
    }

    private static IReadOnlyList<DialogueLine> Demo_Ling_To_Command()
    {
        return new List<DialogueLine>
        {
            new DialogueLine
            {
                speaker = DialogueSpeaker.NPC,
                text = "调度中心：第三小队，任务报告已确认。",
                emphasis = new DialogueEmphasis { enabled = false, scaleMultiplier = 1.25f, shakeMagnitude = 0.08f }
            },
            new DialogueLine
            {
                speaker = DialogueSpeaker.NPC,
                text = "战果评估：东区外围变异体清剿完成。",
                emphasis = new DialogueEmphasis { enabled = true, scaleMultiplier = 1.35f, shakeMagnitude = 0.12f }
            },
            new DialogueLine
            {
                speaker = DialogueSpeaker.NPC,
                text = "清理者凌，你的执勤窗口已关闭，请返回住宿区，准备参加明天的表彰大会。",
                emphasis = new DialogueEmphasis { enabled = false, scaleMultiplier = 1.25f, shakeMagnitude = 0.08f }
            },
            new DialogueLine
            {
                speaker = DialogueSpeaker.NPC,
                text = "通话结束。",
                emphasis = new DialogueEmphasis { enabled = true, scaleMultiplier = 1.45f, shakeMagnitude = 0.18f }
            },
            new DialogueLine
            {
                speaker = DialogueSpeaker.Player,
                text = "凌：为了伊甸。",
                emphasis = new DialogueEmphasis { enabled = false, scaleMultiplier = 1.25f, shakeMagnitude = 0.08f }
            }
        };
    }
}

