using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpressionParser
{
    public partial class Parser
    {
        public IOperator Parse(string exp)
        {
            var token = BuildToken(exp);
            if (token == null || token.Range.End != exp.Length) return null;
            return BuildOperator(exp, token);
        }

        public IOperator BuildOperator(string exp, Token token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }
            if (exp == null)
            {
                throw new ArgumentNullException(nameof(exp));
            }
            switch (token.Type)
            {
                case TokenType.CONST:
                    return BuildConst(exp, token);
                case TokenType.EXP:
                    return BuildExp(exp, token);
                case TokenType.FACTOR:
                case TokenType.FACTORDIV:
                case TokenType.FACTORMUL:
                    return BuildFactor(exp, token);
                case TokenType.FUNCTION:
                    return BuildFunction(exp, token);
                case TokenType.GROUP:
                    return BuildGroup(exp, token);
                case TokenType.TERM:
                case TokenType.TERMADD:
                case TokenType.TERMSUB:
                    return BuildTerm(exp, token);
                case TokenType.VARIABLE:
                    return BuildVariable(exp, token);
                default:
                    break;
            }
            return null;
        }

        private static IOperator BuildVariable(string exp, Token token)
        {
            return new OpVar(exp.Substring(token.Range.Begin, token.Range.End - token.Range.Begin));
        }

        private IOperator BuildTerm(string exp, Token token)
        {
            var ops = token.Childs.Select(term =>
            {
                var op = BuildFactor(exp, term);
                if (term.Type == TokenType.FACTORDIV)
                {
                    op = new OpInv(op);
                }
                return op;
            });

            if (ops.All(op => op != null))
            {
                var oparr = ops.ToArray();
                return oparr.Length == 1? oparr[0] : new OpMul(oparr);
            }
            else
            {
                return null;
            }
        }

        private IOperator BuildGroup(string exp, Token token)
        {
            return BuildExp(exp, token.Childs[0]);
        }

        private IOperator BuildFunction(string exp, Token token)
        {
            var nametk = token.Childs[0];
            var namestr = exp.Substring(nametk.Range.Begin, nametk.Range.End - nametk.Range.Begin);
            if(FunctionTable.TryGetValue(namestr, out Func<double[], double> func))
            {
                var argsLen = token.Childs.Count - 1;
                IOperator[] ops = new IOperator[argsLen];
                for(int i=0; i<argsLen;i++)
                {
                    ops[i] = BuildExp(exp, token.Childs[i+1]);
                }
                return new OpFun(namestr, ops, func);
            }
            return null;
        }

        private IOperator BuildFactor(string exp, Token token)
        {
            var res = BuildOperator(exp, token.Childs[0]);
            if (token.Range.Begin < token.Childs[0].Range.Begin && exp[token.Range.Begin] == '-')
            {
                res = MakeNeg(res);
            }
            return res;
        }

        private static IOperator MakeNeg(IOperator res)
        {
            if(res is OpNeg ng)
            {
                return ng.Child; // 负负得正
            }
            else if(res is OpConst cn) // 直接数字
            {
                return new OpConst(-cn.Calc(null));
            }
            else
            {
                return new OpNeg(res);
            }
        }

        private IOperator BuildExp(string exp, Token token)
        {
            var ops = token.Childs.Select(term =>
               {
                   var op = BuildTerm(exp, term);
                   if (term.Type == TokenType.TERMSUB)
                   {
                       op = MakeNeg(op);
                   }
                   return op;
               });

            if (ops.All(op => op != null))
            {
                var oparr = ops.ToArray();
                return oparr.Length == 1? oparr[0] : new OpAdd(oparr);
            }
            else
            {
                return null;
            }
        }

        private static IOperator BuildConst(string exp, Token token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }
            if (exp == null)
            {
                throw new ArgumentNullException(nameof(exp));
            }
            return new OpConst(
                Convert.ToDouble(
                    exp.Substring(token.Range.Begin, token.Range.End - token.Range.Begin),
                    CultureInfo.InvariantCulture
                    )
                );
        }
    }
}
