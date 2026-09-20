using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>算子控制域通用句柄的托管封装：持有一个按引用计数管理的原生句柄，负责拷贝、装载与释放。</summary>
/// <remarks>
///   <para><b>这个句柄代表什么</b>：JlHandle 包装原生侧的通用句柄对象（语义类型多样，如 serialized_item 或各算子内部句柄），与 JlImage/JlRegion 走图标对象 key 的 JlObjectBase 是两套通道。本类的私有 AssertSemType() 是空实现——托管层不校验句柄装的是哪种语义类型，需要判定类型时用 TupleSemType() 自查。</para>
///   <para><b>由谁创建</b>：算子封装内部经 LoadNew 装载输出、静态 Deserialize(Stream) 反序列化、Clone 深拷贝、各拷贝类构造器，以及本程序集的派生类（如 JlSerializationBuffer）。本类没有"从内容凭空造一个新句柄"的公开入口。</para>
///   <para><b>何时释放</b>：继承 JlHandleBase : IDisposable，Dispose（或终结器）清掉本实例持有的那一份原生引用；句柄值为 JlHandleBase.UNDEF（IntPtr.Zero）即未初始化。释放或清空后再参与算子调用，原生报错由 PostCall 统一抛 JlOperatorException。</para>
///   <para><b>拷贝/移动语义</b>：构造器与 Handle 属性赋值都走原生 CopyHandle 引用计数拷贝——C# 壳各自独立、须各自 Dispose，但底层是否指向同一份内容 [待实测]；本类无移动语义，要内容互不相干的独立副本用 Clone()（序列化往返新建对象）。</para>
/// </remarks>
[Serializable]
public class JlHandle : JlHandleBase, ISerializable, ICloneable
{
	/// <summary>造一个句柄为 UNDEF 的空容器：作 DeserializeHandle/LoadNew 装载前的接收位，不发任何原生调用。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>转调 base(JlHandleBase.UNDEF)，句柄值即 IntPtr.Zero，纯托管建壳；本类没有 JlHandle(bool) 之类重载。</para>
	///   <para><b>约束或前提</b>空句柄不能当算子输入——SerializeHandle/TupleSemType 等会 Store(UNDEF) 进原生，由 PostCall 抛 JlOperatorException [原生错误码待实测]。它的正当用途是做装载接收位：基类 Load 只接受 UNDEF 实例，抛 "Undisposed handle instance when loading output parameter" 正是没先腾空就装载的下场。</para>
	///   <para><b>与相邻构造器的取舍</b>已有活句柄要第二个容器用 JlHandle(JlHandle) 拷贝构造；要内容互不相干的副本用 Clone()；本构造只适合"先造空壳、再 DeserializeHandle 填入"两步用法（Clone 内部就是这个套路）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	/// using (JlHandle shell = new JlHandle())
	/// {
	///     bool ready = shell.IsInitialized();   // false：UNDEF 空壳
	/// }
	/// </code>
	///   <para><b>资源与坑</b>空壳 Dispose 是安全空操作；终结器兜底，但批量建壳仍建议显式 Dispose 及时还原生内存。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlHandle()
		: base(JlHandleBase.UNDEF)
	{
	}

	/// <summary>包装一个外部取得的原生句柄值：经 Handle 属性赋值再走原生 CopyHandle，拿到的是一份引用拷贝，不接管原指针的所有权。</summary>
	/// <param name="handle">原生侧句柄值。IntPtr.Zero 视同 UNDEF：SetHandleInternal 对零值直接跳过拷贝，得到空壳。</param>
	/// <remarks>
	///   <para><b>功能说明</b>base(handle) 落到 JlHandleBase 的 Handle setter，SetHandleInternal(copy:true) 调 HLICopyHandle 另取一份引用——因此把同一个句柄值包进两个 JlHandle 是安全的，两者各自 Dispose 各还各的引用。AssertSemType() 在本类是空实现，任何语义类型的句柄都能包，类型不对不会在构造时被拦下。</para>
	///   <para><b>约束或前提</b>句柄值必须是原生侧当前有效的句柄：悬垂指针（已被 clear 的旧值）会让 CopyHandle 失败，构造阶段直接抛 JlOperatorException，不会静默退化成空壳。</para>
	///   <para><b>与相邻构造器的取舍</b>手里是活 JlHandle 对象就写 new JlHandle(src) 更直白；本重载专供从别处（其他语言接口、非托管层）递来的裸 IntPtr。要"引用交接不复制"只能走字节轮转：SerializeHandle 导出、空壳 DeserializeHandle 装入。</para>
	///   <para><b>用法</b></para>
	///   <code>
	/// using (Stream fs = File.OpenRead("item.bin"))
	/// using (JlHandle h = JlHandle.Deserialize(fs))
	/// using (JlHandle own = new JlHandle(h.Handle))   // 再取一份引用
	/// {
	///     bool valid = own.IsInitialized();
	/// }
	/// </code>
	///   <para><b>资源与坑</b>Handle 属性赋值走同一条 copy:true 路径——h.Handle = 新值 会先清旧引用再拷新值；把 Handle 值长期存到本对象之外时，务必先经本构造器（或原生拷贝）持有引用，否则原件一 Dispose 你存的就是悬垂值。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlHandle(IntPtr handle)
		: base(handle)
	{
		AssertSemType();
	}

