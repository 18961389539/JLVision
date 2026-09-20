using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>表示 Vision 元组（控制参数值的载体）。</summary>
/// <remarks>
///   <para><b>功能说明</b>：一个 <see cref="JlTuple"/> 承载一组有序的并行数据元素，可全部为数值、
///   字符串、句柄，或混合类型（<see cref="JlTupleType.MIXED"/>）。它是算子的控制参数与返回值封装。</para>
///   <para><b>典型场景</b>：向算子传参、接收返回值、在数值/字符串/句柄之间按需取值，以及参与
///   算术、逻辑、位运算（已重载运算符）。</para>
///   <para><b>资源与坑</b>：实现 <see cref="IDisposable"/>，占用原生句柄的对象用完应释放；
///   元素类型由最后写入的值"惰性"决定，读取时用与类型不符的口径会抛 <see cref="JlTupleAccessException"/>。</para>
///   <para><b>调用示例</b></para>
///   <code>
///   JlTuple t = new JlTuple(1, 2, 3);   // 构造多元素元组
///   int first = t[0];                    // t[0] 隐式转 int：first == 1
///   t[1] = 99;                           // 写入下标 1 处为 99
///   JlTuple neg = -t;                    // 重载运算符：逐元素取负 -> (-1,-99,-3)
///   JlTupleType tp = t.Type;             // 查看元素类型：INTEGER
///   t.Dispose();                         // 释放底层原生句柄
///   </code>
/// </remarks>
[Serializable]
public class JlTuple : ISerializable, ICloneable, IDisposable
{
	private delegate void NativeInt2To1(int[] in1, int[] in2, int[] buffer);

	private delegate void NativeLong2To1(long[] in1, long[] in2, long[] buffer);

	private delegate void NativeDouble2To1(double[] in1, double[] in2, double[] buffer);

	private enum ResultSize
	{
		EQUAL,
		SUM
	}

	internal JlTupleImplementation data;

	private static NativeInt2To1 addInt = NativeIntAdd;

	private static NativeLong2To1 addLong = NativeLongAdd;

	private static NativeDouble2To1 addDouble = NativeDoubleAdd;

	private static NativeInt2To1 subInt = NativeIntSub;

	private static NativeLong2To1 subLong = NativeLongSub;

	private static NativeDouble2To1 subDouble = NativeDoubleSub;

	private static NativeInt2To1 multInt = NativeIntMult;

	private static NativeLong2To1 multLong = NativeLongMult;

	private static NativeDouble2To1 multDouble = NativeDoubleMult;

	/// <summary>元组元素的底层存储类型（整数/长整数/浮点/字符串/句柄/混合/空），决定各便捷访问器与 Arr 属性能否直接使用。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>透传 <c>data.Type</c>，返回当前元组的实际存储类型。类型由内容决定：
	///   写入整数得 INTEGER，之后向任一位置写入 double/string 等异型值会把整条元组升级为
	///   <see cref="JlTupleType.MIXED"/>（索引器 setter 在类型不符时走 <c>ConvertToMixed</c>）。</para>
	///   <para><b>约束或前提</b>读取 <see cref="IArr"/>/<see cref="DArr"/> 等直取属性前应先核对本属性：
	///   它们只在类型匹配时有效；混合元组取数请走索引器或 <see cref="OArr"/>。</para>
	///   <para><b>与相邻用法的取舍</b><see cref="Length"/> 给元素个数、本属性给存储口径；判"空元组"
	///   两者皆可（<c>Length == 0</c> 或 <c>Type == EMPTY</c>），但判元素类型只能靠本属性。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new int[] { 5, 6 };
	///   if (t.Type == JlTupleType.INTEGER)
	///   {
	///       int v = t.I;               // 首元素整数便捷访问：v == 5
	///   }
	///   t[0] = 1.5;                    // 写入 double，整条元组升级 MIXED
	///   bool mixed = t.Type == JlTupleType.MIXED;
	///   t.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>属性本身是纯读取、零开销；但类型一旦升 MIXED 不会自动降回，
	///   后续批量取数要改走 object/索引器口径。</para>
	/// </remarks>
	public JlTupleType Type => data.Type;

	/// <summary>元组中的元素个数。</summary>
	/// <remarks>
	/// <para><b>说明</b>：3 个索引器（<c>this[int]</c>/<c>this[int[]]</c>/<c>this[JlTuple]</c>）都在取值时
	///   以下标是否落在 <c>[0, Length)</c> 之外来判断越界。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(1, 2, 3);
	///   int n = t.Length;              // n == 3
	///   </code>
	/// </remarks>
	public int Length => data.Length;

	/// <summary>按一组下标批量取（或写）元组元素：读返回 <see cref="JlTupleElements"/> 视图，写按位置赋值、必要时原地扩容。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>getter 对 <c>indices</c> 逐个检查 <c>0 ≤ i &lt; Length</c>，越界抛
	///   <see cref="JlTupleAccessException"/> "Index out of range"；合法则经 <c>data.GetElements</c>
	///   返回这些位置组成的视图；写入按位置一一赋值。</para>
	///   <para><b>约束或前提</b>setter 只把负下标当越界；上界越界会经 <c>AssertSize</c> 把元组扩到
	///   "最大下标+1"长（底层数组翻倍式扩容）。<c>indices</c> 为空数组时，被赋的值只能有 0 或 1 个
	///   元素（单值广播），否则抛异常。写入异型值会把整条元组升级为 <see cref="JlTupleType.MIXED"/>。</para>
	///   <para><b>与相邻重载的取舍</b>常量单下标用 <c>this[int]</c>；下标本身是算子输出的元组用
	///   <c>this[JlTuple]</c>。一次取多个位置时优先本重载，少一层单元素视图分配。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new int[] { 10, 20, 30 };
	///   int a = t[new[] { 0, 2 }].I;         // 下标 0、2 组成视图，读其首元素 -> 10
	///   t[new[] { 1 }] = 99;                 // 写下标 1 处 -> (10, 99, 30)
	///   int b = t[new[] { 0, 2 }].IArr[1];   // 视图第 1 位 -> 30
	///   t.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>视图是引用语义，经它写回即改源元组；读越界抛异常、写越界却静默扩容——
	///   两个方向的下标约定不对称。</para>
	/// </remarks>
	public JlTupleElements this[int[] indices]
	{
		get
		{
			foreach (int num in indices)
			{
				if (num < 0 || num >= data.Length)
				{
					throw new JlTupleAccessException("Index out of range");
				}
			}
			return data.GetElements(indices, this);
		}
		set
		{
			if (indices.Length == 0)
			{
				if (value.Length <= 1)
				{
					return;
				}
				throw new JlTupleAccessException("Input parameter 2 ('Value') must have one element or the same number of elements as parameter 1 ('Index')");
			}
			for (int i = 0; i < indices.Length; i++)
			{
				if (indices[i] < 0)
				{
					throw new JlTupleAccessException("Index out of range");
				}
			}
			if (data.Type == JlTupleType.EMPTY)
			{
				switch (value.Type)
				{
				case JlTupleType.INTEGER:
					data = new JlTupleInt32(0);
					break;
				case JlTupleType.LONG:
					data = new JlTupleInt64(0L);
					break;
				case JlTupleType.DOUBLE:
					data = new JlTupleDouble(0.0);
					break;
				case JlTupleType.STRING:
					data = new JlTupleString("");
					break;
				case JlTupleType.JlANDLE:
					data = new JlTupleHandle(null);
					break;
				case JlTupleType.MIXED:
					data = new JlTupleMixed(0);
					break;
				default:
					throw new JlTupleAccessException("Inconsistent tuple state encountered");
				}
			}
			data.AssertSize(indices);
			if (value.Type != data.Type)
			{
				ConvertToMixed();
			}
			try
			{
				data.SetElements(indices, value);
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				data.SetElements(indices, value);
			}
		}
	}

	/// <summary>按单个下标取（或写）元组元素；setter 转发到 <c>this[int[]]</c>，因此写入超出上界会原地扩容而非报错。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>getter 检查 <c>0 ≤ index &lt; Length</c>，越界抛
	///   <see cref="JlTupleAccessException"/> "Index out of range"，合法则返回该位置的
	///   <see cref="JlTupleElements"/> 视图；setter 展开为 <c>this[new int[1]{ index }] = value</c>。</para>
	///   <para><b>约束或前提</b>负下标读写都抛越界；上界外写入会把 <c>Length</c> 扩到
	///   <c>index+1</c>，中间新元素的填充口径 [待实测]。写入与当前类型不符的值会把元组升级为 MIXED。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new int[] { 1, 2, 3 };
	///   double first = t[0];   // 视图隐式转 double：first == 1.0
	///   t[2] = 7;              // 原地写下标 2 -> (1, 2, 7)
	///   int got = t[2];        // 7
	///   t.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>取回的是可隐式转 int/long/double/string 的视图对象，声明成 <c>var</c>
	///   拿到的是 <see cref="JlTupleElements"/> 而不是标量。</para>
	/// </remarks>
	public JlTupleElements this[int index]
	{
		get
		{
			if (index < 0 || index >= data.Length)
			{
				throw new JlTupleAccessException("Index out of range");
			}
			return data.GetElement(index, this);
		}
		set
		{
			this[new int[1] { index }] = value;
		}
	}

	/// <summary>下标以元组给出（先经 <see cref="GetIndicesFromTuple"/> 转 int[]），其余读写语义与 <c>this[int[]]</c> 相同。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>整条 <c>indices</c> 为 INTEGER/LONG 时直接 <c>ToIArr()</c>；否则逐元素转换：
	///   INTEGER/LONG 直取，DOUBLE 只接受整值（带小数抛 "Index has fractional part"），其它类型抛
	///   "Invalid index type"。转换后完全走批量索引器路径（读越界抛、写越界扩容）。</para>
	///   <para><b>与相邻重载的取舍</b>下标来自算子输出（筛选、排序结果元组）时用本重载免中转拷贝；
	///   字面下标用 <c>this[int[]]</c> 更直接。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new int[] { 10, 20, 30 };
	///   JlTuple idx = new int[] { 0, 2 };
	///   int b = t[idx].IArr[1];     // 等价取视图 IArr 第 1 位 -> 30
	///   t.Dispose();
	///   idx.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>用 double 元组当下标是常见误用：只有整值能通过，带小数即抛；
	///   空 <c>indices</c> 元组读取返回空视图，写入按广播规则处理。</para>
	/// </remarks>
	public JlTupleElements this[JlTuple indices]
	{
		get
		{
			return this[GetIndicesFromTuple(indices)];
		}
		set
		{
			this[GetIndicesFromTuple(indices)] = value;
		}
	}

	/// <summary>以 <c>int[]</c> 直接暴露底层存储（最省开销，但最不安全）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：直接读/写内部整数数组，走类型检查；写入时会保证元组类型为
	///   <see cref="JlTupleType.INTEGER"/>（类型不符则重建一个整数元组）。</para>
	///   <para><b>约束与坑</b>：仅当 <see cref="Type"/> 为整数类型时读取才有效；返回数组的<b>长度可能大于
	///   <see cref="Length"/></b>——底层为复用预留了空间，遍历务必用 <see cref="Length"/> 而不是 <c>arr.Length</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new int[] { 4, 5, 6 };
	///   int[] arr = t.IArr;                    // 直取底层数组，无逐项转换开销
	///   int sum = 0;
	///   for (int i = 0; i &lt; t.Length; i++)     // 上界用 Length，不用 arr.Length
	///   {
	///       sum += arr[i];
	///   }
	///   t.IArr = new int[] { 1, 2 };           // 写入并保证元组为 INTEGER
	///   t.Dispose();
	///   </code>
	/// </remarks>
	public int[] IArr
	{
		get
		{
			return data.IArr;
		}
		set
		{
			if (Type == JlTupleType.INTEGER)
			{
				data.IArr = value;
			}
			else
			{
				data = new JlTupleInt32(value, copy: false);
			}
		}
	}

	/// <summary>以 <c>long[]</c> 直接暴露底层存储（最省开销，但最不安全）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：直接读/写内部 64 位整数数组，不走类型检查；写入时会保证元组类型为
	///   <see cref="JlTupleType.LONG"/>。</para>
	///   <para><b>约束与坑</b>：仅当 <see cref="Type"/> 为 64 位整数类型时读取才有效；返回数组的<b>长度可能大于
	///   <see cref="Length"/></b>，遍历务必用 <see cref="Length"/> 而不是 <c>arr.Length</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new long[] { 100000L, 200000L };
	///   long[] arr = t.LArr;                   // 直取底层 64 位数组
	///   long v = arr[1];                       // 200000；只看前 Length 项
	///   t.LArr = new long[] { 3L };            // 写入并保证元组为 LONG
	///   t.Dispose();
	///   </code>
	/// </remarks>
	public long[] LArr
	{
		get
		{
			return data.LArr;
		}
		set
		{
			if (Type == JlTupleType.LONG)
			{
				data.LArr = value;
			}
			else
			{
				data = new JlTupleInt64(value, copy: false);
			}
		}
	}

	/// <summary>以 <c>double[]</c> 直接暴露底层存储（最省开销，但最不安全）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：直接读/写内部 double 数组，不走类型检查；写入时会保证元组类型为
	///   <see cref="JlTupleType.DOUBLE"/>。</para>
	///   <para><b>约束与坑</b>：仅当 <see cref="Type"/> 为数值类型时读取才有效；返回数组的<b>长度可能大于
	///   <see cref="Length"/></b>，遍历务必用 <see cref="Length"/> 而不是 <c>arr.Length</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new double[] { 1.5, 2.5 };
	///   double[] arr = t.DArr;                 // 直取底层 double 数组
	///   double d = arr[0];                     // 1.5；只看前 Length 项
	///   t.DArr = new double[] { 3.0, 4.0 };    // 写入并保证元组为 DOUBLE
	///   t.Dispose();
	///   </code>
	/// </remarks>
	public double[] DArr
	{
		get
		{
			return data.DArr;
		}
		set
		{
			if (Type == JlTupleType.DOUBLE)
			{
				data.DArr = value;
			}
			else
			{
				data = new JlTupleDouble(value, copy: false);
			}
		}
	}

	/// <summary>以 <c>string[]</c> 直接暴露底层存储（最省开销，但最不安全）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：直接读/写内部字符串数组，不走类型检查；写入时会保证元组类型为
	///   <see cref="JlTupleType.STRING"/>。</para>
	///   <para><b>约束与坑</b>：仅当 <see cref="Type"/> 为字符串类型时读取才有效；返回数组的<b>长度可能大于
	///   <see cref="Length"/></b>，遍历务必用 <see cref="Length"/> 而不是 <c>arr.Length</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new string[] { "a", "b" };
	///   string[] arr = t.SArr;                 // 直取底层字符串数组
	///   string s = arr[1];                     // "b"；只看前 Length 项
	///   t.SArr = new string[] { "c" };         // 写入并保证元组为 STRING
	///   t.Dispose();
	///   </code>
	/// </remarks>
	public string[] SArr
	{
		get
		{
			return data.SArr;
		}
		set
		{
			if (Type == JlTupleType.STRING)
			{
				data.SArr = value;
			}
			else
			{
				data = new JlTupleString(value, copy: false);
			}
		}
	}

	/// <summary>以 <see cref="JlHandle"/>[] 直接暴露底层存储（最省开销，但最不安全）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：直接读/写内部句柄数组，不走类型检查；写入时会保证元组类型为
	///   <see cref="JlTupleType.JlANDLE"/>。</para>
	///   <para><b>约束与坑</b>：仅当 <see cref="Type"/> 为句柄类型时读取才有效；返回数组的<b>长度可能大于
	///   <see cref="Length"/></b>，遍历务必用 <see cref="Length"/> 而不是 <c>arr.Length</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(new JlHandle[] { new JlHandle(), new JlHandle() });
	///   JlHandle[] hs = t.JlArr;               // 仅当 Type 为句柄类型时有效
	///   int n = t.Length;                      // 用 Length 而非 hs.Length 界定有效范围
	///   bool allocated = hs[0] != null;
	///   t.Dispose();                           // 句柄类元素随元组 Dispose 释放
	///   </code>
	/// </remarks>
	public JlHandle[] JlArr
	{
		get
		{
			return data.JlArr;
		}
		set
		{
			if (Type == JlTupleType.JlANDLE)
			{
				data.JlArr = value;
			}
			else
			{
				data = new JlTupleHandle(value, copy: false);
			}
		}
	}

	/// <summary>以 <c>object[]</c> 暴露混合（<see cref="JlTupleType.MIXED"/>）元组的底层存储。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：直接读取混合元组的内部 object 数组，各元素被装箱为 <c>int</c>/<c>long</c>/
	///   <c>double</c>/<c>string</c>/<see cref="JlHandle"/> 之一。</para>
	///   <para><b>约束与坑</b>：该属性为只读且标 <c>EditorBrowsable(Never)</c>——<b>不建议修改返回的数组</b>；
	///   确需写入时只允许存上述受支持的元素类型。返回数组长度可能大于 <see cref="Length"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new object[] { 1, "two", 3.0 };
	///   object[] oarr = t.OArr;      // 混合元组的装箱元素
	///   int n = t.Length;            // 只信任前 n 项，数组本身可能更长
	///   object first = oarr[0];      // 装箱的 int 1
	///   t.Dispose();
	///   </code>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public object[] OArr => data.OArr;

	/// <summary>便捷访问 <c>this[0]</c> 的 32 位整数元素。</summary>
	/// <remarks>
	///   <para>等价于 <c>this[0].I</c>；读取要求首元素为整数数据，对空元组调用会越界抛
	///   <see cref="JlTupleAccessException"/>。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(7);
	///   int v = t.I;      // v == 7
	///   </code>
	/// </remarks>
	public int I
	{
		get
		{
			return this[0].I;
		}
		set
		{
			this[0].I = value;
		}
	}

	/// <summary>便捷访问 <c>this[0]</c> 的 64 位整数元素。</summary>
	/// <remarks>
	///   <para>等价于 <c>this[0].L</c>；读取要求首元素为整数数据，对空元组调用会越界抛
	///   <see cref="JlTupleAccessException"/>。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(7L);
	///   long v = t.L;      // v == 7
	///   </code>
	/// </remarks>
	public long L
	{
		get
		{
			return this[0].L;
		}
		set
		{
			this[0].L = value;
		}
	}

	/// <summary>便捷访问 <c>this[0]</c> 的 double 元素。</summary>
	/// <remarks>
	///   <para>等价于 <c>this[0].D</c>；读取要求首元素为数值数据，对空元组调用会越界抛
	///   <see cref="JlTupleAccessException"/>。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(0.5);
	///   double v = t.D;      // v == 0.5
	///   </code>
	/// </remarks>
	public double D
	{
		get
		{
			return this[0].D;
		}
		set
		{
			this[0].D = value;
		}
	}

	/// <summary>便捷访问 <c>this[0]</c> 的字符串元素。</summary>
	/// <remarks>
	///   <para>等价于 <c>this[0].S</c>；读取要求首元素为字符串数据，对空元组调用会越界抛
	///   <see cref="JlTupleAccessException"/>。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple t = new JlTuple("abc");
	///   string v = t.S;      // v == "abc"
	///   </code>
	/// </remarks>
	public string S
	{
		get
		{
			return this[0].S;
		}
		set
		{
			this[0].S = value;
		}
	}

	/// <summary>便捷访问 <c>this[0]</c> 的句柄元素。</summary>
	/// <remarks>
	///   <para>等价于 <c>this[0].H</c>；读取要求首元素为句柄数据，对空元组调用会越界抛
	///   <see cref="JlTupleAccessException"/>。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(new JlImage());   // 首元素为对象句柄（默认构造一幅空图）
	///   JlHandle v = t.H;
	///   v.Dispose();   // 用完释放句柄
	///   t.Dispose();
	///   </code>
	/// </remarks>
	public JlHandle H
	{
		get
		{
			return this[0].H;
		}
		set
		{
			this[0].H = value;
		}
	}

	/// <summary>便捷访问 <c>this[0]</c> 的 object 元素（任意类型，数值会被装箱）。</summary>
	/// <remarks>
	///   <para>等价于 <c>this[0].O</c>；读取会按元素实际装箱类型返回值，对空元组调用会越界抛
	///   <see cref="JlTupleAccessException"/>。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(3.5);
	///   object v = t.O;      // v 是装箱的 double：v is double
	///   </code>
	/// </remarks>
	public object O
	{
		get
		{
			return this[0].O;
		}
		set
		{
			this[0].O = value;
		}
	}

