using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>Represents an instance of an iconic object(-array). Base class for images, regions and XLDs</summary>
[Serializable]
public class JlObject : JlObjectBase, ISerializable, ICloneable
{
	/// <summary>按 HALCON 序号取出对象元组中的一个（或多个）元素，等价于 <see cref="SelectObj(JlTuple)"/>。</summary>
	/// <param name="index">要取出的对象序号，1-based（1 指向元组首个对象）。可传入 int、int[] 或 JlTuple（int 经隐式转换得到单元素元组）。越界序号由原生层报错。</param>
	/// <returns>由被选中对象组成的新 JlObject 句柄，与原对象独立，需自行 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 select_obj（id 572）。把元组里位于 <paramref name="index"/> 处的对象抽出来，返回一个新的对象句柄。</para>
	///   <para><b>约束或前提</b>序号从 1 开始，不是 C# 的 0；传入 0 或超过 <see cref="CountObj()"/> 的值属非法请求。索引参数走原生 select_obj，因此它选的是"元组内位置"，与对象内容无关——上游 <c>Connection()</c> 等操作产生的顺序若不固定，这里按位置取会静默错取。</para>
	///   <para><b>与相邻算子的取舍</b>只要一个元素时用 <see cref="SelectObj(int)"/>（标量重载，直接把 int 写进参数、无固定元组开销）；本索引器接收 JlTuple，适合一次取多下标。想"复制出去独立持有"用 <see cref="CopyObj"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion r2 = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlObject pair = r1.ConcatObj(r2);
	///   JlObject first = pair[1];
	///   first.Dispose();
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是新句柄，用完要 Dispose；它不共享被索引对象的引用计数，释放 <c>pair</c> 后 <c>first</c> 仍各自独立存在直到自己 Dispose。被索引对象必须已初始化，否则 <c>key</c> 为 UNDEF 时原生调用报错。</para>
	/// </remarks>
	public JlObject this[JlTuple index] => SelectObj(index);

	/// <summary>创建一个句柄为 UNDEF（未初始化）的空 JlObject，供后续原地装载输出使用。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>转调基类 <c>JlObjectBase(UNDEF, copy: false)</c>，即内部 <c>key = IntPtr.Zero</c>，不申请任何原生对象。它本身不调用任何底层算子。</para>
	///   <para><b>约束或前提</b>未初始化的句柄不能直接当输入传给原生算子（<see cref="JlObjectBase.IsInitialized()"/> 返回 false）。它主要用于两类场景：一是像 <see cref="Deserialize(Stream)"/> 那样先建空壳再 <c>DeserializeObject</c> 填内容；二是 <see cref="GenEmptyObj()"/>、<see cref="ReadObject(string)"/>、<see cref="IntegerToObj(JlTuple)"/> 这类"原地装载输出"的方法，要求句柄当前必须是 UNDEF。</para>
	///   <para><b>与相邻算子的取舍</b>想要引用计数式浅拷贝用 <see cref="JlObject(JlObject)"/>；想要独立深拷贝用 <see cref="Clone()"/>；本构造只是拿到一个"待填充"的空对象。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObject obj = new JlObject();
	///   obj.GenEmptyObj();          // 原地装入空对象元组（id 602），句柄随之变为有效
	///   int n = obj.CountObj();     // 空元组的对象数为 0
	///   obj.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>基类 <see cref="IDisposable"/> 与终结器保证 <c>key != UNDEF</c> 时才 <c>ClearObject</c>，故对刚 new 出来、尚未装载的对象 Dispose 是安全的空操作。</para>
	/// </remarks>
	public JlObject()
		: base(JlObjectBase.UNDEF, copy: false)
	{
	}

	/// <summary>用给定的原生句柄 <paramref name="key"/> 构造 JlObject，默认以"引用计数拷贝"方式接管。</summary>
	/// <param name="key">一个已存在的原生对象句柄（HALCON handle）。null/IntPtr.Zero 视为未初始化。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转调 <see cref="JlObject(IntPtr, bool)"/> 且 <c>copy = true</c>，基类会执行 <c>JlNativeApi.CopyObject(key)</c>，即让新对象与 <paramref name="key"/> 共享底层数据、各自持一份引用计数。</para>
	///   <para><b>约束或前提</b>这是内部/互操作入口（<c>[EditorBrowsable(Never)]</c>）。传入的 key 必须是本进程内有效的图标对象句柄，否则原生调用报错。</para>
	///   <para><b>与相邻算子的取舍</b>引用计数拷贝 ≠ 深拷贝：二者指向同一底层对象，改一处影响另一处的可见数据，除非某一方再次触发真正的拷贝语义。</para>
	///   <para><b>资源与坑</b><c>GC.KeepAlive(this)</c> 保证在原生句柄装载完成前本对象不被回收；调用方持有的原 key 与其自身释放时机互不影响。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObject(IntPtr key)
		: this(key, copy: true)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>用给定句柄构造 JlObject，由 <paramref name="copy"/> 决定是"引用计数拷贝"还是"直接接管裸句柄"。</summary>
	/// <param name="key">原生对象句柄；<c>IntPtr.Zero</c>（UNDEF）表示未初始化，<c>IntPtr(1)</c>（UNDEF2）会被规整为 UNDEF。</param>
	/// <param name="copy">true：对 key 执行 <c>CopyObject</c>，得到共享底层数据、独立引用计数的新句柄；false：直接把 key 作为本对象的句柄接管（不增加引用）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>基类 <c>JlObjectBase(IntPtr, bool)</c> 的透传入口，是所有"接管/拷贝"路径的公共落点。</para>
	///   <para><b>约束或前提</b>仅在 <c>copy &amp;&amp; key != UNDEF &amp;&amp; key != UNDEF2</c> 时才真正 CopyObject；否则按原样存储（UNDEF2 归零为 UNDEF）。当 <c>copy = false</c> 接管裸句柄时，本对象的 Dispose 会负责清该句柄，调用方不得再重复释放。</para>
	///   <para><b>与相邻算子的取舍</b>从 C# 对象复制请用 <see cref="JlObject(JlObject)"/>；从序列化流恢复用 <see cref="Deserialize(Stream)"/>。</para>
	///   <para><b>资源与坑</b>内部/互操作用（<c>[EditorBrowsable(Never)]</c>）。接管语义下所有权转移到本对象，误用会导致双重释放或句柄泄漏。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObject(IntPtr key, bool copy)
		: base(key, copy)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>从另一个 JlObject 复制出新对象：这是一次引用计数拷贝（clone），非深拷贝。</summary>
	/// <param name="obj">被复制的源 JlObject；其内部句柄会被 <c>CopyObject</c> 引用一次。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转调 <c>JlObjectBase(JlObjectBase)</c>，即以 <c>copy = true</c> 对 <c>obj.key</c> 执行 <c>CopyObject</c>，得到共享底层数据、独立引用计数的新句柄。</para>
	///   <para><b>约束或前提</b>源对象 <paramref name="obj"/> 必须非 null 且已初始化；构造期间基类用 <c>GC.KeepAlive(obj)</c> 防止源句柄被提前回收。</para>
	///   <para><b>与相邻算子的取舍</b>需要彼此独立、改一个不影响另一个时改用 <see cref="Clone()"/>（序列化往返的深拷贝）；只需轻量共享引用时用本拷贝构造。</para>
	///   <para><b>资源与坑</b>两者各自 Dispose 时只减一次引用计数，底层对象在最后一个持有者释放后才真正回收。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObject(JlObject obj)
		: base(obj)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	private void AssertObjectClass()
	{
	}

