using System;
using System.ComponentModel;

namespace JLVisionLib;

/// <summary>
///   图标对象包装（图像/区域/XLD 及各模型类）的共同基类：以原生对象 key（<c>IntPtr</c>，<c>UNDEF</c>=0 为未初始化哨兵）为核心，提供 Key/CopyKey、TransferOwnership 及 <c>Dispose</c>/终结器释放。与以引用计数句柄为核心的 <see cref="JlHandleBase"/> 是两套通道，本类实现 <c>IDisposable</c>，而数据类的 <see cref="JlData"/> 不实现。
/// </summary>
public class JlObjectBase : IDisposable
{
	/// <summary>空 key 哨兵值（即 <c>IntPtr.Zero</c>，无单位）：图标对象的内部字段 <c>key</c> 等于它即表示未初始化或已释放，不是可送进算子的有效对象标识。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>基类构造把传进来的 key 一律归一到这条线：<c>copy</c> 为真且 key 既非 UNDEF 又非内部占位值 <c>UNDEF2</c>（<c>IntPtr(1)</c>）时才走原生 <c>CopyObject</c>，否则直接赋值，且 <c>key == UNDEF2</c> 会被写成 UNDEF。释放路径 <c>Dispose(bool)</c> 同样把 key 复位成它。</para>
	///   <para><b>约束或前提</b>基类 <c>Load</c> 要求装载前当前 key <b>必须</b>是 UNDEF，否则抛 <c>"Undisposed object instance when loading output parameter"</c>——UNDEF 既是"空"也是"可接收算子输出"的前提。而 <c>IsInitialized()</c> 只是与本值比较，不查原生。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 8, 8);
	///   bool real = img.Key != JlObjectBase.UNDEF;   // true：真拿到了 key
	///   img.Dispose();
	///   bool gone = img.Key == JlObjectBase.UNDEF;   // true：释放后复位
	///   </code>
	///   <para><b>资源与坑</b>图标对象（<c>JlImage</c>/<c>JlRegion</c>/<c>JlXLD</c> 及各模型类）都走这条 key 通道，与 <c>JlHandleBase</c> 的句柄通道是两套；派生的图标对象需自行 <c>Dispose()</c>，不等 key 变 UNDEF 内容就已经被原生 <c>ClearObject</c> 掉了。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static readonly IntPtr UNDEF = IntPtr.Zero;

	internal static readonly IntPtr UNDEF2 = new IntPtr(1);

	internal IntPtr key = UNDEF;

	private bool suppressedFinalization;

	/// <summary>
	///   本图标对象的原生 key（<c>IntPtr</c>，无单位）：getter 直接回内部字段 <c>key</c>，不做任何原生拷贝，未初始化或已释放时为 UNDEF（0）。
	/// </summary>
	/// <remarks>
	///   <para><b>存活要求</b>调用方须确保对象在其被使用期间保持存活，未被提前释放。</para>
	///   <para><b>功能说明</b>就是字段转发（<c>public IntPtr Key =&gt; key;</c>），因此读它零开销、也不会增引用计数；它随本实例的 <c>Dispose()</c> 一起变回 UNDEF。</para>
	///   <para><b>与相邻成员的取舍</b>要把 key 交给另一个语言接口或本对象之外长期使用，用 <c>CopyKey()</c> 另取一份引用；只是本程序集内传参就用 <c>Store</c> 通道，别手传裸值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 8, 8);
	///   IntPtr k = img.Key;                        // 裸值，不增引用计数
	///   img.Dispose();
	///   bool looksAlive = k != JlObjectBase.UNDEF; // true：值仍是旧数，但对象已被 ClearObject
	///   </code>
	///   <para><b>资源与坑</b>本属性标了 <c>EditorBrowsable(Never)</c>，属互操作通道；把 <c>Dispose()</c> 后残留的旧 key 数值再喂给算子是最难查的一类错——它非 0，看着像有效句柄。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public IntPtr Key => key;

	internal JlObjectBase()
		: this(UNDEF, copy: false)
	{
	}

	internal JlObjectBase(IntPtr key, bool copy)
	{
		if (copy && key != UNDEF && key != UNDEF2)
		{
			this.key = JlNativeApi.CopyObject(key);
		}
		else
		{
			this.key = ((key == UNDEF2) ? UNDEF : key);
		}
	}

	internal JlObjectBase(JlObjectBase obj)
		: this(obj.key, copy: true)
	{
		GC.KeepAlive(obj);
	}

