using System;

namespace JLVisionLib;

/// <summary>元组某元素（或一组元素）的类型化访问器。</summary>
/// <remarks>
///   <para><b>功能说明</b>：从 <see cref="JlTuple"/> 按下标取出的单一/成组元素视图。同一内存位置可根据需要
///   以多种类型口径读取——<c>int</c>（<see cref="I"/>）、<c>long</c>（<see cref="L"/>）、<c>double</c>（<see cref="D"/>）、
///   <c>string</c>（<see cref="S"/>）、句柄（<see cref="H"/>）等。</para>
///   <para><b>类型匹配</b>：读取要求元素与目标类型兼容（整数可读 32/64 位、数值可读 <c>double</c> 等）；
///   不匹配会抛 <see cref="JlTupleAccessException"/>。写入时若直接赋值的类型与元组当前存储类型不一致，
///   会自动把元组<b>惰性转换为混合（mixed）类型</b>后再写入，见 <see cref="ConvertToMixed"/>。</para>
///   <para><b>取舍</b>：它是 <see cref="JlTuple"/> 三种索引器（单下标/下标数组/<see cref="JlTuple"/> 下标元组）的返回类型，
///   是按位/算术/比较运算符的重载载体——与标量、<see cref="JlTuple"/>、自身运算返回<b>新元组</b>，比较返回 <c>bool</c>；
///   并与 <c>bool</c>/<c>int</c>/<c>long</c>/<c>IntPtr</c>/<c>double</c>/<c>string</c> 互为隐式转换，取到单个元素时常直接当标量用而不再显式取 <c>I</c>/<c>D</c>。</para>
///   <para><b>资源与坑</b>：单元素属性（<see cref="I"/>/<see cref="D"/> 等）返回标量；成组属性（<see cref="IArr"/>/
///   <see cref="DArr"/> 等）返回数组。<see cref="IP"/> 按平台位数决定用 32 位还是 64 位整数承载指针。</para>
/// </remarks>
public class JlTupleElements
{
	private JlTuple parent;

	private JlTupleElementsImplementation elements;

	/// <summary>
	///   以 32 位整数读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为整数数据（32 位或 64 位）；类型不符会抛异常。读取对单个元素生效，返回标量。</remarks>
	public int I
	{
		get
		{
			return elements.I[0];
		}
		set
		{
			int[] i = new int[1] { value };
			try
			{
				elements.I = i;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.I = i;
			}
		}
	}

	/// <summary>
	///   以 32 位整数数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为整数数据（32 位或 64 位）。写入的一一对应到被选中的各下标。</remarks>
	public int[] IArr
	{
		get
		{
			return elements.I;
		}
		set
		{
			try
			{
				elements.I = value;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.I = value;
			}
		}
	}

	/// <summary>
	///   以 64 位整数读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为整数数据（32 位或 64 位）；类型不符会抛异常。读取对单个元素生效，返回标量。</remarks>
	public long L
	{
		get
		{
			return elements.L[0];
		}
		set
		{
			long[] l = new long[1] { value };
			try
			{
				elements.L = l;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.L = l;
			}
		}
	}

	/// <summary>
	///   以 64 位整数数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为整数数据（32 位或 64 位）。写入的一一对应到被选中的各下标。</remarks>
	public long[] LArr
	{
		get
		{
			return elements.L;
		}
		set
		{
			try
			{
				elements.L = value;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.L = value;
			}
		}
	}

	/// <summary>
	///   以 double 读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为数值数据；类型不符会抛异常。读取对单个元素生效，返回标量。</remarks>
	public double D
	{
		get
		{
			return elements.D[0];
		}
		set
		{
			double[] d = new double[1] { value };
			try
			{
				elements.D = d;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.D = d;
			}
		}
	}

	/// <summary>
	///   以 double 数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为数值数据。写入的一一对应到被选中的各下标。</remarks>
	public double[] DArr
	{
		get
		{
			return elements.D;
		}
		set
		{
			try
			{
				elements.D = value;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.D = value;
			}
		}
	}

	/// <summary>
	///   以字符串读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为字符串数据；类型不符会抛异常。读取对单个元素生效，返回标量。</remarks>
	public string S
	{
		get
		{
			return elements.S[0];
		}
		set
		{
			string[] s = new string[1] { value };
			try
			{
				elements.S = s;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.S = s;
			}
		}
	}

