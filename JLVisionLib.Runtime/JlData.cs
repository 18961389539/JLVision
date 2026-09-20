using System;

namespace JLVisionLib;

/// <summary>
///   非句柄的数据/值型包装基类（如 <c>JlHomMat2D</c>、<c>JlPose</c> 等）：内部以一个 <see cref="JlTuple"/> 承载分量，而非持有原生句柄或 key。与 <see cref="JlHandleBase"/>、<see cref="JlObjectBase"/> 不同，它<b>不实现 IDisposable</b>、无须手动释放；构造均为 internal，实际经公开派生类使用。
/// </summary>
public class JlData
{
	internal JlTuple tuple;

	/// <summary>
	///   宿主内部那个 JlTuple 本体（托管元组引用、无单位、不是副本）：get 交出同一个引用，set 把赋入的元组再包一层新 JlTuple 后替换字段；JlData 系不实现 IDisposable。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>读：<c>get</c> 直接返回内部字段 <c>tuple</c> 本身（同一 JlTuple 引用，非副本）。写：<c>set</c> 走 <c>tuple = new JlTuple(value)</c>，把赋进来的元组重新包成一个新的 JlTuple 对象再替换字段。</para>
	///   <para><b>约束或前提</b>因 get 返回的是活引用：改这个返回的 JlTuple 会同步反映到宿主对象（JlPose/JlData 家族），反之亦然；而一旦执行 set，宿主就与旧元组断链，之前 get 出来的引用不再随宿主变化。分量的内部排列由原生侧决定（对 JlPose 即 7 个位姿分量，具体槽序 [待实测]）。</para>
	///   <para><b>与相邻算子的取舍</b>只想按类型取单个分量用索引器 <c>this[int]</c>（返回 <c>JlTupleElements</c> 视图，可 <c>.D/.I/.S</c> 读）；要整体搬运/比对才动 RawData。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlTuple raw = pose.RawData;            // get：拿到底层同一个 JlTuple 引用
	///   JlPose other = new JlPose(0.2, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   pose.RawData = other.RawData;          // set：换成新的 JlTuple 包装，与 raw 断链
	///   </code>
	///   <para><b>资源与坑</b>JlData 本身无公开构造器（构造均 internal），实际经由 JlPose 等派生类访问；JlData/JlPose 不实现 IDisposable，RawData 也无须手动释放。</para>
	/// </remarks>
	public JlTuple RawData
	{
		get
		{
			return tuple;
		}
		set
		{
			tuple = new JlTuple(value);
		}
	}

	/// <summary>
	///   按下标（从 0 起）取宿主内部元组的第 index 个分量，get 返回仍绑定该元组的 JlTupleElements 类型化视图（不是标量），set 把视图写回同一下标；对 JlPose 即那 7 个位姿分量之一。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>转发到内部元组：<c>get</c> 返回 <c>tuple[index]</c>，其类型是 <see cref="JlTupleElements"/>——一个仍绑定到底层元组的<b>类型化视图</b>，不是标量；<c>set</c> 把视图写回该下标处。</para>
	///   <para><b>约束或前提</b>取值需按类型口径读取（<c>.I</c> 32 位整数 / <c>.L</c> 64 位 / <c>.D</c> double / <c>.S</c> 字符串，或靠隐式转换直接用），元素类型与读取口径不匹配会抛 <c>JlTupleAccessException</c>。下标是对底层元组的定位（对 JlPose 即那 7 个位姿分量，0..6；各分量含义与排列 [待实测]）。</para>
	///   <para><b>与相邻算子的取舍</b>要整段搬走或整体比对用 <c>RawData</c>；只想按类型读某一个分量用本索引器更直接。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   double first = pose[0].D;      // 以 double 口径读第 0 个分量
	///   </code>
	///   <para><b>资源与坑</b>返回的视图与宿主元组联动，写 <c>pose[i]</c> 会改到宿主；越界下标按元组侧行为报错 [待实测]。JlData/JlPose 不实现 IDisposable。</para>
	/// </remarks>
	public JlTupleElements this[int index]
	{
		get
		{
			return tuple[index];
		}
		set
		{
			tuple[index] = value;
		}
	}

	internal JlData()
	{
		tuple = new JlTuple();
	}

	internal JlData(JlTuple t)
	{
		tuple = t;
	}

	internal JlData(JlData data)
	{
		tuple = data.tuple;
	}

	internal static JlTuple ConcatArray(JlData[] data)
	{
		JlTuple hTuple = new JlTuple();
		for (int i = 0; i < data.Length; i++)
		{
			hTuple = hTuple.TupleConcat(data[i].tuple);
		}
		return hTuple;
	}

	internal void UnpinTuple()
	{
		tuple.UnpinTuple();
	}

	internal void Store(IntPtr proc, int parIndex)
	{
		tuple.Store(proc, parIndex);
	}

	internal int Load(IntPtr proc, int parIndex, int err)
	{
		return tuple.Load(proc, parIndex, err);
	}

	internal int Load(IntPtr proc, int parIndex, JlTupleType type, int err)
	{
		return tuple.Load(proc, parIndex, type, err);
	}

	/// <summary>
	///   将 JlData 隐式转换为 JlTuple。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>等价于把宿主内部字段直接交出去：<c>return data.tuple;</c>，返回的是该 JlData 内部持有的<b>同一个 JlTuple 引用</b>（非副本、非重新包装），因此随后对返回元组的写会同步影响宿主，反之亦然。</para>
	///   <para><b>约束或前提</b>JlData 的构造器全部 <c>internal</c>，外部拿不到裸 JlData；这里的 data 实例须由公开派生类（如 <see cref="JlPose"/>、<c>JlHomMat2D</c>，皆 <c>: JlData</c>）产出，再以其基类 <c>JlData</c> 身份触发本运算符。</para>
	///   <para><b>与相邻能力的取舍</b>本方向只有 JlData→JlTuple 这一个隐式运算符；反向 JlTuple→JlData 无隐式转换，需显式走派生类构造（如 <c>new JlPose(tuple)</c>）。只想按类型读单个分量用索引器 <c>this[int]</c>，要整体搬走才用本转换或 <c>RawData</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlData data = new JlPose(0.1, 0.1, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlTuple tuple = data;   // 隐式转换：拿回宿主内部同一个 JlTuple 引用
	///   </code>
	///   <para><b>资源与坑</b>返回引用与宿主联动，无额外句柄需释放（JlData/JlPose 族不实现 IDisposable）；分量排列由原生侧决定，跨派生类型套用前需确认分量数与含义 [待实测]。</para>
	/// </remarks>
	public static implicit operator JlTuple(JlData data)
	{
		return data.tuple;
	}
}
