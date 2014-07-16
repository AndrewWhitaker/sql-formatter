namespace SqlFormatter
{
    public enum TokenType
    {
        Whitespace = 0,
        Word = 1,
        Quote = 2,
        BacktickQuote = 3,
        Reserved = 4,
        ReservedTopLevel = 5,
        ReservedNewline = 6,
        Boundary = 7,
        Comment = 8,
        BlockComment = 9,
        Number = 10,
        Error = 11,
        Variable = 12
    }
}
