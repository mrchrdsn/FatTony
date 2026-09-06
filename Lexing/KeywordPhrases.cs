namespace FatTony.Lexing;

/// <summary>
/// The single source of truth for every reserved word/phrase in the
/// language, both single-word keywords ("stop") and multi-word phrases
/// ("do me a favor"). Single words are just phrases of length 1, so
/// they're handled uniformly rather than as a special case.
///
/// Lookup is by the phrase's first word, then longest-candidate-first,
/// so e.g. "and" alone (logical AND) doesn't shadow the longer ledger
/// mode phrases "and don't touch nothin'" / "and cook da books" /
/// "and keep writin'" that also start with "and" — the lexer always
/// tries the longest match at a given position before falling back to
/// a shorter one, and only falls all the way through to a bare
/// identifier if nothing in the table matches at all.
/// </summary>
public static class KeywordPhrases
{
    private static readonly (string[] Words, TokenType Type)[] All =
    [
        (["we", "got", "a", "legitimate", "business", "called"], TokenType.WeGotALegitimateBusinessCalled),
        (["we're", "connected", "to"], TokenType.WereConnectedTo),

        (["use"], TokenType.Use),
        (["to", "do", "me", "a", "favor"], TokenType.ToDoMeAFavor),
        (["do", "me", "a", "favor"], TokenType.DoMeAFavor),

        (["function"], TokenType.Function),
        (["here's", "your", "cut"], TokenType.HeresYourCut),

        (["stop"], TokenType.Stop),
        (["let"], TokenType.Let),
        (["be"], TokenType.Be),
        (["say"], TokenType.Say),

        (["if"], TokenType.If),
        (["otherwise"], TokenType.Otherwise),

        (["repeat", "while"], TokenType.RepeatWhile),
        (["walk", "away"], TokenType.WalkAway),
        (["keep", "it", "movin'"], TokenType.KeepItMovin),

        (["increase"], TokenType.Increase),
        (["decrease"], TokenType.Decrease),
        (["by"], TokenType.By),

        (["take", "out", "da", "trash"], TokenType.TakeOutDaTrash),

        (["get"], TokenType.Get),
        (["from", "da", "safe"], TokenType.FromDaSafe),
        (["from"], TokenType.From),
        (["and", "don't", "touch", "nothin'"], TokenType.AndDontTouchNothin),
        (["and", "cook", "da", "books"], TokenType.AndCookDaBooks),
        (["and", "keep", "writin'"], TokenType.AndKeepWritin),
        (["write"], TokenType.Write),
        (["in", "da", "safe"], TokenType.InDaSafe),
        (["in"], TokenType.In),
        (["whack"], TokenType.Whack),
        (["burn"], TokenType.Burn),
        (["stash"], TokenType.Stash),
        (["da", "story", "from"], TokenType.DaStoryFrom),

        (["what", "da", "guy", "said"], TokenType.WhatDaGuySaid),

        (["thing"], TokenType.Thing),
        (["answers", "to"], TokenType.AnswersTo),
        (["initiation"], TokenType.Initiation),
        (["yours", "truly"], TokenType.YoursTruly),
        (["a", "new"], TokenType.ANew),
        (["destroy"], TokenType.Destroy),

        (["more", "than"], TokenType.MoreThan),
        (["less", "than"], TokenType.LessThan),
        (["at", "least"], TokenType.AtLeast),
        (["at", "most"], TokenType.AtMost),
        (["equals"], TokenType.Equals),
        (["ain't"], TokenType.Aint),
        (["don't", "equal"], TokenType.DontEqual),

        (["and"], TokenType.And),
        (["or"], TokenType.Or),

        (["plus"], TokenType.Plus),
        (["minus"], TokenType.Minus),
        (["times"], TokenType.Star),
        (["split", "by"], TokenType.Slash),
    ];

    /// <summary>Candidates grouped by first word, longest phrase first.</summary>
    public static readonly IReadOnlyDictionary<string, (string[] Words, TokenType Type)[]> ByFirstWord =
        All.GroupBy(p => p.Words[0])
           .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Words.Length).ToArray());

    /// <summary>
    /// Reserved contraction spellings that must be recognized as a single
    /// word at the character-scanning stage (pass 1), before phrase
    /// matching runs — otherwise "don't", "here's", etc. would be
    /// mistaken for an identifier plus a stray possessive marker.
    /// </summary>
    public static readonly IReadOnlySet<string> ReservedContractions = new HashSet<string>
    {
        "we're", "don't", "ain't", "nothin'", "movin'", "writin'", "here's"
    };
}
