using System.Text;

namespace FatTony.Lexing;

public sealed class Lexer(string source)
{
    private readonly string _source = source;
    private int _pos;
    private int _line = 1;
    private bool _lineHasTokens;

    // Internal marker used only during pass 1 for a word not yet resolved
    // to a keyword/identifier. Reuses TokenType.Identifier as the
    // "pending" marker — pass 2 either confirms it as an Identifier or
    // replaces it (and its neighbors) with a keyword token.
    private const TokenType Pending = TokenType.Identifier;

    public List<Token> Tokenize()
    {
        var raw = ScanRaw();
        return MergePhrases(raw);
    }

    // ----- Pass 1: character-level scanning -----------------------------

    private List<Token> ScanRaw()
    {
        var tokens = new List<Token>();

        while (!AtEnd)
        {
            char c = Peek();

            if (c == '\n')
            {
                if (_lineHasTokens)
                {
                    tokens.Add(new Token(TokenType.Newline, "\\n", null, _line));
                    _lineHasTokens = false;
                }
                _line++;
                _pos++;
                continue;
            }

            if (c == '\r' || c == ' ' || c == '\t')
            {
                _pos++;
                continue;
            }

            if (c == '-' && PeekNext() == '-')
            {
                while (!AtEnd && Peek() != '\n') _pos++;
                continue;
            }

            if (c == '"')
            {
                tokens.Add(ScanString());
                continue;
            }

            if (char.IsDigit(c))
            {
                tokens.Add(ScanNumber());
                continue;
            }

            if (char.IsLetter(c))
            {
                tokens.AddRange(ScanWord());
                continue;
            }

            switch (c)
            {
                case ',': tokens.Add(Simple(TokenType.Comma, ",")); _pos++; break;
                case '+': tokens.Add(Simple(TokenType.Plus, "+")); _pos++; break;
                case '-': tokens.Add(Simple(TokenType.Minus, "-")); _pos++; break;
                case '*': tokens.Add(Simple(TokenType.Star, "*")); _pos++; break;
                case '/': tokens.Add(Simple(TokenType.Slash, "/")); _pos++; break;
                default:
                    throw new LexException($"Unexpected character '{c}'.", _line);
            }
        }

        if (_lineHasTokens)
            tokens.Add(new Token(TokenType.Newline, "\\n", null, _line));

        tokens.Add(new Token(TokenType.Eof, "", null, _line));
        return tokens;
    }

    private Token Simple(TokenType type, string lexeme)
    {
        _lineHasTokens = true;
        return new Token(type, lexeme, null, _line);
    }

    private Token ScanString()
    {
        int startLine = _line;
        _pos++; // consume opening quote
        var sb = new StringBuilder();
        while (!AtEnd && Peek() != '"')
        {
            if (Peek() == '\n')
                throw new LexException("Unterminated string — hit end of line before the closing quote.", startLine);
            sb.Append(Peek());
            _pos++;
        }
        if (AtEnd)
            throw new LexException("Unterminated string — hit end of file before the closing quote.", startLine);

        _pos++; // consume closing quote
        _lineHasTokens = true;
        string value = sb.ToString();
        return new Token(TokenType.String, $"\"{value}\"", value, startLine);
    }

    private Token ScanNumber()
    {
        int start = _pos;
        while (!AtEnd && char.IsDigit(Peek())) _pos++;
        string text = _source[start.._pos];
        _lineHasTokens = true;
        return new Token(TokenType.Number, text, int.Parse(text), _line);
    }

    /// <summary>
    /// Scans one word, handling the apostrophe cases: reserved
    /// contractions ("don't", "we're", "ain't", "here's", "nothin'",
    /// "movin'", "writin'") become a single raw word token; anything
    /// else followed by 's is split into an identifier-shaped raw word
    /// plus a separate Possessive token.
    /// </summary>
    private List<Token> ScanWord()
    {
        int start = _pos;
        while (!AtEnd && (char.IsLetter(Peek()) || char.IsDigit(Peek()))) _pos++;
        string baseWord = _source[start.._pos];

        if (AtEnd || Peek() != '\'')
        {
            _lineHasTokens = true;
            return [new Token(Pending, baseWord, null, _line)];
        }

        int apostrophePos = _pos;
        bool letterFollows = apostrophePos + 1 < _source.Length && char.IsLetter(_source[apostrophePos + 1]);

        if (letterFollows)
        {
            int suffixStart = apostrophePos + 1;
            int j = suffixStart;
            while (j < _source.Length && char.IsLetter(_source[j])) j++;
            string suffix = _source[suffixStart..j];
            string extended = baseWord + "'" + suffix;

            if (KeywordPhrases.ReservedContractions.Contains(extended))
            {
                _pos = j;
                _lineHasTokens = true;
                return [new Token(Pending, extended, null, _line)];
            }

            if (suffix == "s")
            {
                // Possessive: base word stands alone, 's is a separate token.
                _pos = j;
                _lineHasTokens = true;
                return
                [
                    new Token(Pending, baseWord, null, _line),
                    new Token(TokenType.Possessive, "'s", null, _line)
                ];
            }

            throw new LexException(
                $"Not sure what to do with \"{extended}\" — unexpected apostrophe usage.", _line);
        }
        else
        {
            // Trailing apostrophe with no letter after it, e.g. nothin'
            string extended = baseWord + "'";
            if (KeywordPhrases.ReservedContractions.Contains(extended))
            {
                _pos = apostrophePos + 1;
                _lineHasTokens = true;
                return [new Token(Pending, extended, null, _line)];
            }

            throw new LexException(
                $"Not sure what to do with \"{extended}\" — unexpected apostrophe usage.", _line);
        }
    }

    private bool AtEnd => _pos >= _source.Length;
    private char Peek() => _source[_pos];
    private char PeekNext() => _pos + 1 < _source.Length ? _source[_pos + 1] : '\0';

    // ----- Pass 2: merge raw words into keyword phrases ------------------

    private static List<Token> MergePhrases(List<Token> raw)
    {
        var result = new List<Token>();
        int i = 0;

        while (i < raw.Count)
        {
            var tok = raw[i];

            if (tok.Type != Pending)
            {
                result.Add(tok);
                i++;
                continue;
            }

            if (tok.Lexeme == "LEGIT")
            {
                result.Add(new Token(TokenType.Legit, tok.Lexeme, null, tok.Line));
                i++;
                continue;
            }

            if (tok.Lexeme == "SHADY")
            {
                result.Add(new Token(TokenType.Shady, tok.Lexeme, null, tok.Line));
                i++;
                continue;
            }

            if (KeywordPhrases.ByFirstWord.TryGetValue(tok.Lexeme, out var candidates))
            {
                bool matched = false;
                foreach (var candidate in candidates)
                {
                    if (Matches(raw, i, candidate.Words))
                    {
                        result.Add(new Token(candidate.Type, string.Join(' ', candidate.Words), null, tok.Line));
                        i += candidate.Words.Length;
                        matched = true;
                        break;
                    }
                }
                if (matched) continue;
            }

            // No phrase matched — it's a plain identifier.
            result.Add(new Token(TokenType.Identifier, tok.Lexeme, null, tok.Line));
            i++;
        }

        return result;
    }

    private static bool Matches(List<Token> raw, int start, string[] words)
    {
        if (start + words.Length > raw.Count) return false;
        for (int k = 0; k < words.Length; k++)
        {
            var t = raw[start + k];
            if (t.Type != Pending || t.Lexeme != words[k]) return false;
        }
        return true;
    }
}