	/// <summary>拷贝构造：经基类把源句柄的裸值走 CopyHandle 引用计数拷贝——C# 壳独立、各须各 Dispose，但不是深拷贝。</summary>
	/// <param name="handle">源句柄。传 null 不抛空引用：基类内部走 IntPtr 隐式转换（null 归一为 UNDEF），结果是空壳。</param>
	/// <remarks>
	///   <para><b>功能说明</b>base(handle) 进 JlHandleBase 的拷贝路径，先清本实例旧值再 HLICopyHandle 另取引用。与 JlHandle(IntPtr) 的区别只在入参类型；本类 AssertSemType() 为空实现，不校验源句柄的语义类型。</para>
	///   <para><b>拷贝语义</b>这是"第二个名字指向同一原生资源"级别的浅拷贝：两个壳释放互不干扰（各还各的引用），但一侧内容被原地改写（如 DeserializeHandle）时另一侧看到什么 [底层共享程度待实测]。要彻底独立请走 Clone()——它经序列化往返真正新建对象。</para>
	///   <para><b>与相邻构造器的取舍</b>跨进程/跨语言传内容用 Serialize+Deserialize(Stream)；只在本地多持一个容器用本构造，几乎零开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	/// using (Stream fs = File.OpenRead("item.bin"))
	/// using (JlHandle h = JlHandle.Deserialize(fs))
	/// using (JlHandle alias = new JlHandle(h))
	/// {
	///     string sem = alias.TupleSemType();
	/// }
	/// </code>
	///   <para><b>资源与坑</b>别把浅拷贝当备份：source 与 alias 是两个引用计数持有者而非两份数据，忘 Dispose 任一个都算泄漏。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlHandle(JlHandle handle)
		: base(handle)
	{
		AssertSemType();
	}

