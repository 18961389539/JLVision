using System;
using System.ComponentModel;

namespace JLVisionLib;

/// <summary>
///   持句柄包装对象的共享基类：以引用计数的原生 IntPtr 句柄（<c>UNDEF</c>=0 为未初始化哨兵）为核心，统一实现读写时的 <c>CopyHandle</c> 语义与 <c>Dispose</c>/终结器释放。与按原生 key 管理图标对象的 <see cref="JlObjectBase"/>、以及不持句柄的 <see cref="JlData"/> 并列，是 <c>JlHandle</c> 及各句柄派生类的共同父类。
/// </summary>
public class JlHandleBase : IDisposable
{
	/// <summary>空句柄哨兵值（即 <c>IntPtr.Zero</c>，无单位）：<c>JlHandleBase</c> 内部 <c>mHandle</c> 等于它即表示"未初始化"，它本身不是可送进算子的有效句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>托管侧用它统一表达"没有句柄"：基类拷贝构造与 <c>Handle</c> 赋值经 <c>SetHandleInternal</c>，遇到 <c>handle == UNDEF</c> 直接跳过原生 <c>CopyHandle</c>；<c>Dispose</c> 与 <c>ClearHandleInternal</c> 释放后把 <c>mHandle</c> 复位成它；<c>ToString()</c> 此时返回空串而不是 "H0"。</para>
	///   <para><b>约束或前提</b>基类 <c>Load</c> 反过来要求装载前的当前值<b>必须</b>是 UNDEF，否则抛 <c>"Undisposed handle instance when loading output parameter"</c>——所以 UNDEF 同时是"本壳可以接收算子输出"的前提状态，不只是"空"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle shell = new JlHandle();
	///   bool empty = shell.Handle == JlHandleBase.UNDEF;   // true：空壳，正好当装载接收位
	///   shell.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>把 UNDEF 值当输入送进算子，报错由 <c>PostCall</c> 统一抛托管异常，不会静默返回空结果；判断"有没有句柄"用 <c>IsInitialized()</c>，它查的是原生侧有效性而非只比 0。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static readonly IntPtr UNDEF = IntPtr.Zero;

	/// <summary>
	///   进程内共享的"空句柄"单例：一个句柄值为 UNDEF（<c>IntPtr.Zero</c>）的 <c>JlHandle</c> 对象，用作外部接口传参里"不给对象"的占位。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>静态初始化时 <c>new JlHandle()</c> 一次，落到基类 <c>UNDEF</c> 路径，纯托管建壳、不发任何原生调用；它的 <c>Handle</c> 值恒为 0（未被改写前），走隐式转换即可当 <c>IntPtr</c> 用。</para>
	///   <para><b>约束或前提</b>它是<b>共享单例</b>，不是每次新建的空壳：<c>DeserializeHandle</c> 之类原地改写会把所有人眼里的那个"空句柄"变成实对象；对它调 <c>Dispose()</c> 也会永久抑制该静态实例的终结器。要一个可自由改写的空壳请写 <c>new JlHandle()</c>。</para>
	///   <para><b>与相邻成员的取舍</b>只要裸值 0 就比 <c>JlHandleBase.UNDEF</c>（<c>IntPtr</c>，无对象开销）；只有形参类型是 <c>JlHandle</c>、需要交一个"空对象"时才用本字段。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle h = new JlHandle();
	///   bool sameAsNull = h.Handle == JlHandleBase.JlNULL.Handle;   // true：两者都是 UNDEF
	///   IntPtr id = JlHandleBase.JlNULL;                            // 走隐式转换得到 0
	///   h.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>不要对它调 <c>Dispose()</c> 或原地装载（见前提）；它不持有原生资源，本身也不需要被释放。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static readonly JlHandle JlNULL = new JlHandle();

	private IntPtr mHandle;

	private bool suppressedFinalization;