	/// <summary>
	///   以字符串数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为字符串数据。写入的一一对应到被选中的各下标。</remarks>
	public string[] SArr
	{
		get
		{
			return elements.S;
		}
		set
		{
			try
			{
				elements.S = value;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.S = value;
			}
		}
	}

	/// <summary>
	///   以句柄（<see cref="JlHandle"/>）读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为句柄数据；类型不符会抛异常。读取对单个元素生效，返回标量。</remarks>
	public JlHandle H
	{
		get
		{
			return elements.H[0];
		}
		set
		{
			JlHandle[] h = new JlHandle[1] { value };
			try
			{
				elements.H = h;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.H = h;
			}
		}
	}

	/// <summary>
	///   以句柄数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为句柄数据。写入的一一对应到被选中的各下标。</remarks>
	public JlHandle[] JlArr
	{
		get
		{
			return elements.H;
		}
		set
		{
			try
			{
				elements.H = value;
			}
			catch (JlTupleAccessException)
			{
				ConvertToMixed();
				elements.H = value;
			}
		}
	}

	/// <summary>
	///   以 object 读取/写入该元素。
	/// </summary>
	/// <remarks>元素可为任意类型，读取时数值会被装箱；写入时会按实际装箱类型自动选择对应的类型化赋值路径。</remarks>
	public object O
	{
		get
		{
			return elements.O[0];
		}
		set
		{
			if (elements is JlTupleElementsMixed)
			{
				elements.O[0] = value;
				return;
			}
			switch (JlTupleImplementation.GetObjectType(value))
			{
			case 1:
				I = (int)value;
				break;
			case 129:
				L = (long)value;
				break;
			case 2:
				D = (double)value;
				break;
			case 32898:
				F = (float)value;
				break;
			case 4:
				S = (string)value;
				break;
			case 16:
				H = (JlHandle)value;
				break;
			case 32900:
				IP = (IntPtr)value;
				break;
			default:
				throw new JlTupleAccessException("Attempting to assign object containing invalid type");
			}
		}
	}

	/// <summary>
	///   以 object 数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素可为任意类型，读取时数值会被装箱。写入时会按各元素的装箱类型选择类型化赋值路径。</remarks>
	public object[] OArr
	{
		get
		{
			return elements.O;
		}
		set
		{
			if (elements is JlTupleElementsMixed)
			{
				elements.O = value;
				return;
			}
			switch (JlTupleImplementation.GetObjectsType(value))
			{
			case 1:
				IArr = Array.ConvertAll(value, ObjectToInt);
				break;
			case 129:
				LArr = Array.ConvertAll(value, ObjectToLong);
				break;
			case 2:
				DArr = Array.ConvertAll(value, ObjectToDouble);
				break;
			case 32898:
				FArr = Array.ConvertAll(value, ObjectToFloat);
				break;
			case 4:
				SArr = Array.ConvertAll(value, ObjectToString);
				break;
			case 16:
				JlArr = Array.ConvertAll(value, ObjectToHandle);
				break;
			case 32900:
				IPArr = Array.ConvertAll(value, ObjectToIntPtr);
				break;
			default:
				throw new JlTupleAccessException("Attempting to assign object containing invalid type");
			}
		}
	}

	/// <summary>
	///   以 float 读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为数值数据；以 float 口径读取会有精度损失（内部按 double 存储）。读取对单个元素生效。</remarks>
	public float F
	{
		get
		{
			return (float)D;
		}
		set
		{
			D = value;
		}
	}

	/// <summary>
	///   以 float 数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为数值数据；读取/写入以 float 口径进行，存在精度损失。各值一一对应到被选中的下标。</remarks>
	public float[] FArr
	{
		get
		{
			double[] dArr = DArr;
			float[] array = new float[dArr.Length];
			for (int i = 0; i < dArr.Length; i++)
			{
				array[i] = (float)dArr[i];
			}
			return array;
		}
		set
		{
			double[] array = new double[value.Length];
			for (int i = 0; i < value.Length; i++)
			{
				array[i] = value[i];
			}
			DArr = array;
		}
	}