	/// <summary>
	///   纯托管判断本图标对象是否持有 key：<c>key != UNDEF</c> 即返回 true；不进原生调用、不会抛异常，也不校验该 key 在原生侧是否仍然存在。
	/// </summary>
	/// <remarks>
	///   <para><b>未初始化时机</b>用无参构造器创建对象、或调用 <c>Dispose()</c> 之后，对象即处于未初始化状态（key 为 UNDEF）。</para>
	///   <para><b>与 JlHandleBase 的同名差异</b>句柄基类的同名方法会转调原生 <c>HLIHandleIsValid</c> 真查有效性；本类只比托管字段。故 key 被外部（其他语言接口）清掉后这里仍报 true，别把它当"对象还活着"的证据。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 8, 8);
	///   bool before = img.IsInitialized();   // true
	///   img.Dispose();
	///   bool after = img.IsInitialized();    // false：key 已复位为 UNDEF
	///   </code>
	///   <para><b>资源与坑</b>无参构造出的实例为 false；<c>TransferOwnership(source)</c> 之后 <c>source</c> 也变 false（key 被搬走）。</para>
	/// </remarks>
	public bool IsInitialized()
	{
		return key != UNDEF;
	}

	/// <summary>
	///   经原生 <c>CopyObject</c> 为当前 key 另取一份引用并返回裸 <c>IntPtr</c>（不是新对象）：本托管实例与返回值互不影响，任一侧释放都不会让另一侧失效。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>实现是 <c>IntPtr result = JlNativeApi.CopyObject(key); GC.KeepAlive(this); return result;</c>——只加引用，不新建 C# 包装，故返回后原生对象有两个独立持有者。</para>
	///   <para><b>约束或前提</b>返回的 key 由调用方负责释放，而本 .NET 接口没有释放裸 key 的入口（该方法自身的英文说明亦承认这点）；当前 key 为 UNDEF 时把 0 传给原生 <c>CopyObject</c> 的行为 [待实测]。</para>
	///   <para><b>与相邻成员的取舍</b>只是想在托管侧多一个独立可 Dispose 的容器，用拷贝构造（如 <c>JlImage(img)</c>）或 <c>Clone()</c>；要跨语言/跨进程交接才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 8, 8);
	///   IntPtr alias = img.CopyKey();                  // 另一份引用
	///   img.Dispose();                                 // 本实例释放，不影响 alias 那份
	///   bool hasAlias = alias != JlObjectBase.UNDEF;   // true
	///   </code>
	///   <para><b>资源与坑</b>每次调用都是原生侧一次引用分配，循环里滥用等同于泄漏（托管 GC 不会替你回收裸 key）；方法标了 <c>EditorBrowsable(Never)</c>。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public IntPtr CopyKey()
	{
		IntPtr result = JlNativeApi.CopyObject(key);
		GC.KeepAlive(this);
		return result;
	}

	/// <summary>
	///   把 <c>source</c> 的 key 原地搬进本实例：先 <c>Dispose()</c> 掉本实例原有对象，再取走 <c>source.key</c> 并把 <c>source</c> 复位为 UNDEF。纯托管字段交接，不发任何原生调用。
	/// </summary>
	/// <param name="source">交出 key 的图标对象。调用后它变成空壳（<c>IsInitialized()</c> 为 false）；传 <c>this</c> 时整个方法直接跳过，什么都不做。</param>
	/// <remarks>
	///   <para><b>功能说明</b>实现顺序是 <c>if (source != this) { Dispose(); if (source != null) { key = source.key; source.key = UNDEF; suppressedFinalization = false; GC.ReRegisterForFinalize(this); } }</c>：接收方重新登记终结器，兜住"先前已 Dispose 过"的实例。</para>
	///   <para><b>约束或前提</b>本实例原来的对象会被真释放（<c>ClearObject</c>）且不可恢复——它不是"给空壳填内容"的方法，别拿它覆盖还想留着的对象。<c>source</c> 交完 key 后自身变空壳，再 <c>Dispose()</c> 是空操作，因此不会双重释放同一 key。</para>
	///   <para><b>坑</b><c>null</c> 的判断在 <c>source != this</c> 之后：传 <c>null</c> 时条件成立，结果是本实例被静默清空而什么都没交接；基类的 <c>Load</c> 才有"当前必须为 UNDEF"的抛异常检查，本方法不检查自身状态。</para>
	///   <para><b>与相邻成员的取舍</b>要两份互不相干的副本用 <c>Clone()</c> 或拷贝构造；只要一个额外裸引用（源对象继续自己管自己）用 <c>CopyKey()</c>；确认不再需要 <c>source</c> 且要省一次原生拷贝时才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage donor = new JlImage("byte", 8, 8);
	///   JlImage holder = new JlImage("byte", 8, 8);
	///   holder.TransferOwnership(donor);          // holder 原有的 8×8 图被释放，改持 donor 的 key
	///   bool donorEmpty = !donor.IsInitialized(); // true：donor 已成空壳
	///   holder.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>方法标了 <c>EditorBrowsable(Never)</c>，属内部/移动语义通道；一次调用净释放的是<b>接收方</b>原对象，交出方只是换了个主人，原生侧不会多出引用。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void TransferOwnership(JlObjectBase source)
	{
		if (source != this)
		{
			Dispose();
			if (source != null)
			{
				key = source.key;
				source.key = UNDEF;
				suppressedFinalization = false;
				GC.ReRegisterForFinalize(this);
			}
		}
	}