	/// <summary>
	///   本包装持有的原生句柄值（<c>IntPtr</c>，无单位）：get 直接回内部字段 <c>mHandle</c>（未初始化或已释放时为 UNDEF=0）；set 先清掉旧句柄再经原生 <c>CopyHandle</c> 另取一份引用。
	/// </summary>
	/// <remarks>
	///   <para><b>存活要求</b>调用方须确保传入的句柄在其被使用期间保持存活，未被提前释放。</para>
	///   <para><b>功能说明</b>赋值走 <c>SetHandleInternal(value, copy: true)</c>：先 <c>ClearHandleInternal()</c> 释放本壳原有那一份引用，再 <c>CopyHandle</c> 取新引用；<c>value == UNDEF</c> 时跳过拷贝，结果就是一个空壳。</para>
	///   <para><b>与相邻成员的取舍</b>要"多一个各自独立的容器"用 <c>JlHandle(JlHandle)</c> 拷贝构造，别手存 <c>Handle</c> 裸值：裸值不带引用计数所有权，源一 Dispose 你存的就成了悬垂值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle h = new JlHandle();
	///   IntPtr id = h.Handle;      // 未初始化 → 0（UNDEF）
	///   h.Handle = id;             // setter：先清旧值，零值不再拷引用
	///   h.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>给 <c>Handle</c> 赋新值会连带释放本壳原有句柄（原生 <c>ClearHandle</c>），不是简单换个数字；赋值后旧 <c>IntPtr</c> 值不要再继续使用。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public IntPtr Handle
	{
		get
		{
			return mHandle;
		}
		set
		{
			SetHandleInternal(value, copy: true);
		}
	}

