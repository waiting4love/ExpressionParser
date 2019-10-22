using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpressionParser
{
    partial class Parser
    {
        public enum TokenType {
            CONST, VARIABLE,
            GROUP, FUNCTION, FACTOR,
            TERM, FACTOR_MUL, FACTOR_DIV,
            EXP, TERM_ADD, TERM_SUB
        };
        public struct Range
        {
            public int begin;
            public int end;
        }
        public class Token
        {
            public Range range;
            public TokenType type;
            public List<Token> childs;
        }

        public Token BuildToken(string exp)
        {
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
                type = TokenType.EXP,
                range = t.range
            };
            List<Token> childs = new List<Token>() { t };

            gpos = t.range.end;
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
                tt.type = c == '+' ? TokenType.TERM_ADD : TokenType.TERM_SUB;
                childs.Add(tt);
                gpos = tt.range.end;
                end = gpos;  // 保存位置，循环中退出时这个位置就是最终位置
            }
            res.childs = childs;
            res.range.end = end;
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
                type = TokenType.TERM,
                range = t.range
            };
            List<Token> childs = new List<Token>() { t };

            gpos = t.range.end;
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
                tt.type = c == '*' ? TokenType.FACTOR_MUL : TokenType.FACTOR_DIV;
                childs.Add(tt);
                gpos = tt.range.end;
                end = gpos;  // 保存位置，循环中退出时这个位置就是最终位置
            }
            res.childs = childs;
            res.range.end = end;
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
                        t.range.begin = begin;
                    }
                }
            }
            if (t == null)
            {
                return null;
            }
            Token res = new Token()
            {
                type = TokenType.FACTOR,
                childs = new List<Token> { t },
                range = {begin=begin, end=t.range.end}
            };
            return res;
        }

        private Range ReadNumber(string exp, int begin)
        {
            Range intRange = ReadInteger(exp, begin);
            if (intRange.begin == intRange.end) return intRange;
            Range fraRange = ReadFraction(exp, intRange.end); // Fraction允许空
            Range expRange = ReadExponent(exp, fraRange.end); // Exponent允许空
            return new Range() { begin = begin, end = expRange.end };
        }

        private Range ReadExponent(string exp, int begin)
        {
            // 'E'|'e' >> sign >> digits
            Range res = new Range() { begin = begin, end = begin };
            int len = exp.Length;
            if (begin >= len) return res;
            if(Char.ToUpper(exp[begin]) == 'E' && begin < len - 1)
            {
                int gpos = begin + 1;
                if (exp[gpos] == '-' || exp[gpos] == '+') gpos++;
                if (gpos < len && Char.IsDigit(exp[gpos]))
                {
                    while (gpos < len && Char.IsDigit(exp[gpos])) gpos++;
                    res.end = gpos;
                }
            }
            return res;
        }

        private Range ReadFraction(string exp, int begin)
        {
            // '.' digits
            Range res = new Range() { begin = begin, end = begin };
            int len = exp.Length;
            if (begin >= len) return res;
            if (exp[begin] == '.')
            {
                int gpos = begin + 1;
                while (gpos < len && Char.IsDigit(exp[gpos])) gpos++;
                res.end = gpos;
            }
            return res;
        }

        private Range ReadInteger(string exp, int begin)
        {
            //- digit
            //- onenine >> digit*
            //- '-' digit
            //- '-' onenine >> digit*
            Range res = new Range() { begin = begin, end = begin };
            int len = exp.Length;
            if (begin >= len) return res;
            int gpos = begin;
            if (exp[gpos] == '-') gpos++;
            if(gpos < len && Char.IsDigit(exp[gpos]))
            {
                if(exp[gpos] == '0')
                {
                    res.end = gpos + 1;
                }
                else
                {
                    while (gpos < len && Char.IsDigit(exp[gpos])) gpos++;
                    res.end = gpos;
                }
            }
            return res;
        }

        private Range ReadVeriable(string exp, int begin)
        {
            Range res = new Range() { begin = begin, end = begin };
            int len = exp.Length;
            if (begin >= len) return res;
            if(exp[begin] == '_' || Char.IsLetter(exp[begin]))
            {
                int gpos = begin + 1;
                while (gpos < len && (Char.IsLetterOrDigit(exp[gpos]) || exp[gpos] == '_')) gpos++;
                res.end = gpos;
            }
            return res;
        }

        private Token GetConst(string exp, int begin)
        {
            var num = ReadNumber(exp, begin);
            if (num.begin == num.end) return null;
            return new Token()
            {
                type = TokenType.CONST,
                range = num
            };
        }

        private Token GetVariable(string exp, int begin)
        {
            var variable = ReadVeriable(exp, begin);
            if (variable.begin == variable.end) return null;
            return new Token()
            {
                type = TokenType.VARIABLE,
                range = variable
            };
        }
        private Token GetGroup(string exp, int begin)
        {
            // '(' >> exp >> ')'
            if (begin >= exp.Length) return null;

            if (exp[begin] != '(') return null;
            var t = GetExp(exp, SkipWS(exp, begin + 1));
            if (t == null) return null;

            int gpos = SkipWS(exp, t.range.end);
            if (gpos >= exp.Length || exp[gpos] != ')' ) return null;

            return new Token()
            {
                type = TokenType.GROUP,
                childs = new List<Token>() { t },
                range = { begin = begin, end = gpos + 1 }
            };
        }

        // function.childs: variable, exp, exp, exp...
        private Token GetFunction(string exp, int begin)
        {
            // variable >> '(' >> exp >> (',' >> exp)* >> ')'
            int len = exp.Length;
            if (begin >= len) return null;

            var varRange = ReadVeriable(exp, begin);
            if (varRange.begin == varRange.end) return null;

            // check FunctionTable here

            int gpos = SkipWS(exp, varRange.end);
            if (gpos >= len || exp[gpos++] != '(' ) return null;

            var exp1 = GetExp(exp, SkipWS(exp, gpos));
            if (exp1 == null) return null;

            Token res = new Token()
            {
                type = TokenType.FUNCTION,
                range = { begin = begin },
                childs = new List<Token>() {
                    new Token() { type = TokenType.VARIABLE, range = varRange },
                    exp1
                }
            };

            gpos = SkipWS(exp, exp1.range.end);
            while (gpos < len - 1 && exp[gpos] == ',')
            {
                gpos = SkipWS(exp, gpos + 1);
                var exp2 = GetExp(exp, gpos);
                if (exp2 == null) return null;
                res.childs.Add(exp2);
                gpos = SkipWS(exp, exp2.range.end);
            }

            // check childs number and FunctionTable define

            if (gpos >= len || exp[gpos] != ')' ) return null;
            res.range.end = gpos + 1;
            return res;            
        }

        private int SkipWS(string exp, int begin)
        {
            while (begin < exp.Length && Char.IsWhiteSpace(exp[begin])) begin++;
            return begin;
        }
    }
}