	/// <summary>便捷访问 <c>this[0]</c> 的 IntPtr 元素。</summary>
	/// <remarks>
	///   <para>等价于 <c>this[0].IP</c>；读取要求首元素为代表指针的整数并匹配当前平台的
	///   <see cref="IntPtr.Size"/>，对空元组调用会越界抛 <see cref="JlTupleAccessException"/>。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(new IntPtr(0xFF));
	///   IntPtr v = t.IP;     // v 的地址值 == 0xFF
	///   </code>
	/// </remarks>
	public IntPtr IP
	{
		get
		{
			return this[0].IP;
		}
		set
		{
			this[0].IP = value;
		}
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeTuple();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>ISerializable 反序列化构造器：从 SerializationInfo 的 "data" 字节数组重建元组内容，全程不调用原生算子。</summary>
	/// <param name="info">序列化载体，须含键 "data"（byte[]，即 <c>SerializeTuple()</c> 的产物）。</param>
	/// <param name="context">流式上下文，方法体不读取其内容。</param>
	/// <remarks>
	///   <para><b>功能说明</b>方法体两步：<c>DeserializeTuple((byte[])info.GetValue("data", ...))</c>
	///   还原出中间元组，再把它的内部存储 <c>data</c> 引用直接接进本实例（引用转移，中间对象丢弃）。
	///   纯托管字节流还原，零原生调用。</para>
	///   <para><b>约束或前提</b>标 <c>EditorBrowsable(Never)</c>，是二进制格式化器的反射入口——正常代码
	///   走 <see cref="Serialize(Stream)"/> 与静态 <see cref="Deserialize(Stream)"/> 配对，而不是手写
	///   SerializationInfo。"data" 键缺失或字节非法时的具体异常口径 [待实测]。</para>
	///   <para><b>与相邻用法的取舍</b>只要字节数组时 <c>SerializeTuple()/DeserializeTuple(byte[])</c>
	///   这一对更直接；本构造器仅服务 <c>ISerializable</c> 协议。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple src = new int[] { 4, 5, 6 };
	///   System.Runtime.Serialization.SerializationInfo info = new System.Runtime.Serialization.SerializationInfo(typeof(JlTuple), new System.Runtime.Serialization.FormatterConverter());
	///   info.AddValue("data", src.SerializeTuple());
	///   System.Runtime.Serialization.StreamingContext ctx = new System.Runtime.Serialization.StreamingContext();
	///   using JlTuple restored = new JlTuple(info, ctx);   // restored == (4, 5, 6)
	///   int n = restored.Length;                            // 3
	///   src.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><see cref="Dispose()"/> 只释放句柄类元素；纯数值/字符串元组用完不释放
	///   也无原生泄漏。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlTuple(SerializationInfo info, StreamingContext context)
	{
		JlTuple hTuple = DeserializeTuple((byte[])info.GetValue("data", typeof(byte[])));
		data = hTuple.data;
	}

	/// <summary>把元组按 Vision 二进制格式写入流；字节内容即 <see cref="SerializeTuple()"/> 的返回值。</summary>
	/// <param name="stream">目标流，须可写；本方法不关闭该流。</param>
	/// <remarks>
	///   <para><b>功能说明</b>：整个元组（含 MIXED 与句柄元素）一次性序列化，供缓存或跨进程传递；
	///   读回用静态 <see cref="Deserialize(Stream)"/>。</para>
	///   <para><b>与相邻方法取舍</b>：落盘到文件用 <see cref="WriteTuple(JlTuple)"/>；只要字节数组自己管
	///   存储用 <see cref="SerializeTuple()"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new int[] { 1, 2, 3 };
	///   System.IO.MemoryStream ms = new System.IO.MemoryStream();
	///   t.Serialize(ms);
	///   </code>
	///   <para><b>资源与坑</b>：<see cref="Dispose()"/> 只释放元组内存储的句柄元素；纯数值/字符串元组
	///   序列化后无需特殊释放。</para>
	/// </remarks>
	public void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeTuple(), stream);
	}

	/// <summary>从 Vision 二进制流读回一个元组；返回<b>新建</b>的 <see cref="JlTuple"/>。</summary>
	/// <param name="stream">由 <see cref="Serialize(Stream)"/> 写出的流，须可读。</param>
	/// <returns>新元组（新对象，与流不再有联系）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：读出 <see cref="Serialize(Stream)"/> 格式的字节并重建元组，内部走
	///   <see cref="DeserializeTuple(byte[])"/>；元素类型（含 MIXED）按序列化时的原类型还原。</para>
	///   <para><b>资源与坑</b>：若还原出的元组含句柄元素，用完应对其调用 <see cref="Dispose()"/>；
	///   流位置不对或字节非本格式时抛序列化异常，不会返回半截数据 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new int[] { 1, 2, 3 };
	///   System.IO.MemoryStream ms = new System.IO.MemoryStream();
	///   t.Serialize(ms);
	///   ms.Position = 0;
	///   JlTuple back = JlTuple.Deserialize(ms);
	///   </code>
	/// </remarks>
	public static JlTuple Deserialize(Stream stream)
	{
		return DeserializeTuple(JlSerializationBuffer.ReadFromStream(stream));
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>把本元组与 <paramref name="set2"/> 按集合求并，返回新元组。</summary>
	/// <param name="set2">参与运算的第二路输入元组。</param>
	/// <returns>新建的结果元组（MIXED 口径装载），本元组不被修改。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 95（tuple_union 语义）。本元组与 <paramref name="set2"/> 
	///   在调用内部先钉固定（Store）、原生调用结束即自动解固定，调用方无需也不应自己配对
	///   <see cref="UnpinTuple()"/>。</para>
	///   <para><b>约束或前提</b>：输出顺序、重复元素是否去重由原生侧决定 [待实测]；数值与字符串混用
	///   时结果类型可能变为 MIXED。</para>
	///   <para><b>与相邻算子的取舍</b>：只要"在 A 中但不在 B 中"的部分用
	///   <see cref="TupleDifference(JlTuple)"/>；只要两边独有的用 <see cref="TupleSymmdiff(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple set1 = new int[] { 1, 2, 3 };
	///   JlTuple set2 = new int[] { 3, 4 };
	///   JlTuple u = set1.TupleUnion(set2);
	///   </code>
	///   <para><b>资源与坑</b>：结果是新对象；若结果元组含句柄元素，用完调用其 <see cref="Dispose()"/>，
	///   纯数值/字符串结果可不处理。</para>
	/// </remarks>
	public JlTuple TupleUnion(JlTuple set2)
	{
		IntPtr proc = JlNativeApi.PreCall(95);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, set2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(set2);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把本元组与 <paramref name="set2"/> 按集合求交，返回新元组。</summary>
	/// <param name="set2">参与运算的第二路输入元组。</param>
	/// <returns>新建的结果元组，本元组不被修改。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 96（tuple_intersection 语义）。钉固定/解固定在本方法内部
	///   成对完成（Store → CallProcedure → UnpinTuple），调用方不参与。</para>
	///   <para><b>约束或前提</b>：两路都支持的元素才可比较；交集为空时返回空元组（<see cref="Length"/> 为 0）
	///   而不是 null 或异常。</para>
	///   <para><b>与相邻算子的取舍</b>：求"共有元素之外"的部分用 <see cref="TupleSymmdiff(JlTuple)"/>；
	///   求并集用 <see cref="TupleUnion(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple set1 = new int[] { 1, 2, 3 };
	///   JlTuple set2 = new int[] { 3, 4 };
	///   JlTuple inter = set1.TupleIntersection(set2);
	///   </code>
	///   <para><b>资源与坑</b>：结果含句柄元素时用完调用其 <see cref="Dispose()"/>。输出顺序与重复元素
	///   处理由原生侧决定 [待实测]。</para>
	/// </remarks>
	public JlTuple TupleIntersection(JlTuple set2)
	{
		IntPtr proc = JlNativeApi.PreCall(96);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, set2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(set2);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>本元组减去 <paramref name="set2"/> 的集合差（保留只属于本元组的元素），返回新元组。</summary>
	/// <param name="set2">被减去的输入元组。</param>
	/// <returns>新建的结果元组；方向固定为"本元组 − set2"，不满足交换律。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 97（tuple_difference 语义）。钉固定/解固定在本方法内部成对完成，
	///   调用方不参与。</para>
	///   <para><b>与相邻算子的取舍</b>：要"两边各自独有"的部分用 <see cref="TupleSymmdiff(JlTuple)"/>；
	///   要共有部分用 <see cref="TupleIntersection(JlTuple)"/>。注意本方法是<b>单向</b>差集，反过来求
	///   需换成 <c>set2.TupleDifference(本元组)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple set1 = new int[] { 1, 2, 3 };
	///   JlTuple set2 = new int[] { 3, 4 };
	///   JlTuple diff = set1.TupleDifference(set2);
	///   </code>
	///   <para><b>资源与坑</b>：返回永不为 null——原生调用失败时也是装载为空的元组；结果含句柄元素时
	///   用完调用其 <see cref="Dispose()"/>。重复元素处理与输出顺序由原生侧决定 [待实测]。</para>
	/// </remarks>
	public JlTuple TupleDifference(JlTuple set2)
	{
		IntPtr proc = JlNativeApi.PreCall(97);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, set2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(set2);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>本元组与 <paramref name="set2"/> 的对称差（只属于其中一边的元素），返回新元组。</summary>
	/// <param name="set2">参与运算的第二路输入元组。</param>
	/// <returns>新建的结果元组；满足交换律，与 <paramref name="set2"/> 的先后无关。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 98（tuple_symmdiff 语义）。钉固定/解固定在本方法内部成对完成，
	///   调用方不参与。</para>
	///   <para><b>与相邻算子的取舍</b>：只单向剔除 <paramref name="set2"/> 中的元素用
	///   <see cref="TupleDifference(JlTuple)"/>；对称差会把"两边各自独有"的都留下，用来快速验证两集合是否
	///   相等（结果为空即相等）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple set1 = new int[] { 1, 2, 3 };
	///   JlTuple set2 = new int[] { 3, 4 };
	///   JlTuple sym = set1.TupleSymmdiff(set2);
	///   </code>
	///   <para><b>资源与坑</b>：结果含句柄元素时用完调用其 <see cref="Dispose()"/>。重复元素处理与输出顺序
	///   由原生侧决定 [待实测]。</para>
	/// </remarks>
	public JlTuple TupleSymmdiff(JlTuple set2)
	{
		IntPtr proc = JlNativeApi.PreCall(98);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, set2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(set2);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素判断"该元素是否为字符串"，返回与输入等长的 0/1 整数元组。</summary>
	/// <returns>INTEGER 型元组：1 = 该位置是字符串，0 = 不是。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 99（tuple_is_string_elem 语义），输出按 INTEGER 装载；
	///   钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：只想知道"整个元组是不是字符串元组"用 <see cref="TupleIsString()"/>
	///   （单值结果）；本方法适合 MIXED 元组里逐位甄别。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new object[] { "abc", 12 };
	///   JlTuple flags = t.TupleIsStringElem();
	///   int firstIsString = flags.I;
	///   </code>
	///   <para><b>资源与坑</b>：标量读取（<c>flags.I</c> 或隐式转 int）只看<b>第一个元素</b>，其余 0/1 值
	///   会被静默丢弃；逐位判断请用 <c>flags[i].I</c> 或整数组 <c>int[] v = flags;</c>。</para>
	/// </remarks>
	public JlTuple TupleIsStringElem()
	{
		IntPtr proc = JlNativeApi.PreCall(99);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素判断"该元素是否为实数（浮点）类型"，返回与输入等长的 0/1 整数元组。</summary>
	/// <returns>INTEGER 型元组：1 = 该位置为实数，0 = 不是。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 100（tuple_is_real_elem 语义），输出按 INTEGER 装载；
	///   钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：对 MIXED 元组逐位给值；整数元素是否计为 real 由原生侧的类型口径决定
	///   [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：整个元组一条判定用 <see cref="TupleIsReal()"/>；要拿到具体类型码
	///   用 <see cref="TupleTypeElem()"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new double[] { 0.5, 2.0 };
	///   JlTuple flags = t.TupleIsRealElem();
	///   int[] v = flags;
	///   </code>
	///   <para><b>资源与坑</b>：<c>flags.I</c>/隐式转 int 只读第一个元素，其余判定值被静默丢弃。</para>
	/// </remarks>
	public JlTuple TupleIsRealElem()
	{
		IntPtr proc = JlNativeApi.PreCall(100);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素判断"该元素是否为整数类型"，返回与输入等长的 0/1 整数元组。</summary>
	/// <returns>INTEGER 型元组：1 = 该位置为整数，0 = 不是。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 101（tuple_is_int_elem 语义），输出按 INTEGER 装载；
	///   钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：64 位 LONG 元素是否计为 int 由原生侧口径决定 [待实测]；浮点即使值为
	///   整数（如 2.0）通常也不算整数类型。</para>
	///   <para><b>与相邻算子的取舍</b>：整个元组一条判定用 <see cref="TupleIsInt()"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new object[] { 1, 2.5 };
	///   JlTuple flags = t.TupleIsIntElem();
	///   int firstIsInt = flags.I;
	///   </code>
	///   <para><b>资源与坑</b>：<c>flags.I</c>/隐式转 int 只读第一个元素，其余判定值被静默丢弃。</para>
	/// </remarks>
	public JlTuple TupleIsIntElem()
	{
		IntPtr proc = JlNativeApi.PreCall(101);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素返回其类型码（整数），可用于区分 MIXED 元组里每一位的实际类型。</summary>
	/// <returns>INTEGER 型元组，每个值是原生侧的类型码；与 <see cref="JlTupleType"/> 枚举数值的对应关系
	/// [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 102（tuple_type_elem 语义），输出按 INTEGER 装载；
	///   钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：只判"是不是某种类型"用 <see cref="TupleIsIntElem()"/>/
	///   <see cref="TupleIsRealElem()"/>/<see cref="TupleIsStringElem()"/> 更直观；需要区分全部类型档
	///   （含 LONG/句柄）时才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new object[] { 1, "a" };
	///   JlTuple codes = t.TupleTypeElem();
	///   int[] v = codes;
	///   </code>
	///   <para><b>资源与坑</b>：<c>codes.I</c> 只给第一个元素的类型码；逐位对照前先确认数组长度为
	///   <see cref="Length"/>。</para>
	/// </remarks>
	public JlTuple TupleTypeElem()
	{
		IntPtr proc = JlNativeApi.PreCall(102);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>判断整个元组是否为混合（<see cref="JlTupleType.MIXED"/>）类型，单值 0/1 结果。</summary>
	/// <returns>INTEGER 型单值元组：1 = MIXED，0 = 否。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 103（tuple_is_mixed 语义），输出按 INTEGER 装载；
	///   钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：向纯数值元组写入字符串（或反之）会使存储惰性升级为 MIXED，此后
	///   <see cref="TupleIsInt()"/>/<see cref="TupleIsReal()"/> 等都会判 0——用本方法可先确认升级发生了。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new object[] { 1, "a" };
	///   int mixed = t.TupleIsMixed();
	///   </code>
	///   <para><b>资源与坑</b>：结果隐式转 int/bool 只取第一个元素（本方法即单值，正常）；对空元组的
	///   判定值 [待实测]。</para>
	/// </remarks>
	public JlTuple TupleIsMixed()
	{
		IntPtr proc = JlNativeApi.PreCall(103);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>判断元组的内部存储表示是否为字符串型，单值 0/1 结果。</summary>
	/// <returns>INTEGER 型单值元组：1 = 底层是 string 数组（<see cref="JlTupleType.STRING"/>），0 = 否。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 104（tuple_is_string 语义），输出按 INTEGER 装载；
	///   钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：查的是<b>存储表示</b>而非值内容；MIXED 元组即使含字符串元素也判 0，
	///   逐位甄别改用 <see cref="TupleIsStringElem()"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new string[] { "abc", "def" };
	///   int isStr = t.TupleIsString();
	///   </code>
	///   <para><b>资源与坑</b>：想避免取值异常，先 <c>isStr == 1</c> 再读 <see cref="S"/>/<see cref="SArr"/>。</para>
	/// </remarks>
	public JlTuple TupleIsString()
	{
		IntPtr proc = JlNativeApi.PreCall(104);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>判断元组的内部存储表示是否为 double 型，单值 0/1 结果。</summary>
	/// <returns>INTEGER 型单值元组：1 = 底层是 double 数组（<see cref="JlTupleType.DOUBLE"/>），0 = 否。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 105（tuple_is_real 语义），输出按 INTEGER 装载；
	///   钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：用 int 构造的元组表示为 INTEGER，本方法判 0——不是"值能不能当实数用"，
	///   而是"存储是不是 double"；64 位 LONG 表示的判定口径 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new double[] { 0.5, 2.5 };
	///   int isReal = t.TupleIsReal();
	///   </code>
	///   <para><b>资源与坑</b>：读 <see cref="D"/>/<see cref="DArr"/> 报类型异常时，先用本方法确认存储档位。</para>
	/// </remarks>
	public JlTuple TupleIsReal()
	{
		IntPtr proc = JlNativeApi.PreCall(105);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>判断元组的内部存储表示是否为 32 位整数型，单值 0/1 结果。</summary>
	/// <returns>INTEGER 型单值元组：1 = 底层是 int 数组（<see cref="JlTupleType.INTEGER"/>），0 = 否。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 106（tuple_is_int 语义），输出按 INTEGER 装载；
	///   钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：写入过浮点/字符串后存储会升级为 DOUBLE/MIXED，本方法随即判 0；
	///   LONG（64 位）表示是否判 1 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：拿到"具体是什么类型"用 <see cref="TupleType()"/>，本方法只回答是/否。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new int[] { 1, 2, 3 };
	///   int isInt = t.TupleIsInt();
	///   </code>
	/// </remarks>
	public JlTuple TupleIsInt()
	{
		IntPtr proc = JlNativeApi.PreCall(106);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>以整数类型码返回整个元组的类型（原生算子 id 107）。</summary>
	/// <returns>INTEGER 型单值元组，即原生侧类型码；与 <see cref="JlTupleType"/> 枚举数值的对应关系
	/// [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：走一次原生调用取类型码；钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻成员的取舍</b>：只在托管侧判断时直接用属性 <see cref="Type"/>（零原生开销，返回
	///   <see cref="JlTupleType"/>）；本方法适用于需要与原生算子交换类型码的场合。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new int[] { 1, 2, 3 };
	///   int code = t.TupleType();
	///   </code>
	///   <para><b>资源与坑</b>：单元素整数结果，<c>code</c> 直接隐式转 int 取首元素即可。</para>
	/// </remarks>
	public JlTuple TupleType()
	{
		IntPtr proc = JlNativeApi.PreCall(107);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>在 [min, max] 值域内按等宽 bin 统计本元组的分布，返回计数直方图，bin 宽度经 out 给出。</summary>
	/// <param name="min">直方图最小值（单值；传字面量时经隐式转换升为元组）。</param>
	/// <param name="max">直方图最大值。</param>
	/// <param name="numBins">bin 数量。</param>
	/// <param name="binSize">输出：实际 bin 宽度，按 DOUBLE 装载。</param>
	/// <returns>直方图计数，按 INTEGER 装载的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 108（tuple_histo_range 语义）。两个输出装载口径不同：
	///   计数是整数（INTEGER），binSize 是浮点（DOUBLE）——不要对 binSize 取 <c>.I</c> 截断。</para>
	///   <para><b>约束或前提</b>：输入应为数值元组；区间端点归属（左闭右开？）与值域外元素如何丢弃
	///   [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：需要累积分布时先取直方图再 <see cref="TupleCumul()"/>；
	///   输入乱序不影响计数结果。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple samples = new double[] { 0.3, 1.2, 2.9 };
	///   JlTuple binSize;
	///   JlTuple histo = samples.TupleHistoRange(0.0, 3.0, 3, out binSize);
	///   </code>
	///   <para><b>资源与坑</b>：入参元组的钉固定/解固定在本方法内部成对完成；histo 与 binSize 都是新对象。</para>
	/// </remarks>
	public JlTuple TupleHistoRange(JlTuple min, JlTuple max, JlTuple numBins, out JlTuple binSize)
	{
		IntPtr proc = JlNativeApi.PreCall(108);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, min);
		JlNativeApi.Store(proc, 2, max);
		JlNativeApi.Store(proc, 3, numBins);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(min);
		JlNativeApi.UnpinTuple(max);
		JlNativeApi.UnpinTuple(numBins);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = LoadNew(proc, 1, JlTupleType.DOUBLE, err, out binSize);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>从字符串元组中挑出匹配正则的元素，返回匹配项组成的新元组。</summary>
	/// <param name="expression">正则表达式。Default: ".*"</param>
	/// <returns>匹配成功的字符串（STRING 型新元组）；无一命中时为空元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 109（tuple_regexp_select 语义）；钉固定/解固定在本方法内部
	///   成对完成。</para>
	///   <para><b>约束或前提</b>：输入应为字符串元组；按整串匹配还是部分匹配、正则方言（POSIX/PCRE）与
	///   大小写敏感行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只要"有没有匹配/匹配几个"用 <see cref="TupleRegexpTest(JlTuple)"/>；
	///   要提取串内子段用 <see cref="TupleRegexpMatch(JlTuple)"/>；本方法按整条元素是否匹配来筛选。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple names = new string[] { "img1.bmp", "log.txt", "img2.bmp" };
	///   JlTuple bmps = names.TupleRegexpSelect("^img.*\\.bmp$");
	///   </code>
	///   <para><b>资源与坑</b>：结果长度可能小于输入，遍历按 <c>Length</c>，不要沿用输入的下标。</para>
	/// </remarks>
	public JlTuple TupleRegexpSelect(JlTuple expression)
	{
		IntPtr proc = JlNativeApi.PreCall(109);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, expression);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(expression);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>用正则判定字符串元组的匹配情况，输出按 INTEGER 装载。</summary>
	/// <param name="expression">正则表达式。Default: ".*"</param>
	/// <returns>INTEGER 型元组：语义为"匹配字符串的条数"（单值）还是逐元素 0/1 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 110（tuple_regexp_test 语义），输出按 INTEGER 装载；
	///   钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：要拿到匹配的是哪些串用 <see cref="TupleRegexpSelect(JlTuple)"/>；
	///   本方法不返回内容，只给 INTEGER 判定结果。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple names = new string[] { "img1.bmp", "log.txt" };
	///   int n = names.TupleRegexpTest("^img");
	///   </code>
	///   <para><b>资源与坑</b>：结果隐式转 int 时只取第一个值；若语义是计数则直接可用，若是逐元素
	///   0/1 则其余位会被静默丢弃 [待实测]。</para>
	/// </remarks>
	public JlTuple TupleRegexpTest(JlTuple expression)
	{
		IntPtr proc = JlNativeApi.PreCall(110);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, expression);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(expression);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>用正则把字符串元组中命中的子串替换为 <paramref name="replace"/>，返回改写后的新元组。</summary>
	/// <param name="expression">正则表达式。Default: ".*"</param>
	/// <param name="replace">替换串（单值广播或逐元素对应）。</param>
	/// <returns>替换后的字符串新元组，长度与输入一致。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 111（tuple_regexp_replace 语义）；两路入参的钉固定/解固定
	///   在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：替换串是否支持捕获组反向引用（如 \1、$1）[待实测]；是替换全部命中还是
	///   仅首个命中 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：固定文本（非正则）的前后替换用 <see cref="TupleStrReplace(JlTuple, JlTuple)"/>；
	///   只想删除命中内容时把 <paramref name="replace"/> 传空串。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple names = new string[] { "img1.bmp", "img2.bmp" };
	///   JlTuple renamed = names.TupleRegexpReplace("\\.bmp$", ".png");
	///   </code>
	///   <para><b>资源与坑</b>：本方法原地不改动输入元组，结果是新对象。</para>
	/// </remarks>
	public JlTuple TupleRegexpReplace(JlTuple expression, JlTuple replace)
	{
		IntPtr proc = JlNativeApi.PreCall(111);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, expression);
		JlNativeApi.Store(proc, 2, replace);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(expression);
		JlNativeApi.UnpinTuple(replace);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>用正则从字符串元组中<b>提取子段</b>（可含捕获组），返回提取出的字符串新元组。</summary>
	/// <param name="expression">正则表达式。Default: ".*"</param>
	/// <returns>命中的子串集合；一条输入可展开出多条输出，长度与输入无关。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 112（tuple_regexp_match 语义）；钉固定/解固定在本方法内部
	///   成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：筛选"整条是否匹配"用 <see cref="TupleRegexpSelect(JlTuple)"/>；
	///   固定位置截取用 <see cref="TupleSubstr(JlTuple, JlTuple)"/>；本方法用于按模式抠出串内片段
	///   （如从路径里取文件名数字段）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple names = new string[] { "img001.bmp", "img042.bmp" };
	///   JlTuple nums = names.TupleRegexpMatch("img([0-9]+)");
	///   </code>
	///   <para><b>资源与坑</b>：捕获组是否单独成项、未命中元素如何占位 [待实测]——解析结果前先核对
	///   <see cref="Length"/>，不要假设与输入一一对应。</para>
	/// </remarks>
	public JlTuple TupleRegexpMatch(JlTuple expression)
	{
		IntPtr proc = JlNativeApi.PreCall(112);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, expression);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(expression);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>生成指定长度的 0 到 1 之间随机数元组（静态方法，无需实例；端点归属见 remarks）。</summary>
	/// <param name="length">要生成的元素个数（单值）。</param>
	/// <returns>DOUBLE 型新元组，长度为 <paramref name="length"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 113（tuple_rand 语义），输出按 DOUBLE 装载——即使想要整数
	///   随机数也要再自行取整；静态方法内部只钉固定/解固定 <paramref name="length"/>。</para>
	///   <para><b>约束或前提</b>：端点能否取到 0 或 1、随机流是否可复现/受全局种子影响 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：要等差序列用 <see cref="TupleGenSequence(JlTuple, JlTuple, JlTuple)"/>；
	///   要整数随机需 <c>TupleRand(n).TupleInt()</c> 或等价取整。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple r = JlTuple.TupleRand(5);
	///   double first = r.D;
	///   </code>
	///   <para><b>资源与坑</b>：读取单值用 <c>r.D</c> 只拿第一个随机数，其余仍留在元组里。</para>
	/// </remarks>
	public static JlTuple TupleRand(JlTuple length)
	{
		IntPtr proc = JlNativeApi.PreCall(113);
		JlNativeApi.Store(proc, 0, length);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(length);
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>
	///   返回元组的元素个数。
	/// </summary>
	/// <returns>输入元组的元素个数。</returns>
	private JlTuple TupleLengthOp()
	{
		IntPtr proc = JlNativeApi.PreCall(114);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取符号：正数→1、负数→-1、零→0，结果按 INTEGER 装载。</summary>
	/// <returns>与输入等长的 INTEGER 型新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 115（tuple_sgn 语义）；钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：输出装载为整数——即使输入是 double，结果也是 int 元组；NaN 元素的
	///   符号值 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只要绝对值用 <see cref="TupleAbs()"/>；判"是否非负"也可用
	///   比较运算符 <c>&gt;=</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new double[] { -2.5, 0.0, 3.5 };
	///   JlTuple s = v.TupleSgn();
	///   int[] signs = s;
	///   </code>
	/// </remarks>
	public JlTuple TupleSgn()
	{
		IntPtr proc = JlNativeApi.PreCall(115);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>与 <paramref name="t2"/> 逐元素取较大值（常用作下限钳位），返回新元组。</summary>
	/// <param name="t2">第二路输入元组。</param>
	/// <returns>逐元素 max 结果；按 MIXED 装载，数值档位随输入。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 116（tuple_max2 语义）；两路入参钉固定/解固定在本方法内部
	///   成对完成。</para>
	///   <para><b>约束或前提</b>：要求可比较的元素类型；两路长度不等时的广播/报错规则在原生侧 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：把值压进 [min, max] 区间可用一次 <c>Max2</c> 加一次
	///   <see cref="TupleMin2(JlTuple)"/>；求整个元组的最大值用 <see cref="TupleMax()"/>（单值输出）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new double[] { -1.0, 5.0 };
	///   JlTuple clipped = v.TupleMax2(0.0);
	///   </code>
	///   <para><b>资源与坑</b>：示例中单元素 <c>0.0</c> 经隐式转换升为元组；它是否被广播到每一位置
	///   [待实测]。</para>
	/// </remarks>
	public JlTuple TupleMax2(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(116);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>与 <paramref name="t2"/> 逐元素取较小值（常用作上限钳位），返回新元组。</summary>
	/// <param name="t2">第二路输入元组。</param>
	/// <returns>逐元素 min 结果；按 MIXED 装载，数值档位随输入。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 117（tuple_min2 语义）；两路入参钉固定/解固定在本方法内部
	///   成对完成。</para>
	///   <para><b>约束或前提</b>：要求可比较的元素类型；两路长度不等时的广播/报错规则在原生侧 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：下限钳位用 <see cref="TupleMax2(JlTuple)"/>；整个元组的最小值用
	///   <see cref="TupleMin()"/>（单值输出）。两者组合可把灰度值压进任意区间。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new double[] { -1.0, 5.0 };
	///   JlTuple clipped = v.TupleMin2(3.0);
	///   </code>
	/// </remarks>
	public JlTuple TupleMin2(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(117);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>求整个元组的最大元素，输出为单值元组。</summary>
	/// <returns>只含最大值的新元组（MIXED 装载，类型档位随输入）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 118（tuple_max 语义）；钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：字符串元组的比较口径（字典序？）与空元组行为 [待实测]；MIXED 元组
	///   数值与字符串混排时结果不可预期，先筛再求。</para>
	///   <para><b>与相邻算子的取舍</b>：要最大值的<b>下标</b>用 <see cref="TupleFind(JlTuple)"/> 配合
	///   <see cref="TupleSortIndex()"/>，或对结果再找位置；逐元素与另一元组比大小用
	///   <see cref="TupleMax2(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new double[] { 1.5, 8.25, 3.0 };
	///   double m = v.TupleMax();
	///   </code>
	///   <para><b>资源与坑</b>：结果即单值元组，<c>double m = v.TupleMax();</c> 这类隐式转换只取第一个
	///   元素（此处恰为最大值本身）。</para>
	/// </remarks>
	public JlTuple TupleMax()
	{
		IntPtr proc = JlNativeApi.PreCall(118);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>求整个元组的最小元素，输出为单值元组。</summary>
	/// <returns>只含最小值的新元组（MIXED 装载，类型档位随输入）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 119（tuple_min 语义）；钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：与 <see cref="TupleMax()"/> 对称：字符串比较口径与空元组行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：归一化时常与 <see cref="TupleMax()"/> 成对使用求值域两端；
	///   逐元素上限钳位改用 <see cref="TupleMin2(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new double[] { 1.5, 8.25, 3.0 };
	///   double m = v.TupleMin();
	///   </code>
	/// </remarks>
	public JlTuple TupleMin()
	{
		IntPtr proc = JlNativeApi.PreCall(119);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐位置累加（前缀和）：结果第 i 项 = 输入前 i+1 项之和；最后一项即 <see cref="TupleSum()"/> 的值。</summary>
	/// <returns>与输入等长的新元组，数值档位随输入（MIXED 口径装载）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 120（tuple_cumul 语义）；钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：输入须为数值元组；对字符串元组的行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只要总和用 <see cref="TupleSum()"/>（单值，开销更小）；本方法用于
	///   分布函数、按累计权重抽样等需要每一位部分和的场合。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple w = new int[] { 1, 2, 3 };
	///   JlTuple cw = w.TupleCumul();
	///   int[] v = cw;    // v = [1, 3, 6]（按前缀和语义）
	///   </code>
	/// </remarks>
	public JlTuple TupleCumul()
	{
		IntPtr proc = JlNativeApi.PreCall(120);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>按"排名"取元素：返回排序后第 <paramref name="rankIndex"/> 位的值，不改动本元组。</summary>
	/// <param name="rankIndex">名次（单值；0 = 最小值，<see cref="Length"/>−1 = 最大值，若为 0 基）[待实测]。</param>
	/// <returns>选中的元素（单值新元组，类型档位随输入）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 121（tuple_select_rank 语义）；两路入参钉固定/解固定在本方法
	///   内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：与 <see cref="TupleSort()"/> 后按下标取相比，本方法不产生整个有序
	///   副本，取单个分位点更省；中位数直接考虑 <see cref="TupleMedian()"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new double[] { 7.0, 1.0, 4.0 };
	///   double lowest = v.TupleSelectRank(0);
	///   </code>
	///   <para><b>资源与坑</b>：名次超出元素数时的行为（报错或截断）[待实测]。</para>
	/// </remarks>
	public JlTuple TupleSelectRank(JlTuple rankIndex)
	{
		IntPtr proc = JlNativeApi.PreCall(121);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, rankIndex);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(rankIndex);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>求元组元素的中位数（单值输出）。</summary>
	/// <returns>只含中位数的新元组；元素数为偶数时取法（中间两数平均或取下者）[待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 122（tuple_median 语义）；钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：对离群值比 <see cref="TupleMean()"/> 稳；要任意分位数用
	///   <see cref="TupleSelectRank(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new double[] { 7.0, 1.0, 4.0, 100.0 };
	///   double med = v.TupleMedian();
	///   </code>
	///   <para><b>资源与坑</b>：不改动输入元组顺序；它是"一组数的中位数"，与图像中值滤波无关。</para>
	/// </remarks>
	public JlTuple TupleMedian()
	{
		IntPtr proc = JlNativeApi.PreCall(122);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>求全部元素之和（单值输出）。</summary>
	/// <returns>只含总和的新元组，数值档位随输入（整数输入得整数和）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 123（tuple_sum 语义）；钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：输入须为数值元组；需要逐位部分和用 <see cref="TupleCumul()"/>（其末项
	///   与本方法同值）。</para>
	///   <para><b>与相邻算子的取舍</b>：要平均数用 <see cref="TupleMean()"/>，它内部等价于 Sum 除以
	///   <see cref="Length"/> 且总是按 DOUBLE 装载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple w = new int[] { 1, 2, 3 };
	///   int total = w.TupleSum();
	///   </code>
	/// </remarks>
	public JlTuple TupleSum()
	{
		IntPtr proc = JlNativeApi.PreCall(123);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>求数值元组的算术平均，输出按 DOUBLE 装载的单值元组。</summary>
	/// <returns>平均值（DOUBLE——即使输入全为整数，结果也带小数，不会整数截断）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 124（tuple_mean 语义）；钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：要求数值元组；空元组除零行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：易受离群值拉扯，抗噪场景改用 <see cref="TupleMedian()"/>；
	///   需要离散度时配 <see cref="TupleDeviation()"/> 一起用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new int[] { 1, 2, 4 };
	///   double m = v.TupleMean();
	///   </code>
	/// </remarks>
	public JlTuple TupleMean()
	{
		IntPtr proc = JlNativeApi.PreCall(124);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>求元组元素的标准差（DOUBLE 装载的单值输出）。</summary>
	/// <returns>标准差；是总体标准差（除以 N）还是样本标准差（除以 N−1）[待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 125（tuple_deviation 语义）；输出按 DOUBLE 装载——整数输入
	///   也会得到带小数的离散度；钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：阈值自适应常取 <c>Mean ± k·Deviation</c>，两者都要单独调用；
	///   要值域两端用 <see cref="TupleMax()"/>/<see cref="TupleMin()"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new double[] { 1.0, 2.0, 3.0 };
	///   double sd = v.TupleDeviation();
	///   </code>
	///   <para><b>资源与坑</b>：单元素元组返回 0 还是异常 [待实测]。</para>
	/// </remarks>
	public JlTuple TupleDeviation()
	{
		IntPtr proc = JlNativeApi.PreCall(125);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>只去掉"相邻的重复"：连续相同元素仅保留一个，返回可能变短的新元组。</summary>
	/// <returns>去除相邻重复后的元组，顺序保持；不相邻的重复元素不会被删除。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 126（tuple_uniq 语义）；钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：想全量去重必须先 <see cref="TupleSort()"/> 再本方法；集合式去重
	///   可用 <c>tuple.TupleUnion(空元组)</c> 的集合并语义。乱序数据直接调用只会合并"连号"重复。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new int[] { 1, 1, 2, 1 };
	///   JlTuple u = v.TupleUniq();    // 语义上得 [1, 2, 1]
	///   </code>
	///   <para><b>资源与坑</b>：结果长度可能小于输入，后续 <see cref="TupleSelect(JlTuple)"/> 的旧下标全部失效。</para>
	/// </remarks>
	public JlTuple TupleUniq()
	{
		IntPtr proc = JlNativeApi.PreCall(126);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>从尾部向前找 <paramref name="toFind"/>（作为子序列）最后一次出现的起始下标。</summary>
	/// <param name="toFind">要查找的值（单值或多值，多值按连续子序列匹配）。</param>
	/// <returns>INTEGER 型单值元组：起始下标；未找到时为 <c>-1</c> [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 127（tuple_find_last 语义），输出按 INTEGER 装载；两路入参
	///   钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：下标基（0 基/1 基）以原生侧为准 [待实测]——与托管索引器
	///   <c>this[i]</c>（0 基）配合使用时先换算。</para>
	///   <para><b>与相邻算子的取舍</b>：第一次出现用 <see cref="TupleFindFirst(JlTuple)"/>；要所有出现位置用
	///   <see cref="TupleFind(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple hay = new int[] { 3, 7, 3, 9 };
	///   int idx = hay.TupleFindLast(3);
	///   </code>
	///   <para><b>资源与坑</b>：返回 -1 时若不加判断直接喂给 <c>this[i]</c>，会抛
	///   <see cref="JlTupleAccessException"/>（负下标越界）。</para>
	/// </remarks>
	public JlTuple TupleFindLast(JlTuple toFind)
	{
		IntPtr proc = JlNativeApi.PreCall(127);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, toFind);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(toFind);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>从头向后找 <paramref name="toFind"/>（作为子序列）第一次出现的起始下标。</summary>
	/// <param name="toFind">要查找的值（单值或多值，多值按连续子序列匹配）。</param>
	/// <returns>INTEGER 型单值元组：起始下标；未找到时为 <c>-1</c> [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 128（tuple_find_first 语义），输出按 INTEGER 装载；两路入参
	///   钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：只要一个位置时 First/Last 都比 <see cref="TupleFind(JlTuple)"/>
	///   语义明确；需要全部命中位置（如把所有某值的下标收集出来再 <see cref="TupleSelect(JlTuple)"/>）
	///   用 <c>Find</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple hay = new int[] { 3, 7, 3, 9 };
	///   int idx = hay.TupleFindFirst(7);
	///   </code>
	///   <para><b>资源与坑</b>：下标基以原生侧为准 [待实测]；未命中的 <c>-1</c> 传入索引器会抛
	///   <see cref="JlTupleAccessException"/>。</para>
	/// </remarks>
	public JlTuple TupleFindFirst(JlTuple toFind)
	{
		IntPtr proc = JlNativeApi.PreCall(128);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, toFind);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(toFind);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>查找 <paramref name="toFind"/> 在本元组中的出现位置，返回下标集合（INTEGER 装载）。</summary>
	/// <param name="toFind">要查找的值。</param>
	/// <returns>命中位置的下标元组；未找到的项以 <c>-1</c> 占位还是直接缺席 [待实测]。对
	/// <paramref name="toFind"/> 逐元素给位置、还是给出全部命中点 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 129（tuple_find 语义）；两路入参钉固定/解固定在本方法内部
	///   成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：只要"首个/末个位置"用 <see cref="TupleFindFirst(JlTuple)"/>/
	///   <see cref="TupleFindLast(JlTuple)"/>，结果恒为单值，消费更省心。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple hay = new int[] { 3, 7, 3, 9 };
	///   JlTuple where = hay.TupleFind(3);
	///   int[] idxs = where;
	///   </code>
	///   <para><b>资源与坑</b>：下标可能含 <c>-1</c>，未过滤直接传给 <c>this[i]</c> 会抛
	///   <see cref="JlTupleAccessException"/>。</para>
	/// </remarks>
	public JlTuple TupleFind(JlTuple toFind)
	{
		IntPtr proc = JlNativeApi.PreCall(129);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, toFind);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(toFind);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>排序并返回"名次→原下标"的置换索引元组，本元组不变。</summary>
	/// <returns>INTEGER 型下标元组，等长于输入；用它配 <see cref="TupleSelect(JlTuple)"/> 可复现 <see cref="TupleSort()"/> 的结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 130（tuple_sort_index 语义），输出按 INTEGER 装载；
	///   钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：只要有序值用 <see cref="TupleSort()"/>；需要"排序后还能追回原始
	///   对象"（如按分数排序后保留对应文件名）才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new double[] { 5.0, 1.0, 3.0 };
	///   JlTuple order = v.TupleSortIndex();
	///   JlTuple sorted = v.TupleSelect(order);
	///   </code>
	///   <para><b>资源与坑</b>：下标基以原生侧为准 [待实测]，配合索引器 <c>this[i]</c>（0 基）前先验证；
	///   稳定排序与否（相等元素保持原序？）[待实测]。</para>
	/// </remarks>
	public JlTuple TupleSortIndex()
	{
		IntPtr proc = JlNativeApi.PreCall(130);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>升序排序元素值，返回新的有序元组，本元组不变。</summary>
	/// <returns>升序结果（MIXED 口径装载，类型档位随输入）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 131（tuple_sort 语义）；钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：字符串按字典序、数值按大小；MIXED 元组中数值与字符串混排的次序
	///   [待实测]。降序可对结果调用 <see cref="TupleInverse()"/>。</para>
	///   <para><b>与相邻算子的取舍</b>：要"全量去重"先 Sort 再 <see cref="TupleUniq()"/>（Uniq 只删相邻重复）；
	///   需要原始下标映射时用 <see cref="TupleSortIndex()"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new double[] { 5.0, 1.0, 3.0 };
	///   JlTuple s = v.TupleSort();
	///   double lowest = s[0].D;
	///   </code>
	/// </remarks>
	public JlTuple TupleSort()
	{
		IntPtr proc = JlNativeApi.PreCall(131);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把元素次序整体反转（首尾对调），返回新元组。</summary>
	/// <returns>与原元组等长、顺序相反的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 132（tuple_inverse 语义）；钉固定/解固定在本方法内部成对完成。
	///   注意是"倒序"，不是数学求逆——数值元组取倒数请用 <c>1.0 / t</c> 一类的除法运算。</para>
	///   <para><b>与相邻算子的取舍</b>：升序排完想要降序，直接对 <see cref="TupleSort()"/> 的结果再调本方法，
	///   比自定义比较更方便。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new int[] { 1, 2, 3 };
	///   JlTuple r = v.TupleInverse();
	///   int first = r.I;    // first == 3
	///   </code>
	/// </remarks>
	public JlTuple TupleInverse()
	{
		IntPtr proc = JlNativeApi.PreCall(132);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>原生实现：把当前元组与 t2 首尾拼接成新元组，结果按 MIXED 装载。</summary>
	/// <param name="t2">接到当前元组之后的元组。</param>
	/// <returns>拼接后的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 133，tuple_concat）。结果类型按需升级（空元组让位于非空、类型不一致升级为 <c>MIXED</c>）。</para>
	///   <para><b>资源与坑</b>：LoadNew 装载新元组，不改本元组或 t2；句柄类元素以引用方式共享持有。</para>
	///   <para><b>相关算子</b>：<c>TupleConcat</c></para>
	/// </remarks>
	private JlTuple TupleConcatOp(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(133);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>截取连续一段：从下标 <paramref name="leftindex"/> 到 <paramref name="rightindex"/>（两端含闭 [待实测]）的元素。</summary>
	/// <param name="leftindex">首个被选元素的下标。</param>
	/// <param name="rightindex">末个被选元素的下标。</param>
	/// <returns>选中片段组成的新元组；顺序不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 134（tuple_select_range 语义）；两路下标的钉固定/解固定在
	///   本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：下标基（0/1 基）与越界（如右端超过 <see cref="Length"/>−1）的处理 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：非连续的任意位置用 <see cref="TupleSelect(JlTuple)"/>；固定从头部/
	///   尾部截取用 <see cref="TupleFirstN(JlTuple)"/>/<see cref="TupleLastN(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new int[] { 10, 20, 30, 40 };
	///   JlTuple seg = v.TupleSelectRange(1, 2);
	///   </code>
	/// </remarks>
	public JlTuple TupleSelectRange(JlTuple leftindex, JlTuple rightindex)
	{
		IntPtr proc = JlNativeApi.PreCall(134);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, leftindex);
		JlNativeApi.Store(proc, 2, rightindex);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(leftindex);
		JlNativeApi.UnpinTuple(rightindex);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>从下标 <paramref name="index"/> 起到元组末尾的所有元素（参数是"起始下标"，不是个数）。</summary>
	/// <param name="index">首个被选元素的下标。</param>
	/// <returns>尾部片段新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 135（tuple_last_n 语义）；下标钉固定/解固定在本方法内部
	///   成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：名字带 "N" 但收的是下标；要"末尾 k 个"得先取
	///   <c>Length − k</c>（注意下标基 [待实测]），或整段对比 <see cref="TupleSelectRange(JlTuple, JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new int[] { 10, 20, 30, 40 };
	///   JlTuple tail = v.TupleLastN(2);
	///   </code>
	///   <para><b>资源与坑</b>：<c>index</c> 超过末下标时结果为空元组还是越界报错 [待实测]。</para>
	/// </remarks>
	public JlTuple TupleLastN(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(135);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(index);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>从头取下标 <paramref name="index"/> 为止的所有元素（参数是"末个元素的下标"，不是个数）。</summary>
	/// <param name="index">末个被选元素的下标。</param>
	/// <returns>头部片段新元组（含闭端 [待实测]）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 136（tuple_first_n 语义）；下标钉固定/解固定在本方法内部
	///   成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：要"前 k 个元素"且按个数传参，本方法并不满足——需把个数换算成
	///   下标（考虑 0/1 基 [待实测]）；对称的尾部截取用 <see cref="TupleLastN(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new int[] { 10, 20, 30, 40 };
	///   JlTuple head = v.TupleFirstN(1);
	///   </code>
	/// </remarks>
	public JlTuple TupleFirstN(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(136);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(index);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>在下标 <paramref name="index"/> 处插入 <paramref name="insertTuple"/> 的各元素，其后元素整体后移；返回加长后的新元组。</summary>
	/// <param name="index">插入位置（起始下标）。</param>
	/// <param name="insertTuple">要插入的一个或多个元素。</param>
	/// <returns>插入结果新元组，长度为原长 + <c>insertTuple.Length</c>；本元组不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 137（tuple_insert 语义）；两路入参钉固定/解固定在本方法内部
	///   成对完成。</para>
	///   <para><b>约束或前提</b>：插入位置的下标基 [待实测]；插入类型与原有元素不一致时整体升级 MIXED。</para>
	///   <para><b>与相邻算子的取舍</b>：覆盖已有位置用 <see cref="TupleReplace(JlTuple, JlTuple)"/>（长度不变）；
	///   只在尾部追加用 <see cref="Append(JlTuple)"/> 或 <see cref="TupleConcat(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new int[] { 1, 2, 3 };
	///   JlTuple w = v.TupleInsert(1, 99);
	///   </code>
	///   <para><b>资源与坑</b>：插入后原元组的旧下标整体错位，凡缓存过下标的后续逻辑都要重算。</para>
	/// </remarks>
	public JlTuple TupleInsert(JlTuple index, JlTuple insertTuple)
	{
		IntPtr proc = JlNativeApi.PreCall(137);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, insertTuple);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(insertTuple);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>覆盖式替换下标 <paramref name="index"/> 处的元素，长度不变，返回新元组。</summary>
	/// <param name="index">被替换元素的下标（可多值）。</param>
	/// <param name="replaceTuple">替换用的元素（单值广播或与 index 逐位对应）。</param>
	/// <returns>替换结果新元组，长度与本元组一致；本元组不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 138（tuple_replace 语义）；两路入参钉固定/解固定在本方法内部
	///   成对完成。</para>
	///   <para><b>约束或前提</b>：多下标对多替换值的配对规则（逐位对应/广播）[待实测]；替换值类型与原
	///   元素不一致时结果升级 MIXED。</para>
	///   <para><b>与相邻算子的取舍</b>：不覆盖而是"挤入"新位置用 <see cref="TupleInsert(JlTuple, JlTuple)"/>；
	///   只改单点也可以直接用索引器写入 <c>t[i] = ...</c>（原地、无原生调用）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new int[] { 1, 2, 3 };
	///   using JlTuple w = v.TupleReplace(1, 99);   // w == (1, 99, 3)，v 不变
	///   int at = w[1];                             // 99
	///   v.Dispose();
	///   </code>
	/// </remarks>
	public JlTuple TupleReplace(JlTuple index, JlTuple replaceTuple)
	{
		IntPtr proc = JlNativeApi.PreCall(138);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, replaceTuple);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(replaceTuple);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>删除下标 <paramref name="index"/> 处的元素，返回缩短后的新元组。</summary>
	/// <param name="index">要删除的一个或多个下标。</param>
	/// <returns>删除后的新元组，长度为原长 − 实际删除数；本元组不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 139（tuple_remove 语义）；下标元组钉固定/解固定在本方法内部
	///   成对完成。</para>
	///   <para><b>约束或前提</b>：重复下标只删一次还是多次删除、越界下标忽略还是报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：反向操作（按掩码"保留"）用 <see cref="TupleSelectMask(JlTuple)"/>；
	///   逻辑取反掩码后即可当作删除使用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new int[] { 10, 20, 30, 40 };
	///   JlTuple w = v.TupleRemove(new int[] { 0, 2 });
	///   </code>
	///   <para><b>资源与坑</b>：删除会使后续元素下标整体前移，逐个删除时从大下标往小下标删才不会错位。</para>
	/// </remarks>
	public JlTuple TupleRemove(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(139);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(index);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>按掩码逐位筛选：<c>mask</c> 中大于 0 的位置对应的元素被保留，返回新元组。</summary>
	/// <param name="mask">与元组逐位对应的数值掩码，取严格大于 0 者为"选中"。</param>
	/// <returns>被选中元素组成的新元组，顺序不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 140（tuple_select_mask 语义）；掩码钉固定/解固定在本方法内部
	///   成对完成。逐位条件筛选要配 <c>*Elem</c> 系比较（如 <see cref="TupleGreaterElem(JlTuple)"/>）——
	///   注意运算符 <c>v &gt; 4.0</c> 返回的是<b>单个 bool</b>，不是逐位 0/1 元组。</para>
	///   <para><b>约束或前提</b>：掩码长度应与元组一致；不等长时按哪一方截断 [待实测]。注意判据是
	///   <c>&gt; 0</c>，负值与 0 都算不选。</para>
	///   <para><b>与相邻算子的取舍</b>：已知具体下标用 <see cref="TupleSelect(JlTuple)"/>（可重复选取同一
	///   位置，本方法不行）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new double[] { 1.0, 9.0, 5.0 };
	///   JlTuple m = v.TupleGreaterElem(4.0);
	///   JlTuple kept = v.TupleSelectMask(m);
	///   </code>
	/// </remarks>
	public JlTuple TupleSelectMask(JlTuple mask)
	{
		IntPtr proc = JlNativeApi.PreCall(140);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, mask);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(mask);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>按下标取元素：结果按 <paramref name="index"/> 给出的顺序逐位组装，可重复取同一位置。</summary>
	/// <param name="index">一个或多个下标（顺序即输出顺序）。</param>
	/// <returns>选出的元素新元组，长度等于 <c>index.Length</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 141（tuple_select 语义）；下标元组钉固定/解固定在本方法内部
	///   成对完成。托管侧等价的用法是索引器 <c>t[new[] { 2, 0, 2 }]</c>（返回 <see cref="JlTupleElements"/>
	///   视图），无需走原生。</para>
	///   <para><b>约束或前提</b>：下标基以原生侧为准 [待实测]（托管索引器是 0 基并会抛
	///   <see cref="JlTupleAccessException"/>）；越界下标的原生行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：整段连续区间用 <see cref="TupleSelectRange(JlTuple, JlTuple)"/> 更省；
	///   按名次取单个分位点用 <see cref="TupleSelectRank(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple v = new int[] { 10, 20, 30 };
	///   JlTuple s = v.TupleSelect(new int[] { 2, 0 });
	///   </code>
	/// </remarks>
	public JlTuple TupleSelect(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(141);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(index);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>从字符串元素中按下标选字符、从整数元素中按位序选位（bit）。</summary>
	/// <param name="index">字符位置或位序（可多值）。</param>
	/// <returns>选出的字符/位组成的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 142（tuple_str_bit_select 语义）；下标元组钉固定/解固定在
	///   本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：位序方向（LSB=0 还是 MSB=0）、整数按 32 位还是 64 位展开、字符位置
	///   的下标基 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：整段字符用 <see cref="TupleSubstr(JlTuple, JlTuple)"/>；本方法面向
	///   "按位取标志"式的解码场景。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new string[] { "abc", "xyz" };
	///   JlTuple chars = s.TupleStrBitSelect(0);
	///   </code>
	///   <para><b>资源与坑</b>：对 MIXED 元组，字符串位选字符、整数位选 bit，两口径混在一趟调用里，
	///   输出类型可能不齐。</para>
	/// </remarks>
	public JlTuple TupleStrBitSelect(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(142);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(index);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>生成等差序列：从 <paramref name="start"/> 出发按 <paramref name="step"/> 递增，末项不超过 <paramref name="end"/>（静态方法）。</summary>
	/// <param name="start">首项值。</param>
	/// <param name="end">末项允许达到的最大值（不一定恰好取到，见坑）。</param>
	/// <param name="step">步长（可为负以生成降序）。</param>
	/// <returns>序列新元组，数值档位随入参（整数入参得整数元组）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 143（tuple_gen_sequence 语义）。三个入参各自钉固定/解固定，
	///   在本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：<paramref name="end"/> 是"不超过"的上界：当 (end−start) 不能被 step
	///   整除时末项会小于 end（如 0..1 步长 0.3 得不到 1.0），这是浮点步长最常踩的偏差 [待实测确认舍入细节]。</para>
	///   <para><b>与相邻算子的取舍</b>：要"同值填充"用 <see cref="TupleGenConst(JlTuple, JlTuple)"/>；
	///   要随机数用 <see cref="TupleRand(JlTuple)"/>。本方法适合确定性的网格/采样点序列。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple seq = JlTuple.TupleGenSequence(0, 10, 2);
	///   int n = seq.Length;
	///   </code>
	/// </remarks>
	public static JlTuple TupleGenSequence(JlTuple start, JlTuple end, JlTuple step)
	{
		IntPtr proc = JlNativeApi.PreCall(143);
		JlNativeApi.Store(proc, 0, start);
		JlNativeApi.Store(proc, 1, end);
		JlNativeApi.Store(proc, 2, step);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(start);
		JlNativeApi.UnpinTuple(end);
		JlNativeApi.UnpinTuple(step);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>生成长度为 <paramref name="length"/>、每个元素都等于 <paramref name="constVal"/> 的新元组（静态方法）。</summary>
	/// <param name="length">元素个数（单值）。</param>
	/// <param name="constVal">填充值；其类型决定元组类型（传字符串得 STRING 元组）。</param>
	/// <returns>填充结果新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 144（tuple_gen_const 语义）；两个入参钉固定/解固定在本方法
	///   内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：变步长序列用 <see cref="TupleGenSequence(JlTuple, JlTuple, JlTuple)"/>；
	///   本方法是"占位/初始化"工具，例如先把结果整片填 0，再逐点用索引器写入。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple zeros = JlTuple.TupleGenConst(4, 0);
	///   JlTuple tags = JlTuple.TupleGenConst(4, "unset");
	///   </code>
	///   <para><b>资源与坑</b>：<paramref name="length"/> 传非正数时的行为（空元组或报错）[待实测]。</para>
	/// </remarks>
	public static JlTuple TupleGenConst(JlTuple length, JlTuple constVal)
	{
		IntPtr proc = JlNativeApi.PreCall(144);
		JlNativeApi.Store(proc, 0, length);
		JlNativeApi.Store(proc, 1, constVal);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(length);
		JlNativeApi.UnpinTuple(constVal);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>以本元组为"环境变量名列表"，读出对应的环境变量值（字符串元组）。</summary>
	/// <returns>各变量值组成的新元组，顺序与入参名一致；未定义变量的占位行为 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 145（tuple_environment 语义）；本元组只当"名字"用，
	///   钉固定/解固定在本方法内部成对完成，读取的是<b>原生运行时进程</b>的环境而非托管进程环境。</para>
	///   <para><b>与相邻成员的取舍</b>：需要 .NET 侧环境变量请直接用 <c>System.Environment.GetEnvironmentVariable</c>；
	///   仅当要读原生视觉库自己识别的变量（如根目录类变量）时才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple names = new string[] { "PATH" };
	///   JlTuple values = names.TupleEnvironment();
	///   string path = values.S;
	///   </code>
	///   <para><b>资源与坑</b>：结果隐式转 string 只取第一个值；多变量时按 <c>values[i].S</c> 逐项读。</para>
	/// </remarks>
	public JlTuple TupleEnvironment()
	{
		IntPtr proc = JlNativeApi.PreCall(145);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>按分隔符把字符串元组的每个元素拆成多段，返回展开后的字符串新元组。</summary>
	/// <param name="separator">分隔符集合（可多值，任一命中即拆分）。</param>
	/// <returns>全部片段按原顺序拼接展开的新元组；元素数会因拆分而增加。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 146（tuple_split 语义）；分隔符元组钉固定/解固定在本方法
	///   内部成对完成。</para>
	///   <para><b>约束或前提</b>：输入应为字符串元组；连续分隔符产生空段还是被跳过 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：反向操作（用分隔符把片段并回一个串）是 <see cref="TupleJoin(JlTuple)"/>；
	///   按固定字符位置切段用 <see cref="TupleSubstr(JlTuple, JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new string[] { "a,b;c" };
	///   JlTuple parts = s.TupleSplit(new string[] { ",", ";" });
	///   </code>
	///   <para><b>资源与坑</b>：拆分后下标与输入不再一一对应，回填前需自行记录边界。</para>
	/// </remarks>
	public JlTuple TupleSplit(JlTuple separator)
	{
		IntPtr proc = JlNativeApi.PreCall(146);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, separator);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(separator);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>按字符位置截取：从 <paramref name="position1"/> 到 <paramref name="position2"/>（两端含闭）的子串。</summary>
	/// <param name="position1">起始字符位置（可多值，与本元组逐位对应）。</param>
	/// <param name="position2">末字符位置。</param>
	/// <returns>截出的子串新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 147（tuple_substr 语义）；两路位置元组钉固定/解固定在
	///   本方法内部成对完成。</para>
	///   <para><b>约束或前提</b>：字符位置的下标基 [待实测]；越界位置截到串尾还是报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只知道起点要"到串尾"用 <see cref="TupleStrLastN(JlTuple)"/>；
	///   按分隔符切段用 <see cref="TupleSplit(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new string[] { "abcdef" };
	///   JlTuple cut = s.TupleSubstr(1, 3);
	///   </code>
	/// </remarks>
	public JlTuple TupleSubstr(JlTuple position1, JlTuple position2)
	{
		IntPtr proc = JlNativeApi.PreCall(147);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, position1);
		JlNativeApi.Store(proc, 2, position2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(position1);
		JlNativeApi.UnpinTuple(position2);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>从字符位置 <paramref name="position"/> 起截取到串尾（参数是起始位置，不是个数）。</summary>
	/// <param name="position">首个被保留字符的位置（可多值，逐串对应）。</param>
	/// <returns>尾部子串新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 148（tuple_str_last_n 语义）；位置元组钉固定/解固定在本方法
	///   内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：元组元素级的尾部截取是 <see cref="TupleLastN(JlTuple)"/>，本方法是
	///   <b>字符串内部</b>的字符级截取，两级"LastN"容易混。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new string[] { "abcdef", "xyz" };
	///   JlTuple tail = s.TupleStrLastN(2);
	///   </code>
	///   <para><b>资源与坑</b>：位置下标基 [待实测]；位置超过串长时得空串还是报错 [待实测]。</para>
	/// </remarks>
	public JlTuple TupleStrLastN(JlTuple position)
	{
		IntPtr proc = JlNativeApi.PreCall(148);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, position);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(position);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>截取串头到字符位置 <paramref name="position"/> 为止（参数是末个保留字符的位置，不是个数）。</summary>
	/// <param name="position">末个被保留字符的位置（可多值，逐串对应）。</param>
	/// <returns>头部子串新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 149（tuple_str_first_n 语义）；位置元组钉固定/解固定在
	///   本方法内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：对称的串尾截取用 <see cref="TupleStrLastN(JlTuple)"/>；要"前 k 个
	///   字符"须先把个数换算成位置（注意下标基 [待实测]），或改用 <see cref="TupleSubstr(JlTuple, JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new string[] { "abcdef", "xyz" };
	///   JlTuple head = s.TupleStrFirstN(1);
	///   </code>
	/// </remarks>
	public JlTuple TupleStrFirstN(JlTuple position)
	{
		IntPtr proc = JlNativeApi.PreCall(149);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, position);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(position);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>在字符串元组内自后向前搜索字符，返回命中位置（INTEGER 装载）。</summary>
	/// <param name="toFind">要查找的字符（逐串对应，非子串搜索）。</param>
	/// <returns>各串中该字符最后一次出现的位置；未命中的表示法 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 150（tuple_strrchr 语义）；输出按 INTEGER 装载，
	///   <paramref name="toFind"/> 钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：找首个出现用 <see cref="TupleStrchr(JlTuple)"/>；找<b>子串</b>用
	///   <see cref="TupleStrrstr(JlTuple)"/>/<see cref="TupleStrstr(JlTuple)"/>——chr 系按字符、str 系按子串。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new string[] { "banana" };
	///   int pos = s.TupleStrrchr("a");
	///   </code>
	///   <para><b>资源与坑</b>：位置的下标基与逐串对应关系先验证再用；结果隐式转 int 只取第一个位置。</para>
	/// </remarks>
	public JlTuple TupleStrrchr(JlTuple toFind)
	{
		IntPtr proc = JlNativeApi.PreCall(150);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, toFind);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(toFind);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>在字符串元组内自前向后搜索字符，返回命中位置（INTEGER 装载）。</summary>
	/// <param name="toFind">要查找的字符（逐串对应，非子串搜索）。</param>
	/// <returns>各串中该字符第一次出现的位置；未命中的表示法 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 151（tuple_strchr 语义）；输出按 INTEGER 装载，
	///   <paramref name="toFind"/> 钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：最后一次出现用 <see cref="TupleStrrchr(JlTuple)"/>；找子串用
	///   <see cref="TupleStrstr(JlTuple)"/>。典型用法：找到分隔字符位置后配合
	///   <see cref="TupleSubstr(JlTuple, JlTuple)"/> 手动切串。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new string[] { "banana" };
	///   int pos = s.TupleStrchr("a");
	///   </code>
	///   <para><b>资源与坑</b>：位置下标基 [待实测]；隐式转 int 只取第一条串的命中位置。</para>
	/// </remarks>
	public JlTuple TupleStrchr(JlTuple toFind)
	{
		IntPtr proc = JlNativeApi.PreCall(151);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, toFind);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(toFind);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>在字符串元组内自后向前搜索<b>子串</b>，返回最后一次命中的起始位置（INTEGER 装载）。</summary>
	/// <param name="toFind">要查找的子串（逐串对应）。</param>
	/// <returns>各串中子串最后一次出现的位置；未命中的表示法 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 152（tuple_strrstr 语义）；输出按 INTEGER 装载，
	///   <paramref name="toFind"/> 钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：常用在"从路径里剥出文件名"——先 <c>Strrstr("/")</c> 拿最后
	///   分隔位，再配 <see cref="TupleStrLastN(JlTuple)"/> 截尾；单字符搜索用
	///   <see cref="TupleStrrchr(JlTuple)"/>（语义是字符不是子串）；向前找用 <see cref="TupleStrstr(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new string[] { "a/b/c.txt" };
	///   int pos = s.TupleStrrstr("/");
	///   </code>
	///   <para><b>资源与坑</b>：位置下标基 [待实测]。</para>
	/// </remarks>
	public JlTuple TupleStrrstr(JlTuple toFind)
	{
		IntPtr proc = JlNativeApi.PreCall(152);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, toFind);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(toFind);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>在字符串元组内自前向后搜索<b>子串</b>，返回第一次命中的起始位置（INTEGER 装载）。</summary>
	/// <param name="toFind">要查找的子串（逐串对应）。</param>
	/// <returns>各串中子串第一次出现的位置；未命中的表示法 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 153（tuple_strstr 语义）；输出按 INTEGER 装载，
	///   <paramref name="toFind"/> 钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻算子的取舍</b>：元组<b>元素级</b>的"值在不在"用 <see cref="TupleFind(JlTuple)"/>，
	///   本方法是<b>串内部</b>的子串定位；要末次命中用 <see cref="TupleStrrstr(JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new string[] { "img_001.bmp", "log_002.txt" };
	///   JlTuple at = s.TupleStrstr("_");
	///   int[] v = at;
	///   </code>
	///   <para><b>资源与坑</b>：位置下标基 [待实测]。</para>
	/// </remarks>
	public JlTuple TupleStrstr(JlTuple toFind)
	{
		IntPtr proc = JlNativeApi.PreCall(153);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, toFind);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(toFind);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素返回字符串长度，得到与原元组等长的 INTEGER 长度元组。</summary>
	/// <returns>每个字符串的字符数（整数元组）；非字符串元素的行为 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：原生算子 id 154（tuple_strlen 语义）；输出按 INTEGER 装载，
	///   钉固定/解固定在本方法内部成对完成。</para>
	///   <para><b>与相邻成员的取舍</b>：别与元组自己的 <see cref="Length"/>（元素个数）混淆——本方法数的是
	///   <b>每个串里的字符</b>；判"有无空串"用它比 <see cref="TupleEqualElem(JlTuple)"/> 传空串更直观。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new string[] { "abc", "" };
	///   JlTuple lens = s.TupleStrlen();
	///   int[] v = lens;    // [3, 0]
	///   </code>
	///   <para><b>资源与坑</b>：结果整体转 int 只会拿到第一个串的长度。</para>
	/// </remarks>
	public JlTuple TupleStrlen()
	{
		IntPtr proc = JlNativeApi.PreCall(154);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素判断本元组是否"小于等于"t2，返回按位 0/1 的整数元组。</summary>
	/// <param name="t2">比较的右操作数元组。</param>
	/// <returns>逐位为 1（本元组元素 ≤ 对应 t2 元素）或 0 的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生比较算子（id 155），逐位比较，命中记 1、否则记 0。</para>
	///   <para><b>约束或前提</b>：结果长度取决于原生 *_elem 语义对两操作数长度不等时的处理（标量广播或报错），
	///   与不带 _elem 的 <see cref="TupleLessEqual"/> 的确切差异 [待实测]；比较前宜自行对齐长度以免静默错位。</para>
	///   <para><b>与相邻算子的取舍</b>：需要"小于"用 <see cref="TupleLessElem"/>，需要"等于"用 <see cref="TupleEqualElem"/>。</para>
	///   <para><b>钉固定</b>：<c>Store(proc,0)</c> 经 InitPCT 钉住本元组、<c>JlNativeApi.Store(proc,1,t2)</c> 钉住 t2，
	///   <c>CallProcedure</c> 之后 <c>UnpinTuple()</c> 与 <c>JlNativeApi.UnpinTuple(t2)</c> 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 5, 3);
	///   JlTuple t2 = new JlTuple(2, 2, 2);
	///   JlTuple r = t1.TupleLessEqualElem(t2);   // 逐位 t1&lt;=t2 -> (1, 0, 1)
	///   int first = r[0].I;                        // 取第 0 位：1 表示真
	///   </code>
	///   <para><b>资源与坑</b>：返回值是 <c>LoadNew</c> 按 INTEGER 新建的独立元组，不改本元组；纯 0/1 数值不持句柄，
	///   Dispose 无实际回收意义。JlTuple 与 int/double/string 之间存在双向 implicit operator，向"同时有 JlTuple
	///   与标量重载"的算子传参会触发 CS0121 二义（本方法只有 JlTuple 形参，不受影响）。</para>
	/// </remarks>
	public JlTuple TupleLessEqualElem(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(155);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素判断本元组是否"严格小于"t2，返回按位 0/1 的整数元组。</summary>
	/// <param name="t2">比较的右操作数元组。</param>
	/// <returns>逐位为 1（本元组元素 &lt; 对应 t2 元素）或 0 的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生比较算子（id 156），逐位比较，命中记 1、否则记 0。</para>
	///   <para><b>约束或前提</b>：两操作数长度不等时的广播/报错行为由原生 *_elem 语义决定，与不带 _elem 的
	///   <see cref="TupleLess"/> 的确切差异 [待实测]；宜先对齐长度。</para>
	///   <para><b>与相邻算子的取舍</b>：需要"小于等于"用 <see cref="TupleLessEqualElem"/>。</para>
	///   <para><b>钉固定</b>：<c>Store(proc,0)</c> 经 InitPCT 钉住本元组、<c>JlNativeApi.Store(proc,1,t2)</c> 钉住 t2，
	///   <c>CallProcedure</c> 之后 <c>UnpinTuple()</c> 与 <c>JlNativeApi.UnpinTuple(t2)</c> 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 5, 3);
	///   JlTuple t2 = new JlTuple(2, 2, 2);
	///   JlTuple r = t1.TupleLessElem(t2);        // 逐位 t1&lt;t2 -> (1, 0, 0)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 <c>LoadNew</c> 按 INTEGER 新建的独立元组，不改本元组；纯 0/1 数值不持句柄，
	///   Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleLessElem(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(156);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素判断本元组是否"大于等于"t2，返回按位 0/1 的整数元组。</summary>
	/// <param name="t2">比较的右操作数元组。</param>
	/// <returns>逐位为 1（本元组元素 &gt;= 对应 t2 元素）或 0 的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生比较算子（id 157），逐位比较，命中记 1、否则记 0。</para>
	///   <para><b>约束或前提</b>：两操作数长度不等时的标量广播/报错行为由原生 *_elem 语义决定，与不带 _elem 的 <see cref="TupleGreaterEqual"/> 的确切差异 [待实测]；宜先对齐长度以免静默错位。</para>
	///   <para><b>与相邻算子的取舍</b>：需要"严格大于"用 <see cref="TupleGreaterElem"/>，需要"大于等于的整段结果"见不带 _elem 的一组。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 5, 3);
	///   JlTuple t2 = new JlTuple(2, 2, 2);
	///   JlTuple r = t1.TupleGreaterEqualElem(t2);   // 逐位 t1 &gt;= t2 -> (0, 1, 1)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯 0/1 数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleGreaterEqualElem(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(157);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素判断本元组是否"严格大于"t2，返回按位 0/1 的整数元组。</summary>
	/// <param name="t2">比较的右操作数元组。</param>
	/// <returns>逐位为 1（本元组元素 &gt; 对应 t2 元素）或 0 的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生比较算子（id 158），逐位比较，命中记 1、否则记 0。</para>
	///   <para><b>约束或前提</b>：两操作数长度不等时的标量广播/报错行为由原生 *_elem 语义决定，与不带 _elem 的 <see cref="TupleGreater"/> 的确切差异 [待实测]；宜先对齐长度。</para>
	///   <para><b>与相邻算子的取舍</b>：需要"大于等于"用 <see cref="TupleGreaterEqualElem"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 5, 3);
	///   JlTuple t2 = new JlTuple(2, 2, 2);
	///   JlTuple r = t1.TupleGreaterElem(t2);     // 逐位 t1 &gt; t2 -> (0, 1, 0)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯 0/1 数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleGreaterElem(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(158);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素判断本元组与 t2 是否"不相等"，返回按位 0/1 的整数元组。</summary>
	/// <param name="t2">比较的右操作数元组。</param>
	/// <returns>逐位为 1（对应元素不相等）或 0（相等）的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生比较算子（id 159），逐位比较，命中记 1、否则记 0。</para>
	///   <para><b>约束或前提</b>：两操作数长度不等时的标量广播/报错行为由原生 *_elem 语义决定，与不带 _elem 的 <see cref="TupleNotEqual"/> 的确切差异 [待实测]。字符串与数值混排时按底层类型口径比较。</para>
	///   <para><b>与相邻算子的取舍</b>：需要"相等"用 <see cref="TupleEqualElem"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 2, 3);
	///   JlTuple t2 = new JlTuple(1, 5, 3);
	///   JlTuple r = t1.TupleNotEqualElem(t2);    // 逐位 t1 != t2 -> (0, 1, 0)
	///   int second = r[1].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯 0/1 数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleNotEqualElem(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(159);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素判断本元组与 t2 是否"相等"，返回按位 0/1 的整数元组。</summary>
	/// <param name="t2">比较的右操作数元组。</param>
	/// <returns>逐位为 1（对应元素相等）或 0（不相等）的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生比较算子（id 160），逐位比较，命中记 1、否则记 0。</para>
	///   <para><b>约束或前提</b>：两操作数长度不等时的标量广播/报错行为由原生 *_elem 语义决定，与不带 _elem 的 <see cref="TupleEqual"/> 的确切差异 [待实测]。整型与浮点混排时按数值相等判断。</para>
	///   <para><b>与相邻算子的取舍</b>：需要"不相等"用 <see cref="TupleNotEqualElem"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 2, 3);
	///   JlTuple t2 = new JlTuple(1, 5, 3);
	///   JlTuple r = t1.TupleEqualElem(t2);       // 逐位 t1 == t2 -> (1, 0, 1)
	///   int second = r[1].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯 0/1 数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleEqualElem(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(160);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>判断本元组是否"小于等于"t2，返回 0/1 整数结果。</summary>
	/// <param name="t2">比较的右操作数元组。</param>
	/// <returns>表示比较真假的整数元组（1 为真、0 为假）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生比较算子（id 161），是 *_elem 之外的历史命名版本。</para>
	///   <para><b>约束或前提</b>：与 <see cref="TupleLessEqualElem"/> 相比，两者在"结果长度""长度不等的处理"上的确切差异 [待实测]；若需明确的逐元素广播语义，优先用 _elem 版本。</para>
	///   <para><b>与相邻算子的取舍</b>：需要"严格小于"用 <see cref="TupleLess"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 5, 3);
	///   JlTuple t2 = new JlTuple(2, 2, 2);
	///   JlTuple r = t1.TupleLessEqual(t2);       // 比较结果 0/1（真/假）
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯 0/1 数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleLessEqual(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(161);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>判断本元组是否"严格小于"t2，返回 0/1 整数结果。</summary>
	/// <param name="t2">比较的右操作数元组。</param>
	/// <returns>表示比较真假的整数元组（1 为真、0 为假）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生比较算子（id 162），历史命名版本。</para>
	///   <para><b>约束或前提</b>：与 <see cref="TupleLessElem"/> 在长度处理与结果长度上的确切差异 [待实测]；要逐元素广播请用 _elem 版本。</para>
	///   <para><b>与相邻算子的取舍</b>：需要"小于等于"用 <see cref="TupleLessEqual"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 5, 3);
	///   JlTuple t2 = new JlTuple(2, 2, 2);
	///   JlTuple r = t1.TupleLess(t2);            // 比较结果 0/1（真/假）
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯 0/1 数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleLess(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(162);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>判断本元组是否"大于等于"t2，返回 0/1 整数结果。</summary>
	/// <param name="t2">比较的右操作数元组。</param>
	/// <returns>表示比较真假的整数元组（1 为真、0 为假）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生比较算子（id 163），历史命名版本。</para>
	///   <para><b>约束或前提</b>：与 <see cref="TupleGreaterEqualElem"/> 在长度处理与结果长度上的确切差异 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：需要"严格大于"用 <see cref="TupleGreater"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 5, 3);
	///   JlTuple t2 = new JlTuple(2, 2, 2);
	///   JlTuple r = t1.TupleGreaterEqual(t2);    // 比较结果 0/1（真/假）
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯 0/1 数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleGreaterEqual(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(163);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>判断本元组是否"严格大于"t2，返回 0/1 整数结果。</summary>
	/// <param name="t2">比较的右操作数元组。</param>
	/// <returns>表示比较真假的整数元组（1 为真、0 为假）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生比较算子（id 164），历史命名版本。</para>
	///   <para><b>约束或前提</b>：与 <see cref="TupleGreaterElem"/> 在长度处理与结果长度上的确切差异 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：需要"大于等于"用 <see cref="TupleGreaterEqual"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 5, 3);
	///   JlTuple t2 = new JlTuple(2, 2, 2);
	///   JlTuple r = t1.TupleGreater(t2);         // 比较结果 0/1（真/假）
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯 0/1 数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleGreater(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(164);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>判断本元组与 t2 是否"不相等"，返回 0/1 整数结果。</summary>
	/// <param name="t2">比较的右操作数元组。</param>
	/// <returns>表示比较真假的整数元组（1 为真、0 为假）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生比较算子（id 165），历史命名版本。</para>
	///   <para><b>约束或前提</b>：与 <see cref="TupleNotEqualElem"/> 在长度处理上的确切差异 [待实测]；字符串与数值混排按类型口径比较。</para>
	///   <para><b>与相邻算子的取舍</b>：需要"相等"用 <see cref="TupleEqual"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 2, 3);
	///   JlTuple t2 = new JlTuple(1, 5, 3);
	///   JlTuple r = t1.TupleNotEqual(t2);        // 比较结果 0/1（真/假）
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯 0/1 数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleNotEqual(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(165);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>判断本元组与 t2 是否"相等"，返回 0/1 整数结果。</summary>
	/// <param name="t2">比较的右操作数元组。</param>
	/// <returns>表示比较真假的整数元组（1 为真、0 为假）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生比较算子（id 166），历史命名版本。</para>
	///   <para><b>约束或前提</b>：与 <see cref="TupleEqualElem"/> 在长度处理上的确切差异 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：需要"不相等"用 <see cref="TupleNotEqual"/>；需要逐元素布尔掩码用 <see cref="TupleEqualElem"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 2, 3);
	///   JlTuple t2 = new JlTuple(1, 2, 3);
	///   JlTuple r = t1.TupleEqual(t2);           // 比较结果 0/1（真/假）
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯 0/1 数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleEqual(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(166);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>对本元组逐元素取逻辑非，非零变 0、零变 1。</summary>
	/// <returns>逐元素逻辑非后的整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 167），按真值取反：0 记 1、任意非 0 记 0（不是按位取反，按位取反见 <see cref="TupleBnot"/>）。</para>
	///   <para><b>约束或前提</b>：结果按 INTEGER 装载，与输入元素个数相同。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 0, 7);
	///   JlTuple r = t1.TupleNot();               // 逐元素逻辑非 -> (0, 1, 0)
	///   int second = r[1].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleNot()
	{
		IntPtr proc = JlNativeApi.PreCall(167);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素逻辑异或：两操作数真值不同时为 1，否则为 0。</summary>
	/// <param name="t2">第二操作数元组。</param>
	/// <returns>逐元素逻辑异或结果的整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 168），以元素真值（非 0 即真）参与逻辑异或。与按位异或 <see cref="TupleBxor"/> 不同。</para>
	///   <para><b>约束或前提</b>：长度不等时的广播规则由原生语义决定 [待实测]；结果按 INTEGER 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 0, 1);
	///   JlTuple t2 = new JlTuple(1, 1, 0);
	///   JlTuple r = t1.TupleXor(t2);             // 逐元素逻辑异或 -> (0, 1, 1)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleXor(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(168);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素逻辑或：两操作数任一为真即 1，均为假才 0。</summary>
	/// <param name="t2">第二操作数元组。</param>
	/// <returns>逐元素逻辑或结果的整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 169），以元素真值（非 0 即真）参与逻辑或。与按位或 <see cref="TupleBor"/> 不同。</para>
	///   <para><b>约束或前提</b>：长度不等时的广播规则由原生语义决定 [待实测]；结果按 INTEGER 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 0, 0);
	///   JlTuple t2 = new JlTuple(0, 0, 1);
	///   JlTuple r = t1.TupleOr(t2);              // 逐元素逻辑或 -> (1, 0, 1)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleOr(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(169);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素逻辑与：两操作数同时为真才 1，否则 0。</summary>
	/// <param name="t2">第二操作数元组。</param>
	/// <returns>逐元素逻辑与结果的整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 170），以元素真值（非 0 即真）参与逻辑与。与按位与 <see cref="TupleBand"/> 不同；常与 <see cref="TupleSelectMask"/> 配套做掩码筛选。</para>
	///   <para><b>约束或前提</b>：长度不等时的广播规则由原生语义决定 [待实测]；结果按 INTEGER 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 0, 1);
	///   JlTuple t2 = new JlTuple(1, 1, 0);
	///   JlTuple r = t1.TupleAnd(t2);             // 逐元素逻辑与 -> (1, 0, 0)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleAnd(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(170);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>对本元组逐元素按位取反（补码取反）。</summary>
	/// <returns>逐元素按位取反后的整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 171），对整数按位取反（~x），与逻辑非 <see cref="TupleNot"/>（0/1 真值翻转）不同。</para>
	///   <para><b>约束或前提</b>：对无符号/负数的位宽解释依底层整数类型而定 [待实测]；结果按 INTEGER 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(0, 1);
	///   JlTuple r = t1.TupleBnot();               // 逐元素按位取反 -> (-1, -2)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleBnot()
	{
		IntPtr proc = JlNativeApi.PreCall(171);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素按位异或（xor），非逻辑真值运算。</summary>
	/// <param name="t2">第二操作数元组。</param>
	/// <returns>逐元素按位异或结果的整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 172），对整数按位异或。与逻辑异或 <see cref="TupleXor"/>（真值不同为 1）不同。</para>
	///   <para><b>约束或前提</b>：长度不等时的广播规则由原生语义决定 [待实测]；结果按 INTEGER 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(6, 3);
	///   JlTuple t2 = new JlTuple(3, 6);
	///   JlTuple r = t1.TupleBxor(t2);            // 逐元素按位异或 -> (5, 5)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleBxor(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(172);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素按位或（or），非逻辑真值运算。</summary>
	/// <param name="t2">第二操作数元组。</param>
	/// <returns>逐元素按位或结果的整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 173），对整数按位或。与逻辑或 <see cref="TupleOr"/> 不同。</para>
	///   <para><b>约束或前提</b>：长度不等时的广播规则由原生语义决定 [待实测]；结果按 INTEGER 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 2);
	///   JlTuple t2 = new JlTuple(4, 4);
	///   JlTuple r = t1.TupleBor(t2);             // 逐元素按位或 -> (5, 6)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleBor(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(173);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素按位与（and），非逻辑真值运算。</summary>
	/// <param name="t2">第二操作数元组。</param>
	/// <returns>逐元素按位与结果的整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 174），对整数按位与；常用于按位掩码取位。与逻辑与 <see cref="TupleAnd"/> 不同。</para>
	///   <para><b>约束或前提</b>：长度不等时的广播规则由原生语义决定 [待实测]；结果按 INTEGER 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(6, 7);
	///   JlTuple t2 = new JlTuple(3, 3);
	///   JlTuple r = t1.TupleBand(t2);            // 逐元素按位与 -> (2, 3)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleBand(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(174);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素按位右移，shift 元组给出每个元素的位移量。</summary>
	/// <param name="shift">每位元素要右移的位数（非负整数；含义为"移出的低位丢弃、高位补符号/零"依底层实现 [待实测]）。</param>
	/// <returns>逐元素右移后的整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 175）。本元组为被移数值、shift 为位移量，按位配对。</para>
	///   <para><b>约束或前提</b>：位移量为负或超出位宽时的行为依底层实现 [待实测]；结果按 INTEGER 装载。</para>
	///   <para><b>与相邻算子的取舍</b>：左移用 <see cref="TupleLsh"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,shift) 钉住 shift，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(shift) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(8, 16);
	///   JlTuple shift = new JlTuple(2, 2);
	///   JlTuple r = t1.TupleRsh(shift);          // 逐元素右移 2 位 -> (2, 4)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleRsh(JlTuple shift)
	{
		IntPtr proc = JlNativeApi.PreCall(175);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, shift);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(shift);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素按位左移，shift 元组给出每个元素的位移量。</summary>
	/// <param name="shift">每位元素要左移的位数（非负整数；移出高位丢弃、低位补 0）。</param>
	/// <returns>逐元素左移后的整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 176）。本元组为被移数值、shift 为位移量，按位配对；等价于乘以 2 的 shift 次方。</para>
	///   <para><b>约束或前提</b>：位移量超出位宽会溢出，行为依底层实现 [待实测]；结果按 INTEGER 装载。</para>
	///   <para><b>与相邻算子的取舍</b>：右移用 <see cref="TupleRsh"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,shift) 钉住 shift，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(shift) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 2);
	///   JlTuple shift = new JlTuple(3, 3);
	///   JlTuple r = t1.TupleLsh(shift);          // 逐元素左移 3 位 -> (8, 16)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleLsh(JlTuple shift)
	{
		IntPtr proc = JlNativeApi.PreCall(176);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, shift);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(shift);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把整数元组按字符码解释为字符串，遇到 0 值作为字符串分隔。</summary>
	/// <returns>由字符码拼成的字符串元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 177，tuple_chrt）。与 <see cref="TupleChr"/>（每个整数各自转成单字符）不同，本算子把一串整数当作一段文本、以 0 为分隔拼出字符串。</para>
	///   <para><b>约束或前提</b>：输入应为整数（字符码）；结果按 MIXED 装载，故返回的字符串元素类型取决于原生输出。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple codes = new JlTuple(72, 105, 0, 33);   // 'H','i',0,'!'
	///   JlTuple r = codes.TupleChrt();                  // 以 0 为分隔拼字符串
	///   string first = r[0].S;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 新建的独立元组，不改本元组；结果若为纯字符串/数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleChrt()
	{
		IntPtr proc = JlNativeApi.PreCall(177);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把字符串元组的字符展开为 Unicode/ANSI 字符码整数元组。</summary>
	/// <returns>输入各字符串字符的字符码整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 178，tuple_ords）。与 <see cref="TupleOrd"/>（每个元素必须是长度 1 的字符串、逐一取码）不同，ords 把字符串视为整体展开其全部字符。</para>
	///   <para><b>约束或前提</b>：输入应为字符串元组；结果按 INTEGER 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new JlTuple("Hi");
	///   JlTuple r = s.TupleOrds();               // 字符码 -> (72, 105)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleOrds()
	{
		IntPtr proc = JlNativeApi.PreCall(178);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把每个整数按字符码转成对应的单字符字符串。</summary>
	/// <returns>由字符码得到的字符串元组，与输入元素个数相同。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 179，tuple_chr）。与 <see cref="TupleChrt"/>（以 0 为分隔拼整段文本）不同，chr 是一对一：每个整数各自变成一个字符。</para>
	///   <para><b>约束或前提</b>：输入应为整数（字符码）；结果按 MIXED 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple codes = new JlTuple(72, 105);    // 'H','i'
	///   JlTuple r = codes.TupleChr();             // 每个码转单字符 -> ("H", "i")
	///   string first = r[0].S;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 新建的独立元组，不改本元组；结果若为纯字符串不持句柄。</para>
	/// </remarks>
	public JlTuple TupleChr()
	{
		IntPtr proc = JlNativeApi.PreCall(179);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把每个"长度为 1 的字符串"元素转成其 Unicode/ANSI 字符码。</summary>
	/// <returns>各字符对应的字符码整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 180，tuple_ord）。要求每个元素是长度为 1 的字符串，逐一取码；若元素含多字符请改用 <see cref="TupleOrds"/>。</para>
	///   <para><b>约束或前提</b>：输入应为单字符字符串元组；结果按 INTEGER 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new JlTuple("A", "B");
	///   JlTuple r = s.TupleOrd();                // 逐字符取码 -> (65, 66)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄，Dispose 无实际回收意义。</para>
	/// </remarks>
	public JlTuple TupleOrd()
	{
		IntPtr proc = JlNativeApi.PreCall(180);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>按格式串把元组各元素格式化为字符串（类似 printf）。</summary>
	/// <param name="format">格式化串（如 ".2f"、"05d"）；若为单元素则广播到所有元素。</param>
	/// <returns>格式化后的字符串元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 181，tuple_string）。把每个数值元素按 format 转成字符串；本元组为待格式化值、format 为格式说明。</para>
	///   <para><b>约束或前提</b>：数值与字符串混用需确保格式匹配（如对字符串套 %d 未定义）；结果按 MIXED 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,format) 钉住 format，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(format) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple nums = new JlTuple(3.14159, 2.71828);
	///   JlTuple r = nums.TupleString(".2f");     // 按两位小数格式化 -> ("3.14", "2.72")
	///   string first = r[0].S;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 新建的独立元组，不改本元组；结果若为纯字符串不持句柄。</para>
	/// </remarks>
	public JlTuple TupleString(JlTuple format)
	{
		IntPtr proc = JlNativeApi.PreCall(181);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, format);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(format);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素判断字符串是否可解释为数值，返回 0/1。</summary>
	/// <returns>逐位为 1（可解析为数）或 0（不可）的整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 182，tuple_is_number）。用于在 <see cref="TupleNumber"/> 之前预检，避免解析失败。</para>
	///   <para><b>约束或前提</b>：面向字符串元素；结果按 INTEGER 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new JlTuple("12", "1.5", "x");
	///   JlTuple r = s.TupleIsNumber();           // 可解析为数 -> (1, 1, 0)
	///   int third = r[2].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯 0/1 数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleIsNumber()
	{
		IntPtr proc = JlNativeApi.PreCall(182);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把字符串元组逐元素解析为数值。</summary>
	/// <returns>解析得到的数值元组（无法解析者按原生约定处理 [待实测]）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 183，tuple_number）。把表示数字的字符串转成数；建议先用 <see cref="TupleIsNumber"/> 预检。</para>
	///   <para><b>约束或前提</b>：非数值字符串的元素如何处理（0/保留/报错）依原生实现 [待实测]；结果按 MIXED 装载，故整数与浮点字符串会分别得到相应类型。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple s = new JlTuple("12", "1.5");
	///   JlTuple r = s.TupleNumber();             // 解析为数 -> (12, 1.5)
	///   double second = r[1].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleNumber()
	{
		IntPtr proc = JlNativeApi.PreCall(183);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把元组逐元素四舍五入到最近的整数。</summary>
	/// <returns>四舍五入后的整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 184，tuple_round）。与 <see cref="TupleInt"/>（向零截断）不同，round 取最近整数。</para>
	///   <para><b>约束或前提</b>：.5 的取舍方向（银行家/远离零）依原生实现 [待实测]；结果按 INTEGER 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1.6, 2.4, 3.5);
	///   JlTuple r = t1.TupleRound();             // 四舍五入为整数
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleRound()
	{
		IntPtr proc = JlNativeApi.PreCall(184);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把元组逐元素向零截断为整数。</summary>
	/// <returns>截断后的整数元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 185，tuple_int）。丢弃小数部分向零取整，与 <see cref="TupleRound"/>（就近）、<see cref="TupleFloor"/>（向下）、<see cref="TupleCeil"/>（向上）不同。</para>
	///   <para><b>约束或前提</b>：负数向零截断（-2.7 -> -2），结果按 INTEGER 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1.6, -2.7);
	///   JlTuple r = t1.TupleInt();               // 向零截断 -> (1, -2)
	///   int second = r[1].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleInt()
	{
		IntPtr proc = JlNativeApi.PreCall(185);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把元组逐元素转换为双精度浮点数。</summary>
	/// <returns>转换为 DOUBLE 的元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 186，tuple_real）。把整数元组提升为浮点，便于后续做浮点除法/三角等；结果按 DOUBLE 装载。</para>
	///   <para><b>与相邻算子的取舍</b>：向整数方向转换用 <see cref="TupleInt"/>/<see cref="TupleRound"/>；本算子只做数值类型提升、不改变数的大小。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1, 2, 3);
	///   JlTuple r = t1.TupleReal();              // 提升为浮点 -> (1.0, 2.0, 3.0)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleReal()
	{
		IntPtr proc = JlNativeApi.PreCall(186);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素计算 ldexp：本元组 × 2 的 t2 次方。</summary>
	/// <param name="t2">指数元组（2 的幂次），结果为本元组乘以 2^t2。</param>
	/// <returns>DOUBLE 类型的逐元素 ldexp 结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 187，tuple_ldexp）。等价于 t1 * Math.Pow(2, t2)，用于按 2 的幂缩放；结果按 DOUBLE 装载。</para>
	///   <para><b>约束或前提</b>：长度不等时的广播规则由原生语义决定 [待实测]。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1.0, 3.0);
	///   JlTuple t2 = new JlTuple(3, 2);
	///   JlTuple r = t1.TupleLdexp(t2);           // t1 * 2^t2 -> (8.0, 12.0)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleLdexp(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(187);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素浮点取余（fmod），保留符号、按截断除法。</summary>
	/// <param name="t2">除数元组。</param>
	/// <returns>DOUBLE 类型的逐元素浮点余数。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 188，tuple_fmod）。余数符号与被除数（本元组）一致；与整数取余 <see cref="TupleMod"/> 不同，本算子在浮点域进行、结果按 DOUBLE 装载。</para>
	///   <para><b>约束或前提</b>：t2 为 0 时结果未定义 [待实测]；长度不等时的广播规则由原生语义决定 [待实测]。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(7.5, -7.5);
	///   JlTuple t2 = new JlTuple(2.0, 2.0);
	///   JlTuple r = t1.TupleFmod(t2);            // 浮点取余 -> (1.5, -1.5)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleFmod(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(188);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素整数取余（mod），结果按 INTEGER 装载。</summary>
	/// <param name="t2">整数除数元组。</param>
	/// <returns>逐元素整数余数。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 189，tuple_mod）。在整数域取余，与浮点 <see cref="TupleFmod"/> 不同。</para>
	///   <para><b>约束或前提</b>：t2 含 0 会出错或产生未定义结果 [待实测]；负数的余数符号约定 [待实测]；结果按 INTEGER 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(7, 8);
	///   JlTuple t2 = new JlTuple(3, 3);
	///   JlTuple r = t1.TupleMod(t2);             // 整数取余 -> (1, 2)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 INTEGER 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleMod(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(189);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素向上取整（不小于该数的最小整数），结果按 DOUBLE 装载。</summary>
	/// <returns>DOUBLE 类型的向上取整结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 190，tuple_ceil）。与 <see cref="TupleFloor"/>（向下）、<see cref="TupleInt"/>（向零截断）不同。</para>
	///   <para><b>约束或前提</b>：输出仍按 DOUBLE 存储（值为整数但类型是浮点）。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1.2, -1.8);
	///   JlTuple r = t1.TupleCeil();              // 向上取整 -> (2.0, -1.0)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleCeil()
	{
		IntPtr proc = JlNativeApi.PreCall(190);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素向下取整（不大于该数的最大整数），结果按 DOUBLE 装载。</summary>
	/// <returns>DOUBLE 类型的向下取整结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 191，tuple_floor）。与 <see cref="TupleCeil"/>（向上）、<see cref="TupleInt"/>（向零截断）不同。</para>
	///   <para><b>约束或前提</b>：输出仍按 DOUBLE 存储（值为整数但类型是浮点）。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1.8, -1.2);
	///   JlTuple r = t1.TupleFloor();             // 向下取整 -> (1.0, -2.0)
	///   double second = r[1].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleFloor()
	{
		IntPtr proc = JlNativeApi.PreCall(191);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素乘方：本元组为底、t2 为指数，结果按 DOUBLE 装载。</summary>
	/// <param name="t2">指数元组。</param>
	/// <returns>逐元素 t1^t2 的浮点结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 192，tuple_pow）。计算 t1 的 t2 次幂；结果按 DOUBLE 装载。</para>
	///   <para><b>约束或前提</b>：负底数的非整数指数会产生未定义/NaN [待实测]；长度不等时的广播规则由原生语义决定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：以 2 为底的幂缩放可用 <see cref="TupleLdexp"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,t2) 钉住 t2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(t2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(2.0, 3.0);
	///   JlTuple t2 = new JlTuple(10.0, 4.0);
	///   JlTuple r = t1.TuplePow(t2);             // t1^t2 -> (1024.0, 81.0)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TuplePow(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(192);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取常用对数（以 10 为底），结果按 DOUBLE 装载。</summary>
	/// <returns>逐元素 log10 的浮点结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 193，tuple_log10）。与自然对数 <see cref="TupleLog"/>、指数 <see cref="TupleExp"/> 相对。</para>
	///   <para><b>约束或前提</b>：输入应 &gt; 0；对 0 或负数的处理（-inf/NaN/报错）依原生实现 [待实测]。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(100.0, 1000.0);
	///   JlTuple r = t1.TupleLog10();             // -> (2.0, 3.0)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleLog10()
	{
		IntPtr proc = JlNativeApi.PreCall(193);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取自然对数（以 e 为底），结果按 DOUBLE 装载。</summary>
	/// <returns>逐元素 ln 的浮点结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 194，tuple_log）。与 <see cref="TupleLog10"/>（以 10 为底）、<see cref="TupleExp"/>（互为反函数）相对。</para>
	///   <para><b>约束或前提</b>：输入应 &gt; 0；对 0/负数的处理依原生实现 [待实测]。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(2.718281828, 1.0);
	///   JlTuple r = t1.TupleLog();               // ln(e)=1, ln(1)=0 -> (1.0, 0.0)
	///   double second = r[1].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleLog()
	{
		IntPtr proc = JlNativeApi.PreCall(194);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取自然指数 e^x，结果按 DOUBLE 装载。</summary>
	/// <returns>逐元素 exp 的浮点结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 195，tuple_exp）。是 <see cref="TupleLog"/> 的反函数。</para>
	///   <para><b>约束或前提</b>：过大正数会上溢为 inf [待实测]；结果按 DOUBLE 装载。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(0.0, 1.0);
	///   JlTuple r = t1.TupleExp();               // e^0=1, e^1≈2.718 -> (1.0, 2.718...)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleExp()
	{
		IntPtr proc = JlNativeApi.PreCall(195);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取双曲正切，结果按 DOUBLE 装载。</summary>
	/// <returns>逐元素 tanh 的浮点结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 196，tuple_tanh）。与三角正切 <see cref="TupleTan"/> 不同；tanh 恒在 (-1,1)。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(0.0, 1.0);
	///   JlTuple r = t1.TupleTanh();              // tanh(0)=0, tanh(1)≈0.761
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleTanh()
	{
		IntPtr proc = JlNativeApi.PreCall(196);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取双曲余弦，结果按 DOUBLE 装载。</summary>
	/// <returns>逐元素 cosh 的浮点结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 197，tuple_cosh）。与三角余弦 <see cref="TupleCos"/> 不同；cosh 为偶函数且 &gt;= 1。</para>
	///   <para><b>约束或前提</b>：过大绝对值会上溢为 inf [待实测]。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(0.0, 1.0);
	///   JlTuple r = t1.TupleCosh();              // cosh(0)=1, cosh(1)≈1.543
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleCosh()
	{
		IntPtr proc = JlNativeApi.PreCall(197);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取双曲正弦，结果按 DOUBLE 装载。</summary>
	/// <returns>逐元素 sinh 的浮点结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 198，tuple_sinh）。与三角正弦 <see cref="TupleSin"/> 不同；sinh 为奇函数。</para>
	///   <para><b>约束或前提</b>：过大绝对值会上溢为 inf [待实测]。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(0.0, 1.0);
	///   JlTuple r = t1.TupleSinh();              // sinh(0)=0, sinh(1)≈1.175
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleSinh()
	{
		IntPtr proc = JlNativeApi.PreCall(198);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把角度（度）逐元素转换为弧度，结果按 DOUBLE 装载。</summary>
	/// <returns>弧度值元组（= 角度 × π / 180）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 199，tuple_rad）。库内三角函数一律以弧度为输入/输出，故给 <see cref="TupleSin"/> 等喂角度前先转弧度。</para>
	///   <para><b>与相邻算子的取舍</b>：反向（弧度转角度）用 <see cref="TupleDeg"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple deg = new JlTuple(180.0, 90.0);
	///   JlTuple r = deg.TupleRad();              // -> (π, π/2)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleRad()
	{
		IntPtr proc = JlNativeApi.PreCall(199);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把弧度逐元素转换为角度（度），结果按 DOUBLE 装载。</summary>
	/// <returns>角度值元组（= 弧度 × 180 / π）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 200，tuple_deg）。用于把 <see cref="TupleAtan2"/>/<see cref="TupleAcos"/> 等返回的弧度读数换算成度。</para>
	///   <para><b>与相邻算子的取舍</b>：反向（角度转弧度）用 <see cref="TupleRad"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rad = new JlTuple(3.14159265, 1.5707963);
	///   JlTuple r = rad.TupleDeg();              // -> (180.0, 90.0)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleDeg()
	{
		IntPtr proc = JlNativeApi.PreCall(200);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素四象限反正切 atan2(y, x)，本元组提供 y、x 参数提供 x，结果按 DOUBLE 装载。</summary>
	/// <param name="x">x 分量元组（与作为 y 的本元组逐元素配对）。</param>
	/// <returns>角度（弧度），落在 (-π, π]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 201，tuple_atan2）。本元组是 y（分子），参数 x 是横坐标；据此判定象限，得到完整方向角。</para>
	///   <para><b>约束或前提</b>：y、x 同为 0 时的结果未定义 [待实测]；长度不等时的广播规则由原生语义决定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只要单参数比值用 <see cref="TupleAtan"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,x) 钉住 x，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(x) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple y = new JlTuple(1.0, 0.0);
	///   JlTuple x = new JlTuple(1.0, -1.0);
	///   JlTuple r = y.TupleAtan2(x);             // atan2(y,x) 弧度
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleAtan2(JlTuple x)
	{
		IntPtr proc = JlNativeApi.PreCall(201);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, x);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(x);
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取反正切（单参数），结果按 DOUBLE 装载，值域 (-π/2, π/2)。</summary>
	/// <returns>逐元素 arctan 的弧度结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 202，tuple_atan）。</para>
	///   <para><b>与相邻算子的取舍</b>：需要区分象限、避免 ±π/2 折叠时，改用 <see cref="TupleAtan2"/>（本元组=y，参数=x）。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1.0, 0.0);
	///   JlTuple r = t1.TupleAtan();              // -> (π/4, 0.0)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleAtan()
	{
		IntPtr proc = JlNativeApi.PreCall(202);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取反余弦，结果按 DOUBLE 装载，值域 [0, π]。</summary>
	/// <returns>逐元素 arccos 的弧度结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 203，tuple_acos）。</para>
	///   <para><b>约束或前提</b>：定义域为 [-1, 1]；越界元素的处理（NaN/报错）依原生实现 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：需要按 [-π/2, π/2] 输出用 <see cref="TupleAsin"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1.0, 0.0);
	///   JlTuple r = t1.TupleAcos();              // -> (0.0, π/2)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleAcos()
	{
		IntPtr proc = JlNativeApi.PreCall(203);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取反正弦，结果按 DOUBLE 装载，值域 [-π/2, π/2]。</summary>
	/// <returns>逐元素 arcsin 的弧度结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 204，tuple_asin）。</para>
	///   <para><b>约束或前提</b>：定义域为 [-1, 1]；越界元素的处理依原生实现 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：需要 [0, π] 输出用 <see cref="TupleAcos"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(1.0, 0.0);
	///   JlTuple r = t1.TupleAsin();              // -> (π/2, 0.0)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleAsin()
	{
		IntPtr proc = JlNativeApi.PreCall(204);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取正切（输入为弧度），结果按 DOUBLE 装载。</summary>
	/// <returns>逐元素 tan 的浮点结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 205，tuple_tan）。输入是弧度，若手上有角度先经 <see cref="TupleRad"/> 转换。</para>
	///   <para><b>约束或前提</b>：接近 π/2 奇数倍时值发散 [待实测]。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rad = new JlTuple(0.0, 0.7853981634);
	///   JlTuple r = rad.TupleTan();              // tan(0)=0, tan(π/4)=1
	///   double second = r[1].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleTan()
	{
		IntPtr proc = JlNativeApi.PreCall(205);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取余弦（输入为弧度），结果按 DOUBLE 装载。</summary>
	/// <returns>逐元素 cos 的浮点结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 206，tuple_cos）。输入是弧度，角度请先 <see cref="TupleRad"/>。</para>
	///   <para><b>与相邻算子的取舍</b>：双曲余弦是 <see cref="TupleCosh"/>（非周期），不要混用。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rad = new JlTuple(0.0, 3.14159265);
	///   JlTuple r = rad.TupleCos();              // -> (1.0, -1.0)
	///   double second = r[1].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleCos()
	{
		IntPtr proc = JlNativeApi.PreCall(206);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取正弦（输入为弧度），结果按 DOUBLE 装载。</summary>
	/// <returns>逐元素 sin 的浮点结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 207，tuple_sin）。输入是弧度，角度请先 <see cref="TupleRad"/>。</para>
	///   <para><b>与相邻算子的取舍</b>：双曲正弦是 <see cref="TupleSinh"/>（非周期），不要混用。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rad = new JlTuple(0.0, 1.5707963);
	///   JlTuple r = rad.TupleSin();              // -> (0.0, 1.0)
	///   double second = r[1].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleSin()
	{
		IntPtr proc = JlNativeApi.PreCall(207);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取绝对值并强制以浮点（DOUBLE）返回。</summary>
	/// <returns>绝对值元组，始终按 DOUBLE 装载。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 208，tuple_fabs）。与 <see cref="TupleAbs"/> 的区别：fabs 输出统一为浮点，abs 保留输入类型。</para>
	///   <para><b>与相邻算子的取舍</b>：需要保持整型结果用 <see cref="TupleAbs"/>。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(-3, 4);
	///   JlTuple r = t1.TupleFabs();              // -> (3.0, 4.0)，DOUBLE 类型
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleFabs()
	{
		IntPtr proc = JlNativeApi.PreCall(208);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取平方根，结果按 DOUBLE 装载。</summary>
	/// <returns>逐元素平方根的浮点结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 209，tuple_sqrt）。</para>
	///   <para><b>约束或前提</b>：负数输入得到 NaN 或报错 [待实测]；如需任意次幂用 <see cref="TuplePow"/>（t2=0.5）。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(4.0, 9.0, 16.0);
	///   JlTuple r = t1.TupleSqrt();              // -> (2.0, 3.0, 4.0)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 DOUBLE 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleSqrt()
	{
		IntPtr proc = JlNativeApi.PreCall(209);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取绝对值，保留输入的数值类型。</summary>
	/// <returns>绝对值元组（MIXED 装载：整入得整、浮入得浮）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 210，tuple_abs）。与 <see cref="TupleFabs"/> 的区别：abs 按输入类型返回，不强制转浮点。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(-3, 4, -5);
	///   JlTuple r = t1.TupleAbs();               // -> (3, 4, 5)，仍为整数
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 MIXED 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleAbs()
	{
		IntPtr proc = JlNativeApi.PreCall(210);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取负（乘以 -1），保留输入的数值类型。</summary>
	/// <returns>取负后的元组（MIXED 装载）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 211，tuple_neg）。整数取负仍为整数、浮点取负仍为浮点；等价于重载一元负号。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(3, -4);
	///   JlTuple r = t1.TupleNeg();               // -> (-3, 4)
	///   int first = r[0].I;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 MIXED 新建的独立元组，不改本元组；纯数值不持句柄。</para>
	/// </remarks>
	public JlTuple TupleNeg()
	{
		IntPtr proc = JlNativeApi.PreCall(211);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素相除（本元组为被除数、q2 为除数），结果按 MIXED 装载。</summary>
	/// <param name="q2">除数元组。</param>
	/// <returns>逐元素商。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 212，tuple_div）。结果的整数/浮点形态取决于操作数类型；若两侧都是整数是否做整除/截断 [待实测]。</para>
	///   <para><b>约束或前提</b>：q2 含 0 会出错或未定义 [待实测]；长度不等时的广播规则由原生语义决定 [待实测]。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组、JlNativeApi.Store(proc,1,q2) 钉住 q2，CallProcedure 之后 UnpinTuple() 与 JlNativeApi.UnpinTuple(q2) 解除；调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t1 = new JlTuple(6.0, 9.0);
	///   JlTuple q2 = new JlTuple(2.0, 3.0);
	///   JlTuple r = t1.TupleDiv(q2);             // -> (3.0, 3.0)
	///   double first = r[0].D;
	///   </code>
	///   <para><b>资源与坑</b>：返回值为 LoadNew 按 MIXED 新建的独立元组，不改本元组；纯数值不持句柄。JlTuple 与标量间双向隐式转换，若与其他标量重载同用注意 CS0121 二义。</para>
	/// </remarks>
	public JlTuple TupleDiv(JlTuple q2)
	{
		IntPtr proc = JlNativeApi.PreCall(212);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, q2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(q2);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>原生实现：逐元素相乘（本元组为被乘数、p2 为乘数），结果按 MIXED 装载。</summary>
	/// <param name="p2">乘数元组。</param>
	/// <returns>逐元素积。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生整型运算路径（id 213，tuple_mul）。当 <c>TupleMult</c> 的首选整型路径不可用时，回退到本方法。</para>
	///   <para><b>资源与坑</b>：LoadNew 装载新元组，不改本元组；p2 长度不等时的广播行为由原生语义决定 [待实测]。</para>
	///   <para><b>相关算子</b>：<c>TupleMult</c>、<c>operator*</c></para>
	/// </remarks>
	private JlTuple TupleMultOp(JlTuple p2)
	{
		IntPtr proc = JlNativeApi.PreCall(213);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, p2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(p2);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>原生实现：逐元素相减（本元组为被减数、d2 为减数），结果按 MIXED 装载。</summary>
	/// <param name="d2">减数元组。</param>
	/// <returns>逐元素差。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生整型运算路径（id 214，tuple_sub）。当 <c>TupleSub</c> 的首选整型路径不可用时，回退到本方法。</para>
	///   <para><b>资源与坑</b>：LoadNew 装载新元组，不改本元组；d2 长度不等时的广播行为由原生语义决定 [待实测]。</para>
	///   <para><b>相关算子</b>：<c>TupleSub</c>、<c>operator-</c></para>
	/// </remarks>
	private JlTuple TupleSubOp(JlTuple d2)
	{
		IntPtr proc = JlNativeApi.PreCall(214);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, d2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(d2);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>原生实现：逐元素相加（本元组与被加数 s2），结果按 MIXED 装载。</summary>
	/// <param name="s2">被加数元组。</param>
	/// <returns>逐元素和。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生整型运算路径（id 215，tuple_add）。当 <c>TupleAdd</c> 的首选整型路径不可用时，回退到本方法。</para>
	///   <para><b>资源与坑</b>：LoadNew 装载新元组，不改本元组；s2 长度不等时的广播行为由原生语义决定 [待实测]。</para>
	///   <para><b>相关算子</b>：<c>TupleAdd</c>、<c>operator+</c></para>
	/// </remarks>
	private JlTuple TupleAddOp(JlTuple s2)
	{
		IntPtr proc = JlNativeApi.PreCall(215);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, s2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(s2);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>由 <see cref="SerializeTuple"/> 得到的字节流反序列化出一个元组（静态）。</summary>
	/// <param name="serializedItemHandle">序列化缓冲的字节数组。</param>
	/// <returns>新建的元组（MIXED 装载），非原地改写。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 216）。把 <see cref="SerializeTuple"/> 产出的字节还原为独立 JlTuple，用于跨进程/存储传递。</para>
	///   <para><b>钉固定</b>：静态入口不钉住实例；buffer 通过 JlNativeApi.Store 传入，函数用 GC.KeepAlive(buffer) 保证其在原生调用结束前不被回收，调用方无需钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple src = new JlTuple(1, 2, 3);
	///   byte[] blob = src.SerializeTuple();
	///   JlTuple t = JlTuple.DeserializeTuple(blob);
	///   int n = t.Length;                          // n == 3
	///   </code>
	///   <para><b>资源与坑</b>：入参是 <see cref="SerializeTuple"/> 的完整输出，勿手工裁剪；返回值是独立元组，若含句柄元素用毕应 Dispose。</para>
	/// </remarks>
	public static JlTuple DeserializeTuple(byte[] serializedItemHandle)
	{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		IntPtr proc = JlNativeApi.PreCall(216);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(buffer);
		return tuple;
	}

	/// <summary>把本元组序列化为字节流，便于存储或跨进程传递。</summary>
	/// <returns>序列化后的字节数组，可用 <see cref="DeserializeTuple"/> 还原。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 217）。产出的是自描述的字节缓冲。</para>
	///   <para><b>钉固定</b>：Store(proc,0) 经 InitPCT 钉住本元组，CallProcedure 之后 UnpinTuple() 解除；GC.KeepAlive(this) 保证原生调用期间本元组不被回收。调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple src = new JlTuple(7, 8, 9);
	///   byte[] blob = src.SerializeTuple();
	///   int len = blob.Length;
	///   </code>
	///   <para><b>资源与坑</b>：返回的字节数组由调用方管理；不改变本元组内容。</para>
	/// </remarks>
	public byte[] SerializeTuple()
	{
		IntPtr proc = JlNativeApi.PreCall(217);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>把本元组写到文件，走原生序列化格式。</summary>
	/// <param name="fileName">目标文件路径的字符串元组；直接传字符串字面量即可（string 有到 JlTuple 的隐式转换）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 218）。本元组钉在原生输入 0，fileName 钉在输入 1；文件字节全部由原生侧组装，C# 侧拿不到中间数据。写出的文件用 <see cref="ReadTuple"/> 读回。</para>
	///   <para><b>约束或前提</b>：路径不存在或不可写时 PostCall 抛错误。句柄元素能否随文件持久化取决于其所指对象是否可序列化 [待实测]。</para>
	///   <para><b>钉固定</b>：CallProcedure 之后对本元组和 fileName 各调一次 UnpinTuple 解除固定；GC.KeepAlive(this) 保证原生调用期间本元组不被回收。调用方不参与钉/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple samples = new JlTuple(1.5, 2.5, 3.5);
	///   samples.WriteTuple("samples.tup");
	///   </code>
	///   <para><b>资源与坑</b>：无返回值、不改动本元组。fileName 给多元素字符串元组时原生侧只按一个输入槽接收，取用规则由原生决定 [待实测]。</para>
	/// </remarks>
	public void WriteTuple(JlTuple fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(218);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(fileName);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>从文件读回一个元组。</summary>
	/// <param name="fileName">待读文件路径的字符串元组；直接传字符串字面量即可。</param>
	/// <returns>LoadNew 以 MIXED 型装载出的全新元组（不是往已有对象上改写）；若含句柄元素，由调用方负责 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 219），fileName 钉在原生输入 0，调用后 UnpinTuple 解除；InitOCT(0)+LoadNew(0) 产出新元组。与 <see cref="WriteTuple"/> 成对使用。</para>
	///   <para><b>约束或前提</b>：文件不存在或格式不符时 PostCall 抛错误，此时不会返回半成品元组内容（LoadNew 失败路径把 data 置为 EMPTY，但异常先于返回抛出）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple loaded = JlTuple.ReadTuple("samples.tup");
	///   double first = loaded;      // 隐式转 double：只取第一个元素
	///   loaded.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>：静态方法，不钉调用方（本类无实例 this）；返回的新元组独立存在，用完请 Dispose——纯数值内容时 Dispose 是无害空操作。上例的隐式 double 转换只读首元素，多值会被静默丢弃。</para>
	/// </remarks>
	public static JlTuple ReadTuple(JlTuple fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(219);
		JlNativeApi.Store(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(fileName);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>让原生侧清掉本元组内句柄所指的对象内容。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2011），只有输入槽 0（Store 钉住本元组），方法体没有 InitOCT/Load——不产出任何新元组，也不删改元组里的句柄值本身。</para>
	///   <para><b>与相邻算子的取舍</b>：Dispose 释放的是元组自己持有的句柄（C# 侧回收）；ClearHandle 是让原生把句柄指向的对象内容清掉，元组元素个数与句柄数值保持不变。清完后哪个句柄已失效，用 <see cref="TupleIsValidHandle"/> 逐元素检测。</para>
	///   <para><b>钉固定</b>：CallProcedure 后 UnpinTuple 解除固定，GC.KeepAlive(this) 保证期间不回收。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple one = new JlTuple(new JlHandle());
	///   one.ClearHandle();
	///   </code>
	///   <para><b>资源与坑</b>：对纯数值/字符串元组调用时原生侧行为未在本仓库代码中体现 [待实测]；清空后若仍把句柄元素传给算子会得到无效句柄错误。</para>
	/// </remarks>
	public void ClearHandle()
	{
		IntPtr proc = JlNativeApi.PreCall(2011);
		Store(proc, 0);
		int procResult = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>判断本元组的内部表示是否整体为句柄型。</summary>
	/// <returns>按 INTEGER 装载的单元素 0/1 元组；可隐式转成 int/bool 直接用。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2016），看的是元组整体的存储类型，一次布尔判断，不逐元素。</para>
	///   <para><b>约束或前提</b>：存的恰好是指针数值的 INTEGER/LONG 元组会判 0——它不是 HANDLE 存储；MIXED 元组即使每个元素都是句柄，整体类型也不是 HANDLE，同样判 0 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：要逐元素判类型（如 MIXED 元组里哪几个是句柄）用 <see cref="TupleIsHandleElem"/>；要判断句柄是否仍然有效用 <see cref="TupleIsValidHandle"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple hs = new JlTuple(new JlHandle());
	///   int isHandle = hs.TupleIsHandle();   // 隐式取首元素：1
	///   hs.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉住本元组、调用后 UnpinTuple；返回的新元组是 LoadNew(INTEGER) 产物，纯整数内容 Dispose 与否皆可。</para>
	/// </remarks>
	public JlTuple TupleIsHandle()
	{
		IntPtr proc = JlNativeApi.PreCall(2016);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素判断本元组各元素是否为句柄型。</summary>
	/// <returns>与输入等长的 INTEGER 0/1 元组，按句柄元素位为 1。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2017），对每个元素单独问一次"是不是句柄"，结果逐位对齐输入。</para>
	///   <para><b>与相邻算子的取舍</b>：<see cref="TupleIsHandle"/> 只回答整体存储类型一个值；对 MIXED 元组想筛出句柄位置必须用本方法。判"句柄是否还有效"则用 <see cref="TupleIsValidHandle"/>，二者含义不同：类型是句柄不等于仍然有效。</para>
	///   <para><b>参数取向</b>：输入非句柄的纯数值/字符串元组时得到全 0，不报错。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple mixed = new JlTuple(1, "two", new JlHandle());
	///   JlTuple flags = mixed.TupleIsHandleElem();
	///   int lastIsHandle = flags[2];    // 末元素是句柄：1
	///   mixed.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple；返回新元组为 INTEGER 向量。示例里 flags[2] 取单元素再隐式转 int。</para>
	/// </remarks>
	public JlTuple TupleIsHandleElem()
	{
		IntPtr proc = JlNativeApi.PreCall(2017);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>判断本元组整体是否可序列化（能否走 SerializeTuple/WriteTuple）。</summary>
	/// <returns>按 INTEGER 装载的单元素 0/1 元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2018），一次整体判断。纯数值/字符串元组恒可序列化；含句柄的元组要看所指对象是否支持序列化 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：想知道 MIXED 元组里是哪个元素卡住了序列化，用逐元素的 <see cref="TupleIsSerializableElem"/>；判"能不能序列化"和判"是不是句柄"（<see cref="TupleIsHandle"/>）是两回事。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple nums = new JlTuple(1, 2, 3);
	///   bool ok = ((int)nums.TupleIsSerializable()) != 0;   // true
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple。可先调用本方法再决定要不要写 SerializeTuple，避免句柄对象不支持序列化时抛错。</para>
	/// </remarks>
	public JlTuple TupleIsSerializable()
	{
		IntPtr proc = JlNativeApi.PreCall(2018);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素判断本元组各元素是否可序列化。</summary>
	/// <returns>与输入等长的 INTEGER 0/1 元组，可序列化元素位为 1。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2019），逐元素回答"这个值能否进序列化流"——主要用于定位 MIXED/HANDLE 元组里哪一个对象不可序列化导致整体失败。</para>
	///   <para><b>与相邻算子的取舍</b>：只需要"整条元组能不能存"的一次性判断时用 <see cref="TupleIsSerializable"/>，不必逐位比对。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple mixed = new JlTuple(42, "text");
	///   JlTuple perElem = mixed.TupleIsSerializableElem();
	///   int first = (int)perElem[0];         // 显式取 INTEGER 标志位
	///   int second = (int)perElem[1];
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple；结果按 INTEGER 装载，长度与本元组一致，索引越界取用会抛访问异常。</para>
	/// </remarks>
	public JlTuple TupleIsSerializableElem()
	{
		IntPtr proc = JlNativeApi.PreCall(2019);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素检查元组中的句柄是否仍指向有效对象。</summary>
	/// <returns>与输入等长的 INTEGER 1/0 元组，1=句柄有效。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2020）。问的是"对象还活着吗"，与 <see cref="TupleIsHandle"/>（问存储类型）和 <see cref="TupleIsHandleElem"/> 不同：句柄被 Dispose 或 ClearHandle 之后类型仍是句柄，但有效性变 0。</para>
	///   <para><b>约束或前提</b>：非句柄元素如何作答未在本仓库代码中体现 [待实测]；典型用途是在把句柄元组喂给算子前体检，或 ClearHandle 后确认已失效。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle h = new JlHandle();
	///   JlTuple hs = new JlTuple(h);
	///   JlTuple valid = hs.TupleIsValidHandle();
	///   int flag = valid;        // 单元素元组隐式转 int
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple。本方法只查询，不释放任何东西；hs 持有的是 h 的克隆，Dispose hs 不影响 h 本身。</para>
	/// </remarks>
	public JlTuple TupleIsValidHandle()
	{
		IntPtr proc = JlNativeApi.PreCall(2020);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>取本元组的语义类型名（整条元组一个值）。</summary>
	/// <returns>LoadNew 以 MIXED 装载的字符串元组，即原生登记在案的类型名（不是 INTEGER/DOUBLE 这类存储类型）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2021）。语义类型是原生给值的用途命名（量纲/领域含义），与 C# 侧存储类型 JlTupleType 是两层概念。</para>
	///   <para><b>与相邻算子的取舍</b>：MIXED 元组各元素语义类型可能不同，整体查询会退化成单一答案，这时改用逐元素的 <see cref="TupleSemTypeElem"/>；判存储类型看 <see cref="Type"/> 属性即可，不必走原生。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple nums = new JlTuple(1.0, 2.0);
	///   string semType = nums.TupleSemType();   // 单元素字符串元组隐式转 string
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple。未显式标注语义类型的普通数值元组返回什么字符串由原生决定 [待实测]。</para>
	/// </remarks>
	public JlTuple TupleSemType()
	{
		IntPtr proc = JlNativeApi.PreCall(2021);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素取本元组各元素的语义类型名。</summary>
	/// <returns>与输入等长的字符串元组（MIXED 装载），每个位置一个语义类型名。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2022），对 MIXED 元组按位置各问一次，结果向量与输入逐位对齐。</para>
	///   <para><b>与相邻算子的取舍</b>：整条元组存储类型一致（纯数值/字符串）时，用一次性的 <see cref="TupleSemType"/> 更省事；只有元素类型混杂、需要按位核对才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple mixed = new JlTuple(7, "eight");
	///   JlTuple names = mixed.TupleSemTypeElem();
	///   string second = names[1];      // 逐元素取回字符串
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple。返回元组按 MIXED 装载，长度与索引访问越界的风险同普通元组。</para>
	/// </remarks>
	public JlTuple TupleSemTypeElem()
	{
		IntPtr proc = JlNativeApi.PreCall(2022);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素求反双曲余弦 arccosh。</summary>
	/// <returns>与输入等长的 DOUBLE 新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2069），结果强制按 DOUBLE 装载，即使输入是整数元组。</para>
	///   <para><b>约束或前提</b>：定义域 x ≥ 1（arccosh(x) = ln(x+√(x²−1))）；x 小于 1 时实数域无解，原生按 C 库惯例应得 NaN [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：这是双曲族的反函数，别和三角族 TupleCos 系混淆；正向双曲余弦是 TupleCosh。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple xs = new JlTuple(1.0, 2.0);
	///   JlTuple acosh = xs.TupleAcosh();      // (0, 约1.3170)
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple；返回的是新元组，与输入无关。</para>
	/// </remarks>
	public JlTuple TupleAcosh()
	{
		IntPtr proc = JlNativeApi.PreCall(2069);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素求反双曲正弦 arsinh。</summary>
	/// <returns>与输入等长的 DOUBLE 新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2070），按 DOUBLE 装载结果。</para>
	///   <para><b>约束或前提</b>：全实数域都有定义，无定义域坑；但 |x| 很大时 arsinh(x) 增长如 ln(2|x|)，想从 sinh 值反解大参数时注意精度压缩。</para>
	///   <para><b>与相邻算子的取舍</b>：x ≥ 1 场景常用 acosh（<see cref="TupleAcosh"/>），(-1,1) 场景用 atanh（<see cref="TupleAtanh"/>），三者别互相顶替。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple ys = new JlTuple(-1.0, 0.0, 1.0);
	///   JlTuple asinh = ys.TupleAsinh();     // 奇函数：两端互为相反数
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple；输入不被改动。</para>
	/// </remarks>
	public JlTuple TupleAsinh()
	{
		IntPtr proc = JlNativeApi.PreCall(2070);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素求反双曲正切 artanh。</summary>
	/// <returns>与输入等长的 DOUBLE 新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2071），按 DOUBLE 装载结果。artanh(x) = ½·ln((1+x)/(1−x))。</para>
	///   <para><b>约束或前提</b>：定义域开区间 −1 到 1（不含端点）；|x| 等于或大于 1 时发散或无解，得到无穷/NaN 的具体分界行为 [待实测]。把 sigmoid 输出反解成对数几率就是用它，喂进 0/1 边界值会炸。</para>
	///   <para><b>与相邻算子的取舍</b>：正向 TupleTanh 的值域恰好是开区间 (-1,1)，所以 tanh 的逆变换安全；对超出范围的原始数据先截断再调本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple ps = new JlTuple(0.5, -0.25);
	///   JlTuple logs = ps.TupleAtanh();     // (约0.5493, 约-0.2554)
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple。</para>
	/// </remarks>
	public JlTuple TupleAtanh()
	{
		IntPtr proc = JlNativeApi.PreCall(2071);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素求实数立方根。</summary>
	/// <returns>与输入等长的 DOUBLE 新元组；负数得到负的实根。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2072），按 DOUBLE 装载结果。</para>
	///   <para><b>与相邻算子的取舍</b>：这正是和"幂 1/3"写法的关键差别——对负元素，通用的 pow(x,1/3) 在实数域是未定义（得 NaN），cbrt 则照常返回负实根；体积反解边长之类可能出现负偏差的场合应该用它。</para>
	///   <para><b>约束或前提</b>：全实数域有定义，x=0 得 0。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple vs = new JlTuple(27.0, -8.0);
	///   JlTuple edges = vs.TupleCbrt();     // (3, -2)
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple；输入不被改动。</para>
	/// </remarks>
	public JlTuple TupleCbrt()
	{
		IntPtr proc = JlNativeApi.PreCall(2072);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素求误差函数 erf(x)。</summary>
	/// <returns>与输入等长的 DOUBLE 新元组，值落在 (-1,1)。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2073），按 DOUBLE 装载。erf 是奇函数，erf(0)=0，|x| 约大于 4 后饱和到 ±1。</para>
	///   <para><b>与相邻算子的取舍</b>：要算"离中心多远之外"的尾部概率（1−erf 这类），别写 1−TupleErf——x 大时 erf 已贴近 1，减法把有效位全吃掉，应改用 <see cref="TupleErfc"/> 直接算补函数。</para>
	///   <para><b>约束或前提</b>：输入无定义域限制；与正态分布换算时注意 erf(x/√2) 才是 Φ 函数的形态。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple zs = new JlTuple(0.0, 1.0);
	///   JlTuple es = zs.TupleErf();         // (0, 约0.8427)
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple。</para>
	/// </remarks>
	public JlTuple TupleErf()
	{
		IntPtr proc = JlNativeApi.PreCall(2073);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素求互补误差函数 erfc(x) = 1 − erf(x)。</summary>
	/// <returns>与输入等长的 DOUBLE 新元组，值落在 (0,2)。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2074），按 DOUBLE 装载。原生直接算补函数，不是先算 erf 再相减。</para>
	///   <para><b>与相邻算子的取舍</b>：x 正大时 erfc 是极小量，用 1−<see cref="TupleErf"/> 会得到被舍入误差淹没的 0 或假值；尾部概率/误报率一律用本方法。反向（x 负大）时 erfc 贴近 2，此时 erf 本身才稳定。</para>
	///   <para><b>约束或前提</b>：输入无定义域限制；erfc(0)=1。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple tails = new JlTuple(3.0);
	///   double p = tails.TupleErfc();       // 约 2.2e-5，单元素可隐式转 double
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple；示例的隐式 double 转换仅对单元素结果安全。</para>
	/// </remarks>
	public JlTuple TupleErfc()
	{
		IntPtr proc = JlNativeApi.PreCall(2074);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素求 10 的 x 次幂。</summary>
	/// <returns>与输入等长的 DOUBLE 新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2075），按 DOUBLE 装载。输入是"指数是多少个数量级"，输出是倍数。</para>
	///   <para><b>与相邻算子的取舍</b>：底数是 e 用 TupleExp，底数是 2 用 <see cref="TupleExp2"/>；对数刻度换算（dB、等级差）回线性时必须是 10 为底，别拿 TupleExp 顶替。</para>
	///   <para><b>约束或前提</b>：x 约大于 308.5 时超出 double 范围溢出为无穷 [待实测：原生给 inf 还是报错]；x 为负得 (0,1) 的小数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple decades = new JlTuple(-3.0, 0.0, 3.0);
	///   JlTuple linear = decades.TupleExp10();   // (0.001, 1, 1000)
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple；输入不被改动。</para>
	/// </remarks>
	public JlTuple TupleExp10()
	{
		IntPtr proc = JlNativeApi.PreCall(2075);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素求 2 的 x 次幂。</summary>
	/// <returns>与输入等长的 DOUBLE 新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2076），按 DOUBLE 装载。整数指数得到精确的 2 的幂（double 可精确表示），位宽/金字塔层级换算直接用它。</para>
	///   <para><b>与相邻算子的取舍</b>：非整数倍的数量级换算用 <see cref="TupleExp10"/>，自然指数用 TupleExp；x 约大于 1024 时溢出为无穷 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple levels = new JlTuple(0.0, 8.0, 16.0);
	///   JlTuple sizes = levels.TupleExp2();   // (1, 256, 65536)
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple。</para>
	/// </remarks>
	public JlTuple TupleExp2()
	{
		IntPtr proc = JlNativeApi.PreCall(2076);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素求斜边 √(this² + t2²)。</summary>
	/// <param name="t2">第二个分量所在的元组；钉在原生输入 1。</param>
	/// <returns>与输入按广播规则等长的 DOUBLE 新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2077），本元组是 dx 分量、t2 是 dy 分量，逐位配对求欧氏距离的一维形态（径向距离、偏移量合成）。</para>
	///   <para><b>与相邻算子的取舍</b>：别用 TupleSqrt(dx²+dy²) 手工拼——分量接近 double 上限时中间平方先溢出，hypot 内部做了缩放规避；两分量同长度时逐位配对，其中一方为单元素则广播。</para>
	///   <para><b>约束或前提</b>：dx、dy 长度不匹配且都不是 1 时的对齐规则由原生决定 [待实测]；结果恒非负。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple dx = new JlTuple(3.0, 0.0);
	///   JlTuple dy = new JlTuple(4.0, 5.0);
	///   JlTuple r = dx.TupleHypot(dy);      // (5, 5)
	///   </code>
	///   <para><b>资源与坑</b>：本元组与 t2 都走 Store 钉固定，调用后各 UnpinTuple 一次；t2 在原生调用结束前不得 Dispose。</para>
	/// </remarks>
	public JlTuple TupleHypot(JlTuple t2)
	{
		IntPtr proc = JlNativeApi.PreCall(2077);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, t2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(t2);
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素求 ln|Γ(x)|（伽马函数绝对值的自然对数）。</summary>
	/// <returns>与输入等长的 DOUBLE 新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2078），按 DOUBLE 装载。Γ(n+1)=n!，所以这是"阶乘/组合数对数化"的稳定形态。</para>
	///   <para><b>与相邻算子的取舍</b>：算大数阶乘或对数似然时别用 <see cref="TupleTgamma"/> 再取 log——Γ 在 x 约大于 171 就溢成无穷，lgamma 全程对数值不会炸；本方法取的是绝对值，Γ 在负非整数区的符号信息被抹掉 [待实测：负参数具体行为]。</para>
	///   <para><b>约束或前提</b>：非正整数（Γ 的极点位置）处发散，返回什么由原生决定 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple ns = new JlTuple(5.0, 20.0);
	///   JlTuple logFact = ns.TupleLgamma();   // 分别约 ln(24), ln(20!)
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple。</para>
	/// </remarks>
	public JlTuple TupleLgamma()
	{
		IntPtr proc = JlNativeApi.PreCall(2078);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素求以 2 为底的对数。</summary>
	/// <returns>与输入等长的 DOUBLE 新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2079），按 DOUBLE 装载。答案是"相当于 2 的多少次幂"，位深、倍频程、信息量（比特）换算用它。</para>
	///   <para><b>与相邻算子的取舍</b>：自然对数用 TupleLog，常用对数（10 底）用 <see cref="TupleLog10"/>；log2 对 2 的整数幂给出精确整数结果，而 ln 换算除 ln2 会引入舍入。</para>
	///   <para><b>约束或前提</b>：定义域 x 大于 0；0 与负数分别发散/无解，得 -inf 或 NaN 还是报错 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple bits = new JlTuple(1.0, 256.0);
	///   JlTuple widths = bits.TupleLog2();   // (0, 8)
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple。</para>
	/// </remarks>
	public JlTuple TupleLog2()
	{
		IntPtr proc = JlNativeApi.PreCall(2079);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素求伽马函数 Γ(x)。</summary>
	/// <returns>与输入等长的 DOUBLE 新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2080），按 DOUBLE 装载。正整数处 Γ(n)=(n−1)!，是阶乘向实数的延拓。</para>
	///   <para><b>与相邻算子的取舍</b>：参数可能超过约 171 时必须换 <see cref="TupleLgamma"/>——Γ 增长极快，DOUBLE 装不下直接溢出，而 lgamma 仍可控；本方法保留 Γ 的符号（负区交错），lgamma 只剩绝对值。</para>
	///   <para><b>约束或前提</b>：非正整数是 Γ 的极点，函数值发散，具体返回无穷还是报错 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple ns = new JlTuple(5.0, 0.5);
	///   JlTuple gs = ns.TupleTgamma();      // (24, 约1.7725=√π)
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple。</para>
	/// </remarks>
	public JlTuple TupleTgamma()
	{
		IntPtr proc = JlNativeApi.PreCall(2080);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>用分隔符把本元组的字符串元素串接成一个字符串。</summary>
	/// <param name="separators">分隔符字符串元组；钉在原生输入 1。</param>
	/// <returns>MIXED 装载的新元组，内容为串接结果（单元素字符串）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2155），在相邻两个元素之间插入分隔符做整体串接；首尾不加。</para>
	///   <para><b>约束或前提</b>：本元组应为字符串（或可 stringify 的值）；多元素 separators 时的分配/循环规则由原生决定 [待实测]。空元组的结果 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只是把多个元素拼成一串用本方法；要把已有子串换掉用 <see cref="TupleStrReplace"/>，要按序号切回数组需要另行分割算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple parts = new JlTuple("a", "b", "c");
	///   string line = parts.TupleJoin(", ");   // 单元素结果隐式转 string："a, b, c"
	///   </code>
	///   <para><b>资源与坑</b>：本元组与 separators 均 Store 钉固定、调用后各 UnpinTuple；separators 在原生调用结束前不得 Dispose。</para>
	/// </remarks>
	public JlTuple TupleJoin(JlTuple separators)
	{
		IntPtr proc = JlNativeApi.PreCall(2155);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, separators);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(separators);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>按名字取 JlDevelop 语言常量的值，生成元组。</summary>
	/// <param name="name">常量名字符串元组，钉在原生输入 0。Default: "H_INT32_MIN"</param>
	/// <returns>MIXED 装载的新元组，即该常量的值（数值或字符串常量视定义而定）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2168）。常量表在原生侧，C# 不镜像其清单；用来把语言级边界值（如 int32 最小值）取进元组参与运算。</para>
	///   <para><b>与相邻算子的取舍</b>：普通字面量直接 new JlTuple(...) 即可，走本方法是为了拿到与原生一致的极值/系统常量，避免手写常数与原生宽度不符。</para>
	///   <para><b>约束或前提</b>：名字不存在时报错还是返回空元组由原生决定 [待实测]；本库还有哪些常量名无清单可查 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple minInt32 = JlTuple.TupleConstant("H_INT32_MIN");
	///   int v = minInt32;                  // 单元素常数元组隐式转 int
	///   </code>
	///   <para><b>资源与坑</b>：静态方法；name 走 Store 钉固定、调用后 UnpinTuple。返回的新元组纯数值时无需 Dispose 也安全。</para>
	/// </remarks>
	public static JlTuple TupleConstant(JlTuple name)
	{
		IntPtr proc = JlNativeApi.PreCall(2168);
		JlNativeApi.Store(proc, 0, name);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(name);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>逐元素检查是否为 NaN。</summary>
	/// <returns>与输入等长的 INTEGER 0/1 元组，1=该元素是 NaN。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2169）。NaN 不等于自身，普通比较表达式在元组上无法直接筛查，这就是需要逐元素判 NaN 的原因；常见来源是越界数学运算（如对数取负、acosh 小于 1 的参数）的下游污染检测。</para>
	///   <para><b>与相邻算子的取舍</b>：想接着筛掉 NaN 位的元素，把结果元组喂给按标志位取子集的索引/选择族算子，而不是逐个 if 判断。</para>
	///   <para><b>约束或前提</b>：非 DOUBLE 元素（整数/字符串）恒判 0 [待实测]；空元组结果亦空。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple vals = new JlTuple(1.0, 0.0 / 0.0);
	///   JlTuple bad = vals.TupleIsNanElem();   // (0, 1)
	///   </code>
	///   <para><b>资源与坑</b>：Store 钉本元组、调用后 UnpinTuple；注意本方法只看 DOUBLE 元素的 NaN 标志，句柄元素无效不算 NaN，用 <see cref="TupleIsValidHandle"/>。</para>
	/// </remarks>
	public JlTuple TupleIsNanElem()
	{
		IntPtr proc = JlNativeApi.PreCall(2169);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把整条元组的内容重复 num 次拼接。</summary>
	/// <param name="num">重复次数元组；钉在原生输入 1，通常给单元素整数字面量。</param>
	/// <returns>MIXED 装载的新元组，长度为原长度×num。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2184）。复制的是"整条序列"：结果按 原序列、原序列、…… 排布，原有元素顺序在每一轮里保持不变。</para>
	///   <para><b>与相邻算子的取舍</b>：想要 (a,b) 变成 (a,a,b,b) 那种逐元素连续重复，用 <see cref="TupleRepeatElem"/>（id 2185），两者输出顺序完全不同，选错不会报错、只会静默错位下游的 row/col 配对。</para>
	///   <para><b>约束或前提</b>：num 为 0 或负数时的行为（空元组/报错）[待实测]；num 传多元素时如何取值由原生决定 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple pattern = new JlTuple(1, 2);
	///   JlTuple tiled = pattern.TupleRepeat(3);   // (1,2,1,2,1,2)
	///   </code>
	///   <para><b>资源与坑</b>：本元组与 num 均钉固定、调用后各 UnpinTuple；句柄元组重复后元素仍是原句柄值，Dispose 归属注意别重复释放。</para>
	/// </remarks>
	public JlTuple TupleRepeat(JlTuple num)
	{
		IntPtr proc = JlNativeApi.PreCall(2184);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, num);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(num);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把元组的每个元素各自连续重复 num 次。</summary>
	/// <param name="num">每个元素的重复次数元组；钉在原生输入 1。</param>
	/// <returns>MIXED 装载的新元组，长度为原长度×num。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2185）。(a,b,c) 重复 2 次得 (a,a,b,b,c,c)——同一元素的副本相邻成组。</para>
	///   <para><b>与相邻算子的取舍</b>：与 <see cref="TupleRepeat"/>（整条序列滚动复制，得 a,b,c,a,b,c）互为顺序陷阱：两者长度相同、内容相同、排列不同，报错是不会有的，下游按下标配对（row 对 col）就全错位。给"每个标签展开 n 行"这类需求用本方法。</para>
	///   <para><b>约束或前提</b>：num 为 0/负数、或多元素 num 的取值规则由原生决定 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple labels = new JlTuple("L", "R");
	///   JlTuple expanded = labels.TupleRepeatElem(2);   // ("L","L","R","R")
	///   </code>
	///   <para><b>资源与坑</b>：本元组与 num 均钉固定、调用后各 UnpinTuple；空元组输入应得空输出 [待实测]。</para>
	/// </remarks>
	public JlTuple TupleRepeatElem(JlTuple num)
	{
		IntPtr proc = JlNativeApi.PreCall(2185);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, num);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(num);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>在元组各字符串里把子串 before 的全部出现替换为 after。</summary>
	/// <param name="before">待搜索的子串元组（钉在原生输入 1）。</param>
	/// <param name="after">用来替换的子串元组（钉在原生输入 2）。</param>
	/// <returns>MIXED 装载的新元组，元素个数与内容顺序不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2186）。字面量子串替换、命中处全部替换（不是只换第一处），不支持正则语法 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：改文件名前缀/后缀这类定点裁剪别拿它模拟，容易误伤字符串中部的同名片段；它适合明确的整体子串改写（如把路径分隔符统一）。</para>
	///   <para><b>约束或前提</b>：before 为空串时的行为 [待实测]；before 与 after 长度不同时结果字符串随之伸缩，元组元素数不变。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple names = new JlTuple("img_01.raw", "img_02.raw");
	///   JlTuple renamed = names.TupleStrReplace("raw", "tiff");   // 两处后缀都换
	///   </code>
	///   <para><b>资源与坑</b>：三个元组（this、before、after）各自钉固定、调用后逐一 UnpinTuple；它们在本方法返回前都不得 Dispose。</para>
	/// </remarks>
	public JlTuple TupleStrReplace(JlTuple before, JlTuple after)
	{
		IntPtr proc = JlNativeApi.PreCall(2186);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, before);
		JlNativeApi.Store(proc, 2, after);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(before);
		JlNativeApi.UnpinTuple(after);
		err = LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐元素计算本元组字符串与另一元组字符串的距离。</summary>
	/// <param name="string2">配对的字符串元组（钉在原生输入 1）。Default: "String2"</param>
	/// <param name="mode">距离度量名的字符串元组（钉在原生输入 2）。Default: "levenshtein"</param>
	/// <returns>与按广播规则等长的 INTEGER 新元组，每一位是一对字符串的距离。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：调用原生算子（id 2193）。默认 levenshtein：把一个字符串改成另一个所需的最少单字符编辑次数，结果是非负整数而非 0/1 相似度。</para>
	///   <para><b>与相邻算子的取舍</b>：字符串精确匹配用相等类算子更便宜；本方法的价值在"识别结果差一两个字符"的模糊比对。除 levenshtein 外还支持哪些度量名，本库代码未列出 [待实测]。</para>
	///   <para><b>约束或前提</b>：string2 单元素时广播配对、多元素时逐位对齐，长度不匹配且无法广播的行为 [待实测]；非字符串元素如何度量 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple got = new JlTuple("hallo");
	///   JlTuple want = new JlTuple("hello");
	///   int dist = got.TupleStrDistance(want, "levenshtein");   // 1
	///   </code>
	///   <para><b>资源与坑</b>：this、string2、mode 三个元组都钉固定、调用后逐一 UnpinTuple；单元素结果隐式转 int 方便，但多元素结果这样转只会拿到第一个距离——静默丢弃其余。</para>
	/// </remarks>
	public JlTuple TupleStrDistance(JlTuple string2, JlTuple mode)
	{
		IntPtr proc = JlNativeApi.PreCall(2193);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, string2);
		JlNativeApi.Store(proc, 2, mode);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(string2);
		JlNativeApi.UnpinTuple(mode);
		err = LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>创建空元组（EMPTY）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：纯 C# 构造，data 直接置为共享的 JlTupleVoid.EMPTY，不经任何原生调用、不钉固定。</para>
	///   <para><b>约束或前提</b>：空元组没有元素，索引器取用必然越界；但可作为拼接/累加的起点，多数元组算子对空输入返回空。</para>
	///   <para><b>与相邻算子的取舍</b>：想要"有类型但零长"的场合请核对原生行为，本构造只保证 Length==0、Type==EMPTY。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple acc = new JlTuple();
	///   acc = acc.TupleConcat(new JlTuple(7));   // 空串上拼接得到 (7)
	///   </code>
	///   <para><b>资源与坑</b>：EMPTY 是共享实现对象，Dispose 对空元组无实际回收意义。</para>
	/// </remarks>
	public JlTuple()
	{
		data = JlTupleVoid.EMPTY;
	}

	/// <summary>用 bool 创建单元素整数元组：true→1、false→0。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：委托给 JlTuple(IntPtr) 构造，所以存储宽度跟着指针宽度走——64 位进程得到 LONG(64-bit int)，32 位进程得到 INTEGER(32-bit int)。这是"平台相关整数"构造，不是普通 int 构造。</para>
	///   <para><b>与相邻算子的取舍</b>：想要恒 32 位的 0/1 标志（如喂给按 INTEGER 读结果的算子）请写 new JlTuple(b ? 1 : 0)；本构造适合与句柄/指针同宽度的场合。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple flag = new JlTuple(true);
	///   int n = flag;          // 隐式转 int：1
	///   </code>
	///   <para><b>资源与坑</b>：纯数值内容，Dispose 无实际意义。</para>
	/// </remarks>
	public JlTuple(bool b)
		: this(new IntPtr(b ? 1 : 0))
	{
	}

	/// <summary>用单个 32 位整数创建 INTEGER 型元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：纯 C# 构造（new JlTupleInt32），不触原生。存储宽度恒 32 位，与平台无关。</para>
	///   <para><b>与相邻算子的取舍</b>：有些算子按 INTEGER 装载输出、有些按 LONG（如平台相关宽度，见 JlTuple(IntPtr)）；拿本元组与 64 位整数元组混算时结果宽度由原生广播规则决定，需要精确宽度时用 JlTuple(long) 显式指定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple count = new JlTuple(42);
	///   int n = count;           // 隐式转 int，取回 42
	///   </code>
	///   <para><b>资源与坑</b>：单值构造无共享状态，Dispose 无意义。</para>
	/// </remarks>
	public JlTuple(int i)
	{
		data = new JlTupleInt32(i);
	}

	/// <summary>用一组 32 位整数创建 INTEGER 型元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：纯 C# 构造，JlTupleInt32(i, copy: true) —— 传入数组会被拷贝一份，之后你改动原数组不影响元组内容，反之亦然。</para>
	///   <para><b>与相邻算子的取舍</b>：直接传整数字面量序列 new JlTuple(1, 2, 3) 即可（params）；数据本来就是 long[]/double[] 时别硬转 int[]——超 32 位会截断，改用对应宽度构造。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple idx = new JlTuple(10, 20, 30);
	///   int second = idx[1];     // 索引器取第二元素：20
	///   </code>
	///   <para><b>资源与坑</b>：元素类型统一 INTEGER，整条元组判型一致；copy:true 意味着构造本身就有 O(n) 拷贝开销。</para>
	/// </remarks>
	public JlTuple(params int[] i)
	{
		data = new JlTupleInt32(i, copy: true);
	}

	/// <summary>用单个 64 位整数创建 LONG 型元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：纯 C# 构造（new JlTupleInt64），Type 为 LONG，可精确容纳超出 int 范围的计数/时间戳。</para>
	///   <para><b>与相邻算子的取舍</b>：与 JlTuple(int) 的选择就是宽度选择——下游算子若按 INTEGER 读结果，LONG 输入可能被原生改写宽度 [待实测]；确认无大于 2³¹ 的值时按习惯用 int 系即可。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple big = new JlTuple(5000000000L);
	///   long v = big;            // 隐式转 long：5000000000
	///   </code>
	///   <para><b>资源与坑</b>：把 double 或字符串元组用 ToIArr 读成 int 时是静默 (int) 截断，与本构造无关但常在此混淆。</para>
	/// </remarks>
	public JlTuple(long l)
	{
		data = new JlTupleInt64(l);
	}

	/// <summary>用一组 64 位整数创建 LONG 型元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：纯 C# 构造，JlTupleInt64(l, copy: true)——数组被拷贝，构造后改动原数组不影响元组。</para>
	///   <para><b>与相邻算子的取舍</b>：混合大小写数字面量时用 L 后缀（new JlTuple(1L, 2L)）确保走本重载，否则会被 int 系重载捕获、Type 成 INTEGER。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple stamps = new JlTuple(100L, 200L, 300L);
	///   long first = stamps[0];  // 索引器元素隐式转 long
	///   </code>
	///   <para><b>资源与坑</b>：copy:true 有 O(n) 拷贝；如需零拷贝语义请从 ToLArr/ReadTuple 等路径接管数据。</para>
	/// </remarks>
	public JlTuple(params long[] l)
	{
		data = new JlTupleInt64(l, copy: true);
	}

	/// <summary>用指针值创建单元素整数元组，宽度随平台。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：委托给 JlTuple(IntPtr[])：64 位进程存成 LONG，32 位进程存成 INTEGER——这就是"平台相关整数"的来源，bool 构造也走这条路。</para>
	///   <para><b>与相邻算子的取舍</b>：指针/地址数值要用它保证与原生 intptr 同宽；普通的"小整数"计数直接用 JlTuple(int)，避免结果宽度随 32/64 位构建漂移。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple addr = new JlTuple(new IntPtr(0x1000));
	///   long v = addr;           // 隐式转 long 读回指针数值
	///   </code>
	///   <para><b>资源与坑</b>：元组只存整数，不持有也不负责释放该地址指向的内存；把句柄当真指针塞进来是常见误用，句柄请用 JlTuple(JlHandle)。</para>
	/// </remarks>
	public JlTuple(IntPtr ip)
		: this(new IntPtr[1] { ip })
	{
	}

	/// <summary>用一组指针值创建整数元组，宽度随平台。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：64 位进程把每个 IntPtr 转 long 存成 LONG 元组，32 位进程转 int 存成 INTEGER 元组；转换在构造循环里完成，不做越界检查（32 位进程塞 64 位指针会截断）。</para>
	///   <para><b>与相邻算子的取舍</b>：JlTuple(JlHandle[]) 才是句柄集合的正确载体——它带克隆/释放语义；本构造只搬运裸整数值，谁来释放指向的东西一概不管。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple addrs = new JlTuple(new IntPtr(0x100), new IntPtr(0x200));
	///   int n = addrs.Length;    // 2
	///   </code>
	///   <para><b>资源与坑</b>：传入数组本身不拷贝（内部 new 了目标宽度数组逐元素转换），但目标数组是新建的，改动入参 IntPtr[] 不影响元组。</para>
	/// </remarks>
	public JlTuple(params IntPtr[] ip)
	{
		if (JlNativeApi.isPlatform64)
		{
			long[] array = new long[ip.Length];
			for (int i = 0; i < ip.Length; i++)
			{
				array[i] = ip[i].ToInt64();
			}
			data = new JlTupleInt64(array, copy: false);
		}
		else
		{
			int[] array2 = new int[ip.Length];
			for (int j = 0; j < ip.Length; j++)
			{
				array2[j] = ip[j].ToInt32();
			}
			data = new JlTupleInt32(array2, copy: false);
		}
	}

	internal JlTuple(int i, bool platformSize)
	{
		if (platformSize && JlNativeApi.isPlatform64)
		{
			data = new JlTupleInt64(i);
		}
		else
		{
			data = new JlTupleInt32(i);
		}
	}

	/// <summary>用单个 double 创建 DOUBLE 型元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：纯 C# 构造（new JlTupleDouble），不触原生；Type 为 DOUBLE，坐标/角度等连续量都该落在这里。</para>
	///   <para><b>与相邻算子的取舍</b>：整数宽度用 int/long 系构造，别为了"看起来精确"用 double 存索引——部分按索引取用的路径会校验 DOUBLE 下标是否含小数（GetIndicesFromTuple 遇到小数下标抛 JlTupleAccessException）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple angle = new JlTuple(1.5707963);
	///   double rad = angle;      // 隐式转 double
	///   </code>
	///   <para><b>资源与坑</b>：0.0 与 -0.0、NaN 均可存入；参与等值比较时 NaN 永不相等。</para>
	/// </remarks>
	public JlTuple(double d)
	{
		data = new JlTupleDouble(d);
	}

	/// <summary>用一组 double 创建 DOUBLE 型元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：纯 C# 构造，JlTupleDouble(d, copy: true)——数组拷贝进元组，改动原数组不影响已建元组。</para>
	///   <para><b>与相邻算子的取舍</b>：坐标对（row、col）成对传给算子时可用两条 DOUBLE 元组或 MIXED 构造；float[] 数据用 <see cref="JlTuple(float)"/> 系构造更直白，但存储同为 DOUBLE。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple xs = new JlTuple(0.5, 1.5, 2.5);
	///   int n = xs.Length;       // 3
	///   </code>
	///   <para><b>资源与坑</b>：copy:true 有 O(n) 开销；空数组得到零长 DOUBLE 元组（与 EMPTY 不同，Type 仍是 DOUBLE）。</para>
	/// </remarks>
	public JlTuple(params double[] d)
	{
		data = new JlTupleDouble(d, copy: true);
	}

	/// <summary>用单个 float 创建 DOUBLE 型元组（float 被提升为 double 存储）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：委托 JlTupleDouble(f)，元组里没有 FLOAT 存储——float 只是入口宽度，存下即变 64 位。</para>
	///   <para><b>与相邻算子的取舍</b>：若你要的就是 double 数值，直接 <see cref="JlTuple(double)"/>；本重载是给 float 变量的免强转入口。取回 float 用 ToFArr，会再收窄一次。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(1.5f);
	///   double v = t;            // 1.5，DOUBLE 存储
	///   </code>
	///   <para><b>资源与坑</b>：Type 是 DOUBLE 不是 FLOAT——拿它做类型分支判断时别期待见到 float。</para>
	/// </remarks>
	public JlTuple(float f)
	{
		data = new JlTupleDouble(f);
	}

	/// <summary>用一组 float 创建 DOUBLE 型元组（全部提升为 double 存储）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：JlTupleDouble(f) 收 float[] 逐元素拓宽；与 double[] 构造的区别只是入参类型，落库宽度相同。</para>
	///   <para><b>与相邻算子的取舍</b>：需要真正保宽的 float 数组请用 C# float[] 自己拿着，元组体系里没有 FLOAT 存储层；从元组取 float 走 ToFArr。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple vs = new JlTuple(0.25f, 0.5f);
	///   float a = (float)(double)vs[0];   // 元素先按 double 取回再收窄成 float
	///   </code>
	///   <para><b>资源与坑</b>：拓宽不丢精度，收窄（ToFArr）才丢；链路上多次来回会累积舍入。</para>
	/// </remarks>
	public JlTuple(params float[] f)
	{
		data = new JlTupleDouble(f);
	}

	/// <summary>用单个字符串创建 STRING 型元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：纯 C# 构造（new JlTupleString）。传 null 字符串的行为取决于实现，字符串元组与"空元组"是两回事。</para>
	///   <para><b>与相邻算子的取舍</b>：多个路径/标签用 <see cref="JlTuple(string[])"/> 一次建齐；数值别塞进字符串元组——多数算术算子不认 STRING，ToDArr 之类取回也会抛访问异常。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple name = new JlTuple("logo_model");
	///   string s = name;         // 单元素字符串元组隐式转 string
	///   </code>
	///   <para><b>资源与坑</b>：字符串以 UTF 口径进入原生；隐式 operator string 只读首元素。</para>
	/// </remarks>
	public JlTuple(string s)
	{
		data = new JlTupleString(s);
	}

	/// <summary>用一组字符串创建 STRING 型元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：纯 C# 构造，JlTupleString(s, copy: true)——引用数组被拷贝一份（元素字符串本身不可变，无需深拷贝）。</para>
	///   <para><b>与相邻算子的取舍</b>：混合数值与文本要用 JlTuple(params object[])（MIXED）；纯文本集合一律用它，TupleJoin/TupleStrReplace 等字符串算子按 STRING 输入设计。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple files = new JlTuple("a.png", "b.png");
	///   string first = files[0];   // 索引器元素隐式转 string
	///   </code>
	///   <para><b>资源与坑</b>：无原生调用、无钉固定；元素顺序即传参顺序，下游按序号取名字依赖该顺序。</para>
	/// </remarks>
	public JlTuple(params string[] s)
	{
		data = new JlTupleString(s, copy: true);
	}

	/// <summary>用单个句柄创建 HANDLE 型元组（存入的是句柄克隆）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：JlTupleHandle 构造里对入参执行 new JlHandle(h)——元组持有自己的句柄副本，原句柄归原主；整条元组 Type 为 HANDLE。</para>
	///   <para><b>与相邻算子的取舍</b>：想把图像/区域集合批量喂给算子就用它或数组版；混合了序号、分数等其他类型时才用 object[] 构造（MIXED），但注意 MIXED 整条判 TupleIsHandle 为 0。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle h = new JlHandle();
	///   JlTuple one = new JlTuple(h);
	///   one.Dispose();   // 只释放元组内的克隆，h 仍归调用方
	///   </code>
	///   <para><b>资源与坑</b>：JlTuple 实现 IDisposable 的意义就在这类元组——Dispose 逐个释放元组持有的句柄副本；纯数值/字符串元组调用 Dispose 是空操作。</para>
	/// </remarks>
	public JlTuple(JlHandle h)
	{
		data = new JlTupleHandle(h);
	}

	/// <summary>用一组句柄创建 HANDLE 型元组（每个元素都克隆入组）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：JlTupleHandle(h, copy: true) 对数组里每个句柄 new JlHandle(h[i])——顺序保持入参顺序；这条顺序就是下游 CountObj/索引筛选所依赖的序号基准，中途重排会静默错位。</para>
	///   <para><b>与相邻算子的取舍</b>：只有一个句柄时 JlTuple(JlHandle) 更直白；句柄与数值混排走 object[] 构造，但那就不是 HANDLE 整型元组，批量喂算子前先想清楚类型。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple group = new JlTuple(new JlHandle(), new JlHandle());
	///   int n = group.Length;    // 2
	///   group.Dispose();         // 释放组内两个克隆
	///   </code>
	///   <para><b>资源与坑</b>：元组释放的只是自己的副本；原句柄数组及其内容仍由创建方负责。传 null 数组与传空数组的差别未在构造里特判 [待实测]。</para>
	/// </remarks>
	public JlTuple(params JlHandle[] h)
	{
		data = new JlTupleHandle(h, copy: true);
	}

	internal JlTuple(object o)
	{
		data = new JlTupleMixed(o);
	}

	/// <summary>用混合值创建 MIXED 型元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：JlTupleMixed(o, copy: true)。可放整数、double、字符串与句柄的混排，各元素保留自己的类型；整条元组的 Type 是 MIXED。</para>
	///   <para><b>与相邻算子的取舍</b>：类型齐整的数据请用对应单类型构造——MIXED 让 TupleIsHandle 之类的整体判断退化为 0，逐元素判断要换 Elem 族算子，代价明显。超出上述类型支持范围的 object 会被如何处置 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rec = new JlTuple(7, "roi", 0.95);
	///   int idx = (int)rec[0];       // 按元素类型分别取回
	///   string tag = (string)rec[1];
	///   double score = rec[2];       // JlTupleElements 隐式转 double
	///   </code>
	///   <para><b>资源与坑</b>：若混入句柄元素，Dispose 元组会连带释放这些句柄副本；ToHArr 只有在全部元素都是句柄时才可用（否则抛"only allowed with handle types"访问异常）。</para>
	/// </remarks>
	public JlTuple(params object[] o)
	{
		data = new JlTupleMixed(o, copy: true);
	}

	/// <summary>由既有元组创建副本，保持原存储类型。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：按 t.Type 分派——EMPTY/INTEGER/LONG/DOUBLE/STRING/HANDLE/MIXED 各走对应实现类的构造，把 ToXArr 取出的数组以 copy:false 直接接管；数值是逐位复制的新数组。</para>
	///   <para><b>与相邻算子的取舍</b>：HANDLE/MIXED 的深度复制版是 Clone()（走序列化-反序列化）；本构造对句柄元素拿到的是 ToHArr 产出的句柄副本并被元组接管，不是共享引用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple src = new JlTuple(1.0, 2.0);
	///   JlTuple dup = new JlTuple(src);
	///   dup = dup + 10.0;        // 副本改动不影响 src
	///   </code>
	///   <para><b>资源与坑</b>：Type 落在枚举外会抛"Inconsistent tuple state"访问异常。隐式 operator JlTuple(JlHandle) 也可能把句柄误当单元素构造，注意与本法区分。</para>
	/// </remarks>
	public JlTuple(JlTuple t)
	{
		switch (t.Type)
		{
		case JlTupleType.EMPTY:
			data = JlTupleVoid.EMPTY;
			break;
		case JlTupleType.INTEGER:
			data = new JlTupleInt32(t.ToIArr(), copy: false);
			break;
		case JlTupleType.LONG:
			data = new JlTupleInt64(t.ToLArr(), copy: false);
			break;
		case JlTupleType.DOUBLE:
			data = new JlTupleDouble(t.ToDArr(), copy: false);
			break;
		case JlTupleType.STRING:
			data = new JlTupleString(t.ToSArr(), copy: false);
			break;
		case JlTupleType.JlANDLE:
			data = new JlTupleHandle(t.ToHArr(), copy: false);
			break;
		case JlTupleType.MIXED:
			data = new JlTupleMixed(t.ToOArr(), copy: false);
			break;
		default:
			throw new JlTupleAccessException("Inconsistent tuple state encountered");
		}
	}

	/// <summary>把多条既有元组首尾拼接，生成一条新元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：先建空元组再调 TupleConcat(t) 合并全部入参，最后 TransferOwnership 把结果接管过来——顺序即实参顺序，与 <see cref="JlTuple(JlTuple)"/>（单条复制）不是一回事。</para>
	///   <para><b>与相邻算子的取舍</b>：只拼两条也可以用 + 运算符或 TupleConcat(JlTuple)；本构造适合把 N 条一次合完。注意不传任何实参时得到的是空元组。</para>
	///   <para><b>约束或前提</b>：混合数值与字符串/HANDLE 元组拼接后的类型表现由 TupleConcat 的原生规则决定 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple head = new JlTuple(1, 2);
	///   JlTuple tail = new JlTuple(3, 4);
	///   JlTuple all = new JlTuple(head, tail);   // (1,2,3,4)
	///   </code>
	///   <para><b>资源与坑</b>：结果是新元组，head/tail 不被改动、也无需先 Dispose；TransferOwnership 是框架内部移交手段，用户代码不要手动调用。</para>
	/// </remarks>
	public JlTuple(params JlTuple[] t)
		: this()
	{
		JlTuple hTuple = new JlTuple();
		hTuple = hTuple.TupleConcat(t);
		TransferOwnership(hTuple);
	}

	internal JlTuple(JlTupleImplementation data)
	{
		this.data = data;
	}

	/// <summary>把源元组的内部存储整体接管进本实例并把源置空——纯引用转移，无原生调用、无拷贝。</summary>
	/// <param name="source">被接管的源元组；调用后其内容变为空元组（EMPTY）。传 null 则本实例直接置为 EMPTY。</param>
	/// <remarks>
	///   <para><b>功能说明</b>方法体仅交换引用：<c>data = source.data</c> 后 <c>source.data</c> 指向
	///   空实现 <c>JlTupleVoid.EMPTY</c>；<c>source</c> 与本实例相同时不做任何事。本实例原先持有的存储
	///   引用被直接丢弃（交由 GC/finalizer 处理）。</para>
	///   <para><b>约束或前提</b>标 <c>EditorBrowsable(Never)</c>，框架内部用于把"新建并装载好的元组"
	///   零拷贝转交出去（如 <c>JlTuple(params JlTuple[])</c> 构造尾部即调它）；用户代码用赋值/拼接即可，
	///   不要手动调用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple filled = new int[] { 1, 2, 3 };
	///   JlTuple target = new JlTuple();
	///   target.TransferOwnership(filled);   // 内容零拷贝移到 target
	///   int n = target.Length;              // 3
	///   int m = filled.Length;              // 0：filled 已被置空
	///   target.Dispose();
	///   filled.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>接管不是克隆：source 置空后不可再用；接管丢弃本实例旧存储时，若旧内容
	///   存有句柄元素其释放时机 [待实测]。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void TransferOwnership(JlTuple source)
	{
		if (source != this)
		{
			if (source == null)
			{
				data = JlTupleVoid.EMPTY;
				return;
			}
			data = source.data;
			source.data = JlTupleVoid.EMPTY;
		}
	}

	/// <summary>复制元组；HANDLE/MIXED 走序列化-反序列化字节流往返，其余直接构造副本。</summary>
	/// <returns>与原元组内容一致的新 JlTuple。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：分支在方法体里——Type 为 HANDLE 或 MIXED 时先 SerializeTuple 再 DeserializeTuple 绕一圈字节流；数值/字符串走 new JlTuple(this) 直接复制值。句柄经序列化往返后是否连对象内容真复制 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：只是一次引用级复制用 new JlTuple(this) 更便宜；对句柄集合想要"改副本不动原件"才需要本方法的序列化深复制，代价是全量字节流往返。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple src = new JlTuple(1.0, 2.0);
	///   JlTuple dup = src.Clone();
	///   </code>
	///   <para><b>资源与坑</b>：返回值是新 JlTuple（IDisposable）；句柄型副本持有独立句柄，用完各自 Dispose。</para>
	/// </remarks>
	public JlTuple Clone()
	{
		if (Type == JlTupleType.JlANDLE || Type == JlTupleType.MIXED)
		{
			byte[] data = SerializeTuple();
			return DeserializeTuple(data);
		}
		return new JlTuple(this);
	}

	/// <summary>释放元组内保存的句柄元素；不含句柄的元组调用无效果。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：转 data.Dispose()——只有 JlTupleHandle/JlTupleMixed 的实现真正逐个释放句柄副本；数值/字符串/空元组的实现是无回收动作的。JlTuple 虽实现 IDisposable，纯数值元组不调用也无可泄漏的原生资源。</para>
	///   <para><b>约束或前提</b>：释放的是元组自己持有的句柄克隆（构造时 copy 进来的），不波及外部同名句柄；用 TupleIsValidHandle 只能查元组内那份副本的有效性。</para>
	///   <para><b>与相邻算子的取舍</b>：想让原生清掉句柄指向的对象内容用 <see cref="ClearHandle"/>，想让原件继续存活就只 Dispose 元组、别 Dispose 原件。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple nums = new JlTuple(1, 2);
	///   nums.Dispose();              // 纯数值：空操作
	///   JlTuple group = new JlTuple(new JlHandle(), new JlHandle());
	///   group.Dispose();             // 释放组内两个句柄副本
	///   </code>
	///   <para><b>资源与坑</b>：Dispose 后句柄型元组的元素值已失效，再喂算子报无效句柄；重复 Dispose 是否二次释放取决于实现层，避免对同一元组调用两次 [待实测]。</para>
	/// </remarks>
	public void Dispose()
	{
		data.Dispose();
	}

	/// <summary>解除本元组在原生进程参数上的固定。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：转 data.UnpinTuple()。对应的固定动作发生在 Store(..) 里——各元组算子把本元组钉给原生后、CallProcedure 返回时统一调用本法解除。</para>
	///   <para><b>与相邻算子的取舍</b>：普通用户代码不该调它——本仓库所有元组算子都自己成对完成钉/解固定；只有你自己手写 PreCall/Store/CallProcedure 流程时，才需要在调用后手动 UnpinTuple 对齐。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(1, 2, 3);
	///   t.TupleAbs();       // 钉与解固定都在算子内部完成，无需手动 UnpinTuple
	///   </code>
	///   <para><b>资源与坑</b>：在未钉固定的元组上调用是多余的解除动作，是否报错取决于原生层 [待实测]；它与 Dispose 无关，解固定不释放任何句柄。</para>
	/// </remarks>
	public void UnpinTuple()
	{
		data.UnpinTuple();
	}

	internal static int[] GetIndicesFromTuple(JlTuple indices)
	{
		if (indices.Type == JlTupleType.LONG || indices.Type == JlTupleType.INTEGER)
		{
			return indices.ToIArr();
		}
		int[] array = new int[indices.Length];
		for (int i = 0; i < indices.Length; i++)
		{
			if (indices[i].Type == JlTupleType.INTEGER)
			{
				array[i] = indices[i].I;
				continue;
			}
			if (indices[i].Type == JlTupleType.LONG)
			{
				array[i] = indices[i].I;
				continue;
			}
			if (indices[i].Type == JlTupleType.DOUBLE)
			{
				double d = indices[i].D;
				int num = (int)d;
				if ((double)num != d)
				{
					throw new JlTupleAccessException("Index has fractional part");
				}
				array[i] = num;
				continue;
			}
			throw new JlTupleAccessException("Invalid index type");
		}
		return array;
	}

	private void ConvertToMixed()
	{
		if (!(data is JlTupleMixed))
		{
			JlTupleImplementation hTupleImplementation = new JlTupleMixed(data);
			data.Dispose();
			data = hTupleImplementation;
		}
	}

	internal JlTupleElementsMixed ConvertToMixed(int[] indices)
	{
		ConvertToMixed();
		return new JlTupleElementsMixed((JlTupleMixed)data, indices);
	}

	/// <summary>把元组数据取成 32 位整数数组（仅限整数型元组）。</summary>
	/// <returns>新建的 int[]，逐位对应元组元素。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：转 data.ToIArr()。INTEGER 元组是数组拷贝；LONG 元组逐元素 (int) 强转——超过 ±2³¹ 的值静默截断回绕，不检查不报错，这是本方法最大的坑。</para>
	///   <para><b>约束或前提</b>：DOUBLE/STRING/HANDLE/MIXED 元组调用会抛 JlTupleAccessException（"Cannot convert to int array"），没有就近取整的宽容路径；想要截断取整请先显式走算子。</para>
	///   <para><b>与相邻算子的取舍</b>：值可能超 32 位一律用 ToLArr；只喂原生参数时直接 Store 元组本体，绕开数组中间层。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple idx = new JlTuple(10, 20, 30);
	///   int[] arr = idx.ToIArr();          // {10, 20, 30}
	///   int sum = arr[0] + arr[1] + arr[2];
	///   </code>
	///   <para><b>资源与坑</b>：纯托管拷贝，无原生调用、无钉固定；空元组（EMPTY）返回零长数组而不抛错。</para>
	/// </remarks>
	public int[] ToIArr()
	{
		return data.ToIArr();
	}

	/// <summary>把元组数据取成 64 位整数数组（仅限整数型元组）。</summary>
	/// <returns>新建的 long[]，逐位对应元组元素。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：转 data.ToLArr()。INTEGER 元组逐位拓宽为 long，无损；LONG 元组是原样拷贝。</para>
	///   <para><b>与相邻算子的取舍</b>：读回宽度不确定是否超 32 位时用本方法而不是 ToIArr——后者对 LONG 元组会静默截断。要小数口径用 ToDArr，但注意 64 位大整数转 double 在 2⁵³ 以上丢精度。</para>
	///   <para><b>约束或前提</b>：DOUBLE/STRING/HANDLE/MIXED 元组抛 JlTupleAccessException（"Cannot convert to long array"）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple big = new JlTuple(5000000000L);
	///   long[] v = big.ToLArr();   // {5000000000}，用 ToIArr 就废了
	///   </code>
	///   <para><b>资源与坑</b>：纯托管转换，无原生调用；空元组返回零长数组。</para>
	/// </remarks>
	public long[] ToLArr()
	{
		return data.ToLArr();
	}

	/// <summary>把元组数据取成 double 数组（仅限数值型元组）。</summary>
	/// <returns>新建的 double[]；整数元素逐位拓宽为 double。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：转 data.ToDArr()。INTEGER/LONG/DOUBLE 元组都可取；整数在 ±2⁵³ 内转 double 精确，再大的 LONG 值会失去个位精度。</para>
	///   <para><b>与相邻算子的取舍</b>：要精确的大整数用 ToLArr；字符串元素不做自动解析——STRING/HANDLE/MIXED 元组调用本方法抛 JlTupleAccessException，须先在原生侧把字符串换成数值再取。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple coords = new JlTuple(1.5, 2.5);
	///   double[] xy = coords.ToDArr();   // {1.5, 2.5}
	///   double mid = (xy[0] + xy[1]) * 0.5;
	///   </code>
	///   <para><b>资源与坑</b>：纯托管转换；NaN、±inf 会原样进入数组，喂给算子前自己甄别。</para>
	/// </remarks>
	public double[] ToDArr()
	{
		return data.ToDArr();
	}

	/// <summary>把元组数据取成字符串数组。</summary>
	/// <returns>新建的 string[]；STRING 元组为内容拷贝，其他类型逐元素 ToString() 格式化。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：转 data.ToSArr()。基类实现是 GetValue(i).ToString()——所以对数值元组它不抛错而是给格式化文本，小数点符号跟随当前区域设置（de-DE 下变逗号），跨语言环境传文件要当心。</para>
	///   <para><b>与相邻算子的取舍</b>：要可回读的数值文本请检查区域设置或自行格式化；只要调试可见性用 ToString()（整条一个字符串）更省事；HANDLE 元素取回的文本是对象默认 ToString，不含句柄数值语义保证 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple tags = new JlTuple("left", "right");
	///   string[] names = tags.ToSArr();      // {"left", "right"}
	///   string joined = names[0] + "|" + names[1];
	///   </code>
	///   <para><b>资源与坑</b>：纯托管转换；结果与元组脱钩，改数组不影响元组。</para>
	/// </remarks>
	public string[] ToSArr()
	{
		return data.ToSArr();
	}

	/// <summary>把元组数据取成句柄数组（每个元素是新建的句柄副本）。</summary>
	/// <returns>JlHandle[]，元素为 new JlHandle(...) 克隆，必须由调用方逐个 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：转 data.ToHArr()。HANDLE 元组逐元素克隆；MIXED 元组要求所有元素都是句柄，遇到一个非句柄就抛"only allowed with handle types"。</para>
	///   <para><b>约束或前提</b>：数值/字符串元组调用抛 JlTupleAccessException（"Cannot convert to handle array"）。</para>
	///   <para><b>与相邻算子的取舍</b>：只是遍历取值不想管释放的话用 ToOArr 更直白（但句柄元素同样要清）；要把对象交回原生继续用，直接传元组本体，别绕数组克隆一圈。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple group = new JlTuple(new JlHandle(), new JlHandle());
	///   JlHandle[] hs = group.ToHArr();
	///   int n = hs.Length;           // 2
	///   foreach (JlHandle item in hs)
	///   {
	///       item.Dispose();          // 副本各自释放
	///   }
	///   </code>
	///   <para><b>资源与坑</b>：忘 Dispose 返回数组里的句柄就是句柄泄漏；本方法不释放元组内的原件，group 仍按自己的生命周期 Dispose。</para>
	/// </remarks>
	public JlHandle[] ToHArr()
	{
		return data.ToHArr();
	}

	/// <summary>把元组数据取成 object 数组（任何类型都可）。</summary>
	/// <returns>object[]：数值装箱为 int/long/double、字符串为 string、句柄为克隆的 JlHandle。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：转 data.ToOArr()，是最通用的取回方式。句柄/MIXED 里的句柄元素是 new JlHandle(...) 克隆——装箱成 object 不等于不用释放。</para>
	///   <para><b>与相邻算子的取舍</b>：类型齐整时用窄口径的 ToIArr/ToDArr/ToSArr，省掉装箱与模式匹配；本方法适合 MIXED 的逐元素 is/switch 分派，或 ToString 一类的调试路径。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple rec = new JlTuple(7, "roi");
	///   object[] items = rec.ToOArr();
	///   string text = items[0] + ":" + items[1];   // "7:roi"
	///   </code>
	///   <para><b>资源与坑</b>：若数组含 JlHandle 克隆，需对每个 is JlHandle 的元素补 Dispose，否则句柄泄漏；ToString() 内部就是这么做的（它取回后逐个释放 IDisposable 元素）。</para>
	/// </remarks>
	public object[] ToOArr()
	{
		return data.ToOArr();
	}

	/// <summary>把元组数据取成 float 数组（仅限数值型元组）。</summary>
	/// <returns>新建的 float[]；每个 double/整数元素被收窄为 float。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：转 data.ToFArr()。DOUBLE 元组逐元素 (float) 收窄，尾数只剩 24 位——传感器原始 float 数据可以无损，但经 DOUBLE 元组中转过的值再取回 float 有二次舍入。</para>
	///   <para><b>与相邻算子的取舍</b>：要保精度别过 float，直接用 ToDArr；超出 float 范围（约 ±3.4e38）的值变 ±inf。</para>
	///   <para><b>约束或前提</b>：INTEGER/LONG/DOUBLE 之外的存储类型抛 JlTupleAccessException（"Cannot convert to float array"）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple gains = new JlTuple(1.25, 2.75);
	///   float[] fs = gains.ToFArr();   // {1.25f, 2.75f}，两者可精确表示
	///   float sum = fs[0] + fs[1];
	///   </code>
	///   <para><b>资源与坑</b>：纯托管转换；数组是新建的，与元组互不影响。</para>
	/// </remarks>
	public float[] ToFArr()
	{
		return data.ToFArr();
	}

	/// <summary>把元组数据取成 IntPtr 数组（整数宽度必须与平台指针同宽）。</summary>
	/// <returns>新建的 IntPtr[]，逐元素按指针宽度重建。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：转 data.ToIPArr()。实现里 LONG 元组仅在 64 位进程可用、INTEGER 元组仅在 32 位进程可用；宽度不符时基类直接抛 JlTupleAccessException（"Values in tuple do not represent pointers on this platform"）。</para>
	///   <para><b>与相邻算子的取舍</b>：跨 32/64 位都要跑通的代码用 ToLArr 拿 long 再自行 new IntPtr；只有配合 JlTuple(IntPtr...) 构造进来的平台相关整数才该指望本法无损回来。</para>
	///   <para><b>约束或前提</b>：数值/字符串之外的存储类型同样抛；负数指针值原样保留。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple addrs = new JlTuple(new IntPtr(0x1000));   // 64 位下为 LONG
	///   IntPtr[] ps = addrs.ToIPArr();
	///   long addr0 = ps[0].ToInt64();
	///   </code>
	///   <para><b>资源与坑</b>：拿到的 IntPtr 只是数值拷贝，不持有任何内存所有权；32 位进程跑上面这段会因宽度不符抛异常。</para>
	/// </remarks>
	public IntPtr[] ToIPArr()
	{
		return data.ToIPArr();
	}

	/// <summary>把整条元组隐式转成 JlTupleElements 元素视图。</summary>
	/// <returns>单元素元组返回该元素本身；多元素返回按下标 0..n-1 取出的元素集合视图。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：方法体即 t.Length==1 ? t[0] : t[0..n-1] 的索引器路径，是视图而非拷贝——经由它写回会改到原元组。</para>
	///   <para><b>与相邻算子的取舍</b>：只想读第一个值时直接隐式转 int/double（那族只取 [0]）；要把整条元组当"元素们"传给接受 JlTupleElements 的重载才用这条隐式转换。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(1, 2);
	///   JlTupleElements view = t;          // 触发本隐式转换
	///   int[] vals = view.IArr;            // 视图取回整数数组：{1, 2}
	///   </code>
	///   <para><b>资源与坑</b>：空元组走多元素分支得到零长视图；视图随源元组生命周期，源被 Dispose 后经视图取句柄元素可能踩空。</para>
	/// </remarks>
	public static implicit operator JlTupleElements(JlTuple t)
	{
		if (t.Length == 1)
		{
			return t[0];
		}
		int[] array = new int[t.Length];
		for (int i = 0; i < t.Length; i++)
		{
			array[i] = i;
		}
		return t[array];
	}

	/// <summary>把元组隐式转成 bool：只看第一个元素，0 为 false、非 0 为 true。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：实现是 return t[0]（元素视图再隐式转 bool），后续元素被静默丢弃——多元素元组走这条转换不会有任何警告。</para>
	///   <para><b>与相邻算子的取舍</b>：要"全体非零才算真"的语义必须先自己做归约，别指望隐式转换；对 TupleIsHandle 一类单值结果直接隐式转 int 判断更稳。</para>
	///   <para><b>约束或前提</b>：首元素为字符串/句柄时能否转 bool 取决于元素层规则，失败口径 [待实测]；空元组取 [0] 必然越界抛异常。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple flag = new JlTuple(1);
	///   if (flag)   // 隐式走 bool 转换
	///   {
	///       int keep = flag[0];
	///   }
	///   </code>
	///   <para><b>资源与坑</b>：判据只有首元素的数值零性；LENGTH 大于 1 时几乎必然是误用。</para>
	/// </remarks>
	public static implicit operator bool(JlTuple t)
	{
		return t[0];
	}

	/// <summary>把元组隐式转成 int：只取第一个元素。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：实现为 return t[0]（元素层按整数口径读）。这是"单值元组当标量用"的糖衣：TupleConstant、TupleIsHandle 之类单元素结果直接赋给 int 最省事。</para>
	///   <para><b>与相邻算子的取舍</b>：多值元组请老实用 ToIArr()/索引器逐位取，隐式转换只保证第一格；首元素是 LONG 大值时经整数口径读出可能被截窄 [待实测：截断或抛错]。</para>
	///   <para><b>约束或前提</b>：DOUBLE 首元素按整数口径读取的行为（截断/抛错）[待实测]；空元组越界抛异常。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple one = new JlTuple(99);
	///   int n = one;             // 99
	///   int also = one[0];       // 显式索引写法，等价
	///   </code>
	///   <para><b>资源与坑</b>：与 bool 族同样"首元素之外全部丢弃"，是元组族最安静的坑，出现多值输入时应改代码而非依赖它。</para>
	/// </remarks>
	public static implicit operator int(JlTuple t)
	{
		return t[0];
	}

	/// <summary>把元组隐式转成 long：只取第一个元素。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：实现为 return t[0]（元素层按 64 位整数口径读）。读 LONG 元组（平台指针宽、大计数器）首选这个宽度，避开 int 版的截窄风险。</para>
	///   <para><b>与相邻算子的取舍</b>：与隐式 int/bool/double 同族同坑——一律只看首元素；INTEGER 首元素拓宽无损，DOUBLE/字符串首元素走 64 位整数口径的行为 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple stamp = new JlTuple(5000000000L);
	///   long v = stamp;          // 5000000000，隐式 long 转换
	///   </code>
	///   <para><b>资源与坑</b>：空元组越界抛异常；多元素结果其余被丢弃。</para>
	/// </remarks>
	public static implicit operator long(JlTuple t)
	{
		return t[0];
	}

	/// <summary>把元组隐式转成 double：只取第一个元素按小数口径读。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：实现为 return t[0]，元素层走 .D 读取（基类 getD 对 INTEGER/LONG 逐位拓宽为 double、DOUBLE 原样取回）。是"单值元组当标量用"的糖衣：坐标、角度、分数等连续量结果直接赋给 double 最省事。</para>
	///   <para><b>约束或前提</b>：STRING、HANDLE 或首元素非数值的 MIXED 元组会抛 JlTupleAccessException（基类 getD 直接抛，没有就近解析的宽容路径）；空元组取 [0] 必然越界抛 JlTupleAccessException("Index out of range")。</para>
	///   <para><b>与相邻算子的取舍</b>：要整数口径用同族的隐式 int/long（那两者对 DOUBLE 首元素另有截窄行为）；要全部元素走 double[] 版或 ToDArr；本转换只看首元素，多值输入其余被静默丢弃。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple angle = new JlTuple(1.5707963);
	///   double rad = angle;          // 1.5707963，只取首元素
	///   double also = angle[0];      // 显式索引写法，等价
	///   </code>
	///   <para><b>资源与坑</b>：LONG 首元素超过 2^53 时转 double 会丢个位精度 [待实测：整数元组走本转换是否可能截窄而非抛错]；纯数值元组无需 Dispose。</para>
	/// </remarks>
	public static implicit operator double(JlTuple t)
	{
		return t[0];
	}

	/// <summary>把元组隐式转成 string：只取第一个元素按字符串读。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：实现为 return t[0]，元素层走 .S 读取（读底层 string 数组首格）。取回模型名、路径、标签这类 STRING 元组的单值时最省事。</para>
	///   <para><b>约束或前提</b>：只有 STRING 型元组的首元素能走这条路；数值、句柄或 MIXED 首元素非字符串会抛 JlTupleAccessException（基类 getS 直接抛）。注意这与 ToSArr 不同——ToSArr 对数值做 GetValue(i).ToString() 就近格式化永不抛，而本转换不格式化、类型不符就抛。</para>
	///   <para><b>与相邻算子的取舍</b>：想拿"任何类型都转成文本"的宽容行为用 ToSArr 或整条 ToString()；只要多元素文本集合用 string[] 版；本转换只看首元素，空元组越界抛异常。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple name = new JlTuple("logo_model");
	///   string s = name;             // "logo_model"，只取首元素
	///   </code>
	///   <para><b>资源与坑</b>：数值元组误走本转换得到的是异常而非 "42" 之类的文本，是常见踩坑点；纯字符串元组 Dispose 无实际意义。</para>
	/// </remarks>
	public static implicit operator string(JlTuple t)
	{
		return t[0];
	}

	/// <summary>把元组隐式转成 IntPtr：只取第一个元素按平台指针宽度读。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：实现为 return t[0]，元素层走 .IP 读取。指针/地址语义的单值结果（原生回传的内存地址、句柄底层的 IntPtr）直接赋给 IntPtr 用它。</para>
	///   <para><b>约束或前提</b>：宽度必须与当前平台 IntPtr.Size 一致——64 位进程要求首元素是 LONG 或 HANDLE，32 位进程要求 INTEGER 或 HANDLE；不匹配抛 JlTupleAccessException("Value does not represent a pointer on this platform")。空元组越界抛异常。</para>
	///   <para><b>与相邻算子的取舍</b>：要"跨 32/64 位都能跑"请改走隐式 long 再自行 new IntPtr，别用本转换（它在错误位数构建上会抛）；要全部元素用 IntPtr[] 版（ToIPArr）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple addr = new JlTuple(new IntPtr(0x1000));   // 64 位下为 LONG
	///   IntPtr p = addr;             // 只取首元素，按指针宽读回
	///   long raw = p.ToInt64();
	///   </code>
	///   <para><b>资源与坑</b>：拿到的 IntPtr 只是数值拷贝，不持有任何内存所有权；同一份代码在 32 位进程可能因宽度不符抛异常。</para>
	/// </remarks>
	public static implicit operator IntPtr(JlTuple t)
	{
		return t[0];
	}

	/// <summary>把整条元组隐式转成 int[]：逐元素按 32 位整数口径取回。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：实现为 return t.ToIArr()，把<b>所有</b>元素转成 int[]（区别于只取首元素的标量转换）。INTEGER 元组直取，LONG 元组逐位 (int) 静默截断。</para>
	///   <para><b>约束或前提</b>：DOUBLE/STRING/HANDLE/MIXED 元组抛 JlTupleAccessException("Cannot convert to int array")，没有就近取整的宽容路径；空元组返回零长数组而不抛错（与标量版越界抛异常不同）。</para>
	///   <para><b>与相邻算子的取舍</b>：值可能超 32 位一律走 long[]（ToLArr）而不是本法，否则高位被砍；只要首元素用隐式 int；纯数值索引类结果本法最顺手。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple idx = new JlTuple(10, 20, 30);
	///   int[] arr = idx;             // 等价 ToIArr：{10, 20, 30}
	///   int sum = arr[0] + arr[1] + arr[2];
	///   </code>
	///   <para><b>资源与坑</b>：纯托管拷贝、无原生调用无钉固定；结果数组与元组脱钩，改数组不回写元组。</para>
	/// </remarks>
	public static implicit operator int[](JlTuple t)
	{
		return t.ToIArr();
	}

	/// <summary>把整条元组隐式转成 long[]：逐元素按 64 位整数口径取回。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：实现为 return t.ToLArr()，把<b>所有</b>元素转成 long[]。INTEGER 元组逐位无损拓宽，LONG 元组原样拷贝。</para>
	///   <para><b>约束或前提</b>：DOUBLE/STRING/HANDLE/MIXED 元组抛 JlTupleAccessException("Cannot convert to long array")；空元组返回零长数组不抛错。</para>
	///   <para><b>与相邻算子的取舍</b>：读回宽度可能超 32 位时用本法而不是 int[]（ToIArr 对 LONG 会静默截断）；要小数口径用 double[]，但注意 64 位大整数转 double 在 2^53 以上丢精度。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple stamps = new JlTuple(100L, 5000000000L);
	///   long[] v = stamps;           // {100, 5000000000}，用 int[] 版就废了
	///   long latest = v[1];
	///   </code>
	///   <para><b>资源与坑</b>：纯托管转换、无原生调用；结果数组新建，与元组互不影响。</para>
	/// </remarks>
	public static implicit operator long[](JlTuple t)
	{
		return t.ToLArr();
	}

	/// <summary>把整条元组隐式转成 double[]：逐元素按小数口径取回。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：实现为 return t.ToDArr()，把<b>所有</b>元素转成 double[]。INTEGER/LONG/DOUBLE 元组都可取，整数逐位拓宽（±2^53 内精确，再大的 LONG 丢个位精度）。</para>
	///   <para><b>约束或前提</b>：字符串元素不做自动解析——STRING/HANDLE/MIXED 元组抛 JlTupleAccessException，须先在原生侧换成数值再取；空元组返回零长数组不抛错。</para>
	///   <para><b>与相邻算子的取舍</b>：要精确大整数用 long[]；只要首元素用隐式 double；本法是"连续量结果批量取回"的主路径（坐标点集、灰度分布等）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple coords = new JlTuple(1.5, 2.5, 3.5);
	///   double[] xy = coords;        // {1.5, 2.5, 3.5}
	///   double mid = xy[1];
	///   </code>
	///   <para><b>资源与坑</b>：纯托管转换；NaN、±inf 原样进数组，喂给算子前自己甄别。</para>
	/// </remarks>
	public static implicit operator double[](JlTuple t)
	{
		return t.ToDArr();
	}

	/// <summary>把整条元组隐式转成 string[]：逐元素取文本。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：实现为 return t.ToSArr()，把<b>所有</b>元素转成 string[]。STRING 元组是内容拷贝；其他类型走 GetValue(i).ToString() 就近格式化，因此对数值元组它<b>不抛错</b>而给文本。</para>
	///   <para><b>约束或前提</b>：小数点符号跟随当前区域设置（de-DE 下变逗号），跨语言环境写文件要当心；空元组返回零长数组。</para>
	///   <para><b>与相邻算子的取舍</b>：与标量隐式 string 相反——那条对数值元组会抛，本法宽容格式化，要"永不抛地拿文本"用本法；只要调试可读性用整条 ToString() 更省事。HANDLE 元素取回的文本是对象默认 ToString，不含句柄数值语义保证 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple tags = new JlTuple("left", "right");
	///   string[] names = tags;           // {"left", "right"}
	///   string joined = names[0] + "|" + names[1];
	///   </code>
	///   <para><b>资源与坑</b>：纯托管转换；结果与元组脱钩，改数组不影响元组。</para>
	/// </remarks>
	public static implicit operator string[](JlTuple t)
	{
		return t.ToSArr();
	}

	/// <summary>把整条元组隐式转成 JlHandle[]：逐元素取回句柄副本。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：实现为 return t.ToHArr()，把<b>所有</b>元素转成 JlHandle[]。HANDLE 元组逐元素 new JlHandle(...) 克隆；MIXED 元组要求所有元素都是句柄。</para>
	///   <para><b>约束或前提</b>：数值/字符串元组抛 JlTupleAccessException("Cannot convert to handle array")；MIXED 里混进一个非句柄抛 "only allowed with handle types"；空元组返回零长数组。</para>
	///   <para><b>与相邻算子的取舍</b>：只是遍历取值不想管释放用 ToOArr（句柄元素同样要清）；要把对象交回原生继续用，直接传元组本体，别绕数组克隆一圈。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple group = new JlTuple(new JlHandle(), new JlHandle());
	///   JlHandle[] hs = group;           // 逐元素克隆
	///   foreach (JlHandle item in hs)
	///   {
	///       item.Dispose();              // 副本各自释放
	///   }
	///   group.Dispose();                 // 元组自身的原件另算
	///   </code>
	///   <para><b>资源与坑</b>：忘 Dispose 返回数组里的句柄就是句柄泄漏；本方法不释放元组内原件，group 仍按自己生命周期释放。</para>
	/// </remarks>
	public static implicit operator JlHandle[](JlTuple t)
	{
		return t.ToHArr();
	}

	/// <summary>把整条元组隐式转成 IntPtr[]：逐元素按平台指针宽取回。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：实现为 return t.ToIPArr()，把<b>所有</b>元素按当前指针宽度重建为 IntPtr[]。LONG 元组仅 64 位进程可用、INTEGER 仅 32 位进程可用。</para>
	///   <para><b>约束或前提</b>：整数宽度与平台指针不同宽时抛 JlTupleAccessException("Values in tuple do not represent pointers on this platform")；数值/字符串之外的存储类型同样抛；空元组返回零长数组；负数指针值原样保留。</para>
	///   <para><b>与相邻算子的取舍</b>：跨 32/64 位都要跑通的代码用 long[] 拿值再自行 new IntPtr，只有配合 JlTuple(IntPtr...) 构造进来的平台相关整数才该指望本法无损回来。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple addrs = new JlTuple(new IntPtr(0x1000), new IntPtr(0x2000));   // 64 位下为 LONG
	///   IntPtr[] ps = addrs;
	///   long addr0 = ps[0].ToInt64();
	///   </code>
	///   <para><b>资源与坑</b>：拿到的 IntPtr 只是数值拷贝，不持有内存所有权；32 位进程跑上面这段会因宽度不符抛异常。</para>
	/// </remarks>
	public static implicit operator IntPtr[](JlTuple t)
	{
		return t.ToIPArr();
	}

	/// <summary>把元素视图隐式物化成独立元组：按视图的 Type 分派存储类型。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：e.Type switch——INTEGER/LONG/DOUBLE/STRING/HANDLE 各用 e 的对应数组（IArr/LArr/DArr/SArr/JlArr）new 一个 JlTuple，MIXED 用 e.OArr，EMPTY 给空元组；把索引器产出的元素视图"落地"成一条独立元组。</para>
	///   <para><b>约束或前提</b>：Type 落在枚举覆盖之外（视图处于不一致态）抛 JlTupleAccessException("Inconsistent tuple state encountered")。</para>
	///   <para><b>与相邻算子的取舍</b>：与反向的 implicit operator JlTupleElements(JlTuple) 配对使用；只想把整条元组当标量用走隐式 int/double，不必经这条物化。视图是引用语义（经它写回会改到源元组），本转换则拷贝出独立数据。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple src = new JlTuple(10, 20, 30);
	///   JlTupleElements view = src[1];    // 索引器给出单元素视图（INTEGER）
	///   JlTuple item = view;              // 物化成独立元组：{20}
	///   int v = item;                     // 20
	///   </code>
	///   <para><b>资源与坑</b>：句柄视图物化后新元组接管句柄副本，需按 JlTuple.Dispose 释放；纯数值视图无资源负担。</para>
	/// </remarks>
	public static implicit operator JlTuple(JlTupleElements e)
	{
		return e.Type switch
		{
			JlTupleType.INTEGER => new JlTuple(e.IArr), 
			JlTupleType.LONG => new JlTuple(e.LArr), 
			JlTupleType.DOUBLE => new JlTuple(e.DArr), 
			JlTupleType.STRING => new JlTuple(e.SArr), 
			JlTupleType.JlANDLE => new JlTuple(e.JlArr), 
			JlTupleType.MIXED => new JlTuple(e.OArr), 
			JlTupleType.EMPTY => new JlTuple(), 
			_ => throw new JlTupleAccessException("Inconsistent tuple state encountered"), 
		};
	}

	/// <summary>把 int 隐式转成元组：建单元素 INTEGER 元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：return new JlTuple(i)，恒 32 位 INTEGER 存储、平台无关。让标量字面量能直接喂给接受 JlTuple 形参的算子。</para>
	///   <para><b>与相邻算子的取舍</b>：值可能超 32 位用 long 版（走 L 后缀或 long 变量），连续量用 double 版；宽度要随平台走用 IntPtr 版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple n = 42;               // 隐式 new JlTuple(42)：单元素 INTEGER
	///   int back = n;                 // 42
	///   </code>
	///   <para><b>资源与坑</b>：裸 int 字面量赋给 JlTuple 变量安全（int→JlTuple 走本法）；但若把 int 直接传进同时有 JlTuple 和 int 双重载的方法，会因双向隐式转换产生 CS0121 二义，需显式 new JlTuple(...) 消歧。</para>
	/// </remarks>
	public static implicit operator JlTuple(int i)
	{
		return new JlTuple(i);
	}

	/// <summary>把 int[] 隐式转成元组：建多元素 INTEGER 元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：return new JlTuple(i)，整条数组变 INTEGER 元组（Type 统一 INTEGER）。传参顺序即元素顺序。</para>
	///   <para><b>约束或前提</b>：底层是 copy:true——数组被拷贝一份，构造后改原数组不影响元组，反之亦然；元素超 32 位请在源头用 long[]，别硬塞 int[] 后指望补救。</para>
	///   <para><b>与相邻算子的取舍</b>：少量字面量直接 new JlTuple(10, 20, 30)（params）更省事；本转换适合数据已是 int[] 的场合。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   int[] a = new int[] { 10, 20, 30 };
	///   JlTuple t = a;                // 隐式转成 INTEGER 元组
	///   int n = t.Length;             // 3
	///   </code>
	///   <para><b>资源与坑</b>：copy:true 有 O(n) 拷贝开销；纯数值元组 Dispose 无实际意义。</para>
	/// </remarks>
	public static implicit operator JlTuple(int[] i)
	{
		return new JlTuple(i);
	}

	/// <summary>把 long 隐式转成元组：建单元素 LONG 元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：return new JlTuple(l)，64 位 LONG 存储。用于超过 int 范围的计数/时间戳单值。</para>
	///   <para><b>与相邻算子的取舍</b>：小整数用 int 版；宽度随平台走用 IntPtr 版；一组大整数用 long[] 版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple big = 5000000000L;     // 隐式 new JlTuple(long)：单元素 LONG
	///   long v = big;                 // 5000000000
	///   </code>
	///   <para><b>资源与坑</b>：字面量必须带 L 后缀才走本法，否则被 int 版捕获成 INTEGER（>int 范围的裸字面量会编译不过）；纯数值元组 Dispose 无意义。</para>
	/// </remarks>
	public static implicit operator JlTuple(long l)
	{
		return new JlTuple(l);
	}

	/// <summary>把 long[] 隐式转成元组：建多元素 LONG 元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：return new JlTuple(l)，整条数组变 64 位 LONG 元组，逐元素无损。</para>
	///   <para><b>约束或前提</b>：copy:true——数组被拷贝，构造后改原数组不影响元组；顺序即传参顺序。</para>
	///   <para><b>与相邻算子的取舍</b>：数据已是 int[] 且确定不超 32 位可用 int[] 版，但宽度会退回 32 位；需要保 64 位一律用本法或逐元素带 L 的 params 构造。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   long[] a = new long[] { 100L, 5000000000L };
	///   JlTuple t = a;                // 隐式转成 LONG 元组
	///   long first = t[0];            // 100
	///   </code>
	///   <para><b>资源与坑</b>：copy:true 有 O(n) 拷贝；纯数值元组 Dispose 无意义。</para>
	/// </remarks>
	public static implicit operator JlTuple(long[] l)
	{
		return new JlTuple(l);
	}

	/// <summary>把 double 隐式转成元组：建单元素 DOUBLE 元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：return new JlTuple(d)，DOUBLE 存储。坐标、角度、阈值等连续量单值直接喂给接受 JlTuple 的算子用它。</para>
	///   <para><b>与相邻算子的取舍</b>：索引类整数用 int/long 版，别为"看起来精确"用 double 存下标（按索引取用的路径会校验 DOUBLE 下标是否含小数并抛 JlTupleAccessException）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple angle = 1.5707963;     // 隐式 new JlTuple(double)：单元素 DOUBLE
	///   double rad = angle;            // 1.5707963
	///   </code>
	///   <para><b>资源与坑</b>：0.0/-0.0/NaN 均可存入，NaN 参与等值比较永不相等；纯数值元组 Dispose 无意义。</para>
	/// </remarks>
	public static implicit operator JlTuple(double d)
	{
		return new JlTuple(d);
	}

	/// <summary>把 double[] 隐式转成元组：建多元素 DOUBLE 元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：return new JlTuple(d)，整条数组变 DOUBLE 元组，是"点集/分布"批量入算子的主路径。</para>
	///   <para><b>约束或前提</b>：copy:true 有 O(n) 拷贝；空数组得到零长 DOUBLE 元组（Type 仍是 DOUBLE，与 EMPTY 不同）。</para>
	///   <para><b>与相邻算子的取舍</b>：少量字面量用 new JlTuple(0.5, 1.5)（params）；float[] 数据用 float 系构造更直白但存储同为 DOUBLE。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   double[] xs = { 0.5, 1.5, 2.5 };
	///   JlTuple t = xs;               // 隐式转成 DOUBLE 元组
	///   int n = t.Length;             // 3
	///   </code>
	///   <para><b>资源与坑</b>：NaN/±inf 会一并进入元组，喂算子前自行甄别；纯数值元组 Dispose 无意义。</para>
	/// </remarks>
	public static implicit operator JlTuple(double[] d)
	{
		return new JlTuple(d);
	}

	/// <summary>把 string 隐式转成元组：建单元素 STRING 元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：return new JlTuple(s)，STRING 存储。模型名、路径、标签这类文本单值直接入算子用它。</para>
	///   <para><b>与相邻算子的取舍</b>：多个路径/标签用 string[] 版一次建齐；数值别塞字符串元组——多数算术算子不认 STRING，ToDArr 取回也会抛访问异常。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple name = "logo_model";   // 隐式 new JlTuple(string)：单元素 STRING
	///   string s = name;               // "logo_model"
	///   </code>
	///   <para><b>资源与坑</b>：与标量隐式 string 互为逆运算，走 null 字符串的行为取决于实现 [待实测]；纯字符串元组 Dispose 无意义。</para>
	/// </remarks>
	public static implicit operator JlTuple(string s)
	{
		return new JlTuple(s);
	}

	/// <summary>把 string[] 隐式转成元组：建多元素 STRING 元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：return new JlTuple(s)，整条数组变 STRING 元组，TupleJoin/TupleStrReplace 等字符串算子的批量入口。</para>
	///   <para><b>约束或前提</b>：copy:true 拷贝引用数组（字符串本身不可变，无需深拷贝）；顺序即传参顺序，下游按序号取名依赖该顺序。</para>
	///   <para><b>与相邻算子的取舍</b>：混合数值与文本要用 object[] 构造（MIXED）；纯文本集合一律本法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   string[] files = new string[] { "a.png", "b.png" };
	///   JlTuple t = files;            // 隐式转成 STRING 元组
	///   string first = t[0];          // "a.png"
	///   </code>
	///   <para><b>资源与坑</b>：入参须声明成 string[]（而非 object[]/隐式数组字面量），否则 string[]→object[] 数组协变会让本法与 object[] 版争出 CS0121 二义；纯字符串元组 Dispose 无意义。</para>
	/// </remarks>
	public static implicit operator JlTuple(string[] s)
	{
		return new JlTuple(s);
	}

	/// <summary>把 JlHandle 隐式转成元组：建单元素 HANDLE 元组（存句柄克隆）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：return new JlTuple(h)，底层 JlTupleHandle 对入参执行 new JlHandle(h)——元组持有自己的句柄副本，原句柄归原主。</para>
	///   <para><b>与相邻算子的取舍</b>：多个句柄用 JlHandle[] 版；句柄与序号/分数混排才走 object[] 版（MIXED，但整条 TupleIsHandle 会退化为 0）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle h = new JlHandle();
	///   JlTuple one = h;             // 隐式转成 HANDLE 元组，内存的是克隆
	///   one.Dispose();               // 只释放元组内的克隆
	///   h.Dispose();                 // 原句柄由调用方自行释放
	///   </code>
	///   <para><b>资源与坑</b>：元组与原句柄是两个独立副本，各自 Dispose，别指望改 one 会动到 h；这正是 JlTuple 实现 IDisposable 的意义所在。</para>
	/// </remarks>
	public static implicit operator JlTuple(JlHandle h)
	{
		return new JlTuple(h);
	}

	/// <summary>把 JlHandle[] 隐式转成元组：建多元素 HANDLE 元组（逐元素克隆）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：return new JlTuple(h)，JlTupleHandle 对数组里每个句柄 new JlHandle(h[i])——顺序保持入参顺序，这条顺序就是下游 CountObj/按序号筛选所依赖的基准。</para>
	///   <para><b>约束或前提</b>：每个元素都是独立克隆，元组 Dispose 时逐个释放副本，原数组及其内容仍由创建方负责。</para>
	///   <para><b>与相邻算子的取舍</b>：单句柄用 JlHandle 版；句柄与数值混排走 object[] 版但那就不是 HANDLE 整型元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle[] arr = new JlHandle[] { new JlHandle(), new JlHandle() };
	///   JlTuple group = arr;         // 隐式转成 HANDLE 元组，逐个克隆
	///   int n = group.Length;        // 2
	///   group.Dispose();             // 释放组内两个克隆
	///   </code>
	///   <para><b>资源与坑</b>：入参须声明成 JlHandle[]（否则数组协变会让本法与 object[] 版争 CS0121）；中途重排原数组不会同步进元组，且已克隆顺序即定死。</para>
	/// </remarks>
	public static implicit operator JlTuple(JlHandle[] h)
	{
		return new JlTuple(h);
	}

	/// <summary>把 object[] 隐式转成元组：建 MIXED 型元组（逐元素保留类型）。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：return new JlTuple(o)，JlTupleMixed 逐元素保留各自类型（int、double、string、句柄可混排），整条元组 Type 为 MIXED。</para>
	///   <para><b>约束或前提</b>：copy:true 拷贝数组；MIXED 让 TupleIsHandle 之类整体判断退化为 0，要逐元素判断得换 Elem 族算子或按元素 is/switch 分派；超出支持范围的 object 如何处置 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：类型齐整的数据一律用对应单类型构造（int[]/double[]/string[]/JlHandle[]），省掉装箱与模式匹配；本法专为异构记录（一条 = 序号+标签+分数）而生。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   object[] items = new object[] { 7, "roi", 0.95 };
	///   JlTuple rec = items;             // MIXED 元组
	///   int idx = (int)rec[0];           // 按元素类型分别取回：7
	///   string tag = (string)rec[1];     // "roi"
	///   </code>
	///   <para><b>资源与坑</b>：若混入句柄元素，Dispose 元组会连带释放这些句柄副本（装箱成 object 不等于不用释放）。</para>
	/// </remarks>
	public static implicit operator JlTuple(object[] o)
	{
		return new JlTuple(o);
	}

	/// <summary>把 IntPtr 隐式转成元组：建平台相关宽度的单元素整数元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：return new JlTuple(ip)，底层 JlTuple(IntPtr[])：64 位进程存成 LONG、32 位进程存成 INTEGER——"平台相关整数"的来源，bool 构造也走这条路。</para>
	///   <para><b>约束或前提</b>：元组只搬运裸整数值，不持有也不释放该地址指向的内存；把句柄当真指针塞进来是常见误用，句柄请用 JlHandle 版。</para>
	///   <para><b>与相邻算子的取舍</b>：普通小整数计数用 int/long 版避免结果宽度随 32/64 位构建漂移；只有要与原生 intptr 同宽的地址/指针数值才该用本法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple addr = new IntPtr(0x1000);   // 64 位下为 LONG
	///   long v = addr;                       // 隐式转 long 读回指针数值
	///   </code>
	///   <para><b>资源与坑</b>：本法产出的元组宽度依赖运行位数，跨 32/64 位序列化/比较时要意识到同一 IntPtr 值在两种构建下 Type 不同。</para>
	/// </remarks>
	public static implicit operator JlTuple(IntPtr ip)
	{
		return new JlTuple(ip);
	}

	/// <summary>把 IntPtr[] 隐式转成元组：建平台相关宽度的整数元组。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>：return new JlTuple(ip)，逐元素转平台宽度整数——64 位进程转 long 存 LONG 元组，32 位进程转 int 存 INTEGER 元组；转换在构造循环里完成。</para>
	///   <para><b>约束或前提</b>：32 位进程塞 64 位指针值会被截断（构造不做越界检查）；传入数组本身不拷贝，但内部 new 了目标宽度数组逐元素转换，故改动入参 IntPtr[] 不影响已建元组。</para>
	///   <para><b>与相邻算子的取舍</b>：JlHandle[] 版才是句柄集合的正确载体（带克隆/释放语义）；本法只搬运裸整数值，不接管指向内存的所有权。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   IntPtr[] ps = new IntPtr[] { new IntPtr(0x100), new IntPtr(0x200) };
	///   JlTuple addrs = ps;            // 平台宽整数元组
	///   int n = addrs.Length;          // 2
	///   </code>
	///   <para><b>资源与坑</b>：产出元组的 Type 随运行位数在 LONG/INTEGER 间漂移；与 ToIPArr 配对时宽度不符会在 32 位进程抛指针宽异常。</para>
	/// </remarks>
	public static implicit operator JlTuple(IntPtr[] ip)
	{
		return new JlTuple(ip);
	}

	internal void Store(IntPtr proc, int parIndex)
	{
		data.Store(proc, parIndex);
	}

	internal int Load(IntPtr proc, int parIndex, JlTupleType type, int err)
	{
		if (JlNativeApi.IsFailure(err))
		{
			data = JlTupleVoid.EMPTY;
			return err;
		}
		return JlTupleImplementation.Load(proc, parIndex, type, out data);
	}

	internal int Load(IntPtr proc, int parIndex, int err)
	{
		return Load(proc, parIndex, JlTupleType.MIXED, err);
	}

	/// <summary>建一个全新 JlTuple 并把原生调用的 parIndex 输出按指定类型装载进去，返回透传的错误码（"新句柄"装载入口）。</summary>
	/// <param name="proc">PreCall 取得的算子上下文指针。</param>
	/// <param name="parIndex">输出参数在原生侧的序号。</param>
	/// <param name="type">装载口径（INTEGER/LONG/DOUBLE/STRING/MIXED 等），决定新建实例的内部存储类型。</param>
	/// <param name="err">前序调用返回的错误码；失败时不装载，出参为空元组（EMPTY）并原样返回 err。</param>
	/// <param name="tuple">out 出参：装载了输出的新 JlTuple，用完须 Dispose（纯数值时无原生句柄可漏）。</param>
	/// <returns>装载后的错误码，供继续链式装载同一 proc 的其余输出。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>tuple = new JlTuple(); return tuple.Load(proc, parIndex, type, err)</c>
	///   ——先建空实例再原地装载，产出的是新句柄；与写进已有实例的实例方法 <c>Load</c>（旧存储被替换）相对。</para>
	///   <para><b>约束或前提</b>标 <c>EditorBrowsable(Never)</c> 的互操作入口，供本库算子包装方法在
	///   <c>CallProcedure</c> 之后取回 InitOCT 声明的输出；业务代码应调包装方法而非直接触本函数。
	///   type 与原生侧实际类型不符时的截断行为 [待实测]。</para>
	///   <para><b>用法</b>（手写一次 id 138 tuple_replace 调用并取回输出）</para>
	///   <code>
	///   IntPtr proc = JlNativeApi.PreCall(138);
	///   JlNativeApi.Store(proc, 0, new int[] { 1, 2, 3 });
	///   JlNativeApi.Store(proc, 1, new int[] { 1 });
	///   JlNativeApi.Store(proc, 2, new int[] { 9 });
	///   JlNativeApi.InitOCT(proc, 0);
	///   int err = JlNativeApi.CallProcedure(proc);
	///   err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out JlTuple result);
	///   JlNativeApi.PostCall(proc, err);
	///   result.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>err 失败时出参是 EMPTY 元组（无句柄可漏）；成功时返回新元组，
	///   句柄型元素须由 <c>Dispose</c> 释放。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadNew(IntPtr proc, int parIndex, JlTupleType type, int err, out JlTuple tuple)
	{
		tuple = new JlTuple();
		return tuple.Load(proc, parIndex, type, err);
	}

	/// <summary>LoadNew 的免类型重载：新建 JlTuple 并按 MIXED 口径装载 parIndex 输出，返回透传的错误码。</summary>
	/// <param name="proc">PreCall 取得的算子上下文指针。</param>
	/// <param name="parIndex">输出参数在原生侧的序号。</param>
	/// <param name="err">前序调用返回的错误码；失败时不装载，出参为空元组并原样返回 err。</param>
	/// <param name="tuple">out 出参：装载了输出的新 JlTuple，用完须 Dispose。</param>
	/// <returns>装载后的错误码，供继续链式装载。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>等价于 <c>tuple = new JlTuple(); return tuple.Load(proc, parIndex, JlTupleType.MIXED, err)</c>
	///   ——与 <see cref="LoadNew(IntPtr, int, JlTupleType, int, out JlTuple)"/> 同一内部路径，只是装载口径
	///   固定 MIXED，产出新句柄。</para>
	///   <para><b>约束或前提</b>同样是 <c>EditorBrowsable(Never)</c> 互操作入口；输出类型已知且单一时
	///   用带 type 的重载可省 MIXED 装箱路径。失败时出参为 EMPTY 元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   IntPtr proc = JlNativeApi.PreCall(138);
	///   JlNativeApi.Store(proc, 0, new int[] { 1, 2, 3 });
	///   JlNativeApi.Store(proc, 1, new int[] { 0 });
	///   JlNativeApi.Store(proc, 2, new int[] { 9 });
	///   JlNativeApi.InitOCT(proc, 0);
	///   int err = JlNativeApi.CallProcedure(proc);
	///   err = JlTuple.LoadNew(proc, 0, err, out JlTuple result);   // 按 MIXED 口径取输出
	///   JlNativeApi.PostCall(proc, err);
	///   result.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>MIXED 口径下数值元素被装箱存取，批量取数改走 <c>IArr</c>/<c>DArr</c>
	///   前要先看 <see cref="Type"/>。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadNew(IntPtr proc, int parIndex, int err, out JlTuple tuple)
	{
		tuple = new JlTuple();
		return tuple.Load(proc, parIndex, JlTupleType.MIXED, err);
	}

	/// <summary>把元组全部元素按序拼成调试用字符串快照（多元素加方括号、逗号分隔，字符串元素加双引号），不改变元组内容。</summary>
	/// <returns>元组元素的字符串快照：多元素用方括号包裹、以逗号分隔，字符串元素加双引号，单元素不加括号。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：把元组内所有元素按序拼成一段可读文本，主要供调试打印用，不参与任何数值/集合运算，也不是持久化格式。</para>
	///   <para><b>排版规则</b>：元素数 &gt; 1 时整体外加 <c>[ ]</c>、元素间以 <c>, </c> 分隔；元素数恰好为 1 时不加方括号只输出该值本身；类型为 <c>JlTupleType.STRING</c> 的元素会额外套双引号 <c>"..."</c>，其余类型（整数/浮点/句柄等）直接调元素的 <c>ToString()</c>。</para>
	///   <para><b>资源与坑</b>：内部经 <c>ToOArr()</c> 取出 object 数组，遍历后会对其中实现了 <c>IDisposable</c> 的元素（句柄类）逐个 <c>Dispose()</c>；本方法只读取当前元组，不修改其内容，返回的是普通 <c>string</c>，无需释放。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple nums = new JlTuple(1, 2, 3);
	///   Console.WriteLine(nums.ToString());   // [1, 2, 3]
	///   JlTuple strs = new JlTuple("a", "b");
	///   Console.WriteLine(strs.ToString());   // ["a", "b"]
	///   JlTuple one = new JlTuple(7);
	///   Console.WriteLine(one.ToString());    // 7（单元素无方括号）
	///   </code>
	/// </remarks>
	public override string ToString()
	{
		object[] array = ToOArr();
		string text = "";
		if (Length != 1)
		{
			text += "[";
		}
		for (int i = 0; i < array.Length; i++)
		{
			text += ((i > 0) ? ", " : "");
			text = ((this[i].Type != JlTupleType.STRING) ? (text + array[i].ToString()) : (text + "\"" + array[i].ToString() + "\""));
			if (array[i] is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}
		if (Length != 1)
		{
			text += "]";
		}
		return text;
	}

	/// <summary>两元组逐元素相加。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1.TupleAdd(t2)</c>，按"对等元组逐元素加、单元素广播到全元组"的规则做算术加。</para>
	///   <para><b>资源与坑</b>：两元组长度不等时的对齐行为由底层原生算子决定（内部 <c>ResultSize.EQUAL</c>），
	///   若对等逐元语义，请先确保两元组等长或一方为单元素广播。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1, 2);
	///   JlTuple b = new JlTuple(10, 20);
	///   JlTuple c = a + b;     // c == (11, 22)
	///   </code>
	/// </remarks>
	public static JlTuple operator +(JlTuple t1, JlTuple t2)
	{
		return t1.TupleAdd(t2);
	}

	/// <summary>元组逐元素加上 int 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 + (JlTuple)t2</c>，标量广播到元组的每个元素做算术加。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1, 2);
	///   JlTuple c = a + 3;     // c == (4, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator +(JlTuple t1, int t2)
	{
		return t1 + (JlTuple)t2;
	}

	/// <summary>元组逐元素加上 long 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 + (JlTuple)t2</c>，标量广播到元组的每个元素做算术加。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1L, 2L);
	///   JlTuple c = a + 3L;     // c == (4, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator +(JlTuple t1, long t2)
	{
		return t1 + (JlTuple)t2;
	}

	/// <summary>元组逐元素加上 float 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 + (JlTuple)t2</c>，标量广播到元组的每个元素做算术加。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1.5f, 2.5f);
	///   JlTuple c = a + 0.5f;   // c == (2.0, 3.0)
	///   </code>
	/// </remarks>
	public static JlTuple operator +(JlTuple t1, float t2)
	{
		return t1 + (JlTuple)t2;
	}

	/// <summary>元组逐元素加上 double 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 + (JlTuple)t2</c>，标量广播到元组的每个元素做算术加。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1.5, 2.5);
	///   JlTuple c = a + 0.5;     // c == (2.0, 3.0)
	///   </code>
	/// </remarks>
	public static JlTuple operator +(JlTuple t1, double t2)
	{
		return t1 + (JlTuple)t2;
	}

	/// <summary>元组逐元素加上字符串所表示的数值标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 + (JlTuple)t2</c>，标量广播到元组的每个元素做算术加。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(4, 9);
	///   JlTuple c = a + "2";    // c == (6, 11)
	///   </code>
	/// </remarks>
	public static JlTuple operator +(JlTuple t1, string t2)
	{
		return t1 + (JlTuple)t2;
	}

	/// <summary>元组逐元素加上 <see cref="JlTupleElements"/> 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 + (JlTuple)t2</c>，标量元素广播到元组的每个元素做算术加。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1, 2);
	///   JlTuple c = a + new JlTuple(5)[0];   // a + 标量5 -> c == (6, 7)
	///   </code>
	/// </remarks>
	public static JlTuple operator +(JlTuple t1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)t2;
		return t1 + hTuple;
	}

	/// <summary>两元组逐元素相减。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1.TupleSub(t2)</c>，按"对等元组逐元素减、单元素广播到全元组"的规则做算术减。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(7, 3);
	///   JlTuple b = new JlTuple(2, 1);
	///   JlTuple c = a - b;   // c == (5, 2)
	///   </code>
	/// </remarks>
	public static JlTuple operator -(JlTuple t1, JlTuple t2)
	{
		return t1.TupleSub(t2);
	}

	/// <summary>元组逐元素减去 int 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 - (JlTuple)t2</c>，标量广播到元组的每个元素做算术减。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(10, 20);
	///   JlTuple c = a - 3;    // c == (7, 17)
	///   </code>
	/// </remarks>
	public static JlTuple operator -(JlTuple t1, int t2)
	{
		return t1 - (JlTuple)t2;
	}

	/// <summary>元组逐元素减去 long 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 - (JlTuple)t2</c>，标量广播到元组的每个元素做算术减。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(10L, 20L);
	///   JlTuple c = a - 5L;    // c == (5, 15)
	///   </code>
	/// </remarks>
	public static JlTuple operator -(JlTuple t1, long t2)
	{
		return t1 - (JlTuple)t2;
	}

	/// <summary>元组逐元素减去 float 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 - (JlTuple)t2</c>，标量广播到元组的每个元素做算术减。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1.5f, 3.5f);
	///   JlTuple c = a - 0.5f;   // c == (1.0, 3.0)
	///   </code>
	/// </remarks>
	public static JlTuple operator -(JlTuple t1, float t2)
	{
		return t1 - (JlTuple)t2;
	}

	/// <summary>元组逐元素减去 double 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 - (JlTuple)t2</c>，标量广播到元组的每个元素做算术减。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(5.0, 10.0);
	///   JlTuple c = a - 2.0;    // c == (3.0, 8.0)
	///   </code>
	/// </remarks>
	public static JlTuple operator -(JlTuple t1, double t2)
	{
		return t1 - (JlTuple)t2;
	}

	/// <summary>元组逐元素减去字符串所表示的数值标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 - (JlTuple)t2</c>，标量广播到元组的每个元素做算术减。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(9, 4);
	///   JlTuple c = a - "2";     // c == (7, 2)
	///   </code>
	/// </remarks>
	public static JlTuple operator -(JlTuple t1, string t2)
	{
		return t1 - (JlTuple)t2;
	}

	/// <summary>元组逐元素减去 <see cref="JlTupleElements"/> 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 - (JlTuple)t2</c>，标量元素广播到元组的每个元素做算术减。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(8, 6);
	///   JlTuple c = a - new JlTuple(1)[0];   // a - 标量1 -> c == (7, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator -(JlTuple t1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)t2;
		return t1 - hTuple;
	}

	/// <summary>两元组逐元素相乘。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1.TupleMult(t2)</c>，按"对等元组逐元素乘、单元素广播到全元组"的规则做算术乘。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2, 3);
	///   JlTuple b = new JlTuple(4, 5);
	///   JlTuple c = a * b;     // c == (8, 15)
	///   </code>
	/// </remarks>
	public static JlTuple operator *(JlTuple t1, JlTuple t2)
	{
		return t1.TupleMult(t2);
	}

	/// <summary>元组逐元素乘以 int 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 * (JlTuple)t2</c>，标量广播到元组的每个元素做算术乘。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2, 3);
	///   JlTuple c = a * 3;     // c == (6, 9)
	///   </code>
	/// </remarks>
	public static JlTuple operator *(JlTuple t1, int t2)
	{
		return t1 * (JlTuple)t2;
	}

	/// <summary>元组逐元素乘以 long 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 * (JlTuple)t2</c>，标量广播到元组的每个元素做算术乘。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2L, 3L);
	///   JlTuple c = a * 3L;     // c == (6, 9)
	///   </code>
	/// </remarks>
	public static JlTuple operator *(JlTuple t1, long t2)
	{
		return t1 * (JlTuple)t2;
	}

	/// <summary>元组逐元素乘以 float 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 * (JlTuple)t2</c>，标量广播到元组的每个元素做算术乘。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1.5f, 2.0f);
	///   JlTuple c = a * 2.0f;   // c == (3.0, 4.0)
	///   </code>
	/// </remarks>
	public static JlTuple operator *(JlTuple t1, float t2)
	{
		return t1 * (JlTuple)t2;
	}

	/// <summary>元组逐元素乘以 double 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 * (JlTuple)t2</c>，标量广播到元组的每个元素做算术乘。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1.5, 2.0);
	///   JlTuple c = a * 2.0;    // c == (3.0, 4.0)
	///   </code>
	/// </remarks>
	public static JlTuple operator *(JlTuple t1, double t2)
	{
		return t1 * (JlTuple)t2;
	}

	/// <summary>元组逐元素乘以字符串所表示的数值标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 * (JlTuple)t2</c>，标量广播到元组的每个元素做算术乘。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2, 3);
	///   JlTuple c = a * "2";    // c == (4, 6)
	///   </code>
	/// </remarks>
	public static JlTuple operator *(JlTuple t1, string t2)
	{
		return t1 * (JlTuple)t2;
	}

	/// <summary>元组逐元素乘以 <see cref="JlTupleElements"/> 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 * (JlTuple)t2</c>，标量元素广播到元组的每个元素做算术乘。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2, 3);
	///   JlTuple c = a * new JlTuple(4)[0];   // a * 标量4 -> c == (8, 12)
	///   </code>
	/// </remarks>
	public static JlTuple operator *(JlTuple t1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)t2;
		return t1 * hTuple;
	}

	/// <summary>两元组逐元素相除。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1.TupleDiv(t2)</c>，按"对等元组逐元素除、单元素广播到全元组"的规则做算术除。</para>
	///   <para><b>资源与坑</b>：除数元素为 0（或字符串换算为 0）时底层除零行为由原生算子决定。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(8, 12);
	///   JlTuple b = new JlTuple(2, 3);
	///   JlTuple c = a / b;     // c == (4.0, 4.0)
	///   </code>
	/// </remarks>
	public static JlTuple operator /(JlTuple t1, JlTuple t2)
	{
		return t1.TupleDiv(t2);
	}

	/// <summary>元组逐元素除以 int 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 / (JlTuple)t2</c>，标量广播到元组的每个元素做算术除。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(8, 12);
	///   JlTuple c = a / 4;      // c == (2.0, 3.0)
	///   </code>
	/// </remarks>
	public static JlTuple operator /(JlTuple t1, int t2)
	{
		return t1 / (JlTuple)t2;
	}

	/// <summary>元组逐元素除以 long 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 / (JlTuple)t2</c>，标量广播到元组的每个元素做算术除。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(8L, 12L);
	///   JlTuple c = a / 4L;      // c == (2.0, 3.0)
	///   </code>
	/// </remarks>
	public static JlTuple operator /(JlTuple t1, long t2)
	{
		return t1 / (JlTuple)t2;
	}

	/// <summary>元组逐元素除以 float 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 / (JlTuple)t2</c>，标量广播到元组的每个元素做算术除。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1.5f, 3.0f);
	///   JlTuple c = a / 0.5f;   // c == (3.0, 6.0)
	///   </code>
	/// </remarks>
	public static JlTuple operator /(JlTuple t1, float t2)
	{
		return t1 / (JlTuple)t2;
	}

	/// <summary>元组逐元素除以 double 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 / (JlTuple)t2</c>，标量广播到元组的每个元素做算术除。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1.5, 3.0);
	///   JlTuple c = a / 0.5;    // c == (3.0, 6.0)
	///   </code>
	/// </remarks>
	public static JlTuple operator /(JlTuple t1, double t2)
	{
		return t1 / (JlTuple)t2;
	}

	/// <summary>元组逐元素除以字符串所表示的数值标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 / (JlTuple)t2</c>，标量广播到元组的每个元素做算术除。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(8, 12);
	///   JlTuple c = a / "4";    // c == (2.0, 3.0)
	///   </code>
	/// </remarks>
	public static JlTuple operator /(JlTuple t1, string t2)
	{
		return t1 / (JlTuple)t2;
	}

	/// <summary>元组逐元素除以 <see cref="JlTupleElements"/> 标量。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 / (JlTuple)t2</c>，标量元素广播到元组的每个元素做算术除。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(8, 12);
	///   JlTuple c = a / new JlTuple(4)[0];   // a / 标量4 -> c == (2.0, 3.0)
	///   </code>
	/// </remarks>
	public static JlTuple operator /(JlTuple t1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)t2;
		return t1 / hTuple;
	}

	/// <summary>两元组逐元素取模（求余）。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1.TupleMod(t2)</c>，按"对等元组逐元素求余、单元素广播到全元组"的规则计算。</para>
	///   <para><b>资源与坑</b>：通常仅对整数元素有意义；除数元素为 0 时的结果与底层原生算子一致。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(10, 7);
	///   JlTuple b = new JlTuple(3, 4);
	///   JlTuple c = a % b;     // c == (1, 3)
	///   </code>
	/// </remarks>
	public static JlTuple operator %(JlTuple t1, JlTuple t2)
	{
		return t1.TupleMod(t2);
	}

	/// <summary>元组逐元素对 int 标量取模（求余）。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 % (JlTuple)t2</c>，标量广播到元组的每个元素。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(10, 7);
	///   JlTuple c = a % 3;      // c == (1, 1)
	///   </code>
	/// </remarks>
	public static JlTuple operator %(JlTuple t1, int t2)
	{
		return t1 % (JlTuple)t2;
	}

	/// <summary>元组逐元素对 long 标量取模（求余）。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 % (JlTuple)t2</c>，标量广播到元组的每个元素。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(10L, 7L);
	///   JlTuple c = a % 3L;      // c == (1, 1)
	///   </code>
	/// </remarks>
	public static JlTuple operator %(JlTuple t1, long t2)
	{
		return t1 % (JlTuple)t2;
	}

	/// <summary>元组逐元素对 float 标量取模（求余）。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 % (JlTuple)t2</c>，标量广播到元组的每个元素。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(5.5f, 3.5f);
	///   JlTuple c = a % 1.0f;    // c == (0.5, 0.5)
	///   </code>
	/// </remarks>
	public static JlTuple operator %(JlTuple t1, float t2)
	{
		return t1 % (JlTuple)t2;
	}

	/// <summary>元组逐元素对 double 标量取模（求余）。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 % (JlTuple)t2</c>，标量广播到元组的每个元素。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(5.5, 3.5);
	///   JlTuple c = a % 1.0;    // c == (0.5, 0.5)
	///   </code>
	/// </remarks>
	public static JlTuple operator %(JlTuple t1, double t2)
	{
		return t1 % (JlTuple)t2;
	}

	/// <summary>元组逐元素对字符串表示的数值标量取模（求余）。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 % (JlTuple)t2</c>，标量广播到元组的每个元素。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(10, 7);
	///   JlTuple c = a % "3";    // c == (1, 1)
	///   </code>
	/// </remarks>
	public static JlTuple operator %(JlTuple t1, string t2)
	{
		return t1 % (JlTuple)t2;
	}

	/// <summary>元组逐元素对 <see cref="JlTupleElements"/> 标量取模（求余）。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 % (JlTuple)t2</c>，标量元素广播到元组的每个元素。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(10, 7);
	///   JlTuple c = a % new JlTuple(3)[0];   // a % 标量3 -> c == (1, 1)
	///   </code>
	/// </remarks>
	public static JlTuple operator %(JlTuple t1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)t2;
		return t1 % hTuple;
	}

	/// <summary>两元组逐元素按位与（AND）。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1.TupleAnd(t2)</c>，按"对等下标逐元素与、单元素广播到全元组"的规则做按位与，结果取整型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(12, 6);
	///   JlTuple b = new JlTuple(10, 3);
	///   JlTuple c = a &amp; b;   // c == (8, 2)
	///   </code>
	/// </remarks>
	public static JlTuple operator &(JlTuple t1, JlTuple t2)
	{
		return t1.TupleAnd(t2);
	}

	/// <summary>元组逐元素与 int 标量做按位与。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &amp; (JlTuple)t2</c>，标量广播到元组的每个元素做按位与，结果取整型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(7, 6);
	///   JlTuple c = a &amp; 5;    // c == (5, 4)
	///   </code>
	/// </remarks>
	public static JlTuple operator &(JlTuple t1, int t2)
	{
		return t1 & (JlTuple)t2;
	}

	/// <summary>元组逐元素与 long 标量做按位与。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &amp; (JlTuple)t2</c>，标量广播到元组的每个元素做按位与，结果取整型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(7L, 6L);
	///   JlTuple c = a &amp; 5L;    // c == (5, 4)
	///   </code>
	/// </remarks>
	public static JlTuple operator &(JlTuple t1, long t2)
	{
		return t1 & (JlTuple)t2;
	}

	/// <summary>元组逐元素与 float 标量做按位与。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &amp; (JlTuple)t2</c>，标量（按数值转为整型后）广播到元组的每个元素做按位与。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(7.0f, 6.0f);
	///   JlTuple c = a &amp; 5.0f;   // c == (5, 4)
	///   </code>
	/// </remarks>
	public static JlTuple operator &(JlTuple t1, float t2)
	{
		return t1 & (JlTuple)t2;
	}

	/// <summary>元组逐元素与 double 标量做按位与。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &amp; (JlTuple)t2</c>，标量（按数值转为整型后）广播到元组的每个元素做按位与。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(7.0, 6.0);
	///   JlTuple c = a &amp; 5.0;    // c == (5, 4)
	///   </code>
	/// </remarks>
	public static JlTuple operator &(JlTuple t1, double t2)
	{
		return t1 & (JlTuple)t2;
	}

	/// <summary>元组逐元素与字符串所表示数值标量做按位与。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &amp; (JlTuple)t2</c>，字符串按数值转换成标量后广播到元组的每个元素做按位与。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(7, 6);
	///   JlTuple c = a &amp; "5";    // c == (5, 4)
	///   </code>
	/// </remarks>
	public static JlTuple operator &(JlTuple t1, string t2)
	{
		return t1 & (JlTuple)t2;
	}

	/// <summary>元组逐元素与 <see cref="JlTupleElements"/> 标量做按位与。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &amp; (JlTuple)t2</c>，标量元素广播到元组的每个元素做按位与。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(7, 6);
	///   JlTuple c = a &amp; new JlTuple(5)[0];   // a &amp; 标量5 -> c == (5, 4)
	///   </code>
	/// </remarks>
	public static JlTuple operator &(JlTuple t1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)t2;
		return t1 & hTuple;
	}

	/// <summary>两元组逐元素按位或（OR）。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1.TupleOr(t2)</c>，按"对等下标逐元素或、单元素广播到全元组"的规则做按位或，结果取整型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2, 1);
	///   JlTuple b = new JlTuple(8, 4);
	///   JlTuple c = a | b;    // c == (10, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator |(JlTuple t1, JlTuple t2)
	{
		return t1.TupleOr(t2);
	}

	/// <summary>元组逐元素与 int 标量做按位或。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 | (JlTuple)t2</c>，标量广播到元组的每个元素做按位或，结果取整型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2, 1);
	///   JlTuple c = a | 5;     // c == (7, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator |(JlTuple t1, int t2)
	{
		return t1 | (JlTuple)t2;
	}

	/// <summary>元组逐元素与 long 标量做按位或。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 | (JlTuple)t2</c>，标量广播到元组的每个元素做按位或，结果取整型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2L, 1L);
	///   JlTuple c = a | 5L;     // c == (7, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator |(JlTuple t1, long t2)
	{
		return t1 | (JlTuple)t2;
	}

	/// <summary>元组逐元素与 float 标量做按位或。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 | (JlTuple)t2</c>，标量（按数值转为整型后）广播到元组的每个元素做按位或。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2.0f, 1.0f);
	///   JlTuple c = a | 5.0f;    // c == (7, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator |(JlTuple t1, float t2)
	{
		return t1 | (JlTuple)t2;
	}

	/// <summary>元组逐元素与 double 标量做按位或。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 | (JlTuple)t2</c>，标量（按数值转为整型后）广播到元组的每个元素做按位或。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2.0, 1.0);
	///   JlTuple c = a | 5.0;     // c == (7, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator |(JlTuple t1, double t2)
	{
		return t1 | (JlTuple)t2;
	}

	/// <summary>元组逐元素与字符串所表示数值标量做按位或。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 | (JlTuple)t2</c>，字符串按数值转换成标量后广播到元组的每个元素做按位或。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2, 1);
	///   JlTuple c = a | "5";     // c == (7, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator |(JlTuple t1, string t2)
	{
		return t1 | (JlTuple)t2;
	}

	/// <summary>元组逐元素与 <see cref="JlTupleElements"/> 标量做按位或。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 | (JlTuple)t2</c>，标量元素广播到元组的每个元素做按位或。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2, 1);
	///   JlTuple c = a | new JlTuple(5)[0];   // a | 标量5 -> c == (7, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator |(JlTuple t1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)t2;
		return t1 | hTuple;
	}

	/// <summary>两元组逐元素按位异或（XOR）。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1.TupleXor(t2)</c>，按"对等下标逐元素异或、单元素广播到全元组"的规则做按位异或，结果取整型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(6, 12);
	///   JlTuple b = new JlTuple(3, 10);
	///   JlTuple c = a ^ b;    // c == (5, 6)
	///   </code>
	/// </remarks>
	public static JlTuple operator ^(JlTuple t1, JlTuple t2)
	{
		return t1.TupleXor(t2);
	}

	/// <summary>元组逐元素与 int 标量做按位异或。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 ^ (JlTuple)t2</c>，标量广播到元组的每个元素做按位异或，结果取整型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(6, 4);
	///   JlTuple c = a ^ 1;     // c == (7, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator ^(JlTuple t1, int t2)
	{
		return t1 ^ (JlTuple)t2;
	}

	/// <summary>元组逐元素与 long 标量做按位异或。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 ^ (JlTuple)t2</c>，标量广播到元组的每个元素做按位异或，结果取整型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(6L, 4L);
	///   JlTuple c = a ^ 1L;     // c == (7, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator ^(JlTuple t1, long t2)
	{
		return t1 ^ (JlTuple)t2;
	}

	/// <summary>元组逐元素与 float 标量做按位异或。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 ^ (JlTuple)t2</c>，标量（按数值转为整型后）广播到元组的每个元素做按位异或。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(6.0f, 4.0f);
	///   JlTuple c = a ^ 1.0f;   // c == (7, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator ^(JlTuple t1, float t2)
	{
		return t1 ^ (JlTuple)t2;
	}

	/// <summary>元组逐元素与 double 标量做按位异或。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 ^ (JlTuple)t2</c>，标量（按数值转为整型后）广播到元组的每个元素做按位异或。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(6.0, 4.0);
	///   JlTuple c = a ^ 1.0;    // c == (7, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator ^(JlTuple t1, double t2)
	{
		return t1 ^ (JlTuple)t2;
	}

	/// <summary>元组逐元素与字符串所表示数值标量做按位异或。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 ^ (JlTuple)t2</c>，字符串按数值转换成标量后广播到元组的每个元素做按位异或。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(6, 4);
	///   JlTuple c = a ^ "1";     // c == (7, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator ^(JlTuple t1, string t2)
	{
		return t1 ^ (JlTuple)t2;
	}

	/// <summary>元组逐元素与 <see cref="JlTupleElements"/> 标量做按位异或。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 ^ (JlTuple)t2</c>，标量元素广播到元组的每个元素做按位异或。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(6, 4);
	///   JlTuple c = a ^ new JlTuple(1)[0];   // a ^ 标量1 -> c == (7, 5)
	///   </code>
	/// </remarks>
	public static JlTuple operator ^(JlTuple t1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)t2;
		return t1 ^ hTuple;
	}

	/// <summary>判断元组在布尔上下文中是否为假。</summary>
	/// <remarks>
	///   <para>取元组首元素转换为 bool 后取反（等价于 <c>!(bool)t</c>）。与 <see cref="operator true(JlTuple)"/> 一同供 if/while/&amp;&amp;/||/! 等布尔上下文求值使用。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(0);
	///   bool flag = !a;    // 首元素为 0 -> flag 为 true
	///   </code>
	/// </remarks>
	public static bool operator false(JlTuple t)
	{
		return !t;
	}

	/// <summary>判断元组在布尔上下文中是否为真。</summary>
	/// <remarks>
	///   <para>取元组首元素转换为 bool 返回（等价于 <c>(bool)t</c>）。与 <see cref="operator false(JlTuple)"/> 一同供 if/while/&amp;&amp;/||/! 等布尔上下文求值使用。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(3);
	///   if (a) { /* 首元素非零，视为真 */ }
	///   </code>
	/// </remarks>
	public static bool operator true(JlTuple t)
	{
		return t;
	}

	/// <summary>元组逐元素按位左移指定位数。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1.TupleLsh(shift)</c>，对每个元素做左移位，结果取整型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1, 3);
	///   JlTuple c = a &lt;&lt; 2;    // c == (4, 12)
	///   </code>
	/// </remarks>
	public static JlTuple operator <<(JlTuple t1, int shift)
	{
		return t1.TupleLsh(shift);
	}

	/// <summary>元组逐元素按位右移指定位数。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1.TupleRsh(shift)</c>，对每个元素做右移位，结果取整型。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(8, 16);
	///   JlTuple c = a &gt;&gt; 2;    // c == (2, 4)
	///   </code>
	/// </remarks>
	public static JlTuple operator >>(JlTuple t1, int shift)
	{
		return t1.TupleRsh(shift);
	}

	/// <summary>比较两元组，判断其是否小于对方。</summary>
	/// <remarks>
	///   <para>等价于 <c>(int)t1.TupleLess(t2) != 0</c>，对两元组逐元素做小于比较，取结果首元素判断真值；单元素广播规则同样适用。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1, 5);
	///   JlTuple b = new JlTuple(3, 2);
	///   bool c = a &lt; b;        // 首元素 1 &lt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <(JlTuple t1, JlTuple t2)
	{
		return (int)t1.TupleLess(t2) != 0;
	}

	/// <summary>比较元组首元素与 int 标量，判断是否小于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &lt; (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1, 5);
	///   bool c = a &lt; 3;         // 首元素 1 &lt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <(JlTuple t1, int t2)
	{
		return t1 < (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 long 标量，判断是否小于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &lt; (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1L, 5L);
	///   bool c = a &lt; 3L;        // 首元素 1 &lt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <(JlTuple t1, long t2)
	{
		return t1 < (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 float 标量，判断是否小于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &lt; (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1.5f, 5.5f);
	///   bool c = a &lt; 3.0f;      // 首元素 1.5 &lt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <(JlTuple t1, float t2)
	{
		return t1 < (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 double 标量，判断是否小于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &lt; (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1.5, 5.5);
	///   bool c = a &lt; 3.0;       // 首元素 1.5 &lt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <(JlTuple t1, double t2)
	{
		return t1 < (JlTuple)t2;
	}

	/// <summary>比较元组首元素与字符串所表示的数值标量，判断是否小于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &lt; (JlTuple)t2</c>，字符串按数值转换成标量后与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1, 5);
	///   bool c = a &lt; "3";       // 首元素 1 &lt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <(JlTuple t1, string t2)
	{
		return t1 < (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 <see cref="JlTupleElements"/> 标量，判断是否小于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &lt; (JlTuple)t2</c>，标量元素当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1, 5);
	///   bool c = a &lt; new JlTuple(3)[0];   // 首元素 1 &lt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <(JlTuple t1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)t2;
		return t1 < hTuple;
	}

	/// <summary>比较两元组，判断其是否大于对方。</summary>
	/// <remarks>
	///   <para>等价于 <c>(int)t1.TupleGreater(t2) != 0</c>，对两元组逐元素做大于比较，取结果首元素判断真值；单元素广播规则同样适用。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(5, 1);
	///   JlTuple b = new JlTuple(3, 2);
	///   bool c = a &gt; b;        // 首元素 5 &gt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >(JlTuple t1, JlTuple t2)
	{
		return (int)t1.TupleGreater(t2) != 0;
	}

	/// <summary>比较元组首元素与 int 标量，判断是否大于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &gt; (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(5, 1);
	///   bool c = a &gt; 3;         // 首元素 5 &gt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >(JlTuple t1, int t2)
	{
		return t1 > (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 long 标量，判断是否大于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &gt; (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(5L, 1L);
	///   bool c = a &gt; 3L;        // 首元素 5 &gt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >(JlTuple t1, long t2)
	{
		return t1 > (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 float 标量，判断是否大于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &gt; (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(5.5f, 1.5f);
	///   bool c = a &gt; 3.0f;      // 首元素 5.5 &gt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >(JlTuple t1, float t2)
	{
		return t1 > (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 double 标量，判断是否大于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &gt; (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(5.5, 1.5);
	///   bool c = a &gt; 3.0;       // 首元素 5.5 &gt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >(JlTuple t1, double t2)
	{
		return t1 > (JlTuple)t2;
	}

	/// <summary>比较元组首元素与字符串所表示的数值标量，判断是否大于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &gt; (JlTuple)t2</c>，字符串按数值转换成标量后与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(5, 1);
	///   bool c = a &gt; "3";       // 首元素 5 &gt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >(JlTuple t1, string t2)
	{
		return t1 > (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 <see cref="JlTupleElements"/> 标量，判断是否大于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &gt; (JlTuple)t2</c>，标量元素当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(5, 1);
	///   bool c = a &gt; new JlTuple(3)[0];   // 首元素 5 &gt; 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >(JlTuple t1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)t2;
		return t1 > hTuple;
	}

	/// <summary>比较两元组，判断其是否大于等于对方。</summary>
	/// <remarks>
	///   <para>等价于 <c>!(t1 &lt; t2)</c>，即两元组逐元素比较结果的首元素非"小于"即为真；单元素广播规则同样适用。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(3, 1);
	///   JlTuple b = new JlTuple(3, 5);
	///   bool c = a &gt;= b;       // 首元素 3 >= 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >=(JlTuple t1, JlTuple t2)
	{
		return !(t1 < t2);
	}

	/// <summary>比较元组首元素与 int 标量，判断是否大于等于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &gt;= (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(3, 1);
	///   bool c = a &gt;= 3;        // 首元素 3 >= 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >=(JlTuple t1, int t2)
	{
		return t1 >= (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 long 标量，判断是否大于等于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &gt;= (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(3L, 1L);
	///   bool c = a &gt;= 3L;       // 首元素 3 >= 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >=(JlTuple t1, long t2)
	{
		return t1 >= (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 float 标量，判断是否大于等于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &gt;= (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(3.0f, 1.0f);
	///   bool c = a &gt;= 3.0f;     // 首元素 3 >= 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >=(JlTuple t1, float t2)
	{
		return t1 >= (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 double 标量，判断是否大于等于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &gt;= (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(3.0, 1.0);
	///   bool c = a &gt;= 3.0;      // 首元素 3 >= 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >=(JlTuple t1, double t2)
	{
		return t1 >= (JlTuple)t2;
	}

	/// <summary>比较元组首元素与字符串所表示的数值标量，判断是否大于等于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &gt;= (JlTuple)t2</c>，字符串按数值转换成标量后与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(3, 1);
	///   bool c = a &gt;= "3";      // 首元素 3 >= 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >=(JlTuple t1, string t2)
	{
		return t1 >= (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 <see cref="JlTupleElements"/> 标量，判断是否大于等于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &gt;= (JlTuple)t2</c>，标量元素当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(3, 1);
	///   bool c = a &gt;= new JlTuple(3)[0];   // 首元素 3 >= 3 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator >=(JlTuple t1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)t2;
		return t1 >= hTuple;
	}

	/// <summary>比较两元组，判断其是否小于等于对方。</summary>
	/// <remarks>
	///   <para>等价于 <c>!(t1 &gt; t2)</c>，即两元组逐元素比较结果的首元素非"大于"即为真；单元素广播规则同样适用。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2, 6);
	///   JlTuple b = new JlTuple(2, 1);
	///   bool c = a &lt;= b;  // 首元素 2 &lt;= 2 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <=(JlTuple t1, JlTuple t2)
	{
		return !(t1 > t2);
	}

	/// <summary>比较元组首元素与 int 标量，判断是否小于等于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &lt;= (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2, 6);
	///   bool c = a &lt;= 2;        // 首元素 2 &lt;= 2 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <=(JlTuple t1, int t2)
	{
		return t1 <= (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 long 标量，判断是否小于等于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &lt;= (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2L, 6L);
	///   bool c = a &lt;= 2L;  // 首元素 2 &lt;= 2 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <=(JlTuple t1, long t2)
	{
		return t1 <= (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 float 标量，判断是否小于等于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &lt;= (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2.0f, 6.0f);
	///   bool c = a &lt;= 2.0f;     // 首元素 2 &lt;= 2 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <=(JlTuple t1, float t2)
	{
		return t1 <= (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 double 标量，判断是否小于等于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &lt;= (JlTuple)t2</c>，把标量当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2.0, 6.0);
	///   bool c = a &lt;= 2.0;      // 首元素 2 &lt;= 2 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <=(JlTuple t1, double t2)
	{
		return t1 <= (JlTuple)t2;
	}

	/// <summary>比较元组首元素与字符串所表示的数值标量，判断是否小于等于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &lt;= (JlTuple)t2</c>，字符串按数值转换成标量后与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2, 6);
	///   bool c = a &lt;= "2";      // 首元素 2 &lt;= 2 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <=(JlTuple t1, string t2)
	{
		return t1 <= (JlTuple)t2;
	}

	/// <summary>比较元组首元素与 <see cref="JlTupleElements"/> 标量，判断是否小于等于。</summary>
	/// <remarks>
	///   <para>等价于 <c>t1 &lt;= (JlTuple)t2</c>，标量元素当单元素元组与 <c>t1</c> 逐元素比较，结果取首比较项。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2, 6);
	///   bool c = a &lt;= new JlTuple(2)[0];   // 首元素 2 &lt;= 2 -> c 为 true
	///   </code>
	/// </remarks>
	public static bool operator <=(JlTuple t1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)t2;
		return t1 <= hTuple;
	}

	/// <summary>一元取负：对元组每个元素取相反数。</summary>
	/// <remarks>
	///   <para>等价于 <c>t.TupleNeg()</c>，结果与输入等长。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1, -2, 3);
	///   JlTuple c = -a;    // c == (-1, 2, -3)
	///   </code>
	/// </remarks>
	public static JlTuple operator -(JlTuple t)
	{
		return t.TupleNeg();
	}

	/// <summary>元组控制循环的续跑判据：按 increment 首元素的符号选比较方向，判断本元组首元素是否还未越过 final_value 首元素。</summary>
	/// <param name="final_value">循环终值元组，仅比较其首元素。</param>
	/// <param name="increment">步长元组，仅其首元素的正负决定用 &lt;= 还是 &gt;= 比较。</param>
	/// <returns>true 表示还可以再走一轮；纯布尔，不涉及句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>方法体无原生调用：<c>increment[0] &lt; 0</c> 时返回 <c>this[0].D &gt;= final_value[0].D</c>，
	///   否则返回 <c>this[0].D &lt;= final_value[0].D</c>——升序判"未超终值"、降序判"未跌破终值"，
	///   三者都取 double 口径的首元素。</para>
	///   <para><b>约束或前提</b>任一入参（含本元组）为空元组时 <c>[0]</c> 越界抛
	///   <see cref="JlTupleAccessException"/>；本方法只看步长符号不看大小，游标推进由调用方自己负责，
	///   步长给 0 即死循环。</para>
	///   <para><b>与相邻用法的取舍</b>标 <c>EditorBrowsable(Never)</c>，服务原生风格的"元组当循环变量"
	///   模式；普通 C# 计数循环直接比较即可，不必绕本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple cur = new double[] { 1.0 };
	///   JlTuple last = new double[] { 5.0 };
	///   JlTuple step = new double[] { 1.0 };
	///   bool goOn = cur.Continue(last, step);   // 步长为正：1.0 &lt;= 5.0 -> true
	///   cur.Dispose();
	///   last.Dispose();
	///   step.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>用它驱动循环时每轮生成的新游标元组是独立对象，要各自负责回收。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public bool Continue(JlTuple final_value, JlTuple increment)
	{
		if (increment[0] < 0.0)
		{
			return this[0].D >= final_value[0].D;
		}
		return this[0].D <= final_value[0].D;
	}

	/// <summary>把 <paramref name="tuple"/> 追加到本元组的末尾。</summary>
	/// <param name="tuple">要追加的数据。</param>
	/// <remarks>
	///   <para><b>功能说明</b>：等价于 <c>本元组.TupleConcat(tuple)</c>，结果替换本元组内容——调用后
	///   <see cref="Length"/> 增加 <c>tuple.Length</c>。</para>
	///   <para><b>资源与坑</b>：内部是<b>先建新元组、再释放原来的旧句柄</b>；若旧句柄还被别的引用持有，
	///   追加后读到的是新对象。调用前如有别处仍引用本元组，需自行处理引用关系。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1, 2);
	///   JlTuple b = new JlTuple(3, 4);
	///   a.Append(b);           // a 变为 (1, 2, 3, 4)，a.Length == 4
	///   a.Dispose(); b.Dispose();
	///   </code>
	/// </remarks>
	public void Append(JlTuple tuple)
	{
		JlTupleImplementation hTupleImplementation = data;
		data = TupleConcat(tuple).data;
		hTupleImplementation.Dispose();
	}




	private bool ProcessNative2To1(JlTuple t2, ResultSize type, out JlTuple result, NativeInt2To1 opInt, NativeLong2To1 opLong, NativeDouble2To1 opDouble)
	{
		int num = ((type == ResultSize.EQUAL) ? Length : (Length + t2.Length));
		if (num == 0)
		{
			result = new JlTuple();
			return true;
		}
		if (Type == t2.Type && (Length == t2.Length || type == ResultSize.SUM))
		{
			if (Type == JlTupleType.DOUBLE && opDouble != null)
			{
				double[] dArr = DArr;
				double[] dArr2 = t2.DArr;
				double[] array = new double[num];
				array[0] = Length;
				opDouble(dArr, dArr2, array);
				result = new JlTuple(new JlTupleDouble(array, copy: false));
				return true;
			}
			if (Type == JlTupleType.INTEGER && opInt != null)
			{
				int[] iArr = IArr;
				int[] iArr2 = t2.IArr;
				int[] array2 = new int[num];
				array2[0] = Length;
				opInt(iArr, iArr2, array2);
				result = new JlTuple(new JlTupleInt32(array2, copy: false));
				return true;
			}
			if (Type == JlTupleType.LONG && opLong != null)
			{
				long[] lArr = LArr;
				long[] lArr2 = t2.LArr;
				long[] array3 = new long[num];
				array3[0] = Length;
				opLong(lArr, lArr2, array3);
				result = new JlTuple(new JlTupleInt64(array3, copy: false));
				return true;
			}
		}
		result = null;
		return false;
	}

	private static void NativeIntAdd(int[] in1, int[] in2, int[] buffer)
	{
		for (int i = 0; i < buffer.Length; i++)
		{
			buffer[i] = in1[i] + in2[i];
		}
	}

	private static void NativeLongAdd(long[] in1, long[] in2, long[] buffer)
	{
		for (int i = 0; i < buffer.Length; i++)
		{
			buffer[i] = in1[i] + in2[i];
		}
	}

	private static void NativeDoubleAdd(double[] in1, double[] in2, double[] buffer)
	{
		for (int i = 0; i < buffer.Length; i++)
		{
			buffer[i] = in1[i] + in2[i];
		}
	}

	private static void NativeIntSub(int[] in1, int[] in2, int[] buffer)
	{
		for (int i = 0; i < buffer.Length; i++)
		{
			buffer[i] = in1[i] - in2[i];
		}
	}

	private static void NativeLongSub(long[] in1, long[] in2, long[] buffer)
	{
		for (int i = 0; i < buffer.Length; i++)
		{
			buffer[i] = in1[i] - in2[i];
		}
	}

	private static void NativeDoubleSub(double[] in1, double[] in2, double[] buffer)
	{
		for (int i = 0; i < buffer.Length; i++)
		{
			buffer[i] = in1[i] - in2[i];
		}
	}

	private static void NativeIntMult(int[] in1, int[] in2, int[] buffer)
	{
		for (int i = 0; i < buffer.Length; i++)
		{
			buffer[i] = in1[i] * in2[i];
		}
	}

	private static void NativeLongMult(long[] in1, long[] in2, long[] buffer)
	{
		for (int i = 0; i < buffer.Length; i++)
		{
			buffer[i] = in1[i] * in2[i];
		}
	}

	private static void NativeDoubleMult(double[] in1, double[] in2, double[] buffer)
	{
		for (int i = 0; i < buffer.Length; i++)
		{
			buffer[i] = in1[i] * in2[i];
		}
	}

	/// <summary>返回元组当前的元素个数，实现直接转发 Length 属性；空元组返回 0，遍历或判空前先调它。</summary>
	/// <returns>Number of elements of input tuple.</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：返回元组当前包含的元素个数，实现上直接转发到 <c>Length</c> 属性（<c>return Length;</c>），二者取值完全相同。</para>
	///   <para><b>何时用</b>：判断一个算子输出是否空元组、是否为单元素（很多数值算子对单元素与多元素的广播/降级行为不同），或在遍历前取上下界。空元组返回 0，此时 <c>this[0]</c> 会越界，先用本方法判空再索引。</para>
	///   <para><b>与相邻用法</b>：它统计的是"元组元素个数"，不是句柄对象集里的对象个数——后者应用 <c>CountObj()</c>。元组与 <c>Length</c> 属性只是写法差异，取数量优先用属性即可，保留本方法是为对齐原生算子命名。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(3, 5, 7, 9);
	///   int n = t.TupleLength();          // n == 4
	///   if (n &gt; 0)
	///   {
	///       double first = t[0];          // 先判空再按下标取值
	///   }
	///   </code>
	/// </remarks>
	public int TupleLength()
	{
		return Length;
	}

	/// <summary>两元组逐元素相加。</summary>
	/// <param name="s2">相加的右操作数元组。</param>
	/// <returns>逐元素相加得到的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：等价于 <c>this + s2</c>，按"对等下标逐元素加、单元素广播到全元组"的规则做算术加；内部优先走 <c>ProcessNative2To1</c>（<c>ResultSize.EQUAL</c>），不可用时回退到 <c>TupleAddOp</c>。</para>
	///   <para><b>资源与坑</b>：返回新建的独立元组，不改动当前元组；两元组长度不等时的对齐行为由底层原生算子决定，若需对等逐元语义请先保证等长或一方为单元素广播。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1, 2);
	///   JlTuple b = new JlTuple(10, 20);
	///   JlTuple c = a.TupleAdd(b);   // c == (11, 22)
	///   </code>
	///   <para><b>相关算子</b>：<c>operator+</c>、<c>TupleAddOp</c></para>
	/// </remarks>
	public JlTuple TupleAdd(JlTuple s2)
	{
		if (!ProcessNative2To1(s2, ResultSize.EQUAL, out var result, addInt, addLong, addDouble))
		{
			return TupleAddOp(s2);
		}
		return result;
	}

	/// <summary>两元组逐元素相减。</summary>
	/// <param name="d2">相减的右操作数元组（减数）。</param>
	/// <returns>逐元素相减得到的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：等价于 <c>this - d2</c>，按"对等下标逐元素减、单元素广播到全元组"的规则做算术减；内部优先走 <c>ProcessNative2To1</c>（<c>ResultSize.EQUAL</c>），不可用时回退到 <c>TupleSubOp</c>。</para>
	///   <para><b>资源与坑</b>：返回新建的独立元组，不改动当前元组；两元组长度不等时的对齐行为由底层原生算子决定，若需对等逐元语义请先保证等长或一方为单元素广播。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(5, 9);
	///   JlTuple b = new JlTuple(1, 4);
	///   JlTuple c = a.TupleSub(b);   // c == (4, 5)
	///   </code>
	///   <para><b>相关算子</b>：<c>operator-</c>、<c>TupleSubOp</c></para>
	/// </remarks>
	public JlTuple TupleSub(JlTuple d2)
	{
		if (!ProcessNative2To1(d2, ResultSize.EQUAL, out var result, subInt, subLong, subDouble))
		{
			return TupleSubOp(d2);
		}
		return result;
	}

	/// <summary>两元组逐元素相乘。</summary>
	/// <param name="p2">相乘的右操作数元组（乘数）。</param>
	/// <returns>逐元素相乘得到的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：等价于 <c>this * p2</c>，按"对等下标逐元素乘、单元素广播到全元组"的规则做算术乘；内部优先走 <c>ProcessNative2To1</c>（<c>ResultSize.EQUAL</c>），不可用时回退到 <c>TupleMultOp</c>。</para>
	///   <para><b>资源与坑</b>：返回新建的独立元组，不改动当前元组；两元组长度不等时的对齐行为由底层原生算子决定，若需对等逐元语义请先保证等长或一方为单元素广播。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(2, 3);
	///   JlTuple b = new JlTuple(4, 5);
	///   JlTuple c = a.TupleMult(b);   // c == (8, 15)
	///   </code>
	///   <para><b>相关算子</b>：<c>operator*</c>、<c>TupleMultOp</c></para>
	/// </remarks>
	public JlTuple TupleMult(JlTuple p2)
	{
		if (!ProcessNative2To1(p2, ResultSize.EQUAL, out var result, multInt, multLong, multDouble))
		{
			return TupleMultOp(p2);
		}
		return result;
	}

	/// <summary>将当前元组与多个元组首尾拼接成一个新元组。</summary>
	/// <param name="tuples">按顺序接到当前元组之后的若干元组。</param>
	/// <returns>拼接后的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：把当前元组与所有 <paramref name="tuples"/> 依次首尾相连成新元组；结果的类型按需升级（空元组让位于非空元组、类型不一致时升级为 <c>MIXED</c>）。</para>
	///   <para><b>资源与坑</b>：返回新建的独立元组，不改动当前元组及各入参元组；句柄类元素以引用方式共享持有。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1, 2);
	///   JlTuple b = new JlTuple(3, 4);
	///   JlTuple c = a.TupleConcat(b);      // c == (1, 2, 3, 4)
	///   JlTuple d = a.TupleConcat(b, b);   // d == (1, 2, 3, 4, 3, 4)
	///   </code>
	///   <para><b>相关算子</b>：<c>TupleConcat(t2)</c></para>
	/// </remarks>
	public JlTuple TupleConcat(params JlTuple[] tuples)
	{
		JlTupleType hTupleType = Type;
		int num = Length;
		for (int i = 0; i < tuples.Length; i++)
		{
			if (hTupleType == JlTupleType.EMPTY)
			{
				hTupleType = tuples[i].Type;
			}
			else if (hTupleType != tuples[i].Type && tuples[i].Type != JlTupleType.EMPTY)
			{
				hTupleType = JlTupleType.MIXED;
			}
			num += tuples[i].Length;
		}
		JlTupleImplementation hTupleImplementation = JlTupleImplementation.CreateInstanceForType(hTupleType, num);
		int num2 = hTupleImplementation.CopyFrom(data, 0);
		for (int j = 0; j < tuples.Length; j++)
		{
			num2 += hTupleImplementation.CopyFrom(tuples[j].data, num2);
		}
		return new JlTuple(hTupleImplementation);
	}

	/// <summary>将当前元组与另一元组首尾拼接成一个新元组。</summary>
	/// <param name="t2">接到当前元组之后的元组。</param>
	/// <returns>拼接后的新元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：把当前元组与 <paramref name="t2"/> 首尾相连成新元组；结果的类型按需升级（空元组让位于非空元组、类型不一致时升级为 <c>MIXED</c>）。等价于 <c>this.TupleConcat(t2)</c>（params 版本）。</para>
	///   <para><b>资源与坑</b>：返回新建的独立元组，不改动当前元组或 <paramref name="t2"/>；句柄类元素以引用方式共享持有。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple a = new JlTuple(1, 2);
	///   JlTuple b = new JlTuple(3, 4);
	///   JlTuple c = a.TupleConcat(b);     // c == (1, 2, 3, 4)
	///   </code>
	///   <para><b>相关算子</b>：<c>TupleConcat(params JlTuple[])</c></para>
	/// </remarks>
	public JlTuple TupleConcat(JlTuple t2)
	{
		JlTupleType hTupleType = Type;
		int size = Length + t2.Length;
		if (hTupleType == JlTupleType.EMPTY)
		{
			hTupleType = t2.Type;
		}
		else if (hTupleType != t2.Type && t2.Type != JlTupleType.EMPTY)
		{
			hTupleType = JlTupleType.MIXED;
		}
		JlTupleImplementation hTupleImplementation = JlTupleImplementation.CreateInstanceForType(hTupleType, size);
		hTupleImplementation.CopyFrom(t2.data, hTupleImplementation.CopyFrom(data, 0));
		return new JlTuple(hTupleImplementation);
	}
}
