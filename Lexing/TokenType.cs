namespace FatTony.Lexing;

/// <summary>
/// Every token the lexer can produce. Multi-word keyword phrases from the
/// spec (e.g. "do me a favor") each get exactly one TokenType, same as
/// single-word keywords — from the parser's point of view there is no
/// difference between them. The lexer turns both into a single token;
/// see KeywordPhrases.cs for the phrase table that makes this work.
/// </summary>
public enum TokenType
{
    // Literals
    Identifier,
    Number,
    String,
    Legit,                      // reserved literal, exact-case "LEGIT" only
    Shady,                      // reserved literal, exact-case "SHADY" only

    // Namespaces / imports
    WeGotALegitimateBusinessCalled,   // "we got a legitimate business called"
    WereConnectedTo,                  // "we're connected to"

    // Calls
    Use,
    ToDoMeAFavor,                      // "to do me a favor"
    DoMeAFavor,                        // "do me a favor"

    // Functions / return
    Function,
    HeresYourCut,                      // "here's your cut"

    // Structure
    Stop,
    Let, Be,
    Say,

    // Conditionals
    If, Otherwise,

    // Loops
    RepeatWhile,                       // "repeat while"
    WalkAway,                          // "walk away"
    KeepItMovin,                       // "keep it movin'"

    // Increment / decrement
    Increase, Decrease, By,

    // Garbage collection
    TakeOutDaTrash,                    // "take out da trash"

    // Ledgers
    Get,
    FromDaSafe,                        // "from da safe"
    From,                              // bare "from" (whack ... from ledger)
    AndDontTouchNothin,                // "and don't touch nothin'"
    AndCookDaBooks,                    // "and cook da books"
    AndKeepWritin,                     // "and keep writin'"
    Write, In,
    InDaSafe,                          // "in da safe"
    Whack,
    Burn,
    Stash,
    DaStoryFrom,                       // "da story from"

    // Input / output
    WhatDaGuySaid,                     // "what da guy said"

    // Things
    Thing,
    AnswersTo,                         // "answers to"
    Initiation,
    YoursTruly,                        // "yours truly"
    ANew,                              // "a new"
    Destroy,

    // Comparisons
    MoreThan, LessThan, AtLeast, AtMost, Equals, Aint, DontEqual,

    // Logical
    And, Or,

    // Arithmetic — symbol and word spellings both resolve to the SAME
    // token type (true synonyms; the parser never sees which was used)
    Plus, Minus, Star, Slash,

    // Punctuation
    Comma,
    Possessive,                        // 's

    // Structural
    Newline,
    Eof
}
