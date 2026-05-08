using System.Collections.Generic;

public static class DialogueScripts
{
    public static IReadOnlyList<DialogueLine> Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Empty();
        }

        return Chapter1.Get(id)
            ?? Chapter2.Get(id)
            ?? Chapter3.Get(id)
            ?? Chapter4.Get(id)
            ?? Chapter5.Get(id)
            ?? Chapter6.Get(id)
            ?? Chapter7.Get(id)
            ?? Chapter8.Get(id)
            ?? Chapter9.Get(id)
            ?? Chapter10.Get(id)
            ?? Empty();
    }

    private static IReadOnlyList<DialogueLine> Empty()
    {
        return new List<DialogueLine>();
    }
}

