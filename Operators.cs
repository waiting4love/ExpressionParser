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
            if (add_items.Count() > 0)
            {
                var childs = add_items.Select(p => ((OpAdd)p.Item1)._operators).Aggregate((a, b) => a.Concat(b));
                if (childs.Count() > 0)
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
                var tmp = _operators.ToList();
                tmp.Add(Tuple.Create(op, new int[0]));
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
                        res = res + s;
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
            if (add_items.Count() > 0)
            {
                var childs = add_items.Select(p => ((OpMul)p.Item1)._operators).Aggregate((a, b) => a.Concat(b));
                if (childs.Count() > 0)
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
                    var tmp = _operators.ToList();
                    tmp.Add(Tuple.Create(op, new int[0]));
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
                        res = res + s;
                    }
                    else
                    {
                        res = res + "*" + s;
                    }
                }
            }
            if (res.Length > 0 && res[0] == '/') res = '1' + res;
            return res;
        }
    }

    class OpFun : OpBase
    {
        string _name;
        Func<double[], double> _func;
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
                        res = res + s;
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
        double _v;
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
            return _v.ToString();
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

        public string GetString()
        {
            return _keys[0];
        }

        public void Optimize()
        {            
        }
    }

    class OpNeg : IOperator
    {
        IOperator _op;
        public IOperator Child => _op;
        public OpNeg(IOperator op)
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

        public string GetString()
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

        public void Optimize()
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
        public void Optimize()
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
        public string GetString()
        {
            var s = _op.GetString();
            if (s.Length > 0)
            {
                if (_op is OpAdd || _op is OpMul)
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
