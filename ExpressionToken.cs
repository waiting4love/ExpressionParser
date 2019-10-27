using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpressionParser
{
    public class Range
    {
        public int Begin { get; internal set; }
        public int End { get; internal set; }
    }
    public class Token
    {
        public Range Range { get; internal set; }
        public Parser.TokenType Type { get; internal set; }
        public List<Token> Childs { get; internal set; }
    }
    partial class Parser
    {
        public enum TokenType {
            CONST, VARIABLE,
            GROUP, FUNCTION, FACTOR,
            TERM, FACTORMUL, FACTORDIV,
            EXP, TERMADD, TERMSUB
        };

        public Token BuildToken(string exp)
        {
            if (exp == null)
            {
                throw new ArgumentNullException(nameof(exp));
            }
            return GetExp(exp, SkipWS(exp, 0));
        }

        private Token GetExp(string exp, int begin)
        {
            // term >> ('+' >> term | '-' >> term) *
            int len = exp.Length;
            int gpos = begin;
            Token t = GetTerm(exp, gpos);
            if (t == null) return null;

            Token res = new Token()
            {
                Type = TokenType.EXP,
                Range = t.Range
            };
            List<Token> childs = new List<Token>() { t };

            gpos = t.Range.End;
            int end = gpos;
            while (gpos < len)
            {
                gpos = SkipWS(exp, gpos);
                if (gpos >= len) break;
                char c = exp[gpos];
                if (c != '+' && c != '-') break;
                ++gpos;
                Token tt = GetTerm(exp, SkipWS(exp, gpos));
                if (tt == null) break;
                tt.Type = c == '+' ? TokenType.TERMADD : TokenType.TERMSUB;
                childs.Add(tt);
                gpos = tt.Range.End;
                end = gpos;  // 保存位置，循环中退出时这个位置就是最终位置
            }
            res.Childs = childs;
            res.Range.End = end;
            return res;
        }

        private Token GetTerm(string exp, int begin)
        {
            //- factor >> ('*' >> factor | '/' >> factor)*
            int len = exp.Length;
            int gpos = begin;
            Token t = GetFactor(exp, gpos);
            if (t == null) return null;

            Token res = new Token()
            {
                Type = TokenType.TERM,
                Range = t.Range
            };
            List<Token> childs = new List<Token>() { t };

            gpos = t.Range.End;
            int end = gpos;
            while (gpos < len)
            {
                gpos = SkipWS(exp, gpos);
                if (gpos >= len) break;
                char c = exp[gpos];
                if (c != '*' && c != '/') break;
                ++gpos;
                Token tt = GetFactor(exp, SkipWS(exp, gpos));
                if (tt == null) break;
                tt.Type = c == '*' ? TokenType.FACTORMUL : TokenType.FACTORDIV;
                childs.Add(tt);
                gpos = tt.Range.End;
                end = gpos;  // 保存位置，循环中退出时这个位置就是最终位置
            }
            res.Childs = childs;
            res.Range.End = end;
            return res;
        }

        private Token GetFactor(string exp, int begin)
        {
            // const | -?(variable | group | function)
            int len = exp.Length;
            if (begin >= len) return null;

            Token t = null;
            if (t == null)
            {
                t = GetFunction(exp, begin);
            }
            if (t == null)
            {
                t = GetGroup(exp, begin);
            }
            if (t == null)
            {
                t = GetVariable(exp, begin);
            }
            if (t == null)
            {
                t = GetConst(exp, begin);
            }
            if (t == null)
            {
                if(exp[begin] == '-')
                {
                    t = GetFactor(exp, begin + 1);
                    if (t != null)
                    {
                        t.Range.Begin = begin;
                    }
                }
            }
            if (t == null)
            {
                return null;
            }
            Token res = new Token()
            {
                Type = TokenType.FACTOR,
                Childs = new List<Token> { t },
                Range = new Range {Begin=begin, End=t.Range.End}
            };
            return res;
        }

        private static Range ReadNumber(string exp, int begin)
        {
            Range intRange = ReadInteger(exp, begin);
            if (intRange.Begin == intRange.End) return intRange;
            Range fraRange = ReadFraction(exp, intRange.End); // Fraction允许空
            Range expRange = ReadExponent(exp, fraRange.End); // Exponent允许空
            return new Range { Begin = begin, End = expRange.End };
        }

        private static Range ReadExponent(string exp, int begin)
        {
            // 'E'|'e' >> sign >> digits
            Range res = new Range() { Begin = begin, End = begin };
            int len = exp.Length;
            if (begin >= len) return res;
            if(Char.ToUpper(exp[begin], CultureInfo.InvariantCulture) == 'E' && begin < len - 1)
            {
                int gpos = begin + 1;
                if (exp[gpos] == '-' || exp[gpos] == '+') gpos++;
                if (gpos < len && Char.IsDigit(exp[gpos]))
                {
                    while (gpos < len && Char.IsDigit(exp[gpos])) gpos++;
                    res.End = gpos;
                }
            }
            return res;
        }

        private static Range ReadFraction(string exp, int begin)
        {
            // '.' digits
            Range res = new Range() { Begin = begin, End = begin };
            int len = exp.Length;
            if (begin >= len) return res;
            if (exp[begin] == '.')
            {
                int gpos = begin + 1;
                while (gpos < len && Char.IsDigit(exp[gpos])) gpos++;
                res.End = gpos;
            }
            return res;
        }

        private static Range ReadInteger(string exp, int begin)
        {
            //- digit
            //- onenine >> digit*
            //- '-' digit
            //- '-' onenine >> digit*
            Range res = new Range() { Begin = begin, End = begin };
            int len = exp.Length;
            if (begin >= len) return res;
            int gpos = begin;
            if (exp[gpos] == '-') gpos++;
            if(gpos < len && Char.IsDigit(exp[gpos]))
            {
                if(exp[gpos] == '0')
                {
                    res.End = gpos + 1;
                }
                else
                {
                    while (gpos < len && Char.IsDigit(exp[gpos])) gpos++;
                    res.End = gpos;
                }
            }
            return res;
        }

        private static Range ReadVeriable(string exp, int begin)
        {
            Range res = new Range() { Begin = begin, End = begin };
            int len = exp.Length;
            if (begin >= len) return res;
            if(exp[begin] == '_' || Char.IsLetter(exp[begin]))
            {
                int gpos = begin + 1;
                while (gpos < len && (Char.IsLetterOrDigit(exp[gpos]) || exp[gpos] == '_')) gpos++;
                res.End = gpos;
            }
            return res;
        }

        private static Token GetConst(string exp, int begin)
        {
            var num = ReadNumber(exp, begin);
            if (num.Begin == num.End) return null;
            return new Token()
            {
                Type = TokenType.CONST,
                Range = num
            };
        }

        private static Token GetVariable(string exp, int begin)
        {
            var variable = ReadVeriable(exp, begin);
            if (variable.Begin == variable.End) return null;
            return new Token()
            {
                Type = TokenType.VARIABLE,
                Range = variable
            };
        }
        private Token GetGroup(string exp, int begin)
        {
            // '(' >> exp >> ')'
            if (begin >= exp.Length) return null;

            if (exp[begin] != '(') return null;
            var t = GetExp(exp, SkipWS(exp, begin + 1));
            if (t == null) return null;

            int gpos = SkipWS(exp, t.Range.End);
            if (gpos >= exp.Length || exp[gpos] != ')' ) return null;

            return new Token()
            {
                Type = TokenType.GROUP,
                Childs = new List<Token>() { t },
                Range = new Range { Begin = begin, End = gpos + 1 }
            };
        }

        // function.childs: variable, exp, exp, exp...
        private Token GetFunction(string exp, int begin)
        {
            // variable >> '(' >> (exp >> (',' >> exp)*)? >> ')'
            int len = exp.Length;
            if (begin >= len) return null;

            var varRange = ReadVeriable(exp, begin);
            if (varRange.Begin == varRange.End) return null;

            // todo: check FunctionTable here
            int gpos = SkipWS(exp, varRange.End);
            if (gpos >= len || exp[gpos++] != '(' ) return null;

            Token res = new Token()
            {
                Type = TokenType.FUNCTION,
                Range = new Range { Begin = begin },
                Childs = new List<Token>()
                {
                    new Token() { Type = TokenType.VARIABLE, Range = varRange }
                }
            };

            gpos = SkipWS(exp, gpos);
            var exp1 = GetExp(exp, gpos);
            if (exp1 != null)
            {
                res.Childs.Add(exp1);
                gpos = SkipWS(exp, exp1.Range.End);
                while (gpos < len - 1 && exp[gpos] == ',')
                {
                    gpos = SkipWS(exp, gpos + 1);
                    var exp2 = GetExp(exp, gpos);
                    if (exp2 == null) return null;
                    res.Childs.Add(exp2);
                    gpos = SkipWS(exp, exp2.Range.End);
                }
            }

            // todo: check childs number and FunctionTable define
            if (gpos >= len || exp[gpos] != ')' ) return null;
            res.Range.End = gpos + 1;
            return res;            
        }

        private static int SkipWS(string exp, int begin)
        {
            while (begin < exp.Length && Char.IsWhiteSpace(exp[begin])) begin++;
            return begin;
        }
    }
}
