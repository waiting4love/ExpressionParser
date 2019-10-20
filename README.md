# ExpressionParser
四则运算表达式字符串解析

## 用法
### 基本用法
最基本的用法就是输入常数表达式，得到计算结果。仅支持四则运算。

```c#
Parser p = new Parser();
var op = p.Parse("-1.2e3 * ( 5 -11.2) / 2e-1");
var res = op.Calc(null);
Assert.AreEqual(-1.2e3 * (5 - 11.2) / 2e-1, res);
```

### 变量
表达式字符串中允许使用变量。解析后可调用`IOperator.GetKeys()`方法得到变量名列表。
在`IOperator.Calc()`方法中输入变量的值。注意与`IOperator.GetKeys()`给出的变量顺序一致。

```c#
Parser p = new Parser();
var op = p.Parse("a+b*5");

// 取得变量列表，keys = string[]{"a","b"}
var keys = op.GetKeys();

// 计算，注意double[]数组要按keys的变量顺序输入
var res = op.Calc(new double[]{1, 2});
```
### 函数
在表达式中可支持函数，在解析前用`Parser.AddFunction()`方法定义函数。

```c#
Parser p = new Parser();
p.AddFunction("log", a => Math.Log(a[0]));
p.AddFunction("pow", a => Math.Pow(a[0], a[1]));
var op = p.Parse("pow(10, x) * log(x)");
            
var res = op.Calc(new double[] { 5 });
Assert.AreEqual(Math.Pow(10, 5) * Math.Log(5), res);
```

## ENBF 表达式
### ws
- ' ' | '\t'
### sign
- '' | '+' | '-'
### exponent
- ''
- 'E'|'e' >> sign >> digits
### fraction
- ''
- '.' digits
### onenine
- ['1'-'9']
### digit
- '0' | onenine
### integer
- digit
- onenine >> digit*
- '-' digit
- '-' onenine >> digit*
### number
- integer >> fraction >> exponent
### alpha
- ['a'-'z' 'A'-'Z' _]
### alphanum
- alpha | digit
### const
- number
### variable
- alpha >> alphanum*
### group
- '(' >> exp >> ')'
### function
- variable >> '(' >> exp >> (',' >> exp)* >> ')'
### factor
- const | variable | group | function
### term
- factor >> ('*' >> factor | '/' >> factor)*
### exp
- term >> ('+' >> term | '-' >> term)*
