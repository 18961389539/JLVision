using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>表示一个 XLD 轮廓对象（或对象数组）的实例。</summary>
[Serializable]
public class JlXLDCont : JlXLD, ISerializable, ICloneable
{
	/// <summary>按 1 起始的序号取出本轮廓数组中的一条或多条轮廓，转调 <see cref="SelectObj(JlTuple)"/>。</summary>
	/// <param name="index">轮廓序号元组，1 起始，合法取值 1..CountObj()。Default: 1</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>属性不发原生调用，只做 <c>SelectObj(index)</c> 转发，返回的是新句柄，因此取出的轮廓与本数组此后互不影响。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>序号基数是 1（与 <c>SelectObj</c>、<c>CopyObj</c> 的 "Starting index … 1" 一致），<c>contours[0]</c> 取不到第一条 [待实测：0、负数与越界序号是抛 JlOperatorException 还是返回未初始化对象]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只取一条用 <c>this[int]</c> 重载；按条件筛用 <c>SelectContoursXld</c> 或 <c>SelectShapeXld</c>；只要首/末若干条用 <c>CopyObj</c>。在托管侧逐条取出再 <c>ConcatObj</c> 拼回比这些做法多一次句柄拷贝。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlXLDCont contours = new JlXLDCont(new JlTuple(10.0, 20.0, 30.0), new JlTuple(10.0, 40.0, 25.0));
	///   JlXLDCont first = contours[1];
	///   JlXLDCont two = contours[new JlTuple(1, 2)];
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>每次索引都产生新对象，需各自 <c>Dispose()</c>；<c>JlXLDCont</c> 与 <c>JlRegion</c>、<c>JlImage</c> 一样实现 <c>IDisposable</c>，放在 <c>using</c> 里最省事。</para>
	/// </remarks>
	public new JlXLDCont this[JlTuple index] => SelectObj(index);

	/// <summary>创建一个未初始化（空）的轮廓数组句柄，不发任何原生调用。</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>直接以 <c>JlObjectBase.UNDEF</c> 句柄构造（<c>copy: false</c>，不做句柄复制），得到"存在但没有内容"的 XLD 轮廓数组，<c>CountObj()</c> 为 0。它不是图像对象，只是轮廓容器。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>空句柄不能作为读取类算子的轮廓输入；但它是本类多处流程的必需中间态：<c>Deserialize(Stream)</c> 内部先构造本空对象再装载，各 <c>Gen*ContourXld</c>/<c>Read*</c> 原地生成族也常以 <c>new JlXLDCont()</c> 作为待填充容器。</para>
	///   <para><b>与相邻构造器的取舍</b></para>
	///   <para>要一条有内容的轮廓用 <c>JlXLDCont(JlTuple, JlTuple)</c> 或区域版构造器；要"先声明、后原地生成"用本构造器配 Gen 方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlXLDCont buffer = new JlXLDCont())
	///   {
	///       buffer.GenRectangle2ContourXld(300.0, 200.0, 0.0, 100.5, 20.5);
	///       int n = buffer.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>空对象同样占原生句柄，用完 <c>Dispose()</c>；对未初始化对象直接取点坐标会以算子异常失败 [待实测：异常类别与错误码]。</para>
	/// </remarks>
	public JlXLDCont()
		: base(JlObjectBase.UNDEF, copy: false)
	{
	}

	/// <summary>用已存在的原生句柄建立轮廓数组视图：转调 <c>this(key, copy: true)</c> 做引用计数拷贝，随后断言句柄类别为 xld_cont；不发任何算子调用。</summary>
	/// <param name="key">本进程内有效的原生对象句柄，实际类别须属 xld_cont；<c>IntPtr.Zero</c>（<c>JlObjectBase.UNDEF</c>）按未初始化空句柄处理并跳过类别断言。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>拷贝语义落在基类 <c>JlObjectBase(IntPtr, bool)</c>：仅当 copy=true 且 key 不是 UNDEF、不是 UNDEF2（IntPtr(1)，会被规整为 UNDEF）时才执行 <c>JlNativeApi.CopyObject(key)</c>，即新对象与传入句柄共享底层轮廓数据、各持一份引用计数。构造后 <c>AssertObjectClass()</c> 发原生 get_obj_class（id 579）查询类别，类名既不以 "xld_cont" 开头又不是 "any" 时抛 <c>JlException</c>；key 为 UNDEF 时整条断言短路，不产生任何原生调用。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>带 <c>[EditorBrowsable(EditorBrowsableState.Never)]</c> 的内部/互操作入口，供句柄跨边界传递使用；日常建模改用 <see cref="JlXLDCont(JlTuple, JlTuple)"/> 或各 <c>Gen*ContourXld</c> 族。传入已释放或无效指针时行为由原生层决定 [待实测：报错形态是异常还是原生崩溃]。</para>
	///   <para><b>与相邻构造器的取舍</b></para>
	///   <para>引用计数拷贝不是深拷贝：任何一侧被原地生成族改写，另一侧可见；要彻底解耦用 <see cref="Clone()"/>，从 C# 对象拷贝用 <see cref="JlXLDCont(JlObject)"/>，接管裸句柄（不增引用）用 <c>JlXLDCont(IntPtr, bool)</c> 传 copy=false。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlXLDCont empty = new JlXLDCont(JlObjectBase.UNDEF))
	///   {
	///       int n = empty.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>本实例 <c>Dispose()</c> 只减自己那一份引用计数，传进来的原 key 何时释放仍由调用方负责，两边互不顶替；UNDEF 入参时本构造器实效上等价于 <c>new JlXLDCont()</c>（copy 对 UNDEF 短路，不做句柄复制）。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDCont(IntPtr key)
		: this(key, copy: true)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>所有"接管/拷贝"路径的公共落点：由 <paramref name="copy"/> 决定对原生句柄 <paramref name="key"/> 做引用计数拷贝还是直接接管裸句柄，随后断言类别为 xld_cont。</summary>
	/// <param name="key">原生对象句柄；<c>IntPtr.Zero</c>（UNDEF）表示未初始化，<c>IntPtr(1)</c>（UNDEF2）被规整为 UNDEF。</param>
	/// <param name="copy">true：对 key 执行 <c>CopyObject</c>，得到共享底层数据、独立引用计数的句柄；false：不增引用，key 原样存为本实例句柄，释放责任随之转移。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>透传基类 <c>JlObjectBase(IntPtr, bool)</c>：仅当 copy=true 且 key 非 UNDEF 非 UNDEF2 才真正 <c>CopyObject</c>，否则按原样存储。构造后发原生 get_obj_class（id 579）断言类名以 "xld_cont" 开头或为 "any"，不符抛 <c>JlException</c>；UNDEF 句柄跳过断言。全程不调任何算子。</para>
	///   <para><b>约束或前提</b></para>
	///   <para><c>[EditorBrowsable(EditorBrowsableState.Never)]</c> 互操作用。copy=false 时句柄所有权转移到本实例：本对象 Dispose 负责关闭该 key，调用方不得再对同一 key 释放或再包一个会 Dispose 的对象，否则双重释放；反之 copy=true 时两边各持一份引用，底层数据在最后一个持有者释放后才回收。</para>
	///   <para><b>与相邻构造器的取舍</b></para>
	///   <para>单参重载 <c>JlXLDCont(IntPtr)</c> 就是本构造器 copy=true 的特化；从 C# 对象复制用 <see cref="JlXLDCont(JlObject)"/>；要独立数据用 <see cref="Clone()"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   IntPtr key = JlObjectBase.UNDEF;
	///   JlXLDCont placeholder = new JlXLDCont(key, copy: false);
	///   int n = placeholder.CountObj();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>参数名 copy 容易误读成"深拷贝"——它只复制句柄引用计数，点数据始终共享；<c>GC.KeepAlive(this)</c> 保证句柄装配完成前实例不被回收。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDCont(IntPtr key, bool copy)
		: base(key, copy)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>从另一个 <c>JlObject</c> 构造：对源句柄做引用计数拷贝（clone，非深拷贝），并断言其实际类别是 xld_cont——也是把未知来源的 JlObject 引用重新当作具体轮廓的入口。</summary>
	/// <param name="obj">被拷贝的源对象；其内部句柄以 <c>copy=true</c> 走 <c>CopyObject</c> 引用一份。不得为 null；静态类型可以是 <c>JlObject</c> 或 <c>JlXLD</c>，但承载的原生句柄类别必须是 xld_cont。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>透传基类 <c>JlObjectBase(JlObjectBase)</c>（即 <c>this(obj.key, copy: true)</c>），得到共享底层点数据、独立引用计数的新句柄；随后 <c>AssertObjectClass()</c> 发原生 get_obj_class（id 579），源句柄若实为区域、图像等非 xld_cont 对象则抛 <c>JlException</c>（expected xld_cont）。不发算子调用。</para>
	///   <para><b>约束或前提</b></para>
	///   <para><c>[EditorBrowsable(EditorBrowsableState.Never)]</c>。obj 为 null 时访问其句柄立即抛 <c>NullReferenceException</c>。新对象与源共享数据：任何一侧被原地生成族改写，另一侧同步可见。</para>
	///   <para><b>与相邻构造器的取舍</b></para>
	///   <para>要两份互不干扰的轮廓用 <see cref="Clone()"/>（序列化往返的深拷贝，代价随总点数增长）；只要数组中的若干条用 <c>this[index]</c>/<c>SelectObj</c>；只要"同一份数据再多一个句柄"才是本构造器。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlXLDCont src = new JlXLDCont(new JlTuple(10.0, 20.0, 30.0), new JlTuple(10.0, 40.0, 25.0)))
	///   {
	///       JlObject asObj = src;
	///       using (JlXLDCont twin = new JlXLDCont(asObj))
	///       {
	///           int n = twin.CountObj();
	///       }
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>src 与 twin 各自 <c>Dispose()</c>，底层数据在最后一个持有者释放后才回收；类别断言意味着对纯数值/字符串对象无意义，只针对图标句柄。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDCont(JlObject obj)
		: base(obj)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	private void AssertObjectClass()
	{
		JlNativeApi.AssertObjectClass(key, "xld_cont");
	}

	/// <summary>"返回新句柄"的装载通道：以 UNDEF 新建一个轮廓数组对象，把原生过程输出槽 <paramref name="parIndex"/> 里的句柄接管进去，透传错误码；不发 <c>PostCall</c>、不抛算子异常。</summary>
	/// <param name="proc">当前原生过程句柄（<c>JlNativeApi.PreCall</c> 取得，尚未 <c>PostCall</c>）。</param>
	/// <param name="parIndex">输出对象参数在原生侧的槽位索引；本类内部调用点见到的取值均为 1。</param>
	/// <param name="err"><c>CallProcedure</c> 的返回码：失败时不触碰输出槽、原样返回。</param>
	/// <param name="obj">输出。成功＝持有输出句柄的新 JlXLDCont；失败＝仍为 UNDEF 的空句柄对象。</param>
	/// <returns>透传的错误码：<paramref name="err"/> 已失败则原样返回，否则返回 <c>GetOutputObject</c> 的取句柄结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>实现 = <c>new JlXLDCont(JlObjectBase.UNDEF)</c> + 基类 <c>Load(proc, parIndex, err)</c>。UNDEF 使拷贝构造短路（不 CopyObject、跳过 xld_cont 类别断言），因此第一步零原生调用；<c>Load</c> 要求 key 必须是 UNDEF 否则抛 <c>JlException</c>，err 已失败则直接返回，成功才 <c>GetOutputObject(proc, parIndex, out key)</c> 接管输出句柄。注意该路径拿到句柄后不再断言类别——算子输出声明的是什么类就装什么类。</para>
	///   <para><b>约束或前提</b></para>
	///   <para><c>[EditorBrowsable(EditorBrowsableState.Never)]</c>，供各"返回新轮廓数组"的算子方法内部使用：固定顺序为 <c>InitOCT(proc, 1)</c> →（可选 <c>UnpinTuple</c>）→ <c>CallProcedure</c> → <c>LoadNew(proc, 1, err, out obj)</c> → <c>PostCall(proc, err)</c>；错误升级为 <c>JlOperatorException</c> 发生在调用方的 <c>PostCall</c>，不在本方法。调用前必须已 InitOCT 初始化输出对象槽，否则取句柄失败。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>元组输出走 <c>JlTuple.LoadNew</c>，句柄类输出才走本重载；原地改写族（各 <c>Gen*ContourXld</c>、<c>DeserializeXld</c>）用 <c>Load</c> 写回既有实例，不产生新句柄。</para>
	///   <para><b>资源与坑</b></para>
	///   <para>成功时 <paramref name="obj"/> 是需调用方单独 <c>Dispose()</c> 的新句柄；失败时它是空句柄但同样占用托管对象，继续当有效轮廓用会在后续算子处报错。各算子末尾的 <c>GC.KeepAlive(this)</c> 保证输入轮廓在原生调用结束前不被回收。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadNew(IntPtr proc, int parIndex, int err, out JlXLDCont obj)
	{
		obj = new JlXLDCont(JlObjectBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	/// <summary>从输入区域逐区域生成边界轮廓并装入正在构造的本实例（原地装入、不返回新句柄），区域对象不被改动；边界走边界像素的中心，一个区域含多个连通域则输出多条轮廓。</summary>
	/// <param name="regions">输入区域，逐区域生成边界轮廓。</param>
	/// <param name="mode">轮廓生成方式。Default: "border"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 70：<c>Store(regions)</c> + <c>StoreS(mode)</c> + <c>InitOCT(1)</c> + <c>Load</c>，结果装进正在构造的本实例；区域对象不被改动。一个区域若含多个连通域则输出多条轮廓。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>边界轮廓走的是边界像素的<b>中心</b>，不是像素几何外沿，所以由它再 <c>GenRegionContourXld("filled")</c> 填回来的区域与原区域在半像素意义下不完全重合；用 <c>AreaCenter</c> 比对面积时会看到系统性偏差。mode 字符串不在托管侧校验，非法规格由原生层报错 [待实测：mode 取值集合，以及孔洞边界是否需要额外规格]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>想要"点数固定、角点精确"的几何轮廓，用 <c>GenRectangle2ContourXld</c>/<c>GenCircleContourXld</c> 直接按参数生成；想从灰度图拿亚像素边，用 <c>JlImage.EdgesSubPix</c>。本构造器只适合"已经把区域当作对象"的流程。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\part.pgm");
	///   JlRegion reg = img.Threshold(80.0, 255.0);
	///   JlXLDCont contours = new JlXLDCont(reg, "border");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>本实例、<c>reg</c>、<c>img</c> 都要 <c>Dispose()</c>；构造完成后本实例若已有句柄会被 <c>Load</c> 覆盖，旧句柄的释放时机依赖原生层 [待实测]。</para>
	/// </remarks>
	public JlXLDCont(JlRegion regions, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(70);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
	}

	/// <summary>用一组等长的 (row, col) 点序列构造一条轮廓。</summary>
	/// <param name="row">轮廓各点的行坐标（y，向下为正，单位像素）。Default: [0,1,2,2,2]</param>
	/// <param name="col">轮廓各点的列坐标（x，向右为正，单位像素），个数须与 row 相同。Default: [0,0,0,1,2]</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 72：<c>Store(row)</c> + <c>Store(col)</c> + <c>InitOCT(1)</c> + <c>Load</c>，点数据装进正在构造的本实例，属"原地生成"而非返回新句柄；两个元组在原生调用后即 <c>UnpinTuple</c>。生成的是一条开口轮廓，点序即输入顺序，首末点不自动重合。</para>
	///   <para><b>约束或前提</b></para>
	///   <para><c>row</c> 与 <c>col</c> 逐点配对为 (row[i], col[i])，长度必须相等 [待实测：长度不匹配时报错还是按短者截断]。少于 2 点时轮廓退化。</para>
	///   <para><b>与相邻构造器的取舍</b></para>
	///   <para>从区域拿边界用 <see cref="JlXLDCont(JlRegion, string)"/>；规则几何（圆/椭圆/矩形）用对应 <c>Gen*ContourXld</c>，采样密度可控；本构造器适合"点序列已在手"的场景（拟合输出、外部 CAD/测量数据转轮廓）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlXLDCont tri = new JlXLDCont(new JlTuple(10.0, 10.0, 60.0), new JlTuple(10.0, 60.0, 35.0)))
	///   {
	///       JlTuple num = tri.ContourPointNumXld();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para><c>JlTuple</c> 接受 double 数组的隐式转换，传 <c>new double[] { 10.0, 10.0, 60.0 }</c> 同样合法；本实例用完需 <c>Dispose()</c>。</para>
	/// </remarks>
	public JlXLDCont(JlTuple row, JlTuple col)
	{
		IntPtr proc = JlNativeApi.PreCall(72);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, col);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(col);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeXld();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>ISerializable 反序列化构造器：从 <see cref="SerializationInfo"/> 取 "data" 字节字段交给 <c>DeserializeXld</c>（原生 id 1552），把轮廓数组原地装进正在构造的本实例。</summary>
	/// <param name="info">序列化数据容器，必须含名为 "data"、类型为 <c>byte[]</c> 的字段（由本类 <c>GetObjectData</c> 写入，内容是 <see cref="SerializeXld()"/> 的原生字节）。</param>
	/// <param name="context">流上下文；实现完全不读取它，仅为序列化构造器的固定签名。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>与 <c>ISerializable.GetObjectData</c> 严格配对：序列化端 <see cref="SerializeXld()"/> 发原生 id 1553，把本实例索引 1 的轮廓装成 <c>byte[]</c> 存进 "data"；反序列化端 <see cref="DeserializeXld(byte[])"/> 发原生 id 1552——<c>JlSerializationBuffer</c> 包字节、<c>Store(proc, 0, buffer)</c> + <c>InitOCT(proc, 1)</c> + <c>CallProcedure</c> 后 <c>Load(proc, 1, err)</c>，结果写进本实例，是原地装载而非返回第二个句柄；它内部先 <c>Dispose()</c> 再覆盖。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>带 <c>[EditorBrowsable(EditorBrowsableState.Never)]</c>，只由 BinaryFormatter 系序列化基础设施回调触发 [待实测：当前运行时下该整条链路的可用性]。"data" 字段缺失或类型不是 <c>byte[]</c> 时在 <c>GetValue</c> 处抛异常 [待实测：具体异常类型]。</para>
	///   <para><b>与相邻能力的取舍</b></para>
	///   <para>业务代码搬轮廓用 <c>Serialize(Stream)</c> 与 <see cref="Deserialize(Stream)"/>（同一份原生序列化字节加库自有头部）；内存里传字节用 <see cref="SerializeXld()"/>/<see cref="DeserializeXld(byte[])"/>；给第三方交换用 DXF 族。本构造器只是"整个对象进 .NET 序列化"这条通道的落地点。</para>
	///   <para><b>资源与坑</b></para>
	///   <para><c>DeserializeXld</c> 的先行 <c>Dispose()</c> 意味着构造出的实例旧内容一律作废；原生调用结束前有 <c>GC.KeepAlive(buffer)</c> 钉住序列化缓冲，属性是否随字节完整往返 [待实测：与序列化前逐属性比对]。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlXLDCont(SerializationInfo info, StreamingContext context)
	{
		DeserializeXld((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把本实例轮廓按库自有二进制格式写入可写流（先取原生字节再落流），流里带私有头部、只能由 Deserialize 读回，不是可读文本格式；不关闭流也不回绕流位置。</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>实现是 <c>SerializeXld()</c> 先取原生字节，再 <c>JlSerializationBuffer.WriteToStream</c> 落流；流里带库自有头部，因此只能由 <c>Deserialize(Stream)</c> 读回，不是可读的文本格式。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>要求流可写；本方法不关闭也不回绕流的位置，跨进程或写盘后要再读需自行把 <c>Position</c> 归零。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只要内存字节（送队列、存数据库字段）用 <c>SerializeXld()</c>；要落文件用本方法配 <c>FileStream</c>；想把轮廓点导成可读文本给别的软件，用 <c>WriteContourXldDxf</c> 或 <c>WriteContourXldArcInfo</c>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   using System.IO;
	///   JlXLDCont c = new JlXLDCont(new JlTuple(10.0, 20.0, 30.0), new JlTuple(10.0, 40.0, 25.0));
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       c.Serialize(ms);
	///       ms.Position = 0;
	///       JlXLDCont back = JlXLDCont.Deserialize(ms);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para><c>Deserialize</c> 返回的是新对象，需单独 <c>Dispose()</c>；轮廓上的属性是否随二进制一起走未在托管侧体现 [待实测]。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeXld(), stream);
	}

	/// <summary>从库自有二进制流反序列化出一个新的轮廓数组。</summary>
	/// <param name="stream">由 <see cref="Serialize(Stream)"/> 写出的可读流；当前位置须停在对象数据起点。</param>
	/// <returns>新建的 <c>JlXLDCont</c> 句柄（不是原地改写本实例，本方法为静态）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>实现 = 构造空对象 + <c>JlSerializationBuffer.ReadFromStream(stream)</c> + <c>DeserializeXld</c>：不发算子调用，走的是原生序列化通道。与 <c>Serialize(Stream)</c> 成对，流内含库自有头部，只能由本库读回，不是可读文本格式。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>流里必须是本库 <c>Serialize</c> 写出的同类型对象数据；用别的对象的字节喂本方法会以异常失败 [待实测：异常类别]。本方法不关闭流。</para>
	///   <para><b>与相邻能力的取舍</b></para>
	///   <para>在内存里搬字节（消息队列、数据库字段）用 <c>SerializeXld()</c>/<c>DeserializeXld(byte[])</c>；给 CAD/GIS 等第三方交换轮廓用 DXF/ARC/INFO 的 <c>Write*</c>/<c>Read*</c> 族；跨版本长期归档不建议依赖本二进制格式。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using System.IO;
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       using (JlXLDCont src = new JlXLDCont(new JlTuple(10.0, 20.0, 30.0), new JlTuple(10.0, 40.0, 25.0)))
	///       {
	///           src.Serialize(ms);
	///       }
	///       ms.Position = 0;
	///       using (JlXLDCont back = JlXLDCont.Deserialize(ms))
	///       {
	///           int n = back.CountObj();
	///       }
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值是新对象，必须自行 <c>Dispose()</c>；<c>Serialize</c> 写流后不自动回绕位置，读之前要像示例那样把 <c>Position</c> 归零。</para>
	/// </remarks>
	public new static JlXLDCont Deserialize(Stream stream)
	{
		JlXLDCont hXLDCont = new JlXLDCont();
		hXLDCont.DeserializeXld(JlSerializationBuffer.ReadFromStream(stream));
		return hXLDCont;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>实现 = <c>SerializeXld()</c> + <c>new JlXLDCont()</c> + <c>DeserializeXld(data)</c>，即两次原生调用换一份与源对象彻底解耦的点数据。改副本不会影响原件，反之亦然。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只要"同一数组的另一份句柄"时用 <c>CopyObj(1, -1)</c>（原生 id 568，一次调用，numObj 传 -1 表示全部）更省；<c>Clone</c> 走托管字节缓冲，代价随轮廓总点数线性增长。浅复制（只多一个句柄引用）不要用本方法。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlXLDCont raw = new JlXLDCont(new JlTuple(10.0, 20.0, 30.0), new JlTuple(10.0, 40.0, 25.0));
	///   JlXLDCont frozen = raw.Clone();
	///   JlXLDCont sorted = raw.SortContoursXld("upper_left", "true", "row");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回新对象必须 <c>Dispose()</c>；<c>Clone</c> 保持数组内轮廓的原有顺序，下标与源一致，这点与 <c>SortContoursXld</c>/<c>SelectContoursXld</c> 重排下标不同。</para>
	/// </remarks>
	public new JlXLDCont Clone()
	{
		byte[] data = SerializeXld();
		JlXLDCont obj = new JlXLDCont();
		obj.DeserializeXld(data);
		return obj;
	}

	/// <summary>按开口轮廓端点处的切线方向把轮廓连成一条：先在两端跳过 fitClippingLength、再在随后的 fitLength 段上拟合方向，端点距离、垂直于切线的偏移、切线夹角与重叠长度四项阈值同时满足才连；LoadNew 返回合并后的新句柄，原轮廓数组不被改动、条数变少且不保留输入下标。</summary>
	/// <param name="fitClippingLength">求切线时跳过的轮廓段长度。Default: 0.0</param>
	/// <param name="fitLength">求切线时使用的轮廓段长度。Default: 30.0</param>
	/// <param name="maxTangAngle">两条轮廓切线间的最大角度差。Default: 0.78539816</param>
	/// <param name="maxDist">轮廓端点间的最大距离。Default: 25.0</param>
	/// <param name="maxDistPerp">轮廓端点在垂直于其切线方向上的最大距离。Default: 10.0</param>
	/// <param name="maxOverlap">两条轮廓的最大重叠。Default: 2.0</param>
	/// <param name="mode">描述轮廓属性处理方式的模式。Default: "attr_forget"</param>
	/// <returns>输出的 XLD 轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>（本重载为主，<c>double fitLength</c> 版本见其后）</para>
	///   <para>原生 id 0。输入是本实例（<c>Store(proc,1)</c>），输出走 <c>LoadNew</c> 返回新句柄，原轮廓数组不被改动。判据是端点处的切线：先在两端各跳过 <c>fitClippingLength</c> 长度，再在随后的 <c>fitLength</c> 段上拟合方向，两条轮廓需同时满足端点距离、垂直于切线的偏移、切线夹角与重叠长度四项阈值才被连成一条。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>只有开口轮廓有"端点切线"可言，闭合轮廓在此不与其他轮廓相连 [待实测：闭合轮廓是被原样透传还是被丢弃]。<c>fitClippingLength + fitLength</c> 之和不应超过轮廓自身长度，否则方向拟合不出 [待实测：此时该轮廓的处理方式]。</para>
	///   <para><b>参数取向</b></para>
	///   <para><c>fitClippingLength</c> 用来躲开 <c>EdgesSubPix</c> 在图像边界处截断出的坏端点；<c>fitLength</c> 越长方向越稳，但会把弯线上的端点方向"平均"掉，曲率大的轮廓应给短值；<c>maxTangAngle</c> 收紧可避免把折线拐角误连，放宽则连得激进；<c>maxDistPerp</c> 比 <c>maxDist</c> 更管"横向错开"，两条几乎同向但错开几像素的轮廓靠它挡。</para>
	///   <para>元组重载的 <c>fitLength</c> 可给多值以逐轮廓设定 [待实测：多值长度与轮廓条数的对应/广播规则]。注意传字面量 <c>30.0</c> 会绑定到 <c>double</c> 重载，需要显式写 <c>new JlTuple(30.0)</c>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\edge.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlXLDCont joined = edges.UnionCotangentialContoursXld(0.0, new JlTuple(30.0), 0.78539816, 25.0, 10.0, 2.0, "attr_forget");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>合并后轮廓条数一般小于输入条数且不保留输入下标，按 <c>ContourPointNumXld()</c> 顺序缓存的 per-contour 元组必须重算，不能用 <c>result[i]</c> 配 <c>input[i]</c>。<c>mode="attr_forget"</c> 时合并结果不再带 <c>DistanceContoursXld</c> 等写入的属性。连接处的点是直线桥还是延续原采样 [待实测]。返回值需 <c>Dispose()</c>。</para>
	/// </remarks>
	public JlXLDCont UnionCotangentialContoursXld(double fitClippingLength, JlTuple fitLength, double maxTangAngle, double maxDist, double maxDistPerp, double maxOverlap, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(0);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, fitClippingLength);
		JlNativeApi.Store(proc, 1, fitLength);
		JlNativeApi.StoreD(proc, 2, maxTangAngle);
		JlNativeApi.StoreD(proc, 3, maxDist);
		JlNativeApi.StoreD(proc, 4, maxDistPerp);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.StoreS(proc, 6, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(fitLength);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按端点切线合并轮廓的标量便捷重载（同原生 id 0）：fitLength 走 StoreD 全局单值、所有轮廓共用一个拟合长度，同样返回新句柄，"条数变少、不能按输入下标配对"的坑与主重载一致。</summary>
	/// <param name="fitClippingLength">求切线时跳过的轮廓段长度。Default: 0.0</param>
	/// <param name="fitLength">求切线时使用的轮廓段长度。Default: 30.0</param>
	/// <param name="maxTangAngle">两条轮廓切线间的最大角度差。Default: 0.78539816</param>
	/// <param name="maxDist">轮廓端点间的最大距离。Default: 25.0</param>
	/// <param name="maxDistPerp">轮廓端点在垂直于其切线方向上的最大距离。Default: 10.0</param>
	/// <param name="maxOverlap">两条轮廓的最大重叠。Default: 2.0</param>
	/// <param name="mode">描述轮廓属性处理方式的模式。Default: "attr_forget"</param>
	/// <returns>输出的 XLD 轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>标量便捷重载，语义与 <see cref="UnionCotangentialContoursXld(double, JlTuple, double, double, double, double, string)"/> 完全一致（同一原生 id 0，同样返回新句柄、同样有"条数变少、不能按输入下标配对"的坑）。</para>
	///   <para><b>与主重载的实际差异</b></para>
	///   <para><c>fitLength</c> 用 <c>StoreD</c> 而非 <c>Store</c>+<c>UnpinTuple</c>：所有轮廓共用一个长度，无法逐轮廓设定；对长短差异很大的轮廓混合场景，主重载给多条值更合适。传 <c>30.0</c> 这类字面量时绑定到本重载。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\edge.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont joined = edges.UnionCotangentialContoursXld(0.0, 30.0, 0.78539816, 25.0, 10.0, 2.0, "attr_keep"))
	///   {
	///       int n = joined.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值需 <c>Dispose()</c>；<c>mode="attr_keep"</c> 才保留轮廓属性。</para>
	/// </remarks>
	public JlXLDCont UnionCotangentialContoursXld(double fitClippingLength, double fitLength, double maxTangAngle, double maxDist, double maxDistPerp, double maxOverlap, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(0);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, fitClippingLength);
		JlNativeApi.StoreD(proc, 1, fitLength);
		JlNativeApi.StoreD(proc, 2, maxTangAngle);
		JlNativeApi.StoreD(proc, 3, maxDist);
		JlNativeApi.StoreD(proc, 4, maxDistPerp);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.StoreS(proc, 6, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}





	/// <summary>把 NURBS 曲线（控制多边形+节点矢量+权因子）离散化成 XLD 点列：maxError 是点列对原曲线的最大偏差（像素）、maxDistance 是相邻采样点最大间距；先 Dispose 后 Load，结果原地写入本实例、不返回新对象。</summary>
	/// <param name="rows">控制多边形的行坐标。</param>
	/// <param name="cols">控制多边形的列坐标。</param>
	/// <param name="knots">节点向量 u@f$u$。Default: "auto"</param>
	/// <param name="weights">权重向量 w@f$w$。Default: "auto"</param>
	/// <param name="degree">NURBS 曲线的次数 p@f$p$。Default: 3</param>
	/// <param name="maxError">NURBS 曲线与其逼近之间的最大距离。Default: 1.0</param>
	/// <param name="maxDistance">相邻两个轮廓点之间的最大距离。Default: 5.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>（本重载为主，string knots/weights 版本见其后）</para>
	///   <para>原生 id 4。实现开头先 <c>Dispose()</c> 再 <c>Load(proc,1)</c>：结果写回本实例，属于"原地生成"，不返回新对象。方向是把 NURBS 曲线（控制多边形 + 节点矢量 + 权因子）离散化成 XLD 点列，与拟合族相反：这里参数是已知的，输出是采样点。</para>
	///   <para><b>约束或前提</b></para>
	///   <para><c>rows</c> 与 <c>cols</c> 长度必须相同（同一控制多边形）；<c>degree</c> 是曲线次数，控制点数少于 <c>degree+1</c> 时曲线定义不完整 [待实测：报错还是自动降阶]。本方法不校验字符串与次数，非法组合以算子异常抛出。</para>
	///   <para><b>参数取向</b></para>
	///   <para><c>knots</c>/<c>weights</c> 一般给 "auto"，由原生层按控制点数生成均匀节点与等权；要给非圆二次/有理曲线（如精确圆）才手填权因子。<c>maxError</c> 是离散点列对原曲线的最大偏差（像素），调小点更密、几何精度更高但点数与后续算子耗时上升；<c>maxDistance</c> 是相邻采样点的最大间距，它决定"稀疏段"是否被强制加密，两者共同控制点数，不要只看 <c>maxError</c>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   using (JlXLDCont nurbs = new JlXLDCont())
	///   {
	///       nurbs.GenContourNurbsXld(new JlTuple(100.0, 120.0, 180.0, 240.0), new JlTuple(50.0, 20.0, 20.0, 60.0),
	///           new JlTuple("auto"), new JlTuple("auto"), 3, new JlTuple(1.0), new JlTuple(5.0));
	///       JlTuple num = nurbs.ContourPointNumXld();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>因为是 <c>Dispose()</c>+<c>Load</c>，调用前本实例的内容一定被丢掉；要保留旧结果必须先 <c>Clone()</c> 或改用别的实例。生成的是开口轮廓，首末点不重复 [待实测：首末点是否重复、以及控制多边形首尾重合时是否闭合]。元组版本可对 <c>knots</c>/<c>weights</c>/<c>maxError</c>/<c>maxDistance</c> 给多值 [待实测：多值与曲线条数的对应规则]。</para>
	/// </remarks>
	public void GenContourNurbsXld(JlTuple rows, JlTuple cols, JlTuple knots, JlTuple weights, int degree, JlTuple maxError, JlTuple maxDistance)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(4);
		JlNativeApi.Store(proc, 0, rows);
		JlNativeApi.Store(proc, 1, cols);
		JlNativeApi.Store(proc, 2, knots);
		JlNativeApi.Store(proc, 3, weights);
		JlNativeApi.StoreI(proc, 4, degree);
		JlNativeApi.Store(proc, 5, maxError);
		JlNativeApi.Store(proc, 6, maxDistance);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rows);
		JlNativeApi.UnpinTuple(cols);
		JlNativeApi.UnpinTuple(knots);
		JlNativeApi.UnpinTuple(weights);
		JlNativeApi.UnpinTuple(maxError);
		JlNativeApi.UnpinTuple(maxDistance);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>把 NURBS 曲线离散成 XLD 点列的标量便捷重载（原生 id 同为 4）：knots/weights 以 StoreS 只各给一个字符串（实际只用于 "auto" 的常规情形）、maxError/maxDistance 为全局标量，同样 Dispose+Load 原地写入本实例。</summary>
	/// <param name="rows">控制多边形的行坐标。</param>
	/// <param name="cols">控制多边形的列坐标。</param>
	/// <param name="knots">节点向量 u@f$u$。Default: "auto"</param>
	/// <param name="weights">权重向量 w@f$w$。Default: "auto"</param>
	/// <param name="degree">NURBS 曲线的次数 p@f$p$。Default: 3</param>
	/// <param name="maxError">NURBS 曲线与其逼近之间的最大距离。Default: 1.0</param>
	/// <param name="maxDistance">相邻两个轮廓点之间的最大距离。Default: 5.0</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>标量便捷重载，原生 id 同为 4，同样是 <c>Dispose()</c>+<c>Load</c> 的原地生成，语义与 <see cref="GenContourNurbsXld(JlTuple, JlTuple, JlTuple, JlTuple, int, JlTuple, JlTuple)"/> 一致。</para>
	///   <para><b>与主重载的实际差异</b></para>
	///   <para><c>knots</c>/<c>weights</c> 以 <c>StoreS</c> 送单个字符串（实际只用于 "auto" 的常规情形），<c>maxError</c>/<c>maxDistance</c> 以 <c>StoreD</c> 送全局标量，因此不能再逐条曲线指定不同节点矢量、权因子或容差；需要非 "auto" 的节点/权向量或每条曲线不同精度时用主重载。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   using (JlXLDCont nurbs = new JlXLDCont())
	///   {
	///       nurbs.GenContourNurbsXld(new JlTuple(100.0, 120.0, 180.0, 240.0), new JlTuple(50.0, 20.0, 20.0, 60.0),
	///           "auto", "auto", 3, 1.0, 5.0);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>本实例原内容被替换，用完记得 <c>Dispose()</c>。</para>
	/// </remarks>
	public void GenContourNurbsXld(JlTuple rows, JlTuple cols, string knots, string weights, int degree, double maxError, double maxDistance)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(4);
		JlNativeApi.Store(proc, 0, rows);
		JlNativeApi.Store(proc, 1, cols);
		JlNativeApi.StoreS(proc, 2, knots);
		JlNativeApi.StoreS(proc, 3, weights);
		JlNativeApi.StoreI(proc, 4, degree);
		JlNativeApi.StoreD(proc, 5, maxError);
		JlNativeApi.StoreD(proc, 6, maxDistance);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rows);
		JlNativeApi.UnpinTuple(cols);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>把两组闭合轮廓各自围成的区域做并集，再输出并集的亚像素边界轮廓（不栅格化）：LoadNew 返回新句柄、两个输入都不被改动，结果通常不止一条轮廓。</summary>
	/// <param name="contours2">包围第二个区域的轮廓。</param>
	/// <returns>包围并集的轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 6：把本实例（索引 1）与 <c>contours2</c>（索引 2）作为两个输入，<c>InitOCT(1)</c>+<c>LoadNew</c> 返回承载并集边界的<b>新</b>句柄，两个输入都不被改动。它先把轮廓围成的区域做并，再输出边界轮廓，故结果通常不止一条轮廓。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>输入必须是闭合轮廓；开口轮廓围成的区域由首末点直连补出还是被忽略 [待实测]。同一数组内多条轮廓如何共同定义区域（嵌套孔洞是否按奇偶规则相减）[待实测]。输入含自交轮廓时结果未定义 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>与 <c>JlRegion</c> 的并集相比，本算子保留亚像素边界（不栅格化），代价是结果被切成多段轮廓、且没有现成的面积可直接读；只要面积/重心/显示时用区域族。想让并集结果重新变成轮廓围成的区域，用返回值的 <c>GenRegionContourXld("filled")</c>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlXLDCont a = new JlXLDCont(new JlTuple(10.0, 10.0, 60.0, 60.0), new JlTuple(10.0, 60.0, 60.0, 10.0));
	///   JlXLDCont b = new JlXLDCont(new JlTuple(40.0, 40.0, 90.0, 90.0), new JlTuple(20.0, 70.0, 70.0, 20.0));
	///   JlXLDCont u = a.Union2ClosedContoursXld(b);
	///   int n = u.CountObj();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回轮廓需 <c>Dispose()</c>；结果轮廓的起点与绕行方向由原生层决定，与两个输入的下标顺序都不对应，因此不能按下标追溯"这条边来自哪个输入" [待实测：输出轮廓的点序方向]。</para>
	/// </remarks>
	public JlXLDCont Union2ClosedContoursXld(JlXLDCont contours2)
	{
		IntPtr proc = JlNativeApi.PreCall(6);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, contours2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours2);
		return obj;
	}

	/// <summary>取两组闭合轮廓所围区域的对称差（只属于一侧的部分），边界由两侧轮廓的交点重新切开；LoadNew 返回新句柄、两个输入不变，结果条数与输入条数无对应关系。</summary>
	/// <param name="contours2">包围第二个区域的轮廓。</param>
	/// <returns>包围对称差的轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 8，返回新句柄（<c>LoadNew</c>），两个输入不变。取"只属于一侧"的部分，边界由两侧轮廓的交点重新切开。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>输入须闭合；两条完全重合或互不相交时的退化结果（空数组、重复边界）[待实测]。运算对两个输入是对称的，交换参数只影响结果轮廓的排列顺序，不影响几何 [待实测：是否严格如此]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只需要"从本轮廓里挖掉另一组"用 <c>DifferenceClosedContoursXld</c>（id 10，顺序敏感）；只需要公共部分用 <c>IntersectionClosedContoursXld</c>（id 12）。对称差在两块几乎重合时会产出两圈极窄的碎片轮廓，后续 <c>SelectContoursXld</c> 按长度筛掉它们否则会把噪声带进拟合。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlXLDCont a = new JlXLDCont(new JlTuple(10.0, 10.0, 60.0, 60.0), new JlTuple(10.0, 60.0, 60.0, 10.0));
	///   JlXLDCont b = new JlXLDCont(new JlTuple(40.0, 40.0, 90.0, 90.0), new JlTuple(20.0, 70.0, 70.0, 20.0));
	///   using (JlXLDCont diff = a.SymmDifferenceClosedContoursXld(b))
	///   {
	///       int pieces = diff.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回对象需 <c>Dispose()</c>；结果条数与输入条数无对应关系。</para>
	/// </remarks>
	public JlXLDCont SymmDifferenceClosedContoursXld(JlXLDCont contours2)
	{
		IntPtr proc = JlNativeApi.PreCall(8);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, contours2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours2);
		return obj;
	}

	/// <summary>从本实例闭合轮廓围成的区域中挖去 sub 围成的区域，结果经 LoadNew 作为差集边界的新句柄返回、两个输入不变；被减数/减数顺序由参数位次固定不可交换，结果含外边界加若干内边界、条数不定。</summary>
	/// <param name="sub">包围从第一个区域中减去的那个区域的轮廓。</param>
	/// <returns>包围差集的轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 10：本实例是被减数（iconic 索引 1），<c>sub</c> 是减数（索引 2），结果经 <c>LoadNew</c> 作为新句柄返回，两个输入不变。参数顺序不可交换。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>两侧都必须是闭合轮廓。当 <c>sub</c> 完全包住本实例围成的区域时，结果为空对象数组还是抛算子异常 [待实测]；调用侧应先准备 <c>CountObj()==0</c> 的分支，别直接对结果取 <c>[1]</c>。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>想"挖洞并保留洞的边界"用本算子；只想知道差集面积，用 <c>GenRegionContourXld("filled")</c> 转区域后做区域差再 <c>AreaCenter</c> 更快。差集与交集、对称差常配套使用：同一对输入三次调用可得完整分解，代价是三倍的轮廓切分与点数。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlXLDCont big = new JlXLDCont(new JlTuple(10.0, 10.0, 90.0, 90.0), new JlTuple(10.0, 90.0, 90.0, 10.0));
	///   JlXLDCont hole = new JlXLDCont(new JlTuple(30.0, 30.0, 60.0, 60.0), new JlTuple(30.0, 60.0, 60.0, 30.0));
	///   JlXLDCont rest = big.DifferenceClosedContoursXld(hole);
	///   if (rest.CountObj() &gt; 0)
	///   {
	///       JlXLDCont outer = rest[1];
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>结果条数不定（外边界加若干内边界），需 <c>Dispose()</c>；被切出来的每条轮廓起点独立，后续 <c>FitLineContourXld</c>/<c>FitCircleContourXld</c> 是按轮廓逐条进行的，别假设一条结果对应输入的一条。</para>
	/// </remarks>
	public JlXLDCont DifferenceClosedContoursXld(JlXLDCont sub)
	{
		IntPtr proc = JlNativeApi.PreCall(10);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, sub);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(sub);
		return obj;
	}

	/// <summary>返回两组闭合轮廓所围区域公共部分的边界轮廓：LoadNew 返回新句柄、运算对两个输入对称且输入不被改动；相交边界在交点处被切碎，结果条数常远多于 2。</summary>
	/// <param name="contours2">包围参与求交的第二个区域的轮廓。</param>
	/// <returns>包围交集的轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 12：本实例与 <c>contours2</c> 各作索引 1、2 输入，<c>LoadNew</c> 返回公共部分边界的新句柄。运算对两个输入对称，但输出排列顺序不保证与任一输入一致 [待实测]。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>输入须闭合。两块区域不相交时是返回空轮廓数组还是抛异常 [待实测]——判"有没有交"更稳妥的写法是先 <c>GenRegionContourXld("filled")</c> 转区域再用 <c>Intersection</c> 配 <c>AreaCenter</c> 看面积。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>与 <c>DistanceCc</c>/<c>DistanceCcMin</c> 的区别是本算子回答"重叠成什么形状"，后者回答"离得多近"；只想判断相交与否且轮廓密集时，距离阈值法比再做一次布尔便宜。相交边界会被切碎，条数常远多于 2。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlXLDCont a = new JlXLDCont(new JlTuple(10.0, 10.0, 60.0, 60.0), new JlTuple(10.0, 60.0, 60.0, 10.0));
	///   JlXLDCont b = new JlXLDCont(new JlTuple(40.0, 40.0, 90.0, 90.0), new JlTuple(20.0, 70.0, 70.0, 20.0));
	///   using (JlXLDCont inter = a.IntersectionClosedContoursXld(b))
	///   {
	///       int pieces = inter.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值需 <c>Dispose()</c>；结果边界由交点截断，交点附近的重合/共线边界处理 [待实测]。</para>
	/// </remarks>
	public JlXLDCont IntersectionClosedContoursXld(JlXLDCont contours2)
	{
		IntPtr proc = JlNativeApi.PreCall(12);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, contours2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours2);
		return obj;
	}