	/// <summary>
	///   以 IntPtr 读取/写入该元素。
	/// </summary>
	/// <remarks>元素须为代表指针的整数，且需匹配当前平台的 <see cref="IntPtr.Size"/>（64 位平台用 64 位整数、32 位平台用 32 位整数）。</remarks>
	public IntPtr IP
	{
		get
		{
			if (JlNativeApi.isPlatform64)
			{
				if (Type == JlTupleType.LONG || Type == JlTupleType.JlANDLE)
				{
					return new IntPtr(L);
				}
			}
			else if (Type == JlTupleType.INTEGER || Type == JlTupleType.JlANDLE)
			{
				return new IntPtr(I);
			}
			throw new JlTupleAccessException("Value does not represent a pointer on this platform");
		}
		set
		{
			if (Type == JlTupleType.JlANDLE)
			{
				value = H.Handle;
			}
			if (JlNativeApi.isPlatform64)
			{
				L = value.ToInt64();
			}
			else
			{
				I = value.ToInt32();
			}
		}
	}

	/// <summary>
	///   以 IntPtr 数组整体读取/写入这组元素。
	/// </summary>
	/// <remarks>元素须为代表指针的整数，且匹配当前平台的 <see cref="IntPtr.Size"/>。</remarks>
	public IntPtr[] IPArr
	{
		get
		{
			if (JlNativeApi.isPlatform64 && Type == JlTupleType.LONG)
			{
				IntPtr[] array = new IntPtr[LArr.Length];
				for (int i = 0; i < LArr.Length; i++)
				{
					array[i] = new IntPtr(LArr[i]);
				}
				return array;
			}
			if (Type == JlTupleType.INTEGER)
			{
				IntPtr[] array2 = new IntPtr[IArr.Length];
				for (int j = 0; j < IArr.Length; j++)
				{
					array2[j] = new IntPtr(IArr[j]);
				}
				return array2;
			}
			throw new JlTupleAccessException("Value does not represent a pointer on this platform");
		}
		set
		{
			if (JlNativeApi.isPlatform64)
			{
				long[] array = new long[value.Length];
				for (int i = 0; i < value.Length; i++)
				{
					array[i] = value[i].ToInt64();
				}
				LArr = array;
			}
			else
			{
				int[] array2 = new int[value.Length];
				for (int j = 0; j < value.Length; j++)
				{
					array2[j] = value[j].ToInt32();
				}
				IArr = array2;
			}
		}
	}

	/// <summary>该元素的实际数据类型。</summary>
	public JlTupleType Type => elements.Type;

	/// <summary>该访问器覆盖的元素个数（单元素访问时为 1）。</summary>
	internal int Length => elements.Length;

	internal JlTupleElements()
	{
		parent = null;
		elements = new JlTupleElementsImplementation();
	}

	internal JlTupleElements(JlTuple parent, JlTupleInt32 source, int index)
	{
		this.parent = parent;
		elements = new JlTupleElementsInt32(source, index);
	}

	internal JlTupleElements(JlTuple parent, JlTupleInt32 source, int[] indices)
	{
		this.parent = parent;
		elements = new JlTupleElementsInt32(source, indices);
	}

	internal JlTupleElements(JlTuple parent, JlTupleInt64 tupleImp, int index)
	{
		this.parent = parent;
		elements = new JlTupleElementsInt64(tupleImp, index);
	}

	internal JlTupleElements(JlTuple parent, JlTupleInt64 tupleImp, int[] indices)
	{
		this.parent = parent;
		elements = new JlTupleElementsInt64(tupleImp, indices);
	}

	internal JlTupleElements(JlTuple parent, JlTupleDouble tupleImp, int index)
	{
		this.parent = parent;
		elements = new JlTupleElementsDouble(tupleImp, index);
	}

	internal JlTupleElements(JlTuple parent, JlTupleDouble tupleImp, int[] indices)
	{
		this.parent = parent;
		elements = new JlTupleElementsDouble(tupleImp, indices);
	}

	internal JlTupleElements(JlTuple parent, JlTupleString tupleImp, int index)
	{
		this.parent = parent;
		elements = new JlTupleElementsString(tupleImp, index);
	}

	internal JlTupleElements(JlTuple parent, JlTupleString tupleImp, int[] indices)
	{
		this.parent = parent;
		elements = new JlTupleElementsString(tupleImp, indices);
	}

	internal JlTupleElements(JlTuple parent, JlTupleHandle tupleImp, int index)
	{
		this.parent = parent;
		elements = new JlTupleElementsHandle(tupleImp, index);
	}

