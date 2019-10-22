using System;
using System.Collections.Generic;
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
            if (token == null || token.range.end != exp.Length) return null;
            return BuildOperator(exp, token);
        }

        public IOperator BuildOperator(string exp, Token token)
        {
            switch(token.type)
            {
                case TokenType.CONST:
                    return BuildConst(exp, token);
                case TokenType.EXP:
                    return BuildExp(exp, token);
                case TokenType.FACTOR:
                case TokenType.FACTOR_DIV:
                case TokenType.FACTOR_MUL:
                    return BuildFactor(exp, token);
                case TokenType.FUNCTION:
                    return BuildFunction(exp, token);
                case TokenType.GROUP:
                    return BuildGroup(exp, token);
                case TokenType.TERM:
                case TokenType.TERM_ADD:
                case TokenType.TERM_SUB:
                    return BuildTerm(exp, token);
                case TokenType.VARIABLE:
                    return BuildVariable(exp, token);
            }
            return null;
        }

        private IOperator BuildVariable(string exp, Token token)
        {
            return new OpVar(exp.Substring(token.range.begin, token.range.end - token.range.begin));
        }

        private IOperator BuildTerm(string exp, Token token)
        {
            var ops = token.childs.Select(term =>
            {
                var op = BuildFactor(exp, term);
                if (term.type == TokenType.FACTOR_DIV)
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
            return BuildExp(exp, token.childs[0]);
        }

        private IOperator BuildFunction(string exp, Token token)
        {
            var nametk = token.childs[0];
            var namestr = exp.Substring(nametk.range.begin, nametk.range.end - nametk.range.begin);
            if(FunctionTable.TryGetValue(namestr, out Func<double[], double> func))
            {
                var argsLen = token.childs.Count - 1;
                IOperator[] ops = new IOperator[argsLen];
                for(int i=0; i<argsLen;i++)
                {
                    ops[i] = BuildExp(exp, token.childs[i+1]);
                }
                return new OpFun(namestr, ops, func);
            }
            return null;
        }

        private IOperator BuildFactor(string exp, Token token)
        {
            var res = BuildOperator(exp, token.childs[0]);
            if (token.range.begin < token.childs[0].range.begin && exp[token.range.begin] == '-')
            {
                res = MakeNeg(res);
            }
            return res;
        }

        private IOperator MakeNeg(IOperator res)
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
            var ops = token.childs.Select(term =>
               {
                   var op = BuildTerm(exp, term);
                   if (term.type == TokenType.TERM_SUB)
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

        private IOperator BuildConst(string exp, Token token)
        {
            return new OpConst(Convert.ToDouble(exp.Substring(token.range.begin, token.range.end - token.range.begin)));
        }
    }
}