	/// <summary>把原生过程的输出对象参数装载进一个全新的 JlObject（"返回新句柄"路径，对应 LoadNew 语义）。</summary>
	/// <param name="proc">当前原生过程句柄。</param>
	/// <param name="parIndex">输出对象在原生侧的参数序号。</param>
	/// <param name="err">调用返回码，透传给 <c>Load</c> 判断是否失败。</param>
	/// <param name="obj">输出参数：装载成功时返回一个持有新句柄的 JlObject。</param>
	/// <returns>原生装载的结果码（0 表示成功）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>先 <c>new JlObject(UNDEF)</c> 建空壳，再调实例的 <c>Load</c> 从原生输出取回句柄——因此与"原地改写"系（<see cref="GenEmptyObj()"/> 等先 Dispose 再 Load 到 this）相对，本方法产出的对象所有权归调用方。</para>
	///   <para><b>约束或前提</b>基类 <c>Load</c> 要求目标句柄必须为 UNDEF，这里用刚 new 的空对象天然满足；若 <paramref name="err"/> 已是失败码则不覆盖句柄。</para>
	///   <para><b>资源与坑</b>调用方拿到的 <paramref name="obj"/> 是独立新句柄，用完须 <see cref="JlObjectBase.Dispose()"/>。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadNew(IntPtr proc, int parIndex, int err, out JlObject obj)
	{
		obj = new JlObject(JlObjectBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeObject();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>二进制反序列化构造器：从 <paramref name="info"/> 里名为 "data" 的字节块重建出对象句柄。</summary>
	/// <param name="info">序列化载体，须包含由 <c>GetObjectData</c> 写入的 "data"（<c>byte[]</c>）。</param>
	/// <param name="context">流式上下文，本实现不使用。</param>
	/// <remarks>
	///   <para><b>功能说明</b>读出 "data" 字节块后调用 <see cref="DeserializeObject(byte[])"/>，走原生 <c>deserialize_obj</c>（id 1568）把字节流还原成一个新的对象句柄。</para>
	///   <para><b>约束或前提</b>字节块必须来自同一序列化族（<see cref="SerializeObject"/>/<see cref="Serialize(Stream)"/>），格式与版本需匹配，否则原生报错。</para>
	///   <para><b>资源与坑</b>这是 <see cref="ISerializable"/> 契约所需（供 BinaryFormatter 等使用），构造完本对象即持有还原后的句柄，需正常 Dispose。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObject(SerializationInfo info, StreamingContext context)
	{
		DeserializeObject((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把当前对象序列化为 Vision 二进制格式并写入 <paramref name="stream"/>，不改变本对象句柄。</summary>
	/// <param name="stream">可写的目标流。方法把 <see cref="SerializeObject"/> 得到的字节块写入其中，流的位置由 <c>JlSerializationBuffer</c> 管理。</param>
	/// <remarks>
	///   <para><b>功能说明</b>先调用 <see cref="SerializeObject"/>（原生 <c>serialize_obj</c>，id 1569）拿到内存序列块，再原样落到流上。对象本身仍归本实例持有，序列化不消费句柄。</para>
	///   <para><b>约束或前提</b>对象必须已初始化且非空内容才有意义；目标流必须支持写入。与 <see cref="WriteObject(string)"/> 的区别：后者按文件名落盘成独立文件，本方法只把字节写进任意 <see cref="Stream"/>（内存流、网络流、自定义容器）。</para>
	///   <para><b>与相邻算子的取舍</b>跨进程/网络传对象用本方法或 <see cref="SerializeObject"/>；落文件存档用 <see cref="WriteObject(string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage image = new JlImage("byte", 64, 64);
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       image.Serialize(ms);
	///       ms.Position = 0;
	///       JlObject back = JlObject.Deserialize(ms);
	///       back.Dispose();
	///   }
	///   image.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>读回时 <see cref="Deserialize(Stream)"/> 会新建一个独立句柄，需单独 Dispose；<paramref name="stream"/> 的关闭由调用方负责。</para>
	/// </remarks>
	public void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeObject(), stream);
	}

	/// <summary>从 <paramref name="stream"/> 读取 Vision 二进制格式，还原出一个全新的 JlObject 句柄并返回。</summary>
	/// <param name="stream">可读的源流，其内容须由 <see cref="Serialize(Stream)"/> 写入（同族格式）。</param>
	/// <returns>持有反序列化后新句柄的 JlObject；调用方负责 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>内部 <c>new JlObject()</c> 建空壳，读出流字节后调 <see cref="DeserializeObject(byte[])"/>（原生 <c>deserialize_obj</c>，id 1568）把句柄装载进这个新对象——属于"返回新句柄"，不影响任何已有对象。</para>
	///   <para><b>约束或前提</b>返回类型为基类 JlObject，还原出的运行时类别取决于流中原始对象；如需强类型可再判 <see cref="GetObjClass"/> 或按已知类型包装。</para>
	///   <para><b>与相邻算子的取舍</b>从内存字节还原用 <see cref="DeserializeObject(byte[])"/>（原地）；从文件还原用 <see cref="ReadObject(string)"/>（原地改写 this）；本方法是"流 → 新对象"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       JlImage src = new JlImage("byte", 32, 32);
	///       src.Serialize(ms);
	///       ms.Position = 0;
	///       JlObject restored = JlObject.Deserialize(ms);
	///       restored.Dispose();
	///       src.Dispose();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回句柄独立于流；关闭流不会使返回对象失效，但两者都需各自释放。</para>
	/// </remarks>
	public static JlObject Deserialize(Stream stream)
	{
		JlObject hObject = new JlObject();
		hObject.DeserializeObject(JlSerializationBuffer.ReadFromStream(stream));
		return hObject;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>通过序列化往返生成一个与原对象完全独立的深拷贝。</summary>
	/// <returns>内容与本对象相同、但句柄独立的新 JlObject；调用方负责 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>实现 <see cref="ICloneable.Clone"/> 的公开版本：先 <see cref="SerializeObject"/> 取字节块，再 <c>new JlObject()</c> + <see cref="DeserializeObject(byte[])"/> 还原，等价于"序列化→反序列化"的深拷贝。</para>
	///   <para><b>约束或前提</b>原对象必须可序列化（已初始化）。深拷贝不共享底层内存，之后对副本的任何修改都不影响原对象。</para>
	///   <para><b>与相邻算子的取舍</b>只需共享底层、省内存省时间的浅拷贝用 <see cref="JlObject(JlObject)"/>（引用计数 clone）或 <see cref="CopyObj"/>；要真正独立、可各自改写时用本方法。深拷贝代价高于引用计数拷贝。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage image = new JlImage("byte", 64, 64);
	///   JlObject copy = image.Clone();
	///   copy.Dispose();
	///   image.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄独立于原对象，二者分别 Dispose；不要因"看起来是同一个"而漏放其一。</para>
	/// </remarks>
	public JlObject Clone()
	{
		byte[] data = SerializeObject();
		JlObject obj = new JlObject();
		obj.DeserializeObject(data);
		return obj;
	}

	/// <summary>对两个对象元组求差：返回属于本对象元组、但不属于 <paramref name="objectsSub"/> 的对象。</summary>
	/// <param name="objectsSub">被减对象元组（第二路输入）。</param>
	/// <returns>结果对象元组，是一个新句柄，需 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 obj_diff（id 558）。以对象句柄为单位做集合差：<c>this</c> 为被减集，<paramref name="objectsSub"/> 为减集，输出保留 <c>this</c> 中未被 <paramref name="objectsSub"/> 命中的元素及其原顺序。</para>
	///   <para><b>约束或前提</b>判等依据是句柄身份/相等性，与像素或几何内容是否"看起来相同"无关——两个内容相同但独立生成的对象不算同一元素。两路输入必须同族且已初始化。</para>
	///   <para><b>与相邻算子的取舍</b>求并集用 <see cref="ConcatObj"/>；按条件挑选用 <see cref="SelectObj(JlTuple)"/>；这里做的是"去掉另一元组里也有的那些对象"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion a = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion b = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlRegion ab = a.ConcatObj(b);
	///   JlObject rest = ab.ObjDiff(a);
	///   rest.Dispose();
	///   ab.Dispose();
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；输入元组 <c>this</c>、<paramref name="objectsSub"/> 由各自的 <c>GC.KeepAlive</c> 保证在原生调用期间不被回收，调用方可在其后照常释放。</para>
	/// </remarks>
	public JlObject ObjDiff(JlObject objectsSub)
	{
		IntPtr proc = JlNativeApi.PreCall(558);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objectsSub);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objectsSub);
		return obj;
	}

	/// <summary>把表示对象的"整数替身"（surrogate）转换回真实的图标对象，结果原地写入本实例。</summary>
	/// <param name="surrogateTuple">对象替身的整数元组（如 <see cref="ObjToInteger"/> 的返回值），每个整数代表一个句柄的整型编码。</param>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 integer_to_obj（id 566）。方法体先 <see cref="JlObjectBase.Dispose()"/> 释放本实例旧句柄，再把转换结果 <c>Load</c> 进 this（输出参数序 1）。</para>
	///   <para><b>约束或前提</b>基类 <c>Load</c> 要求目标句柄为 UNDEF，故本方法必须先 Dispose——意味着这是一次"替换"而非"追加"：调用前 this 持有的对象被丢弃。替身整数必须仍指向有效对象，否则原生调用报错。</para>
	///   <para><b>与相邻算子的取舍</b>本重载走 JlTuple 固定参数（Store + 调用后 <c>UnpinTuple</c>），适合一次传入多个替身；单个裸句柄用 <see cref="IntegerToObj(IntPtr)"/> 可省固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage image = new JlImage("byte", 64, 64);
	///   JlTuple surrogate = image.ObjToInteger(1, -1);
	///   JlObject restored = new JlObject();
	///   restored.IntegerToObj(surrogate);
	///   surrogate.Dispose();
	///   restored.Dispose();
	///   image.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原地改写：调用后 this 拥有新句柄、旧句柄已释放，不要对同一实例再期望旧内容。本方法不负责释放 <paramref name="surrogateTuple"/> 指向的原对象，替身所有权仍归原持有者。</para>
	/// </remarks>
	public void IntegerToObj(JlTuple surrogateTuple)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(566);
		JlNativeApi.Store(proc, 0, surrogateTuple);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(surrogateTuple);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>把单个"整数替身"（surrogate）转换回真实的图标对象，结果原地写入本实例。</summary>
	/// <param name="surrogateTuple">一个对象替身的整型编码（<see cref="ObjToInteger"/> 结果里的一个元素），以 <c>IntPtr</c> 形式直接传入。</param>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 integer_to_obj（id 566），与 <see cref="IntegerToObj(JlTuple)"/> 同一个算子。本标量重载用 <c>StoreIP</c> 把整数直写进原生参数 0，不走固定（pin）元组路径，因此没有 <c>UnpinTuple</c> 开销；输出经 InitOCT 在参数 1 装载。</para>
	///   <para><b>约束或前提</b>方法体先 <see cref="JlObjectBase.Dispose()"/> 释放本实例旧句柄再 <c>Load</c> 进 this——这是一次"替换"：调用前 this 持有的对象被丢弃。替身整数必须在转换时刻仍指向有效对象；本方法不增加引用计数，若原持有者已先释放该对象，转换结果即悬空句柄。</para>
	///   <para><b>与相邻算子的取舍</b>一次只还原一个对象用本重载（省钉固定开销）；要一次还原多个替身用 <see cref="IntegerToObj(JlTuple)"/>（Store + 调用后 UnpinTuple）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage image = new JlImage("byte", 64, 64);
	///   JlTuple surrogate = image.ObjToInteger(1, 1);
	///   JlObject restored = new JlObject();
	///   restored.IntegerToObj(new IntPtr(surrogate.IArr[0]));
	///   surrogate.Dispose();
	///   restored.Dispose();
	///   image.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原地改写：调用后 this 拥有新句柄、旧句柄已释放。<paramref name="surrogateTuple"/> 指向的原对象所有权仍归原持有者，本方法不负责释放它；示例中 <c>image</c> 必须先于 <c>restored</c> 的使用保持存活。</para>
	/// </remarks>
	public void IntegerToObj(IntPtr surrogateTuple)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(566);
		JlNativeApi.StoreIP(proc, 0, surrogateTuple);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>把本对象元组中的对象转成"整数替身"（surrogate）元组并返回，常用于跨语言/跨接口传递句柄。</summary>
	/// <param name="index">起始对象序号，1-based。Default: 1</param>
	/// <param name="number">要转换的对象个数，-1 表示从 index 起到元组末尾全部转换。Default: -1</param>
	/// <returns>INTEGER 类型的 JlTuple 新元组（每个元素是一个句柄的整型编码），需自行 <see cref="JlTuple.Dispose()"/>；本对象句柄不受影响。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 obj_to_integer（id 567）。本对象存入原生参数 1，index/number 分别以 <c>StoreI</c> 直写参数 0/1；输出经 <c>JlTuple.LoadNew</c> 按 INTEGER 类型装载——即替身是整数而非指针，后续再交给 <see cref="IntegerToObj(JlTuple)"/> 或 <see cref="IntegerToObj(IntPtr)"/> 还原。</para>
	///   <para><b>约束或前提</b>本对象必须已初始化；index 越界或为 0 由原生层报错。返回的整数只是句柄的编码快照，不增加引用计数——原对象被释放后替身即失效。</para>
	///   <para><b>与相邻算子的取舍</b>想把对象存进只接受整数的接口（回调、外部库参数）用本方法；要把对象写文件用 <see cref="WriteObject(string)"/>，要在内存传引用直接传 JlObject 即可。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion r2 = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlObject pair = r1.ConcatObj(r2);
	///   JlTuple surrogates = pair.ObjToInteger(1, -1);
	///   surrogates.Dispose();
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组独立于本对象；本对象与结果各管各的释放。取单个元素可用 <c>surrogates.IArr[0]</c>；注意 <c>IArr</c> 底层数组长度可能大于 <see cref="JlTuple.Length"/>，遍历以 Length 为准。</para>
	/// </remarks>
	public JlTuple ObjToInteger(int index, int number)
	{
		IntPtr proc = JlNativeApi.PreCall(567);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, index);
		JlNativeApi.StoreI(proc, 1, number);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>从本元组第 <paramref name="index"/> 个起复制 <paramref name="numObj"/> 个对象，返回新的对象元组。</summary>
	/// <param name="index">起始对象序号，1-based。Default: 1</param>
	/// <param name="numObj">要复制的对象个数；-1 表示从 index 起全部复制 [待实测]。Default: 1</param>
	/// <returns>复制得到的新 JlObject 句柄（底层数据共享、引用计数独立），需自行 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 copy_obj（id 568）。本对象存入原生参数 1，index/numObj 以 <c>StoreI</c> 直写参数 0/1；输出经 InitOCT + <c>LoadNew</c> 装载为新句柄——属"返回新句柄"，原元组不被修改。</para>
	///   <para><b>约束或前提</b>这是引用计数式复制：副本与原件共享底层对象，不是像素级深拷贝。index 越界或为 0 由原生层报错；本对象未初始化时报错。</para>
	///   <para><b>与相邻算子的取舍</b>要真正独立的副本用 <see cref="Clone"/>（序列化往返）；只按序号挑选子集用 <see cref="SelectObj(int)"/>（选出的元素同样是共享引用）；整体浅拷贝一份再改写才显式 CopyObj。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion r2 = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlObject pair = r1.ConcatObj(r2);
	///   JlObject copy = pair.CopyObj(1, -1);
	///   copy.Dispose();
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果与原件引用计数共享：Dispose 结果只是减一次计数，不影响原件内容；反之原件 Dispose 后结果仍可用。逐个对象复制时留意每次调用都是一次原生往返。</para>
	/// </remarks>
	public JlObject CopyObj(int index, int numObj)
	{
		IntPtr proc = JlNativeApi.PreCall(568);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, index);
		JlNativeApi.StoreI(proc, 1, numObj);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把两路对象元组首尾相接：本对象的元素在前、<paramref name="objects2"/> 的元素在后，返回拼接后的新元组。</summary>
	/// <param name="objects2">第二路对象元组，接在本对象之后。</param>
	/// <returns>拼接后的新 JlObject 句柄（新元组），需自行 <see cref="JlObjectBase.Dispose()"/>；两路输入本身不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 concat_obj（id 569）。本对象存入原生参数 1、第二路存入参数 2；输出经 InitOCT + <c>LoadNew</c> 装载为新句柄。结果长度 = 两路 <see cref="CountObj()"/> 之和。</para>
	///   <para><b>约束或前提</b>两路都必须已初始化且同族（图像/区域/XLD 混拼由原生层报错）。拼接只建立引用，元素对象本身与输入共享引用计数。</para>
	///   <para><b>与相邻算子的取舍</b>往元组中部插元素用 <see cref="InsertObj"/>（保持编号段语义）；合并后想去掉重复元素用 <see cref="ObjDiff"/>；只需追加到尾部时 ConcatObj 最直观、单次原生往返。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion r2 = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlObject pair = r1.ConcatObj(r2);
	///   int n = pair.CountObj();
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>输入元组可先于结果释放（元素靠引用计数存活）；两路由 <c>GC.KeepAlive</c> 保活到原生调用结束。循环里反复 ConcatObj 累积大元组是 O(N²) 的原生往返，批量合并建议先攒数组再拼。</para>
	/// </remarks>
	public JlObject ConcatObj(JlObject objects2)
	{
		IntPtr proc = JlNativeApi.PreCall(569);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objects2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objects2);
		return obj;
	}

	/// <summary>按索引元组一次性从本对象元组中挑出多个对象，返回由选中元素组成的新元组。</summary>
	/// <param name="index">要选出的对象序号（1-based），可含多个值；int/int[] 可隐式转换为 JlTuple。Default: 1</param>
	/// <returns>选中对象组成的新 JlObject 句柄（元素与原件共享引用计数），需自行 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 select_obj（id 572），与索引器 <c>this[JlTuple]</c> 同一入口。索引以固定元组 <c>Store</c> 进原生参数 0，调用后 <c>UnpinTuple</c> 解除固定；本对象存入参数 1；输出 InitOCT + <c>LoadNew</c> 装载新句柄。索引重复出现时同一对象会被多次纳入结果 [待实测]。</para>
	///   <para><b>约束或前提</b>序号必须落在 1..<see cref="CountObj()"/>，0 或越界由原生层报错。选的是"元组内位置"，与内容无关：上游 <c>Connection()</c> 等输出的顺序不稳定时按位置取会静默错取。</para>
	///   <para><b>与相邻算子的取舍</b>只取一个位置用 <see cref="SelectObj(int)"/>（StoreI 直写，无钉固定元组开销）；索引来自上游算子输出（如排序序号）时用本重载，一次调用取回全部。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion r2 = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlRegion r3 = new JlRegion(90.0, 90.0, 120.0, 120.0);
	///   JlObject triple = r1.ConcatObj(r2).ConcatObj(r3);
	///   JlTuple idx = new int[] { 3, 1 };
	///   JlObject picked = triple.SelectObj(idx);
	///   idx.Dispose();
	///   picked.Dispose();
	///   triple.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   r3.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><paramref name="index"/> 在原生调用返回前被钉住，调用完成前不要 Dispose 它；结果元素的存活不依赖输入元组句柄，靠引用计数各自管理。</para>
	/// </remarks>
	public JlObject SelectObj(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(572);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按单个序号（1-based）从本对象元组取出一个对象，返回只含它的 JlObject 新句柄。</summary>
	/// <param name="index">要取出的对象序号，1 指向元组首个对象。Default: 1</param>
	/// <returns>被选中对象组成的新 JlObject 句柄（元素与原件共享引用计数），需自行 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 select_obj（id 572），与 <see cref="SelectObj(JlTuple)"/> 和索引器同一入口。本标量重载用 <c>StoreI</c> 把 int 直写原生参数 0，不钉固定元组、无 <c>UnpinTuple</c> 开销；本对象存入参数 1，输出 <c>LoadNew</c> 装载新句柄。</para>
	///   <para><b>约束或前提</b>index 必须落在 1..<see cref="CountObj()"/>；0 或越界由原生层报错。传字面量时本重载优先于 JlTuple 重载（隐式转换是候选劣后），语义上等价于 <c>this[index]</c>。</para>
	///   <para><b>与相邻算子的取舍</b>一次取多个下标用 <see cref="SelectObj(JlTuple)"/>（索引可来自上游排序输出）；要拿到能独立改写的副本用 <see cref="CopyObj"/>；本重载适合"取第 k 个连通域"这类单点访问。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion r2 = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlObject pair = r1.ConcatObj(r2);
	///   JlObject first = pair.SelectObj(1);
	///   first.Dispose();
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是新句柄；按位置取数依赖上游输出顺序稳定（<c>Connection()</c> 顺序不保证），否则会静默取错。本对象由 <c>GC.KeepAlive</c> 保活到调用结束。</para>
	/// </remarks>
	public JlObject SelectObj(int index)
	{
		IntPtr proc = JlNativeApi.PreCall(572);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐对比较本元组与 <paramref name="objects2"/> 的元素是否"近似相等"，容差由 JlTuple 给出，返回单一布尔结果。</summary>
	/// <param name="objects2">参与比较的第二路对象元组。</param>
	/// <param name="epsilon">两灰度值/坐标等允许的最大偏差；以固定元组传入（Store + 调用后 UnpinTuple），可携带多个值。Default: 0.0</param>
	/// <returns>1 = 全部一致，0 = 存在不一致（经 <c>LoadI</c> 按 INTEGER 装载的单个整数，不是元组）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 compare_obj（id 573），与 <see cref="CompareObj(JlObject,double)"/> 同一个 id；差异仅在 epsilon 的写入方式：本重载钉固定元组（适合容差本身是算子输出），标量重载 StoreD 直写。</para>
	///   <para><b>约束或前提</b>两路元组按位置逐对比较；比较基于几何/灰度数据的近似相等，与句柄身份无关（内容相同但独立生成的两个对象判为相等，区别于 <see cref="ObjDiff"/> 的身份语义）。未初始化句柄由原生层报错。</para>
	///   <para><b>与相邻算子的取舍</b>严格判等（同一对象）用 <see cref="TestEqualObj"/>；需要数值容差（浮点坐标、灰度噪声）用本方法；单个容差值写起来更顺手的场合用 <see cref="CompareObj(JlObject,double)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion a = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion b = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlTuple tol = 0.05;
	///   int equal = a.CompareObj(b, tol);
	///   tol.Dispose();
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><paramref name="epsilon"/> 在原生调用返回前被钉住，调用完成前不要 Dispose 它；两路输入由 <c>GC.KeepAlive</c> 保活到调用结束。传 double 字面量而非 JlTuple 变量会绑定到标量重载，示例用显式 JlTuple 变量消歧。</para>
	/// </remarks>
	public int CompareObj(JlObject objects2, JlTuple epsilon)
	{
		IntPtr proc = JlNativeApi.PreCall(573);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objects2);
		JlNativeApi.Store(proc, 0, epsilon);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(epsilon);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objects2);
		return intValue;
	}

	/// <summary>逐对比较本元组与 <paramref name="objects2"/> 是否近似相等，容差为单个 double，直接写入原生参数。</summary>
	/// <param name="objects2">参与比较的第二路对象元组。</param>
	/// <param name="epsilon">两灰度值/坐标等允许的最大偏差，全对比共用这一标量值。Default: 0.0</param>
	/// <returns>1 = 全部一致，0 = 存在不一致（<c>LoadI</c> 按 INTEGER 装载的单个整数）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 compare_obj（id 573），与 <see cref="CompareObj(JlObject,JlTuple)"/> 同 id。本重载用 <c>StoreD</c> 把容差直写原生参数 0，不钉固定元组、无 <c>UnpinTuple</c>，日常"给个统一容差"的场景首选本重载。</para>
	///   <para><b>约束或前提</b>epsilon 是灰度值/坐标允许偏差的统一上限，0（默认）即严格比较；比较语义是内容近似相等而非句柄身份。两路都必须已初始化。</para>
	///   <para><b>与相邻算子的取舍</b>容差需要按对象逐对给不同值（来自上游算子输出）时用 JlTuple 重载；不需要容差、只判"同一对象"用 <see cref="TestEqualObj"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion a = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion b = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   int equal = a.CompareObj(b, 0.05);
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两路输入由 <c>GC.KeepAlive</c> 保活到原生调用结束，调用后可照常各自 Dispose；本重载无元组生命周期牵连。</para>
	/// </remarks>
	public int CompareObj(JlObject objects2, double epsilon)
	{
		IntPtr proc = JlNativeApi.PreCall(573);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objects2);
		JlNativeApi.StoreD(proc, 0, epsilon);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objects2);
		return intValue;
	}

	/// <summary>严格测试本元组与 <paramref name="objects2"/> 是否逐对相等（无容差参数），返回单一布尔结果。</summary>
	/// <param name="objects2">对照的对象元组。</param>
	/// <returns>1 = 两元组完全一致，0 = 不一致（<c>LoadI</c> 按 INTEGER 装载，非元组）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 test_equal_obj（id 576）。本对象存入原生参数 1、对照存入参数 2，输出为整型布尔。它是 compare_obj 的"零容差"近亲：不接受 epsilon，逐灰度值/逐坐标精确比对。</para>
	///   <para><b>约束或前提</b>两路元组长度与对象类别须一致，按位置逐对比较；任何一位浮点噪声都会判 0——经过变换链（旋转、插值）生成的"理论相同"图像通常判不等 [待实测]。双方必须已初始化。</para>
	///   <para><b>与相邻算子的取舍</b>有噪声/浮点误差的比对改用 <see cref="CompareObj(JlObject,double)"/> 给容差；想算"差集"用 <see cref="ObjDiff"/>；本方法适合阈值、二值化等离散精确结果的一致性校验。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion r2 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlObject pairA = r1.ConcatObj(r2);
	///   int same = pairA.TestEqualObj(pairA);
	///   pairA.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两路由 <c>GC.KeepAlive</c> 保活到原生调用结束；比较不消费任何句柄，双方调用后照常可用。</para>
	/// </remarks>
	public int TestEqualObj(JlObject objects2)
	{
		IntPtr proc = JlNativeApi.PreCall(576);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objects2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objects2);
		return intValue;
	}

	/// <summary>返回本对象元组包含的对象个数。</summary>
	/// <returns>元组内对象个数（<c>LoadI</c> 按 INTEGER 装载的 int 标量）；空元组返回 0。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 count_obj（id 577）。只读操作：本对象存入原生参数 1，输出单个整数，不产生新句柄、不改变本对象。</para>
	///   <para><b>约束或前提</b>本库所有"元组"语义（<see cref="ConcatObj"/>、<see cref="InsertObj"/>、<see cref="SelectObj(int)"/> 等）都以 1-based 序号定位，序号合法上限就是本方法的返回值；句柄为 UNDEF 未初始化时调用由原生层报错。</para>
	///   <para><b>与相邻算子的取舍</b>想按内容判"是否同一个对象"用 <see cref="TestEqualObj"/>；想枚举每个对象逐个处理时，配合 <see cref="SelectObj(int)"/> 循环 1..CountObj() 即可，无需其它算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion r2 = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlObject pair = r1.ConcatObj(r2);
	///   int n = pair.CountObj();
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>每次调用都是一次原生过程往返，热循环里反复取长度可先缓存；本对象由 <c>GC.KeepAlive</c> 保活到调用结束。</para>
	/// </remarks>
	public int CountObj()
	{
		IntPtr proc = JlNativeApi.PreCall(577);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>查询对象"分量"（通道/区域成分）信息，channel 以元组传入，返回可含多值的 JlTuple。</summary>
	/// <param name="request">要查询的信息种类（字符串直写原生参数 0）。Default: "creator"</param>
	/// <param name="channel">被检查的分量序号元组；区域/XLD 无通道概念，传 0。走固定元组（Store + 调用后 UnpinTuple）。Default: 0</param>
	/// <returns>所请求信息的 JlTuple 新元组（多通道时逐通道各占一值），需自行 <see cref="JlTuple.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 get_channel_info（id 578），与 <see cref="GetChannelInfo(string,int)"/> 同 id。request 用 <c>StoreS</c> 写入参数 0，channel 写入参数 1；输出经 <c>JlTuple.LoadNew</c> 装载，保留原生返回的全部值。</para>
	///   <para><b>约束或前提</b>本对象必须已初始化；对区域/XLD 传非 0 的 channel 由原生层报错（它们没有多通道）。request 除默认 "creator" 外的合法取值集合本仓库文档未枚举 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要一个分量的单个字符串时用 <see cref="GetChannelInfo(string,int)"/>（StoreI + LoadS，直读标量）；需要一次拿全多通道信息（如拼接图各通道的创建者）用本重载，标量重载的 <c>LoadS</c> 只读第一个值、多余结果会被静默丢弃。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage image = new JlImage("byte", 64, 64);
	///   JlTuple chans = 1;
	///   JlTuple info = image.GetChannelInfo("creator", chans);
	///   chans.Dispose();
	///   info.Dispose();
	///   image.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><paramref name="channel"/> 在原生调用返回前被钉住，调用完成前不要 Dispose；本重载传 int 字面量会绑定到标量版本（返回 string），故示例显式声明 JlTuple 变量消歧。</para>
	/// </remarks>
	public JlTuple GetChannelInfo(string request, JlTuple channel)
	{
		IntPtr proc = JlNativeApi.PreCall(578);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, request);
		JlNativeApi.Store(proc, 1, channel);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(channel);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>查询单个分量的信息，channel 以 int 直写原生参数，返回单个字符串。</summary>
	/// <param name="request">要查询的信息种类（<c>StoreS</c> 直写原生参数 0）。Default: "creator"</param>
	/// <param name="channel">被检查的分量序号；区域/XLD 无通道概念，传 0。Default: 0</param>
	/// <returns>所请求信息的单个字符串（经 <c>LoadS</c> 装载）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 get_channel_info（id 578），与 <see cref="GetChannelInfo(string,JlTuple)"/> 同 id。本重载用 <c>StoreI</c> 直写 channel 到参数 1，无钉固定元组开销。</para>
	///   <para><b>约束或前提</b>本对象必须已初始化。真坑：输出走 <c>LoadS</c> 只读第一个字符串——若原生侧对多通道返回多值，其余值被静默丢弃；要多值必须用 JlTuple 重载。</para>
	///   <para><b>与相邻算子的取舍</b>一次查多个分量、或确定图像多通道时用 <see cref="GetChannelInfo(string,JlTuple)"/>；对象整体的类名用 <see cref="GetObjClass"/>；本重载适合"单通道图查一个属性"的直读场景。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage image = new JlImage("byte", 64, 64);
	///   string creator = image.GetChannelInfo("creator", 1);
	///   image.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>request 除默认 "creator" 外的合法取值集合本仓库文档未枚举 [待实测]；本对象由 <c>GC.KeepAlive</c> 保活到调用结束，返回的 string 为托管数据，无需释放。</para>
	/// </remarks>
	public string GetChannelInfo(string request, int channel)
	{
		IntPtr proc = JlNativeApi.PreCall(578);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, request);
		JlNativeApi.StoreI(proc, 1, channel);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadS(proc, 0, err, out var stringValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return stringValue;
	}

	/// <summary>返回本图标对象的类名字符串元组（image / region / xld 等原生对象类）。</summary>
	/// <returns>含类名的 JlTuple 字符串元组（<c>JlTuple.LoadNew</c> 装载），需自行 <see cref="JlTuple.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 get_obj_class（id 579）。只读：本对象存入原生参数 1，输出按字符串元组装载；返回的是原生层的对象类名，不是 C# 运行时类型。</para>
	///   <para><b>约束或前提</b>本对象必须已初始化，UNDEF 句柄由原生层报错。经 <see cref="Deserialize(Stream)"/> 还原的对象其 C# 静态类型恒为 JlObject，判真实类别要靠本方法的类名而非 <c>is/as</c>。</para>
	///   <para><b>与相邻算子的取舍</b>查通道级信息用 <see cref="GetChannelInfo(string,int)"/>；本方法只回答"这是什么类的对象"，用于分支派发或反序列化后的类型确认。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion region = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlTuple cls = region.GetObjClass();
	///   string name = cls.S;
	///   cls.Dispose();
	///   region.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>单值可经 <c>cls.S</c>（首元素字符串）取出；元组本身独立分配，用完 Dispose。本对象由 <c>GC.KeepAlive</c> 保活到调用结束。</para>
	/// </remarks>
	public JlTuple GetObjClass()
	{
		IntPtr proc = JlNativeApi.PreCall(579);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把本实例改写为"空对象元组"（长度为 0 的合法句柄），旧句柄先被释放。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 gen_empty_obj（id 602）。无输入参数；方法体先 <see cref="JlObjectBase.Dispose()"/>，输出经 InitOCT 在参数 1 装载进 this——原地改写。空元组是已初始化的合法对象：<see cref="CountObj()"/> 返回 0，可直接作 ConcatObj/InsertObj 的接收方。</para>
	///   <para><b>约束或前提</b>调用前 this 持有的对象被无条件丢弃；想保住旧内容先 <see cref="Clone"/> 或 <see cref="CopyObj"/> 出去。循环里对同一实例反复 GenEmptyObj 会逐次释放旧句柄，是累积型流程（逐个 ConcatObj 进结果元组）的标准起点。</para>
	///   <para><b>与相邻算子的取舍</b>只要"未装载"的壳用 <c>new JlObject()</c>（UNDEF，不能当输入传给算子）；空元组能进算子而 UNDEF 不能——初始化输出容器优先用本方法。序列化族（WriteObject 等）对空元组的行为 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObject acc = new JlObject();
	///   acc.GenEmptyObj();
	///   JlRegion r = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlObject grown = acc.ConcatObj(r);
	///   acc.Dispose();
	///   grown.Dispose();
	///   r.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本实例由 <c>GC.KeepAlive</c> 保活到调用结束；改写后 <see cref="JlObjectBase.IsInitialized()"/> 为 true，与刚 new 出来的 UNDEF 壳不同。</para>
	/// </remarks>
	public void GenEmptyObj()
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(602);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>从 Vision 对象文件读取对象元组，装载结果原地写入本实例（旧句柄先被释放）。</summary>
	/// <param name="fileName">对象文件路径（<see cref="WriteObject(string)"/> 写出的同族格式）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 read_obj（id 1566）。文件名经 <c>StoreS</c> 写入原生参数 0；方法体先 <see cref="JlObjectBase.Dispose()"/> 再 <c>Load</c> 进 this（输出参数 1）——是"原地改写"而非返回新句柄，读到的可以是多对象元组。</para>
	///   <para><b>约束或前提</b>这是一次替换：调用前 this 持有的对象被无条件丢弃，别把结果装进还想保留内容的实例。文件不存在或格式不符由原生层报错；一个文件含多个对象时整个元组进入 this，用 <see cref="CountObj()"/> 判断数量。</para>
	///   <para><b>与相邻算子的取舍</b>想直接拿到新句柄（不动已有实例）就先 <c>new JlObject()</c> 再 ReadObject，或用 <see cref="Deserialize(Stream)"/>（流→新对象）；写回文件用 <see cref="WriteObject(string)"/>；内存字节还原用 <see cref="DeserializeObject(byte[])"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObject obj = new JlObject();
	///   obj.ReadObject("data.dat");    // 原地装载（id 1566）：UNDEF 空壳填入文件内容
	///   int n = obj.CountObj();        // 文件含多个对象时据此判断元组数量
	///   obj.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>读取失败时旧句柄已释放、this 处于 UNDEF——调用后若需回退只能重读；本实例由 <c>GC.KeepAlive</c> 保活到调用结束。</para>
	/// </remarks>
	public void ReadObject(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1566);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>把本对象元组（全部元素）以 Vision 二进制格式写入单个文件，不改变本对象句柄。</summary>
	/// <param name="fileName">目标文件路径；元组有 N 个对象时全部进入同一文件，读回（<see cref="ReadObject(string)"/>）后元组长度不变。</param>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 write_obj（id 1567）。本对象存入原生参数 1，文件名 <c>StoreS</c> 写入参数 0；无输出参数——纯只读落盘，调用后对象照常可用。</para>
	///   <para><b>约束或前提</b>本对象必须已初始化；目标目录需存在且可写，路径非法由原生层报错。文件为库专有格式（非 BMP/PNG 图像文件），跨格式保存图像不走本方法。</para>
	///   <para><b>与相邻算子的取舍</b>要写进任意 <see cref="Stream"/>（内存、网络、自定义容器）用 <see cref="Serialize(Stream)"/>；只要裸字节用 <see cref="SerializeObject"/>；本方法适合"对象快照存盘、之后按同名文件读回"的工作流。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion r2 = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlObject pair = r1.ConcatObj(r2);
	///   pair.WriteObject("regions.dat");
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本对象由 <c>GC.KeepAlive</c> 保活到原生调用结束；多对象元组逐个序列化，大元组落盘耗时随元素数线性增长。</para>
	/// </remarks>
	public void WriteObject(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1567);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>把 <see cref="SerializeObject"/> 得到的内存序列块还原为对象，原地装载进本实例（旧句柄先被释放）。</summary>
	/// <param name="serializedItemHandle">序列化字节块，必须来自同族 <see cref="SerializeObject"/>/<see cref="Serialize(Stream)"/> 输出，格式与版本需匹配。</param>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 deserialize_obj（id 1568）。字节块先包成 <c>JlSerializationBuffer</c> 存入原生参数 0；方法体先 <see cref="JlObjectBase.Dispose()"/> 再 <c>Load</c> 进 this（输出参数 1）——原地改写，不返回新句柄。</para>
	///   <para><b>约束或前提</b>这是一次替换：调用前 this 的内容被丢弃；序列块若含多对象元组，还原后整体进入 this。<paramref name="serializedItemHandle"/> 为 null 或格式损坏由原生层报错，此时旧句柄已释放。</para>
	///   <para><b>与相邻算子的取舍</b>不想动已有实例、要新句柄时用 <see cref="Deserialize(Stream)"/>（返回 JlObject）或 <see cref="Clone"/>；从文件还原用 <see cref="ReadObject(string)"/>（同样原地改写）；本方法专用于"裸字节数组→指定实例"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage image = new JlImage("byte", 64, 64);
	///   byte[] data = image.SerializeObject();
	///   JlObject restored = new JlObject();
	///   restored.DeserializeObject(data);
	///   restored.Dispose();
	///   image.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>缓冲与 this 都由 <c>GC.KeepAlive</c> 保活到原生调用结束；byte[] 是纯托管数据可随意保留，还原不消费它，同一块字节可反复装进不同实例。</para>
	/// </remarks>
	public void DeserializeObject(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1568);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>把当前对象整体序列化为 Vision 二进制字节块并返回，供内存传输或 <see cref="DeserializeObject(byte[])"/> 还原。</summary>
	/// <returns>序列化后的字节数组（纯托管数据，不涉及句柄释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 serialize_obj（id 1569）。本对象存入原生参数 1，输出按字节缓冲装载（<c>JlSerializationBuffer.LoadBytes</c>）。这是一次只读操作：不消费、不改变本对象句柄，调用后对象照常可用。</para>
	///   <para><b>约束或前提</b>对象必须已初始化（UNDEF 句柄调用由原生层报错）；对空元组调用的行为 [待实测]。多对象元组会被完整序列化，还原后元组长度不变。</para>
	///   <para><b>与相邻算子的取舍</b>要落到任意 <see cref="Stream"/>（网络、自定义容器）用 <see cref="Serialize(Stream)"/>（内部就是本方法加一次写流）；落盘成独立文件用 <see cref="WriteObject(string)"/>；<see cref="Clone"/> 则是序列化+反序列化的深拷贝组合。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage image = new JlImage("byte", 64, 64);
	///   byte[] data = image.SerializeObject();
	///   JlObject restored = new JlObject();
	///   restored.DeserializeObject(data);
	///   restored.Dispose();
	///   image.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的 byte[] 是拷贝，与原生缓冲无生命周期牵连，可随意保留、跨线程传递；还原端 <see cref="DeserializeObject(byte[])"/> 会先释放目标旧句柄，注意别把结果装进还想保留内容的对象。</para>
	/// </remarks>
	public byte[] SerializeObject()
	{
		IntPtr proc = JlNativeApi.PreCall(1569);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>把 <paramref name="objectsInsert"/> 整段插入本元组第 <paramref name="index"/> 个位置之前，返回加长后的新元组。</summary>
	/// <param name="objectsInsert">待插入的对象元组（可含多个对象，按原顺序整体嵌入）。</param>
	/// <param name="index">插入位置，1-based；合法范围 1..N+1（N 为本元组长度），取 N+1 即追加到尾部。</param>
	/// <returns>插入后的新对象元组（新句柄），需自行 <see cref="JlObjectBase.Dispose()"/>；原元组不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 insert_obj（id 2003）。原生参数序：索引在 0、输入元组在 1、插入元组在 2；索引经 <c>StoreI</c> 直写。输出 InitOCT 装载新句柄。</para>
	///   <para><b>约束或前提</b>索引 0 或大于 N+1 由原生层报错。对本元组为空元组（<see cref="CountObj()"/> 为 0）的情形，只能插到位置 1。</para>
	///   <para><b>与相邻算子的取舍</b>只在尾部追加用 <see cref="ConcatObj"/> 更直观；要覆盖已有位置用 <see cref="ReplaceObj(JlObject,int)"/>；本算子的价值是把新对象插进元组中部、维持既有序列语义（比如把补测结果插回原编号段）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion r2 = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlRegion mid = new JlRegion(30.0, 30.0, 45.0, 45.0);
	///   JlObject pair = r1.ConcatObj(r2);
	///   JlObject grown = pair.InsertObj(mid, 2);
	///   grown.Dispose();
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   mid.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果元组中插入的元素与 <paramref name="objectsInsert"/> 引用计数共享，释放结果不影响元素本身。本对象与插入元组都由 <c>GC.KeepAlive</c> 保活到原生调用结束；插入后其后元素编号整体后移，下游按序号取数需按新序换算。</para>
	/// </remarks>
	public JlObject InsertObj(JlObject objectsInsert, int index)
	{
		IntPtr proc = JlNativeApi.PreCall(2003);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objectsInsert);
		JlNativeApi.StoreI(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objectsInsert);
		return obj;
	}

	/// <summary>一次性删掉本元组中位于 <paramref name="index"/> 各位置（按原始元组计）的对象，返回剩余新元组。</summary>
	/// <param name="index">待删除位置的 1-based 索引元组，可含多个值；int/int[] 可隐式转换。</param>
	/// <returns>剩余对象的新元组（新句柄），相对顺序不变；需自行 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 remove_obj（id 2005）。索引以固定元组 <c>Store</c> 进原生参数 0，调用后 <c>UnpinTuple</c>；本对象在参数 1；输出 InitOCT 装载为新句柄。</para>
	///   <para><b>约束或前提</b>元组内各索引都针对调用时的原元组解释（不是边删边重排），重复索引的处理 [待实测]；任一索引为 0 或越界由原生层报错。空索引元组等于整份浅拷贝。</para>
	///   <para><b>与相邻算子的取舍</b>配合 <c>Connection</c>+按面积/灰度筛选的典型流程：先用条件算子得到要剔除的序号元组，再一次性 RemoveObj，比循环调用 <see cref="RemoveObj(int)"/> 既少一次原生往返、又不会因元组缩短而错位。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion r2 = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlRegion r3 = new JlRegion(90.0, 90.0, 120.0, 120.0);
	///   JlObject triple = r1.ConcatObj(r2).ConcatObj(r3);
	///   JlTuple drop = new int[] { 1, 3 };
	///   JlObject rest = triple.RemoveObj(drop);
	///   drop.Dispose();
	///   rest.Dispose();
	///   triple.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   r3.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><paramref name="index"/> 在原生调用返回前被钉住，调用完成前不要 Dispose 它；结果元组与新句柄独立，但元素与其它句柄共享引用计数。</para>
	/// </remarks>
	public JlObject RemoveObj(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(2005);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>从本对象元组中删掉第 <paramref name="index"/> 个对象，返回删除后的新元组。</summary>
	/// <param name="index">要删除的对象位置，1-based（1 指向元组首元素）。</param>
	/// <returns>剩余对象组成的新元组（新句柄），保持原有相对顺序；需自行 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 remove_obj（id 2005）。本对象存入原生参数 1，索引经 <c>StoreI</c> 写入参数 0，输出用 InitOCT 装载为新句柄——原元组不被修改。</para>
	///   <para><b>约束或前提</b>索引 0 或超过 <see cref="CountObj()"/> 属非法请求，由原生层报错。删除后其余元素位置整体前移：连续删多个位置时用 <see cref="RemoveObj(JlTuple)"/> 一次完成，避免按"原始序号"逐个删时因元组缩短而错位。</para>
	///   <para><b>与相邻算子的取舍</b>只删一个位置用本重载（无钉固定元组开销）；删多个位置或位置来自上游算子输出时用 JlTuple 重载；按内容剔除用 <see cref="ObjDiff"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion r2 = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlObject pair = r1.ConcatObj(r2);
	///   JlObject rest = pair.RemoveObj(1);
	///   rest.Dispose();
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；被"删除"只是从结果元组中移除引用，元素对象本身仍由原持有者引用计数管理。删除最后一个元素得到的是空元组而非 null，可继续 <see cref="CountObj()"/> 判 0。</para>
	/// </remarks>
	public JlObject RemoveObj(int index)
	{
		IntPtr proc = JlNativeApi.PreCall(2005);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按 <paramref name="index"/> 给出的一个或多个位置，用 <paramref name="objectsReplace"/> 的元素逐个覆盖本元组，返回新元组。</summary>
	/// <param name="objectsReplace">替换对象元组；其元素按顺序与 <paramref name="index"/> 的各位置配对。</param>
	/// <param name="index">被替换元素的 1-based 位置序列（单值或多值 JlTuple，int/int[] 可隐式转换）。</param>
	/// <returns>替换后的新对象元组（新句柄），需自行 <see cref="JlObjectBase.Dispose()"/>；原元组不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 replace_obj（id 2006）。原生参数序：索引在 0、输入元组在 1、替换元组在 2（与 C# 形参序相反）。索引以固定元组方式 <c>Store</c>，调用完成后 <c>UnpinTuple</c> 解除固定。</para>
	///   <para><b>约束或前提</b>所有索引位置必须存在于原元组内（1-based，0 或越界由原生层报错）。多位置替换时 <paramref name="index"/> 的元素数应与 <paramref name="objectsReplace"/> 的对象数一致；替换元组只有 1 个对象而索引多个时是否"广播"到全部位置 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只替换固定一个位置时用 <see cref="ReplaceObj(JlObject,int)"/>（StoreI 直写、无钉元组开销）；常见用法是 <c>Connection</c> 之后按上游序号替换掉误检的那一块——注意按位置替换依赖上游输出顺序稳定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion r1 = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion r2 = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlRegion r3 = new JlRegion(90.0, 90.0, 120.0, 120.0);
	///   JlObject pair = r1.ConcatObj(r2);
	///   JlObject replaced = pair.ReplaceObj(r3, 1);
	///   replaced.Dispose();
	///   pair.Dispose();
	///   r1.Dispose();
	///   r2.Dispose();
	///   r3.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；结果内未被打断位置的元素仍与原元组引用计数共享，释放结果不影响原对象。<paramref name="index"/> 在原生调用结束后才 Unpin，调用前不要 Dispose 它。</para>
	/// </remarks>
	public JlObject ReplaceObj(JlObject objectsReplace, JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(2006);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objectsReplace);
		JlNativeApi.Store(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objectsReplace);
		return obj;
	}

	/// <summary>用 <paramref name="objectsReplace"/> 中的对象替换本元组自 <paramref name="index"/> 起的元素，返回替换后的新元组。</summary>
	/// <param name="objectsReplace">替换用对象元组；从 <paramref name="index"/> 起按顺序逐个覆盖原元组的对应位置。</param>
	/// <param name="index">起始替换位置，1-based（1 指向元组首元素）。本重载的索引必须是元组中真实存在的位置。</param>
	/// <returns>替换后的新对象元组（新句柄）；原元组本身不变，结果需自行 <see cref="JlObjectBase.Dispose()"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>底层算子 replace_obj（id 2006）。本对象存入原生参数 1，<paramref name="objectsReplace"/> 存入参数 2，索引写入参数 0；输出经 InitOCT 装载为新句柄，属"返回新句柄"而非原地改写。</para>
	///   <para><b>约束或前提</b>替换区间 [index, index + len(objectsReplace)) 必须落在原元组长度内，越界由原生层报错；两方都必须已初始化。本标量重载用 <c>StoreI</c> 直写 int，无钉固定元组的开销。</para>
	///   <para><b>与相邻算子的取舍</b>要在多个离散位置各放一个对象时用 <see cref="ReplaceObj(JlObject,JlTuple)"/>（元组索引走 Store + 调用后 UnpinTuple）；只做插入不做覆盖用 <see cref="InsertObj"/>；只删不换用 <see cref="RemoveObj(int)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlRegion a = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlRegion b = new JlRegion(50.0, 50.0, 80.0, 80.0);
	///   JlRegion c = new JlRegion(90.0, 90.0, 120.0, 120.0);
	///   JlObject pair = a.ConcatObj(b);
	///   JlObject replaced = pair.ReplaceObj(c, 2);
	///   replaced.Dispose();
	///   pair.Dispose();
	///   a.Dispose();
	///   b.Dispose();
	///   c.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果元组里的元素与原元组/替换元组按引用计数共享底层数据：Dispose 结果句柄不会释放元素对象本身，三个输入句柄与结果要各自释放。本对象与 <paramref name="objectsReplace"/> 由 <c>GC.KeepAlive</c> 保证在原生调用结束前不被回收。</para>
	/// </remarks>
	public JlObject ReplaceObj(JlObject objectsReplace, int index)
	{
		IntPtr proc = JlNativeApi.PreCall(2006);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, objectsReplace);
		JlNativeApi.StoreI(proc, 0, index);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(objectsReplace);
		return obj;
	}
}