	internal JlTupleElements(JlTuple parent, JlTupleHandle tupleImp, int[] indices)
	{
		this.parent = parent;
		elements = new JlTupleElementsHandle(tupleImp, indices);
	}

	internal JlTupleElements(JlTuple parent, JlTupleMixed tupleImp, int index)
	{
		this.parent = parent;
		elements = new JlTupleElementsMixed(tupleImp, index);
	}

	internal JlTupleElements(JlTuple parent, JlTupleMixed tupleImp, int[] indices)
	{
		this.parent = parent;
		elements = new JlTupleElementsMixed(tupleImp, indices);
	}

	/// <summary>将装箱的 object 强制拆箱为 int；装箱类型不符会抛 InvalidCastException。供 OArr 批量转换使用。</summary>
	public static int ObjectToInt(object o)
	{
		return (int)o;
	}

	/// <summary>将装箱的 object 强制拆箱为 long；装箱类型不符会抛 InvalidCastException。供 OArr 批量转换使用。</summary>
	public static long ObjectToLong(object o)
	{
		return (long)o;
	}

	/// <summary>将装箱的 object 强制拆箱为 double；装箱类型不符会抛 InvalidCastException。供 OArr 批量转换使用。</summary>
	public static double ObjectToDouble(object o)
	{
		return (double)o;
	}

	/// <summary>将装箱的 object 强制拆箱为 float；装箱类型不符会抛 InvalidCastException。供 OArr 批量转换使用。</summary>
	public static float ObjectToFloat(object o)
	{
		return (float)o;
	}

	/// <summary>将 object 按引用转为 string；非字符串引用会抛 InvalidCastException。供 OArr 批量转换使用。</summary>
	public static string ObjectToString(object o)
	{
		return (string)o;
	}

	/// <summary>将 object 按引用转为 JlHandle；非句柄引用会抛 InvalidCastException。供 OArr 批量转换使用。</summary>
	public static JlHandle ObjectToHandle(object o)
	{
		return (JlHandle)o;
	}

	/// <summary>将装箱的 object 强制拆箱为 IntPtr；装箱类型不符会抛 InvalidCastException。供 OArr 批量转换使用。</summary>
	public static IntPtr ObjectToIntPtr(object o)
	{
		return (IntPtr)o;
	}

	internal void ConvertToMixed()
	{
		if (elements is JlTupleElementsMixed)
		{
			throw new JlTupleAccessException();
		}
		elements = parent.ConvertToMixed(elements.getIndices());
	}

	/// <summary>把元素视图转为元组后与 int 标量逐元素相加，返回新 JlTuple。</summary>
	public static JlTuple operator +(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple + hTuple2;
	}

	/// <summary>把元素视图转为元组后与 long 标量逐元素相加，返回新 JlTuple。</summary>
	public static JlTuple operator +(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple + hTuple2;
	}

	/// <summary>把元素视图转为元组后与 float 标量逐元素相加，返回新 JlTuple。</summary>
	public static JlTuple operator +(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple + hTuple2;
	}

	/// <summary>把元素视图转为元组后与 double 标量逐元素相加，返回新 JlTuple。</summary>
	public static JlTuple operator +(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple + hTuple2;
	}

