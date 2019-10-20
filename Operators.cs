using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpressionParser
{
    public interface IOperator
    {
        double Calc(double[] input);
        string[] GetKeys();
    }

    abstract class OpBase : IOperator
    {
        string[] _keys;
        Tuple<IOperator, int[]>[] _operators;

        public OpBase(IOperator[] input)
        {
            IEnumerable<string> keys = new string[0];
            foreach (var i in input)
            {
                keys = keys.Concat(i.GetKeys());
            }

            _keys = keys.Distinct().ToArray();

            _operators = input.Select(
                    i => Tuple.Create(i, i.GetKeys().Select(k => Array.IndexOf(_keys, k)).ToArray())
                ).ToArray();
        }
        public double Calc(double[] param)
        {
            var midres = _operators.Select(op => op.Item1.Calc(op.Item2.Select(i => param[i]).ToArray())).ToArray();
            return DoCalc(midres);
        }

        public abstract double DoCalc(double[] midres);

        public string[] GetKeys()
        {
            return _keys;
        }
    }

    class OpAdd : OpBase
    {
        public OpAdd(IOperator[] input) : base(input)
        {
        }

        public override double DoCalc(double[] midres)
        {
            return midres.Aggregate((a, b) => a + b);
        }
    }

    class OpSub : OpBase
    {
        public OpSub(IOperator[] input) : base(input)
        {
        }

        public override double DoCalc(double[] midres)
        {
            return midres.Aggregate((a, b) => a - b);
        }
    }

    class OpMul : OpBase
    {
        public OpMul(IOperator[] input) : base(input)
        {
        }

        public override double DoCalc(double[] midres)
        {
            return midres.Aggregate((a, b) => a * b);
        }
    }

    class OpDiv : OpBase
    {
        public OpDiv(IOperator[] input) : base(input)
        {
        }

        public override double DoCalc(double[] midres)
        {
            return midres.Aggregate((a, b) => a / b);
        }
    }

    class OpFun : OpBase
    {
        Func<double[], double> _func;
        public OpFun(IOperator[] input, Func<double[], double> func) : base(input)
        {
            _func = func;
        }

        public override double DoCalc(double[] midres)
        {
            return _func(midres);
        }
    }

    class OpConst : IOperator
    {
        double _v;
        static string[] _keys = new string[0];
        public OpConst(double v)
        {
            _v = v;
        }
        public double Calc(double[] input)
        {
            return _v;
        }

        public string[] GetKeys()
        {
            return _keys;
        }
    }

    class OpVar : IOperator
    {
        string[] _keys;
        public OpVar(string name)
        {
            _keys = new string[] { name };
        }

        public double Calc(double[] input)
        {
            return input[0];
        }

        public string[] GetKeys()
        {
            return _keys;
        }
    }

    class OpMinus : IOperator
    {
        IOperator _op;
        public OpMinus(IOperator op)
        {
            _op = op;
        }
        public double Calc(double[] input)
        {
            return -_op.Calc(input);
        }

        public string[] GetKeys()
        {
            return _op.GetKeys();
        }
    }
    class OpInv : IOperator
    {
        IOperator _op;
        public OpInv(IOperator op)
        {
            _op = op;
        }
        public double Calc(double[] input)
        {
            return 1.0 / _op.Calc(input);
        }

        public string[] GetKeys()
        {
            return _op.GetKeys();
        }
    }
}