	private void AssertSemType()
	{
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlHandle obj)
	{
		obj = new JlHandle(JlHandleBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlHandle[] obj)
	{
		err = JlTuple.LoadNew(proc, parIndex, err, out var tuple);
		obj = new JlHandle[tuple.Length];
		for (int i = 0; i < tuple.Length; i++)
		{
			obj[i] = new JlHandle(tuple[i].H);
		}
		tuple.Dispose();
		return err;
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeHandle();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>.NET 二进制反序列化构造器：从 SerializationInfo 的 "data" 键取出序列化字节，经 DeserializeHandle 装入本实例。</summary>
	/// <param name="info">序列化载体，须含键 "data"（byte[]）——即本类 ISerializable.GetObjectData 用 SerializeHandle() 写出的那份负载；键缺失或类型不符抛 SerializationException。</param>
	/// <param name="context">流上下文，本实现不参与解析。</param>
	/// <remarks>
	///   <para><b>功能说明</b>构造器未显式链 base，隐式走基类无参路径先成 UNDEF 空壳，再 info.GetValue("data", typeof(byte[])) → DeserializeHandle 原地装入；负载格式与 Serialize(Stream)/Deserialize(Stream) 完全同源，三个入口的字节可互喂。</para>
	///   <para><b>与相邻入口的取舍</b>手里是裸流优先静态 Deserialize(Stream)，报错信息明确（"Input stream is no serialized Vision object"）；已有 byte[] 用实例 DeserializeHandle；本构造器只在整体对象图参与 .NET 二进制序列化（类标了 [Serializable]+ISerializable）时被格式化器回调，业务代码一般不直接 new。</para>
	///   <para><b>用法</b></para>
	///   <code>
	/// using (Stream fs = File.OpenRead("item.bin"))
	/// using (JlHandle h = JlHandle.Deserialize(fs))
	/// {
	///     byte[] payload = h.SerializeHandle();   // 本构造器读的 "data" 就是这种字节
	/// }
	/// </code>
	///   <para><b>资源与坑</b>构造出的实例持有新原生对象，同样要 Dispose；负载非法时 DeserializeHandle 中途抛异常，实例停在已 Dispose 的空壳态。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlHandle(SerializationInfo info, StreamingContext context)
	{
		DeserializeHandle((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把句柄内容序列化为 Vision 二进制格式并写入 stream。</summary>
	/// <param name="stream">可写的目标流；只追加原始序列化字节，不负责关闭流。</param>
	/// <remarks>
	///   <para><b>功能说明</b>内部先调用 SerializeHandle()（原生算子 2015）得到完整序列化项字节，再用 JlSerializationBuffer.WriteToStream 原样写入流，即 stream.Write(data,0,len)，不额外添加长度框。</para>
	///   <para><b>约束或前提</b>写入的字节自带 16 字节头，因此可被静态 Deserialize / ReadFromStream 原样读回，实现文件级往返。</para>
	///   <para><b>与相邻成员的取舍</b>只要内存字节用 SerializeHandle()；要落盘或跨进程用本方法。本库序列化的即是原生 serialized_item 格式。</para>
	///   <para><b>用法</b></para>
	///   <code>
	/// using (Stream src = File.OpenRead("item.bin"))
	/// using (JlHandle h = JlHandle.Deserialize(src))
	/// using (Stream dst = File.Create("copy.bin"))
	/// {
	///     h.Serialize(dst);
	/// }
	/// </code>
	///   <para><b>资源与坑</b>h 由 Deserialize 返回属新句柄，需 Dispose；stream 生命周期由调用方管理；序列化只读，不改变 this 指向的内容。</para>
	/// </remarks>
	public virtual void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeHandle(), stream);
	}

	/// <summary>从 Vision 二进制流反序列化出一个新句柄。</summary>
	/// <param name="stream">已定位到序列化项起点的可读流。</param>
	/// <returns>一个新建的通用 JlHandle（非具体的 JlImage/JlRegion 等包装类型），指向反序列化出的对象；用完需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>先 new JlHandle()，再用 JlSerializationBuffer.ReadFromStream 读取字节并调用 DeserializeHandle 原地填充，返回该句柄。读取时按 16 字节头解析出对象大小，只消费一个序列化项，流位置随之前进。</para>
	///   <para><b>约束或前提</b>stream 必须是从 Serialize/SerializeHandle 写出的合法 Vision 序列化数据，否则抛 JlException（"Input stream is no serialized Vision object" 或 "Unexpected end of serialization data"）。</para>
	///   <para><b>与相邻成员的取舍</b>字节已在内存中用实例方法 DeserializeHandle；本方法专门面向流。</para>
	///   <para><b>用法</b></para>
	///   <code>
	/// using (Stream fs = File.OpenRead("item.bin"))
	/// {
	///     JlHandle h = JlHandle.Deserialize(fs);
	///     // ... 使用 h ...
	///     h.Dispose();
	/// }
	/// </code>
	///   <para><b>资源与坑</b>返回的是新句柄，必须 Dispose；本方法不关闭调用方持有的 stream。</para>
	/// </remarks>
	public static JlHandle Deserialize(Stream stream)
	{
		JlHandle hHandle = new JlHandle();
		hHandle.DeserializeHandle(JlSerializationBuffer.ReadFromStream(stream));
		return hHandle;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>深拷贝句柄内容，返回一个独立的新句柄。</summary>
	/// <returns>新 JlHandle，为原句柄内容的独立副本；需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>通过 SerializeHandle() 把 this 序列化到内存字节，再 new JlHandle() + DeserializeHandle 反序列化回来，得到与源句柄互不影响的副本。</para>
	///   <para><b>与相邻成员的取舍</b>Clone 走完整序列化，开销更大但得到真正独立副本；只需浅引用时用拷贝构造函数 JlHandle(JlHandle)。底层原生对象是否共享引用计数不在本方法语义内 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	/// using (Stream fs = File.OpenRead("item.bin"))
	/// {
	///     JlHandle h = JlHandle.Deserialize(fs);
	///     JlHandle copy = h.Clone();
	///     // h 与 copy 各自独立
	///     copy.Dispose();
	///     h.Dispose();
	/// }
	/// </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose；Clone 只读，不改变 this。</para>
	/// </remarks>
	public JlHandle Clone()
	{
		byte[] data = SerializeHandle();
		JlHandle obj = new JlHandle();
		obj.DeserializeHandle(data);
		return obj;
	}

	/// <summary>
	///   清空 this 所指原生句柄的内容（原生 id 2011）：把 this 存进第 0 输入位后调用，不走 Load，故 C# 侧 mHandle 仍指向同一句柄 id；无参数、无返回值。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>PreCall(2011)，把 this 存入第 0 个输入参数位后调用原生过程清空其内容；不调用 Load，故 C# 侧 mHandle 仍指向同一个原生句柄 id。</para>
	///   <para><b>与相邻成员的取舍</b>与 Dispose 不同：Dispose 会释放并让句柄回到未初始化（UNDEF），ClearHandle 只清内容不动 C# 句柄变量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	/// using (Stream fs = File.OpenRead("item.bin"))
	/// {
	///     JlHandle h = JlHandle.Deserialize(fs);
	///     h.ClearHandle();   // 清空句柄指向的内容
	///     h.Dispose();
	/// }
	/// </code>
	///   <para><b>资源与坑</b>GC.KeepAlive(this) 保证原生调用结束前句柄不被终结；清空后 IsInitialized() 的实际取值取决于底层实现 [待实测]。</para>
	/// </remarks>
	public void ClearHandle()
	{
		IntPtr proc = JlNativeApi.PreCall(2011);
		Store(proc, 0);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用序列化字节原地重建本句柄（原生 id 2012）：入口先 Dispose 丢掉旧内容，装载结果写回 this 自身，不返回新对象。
	/// </summary>
	/// <param name="serializedItem">完整的序列化项字节（含 16 字节头），通常来自 SerializeHandle()；反序列化结果原地写回本句柄。</param>
	/// <remarks>
	///   <para><b>功能说明</b>PreCall(2012)：先把字节包成 JlSerializationBuffer（其构造经原生 op 404 钉住 data 生成 serialized_item 句柄），Store 到第 0 位并 InitOCT(0)，调用后用 Load(proc,0,err) 把结果原地写回 this。</para>
	///   <para><b>关键前提</b>方法第一步即 Dispose() 丢弃 this 原内容——因为基类 Load 要求当前 mHandle==UNDEF，否则抛 "Undisposed handle instance when loading output parameter"。故这是对已有句柄的原地改写，且会牺牲其旧对象。</para>
	///   <para><b>用法</b></para>
	///   <code>
	/// using (Stream fs = File.OpenRead("item.bin"))
	/// using (BinaryReader br = new BinaryReader(fs))
	/// {
	///     byte[] data = br.ReadBytes((int)fs.Length);
	///     using (JlHandle h = new JlHandle())
	///     {
	///         h.DeserializeHandle(data);   // 原地改写 h
	///     }
	/// }
	/// </code>
	///   <para><b>资源与坑</b>失败时 this 变空句柄；JlSerializationBuffer 用 using 释放；new JlHandle() 得到 UNDEF 占位后由本方法填充。</para>
	/// </remarks>
	public void DeserializeHandle(byte[] serializedItem)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(2012);
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItem);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>
	///   把 this 所指句柄的内容导出为库自有二进制的完整序列化字节（原生 id 2015），返回托管 byte[]；只读操作，不改变 this 指向的内容。
	/// </summary>
	/// <returns>完整序列化项字节数组（含 16 字节头），可被 DeserializeHandle/ReadFromStream 读回；托管数组，无需手动释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>PreCall(2015)：Store(this,0) 作输入，InitOCT(0) 声明第 0 位为输出控制，调用后由 JlSerializationBuffer.LoadBytes 取出该输出句柄并转成 byte[]（内部 LoadNew→ToBytes→Dispose 临时缓冲句柄）。</para>
	///   <para><b>与相邻成员的取舍</b>要落盘/走流用 Serialize(Stream)；只要内存字节用本方法。二者输出格式一致，可配对 DeserializeHandle。</para>
	///   <para><b>用法</b></para>
	///   <code>
	/// using (Stream fs = File.OpenRead("item.bin"))
	/// {
	///     JlHandle h = JlHandle.Deserialize(fs);
	///     byte[] data = h.SerializeHandle();
	///     using (Stream dst = File.Create("copy.bin"))
	///     {
	///         dst.Write(data, 0, data.Length);
	///     }
	///     h.Dispose();
	/// }
	/// </code>
	///   <para><b>资源与坑</b>GC.KeepAlive(this) 保证调用期间 this 存活；本方法只读，不改变 this 内容。</para>
	/// </remarks>
	public byte[] SerializeHandle()
	{
		IntPtr proc = JlNativeApi.PreCall(2015);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   判断 this 所指句柄的内容能否序列化（原生 id 2018，INTEGER 装载）：以 int 返回 1 可、0 不可，不是 C# bool。
	/// </summary>
	/// <returns>布尔以 int 表示：1 可序列化、0 不可（并非 C# bool）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>PreCall(2018)：Store(this,0) + InitOCT(0)，调用后 LoadI 取回 INTEGER 输出（只读第一个值）。方法名带 Tuple 但作用于单个 JlHandle 的内容，与元组本身无关。</para>
	///   <para><b>用法</b></para>
	///   <code>
	/// using (Stream fs = File.OpenRead("item.bin"))
	/// {
	///     JlHandle h = JlHandle.Deserialize(fs);
	///     int ok = h.TupleIsSerializable();
	///     h.Dispose();
	/// }
	/// </code>
	///   <para><b>资源与坑</b>返回 int 需按 0/1 判读；不可序列化的句柄调用 Serialize/SerializeHandle 会失败。</para>
	/// </remarks>
	public int TupleIsSerializable()
	{
		IntPtr proc = JlNativeApi.PreCall(2018);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   以元组形式返回 this 所指句柄的有效性标记（原生 id 2020，按 INTEGER 装载）：每元素 1 有效、0 无效，是新建的 JlTuple，用完需 Dispose。
	/// </summary>
	/// <returns>新 JlTuple，每元素为 1（有效句柄）或 0（无效）；需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>PreCall(2020)：Store(this,0) + InitOCT(0)，调用后用 JlTuple.LoadNew(...,INTEGER,...) 以整数装载有效性输出。返回元组而非标量，便于批量表达句柄数组的有效性。方法名带 Tuple，作用于此句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	/// using (Stream fs = File.OpenRead("item.bin"))
	/// {
	///     JlHandle h = JlHandle.Deserialize(fs);
	///     JlTuple v = h.TupleIsValidHandle();
	///     v.Dispose();
	///     h.Dispose();
	/// }
	/// </code>
	///   <para><b>资源与坑</b>JlTuple 实现了 IDisposable，返回的新元组需 Dispose；1/0 约定见返回值说明。</para>
	/// </remarks>
	public JlTuple TupleIsValidHandle()
	{
		IntPtr proc = JlNativeApi.PreCall(2020);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   取 this 所指句柄的语义类型名（原生 id 2021，按字符串装载并只读第一个值），用来判断这个通用 JlHandle 实际承载的是哪一类对象。
	/// </summary>
	/// <returns>语义类型字符串，如代码中可见的 "serialized_item"；其余取值随对象类别而定 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>PreCall(2021)：Store(this,0) + InitOCT(0)，调用后 LoadS 取回字符串输出（只读第一个值）。可用于判断一个通用 JlHandle 实际承载的对象族；方法名带 Tuple，作用于此句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	/// using (Stream fs = File.OpenRead("item.bin"))
	/// {
	///     JlHandle h = JlHandle.Deserialize(fs);
	///     string sem = h.TupleSemType();
	///     h.Dispose();
	/// }
	/// </code>
	///   <para><b>资源与坑</b>JlHandle 自身的私有 AssertSemType() 为空实现、不做校验，真正的语义校验在派生封装里（如 JlSerializationBuffer 断言 "serialized_item"）；本方法只读取类型不改变状态。</para>
	/// </remarks>
	public string TupleSemType()
	{
		IntPtr proc = JlNativeApi.PreCall(2021);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadS(proc, 0, err, out var stringValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return stringValue;
	}
}