	/// <summary>合并属于同一圆的弧段轮廓：对每段弧各自拟合圆，弧角跨度差（弧度）、间隙与半径差/圆心差（像素）等六项阈值走元组可逐弧设定，拟出的圆足够一致才连成一条；LoadNew 返回合并后的新句柄，输入不变、条数变少且不保留输入下标。</summary>
	/// <param name="maxArcAngleDiff">两条圆弧间的最大角度距离。Default: 0.5</param>
	/// <param name="maxArcOverlap">两条圆弧的最大重叠。Default: 0.1</param>
	/// <param name="maxTangentAngle">连接线与圆弧切线之间的最大夹角。Default: 0.2</param>
	/// <param name="maxDist">两条圆弧间隙的最大长度（像素）。Default: 30</param>
	/// <param name="maxRadiusDiff">两条弧各自拟合圆的最大半径差。Default: 10</param>
	/// <param name="maxCenterDist">两条弧各自拟合圆的最大圆心距。Default: 10</param>
	/// <param name="mergeSmallContours">是否也合并未拟合出圆的小轮廓。Default: "true"</param>
	/// <param name="iterations">迭代次数。Default: 1</param>
	/// <returns>合并后的轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>（本重载为主，<c>double</c> 标量版本见其后）</para>
	///   <para>原生 id 13：<c>Store(proc,1)</c> 送本实例轮廓，六个阈值走 <c>Store</c>（元组），<c>mergeSmallContours</c> 走 <c>StoreS</c>，<c>iterations</c> 走 <c>StoreI</c>；结果 <c>LoadNew</c> 成新句柄返回，输入不变。合并判据建立在"对每段弧各自拟合圆"之上，只有拟出的圆足够一致才连成一条。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>接近直线的轮廓拟不出稳定半径，这类"拟合不出圆"的短轮廓只由 <c>mergeSmallContours</c> 决定是否参与合并；因此同一次调用里，长弧与短碎弧的合并结果对这一个开关都很敏感。整圆被切成两段首尾相接的弧时，靠 <c>maxDist</c> 与 <c>maxTangentAngle</c> 连接，而不是靠圆心距。</para>
	///   <para><b>参数取向</b></para>
	///   <para><c>maxArcAngleDiff</c> 管两段弧角跨度的差异（弧度），<c>maxArcOverlap</c> 管允许重叠的多少 [待实测：单位是像素弧长还是弧度]，<c>maxDist</c> 是间隙长度上限（像素），<c>maxRadiusDiff</c>/<c>maxCenterDist</c> 是两次拟合圆的半径差与圆心差（像素）。想让"同一圆但缺口很大"的弧连上，只放大 <c>maxDist</c>，同时把 <c>maxRadiusDiff</c> 收紧，否则会把同心度差的邻弧错并；反之若零件半径公差大，放宽 <c>maxRadiusDiff</c> 但收紧 <c>maxCenterDist</c>。<c>iterations</c> 增大到 2 以上可让链式合并继续传递（A-B 先并、再与 C 并），代价是耗时随轮次上升 [待实测：多轮耗时与收敛判据]。</para>
	///   <para>元组重载可对六个阈值各给多值 [待实测：多值与轮廓条数的对应/广播规则]。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\gear.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlXLDCont arcs = edges.UnionCocircularContoursXld(new JlTuple(0.5), new JlTuple(0.1), new JlTuple(0.2),
	///       new JlTuple(30.0), new JlTuple(10.0), new JlTuple(10.0), "true", 1);
	///   int n = arcs.CountObj();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>合并后子轮廓条数变少且不保留输入下标（与 <c>JlRegion</c> 的 <c>ExpandRegion</c> 族同类坑），任何"按输入第 i 条缓存结果"的写法在这里都会错位；本算子没有属性保留开关，输入轮廓上携带的属性在合并结果上如何处理 [待实测]。返回值需 <c>Dispose()</c>。</para>
	/// </remarks>
	public JlXLDCont UnionCocircularContoursXld(JlTuple maxArcAngleDiff, JlTuple maxArcOverlap, JlTuple maxTangentAngle, JlTuple maxDist, JlTuple maxRadiusDiff, JlTuple maxCenterDist, string mergeSmallContours, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(13);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, maxArcAngleDiff);
		JlNativeApi.Store(proc, 1, maxArcOverlap);
		JlNativeApi.Store(proc, 2, maxTangentAngle);
		JlNativeApi.Store(proc, 3, maxDist);
		JlNativeApi.Store(proc, 4, maxRadiusDiff);
		JlNativeApi.Store(proc, 5, maxCenterDist);
		JlNativeApi.StoreS(proc, 6, mergeSmallContours);
		JlNativeApi.StoreI(proc, 7, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maxArcAngleDiff);
		JlNativeApi.UnpinTuple(maxArcOverlap);
		JlNativeApi.UnpinTuple(maxTangentAngle);
		JlNativeApi.UnpinTuple(maxDist);
		JlNativeApi.UnpinTuple(maxRadiusDiff);
		JlNativeApi.UnpinTuple(maxCenterDist);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>同圆弧段轮廓合并的标量便捷重载（原生 id 同为 13）：六个阈值走 StoreD 全局单值、所有弧共用一套公差；判据与"条数变少、不能按输入下标配对"的坑见主重载，LoadNew 返回新句柄、输入不变。</summary>
	/// <param name="maxArcAngleDiff">两条圆弧间的最大角度距离。Default: 0.5</param>
	/// <param name="maxArcOverlap">两条圆弧的最大重叠。Default: 0.1</param>
	/// <param name="maxTangentAngle">连接线与圆弧切线之间的最大夹角。Default: 0.2</param>
	/// <param name="maxDist">两条圆弧间隙的最大长度（像素）。Default: 30</param>
	/// <param name="maxRadiusDiff">两条弧各自拟合圆的最大半径差。Default: 10</param>
	/// <param name="maxCenterDist">两条弧各自拟合圆的最大圆心距。Default: 10</param>
	/// <param name="mergeSmallContours">是否也合并未拟合出圆的小轮廓。Default: "true"</param>
	/// <param name="iterations">迭代次数。Default: 1</param>
	/// <returns>合并后的轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>标量便捷重载，原生 id 同为 13，判据、"条数变少、不能按输入下标配对"的坑均见 <see cref="UnionCocircularContoursXld(JlTuple, JlTuple, JlTuple, JlTuple, JlTuple, JlTuple, string, int)"/>。</para>
	///   <para><b>与主重载的实际差异</b></para>
	///   <para>六个阈值用 <c>StoreD</c> 送全局标量，不产生 <c>UnpinTuple</c> 调用，因而无法对不同半径档的弧给不同公差；同一次调用里所有弧共用一套阈值。需要按轮廓分别给值时用主重载并显式写 <c>new JlTuple(...)</c>（直接写 <c>0.5</c> 会绑定到本重载）。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\gear.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont arcs = edges.UnionCocircularContoursXld(0.5, 0.1, 0.2, 30.0, 10.0, 10.0, "true", 1))
	///   {
	///       JlTuple num = arcs.ContourPointNumXld();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值需 <c>Dispose()</c>；<c>iterations</c> 大于 1 时耗时上升。</para>
	/// </remarks>
	public JlXLDCont UnionCocircularContoursXld(double maxArcAngleDiff, double maxArcOverlap, double maxTangentAngle, double maxDist, double maxRadiusDiff, double maxCenterDist, string mergeSmallContours, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(13);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maxArcAngleDiff);
		JlNativeApi.StoreD(proc, 1, maxArcOverlap);
		JlNativeApi.StoreD(proc, 2, maxTangentAngle);
		JlNativeApi.StoreD(proc, 3, maxDist);
		JlNativeApi.StoreD(proc, 4, maxRadiusDiff);
		JlNativeApi.StoreD(proc, 5, maxCenterDist);
		JlNativeApi.StoreS(proc, 6, mergeSmallContours);
		JlNativeApi.StoreI(proc, 7, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用浮点坐标的矩形框只保留框内的轮廓部分：被框边穿过的轮廓在边界处断开、一条可切成多条，closeContours 决定闭合轮廓裁后是否仍闭合，四边走元组可逐轮廓给框；LoadNew 返回新句柄，输入不变、输出条数与点数都可能变。</summary>
	/// <param name="row1">裁剪矩形的上边界。Default: 0</param>
	/// <param name="col1">裁剪矩形的左边界。Default: 0</param>
	/// <param name="row2">裁剪矩形的下边界。Default: 512</param>
	/// <param name="col2">裁剪矩形的右边界。Default: 512</param>
	/// <param name="closeContours">闭合轮廓是否生成闭合的输出轮廓？Default: "true"</param>
	/// <returns>输出轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>（本重载为主，<c>double</c> 标量版本见其后）</para>
	///   <para>原生 id 14：本实例作索引 1 输入，四边以 <c>Store</c>（元组）送入、<c>closeContours</c> 以 <c>StoreS</c> 送入，结果 <c>LoadNew</c> 返回新句柄。语义是"只保留矩形内的部分"，被矩形边穿过的轮廓会在边界处断开。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>需满足 <c>row1 &lt; row2</c> 且 <c>col1 &lt; col2</c>（图像坐标 row 向下），写反时是返回空还是报参数错 [待实测]。轮廓点坐标是亚像素，裁剪框因此用元组/浮点给值才有意义。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>与 <c>ClipContoursXld</c>（id 52）的区别：本算子参数是浮点且能用 <c>closeContours</c> 控制闭合性，<c>ClipContoursXld</c> 只收整数框且无该开关；与 <c>SelectContoursXld</c> 的区别是后者按整条轮廓取舍、不会把一条轮廓切断。要"只看视野内的边"用本算子，要"剔除视野外的轮廓"用选择算子（更快且不改变点数）。</para>
	///   <para><b>参数取向</b></para>
	///   <para><c>closeContours="true"</c> 让原本闭合的轮廓在裁切后仍是闭合轮廓 [待实测：是沿裁剪框边界补回，还是把断开处直接首尾相连]。给 "false" 得到开口轮廓，随后若喂给 <c>GenRegionContourXld("filled")</c> 或闭合轮廓布尔族，结果与预期不符且不一定报错。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\roi.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlXLDCont cropped = edges.CropContoursXld(new JlTuple(64.0), new JlTuple(64.0), new JlTuple(448.0), new JlTuple(448.0), "true");
	///   int n = cropped.CountObj();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>一条轮廓可能被切成多条，输出条数与点数都不等于输入，携带的属性段也随之重排 [待实测]；返回值需 <c>Dispose()</c>。</para>
	/// </remarks>
	public JlXLDCont CropContoursXld(JlTuple row1, JlTuple col1, JlTuple row2, JlTuple col2, string closeContours)
	{
		IntPtr proc = JlNativeApi.PreCall(14);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row1);
		JlNativeApi.Store(proc, 1, col1);
		JlNativeApi.Store(proc, 2, row2);
		JlNativeApi.Store(proc, 3, col2);
		JlNativeApi.StoreS(proc, 4, closeContours);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(col1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(col2);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形框裁剪轮廓的标量便捷重载（原生 id 同为 14）：四边以 StoreD 送单一矩形、本实例所有轮廓共用同一个裁剪框，断开与闭合语义同主重载；LoadNew 返回新句柄、输入不变。</summary>
	/// <param name="row1">裁剪矩形的上边界。Default: 0</param>
	/// <param name="col1">裁剪矩形的左边界。Default: 0</param>
	/// <param name="row2">裁剪矩形的下边界。Default: 512</param>
	/// <param name="col2">裁剪矩形的右边界。Default: 512</param>
	/// <param name="closeContours">闭合轮廓是否生成闭合的输出轮廓？Default: "true"</param>
	/// <returns>输出轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>标量便捷重载，原生 id 同为 14，裁切语义、<c>closeContours</c> 的影响与"切完条数变化"的坑见 <see cref="CropContoursXld(JlTuple, JlTuple, JlTuple, JlTuple, string)"/>。</para>
	///   <para><b>与主重载的实际差异</b></para>
	///   <para>四边用 <c>StoreD</c> 送单一矩形，本实例中的所有轮廓共用同一个裁剪框；主重载可给多条值以逐轮廓设定不同框 [待实测：多值与轮廓条数的对应规则]。写 <c>64</c> 这样的整数字面量即可命中本重载（隐式转 double）。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\roi.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont cropped = edges.CropContoursXld(64.0, 64.0, 448.0, 448.0, "true"))
	///   {
	///       int n = cropped.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值需 <c>Dispose()</c>。</para>
	/// </remarks>
	public JlXLDCont CropContoursXld(double row1, double col1, double row2, double col2, string closeContours)
	{
		IntPtr proc = JlNativeApi.PreCall(14);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row1);
		JlNativeApi.StoreD(proc, 1, col1);
		JlNativeApi.StoreD(proc, 2, row2);
		JlNativeApi.StoreD(proc, 3, col2);
		JlNativeApi.StoreS(proc, 4, closeContours);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>为元组给定的每组 (row,col) 点生成十字标记轮廓：臂长由 size 给、整体绕中心转 angle（弧度，默认 0.785398 约 45° 即 X 形，要 + 形须显式给 0）；先 Dispose 后 Load 原地写入本实例、不返回新对象，调用前本实例内容全部丢失。</summary>
	/// <param name="row">输入点的行坐标。</param>
	/// <param name="col">输入点的列坐标。</param>
	/// <param name="size">十字横杠的长度。Default: 6.0</param>
	/// <param name="angle">十字的朝向。Default: 0.785398</param>
	/// <remarks>
	///   <para><b>功能说明</b>（本重载为主，<c>double</c> 标量版本见其后）</para>
	///   <para>原生 id 15，开头 <c>Dispose()</c>、结尾 <c>Load(proc,1)</c>：结果原地写入本实例，不返回新对象。对每一组 (row,col) 生成一个十字标记，臂长按 <c>size</c> 给、整体绕中心转 <c>angle</c>。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>本实例调用前的内容全部丢失，不能当"往数组里追加"的手段用（要保留先 <c>Clone()</c>）。它不需要图像，是纯几何生成，因此也常被用来在结果图上画定位点。</para>
	///   <para><b>参数取向</b></para>
	///   <para><c>angle</c> 单位弧度且文档默认值是 0.785398（约 45°），即默认画出的是 X 形而不是 + 形；要水平/垂直的十字需显式给 0。用不同 <c>angle</c> 可在同一批点上做方向编码 [待实测：<c>size</c> 是整条横杠长度还是半臂长；每个点是合成一条轮廓还是拆成多条]。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   using (JlXLDCont marks = new JlXLDCont())
	///   {
	///       marks.GenCrossContourXld(new JlTuple(120.0, 340.0), new JlTuple(200.0, 480.0), new JlTuple(12.0), 0.0);
	///       JlTuple num = marks.ContourPointNumXld();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>元组重载允许 <c>row</c>/<c>col</c>/<c>size</c> 给多值 [待实测：长度不匹配（如 size 给单值而 row 给多值）时是否广播]；<c>angle</c> 只有 <c>StoreD</c> 一个标量通道，无法逐点设定方向。</para>
	/// </remarks>
	public void GenCrossContourXld(JlTuple row, JlTuple col, JlTuple size, double angle)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(15);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, col);
		JlNativeApi.Store(proc, 2, size);
		JlNativeApi.StoreD(proc, 3, angle);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(col);
		JlNativeApi.UnpinTuple(size);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>生成十字标记轮廓的标量便捷重载（原生 id 同为 15）：row/col/size 走 StoreD 单值、一次只能画一个十字，同样 Dispose+Load 原地写入本实例；angle 单位弧度，默认约 45° 即 X 形。</summary>
	/// <param name="row">输入点的行坐标。</param>
	/// <param name="col">输入点的列坐标。</param>
	/// <param name="size">十字横杠的长度。Default: 6.0</param>
	/// <param name="angle">十字的朝向。Default: 0.785398</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>标量便捷重载，原生 id 同为 15，同样是 <c>Dispose()</c>+<c>Load</c> 的原地生成；语义与默认角度见 <see cref="GenCrossContourXld(JlTuple, JlTuple, JlTuple, double)"/>。</para>
	///   <para><b>与主重载的实际差异</b></para>
	///   <para><c>row</c>/<c>col</c>/<c>size</c> 走 <c>StoreD</c>，一次只能画一个十字（中心与臂长都是单值）；要一批点画一排标记必须用元组重载。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   using (JlXLDCont mark = new JlXLDCont())
	///   {
	///       mark.GenCrossContourXld(240.0, 320.0, 12.0, 0.0);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>本实例原内容被替换，用完 <c>Dispose()</c>。</para>
	/// </remarks>
	public void GenCrossContourXld(double row, double col, double size, double angle)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(15);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, col);
		JlNativeApi.StoreD(proc, 2, size);
		JlNativeApi.StoreD(proc, 3, angle);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>按轮廓的相对位置（sortMode 指定左上角、首点等基准）重排数组内轮廓的先后，rowOrCol 决定行列谁优先；只改顺序不增删（CountObj 前后一致），LoadNew 返回新句柄、输入数组不被改动。</summary>
	/// <param name="sortMode">排序方式。Default: "upper_left"</param>
	/// <param name="order">升序或降序排序。Default: "true"</param>
	/// <param name="rowOrCol">先按行、再按列排序。Default: "row"</param>
	/// <returns>排序后的轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 16，结果 <c>LoadNew</c> 成新句柄返回，输入数组不被改动。按轮廓的相对位置（左上角、首点等，由 <c>sortMode</c> 定）重排数组内轮廓的先后。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>只改变顺序，不增删轮廓，因此 <c>CountObj()</c> 前后一致；但 <c>sortMode</c>/<c>rowOrCol</c> 的取值组合是否全部受支持托管侧不校验 [待实测：可用取值集合，以及 <c>rowOrCol</c> 在哪几种 sortMode 下才被使用]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要"编号输出（第 1 个孔、第 2 个孔）"必须排序后再逐条 <c>[i]</c>；要"只要符合某条件的"用 <c>SelectContoursXld</c>（会删条目）或 <c>SelectShapeXld</c>（按几何形状特征）。先筛后排通常更快，因为参与排序的轮廓更少。</para>
	///   <para><b>参数取向</b></para>
	///   <para><c>order</c> 取 <c>"true"</c> 为递增（左上先、行号小者先），<c>"false"</c> 反向；<c>rowOrCol</c> 决定行列谁优先，读表格状排列的字符/焊点时应与阅读方向一致，否则视觉正确但下标全乱。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\bga.pgm");
	///   JlRegion reg = img.Threshold(0.0, 100.0);
	///   JlXLDCont conts = new JlXLDCont(reg, "border");
	///   JlXLDCont sorted = conts.SortContoursXld("upper_left", "true", "row");
	///   JlXLDCont first = sorted[1];
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>排序后下标全部变化：此前用 <c>ContourPointNumXld()</c>/<c>GetRegressParamsXld(...)</c> 取到的 per-contour 元组不再与 <c>sorted[i]</c> 对应，需要在排序结果上重新取；输入与输出都要 <c>Dispose()</c>。</para>
	/// </remarks>
	public JlXLDCont SortContoursXld(string sortMode, string order, string rowOrCol)
	{
		IntPtr proc = JlNativeApi.PreCall(16);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, sortMode);
		JlNativeApi.StoreS(proc, 1, order);
		JlNativeApi.StoreS(proc, 2, rowOrCol);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>线扫逐帧滚动合并：把本实例的当前帧轮廓与 prevConts（上一轮累计结果）跨帧拼接，返回值经 LoadNew(proc,1) 给"与旧轮廓合并后的当前结果"，out prevMergedConts 经 LoadNew(proc,2) 给"本轮没能并上的上一帧轮廓"，两个输入都不被改写；margin 走元组可多值。</summary>
	/// <param name="prevConts">上一次迭代合并出的轮廓。</param>
	/// <param name="prevMergedConts">上一次迭代中未能与当前轮廓合并的轮廓。</param>
	/// <param name="imageHeight">行扫描图像的高度。Default: 512</param>
	/// <param name="margin">轮廓到图像边界的最大距离。Default: 0.0</param>
	/// <param name="mergeBorder">当前图像中与前一幅图像相接的图像行。Default: "top"</param>
	/// <param name="maxImagesCont">一条轮廓最多覆盖的图像数。Default: 3</param>
	/// <returns>当前轮廓，并在适用处与旧轮廓合并。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>（本重载为主，<c>double margin</c> 版本见其后）</para>
	///   <para>原生 id 17，两个输出：本方法返回值经 <c>LoadNew(proc,1)</c> 得到"当前帧轮廓与上一帧合并后的结果"，<c>out prevMergedConts</c> 经 <c>LoadNew(proc,2)</c> 得到"上一帧里本轮没能与当前帧合并上的轮廓"。本实例充当当前帧的轮廓输入，<c>prevConts</c> 是上一轮累计的合并结果，二者都不被改写。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>这是有状态的滚动调用：每帧必须把上一帧的返回值再传成 <c>prevConts</c>，并把 <c>prevMergedConts</c> 一并保留到下一轮，否则跨帧轮廓会在帧界处被截断或重复计数 [待实测：<c>prevMergedConts</c> 是应并入下一帧输入还是可丢弃]。合并后轮廓的 row 坐标是否被换算到统一的连续坐标 [待实测]。</para>
	///   <para><b>参数取向</b></para>
	///   <para><c>imageHeight</c> 必须是线扫图像的行数（不是宽），它决定帧间位移量；<c>margin</c> 只对距图像边界该范围内的轮廓做合并，给 0 表示只在正好相接的边界行上判断，漏配时优先加大 <c>margin</c> 而不是加大 <c>maxImagesCont</c>。<c>mergeBorder</c> 指明当前帧与前帧相接的边，装反方向会导致轮廓首尾对不上。<c>maxImagesCont</c> 限制一条轮廓最多跨几帧，产线速度高、单帧视野短时才需要调大。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlXLDCont acc = new JlXLDCont();
	///   JlXLDCont accLeft = new JlXLDCont();
	///   foreach (string f in new string[] { @"C:\scan\l0.pgm", @"C:\scan\l1.pgm" })
	///   {
	///       JlImage line = new JlImage(f);
	///       JlXLDCont cur = line.EdgesSubPix("canny", 0.4, 20, 40);
	///       JlXLDCont left;
	///       JlXLDCont merged = cur.MergeContLineScanXld(acc, out left, 128, new JlTuple(8.0), "top", 3);
	///       acc.Dispose();
	///       accLeft.Dispose();
	///       acc = merged;
	///       accLeft = left;
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值与 <c>out</c> 值都是新句柄，两条都要 <c>Dispose()</c>；示例中的做法是先用新值覆盖累计变量、再释放被替换下来的旧句柄（顺序反过来会把仍在用的对象释放掉）。<c>margin</c> 的元组重载允许多值 [待实测：多值含义]。</para>
	/// </remarks>
	public JlXLDCont MergeContLineScanXld(JlXLDCont prevConts, out JlXLDCont prevMergedConts, int imageHeight, JlTuple margin, string mergeBorder, int maxImagesCont)
	{
		IntPtr proc = JlNativeApi.PreCall(17);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, prevConts);
		JlNativeApi.StoreI(proc, 0, imageHeight);
		JlNativeApi.Store(proc, 1, margin);
		JlNativeApi.StoreS(proc, 2, mergeBorder);
		JlNativeApi.StoreI(proc, 3, maxImagesCont);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out prevMergedConts);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(prevConts);
		return obj;
	}

	/// <summary>线扫跨帧合并轮廓的标量便捷重载（原生 id 同为 17）：margin 走 StoreD 单一容差、上下边界不能用不同值，双输出（返回值=合并后的当前结果、out prevMergedConts=没并上的旧轮廓）且都是新句柄，滚动回传用法同主重载。</summary>
	/// <param name="prevConts">上一次迭代合并出的轮廓。</param>
	/// <param name="prevMergedConts">上一次迭代中未能与当前轮廓合并的轮廓。</param>
	/// <param name="imageHeight">行扫描图像的高度。Default: 512</param>
	/// <param name="margin">轮廓到图像边界的最大距离。Default: 0.0</param>
	/// <param name="mergeBorder">当前图像中与前一幅图像相接的图像行。Default: "top"</param>
	/// <param name="maxImagesCont">一条轮廓最多覆盖的图像数。Default: 3</param>
	/// <returns>当前轮廓，并在适用处与旧轮廓合并。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>标量便捷重载，原生 id 同为 17，双输出与"每帧必须回传 <c>prevConts</c>"的滚动用法见 <see cref="MergeContLineScanXld(JlXLDCont, out JlXLDCont, int, JlTuple, string, int)"/>。</para>
	///   <para><b>与主重载的实际差异</b></para>
	///   <para><c>margin</c> 用 <c>StoreD</c> 送单一标量，因而没有 <c>UnpinTuple</c>；上下边界只能用同一个容差，主重载可给多值分别设定 [待实测：主重载多值的具体分配方式]。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage line = new JlImage(@"C:\scan\l1.pgm");
	///   JlXLDCont cur = line.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlXLDCont prev = new JlXLDCont();
	///   JlXLDCont left;
	///   JlXLDCont merged = cur.MergeContLineScanXld(prev, out left, 128, 8.0, "top", 3);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值与 <c>out</c> 值都要 <c>Dispose()</c>；<c>prev</c> 为空对象数组（<c>new JlXLDCont()</c>）时首帧是否被原生层接受 [待实测]。</para>
	/// </remarks>
	public JlXLDCont MergeContLineScanXld(JlXLDCont prevConts, out JlXLDCont prevMergedConts, int imageHeight, double margin, string mergeBorder, int maxImagesCont)
	{
		IntPtr proc = JlNativeApi.PreCall(17);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, prevConts);
		JlNativeApi.StoreI(proc, 0, imageHeight);
		JlNativeApi.StoreD(proc, 1, margin);
		JlNativeApi.StoreS(proc, 2, mergeBorder);
		JlNativeApi.StoreI(proc, 3, maxImagesCont);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out prevMergedConts);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(prevConts);
		return obj;
	}

	/// <summary>读 ARC/INFO generate 文本（GIS 弧段坐标列表）：先 Dispose 再 Load，文件内容原地灌入本实例、原有轮廓全部被替换，坐标数值直接当像素坐标用；不返回新句柄。</summary>
	/// <param name="fileName">ARC/INFO 文件名。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 20。方法开头先 <c>Dispose()</c>，随后 <c>StoreS(fileName)</c> + <c>InitOCT(1)</c> + <c>Load(proc,1)</c>：文件内容原地写入本实例，本实例原有轮廓全部被替换。读的是 ARC/INFO generate 文本格式（GIS 里的弧段坐标列表）。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>文件必须存在且格式符合 generate 约定，托管侧不做存在性检查，路径错误以算子异常抛出 [待实测：异常类别与编码/换行要求]。文件里的坐标数值直接当像素坐标用，若源数据带地理配准比例，需要先自行换算 [待实测：是否读入 world 文件]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要与 CAD/第三方几何交换用 <c>ReadContourXldDxf</c>（还能按图层筛选并返回图元名）；只在库内搬运用 <c>Serialize</c>/<c>Deserialize</c> 二进制更快。本算子适合接 GIS/测绘导出的弧段。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   using (JlXLDCont loaded = new JlXLDCont())
	///   {
	///       loaded.ReadContourXldArcInfo(@"C:\gis\contours.gen");
	///       int n = loaded.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>不要复用已有内容的实例来读文件（内容会没）；文件里每条弧对应几条轮廓、轮廓是否闭合、点序方向 [待实测]。</para>
	/// </remarks>
	public void ReadContourXldArcInfo(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(20);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>把本实例轮廓写成 ARC/INFO generate 文本坐标序列：纯输出调用（只有 Store+StoreS、无 InitOCT/Load），本实例只作输入、内容不被改写。</summary>
	/// <param name="fileName">ARC/INFO 文件名。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 21。实现里只有 <c>Store(proc,1)</c> + <c>StoreS(fileName)</c>，没有 <c>InitOCT</c> 也没有 <c>Load</c>：这是纯输出调用，本实例只作输入，内容不会被改写。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>目标文件由原生层创建，同名文件如何处理（覆盖或追加）[待实测]；目录不存在时以算子异常抛出。写出的是文本坐标序列，精度受格式化位数限制 [待实测：小数位数]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>成对的读入口是 <c>ReadContourXldArcInfo</c>；要给 CAD/第三方用 <c>WriteContourXldDxf</c>。本算子适合把检测出的边界交给 GIS/测绘侧继续处理。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\part.pgm");
	///   JlRegion reg = img.Threshold(60.0, 255.0);
	///   using (JlXLDCont border = new JlXLDCont(reg, "border"))
	///   {
	///       border.WriteContourXldArcInfo(@"C:\out\border.gen");
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>多条轮廓会写进同一个文件并以前后顺序排列，读回时的条数与顺序是否与写出一致 [待实测]。</para>
	/// </remarks>
	public void WriteContourXldArcInfo(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(21);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>把每条轮廓沿法向平移 distance（可为负、元组可逐轮廓给不同距离）得到平行轮廓；mode 决定逐点法向取贴合曲线的 contour_normal 还是整条回归线的 regression_normal；LoadNew 返回新句柄、输入不变。</summary>
	/// <param name="mode">计算方向信息所用的模式。Default: "regression_normal"</param>
	/// <param name="distance">平行轮廓的距离。Default: 1</param>
	/// <returns>平行轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>（本重载为主，<c>double distance</c> 版本见其后）</para>
	///   <para>原生 id 23：<c>StoreS(mode)</c> + <c>Store(distance)</c>，本实例作索引 1 输入，结果 <c>LoadNew</c> 返回新句柄，输入不变。输出是把每条轮廓沿法向平移 <c>distance</c> 后的平行轮廓。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>逐点法向来自局部拟合，故 <c>distance</c> 的绝对值不应小于轮廓自身的最小曲率半径，否则凹侧会翻转自交 [待实测：自交时原生层是否做消解]。开口轮廓两端如何收尾 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para><c>mode</c> 的两种取法决定了用途：<c>contour_normal</c> 让平行线贴合原曲线的弯曲（做等距公差带用这个），<c>regression_normal</c> 按整条轮廓的回归线法向平移（做"把线整体挪开一点"用这个，弯线上不会自交但也不再等距）。要一次生成多条平行线/中心线两侧对称线用 <c>JlXLDPoly.GenParallelsXld</c>。</para>
	///   <para><b>参数取向</b></para>
	///   <para><c>distance</c> 可为负，负值表示往法向反的一侧偏（同一条轮廓给 ±d 可得到对称的两侧边界）[待实测：正负与左右侧的对应关系]。元组重载可对不同轮廓给不同距离 [待实测：多值与轮廓条数的对应规则]。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\seam.pgm");
	///   JlXLDCont edge = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlXLDCont offset1 = edge.GenParallelContourXld("contour_normal", new JlTuple(3.0));
	///   JlXLDCont offset2 = edge.GenParallelContourXld("contour_normal", new JlTuple(-3.0));
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>两次调用产生两个新对象，都要 <c>Dispose()</c>；平行轮廓的点数与原轮廓是否一一对应（便于按点比较）[待实测]。</para>
	/// </remarks>
	public JlXLDCont GenParallelContourXld(string mode, JlTuple distance)
	{
		IntPtr proc = JlNativeApi.PreCall(23);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.Store(proc, 1, distance);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(distance);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>法向平移生成平行轮廓的标量便捷重载（原生 id 同为 23）：distance 走 StoreD、本实例所有轮廓共用同一偏移量且不做 UnpinTuple；法向取法与自交、正负侧的说明同主重载，LoadNew 返回新句柄、输入不变。</summary>
	/// <param name="mode">计算方向信息所用的模式。Default: "regression_normal"</param>
	/// <param name="distance">平行轮廓的距离。Default: 1</param>
	/// <returns>平行轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>标量便捷重载，原生 id 同为 23；法向取法、自交与正负侧的说明见 <see cref="GenParallelContourXld(string, JlTuple)"/>。</para>
	///   <para><b>与主重载的实际差异</b></para>
	///   <para><c>distance</c> 走 <c>StoreD</c>，本实例所有轮廓共用同一偏移量，且不做 <c>UnpinTuple</c>；每条轮廓不同偏移时用主重载并写 <c>new JlTuple(...)</c>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\seam.pgm");
	///   JlXLDCont edge = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont band = edge.GenParallelContourXld("regression_normal", 1.0))
	///   {
	///       int n = band.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值需 <c>Dispose()</c>。</para>
	/// </remarks>
	public JlXLDCont GenParallelContourXld(string mode, double distance)
	{
		IntPtr proc = JlNativeApi.PreCall(23);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreD(proc, 1, distance);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按中心 (row,column)、主轴角 phi（弧度）与两个半边长（length 是半边长、非全长）构造旋转矩形轮廓，五个元组参数须一一对应；先 Dispose 后 Load 原地写入本实例、不返回新对象，角点落在真实几何位置。</summary>
	/// <param name="row">矩形中心的行坐标。Default: 300.0</param>
	/// <param name="column">矩形中心的列坐标。Default: 200.0</param>
	/// <param name="phi">矩形主轴的朝向 [rad]。Default: 0.0</param>
	/// <param name="length1">矩形的第一半径（半长）。Default: 100.5</param>
	/// <param name="length2">矩形的第二半径（半宽）。Default: 20.5</param>
	/// <remarks>
	///   <para><b>功能说明</b>（本重载为主，<c>double</c> 标量版本见其后）</para>
	///   <para>原生 id 24，开头 <c>Dispose()</c>、结尾 <c>Load(proc,1)</c>：结果原地写入本实例。按中心、<c>phi</c>、两个半边长构造旋转矩形轮廓，几何定义与 <c>JlRegion.GenRectangle1</c> 的旋转版一致（长度是半边长，不是全长）。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>五个参数须一一对应（同一元组长度或可广播）[待实测：长度不匹配时的行为]。<c>length1</c>/<c>length2</c> 给 0 或负值时是否退化为线段/报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>想要像素对齐的矩形区域，直接用 <c>JlRegion.GenRectangle1</c>；想要"轮廓点数固定、角点落在真实几何位置"的模板或标定标记，用本算子（从区域转来的边界轮廓会沿像素中心走、角点偏半像素且点数随尺寸变化）。拟合得到的矩形要还原成轮廓也用本算子，参数可原样传回。</para>
	///   <para><b>参数取向</b></para>
	///   <para><c>phi</c> 是主轴（<c>length1</c> 方向）相对列轴的角度 [待实测：角度正方向与主轴约定]，弧度制；<c>length1 &lt; length2</c> 时矩形只是换了一个朝向，但后续按 <c>phi</c> 判断长短轴的比较会反。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   using (JlXLDCont rect = new JlXLDCont())
	///   {
	///       rect.GenRectangle2ContourXld(new JlTuple(300.0), new JlTuple(200.0), new JlTuple(0.0), new JlTuple(100.5), new JlTuple(20.5));
	///       JlTuple num = rect.ContourPointNumXld();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>本实例旧内容被替换；生成轮廓是否首尾点重复以闭合 [待实测]，若下游要闭合轮廓（<c>GenRegionContourXld("filled")</c>、闭合轮廓布尔族）不确定时先套一层 <c>CloseContoursXld()</c> 更稳。</para>
	/// </remarks>
	public void GenRectangle2ContourXld(JlTuple row, JlTuple column, JlTuple phi, JlTuple length1, JlTuple length2)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(24);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, phi);
		JlNativeApi.Store(proc, 3, length1);
		JlNativeApi.Store(proc, 4, length2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(length1);
		JlNativeApi.UnpinTuple(length2);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>生成旋转矩形轮廓的标量便捷重载（原生 id 同为 24）：五个参数全走 StoreD、一次只能生成一个矩形，半边长含义与 phi 弧度约定同主重载，同样 Dispose+Load 原地写入本实例、不返回新对象。</summary>
	/// <param name="row">矩形中心的行坐标。Default: 300.0</param>
	/// <param name="column">矩形中心的列坐标。Default: 200.0</param>
	/// <param name="phi">矩形主轴的朝向 [rad]。Default: 0.0</param>
	/// <param name="length1">矩形的第一半径（半长）。Default: 100.5</param>
	/// <param name="length2">矩形的第二半径（半宽）。Default: 20.5</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>标量便捷重载，原生 id 同为 24，同样是 <c>Dispose()</c>+<c>Load</c> 的原地生成；半边长含义、<c>phi</c> 约定与闭合性见 <see cref="GenRectangle2ContourXld(JlTuple, JlTuple, JlTuple, JlTuple, JlTuple)"/>。</para>
	///   <para><b>与主重载的实际差异</b></para>
	///   <para>五个参数全走 <c>StoreD</c>，一次只能生成一个矩形；一批矩形（例如按拟合结果批量画框）必须用元组重载。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   using (JlXLDCont rect = new JlXLDCont())
	///   {
	///       rect.GenRectangle2ContourXld(300.0, 200.0, 0.0, 100.5, 20.5);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>本实例原内容被替换，用完 <c>Dispose()</c>。</para>
	/// </remarks>
	public void GenRectangle2ContourXld(double row, double column, double phi, double length1, double length2)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(24);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, phi);
		JlNativeApi.StoreD(proc, 3, length1);
		JlNativeApi.StoreD(proc, 4, length2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>逐点计算轮廓上每个点到给定旋转矩形的距离。</summary>
	/// <param name="clippingEndPoints">每条轮廓首尾各忽略不处理的点数，用于躲开截断产生的坏端点。Default: 0</param>
	/// <param name="row">矩形中心的行坐标（像素）。</param>
	/// <param name="column">矩形中心的列坐标（像素）。</param>
	/// <param name="phi">矩形主轴（length1 方向）相对列轴的转角 [rad]。</param>
	/// <param name="length1">主轴半长（像素）。</param>
	/// <param name="length2">副轴半长（像素）。</param>
	/// <returns>与轮廓点一一对应的距离元组；数组含多条轮廓时按轮廓顺序拼接成一个长元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>（本重载为主，元组版见 <c>JlXLD</c> 族说明的同类差异；本算子仅此一个标量签名）</para>
	///   <para>原生 id 25：本实例经 <c>Store(proc,1)</c> 作输入且不被改动；<c>clippingEndPoints</c> 走 <c>StoreI</c>、矩形 5 参数走 <c>StoreD</c>，全部是全局标量。输出 <c>InitOCT(proc,0)</c> 后按 <c>JlTupleType.DOUBLE</c> 装载——是控制值元组，不产生新句柄。矩形是旋转矩形（中心+角度+两半轴），距离按点到矩形周界计算，点在矩形内部时为到周界的距离 [待实测：内部点是取到边界距离还是恒 0、有无符号]。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>同一矩形作用于本实例的所有轮廓；被裁剪掉的端点是否仍占输出位置（0 填充还是整个跳过导致元组变短）[待实测]。点数过少（不足 2×clippingEndPoints+1）的轮廓如何处理 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要逐圆/椭圆的点距离用 <c>DistEllipseContourPointsXld</c>（本文件内）；要"每条轮廓一个 min/max/avg/sigma"统计值的 <c>DistRectangleContourXld</c> 本库不存在，只能拿本方法的逐点元组自行归约。判定"轮廓是否超出公差框"用逐点距离配阈值，比转区域做布尔更省且保留点对应关系。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\part.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlTuple dist = edges.DistRectangle2ContourPointsXld(0, 240.0, 320.0, 0.0, 150.0, 80.0);
	///   JlTuple num = edges.ContourPointNumXld();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>输出是拼接长元组，按下标追溯到"第 i 条轮廓第 j 点"必须先用 <c>ContourPointNumXld()</c> 算偏移，直接假设"一条轮廓一个值"会全部错位；纯数值元组无句柄，不必显式释放。</para>
	/// </remarks>
	public JlTuple DistRectangle2ContourPointsXld(int clippingEndPoints, double row, double column, double phi, double length1, double length2)
	{
		IntPtr proc = JlNativeApi.PreCall(25);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, clippingEndPoints);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		JlNativeApi.StoreD(proc, 3, phi);
		JlNativeApi.StoreD(proc, 4, length1);
		JlNativeApi.StoreD(proc, 5, length2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>对每条轮廓拟合旋转矩形，输出逐轮廓的矩形参数。</summary>
	/// <param name="algorithm">拟合算法："regression" 为最小二乘；含 huber/tukey 的为鲁棒加权（锚点 a 前缀按点到模型的偏差、数据 d 前缀按残差剔除离群点）。Default: "regression"</param>
	/// <param name="maxNumPoints">参与拟合的最大点数，-1 用全部点。Default: -1</param>
	/// <param name="maxClosureDist">首末点距离不超过该值即视为闭合轮廓。Default: 0.0</param>
	/// <param name="clippingEndPoints">每条轮廓首尾各剔除的点数。Default: 0</param>
	/// <param name="iterations">鲁棒加权最大迭代次数（"regression" 不使用）。Default: 3</param>
	/// <param name="clippingFactor">离群点剔除的截断因子（'huber' 常用 1.0，'tukey' 常用 2.0）。Default: 2.0</param>
	/// <param name="row">输出：各矩形中心的行坐标，逐轮廓一个值。</param>
	/// <param name="column">输出：各矩形中心的列坐标。</param>
	/// <param name="phi">输出：各矩形主轴转角 [rad]。</param>
	/// <param name="length1">输出：各矩形主轴半长。</param>
	/// <param name="length2">输出：各矩形副轴半长。</param>
	/// <param name="pointOrder">输出：点沿矩形边界的绕行方向（"positive"/"negative"），逐轮廓一个字符串。</param>
	/// <remarks>
	///   <para><b>功能说明</b>（本重载为主，<c>out double</c> 标量版本见其后）</para>
	///   <para>原生 id 26：本实例 <c>Store(proc,1)</c> 只读不改；<c>algorithm</c> 走 <c>StoreS</c>，其余 5 个控制参数 <c>StoreI</c>/<c>StoreD</c> 按签名序 0..5 送入。六个输出经 <c>InitOCT(0..5)</c> 全部用 <c>JlTuple.LoadNew</c> 装载，前五个按 DOUBLE、<c>pointOrder</c> 按元组自然类型（字符串）——即每条轮廓一个值、长度等于 <c>CountObj()</c>。输入轮廓不被改写，本方法无句柄输出。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>拟合对象应是近似矩形的外边界：开口轮廓或含多条边的碎片轮廓会拟出无意义参数 [待实测：对非闭合/非凸轮廓是否报错还是照样返回]。<c>maxClosureDist</c> 默认 0 意味着只有严格首末重合才算闭合；区域转来的 "border" 轮廓走像素中心，端点常差不到一个像素，给 1.0 左右更稳。</para>
	///   <para><b>参数取向</b></para>
	///   <para>点列干净用 "regression"（一次解算，<c>iterations</c> 被忽略、不生效）；边缘有毛刺或混入邻近结构时改 "atukey"/"dtukey" 并配 <c>clippingFactor</c>≈2.0，鲁棒迭代收敛慢但抗离群强。<c>clippingEndPoints</c> 专门用来躲 <c>EdgesSubPix</c> 在图像边界截断出的坏端点，通常 2~5。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\block.pgm");
	///   JlRegion reg = img.Threshold(60.0, 255.0);
	///   using (JlXLDCont parts = new JlXLDCont(reg, "border"))
	///   {
	///       parts.FitRectangle2ContourXld("atukey", -1, 1.0, 2, 3, 2.0, out JlTuple row, out JlTuple column, out JlTuple phi, out JlTuple length1, out JlTuple length2, out JlTuple pointOrder);
	///       int n = parts.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>输出元组与"数组内第 i 条轮廓"按下标对应——若之后调过 <c>SortContoursXld</c>/<c>SelectContoursXld</c> 重排或删减，旧元组即作废须在新数组上重算；<c>phi</c> 受主轴约定影响，长短轴接近的方形对噪声敏感、角度会跳变；数值元组不必显式释放。</para>
	/// </remarks>
	public void FitRectangle2ContourXld(string algorithm, int maxNumPoints, double maxClosureDist, int clippingEndPoints, int iterations, double clippingFactor, out JlTuple row, out JlTuple column, out JlTuple phi, out JlTuple length1, out JlTuple length2, out JlTuple pointOrder)
	{
		IntPtr proc = JlNativeApi.PreCall(26);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, algorithm);
		JlNativeApi.StoreI(proc, 1, maxNumPoints);
		JlNativeApi.StoreD(proc, 2, maxClosureDist);
		JlNativeApi.StoreI(proc, 3, clippingEndPoints);
		JlNativeApi.StoreI(proc, 4, iterations);
		JlNativeApi.StoreD(proc, 5, clippingFactor);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out phi);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out length1);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out length2);
		err = JlTuple.LoadNew(proc, 5, err, out pointOrder);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>拟合旋转矩形并按标量取出结果（只读每条输出的第一个值）。</summary>
	/// <param name="algorithm">拟合算法，取值同主重载。Default: "regression"</param>
	/// <param name="maxNumPoints">参与拟合的最大点数，-1 用全部点。Default: -1</param>
	/// <param name="maxClosureDist">首末点距离不超过该值即视为闭合。Default: 0.0</param>
	/// <param name="clippingEndPoints">每条轮廓首尾各剔除的点数。Default: 0</param>
	/// <param name="iterations">鲁棒加权最大迭代次数（"regression" 不使用）。Default: 3</param>
	/// <param name="clippingFactor">离群点截断因子（'huber' 约 1.0，'tukey' 约 2.0）。Default: 2.0</param>
	/// <param name="row">输出：第 1 条轮廓拟合矩形的中心行坐标。</param>
	/// <param name="column">输出：第 1 个矩形中心的列坐标。</param>
	/// <param name="phi">输出：第 1 个矩形的主轴转角 [rad]。</param>
	/// <param name="length1">输出：第 1 个矩形的主轴半长。</param>
	/// <param name="length2">输出：第 1 个矩形的副轴半长。</param>
	/// <param name="pointOrder">输出：第 1 条轮廓的绕行方向字符串。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>标量便捷重载，原生 id 同为 26，算法、闭合判定与参数取向见 <see cref="FitRectangle2ContourXld(string, int, double, int, int, double, out JlTuple, out JlTuple, out JlTuple, out JlTuple, out JlTuple, out JlTuple)"/>。</para>
	///   <para><b>与主重载的实际差异</b></para>
	///   <para>输出用 <c>LoadD</c>/<c>LoadS</c> 逐个装载：每个输出只读回元组的<b>第一个值</b>。数组里有多条轮廓时，第 2 条起的拟合结果被静默丢弃且无任何提示——"哪条是第一条"由数组顺序决定，而边缘检测的输出顺序并不稳定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\block.pgm");
	///   JlRegion reg = img.Threshold(60.0, 255.0);
	///   using (JlXLDCont parts = new JlXLDCont(reg, "border"))
	///   {
	///       using (JlXLDCont biggest = parts[1])
	///       {
	///           biggest.FitRectangle2ContourXld("regression", -1, 0.0, 0, 3, 2.0, out double row, out double column, out double phi, out double length1, out double length2, out string pointOrder);
	///       }
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>确有多条轮廓时用元组重载；用本重载时先 <c>SelectContoursXld</c> 或 <c>[1]</c> 把目标缩到唯一一条再拟合，否则取到的可能是错误的那条。示例中 <c>parts[1]</c> 产生新句柄，须单独 <c>Dispose()</c>。</para>
	/// </remarks>
	public void FitRectangle2ContourXld(string algorithm, int maxNumPoints, double maxClosureDist, int clippingEndPoints, int iterations, double clippingFactor, out double row, out double column, out double phi, out double length1, out double length2, out string pointOrder)
	{
		IntPtr proc = JlNativeApi.PreCall(26);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, algorithm);
		JlNativeApi.StoreI(proc, 1, maxNumPoints);
		JlNativeApi.StoreD(proc, 2, maxClosureDist);
		JlNativeApi.StoreI(proc, 3, clippingEndPoints);
		JlNativeApi.StoreI(proc, 4, iterations);
		JlNativeApi.StoreD(proc, 5, clippingFactor);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out row);
		err = JlNativeApi.LoadD(proc, 1, err, out column);
		err = JlNativeApi.LoadD(proc, 2, err, out phi);
		err = JlNativeApi.LoadD(proc, 3, err, out length1);
		err = JlNativeApi.LoadD(proc, 4, err, out length2);
		err = JlNativeApi.LoadS(proc, 5, err, out pointOrder);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>按轮廓点上已存储的局部属性值把轮廓切成满足区间的段。</summary>
	/// <param name="attribute">要检查的属性名，可给多值与多个区间配对。Default: "distance"</param>
	/// <param name="operation">多属性时的组合方式（"and"/"or"）。Default: "and"</param>
	/// <param name="min">各属性区间下界。Default: 150.0</param>
	/// <param name="max">各属性区间上界。Default: 99999.0</param>
	/// <returns>满足条件的轮廓段构成的新 XLD 数组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>（本重载为主，<c>string/double</c> 标量版本见其后）</para>
	///   <para>原生 id 27：本实例 <c>Store(proc,1)</c> 只读；<c>attribute</c>/<c>min</c>/<c>max</c> 走 <c>Store</c>+调用后 <c>UnpinTuple</c>（允许多值），<c>operation</c> 走 <c>StoreS</c>；结果 <c>LoadNew</c> 返回新句柄，条数与输入无对应关系。它是<b>点级</b>操作：读取每条轮廓上已存的局部属性序列，把属性值落在 [min,max] 内的连续点段切出来成为新轮廓。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>属性必须先由计算算子写到轮廓上（如 <c>DistanceContoursXld(contour2, mode)</c> 写出 "distance"）；没写过该属性的轮廓在此无值可筛，结果为空或行为未定义 [待实测：属性缺失时是跳过、报错还是整条透传]。默认 "distance" 配 150.0~99999.0 的示例区间只在大图上成立，实际须按自己设的参考轮廓换算。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>与 <c>SelectContoursXld</c> 的本质区别：后者按<b>整条轮廓</b>的统计特征取舍、不改变轮廓本身；本算子会把一条轮廓<b>切碎</b>，只留下合格片段（切完点数、条数全变）。要"整条留下/整条扔掉"用 Select，要"只要高差超标的局部段"才用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\flat.pgm");
	///   JlXLDCont edge = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont refLine = new JlXLDCont(new JlTuple(100.0, 100.0), new JlTuple(50.0, 350.0)))
	///   {
	///       JlXLDCont measured = edge.DistanceContoursXld(refLine, "perpendicular"); // mode 取值集合托管侧不校验 [待实测]
	///       JlXLDCont hot = measured.SegmentContourAttribXld(new JlTuple("distance"), "and", new JlTuple(5.0), new JlTuple(99999.0));
	///       hot.Dispose();
	///       measured.Dispose();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值需 <c>Dispose()</c>；片段的下标与原轮廓无关，若还要知道"这段来自原第几条"，须先对原数组 <c>LengthXld()</c>/<c>ContourPointNumXld()</c> 建立映射。<c>DistanceContoursXld</c> 与本方法各产生一个新句柄（示例里的 <c>measured</c>、<c>hot</c>），都要释放。</para>
	/// </remarks>
	public JlXLDCont SegmentContourAttribXld(JlTuple attribute, string operation, JlTuple min, JlTuple max)
	{
		IntPtr proc = JlNativeApi.PreCall(27);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, attribute);
		JlNativeApi.StoreS(proc, 1, operation);
		JlNativeApi.Store(proc, 2, min);
		JlNativeApi.Store(proc, 3, max);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(attribute);
		JlNativeApi.UnpinTuple(min);
		JlNativeApi.UnpinTuple(max);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按已存储的局部属性切出满足单一区间的轮廓段（标量便捷重载）。</summary>
	/// <param name="attribute">要检查的属性名（单值）。Default: "distance"</param>
	/// <param name="operation">多属性组合方式；单属性时不起作用。Default: "and"</param>
	/// <param name="min">区间下界。Default: 150.0</param>
	/// <param name="max">区间上界。Default: 99999.0</param>
	/// <returns>合格轮廓段组成的新 XLD 数组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>标量便捷重载，原生 id 同为 27，"点级切段、先算属性再筛、结果条数与原数组无对应"的要点见 <see cref="SegmentContourAttribXld(JlTuple, string, JlTuple, JlTuple)"/>。</para>
	///   <para><b>与主重载的实际差异</b></para>
	///   <para><c>attribute</c> 走 <c>StoreS</c>、区间走 <c>StoreD</c>：一次只能给一个属性和一个区间，无法多属性联筛（<c>operation</c> 因此形同虚设）。注意传字面量 <c>"distance", 150.0</c> 绑定到本重载，多属性需求要显式写 <c>new JlTuple(...)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\flat.pgm");
	///   JlXLDCont edge = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont refLine = new JlXLDCont(new JlTuple(100.0, 100.0), new JlTuple(50.0, 350.0)))
	///   {
	///       JlXLDCont measured = edge.DistanceContoursXld(refLine, "perpendicular");
	///       using (JlXLDCont hot = measured.SegmentContourAttribXld("distance", "and", 5.0, 99999.0))
	///       {
	///           int n = hot.CountObj();
	///       }
	///       measured.Dispose();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回值需 <c>Dispose()</c>；轮廓上不存在该属性时无段可切 [待实测：报错还是静默返回空]。</para>
	/// </remarks>
	public JlXLDCont SegmentContourAttribXld(string attribute, string operation, double min, double max)
	{
		IntPtr proc = JlNativeApi.PreCall(27);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, attribute);
		JlNativeApi.StoreS(proc, 1, operation);
		JlNativeApi.StoreD(proc, 2, min);
		JlNativeApi.StoreD(proc, 3, max);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把轮廓按直线段与圆/椭圆弧切分成几何基元。</summary>
	/// <param name="mode">分割模式（切成直线+圆、直线+椭圆还是纯多边形等）。Default: "lines_circles"</param>
	/// <param name="smoothCont">先做平滑时所用滑窗的点数（奇数），0 表示不平滑。Default: 5</param>
	/// <param name="maxLineDist1">第一轮用"直线逼近轮廓"允许的最大偏差（像素）。Default: 4.0</param>
	/// <param name="maxLineDist2">第二轮精修时允许的最大偏差（像素）。Default: 2.0</param>
	/// <returns>切分后的基元轮廓数组（新句柄），条数一般多于输入。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 28：<c>StoreS(mode)</c> + <c>StoreI(smoothCont)</c> + 两个 <c>StoreD</c> 阈值，本实例作索引 1 输入不被改动，结果 <c>LoadNew</c> 为新句柄。做法是先平滑、再迭代地把轮廓拆为直线段与圆/椭圆弧，直到每段对原曲线的偏差不超过给定阈值。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>输入应是无自交、无明显噪声毛刺的轮廓：毛刺会被当成"基元"切碎，先 <c>smoothCont&gt;0</c> 平滑再分割。阈值给小了碎片爆炸、给大了圆角会被直接吃掉成直线 [待实测：mode 与两个阈值冲突时的取舍]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要"点数少、按折线保存"用 <c>GenPolygonsXld</c>（结果是多边形对象 JlXLDPoly）；已经知道形状是"几条边"时用 <c>GenContourPolygonXld(row, col)</c> 手工给出顶点更可控；本算子适合形状未知、需要先分解成直线+圆弧再逐段拟合的场景。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\bracket.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont parts = edges.SegmentContoursXld("lines_circles", 5, 4.0, 2.0))
	///   {
	///       int n = parts.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>输出条数、顺序与输入无对应关系，per-contour 缓存全部作废；每段输出仍是"采样点列"而非参数化基元，要拿圆心半径须再对单条结果调 <c>FitCircleContourXld</c> 等；返回值需 <c>Dispose()</c>。</para>
	/// </remarks>
	public JlXLDCont SegmentContoursXld(string mode, int smoothCont, double maxLineDist1, double maxLineDist2)
	{
		IntPtr proc = JlNativeApi.PreCall(28);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, smoothCont);
		JlNativeApi.StoreD(proc, 2, maxLineDist1);
		JlNativeApi.StoreD(proc, 3, maxLineDist2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>对每条轮廓拟合圆或圆弧，逐轮廓输出圆心、半径与起止角。</summary>
	/// <param name="algorithm">圆拟合算法："algebraic" 代数最小二乘最快；"geometric" 按几何距离迭代更准；含 huber/tukey 的为鲁棒变体。Default: "algebraic"</param>
	/// <param name="maxNumPoints">参与拟合的最大点数，-1 全用。Default: -1</param>
	/// <param name="maxClosureDist">首末点距离不超过该值视为闭合（整圆）。Default: 0.0</param>
	/// <param name="clippingEndPoints">每条轮廓首尾各剔除的点数。Default: 0</param>
	/// <param name="iterations">鲁棒加权最大迭代次数。Default: 3</param>
	/// <param name="clippingFactor">离群点截断因子（Huber 约 1.0、Tukey 约 2.0）。Default: 2.0</param>
	/// <param name="row">输出：各圆心行坐标，逐轮廓一个值。</param>
	/// <param name="column">输出：各圆心列坐标。</param>
	/// <param name="radius">输出：各圆半径（像素）。</param>
	/// <param name="startPhi">输出：弧起点角 [rad]。</param>
	/// <param name="endPhi">输出：弧终点角 [rad]。</param>
	/// <param name="pointOrder">输出：绕行方向（"positive"/"negative"），逐轮廓一个字符串。</param>
	/// <remarks>
	///   <para><b>功能说明</b>（本重载为主，<c>out double</c> 标量版本见其后）</para>
	///   <para>原生 id 29：本实例只读；输入 6 个控制参数 <c>StoreS/StoreI/StoreD</c> 按签名序 0..5，输出 6 个经 <c>InitOCT(0..5)</c>，前五个按 DOUBLE、<c>pointOrder</c> 按自然类型装载。每个输出长度等于 <c>CountObj()</c>，与轮廓按下标一一对应；整圆与短弧给同一组签名，靠 <c>startPhi/endPhi</c> 的跨度区分。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>接近直线的轮廓不会报错，而是拟出一个巨大半径、圆心跑到图外的"圆"——必须用 <c>radius</c> 上限或弧长/转角比自行剔除这种退化结果 [待实测：退化时是否返回 inf/NaN]。<c>maxClosureDist</c> 默认 0 意味着只有严格闭合的轮廓才按整圆处理。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>先 <c>SegmentContoursXld("lines_circles", ...)</c> 把折线与圆弧拆开再对本方法喂圆弧段，直接对整条多基元轮廓拟合圆必错。要椭圆用 <c>FitEllipseContourXld</c>；"圆度够不够好"的校验本库没有圆的聚合距离算子（<c>DistCircleContourXld</c> 本库不存在），可把 <c>DistEllipseContourPointsXld</c> 的两个半轴取等当圆用。代数法适合实时线阵，几何法适合精密测量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\pins.pgm");
	///   JlRegion reg = img.Threshold(100.0, 255.0);
	///   using (JlXLDCont holes = new JlXLDCont(reg, "border"))
	///   {
	///       holes.FitCircleContourXld("algebraic", -1, 3.0, 0, 3, 2.0, out JlTuple row, out JlTuple column, out JlTuple radius, out JlTuple startPhi, out JlTuple endPhi, out JlTuple pointOrder);
	///       int n = holes.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>角度是相对列轴（+x）的 [rad] 值，起终点角的跨法与 <c>pointOrder</c> 联动，判断"缺口在哪个象限"前先确认绕行方向 [待实测：角范围是 -pi..pi 还是 0..2pi]；排序/筛选后旧输出元组与下标错位，须重算；数值元组无句柄不必释放。</para>
	/// </remarks>
	public void FitCircleContourXld(string algorithm, int maxNumPoints, double maxClosureDist, int clippingEndPoints, int iterations, double clippingFactor, out JlTuple row, out JlTuple column, out JlTuple radius, out JlTuple startPhi, out JlTuple endPhi, out JlTuple pointOrder)
	{
		IntPtr proc = JlNativeApi.PreCall(29);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, algorithm);
		JlNativeApi.StoreI(proc, 1, maxNumPoints);
		JlNativeApi.StoreD(proc, 2, maxClosureDist);
		JlNativeApi.StoreI(proc, 3, clippingEndPoints);
		JlNativeApi.StoreI(proc, 4, iterations);
		JlNativeApi.StoreD(proc, 5, clippingFactor);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out radius);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out startPhi);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out endPhi);
		err = JlTuple.LoadNew(proc, 5, err, out pointOrder);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>拟合圆/圆弧并按标量取出结果（只读每条输出的第一个值）。</summary>
	/// <param name="algorithm">圆拟合算法，取值同主重载。Default: "algebraic"</param>
	/// <param name="maxNumPoints">参与拟合的最大点数，-1 全用。Default: -1</param>
	/// <param name="maxClosureDist">闭合判定距离。Default: 0.0</param>
	/// <param name="clippingEndPoints">首尾各剔除的点数。Default: 0</param>
	/// <param name="iterations">鲁棒迭代次数。Default: 3</param>
	/// <param name="clippingFactor">离群点截断因子。Default: 2.0</param>
	/// <param name="row">输出：第 1 个圆心行坐标。</param>
	/// <param name="column">输出：第 1 个圆心列坐标。</param>
	/// <param name="radius">输出：第 1 个半径。</param>
	/// <param name="startPhi">输出：第 1 条弧起点角 [rad]。</param>
	/// <param name="endPhi">输出：第 1 条弧终点角 [rad]。</param>
	/// <param name="pointOrder">输出：第 1 条轮廓的绕行方向。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>标量便捷重载，原生 id 同为 29；算法取向与退化圆的坑见 <see cref="FitCircleContourXld(string, int, double, int, int, double, out JlTuple, out JlTuple, out JlTuple, out JlTuple, out JlTuple, out JlTuple)"/>。</para>
	///   <para><b>与主重载的实际差异</b></para>
	///   <para>输出用 <c>LoadD</c>/<c>LoadS</c>，每个只读回第一个值：数组含多条轮廓时第 2 条起的圆全部丢弃且不报错。只拟合"图里唯一那个圆"时用本重载，否则用元组重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\dial.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont ring = edges.SelectContoursXld("contour_length", 200.0, 99999.0, -99999.0, 99999.0))
	///   {
	///       ring.FitCircleContourXld("algebraic", -1, 3.0, 0, 3, 2.0, out double row, out double column, out double radius, out double startPhi, out double endPhi, out string pointOrder);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>本示例先用 <c>SelectContoursXld</c> 把候选缩到少数长轮廓再取标量——若 <c>ring</c> 仍含多条轮廓，取到的是数组第 1 条而非"最长的"，顺序不保证；返回值 <c>ring</c> 需 <c>Dispose()</c>。</para>
	/// </remarks>
	public void FitCircleContourXld(string algorithm, int maxNumPoints, double maxClosureDist, int clippingEndPoints, int iterations, double clippingFactor, out double row, out double column, out double radius, out double startPhi, out double endPhi, out string pointOrder)
	{
		IntPtr proc = JlNativeApi.PreCall(29);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, algorithm);
		JlNativeApi.StoreI(proc, 1, maxNumPoints);
		JlNativeApi.StoreD(proc, 2, maxClosureDist);
		JlNativeApi.StoreI(proc, 3, clippingEndPoints);
		JlNativeApi.StoreI(proc, 4, iterations);
		JlNativeApi.StoreD(proc, 5, clippingFactor);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out row);
		err = JlNativeApi.LoadD(proc, 1, err, out column);
		err = JlNativeApi.LoadD(proc, 2, err, out radius);
		err = JlNativeApi.LoadD(proc, 3, err, out startPhi);
		err = JlNativeApi.LoadD(proc, 4, err, out endPhi);
		err = JlNativeApi.LoadS(proc, 5, err, out pointOrder);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>对每条轮廓拟合直线段，逐轮廓输出端点与 Hesse 法线式参数。</summary>
	/// <param name="algorithm">直线拟合算法："regression" 最小二乘；huber/tukey 系为鲁棒加权。Default: "tukey"</param>
	/// <param name="maxNumPoints">参与拟合的最大点数，-1 全用。Default: -1</param>
	/// <param name="clippingEndPoints">首尾各剔除的点数。Default: 0</param>
	/// <param name="iterations">鲁棒迭代次数（"regression" 不用）。Default: 5</param>
	/// <param name="clippingFactor">离群点截断因子（'huber'/'drop' 约 1.0，'tukey' 约 2.0）。Default: 2.0</param>
	/// <param name="rowBegin">输出：各线段起点行坐标（轮廓首点在直线上的投影）。</param>
	/// <param name="colBegin">输出：各线段起点列坐标。</param>
	/// <param name="rowEnd">输出：各线段终点行坐标（轮廓末点在直线上的投影）。</param>
	/// <param name="colEnd">输出：各线段终点列坐标。</param>
	/// <param name="nr">输出：单位法向量的行分量（Hesse 式 nr*row + nc*col = dist）。</param>
	/// <param name="nc">输出：单位法向量的列分量。</param>
	/// <param name="dist">输出：直线到坐标原点（图像左上像素中心）的有向距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>（本重载为主，<c>out double</c> 标量版本见其后）</para>
	///   <para>原生 id 30：本实例只读。输入 5 控制参数按 0..4 送入；输出 7 个全部 <c>InitOCT</c>+<c>JlTuple.LoadNew(DOUBLE)</c>，逐轮廓一个值。拟合线由 (nr,nc,dist) 唯一表示（法向单位向量、原点到线距离），端点只是同一条线的另一种读法——闭合轮廓上端点会绕回，别拿它当"线段长度"。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>对折线/圆弧整条拟一条直线没有意义，先 <c>SegmentContoursXld("lines_ellipses", ...)</c> 或 <c>SelectContoursXld</c> 分出直段。法向朝向与轮廓绕行方向相关 [待实测：positive/negative 轮廓下 nr/nc 的符号约定]，比较角度时用 atan2(nc,nr) 并把差值折到 ±π/2 内（直线无"正反"）。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要"沿轮廓滑动的局部方向"用 <c>GetRegressParamsXld</c>（逐点回归法向/位置，开销大）；要整条一条线用本方法；要把点列压成两端点折线用 <c>GenPolygonsXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\rail.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont parts = edges.SegmentContoursXld("lines_ellipses", 5, 4.0, 2.0))
	///   {
	///       parts.FitLineContourXld("atukey", -1, 0, 5, 1.0, out JlTuple rowBegin, out JlTuple colBegin, out JlTuple rowEnd, out JlTuple colEnd, out JlTuple nr, out JlTuple nc, out JlTuple dist);
	///       int n = parts.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>7 个元组间按下标配对（第 i 个值同属第 i 条轮廓），任何改变数组顺序的操作都使旧元组作废；<c>dist</c> 是像素单位、原点参考，平移图像坐标系后不可直接比较；数值元组无需显式释放，<c>parts</c> 需 <c>Dispose()</c>。</para>
	/// </remarks>
	public void FitLineContourXld(string algorithm, int maxNumPoints, int clippingEndPoints, int iterations, double clippingFactor, out JlTuple rowBegin, out JlTuple colBegin, out JlTuple rowEnd, out JlTuple colEnd, out JlTuple nr, out JlTuple nc, out JlTuple dist)
	{
		IntPtr proc = JlNativeApi.PreCall(30);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, algorithm);
		JlNativeApi.StoreI(proc, 1, maxNumPoints);
		JlNativeApi.StoreI(proc, 2, clippingEndPoints);
		JlNativeApi.StoreI(proc, 3, iterations);
		JlNativeApi.StoreD(proc, 4, clippingFactor);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowBegin);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out colBegin);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out rowEnd);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out colEnd);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out nr);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out nc);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out dist);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>拟合直线段并按标量取出结果（只读每条输出的第一个值）。</summary>
	/// <param name="algorithm">直线拟合算法，取值同主重载。Default: "tukey"</param>
	/// <param name="maxNumPoints">参与拟合的最大点数，-1 全用。Default: -1</param>
	/// <param name="clippingEndPoints">首尾各剔除的点数。Default: 0</param>
	/// <param name="iterations">鲁棒迭代次数。Default: 5</param>
	/// <param name="clippingFactor">离群点截断因子。Default: 2.0</param>
	/// <param name="rowBegin">输出：第 1 条线段的起点行坐标。</param>
	/// <param name="colBegin">输出：第 1 条线段的起点列坐标。</param>
	/// <param name="rowEnd">输出：第 1 条线段的终点行坐标。</param>
	/// <param name="colEnd">输出：第 1 条线段的终点列坐标。</param>
	/// <param name="nr">输出：第 1 条线的单位法向量行分量。</param>
	/// <param name="nc">输出：第 1 条线的单位法向量列分量。</param>
	/// <param name="dist">输出：第 1 条线到原点的有向距离（像素）。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>标量便捷重载，原生 id 同为 30；Hesse 参数含义与用法见 <see cref="FitLineContourXld(string, int, int, int, double, out JlTuple, out JlTuple, out JlTuple, out JlTuple, out JlTuple, out JlTuple, out JlTuple)"/>。</para>
	///   <para><b>与主重载的实际差异</b></para>
	///   <para>7 个输出经 <c>LoadD</c> 各只读回第一个值：输入数组含多条轮廓时，只得到"第 1 条"的拟合结果，其余静默丢弃。先确保数组里只剩目标轮廓（<c>SelectContoursXld</c> 或 <c>[i]</c>）再用本重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\rail.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont rail = edges.SelectContoursXld("contour_length", 300.0, 99999.0, -99999.0, 99999.0))
	///   {
	///       rail.FitLineContourXld("regression", -1, 0, 5, 2.0, out double rowBegin, out double colBegin, out double rowEnd, out double colEnd, out double nr, out double nc, out double dist);
	///       double angle = Math.Atan2(nc, nr);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>"regression" 时 <c>iterations</c>/<c>clippingFactor</c> 不生效但仍必传；<c>Math.Atan2</c> 可直接用（System 已在域内）；返回值需 <c>Dispose()</c>。</para>
	/// </remarks>
	public void FitLineContourXld(string algorithm, int maxNumPoints, int clippingEndPoints, int iterations, double clippingFactor, out double rowBegin, out double colBegin, out double rowEnd, out double colEnd, out double nr, out double nc, out double dist)
	{
		IntPtr proc = JlNativeApi.PreCall(30);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, algorithm);
		JlNativeApi.StoreI(proc, 1, maxNumPoints);
		JlNativeApi.StoreI(proc, 2, clippingEndPoints);
		JlNativeApi.StoreI(proc, 3, iterations);
		JlNativeApi.StoreD(proc, 4, clippingFactor);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out rowBegin);
		err = JlNativeApi.LoadD(proc, 1, err, out colBegin);
		err = JlNativeApi.LoadD(proc, 2, err, out rowEnd);
		err = JlNativeApi.LoadD(proc, 3, err, out colEnd);
		err = JlNativeApi.LoadD(proc, 4, err, out nr);
		err = JlNativeApi.LoadD(proc, 5, err, out nc);
		err = JlNativeApi.LoadD(proc, 6, err, out dist);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	/// 逐点计算本轮廓集中每个采样点到指定椭圆边界的距离，返回展平后的距离元组。
	/// </summary>
	/// <param name="distanceMode">距离形式："unsigned" 只给无符号距离；"signed" 给带符号距离（内/外侧的符号约定 [待实测]）。Default: "unsigned"</param>
	/// <param name="clippingEndPoints">每条轮廓首、尾各忽略的点数，用于剔除端部毛刺点。Default: 0</param>
	/// <param name="row">椭圆中心的行坐标（像素，向下为正）。</param>
	/// <param name="column">椭圆中心的列坐标（像素，向右为正）。</param>
	/// <param name="phi">椭圆长轴方向角，弧度制。</param>
	/// <param name="radius1">长半轴长度（像素）。</param>
	/// <param name="radius2">短半轴长度（像素）。</param>
	/// <returns>新分配的 DOUBLE 型 JlTuple（LoadNew 新建句柄，需 Dispose()）；本实例轮廓不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 31：本实例经 <c>Store(proc,1)</c> 以控制参数索引 1 送入（轮廓在原生侧排第 2 位），<c>distanceMode</c>~<c>radius2</c> 依次占控制索引 0~6。输出逐点距离按 DOUBLE 装载；多条轮廓的距离在元组里的拼接顺序 [待实测]。</para>
	///   <para><b>约束或前提</b>只测量不修改：轮廓点序、闭合标志不影响数值，但 <c>clippingEndPoints</c> 是对每条轮廓首尾各裁 N 个点，裁掉点数超过轮廓一半有效点时的行为 [待实测]。<c>phi</c> 必须是弧度，按度传入会得到转错方向的椭圆。</para>
	///   <para><b>与相邻算子的取舍</b>只需要每条轮廓一个汇总统计（最小/最大/平均/标准差）时用 <see cref="DistEllipseContourXld(string,int,int,double,double,double,double,double,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>；要逐点贴合剖面（如检测轮廓哪一段偏离椭圆）才用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlImage img = new JlImage(@"C:\vision\part.pgm"))
	///   using (JlXLDCont conts = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlTuple dist = conts.DistEllipseContourPointsXld("unsigned", 0, 200.0, 200.0, 0.0, 100.0, 50.0))
	///   {
	///       int nPts = dist.Length;
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回元组是新句柄，用完 <c>Dispose()</c>（纯数值元组亦建议处理）；<c>GC.KeepAlive(this)</c> 表明调用结束前本实例不得释放，调用返回后即可安全 Dispose 轮廓。</para>
	/// </remarks>
	public JlTuple DistEllipseContourPointsXld(string distanceMode, int clippingEndPoints, double row, double column, double phi, double radius1, double radius2)
	{
		IntPtr proc = JlNativeApi.PreCall(31);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, distanceMode);
		JlNativeApi.StoreI(proc, 1, clippingEndPoints);
		JlNativeApi.StoreD(proc, 2, row);
		JlNativeApi.StoreD(proc, 3, column);
		JlNativeApi.StoreD(proc, 4, phi);
		JlNativeApi.StoreD(proc, 5, radius1);
		JlNativeApi.StoreD(proc, 6, radius2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	/// 计算每条轮廓到指定椭圆的距离统计量（最小/最大/平均/标准差），逐轮廓输出为元组。
	/// </summary>
	/// <param name="mode">距离确定方式，可选值集合 [待实测]。Default: "geometric"</param>
	/// <param name="maxNumPoints">参与统计的轮廓点数上限，-1 表示全部点。Default: -1</param>
	/// <param name="clippingEndPoints">每条轮廓首、尾各忽略的点数。Default: 0</param>
	/// <param name="row">椭圆中心的行坐标（像素）。</param>
	/// <param name="column">椭圆中心的列坐标（像素）。</param>
	/// <param name="phi">椭圆长轴方向角，弧度制。</param>
	/// <param name="radius1">长半轴长度（像素）。</param>
	/// <param name="radius2">短半轴长度（像素）。</param>
	/// <param name="minDist">每条轮廓的最小距离（DOUBLE 新元组，需 Dispose）。</param>
	/// <param name="maxDist">每条轮廓的最大距离（DOUBLE 新元组，需 Dispose）。</param>
	/// <param name="avgDist">每条轮廓的平均距离（DOUBLE 新元组，需 Dispose）。</param>
	/// <param name="sigmaDist">每条轮廓距离的标准差（DOUBLE 新元组，需 Dispose）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 32：轮廓以控制索引 1 送入（<c>Store(proc,1)</c>），<c>mode</c> 走 <c>StoreS</c> 索引 0，其余数值参数占索引 1~7。四个输出均 <c>JlTuple.LoadNew(..., DOUBLE)</c> 新建，各含每轮廓一个值，元组下标与轮廓序号对齐 [待实测]。</para>
	///   <para><b>约束或前提</b><c>maxNumPoints</c> 限制参与统计的点数（超限时的取点方式 [待实测]）；<c>clippingEndPoints</c> 先裁边再统计，端点毛刺会显著拉高 <c>maxDist</c>，通常配合使用。<c>phi</c> 用弧度。</para>
	///   <para><b>与标量重载的取舍</b>本重载对多条轮廓逐条给值；<see cref="DistEllipseContourXld(string,int,int,double,double,double,double,double,out double,out double,out double,out double)"/> 只回传第一条轮廓的统计（内部 <c>LoadD</c> 仅读首值），多轮廓时不要用那个。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlImage img = new JlImage(@"C:\vision\part.pgm"))
	///   using (JlXLDCont conts = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       conts.DistEllipseContourXld("geometric", -1, 0, 200.0, 200.0, 0.0, 100.0, 50.0,
	///           out JlTuple minD, out JlTuple maxD, out JlTuple avgD, out JlTuple sigD);
	///       int n = minD.Length;
	///       minD.Dispose(); maxD.Dispose(); avgD.Dispose(); sigD.Dispose();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>四个 out 元组都是新句柄，必须逐个释放；本实例只读不改（<c>GC.KeepAlive(this)</c>），调用返回后即可 Dispose。</para>
	/// </remarks>
	public void DistEllipseContourXld(string mode, int maxNumPoints, int clippingEndPoints, double row, double column, double phi, double radius1, double radius2, out JlTuple minDist, out JlTuple maxDist, out JlTuple avgDist, out JlTuple sigmaDist)
	{
		IntPtr proc = JlNativeApi.PreCall(32);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, maxNumPoints);
		JlNativeApi.StoreI(proc, 2, clippingEndPoints);
		JlNativeApi.StoreD(proc, 3, row);
		JlNativeApi.StoreD(proc, 4, column);
		JlNativeApi.StoreD(proc, 5, phi);
		JlNativeApi.StoreD(proc, 6, radius1);
		JlNativeApi.StoreD(proc, 7, radius2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out minDist);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out maxDist);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out avgDist);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out sigmaDist);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	/// 计算轮廓到指定椭圆的距离统计量，标量版：每条输出只回传第一条轮廓的值。
	/// </summary>
	/// <param name="mode">距离确定方式，可选值集合 [待实测]。Default: "geometric"</param>
	/// <param name="maxNumPoints">参与统计的轮廓点数上限，-1 表示全部点。Default: -1</param>
	/// <param name="clippingEndPoints">每条轮廓首、尾各忽略的点数。Default: 0</param>
	/// <param name="row">椭圆中心的行坐标（像素）。</param>
	/// <param name="column">椭圆中心的列坐标（像素）。</param>
	/// <param name="phi">椭圆长轴方向角，弧度制。</param>
	/// <param name="radius1">长半轴长度（像素）。</param>
	/// <param name="radius2">短半轴长度（像素）。</param>
	/// <param name="minDist">最小距离（仅第一条轮廓）。</param>
	/// <param name="maxDist">最大距离（仅第一条轮廓）。</param>
	/// <param name="avgDist">平均距离（仅第一条轮廓）。</param>
	/// <param name="sigmaDist">距离标准差（仅第一条轮廓）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一个原生 id 32，调用完全相同；差异只在取回：<c>JlNativeApi.LoadD</c> 对每个输出只读元组第 1 个 DOUBLE 值，不新建句柄、无释放义务。</para>
	///   <para><b>真坑</b>轮廓集中有多条时，第 2 条及以后的统计值被静默丢弃且<b>不报错</b>——拿到的是"第一条轮廓"的数字，很容易被误当成全集统计。多轮廓必须改用 <see cref="DistEllipseContourXld(string,int,int,double,double,double,double,double,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>。"第一条"取决于轮廓在集合中的存储顺序，经 <c>SortContoursXld</c> 等操作后可能变化 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>典型用法是对单条拟合目标（如单独生成的椭圆弧）快速核对偏差；要逐点剖面用 <c>DistEllipseContourPointsXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlXLDCont c = new JlXLDCont())
	///   {
	///       c.GenCircleContourXld(200.0, 300.0, 80.0, 0.0, 6.28318, "positive", 1.0);
	///       c.DistEllipseContourXld("geometric", -1, 0, 200.0, 300.0, 0.0, 80.0, 80.0,
	///           out double minD, out double maxD, out double avgD, out double sigD);
	///       // 圆对同尺寸椭圆（两半轴相等）的偏差：avgD 应接近 0
	///   }
	///   </code>
	///   <para><b>资源与坑</b>本实例只读（<c>GC.KeepAlive(this)</c>），调用结束前不释放即可；输出为纯标量，无需 Dispose。</para>
	/// </remarks>
	public void DistEllipseContourXld(string mode, int maxNumPoints, int clippingEndPoints, double row, double column, double phi, double radius1, double radius2, out double minDist, out double maxDist, out double avgDist, out double sigmaDist)
	{
		IntPtr proc = JlNativeApi.PreCall(32);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, maxNumPoints);
		JlNativeApi.StoreI(proc, 2, clippingEndPoints);
		JlNativeApi.StoreD(proc, 3, row);
		JlNativeApi.StoreD(proc, 4, column);
		JlNativeApi.StoreD(proc, 5, phi);
		JlNativeApi.StoreD(proc, 6, radius1);
		JlNativeApi.StoreD(proc, 7, radius2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out minDist);
		err = JlNativeApi.LoadD(proc, 1, err, out maxDist);
		err = JlNativeApi.LoadD(proc, 2, err, out avgDist);
		err = JlNativeApi.LoadD(proc, 3, err, out sigmaDist);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	/// 把每条 XLD 轮廓拟合为椭圆或椭圆弧，逐轮廓输出中心、半轴、角度与走向。
	/// </summary>
	/// <param name="algorithm">椭圆拟合算法（带 "*huber"/*tukey" 后缀为鲁棒加权变体，其余取值集合 [待实测]）。Default: "fitzgibbon"</param>
	/// <param name="maxNumPoints">参与拟合的点数上限，-1 表示全部。Default: -1</param>
	/// <param name="maxClosureDist">首尾点距离不超过该值（像素）即按"闭合轮廓"处理，Default: 0.0</param>
	/// <param name="clippingEndPoints">拟合时每条轮廓首、尾各忽略的点数。Default: 0</param>
	/// <param name="vossTabSize">Voss 法使用的圆分割段数，对其他算法是否生效 [待实测]。Default: 200</param>
	/// <param name="iterations">鲁棒加权拟合的最大迭代次数。Default: 3</param>
	/// <param name="clippingFactor">剔除离群点的截断因子（typical: 1.0 for '*huber' and 2.0 for '*tukey'）。Default: 2.0</param>
	/// <param name="row">各椭圆中心行坐标（DOUBLE 新元组，需 Dispose）。</param>
	/// <param name="column">各椭圆中心列坐标（DOUBLE 新元组，需 Dispose）。</param>
	/// <param name="phi">长轴方向角，弧度（DOUBLE 新元组，需 Dispose）。</param>
	/// <param name="radius1">长半轴（DOUBLE 新元组，需 Dispose）。</param>
	/// <param name="radius2">短半轴（DOUBLE 新元组，需 Dispose）。</param>
	/// <param name="startPhi">弧段起点角，弧度（DOUBLE 新元组，需 Dispose）。</param>
	/// <param name="endPhi">弧段终点角，弧度（DOUBLE 新元组，需 Dispose）。</param>
	/// <param name="pointOrder">各轮廓相对拟合椭圆的走向（原生 STRING 元组，需 Dispose）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 33：轮廓以控制索引 1 送入（<c>Store(proc,1)</c>），<c>algorithm</c> 走 <c>StoreS</c> 索引 0、其余占 1~6。输出 0~6 按 DOUBLE <c>LoadNew</c> 新建；<c>pointOrder</c>（输出 7）用不指定类型的 <c>JlTuple.LoadNew</c>，由原生类型决定装载——标量重载对同一输出走 <c>LoadS</c> 可证其为字符串元组（"positive"/"negative"，含义见下）。</para>
	///   <para><b>约束或前提</b><c>maxClosureDist</c> 决定开轮廓是否被当作整椭圆：默认 0.0 时首尾不重合的开弧只拟合为椭圆弧（<c>startPhi</c>/<c>endPhi</c> 给弧段范围）；点太少（少于 5 个自由度假设 [待实测]）会得到退化椭圆。<c>iterations</c>/<c>clippingFactor</c> 只对带 *huber/*tukey 后缀的鲁棒算法有意义（由参数文档的典型值说明可推知）。</para>
	///   <para><b>顺序/状态依赖</b>拟合结果只反映调用时刻的点集：做过 <c>AffineTransContourXld</c>、<c>ClipContoursXld</c> 等改动后必须重新拟合；<c>pointOrder</c> 依赖轮廓点序方向，把轮廓点序整体反向（本库不存在 ReverseContoursXld 之类算子，需自行重建轮廓）预期会把它从 "positive" 变 "negative" [待实测]。各输出元组下标与输入轮廓序号一一对应 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>整圆目标用 <c>FitCircleContourXld</c>（参数更少、少一个半轴自由度）；要拿拟合椭圆再画轮廓用 <c>GenEllipseContourXld</c>（本库无 Contregion 包装，拟合结果不能直接转区域）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlImage img = new JlImage(@"C:\vision\part.pgm"))
	///   using (JlXLDCont conts = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       conts.FitEllipseContourXld("fitzgibbon", -1, 0.0, 0, 200, 3, 2.0,
	///           out JlTuple row, out JlTuple col, out JlTuple phi, out JlTuple r1, out JlTuple r2,
	///           out JlTuple startPhi, out JlTuple endPhi, out JlTuple order);
	///       int n = row.Length;
	///       row.Dispose(); col.Dispose(); phi.Dispose(); r1.Dispose(); r2.Dispose();
	///       startPhi.Dispose(); endPhi.Dispose(); order.Dispose();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>八个 out 全是新句柄，逐个 Dispose；漏掉的字符串元组 <c>order</c> 最容易被忘。度量单位是像素/弧度，不是毫米/角度。</para>
	/// </remarks>
	public void FitEllipseContourXld(string algorithm, int maxNumPoints, double maxClosureDist, int clippingEndPoints, int vossTabSize, int iterations, double clippingFactor, out JlTuple row, out JlTuple column, out JlTuple phi, out JlTuple radius1, out JlTuple radius2, out JlTuple startPhi, out JlTuple endPhi, out JlTuple pointOrder)
	{
		IntPtr proc = JlNativeApi.PreCall(33);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, algorithm);
		JlNativeApi.StoreI(proc, 1, maxNumPoints);
		JlNativeApi.StoreD(proc, 2, maxClosureDist);
		JlNativeApi.StoreI(proc, 3, clippingEndPoints);
		JlNativeApi.StoreI(proc, 4, vossTabSize);
		JlNativeApi.StoreI(proc, 5, iterations);
		JlNativeApi.StoreD(proc, 6, clippingFactor);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out phi);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out radius1);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out radius2);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out startPhi);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out endPhi);
		err = JlTuple.LoadNew(proc, 7, err, out pointOrder);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	/// 把 XLD 轮廓拟合为椭圆/椭圆弧的标量版：每条输出只回传第一条轮廓的拟合结果。
	/// </summary>
	/// <param name="algorithm">椭圆拟合算法（*huber/*tukey 后缀为鲁棒变体，其余取值集合 [待实测]）。Default: "fitzgibbon"</param>
	/// <param name="maxNumPoints">参与拟合的点数上限，-1 表示全部。Default: -1</param>
	/// <param name="maxClosureDist">首尾点距离不超过该值（像素）即按闭合轮廓处理。Default: 0.0</param>
	/// <param name="clippingEndPoints">拟合时每条轮廓首、尾各忽略的点数。Default: 0</param>
	/// <param name="vossTabSize">Voss 法的圆分割段数。Default: 200</param>
	/// <param name="iterations">鲁棒加权拟合的最大迭代次数。Default: 3</param>
	/// <param name="clippingFactor">离群点截断因子（typical: 1.0 for '*huber' and 2.0 for '*tukey'）。Default: 2.0</param>
	/// <param name="row">第一条椭圆的中心行坐标。</param>
	/// <param name="column">第一条椭圆的中心列坐标。</param>
	/// <param name="phi">长轴方向角，弧度。</param>
	/// <param name="radius1">长半轴（像素）。</param>
	/// <param name="radius2">短半轴（像素）。</param>
	/// <param name="startPhi">弧段起点角，弧度。</param>
	/// <param name="endPhi">弧段终点角，弧度。</param>
	/// <param name="pointOrder">第一条轮廓的走向字符串（"positive"/"negative"）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一个原生 id 33，参数布置一致；取回时 7 个几何量走 <c>LoadD</c>（只读各输出元组第 1 个 DOUBLE），<c>pointOrder</c> 走 <c>LoadS</c>（读第 1 个字符串）。输出为纯标量，无新句柄、无 Dispose 义务。</para>
	///   <para><b>真坑</b>轮廓集有多条时只得到"第一条轮廓"的椭圆，其余静默丢弃、不报错；且该重载不做类型检查就把 STRING 读进 <c>pointOrder</c>，可反证元组重载里输出 7 的原生类型是字符串。批量拟合必须用 <see cref="FitEllipseContourXld(string,int,double,int,int,int,double,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>。</para>
	///   <para><b>顺序/状态依赖</b>拟合只在调用瞬间生效：输入点集被改变（变换、裁剪、去噪）后必须重拟合；角度全是弧度、长度全是像素。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlXLDCont e = new JlXLDCont())
	///   {
	///       e.GenEllipseContourXld(200.0, 200.0, 0.3, 120.0, 60.0, 0.0, 6.28318, "positive", 1.5);
	///       e.FitEllipseContourXld("fitzgibbon", -1, 0.0, 0, 200, 3, 2.0,
	///           out double row, out double col, out double phi,
	///           out double r1, out double r2, out double sp, out double ep, out string order);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>本实例只读（<c>GC.KeepAlive(this)</c>）；示例拟合自身生成的闭合椭圆弧，中心/半轴应能还原输入参数，可当自检用。</para>
	/// </remarks>
	public void FitEllipseContourXld(string algorithm, int maxNumPoints, double maxClosureDist, int clippingEndPoints, int vossTabSize, int iterations, double clippingFactor, out double row, out double column, out double phi, out double radius1, out double radius2, out double startPhi, out double endPhi, out string pointOrder)
	{
		IntPtr proc = JlNativeApi.PreCall(33);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, algorithm);
		JlNativeApi.StoreI(proc, 1, maxNumPoints);
		JlNativeApi.StoreD(proc, 2, maxClosureDist);
		JlNativeApi.StoreI(proc, 3, clippingEndPoints);
		JlNativeApi.StoreI(proc, 4, vossTabSize);
		JlNativeApi.StoreI(proc, 5, iterations);
		JlNativeApi.StoreD(proc, 6, clippingFactor);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out row);
		err = JlNativeApi.LoadD(proc, 1, err, out column);
		err = JlNativeApi.LoadD(proc, 2, err, out phi);
		err = JlNativeApi.LoadD(proc, 3, err, out radius1);
		err = JlNativeApi.LoadD(proc, 4, err, out radius2);
		err = JlNativeApi.LoadD(proc, 5, err, out startPhi);
		err = JlNativeApi.LoadD(proc, 6, err, out endPhi);
		err = JlNativeApi.LoadS(proc, 7, err, out pointOrder);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	/// 按整圆或圆弧批量生成本实例的轮廓内容（元组版：每个元素生成一条圆/弧）。
	/// </summary>
	/// <param name="row">各圆中心的行坐标（像素）。Default: 200.0</param>
	/// <param name="column">各圆中心的列坐标（像素）。Default: 200.0</param>
	/// <param name="radius">各圆半径（像素）。Default: 100.0</param>
	/// <param name="startPhi">各弧段起始角，弧度。Default: 0.0</param>
	/// <param name="endPhi">各弧段终止角，弧度。Default: 6.28318</param>
	/// <param name="pointOrder">各弧的点序方向（"positive"/"negative"）。Default: "positive"</param>
	/// <param name="resolution">相邻轮廓点的间距（像素），标量、对所有圆共用。Default: 1.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 34：方法入口先 <c>Dispose()</c> 再 <c>Load(proc,1)</c> 写回——这是原地生成：本实例原有轮廓全部丢弃、被新圆集替换，不返回新句柄。6 个批量参数走 <c>Store</c>（钉固定元组）+ 调用后 <c>UnpinTuple</c>；<c>resolution</c> 走 <c>StoreD</c>。</para>
	///   <para><b>约束或前提</b><c>row</c>~<c>pointOrder</c> 各元组长度应相等（逐元素配对成圆），不等长时的补齐/报错行为 [待实测]。角度为弧度；<c>endPhi</c>-<c>startPhi</c> 覆盖 2π 即整圆；起始角自 0 弧度方向起算，"positive" 在图像坐标系（row 向下）里的视觉旋转方向 [待实测]。整圆时首尾点是否重复计入 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>单条圆用标量重载（无钉固定开销）；要椭圆/椭圆弧用 <c>GenEllipseContourXld</c>；此算子产出均匀采样点列，若需要少而准的多边形近似顶点，画完再走 <c>GenPolygonsXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlXLDCont rings = new JlXLDCont())
	///   {
	///       JlTuple rows = new double[] { 100.0, 300.0 };
	///       JlTuple cols = new double[] { 100.0, 300.0 };
	///       JlTuple radii = new double[] { 40.0, 60.0 };
	///       JlTuple starts = new double[] { 0.0, 1.570796 };
	///       JlTuple ends = new double[] { 6.28318, 3.14159 };
	///       JlTuple orders = new string[] { "positive", "negative" };
	///       rings.GenCircleContourXld(rows, cols, radii, starts, ends, orders, 1.0);
	///       int n = rings.CountObj();   // 2 条圆/弧
	///   }
	///   </code>
	///   <para><b>资源与坑</b>本实例被覆盖但仍是同一有效句柄，继续用 <c>using</c> 管理即可；传入的 JlTuple 由内部 Unpin，调用方不需处理（纯数值/字符串元组 Dispose 亦非必需）。</para>
	/// </remarks>
	public void GenCircleContourXld(JlTuple row, JlTuple column, JlTuple radius, JlTuple startPhi, JlTuple endPhi, JlTuple pointOrder, double resolution)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(34);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, radius);
		JlNativeApi.Store(proc, 3, startPhi);
		JlNativeApi.Store(proc, 4, endPhi);
		JlNativeApi.Store(proc, 5, pointOrder);
		JlNativeApi.StoreD(proc, 6, resolution);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(radius);
		JlNativeApi.UnpinTuple(startPhi);
		JlNativeApi.UnpinTuple(endPhi);
		JlNativeApi.UnpinTuple(pointOrder);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	/// 生成本实例的圆/圆弧轮廓内容（标量版：一次一条，参数直写无钉固定开销）。
	/// </summary>
	/// <param name="row">圆中心的行坐标（像素）。Default: 200.0</param>
	/// <param name="column">圆中心的列坐标（像素）。Default: 200.0</param>
	/// <param name="radius">半径（像素）。Default: 100.0</param>
	/// <param name="startPhi">弧段起始角，弧度。Default: 0.0</param>
	/// <param name="endPhi">弧段终止角，弧度。Default: 6.28318</param>
	/// <param name="pointOrder">点序方向（"positive"/"negative"）。Default: "positive"</param>
	/// <param name="resolution">相邻轮廓点间距（像素），越小点越密。Default: 1.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一个原生 id 34；差异在装载方式：本重载 <c>row</c>~<c>endPhi</c> 与 <c>resolution</c> 走 <c>StoreD</c>、<c>pointOrder</c> 走 <c>StoreS</c> 直写，无 <c>Store</c>+<c>UnpinTuple</c> 的钉固定开销，但一次只能生成一条圆/弧。入口同样先 <c>Dispose()</c>：本实例旧轮廓被清空，属原地生成。</para>
	///   <para><b>约束或前提</b>角度为弧度；<c>endPhi</c>-<c>startPhi</c> 等于 2π 得整圆，负值或跨越 0 的区间行为 [待实测]。注意传整数字面量（如 <c>200</c>）会被隐式提升绑到本重载，而传 <c>JlTuple</c> 变量才走批量重载。</para>
	///   <para><b>与相邻算子的取舍</b>同半径的多条同心圆/批量不同半径弧，用 <see cref="GenCircleContourXld(JlTuple, JlTuple, JlTuple, JlTuple, JlTuple, JlTuple, double)"/> 一次生成，省反复覆盖本实例的代价。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlXLDCont ring = new JlXLDCont())
	///   {
	///       ring.GenCircleContourXld(200.0, 250.0, 90.0, 0.0, 3.14159, "positive", 0.5);
	///       int n = ring.CountObj();   // 1 条半圆弧
	///   }
	///   </code>
	///   <para><b>资源与坑</b><c>resolution</c> 设得过小会生成海量采样点，后续 <c>Fit*ContourXld</c>/<c>GenPolygonsXld</c> 的耗时随点数线性上涨；<c>Dispose()</c> 在前意味着重复调用只保留最后一条结果。</para>
	/// </remarks>
	public void GenCircleContourXld(double row, double column, double radius, double startPhi, double endPhi, string pointOrder, double resolution)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(34);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, radius);
		JlNativeApi.StoreD(proc, 3, startPhi);
		JlNativeApi.StoreD(proc, 4, endPhi);
		JlNativeApi.StoreS(proc, 5, pointOrder);
		JlNativeApi.StoreD(proc, 6, resolution);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	/// 批量生成本实例的椭圆/椭圆弧轮廓内容（元组版：每个元素一条，中心可为元组）。
	/// </summary>
	/// <param name="row">各椭圆中心的行坐标（像素）。Default: 200.0</param>
	/// <param name="column">各椭圆中心的列坐标（像素）。Default: 200.0</param>
	/// <param name="phi">各长轴方向角，弧度。Default: 0.0</param>
	/// <param name="radius1">各长半轴（像素）。Default: 100.0</param>
	/// <param name="radius2">各短半轴（像素）。Default: 50.0</param>
	/// <param name="startPhi">弧段起始角，弧度；按"最小包围圆"上的角度定义而非椭圆参数角。Default: 0.0</param>
	/// <param name="endPhi">弧段终止角，弧度，同在包围圆上度量。Default: 6.28318</param>
	/// <param name="pointOrder">各弧点序方向（"positive"/"negative"）。Default: "positive"</param>
	/// <param name="resolution">相邻点的最大间距（像素），标量共用。Default: 1.5</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 35：入口 <c>Dispose()</c> 后 <c>Load(proc,1)</c> 写回，原地生成为本实例新内容（无返回句柄）。8 个批量参数 <c>Store</c>+<c>UnpinTuple</c>，<c>resolution</c> <c>StoreD</c>。</para>
	///   <para><b>约束或前提</b><c>startPhi</c>/<c>endPhi</c> 是在最小包围圆上的角度——扁椭圆的弧起止在椭圆上的实际位置与直觉的"椭圆参数角"不一致，这是本算子最易错的约定。定义上 <c>radius1</c> 为长半轴、<c>radius2</c> 为短半轴，传入 <c>radius2 &gt; radius1</c> 时 <c>phi</c> 含义如何变化 [待实测]。各元组须等长逐元素配对，不等长行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要圆用 <c>GenCircleContourXld</c>（少一个半轴/角度参数）；从已有轮廓"求"椭圆用 <c>FitEllipseContourXld</c>，本算子是"画"椭圆。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlXLDCont ell = new JlXLDCont())
	///   {
	///       JlTuple rows = new double[] { 150.0, 350.0 };
	///       JlTuple cols = new double[] { 200.0, 200.0 };
	///       JlTuple phis = new double[] { 0.0, 0.785398 };
	///       JlTuple r1s = new double[] { 100.0, 120.0 };
	///       JlTuple r2s = new double[] { 50.0, 40.0 };
	///       JlTuple starts = new double[] { 0.0, 0.0 };
	///       JlTuple ends = new double[] { 6.28318, 3.14159 };
	///       JlTuple orders = new string[] { "positive", "positive" };
	///       ell.GenEllipseContourXld(rows, cols, phis, r1s, r2s, starts, ends, orders, 1.5);
	///       int n = ell.CountObj();   // 2 条：整椭圆 + 半弧
	///   }
	///   </code>
	///   <para><b>资源与坑</b>重复调用会清掉上一次的生成结果（先 <c>Dispose()</c>）；需要同时保留多组时改用元组批量传入，或先把结果并入其他轮廓集。</para>
	/// </remarks>
	public void GenEllipseContourXld(JlTuple row, JlTuple column, JlTuple phi, JlTuple radius1, JlTuple radius2, JlTuple startPhi, JlTuple endPhi, JlTuple pointOrder, double resolution)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(35);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, phi);
		JlNativeApi.Store(proc, 3, radius1);
		JlNativeApi.Store(proc, 4, radius2);
		JlNativeApi.Store(proc, 5, startPhi);
		JlNativeApi.Store(proc, 6, endPhi);
		JlNativeApi.Store(proc, 7, pointOrder);
		JlNativeApi.StoreD(proc, 8, resolution);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(radius1);
		JlNativeApi.UnpinTuple(radius2);
		JlNativeApi.UnpinTuple(startPhi);
		JlNativeApi.UnpinTuple(endPhi);
		JlNativeApi.UnpinTuple(pointOrder);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	/// 生成本实例的一条椭圆/椭圆弧轮廓（标量版：参数直写，无钉固定开销）。
	/// </summary>
	/// <param name="row">椭圆中心的行坐标（像素）。Default: 200.0</param>
	/// <param name="column">椭圆中心的列坐标（像素）。Default: 200.0</param>
	/// <param name="phi">长轴方向角，弧度。Default: 0.0</param>
	/// <param name="radius1">长半轴（像素）。Default: 100.0</param>
	/// <param name="radius2">短半轴（像素）。Default: 50.0</param>
	/// <param name="startPhi">弧段起始角，弧度（在最小包围圆上度量）。Default: 0.0</param>
	/// <param name="endPhi">弧段终止角，弧度（在最小包围圆上度量）。Default: 6.28318</param>
	/// <param name="pointOrder">点序方向（"positive"/"negative"）。Default: "positive"</param>
	/// <param name="resolution">相邻点的最大间距（像素）。Default: 1.5</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一个原生 id 35；本重载全部数值参数走 <c>StoreD</c>、<c>pointOrder</c> 走 <c>StoreS</c>，无 <c>Store</c>+<c>UnpinTuple</c> 固定开销。入口 <c>Dispose()</c> 后 <c>Load(proc,1)</c> 写回，原地覆盖本实例内容。</para>
	///   <para><b>约束或前提</b>角度全部弧度制；<c>startPhi</c>/<c>endPhi</c> 的基准是最小包围圆而非椭圆参数角（扁椭圆上肉眼位置会偏）。<c>radius2 &gt; radius1</c> 之类的退化/倒置入参行为 [待实测]。整椭圆用 0~2π（<c>endPhi</c> 取 6.28318）。</para>
	///   <para><b>与相邻算子的取舍</b>一次画多条不同参数的椭圆用元组重载；把拟合结果可视化也应走本算子重画，而不是拿拟合参数自己拼点。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlXLDCont e = new JlXLDCont())
	///   {
	///       e.GenEllipseContourXld(200.0, 200.0, 0.0, 100.0, 50.0, 0.0, 6.28318, "positive", 1.5);
	///       int n = e.CountObj();   // 1 条整椭圆
	///   }
	///   </code>
	///   <para><b>资源与坑</b>与 <c>FitEllipseContourXld</c> 的输出一一对应（中心/phi/两半轴/起止角/走向），两者互为正逆变换，可用于验证拟合精度；调用即清空旧内容，勿在复用实例上累积。</para>
	/// </remarks>
	public void GenEllipseContourXld(double row, double column, double phi, double radius1, double radius2, double startPhi, double endPhi, string pointOrder, double resolution)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(35);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, phi);
		JlNativeApi.StoreD(proc, 3, radius1);
		JlNativeApi.StoreD(proc, 4, radius2);
		JlNativeApi.StoreD(proc, 5, startPhi);
		JlNativeApi.StoreD(proc, 6, endPhi);
		JlNativeApi.StoreS(proc, 7, pointOrder);
		JlNativeApi.StoreD(proc, 8, resolution);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	/// 给轮廓点叠加均匀分布的随机扰动，返回带噪新轮廓（本实例不变）。
	/// </summary>
	/// <param name="numRegrPoints">计算局部回归线所用的邻域点数。Default: 5</param>
	/// <param name="amp">噪声最大幅值，均匀分布于 [-Amp,Amp]（像素）。Default: 1.0</param>
	/// <returns>带噪轮廓的新句柄（LoadNew 新建，需 Dispose()）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 36：本实例以控制索引 1 送入（<c>Store(proc,1)</c>），<c>numRegrPoints</c> 走 <c>StoreI</c> 索引 0、<c>amp</c> 走 <c>StoreD</c> 索引 1；输出 <c>LoadNew</c> 返回新句柄，原轮廓保持不变。扰动的施加方向（是否沿局部回归线法向）签名无从证实 [待实测]，<c>numRegrPoints</c> 即用于估这条局部方向的窗口大小。</para>
	///   <para><b>约束或前提</b>轮廓点数不变、仅坐标被扰动；<c>amp</c> 是各点独立均匀抽样，理论上单点最大偏移可达 <c>amp</c>（像素）。签名无随机种子参数，两次调用结果是否一致 [待实测]——若需可复现，需在调用方自持副本。</para>
	///   <para><b>与相邻算子的取舍</b>这是"加噪"不是"去噪"：给拟合/选择算法造鲁棒性测试数据用它；轮廓太抖想平滑，应走 <c>GenPolygonsXld</c> 近似或重采样，而不是本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlXLDCont c0 = new JlXLDCont())
	///   {
	///       c0.GenCircleContourXld(200.0, 200.0, 90.0, 0.0, 6.28318, "positive", 1.0);
	///       using (JlXLDCont noisy = c0.AddNoiseWhiteContourXld(5, 1.0))
	///       {
	///           int n = noisy.CountObj();
	///       }
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回句柄必须释放；<c>GC.KeepAlive(this)</c> 表示原生调用结束前本实例不得 Dispose，之后可自由释放。<c>amp</c> 大于边缘宽度时噪声会淹没真实形状，拟合结果随之失真。</para>
	/// </remarks>
	public JlXLDCont AddNoiseWhiteContourXld(int numRegrPoints, double amp)
	{
		IntPtr proc = JlNativeApi.PreCall(36);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numRegrPoints);
		JlNativeApi.StoreD(proc, 1, amp);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	/// 把每条轮廓用多边形逼近，阈值可按轮廓逐条给定，返回 JlXLDPoly 新句柄。
	/// </summary>
	/// <param name="type">逼近算法（其他取值集合 [待实测]）。Default: "ramer"</param>
	/// <param name="alpha">逼近阈值，逐轮廓一个值；"ramer" 下为最大偏离距离（像素）[待实测]。Default: 2.0</param>
	/// <returns>多边形逼近结果的新 JlXLDPoly 句柄（需 Dispose()）；本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 45：轮廓走 <c>Store(proc,1)</c>，<c>type</c> 走 <c>StoreS</c> 索引 0，<c>alpha</c> 走 <c>Store</c> 索引 1（钉固定元组）+ 调用后 <c>UnpinTuple</c>；输出 <c>JlXLDPoly.LoadNew</c> 新建句柄。元组长度应与轮廓条数一致 [待实测]。</para>
	///   <para><b>约束或前提</b>返回类型是 <c>JlXLDPoly</c> 而不是 JlXLDCont：它是多边形族对象，不能直接喂给只收 JlXLDCont 的下游算子（本库无 Contregion、也无两者互转的包装）。输入轮廓的点序方向影响首尾顶点，但闭合与否决定多边形是否闭合 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要一个统一阈值时用标量重载（<c>StoreD</c> 直写、无固定开销）；想把轮廓转成区域应走区域族算子而不是多边形逼近。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlImage img = new JlImage(@"C:\vision\part.pgm"))
	///   using (JlXLDCont conts = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       JlTuple alpha = new double[] { 2.0 };
	///       using (JlXLDPoly polys = conts.GenPolygonsXld("ramer", alpha))
	///       {
	///           int n = polys.CountObj();
	///       }
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回的 JlXLDPoly 必须释放；<c>alpha</c> 调大会把圆弧压成寥寥几边的"方块"，调小则顶点数接近原点数、失去逼近意义——先用标量重载试值再逐条精调。</para>
	/// </remarks>
	public JlXLDPoly GenPolygonsXld(string type, JlTuple alpha)
	{
		IntPtr proc = JlNativeApi.PreCall(45);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.Store(proc, 1, alpha);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(alpha);
		err = JlXLDPoly.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	/// 用统一阈值把所有轮廓多边形化，返回 JlXLDPoly 新句柄。
	/// </summary>
	/// <param name="type">逼近算法（其他取值集合 [待实测]）。Default: "ramer"</param>
	/// <param name="alpha">统一逼近阈值；"ramer" 下为最大偏离距离（像素）[待实测]。Default: 2.0</param>
	/// <returns>多边形结果的新 JlXLDPoly 句柄（需 Dispose()）；本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一个原生 id 45；本重载 <c>alpha</c> 走 <c>StoreD</c> 直写，无 <c>Store</c>+<c>UnpinTuple</c> 固定开销。输出同样 <c>JlXLDPoly.LoadNew</c> 新建句柄。</para>
	///   <para><b>约束或前提</b>一个阈值作用于所有轮廓：长短差异悬殊的轮廓集里，小轮廓可能被整体坍缩成退化为极少数顶点 [待实测]，这种场景改逐条给阈值。</para>
	///   <para><b>与相邻算子的取舍</b>要顶点数由自己指定的规则多边形，用 <c>GenContourPolygonXld(JlTuple, JlTuple)</c>；本算子是"自动找拐点"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlImage img = new JlImage(@"C:\vision\part.pgm"))
	///   using (JlXLDCont conts = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDPoly polys = conts.GenPolygonsXld("ramer", 2.0))
	///   {
	///       int n = polys.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需释放；传 <c>JlTuple</c> 变量才会走批量阈值重载，字面量 <c>2.0</c> 绑定到本重载。逼近是一次性输出，不会改写输入轮廓。</para>
	/// </remarks>
	public JlXLDPoly GenPolygonsXld(string type, double alpha)
	{
		IntPtr proc = JlNativeApi.PreCall(45);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDPoly.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	/// 对全部轮廓点施加 3x3 投影（单应）变换，返回变换后的新轮廓句柄。
	/// </summary>
	/// <param name="homMat2D">3x3 齐次投影变换矩阵（行优先 9 元素语义由 JlHomMat2D 封装）。</param>
	/// <returns>变换后轮廓的新句柄（LoadNew 新建，需 Dispose()）；本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 47：<c>homMat2D</c> 走 <c>Store(proc,0)</c> 钉固定 + 调用后 <c>UnpinTuple</c>，轮廓在索引 1；输出 <c>LoadNew</c> 新建句柄。点在齐次坐标下乘矩阵再除以第三个分量（w 除法），因此直线仍映为直线，但平行性与长度比例不保持。</para>
	///   <para><b>约束或前提</b><c>JlHomMat2D</c> 派生自 JlData、不实现 IDisposable——对它写 <c>Dispose()</c>/<c>using</c> 是编译错误。矩阵可用 <c>new JlHomMat2D()</c> 起步，经 <c>HomMat2dRotate</c>/<c>HomMat2dTranslate</c>/<c>HomMat2dCompose</c> 叠加。</para>
	///   <para><b>与相邻算子的取舍</b>纯旋转/平移/缩放用 <see cref="AffineTransContourXld(JlHomMat2D)"/>（2x3、语义受控不易出 w 问题）；本算子只在真有透视/镜头倾斜需求时用，且矩阵奇异或点落在消隐线附近会产生爆点/NaN 级坐标 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlImage img = new JlImage(@"C:\vision\part.pgm"))
	///   using (JlXLDCont conts = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       JlHomMat2D h = new JlHomMat2D().HomMat2dRotate(0.3, 200.0, 200.0);
	///       using (JlXLDCont warped = conts.ProjectiveTransContourXld(h))
	///       {
	///           int n = warped.CountObj();
	///       }
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；矩阵在原生调用期间不得失效（本重载经固定/Unpin 管理）；轮廓属性（曲率等）变换后是否保留 [待实测]，对结果再拟合前建议重算属性。</para>
	/// </remarks>
	public JlXLDCont ProjectiveTransContourXld(JlHomMat2D homMat2D)
	{
		IntPtr proc = JlNativeApi.PreCall(47);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, homMat2D);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	/// 对全部轮廓点施加任意 2D 仿射变换，返回变换后的新轮廓句柄（本实例不变）。
	/// </summary>
	/// <param name="homMat2D">输入变换矩阵（旋转/平移/缩放/错切组合，弧度角经 HomMat2dRotate 等构建）。</param>
	/// <returns>变换后轮廓的新句柄（LoadNew 新建，需 Dispose()）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 49：<c>homMat2D</c> 走 <c>Store(proc,0)</c> 钉固定 + 调用后 <c>UnpinTuple</c>（矩阵到 JlTuple 的隐式转换），轮廓在索引 1；输出 <c>LoadNew</c> 新建句柄。仿射保持直线性、平行性与长度比例，角度只在纯旋转时保持。</para>
	///   <para><b>约束或前提</b>矩阵用 <c>new JlHomMat2D()</c>（单位阵）起步叠加 <c>HomMat2dRotate(phi,px,py)</c>/<c>HomMat2dTranslate(tx,ty)</c> 得到，<c>phi</c> 弧度。注意这些方法返回的都是新矩阵对象，且 <c>JlHomMat2D</c> 不实现 IDisposable，不要写 <c>using</c>。</para>
	///   <para><b>顺序/状态依赖</b>变换只动点坐标：此前用 <c>Fit*ContourXld</c> 得到的椭圆/圆参数不会跟着变，必须对结果重新拟合；非均匀缩放会使曲率类属性失真 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要透视效果用 <see cref="ProjectiveTransContourXld(JlHomMat2D)"/>；只想按像素平移，本算子仍要建矩阵，简单场景可考虑点级平移（生成新 JlXLDCont）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlImage img = new JlImage(@"C:\vision\part.pgm"))
	///   using (JlXLDCont conts = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       JlHomMat2D m = new JlHomMat2D().HomMat2dRotate(1.570796, 0.0, 0.0).HomMat2dTranslate(50.0, 10.0);
	///       using (JlXLDCont moved = conts.AffineTransContourXld(m))
	///       {
	///           int n = moved.CountObj();
	///       }
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；变换后轮廓可能整体移出图像域，下游 <c>ClipContoursXld</c> 会把它裁空，检查 <c>CountObj()</c> 与坐标范围。</para>
	/// </remarks>
	public JlXLDCont AffineTransContourXld(JlHomMat2D homMat2D)
	{
		IntPtr proc = JlNativeApi.PreCall(49);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, homMat2D);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	/// 把每条开轮廓的首尾点连接、闭合成新轮廓集返回（本实例的开/闭状态不变）。
	/// </summary>
	/// <returns>闭合后轮廓的新句柄（LoadNew 新建，需 Dispose()）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 50：轮廓走 <c>Store(proc,1)</c>，输出 <c>LoadNew</c> 新建句柄。闭合是"复制并连接首尾"，不会把本实例自身的轮廓改成闭合——需要闭合语义时必须使用返回结果。</para>
	///   <para><b>约束或前提</b>对本来就闭合的轮廓是否原样返回 [待实测]；单点轮廓、零长度轮廓闭合后仍是退化对象。闭合段是直线段还是补弧 [待实测]，端点缺口大时两种结果差异明显，先目测或 <c>LengthXld()</c> 对比长度再依赖。</para>
	///   <para><b>顺序/状态依赖</b>下游 <c>FitCircleContourXld</c>/<c>FitEllipseContourXld</c> 的 <c>maxClosureDist</c> 判"闭合"、交并差族（<c>Union2ClosedContoursXld</c> 等）要求输入已闭合——这些场景应在本算子之后调用；点序方向决定"首"与"尾"，但对闭合结果只是起算点不同。</para>
	///   <para><b>与相邻算子的取舍</b>要"缺口小于阈值才闭合"的保守行为，可先用 <c>SelectContoursXld</c> 配合首尾距过滤再闭合 [待实测：本库选择特征名]；无条件硬闭合成环就用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlXLDCont arc = new JlXLDCont())
	///   {
	///       arc.GenCircleContourXld(200.0, 200.0, 90.0, 0.5, 4.0, "positive", 1.0);
	///       using (JlXLDCont closed = arc.CloseContoursXld())
	///       {
	///           int n = closed.CountObj();   // 仍为 1 条，但已闭合
	///       }
	///   }
	///   </code>
	///   <para><b>资源与坑</b>轮廓条数与输入一致（逐条闭合，不拆分不合并）；返回句柄需释放，输入 <c>arc</c> 在调用返回前不可 Dispose（<c>GC.KeepAlive</c>）。</para>
	/// </remarks>
	public JlXLDCont CloseContoursXld()
	{
		IntPtr proc = JlNativeApi.PreCall(50);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	/// 从每条轮廓两端各裁掉一段（按点数或按弧长），裁剪量可逐轮廓给定，返回新句柄。
	/// </summary>
	/// <param name="mode">裁剪方式："num_points" 按点数、"length" 按像素弧长。Default: "num_points"</param>
	/// <param name="length">逐轮廓的裁剪量：点数（mode="num_points"）或像素长度（mode="length"）。Default: 3</param>
	/// <returns>裁剪后轮廓的新句柄（LoadNew 新建，需 Dispose()）；本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 51：轮廓走 <c>Store(proc,1)</c>，<c>mode</c> 走 <c>StoreS</c> 索引 0，<c>length</c> 走 <c>Store</c> 索引 1（钉固定）+ 调用后 <c>UnpinTuple</c>；输出新句柄。元组长度应与轮廓条数配对，不等长行为 [待实测]。</para>
	///   <para><b>约束或前提</b>裁掉的量 ≥ 轮廓一半（总长或总点数）时该轮廓被裁空/裁剩单点 [待实测]——常见于短毛刺轮廓，先 <c>SelectContoursXld</c> 按长度过滤再裁更安全。闭合轮廓被裁端点时的语义（先开环再裁？）[待实测]。</para>
	///   <para><b>顺序/状态依赖</b>"端点"以点序的首尾为准，上游算子若反转过点序，裁剪位置随之换头；裁后点数变少，之前算好的拟合/长度属性作废需重算。</para>
	///   <para><b>与相邻算子的取舍</b>按矩形区域裁用 <see cref="ClipContoursXld(int,int,int,int)"/>；按任意多边形窗口裁用 <c>CropContoursXld</c>。本算子只管"掐头去尾"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple r = new double[] { 10.0, 20.0, 30.0, 40.0, 50.0, 60.0, 70.0 };
	///   JlTuple c = new double[] { 10.0, 12.0, 14.0, 16.0, 18.0, 20.0, 22.0 };
	///   using (JlXLDCont line = new JlXLDCont(r, c))
	///   {
	///       JlTuple cut = 2;   // 显式 JlTuple，绑到本重载
	///       using (JlXLDCont clipped = line.ClipEndPointsContoursXld("num_points", cut))
	///       {
	///           int n = clipped.CountObj();
	///       }
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；字面量 <c>3</c>/<c>2</c> 若不加类型会绑到标量重载——想批量逐条裁剪必须声明成 <c>JlTuple</c> 变量。</para>
	/// </remarks>
	public JlXLDCont ClipEndPointsContoursXld(string mode, JlTuple length)
	{
		IntPtr proc = JlNativeApi.PreCall(51);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.Store(proc, 1, length);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(length);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	/// 从每条轮廓两端各裁掉同一裁剪量（按点数或按弧长），返回新句柄。
	/// </summary>
	/// <param name="mode">裁剪方式："num_points" 按点数、"length" 按像素弧长。Default: "num_points"</param>
	/// <param name="length">统一裁剪量：点数（mode="num_points"）或像素长度（mode="length"）。Default: 3</param>
	/// <returns>裁剪后轮廓的新句柄（LoadNew 新建，需 Dispose()）；本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一个原生 id 51；本重载 <c>length</c> 走 <c>StoreD</c> 直写，无钉固定开销，裁剪量对所有轮廓一致。</para>
	///   <para><b>约束或前提</b>统一裁剪量对长短不一的轮廓集是双刃剑：短轮廓可能被整条裁掉 [待实测]。裁掉的"端点"以点序首尾为准，上游若重排/反转过点序，被裁的是另一端。</para>
	///   <para><b>与相邻算子的取舍</b>需要逐轮廓不同裁剪量（例如按长度比例裁）时用 <see cref="ClipEndPointsContoursXld(string, JlTuple)"/>；按矩形窗口裁用 <c>ClipContoursXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple r = new double[] { 10.0, 20.0, 30.0, 40.0, 50.0 };
	///   JlTuple c = new double[] { 10.0, 12.0, 14.0, 16.0, 18.0 };
	///   using (JlXLDCont line = new JlXLDCont(r, c))
	///   using (JlXLDCont clipped = line.ClipEndPointsContoursXld("num_points", 1.0))
	///   {
	///       int n = clipped.CountObj();   // 两端各去 1 点，剩 3 点
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；剩余点数不足以拟合（圆/椭圆至少 5 自由度的量级）时下游 <c>Fit*ContourXld</c> 的退化行为 [待实测]，裁后要检查 <c>CountObj()</c> 与长度。</para>
	/// </remarks>
	public JlXLDCont ClipEndPointsContoursXld(string mode, double length)
	{
		IntPtr proc = JlNativeApi.PreCall(51);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreD(proc, 1, length);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	/// 用一个整数坐标的矩形窗口裁剪轮廓，窗口外的部分被丢弃，返回新句柄。
	/// </summary>
	/// <param name="row1">裁剪矩形左上角行坐标（整像素）。Default: 0</param>
	/// <param name="column1">裁剪矩形左上角列坐标。Default: 0</param>
	/// <param name="row2">裁剪矩形右下角行坐标。Default: 512</param>
	/// <param name="column2">裁剪矩形右下角列坐标。Default: 512</param>
	/// <returns>裁剪后轮廓的新句柄（LoadNew 新建，需 Dispose()）；本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 52：轮廓走 <c>Store(proc,1)</c>，矩形四角走 <c>StoreI</c> 索引 0~3（整数）。与区域裁剪不同，轮廓被裁后条数可能变化：一条轮廓跨出窗口边界会被拆成多段 [待实测]，完全在窗口外的轮廓整条消失。</para>
	///   <para><b>约束或前提</b>左上/右下角点约定 (row1&lt;=row2, column1&lt;=column2)，坐标反了的行为 [待实测]；参数是 int，亚像素窗口边缘请改用 <c>CropContoursXld</c>（双精度元组矩形 + 是否闭合被裁轮廓的选项）。</para>
	///   <para><b>顺序/状态依赖</b>因拆分/删除会改变轮廓条数与下标，裁剪前按 <c>SortContoursXld</c> 建立的"第 k 条"对应关系在结果集中不再成立，按号取轮廓的逻辑要在裁剪后重建。</para>
	///   <para><b>与相邻算子的取舍</b>多矩形/浮点窗口用 <c>CropContoursXld</c>；只想限制后续算子的计算范围而不是真删点，传 ROI 参数即可，不必先裁。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlImage img = new JlImage(@"C:\vision\part.pgm"))
	///   using (JlXLDCont conts = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont win = conts.ClipContoursXld(100, 100, 300, 400))
	///   {
	///       int nBefore = conts.CountObj();
	///       int nAfter = win.CountObj();   // 可能不等于 nBefore：有拆分/整条丢弃
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；被裁断的开轮廓不自动补闭合，若下游要求闭合轮廓（如交并差族），需再走 <c>CloseContoursXld</c>。</para>
	/// </remarks>
	public JlXLDCont ClipContoursXld(int row1, int column1, int row2, int column2)
	{
		IntPtr proc = JlNativeApi.PreCall(52);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row1);
		JlNativeApi.StoreI(proc, 1, column1);
		JlNativeApi.StoreI(proc, 2, row2);
		JlNativeApi.StoreI(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	/// 从候选轮廓中筛出沿灰度剖面看位于局部极大值上的轮廓（脊线），比例阈值可逐轮廓给定。
	/// </summary>
	/// <param name="image">与轮廓同一坐标系、同尺寸的灰度图。</param>
	/// <param name="minPercent">逐轮廓的"极大值点占比"阈值（%），元组版每条轮廓一个值。Default: 70</param>
	/// <param name="minDiff">极大值须高出剖面的最小灰度差。Default: 15</param>
	/// <param name="distance">检查极大值所用剖面的最大宽度（像素）。Default: 4</param>
	/// <returns>被选中的轮廓子集的新句柄（LoadNew 新建，需 Dispose()）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 53：原生侧控制参数序与 C# 形参序不同——<c>minPercent</c> 占索引 0、<c>minDiff</c> 索引 1、<c>distance</c> 索引 2；对象输入两个：轮廓在索引 1、<c>image</c> 在索引 2（各走 <c>Store</c> 钉固定）。<c>minPercent</c> 走 Store+UnpinTuple。</para>
	///   <para><b>约束或前提</b>轮廓点坐标必须与 <c>image</c> 像素网格对齐（同一变换域），否则读到的剖面灰度与轮廓无关，选择结果看似随机。图像通道数/位深要求 [待实测]；建议单通道字节图。</para>
	///   <para><b>与相邻算子的取舍</b>本算子是"按灰度证据筛轮廓"，不是灰度分水岭分割：想做峰值线提取可对比 <c>Threshold</c>+区域边界路线；只想按几何特征筛则用 <c>SelectContoursXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlImage img = new JlImage(@"C:\vision\part.pgm"))
	///   using (JlXLDCont cand = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       JlTuple pct = new double[] { 70.0 };
	///       using (JlXLDCont ridges = cand.LocalMaxContoursXld(img, pct, 15, 4))
	///       {
	///           int n = ridges.CountObj();
	///       }
	///   }
	///   </code>
	///   <para><b>资源与坑</b><c>GC.KeepAlive(image)</c> 与 <c>GC.KeepAlive(this)</c> 表明两路输入在原生调用结束前都不得释放——常见错误是把 <c>img</c> 提前 Dispose 或移出作用域。返回句柄需释放。</para>
	/// </remarks>
	public JlXLDCont LocalMaxContoursXld(JlImage image, JlTuple minPercent, int minDiff, int distance)
	{
		IntPtr proc = JlNativeApi.PreCall(53);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.Store(proc, 0, minPercent);
		JlNativeApi.StoreI(proc, 1, minDiff);
		JlNativeApi.StoreI(proc, 2, distance);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minPercent);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>
	/// 从候选轮廓中筛出位于灰度局部极大值上的轮廓（脊线），使用统一的比例阈值。
	/// </summary>
	/// <param name="image">与轮廓同一坐标系、同尺寸的灰度图。</param>
	/// <param name="minPercent">统一作用于所有轮廓的"极大值点占比"阈值（%）。Default: 70</param>
	/// <param name="minDiff">极大值须高出剖面的最小灰度差。Default: 15</param>
	/// <param name="distance">检查极大值所用剖面的最大宽度（像素）。Default: 4</param>
	/// <returns>被选中的轮廓子集的新句柄（LoadNew 新建，需 Dispose()）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一个原生 id 53；本重载 <c>minPercent</c> 走 <c>StoreI</c> 直写（无钉固定），<c>minDiff</c>/<c>distance</c> 亦 <c>StoreI</c>，但 <c>image</c> 仍走 <c>Store</c> 钉固定（索引 2）、轮廓索引 1。</para>
	///   <para><b>约束或前提</b>阈值为百分比（0~100），传 70 表示要求轮廓上至少七成点处于剖面局部极大 [待实测：语义细节以原生为准]；轮廓与图像坐标不对齐时结果无意义。</para>
	///   <para><b>与相邻算子的取舍</b>多数轮廓强弱不一（前景清晰+背景噪声线混杂）时，逐轮廓给 <c>minPercent</c> 的元组重载更好；一刀切筛线用本重载即可。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlImage img = new JlImage(@"C:\vision\part.pgm"))
	///   using (JlXLDCont cand = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont ridges = cand.LocalMaxContoursXld(img, 70, 15, 4))
	///   {
	///       int n = ridges.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>整数字面量 <c>70</c> 绑到本重载、<c>JlTuple</c> 变量才绑批量重载；<c>img</c> 在调用结束前不得 Dispose（<c>GC.KeepAlive(image)</c>）；返回值与 <c>cand</c> 均需在用完后释放。</para>
	/// </remarks>
	public JlXLDCont LocalMaxContoursXld(JlImage image, int minPercent, int minDiff, int distance)
	{
		IntPtr proc = JlNativeApi.PreCall(53);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.StoreI(proc, 0, minPercent);
		JlNativeApi.StoreI(proc, 1, minDiff);
		JlNativeApi.StoreI(proc, 2, distance);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>
	/// 按"到参考直线的距离直方图"合并相邻的直线轮廓，同时输出合并结果、选中轮廓与直方图计数。
	/// </summary>
	/// <param name="selectedContours">输出：被选中的输入轮廓（新句柄，需 Dispose()）[待实测：与返回值的分工]。</param>
	/// <param name="refLineStartRow">参考直线起点行坐标。Default: 0</param>
	/// <param name="refLineStartColumn">参考直线起点列坐标。Default: 0</param>
	/// <param name="refLineEndRow">参考直线终点行坐标。Default: 0</param>
	/// <param name="refLineEndColumn">参考直线终点列坐标。Default: 0</param>
	/// <param name="width">最大距离（像素）。Default: 1</param>
	/// <param name="maxWidth">两个极小值之间的最大宽度。Default: 1</param>
	/// <param name="filterSize">平滑滤波器的尺寸。Default: 1</param>
	/// <param name="histoValues">距离直方图的输出值（INTEGER 新元组，需 Dispose()）。</param>
	/// <returns>输出 XLD 轮廓的新句柄（LoadNew 新建，需 Dispose()）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 54：轮廓走 <c>Store(proc,1)</c>，7 个几何/滤波参数全部走 <c>StoreI</c> 索引 0~6（注意是整数装载，无亚像素参考线）。三路输出中轮廓走 OCT 1、2，直方图走 OCT 0 且按 <c>INTEGER</c> 装载——计数是整型，不要按浮点频数理解。</para>
	///   <para><b>约束或前提</b>默认参考线起止点同为 (0,0)，是退化直线：必须先给出真正的起止坐标才有意义 [待实测：退化输入是报错还是全不选]。<c>width</c>/<c>maxWidth</c>/<c>filterSize</c> 为 int，像素级取整会丢掉亚像素间距分辨力。</para>
	///   <para><b>与相邻算子的取舍</b>本算子面向"到同一条参考线距离相近"的直线合并（如共线条码边缘）；方向相近就合并用 <c>UnionStraightContoursXld</c>，共线+垂距约束用 <c>UnionCollinearContoursXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlImage img = new JlImage(@"C:\vision\part.pgm"))
	///   using (JlXLDCont conts = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       using (JlXLDCont merged = conts.UnionStraightContoursHistoXld(out var selected, 0, 100, 511, 100, 1, 1, 1, out var histo))
	///       using (selected)
	///       using (histo)
	///       {
	///           int nMerge = merged.CountObj();
	///           int nSel = selected.CountObj();
	///           int bins = histo.Length;
	///       }
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值、out 轮廓、out 元组三路都是新句柄，漏一个泄一个；合并后条数减少、下标重排，按下标配对的旧缓存全部作废。</para>
	/// </remarks>
	public JlXLDCont UnionStraightContoursHistoXld(out JlXLDCont selectedContours, int refLineStartRow, int refLineStartColumn, int refLineEndRow, int refLineEndColumn, int width, int maxWidth, int filterSize, out JlTuple histoValues)
	{
		IntPtr proc = JlNativeApi.PreCall(54);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, refLineStartRow);
		JlNativeApi.StoreI(proc, 1, refLineStartColumn);
		JlNativeApi.StoreI(proc, 2, refLineEndRow);
		JlNativeApi.StoreI(proc, 3, refLineEndColumn);
		JlNativeApi.StoreI(proc, 4, width);
		JlNativeApi.StoreI(proc, 5, maxWidth);
		JlNativeApi.StoreI(proc, 6, filterSize);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out selectedContours);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out histoValues);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	/// 合并方向相近、端点相邻的直线轮廓，迭代次数可为数值元组（字符串版见另一重载）。
	/// </summary>
	/// <param name="maxDist">端点间最大距离（像素）。Default: 5.0</param>
	/// <param name="maxDiff">方向最大差（弧度）。Default: 0.5</param>
	/// <param name="percent">两项判据的加权配比。Default: 50.0</param>
	/// <param name="mode">是否并入平行轮廓（"parallel"/"noparallel"）。Default: "noparallel"</param>
	/// <param name="iterations">合并轮数的数值元组形式（要传 "maximum" 请用字符串重载）。Default: "maximum"</param>
	/// <returns>合并后轮廓的新句柄（LoadNew 新建，需 Dispose()）；本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 55：轮廓走 <c>Store(proc,1)</c>，<c>maxDist</c>/<c>maxDiff</c>/<c>percent</c> 走 <c>StoreD</c> 索引 0~2，<c>mode</c> 走 <c>StoreS</c> 索引 3，<c>iterations</c> 走 <c>Store</c> 索引 4（钉固定）+ 调用后 <c>UnpinTuple</c>。与 <see cref="UnionStraightContoursXld(double, double, double, string, string)"/> 同一原生算子，只差 iterations 的装载方式。</para>
	///   <para><b>约束或前提</b>只对"直线段"轮廓有意义，弯轮廓请先 <c>GenPolygonsXld</c> 或分段；<c>maxDiff</c> 弧度制，按度填会放宽方向约束造成误并。数值 <c>iterations</c> 表示合并轮数，轮数不足时链状直线只并了一部分 [待实测：多轮与单轮的收敛差异]。</para>
	///   <para><b>顺序/状态依赖</b>合并是不可逆操作：条数减少、下标重排，任何按序号引用需在合并后重建；重复调用（在结果上再跑一次）配合"maximum"以外的固定轮数会得到与一次跑完不同的拓扑。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlImage img = new JlImage(@"C:\vision\part.pgm"))
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       JlTuple iters = 10;   // 数值轮数：JlTuple 字面量走本重载
	///       using (JlXLDCont joined = edges.UnionStraightContoursXld(5.0, 0.5, 50.0, "noparallel", iters))
	///       {
	///           int n = joined.CountObj();
	///       }
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；字符串字面量 <c>"maximum"</c> 会绑到字符串重载而非本重载——需要逐轮廓给迭代数时才用元组版。</para>
	/// </remarks>
	public JlXLDCont UnionStraightContoursXld(double maxDist, double maxDiff, double percent, string mode, JlTuple iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(55);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maxDist);
		JlNativeApi.StoreD(proc, 1, maxDiff);
		JlNativeApi.StoreD(proc, 2, percent);
		JlNativeApi.StoreS(proc, 3, mode);
		JlNativeApi.Store(proc, 4, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(iterations);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>专合方向相近的直线轮廓：端点最大间距 maxDist（像素）与方向最大差异 maxDiff（弧度）按 percent（0~100，越大越看重方向）加权，把首尾接近、走向一致的分段直线拼成更长直线；iterations 以字符串直写（可 "maximum"），LoadNew 返回新句柄、原轮廓不变。</summary>
	/// <param name="maxDist">轮廓端点间的最大距离。Default: 5.0</param>
	/// <param name="maxDiff">方向的最大差异。Default: 0.5</param>
	/// <param name="percent">两个筛选条件的加权因子。Default: 50.0</param>
	/// <param name="mode">是否考虑平行轮廓。Default: "noparallel"</param>
	/// <param name="iterations">迭代次数或 'maximum'。Default: "maximum"</param>
	/// <returns>输出的 XLD 轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 55（<c>iterations</c> 为字符串的便捷重载）：本实例以索引 1 送入口，<c>maxDist</c>/<c>maxDiff</c>/<c>percent</c> 走 <c>StoreD</c> 索引 0~2、<c>mode</c>/<c>iterations</c> 走 <c>StoreS</c> 索引 3~4；<c>InitOCT</c>+<c>LoadNew</c> 返回合并后的新句柄，原轮廓不变。专门合并<b>方向相近的直线轮廓</b>：把首尾接近、走向一致的分段直线拼成更长直线。</para>
	///   <para><b>与元组重载的差异</b>元组重载（<see cref="UnionStraightContoursXld(double, double, double, string, JlTuple)"/>）对 <c>iterations</c> 用 <c>Store</c>+调用后 <c>UnpinTuple</c>；本重载用 <c>StoreS</c> 直写，无固定开销。传字符串字面量 <c>"maximum"</c> 会绑定到本重载；若要逐次给不同迭代数需改用元组重载并显式 <c>new JlTuple(...)</c>。</para>
	///   <para><b>判据与单位</b><c>maxDist</c> 是端点最大间距（像素），<c>maxDiff</c> 是方向最大差异（弧度），<c>percent</c> 是这两项判据的加权配比（0~100，越大越看重方向）；<c>mode="parallel"</c> 时连并排的平行直线也合并、<c>"noparallel"</c> 不并；<c>iterations</c> 是合并轮数或 <c>"maximum"</c>（反复合并直到无可并）。</para>
	///   <para><b>与相邻算子的取舍</b>要按共线（含垂距/夹角约束）合并用 <c>UnionCollinearContoursXld</c>；只要端点挨得近就焊（不看方向）用 <c>UnionAdjacentContoursXld</c>；本算子面向"直线段"且提供平行处理与迭代。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\frame.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont joined = edges.UnionStraightContoursXld(5.0, 0.5, 50.0, "noparallel", "maximum"))
	///   {
	///       int n = joined.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；合并使条数减少、下标重排，此前按下标缓存的配对作废；<c>maxDiff</c> 按度填会放宽方向约束导致误并，须用弧度。</para>
	/// </remarks>
	public JlXLDCont UnionStraightContoursXld(double maxDist, double maxDiff, double percent, string mode, string iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(55);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maxDist);
		JlNativeApi.StoreD(proc, 1, maxDiff);
		JlNativeApi.StoreD(proc, 2, percent);
		JlNativeApi.StoreS(proc, 3, mode);
		JlNativeApi.StoreS(proc, 4, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>共线轮廓合并的加权代价扩展版：每对候选合并按沿线间距、垂距、夹角、连接处扰动的权重折算成总代价，超过 maxCosts 即拒绝（maxRegrError/weightRegr 当前实现不使用）；LoadNew 返回新句柄、原轮廓不变。</summary>
	/// <param name="maxDistAbs">轮廓端点沿参考回归线方向的最大距离。Default: 10.0</param>
	/// <param name="maxDistRel">轮廓端点沿参考回归线方向的最大距离（相对于被延长轮廓的长度）。Default: 1.0</param>
	/// <param name="maxShift">轮廓到参考回归线（即垂直于该线方向）的最大距离。Default: 2.0</param>
	/// <param name="maxAngle">两条轮廓间的最大角度差。Default: 0.1</param>
	/// <param name="maxOverlap">最大重叠范围。Default: 0.0</param>
	/// <param name="maxRegrError">结果轮廓的最大回归误差（未使用）。Default: -1.0</param>
	/// <param name="maxCosts">降低统一总代价的阈值。Default: 1.0</param>
	/// <param name="weightDist">线方向距离对总代价的影响。Default: 1.0</param>
	/// <param name="weightShift">到回归线距离对总代价的影响。Default: 1.0</param>
	/// <param name="weightAngle">角度差对总代价的影响。Default: 1.0</param>
	/// <param name="weightLink">连接段扰动（重叠与角度差）对总代价的影响。Default: 1.0</param>
	/// <param name="weightRegr">回归误差对总代价的影响（未使用）。Default: 0.0</param>
	/// <param name="mode">描述轮廓属性处理方式的模式。Default: "attr_keep"</param>
	/// <returns>输出的 XLD 轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 56：本实例以索引 1 送入口，12 个数值参数 <c>maxDistAbs</c>…<c>weightRegr</c> 依次走 <c>StoreD</c> 索引 0~11、<c>mode</c> 走 <c>StoreS</c> 索引 12；<c>InitOCT</c>+<c>LoadNew</c> 返回按代价模型合并后的新句柄，原轮廓不变。这是 <c>UnionCollinearContoursXld</c> 的"加权代价"扩展版：把每对候选合并折算成一个总代价，超过 <c>maxCosts</c> 就拒绝合并。</para>
	///   <para><b>参数取向</b><c>maxDistAbs</c>/<c>maxDistRel</c>/<c>maxShift</c>/<c>maxAngle</c> 含义同简化版（距离像素、<c>maxAngle</c> 弧度）；<c>maxOverlap</c> 限制两条轮廓允许的重叠长度；<c>maxCosts</c> 是合并的总代价阈值；<c>weightDist</c>/<c>weightShift</c>/<c>weightAngle</c>/<c>weightLink</c> 分别是"沿线间距、垂距、夹角、连接处扰动"对总代价的权重，全设 1.0 即等权。<b><c>maxRegrError</c> 与 <c>weightRegr</c> 当前实现不使用</b>（英文文档标 NOT USED），填默认值即可，改动无效果。</para>
	///   <para><b>与相邻算子的取舍</b>不关心权重、只要"共线就并"时用参数更少的 <c>UnionCollinearContoursXld</c>；完全不看方向、只看端点挨得近用 <c>UnionAdjacentContoursXld</c>；合并后按长度回筛用 <c>SelectContoursXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\frame.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont joined = edges.UnionCollinearContoursExtXld(
	///       10.0, 1.0, 2.0, 0.1, 0.0, -1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 0.0, "attr_keep"))
	///   {
	///       int n = joined.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；13 个实参个数与顺序必须与签名严格一致；<c>mode</c> 控制逐点属性 <c>"attr_keep"</c>/<c>"attr_discard"</c>，合并使条数减少、下标重排，旧按下标配对作废。</para>
	/// </remarks>
	public JlXLDCont UnionCollinearContoursExtXld(double maxDistAbs, double maxDistRel, double maxShift, double maxAngle, double maxOverlap, double maxRegrError, double maxCosts, double weightDist, double weightShift, double weightAngle, double weightLink, double weightRegr, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(56);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maxDistAbs);
		JlNativeApi.StoreD(proc, 1, maxDistRel);
		JlNativeApi.StoreD(proc, 2, maxShift);
		JlNativeApi.StoreD(proc, 3, maxAngle);
		JlNativeApi.StoreD(proc, 4, maxOverlap);
		JlNativeApi.StoreD(proc, 5, maxRegrError);
		JlNativeApi.StoreD(proc, 6, maxCosts);
		JlNativeApi.StoreD(proc, 7, weightDist);
		JlNativeApi.StoreD(proc, 8, weightShift);
		JlNativeApi.StoreD(proc, 9, weightAngle);
		JlNativeApi.StoreD(proc, 10, weightLink);
		JlNativeApi.StoreD(proc, 11, weightRegr);
		JlNativeApi.StoreS(proc, 12, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>只在近似共线时焊接：以参考轮廓的回归线为准，沿线端点间距（maxDistAbs 像素、maxDistRel 相对参考轮廓长的比值）、偏离回归线的垂距（maxShift，像素）、回归线夹角（maxAngle，弧度）任一超限即不合并；LoadNew 返回新句柄、原轮廓不变。</summary>
	/// <param name="maxDistAbs">两条轮廓间隙的最大长度（沿参考轮廓的回归线度量）。Default: 10.0</param>
	/// <param name="maxDistRel">两条轮廓间隙的最大长度（相对于参考轮廓的长度，均沿参考轮廓的回归线度量）。Default: 1.0</param>
	/// <param name="maxShift">第二条轮廓到参考轮廓回归线的最大距离。Default: 2.0</param>
	/// <param name="maxAngle">两条轮廓回归线间的最大夹角。Default: 0.1</param>
	/// <param name="mode">定义轮廓属性处理方式的模式，即保留还是丢弃轮廓属性。Default: "attr_keep"</param>
	/// <returns>输出的 XLD 轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 57：本实例以索引 1 送入口、四个阈值 <c>maxDistAbs</c>/<c>maxDistRel</c>/<c>maxShift</c>/<c>maxAngle</c> 依次走 <c>StoreD</c> 索引 0~3、<c>mode</c> 走 <c>StoreS</c> 索引 4；<c>InitOCT</c>+<c>LoadNew</c> 返回合并后的新句柄，原轮廓不变。只在"近似共线"时焊接：以参考轮廓的回归线为准，把落在其延长线附近且方向一致的轮廓连起来。</para>
	///   <para><b>判据与单位</b>沿回归线方向的端点间距受 <c>maxDistAbs</c>（像素）与 <c>maxDistRel</c>（相对参考轮廓长度的比值）双重限制；<c>maxShift</c> 限制第二条轮廓偏离参考回归线的<b>垂距</b>（像素）；<c>maxAngle</c> 限制两条回归线之间的夹角，单位为<b>弧度</b>（本库角度统一约定）。任一超限即不合并。</para>
	///   <para><b>与相邻算子的取舍</b>与 <c>UnionAdjacentContoursXld</c> 的区别是本算子额外要求方向/偏移一致，不会把拐角两边焊到一起；需要更精细的代价函数（重叠长度、回归误差、各权重）时用 <c>UnionCollinearContoursExtXld</c>；合并过窄或过宽时事后用 <c>SelectContoursXld</c> 按长度回筛。</para>
	///   <para><b>参数取向</b><c>mode</c> 控制逐点属性是 <c>"attr_keep"</c> 保留还是 <c>"attr_discard"</c> 丢弃；合并使条数减少、下标重排，旧的按下标配对关系作废。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\frame.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont joined = edges.UnionCollinearContoursXld(10.0, 1.0, 2.0, 0.1, "attr_keep"))
	///   {
	///       int n = joined.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；<c>maxAngle</c> 若按"度"填会放宽方向约束导致误并，务必传弧度。</para>
	/// </remarks>
	public JlXLDCont UnionCollinearContoursXld(double maxDistAbs, double maxDistRel, double maxShift, double maxAngle, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(57);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maxDistAbs);
		JlNativeApi.StoreD(proc, 1, maxDistRel);
		JlNativeApi.StoreD(proc, 2, maxShift);
		JlNativeApi.StoreD(proc, 3, maxAngle);
		JlNativeApi.StoreS(proc, 4, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>只看端点距离、不看朝向的两两焊接：端点绝对间距（maxDistAbs，像素）与其相对较长轮廓长度的比值（maxDistRel）都在上限内就把首尾挨得近的轮廓连成更长轮廓，拐角两边也会被焊成折线；LoadNew 返回新句柄、原轮廓不变。</summary>
	/// <param name="maxDistAbs">轮廓端点间的最大距离。Default: 10.0</param>
	/// <param name="maxDistRel">轮廓端点间的最大距离（相对于较长轮廓的长度）。Default: 1.0</param>
	/// <param name="mode">描述轮廓属性处理方式的模式。Default: "attr_keep"</param>
	/// <returns>输出的 XLD 轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 58：本实例以索引 1 送入口、<c>maxDistAbs</c>/<c>maxDistRel</c> 走 <c>StoreD</c> 索引 0/1、<c>mode</c> 走 <c>StoreS</c> 索引 2；<c>InitOCT</c>+<c>LoadNew</c> 返回合并后的新句柄，原轮廓不变。把首尾点挨得足够近的轮廓两两焊接成更长的轮廓。</para>
	///   <para><b>与相邻算子的取舍</b>本算子<b>只看端点距离、不看朝向</b>——因此会把在拐角处相接的两条边（如矩形相邻两边）也焊成一条折线；要"方向必须接近才合并"改用 <c>UnionCollinearContoursXld</c>（带最大偏移/夹角约束）或 <c>UnionStraightContoursXld</c>；合并过头时事后用 <c>SelectContoursXld</c> 按长度筛回。判据：<c>maxDistAbs</c> 是端点间绝对间距上限（像素），<c>maxDistRel</c> 是同一间距相对较长轮廓长度的比值上限，两者都要满足才合并。</para>
	///   <para><b>参数取向</b><c>mode="attr_keep"</c> 时尽量保留合并前逐点属性，<c>"attr_discard"</c> 丢弃；两条带不同属性值的轮廓焊接时属性如何取值 [待实测]。合并会使条数减少、下标重排，此前按下标缓存的配对关系全部作废。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\frame.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont joined = edges.UnionAdjacentContoursXld(10.0, 1.0, "attr_keep"))
	///   {
	///       int n = joined.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；<c>maxDistAbs</c>/<c>maxDistRel</c> 给得过大会把本不该连的相邻碎线连成一团，建议配合 <c>CountObj()</c> 校验合并前后的条数变化。</para>
	/// </remarks>
	public JlXLDCont UnionAdjacentContoursXld(double maxDistAbs, double maxDistRel, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(58);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maxDistAbs);
		JlNativeApi.StoreD(proc, 1, maxDistRel);
		JlNativeApi.StoreS(proc, 2, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按轮廓内置的轻量特征（如 contour_length 按像素、closed、需先算过的回归派生量）以 [min1,max1]（双参数特征再交 [min2,max2]）筛出子集：命中的整条保留、不切断，未选中直接消失、下标重新紧凑；LoadNew 返回新句柄、原轮廓不变。</summary>
	/// <param name="feature">用于筛选轮廓的特征。Default: "contour_length"</param>
	/// <param name="min1">下阈值。Default: 0.5</param>
	/// <param name="max1">上阈值。Default: 200.0</param>
	/// <param name="min2">下阈值。Default: -0.5</param>
	/// <param name="max2">上阈值。Default: 0.5</param>
	/// <returns>输出的 XLD 轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 59：本实例以索引 1 送入口、<c>feature</c> 走 <c>StoreS</c> 索引 0、<c>min1</c>/<c>max1</c>/<c>min2</c>/<c>max2</c> 依次走 <c>StoreD</c> 索引 1~4；<c>InitOCT</c>+<c>LoadNew</c> 返回"满足条件的轮廓子集"新句柄，原轮廓不变。命中的轮廓<b>整条保留、不切断</b>，只是被从元组里挑出来，未选中的直接消失、下标重新紧凑。</para>
	///   <para><b>输入/参数取向</b><c>feature</c> 是轮廓自带的轻量特征名（如 <c>"contour_length"</c> 长度按像素、<c>"closed"</c> 开/闭、<c>"reg_angle"</c>/<c>"reg_mean"</c>/<c>"reg_sigma"</c> 等回归派生量，需先跑过相应拟合算子才有值 [待实测：可用特征名全集及其量纲]）。单值特征只用 <c>[min1,max1]</c>，<c>min2</c>/<c>max2</c> 被忽略；双参数特征（如角度+距离）用第二对 <c>[min2,max2]</c> 再取交集。</para>
	///   <para><b>与相邻算子的取舍</b>本算子按内置廉价特征筛"整条轮廓"，快且不改变点数；按形状量（面积、圆度等）筛用 <see cref="SelectShapeXld(string, string, double, double)"/>（开口轮廓按闭合解释）；要按坐标裁切轮廓段用 <c>CropContoursXld</c>；按任意逻辑筛选就直接遍历 <c>GetContourAttribXld</c> 自己判断。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\parts.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont longs = edges.SelectContoursXld("contour_length", 100.0, 1e9, 0.0, 0.0))
	///   {
	///       int n = longs.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；筛选后是保序子集，之前按旧下标缓存的配对关系全部错位，须在结果上重新取值。特征名拼错或未先算出该属性时的行为（返回空还是异常）[待实测]。</para>
	/// </remarks>
	public JlXLDCont SelectContoursXld(string feature, double min1, double max1, double min2, double max2)
	{
		IntPtr proc = JlNativeApi.PreCall(59);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, feature);
		JlNativeApi.StoreD(proc, 1, min1);
		JlNativeApi.StoreD(proc, 2, max1);
		JlNativeApi.StoreD(proc, 3, min2);
		JlNativeApi.StoreD(proc, 4, max2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>读回逐条轮廓已挂的回归线参数：返回值是 INTEGER 元组=各轮廓采样点数，9 个 out 的 DOUBLE 元组给回归线单位法向两分量与线偏移、首末点在回归线上的投影、点到线距离的均值与标准差（像素）；不产生新句柄，须先由 RegressContoursXld 算出并写入属性。</summary>
	/// <param name="nx">回归线法向量的 X 坐标。</param>
	/// <param name="ny">回归线法向量的 Y 坐标。</param>
	/// <param name="dist">回归线到原点的距离。</param>
	/// <param name="fpx">轮廓起点在回归线上投影点的 X 坐标。</param>
	/// <param name="fpy">轮廓起点在回归线上投影点的 Y 坐标。</param>
	/// <param name="lpx">轮廓终点在回归线上投影点的 X 坐标。</param>
	/// <param name="lpy">轮廓终点在回归线上投影点的 Y 坐标。</param>
	/// <param name="mean">轮廓点到回归线的平均距离。</param>
	/// <param name="deviation">到回归线距离的标准差。</param>
	/// <returns>轮廓点数。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 60：本实例以索引 1 送入口、无控制参数；一次回送 10 个元组（<c>InitOCT 0~9</c>）——返回值是 <c>INTEGER</c> 元组 = 逐条轮廓的<b>采样点数</b>，其余 9 个 <c>DOUBLE</c> 元组是逐条轮廓的回归线参数。第 i 个值对应第 i 条轮廓；本方法不产生新句柄，只是把已算好的参数读回来。</para>
	///   <para><b>前提</b>这些参数是"读缓存"，必须先由 <see cref="RegressContoursXld(string, int)"/> 一类算子在轮廓上算出回归并写入属性，本方法才取得到；对未经回归的轮廓调用时的行为（空元组还是异常）[待实测]。</para>
	///   <para><b>各输出含义</b><c>nx</c>/<c>ny</c> 是回归线单位法向的两分量（线的隐式形式为 <c>nx·x+ny·y+dist=0</c>），<c>dist</c> 为回归线到坐标原点的偏移；<c>fpx</c>/<c>fpy</c>、<c>lpx</c>/<c>lpy</c> 分别是轮廓起点、终点投影到回归线上得到的坐标；<c>mean</c>/<c>deviation</c> 为各轮廓点到回归线距离的均值与标准差（像素）。注意形参用 <c>x/y</c> 命名，而本库点坐标约定是 <c>row</c>=y、<c>column</c>=x，二者的对应关系 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>想直接得到"投影后的理想直线轮廓"用 <c>RegressContoursXld</c>（它给新句柄）；只有要读回已有拟合的数值参数时才用本方法；逐点偏差/切向请改走 <c>GetContourAttribXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\ruler.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont regr = edges.RegressContoursXld("no", 1))
	///   {
	///       JlTuple numPts = regr.GetRegressParamsXld(
	///           out JlTuple nx, out JlTuple ny, out JlTuple dist,
	///           out JlTuple fpx, out JlTuple fpy, out JlTuple lpx, out JlTuple lpy,
	///           out JlTuple mean, out JlTuple deviation);
	///       int m = numPts.Length;
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回的点数元组与 9 个 out 元组都是纯数值 <c>JlTuple</c>，一般无需 <c>Dispose()</c>；但示例里 <c>edges</c>/<c>regr</c> 是各自算子返回的新句柄，需逐个释放。9 个 out 实参必须逐个写 <c>out</c> 且顺序与签名严格一致，否则 CS1615。</para>
	/// </remarks>
	public JlTuple GetRegressParamsXld(out JlTuple nx, out JlTuple ny, out JlTuple dist, out JlTuple fpx, out JlTuple fpy, out JlTuple lpx, out JlTuple lpy, out JlTuple mean, out JlTuple deviation)
	{
		IntPtr proc = JlNativeApi.PreCall(60);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		JlNativeApi.InitOCT(proc, 8);
		JlNativeApi.InitOCT(proc, 9);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out nx);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out ny);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out dist);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out fpx);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out fpy);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out lpx);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.DOUBLE, err, out lpy);
		err = JlTuple.LoadNew(proc, 8, JlTupleType.DOUBLE, err, out mean);
		err = JlTuple.LoadNew(proc, 9, JlTupleType.DOUBLE, err, out deviation);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>对每条轮廓拟合一条全局回归线并把回归参数挂到结果轮廓上，供 GetRegressParamsXld 或全局属性族读取；mode 选外点处理方式、iterations 是其迭代次数；LoadNew 返回新句柄、本实例不变。</summary>
	/// <param name="mode">离群点处理方式。Default: "no"</param>
	/// <param name="iterations">离群点处理的迭代次数。Default: 1</param>
	/// <returns>得到的 XLD 轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 61：<c>mode</c>（外点处理方式）走 <c>StoreS</c> 索引 0、<c>iterations</c>（外点处理迭代次数）走 <c>StoreI</c> 索引 1；<c>LoadNew</c> 返回新句柄，本实例不变。对每条轮廓拟合全局回归线并把回归参数挂到结果上，供 <see cref="GetRegressParamsXld(out JlTuple, out JlTuple, out JlTuple, out JlTuple, out JlTuple, out JlTuple, out JlTuple, out JlTuple, out JlTuple)"/> 或全局属性族读取。</para>
	///   <para><b>与相邻算子的取舍</b>这是"一条直线拟合一条轮廓"的轻量工具；要更严格的偏差控制用 <c>FitLineContourXld</c>（带算法/裁剪端点等参数），要剔除离群段先 <c>SegmentContourAttribXld</c> 再回归；回归不做逐段线性分段，曲率大的轮廓会得到无意义的"平均线"。</para>
	///   <para><b>参数取向</b><c>mode="no"</c> 时 <c>iterations</c> 不起作用；开启外点处理后迭代次数越多剔除越激进 [待实测：可取的 mode 值集合与外点判据]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\ruler.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont regr = edges.RegressContoursXld("no", 1))
	///   {
	///       JlTuple attrs = regr.QueryContourGlobalAttribsXld();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；输出轮廓条数与输入是否一一对应 [待实测]。</para>
	/// </remarks>
	public JlXLDCont RegressContoursXld(string mode, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(61);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐轮廓点计算切向方向角，回送与 GetContourXld 的点一一对应的 DOUBLE 元组（弧度制）：lookaround 定方向差分用多少个邻点；只给元组、不产生新句柄，整条一个方向角请用 OrientationXld。</summary>
	/// <param name="angleMode">角度的返回形式。Default: "abs"</param>
	/// <param name="calcMode">角度的计算方法。Default: "range"</param>
	/// <param name="lookaround">参与计算的点数。Default: 3</param>
	/// <returns>轮廓点处的切线方向。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 62：轮廓以索引 1 送入，<c>angleMode</c>（0）/<c>calcMode</c>（1）走 <c>StoreS</c>、<c>lookaround</c>（2）走 <c>StoreI</c>；回送 <c>DOUBLE</c> 元组——逐点的切向方向角，弧度制（本库角度参数的统一约定），与 <c>GetContourXld</c> 的点一一对应。</para>
	///   <para><b>参数取向</b><c>lookaround</c> 是用多少个邻点差分方向：给大更平滑但在角点处"啃掉"真实转折；给 1~2 时噪声轮廓的角度剧烈抖动。<c>angleMode</c>/<c>calcMode</c> 除默认 "abs"/"range" 外的候选取值与语义 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要"整条轮廓的方向角"用 <c>OrientationXld</c>（单值）；本算子用于逐点曲率/方向剖面分析；开口轮廓首末端点因邻点不足给出的角度 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       JlTuple angles = edges.GetContourAngleXld("abs", "range", 3);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回普通 <c>JlTuple</c>，不实现 IDisposable；多轮廓容器下角度元组的拼接组织同 <c>GetContourXld</c> [待实测]。</para>
	/// </remarks>
	public JlTuple GetContourAngleXld(string angleMode, string calcMode, int lookaround)
	{
		IntPtr proc = JlNativeApi.PreCall(62);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, angleMode);
		JlNativeApi.StoreS(proc, 1, calcMode);
		JlNativeApi.StoreI(proc, 2, lookaround);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>逐点位移式滑动平滑：在 numRegrPoints 点的窗口内把各点投到该窗口的回归线上逐点滚动，点数、条数与走向都不变；输出新句柄（LoadNew），原轮廓不变。</summary>
	/// <param name="numRegrPoints">用于计算回归线的点数。Default: 5</param>
	/// <returns>平滑后的轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 63：<c>numRegrPoints</c> 走 <c>StoreI</c> 送索引 0，输出新句柄（原轮廓不变）。对每条轮廓做滑动回归线平滑：窗口内所有点被投到该窗口的回归线上，逐点滚动。</para>
	///   <para><b>与相邻算子的取舍</b>它是<b>逐点位移</b>式平滑，点数不变、走向不变；要减少点数（顶点抽稀）用 <c>GenPolygonsXld</c>（多边形近似本身即抽稀）；要整体去噪声大偏差用 <c>RegressContoursXld</c> 的外点处理。平滑不会修复断裂轮廓，也不会改变条数。</para>
	///   <para><b>参数取向</b><c>numRegrPoints</c> 越大越平滑但圆角变钝、小特征（凹口、针孔）被抹掉；给 2 时几乎只去单点毛刺。轮廓点数小于窗口时的行为 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\film_grain.pgm");
	///   using (JlXLDCont rough = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont smooth = rough.SmoothContoursXld(5))
	///   {
	///       JlTuple num = smooth.ContourPointNumXld();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；平滑后再拟合（圆/矩形）参数会变——对比基准要统一在平滑前或后。</para>
	/// </remarks>
	public JlXLDCont SmoothContoursXld(int numRegrPoints)
	{
		IntPtr proc = JlNativeApi.PreCall(63);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numRegrPoints);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>回送 INTEGER 元组，第 i 个值是本实例第 i 条轮廓的采样点数（是点数不是长度，条数用 CountObj），用于按条切分 GetContourXld/GetContourAttribXld 拼接的大元组；只读不筛、不产生新句柄。</summary>
	/// <returns>轮廓点数。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 64：回送 <c>JlTupleType.INTEGER</c> 元组，第 i 个值是本实例第 i 条轮廓的<b>采样点数</b>（不是长度，也不是元素数 N 条——条数用 <c>CountObj()</c>）。这是按条切分 <c>GetContourXld</c>/<c>GetContourAttribXld</c> 拼接大元组的钥匙。</para>
	///   <para><b>与相邻算子的取舍</b>按点数过滤轮廓用 <see cref="SelectContoursXld(string, double, double, double, double)"/>（feature "contour_point_num" 一类）[待实测：feature 名]；本算子只读不筛、开销最低。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       JlTuple num = edges.ContourPointNumXld();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回元组无需 <c>Dispose()</c>；任何合并/裁剪算子之后点数与条数都会变，别缓存本结果跨算子使用。</para>
	/// </remarks>
	public JlTuple ContourPointNumXld()
	{
		IntPtr proc = JlNativeApi.PreCall(64);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>回送字符串元组，列出本实例上已定义的整条轮廓级（全局）属性名，与逐点属性名是两套；只查名不取值、不产生新句柄，配合 GetContourGlobalAttribXld 先查名再取值。</summary>
	/// <returns>已定义的全局轮廓属性列表。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 65：无参数，<c>JlTuple.LoadNew</c>（不带类型约束）回送字符串元组——本实例上已定义的<b>全局</b>（整条轮廓级）属性名列表；与逐点属性列表（<see cref="QueryContourAttribsXld()"/>）是两套名字。</para>
	///   <para><b>与相邻算子的取舍</b>配合 <see cref="GetContourGlobalAttribXld(string)"/> 使用：先查名再取值，避免对未定义属性扑空 [待实测：扑空形态]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont regr = edges.RegressContoursXld("no", 1))
	///   {
	///       JlTuple names = regr.QueryContourGlobalAttribsXld();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回元组无需 <c>Dispose()</c>；多轮廓容器上"已定义"的判定是任一条还是全部 [待实测]。</para>
	/// </remarks>
	public JlTuple QueryContourGlobalAttribsXld()
	{
		IntPtr proc = JlNativeApi.PreCall(65);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>主重载：属性名以元组送入（Store+UnpinTuple），可一次批量读取多个全局（整条轮廓级）属性，输出 DOUBLE 元组；不产生新句柄，用前先拿 QueryContourGlobalAttribsXld 对名字。</summary>
	/// <param name="name">属性名。Default: "regr_norm_row"</param>
	/// <returns>属性值。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 66 主重载：<c>name</c> 以元组 <c>Store</c>+<c>UnpinTuple</c> 送索引 0，可一次查多个全局属性；输出 <c>DOUBLE</c> 元组——多属性名与逐轮廓值如何排布 [待实测]，用前先拿 <see cref="QueryContourGlobalAttribsXld()"/> 对名字。</para>
	///   <para><b>与标量重载的差异</b>单个属性名直接写字符串会绑定 <see cref="GetContourGlobalAttribXld(string)"/>；本重载的意义只在批量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont regr = edges.RegressContoursXld("no", 1))
	///   {
	///       JlTuple vals = regr.GetContourGlobalAttribXld(new JlTuple("regr_norm_row"));
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回元组无需 <c>Dispose()</c>；查未定义属性名时是报错还是回空洞 [待实测]。</para>
	/// </remarks>
	public JlTuple GetContourGlobalAttribXld(JlTuple name)
	{
		IntPtr proc = JlNativeApi.PreCall(66);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, name);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(name);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>标量便捷重载（原生 id 66）：按单个属性名读取全局属性，回送 DOUBLE 元组、第 i 个值是本实例第 i 条轮廓"整条一个值"的属性；须先由回归/拟合族算子挂过属性，不产生新句柄。</summary>
	/// <param name="name">属性名。Default: "regr_norm_row"</param>
	/// <returns>属性值。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 66（标量便捷重载）：<c>name</c> 走 <c>StoreS</c>；回送 <c>DOUBLE</c> 元组，第 i 个值是本实例第 i 条轮廓的全局属性值（"整条一个值"，区别于逐点属性）。</para>
	///   <para><b>与主重载的差异</b>一次查一个属性名；多属性批量用 <see cref="GetContourGlobalAttribXld(JlTuple)"/>（元组多值输出拼接顺序 [待实测]）。属性不存在时的行为 [待实测]。</para>
	///   <para><b>约束或前提</b>必须先由挂属性的算子（回归/拟合族）算过，例如 <c>RegressContoursXld</c> 之后才有 <c>regr_norm_row</c> 一类值；新提取的轮廓上直接查大概率扑空 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont regr = edges.RegressContoursXld("no", 1))
	///   {
	///       JlTuple norms = regr.GetContourGlobalAttribXld("regr_norm_row");
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回元组无需 <c>Dispose()</c>；可用 <see cref="QueryContourGlobalAttribsXld()"/> 先确认属性在不在。</para>
	/// </remarks>
	public JlTuple GetContourGlobalAttribXld(string name)
	{
		IntPtr proc = JlNativeApi.PreCall(66);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, name);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>回送字符串元组，列出本实例当前定义了哪些逐点属性（返回空元组意味着后续 GetContourAttribXld 全部扑空）；与全局属性名是两套、不可混用，不产生新句柄。</summary>
	/// <returns>已定义的轮廓属性列表。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 67：无参数，<c>JlTuple.LoadNew</c>（不带类型约束）回送<b>字符串元组</b>——本实例当前定义了哪些逐点属性。返回空元组意味着后续 <c>GetContourAttribXld</c> 全部会扑空 [待实测：未定义属性时 Get 侧的具体行为]。</para>
	///   <para><b>与相邻算子的取舍</b>逐点属性用本算子查名，整条轮廓意义的全局属性用 <see cref="QueryContourGlobalAttribsXld()"/>；两族名字不互通，别拿本结果去喂 <c>GetContourGlobalAttribXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont refCopy = edges.Clone())
	///   using (JlXLDCont withDist = edges.DistanceContoursXld(refCopy, "point_to_segment"))
	///   {
	///       JlTuple names = withDist.QueryContourAttribsXld();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回元组无需 <c>Dispose()</c>；多轮廓容器的属性名是并集还是交集 [待实测]。</para>
	/// </remarks>
	public JlTuple QueryContourAttribsXld()
	{
		IntPtr proc = JlNativeApi.PreCall(67);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>按属性名读取挂在每个轮廓点上的逐点属性值，回送与 GetContourXld 的点一一对应的 DOUBLE 元组（像素/弧度等量纲按属性自身定义）；只读上游算子已算好挂上的数据、不重复计算，不产生新句柄。</summary>
	/// <param name="name">属性名。Default: "angle"</param>
	/// <returns>属性值。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 68：<c>name</c> 以 <c>StoreS</c> 送索引 0，回送 <c>DOUBLE</c> 元组——该属性在<b>每个轮廓点</b>上的值（像素/弧度等按属性自身定义），与 <c>GetContourXld</c> 的点一一对应。</para>
	///   <para><b>约束或前提</b>属性是"存在才有值"：由上游算子挂上（如 <c>DistanceContoursXld</c> 的距离、拟合族的回归参数），未定义时返回什么（空元组还是报错）[待实测]——先查询 <see cref="QueryContourAttribsXld()"/> 再读取是最稳的写法。属性名大小写敏感性 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>整条轮廓一个值的"全局属性"用 <see cref="GetContourGlobalAttribXld(string)"/>；逐点角度也可直接 <see cref="GetContourAngleXld(string, string, int)"/> 现算；本算子读的是<b>已算好挂在轮廓上</b>的数据，不重复计算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       JlTuple names = edges.QueryContourAttribsXld();
	///       JlTuple angles = edges.GetContourAttribXld("angle");
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回元组无需 <c>Dispose()</c>；多轮廓容器下值元组的拼接组织同 <c>GetContourXld</c> [待实测]。</para>
	/// </remarks>
	public JlTuple GetContourAttribXld(string name)
	{
		IntPtr proc = JlNativeApi.PreCall(68);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, name);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把轮廓点的亚像素坐标导出为 out row、col 两个 DOUBLE 元组（row=y 向下、column=x 向右，像素单位），供导出坐标做自定义计算或存盘；不产生新句柄，只要点数用 ContourPointNumXld。</summary>
	/// <param name="row">轮廓点的行坐标。</param>
	/// <param name="col">轮廓点的列坐标。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 69：输出两个 <c>DOUBLE</c> 元组（<c>JlTuple.LoadNew</c>），即轮廓点亚像素坐标（row 向下、column 向右，像素单位）。多轮廓容器下两元组如何组织（按条拼接还是合并）[待实测]——拼接时须用 <see cref="ContourPointNumXld()"/> 的每条点数来切分。</para>
	///   <para><b>与相邻算子的取舍</b>只要点数用 <c>ContourPointNumXld()</c>（轻量 INTEGER）；要属性用 <c>GetContourAttribXld</c>；本算子用于导出坐标做自定义计算或存盘，注意大轮廓的全量拷贝。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       edges.GetContourXld(out JlTuple row, out JlTuple col);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>out 元组不实现 IDisposable，无需释放；点序与起点即该轮廓内部参数化，起点变化会让数值序列整体旋转。</para>
	/// </remarks>
	public void GetContourXld(out JlTuple row, out JlTuple col)
	{
		IntPtr proc = JlNativeApi.PreCall(69);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out col);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>由顶点元组与逐顶点圆角半径（radius 与顶点一一对应，0=直角）生成圆角多边形轮廓，采样步长也走元组、可逐段不同密度；先 Dispose 后 Load 原地灌入本实例、不返回新句柄。</summary>
	/// <param name="row">多边形的行坐标。Default: [20,80,80,20,20]</param>
	/// <param name="col">多边形的列坐标。Default: [20,20,80,80,20]</param>
	/// <param name="radius">各圆角的半径。Default: [20,20,20,20,20]</param>
	/// <param name="samplingInterval">采样点间距。Default: 1.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 71 主重载：<c>Dispose()</c>+<c>Load(proc,1)</c>，圆角多边形轮廓原地灌入本实例；四个参数（含 <c>samplingInterval</c>）全部以元组 <c>Store</c>+<c>UnpinTuple</c> 送索引 0~3，采样步长可逐段不同。</para>
	///   <para><b>与标量重载的差异</b>统一采样距用 <see cref="GenContourPolygonRoundedXld(JlTuple, JlTuple, JlTuple, double)"/>（<c>StoreD</c> 直写、少一次钉/解钉）；本重载的意义是给不同圆角段不同密度。</para>
	///   <para><b>约束或前提</b><c>radius</c> 与顶点一一对应（0 = 直角）；步长元组与段数的配对 [待实测]；圆角半径与边长冲突时的退化 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont rounded = new JlXLDCont();
	///   rounded.GenContourPolygonRoundedXld(new JlTuple(20.0, 80.0, 80.0, 20.0),
	///       new JlTuple(20.0, 20.0, 80.0, 80.0),
	///       new JlTuple(10.0, 10.0, 10.0, 10.0), new JlTuple(0.5));
	///   int n = rounded.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>本实例旧内容在调用瞬间释放；点数随步长减小而膨胀，注意与拟合算子的联动耗时。</para>
	/// </remarks>
	public void GenContourPolygonRoundedXld(JlTuple row, JlTuple col, JlTuple radius, JlTuple samplingInterval)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(71);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, col);
		JlNativeApi.Store(proc, 2, radius);
		JlNativeApi.Store(proc, 3, samplingInterval);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(col);
		JlNativeApi.UnpinTuple(radius);
		JlNativeApi.UnpinTuple(samplingInterval);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>圆角多边形生成的统一采样距重载（原生 id 71）：samplingInterval 走 StoreD 给全局弧长采样步长、radius 仍逐顶点给（0=该角不倒圆）；同样 Dispose+Load 原地灌入本实例、不返回新句柄。</summary>
	/// <param name="row">多边形的行坐标。Default: [20,80,80,20,20]</param>
	/// <param name="col">多边形的列坐标。Default: [20,20,80,80,20]</param>
	/// <param name="radius">各圆角的半径。Default: [20,20,20,20,20]</param>
	/// <param name="samplingInterval">采样点间距。Default: 1.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 71（统一采样距重载）：先 <c>Dispose()</c> 后 <c>Load(proc,1)</c>，圆角多边形轮廓<b>原地灌入本实例</b>；<c>row</c>/<c>col</c>/<c>radius</c> 为元组、<c>samplingInterval</c> 以 <c>StoreD</c> 给全局弧长采样步长。</para>
	///   <para><b>约束或前提</b>每个顶点各有一个 <c>radius</c>（0 = 该角不倒圆）；半径超过相邻边长一半时相邻圆角会重叠，产出形态 [待实测]。顶点数与 radius 数不等时 [待实测]。结果自动闭合与否 [待实测]——不要假设与 <c>GenContourPolygonXld</c> 的"手动闭合"规则一致。</para>
	///   <para><b>与主重载的差异</b>需要逐角不同采样密度时用 <see cref="GenContourPolygonRoundedXld(JlTuple, JlTuple, JlTuple, JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont rounded = new JlXLDCont();
	///   rounded.GenContourPolygonRoundedXld(new JlTuple(20.0, 80.0, 80.0, 20.0),
	///       new JlTuple(20.0, 20.0, 80.0, 80.0),
	///       new JlTuple(10.0, 10.0, 10.0, 10.0), 1.0);
	///   int n = rounded.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>采样步长越小点数越多，下游拟合耗时随之上升；本实例旧内容在调用瞬间被释放。</para>
	/// </remarks>
	public void GenContourPolygonRoundedXld(JlTuple row, JlTuple col, JlTuple radius, double samplingInterval)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(71);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, col);
		JlNativeApi.Store(proc, 2, radius);
		JlNativeApi.StoreD(proc, 3, samplingInterval);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(col);
		JlNativeApi.UnpinTuple(radius);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>由两个等长的 row/col 元组生成一条折线轮廓，点数=元组长度，且不自动闭合（要闭合须自己在首尾重复起点）；先 Dispose 再 Load 原地灌入本实例、不返回新句柄。</summary>
	/// <param name="row">多边形的行坐标。Default: [0,1,2,2,2]</param>
	/// <param name="col">多边形的列坐标。Default: [0,0,0,1,2]</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 72：实现体先 <c>Dispose()</c> 再 <c>Load(proc,1)</c>——生成的轮廓<b>原地灌入本实例</b>（不是返回新句柄）；<c>row</c>/<c>col</c> 以元组 <c>Store</c>+<c>UnpinTuple</c> 送索引 0/1。一次调用得到<b>一条</b>折线轮廓，点数 = 两元组长度。</para>
	///   <para><b>约束或前提</b>两元组必须等长，否则 [待实测：报错形态]。本算子<b>不自动闭合</b>：要得到闭合多边形需自己在首尾各写一遍起点；不闭合的"多边形"喂给填充/面积类后续算子会得到开口语义的结果。点序即轮廓走向（顺/逆时针影响 <c>OrientationXld</c> 与填充行为）。</para>
	///   <para><b>与相邻算子的取舍</b>要圆/椭圆/矩形等参数化轮廓用 <c>GenCircleContourXld</c>/<c>GenEllipseContourXld</c>/<c>GenRectangle2ContourXld</c>（自带等弧长采样）；要从区域取边界走 <c>JlRegion</c> 一侧；本算子专用于"手上已有一串顶点坐标"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont rect = new JlXLDCont();
	///   rect.GenContourPolygonXld(new JlTuple(10.0, 10.0, 90.0, 90.0, 10.0),
	///       new JlTuple(10.0, 90.0, 90.0, 10.0, 10.0));
	///   int n = rect.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>本实例旧内容在调用瞬间已释放——不要在共享该句柄的引用还活着时重入；本实例用完仍需 <c>Dispose()</c>。示例首尾重复起点即为手动闭合。</para>
	/// </remarks>
	public void GenContourPolygonXld(JlTuple row, JlTuple col)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(72);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, col);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(col);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>按对象元组做集合差：返回"属于本实例但不属于 objectsSub"的元素组成的新句柄（LoadNew），输出保持本实例的原有顺序。</summary>
	/// <param name="objectsSub">对象元组 2。</param>
	/// <returns>Objects 中不属于 ObjectsSub 的对象。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 558：返回"属于本实例但不属于 <c>objectsSub</c>"的元素组成的新句柄（<c>Store</c> 索引 1/2，<c>InitOCT</c>+<c>LoadNew</c>）。</para>
	///   <para><b>约束或前提</b>"属于"的判定标准是同一底层引用、还是按内容/坐标判同 [待实测]——用两条采样不同但几何相同的轮廓做差，结果可能与直觉相反；输出保持本实例的原有顺序。</para>
	///   <para><b>与相邻算子的取舍</b>按下标删用 <c>RemoveObj</c>（确定性强）；按特征筛用 <see cref="SelectContoursXld(string, double, double, double, double)"/>；只有"两个结果集做集合差"才用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   JlXLDCont all = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlXLDCont left = all.CropContoursXld(0.0, 0.0, 512.0, 256.0, "true");
	///   JlXLDCont rest = all.ObjDiff(left);
	///   int n = rest.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；<c>all</c>/<c>left</c> 是不同句柄，即使元素同源于同一次分割 [待实测：差集会否判同]。</para>
	/// </remarks>
	public JlXLDCont ObjDiff(JlXLDCont objectsSub)
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

	/// <summary>标量便捷重载（原生 id 568）：从本实例第 index 条起复制 numObj 条（-1=复制到底），LoadNew 返回独立副本的新句柄，StoreI 直写、原容器不被改动。</summary>
	/// <param name="index">待拷贝对象的起始序号。Default: 1</param>
	/// <param name="numObj">待拷贝对象数或 -1。Default: 1</param>
	/// <returns>拷贝出的对象。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>标量便捷重载，原生 id 568：<c>index</c>/<c>numObj</c> 以 <c>StoreI</c> 直写索引 0/1，从本实例第 <c>index</c> 条起复制 <c>numObj</c> 条（-1 = 复制到底），<c>LoadNew</c> 返回<b>独立副本</b>新句柄。</para>
	///   <para><b>与 Clone 的取舍</b><c>Clone()</c> 复制整个容器；本算子可只取一段。副本是深拷贝意义下"改一头不动另一头"[待实测：轮廓数据是否共享到写时复制]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       JlXLDCont tail = edges.CopyObj(3, -1);
	///       int n = tail.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；越界起点/条数过多时的截断或报错 [待实测]。方法带 <c>new</c>。</para>
	/// </remarks>
	public new JlXLDCont CopyObj(int index, int numObj)
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

	/// <summary>把 objects2 的元素整段接在本实例之后：LoadNew 返回拼接后的新句柄，两侧原样保留；输出顺序确定——先本实例全部（原序）、后 objects2 全部（原序）。</summary>
	/// <param name="objects2">对象元组 2。</param>
	/// <returns>拼接后的对象。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 569：把 <c>objects2</c> 的元素整段接在本实例之后，<c>LoadNew</c> 返回新句柄；两侧原样保留。输出顺序确定：先本实例全部（保持原序），后 <c>objects2</c> 全部（保持原序）。</para>
	///   <para><b>与相邻算子的取舍</b>要插到中间用 <c>InsertObj</c>；要替换用 <c>ReplaceObj</c>；循环里反复 <c>ConcatObj</c> 累积结果集每次都要复制容器引用，代价随已累积规模增长 [待实测]，量大时攒数组一次性拼。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   JlXLDCont a = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlXLDCont b = a.CropContoursXld(0.0, 0.0, 256.0, 256.0, "true");
	///   JlXLDCont all = a.ConcatObj(b);
	///   int n = all.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；<c>GC.KeepAlive</c> 表明两侧输入在调用结束前都不可释放。</para>
	/// </remarks>
	public JlXLDCont ConcatObj(JlXLDCont objects2)
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

	/// <summary>按下标元组批量选取：index 以元组 Store+UnpinTuple 送入，输出新句柄（LoadNew），元素顺序跟随 index 的书写顺序、可与原顺序不同。</summary>
	/// <param name="index">待选取对象的序号。Default: 1</param>
	/// <returns>选取出的对象。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 572 主重载：<c>index</c> 以元组 <c>Store</c>+<c>UnpinTuple</c> 送索引 0；输出新句柄，元素顺序跟随 <c>index</c> 的书写顺序（可与原顺序不同，甚至重复选取同一元素）[待实测：重复索引行为]。</para>
	///   <para><b>与相邻算子的取舍</b>按几何特征筛用 <see cref="SelectContoursXld(string, double, double, double, double)"/>；删而非留用 <c>RemoveObj</c>；"选第 k 条"的整数写法会绑定 <see cref="SelectObj(int)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       JlXLDCont picked = edges.SelectObj(new JlTuple(3, 1, 2));
	///       int n = picked.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；下标基数（0 或 1）[待实测]，越界行为 [待实测]。</para>
	/// </remarks>
	public new JlXLDCont SelectObj(JlTuple index)
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

	/// <summary>按下标取单条的标量便捷重载（原生 id 同为 572，StoreI 直写）：一次只选一条，返回的仍是只含 1 个元素的 JlXLDCont 新句柄，不是单条轮廓的新类型。</summary>
	/// <param name="index">待选取对象的序号。Default: 1</param>
	/// <returns>选取出的对象。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>标量便捷重载，原生 id 同为 572：按下标取元素组成新句柄（<c>StoreI</c> 直写，无 <c>UnpinTuple</c>）。</para>
	///   <para><b>与主重载的实际差异</b>一次只能选一条；批量选取用 <see cref="SelectObj(JlTuple)"/>。返回的仍是 <c>JlXLDCont</c> 容器（元素数为 1），不是单条轮廓的新类型。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       JlXLDCont first = edges.SelectObj(1);
	///       int n = first.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；下标越界行为 [待实测]。方法带 <c>new</c>，基类变量调用不走本实现。</para>
	/// </remarks>
	public new JlXLDCont SelectObj(int index)
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

	/// <summary>元组主重载（原生 id 573）：epsilon 以元组送入、可逐元素给不同容差，比较本实例与 objects2 是否相等，经 LoadI 返回 1/0 整数、不产生新句柄；几何相同但采样不同的轮廓在 epsilon=0 下会判不等。</summary>
	/// <param name="objects2">待比较的对象。</param>
	/// <param name="epsilon">两个灰度值或坐标等之间允许的最大差异。Default: 0.0</param>
	/// <returns>布尔结果值。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 573 主重载：<c>epsilon</c> 以元组 <c>Store</c>+<c>UnpinTuple</c> 送索引 0，可逐元素给不同容差 [待实测：epsilon 值数与被比较元素数的对应规则]；<c>LoadI</c> 回 1/0。</para>
	///   <para><b>与标量重载的差异</b>单容差直接写 double 会绑定 <see cref="CompareObj(JlXLDCont, double)"/>；本重载存在的意义就是多容差。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont refCopy = edges.Clone())
	///   {
	///       int same = edges.CompareObj(refCopy, new JlTuple(0.01));
	///   }
	///   </code>
	///   <para><b>资源与坑</b>无新句柄；比较坐标对采样密度敏感——两条几何相同但采样不同的轮廓在 epsilon=0 下判不等。</para>
	/// </remarks>
	public int CompareObj(JlXLDCont objects2, JlTuple epsilon)
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

	/// <summary>按下标逐元素比较两条轮廓对象的坐标（及各自属性），像素差异不超过 epsilon 判为相等，返回 1/0 布尔意义整数（标量便捷重载，不产生新句柄）。</summary>
	/// <param name="objects2">待比较的对象。</param>
	/// <param name="epsilon">两个灰度值或坐标等之间允许的最大差异。Default: 0.0</param>
	/// <returns>布尔结果值。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>标量便捷重载，原生 id 同为 573：按下标逐元素比较坐标（以及各属性），差异不超过 <c>epsilon</c> 视为相等；<c>LoadI</c> 回布尔意义整数。</para>
	///   <para><b>与主重载的实际差异</b><c>epsilon</c> 走 <c>StoreD</c> 全局单值——整个元组共用一套容差；要逐条轮廓不同容差用 <see cref="CompareObj(JlXLDCont, JlTuple)"/>。</para>
	///   <para><b>参数取向</b>坐标单位为像素：0.5 以内的抖动通常来自浮点舍入与边缘模型差异，放太宽会把真形变判成相等。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont refCopy = edges.Clone())
	///   {
	///       int same = edges.CompareObj(refCopy, 0.01);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值 1/0；两侧条数不等时 [待实测]。</para>
	/// </remarks>
	public int CompareObj(JlXLDCont objects2, double epsilon)
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

	/// <summary>按下标逐元素严格比较两个轮廓对象元组是否相等（无容差参数），返回 1（相等）或 0（不等），不产生新句柄。</summary>
	/// <param name="objects2">用于比较的对象。</param>
	/// <returns>布尔结果值。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 576：两个对象元组按下标逐元素比较，<c>LoadI</c> 回 1（相等）或 0（不等）。"相等"指同一底层引用的复制体还是坐标容差意义下相等 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要"误差多小算相等"用 <see cref="CompareObj(JlXLDCont, double)"/> 给 <c>epsilon</c>；本算子无容差参数，亚像素处理链的产物几乎不可能与参考值严格相等，别拿它做回归判定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   using (JlXLDCont refCopy = edges.Clone())
	///   {
	///       int eq = edges.TestEqualObj(refCopy);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>两侧条数不等时的返回（0 还是报错）[待实测]。</para>
	/// </remarks>
	public int TestEqualObj(JlXLDCont objects2)
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

	/// <summary>把本实例的亚像素轮廓光栅化为区域，按 mode 填充闭合轮廓内部或仅取轮廓经过的像素（细线状），返回 JlRegion 新句柄（需释放）。</summary>
	/// <param name="mode">区域的填充方式。Default: "filled"</param>
	/// <returns>创建的区域。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 582：本实例索引 1 送入口、<c>mode</c> 走 <c>StoreS</c>，<c>JlRegion.LoadNew</c> 返回区域新句柄。把亚像素轮廓"光栅化"成区域："filled" 填充闭合轮廓内部，"marker" 只取轮廓经过的像素（细线状区域）。开口轮廓在 "filled" 下按首尾相连处理 [待实测]。</para>
	///   <para><b>约束或前提</b>轮廓坐标是亚像素、区域是像素格：转换必然丢失半像素信息，转换后再 <c>AreaCenter</c> 得到的面积与轮廓意义下的面积有系统偏差 [待实测：偏差量级]。自交轮廓的填充规则（奇偶/非零）[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要逐条轮廓的面积数值不要区域时别绕这一圈（像素化徒增误差）；区域→轮廓的逆操作本库有 <c>JlRegion</c> 一侧边界提取，但那条轮廓与这里的输入轮廓不保证逐点互逆。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\coins.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       JlRegion filled = edges.GenRegionContourXld("filled");
	///       int n = filled.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回区域是新句柄需 <c>Dispose()</c>；输入有 N 条轮廓时输出区域条数与配对 [待实测：是否仍为 N 条一一映射]。</para>
	/// </remarks>
	public JlRegion GenRegionContourXld(string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(582);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>由本实例 XLD 轮廓生成支持行、列独立缩放并可旋转的形状模型用于匹配（角度弧度），元组参数可携带 "auto"，返回新的 JlShapeModel 模型句柄。</summary>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">图案的最小旋转。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleRMin">图案在行方向的最小缩放。Default: 0.9</param>
	/// <param name="scaleRMax">图案在行方向的最大缩放。Default: 1.1</param>
	/// <param name="scaleRStep">行方向的缩放步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleCMin">图案在列方向的最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">图案在列方向的最大缩放。Default: 1.1</param>
	/// <param name="scaleCStep">列方向的缩放步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">优化类型及可选的模型生成方法。Default: "auto"</param>
	/// <param name="metric">匹配度量。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <returns>模型句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 892 主重载：<c>numLevels</c>（索引 0）、<c>angleStep</c>（3）、<c>scaleRStep</c>（6）、<c>scaleCStep</c>（9）、<c>optimization</c>（10）以元组 <c>Store</c>+<c>UnpinTuple</c> 送入，可携带 "auto"；行向与列向缩放界各自独立，角度弧度；返回新 <c>JlShapeModel</c> 句柄。</para>
	///   <para><b>约束或前提</b>与非方形像素的关系：像素行/列间距不同（如部分线扫相机）时，用本算子只在其中一个方向给补偿即可，双方向都放宽等于把搜索空间平方放大。</para>
	///   <para><b>与相邻算子的取舍</b>详见标量重载 <see cref="CreateAnisoShapeModelXld(int, double, double, double, double, double, double, double, double, double, string, string, int)"/> 的取舍说明；需要逐档控制步长（元组多值）时只有本重载支持 [待实测：多值与各参数索引的广播规则]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage(@"C:\vision\template.pgm");
	///   JlXLDCont modelEdges = tmpl.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlShapeModel model = modelEdges.CreateAnisoShapeModelXld(new JlTuple("auto"), -0.39, 0.79,
	///       new JlTuple("auto"), 0.9, 1.1, new JlTuple("auto"), 0.95, 1.05, new JlTuple("auto"),
	///       new JlTuple("auto"), "ignore_local_polarity", 5);
	///   JlTuple lvls = model.GetShapeModelParams(out double a0, out double ae, out double aStep,
	///       out JlTuple sMin, out JlTuple sMax, out JlTuple sStep, out string met, out int mc);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄须 <c>Dispose()</c>；各输入轮廓/图像句柄也需释放。</para>
	/// </remarks>
	public JlShapeModel CreateAnisoShapeModelXld(JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleRMin, double scaleRMax, JlTuple scaleRStep, double scaleCMin, double scaleCMax, JlTuple scaleCStep, JlTuple optimization, string metric, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(892);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, angleStep);
		JlNativeApi.StoreD(proc, 4, scaleRMin);
		JlNativeApi.StoreD(proc, 5, scaleRMax);
		JlNativeApi.Store(proc, 6, scaleRStep);
		JlNativeApi.StoreD(proc, 7, scaleCMin);
		JlNativeApi.StoreD(proc, 8, scaleCMax);
		JlNativeApi.Store(proc, 9, scaleCStep);
		JlNativeApi.Store(proc, 10, optimization);
		JlNativeApi.StoreS(proc, 11, metric);
		JlNativeApi.StoreI(proc, 12, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(angleStep);
		JlNativeApi.UnpinTuple(scaleRStep);
		JlNativeApi.UnpinTuple(scaleCStep);
		JlNativeApi.UnpinTuple(optimization);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>由本实例轮廓生成行、列独立缩放并可旋转的形状模型用于匹配（角度弧度，1.0 为模板原尺寸），返回新 JlShapeModel 句柄；本标量重载以数值/字符串直写，无法表达默认的 "auto"。</summary>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">图案的最小旋转。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleRMin">图案在行方向的最小缩放。Default: 0.9</param>
	/// <param name="scaleRMax">图案在行方向的最大缩放。Default: 1.1</param>
	/// <param name="scaleRStep">行方向的缩放步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleCMin">图案在列方向的最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">图案在列方向的最大缩放。Default: 1.1</param>
	/// <param name="scaleCStep">列方向的缩放步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">优化类型及可选的模型生成方法。Default: "auto"</param>
	/// <param name="metric">匹配度量。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <returns>模型句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>标量便捷重载，原生 id 同为 892：旋转 + <b>行列独立缩放</b>的形状模型（<c>scaleRMin/Max</c> 行向、<c>scaleCMin/Max</c> 列向，1.0 = 模板原尺寸），角度弧度；<c>JlShapeModel.LoadNew</c> 返回新句柄。</para>
	///   <para><b>与主重载的实际差异（重载陷阱）</b><c>numLevels</c> 等五参在本重载全是数值/字符串直写（<c>StoreI</c>/<c>StoreD</c>/<c>StoreS</c>，无 <c>UnpinTuple</c>），无法表达文档默认 "auto"；要 auto 用 <see cref="CreateAnisoShapeModelXld(JlTuple, double, double, JlTuple, double, double, JlTuple, double, double, JlTuple, JlTuple, string, int)"/>。</para>
	///   <para><b>与相邻算子的取舍</b>方形像素且零件只整体放大缩小 → 用 Scaled 版（搜索空间少一维）；本算子留给非方形像素、或行列公差不同的场景；两界都给宽则耗时按两维档数乘积膨胀。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage(@"C:\vision\template.pgm");
	///   JlXLDCont modelEdges = tmpl.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlShapeModel model = modelEdges.CreateAnisoShapeModelXld(3, -0.39, 0.79, 0.05,
	///       0.9, 1.1, 0.05, 0.9, 1.1, 0.05, "auto", "ignore_local_polarity", 5);
	///   using (model)
	///   {
	///       JlImage scene = new JlImage(@"C:\vision\scene.pgm");
	///       model.FindAnisoShapeModel(scene, -0.39, 0.79, 0.9, 1.1, 0.9, 1.1, 0.5, 1, 3.0,
	///           "true", -1, 0.8,
	///           out JlTuple row, out JlTuple col, out JlTuple angle,
	///           out JlTuple scaleR, out JlTuple scaleC, out JlTuple score);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>模型与全部输入句柄须 <c>Dispose()</c>；行列缩放界相同也不比 Scaled 版更准，只是更慢。</para>
	/// </remarks>
	public JlShapeModel CreateAnisoShapeModelXld(int numLevels, double angleStart, double angleExtent, double angleStep, double scaleRMin, double scaleRMax, double scaleRStep, double scaleCMin, double scaleCMax, double scaleCStep, string optimization, string metric, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(892);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, angleStep);
		JlNativeApi.StoreD(proc, 4, scaleRMin);
		JlNativeApi.StoreD(proc, 5, scaleRMax);
		JlNativeApi.StoreD(proc, 6, scaleRStep);
		JlNativeApi.StoreD(proc, 7, scaleCMin);
		JlNativeApi.StoreD(proc, 8, scaleCMax);
		JlNativeApi.StoreD(proc, 9, scaleCStep);
		JlNativeApi.StoreS(proc, 10, optimization);
		JlNativeApi.StoreS(proc, 11, metric);
		JlNativeApi.StoreI(proc, 12, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>由本实例轮廓生成可旋转且行列等比缩放（各向同性）的形状模型用于匹配（角度弧度），元组参数可携带 "auto"，返回新 JlShapeModel 句柄。</summary>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">图案的最小旋转。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleMin">图案的最小缩放。Default: 0.9</param>
	/// <param name="scaleMax">图案的最大缩放。Default: 1.1</param>
	/// <param name="scaleStep">缩放步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">优化类型及可选的模型生成方法。Default: "auto"</param>
	/// <param name="metric">匹配度量。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <returns>模型句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 893 主重载：<c>numLevels</c>/<c>angleStep</c>/<c>scaleStep</c>/<c>optimization</c> 以元组 <c>Store</c>+<c>UnpinTuple</c> 送入，可携带 "auto"；各向同性缩放模型（行=列），角度弧度，<c>JlShapeModel.LoadNew</c> 返回新句柄。</para>
	///   <para><b>参数取向</b><c>scaleStep="auto"</c> 由缩放界与模型尺寸选步长 [待实测：auto 公式]；<c>scaleMin</c> 接近 0（如 0.1）会让小尺寸档在粗金字塔层丢失，匹配时间却线性膨胀。缩放界只覆盖零件真实公差即可。</para>
	///   <para><b>与相邻算子的取舍</b>不需要缩放直接 <see cref="CreateShapeModelXld(JlTuple, double, double, JlTuple, JlTuple, string, int)"/>（搜索空间少一维、快得多）；行/列独立缩放（非方形像素、透视近似）用 <c>CreateAnisoShapeModelXld</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage(@"C:\vision\template.pgm");
	///   JlXLDCont modelEdges = tmpl.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlShapeModel model = modelEdges.CreateScaledShapeModelXld(new JlTuple("auto"), -0.39, 0.79,
	///       new JlTuple("auto"), 0.9, 1.1, new JlTuple("auto"), new JlTuple("auto"),
	///       "ignore_local_polarity", 5);
	///   JlTuple levels = model.GetShapeModelParams(out double a0, out double ae, out double aStep,
	///       out JlTuple sMin, out JlTuple sMax, out JlTuple sStep, out string met, out int mc);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄用 <c>ClearShapeModel()</c> 或 <c>Dispose()</c> 释放；auto 解析出的实际层数可由 <c>GetShapeModelParams</c> 回读核对。</para>
	/// </remarks>
	public JlShapeModel CreateScaledShapeModelXld(JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleMin, double scaleMax, JlTuple scaleStep, JlTuple optimization, string metric, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(893);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, angleStep);
		JlNativeApi.StoreD(proc, 4, scaleMin);
		JlNativeApi.StoreD(proc, 5, scaleMax);
		JlNativeApi.Store(proc, 6, scaleStep);
		JlNativeApi.Store(proc, 7, optimization);
		JlNativeApi.StoreS(proc, 8, metric);
		JlNativeApi.StoreI(proc, 9, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(angleStep);
		JlNativeApi.UnpinTuple(scaleStep);
		JlNativeApi.UnpinTuple(optimization);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>由本实例轮廓生成旋转加行列等比缩放（各向同性）的形状模型用于匹配（角度弧度，1.0 为模板原尺寸），返回新 JlShapeModel 句柄；本标量重载数值直写，无法表达 "auto" 默认。</summary>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">图案的最小旋转。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleMin">图案的最小缩放。Default: 0.9</param>
	/// <param name="scaleMax">图案的最大缩放。Default: 1.1</param>
	/// <param name="scaleStep">缩放步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">优化类型及可选的模型生成方法。Default: "auto"</param>
	/// <param name="metric">匹配度量。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <returns>模型句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>标量便捷重载，原生 id 同为 893：由本实例轮廓生成"旋转+各向同性缩放"形状模型；<c>scaleMin/scaleMax</c> 是行、列同比例的缩放界（1.0 = 模板原尺寸），角度弧度，结果为新 <c>JlShapeModel</c> 句柄。</para>
	///   <para><b>与主重载的实际差异（重载陷阱）</b><c>numLevels</c>/<c>scaleStep</c> 在本重载是数值，无法表达默认 "auto"；要自动层数/自动缩放步长用 <see cref="CreateScaledShapeModelXld(JlTuple, double, double, JlTuple, double, double, JlTuple, JlTuple, string, int)"/> 显式 <c>new JlTuple("auto")</c>。</para>
	///   <para><b>参数取向</b>缩放档数是搜索空间乘数：angleStep 与 scaleStep 同时减半，耗时约乘 4；只放宽其中一个。给宽缩放界却用大 <c>scaleStep</c> 会整档漏检。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage(@"C:\vision\template.pgm");
	///   JlXLDCont modelEdges = tmpl.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlShapeModel model = modelEdges.CreateScaledShapeModelXld(3, -0.39, 0.79, 0.05,
	///       0.9, 1.1, 0.05, "auto", "ignore_local_polarity", 5);
	///   using (model)
	///   {
	///       JlImage scene = new JlImage(@"C:\vision\scene.pgm");
	///       model.FindScaledShapeModel(scene, -0.39, 0.79, 0.9, 1.1, 0.5, 1, 3.0, "true", -1, 0.8,
	///           out JlTuple row, out JlTuple col, out JlTuple angle, out JlTuple scale, out JlTuple score);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>模型与 <c>modelEdges</c>/<c>tmpl</c>/<c>scene</c> 均须 <c>Dispose()</c>；行列像素尺寸不等的相机请用 Aniso 版，否则缩放补偿会被错误摊到两个方向。</para>
	/// </remarks>
	public JlShapeModel CreateScaledShapeModelXld(int numLevels, double angleStart, double angleExtent, double angleStep, double scaleMin, double scaleMax, double scaleStep, string optimization, string metric, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(893);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, angleStep);
		JlNativeApi.StoreD(proc, 4, scaleMin);
		JlNativeApi.StoreD(proc, 5, scaleMax);
		JlNativeApi.StoreD(proc, 6, scaleStep);
		JlNativeApi.StoreS(proc, 7, optimization);
		JlNativeApi.StoreS(proc, 8, metric);
		JlNativeApi.StoreI(proc, 9, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>由本实例轮廓生成可旋转的形状模型用于匹配（不含缩放，角度弧度），元组参数可携带 "auto"，返回新 JlShapeModel 模型句柄。</summary>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">图案的最小旋转。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">优化类型及可选的模型生成方法。Default: "auto"</param>
	/// <param name="metric">匹配度量。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <returns>模型句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 894 主重载：<c>numLevels</c>/<c>angleStep</c>/<c>optimization</c> 以元组 <c>Store</c>+<c>UnpinTuple</c> 送入，可携带 "auto" 字符串；角度弧度；<c>JlShapeModel.LoadNew</c> 返回新模型句柄。</para>
	///   <para><b>参数取向</b><c>numLevels="auto"</c> 按模型尺寸自动选金字塔层数（层多→快但对小位移钝感）；<c>angleStep="auto"</c> 由实现选角步长 [待实测：auto 的具体公式]。<c>optimization</c> 可给 "auto"/"none"/"point_reduction_*" 一类值 [待实测：本库支持的确切取值]，选激进减点会让小零件误检率上升。</para>
	///   <para><b>约束或前提</b>轮廓应来自 <c>EdgesSubPix</c> 且与模板图同一坐标系；运行时图像对比度低于 <c>minContrast</c> 的边缘不会参与匹配。</para>
	///   <para><b>与相邻算子的取舍</b>要同时搜缩放用 <see cref="CreateScaledShapeModelXld(JlTuple, double, double, JlTuple, double, double, JlTuple, JlTuple, string, int)"/>；行列向缩放不同（非方形像素）用 <c>CreateAnisoShapeModelXld</c>；灰度渐变明显的零件宁可用 NCC 模板。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage(@"C:\vision\template.pgm");
	///   JlXLDCont modelEdges = tmpl.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlShapeModel model = modelEdges.CreateShapeModelXld(new JlTuple("auto"), -0.39, 0.79,
	///       new JlTuple("auto"), new JlTuple("auto"), "ignore_local_polarity", 5);
	///   using (model)
	///   {
	///       JlImage scene = new JlImage(@"C:\vision\scene.pgm");
	///       model.FindShapeModel(scene, -0.39, 0.79, 0.5, 1, 3.0, "true", -1, 0.8,
	///           out JlTuple row, out JlTuple col, out JlTuple angle, out JlTuple score);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>模型句柄须 <c>Dispose()</c>；建模板用的 <c>modelEdges</c> 与 <c>tmpl</c> 也需释放。后续可用 <c>GetShapeModelContours</c>/<c>ClearShapeModel</c> 检视与释放。</para>
	/// </remarks>
	public JlShapeModel CreateShapeModelXld(JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, JlTuple optimization, string metric, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(894);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, angleStep);
		JlNativeApi.Store(proc, 4, optimization);
		JlNativeApi.StoreS(proc, 5, metric);
		JlNativeApi.StoreI(proc, 6, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(angleStep);
		JlNativeApi.UnpinTuple(optimization);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>由本实例轮廓生成旋转不变（不含缩放）的形状模型用于匹配（角度弧度），返回新 JlShapeModel 句柄；本标量重载以 int/double 直写，无法表达 "auto" 默认层数与角步长。</summary>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">图案的最小旋转。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">优化类型及可选的模型生成方法。Default: "auto"</param>
	/// <param name="metric">匹配度量。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <returns>模型句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>标量便捷重载，原生 id 同为 894：由本实例轮廓生成旋转不变形状模型（不含缩放），<c>JlShapeModel.LoadNew</c> 返回新模型句柄。角度一律为弧度。</para>
	///   <para><b>与主重载的实际差异（重载陷阱）</b><c>numLevels</c> 在本重载是 <c>int</c>——文档默认 "auto" 在此无法表达，写死层数会绕过"按模型尺寸自动定层"；要 auto 必须用 <see cref="CreateShapeModelXld(JlTuple, double, double, JlTuple, JlTuple, string, int)"/> 并显式 <c>new JlTuple("auto")</c>。<c>angleStep</c> 同理（<c>StoreD</c> 数值 vs 元组 "auto"）。</para>
	///   <para><b>约束或前提</b>轮廓须来自模板图本身的 <c>EdgesSubPix</c> 输出（模型坐标即这些轮廓坐标）；<c>metric="use_polarity"</c> 时运行时灰度极性必须与建模板时一致。角度范围跨度 ≥2π 时端点会重复计角 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage(@"C:\vision\template.pgm");
	///   JlXLDCont modelEdges = tmpl.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlShapeModel model = modelEdges.CreateShapeModelXld(4, -0.39, 0.79, 0.05, "auto", "ignore_local_polarity", 5);
	///   JlImage scene = new JlImage(@"C:\vision\scene.pgm");
	///   model.FindShapeModel(scene, -0.39, 0.79, 0.5, 1, 3.0, "true", -1, 0.8,
	///       out JlTuple row, out JlTuple col, out JlTuple angle, out JlTuple score);
	///   </code>
	///   <para><b>资源与坑</b>模型句柄用完须 <c>Dispose()</c>（或 <c>ClearShapeModel</c>）；<c>modelEdges</c> 建完模型后仍需保留给 <c>GetShapeModelContours</c> 之类的对照用途与否由调用方决定，但别忘了释放。</para>
	/// </remarks>
	public JlShapeModel CreateShapeModelXld(int numLevels, double angleStart, double angleExtent, double angleStep, string optimization, string metric, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(894);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, angleStep);
		JlNativeApi.StoreS(proc, 4, optimization);
		JlNativeApi.StoreS(proc, 5, metric);
		JlNativeApi.StoreI(proc, 6, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}







	/// <summary>依据视场中规则网格轮廓拟合畸变图像到校正图像的映射（gridSpacing 为校正图中的整数网格点间距），返回映射图 JlImage 新句柄并经 out 参数另给网格轮廓 JlXLDCont 新句柄（两者均需释放）。</summary>
	/// <param name="image">输入图像。</param>
	/// <param name="meshes">输出轮廓。</param>
	/// <param name="gridSpacing">校正图像中网格点的间距。</param>
	/// <param name="rotation">作用于点网格的旋转。Default: "auto"</param>
	/// <param name="row">网格点的行坐标。</param>
	/// <param name="column">网格点的列坐标。</param>
	/// <param name="mapType">映射类型。Default: "bilinear"</param>
	/// <returns>包含映射数据的图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1107 主重载：<c>rotation</c>/<c>row</c>/<c>column</c> 以元组 <c>Store</c>+<c>UnpinTuple</c> 送控制索引 1~3；本实例（网格轮廓）放 iconic 索引 2、<c>image</c> 放索引 1。输出两个新句柄：映射图 <c>JlImage</c>（返回值）与网格轮廓 <c>JlXLDCont</c>（<c>out meshes</c>）。</para>
	///   <para><b>约束或前提</b><c>gridSpacing</c> 是校正后图像中的网格点间距（<c>StoreI</c> 整数像素）。<c>rotation</c> 传数值角（[待实测：弧度还是度]）可跳过自动估计；给字符串 "auto" 会绑定到 <see cref="GenGridRectificationMap(JlImage, out JlXLDCont, int, string, JlTuple, JlTuple, string)"/>。网格点过少或畸变过重时拟合失败形态 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>已知解析畸变模型时用 <c>camera_calibration</c> 族直接求映射；本算子适合"镜头畸变无标定、但视场里有规则点阵/网格"的免标定校正。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlXLDCont grid = new JlXLDCont();
	///   grid.GenContourPolygonXld(new JlTuple(100.0, 100.0, 400.0, 400.0),
	///       new JlTuple(100.0, 400.0, 100.0, 400.0));
	///   JlImage mapping = grid.GenGridRectificationMap(img, out JlXLDCont meshes,
	///       32, new JlTuple(0.0), new JlTuple(), new JlTuple(), "bilinear");
	///   mapping.GetImageSize(out int w, out int h);
	///   </code>
	///   <para><b>资源与坑</b>返回值与 <c>out meshes</c> 都要 <c>Dispose()</c>；示例中 <c>img</c>/<c>grid</c> 也需处置。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlImage image, out JlXLDCont meshes, int gridSpacing, JlTuple rotation, JlTuple row, JlTuple column, string mapType)
	{
		IntPtr proc = JlNativeApi.PreCall(1107);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.StoreI(proc, 0, gridSpacing);
		JlNativeApi.Store(proc, 1, rotation);
		JlNativeApi.Store(proc, 2, row);
		JlNativeApi.Store(proc, 3, column);
		JlNativeApi.StoreS(proc, 4, mapType);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rotation);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out meshes);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>依据规则网格轮廓拟合畸变到校正图像的映射（本重载 rotation 走字符串，"auto" 由数据估计网格旋转），返回映射图 JlImage 新句柄并经 out 参数给网格轮廓 JlXLDCont 新句柄（均需释放）。</summary>
	/// <param name="image">输入图像。</param>
	/// <param name="meshes">输出轮廓。</param>
	/// <param name="gridSpacing">校正图像中网格点的间距。</param>
	/// <param name="rotation">作用于点网格的旋转。Default: "auto"</param>
	/// <param name="row">网格点的行坐标。</param>
	/// <param name="column">网格点的列坐标。</param>
	/// <param name="mapType">映射类型。Default: "bilinear"</param>
	/// <returns>包含映射数据的图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1107（<c>rotation</c> 为字符串的便捷重载）：本实例应是覆盖在畸变网格图案上的轮廓——实现体把它放在 iconic 索引 2、<c>image</c> 放索引 1。由网格点拟合畸变→校正的映射：<c>LoadNew</c> 返回映射图 <c>JlImage</c>（供 image processing 类算子使用），<c>out meshes</c> 是另一条新 <c>JlXLDCont</c> 句柄。</para>
	///   <para><b>约束或前提</b><c>gridSpacing</c> 是<b>校正后图像</b>中网格点间距（整数像素，<c>StoreI</c>）；与实际零件网格不符会收敛出错误映射。<c>rotation="auto"</c> 由数据估计网格旋转，其余合法字符串 [待实测]。<c>row</c>/<c>column</c> 传空元组时是否自动取网格交点 [待实测]。</para>
	///   <para><b>与主重载的实际差异</b>主重载（<see cref="GenGridRectificationMap(JlImage, out JlXLDCont, int, JlTuple, JlTuple, JlTuple, string)"/>）<c>rotation</c> 为 <c>JlTuple</c>，可传数值角；本重载 <c>StoreS</c> 只给字符串。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlXLDCont grid = new JlXLDCont();
	///   grid.GenContourPolygonXld(new JlTuple(100.0, 100.0, 400.0, 400.0),
	///       new JlTuple(100.0, 400.0, 100.0, 400.0));
	///   JlImage mapping = grid.GenGridRectificationMap(img, out JlXLDCont meshes,
	///       32, "auto", new JlTuple(), new JlTuple(), "bilinear");
	///   mapping.GetImageSize(out int w, out int h);
	///   </code>
	///   <para><b>资源与坑</b>返回值映射图与 <c>out meshes</c> 都是新句柄，两个都要 <c>Dispose()</c>；<c>GC.KeepAlive(image)</c> 表明 <c>img</c> 在调用结束前不可释放。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlImage image, out JlXLDCont meshes, int gridSpacing, string rotation, JlTuple row, JlTuple column, string mapType)
	{
		IntPtr proc = JlNativeApi.PreCall(1107);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.StoreI(proc, 0, gridSpacing);
		JlNativeApi.StoreS(proc, 1, rotation);
		JlNativeApi.Store(proc, 2, row);
		JlNativeApi.Store(proc, 3, column);
		JlNativeApi.StoreS(proc, 4, mapType);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out meshes);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>计算本实例每条轮廓到配对目标轮廓的逐点距离（像素）并挂为轮廓属性，输出是本实例轮廓的拷贝新句柄（需释放）。</summary>
	/// <param name="contourTo">计算距离所朝向的目标轮廓。</param>
	/// <param name="mode">计算到点的距离（'point_to_point'）还是到整个线段的距离（'point_to_segment'）。Default: "point_to_point"</param>
	/// <returns>ContourFrom 的副本，距离以属性形式附加其上。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1300：本实例作"从"轮廓（索引 1）、<c>contourTo</c> 作"到"目标（索引 2），输出是<b>本实例轮廓的拷贝</b>（<c>LoadNew</c> 新句柄），拷贝上挂逐点距离属性——与 <c>DistanceCc</c> 返回标量带的本质区别：这里距离与点一一对应，可再被 <c>GetContourAttribXld</c> 按点读出 [待实测：属性名，疑为 "distance"]。</para>
	///   <para><b>约束或前提</b>"point_to_point" 只量到目标轮廓采样点；"point_to_segment" 量到目标线段（值更小、对稀疏采样更稳）。第 i 条"从"轮廓配第 i 条"到"轮廓；条数不等时 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要一个最近距离数字用 <c>DistanceCcMin</c>（开销小得多）；需要"每个点偏了多少"（偏差热图、逐点剔除）才用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\compare.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont refEdges = edges.Clone())
	///   {
	///       JlXLDCont withDist = edges.DistanceContoursXld(refEdges, "point_to_segment");
	///       int n = withDist.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；拷贝意味着输入属性会被带过来，逐点距离属性是否会覆盖同名旧属性 [待实测]。</para>
	/// </remarks>
	public JlXLDCont DistanceContoursXld(JlXLDCont contourTo, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(1300);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, contourTo);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contourTo);
		return obj;
	}

	/// <summary>按下标成对计算本实例各轮廓与 contour2 对应轮廓的最近距离（像素），返回逐条一值的 DOUBLE 意义 JlTuple，不产生新句柄；mode 以元组送入。</summary>
	/// <param name="contour2">第二条输入轮廓。</param>
	/// <param name="mode">距离计算方式。Default: "fast_point_to_segment"</param>
	/// <returns>两条轮廓间的最小距离。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1301 主重载：<c>mode</c> 以元组 <c>Store</c>+<c>UnpinTuple</c> 送索引 0，输出按 <c>JlTupleType.DOUBLE</c> 装载的最近距离元组，第 i 个值 = 本实例第 i 条轮廓对 <c>contour2</c> 第 i 条的最近距离（像素）。距离在对方<b>采样点/线段</b>上度量。</para>
	///   <para><b>与相邻算子的取舍</b>与标量重载（<see cref="DistanceCcMin(JlXLDCont, string)"/>）同源；要"最近发生在哪两个点"用 <c>DistanceCcMinPoints</c>，只要均值/最值分布用 <c>DistanceCc</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\compare.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont other = edges.CropContoursXld(new JlTuple(0.0), new JlTuple(256.0), new JlTuple(512.0), new JlTuple(512.0), "true"))
	///   {
	///       JlTuple d = edges.DistanceCcMin(other, new JlTuple("point_to_point"));
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回的 <c>JlTuple</c> 无需 <c>Dispose()</c>；<c>edges</c> 需要。两侧轮廓条数不等时的配对 [待实测]。</para>
	/// </remarks>
	public JlTuple DistanceCcMin(JlXLDCont contour2, JlTuple mode)
	{
		IntPtr proc = JlNativeApi.PreCall(1301);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, contour2);
		JlNativeApi.Store(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mode);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour2);
		return tuple;
	}

	/// <summary>计算本实例与 contour2 轮廓对的最近距离（像素），返回 double；本标量重载只装载第一个值，多轮廓对时仅得第一对，成批需元组重载，不产生新句柄。</summary>
	/// <param name="contour2">第二条输入轮廓。</param>
	/// <param name="mode">距离计算方式。Default: "fast_point_to_segment"</param>
	/// <returns>两条轮廓间的最小距离。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1301（标量便捷重载）：返回本实例第 i 条轮廓与 <c>contour2</c> 第 i 条的最近距离（像素，采样点/线段意义下）。</para>
	///   <para><b>mode 取向</b>"fast_point_to_segment" 用"点到对方线段"的近似快速算法 [待实测：与精确模式在曲率大处的偏差量级]；模式串非法时实现体不做任何校验，错误会由原生层抛出。</para>
	///   <para><b>与主重载的实际差异（真坑）</b><c>LoadD</c> 只装第一个值——两侧各有 N 条轮廓时只见第一对；成批距离用 <see cref="DistanceCcMin(JlXLDCont, JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\gap.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont other = edges.CropContoursXld(0.0, 256.0, 512.0, 512.0, "true"))
	///   {
	///       double d = edges.DistanceCcMin(other, "fast_point_to_segment");
	///   }
	///   </code>
	///   <para><b>资源与坑</b>无新句柄；<c>edges</c> 需 <c>Dispose()</c>。要最近点坐标用 <c>DistanceCcMinPoints</c>。</para>
	/// </remarks>
	public double DistanceCcMin(JlXLDCont contour2, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(1301);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, contour2);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour2);
		return doubleValue;
	}

	/// <summary>按下标成对计算本实例与 contour2 各轮廓对的最近与最远距离（采样点意义、像素单位），经 out 参数逐条给出两个 DOUBLE 意义 JlTuple 距离元组，无返回值也不产生新句柄。</summary>
	/// <param name="contour2">第二条输入轮廓。</param>
	/// <param name="mode">距离计算方式。Default: "point_to_point"</param>
	/// <param name="distanceMin">两条轮廓间的最小距离。</param>
	/// <param name="distanceMax">两条轮廓间的最大距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1302 主重载：<c>contour2</c> 以 <c>Store</c> 送索引 2、<c>mode</c> 以元组 <c>Store</c>+<c>UnpinTuple</c> 送索引 0；输出两个 <c>DOUBLE</c> 元组，第 i 个值对应第 i 对轮廓（本实例第 i 条 vs <c>contour2</c> 第 i 条），在采样点上度量，单位像素。</para>
	///   <para><b>与标量重载的差异</b>标量版 <c>LoadD</c> 只回第一对；多轮廓对必须用本重载。mode 取值语义与"配对不等长"问题见 <see cref="DistanceCc(JlXLDCont, string, out double, out double)"/>。</para>
	///   <para><b>与相邻算子的取舍</b>只要全局最近距离用 <c>DistanceCcMin</c>（更快）；要把逐点距离挂到轮廓属性上供后续着色/统计用 <see cref="DistanceContoursXld(JlXLDCont, string)"/>；本重载适合"成对轮廓的距离带"批处理。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\two_edges.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont shifted = edges.AffineTransContourXld(new JlHomMat2D()))
	///   {
	///       JlTuple mode = "point_to_point";
	///       edges.DistanceCc(shifted, mode, out JlTuple dMin, out JlTuple dMax);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>out 元组无需 <c>Dispose()</c>；<c>edges</c> 需 <c>Dispose()</c>。</para>
	/// </remarks>
	public void DistanceCc(JlXLDCont contour2, JlTuple mode, out JlTuple distanceMin, out JlTuple distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1302);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, contour2);
		JlNativeApi.Store(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mode);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out distanceMin);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour2);
	}

	/// <summary>按下标成对计算本实例与 contour2 各轮廓对的最近/最远距离（采样点意义、像素单位），经 out 标量参数只回第一对的值，其余轮廓对静默丢弃，无新句柄。</summary>
	/// <param name="contour2">第二条输入轮廓。</param>
	/// <param name="mode">距离计算方式。Default: "point_to_point"</param>
	/// <param name="distanceMin">两条轮廓间的最小距离。</param>
	/// <param name="distanceMax">两条轮廓间的最大距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1302（标量便捷重载）：按下标成对比较本实例第 i 条与 <c>contour2</c> 第 i 条轮廓，每对给出采样点意义下的最近/最远距离（像素）。</para>
	///   <para><b>mode 的含义</b>"point_to_point" 只在两侧采样点之间量；"point_to_segment" 允许量到对方线段上（结果更小、更接近连续几何距离）[待实测：本库实现体对 mode 串无校验，非法值如何反馈]。</para>
	///   <para><b>与主重载的实际差异（真坑）</b>输出用 <c>LoadD</c> 只装第一个值：轮廓对多于 1 时其余距离静默丢弃。逐对距离用 <see cref="DistanceCc(JlXLDCont, JlTuple, out JlTuple, out JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\two_edges.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont shifted = edges.AffineTransContourXld(new JlHomMat2D()))
	///   {
	///       edges.DistanceCc(shifted, "point_to_point", out double dMin, out double dMax);
	///   }
	///   </code>
	///   <para><b>资源与坑</b><c>JlHomMat2D</c> 不实现 IDisposable；示例中 <c>edges</c> 需自行 <c>Dispose()</c>。两侧条数不等时的配对 [待实测]。</para>
	/// </remarks>
	public void DistanceCc(JlXLDCont contour2, string mode, out double distanceMin, out double distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1302);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, contour2);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out distanceMin);
		err = JlNativeApi.LoadD(proc, 1, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour2);
	}

	/// <summary>以元组给出多条线段端点坐标，计算有限长线段到本实例轮廓的最近与最远距离（像素，投影落在线段外按端点计），经 out 参数逐值给出两个 DOUBLE 意义 JlTuple 元组，无新句柄。</summary>
	/// <param name="row1">线段第一个点的行坐标。</param>
	/// <param name="column1">线段第一个点的列坐标。</param>
	/// <param name="row2">线段第二个点的行坐标。</param>
	/// <param name="column2">线段第二个点的列坐标。</param>
	/// <param name="distanceMin">线段与轮廓间的最小距离。</param>
	/// <param name="distanceMax">线段与轮廓间的最大距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1303 主重载：线段两端点坐标以元组 <c>Store</c> 送索引 0~3 并逐一 <c>UnpinTuple</c>；输出两个 <c>DOUBLE</c> 元组（像素单位）。线段为有限长，投影落在线段外时按端点度量。</para>
	///   <para>元组多值（多条线段）与本实例轮廓的配对/广播规则 [待实测]；单线段用 <see cref="DistanceSc(double, double, double, double, out double, out double)"/> 省四次钉/解钉。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\rail.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       edges.DistanceSc(new JlTuple(100.0), new JlTuple(50.0),
	///           new JlTuple(100.0), new JlTuple(400.0),
	///           out JlTuple dMin, out JlTuple dMax);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>out 元组无需 <c>Dispose()</c>。</para>
	/// </remarks>
	public void DistanceSc(JlTuple row1, JlTuple column1, JlTuple row2, JlTuple column2, out JlTuple distanceMin, out JlTuple distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1303);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row1);
		JlNativeApi.Store(proc, 1, column1);
		JlNativeApi.Store(proc, 2, row2);
		JlNativeApi.Store(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(column1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(column2);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out distanceMin);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>计算由两点定义的有限长线段到本实例轮廓的最近/最远距离（像素），经 out 标量参数只回第一个值，本实例多轮廓时仅见第一条，无新句柄。</summary>
	/// <param name="row1">线段第一个点的行坐标。</param>
	/// <param name="column1">线段第一个点的列坐标。</param>
	/// <param name="row2">线段第二个点的行坐标。</param>
	/// <param name="column2">线段第二个点的列坐标。</param>
	/// <param name="distanceMin">线段与轮廓间的最小距离。</param>
	/// <param name="distanceMax">线段与轮廓间的最大距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1303（标量便捷重载）：两点定义<b>有限长线段</b>（延伸到线段外的部分不算，无限直线用 <c>DistanceLc</c>）；距离在本实例轮廓采样点上度量，单位像素。</para>
	///   <para><b>与主重载的实际差异</b>四点 <c>StoreD</c> 直写、输出 <c>LoadD</c> 只装第一个值：本实例多轮廓时只见第一条；批量线段用 <see cref="DistanceSc(JlTuple, JlTuple, JlTuple, JlTuple, out JlTuple, out JlTuple)"/>。</para>
	///   <para><b>参数取向</b>线段长度趋近 0 时退化为点到轮廓距离（≈<c>DistancePc</c>）[待实测：是否直接报错]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\rail.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       edges.DistanceSc(100.0, 50.0, 100.0, 400.0, out double dMin, out double dMax);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>无新句柄产生；轮廓整体在线段延长线方向外侧时，最近距离按端点计。</para>
	/// </remarks>
	public void DistanceSc(double row1, double column1, double row2, double column2, out double distanceMin, out double distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1303);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row1);
		JlNativeApi.StoreD(proc, 1, column1);
		JlNativeApi.StoreD(proc, 2, row2);
		JlNativeApi.StoreD(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out distanceMin);
		err = JlNativeApi.LoadD(proc, 1, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>以元组给出多条直线两端点坐标，计算过两点的无限长直线到本实例轮廓的最近/最远距离（采样点意义、像素单位），经 out 参数逐值给出两个 DOUBLE 意义 JlTuple 元组，无新句柄。</summary>
	/// <param name="row1">直线第一个点的行坐标。</param>
	/// <param name="column1">直线第一个点的列坐标。</param>
	/// <param name="row2">直线第二个点的行坐标。</param>
	/// <param name="column2">直线第二个点的列坐标。</param>
	/// <param name="distanceMin">直线与轮廓间的最小距离。</param>
	/// <param name="distanceMax">直线与轮廓间的最大距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1304 主重载：四个坐标以元组 <c>Store</c> 送索引 0~3 并逐一 <c>UnpinTuple</c>；输出两个 <c>DOUBLE</c> 元组。直线是过两点的<b>无限长直线</b>，距离在本实例轮廓采样点上度量（像素单位）。</para>
	///   <para>四个元组各自多值时（多条直线）与轮廓的配对/广播规则 [待实测]；与标量重载的取舍：只在需要多条直线批量算距离时用本重载，单直线用 <see cref="DistanceLc(double, double, double, double, out double, out double)"/> 省去四次钉/解钉。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\rail.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       edges.DistanceLc(new JlTuple(100.0), new JlTuple(0.0),
	///           new JlTuple(100.0), new JlTuple(512.0),
	///           out JlTuple dMin, out JlTuple dMax);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>out 元组无需 <c>Dispose()</c>。</para>
	/// </remarks>
	public void DistanceLc(JlTuple row1, JlTuple column1, JlTuple row2, JlTuple column2, out JlTuple distanceMin, out JlTuple distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1304);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row1);
		JlNativeApi.Store(proc, 1, column1);
		JlNativeApi.Store(proc, 2, row2);
		JlNativeApi.Store(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(column1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(column2);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out distanceMin);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>计算由两点确定的无限长直线到本实例轮廓的最近/最远距离（采样点意义、像素单位），经 out 标量参数只回第一个值，多轮廓时仅见第一条，无新句柄。</summary>
	/// <param name="row1">直线第一个点的行坐标。</param>
	/// <param name="column1">直线第一个点的列坐标。</param>
	/// <param name="row2">直线第二个点的行坐标。</param>
	/// <param name="column2">直线第二个点的列坐标。</param>
	/// <param name="distanceMin">直线与轮廓间的最小距离。</param>
	/// <param name="distanceMax">直线与轮廓间的最大距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1304（标量便捷重载）：<c>(row1,column1)-(row2,column2)</c> 定义的是<b>无限长直线</b>（不是线段；要线段用 <c>DistanceSc</c>），距离在本实例轮廓采样点上度量，单位为像素。</para>
	///   <para><b>与主重载的实际差异</b>四点走 <c>StoreD</c>、输出 <c>LoadD</c> 只装第一个值：多轮廓时只拿到第一条的距离，多直线（元组多值）不可用本重载。</para>
	///   <para><b>参数取向</b>两点重合时直线方向无定义，报错还是退化 [待实测]。distanceMin 不区分点在直线哪一侧（无符号），要带符号偏差用 <c>GetRegressParamsXld</c> 或拟合直线后自行计算 [待实测：本算子是否给带符号值]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\rail.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       edges.DistanceLc(100.0, 0.0, 100.0, 512.0, out double dMin, out double dMax);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>无新句柄产生；直线完全穿过轮廓时 distanceMin≈0。</para>
	/// </remarks>
	public void DistanceLc(double row1, double column1, double row2, double column2, out double distanceMin, out double distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1304);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row1);
		JlNativeApi.StoreD(proc, 1, column1);
		JlNativeApi.StoreD(proc, 2, row2);
		JlNativeApi.StoreD(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out distanceMin);
		err = JlNativeApi.LoadD(proc, 1, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>以元组给出测试点坐标，计算点到本实例轮廓采样点集的最近/最远距离（像素），经 out 参数给出两个 DOUBLE 意义 JlTuple 元组，无新句柄。</summary>
	/// <param name="row">点的行坐标。</param>
	/// <param name="column">点的列坐标。</param>
	/// <param name="distanceMin">点与轮廓间的最小距离。</param>
	/// <param name="distanceMax">点与轮廓间的最大距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1305 主重载：<c>row</c>/<c>column</c> 以元组 <c>Store</c>+<c>UnpinTuple</c> 送索引 0/1，输出两个 <c>JlTupleType.DOUBLE</c> 元组（像素单位的最近/最远距离）。</para>
	///   <para>距离在本实例各轮廓的<b>采样点</b>上度量（不是连续曲线）；本实例含 N 条轮廓时输出是否有 N 个值、多测试点与多轮廓的配对/广播规则 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要"直线/线段到轮廓"用 <see cref="DistanceLc(JlTuple, JlTuple, JlTuple, JlTuple, out JlTuple, out JlTuple)"/> 或 <c>DistanceSc</c>；要逐点距离存成轮廓属性用 <see cref="DistanceContoursXld(JlXLDCont, string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\via.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       edges.DistancePc(new JlTuple(256.0), new JlTuple(128.0),
	///           out JlTuple dMin, out JlTuple dMax);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>out 元组是普通 <c>JlTuple</c>，不要 <c>Dispose()</c>；传 double 字面量会绑定标量重载并丢多值输出。</para>
	/// </remarks>
	public void DistancePc(JlTuple row, JlTuple column, out JlTuple distanceMin, out JlTuple distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1305);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out distanceMin);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>计算单个测试点到本实例轮廓采样点集的最近/最远距离（像素，row 向下、column 向右），经 out 标量参数只回第一个值，多轮廓时仅见第一条，无新句柄。</summary>
	/// <param name="row">点的行坐标。</param>
	/// <param name="column">点的列坐标。</param>
	/// <param name="distanceMin">点与轮廓间的最小距离。</param>
	/// <param name="distanceMax">点与轮廓间的最大距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1305（标量便捷重载）：算点到<b>本实例轮廓采样点集</b>的最小/最大距离——不是到连续插值曲线的距离，采样稀疏时 distanceMax 会被最近采样点钳制 [待实测：max 是否按整条轮廓取]。</para>
	///   <para><b>与主重载的实际差异</b>输入 <c>StoreD</c> 单点直写；输出 <c>LoadD</c> 只装第一个值——本实例含多条轮廓时只有第一条的距离可见，其余静默丢弃。逐条距离用 <see cref="DistancePc(JlTuple, JlTuple, out JlTuple, out JlTuple)"/>。</para>
	///   <para><b>参数取向</b>坐标单位为像素（亚像素值合法），row 向下、column 向右；点在轮廓上时 distanceMin≈0。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\via.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       edges.DistancePc(256.0, 128.0, out double dMin, out double dMax);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>无新句柄产生；点到空元组对象（<c>CountObj()==0</c>）上调用时的行为 [待实测]。</para>
	/// </remarks>
	public void DistancePc(double row, double column, out double distanceMin, out double distanceMax)
	{
		IntPtr proc = JlNativeApi.PreCall(1305);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out distanceMin);
		err = JlNativeApi.LoadD(proc, 1, err, out distanceMax);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>把 SerializeXld 产出的字节反序列化并原地灌入本实例（先释放原句柄再装载，调用后旧句柄作废），无返回值；不返回新句柄，本实例之后仍需正常释放。</summary>
	/// <param name="serializedItemHandle">序列化项的句柄。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1552：实现体先 <c>Dispose()</c> 再 <c>Load(proc,1)</c>——反序列化结果<b>原地灌入本实例</b>，无返回值；字节经 <c>JlSerializationBuffer</c>（<c>using</c> 管理，调用结束即释放原生缓冲）以 <c>Store</c> 送索引 0。</para>
	///   <para><b>约束或前提</b>只接受 <c>SerializeXld()</c>（或同族序列化）产出的字节；喂入损坏/异源字节时的失败形态 [待实测]。调用后本实例原句柄已作废，指向同一旧句柄的其他引用不可再用。</para>
	///   <para><b>与相邻算子的取舍</b>要"返回新句柄"的写法本库没有对应物——先 <c>new JlXLDCont()</c> 再反序列化进新实例即可，别把结果再 <c>Dispose()</c> 到旧实例头上。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\mask.pgm");
	///   byte[] blob;
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       blob = edges.SerializeXld();
	///   }
	///   JlXLDCont target = new JlXLDCont();
	///   target.DeserializeXld(blob);
	///   int n = target.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>方法带 <c>new</c>；本实例之后仍需正常 <c>Dispose()</c>。</para>
	/// </remarks>
	public new void DeserializeXld(byte[] serializedItemHandle)
	{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1552);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>把本实例轮廓序列化为托管 byte[] 数组返回（普通数组、非句柄、无需释放），本实例内容保持不变。</summary>
	/// <returns>序列化项的句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1553：本实例索引 1 送入口，序列化结果经 <c>JlSerializationBuffer.LoadBytes</c> 从索引 0 装成 <c>byte[]</c> 返回——是普通托管数组，不是句柄，也无需 <c>Dispose()</c>。本实例保持不变。</para>
	///   <para><b>约束或前提</b>元组内多条轮廓与各自属性是否完整序列化 [待实测：与 <see cref="DeserializeXld(byte[])"/> 往返后属性是否仍在]。字节内容不可作为跨版本长期存档格式依赖 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>给人看的矢量文件用 DXF 族；程序内暂存/跨线程转移用本对算子（保真且免磁盘）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\mask.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       byte[] blob = edges.SerializeXld();
	///       JlXLDCont copy = new JlXLDCont();
	///       copy.DeserializeXld(blob);
	///       int n = copy.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>方法带 <c>new</c>，基类变量调用不走本实现；大轮廓集合序列化会占与点数成正比的内存。</para>
	/// </remarks>
	public new byte[] SerializeXld()
	{
		IntPtr proc = JlNativeApi.PreCall(1553);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>从 DXF 文件读取轮廓并原地灌入本实例（先释放后装载，原内容失效），返回状态信息 JlTuple（非句柄）；本元组重载可一次传多个通用生成参数。</summary>
	/// <param name="fileName">DXF 文件名。</param>
	/// <param name="genParamName">可为 DXF 输入调整的通用参数名。Default: []</param>
	/// <param name="genParamValue">可为 DXF 输入调整的通用参数值。Default: []</param>
	/// <returns>状态信息。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1556 主重载：实现体开头 <c>Dispose()</c>、结尾 <c>Load(proc,1)</c>——读到的轮廓原地灌入本实例；返回的状态信息是 <c>JlTuple.LoadNew</c> 出的新元组（索引 0），不是句柄。通用参数名/值以元组 <c>Store</c>+<c>UnpinTuple</c> 送入，可一次给多个参数。</para>
	///   <para><b>约束或前提</b>与标量重载同为"覆写本实例"语义：调用后原内容失效，共享该句柄的引用会踩空；DXF 单位与图像像素的换算 [待实测]。空文件/非法文件的行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>HALCON 原生格式轮廓读写算子本库未包装（本库不存在）；文件交换通道只有 DXF（本对算子）与 ArcInfo（<see cref="ReadContourXldArcInfo(string)"/>/<c>WriteContourXldArcInfo</c>）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont contours = new JlXLDCont();
	///   JlTuple status = contours.ReadContourXldDxf(@"C:\vision\flange.dxf", new JlTuple(), new JlTuple());
	///   int n = contours.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回的 <c>JlTuple</c> 不需要 <c>Dispose()</c>；本实例用完记得释放。</para>
	/// </remarks>
	public JlTuple ReadContourXldDxf(string fileName, JlTuple genParamName, JlTuple genParamValue)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1556);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.Store(proc, 2, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = Load(proc, 1, err);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>从 DXF 文件读取轮廓并原地灌入本实例（先释放后装载、非返回新句柄），返回状态信息字符串；本标量重载一次只接受一个通用生成参数（名走 StoreS、值走 StoreD）。</summary>
	/// <param name="fileName">DXF 文件名。</param>
	/// <param name="genParamName">可为 DXF 输入调整的通用参数名。Default: []</param>
	/// <param name="genParamValue">可为 DXF 输入调整的通用参数值。Default: []</param>
	/// <returns>状态信息。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1556（标量便捷重载）：实现体先 <c>Dispose()</c> 再 <c>Load(proc,1)</c>——读到的轮廓<b>原地灌入本实例</b>，不是返回新句柄；返回值只是状态信息字符串（<c>LoadS</c> 装载索引 0）。</para>
	///   <para><b>约束或前提</b>调用前本实例可以是空 <c>new JlXLDCont()</c>，但调用后原内容已被释放——把同一个句柄传给别人再用会踩空。DXF 中的坐标/比例如何换算到图像坐标 [待实测]。文件不存在时的报错形态 [待实测]。</para>
	///   <para><b>与主重载的实际差异</b>本重载 <c>genParamName</c> 走 <c>StoreS</c>、<c>genParamValue</c> 走 <c>StoreD</c>，一次只能给一个通用参数；多参数（图层过滤等）用元组重载（<see cref="ReadContourXldDxf(string, JlTuple, JlTuple)"/>），状态输出为 <c>JlTuple</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlXLDCont contours = new JlXLDCont();
	///   // "scale" 仅示意：合法的通用参数名与取值范围 [待实测]
	///   string status = contours.ReadContourXldDxf(@"C:\vision\flange.dxf", "scale", 1.0);
	///   int n = contours.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>本实例仍在托管侧持有新句柄，用完记得 <c>Dispose()</c>；不要再对返回值做句柄处置——它只是字符串。</para>
	/// </remarks>
	public string ReadContourXldDxf(string fileName, string genParamName, double genParamValue)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1556);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.StoreD(proc, 2, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		err = JlNativeApi.LoadS(proc, 0, err, out var stringValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return stringValue;
	}

	/// <summary>把本实例轮廓按 DXF 格式写出到指定文件，属纯输出动作：本实例不被改写、不产生新句柄、无返回值。</summary>
	/// <param name="fileName">DXF 文件名。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1557：本实例索引 1 送入口、<c>fileName</c> 走 <c>StoreS</c>，无输出参数——纯写出动作，本实例不被改写、不产生新句柄。</para>
	///   <para><b>约束或前提</b>轮廓坐标按 DXF 的 (x,y) 写出（即 column→x、row→y，y 轴方向与图像坐标相反与否 [待实测]）；文件已存在时是覆盖还是追加 [待实测]。属性/开口闭合信息能否往返 [待实测]，要与 <see cref="ReadContourXldDxf(string, string, double)"/> 成对验证后再依赖。</para>
	///   <para><b>与相邻算子的取舍</b>跨进程/落盘缓存对象树用 <c>SerializeXld</c>+<c>DeserializeXld</c>（保真、含属性）；与 CAD 交互才用 DXF。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\gasket.pgm");
	///   using (JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40))
	///   {
	///       edges.WriteContourXldDxf(@"C:\vision\gasket.dxf");
	///   }
	///   </code>
	///   <para><b>资源与坑</b>目录不存在或无写权限时报错方式（异常还是错误码上抛）[待实测]。</para>
	/// </remarks>
	public void WriteContourXldDxf(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1557);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>从本实例轮廓中筛选出包含给定测试点（图像像素坐标，亚像素值合法）的那些，输出为保序子集新句柄（需释放）；本元组重载支持多个测试点。</summary>
	/// <param name="row">测试点的行坐标。Default: 100.0</param>
	/// <param name="column">测试点的列坐标。Default: 100.0</param>
	/// <returns>包含测试点的所有轮廓或多边形。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1595 主重载：<c>row</c>/<c>column</c> 以元组 <c>Store</c> 送索引 0/1 并在调用后 <c>UnpinTuple</c>；输出为包含任一测试点的轮廓新句柄，保序子集。"任一命中"还是"全部命中"、点数与轮廓数不等时的配对 [待实测]。</para>
	///   <para><b>约束或前提</b>坐标是图像像素坐标（亚像素值合法）；判定基于轮廓折线围成的内部区域，开口轮廓如何闭合参与判定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>单点查询用标量重载更省事（少两次 <c>UnpinTuple</c>）；按形状筛用 <see cref="SelectShapeXld(JlTuple, string, JlTuple, JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\stencil.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlXLDCont holes = edges.SelectXldPoint(new JlTuple(128.0, 384.0), new JlTuple(128.0, 384.0));
	///   int n = holes.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；直接给两个 double 字面量会绑定到标量重载，多点必须显式 <c>new JlTuple(...)</c>。</para>
	/// </remarks>
	public new JlXLDCont SelectXldPoint(JlTuple row, JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1595);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>判断单个测试点是否落在各闭合轮廓（按采样点连成折线的内部解释、非像素区域）之内，输出包含该点的轮廓保序子集新句柄（需释放）。</summary>
	/// <param name="row">测试点的行坐标。Default: 100.0</param>
	/// <param name="column">测试点的列坐标。Default: 100.0</param>
	/// <returns>包含测试点的所有轮廓或多边形。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>标量便捷重载，原生 id 同为 1595：判断测试点是否落在各闭合轮廓（按多边形内部解释）之内，输出"包含该点"的轮廓新句柄；判定发生在轮廓采样点连成的折线上，不是像素区域——半像素级贴边点结果不稳定 [待实测：贴边判定方向]。</para>
	///   <para><b>与主重载的实际差异</b><c>row</c>/<c>column</c> 走 <c>StoreD</c> 单点直写，无 <c>UnpinTuple</c>；要"任一测试点命中即选中"的多点筛选用 <see cref="SelectXldPoint(JlTuple, JlTuple)"/> 并显式 <c>new JlTuple(...)</c>（两个 double 字面量总绑定本重载）。多点多轮廓时"任取"还是"全取" [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>按形状特征筛用 <see cref="SelectShapeXld(string, string, double, double)"/>；点是否属于某区域用 <c>JlRegion</c> 一侧的测试；本算子专用于"这个孔/这块料对应哪条轮廓"的定位。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\stencil.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont hole = edges.SelectXldPoint(256.0, 128.0))
	///   {
	///       int n = hole.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；没有轮廓包含该点时得到空对象元组而非异常 [待实测]。开口轮廓把首尾相连后参与判定 [待实测]。</para>
	/// </remarks>
	public new JlXLDCont SelectXldPoint(double row, double column)
	{
		IntPtr proc = JlNativeApi.PreCall(1595);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按形状特征（如面积、圆度）及其 min/max 区间筛选本实例轮廓，输出满足条件者的保序子集新句柄（未选中项消失、下标重排，需释放）；本元组重载支持多特征 and/or 组合。</summary>
	/// <param name="features">要检查的形状特征。Default: "area"</param>
	/// <param name="operation">各特征之间的运算方式。Default: "and"</param>
	/// <param name="min">特征下限或 'min'。Default: 150.0</param>
	/// <param name="max">特征上限或 'max'。Default: 99999.0</param>
	/// <returns>满足条件的轮廓或多边形。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1597 主重载：<c>features</c>/<c>min</c>/<c>max</c> 以元组 <c>Store</c>+<c>UnpinTuple</c> 送入口，<c>operation</c> 走 <c>StoreS</c>；输出为满足条件轮廓组成的新句柄（保序子集，未选中的直接消失，下标重新紧凑）。</para>
	///   <para><b>约束或前提</b><c>features</c>、<c>min</c>、<c>max</c> 三个元组按位置一一配对，长度不一致时的行为 [待实测]；<c>min</c>/<c>max</c> 允许填字符串 "min"/"max" 表示该特征不设下限/上限（英文文档 "or 'min'"）。开口轮廓的面积等形状特征按首尾相连的多边形解释 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>本算子只按形状特征（面积、圆度等）筛；按下标删用 <c>RemoveObj</c>；按坐标位置筛用 <see cref="SelectXldPoint(JlTuple, JlTuple)"/>；按自定义逻辑筛选就直接遍历 <c>GetContourAttribXld</c> 自己判断。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\stencil.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont big = edges.SelectShapeXld(new JlTuple("area"), "and",
	///       new JlTuple(500.0), new JlTuple("max")))
	///   {
	///       int n = big.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；方法带 <c>new</c>，经基类变量调用时不走本实现。</para>
	/// </remarks>
	public new JlXLDCont SelectShapeXld(JlTuple features, string operation, JlTuple min, JlTuple max)
	{
		IntPtr proc = JlNativeApi.PreCall(1597);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, features);
		JlNativeApi.StoreS(proc, 1, operation);
		JlNativeApi.Store(proc, 2, min);
		JlNativeApi.Store(proc, 3, max);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(features);
		JlNativeApi.UnpinTuple(min);
		JlNativeApi.UnpinTuple(max);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按单个形状特征及其 min/max 标量区间筛选本实例轮廓，输出满足条件者的保序子集新句柄（未选中项消失、下标重排，需释放）；本标量重载一次只查一个特征一个区间。</summary>
	/// <param name="features">要检查的形状特征。Default: "area"</param>
	/// <param name="operation">各特征之间的运算方式。Default: "and"</param>
	/// <param name="min">特征下限或 'min'。Default: 150.0</param>
	/// <param name="max">特征上限或 'max'。Default: 99999.0</param>
	/// <returns>满足条件的轮廓或多边形。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>标量便捷重载，原生 id 同为 1597，筛选语义与"保序子集、下标重排"的坑见 <see cref="SelectShapeXld(JlTuple, string, JlTuple, JlTuple)"/>。</para>
	///   <para><b>与主重载的实际差异</b><c>features</c> 走 <c>StoreS</c>、<c>min</c>/<c>max</c> 走 <c>StoreD</c> 直写全局标量，无 <c>UnpinTuple</c> 开销；一次只能查一个特征一个区间。多特征 "and"/"or" 组合必须用元组重载显式 <c>new JlTuple(...)</c>（字符串字面量会绑定到本重载）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\stencil.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont mid = edges.SelectShapeXld("area", "and", 150.0, 99999.0))
	///   {
	///       int n = mid.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；全部轮廓都不满足时得到空元组对象（<c>CountObj()==0</c>）而非异常 [待实测]。</para>
	/// </remarks>
	public new JlXLDCont SelectShapeXld(string features, string operation, double min, double max)
	{
		IntPtr proc = JlNativeApi.PreCall(1597);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, features);
		JlNativeApi.StoreS(proc, 1, operation);
		JlNativeApi.StoreD(proc, 2, min);
		JlNativeApi.StoreD(proc, 3, max);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>对本实例每条轮廓单独做形状变换（如 type="convex" 求凸包），输出与原轮廓条数一致、下标一一对应的新句柄（需释放），原轮廓不变。</summary>
	/// <param name="type">变换类型。Default: "convex"</param>
	/// <returns>变换后的轮廓或多边形。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1608：本实例索引 1 送入口、<c>type</c> 走 <c>StoreS</c>，<c>LoadNew</c> 返回新句柄，原轮廓不变。对每条输入轮廓单独做形状变换（凸包等），输出条数与输入一致、下标保持对应。</para>
	///   <para><b>约束或前提</b>形状变换按"闭合区域意义"解释轮廓：开口轮廓如何参与凸包/外接矩形计算 [待实测]；结果轮廓的起点与方向是否与输入对齐 [待实测]。除 "convex" 外支持的类型取值 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要外接矩形参数（行列、边长、角度）用 <c>JlXLD.SmallestRectangle2Xld</c>；只要按特征筛选用 <see cref="SelectContoursXld(string, double, double, double, double)"/>；本算子的价值在于得到"变换后的轮廓本身"继续做形状比较或生成区域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\sieve.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont hull = edges.ShapeTransXld("convex"))
	///   {
	///       int n = hull.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；凸包会把凹进去的缺损整体抹平，拿它做缺损检测等于把缺陷抹掉再比。</para>
	/// </remarks>
	public new JlXLDCont ShapeTransXld(string type)
	{
		IntPtr proc = JlNativeApi.PreCall(1608);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}


	/// <summary>结合相机内参与世界位姿把本实例轮廓点反投影到世界坐标系 z=0 平面，返回世界坐标下的轮廓新句柄（需释放）；本元组重载的 scale 可携带数值，输出量纲随之而定。</summary>
	/// <param name="cameraParam">相机内参。</param>
	/// <param name="worldPose">世界坐标系在相机坐标下的 3D 位姿。</param>
	/// <param name="scale">尺度或单位。Default: "m"</param>
	/// <returns>世界坐标下变换后的 XLD 轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1810 主重载：<c>cameraParam</c>、<c>worldPose</c>、<c>scale</c> 全部以元组 <c>Store</c> 送入口并在调用后 <c>UnpinTuple</c>，把轮廓点反投影到世界坐标系 z=0 平面，<c>LoadNew</c> 返回新句柄。</para>
	///   <para><b>与标量重载的差异</b>字符串版（<see cref="ContourToWorldPlaneXld(JlTuple, JlPose, string)"/>）只能给量纲字符串；本重载的 <c>scale</c> 可携带数值 [待实测：数值表示比例因子还是每像素物理尺寸]。</para>
	///   <para><b>约束或前提</b>相机参数与位姿必须匹配产生该轮廓的图像；输出坐标量纲与 <c>scale</c> 绑定，下游若按像素理解会整体错一个尺度。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\plate.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlTuple camPar = new double[] { 8e-3, 1e-5, 1e-5, 5.8e-6, 3.2e-6, 512.0, 512.0, 0.0 };
	///   JlPose pose = new JlPose();
	///   using (JlXLDCont world = edges.ContourToWorldPlaneXld(camPar, pose, new JlTuple(0.001)))
	///   {
	///       int n = world.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；<c>UnpinTuple</c> 在调用后立即执行，传入的 <c>JlTuple</c>/<c>JlPose</c> 不实现 IDisposable，无需额外处置。</para>
	/// </remarks>
	public JlXLDCont ContourToWorldPlaneXld(JlTuple cameraParam, JlPose worldPose, JlTuple scale)
	{
		IntPtr proc = JlNativeApi.PreCall(1810);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, cameraParam);
		JlNativeApi.Store(proc, 1, worldPose);
		JlNativeApi.Store(proc, 2, scale);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(cameraParam);
		JlNativeApi.UnpinTuple(worldPose);
		JlNativeApi.UnpinTuple(scale);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>沿相机成像射线把本实例轮廓点反投影到世界坐标系 z=0 平面，返回世界坐标下的轮廓新句柄（需释放）；本重载 scale 为量纲字符串（如 "m"），输出坐标量纲随其改变。</summary>
	/// <param name="cameraParam">相机内参。</param>
	/// <param name="worldPose">世界坐标系在相机坐标下的 3D 位姿。</param>
	/// <param name="scale">尺度或单位。Default: "m"</param>
	/// <returns>世界坐标下变换后的 XLD 轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1810（标量便捷重载，<c>scale</c> 走 <c>StoreS</c>）：把图像坐标下的轮廓点沿相机成像射线反投影到世界坐标系 z=0 平面，输出新句柄。输入的 row/column 被视为图像坐标，<c>cameraParam</c>、<c>worldPose</c> 共同决定射线与平面的交点。</para>
	///   <para><b>与主重载的实际差异</b>元组重载对 <c>scale</c> 用 <c>Store</c>+<c>UnpinTuple</c>，可传数值比例；本重载只接受字符串量纲（如 "m"）。两个重载对 <c>cameraParam</c>/<c>worldPose</c> 都按元组钉住后 <c>UnpinTuple</c>——<c>JlPose</c> 在原生侧就是按元组传的。</para>
	///   <para><b>约束或前提</b><c>cameraParam</c> 必须是与生成轮廓的图像同一套标定参数；<c>worldPose</c> 是世界系在相机坐标下的位姿（方向写反会得到错得离谱但"看起来正常"的坐标）[待实测：错误位姿下的具体畸变形态]。轮廓射线的延长线与 z=0 无交点（点位于地平线以上）时输出如何给出 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要图像平面内的平移/旋转用 <c>AffineTransContourXld</c>；本算子用于"相机已标定、零件是平面"的毫米坐标换算，比三维重建族轻。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\plate.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlTuple camPar = new double[] { 8e-3, 1e-5, 1e-5, 5.8e-6, 3.2e-6, 512.0, 512.0, 0.0 };
	///   JlPose pose = new JlPose();
	///   JlXLDCont world = edges.ContourToWorldPlaneXld(camPar, pose, "m");
	///   int n = world.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄需 <c>Dispose()</c>；输出坐标的量纲随 <c>scale</c> 改变，与像素坐标混用（比如再喂给按像素设计的算子）会得到尺度错乱的结果。</para>
	/// </remarks>
	public JlXLDCont ContourToWorldPlaneXld(JlTuple cameraParam, JlPose worldPose, string scale)
	{
		IntPtr proc = JlNativeApi.PreCall(1810);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, cameraParam);
		JlNativeApi.Store(proc, 1, worldPose);
		JlNativeApi.StoreS(proc, 2, scale);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(cameraParam);
		JlNativeApi.UnpinTuple(worldPose);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}


	/// <summary>按下标成对计算本实例与 contour2 各轮廓对的最近距离，并经 out 参数同时给出两侧最近点的亚像素 row/column（返回值与出参均为 DOUBLE 意义 JlTuple，像素单位），不产生新句柄。</summary>
	/// <param name="contour2">第二条输入轮廓。</param>
	/// <param name="mode">距离计算方式。Default: "fast_point_to_segment"</param>
	/// <param name="row1">Contour1 上该点的行坐标。</param>
	/// <param name="column1">Contour1 上该点的列坐标。</param>
	/// <param name="row2">Contour2 上该点的行坐标。</param>
	/// <param name="column2">Contour2 上该点的列坐标。</param>
	/// <returns>两条轮廓间的最小距离。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1996：本实例以索引 1、<c>contour2</c> 以索引 2 送入口，<c>mode</c> 以元组 <c>Store</c>+<c>UnpinTuple</c>；输出 5 个元组（最小距离 + 两条轮廓上最近点的 row/column）全部按 <c>JlTupleType.DOUBLE</c> 装载，即亚像素精度的像素坐标。</para>
	///   <para>按下标成对计算：第 i 个值 = 本实例第 i 条与 <c>contour2</c> 第 i 条的最小距离；两侧条数不等时的配对/截断规则 [待实测]。与 <c>DistanceCcMin</c> 的差别仅在于本算子额外回送"最近点是在哪两个坐标上取到的"，适合还要继续用这两个点做测量的场合。</para>
	///   <para><b>与相邻算子的取舍</b>只要一个距离数值用 <c>DistanceCcMin</c>；要逐条"最短+最长+均值"用 <c>DistanceCc</c>；点与轮廓用 <c>DistancePc</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\parts.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlXLDCont left = edges.CropContoursXld(new JlTuple(0.0), new JlTuple(0.0), new JlTuple(512.0), new JlTuple(256.0), "true");
	///   using (JlXLDCont right = edges.CropContoursXld(new JlTuple(0.0), new JlTuple(256.0), new JlTuple(512.0), new JlTuple(512.0), "true"))
	///   {
	///       JlTuple mode = "fast_point_to_segment";
	///       JlTuple d = left.DistanceCcMinPoints(right, mode,
	///           out JlTuple r1, out JlTuple c1, out JlTuple r2, out JlTuple c2);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>本算子不产生新句柄，返回的是普通 <c>JlTuple</c>（不实现 IDisposable，不要 Dispose）；5 个 out 元组同理。</para>
	/// </remarks>
	public JlTuple DistanceCcMinPoints(JlXLDCont contour2, JlTuple mode, out JlTuple row1, out JlTuple column1, out JlTuple row2, out JlTuple column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1996);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, contour2);
		JlNativeApi.Store(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mode);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out row1);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out column1);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out row2);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out column2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour2);
		return tuple;
	}

	/// <summary>计算本实例与 contour2 轮廓对的最近距离及两侧最近点的亚像素坐标（像素单位），经 out 标量参数与返回值只回第一对的值，其余静默丢弃且不报错；不产生新句柄。</summary>
	/// <param name="contour2">第二条输入轮廓。</param>
	/// <param name="mode">距离计算方式。Default: "fast_point_to_segment"</param>
	/// <param name="row1">Contour1 上该点的行坐标。</param>
	/// <param name="column1">Contour1 上该点的列坐标。</param>
	/// <param name="row2">Contour2 上该点的行坐标。</param>
	/// <param name="column2">Contour2 上该点的列坐标。</param>
	/// <returns>两条轮廓间的最小距离。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>标量便捷重载，原生 id 同为 1996：成对比较本实例第 i 条与 <c>contour2</c> 第 i 条轮廓，给出最小距离及两侧最近点的亚像素坐标（像素单位）；元组重载与模式取舍见 <see cref="DistanceCcMinPoints(JlXLDCont, JlTuple, out JlTuple, out JlTuple, out JlTuple, out JlTuple)"/>。</para>
	///   <para><b>与主重载的实际差异（真坑）</b>输出用 <c>LoadD</c> 只装载每个结果的<b>第一个值</b>：两侧元组各含 N 条轮廓时只拿到第一对的距离与点，其余 N-1 个值被静默丢弃且不报错。需要逐条距离必须用元组重载。</para>
	///   <para><b>约束或前提</b>两侧元素数不等时的配对规则（截断/广播）[待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\parts.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlXLDCont left = edges.CropContoursXld(0.0, 0.0, 512.0, 256.0, "true");
	///   using (JlXLDCont right = edges.CropContoursXld(0.0, 256.0, 512.0, 512.0, "true"))
	///   {
	///       double d = left.DistanceCcMinPoints(right, "fast_point_to_segment",
	///           out double r1, out double c1, out double r2, out double c2);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>本算子不产生新句柄；但示例中 <c>edges</c>/<c>left</c>/<c>right</c> 都是各自算子返回的新句柄，需逐个 <c>Dispose()</c>。两轮廓相交时最小距离为 0，最近点即交点附近。</para>
	/// </remarks>
	public double DistanceCcMinPoints(JlXLDCont contour2, string mode, out double row1, out double column1, out double row2, out double column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1996);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, contour2);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out row1);
		err = JlNativeApi.LoadD(proc, 2, err, out column1);
		err = JlNativeApi.LoadD(proc, 3, err, out row2);
		err = JlNativeApi.LoadD(proc, 4, err, out column2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour2);
		return doubleValue;
	}

	/// <summary>把 objectsInsert 整段插入本实例轮廓元组的 index 位置（该处及之后元素后移），返回拼接后的新句柄（需释放），本实例不变。</summary>
	/// <param name="objectsInsert">要插入的对象元组。</param>
	/// <param name="index">插入对象的位置序号。</param>
	/// <returns>扩充后的对象元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2003：本实例以索引 1 送入口、<c>objectsInsert</c> 以 <c>Store</c> 送索引 2、<c>index</c> 以 <c>StoreI</c> 送索引 0；结果 <c>LoadNew</c> 返回拼接后的新句柄，本实例不变。</para>
	///   <para>在 <c>index</c> 处整段插入：该位置及其后的元素全部后移，插入多元素元组时新元组长度 = 原长度 + <c>CountObj(objectsInsert)</c>。插入点是紧邻 <c>index</c> 之前还是之后、下标基数、能否用超出末尾的索引实现"追加" [待实测]；追加到末尾更稳的写法是 <c>ConcatObj</c>。</para>
	///   <para><b>与相邻算子的取舍</b>总是接在末尾用 <c>ConcatObj</c>（无需知道当前长度）；按特征挑选用 <c>SelectContoursXld</c>；替换既有元素用 <c>ReplaceObj</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\wafer.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlXLDCont extra = edges.CropContoursXld(0.0, 0.0, 128.0, 128.0, "true");
	///   JlXLDCont merged = edges.InsertObj(extra, 3);
	///   int n = merged.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；<c>GC.KeepAlive</c> 表明 <c>objectsInsert</c> 在原生调用结束前不得释放，调用返回后即可处置。</para>
	/// </remarks>
	public JlXLDCont InsertObj(JlXLDCont objectsInsert, int index)
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

	/// <summary>按索引元组从本实例轮廓元组删除指定元素，返回剩余元素组成的新句柄（需释放），本实例不被改写；删除后剩余轮廓整体重新编号，旧下标全部错位。</summary>
	/// <param name="index">待移除对象的序号。</param>
	/// <returns>剩余的对象元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2005：本实例以索引 1 送入口、<c>index</c> 以 <c>Store</c>（元组）送索引 0 并在调用后 <c>UnpinTuple</c>；结果 <c>LoadNew</c> 返回"剩余元素"的新句柄，本实例不被改写。</para>
	///   <para>删除后剩余轮廓整体重新编号：此前按旧下标缓存的配对关系（例如与某 <c>JlTuple</c> 特征数组按下标对齐）全部错位，需要在结果上重新取值。越界、重复、负索引的处理 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>按几何/统计特征剔除用 <c>SelectContoursXld</c>（不依赖下标，鲁棒性更好）；本重载适合"已知确切下标"的场景，如下游流程只关心第 3、7 条弧。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\ball.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlXLDCont rest = edges.RemoveObj(new JlTuple(2));
	///   int n = rest.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>。方法声明带 <c>new</c>：经 <c>JlXLDCont</c> 变量调用时绑定的是本实现（返回强类型句柄），向上转型为基类后调用的是基类版本。整数字面量会绑定 <see cref="RemoveObj(int)"/>，多索引须显式 <c>new JlTuple(...)</c>。</para>
	/// </remarks>
	public new JlXLDCont RemoveObj(JlTuple index)
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

	/// <summary>按单个整数索引从本实例轮廓元组删除一条元素，返回剩余元素重新编号后的新句柄（需释放），本实例不被改写；本标量重载一次只能删一条。</summary>
	/// <param name="index">待移除对象的序号。</param>
	/// <returns>剩余的对象元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>标量便捷重载，原生 id 同为 2005，"删除后重新编号"的坑与返回值语义见 <see cref="RemoveObj(JlTuple)"/>。</para>
	///   <para><b>与主重载的实际差异</b><c>index</c> 走 <c>StoreI</c> 直写，无 <c>UnpinTuple</c> 固定开销，但一次只能删一条轮廓；成批删除用元组重载并显式 <c>new JlTuple(...)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\ball.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont rest = edges.RemoveObj(1))
	///   {
	///       int n = rest.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；删除不存在的下标时是静默忽略还是报错 [待实测]。</para>
	/// </remarks>
	public new JlXLDCont RemoveObj(int index)
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

	/// <summary>用 objectsReplace 的元素替换本实例轮廓元组中 index（元组，可多处）处的元素，返回替换后的新句柄（需释放），原元组不被改写；元素数不等时元组长度随之变、其后下标平移。</summary>
	/// <param name="objectsReplace">用于替换的元素。</param>
	/// <param name="index">待替换元素的序号。</param>
	/// <returns>替换后的元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2006：本实例以索引 1 送入口、<c>objectsReplace</c> 以索引 2 送入口、<c>index</c> 以 <c>Store</c>（元组）送索引 0 并在调用后 <c>UnpinTuple</c>，结果 <c>LoadNew</c> 返回新句柄，原元组不被改写。</para>
	///   <para>把 <c>index</c> 处的元素换成 <c>objectsReplace</c> 的元素：当 <c>objectsReplace</c> 的元素数与被替换元素数不等时，整个元组长度随之变（其后元素下标全部平移）；<c>index</c> 的多值与 <c>objectsReplace</c> 的多元素如何配对、基 0 还是基 1 [待实测]。</para>
	///   <para><b>约束或前提</b>越界或重复索引的行为 [待实测]；替换只换"对象元素"，不会把被替换轮廓的属性带进新元素。</para>
	///   <para><b>与相邻算子的取舍</b>只增不换用 <c>InsertObj</c>/<c>ConcatObj</c>；要"删掉某些轮廓"用 <c>RemoveObj</c>；按特征筛选用 <c>SelectContoursXld</c>（不按下标、更稳）。按下标操作依赖上游输出顺序，<c>SortContoursXld</c> 或任何合并类算子之后旧下标一律作废。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\pcb.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   JlXLDCont patch = edges.CropContoursXld(0.0, 0.0, 256.0, 256.0, "true");
	///   JlXLDCont replaced = edges.ReplaceObj(patch, new JlTuple(2));
	///   int n = replaced.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；直接写整数字面量会绑定到 <c>int</c> 重载，需要多索引时必须显式 <c>new JlTuple(...)</c>。输入元组与 <c>objectsReplace</c> 在调用结束前都不可释放（<c>GC.KeepAlive</c>）。</para>
	/// </remarks>
	public JlXLDCont ReplaceObj(JlXLDCont objectsReplace, JlTuple index)
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

	/// <summary>用 objectsReplace 的元素替换本实例轮廓元组中单个 index 处的元素，返回替换后的新句柄（需释放），本实例保持原样；本标量重载一次只替换一个位置。</summary>
	/// <param name="objectsReplace">用于替换的元素。</param>
	/// <param name="index">待替换元素的序号。</param>
	/// <returns>替换后的元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>标量便捷重载，原生 id 同为 2006，语义、"替换后元组长度可能变、其后下标平移"的坑均见 <see cref="ReplaceObj(JlXLDCont, JlTuple)"/>。</para>
	///   <para><b>与主重载的实际差异</b><c>index</c> 走 <c>StoreI</c> 直写单个整数，不产生 <c>UnpinTuple</c>，一次调用只能替换一个位置；需要成批替换时用元组重载并显式写 <c>new JlTuple(...)</c>（整数字面量总是绑定到本重载）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\pcb.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   using (JlXLDCont patch = edges.CropContoursXld(0.0, 0.0, 256.0, 256.0, "true"))
	///   {
	///       JlXLDCont replaced = edges.ReplaceObj(patch, 2);
	///       int n = replaced.CountObj();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>返回值需 <c>Dispose()</c>；替换只影响结果句柄，本实例保持原样。</para>
	/// </remarks>
	public JlXLDCont ReplaceObj(JlXLDCont objectsReplace, int index)
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


	/// <summary>用 region 与 mode 对本实例轮廓求交，只保留落在区域内的轮廓段，一条轮廓可能被切成多条（输出条数与下标不再对应输入），返回新句柄（需释放），原轮廓不被改写。</summary>
	/// <param name="region">输入区域。</param>
	/// <param name="mode">求交方式。Default: "lines"</param>
	/// <returns>输入轮廓被选中的部分。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 2183：本实例轮廓以索引 2 送入口、<c>region</c> 以索引 1 送入口、<c>mode</c> 走 <c>StoreS</c>，结果 <c>InitOCT</c>+<c>LoadNew</c> 返回新句柄，不改写原轮廓。</para>
	///   <para>只保留轮廓落在区域内的部分：区域外的片段被剪掉，一条轮廓可能被切成多条，输出条数与下标都不再与输入对应（与 <c>UnionCocircularContoursXld</c>、<c>JlRegion</c> 的 <c>ExpandRegion</c> 族同类坑），不要按"输入第 i 条"配对取结果。</para>
	///   <para><b>约束或前提</b>轮廓与区域须处于同一图像坐标系。轮廓点是亚像素而区域按像素格序列化，跨区域边界那一小段点如何取舍 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>矩形视野裁切用 <c>CropContoursXld</c>（浮点边框、可用 <c>closeContours</c> 控制闭合性）；要整条轮廓按条件取舍、不切断轮廓（点数不变、更快）用 <c>SelectContoursXld</c>；本算子适合任意形状掩膜（如阈值区域、环形区域）取轮廓段。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage(@"C:\vision\part.pgm");
	///   JlXLDCont edges = img.EdgesSubPix("canny", 0.4, 20, 40);
	///   // 整数字面量会同时匹配 (JlTuple x4) 与 (double x4) 构造器 → CS0121；写小数即绑 double 版
	///   JlRegion mask = new JlRegion(64.0, 64.0, 448.0, 448.0);
	///   JlXLDCont inside = edges.IntersectionRegionContourXld(mask, "lines");
	///   int n = inside.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回值为新句柄，需 <c>Dispose()</c>；轮廓与区域完全不相交时返回空对象元组还是报错 [待实测]。实现体末尾的 <c>GC.KeepAlive(region)</c> 表明原生调用结束前 <c>region</c> 不可释放，调用返回后即可 <c>Dispose()</c>。</para>
	/// </remarks>
	public JlXLDCont IntersectionRegionContourXld(JlRegion region, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(2183);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}
}
