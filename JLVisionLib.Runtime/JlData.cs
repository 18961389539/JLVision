using System;

namespace JLVisionLib;

/// <summary>
///   非句柄的数据/值型包装基类（如 <c>JlHomMat2D</c>、<c>JlPose</c> 等）：内部以一个 <see cref="JlTuple"/> 承载分量，而非持有原生对象 key。它与句柄/对象包装一样实现 <see cref="IDisposable"/>，由本实例拥有内部元组；构造均为 internal，实际经公开派生类使用。
/// </summary>
public class JlData : IDisposable
{
	internal JlTuple tuple;

	/// <summary>
///   宿主数据的独立元组副本：get 返回新的 <see cref="JlTuple"/>，set 复制赋入值并释放旧存储；JlData 系拥有该元组并可调用 <see cref="Dispose()"/>。
	/// </summary>
	/// <remarks>
///   <para><b>功能说明</b>读：<c>get</c> 返回 <c>new JlTuple(tuple)</c>，调用方得到独立副本；写：先复制赋入值，再释放本实例原有元组，避免旧元组中的句柄悬挂。</para>
///   <para><b>约束或前提</b>get 返回的副本不会随宿主变化，调用方负责在使用后 Dispose；分量顺序由具体派生类型定义。<see cref="JlPose"/> 固定为 <c>TransX</c>、<c>TransY</c>、<c>TransZ</c>、<c>RotX</c>、<c>RotY</c>、<c>RotZ</c>、表示类型码。</para>
	///   <para><b>与相邻算子的取舍</b>只想按类型取单个分量用索引器 <c>this[int]</c>（返回 <c>JlTupleElements</c> 视图，可 <c>.D/.I/.S</c> 读）；要整体搬运/比对才动 RawData。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
///   using JlTuple raw = pose.RawData;       // get：拿到独立副本
	///   JlPose other = new JlPose(0.2, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   pose.RawData = other.RawData;          // set：换成新的 JlTuple 包装，与 raw 断链
	///   </code>
///   <para><b>资源与坑</b>JlData 本身无公开构造器（构造均 internal），实际经由 JlPose 等派生类访问；JlData/JlPose 实现 IDisposable，RawData 返回的副本也应释放。</para>
	/// </remarks>
	public JlTuple RawData
	{
		get => new JlTuple(tuple);
		set
		{
			JlTuple replacement = new JlTuple(value);
			JlTuple previous = tuple;
			tuple = replacement;
			if (!ReferenceEquals(previous, replacement))
			{
				previous.Dispose();
			}
		}
	}

	/// <summary>
	///   按下标（从 0 起）取宿主内部元组的第 index 个分量，get 返回仍绑定该元组的 JlTupleElements 类型化视图（不是标量），set 把视图写回同一下标；对 JlPose 即那 7 个位姿分量之一。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>转发到内部元组：<c>get</c> 返回 <c>tuple[index]</c>，其类型是 <see cref="JlTupleElements"/>——一个仍绑定到底层元组的<b>类型化视图</b>，不是标量；<c>set</c> 把视图写回该下标处。</para>
	///   <para><b>约束或前提</b>取值需按类型口径读取（<c>.I</c> 32 位整数 / <c>.L</c> 64 位 / <c>.D</c> double / <c>.S</c> 字符串，或靠隐式转换直接用），元素类型与读取口径不匹配会抛 <c>JlTupleAccessException</c>。对 <see cref="JlPose"/>，下标 0..6 依次对应 <c>TransX</c>、<c>TransY</c>、<c>TransZ</c>、<c>RotX</c>、<c>RotY</c>、<c>RotZ</c> 和表示类型码；其他派生类型按各自的元组布局解释。</para>
	///   <para><b>与相邻算子的取舍</b>要整段搬走或整体比对用 <c>RawData</c>；只想按类型读某一个分量用本索引器更直接。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   double first = pose[0].D;      // 以 double 口径读第 0 个分量
	///   </code>
	///   <para><b>资源与坑</b>返回的视图仍绑定宿主元组，写 <c>pose[i]</c> 会改到宿主；读取负数或超过当前长度的下标抛 <see cref="JlTupleAccessException"/>，写入非负的上界外下标会按元组规则扩容，新槽使用该存储类型的默认值。使用完 JlData/JlPose 应调用 <see cref="Dispose()"/>。</para>
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
		tuple = new JlTuple(t);
	}

	internal JlData(JlTuple t, bool takeOwnership)
	{
		tuple = takeOwnership ? t : new JlTuple(t);
	}

	internal JlData(JlData data)
	{
		tuple = new JlTuple(data.tuple);
	}

	internal static JlTuple ConcatArray(JlData[] data)
	{
		JlTuple hTuple = new JlTuple();
		for (int i = 0; i < data.Length; i++)
		{
			JlTuple next = hTuple.TupleConcat(data[i].tuple);
			hTuple.Dispose();
			hTuple = next;
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

	/// <summary>释放本实例拥有的元组，并将实例恢复为空数据壳；可重复调用。</summary>
	public void Dispose()
	{
		JlTuple previous = tuple;
		tuple = new JlTuple();
		if (!ReferenceEquals(previous, tuple))
		{
			previous.Dispose();
		}
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///   将 JlData 隐式转换为 JlTuple。
	/// </summary>
	/// <remarks>
///   <para><b>功能说明</b>等价于 <c>return data.RawData;</c>，返回的是 JlData 内部元组的<b>独立副本</b>，调用方可单独修改和释放。</para>
	///   <para><b>约束或前提</b>JlData 的构造器全部 <c>internal</c>，外部拿不到裸 JlData；这里的 data 实例须由公开派生类（如 <see cref="JlPose"/>、<c>JlHomMat2D</c>，皆 <c>: JlData</c>）产出，再以其基类 <c>JlData</c> 身份触发本运算符。</para>
	///   <para><b>与相邻能力的取舍</b>本方向只有 JlData→JlTuple 这一个隐式运算符；反向 JlTuple→JlData 无隐式转换，需显式走派生类构造（如 <c>new JlPose(tuple)</c>）。只想按类型读单个分量用索引器 <c>this[int]</c>，要整体搬走才用本转换或 <c>RawData</c>。</para>
	///   <para><b>用法</b></para>
///   <code>
///   JlData data = new JlPose(0.1, 0.1, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
///   using JlTuple tuple = data;   // 隐式转换：取得独立副本
///   </code>
///   <para><b>资源与坑</b>返回值是独立元组，使用后应 Dispose；元组只保留元素和值，不携带派生类型信息。把它交给其他派生类前，必须确认长度和布局与目标类型匹配；例如 <see cref="JlPose"/> 要求 7 个元素且末项为表示类型码。</para>
	/// </remarks>
	public static implicit operator JlTuple(JlData data)
	{
		return data.RawData;
	}
}