	/// <summary>
	///   终结器：若本图标对象未经显式 <c>Dispose()</c> 就进入回收，则在此对残留 key 调原生 <c>ClearObject</c> 释放（内部 <c>Dispose(disposing: false)</c>），异常被吞掉、不向外抛。
	/// </summary>
	~JlObjectBase()
	{
		try
		{
			Dispose(disposing: false);
		}
		catch (Exception)
		{
		}
	}

	private void Dispose(bool disposing)
	{
		if (key != UNDEF)
		{
			JlNativeApi.ClearObject(key);
			key = UNDEF;
		}
		if (disposing)
		{
			GC.SuppressFinalize(this);
			suppressedFinalization = true;
		}
		GC.KeepAlive(this);
	}

	void IDisposable.Dispose()
	{
		Dispose(disposing: true);
	}

	/// <summary>
	///   释放本图标对象：仅当 <c>key</c> 不是 UNDEF 时调原生 <c>ClearObject</c> 并把 <c>key</c> 复位为 UNDEF，同时抑制终结器；无参数、无返回值，重复调用安全。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>落到私有 <c>Dispose(bool)</c>：条件成立才进一次原生 <c>ClearObject</c>；<c>disposing</c> 为真时 <c>GC.SuppressFinalize</c> 并置内部标记。终结器 <c>~JlObjectBase()</c> 走同一段代码并吞掉所有异常，所以漏掉的释放最终仍会回收 key。</para>
	///   <para><b>约束或前提</b>与句柄基类不同，这里的释放条件是纯托管的 <c>key != UNDEF</c>：key 若已被外部清过，本方法仍会对该值调一次 <c>ClearObject</c> [待实测：原生对悬垂 key 的反应]。</para>
	///   <para><b>与相邻成员的取舍</b>想保住原生对象、只多拿一个裸引用，用 <c>CopyKey()</c>；想把内容交给另一个实例托管而不释放，用 <c>TransferOwnership(source)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 8, 8);
	///   img.Dispose();                            // ClearObject + key 复位 UNDEF
	///   bool gone = !img.IsInitialized();         // true
	///   img.Dispose();                            // 幂等：key 已是 UNDEF，不再进原生
	///   </code>
	///   <para><b>资源与坑</b>所有派生图标对象（<c>JlImage</c>/<c>JlRegion</c>/<c>JlXLD</c>/<c>JlMeasure</c> 及各模型类）都要自行 <c>Dispose()</c> 或用 <c>using</c>；<c>JlData</c> 系（<c>JlHomMat2D</c>、<c>JlPose</c>）不在此列，它们不实现 <c>IDisposable</c>。</para>
	/// </remarks>
	public virtual void Dispose()
	{
		Dispose(disposing: true);
	}

	internal void Store(IntPtr proc, int parIndex)
	{
		JlNativeApi.JlCkP(proc, JlNativeApi.SetInputObject(proc, parIndex, key));
	}

	internal int Load(IntPtr proc, int parIndex, int err)
	{
		if (key != UNDEF)
		{
			throw new JlException("Undisposed object instance when loading output parameter");
		}
		if (JlNativeApi.IsFailure(err))
		{
			return err;
		}
		err = JlNativeApi.GetOutputObject(proc, parIndex, out key);
		if (suppressedFinalization)
		{
			suppressedFinalization = false;
			GC.ReRegisterForFinalize(this);
		}
		return err;
	}
}
