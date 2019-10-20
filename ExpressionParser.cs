using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpressionParser
{
    public interface IFunction
    {
        double Invoke(double[] args);
    }
    public interface IParser
    {
        void AddFunction(string name, IFunction func);
        IOperator Parse(string exp);
    }

    public partial class Parser : IParser
    {
        readonly Dictionary<string, Func<double[], double>> FunctionTable = new Dictionary<string, Func<double[], double>>();

        public void AddFunction(string name, Func<double[], double> func)
        {
            FunctionTable.Add(name, func);
        }

        public void AddFunction(string name, IFunction func)
        {
            AddFunction(name, a => func.Invoke(a));
        }
    }
}
