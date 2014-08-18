namespace SqlFormatter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public class SqlFormatter
    {
        private static char Tab = '\t';

        private static Regex Whitespace = new Regex(@"^\s+");
        private static Regex NonQuotedVariableName = new Regex("^(@[a-zA-Z0-9\\._\\$]+)");
        private static Regex Number;
        private static Regex Boundary;
        private static Regex ReservedTopLevelExpr;
        private static Regex Newline;
        private static Regex ReservedExpr;
        private static Regex Function;
        private static Regex Word;

        static SqlFormatter()
        {
            string boundaries = "(" + string.Join("|", Keywords.Boundaries.Select(Regex.Escape)) + ")";
            
            Number = new Regex("^([0-9]+(\\.[0-9]+)?|0x[0-9a-fA-F]+|0b[01]+)($|\\s|\"\'`|" + boundaries + ')');
            Boundary = new Regex("^(" + boundaries + ")");

            string topLevel = ("(" + string.Join("|", Keywords.ReservedToplevel.Select(Regex.Escape)) + ")").Replace(@"\ ", @"\s+");
            ReservedTopLevelExpr = new Regex("^(" + topLevel + @")($|\s|" + boundaries + ")");

            string newline = ("(" + string.Join("|", Keywords.ReservedNewline.Select(Regex.Escape)) + ")").Replace(@"\ ", @"\s+");
            Newline = new Regex("^(" + newline + @")($|\s|" + boundaries + ")");

            string reserved = "(" + string.Join("|", Keywords.Reserved.Select(Regex.Escape)) + ")";
            ReservedExpr = new Regex("^(" + reserved + @")($|\s|" + boundaries + ")");

            string function = "(" + string.Join("|", Keywords.Functions.Select(Regex.Escape)) + ")";
            Function = new Regex("^(" + function + @"[([|\s|[)])");

            Word = new Regex("^(.*?)($|\\s|[\"\'`]|" + boundaries + ")");
        }

        public static string Format(string str)
        {
            List<Token> originalTokens = Tokenize(str);

            List<Token> tokens = originalTokens
                .Where(token => token.Type != TokenType.Whitespace)
                .ToList();

            bool increaseSpecialIndent = false;
            bool increaseBlockIndent = false;
            int indentLevel = 0;

            bool newline = false;
            string result = string.Empty;
            bool addedNewline = false;
            bool inlineParentheses = false;
            bool inlineIndented = false;
            int inlineCount = 0;
            bool clauseLimit = false;

            var indentTypes = new Stack<IndentType>();

            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                string highlighted = token.Value;

                if (increaseSpecialIndent)
                {
                    indentLevel++;
                    increaseSpecialIndent = false;
                    indentTypes.Push(IndentType.Special);
                }

                if (increaseBlockIndent)
                {
                    indentLevel++;
                    increaseBlockIndent = false;
                    indentTypes.Push(IndentType.Block);
                }

                if (newline)
                {
                    result += Environment.NewLine + new string(Tab, indentLevel);
                    newline = false;
                    addedNewline = true;
                }
                else 
                {
                    addedNewline = false;
                }

                if (token.Type == TokenType.Comment || token.Type == TokenType.BlockComment)
                {
                    if (token.Type == TokenType.BlockComment)
                    {
                        var indent = new string(Tab, indentLevel);
                        result = Environment.NewLine + indent;
                        highlighted = highlighted.Replace(Environment.NewLine, Environment.NewLine + indent);
                    }

                    result += highlighted;
                    newline = true;
                    continue;
                }

                if (inlineParentheses)
                {
                    if (token.Value == ")")
                    {
                        result = result.TrimEnd(' ');

                        if (inlineIndented)
                        {
                            indentTypes.Pop();
                            indentLevel--;
                            result += Environment.NewLine + new string(Tab, indentLevel);
                        }

                        inlineParentheses = false;

                        result += highlighted + ' ';
                        continue;
                    }

                    if (token.Value == ",")
                    {
                        if (inlineCount >= 30)
                        {
                            inlineCount = 0;
                            newline = true;
                        }
                    }

                    inlineCount += token.Value.Length;
                }

                if (token.Value == "(")
                {
                    int length = 0;
                    for (int j = 1; j < 250; j++)
                    {
                        if (i + j >= tokens.Count)
                        {
                            break;
                        }

                        Token next = tokens[i + j];

                        if (next.Value == ")")
                        {
                            inlineParentheses = true;
                            inlineCount = 0;
                            inlineIndented = false;
                            break;
                        }

                        if (next.Value == ";" || next.Value == "(")
                        {
                            break;
                        }

                        if (next.Type == TokenType.ReservedTopLevel || next.Type == TokenType.ReservedNewline || next.Type == TokenType.Comment || next.Type == TokenType.BlockComment)
                        {
                            break;
                        }
                        length += next.Value.Length;
                    }

                    if (i < originalTokens.Count && originalTokens[i].Type == TokenType.Whitespace)
                    {
                        result = result.TrimEnd(' ');
                    }

                    if (!inlineParentheses)
                    {
                        increaseBlockIndent = true;
                        newline = true;
                    }
                }
                else if (token.Value == ")")
                {
                    result = result.TrimEnd(' ');
                    indentLevel--;

                    while (indentTypes.Count > 0)
                    {
                        IndentType type = indentTypes.Pop();
                        if (type == IndentType.Special)
                        {
                            indentLevel--;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (indentLevel < 0)
                    {
                        continue;
                    }

                    if (!addedNewline)
                    {
                        result += Environment.NewLine + new string(Tab, indentLevel);
                    }
                }
                else if (token.Type == TokenType.ReservedTopLevel)
                {
                    increaseSpecialIndent = true;

                    if (indentTypes.Count > 0 && indentTypes.Peek() == IndentType.Special)
                    {
                        indentLevel--;
                        indentTypes.Pop();
                    }

                    newline = true;

                    if (!addedNewline)
                    {
                        result += Environment.NewLine + new string(Tab, indentLevel);
                    }
                    else
                    {
                        result = result.TrimEnd() + new string(Tab, indentLevel);
                    }

                    if (!token.Value.Contains(' ') || !token.Value.Contains('\n') || !token.Value.Contains(Tab))
                    {
                        highlighted = Regex.Replace(highlighted, @"\s+", " ");
                    }

                    if (token.Value == "LIMIT" && !inlineParentheses)
                    {
                        clauseLimit = true;
                    }
                }
                else if (clauseLimit && token.Value != "," && token.Type != TokenType.Number && token.Type != TokenType.Whitespace)
                {
                    clauseLimit = false;
                }
                else if (token.Value == "," && !inlineParentheses)
                {
                    if (clauseLimit)
                    {
                        newline = false;
                        clauseLimit = false;
                    }
                    else
                    {
                        newline = true;
                    }
                }
                else if (token.Type == TokenType.ReservedNewline)
                {
                    if (!addedNewline)
                    {
                        result += Environment.NewLine + new string(Tab, indentLevel);
                    }

                    if (!token.Value.Contains(' ') || !token.Value.Contains('\n') || !token.Value.Contains(Tab))
                    {
                        highlighted = Regex.Replace(highlighted, @"\s+", " ");
                    }
                }
                else if (token.Type == TokenType.Boundary)
                {
                    if (i - 1 >= 0 && tokens[i - 1].Type == TokenType.Boundary)
                    {
                        if (i < originalTokens.Count && originalTokens[i - 1].Type != TokenType.Whitespace)
                        {
                            result = result.TrimEnd(' ');
                        }
                    }
                }
                
                if (token.Value == "." || token.Value == "," || token.Value == ";")
                {
                    result = result.TrimEnd(' ');
                }

                result += highlighted + ' ';

                if (token.Value == "(" || token.Value == ".")
                {
                    result = result.TrimEnd(' ');
                }

                if (token.Value == "-" && i + 1 < tokens.Count && tokens[i + 1].Type == TokenType.Number && i - 1 >= 0)
                {
                    TokenType prev = tokens[i - 1].Type;

                    if (prev != TokenType.Quote && prev != TokenType.BacktickQuote && prev != TokenType.Word && prev != TokenType.Number)
                    {
                        result = result.TrimEnd(' ');
                    }
                }
            }

            return result.Trim();
        }

        public static List<Token> Tokenize(string str)
        {
            var tokens = new List<Token>();

            int length = str.Length;

            int oldStringLength = str.Length + 1;

            Token token = null;

            int currentLength = str.Length;

            while (currentLength > 0)
            {
                if (oldStringLength <= currentLength)
                {
                    tokens.Add(new Token(TokenType.Error, str));
                    return tokens;
                }

                oldStringLength = currentLength;

                token = GetNextToken(str, token);

                tokens.Add(token);

                str = str.Substring(token.Value.Length);
                currentLength -= token.Value.Length;
            }

            return tokens;
        }

        public static IEnumerable<string> SplitQuery(string str)
        {
            var tokens = Tokenize(str);
            var queries = new List<string>();

            string currentQuery = string.Empty;
            bool empty = true;

            foreach (Token token in tokens)
            {
                if (token.Value == ";")
                {
                    if (!empty)
                    {
                        queries.Add(currentQuery + ";");
                    }
                    currentQuery = string.Empty;
                    empty = true;
                }
                else
                {
                    if (token.Type != TokenType.Whitespace && token.Type != TokenType.Comment && token.Type != TokenType.BlockComment)
                    {
                        empty = false;
                    }
                    currentQuery += token.Value;
                }

            }

            if (!empty)
            {
                queries.Add(currentQuery.Trim());
            }

            return queries;
        }

        private static Token GetNextToken(string str, Token previous = null)
        {
            Match match = Whitespace.Match(str);

            if (match.Success)
            {
                return new Token(TokenType.Whitespace, match.Groups[0].Value);
            }

            int? last = null;
            TokenType type;

            if (str[0] == '#' || (str.Length > 1 && ((str[0] == '-' && str[1] == '-') || (str[0] == '/' && str[1] == '*'))))
            {
                if (str[0] == '-' || str[0] == '#')
                {
                    last = str.IndexOf('\n');
                    type = TokenType.Comment;
                }
                else
                {
                    last = str.IndexOf("*/", 2);
                    type = TokenType.BlockComment;
                }

                if (last == null)
                {
                    last = str.Length;
                }

                return new Token(type, str);
            }

            if (str[0] == '"' || str[0] == '\'' || str[0] == '`' || str[0] == '[')
            {
                return new Token(str[0] == '`' || str[0] == '[' ? TokenType.BacktickQuote : TokenType.Quote, GetQuotedString(str));
            }

            if (str[0] == '@' && str.Length > 1)
            {
                string value = null;

                if (str[1] == '"' || str[1] == '\'' || str[1] == '`')
                {
                    value = '@' + GetQuotedString(str.Substring(1));
                }
                else
                {
                    match = NonQuotedVariableName.Match(str);

                    if (match.Success)
                    {
                        value = match.Groups[1].Value;
                    }
                }

                if (value != null)
                {
                    return new Token(TokenType.Variable, value);
                }
            }

            match = Number.Match(str);

            if (match.Success)
            {
                return new Token(TokenType.Number, match.Groups[1].Value);
            }

            match = Boundary.Match(str);

            if (match.Success)
            {
                return new Token(TokenType.Boundary, match.Groups[1].Value);
            }

            string upper = str.ToUpper();

            if (previous == null || previous.Value != ".")
            {
                match = ReservedTopLevelExpr.Match(upper);

                if (match.Success)
                {
                    return new Token(TokenType.ReservedTopLevel, match.Groups[1].Value);
                }

                match = Newline.Match(upper);

                if (match.Success)
                {
                    return new Token(TokenType.ReservedNewline, match.Groups[1].Value);
                }

                match = ReservedExpr.Match(upper);

                if (match.Success)
                {
                    return new Token(TokenType.Reserved, match.Groups[1].Value);
                }
            }
            match = Function.Match(upper);

            if (match.Success)
            {
                string matchValue = match.Groups[1].Value;

                return new Token(TokenType.Reserved, matchValue.Substring(0, matchValue.Length - 1));
            }

            match = Word.Match(str);

            return new Token(TokenType.Word, match.Groups[1].Value);

        }

        private static string GetQuotedString(string str)
        {
            Match match = Regex.Match(str, "^(((`[^`]*($|`))+)|((\\[[^\\]]*($|\\]))(\\][^\\]]*($|\\]))*)|((\"[^\"\\\\]*(?:\\\\.[^\"\\\\]*)*(\"|$))+)|((\'[^\'\\\\]*(?:\\\\.[^\'\\\\]*)*(\'|$))+))");
            string result = null;

            if (match != null)
            {
                result = match.Value;
            }

            return result;
        }
    }
}