	internal JlHandleBase()
		: this(UNDEF)
	{
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	internal JlHandleBase(IntPtr handle)
	{
		Handle = handle;
	}

	private void SetHandleInternal(IntPtr handle, bool copy)
	{
		ClearHandleInternal();
		if (suppressedFinalization)
		{
			suppressedFinalization = false;
			GC.ReRegisterForFinalize(this);
		}
		if (handle != UNDEF)
		{
			mHandle = (copy ? JlNativeApi.CopyHandle(handle) : handle);
		}
	}

	internal JlHandleBase(JlHandleBase handle)
	{
		SetHandleInternal(handle, copy: true);
	}

	/// <summary>
	///   问原生侧"本壳的句柄值当前是否有效"（转调 <c>HLIHandleIsValid</c>），返回 true 表示句柄可用；无参数、无单位，判定结果不由托管侧缓存。
	/// </summary>
	/// <remarks>
	///   <para><b>未初始化时机</b>用无参构造器创建、或调用 <c>Dispose()</c> 之后，句柄即处于未初始化状态。</para>
	///   <para><b>与 JlObjectBase 的同名差异</b>本方法真的进一次原生调用；<c>JlObjectBase.IsInitialized()</c> 只是 <c>key != UNDEF</c> 的纯托管比较。故被外部（其他语言接口、<c>JlOperatorSet</c> 模块）清掉的句柄，这里能报 false，图标对象那边却仍报 true。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle shell = new JlHandle();
	///   bool ready = shell.IsInitialized();   // false：句柄为 UNDEF
	///   shell.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>句柄值为悬垂值时该原生查询会不会抛异常、还是只回 false [待实测]；不要用 try/catch 之外的方式依赖它的返回做释放决策。</para>
	/// </remarks>
	public bool IsInitialized()
	{
		return JlNativeApi.HandleIsValid(mHandle);
	}

	/// <summary>
	///   终结器：若本壳未经显式 <c>Dispose()</c> 就进入回收，则在此释放其持有的那一份原生句柄引用（内部 <c>Dispose(disposing: false)</c>），异常被吞掉、不向外抛。
	/// </summary>
	~JlHandleBase()
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
		if (mHandle != UNDEF)
		{
			ClearHandleInternal();
			mHandle = UNDEF;
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
	///   释放本实例持有的那一份原生句柄引用：仅当 <c>mHandle</c> 不是 UNDEF 时才进原生 <c>ClearHandle</c> 并复位为 UNDEF，同时抑制终结器；无参数、无返回值，可重复调用。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>落到私有 <c>Dispose(bool)</c>：条件成立才 <c>ClearHandleInternal()</c>（原生 <c>HLIClearHandle</c>）+ <c>mHandle = UNDEF</c>；<c>disposing</c> 为真时 <c>GC.SuppressFinalize</c> 并置内部标记。终结器走同一段代码，只是不抑制自身。</para>
	///   <para><b>约束或前提</b><c>public virtual</c>：派生类（<c>JlHandle</c> 及各模型类）可覆写，覆写里若不调 base 就不会走这段释放；显式接口实现 <c>IDisposable.Dispose</c> 与直接调 <c>Dispose()</c> 是同一效果。已空壳时重复调用是安全的空操作。</para>
	///   <para><b>与相邻成员的取舍</b>只想清内容、保留句柄 id 继续用的话别用本方法，用 <c>JlHandle.ClearHandle()</c>；<c>InvalidateWithoutDispose()</c> 名义上是"作废不释放"，实现体却只是转调本方法，当前代码里两者没有差别。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle h = new JlHandle();
	///   h.Dispose();                                   // 已是 UNDEF → 不进原生，只抑制终结器
	///   bool empty = h.Handle == JlHandleBase.UNDEF;   // 释放后恒为 true
	///   </code>
	///   <para><b>资源与坑</b>每个 <c>JlHandle</c> 壳各自持有引用，谁建的壳谁 Dispose；用 <c>JlHandle(JlHandle)</c> 造的浅拷贝别名也要各自释放，漏一个就是一笔未归还的原生引用。</para>
	/// </remarks>
	public virtual void Dispose()
	{
		Dispose(disposing: true);
	}

	/// <summary>
	///   让本 C# 壳回到未初始化态而不再额外做一遍释放动作——但当前实现体就只有一行 <c>Dispose();</c>，因此句柄值同样被复位为 UNDEF（0），没有独立的"只作废不释放"路径。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>命名意图是"作废本壳、把原生句柄留给外部去清"，适用于句柄被 <c>JlOperatorSet</c> 模块或其他语言接口持有并稍后清理的场合；但方法体内没有任何与 <c>Dispose()</c> 不同的分支。</para>
	///   <para><b>与相邻成员的取舍</b>与 <c>Dispose()</c> 在当前代码里效果一致（实现体就是转调它）；与 <c>JlHandle.ClearHandle()</c> 不同——后者保留句柄 id 只清内容。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle h = new JlHandle();
	///   h.InvalidateWithoutDispose();
	///   bool empty = h.Handle == JlHandleBase.UNDEF;   // true：本壳已与原生对象脱钩
	///   </code>
	///   <para><b>资源与坑</b>标了 <c>EditorBrowsable(Never)</c>，属内部/互操作通道；调用后原生侧那份对象由外部负责清理，若外部并不清理则成泄漏 [待实测：原生 ClearHandle 是减引用还是彻底销毁内容]。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void InvalidateWithoutDispose()
	{
		Dispose();
	}

	internal void Store(IntPtr proc, int parIndex)
	{
		JlNativeApi.StoreH(proc, parIndex, mHandle);
	}

	internal int Load(IntPtr proc, int parIndex, int err)
	{
		if (mHandle != UNDEF)
		{
			throw new JlException("Undisposed handle instance when loading output parameter");
		}
		if (JlNativeApi.IsFailure(err))
		{
			return err;
		}
		err = JlNativeApi.LoadH(proc, parIndex, err, out var handleValue);
		SetHandleInternal(handleValue.Handle, copy: true);
		handleValue.Dispose();
		return err;
	}

	/// <summary>
	///   释放本壳自身持有的那一份原生句柄引用：仅当 <c>mHandle</c> 非 UNDEF 时调原生 <c>ClearHandle</c> 并复位为 UNDEF；供 <c>Handle</c> 赋值、<c>Dispose</c> 等内部路径复用，<c>protected virtual</c> 供派生类覆写。
	/// </summary>
	protected virtual void ClearHandleInternal()
	{
		if (mHandle != UNDEF)
		{
			JlNativeApi.ClearHandle(mHandle);
			mHandle = UNDEF;
		}
	}

	/// <summary>
	///   把句柄值格式化成 <c>"H"</c> + 十进制转大写十六进制的短串（仅调试用）；句柄为 UNDEF 时返回空串 <c>""</c>，而不是 "H0"。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>实现是 <c>mHandle.ToInt64().ToString("X")</c> 前缀 "H"：无前导零、无固定宽度，同一句柄值得到同一串，可用来肉眼判断两个壳是否指向同一 id。</para>
	///   <para><b>约束或前提</b>两个空壳的 <c>ToString()</c> 都是空串，相等不代表它们"该是同一个对象"；它也不是语义类型或序列化标识，别拿它做分支判据。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle a = new JlHandle();
	///   JlHandle b = new JlHandle();
	///   string sa = a.ToString();                      // 空壳 → ""
	///   bool sameTag = a.ToString() == b.ToString();   // 只说明两个都是空壳
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>纯托管格式化，不进原生、不改变句柄状态。</para>
	/// </remarks>
	public override string ToString()
	{
		if (mHandle == UNDEF)
		{
			return "";
		}
		return "H" + mHandle.ToInt64().ToString("X");
	}

	/// <summary>
	///   校验所包装对象的语义类型名：句柄非 UNDEF 时向原生查询其实际语义类型并与 <c>sem_type</c> 比较，不一致即抛 <c>JlException("Invalid handle instance passed")</c>；句柄为 UNDEF（空壳）时跳过校验。
	/// </summary>
	protected internal void AssertSemType(string sem_type)
	{
		if (mHandle != UNDEF)
		{
			string handleSemType = JlNativeApi.GetHandleSemType(mHandle);
			if (!sem_type.Equals(handleSemType))
			{
				throw new JlException("Invalid handle instance passed");
			}
		}
		GC.KeepAlive(this);
	}

	/// <summary>
	///   把一组 <c>JlHandleBase</c> 打包成 HANDLE 型 <c>JlTuple</c>（每个元素各克隆一份引用入组），返回新元组；纯托管组装，不调任何原生算子。
	/// </summary>
	/// <param name="handles">待打包的句柄数组，元素顺序即元组下标顺序；数组本身可为空。</param>
	/// <returns>新建的 <c>JlTuple</c>，元素为各句柄的引用拷贝；元组需 Dispose（只释放组内副本），用完前原句柄须保持存活。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>实现是 <c>new JlTuple(handles as JlHandle[])</c>：走 <c>JlTuple(params JlHandle[])</c>，对每个元素做 <c>new JlHandle(h[i])</c> 拷贝（<c>copy: true</c>），顺序与入参数组一一对应。</para>
	///   <para><b>关键前提</b>转换用的是 <c>as</c>：运行时类型不是 <c>JlHandle[]</c>（例如数组按 <c>new JlHandleBase[n]</c> 创建，或元素是别的基类派生实例）时 <c>as</c> 得到 null，再喂给 <c>JlHandle[]</c> 形参，后果是空引用类异常 [待实测：具体异常形态]。要避开就用 <c>new JlHandle[] { ... }</c> 字面量（协变可直接当 <c>JlHandleBase[]</c> 传）。数组里含 null 元素时的行为也未在托管层特判 [待实测]。</para>
	///   <para><b>与相邻成员的取舍</b>数值/字符串数组的拼接走 <c>JlTuple</c> 自身的构造或 <c>TupleConcat</c>；<c>JlData.ConcatArray</c>（internal）拼的是元组内容而非句柄引用，两者不可互换。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle a = new JlHandle();
	///   JlHandle b = new JlHandle();
	///   using JlTuple group = JlHandleBase.ConcatArray(new JlHandle[] { a, b });
	///   int n = group.Length;   // 2，顺序与入参数组一致
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>元组释放的只是自己的那批引用副本，原句柄仍由创建方负责 <c>Dispose()</c>，两边都要收尾；<c>JlTuple</c> 实现了 <c>IDisposable</c>，故 <c>using</c> 合法。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static JlTuple ConcatArray(JlHandleBase[] handles)
	{
		return new JlTuple(handles as JlHandle[]);
	}

	/// <summary>
	///   把句柄对象隐式转成其原生句柄值（<c>IntPtr</c>，无单位）：取的是内部 <c>mHandle</c>，入参为 null 时返回 UNDEF（0）而不抛空引用。
	/// </summary>
	/// <remarks>
	///   <para><b>存活要求</b>调用方须确保传入对象在其被使用期间保持存活，未被提前释放。</para>
	///   <para><b>功能说明</b>实现是 <c>handle?.mHandle ?? UNDEF</c>，与 <c>JlHandleBase.Handle</c> 同值，只是能直接喂给收 <c>IntPtr</c> 的互操作签名。</para>
	///   <para><b>与相邻成员的取舍</b>要"再多一个托管容器"用 <c>JlHandle(JlHandle)</c> 拷贝构造（会走原生 <c>CopyHandle</c> 增引用）；本转换只搬数字，不带来任何所有权。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle h = new JlHandle();
	///   IntPtr id = h;                                // 隐式转换，等价 h.Handle
	///   bool empty = id == JlHandleBase.UNDEF;        // 空壳 → true
	///   h.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>源对象一旦 <c>Dispose()</c>，先前转出的裸值即悬垂；长期保存请把源对象一起存活，别只留 <c>IntPtr</c>。</para>
	/// </remarks>
	public static implicit operator IntPtr(JlHandleBase handle)
	{
		return handle?.mHandle ?? UNDEF;
	}
}
