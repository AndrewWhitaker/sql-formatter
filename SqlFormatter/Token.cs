namespace SqlFormatter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class Token
    {
        public Token(TokenType type, string value)
        {
            this.Type = type;
            this.Value = value;
        }

        public TokenType Type { get; private set; }

        public string Value { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}, \"{1}\"", this.Type, this.Value);
        }
    }
}
