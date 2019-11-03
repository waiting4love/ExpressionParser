using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpressionParser
{
    public interface IOperator
    {
        double Calc(double[] input);
        string[] GetKeys();
        void Optimize(); // 尽量减少节点数
        string GetString(); // 转换回字符串
    }

    abstract class OpBase : IOperator
    {
        protected string[] _keys;
        protected IEnumerable<Tuple<IOperator, int[]>> _operators;

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
            // if (param.Length < _keys.Length) return Double.NaN;
            var midres = _operators.Select(op => op.Item1.Calc(op.Item2.Select(i => param[i]).ToArray())).ToArray();
            return DoCalc(midres);
        }

        public abstract double DoCalc(double[] midres);

        public string[] GetKeys()
        {
            return _keys;
        }

        public virtual void Optimize()
        {
            // 如果子数据不需要key且不是OpConst，直接替换成OpConst
            _operators = _operators.Select(p =>
            {
                if (p.Item2.Length == 0 && !(p.Item1 is OpConst))
                {
                    IOperator op = new OpConst(p.Item1.Calc(null));
                    return Tuple.Create(op, new int[0]);
                }
                else
                {
                    p.Item1.Optimize();
                    return p;
                }
            });
        }

        public abstract string GetString();
    }

    class OpAdd : OpBase
    {
        public OpAdd(IOperator[] input) : base(input)
        {
        }

        public override double DoCalc(double[] midres)
        {
            return midres.Length == 0? 0: midres.Aggregate((a, b) => a + b);
        }

        public override void Optimize()
        {
            base.Optimize();
            // 遍历所有子项，如果有OpAdd的，把这个子项的所有子项提到当前
            var add_items = _operators.Where(p => p.Item1 is OpAdd);
            if (add_items.Any())
            {
                var childs = add_items.Select(p => ((OpAdd)p.Item1)._operators).Aggregate((a, b) => a.Concat(b));
                if (childs.Any())
                {
                    _operators = _operators.Where(p => !(p.Item1 is OpAdd)).Concat(childs);
                    // 重建变量位置信息
                    _operators = _operators.Select(
                        i => Tuple.Create(i.Item1, i.Item1.GetKeys().Select(k => Array.IndexOf(_keys, k)).ToArray())
                    );
                }
            }
            // 所有的Const的数合并成一个
            double v = DoCalc(_operators.Where(p => p.Item1 is OpConst).Select(p => p.Item1.Calc(null)).ToArray());
            _operators = _operators.Where(p => !(p.Item1 is OpConst));
            if (v != 0)
            {
                IOperator op = new OpConst(v);
                var tmp = new List<Tuple<IOperator, int[]>>() { Tuple.Create(op, new int[0]) };
                tmp.AddRange(_operators);
                _operators = tmp;
            }
        }

        public override string GetString()
        {
            string res = "";
            foreach(var p in _operators)
            {
                string s = p.Item1.GetString();
                if (s.Length > 0)
                {                    
                    if (res.Length ==0 || s[0] == '-')
                    {
                        res += s;
                    }
                    else
                    {
                        res = res + "+" + s;
                    }
                }
            }
            return res;
        }
    }

    class OpMul : OpBase
    {
        public OpMul(IOperator[] input) : base(input)
        {
        }

        public override double DoCalc(double[] midres)
        {
            return midres.Length == 0 ? 1 : midres.Aggregate((a, b) => a * b);
        }
        public override void Optimize()
        {
            base.Optimize();
            // 遍历所有子项，如果有OpMul的，把这个子项的所有子项提到当前
            var add_items = _operators.Where(p => p.Item1 is OpMul);
            if (add_items.Any())
            {
                var childs = add_items.Select(p => ((OpMul)p.Item1)._operators).Aggregate((a, b) => a.Concat(b));
                if (childs.Any())
                {
                    _operators = _operators.Where(p => !(p.Item1 is OpMul)).Concat(childs);
                    // 重建变量位置信息
                    _operators = _operators.Select(
                        i => Tuple.Create(i.Item1, i.Item1.GetKeys().Select(k => Array.IndexOf(_keys, k)).ToArray())
                    );
                }
            }
            // 所有的Const的数合并成一个
            double v = DoCalc(_operators.Where(p => p.Item1 is OpConst).Select(p => p.Item1.Calc(null)).ToArray());
            if (v != 0)
            {
                _operators = _operators.Where(p => !(p.Item1 is OpConst));
                if (v != 1)
                {
                    IOperator op = new OpConst(v);
                    var tmp = new List<Tuple<IOperator, int[]>>() { Tuple.Create(op, new int[0]) };
                    tmp.AddRange(_operators);
                    _operators = tmp;
                }
            }
            else // 和0相乘
            {
                // 结果为0，_operators不能空，否则不方便转成字符串
                var tmp = new Tuple<IOperator, int[]>[1];
                tmp[0] = Tuple.Create( (IOperator)(new OpConst(0)), new int[0] );
                _operators = tmp;
                _keys = OpConst._keys;
            }
        }
        public override string GetString()
        {
            string res = "";
            foreach (var p in _operators)
            {
                string s = p.Item1.GetString();
                if (s.Length > 0)
                {
                    if(p.Item1 is OpAdd)
                    {
                        s = '(' + s + ')';
                    }

                    if (res.Length == 0 || s[0] == '/')
                    {
                        res += s;
                    }
                    else
                    {
                        res += "*" + s;
                    }
                }
            }
            if (res.Length > 0 && res[0] == '/') res = '1' + res;
            return res;
        }
    }

    class OpFun : OpBase
    {
        readonly string _name;
        readonly Func<double[], double> _func;
        public OpFun(string name, IOperator[] input, Func<double[], double> func) : base(input)
        {
            _func = func;
            _name = name;
        }

        public override double DoCalc(double[] midres)
        {
            return _func(midres);
        }

        public override string GetString()
        {
            string res = "";
            foreach (var p in _operators)
            {
                string s = p.Item1.GetString();
                if (s.Length > 0)
                {
                    if (s.Length > 0 && s[0] == '/') s = '1' + s;
                    if (res.Length == 0)
                    {
                        res += s;
                    }
                    else
                    {
                        res = res + "," + s;
                    }
                }
            }
            
            return _name + "(" + res + ")";
        }
    }

    class OpConst : IOperator
    {
        readonly double _v;
        public static string[] _keys = new string[0];
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

        public void Optimize()
        {

        }
        public string GetString()
        {
            return _v.ToString(CultureInfo.InvariantCulture);
        }
    }

    class OpVar : IOperator
    {
        readonly string[] _keys;
        public OpVar(string name)
        {
            _keys = new string[] { name };
        }

        public double Calc(double[] input)
        {
            // return input.Length>0? input[0] : double.NaN;
            return input[0];
        }

        public string[] GetKeys()
        {
            return _keys;
        }

        public string GetString()
        {
            return _keys[0];
        }

        public void Optimize()
        {            
        }
    }

    abstract class OpSingle : IOperator
    {
        protected IOperator _op;

        public IOperator Child => _op;
        public IOperator TermChild
        {
            get
            {
                var res = Child;
                while(res is OpSingle t)
                {
                    res = t.Child;
                }
                return res;
            }
        }
        public abstract double Calc(double[] input);
        public abstract string[] GetKeys();
        public abstract void Optimize(); // 尽量减少节点数
        public abstract string GetString(); // 转换回字符串
    }

    class OpCheckParam : OpSingle
    {
        public OpCheckParam(IOperator op)
        {
            _op = op;
        }
        public override double Calc(double[] input)
        {
            var keys = _op.GetKeys();
            var num = input == null ? 0 : input.Length;
            if (num < keys.Length) return Double.NaN;
            return _op.Calc(input);
        }

        public override string[] GetKeys()
        {
            return _op.GetKeys();
        }

        public override string GetString()
        {
            return _op.GetString();
        }

        public override void Optimize()
        {
            _op.Optimize();
        }
    }
    class OpNeg : OpSingle
    {
        public OpNeg(IOperator op)
        {
            _op = op;
        }
        public override double Calc(double[] input)
        {
            return -_op.Calc(input);
        }

        public override string[] GetKeys()
        {
            return _op.GetKeys();
        }

        public override string GetString()
        {
            var s = _op.GetString();
            if(s.Length > 0)
            {
                if (_op is OpAdd)
                {
                    s = '(' + s + ')';
                }
                if (s[0] != '-')
                    s = "-" + s;
                else
                    s = s.Substring(1);  // 负负得正
            }
            return s;
        }

        public override void Optimize()
        {
            if(_op.GetKeys().Length == 0)
            {
                _op = new OpConst(_op.Calc(null));
            }
            else
            {
                _op.Optimize();
            }
        }
    }
    class OpInv : OpSingle
    {
        public OpInv(IOperator op)
        {
            _op = op;
        }
        public override double Calc(double[] input)
        {
            return 1.0 / _op.Calc(input);
        }

        public override string[] GetKeys()
        {
            return _op.GetKeys();
        }
        public override void Optimize()
        {
            if (_op.GetKeys().Length == 0)
            {
                _op = new OpConst(_op.Calc(null));
            }
            else
            {
                _op.Optimize();
            }
        }
        public override string GetString()
        {
            var s = _op.GetString();
            if (s.Length > 0)
            {
                if (_op is OpAdd || TermChild is OpMul) // 由于OpNeg的子项为Mul时不加括号，这里要考虑
                {
                    s = '(' + s + ')';
                }

                if (s[0] != '/')
                    s = "/" + s;
                else
                    s = s.Substring(1);  // 负负得正
            }
            return s;
        }
    }
}