	/// <summary>两侧转为元组后转调 JlTuple 的加运算符；字符串元素的行为[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator +(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple + hTuple2;
	}

	/// <summary>两侧元素视图转为元组后逐元素相加，返回新 JlTuple。</summary>
	public static JlTuple operator +(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple + hTuple2;
	}

	/// <summary>把元素视图转为元组后与元组 t2 逐元素相加，返回新 JlTuple。</summary>
	public static JlTuple operator +(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple + t2;
	}

	/// <summary>把元素视图转为元组后逐元素减去 int 标量，返回新 JlTuple。</summary>
	public static JlTuple operator -(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple - hTuple2;
	}

	/// <summary>把元素视图转为元组后逐元素减去 long 标量，返回新 JlTuple。</summary>
	public static JlTuple operator -(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple - hTuple2;
	}

	/// <summary>把元素视图转为元组后逐元素减去 float 标量，返回新 JlTuple。</summary>
	public static JlTuple operator -(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple - hTuple2;
	}

	/// <summary>把元素视图转为元组后逐元素减去 double 标量，返回新 JlTuple。</summary>
	public static JlTuple operator -(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple - hTuple2;
	}

	/// <summary>两侧转为元组后转调 JlTuple 的减运算符；字符串元素的行为[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator -(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple - hTuple2;
	}

	/// <summary>两侧元素视图转为元组后逐元素相减（左减右），返回新 JlTuple。</summary>
	public static JlTuple operator -(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple - hTuple2;
	}

	/// <summary>把元素视图转为元组后逐元素减去元组 t2，返回新 JlTuple。</summary>
	public static JlTuple operator -(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple - t2;
	}

	/// <summary>把元素视图转为元组后与 int 标量逐元素相乘，返回新 JlTuple。</summary>
	public static JlTuple operator *(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple * hTuple2;
	}

	/// <summary>把元素视图转为元组后与 long 标量逐元素相乘，返回新 JlTuple。</summary>
	public static JlTuple operator *(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple * hTuple2;
	}

	/// <summary>把元素视图转为元组后与 float 标量逐元素相乘，返回新 JlTuple。</summary>
	public static JlTuple operator *(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple * hTuple2;
	}

	/// <summary>把元素视图转为元组后与 double 标量逐元素相乘，返回新 JlTuple。</summary>
	public static JlTuple operator *(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple * hTuple2;
	}

	/// <summary>两侧转为元组后转调 JlTuple 的乘运算符；字符串元素的行为[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator *(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple * hTuple2;
	}

	/// <summary>两侧元素视图转为元组后逐元素相乘，返回新 JlTuple。</summary>
	public static JlTuple operator *(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple * hTuple2;
	}

	/// <summary>把元素视图转为元组后与元组 t2 逐元素相乘，返回新 JlTuple。</summary>
	public static JlTuple operator *(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple * t2;
	}

	/// <summary>把元素视图转为元组后逐元素除以 int 标量，返回新 JlTuple；整数商口径[待实测]。</summary>
	public static JlTuple operator /(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple / hTuple2;
	}

	/// <summary>把元素视图转为元组后逐元素除以 long 标量，返回新 JlTuple；整数商口径[待实测]。</summary>
	public static JlTuple operator /(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple / hTuple2;
	}

	/// <summary>把元素视图转为元组后逐元素除以 float 标量，返回新 JlTuple。</summary>
	public static JlTuple operator /(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple / hTuple2;
	}

	/// <summary>把元素视图转为元组后逐元素除以 double 标量，返回新 JlTuple。</summary>
	public static JlTuple operator /(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple / hTuple2;
	}

	/// <summary>两侧转为元组后转调 JlTuple 的除运算符；字符串元素的行为[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator /(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple / hTuple2;
	}

	/// <summary>两侧元素视图转为元组后逐元素相除（左除右），返回新 JlTuple。</summary>
	public static JlTuple operator /(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple / hTuple2;
	}

	/// <summary>把元素视图转为元组后逐元素除以元组 t2，返回新 JlTuple。</summary>
	public static JlTuple operator /(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple / t2;
	}

	/// <summary>把元素视图转为元组后逐元素对 int 标量取余，返回新 JlTuple。</summary>
	public static JlTuple operator %(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple % hTuple2;
	}

	/// <summary>把元素视图转为元组后逐元素对 long 标量取余，返回新 JlTuple。</summary>
	public static JlTuple operator %(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple % hTuple2;
	}

	/// <summary>把元素视图转为元组后逐元素对 float 标量取余，返回新 JlTuple；浮点取余口径[待实测]。</summary>
	public static JlTuple operator %(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple % hTuple2;
	}

	/// <summary>把元素视图转为元组后逐元素对 double 标量取余，返回新 JlTuple；浮点取余口径[待实测]。</summary>
	public static JlTuple operator %(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple % hTuple2;
	}

	/// <summary>两侧转为元组后转调 JlTuple 的取余运算符；字符串元素的行为[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator %(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple % hTuple2;
	}

	/// <summary>两侧元素视图转为元组后逐元素取余（左除右取余），返回新 JlTuple。</summary>
	public static JlTuple operator %(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple % hTuple2;
	}

	/// <summary>把元素视图转为元组后逐元素对元组 t2 取余，返回新 JlTuple。</summary>
	public static JlTuple operator %(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple % t2;
	}

	/// <summary>把元素视图转为元组后与 int 标量逐元素按位与，返回新 JlTuple。</summary>
	public static JlTuple operator &(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple & hTuple2;
	}

	/// <summary>把元素视图转为元组后与 long 标量逐元素按位与，返回新 JlTuple。</summary>
	public static JlTuple operator &(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple & hTuple2;
	}

	/// <summary>转为元组后按 JlTuple 的按位与运算符与 float 标量组合；浮点元素适用性[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator &(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple & hTuple2;
	}

	/// <summary>转为元组后按 JlTuple 的按位与运算符与 double 标量组合；浮点元素适用性[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator &(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple & hTuple2;
	}

	/// <summary>两侧转为元组后转调 JlTuple 的按位与运算符；字符串元素的行为[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator &(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple & hTuple2;
	}

	/// <summary>两侧元素视图转为元组后逐元素按位与，返回新 JlTuple。</summary>
	public static JlTuple operator &(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple & hTuple2;
	}

	/// <summary>把元素视图转为元组后与元组 t2 逐元素按位与，返回新 JlTuple。</summary>
	public static JlTuple operator &(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple & t2;
	}

	/// <summary>把元素视图转为元组后与 int 标量逐元素按位或，返回新 JlTuple。</summary>
	public static JlTuple operator |(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple | hTuple2;
	}

	/// <summary>把元素视图转为元组后与 long 标量逐元素按位或，返回新 JlTuple。</summary>
	public static JlTuple operator |(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple | hTuple2;
	}

	/// <summary>转为元组后按 JlTuple 的按位或运算符与 float 标量组合；浮点元素适用性[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator |(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple | hTuple2;
	}

	/// <summary>转为元组后按 JlTuple 的按位或运算符与 double 标量组合；浮点元素适用性[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator |(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple | hTuple2;
	}

	/// <summary>两侧转为元组后转调 JlTuple 的按位或运算符；字符串元素的行为[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator |(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple | hTuple2;
	}

	/// <summary>两侧元素视图转为元组后逐元素按位或，返回新 JlTuple。</summary>
	public static JlTuple operator |(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple | hTuple2;
	}

	/// <summary>把元素视图转为元组后与元组 t2 逐元素按位或，返回新 JlTuple。</summary>
	public static JlTuple operator |(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple | t2;
	}

	/// <summary>把元素视图转为元组后与 int 标量逐元素按位异或，返回新 JlTuple。</summary>
	public static JlTuple operator ^(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple ^ hTuple2;
	}

	/// <summary>把元素视图转为元组后与 long 标量逐元素按位异或，返回新 JlTuple。</summary>
	public static JlTuple operator ^(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple ^ hTuple2;
	}

	/// <summary>转为元组后按 JlTuple 的按位异或运算符与 float 标量组合；浮点元素适用性[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator ^(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple ^ hTuple2;
	}

	/// <summary>转为元组后按 JlTuple 的按位异或运算符与 double 标量组合；浮点元素适用性[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator ^(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple ^ hTuple2;
	}

	/// <summary>两侧转为元组后转调 JlTuple 的按位异或运算符；字符串元素的行为[待实测]，返回新 JlTuple。</summary>
	public static JlTuple operator ^(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple ^ hTuple2;
	}

	/// <summary>两侧元素视图转为元组后逐元素按位异或，返回新 JlTuple。</summary>
	public static JlTuple operator ^(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple ^ hTuple2;
	}

	/// <summary>把元素视图转为元组后与元组 t2 逐元素按位异或，返回新 JlTuple。</summary>
	public static JlTuple operator ^(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple ^ t2;
	}

	/// <summary>把元素视图转为元组后与 int 标量逐元素做小于比较，返回 bool（多元素结果的归并规则随 JlTuple 小于运算符）。</summary>
	public static bool operator <(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple < hTuple2;
	}

	/// <summary>把元素视图转为元组后与 long 标量逐元素做小于比较，返回 bool（归并规则随 JlTuple 小于运算符）。</summary>
	public static bool operator <(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple < hTuple2;
	}

	/// <summary>把元素视图转为元组后与 float 标量逐元素做小于比较，返回 bool（归并规则随 JlTuple 小于运算符）。</summary>
	public static bool operator <(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple < hTuple2;
	}

	/// <summary>把元素视图转为元组后与 double 标量逐元素做小于比较，返回 bool（归并规则随 JlTuple 小于运算符）。</summary>
	public static bool operator <(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple < hTuple2;
	}

	/// <summary>两侧转为元组后转调 JlTuple 的小于运算符，返回 bool；字符串元素的比较行为[待实测]。</summary>
	public static bool operator <(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple < hTuple2;
	}

	/// <summary>两侧元素视图转为元组后逐元素做小于比较，返回 bool（归并规则随 JlTuple 小于运算符）。</summary>
	public static bool operator <(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple < hTuple2;
	}

	/// <summary>把元素视图转为元组后与元组 t2 逐元素做小于比较，返回 bool（归并规则随 JlTuple 小于运算符）。</summary>
	public static bool operator <(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple < t2;
	}

	/// <summary>把元素视图转为元组后与 int 标量逐元素做大于比较，返回 bool（归并规则随 JlTuple 大于运算符）。</summary>
	public static bool operator >(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple > hTuple2;
	}

	/// <summary>把元素视图转为元组后与 long 标量逐元素做大于比较，返回 bool（归并规则随 JlTuple 大于运算符）。</summary>
	public static bool operator >(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple > hTuple2;
	}

	/// <summary>把元素视图转为元组后与 float 标量逐元素做大于比较，返回 bool（归并规则随 JlTuple 大于运算符）。</summary>
	public static bool operator >(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple > hTuple2;
	}

	/// <summary>把元素视图转为元组后与 double 标量逐元素做大于比较，返回 bool（归并规则随 JlTuple 大于运算符）。</summary>
	public static bool operator >(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple > hTuple2;
	}

	/// <summary>两侧转为元组后转调 JlTuple 的大于运算符，返回 bool；字符串元素的比较行为[待实测]。</summary>
	public static bool operator >(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple > hTuple2;
	}

	/// <summary>两侧元素视图转为元组后逐元素做大于比较，返回 bool（归并规则随 JlTuple 大于运算符）。</summary>
	public static bool operator >(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple > hTuple2;
	}

	/// <summary>把元素视图转为元组后与元组 t2 逐元素做大于比较，返回 bool（归并规则随 JlTuple 大于运算符）。</summary>
	public static bool operator >(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple > t2;
	}

	/// <summary>把元素视图转为元组后与 int 标量逐元素做小于等于比较，返回 bool（归并规则随 JlTuple 小于等于运算符）。</summary>
	public static bool operator <=(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple <= hTuple2;
	}

	/// <summary>把元素视图转为元组后与 long 标量逐元素做小于等于比较，返回 bool（归并规则随 JlTuple 小于等于运算符）。</summary>
	public static bool operator <=(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple <= hTuple2;
	}

	/// <summary>把元素视图转为元组后与 float 标量逐元素做小于等于比较，返回 bool（归并规则随 JlTuple 小于等于运算符）。</summary>
	public static bool operator <=(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple <= hTuple2;
	}

	/// <summary>把元素视图转为元组后与 double 标量逐元素做小于等于比较，返回 bool（归并规则随 JlTuple 小于等于运算符）。</summary>
	public static bool operator <=(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple <= hTuple2;
	}

	/// <summary>两侧转为元组后转调 JlTuple 的小于等于运算符，返回 bool；字符串元素的比较行为[待实测]。</summary>
	public static bool operator <=(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple <= hTuple2;
	}

	/// <summary>两侧元素视图转为元组后逐元素做小于等于比较，返回 bool（归并规则随 JlTuple 小于等于运算符）。</summary>
	public static bool operator <=(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple <= hTuple2;
	}

	/// <summary>把元素视图转为元组后与元组 t2 逐元素做小于等于比较，返回 bool（归并规则随 JlTuple 小于等于运算符）。</summary>
	public static bool operator <=(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple <= t2;
	}

	/// <summary>把元素视图转为元组后与 int 标量逐元素做大于等于比较，返回 bool（归并规则随 JlTuple 大于等于运算符）。</summary>
	public static bool operator >=(JlTupleElements e1, int t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple >= hTuple2;
	}

	/// <summary>把元素视图转为元组后与 long 标量逐元素做大于等于比较，返回 bool（归并规则随 JlTuple 大于等于运算符）。</summary>
	public static bool operator >=(JlTupleElements e1, long t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple >= hTuple2;
	}

	/// <summary>把元素视图转为元组后与 float 标量逐元素做大于等于比较，返回 bool（归并规则随 JlTuple 大于等于运算符）。</summary>
	public static bool operator >=(JlTupleElements e1, float t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple >= hTuple2;
	}

	/// <summary>把元素视图转为元组后与 double 标量逐元素做大于等于比较，返回 bool（归并规则随 JlTuple 大于等于运算符）。</summary>
	public static bool operator >=(JlTupleElements e1, double t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple >= hTuple2;
	}

	/// <summary>两侧转为元组后转调 JlTuple 的大于等于运算符，返回 bool；字符串元素的比较行为[待实测]。</summary>
	public static bool operator >=(JlTupleElements e1, string t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple >= hTuple2;
	}

	/// <summary>两侧元素视图转为元组后逐元素做大于等于比较，返回 bool（归并规则随 JlTuple 大于等于运算符）。</summary>
	public static bool operator >=(JlTupleElements e1, JlTupleElements t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		using JlTuple hTuple2 = (JlTuple)t2;
		return hTuple >= hTuple2;
	}

	/// <summary>把元素视图转为元组后与元组 t2 逐元素做大于等于比较，返回 bool（归并规则随 JlTuple 大于等于运算符）。</summary>
	public static bool operator >=(JlTupleElements e1, JlTuple t2)
	{
		using JlTuple hTuple = (JlTuple)e1;
		return hTuple >= t2;
	}

	/// <summary>把元素视图隐式转为 bool：按 64 位整数读取该元素，非零即 true。</summary>
	public static implicit operator bool(JlTupleElements hte)
	{
		return hte.L != 0;
	}

	/// <summary>把元素视图隐式转为 int 标量，等价于读取 I。</summary>
	public static implicit operator int(JlTupleElements hte)
	{
		return hte.I;
	}

	/// <summary>把元素视图隐式转为 long 标量，等价于读取 L。</summary>
	public static implicit operator long(JlTupleElements hte)
	{
		return hte.L;
	}

	/// <summary>把元素视图隐式转为 IntPtr 标量，等价于读取 IP。</summary>
	public static implicit operator IntPtr(JlTupleElements hte)
	{
		return hte.IP;
	}

	/// <summary>把元素视图隐式转为 double 标量，等价于读取 D。</summary>
	public static implicit operator double(JlTupleElements hte)
	{
		return hte.D;
	}

	/// <summary>把元素视图隐式转为 string，等价于读取 S。</summary>
	public static implicit operator string(JlTupleElements hte)
	{
		return hte.S;
	}

	/// <summary>把 int 标量隐式转为元素视图：新建单元素元组并取其首元素（非共享）。</summary>
	public static implicit operator JlTupleElements(int i)
	{
		return new JlTuple(i)[0];
	}

	/// <summary>把 long 标量隐式转为元素视图：新建单元素元组并取其首元素（非共享）。</summary>
	public static implicit operator JlTupleElements(long l)
	{
		return new JlTuple(l)[0];
	}

	/// <summary>把 IntPtr 隐式转为元素视图：新建单元素元组并取其首元素（非共享）。</summary>
	public static implicit operator JlTupleElements(IntPtr ip)
	{
		return new JlTuple(ip)[0];
	}

	/// <summary>把 double 标量隐式转为元素视图：新建单元素元组并取其首元素（非共享）。</summary>
	public static implicit operator JlTupleElements(double d)
	{
		return new JlTuple(d)[0];
	}

	/// <summary>把 string 隐式转为元素视图：新建单元素字符串元组并取其首元素（非共享）。</summary>
	public static implicit operator JlTupleElements(string s)
	{
		return new JlTuple(s)[0];
	}

	/// <summary>把 JlHandle 隐式转为元素视图：包成单元素句柄元组，不复制句柄数组。</summary>
	public static implicit operator JlTupleElements(JlHandle h)
	{
		return new JlTuple(new JlTupleHandle(new JlHandle[1] { h }, copy: false));
	}
}
