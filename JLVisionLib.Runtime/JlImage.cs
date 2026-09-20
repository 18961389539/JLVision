using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>表示图像对象（数组）的实例。</summary>
[Serializable]
public class JlImage : JlObject, ISerializable, ICloneable
{
	/// <summary>按 1 起始的索引取回对象数组中的单个图像元素。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>本索引器直接转调 <c>SelectObj(index)</c> 的元组重载（原生 id 572）：
	///   <c>index</c> 以 <see cref="JlTuple"/> 传入，内部先固定后解固定，取回的是<b>新句柄</b>而不是原对象的引用。</para>
	///   <para><b>索引从 1 起</b>对象数组沿用 1 基索引，<c>this[0]</c> 取不到首元素（结果大概率为空对象数组）[待实测]；
	///   要第一个元素写 <c>this[1]</c>，元素个数用 <c>CountObj()</c> 核对，别按 C# 数组的 0 基习惯用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   int n = img.CountObj();
	///   using JlImage first = img[1];                       // 单个元素，返回新句柄
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需自行释放；一次取多段请用 <c>SelectObj(JlTuple)</c> 传索引列表，
	///   只取一个单值时用 <c>SelectObj(int)</c> 可省掉元组固定开销。</para>
	/// </remarks>
	public new JlImage this[JlTuple index] => SelectObj(index);

	/// <summary>创建一个未初始化的图像句柄（key 为空指针），不分配任何像素内存。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>实现只是 <c>base(JlObjectBase.UNDEF, copy: false)</c>：托管对象建出来了，但原生端还没有
	///   任何图像。此时 <see cref="JlObjectBase.IsInitialized"/> 返回 <c>false</c>，直接拿它参与运算会在原生端报错
	///   [待实测：具体错误码]。</para>
	///   <para><b>什么时候用</b>两种场景：一是配合 <c>GenPsfMotion</c>/<c>GenPsfDefocus</c> 这类<b>原地生成器</b>
	///   （它们先 <c>Dispose()</c> 再把结果 <c>Load</c> 进当前对象，所以调用前只需要一个空壳）；二是作为
	///   <c>Deserialize</c>/<c>Clone</c> 的内部占位。有真实像素需求时应该用带尺寸的构造器，别拿空句柄凑数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage psf = new JlImage();                    // 空壳占位
	///   psf.GenPsfDefocus(64, 64, 5.0);                       // 原地生成器把 PSF 写进 psf 本身
	///   bool ready = psf.IsInitialized();                     // 生成后为 true
	///   </code>
	///   <para><b>资源与坑</b>未初始化的对象 <c>Dispose()</c> 也安全（无柄可释放）；但拿它当输入调算子时失败发生在
	///   原生侧，托管层看不到有意义的图像属性。</para>
	/// </remarks>
	public JlImage()
		: base(JlObjectBase.UNDEF, copy: false)
	{
	}

	/// <summary>把已有原生图像句柄包成托管 JlImage：执行引用计数拷贝（CopyObject），不是像素深拷贝。</summary>
	/// <param name="key">本进程内有效的图标对象句柄；<c>IntPtr.Zero</c>（UNDEF）视为未初始化的空对象。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转调 <see cref="JlImage(IntPtr,bool)"/> 且 copy=true：key 为有效句柄时基类执行 <c>JlNativeApi.CopyObject(key)</c>，新对象与 key 共享底层数据、各持独立引用计数；key 为 UNDEF 时不发起任何原生调用。</para>
	///   <para><b>约束或前提</b>内部/互操作用入口（[EditorBrowsable(Never)]）。构造末尾 <c>AssertObjectClass</c> 经原生查询类名：仅以 "image" 开头或等于 "any" 才放行，否则抛 JlException（"Iconic object type mismatch"）；UNDEF 句柄跳过该校验，构造出的是未初始化对象。</para>
	///   <para><b>与相邻成员的取舍</b>要"不加引用、直接接管"外部裸句柄用 <see cref="JlImage(IntPtr,bool)"/> 的 copy=false；要改动互不影响的独立像素副本用 <c>Clone()</c>（序列化往返的深拷贝），本构造只多一层引用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage src = new JlImage("byte", 64, 64);
	///   using JlImage dup = new JlImage(src.Key);      // 引用计数拷贝，共享底层图像数据
	///   </code>
	///   <para><b>资源与坑</b>引用计数拷贝 ≠ 深拷贝：两侧看到同一份底层数据。copy=true 路径下传入的 key 本身仍由调用方负责释放（新对象另持一份引用）；<c>GC.KeepAlive(this)</c> 保证句柄装载完成前本对象不被回收。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlImage(IntPtr key)
		: this(key, copy: true)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>按原生句柄构造图像，copy 决定是"CopyObject 加引用"还是"直接接管裸句柄"。</summary>
	/// <param name="key">原生图标对象句柄；<c>IntPtr.Zero</c>（UNDEF）表示未初始化，<c>IntPtr(1)</c>（UNDEF2）会被规整为 UNDEF。</param>
	/// <param name="copy">true：对 key 执行 CopyObject，得到共享底层数据、引用计数独立的新句柄；false：原样接管 key，不增加引用。</param>
	/// <remarks>
	///   <para><b>功能说明</b>基类 <c>JlObjectBase(IntPtr, bool)</c> 的透传入口，是所有"接管/拷贝"路径的公共落点：仅当 copy 为真且 key 既非 UNDEF 也非 UNDEF2 时才真正调用 <c>JlNativeApi.CopyObject</c>，否则按原样存键（UNDEF2 归零为 UNDEF）。</para>
	///   <para><b>约束或前提</b>内部/互操作用入口（[EditorBrowsable(Never)]）。构造末尾 <c>AssertObjectClass</c> 经原生查询类名，仅以 "image" 开头或等于 "any" 才放行，否则抛 JlException；UNDEF 句柄跳过校验。</para>
	///   <para><b>与相邻成员的取舍</b>常规包装已有句柄直接用 <see cref="JlImage(IntPtr)"/>（即固定 copy=true 的简写）；从托管 JlObject 复制用 <see cref="JlImage(JlObject)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using System;
	///   using JLVisionLib;
	///
	///   JlImage src = new JlImage("byte", 64, 64);
	///   IntPtr k = src.CopyKey();                              // 独立句柄，默认由调用方负责释放
	///   using JlImage takeover = new JlImage(k, copy: false);  // 对象接管 k，交出后不得再释放 k
	///   </code>
	///   <para><b>资源与坑</b>copy=false 时所有权转移进本对象，Dispose 会对 key 执行 ClearObject：再释放一次即双重释放，key 被提前清掉则本对象悬空；copy=true 时传入的 key 释放责任留在调用方。两种误用在托管层都不报错，故障发生在原生侧。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlImage(IntPtr key, bool copy)
		: base(key, copy)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	/// <summary>把任意 JlObject 包装/复制成 JlImage：引用计数拷贝并做原生类名校验，非 image 类型抛 JlException。</summary>
	/// <param name="obj">源对象；其内部句柄会被 CopyObject 复制一次引用。null 会在取句柄时抛托管异常。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转调基类 <c>JlObjectBase(JlObjectBase)</c>，即以 copy=true 对 obj 的句柄执行 <c>CopyObject</c>：新对象与源共享底层数据、引用计数独立，这是引用级复制而非像素深拷贝。</para>
	///   <para><b>约束或前提</b>本构造是 "JlObject → JlImage" 的类型收窄入口：构造末尾 <c>AssertObjectClass</c> 向原生查类名，以 "image" 开头或等于 "any" 才放行——拿 JlRegion 等其他图标族来包装会直接抛 JlException（"Iconic object type mismatch"）；源是未初始化对象（UNDEF 句柄）时跳过校验，构造出的对象同样未初始化。</para>
	///   <para><b>与相邻成员的取舍</b>源已确认为 JlImage 时直接传引用即可，不必再构造；需要改动互不影响的独立副本用 <c>Clone()</c>（序列化往返深拷贝）。包装对象数组时只加一层引用，元素个数与内容与源一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlObject generic = img;                                // 向上转型，不产生新句柄
	///   using JlImage img2 = new JlImage(generic);             // 经 image 类名校验后引用计数复制
	///   </code>
	///   <para><b>资源与坑</b>构造期间基类用 <c>GC.KeepAlive(obj)</c> 防止源句柄被提前回收；得到的副本是新句柄，不用时自行 Dispose。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlImage(JlObject obj)
		: base(obj)
	{
		AssertObjectClass();
		GC.KeepAlive(this);
	}

	private void AssertObjectClass()
	{
		JlNativeApi.AssertObjectClass(key, "image");
	}

	/// <summary>把原生过程某槽位的输出句柄装载进全新 JlImage（"返回新句柄"路径的统一出口），透传错误码、自身不抛异常。</summary>
	/// <param name="proc">当前正在执行的原生过程句柄：须在 CallProcedure 之后、PostCall 之前调用（PostCall 会销毁 proc）。</param>
	/// <param name="parIndex">输出图像在原生侧的图标参数索引，与 InitOCT 声明的索引一致。</param>
	/// <param name="err">CallProcedure 的返回码；失败（<c>JlNativeApi.IsFailure</c>：非 2 即失败）时原样短路返回，不触碰过程输出。</param>
	/// <param name="obj">输出：新建的 JlImage。失败时它仍非 null，但未初始化（IsInitialized() 为 false）。</param>
	/// <returns>透传或更新后的错误码，供调用方交给 PostCall。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>内部/互操作用入口（[EditorBrowsable(Never)]），是所有"返回新图像"算子包装的公共出口：先用 <c>JlObjectBase.UNDEF</c> 造一个未初始化的空对象（不分配像素内存），再调实例 <c>Load</c>——由 <c>GetOutputObject(proc, parIndex, out key)</c> 直接接管输出句柄（不做 CopyObject，引用归 obj），并在需要时重新登记终结化。</para>
	///   <para><b>约束或前提</b>Load 要求目标句柄必须为未初始化，否则抛 JlException（本方法刚新建空对象，天然满足）。本方法装载的是图标对象，与数值输出的 INTEGER/DOUBLE 装载（LoadI/LoadD）是两条不同路径。</para>
	///   <para><b>与相邻方法的取舍</b>原地改写已有对象走实例 <c>Load</c>（如 read_image 构造器 id 1578）；产出全新结果句柄走本方法。err 转成异常由调用方的 <c>PostCall</c> 完成，本方法只传递错误码。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using System;
	///   using JLVisionLib;
	///
	///   IntPtr proc = JlNativeApi.PreCall(1578);               // read_image
	///   JlNativeApi.StoreS(proc, 0, "c:\\tmp\\part01");
	///   JlNativeApi.InitOCT(proc, 1);                          // 声明图标输出在槽 1
	///   int err = JlNativeApi.CallProcedure(proc);
	///   err = JlImage.LoadNew(proc, 1, err, out JlImage img);  // 输出接管为新句柄
	///   JlNativeApi.PostCall(proc, err);                       // 销毁 proc，出错则抛异常
	///   using (img)
	///   {
	///   }
	///   </code>
	///   <para><b>资源与坑</b>失败路径下 img 是未初始化空对象，Dispose 安全；成功时 obj 独占新句柄，不用要释放。因 err 失败时本方法不读输出，句柄不会装载到一半悬在过程中。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadNew(IntPtr proc, int parIndex, int err, out JlImage obj)
	{
		obj = new JlImage(JlObjectBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	/// <summary>
	///   把 pixelPointer 指向的外部像素内存包成图像，结果经 Load 原地写进当前对象；type 与缓冲区的元素类型、行长必须一致。
	/// </summary>
	/// <param name="type">像素类型。默认值："byte"</param>
	/// <param name="width">图像宽度。默认值：512</param>
	/// <param name="height">图像高度。默认值：512</param>
	/// <param name="pixelPointer">指向第一个灰度值的指针。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 591：把一块<b>外部像素内存</b>（<paramref name="pixelPointer"/> 指向的连续缓冲区）
	///   包成图像，结果经 <c>Load</c> 写进当前对象。适合与大数组/非托管库共享像素而不再复制一遍的场景。</para>
	///   <para><b>前提</b><paramref name="type"/> 与缓冲区的元素类型、行长必须一致：本层只把指针原样
	///   <c>StoreIP</c> 传下去，不校验指向的内容。行对齐/字节序约定由原生决定 [待实测]；类型写错时读到的是错位数据，
	///   不会在这里报错。<paramref name="width"/>/<paramref name="height"/> 声明缓冲区的行列数。</para>
	///   <para><b>与三参构造器的取舍</b>只要一块全零画布用 <see cref="JlImage(string,int,int)"/>；
	///   像素已在非托管缓冲区里（自采图、编解码库输出）才用本构造器。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using System;
	///   using System.Runtime.InteropServices;
	///   using JLVisionLib;
	///
	///   IntPtr buf = Marshal.AllocHGlobal(64 * 64);           // 外部字节缓冲
	///   using (JlImage img = new JlImage("byte", 64, 64, buf))
	///   {
	///       using JlRegion reg = img.Threshold(128.0, double.MaxValue);
	///   }
	///   Marshal.FreeHGlobal(buf);                             // 图像释放后再归还缓冲区
	///   </code>
	///   <para><b>资源与坑</b>原生侧是否复制这份像素本层不体现 [待实测]；保守做法是图像对象存活期间保持缓冲区有效、
	///   <c>Dispose</c> 之后再 <c>FreeHGlobal</c>。缓冲区尺寸不足会把越界内容当成像素读掉。</para>
	/// </remarks>
	public JlImage(string type, int width, int height, IntPtr pixelPointer)
	{
		IntPtr proc = JlNativeApi.PreCall(591);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreIP(proc, 3, pixelPointer);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   分配一张 width×height、像素类型 type 的真实图像并原地写进当前对象，全图填充同一常数灰度，用作运算画布或合成测试图。
	/// </summary>
	/// <param name="type">像素类型。默认值："byte"</param>
	/// <param name="width">图像宽度。默认值：512</param>
	/// <param name="height">图像高度。默认值：512</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 592：分配一张 <c>width×height</c>、像素类型为 <paramref name="type"/> 的图像，
	///   全图填充同一常数灰度（本层不传灰度参数，常数值由原生定为 0 [待实测]）。结果经 <c>Load</c> 写进当前对象，
	///   这是<b>真分配了像素</b>的对象，与 <see cref="JlImage()"/> 的空壳不同。</para>
	///   <para><b>type 的写法</b><paramref name="type"/> 是字符串型像素类型名（<c>StoreS</c> 直传、本层不校验），
	///   本仓库示例惯用 "byte" 与 "real"；合法取值全集与其对内存占用的影响由原生决定 [待实测]。</para>
	///   <para><b>典型用途</b>做运算前的画布（与别的图 <c>AddImage</c>/<c>AbsDiffImage</c> 前对齐尺寸）、造合成测试图。
	///   需要真实内容时应该用读图或指针构造器，而不是拿零图凑数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage camera = new JlImage("byte", 640, 480);
	///   using JlImage canvas = new JlImage("byte", 640, 480);   // 同尺寸零图，可参与逐像素运算
	///   using JlImage diff = camera - canvas;                   // byte 图差值非负（camera 更暗一侧被截 0）
	///   </code>
	///   <para><b>资源与坑</b>分配的是完整像素内存，大尺寸画布别反复建；用完释放。</para>
	/// </remarks>
	public JlImage(string type, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(592);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   以 JlTuple 传入文件名从磁盘读图（原生 id 1578 的元组重载），像素原地写进当前对象；手里只有一个文件名时走 string 重载免元组固定开销。
	/// </summary>
	/// <param name="fileName">要读取的图像名称。默认值："printer_chip/printer_chip_01"</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1578 的元组重载：文件名以 <c>Store</c> 钉成元组传下去，读出的图像经 <c>Load</c>
	///   写进当前对象。与 <see cref="JlImage(string)"/> 同算子，差别仅在于名字走元组（可携带多文件名，多文件名是否
	///   读成多元素对象数组由原生决定 [待实测]）。</para>
	///   <para><b>与 string 重载的取舍</b>单个文件名直接写字符串字面量即可（编译器会走更专门的 <c>JlImage(string)</c>，
	///   无固定/解固定开销）；只有当你手里已经是 <see cref="JlTuple"/> 形态的名字集合时才用本重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage img = new JlImage(new JlTuple("c:\\tmp\\part01"));
	///   </code>
	///   <para><b>资源与坑</b>读文件失败在原生侧报错；支持的格式、是否自动补扩展名由原生决定 [待实测]。</para>
	/// </remarks>
	public JlImage(JlTuple fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1578);
		JlNativeApi.Store(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(fileName);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   按字符串文件名从磁盘读一张图，像素经 Load 原地写进当前对象，是读单个文件的常规入口；彩色文件读出通常是多通道图。
	/// </summary>
	/// <param name="fileName">要读取的图像名称。默认值："printer_chip/printer_chip_01"</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1578（read_image）：从磁盘读一张图，结果经 <c>Load</c> 写进当前对象。
	///   文件名以 <c>StoreS</c> 直写字符串，无元组固定/解固定，是读单个文件时的常规入口。</para>
	///   <para><b>路径约定</b>绝对路径最稳；相对名要先能解析到原生图像目录，其解析规则本层不体现 [待实测]。
	///   彩色文件读出通常是多通道图，喂给只认单通道的算子前先 <c>AccessChannel(1)</c> 取通道或转类型。</para>
	///   <para><b>与元组重载的取舍</b>见 <see cref="JlImage(JlTuple)"/>；单个文件名一律用本重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage img = new JlImage("c:\\tmp\\part01");   // 读盘上的图
	///   using JlRegion fg = img.Threshold(128.0, double.MaxValue);
	///   </code>
	///   <para><b>资源与坑</b>文件不存在/格式不支持时在原生侧报错并由 <c>PostCall</c> 抛出；
	///   构造出的对象持有真实像素，用完释放。</para>
	/// </remarks>
	public JlImage(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1578);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeImage();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>ISerializable 反序列化构造器：从 info 的 "data" 键取出字节块，经原生 id 1570 原地重建图像。</summary>
	/// <param name="info">序列化数据；必须含键 "data"，值为 SerializeImage（id 1571）写出的 byte[]，缺键或类型不符会在 GetValue 处抛托管异常。</param>
	/// <param name="context">序列化框架的流上下文；本实现不读取该参数。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>ISerializable.GetObjectData</c> 配对的接收端：取 "data" 字节块后转交 <c>DeserializeImage</c>——先 Dispose 当前对象，再以原生 id 1570 把字节块作为控制参数写入槽 0、InitOCT 声明槽 1 图标输出，最后 Load 回填本对象：原地改写落一个全新原生句柄，像素在原生端重新分配，与序列化时的原图完全独立（深拷贝语义）。</para>
	///   <para><b>约束或前提</b>设计上由序列化 formatter 自动调用（[EditorBrowsable(Never)]），不是日常手工构造入口；字节格式与 SerializeImage 的输出强耦合，外部来源的 byte[] 能否被 1570 解析 [待实测]。</para>
	///   <para><b>与相邻成员的取舍</b>流到对象的重建用静态 <see cref="Deserialize(Stream)"/>（内部同样走 DeserializeImage）；只有参与对象图序列化机制时才需要本构造器。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using System.Runtime.Serialization;
	///   using JLVisionLib;
	///
	///   JlImage src = new JlImage("byte", 64, 64);
	///   byte[] data = src.SerializeImage();                          // id 1571：打包成托管 byte[]
	///   SerializationInfo info = new SerializationInfo(typeof(JlImage), new FormatterConverter());
	///   info.AddValue("data", data, typeof(byte[]));                 // 与 GetObjectData 的键同名
	///   using JlImage restored = new JlImage(info, default(StreamingContext));
	///   </code>
	///   <para><b>资源与坑</b>restored 是新句柄需自行释放。实现进入 DeserializeImage 前会先 Dispose()——在本构造器里对象刚建出来 Dispose 无害，但这条先释放再装载的路径意味着绝不能拿还想保留的图像去复用同一套反序列化改写。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlImage(SerializationInfo info, StreamingContext context)
	{
		DeserializeImage((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把图像对象序列化为二进制流。</summary>
	/// <remarks>
	///   <para><b>功能说明</b><c>Serialize(Stream)</c> 走托管层的 <c>SerializeImage()</c>：把当前对象的像素数据与对象数组
	///   结构打包成一段字节写进流。它序列化的是<b>数据副本</b>，与原生句柄无关，因此可用于落盘或跨进程传输，
	///   接收端反序列化得到的是新分配的句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using System.IO;
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using (var ms = new MemoryStream())
	///   {
	///       img.Serialize(ms);
	///       ms.Position = 0;
	///       using JlImage back = JlImage.Deserialize(ms);   // 新句柄，与 img 独立
	///   }
	///   </code>
	///   <para><b>资源与坑</b>与 <see cref="Deserialize(Stream)"/> 配对使用；反序列化得到的对象由调用方释放。
	///   单张图与对象数组都能序列化，但流里不带类型信息，读回需按同族接口。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeImage(), stream);
	}

	/// <summary>从二进制流反序列化出图像对象。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>静态方法，从 <see cref="Serialize(Stream)"/> 写出的流重建一个新 <c>JlImage</c>，
	///   经 <c>DeserializeImage</c> 在原生端重新分配像素内存。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using System.IO;
	///   using JLVisionLib;
	///
	///   using JlImage img = JlImage.Deserialize(File.OpenRead("c:\\tmp\\image.bin"));
	///   </code>
	///   <para><b>资源与坑</b>返回的是新句柄，用完释放；只喂本族 <c>Serialize</c> 写出的流，其它来源的字节无法保证能读回。</para>
	/// </remarks>
	public new static JlImage Deserialize(Stream stream)
	{
		JlImage hImage = new JlImage();
		hImage.DeserializeImage(JlSerializationBuffer.ReadFromStream(stream));
		return hImage;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <remarks>
	///   <para><b>功能说明</b><c>Clone()</c> 通过 <c>SerializeImage()</c>+<c>DeserializeImage()</c> 做一次<b>深拷贝</b>：
	///   新对象的像素内存是重新分配的，改副本不影响原图。这与 <c>CopyImage()</c>（原生 id 571，同为深拷贝但走原生端）
	///   目的相同，区别只是本方法在托管层绕了序列化一圈。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage copy = img.Clone();                   // 独立副本，像素已复制
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；对象数组会连同结构一起复制，<c>CountObj()</c> 保持不变。
	///   只需一个引用别名、不打算改动时不要 <c>Clone()</c>，白白复制一份像素。</para>
	/// </remarks>
	public new JlImage Clone()
	{
		byte[] data = SerializeImage();
		JlImage obj = new JlImage();
		obj.DeserializeImage(data);
		return obj;
	}

	/// <summary>图像取反：-image 转调 InvertImage()。</summary>
	/// <remarks>
	///   <para><b>功能说明</b><c>-image</c> 返回 <c>image.InvertImage()</c> 的新句柄，输入不变。
	///   取反是按通道类型最大灰度做镜像：byte 上 <c>255-g</c>，<c>uint2</c> 上 <c>65535-g</c>；
	///   <c>real</c> 的镜像基准本层不确定 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage inv = -img;                           // 等价于 img.InvertImage()
	///   </code>
	/// </remarks>
	public static JlImage operator -(JlImage image)
	{
		return image.InvertImage();
	}

	/// <summary>两图相加：image1 + image2 转调 AddImage(image2, 1.0, 0.0)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>逐像素求和，返回新句柄。要求两图同尺寸、同通道数；类型不一致时的取舍由原生决定 [待实测]。</para>
	///   <para><b>截断坑</b>结果按操作数类型存储：<c>byte</c> 图上两值相加超过 255 会被截断而非进位，
	///   要保住量程先 <c>ConvertImageType("real")</c> 再相加，或事后 <c>ScaleImage</c> 归一化。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage a = new JlImage("real", 64, 64);
	///   JlImage b = new JlImage("real", 64, 64);
	///   using JlImage sum = a + b;                          // 等价于 a.AddImage(b, 1.0, 0.0)
	///   </code>
	/// </remarks>
	public static JlImage operator +(JlImage image1, JlImage image2)
	{
		return image1.AddImage(image2, 1.0, 0.0);
	}

	/// <summary>两图相减：image1 - image2 转调 SubImage(image2, 1.0, 0.0)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>逐像素求差，返回新句柄，常用于配准残差、背景差分。</para>
	///   <para><b>负值丢失</b><c>byte</c> 图差分会出现负值并被截成 0（或回绕），差分图因此看不到暗下去的一侧。
	///   做缺陷/运动差分前先 <c>ConvertImageType("real")</c>，或差完再 <c>+ 128</c> 抬偏移。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage cur = new JlImage("real", 64, 64);
	///   JlImage bg = new JlImage("real", 64, 64);
	///   using JlImage diff = cur - bg;                      // 保留正负，可再取绝对值/阈值
	///   </code>
	/// </remarks>
	public static JlImage operator -(JlImage image1, JlImage image2)
	{
		return image1.SubImage(image2, 1.0, 0.0);
	}

	/// <summary>两图相乘：image1 * image2 转调 MultImage(image2, 1.0, 0.0)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>逐像素乘积，返回新句柄。常见用法是拿一幅二值/掩膜图与彩色或灰度图相乘做区域屏蔽。</para>
	///   <para><b>截断坑</b>两个 0..255 的 <c>byte</c> 值相乘会迅速超过 255 并被截断，乘出来的图往往一片死白；
	///   做掩膜请用 0/1 取值的一路，或先 <c>ConvertImageType("real")</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   JlImage mask = new JlImage("real", 64, 64);
	///   using JlImage outImg = img * mask;                  // 等价于 img.MultImage(mask, 1.0, 0.0)
	///   </code>
	/// </remarks>
	public static JlImage operator *(JlImage image1, JlImage image2)
	{
		return image1.MultImage(image2, 1.0, 0.0);
	}

	/// <summary>整幅加常数偏移：image + add 转调 ScaleImage(1.0, add)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>每像素加 <paramref name="add"/>，返回新句柄。常用来给差分图/对数图抬偏移，把负值搬进正区间。</para>
	///   <para><b>截断坑</b>结果按原类型存储，<c>byte</c> 图加正数超过 255 会饱和或截断 [待实测：饱和还是截断]，
	///   需要可逆的量纲时先 <c>ConvertImageType("real")</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage diff = new JlImage("real", 64, 64);
	///   using JlImage lifted = diff + 128.0;                // 等价于 diff.ScaleImage(1.0, 128.0)
	///   </code>
	/// </remarks>
	public static JlImage operator +(JlImage image, double add)
	{
		return image.ScaleImage(1.0, add);
	}

	/// <summary>加常数偏移（常数在左）：add + image 与 image + add 等价。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>加法可交换，本重载同样转调 <c>image.ScaleImage(1.0, add)</c>，行为、截断坑与
	///   <c>image + add</c> 完全一致，只是写法语序。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage diff = new JlImage("real", 64, 64);
	///   using JlImage lifted = 128.0 + diff;               // 与 diff + 128.0 相同
	///   </code>
	/// </remarks>
	public static JlImage operator +(double add, JlImage image)
	{
		return image.ScaleImage(1.0, add);
	}

	/// <summary>整幅减常数偏移：image - sub 转调 ScaleImage(1.0, -sub)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>每像素减 <paramref name="sub"/>，返回新句柄。没有 <c>sub - image</c> 的反向重载，
	///   需要"常数减去图像"请写 <c>(-image) + sub</c>。</para>
	///   <para><b>截断坑</b><c>byte</c> 图减出负值会被截到 0，做黑电平扣除前建议 <c>ConvertImageType("real")</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   using JlImage darkened = img - 10.0;               // 等价于 img.ScaleImage(1.0, -10.0)
	///   </code>
	/// </remarks>
	public static JlImage operator -(JlImage image, double sub)
	{
		return image.ScaleImage(1.0, 0.0 - sub);
	}

	/// <summary>按系数缩放灰度：image * mult 转调 ScaleImage(mult, 0.0)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>每像素乘以 <paramref name="mult"/>，返回新句柄，用于增益/归一化。</para>
	///   <para><b>截断坑</b><c>byte</c> 上乘大于 1 的系数会超过 255 而饱和或截断 [待实测]，乘小于 1 会丢低位精度；
	///   要精确增益先转 <c>real</c> 再乘、再转回。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   using JlImage gain = img * 2.0;                    // 等价于 img.ScaleImage(2.0, 0.0)
	///   </code>
	/// </remarks>
	public static JlImage operator *(JlImage image, double mult)
	{
		return image.ScaleImage(mult, 0.0);
	}

	/// <summary>按系数缩放灰度（常数在左）：mult * image 与 image * mult 等价。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>乘法可交换，同样转调 <c>image.ScaleImage(mult, 0.0)</c>，行为与 <c>image * mult</c> 一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   using JlImage gain = 2.0 * img;                    // 与 img * 2.0 相同
	///   </code>
	/// </remarks>
	public static JlImage operator *(double mult, JlImage image)
	{
		return image.ScaleImage(mult, 0.0);
	}

	/// <summary>按除数缩放灰度：image / div 转调 ScaleImage(1.0/div, 0.0)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>每像素除以 <paramref name="div"/>，本层实现是乘 <c>1.0/div</c> 再交给 <c>ScaleImage</c>，
	///   返回新句柄。<paramref name="div"/> 为 0 时 <c>1.0/0</c> 得 Infinity，行为传下去由原生决定 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   using JlImage dim = img / 2.0;                     // 等价于 img.ScaleImage(0.5, 0.0)
	///   </code>
	/// </remarks>
	public static JlImage operator /(JlImage image, double div)
	{
		return image.ScaleImage(1.0 / div, 0.0);
	}

	/// <summary>逐像素动态分割：image1 &gt;= image2 转调 image1.DynThreshold(image2, 0.0, "light")。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>把 image2 当作逐像素阈值图，取 image1 中亮于（含等于）它的像素，偏移 0、取 light。
	///   这正是做局部阈值/背景差分的写法：image2 是低频照度图或参考帧。</para>
	///   <para><b>返回的是区域不是布尔</b>结果是 <see cref="JlRegion"/>，不是 <c>bool</c>，也不能与 <c>true/false</c> 比较；
	///   要"哪些像素满足"就接区域，要按像素计数用 <c>.Area()</c> 一类。两图须同尺寸，本层不校验 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlImage refImg = new JlImage("real", 64, 64);
	///   using JlRegion bright = img &gt;= refImg;            // 逐像素比参考帧，返回区域
	///   int n = bright.CountObj();
	///   </code>
	/// </remarks>
	public static JlRegion operator >=(JlImage image1, JlImage image2)
	{
		return image1.DynThreshold(image2, 0.0, "light");
	}

	/// <summary>逐像素动态分割（取暗区）：image1 &lt;= image2 转调 image1.DynThreshold(image2, 0.0, "dark")。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>image1 &gt;= image2</c> 同族、同为逐像素、同样返回 <see cref="JlRegion"/>，
	///   差别仅在取 dark：保留 image1 中暗于（含等于）阈值图 image2 的像素。用于暗点/阴影缺陷。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlImage refImg = new JlImage("real", 64, 64);
	///   using JlRegion dark = img &lt;= refImg;              // 逐像素暗于参考帧
	///   </code>
	/// </remarks>
	public static JlRegion operator <=(JlImage image1, JlImage image2)
	{
		return image1.DynThreshold(image2, 0.0, "dark");
	}

	/// <summary>常数阈值分割：image &gt;= threshold 转调 image.Threshold(threshold, double.MaxValue)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>取灰度不小于 <paramref name="threshold"/> 的像素，上界用 <c>double.MaxValue</c> 表示"到此为止全要"。
	///   返回 <see cref="JlRegion"/>，不是 <c>bool</c>——别把比较运算符当布尔表达式用。</para>
	///   <para><b>量纲</b><paramref name="threshold"/> 是 <c>double</c>，但比较对象是图像实际灰度：<c>byte</c> 0..255、
	///   <c>uint2</c> 0..65535。给一个超过本类型量程的阈值不会报错，只会得到空区域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion bright = img &gt;= 128.0;            // 等价于 img.Threshold(128.0, double.MaxValue)
	///   int n = bright.Connection().CountObj();
	///   </code>
	/// </remarks>
	public static JlRegion operator >=(JlImage image, double threshold)
	{
		return image.Threshold(threshold, double.MaxValue);
	}

	/// <summary>常数阈值分割（取暗区）：image &lt;= threshold 转调 image.Threshold(double.MinValue, threshold)。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>image &gt;= threshold</c> 同族，同样返回 <see cref="JlRegion"/>；这里取灰度不大于阈值的像素，
	///   下界用 <c>double.MinValue</c> 兜住。量纲与"不是布尔"的注意事项见 <c>image &gt;= threshold</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion dark = img &lt;= 60.0;               // 等价于 img.Threshold(double.MinValue, 60.0)
	///   </code>
	/// </remarks>
	public static JlRegion operator <=(JlImage image, double threshold)
	{
		return image.Threshold(double.MinValue, threshold);
	}

	/// <summary>常数阈值分割（常数在左）：threshold &gt;= image 等价于 image &lt;= threshold。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>常数写在左边时语义会翻转："<c>threshold &gt;= image</c>"其实是"取 image 中不大于阈值的像素"，
	///   故转调 <c>image.Threshold(double.MinValue, threshold)</c>。同样返回 <see cref="JlRegion"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion dark = 100.0 &gt;= img;               // 与 img &lt;= 100.0 相同
	///   </code>
	/// </remarks>
	public static JlRegion operator >=(double threshold, JlImage image)
	{
		return image.Threshold(double.MinValue, threshold);
	}

	/// <summary>常数阈值分割（常数在左、取亮区）：threshold &lt;= image 等价于 image &gt;= threshold。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>字面比较是"常数不大于图像"，即取 image 中<b>不小于</b> <paramref name="threshold"/> 的像素，
	///   故转调 <c>image.Threshold(threshold, double.MaxValue)</c>，返回 <see cref="JlRegion"/>（新句柄），不是布尔。</para>
	///   <para><b>与同族写法的对应</b><c>img &gt;= t</c> 与 <c>t &lt;= img</c> 走的是同一实现；
	///   <c>t &gt;= img</c> 与 <c>img &lt;= t</c> 则取暗区（同族的另外两个常数重载）。
	///   两种写法结果完全相同，按可读性选：拿参考帧比亮度时把常数写在左边更像人话。</para>
	///   <para><b>量纲</b>阈值与图像实际灰度比较：byte 0..255、uint2 0..65535；超量程阈值不报错、只给空区域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion bright = 100.0 &lt;= img;             // 灰度不小于 100 的像素
	///   int n = bright.Connection().CountObj();
	///   </code>
	/// </remarks>
	public static JlRegion operator <=(double threshold, JlImage image)
	{
		return image.Threshold(threshold, double.MaxValue);
	}

	/// <summary>收缩定义域：image &amp; region 转调 image.ReduceDomain(region)，返回新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>把图像的有效域收缩到 <paramref name="region"/> 与原定义域的<b>交集</b>内，像素值一个不改，
	///   改的只是"哪些像素参与后续统计/分割"。返回新图像句柄，输入不被改写。</para>
	///   <para><b>什么时候用它</b>只想在 ROI 内做直方图、阈值、Blob 统计时——比先抠像素再拼回便宜得多，
	///   因为域运算通常只改变遍历范围。要"把域外像素置 0 变成真数据"的不是本方法，那得逐像素乘掩膜。</para>
	///   <para><b>与 Threshold 的关系</b>两者常配成环：<c>Threshold</c> 图→区域，<c>&amp;</c> 区域→缩图域；
	///   对已收缩的图再 <c>Threshold</c> 只在域内出结果。区域完全落在图外时得到空域图 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion roi = new JlRegion(8.0, 8.0, 56.0, 56.0);   // 左上/右下角点
	///   using JlImage reduced = img &amp; roi;                       // 域收缩到 ROI 交集
	///   </code>
	///   <para><b>资源与坑</b>结果是新句柄需释放；<c>GC.KeepAlive</c> 在 <c>ReduceDomain</c> 内部保两边输入。
	///   收缩后的图仍可继续 <c>&amp;</c> 别的区域，域只减不增（要恢复全域用 <c>FullDomain</c> 一类）。</para>
	/// </remarks>
	public static JlImage operator &(JlImage image, JlRegion region)
	{
		return image.ReduceDomain(region);
	}

	/// <summary>隐式转换：把图像当作它的定义域区域（转调 GetDomain），每次转换都产出新句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>凡需要 <see cref="JlRegion"/> 的地方都可以直接交一个 <see cref="JlImage"/>，
	///   编译器走本转换取回图像的<b>定义域</b>。新图（如 <c>new JlImage("byte",64,64)</c>）全域有效，取到的就是整幅矩形；
	///   被 <c>ReduceDomain</c> 收缩过的图取到的是收缩后的那块域。</para>
	///   <para><b>这不是像素阈值</b>想要"灰度落在某区间的像素集合"请用 <c>Threshold</c>；本转换只回答"哪些像素在域内"，
	///   与灰度值无关。把二者混为一谈是最常见的误用。</para>
	///   <para><b>句柄坑</b>每次隐式转换都调用一次 <c>GetDomain</c>、返回<b>新区域句柄</b>：
	///   赋给局部变量后可枚举、可释放；直接嵌在表达式里传参（如 <c>SomeOp(img, ...)</c> 处需要区域实参时）产生的
	///   临时区域要等终结器兜底回收。高频循环里请显式取域并复用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion dom = img;                             // 隐式走 GetDomain，得到全域矩形
	///   int n = dom.CountObj();
	///   </code>
	/// </remarks>
	public static implicit operator JlRegion(JlImage image)
	{
		return image.GetDomain();
	}

	/// <summary>
	///   按退化模型 psf 并从 noiseRegion 自估噪声做维纳复原，返回新图像句柄，输入图、psf、噪声区均不改写。
	/// </summary>
	/// <param name="psf">退化（空间域中）的脉冲响应（PSF）。</param>
	/// <param name="noiseRegion">用于噪声估计的区域。</param>
	/// <param name="maskWidth">滤波掩码宽度。默认值：3</param>
	/// <param name="maskHeight">滤波掩码高度。默认值：3</param>
	/// <returns>复原后的图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 75，返回<b>新图像句柄</b>（<c>LoadNew</c>），输入图、psf、noiseRegion 都不被改写。
	///   维纳滤波需要一个退化模型的冲激响应 <paramref name="psf"/> 和一块用于估计噪声功率的 <paramref name="noiseRegion"/>：
	///   噪声是从这块区域里量出来的，所以要选在"确定是纯噪声/平坦背景"的地方，选在目标上会把目标纹理当噪声。</para>
	///   <para><b>与 WienerFilter 的取舍</b>本重载自带噪声估计，只要给噪声区；<see cref="WienerFilter(JlImage,JlImage)"/>
	///   要你自己先算一幅平滑图当噪声来源。已知噪声区在哪时用它更方便。</para>
	///   <para><b>约束</b><paramref name="psf"/> 尺寸、<paramref name="maskWidth"/>/<paramref name="maskHeight"/> 的合法范围
	///   本层不校验 [待实测]；PSF 与图像不匹配时结果无意义但不报错。多通道图的处理方式未在本层体现 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage psf = new JlImage();
	///   psf.GenPsfMotion(64, 64, 20.0, 0, 3);                // 退化冲激响应
	///   using JlRegion noise = img.Threshold(0.0, 40.0);      // 估噪区（平坦背景）
	///   using JlImage restored = img.WienerFilterNi(psf, noise, 3, 3);
	///   </code>
	///   <para><b>资源与坑</b>结果是新句柄需释放；末尾对 <c>this</c>、psf、noiseRegion 都做 <c>GC.KeepAlive</c>，
	///   三者在原生调用期间都不能被回收。</para>
	/// </remarks>
	public JlImage WienerFilterNi(JlImage psf, JlRegion noiseRegion, int maskWidth, int maskHeight)
	{
		IntPtr proc = JlNativeApi.PreCall(75);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, psf);
		JlNativeApi.Store(proc, 3, noiseRegion);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(psf);
		GC.KeepAlive(noiseRegion);
		return obj;
	}

	/// <summary>
	///   用退化模型 psf 与一幅低通平滑版退化图（按"原图减平滑图"估噪声谱）做维纳复原，返回新图像句柄，输入不改写。
	/// </summary>
	/// <param name="psf">退化（空间域中）的脉冲响应（PSF）。</param>
	/// <param name="filteredImage">受损图像的平滑版本。</param>
	/// <returns>复原后的图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 76，同样返回新图像句柄。与 <see cref="WienerFilterNi(JlImage,JlRegion,int,int)"/>
	///   的区别在噪声来源：这里不传噪声区，而是传一幅<b>已平滑的退化图</b> <paramref name="filteredImage"/>，
	///   算子用"原图减平滑图"来估计噪声谱，因此 <paramref name="filteredImage"/> 必须是本图的低通版本，
	///   由 <c>MeanImage</c>/<c>GaussImage</c> 得到，且尺寸/通道与 <c>this</c> 一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage psf = new JlImage();
	///   psf.GenPsfMotion(64, 64, 20.0, 0, 3);
	///   using JlImage smooth = img.MeanImage(5, 5);          // 平滑版=噪声参考
	///   using JlImage restored = img.WienerFilter(psf, smooth);
	///   </code>
	///   <para><b>资源与坑</b>psf 与 filteredImage 只是被读取、不转交所有权；三者末尾都 <c>GC.KeepAlive</c>。</para>
	/// </remarks>
	public JlImage WienerFilter(JlImage psf, JlImage filteredImage)
	{
		IntPtr proc = JlNativeApi.PreCall(76);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, psf);
		JlNativeApi.Store(proc, 3, filteredImage);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(psf);
		GC.KeepAlive(filteredImage);
		return obj;
	}

	/// <summary>
	///   原地生成运动模糊的冲激响应：blurring 为拖尾像素长度、angle 为与 x 轴逆时针夹角（int），先 Dispose 再把 PSF 写进当前对象，无返回值。
	/// </summary>
	/// <param name="PSFwidth">脉冲响应图像的宽度。默认值：256</param>
	/// <param name="PSFheight">脉冲响应图像的高度。默认值：256</param>
	/// <param name="blurring">运动模糊程度。默认值：20.0</param>
	/// <param name="angle">运动方向与 x 轴之间的夹角（逆时针）。默认值：0</param>
	/// <param name="type">PSF 原型，即运动类型。默认值：3</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 77，是<b>原地生成器</b>：先 <c>Dispose()</c> 再 <c>Load</c> 把 PSF 写进当前对象，
	///   返回 <c>void</c>。所以要在一个可用的 <c>JlImage</c> 实例上调用（示例里对新建对象调用），不能拿返回值。</para>
	///   <para><b>参数</b><paramref name="blurring"/> 是运动拖尾的像素长度（double）；<paramref name="angle"/> 是运动方向
	///   与 x 轴逆时针夹角且本层是 <c>int</c>（<c>StoreI</c>），拿不到小数角度 [待实测]；<paramref name="type"/> 是运动原型编号
	///   （<c>int</c>），取值 0..N 各自对应哪种运动本层不体现 [待实测]。PSFwidth/PSFheight 决定 PSF 图像尺寸。</para>
	///   <para><b>与 SimulateMotion 的取舍</b>本算子只造冲激响应（给 <c>WienerFilter*</c> 当退化模型）；
	///   要"把一张清晰图做成模糊图"用 <see cref="SimulateMotion(double,int,int)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage psf = new JlImage();
	///   psf.GenPsfMotion(64, 64, 20.0, 0, 3);               // 结果写进 psf 本身
	///   </code>
	///   <para><b>资源与坑</b>会 Dispose 掉调用前对象持有的句柄再重建，别在还想要原内容时调用。</para>
	/// </remarks>
	public void GenPsfMotion(int PSFwidth, int PSFheight, double blurring, int angle, int type)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(77);
		JlNativeApi.StoreI(proc, 0, PSFwidth);
		JlNativeApi.StoreI(proc, 1, PSFheight);
		JlNativeApi.StoreD(proc, 2, blurring);
		JlNativeApi.StoreI(proc, 3, angle);
		JlNativeApi.StoreI(proc, 4, type);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   按 blurring（拖尾像素长度）/angle/type 在内部生成运动 PSF 并对输入卷积，返回模拟运动模糊后的新图像句柄，输入不变。
	/// </summary>
	/// <param name="blurring">模糊程度。默认值：20.0</param>
	/// <param name="angle">运动方向与 x 轴之间的夹角（逆时针）。默认值：0</param>
	/// <param name="type">运动模糊的脉冲响应。默认值：3</param>
	/// <returns>运动模糊后的图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 78，返回<b>新图像句柄</b>，输入不变。它在内部按 <paramref name="blurring"/>/
	///   <paramref name="angle"/>/<paramref name="type"/> 生成运动 PSF 并对输入做卷积，一步得到"被运动模糊后的图"，
	///   用于造测试样本或评估去模糊算法。</para>
	///   <para><b>与 GenPsfMotion 的取舍</b>只要模糊图 → 本方法；还想要那个 PSF 本身（喂给 <c>WienerFilterNi</c> 复原）
	///   → 用 <see cref="GenPsfMotion(int,int,double,int,int)"/>，两者的 blurring/angle/type 语义一致。</para>
	///   <para><b>参数</b><paramref name="blurring"/> 拖尾像素长度（double）；<paramref name="angle"/>/
	///   <paramref name="type"/> 与 GenPsfMotion 同为 <c>int</c>，含义/取值范围见该重载 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage blurred = img.SimulateMotion(20.0, 0, 3);
	///   </code>
	///   <para><b>资源与坑</b>结果新句柄需释放；末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlImage SimulateMotion(double blurring, int angle, int type)
	{
		IntPtr proc = JlNativeApi.PreCall(78);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, blurring);
		JlNativeApi.StoreI(proc, 1, angle);
		JlNativeApi.StoreI(proc, 2, type);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   原地生成均匀离焦的冲激响应：blurring 越大散焦圆盘越大，先 Dispose 再把 PSF 写进当前对象，无返回值。
	/// </summary>
	/// <param name="PSFwidth">结果图像的宽度。默认值：256</param>
	/// <param name="PSFheight">结果图像的高度。默认值：256</param>
	/// <param name="blurring">模糊程度。默认值：5.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 79，<b>原地生成器</b>（先 <c>Dispose()</c> 再 <c>Load</c>，<c>void</c>）：把均匀离焦的
	///   冲激响应写进当前对象。<paramref name="blurring"/> 是离焦程度（默认 5.0），越大散焦圆盘越大。</para>
	///   <para><b>与 GenPsfMotion 的取舍</b>散焦（离焦）用本算子，运动拖尾用 <see cref="GenPsfMotion(int,int,double,int,int)"/>；
	///   两者产物都是喂给 <c>WienerFilter*</c> 的退化模型。要直接得到模糊图用 <see cref="SimulateDefocus(double)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage psf = new JlImage();
	///   psf.GenPsfDefocus(64, 64, 5.0);                    // 结果写进 psf 本身
	///   </code>
	///   <para><b>资源与坑</b>会先释放调用前对象的句柄再重建。</para>
	/// </remarks>
	public void GenPsfDefocus(int PSFwidth, int PSFheight, double blurring)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(79);
		JlNativeApi.StoreI(proc, 0, PSFwidth);
		JlNativeApi.StoreI(proc, 1, PSFheight);
		JlNativeApi.StoreD(proc, 2, blurring);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   按 blurring 离焦程度对输入做均匀离焦模糊，返回新图像句柄，输入不变；blurring 与 GenPsfDefocus 的离焦程度同义。
	/// </summary>
	/// <param name="blurring">模糊程度。默认值：5.0</param>
	/// <returns>模糊后的图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 80，返回<b>新图像句柄</b>，输入不变：按 <paramref name="blurring"/> 对输入做均匀离焦模糊。
	///   <paramref name="blurring"/> 与 <see cref="GenPsfDefocus(int,int,double)"/> 的离焦程度同义。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage soft = img.SimulateDefocus(5.0);
	///   </code>
	///   <para><b>资源与坑</b>结果新句柄需释放；<c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlImage SimulateDefocus(double blurring)
	{
		IntPtr proc = JlNativeApi.PreCall(80);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, blurring);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}




























	/// <summary>
	///   对立体图对的已知特征点做灰值自动配对与 RANSAC 拟合本质矩阵（id 356）：返回 JlHomMat2D 新句柄，4 个 out 元组带回 9×9 协方差、误差均方根与匹配点索引；rotation 等参数走元组可多值。
	/// </summary>
	/// <param name="image2">输入图像 2。</param>
	/// <param name="rows1">图像 1 中特征点的行坐标。</param>
	/// <param name="cols1">图像 1 中特征点的列坐标。</param>
	/// <param name="rows2">图像 2 中特征点的行坐标。</param>
	/// <param name="cols2">图像 2 中特征点的列坐标。</param>
	/// <param name="camMat1">第 1 个相机的相机矩阵。</param>
	/// <param name="camMat2">第 2 个相机的相机矩阵。</param>
	/// <param name="grayMatchMethod">灰度值比较度量。默认值："ssd"</param>
	/// <param name="maskSize">灰度值掩码的大小。默认值：10</param>
	/// <param name="rowMove">对应点的平均行坐标偏移。默认值：0</param>
	/// <param name="colMove">对应点的平均列坐标偏移。默认值：0</param>
	/// <param name="rowTolerance">匹配搜索窗口的一半高度。默认值：200</param>
	/// <param name="colTolerance">匹配搜索窗口的一半宽度。默认值：200</param>
	/// <param name="rotation">右图像相对于左图像的相对位姿估计。默认值：0.0</param>
	/// <param name="matchThreshold">灰度值匹配的阈值。默认值：10</param>
	/// <param name="estimationMethod">计算本质矩阵及特殊相机位姿的算法。默认值："normalized_dlt"</param>
	/// <param name="distanceThreshold">点到其极线的最大偏差。默认值：1</param>
	/// <param name="randSeed">随机数生成器的种子。默认值：0</param>
	/// <param name="covEMat">本质矩阵的 9x9 协方差矩阵。</param>
	/// <param name="error">极线距离误差的均方根。</param>
	/// <param name="points1">图像 1 中已匹配输入点的索引。</param>
	/// <param name="points2">图像 2 中已匹配输入点的索引。</param>
	/// <returns>计算得到的本质矩阵。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 356，对立体图对求基础/本质矩阵：返回值以 <see cref="JlHomMat2D"/> 承载
	///   （<c>JlHomMat2D.LoadNew(proc,0,...)</c>），另有 4 个 <c>out</c> 元组：9×9 协方差 <paramref name="covEMat"/>、
	///   平均误差 <paramref name="error"/>、参与拟合的匹配点索引 <paramref name="points1"/>/<paramref name="points2"/>。</para>
	///   <para><b>它要特征点、不是全自动</b><paramref name="rows1"/>/<paramref name="cols1"/> 等是你先找好的角点/特征点坐标，
	///   本方法做的是"自动配对 + RANSAC 拟合"，不是"自动找点"。两图坐标系约定 (row,column) 且行在前。</para>
	///   <para><b>多值参数用元组</b>本重载 <paramref name="rotation"/>/<paramref name="matchThreshold"/>/
	///   <paramref name="distanceThreshold"/> 走 <c>Store</c>（可多值、逐通道），代价是每次固定/解固定；
	///   单值调参请见 <see cref="MatchEssentialMatrixRansac(JlImage,JlTuple,JlTuple,JlTuple,JlTuple,JlHomMat2D,JlHomMat2D,string,int,int,int,int,int,double,int,string,double,int,out JlTuple,out double,out JlTuple,out JlTuple)"/>。</para>
	///   <para><b>坑</b>相机矩阵 <paramref name="camMat1"/>/<paramref name="camMat2"/> 直接 <c>Store</c>，
	///   本层不校验其形状 [待实测]；RANSAC 有随机性，<paramref name="randSeed"/> 给 0 时结果逐次可能不同。
	///   <c>out</c> 实参必须写 <c>out</c>，不能预声明后按值传。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage img1 = new JlImage("byte", 64, 64);
	///   using JlImage img2 = new JlImage("byte", 64, 64);
	///   JlTuple r1 = new JlTuple(10.0, 20.0), c1 = new JlTuple(12.0, 22.0);
	///   JlTuple r2 = new JlTuple(10.0, 20.0), c2 = new JlTuple(14.0, 24.0);
	///   JlHomMat2D cam1 = new JlHomMat2D(), cam2 = new JlHomMat2D();
	///   JlHomMat2D eMat = img1.MatchEssentialMatrixRansac(img2, r1, c1, r2, c2, cam1, cam2,
	///       "ssd", 10, 0, 0, 200, 200, new JlTuple(0.0), new JlTuple(10), "normalized_dlt",
	///       new JlTuple(1), 0, out JlTuple cov, out JlTuple err, out JlTuple p1, out JlTuple p2);
	///   </code>
	///   <para><b>资源与坑</b>返回矩阵与各 <c>out</c> 元组都是新对象，各自释放；<c>this</c> 与 <paramref name="image2"/> 都 <c>GC.KeepAlive</c>。</para>
	/// </remarks>
	public JlHomMat2D MatchEssentialMatrixRansac(JlImage image2, JlTuple rows1, JlTuple cols1, JlTuple rows2, JlTuple cols2, JlHomMat2D camMat1, JlHomMat2D camMat2, string grayMatchMethod, int maskSize, int rowMove, int colMove, int rowTolerance, int colTolerance, JlTuple rotation, JlTuple matchThreshold, string estimationMethod, JlTuple distanceThreshold, int randSeed, out JlTuple covEMat, out JlTuple error, out JlTuple points1, out JlTuple points2)
	{
		IntPtr proc = JlNativeApi.PreCall(356);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, rows1);
		JlNativeApi.Store(proc, 1, cols1);
		JlNativeApi.Store(proc, 2, rows2);
		JlNativeApi.Store(proc, 3, cols2);
		JlNativeApi.Store(proc, 4, camMat1);
		JlNativeApi.Store(proc, 5, camMat2);
		JlNativeApi.StoreS(proc, 6, grayMatchMethod);
		JlNativeApi.StoreI(proc, 7, maskSize);
		JlNativeApi.StoreI(proc, 8, rowMove);
		JlNativeApi.StoreI(proc, 9, colMove);
		JlNativeApi.StoreI(proc, 10, rowTolerance);
		JlNativeApi.StoreI(proc, 11, colTolerance);
		JlNativeApi.Store(proc, 12, rotation);
		JlNativeApi.Store(proc, 13, matchThreshold);
		JlNativeApi.StoreS(proc, 14, estimationMethod);
		JlNativeApi.Store(proc, 15, distanceThreshold);
		JlNativeApi.StoreI(proc, 16, randSeed);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rows1);
		JlNativeApi.UnpinTuple(cols1);
		JlNativeApi.UnpinTuple(rows2);
		JlNativeApi.UnpinTuple(cols2);
		JlNativeApi.UnpinTuple(camMat1);
		JlNativeApi.UnpinTuple(camMat2);
		JlNativeApi.UnpinTuple(rotation);
		JlNativeApi.UnpinTuple(matchThreshold);
		JlNativeApi.UnpinTuple(distanceThreshold);
		err = JlHomMat2D.LoadNew(proc, 0, err, out var obj);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out covEMat);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out error);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out points1);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out points2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   同 id 356 的配对+RANSAC 本质矩阵估计：rotation/distanceThreshold/matchThreshold 直写单值免固定开销，返回 JlHomMat2D 新句柄，out 带回协方差元组、标量 error 与匹配点索引。
	/// </summary>
	/// <param name="image2">输入图像 2。</param>
	/// <param name="rows1">图像 1 中特征点的行坐标。</param>
	/// <param name="cols1">图像 1 中特征点的列坐标。</param>
	/// <param name="rows2">图像 2 中特征点的行坐标。</param>
	/// <param name="cols2">图像 2 中特征点的列坐标。</param>
	/// <param name="camMat1">第 1 个相机的相机矩阵。</param>
	/// <param name="camMat2">第 2 个相机的相机矩阵。</param>
	/// <param name="grayMatchMethod">灰度值比较度量。默认值："ssd"</param>
	/// <param name="maskSize">灰度值掩码的大小。默认值：10</param>
	/// <param name="rowMove">对应点的平均行坐标偏移。默认值：0</param>
	/// <param name="colMove">对应点的平均列坐标偏移。默认值：0</param>
	/// <param name="rowTolerance">匹配搜索窗口的一半高度。默认值：200</param>
	/// <param name="colTolerance">匹配搜索窗口的一半宽度。默认值：200</param>
	/// <param name="rotation">右图像相对于左图像的相对位姿估计。默认值：0.0</param>
	/// <param name="matchThreshold">灰度值匹配的阈值。默认值：10</param>
	/// <param name="estimationMethod">计算本质矩阵及特殊相机位姿的算法。默认值："normalized_dlt"</param>
	/// <param name="distanceThreshold">点到其极线的最大偏差。默认值：1</param>
	/// <param name="randSeed">随机数生成器的种子。默认值：0</param>
	/// <param name="covEMat">本质矩阵的 9x9 协方差矩阵。</param>
	/// <param name="error">极线距离误差的均方根。</param>
	/// <param name="points1">图像 1 中已匹配输入点的索引。</param>
	/// <param name="points2">图像 2 中已匹配输入点的索引。</param>
	/// <returns>计算得到的本质矩阵。</returns>
	/// <remarks>
	///   <para>算法、特征点前提与随机性见 <see cref="MatchEssentialMatrixRansac(JlImage,JlTuple,JlTuple,JlTuple,JlTuple,JlHomMat2D,JlHomMat2D,string,int,int,int,int,int,JlTuple,JlTuple,string,JlTuple,int,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>：同一原生 id 356，区域/矩阵输出路径完全相同。</para>
	///   <para><b>实际差异</b><paramref name="rotation"/>/<paramref name="distanceThreshold"/> 经 <c>StoreD</c>、
	///   <paramref name="matchThreshold"/> 经 <c>StoreI</c> 直写单值，省掉固定/解固定；<paramref name="error"/> 用 <c>LoadD</c>
	///   读第一个值（本就是标量 RMS，无损失），而 <paramref name="covEMat"/> 仍是 <c>JlTuple</c>（9×9 协方差不被裁）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage img1 = new JlImage("byte", 64, 64);
	///   using JlImage img2 = new JlImage("byte", 64, 64);
	///   JlTuple r1 = new JlTuple(10.0, 20.0), c1 = new JlTuple(12.0, 22.0);
	///   JlTuple r2 = new JlTuple(10.0, 20.0), c2 = new JlTuple(14.0, 24.0);
	///   JlHomMat2D cam1 = new JlHomMat2D(), cam2 = new JlHomMat2D();
	///   JlHomMat2D eMat = img1.MatchEssentialMatrixRansac(img2, r1, c1, r2, c2, cam1, cam2,
	///       "ssd", 10, 0, 0, 200, 200, 0.0, 10, "normalized_dlt", 1.0, 0,
	///       out JlTuple cov, out double err, out JlTuple p1, out JlTuple p2);
	///   </code>
	/// </remarks>
	public JlHomMat2D MatchEssentialMatrixRansac(JlImage image2, JlTuple rows1, JlTuple cols1, JlTuple rows2, JlTuple cols2, JlHomMat2D camMat1, JlHomMat2D camMat2, string grayMatchMethod, int maskSize, int rowMove, int colMove, int rowTolerance, int colTolerance, double rotation, int matchThreshold, string estimationMethod, double distanceThreshold, int randSeed, out JlTuple covEMat, out double error, out JlTuple points1, out JlTuple points2)
	{
		IntPtr proc = JlNativeApi.PreCall(356);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, rows1);
		JlNativeApi.Store(proc, 1, cols1);
		JlNativeApi.Store(proc, 2, rows2);
		JlNativeApi.Store(proc, 3, cols2);
		JlNativeApi.Store(proc, 4, camMat1);
		JlNativeApi.Store(proc, 5, camMat2);
		JlNativeApi.StoreS(proc, 6, grayMatchMethod);
		JlNativeApi.StoreI(proc, 7, maskSize);
		JlNativeApi.StoreI(proc, 8, rowMove);
		JlNativeApi.StoreI(proc, 9, colMove);
		JlNativeApi.StoreI(proc, 10, rowTolerance);
		JlNativeApi.StoreI(proc, 11, colTolerance);
		JlNativeApi.StoreD(proc, 12, rotation);
		JlNativeApi.StoreI(proc, 13, matchThreshold);
		JlNativeApi.StoreS(proc, 14, estimationMethod);
		JlNativeApi.StoreD(proc, 15, distanceThreshold);
		JlNativeApi.StoreI(proc, 16, randSeed);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rows1);
		JlNativeApi.UnpinTuple(cols1);
		JlNativeApi.UnpinTuple(rows2);
		JlNativeApi.UnpinTuple(cols2);
		JlNativeApi.UnpinTuple(camMat1);
		JlNativeApi.UnpinTuple(camMat2);
		err = JlHomMat2D.LoadNew(proc, 0, err, out var obj);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out covEMat);
		err = JlNativeApi.LoadD(proc, 2, err, out error);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out points1);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out points2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}














	/// <summary>
	///   把输入当作高度场，按 slant/tilt（角度制）与 albedo/ambient 正向合成明暗图，光照参数走元组可逐区域/逐像素不同，返回新图像句柄，高度场不变。
	/// </summary>
	/// <param name="slant">光源与 z 轴正方向之间的夹角（单位：度）。默认值：0.0</param>
	/// <param name="tilt">光源与 x 轴的夹角投影到 xy 平面后的大小（单位：度）。默认值：0.0</param>
	/// <param name="albedo">表面反射的光量。默认值：1.0</param>
	/// <param name="ambient">环境光强度。默认值：0.0</param>
	/// <param name="shadows">是否计算阴影？默认值："false"</param>
	/// <returns>明暗渲染后的图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 387，输入被当作<b>高度场</b>（每像素是表面高度，通常 <c>real</c> 图），
	///   按给定的光源方向与反射参数合成一幅明暗图，返回<b>新图像句柄</b>，高度场本身不变。</para>
	///   <para><b>参数单位</b><paramref name="slant"/> 光源与 +z 轴夹角、<paramref name="tilt"/> 光源在 xy 投影与 x 轴夹角，均为<b>角度</b>（非弧度）；
	///   <paramref name="albedo"/> 反射率、<paramref name="ambient"/> 环境光。要正向渲染明暗图用它；
	///   反过来从明暗图估 slant/albedo 用 <c>EstimateSlAl*</c> 一族。</para>
	///   <para><b>多值参数</b>本重载四个参数走 <c>Store</c>（可逐区域/逐像素不同光照，代价是固定/解固定）；
	///   单一光照请见 <see cref="ShadeHeightField(double,double,double,double,string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage hf = new JlImage("real", 64, 64);
	///   using JlImage shaded = hf.ShadeHeightField(new JlTuple(45.0), new JlTuple(45.0),
	///       new JlTuple(1.0), new JlTuple(0.0), "false");
	///   </code>
	///   <para><b>资源与坑</b><paramref name="shadows"/> 是字符串，取值不校验；结果新句柄需释放。</para>
	/// </remarks>
	public JlImage ShadeHeightField(JlTuple slant, JlTuple tilt, JlTuple albedo, JlTuple ambient, string shadows)
	{
		IntPtr proc = JlNativeApi.PreCall(387);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, slant);
		JlNativeApi.Store(proc, 1, tilt);
		JlNativeApi.Store(proc, 2, albedo);
		JlNativeApi.Store(proc, 3, ambient);
		JlNativeApi.StoreS(proc, 4, shadows);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(slant);
		JlNativeApi.UnpinTuple(tilt);
		JlNativeApi.UnpinTuple(albedo);
		JlNativeApi.UnpinTuple(ambient);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   同 id 387：把高度场按全图一套光照（slant/tilt 角度制、albedo/ambient）渲染成明暗图，参数直写单值免固定开销，返回新图像句柄。
	/// </summary>
	/// <param name="slant">光源与 z 轴正方向之间的夹角（单位：度）。默认值：0.0</param>
	/// <param name="tilt">光源与 x 轴的夹角投影到 xy 平面后的大小（单位：度）。默认值：0.0</param>
	/// <param name="albedo">表面反射的光量。默认值：1.0</param>
	/// <param name="ambient">环境光强度。默认值：0.0</param>
	/// <param name="shadows">是否计算阴影？默认值："false"</param>
	/// <returns>明暗渲染后的图像。</returns>
	/// <remarks>
	///   <para>算法、参数单位与正向/逆向用途见 <see cref="ShadeHeightField(JlTuple,JlTuple,JlTuple,JlTuple,string)"/>：
	///   同一原生 id 387。本重载四个光照参数经 <c>StoreD</c> 直写单值，全图一套光照，无固定/解固定，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage hf = new JlImage("real", 64, 64);
	///   using JlImage shaded = hf.ShadeHeightField(45.0, 45.0, 1.0, 0.0, "false");
	///   </code>
	/// </remarks>
	public JlImage ShadeHeightField(double slant, double tilt, double albedo, double ambient, string shadows)
	{
		IntPtr proc = JlNativeApi.PreCall(387);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, slant);
		JlNativeApi.StoreD(proc, 1, tilt);
		JlNativeApi.StoreD(proc, 2, albedo);
		JlNativeApi.StoreD(proc, 3, ambient);
		JlNativeApi.StoreS(proc, 4, shadows);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   从明暗图反估表面反射率与环境光（id 388）：返回 albedo 元组，ambient 经 out 带回，均可含多值，不产生图像输出。
	/// </summary>
	/// <param name="ambient">环境光强度。</param>
	/// <returns>表面反射的光量。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 388，从明暗图反估反射率与环境光：返回值是 <b>albedo</b>（反射率）元组，
	///   <paramref name="ambient"/>（环境光）经 <c>out</c> 带回，两者都是 <c>JlTuple</c>（<c>LoadNew</c> 读，可含多值）。
	///   输入被当作带光照信息的图，本方法不产生图像输出。</para>
	///   <para><b>与相邻算子的取舍</b>估"倾斜角+反射率"用 <c>EstimateSlAlZc</c>/<c>EstimateSlAlLr</c>；
	///   估光源方位角用 <c>EstimateTilt*</c>；本方法专估 albedo 与 ambient 两个反射量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   JlTuple albedo = img.EstimateAlAm(out JlTuple ambient);
	///   int n = albedo.Length;
	///   </code>
	///   <para><b>资源与坑</b>返回元组与 <paramref name="ambient"/> 都是新对象需释放；<c>out</c> 必须写 <c>out</c>。</para>
	/// </remarks>
	public JlTuple EstimateAlAm(out JlTuple ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(388);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out ambient);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   同 id 388 的 albedo/ambient 反估：LoadD 对返回值与 ambient 各只取第一个值，成组结果除首个外静默丢弃，要全量必须用元组版。
	/// </summary>
	/// <param name="ambient">环境光强度。</param>
	/// <returns>表面反射的光量。</returns>
	/// <remarks>
	///   <para>反估什么量、与 <c>EstimateSlAl*</c> 的取舍见 <see cref="EstimateAlAm(out JlTuple)"/>：同一原生 id 388。</para>
	///   <para><b>实际差异（重要）</b>本重载用 <c>LoadD</c> 读结果，<b>只取第一个值</b>：当算子对多区域/多通道给出成组 albedo、
	///   ambient 时，除首个外的值会被静默丢弃。要拿全量必须用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   double albedo = img.EstimateAlAm(out double ambient);   // 只有第一个值
	///   </code>
	/// </remarks>
	public double EstimateAlAm(out double ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(388);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out ambient);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   以 Zc 法（id 389）从明暗图反估光源倾斜角与表面反射率：返回 slant 元组（与 +z 轴夹角，角度制），albedo 经 out 带回。
	/// </summary>
	/// <param name="albedo">表面反射的光量。</param>
	/// <returns>光源与 z 轴正方向之间的夹角（单位：度）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 389，反估光源倾斜角（slant，光源与 +z 轴夹角，单位角度）与表面反射率：
	///   返回 <b>slant</b> 元组，<paramref name="albedo"/> 经 <c>out</c> 带回，均为 <c>JlTuple</c>。是 <c>ShadeHeightField</c>
	///   的反问题之一（正向渲染 ↔ 逆向估计）。</para>
	///   <para><b>Zc 与 Lr 之别</b>本方法与 <see cref="EstimateSlAlLr(out JlTuple)"/> 是同题不同法（原生 id 389 vs 390），
	///   两者的算法差异与精度差异本层无从体现 [待实测]，一般按同一份数据分别试、看拟合误差再选。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   JlTuple slant = img.EstimateSlAlZc(out JlTuple albedo);
	///   </code>
	///   <para><b>资源与坑</b>返回元组与 <paramref name="albedo"/> 需各自释放；<c>out</c> 必须写 <c>out</c>。</para>
	/// </remarks>
	public JlTuple EstimateSlAlZc(out JlTuple albedo)
	{
		IntPtr proc = JlNativeApi.PreCall(389);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out albedo);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   同 id 389 的 Zc 法反估：LoadD 对 slant 与 albedo 各只取第一个值，多区域成组结果会被裁掉，要全量请用元组版。
	/// </summary>
	/// <param name="albedo">表面反射的光量。</param>
	/// <returns>光源与 z 轴正方向之间的夹角（单位：度）。</returns>
	/// <remarks>
	///   <para>反估的量、与 Lr 法的关系见 <see cref="EstimateSlAlZc(out JlTuple)"/>：同一原生 id 389。</para>
	///   <para><b>实际差异（重要）</b>用 <c>LoadD</c> 读 slant 与 <paramref name="albedo"/>，<b>各只取第一个值</b>，
	///   多区域成组结果会被裁掉；要全量请用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   double slant = img.EstimateSlAlZc(out double albedo);
	///   </code>
	/// </remarks>
	public double EstimateSlAlZc(out double albedo)
	{
		IntPtr proc = JlNativeApi.PreCall(389);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out albedo);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   以 Lr 法（id 390）从明暗图反估光源倾斜角与表面反射率：返回 slant 元组（角度制）、albedo 经 out 带回，均 LoadNew(DOUBLE) 可含多值。
	/// </summary>
	/// <param name="albedo">表面反射的光量。</param>
	/// <returns>光源与 z 轴正方向之间的夹角（单位：度）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 390，与 <see cref="EstimateSlAlZc(out JlTuple)"/> 同题异法（Lr 拟合）：从明暗图
	///   反估光源倾斜角与表面反射率。返回 <b>slant</b>（光源与 +z 轴夹角，<b>角度</b>制）元组，
	///   <paramref name="albedo"/> 经 <c>out</c> 带回；两者都由 <c>JlTuple.LoadNew(DOUBLE)</c> 读出、可含多值。</para>
	///   <para><b>Zc 与 Lr 之别</b>两法对应原生 id 389 与 390，算法与精度差异本层无从体现 [待实测]；
	///   常规做法是同一份数据两法各跑一遍、比拟合误差再定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   JlTuple slant = img.EstimateSlAlLr(out JlTuple albedo);
	///   int n = slant.Length;
	///   </code>
	///   <para><b>资源与坑</b>返回元组与 <paramref name="albedo"/> 都是新对象，需各自释放；<c>out</c> 实参必须写 <c>out</c>。</para>
	/// </remarks>
	public JlTuple EstimateSlAlLr(out JlTuple albedo)
	{
		IntPtr proc = JlNativeApi.PreCall(390);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out albedo);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   同 id 390 的 Lr 法反估：LoadD 对 slant 与 albedo 各只取第一个值，成组结果除首个外全部静默丢弃，确定只要标量时才用本版。
	/// </summary>
	/// <param name="albedo">表面反射的光量。</param>
	/// <returns>光源与 z 轴正方向之间的夹角（单位：度）。</returns>
	/// <remarks>
	///   <para>反估的量、单位与 Zc/Lr 两法的关系见 <see cref="EstimateSlAlLr(out JlTuple)"/>：同一原生 id 390。</para>
	///   <para><b>实际差异（重要）</b>本重载用 <c>LoadD</c> 读 slant 与 <paramref name="albedo"/>，<b>各只取第一个值</b>：
	///   算子对多区域/多通道给成组结果时，除首个外全部静默丢弃。确定只要一个标量时用本版，否则必须走元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   double slant = img.EstimateSlAlLr(out double albedo);   // 只有第一个值
	///   </code>
	/// </remarks>
	public double EstimateSlAlLr(out double albedo)
	{
		IntPtr proc = JlNativeApi.PreCall(390);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out albedo);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   从明暗图反估光源方位角 tilt（投影到 xy 平面后与 x 轴夹角，角度制，可直接回填 ShadeHeightField），返回可逐区域多值的 JlTuple 新对象。
	/// </summary>
	/// <returns>光源与 x 轴的夹角投影到 xy 平面后的大小（单位：度）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 391，从明暗图反估光源<b>方位角 tilt</b>：光源投影到 xy 平面后与 x 轴的夹角，
	///   单位<b>角度</b>（与 <c>ShadeHeightField</c> 的 tilt 参数量纲一致，可直接回填正向渲染）。
	///   结果经 <c>JlTuple.LoadNew(DOUBLE)</c> 返回，可含多值（逐区域各估一个）。</para>
	///   <para><b>与 Lr 版的取舍</b><see cref="EstimateTiltLr"/> 是同题另一法（id 392），差异本层无从体现 [待实测]；
	///   光源<b>俯角</b>（slant）不在本算子输出里，要 slant+albedo 用 <c>EstimateSlAl*</c> 族。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   JlTuple tilt = img.EstimateTiltZc();
	///   int n = tilt.Length;
	///   </code>
	///   <para><b>资源与坑</b>返回元组是新对象需释放（纯数值元组 <c>Dispose</c> 无句柄可清，仍建议与 <c>EstimateSlAl*</c>
	///   的元组输出一视同仁处理）。</para>
	/// </remarks>
	public JlTuple EstimateTiltZc()
	{
		IntPtr proc = JlNativeApi.PreCall(391);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   同题另一法（id 392）反估光源方位角 tilt（光源投影与 x 轴夹角，角度制），经 LoadNew(DOUBLE) 返回可含多值的元组。
	/// </summary>
	/// <returns>光源与 x 轴的夹角投影到 xy 平面后的大小（单位：度）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 392：反估光源方位角 tilt（光源投影与 x 轴夹角，<b>角度</b>制），
	///   经 <c>JlTuple.LoadNew(DOUBLE)</c> 返回、可含多值。量纲与用途同 <see cref="EstimateTiltZc"/>，
	///   两者是同题不同法 [待实测：算法与精度差异]，同一份数据各跑一遍择优即可。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("real", 64, 64);
	///   JlTuple tilt = img.EstimateTiltLr();
	///   int n = tilt.Length;
	///   </code>
	/// </remarks>
	public JlTuple EstimateTiltLr()
	{
		IntPtr proc = JlNativeApi.PreCall(392);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   把携带 dX、dY 两个梯度分量的图按指定重建方法积分回高度场，返回新图像句柄，输入不变；手里是明暗灰度图时应走 Sfs* 族而非本方法。
	/// </summary>
	/// <param name="reconstructionMethod">重建方法的类型。默认值："poisson"</param>
	/// <param name="genParamName">通用参数的名称。默认值：[]</param>
	/// <param name="genParamValue">通用参数的值。默认值：[]</param>
	/// <returns>重建的高度场。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 393：把<b>梯度场积分回高度场</b>——输入是一幅携带 dX、dY 两个梯度分量的图
	///   （两分量各占一个通道，可用 <c>Compose2</c> 把两幅单通道梯度图并成一幅 [待实测：通道与分量的对应序]），
	///   输出是重建出的高度场<b>新图像句柄</b>，输入不变。</para>
	///   <para><b>参数</b><paramref name="reconstructionMethod"/> 字符串直传（<c>StoreS</c>），本层不校验取值，
	///   默认 "poisson"；其它合法方法名及其对边界/精度矩阵的影响由原生决定 [待实测]。
	///   <paramref name="genParamName"/>/<paramref name="genParamValue"/> 是名值成对的通用参数（走 <c>Store</c> 固定），
	///   不用时传<b>两个空元组</b>，名值个数不等时的行为未在本层体现 [待实测]。</para>
	///   <para><b>与 Sfs* 族的取舍</b>手里是<b>梯度图</b>用本方法积分高度；手里是明暗灰度图则直接走 <c>SfsPentland</c>
	///   等灰度重建族，不经过本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage dx = new JlImage("real", 64, 64);
	///   JlImage dy = new JlImage("real", 64, 64);
	///   using JlImage grad = dx.Compose2(dy);                  // 两通道梯度图
	///   using JlImage hf = grad.ReconstructHeightFieldFromGradient("poisson", new JlTuple(), new JlTuple());
	///   </code>
	///   <para><b>资源与坑</b>结果新句柄需释放；重建高度只到"差一个常数基面"的精度，绝对高度不可信 [待实测]。</para>
	/// </remarks>
	public JlImage ReconstructHeightFieldFromGradient(string reconstructionMethod, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(393);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, reconstructionMethod);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.Store(proc, 2, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}


	/// <summary>
	///   用 Pentland 法（id 395）从明暗灰度图重建高度场：给定光源方向 slant/tilt（角度制）与 albedo/ambient，光照参数走元组可多值，返回新图像句柄，输入不变。
	/// </summary>
	/// <param name="slant">光源与 z 轴正方向之间的夹角（单位：度）。默认值：45.0</param>
	/// <param name="tilt">光源与 x 轴的夹角投影到 xy 平面后的大小（单位：度）。默认值：45.0</param>
	/// <param name="albedo">表面反射的光量。默认值：1.0</param>
	/// <param name="ambient">环境光强度。默认值：0.0</param>
	/// <returns>重建的高度场。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 395（Pentland 法 shape-from-shading）：已知光照方向与反射参数时，从单幅明暗
	///   灰度图重建表面形状，输出<b>高度场新图像句柄</b>，输入不变。<paramref name="slant"/>/<paramref name="tilt"/>
	///   描述光源方向（<b>角度</b>制，定义与 <c>ShadeHeightField</c> 的入参同一套，可把正向渲染用的值原样填回来）；
	///   <paramref name="albedo"/>/<paramref name="ambient"/> 是反射率与环境光。</para>
	///   <para><b>前提</b>光源方向必须先知道（或用 <c>EstimateTilt*</c>/<c>EstimateSlAl*</c> 反估出来再填）；
	///   方向给错时结果仍是"一张高度场"，只是形状是假的，不会报错。灰度图建议先平滑，高光/阴影饱和区不可恢复 [待实测]。</para>
	///   <para><b>三法取舍</b>本方法对应 Pentland 算法；同为灰度重建还有 <see cref="SfsOrigLr(JlTuple,JlTuple,JlTuple,JlTuple)"/>
	///   （id 396）与 <see cref="SfsModLr(JlTuple,JlTuple,JlTuple,JlTuple)"/>（id 397），精度/收敛差异本层无从体现 [待实测]，
	///   按同一组光照参数三法各跑、看高度场噪声水平择优。</para>
	///   <para><b>多值参数</b>本重载四个参数走 <c>Store</c> 钉元组（可传多值，语义由原生决定 [待实测]）；
	///   全图一套光照请用标量版 <see cref="SfsPentland(double,double,double,double)"/>，免固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage shading = new JlImage("byte", 64, 64);
	///   using JlImage hf = shading.SfsPentland(new JlTuple(45.0), new JlTuple(45.0),
	///       new JlTuple(1.0), new JlTuple(0.0));            // 灰度图 → 高度场
	///   </code>
	///   <para><b>资源与坑</b>结果新句柄需释放；末尾 <c>GC.KeepAlive(this)</c>，输入在原生调用结束前不得回收。</para>
	/// </remarks>
	public JlImage SfsPentland(JlTuple slant, JlTuple tilt, JlTuple albedo, JlTuple ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(395);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, slant);
		JlNativeApi.Store(proc, 1, tilt);
		JlNativeApi.Store(proc, 2, albedo);
		JlNativeApi.Store(proc, 3, ambient);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(slant);
		JlNativeApi.UnpinTuple(tilt);
		JlNativeApi.UnpinTuple(albedo);
		JlNativeApi.UnpinTuple(ambient);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   同 id 395 的 Pentland 灰度重建：四个光照参数直写单值、全图一套光照免固定开销，返回高度场新图像句柄。
	/// </summary>
	/// <param name="slant">光源与 z 轴正方向之间的夹角（单位：度）。默认值：45.0</param>
	/// <param name="tilt">光源与 x 轴的夹角投影到 xy 平面后的大小（单位：度）。默认值：45.0</param>
	/// <param name="albedo">表面反射的光量。默认值：1.0</param>
	/// <param name="ambient">环境光强度。默认值：0.0</param>
	/// <returns>重建的高度场。</returns>
	/// <remarks>
	///   <para>算法、光照参数量纲与三法取舍见
	///   <see cref="SfsPentland(JlTuple,JlTuple,JlTuple,JlTuple)"/>：同一原生 id 395，输出同为高度场新句柄。</para>
	///   <para><b>实际差异</b>四个参数经 <c>StoreD</c> 直写单值，全图一套光照、无固定/解固定开销，是常规写法；
	///   想给多值参数才需要元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage shading = new JlImage("byte", 64, 64);
	///   using JlImage hf = shading.SfsPentland(45.0, 45.0, 1.0, 0.0);
	///   </code>
	/// </remarks>
	public JlImage SfsPentland(double slant, double tilt, double albedo, double ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(395);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, slant);
		JlNativeApi.StoreD(proc, 1, tilt);
		JlNativeApi.StoreD(proc, 2, albedo);
		JlNativeApi.StoreD(proc, 3, ambient);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用 Orig-Lr 法（id 396）从明暗灰度图重建高度场，光源方向按角度制（slant/tilt）与 albedo/ambient 给定，返回新图像句柄，输入不变。
	/// </summary>
	/// <param name="slant">光源与 z 轴正方向之间的夹角（单位：度）。默认值：45.0</param>
	/// <param name="tilt">光源与 x 轴的夹角投影到 xy 平面后的大小（单位：度）。默认值：45.0</param>
	/// <param name="albedo">表面反射的光量。默认值：1.0</param>
	/// <param name="ambient">环境光强度。默认值：0.0</param>
	/// <returns>重建的高度场。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 396：与 <see cref="SfsPentland(JlTuple,JlTuple,JlTuple,JlTuple)"/> 同族
	///   （Orig-Lr 算法），从明暗灰度图重建高度场，返回<b>新图像句柄</b>、输入不变。
	///   <paramref name="slant"/>/<paramref name="tilt"/> 为光源方向（<b>角度</b>制，与 <c>ShadeHeightField</c> 同套定义），
	///   <paramref name="albedo"/>/<paramref name="ambient"/> 为反射率与环境光；参数量纲与"方向错则形状假、不报错"
	///   的前提同 Pentland 版。</para>
	///   <para><b>三法取舍</b>与 Pentland（id 395）、<see cref="SfsModLr(JlTuple,JlTuple,JlTuple,JlTuple)"/>（id 397）
	///   的精度/收敛差异本层无从体现 [待实测]，同参各跑对比噪声再选。</para>
	///   <para><b>多值参数</b>本重载走 <c>Store</c> 钉元组；单套光照请用标量版免固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage shading = new JlImage("byte", 64, 64);
	///   using JlImage hf = shading.SfsOrigLr(new JlTuple(45.0), new JlTuple(45.0),
	///       new JlTuple(1.0), new JlTuple(0.0));
	///   </code>
	///   <para><b>资源与坑</b>结果新句柄需释放；<c>GC.KeepAlive(this)</c> 保输入。</para>
	/// </remarks>
	public JlImage SfsOrigLr(JlTuple slant, JlTuple tilt, JlTuple albedo, JlTuple ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(396);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, slant);
		JlNativeApi.Store(proc, 1, tilt);
		JlNativeApi.Store(proc, 2, albedo);
		JlNativeApi.Store(proc, 3, ambient);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(slant);
		JlNativeApi.UnpinTuple(tilt);
		JlNativeApi.UnpinTuple(albedo);
		JlNativeApi.UnpinTuple(ambient);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   同 id 396 的 Orig-Lr 灰度重建：光照参数直写单值、全图一套光照免固定开销，返回高度场新图像句柄。
	/// </summary>
	/// <param name="slant">光源与 z 轴正方向之间的夹角（单位：度）。默认值：45.0</param>
	/// <param name="tilt">光源与 x 轴的夹角投影到 xy 平面后的大小（单位：度）。默认值：45.0</param>
	/// <param name="albedo">表面反射的光量。默认值：1.0</param>
	/// <param name="ambient">环境光强度。默认值：0.0</param>
	/// <returns>重建的高度场。</returns>
	/// <remarks>
	///   <para>算法、光照参数量纲与三法取舍见
	///   <see cref="SfsOrigLr(JlTuple,JlTuple,JlTuple,JlTuple)"/>：同一原生 id 396，输出同为高度场新句柄。</para>
	///   <para><b>实际差异</b>四个参数经 <c>StoreD</c> 直写单值，全图一套光照、无固定/解固定开销，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage shading = new JlImage("byte", 64, 64);
	///   using JlImage hf = shading.SfsOrigLr(45.0, 45.0, 1.0, 0.0);
	///   </code>
	/// </remarks>
	public JlImage SfsOrigLr(double slant, double tilt, double albedo, double ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(396);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, slant);
		JlNativeApi.StoreD(proc, 1, tilt);
		JlNativeApi.StoreD(proc, 2, albedo);
		JlNativeApi.StoreD(proc, 3, ambient);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用 Mod-Lr 法（id 397）从明暗灰度图重建高度场，光源方向按角度制（slant/tilt）与 albedo/ambient 给定，返回新图像句柄，输入不变。
	/// </summary>
	/// <param name="slant">光源与 z 轴正方向之间的夹角（单位：度）。默认值：45.0</param>
	/// <param name="tilt">光源与 x 轴的夹角投影到 xy 平面后的大小（单位：度）。默认值：45.0</param>
	/// <param name="albedo">表面反射的光量。默认值：1.0</param>
	/// <param name="ambient">环境光强度。默认值：0.0</param>
	/// <returns>重建的高度场。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 397（Mod-Lr 算法，Lr 族的变体）：从明暗灰度图重建高度场，返回
	///   <b>新图像句柄</b>、输入不变。<paramref name="slant"/>/<paramref name="tilt"/> 为光源方向（<b>角度</b>制，
	///   与 <c>ShadeHeightField</c> 同套定义），<paramref name="albedo"/>/<paramref name="ambient"/> 为反射率与环境光；
	///   参数给错只出假形状、不报错，前提同 <see cref="SfsPentland(JlTuple,JlTuple,JlTuple,JlTuple)"/>。</para>
	///   <para><b>三法取舍</b>Pentland（id 395）、Orig-Lr（id 396）与本方法（id 397）的精度/收敛差异
	///   本层无从体现 [待实测]，同参各跑对比噪声再选。</para>
	///   <para><b>多值参数</b>本重载走 <c>Store</c> 钉元组；单套光照用标量版免固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage shading = new JlImage("byte", 64, 64);
	///   using JlImage hf = shading.SfsModLr(new JlTuple(45.0), new JlTuple(45.0),
	///       new JlTuple(1.0), new JlTuple(0.0));
	///   </code>
	///   <para><b>资源与坑</b>结果新句柄需释放；<c>GC.KeepAlive(this)</c> 保输入。</para>
	/// </remarks>
	public JlImage SfsModLr(JlTuple slant, JlTuple tilt, JlTuple albedo, JlTuple ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(397);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, slant);
		JlNativeApi.Store(proc, 1, tilt);
		JlNativeApi.Store(proc, 2, albedo);
		JlNativeApi.Store(proc, 3, ambient);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(slant);
		JlNativeApi.UnpinTuple(tilt);
		JlNativeApi.UnpinTuple(albedo);
		JlNativeApi.UnpinTuple(ambient);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   同 id 397 的 Mod-Lr 灰度重建：光照参数直写单值、全图一套光照免固定开销，返回高度场新图像句柄。
	/// </summary>
	/// <param name="slant">光源与 z 轴正方向之间的夹角（单位：度）。默认值：45.0</param>
	/// <param name="tilt">光源与 x 轴的夹角投影到 xy 平面后的大小（单位：度）。默认值：45.0</param>
	/// <param name="albedo">表面反射的光量。默认值：1.0</param>
	/// <param name="ambient">环境光强度。默认值：0.0</param>
	/// <returns>重建的高度场。</returns>
	/// <remarks>
	///   <para>算法、光照参数量纲与三法取舍见
	///   <see cref="SfsModLr(JlTuple,JlTuple,JlTuple,JlTuple)"/>：同一原生 id 397，输出同为高度场新句柄。</para>
	///   <para><b>实际差异</b>四个参数经 <c>StoreD</c> 直写单值，全图一套光照、无固定/解固定开销，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage shading = new JlImage("byte", 64, 64);
	///   using JlImage hf = shading.SfsModLr(45.0, 45.0, 1.0, 0.0);
	///   </code>
	/// </remarks>
	public JlImage SfsModLr(double slant, double tilt, double albedo, double ambient)
	{
		IntPtr proc = JlNativeApi.PreCall(397);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, slant);
		JlNativeApi.StoreD(proc, 1, tilt);
		JlNativeApi.StoreD(proc, 2, albedo);
		JlNativeApi.StoreD(proc, 3, ambient);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}















	/// <summary>
	///   有监督二维分类（id 431）：像素由本图与 imageRow 同位置灰度定出特征空间中的点，落在特征空间区域 featureSpace 内者归为该类，返回 JlRegion 新句柄，两图不改写。
	/// </summary>
	/// <param name="imageRow">输入图像（第二个通道）。</param>
	/// <param name="featureSpace">定义特征空间的区域。</param>
	/// <returns>分类得到的区域。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 431：<b>有监督</b>二维分类——每个像素由 (本图灰度, <paramref name="imageRow"/> 同位置灰度)
	///   两个特征定出二维特征空间中的一个点，落在 <paramref name="featureSpace"/> 内的像素被归为该类。
	///   返回 <see cref="JlRegion"/> 对象数组（新句柄），两幅输入图都不改写。</para>
	///   <para><b>featureSpace 怎么给</b>它不是图像上的 ROI，而是画在<b>特征空间</b>（两轴是两路灰度值）里的区域：
	///   通常一块子区域对应一个输出类；子区域块数与返回对象数组元素的对应顺序本层不体现 [待实测]，
	///   别按"第一块=背景"想当然，用 <c>CountObj()</c> 核对再取。</para>
	///   <para><b>与 Class2dimUnsup 的取舍</b>已知各类在特征空间的落点（灰度+梯度幅值、双波段等）用本方法；
	///   不知道类别、想让算子自己聚类才用 <see cref="Class2dimUnsup(JlImage,int,int)"/>。</para>
	///   <para><b>前提</b>两幅图需同尺寸且各为单通道 [待实测：本层不校验，<paramref name="imageRow"/> 形参名即"第二通道图"]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage gray = new JlImage("byte", 64, 64);
	///   using JlImage amp = gray.SobelAmp("sum", 3);                 // 第二特征：边缘幅值
	///   using JlRegion fs = new JlRegion(0.0, 128.0, 255.0, 255.0); // 特征空间中划出高幅值域
	///   using JlRegion cls = gray.Class2dimSup(amp, fs);             // this 是第一特征图
	///   int n = cls.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>结果需释放；<c>this</c>、<paramref name="imageRow"/>、<paramref name="featureSpace"/>
	///   三者末尾均 <c>GC.KeepAlive</c>，原生调用结束前不得回收。</para>
	/// </remarks>
	public JlRegion Class2dimSup(JlImage imageRow, JlRegion featureSpace)
	{
		IntPtr proc = JlNativeApi.PreCall(431);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageRow);
		JlNativeApi.Store(proc, 3, featureSpace);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageRow);
		GC.KeepAlive(featureSpace);
		return obj;
	}

	/// <summary>
	///   无监督二维聚类（id 432）：把本图与 image2 两路灰度当作二维特征聚成 numClasses 类，距聚类中心超过 threshold 的像素不归任何类，返回类区域对象数组新句柄。
	/// </summary>
	/// <param name="image2">第二个输入图像。</param>
	/// <param name="threshold">阈值（到聚类中心的最大距离）。默认值：15</param>
	/// <param name="numClasses">类别数（聚类中心个数）。默认值：5</param>
	/// <returns>分类结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 432：<b>无监督</b>二维聚类——把 (本图, <paramref name="image2"/>) 两路灰度当作每个像素的
	///   二维特征，聚成 <paramref name="numClasses"/> 类，返回类区域对象数组（新句柄）。两幅输入图都不改写。</para>
	///   <para><b>两个参数怎么读</b><paramref name="numClasses"/> 是<b>预定的类数</b>（聚类中心个数），不是自动定类；
	///   <paramref name="threshold"/> 是像素到聚类中心的<b>最大距离</b>（特征空间灰度距离、<c>int</c> 精度），
	///   超出的像素不归任何类——距离给得太小时输出会缺块。二者都经 <c>StoreI</c> 直写整数 [待实测：是否允许小数语义]。</para>
	///   <para><b>与 Class2dimSup 的取舍</b>不知道各类长什么样才用本方法；代价是类心由数据自己决定，跨批次/跨图不稳定，
	///   "第 3 类"在不同帧可能指不同东西，别下游按序号硬绑语义。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage gray = new JlImage("byte", 64, 64);
	///   JlImage amp = new JlImage("byte", 64, 64);
	///   using JlRegion cls = gray.Class2dimUnsup(amp, 15, 5);   // 聚成 5 类、距离上限 15
	///   int n = cls.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>结果需释放；两图在原生调用结束前不得回收（<c>GC.KeepAlive</c>）。</para>
	/// </remarks>
	public JlRegion Class2dimUnsup(JlImage image2, int threshold, int numClasses)
	{
		IntPtr proc = JlNativeApi.PreCall(432);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.StoreI(proc, 0, threshold);
		JlNativeApi.StoreI(proc, 1, numClasses);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>
	///   逐像素比较本图与 pattern：先把本图减 grayOffset、把 pattern 平移 (addRow,addCol) 补偿对位，再按 mode 返回差值落在容差带内/外的像素区域新句柄。
	/// </summary>
	/// <param name="pattern">比较图像。</param>
	/// <param name="mode">模式：返回相似还是不同的像素。默认值："diff_outside"</param>
	/// <param name="diffLowerBound">允许的灰度值差的下限。默认值：-5</param>
	/// <param name="diffUpperBound">允许的灰度值差的上限。默认值：5</param>
	/// <param name="grayOffset">从输入图像中减去的灰度值偏移。默认值：0</param>
	/// <param name="addRow">比较图像平移的行坐标。默认值：0</param>
	/// <param name="addCol">比较图像平移的列坐标。默认值：0</param>
	/// <returns>两幅图像相似/不同的点。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 433：逐像素比较本图与 <paramref name="pattern"/>，输出满足模式条件的像素集合
	///   （<see cref="JlRegion"/> 新句柄）。比较前先把本图灰度减 <paramref name="grayOffset"/>、把 <paramref name="pattern"/>
	///   平移 (<paramref name="addRow"/>, <paramref name="addCol"/>)，再问"差值落在 <paramref name="diffLowerBound"/>..
	///   <paramref name="diffUpperBound"/> 之内还是之外"。</para>
	///   <para><b>mode 语义</b><paramref name="mode"/> 决定返回"相似"还是"不同"的像素（默认 "diff_outside" 取差值
	///   在容差带<b>之外</b>的点，即找差异）；合法取值全集与各自的精确语义本层不校验、未体现 [待实测]。</para>
	///   <para><b>与 SubImage/AbsDiffImage 的取舍</b>要"差值图像"（保灰度量纲、可再处理）用逐像素运算；
	///   要直接拿"超差像素区域"去做 Blob 判定的用本方法——它一步给出区域，且带平移补偿，适合对位微偏的模板比对。</para>
	///   <para><b>坑</b>容差带是闭区间还是开区间、平移出界的像素如何计 [待实测]；两图需同尺寸，本层不校验。
	///   容差与偏移参数都是 <c>int</c>（<c>StoreI</c>），给不了小数精度；real 图的小数级差异如何量化由原生决定 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage cur = new JlImage("byte", 64, 64);
	///   JlImage pattern = new JlImage("byte", 64, 64);
	///   using JlRegion diff = cur.CheckDifference(pattern, "diff_outside", -5, 5, 0, 0, 0);
	///   int n = diff.Connection().CountObj();                 // 超差块数
	///   </code>
	///   <para><b>资源与坑</b>结果需释放；<c>GC.KeepAlive</c> 保两图到调用结束。</para>
	/// </remarks>
	public JlRegion CheckDifference(JlImage pattern, string mode, int diffLowerBound, int diffUpperBound, int grayOffset, int addRow, int addCol)
	{
		IntPtr proc = JlNativeApi.PreCall(433);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, pattern);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, diffLowerBound);
		JlNativeApi.StoreI(proc, 2, diffUpperBound);
		JlNativeApi.StoreI(proc, 3, grayOffset);
		JlNativeApi.StoreI(proc, 4, addRow);
		JlNativeApi.StoreI(proc, 5, addCol);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(pattern);
		return obj;
	}

	/// <summary>
	///   在 histoRegion 内统计灰度直方图、经 sigma 平滑后按 percent 灰度差百分比定阈值，返回暗于阈值的字符区域新句柄；threshold 以整数元组经 out 带回，输入图不改写。
	/// </summary>
	/// <param name="histoRegion">计算直方图的区域。</param>
	/// <param name="sigma">直方图高斯平滑的 Sigma。默认值：2.0</param>
	/// <param name="percent">灰度值差的百分比。默认值：95</param>
	/// <param name="threshold">计算得到的阈值。</param>
	/// <returns>暗区域（字符）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 434（字符阈值）：在 <paramref name="histoRegion"/> 内统计灰度直方图，先用高斯
	///   （<paramref name="sigma"/>，直方图平滑量、非图像平滑）压噪，再按 <paramref name="percent"/> 规定的灰度差百分比
	///   定出一个阈值，把<b>暗于它</b>的像素作为字符区域返回。<see cref="JlRegion"/> 与 <paramref name="threshold"/>
	///   都是新对象；输入图不改写。</para>
	///   <para><b>threshold 的形态</b>经 <c>JlTuple.LoadNew(INTEGER)</c> 读出——是<b>整数灰度</b>级阈值；
	///   元素个数与 <paramref name="percent"/> 是否传多值有关（逐通道各定一个阈值 [待实测]）。</para>
	///   <para><b>与 BinaryThreshold 的取舍</b>本算子专为"浅底深字"设计（判据是直方图灰度差百分比），
	///   一般二值化选 <c>BinaryThreshold</c> 的判据族；字符/印刷码场景先试本方法。</para>
	///   <para><b>前提</b><paramref name="histoRegion"/> 应只框住字符与其背景，混入深色机构件会把直方图第二峰带偏；
	///   多值 <paramref name="percent"/> 走 <c>Store</c> 固定，单值请见标量重载（注意两版 out 类型不同，别传错）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion histo = img.Threshold(0.0, 255.0);                    // 全图参与直方图统计
	///   using JlRegion chars = img.CharThreshold(histo, 2.0, new JlTuple(95), out JlTuple thr);
	///   int n = chars.Connection().CountObj();                               // 候选字符块数
	///   </code>
	///   <para><b>资源与坑</b>区域与阈值元组各需释放；<c>out</c> 实参必须写 <c>out</c>；
	///   <c>GC.KeepAlive</c> 保输入图与统计区到调用结束。</para>
	/// </remarks>
	public JlRegion CharThreshold(JlRegion histoRegion, double sigma, JlTuple percent, out JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(434);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, histoRegion);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.Store(proc, 1, percent);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(percent);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out threshold);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(histoRegion);
		return obj;
	}

	/// <summary>
	///   同 id 434 的字符阈值分割：percent 直写单值免固定开销，threshold 经 LoadI 读成 int（多值结果只保留第一个），返回暗于阈值的字符区域。
	/// </summary>
	/// <param name="histoRegion">计算直方图的区域。</param>
	/// <param name="sigma">直方图高斯平滑的 Sigma。默认值：2.0</param>
	/// <param name="percent">灰度值差的百分比。默认值：95</param>
	/// <param name="threshold">计算得到的阈值。</param>
	/// <returns>暗区域（字符）。</returns>
	/// <remarks>
	///   <para>算法、统计区选择与和 <c>BinaryThreshold</c> 的取舍见
	///   <see cref="CharThreshold(JlRegion,double,JlTuple,out JlTuple)"/>：同一原生 id 434。</para>
	///   <para><b>实际差异</b><paramref name="percent"/> 经 <c>StoreD</c> 直写单值、无固定开销；阈值用 <c>LoadI</c>
	///   读成 <c>int</c>——多值结果只保留第一个（超出 int 灰度量纲时如何裁 [待实测]）。单通道常规用法选本版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion histo = img.Threshold(0.0, 255.0);
	///   using JlRegion chars = img.CharThreshold(histo, 2.0, 95.0, out int thr);
	///   bool plausible = thr &gt;= 0 &amp;&amp; thr &lt;= 255;
	///   </code>
	/// </remarks>
	public JlRegion CharThreshold(JlRegion histoRegion, double sigma, double percent, out int threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(434);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, histoRegion);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreD(proc, 1, percent);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlNativeApi.LoadI(proc, 0, err, out threshold);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(histoRegion);
		return obj;
	}

	/// <summary>把整数型标签图按灰度值拆成区域栈：图内每出现一个不同的整数值就生成一个区域。</summary>
	/// <returns>新区域栈句柄（JlRegion.LoadNew 槽 1），用毕需 Dispose；本图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 435。只有 <c>this</c> 一路图标输入（<c>Store(proc,1)</c>）、无控制参数：
	///   灰度值即标签，栈内区域个数 = 图内出现过的不同灰度值个数。</para>
	///   <para><b>约束或前提</b>输入应为整数型单通道图（byte/int2 等）；浮点图的取整行为本层未体现 [待实测]。
	///   只有落在域（domain）内的像素参与分档，域外的值不产生区域。</para>
	///   <para><b>与相邻算子的取舍</b>按"某个灰度范围"抠一块区域用 <see cref="Threshold(double,double)"/>；
	///   要"每一档灰度值各成一个区域"才用本方法。分类/分割算子输出的标签图常接本方法还原成区域栈。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion levels = img.LabelToRegion();
	///   int nLevels = levels.CountObj();                   // 全零图只有 1 档灰度
	///   </code>
	///   <para><b>资源与坑</b>末尾 <c>GC.KeepAlive(this)</c>，调用结束前本图不得释放。栈内区域是否严格按
	///   灰度值升序排列本层未校验 [待实测]，按序号取档前先用灰度/面积属性核对一遍。</para>
	/// </remarks>
	public JlRegion LabelToRegion()
	{
		IntPtr proc = JlNativeApi.PreCall(435);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>对梯度幅值图做非极大值抑制，把粗边缘细化到近单像素宽（仅幅值版，不需要方向图）。</summary>
	/// <param name="mode">抑制方式：水平/垂直或无方向 NMS。Default: "hvnms"</param>
	/// <returns>细化后的新幅值图句柄（LoadNew）；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 436，mode 以 <c>StoreS</c> 直写控制槽 0、无钉元组开销。典型输入是
	///   <see cref="SobelAmp(string,int)"/> 一类的幅值图：沿坐标轴方向比较邻域幅值，只保留局部极大点。</para>
	///   <para><b>约束或前提</b>输入应为单通道整数/浮点幅值图；对普通灰度图调用也能执行，但平坦区没有细化意义。
	///   "hvnms" 只比较水平与垂直两个方向，mode 其余取值集合本层未校验 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>斜向边缘需要沿真实梯度方向细化时改用
	///   <see cref="NonmaxSuppressionDir(JlImage,string)"/>（额外吃一幅方向图）；本方法免配方向图、更省，
	///   代价是斜边细化不彻底。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage amp = img.SobelAmp("sum_abs", 3);
	///   using JlImage thin = amp.NonmaxSuppressionAmp("hvnms");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose；末尾 <c>GC.KeepAlive(this)</c>，调用结束前本图不得释放。
	///   细化后的幅值图通常再跟一次阈值取二值边缘。</para>
	/// </remarks>
	public JlImage NonmaxSuppressionAmp(string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(436);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用配套方向图对幅值边缘做非极大值抑制：可沿任意梯度方向细化，不像仅幅值版只查水平/垂直。</summary>
	/// <param name="imgDir">与幅值图同尺寸同域的方向图（须来自同一套边缘算子的方向输出）。</param>
	/// <param name="mode">纯抑制还是插值细化。Default: "nms"</param>
	/// <returns>细化后的新幅值图句柄；本图与方向图均不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 437。<c>this</c> 是幅值输入（图标槽 1），<paramref name="imgDir"/> 存图标槽 2，
	///   mode 以 <c>StoreS</c> 直写控制槽 0：沿方向图指示的梯度方向找局部极大点。</para>
	///   <para><b>约束或前提</b>幅值与方向必须成对——方向图通常取自 <see cref="SobelDir(out JlImage,string,int)"/> 的 out 输出；
	///   拿另一算法的方向图混配会细化错位且不报错 [待实测：尺寸不符是否报原生错]。</para>
	///   <para><b>与相邻算子的取舍</b>只要水平/垂直细化、不想多养一幅方向图时用 <see cref="NonmaxSuppressionAmp(string)"/>；
	///   "nms" 只删非极大点、结果仍是原幅值，插值类 mode 做亚像素峰值插值（取值集合本层未校验 [待实测]）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage amp = img.SobelDir(out JlImage dir, "sum_abs", 3);
	///   using JlImage thin = amp.NonmaxSuppressionDir(dir, "nms");
	///   dir.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose；末尾对 <c>this</c> 与 <paramref name="imgDir"/> 均 <c>GC.KeepAlive</c>，
	///   两图在调用结束前都不得释放（示例里 dir 在抑制完成后才可 Dispose）。</para>
	/// </remarks>
	public JlImage NonmaxSuppressionDir(JlImage imgDir, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(437);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imgDir);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imgDir);
		return obj;
	}

	/// <summary>双阈值（滞后）分割：强像素作种子，弱像素只在能连通到种子时才保留。</summary>
	/// <param name="low">弱阈值下界。Default: 30</param>
	/// <param name="high">强阈值下界。Default: 60</param>
	/// <param name="maxLength">"潜在"点沿弱像素路径走到"安全"点所允许的最大步数。Default: 10</param>
	/// <returns>新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 438，<c>InitOCT(proc,1)</c> 后经 <c>JlRegion.LoadNew</c> 取一个新区域。
	///   灰度 <c>&gt; high</c> 的像素是安全点，<c>low..high</c> 之间的潜在点只有能在 <paramref name="maxLength"/> 步内
	///   沿潜在点走到某个安全点时才进入结果，因此它不是"两个 <c>Threshold</c> 求并"：孤立出现的弱区域会被丢掉。</para>
	///   <para><b>什么时候该用它</b>边缘/缺陷对比度局部不足、用单一阈值要么断线要么吞噪声时。
	///   灰度整体可靠时不要用它——它比 <c>Threshold</c> 多了路径搜索，且 <paramref name="maxLength"/> 给小会随机截断弱结构，
	///   给大则把弱噪声一路串进来，这两个方向的误差都不体现在返回值上，只能靠面积统计发现。</para>
	///   <para><b>约束</b>本层不检查输入是否单通道 [待实测]，多通道图请先 <c>AccessChannel(1)</c> 取一个通道再分割。
	///   <paramref name="low"/> 与 <paramref name="high"/> 的相对大小未在本层校验，传反了不会在这里报错 [待实测]。
	///   <paramref name="maxLength"/> 是 <c>int</c>（<c>StoreI</c>），只能整数步数；0 与负值的语义本层未体现 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion weak = img.HysteresisThreshold(new JlTuple(20.0), new JlTuple(60.0), 30);
	///   int n = weak.Connection().CountObj();
	///   </code>
	///   <para><b>资源与坑</b>元组重载对 <paramref name="low"/>/<paramref name="high"/> 做固定与 <c>UnpinTuple</c>；
	///   返回句柄由调用者释放。末尾 <c>GC.KeepAlive(this)</c> 只保输入，不保输出。</para>
	/// </remarks>
	public JlRegion HysteresisThreshold(JlTuple low, JlTuple high, int maxLength)
	{
		IntPtr proc = JlNativeApi.PreCall(438);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, low);
		JlNativeApi.Store(proc, 1, high);
		JlNativeApi.StoreI(proc, 2, maxLength);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>双阈值分割（整数阈值版）。</summary>
	/// <param name="low">弱阈值下界。Default: 30</param>
	/// <param name="high">强阈值下界。Default: 60</param>
	/// <param name="maxLength">潜在点走到安全点的最大步数。Default: 10</param>
	/// <returns>新区域句柄。</returns>
	/// <remarks>
	///   <para>算法与取舍见 <see cref="HysteresisThreshold(JlTuple,JlTuple,int)"/>：同一原生 id 438。</para>
	///   <para><b>实际差异</b><paramref name="low"/>/<paramref name="high"/> 以 <c>StoreI</c> 作整数传，
	///   灰度阈值不能带小数；元组版走 <c>Store</c> 并额外做固定/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.HysteresisThreshold(30, 60, 10);
	///   </code>
	/// </remarks>
	public JlRegion HysteresisThreshold(int low, int high, int maxLength)
	{
		IntPtr proc = JlNativeApi.PreCall(438);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, low);
		JlNativeApi.StoreI(proc, 1, high);
		JlNativeApi.StoreI(proc, 2, maxLength);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>自动求一个二值化阈值并分割，同时把实际用到的阈值回传。</summary>
	/// <param name="method">判据方法。Default: "max_separability"</param>
	/// <param name="lightDark">取前景还是背景。Default: "dark"</param>
	/// <param name="usedThreshold">回传：本帧实际算出的阈值。</param>
	/// <returns>新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 439。两个输出：<c>JlRegion.LoadNew(proc,1,...)</c> 取区域，
	///   <c>JlTuple.LoadNew(proc,0,...)</c> 取阈值——阈值是<b>控制参数输出</b>（<c>InitOCT(proc,0)</c>），
	///   不是图标对象，所以它可以逐帧变化并被记录下来。</para>
	///   <para><b>为什么先想到它而不是 <c>Threshold</c></b>光源或工件反射率批次漂移时，写死阈值会缓慢失配；
	///   本算子按直方图判据自动定阈值，并给出该帧阈值，便于写日志、做统计门限（阈值突变通常意味着来料或光照变了）。
	///   反过来：需要跨帧严格可比的量纲（"灰度 &gt; 180 才算合格"）时<b>不要</b>用它，每帧重算的阈值会让判定标准本身漂移。</para>
	///   <para><b>与 <c>AutoThreshold</c> 的取舍</b><c>AutoThreshold</c> 只需一个直方图平滑量 <c>sigma</c>，不回报阈值；
	///   本算子按 <paramref name="method"/> 选判据并回报阈值。要看阈值、要在多判据之间比较时用它。</para>
	///   <para><b>约束</b><paramref name="method"/> 与 <paramref name="lightDark"/> 都是字符串（<c>StoreS</c>），
	///   本层不校验取值，写错只能等原生端在 <c>PostCall</c> 抛 <c>JlOperatorException</c>；
	///   <paramref name="lightDark"/> 的两个取值给出互补的两块区域，边界像素归哪一侧未在本层体现 [待实测]。
	///   <paramref name="usedThreshold"/> 的元素个数由判据决定（多类判据可能不止一个）[待实测]，
	///   用 <c>Length</c> 判断后再按下标取值。多通道输入的通道数不检查 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlRegion fg = img.BinaryThreshold("max_separability", "dark", out JlTuple used);
	///   double t = used[0].D;                                    // 本帧阈值
	///   using JlRegion same = img.Threshold(new JlTuple(0.0), new JlTuple(t));
	///   fg.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>区域与新元组都是新对象，需各自释放；<c>out</c> 实参必须写 <c>out</c>，
	///   不能预先声明后按值传（CS1615 方向不匹配）。</para>
	/// </remarks>
	public JlRegion BinaryThreshold(string method, string lightDark, out JlTuple usedThreshold)
	{
		IntPtr proc = JlNativeApi.PreCall(439);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, method);
		JlNativeApi.StoreS(proc, 1, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, err, out usedThreshold);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>自动二值化并分割（阈值以整数回传）。</summary>
	/// <param name="method">判据方法。Default: "max_separability"</param>
	/// <param name="lightDark">取前景还是背景。Default: "dark"</param>
	/// <param name="usedThreshold">回传：本帧实际用到的整数阈值。</param>
	/// <returns>新区域句柄。</returns>
	/// <remarks>
	///   <para>算法、判据选择与 <paramref name="lightDark"/> 的取舍见 <see cref="BinaryThreshold(string,string,out JlTuple)"/>：
	///   同一原生 id 439，区域输出路径完全相同。</para>
	///   <para><b>实际差异</b>阈值改用 <c>JlNativeApi.LoadI</c> 读取，因此只适合 8 位/整型灰度阈值。
	///   <c>float</c> 图或判据给出非整数阈值时会被截断成 <c>int</c> [待实测：是否改为报错]，
	///   这类图像请用元组版按 <c>double</c> 取值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlRegion fg = img.BinaryThreshold("max_separability", "light", out int used);
	///   bool plausible = used &gt;= 0 &amp;&amp; used &lt;= 255;
	///   fg.Dispose();
	///   </code>
	/// </remarks>
	public JlRegion BinaryThreshold(string method, string lightDark, out int usedThreshold)
	{
		IntPtr proc = JlNativeApi.PreCall(439);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, method);
		JlNativeApi.StoreS(proc, 1, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlNativeApi.LoadI(proc, 0, err, out usedThreshold);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用逐像素邻域统计出的局部阈值分割图像，返回区域（元组参数版）。</summary>
	/// <param name="method">局部阈值判据名。Default: "adapted_std_deviation"</param>
	/// <param name="lightDark">取前景还是背景。Default: "dark"</param>
	/// <param name="genParamName">判据的通用参数名列表。Default: []</param>
	/// <param name="genParamValue">与参数名按下标一一对应的取值。Default: []</param>
	/// <returns>新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 440，单个输出对象（<c>InitOCT(proc,1)</c> + <c>JlRegion.LoadNew</c>），
	///   即阈值是"每个像素各算一份"的：判据取自该像素邻域的统计量，因此同一组参数在亮区和暗区会给出不同的门限。
	///   <c>method</c> 与 <c>lightDark</c> 都是字符串，本层不做白名单校验，写错只在 <c>PostCall</c> 抛原生错误码。</para>
	///   <para><b>约束与前提</b>参数名与取值以 <c>Store</c> 成对写入槽位 2、3，两者按下标配对；
	///   长度不等、给了名字却没给值、或名字不属于当前 <c>method</c> 支持的集合，本层一律不检查，
	///   多余的值会被原生端忽略还是报错 [待实测]。给空元组表示"全部用原生默认"，这也是 <c>Default: []</c> 的含义。</para>
	///   <para><b>与相邻算子的取舍</b>只需"邻域均值 ± 系数×邻域标准差"这一种判据时用 <c>VarThreshold</c>，
	///   它的窗口与系数是显式数值形参，比这里靠字符串开关更清楚；已经有一幅阈值图时用 <c>DynThreshold</c>；
	///   光照本身均匀时直接用 <c>Threshold</c>——本算子要为每个像素做邻域统计，代价明显更高 [待实测：具体倍率]。
	///   输入应为单通道灰度图，多通道时的取通道规则本层未体现 [待实测]。</para>
	///   <para><b>参数取向</b>元组版可以把同名参数给多个值（配合按通道/按区间展开的判据），
	///   代价是每次调用要固定并在调用后 <c>UnpinTuple</c> 两个元组；只给单值时请改用
	///   <see cref="LocalThreshold(string,string,string,int)"/>，它走 <c>StoreS</c>/<c>StoreI</c> 直写，无固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.LocalThreshold("adapted_std_deviation", "dark", new JlTuple(), new JlTuple());
	///   int n = reg.Connection().CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回的新句柄归调用者释放（<c>JlRegion</c> 实现 <c>IDisposable</c>）；
	///   末尾 <c>GC.KeepAlive(this)</c>，故输入图像在整个原生调用期间不得回收。
	///   分割结果是所有合格像素的并，想按目标分别处理必须再 <c>Connection()</c>。</para>
	/// </remarks>
	public JlRegion LocalThreshold(string method, string lightDark, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(440);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, method);
		JlNativeApi.StoreS(proc, 1, lightDark);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.Store(proc, 3, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>局部阈值分割（只给一个整数型通用参数的单值版）。</summary>
	/// <param name="method">局部阈值判据名。Default: "adapted_std_deviation"</param>
	/// <param name="lightDark">取前景还是背景。Default: "dark"</param>
	/// <param name="genParamName">单个通用参数的名字。Default: []</param>
	/// <param name="genParamValue">该参数的整数值。Default: []</param>
	/// <returns>新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para>判据含义、字符串不校验、与 <c>VarThreshold</c>/<c>DynThreshold</c> 的取舍见
	///   <see cref="LocalThreshold(string,string,JlTuple,JlTuple)"/>：两个重载同走原生 id 440，区域输出路径一致。</para>
	///   <para><b>实际差异（两处会坑人的地方）</b>①名字与值经 <c>StoreS</c>(槽位 2)/<c>StoreI</c>(槽位 3) 直写，
	///   不做元组固定与解固定，热路径单次调参用它更省；②形参类型是 <c>int</c>，
	///   所以<b>带小数的判据参数（各类加权系数、比例阈值）在这里表达不出来</b>，
	///   传 0.2 这类值编译期就通不过，只能改元组重载并 <c>new JlTuple(0.2)</c>。
	///   同理，本重载一次只能带一个参数，要给多对参数也只能回元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.LocalThreshold("adapted_std_deviation", "dark", "min_length", 5);
	///   double area = reg.RegionFeatures("area");
	///   </code>
	///   <para><b>资源与坑</b>参数名不经本层校验，拼错或该判据不支持此名时由原生端报错 [待实测]；
	///   返回新句柄需释放；末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlRegion LocalThreshold(string method, string lightDark, string genParamName, int genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(440);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, method);
		JlNativeApi.StoreS(proc, 1, lightDark);
		JlNativeApi.StoreS(proc, 2, genParamName);
		JlNativeApi.StoreI(proc, 3, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按局部均值与局部标准差自适应阈值分割（元组版）。</summary>
	/// <param name="maskWidth">计算局部均值/标准差的窗口宽。Default: 15</param>
	/// <param name="maskHeight">窗口高。Default: 15</param>
	/// <param name="stdDevScale">局部标准差的加权系数。Default: 0.2</param>
	/// <param name="absThreshold">与局部均值的最小灰度差。Default: 2</param>
	/// <param name="lightDark">取亮区还是暗区。Default: "dark"</param>
	/// <returns>新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 441，一个区域输出（<c>InitOCT(proc,1)</c> + <c>JlRegion.LoadNew</c>）。
	///   每像素的判据来自其 <paramref name="maskWidth"/>×<paramref name="maskHeight"/> 邻域：局部阈值由均值与
	///   <paramref name="stdDevScale"/>×局部标准差构成，并要求像素与均值的差超过 <paramref name="absThreshold"/>
	///   才算目标——两个条件谁起决定作用，取决于平坦区（标准差小，<paramref name="absThreshold"/> 说话）
	///   还是纹理区（标准差大，<paramref name="stdDevScale"/> 说话）。</para>
	///   <para><b>什么时候该用它</b>阴影、渐晕等低频不均 + 目标本身灰度接近背景。平坦背景上它会比
	///   <c>Threshold</c> 稳，但在<b>大片的强纹理区会把纹理本身整片切成目标</b>——这是它最常见的误用，
	///   表现为区域面积随纹理而非随缺陷变化。此时先调大 <paramref name="stdDevScale"/>（提高门限），
	///   或改用 <c>DynThreshold</c> 配合自己构造的阈值图。</para>
	///   <para><b>窗口与代价</b><paramref name="maskWidth"/>/<paramref name="maskHeight"/> 是 <c>int</c>（<c>StoreI</c>），
	///   窗口需大于目标尺寸才有意义，但逐窗统计的开销按面积增长，大图上它比 <c>Threshold</c> 慢得多 [待实测：具体倍率]。
	///   偶数窗口与 1×1 窗口本层不校验 [待实测]。窗口边缘像素的补齐方式（是否等同 <c>Reflection</c>/<c>Representative</c> 那类边界处理）在本层没有体现 [待实测]。</para>
	///   <para><b>参数取向</b><paramref name="stdDevScale"/>、<paramref name="absThreshold"/> 接受元组，
	///   多值语义（是否按通道或按区间展开）本层无法判断 [待实测]；单值场景请直接用
	///   <see cref="VarThreshold(int,int,double,double,string)"/>，省掉固定/解固定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion defects = img.VarThreshold(15, 15, new JlTuple(0.2), new JlTuple(2.0), "dark");
	///   int n = defects.Connection().CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄归调用者释放；<c>lightDark</c> 为字符串，取值错误只在原生端报错。
	///   末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlRegion VarThreshold(int maskWidth, int maskHeight, JlTuple stdDevScale, JlTuple absThreshold, string lightDark)
	{
		IntPtr proc = JlNativeApi.PreCall(441);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.Store(proc, 2, stdDevScale);
		JlNativeApi.Store(proc, 3, absThreshold);
		JlNativeApi.StoreS(proc, 4, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(stdDevScale);
		JlNativeApi.UnpinTuple(absThreshold);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>局部均值/标准差自适应阈值分割（单值版）。</summary>
	/// <param name="maskWidth">窗口宽。Default: 15</param>
	/// <param name="maskHeight">窗口高。Default: 15</param>
	/// <param name="stdDevScale">局部标准差加权系数。Default: 0.2</param>
	/// <param name="absThreshold">与均值的最小灰度差。Default: 2</param>
	/// <param name="lightDark">取亮区还是暗区。Default: "dark"</param>
	/// <returns>新区域句柄。</returns>
	/// <remarks>
	///   <para>算法、窗口代价与误用场景见 <see cref="VarThreshold(int,int,JlTuple,JlTuple,string)"/>：同一原生 id 441，
	///   本版本把两个阈值参数经 <c>StoreD</c> 直写，不做元组固定/解固定，单值调参时用它更省事。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion dark = img.VarThreshold(15, 15, 0.2, 2.0, "dark");
	///   </code>
	/// </remarks>
	public JlRegion VarThreshold(int maskWidth, int maskHeight, double stdDevScale, double absThreshold, string lightDark)
	{
		IntPtr proc = JlNativeApi.PreCall(441);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreD(proc, 2, stdDevScale);
		JlNativeApi.StoreD(proc, 3, absThreshold);
		JlNativeApi.StoreS(proc, 4, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用另一幅图逐像素当阈值做分割（元组偏移版）。</summary>
	/// <param name="thresholdImage">逐像素阈值的来源图像。</param>
	/// <param name="offset">叠加在 <paramref name="thresholdImage"/> 上的偏移量。Default: 5.0</param>
	/// <param name="lightDark">取亮于、暗于还是近似等于阈值的像素。Default: "light"</param>
	/// <returns>新区域句柄；两幅输入图像都不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 442，区域输出。阈值图以图像句柄存在第 3 个槽位
	///   （<c>Store(proc, 2, thresholdImage)</c>），偏移与控制参数 <paramref name="lightDark"/> 分别在槽位 0、1，
	///   也就是说<b>阈值是图，不是标量</b>：每个像素各比各的。</para>
	///   <para><b>典型搭配</b>把 <c>MeanImage(...)</c> 或 <c>GaussImage(...)</c> 得到的低频图当阈值图，
	///   就得到"比局部平均亮 <c>offset</c> 的像素"，这是亮斑/暗点检测的标准做法，也是它区别于
	///   <c>VarThreshold</c> 的地方：阈值的来源由你自己选（还可以是另一帧的参考图、或标定好的照度补偿图），
	///   而不是算子内部统计出来的。</para>
	///   <para><b>约束</b>两幅图必须同尺寸，本层不做尺寸/类型匹配检查 [待实测]：阈值图与被分割图的宽高不一致时，
	///   错误由原生端在 <c>PostCall</c> 抛出。<c>MeanImage</c>/<c>GaussImage</c> 的输出类型可能与输入不同
	///   （如 <c>byte</c> 进 <c>real</c> 出），跨类型时 <paramref name="offset"/> 的量纲按谁的类型理解 [待实测]。</para>
	///   <para><b>参数取向</b><paramref name="lightDark"/> 按参数说明有三种取向（亮、暗、与阈值相近）；字符串不经本层校验。
	///   <paramref name="offset"/> 给元组时多值语义本层无法确定 [待实测]，单值请改用
	///   <see cref="DynThreshold(JlImage,double,string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage localMean = img.MeanImage(15, 15);                       // 低频照度
	///   using JlRegion spots = img.DynThreshold(localMean, new JlTuple(5.0), "light");
	///   int n = spots.Connection().CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回区域是新句柄；<c>thresholdImage</c> 只是被读取，不因此转交所有权，
	///   用完自己释放。代码末尾对 <c>this</c> 与 <paramref name="thresholdImage"/> 都做了
	///   <c>GC.KeepAlive</c>，即两幅图在整个原生调用期间都不能被回收。</para>
	/// </remarks>
	public JlRegion DynThreshold(JlImage thresholdImage, JlTuple offset, string lightDark)
	{
		IntPtr proc = JlNativeApi.PreCall(442);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, thresholdImage);
		JlNativeApi.Store(proc, 0, offset);
		JlNativeApi.StoreS(proc, 1, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(offset);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(thresholdImage);
		return obj;
	}

	/// <summary>用另一幅图逐像素当阈值做分割（单值偏移版）。</summary>
	/// <param name="thresholdImage">逐像素阈值的来源图像。</param>
	/// <param name="offset">叠加在阈值图上的偏移量。Default: 5.0</param>
	/// <param name="lightDark">取亮于、暗于还是近似等于阈值的像素。Default: "light"</param>
	/// <returns>新区域句柄。</returns>
	/// <remarks>
	///   <para>算法、尺寸匹配与 KeepAlive 细节见 <see cref="DynThreshold(JlImage,JlTuple,string)"/>：同一原生 id 442。
	///   本版本用 <c>StoreD</c> 直写偏移，无元组固定开销，实际调参时基本都用这一版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage refImg = img.GaussImage(15);
	///   using JlRegion bright = img.DynThreshold(refImg, 10.0, "light");
	///   </code>
	/// </remarks>
	public JlRegion DynThreshold(JlImage thresholdImage, double offset, string lightDark)
	{
		IntPtr proc = JlNativeApi.PreCall(442);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, thresholdImage);
		JlNativeApi.StoreD(proc, 0, offset);
		JlNativeApi.StoreS(proc, 1, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(thresholdImage);
		return obj;
	}

	/// <summary>按一个或多个灰度区间分割整幅图像，返回区域。</summary>
	/// <param name="minGray">各区间下界，或特殊值 "min"。Default: 128.0</param>
	/// <param name="maxGray">各区间上界，或特殊值 "max"。Default: 255.0</param>
	/// <returns>落在任一区间内的像素组成的新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 443，<c>InitOCT(proc,1)</c> 只声明一个输出对象，
	///   经 <c>JlRegion.LoadNew(proc,1,...)</c> 取回<b>新句柄</b>：给 N 个区间也只有一个区域（各区间的并），
	///   不是长度为 N 的区域数组——想按区间分别处理，必须先 <c>Connection()</c> 再按灰度另做区分。</para>
	///   <para><b>输出类型</b>本算子出的是 <see cref="JlRegion"/>，不是 <c>JlImage</c>。
	///   需要"分割后的图像"时，把结果区域交给 <c>ReduceDomain</c> 或 <c>PaintRegion</c>；
	///   比较运算符 <c>image &gt;= 128.0</c> 同样返回区域而不是图像，别按布尔图的思路去接。</para>
	///   <para><b>灰度写死 0.0/255.0 会静默漏像素</b>上下界是 double，与本层无关的图像类型决定实际量程：
	///   <c>byte</c> 为 0..255，<c>uint2</c> 到 65535，<c>float</c>、<c>direction</c> 可超过 255。
	///   在 <c>uint2</c>/<c>float</c> 图上写 <c>Threshold(0.0, 255.0)</c> 不报错，只是高灰度像素被丢掉。
	///   跨类型通用写法是用参数说明里的特殊值 <c>"min"</c>/<c>"max"</c>（元组重载可直接
	///   <c>new JlTuple("min")</c>），由原生端按图像类型取实际极值 [待实测：double 重载能否表达特殊值]。</para>
	///   <para><b>区间边界</b>给出的是下界与上界，属闭区间还是左闭右开，托管层只把两个值 <c>Store</c> 给原生，未做任何裁剪或校验 [待实测]。
	///   两个元组按下标两两配对，长度不等或长度为奇数时本层不检查，行为由原生端决定 [待实测]。</para>
	///   <para><b>通道数</b>本层不检查通道数（<c>CountChannels()</c> 需自行调用）。多通道图如何取舍通道未在本层体现 [待实测]，
	///   常规做法是先 <c>AccessChannel(1)</c>（取单个通道，索引从 1 起）或 <c>ChannelsToImage()</c> 拆成通道数组后
	///   用 <c>Rgb3ToGray(imageGreen, imageBlue)</c> 加权合成，再分割。</para>
	///   <para><b>与相邻算子的取舍</b>光照不均用 <c>DynThreshold</c>；对比度弱的边缘用 <c>HysteresisThreshold</c>；
	///   不知道阈值时用 <c>AutoThreshold</c>/<c>BinaryThreshold</c>；只要亚像素等灰度线时用 <c>ThresholdSubPix</c>。
	///   本算子按全图绝对灰度切，亮度漂移会让同一组参数在不同批次图上给出不同面积的区域，这是它最典型的失效方式。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   string type = img.GetImageType()[0].S;                                  // "byte" -&gt; 0..255
	///   using JlRegion all = img.Threshold(new JlTuple("min"), new JlTuple("max"));   // 随类型自适应
	///   int n = all.Connection().CountObj();
	///   using JlRegion two = img.Threshold(new JlTuple(0.0, 200.0), new JlTuple(60.0, 255.0));   // 两个区间取并
	///   </code>
	///   <para><b>资源与坑</b>返回的新句柄归调用者释放（<c>JlRegion</c> 实现 <c>IDisposable</c>）；
	///   末尾 <c>GC.KeepAlive(this)</c> 保证输入图像在原生调用期间不被回收。
	///   元组重载每次调用都固定并解固定 <paramref name="minGray"/>/<paramref name="maxGray"/>，
	///   单次取值请改用 <see cref="Threshold(double,double)"/>。</para>
	/// </remarks>
	public JlRegion Threshold(JlTuple minGray, JlTuple maxGray)
	{
		IntPtr proc = JlNativeApi.PreCall(443);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, minGray);
		JlNativeApi.Store(proc, 1, maxGray);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minGray);
		JlNativeApi.UnpinTuple(maxGray);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按单个灰度区间分割图像（单值版）。</summary>
	/// <param name="minGray">区间下界。Default: 128.0</param>
	/// <param name="maxGray">区间上界。Default: 255.0</param>
	/// <returns>新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para>语义、边界与灰度量程问题见 <see cref="Threshold(JlTuple,JlTuple)"/>：两个重载同走原生 id 443，
	///   本版本用 <c>StoreD</c> 直写两个 double，不做元组固定/解固定，单次分割应当用它。</para>
	///   <para><b>实际差异</b>参数是 <c>double</c>，因此无法传 <c>"min"</c>/<c>"max"</c> 特殊值：
	///   需要按图像类型自适应量程时只能用元组重载；多区间也只能用元组重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion reg = img.Threshold(128.0, 255.0);
	///   double area = reg.RegionFeatures("area");
	///   </code>
	/// </remarks>
	public JlRegion Threshold(double minGray, double maxGray)
	{
		IntPtr proc = JlNativeApi.PreCall(443);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, minGray);
		JlNativeApi.StoreD(proc, 1, maxGray);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>提取等灰度线（level crossing），结果为亚像素轮廓。</summary>
	/// <param name="threshold">等灰度线的灰度值。Default: 128</param>
	/// <returns>提取出的等灰度线轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 444，输出经 <c>JlXLDCont.LoadNew</c> 取回，是 <see cref="JlXLDCont"/>
	///   轮廓而不是 <see cref="JlRegion"/>：像素中心间用插值定位，所以轮廓坐标可落在像素之间。</para>
	///   <para><b>什么时候用它</b>要拿亚像素位置做测量/拟合时。要的是"哪些像素属于目标"（面积、连通域）时用它反而绕远：
	///   轮廓得先经 <c>GenRegionContourXld("filled")</c> 才能变回区域，且面积不再与像素网格严格对应。</para>
	///   <para><b>易踩</b>它只看灰度等于 <paramref name="threshold"/> 的位置，没有幅值/梯度门限，
	///   所以噪声图上会得到大量几像素长的闭合碎轮廓，可用 <c>LengthXld()</c> 返回的逐条长度筛掉。
	///   多个灰度值经元组一次提多条等灰度线时，输出是单个多轮廓对象还是对象数组，本层无法判断，
	///   用 <c>CountObj()</c> 确认 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont contours = img.ThresholdSubPix(new JlTuple(128.0));
	///   int numContours = contours.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；元组重载会固定/解固定 <paramref name="threshold"/>。</para>
	/// </remarks>
	public JlXLDCont ThresholdSubPix(JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(444);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(threshold);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>提取等灰度线（单值版）。</summary>
	/// <param name="threshold">等灰度线的灰度值。Default: 128</param>
	/// <returns>亚像素轮廓。</returns>
	/// <remarks>
	///   <para>语义、碎轮廓筛查与 XLD→区域的代价见 <see cref="ThresholdSubPix(JlTuple)"/>：同一原生 id 444，
	///   本版本 <c>StoreD</c> 直写单个 double，只提一条等灰度线，无元组固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont c = img.ThresholdSubPix(128.0);
	///   int n = c.CountObj();
	///   </code>
	/// </remarks>
	public JlXLDCont ThresholdSubPix(double threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(444);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按多通道特征向量的距离做区域生长（元组容差版）。</summary>
	/// <param name="metric">特征向量距离的度量。Default: "2-norm"</param>
	/// <param name="minTolerance">距离下界。Default: 0.0</param>
	/// <param name="maxTolerance">距离上界。Default: 20.0</param>
	/// <param name="minSize">输出区域的最小像素数。Default: 30</param>
	/// <returns>生长得到的区域（对象数组）；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 445，单路区域输出。判据是<b>特征向量</b>距离：每个像素的各通道值组成向量，
	///   按 <paramref name="metric"/> 比较。因此它的正当用途是多通道图（RGB、Lab，或 <c>Compose3</c>/<c>Compose2</c>
	///   拼出来的自定义特征图）。单通道图上它退化成按灰度差生长，此时更该用 <see cref="Regiongrowing(int,int,double,int)"/>。</para>
	///   <para><b>两个容差是一上一下</b>距离被分成三档：不超过 <paramref name="minTolerance"/> 的像素直接并入，
	///   介于两界之间的像素是否并入由原生端决定 [待实测]，超过 <paramref name="maxTolerance"/> 则拒绝。
	///   <c>minTolerance = 0</c> 是默认，等于"只有完全相同的值才无条件并入"。<paramref name="maxTolerance"/> 越大，
	///   跨区域合并越激进、区域数越少，最终表现为"欠分割"（目标和背景粘成一片）。</para>
	///   <para><b>坑：结果不覆盖全图</b>小于 <paramref name="minSize"/> 的区域被直接丢弃，丢弃部分不会并入邻区，
	///   于是区域之间留下空隙。想拿它做"整图分块"必须再 <c>Union1</c> 或补一次 <c>FillUp</c>，否则面积统计对不上图像总像素数。
	///   <paramref name="minSize"/> 是 <c>int</c> 像素个数，与目标实际尺寸同量纲，换相机分辨率后必须重新给值。</para>
	///   <para><b>与相邻算子的取舍</b>已知每个块内灰度应围绕某个均值保持一致 → <see cref="RegiongrowingMean(JlTuple,JlTuple,double,int)"/>；
	///   想省算力、按栅格播种 → <see cref="Regiongrowing(int,int,JlTuple,int)"/>；只要按灰度区间分块 → <c>Threshold</c>。
	///   生长类算子普遍比阈值类慢，且对椒盐噪声敏感，常规做法是先 <c>MedianImage("circle",2,"mirrored")</c> 再生长。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage r = new JlImage("byte", 640, 480);
	///   using JlImage g = new JlImage("byte", 640, 480);
	///   using JlImage b = new JlImage("byte", 640, 480);
	///   using JlImage rgb = r.Compose3(g, b);
	///   using JlRegion seg = rgb.RegiongrowingN("2-norm", new JlTuple(0.0), new JlTuple(20.0), 30);
	///   int numSegments = seg.CountObj();
	///   </code>
	///   <para><b>资源与坑</b><paramref name="metric"/> 为字符串，不校验取值，错了由原生端 <c>PostCall</c> 抛
	///   <c>JlOperatorException</c>；元组容差版会固定/解固定两个元组，定值调参用 double 版。</para>
	/// </remarks>
	public JlRegion RegiongrowingN(string metric, JlTuple minTolerance, JlTuple maxTolerance, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(445);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, metric);
		JlNativeApi.Store(proc, 1, minTolerance);
		JlNativeApi.Store(proc, 2, maxTolerance);
		JlNativeApi.StoreI(proc, 3, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minTolerance);
		JlNativeApi.UnpinTuple(maxTolerance);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>多通道特征区域生长（单值容差版）。</summary>
	/// <param name="metric">距离度量。Default: "2-norm"</param>
	/// <param name="minTolerance">距离下界。Default: 0.0</param>
	/// <param name="maxTolerance">距离上界。Default: 20.0</param>
	/// <param name="minSize">最小区域像素数。Default: 30</param>
	/// <returns>区域数组。</returns>
	/// <remarks>
	///   <para>三档容差、<c>minSize</c> 造成的空隙、以及"单通道图该改用 <c>Regiongrowing</c>"等结论见
	///   <see cref="RegiongrowingN(string,JlTuple,JlTuple,int)"/>：同一原生 id 445。</para>
	///   <para><b>实际差异</b>两个容差经 <c>StoreD</c> 直写，无固定/解固定开销，定值调参用这一版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion seg = img.RegiongrowingN("2-norm", 0.0, 20.0, 30);
	///   int n = seg.CountObj();
	///   </code>
	/// </remarks>
	public JlRegion RegiongrowingN(string metric, double minTolerance, double maxTolerance, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(445);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, metric);
		JlNativeApi.StoreD(proc, 1, minTolerance);
		JlNativeApi.StoreD(proc, 2, maxTolerance);
		JlNativeApi.StoreI(proc, 3, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>栅格播种的区域生长（元组容差版）。</summary>
	/// <param name="rasterHeight">播种点的行间距。Default: 3</param>
	/// <param name="rasterWidth">播种点的列间距。Default: 3</param>
	/// <param name="tolerance">允许并入同一区域的灰度差上限。Default: 6.0</param>
	/// <param name="minSize">输出区域的最小像素数。Default: 100</param>
	/// <returns>生长得到的区域；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 446。按 <paramref name="rasterHeight"/>×<paramref name="rasterWidth"/>
	///   的栅格取候选点做生长；参数说明写明"灰度差不超过 <paramref name="tolerance"/> 的点归入同一区域"，
	///   即上界取等号。参数说明用"accumulated into the same object"描述并入过程，因此缓慢渐变的区域有可能被
	///   逐级并成一大块（与 <c>Threshold</c> 按绝对灰度切的结果不可互相替换）[待实测]。</para>
	///   <para><b>栅格是速度来源，也是漏检来源</b>播种点之间隔 <paramref name="rasterWidth"/> 列，
	///   小于栅格间距的结构根本没有播种点，会<b>整块不出现在结果里</b>（不是变小，是没有）。
	///   默认 3×3 适合做背景分块；要找小缺陷请把栅格降到 1 并改用 <c>Threshold</c>/<c>VarThreshold</c>，
	///   靠生长找小目标既慢又不可复现。</para>
	///   <para><b>坑</b>小于 <paramref name="minSize"/>（像素个数，<c>int</c>）的区域被丢弃且不并邻，
	///   结果不覆盖全图；<paramref name="rasterHeight"/>/<paramref name="rasterWidth"/> 大于图像尺寸时
	///   本层不做检查，播种点数为 0 时返回什么由原生决定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>多通道一致性 → <see cref="RegiongrowingN(string,double,double,int)"/>；
	///   已知种子坐标、要求"块内围绕均值一致" → <see cref="RegiongrowingMean(JlTuple,JlTuple,double,int)"/>。
	///   本算子不需要种子，适合"把图自动切成若干块"的分块场景，不适合按目标找目标。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion blocks = img.Regiongrowing(3, 3, new JlTuple(6.0), 100);
	///   int n = blocks.CountObj();
	///   using JlRegion cover = blocks.Union1();            // 生长结果有空隙，需要覆盖全图时自己并起来
	///   </code>
	///   <para><b>资源与坑</b>元组版对 <paramref name="tolerance"/> 固定/解固定；单值请用
	///   <see cref="Regiongrowing(int,int,double,int)"/>。</para>
	/// </remarks>
	public JlRegion Regiongrowing(int rasterHeight, int rasterWidth, JlTuple tolerance, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(446);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, rasterHeight);
		JlNativeApi.StoreI(proc, 1, rasterWidth);
		JlNativeApi.Store(proc, 2, tolerance);
		JlNativeApi.StoreI(proc, 3, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tolerance);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>栅格播种的区域生长（单值容差版）。</summary>
	/// <param name="rasterHeight">播种点行间距。Default: 3</param>
	/// <param name="rasterWidth">播种点列间距。Default: 3</param>
	/// <param name="tolerance">灰度差上限。Default: 6.0</param>
	/// <param name="minSize">最小区域像素数。Default: 100</param>
	/// <returns>区域数组。</returns>
	/// <remarks>
	///   <para>栅格漏检、<c>minSize</c> 空隙等结论见 <see cref="Regiongrowing(int,int,JlTuple,int)"/>：同一原生 id 446，
	///   本版本用 <c>StoreD</c> 直写容差，无固定/解固定开销，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion blocks = img.Regiongrowing(3, 3, 6.0, 100);
	///   int n = blocks.CountObj();
	///   </code>
	/// </remarks>
	public JlRegion Regiongrowing(int rasterHeight, int rasterWidth, double tolerance, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(446);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, rasterHeight);
		JlNativeApi.StoreI(proc, 1, rasterWidth);
		JlNativeApi.StoreD(proc, 2, tolerance);
		JlNativeApi.StoreI(proc, 3, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>从给定种子点出发、按"与区域均值的偏差"生长（元组种子版）。</summary>
	/// <param name="startRows">种子点行坐标，与 <paramref name="startColumns"/> 一一对应。Default: []</param>
	/// <param name="startColumns">种子点列坐标。Default: []</param>
	/// <param name="tolerance">像素与区域均值的最大允许偏差。Default: 5.0</param>
	/// <param name="minSize">小于该像素数的区域不输出。Default: 100</param>
	/// <returns>每个合格种子长出的区域；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 447。与前两个生长算子的根本区别：判据是<b>像素与"区域均值"的偏差</b>
	///   （参数说明 Maximum deviation from the mean），而 <see cref="Regiongrowing(int,int,JlTuple,int)"/> 用的是相邻像素差。
	///   均值随并入的像素更新，因此 <paramref name="tolerance"/> 给大时区域会顺着渐变一路吞下去；
	///   给小时区域停在纹理边界，面积对 <paramref name="tolerance"/> 极其敏感，建议按批次直方图重新定值而不是沿用。</para>
	///   <para><b>种子决定结果</b>只有能长成不小于 <paramref name="minSize"/> 的种子才会出现在输出里，
	///   一个种子可能长成多个区域也可能一个都不长（长度与顺序都不保证与种子一一对应）[待实测]，
	///   所以<b>不要按下标把输出区域当成对应种子</b>，需要对应关系时用 <c>TestSubsetRegion</c> 一类包含判断。
	///   两个坐标元组按下标配对，长度不等时多余部分如何处理本层不校验 [待实测]；
	///   默认值是<b>空元组</b>，即不给种子：本层不会替你报错，结果大概率为空区域 [待实测]。</para>
	///   <para><b>坐标顺序</b><paramref name="startRows"/> 在前、<paramref name="startColumns"/> 在后，
	///   与常见的 (column,row) 图像坐标习惯相反；由标定/模板得到的 x,y 记得交换。</para>
	///   <para><b>什么时候用它</b>已知目标大概位置（上一工位、标定 ROI、手工点选），要"以该点为中心把这块东西完整捞出来"。
	///   不知道位置就别用它：全图撒种子比不过 <c>Threshold</c>+<c>Connection</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlTuple rows = new JlTuple(120.0, 340.0);
	///   JlTuple cols = new JlTuple(80.0, 500.0);          // 两个种子，元组等长成对给出
	///   using JlRegion parts = img.RegiongrowingMean(rows, cols, 5.0, 100);
	///   int n = parts.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>两个坐标元组各做固定与 <c>UnpinTuple</c>；返回区域需释放。</para>
	/// </remarks>
	public JlRegion RegiongrowingMean(JlTuple startRows, JlTuple startColumns, double tolerance, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(447);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, startRows);
		JlNativeApi.Store(proc, 1, startColumns);
		JlNativeApi.StoreD(proc, 2, tolerance);
		JlNativeApi.StoreI(proc, 3, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(startRows);
		JlNativeApi.UnpinTuple(startColumns);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>从单个种子点按均值偏差生长（整数坐标版）。</summary>
	/// <param name="startRows">种子点行坐标（单个）。Default: []</param>
	/// <param name="startColumns">种子点列坐标（单个）。Default: []</param>
	/// <param name="tolerance">与区域均值的最大偏差。Default: 5.0</param>
	/// <param name="minSize">最小区域像素数。Default: 100</param>
	/// <returns>长出的区域。</returns>
	/// <remarks>
	///   <para>算法与"种子数与输出区域数不对应"等注意事项见 <see cref="RegiongrowingMean(JlTuple,JlTuple,double,int)"/>：
	///   同一原生 id 447，<paramref name="tolerance"/> 在两个重载里都是 <c>double</c>。</para>
	///   <para><b>实际差异（不只是省一个元组）</b>种子坐标经 <c>StoreI</c> 按<b>单个整数</b>传入，
	///   本重载只能给一个种子点，坐标也无法取半像素；多种子必须用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion part = img.RegiongrowingMean(120, 80, 5.0, 100);
	///   double area = part.RegionFeatures("area");
	///   </code>
	/// </remarks>
	public JlRegion RegiongrowingMean(int startRows, int startColumns, double tolerance, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(447);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, startRows);
		JlNativeApi.StoreI(proc, 1, startColumns);
		JlNativeApi.StoreD(proc, 2, tolerance);
		JlNativeApi.StoreI(proc, 3, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>往灰度高程面上"灌水"，取指定水位下的连通洼地，返回区域。</summary>
	/// <param name="mode">运算模式；字符串取值不经本层校验。Default: "all"</param>
	/// <param name="minGray">低于此灰度的像素被忽略。Default: 0</param>
	/// <param name="maxGray">高于此灰度的像素被忽略。Default: 255</param>
	/// <returns>新区域句柄（数组）；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 448，一路图标输出（<c>InitOCT(proc,1)</c> + <c>JlRegion.LoadNew</c>）。
	///   原生侧参数序是 <c>mode</c>→槽 0、<c>minGray</c>→槽 1、<c>maxGray</c>→槽 2，图像本身走 <c>Store(proc,1,...)</c>，
	///   即 C# 形参序与原生序一致。<paramref name="minGray"/>/<paramref name="maxGray"/> 先把灰度量裁成一段，
	///   再在裁出的面上做灌水 [待实测：裁剪与灌水的先后是否如字面所述]。</para>
	///   <para><b>前提（本算子最容易被误用的点）</b>它把灰度当地形高程，因此输入应当是<b>高程类图像</b>：
	///   距离变换结果、<see cref="WatershedsThreshold(JlTuple)"/>/<see cref="Watersheds(out JlRegion)"/> 系列所依赖的那类梯度/盆地图，
	///   或直接拿 <c>byte</c> 拍摄图跑，得到的洼地由纹理噪声决定，与目标无关 [待实测：典型区域数量级]。
	///   应为单通道图，本层不查通道数。</para>
	///   <para><b>取值约定</b>两个界限是 <c>int</c>（<c>StoreI</c>）：本方法<b>没有</b> double 或元组重载，
	///   所以在 <c>float</c>/<c>direction</c> 这类需要小数阈值的图上只能取整，且默认 <c>255</c> 会把
	///   <c>uint2</c>/<c>float</c> 图的高灰度全部裁掉——这与 <c>Threshold</c> 上写死 255.0 是同一类静默丢像素问题。
	///   <paramref name="mode"/> 的可选值不经本层校验，写错由原生端报错 [待实测：完整取值列表]。</para>
	///   <para><b>与相邻算子的取舍</b>只想按浸水深度合并盆地用 <see cref="WatershedsThreshold(JlTuple)"/>；
	///   想同时拿分水线用 <see cref="Watersheds(out JlRegion)"/>；已经有标记（种子）时用
	///   <see cref="WatershedsMarker(JlRegion)"/>，它是控制过分割最直接的一档。三者与本算子同为"灰度即高程"的思路，差别在控制手段。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion basins = img.Pouring("all", 0, 255);
	///   int n = basins.CountObj();                              // 先量规模再决定怎么筛
	///   </code>
	///   <para><b>资源与坑</b>返回区域数组句柄，由调用者释放；末尾 <c>GC.KeepAlive(this)</c>。
	///   过分割时区域数很大，逐区域统计前先 <c>CountObj()</c> 判断规模。</para>
	/// </remarks>
	public JlRegion Pouring(string mode, int minGray, int maxGray)
	{
		IntPtr proc = JlNativeApi.PreCall(448);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, minGray);
		JlNativeApi.StoreI(proc, 2, maxGray);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按阈值提取分水盆地（暗盆地，元组阈值版）。</summary>
	/// <param name="threshold">分水阈值。Default: 10</param>
	/// <returns>找到的分段（暗盆地）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 449，一路区域输出。英文说明把输出明确为 dark basins：
	///   灰度被当成高程面，暗处是盆地，<paramref name="threshold"/> 控制"浸水深度"，决定相邻盆地合并到什么程度 [待实测：确切判据]。
	///   输入应是单通道灰度图；本层不检查通道数 [待实测]。</para>
	///   <para><b>输出规模</b>分水类算子天然过分割：纹理多的图上区域数可达几千，直接 <c>Connection()</c>+逐个统计会拖死节拍。
	///   先 <c>CountObj()</c> 看规模，再按面积/灰度筛（<c>SelectShape</c>）。</para>
	///   <para><b>与相邻算子的取舍</b>要"分水线"本身而不是盆地 → <see cref="Watersheds(out JlRegion)"/>；
	///   已经知道每个目标该有一个种子（标记）→ <see cref="WatershedsMarker(JlRegion)"/>，它是控制过分割最直接的手段。
	///   只是想按灰度分层，不要用水分：用 <c>Threshold</c>/<c>AutoThreshold</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion basins = img.WatershedsThreshold(new JlTuple(10.0));
	///   int n = basins.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回区域数组句柄，需释放；元组版做固定/解固定，单值请用
	///   <see cref="WatershedsThreshold(int)"/>。</para>
	/// </remarks>
	public JlRegion WatershedsThreshold(JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(449);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(threshold);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按阈值提取分水盆地（整数阈值版）。</summary>
	/// <param name="threshold">分水阈值。Default: 10</param>
	/// <returns>暗盆地区域数组。</returns>
	/// <remarks>
	///   <para>算法、过分割与三个分水算子之间的取舍见 <see cref="WatershedsThreshold(JlTuple)"/>：同一原生 id 449。</para>
	///   <para><b>实际差异</b>阈值经 <c>StoreI</c> 作整数传，<c>float</c>/<c>direction</c> 图上需要非整数阈值时用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion basins = img.WatershedsThreshold(10);
	///   </code>
	/// </remarks>
	public JlRegion WatershedsThreshold(int threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(449);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>一次算出盆地与盆地之间的分水线（两路区域输出）。</summary>
	/// <param name="watersheds">回传：盆地之间的分水线区域。</param>
	/// <returns>分割出的盆地。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 450，<b>没有</b>任何控制参数：代码里两次 <c>InitOCT</c>（槽位 1 与 2）
	///   声明两个图标输出，返回值是盆地（<c>LoadNew(proc,1,...)</c>），<c>out</c> 参数是分水线
	///   （<c>LoadNew(proc,2,...)</c>）。两者都是区域，容易搞混：要"目标块"用返回值，要"边界线"用 <paramref name="watersheds"/>。</para>
	///   <para><b>无参数的代价</b>没有阈值、没有标记，分割粒度完全由图像本身的灰度极小值决定，
	///   因此在噪声/纹理图上会严重过分割 [待实测：典型区域数量级]。需要控制粒度时改用
	///   <see cref="WatershedsThreshold(JlTuple)"/>（按深度合并）或 <see cref="WatershedsMarker(JlRegion)"/>（按标记生长）。</para>
	///   <para><b>典型用法</b>把分水线当作"减法"用：先分割出粘连块，再用 <c>Difference</c> 从块里挖掉分水线，
	///   让粘连目标在像素级分开。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion basins = img.Watersheds(out JlRegion lines);
	///   int n = basins.CountObj();
	///   lines.Dispose();                                   // 两个输出都要释放
	///   </code>
	///   <para><b>资源与坑</b><paramref name="watersheds"/> 必须写成 <c>out JlRegion x</c>（按值传会 CS1615）；
	///   两路输出互不隶属，只释放其中一个会漏掉另一个的句柄。</para>
	/// </remarks>
	public JlRegion Watersheds(out JlRegion watersheds)
	{
		IntPtr proc = JlNativeApi.PreCall(450);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlRegion.LoadNew(proc, 2, err, out watersheds);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>为有符号（含负值）图像做的阈值分割：按正负两侧同时抠出区域。</summary>
	/// <param name="minSize">像素数小于该值的区域被丢弃。Default: 20</param>
	/// <param name="minGray">最大绝对灰度小于该值的区域被丢弃。Default: 5.0</param>
	/// <param name="threshold">绝对灰度落在 ±<paramref name="threshold"/> 之内的像素被丢弃。Default: 2.0</param>
	/// <returns>新区域句柄（正、负两类区域一起给）；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 453，一路区域输出。原生侧参数序与 C# 形参序一致：
	///   <c>minSize</c>→槽 0（<c>StoreI</c>）、<c>minGray</c>→槽 1、<c>threshold</c>→槽 2（均 <c>StoreD</c>）。
	///   它是"先按灰度挑像素、再按区域大小/峰值二次筛"的一体化算子：后两道筛发生在区域层面，
	///   所以不用再自己跑 <c>Connection()</c> + 面积筛选。</para>
	///   <para><b>必须用在有符号图上</b><paramref name="minGray"/> 与 <paramref name="threshold"/> 都按<b>绝对值</b>判定，
	///   这正是它相对 <c>Threshold</c> 的存在意义：<c>Threshold</c> 只能表达一个灰度区间，在负值占大半量程的
	///   <c>signed</c>/<c>float</c> 图上会把一侧整个丢掉。有符号图的常规来源是 <see cref="SubImage(JlImage,double,double)"/>
	///   的残差或 <see cref="Laplace(string,int,string)"/> 这类带符号响应的结果；本层不检查图像类型，
	///   拿 <c>byte</c> 图来用不报错，只是"负半轴"那侧永远为空 [待实测：非 signed 图上的具体行为]。</para>
	///   <para><b>三个参数的取向</b><paramref name="threshold"/> 是对称的：绝对值小于它的像素一律不要，
	///   因此零附近的弱响应被整片丢掉，正负两侧却用同一个门限，无法分开调；
	///   <paramref name="minGray"/> 卡的是区域峰值（绝对值），一个又小又强的区域能过、又大又平的通不过；
	///   <paramref name="minSize"/> 是 <c>int</c> 像素数，与图像分辨率直接相关，换相机后要重标 [待实测：是否含边界值]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage golden = new JlImage("byte", 640, 480);
	///   using JlImage diff = img.SubImage(golden, 1.0, 0.0);            // 有符号残差
	///   using JlRegion defects = diff.DualThreshold(20, 5.0, 2.0);
	///   int n = defects.CountObj();                                     // 正负区域混在同一个数组里
	///   </code>
	///   <para><b>资源与坑</b>返回句柄归调用者释放。正负两类区域被混在同一个输出数组中，
	///   按下标取第几个时不要假定"先正后负"这种稳定顺序，需按各自灰度极值自行归类 [待实测：输出排列规则]。
	///   末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlRegion DualThreshold(int minSize, double minGray, double threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(453);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, minSize);
		JlNativeApi.StoreD(proc, 1, minGray);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>从一条种子行/列线出发按停止判据向两侧扩张，把图切成若干区域（元组阈值版）。</summary>
	/// <param name="coordinate">种子线所在的行号或列号。Default: 256</param>
	/// <param name="expandType">扩张的停止判据。Default: "gradient"</param>
	/// <param name="rowColumn">把种子线当横（row）还是竖（column）线。Default: "row"</param>
	/// <param name="threshold">扩张阈值。Default: 3.0</param>
	/// <returns>新区域句柄（多个条带区域）；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 454，一路区域输出。槽位序为 <c>coordinate</c>→0（<c>StoreI</c>）、
	///   <c>expandType</c>→1、<c>rowColumn</c>→2（<c>StoreS</c>）、<c>threshold</c>→3。
	///   它不是"把已有区域放大"，而是<b>以一条线为种子做区域生长</b>：从第 <paramref name="coordinate"/> 行（或列）出发，
	///   按 <paramref name="expandType"/> 的判据一路吞掉相邻像素，判据不满足即停，最终把整幅图切成若干条带。</para>
	///   <para><b>参数之间的耦合（最容易错的地方）</b><paramref name="coordinate"/> 的<b>含义由
	///   <paramref name="rowColumn"/> 决定</b>：值为 <c>"row"</c> 时它是行号（沿 y 方向，向下为正），
	///   值为 <c>"column"</c> 时它是列号（沿 x 方向，向右为正）。本层不做范围检查，
	///   超出图像尺寸或写成负数时由原生端决定行为 [待实测]；坐标下标从 0 还是 1 起本层看不出来 [待实测]。
	///   <paramref name="expandType"/> 与 <paramref name="rowColumn"/> 都是字符串，取值不经校验。</para>
	///   <para><b>输出是区域数组且顺序由生长过程决定</b>条带数量取决于图像内容与停止判据，不是固定值：
	///   用之前必须 <c>CountObj()</c> 确认数量；两幅内容不同的图给出的条带不能按下标直接对齐
	///   （这与 <c>Connection()</c> 的顺序不稳定问题同源），要按位置筛选请读各区域的行/列范围再排序。</para>
	///   <para><b>什么时候不该用它</b>目标是按连通性分开时用 <c>Connection()</c>；目标是按灰度分层时用 <c>Threshold</c>。
	///   本算子的价值在于条带是"沿一个方向延伸"的结构（例如按行把织物/条码状目标切开），
	///   在 <paramref name="expandType"/> 为梯度判据时，若种子线两侧对比度很低会一路扩到图像边界，
	///   表现为"只得到一个覆盖全图的区域" [待实测：确切停止条件]。</para>
	///   <para><b>参数取向</b>元组版可给 <paramref name="threshold"/> 多个值（多判据/分段取值的语义本层无法判断 [待实测]），
	///   代价是每次固定与 <c>UnpinTuple</c>；单值请用 <see cref="ExpandLine(int,string,string,double)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion bands = img.ExpandLine(240, "gradient", "row", new JlTuple(3.0));
	///   int n = bands.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；末尾 <c>GC.KeepAlive(this)</c>。
	///   默认 <c>256</c> 只是模板值，要求图像在该方向上确实有这一行/列，示例里换成了按 480 行取的 240。</para>
	/// </remarks>
	public JlRegion ExpandLine(int coordinate, string expandType, string rowColumn, JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(454);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, coordinate);
		JlNativeApi.StoreS(proc, 1, expandType);
		JlNativeApi.StoreS(proc, 2, rowColumn);
		JlNativeApi.Store(proc, 3, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(threshold);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>从种子线扩张分段的单阈值版。</summary>
	/// <param name="coordinate">种子线所在的行号或列号。Default: 256</param>
	/// <param name="expandType">扩张的停止判据。Default: "gradient"</param>
	/// <param name="rowColumn">把种子线当横（row）还是竖（column）线。Default: "row"</param>
	/// <param name="threshold">扩张阈值。Default: 3.0</param>
	/// <returns>新区域句柄（多个条带区域）。</returns>
	/// <remarks>
	///   <para>生长方式、<paramref name="coordinate"/> 随 <paramref name="rowColumn"/> 变义、条带数量与顺序不可假定等问题见
	///   <see cref="ExpandLine(int,string,string,JlTuple)"/>：两个重载同走原生 id 454，输出路径完全一致。</para>
	///   <para><b>实际差异</b><paramref name="threshold"/> 经 <c>StoreD</c> 直写槽位 3，不做元组固定/解固定，
	///   单次分割用它更省；换来的限制是只能给一个阈值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion bands = img.ExpandLine(240, "gradient", "row", 3.0);
	///   int n = bands.CountObj();                            // 条带数由图像内容决定，先量再按下标取
	///   </code>
	///   <para><b>资源与坑</b>返回区域数组句柄需释放；末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlRegion ExpandLine(int coordinate, string expandType, string rowColumn, double threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(454);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, coordinate);
		JlNativeApi.StoreS(proc, 1, expandType);
		JlNativeApi.StoreS(proc, 2, rowColumn);
		JlNativeApi.StoreD(proc, 3, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>找出所有比邻域都低的像素，以区域形式给出（无参数的极值检测）。</summary>
	/// <returns>新区域句柄，一个连通极值一块；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 455，本层<b>不传任何控制参数</b>（方法体里只有 <c>Store(proc,1)</c> 加
	///   一路 <c>InitOCT</c>/<c>LoadNew</c>）：判据完全由原生端按邻域比较决定，即"比它周围都低"的像素被选中。</para>
	///   <para><b>没有参数意味着什么</b>尺度、容差、最小面积一概不可调，所以它对噪声零抵抗：
	///   未滤波的实拍图上几乎每个噪声暗点都会成为一个区域，区域数可达像素数量的同数量级 [待实测：典型规模]。
	///   想控制粒度只能先对输入图做尺度滤波，例如 <see cref="RankRect(int,int,int)"/> 或
	///   <see cref="GaussImage(int)"/>，再在滤波结果上调用本算子。</para>
	///   <para><b>与相邻算子的取舍</b>要"低洼的成片区域"用 <see cref="Lowlands()"/>；要盆地随浸水深度合并用
	///   <see cref="WatershedsThreshold(JlTuple)"/>；只要一个固定灰度以下用 <c>Threshold</c>。
	///   本算子给的是极值点级的小块，适合当种子/标记（配合 <see cref="WatershedsMarker(JlRegion)"/>），
	///   不适合当目标本身——它的面积没有度量意义。</para>
	///   <para><b>输入前提</b>应为单通道灰度图，本层不检查通道数 [待实测]；<c>byte</c> 图上大片饱和到 0 的区域
	///   是否被当成极值 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage smooth = img.RankRect(5, 5, 12);            // 先压掉孤立噪声点
	///   using JlRegion minima = smooth.LocalMin();
	///   int n = minima.CountObj();                                 // 数量即极值块数，务必先检查规模
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄归调用者释放；末尾 <c>GC.KeepAlive(this)</c>。
	///   逐区域统计前若不做 <c>CountObj()</c> 判规模，很容易在一次调用里处理上万个碎块。</para>
	/// </remarks>
	public JlRegion LocalMin()
	{
		IntPtr proc = JlNativeApi.PreCall(455);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>找出全部灰度洼地（每个洼地一个区域），无控制参数。</summary>
	/// <returns>新区域句柄（洼地数组）；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 456。它与 <see cref="LocalMin()"/> 的区别不是"符号相反"，而是<b>粒度</b>：
	///   极值检测给的是比邻域都低的点，本算子给的是连成片的低洼域（一个洼地一个区域），
	///   因此区域数少于极值检测，但同样不可控。</para>
	///   <para><b>输入前提</b>和所有"灰度即高程"的算子一样，输入应是高程型图像（距离变换、梯度面、
	///   <see cref="Laplace(string,int,string)"/> 一类响应的结果）；直接拿普通拍摄图调用，
	///   洼地由纹理与阴影决定而不是由目标决定 [待实测：byte 拍摄图上的典型区域数]。本层不检查通道数。</para>
	///   <para><b>不可调之处</b>方法签名没有任何参数（本层只有 <c>Store(proc,1)</c> 与一路区域输出），
	///   所以合并深度、最小面积都改不了；要按"浸水深度"控制粒度必须换 <see cref="WatershedsThreshold(JlTuple)"/>，
	///   要按已知目标个数控制就换 <see cref="WatershedsMarker(JlRegion)"/>。</para>
	///   <para><b>顺序状态依赖</b>洼地之间的排列由原生端扫描/生长过程决定，本层不做任何排序；
	///   两帧内容不同的图之间按 <c>[i]</c> 对齐会静默错位，请改用几何量（重心、行范围）配对，
	///   或先取到 <c>JlRegion</c> 再调其 <c>SortRegion(JlTuple,string,string)</c> 显式排序。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion pits = img.Lowlands();
	///   int n = pits.CountObj();                                // 洼地数不可控，先量规模
	///   </code>
	///   <para><b>资源与坑</b>返回区域数组句柄，由调用者释放；末尾 <c>GC.KeepAlive(this)</c>。
	///   要洼地的中心位置而不是整片，请用 <see cref="LowlandsCenter()"/>。</para>
	/// </remarks>
	public JlRegion Lowlands()
	{
		IntPtr proc = JlNativeApi.PreCall(456);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把每个灰度洼地压缩成其重心区域（洼地的"点"版本）。</summary>
	/// <returns>新区域句柄，每个洼地一个重心块；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 457，与 <see cref="Lowlands()"/>（id 456）同族：本算子返回的不是洼地本身，
	///   而是每个洼地的<b>重心</b>（英文说明为 centers of gravity）——一个洼地对应一个小区域。</para>
	///   <para><b>取舍（关键差异）</b>要"洼地有多大、形状如何"时必须用 <see cref="Lowlands()"/>，
	///   重心版本已经把面积与形状丢了，再算 <c>RegionFeatures("area")</c> 得到的是重心块的面积，
	///   与洼地面积无关，这是本方法最常见的误读。要定位点（阵列标记、待测点的候选位置）时才用它。</para>
	///   <para><b>输入前提</b>与洼地类算子一样要求灰度即高程（距离变换/梯度型图像），
	///   普通拍摄图上得到的重心只是纹理极小处 [待实测]；本层不检查通道数。同样<b>没有任何参数</b>可调，
	///   粒度只能靠输入预处理控制。</para>
	///   <para><b>顺序状态依赖</b>重心与洼地一一对应，但对应关系靠输出排列维持；本层不排序，
	///   两帧之间按 <c>[i]</c> 配对不可靠。需要稳定次序时先把结果交给 <c>JlRegion</c> 的
	///   <c>SortRegion(JlTuple,string,string)</c> 按几何量排序后再配对。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion centers = img.LowlandsCenter();
	///   int n = centers.CountObj();                               // 重心块数 = 洼地数
	///   </code>
	///   <para><b>资源与坑</b>返回区域数组句柄需释放；末尾 <c>GC.KeepAlive(this)</c>。
	///   想把这些点当标记去长区域，接 <see cref="WatershedsMarker(JlRegion)"/>。</para>
	/// </remarks>
	public JlRegion LowlandsCenter()
	{
		IntPtr proc = JlNativeApi.PreCall(457);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>找出所有比邻域都高的像素，以区域形式给出（无参数的极大值检测）。</summary>
	/// <returns>新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 458，与 <see cref="LocalMin()"/>（id 455）互为镜像：本层同样不传任何控制参数，
	///   只有一路 <c>InitOCT</c>/<c>JlRegion.LoadNew</c> 的区域输出。注意英文说明把本算子的输出写成单数
	///   （"as a region"）而 <c>LocalMin</c>/<c>Lowlands</c> 写成复数，是否真会合并成一个对象本层无法判断，
	///   用 <c>CountObj()</c> 确认 [待实测]。</para>
	///   <para><b>没有参数意味着什么</b>尺度与容差不可调，噪声亮点逐个成为区域，区域数随噪声密度线性增长 [待实测：典型规模]；
	///   要控制粒度只能先在输入上做 <see cref="RankRect(int,int,int)"/>/<see cref="GaussImage(int)"/> 一类的尺度滤波。</para>
	///   <para><b>与相邻算子的取舍</b>检"比局部平均亮若干的亮斑"更该用
	///   <see cref="DynThreshold(JlImage,double,string)"/>（阈值图来自 <c>MeanImage</c>/<c>GaussImage</c>，
	///   偏移量连续可调）或 <see cref="VarThreshold(int,int,double,double,string)"/>，它们能给出与目标大小相关的区域；
	///   要成片的高地用 <see cref="Plateaus()"/>。本算子的合适用途是产生<b>种子/标记点</b>
	///   （喂给 <see cref="WatershedsMarker(JlRegion)"/>），而不是产生被测区域。</para>
	///   <para><b>输入前提</b>单通道灰度图；本层不检查通道数 [待实测]。大面积纯白（饱和）区与极值判据的关系 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage smooth = img.RankRect(5, 5, 13);             // 接近最大值：压掉暗噪点
	///   using JlRegion maxima = smooth.LocalMax();
	///   int n = maxima.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；末尾 <c>GC.KeepAlive(this)</c>；逐区域处理前先判规模。</para>
	/// </remarks>
	public JlRegion LocalMax()
	{
		IntPtr proc = JlNativeApi.PreCall(458);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>找出全部灰度平台（每片平坦高区一个区域），无控制参数。</summary>
	/// <returns>新区域句柄（平台数组）；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 459，是 <see cref="Lowlands()"/>（id 456）的高程镜像：
	///   灰度当高程，"平台"是一片不再上升的平坦区域，每个平台输出一个区域。</para>
	///   <para><b>最容易踩的边界行为</b>它按<b>灰度平坦度</b>识别，不按键值大小。于是背景只要有一片完全均匀的灰度
	///   （合成图、被 <see cref="MeanImage(int,int)"/>/<see cref="GaussImage(int)"/> 抹平的区、传感器饱和平台），
	///   就会整片成为一个平台，输出里出现一个覆盖半个图幅的区域；这与 <c>Threshold</c> 那种"按值切"的行为完全不同。
	///   因此结果用前必须按面积筛，别假定每个区域都是一个目标。</para>
	///   <para><b>不可调之处</b>签名无参数（本层只有 <c>Store(proc,1)</c> 与一路区域输出），平台最小尺寸、
	///   容差都改不了；只想取"比邻域都高的点"用 <see cref="LocalMax()"/>，要按浸水深度合并盆地用
	///   <see cref="WatershedsThreshold(JlTuple)"/>。要控制粒度只能靠输入预处理。</para>
	///   <para><b>输入前提与顺序</b>应为单通道高程型灰度图，本层不检查通道数 [待实测]；
	///   区域排列由原生端决定，本层不排序，跨帧按下标对齐不可靠。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion flats = img.Plateaus();
	///   int n = flats.CountObj();                                 // 大片均匀背景会整片成为一个平台
	///   </code>
	///   <para><b>资源与坑</b>返回区域数组句柄由调用者释放；末尾 <c>GC.KeepAlive(this)</c>。
	///   只要平台中心位置时用 <see cref="PlateausCenter()"/>。</para>
	/// </remarks>
	public JlRegion Plateaus()
	{
		IntPtr proc = JlNativeApi.PreCall(459);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把每个灰度平台压缩成其重心区域（平台的"点"版本）。</summary>
	/// <returns>新区域句柄，每个平台一个重心块；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 460，与 <see cref="Plateaus()"/>（id 459）的关系同
	///   <see cref="LowlandsCenter()"/> 之于 <see cref="Lowlands()"/>：返回的是每个平台的重心，一片平台一个点块。</para>
	///   <para><b>取舍</b>需要平台的大小/形状/平均灰度时<b>不要</b>用它——重心块已经把原平台的面积丢了，
	///   在重心结果上做的面积统计与本算子的物理含义无关。它适合把平台当定位点用；
	///   需要"点"但希望更细的粒度时用 <see cref="LocalMax()"/>（点级，不带成片合并）。</para>
	///   <para><b>边界行为</b>与平台类判据一致：大片均匀背景会整片成为一个平台，于是它也会给出一个"背景中心"点，
	///   必须按面积或按灰度把这些巨块对应的点筛掉。</para>
	///   <para><b>前提与顺序</b>单通道灰度图（本层不检查通道数 [待实测]）；无任何参数可调；
	///   输出排列不保证稳定次序，跨帧按下标对齐不可靠。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion dots = img.PlateausCenter();
	///   int n = dots.CountObj();                                  // 点数 = 平台数，含背景巨块的中心
	///   </code>
	///   <para><b>资源与坑</b>返回区域数组句柄需释放；末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlRegion PlateausCenter()
	{
		IntPtr proc = JlNativeApi.PreCall(460);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>由直方图自动确定若干灰度区间并分割（元组版）。</summary>
	/// <param name="sigma">直方图高斯平滑量。Default: 2.0</param>
	/// <returns>各自动确定区间内的区域。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 462。对<b>直方图</b>（不是图像）做高斯平滑后找峰，按峰间位置切出多个灰度区间。
	///   参数说明里的 <c>sigma</c> 作用在直方图索引上，与空间分辨率无关：换相机或改图像尺寸不会改变它的含义，
	///   但改图像类型（<c>byte</c> 256 级 vs <c>uint2</c> 65536 级）会让同一个 <c>sigma</c> 对应完全不同的灰度跨度 [待实测]。</para>
	///   <para><b>输出形状</b>返回值可能是<b>区域数组</b>（每个区间一个区域），也可能是单个区域，本层只经
	///   <c>JlRegion.LoadNew(proc,1,...)</c> 取回一个句柄对象；用 <c>CountObj()</c> 判断段数再决定按索引取
	///   （<c>SelectObj</c>）还是整体处理。假设它只出一段是这个算子最常见的写法错误。</para>
	///   <para><b>什么时候该用它</b>灰度级数已知、目标与背景双峰明显、但不想手调阈值时。<b>不该</b>用它的情况：
	///   需要固定量纲的判定标准（它每帧重算区间，标准会漂）；直方图单峰或近似均匀（切出来的区间由噪声峰决定，
	///   结果逐帧抖动）；需要可控的区间数（区间数由峰数决定，不由你决定）。</para>
	///   <para><b>与 <c>BinaryThreshold</c> 的取舍</b>只要前景/背景两类且想知道阈值时优先 <c>BinaryThreshold</c>（它会回报阈值）；
	///   要按多级灰度分层（如料堆高度分级）时用本算子。调 <c>sigma</c> 的方向：调大→峰更少→区间更少。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion bands = img.AutoThreshold(new JlTuple(2.0));
	///   int numBands = bands.CountObj();
	///   using JlRegion first = bands.SelectObj(1);
	///   </code>
	///   <para><b>资源与坑</b>数组切片 <c>SelectObj</c> 又会产生新句柄，逐层都要释放；元组版有固定/解固定开销。</para>
	/// </remarks>
	public JlRegion AutoThreshold(JlTuple sigma)
	{
		IntPtr proc = JlNativeApi.PreCall(462);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, sigma);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sigma);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>直方图自动分区间（单值版）。</summary>
	/// <param name="sigma">直方图高斯平滑量。Default: 2.0</param>
	/// <returns>各区间对应的区域（可能为区域数组）。</returns>
	/// <remarks>
	///   <para>算法、"输出可能是区域数组"这一点与不该用它的场景见 <see cref="AutoThreshold(JlTuple)"/>：
	///   同一原生 id 462，本版本用 <c>StoreD</c> 直写单个 <c>sigma</c>，无元组固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion bands = img.AutoThreshold(2.0);
	///   int numBands = bands.CountObj();
	///   </code>
	/// </remarks>
	public JlRegion AutoThreshold(double sigma)
	{
		IntPtr proc = JlNativeApi.PreCall(462);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>全自动单阈值分割（无任何参数，固定给暗区）。</summary>
	/// <returns>新区域句柄（暗部前景）；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 463，方法体里除了 <c>Store(proc,1)</c> 与一路
	///   <c>InitOCT</c>/<c>JlRegion.LoadNew</c> 之外没有任何参数写入——阈值由原生端按本帧统计自动决定。
	///   英文说明写明输出是 dark regions，即前景取的是"暗的那一侧"；要亮目标得自己做差集或换
	///   <see cref="BinaryThreshold(string,string,out JlTuple)"/>（它有 <c>lightDark</c> 可选）。</para>
	///   <para><b>零参数的代价（会静默错的地方）</b>①阈值不回传：同一批次里想复用上一次的阈值、或者把阈值当报警量监控，
	///   本方法做不到，必须换 <c>BinaryThreshold(..., out JlTuple)</c>；②每帧阈值各算各的，
	///   光源波动、工件换批会让直方图形态变化，区域面积随之漂移却不报任何错——这是它最典型的失效方式；
	///   ③判据不可选，是否为 <c>"max_separability"</c> 由原生端固定 [待实测：与 id 439 哪一档预设等价]。</para>
	///   <para><b>与相邻算子的取舍</b>已知阈值用 <c>Threshold</c>（可复现、可控）；想同时拿到所用阈值用
	///   <c>BinaryThreshold</c>；要按多个区间分层用 <c>AutoThreshold</c>（id 462，直方图多峰切分，
	///   且 <c>sigma</c> 可调）；光照不均时它们都不合适，改用 <c>DynThreshold</c>/<c>VarThreshold</c>。
	///   本方法是"只想先看一眼能不能分开"的探索档，不建议留在量产流程里。</para>
	///   <para><b>输入前提</b>应为单通道灰度图，本层不检查通道数 [待实测]。输出是像素的并还是按目标分块的数组
	///   本层无法判断，用 <c>CountObj()</c> 确认 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion dark = img.BinThreshold();
	///   int n = dark.Connection().CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄归调用者释放；末尾 <c>GC.KeepAlive(this)</c>。
	///   <c>Connection()</c> 又产生一个新句柄（它不是原地改写），两处都要处理。</para>
	/// </remarks>
	public JlRegion BinThreshold()
	{
		IntPtr proc = JlNativeApi.PreCall(463);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>全局阈值分割并在算子内按最小面积丢弃小块（元组阈值版）。</summary>
	/// <param name="minGray">灰度下界。Default: 128</param>
	/// <param name="maxGray">灰度上界。Default: 255.0</param>
	/// <param name="minSize">小于该像素数的目标被丢弃。Default: 20</param>
	/// <returns>新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 464。原生侧参数序 <c>minGray</c>→槽 0、<c>maxGray</c>→槽 1、
	///   <c>minSize</c>→槽 2，与 C# 形参序一致；输出仍是一路图标（<c>JlRegion.LoadNew</c>）。
	///   它与 <see cref="Threshold(JlTuple,JlTuple)"/>（id 443）的实质差别是<b>多了一道区域级过滤</b>：
	///   像素数小于 <paramref name="minSize"/> 的目标在算子内被直接丢掉，因此省掉
	///   <c>Connection()</c> + 逐个面积筛选这一步，这也是"fast"的来处 [待实测：输出是否已按连通块分开]。</para>
	///   <para><b>灰度量程（写死就漏像素）</b>参数说明里 <c>minGray</c> 的默认是<b>整数 128</b> 而
	///   <c>maxGray</c> 是 <b>255.0</b>：上界 255 只对 <c>byte</c> 是满量程，在 <c>uint2</c>/<c>float</c> 图上
	///   会把高灰度目标整片丢掉且不报错。本算子的参数说明并未列出 <c>"min"</c>/<c>"max"</c> 特殊值
	///   （<c>Threshold</c> 的参数说明里有），跨类型请先 <c>GetImageType()</c> 判类型再换算上界
	///   [待实测：本算子是否也接受 min/max]。区间开闭、两值反序（下界大于上界）时的行为本层不做校验 [待实测]。</para>
	///   <para><b>minSize 的含义</b>是 <c>int</c> 像素数，随分辨率与像素当量变化：换相机/换 ROI 后必须重标，
	///   否则要么噪声没滤掉，要么真目标被当噪声吞掉——后者比前者更危险，因为它表现为"漏检为 OK"。
	///   边界值是否计入 [待实测]。</para>
	///   <para><b>参数取向</b>元组版可给多组区间（与 <c>Threshold</c> 一样按下标配对 [待实测：多区间是否被本算子支持]），
	///   代价是 <c>Store</c>+调用后 <c>UnpinTuple</c>；单区间请用 <see cref="FastThreshold(double,double,int)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion parts = img.FastThreshold(new JlTuple(128.0), new JlTuple(255.0), 20);
	///   int n = parts.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄归调用者释放；末尾 <c>GC.KeepAlive(this)</c>。
	///   小块被算子内丢弃后，"目标个数为 0"既可能是真没目标、也可能是被 <paramref name="minSize"/> 吃掉了，
	///   排查时先把 <paramref name="minSize"/> 设成 1 对比一次。</para>
	/// </remarks>
	public JlRegion FastThreshold(JlTuple minGray, JlTuple maxGray, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(464);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, minGray);
		JlNativeApi.Store(proc, 1, maxGray);
		JlNativeApi.StoreI(proc, 2, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minGray);
		JlNativeApi.UnpinTuple(maxGray);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>全局阈值分割 + 算子内最小面积过滤（单区间数值版）。</summary>
	/// <param name="minGray">灰度下界。Default: 128</param>
	/// <param name="maxGray">灰度上界。Default: 255.0</param>
	/// <param name="minSize">小于该像素数的目标被丢弃。Default: 20</param>
	/// <returns>新区域句柄；输入图像不变。</returns>
	/// <remarks>
	///   <para>算子内的面积过滤、<paramref name="minSize"/> 随分辨率漂移、255 上界在非 <c>byte</c> 图上静默丢像素等问题见
	///   <see cref="FastThreshold(JlTuple,JlTuple,int)"/>：两个重载同走原生 id 464，槽位序与输出路径一致。</para>
	///   <para><b>实际差异</b>两个界限经 <c>StoreD</c> 直写，不做元组固定/解固定，单次分割用它更省；
	///   代价是只能给一个区间。参数是 <c>double</c>，因此也无法像元组版那样考虑 <c>"min"</c>/<c>"max"</c> 一类的写法，
	///   换图像类型时必须自己改数值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion parts = img.FastThreshold(128.0, 255.0, 20);
	///   int n = parts.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄归调用者释放；末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlRegion FastThreshold(double minGray, double maxGray, int minSize)
	{
		IntPtr proc = JlNativeApi.PreCall(464);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, minGray);
		JlNativeApi.StoreD(proc, 1, maxGray);
		JlNativeApi.StoreI(proc, 2, minSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按灰度相似性做区域扩张/分离，原生算子 id 499，控制参数以元组传入。</summary>
	/// <param name="regions">要弥合间隙或要分离的重叠区域（种子区域）。</param>
	/// <param name="forbiddenArea">禁区：该区域内不发生扩张。</param>
	/// <param name="iterations">迭代次数。Default: "maximal"</param>
	/// <param name="mode">扩张模式。Default: "image"</param>
	/// <param name="threshold">候选像素与区域边界灰度的最大允许差值。Default: 32</param>
	/// <returns>扩张或分离后的新区域元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>图像本身是 iconc 输入（<c>Store(proc, 2)</c>），<paramref name="regions"/> 与 <paramref name="forbiddenArea"/>
	///   是 iconc 1、3 的区域输入，输出为全新 <see cref="JlRegion"/> 元组，输入区域不被修改。与形态学扩张（如
	///   <see cref="JlRegion.DilationCircle(double)"/>）的本质区别是吞并像素由灰度决定：从区域边界出发，
	///   候选像素与边界灰度差不超过 <paramref name="threshold"/> 才被吸收，因此扩张会停在材质边界上；
	///   <paramref name="iterations"/>="maximal" 表示迭代到边界不再变化为止。</para>
	///   <para><b>约束与失效方式</b>弥合间隙要求缝隙两侧灰度差在 <paramref name="threshold"/> 内，否则扩张无效；
	///   <paramref name="threshold"/> 给大了则两种不同材质直接"焊死"成一块——这是本算子最常见的翻车点。
	///   输出个数一般不等于输入：每弥合一个间隙少一个区域，分离模式下又可能变多，
	///   用 <c>CountObj()</c> 核对数量，不要假设 result[i] 对应 regions[i]。<paramref name="mode"/> 各取值的确切语义
	///   托管层看不出来 [待实测]。多通道图像下按灰度还是按颜色距离比较 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要让扩张停在固定的材质灰度（不随边界推进漂移）→
	///   <see cref="ExpandGrayRef(JlRegion,JlRegion,JlTuple,string,JlTuple,JlTuple)"/>；纯几何放大、不看灰度 → 直接用区域形态学。</para>
	///   <para><b>参数取向</b>本重载 <paramref name="iterations"/>/<paramref name="threshold"/> 以 <see cref="JlTuple"/> 传入：
	///   <c>Store</c> 固定、调用后 <c>UnpinTuple</c>；多元素是否逐区域对应 [待实测]，单值请用
	///   <see cref="ExpandGray(JlRegion,JlRegion,string,string,int)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion seeds = img.Threshold(120.0, 255.0);
	///   using JlRegion banned = new JlRegion(0.0, 0.0, 10.0, 10.0);   // 左上角 10x10 不许扩张
	///   using JlRegion grown = img.ExpandGray(seeds, banned, new JlTuple("maximal"), "image", new JlTuple(32.0));
	///   int n = grown.CountObj();   // 通常不等于 seeds.CountObj()
	///   </code>
	///   <para><b>资源与坑</b>返回的新区域需释放；实现只靠 <c>GC.KeepAlive</c> 钉住图像与两个输入区域，调用结束即可各自释放。
	///   大图上 "maximal" 迭代很慢，只为弥合几个缺口时先 <see cref="JlRegion.Connection()"/> 拆分连通域、只喂需要的种子。</para>
	/// </remarks>
	public JlRegion ExpandGray(JlRegion regions, JlRegion forbiddenArea, JlTuple iterations, string mode, JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(499);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.Store(proc, 3, forbiddenArea);
		JlNativeApi.Store(proc, 0, iterations);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.Store(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(iterations);
		JlNativeApi.UnpinTuple(threshold);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>灰度扩张/分离（迭代与阈值以标量传入）。</summary>
	/// <param name="regions">要弥合间隙或要分离的重叠区域（种子区域）。</param>
	/// <param name="forbiddenArea">禁区：该区域内不发生扩张。</param>
	/// <param name="iterations">迭代次数。Default: "maximal"</param>
	/// <param name="mode">扩张模式。Default: "image"</param>
	/// <param name="threshold">候选像素与区域边界灰度的最大允许差值。Default: 32</param>
	/// <returns>扩张或分离后的新区域元组。</returns>
	/// <remarks>
	///   <para>灰度吞并机制、<paramref name="threshold"/> 过大导致误粘连、输出个数变化等要点见
	///   <see cref="ExpandGray(JlRegion,JlRegion,JlTuple,string,JlTuple)"/>：同一原生 id 499，本版本
	///   <paramref name="iterations"/>/<paramref name="mode"/> 走 <c>StoreS</c>、<paramref name="threshold"/> 走 <c>StoreI</c> 直写，
	///   无元组固定/解固定，是常规写法；<paramref name="threshold"/> 只接受整数灰度差，需要 0.5 级差值时走元组版 [待实测：小数是否被截断]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion seeds = img.Threshold(120.0, 255.0);
	///   using JlRegion banned = new JlRegion(0.0, 0.0, 10.0, 10.0);
	///   using JlRegion grown = img.ExpandGray(seeds, banned, "maximal", "image", 32);
	///   </code>
	/// </remarks>
	public JlRegion ExpandGray(JlRegion regions, JlRegion forbiddenArea, string iterations, string mode, int threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(499);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.Store(proc, 3, forbiddenArea);
		JlNativeApi.StoreS(proc, 0, iterations);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.StoreI(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>按固定参考灰度做区域扩张/分离，原生算子 id 500，控制参数以元组传入。</summary>
	/// <param name="regions">要弥合间隙或要分离的重叠区域（种子区域）。</param>
	/// <param name="forbiddenArea">禁区：该区域内不发生扩张。</param>
	/// <param name="iterations">迭代次数。Default: "maximal"</param>
	/// <param name="mode">扩张模式。Default: "image"</param>
	/// <param name="refGray">用于比较的参考灰度值（或颜色）。Default: 128</param>
	/// <param name="threshold">候选像素与参考灰度的最大允许差值。Default: 32</param>
	/// <returns>扩张或分离后的新区域元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>输入布局与 <see cref="ExpandGray(JlRegion,JlRegion,JlTuple,string,JlTuple)"/> 相同
	///   （图像 iconc 2、区域 iconc 1/3、新区域输出 iconc 1），本版本多出 iconc 控制槽位 2 的 <paramref name="refGray"/>
	///   （<paramref name="threshold"/> 因此后移到 3）。实质差异是对比基准：<c>ExpandGray</c> 拿候选像素和<b>区域当前边界</b>的灰度比，
	///   边界会随灰度渐变慢慢推进；这里候选像素只和<b>固定参考值</b> <paramref name="refGray"/> 比，
	///   差值在 <paramref name="threshold"/> 内才吸收——扩张停在"该材质"的灰度带内，不被过渡带拖走。</para>
	///   <para><b>取舍</b>目标材质灰度稳定但边界发虚、希望"长到某个材质为止"→ 本算子；希望吸收一切与当前区域连续的相近像素 →
	///   <c>ExpandGray</c>。颜色图像需要逐通道给参考值，用本元组版最自然（多元素与通道数的对应关系 [待实测]）。</para>
	///   <para><b>参数取向</b><paramref name="iterations"/>/<paramref name="refGray"/>/<paramref name="threshold"/> 均为
	///   <c>Store</c> 固定 + <c>UnpinTuple</c> 解固定；单值简写见 <see cref="ExpandGrayRef(JlRegion,JlRegion,string,string,int,int)"/>，
	///   但那里 <paramref name="refGray"/> 只能传一个 int，颜色图像无法逐通道设定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion seeds = img.Threshold(200.0, 255.0);
	///   using JlRegion banned = new JlRegion(0.0, 0.0, 10.0, 10.0);
	///   using JlRegion grown = img.ExpandGrayRef(seeds, banned, new JlTuple("maximal"), "image",
	///       new JlTuple(230.0), new JlTuple(25.0));   // 只长到灰度 230±25 的像素
	///   </code>
	///   <para><b>资源与坑</b>返回的新区域需释放；<paramref name="refGray"/> 与种子实际灰度明显失配时（如参考值落在背景上），
	///   扩张结果会整体偏空或吞进背景，调 <c>Intensity</c> 量一下种子均值再定参考值。</para>
	/// </remarks>
	public JlRegion ExpandGrayRef(JlRegion regions, JlRegion forbiddenArea, JlTuple iterations, string mode, JlTuple refGray, JlTuple threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(500);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.Store(proc, 3, forbiddenArea);
		JlNativeApi.Store(proc, 0, iterations);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.Store(proc, 2, refGray);
		JlNativeApi.Store(proc, 3, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(iterations);
		JlNativeApi.UnpinTuple(refGray);
		JlNativeApi.UnpinTuple(threshold);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>固定参考灰度的区域扩张（控制参数全部以标量传入）。</summary>
	/// <param name="regions">要弥合间隙或要分离的重叠区域（种子区域）。</param>
	/// <param name="forbiddenArea">禁区：该区域内不发生扩张。</param>
	/// <param name="iterations">迭代次数。Default: "maximal"</param>
	/// <param name="mode">扩张模式。Default: "image"</param>
	/// <param name="refGray">参考灰度值。Default: 128</param>
	/// <param name="threshold">候选像素与参考灰度的最大允许差值。Default: 32</param>
	/// <returns>扩张或分离后的新区域元组。</returns>
	/// <remarks>
	///   <para>固定 <c>refGray</c> 基准与 <c>ExpandGray</c> 的取舍见
	///   <see cref="ExpandGrayRef(JlRegion,JlRegion,JlTuple,string,JlTuple,JlTuple)"/>：同一原生 id 500，
	///   本版本 <c>StoreS</c>/<c>StoreI</c> 直写四个控制参数，无固定/解固定；<paramref name="refGray"/> 与
	///   <paramref name="threshold"/> 都是 <c>int</c>，只能对单一灰度值生效，颜色图像的逐通道参考需回元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion seeds = img.Threshold(200.0, 255.0);
	///   using JlRegion banned = new JlRegion(0.0, 0.0, 10.0, 10.0);
	///   using JlRegion grown = img.ExpandGrayRef(seeds, banned, "maximal", "image", 230, 25);
	///   </code>
	/// </remarks>
	public JlRegion ExpandGrayRef(JlRegion regions, JlRegion forbiddenArea, string iterations, string mode, int refGray, int threshold)
	{
		IntPtr proc = JlNativeApi.PreCall(500);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.Store(proc, 3, forbiddenArea);
		JlNativeApi.StoreS(proc, 0, iterations);
		JlNativeApi.StoreS(proc, 1, mode);
		JlNativeApi.StoreI(proc, 2, refGray);
		JlNativeApi.StoreI(proc, 3, threshold);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		GC.KeepAlive(forbiddenArea);
		return obj;
	}

	/// <summary>对象元组求差：去掉同时出现在另一元组里的对象，返回剩下的对象。</summary>
	/// <param name="objectsSub">要从中扣除的对象元组。</param>
	/// <returns>新的图像对象元组句柄；两个输入都不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 558。iconc 输入槽位是 <c>this</c>→1、<paramref name="objectsSub"/>→2，
	///   输出一路（<c>LoadNew</c>）取回<b>新句柄</b>。它算的是<b>集合差</b>：结果是"属于本元组、但不属于
	///   <paramref name="objectsSub"/>的那些对象，元素个数会减少，每个保留下来的对象本身不被修改。</para>
	///   <para><b>最容易混的一点</b>名字里的 Diff 不是灰度相减。逐像素算术差请用
	///   <see cref="SubImage(JlImage,double,double)"/>；把区域从图像里挖掉用 <c>ReduceDomain</c>/<c>Difference</c> 一类区域算子。
	///   在只有单个图像的元组上调用本方法不会报错，但结果是"两个对象是否算同一个"由原生端判定
	///   [待实测：判定是比句柄身份、比像素内容还是比图标序号]，这正是它最容易被误用的地方。</para>
	///   <para><b>输入前提</b>两路都应是同类对象元组（图像对图像）。本层不检查元素个数、类型与尺寸是否一致 [待实测]；
	///   类型不一致时由原生端在 <c>PostCall</c> 抛错。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage a = new JlImage("byte", 64, 48);
	///   JlImage b = new JlImage("byte", 64, 48);
	///   using JlImage onlyInA = a.ObjDiff(b);
	///   int n = onlyInA.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄归调用者释放；<paramref name="objectsSub"/> 只是被读取，所有权不转交。
	///   末尾对 <c>this</c> 与 <paramref name="objectsSub"/> 都做了 <c>GC.KeepAlive</c>，两路输入在整个原生调用期间都不得回收。</para>
	/// </remarks>
	public JlImage ObjDiff(JlImage objectsSub)
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

	/// <summary>批量改写指定像素的灰度（原地改本图像，元组坐标版）。</summary>
	/// <param name="row">待改像素的行坐标。Default: 0</param>
	/// <param name="column">待改像素的列坐标。Default: 0</param>
	/// <param name="grayval">写入的灰度值。Default: 255.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 559。方法体里只有 <c>Store(proc,1)</c> 把<b>本对象自己</b>交进去，
	///   没有任何 <c>LoadNew</c>/<c>InitOCT</c>，返回 <c>void</c>——也就是说它是<b>原地改写</b>：
	///   调用后 <c>this</c> 指向的图像内容已经变了，不存在"用完丢弃结果"这种安全写法。</para>
	///   <para><b>由此推出的坑</b>与 <c>this</c> 共享同一句柄的引用（例如 <see cref="CopyObj(int,int)"/>
	///   那类不重新分配内存的复制结果，或你自己保存的"原图"）会一起被改 [待实测：句柄共享的确切范围]。
	///   要保留原图，先 <see cref="CopyImage()"/> 再在副本上写。</para>
	///   <para><b>参数配对与校验</b><paramref name="row"/>/<paramref name="column"/>/<paramref name="grayval"/>
	///   按<b>下标一一对应</b>，长度不等时本层不检查，多余元素被忽略还是报错 [待实测]。
	///   坐标按 row=y（向下为正）、column=x（向右为正）理解；越界、负坐标、写在图像外都不做托管层校验 [待实测]。
	///   灰度是 <c>double</c> 而目标图像可能是 <c>byte</c>：超量程或带小数的值如何落盘（截断还是取整）由原生端决定 [待实测]。
	///   多通道图像上单值灰度是逐通道写还是只写第 1 通道 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>成片的区域填色用 <see cref="PaintRegion(JlRegion,double,string)"/>，
	///   轮廓画回图像用 <see cref="PaintXld(JlXLD,double)"/>——它们返回新图像、不动原图；
	///   本方法适合按坐标表逐点写（打标记、修坏点）。另外与 <see cref="OverpaintRegion(JlRegion,double,string)"/> 一样是本地图像被改，
	///   区别只在写入目标的给定方式。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 48);
	///   using JlImage keep = img.CopyImage();                 // 先留一份，避免原地改写污染
	///   img.SetGrayval(new JlTuple(3.0, 7.0), new JlTuple(5.0, 9.0), new JlTuple(255.0, 255.0));
	///   </code>
	///   <para><b>资源与坑</b>三个元组都在调用后被 <c>UnpinTuple</c> 解固定，调用返回后即可释放；
	///   末尾 <c>GC.KeepAlive(this)</c> 保证被改写的图像在原生调用结束前不回收。</para>
	/// </remarks>
	public void SetGrayval(JlTuple row, JlTuple column, JlTuple grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(559);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, grayval);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(grayval);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>改写单个像素的灰度（原地，单点版）。</summary>
	/// <param name="row">待改像素的行坐标。Default: 0</param>
	/// <param name="column">待改像素的列坐标。Default: 0</param>
	/// <param name="grayval">写入的灰度值。Default: 255.0</param>
	/// <remarks>
	///   <para><b>原地改写</b>、坐标与灰度的取值约定、共享句柄会被污染等注意事项见
	///   <see cref="SetGrayval(JlTuple,JlTuple,JlTuple)"/>：两个重载同走原生 id 559。</para>
	///   <para><b>实际差异</b>本重载用 <c>StoreI</c>(槽 0、1)/<c>StoreD</c>(槽 2) 直写，没有固定/解固定开销，
	///   但坐标是 <c>int</c>：只能写<b>整像素</b>、且一次只写一个点。亚像素坐标与批量点表都必须用元组版。
	///   循环调用本重载写很多点时，每点一次原生调用，开销远高于元组版一次写完整表 [待实测：倍率]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 48);
	///   using JlImage keep = img.CopyImage();
	///   img.SetGrayval(3, 5, 255.0);                             // 注意 row 在前、column 在后
	///   </code>
	///   <para><b>资源与坑</b>形参序是 (row, column)，把 (x, y) 习惯直接搬过来会得到转置位置的像素——本层无法发现；
	///   末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public void SetGrayval(int row, int column, double grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(559);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, grayval);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>把 XLD 轮廓画进图像（返回新图像，灰度以元组传入）。</summary>
	/// <param name="XLD">要画入的轮廓对象。</param>
	/// <param name="grayval">轮廓的灰度值。Default: 255.0</param>
	/// <returns>画好轮廓的<b>新</b>图像句柄；本对象与 <paramref name="XLD"/> 都不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 560。槽位安排值得注意：控制参数 <c>grayval</c> 在槽 0、
	///   <c>XLD</c> 在 iconc 槽 1、<b>图像在 iconc 槽 2</b>（<c>Store(proc,2)</c>），即原生侧槽位序与 C# 形参序并不对应。
	///   输出经 <c>LoadNew</c> 返回<b>新句柄</b>——本对象不会被画脏，这与 void 版的
	///   <see cref="OverpaintRegion(JlRegion,double,string)"/> 是本质区别。</para>
	///   <para><b>取值约定</b><paramref name="grayval"/> 是 double：落到 <c>byte</c> 图上会被量化成整数
	///   [待实测：截断还是四舍五入]；在多通道图像上单值会写到哪些通道 [待实测]。
	///   元组版给多个值时，按 <c>XLD</c> 元素序逐条轮廓上色 [待实测：是否真按元素序一一对应]，
	///   因此轮廓数与灰度数不等时不要假定结果稳定。</para>
	///   <para><b>与相邻算子的取舍</b>要填一片区域用 <see cref="PaintRegion(JlRegion,JlTuple,string)"/>（它有
	///   <c>type</c> 可选填充还是描边）；本算子没有 <c>type</c>，画的就是轮廓本身，亚像素轮廓位置能保住
	///   [待实测：轮廓落到像素网格的规则]。想把结果直接叠在原图上、不额外要一份图像，用 <c>Overpaint*</c> 族。</para>
	///   <para><b>前提</b>轮廓坐标须落在图像范围内，越界部分由原生端裁剪 [待实测]；
	///   本层不检查图像与轮廓的尺寸是否匹配。JlXLDCont、JlXLDPara 等均派生自 <c>JlXLD</c>，可直接传入。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 48);
	///   using JlXLDCont cont = new JlXLDCont(new JlTuple(4.0, 5.0, 6.0), new JlTuple(10.0, 11.0, 12.0));
	///   using JlImage painted = img.PaintXld(cont, new JlTuple(255.0));
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄归调用者释放；<paramref name="XLD"/> 只读，所有权不转交。
	///   末尾对 <c>this</c> 与 <paramref name="XLD"/> 都做了 <c>GC.KeepAlive</c>。</para>
	/// </remarks>
	public JlImage PaintXld(JlXLD XLD, JlTuple grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(560);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, XLD);
		JlNativeApi.Store(proc, 0, grayval);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(grayval);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(XLD);
		return obj;
	}

	/// <summary>把 XLD 轮廓画进图像（单灰度版，返回新图像）。</summary>
	/// <param name="XLD">要画入的轮廓对象。</param>
	/// <param name="grayval">所有轮廓共用的灰度值。Default: 255.0</param>
	/// <returns>画好轮廓的<b>新</b>图像句柄；本对象与 <paramref name="XLD"/> 都不变。</returns>
	/// <remarks>
	///   <para>槽位序（<c>grayval</c>→控制槽 0、<c>XLD</c>→iconc 1、图像→iconc 2）、返回新句柄而非原地改写、
	///   double 灰度落在 <c>byte</c> 图上会被量化等问题见 <see cref="PaintXld(JlXLD,JlTuple)"/>：两个重载同走原生 id 560。</para>
	///   <para><b>实际差异</b>灰度经 <c>StoreD</c> 直写，不做固定/<c>UnpinTuple</c>，画一条轮廓或全部同色时用它更省；
	///   代价是<b>无法逐条轮廓给不同灰度</b>——要在同一幅图上按颜色区分多条轮廓只能回元组重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 48);
	///   using JlXLDCont cont = new JlXLDCont(new JlTuple(4.0, 5.0, 6.0), new JlTuple(10.0, 11.0, 12.0));
	///   using JlImage painted = img.PaintXld(cont, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放，输入图像与其副本互不影响（各归各的释放）；
	///   末尾对 <c>this</c> 与 <paramref name="XLD"/> 做了 <c>GC.KeepAlive</c>。</para>
	/// </remarks>
	public JlImage PaintXld(JlXLD XLD, double grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(560);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, XLD);
		JlNativeApi.StoreD(proc, 0, grayval);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(XLD);
		return obj;
	}

	/// <summary>把区域画进图像（返回新图像，多区域可各给灰度）。</summary>
	/// <param name="region">要画入的区域（可为多个）。</param>
	/// <param name="grayval">区域灰度值。Default: 255.0</param>
	/// <param name="type">填充还是只描边。Default: "fill"</param>
	/// <returns>画好区域的<b>新</b>图像句柄；本对象与 <paramref name="region"/> 都不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 561。槽位序是 <c>grayval</c>→控制槽 0、<c>type</c>→控制槽 1、
	///   <c>region</c>→iconc 槽 1、<c>图像</c>→iconc 槽 2（注意图像在区域<b>之后</b>，与
	///   <see cref="OverpaintRegion(JlRegion,JlTuple,string)"/> 里两者刚好相反）。
	///   输出经 <c>LoadNew</c> 是<b>新句柄</b>：原图保持不变，可以同时留着"底图 + 叠加图"两版本。</para>
	///   <para><b>多区域与多灰度</b>元组版给多个灰度时按区域元组的元素序一一对应 [待实测：区域数与灰度数不等时的行为]，
	///   而区域元组的次序由它的来源决定（<c>Connection()</c> 之后的序号并不稳定），所以"给第 3 个区域涂红"这类写法
	///   必须先把区域排过序，否则会静默涂错目标。</para>
	///   <para><b>取值约定</b><paramref name="type"/> 是字符串（<c>StoreS</c>），本层不校验取值，写错由原生端报错 [待实测：完整取值]；
	///   描边模式下轮廓宽度不可控 [待实测]。<paramref name="grayval"/> 是 double，落到 <c>byte</c> 图会被量化
	///   [待实测：截断还是取整]；多通道图像上单值写到哪些通道 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只想缩小处理域、不产生灰度像素时用 <c>ReduceDomain</c>（结果仍是图像但带区域掩膜语义）；
	///   要把结果就地叠进原图、省一份内存时用 <see cref="OverpaintRegion(JlRegion,JlTuple,string)"/>；
	///   画亚像素轮廓用 <see cref="PaintXld(JlXLD,JlTuple)"/>。画入区域超出图像范围时本层不裁剪 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 48);
	///   using JlRegion roi = new JlRegion(4.0, 6.0, 20.0, 30.0);      // 左上/右下角点
	///   using JlImage painted = img.PaintRegion(roi, new JlTuple(255.0), "fill");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄归调用者释放，原图另算；末尾对 <c>this</c> 与 <paramref name="region"/> 都做了
	///   <c>GC.KeepAlive</c>，二者在整个原生调用期间都不得回收。</para>
	/// </remarks>
	public JlImage PaintRegion(JlRegion region, JlTuple grayval, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(561);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.Store(proc, 0, grayval);
		JlNativeApi.StoreS(proc, 1, type);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(grayval);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>把区域画进图像（单灰度版，返回新图像）。</summary>
	/// <param name="region">要画入的区域（可为多个）。</param>
	/// <param name="grayval">所有区域共用的灰度值。Default: 255.0</param>
	/// <param name="type">填充还是只描边。Default: "fill"</param>
	/// <returns>画好区域的<b>新</b>图像句柄；本对象与 <paramref name="region"/> 都不变。</returns>
	/// <remarks>
	///   <para>槽位序、返回新句柄而非原地改写、<paramref name="type"/> 不校验、double 灰度在 <c>byte</c> 图上被量化等问题见
	///   <see cref="PaintRegion(JlRegion,JlTuple,string)"/>：两个重载同走原生 id 561。</para>
	///   <para><b>实际差异</b>灰度经 <c>StoreD</c> 直写槽 0，无固定/<c>UnpinTuple</c> 开销，是常规写法；
	///   代价是所有区域只能同一灰度——要按区域分别上色（例如把缺陷按类别涂不同值）必须用元组重载，
	///   并注意其配对依赖区域次序。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 48);
	///   using JlRegion roi = img.BinThreshold();
	///   using JlImage painted = img.PaintRegion(roi, 255.0, "fill");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；原图与 <paramref name="region"/> 各自的所有权不变，用完分别释放；
	///   末尾对 <c>this</c> 与 <paramref name="region"/> 做了 <c>GC.KeepAlive</c>。</para>
	/// </remarks>
	public JlImage PaintRegion(JlRegion region, double grayval, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(561);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.StoreD(proc, 0, grayval);
		JlNativeApi.StoreS(proc, 1, type);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>把区域就地涂进本图像（原地改写，多区域可各给灰度）。</summary>
	/// <param name="region">要涂入的区域（可为多个）。</param>
	/// <param name="grayval">区域灰度值。Default: 255.0</param>
	/// <param name="type">填充还是只描边。Default: "fill"</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 562。<c>Store(proc,1)</c> 把本对象作为既输又入的图像交出去，
	///   方法返回 <c>void</c>、没有 <c>LoadNew</c>——<b>灰度直接落在本图像的内存上</b>。
	///   iconc 槽位是 <c>图像</c>→1、<c>region</c>→2，正好与 <see cref="PaintRegion(JlRegion,JlTuple,string)"/> 相反。</para>
	///   <para><b>与 PaintRegion 的取舍</b>需要"原图 + 叠加图"两份（例如还要拿原图做后续测量）时<b>必须</b>用
	///   <c>PaintRegion</c>；本方法一旦涂下去原像素就回不来，只能事先 <see cref="CopyImage()"/> 留档。
	///   反过来，在流水线末端只为生成一张结果图时用本方法省一次整幅内存分配 [待实测：两者耗时差异]。</para>
	///   <para><b>前提</b>图像类型决定写入精度（<c>byte</c> 图上 double 灰度会被量化 [待实测]）；
	///   <paramref name="type"/> 不经本层校验 [待实测：完整取值]；多个灰度与多个区域的配对按区域次序 [待实测]，
	///   而区域次序由上游 <c>Connection()</c> 决定、并不稳定，跨帧复用同一套灰度表时容易涂错。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 48);
	///   using JlImage keep = img.CopyImage();                         // 原地改写前先把原图存一份
	///   using JlRegion roi = img.BinThreshold();
	///   img.OverpaintRegion(roi, new JlTuple(0.0), "fill");           // 把暗区涂黑
	///   </code>
	///   <para><b>资源与坑</b>没有返回值可用，不要写成 <c>var x = img.OverpaintRegion(...)</c>；
	///   末尾对 <c>this</c> 与 <paramref name="region"/> 做 <c>GC.KeepAlive</c>，二者在原生调用结束前不得回收。</para>
	/// </remarks>
	public void OverpaintRegion(JlRegion region, JlTuple grayval, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(562);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.Store(proc, 0, grayval);
		JlNativeApi.StoreS(proc, 1, type);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(grayval);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
	}

	/// <summary>把区域就地涂进本图像（原地改写，单灰度版）。</summary>
	/// <param name="region">要涂入的区域（可为多个）。</param>
	/// <param name="grayval">所有区域共用的灰度值。Default: 255.0</param>
	/// <param name="type">填充还是只描边。Default: "fill"</param>
	/// <remarks>
	///   <para>原地改写本图像、iconc 槽位与 <c>PaintRegion</c> 相反、要留原图必须先 <c>CopyImage</c> 等注意事项见
	///   <see cref="OverpaintRegion(JlRegion,JlTuple,string)"/>：两个重载同走原生 id 562。</para>
	///   <para><b>实际差异</b>灰度经 <c>StoreD</c> 直写槽 0，不做固定/<c>UnpinTuple</c>，只为盖一块区域时用它最省；
	///   代价是所有区域同一个灰度，无法按区域分别涂值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 64, 48);
	///   using JlImage keep = img.CopyImage();
	///   using JlRegion roi = img.BinThreshold();
	///   img.OverpaintRegion(roi, 0.0, "fill");
	///   </code>
	///   <para><b>资源与坑</b>返回 <c>void</c>；本图像内容已被改，任何与它共享句柄的引用同样被改 [待实测：共享范围]；
	///   末尾对 <c>this</c> 与 <paramref name="region"/> 做 <c>GC.KeepAlive</c>。</para>
	/// </remarks>
	public void OverpaintRegion(JlRegion region, double grayval, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(562);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreD(proc, 0, grayval);
		JlNativeApi.StoreS(proc, 1, type);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
	}

	/// <summary>以本图为"原型"生成同尺寸同域的常数灰度图（元组版：可整条传入多个灰度值）。</summary>
	/// <param name="grayval">输出图像的常数灰度值；尺寸与域取自本图。Default: 0</param>
	/// <returns>新图像句柄（LoadNew）；原型图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 563。<c>this</c> 只贡献尺寸与域（<c>Store(proc,1)</c>），grayval 整条钉传到
	///   控制槽 0、调用后 <c>UnpinTuple</c>。多值元组的语义（按通道展开还是只取首值）本层未体现 [待实测]。</para>
	///   <para><b>与标量重载的取舍</b>单值请写 <see cref="GenImageProto(double)"/>：同 id、<c>StoreD</c> 直写、无钉固定开销。
	///   注意裸 int 字面量（如 <c>0</c>）会同时在 int→double 与 int→JlTuple 两条隐式转换间二义（CS0121），
	///   本重载必须显式写 <c>new JlTuple(...)</c>，标量版必须写 double 字面量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage proto = new JlImage("byte", 64, 48);
	///   using JlImage flat = proto.GenImageProto(new JlTuple(128));
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose；输出像素类型由原生按 grayval 选定、与原型类型不必一致 [待实测]。
	///   末尾 <c>GC.KeepAlive(this)</c>，调用结束前原型图不得释放。</para>
	/// </remarks>
	public JlImage GenImageProto(JlTuple grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(563);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, grayval);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(grayval);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>以本图为"原型"生成同尺寸同域的常数灰度图（double 标量版，单值场合的首选写法）。</summary>
	/// <param name="grayval">输出图像的常数灰度值；尺寸与域取自本图。Default: 0</param>
	/// <returns>新图像句柄（LoadNew）；原型图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 563，与 <see cref="GenImageProto(JlTuple)"/> 同一算子：尺寸/域取自 <c>this</c>，
	///   grayval 经 <c>StoreD</c> 直写控制槽 0，不钉元组、无 <c>UnpinTuple</c> 开销。</para>
	///   <para><b>取值约定</b>必须写 double 字面量（<c>128.0</c>）：裸 int 字面量会因 int→JlTuple 的隐式转换与元组重载
	///   撞成 CS0121 二义。要按通道给多值才走元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage proto = new JlImage("byte", 64, 48);
	///   using JlImage flat = proto.GenImageProto(128.0);       // 与原型同尺寸同域的全 128 图
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose；本图只当原型用、内容不变。输出像素类型按 grayval 由原生决定 [待实测]。
	///   末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlImage GenImageProto(double grayval)
	{
		IntPtr proc = JlNativeApi.PreCall(563);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, grayval);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把本图的灰度刷到目标图上得到第三幅结果图；源、目标两图都不被改写。</summary>
	/// <param name="imageDestination">被刷上的底图（图标槽 2）；须与本图同尺寸。</param>
	/// <returns>合成结果的新图像句柄（LoadNew）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 564。<c>this</c> 是灰度来源（槽 1）、<paramref name="imageDestination"/> 是底图（槽 2），
	///   结果作为第三个句柄返回——源、目标、结果三份独立，刷写不落回任何一个输入。</para>
	///   <para><b>约束或前提</b>两图尺寸必须一致，否则原生侧报错 [待实测：错误码]；本图域限制刷入范围，域外像素保留目标原值 [待实测]。
	///   类型不一致时按目标类型落值，超范围截断方向未校验 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只想在"自己"上涂一块定值区域用 <see cref="OverpaintRegion(JlRegion,double,string)"/>
	///   （原地改写、无新图）；要把一幅完整图像的内容合进另一幅才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage src = new JlImage("byte", 64, 64);
	///   using JlImage dst = new JlImage("byte", 64, 64);
	///   using JlImage res = src.PaintGray(dst);            // src、dst 均保持原样
	///   </code>
	///   <para><b>资源与坑</b>结果是新句柄需 Dispose；末尾对 <c>this</c> 与目标图 <c>GC.KeepAlive</c>，调用结束前两者不可释放。</para>
	/// </remarks>
	public JlImage PaintGray(JlImage imageDestination)
	{
		IntPtr proc = JlNativeApi.PreCall(564);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageDestination);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageDestination);
		return obj;
	}


	/// <summary>从本图像对象栈按 1 起始序号截取一段对象，产出新图像栈句柄。</summary>
	/// <param name="index">起始对象序号，从 1 开始计数。Default: 1</param>
	/// <param name="numObj">要复制的对象个数；-1 表示从 index 起复制到栈尾。Default: 1</param>
	/// <returns>复制出的图像栈新句柄；原栈不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 568；index、numObj 以 <c>StoreI</c> 写控制槽 0、1。方法带 <c>new</c>：
	///   隐藏 <c>JlObject.CopyObj</c>，返回类型收窄为 JlImage，省去手工下转。</para>
	///   <para><b>与 CopyImage 的取舍</b>整栈截取子集（配合序号筛选流程）只能用本方法；只是要一幅内容相同、
	///   内存独立的图用 <see cref="CopyImage()"/>。像素缓冲是否共享至首次写入本层无法判断 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage a = new JlImage("byte", 32, 32);
	///   using JlImage stack2 = a.ConcatObj(new JlImage("byte", 32, 32));
	///   using JlImage tail = stack2.CopyObj(2, -1);              // 第 2 幅起全部
	///   int n = tail.CountObj();                                 // 1
	///   </code>
	///   <para><b>资源与坑</b>index 越界（&lt;1 或大于栈内对象数）由原生报错，本层不预检 [待实测：错误码]；
	///   返回新句柄需 Dispose，末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public new JlImage CopyObj(int index, int numObj)
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

	/// <summary>把两个图像对象栈首尾接成一个新栈：本栈对象在前、objects2 在后。</summary>
	/// <param name="objects2">拼接到后面的对象栈。</param>
	/// <returns>拼接后的新图像栈句柄；两个输入栈均不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 569。<c>this</c> 的栈存图标槽 1、<paramref name="objects2"/> 存槽 2；
	///   结果顺序 = 本栈全部对象 + objects2 全部对象，是后续 CopyObj/SelectObj 按序号取用的建栈手段。</para>
	///   <para><b>约束或前提</b>两栈只要求同为图像对象，尺寸/类型可以不同（各对象保留自身属性）；
	///   拼接次序即最终栈序，上游次序不稳会静默错位。</para>
	///   <para><b>与相邻算子的取舍</b>要把多幅同尺寸单通道图合成<b>一幅</b>多通道图用 <see cref="ChannelsToImage()"/>
	///   或 <c>Compose2/3</c> 族；本方法产出的仍是逐图对象栈，不是多通道图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage a = new JlImage("byte", 32, 32);
	///   using JlImage b = new JlImage("int2", 64, 64);           // 尺寸类型可不同
	///   using JlImage pair = a.ConcatObj(b);
	///   int n = pair.CountObj();                                 // 2
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose；末尾对两栈 <c>GC.KeepAlive</c>，调用结束前都不得释放。</para>
	/// </remarks>
	public JlImage ConcatObj(JlImage objects2)
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

	/// <summary>深拷贝本图并为像素分配新内存，得到内容相同、可独立改写的新句柄。</summary>
	/// <returns>独立副本的新图像句柄；原图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 571：无控制参数，仅 <c>this</c> 一路图标输入，<c>LoadNew</c> 返回新句柄；
	///   所有通道与域一并复制。</para>
	///   <para><b>什么时候必须用它</b>接着要调用 <see cref="OverpaintRegion(JlRegion,double,string)"/> 这类<b>原地改写</b>族
	///   改像素、但还想保留原图时，先本方法留底（OverpaintRegion 族注释同样反复提示）；只赋句柄的"浅拷贝"会连原图一起被改。</para>
	///   <para><b>与 CopyObj 的取舍</b>按序号截取栈中一部分对象用 <see cref="CopyObj(int,int)"/>；
	///   本方法整图复制且明确"分配新内存"，改写副本不牵连原图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage keep = img.CopyImage();                    // 留底：img 随后被原地涂改
	///   using JlRegion roi = img.BinThreshold();
	///   img.OverpaintRegion(roi, 0.0, "fill");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose；大尺寸多通道图的副本按全量占内存（"新内存"语义即非共享缓冲），
	///   高频流水线里勿无谓调用。末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlImage CopyImage()
	{
		IntPtr proc = JlNativeApi.PreCall(571);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>从本图像对象栈按序号挑选对象组成新栈（元组版：一次可选多幅、可重排可重复）。</summary>
	/// <param name="index">要选出的对象序号，从 1 开始。Default: 1</param>
	/// <returns>选出对象组成的新图像栈句柄；原栈不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 572。元组整条钉传到控制槽 0、调用后 <c>UnpinTuple</c>：结果栈严格按
	///   元组给定的顺序与重复次数生成（同一序号可选多次），这也是与 <see cref="CopyObj(int,int)"/> 的关键差别——
	///   后者只能截连续一段，本方法能任意重排。</para>
	///   <para><b>与标量重载的取舍</b>只选一帧用 <see cref="SelectObj(int)"/>（<c>StoreI</c> 直写、免钉）。
	///   裸 int 字面量会精确匹配到 int 版；本重载需显式写 <c>new JlTuple(...)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage a = new JlImage("byte", 32, 32);
	///   using JlImage stack2 = a.ConcatObj(new JlImage("byte", 32, 32));
	///   using JlImage swapped = stack2.SelectObj(new JlTuple(2.0, 1.0));   // 交换两帧次序
	///   </code>
	///   <para><b>资源与坑</b>序号 &lt;1 或越界由原生报错、本层不预检 [待实测]；返回新句柄需 Dispose。
	///   末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public new JlImage SelectObj(JlTuple index)
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

	/// <summary>从本图像对象栈取出第 index 帧（标量版，单帧取用的常规写法）。</summary>
	/// <param name="index">要选出的对象序号，从 1 开始。Default: 1</param>
	/// <returns>选出的图像新句柄；原栈不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 572，与 <see cref="SelectObj(JlTuple)"/> 同一算子：index 经 <c>StoreI</c>
	///   直写控制槽 0，不钉元组；裸 int 字面量精确匹配本版，是"取第 N 帧"最省事的入口。
	///   Connection 之后按序号筛对象也走这里，但要清楚上游栈序不保证稳定。</para>
	///   <para><b>约束</b>序号 1 起始；传 0 或超出栈长由原生报错，本层不预检 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage a = new JlImage("byte", 32, 32);
	///   using JlImage stack2 = a.ConcatObj(new JlImage("byte", 32, 32));
	///   using JlImage first = stack2.SelectObj(1);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose；选出的帧与原栈内对象是否共享像素缓冲本层无法判断 [待实测]，
	///   要独立改写像素先 <c>CopyImage()</c>。末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public new JlImage SelectObj(int index)
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

	/// <summary>逐像素比较两栈图像是否近似相等，返回 1/0 布尔整数（元组容差版）。</summary>
	/// <param name="objects2">与当前图像栈逐张比较的测试对象。</param>
	/// <param name="epsilon">允许的最大灰度/坐标差，整条元组钉传。Default: 0.0</param>
	/// <returns>1=逐张逐像素均在容差内相等；0=不等。不产生新句柄，两栈均保持原样。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 573，<c>InitOCT(proc,0)</c>+<c>LoadI</c>：输出按 INTEGER 装载为 int 而非 bool，
	///   只给结论不抛异常。当前对象是第一路输入，与 objects2 栈内对应位置的图逐张比较，任一张超差即整体返回 0。</para>
	///   <para><b>与标量重载的取舍</b>单容差用 <see cref="CompareObj(JlImage,double)"/>（<c>StoreD</c> 直写、免钉）；
	///   本版把 epsilon 整条钉传后 <c>UnpinTuple</c>。多值 epsilon 是否逐通道分别容差本层未体现 [待实测]。
	///   裸 int 字面量会撞 double/JlTuple 的 CS0121 二义，须写 <c>new JlTuple(0.0)</c> 或 <c>0.0</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage imgA = new JlImage("byte", 64, 64);
	///   using JlImage imgB = imgA.CopyImage();
	///   int equal = imgA.CompareObj(imgB, new JlTuple(0.0));   // 副本必为 1，可做帧间去重
	///   </code>
	///   <para><b>资源与坑</b>两栈个数/尺寸不一致按"不等"处理而不报错 [待实测]；比较不改动输入，
	///   末尾对两栈 <c>GC.KeepAlive</c>。</para>
	/// </remarks>
	public int CompareObj(JlImage objects2, JlTuple epsilon)
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

	/// <summary>逐像素比较两栈图像是否近似相等，返回 1/0 布尔整数。</summary>
	/// <param name="objects2">与当前图像栈逐张比较的测试对象。</param>
	/// <param name="epsilon">两个灰度值（或坐标）之间允许的最大差值。Default: 0.0</param>
	/// <returns>1 表示两栈逐张、逐像素均在容差内相等；0 表示不相等。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 573。当前对象是第一路输入：把自身栈内每张图与 <paramref name="objects2"/> 栈内对应位置的图逐像素比较，任一张超差即整体返回 0。</para>
	///   <para><b>与同类算子的取舍</b>需要灰度容差时用本方法；只判严格等价用 <see cref="TestEqualObj"/>。与 <see cref="JlImage"/> 相等性判断不同，本方法不抛异常、只给 0/1，适合做帧间去重。</para>
	///   <para><b>参数取向</b>返回 int（非 bool），由原生侧按 INTEGER 装载（LoadI）；比较结果不落新句柄，两栈图像均保持原样。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage imgA = new JlImage("byte", 64, 64);
	///   JlImage imgB = new JlImage("byte", 64, 64);
	///   int isEqual = imgA.CompareObj(imgB, 0.0);
	///   imgA.Dispose();
	///   imgB.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两栈对象个数、尺寸或类型不一致时按"不等"处理而不会报错 [待实测]。元组重载（epsilon 为 JlTuple）与标量重载同用 id 573：元组重载调用后需钉住并解除固定（UnpinTuple），单值场景直接用本标量重载可省去该开销。返回的 0/1 不产生新句柄，但示例中两个图像句柄仍须各自 Dispose。</para>
	/// </remarks>
	public int CompareObj(JlImage objects2, double epsilon)
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

	/// <summary>不带容差地判断两栈图像对象是否等价，返回 1/0 布尔整数。</summary>
	/// <param name="objects2">与当前图像栈比较的对照对象。</param>
	/// <returns>1 表示等价；0 表示不等价。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 576。与 <see cref="CompareObj(JlImage,double)"/> 的区别：本方法没有 epsilon 参数，做的是无容差的严格比较，适合校验"同一句柄内容未被改动"或复制前后一致性检查。</para>
	///   <para><b>参数取向</b>返回 int（非 bool），原生侧按 INTEGER 装载；不产生新句柄，两栈均保持原样。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage imgA = new JlImage("byte", 64, 64);
	///   JlImage imgB = new JlImage("byte", 64, 64);
	///   int same = imgA.TestEqualObj(imgB);
	///   imgA.Dispose();
	///   imgB.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>需要容忍灰度抖动（例如压缩或噪声引起的 ±1）时不要用本方法，改用带 epsilon 的 CompareObj，否则帧间几乎永远返回 0。两栈个数或尺寸不一致时的返回约定 [待实测]。</para>
	/// </remarks>
	public int TestEqualObj(JlImage objects2)
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

	/// <summary>从交错（interleaved）像素缓冲生成三通道图像，原地改写当前句柄。</summary>
	/// <param name="pixelPointer">指向交错排列像素首地址的指针，通道按 colorFormat 顺序逐像素交替存放。</param>
	/// <param name="colorFormat">输入像素的通道顺序格式。Default: "rgb"</param>
	/// <param name="originalWidth">输入缓冲一行实际的像素个数（用于定位下一行）。Default: 512</param>
	/// <param name="originalHeight">输入缓冲的行数。Default: 512</param>
	/// <param name="alignment">保留参数，当前版本不使用。</param>
	/// <param name="type">输出图像像素类型。Default: "byte"</param>
	/// <param name="imageWidth">输出图像宽度，0 表示取 originalWidth。Default: 0</param>
	/// <param name="imageHeight">输出图像高度，0 表示取 originalHeight。Default: 0</param>
	/// <param name="startRow">所需图像部分左上角的行号。Default: 0</param>
	/// <param name="startColumn">所需图像部分左上角的列号。Default: 0</param>
	/// <param name="bitsPerChannel">输出图像每通道有效位数，-1 表示全部位。Default: -1</param>
	/// <param name="bitShift">颜色值右移位数（仅对 uint2 输入有意义）。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 580。从外部交错缓冲（如相机 SDK 的 RGBRGBRGB… 帧）零拷贝建图；本方法体开头先 Dispose 再 Load，属<b>原地改写</b>：调用后当前句柄即新图，旧句柄内容作废，不返回新对象。</para>
	///   <para><b>约束或前提</b>像素指针必须指向非托管可见且<b>不会被 GC 移动</b>的内存：托管数组须先 GCHandle.Pinned 固定或由非托管层持有；图像后续被其它算子读取前不得释放该内存 [待实测]。缓冲按 originalWidth 跨行寻址，与输出尺寸不同时靠 startRow/startColumn + originalWidth 抽取子区域。</para>
	///   <para><b>与相邻算子的取舍</b>数据已是三平面（R 整幅、G 整幅、B 整幅）时用 <see cref="GenImage3"/>，交错格式硬套 GenImage3 会得到通道串扰的图；只需单通道用 <see cref="GenImage1"/>。</para>
	///   <para><b>参数取向</b>返回 void，输出经 Load 写回 this；type 为 "uint2" 时才考虑 bitShift。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   byte[] rgb = new byte[64 * 64 * 3];
	///   System.Runtime.InteropServices.GCHandle pinned =
	///       System.Runtime.InteropServices.GCHandle.Alloc(rgb, System.Runtime.InteropServices.GCHandleType.Pinned);
	///   try
	///   {
	///       JlImage img = new JlImage();
	///       img.GenImageInterleaved(pinned.AddrOfPinnedObject(), "rgb", 64, 64, 0, "byte", 0, 0, 0, 0, -1, 0);
	///   }
	///   finally
	///   {
	///       pinned.Free();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>bitsPerChannel 与 type 位数不匹配会截断高位 [待实测]；本方法不复制像素，Free 固定句柄后继续用该图像读取像素属未定义行为。</para>
	/// </remarks>
	public void GenImageInterleaved(IntPtr pixelPointer, string colorFormat, int originalWidth, int originalHeight, int alignment, string type, int imageWidth, int imageHeight, int startRow, int startColumn, int bitsPerChannel, int bitShift)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(580);
		JlNativeApi.StoreIP(proc, 0, pixelPointer);
		JlNativeApi.StoreS(proc, 1, colorFormat);
		JlNativeApi.StoreI(proc, 2, originalWidth);
		JlNativeApi.StoreI(proc, 3, originalHeight);
		JlNativeApi.StoreI(proc, 4, alignment);
		JlNativeApi.StoreS(proc, 5, type);
		JlNativeApi.StoreI(proc, 6, imageWidth);
		JlNativeApi.StoreI(proc, 7, imageHeight);
		JlNativeApi.StoreI(proc, 8, startRow);
		JlNativeApi.StoreI(proc, 9, startColumn);
		JlNativeApi.StoreI(proc, 10, bitsPerChannel);
		JlNativeApi.StoreI(proc, 11, bitShift);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>从三个平面指针（R/G/B 各一整幅）生成三通道图像，原地改写当前句柄。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <param name="pixelPointerRed">第一通道（R）首个灰度值指针。</param>
	/// <param name="pixelPointerGreen">第二通道（G）首个灰度值指针。</param>
	/// <param name="pixelPointerBlue">第三通道（B）首个灰度值指针。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 590。三平面（planar）布局零拷贝建图：三个指针各指向 width*height 个连续同类像素。方法体先 Dispose 再 Load，属<b>原地改写</b>，不返回新句柄。</para>
	///   <para><b>约束或前提</b>三个缓冲在图像存续期内必须保持有效且不被 GC 移动（托管数组须 GCHandle 固定）；三块缓冲须同尺寸同类型，否则读像素越界。</para>
	///   <para><b>与相邻算子的取舍</b>相机给出的是逐像素交错的 RGBRGB… 时用 <see cref="GenImageInterleaved"/>；只要一个通道用 <see cref="GenImage1"/>。本方法在原生侧不复制像素，通道顺序由指针传入顺序决定（第一指针即通道 1，语义上叫 Red 但不强制）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   byte[] red = new byte[64 * 64];
	///   byte[] green = new byte[64 * 64];
	///   byte[] blue = new byte[64 * 64];
	///   System.Runtime.InteropServices.GCHandle h1 = System.Runtime.InteropServices.GCHandle.Alloc(red, System.Runtime.InteropServices.GCHandleType.Pinned);
	///   System.Runtime.InteropServices.GCHandle h2 = System.Runtime.InteropServices.GCHandle.Alloc(green, System.Runtime.InteropServices.GCHandleType.Pinned);
	///   System.Runtime.InteropServices.GCHandle h3 = System.Runtime.InteropServices.GCHandle.Alloc(blue, System.Runtime.InteropServices.GCHandleType.Pinned);
	///   try
	///   {
	///       JlImage img = new JlImage();
	///       img.GenImage3("byte", 64, 64, h1.AddrOfPinnedObject(), h2.AddrOfPinnedObject(), h3.AddrOfPinnedObject());
	///   }
	///   finally
	///   {
	///       h1.Free();
	///       h2.Free();
	///       h3.Free();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>与 GenImage3Extern 的区别：本算子假定内存生命周期由调用方管理且无释放回调；需要在图像销毁时自动回收非托管内存应改用 GenImage3Extern。</para>
	/// </remarks>
	public void GenImage3(string type, int width, int height, IntPtr pixelPointerRed, IntPtr pixelPointerGreen, IntPtr pixelPointerBlue)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(590);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreIP(proc, 3, pixelPointerRed);
		JlNativeApi.StoreIP(proc, 4, pixelPointerGreen);
		JlNativeApi.StoreIP(proc, 5, pixelPointerBlue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>从单通道像素指针建图，原地改写当前句柄。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <param name="pixelPointer">指向首个灰度值的指针，像素按行连续存放。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 591。把外部单通道缓冲包成图像；方法体先 Dispose 再 Load，属<b>原地改写</b>，不返回新句柄。</para>
	///   <para><b>约束或前提</b>缓冲须连续按行存放、长度至少 width*height*每像素字节数；托管数组要先 GCHandle 固定，且在不再使用图像前保持固定，本算子不复制像素 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>需要"复制一份、之后缓冲可立即释放"的语义时用 <see cref="GenImage1Rect"/>（doCopy 传 "true"）；需要图像销毁时回调释放内存用 <see cref="GenImage1Extern"/>；三通道用 <see cref="GenImage3"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   byte[] gray = new byte[64 * 64];
	///   System.Runtime.InteropServices.GCHandle h = System.Runtime.InteropServices.GCHandle.Alloc(gray, System.Runtime.InteropServices.GCHandleType.Pinned);
	///   try
	///   {
	///       JlImage img = new JlImage();
	///       img.GenImage1("byte", 64, 64, h.AddrOfPinnedObject());
	///   }
	///   finally
	///   {
	///       h.Free();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>域（domain）为整幅矩形；只想造一块全常数图（不需要外部内存）应改用 <see cref="GenImageConst"/>，由运行时管理内存、无悬挂指针风险。</para>
	/// </remarks>
	public void GenImage1(string type, int width, int height, IntPtr pixelPointer)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(591);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreIP(proc, 3, pixelPointer);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>创建整幅为常数灰度的单通道图像，原地改写当前句柄。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 592。内存由视觉运行时自行分配并全部置为该类型的常数灰度（0）；方法体先 Dispose 再 Load，属<b>原地改写</b>，不返回新句柄。</para>
	///   <para><b>与相邻算子的取舍</b>想包一块已有像素缓冲用 <see cref="GenImage1"/>；想要线性灰度坡用 <see cref="GenImageGrayRamp"/>。本算子是拿"干净底图"（做叠加、掩码、计时占位）最省事的途径，无外部内存生命周期负担。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage();
	///   img.GenImageConst("uint2", 512, 512);
	///   </code>
	///   <para><b>资源与坑</b>uint2 图像常数值 0 并非"最暗可视值"意义上的 12bit 起点，后续与 8bit 图做 AddImage 等运算前先注意位深不一致会被类型检查拒绝 [待实测]。生成多通道常数图没有对应参数，需自行 AppendChannel 拼接。</para>
	/// </remarks>
	public void GenImageConst(string type, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(592);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>生成线性灰度坡图像（坡率按行/列方向给定），原地改写当前句柄。</summary>
	/// <param name="alpha">沿行方向（row 增大）每行灰度增量。Default: 1.0</param>
	/// <param name="beta">沿列方向（column 增大）每列灰度增量。Default: 1.0</param>
	/// <param name="mean">参考点处的灰度值。Default: 128</param>
	/// <param name="row">参考点的行号。Default: 256</param>
	/// <param name="column">参考点的列号。Default: 256</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 604。像素灰度按 gray(r,c) = mean + alpha*(r-row) + beta*(c-column) 线性铺展；方法体先 Dispose 再 Load，属<b>原地改写</b>。</para>
	///   <para><b>约束或前提</b>参考点 (row,column) 不必落在图内，落在图外则整幅位于坡面同一侧；byte 类型下越出 0…255 的像素会按饱和处理 [待实测]，标定照明均匀性时建议先用 float 类型验证坡幅。</para>
	///   <para><b>与相邻算子的取舍</b>要常数底图用 <see cref="GenImageConst"/>；要模拟渐晕/平场不均时本算子的两个独立坡率（行、列）比先建图再乘系数更省一步，但无法表达径向渐变。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage ramp = new JlImage();
	///   ramp.GenImageGrayRamp(1.0, 2.0, 128, 256, 256, 512, 512);
	///   </code>
	///   <para><b>资源与坑</b>alpha/beta 为 double、参考点行列与宽高为 int，原生装载序为 D,D,D,I,I,I,I，与 C# 形参序一致（无重排）。产物是单通道图，参与彩色流程前需 ChannelsToImage 合成。</para>
	/// </remarks>
	public void GenImageGrayRamp(double alpha, double beta, double mean, int row, int column, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(604);
		JlNativeApi.StoreD(proc, 0, alpha);
		JlNativeApi.StoreD(proc, 1, beta);
		JlNativeApi.StoreD(proc, 2, mean);
		JlNativeApi.StoreI(proc, 3, row);
		JlNativeApi.StoreI(proc, 4, column);
		JlNativeApi.StoreI(proc, 5, width);
		JlNativeApi.StoreI(proc, 6, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>从三个平面指针建三通道图并可选注册内存释放回调，原地改写当前句柄。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <param name="pointerRed">第一通道首个灰度值指针。</param>
	/// <param name="pointerGreen">第二通道首个灰度值指针。</param>
	/// <param name="pointerBlue">第三通道首个灰度值指针。</param>
	/// <param name="clearProc">图像销毁时调用的内存释放过程指针，0 表示不回调。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 605。与 <see cref="GenImage3"/> 同为三平面零拷贝建图，多出的 clearProc 让运行时在 Dispose 该图像时代为释放非托管缓冲，实现"图像持有并接管内存"。</para>
	///   <para><b>约束或前提</b>clearProc 只适用于非托管内存（如 Marshal.AllocHGlobal 或原生分配器）；对托管数组传回调是错误用法——托管数组须 GCHandle 固定且回调无法回收它。传 0 时内存管理责任仍在调用方。</para>
	///   <para><b>与相邻算子的取舍</b>拿不准释放时机就不要传回调、改用 GenImage3 自管生命周期；单通道对应 <see cref="GenImage1Extern"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   IntPtr red = System.Runtime.InteropServices.Marshal.AllocHGlobal(64 * 64);
	///   IntPtr green = System.Runtime.InteropServices.Marshal.AllocHGlobal(64 * 64);
	///   IntPtr blue = System.Runtime.InteropServices.Marshal.AllocHGlobal(64 * 64);
	///   JlImage img = new JlImage();
	///   img.GenImage3Extern("byte", 64, 64, red, green, blue, IntPtr.Zero);
	///   img.Dispose();
	///   System.Runtime.InteropServices.Marshal.FreeHGlobal(red);
	///   System.Runtime.InteropServices.Marshal.FreeHGlobal(green);
	///   System.Runtime.InteropServices.Marshal.FreeHGlobal(blue);
	///   </code>
	///   <para><b>资源与坑</b>示例传 0 意味着自行负责释放三块 HGlobal 内存；若把某块内存交给 clearProc 接管后又手动释放同一块，会二次释放崩溃。图像存续期内不得移动或释放缓冲。</para>
	/// </remarks>
	public void GenImage3Extern(string type, int width, int height, IntPtr pointerRed, IntPtr pointerGreen, IntPtr pointerBlue, IntPtr clearProc)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(605);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreIP(proc, 3, pointerRed);
		JlNativeApi.StoreIP(proc, 4, pointerGreen);
		JlNativeApi.StoreIP(proc, 5, pointerBlue);
		JlNativeApi.StoreIP(proc, 6, clearProc);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>从单通道指针建图并可选注册内存释放回调，原地改写当前句柄。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <param name="pixelPointer">指向首个灰度值的指针。</param>
	/// <param name="clearProc">图像销毁时调用的内存释放过程指针，0 表示不回调。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 606。相对 <see cref="GenImage1"/> 增加 clearProc：图像对象被删除时由运行时回调释放该像素内存，适合把 Marshal.AllocHGlobal 或原生帧缓冲的生命周期交给图像接管。</para>
	///   <para><b>约束或前提</b>回调只应对非托管内存注册；托管数组用本方法会在数组被 GC 回收后留下悬挂图像。传 0 则调用方自管释放时机（必须晚于图像最后一次使用）。</para>
	///   <para><b>与相邻算子的取舍</b>想要"复制一份再脱钩"用 <see cref="GenImage1Rect"/> 的 doCopy="true"；三通道用 <see cref="GenImage3Extern"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   IntPtr buf = System.Runtime.InteropServices.Marshal.AllocHGlobal(64 * 64);
	///   JlImage img = new JlImage();
	///   img.GenImage1Extern("byte", 64, 64, buf, IntPtr.Zero);
	///   img.Dispose();
	///   System.Runtime.InteropServices.Marshal.FreeHGlobal(buf);
	///   </code>
	///   <para><b>资源与坑</b>同一块内存被注册给两张图像（clearProc 非 0）会二次释放；本示例因回调传 0 而手动配对释放。</para>
	/// </remarks>
	public void GenImage1Extern(string type, int width, int height, IntPtr pixelPointer, IntPtr clearProc)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(606);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreIP(proc, 3, pixelPointer);
		JlNativeApi.StoreIP(proc, 4, clearProc);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>从带行距/位距的任意外部缓冲抠出矩形区域建图，原地改写当前句柄。</summary>
	/// <param name="pixelPointer">指向首像素的指针，可指向外部大图内部的子矩形起点。</param>
	/// <param name="width">图像宽度。Default: 512</param>
	/// <param name="height">图像高度。Default: 512</param>
	/// <param name="verticalPitch">外部缓冲中相邻两行同列像素间的字节距离（= 外部行字节跨度，可大于 width*像素字节数）。</param>
	/// <param name="horizontalBitPitch">外部缓冲中相邻两像素间的位距离。Default: 8</param>
	/// <param name="bitsPerPixel">每像素有效位数。Default: 8</param>
	/// <param name="doCopy">"true" 时复制像素数据、与原缓冲脱钩；"false" 时仅引用。Default: "false"</param>
	/// <param name="clearProc">图像销毁时的内存释放过程指针，0 表示不回调。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 607。专为"外部图像行不对齐/位打包"场景设计：verticalPitch 以<b>字节</b>计、horizontalBitPitch 以<b>位</b>计，两值允许与紧凑布局不同（如 4:2:2 或带 padding 的 stride）；方法体先 Dispose 再 Load，属<b>原地改写</b>。</para>
	///   <para><b>约束或前提</b>horizontalBitPitch 小于 bitsPerPixel 表示位打包格式，逐像素按位距离寻址；参数组合不合法（如位距为 0）时行为未定义 [待实测]。像素类型面向 8 位字节图 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只有连续紧凑的整幅缓冲时用更简单的 <see cref="GenImage1"/>；需要在原缓冲可释放后仍安全使用图像，务必 doCopy="true"，代价是一次内存复制。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   byte[] buf = new byte[100 * 64];
	///   System.Runtime.InteropServices.GCHandle h = System.Runtime.InteropServices.GCHandle.Alloc(buf, System.Runtime.InteropServices.GCHandleType.Pinned);
	///   JlImage img = new JlImage();
	///   try
	///   {
	///       img.GenImage1Rect(h.AddrOfPinnedObject(), 64, 64, 100, 8, 8, "true", IntPtr.Zero);
	///   }
	///   finally
	///   {
	///       h.Free();
	///   }
	///   </code>
	///   <para><b>资源与坑</b>示例因 doCopy="true" 才允许在 finally 立即解除固定；若传 "false"，h.Free() 之后图像像素即为悬挂读。clearProc 语义同 <see cref="GenImage1Extern"/>。</para>
	/// </remarks>
	public void GenImage1Rect(IntPtr pixelPointer, int width, int height, int verticalPitch, int horizontalBitPitch, int bitsPerPixel, string doCopy, IntPtr clearProc)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(607);
		JlNativeApi.StoreIP(proc, 0, pixelPointer);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreI(proc, 3, verticalPitch);
		JlNativeApi.StoreI(proc, 4, horizontalBitPitch);
		JlNativeApi.StoreI(proc, 5, bitsPerPixel);
		JlNativeApi.StoreS(proc, 6, doCopy);
		JlNativeApi.StoreIP(proc, 7, clearProc);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>取图像域最小外接矩形对应的像素指针及行/位距信息。</summary>
	/// <param name="width">输出外接矩形宽度。</param>
	/// <param name="height">输出外接矩形高度。</param>
	/// <param name="verticalPitch">相邻两行的字节距离，等于 输入图宽*(HorizontalBitPitch/8)。</param>
	/// <param name="horizontalBitPitch">相邻两像素的位距离。</param>
	/// <param name="bitsPerPixel">每像素有效位数。</param>
	/// <returns>指向外接矩形首像素的非托管指针（不是新句柄，无需释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 608。与 <see cref="GetImagePointer1(out string,out int,out int)"/> 不同：返回的是<b>域（domain）最小外接矩形</b>的指针与布局参数，域被 ReduceDomain 缩小后行距仍按原图宽计，须用 verticalPitch 跨行寻址，不能假定 width*height 连续。</para>
	///   <para><b>约束或前提</b>仅单通道图像可用（彩色图用 GetImagePointer3）；域为空时外接矩形退化，指针无效 [待实测]。</para>
	///   <para><b>参数取向</b>1 返回 + 5 个 out，均按 INTEGER 装载（LoadI/LoadIP）；不产生新图像句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   IntPtr ptr = img.GetImagePointer1Rect(out int width, out int height,
	///       out int verticalPitch, out int horizontalBitPitch, out int bitsPerPixel);
	///   byte firstPixel = System.Runtime.InteropServices.Marshal.ReadByte(ptr);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>指针存活期受图像句柄约束（实现末尾 GC.KeepAlive(this) 只保证本次调用内不被释放）：任何原地改写 img 的 Gen*/Paint* 之后再解 ptr 都是悬挂读；Marshal.ReadByte 示例仅演示读取第一像素，逐行遍历须 ptr + n*verticalPitch。</para>
	/// </remarks>
	public IntPtr GetImagePointer1Rect(out int width, out int height, out int verticalPitch, out int horizontalBitPitch, out int bitsPerPixel)
	{
		IntPtr proc = JlNativeApi.PreCall(608);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadIP(proc, 0, err, out var intPtrValue);
		err = JlNativeApi.LoadI(proc, 1, err, out width);
		err = JlNativeApi.LoadI(proc, 2, err, out height);
		err = JlNativeApi.LoadI(proc, 3, err, out verticalPitch);
		err = JlNativeApi.LoadI(proc, 4, err, out horizontalBitPitch);
		err = JlNativeApi.LoadI(proc, 5, err, out bitsPerPixel);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intPtrValue;
	}

	/// <summary>按栈内每张图取彩色图像三通道指针与规格（元组版）。</summary>
	/// <param name="pointerRed">各图第一通道像素指针，INTEGER 元组。</param>
	/// <param name="pointerGreen">各图第二通道像素指针，INTEGER 元组。</param>
	/// <param name="pointerBlue">各图第三通道像素指针，INTEGER 元组。</param>
	/// <param name="type">各图类型字符串元组。</param>
	/// <param name="width">各图宽度元组。</param>
	/// <param name="height">各图高度元组。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 609。当前句柄是图像栈时，本重载为<b>每张图各占一个元素</b>返回六条元组（LoadNew，指针按 INTEGER 装载）；这是访问栈内第 2 张以后图像指针的唯一途径。</para>
	///   <para><b>约束或前提</b>图像须为三通道；单通道图调用失败 [待实测]。指针元素只能解引用到对应图像 Dispose 之前。</para>
	///   <para><b>与相邻算子的取舍</b>只关心栈中第一张图时用标量重载 <see cref="GetImagePointer3(out IntPtr,out IntPtr,out IntPtr,out string,out int,out int)"/>，免去六条元组的分配与固定开销；元组版适合批量导出到外部编解码缓冲。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage rgb = new JlImage("byte", 64, 64);
	///   rgb.GetImagePointer3(out JlTuple pr, out JlTuple pg, out JlTuple pb,
	///       out JlTuple type, out JlTuple width, out JlTuple height);
	///   </code>
	///   <para><b>资源与坑</b>示例中的构造仅示意调用形式，本算子要求图像确为三通道；解出的 INTEGER 指针值在原生侧不受引用计数保护，遍历前先读出 type/width/height 并换算每像素字节数，别越过缓冲末尾，且在图像 Dispose 后不得再用。</para>
	/// </remarks>
	public void GetImagePointer3(out JlTuple pointerRed, out JlTuple pointerGreen, out JlTuple pointerBlue, out JlTuple type, out JlTuple width, out JlTuple height)
	{
		IntPtr proc = JlNativeApi.PreCall(609);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out pointerRed);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out pointerGreen);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out pointerBlue);
		err = JlTuple.LoadNew(proc, 3, err, out type);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out width);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.INTEGER, err, out height);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>取彩色图像（栈中第一张）三通道指针与规格的标量版。</summary>
	/// <param name="pointerRed">第一通道像素指针。</param>
	/// <param name="pointerGreen">第二通道像素指针。</param>
	/// <param name="pointerBlue">第三通道像素指针。</param>
	/// <param name="type">图像类型字符串。</param>
	/// <param name="width">图像宽度。</param>
	/// <param name="height">图像高度。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 609 的标量装载版：LoadIP/LoadS/LoadI 各只读原生输出的<b>第一个值</b>。</para>
	///   <para><b>与相邻算子的取舍</b>与元组重载的关键差异在此：当前句柄若含多张图，本重载会<b>静默丢弃第一张以外的全部结果</b>——不报错、不截断提示；需要逐张遍历栈时必须改用 <see cref="GetImagePointer3(out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>。</para>
	///   <para><b>约束或前提</b>图像须为三通道，单通道图调用失败 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage rgb = new JlImage("byte", 64, 64);
	///   rgb.GetImagePointer3(out IntPtr pr, out IntPtr pg, out IntPtr pb,
	///       out string type, out int width, out int height);
	///   </code>
	///   <para><b>资源与坑</b>三个指针在 rgb 被原地改写或 Dispose 后立即失效；示例中的构造仅示意形式，真实调用前三通道前提必须成立。</para>
	/// </remarks>
	public void GetImagePointer3(out IntPtr pointerRed, out IntPtr pointerGreen, out IntPtr pointerBlue, out string type, out int width, out int height)
	{
		IntPtr proc = JlNativeApi.PreCall(609);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadIP(proc, 0, err, out pointerRed);
		err = JlNativeApi.LoadIP(proc, 1, err, out pointerGreen);
		err = JlNativeApi.LoadIP(proc, 2, err, out pointerBlue);
		err = JlNativeApi.LoadS(proc, 3, err, out type);
		err = JlNativeApi.LoadI(proc, 4, err, out width);
		err = JlNativeApi.LoadI(proc, 5, err, out height);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>按栈内每张图取单通道像素指针与规格（元组版）。</summary>
	/// <param name="type">各图类型字符串元组。</param>
	/// <param name="width">各图宽度元组（INTEGER）。</param>
	/// <param name="height">各图高度元组（INTEGER）。</param>
	/// <returns>各图像素指针组成的 INTEGER 元组，一张图一个元素；是新元组，不需释放句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 610。图像栈的每张图各占一个输出元素（LoadNew），是逐张访问栈内像素缓冲的唯一途径；type 按字符串装载、其余按 INTEGER 装载。</para>
	///   <para><b>与相邻算子的取舍</b>单通道图用它；三通道用 <see cref="GetImagePointer3(out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>；需要域外接矩形布局参数（行距/位距）用 <see cref="GetImagePointer1Rect"/>；只关心第一张图时用标量重载更省。</para>
	///   <para><b>约束或前提</b>对彩色图调用会失败（原生要求 1 通道）[待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlTuple ptrs = img.GetImagePointer1(out JlTuple type, out JlTuple width, out JlTuple height);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的指针值只在 img 存活且未被原地改写期间有效；JlTuple 不实现 IDisposable，无释放负担但也不要跨线程长期持有指针。</para>
	/// </remarks>
	public JlTuple GetImagePointer1(out JlTuple type, out JlTuple width, out JlTuple height)
	{
		IntPtr proc = JlNativeApi.PreCall(610);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, err, out type);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out width);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out height);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>取栈中第一张单通道图的像素指针与规格（标量版）。</summary>
	/// <param name="type">图像类型字符串。</param>
	/// <param name="width">图像宽度。</param>
	/// <param name="height">图像高度。</param>
	/// <returns>指向像素数据的非托管指针（非新句柄，勿单独释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 610 的标量装载版：LoadIP/LoadS/LoadI 只读原生输出的<b>第一个值</b>。</para>
	///   <para><b>与相邻算子的取舍</b>与元组重载同 id，但句柄含多张图时本重载会<b>静默丢弃第一张以外的所有指针/规格</b>，批量遍历必须改用元组重载；单图取指针本重载免去元组分配。彩色图应使用 GetImagePointer3 族。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   IntPtr ptr = img.GetImagePointer1(out string type, out int width, out int height);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回指针的有效期止于 img 的 Dispose 或任何原地改写；type/width/height 与指针同源，遍历前先用它们核对缓冲长度。</para>
	/// </remarks>
	public IntPtr GetImagePointer1(out string type, out int width, out int height)
	{
		IntPtr proc = JlNativeApi.PreCall(610);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadIP(proc, 0, err, out var intPtrValue);
		err = JlNativeApi.LoadS(proc, 1, err, out type);
		err = JlNativeApi.LoadI(proc, 2, err, out width);
		err = JlNativeApi.LoadI(proc, 3, err, out height);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intPtrValue;
	}

	/// <summary>取图像像素类型（栈内每张图一个元素）。</summary>
	/// <returns>类型字符串组成的新 JlTuple，如 "byte"/"int1"/"uint2"/"float"；不是句柄，无需释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 611，LoadNew 按字符串装载。彩色图的类型也只报一次（各通道类型一致），不含通道数信息——想知道通道数用 <see cref="CountChannels()"/>。</para>
	///   <para><b>与相邻算子的取舍</b>与 <see cref="GetImagePointer1(out string,out int,out int)"/> 相比不触像素缓冲，可在解引用前安全探测位深以决定步长。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("uint2", 64, 64);
	///   JlTuple types = img.GetImageType();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>uint2 图灰度上限 4095 而非 65535 [待实测]，按 255 阈值处理 uint2 图会整图判白，先查类型再定 Threshold 范围。</para>
	/// </remarks>
	public JlTuple GetImageType()
	{
		IntPtr proc = JlNativeApi.PreCall(611);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>取栈内每张图像的宽与高（元组版，INTEGER 装载）。</summary>
	/// <param name="width">各图宽度元组。</param>
	/// <param name="height">各图高度元组。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 612 的元组版：句柄含 N 张图时输出 N 个元素的元组，用于核对 ConcatObj/TileImages 之后栈内各成员尺寸是否一致。</para>
	///   <para><b>与相邻算子的取舍</b>只要第一张图的尺寸时用标量重载 <see cref="GetImageSize(out int,out int)"/>，省两条元组分配；元组版是为批量一致性检查设计的。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 48);
	///   img.GetImageSize(out JlTuple width, out JlTuple height);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是像素尺寸而非域尺寸：ReduceDomain 之后宽高不变，域范围要用 <see cref="GetDomain()"/>。</para>
	/// </remarks>
	public void GetImageSize(out JlTuple width, out JlTuple height)
	{
		IntPtr proc = JlNativeApi.PreCall(612);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out width);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out height);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>取栈中第一张图像的宽与高（标量版）。</summary>
	/// <param name="width">图像宽度（像素个数）。</param>
	/// <param name="height">图像高度（行数）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 612 的标量装载版：LoadI 只读第一个值。</para>
	///   <para><b>与相邻算子的取舍</b>句柄含多张图时本重载<b>静默丢弃第一张以外的尺寸</b>；批量核对尺寸请用元组重载 <see cref="GetImageSize(out JlTuple,out JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 48);
	///   img.GetImageSize(out int width, out int height);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>width/height 是全幅像素尺寸，与域（domain）范围无关；坐标合法范围是 0..width-1 与 0..height-1（闭区间）。</para>
	/// </remarks>
	public void GetImageSize(out int width, out int height)
	{
		IntPtr proc = JlNativeApi.PreCall(612);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out width);
		err = JlNativeApi.LoadI(proc, 1, err, out height);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>读取图像对象内记录的时间戳（毫秒为返回值，其余字段 out）。</summary>
	/// <param name="second">秒（0..59）。</param>
	/// <param name="minute">分（0..59）。</param>
	/// <param name="hour">时（0..23）。</param>
	/// <param name="day">当月日（1..31）。</param>
	/// <param name="YDay">当年日（1..366）。</param>
	/// <param name="month">月（1..12）。</param>
	/// <param name="year">四位年份。</param>
	/// <returns>毫秒（0..999），按 INTEGER 装载。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 613。时间戳由图像创建时的运行环境写入：本库已无 framegrabber 采集，Gen*/读文件所得图像通常即"本次创建时刻"，不要指望它反映真实曝光时间 [待实测]。</para>
	///   <para><b>参数取向</b>1 返回 + 7 个 out 共 8 个 INTEGER 输出；年月日与时分秒字段同时给出，day/YDay 二者信息重复，取任一即可。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   int ms = img.GetImageTime(out int second, out int minute, out int hour,
	///       out int day, out int YDay, out int month, out int year);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>对同一图像的多次读取不会变化；但任何原地改写（如 PaintRegion 直接画进 this）之后时间戳是否刷新为当前时刻 [待实测]，做帧序追踪请改用外部计数器而不是本方法。</para>
	/// </remarks>
	public int GetImageTime(out int second, out int minute, out int hour, out int day, out int YDay, out int month, out int year)
	{
		IntPtr proc = JlNativeApi.PreCall(613);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlNativeApi.LoadI(proc, 1, err, out second);
		err = JlNativeApi.LoadI(proc, 2, err, out minute);
		err = JlNativeApi.LoadI(proc, 3, err, out hour);
		err = JlNativeApi.LoadI(proc, 4, err, out day);
		err = JlNativeApi.LoadI(proc, 5, err, out YDay);
		err = JlNativeApi.LoadI(proc, 6, err, out month);
		err = JlNativeApi.LoadI(proc, 7, err, out year);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>在亚像素坐标处按指定插值取一组灰度值（元组版，结果为 DOUBLE）。</summary>
	/// <param name="row">采样点行坐标元组（double，可为亚像素）。Default: 0</param>
	/// <param name="column">采样点列坐标元组（double，可为亚像素）。Default: 0</param>
	/// <param name="interpolation">插值方法。Default: "bilinear"</param>
	/// <returns>各采样点灰度值组成的新 JlTuple，按 DOUBLE 装载；多点多通道时元素数会成倍增加 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 614。像素中心位于整数坐标 (row, column)；给出非整坐标时由 interpolation 决定邻域混合，结果保留小数——与 <see cref="GetGrayval(JlTuple,JlTuple)"/> 只取整数像素、结果按像素类型取整不同。</para>
	///   <para><b>约束或前提</b>row/column 必须等长；坐标越出图像或邻域触及域外时该点结果的约定 [待实测]，采样前应自行裁剪坐标。</para>
	///   <para><b>与相邻算子的取舍</b>批量曲线采样用本元组重载（一次钉住两个元组、一次调用）；单点探测用标量重载省去 UnpinTuple 固定开销。要精确像素值而不要混合值时必须用 GetGrayval，用双线性会在边缘处引入半值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlTuple vals = img.GetGrayvalInterpolated(new double[] { 10.5, 20.0 },
	///       new double[] { 30.0, 40.25 }, "bilinear");
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>元组重载调用中 row/column 被钉住、调用后 UnpinTuple；返回 DOUBLE 意味着 byte 图也会得到 128.75 这类值，做等值比较前想清楚取整策略。</para>
	/// </remarks>
	public JlTuple GetGrayvalInterpolated(JlTuple row, JlTuple column, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(614);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.StoreS(proc, 2, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>在单个亚像素坐标处插值取灰度值（标量版）。</summary>
	/// <param name="row">采样点行坐标（double，可为亚像素）。Default: 0</param>
	/// <param name="column">采样点列坐标（double，可为亚像素）。Default: 0</param>
	/// <param name="interpolation">插值方法。Default: "bilinear"</param>
	/// <returns>该点插值灰度值，按 DOUBLE 装载（LoadD 只读原生输出的第一个值）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 614 的标量版：StoreD 直写单值，无元组钉住/解固定开销，与元组重载同 id。</para>
	///   <para><b>与相邻算子的取舍</b>多点批量采样不要循环调用本重载（每点一次原生调用），应改用元组重载一次取回；只要整数像素原值时用 <see cref="GetGrayval(int,int)"/>。</para>
	///   <para><b>约束或前提</b>像素中心在整数坐标；坐标越界或邻域触域外的行为 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   double g = img.GetGrayvalInterpolated(10.5, 20.0, "bilinear");
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>若图像为多通道，原生输出含逐通道多个值，本重载只回第一个通道值——其余通道被静默丢弃；需要全部通道值时用元组重载。</para>
	/// </remarks>
	public double GetGrayvalInterpolated(double row, double column, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(614);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreS(proc, 2, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>取一批整数坐标像素的灰度值（元组版）。</summary>
	/// <param name="row">各采样点行坐标（整数）元组。Default: 0</param>
	/// <param name="column">各采样点列坐标（整数）元组。Default: 0</param>
	/// <returns>各点（及多通道图的各通道）灰度值组成的新 JlTuple。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 615。不做任何插值/取整换算，返回的就是像素存储值；对多通道图，每点会按通道连续给出多个值 [待实测]。</para>
	///   <para><b>约束或前提</b>坐标必须是落在图内且在域内的整数点，越界或触域外时原生报错而非返回缺省值 [待实测]；row/column 等长。</para>
	///   <para><b>与相邻算子的取舍</b>亚像素位置要用 <see cref="GetGrayvalInterpolated(JlTuple,JlTuple,string)"/>；本元组重载适合掩模抽检等批量点位。与标量 int 重载同 id，差异仅在元组钉住（Store+UnpinTuple）与 StoreI 直写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlTuple g = img.GetGrayval(new int[] { 10, 20 }, new int[] { 30, 40 });
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>float 图的返回值可能带小数（DOUBLE 元组），byte 图则是整数值；对返回值做元素运算前先看 GetImageType。</para>
	/// </remarks>
	public JlTuple GetGrayval(JlTuple row, JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(615);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>取单个整数坐标像素的灰度值（标量版，返回仍是元组）。</summary>
	/// <param name="row">像素行坐标。Default: 0</param>
	/// <param name="column">像素列坐标。Default: 0</param>
	/// <returns>该像素灰度值元组：单通道图只有一个元素，多通道图按通道给出多个元素 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 615 的标量版：StoreI 直写坐标，无元组钉住开销。注意返回类型不是 double/int 而是 JlTuple——因为一个点在彩色图上对应多个通道值，签名故意不做截断。</para>
	///   <para><b>与相邻算子的取舍</b>多点批量用元组重载 <see cref="GetGrayval(JlTuple,JlTuple)"/>（一次调用换 N 点）；坐标非整数用 GetGrayvalInterpolated 族。</para>
	///   <para><b>约束或前提</b>坐标越界或在域外时原生侧行为为错误而非零值 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlTuple gray = img.GetGrayval(10, 20);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>调用方常误以为返回单值而直接强转；应取元组首元素并自行判长度。本重载不会像某些 LoadI/LoadD 标量重载那样丢多值——它原样带回整条元组。</para>
	/// </remarks>
	public JlTuple GetGrayval(int row, int column)
	{
		IntPtr proc = JlNativeApi.PreCall(615);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}








	/// <summary>以域的外接矩形为基准四边各裁去指定行数，返回裁剪后的新图像句柄。</summary>
	/// <param name="top">上边裁掉的行数。Default: -1</param>
	/// <param name="left">左边裁掉的列数。Default: -1</param>
	/// <param name="bottom">下边裁掉的行数。Default: -1</param>
	/// <param name="right">右边裁掉的列数。Default: -1</param>
	/// <returns>裁剪结果的新图像句柄（LoadNew），需释放；当前对象不受影响。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 705。基准是<b>域</b>的外接矩形而不是全幅：先取域包围盒，再按四个参数收缩，输出图的宽高即收缩后的矩形尺寸。</para>
	///   <para><b>约束或前提</b>-1 表示该边不裁；裁缩量超过包围盒一半导致宽高退化为 0 时的行为 [待实测]。域为空时无法定基准 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要按绝对坐标裁剪用 CropRectangle1/CropPart；要"贴紧域"一刀切边用无参 CropDomain；本算子适合在域上再去掉毛边（例如 GenRectangle 后各缩 2 像素避开羽化）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion dom = img.Threshold(1.0, 255.0);
	///   using JlImage trimmed = img.ReduceDomain(dom).CropDomainRel(2, 2, 2, 2);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；像素保留、域被裁小后若还想统计"原域"要先算再裁。输出图的域约定（全幅矩形还是原域平移）[待实测]。</para>
	/// </remarks>
	public JlImage CropDomainRel(int top, int left, int bottom, int right)
	{
		IntPtr proc = JlNativeApi.PreCall(705);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, top);
		JlNativeApi.StoreI(proc, 1, left);
		JlNativeApi.StoreI(proc, 2, bottom);
		JlNativeApi.StoreI(proc, 3, right);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}








	/// <summary>灰度黑帽：闭运算结果减去原图，突出比邻域暗的窄谷。</summary>
	/// <param name="SE">灰度结构元图像。</param>
	/// <returns>黑帽图像的新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 759。黑帽 = <see cref="GrayClosing(JlImage)"/> − 原图，
	///   保留"比周围低、且宽度小于 SE"的暗结构：划痕、凹坑、压印缺口。SE 的尺寸/灰度取法与顶帽完全对称，
	///   共用一套注意事项，见 <see cref="GrayTophat(JlImage)"/>。</para>
	///   <para><b>与相邻算子的取舍</b>目标比背景<b>亮</b>时用 <see cref="GrayTophat(JlImage)"/>；
	///   想把暗结构直接抹平而不是提出来，用 <c>GrayClosing</c>。
	///   不要用"先 <c>InvertImage</c> 再顶帽"来代替黑帽：反相会改变后续阈值的量纲含义，还得再反回来。</para>
	///   <para><b>输出</b>是图像，且黑帽图的灰度以"0 = 无谷"为基准，背景被压平到接近 0，
	///   因此阈值下限通常取几而不是 128 [待实测：byte 输出是否含偏置]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage se = new JlImage();
	///   se.GenDiscSe("byte", 9, 9, 30.0);
	///   using JlImage valleys = img.GrayBothat(se);
	///   using JlRegion scratches = valleys.Threshold(15.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b><paramref name="SE"/> 只读；返回新句柄需释放。</para>
	/// </remarks>
	public JlImage GrayBothat(JlImage SE)
	{
		IntPtr proc = JlNativeApi.PreCall(759);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, SE);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(SE);
		return obj;
	}

	/// <summary>灰度顶帽：原图减去开运算结果，突出比邻域亮的窄峰（输出图像）。</summary>
	/// <param name="SE">灰度结构元图像。</param>
	/// <returns>顶帽图像的新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 760。英文说明 bottom/top hat 成对：顶帽 = 原图 − <see cref="GrayOpening(JlImage)"/>，
	///   留下的是"比周围高、且宽度小于 SE"的亮结构（划痕亮点、灰尘、字符笔画），背景趋势被抵消掉。
	///   反过来的 <see cref="GrayBothat(JlImage)"/> 用闭运算提取暗谷。</para>
	///   <para><b>SE 是图像不是区域</b><paramref name="SE"/> 走第 3 个槽位（<c>Store(proc, 2, SE)</c>），
	///   它带灰度值：SE 的<b>尺寸</b>决定多宽的结构会被当作"背景"保留、多窄的会被顶帽留下，SE 的<b>灰度峰值</b>
	///   （见 <c>GenDiscSe</c> 的 <c>smax</c>）决定多高的峰才够格。所以 SE 要给"刚好比目标宽一点、比目标高一点"的帽状体，
	///   给大了目标整体被当背景吃掉，给小了噪声全留下。</para>
	///   <para><b>与相邻算子的取舍</b>只要区域级别的去毛刺/断开粘连，用 <see cref="JlRegion"/> 上的二值形态学，不要用本族：
	///   本族<b>输出仍是图像</b>（<c>LoadNew(proc,1,...)</c> 取 <c>JlImage</c>），接成 <c>JlRegion</c> 是常见错误，
	///   灰度结果通常还要再 <c>Threshold</c> 才能统计。想按固定矩形/圆盘 SE 做同类滤波，用更省事的
	///   <see cref="GrayOpeningRect(int,int)"/>/<see cref="GrayClosingShape(JlTuple,JlTuple,string)"/>，不必自己造 SE 图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage se = new JlImage();               // 无参构造：未初始化句柄
	///   se.GenDiscSe("byte", 7, 7, 40.0);              // 原地生成帽状灰度结构元
	///   using JlImage peaks = img.GrayTophat(se);      // 只剩比局部背景高的窄亮结构
	///   using JlRegion dust = peaks.Threshold(10.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>返回新图像句柄需释放；<paramref name="SE"/> 只读，调用结束前由
	///   <c>GC.KeepAlive</c> 保命，本层不检查 SE 尺寸是否为奇数、是否比图像还大 [待实测]。
	///   多通道图上本族的通道行为本层未体现 [待实测]。</para>
	/// </remarks>
	public JlImage GrayTophat(JlImage SE)
	{
		IntPtr proc = JlNativeApi.PreCall(760);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, SE);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(SE);
		return obj;
	}

	/// <summary>灰度闭运算（先膨胀后腐蚀）：填掉比 SE 暗且窄的谷。</summary>
	/// <param name="SE">灰度结构元图像。</param>
	/// <returns>闭运算后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 761，在灰度上先 <see cref="GrayDilation(JlImage)"/> 后 <see cref="GrayErosion(JlImage)"/>，
	///   得到原图的上包络：窄而深的暗谷（文字笔画断口、表面麻点、暗划痕）被抬到邻近水平，宽谷不受影响。</para>
	///   <para><b>与相邻算子的取舍</b>开运算治亮毛刺、闭运算治暗毛刺，两者方向相反，不可互相顶替；
	///   要把暗谷<b>提取</b>出来而不是抹平，用 <see cref="GrayBothat(JlImage)"/>。
	///   只要按矩形窗做闭运算，用 <see cref="GrayClosingRect(int,int)"/>，不需要造 SE 图。</para>
	///   <para><b>坑</b>闭运算会抬高灰度：对被处理区域做灰度测量（平均灰度、<c>Intensity</c>）的结果会系统性偏亮，
	///   把它接在灰度统计前必须说明；SE 灰度峰值 <c>smax</c> 给多大，最多就把谷抬多深，超过部分不会继续填。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage se = new JlImage();
	///   se.GenDiscSe("byte", 5, 5, 20.0);
	///   using JlImage filled = img.GrayClosing(se);         // 暗麻点被抬平
	///   using JlRegion reg = filled.Threshold(0.0, 80.0);
	///   </code>
	///   <para><b>资源与坑</b>SE 只读；输出为新句柄。</para>
	/// </remarks>
	public JlImage GrayClosing(JlImage SE)
	{
		IntPtr proc = JlNativeApi.PreCall(761);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, SE);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(SE);
		return obj;
	}

	/// <summary>灰度开运算（先腐蚀后膨胀）：削掉比 SE 亮且窄的峰，保留整体灰度趋势。</summary>
	/// <param name="SE">灰度结构元图像。</param>
	/// <returns>开运算后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 762，在灰度上先 <see cref="GrayErosion(JlImage)"/> 后 <see cref="GrayDilation(JlImage)"/>。
	///   结果是原图的下包络：高于局部背景、且宽度或高度不足以放下 SE 的亮峰被削平，其余像素值基本不动。</para>
	///   <para><b>与 <c>GrayTophat</c> 的分界</b>要"去掉亮毛刺、继续做灰度统计"→ 本算子；
	///   要"把亮毛刺单独拿出来做检测"→ <see cref="GrayTophat(JlImage)"/>。
	///   两者常被混用：顶帽的输出量纲已经是残差（背景≈0），拿它当"滤波后的图"再 <c>Threshold(128,255)</c> 会什么都分不出来。</para>
	///   <para><b>SE 取法与副作用</b>SE 比目标宽才会削到目标，因此本算子不能用于"目标可能很小"的场合；
	///   它会<b>不可逆地改变</b>被削区域的灰度，后面若还要原始灰度测量，先在这一步之前存图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage se = new JlImage();
	///   se.GenDiscSe("byte", 5, 5, 10.0);
	///   using JlImage cleaned = img.GrayOpening(se);        // 削平亮毛刺，保留低频
	///   using JlRegion reg = cleaned.Threshold(100.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>SE 为图像输入、只读；输出类型与输入类型的关系本层未体现 [待实测]。</para>
	/// </remarks>
	public JlImage GrayOpening(JlImage SE)
	{
		IntPtr proc = JlNativeApi.PreCall(762);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, SE);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(SE);
		return obj;
	}

	/// <summary>灰度膨胀：邻域内按 SE 取值后的逐点最大，亮区扩张且整体变亮。</summary>
	/// <param name="SE">灰度结构元图像。</param>
	/// <returns>膨胀后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 763。与二值膨胀的区别在于 SE 带灰度：结果是把邻域灰度按 SE 偏移后取最大，
	///   所以亮结构变大、暗结构被吃掉，并且<b>整幅图的灰度只会升不会降</b>。</para>
	///   <para><b>坑：饱和与量纲</b>在 <c>byte</c> 图上，接近 255 的区域再膨胀会被压在上限附近 [待实测：截断还是回绕]，
	///   后续做 <c>Threshold</c> 或 <c>GrayHisto</c> 时直方图会在高端堆出一个尖峰；需要严格可加的量（高度图、灰度测量）
	///   应改用 <c>float</c> 类型（<c>ConvertImageType("float")</c>）再膨胀。</para>
	///   <para><b>与相邻算子的取舍</b>只是想让亮区变宽、形状变圆，用 <see cref="GrayDilationRect(int,int)"/> 免去造 SE；
	///   想把膨胀参与"上包络/闭运算"链条，直接用 <see cref="GrayClosing(JlImage)"/> 而不是自己串两步，
	///   两步之间会多一次中间图的分配。做减背景请配 <see cref="GrayErosion(JlImage)"/>，两者不成对使用时结果会整体偏移。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage se = new JlImage();
	///   se.GenDiscSe("byte", 5, 5, 0.0);                    // 平顶 SE：只扩形状不改灰度偏移
	///   using JlImage grown = img.GrayDilation(se);
	///   </code>
	///   <para><b>资源与坑</b>SE 只读、输出新句柄；图像边缘处的取值方式本层未体现 [待实测]。</para>
	/// </remarks>
	public JlImage GrayDilation(JlImage SE)
	{
		IntPtr proc = JlNativeApi.PreCall(763);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, SE);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(SE);
		return obj;
	}

	/// <summary>灰度腐蚀：邻域内按 SE 取值后的逐点最小，暗区扩张且整体变暗。</summary>
	/// <param name="SE">灰度结构元图像。</param>
	/// <returns>腐蚀后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 764，与 <see cref="GrayDilation(JlImage)"/> 互为对偶（取最小而非最大）：
	///   亮结构变窄、暗背景被抬高，<b>整幅图的灰度只会降不会升</b>。用它可以估出局部背景的下界，
	///   再配合 <c>SubImage</c> 得到扣除背景后的图。</para>
	///   <para><b>坑</b>窄亮目标在腐蚀后可能整体掉到接近背景值，"腐蚀后再阈值"会稳定漏检小目标；
	///   <c>byte</c> 图低端同理存在截断风险 [待实测]。与膨胀一样，边缘像素处理本层不体现 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>固定矩形窗用 <see cref="GrayErosionRect(int,int)"/>；
	///   目标是"先腐蚀后膨胀"的滤波就写 <see cref="GrayOpening(JlImage)"/>，别手拼两步。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage se = new JlImage();
	///   se.GenDiscSe("byte", 9, 9, 0.0);
	///   using JlImage background = img.GrayErosion(se);          // 背景（暗）估计
	///   using JlImage residual = img.SubImage(background, 1.0, 0.0);
	///   </code>
	///   <para><b>资源与坑</b>SE 只读；输出新句柄。</para>
	/// </remarks>
	public JlImage GrayErosion(JlImage SE)
	{
		IntPtr proc = JlNativeApi.PreCall(764);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, SE);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(SE);
		return obj;
	}

	/// <summary>从文件读入灰度形态学结构元，原地改写当前句柄。</summary>
	/// <param name="fileName">存放结构元的文件名。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 765。方法体先 Dispose 再 Load：当前 JlImage 句柄被改写为读入的结构元图（SE 就是带灰度的小图），不返回新对象。</para>
	///   <para><b>与相邻算子的取舍</b>SE 形状能用现成生成器造出来时优先 <see cref="GenDiscSe(string,int,int,double)"/>（圆盘/椭球帽），本方法专用于把外场标定的自定义 SE 落盘复用；与二值区域形态学的 SE 不通用——这里读进来的 SE 带灰度值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage se = new JlImage();
	///   se.ReadGraySe("E:/se/cap8.dat");
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlImage hat = img.GrayTophat(se);
	///   </code>
	///   <para><b>资源与坑</b>文件不存在或格式不符时原生报错、当前句柄已被 Dispose 处于未初始化态，需要重新 Gen；SE 用于 Gray* 族前请确认其 smax 峰值与检测目标灰度差匹配。</para>
	/// </remarks>
	public void ReadGraySe(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(765);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>生成帽状（椭球/圆盘）灰度结构元，原地改写当前句柄（smax 元组版）。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">结构元宽度。Default: 5</param>
	/// <param name="height">结构元高度。Default: 5</param>
	/// <param name="smax">结构元中心最大灰度值（元组，调用期间被钉住）。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 766。SE 为从中心 smax 向边缘衰减到 0 的帽状灰度图，供 <see cref="GrayTophat(JlImage)"/>/<see cref="GrayClosing(JlImage)"/> 等灰度形态学使用；方法体先 Dispose 再 Load，原地改写当前句柄。</para>
	///   <para><b>与相邻算子的取舍</b>与 double 重载同 id：元组版走 Store+UnpinTuple（钉住元组），仅当需要以元组形式批量传递 smax 或与其它元组逻辑复用时有意义，常规单值请直接传 double。smax=0 得到平顶 SE（只改形状不改灰度基准），smax&gt;0 才形成"帽高"，决定多深的谷会被填、多高的峰够格被顶帽提取。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage se = new JlImage();
	///   JlTuple smax = 30.0;
	///   se.GenDiscSe("byte", 9, 9, smax);
	///   </code>
	///   <para><b>资源与坑</b>示例传入字面量经隐式转换落到元组重载/标量重载均可；宽高应给奇数保证中心像素存在 [待实测]。</para>
	/// </remarks>
	public void GenDiscSe(string type, int width, int height, JlTuple smax)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(766);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.Store(proc, 3, smax);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(smax);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>生成帽状（椭球/圆盘）灰度结构元，原地改写当前句柄（smax 标量版）。</summary>
	/// <param name="type">像素类型。Default: "byte"</param>
	/// <param name="width">结构元宽度。Default: 5</param>
	/// <param name="height">结构元高度。Default: 5</param>
	/// <param name="smax">结构元中心最大灰度值。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 766 的标量版：StoreD 直写 smax，无元组钉住/解固定开销；生成帽状灰度 SE（中心 smax、边缘 0），供 <see cref="GrayOpening(JlImage)"/>/<see cref="GrayBothat(JlImage)"/> 等灰度形态学使用。方法体先 Dispose 再 Load，<b>原地改写</b>当前句柄。</para>
	///   <para><b>与相邻算子的取舍</b>SE 需从文件复用时改用 <see cref="ReadGraySe(string)"/>；矩形平顶 SE 可直接用 GrayOpeningRect 族而不必造 SE 图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage se = new JlImage();
	///   se.GenDiscSe("byte", 7, 7, 40.0);
	///   </code>
	///   <para><b>资源与坑</b>smax=0 是平顶 SE：灰度形态学退化为"只看形状不看高度"；帽高大于待测峰高时顶帽会把目标整体当背景吃掉（详见 GrayTophat 的 SE 取法）。</para>
	/// </remarks>
	public void GenDiscSe(string type, int width, int height, double smax)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(766);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		JlNativeApi.StoreD(proc, 3, smax);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>沿卡尺轮廓找出灰度等于给定阈值的点（亚像素，元组输出）。</summary>
	/// <param name="measureHandle">已生成的 JlMeasure 卡尺句柄（矩形/圆弧均可）。</param>
	/// <param name="sigma">提取前对轮廓做高斯平滑的 sigma。Default: 1.0</param>
	/// <param name="threshold">要提取的灰度值。Default: 128.0</param>
	/// <param name="select">交点选取方式（全部/首/末）。Default: "all"</param>
	/// <param name="rowThresh">交点行坐标（DOUBLE 元组）。</param>
	/// <param name="columnThresh">交点列坐标（DOUBLE 元组）。</param>
	/// <param name="distance">各交点沿轮廓距起点的距离（DOUBLE 元组）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 803。在本图像上按 measureHandle 定义的轮廓采样，再取灰度恰等于 threshold 的亚像素交点；三条输出一一对应、均按 DOUBLE 装载（LoadNew）。</para>
	///   <para><b>约束或前提</b>图像与 measure 生成时传入的 width/height 必须一致，否则卡尺越出图像；threshold 对 byte 图取值 0..255 且必须落在轮廓实际灰度范围内才有交点——轮廓从未到过该灰度时输出为空元组而非报错 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要的是"边缘"（灰度跳变）用 <see cref="MeasurePos"/>/<see cref="MeasurePairs"/>（基于导数与幅值），本算子提取的是"等灰度线交点"（如液面高度、印刷灰度线），两者不可互替；sigma 越大交点越稳但会钝化、位置系统偏移 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   img.MeasureThresh(caliper, 1.0, 128.0, "all",
	///       out JlTuple rowThresh, out JlTuple columnThresh, out JlTuple distance);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>JlMeasure 经 JlObjectBase 实现 IDisposable（另有 CloseMeasure），用完必须关闭；measureHandle 在原生调用结束前不得释放（实现末尾 GC.KeepAlive 佐证）。本层不缓存采样结果，换图重调即可复用同一卡尺。</para>
	/// </remarks>
	public void MeasureThresh(JlMeasure measureHandle, double sigma, double threshold, string select, out JlTuple rowThresh, out JlTuple columnThresh, out JlTuple distance)
	{
		IntPtr proc = JlNativeApi.PreCall(803);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.StoreS(proc, 3, select);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowThresh);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnThresh);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out distance);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
	}

	/// <summary>沿卡尺提取垂直于矩形/圆弧方向的原始灰度剖面（一条 DOUBLE 元组）。</summary>
	/// <param name="measureHandle">已生成的 JlMeasure 卡尺句柄。</param>
	/// <returns>灰度剖面元组，按 DOUBLE 装载；是新元组，不需释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 805。不做任何边缘评估，只把卡尺内逐条扫描线的灰度值原样铺出来，供自写峰值/过零检测使用。</para>
	///   <para><b>约束或前提</b>剖面元素的排列顺序（先扫描线后采样点，或反之）本层未体现 [待实测]；卡尺生成时的 interpolation 决定采样点是整数还是插值灰度。图像尺寸须与卡尺登记的 width/height 一致。</para>
	///   <para><b>与相邻算子的取舍</b>要现成的边缘坐标/配对宽度，直接用 <see cref="MeasurePos"/> 或 <see cref="MeasurePairs"/>，不要拿本算子的剖面手搓；本算子适合教学调试卡尺参数或对剖面做自定义滤波后再判读。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   JlTuple profile = img.MeasureProjection(caliper);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>剖面平滑建议先对图做 <c>SmoothImage</c> 而不是事后对元组差分平均——后者会破坏采样点与坐标的对应关系。</para>
	/// </remarks>
	public JlTuple MeasureProjection(JlMeasure measureHandle)
	{
		IntPtr proc = JlNativeApi.PreCall(805);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
		return tuple;
	}

	/// <summary>沿卡尺做带模糊评分的边缘配对，限制配对数量与配对方式（元组输出）。</summary>
	/// <param name="measureHandle">JlMeasure 卡尺句柄。</param>
	/// <param name="sigma">轮廓高斯平滑 sigma。Default: 1.0</param>
	/// <param name="ampThresh">最小边缘幅值。Default: 30.0</param>
	/// <param name="fuzzyThresh">最小模糊隶属度。Default: 0.5</param>
	/// <param name="transition">边缘对首边的灰度跳变方向。Default: "all"</param>
	/// <param name="pairing">配对约束方式。Default: "no_restriction"</param>
	/// <param name="numPairs">最多输出的边缘对个数。Default: 10</param>
	/// <param name="rowEdgeFirst">第一边缘行坐标（DOUBLE 元组）。</param>
	/// <param name="columnEdgeFirst">第一边缘列坐标。</param>
	/// <param name="amplitudeFirst">第一边缘幅值（带符号）。</param>
	/// <param name="rowEdgeSecond">第二边缘行坐标。</param>
	/// <param name="columnEdgeSecond">第二边缘列坐标。</param>
	/// <param name="amplitudeSecond">第二边缘幅值（带符号）。</param>
	/// <param name="rowPairCenter">边缘对中点行坐标。</param>
	/// <param name="columnPairCenter">边缘对中点列坐标。</param>
	/// <param name="fuzzyScore">该边缘对的模糊隶属度。</param>
	/// <param name="intraDistance">对内两边缘间距。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 809。与 <see cref="FuzzyMeasurePairs"/> 同族但多出 pairing/numPairs 控制：pairing 决定相邻边缘如何成对、numPairs 限制输出对数（取值集合本层未体现 [待实测]）；本方法输出没有 interDistance。</para>
	///   <para><b>约束或前提</b>10 条输出全部 DOUBLE 装载、逐条一一对应；ampThresh 是对比度门槛，byte 图上给 30 表示灰度跳变至少 30 级；fuzzyScore 越大配对越可信，用它可以按置信度二次筛。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   img.FuzzyMeasurePairing(caliper, 1.0, 30.0, 0.5, "all", "no_restriction", 10,
	///       out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst,
	///       out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond,
	///       out JlTuple rowPairCenter, out JlTuple columnPairCenter,
	///       out JlTuple fuzzyScore, out JlTuple intraDistance);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>numPairs 截断的是输出条数，边缘对按轮廓推进顺序排列；下游若按"第 k 对对应第 k 条扫描线"理解会静默错位，需要坐标回查时用 rowPairCenter/columnPairCenter。</para>
	/// </remarks>
	public void FuzzyMeasurePairing(JlMeasure measureHandle, double sigma, double ampThresh, double fuzzyThresh, string transition, string pairing, int numPairs, out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst, out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond, out JlTuple rowPairCenter, out JlTuple columnPairCenter, out JlTuple fuzzyScore, out JlTuple intraDistance)
	{
		IntPtr proc = JlNativeApi.PreCall(809);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, ampThresh);
		JlNativeApi.StoreD(proc, 3, fuzzyThresh);
		JlNativeApi.StoreS(proc, 4, transition);
		JlNativeApi.StoreS(proc, 5, pairing);
		JlNativeApi.StoreI(proc, 6, numPairs);
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
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowEdgeFirst);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnEdgeFirst);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out amplitudeFirst);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out rowEdgeSecond);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out columnEdgeSecond);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out amplitudeSecond);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out rowPairCenter);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.DOUBLE, err, out columnPairCenter);
		err = JlTuple.LoadNew(proc, 8, JlTupleType.DOUBLE, err, out fuzzyScore);
		err = JlTuple.LoadNew(proc, 9, JlTupleType.DOUBLE, err, out intraDistance);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
	}

	/// <summary>沿卡尺做带模糊评分的边缘配对（元组输出，含对间距与相邻对间距）。</summary>
	/// <param name="measureHandle">JlMeasure 卡尺句柄。</param>
	/// <param name="sigma">轮廓高斯平滑 sigma。Default: 1.0</param>
	/// <param name="ampThresh">最小边缘幅值。Default: 30.0</param>
	/// <param name="fuzzyThresh">最小模糊隶属度。Default: 0.5</param>
	/// <param name="transition">边缘对首边的灰度跳变方向。Default: "all"</param>
	/// <param name="rowEdgeFirst">第一边缘点行坐标（DOUBLE 元组）。</param>
	/// <param name="columnEdgeFirst">第一边缘点列坐标。</param>
	/// <param name="amplitudeFirst">第一边缘幅值（带符号）。</param>
	/// <param name="rowEdgeSecond">第二边缘点行坐标。</param>
	/// <param name="columnEdgeSecond">第二边缘点列坐标。</param>
	/// <param name="amplitudeSecond">第二边缘幅值（带符号）。</param>
	/// <param name="rowEdgeCenter">边缘对中点行坐标。</param>
	/// <param name="columnEdgeCenter">边缘对中点列坐标。</param>
	/// <param name="fuzzyScore">边缘对的模糊隶属度。</param>
	/// <param name="intraDistance">对内两边缘间距（目标宽度）。</param>
	/// <param name="interDistance">相邻边缘对之间的间距（目标节距）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 810。测宽+测距一体：intraDistance 给"每根目标有多宽"，interDistance 给"目标之间的空隙有多宽"，适合等间距条纹/引脚节距测量。</para>
	///   <para><b>与相邻算子的取舍</b>与 <see cref="FuzzyMeasurePairing"/> 的差别是它可按对数截断、本方法不可以；与硬阈值的 <see cref="MeasurePairs"/> 相比，本方法以 fuzzyThresh 隶属度筛对，噪声场景更稳但多一条 fuzzyScore 输出需要自己设线。测量单边缘用 FuzzyMeasurePos 即可，别用配对算子凑。</para>
	///   <para><b>约束或前提</b>11 条输出全部 DOUBLE 装载、按扫描线推进顺序一一对应；transition 选 "positive" 还是 "negative" 决定只配"暗→亮"或"亮→暗"起步的对（关键词与方向的对应关系 [待实测]），选错方向会整批漏配。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   img.FuzzyMeasurePairs(caliper, 1.0, 30.0, 0.5, "all",
	///       out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst,
	///       out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond,
	///       out JlTuple rowEdgeCenter, out JlTuple columnEdgeCenter,
	///       out JlTuple fuzzyScore, out JlTuple intraDistance, out JlTuple interDistance);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原英文备注的"相关算子 GenMeasureRectangle2、CloseMeasure"仍在 JlMeasure 侧存在；卡尺用完记得关闭，句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）。</para>
	/// </remarks>
	public void FuzzyMeasurePairs(JlMeasure measureHandle, double sigma, double ampThresh, double fuzzyThresh, string transition, out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst, out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond, out JlTuple rowEdgeCenter, out JlTuple columnEdgeCenter, out JlTuple fuzzyScore, out JlTuple intraDistance, out JlTuple interDistance)
	{
		IntPtr proc = JlNativeApi.PreCall(810);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, ampThresh);
		JlNativeApi.StoreD(proc, 3, fuzzyThresh);
		JlNativeApi.StoreS(proc, 4, transition);
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
		JlNativeApi.InitOCT(proc, 10);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowEdgeFirst);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnEdgeFirst);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out amplitudeFirst);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out rowEdgeSecond);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out columnEdgeSecond);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out amplitudeSecond);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out rowEdgeCenter);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.DOUBLE, err, out columnEdgeCenter);
		err = JlTuple.LoadNew(proc, 8, JlTupleType.DOUBLE, err, out fuzzyScore);
		err = JlTuple.LoadNew(proc, 9, JlTupleType.DOUBLE, err, out intraDistance);
		err = JlTuple.LoadNew(proc, 10, JlTupleType.DOUBLE, err, out interDistance);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
	}

	/// <summary>沿卡尺提取带模糊评分的单边缘点集（不做配对）。</summary>
	/// <param name="measureHandle">JlMeasure 卡尺句柄。</param>
	/// <param name="sigma">轮廓高斯平滑 sigma。Default: 1.0</param>
	/// <param name="ampThresh">最小边缘幅值。Default: 30.0</param>
	/// <param name="fuzzyThresh">最小模糊隶属度。Default: 0.5</param>
	/// <param name="transition">保留的跳变方向（亮暗向）。Default: "all"</param>
	/// <param name="rowEdge">边缘点行坐标（DOUBLE 元组）。</param>
	/// <param name="columnEdge">边缘点列坐标。</param>
	/// <param name="amplitude">边缘幅值（带符号，符号即跳变方向）。</param>
	/// <param name="fuzzyScore">各边缘的模糊隶属度。</param>
	/// <param name="distance">相邻（连续）边缘点之间的距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 811。Fuzzy 族的"单边缘"版：每条扫描线上留下的是一串独立边缘点及其置信度，5 条输出均 DOUBLE 装载。</para>
	///   <para><b>与相邻算子的取舍</b>目标是一根线/一条轮廓边界时用本方法或 <see cref="MeasurePos"/>；目标是"宽度"才用 FuzzyMeasurePairs 族。与硬阈值 MeasurePos 相比，本方法能顺带给出 fuzzyScore 供按置信度筛点，代价是多一个参数要调（fuzzyThresh 定太低会把噪声边缘全放进来）。</para>
	///   <para><b>约束或前提</b>ampThresh 以灰度量纲计；图像类型是 uint2 时 30 的门槛含义与 byte 完全不同，先换算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   img.FuzzyMeasurePos(caliper, 1.0, 30.0, 0.5, "all",
	///       out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude,
	///       out JlTuple fuzzyScore, out JlTuple distance);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>distance 的基准（相邻点间距还是到轮廓起点距离）本层未体现 [待实测]，画回图上时一律以 rowEdge/columnEdge 为准；卡尺句柄在原生调用结束前不得释放。</para>
	/// </remarks>
	public void FuzzyMeasurePos(JlMeasure measureHandle, double sigma, double ampThresh, double fuzzyThresh, string transition, out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude, out JlTuple fuzzyScore, out JlTuple distance)
	{
		IntPtr proc = JlNativeApi.PreCall(811);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, ampThresh);
		JlNativeApi.StoreD(proc, 3, fuzzyThresh);
		JlNativeApi.StoreS(proc, 4, transition);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowEdge);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnEdge);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out amplitude);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out fuzzyScore);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out distance);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
	}

	/// <summary>沿卡尺做硬阈值边缘配对，按 transition/select 筛选（元组输出）。</summary>
	/// <param name="measureHandle">JlMeasure 卡尺句柄。</param>
	/// <param name="sigma">轮廓高斯平滑 sigma。Default: 1.0</param>
	/// <param name="threshold">最小边缘幅值。Default: 30.0</param>
	/// <param name="transition">决定如何把边缘归组成对的灰度跳变类型。Default: "all"</param>
	/// <param name="select">边缘对选取（全部/首末等）。Default: "all"</param>
	/// <param name="rowEdgeFirst">第一边缘中心行坐标（DOUBLE 元组）。</param>
	/// <param name="columnEdgeFirst">第一边缘中心列坐标。</param>
	/// <param name="amplitudeFirst">第一边缘幅值（带符号）。</param>
	/// <param name="rowEdgeSecond">第二边缘中心行坐标。</param>
	/// <param name="columnEdgeSecond">第二边缘中心列坐标。</param>
	/// <param name="amplitudeSecond">第二边缘幅值（带符号）。</param>
	/// <param name="intraDistance">对内两边缘间距（目标宽度）。</param>
	/// <param name="interDistance">相邻边缘对间距（目标节距）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 812。经典硬阈值配对：边缘以导数过零+幅值 threshold 判定，成对由 transition 规定首边方向；8 条输出均 DOUBLE 装载。</para>
	///   <para><b>与相邻算子的取舍</b>没有置信度可用、也不接受模糊隶属度参数——需要按 fuzzyScore 筛对时改 <see cref="FuzzyMeasurePairs"/>；只需要单条边缘宽度序列时 <see cref="MeasurePos"/> 更轻。select 用 "first"/"last" 时输出条数骤减，务必确认卡尺起点方向与工艺约定一致，否则拿到的是"另一端"的边。</para>
	///   <para><b>约束或前提</b>threshold 与图像灰度量纲一致；轮廓未出现指定 transition 方向的对时输出空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   img.MeasurePairs(caliper, 1.0, 30.0, "all", "all",
	///       out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst,
	///       out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond,
	///       out JlTuple intraDistance, out JlTuple interDistance);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本方法不输出对中点坐标（Fuzzy 族有），要"目标中心"需自行取 first/second 均值；卡尺句柄在调用结束前不得释放。</para>
	/// </remarks>
	public void MeasurePairs(JlMeasure measureHandle, double sigma, double threshold, string transition, string select, out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst, out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond, out JlTuple intraDistance, out JlTuple interDistance)
	{
		IntPtr proc = JlNativeApi.PreCall(812);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.StoreS(proc, 3, transition);
		JlNativeApi.StoreS(proc, 4, select);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowEdgeFirst);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnEdgeFirst);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out amplitudeFirst);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out rowEdgeSecond);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out columnEdgeSecond);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out amplitudeSecond);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out intraDistance);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.DOUBLE, err, out interDistance);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
	}

	/// <summary>沿卡尺提取硬阈值单边缘点集（最常用的卡尺测边算子）。</summary>
	/// <param name="measureHandle">JlMeasure 卡尺句柄。</param>
	/// <param name="sigma">轮廓高斯平滑 sigma。Default: 1.0</param>
	/// <param name="threshold">最小边缘幅值。Default: 30.0</param>
	/// <param name="transition">保留亮→暗或暗→亮方向的边缘。Default: "all"</param>
	/// <param name="select">端点选取（全部/首/末）。Default: "all"</param>
	/// <param name="rowEdge">边缘中心行坐标（DOUBLE 元组）。</param>
	/// <param name="columnEdge">边缘中心列坐标。</param>
	/// <param name="amplitude">边缘幅值（带符号，符号表示跳变方向）。</param>
	/// <param name="distance">连续边缘之间的距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 813。对每条扫描线做一维高斯导数卷积，幅值过 threshold 者成为亚像素边缘点；4 条输出均 DOUBLE 装载，按扫描线推进顺序一一对应。</para>
	///   <para><b>与相邻算子的取舍</b>需要置信度筛选用 <see cref="FuzzyMeasurePos"/>；要宽度用 <see cref="MeasurePairs"/>；要"灰度恰好等于某值"的点用 <see cref="MeasureThresh"/>。本方法无评分输出，噪声图上调 threshold 往往不够，还要配合 sigma（sigma 加大会钝化并平移边缘 [待实测]）。</para>
	///   <para><b>约束或前提</b>threshold 与灰度量纲一致；select="first"/"last" 的"首末"以卡尺局部坐标方向为准，卡尺角度摆反会选中工件另一侧边缘且无任何报错。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   img.MeasurePos(caliper, 1.0, 30.0, "all", "all",
	///       out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude, out JlTuple distance);
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>多张扫描线共用一次调用：结果把各线边缘串接在一起，不给出"属于哪条线"的索引，需按每条线最大边缘数自行切分或改用逐线测量 [待实测]；卡尺句柄在调用结束前不得释放。</para>
	/// </remarks>
	public void MeasurePos(JlMeasure measureHandle, double sigma, double threshold, string transition, string select, out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude, out JlTuple distance)
	{
		IntPtr proc = JlNativeApi.PreCall(813);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.StoreS(proc, 3, transition);
		JlNativeApi.StoreS(proc, 4, select);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowEdge);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnEdge);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out amplitude);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out distance);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(measureHandle);
	}

	/// <summary>在本模板图上试算形状模型的自动化参数（元组版，可传 "auto"）。</summary>
	/// <param name="numLevels">金字塔层数或 "auto"。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="scaleMin">最小缩放或 "auto"。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放或 "auto"。Default: 1.1</param>
	/// <param name="optimization">优化方式。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值（或滞后双阈值+最小尺寸）。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度。Default: "auto"</param>
	/// <param name="parameters">要自动确定的参数名集合。Default: "all"</param>
	/// <param name="parameterValue">与返回的参数名一一对应的建议值（新元组）。</param>
	/// <returns>被自动确定的参数名元组，如 "num_levels"、"contrast"。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 880。它<b>不创建模型</b>，只回答"若按这些取值建 CreateShapeModel 会自动定出哪些参数、定成多少"，用于把确定后的值原样喂给 CreateShapeModel 复现建模型过程。九个元组形参全程钉住、调用后逐个 UnpinTuple。</para>
	///   <para><b>约束或前提</b>当前对象必须是已裁好域的模板图：建议先 ReduceDomain 把目标圈进域内再调用，否则 contrast 的 "auto" 会按整图噪声估计 [待实测]。角度单位为弧度且 angleExtent 覆盖 angleStart 之后的区间。</para>
	///   <para><b>与相邻算子的取舍</b>与 int 重载同 id：int 重载（numLevels/contrast/minContrast 为 int）不能表达 "auto"，适合把本重载算出的结果回填；首次摸索参数用本元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage template = new JlImage("byte", 64, 64);
	///   JlTuple names = template.DetermineShapeModelParams("auto", -0.39, 0.79, "auto", "auto",
	///       "auto", "use_polarity", "auto", "auto", "all", out JlTuple values);
	///   template.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>names 与 values 等长按序对应；把它们逐项传给 CreateShapeModel 前注意 int/double/JlTuple 重载选择，传错重载会把字符串 "auto" 再交给原生（等价于不采用建议值）。</para>
	/// </remarks>
	public JlTuple DetermineShapeModelParams(JlTuple numLevels, double angleStart, double angleExtent, JlTuple scaleMin, JlTuple scaleMax, string optimization, string metric, JlTuple contrast, JlTuple minContrast, JlTuple parameters, out JlTuple parameterValue)
	{
		IntPtr proc = JlNativeApi.PreCall(880);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, scaleMin);
		JlNativeApi.Store(proc, 4, scaleMax);
		JlNativeApi.StoreS(proc, 5, optimization);
		JlNativeApi.StoreS(proc, 6, metric);
		JlNativeApi.Store(proc, 7, contrast);
		JlNativeApi.Store(proc, 8, minContrast);
		JlNativeApi.Store(proc, 9, parameters);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(scaleMin);
		JlNativeApi.UnpinTuple(scaleMax);
		JlNativeApi.UnpinTuple(contrast);
		JlNativeApi.UnpinTuple(minContrast);
		JlNativeApi.UnpinTuple(parameters);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, err, out parameterValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>在本模板图上试算形状模型参数（标量版，numLevels/contrast 必须是数值）。</summary>
	/// <param name="numLevels">金字塔层数（无法传 "auto"）。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="scaleMin">最小缩放。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="optimization">优化方式。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值（整数，无法传 "auto"）。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度（整数，无法传 "auto"）。Default: "auto"</param>
	/// <param name="parameters">要自动确定的参数名集合。Default: "all"</param>
	/// <param name="parameterValue">与返回参数名一一对应的建议值（新元组）。</param>
	/// <returns>被自动确定的参数名元组。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 880 的标量版：numLevels/contrast/minContrast 走 StoreI、scaleMin/scaleMax 走 StoreD，无元组钉住开销。</para>
	///   <para><b>与相邻算子的取舍</b>本重载把 "auto" 类语义换成了强制给数值：适合在元组版跑完、拿到建议值后做<b>复核性重算</b>或纯数值实验；第一次建模型请仍用元组版 <see cref="DetermineShapeModelParams(JlTuple,double,double,JlTuple,JlTuple,string,string,JlTuple,JlTuple,JlTuple,out JlTuple)"/>。</para>
	///   <para><b>约束或前提</b>contrast 给单整数即单阈值；滞后双阈值只能走元组版。角度弧度制。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   JlTuple names = tmpl.DetermineShapeModelParams(4, -0.39, 0.79, 0.9, 1.1,
	///       "auto", "use_polarity", 40, 15, "all", out JlTuple values);
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>注意与元组版的重载选择：任何实参写成字符串字面量都会跳回元组重载（隐式转换），需要数值语义时写 int/double 字面量。</para>
	/// </remarks>
	public JlTuple DetermineShapeModelParams(int numLevels, double angleStart, double angleExtent, double scaleMin, double scaleMax, string optimization, string metric, int contrast, int minContrast, string parameters, out JlTuple parameterValue)
	{
		IntPtr proc = JlNativeApi.PreCall(880);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleMin);
		JlNativeApi.StoreD(proc, 4, scaleMax);
		JlNativeApi.StoreS(proc, 5, optimization);
		JlNativeApi.StoreS(proc, 6, metric);
		JlNativeApi.StoreI(proc, 7, contrast);
		JlNativeApi.StoreI(proc, 8, minContrast);
		JlNativeApi.StoreS(proc, 9, parameters);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, err, out parameterValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>在图中查找多个各向异性缩放形状模型的全部/前 N 个最佳匹配（元组参数版）。</summary>
	/// <param name="modelIDs">模型句柄数组，原生侧先 ConcatArray 拼成句柄元组。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="scaleRMin">行方向最小缩放。Default: 0.9</param>
	/// <param name="scaleRMax">行方向最大缩放。Default: 1.1</param>
	/// <param name="scaleCMin">列方向最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">列方向最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">找到的实例个数，0 表示所有满足条件的匹配。Default: 1</param>
	/// <param name="maxOverlap">实例间允许的最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数（=2 时兼作最低层）。Default: 0</param>
	/// <param name="greediness">搜索启发式贪心度：0 稳而慢，1 快但可能漏检。Default: 0.9</param>
	/// <param name="row">找到实例的形心行坐标（DOUBLE）。</param>
	/// <param name="column">形心列坐标（DOUBLE）。</param>
	/// <param name="angle">实例旋转角（弧度，DOUBLE）。</param>
	/// <param name="scaleR">实例行方向缩放。</param>
	/// <param name="scaleC">实例列方向缩放。</param>
	/// <param name="score">实例匹配分。</param>
	/// <param name="model">实例来自第几个输入模型（INTEGER 索引）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 884。行、列缩放独立（anisotropic），适合工件在两个方向上有不同形变的场景；7 条输出按匹配实例对齐，前 6 条 DOUBLE、model 条 INTEGER 装载。</para>
	///   <para><b>与相邻算子的取舍</b>各向同性缩放用 <see cref="FindScaledShapeModels(JlShapeModel[],JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>（更省），无缩放需求用 FindShapeModels；标量参数版（单模型+double 形参）适合固定参数的高频调用，本数组元组版适合"一批模型一次查"。model 索引指明每条结果属于 modelIDs 数组中哪个模型。</para>
	///   <para><b>约束或前提</b>angleStart/angleExtent 弧度制；numMatches=0 时 maxOverlap 才参与去重（否则按分数取前 N）[待实测]；图像与模型坐标系一致。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindAnisoShapeModels(new JlShapeModel[] { model }, -0.39, 0.79, 0.9, 1.1, 0.9, 1.1,
	///       0.5, 0, 0.5, "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scaleR, out JlTuple scaleC, out JlTuple score, out JlTuple modelIdx);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄数组在原生调用结束前不得释放（GC.KeepAlive(modelIDs) 佐证）；实例顺序按分数降序还是按模型分组排列本层未体现 [待实测]，跨帧追踪不要依赖输出次序。</para>
	/// </remarks>
	public void FindAnisoShapeModels(JlShapeModel[] modelIDs, JlTuple angleStart, JlTuple angleExtent, JlTuple scaleRMin, JlTuple scaleRMax, JlTuple scaleCMin, JlTuple scaleCMax, JlTuple minScore, JlTuple numMatches, JlTuple maxOverlap, JlTuple subPixel, JlTuple numLevels, JlTuple greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score, out JlTuple model)
	{
		JlTuple hTuple = JlHandleBase.ConcatArray(modelIDs);
		IntPtr proc = JlNativeApi.PreCall(884);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, angleStart);
		JlNativeApi.Store(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, scaleRMin);
		JlNativeApi.Store(proc, 4, scaleRMax);
		JlNativeApi.Store(proc, 5, scaleCMin);
		JlNativeApi.Store(proc, 6, scaleCMax);
		JlNativeApi.Store(proc, 7, minScore);
		JlNativeApi.Store(proc, 8, numMatches);
		JlNativeApi.Store(proc, 9, maxOverlap);
		JlNativeApi.Store(proc, 10, subPixel);
		JlNativeApi.Store(proc, 11, numLevels);
		JlNativeApi.Store(proc, 12, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(angleStart);
		JlNativeApi.UnpinTuple(angleExtent);
		JlNativeApi.UnpinTuple(scaleRMin);
		JlNativeApi.UnpinTuple(scaleRMax);
		JlNativeApi.UnpinTuple(scaleCMin);
		JlNativeApi.UnpinTuple(scaleCMax);
		JlNativeApi.UnpinTuple(minScore);
		JlNativeApi.UnpinTuple(numMatches);
		JlNativeApi.UnpinTuple(maxOverlap);
		JlNativeApi.UnpinTuple(subPixel);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(greediness);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scaleR);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out scaleC);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>在图中查找单个各向异性缩放形状模型的最佳匹配（标量参数版）。</summary>
	/// <param name="modelIDs">单个模型句柄（多模型请用数组元组重载）。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="scaleRMin">行方向最小缩放。Default: 0.9</param>
	/// <param name="scaleRMax">行方向最大缩放。Default: 1.1</param>
	/// <param name="scaleCMin">列方向最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">列方向最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度：0 稳而慢，1 快但漏检。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scaleR">行方向缩放。</param>
	/// <param name="scaleC">列方向缩放。</param>
	/// <param name="score">匹配分。</param>
	/// <param name="model">来源模型索引（INTEGER；单模型时恒指同一模型）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 884 的标量版：角度/缩放/分数阈值走 StoreD、numMatches/numLevels 走 StoreI、subPixel 走 StoreS，句柄直接 Store——没有元组钉住与 ConcatArray 组装开销，单模型高频产线调用首选。</para>
	///   <para><b>与相邻算子的取舍</b>要一次查多个模型改数组元组重载 <see cref="FindAnisoShapeModels(JlShapeModel[],JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>；不需要缩放差改用 FindScaledShapeModels/FindShapeModels 以省 pyramid 计算。</para>
	///   <para><b>约束或前提</b>角度弧度制；numMatches=0 时 maxOverlap 才参与去重 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindAnisoShapeModels(model, -0.79, 1.57, 0.9, 1.1, 0.9, 1.1, 0.6, 1, 0.5,
	///       "least_squares", 0, 0.8,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scaleR, out JlTuple scaleC, out JlTuple score, out JlTuple modelIdx);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证），可在本方法返回后再 Dispose；单模型时 model 输出仍需读取——用它区分"未找到"（空元组）与找到。</para>
	/// </remarks>
	public void FindAnisoShapeModels(JlShapeModel modelIDs, double angleStart, double angleExtent, double scaleRMin, double scaleRMax, double scaleCMin, double scaleCMax, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score, out JlTuple model)
	{
		IntPtr proc = JlNativeApi.PreCall(884);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelIDs);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleRMin);
		JlNativeApi.StoreD(proc, 4, scaleRMax);
		JlNativeApi.StoreD(proc, 5, scaleCMin);
		JlNativeApi.StoreD(proc, 6, scaleCMax);
		JlNativeApi.StoreD(proc, 7, minScore);
		JlNativeApi.StoreI(proc, 8, numMatches);
		JlNativeApi.StoreD(proc, 9, maxOverlap);
		JlNativeApi.StoreS(proc, 10, subPixel);
		JlNativeApi.StoreI(proc, 11, numLevels);
		JlNativeApi.StoreD(proc, 12, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scaleR);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out scaleC);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>在图中查找多个各向同性缩放形状模型的最佳匹配（数组+元组参数版）。</summary>
	/// <param name="modelIDs">模型句柄数组，原生侧 ConcatArray 拼为句柄元组。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.78</param>
	/// <param name="scaleMin">最小缩放（行列同步）。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scale">统一缩放系数（行=列）。</param>
	/// <param name="score">匹配分。</param>
	/// <param name="model">来源模型在数组中的索引（INTEGER）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 885。与 884（Aniso）同为多模型查找，但只有一个 scale 维度：6 条 DOUBLE/INTEGER 混装输出（前 5 条 DOUBLE、model 条 INTEGER）。本库文档默认 angleExtent 与 Aniso 版不同（0.78 vs 0.79），照抄默认值时注意区分。</para>
	///   <para><b>与相邻算子的取舍</b>工件存在透视/非均匀缩放（如倾斜放置的标签）时 scale 单一维度表达不了，须用 <see cref="FindAnisoShapeModels(JlShapeModel[],JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>；完全无缩放则 FindShapeModels 更快。</para>
	///   <para><b>约束或前提</b>角度弧度制；numMatches=0 时 maxOverlap 参与去重 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindScaledShapeModels(new JlShapeModel[] { model }, -0.39, 0.78, 0.9, 1.1, 0.5, 0, 0.5,
	///       "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scale, out JlTuple score, out JlTuple modelIdx);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>数组内任一模型句柄已 Dispose 会连带本次调用失败；句柄数组在原生调用结束前不得释放（GC.KeepAlive 佐证）。</para>
	/// </remarks>
	public void FindScaledShapeModels(JlShapeModel[] modelIDs, JlTuple angleStart, JlTuple angleExtent, JlTuple scaleMin, JlTuple scaleMax, JlTuple minScore, JlTuple numMatches, JlTuple maxOverlap, JlTuple subPixel, JlTuple numLevels, JlTuple greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score, out JlTuple model)
	{
		JlTuple hTuple = JlHandleBase.ConcatArray(modelIDs);
		IntPtr proc = JlNativeApi.PreCall(885);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, angleStart);
		JlNativeApi.Store(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, scaleMin);
		JlNativeApi.Store(proc, 4, scaleMax);
		JlNativeApi.Store(proc, 5, minScore);
		JlNativeApi.Store(proc, 6, numMatches);
		JlNativeApi.Store(proc, 7, maxOverlap);
		JlNativeApi.Store(proc, 8, subPixel);
		JlNativeApi.Store(proc, 9, numLevels);
		JlNativeApi.Store(proc, 10, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(angleStart);
		JlNativeApi.UnpinTuple(angleExtent);
		JlNativeApi.UnpinTuple(scaleMin);
		JlNativeApi.UnpinTuple(scaleMax);
		JlNativeApi.UnpinTuple(minScore);
		JlNativeApi.UnpinTuple(numMatches);
		JlNativeApi.UnpinTuple(maxOverlap);
		JlNativeApi.UnpinTuple(subPixel);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(greediness);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scale);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>在图中查找单个各向同性缩放形状模型的最佳匹配（标量参数版）。</summary>
	/// <param name="modelIDs">单个模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.78</param>
	/// <param name="scaleMin">最小缩放。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scale">统一缩放系数。</param>
	/// <param name="score">匹配分。</param>
	/// <param name="model">来源模型索引（INTEGER，单模型时用于判定是否命中）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 885 的标量版：StoreD/StoreI/StoreS 直写、无 ConcatArray 与钉固定开销；前 5 条输出 DOUBLE、model 条 INTEGER 装载。</para>
	///   <para><b>与相邻算子的取舍</b>单模型且不需要 scale 输出时用最普通的 FindShapeModels（少一维搜索更省时）；行列形变不等用 FindAnisoShapeModels 族；本方法专属"等比缩放+旋转"的找料场景。</para>
	///   <para><b>约束或前提</b>角度弧度制；未命中时全部输出为空元组；numMatches=0 时 maxOverlap 参与去重 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindScaledShapeModels(model, -0.39, 0.78, 0.9, 1.1, 0.5, 1, 0.5,
	///       "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scale, out JlTuple score, out JlTuple modelIdx);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在本方法返回前不得释放（GC.KeepAlive 佐证）；scale 输出可反推工件距离变化，做定标时先固定 numMatches=1。</para>
	/// </remarks>
	public void FindScaledShapeModels(JlShapeModel modelIDs, double angleStart, double angleExtent, double scaleMin, double scaleMax, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score, out JlTuple model)
	{
		IntPtr proc = JlNativeApi.PreCall(885);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelIDs);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleMin);
		JlNativeApi.StoreD(proc, 4, scaleMax);
		JlNativeApi.StoreD(proc, 5, minScore);
		JlNativeApi.StoreI(proc, 6, numMatches);
		JlNativeApi.StoreD(proc, 7, maxOverlap);
		JlNativeApi.StoreS(proc, 8, subPixel);
		JlNativeApi.StoreI(proc, 9, numLevels);
		JlNativeApi.StoreD(proc, 10, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scale);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>在图中查找多个形状模型（仅旋转、不缩放）的最佳匹配（数组+元组参数版）。</summary>
	/// <param name="modelIDs">模型句柄数组，原生侧 ConcatArray 拼为句柄元组。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度：0 稳而慢，1 快但漏检。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="score">匹配分。</param>
	/// <param name="model">来源模型在数组中的索引（INTEGER）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 886。形状匹配族里搜索维度最少、速度最快的多模型版：4 条 DOUBLE + 1 条 INTEGER 输出，无 scale 维度。</para>
	///   <para><b>与相邻算子的取舍</b>工件有缩放（输送距离波动、镜头变焦）时本方法会因分数不达标而漏检，须换 FindScaledShapeModels/FindAnisoShapeModels 族；灰度渐变、无稳定边缘的工件形状匹配本就不合适，改用 NCC 族（<see cref="CreateNccModel(JlTuple, double, double, JlTuple, string)"/>）。</para>
	///   <para><b>约束或前提</b>角度弧度制；未命中输出空元组；模型句柄保持未释放期间本方法可对同一模型反复调用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindShapeModels(new JlShapeModel[] { model }, -0.39, 0.79, 0.5, 1, 0.5,
	///       "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple score, out JlTuple modelIdx);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>数组与九条元组参数全部钉住后逐个 UnpinTuple（与标量重载的本质差异）；输出实例的排列次序不承诺稳定，跨帧对应请按 (row,column) 距离匹配而非按下标。</para>
	/// </remarks>
	public void FindShapeModels(JlShapeModel[] modelIDs, JlTuple angleStart, JlTuple angleExtent, JlTuple minScore, JlTuple numMatches, JlTuple maxOverlap, JlTuple subPixel, JlTuple numLevels, JlTuple greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		JlTuple hTuple = JlHandleBase.ConcatArray(modelIDs);
		IntPtr proc = JlNativeApi.PreCall(886);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, angleStart);
		JlNativeApi.Store(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, minScore);
		JlNativeApi.Store(proc, 4, numMatches);
		JlNativeApi.Store(proc, 5, maxOverlap);
		JlNativeApi.Store(proc, 6, subPixel);
		JlNativeApi.Store(proc, 7, numLevels);
		JlNativeApi.Store(proc, 8, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(angleStart);
		JlNativeApi.UnpinTuple(angleExtent);
		JlNativeApi.UnpinTuple(minScore);
		JlNativeApi.UnpinTuple(numMatches);
		JlNativeApi.UnpinTuple(maxOverlap);
		JlNativeApi.UnpinTuple(subPixel);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(greediness);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>在图中查找单个形状模型（仅旋转）的最佳匹配（标量参数版，最常用的定位入口）。</summary>
	/// <param name="modelIDs">单个模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式，"none" 关闭亚像素。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度：0 稳而慢，1 快但漏检。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="score">匹配分。</param>
	/// <param name="model">来源模型索引（INTEGER，单模型场景配合判空即可）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 886 的标量版：StoreD/StoreI/StoreS 直写全部参数，无钉固定开销，是"取位姿"场景的默认入口；row/column/angle 三者可直接喂给仿射变换（angle 为弧度）。</para>
	///   <para><b>与相邻算子的取舍</b>只允许一个实例时设 numMatches=1 最快；要同型号多件用数组元组重载一次查全。对亮度/灰度漂移敏感的场景不用形状匹配，改 NCC 族。</para>
	///   <para><b>约束或前提</b>angleExtent 与 angleStart 定义的是同一个连续区间，跨过 0 的摆动角区间要写成 angleStart=-a、angleExtent=2a 而不是两段拼接 [待实测]；未命中时所有输出为空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindShapeModels(model, -0.39, 0.79, 0.5, 1, 0.5,
	///       "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple score, out JlTuple modelIdx);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在本方法返回前不得释放（GC.KeepAlive 佐证），返回后即可安全 Dispose；score 上限 1，接近 1 不代表无偏移，仅表示轮廓一致度高，平移精度另看 subPixel 设置。</para>
	/// </remarks>
	public void FindShapeModels(JlShapeModel modelIDs, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		IntPtr proc = JlNativeApi.PreCall(886);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelIDs);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, minScore);
		JlNativeApi.StoreI(proc, 4, numMatches);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.StoreS(proc, 6, subPixel);
		JlNativeApi.StoreI(proc, 7, numLevels);
		JlNativeApi.StoreD(proc, 8, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>查找单个各向异性缩放形状模型的最佳匹配（混合元组参数版）。</summary>
	/// <param name="modelID">模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="scaleRMin">行方向最小缩放（标量直写）。Default: 0.9</param>
	/// <param name="scaleRMax">行方向最大缩放。Default: 1.1</param>
	/// <param name="scaleCMin">列方向最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">列方向最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分（元组，被钉住）。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式（元组）。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数（元组）。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scaleR">行方向缩放。</param>
	/// <param name="scaleC">列方向缩放。</param>
	/// <param name="score">匹配分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 887。单模型版没有 model 索引输出（6 条输出全 DOUBLE）——与 FindAnisoShapeModels 族的本质区别。本重载中 minScore/subPixel/numLevels 走 Store+UnpinTuple（钉住），缩放与角度参数走 StoreD。</para>
	///   <para><b>与相邻算子的取舍</b>纯标量参数请选 double/string 重载以免钉固定开销；本重载存在的意义是把 subPixel、numLevels 以多元素元组一次性交给原生（行为 [待实测]）。只找位姿、无需行列分别缩放时用 FindScaledShapeModel/FindShapeModel。</para>
	///   <para><b>约束或前提</b>角度弧度制；scaleR/scaleC 分别对应建模型时给定的行/列缩放区间，超出部分不会被搜到；未命中输出空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   JlTuple minScore = 0.5;
	///   JlTuple subPixel = "least_squares";
	///   JlTuple numLevels = 0;
	///   scene.FindAnisoShapeModel(model, -0.39, 0.79, 0.9, 1.1, 0.9, 1.1, minScore, 1, 0.5,
	///       subPixel, numLevels, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scaleR, out JlTuple scaleC, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）；行列两个缩放输出做几何补偿时需一起用，只取其一会把工件拉变形。</para>
	/// </remarks>
	public void FindAnisoShapeModel(JlShapeModel modelID, double angleStart, double angleExtent, double scaleRMin, double scaleRMax, double scaleCMin, double scaleCMax, JlTuple minScore, int numMatches, double maxOverlap, JlTuple subPixel, JlTuple numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(887);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleRMin);
		JlNativeApi.StoreD(proc, 4, scaleRMax);
		JlNativeApi.StoreD(proc, 5, scaleCMin);
		JlNativeApi.StoreD(proc, 6, scaleCMax);
		JlNativeApi.Store(proc, 7, minScore);
		JlNativeApi.StoreI(proc, 8, numMatches);
		JlNativeApi.StoreD(proc, 9, maxOverlap);
		JlNativeApi.Store(proc, 10, subPixel);
		JlNativeApi.Store(proc, 11, numLevels);
		JlNativeApi.StoreD(proc, 12, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minScore);
		JlNativeApi.UnpinTuple(subPixel);
		JlNativeApi.UnpinTuple(numLevels);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scaleR);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out scaleC);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>查找单个各向异性缩放形状模型的最佳匹配（全标量参数版）。</summary>
	/// <param name="modelID">模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="scaleRMin">行方向最小缩放。Default: 0.9</param>
	/// <param name="scaleRMax">行方向最大缩放。Default: 1.1</param>
	/// <param name="scaleCMin">列方向最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">列方向最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scaleR">行方向缩放。</param>
	/// <param name="scaleC">列方向缩放。</param>
	/// <param name="score">匹配分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 887 的全标量版：除模型句柄外全部 StoreD/StoreI/StoreS 直写，无钉固定开销；6 条输出全 DOUBLE、无 model 索引（单模型无需）。</para>
	///   <para><b>与相邻算子的取舍</b>与混合元组重载（minScore/subPixel/numLevels 为 JlTuple）同 id，单值场景选本重载；无行列差异的缩放用 FindScaledShapeModel 少一维搜索。</para>
	///   <para><b>约束或前提</b>角度弧度制；行列缩放区间独立生效；未命中输出空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindAnisoShapeModel(model, -0.39, 0.79, 0.9, 1.1, 0.9, 1.1, 0.5, 1, 0.5,
	///       "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scaleR, out JlTuple scaleC, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）。</para>
	/// </remarks>
	public void FindAnisoShapeModel(JlShapeModel modelID, double angleStart, double angleExtent, double scaleRMin, double scaleRMax, double scaleCMin, double scaleCMax, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(887);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleRMin);
		JlNativeApi.StoreD(proc, 4, scaleRMax);
		JlNativeApi.StoreD(proc, 5, scaleCMin);
		JlNativeApi.StoreD(proc, 6, scaleCMax);
		JlNativeApi.StoreD(proc, 7, minScore);
		JlNativeApi.StoreI(proc, 8, numMatches);
		JlNativeApi.StoreD(proc, 9, maxOverlap);
		JlNativeApi.StoreS(proc, 10, subPixel);
		JlNativeApi.StoreI(proc, 11, numLevels);
		JlNativeApi.StoreD(proc, 12, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scaleR);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out scaleC);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>查找单个等比缩放形状模型的最佳匹配（混合元组参数版）。</summary>
	/// <param name="modelID">模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.78</param>
	/// <param name="scaleMin">最小缩放（标量直写）。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分（元组，被钉住）。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式（元组）。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数（元组）。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scale">统一缩放系数。</param>
	/// <param name="score">匹配分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 888。单模型等比缩放查找：5 条 DOUBLE 输出、无 model 索引；minScore/subPixel/numLevels 走 Store+UnpinTuple（钉住），其余标量直写。</para>
	///   <para><b>与相邻算子的取舍</b>单值参数请选全标量重载以免钉固定开销；行列缩放不等用 FindAnisoShapeModel；无缩放需求用 FindShapeModel（搜索维度最少最快）。本重载用于需要以元组形式批量传分阈值/亚像素模式的场合（原生对多元素的行为 [待实测]）。</para>
	///   <para><b>约束或前提</b>角度弧度制；scale 输出可乘回建模型时的基准得到工件实际尺寸比例；未命中输出空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   JlTuple minScore = 0.5;
	///   JlTuple subPixel = "least_squares";
	///   JlTuple numLevels = 0;
	///   scene.FindScaledShapeModel(model, -0.39, 0.78, 0.9, 1.1, minScore, 1, 0.5,
	///       subPixel, numLevels, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scale, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）。</para>
	/// </remarks>
	public void FindScaledShapeModel(JlShapeModel modelID, double angleStart, double angleExtent, double scaleMin, double scaleMax, JlTuple minScore, int numMatches, double maxOverlap, JlTuple subPixel, JlTuple numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(888);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleMin);
		JlNativeApi.StoreD(proc, 4, scaleMax);
		JlNativeApi.Store(proc, 5, minScore);
		JlNativeApi.StoreI(proc, 6, numMatches);
		JlNativeApi.StoreD(proc, 7, maxOverlap);
		JlNativeApi.Store(proc, 8, subPixel);
		JlNativeApi.Store(proc, 9, numLevels);
		JlNativeApi.StoreD(proc, 10, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minScore);
		JlNativeApi.UnpinTuple(subPixel);
		JlNativeApi.UnpinTuple(numLevels);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scale);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>查找单个等比缩放形状模型的最佳匹配（全标量参数版）。</summary>
	/// <param name="modelID">模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.78</param>
	/// <param name="scaleMin">最小缩放。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="scale">统一缩放系数。</param>
	/// <param name="score">匹配分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 888 的全标量版：minScore/scale 等走 StoreD、numMatches/numLevels 走 StoreI、subPixel 走 StoreS，无钉固定开销；5 条 DOUBLE 输出、无 model 索引。</para>
	///   <para><b>与相邻算子的取舍</b>本重载是等比缩放定位的默认入口；只要位姿不要缩放时用 FindShapeModel 更省；缩放各向异性时用 FindAnisoShapeModel。</para>
	///   <para><b>约束或前提</b>角度弧度制；未命中时输出全为空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindScaledShapeModel(model, -0.39, 0.78, 0.9, 1.1, 0.5, 1, 0.5,
	///       "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle,
	///       out JlTuple scale, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）。</para>
	/// </remarks>
	public void FindScaledShapeModel(JlShapeModel modelID, double angleStart, double angleExtent, double scaleMin, double scaleMax, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(888);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, scaleMin);
		JlNativeApi.StoreD(proc, 4, scaleMax);
		JlNativeApi.StoreD(proc, 5, minScore);
		JlNativeApi.StoreI(proc, 6, numMatches);
		JlNativeApi.StoreD(proc, 7, maxOverlap);
		JlNativeApi.StoreS(proc, 8, subPixel);
		JlNativeApi.StoreI(proc, 9, numLevels);
		JlNativeApi.StoreD(proc, 10, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out scale);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>查找单个形状模型（仅旋转）的最佳匹配（混合元组参数版）。</summary>
	/// <param name="modelID">模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="minScore">最低匹配分（元组，被钉住）。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式（元组）。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数（元组）。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="score">匹配分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 889。最基础的"给位姿"算子：4 条 DOUBLE 输出即刚体位姿 (row, column, angle) + score；本重载把 minScore/subPixel/numLevels 以钉住的元组交给原生。</para>
	///   <para><b>与相邻算子的取舍</b>单值场合请用全标量重载（免钉固）；有缩放/透视时位姿会因分数不足而漏检，须换 Scaled/Aniso 族；NCC 定位用 FindNccModel 族。</para>
	///   <para><b>约束或前提</b>angle 为相对模型坐标系的弧度角；未命中输出空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   JlTuple minScore = 0.5;
	///   JlTuple subPixel = "least_squares";
	///   JlTuple numLevels = 0;
	///   scene.FindShapeModel(model, -0.39, 0.79, minScore, 1, 0.5, subPixel, numLevels, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）；row/column/angle 三个元组长度一致但顺序不承诺跨帧稳定，多实例时自行按 score 排序再消费。</para>
	/// </remarks>
	public void FindShapeModel(JlShapeModel modelID, double angleStart, double angleExtent, JlTuple minScore, int numMatches, double maxOverlap, JlTuple subPixel, JlTuple numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(889);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, minScore);
		JlNativeApi.StoreI(proc, 4, numMatches);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.Store(proc, 6, subPixel);
		JlNativeApi.Store(proc, 7, numLevels);
		JlNativeApi.StoreD(proc, 8, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minScore);
		JlNativeApi.UnpinTuple(subPixel);
		JlNativeApi.UnpinTuple(numLevels);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>查找单个形状模型（仅旋转）的最佳匹配（全标量参数版，最常用的定位入口）。</summary>
	/// <param name="modelID">模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="minScore">最低匹配分。Default: 0.5</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">亚像素精度模式，"none" 关闭。Default: "least_squares"</param>
	/// <param name="numLevels">金字塔层数。Default: 0</param>
	/// <param name="greediness">搜索贪心度。Default: 0.9</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="score">匹配分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 889 的全标量版：全部参数 StoreD/StoreI/StoreS 直写，无钉固定开销；输出 4 条 DOUBLE 元组即刚体位姿+分数，可直接生成 <see cref="JlHomMat2D"/> 做后续对齐。</para>
	///   <para><b>与相邻算子的取舍</b>与混合元组重载同 id、行为一致，仅装载路径不同；单值参数场景一律用本重载。多模型轮询用 FindShapeModels 数组版更省调用次数。</para>
	///   <para><b>约束或前提</b>angle 弧度制；未命中输出空元组（用 row.TupleLength()==0 判空，而非比较 score 与 minScore）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindShapeModel(model, -0.39, 0.79, 0.5, 1, 0.5, "least_squares", 0, 0.9,
	///       out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）。</para>
	/// </remarks>
	public void FindShapeModel(JlShapeModel modelID, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(889);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, minScore);
		JlNativeApi.StoreI(proc, 4, numMatches);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.StoreS(proc, 6, subPixel);
		JlNativeApi.StoreI(proc, 7, numLevels);
		JlNativeApi.StoreD(proc, 8, greediness);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>对由 XLD 轮廓生成的形状模型重设匹配度量（需带上其模板图作为当前对象）。</summary>
	/// <param name="modelID">待改的模型句柄。</param>
	/// <param name="homMat2D">建模型时所用的变换矩阵（与建模型时保持一致才有效）。</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 890。当前 JlImage 会作为第一路 iconic 输入（实现里 Store(proc,1) 先存 this）——必须是当初生成该模型的模板图，原生要据此重算轮廓；把别的图传进来结果不可信 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>改的是"极性/忽略极性"这一匹配语义；改其它参数（角度序、层数等）走静态 <see cref="SetShapeModelParam"/>。metric 与 CreateShapeModel 的 metric 形参同一取值集。</para>
	///   <para><b>约束或前提</b>仅适用于 XLD 生成法建的模型；homMat2D 以钉住方式传给原生（调用后 UnpinTuple），期间不得改写该矩阵。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "ignore_polarity", "auto", "auto");
	///   JlHomMat2D hom = new JlHomMat2D();
	///   tmpl.SetShapeModelMetric(model, hom, "use_polarity");
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>JlHomMat2D 不实现 IDisposable，无释放负担；model 句柄调用后仍归调用方管理；示例中 hom 为恒等阵，与建模型时的缺省变换一致。</para>
	/// </remarks>
	public void SetShapeModelMetric(JlShapeModel modelID, JlHomMat2D homMat2D, string metric)
	{
		IntPtr proc = JlNativeApi.PreCall(890);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.Store(proc, 1, homMat2D);
		JlNativeApi.StoreS(proc, 2, metric);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>按"参数名/参数值"元组批量改写形状模型的可选参数（静态方法，不需要模板图）。</summary>
	/// <param name="modelID">待改的模型句柄。</param>
	/// <param name="genParamName">参数名元组（如 "num_levels"、"angle_step"，取值集合本层未体现 [待实测]）。</param>
	/// <param name="genParamValue">与参数名等长、按序对应的值元组。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 891。这是静态方法：实现里没有 Store(this)，与当前图像对象无关，通过 JlImage.SetShapeModelParam(...) 调用即可；两个元组全程钉住、调用后 UnpinTuple。</para>
	///   <para><b>与相邻算子的取舍</b>改匹配极性用 <see cref="SetShapeModelMetric(JlShapeModel,JlHomMat2D,string)"/>（那条还要模板图）；本方法是"万能后门"，参数名写错时原生可能静默忽略 [待实测]，改完建议用一次 FindShapeModel 验证分数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   JlImage.SetShapeModelParam(model, new string[] { "num_levels" }, new int[] { 4 });
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive(modelID) 佐证）；name 与 value 长度不等时的行为未定义 [待实测]。</para>
	/// </remarks>
	public static void SetShapeModelParam(JlShapeModel modelID, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(891);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.Store(proc, 2, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(modelID);
	}

	/// <summary>在当前（已裁域的）模板图上创建各向异性缩放形状模型，返回模型新句柄。</summary>
	/// <param name="numLevels">金字塔层数或 "auto"。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长或 "auto"。Default: "auto"</param>
	/// <param name="scaleRMin">行方向最小缩放。Default: 0.9</param>
	/// <param name="scaleRMax">行方向最大缩放。Default: 1.1</param>
	/// <param name="scaleRStep">行方向缩放步长或 "auto"。Default: "auto"</param>
	/// <param name="scaleCMin">列方向最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">列方向最大缩放。Default: 1.1</param>
	/// <param name="scaleCStep">列方向缩放步长或 "auto"。Default: "auto"</param>
	/// <param name="optimization">优化方式或 "auto"。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值/滞后双阈值或 "auto"。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度或 "auto"。Default: "auto"</param>
	/// <returns>模型的新句柄（JlShapeModel.LoadNew），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 895。行列缩放各自成维（anisotropic），搜索空间是 Scaled 版的平方级放大：建模型与匹配都更慢、内存更多。当前图像是模板源，其<b>域</b>决定取哪块轮廓，建模型前务必 ReduceDomain 圈住目标。</para>
	///   <para><b>与相邻算子的取舍</b>只有等比缩放用 CreateScaledShapeModel（id 896，一维 scale）；无缩放用 CreateShapeModel（id 897）；三者的 metric/contrast 语义相同。能 "auto" 的形参在标量重载里必须给数值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateAnisoShapeModel("auto", -0.39, 0.79, "auto",
	///       0.9, 1.1, "auto", 0.9, 1.1, "auto", "auto", "use_polarity", "auto", "auto");
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回句柄与模板图生命周期独立：Dispose 模板图后模型仍可用；angleStep 给得过小会把角度库撑大、Find 阶段逐角匹配变慢。</para>
	/// </remarks>
	public JlShapeModel CreateAnisoShapeModel(JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleRMin, double scaleRMax, JlTuple scaleRStep, double scaleCMin, double scaleCMax, JlTuple scaleCStep, JlTuple optimization, string metric, JlTuple contrast, JlTuple minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(895);
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
		JlNativeApi.Store(proc, 12, contrast);
		JlNativeApi.Store(proc, 13, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(angleStep);
		JlNativeApi.UnpinTuple(scaleRStep);
		JlNativeApi.UnpinTuple(scaleCStep);
		JlNativeApi.UnpinTuple(optimization);
		JlNativeApi.UnpinTuple(contrast);
		JlNativeApi.UnpinTuple(minContrast);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在模板图上创建各向异性缩放形状模型（全标量参数版，numLevels/contrast 必须给数值）。</summary>
	/// <param name="numLevels">金字塔层数（数值，无法传 "auto"）。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度，数值）。Default: "auto"</param>
	/// <param name="scaleRMin">行方向最小缩放。Default: 0.9</param>
	/// <param name="scaleRMax">行方向最大缩放。Default: 1.1</param>
	/// <param name="scaleRStep">行方向缩放步长。Default: "auto"</param>
	/// <param name="scaleCMin">列方向最小缩放。Default: 0.9</param>
	/// <param name="scaleCMax">列方向最大缩放。Default: 1.1</param>
	/// <param name="scaleCStep">列方向缩放步长。Default: "auto"</param>
	/// <param name="optimization">优化方式。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值（整数）。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度（整数）。Default: "auto"</param>
	/// <returns>模型的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 895 的全标量版：numLevels/contrast/minContrast 走 StoreI、步长与缩放走 StoreD，无钉固定开销。适合把 DetermineShapeModelParams 定出的数值直接回填、复现同一模型。</para>
	///   <para><b>与相邻算子的取舍</b>要 "auto" 自动量纲就用元组重载；等比缩放用 CreateScaledShapeModel。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateAnisoShapeModel(4, -0.39, 0.79, 0.052,
	///       0.9, 1.1, 0.05, 0.9, 1.1, 0.05, "auto", "use_polarity", 40, 15);
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>重载选择与元组版互斥于实参类型：任一处写 "auto" 字符串会整体落到元组重载；建模型在 Dispose 模板图后模型仍可用。</para>
	/// </remarks>
	public JlShapeModel CreateAnisoShapeModel(int numLevels, double angleStart, double angleExtent, double angleStep, double scaleRMin, double scaleRMax, double scaleRStep, double scaleCMin, double scaleCMax, double scaleCStep, string optimization, string metric, int contrast, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(895);
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
		JlNativeApi.StoreI(proc, 12, contrast);
		JlNativeApi.StoreI(proc, 13, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在模板图上创建等比缩放形状模型，返回模型新句柄（元组参数版）。</summary>
	/// <param name="numLevels">金字塔层数或 "auto"。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度）或 "auto"。Default: "auto"</param>
	/// <param name="scaleMin">最小缩放（行列同步）。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="scaleStep">缩放步长或 "auto"。Default: "auto"</param>
	/// <param name="optimization">优化方式或 "auto"。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值/滞后双阈值或 "auto"。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度或 "auto"。Default: "auto"</param>
	/// <returns>模型的新句柄（JlShapeModel.LoadNew），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 896。当前图像为模板源，其<b>域</b>决定提取哪块轮廓；输出模型携带 angle×scale 两级搜索库。可 "auto" 的形参走 Store+UnpinTuple（钉住）。</para>
	///   <para><b>与相邻算子的取舍</b>行列形变不等才升 CreateAnisoShapeModel（895，搜索量平方级）；确定无缩放用 CreateShapeModel（897）。metric 选 "ignore_polarity" 可容忍反色打光，但误匹配率上升。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateScaledShapeModel("auto", -0.39, 0.79, "auto",
	///       0.9, 1.1, "auto", "auto", "use_polarity", "auto", "auto");
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模板图 Dispose 后模型仍可用（生命周期独立）；scaleStep 过密会让角度×缩放组合数暴涨，Find 阶段耗时按组合数线性上升。</para>
	/// </remarks>
	public JlShapeModel CreateScaledShapeModel(JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleMin, double scaleMax, JlTuple scaleStep, JlTuple optimization, string metric, JlTuple contrast, JlTuple minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(896);
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
		JlNativeApi.Store(proc, 9, contrast);
		JlNativeApi.Store(proc, 10, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(angleStep);
		JlNativeApi.UnpinTuple(scaleStep);
		JlNativeApi.UnpinTuple(optimization);
		JlNativeApi.UnpinTuple(contrast);
		JlNativeApi.UnpinTuple(minContrast);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在模板图上创建等比缩放形状模型（全标量参数版，numLevels/contrast 必须给数值）。</summary>
	/// <param name="numLevels">金字塔层数（数值，无法传 "auto"）。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度，数值）。Default: "auto"</param>
	/// <param name="scaleMin">最小缩放。Default: 0.9</param>
	/// <param name="scaleMax">最大缩放。Default: 1.1</param>
	/// <param name="scaleStep">缩放步长（数值）。Default: "auto"</param>
	/// <param name="optimization">优化方式。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值（整数）。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度（整数）。Default: "auto"</param>
	/// <returns>模型的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 896 的全标量版：numLevels/contrast/minContrast 走 StoreI、角度与缩放走 StoreD，无钉固定开销；适合回填 DetermineShapeModelParams 定出的数值以复现模型。</para>
	///   <para><b>与相邻算子的取舍</b>需要 "auto" 语义（让原生自选层数/对比度）就用元组重载；无缩放场景降档 CreateShapeModel 更省。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateScaledShapeModel(4, -0.39, 0.79, 0.052,
	///       0.9, 1.1, 0.05, "auto", "use_polarity", 40, 15);
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>任一 "auto" 字符串实参会使整体落到元组重载，勿混写；Dispose 模板图不影响已建模型。</para>
	/// </remarks>
	public JlShapeModel CreateScaledShapeModel(int numLevels, double angleStart, double angleExtent, double angleStep, double scaleMin, double scaleMax, double scaleStep, string optimization, string metric, int contrast, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(896);
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
		JlNativeApi.StoreI(proc, 9, contrast);
		JlNativeApi.StoreI(proc, 10, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在模板图上创建（仅旋转的）形状模型，返回模型新句柄（元组参数版）。</summary>
	/// <param name="numLevels">金字塔层数或 "auto"。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度）或 "auto"。Default: "auto"</param>
	/// <param name="optimization">优化方式或 "auto"。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值/滞后双阈值或 "auto"。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度或 "auto"。Default: "auto"</param>
	/// <returns>模型的新句柄（JlShapeModel.LoadNew），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 897。形状匹配族的"标准建模型"：只搜旋转不搜缩放；当前图像为模板源，其<b>域</b>决定提取哪块轮廓，先 ReduceDomain 再建。可 "auto" 的形参钉住后 UnpinTuple。</para>
	///   <para><b>与相邻算子的取舍</b>有缩放用 CreateScaledShapeModel/CreateAnisoShapeModel；亮度漂移大、纹理弱（如印刷灰度块）时形状匹配不如 NCC：CreateNccModel。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel("auto", -0.39, 0.79, "auto", "auto", "use_polarity", "auto", "auto");
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>contrast 用 "auto" 时由原生按模板噪声水平定阈值——模板里若混入半个相邻零件，auto 会把它的轮廓也编进模型，建前务必裁域；Dispose 模板图不影响已建模型。</para>
	/// </remarks>
	public JlShapeModel CreateShapeModel(JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, JlTuple optimization, string metric, JlTuple contrast, JlTuple minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(897);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, angleStep);
		JlNativeApi.Store(proc, 4, optimization);
		JlNativeApi.StoreS(proc, 5, metric);
		JlNativeApi.Store(proc, 6, contrast);
		JlNativeApi.Store(proc, 7, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(angleStep);
		JlNativeApi.UnpinTuple(optimization);
		JlNativeApi.UnpinTuple(contrast);
		JlNativeApi.UnpinTuple(minContrast);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在模板图上创建（仅旋转的）形状模型（全标量参数版，numLevels/contrast 必须给数值）。</summary>
	/// <param name="numLevels">金字塔层数（数值，无法传 "auto"）。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度，数值）。Default: "auto"</param>
	/// <param name="optimization">优化方式。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <param name="contrast">模板图对比度阈值（整数）。Default: "auto"</param>
	/// <param name="minContrast">搜索图最小对比度（整数）。Default: "auto"</param>
	/// <returns>模型的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 897 的全标量版：numLevels/contrast/minContrast 走 StoreI、angleStep 走 StoreD，无钉固定开销；适合按 DetermineShapeModelParams 的建议值精确复现模型。</para>
	///   <para><b>与相邻算子的取舍</b>要让原生自选层数/对比度就用元组重载；本重载 contrast 只能给单阈值，滞后双阈值（"high_low" 两值）不可表达。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlShapeModel model = tmpl.CreateShapeModel(4, -0.39, 0.79, 0.052, "auto", "use_polarity", 40, 15);
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>混写字符串实参会整体落到元组重载；建模型阶段对比度门槛给太低会把背景纹理一起编进模型，Find 分数天花板被拉低。</para>
	/// </remarks>
	public JlShapeModel CreateShapeModel(int numLevels, double angleStart, double angleExtent, double angleStep, string optimization, string metric, int contrast, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(897);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, angleStep);
		JlNativeApi.StoreS(proc, 4, optimization);
		JlNativeApi.StoreS(proc, 5, metric);
		JlNativeApi.StoreI(proc, 6, contrast);
		JlNativeApi.StoreI(proc, 7, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlShapeModel.LoadNew(proc, 0, err, out JlShapeModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>可视化"若按此对比度建模型会提取到哪些轮廓"：返回金字塔图像与模型区域。</summary>
	/// <param name="modelRegions">模型区域金字塔（新 JlRegion 句柄，每层一个区域）。</param>
	/// <param name="numLevels">金字塔层数。Default: 4</param>
	/// <param name="contrast">对比度阈值/滞后双阈值或最小尺寸（元组，被钉住）。Default: 30</param>
	/// <returns>输入图的金字塔（新图像句柄，每层一幅）。返回值与 out 都要释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 898。它<b>不创建模型</b>，是把 CreateShapeModel 前半段（边缘提取+按对比度筛选）的结果画出来供人眼核对：把 modelRegions 叠回图上即可看到将被编入模型的轮廓。</para>
	///   <para><b>与相邻算子的取舍</b>正式建模型直接调 CreateShapeModel；调 contrast/域 拿不准时先用本方法验证，避免建出"半个零件"或漏轮廓的模型。int contrast 重载省去钉住。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   JlTuple contrast = 30;
	///   using JlImage pyramid = tmpl.InspectShapeModel(out JlRegion modelRegions, 4, contrast);
	///   modelRegions.Dispose();
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的图像与 out 的区域都是新句柄，示例中 pyramid 用 using、modelRegions 手动 Dispose；numLevels 与金字塔幅数一致，region 元组式栈按层排列，取第 k 层用 SelectObj。</para>
	/// </remarks>
	public JlImage InspectShapeModel(out JlRegion modelRegions, int numLevels, JlTuple contrast)
	{
		IntPtr proc = JlNativeApi.PreCall(898);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.Store(proc, 1, contrast);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(contrast);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlRegion.LoadNew(proc, 2, err, out modelRegions);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>预览按给定单一对比度阈值建形状模型将提取到的轮廓（标量版）。</summary>
	/// <param name="modelRegions">模型区域金字塔（新 JlRegion 句柄栈，每层一个区域，需释放）。</param>
	/// <param name="numLevels">金字塔层数。Default: 4</param>
	/// <param name="contrast">对比度阈值（整数，无法传滞后双阈值/"auto"）。Default: 30</param>
	/// <returns>输入图的金字塔（新图像句柄栈，需释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 898 的标量版：contrast 走 StoreI、不钉元组。用途同元组版——把 CreateShapeModel 的边缘提取阶段可视化核对。</para>
	///   <para><b>与相邻算子的取舍</b>要滞后双阈值（contrast 两元素）或"auto"，用 <see cref="InspectShapeModel(out JlRegion,int,JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlImage pyramid = tmpl.InspectShapeModel(out JlRegion modelRegions, 4, 30);
	///   modelRegions.Dispose();
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两个输出都是新句柄：示例中 pyramid 用 using、modelRegions 手动 Dispose；本方法只是预览，最终模型仍要 CreateShapeModel 生成。</para>
	/// </remarks>
	public JlImage InspectShapeModel(out JlRegion modelRegions, int numLevels, int contrast)
	{
		IntPtr proc = JlNativeApi.PreCall(898);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreI(proc, 1, contrast);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlRegion.LoadNew(proc, 2, err, out modelRegions);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}























	/// <summary>在图中查找单个 NCC 模型的最佳匹配（灰度相关定位）。</summary>
	/// <param name="modelID">NCC 模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="minScore">最低匹配分。Default: 0.8</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">是否亚像素（"true"/"false" 风格）。Default: "true"</param>
	/// <param name="numLevels">金字塔层数（元组，被钉住）。Default: 0</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="score">归一化互相关得分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 945。按灰度相关（NCC）打分而非边缘形状：适合纹理弱、对比度低但亮度模式稳定的工件。缺省 minScore 比形状族高（0.8 vs 0.5）——互相关分数分布更集中。</para>
	///   <para><b>与相邻算子的取舍</b>边缘清晰、光照渐变不敏感的用 FindShapeModel；需要多模型一次查用 FindNccModels；本算子的 NCC 模型由 <see cref="CreateNccModel(JlTuple,double,double,JlTuple,string)"/> 创建。</para>
	///   <para><b>约束或前提</b>角度弧度制；NCC 对整体亮度线性变化不敏感但对非线性gamma差异敏感 [待实测]；未命中输出空元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlNCCModel model = tmpl.CreateNccModel("auto", -0.39, 0.79, "auto", "use_polarity");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindNccModel(model, -0.39, 0.79, 0.8, 1, 0.5, "true", 0,
	///       out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>numLevels 是钉住的元组形参（与标量版 FindShapeModel 不同），传字面量 0 时经隐式转换仍走本签名；模型句柄在原生调用结束前不得释放。</para>
	/// </remarks>
	public void FindNccModel(JlNCCModel modelID, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, JlTuple numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(945);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, minScore);
		JlNativeApi.StoreI(proc, 4, numMatches);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.StoreS(proc, 6, subPixel);
		JlNativeApi.Store(proc, 7, numLevels);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>在图中查找单个 NCC 模型的最佳匹配（numLevels 标量版）。</summary>
	/// <param name="modelID">NCC 模型句柄。</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="minScore">最低匹配分。Default: 0.8</param>
	/// <param name="numMatches">实例个数上限，0 表示全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠度。Default: 0.5</param>
	/// <param name="subPixel">是否亚像素（"true"/"false" 风格）。Default: "true"</param>
	/// <param name="numLevels">金字塔层数（int 直写，无钉固定）。Default: 0</param>
	/// <param name="row">实例形心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">形心列坐标。</param>
	/// <param name="angle">旋转角（弧度）。</param>
	/// <param name="score">归一化互相关得分。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 945：numLevels 走 StoreI 的标量重载，其余与元组版一致（4 条 DOUBLE 输出）。NCC 按灰度相关打分，适合边缘弱、亮度模式稳的工件。</para>
	///   <para><b>与相邻算子的取舍</b>传字面量 0 时两个重载都可用，本重载绑定 int 实参更严格、免 UnpinTuple；多模型一次查用 FindNccModels。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlNCCModel model = tmpl.CreateNccModel(4, -0.39, 0.79, 0.052, "use_polarity");
	///   JlImage scene = new JlImage("byte", 512, 512);
	///   scene.FindNccModel(model, -0.39, 0.79, 0.8, 1, 0.5, "true", 0,
	///       out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
	///   tmpl.Dispose();
	///   scene.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>模型句柄在原生调用结束前不得释放（GC.KeepAlive 佐证）；未命中输出空元组。</para>
	/// </remarks>
	public void FindNccModel(JlNCCModel modelID, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(945);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, minScore);
		JlNativeApi.StoreI(proc, 4, numMatches);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.StoreS(proc, 6, subPixel);
		JlNativeApi.StoreI(proc, 7, numLevels);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelID);
	}

	/// <summary>按"参数名/参数值"元组批量改写 NCC 模型的可选参数（静态方法，不需要图像）。</summary>
	/// <param name="modelID">待改的 NCC 模型句柄。</param>
	/// <param name="genParamName">参数名元组（取值集合本层未体现 [待实测]）。</param>
	/// <param name="genParamValue">与参数名等长、按序对应的值元组。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 946。实现里无 Store(this)：静态调用 JlImage.SetNccModelParam(...)，两个元组钉住后 UnpinTuple；与形状模型的 <see cref="SetShapeModelParam(JlShapeModel,JlTuple,JlTuple)"/> 同构。</para>
	///   <para><b>与相邻算子的取舍</b>模型的角度范围/层数在建模型时定死，本方法能改哪些项不确定 [待实测]，改后务必用 FindNccModel 验证。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlNCCModel model = tmpl.CreateNccModel(4, -0.39, 0.79, 0.052, "use_polarity");
	///   JlImage.SetNccModelParam(model, new string[] { "min_contrast" }, new int[] { 15 });
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>name/value 长度不等时行为未定义 [待实测]；模型句柄在原生调用结束前不得释放。</para>
	/// </remarks>
	public static void SetNccModelParam(JlNCCModel modelID, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(946);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.Store(proc, 2, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(modelID);
	}

	/// <summary>以当前图为模板创建 NCC 模型，返回模型新句柄（元组参数版）。</summary>
	/// <param name="numLevels">金字塔层数或 "auto"。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度）或 "auto"。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <returns>NCC 模型的新句柄（JlNCCModel.LoadNew），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 947。与形状模型的差异：不抽边缘、不要求 contrast 参数，直接以<b>域内灰度图样</b>做互相关模板——当前图像的域就是模板本身，裁域比形状族更关键。</para>
	///   <para><b>与相邻算子的取舍</b>需要行列独立缩放或遮挡容忍时形状模型更强；光照线性波动、弱边缘工件用本族。查找配 <see cref="FindNccModel(JlNCCModel,double,double,double,int,double,string,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlNCCModel model = tmpl.CreateNccModel("auto", -0.39, 0.79, "auto", "use_polarity");
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>numLevels/angleStep 钉住后 UnpinTuple；Dispose 模板图后模型仍可用；metric 用 "ignore_polarity" 时黑白反转也能匹配，注意误配风险。</para>
	/// </remarks>
	public JlNCCModel CreateNccModel(JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, string metric)
	{
		IntPtr proc = JlNativeApi.PreCall(947);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, angleStep);
		JlNativeApi.StoreS(proc, 4, metric);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(numLevels);
		JlNativeApi.UnpinTuple(angleStep);
		err = JlNCCModel.LoadNew(proc, 0, err, out JlNCCModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>以当前图为模板创建 NCC 模型（全标量参数版，numLevels/angleStep 必须给数值）。</summary>
	/// <param name="numLevels">金字塔层数（数值，无法传 "auto"）。Default: "auto"</param>
	/// <param name="angleStart">最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">旋转角覆盖范围（弧度）。Default: 0.79</param>
	/// <param name="angleStep">角度步长（弧度，数值）。Default: "auto"</param>
	/// <param name="metric">匹配度量（是否利用极性）。Default: "use_polarity"</param>
	/// <returns>NCC 模型的新句柄，用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 947 的标量版：numLevels 走 StoreI、angleStep 走 StoreD，无钉固定开销。模板=当前图像域内灰度图样。</para>
	///   <para><b>与相邻算子的取舍</b>要 "auto" 让原生选层数/步长用元组重载；本重载参数组合固定、适合产线固化配置。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage tmpl = new JlImage("byte", 64, 64);
	///   using JlNCCModel model = tmpl.CreateNccModel(4, -0.39, 0.79, 0.052, "use_polarity");
	///   tmpl.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>angleStep 决定角度库密度，过密建模型时间与内存上升；Dispose 模板图不影响模型。</para>
	/// </remarks>
	public JlNCCModel CreateNccModel(int numLevels, double angleStart, double angleExtent, double angleStep, string metric)
	{
		IntPtr proc = JlNativeApi.PreCall(947);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, angleStep);
		JlNativeApi.StoreS(proc, 4, metric);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNCCModel.LoadNew(proc, 0, err, out JlNCCModel obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}










	/// <summary>按滞后对比度把图像自动分割成初始连通域，返回轮廓区域新句柄（元组参数版）。</summary>
	/// <param name="contrastLow">滞后下阈值（对比度低限）。Default: "auto"</param>
	/// <param name="contrastHigh">滞后上阈值（对比度高限）。Default: "auto"</param>
	/// <param name="minSize">初始成分最小尺寸（像素数）。Default: "auto"</param>
	/// <param name="mode">自动分割方式。Default: "connection"</param>
	/// <param name="genericName">可选控制参数名（空元组表示不传）。Default: []</param>
	/// <param name="genericValue">可选控制参数值，与 genericName 等长。Default: []</param>
	/// <returns>各初始成分的轮廓区域栈（JlRegion 新句柄，LoadNew），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 970。这是成分模型（component model）流水线的第一步：先用滞后阈值+连通域把目标切成"初始成分"，再逐组喂给 GenShapeTrans/成分训练算子；单用价值有限。</para>
	///   <para><b>约束或前提</b>contrastLow 必须 ≤ contrastHigh，滞后双阈值语义同 EdgesHysteresis 族；mode/取值集合与 genericName 可选项本层未体现 [待实测]。当前对象是待分割图像。</para>
	///   <para><b>与相邻算子的取舍</b>只要普通二值域时用 Threshold+Connection 即可，不必进本条成分模型链路；本算子输出的区域是"轮廓区域"，面积统计前需 AreaTrans/重算语义 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion comps = img.GenInitialComponents("auto", "auto", "auto", "connection",
	///       new JlTuple(), new JlTuple());
	///   int n = comps.CountObj();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>五个元组形参全部钉住后逐个 UnpinTuple；输出区域栈顺序由连通域扫描序决定，与上游 Connection 一样不保证跨阈值参数稳定。</para>
	/// </remarks>
	public JlRegion GenInitialComponents(JlTuple contrastLow, JlTuple contrastHigh, JlTuple minSize, string mode, JlTuple genericName, JlTuple genericValue)
	{
		IntPtr proc = JlNativeApi.PreCall(970);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, contrastLow);
		JlNativeApi.Store(proc, 1, contrastHigh);
		JlNativeApi.Store(proc, 2, minSize);
		JlNativeApi.StoreS(proc, 3, mode);
		JlNativeApi.Store(proc, 4, genericName);
		JlNativeApi.Store(proc, 5, genericValue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(contrastLow);
		JlNativeApi.UnpinTuple(contrastHigh);
		JlNativeApi.UnpinTuple(minSize);
		JlNativeApi.UnpinTuple(genericName);
		JlNativeApi.UnpinTuple(genericValue);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>成分模型流水线第一步：滞后对比度+连通域切出初始成分轮廓区域（标量参数版，阈值必须给实数、不能传 "auto"）。</summary>
	/// <param name="contrastLow">滞后对比度下阈值，本版须为具体整数。Default: "auto"</param>
	/// <param name="contrastHigh">滞后对比度上阈值，须 ≥ contrastLow。Default: "auto"</param>
	/// <param name="minSize">初始成分最小像素数。Default: "auto"</param>
	/// <param name="mode">自动分割方式。Default: "connection"</param>
	/// <param name="genericName">单个可选控制参数名；传空串表示不带附加参数。Default: []</param>
	/// <param name="genericValue">该控制参数的值。Default: []</param>
	/// <returns>初始成分轮廓区域栈新句柄（JlRegion.LoadNew），用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 970，与元组重载同算子：本版把三个数值参数按 <c>StoreI</c> 写控制槽 0/1/2，
	///   mode 与参数名写 <c>StoreS</c> 槽 3/4，值写 <c>StoreD</c> 槽 5——全程不钉元组，且只支持一对 generic 参数。</para>
	///   <para><b>与元组重载的取舍</b><see cref="GenInitialComponents(JlTuple,JlTuple,JlTuple,string,JlTuple,JlTuple)"/>
	///   可传字符串 "auto" 让原生自动选阈值/最小尺寸，并能给多对 generic 参数；本标量版参数固定，适合产线固化配置。
	///   只要普通二值域时 Threshold+Connection 即可，不必进成分模型链路。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion comps = img.GenInitialComponents(15, 60, 100, "connection", "", 0.0);
	///   int n = comps.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>genericName 合法名字集合与 mode 取值本层未校验 [待实测]；输出为轮廓区域，
	///   面积统计前需按成分模型链路重算 [待实测]；区域栈顺序由连通域扫描序决定，跨参数不稳定。末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlRegion GenInitialComponents(int contrastLow, int contrastHigh, int minSize, string mode, string genericName, double genericValue)
	{
		IntPtr proc = JlNativeApi.PreCall(970);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, contrastLow);
		JlNativeApi.StoreI(proc, 1, contrastHigh);
		JlNativeApi.StoreI(proc, 2, minSize);
		JlNativeApi.StoreS(proc, 3, mode);
		JlNativeApi.StoreS(proc, 4, genericName);
		JlNativeApi.StoreD(proc, 5, genericValue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}


	/// <summary>把本图像对象栈里的多幅单通道图合成一幅多通道图：栈序即通道序。</summary>
	/// <returns>多通道图像新句柄；原栈不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1067：无控制参数，仅 <c>this</c> 一路图标输入。栈内每幅图成为一个通道、
	///   通道顺序 = 栈内序号（可先用 <see cref="SelectObj(JlTuple)"/> 重排再合成），结果尺寸 = 单幅尺寸。</para>
	///   <para><b>约束或前提</b>栈内各图必须同尺寸，否则原生侧报错 [待实测]；各图应为单通道，多通道输入叠加后的
	///   通道序本层无法判断 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>固定 2~7 路、写死实参更直观时用 <see cref="Compose3(JlImage,JlImage)"/> 族；
	///   栈长不固定（如 <see cref="ImageToChannels()"/> 拆完改完再合回）时本方法是唯一入口。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage r = new JlImage("byte", 64, 64);
	///   using JlImage stack3 = r.ConcatObj(new JlImage("byte", 64, 64)).ConcatObj(new JlImage("byte", 64, 64));
	///   using JlImage rgb = stack3.ChannelsToImage();
	///   int ch = rgb.CountChannels();                            // 3
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose；末尾 <c>GC.KeepAlive(this)</c>，调用结束前原栈不得释放。</para>
	/// </remarks>
	public JlImage ChannelsToImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1067);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把一幅多通道图拆成逐通道的单通道图像栈（一个栈句柄，通道序=栈序）。</summary>
	/// <returns>单通道图像栈新句柄，每通道一幅；原多通道图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1068，是 <see cref="ChannelsToImage()"/> 的逆操作：只返回<b>一个</b>栈句柄，
	///   逐通道取帧要再走 <see cref="SelectObj(int)"/>（1 起始）。灰度算子（阈值、滤波、直方图）不接受多通道输入 [待实测]，
	///   彩色图做这类处理前常先走本方法拆开。</para>
	///   <para><b>与相邻算子的取舍</b>通道数已知且 ≤7 时用 <c>DecomposeN</c> 一次拆全并直接逐路拿句柄，省掉再 SelectObj；
	///   只要一路通道用 <see cref="AccessChannel(int)"/> 更省。通道数不定（运行时才知）时才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage r = new JlImage("byte", 64, 64);
	///   using JlImage rgb = r.Compose3(new JlImage("byte", 64, 64), new JlImage("byte", 64, 64));
	///   using JlImage chans = rgb.ImageToChannels();
	///   int n = chans.CountObj();                                  // 3
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose；对单通道图调用得到只含 1 幅图的栈（幂等）[待实测]。
	///   末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlImage ImageToChannels()
	{
		IntPtr proc = JlNativeApi.PreCall(1068);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>七幅图合成七通道图，id 1069（本族通道数上限）。</summary>
	/// <param name="image2">第 2 通道。</param>
	/// <param name="image3">第 3 通道。</param>
	/// <param name="image4">第 4 通道。</param>
	/// <param name="image5">第 5 通道。</param>
	/// <param name="image6">第 6 通道。</param>
	/// <param name="image7">第 7 通道。</param>
	/// <returns>七通道图像的新句柄。</returns>
	/// <remarks>
	///   <para>族内分工、尺寸一致性与多通道图的后续处理见 <see cref="Compose3(JlImage,JlImage)"/>；
	///   本重载是独立原生 id 1069。7 是本族上限，再多通道需要按图像数组分开处理或分次合成
	///   [待实测：是否还有更高通道数的算子]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage ch0 = new JlImage("byte", 64, 64);
	///   using JlImage seven = ch0.Compose7(                 // this 提供第 1 路，故只传 6 个实参
	///       new JlImage("byte", 64, 64), new JlImage("byte", 64, 64), new JlImage("byte", 64, 64),
	///       new JlImage("byte", 64, 64), new JlImage("byte", 64, 64), new JlImage("byte", 64, 64));
	///   int channels = seven.CountChannels();                                 // 7
	///   </code>
	/// </remarks>
	public JlImage Compose7(JlImage image2, JlImage image3, JlImage image4, JlImage image5, JlImage image6, JlImage image7)
	{
		IntPtr proc = JlNativeApi.PreCall(1069);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 3, image3);
		JlNativeApi.Store(proc, 4, image4);
		JlNativeApi.Store(proc, 5, image5);
		JlNativeApi.Store(proc, 6, image6);
		JlNativeApi.Store(proc, 7, image7);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		GC.KeepAlive(image3);
		GC.KeepAlive(image4);
		GC.KeepAlive(image5);
		GC.KeepAlive(image6);
		GC.KeepAlive(image7);
		return obj;
	}

	/// <summary>六幅图合成六通道图，id 1070。</summary>
	/// <param name="image2">第 2 通道。</param>
	/// <param name="image3">第 3 通道。</param>
	/// <param name="image4">第 4 通道。</param>
	/// <param name="image5">第 5 通道。</param>
	/// <param name="image6">第 6 通道。</param>
	/// <returns>六通道图像的新句柄。</returns>
	/// <remarks>
	///   <para>族内分工与约束见 <see cref="Compose3(JlImage,JlImage)"/>；本重载是独立原生 id 1070，
	///   通道顺序 <c>this → image2 → … → image6</c>。再多一路用 <see cref="Compose7"/>（id 1069），这是本族上限。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage ch0 = new JlImage("byte", 64, 64);
	///   using JlImage six = ch0.Compose6(                   // this 提供第 1 路，故只传 5 个实参
	///       new JlImage("byte", 64, 64), new JlImage("byte", 64, 64), new JlImage("byte", 64, 64),
	///       new JlImage("byte", 64, 64), new JlImage("byte", 64, 64));
	///   int channels = six.CountChannels();                                   // 6
	///   </code>
	/// </remarks>
	public JlImage Compose6(JlImage image2, JlImage image3, JlImage image4, JlImage image5, JlImage image6)
	{
		IntPtr proc = JlNativeApi.PreCall(1070);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 3, image3);
		JlNativeApi.Store(proc, 4, image4);
		JlNativeApi.Store(proc, 5, image5);
		JlNativeApi.Store(proc, 6, image6);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		GC.KeepAlive(image3);
		GC.KeepAlive(image4);
		GC.KeepAlive(image5);
		GC.KeepAlive(image6);
		return obj;
	}

	/// <summary>五幅图合成五通道图，id 1071。</summary>
	/// <param name="image2">第 2 通道。</param>
	/// <param name="image3">第 3 通道。</param>
	/// <param name="image4">第 4 通道。</param>
	/// <param name="image5">第 5 通道。</param>
	/// <returns>五通道图像的新句柄。</returns>
	/// <remarks>
	///   <para>族内分工、尺寸一致性与"多通道图先转灰度再分割"见 <see cref="Compose3(JlImage,JlImage)"/>；
	///   本重载是独立原生 id 1071，通道顺序 <c>this → image2 → … → image5</c>。
	///   需要更多通道依次用 <see cref="Compose6"/>、<see cref="Compose7"/>（id 1070、1069），上限 7。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage a = new JlImage("byte", 640, 480);
	///   JlImage b = new JlImage("byte", 640, 480);
	///   JlImage c = new JlImage("byte", 640, 480);
	///   JlImage d = new JlImage("byte", 640, 480);
	///   JlImage e = new JlImage("byte", 640, 480);
	///   using (b) using (c) using (d) using (e)
	///   {
	///       using JlImage five = a.Compose5(b, c, d, e);
	///   }
	///   a.Dispose();
	///   </code>
	/// </remarks>
	public JlImage Compose5(JlImage image2, JlImage image3, JlImage image4, JlImage image5)
	{
		IntPtr proc = JlNativeApi.PreCall(1071);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 3, image3);
		JlNativeApi.Store(proc, 4, image4);
		JlNativeApi.Store(proc, 5, image5);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		GC.KeepAlive(image3);
		GC.KeepAlive(image4);
		GC.KeepAlive(image5);
		return obj;
	}

	/// <summary>四幅图合成四通道图，id 1072。</summary>
	/// <param name="image2">第 2 通道。</param>
	/// <param name="image3">第 3 通道。</param>
	/// <param name="image4">第 4 通道。</param>
	/// <returns>四通道图像的新句柄。</returns>
	/// <remarks>
	///   <para>与 <see cref="Compose3(JlImage,JlImage)"/> 同一族但<b>原生 id 不同（1072）</b>：
	///   通道顺序 <c>this → image2 → image3 → image4</c>，仍是一路图像输出。约束与坑（尺寸必须一致、
	///   多通道图不能直接进灰度算子）见该重载说明。</para>
	///   <para><b>注意</b>四通道不是"3+1"的扩展：把 alpha/置信度放第 4 通道是本库之外的约定，
	///   本层不会在任何算子里按第 4 通道特殊处理 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage a = new JlImage("byte", 640, 480);
	///   JlImage b = new JlImage("byte", 640, 480);
	///   JlImage c = new JlImage("byte", 640, 480);
	///   JlImage d = new JlImage("byte", 640, 480);
	///   using (b) using (c) using (d)
	///   {
	///       using JlImage quad = a.Compose4(b, c, d);
	///   }
	///   a.Dispose();
	///   </code>
	/// </remarks>
	public JlImage Compose4(JlImage image2, JlImage image3, JlImage image4)
	{
		IntPtr proc = JlNativeApi.PreCall(1072);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 3, image3);
		JlNativeApi.Store(proc, 4, image4);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		GC.KeepAlive(image3);
		GC.KeepAlive(image4);
		return obj;
	}

	/// <summary>把三幅图合成一幅三通道图（通道顺序 = 参数顺序）。</summary>
	/// <param name="image2">第 2 通道。</param>
	/// <param name="image3">第 3 通道。</param>
	/// <returns>三通道图像的新句柄；输入图不变。</returns>
	/// <remarks>
	///   <para><b>通道族分工（先选对算子）</b>
	///   <c>ComposeN</c>：把 N 幅图合成<b>一幅 N 通道图</b>，N=2…7 各占一个原生算子（1074、1073、1072、1071、1070、1069，随 N 递减），
	///   彼此不是同一 id 的参数化，别指望"一个算子支持任意通道数"；
	///   <c>DecomposeN</c>：其逆运算；
	///   <see cref="ChannelsToImage"/> / <see cref="ImageToChannels"/>（1067/1068）：在"图像数组"与"一幅多通道图"之间整批转换；
	///   <see cref="AppendChannel(JlImage)"/>（1082）：只追加一个通道，不必预先数清通道数；
	///   <c>TileChannels</c>：把通道<b>平铺成一张大图</b>便于比较，产物仍是单通道图；
	///   <see cref="AccessChannel(int)"/>（1083）：反向取通道。</para>
	///   <para><b>功能说明</b>本算子 id 1073，只声明一路图标输出（<c>InitOCT(proc,1)</c>），
	///   结果必定是<b>一幅</b>三通道图，通道顺序严格为 <c>this → image2 → image3</c>。
	///   所谓 "RGB" 只是这个顺序的命名约定，本层不会校正你把 G 放在哪一位。</para>
	///   <para><b>约束</b>三幅输入需同宽高；类型是否必须一致本层未校验 [待实测：不一致时报错还是隐式提升]。
	///   输入本身已是多通道图时的展开方式本层无法判断 [待实测]，稳妥做法是先 <c>AccessChannel</c> 取单通道。
	///   合成出的多通道图不能直接进灰度算子 [待实测]，先转灰度或取通道。</para>
	///   <para><b>与 <c>Compose2</c> 的取舍</b>只有两个特征通道（灰度 + 梯度幅值、可见光 + 红外）就用
	///   <see cref="Compose2(JlImage)"/>；不要为凑三通道复制一幅无意义图——<see cref="CountChannels()"/> 的结果会被下游按通道数分支的代码读到并误解。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage r = new JlImage("byte", 640, 480);
	///   using JlImage g = new JlImage("byte", 640, 480);
	///   using JlImage b = new JlImage("byte", 640, 480);
	///   using JlImage rgb = r.Compose3(g, b);
	///   int channels = rgb.CountChannels();                       // 3
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；三幅输入只读、各做 <c>GC.KeepAlive</c>，所有权不转移。</para>
	/// </remarks>
	public JlImage Compose3(JlImage image2, JlImage image3)
	{
		IntPtr proc = JlNativeApi.PreCall(1073);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 3, image3);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		GC.KeepAlive(image3);
		return obj;
	}

	/// <summary>把两幅图合成一幅两通道图，id 1074。</summary>
	/// <param name="image2">第 2 通道。</param>
	/// <returns>两通道图像的新句柄。</returns>
	/// <remarks>
	///   <para>通道族分工、尺寸/类型约束、多通道图不能直接进灰度算子等要点见 <see cref="Compose3(JlImage,JlImage)"/>；
	///   本算子是同一族的 N=2 情形，<b>独立原生 id 1074</b>，通道顺序为 <c>this → image2</c>。</para>
	///   <para><b>典型用法</b>把两路特征并到一幅图里再做双通道判据：灰度 + <c>SobelAmp</c> 幅值、可见光 + 热成像。
	///   如果只是想让两幅图共用一次处理流程，用图像数组（<c>ConcatObj</c>）而不是加通道。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage amp = img.SobelAmp("sum", 3);
	///   using JlImage pair = img.Compose2(amp);
	///   int channels = pair.CountChannels();
	///   </code>
	///   <para><b>资源与坑</b>输入只读，返回新句柄。</para>
	/// </remarks>
	public JlImage Compose2(JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1074);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>把 7 通道图拆成 7 幅单通道图：第 1 通道作返回值，第 2~7 通道经 out 带出。</summary>
	/// <param name="image2">第 2 通道输出（新句柄）。</param>
	/// <param name="image3">第 3 通道输出（新句柄）。</param>
	/// <param name="image4">第 4 通道输出（新句柄）。</param>
	/// <param name="image5">第 5 通道输出（新句柄）。</param>
	/// <param name="image6">第 6 通道输出（新句柄）。</param>
	/// <param name="image7">第 7 通道输出（新句柄）。</param>
	/// <returns>第 1 通道的新图像句柄；原 7 通道图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1075，<see cref="Compose7"/> 的逆操作：<c>InitOCT</c> 声明 1~7 共七路图标输出，
	///   调用后逐槽 <c>LoadNew</c>。一次调用即可拿到全部 7 幅独立句柄，是唯一"逐路直接给句柄"的 7 通道拆法。</para>
	///   <para><b>约束或前提</b>输入必须恰为 7 通道；通道数不符由原生报错、本层不预检 [待实测：错误码]。</para>
	///   <para><b>与相邻算子的取舍</b>通道数运行时才知用 <see cref="ImageToChannels()"/>（拿一个栈再 SelectObj）；
	///   只要某一路用 <see cref="AccessChannel(int)"/>。6 路及以下各有对应版本的 <c>DecomposeN</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage ch0 = new JlImage("byte", 64, 64);
	///   using JlImage seven = ch0.Compose7(new JlImage("byte", 64, 64), new JlImage("byte", 64, 64),
	///       new JlImage("byte", 64, 64), new JlImage("byte", 64, 64), new JlImage("byte", 64, 64),
	///       new JlImage("byte", 64, 64));
	///   using JlImage c1 = seven.Decompose7(out JlImage c2, out JlImage c3, out JlImage c4,
	///       out JlImage c5, out JlImage c6, out JlImage c7);
	///   </code>
	///   <para><b>资源与坑</b>返回值 + 6 个 out 共 7 份新句柄，用毕逐一 Dispose；漏 Dispose 只泄原生句柄、不报编译错。
	///   out 实参必须显式写 <c>out</c>（漏写 CS1615）。</para>
	/// </remarks>
	public JlImage Decompose7(out JlImage image2, out JlImage image3, out JlImage image4, out JlImage image5, out JlImage image6, out JlImage image7)
	{
		IntPtr proc = JlNativeApi.PreCall(1075);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out image2);
		err = LoadNew(proc, 3, err, out image3);
		err = LoadNew(proc, 4, err, out image4);
		err = LoadNew(proc, 5, err, out image5);
		err = LoadNew(proc, 6, err, out image6);
		err = LoadNew(proc, 7, err, out image7);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把 6 通道图拆成 6 幅单通道图：第 1 通道作返回值，第 2~6 通道经 out 带出。</summary>
	/// <param name="image2">第 2 通道输出（新句柄）。</param>
	/// <param name="image3">第 3 通道输出（新句柄）。</param>
	/// <param name="image4">第 4 通道输出（新句柄）。</param>
	/// <param name="image5">第 5 通道输出（新句柄）。</param>
	/// <param name="image6">第 6 通道输出（新句柄）。</param>
	/// <returns>第 1 通道的新图像句柄；原 6 通道图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1076，<see cref="Compose6"/> 的逆操作：<c>InitOCT</c> 声明 1~6 六路图标输出、
	///   逐槽 <c>LoadNew</c>。通道序 = 出参序（返回值是第 1 通道），与 Compose 系拼装顺序严格互逆。</para>
	///   <para><b>约束或前提</b>输入必须恰为 6 通道，通道数不符由原生报错 [待实测：错误码]；
	///   7 通道用 <see cref="Decompose7(out JlImage,out JlImage,out JlImage,out JlImage,out JlImage,out JlImage)"/>。</para>
	///   <para><b>与相邻算子的取舍</b>通道数运行时才知用 <see cref="ImageToChannels()"/>；只要某一路用
	///   <see cref="AccessChannel(int)"/>；确定 6 通道时本版一次拿全、免建栈免 SelectObj。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage ch0 = new JlImage("byte", 64, 64);
	///   using JlImage six = ch0.Compose6(new JlImage("byte", 64, 64), new JlImage("byte", 64, 64),
	///       new JlImage("byte", 64, 64), new JlImage("byte", 64, 64), new JlImage("byte", 64, 64));
	///   using JlImage c1 = six.Decompose6(out JlImage c2, out JlImage c3, out JlImage c4, out JlImage c5, out JlImage c6);
	///   c6.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值 + 5 个 out 共 6 份新句柄，用毕逐一 Dispose；out 实参必须显式写 <c>out</c>。</para>
	/// </remarks>
	public JlImage Decompose6(out JlImage image2, out JlImage image3, out JlImage image4, out JlImage image5, out JlImage image6)
	{
		IntPtr proc = JlNativeApi.PreCall(1076);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out image2);
		err = LoadNew(proc, 3, err, out image3);
		err = LoadNew(proc, 4, err, out image4);
		err = LoadNew(proc, 5, err, out image5);
		err = LoadNew(proc, 6, err, out image6);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把 5 通道图拆成 5 幅单通道图：第 1 通道作返回值，第 2~5 通道经 out 带出。</summary>
	/// <param name="image2">第 2 通道输出（新句柄）。</param>
	/// <param name="image3">第 3 通道输出（新句柄）。</param>
	/// <param name="image4">第 4 通道输出（新句柄）。</param>
	/// <param name="image5">第 5 通道输出（新句柄）。</param>
	/// <returns>第 1 通道的新图像句柄；原 5 通道图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1077，<see cref="Compose5"/> 的逆操作：<c>InitOCT</c> 声明 1~5 五路图标输出、
	///   逐槽 <c>LoadNew</c>。各版 DecomposeN 的 id 连排（7→1075 递减至 2→1080），N 必须与图像实际通道数严格相等。</para>
	///   <para><b>约束或前提</b>输入恰为 5 通道；4 或 6 通道调本版由原生报错、不会自动对齐 [待实测：错误码]。</para>
	///   <para><b>与相邻算子的取舍</b>通道数不定用 <see cref="ImageToChannels()"/>，只要一路用 <see cref="AccessChannel(int)"/>，
	///   确定 5 通道时本版一次拿全。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage ch0 = new JlImage("byte", 64, 64);
	///   using JlImage five = ch0.Compose5(new JlImage("byte", 64, 64), new JlImage("byte", 64, 64),
	///       new JlImage("byte", 64, 64), new JlImage("byte", 64, 64));
	///   using JlImage c1 = five.Decompose5(out JlImage c2, out JlImage c3, out JlImage c4, out JlImage c5);
	///   </code>
	///   <para><b>资源与坑</b>返回值 + 4 个 out 共 5 份新句柄，用毕逐一 Dispose；out 实参必须显式写 <c>out</c>。
	///   末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlImage Decompose5(out JlImage image2, out JlImage image3, out JlImage image4, out JlImage image5)
	{
		IntPtr proc = JlNativeApi.PreCall(1077);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out image2);
		err = LoadNew(proc, 3, err, out image3);
		err = LoadNew(proc, 4, err, out image4);
		err = LoadNew(proc, 5, err, out image5);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把 4 通道图拆成 4 幅单通道图：第 1 通道作返回值，第 2~4 通道经 out 带出。</summary>
	/// <param name="image2">第 2 通道输出（新句柄）。</param>
	/// <param name="image3">第 3 通道输出（新句柄）。</param>
	/// <param name="image4">第 4 通道输出（新句柄）。</param>
	/// <returns>第 1 通道的新图像句柄；原 4 通道图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1078，<see cref="Compose4"/> 的逆操作：<c>InitOCT</c> 声明 1~4 四路图标输出、
	///   逐槽 <c>LoadNew</c>。常见于 CMYK 类四通道图或"RGB+附加通道"的拆分。</para>
	///   <para><b>约束或前提</b>输入恰为 4 通道；三通道彩色图请用 <see cref="Decompose3(out JlImage,out JlImage)"/>，
	///   调错版本由原生报错 [待实测：错误码]。</para>
	///   <para><b>与相邻算子的取舍</b>通道数不定用 <see cref="ImageToChannels()"/>；只要一路用 <see cref="AccessChannel(int)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage ch0 = new JlImage("byte", 64, 64);
	///   using JlImage four = ch0.Compose4(new JlImage("byte", 64, 64), new JlImage("byte", 64, 64),
	///       new JlImage("byte", 64, 64));
	///   using JlImage c1 = four.Decompose4(out JlImage c2, out JlImage c3, out JlImage c4);
	///   </code>
	///   <para><b>资源与坑</b>返回值 + 3 个 out 共 4 份新句柄，用毕逐一 Dispose；out 实参必须显式写 <c>out</c>。
	///   末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlImage Decompose4(out JlImage image2, out JlImage image3, out JlImage image4)
	{
		IntPtr proc = JlNativeApi.PreCall(1078);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out image2);
		err = LoadNew(proc, 3, err, out image3);
		err = LoadNew(proc, 4, err, out image4);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把 3 通道图拆成 3 幅单通道图：第 1 通道作返回值，第 2、3 通道经 out 带出（彩色 RGB 拆分常用）。</summary>
	/// <param name="image2">第 2 通道输出（新句柄）。</param>
	/// <param name="image3">第 3 通道输出（新句柄）。</param>
	/// <returns>第 1 通道的新图像句柄；原 3 通道图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1079，<see cref="Compose3(JlImage,JlImage)"/> 的逆操作：<c>InitOCT</c> 声明
	///   1~3 三路图标输出、逐槽 <c>LoadNew</c>。通道 1/2/3 = Compose3 的 this/image2/image3 各路，顺序严格互逆。</para>
	///   <para><b>约束或前提</b>输入恰为 3 通道；单通道图对每一路做处理前请直接用原图，两通道用 Decompose2，
	///   调错版本由原生报错 [待实测：错误码]。</para>
	///   <para><b>与相邻算子的取舍</b>只要其中一路用 <see cref="AccessChannel(int)"/> 更省；通道数不定用
	///   <see cref="ImageToChannels()"/>；拆出的三路要按加权亮度合灰度时改走颜色转换族而非逐路手工加权 [待实测：本库颜色族覆盖]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage r = new JlImage("byte", 64, 64);
	///   using JlImage rgb = r.Compose3(new JlImage("byte", 64, 64), new JlImage("byte", 64, 64));
	///   using JlImage g = rgb.Decompose3(out JlImage c2, out JlImage c3);   // c2/c3 即第 2、3 通道
	///   </code>
	///   <para><b>资源与坑</b>返回值 + 2 个 out 共 3 份新句柄，用毕逐一 Dispose；out 实参必须显式写 <c>out</c>。
	///   末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlImage Decompose3(out JlImage image2, out JlImage image3)
	{
		IntPtr proc = JlNativeApi.PreCall(1079);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out image2);
		err = LoadNew(proc, 3, err, out image3);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把 2 通道图拆成 2 幅单通道图：第 1 通道作返回值，第 2 通道经 out 带出。</summary>
	/// <param name="image2">第 2 通道输出（新句柄）。</param>
	/// <returns>第 1 通道的新图像句柄；原 2 通道图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1080，<see cref="Compose2(JlImage)"/> 的逆操作：<c>InitOCT</c> 声明 1、2 两路
	///   图标输出、逐槽 <c>LoadNew</c>。典型用法是把"灰度+置信度/法向"一类双通道图拆开分别处理。</para>
	///   <para><b>约束或前提</b>输入必须恰为 2 通道；单通道或 3 通道调本版由原生报错 [待实测：错误码]。
	///   与 <see cref="Decompose3(out JlImage,out JlImage)"/> 及更高分解版共享同一族 id（1075~1080 依次对应 7→2 通道）。</para>
	///   <para><b>与相邻算子的取舍</b>只取一路时 <see cref="AccessChannel(int)"/> 一个调用即可、无需接收第二路；
	///   要逐通道对象栈用 <see cref="ImageToChannels()"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage a = new JlImage("byte", 64, 64);
	///   using JlImage pair = a.Compose2(new JlImage("byte", 64, 64));
	///   using JlImage c1 = pair.Decompose2(out JlImage c2);
	///   </code>
	///   <para><b>资源与坑</b>返回值与 out 共 2 份新句柄，用毕 Dispose；out 实参必须显式写 <c>out</c>（漏写 CS1615）。
	///   末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlImage Decompose2(out JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1080);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out image2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>统计通道数：栈内每幅图各得一个整数值，按 INTEGER 装入 JlTuple 返回。</summary>
	/// <returns>整数元组，长度 = 本图像栈的对象个数；每元素为对应图的通道数。不产生图标句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1081。<c>InitOCT(proc,0)</c> 表明输出是控制参数而非图标对象，
	///   <c>JlTuple.LoadNew(..., JlTupleType.INTEGER, ...)</c> 按整数装载——返回类型是 JlTuple，不是 int。</para>
	///   <para><b>多值坑</b>对多幅图栈调用会返回多值元组；经 JlTuple→int 隐式转换接收只保留<b>首值</b>、其余静默丢弃。
	///   要逐图通道数需遍历元组，或先 <see cref="SelectObj(int)"/> 拆成单图再查。</para>
	///   <para><b>与相邻算子的取舍</b>拆前先查通道数决定用哪个 <c>DecomposeN</c>，正是它的主要用途；
	///   判断"是不是彩色图"也可以直接比较首值是否 ≥3，无需另找属性算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage r = new JlImage("byte", 64, 64);
	///   using JlImage rgb = r.Compose3(new JlImage("byte", 64, 64), new JlImage("byte", 64, 64));
	///   int ch = rgb.CountChannels();                                // 单图栈：3
	///   </code>
	///   <para><b>资源与坑</b>JlTuple 虽实现 IDisposable，纯数值元组可不处理 [待实测：其 Dispose 语义]；
	///   末尾 <c>GC.KeepAlive(this)</c>，调用结束前图像不得释放。</para>
	/// </remarks>
	public JlTuple CountChannels()
	{
		IntPtr proc = JlNativeApi.PreCall(1081);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>把另一幅图的通道追加到本图通道之后，得到通道数相加的新多通道图。</summary>
	/// <param name="image">被追加的图像，其通道按自身顺序接在本图之后。</param>
	/// <returns>追加完成的新图像句柄；两个输入均不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1082：<c>this</c> 存图标槽 1、<paramref name="image"/> 存槽 2，结果 <c>LoadNew</c> 槽 1。
	///   通道顺序 = 本图原有通道 → image 的通道，适合"在已合成图上再挂一路附加数据"（灰度图补法向/置信度通道）。</para>
	///   <para><b>约束或前提</b>两图须同尺寸 [待实测：不符时报错行为]；image 为多通道时一次全部接上 [待实测]；
	///   类型不一致时结果类型与本图类型的落值关系未在本层体现 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>从零开始拼 2~7 路等通道图用 <see cref="Compose3(JlImage,JlImage)"/> 族更直观；
	///   本方法是"已有图 + 增量通道"。拆回单路用 <see cref="AccessChannel(int)"/>，两者互逆。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage baseImg = new JlImage("byte", 64, 64);
	///   using JlImage extra = new JlImage("byte", 64, 64);
	///   using JlImage twoCh = baseImg.AppendChannel(extra);
	///   int ch = twoCh.CountChannels();                              // 2
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需 Dispose；末尾对两图 <c>GC.KeepAlive</c>，调用结束前都不得释放。
	///   追加不改本图，若想要"原地加通道"的语义本库没有提供 [待实测]。</para>
	/// </remarks>
	public JlImage AppendChannel(JlImage image)
	{
		IntPtr proc = JlNativeApi.PreCall(1082);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>取多通道图的通道（索引从 1 开始，可重排）。</summary>
	/// <param name="channel">通道索引。Default: 1</param>
	/// <returns>取出的通道图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1083。通道索引是<b>控制参数</b>（<c>Store(proc, 0, channel)</c>），
	///   不是图标对象；从 <b>1</b> 开始计数，写 0 不会在这里被拦下 [待实测：越界是报错还是回绕]。</para>
	///   <para><b>多索引</b>元组可一次给多个索引，本层仍只声明一路图标输出（<c>InitOCT(proc,1)</c>），
	///   因此结果应是<b>一幅按给定顺序重排的</b>多通道图 [待实测]；要拆成逐通道单图请用 <c>DecomposeN</c>
	///   或反复 <c>AccessChannel(1)</c>/<see cref="AppendChannel(JlImage)"/> 组合。这与
	///   <see cref="Compose3(JlImage,JlImage)"/> 互为逆操作。</para>
	///   <para><b>为什么常要用它</b>阈值、滤波、直方图这类灰度算子不接受多通道输入 [待实测]，
	///   彩色图做分割前先 <c>AccessChannel(1)</c> 取一个通道；要按加权亮度合成，则用
	///   <c>ChannelsToImage()</c> 拆成三幅单通道图后再调 <c>Rgb3ToGray(imageGreen, imageBlue)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage r = new JlImage("byte", 640, 480);
	///   using JlImage g = new JlImage("byte", 640, 480);
	///   using JlImage b = new JlImage("byte", 640, 480);
	///   using JlImage rgb = r.Compose3(g, b);
	///   using JlImage red = rgb.AccessChannel(new JlTuple(1));      // 第 1 通道
	///   using JlImage swapped = rgb.AccessChannel(new JlTuple(3.0, 2.0, 1.0));
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄；取出的通道是否与原图共享像素内存本层无法判断 [待实测]，
	///   要改写像素先 <c>CopyImage()</c>。</para>
	/// </remarks>
	public JlImage AccessChannel(JlTuple channel)
	{
		IntPtr proc = JlNativeApi.PreCall(1083);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, channel);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(channel);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>取一个通道（单索引版）。</summary>
	/// <param name="channel">通道索引，从 1 开始。Default: 1</param>
	/// <returns>该通道图像的新句柄。</returns>
	/// <remarks>
	///   <para>1 起始计数、越界不校验、与 Compose/Decompose 的关系见 <see cref="AccessChannel(JlTuple)"/>：
	///   同一原生 id 1083，本版本 <c>StoreI</c> 只传一个索引，返回单通道图，是绝大多数场合的写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage r = new JlImage("byte", 640, 480);
	///   using JlImage g = new JlImage("byte", 640, 480);
	///   using JlImage rgb = r.Compose2(g);
	///   using JlImage green = rgb.AccessChannel(2);
	///   </code>
	/// </remarks>
	public JlImage AccessChannel(int channel)
	{
		IntPtr proc = JlNativeApi.PreCall(1083);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, channel);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把本图像栈里的每幅图按元组逐图给定落位/裁取范围，拼贴到 width×height 的大画布上。</summary>
	/// <param name="offsetRow">各图左上角落在输出图中的行坐标（像素），元组第 i 值对第 i 幅图。Default: 0</param>
	/// <param name="offsetCol">各图左上角落在输出图中的列坐标（像素）。Default: 0</param>
	/// <param name="row1">各图裁取源区域左上角行坐标；-1 表示整幅拷入。Default: -1</param>
	/// <param name="col1">各图裁取源区域左上角列坐标；-1 表示整幅拷入。Default: -1</param>
	/// <param name="row2">各图裁取源区域右下角行坐标（闭区间）。Default: -1</param>
	/// <param name="col2">各图裁取源区域右下角列坐标（闭区间）。Default: -1</param>
	/// <param name="width">输出画布宽度（像素），以 <c>StoreI</c> 单独直写。Default: 512</param>
	/// <param name="height">输出画布高度（像素）。Default: 512</param>
	/// <returns>拼贴后的新图像句柄；输入栈不变，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1084。六个定位参数各是整条钉传的控制元组（槽 0~5，调用后逐一 <c>UnpinTuple</c>）：
	///   元组第 i 个值作用于本栈第 i 幅图，逐图独立定位；width/height 是画布全局量、以 <c>StoreI</c> 写槽 6/7。</para>
	///   <para><b>与标量重载的取舍</b>所有图共用同一组落位/裁取时用
	///   <see cref="TileImagesOffset(int,int,int,int,int,int,int,int)"/>，免钉六条元组；本重载适合错位排布、
	///   只拷每图局部块的场景。元组长度与栈长不等时如何取值本层未校验 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage a = new JlImage("byte", 32, 32);
	///   using JlImage stack2 = a.ConcatObj(new JlImage("byte", 32, 32));
	///   using JlImage tiled = stack2.TileImagesOffset(
	///       new JlTuple(0.0, 0.0), new JlTuple(0.0, 32.0),        // 第二幅右移 32 列
	///       new JlTuple(-1.0, -1.0), new JlTuple(-1.0, -1.0),     // 行裁取界：-1 = 整幅
	///       new JlTuple(-1.0, -1.0), new JlTuple(-1.0, -1.0), 64, 32);
	///   </code>
	///   <para><b>资源与坑</b>画布固定 width×height，落位越界的像素被裁掉、拷入内容超出画布的部分丢弃；
	///   拼贴次序依赖栈内既有次序，次序不稳会静默错位。末尾 <c>GC.KeepAlive(this)</c>。</para>
	/// </remarks>
	public JlImage TileImagesOffset(JlTuple offsetRow, JlTuple offsetCol, JlTuple row1, JlTuple col1, JlTuple row2, JlTuple col2, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1084);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, offsetRow);
		JlNativeApi.Store(proc, 1, offsetCol);
		JlNativeApi.Store(proc, 2, row1);
		JlNativeApi.Store(proc, 3, col1);
		JlNativeApi.Store(proc, 4, row2);
		JlNativeApi.Store(proc, 5, col2);
		JlNativeApi.StoreI(proc, 6, width);
		JlNativeApi.StoreI(proc, 7, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(offsetRow);
		JlNativeApi.UnpinTuple(offsetCol);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(col1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(col2);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把 `this`（图像对象集）里的每幅图按显式坐标拼贴到一张大图中，位置与裁取范围均以标量整数给定。
	/// </summary>
	/// <param name="offsetRow">该图左上角落在输出图中的行坐标（像素）。Default: 0</param>
	/// <param name="offsetCol">该图左上角落在输出图中的列坐标（像素）。Default: 0</param>
	/// <param name="row1">从该图上裁取的源区域左上角行坐标；填 -1 表示整幅拷入。Default: -1</param>
	/// <param name="col1">从该图上裁取的源区域左上角列坐标；填 -1 表示整幅拷入。Default: -1</param>
	/// <param name="row2">裁取源区域右下角行坐标（闭区间）。Default: -1</param>
	/// <param name="col2">裁取源区域右下角列坐标（闭区间）。Default: -1</param>
	/// <param name="width">输出大图宽度（像素）。Default: 512</param>
	/// <param name="height">输出大图高度（像素）。Default: 512</param>
	/// <returns>拼贴后的新图像句柄（非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本重载把 offsetRow/offsetCol 等六个定位参数按标量整数一次性写入原生侧（`StoreI`），对所有输入图共用同一组数值。原生算子 id 1084。</para>
	///   <para><b>约束或前提</b>`this` 须为含多幅图的图像对象集（iconc）才有拼贴意义，单幅图只会得到一张把该图放到指定位置的画布。源裁取参数任一为 -1 即视为整幅拷入；越界部分被丢弃，落在 width/height 之外的像素被裁掉。</para>
	///   <para><b>与相邻算子的取舍</b>当每幅图需要各自不同的落位/裁取范围时，改用 JlTuple 重载（可传入等长元组逐图给定）；本标量重载省去钉元组开销，适合整齐排布。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlImage tiled = img.TileImagesOffset(0, 0, -1, -1, -1, -1, 128, 128);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；`this` 在原生调用结束前不得 Dispose（代码末尾 GC.KeepAlive(this) 已保证）。</para>
	/// </remarks>
	public JlImage TileImagesOffset(int offsetRow, int offsetCol, int row1, int col1, int row2, int col2, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1084);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, offsetRow);
		JlNativeApi.StoreI(proc, 1, offsetCol);
		JlNativeApi.StoreI(proc, 2, row1);
		JlNativeApi.StoreI(proc, 3, col1);
		JlNativeApi.StoreI(proc, 4, row2);
		JlNativeApi.StoreI(proc, 5, col2);
		JlNativeApi.StoreI(proc, 6, width);
		JlNativeApi.StoreI(proc, 7, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把 `this`（图像对象集）里的多幅图按网格拼成一张大图。
	/// </summary>
	/// <param name="numColumns">输出网格的列数（&gt;0）。Default: 1</param>
	/// <param name="tileOrder">排列方向："vertical" 逐列填满后再进下一列，"horizontal" 逐行填满后再进下一行。Default: "vertical"</param>
	/// <returns>拼贴后的新图像句柄（非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1085。tileOrder 以 `StoreS` 写字符串，numColumns 以 `StoreI` 写整数。</para>
	///   <para><b>约束或前提</b>`this` 内的各幅图必须宽高一致，且通道数一致，否则原生侧报错。输出图尺寸 = 单图尺寸 × 网格行列数；图数不能被 numColumns 整除时末尾留空。</para>
	///   <para><b>与相邻算子的取舍</b>需要逐图自定义落位或裁取范围时改用 TileImagesOffset；本算子只做规整网格。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlImage tiled = img.TileImages(2, "horizontal");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；拼贴顺序依赖 `this` 内图像对象的既有次序，次序不稳会静默错位。</para>
	/// </remarks>
	public JlImage TileImages(int numColumns, string tileOrder)
	{
		IntPtr proc = JlNativeApi.PreCall(1085);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numColumns);
		JlNativeApi.StoreS(proc, 1, tileOrder);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把 `this`（图像对象集）中各图的所有通道拆成单通道灰度图，再按网格拼成一张大图。
	/// </summary>
	/// <param name="numColumns">输出网格的列数（&gt;0）。Default: 1</param>
	/// <param name="tileOrder">通道排列方向："vertical" 逐列，"horizontal" 逐行。Default: "vertical"</param>
	/// <returns>拼贴后的新图像句柄（非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1086。与 TileImages 的区别在于：这里被拼的是"通道"——每幅多通道图会被拆成多张单通道图后统一排布。</para>
	///   <para><b>约束或前提</b>`this` 内各图宽高必须一致。输出网格按通道总数铺放，图数不能被 numColumns 整除时末尾留空。</para>
	///   <para><b>与相邻算子的取舍</b>只想并排放整图（不拆通道）时用 TileImages；要看某彩色图各分量分布时用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage rgb = new JlImage("byte", 64, 64);
	///   JlImage tiled = rgb.TileChannels(3, "horizontal");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；通道次序沿用图像通道序（如 R、G、B），换序需先用通道重排类算子。</para>
	/// </remarks>
	public JlImage TileChannels(int numColumns, string tileOrder)
	{
		IntPtr proc = JlNativeApi.PreCall(1086);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, numColumns);
		JlNativeApi.StoreS(proc, 1, tileOrder);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把图像裁到恰好覆盖其当前 definition domain 的最小外接矩形。
	/// </summary>
	/// <returns>裁剪后的新图像句柄（尺寸可能变小，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1087。只依赖 domain，不看灰度值；domain 由先前 Threshold/ReduceDomain 等操作决定。</para>
	///   <para><b>约束或前提</b>若 domain 已是整幅图，输出与输入同尺寸（等价拷贝）。domain 为空/退化时结果尺寸随之坍缩，慎用。</para>
	///   <para><b>与相邻算子的取舍</b>想按固定坐标裁剪用 CropRectangle1/CropPart；想改尺寸重采样用 ChangeSize（若本库提供）；本算子是"跟着 domain 走"，适合处理已被非矩形区域限定过的图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage cut = img.CropDomain();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；裁剪后像素坐标原点已平移，此前记录的行/列坐标不能再套用。</para>
	/// </remarks>
	public JlImage CropDomain()
	{
		IntPtr proc = JlNativeApi.PreCall(1087);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按角点坐标从图像中裁出一个或多个矩形区域（各角点以元组逐矩形给定）。
	/// </summary>
	/// <param name="row1">各矩形左上角行坐标（像素，闭区间）。Default: 100</param>
	/// <param name="column1">各矩形左上角列坐标（像素，闭区间）。Default: 100</param>
	/// <param name="row2">各矩形右下角行坐标（须 ≥ row1）。Default: 200</param>
	/// <param name="column2">各矩形右下角列坐标（须 ≥ column1）。Default: 200</param>
	/// <returns>裁剪结果的新图像句柄；多矩形时返回图像对象集，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1088。四个元组须等长，逐个对应一个矩形；调用后逐一 UnpinTuple。</para>
	///   <para><b>约束或前提</b>所有矩形必须完全落在图像范围内（角点越界即原生报错），坐标以像素索引计、闭区间（右下角像素包含在内）。要裁"任意形状"不能靠本算子，它只处理轴对齐矩形。</para>
	///   <para><b>与相邻算子的取舍</b>已知左上角+宽高时改用 CropPart（免去 row2/col2 换算）；跟着 domain 走用 CropDomain。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple r1 = 10, c1 = 10, r2 = 50, c2 = 50;
	///   JlImage cut = img.CropRectangle1(r1, c1, r2, c2);
	///   </code>
	///   <para><b>资源与坑</b>返回句柄（或对象集）需释放；每个输出矩形自带新的局部坐标原点。</para>
	/// </remarks>
	public JlImage CropRectangle1(JlTuple row1, JlTuple column1, JlTuple row2, JlTuple column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1088);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row1);
		JlNativeApi.Store(proc, 1, column1);
		JlNativeApi.Store(proc, 2, row2);
		JlNativeApi.Store(proc, 3, column2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(column1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(column2);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按角点坐标从图像中裁出一个矩形区域（标量整数重载）。
	/// </summary>
	/// <param name="row1">矩形左上角行坐标（像素，闭区间）。Default: 100</param>
	/// <param name="column1">矩形左上角列坐标（像素，闭区间）。Default: 100</param>
	/// <param name="row2">矩形右下角行坐标（须 ≥ row1）。Default: 200</param>
	/// <param name="column2">矩形右下角列坐标（须 ≥ column1）。Default: 200</param>
	/// <returns>裁剪后的新图像句柄（单幅，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1088，与元组重载同一算子；本重载以 `StoreI` 直写单矩形，无钉元组开销。</para>
	///   <para><b>约束或前提</b>矩形必须完全落在图像范围内且 row2≥row1、column2≥column1，坐标为闭区间的像素索引。只裁单矩形时用本重载，多矩形用元组重载。</para>
	///   <para><b>与相邻算子的取舍</b>已知宽高改用 CropPart；跟随 domain 用 CropDomain。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage cut = img.CropRectangle1(10, 10, 50, 50);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；输出图自带局部坐标原点。</para>
	/// </remarks>
	public JlImage CropRectangle1(int row1, int column1, int row2, int column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1088);
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
	///   按左上角+宽高从图像中裁出一个或多个矩形区域（各参数以元组逐区域给定）。
	/// </summary>
	/// <param name="row">各矩形左上角行坐标（像素）。Default: 100</param>
	/// <param name="column">各矩形左上角列坐标（像素）。Default: 100</param>
	/// <param name="width">各矩形宽度（像素，&gt;0）。Default: 128</param>
	/// <param name="height">各矩形高度（像素，&gt;0）。Default: 128</param>
	/// <returns>裁剪结果的新图像句柄；多区域时返回图像对象集，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1089。四个元组须等长，逐一对应一个裁剪窗口；调用后逐一 UnpinTuple。</para>
	///   <para><b>约束或前提</b>窗口须整体落在图像内：row+height-1、column+width-1 不得越过图像下边界，否则原生报错。尺寸以像素计。</para>
	///   <para><b>与相邻算子的取舍</b>习惯用右下角坐标时用 CropRectangle1；本重载适合"原点+尺寸"的表达。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple r = 10, c = 10, w = 41, h = 41;
	///   JlImage cut = img.CropPart(r, c, w, h);
	///   </code>
	///   <para><b>资源与坑</b>返回句柄/对象集需释放；每个输出自带局部坐标原点。</para>
	/// </remarks>
	public JlImage CropPart(JlTuple row, JlTuple column, JlTuple width, JlTuple height)
	{
		IntPtr proc = JlNativeApi.PreCall(1089);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, width);
		JlNativeApi.Store(proc, 3, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(width);
		JlNativeApi.UnpinTuple(height);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按左上角+宽高从图像中裁出一个矩形区域（标量整数重载）。
	/// </summary>
	/// <param name="row">矩形左上角行坐标（像素）。Default: 100</param>
	/// <param name="column">矩形左上角列坐标（像素）。Default: 100</param>
	/// <param name="width">矩形宽度（像素，&gt;0）。Default: 128</param>
	/// <param name="height">矩形高度（像素，&gt;0）。Default: 128</param>
	/// <returns>裁剪后的新图像句柄（单幅，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1089，与元组重载同一算子；本重载以 `StoreI` 直写单窗口，无钉元组开销。</para>
	///   <para><b>约束或前提</b>窗口须整体落在图像内（row+height-1、column+width-1 不越界）。裁单个区域时用本重载。[待实测]</para>
	///   <para><b>与相邻算子的取舍</b>用右下角坐标表达时改用 CropRectangle1。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage cut = img.CropPart(10, 10, 41, 41);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；输出自带局部坐标原点。</para>
	/// </remarks>
	public JlImage CropPart(int row, int column, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1089);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, row);
		JlNativeApi.StoreI(proc, 1, column);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把图像改成指定的 width×height：变大则补零边，变小则裁掉多余行列（不做插值重采样）。
	/// </summary>
	/// <param name="width">目标宽度（像素）。Default: 512</param>
	/// <param name="height">目标高度（像素）。Default: 512</param>
	/// <returns>新尺寸图像句柄（非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1090。它只做"裁剪或填充"，像素一一对应、绝不缩放内容。</para>
	///   <para><b>约束或前提</b>目标尺寸可大于或小于原图；补出来的区域灰度为 0。宽高为整数像素。</para>
	///   <para><b>与相邻算子的取舍</b>若要把内容按倍率缩放（真缩放像素网格）不能用本算子，需改用缩放/仿射类算子；本算子只用于凑齐统一尺寸或补边。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 300, 300);
	///   JlImage resized = img.ChangeFormat(512, 512);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；补零边会拉低整图统计量（如灰度均值），做特征前先想清楚。</para>
	/// </remarks>
	public JlImage ChangeFormat(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1090);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用给定的区域整体替换图像的 definition domain（不做与旧 domain 的交集）。
	/// </summary>
	/// <param name="newDomain">作为新 domain 的区域。</param>
	/// <returns>携带新 domain 的图像句柄（像素尺寸不变，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1091。图像宽高与像素数据保持不变，只换掉"哪些像素算有效"。newDomain 以 `Store` 作为句柄参数传入。</para>
	///   <para><b>约束或前提</b>newDomain 与图像须同一坐标系；区域超出图像范围的部分无效。想"在旧 domain 基础上再收窄"应用 ReduceDomain（取交集），本算子会丢弃旧 domain。</para>
	///   <para><b>与相邻算子的取舍</b>ReduceDomain=交集、ChangeDomain=替换、FullDomain=恢复全幅——按是否需要保留旧限制选。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlRegion dom = new JlRegion(10.0, 10.0, 100.0, 100.0);
	///   JlImage restricted = img.ChangeDomain(dom);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄与传入区域在原生调用结束前均不得释放（GC.KeepAlive 已保 this 与 newDomain）。</para>
	/// </remarks>
	public JlImage ChangeDomain(JlRegion newDomain)
	{
		IntPtr proc = JlNativeApi.PreCall(1091);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, newDomain);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(newDomain);
		return obj;
	}

	/// <summary>
	///   把图像的 domain 收窄到与指定矩形的交集（图像像素尺寸不变，仅有效区缩小）。
	/// </summary>
	/// <param name="row1">矩形左上角行坐标（像素，闭区间）。Default: 100</param>
	/// <param name="column1">矩形左上角列坐标（像素，闭区间）。Default: 100</param>
	/// <param name="row2">矩形右下角行坐标。Default: 200</param>
	/// <param name="column2">矩形右下角列坐标。Default: 200</param>
	/// <returns>domain 收窄后的图像句柄（尺寸不变，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1093。等价于用矩形区域做一次 ReduceDomain：原 domain ∩ 矩形。</para>
	///   <para><b>约束或前提</b>矩形坐标以闭区间像素索引计。与 CropRectangle1 不同，本算子不裁掉矩形外的像素数据，只把它们标记为 domain 之外——宽高保持原样。</para>
	///   <para><b>与相邻算子的取舍</b>想真正改变图像尺寸去 CropRectangle1；只想限制后续统计/滤波作用范围用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage limited = img.Rectangle1Domain(20, 20, 120, 120);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；后续 Intensity/算子只在 domain 内统计。</para>
	/// </remarks>
	public JlImage Rectangle1Domain(int row1, int column1, int row2, int column2)
	{
		IntPtr proc = JlNativeApi.PreCall(1093);
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
	///   把图像的 domain 收窄到与给定区域的交集（尺寸不变，仅有效区缩小）。
	/// </summary>
	/// <param name="region">用于收窄的新 domain 区域。</param>
	/// <returns>domain 收窄后的图像句柄（尺寸不变，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1094。结果是"旧 domain ∩ region"，与 ChangeDomain 的"直接替换"相对。</para>
	///   <para><b>约束或前提</b>region 与图像同一坐标系；超出图像范围或落在旧 domain 外的部分被排除。多次 ReduceDomain 逐次累积收窄。</para>
	///   <para><b>与相邻算子的取舍</b>要整体替换 domain 用 ChangeDomain；要真正裁掉像素改变尺寸用 CropDomain/CropRectangle1；本算子只做交集且保留原尺寸。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlRegion keep = new JlRegion(20.0, 20.0, 120.0, 120.0);
	///   JlImage limited = img.ReduceDomain(keep);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄与传入区域在原生调用结束前不得释放（GC.KeepAlive 已保 this 与 region）。</para>
	/// </remarks>
	public JlImage ReduceDomain(JlRegion region)
	{
		IntPtr proc = JlNativeApi.PreCall(1094);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>
	///   把图像的 domain 扩张到整幅（清除之前 ReduceDomain/Threshold 造成的有效区限制）。
	/// </summary>
	/// <returns>domain 恢复为全幅的图像句柄（尺寸不变，非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1095。之后所有算子都会在全部像素上生效。</para>
	///   <para><b>约束或前提</b>不会恢复被 CropDomain 真正裁掉的像素——那只改了图像尺寸，不可逆；本算子只放开 domain。</para>
	///   <para><b>与相邻算子的取舍</b>想反向操作（收窄）用 ReduceDomain/Rectangle1Domain；想以当前 domain 外接矩形裁小图像用 CropDomain。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage full = img.FullDomain();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage FullDomain()
	{
		IntPtr proc = JlNativeApi.PreCall(1095);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   以区域形式取出图像当前的 definition domain。
	/// </summary>
	/// <returns>表示当前 domain 的新 JlRegion 句柄（非原地改写），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1096，经 JlRegion.LoadNew 装载为区域句柄。未收窄过的图，其 domain 即整幅矩形。</para>
	///   <para><b>约束或前提</b>返回的是 domain（有效像素集合），不是阈值分割结果，也不是灰度值。</para>
	///   <para><b>与相邻算子的取舍</b>想按 domain 外接矩形裁图用 CropDomain；想直接拿分割结果用 Threshold。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlRegion dom = img.GetDomain();
	///   </code>
	///   <para><b>资源与坑</b>返回新区域句柄需释放；它是快照，之后再改图像 domain 不会影响已取出的区域。</para>
	/// </remarks>
	public JlRegion GetDomain()
	{
		IntPtr proc = JlNativeApi.PreCall(1096);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>
	///   在图像中定位矫正网格（rectification grid）区域，参数以元组给定。
	/// </summary>
	/// <param name="minContrast">识别网格线所需的最小对比度（灰度级）。Default: 8.0</param>
	/// <param name="radius">所用圆形结构元的半径（像素）。Default: 7.5</param>
	/// <returns>包含网格区域的新 JlRegion 句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1104。用于标定/畸变矫正流程中第一步找出规则网格，供后续 FindGrid/CropGrid 类算子使用。</para>
	///   <para><b>约束或前提</b>输入应为含规则网格图案的单通道灰度图；minContrast 过大找不到、过小易把噪声当网格。radius 应匹配网格线粗细。</para>
	///   <para><b>与相邻算子的取舍</b>本算子只给"网格在哪"，逐点连接与映射由 ConnectGridPoints/GenGridRectificationMap 完成。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlTuple contrast = 8.0, rad = 7.5;
	///   JlRegion grid = img.FindRectificationGrid(contrast, rad);
	///   </code>
	///   <para><b>资源与坑</b>返回新区域句柄需释放；元组重载调用后逐一 UnpinTuple。</para>
	/// </remarks>
	public JlRegion FindRectificationGrid(JlTuple minContrast, JlTuple radius)
	{
		IntPtr proc = JlNativeApi.PreCall(1104);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, minContrast);
		JlNativeApi.Store(proc, 1, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minContrast);
		JlNativeApi.UnpinTuple(radius);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   在图像中定位矫正网格（rectification grid）区域，参数以标量给定。
	/// </summary>
	/// <param name="minContrast">识别网格线所需的最小对比度（灰度级）。Default: 8.0</param>
	/// <param name="radius">所用圆形结构元的半径（像素）。Default: 7.5</param>
	/// <returns>包含网格区域的新 JlRegion 句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1104，与元组重载同一算子；本重载以 `StoreD` 直写双精度，无钉元组开销。</para>
	///   <para><b>约束或前提</b>输入应为含规则网格图案的单通道灰度图。minContrast 过大漏检、过小误检噪声；radius 应匹配线宽。</para>
	///   <para><b>与相邻算子的取舍</b>需要逐网格不同参数时改用元组重载；本算子只做单组参数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlRegion grid = img.FindRectificationGrid(8.0, 7.5);
	///   </code>
	///   <para><b>资源与坑</b>返回新区域句柄需释放。</para>
	/// </remarks>
	public JlRegion FindRectificationGrid(double minContrast, double radius)
	{
		IntPtr proc = JlNativeApi.PreCall(1104);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, minContrast);
		JlNativeApi.StoreD(proc, 1, radius);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把矫正网格的网格点连成轮廓线（各点坐标与参数以元组给定），返回 XLD 轮廓。
	/// </summary>
	/// <param name="row">各网格点的行坐标。</param>
	/// <param name="column">各网格点的列坐标。</param>
	/// <param name="sigma">所用高斯核的宽度（越大连接越保守）。Default: 0.9</param>
	/// <param name="maxDist">连线相对网格点的最大偏离距离（像素）。Default: 5.5</param>
	/// <returns>连接线的新 JlXLD 句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1106。row/column 通常来自网格点提取算子的输出，二者长度必须一致。</para>
	///   <para><b>约束或前提</b>点集须构成规则网格拓扑；sigma/maxDist 共同决定哪些相邻点被连起来，配错会漏连或错连。</para>
	///   <para><b>与相邻算子的取舍</b>本算子产出"连线"，是 GenGridRectificationMap 的输入；不做映射本身。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlTuple row = new JlTuple(10, 20), col = new JlTuple(10, 20);
	///   JlTuple sigma = 0.9, maxDist = 5.5;
	///   JlXLD lines = img.ConnectGridPoints(row, col, sigma, maxDist);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；元组重载调用后逐一 UnpinTuple。JlTuple 构造签名以本仓库实际为准 [待实测]。</para>
	/// </remarks>
	public JlXLD ConnectGridPoints(JlTuple row, JlTuple column, JlTuple sigma, JlTuple maxDist)
	{
		IntPtr proc = JlNativeApi.PreCall(1106);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, sigma);
		JlNativeApi.Store(proc, 3, maxDist);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(sigma);
		JlNativeApi.UnpinTuple(maxDist);
		err = JlXLD.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把矫正网格的网格点连成轮廓线，sigma 以整数、maxDist 以标量给定。
	/// </summary>
	/// <param name="row">各网格点的行坐标。</param>
	/// <param name="column">各网格点的列坐标。</param>
	/// <param name="sigma">所用高斯核宽度（此重载取整，Default: 0.9 会被截为 0）。</param>
	/// <param name="maxDist">连线相对网格点的最大偏离距离（像素）。Default: 5.5</param>
	/// <returns>连接线的新 JlXLD 句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1106，与元组重载同一算子；row/column 仍以 `Store` 钉元组，sigma 用 `StoreI`、maxDist 用 `StoreD` 直写。</para>
	///   <para><b>约束或前提</b>sigma 是整数形参，无法表达 0.9 这类小数默认值——要精确控制核宽请改用全元组重载。</para>
	///   <para><b>与相邻算子的取舍</b>需要小数 sigma 时用 <see cref="ConnectGridPoints(JlTuple,JlTuple,JlTuple,JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlTuple row = new JlTuple(10, 20), col = new JlTuple(10, 20);
	///   JlXLD lines = img.ConnectGridPoints(row, col, 1, 5.5);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；row/column 逐一 UnpinTuple。</para>
	/// </remarks>
	public JlXLD ConnectGridPoints(JlTuple row, JlTuple column, int sigma, double maxDist)
	{
		IntPtr proc = JlNativeApi.PreCall(1106);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.StoreI(proc, 2, sigma);
		JlNativeApi.StoreD(proc, 3, maxDist);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		err = JlXLD.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   依据规则网格点计算畸变图与矫正图之间的映射，rotation 以元组给定。
	/// </summary>
	/// <param name="connectingLines">网格点连线轮廓（ConnectGridPoints 的输出）。</param>
	/// <param name="meshes">输出的网格单元轮廓。</param>
	/// <param name="gridSpacing">矫正图中网格点间距（像素）；填 0 表示由算子自动推断。Default: 0</param>
	/// <param name="rotation">点网格的旋转，以元组给定。Default: "auto"</param>
	/// <param name="row">网格点行坐标。</param>
	/// <param name="column">网格点列坐标。</param>
	/// <param name="mapType">映射类型："bilinear"/"inverse_affine"/"linear_trans"。Default: "bilinear"</param>
	/// <returns>承载映射数据的新图像句柄（供网格矫正类映射应用），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1107。返回两样东西：函数返回 JlImage（映射图），并通过 out 参数返回 meshes（JlXLD）。</para>
	///   <para><b>约束或前提</b>connectingLines/row/column 必须来自同一网格检测结果且相互一致；gridSpacing=0 才走自动推断。本库不提供 3D/显示族，此映射仅用于图像域的网格矫正。</para>
	///   <para><b>与相邻算子的取舍</b>rotation 想用字符串常量（如 "auto"）改用 <see cref="GenGridRectificationMap(JlXLD,out JlXLD,int,string,JlTuple,JlTuple,string)"/>；本重载支持逐点数值旋转。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlTuple row = new JlTuple(10, 20), col = new JlTuple(10, 20);
	///   JlXLD lines = img.ConnectGridPoints(row, col, 1, 5.5);
	///   JlTuple rot = "auto";
	///   JlImage map = img.GenGridRectificationMap(lines, out JlXLD meshes, 0, rot, row, col, "bilinear");
	///   </code>
	///   <para><b>资源与坑</b>返回的映射图与 out meshes 都是新句柄，均需释放；connectingLines 由调用方持有至调用结束（GC.KeepAlive 已保）。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlXLD connectingLines, out JlXLD meshes, int gridSpacing, JlTuple rotation, JlTuple row, JlTuple column, string mapType)
	{
		IntPtr proc = JlNativeApi.PreCall(1107);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, connectingLines);
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
		err = LoadNew(proc, 1, err, out var obj);
		err = JlXLD.LoadNew(proc, 2, err, out meshes);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(connectingLines);
		return obj;
	}

	/// <summary>
	///   依据规则网格点计算畸变图与矫正图之间的映射，rotation 以字符串常量给定。
	/// </summary>
	/// <param name="connectingLines">网格点连线轮廓（ConnectGridPoints 的输出）。</param>
	/// <param name="meshes">输出的网格单元轮廓。</param>
	/// <param name="gridSpacing">矫正图中网格点间距（像素）；填 0 表示自动推断。Default: 0</param>
	/// <param name="rotation">点网格旋转："auto" 自动判定。Default: "auto"</param>
	/// <param name="row">网格点行坐标。</param>
	/// <param name="column">网格点列坐标。</param>
	/// <param name="mapType">映射类型："bilinear"/"inverse_affine"/"linear_trans"。Default: "bilinear"</param>
	/// <returns>承载映射数据的新图像句柄（供网格矫正类映射应用），用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1107，与元组重载同一算子；本重载 rotation 以 `StoreS` 写字符串。返回 JlImage 映射图，out 参数 meshes 为 JlXLD。</para>
	///   <para><b>约束或前提</b>connectingLines/row/column 须来自同一网格检测且相互一致；gridSpacing=0 才走自动推断。</para>
	///   <para><b>与相邻算子的取舍</b>需要逐点数值旋转时改用 <see cref="GenGridRectificationMap(JlXLD,out JlXLD,int,JlTuple,JlTuple,JlTuple,string)"/>；本重载适合用 "auto" 一把梭。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 512, 512);
	///   JlTuple row = new JlTuple(10, 20), col = new JlTuple(10, 20);
	///   JlXLD lines = img.ConnectGridPoints(row, col, 1, 5.5);
	///   JlImage map = img.GenGridRectificationMap(lines, out JlXLD meshes, 0, "auto", row, col, "bilinear");
	///   </code>
	///   <para><b>资源与坑</b>映射图与 out meshes 均为新句柄需释放；connectingLines 保持到调用结束。</para>
	/// </remarks>
	public JlImage GenGridRectificationMap(JlXLD connectingLines, out JlXLD meshes, int gridSpacing, string rotation, JlTuple row, JlTuple column, string mapType)
	{
		IntPtr proc = JlNativeApi.PreCall(1107);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, connectingLines);
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
		err = LoadNew(proc, 1, err, out var obj);
		err = JlXLD.LoadNew(proc, 2, err, out meshes);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(connectingLines);
		return obj;
	}

	/// <summary>
	///   计算每个矩形窗口内灰度的标准差，生成局部标准差图。
	/// </summary>
	/// <param name="width">计算标准差的窗口宽（像素）。Default: 11</param>
	/// <param name="height">计算标准差的窗口高（像素）。Default: 11</param>
	/// <returns>承载各窗标准差的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1342。输出像素是"邻域纹理/噪声强度"，不是灰度本身；窗越大越平滑、越偏低频结构。</para>
	///   <para><b>约束或前提</b>输入应为单通道图。输出数值范围可能超过原图类型位深，用于二次 Threshold 时注意量纲。</para>
	///   <para><b>与相邻算子的取舍</b>要看"平均亮度"用 MeanImage；要看"信息量/混乱度"用 EntropyImage；标准差更擅长定位纹理边界。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage dev = img.DeviationImage(11, 11);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；结果在图像四缘受窗口越界处理影响。</para>
	/// </remarks>
	public JlImage DeviationImage(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1342);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   计算矩形窗口内灰度分布的信息熵，生成局部熵图。
	/// </summary>
	/// <param name="width">计算熵的窗口宽（像素）。Default: 9</param>
	/// <param name="height">计算熵的窗口高（像素）。Default: 9</param>
	/// <returns>承载各窗灰度熵的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1343。熵高代表窗内灰度分布杂乱（纹理/边缘），熵低代表平坦。</para>
	///   <para><b>约束或前提</b>单通道输入。窗尺寸决定统计的灰度直方图跨度，窗太小估计噪声大。</para>
	///   <para><b>与相邻算子的取舍</b>只要区分"平坦 vs 复杂"且对纹理方向不敏感时，熵比标准差更聚焦分布形态；两者常互为备选。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage ent = img.EntropyImage(9, 9);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；输出为统计量，非 0-255 直观灰度。</para>
	/// </remarks>
	public JlImage EntropyImage(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1343);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   对图像做各向同性扩散（等价于反复高斯平滑），不区分边缘方向。
	/// </summary>
	/// <param name="sigma">高斯分布的标准差（越大单次平滑越强）。Default: 1.0</param>
	/// <param name="iterations">扩散迭代次数（越多累计平滑越强）。Default: 10</param>
	/// <returns>平滑后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1344。sigma 用 `StoreD`、iterations 用 `StoreI` 写入。</para>
	///   <para><b>约束或前提</b>各向同性意味着边缘也会被抹平（不像各向异性扩散保边）。总平滑强度大致随 sigma×iterations 增长。</para>
	///   <para><b>与相邻算子的取舍</b>需要保边去噪改用各向异性/非局部扩散类算子；只想去噪又不介意糊边时本算子简单可用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage smooth = img.IsotropicDiffusion(1.0, 10);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；iterations 过大耗时上升且细节全丢。</para>
	/// </remarks>
	public JlImage IsotropicDiffusion(double sigma, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(1344);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreI(proc, 1, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}


	/// <summary>
	///   用指定滤波器对图像做平滑。
	/// </summary>
	/// <param name="filter">滤波器类型："deriche2"/"gauss"/"binomial"/"mean"。Default: "deriche2"</param>
	/// <param name="alpha">滤波参数：deriche2/binomial 下越小平滑越强；但 gauss 下语义相反（越小越弱）。Default: 0.5</param>
	/// <returns>平滑后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1346。filter 以 `StoreS` 写字符串，alpha 以 `StoreD` 写。</para>
	///   <para><b>约束或前提</b>alpha 的调参方向依赖 filter：切到 "gauss" 时别沿用 "deriche2" 的直觉，否则平滑力度会事与愿违。</para>
	///   <para><b>与相邻算子的取舍</b>deriche 类便于后续求导/边缘，gauss 是通用低通；只想快速去椒盐点用中值类而非本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage sm = img.SmoothImage("gauss", 1.5);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage SmoothImage(string filter, double alpha)
	{
		IntPtr proc = JlNativeApi.PreCall(1346);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用 sigma 滤波器做非线性平滑：只把与窗均值偏差不超过 sigma 的像素纳入平均。
	/// </summary>
	/// <param name="maskHeight">滤波掩膜的高度（行数）。Default: 5</param>
	/// <param name="maskWidth">滤波掩膜的宽度（列数）。Default: 5</param>
	/// <param name="sigma">允许并入平均的最大灰度偏差（灰度级）。Default: 3</param>
	/// <returns>平滑后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1347。形参序是"先高后宽"（maskHeight 在前），与直觉的宽×高相反，别写反。</para>
	///   <para><b>约束或前提</b>sigma 小则抗噪弱但保边强，大则接近普通均值滤波。与 GaussFilter 相比它能把椒盐离群点排除在均值外。</para>
	///   <para><b>与相邻算子的取舍</b>要更强保边去噪用中值/截尾均值；只要各向同性模糊用高斯/SmoothImage。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage sm = img.SigmaImage(5, 5, 3);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；三参数均为整数，掩膜尺寸直接决定成本。</para>
	/// </remarks>
	public JlImage SigmaImage(int maskHeight, int maskWidth, int sigma)
	{
		IntPtr proc = JlNativeApi.PreCall(1347);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskHeight);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.StoreI(proc, 2, sigma);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   取掩膜内最大值与最小值的平均（中值程滤波），margin 以元组给定。
	/// </summary>
	/// <param name="mask">其区域作为滤波掩膜的图像。</param>
	/// <param name="margin">边界处理方式，以元组传入。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1348。输出 = (窗内最大 + 窗内最小)/2，介于最小/最大之间，能压掉极端噪声同时较好保留台阶边缘。</para>
	///   <para><b>约束或前提</b>mask 是 iconc 区域输入（`Store(proc,2,mask)`），其形状决定滤波邻域；margin 元组重载以 `Store`+`UnpinTuple` 处理。</para>
	///   <para><b>与相邻算子的取舍</b>要更稳的抗椒盐用中值/截尾均值；要普通模糊用均值/高斯。中值程对"平台+细线"结构边缘位移小。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlRegion mask = new JlRegion(0.0, 0.0, 3.0, 3.0);
	///   JlTuple margin = "mirrored";
	///   JlImage outImg = img.MidrangeImage(mask, margin);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；掩膜区域由调用方释放。</para>
	/// </remarks>
	public JlImage MidrangeImage(JlRegion mask, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1348);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.Store(proc, 0, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}

	/// <summary>
	///   取掩膜内最大值与最小值的平均（中值程滤波），margin 以字符串给定。
	/// </summary>
	/// <param name="mask">其区域作为滤波掩膜的图像。</param>
	/// <param name="margin">边界处理方式："mirrored"/"reduced"/"bound"/"cyclic"。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1348，与元组重载同一算子；本重载 margin 以 `StoreS` 写字符串，无钉元组开销。</para>
	///   <para><b>约束或前提</b>mask 是 iconc 区域输入。margin 决定图像四缘如何延拓，"reduced" 会缩短有效域。</para>
	///   <para><b>与相邻算子的取舍</b>单值 margin 用本重载；需要元组化传参才用 <see cref="MidrangeImage(JlRegion,JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlRegion mask = new JlRegion(0.0, 0.0, 3.0, 3.0);
	///   JlImage outImg = img.MidrangeImage(mask, "mirrored");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；掩膜区域由调用方释放。</para>
	/// </remarks>
	public JlImage MidrangeImage(JlRegion mask, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1348);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.StoreS(proc, 0, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}

	/// <summary>截尾均值滤波：掩膜内排序后取中间若干像素求平均，原生算子 id 1349，margin 以元组传入。</summary>
	/// <param name="mask">其区域作为滤波掩膜的图像。</param>
	/// <param name="number">参与平均的像素个数。典型值是掩膜面积的一半。Default: 5</param>
	/// <param name="margin">边界处理方式。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>掩膜是 iconc 区域输入（<c>Store(proc, 2, mask)</c>），窗内灰度排序后只把<b>中间</b>
	///   <paramref name="number"/> 个像素求平均，两端极端值全部截掉。定位介于 <see cref="MedianImage(string,int,JlTuple)"/>
	///   与 <see cref="MeanImage(int,int)"/> 之间：和均值滤波相比对离群点免疫（亮点/暗点进不了平均），
	///   和中值滤波相比平坦区输出的是平均值而不是"择一"的某个像素值，大窗下不会出现中值特有的块状纹理退化，
	///   同时边缘位置像中值一样基本不漂移。</para>
	///   <para><b>关键约束</b><paramref name="number"/> 不得超过掩膜像素数——上限由 <paramref name="mask"/> 面积决定，
	///   换掩膜必须重算该值 [待实测：越界行为]。它是 <c>StoreI</c> 的 <c>int</c> 像素计数，不是百分比，
	///   也不能表达"截尾比例"这类语义；<paramref name="number"/> 等于掩膜面积时退化为普通均值（截不到任何端）。</para>
	///   <para><b>与相邻算子的取舍</b>只去椒盐点、平坦区粗糙无所谓 → <see cref="MedianImage(string,int,JlTuple)"/>（更快）；
	///   纯平滑、噪声不极端 → <see cref="MeanImage(int,int)"/>；传感器坏点/灰尘亮斑会污染 <c>Intensity</c> 类量测 →
	///   本算子。截尾均值比两者都贵，大图先 <c>ReduceDomain</c> 限定处理域。</para>
	///   <para><b>参数取向</b><paramref name="margin"/> 元组版 <c>Store</c>+<c>UnpinTuple</c>，多值语义本层未体现 [待实测]；
	///   单值用 <see cref="TrimmedMean(JlRegion,int,string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion mask = new JlRegion(0.0, 0.0, 3.0, 3.0);   // 4x4 共 16 像素
	///   using JlImage tm = img.TrimmedMean(mask, 8, new JlTuple("mirrored"));
	///   </code>
	///   <para><b>资源与坑</b>输出新图像需释放；掩膜区域同样由调用方释放；边界环带的 "mirrored" 延拓只影响四周一圈，
	///   但量测 ROI 贴图像边缘时该环带误差会直接进入统计值。</para>
	/// </remarks>
	public JlImage TrimmedMean(JlRegion mask, int number, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1349);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.StoreI(proc, 0, number);
		JlNativeApi.Store(proc, 1, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}

	/// <summary>截尾均值滤波（margin 以字符串传入）。</summary>
	/// <param name="mask">滤波掩膜区域。</param>
	/// <param name="number">参与平均的像素个数。Default: 5</param>
	/// <param name="margin">边界处理。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para>"掩膜内排序、平均中间 number 个"的机制与 number/掩膜面积的约束见
	///   <see cref="TrimmedMean(JlRegion,int,JlTuple)"/>：同一原生 id 1349，本版本 <c>StoreS</c> 直写
	///   <paramref name="margin"/>，无固定/解固定，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion mask = new JlRegion(0.0, 0.0, 3.0, 3.0);
	///   using JlImage tm = img.TrimmedMean(mask, 8, "mirrored");
	///   </code>
	/// </remarks>
	public JlImage TrimmedMean(JlRegion mask, int number, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1349);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.StoreI(proc, 0, number);
		JlNativeApi.StoreS(proc, 1, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}

	/// <summary>可分离中值滤波：横竖两次一维中值近似大矩形窗中值，原生算子 id 1350，margin 以元组传入。</summary>
	/// <param name="maskWidth">秩掩膜宽度，单位是像素。Default: 25</param>
	/// <param name="maskHeight">秩掩膜高度，单位是像素。Default: 25</param>
	/// <param name="margin">边界处理方式。Default: "mirrored"</param>
	/// <returns>中值滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>先以 <c>maskWidth×1</c> 的水平窗做一次一维中值，再以 <c>1×maskHeight</c> 的竖直窗做一次，
	///   用两次一维运算逼近 <see cref="MedianImage(string,int,string)"/> 在 <c>maskWidth×maskHeight</c> 矩形窗上的严格中值
	///   （两个尺寸都是 <c>StoreI</c> 的 <c>int</c>，单位是像素）。默认 25×25：这种大窗全中值逐窗排序极其慢，
	///   分离方案把排序规模从 625 降到 25+25 [待实测：实际加速幅度]。</para>
	///   <para><b>与 MedianImage 的取舍</b>近似是有代价的：两遍滤波去除的像素集合不同，结果<b>不是任何窗的真中值</b>——
	///   对角/斜向细线经过两遍后比全中值更容易被削断，接近 min/max 组合；而水平、垂直边缘与条纹噪声保留得好。
	///   小窗（半径 ≤4 像素量级）直接用 <c>MedianImage</c>/<c>MedianRect</c>，没必要分离；
	///   大窗压低频噪声或条纹背景才用本算子，且结构以横平竖直为主。</para>
	///   <para><b>参数取向</b><paramref name="margin"/> 元组版 <c>Store</c>+<c>UnpinTuple</c>，多值语义 [待实测]，单值用
	///   <see cref="MedianSeparate(int,int,string)"/>；窗宽高的偶数与 ≤0 行为 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage sep = img.MedianSeparate(15, 15, new JlTuple("mirrored"));
	///   </code>
	///   <para><b>资源与坑</b>输出新图像需释放；多通道输入是否逐通道独立滤波 [待实测]。
	///   两次滤波叠加的偏移会把细结构削得比预期多，标定滤波强度时用真图上的最细结构试。</para>
	/// </remarks>
	public JlImage MedianSeparate(int maskWidth, int maskHeight, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1350);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.Store(proc, 2, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>可分离中值滤波（margin 以字符串传入）。</summary>
	/// <param name="maskWidth">秩掩膜宽度，单位是像素。Default: 25</param>
	/// <param name="maskHeight">秩掩膜高度，单位是像素。Default: 25</param>
	/// <param name="margin">边界处理。Default: "mirrored"</param>
	/// <returns>中值滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para>两遍一维中值的近似性质与和 <c>MedianImage</c> 的取舍见
	///   <see cref="MedianSeparate(int,int,JlTuple)"/>：同一原生 id 1350，本版本 <c>StoreS</c> 直写
	///   <paramref name="margin"/>，无固定/解固定，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage sep = img.MedianSeparate(25, 25, "mirrored");
	///   </code>
	/// </remarks>
	public JlImage MedianSeparate(int maskWidth, int maskHeight, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1350);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreS(proc, 2, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用矩形掩膜做严格中值滤波，原生算子 id 1351。
	/// </summary>
	/// <param name="maskWidth">矩形掩膜的宽（像素）。Default: 15</param>
	/// <param name="maskHeight">矩形掩膜的高（像素）。Default: 15</param>
	/// <returns>中值滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对 maskWidth×maskHeight 矩形窗内全部像素排序取中值，去除小于掩膜的孤立亮/暗点，阶跃边缘位置基本不动。</para>
	///   <para><b>约束或前提</b>本算子无边界参数，四缘环带处理由原生决定 [待实测]。大窗（如 15×15=225 像素排序）很慢。</para>
	///   <para><b>与相邻算子的取舍</b>要圆形/自定义掩膜或可控边界用 <see cref="MedianImage(string,int,string)"/>；只想横竖两遍快速近似用 <see cref="MedianSeparate(int,int,string)"/>（非严格中值但更快）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage med = img.MedianRect(5, 5);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；密集高对比纹理会被中值"择一"，之后别再做方向量测。</para>
	/// </remarks>
	public JlImage MedianRect(int maskWidth, int maskHeight)
	{
		IntPtr proc = JlNativeApi.PreCall(1351);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>中值滤波（可选掩膜形状），边界处理以元组传入。</summary>
	/// <param name="maskType">掩膜类型。Default: "circle"</param>
	/// <param name="radius">掩膜半径。Default: 1</param>
	/// <param name="margin">边界处理方式。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1352。半径 <paramref name="radius"/> 的掩膜内取中值：
	///   椒盐点（面积小于掩膜一半的孤立亮/暗点）被直接替换掉，而<b>阶跃边缘的位置基本不动</b>——
	///   这是它相对 <see cref="MeanImage(int,int)"/> 的核心优势：均值滤波会把边缘线性拖糊，
	///   紧接着做 <c>Threshold</c> 时边缘像素会被"半收"，宽度测量随之漂移。</para>
	///   <para><b>尺寸怎么定</b>中值滤波只能去掉<b>小于掩膜</b>的结构：<paramref name="radius"/>=1 的圆掩膜覆盖 5 像素，
	///   只能去单点噪声；掩膜再大就开始吃掉细线、窄脊（例如字符笔画），表现为断笔。
	///   <see cref="MedianSeparate(int,int,string)"/> 用横竖两次小掩膜代替大掩膜，能保住线状结构，代价是不再是严格中值。</para>
	///   <para><b>边界</b><paramref name="margin"/> 是本族少见的显式边界参数：默认 "mirrored"（镜像延拓），
	///   其余可取值由原生决定 [待实测]。它只影响图像四周 <paramref name="radius"/> 宽的环带，
	///   但该环带内的灰度会系统性偏离中心统计值——量测 ROI 贴到图像边缘时误差会体现在 <c>Intensity</c> 上。</para>
	///   <para><b>与相邻算子的取舍</b>要压亮毛刺而保留趋势 → <see cref="GrayOpeningRect(int,int)"/>；
	///   要按窗内第 k 小取值（比中值更极端）→ <see cref="RankImage(JlRegion,int,string)"/>/<see cref="RankRect(int,int,int)"/>；
	///   要各向同性平滑、不在乎边缘位置 → <see cref="GaussImage(int)"/>。中值滤波在<b>密集高对比纹理</b>上会把纹理"择一"，
	///   纹理方向信息丢失，此时不要用中值预处理再做方向量测。</para>
	///   <para><b>参数取向</b><paramref name="margin"/> 接受元组，多值语义本层无法判断 [待实测]；
	///   单值请用 <see cref="MedianImage(string,int,string)"/>（字符串直传，无固定/解固定）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage med = img.MedianImage("circle", 1, new JlTuple("mirrored"));
	///   using JlRegion parts = med.Threshold(128.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>掩膜类型/半径是 <c>StoreS</c>/<c>StoreI</c> 控制参数；输出新句柄需释放；
	///   <c>radius</c> 为 <c>int</c>，需要半像素或大窗口时改用 <see cref="MedianRect(int,int)"/> 或 Rank 族 [待实测]。</para>
	/// </remarks>
	public JlImage MedianImage(string maskType, int radius, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1352);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, maskType);
		JlNativeApi.StoreI(proc, 1, radius);
		JlNativeApi.Store(proc, 2, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>中值滤波（边界处理以字符串传入）。</summary>
	/// <param name="maskType">掩膜类型。Default: "circle"</param>
	/// <param name="radius">掩膜半径。Default: 1</param>
	/// <param name="margin">边界处理。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para>算法、掩膜尺寸与边界环带的注意事项见 <see cref="MedianImage(string,int,JlTuple)"/>：
	///   同一原生 id 1352，本版本 <c>StoreS</c> 直写 <paramref name="margin"/>，无固定/解固定，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage med = img.MedianImage("circle", 2, "mirrored");
	///   </code>
	/// </remarks>
	public JlImage MedianImage(string maskType, int radius, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1352);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, maskType);
		JlNativeApi.StoreI(proc, 1, radius);
		JlNativeApi.StoreS(proc, 2, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用不同秩掩膜做加权中值滤波，原生算子 id 1353。
	/// </summary>
	/// <param name="maskType">中值掩膜类型："all"/"inner"/"outer"/"border"（决定各像素的加权方式）。Default: "inner"</param>
	/// <param name="maskSize">掩膜尺寸（奇数，像素）。Default: 3</param>
	/// <returns>加权中值滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对窗内像素按 maskType 指定的权重重排后取加权中值，比标准中值对边缘/线条有不同保留特性。maskType 以 `StoreS` 写、maskSize 以 `StoreI` 写。</para>
	///   <para><b>约束或前提</b>maskSize 应为奇数 [待实测：偶数行为]。四种 maskType 的确切权重定义由原生决定，换类型结果差异明显，需用真图标定。</para>
	///   <para><b>与相邻算子的取舍</b>普通去椒盐优先 <see cref="MedianRect(int,int)"/>/<see cref="MedianImage(string,int,string)"/>；本算子用于需要偏向保留中心/边缘权重的场合。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage med = img.MedianWeighted("inner", 3);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage MedianWeighted(string maskType, int maskSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1353);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, maskType);
		JlNativeApi.StoreI(proc, 1, maskSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗排序（rank）滤波，id 1354。</summary>
	/// <param name="maskWidth">窗宽。Default: 15</param>
	/// <param name="maskHeight">窗高。Default: 15</param>
	/// <param name="rank">取窗内第几小的值。Default: 5</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把窗内 <c>maskWidth×maskHeight</c> 个灰度排序后取第 <paramref name="rank"/> 个。
	///   默认参数是 15×15=225 个值里取第 5 小，等于"接近最小值滤波"：亮噪声被压掉、暗结构保留；
	///   <c>rank ≈ 窗面积/2</c> 时退化为中值滤波，<c>rank = 窗面积</c> 时就是最大值滤波（≈ <see cref="GrayDilationRect(int,int)"/>）。</para>
	///   <para><b>关键差异：本重载没有边界参数</b>与 <see cref="RankImage(JlRegion,int,string)"/> 不同，
	///   <c>RankRect</c> 的参数只有三个 <c>int</c>（<c>StoreI</c>），边缘环带如何处理由原生决定 [待实测]，
	///   不能照搬 MedianImage 的 "mirrored" 设定。要求边界可控时改用 <c>RankImage</c> 并自己给掩膜区域。</para>
	///   <para><b>坑</b><paramref name="rank"/> 超出窗面积、或 ≤0 时本层不校验 [待实测]；
	///   rank 滤波是最慢的一类空域滤波（每窗排序），大图上先 <c>ReduceDomain</c> 缩小处理域；
	///   与 <c>GrayOpeningRect</c> 相比：rank 会同时改动一大片区域的灰度（不只是毛刺），别当通用去噪用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage darkOnly = img.RankRect(15, 15, 5);          // 接近最小值：压掉亮噪点
	///   using JlRegion bright = darkOnly.Threshold(180.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>单路图像输出；输入不变。</para>
	/// </remarks>
	public JlImage RankRect(int maskWidth, int maskHeight, int rank)
	{
		IntPtr proc = JlNativeApi.PreCall(1354);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreI(proc, 2, rank);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>任意形状掩膜的排序滤波，边界处理以元组传入（id 1355）。</summary>
	/// <param name="mask">滤波掩膜区域。</param>
	/// <param name="rank">取掩膜内第几小的值。Default: 5</param>
	/// <param name="margin">边界处理。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1355。掩膜是<b>图标输入</b>（区域，<c>Store(proc, 2, mask)</c>），
	///   <paramref name="rank"/> 与 <paramref name="margin"/> 分别占控制槽位 0、1。
	///   排序范围是<b>掩膜覆盖的像素集合</b>，不是外接矩形：用细长/环形掩膜可以做方向性 rank 滤波，
	///   这是它相对 <see cref="RankRect(int,int,int)"/> 的唯一实质优势。</para>
	///   <para><b>rank 的上限由掩膜决定</b>合法范围是掩膜的像素个数 [待实测：越界行为]，
	///   所以换掩膜后 <paramref name="rank"/> 必须重算，沿用上一次的数值会得到含义完全不同的滤波强度。
	///   排序位置是第几<b>小</b>：小 rank 压亮、大 rank 压暗，中位附近约等于中值滤波。</para>
	///   <para><b>与相邻算子的取舍</b>矩形窗且不在乎边界 → <see cref="RankRect(int,int,int)"/>（更快、参数更少）；
	///   只要均值 → <see cref="MeanImageShape(JlRegion)"/>；要去椒盐且掩膜是圆/方 → <see cref="MedianImage(string,int,string)"/>。
	///   掩膜像素数很少（如 5 个）时 rank 滤波等于最小值滤波，会把亮结构整体抹掉，不要拿它做"温和去噪"。</para>
	///   <para><b>参数取向</b><paramref name="margin"/> 元组版多值语义未在本层体现 [待实测]，单值请用
	///   <see cref="RankImage(JlRegion,int,string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion mask = new JlRegion(0.0, 0.0, 5.0, 5.0);       // (0,0)-(5,5) 共 36 像素的矩形掩膜
	///   using JlImage filtered = img.RankImage(mask, 5, new JlTuple("mirrored"));
	///   </code>
	///   <para><b>资源与坑</b>掩膜只读、需调用方自行释放（代码对 <c>this</c> 与 <paramref name="mask"/> 都 <c>GC.KeepAlive</c>）；
	///   掩膜若带"洞"或不连通，排序集合按掩膜实际像素计 [待实测]。</para>
	/// </remarks>
	public JlImage RankImage(JlRegion mask, int rank, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1355);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.StoreI(proc, 0, rank);
		JlNativeApi.Store(proc, 1, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}

	/// <summary>任意形状掩膜排序滤波（边界处理以字符串传入）。</summary>
	/// <param name="mask">滤波掩膜区域。</param>
	/// <param name="rank">取掩膜内第几小。Default: 5</param>
	/// <param name="margin">边界处理。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para>掩膜决定排序集合、rank 上限随掩膜变化等要点见 <see cref="RankImage(JlRegion,int,JlTuple)"/>：
	///   同一原生 id 1355，本版本 <c>StoreS</c> 直写 <paramref name="margin"/>，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion mask = new JlRegion(0.0, 0.0, 7.0, 7.0);
	///   using JlImage filtered = img.RankImage(mask, 5, "mirrored");
	///   </code>
	/// </remarks>
	public JlImage RankImage(JlRegion mask, int rank, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1355);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.StoreI(proc, 0, rank);
		JlNativeApi.StoreS(proc, 1, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}

	/// <summary>
	///   灰度开/中值/闭一体化滤波（连续可调），边界处理以元组传入。原生算子 id 1356。
	/// </summary>
	/// <param name="maskType">掩膜形状："circle" 或 "rect"。Default: "circle"</param>
	/// <param name="radius">滤波掩膜半径（像素）。Default: 1</param>
	/// <param name="modePercent">模式：0=灰度开运算，50=中值，100=灰度闭运算，中间为插值。Default: 10</param>
	/// <param name="margin">边界处理方式，以元组传入。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>modePercent 在开运算（去亮结构/毛刺）→中值→闭运算（去暗结构）之间连续取值，一个算子覆盖三种灰度形态学。</para>
	///   <para><b>约束或前提</b>maskType="rect" 时以 radius 定方形窗（如需各向不同宽高需换算子）[待实测]。margin 元组重载调用后 UnpinTuple。</para>
	///   <para><b>与相邻算子的取舍</b>只要纯中值用 MedianImage/MedianRect；只要纯开/闭用 GrayOpening/GrayClosing 族；本算子适合"偏开一点/偏闭一点"的折中滤噪。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple margin = "mirrored";
	///   JlImage f = img.DualRank("circle", 1, 10, margin);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；modePercent 越出 0..100 的行为未定义 [待实测]。</para>
	/// </remarks>
	public JlImage DualRank(string maskType, int radius, int modePercent, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1356);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, maskType);
		JlNativeApi.StoreI(proc, 1, radius);
		JlNativeApi.StoreI(proc, 2, modePercent);
		JlNativeApi.Store(proc, 3, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   灰度开/中值/闭一体化滤波（连续可调），边界处理以字符串传入。原生算子 id 1356。
	/// </summary>
	/// <param name="maskType">掩膜形状："circle" 或 "rect"。Default: "circle"</param>
	/// <param name="radius">滤波掩膜半径（像素）。Default: 1</param>
	/// <param name="modePercent">模式：0=灰度开运算，50=中值，100=灰度闭运算。Default: 10</param>
	/// <param name="margin">边界处理方式。Default: "mirrored"</param>
	/// <returns>滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一算子 id 1356，本重载 margin 以 `StoreS` 直写，无钉元组开销。</para>
	///   <para><b>约束或前提</b>modePercent 在开→中值→闭之间连续取值；radius 决定形态学作用尺度。</para>
	///   <para><b>与相邻算子的取舍</b>需要元组化传参才用 <see cref="DualRank(string,int,int,JlTuple)"/>；本重载是常规单值写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage f = img.DualRank("circle", 1, 10, "mirrored");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage DualRank(string maskType, int radius, int modePercent, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1356);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, maskType);
		JlNativeApi.StoreI(proc, 1, radius);
		JlNativeApi.StoreI(proc, 2, modePercent);
		JlNativeApi.StoreS(proc, 3, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗均值滤波（平滑），id 1357。</summary>
	/// <param name="maskWidth">窗宽。Default: 9</param>
	/// <param name="maskHeight">窗高。Default: 9</param>
	/// <returns>平滑后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>窗内算术平均，是最低成本的空间平滑。参数只有两个 <c>int</c>（<c>StoreI</c>），
	///   <b>没有</b>边界处理参数（与 <see cref="MedianImage(string,int,string)"/> 不同），边缘环带的处理方式由原生决定 [待实测]。</para>
	///   <para><b>典型用途：低频背景估计</b>它常作为 <see cref="DynThreshold(JlImage,double,string)"/> 的阈值图来源，
	///   或经 <see cref="SubImage(JlImage,double,double)"/> 相减做背景归一化。窗要明显大于目标，
	///   否则目标自身被算进"背景"，减法后目标消失——这是该用法最常见的失效方式。</para>
	///   <para><b>什么时候不该用它</b>尺寸/边缘位置测量之前。均值滤波把阶跃边缘展宽为窗宽量级的斜坡，
	///   之后 <c>Threshold</c> 的 50% 交点会随窗尺寸移动，宽度与位置测量随之系统偏移；
	///   这种场合用 <see cref="MedianImage(string,int,string)"/> 或直接不滤波。
	///   要"平滑但更贴近高斯"用 <see cref="GaussImage(int)"/>；要任意形状窗用 <see cref="MeanImageShape(JlRegion)"/>。</para>
	///   <para><b>坑</b>平均会把小数部分量化：源为 <c>byte</c> 时输出若仍是 <c>byte</c>，低对比结构的差异可能被量化抹平 [待实测：输出类型]；
	///   做灰度测量前建议先 <c>ConvertImageType("float")</c> 再平滑（类型名以 <c>GetImageType()</c> 实际返回的字符串为准）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage lowFreq = img.MeanImage(31, 31);
	///   using JlRegion spots = img.DynThreshold(lowFreq, 10.0, "light");
	///   </code>
	///   <para><b>资源与坑</b>输出新句柄；输入不变；窗面积越大耗时越长 [待实测：耗时量级]。</para>
	/// </remarks>
	public JlImage MeanImage(int maskWidth, int maskHeight)
	{
		IntPtr proc = JlNativeApi.PreCall(1357);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用二项式滤波器平滑图像，原生算子 id 1359。
	/// </summary>
	/// <param name="maskWidth">滤波器宽（像素）。Default: 5</param>
	/// <param name="maskHeight">滤波器高（像素）。Default: 5</param>
	/// <returns>平滑后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>二项式核近似高斯低通，尺寸越大平滑越强、振铃越小。参数以 `StoreI` 直写整数。</para>
	///   <para><b>约束或前提</b>单通道处理更常见；本算子无独立边界参数，四缘由原生决定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要更锐利的矩窗均值用 MeanImage；要真正的高斯用 GaussImage/GaussFilter；二项式在平滑与保边间取折中。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage sm = img.BinomialFilter(5, 5);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；滤波会压低峰值，后续灰度阈值不能沿用未平滑时的取值。</para>
	/// </remarks>
	public JlImage BinomialFilter(int maskWidth, int maskHeight)
	{
		IntPtr proc = JlNativeApi.PreCall(1359);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>离散高斯平滑，参数是滤波器尺寸而非 sigma（id 1360）。</summary>
	/// <param name="size">所需滤波器尺寸。Default: 5</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1360。唯一的参数 <paramref name="size"/> 是<b>尺寸</b>（英文 "Required filter size"），
	///   不是标准差：核的 σ 由原生按尺寸推出 [待实测：换算关系]，因此"把 σ 调成 1.5"这类需求在这里表达不了，
	///   需要显式 σ 时改用 <c>GenGaussFilter(...)</c> + <c>ConvolImage(...)</c>。</para>
	///   <para><b>与 <c>GaussFilter</c> 的区别</b>本库另有 <see cref="GaussFilter(int)"/>，同名同参却是<b>另一个原生 id 1361</b>。
	///   两者的实际差别无法从托管层看出 [待实测]，不要以为换名字只是别名——切换实现时要用输出图逐像素比对确认。</para>
	///   <para><b>与相邻算子的取舍</b>只要快的粗略平滑 → <see cref="MeanImage(int,int)"/>（矩形核，等效截止更钝）；
	///   要保边缘去椒盐 → <see cref="MedianImage(string,int,string)"/>；要给 <c>DynThreshold</c> 造低频阈值图，
	///   <c>GaussImage</c> 与 <c>MeanImage</c> 都行，但高斯的振铃更小、边缘处背景估计更贴近局部。</para>
	///   <para><b>坑</b>平滑会抬高噪声底、压低峰值：随后做 <c>GrayHisto</c>/<c>Intensity</c> 时同一物理条件的直方图会整体变窄，
	///   阈值随平滑强度变化，不能沿用未平滑时调出的值。边缘与核截断处理本层不体现 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage g = img.GaussImage(5);
	///   using JlRegion reg = g.Threshold(100.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>输出新句柄；<paramref name="size"/> 为 <c>int</c>，偶数/负值不做校验 [待实测]。</para>
	/// </remarks>
	public JlImage GaussImage(int size)
	{
		IntPtr proc = JlNativeApi.PreCall(1360);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, size);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用离散高斯函数平滑图像，原生算子 id 1361。
	/// </summary>
	/// <param name="size">所需滤波器尺寸（像素，非标准差）。Default: 5</param>
	/// <returns>滤波后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>size 是核尺寸而非 σ，σ 由原生按尺寸推出 [待实测：换算关系]。以 `StoreI` 写整数。</para>
	///   <para><b>与相邻算子的取舍</b>本库另有 <see cref="GaussImage(int)"/>（id 1360），同参不同算子，二者托管层看不出差别 [待实测]；需要显式控制高斯 σ 时本算子表达不了。粗略快速平滑可用 MeanImage。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage g = img.GaussFilter(5);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；平滑会压低峰值、抬高噪声底，阈值需重调。</para>
	/// </remarks>
	public JlImage GaussFilter(int size)
	{
		IntPtr proc = JlNativeApi.PreCall(1361);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, size);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把掩膜内偏离邻域过大的极值像素替换为邻域均值，做定向去噪，原生算子 id 1362。
	/// </summary>
	/// <param name="maskWidth">滤波掩膜宽（像素）。Default: 3</param>
	/// <param name="maskHeight">滤波掩膜高（像素）。Default: 3</param>
	/// <param name="gap">极值与邻域其余灰度之间所需的最小差值（灰度级）；差值超过它才被替换。Default: 1.0</param>
	/// <param name="mode">替换规则：选替换极小/极大/两者 [待实测：具体取值含义]。Default: 3</param>
	/// <returns>处理后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>只针对窗内"孤立的极亮/极暗"像素动手，其余像素保持原样，因此比均值/高斯更保边。</para>
	///   <para><b>约束或前提</b>gap 越大越保守（只替换极端离群），越小越接近普通均值滤波。mode 的确切取值语义本层无法确定 [待实测]，务必实测确认。</para>
	///   <para><b>与相邻算子的取舍</b>密集椒盐用 MedianImage；只想削掉个别坏点又不动边缘用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage outImg = img.EliminateMinMax(3, 3, 1.0, 3);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage EliminateMinMax(int maskWidth, int maskHeight, double gap, int mode)
	{
		IntPtr proc = JlNativeApi.PreCall(1362);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreD(proc, 2, gap);
		JlNativeApi.StoreI(proc, 3, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   对隔行扫描图像做去交错：插值或丢弃偶/奇场行，得到逐行连续的图像，原生算子 id 1363。
	/// </summary>
	/// <param name="mode">被替换/移除的行奇偶："even" 或 "odd"。Default: "odd"</param>
	/// <returns>去交错后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>用相邻保留行插值补齐被丢弃的场行，消除隔行采集造成的水平横纹错位。</para>
	///   <para><b>约束或前提</b>只对确为隔行采集的图像有意义；逐行采集的图强行去交错会引入竖直方向模糊。mode 选哪一半场被替换。</para>
	///   <para><b>与相邻算子的取舍</b>本库不提供 framegrabber 采集族，去交错是离线图像后处理手段。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage prog = img.FillInterlace("odd");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage FillInterlace(string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(1363);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在多幅/多通道图之间按逐像素排序取第 rankIndex 个灰度。</summary>
	/// <param name="rankIndex">取第几个排序位置。Default: 2</param>
	/// <returns>排序结果图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1364，英文定义 "Return gray values with given rank from multiple channels"：
	///   排序发生在<b>通道之间</b>而不是空间邻域内——对每个像素，把各通道的取值排序后输出第 <paramref name="rankIndex"/> 个。
	///   所以它<b>不是</b> <see cref="RankRect(int,int,int)"/>/<see cref="RankImage(JlRegion,int,string)"/> 那一类空间滤波，
	///   两者参数相似但作用完全不同，混用会得到一张"没有空间平滑效果"的图。</para>
	///   <para><b>输入形态</b>需要的是"同一位置有多个灰度"的数据：一幅 N 通道图（<see cref="Compose3(JlImage,JlImage)"/> 之类合成），
	///   或图像数组 [待实测：本层只 <c>Store(proc,1)</c> 声明一路输入，数组是否等价于多通道无法从托管层判断]。
	///   通道数用 <see cref="CountChannels()"/> 先确认。</para>
	///   <para><b>坑</b><paramref name="rankIndex"/> 是 <c>int</c> 控制参数，超过通道数时本层不校验 [待实测]；
	///   <c>rankIndex = 1</c> 即逐像素取最小通道值（多曝光/多视角里的"最暗"），最后一个即"最亮"，
	///   与 <see cref="MinImage(JlImage)"/>/<see cref="MaxImage(JlImage)"/> 只在两幅图时结果相同，多于两幅时才是它的用武之地。
	///   取均值请直接用 <c>MeanN()</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage a = new JlImage("byte", 640, 480);
	///   using JlImage b = new JlImage("byte", 640, 480);
	///   using JlImage two = a.Compose2(b);
	///   using JlImage darker = two.RankN(1);                       // 逐像素取两通道中较小者
	///   </code>
	///   <para><b>资源与坑</b>输出新句柄；输入不变。</para>
	/// </remarks>
	public JlImage RankN(int rankIndex)
	{
		IntPtr proc = JlNativeApi.PreCall(1364);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, rankIndex);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把多通道在每一像素上求算术平均，压成单通道图，原生算子 id 1365。
	/// </summary>
	/// <returns>通道平均后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <see cref="RankN(int)"/> 同族，但取的是均值而非第 k 序值：对每像素把 N 个通道值求平均。</para>
	///   <para><b>约束或前提</b>输入须为多通道图（通道数用 CountChannels 确认）；单通道时输出约等于拷贝。</para>
	///   <para><b>与相邻算子的取舍</b>想逐像素取最亮/最暗用 RankN；想把彩色图整体降灰度又保留平均亮度用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage a = new JlImage("byte", 256, 256);
	///   JlImage b = new JlImage("byte", 256, 256);
	///   JlImage two = a.Compose2(b);
	///   JlImage avg = two.MeanN();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；Compose2 的签名以本仓库实际为准 [待实测]。</para>
	/// </remarks>
	public JlImage MeanN()
	{
		IntPtr proc = JlNativeApi.PreCall(1365);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把掩膜内超出 [minThresh,maxThresh] 的灰度值替换为该掩膜的均值，原生算子 id 1366。
	/// </summary>
	/// <param name="maskWidth">滤波掩膜宽（像素）。Default: 3</param>
	/// <param name="maskHeight">滤波掩膜高（像素）。Default: 3</param>
	/// <param name="minThresh">保留区间下界（灰度级）。Default: 1</param>
	/// <param name="maxThresh">保留区间上界（灰度级）。Default: 254</param>
	/// <returns>处理后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>只把"落到有效区间外"的像素（过亮/过暗的坏点）改成掩膜均值，区间内像素原样保留。</para>
	///   <para><b>约束或前提</b>minThresh/maxThresh 是按图像灰度量纲设定的硬阈值——对 byte 图常用 1/254 只削纯黑纯白坏点；改成 float 图时区间语义完全不同。</para>
	///   <para><b>与相邻算子的取舍</b>要按"偏离邻域统计"判离群用 EliminateMinMax（自适应）；本算子是按绝对灰度区间判，适合已知坏点落在黑/白端。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage outImg = img.EliminateSp(3, 3, 1, 254);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage EliminateSp(int maskWidth, int maskHeight, int minThresh, int maxThresh)
	{
		IntPtr proc = JlNativeApi.PreCall(1366);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreI(proc, 2, minThresh);
		JlNativeApi.StoreI(proc, 3, maxThresh);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   抑制椒盐噪声：对落在 [minThresh,maxThresh] 之外的中心像素，用掩膜内均值替换，原生算子 id 1367。
	/// </summary>
	/// <param name="maskWidth">滤波掩膜宽（像素）。Default: 3</param>
	/// <param name="maskHeight">滤波掩膜高（像素）。Default: 3</param>
	/// <param name="minThresh">判定为噪声的下界（灰度级）。Default: 1</param>
	/// <param name="maxThresh">判定为噪声的上界（灰度级）。Default: 254</param>
	/// <returns>去噪后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>仅替换被 minThresh/maxThresh 判为"过暗/过亮"的中心像素，其余原样保留，比全窗均值更保边。</para>
	///   <para><b>约束或前提</b>阈值按图像灰度量纲设定：默认 1/254 针对 byte 图的纯黑/纯白噪声。区间太宽则几乎不动，太窄则连正常细节都被当噪声替换。</para>
	///   <para><b>与相邻算子的取舍</b>噪声幅度不固定时改用自适应的 EliminateMinMax 或中值滤波；坏点稳定贴黑/贴白时用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage outImg = img.MeanSp(3, 3, 1, 254);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage MeanSp(int maskWidth, int maskHeight, int minThresh, int maxThresh)
	{
		IntPtr proc = JlNativeApi.PreCall(1367);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreI(proc, 2, minThresh);
		JlNativeApi.StoreI(proc, 3, maxThresh);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   用 Sojka 算子检测角点，权重/阈值参数以元组传入，输出角点坐标。原生算子 id 1368。
	/// </summary>
	/// <param name="maskSize">所需滤波器尺寸（像素）。Default: 9</param>
	/// <param name="sigmaW">距离角点候选的高斯权重 σ。Default: 2.5</param>
	/// <param name="sigmaD">距离理想灰度边缘的高斯权重 σ。Default: 0.75</param>
	/// <param name="minGrad">梯度幅值阈值。Default: 30.0</param>
	/// <param name="minApparentness">显著度(apparentness)阈值。Default: 90.0</param>
	/// <param name="minAngle">角点处方向变化的阈值（弧度）。Default: 0.5</param>
	/// <param name="subpix">是否亚像素精化："true"/"false"。Default: "false"</param>
	/// <param name="row">输出角点行坐标（新元组句柄）。</param>
	/// <param name="column">输出角点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>Sojka 是灰度型角点检测：minGrad 决定"边缘要多强"，minApparentness 决定"角要多显著"，minAngle 用弧度限定两边缘的夹角跨度。本元组重载对前四个参数以 `Store`+`UnpinTuple` 传值。</para>
	///   <para><b>约束或前提</b>minAngle 是弧度不是角度。输入建议单通道灰度图。</para>
	///   <para><b>与相邻算子的取舍</b>想要标量签名用 <see cref="PointsSojka(int,double,double,double,double,double,string,out JlTuple,out JlTuple)"/>；相对 Harris/Lepetit/Foerstner，Sojka 更依赖灰度边缘模型。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple sw = 2.5, sd = 0.75, mg = 30.0, ma = 90.0;
	///   img.PointsSojka(9, sw, sd, mg, ma, 0.5, "false", out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 是新元组句柄，用完可 Dispose；无角点时输出空元组。</para>
	/// </remarks>
	public void PointsSojka(int maskSize, JlTuple sigmaW, JlTuple sigmaD, JlTuple minGrad, JlTuple minApparentness, double minAngle, string subpix, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1368);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSize);
		JlNativeApi.Store(proc, 1, sigmaW);
		JlNativeApi.Store(proc, 2, sigmaD);
		JlNativeApi.Store(proc, 3, minGrad);
		JlNativeApi.Store(proc, 4, minApparentness);
		JlNativeApi.StoreD(proc, 5, minAngle);
		JlNativeApi.StoreS(proc, 6, subpix);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sigmaW);
		JlNativeApi.UnpinTuple(sigmaD);
		JlNativeApi.UnpinTuple(minGrad);
		JlNativeApi.UnpinTuple(minApparentness);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Sojka 算子检测角点，参数以标量给定，输出角点坐标。原生算子 id 1368。
	/// </summary>
	/// <param name="maskSize">所需滤波器尺寸（像素）。Default: 9</param>
	/// <param name="sigmaW">距离角点候选的高斯权重 σ。Default: 2.5</param>
	/// <param name="sigmaD">距离理想灰度边缘的高斯权重 σ。Default: 0.75</param>
	/// <param name="minGrad">梯度幅值阈值。Default: 30.0</param>
	/// <param name="minApparentness">显著度阈值。Default: 90.0</param>
	/// <param name="minAngle">角点处方向变化阈值（弧度）。Default: 0.5</param>
	/// <param name="subpix">是否亚像素精化："true"/"false"。Default: "false"</param>
	/// <param name="row">输出角点行坐标（新元组句柄）。</param>
	/// <param name="column">输出角点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一算子 id 1368；本重载全部参数以 `StoreI`/`StoreD`/`StoreS` 直写，无钉元组开销。</para>
	///   <para><b>约束或前提</b>minAngle 为弧度。用字面量调用即绑定到本重载（优先于需隐式转换的元组重载）。</para>
	///   <para><b>与相邻算子的取舍</b>常规单组阈值检测用本重载；需元组传参见元组重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.PointsSojka(9, 2.5, 0.75, 30.0, 90.0, 0.5, "false", out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 是新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsSojka(int maskSize, double sigmaW, double sigmaD, double minGrad, double minApparentness, double minAngle, string subpix, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1368);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSize);
		JlNativeApi.StoreD(proc, 1, sigmaW);
		JlNativeApi.StoreD(proc, 2, sigmaD);
		JlNativeApi.StoreD(proc, 3, minGrad);
		JlNativeApi.StoreD(proc, 4, minApparentness);
		JlNativeApi.StoreD(proc, 5, minAngle);
		JlNativeApi.StoreS(proc, 6, subpix);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   增强图像中的圆形点（斑点），按直径匹配做亮/暗响应，原生算子 id 1369。
	/// </summary>
	/// <param name="diameter">待增强圆点的直径（像素）。Default: 5</param>
	/// <param name="filterType">增强对象："light" 亮斑、"dark" 暗斑、"all" 两者。Default: "light"</param>
	/// <param name="pixelShift">滤波响应的平移量（灰度级偏置）。Default: 0</param>
	/// <returns>增强后的新图像句柄，用毕需 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>输出是"点响应图"而非原图：直径匹配目标点处响应高，其余接近背景。便于随后 Threshold 提取点。</para>
	///   <para><b>约束或前提</b>diameter 必须接近真实点径，否则不响应。filterType 决定只看亮/暗，混用会同时增强噪声斑。</para>
	///   <para><b>与相邻算子的取舍</b>找十字用 CrossImage，找线用 LineExtractor 类；本算子专用于圆点。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage dots = img.DotsImage(5, "light", 0);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage DotsImage(int diameter, string filterType, int pixelShift)
	{
		IntPtr proc = JlNativeApi.PreCall(1369);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, diameter);
		JlNativeApi.StoreS(proc, 1, filterType);
		JlNativeApi.StoreI(proc, 2, pixelShift);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   亚像素检测图像中的局部极小值（暗点），输出坐标元组。原生算子 id 1370。
	/// </summary>
	/// <param name="filter">求偏导的方法："facet"/"deriche" 等。Default: "facet"</param>
	/// <param name="sigma">高斯 σ；filter="facet" 时可置 0.0 表示不对输入做平滑。</param>
	/// <param name="threshold">Hessian 矩阵特征值绝对值的最小门限（越大越只留强极值）。Default: 5.0</param>
	/// <param name="row">输出极小值行坐标（新元组句柄）。</param>
	/// <param name="column">输出极小值列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>基于二阶导（Hessian）特征值定位亚像素极小值。threshold 是响应强度门限，直接决定检出数量。</para>
	///   <para><b>约束或前提</b>filter="facet" 且 sigma=0 时用最原始邻域、不预平滑，噪声多时易误检，此时改 deriche 或给非零 sigma。</para>
	///   <para><b>与相邻算子的取舍</b>找极大多用 LocalMaxSubPix，找鞍点用 SaddlePointsSubPix；要一次拿全三类用 CriticalPointsSubPix。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.LocalMinSubPix("facet", 0.0, 5.0, out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void LocalMinSubPix(string filter, double sigma, double threshold, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1370);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   亚像素检测图像中的局部极大值（亮点），输出坐标元组。原生算子 id 1371。
	/// </summary>
	/// <param name="filter">求偏导的方法："facet"/"deriche" 等。Default: "facet"</param>
	/// <param name="sigma">高斯 σ；filter="facet" 时可置 0.0 不做预平滑。</param>
	/// <param name="threshold">Hessian 特征值绝对值门限。Default: 5.0</param>
	/// <param name="row">输出极大值行坐标（新元组句柄）。</param>
	/// <param name="column">输出极大值列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 LocalMinSubPix 对称，定位亚像素局部极大值。</para>
	///   <para><b>约束或前提</b>filter="facet" 且 sigma=0 时对噪声敏感。</para>
	///   <para><b>与相邻算子的取舍</b>找暗点用 LocalMinSubPix；三类一起用 CriticalPointsSubPix。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.LocalMaxSubPix("facet", 0.0, 5.0, out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void LocalMaxSubPix(string filter, double sigma, double threshold, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1371);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   亚像素检测图像中的鞍点，输出坐标元组。原生算子 id 1372。
	/// </summary>
	/// <param name="filter">求偏导的方法："facet"/"deriche" 等。Default: "facet"</param>
	/// <param name="sigma">高斯 σ；filter="facet" 时可置 0.0 不做预平滑。</param>
	/// <param name="threshold">Hessian 特征值绝对值门限。Default: 5.0</param>
	/// <param name="row">输出鞍点行坐标（新元组句柄）。</param>
	/// <param name="column">输出鞍点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>鞍点是"一个方向极大、垂直方向极小"的点，常用于分叉/桥接结构定位。</para>
	///   <para><b>约束或前提</b>同族亚像素检测器，threshold 决定强度门限。</para>
	///   <para><b>与相邻算子的取舍</b>要同时得极小/极大/鞍点用 CriticalPointsSubPix。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.SaddlePointsSubPix("facet", 0.0, 5.0, out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void SaddlePointsSubPix(string filter, double sigma, double threshold, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1372);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   亚像素一次性检测极小值、极大值与鞍点三类临界点。原生算子 id 1373。
	/// </summary>
	/// <param name="filter">求偏导的方法："facet"/"deriche" 等。Default: "facet"</param>
	/// <param name="sigma">高斯 σ；filter="facet" 时可置 0.0 不做预平滑。</param>
	/// <param name="threshold">Hessian 特征值绝对值门限。Default: 5.0</param>
	/// <param name="rowMin">输出极小值行坐标（新元组句柄）。</param>
	/// <param name="columnMin">输出极小值列坐标（新元组句柄）。</param>
	/// <param name="rowMax">输出极大值行坐标（新元组句柄）。</param>
	/// <param name="columnMax">输出极大值列坐标（新元组句柄）。</param>
	/// <param name="rowSaddle">输出鞍点行坐标（新元组句柄）。</param>
	/// <param name="columnSaddle">输出鞍点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>等价于把 LocalMinSubPix + LocalMaxSubPix + SaddlePointsSubPix 一次算完，省三遍偏导计算。</para>
	///   <para><b>约束或前提</b>六个 out 全部为 DOUBLE 元组，长度各不相同（各自一类点的数量）。</para>
	///   <para><b>与相邻算子的取舍</b>只要其中一类时用三个单独算子，避免装载多余输出。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.CriticalPointsSubPix("facet", 0.0, 5.0, out JlTuple rmin, out JlTuple cmin, out JlTuple rmax, out JlTuple cmax, out JlTuple rsad, out JlTuple csad);
	///   </code>
	///   <para><b>资源与坑</b>六个元组均为新句柄，用完可 Dispose。</para>
	/// </remarks>
	public void CriticalPointsSubPix(string filter, double sigma, double threshold, out JlTuple rowMin, out JlTuple columnMin, out JlTuple rowMax, out JlTuple columnMax, out JlTuple rowSaddle, out JlTuple columnSaddle)
	{
		IntPtr proc = JlNativeApi.PreCall(1373);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, sigma);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowMin);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnMin);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out rowMax);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out columnMax);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out rowSaddle);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out columnSaddle);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Harris 算子检测兴趣点，threshold 以元组传入。原生算子 id 1374。
	/// </summary>
	/// <param name="sigmaGrad">计算梯度时的平滑量。Default: 0.7</param>
	/// <param name="sigmaSmooth">积分梯度时的平滑量。Default: 2.0</param>
	/// <param name="alpha">梯度矩阵平方项迹的权重（Harris k）。Default: 0.08</param>
	/// <param name="threshold">点的最小滤波响应门限，以元组传入。Default: 1000.0</param>
	/// <param name="row">输出兴趣点行坐标（新元组句柄）。</param>
	/// <param name="column">输出兴趣点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>Harris 依据二阶矩矩阵的角点度量响应，对旋转稳定、对尺度不敏感。threshold 越高检出越少。</para>
	///   <para><b>约束或前提</b>threshold 以 `Store`+`UnpinTuple` 传值；响应量纲随 sigma/alpha 变，换参数后 threshold 需重调。</para>
	///   <para><b>与相邻算子的取舍</b>要标量签名用 <see cref="PointsHarris(double,double,double,double,out JlTuple,out JlTuple)"/>；要亚像素/二项式近似用 PointsHarrisBinomial。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple thr = 1000.0;
	///   img.PointsHarris(0.7, 2.0, 0.08, thr, out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsHarris(double sigmaGrad, double sigmaSmooth, double alpha, JlTuple threshold, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1374);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigmaGrad);
		JlNativeApi.StoreD(proc, 1, sigmaSmooth);
		JlNativeApi.StoreD(proc, 2, alpha);
		JlNativeApi.Store(proc, 3, threshold);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(threshold);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Harris 算子检测兴趣点，threshold 以标量给定。原生算子 id 1374。
	/// </summary>
	/// <param name="sigmaGrad">计算梯度时的平滑量。Default: 0.7</param>
	/// <param name="sigmaSmooth">积分梯度时的平滑量。Default: 2.0</param>
	/// <param name="alpha">梯度矩阵平方项迹的权重。Default: 0.08</param>
	/// <param name="threshold">点的最小滤波响应门限。Default: 1000.0</param>
	/// <param name="row">输出兴趣点行坐标（新元组句柄）。</param>
	/// <param name="column">输出兴趣点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一算子 id 1374；threshold 以 `StoreD` 直写。</para>
	///   <para><b>约束或前提</b>响应量纲随 sigma/alpha 变，换参数须重调 threshold。</para>
	///   <para><b>与相邻算子的取舍</b>字面量调用绑定到本重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.PointsHarris(0.7, 2.0, 0.08, 1000.0, out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsHarris(double sigmaGrad, double sigmaSmooth, double alpha, double threshold, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1374);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigmaGrad);
		JlNativeApi.StoreD(proc, 1, sigmaSmooth);
		JlNativeApi.StoreD(proc, 2, alpha);
		JlNativeApi.StoreD(proc, 3, threshold);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Harris 算子的二项式近似检测兴趣点，threshold 以元组传入。原生算子 id 1375。
	/// </summary>
	/// <param name="maskSizeGrad">计算梯度时的二项式平滑量（核尺寸）。Default: 5</param>
	/// <param name="maskSizeSmooth">积分梯度时的平滑量（核尺寸）。Default: 15</param>
	/// <param name="alpha">梯度矩阵平方项迹的权重。Default: 0.08</param>
	/// <param name="threshold">点的最小响应门限，以元组传入。Default: 1000.0</param>
	/// <param name="subpix">是否亚像素精化："on"/"off"。Default: "on"</param>
	/// <param name="row">输出兴趣点行坐标（新元组句柄）。</param>
	/// <param name="column">输出兴趣点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>用二项式核近似高斯，核尺寸以 `StoreI` 给（是尺寸不是 σ）；比 PointsHarris 更快，参数序不同。</para>
	///   <para><b>约束或前提</b>maskSize 越大越平滑、检出越少；subpix 与 threshold 配合决定最终点数。</para>
	///   <para><b>与相邻算子的取舍</b>需要 σ 精控用 <see cref="PointsHarris(double,double,double,JlTuple,out JlTuple,out JlTuple)"/>；要标量 threshold 用本类标量重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple thr = 1000.0;
	///   img.PointsHarrisBinomial(5, 15, 0.08, thr, "on", out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsHarrisBinomial(int maskSizeGrad, int maskSizeSmooth, double alpha, JlTuple threshold, string subpix, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1375);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSizeGrad);
		JlNativeApi.StoreI(proc, 1, maskSizeSmooth);
		JlNativeApi.StoreD(proc, 2, alpha);
		JlNativeApi.Store(proc, 3, threshold);
		JlNativeApi.StoreS(proc, 4, subpix);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(threshold);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Harris 二项式近似检测兴趣点，threshold 以标量给定。原生算子 id 1375。
	/// </summary>
	/// <param name="maskSizeGrad">计算梯度时的二项式平滑量（核尺寸）。Default: 5</param>
	/// <param name="maskSizeSmooth">积分梯度时的平滑量（核尺寸）。Default: 15</param>
	/// <param name="alpha">梯度矩阵平方项迹的权重。Default: 0.08</param>
	/// <param name="threshold">点的最小响应门限。Default: 1000.0</param>
	/// <param name="subpix">是否亚像素精化："on"/"off"。Default: "on"</param>
	/// <param name="row">输出兴趣点行坐标（新元组句柄）。</param>
	/// <param name="column">输出兴趣点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一算子 id 1375；threshold 以 `StoreD` 直写。</para>
	///   <para><b>约束或前提</b>maskSize 为整数核尺寸。</para>
	///   <para><b>与相邻算子的取舍</b>字面量 threshold 调用绑定到本重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.PointsHarrisBinomial(5, 15, 0.08, 1000.0, "on", out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsHarrisBinomial(int maskSizeGrad, int maskSizeSmooth, double alpha, double threshold, string subpix, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1375);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSizeGrad);
		JlNativeApi.StoreI(proc, 1, maskSizeSmooth);
		JlNativeApi.StoreD(proc, 2, alpha);
		JlNativeApi.StoreD(proc, 3, threshold);
		JlNativeApi.StoreS(proc, 4, subpix);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Lepetit(FAST) 算子检测兴趣点。原生算子 id 1376。
	/// </summary>
	/// <param name="radius">检测圆的半径（像素）。Default: 3</param>
	/// <param name="checkNeighbor">圆周上被检查的相邻点数（连续计数）。Default: 1</param>
	/// <param name="minCheckNeighborDiff">与圆周每一点所需的灰度差阈值。Default: 15</param>
	/// <param name="minScore">与全部圆周点灰度差之和的阈值。Default: 30</param>
	/// <param name="subpix">坐标亚像素精度："none"/"interpolation"/"regression"。Default: "interpolation"</param>
	/// <param name="row">输出兴趣点行坐标（新元组句柄）。</param>
	/// <param name="column">输出兴趣点列坐标（新元组句柄）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>FAST 型检测：比较中心与半径 radius 圆周上像素的灰度差，连续超阈点数达 checkNeighbor 判为角点。全参数以 `StoreI`/`StoreS` 直写。</para>
	///   <para><b>关键坑</b>row/column 走的是<b>不带 DOUBLE 类型标记</b>的 `JlTuple.LoadNew`（对比 PointsHarris 等用 `JlTupleType.DOUBLE`）——即使 subpix="interpolation"，输出坐标也可能是整数量纲，亚像素小数被丢 [待实测：实际装载类型]。</para>
	///   <para><b>与相邻算子的取舍</b>需要可靠亚像素坐标改用 PointsHarrisBinomial(subpix="on")；本算子胜在快。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.PointsLepetit(3, 1, 15, 30, "interpolation", out JlTuple row, out JlTuple column);
	///   </code>
	///   <para><b>资源与坑</b>row/column 为新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsLepetit(int radius, int checkNeighbor, int minCheckNeighborDiff, int minScore, string subpix, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1376);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, radius);
		JlNativeApi.StoreI(proc, 1, checkNeighbor);
		JlNativeApi.StoreI(proc, 2, minCheckNeighborDiff);
		JlNativeApi.StoreI(proc, 3, minScore);
		JlNativeApi.StoreS(proc, 4, subpix);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out row);
		err = JlTuple.LoadNew(proc, 1, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Foerstner 算子同时检测角点(junction)与区域(area)兴趣点，平滑参数以元组传入。原生算子 id 1377。
	/// </summary>
	/// <param name="sigmaGrad">计算梯度时的平滑量；smoothing="mean" 时忽略。Default: 1.0</param>
	/// <param name="sigmaInt">积分梯度时的平滑量。Default: 2.0</param>
	/// <param name="sigmaPoints">优化函数中的平滑量。Default: 3.0</param>
	/// <param name="threshInhom">非均匀区域分割阈值。Default: 200</param>
	/// <param name="threshShape">点区域分割阈值。Default: 0.3</param>
	/// <param name="smoothing">平滑方法："gauss"/"mean"。Default: "gauss"</param>
	/// <param name="eliminateDoublets">是否合并重复点。Default: "false"</param>
	/// <param name="rowJunctions">角点行坐标（新元组句柄）。</param>
	/// <param name="columnJunctions">角点列坐标（新元组句柄）。</param>
	/// <param name="coRRJunctions">角点协方差矩阵行-行分量。</param>
	/// <param name="coRCJunctions">角点协方差矩阵行-列混合分量。</param>
	/// <param name="coCCJunctions">角点协方差矩阵列-列分量。</param>
	/// <param name="rowArea">区域点行坐标。</param>
	/// <param name="columnArea">区域点列坐标。</param>
	/// <param name="coRRArea">区域点协方差矩阵行-行分量。</param>
	/// <param name="coRCArea">区域点协方差矩阵行-列混合分量。</param>
	/// <param name="coCCArea">区域点协方差矩阵列-列分量。</param>
	/// <remarks>
	///   <para><b>功能说明</b>Foerstner 通过二阶矩矩阵同时给出"点位置"与"定位不确定度"（协方差三分量）。本元组重载 sigmaGrad/sigmaInt/sigmaPoints/threshInhom 走 `Store`+`UnpinTuple`。</para>
	///   <para><b>约束或前提</b>smoothing="mean" 时 sigmaGrad 被忽略；threshShape 是标量 double。输出共 10 个 DOUBLE 元组，长度分别对应 junction 与 area 两组。</para>
	///   <para><b>与相邻算子的取舍</b>只要 Harris/Lepetit 类点数不含协方差时用相应算子；需要点位置+不确定度用本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple sg = 1.0, si = 2.0, sp = 3.0, ti = 200.0;
	///   img.PointsFoerstner(sg, si, sp, ti, 0.3, "gauss", "false",
	///       out JlTuple rj, out JlTuple cj, out JlTuple rrj, out JlTuple rcj, out JlTuple ccj,
	///       out JlTuple ra, out JlTuple ca, out JlTuple rra, out JlTuple rca, out JlTuple cca);
	///   </code>
	///   <para><b>资源与坑</b>10 个 out 元组都是新句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsFoerstner(JlTuple sigmaGrad, JlTuple sigmaInt, JlTuple sigmaPoints, JlTuple threshInhom, double threshShape, string smoothing, string eliminateDoublets, out JlTuple rowJunctions, out JlTuple columnJunctions, out JlTuple coRRJunctions, out JlTuple coRCJunctions, out JlTuple coCCJunctions, out JlTuple rowArea, out JlTuple columnArea, out JlTuple coRRArea, out JlTuple coRCArea, out JlTuple coCCArea)
	{
		IntPtr proc = JlNativeApi.PreCall(1377);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, sigmaGrad);
		JlNativeApi.Store(proc, 1, sigmaInt);
		JlNativeApi.Store(proc, 2, sigmaPoints);
		JlNativeApi.Store(proc, 3, threshInhom);
		JlNativeApi.StoreD(proc, 4, threshShape);
		JlNativeApi.StoreS(proc, 5, smoothing);
		JlNativeApi.StoreS(proc, 6, eliminateDoublets);
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
		JlNativeApi.UnpinTuple(sigmaGrad);
		JlNativeApi.UnpinTuple(sigmaInt);
		JlNativeApi.UnpinTuple(sigmaPoints);
		JlNativeApi.UnpinTuple(threshInhom);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowJunctions);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnJunctions);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out coRRJunctions);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out coRCJunctions);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out coCCJunctions);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out rowArea);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out columnArea);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.DOUBLE, err, out coRRArea);
		err = JlTuple.LoadNew(proc, 8, JlTupleType.DOUBLE, err, out coRCArea);
		err = JlTuple.LoadNew(proc, 9, JlTupleType.DOUBLE, err, out coCCArea);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 Foerstner 算子同时检测角点(junction)与区域(area)兴趣点，平滑参数以标量给定。原生算子 id 1377。
	/// </summary>
	/// <param name="sigmaGrad">计算梯度时的平滑量；smoothing="mean" 时忽略。Default: 1.0</param>
	/// <param name="sigmaInt">积分梯度时的平滑量。Default: 2.0</param>
	/// <param name="sigmaPoints">优化函数中的平滑量。Default: 3.0</param>
	/// <param name="threshInhom">非均匀区域分割阈值。Default: 200</param>
	/// <param name="threshShape">点区域分割阈值。Default: 0.3</param>
	/// <param name="smoothing">平滑方法："gauss"/"mean"。Default: "gauss"</param>
	/// <param name="eliminateDoublets">是否合并重复点。Default: "false"</param>
	/// <param name="rowJunctions">角点行坐标（新元组句柄）。</param>
	/// <param name="columnJunctions">角点列坐标（新元组句柄）。</param>
	/// <param name="coRRJunctions">角点协方差行-行分量。</param>
	/// <param name="coRCJunctions">角点协方差行-列分量。</param>
	/// <param name="coCCJunctions">角点协方差列-列分量。</param>
	/// <param name="rowArea">区域点行坐标。</param>
	/// <param name="columnArea">区域点列坐标。</param>
	/// <param name="coRRArea">区域点协方差行-行分量。</param>
	/// <param name="coRCArea">区域点协方差行-列分量。</param>
	/// <param name="coCCArea">区域点协方差列-列分量。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一算子 id 1377；本重载 sigmaGrad/sigmaInt/sigmaPoints/threshInhom 以 `StoreD` 直写，无钉元组开销。</para>
	///   <para><b>约束或前提</b>10 个 out 均为 DOUBLE 元组。</para>
	///   <para><b>与相邻算子的取舍</b>字面量参数调用即绑定本重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   img.PointsFoerstner(1.0, 2.0, 3.0, 200.0, 0.3, "gauss", "false",
	///       out JlTuple rj, out JlTuple cj, out JlTuple rrj, out JlTuple rcj, out JlTuple ccj,
	///       out JlTuple ra, out JlTuple ca, out JlTuple rra, out JlTuple rca, out JlTuple cca);
	///   </code>
	///   <para><b>资源与坑</b>10 个 out 元组都是新句柄，用完可 Dispose。</para>
	/// </remarks>
	public void PointsFoerstner(double sigmaGrad, double sigmaInt, double sigmaPoints, double threshInhom, double threshShape, string smoothing, string eliminateDoublets, out JlTuple rowJunctions, out JlTuple columnJunctions, out JlTuple coRRJunctions, out JlTuple coRCJunctions, out JlTuple coCCJunctions, out JlTuple rowArea, out JlTuple columnArea, out JlTuple coRRArea, out JlTuple coRCArea, out JlTuple coCCArea)
	{
		IntPtr proc = JlNativeApi.PreCall(1377);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigmaGrad);
		JlNativeApi.StoreD(proc, 1, sigmaInt);
		JlNativeApi.StoreD(proc, 2, sigmaPoints);
		JlNativeApi.StoreD(proc, 3, threshInhom);
		JlNativeApi.StoreD(proc, 4, threshShape);
		JlNativeApi.StoreS(proc, 5, smoothing);
		JlNativeApi.StoreS(proc, 6, eliminateDoublets);
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
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowJunctions);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out columnJunctions);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out coRRJunctions);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out coRCJunctions);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out coCCJunctions);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out rowArea);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out columnArea);
		err = JlTuple.LoadNew(proc, 7, JlTupleType.DOUBLE, err, out coRRArea);
		err = JlTuple.LoadNew(proc, 8, JlTupleType.DOUBLE, err, out coRCArea);
		err = JlTuple.LoadNew(proc, 9, JlTupleType.DOUBLE, err, out coCCArea);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   从单幅图像估计噪声标准差，以元组返回（每通道一个值）。原生算子 id 1378。
	/// </summary>
	/// <param name="method">噪声估计方法："foerstner"/"deriche1"/"lmed"。Default: "foerstner"</param>
	/// <param name="percent">参与估计的图像点百分比（0..100），以元组传入。Default: 20</param>
	/// <returns>噪声标准差元组（DOUBLE，可能多值），用毕可 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>用于自动调参/质量评估。percent 小 → 只用最"平坦"的点、估计偏保守；大 → 覆盖面广但可能把边缘当噪声。</para>
	///   <para><b>约束或前提</b>元组版按 `LoadNew(DOUBLE)` 返回全部值，多通道图每通道一项。</para>
	///   <para><b>与相邻算子的取舍</b>只需第一路数值用 <see cref="EstimateNoise(string,double)"/>（返回 double，丢弃其余通道）；要逐通道 σ 用本重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlTuple pct = 20;
	///   JlTuple noise = img.EstimateNoise("foerstner", pct);
	///   </code>
	///   <para><b>资源与坑</b>返回新元组句柄，用完可 Dispose。</para>
	/// </remarks>
	public JlTuple EstimateNoise(string method, JlTuple percent)
	{
		IntPtr proc = JlNativeApi.PreCall(1378);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, method);
		JlNativeApi.Store(proc, 1, percent);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(percent);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   从单幅图像估计噪声标准差，以 double 返回。原生算子 id 1378。
	/// </summary>
	/// <param name="method">噪声估计方法。Default: "foerstner"</param>
	/// <param name="percent">参与估计的图像点百分比（0..100）。Default: 20</param>
	/// <returns>噪声标准差（仅第一个值）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一算子 id 1378；本重载以 `LoadD` 只取第一个返回值。</para>
	///   <para><b>约束或前提</b>多通道输入下原生可能返回逐通道 σ，本 double 版会静默丢弃除首值以外的所有通道。</para>
	///   <para><b>与相邻算子的取舍</b>要每通道 σ 请用 <see cref="EstimateNoise(string,JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   double noise = img.EstimateNoise("foerstner", 20.0);
	///   </code>
	///   <para><b>资源与坑</b>返回值为原生 double，无需释放。</para>
	/// </remarks>
	public double EstimateNoise(string method, double percent)
	{
		IntPtr proc = JlNativeApi.PreCall(1378);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, method);
		JlNativeApi.StoreD(proc, 1, percent);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   从 constRegion 平坦区按 filterSize 均值窗估计噪声分布，经 LoadNew(DOUBLE) 逐输入区域返回元组，可喂给 AddNoiseDistribution 合成同分布噪声。
	/// </summary>
	/// <param name="constRegion">用于估计噪声分布的区域。</param>
	/// <param name="filterSize">均值滤波器的大小。默认值：21</param>
	/// <returns>所有输入区域的噪声分布。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>从图像里的一块"平坦/恒定"区域估算噪声分布，返回一个 DOUBLE 型元组，可直接喂给 <see cref="AddNoiseDistribution(JlTuple)"/> 合成同分布的噪声。原生算子 id 1379。</para>
	///   <para><b>约束或前提</b>constRegion 应是真实背景里灰度几乎不变的块（取到纹理区会把结构当噪声、估出的分布偏大）；filterSize 是均值滤波窗口，经 StoreI 以 INTEGER 装载，宜取奇数且不宜大于区域尺寸。图像(this)与 constRegion 两个输入句柄一起钉入，调用后靠 GC.KeepAlive 保命。</para>
	///   <para><b>与相邻算子的取舍</b>只想快速叠加随机噪声、不必匹配相机特性时用 <see cref="AddNoiseWhite(double)"/>（给幅度即可）；要复刻某采集设备的真实噪声形态，才走"本算子估分布 → AddNoiseDistribution 注入"这条链。</para>
	///   <para><b>参数取向</b>返回 LoadNew(DOUBLE) 新建的元组，"of all input regions"——传入区域数组时每区域各一项，多值不折叠；只要标量请自行取首元素。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlRegion flat = new JlRegion(20.0, 20.0, 60.0, 60.0);
	///   JlTuple dist = img.NoiseDistributionMean(flat, 21);
	///   JlImage noisy = img.AddNoiseDistribution(dist);
	///   </code>
	///   <para><b>资源与坑</b>本算子不改原图；dist 是新元组句柄、noisy 是新图句柄，用毕均需 Dispose。</para>
	/// </remarks>
	public JlTuple NoiseDistributionMean(JlRegion constRegion, int filterSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1379);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, constRegion);
		JlNativeApi.StoreI(proc, 0, filterSize);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(constRegion);
		return tuple;
	}

	/// <summary>
	///   叠加最大幅度为 amp 的白色随机噪声（是峰值幅度、非标准差），返回新图像句柄，原图不被改写。
	/// </summary>
	/// <param name="amp">最大噪声幅度。默认值：60.0</param>
	/// <returns>含噪声的图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>给图像叠加白色（均匀/随机）噪声，生成一幅新的"更脏"的图，常用于制造鲁棒性测试样本或验证去噪算子。原生算子 id 1380。</para>
	///   <para><b>约束或前提</b>amp 经 StoreD 以 DOUBLE 装载，是噪声最大幅度（叠加量的峰值，非标准差）；对 byte 图幅度越大越接近把像素推向 0/255 端而削顶。原图不被改写。</para>
	///   <para><b>与相邻算子的取舍</b>只是想"加点噪声"时用本算子（一个幅度即可）；要让噪声贴合相机真实分布，改用 <see cref="AddNoiseDistribution(JlTuple)"/>，其分布可由 <see cref="NoiseDistributionMean(JlRegion,int)"/> 估得。</para>
	///   <para><b>参数取向</b>InitOCT(1) 出一个图像，LoadNew 返回新 JlImage 句柄；输入仅 this（Store(proc,1)）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlImage noisy = img.AddNoiseWhite(60.0);
	///   </code>
	///   <para><b>资源与坑</b>noisy 是新句柄，用毕 Dispose；原 img 需保活到调用结束（内部 GC.KeepAlive）。</para>
	/// </remarks>
	public JlImage AddNoiseWhite(double amp)
	{
		IntPtr proc = JlNativeApi.PreCall(1380);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, amp);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   按 distribution 元组描述的噪声形态向图像注入噪声，返回新图像句柄，原图不改写；分布典型来自 NoiseDistributionMean 的实测估计。
	/// </summary>
	/// <param name="distribution">噪声分布。</param>
	/// <returns>含噪声的图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>按给定的噪声分布元组向图像注入噪声，生成新图，用来复刻某设备/某工况下实测到的噪声特征。原生算子 id 1381。</para>
	///   <para><b>约束或前提</b>distribution 是描述噪声形态的元组（典型来源是 <see cref="NoiseDistributionMean(JlRegion,int)"/> 的返回），以 Store 钉成固定元组、调用后 UnpinTuple；传入与图像通道不匹配的分布会影响结果。原图不改写。</para>
	///   <para><b>与相邻算子的取舍</b>没有实测分布、只想加随机噪声时用 <see cref="AddNoiseWhite(double)"/>（直接给幅度）；本算子适合"先估分布再注入"的标定式流程。</para>
	///   <para><b>参数取向</b>输入仅 this（Store(proc,1)）+ 参数 0 处的 distribution；InitOCT(1) 出一个图像，LoadNew 返回新 JlImage 句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlRegion flat = new JlRegion(20.0, 20.0, 60.0, 60.0);
	///   JlTuple dist = img.NoiseDistributionMean(flat, 21);
	///   JlImage noisy = img.AddNoiseDistribution(dist);
	///   </code>
	///   <para><b>资源与坑</b>noisy 是新句柄需 Dispose；dist 用完也应释放。</para>
	/// </remarks>
	public JlImage AddNoiseDistribution(JlTuple distribution)
	{
		IntPtr proc = JlNativeApi.PreCall(1381);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, distribution);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(distribution);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   对每个像素跨通道求标准差，把多通道图塌缩成单通道通道差异图（通道间越不一致越亮），返回新图像句柄；只在通道维统计、不看空间邻域。
	/// </summary>
	/// <returns>计算结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对同一像素的多个通道求标准差，把多通道图塌缩成单通道"通道差异图"——通道间越不一致处越亮，可用于彩色图的分化/显著性检测。原生算子 id 1384。</para>
	///   <para><b>约束或前提</b>语义依赖"若干通道"，输入应为多通道图（如三通道彩色图）；单通道输入下逐像素跨通道方差退化、结果无实际意义 [待实测]。原图不改写。</para>
	///   <para><b>与相邻算子的取舍</b>要"通道间差异/彩色分割线索"用它；要"空间邻域内的局部方差/纹理能量"则不是它的职责（本算子只在通道维统计，不看空间邻域）。</para>
	///   <para><b>参数取向</b>无标量控制参数，输入仅 this（Store(proc,1)），InitOCT(1) 出一个图像，LoadNew 返回新 JlImage 句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage color = new JlImage("sample_rgb.png");
	///   JlImage dev = color.DeviationN();
	///   </code>
	///   <para><b>资源与坑</b>dev 是新句柄，用毕 Dispose。</para>
	/// </remarks>
	public JlImage DeviationN()
	{
		IntPtr proc = JlNativeApi.PreCall(1384);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}


	/// <summary>
	///   用相干传输沿等照度线/结构方向把周围像素信息传进 region 掩膜区，region 外不变、原图不改写；本重载 channelCoefficients 走元组逐通道加权，返回修补后新图像句柄。
	/// </summary>
	/// <param name="region">图像修复区域。</param>
	/// <param name="epsilon">像素邻域的半径。默认值：5.0</param>
	/// <param name="kappa">锐度参数（百分比）。默认值：25.0</param>
	/// <param name="sigma">预平滑参数。默认值：1.41</param>
	/// <param name="rho">方向估计的平滑参数。默认值：4.0</param>
	/// <param name="channelCoefficients">通道权重。默认值：1</param>
	/// <returns>输出图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>用"相干传输(coherence transport)"修补 region 内的空洞：沿等照度线/结构方向把周围像素信息传输进被遮区域，擅长延续边缘、条纹等线性结构。原生算子 id 1386。本重载以 JlTuple 逐通道传权重。</para>
	///   <para><b>约束或前提</b>region 指定要修补的掩膜区，其外像素保持不变、原图不改写；epsilon 是像素邻域半径、kappa 是锐度百分比、sigma 预平滑、rho 方向估计平滑，均经 StoreD 以 DOUBLE 装载；channelCoefficients 走 Store 钉成固定元组、调用后 UnpinTuple，多通道时按通道给权重。</para>
	///   <para><b>与相邻算子的取舍</b>本重载的 channelCoefficients 是 JlTuple，另有 <see cref="InpaintingCt(JlRegion,double,double,double,double,double)"/> 收 double 标量版；数值参数务必用 double 字面量、走元组那一路须显式传 JlTuple，否则末位传裸 int 会因 JlTuple↔double 双向隐式转换而触发 CS0121 二义。要"平滑层级线"修补改用 InpaintingMcf，要"相干增强扩散"改用 InpaintingCed。</para>
	///   <para><b>参数取向</b>this 图像 + region 两个输入句柄（region 由 GC.KeepAlive 保命）；InitOCT(1) 出一个修补后图像，LoadNew 返回新 JlImage 句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 256, 256);
	///   JlRegion hole = new JlRegion(80.0, 80.0, 120.0, 120.0);
	///   JlTuple coeff = new JlTuple(1.0);
	///   JlImage filled = img.InpaintingCt(hole, 10.0, 25.0, 1.41, 4.0, coeff);
	///   </code>
	///   <para><b>资源与坑</b>filled 是新句柄需 Dispose；hole 也是新区域句柄。</para>
	/// </remarks>
	public JlImage InpaintingCt(JlRegion region, double epsilon, double kappa, double sigma, double rho, JlTuple channelCoefficients)
	{
		IntPtr proc = JlNativeApi.PreCall(1386);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreD(proc, 0, epsilon);
		JlNativeApi.StoreD(proc, 1, kappa);
		JlNativeApi.StoreD(proc, 2, sigma);
		JlNativeApi.StoreD(proc, 3, rho);
		JlNativeApi.Store(proc, 4, channelCoefficients);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(channelCoefficients);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>用相干输运（Coherence Transport）在洞区域内做修补，标量通道权重重载，原生算子 id 1386。</summary>
	/// <param name="region">待修补区域；仅区域内像素被改写，区域外保留原值。</param>
	/// <param name="epsilon">像素邻域半径（决定"往哪输运"的搜索尺度，像素）。Default: 5.0</param>
	/// <param name="kappa">方向选择的锐度参数（百分比）；越大越"敢跟着弱梯度"。Default: 25.0</param>
	/// <param name="sigma">求导前预平滑 σ（像素）。Default: 1.41</param>
	/// <param name="rho">方向估计的平滑系数。Default: 4.0</param>
	/// <param name="channelCoefficients">多通道加权（本重载为标量单值，等价于每通道权重都相同）。Default: 1</param>
	/// <returns>修补后的新 JlImage 句柄；输入图与区域不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>沿结构张量方向把洞外的灰度"输运"进洞里，是三族修补里方向延续性最强的一种。
	///   原生参数序与 C# 形参序一致：epsilon→0(D)、kappa→1(D)、sigma→2(D)、rho→3(D)、channelCoefficients→4(D)；
	///   region→图像槽 2、this→图像槽 1。</para>
	///   <para><b>约束或前提</b>本重载的 channelCoefficients 是"每通道相同"的标量；
	///   如需按通道差异化加权（如 Lab 中更信任 a/b 而不是 L），请走同 id 的元组重载
	///   <see cref="InpaintingCt(JlRegion,double,double,double,double,JlTuple)"/>。
	///   单通道输入时 channelCoefficients 无实际影响 [待实测：是否被原生忽略]。</para>
	///   <para><b>与相邻算子的取舍</b>想更省参数、洞较小：MCF <see cref="InpaintingMcf(JlRegion,double,double,int)"/>；
	///   想边缘保留更强、洞较大：CED <see cref="InpaintingCed(JlRegion,double,double,double,int)"/>；
	///   想让条纹直接跨过洞接上：本算子。</para>
	///   <para><b>参数取向</b>返回新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 256, 256);
	///   using JlRegion hole = new JlRegion(80.0, 80.0, 120.0, 120.0);
	///   using JlImage filled = img.InpaintingCt(hole, 5.0, 25.0, 1.41, 4.0, 1.0);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；<c>GC.KeepAlive(this/region)</c> 保活到原生调用结束，
	///   方法返回后 this 与 region 即可 Dispose。示例末尾用 <c>1.0</c> 而非 <c>1</c>，
	///   避免与 JlTuple 元组重载在 CS0121 上撞车（虽然 int→double 是标准隐式，仍应显式写 double 字面量）。</para>
	/// </remarks>
	public JlImage InpaintingCt(JlRegion region, double epsilon, double kappa, double sigma, double rho, double channelCoefficients)
	{
		IntPtr proc = JlNativeApi.PreCall(1386);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreD(proc, 0, epsilon);
		JlNativeApi.StoreD(proc, 1, kappa);
		JlNativeApi.StoreD(proc, 2, sigma);
		JlNativeApi.StoreD(proc, 3, rho);
		JlNativeApi.StoreD(proc, 4, channelCoefficients);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>用等高线平滑（MCF）在指定区域内做修补，比 CED 更简单，原生算子 id 1387。</summary>
	/// <param name="region">待修补的洞区域；仅区域内像素被改写，区域外保留原值。</param>
	/// <param name="sigma">求导核的预平滑 σ（像素）。Default: 0.5</param>
	/// <param name="theta">迭代步长；过大时数值格式不稳定 [待实测：稳定上界]。Default: 0.5</param>
	/// <param name="iterations">迭代次数，决定洞能被"填"到多深。Default: 10</param>
	/// <returns>修补后的新 JlImage 句柄；输入图与区域不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把等高线（等灰度线）沿切向平滑延拓进洞里。原生参数序与 C# 形参序一致：
	///   sigma→0(D)、theta→1(D)、iterations→2(I)；region→图像槽 2、this→图像槽 1。</para>
	///   <para><b>约束或前提</b>输入需为单通道；iterations 太小则洞中心仍是平坦外推。
	///   theta 与 iterations 共同决定实际"演化时长"，两者可粗略等效但会改变数值稳定性。</para>
	///   <para><b>与相邻算子的取舍</b>洞跨复杂条纹结构时选 <see cref="InpaintingCed(JlRegion,double,double,double,int)"/>
	///   或 <c>InpaintingCt</c>（都更能"沿结构长"）；洞在平坦区且想快选 <c>HarmonicInterpolation</c>。</para>
	///   <para><b>参数取向</b>返回新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 256, 256);
	///   using JlRegion hole = new JlRegion(80.0, 80.0, 120.0, 120.0);
	///   using JlImage filled = img.InpaintingMcf(hole, 0.5, 0.5, 10);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；<c>GC.KeepAlive(this/region)</c> 保活到原生调用结束，
	///   方法返回后 this 与 region 即可 Dispose。</para>
	/// </remarks>
	public JlImage InpaintingMcf(JlRegion region, double sigma, double theta, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(1387);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreD(proc, 1, theta);
		JlNativeApi.StoreI(proc, 2, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>用结构张量引导的相干增强扩散（CED）修补区域，比 MCF 更"沿结构长"，原生算子 id 1388。</summary>
	/// <param name="region">要修补的洞区域；仅区域内像素被改写，区域外保留原值。</param>
	/// <param name="sigma">求导核的预平滑 σ（像素）。Default: 0.5</param>
	/// <param name="rho">扩散系数图的平滑 σ（决定"结构感知"的范围）。Default: 3.0</param>
	/// <param name="theta">显式迭代的步长，过大时数值格式不稳定 [待实测：稳定上界]。Default: 0.5</param>
	/// <param name="iterations">扩散迭代次数，决定洞能被"填"到多深。Default: 10</param>
	/// <returns>修补后的新 JlImage 句柄；输入图与区域不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>先在整图上估结构张量，再沿结构方向做各向异性扩散：洞内的像素由周边沿
	///   结构连续方向流入。原生参数序与 C# 形参序一致：sigma→0(D)、rho→1(D)、theta→2(D)、iterations→3(I)；
	///   region 在图像槽 2、this 在图像槽 1。</para>
	///   <para><b>约束或前提</b>输入需为单通道图（多通道需按通道分开做 [待实测]）。
	///   iterations 太少则洞中心仍呈"平坦外推"；太多耗时线性增长。
	///   rho 决定跨边缘扩散的衰减尺度，太小会误沿弱梯度糊过边缘。</para>
	///   <para><b>与相邻算子的取舍</b>平坦区一次边值求解用 <c>HarmonicInterpolation</c>（快但会糊）；
	///   跨条纹的洞用 <c>InpaintingCt</c>（连贯输运，方向性最强但参数更多）；
	///   仅需"平滑等高线延拓"用 <see cref="InpaintingMcf(JlRegion,double,double,int)"/>（本方法的结构张量版）。</para>
	///   <para><b>参数取向</b>返回新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 256, 256);
	///   using JlRegion hole = new JlRegion(80.0, 80.0, 120.0, 120.0);
	///   using JlImage filled = img.InpaintingCed(hole, 0.5, 3.0, 0.5, 10);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；<c>GC.KeepAlive(this/region)</c> 保活到原生调用结束，
	///   方法返回后 this 与 region 即可 Dispose。</para>
	/// </remarks>
	public JlImage InpaintingCed(JlRegion region, double sigma, double rho, double theta, int iterations)
	{
		IntPtr proc = JlNativeApi.PreCall(1388);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreD(proc, 1, rho);
		JlNativeApi.StoreD(proc, 2, theta);
		JlNativeApi.StoreI(proc, 3, iterations);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>在指定区域内做各向异性扩散修补（边缘导向的"补洞"），原生算子 id 1389。</summary>
	/// <param name="region">要修补的区域：只有区域内的像素被改写，区域外原样保留。</param>
	/// <param name="mode">边缘锐化（扩散系数）算法族，取值由原生侧解释。Default: "weickert"</param>
	/// <param name="contrast">对比参数，决定梯度多陡才算"边缘"、扩散在边缘处衰减多快。Default: 5.0</param>
	/// <param name="theta">显式迭代的步长；过大时数值格式不稳定 [待实测：稳定上界]。Default: 0.5</param>
	/// <param name="iterations">扩散迭代次数，次数越多修补范围越能"长"进区域内部。Default: 10</param>
	/// <param name="rho">边缘信息的平滑系数（梯度尺度）。Default: 3.0</param>
	/// <returns>修补后的新图像句柄；输入图与区域都不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把 <paramref name="region"/> 当成待填充的"洞"，在洞内反复做受边缘调制的扩散：
	///   洞周边灰度沿连续梯度向内延拓，<paramref name="contrast"/>/<paramref name="rho"/> 控制"遇强边缘即停"的程度，
	///   所以跨在洞上的条纹能被顺势接上而不是糊成一片均值。原生参数序与 C# 形参序一致
	///   （mode→0、contrast→1、theta→2、iterations→3 INTEGER、rho→4），区域在图像槽 2。</para>
	///   <para><b>与相邻算子的取舍</b>洞横跨明显条纹/边缘时用本算子；洞在平坦区、只求快省时用
	///   <see cref="HarmonicInterpolation(JlRegion,double)"/>（一次边值求解，糊但稳）；要最强的方向延续性用
	///   <see cref="InpaintingCed(JlRegion,double,double,double,int)"/>（CED 沿结构张量方向扩散，参数更多也更慢）。</para>
	///   <para><b>约束</b>iterations 太小则洞中心仍是"平坦外推"，太大耗时线性增长；
	///   <paramref name="mode"/> 的合法取值集合本层不校验、透传原生 [待实测]。</para>
	///   <para><b>资源与坑</b>返回新句柄需释放；实现末尾 <c>GC.KeepAlive(this/region)</c>，
	///   调用返回后输入图与区域即可 Dispose。修补类算子只写 iconc 一路输出，无原地改写。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion hole = new JlRegion(200.0, 150.0, 240.0, 190.0);   // 左上/右下角点，待补的脏点块
	///   using JlImage filled = img.InpaintingAniso(hole, "weickert", 5.0, 0.5, 10, 3.0);
	///   </code>
	/// </remarks>
	public JlImage InpaintingAniso(JlRegion region, string mode, double contrast, double theta, int iterations, double rho)
	{
		IntPtr proc = JlNativeApi.PreCall(1389);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreD(proc, 1, contrast);
		JlNativeApi.StoreD(proc, 2, theta);
		JlNativeApi.StoreI(proc, 3, iterations);
		JlNativeApi.StoreD(proc, 4, rho);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>把区域内像素解一次调和边值问题（拉普拉斯插值）来修补，原生算子 id 1390。</summary>
	/// <param name="region">要填充的区域：洞的边界值来自区域外圈像素。</param>
	/// <param name="precision">迭代求解的收敛容差（相对残差），越小越精确越慢。Default: 0.001</param>
	/// <returns>填充后的新图像句柄；输入图与区域不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>以区域边界上的现有灰度为 Dirichlet 边界条件，在区域内部解离散拉普拉斯方程，
	///   得到区域内处处光滑（无局部极值）的填充——相当于"把边界平滑地拽进洞里"。
	///   托管层只把 <paramref name="precision"/> 以 <c>StoreD</c> 写入参数槽 0，区域在图像槽 2，输出单路 iconc。</para>
	///   <para><b>与相邻算子的取舍</b>它不延续任何纹理/条纹：跨越洞的结构到洞里会断成平滑过渡。
	///   洞在渐变背景上时它比 <see cref="InpaintingAniso(JlRegion,string,double,double,int,double)"/> 与
	///   <see cref="InpaintingCed(JlRegion,double,double,double,int)"/> 更便宜也更稳；
	///   要接条纹就必须换扩散族。修补整片低对比背景（如 vignette 区）也常用它。</para>
	///   <para><b>约束</b>大区域是逐像素迭代求解，耗时随洞面积增长明显 [待实测：具体量级]；
	///   区域若与图像定义域相交不良，边界条件取自哪些像素 [待实测]。</para>
	///   <para><b>资源与坑</b>返回新句柄需释放；调用返回后输入图与区域可 Dispose（实现末尾有 <c>GC.KeepAlive</c>）。
	///   注意输出只在洞内与输入不同，洞外是整幅复制，别指望它省内存。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion scratch = new JlRegion(100.0, 220.0, 104.0, 400.0);   // 一条细长划痕
	///   using JlImage repaired = img.HarmonicInterpolation(scratch, 0.001);
	///   </code>
	/// </remarks>
	public JlImage HarmonicInterpolation(JlRegion region, double precision)
	{
		IntPtr proc = JlNativeApi.PreCall(1390);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, region);
		JlNativeApi.StoreD(proc, 0, precision);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>向外扩张图像定义域并为新增环带补灰度，原生算子 id 1391。</summary>
	/// <param name="expansionRange">灰度扩张半径，单位是像素。Default: 2</param>
	/// <returns>定义域变大、扩张区带灰度的新图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把当前定义域沿边界向外生长 <paramref name="expansionRange"/> 像素，新增环带内生成有效灰度；
	///   输入图像不变，输出是全新图像句柄（iconc 槽位 1）。典型用途：ROI 处理链——<see cref="ReduceDomain(JlRegion)"/>
	///   之后的滤波、特征计算或写文件会在 ROI 外圈留下"域空洞"，用本算子把边界一圈补出来。</para>
	///   <para><b>约束</b>它不是"把整幅图填满"：每次只从现有域边界外推一圈，内部孤立空洞的半径大于
	///   <paramref name="expansionRange"/> 时填不到中心，需要加大半径或迭代调用。扩张带灰度的生成方法
	///   （插值/延拓/邻域统计）由原生决定，托管层看不出来 [待实测]；输出像素类型与输入的关系同 [待实测]。</para>
	///   <para><b>资源与坑</b><paramref name="expansionRange"/> 是 <c>int</c>（<c>StoreI</c>），0 与负值行为 [待实测]；
	///   返回图像需释放；对定义域本就覆盖全图的图像调用它，只是白复制一份图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(100.0, 100.0, 300.0, 300.0);
	///   using JlImage cut = img.ReduceDomain(roi);
	///   using JlImage filled = cut.ExpandDomainGray(3);   // 把 ROI 边界外 3 像素一圈补成有效域
	///   </code>
	/// </remarks>
	public JlImage ExpandDomainGray(int expansionRange)
	{
		IntPtr proc = JlNativeApi.PreCall(1391);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, expansionRange);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>地形学边缘素描：把每个像素归类为 11 种"地形结构"之一，输出标记图像，原生算子 id 1392。</summary>
	/// <returns>标记图像（新句柄）：每个像素的灰度是类编号，共 11 类。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把灰度图当高程面，对每个像素比较它与两个正交方向上的剖面曲率/梯度，
	///   归入峰、坑、山脊、谷、平坦区、斜坡（再按对比度强弱细分）等地形类。实现零参数：
	///   仅 <c>Store(proc,1)</c> 输入图像、单路 iconc 输出，检测尺度全部由原生内定 [待实测：尺度与类编号对应表]。</para>
	///   <para><b>什么时候用它</b>要"按地形结构分割"而不是按灰度分割时——同一灰度的山脊和谷会被
	///   <see cref="Threshold(double,double)"/> 混在一起，而本算子给出的是结构类别。
	///   之后对标记图逐值 <c>Threshold</c> 即可取出某一类结构成区域。</para>
	///   <para><b>与相邻算子的取舍</b>只要梯度幅值图用 <see cref="SobelAmp(string,int)"/> 一类边缘滤波；
	///   只要二值"凸/凹"判别用形态学顶帽/黑帽。本算子贵在为"分类图"，输出量大且需要解释类编号。</para>
	///   <para><b>约束</b>单通道图像；对 <c>byte</c> 以外类型（如 real 滤波中间结果）的类划分行为 [待实测]。</para>
	///   <para><b>资源与坑</b>返回新句柄需释放；标记图像素值 0..10 是类别编号而不是强度，不能再对它做均值类统计。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage labels = img.TopographicSketch();
	///   using JlRegion ridges = labels.Threshold(2.0, 2.0);   // 单独取出某一类（类号含义以实测标定为准）
	///   </code>
	/// </remarks>
	public JlImage TopographicSketch()
	{
		IntPtr proc = JlNativeApi.PreCall(1392);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用给定矩阵对多通道图像的通道值做逐像素线性变换（如 PCA 换基），原生算子 id 1393。</summary>
	/// <param name="transMat">变换矩阵，按行主序铺平成元组；行数应匹配通道数。矩阵确切尺寸约定 [待实测]。</param>
	/// <returns>变换后的多通道新图像句柄；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对每个像素的通道向量 v 计算 transMat·v（含常数项则加平移），得到新通道组合。
	///   典型用法是配合 <see cref="GenPrincipalCompTrans(out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>：
	///   它在前一幅图上学出 PCA 基，本算子把同一套基应用到任意同通道数的图上，输出按信息量排序的主成分通道。</para>
	///   <para><b>实现事实</b>矩阵走 <c>Store(proc,0,transMat)</c> 钉住固定内存，调用后立刻 <c>UnpinTuple</c>；
	///   图像在槽 1、单路 iconc 输出。矩阵怎么摊平（行优先/列优先）托管层不做加工，直接交给原生 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只想取某一个已有通道用 <see cref="AccessChannel(int)"/>，别为抽一个通道造矩阵；
	///   白平衡/亮度类一维缩放用 Scale 族。本算子的价值在"通道间混合"：降维、换色空间、按 PCA 投影。</para>
	///   <para><b>约束</b>输入必须是多通道图像；单通道图传矩阵等价于一次逐像素乘加（矩阵尺寸不符时原生报错）。</para>
	///   <para><b>资源与坑</b>返回新句柄需释放；<paramref name="transMat"/> 在调用返回后可 Dispose（Unpin 已在 Call 后完成）。
	///   注意 PCA 基是"在训练图上学的"，换一批样本颜色分布漂移时应重算 transMat，不要复用旧矩阵。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage rgb = new JlImage("sample_rgb.tif");              // 多通道输入（构造器按文件名读图）
	///   using JlTuple trans = rgb.GenPrincipalCompTrans(out JlTuple transInv, out JlTuple mean, out JlTuple cov, out JlTuple info);
	///   using JlImage pca = rgb.LinearTransColor(trans);               // 换到主成分基下
	///   </code>
	/// </remarks>
	public JlImage LinearTransColor(JlTuple transMat)
	{
		IntPtr proc = JlNativeApi.PreCall(1393);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, transMat);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(transMat);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在本图像的像素上训练 PCA：返回正变换矩阵，并给出逆变换、均值、协方差与各主成分信息量，原生算子 id 1394。</summary>
	/// <param name="transInv">逆变换矩阵（主成分空间映回原通道），DOUBLE 元组。</param>
	/// <param name="mean">各通道灰度均值，DOUBLE 元组。</param>
	/// <param name="cov">通道协方差矩阵（摊平），DOUBLE 元组。</param>
	/// <param name="infoPerComp">每个变换通道的信息量（特征值占比，按降序），DOUBLE 元组。</param>
	/// <returns>正变换矩阵（DOUBLE 元组），直接喂给 <see cref="LinearTransColor(JlTuple)"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把整幅多通道图像当作样本集做主成分分析。五个输出（含返回值）全部按
	///   <c>JlTupleType.DOUBLE</c> 装载：矩阵与统计量都是实数，别按 INTEGER 解读。
	///   返回值是"投影用"的正矩阵，<paramref name="transInv"/> 可把主成分图还原成原通道（逆变换后再 <c>LinearTransColor(transInv)</c>）。</para>
	///   <para><b>典型决策用法</b>拿 <paramref name="infoPerComp"/> 决定降维到几路：例如前两路信息量之和已接近 1，
	///   则后续只对正矩阵的前两行构成的子矩阵做变换即可 [待实测：正矩阵行序与信息量排序的对应关系]。</para>
	///   <para><b>与相邻算子的取舍</b>不想自己拼矩阵、只要完整 PCA 图时用 <see cref="PrincipalComp(out JlTuple)"/>（一步变换）；
	///   要"在 A 图上训练、应用到 B 图"的分离流程才用本算子——这正是它存在的意义。</para>
	///   <para><b>资源与坑</b>返回与四个 out 都是新建的 <c>JlTuple</c>（实现 <c>IDisposable</c>），用完要释放；
	///   输入图不变。样本只来自当前图，训练图若有脏区/背景占比失衡，主成分会被背景方向带偏——先 <c>ReduceDomain</c> 圈出目标再训。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage rgb = new JlImage("sample_rgb.tif");
	///   using JlTuple trans = rgb.GenPrincipalCompTrans(out JlTuple transInv, out JlTuple mean, out JlTuple cov, out JlTuple info);
	///   double first = info[0].D;                   // 第一主成分信息量（索引器给 JlTupleElements，取 .D）
	///   using JlImage pca = rgb.LinearTransColor(trans);
	///   </code>
	/// </remarks>
	public JlTuple GenPrincipalCompTrans(out JlTuple transInv, out JlTuple mean, out JlTuple cov, out JlTuple infoPerComp)
	{
		IntPtr proc = JlNativeApi.PreCall(1394);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out transInv);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out mean);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out cov);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out infoPerComp);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>一步完成 PCA：把多通道图像变换到主成分基下并给出各通道信息量，原生算子 id 1395。</summary>
	/// <param name="infoPerComp">各输出通道信息量（特征值占比，DOUBLE 元组），与输出通道一一对应。</param>
	/// <returns>主成分图像（新句柄，通道数与输入相同、按信息量降序）；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>等价于在本图上"现场训练 + 现场变换"：内部就是 <see cref="GenPrincipalCompTrans(out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>
	///   接 <see cref="LinearTransColor(JlTuple)"/> 的一次性版本。实现按 <c>JlTupleType.DOUBLE</c> 装载 infoPerComp（槽 0），
	///   图像走 iconc 槽 1。第一通道承载最大方差方向——把第一通道单独 <c>AccessChannel(0)</c> 出来常是最强判别特征。</para>
	///   <para><b>与相邻算子的取舍</b>矩阵需要复用到其他图、或只取前 k 个成分时，改用"训练/应用"分离的两步写法
	///   （GenPrincipalCompTrans + LinearTransColor），本算子不留矩阵、无法复用；只想换某个固定色空间也别说 PCA。</para>
	///   <para><b>约束</b>输入需为多通道图像；输出仍是图像句柄，若直接 <c>Threshold</c> 主成分图，
	///   阈值范围会随批次数据重排（PCA 基是数据相关的），静态阈值易漂 [待实测：byte 图变换后的量化与动态范围行为]。</para>
	///   <para><b>资源与坑</b>返回图像与 out 元组都是新对象需释放；infoPerComp 之和应为全通道总信息
	///   （归一化约定 [待实测]），据此截断保留前 k 路时别忘了 k 之外的方差被丢弃。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage rgb = new JlImage("sample_rgb.tif");
	///   using JlImage pca = rgb.PrincipalComp(out JlTuple info);
	///   using JlImage strongest = pca.AccessChannel(0);        // 第一主成分当单通道判别图
	///   using JlRegion blob = strongest.Threshold(200.0, 255.0);
	///   info.Dispose();
	///   </code>
	/// </remarks>
	public JlImage PrincipalComp(out JlTuple infoPerComp)
	{
		IntPtr proc = JlNativeApi.PreCall(1395);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out infoPerComp);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按模糊隶属函数计算区域灰度分布的模糊熵（边界模糊程度/不确定度），原生算子 id 1396。</summary>
	/// <param name="regions">要量测的区域（可含多个连通块，逐区域输出）。</param>
	/// <param name="apar">模糊隶属函数的起点（灰度低端），以 <c>INTEGER</c> 写入。Default: 0</param>
	/// <param name="cpar">模糊隶属函数的终点（灰度高端）。Default: 255</param>
	/// <returns>DOUBLE 元组：每个输入区域一个熵值，顺序与 regions 一致。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把"像素属于该区域"看成隶属度在 [<paramref name="apar"/>, <paramref name="cpar"/>]
	///   间渐变的模糊集合，熵越大表示区域内灰度越"骑墙"（没有明确的高低分界）。经典用法是扫描
	///   apar/cpar 找模糊熵极小点来自动选阈值；作为量测时它是"区域内部对比是否分明"的指标。</para>
	///   <para><b>实现事实</b>原生输入序与 C# 相反：区域在槽 1、图像在槽 2（<c>Store(proc,2)</c> 是 this），
	///   apar/cpar 走 <c>StoreI</c> 即参数槽 0/1。结果是 DOUBLE 元组，每区域一个值。</para>
	///   <para><b>与相邻算子的取舍</b>只要区域灰度均值/偏差用 <see cref="Intensity(JlRegion,out double)"/>（快且量纲直观）；
	///   只要"边界模糊"的周长类指标用 <see cref="FuzzyPerimeter(JlRegion,int,int)"/>——模糊熵看区域内灰度分布，模糊周长看边界过渡，二者不可互替。
	///   注意本算子同时吃"图 + 区域"：脱离灰度谈它没意义。</para>
	///   <para><b>约束</b>图像需单通道；apar 与 cpar 的大小关系/相等时行为 [待实测]。
	///   regions 与图像定义域不相交的区域输出什么 [待实测]。</para>
	///   <para><b>资源与坑</b>返回新元组需 Dispose；regions 与 this 在调用返回后可释放（末尾有 <c>GC.KeepAlive</c>）。
	///   多区域时别把熵值错位：输出顺序完全依赖 regions 的块序，上游 <c>Connection</c> 顺序变化会静默对错号。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion parts = img.Threshold(100.0, 255.0);
	///   using JlRegion one = parts.Connection();
	///   using JlTuple ent = img.FuzzyEntropy(one, 0, 255);   // 每个连通块一个模糊熵
	///   </code>
	/// </remarks>
	public JlTuple FuzzyEntropy(JlRegion regions, int apar, int cpar)
	{
		IntPtr proc = JlNativeApi.PreCall(1396);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.StoreI(proc, 0, apar);
		JlNativeApi.StoreI(proc, 1, cpar);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>按隶属度加权计算区域边界的模糊周长，边界越"实"越接近真实周长、越"虚"值越小，原生算子 id 1397。</summary>
	/// <param name="regions">要量测的区域（可含多个连通块，逐区域输出）。</param>
	/// <param name="apar">模糊隶属函数起点（灰度低端），<c>INTEGER</c> 写入。Default: 0</param>
	/// <param name="cpar">模糊隶属函数终点（灰度高端）。Default: 255</param>
	/// <returns>DOUBLE 元组：每个输入区域一个模糊周长（像素单位）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>沿区域边界逐段累加，每段权重取该处灰度的隶属度：定义域内灰度接近 apar/cpar
	///   两端（明确属于/不属于）的边界段全额计入，灰度居中的"软边界"按隶属度打折。
	///   适合评估失焦、低对比图像上边界的有效性——按纯几何数边长的周长会把噪声锯齿全额计入。</para>
	///   <para><b>实现事实</b>与 <see cref="FuzzyEntropy(JlRegion,int,int)"/> 同一套槽位布局：区域在原生槽 1、
	///   图像（this）在槽 2，apar/cpar 走 <c>StoreI</c> 槽 0/1，结果按 DOUBLE 装载。</para>
	///   <para><b>与相邻算子的取舍</b>本库没有包装纯几何周长算子，要"灰度无关"的边界量测只能自行由区域轮廓计算；
	///   要看区域内部灰度分布用模糊熵。隶属函数两端（apar/cpar）就是你对"边界像素灰度"的先验，两端给窄了
	///   几乎所有边界都算"实"，退化成普通周长。</para>
	///   <para><b>约束</b>图像单通道；多块区域输出顺序与 regions 块序一致，上游排序不稳定会静默错位。</para>
	///   <para><b>资源与坑</b>返回新元组需 Dispose；调用返回后 this/regions 可释放。空区域输入的输出值 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion parts = img.Threshold(120.0, 255.0);
	///   using JlTuple per = img.FuzzyPerimeter(parts, 60, 200);   // 隶属函数只认 60/200 之外的灰度为"确定"
	///   </code>
	/// </remarks>
	public JlTuple FuzzyPerimeter(JlRegion regions, int apar, int cpar)
	{
		IntPtr proc = JlNativeApi.PreCall(1397);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.StoreI(proc, 0, apar);
		JlNativeApi.StoreI(proc, 1, cpar);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>用指定形状掩膜做灰度闭运算（先 max 后 min）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>闭运算后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>本族三种写法（先选这一层）</b>
	///   ① <c>*Rect(int,int)</c>：矩形窗，尺寸是 <c>int</c>，见 <see cref="GrayClosingRect(int,int)"/>；
	///   ② <c>*Shape</c>（本族）：形状可选 <paramref name="maskShape"/>，且掩膜尺寸是 <c>double</c>，可以传 7.5 这类非整窗 [待实测：如何取整]；
	///   ③ <c>Gray*(JlImage SE)</c>：结构元自己造（<c>GenDiscSe</c>/<c>ReadGraySe</c>），SE 可带灰度坡度和任意形状，见
	///   <see cref="GrayClosing(JlImage)"/>。
	///   99% 的调参需求停在 ① 或 ②，只有需要"平顶以外的灰度帽"（黑帽/顶帽的 SE）时才值得上 ③。</para>
	///   <para><b>功能说明</b>原生算子 id 1398：窗内先取最大后取最小，即"上包络"，把窄暗谷抬到邻近水平。
	///   本族英文 <c>returns</c> 一律写着 "minimum gray values"，那是模板串抄错了：只有腐蚀才是窗内最小值，
	///   闭运算的输出量纲与 <see cref="GrayErosionShape(JlTuple,JlTuple,string)"/> 不同，别照抄那句说明理解算子。</para>
	///   <para><b>坑</b>掩膜是<b>邻域窗</b>，不是二值形态学里的"能不能放下"：窗给大一片，暗谷就整体被抬，
	///   随后 <c>Threshold</c> 的同一阈值会给出不同面积，灰度测量类流程里必须先固定窗再固定阈值。
	///   图像边缘的开窗方式本层未体现 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage closed = img.GrayClosingShape(new JlTuple(11.0), new JlTuple(11.0), "octagon");
	///   using JlRegion dark = closed.Threshold(0.0, 60.0);
	///   </code>
	///   <para><b>参数取向</b>元组版可一次传多组尺寸（多值如何与输出对应本层无法判断 [待实测]）；
	///   单组尺寸请用 <see cref="GrayClosingShape(double,double,string)"/>。
	///   重载绑定要注意：写 <c>GrayClosingShape(11, 11, "octagon")</c> 或 <c>(11.0, 11.0, ...)</c> 都会选到 double 版——
	///   本库有 <c>int/double/string → JlTuple</c> 的隐式转换，但用户定义转换在重载解析里排在标准隐式转换之后；
	///   想显式命中元组版必须写 <c>new JlTuple(...)</c>。</para>
	/// </remarks>
	public JlImage GrayClosingShape(JlTuple maskHeight, JlTuple maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1398);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, maskHeight);
		JlNativeApi.Store(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maskHeight);
		JlNativeApi.UnpinTuple(maskWidth);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>形状掩膜灰度闭运算（单组尺寸版）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>新图像句柄。</returns>
	/// <remarks>
	///   <para>算法与本族三种写法的取舍见 <see cref="GrayClosingShape(JlTuple,JlTuple,string)"/>：同一原生 id 1398。</para>
	///   <para><b>实际差异</b>两个尺寸经 <c>StoreD</c> 直写，无固定/解固定；掩膜形状仍是字符串，取值不校验。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage closed = img.GrayClosingShape(11.0, 11.0, "octagon");
	///   </code>
	/// </remarks>
	public JlImage GrayClosingShape(double maskHeight, double maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1398);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maskHeight);
		JlNativeApi.StoreD(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用指定形状掩膜做灰度开运算（先 min 后 max）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>开运算后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1399：窗内先取最小后取最大，得到原图的<b>下包络</b>，
	///   把比邻域高且窄的亮峰削掉，暗结构不受影响。与闭运算方向相反，两者不能互相顶替。</para>
	///   <para>三种灰度形态学写法（Rect / Shape / 自建 SE）的选择、掩膜尺寸的 <c>double</c> 语义、
	///   以及"本族英文 <c>returns</c> 写成 minimum gray values 是模板抄错"这一点，见
	///   <see cref="GrayClosingShape(JlTuple,JlTuple,string)"/>。</para>
	///   <para><b>与顶帽的分界</b>要"削掉亮峰继续用灰度"用本算子；要"把亮峰单独取出来"用
	///   <see cref="GrayTophat(JlImage)"/>（它就是原图减本算子的结果）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage cleaned = img.GrayOpeningShape(new JlTuple(11.0), new JlTuple(11.0), "octagon");
	///   </code>
	/// </remarks>
	public JlImage GrayOpeningShape(JlTuple maskHeight, JlTuple maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1399);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, maskHeight);
		JlNativeApi.Store(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maskHeight);
		JlNativeApi.UnpinTuple(maskWidth);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>形状掩膜灰度开运算（单组尺寸版）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>新图像句柄。</returns>
	/// <remarks>
	///   <para>下包络语义见 <see cref="GrayOpeningShape(JlTuple,JlTuple,string)"/>，本族写法取舍见
	///   <see cref="GrayClosingShape(JlTuple,JlTuple,string)"/>：同一原生 id 1399，尺寸走 <c>StoreD</c>。
	///   数字字面量会绑定到本重载；要打元组版必须显式 <c>new JlTuple(...)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage opened = img.GrayOpeningShape(11.0, 11.0, "octagon");
	///   </code>
	/// </remarks>
	public JlImage GrayOpeningShape(double maskHeight, double maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1399);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maskHeight);
		JlNativeApi.StoreD(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>形状掩膜内的灰度最小值滤波（灰度腐蚀）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>窗内最小值图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1400，英文说明就是"取掩膜范围内的最小灰度"：输出是逐窗最小值图。
	///   它把亮结构按窗尺寸"蚀掉"，只留下暗的东西，因此常用作<b>暗背景估计</b>：
	///   <c>img.GrayErosionShape(...)</c> 后与 <c>img.SubImage(bg, 1.0, 0.0)</c> 相减，即得到扣除背景后的亮目标图。</para>
	///   <para><b>与 <c>MinImage</c> 的区别</b>本算子在<b>同一幅图</b>的邻域内取最小（会移动边缘），
	///   <c>MinImage(JlImage)</c> 在两幅图之间逐像素取最小（不动边缘），二者不可互换。</para>
	///   <para><b>坑</b>输出整体变暗：亮目标小于窗宽时会被完全抹掉，做"背景估计"时窗必须明显大于目标最大尺寸，
	///   否则目标本身被当成背景减掉。边缘开窗与 <c>double</c> 尺寸取整方式见
	///   <see cref="GrayClosingShape(JlTuple,JlTuple,string)"/> [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage bg = img.GrayErosionShape(new JlTuple(21.0), new JlTuple(21.0), "octagon");
	///   using JlImage flat = img.SubImage(bg, 1.0, 0.0);          // 去背景的亮目标
	///   using JlRegion parts = flat.Threshold(20.0, 255.0);
	///   </code>
	/// </remarks>
	public JlImage GrayErosionShape(JlTuple maskHeight, JlTuple maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1400);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, maskHeight);
		JlNativeApi.Store(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maskHeight);
		JlNativeApi.UnpinTuple(maskWidth);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>形状掩膜灰度最小值滤波（单组尺寸版）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>新图像句柄。</returns>
	/// <remarks>
	///   <para>逐窗最小值语义、背景估计用法见 <see cref="GrayErosionShape(JlTuple,JlTuple,string)"/>；
	///   本族写法取舍见 <see cref="GrayClosingShape(JlTuple,JlTuple,string)"/>。同一原生 id 1400，尺寸走 <c>StoreD</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage min = img.GrayErosionShape(21.0, 21.0, "octagon");
	///   </code>
	/// </remarks>
	public JlImage GrayErosionShape(double maskHeight, double maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1400);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maskHeight);
		JlNativeApi.StoreD(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>形状掩膜内的灰度最大值滤波（灰度膨胀）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>窗内最大值图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1401，英文说明"取掩膜范围内的最大灰度"：逐窗最大值图。
	///   暗结构被抬到邻近亮水平，只剩最暗的东西，因此它是 <see cref="GrayErosionShape(JlTuple,JlTuple,string)"/> 的对偶：
	///   找<b>暗</b>目标（黑点、气泡、孔）时用本算子做<b>亮背景估计</b>，再 <c>SubImage</c>（背景减目标方向）或 <c>GrayBothat</c>。</para>
	///   <para><b>坑</b>输出整体变亮，<c>byte</c> 图接近 255 的部分被压顶，后续阈值/直方图在高端堆积 [待实测：饱和行为]。
	///   与 <c>MaxImage(JlImage)</c> 的区别同腐蚀：<b>邻域</b>取最大 vs <b>两幅图间</b>取最大。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage bg = img.GrayDilationShape(new JlTuple(21.0), new JlTuple(21.0), "octagon");
	///   using JlRegion holes = bg.SubImage(img, 1.0, 0.0).Threshold(20.0, 255.0);   // 背景减原图，暗孔变亮
	///   </code>
	///   <para><b>参数取向</b>多组尺寸走元组版，单组用 <see cref="GrayDilationShape(double,double,string)"/>；
	///   数字字面量会绑定到 double 版。</para>
	/// </remarks>
	public JlImage GrayDilationShape(JlTuple maskHeight, JlTuple maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1401);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, maskHeight);
		JlNativeApi.Store(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maskHeight);
		JlNativeApi.UnpinTuple(maskWidth);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>形状掩膜灰度最大值滤波（单组尺寸版）。</summary>
	/// <param name="maskHeight">掩膜高。Default: 11</param>
	/// <param name="maskWidth">掩膜宽。Default: 11</param>
	/// <param name="maskShape">掩膜形状。Default: "octagon"</param>
	/// <returns>新图像句柄。</returns>
	/// <remarks>
	///   <para>逐窗最大值语义与"亮背景估计"用法见 <see cref="GrayDilationShape(JlTuple,JlTuple,string)"/>；
	///   本族写法取舍见 <see cref="GrayClosingShape(JlTuple,JlTuple,string)"/>。同一原生 id 1401，尺寸走 <c>StoreD</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage max = img.GrayDilationShape(21.0, 21.0, "octagon");
	///   </code>
	/// </remarks>
	public JlImage GrayDilationShape(double maskHeight, double maskWidth, string maskShape)
	{
		IntPtr proc = JlNativeApi.PreCall(1401);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, maskHeight);
		JlNativeApi.StoreD(proc, 1, maskWidth);
		JlNativeApi.StoreS(proc, 2, maskShape);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗内的灰度极差（最大值减最小值），得到局部对比度图。</summary>
	/// <param name="maskHeight">窗高。Default: 11</param>
	/// <param name="maskWidth">窗宽。Default: 11</param>
	/// <returns>逐窗灰度极差图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1402。输出既不是膨胀也不是腐蚀，而是 <c>max − min</c>：
	///   平坦区接近 0，纹理/边缘/缺陷处变大。因此它是"哪里有起伏"的图，与"哪里亮"无关——
	///   用它 <c>Threshold</c> 出的区域是<b>纹理区</b>而不是亮区，反过来光照变化不影响它的判据。</para>
	///   <para><b>与相邻算子的取舍</b>要"起伏强度"的另一种统计量用 <c>DeviationImage(width,height)</c>（窗内标准差），
	///   对离群单点没本算子敏感；要"局部亮度趋势"用 <c>MeanImage</c>。自己用 <c>GrayDilationRect</c> 减
	///   <c>GrayErosionRect</c> 也能拼出同样结果，但多一次 <c>SubImage</c> 分配，本算子一步到位。</para>
	///   <para><b>坑</b>窗尺寸 <c>int</c>（<c>StoreI</c>），必须 ≥ 目标起伏的跨度才能把该起伏记进极差；
	///   窗越大越会把整片背景的低频不均也算成"对比度"。图像边缘开窗方式本层不体现 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage contrast = img.GrayRangeRect(9, 9);
	///   using JlRegion textured = contrast.Threshold(25.0, 255.0);   // 只看起伏，不看亮度
	///   </code>
	/// </remarks>
	public JlImage GrayRangeRect(int maskHeight, int maskWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1402);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskHeight);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗灰度闭运算（先 max 后 min），id 1403。</summary>
	/// <param name="maskHeight">窗高。Default: 11</param>
	/// <param name="maskWidth">窗宽。Default: 11</param>
	/// <returns>闭运算后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>矩形窗版本的 <see cref="GrayClosing(JlImage)"/>（用平顶 SE），把窄暗谷抬平。
	///   尺寸是 <c>int</c>（<c>StoreI</c>），只能整窗；需要圆形/八边形窗用
	///   <see cref="GrayClosingShape(double,double,string)"/>。</para>
	///   <para><b>坑</b>矩形窗的角部会在结果里留下 45° 以外的方角伪影：被抬平的区域外缘会带上窗形状，
	///   后面做 <c>Roundness</c>/<c>EllipticAxis</c> 一类形状量测时偏差来自窗形状而不是目标 [待实测：偏差量级]。
	///   非正方形窗（高≠宽）等价于给方向性滤波，横向暗线用 <c>(3,15)</c> 才抬得平。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage closed = img.GrayClosingRect(11, 11);
	///   using JlRegion dark = closed.Threshold(0.0, 50.0);
	///   </code>
	/// </remarks>
	public JlImage GrayClosingRect(int maskHeight, int maskWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1403);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskHeight);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗灰度开运算（先 min 后 max），id 1404。</summary>
	/// <param name="maskHeight">窗高。Default: 11</param>
	/// <param name="maskWidth">窗宽。Default: 11</param>
	/// <returns>开运算后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>矩形窗版本的 <see cref="GrayOpening(JlImage)"/>：削掉窄亮峰、保留下包络，
	///   常放在 <c>Threshold</c> 之前当"灰度域去噪"。与 <c>MedianImage</c> 的分工：中值滤波保边缘去椒盐，
	///   开运算专门压亮毛刺但会把亮边缘整体压低。</para>
	///   <para><b>坑</b>亮目标宽度小于窗宽时会被一起削掉；输出偏暗，<c>byte</c> 图低端有截断风险 [待实测]。
	///   形状用圆/八边形请改 <see cref="GrayOpeningShape(double,double,string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage opened = img.GrayOpeningRect(5, 5);
	///   using JlRegion bright = opened.Threshold(150.0, 255.0);
	///   </code>
	/// </remarks>
	public JlImage GrayOpeningRect(int maskHeight, int maskWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1404);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskHeight);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗内灰度最小值滤波，id 1405。</summary>
	/// <param name="maskHeight">窗高。Default: 11</param>
	/// <param name="maskWidth">窗宽。Default: 11</param>
	/// <returns>逐窗最小值图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>窗内取最小：亮结构按窗尺寸被抹掉，留下暗的东西，所以它是"暗背景/下包络"估计。
	///   与 <see cref="GrayErosionShape(JlTuple,JlTuple,string)"/> 只差窗形状（矩形 vs 可选圆/八边形）与尺寸类型（<c>int</c> vs <c>double</c>）。</para>
	///   <para><b>坑</b>窗必须明显大于亮目标，否则目标被当成背景；后续 <c>SubImage</c> 相减时两幅图类型不一致会引入
	///   量化误差 [待实测]。矩形窗的方角同样会留在结果边缘。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage bg = img.GrayErosionRect(21, 21);
	///   using JlImage flat = img.SubImage(bg, 1.0, 0.0);
	///   </code>
	/// </remarks>
	public JlImage GrayErosionRect(int maskHeight, int maskWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1405);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskHeight);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩形窗内灰度最大值滤波，id 1406。</summary>
	/// <param name="maskHeight">窗高。Default: 11</param>
	/// <param name="maskWidth">窗宽。Default: 11</param>
	/// <returns>逐窗最大值图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>窗内取最大：暗结构被邻近亮值覆盖，得到"上包络/亮背景"，与
	///   <see cref="GrayErosionRect(int,int)"/> 成对使用即可做背景归一化。</para>
	///   <para><b>与相邻算子的取舍</b>要圆/八边形窗或小数尺寸用 <see cref="GrayDilationShape(JlTuple,JlTuple,string)"/>；
	///   要在两幅图之间逐像素取最大用 <c>MaxImage(JlImage)</c>（它不做邻域、不会移动边缘）。</para>
	///   <para><b>坑</b>暗目标小于窗尺寸时会被彻底抹掉，做背景估计时窗要明显大于目标；
	///   <c>byte</c> 图输出偏亮，接近 255 处存在饱和 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage bg = img.GrayDilationRect(21, 21);
	///   using JlRegion pits = bg.SubImage(img, 1.0, 0.0).Threshold(20.0, 255.0);
	///   </code>
	/// </remarks>
	public JlImage GrayDilationRect(int maskHeight, int maskWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1406);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskHeight);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>灰度图像细化：把亮结构缩成保留灰度值的脊线。</summary>
	/// <returns>细化后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1407，英文说明即 "Thinning of gray value images"：在保持亮结构连通性的前提下
	///   把它细化到一线宽，并且<b>脊线上保留原灰度值</b>（不像二值细化那样把灰度信息丢掉），
	///   因此脊线灰度仍可当作宽度/强度量。</para>
	///   <para><b>与相邻算子的取舍</b>只需要形状骨架（拓扑、分支点）时用 <see cref="JlRegion"/> 的 <c>Skeleton()</c>，输出区域更省内存；
	///   要沿纹路的灰度剖面（划痕深度、焊点亮度）用本算子。本算子输出<b>仍是图像</b>，需要区域时再 <c>Threshold</c>。</para>
	///   <para><b>坑</b>细化对毛刺极敏感：亮毛刺会被细化成额外的枝杈，通常先做 <c>GrayOpeningRect</c> 或
	///   <c>MedianImage</c> 再细化；枝杈要量化可用区域侧 <c>JunctionsSkeleton(out JlRegion juncPoints)</c> 取分支点后再修剪。
	///   耗时随亮结构面积增长，大面积高亮图上比二值细化明显慢 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage prepared = img.GrayOpeningRect(3, 3);
	///   using JlImage ridges = prepared.GraySkeleton();
	///   using JlRegion lines = ridges.Threshold(1.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>无参数、单路输出，返回新句柄需释放；输入图不变。</para>
	/// </remarks>
	public JlImage GraySkeleton()
	{
		IntPtr proc = JlNativeApi.PreCall(1407);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>灰度查找表变换：新值 = lut[旧值]，逐像素重映射，原生算子 id 1408。</summary>
	/// <param name="lut">查找表（数值元组）：下标是原灰度，元素值是映射后的灰度。</param>
	/// <returns>重映射后的新图像句柄；输入图与元组不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对每个像素把原灰度当数组下标查表替换。适合一切"分段/非线性"的灰度整形：
	///   反相（表倒序）、二值化式分段、伽马曲线离散化、把 12bit 压到 8bit 的降位映射。
	///   实现把 <paramref name="lut"/> <c>Store</c> 钉住、调用后 <c>UnpinTuple</c>。</para>
	///   <para><b>与相邻算子的取舍</b>线性拉伸用 Scale 族（不必造 256 项表）；直方图整体重排用
	///   <see cref="EquHistoImage()"/>（自动按累积分布生成表）。本算子的优势是"表可手工设计、可跨批次固定"，
	///   产线要求同一 LUT 复现时首选它。</para>
	///   <para><b>约束</b>表按下标取整索引；原灰度超出表长（如 uint2 图喂 256 项表）时的越界行为 [待实测]。
	///   多通道图像是否每通道共用同一张表 [待实测]。表元素按 DOUBLE 装载即可，映射结果写回目标类型的量化方式 [待实测]。</para>
	///   <para><b>资源与坑</b>返回新句柄需释放；<paramref name="lut"/> 调用返回后可 Dispose。
	///   byte 图上表值超过 255 会截断/饱和 [待实测]，别指望 LUT 能扩大动态范围。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   double[] table = new double[256];
	///   for (int i = 0; i &lt; 256; i++) table[i] = (i &gt;= 100 &amp;&amp; i &lt;= 180) ? 255.0 : 0.0;   // 把 100~180 灰度段提白
	///   using JlImage hi = img.LutTrans(table);        // 隐式 double[] → JlTuple
	///   </code>
	/// </remarks>
	public JlImage LutTrans(JlTuple lut)
	{
		IntPtr proc = JlNativeApi.PreCall(1408);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, lut);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(lut);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>图像与任意滤波核做相关（非卷积），核由元组现场给出，原生算子 id 1409。</summary>
	/// <param name="filterMask">核系数按行铺平的数值元组，或内建核族的元组形式；尺寸决定邻域。</param>
	/// <param name="margin">边界处理（以元组传入的字符串），合法取值集合由原生侧决定。Default: "mirrored"</param>
	/// <returns>相关结果图像（新句柄）；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>逐窗做"核与图像点积"的相关运算：自定义锐化、定向纹理检测核都可以直接摆进
	///   <paramref name="filterMask"/>。real 结果不会替你归一化，量纲是核权重加权和 [待实测：输出类型与动态范围]。</para>
	///   <para><b>两个重载的分工</b>本重载吃"内存里造出来的核"（自定义矩阵；两参数都 <c>Store</c> 钉固定、
	///   调用后 <c>UnpinTuple</c>）；字符串版 <see cref="ConvolImage(string,string)"/> 吃内建核族名或掩膜文件名，
	///   走 <c>StoreS</c> 无钉固定开销。同一原生 id 1409。</para>
	///   <para><b>与相邻算子的取舍</b>相关≠卷积：核不对称时结果会中心翻转，做方向性增强时注意核要按"镜像预期"摆放 [待实测：原生是否按相关语义]。
	///   常规平滑用 <see cref="MeanImage(int,int)"/>/Rank 族更快；二阶结构用 <c>Laplace</c> 族。</para>
	///   <para><b>资源与坑</b>返回新句柄需释放；核元组与 margin 元组在调用返回后可 Dispose。
	///   重载绑定：一个字面量一个元组混着传会触发 JlTuple↔string 双向隐式的二义（CS0121），
	///   要走本重载就把两个实参都写成 <c>JlTuple</c> 类型变量。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlTuple kernel = new double[9] { 0, -1, 0, -1, 4, -1, 0, -1, 0 };   // 4 邻域拉普拉斯锐化核
	///   JlTuple margin = "mirrored";
	///   using JlImage sharp = img.ConvolImage(kernel, margin);
	///   kernel.Dispose(); margin.Dispose();
	///   </code>
	/// </remarks>
	public JlImage ConvolImage(JlTuple filterMask, JlTuple margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1409);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, filterMask);
		JlNativeApi.Store(proc, 1, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(filterMask);
		JlNativeApi.UnpinTuple(margin);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>图像与任意滤波核做相关（非卷积），核用族名/文件名指定，原生算子 id 1409。</summary>
	/// <param name="filterMask">内建核族名（如 "sobel"）或核掩膜文件名，取值由原生侧解析。Default: "sobel"</param>
	/// <param name="margin">边界处理字符串，取值由原生侧决定。Default: "mirrored"</param>
	/// <returns>相关结果图像（新句柄）；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与元组版 <see cref="ConvolImage(JlTuple,JlTuple)"/> 同一原生 id 1409，
	///   区别只在参数装载：本重载 <c>StoreS</c> 直写字符串、无钉固定/解固定开销。核族名对应哪组系数、
	///   掩膜文件的格式约定都在原生侧 [待实测：可用核族清单]。</para>
	///   <para><b>什么时候用它</b>用标准核（sobel/prewitt/gauss 一类，具体名称以原生为准）时不必自己填矩阵；
	///   需要实验性自定义核时退回元组版。想要现成的边缘幅值图直接 <c>SobelAmp</c> 族，不用手配核。</para>
	///   <para><b>资源与坑</b>返回新句柄需释放；两参数字面量直接写字符串即可绑定本重载，
	///   若混传 <c>JlTuple</c> 变量与字符串字面量会撞双向隐式转换的 CS0121 二义。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage edges = img.ConvolImage("sobel", "mirrored");
	///   using JlRegion strong = edges.Threshold(30.0, 255.0);
	///   </code>
	/// </remarks>
	public JlImage ConvolImage(string filterMask, string margin)
	{
		IntPtr proc = JlNativeApi.PreCall(1409);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filterMask);
		JlNativeApi.StoreS(proc, 1, margin);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>只换像素数据类型、不动数值范围的类型转换（real→byte 会量化丢小数），原生算子 id 1410。</summary>
	/// <param name="newType">目标像素类型名（如 "byte"、"real"、"uint2"），由原生侧校验。Default: "byte"</param>
	/// <returns>目标类型的新图像句柄；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>逐像素把数值 cast 成目标类型：它不做任何线性拉伸，
	///   因此 real 图上 137.6 变成 byte 的 137 附近值 [待实测：舍入还是截断]，超过目标量程的会被饱和掉 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>想把"实际用到的动态范围"铺满 0..255 用 <see cref="ScaleImageMax()"/>，
	///   想按系数拉伸用 Scale 族——只转类型不重标定才是本算子。存盘前把 real 中间结果转 byte、
	///   或把 16 位相机图降为 byte 给它。</para>
	///   <para><b>约束</b>通道数与定义域原样保留；负值转无符号类型的行为 [待实测]。</para>
	///   <para><b>资源与坑</b>返回新句柄需释放。最常见错误：先 <c>SubImage</c> 出带负差的 real 图，
	///   直接本算子转 byte 把负半轴全压成 0，差分缺陷全丢——应先加偏置或 <c>ScaleImageMax</c> 再转。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage wide = img.ConvertImageType("uint2");     // 升位后做累加不易饱和
	///   using JlImage back = wide.ConvertImageType("byte");
	///   </code>
	/// </remarks>
	public JlImage ConvertImageType(string newType)
	{
		IntPtr proc = JlNativeApi.PreCall(1410);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, newType);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把两幅 real 位移分量图合成一幅矢量场图像，原生算子 id 1411。</summary>
	/// <param name="col">列方向（x）分量图，须与行分量同尺寸同类型（real）。</param>
	/// <param name="type">矢量场语义标记（如相对/绝对位移），影响下游算子的解读。Default: "vector_field_relative"</param>
	/// <returns>新的矢量场图像句柄；两幅输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把 this 当作行（y 向下为正）分量、<paramref name="col"/> 当作列（x 向右为正）分量，
	///   打包成单句柄矢量场。<paramref name="type"/> 只是给场贴语义标签（托管层原样 <c>StoreS</c> 透传），
	///   供 <see cref="DerivateVectorField(double,string)"/>、<see cref="VectorFieldLength(string)"/> 等下游按含义处理；
	///   标签写错不会报错但会误导曲率/散度类计算 [待实测：各取值语义差异]。</para>
	///   <para><b>典型来源</b>光流/形变配准类算法输出的 row、col 两张分量图在此合体；反过来拆开用
	///   <see cref="VectorFieldToReal(out JlImage)"/>。两分量必须同网格，尺寸不一致时原生行为 [待实测：报错形式]。</para>
	///   <para><b>资源与坑</b>返回新句柄需释放；两输入在调用返回后可 Dispose（<c>GC.KeepAlive</c> 保证原生调用期间存活）。
	///   byte 分量图不合语义，输入应是 real 型。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage rowComp = new JlImage("real", 64, 64);        // 行分量
	///   using JlImage colComp = new JlImage("real", 64, 64);        // 列分量
	///   using JlImage vf = rowComp.RealToVectorField(colComp, "vector_field_relative");
	///   using JlImage mag = vf.VectorFieldLength("length");          // 每点位移大小
	///   </code>
	/// </remarks>
	public JlImage RealToVectorField(JlImage col, string type)
	{
		IntPtr proc = JlNativeApi.PreCall(1411);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, col);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(col);
		return obj;
	}

	/// <summary>把矢量场图像拆回行、列两幅 real 分量图，原生算子 id 1412。</summary>
	/// <param name="col">列（x）方向分量图：调用后新获得的句柄，须自行释放。</param>
	/// <returns>行（y）方向分量图（新句柄）；原矢量场不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b><see cref="RealToVectorField(JlImage,string)"/> 的逆操作：iconc 槽 1 给行分量、
	///   槽 2 给列分量（实现里各 <c>LoadNew</c> 一次），两路都是独立新图像句柄，与矢量场不共享内存。</para>
	///   <para><b>什么时候用它</b>分量图需要各路单独做统计/滤波（如只对行分量 <c>MeanImage</c>）时拆开；
	///   只求模长或场微分不要拆——<see cref="VectorFieldLength(string)"/> 与
	///   <see cref="DerivateVectorField(double,string)"/> 都直接吃矢量场，拆开再拼纯属多两次拷贝。</para>
	///   <para><b>约束</b>输入须为矢量场图像；对普通双通道 real 图调用的行为 [待实测]。</para>
	///   <para><b>资源与坑</b>返回值与 out 两路都要释放，最常见的泄漏是只 using 了返回值而忘了 col。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage rowComp = new JlImage("real", 64, 64);
	///   using JlImage colComp = new JlImage("real", 64, 64);
	///   using JlImage vf = rowComp.RealToVectorField(colComp, "vector_field_relative");
	///   using JlImage row = vf.VectorFieldToReal(out JlImage col);   // 别忘了 col 也要释放
	///   </code>
	/// </remarks>
	public JlImage VectorFieldToReal(out JlImage col)
	{
		IntPtr proc = JlNativeApi.PreCall(1412);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out col);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把两幅 real 图打包成一幅复数图：this 作实部、参数作虚部，原生算子 id 1413。</summary>
	/// <param name="imageImaginary">虚部分量图，须与实部同尺寸同类型（real）。</param>
	/// <returns>复数图像新句柄；两幅输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>纯打包算子：不搬数值、不做变换，只是把成对的实/虚分量存进同一个复数图像句柄
	///   （实现里 this→输入槽 1、虚部→槽 2，单路 iconc 输出）。频域处理流程里"手上有两张分量谱图、要合成
	///   一路复数结果"时才用它；普通两通道 real 图请直接用通道算子族，别为打包而打包。</para>
	///   <para><b>与相邻算子的取舍</b>拆分回两幅用 <see cref="ComplexToReal(out JlImage)"/>；
	///   复数→幅值/相位类需求若本库没有现成算子，先拆实虚再自行组合。</para>
	///   <para><b>约束</b>两分量尺寸/类型不一致时报错形式 [待实测]；byte 分量图不合语义。</para>
	///   <para><b>资源与坑</b>返回新句柄需释放；输入两图在调用返回后可 Dispose。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   using JlImage re = new JlImage("real", 64, 64);          // 实部分量
	///   using JlImage im = new JlImage("real", 64, 64);          // 虚部分量
	///   using JlImage cx = re.RealToComplex(im);
	///   using JlImage re2 = cx.ComplexToReal(out JlImage im2);   // 可无损拆回
	///   </code>
	/// </remarks>
	public JlImage RealToComplex(JlImage imageImaginary)
	{
		IntPtr proc = JlNativeApi.PreCall(1413);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageImaginary);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageImaginary);
		return obj;
	}

	/// <summary>把一路复数图拆成两路同尺寸实数图：返回实部，out 出参给虚部，原生算子 id 1414。</summary>
	/// <param name="imageImaginary">输出的虚部图（新句柄），必须由调用方 Dispose。</param>
	/// <returns>输出的实部图（新句柄），必须由调用方 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>输入应为 <c>RealToComplex</c> 或 FFT 输出的复数图。实现里 <c>InitOCT(1)</c> 与
	///   <c>InitOCT(2)</c> 各申请一路 iconc 输出：LoadNew(slot 1) 得实部、LoadNew(slot 2) 得虚部。
	///   这是原生参数序之外唯一的关键事实——两路输出都是<b>新句柄</b>。</para>
	///   <para><b>约束或前提</b>输入必须是复数图像（"complex" 类型）；否则原生侧行为未定义 [待实测：是否直接抛错]。
	///   输出尺寸与输入一致，类型为实数（通常 "real"）。</para>
	///   <para><b>与相邻算子的取舍</b>想把两路实数合成复数请走 <see cref="RealToComplex(JlImage)"/>；
	///   若只需要"幅值/相位"表达，本库未直接暴露这类拆合算子。</para>
	///   <para><b>参数取向</b>返回值 + out 参数各持一个新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage re = new JlImage("real", 64, 64);
	///   JlImage im = new JlImage("real", 64, 64);
	///   using JlImage cplx = re.RealToComplex(im);
	///   re.Dispose(); im.Dispose();
	///
	///   JlImage imaginaryOut;
	///   using JlImage realOut = cplx.ComplexToReal(out imaginaryOut);
	///   imaginaryOut.Dispose();
	///   cplx.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两路输出都是新句柄；<c>GC.KeepAlive(this)</c> 保证原生调用期内输入不被回收，
	///   调用返回后输入即可 Dispose。</para>
	/// </remarks>
	public JlImage ComplexToReal(out JlImage imageImaginary)
	{
		IntPtr proc = JlNativeApi.PreCall(1414);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out imageImaginary);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>对每个输入区域计算其在 this 图上的平均灰度并以该值填充对应像素，返回同尺寸新图，原生算子 id 1415。</summary>
	/// <param name="regions">输入区域集合（iconc，多区域按序处理）。</param>
	/// <returns>新 JlImage 句柄；仅区域覆盖像素被改写为区域均值，区域外保持原灰度 [待实测：是否保留区域外内容或置零]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把 regions 拆出的每个区域独立求均值再回填，常用于分块平滑或"分区去噪"。
	///   原生槽位序<b>与 C# 形参序相反</b>：regions→slot 1、this 图→slot 2。这是本族的固有约定，
	///   写注释时务必核对，不能想当然。</para>
	///   <para><b>约束或前提</b>输入需为单通道图；多通道图的行为未在本层校验。
	///   regions 若为 iconc 多区域，序号顺序由上游 <c>Connection</c> 决定，顺序不稳定会导致错位。</para>
	///   <para><b>与相邻算子的取舍</b>只想"整图按 mask 均值滤波"用 <c>MeanImage</c>；
	///   只关心每个区域的均值数值（而不是回填图）用本类的 <c>Intensity</c> 族。</para>
	///   <para><b>参数取向</b>返回新句柄；regions 与 this 都不变。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion r = img.Threshold(50.0, 200.0).Connection();
	///   using JlImage painted = img.RegionToMean(r);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄需 Dispose；regions 与 this 通过 <c>GC.KeepAlive</c> 保活到原生调用结束。</para>
	/// </remarks>
	public JlImage RegionToMean(JlRegion regions)
	{
		IntPtr proc = JlNativeApi.PreCall(1415);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return obj;
	}

	/// <summary>逐像素求"通往图像边界任意路径上所能遇到的最低灰度"。</summary>
	/// <returns>灰度内部值图像（新句柄）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1416。英文定义："对每个像素，求通往图像边界任意路径上的最低可能灰度"。
	///   即把图像当高程面，从<b>边界</b>往里浸水：一个像素的输出值 = 它能沿连续路径"逃到"边界时会遇到的最低水位。
	///   被高灰度包围的封闭低洼区（孔、暗斑内部）逃不出去，输出值会高于其实际灰度——这正是它用来区分
	///   "真正连到外部的低区"与"封闭洼地"的能力。</para>
	///   <para><b>什么时候用它</b>需要判断暗区是否与图像边界连通（涂布边缘、连通气孔 vs 内部夹杂），
	///   或作为分水/流域合并（见 <see cref="WatershedsMarker(JlRegion)"/>）的前置量。
	///   只是想去低频背景，用 <c>GrayErosionRect</c>/<c>GrayBothat</c> 更直接也更快。</para>
	///   <para><b>坑</b>输入被当成高程面：如果图像是"目标亮、背景暗"，洼地在背景，输出几乎处处等于背景最小值，
	///   信息全丢——这类图必须先 <c>InvertImage()</c> 或改用对偶方向的算子 [待实测：本算子是否有方向参数]。
	///   边界本身的灰度会向内传播整个连通区域，所以来料边缘压暗会污染整块判据。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage inside = img.GrayInside();
	///   using JlRegion trapped = img.SubImage(inside, 1.0, 0.0).Threshold(1.0, 255.0);   // 封闭洼地处有差值
	///   </code>
	///   <para><b>资源与坑</b>无参数、单路图像输出；输入图不变。</para>
	/// </remarks>
	public JlImage GrayInside()
	{
		IntPtr proc = JlNativeApi.PreCall(1416);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>沿指定方向计算灰度对称响应，用于纹理/缺陷方向性检测，原生算子 id 1417。</summary>
	/// <param name="maskSize">对称核沿测试方向的最大延展半径（像素，INTEGER）。Default: 40</param>
	/// <param name="direction">测试方向（相对水平轴，弧度）。Default: 0.0</param>
	/// <param name="exponent">对称度加权指数，控制"越对称分数越高"的锐度。Default: 0.5</param>
	/// <returns>新 JlImage 对称响应图（实数域）；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对每一像素，考察沿 direction 方向上 maskSize 邻域内左右两侧的灰度对称性，
	///   以 exponent 加权得到对称度量。原生参数序：maskSize→0(I)、direction→1(D)、exponent→2(D)。</para>
	///   <para><b>约束或前提</b>输入应为单通道图；maskSize 越大响应越平滑但小结构会被抹掉。
	///   direction 是弧度还是角度本层不校验 [待实测]。exponent 需非负 [待实测：本层是否校验]。</para>
	///   <para><b>与相邻算子的取舍</b>只想找边缘用 <c>SobelAmp</c>；本算子专用于灰度对称性（例如条纹/划痕
	///   检测里的"中间亮两侧暗"或"中间暗两侧亮"的对称模式）。</para>
	///   <para><b>参数取向</b>返回新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage sym = img.Symmetry(40, 0.0, 0.5);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；输出实数图直接 Threshold 前需注意量纲归一。</para>
	/// </remarks>
	public JlImage Symmetry(int maskSize, double direction, double exponent)
	{
		IntPtr proc = JlNativeApi.PreCall(1417);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSize);
		JlNativeApi.StoreD(proc, 1, direction);
		JlNativeApi.StoreD(proc, 2, exponent);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按索引图像的像素值逐像素从多通道源图中挑选对应通道的灰度，原生算子 id 1418。</summary>
	/// <param name="indexImage">索引图：像素值当作通道下标（0 起），需与 this 同尺寸。</param>
	/// <returns>新 JlImage 句柄，单通道；输入 this 与 indexImage 均不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>典型用法是"颜色分割完再回填"——先用分类器给出通道归属，再用本算子把
	///   原多通道图按索引展平为单通道。原生参数序：this→slot 1、indexImage→slot 2。</para>
	///   <para><b>约束或前提</b>this 必须是多通道图；indexImage 的取值范围必须落在 [0, 通道数) 内，
	///   越界索引在原生侧的处理未在本层校验 [待实测：越界行为]。两图宽高需一致。</para>
	///   <para><b>与相邻算子的取舍</b>如果只需固定挑某一路通道，直接 <c>AccessChannel(int)</c> 更清晰；
	///   本算子专用于"每个像素挑不同通道"的场景。</para>
	///   <para><b>参数取向</b>返回新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage rgb = new JlImage("byte", 640, 480);   // 3 通道
	///   JlImage idx = new JlImage("byte", 640, 480);   // 每像素存 0/1/2
	///   using JlImage pick = rgb.SelectGrayvaluesFromChannels(idx);
	///   idx.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄需 Dispose；this 与 indexImage 由 <c>GC.KeepAlive</c> 保活到
	///   原生调用结束，返回后 indexImage 即可 Dispose。</para>
	/// </remarks>
	public JlImage SelectGrayvaluesFromChannels(JlImage indexImage)
	{
		IntPtr proc = JlNativeApi.PreCall(1418);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, indexImage);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(indexImage);
		return obj;
	}






	/// <summary>对 2 通道向量场做高斯导数卷积，抽出指定曲率/散度分量，元组 σ 重载，原生算子 id 1423。</summary>
	/// <param name="sigma">σ 元组，允许多组值批处理。Default: 1.0</param>
	/// <param name="component">要抽取的分量（同标量重载）。Default: "mean_curvature"</param>
	/// <returns>新 JlImage 句柄；输入向量场不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量重载共用 id 1423；σ 用 <c>Store</c> 钉元组、结束再 <c>UnpinTuple</c>。
	///   其余参数槽位与标量版一致（component→1(S)）。</para>
	///   <para><b>约束或前提</b>σ 元组在 <c>CallProcedure</c> 结束前不得 Dispose，返回后方可释放。
	///   多 σ 是否对应 iconc 内多路输出未在本层校验 [待实测]。输入需 2 通道。</para>
	///   <para><b>与相邻算子的取舍</b>单 σ 场景优先走标量重载（无钉固定开销）。</para>
	///   <para><b>参数取向</b>返回新句柄；元组保持钉住。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage flow = new JlImage("real", 640, 480);
	///   JlTuple s = new JlTuple(1.0);
	///   using JlImage k = flow.DerivateVectorField(s, "mean_curvature");
	///   s.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；纯数值元组的 Dispose 是安全的空操作。</para>
	/// </remarks>
	public JlImage DerivateVectorField(JlTuple sigma, string component)
	{
		IntPtr proc = JlNativeApi.PreCall(1423);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, sigma);
		JlNativeApi.StoreS(proc, 1, component);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sigma);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>对 2 通道向量场做高斯导数卷积，抽出指定曲率/散度分量，标量 σ 重载，原生算子 id 1423。</summary>
	/// <param name="sigma">高斯导数核的 σ（像素）。Default: 1.0</param>
	/// <param name="component">要抽取的分量："mean_curvature"、"divergence"、"v" 等由原生侧解释。Default: "mean_curvature"</param>
	/// <returns>新 JlImage 句柄，单通道实数图；输入向量场不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把输入向量场视为 (vx, vy) 场，与高斯的各阶导数核做卷积后按 component 取指定组合。
	///   原生参数序：sigma→0(D)、component→1(S)。本重载用 <c>StoreD</c> 直写 σ，比元组重载少一次钉固定。</para>
	///   <para><b>约束或前提</b>输入需为 2 通道向量场；单通道图会退化 [待实测：是否直接抛错]。
	///   component 的合法集合本层不校验；写错字符串不会在 C# 层报错。</para>
	///   <para><b>与相邻算子的取舍</b>想一次拿多个分量请走 <see cref="DerivateVectorField(JlTuple,string)"/> 元组重载；
	///   只关心向量场模长请走 <see cref="VectorFieldLength(string)"/>。</para>
	///   <para><b>参数取向</b>返回新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage flow = new JlImage("real", 640, 480);
	///   using JlImage k = flow.DerivateVectorField(1.0, "mean_curvature");
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；σ 越大输出越平滑但小尺度结构会被抹掉。</para>
	/// </remarks>
	public JlImage DerivateVectorField(double sigma, string component)
	{
		IntPtr proc = JlNativeApi.PreCall(1423);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreS(proc, 1, component);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把两通道向量场压成单通道标量图，可按长度/角度/分量数选模式，原生算子 id 1424。</summary>
	/// <param name="mode">长度语义："length"（欧氏 |v|）、"angle"（atan2，弧度）或其它由原生侧解释的别名。Default: "length"</param>
	/// <returns>新 JlImage 句柄（单通道实数图）；输入向量场图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>输入必须是 2 通道图像（前两通道被解释为 (vx, vy)）。mode 决定输出的是模长、
	///   方向角还是分量索引 [待实测：本层暴露的 mode 完整集合]。原生参数序：mode→0(S)。</para>
	///   <para><b>约束或前提</b>单通道输入会产生退化输出 [待实测：是否直接报错]。
	///   输出为实数图，直接 Threshold 前需先做量纲归一。</para>
	///   <para><b>与相邻算子的取舍</b>需要沿方向的二阶导数（曲率、发散度）请走 <see cref="DerivateVectorField(double,string)"/>；
	///   只想看模长热力图用本算子的 "length"。</para>
	///   <para><b>参数取向</b>返回新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage flow = new JlImage("real", 640, 480);   // 假设已填入 2 通道光流
	///   using JlImage mag = flow.VectorFieldLength("length");
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄需 Dispose；输入图 this 由 <c>GC.KeepAlive(this)</c> 保活到原生调用结束。</para>
	/// </remarks>
	public JlImage VectorFieldLength(string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(1424);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}





	/// <summary>对图像做 Harris 型角点响应滤波，输出响应强度图，原生算子 id 1428。</summary>
	/// <param name="size">灰度掩膜的滤波器尺寸（整数，通常为奇数）。Default: 3</param>
	/// <param name="weight">Harris 协方差矩阵加权系数 k（越小越偏向特征值之积，越大越偏向差）。Default: 0.04</param>
	/// <returns>新 JlImage 响应强度图（实数域），数值不是坐标而是"角点候选度"；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本方法只做角点响应滤波，不做非极大值抑制也不做筛选。
	///   想要离散角点坐标，需在响应图上再做 <c>Threshold</c> + <c>Connection</c> + <c>RankN</c> 或类似流程。
	///   原生参数序：size→0(I)、weight→1(D)。</para>
	///   <para><b>约束或前提</b>输入应为单通道图；weight 是 Harris 中经典 k，一般 0.04–0.06 之间 [待实测：本层是否校验]。
	///   size 越大响应越平滑但角点定位越粗。</para>
	///   <para><b>与相邻算子的取舍</b>只想找线交点用 <c>LinesGauss</c>+<c>completeJunctions</c>；
	///   本方法适合"先算响应，再自选抑制策略"的流水线。</para>
	///   <para><b>参数取向</b>返回新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage resp = img.CornerResponse(3, 0.04);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；输出为实数图，直接显示可能被截断，需先做灰度线性化。</para>
	/// </remarks>
	public JlImage CornerResponse(int size, double weight)
	{
		IntPtr proc = JlNativeApi.PreCall(1428);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, size);
		JlNativeApi.StoreD(proc, 1, weight);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>对输入图做一层高斯金字塔降采样，返回同尺寸的实数中间层，原生算子 id 1429。</summary>
	/// <param name="mode">核权重模式："weighted"（按邻域权重）或 "uniform"（等权），由原生侧解释。Default: "weighted"</param>
	/// <param name="scale">向下缩放系数（小于 1 表示降采样，0.5 即半分辨率）。Default: 0.5</param>
	/// <returns>新 JlImage 句柄（iconc 输出，可含多通道金字塔层）；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>用高斯预平滑并按 scale 缩放做金字塔降采样，常用于多尺度匹配前的预处理。
	///   原生参数序：mode→0(S)、scale→1(D)。</para>
	///   <para><b>约束或前提</b>输入应为单通道实数或字节图；scale 必须小于 1 才有"金字塔"意义，
	///   ≥1 时退化 [待实测：本层是否拒绝该值]。mode 的合法字符串集合本层不校验。
	///   输出走 iconc 一路句柄，尺寸与通道数由原生决定。</para>
	///   <para><b>与相邻算子的取舍</b>本算子只出一层高斯降采样；要拉普拉斯残差需自行 <c>SubImage</c> 与上一层作差。
	///   只是想去噪，用 <c>BinomialFilter</c> 或 <c>MeanImage</c> 即可。</para>
	///   <para><b>参数取向</b>返回新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage pyr = img.GenGaussPyramid("weighted", 0.5);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄需 Dispose；输入图 this 由 <c>GC.KeepAlive(this)</c> 保活到原生调用结束。</para>
	/// </remarks>
	public JlImage GenGaussPyramid(string mode, double scale)
	{
		IntPtr proc = JlNativeApi.PreCall(1429);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreD(proc, 1, scale);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>在彩色（多通道）图上提取亮/暗线并给出宽度，元组参数重载，原生算子 id 1432。</summary>
	/// <param name="sigma">预平滑 σ 元组。Default: 1.5</param>
	/// <param name="low">滞后阈值下门限元组。Default: 3</param>
	/// <param name="high">滞后阈值上门限元组。Default: 8</param>
	/// <param name="extractWidth">是否给出线宽。Default: "true"</param>
	/// <param name="completeJunctions">是否补全交叉处。Default: "true"</param>
	/// <returns>新 JlXLDCont 句柄（可能包含多轮廓）；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量重载共用 id 1432。sigma/low/high 用 <c>Store</c> 钉 + 结束 <c>UnpinTuple</c>；
	///   其余槽位与形参序一致：extractWidth→3(S)、completeJunctions→4(S)。</para>
	///   <para><b>约束或前提</b>三个元组在 <c>CallProcedure</c> 完成前不得 Dispose；返回后可释放。
	///   只走一组阈值时请改走标量重载（少一次钉固定开销）。输入需为 3 通道彩色图。</para>
	///   <para><b>与相邻算子的取舍</b>灰度线 → <c>LinesGauss</c>；细线定位 → <c>LinesFacet</c>。</para>
	///   <para><b>参数取向</b>返回新句柄；元组参数保持钉住。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage rgb = new JlImage("byte", 640, 480);
	///   JlTuple s = new JlTuple(1.5);
	///   JlTuple lo = new JlTuple(3.0);
	///   JlTuple hi = new JlTuple(8.0);
	///   using JlXLDCont lines = rgb.LinesColor(s, lo, hi, "true", "true");
	///   s.Dispose(); lo.Dispose(); hi.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；纯数值元组的 Dispose 是安全的。</para>
	/// </remarks>
	public JlXLDCont LinesColor(JlTuple sigma, JlTuple low, JlTuple high, string extractWidth, string completeJunctions)
	{
		IntPtr proc = JlNativeApi.PreCall(1432);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, sigma);
		JlNativeApi.Store(proc, 1, low);
		JlNativeApi.Store(proc, 2, high);
		JlNativeApi.StoreS(proc, 3, extractWidth);
		JlNativeApi.StoreS(proc, 4, completeJunctions);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sigma);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在彩色（多通道）图上提取亮/暗线并给出宽度，标量阈值重载，原生算子 id 1432。</summary>
	/// <param name="sigma">预平滑高斯的 σ（像素）。Default: 1.5</param>
	/// <param name="low">滞后阈值下门限（响应幅值）。Default: 3</param>
	/// <param name="high">滞后阈值上门限（响应幅值）。Default: 8</param>
	/// <param name="extractWidth">是否同时给出线宽（"true" / "false"）。Default: "true"</param>
	/// <param name="completeJunctions">是否补全交叉处（"true" / "false"）。Default: "true"</param>
	/// <returns>新 JlXLDCont 句柄，包含检测到的彩色线轮廓（可能附宽度点）；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>在 RGB 或 Lab 等三通道图上跨通道做方向导数并做滞后阈值，专用于彩色对比线
	///   （灰度弱但色度强的边界）。原生参数序：sigma→0(D)、low→1(D)、high→2(D)、
	///   extractWidth→3(S)、completeJunctions→4(S)。</para>
	///   <para><b>约束或前提</b>输入必须是多通道（3 通道）图；单通道图请改走 <c>LinesGauss</c>。
	///   与元组重载共用 id 1432，本重载用 <c>StoreD</c> 直写免去 <c>UnpinTuple</c>。
	///   没有 lightDark 参数，暗线通过阈值符号或预处理反转得到 [待实测：具体方向语义]。</para>
	///   <para><b>与相邻算子的取舍</b>灰度线用 <c>LinesGauss</c>（可指定 lineModel 与 lightDark）；
	///   细线定位优先用 <c>LinesFacet</c>；彩色线才用本算子。</para>
	///   <para><b>参数取向</b>返回新句柄 JlXLDCont。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage rgb = new JlImage("byte", 640, 480);   // 应为 3 通道
	///   using JlXLDCont lines = rgb.LinesColor(1.5, 3.0, 8.0, "true", "true");
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；输入 this 通过 <c>GC.KeepAlive(this)</c> 保活到原生调用结束。</para>
	/// </remarks>
	public JlXLDCont LinesColor(double sigma, double low, double high, string extractWidth, string completeJunctions)
	{
		IntPtr proc = JlNativeApi.PreCall(1432);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreD(proc, 1, low);
		JlNativeApi.StoreD(proc, 2, high);
		JlNativeApi.StoreS(proc, 3, extractWidth);
		JlNativeApi.StoreS(proc, 4, completeJunctions);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用高斯基方向导数提取亮/暗线，可指定线模型与连接补全，元组参数重载，原生算子 id 1433。</summary>
	/// <param name="sigma">预平滑高斯 σ 的元组（允许多组值批处理）。Default: 1.5</param>
	/// <param name="low">滞后阈值下门限元组。Default: 3</param>
	/// <param name="high">滞后阈值上门限元组。Default: 8</param>
	/// <param name="lightDark">"light" 抽亮线、"dark" 抽暗线。Default: "light"</param>
	/// <param name="extractWidth">是否给出线宽（"true" / "false"）。Default: "true"</param>
	/// <param name="lineModel">"bar-shaped"（条形）或 "line"（对称线）。Default: "bar-shaped"</param>
	/// <param name="completeJunctions">是否补全交叉处。Default: "true"</param>
	/// <returns>新 JlXLDCont 句柄（可能包含多轮廓）；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量重载共用原生 id 1433；差别仅在 σ/low/high 的装载：本重载 <c>Store</c> 钉元组、
	///   调用结束再 <c>UnpinTuple</c>。槽位序一致（sigma→0、low→1、high→2，均 DOUBLE/元组槽；
	///   lightDark→3、extractWidth→4、lineModel→5、completeJunctions→6 全 STRING）。</para>
	///   <para><b>约束或前提</b>三个元组在 <c>CallProcedure</c> 完成前必须保持有效，因此不能在方法内提前释放；
	///   元组本身可以在方法返回后 Dispose。若只传一组阈值请走标量重载以免钉固定开销。
	///   原生对元组长度不一致时的行为未在本层校验 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>彩色线 → <c>LinesColor</c>；细线定位优先 → <c>LinesFacet</c>。</para>
	///   <para><b>参数取向</b>返回新句柄；元组参数保持钉住。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlTuple s = new JlTuple(1.5);
	///   JlTuple lo = new JlTuple(3.0);
	///   JlTuple hi = new JlTuple(8.0);
	///   using JlXLDCont lines = img.LinesGauss(s, lo, hi, "light", "true", "bar-shaped", "true");
	///   s.Dispose(); lo.Dispose(); hi.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；纯数值元组的 Dispose 是安全的空操作。</para>
	/// </remarks>
	public JlXLDCont LinesGauss(JlTuple sigma, JlTuple low, JlTuple high, string lightDark, string extractWidth, string lineModel, string completeJunctions)
	{
		IntPtr proc = JlNativeApi.PreCall(1433);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, sigma);
		JlNativeApi.Store(proc, 1, low);
		JlNativeApi.Store(proc, 2, high);
		JlNativeApi.StoreS(proc, 3, lightDark);
		JlNativeApi.StoreS(proc, 4, extractWidth);
		JlNativeApi.StoreS(proc, 5, lineModel);
		JlNativeApi.StoreS(proc, 6, completeJunctions);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sigma);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用高斯基方向导数提取亮/暗线，可指定线模型与连接补全，标量阈值重载，原生算子 id 1433。</summary>
	/// <param name="sigma">预平滑高斯的 σ（像素）。Default: 1.5</param>
	/// <param name="low">滞后阈值下门限（响应幅值）。Default: 3</param>
	/// <param name="high">滞后阈值上门限（响应幅值）。Default: 8</param>
	/// <param name="lightDark">"light" 抽亮线、"dark" 抽暗线。Default: "light"</param>
	/// <param name="extractWidth">是否同时给出线宽（"true" / "false"）。Default: "true"</param>
	/// <param name="lineModel">位置/宽度修正模型："bar-shaped"（条形）或 "line"（亮暗对称线），决定亚像素对齐方式。Default: "bar-shaped"</param>
	/// <param name="completeJunctions">是否在无法直接测得的交叉处补全线段。Default: "true"</param>
	/// <returns>新 JlXLDCont 句柄，包含检测到的线轮廓（含宽度点集，如启用）；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对图像做 σ 高斯平滑，取方向导数并做滞后阈值；lineModel 决定用亮暗不对称的"bar"
	///   还是对称的"line"模型修正亚像素位置与宽度。原生参数序：sigma→0(D)、low→1(D)、high→2(D)、
	///   lightDark→3(S)、extractWidth→4(S)、lineModel→5(S)、completeJunctions→6(S)。</para>
	///   <para><b>约束或前提</b>输入应为单通道实数或字节图。low/high 是响应幅值不是像素灰度。
	///   标量重载用 <c>StoreD</c> 直写，与元组重载共用同一原生算子；若需要多组 σ/阈值批处理请走 JlTuple 重载。
	///   lightDark 与 lineModel 的合法字符串集合本层不校验 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>彩色线用 <c>LinesColor</c>；对细线定位精度更敏感的场景用 <c>LinesFacet</c>。
	///   只要边不用线时可考虑 <c>EdgesSubPix</c> 族。</para>
	///   <para><b>参数取向</b>返回新 JlXLDCont 句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont lines = img.LinesGauss(1.5, 3.0, 8.0, "light", "true", "bar-shaped", "true");
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；输入 this 由 <c>GC.KeepAlive</c> 保活到原生调用结束。</para>
	/// </remarks>
	public JlXLDCont LinesGauss(double sigma, double low, double high, string lightDark, string extractWidth, string lineModel, string completeJunctions)
	{
		IntPtr proc = JlNativeApi.PreCall(1433);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreD(proc, 1, low);
		JlNativeApi.StoreD(proc, 2, high);
		JlNativeApi.StoreS(proc, 3, lightDark);
		JlNativeApi.StoreS(proc, 4, extractWidth);
		JlNativeApi.StoreS(proc, 5, lineModel);
		JlNativeApi.StoreS(proc, 6, completeJunctions);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用 facet 模型提取图像中的亮/暗线（XLD 轮廓输出），元组阈值重载，原生算子 id 1434。</summary>
	/// <param name="maskSize">facet 模型核尺寸（整数）。Default: 5</param>
	/// <param name="low">滞后阈值下门限；JlTuple 允许一次传入多组值。Default: 3</param>
	/// <param name="high">滞后阈值上门限；JlTuple 允许多组值。Default: 8</param>
	/// <param name="lightDark">抽取亮线还是暗线（"light" 或 "dark"）。Default: "light"</param>
	/// <returns>新 JlXLDCont 句柄（可能包含多轮廓）；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量重载同一原生算子（id 1434）。区别：low/high 用 <c>Store</c> 钉元组 +
	///   <c>UnpinTuple</c>；maskSize 仍是 INTEGER 槽 0；lightDark 仍是 STRING 槽 3。
	///   原生侧对元组长度不一致时的行为未在本层校验 [待实测：多阈值是否会展开成多条输出]。</para>
	///   <para><b>约束或前提</b>传入的 JlTuple 在方法返回前不得被外部释放——<c>UnpinTuple</c> 在 <c>CallProcedure</c> 之后。
	///   返回后可 Dispose 元组。若只想传一组阈值，优先走标量重载（无钉固定开销）。</para>
	///   <para><b>与相邻算子的取舍</b>需要"亮度差"型线检测用 <c>LinesGauss</c>；彩色线用 <c>LinesColor</c>。
	///   本族 facet 模型对细线定位精度高但对噪声更敏感。</para>
	///   <para><b>参数取向</b>返回新句柄 JlXLDCont；元组参数保持钉住。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlTuple low = new JlTuple(3.0);
	///   JlTuple high = new JlTuple(8.0);
	///   using JlXLDCont lines = img.LinesFacet(5, low, high, "light");
	///   low.Dispose();
	///   high.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>低/高元组纯数值时无需 Dispose 也无害（Dispose 只处理句柄类元素），
	///   但显式释放更符合规范。返回值是新句柄。</para>
	/// </remarks>
	public JlXLDCont LinesFacet(int maskSize, JlTuple low, JlTuple high, string lightDark)
	{
		IntPtr proc = JlNativeApi.PreCall(1434);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSize);
		JlNativeApi.Store(proc, 1, low);
		JlNativeApi.Store(proc, 2, high);
		JlNativeApi.StoreS(proc, 3, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用 facet 模型提取图像中的亮/暗线（XLD 轮廓输出），标量阈值重载，原生算子 id 1434。</summary>
	/// <param name="maskSize">facet 模型核尺寸（整数，典型奇数）。Default: 5</param>
	/// <param name="low">滞后阈值的下门限（响应幅值，低于此值丢弃）。Default: 3</param>
	/// <param name="high">滞后阈值的上门限（响应幅值，高于此值必留，之间者需与高响应连通）。Default: 8</param>
	/// <param name="lightDark">抽取亮线还是暗线（"light" 或 "dark"）。Default: "light"</param>
	/// <returns>新 JlXLDCont 句柄，包含提取到的线轮廓；输入图不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>facet 模型在高斯基上拟合二阶方向导数并做非极大值抑制，输出线状响应的轮廓集。
	///   原生参数序与 C# 形参序一致：maskSize→0(I)、low→1(D)、high→2(D)、lightDark→3(S)。</para>
	///   <para><b>约束或前提</b>输入应为单通道实数或字节图。low 与 high 都是灰度响应量纲，
	///   不是图像像素灰度；对高噪声图 low 太低会抽出大量毛刺。
	///   标量重载用 <c>StoreD</c> 直写，不做 <c>UnpinTuple</c>，比 JlTuple 重载少一次钉固定开销；
	///   若需要多阈值批处理请走 JlTuple 重载。</para>
	///   <para><b>与相邻算子的取舍</b>需要"亮度差"型线检测用 <c>LinesGauss</c>（可指定 bar-shaped/line 模型），
	///   彩色图上的线用 <c>LinesColor</c>。facet 模型对纹理敏感但对细线定位精度更高。</para>
	///   <para><b>参数取向</b>返回新 JlXLDCont 句柄，需调用方 Dispose；this 与输入不变。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont lines = img.LinesFacet(5, 3.0, 8.0, "light");
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；实现末尾 <c>GC.KeepAlive(this)</c> 保证原生调用期内输入不被释放，
	///   方法返回后输入即可 Dispose。</para>
	/// </remarks>
	public JlXLDCont LinesFacet(int maskSize, double low, double high, string lightDark)
	{
		IntPtr proc = JlNativeApi.PreCall(1434);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskSize);
		JlNativeApi.StoreD(proc, 1, low);
		JlNativeApi.StoreD(proc, 2, high);
		JlNativeApi.StoreS(proc, 3, lightDark);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用元组形式的核矩阵在空间域生成实数滤波掩膜图像，原生算子 id 1435。</summary>
	/// <param name="filterMask">核数据打包成的 JlTuple（可以是数值元组序列，或单元素字符串元组指向命名核）。</param>
	/// <param name="scale">核权重的整体缩放因子。Default: 1.0</param>
	/// <param name="width">输出掩膜图像的列数。Default: 512</param>
	/// <param name="height">输出掩膜图像的行数。Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 <see cref="GenFilterMask(string,double,int,int)"/> 同一原生算子（id 1435），
	///   区别仅在 filterMask 的装载方式：本重载用 <c>JlNativeApi.Store</c> 钉住元组，
	///   调用完成后再 <c>UnpinTuple</c>；而 string 重载用 <c>StoreS</c> 直接写字符指针。
	///   其余槽位与形参序一致：scale→1(D)、width→2(I)、height→3(I)。</para>
	///   <para><b>约束或前提</b>本方法先 <c>Dispose()</c> 再 <c>Load()</c>，调用后 this 变成新掩膜图（原地改写）。
	///   传入的 JlTuple 在整个原生调用期间必须保持有效——<c>UnpinTuple</c> 位于 <c>CallProcedure</c> 之后，
	///   元组本身可以在方法返回后 Dispose。width/height 决定画布尺寸，不决定核尺寸。</para>
	///   <para><b>与相邻算子的取舍</b>只是"gauss"这类命名核时优先走 string 重载（零拷贝，也不钉元组）；
	///   仅当核需要以元组序列显式给出（如批量参数化核、或核内容随运行时决定）时才走本重载。</para>
	///   <para><b>参数取向</b>void + 原地改写；无返回值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage mask = new JlImage();
	///   JlTuple gauss = new JlTuple("gauss");
	///   mask.GenFilterMask(gauss, 1.0, 512, 512);
	///   gauss.Dispose();
	///   mask.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原地改写：反复调用同一路径会 Dispose 上一版；JlTuple 若为纯数值元组
	///   不需要 Dispose（Dispose 只处理句柄类元素），但当 filterMask 内含句柄型元素时应在使用完后释放。</para>
	/// </remarks>
	public void GenFilterMask(JlTuple filterMask, double scale, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1435);
		JlNativeApi.Store(proc, 0, filterMask);
		JlNativeApi.StoreD(proc, 1, scale);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(filterMask);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>用命名核（文件路径或内置关键字）在空间域生成实数滤波掩膜图像，原生算子 id 1435。</summary>
	/// <param name="filterMask">核来源：文件名（如 "gauss"、"binomial"）或外部核图像路径。Default: "gauss"</param>
	/// <param name="scale">核权重的整体缩放因子。Default: 1.0</param>
	/// <param name="width">输出掩膜图像（画布）的列数。Default: 512</param>
	/// <param name="height">输出掩膜图像（画布）的行数。Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b>把一张指定的空间域核铺到 width×height 的实数图上（核在中心，其余补零），
	///   供频域滤波流程中"先转频域再相乘"或作为自定义核参与卷积使用。原生参数序与 C# 形参序一致：
	///   filterMask→0(S)、scale→1(D)、width→2(I)、height→3(I)。</para>
	///   <para><b>约束或前提</b>本方法先 <c>Dispose()</c> 再 <c>Load()</c>：调用前 this 若持有旧句柄会被释放；
	///   调用后 this 指向新掩膜图。这是原地改写语义，不返回新句柄。
	///   filterMask 是文件路径还是内置关键字由原生侧解释；写错名字不会在 C# 层报错 [待实测：合法关键字集合]。
	///   width/height 通常应不小于核本身尺寸，否则核会被截断。</para>
	///   <para><b>与相邻算子的取舍</b>已知参数化核（高斯/均值）优先用 <c>GenGaussFilter</c>/<c>GenMeanFilter</c> 直接生成，
	///   本算子更适合"手上有自定义核文件"的场景。若两个同名重载并存，用 string 走本方法，
	///   用元组表达核矩阵则走 <see cref="GenFilterMask(JlTuple,double,int,int)"/>。</para>
	///   <para><b>参数取向</b>void + 原地改写；无返回值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage mask = new JlImage();
	///   mask.GenFilterMask("gauss", 1.0, 512, 512);
	///   // mask 现在是 512×512 的实数图，中间嵌有一枚高斯核。
	///   mask.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原地改写：反复调用同一路径会持续 Dispose 上一版句柄，避免与外部持有者共用同一 JlImage；
	///   scale 只影响数值幅度，不改变核的空间展布。</para>
	/// </remarks>
	public void GenFilterMask(string filterMask, double scale, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1435);
		JlNativeApi.StoreS(proc, 0, filterMask);
		JlNativeApi.StoreD(proc, 1, scale);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>在频域图上放置一个椭圆/矩形的均值（低通）核，供与 FFTImage 逐点相乘，原生算子 id 1436。</summary>
	/// <param name="maskShape">滤波器核在空间域的支撑形状（如 "ellipse"、"rect"），由原生侧解释。Default: "ellipse"</param>
	/// <param name="diameter1">主方向上的核直径（像素，空间域量纲）。Default: 11.0</param>
	/// <param name="diameter2">垂直于主方向的核直径（像素）。Default: 11.0</param>
	/// <param name="phi">主方向与水平轴的夹角，弧度，不是角度 [待实测：本层未做弧度/角度校验]。Default: 0.0</param>
	/// <param name="norm">归一化模式，决定核的能量（如 "none"、"norm"）；影响输出图的直流增益。Default: "none"</param>
	/// <param name="mode">DC 在频域图中的位置（"dc_center" 或 "dc_corner"），必须与将被滤波的 FFT 图一致。Default: "dc_center"</param>
	/// <param name="width">滤波器图的列数，应与被滤波 FFT 图的宽一致。Default: 512</param>
	/// <param name="height">滤波器图的行数，应与被滤波 FFT 图的高一致。Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b>按给定的直径与主方向，在 width×height 的实数频域图中生成一枚均值核。
	///   原生参数序与 C# 形参序严格一致：maskShape→0(S)、diameter1→1(D)、diameter2→2(D)、phi→3(D)、
	///   norm→4(S)、mode→5(S)、width→6(I)、height→7(I)。</para>
	///   <para><b>约束或前提</b>本方法先 <c>Dispose()</c> 再 <c>Load()</c>：调用前 this 若持有旧句柄会被释放，
	///   调用后 this 指向新构造的频域滤波器图（原地改写语义，不是新句柄返回值）。
	///   width/height 必须与被滤波 FFT 图像尺寸完全一致，否则与频域图相乘会因维度不匹配而报错。
	///   maskShape、norm、mode 都是透传字符串，本层不做取值校验 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>想要"温和去噪、无振铃"用 <c>GenGaussFilter</c>（高斯核没有 ringing）；
	///   只想空间域均值，直接 <c>MeanImage(int,int)</c> 更快，不必进频域。均值核锐截止但会在边缘附近产生
	///   周期性过冲；对纹理敏感图不宜作为默认。</para>
	///   <para><b>参数取向</b>无返回值，void + 原地改写；输入完全靠参数描述。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage filt = new JlImage();
	///   filt.GenMeanFilter("ellipse", 11.0, 11.0, 0.0, "none", "dc_center", 512, 512);
	///   // filt 现为 512×512 频域均值核，后续可与频域图逐点相乘；用完自行 Dispose。
	///   filt.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原地改写：调用即令 this 成为新滤波器句柄；对同一路 this 反复调用会不断 Dispose 上一版。
	///   mode 与被滤波图不一致时 DC 位置错位会引入整幅亮度偏移，本层不主动校验。
	///   <c>GC.KeepAlive(this)</c> 只保证原生调用期内句柄不被回收，方法返回后旧句柄已释放无需再处置。</para>
	/// </remarks>
	public void GenMeanFilter(string maskShape, double diameter1, double diameter2, double phi, string norm, string mode, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1436);
		JlNativeApi.StoreS(proc, 0, maskShape);
		JlNativeApi.StoreD(proc, 1, diameter1);
		JlNativeApi.StoreD(proc, 2, diameter2);
		JlNativeApi.StoreD(proc, 3, phi);
		JlNativeApi.StoreS(proc, 4, norm);
		JlNativeApi.StoreS(proc, 5, mode);
		JlNativeApi.StoreI(proc, 6, width);
		JlNativeApi.StoreI(proc, 7, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>在频域生成一枚二维高斯（低通）核图像并原地写入 this，原生算子 id 1437。</summary>
	/// <param name="sigma1">高斯核沿主方向的 σ，空间域像素量纲。Default: 1.0</param>
	/// <param name="sigma2">高斯核垂直于主方向的 σ，空间域像素量纲。Default: 1.0</param>
	/// <param name="phi">核主方向与水平轴的夹角，弧度 [待实测：本层未校验弧度/角度]。Default: 0.0</param>
	/// <param name="norm">核的归一化模式字符串，决定输出核图的直流增益。Default: "none"</param>
	/// <param name="mode">DC 项在频域图中的位置（如 "dc_center" 或 "dc_corner"），须与将被滤波的频域图一致。Default: "dc_center"</param>
	/// <param name="width">输出核图像的列数。Default: 512</param>
	/// <param name="height">输出核图像的行数。Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b>按 σ1/σ2/phi 在 width×height 的实数图上铺一枚频域高斯核。方法体先 <c>Dispose()</c>
	///   再 <c>Load()</c>：结果原地写入 this（无返回值），且不调用任何输入端 <c>Store</c>——它是纯生成器，
	///   不吃输入图像。原生参数序与 C# 形参序一致：sigma1→0(D)、sigma2→1(D)、phi→2(D)、norm→3(S)、
	///   mode→4(S)、width→5(I)、height→6(I)。</para>
	///   <para><b>约束或前提</b>this 必须是可占用的独立 JlImage：调用会释放其原有句柄。norm 与 mode 是透传字符串，
	///   本层不校验取值。本库未封装 FFTImage/IFTImage 一类频域变换算子（全库检索无声明），
	///   生成的频域核在 C# 层的直接消费方有限 [待实测：核图像的实际用途边界]。</para>
	///   <para><b>与相邻算子的取舍</b>空间域平滑直接 <c>GaussImage(int)</c> 或 <c>MeanImage(int,int)</c>，不必进频域；
	///   频域均值核用 <c>GenMeanFilter</c>；铺自定义空间核用 <c>GenFilterMask</c>；频域微分核用 <c>GenDerivativeFilter</c>。
	///   高斯核无振铃，锐截止的均值核会在边缘附近产生过冲。</para>
	///   <para><b>参数取向</b>void + 原地改写；无返回值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage filt = new JlImage();
	///   filt.GenGaussFilter(1.0, 1.0, 0.0, "none", "dc_center", 512, 512);
	///   // filt 现在是一张 512×512 的频域高斯核图（实数型）。
	///   filt.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>对同一 this 反复调用会持续 Dispose 上一版，勿与其他持有者共用同一 JlImage 对象；
	///   σ 为 0 或负值的行为由原生侧决定 [待实测]。</para>
	/// </remarks>
	public void GenGaussFilter(double sigma1, double sigma2, double phi, string norm, string mode, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1437);
		JlNativeApi.StoreD(proc, 0, sigma1);
		JlNativeApi.StoreD(proc, 1, sigma2);
		JlNativeApi.StoreD(proc, 2, phi);
		JlNativeApi.StoreS(proc, 3, norm);
		JlNativeApi.StoreS(proc, 4, mode);
		JlNativeApi.StoreI(proc, 5, width);
		JlNativeApi.StoreI(proc, 6, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>在频域生成一枚微分（方向导数）核图像并原地写入 this，原生算子 id 1438。</summary>
	/// <param name="derivative">要计算的微分方向/类型标识字符串。Default: "x"</param>
	/// <param name="exponent">逆变换时使用的指数。Default: 1</param>
	/// <param name="norm">核的归一化模式字符串。Default: "none"</param>
	/// <param name="mode">DC 项在频域图中的位置（如 "dc_center" 或 "dc_corner"），须与将被滤波的频域图一致。Default: "dc_center"</param>
	/// <param name="width">输出核图像的列数。Default: 512</param>
	/// <param name="height">输出核图像的行数。Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b>按指定微分方向在 width×height 的实数频域图中生成微分核。方法体先 <c>Dispose()</c>
	///   再 <c>Load()</c>，原地改写 this；无输入端 <c>Store</c>，是纯生成器。原生参数序与形参序一致：
	///   derivative→0(S)、exponent→1(I)、norm→2(S)、mode→3(S)、width→4(I)、height→5(I)。</para>
	///   <para><b>约束或前提</b>derivative 除 "x" 外的合法取值（如 "y"、"xy" 之类写法）本层不校验 [待实测]；
	///   exponent 传负值或非期望值的行为由原生侧决定 [待实测]。本库未封装 FFTImage 族频域变换算子，
	///   此核的下游消费需自行处理。</para>
	///   <para><b>与相邻算子的取舍</b>只要梯度幅值图直接 <c>SobelAmp</c>；要亚像素边缘轮廓用 <c>EdgesSubPix</c>；
	///   空间域二阶微分用 <c>Laplace</c>。若流程本就在空域，没必要为微分开本算子生成频域核。</para>
	///   <para><b>参数取向</b>void + 原地改写；无返回值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage filt = new JlImage();
	///   filt.GenDerivativeFilter("x", 1, "none", "dc_center", 512, 512);
	///   filt.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>与 <c>GenGaussFilter</c> 同为"生成即占用"语义：调用后 this 即新核图、旧句柄已释放，
	///   外部不得再持有该对象的旧引用。</para>
	/// </remarks>
	public void GenDerivativeFilter(string derivative, int exponent, string norm, string mode, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1438);
		JlNativeApi.StoreS(proc, 0, derivative);
		JlNativeApi.StoreI(proc, 1, exponent);
		JlNativeApi.StoreS(proc, 2, norm);
		JlNativeApi.StoreS(proc, 3, mode);
		JlNativeApi.StoreI(proc, 4, width);
		JlNativeApi.StoreI(proc, 5, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

























	/// <summary>对整幅图做直方图均衡（灰度线性化），返回新图像句柄，原生算子 id 1469。</summary>
	/// <returns>新 JlImage 句柄，灰度分布被摊到整个可用区间；输入 this 不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>按输入图的灰度统计做非线性映射，使输出直方图近似均匀（英文原文：Histogram
	///   linearization of images / Image with linearized gray values）。输入经 <c>Store(proc, 1)</c> 提交，
	///   输出用 <c>LoadNew</c> 装载——返回新句柄，不是原地改写。</para>
	///   <para><b>约束或前提</b>对输入类型/通道的要求由原生侧校验，本层不拦截 [待实测]。映射基于全图统计：
	///   背景占比过大的图会把目标灰度挤到窄区间，必要时先 <c>ReduceDomain(JlRegion)</c> 限定统计范围
	///   [待实测：统计是否只计 domain 内像素]。</para>
	///   <para><b>与相邻算子的取舍</b>只想把现有极值线性拉到 0–255、保持灰度次序与量值可解释性，用
	///   <c>ScaleImageMax</c>；要压平全局直方图（牺牲灰度量值含义）才用本算子；局部细节发虚用 <c>Emphasize</c>，
	///   亮度不均用 <c>Illuminate</c>。标定/测灰度类流程不宜做均衡。</para>
	///   <para><b>参数取向</b>返回新句柄；this 与输入数据不变。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage eq = img.EquHistoImage();
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄需释放；实现末尾 <c>GC.KeepAlive(this)</c> 保输入到原生调用结束，
	///   方法返回后输入即可 Dispose。</para>
	/// </remarks>
	public JlImage EquHistoImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1469);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用低通核估计光照并加性补偿，校正亮度不均，返回新图像句柄，原生算子 id 1470。</summary>
	/// <param name="maskWidth">低通核宽度（像素）。Default: 101</param>
	/// <param name="maskHeight">低通核高度（像素）。Default: 101</param>
	/// <param name="factor">"补偿灰度值"叠加回原图时的缩放系数（英文原文：Scales the correction gray value added to the original gray values）。Default: 0.7</param>
	/// <returns>亮度校正后的新 JlImage 句柄；输入 this 不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>以 maskWidth×maskHeight 的低通核刻画图像的缓变光照分量，把修正量乘 factor 后
	///   加回原值得到输出。原生参数序与形参序一致：maskWidth→0(I)、maskHeight→1(I)、factor→2(D)；
	///   输出 <c>LoadNew</c> 新句柄。</para>
	///   <para><b>约束或前提</b>核尺寸应接近光照不均的空间尺度且明显大于待保留结构，核太小会把有效细节当光照抹掉。
	///   factor 合法范围本层不校验，过补偿会放大噪声 [待实测]。输入类型/通道要求由原生侧校验 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>与 <c>Emphasize</c> 的参数布局完全相同（宽、高、factor）但方向相反：
	///   本算子处理低频亮度梯度，Emphasize 放大高频细节；分不清问题时先看它是"整幅偏暗/局部阴影"还是"纹理发虚"。
	///   全图对比度不足优先 <c>ScaleImageMax</c> 或 <c>EquHistoImage</c>。</para>
	///   <para><b>参数取向</b>返回新句柄；this 不变。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage lit = img.Illuminate(101, 101, 0.7);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；默认核 101×101 对小于该尺度图会失真 [待实测：核大于图像时的行为]。</para>
	/// </remarks>
	public JlImage Illuminate(int maskWidth, int maskHeight, double factor)
	{
		IntPtr proc = JlNativeApi.PreCall(1470);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreD(proc, 2, factor);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>以低通核为参考增强局部对比（高频强调），返回新图像句柄，原生算子 id 1471。</summary>
	/// <param name="maskWidth">低通核宽度（像素），决定"低频参考"的空间尺度。Default: 7</param>
	/// <param name="maskHeight">低通核高度（像素）。Default: 7</param>
	/// <param name="factor">对比强调强度（英文原文：Intensity of contrast emphasis）。Default: 1.0</param>
	/// <returns>对比增强后的新 JlImage 句柄；输入 this 不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>用 maskWidth×maskHeight 的低通核构造强调滤波器，factor 控制增强力度
	///   （英文文档原文：Enhance contrast of the image）。是否为"原图 + factor×高频分量"的经典强调公式
	///   本层不可见 [待实测]。原生参数序：maskWidth→0(I)、maskHeight→1(I)、factor→2(D)，输出 <c>LoadNew</c>。</para>
	///   <para><b>约束或前提</b>核越大、被回加的"高频"越粗；factor 越大噪声同步放大。增强结果若超出目标灰度
	///   区间如何截断由原生决定 [待实测]。输入类型/通道要求由原生侧校验 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>阴影式亮度不均用 <c>Illuminate</c>（同参数布局、补低频）而不是本算子；
	///   只想全局拉开动态用 <c>ScaleImageMax</c>（线性、不增噪声）；去噪方向相反，用 <c>MeanImage</c>/<c>GaussImage</c>。</para>
	///   <para><b>参数取向</b>返回新句柄；this 不变。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage emph = img.Emphasize(7, 7, 1.0);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；对含噪图先滤波再强调，否则 factor 会同时放大噪声。</para>
	/// </remarks>
	public JlImage Emphasize(int maskWidth, int maskHeight, double factor)
	{
		IntPtr proc = JlNativeApi.PreCall(1471);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, maskWidth);
		JlNativeApi.StoreI(proc, 1, maskHeight);
		JlNativeApi.StoreD(proc, 2, factor);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把全图灰度线性拉伸铺满 0–255 区间，返回新图像句柄，原生算子 id 1472。</summary>
	/// <returns>拉伸后的新 JlImage 句柄；输入 this 不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>以全图灰度最小/最大值做端点做线性映射，把动态范围铺满 0 到 255
	///   （英文原文：Maximum gray value spreading in the value range 0 to 255）。只改对比、不改灰度次序。
	///   输入 <c>Store(proc, 1)</c>，输出 <c>LoadNew</c> 新句柄；输出图像类型 [待实测：是否恒为 byte]。</para>
	///   <para><b>约束或前提</b>基于全局极值：一个孤立噪声点就能把有效灰度区间压窄、拉伸近乎失效，
	///   这类图先滤波或先 <c>ReduceDomain(JlRegion)</c> 再拉伸 [待实测：统计是否只计 domain 内像素]。
	///   本算子无参数，无法手工控制斜率。</para>
	///   <para><b>与相邻算子的取舍</b>需要精确的斜率与偏移（灰度标定、复现性要求）用 <c>ScaleImage(double,double)</c>；
	///   需要非线性摊平直方图用 <c>EquHistoImage</c>；本算子适合"极值即真实动态"的干净图。</para>
	///   <para><b>参数取向</b>返回新句柄；this 不变。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage stretched = img.ScaleImageMax();
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；拉伸后灰度量值不再对应原始物理量，标定流程慎用。</para>
	/// </remarks>
	public JlImage ScaleImageMax()
	{
		IntPtr proc = JlNativeApi.PreCall(1472);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}









	/// <summary>Sobel 算子求边缘幅值图，核尺寸走元组可批量传值，返回新句柄，原生算子 id 1481。</summary>
	/// <param name="filterType">梯度合成方式字符串。Default: "sum_abs"</param>
	/// <param name="size">Sobel 核尺寸元组（典型 3），允许多值一次批处理。Default: 3</param>
	/// <returns>新 JlImage 句柄，边缘幅值（梯度模）图；输入 this 不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量重载共用原生 id 1481；差别仅在 size 的装载：本重载 <c>Store</c> 钉元组、
	///   <c>CallProcedure</c> 之后 <c>UnpinTuple</c>，标量重载用 <c>StoreI</c> 直写。槽位序一致：
	///   filterType→0(S)、size→1。</para>
	///   <para><b>约束或前提</b>传入的 JlTuple 在原生调用结束前不得被外部释放，方法返回后可 Dispose。
	///   多值 size 是否展开为多幅输出、filterType 合法集合本层均不校验 [待实测]。只传一组尺寸请走标量重载以免钉固定开销。</para>
	///   <para><b>与相邻算子的取舍</b>需要边缘方向用 <c>SobelDir</c>；亚像素边缘轮廓用 <c>EdgesSubPix</c>；
	///   更快的粗筛用 <c>Roberts</c>。</para>
	///   <para><b>参数取向</b>返回新句柄；元组参数保持钉住。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlTuple size = new JlTuple(3);
	///   using JlImage amp = img.SobelAmp("sum_abs", size);
	///   size.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；纯数值元组的 Dispose 是安全操作。</para>
	/// </remarks>
	public JlImage SobelAmp(string filterType, JlTuple size)
	{
		IntPtr proc = JlNativeApi.PreCall(1481);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filterType);
		JlNativeApi.Store(proc, 1, size);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(size);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>Sobel 算子求边缘幅值图（梯度模），标量核尺寸重载，返回新句柄，原生算子 id 1481。</summary>
	/// <param name="filterType">梯度合成方式字符串。Default: "sum_abs"</param>
	/// <param name="size">Sobel 核尺寸（像素，典型 3）。Default: 3</param>
	/// <returns>新 JlImage 句柄，边缘幅值（梯度模）图；输入 this 不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>水平/垂直一阶差分求梯度再按 filterType 合成幅值图。原生参数序与形参序一致：
	///   filterType→0(S)、size→1(I)；size 用 <c>StoreI</c> 直写，与 JlTuple 重载共用 id 1481，但少一次
	///   钉固定元组的开销。输出 <c>LoadNew</c> 新句柄。</para>
	///   <para><b>约束或前提</b>幅值图的量纲是梯度响应、不是原图灰度，把原图的阈值经验直接套到它上面会选错门限。
	///   size 与 filterType 的合法取值本层不校验 [待实测]。重载解析：整数字面量（如 3）绑定本重载；
	///   要批处理多组尺寸须显式传 <c>new JlTuple(...)</c>。</para>
	///   <para><b>与相邻算子的取舍</b>还要梯度方向（后续按方向筛边）用 <c>SobelDir</c>；亚像素边缘用 <c>EdgesSubPix</c>；
	///   二阶微分零交叉用 <c>Laplace</c>。Sobel 核自带平滑，比 <c>Roberts</c> 抗噪、比 <c>Laplace</c> 稳。</para>
	///   <para><b>参数取向</b>返回新句柄；this 不变。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage amp = img.SobelAmp("sum_abs", 3);
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；<c>GC.KeepAlive(this)</c> 保输入到原生调用结束，返回后输入可 Dispose。</para>
	/// </remarks>
	public JlImage SobelAmp(string filterType, int size)
	{
		IntPtr proc = JlNativeApi.PreCall(1481);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filterType);
		JlNativeApi.StoreI(proc, 1, size);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>Sobel 一次调用同时输出边缘幅值图与方向图，核尺寸元组重载，原生算子 id 1482。</summary>
	/// <param name="edgeDirection">输出参数：新 JlImage 句柄，梯度方向图。</param>
	/// <param name="filterType">梯度合成方式字符串。Default: "sum_abs"</param>
	/// <param name="size">Sobel 核尺寸元组（典型 3），允许多值。Default: 3</param>
	/// <returns>新 JlImage 句柄，边缘幅值（梯度模）图；输入 this 不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量重载共用 id 1482；本重载 size 用 <c>Store</c> 钉元组、调用后 <c>UnpinTuple</c>。
	///   方法体做了两次 <c>InitOCT</c>（输出槽 1、槽 2）：幅值图从槽 1 <c>LoadNew</c> 作返回值，方向图从槽 2
	///   <c>LoadNew</c> 写进 out 参数——这是它与 <c>SobelAmp</c>（只装载槽 1）的本质区别。槽位序：filterType→0(S)、size→1。</para>
	///   <para><b>约束或前提</b>JlTuple 在原生调用结束前不得外部释放，返回后可 Dispose。方向图像素的编码
	///   （角度直接写入还是灰度量化、单位弧度/度）本层不可见 [待实测]。只传一组尺寸请走标量重载。</para>
	///   <para><b>与相邻算子的取舍</b>不需要方向时用 <c>SobelAmp</c>，少一个输出句柄与装载；亚像素轮廓用 <c>EdgesSubPix</c>。</para>
	///   <para><b>参数取向</b>返回值 + out 各一个新句柄，两个都需释放。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlTuple size = new JlTuple(3);
	///   JlImage amp = img.SobelDir(out JlImage dir, "sum_abs", size);
	///   size.Dispose();
	///   amp.Dispose();
	///   dir.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>out 方向图常被忘 Dispose，是隐性泄漏点；out 实参必须写 <c>out</c>（漏写报 CS1615）。</para>
	/// </remarks>
	public JlImage SobelDir(out JlImage edgeDirection, string filterType, JlTuple size)
	{
		IntPtr proc = JlNativeApi.PreCall(1482);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filterType);
		JlNativeApi.Store(proc, 1, size);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(size);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out edgeDirection);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>Sobel 同时输出边缘幅值图与方向图，标量核尺寸重载，原生算子 id 1482。</summary>
	/// <param name="edgeDirection">输出参数：新 JlImage 句柄，梯度方向图。</param>
	/// <param name="filterType">梯度合成方式字符串。Default: "sum_abs"</param>
	/// <param name="size">Sobel 核尺寸（像素，典型 3）。Default: 3</param>
	/// <returns>新 JlImage 句柄，边缘幅值（梯度模）图；输入 this 不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>一次原生调用产出两幅新图：返回值是幅值图（输出槽 1），out 参数是方向图（槽 2），
	///   方法体两次 <c>InitOCT</c>、两次 <c>LoadNew</c>。原生参数序 filterType→0(S)、size→1(I)，size 用
	///   <c>StoreI</c> 直写，与元组重载共用 id 1482 但无钉固定开销。</para>
	///   <para><b>约束或前提</b>重载解析：整数字面量绑定本重载，显式 <c>new JlTuple(...)</c> 才走元组重载；
	///   out 实参必须带 <c>out</c> 关键字。方向图的单位与编码本层不可见 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要幅值用 <c>SobelAmp(string,int)</c>，省一次输出装载与一个待释放句柄；
	///   方向仅在为"按梯度角筛边"收集信息时才值得多取一路输出。</para>
	///   <para><b>参数取向</b>返回新句柄 + out 新句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlImage amp = img.SobelDir(out JlImage dir, "sum_abs", 3);
	///   amp.Dispose();
	///   dir.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两路输出都是新句柄都要释放；<c>GC.KeepAlive(this)</c> 保输入到调用结束，返回后输入可 Dispose。</para>
	/// </remarks>
	public JlImage SobelDir(out JlImage edgeDirection, string filterType, int size)
	{
		IntPtr proc = JlNativeApi.PreCall(1482);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filterType);
		JlNativeApi.StoreI(proc, 1, size);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out edgeDirection);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>Roberts 对角差分检测边缘，仅一个合成方式参数，返回新句柄，原生算子 id 1483。</summary>
	/// <param name="filterType">梯度合成方式字符串。Default: "gradient_sum"</param>
	/// <returns>Roberts 滤波结果的新 JlImage 句柄（英文原文：Roberts-filtered result images）；输入 this 不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>用最简对角邻域差分（经典 Roberts 核 2×2 [待实测：本层仅透传参数]）求边缘响应。
	///   原生只有一个字符串槽：filterType→0(S)；没有核尺寸参数，平滑程度无法调节。输出 <c>LoadNew</c> 新句柄。</para>
	///   <para><b>约束或前提</b>差分不含平滑，噪声会直接进响应 [待实测：抗噪对比]；filterType 合法集合本层不校验。
	///   对噪声大的图不要用本算子，换带平滑的 <c>SobelAmp</c>。</para>
	///   <para><b>与相邻算子的取舍</b>要边缘方向用 <c>SobelDir</c>；要二阶微分/零交叉用 <c>Laplace</c>；
	///   要亚像素轮廓用 <c>EdgesSubPix</c>。本算子的价值在"图像干净、只要能最快的一阶响应"的粗筛。</para>
	///   <para><b>参数取向</b>返回新句柄；this 不变。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage res = img.Roberts("gradient_sum");
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；"想要更平滑"在本算子上无参数可调，只能换算子。</para>
	/// </remarks>
	public JlImage Roberts(string filterType)
	{
		IntPtr proc = JlNativeApi.PreCall(1483);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filterType);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>有限差分 Laplace 二阶微分，核尺寸元组重载，返回新句柄，原生算子 id 1484。</summary>
	/// <param name="resultType">结果图像类型；结果为 byte 或 uint2 时输出取绝对值。Default: "absolute"</param>
	/// <param name="maskSize">差分核尺寸元组（典型 3）。Default: 3</param>
	/// <param name="filterMask">所用的 Laplace 差分核标识字符串。Default: "n_4"</param>
	/// <returns>新 JlImage 句柄（Laplace 结果图）；输入 this 不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量重载共用 id 1484；本重载 maskSize 用 <c>Store</c> 钉元组、
	///   <c>CallProcedure</c> 后 <c>UnpinTuple</c>。槽位序一致：resultType→0(S)、maskSize→1、filterMask→2(S)。</para>
	///   <para><b>约束或前提</b>关键坑（英文参数原文）：resultType 为 byte 或 uint2 时输出取绝对值——负响应被折正，
	///   边缘过零两侧都呈亮值，亮边与暗边的符号信息丢失。二阶微分强烈放高频，噪声图必须先平滑再用。
	///   resultType 与 filterMask 的合法集合本层不校验 [待实测]；多值元组的行为未校验 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要可用的边缘幅值走一阶的 <c>SobelAmp</c>/<c>Roberts</c>；
	///   频域微分核走 <c>GenDerivativeFilter</c>。Laplace 的价值在零交叉定位与（若 resultType 允许）带符号响应。</para>
	///   <para><b>参数取向</b>返回新句柄；元组参数保持钉住。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlTuple maskSize = new JlTuple(3);
	///   using JlImage lap = img.Laplace("absolute", maskSize, "n_4");
	///   maskSize.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值是新句柄；纯数值元组 Dispose 是安全操作；只传一组尺寸请走标量重载以免钉固定开销。</para>
	/// </remarks>
	public JlImage Laplace(string resultType, JlTuple maskSize, string filterMask)
	{
		IntPtr proc = JlNativeApi.PreCall(1484);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, resultType);
		JlNativeApi.Store(proc, 1, maskSize);
		JlNativeApi.StoreS(proc, 2, filterMask);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maskSize);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用有限差分算拉普拉斯（二阶导数）算子，原生算子 id 1484。掩模取法与是否取绝对值由字符串参数决定。</summary>
	/// <param name="resultType">结果图像类型策略：取 "absolute" 时对 byte/uint2 结果取绝对值（负响应折成正值），否则保留带符号响应。Default: "absolute"</param>
	/// <param name="maskSize">滤波掩模尺寸（奇数，越大频带越宽）。Default: 3</param>
	/// <param name="filterMask">拉普拉斯掩模系数形式，如 "n_4"（四邻域）、"n_8"（八邻域）等字符串常量。Default: "n_4"</param>
	/// <returns>拉普拉斯滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对输入灰度图做二阶导数（拉普拉斯）卷积，突出灰度突变的边缘环，恒定区输出 0。
	///   掩模系数由 <paramref name="filterMask"/> 命名（"n_4" 四邻域、"n_8" 八邻域等），<paramref name="maskSize"/> 控制核尺寸。</para>
	///   <para><b>与相邻算子的取舍</b>本算子不做平滑，直接对原始灰度求二阶导，单像素噪声会被剧烈放大；
	///   若图像有噪，应优先用先高斯平滑再微分的 <see cref="LaplaceOfGauss(double)"/>（LoG）。
	///   要一阶梯度幅值/方向而非二阶过零，用 <see cref="SobelAmp(string,int)"/>、<see cref="SobelDir(out JlImage,string,int)"/>。
	///   本重载 <paramref name="maskSize"/> 为 int；多尺度列表用 <see cref="Laplace(string,JlTuple,string)"/>。</para>
	///   <para><b>负值域坑</b>拉普拉斯响应天然含负值。<paramref name="resultType"/>="absolute" 时负值被折正，
	///   此时后续 <c>Threshold</c> 无法区分"亮边"与"暗边"；要保留符号请传非 absolute 值并先把图转 float，
	///   否则在 byte 域负响应被截断丢失 [待实测：本层未见 resultType 允许的非 absolute 取值清单]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage lap = img.Laplace("absolute", 3, "n_4");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄，用完 <c>Dispose</c>；输入经 <c>Store</c>(this) 传入，原生调用结束前不得释放。</para>
	/// </remarks>
	public JlImage Laplace(string resultType, int maskSize, string filterMask)
	{
		IntPtr proc = JlNativeApi.PreCall(1484);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, resultType);
		JlNativeApi.StoreI(proc, 1, maskSize);
		JlNativeApi.StoreS(proc, 2, filterMask);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>高通滤波：从图像中剔除低频分量、保留高频细节，原生算子 id 1485。</summary>
	/// <param name="width">高通掩模宽（掩模决定被去除的低频尺度）。Default: 9</param>
	/// <param name="height">高通掩模高。Default: 9</param>
	/// <returns>高通滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>用一个 <paramref name="width"/>×<paramref name="height"/> 的掩模把低于某截止尺度的
	///   分量减掉，输出强调边缘/纹理的高频图，常用于后续阈值前的对比度增强。</para>
	///   <para><b>与相邻算子的取舍</b>要各向同性、只改掩模尺度用本算子；要按标准差精确控制平滑/带通尺度，
	///   用 <see cref="LaplaceOfGauss(double)"/> 或 <see cref="DiffOfGauss(double,double)"/>。
	///   宽高分别可调，可构造非对称频带。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage hp = img.HighpassImage(9, 9);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄，用完 <c>Dispose</c>；掩模尺寸对结果的物理意义 [待实测：原生侧掩模构造细节本层不可见]。</para>
	/// </remarks>
	public JlImage HighpassImage(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1485);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>彩色亚像素边缘：对彩色图用 Deriche/Shen/Canny 提亚像素精度边缘轮廓，原生算子 id 1487，低高阈值以元组传入。</summary>
	/// <param name="filter">边缘算子，"canny"/"deriche"/"shen"。Default: "canny"</param>
	/// <param name="alpha">平滑参数：值越小平滑越强、细节越少；对 "canny" 语义相反。Default: 1.0</param>
	/// <param name="low">滞后阈值下限（元组）。Default: 20</param>
	/// <param name="high">滞后阈值上限（元组）。Default: 40</param>
	/// <returns>提取到的边缘，新的 JlXLDCont 亚像素轮廓句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>在彩色图上做边缘检测并输出亚像素精度的轮廓集合（XLD），
	///   内部按各色彩通道分别计算梯度后合并，比灰度边缘更能区分仅靠颜色（灰度相同）分隔的目标。</para>
	///   <para><b>前提</b>要 <paramref name="filter"/>="canny" 时，本族算子需先经高斯平滑参数 alpha 生效 [待实测：canny 是否额外要求 sigma 设置]。</para>
	///   <para><b>与相邻算子的取舍</b>只要亚像素轮廓（拟合/测量）→ 本算子；要边缘幅值/方向"图像"→ <see cref="EdgesColor(out JlImage,string,double,string,int,int)"/>；
	///   灰度图上的亚像素边缘 → <see cref="EdgesSubPix(string,double,JlTuple,JlTuple)"/>。单阈值标量版用
	///   <see cref="EdgesColorSubPix(string,double,double,double)"/>；本元组版可对低/高阈值传多值。注意传字面量会命中 double 重载造成重载歧义，须用 <c>new JlTuple(...)</c>。</para>
	///   <para><b>参数取向</b><paramref name="low"/>/<paramref name="high"/> 经 <c>Store</c>+调用后 <c>UnpinTuple</c>（钉固定元组）传入。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont edges = img.EdgesColorSubPix("canny", 1.0, new JlTuple(20.0), new JlTuple(40.0));
	///   </code>
	///   <para><b>资源与坑</b>返回新轮廓句柄，用完 <c>Dispose</c>；alpha 对 canny 与其他算子方向相反，套用同一 alpha 值易出错。</para>
	/// </remarks>
	public JlXLDCont EdgesColorSubPix(string filter, double alpha, JlTuple low, JlTuple high)
	{
		IntPtr proc = JlNativeApi.PreCall(1487);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.Store(proc, 2, low);
		JlNativeApi.Store(proc, 3, high);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>彩色亚像素边缘（低/高阈值以 double 传入）：Deriche/Shen/Canny，原生算子 id 1487。</summary>
	/// <param name="filter">边缘算子，"canny"/"deriche"/"shen"。Default: "canny"</param>
	/// <param name="alpha">平滑参数：值越小平滑越强、细节越少；对 "canny" 语义相反。Default: 1.0</param>
	/// <param name="low">滞后阈值下限。Default: 20</param>
	/// <param name="high">滞后阈值上限。Default: 40</param>
	/// <returns>提取到的边缘，新的 JlXLDCont 亚像素轮廓句柄。</returns>
	/// <remarks>
	///   <para>彩色亚像素边缘的算法含义、彩色通道语义与 canny 前提见
	///   <see cref="EdgesColorSubPix(string,double,JlTuple,JlTuple)"/>。本重载同一原生 id 1487，
	///   <paramref name="low"/>/<paramref name="high"/> 走 <c>StoreD</c> 直写单个 double 阈值，无固定/解固定，是常规写法。</para>
	///   <para><b>CS0121 提醒</b>本重载与元组重载成对存在：示例必须传 double 字面量（如 20.0），
	///   传整数字面量会同时匹配元组重载的隐式转换而歧义。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont edges = img.EdgesColorSubPix("canny", 1.0, 20.0, 40.0);
	///   </code>
	///   <para><b>资源与坑</b>返回新轮廓句柄，用完 <c>Dispose</c>。</para>
	/// </remarks>
	public JlXLDCont EdgesColorSubPix(string filter, double alpha, double low, double high)
	{
		IntPtr proc = JlNativeApi.PreCall(1487);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreD(proc, 2, low);
		JlNativeApi.StoreD(proc, 3, high);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>彩色边缘（图像输出）：对彩色图用 Canny/Deriche/Shen 提取边缘幅值图，并回带方向图，原生算子 id 1488。</summary>
	/// <param name="imaDir">输出：边缘方向图（新句柄）。</param>
	/// <param name="filter">边缘算子，"canny"/"deriche"/"shen"。Default: "canny"</param>
	/// <param name="alpha">平滑参数：值越小平滑越强、细节越少；对 "canny" 语义相反。Default: 1.0</param>
	/// <param name="NMS">是否做非极大值抑制："nms" 抑制，"none" 不做。Default: "nms"</param>
	/// <param name="low">滞后阈值下限（负值表示不做阈值化）。Default: 20</param>
	/// <param name="high">滞后阈值上限（负值表示不做阈值化）。Default: 40</param>
	/// <returns>边缘幅值（梯度模）图像，新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>在彩色图上按各通道计算梯度并合并，返回边缘"幅值图像"（灰度图，值=梯度模），
	///   同时通过 <paramref name="imaDir"/> 输出边缘方向图。与亚像素版不同，这里得到的是像素级图像而非轮廓。</para>
	///   <para><b>与相邻算子的取舍</b>要亚像素轮廓做几何拟合 → <see cref="EdgesColorSubPix(string,double,double,double)"/>；
	///   灰度图上的等价操作 → <see cref="EdgesImage(out JlImage,string,double,string,int,int)"/>。
	///   <paramref name="NMS"/>="nms" 会让边缘更细更干净，代价是可能丢弱边缘。</para>
	///   <para><b>阈值语义</b><paramref name="low"/>/<paramref name="high"/> 为滞后双阈值；传负值则跳过阈值化、返回连续幅值。</para>
	///   <para><b>参数取向</b>本算子只有单个 int 阈值重载，low/high 用 int 字面量无重载歧义。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage amp = img.EdgesColor(out JlImage dir, "canny", 1.0, "nms", 20, 40);
	///   dir.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值与 <paramref name="imaDir"/> 都是新句柄，各自需 <c>Dispose</c>。</para>
	/// </remarks>
	public JlImage EdgesColor(out JlImage imaDir, string filter, double alpha, string NMS, int low, int high)
	{
		IntPtr proc = JlNativeApi.PreCall(1488);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreS(proc, 2, NMS);
		JlNativeApi.StoreI(proc, 3, low);
		JlNativeApi.StoreI(proc, 4, high);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out imaDir);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>灰度亚像素边缘：用 Deriche/Lanser/Shen/Canny 提亚像素精度轮廓，原生算子 id 1489，低高阈值以元组传入。</summary>
	/// <param name="filter">边缘算子，"canny"/"deriche"/"lanser"/"shen"。Default: "canny"</param>
	/// <param name="alpha">平滑参数：值越小平滑越强、细节越少；对 "canny" 语义相反。Default: 1.0</param>
	/// <param name="low">滞后阈值下限（元组）。Default: 20</param>
	/// <param name="high">滞后阈值上限（元组）。Default: 40</param>
	/// <returns>提取到的边缘，新的 JlXLDCont 亚像素轮廓句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对灰度图做一阶梯度并做滞后阈值，输出亚像素精度的轮廓集合（XLD）。
	///   比像素级边缘更适合后续做 <c>FitLineContourXld</c>/<c>SelectContoursXld</c> 之类的几何拟合与筛选。</para>
	///   <para><b>与相邻算子的取舍</b>彩色图 → <see cref="EdgesColorSubPix(string,double,JlTuple,JlTuple)"/>；
	///   要像素级幅值/方向"图像"而非轮廓 → <see cref="EdgesImage(out JlImage,string,double,string,int,int)"/>；
	///   单一 double 阈值 → <see cref="EdgesSubPix(string,double,int,int)"/>。本元组版可对低/高阈值传多值，
	///   传字面量会命中标量重载而歧义，须 <c>new JlTuple(...)</c>。</para>
	///   <para><b>参数取向</b><paramref name="low"/>/<paramref name="high"/> 经 <c>Store</c>+<c>UnpinTuple</c>（钉固定元组）传入。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont edges = img.EdgesSubPix("canny", 1.0, new JlTuple(20.0), new JlTuple(40.0));
	///   </code>
	///   <para><b>资源与坑</b>返回新轮廓句柄，用完 <c>Dispose</c>；alpha 对 canny 方向与其他算子相反。</para>
	/// </remarks>
	public JlXLDCont EdgesSubPix(string filter, double alpha, JlTuple low, JlTuple high)
	{
		IntPtr proc = JlNativeApi.PreCall(1489);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.Store(proc, 2, low);
		JlNativeApi.Store(proc, 3, high);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>灰度亚像素边缘（低/高阈值以 int 传入）：Deriche/Lanser/Shen/Canny，原生算子 id 1489。</summary>
	/// <param name="filter">边缘算子，"canny"/"deriche"/"lanser"/"shen"。Default: "canny"</param>
	/// <param name="alpha">平滑参数：值越小平滑越强、细节越少；对 "canny" 语义相反。Default: 1.0</param>
	/// <param name="low">滞后阈值下限。Default: 20</param>
	/// <param name="high">滞后阈值上限。Default: 40</param>
	/// <returns>提取到的边缘，新的 JlXLDCont 亚像素轮廓句柄。</returns>
	/// <remarks>
	///   <para>算法含义、亚像素轮廓用途与 alpha 对 canny 的反向语义见
	///   <see cref="EdgesSubPix(string,double,JlTuple,JlTuple)"/>。本重载同一原生 id 1489，
	///   <paramref name="low"/>/<paramref name="high"/> 走 <c>StoreI</c> 直写整型阈值，无固定/解固定。</para>
	///   <para><b>CS0121 提醒</b>本重载与元组重载成对存在：示例传整数字面量 20/40 会命中本 int 重载（identity 优先），
	///   要走低/高多值请用元组重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlXLDCont edges = img.EdgesSubPix("canny", 1.0, 20, 40);
	///   </code>
	///   <para><b>资源与坑</b>返回新轮廓句柄，用完 <c>Dispose</c>。</para>
	/// </remarks>
	public JlXLDCont EdgesSubPix(string filter, double alpha, int low, int high)
	{
		IntPtr proc = JlNativeApi.PreCall(1489);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreI(proc, 2, low);
		JlNativeApi.StoreI(proc, 3, high);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>灰度边缘（图像输出）：用 Deriche/Lanser/Shen/Canny 提取边缘幅值图并回带方向图，原生算子 id 1490，阈值以元组传入。</summary>
	/// <param name="imaDir">输出：边缘方向图（新句柄）。</param>
	/// <param name="filter">边缘算子，"canny"/"deriche"/"lanser"/"shen"。Default: "canny"</param>
	/// <param name="alpha">平滑参数：值越小平滑越强、细节越少；对 "canny" 语义相反。Default: 1.0</param>
	/// <param name="NMS">非极大值抑制："nms" 抑制，"none" 不做。Default: "nms"</param>
	/// <param name="low">滞后阈值下限（元组，负值表示不做阈值化）。Default: 20</param>
	/// <param name="high">滞后阈值上限（元组，负值表示不做阈值化）。Default: 40</param>
	/// <returns>边缘幅值（梯度模）图像，新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对灰度图做一阶梯度，返回边缘"幅值图像"（值=梯度模），并通过 <paramref name="imaDir"/>
	///   输出边缘方向图。得到的是像素级图像，适合再做阈值/区域分析。</para>
	///   <para><b>与相邻算子的取舍</b>要亚像素轮廓做拟合 → <see cref="EdgesSubPix(string,double,JlTuple,JlTuple)"/>；
	///   彩色图 → <see cref="EdgesColor(out JlImage,string,double,string,int,int)"/>；单一 int 阈值 →
	///   <see cref="EdgesImage(out JlImage,string,double,string,int,int)"/>。本元组版可对低/高阈值传多值，
	///   传字面量会命中标量重载而歧义，须 <c>new JlTuple(...)</c>。</para>
	///   <para><b>阈值语义</b><paramref name="low"/>/<paramref name="high"/> 为滞后双阈值，负值跳过阈值化。</para>
	///   <para><b>参数取向</b><paramref name="low"/>/<paramref name="high"/> 经 <c>Store</c>+<c>UnpinTuple</c> 传入。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage amp = img.EdgesImage(out JlImage dir, "canny", 1.0, "nms", new JlTuple(20.0), new JlTuple(40.0));
	///   dir.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值与 <paramref name="imaDir"/> 都是新句柄，各自需 <c>Dispose</c>。</para>
	/// </remarks>
	public JlImage EdgesImage(out JlImage imaDir, string filter, double alpha, string NMS, JlTuple low, JlTuple high)
	{
		IntPtr proc = JlNativeApi.PreCall(1490);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreS(proc, 2, NMS);
		JlNativeApi.Store(proc, 3, low);
		JlNativeApi.Store(proc, 4, high);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(low);
		JlNativeApi.UnpinTuple(high);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out imaDir);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>灰度边缘（图像输出，低/高阈值以 int 传入）：Deriche/Lanser/Shen/Canny，原生算子 id 1490。</summary>
	/// <param name="imaDir">输出：边缘方向图（新句柄）。</param>
	/// <param name="filter">边缘算子，"canny"/"deriche"/"lanser"/"shen"。Default: "canny"</param>
	/// <param name="alpha">平滑参数：值越小平滑越强、细节越少；对 "canny" 语义相反。Default: 1.0</param>
	/// <param name="NMS">非极大值抑制："nms" 抑制，"none" 不做。Default: "nms"</param>
	/// <param name="low">滞后阈值下限（负值表示不做阈值化）。Default: 20</param>
	/// <param name="high">滞后阈值上限（负值表示不做阈值化）。Default: 40</param>
	/// <returns>边缘幅值（梯度模）图像，新句柄。</returns>
	/// <remarks>
	///   <para>算法含义、幅值图/方向图与 NMS 的取舍见
	///   <see cref="EdgesImage(out JlImage,string,double,string,JlTuple,JlTuple)"/>。本重载同一原生 id 1490，
	///   <paramref name="low"/>/<paramref name="high"/> 走 <c>StoreI</c> 直写整型阈值，无固定/解固定。</para>
	///   <para><b>CS0121 提醒</b>本重载与元组重载成对存在：示例传整数字面量 20/40 命中本 int 重载（identity 优先）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage amp = img.EdgesImage(out JlImage dir, "canny", 1.0, "nms", 20, 40);
	///   dir.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值与 <paramref name="imaDir"/> 都是新句柄，各自需 <c>Dispose</c>。</para>
	/// </remarks>
	public JlImage EdgesImage(out JlImage imaDir, string filter, double alpha, string NMS, int low, int high)
	{
		IntPtr proc = JlNativeApi.PreCall(1490);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, filter);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreS(proc, 2, NMS);
		JlNativeApi.StoreI(proc, 3, low);
		JlNativeApi.StoreI(proc, 4, high);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out imaDir);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>LoG 带通算子：高斯平滑后再拉普拉斯微分，原生算子 id 1492，sigma 以元组传入。</summary>
	/// <param name="sigma">高斯平滑的标准差，单位是像素。Default: 2.0</param>
	/// <returns>拉普拉斯滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>先以标准差 <paramref name="sigma"/> 做高斯平滑压噪，再取拉普拉斯（二阶导数），
	///   等效带通：只有尺度约在 σ 附近的灰度结构被突出，恒定区和缓变背景输出接近 0。相比不做平滑的有限差分
	///   <see cref="Laplace(string,int,string)"/>，对单像素噪声的敏感度大幅下降；<paramref name="sigma"/> 因此是
	///   "目标尺度旋钮"——要找某一直径的斑点/孔洞，σ 取该尺度的一半上下 [待实测：精确对应关系]。</para>
	///   <para><b>负值域（最容易错的地方）</b>LoG 输出天然含负值（边缘一侧为负）。本算子没有 <c>resultType</c> 参数
	///   （不同于 <c>Laplace</c> 可要绝对值），输出类型与负值是否被截断完全由原生决定 [待实测]。稳妥做法是先转
	///   <c>float</c> 再滤波，或用 <see cref="ScaleImage(double,double)"/> 加偏移抬到正值域后再 <c>Threshold</c>，
	///   否则负边缘响应在 <c>byte</c> 域里被静默丢光。</para>
	///   <para><b>与相邻算子的取舍</b>要更快（两次高斯相减的近似 LoG，Marr 路线）→ <see cref="DiffOfGauss(double,double)"/>；
	///   要梯度幅值/方向（一阶）而不是二阶过零 → <see cref="SobelAmp(string,int)"/>、<see cref="SobelDir(out JlImage,string,int)"/>。
	///   多通道输入行为 [待实测]。</para>
	///   <para><b>参数取向</b>元组版 <paramref name="sigma"/> 走 <c>Store</c>+<c>UnpinTuple</c>，多元素（一次给多个尺度）
	///   语义本层看不出来 [待实测]；单尺度用 <see cref="LaplaceOfGauss(double)"/> 更省事。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage f = img.ConvertImageType("float");
	///   using JlImage log = f.LaplaceOfGauss(new JlTuple(2.0));
	///   using JlImage shifted = log.ScaleImage(1.0, 128.0);   // 抬偏移，负响应不再丢
	///   </code>
	///   <para><b>资源与坑</b>σ≤0 行为 [待实测]；float 中间图 + LoG 图内存是原图数倍，大图及时 <c>Dispose</c>。</para>
	/// </remarks>
	public JlImage LaplaceOfGauss(JlTuple sigma)
	{
		IntPtr proc = JlNativeApi.PreCall(1492);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, sigma);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(sigma);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>LoG 算子（单个 σ 以 double 传入）。</summary>
	/// <param name="sigma">高斯平滑的标准差，单位是像素。Default: 2.0</param>
	/// <returns>拉普拉斯滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para>σ 的尺度含义与负值域处理（先转 float 或加偏移再 Threshold）见
	///   <see cref="LaplaceOfGauss(JlTuple)"/>：同一原生 id 1492，本版本 <c>StoreD</c> 直写 σ，无固定/解固定，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage log = img.LaplaceOfGauss(2.0);
	///   </code>
	/// </remarks>
	public JlImage LaplaceOfGauss(double sigma)
	{
		IntPtr proc = JlNativeApi.PreCall(1492);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用两个高斯之差近似 LoG（Marr 路线），原生算子 id 1493。</summary>
	/// <param name="sigma">被近似的 LoG 平滑标准差，单位像素。Default: 3.0</param>
	/// <param name="sigFactor">两个高斯标准差之比（Marr 推荐 1.6）。Default: 1.6</param>
	/// <returns>LoG 近似图像，新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>以两个不同标准差的高斯图相减来逼近拉普拉斯高斯（LoG）带通：
	///   大 σ 去低频背景、小 σ 保留结构，差值突出约在 <paramref name="sigma"/> 尺度上的斑点/边缘。
	///   <paramref name="sigFactor"/> 控制两高斯的比例（Marr 建议 1.6）。</para>
	///   <para><b>与相邻算子的取舍</b>要精确 LoG 直接调 σ → <see cref="LaplaceOfGauss(double)"/>；
	///   本算子胜在可用高斯可分离实现更快，但带通不如精确 LoG 干净。</para>
	///   <para><b>负值域坑</b>与 LoG 同：差值图天然含负值，byte 域会截断，建议先转 float 或加偏移再阈值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage f = img.ConvertImageType("float");
	///   using JlImage dog = f.DiffOfGauss(3.0, 1.6);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄，用完 <c>Dispose</c>；σ≤0 或 sigFactor≤1 行为 [待实测]。</para>
	/// </remarks>
	public JlImage DiffOfGauss(double sigma, double sigFactor)
	{
		IntPtr proc = JlNativeApi.PreCall(1493);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, sigma);
		JlNativeApi.StoreD(proc, 1, sigFactor);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>检测直线边缘线段：Sobel 求梯度后按滞后阈值与线段近似提取直线段端点，原生算子 id 1496。</summary>
	/// <param name="sobelSize">Sobel 算子掩模尺寸。Default: 5</param>
	/// <param name="minAmplitude">最小边缘强度（低于此的梯度不算边缘）。Default: 32</param>
	/// <param name="maxDistance">近似直线到其原始边缘的最大容许偏差（像素）。Default: 3</param>
	/// <param name="minLength">输出线段的最小长度（像素）。Default: 10</param>
	/// <param name="beginRow">输出：线段起点行坐标（INTEGER 元组）。</param>
	/// <param name="beginCol">输出：线段起点列坐标（INTEGER 元组）。</param>
	/// <param name="endRow">输出：线段终点行坐标（INTEGER 元组）。</param>
	/// <param name="endCol">输出：线段终点列坐标（INTEGER 元组）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>先用 <paramref name="sobelSize"/> 尺寸的 Sobel 求梯度，按 <paramref name="minAmplitude"/>
	///   过滤弱边，再把邻近边缘聚成直线段：与理想直线偏差超 <paramref name="maxDistance"/> 处断开，短于
	///   <paramref name="minLength"/> 的段丢弃。四个 <c>out</c> 元组一一对应给出每段起止点。</para>
	///   <para><b>坐标量纲</b>四路输出均以 <c>JlTupleType.INTEGER</c> 装载——端点是整像素坐标，非亚像素。
	///   要亚像素直线请改用轮廓拟合路线（<c>EdgesSubPix</c> 后 <c>FitLineContourXld</c>）。</para>
	///   <para><b>与相邻算子的取舍</b>要连续边缘轮廓 → <see cref="EdgesSubPix(string,double,int,int)"/>；
	///   只要线段端点做测量/对位 → 本算子更直接。</para>
	///   <para><b>参数取向</b>void 方法，四路结果经 <c>out</c> 返回，实参必须写 <c>out</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   img.DetectEdgeSegments(5, 32, 3, 10, out JlTuple beginRow, out JlTuple beginCol, out JlTuple endRow, out JlTuple endCol);
	///   </code>
	///   <para><b>资源与坑</b>四路 <c>JlTuple</c> 各含句柄元素需 <c>Dispose</c>（纯数值元组可省）；无检出时段数为空。</para>
	/// </remarks>
	public void DetectEdgeSegments(int sobelSize, int minAmplitude, int maxDistance, int minLength, out JlTuple beginRow, out JlTuple beginCol, out JlTuple endRow, out JlTuple endCol)
	{
		IntPtr proc = JlNativeApi.PreCall(1496);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, sobelSize);
		JlNativeApi.StoreI(proc, 1, minAmplitude);
		JlNativeApi.StoreI(proc, 2, maxDistance);
		JlNativeApi.StoreI(proc, 3, minLength);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out beginRow);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.INTEGER, err, out beginCol);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.INTEGER, err, out endRow);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out endCol);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}




	/// <summary>去马赛克：把单通道彩色滤光阵列（CFA/Bayer）图重建为三通道 RGB 图，原生算子 id 1500。</summary>
	/// <param name="CFAType">滤光阵列排布类型，如 "bayer_gb"/"bayer_gr"/"bayer_rg"/"bayer_bg"（指明左上角起始通道）。Default: "bayer_gb"</param>
	/// <param name="interpolation">缺失通道的插值方式，如 "bilinear"/"nearest_neighbor"。Default: "bilinear"</param>
	/// <returns>重建后的三通道 RGB 图像，新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>Bayer 传感器每个像素只记录 R/G/B 之一，本算子按 <paramref name="CFAType"/> 声明的排布
	///   推断各像素所属通道，再用 <paramref name="interpolation"/> 把缺失的两个通道补齐，输出真正的三通道 RGB 图。</para>
	///   <para><b>前提</b>输入必须是<strong>单通道</strong> CFA 图，且 <paramref name="CFAType"/> 必须与传感器实际排布一致；
	///   排布选错会让红蓝通道互换，产生整体偏色却仍"能出图"，属静默错误。</para>
	///   <para><b>与相邻算子的取舍</b>已是三通道 RGB 只是想转灰度 → <see cref="Rgb1ToGray()"/>；
	///   本算子专用于 raw CFA→RGB 的重建，不要对已插值的图重复调用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage cfa = new JlImage("byte", 640, 480);
	///   using JlImage rgb = cfa.CfaToRgb("bayer_gb", "bilinear");
	///   </code>
	///   <para><b>资源与坑</b>返回新三通道句柄，用完 <c>Dispose</c>；插值会在强边缘处产生彩色伪边（拉链效应）。</para>
	/// </remarks>
	public JlImage CfaToRgb(string CFAType, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(1500);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, CFAType);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把单张三通道 RGB 图转灰度，原生算子 id 1501。</summary>
	/// <returns>灰度图像，新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对"一张含三通道"的 RGB 图像做红绿蓝加权合成，得到单通道灰度图。
	///   加权系数（感知亮度权重，如 Rec.601/709）由原生侧决定 [待实测：具体权重值]。</para>
	///   <para><b>与相邻算子的取舍</b>RGB 三通道分别存成三张单通道图 → 用 <see cref="Rgb3ToGray(JlImage,JlImage)"/>；
	///   已是单通道输入想再取灰度无意义。</para>
	///   <para><b>前提</b>输入须为三通道；对单通道调用行为 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage rgb = new JlImage("byte", 640, 480);
	///   using JlImage gray = rgb.Rgb1ToGray();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄，用完 <c>Dispose</c>；无参方法，输入即 this。</para>
	/// </remarks>
	public JlImage Rgb1ToGray()
	{
		IntPtr proc = JlNativeApi.PreCall(1501);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把分开的三通道图合成灰度：this=红通道，另传入绿、蓝通道，原生算子 id 1502。</summary>
	/// <param name="imageGreen">绿通道输入图像。</param>
	/// <param name="imageBlue">蓝通道输入图像。</param>
	/// <returns>灰度图像，新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>当 R/G/B 三通道以三张独立单通道图存在时，本算子按红绿蓝加权合成为一张灰度图。
	///   调用者即红通道，<paramref name="imageGreen"/>、<paramref name="imageBlue"/> 依次补齐。</para>
	///   <para><b>前提</b>三张图尺寸、类型须一致；加权系数由原生决定 [待实测：具体权重值]。</para>
	///   <para><b>与相邻算子的取舍</b>RGB 已在一张三通道图里 → <see cref="Rgb1ToGray()"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage red = new JlImage("byte", 640, 480);
	///   JlImage green = new JlImage("byte", 640, 480);
	///   JlImage blue = new JlImage("byte", 640, 480);
	///   using JlImage gray = red.Rgb3ToGray(green, blue);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄，用完 <c>Dispose</c>；<paramref name="imageGreen"/>、<paramref name="imageBlue"/> 在原生调用结束前不得释放（内部 GC.KeepAlive 保命）。</para>
	/// </remarks>
	public JlImage Rgb3ToGray(JlImage imageGreen, JlImage imageBlue)
	{
		IntPtr proc = JlNativeApi.PreCall(1502);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageGreen);
		JlNativeApi.Store(proc, 3, imageBlue);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageGreen);
		GC.KeepAlive(imageBlue);
		return obj;
	}

	/// <summary>RGB→任意色彩空间：this=红通道，另传绿、蓝，输出所选色彩空间的三通道，原生算子 id 1503。</summary>
	/// <param name="imageGreen">绿通道输入。</param>
	/// <param name="imageBlue">蓝通道输入。</param>
	/// <param name="imageResult2">输出：变换后第 2 通道（新句柄）。</param>
	/// <param name="imageResult3">输出：变换后第 3 通道（新句柄）。</param>
	/// <param name="colorSpace">目标色彩空间，如 "hsv"/"hsi"/"xyz" 等字符串常量。Default: "hsv"</param>
	/// <returns>变换后第 1 通道（如 HSV 的 H 分量），新句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把以三张单通道给出的 RGB 转到 <paramref name="colorSpace"/> 指定的空间，
	///   结果三通道分别由返回值（第1通道）、<paramref name="imageResult2"/>、<paramref name="imageResult3"/> 给出。
	///   调用者即红通道。</para>
	///   <para><b>典型动机</b>转 HSV/HSI 后可单独用 H（色调）或 I（强度）通道做颜色分割，比在 RGB 里三通道联合阈值更稳。</para>
	///   <para><b>反向操作</b>转回 RGB 用 <see cref="TransToRgb(JlImage,JlImage,out JlImage,out JlImage,string)"/>。</para>
	///   <para><b>参数取向</b>输出通道顺序（第1/第2/第3）与色彩空间定义绑定；例：HSV 返回 H、out 得 S、V。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage red = new JlImage("byte", 640, 480);
	///   JlImage green = new JlImage("byte", 640, 480);
	///   JlImage blue = new JlImage("byte", 640, 480);
	///   using JlImage ch1 = red.TransFromRgb(green, blue, out JlImage ch2, out JlImage ch3, "hsv");
	///   ch2.Dispose();
	///   ch3.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值与两路 out 都是新句柄，各自需 <c>Dispose</c>；输入三图在原生调用结束前不得释放。</para>
	/// </remarks>
	public JlImage TransFromRgb(JlImage imageGreen, JlImage imageBlue, out JlImage imageResult2, out JlImage imageResult3, string colorSpace)
	{
		IntPtr proc = JlNativeApi.PreCall(1503);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageGreen);
		JlNativeApi.Store(proc, 3, imageBlue);
		JlNativeApi.StoreS(proc, 0, colorSpace);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out imageResult2);
		err = LoadNew(proc, 3, err, out imageResult3);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageGreen);
		GC.KeepAlive(imageBlue);
		return obj;
	}

	/// <summary>把某颜色空间的三通道图反变换回 RGB 三通道（原生 id 1504，TransFromRgb 的逆运算）。</summary>
	/// <param name="imageInput2">源色空间第 2 通道（hsv 时为 S；this 为第 1 通道 H）。</param>
	/// <param name="imageInput3">源色空间第 3 通道（hsv 时为 V）。</param>
	/// <param name="imageGreen">输出的绿通道（新句柄）。</param>
	/// <param name="imageBlue">输出的蓝通道（新句柄）。</param>
	/// <param name="colorSpace">输入图像所在的颜色空间名。Default: "hsv"</param>
	/// <returns>输出的红通道（新句柄，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>三张单通道图按 colorSpace 解释为源色空间的分量（如 hsv 的 H/S/V），联立反解成 R/G/B。本实例即第 1 分量，imageInput2/imageInput3 是第 2/3 分量；返回值=红通道，out 参数=绿、蓝通道。三个输出都由 <c>InitOCT</c>+<c>LoadNew</c> 新建，是独立句柄。</para>
	///   <para><b>约束或前提</b>必须是三张同尺寸的单通道图；彩色多通道源要先拆成单通道再喂入。colorSpace 命名的是输入空间，不是输出（输出恒为 RGB）。对 hsv 而言 H 常按 0..255 编码，反变换前量纲要与正向变换时一致，否则色相偏 [待实测：各空间分量量纲]。</para>
	///   <para><b>与相邻算子的取舍</b>方向相反：<see cref="TransFromRgb(JlImage,JlImage,out JlImage,out JlImage,string)"/> 把 RGB 拆到任意空间；本算子把任意空间并回 RGB。只要单分量时用对应分量即可，不必凑齐三通道。</para>
	///   <para><b>参数取向</b>Store 序固定 this→槽1、imageInput2→槽2、imageInput3→槽3，colorSpace 走 StoreS 槽0。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage h = new JlImage("byte", 640, 480);   // 源第 1 分量（H）
	///   JlImage s = new JlImage("byte", 640, 480);   // 第 2 分量（S）
	///   JlImage v = new JlImage("byte", 640, 480);   // 第 3 分量（V）
	///   JlImage green, blue;
	///   using JlImage red = h.TransToRgb(s, v, out green, out blue, "hsv");
	///   green.Dispose();
	///   blue.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的 red 与两个 out 通道都是新句柄，共三个，全部要释放；GC.KeepAlive 保证调用结束前 this 与两入参不被回收。</para>
	/// </remarks>
	public JlImage TransToRgb(JlImage imageInput2, JlImage imageInput3, out JlImage imageGreen, out JlImage imageBlue, string colorSpace)
	{
		IntPtr proc = JlNativeApi.PreCall(1504);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageInput2);
		JlNativeApi.Store(proc, 3, imageInput3);
		JlNativeApi.StoreS(proc, 0, colorSpace);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out imageGreen);
		err = LoadNew(proc, 3, err, out imageBlue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageInput2);
		GC.KeepAlive(imageInput3);
		return obj;
	}

	/// <summary>逐像素与位掩码做按位与（原生 id 1505）：out = this &amp; bitMask，用于取/筛定位。</summary>
	/// <param name="bitMask">按位与的掩码整数。Default: 128</param>
	/// <returns>按位与结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把每个灰度当整数位串，与 bitMask 逐位 AND。默认 128=2^7，对 byte 而言即只保留最高位。常用来从编码位图里抽出某几位、或配合 BitSlice/位打包做按位提取。</para>
	///   <para><b>约束或前提</b>要求整数型图像（byte/int2 等），float 需先 <see cref="ConvertImageType(string)"/> 转整 [待实测：允许的类型集]。AND 只会把位清 0、不会置 1，故结果 ≤ 原值且 ≤ bitMask。</para>
	///   <para><b>与相邻算子的取舍</b>只要某一位的 0/1 → <see cref="BitSlice(int)"/>；置某几位 → BitOr；翻转若干位 → BitXor。本算子专做"屏蔽"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage packed = new JlImage("byte", 640, 480);
	///   using JlImage hiBit = packed.BitMask(128);   // 只留最高位
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；bitMask 走 <c>StoreI</c> 按整数写入。</para>
	/// </remarks>
	public JlImage BitMask(int bitMask)
	{
		IntPtr proc = JlNativeApi.PreCall(1505);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, bitMask);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素抽取指定位（原生 id 1506）：把某一位的值取成二值图，用于位解码/分层掩膜。</summary>
	/// <param name="bit">要抽取的位序号。Default: 8</param>
	/// <returns>只含该位的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把每个灰度当位串，抽出第 bit 位。与 <see cref="BitMask(int)"/> 的区别：BitMask 保留该位的原始权值（如最高位仍是 128），BitSlice 归一到只含 0/该位权值或 0/1（索引基与是否归一 [待实测]）。适合把位打包的多通道信息拆成独立二值层再 Threshold。</para>
	///   <para><b>约束或前提</b>要求整数型图像；byte 只有 8 位，bit 越界行为 [待实测]。位序号从低到高还是从高到低 [待实测]，用前建议先拿已知值标定一次。</para>
	///   <para><b>与相邻算子的取舍</b>要"屏蔽掉其余位但仍保留原权值"→ BitMask；要把位当二值层用 → 本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage packed = new JlImage("byte", 640, 480);
	///   using JlImage layer = packed.BitSlice(1);   // 抽最低位当一层掩膜
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；bit 走 <c>StoreI</c> 按整数写入。</para>
	/// </remarks>
	public JlImage BitSlice(int bit)
	{
		IntPtr proc = JlNativeApi.PreCall(1506);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, bit);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素右移（原生 id 1507）：整除 2^shift，做无损降位/降采样。</summary>
	/// <param name="shift">右移位数。Default: 3</param>
	/// <returns>右移结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>out = this &gt;&gt; shift，等价对非负整数向零取整除 2^shift。默认 3 即 ÷8，把高位量化掉、缩小动态范围（如把 12-bit 压到 9-bit 或做灰度分层）。</para>
	///   <para><b>约束或前提</b>要求整数型图像，float 需先 <see cref="ConvertImageType(string)"/>；右移丢低位不可逆。shift 超过位宽 → 全 0 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>放大用 <see cref="BitLshift(int)"/>；要保留低位、只清高位用 <see cref="BitMask(int)"/>；需带四舍五入的除法用 <see cref="ScaleImage(double,double)"/> 更直观。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage small = img.BitRshift(3);   // ÷8 降位
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；shift 走 <c>StoreI</c>。</para>
	/// </remarks>
	public JlImage BitRshift(int shift)
	{
		IntPtr proc = JlNativeApi.PreCall(1507);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, shift);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素左移（原生 id 1508）：整数乘 2^shift，做无损升位/位拼接。</summary>
	/// <param name="shift">左移位数。Default: 3</param>
	/// <returns>左移结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>out = this &lt;&lt; shift，即 ×2^shift。常与 <see cref="BitRshift(int)"/> 配对做位打包：把低位段左移腾位，再 BitOr 合并另一段。</para>
	///   <para><b>约束或前提</b>要求整数型图像；左移会挤掉高位，超位宽部分丢失（截断行为 [待实测]）——放大前先估算是否溢出，byte ×8 只要原值≥32 就溢出。</para>
	///   <para><b>与相邻算子的取舍</b>降位用 <see cref="BitRshift(int)"/>；只要 ×2^shift 且担心溢出可改 <see cref="ScaleImage(double,double)"/> 换到 float 域。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage big = img.BitLshift(3);   // ×8 腾出低位
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；shift 走 <c>StoreI</c>。</para>
	/// </remarks>
	public JlImage BitLshift(int shift)
	{
		IntPtr proc = JlNativeApi.PreCall(1508);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, shift);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素按位取反（原生 id 1509）：整型域按位翻转，byte 上等价 255−g。</summary>
	/// <returns>按位取反结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对每个像素的位串取补。byte 输出即 255−g，效果与 <see cref="InvertImage()"/> 一致；区别在本算子是按位定义、面向整数编码位图，而 InvertImage 是按类型满量程反转。</para>
	///   <para><b>约束或前提</b>要求整数型图像（float 无从"取补"）[待实测：允许的类型集]。位宽由图像类型决定，int2 与 byte 的取反范围不同。</para>
	///   <para><b>与相邻算子的取舍</b>只想反灰度、语义更清晰 → <see cref="InvertImage()"/>；要按位取反（配合 BitAnd/BitXor 做位图运算）→ 本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage mask = new JlImage("byte", 640, 480);
	///   using JlImage inv = mask.BitNot();   // byte 上即 255−g
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；无额外参数。</para>
	/// </remarks>
	public JlImage BitNot()
	{
		IntPtr proc = JlNativeApi.PreCall(1509);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>两图逐像素按位异或（原生 id 1510）：out = this ^ image2，标记"仅一方置位"的像素。</summary>
	/// <param name="image2">第二输入图像（this 为第一输入）。</param>
	/// <returns>异或结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本实例是第 1 输入，image2 是第 2 输入，逐位 XOR。两图相同处相消为 0、不同处置 1，因此可用来快速定位两幅二值/位图掩膜的差异（相同位被抹掉，正好互补 <see cref="BitAnd(JlImage)"/> 的"共有位"）。</para>
	///   <para><b>约束或前提</b>要求整数型图像，两图必须同尺寸、同类型、同通道数，否则 [待实测：报错还是逐元素截断]。</para>
	///   <para><b>与相邻算子的取舍</b>要"都置位"→ <see cref="BitAnd(JlImage)"/>；要"任一方置位"→ <see cref="BitOr(JlImage)"/>；要"仅一方"→ 本算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage maskA = new JlImage("byte", 640, 480);
	///   JlImage maskB = new JlImage("byte", 640, 480);
	///   using JlImage diff = maskA.BitXor(maskB);   // 掩膜不一致处
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；this 与 image2 由 GC.KeepAlive 保活到调用结束。</para>
	/// </remarks>
	public JlImage BitXor(JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1510);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>两图逐像素按位或（原生 id 1511）：out = this | image2，合并置位/并掩膜。</summary>
	/// <param name="image2">第二输入图像（this 为第一输入）。</param>
	/// <returns>或结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本实例是第 1 输入，image2 是第 2 输入，逐位 OR。任一方置位即置位，最常用于把多张二值掩膜并成一张，或配合 <see cref="BitLshift(int)"/> 把腾好位的段拼回一个整数。</para>
	///   <para><b>约束或前提</b>要求整数型图像，两图同尺寸/同类型/同通道数；OR 只会置 1、不会清 0，故结果 ≥ 两输入按位包含关系。</para>
	///   <para><b>与相邻算子的取舍</b>要共有位置位 → <see cref="BitAnd(JlImage)"/>；只要差异 → <see cref="BitXor(JlImage)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage maskA = new JlImage("byte", 640, 480);
	///   JlImage maskB = new JlImage("byte", 640, 480);
	///   using JlImage merged = maskA.BitOr(maskB);   // 并掩膜
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；this 与 image2 由 GC.KeepAlive 保活。</para>
	/// </remarks>
	public JlImage BitOr(JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1511);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>两图逐像素按位与（原生 id 1512）：out = this &amp; image2，取共有置位/交掩膜。</summary>
	/// <param name="image2">第二输入图像（this 为第一输入）。</param>
	/// <returns>与结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本实例是第 1 输入，image2 是第 2 输入，逐位 AND。两方都置位才置位，用于求两张位图掩膜的交集，或对彩色/打包图逐通道取公共位。与 <see cref="BitMask(int)"/> 的区别：后者掩码是常量整数，本算子掩码是另一幅图（逐像素可变）。</para>
	///   <para><b>约束或前提</b>要求整数型图像，两图同尺寸/同类型/同通道数；AND 只清 0 不置 1，结果 ≤ 两输入。</para>
	///   <para><b>与相邻算子的取舍</b>并集 → <see cref="BitOr(JlImage)"/>；差异 → <see cref="BitXor(JlImage)"/>；固定位掩码 → <see cref="BitMask(int)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage maskA = new JlImage("byte", 640, 480);
	///   JlImage maskB = new JlImage("byte", 640, 480);
	///   using JlImage inter = maskA.BitAnd(maskB);   // 掩膜交集
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；this 与 image2 由 GC.KeepAlive 保活。</para>
	/// </remarks>
	public JlImage BitAnd(JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1512);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>Gamma 编/解码的逐通道重载（原生 id 1513）：maxGray 用元组按通道给满量程。</summary>
	/// <param name="gamma">指数段幂系数（1/2.4≈0.41667 即 sRGB）。Default: 0.416666666667</param>
	/// <param name="offset">指数段偏置。Default: 0.055</param>
	/// <param name="threshold">线性段与指数段切换的归一化阈值。Default: 0.0031308</param>
	/// <param name="maxGray">各通道输入类型满量程（元组，可多值）。Default: 255.0</param>
	/// <param name="encode">'true' 做编码（线性→感知域），否则解码。Default: "true"</param>
	/// <returns>变换后的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量版同 id 1513 的分段传递函数：以 f=g/maxGray 归一，f≤threshold 走线性段、否则走 (1+offset)·f^gamma−offset 的指数段，再乘回 maxGray。默认三个常数正是 sRGB 规格值，故默认调用即 sRGB 编码。区别只在 maxGray 走 <c>Store</c>+<c>UnpinTuple</c>，可给 RGB 各通道不同满量程。</para>
	///   <para><b>约束或前提</b>maxGray 长度=通道数或单值广播 [待实测]；gamma/offset/threshold 仍走 <c>StoreD</c> 标量。要求输入值域与 maxGray 一致，否则归一失真。</para>
	///   <para><b>与相邻算子的取舍</b>全通道同满量程用 <see cref="GammaImage(double,double,double,double,string)"/> 免固定开销；只做线性明暗用 <see cref="ScaleImage(double,double)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage rgb = new JlImage("byte", 640, 480);
	///   using JlImage enc = rgb.GammaImage(0.416666666667, 0.055, 0.0031308,
	///       new JlTuple(new double[] { 255.0, 255.0, 255.0 }), "true");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；encode 是字符串开关不是布尔。</para>
	/// </remarks>
	public JlImage GammaImage(double gamma, double offset, double threshold, JlTuple maxGray, string encode)
	{
		IntPtr proc = JlNativeApi.PreCall(1513);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, gamma);
		JlNativeApi.StoreD(proc, 1, offset);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.Store(proc, 3, maxGray);
		JlNativeApi.StoreS(proc, 4, encode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maxGray);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>Gamma 编/解码标量重载（原生 id 1513）：整图用同一满量程做分段传递。</summary>
	/// <param name="gamma">指数段幂系数（1/2.4≈0.41667 即 sRGB）。Default: 0.416666666667</param>
	/// <param name="offset">指数段偏置。Default: 0.055</param>
	/// <param name="threshold">线性段与指数段切换的归一化阈值。Default: 0.0031308</param>
	/// <param name="maxGray">输入类型满量程（byte=255、uint2=65535）。Default: 255.0</param>
	/// <param name="encode">'true' 做编码（线性→感知域），否则解码（感知域→线性）。Default: "true"</param>
	/// <returns>变换后的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>out 按 f=g/maxGray 归一后走分段函数：f≤threshold 线性、否则 (1+offset)·f^gamma−offset，再乘 maxGray 回原量纲。默认参数即 sRGB 曲线；做显示/存储用 encode='true'，做光照/反射率等线性计算前先解码成线性域，否则后续加减乘都失真。</para>
	///   <para><b>约束或前提</b>maxGray 必须匹配真实图像类型，取错会把整条曲线压偏；int 型图负值/越界处理 [待实测]。全通道不同满量程需走 <see cref="GammaImage(double,double,double,JlTuple,string)"/>。</para>
	///   <para><b>与相邻算子的取舍</b>只做线性明暗/反相 → <see cref="ScaleImage(double,double)"/>；本算子专做感知↔线性的非线性映射。</para>
	///   <para><b>参数取向</b>本重载 maxGray 走 <c>StoreD</c> 直写、零固定开销；gamma/offset/threshold 同样 <c>StoreD</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage linear = img.GammaImage(0.416666666667, 0.055, 0.0031308, 255.0, "false");   // 解码到线性域
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；encode 用字符串 "true"/"false" 而非 bool。</para>
	/// </remarks>
	public JlImage GammaImage(double gamma, double offset, double threshold, double maxGray, string encode)
	{
		IntPtr proc = JlNativeApi.PreCall(1513);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, gamma);
		JlNativeApi.StoreD(proc, 1, offset);
		JlNativeApi.StoreD(proc, 2, threshold);
		JlNativeApi.StoreD(proc, 3, maxGray);
		JlNativeApi.StoreS(proc, 4, encode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素求幂的逐通道重载（原生 id 1514）：out = g^exponent，指数用元组按通道给。</summary>
	/// <param name="exponent">各通道指数（元组，可多值）。Default: 2</param>
	/// <returns>幂结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量版同 id 1514，差别只在 exponent 走 <c>Store</c>+<c>UnpinTuple</c>，可对 RGB 各通道用不同次幂（如分别做 γ 式通道增益）。指数 2 得平方、0.5 即开方。</para>
	///   <para><b>约束或前提</b>负底数配非整数指数无定义（NaN/非法），处理 [待实测]；建议在 float 域用，byte 整型域次幂&gt;1 迅速溢出。exponent 长度=通道数或单值广播 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>全通道同指数用 <see cref="PowImage(double)"/> 免固定开销；专做开方用 <see cref="SqrtImage()"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage f = new JlImage("float", 640, 480);
	///   using JlImage p = f.PowImage(new JlTuple(2.0));   // 逐像素平方
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage PowImage(JlTuple exponent)
	{
		IntPtr proc = JlNativeApi.PreCall(1514);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, exponent);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(exponent);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素求幂标量重载（原生 id 1514）：out = g^exponent，全通道用同一指数。</summary>
	/// <param name="exponent">指数。Default: 2</param>
	/// <returns>幂结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>每个灰度取 exponent 次幂。用途：2 次幂配合求和再开方自算幅值/能量；&lt;1（如 0.5）压缩高亮拉开暗部做非线性增强（本质是一个单参数版 gamma）。负指数即 1/g^n，g=0 处发散。</para>
	///   <para><b>约束或前提</b>负底数配非整数指数无定义 [待实测]；byte 整型域次幂&gt;1 迅速溢出/饱和 [待实测]，先 <see cref="ConvertImageType(string)"/>("float") 再幂。</para>
	///   <para><b>与相邻算子的取舍</b>各通道不同指数用 <see cref="PowImage(JlTuple)"/>；只要开方用 <see cref="SqrtImage()"/>。</para>
	///   <para><b>参数取向</b>本重载 <c>StoreD</c> 直写指数、零固定开销；示例务必用 double 字面量（2.0）以避开与 JlTuple 重载的二义性。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage f = new JlImage("float", 640, 480);
	///   using JlImage sq = f.PowImage(2.0);   // 逐像素平方
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage PowImage(double exponent)
	{
		IntPtr proc = JlNativeApi.PreCall(1514);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, exponent);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素指数运算的元组重载（原生 id 1515）：out = baseVal^g，底数可按通道给。</summary>
	/// <param name="baseVal">各通道底数（元组，可含 "e" 或数值）。Default: "e"</param>
	/// <returns>指数结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量版同 id 1515，底数走 <c>Store</c>+<c>UnpinTuple</c>，可给多通道各设底数。计算 baseVal 的 g 次幂（g=像素灰度）。</para>
	///   <para><b>约束或前提</b>指数增长极快：g 直接取 0..255 会瞬间溢出，务必先 <see cref="ScaleImage(double,double)"/> 把灰度压进小范围（如 0..±几）再运算；建议在 float 域。baseVal 长度=通道数或单值广播 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>全图同底数用 <see cref="ExpImage(string)"/> 免固定开销；LogImage 是本运算的逆。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage f = new JlImage("float", 640, 480);
	///   using JlImage small = f.ScaleImage(0.01, 0.0);            // 先压范围防溢出
	///   using JlImage e = small.ExpImage(new JlTuple(2.718281828459045));   // 底数 e
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage ExpImage(JlTuple baseVal)
	{
		IntPtr proc = JlNativeApi.PreCall(1515);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, baseVal);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(baseVal);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素指数运算标量重载（原生 id 1515）：out = baseVal^g，全图同一底数。</summary>
	/// <param name="baseVal">底数："e" 或数值字符串（如 "2"、"10"）。Default: "e"</param>
	/// <returns>指数结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>以像素灰度 g 为指数、baseVal 为底做幂。baseVal="e" 即自然指数。常用于把对数域结果反变换回线性域（配 <see cref="LogImage(string)"/>）。</para>
	///   <para><b>约束或前提</b>指数增长极快，g 直接取 0..255 必溢出，务必先 <see cref="ScaleImage(double,double)"/> 压进小范围；建议在 float 域，输出类型/溢出处理 [待实测]。baseVal 走 <c>StoreS</c> 按字符串写入。</para>
	///   <para><b>与相邻算子的取舍</b>各通道不同底数用 <see cref="ExpImage(JlTuple)"/>。</para>
	///   <para><b>参数取向</b>示例传字符串字面量（"e"），它精确匹配本重载、不会误落到 JlTuple 重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage f = new JlImage("float", 640, 480);
	///   using JlImage small = f.ScaleImage(0.01, 0.0);   // 先压范围
	///   using JlImage e = small.ExpImage("e");          // e^g
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage ExpImage(string baseVal)
	{
		IntPtr proc = JlNativeApi.PreCall(1515);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, baseVal);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素对数运算的元组重载（原生 id 1516）：out = log_baseVal(g)，底数可按通道给。</summary>
	/// <param name="baseVal">各通道对数底（元组，可含 "e" 或数值）。Default: "e"</param>
	/// <returns>对数结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量版同 id 1516，底数走 <c>Store</c>+<c>UnpinTuple</c>，可给多通道各设底。把像素灰度取对数，压缩大动态范围（乘性/亮度分布转加性），或对数域分解后再 <see cref="ExpImage(JlTuple)"/> 合成。</para>
	///   <para><b>约束或前提</b>定义域 g&gt;0：g=0 → −∞、g&lt;0 → 无定义，处理 [待实测]，运算前应垫最小正值或先 <see cref="ScaleImage(double,double)"/> 抬离 0。建议在 float 域；baseVal 长度与通道匹配 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>全图同底用 <see cref="LogImage(string)"/> 免固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage f = new JlImage("float", 640, 480);
	///   using JlImage pos = f.ScaleImage(1.0, 1.0);                        // 抬离 0，保 g&gt;0
	///   using JlImage lg = pos.LogImage(new JlTuple(2.718281828459045));  // 自然对数
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；结果含负值，回看 byte 前需再抬偏移。</para>
	/// </remarks>
	public JlImage LogImage(JlTuple baseVal)
	{
		IntPtr proc = JlNativeApi.PreCall(1516);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, baseVal);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(baseVal);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素对数运算标量重载（原生 id 1516）：out = log_baseVal(g)，全图同一底数。</summary>
	/// <param name="baseVal">对数底："e" 或数值字符串（如 "2"、"10"）。Default: "e"</param>
	/// <returns>对数结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把每个灰度取 baseVal 为底的对数，baseVal="e" 即 ln。典型：光学密度/透射率换算、把乘性分量（光照、增益）拆成加性便于后续分离，配 <see cref="ExpImage(string)"/> 互逆。</para>
	///   <para><b>约束或前提</b>定义域 g&gt;0：0 与负像素产生 −∞/NaN [待实测]，byte 图含纯黑时务必先抬底（ScaleImage add&gt;0 或垫常数）。底数走 <c>StoreS</c> 字符串。</para>
	///   <para><b>与相邻算子的取舍</b>各通道不同底数用 <see cref="LogImage(JlTuple)"/>。</para>
	///   <para><b>参数取向</b>示例传字符串字面量（"e"），精确匹配本重载、不误落 JlTuple 重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage f = new JlImage("float", 640, 480);
	///   using JlImage pos = f.ScaleImage(1.0, 1.0);   // 抬离 0，保 g&gt;0
	///   using JlImage lg = pos.LogImage("10");        // 以 10 为底
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；结果含负值，落整型前需处理。</para>
	/// </remarks>
	public JlImage LogImage(string baseVal)
	{
		IntPtr proc = JlNativeApi.PreCall(1516);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, baseVal);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>两图逐像素四象限反正切（原生 id 1517）：out = atan2(this, imageX)，值域 (−π, π]。</summary>
	/// <param name="imageX">分母图 X（this 为分子图 Y）。</param>
	/// <returns>角度结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本实例是分子 Y、imageX 是分母 X，逐像素 atan2(Y,X) 给出方向角。相比 <see cref="AtanImage()"/> 的单值域 (−π/2, π/2)，本算子靠 X 的符号区分象限，覆盖整圈 (−π, π]——典型用法是把梯度分量 gx(X)、gy(Y) 合成每像素法线/流向角。</para>
	///   <para><b>约束或前提</b>两图同尺寸同类型；X=0 处 atan2 仍按 Y 定 ±π/2，但 Y=X=0 的角无定义 [待实测]。输出是弧度且含负值，落 byte/查看前先抬偏移或换 float。</para>
	///   <para><b>与相邻算子的取舍</b>只有斜率 Y/X 无 X 符号信息时用 <see cref="AtanImage()"/>；要全象限角必须用本算子，参数别写反（this 必须是 Y）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage gy = new JlImage("float", 640, 480);   // Y 分量（分子）
	///   JlImage gx = new JlImage("float", 640, 480);   // X 分量（分母）
	///   using JlImage angle = gy.Atan2Image(gx);       // 每像素方向角
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；this 与 imageX 由 GC.KeepAlive 保活。</para>
	/// </remarks>
	public JlImage Atan2Image(JlImage imageX)
	{
		IntPtr proc = JlNativeApi.PreCall(1517);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageX);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageX);
		return obj;
	}

	/// <summary>逐像素反正切（原生 id 1518）：输入是任意实数斜率场，输出 (−π/2, π/2) 弧度。</summary>
	/// <returns>atan 结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把"斜率/正切值"图转回角度：atan 无定义域限制（全体实数），输出只覆盖 ±π/2 —— 象限信息本就不在单分量里。</para>
	///   <para><b>约束或前提</b>需要全象限角（±π）时必须配分母图走 <see cref="Atan2Image(JlImage)"/>，用本算子会把反向量折叠成同角。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage slope = new JlImage("float", 640, 480);
	///   using JlImage angle = slope.AtanImage();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；输出含负弧度，byte 域查看前抬偏移。</para>
	/// </remarks>
	public JlImage AtanImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1518);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素反余弦（原生 id 1519）：输入必须已归一到 [−1,1]，输出 [0,π] 弧度。</summary>
	/// <returns>acos 结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把 cos 分量反解为角度：值域 [0,π] 比 asin 多出"钝角侧"，但同样丢 sin 符号信息（θ 与 −θ 不可分）。常见于方向编码图/着色球标定图的解码。</para>
	///   <para><b>约束或前提</b>定义域外像素处理 [待实测]；byte 灰度先归一再反解，量纲约定同 <see cref="AsinImage()"/>。</para>
	///   <para><b>与相邻算子的取舍</b>两分量齐备时用 <see cref="Atan2Image(JlImage)"/> 一步到位。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage c = new JlImage("float", 640, 480);
	///   using JlImage angle = c.AcosImage();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage AcosImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1519);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素反正弦（原生 id 1520）：输入必须已归一到 [−1,1]，输出为弧度。</summary>
	/// <returns>asin 结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把 sin 分量图反解回角度场（干涉条纹相位解码的一环）。定义域只有 [−1,1]：byte 灰度 0..255 绝大多数越界，必须先 <see cref="ScaleImage(double,double)"/> 归一（如 ×(2/255)−1 映到 [−1,1]）。</para>
	///   <para><b>约束或前提</b>越界像素的处理（钳位还是 NaN）[待实测]；asin 只能还原 [−π/2, π/2]，丢失的象限信息要靠 cos 分量或符号位补。</para>
	///   <para><b>与相邻算子的取舍</b>有正交两分量时优先 <see cref="Atan2Image(JlImage)"/>，全象限无歧义。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage s = new JlImage("float", 640, 480);
	///   using JlImage norm = s.ScaleImage(1.0, 0.0);   // 确保已在 [−1,1]
	///   using JlImage angle = norm.AsinImage();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage AsinImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1520);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素正切（原生 id 1521）：灰度按弧度解释；渐近线附近输出发散。</summary>
	/// <returns>tan 结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把角度场转斜率（如已知每像素法线角求梯度比值）。值域无界：角度越接近 π/2+kπ（byte 弧度域即 1.57、4.71…）数值越爆，溢出/截断行为 [待实测]。</para>
	///   <para><b>约束或前提</b>输入是弧度不是角度也不是灰度——不先换算值域时结果无意义（同 sin/cos 的量纲坑）。</para>
	///   <para><b>与相邻算子的取舍</b>需要无界→有界映射时考虑先 <see cref="ScaleImage(double,double)"/> 把角度域限制在 ±π/4 内（tan∈[−1,1]）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage angle = new JlImage("float", 640, 480);            // 弧度制角度场
	///   using JlImage clipped = angle.ScaleImage(0.5, 0.0);        // 压离渐近线
	///   using JlImage slope = clipped.TanImage();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage TanImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1521);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素余弦（原生 id 1522）：灰度按弧度解释，输出落在 [−1,1]。</summary>
	/// <returns>cos 结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 sin 同一套量纲约定：像素即弧度。相位场做 cos 分量（如自算傅里叶条纹、方向编码图的三角解码）用本算子，配 <see cref="SinImage()"/> 得正交两分量。</para>
	///   <para><b>约束或前提</b>byte 图 0..255 弧度会高频卷绕出莫尔状假象，必须先换算值域；输出负值在整型域的处理 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要 [−1,1]→角度反解用 <see cref="AcosImage()"/>；一般"以方向合成幅值"优先 <see cref="Atan2Image(JlImage)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage phase = new JlImage("float", 640, 480);   // 弧度制相位场
	///   using JlImage c = phase.CosImage();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage CosImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1522);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素正弦（原生 id 1523）：灰度按弧度解释，输出落在 [−1,1]。</summary>
	/// <returns>sin 结果的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>像素值被直接当弧度代入。sin 周期 2π≈6.283：byte 图灰度 0..255 会绕周期约 40 次、输出成无意义的高频波纹——先把灰度换算成目标弧度域（<see cref="ScaleImage(double,double)"/>）再调用。</para>
	///   <para><b>约束或前提</b>输出含负值，落在 byte 域会丢负半周（输出类型规则 [待实测]），float 域或加偏置后再看。</para>
	///   <para><b>与相邻算子的取舍</b>cos 即相位差 π/2 的 sin；已知对边/邻边求角度用 <see cref="Atan2Image(JlImage)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage phase = new JlImage("float", 640, 480);   // 已换算成弧度的相位场
	///   using JlImage s = phase.SinImage();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage SinImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1523);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>绝对差的元组重载：mult 可按通道各给放大率，钉元组走原生 id 1524。</summary>
	/// <param name="image2">参与比较的第二幅图（this 为第一幅）。</param>
	/// <param name="mult">各通道差值放大系数（元组，可多值）。Default: 1.0</param>
	/// <returns>绝对差图的新句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量版同 id 1524；mult 走 <c>Store</c>+<c>UnpinTuple</c>。彩色差分对蓝色通道噪声更敏感时，可给 B 较小放大率、R/G 较大。</para>
	///   <para><b>约束或前提</b>元组长度=通道数或单值广播 [待实测：不符行为]；两图同尺寸。</para>
	///   <para><b>参数取向</b>全通道同一放大率用 <see cref="AbsDiffImage(JlImage,double)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage rgb = new JlImage("byte", 640, 480);
	///   JlImage reference = new JlImage("byte", 640, 480);
	///   using JlImage diff = rgb.AbsDiffImage(reference, new JlTuple(new double[] { 4.0, 4.0, 2.0 }));
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage AbsDiffImage(JlImage image2, JlTuple mult)
	{
		IntPtr proc = JlNativeApi.PreCall(1524);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, mult);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mult);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>两图逐像素绝对差 ×mult（原生 id 1524），帧间差异/缺陷检测的首选。</summary>
	/// <param name="image2">参与比较的第二幅图（this 为第一幅）。</param>
	/// <param name="mult">差值放大系数。Default: 1.0</param>
	/// <returns>绝对差图的新句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>out = |this − image2|×mult。结果恒非负、无方向，mult 把微弱差异线性放大到便于 <see cref="Threshold(double,double)"/> 的幅值域——典型如当前帧对参考帧找缺陷/变化区域。</para>
	///   <para><b>约束或前提</b>两图同尺寸同通道；mult 是纯增益不是判据，放太大会把噪声与真缺陷一起放大，判据留给后续 Threshold。</para>
	///   <para><b>与相邻算子的取舍</b>要区分变亮/变暗（有方向的差）→ <see cref="SubImage(JlImage,double,double)"/>；本算子没有 add 参数，基线偏移要靠后续 ScaleImage 补。</para>
	///   <para><b>参数取向</b>逐通道不同放大率用 <see cref="AbsDiffImage(JlImage,JlTuple)"/>；本重载 <c>StoreD</c> 直写槽 0。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage cur = new JlImage("byte", 640, 480);
	///   JlImage reference = new JlImage("byte", 640, 480);
	///   using JlImage diff = cur.AbsDiffImage(reference, 4.0);
	///   using JlRegion bad = diff.Threshold(30.0, 255.0);
	///   </code>
	///   <para><b>资源与坑</b>diff 与 bad 均为新句柄，都要释放。</para>
	/// </remarks>
	public JlImage AbsDiffImage(JlImage image2, double mult)
	{
		IntPtr proc = JlNativeApi.PreCall(1524);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.StoreD(proc, 0, mult);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>逐像素开平方（原生 id 1525），把"平方量纲"的结果拉回线性量纲。</summary>
	/// <returns>开方后的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>常作自算幅值的最后一步：梯度两分量各 <see cref="PowImage(double)"/>(2.0) 相加后用本算子还原 sqrt(gx²+gy²)，或把方差/能量图转回标准差量纲。</para>
	///   <para><b>约束或前提</b>负像素的处理 [待实测]；byte 输入开方把 0..255 压进 0..15，低位精度大量丢失——先 <see cref="ConvertImageType(string)"/>("float") 再开方。</para>
	///   <para><b>与相邻算子的取舍</b>要 |g| 不是开方 → <see cref="AbsImage()"/>；任意次幂（含开方等价的 0.5 次幂）→ <see cref="PowImage(double)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("float", 640, 480);
	///   using JlImage energy = img.PowImage(2.0);
	///   using JlImage mag = energy.SqrtImage();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage SqrtImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1525);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>两图相减的元组重载：mult/add 可按通道各给值，钉元组传入，原生 id 1526。</summary>
	/// <param name="imageSubtrahend">减数图像（this 为被减数）。</param>
	/// <param name="mult">各通道校正系数（元组，可多值）。Default: 1.0</param>
	/// <param name="add">各通道校正偏移（元组，可多值）。Default: 128.0</param>
	/// <returns>逐像素相减并适配后的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>out = (this − imageSubtrahend) × mult + add，结果经图标槽 1 由 LoadNew 装载为新句柄。与标量版同 id 1526 但写入路径不同：mult/add 以元组经 <c>Store</c> 钉住传入，CallProcedure 一返回就 <c>UnpinTuple</c> 解钉（在 LoadNew/PostCall 之前，失败也不会把元组钉死在原生端）；标量版 <c>StoreD</c> 直写没有钉固定开销。</para>
	///   <para><b>原生参数序</b>与 C# 形参序不一致：mult、add 占控制参数槽 0、1，两幅图作为图标参数分别落在槽 1、2。</para>
	///   <para><b>约束或前提</b>两图需同尺寸同通道数；元组长度应等于通道数（逐通道增益/基线）或单值广播，长度不符的行为 [待实测]；byte 域负差值的处理 [待实测]。即使每个元组只有一个元素也照走钉固定路径，不比标量重载便宜。</para>
	///   <para><b>与相邻算子的取舍</b>全通道同一系数用 <see cref="SubImage(JlImage,double,double)"/>；只关心差幅不关心方向用 <c>AbsDiffImage</c> 更稳；带符号差值要转幅值再走 <c>AbsImage()</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage rgb = new JlImage("byte", 640, 480);
	///   JlImage bg = new JlImage("byte", 640, 480);
	///   using JlImage diff = rgb.SubImage(bg, new double[] { 1.0, 1.0, 1.0 },
	///       new double[] { 100.0, 128.0, 156.0 });   // 逐通道不同基线，double[] 隐式转 JlTuple
	///   </code>
	///   <para><b>资源与坑</b>diff 是新句柄需释放；<c>GC.KeepAlive</c> 让 this 与 imageSubtrahend 撑到原生调用结束，返回后入参可安全 Dispose。</para>
	/// </remarks>
	public JlImage SubImage(JlImage imageSubtrahend, JlTuple mult, JlTuple add)
	{
		IntPtr proc = JlNativeApi.PreCall(1526);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageSubtrahend);
		JlNativeApi.Store(proc, 0, mult);
		JlNativeApi.Store(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mult);
		JlNativeApi.UnpinTuple(add);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageSubtrahend);
		return obj;
	}

	/// <summary>两图逐像素相减后加适配 out = (this − imageSubtrahend)×mult + add，返回新句柄。</summary>
	/// <param name="imageSubtrahend">减数图像（this 为被减数）。</param>
	/// <param name="mult">校正系数。Default: 1.0</param>
	/// <param name="add">校正偏移。Default: 128.0</param>
	/// <returns>逐像素相减并适配后的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1526。默认 add=128 的用意：差值天然带负，抬 128 让"无变化"落在中间灰度、变亮/变暗各占半量程，可视化或再 Threshold 双向取段都方便。</para>
	///   <para><b>约束或前提</b>两图同尺寸同通道；byte 域负差值的处理（截断到 0 / 环绕 / 换 signed 输出）[待实测] —— 只关心"差多少"不关心方向时用 <see cref="AbsDiffImage(JlImage,double)"/> 更稳。</para>
	///   <para><b>与相邻算子的取舍</b>保方向的差 → 本算子；只要差幅 → AbsDiffImage；带符号结果要转幅值 → 再 <see cref="AbsImage()"/>。</para>
	///   <para><b>参数取向</b>逐通道系数用 <see cref="SubImage(JlImage,JlTuple,JlTuple)"/>；本重载 <c>StoreD</c> 直写（槽 0/1）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage cur = new JlImage("byte", 640, 480);
	///   JlImage bg = new JlImage("byte", 640, 480);
	///   using JlImage diff = cur.SubImage(bg, 1.0, 128.0);
	///   using JlRegion brighter = diff.Threshold(160.0, 255.0);   // 比背景亮的部分
	///   </code>
	///   <para><b>资源与坑</b>diff 与 region 都是新句柄，都要释放。</para>
	/// </remarks>
	public JlImage SubImage(JlImage imageSubtrahend, double mult, double add)
	{
		IntPtr proc = JlNativeApi.PreCall(1526);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, imageSubtrahend);
		JlNativeApi.StoreD(proc, 0, mult);
		JlNativeApi.StoreD(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(imageSubtrahend);
		return obj;
	}

	/// <summary>灰度线性变换的元组重载：mult/add 按通道给值（原生 id 1527，钉元组传入）。</summary>
	/// <param name="mult">各通道缩放系数（元组，可多值）。Default: 0.01</param>
	/// <param name="add">各通道偏移（元组，可多值）。Default: 0</param>
	/// <returns>线性变换后的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>同标量版 id 1527，mult/add 走 <c>Store</c>+<c>UnpinTuple</c>；三通道图可给 (0.9,1.0,1.1) 这类逐通道增益做简易白平衡/通道平衡。</para>
	///   <para><b>约束或前提</b>元组长度=通道数或单值广播 [待实测：不符行为]；负值域处理与取整规则同标量版 [待实测]。</para>
	///   <para><b>参数取向</b>全通道同一映射用 <see cref="ScaleImage(double,double)"/> 更省。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage rgb = new JlImage("byte", 640, 480);
	///   using JlImage wb = rgb.ScaleImage(new JlTuple(new double[] { 0.9, 1.0, 1.1 }), new JlTuple(0.0));
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage ScaleImage(JlTuple mult, JlTuple add)
	{
		IntPtr proc = JlNativeApi.PreCall(1527);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, mult);
		JlNativeApi.Store(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mult);
		JlNativeApi.UnpinTuple(add);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>灰度线性变换 out = g×mult + add：量程伸缩、加偏置、反相的通用工具。</summary>
	/// <param name="mult">缩放系数。Default: 0.01</param>
	/// <param name="add">平移偏移。Default: 0</param>
	/// <returns>线性变换后的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1527，纯逐点仿射、不参考任何图像统计量。常用两处：把 float 结果域（微分的 ±几）映射进 byte 可显示的 0..255（负响应抬高 add 到半量程再 <see cref="Threshold(double,double)"/>）；mult 取负即黑白反相，比 <see cref="InvertImage()"/> 幅度可控。</para>
	///   <para><b>约束或前提</b>结果越出输出类型域时的截断/回绕 [待实测]；对 byte 乘小数会取整丢低位精度，取整方式 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>想让映射自动贴合"实测 min/max 铺满 0..255" → <see cref="ScaleImageMax()"/>，本算子系数全靠手算；按直方图统计拉开对比 → <see cref="EquHistoImage()"/>。</para>
	///   <para><b>参数取向</b>逐通道不同映射用 <see cref="ScaleImage(JlTuple,JlTuple)"/>；本重载 <c>StoreD</c> 直写、零固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage dim = img.ScaleImage(0.5, 64.0);   // 压对比度并抬高黑位
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage ScaleImage(double mult, double add)
	{
		IntPtr proc = JlNativeApi.PreCall(1527);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, mult);
		JlNativeApi.StoreD(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>两图相除的元组重载：mult/add 可按通道给值，钉元组走原生 id 1528。</summary>
	/// <param name="image2">分母图像（this 为分子）。</param>
	/// <param name="mult">各通道适配系数（元组，可多值）。Default: 255</param>
	/// <param name="add">各通道适配偏移（元组，可多值）。Default: 0</param>
	/// <returns>逐像素相除并适配后的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量版同 id 1528；mult/add 走 <c>Store</c>+<c>UnpinTuple</c>，平场校正遇到 Bayer 增益不同的通道时可逐通道归一。</para>
	///   <para><b>约束或前提</b>分母含 0 的像素风险同标量版 [待实测]；元组长度与通道数匹配规则 [待实测]。</para>
	///   <para><b>参数取向</b>全图同一系数用 <see cref="DivImage(JlImage,double,double)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage rgb = new JlImage("byte", 640, 480);
	///   JlImage flat = new JlImage("byte", 640, 480);
	///   using JlImage corr = rgb.DivImage(flat, new JlTuple(new double[] { 255.0, 250.0, 245.0 }),
	///       new JlTuple(0.0));
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage DivImage(JlImage image2, JlTuple mult, JlTuple add)
	{
		IntPtr proc = JlNativeApi.PreCall(1528);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, mult);
		JlNativeApi.Store(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mult);
		JlNativeApi.UnpinTuple(add);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>两图逐像素相除加适配 out = this÷image2×mult + add，返回新句柄；平场校正主入口。</summary>
	/// <param name="image2">分母图像（this 为分子）。</param>
	/// <param name="mult">灰度范围适配系数。Default: 255</param>
	/// <param name="add">灰度范围适配偏移。Default: 0</param>
	/// <returns>逐像素相除并适配后的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1528。经典用法是平场/阴影校正：原图÷平场参考图，再×满量程还原对比度（默认 mult=255 正对应 byte 满量程）。除法逐点归一，能消掉乘性不均匀（光照梯度、镜头渐晕）。</para>
	///   <para><b>约束或前提</b>分母为 0 或近 0 的像素是最大风险：结果爆炸或非法值，处理 [待实测]；byte 分母图含纯黑区时应先给分母垫底（如 <see cref="MaxImage(JlImage)"/> 一张常数小值图）再除。</para>
	///   <para><b>与相邻算子的取舍</b>乘性叠加 → <see cref="MultImage(JlImage,double,double)"/>；只想抹掉慢变光照且无参考平场 → <see cref="Illuminate(int,int,double)"/> 自估背景。</para>
	///   <para><b>参数取向</b>逐通道系数用 <see cref="DivImage(JlImage,JlTuple,JlTuple)"/>；本重载 <c>StoreD</c> 直写（槽 0/1）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlImage flat = new JlImage("byte", 640, 480);
	///   using JlImage corr = img.DivImage(flat, 255.0, 0.0);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；输出类型 [待实测]。</para>
	/// </remarks>
	public JlImage DivImage(JlImage image2, double mult, double add)
	{
		IntPtr proc = JlNativeApi.PreCall(1528);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.StoreD(proc, 0, mult);
		JlNativeApi.StoreD(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>两图乘积的元组重载：mult/add 可按通道给值，钉元组走原生 id 1529。</summary>
	/// <param name="image2">第二幅输入图像（this 为第一幅）。</param>
	/// <param name="mult">各通道适配系数（元组，可多值）。Default: 0.005</param>
	/// <param name="add">各通道适配偏移（元组，可多值）。Default: 0</param>
	/// <returns>逐像素相乘并适配后的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量版同 id 1529；mult/add 走 <c>Store</c>+<c>UnpinTuple</c> 钉固定元组，可给每通道不同增益（如对 RGB 乘同一权重图但三通道增益不同）。</para>
	///   <para><b>约束或前提</b>元组长度=通道数或单值广播 [待实测：长度不符行为]；两图同尺寸。</para>
	///   <para><b>参数取向</b>全图同一系数用 <see cref="MultImage(JlImage,double,double)"/> 免固定开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage rgb = new JlImage("byte", 640, 480);
	///   JlImage w = new JlImage("byte", 640, 480);
	///   using JlImage blend = rgb.MultImage(w, new JlTuple(new double[] { 0.004, 0.005, 0.006 }),
	///       new JlTuple(0.0));
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage MultImage(JlImage image2, JlTuple mult, JlTuple add)
	{
		IntPtr proc = JlNativeApi.PreCall(1529);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, mult);
		JlNativeApi.Store(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mult);
		JlNativeApi.UnpinTuple(add);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>两图逐像素乘积加适配 out = this×image2×mult + add，返回新句柄。</summary>
	/// <param name="image2">第二幅输入图像（this 为第一幅）。</param>
	/// <param name="mult">灰度范围适配系数。Default: 0.005</param>
	/// <param name="add">灰度范围适配偏移。Default: 0</param>
	/// <returns>逐像素相乘并适配后的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1529。乘性合成/加权：图像×0-1 权重图可做局部掩膜融合；byte×byte 乘积最大 65025，默认 0.005 约按 1/200 量级把结果压回可见灰度——实际应按两图满量程换算（byte×byte 想回原量纲用 1/255≈0.00392）。</para>
	///   <para><b>约束或前提</b>两图同尺寸同通道；本重载标量 mult/add 走 <c>StoreD</c>（槽 0/1），image2 占输入槽 2。</para>
	///   <para><b>与相邻算子的取舍</b>除掉乘性背景（平场/阴影）→ <see cref="DivImage(JlImage,double,double)"/>；加性合成 → <see cref="AddImage(JlImage,double,double)"/>。</para>
	///   <para><b>参数取向</b>逐通道系数用 <see cref="MultImage(JlImage,JlTuple,JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlImage weight = new JlImage("byte", 640, 480);
	///   using JlImage blend = img.MultImage(weight, 1.0 / 255.0, 0.0);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage MultImage(JlImage image2, double mult, double add)
	{
		IntPtr proc = JlNativeApi.PreCall(1529);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.StoreD(proc, 0, mult);
		JlNativeApi.StoreD(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>两图加权和的元组重载：mult/add 可按通道各给一个值，钉元组走原生 id 1530。</summary>
	/// <param name="image2">第二幅输入图像（this 为第一幅）。</param>
	/// <param name="mult">各通道灰度适配系数（元组，可多值）。Default: 0.5</param>
	/// <param name="add">各通道灰度适配偏移（元组，可多值）。Default: 0</param>
	/// <returns>逐像素相加并适配后的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与标量版同一原生 id 1530，差别在 mult/add 走 <c>Store</c>（钉固定元组，调用后 <c>UnpinTuple</c>）而非 <c>StoreD</c>，因此可给 RGB 三通道分别设增益/偏置。</para>
	///   <para><b>约束或前提</b>元组长度须与通道数匹配：单值是否广播到全通道、长度不符时行为 [待实测]。两图仍须同尺寸。</para>
	///   <para><b>参数取向</b>全通道同系数时用 <see cref="AddImage(JlImage,double,double)"/> 更省（免固定/解固定）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage rgb1 = new JlImage("byte", 640, 480);
	///   JlImage rgb2 = new JlImage("byte", 640, 480);
	///   using JlImage mix = rgb1.AddImage(rgb2, new JlTuple(new double[] { 0.3, 0.5, 0.2 }),
	///       new JlTuple(0.0));
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；纯数值 JlTuple 无需额外处置。</para>
	/// </remarks>
	public JlImage AddImage(JlImage image2, JlTuple mult, JlTuple add)
	{
		IntPtr proc = JlNativeApi.PreCall(1530);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, mult);
		JlNativeApi.Store(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mult);
		JlNativeApi.UnpinTuple(add);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>两图逐像素加权和 out = (this + image2)×mult + add，返回新句柄。</summary>
	/// <param name="image2">第二幅输入图像（this 为第一幅）。</param>
	/// <param name="mult">灰度范围适配系数。Default: 0.5</param>
	/// <param name="add">灰度范围适配偏移。Default: 0</param>
	/// <returns>逐像素相加并适配后的新图像句柄（需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1530。mult/add 是对"和"的线性适配：默认 0.5 恰是两帧求平均（时域降噪）；两幅 byte 图直加可到 510，必须靠 mult 压回量程，否则溢出（byte 域饱和还是环绕 [待实测]）。</para>
	///   <para><b>约束或前提</b>两图尺寸、通道数须一致才逐点对应；本重载标量 mult/add 走 <c>StoreD</c>（原生参数槽 0/1），image2 占输入槽 2。</para>
	///   <para><b>与相邻算子的取舍</b>逐点取大（非线性合成）→ <see cref="MaxImage(JlImage)"/>；乘性叠加 → <see cref="MultImage(JlImage,double,double)"/>；邻域平均（空间降噪）→ <see cref="MeanImage(int,int)"/>，与"两帧平均"不是一回事。</para>
	///   <para><b>参数取向</b>逐通道不同系数用 <see cref="AddImage(JlImage,JlTuple,JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img1 = new JlImage("byte", 640, 480);
	///   JlImage img2 = new JlImage("byte", 640, 480);
	///   using JlImage avg = img1.AddImage(img2, 0.5, 0.0);   // 两帧平均
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage AddImage(JlImage image2, double mult, double add)
	{
		IntPtr proc = JlNativeApi.PreCall(1530);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.StoreD(proc, 0, mult);
		JlNativeApi.StoreD(proc, 1, add);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>逐像素取绝对值：把 signed/float 结果的负响应转正；byte 输入下近似恒等拷贝。</summary>
	/// <returns>取绝对值后的新图像句柄（非原地改写，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1531，只有 this 一路输入（无标量参数）。有意义的前提是像素可为负：微分/差分（<see cref="SubImage(JlImage,double,double)"/> 不加抬升偏移时）等算子的输出。</para>
	///   <para><b>约束或前提</b>byte/uint2 输入本无负值，调用等于白算一遍还多一份内存；输出类型规则 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要看"两幅图差多少" → 直接 <see cref="AbsDiffImage(JlImage,double)"/> 一步得 |a−b|，不要 SubImage 之后再 AbsImage。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage a = new JlImage("float", 640, 480);
	///   JlImage b = new JlImage("float", 640, 480);
	///   using JlImage diff = a.SubImage(b, 1.0, 0.0);
	///   using JlImage mag = diff.AbsImage();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；diff 若不再用可提前 Dispose。</para>
	/// </remarks>
	public JlImage AbsImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1531);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐像素取两幅图像的较小值（保暗/取下包络），返回新句柄。</summary>
	/// <param name="image2">第二幅输入图像（this 为第一幅）。</param>
	/// <returns>逐像素取 min 的新图像句柄（非原地改写，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1532。对每像素逐通道求 min，常用于压掉亮尖峰（多次曝光取暗包络）、把噪声的亮脉冲钉回参考图的上界。</para>
	///   <para><b>约束或前提</b>两图尺寸须一致（尺寸不同行为 [待实测]），多通道按对应通道分别取小；输出类型由原生按输入类型决定 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要保亮/取上包络 → <see cref="MaxImage(JlImage)"/>；要数值差而非逐点取小 → <see cref="SubImage(JlImage,double,double)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img1 = new JlImage("byte", 640, 480);
	///   JlImage img2 = new JlImage("byte", 640, 480);
	///   using JlImage mn = img1.MinImage(img2);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；image2 在原生调用结束前不得释放（实现内有 GC.KeepAlive）。</para>
	/// </remarks>
	public JlImage MinImage(JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1532);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>逐像素取两幅图像的较大值（保亮/取包络上界），返回新句柄。</summary>
	/// <param name="image2">第二幅输入图像（this 为第一幅）。</param>
	/// <returns>逐像素取 max 的新图像句柄（非原地改写，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对每像素逐通道求 max，常用于叠加/取多次曝光亮部，或把噪声的暗尖峰抬到参考图上界。</para>
	///   <para><b>约束或前提</b>两图尺寸须一致（尺寸不同行为 [待实测]），输出类型由原生按输入类型决定 [待实测]；多通道按对应通道分别取。</para>
	///   <para><b>与相邻算子的取舍</b>要保暗/取下包络 → <see cref="MinImage(JlImage)"/>；要数值和而不仅仅是逐点取大 → <see cref="AddImage(JlImage,double,double)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img1 = new JlImage("byte", 640, 480);
	///   JlImage img2 = new JlImage("byte", 640, 480);
	///   using JlImage mx = img1.MaxImage(img2);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage MaxImage(JlImage image2)
	{
		IntPtr proc = JlNativeApi.PreCall(1533);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>按输出类型的灰度上限翻转每个像素（byte 即 255−g），用于黑白极性互换。</summary>
	/// <returns>取反后的新图像句柄（非原地改写，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>逐像素做 max−gray（byte 时 max=255），使暗变亮、亮变暗；常用于把"暗目标"翻转成可被高灰度 Threshold 选中的形态。</para>
	///   <para><b>与相邻算子的取舍</b>只想做整体线性反相/抬偏 → 用 <see cref="ScaleImage(double,double)"/>（mult 取负、add 补偿），可控幅度且支持浮点；本算子是按类型满量程的"硬反相"，对 int2/float 等有符号类型的 max 取值 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage inv = img.InvertImage();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放。</para>
	/// </remarks>
	public JlImage InvertImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1534);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>对拼接/全景图自动做跨视图亮度与色彩一致性校正，消除重叠区因曝光差异产生的接缝。</summary>
	/// <param name="from">每条对应变换的源图序号（索引元组）。</param>
	/// <param name="to">每条对应变换的目标图序号，与 from 成对。</param>
	/// <param name="referenceImage">作为亮度基准的参考图序号。</param>
	/// <param name="homMatrices2D">各视图间投影矩阵拼成的句柄元组。</param>
	/// <param name="estimationMethod">校正量的估计算法。Default: "standard"</param>
	/// <param name="estimateParameters">要估计的量（如仅灰度乘子）。Default: ["mult_gray"]</param>
	/// <param name="OECFModel">光电响应转换模型。Default: ["laguerre"]</param>
	/// <returns>校正后的新图像句柄（非原地改写，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>以 <paramref name="referenceImage"/> 为基准，沿 from→to 的变换关系在各视图重叠区估计亮度/色彩补偿并回乘，使 mosaic 接缝处过渡平滑；OECF 模型决定补偿是否考虑非线性光电响应。</para>
	///   <para><b>约束或前提</b>from/to 等长且索引须落在输入图元组内（此处 <c>this</c> 提供 Image 输入）；索引/句柄语义 [待实测：从/to 是否要求句柄元组]。<paramref name="referenceImage"/> 走 <c>StoreI</c> 为 int。</para>
	///   <para><b>参数取向</b>本重载 <paramref name="estimateParameters"/> 为 JlTuple 走钉元组 <c>Store</c>+<c>UnpinTuple</c>，可一次给多个待估量；单值用 string 重载更省。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(20.0, 0.0);
	///   JlTuple homMatrices2D = m;   // JlData→JlTuple 隐式转换（单个 2D 变换矩阵；未提供数组版 ConcatArray）
	///   JlTuple from = new JlTuple(new int[] { 0 });
	///   JlTuple to = new JlTuple(new int[] { 1 });
	///   using JlImage corrected = img.AdjustMosaicImages(from, to, 0, homMatrices2D, "standard",
	///       new JlTuple("mult_gray"), "laguerre");
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；<c>JlHomMat2D</c> 无 IDisposable。</para>
	/// </remarks>
	public JlImage AdjustMosaicImages(JlTuple from, JlTuple to, int referenceImage, JlTuple homMatrices2D, string estimationMethod, JlTuple estimateParameters, string OECFModel)
	{
		IntPtr proc = JlNativeApi.PreCall(1535);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, from);
		JlNativeApi.Store(proc, 1, to);
		JlNativeApi.StoreI(proc, 2, referenceImage);
		JlNativeApi.Store(proc, 3, homMatrices2D);
		JlNativeApi.StoreS(proc, 4, estimationMethod);
		JlNativeApi.Store(proc, 5, estimateParameters);
		JlNativeApi.StoreS(proc, 6, OECFModel);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(from);
		JlNativeApi.UnpinTuple(to);
		JlNativeApi.UnpinTuple(homMatrices2D);
		JlNativeApi.UnpinTuple(estimateParameters);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>对拼接/全景图自动做跨视图亮度色彩一致性校正；本重载 estimateParameters 以标量字符串传入。</summary>
	/// <param name="from">每条对应变换的源图序号（索引元组）。</param>
	/// <param name="to">每条对应变换的目标图序号，与 from 成对。</param>
	/// <param name="referenceImage">作为亮度基准的参考图序号。</param>
	/// <param name="homMatrices2D">各视图间投影矩阵拼成的句柄元组。</param>
	/// <param name="estimationMethod">校正量的估计算法。Default: "standard"</param>
	/// <param name="estimateParameters">要估计的量。Default: ["mult_gray"]</param>
	/// <param name="OECFModel">光电响应转换模型。Default: ["laguerre"]</param>
	/// <returns>校正后的新图像句柄（非原地改写，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算法同钉元组版（原生 id 1535），仅 <paramref name="estimateParameters"/> 走 <c>StoreS</c> 直写单值、无固定/解固定；需一次给多个待估量时改用 JlTuple 重载。</para>
	///   <para><b>约束或前提</b>from/to 等长且索引落在输入图元组内；<paramref name="referenceImage"/> 为 int。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(20.0, 0.0);
	///   JlTuple homMatrices2D = m;   // JlData→JlTuple 隐式转换（单个 2D 变换矩阵；未提供数组版 ConcatArray）
	///   JlTuple from = new JlTuple(new int[] { 0 });
	///   JlTuple to = new JlTuple(new int[] { 1 });
	///   using JlImage corrected = img.AdjustMosaicImages(from, to, 0, homMatrices2D, "standard",
	///       "mult_gray", "laguerre");
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；<c>JlHomMat2D</c> 无 IDisposable。</para>
	/// </remarks>
	public JlImage AdjustMosaicImages(JlTuple from, JlTuple to, int referenceImage, JlTuple homMatrices2D, string estimationMethod, string estimateParameters, string OECFModel)
	{
		IntPtr proc = JlNativeApi.PreCall(1535);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, from);
		JlNativeApi.Store(proc, 1, to);
		JlNativeApi.StoreI(proc, 2, referenceImage);
		JlNativeApi.Store(proc, 3, homMatrices2D);
		JlNativeApi.StoreS(proc, 4, estimationMethod);
		JlNativeApi.StoreS(proc, 5, estimateParameters);
		JlNativeApi.StoreS(proc, 6, OECFModel);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(from);
		JlNativeApi.UnpinTuple(to);
		JlNativeApi.UnpinTuple(homMatrices2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>由球面全景/多幅带相机朝向矩阵的图生成 6 个立方图面（cube map）：返回 front 面，其余 5 面经 out 参数给出。</summary>
	/// <param name="rear">输出：后方立方面（新句柄，需释放）。</param>
	/// <param name="left">输出：左方立方面（新句柄，需释放）。</param>
	/// <param name="right">输出：右方立方面（新句柄，需释放）。</param>
	/// <param name="top">输出：上方立方面（新句柄，需释放）。</param>
	/// <param name="bottom">输出：下方立方面（新句柄，需释放）。</param>
	/// <param name="cameraMatrices">决定各图内参的 3×3 投影相机矩阵数组。</param>
	/// <param name="rotationMatrices">决定各图相机朝向的 3×3 旋转矩阵数组。</param>
	/// <param name="cubeMapDimension">每个立方面的宽=高（像素）。Default: 1000</param>
	/// <param name="stackingOrder">重叠区叠放策略。Default: "voronoi"</param>
	/// <param name="interpolation">重采样方式。Default: "bilinear"</param>
	/// <returns>前方（front）立方面的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把输入按相机朝向投影到立方体内壁，产出 6 张 <paramref name="cubeMapDimension"/> 见方的正交面；本包装把 front 面当返回值、后/左/右/上/下 5 面塞进 out——共 6 个新句柄，一个都不能漏释放。</para>
	///   <para><b>约束或前提</b>两矩阵数组等长；<paramref name="cubeMapDimension"/> 是 int（走 <c>StoreI</c>）不是句柄，直接给整数即可。极区/面接缝处的填充 [待实测]。</para>
	///   <para><b>参数取向</b>本重载 <paramref name="stackingOrder"/> 为 JlTuple 走 <c>Store</c>+<c>UnpinTuple</c>；标量策略请用 string 重载。注意 <paramref name="interpolation"/> 恒为 string。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(40.0, 0.0);
	///   JlHomMat2D[] cam = { m };
	///   JlHomMat2D[] rot = { m };
	///   using JlImage front = img.GenCubeMapMosaic(out JlImage rear, out JlImage left, out JlImage right,
	///       out JlImage top, out JlImage bottom, cam, rot, 1000, new JlTuple("voronoi"), "bilinear");
	///   </code>
	///   <para><b>资源与坑</b>front 与 5 个 out 面共 6 个句柄都要 <c>Dispose</c>；<c>JlHomMat2D</c> 无 IDisposable，勿写 using。</para>
	/// </remarks>
	public JlImage GenCubeMapMosaic(out JlImage rear, out JlImage left, out JlImage right, out JlImage top, out JlImage bottom, JlHomMat2D[] cameraMatrices, JlHomMat2D[] rotationMatrices, int cubeMapDimension, JlTuple stackingOrder, string interpolation)
	{
		JlData[] data = cameraMatrices;
		JlTuple hTuple = JlData.ConcatArray(data);
		data = rotationMatrices;
		JlTuple hTuple2 = JlData.ConcatArray(data);
		IntPtr proc = JlNativeApi.PreCall(1536);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, hTuple2);
		JlNativeApi.StoreI(proc, 2, cubeMapDimension);
		JlNativeApi.Store(proc, 3, stackingOrder);
		JlNativeApi.StoreS(proc, 4, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(hTuple2);
		JlNativeApi.UnpinTuple(stackingOrder);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out rear);
		err = LoadNew(proc, 3, err, out left);
		err = LoadNew(proc, 4, err, out right);
		err = LoadNew(proc, 5, err, out top);
		err = LoadNew(proc, 6, err, out bottom);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>生成 6 个立方图面：返回 front 面、其余 5 面经 out 给出；本重载 stackingOrder 以标量字符串传入。</summary>
	/// <param name="rear">输出：后方立方面（新句柄，需释放）。</param>
	/// <param name="left">输出：左方立方面（新句柄，需释放）。</param>
	/// <param name="right">输出：右方立方面（新句柄，需释放）。</param>
	/// <param name="top">输出：上方立方面（新句柄，需释放）。</param>
	/// <param name="bottom">输出：下方立方面（新句柄，需释放）。</param>
	/// <param name="cameraMatrices">决定各图内参的 3×3 投影相机矩阵数组。</param>
	/// <param name="rotationMatrices">决定各图相机朝向的 3×3 旋转矩阵数组。</param>
	/// <param name="cubeMapDimension">每个立方面的宽=高（像素）。Default: 1000</param>
	/// <param name="stackingOrder">重叠区叠放策略。Default: "voronoi"</param>
	/// <param name="interpolation">重采样方式。Default: "bilinear"</param>
	/// <returns>前方（front）立方面的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与钉元组版同（原生 id 1536），仅 <paramref name="stackingOrder"/> 走 <c>StoreS</c> 直写、无固定/解固定，是单值策略首选。</para>
	///   <para><b>约束或前提</b><paramref name="cubeMapDimension"/> 为 int，直接给整数；两矩阵数组等长。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(40.0, 0.0);
	///   JlHomMat2D[] cam = { m };
	///   JlHomMat2D[] rot = { m };
	///   using JlImage front = img.GenCubeMapMosaic(out JlImage rear, out JlImage left, out JlImage right,
	///       out JlImage top, out JlImage bottom, cam, rot, 1000, "voronoi", "bilinear");
	///   </code>
	///   <para><b>资源与坑</b>front 与 5 个 out 面共 6 个句柄都要 <c>Dispose</c>；<c>JlHomMat2D</c> 无 IDisposable，勿写 using。</para>
	/// </remarks>
	public JlImage GenCubeMapMosaic(out JlImage rear, out JlImage left, out JlImage right, out JlImage top, out JlImage bottom, JlHomMat2D[] cameraMatrices, JlHomMat2D[] rotationMatrices, int cubeMapDimension, string stackingOrder, string interpolation)
	{
		JlData[] data = cameraMatrices;
		JlTuple hTuple = JlData.ConcatArray(data);
		data = rotationMatrices;
		JlTuple hTuple2 = JlData.ConcatArray(data);
		IntPtr proc = JlNativeApi.PreCall(1536);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, hTuple2);
		JlNativeApi.StoreI(proc, 2, cubeMapDimension);
		JlNativeApi.StoreS(proc, 3, stackingOrder);
		JlNativeApi.StoreS(proc, 4, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(hTuple2);
		err = LoadNew(proc, 1, err, out var obj);
		err = LoadNew(proc, 2, err, out rear);
		err = LoadNew(proc, 3, err, out left);
		err = LoadNew(proc, 4, err, out right);
		err = LoadNew(proc, 5, err, out top);
		err = LoadNew(proc, 6, err, out bottom);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把多幅带内外参/旋转矩阵的图像展开到经纬度网格上，生成一幅球面全景（equirectangular）。</summary>
	/// <param name="cameraMatrices">决定各图内参的 3×3 投影相机矩阵数组。</param>
	/// <param name="rotationMatrices">决定各图相机朝向的 3×3 旋转矩阵数组，与 cameraMatrices 等长。</param>
	/// <param name="latMin">全景最小纬度（度）。Default: -90</param>
	/// <param name="latMax">全景最大纬度（度）。Default: 90</param>
	/// <param name="longMin">全景最小经度（度）。Default: -180</param>
	/// <param name="longMax">全景最大经度（度）。Default: 180</param>
	/// <param name="latLongStep">经纬度采样步长（度），直接决定全景分辨率。Default: 0.1</param>
	/// <param name="stackingOrder">重叠区叠放策略。Default: "voronoi"</param>
	/// <param name="interpolation">重采样方式。Default: "bilinear"</param>
	/// <returns>球面全景新图像句柄（非原地改写，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把每张输入图按其相机/旋转变换反投影到球面，再以 <paramref name="latLongStep"/> 的经纬网格重采样展开；输出宽×高≈((longMax-longMin)/step)×((latMax-latMin)/step)，<paramref name="latLongStep"/> 越小越清晰也越吃内存。</para>
	///   <para><b>约束或前提</b>经纬度均以度为单位、闭区间采样；两矩阵数组长度须一致。重叠区谁覆盖谁由 <paramref name="stackingOrder"/>（voronoi 取最近相机等）决定 [待实测：非 voronoi 取值语义]。</para>
	///   <para><b>与相邻算子的取舍</b>要立方六面贴图 → <see cref="GenCubeMapMosaic(out JlImage,out JlImage,out JlImage,out JlImage,out JlImage,JlHomMat2D[],JlHomMat2D[],int,JlTuple,string)"/>；两两局部单应拼平面全景 → 投影拼接族。</para>
	///   <para><b>参数取向</b>本重载经纬/step/stackingOrder/interpolation 全走 <c>Store</c>+<c>UnpinTuple</c>（钉元组）；标量常量请用 double/string 重载更省，见下。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(40.0, 0.0);
	///   JlHomMat2D[] cam = { m };
	///   JlHomMat2D[] rot = { m };
	///   using JlImage sph = img.GenSphericalMosaic(cam, rot,
	///       new JlTuple(-90.0), new JlTuple(90.0), new JlTuple(-180.0), new JlTuple(180.0),
	///       new JlTuple(0.1), new JlTuple("voronoi"), new JlTuple("bilinear"));
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；<c>JlHomMat2D</c> 无 IDisposable。全景图尺寸由步长决定，step 取很小时会瞬间撑爆内存。</para>
	/// </remarks>
	public JlImage GenSphericalMosaic(JlHomMat2D[] cameraMatrices, JlHomMat2D[] rotationMatrices, JlTuple latMin, JlTuple latMax, JlTuple longMin, JlTuple longMax, JlTuple latLongStep, JlTuple stackingOrder, JlTuple interpolation)
	{
		JlData[] data = cameraMatrices;
		JlTuple hTuple = JlData.ConcatArray(data);
		data = rotationMatrices;
		JlTuple hTuple2 = JlData.ConcatArray(data);
		IntPtr proc = JlNativeApi.PreCall(1537);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, hTuple2);
		JlNativeApi.Store(proc, 2, latMin);
		JlNativeApi.Store(proc, 3, latMax);
		JlNativeApi.Store(proc, 4, longMin);
		JlNativeApi.Store(proc, 5, longMax);
		JlNativeApi.Store(proc, 6, latLongStep);
		JlNativeApi.Store(proc, 7, stackingOrder);
		JlNativeApi.Store(proc, 8, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(hTuple2);
		JlNativeApi.UnpinTuple(latMin);
		JlNativeApi.UnpinTuple(latMax);
		JlNativeApi.UnpinTuple(longMin);
		JlNativeApi.UnpinTuple(longMax);
		JlNativeApi.UnpinTuple(latLongStep);
		JlNativeApi.UnpinTuple(stackingOrder);
		JlNativeApi.UnpinTuple(interpolation);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把多幅带相机/旋转矩阵的图像展开成球面全景；本重载经纬/步长以 double、策略以 string 直写（标量常规写法）。</summary>
	/// <param name="cameraMatrices">决定各图内参的 3×3 投影相机矩阵数组。</param>
	/// <param name="rotationMatrices">决定各图相机朝向的 3×3 旋转矩阵数组。</param>
	/// <param name="latMin">全景最小纬度（度）。Default: -90</param>
	/// <param name="latMax">全景最大纬度（度）。Default: 90</param>
	/// <param name="longMin">全景最小经度（度）。Default: -180</param>
	/// <param name="longMax">全景最大经度（度）。Default: 180</param>
	/// <param name="latLongStep">经纬度采样步长（度），决定全景分辨率。Default: 0.1</param>
	/// <param name="stackingOrder">重叠区叠放策略。Default: "voronoi"</param>
	/// <param name="interpolation">重采样方式。Default: "bilinear"</param>
	/// <returns>球面全景新图像句柄（非原地改写，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>语义同钉元组版（原生 id 1537），区别仅在数值/字符串参数走 <c>StoreD</c>/<c>StoreS</c> 直写、无固定/解固定；给单值常量时是本族首选。</para>
	///   <para><b>约束或前提</b>经纬度以度为单位、闭区间；<paramref name="latLongStep"/> 越小全景越大越吃内存。注意此处经纬用 double 字面量，勿误传成裸 int（会与元组重载产生二义/落错重载）。</para>
	///   <para><b>与相邻算子的取舍</b>要立方六面贴图 → <see cref="GenCubeMapMosaic(out JlImage,out JlImage,out JlImage,out JlImage,out JlImage,JlHomMat2D[],JlHomMat2D[],int,string,string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(40.0, 0.0);
	///   JlHomMat2D[] cam = { m };
	///   JlHomMat2D[] rot = { m };
	///   using JlImage sph = img.GenSphericalMosaic(cam, rot, -90.0, 90.0, -180.0, 180.0, 0.1, "voronoi", "bilinear");
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；<c>JlHomMat2D</c> 无 IDisposable。</para>
	/// </remarks>
	public JlImage GenSphericalMosaic(JlHomMat2D[] cameraMatrices, JlHomMat2D[] rotationMatrices, double latMin, double latMax, double longMin, double longMax, double latLongStep, string stackingOrder, string interpolation)
	{
		JlData[] data = cameraMatrices;
		JlTuple hTuple = JlData.ConcatArray(data);
		data = rotationMatrices;
		JlTuple hTuple2 = JlData.ConcatArray(data);
		IntPtr proc = JlNativeApi.PreCall(1537);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, hTuple2);
		JlNativeApi.StoreD(proc, 2, latMin);
		JlNativeApi.StoreD(proc, 3, latMax);
		JlNativeApi.StoreD(proc, 4, longMin);
		JlNativeApi.StoreD(proc, 5, longMax);
		JlNativeApi.StoreD(proc, 6, latLongStep);
		JlNativeApi.StoreS(proc, 7, stackingOrder);
		JlNativeApi.StoreS(proc, 8, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(hTuple2);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把经 bundle adjustment 全局优化过的一组单应矩阵用于拼接全景，并额外给出为把全部内容纳入画布而施加的整体平移矩阵。</summary>
	/// <param name="homMatrices2D">每张输入图对应的 3×3 投影矩阵数组（建议来自光束平差/global optimization，非两两局部单应）。</param>
	/// <param name="stackingOrder">各图叠放次序。Default: "default"</param>
	/// <param name="transformDomain">是否连同定义域一起变换。Default: "false"</param>
	/// <param name="transMat2D">输出：为把所有图完整落进输出画布而叠加的整体 3×3 平移矩阵。</param>
	/// <returns>拼接后的新图像句柄（非原地改写，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与逐对映射的投影拼接不同，本算子假定矩阵已是"每张图→全景"的全局位姿（bundle adjustment 产物），因此无需 mappingSource/Dest；它会把所有变换先平移进正坐标区，平移量以 <paramref name="transMat2D"/> 回吐，供你反向映射坐标时抵消。</para>
	///   <para><b>约束或前提</b>数组长度须与输入图元组（此处 <c>this</c> 提供）一致，否则错位 [待实测：越界/不等长行为]。<paramref name="transMat2D"/> 是新增的一个输出（InitOCT 索引 0）。</para>
	///   <para><b>与相邻算子的取舍</b>手上只有两两之间的局部单应、还需解链 → 用 <see cref="GenProjectiveMosaic(int,JlTuple,JlTuple,JlHomMat2D[],JlTuple,string,out JlHomMat2D[])"/>；球面/立方全景 → 用球面/立方拼接族。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(40.0, 0.0);
	///   JlHomMat2D[] homMatrices2D = { m };
	///   using JlImage mosaic = img.GenBundleAdjustedMosaic(homMatrices2D, new JlTuple("default"), "false",
	///       out JlHomMat2D transMat2D);
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；<c>JlHomMat2D</c> 无 IDisposable，勿写 using。本重载 <paramref name="stackingOrder"/> 走钉元组 <c>Store</c>+<c>UnpinTuple</c>，单值用 string 重载更省。</para>
	/// </remarks>
	public JlImage GenBundleAdjustedMosaic(JlHomMat2D[] homMatrices2D, JlTuple stackingOrder, string transformDomain, out JlHomMat2D transMat2D)
	{
		JlTuple hTuple = JlData.ConcatArray(homMatrices2D);
		IntPtr proc = JlNativeApi.PreCall(1538);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, stackingOrder);
		JlNativeApi.StoreS(proc, 2, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(stackingOrder);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlHomMat2D.LoadNew(proc, 0, err, out transMat2D);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把一组全局优化过的单应矩阵用于拼接全景并给出整体平移矩阵；本重载 stackingOrder 以标量字符串传入。</summary>
	/// <param name="homMatrices2D">每张输入图对应的 3×3 投影矩阵数组（建议来自光束平差，非两两局部单应）。</param>
	/// <param name="stackingOrder">各图叠放次序。Default: "default"</param>
	/// <param name="transformDomain">是否连同定义域一起变换。Default: "false"</param>
	/// <param name="transMat2D">输出：为把所有图完整落进输出画布而叠加的整体 3×3 平移矩阵。</param>
	/// <returns>拼接后的新图像句柄（非原地改写，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与钉元组版同（原生 id 1538），区别仅 <paramref name="stackingOrder"/> 走 <c>StoreS</c> 直写字符串、无固定/解固定开销，是单值叠放方式的常规写法。</para>
	///   <para><b>与相邻算子的取舍</b>只有两两局部单应需解链 → <see cref="GenProjectiveMosaic(int,JlTuple,JlTuple,JlHomMat2D[],string,string,out JlHomMat2D[])"/>；需要多元素 stackingOrder 才改用 JlTuple 重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(40.0, 0.0);
	///   JlHomMat2D[] homMatrices2D = { m };
	///   using JlImage mosaic = img.GenBundleAdjustedMosaic(homMatrices2D, "default", "false",
	///       out JlHomMat2D transMat2D);
	///   </code>
	///   <para><b>资源与坑</b>返回句柄需释放；<c>JlHomMat2D</c> 无 IDisposable，勿写 using。</para>
	/// </remarks>
	public JlImage GenBundleAdjustedMosaic(JlHomMat2D[] homMatrices2D, string stackingOrder, string transformDomain, out JlHomMat2D transMat2D)
	{
		JlTuple hTuple = JlData.ConcatArray(homMatrices2D);
		IntPtr proc = JlNativeApi.PreCall(1538);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.StoreS(proc, 1, stackingOrder);
		JlNativeApi.StoreS(proc, 2, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlHomMat2D.LoadNew(proc, 0, err, out transMat2D);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用逐对给定的单应矩阵把多幅输入图拼成一幅投影拼接全景（mosaic），并回吐每张图在 mosaic 坐标系里的位姿矩阵。</summary>
	/// <param name="startImage">作为拼接基准（中心图）的输入图序号（0 起）。</param>
	/// <param name="mappingSource">每条变换的源图序号数组。</param>
	/// <param name="mappingDest">每条变换的目标图序号数组，与 mappingSource 成对。</param>
	/// <param name="homMatrices2D">把 mappingSource[i] 映到 mappingDest[i] 的 3×3 投影矩阵数组，与映射对数等长。</param>
	/// <param name="stackingOrder">各图叠放次序。Default: "default"</param>
	/// <param name="transformDomain">是否连同定义域一起变换。Default: "false"</param>
	/// <param name="mosaicMatrices2D">输出：每张图从自身坐标到 mosaic 坐标的 3×3 投影矩阵数组。</param>
	/// <returns>拼接后的新图像句柄（非原地改写，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>以 <paramref name="startImage"/> 为基准，沿 <paramref name="mappingSource"/>→<paramref name="mappingDest"/> 的变换链把整幅图拼起来；返回的 <paramref name="mosaicMatrices2D"/> 记录了每张图落到全景后的绝对位姿，可再喂给别的投影变换。</para>
	///   <para><b>约束或前提</b>输入图像本身是一个句柄元组（此处 <c>this</c> 提供该 Image 输入），矩阵数组长度必须与映射对数一致，序号越界行为 [待实测]。拼接结果尺寸由所有图变换后的包围范围决定。</para>
	///   <para><b>与相邻算子的取舍</b>矩阵由 bundle adjustment 估出来的场景 → 用 <see cref="GenBundleAdjustedMosaic(JlHomMat2D[],JlTuple,string,out JlHomMat2D)"/>；要球面/立方图全景 → 用球面/立方拼接族。本算子要求你已手上有两两之间的单应。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(30.0, 0.0);
	///   JlHomMat2D[] homMatrices2D = { m };
	///   JlTuple mappingSource = new JlTuple(0);
	///   JlTuple mappingDest = new JlTuple(1);
	///   using JlImage mosaic = img.GenProjectiveMosaic(0, mappingSource, mappingDest, homMatrices2D,
	///       new JlTuple("default"), "false", out JlHomMat2D[] mosaicMatrices2D);
	///   </code>
	///   <para><b>资源与坑</b>返回句柄与新数组均需释放/弃用；<c>JlHomMat2D</c> 不实现 IDisposable，勿对其写 using。本重载 <paramref name="stackingOrder"/> 走 <c>Store</c>+<c>UnpinTuple</c>（钉元组），单值字符串场景用 string 重载更省。</para>
	/// </remarks>
	public JlImage GenProjectiveMosaic(int startImage, JlTuple mappingSource, JlTuple mappingDest, JlHomMat2D[] homMatrices2D, JlTuple stackingOrder, string transformDomain, out JlHomMat2D[] mosaicMatrices2D)
	{
		JlTuple hTuple = JlData.ConcatArray(homMatrices2D);
		IntPtr proc = JlNativeApi.PreCall(1539);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, startImage);
		JlNativeApi.Store(proc, 1, mappingSource);
		JlNativeApi.Store(proc, 2, mappingDest);
		JlNativeApi.Store(proc, 3, hTuple);
		JlNativeApi.Store(proc, 4, stackingOrder);
		JlNativeApi.StoreS(proc, 5, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mappingSource);
		JlNativeApi.UnpinTuple(mappingDest);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(stackingOrder);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		mosaicMatrices2D = JlHomMat2D.SplitArray(tuple);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>用逐对给定的单应矩阵把多幅输入图拼成投影全景并回吐各图位姿矩阵；本重载 stackingOrder 以标量字符串传入。</summary>
	/// <param name="startImage">作为拼接基准（中心图）的输入图序号（0 起）。</param>
	/// <param name="mappingSource">每条变换的源图序号数组。</param>
	/// <param name="mappingDest">每条变换的目标图序号数组，与 mappingSource 成对。</param>
	/// <param name="homMatrices2D">把 mappingSource[i] 映到 mappingDest[i] 的 3×3 投影矩阵数组。</param>
	/// <param name="stackingOrder">各图叠放次序。Default: "default"</param>
	/// <param name="transformDomain">是否连同定义域一起变换。Default: "false"</param>
	/// <param name="mosaicMatrices2D">输出：每张图从自身坐标到 mosaic 坐标的 3×3 投影矩阵数组。</param>
	/// <returns>拼接后的新图像句柄（非原地改写，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>算法与钉元组的 <see cref="GenProjectiveMosaic(int,JlTuple,JlTuple,JlHomMat2D[],JlTuple,string,out JlHomMat2D[])"/> 同（原生 id 1539），区别仅在 <paramref name="stackingOrder"/> 走 <c>StoreS</c> 直写字符串、无固定/解固定开销，是单值叠放方式的常规写法。</para>
	///   <para><b>与相邻算子的取舍</b>矩阵来自 bundle adjustment → <see cref="GenBundleAdjustedMosaic(JlHomMat2D[],string,string,out JlHomMat2D)"/>；需要多元素 stackingOrder 时才改用 JlTuple 重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(30.0, 0.0);
	///   JlHomMat2D[] homMatrices2D = { m };
	///   JlTuple mappingSource = new JlTuple(0);
	///   JlTuple mappingDest = new JlTuple(1);
	///   using JlImage mosaic = img.GenProjectiveMosaic(0, mappingSource, mappingDest, homMatrices2D,
	///       "default", "false", out JlHomMat2D[] mosaicMatrices2D);
	///   </code>
	///   <para><b>资源与坑</b>返回句柄与新数组需释放/弃用；<c>JlHomMat2D</c> 无 IDisposable，勿写 using。</para>
	/// </remarks>
	public JlImage GenProjectiveMosaic(int startImage, JlTuple mappingSource, JlTuple mappingDest, JlHomMat2D[] homMatrices2D, string stackingOrder, string transformDomain, out JlHomMat2D[] mosaicMatrices2D)
	{
		JlTuple hTuple = JlData.ConcatArray(homMatrices2D);
		IntPtr proc = JlNativeApi.PreCall(1539);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, startImage);
		JlNativeApi.Store(proc, 1, mappingSource);
		JlNativeApi.Store(proc, 2, mappingDest);
		JlNativeApi.Store(proc, 3, hTuple);
		JlNativeApi.StoreS(proc, 4, stackingOrder);
		JlNativeApi.StoreS(proc, 5, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mappingSource);
		JlNativeApi.UnpinTuple(mappingDest);
		JlNativeApi.UnpinTuple(hTuple);
		err = LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		mosaicMatrices2D = JlHomMat2D.SplitArray(tuple);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>对图像做投影（单应）变换，并把结果固定到 width×height 的新画布上。</summary>
	/// <param name="homMat2D">3×3 投影变换矩阵（齐次坐标），以句柄形式钉入原生（调用内 UnpinTuple 解固定）。</param>
	/// <param name="interpolation">输出重采样方式。Default: "bilinear"</param>
	/// <param name="width">输出图像宽度（像素），必须显式给出、无默认值。</param>
	/// <param name="height">输出图像高度（像素），必须显式给出、无默认值。</param>
	/// <param name="transformDomain">是否把定义域一并变换。Default: "false"</param>
	/// <returns>变换后的新图像句柄（非原地改写，需自行释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>按 <paramref name="homMat2D"/> 指定的投影把源图重采样到一个尺寸由调用方定死的画布；这是它与不带 Size 的投影变换的唯一区别——后者沿用原图尺寸或自适应包住全内容。</para>
	///   <para><b>约束或前提</b>投影矩阵含透视分量时，源图只有落在这 width×height 画布内的部分才保留，越界处的边界填充值 [待实测]。<paramref name="width"/>/<paramref name="height"/> 给 0 或负数行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>希望输出自动放大以容纳变换后的全部内容 → 用 <see cref="ProjectiveTransImage(JlHomMat2D,string,string,string)"/> 且 adaptImageSize="true"；平行线需保持平行（无透视）→ 走仿射变换族。单应会把平行线变成会聚线，整幅矫正时注意消失点附近外扩。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D h = new JlHomMat2D().HomMat2dTranslate(20.0, 10.0);
	///   using JlImage outImg = img.ProjectiveTransImageSize(h, "bilinear", 640, 480, "false");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄需释放；<c>JlHomMat2D</c> 不实现 IDisposable，示例中不要对它写 using。</para>
	/// </remarks>
	public JlImage ProjectiveTransImageSize(JlHomMat2D homMat2D, string interpolation, int width, int height, string transformDomain)
	{
		IntPtr proc = JlNativeApi.PreCall(1540);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, homMat2D);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.StoreS(proc, 4, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>对整幅图像做投影（单应）变换，可选是否自动放大画布以容纳变换后的全部内容。</summary>
	/// <param name="homMat2D">3×3 投影变换矩阵（齐次坐标），以句柄形式钉入原生（调用内 Store 后 UnpinTuple 解固定）。</param>
	/// <param name="interpolation">输出重采样方式，决定变换后空白区如何补齐。Default: "bilinear"</param>
	/// <param name="adaptImageSize">是否自动调整输出尺寸以包住变换后的全部内容（"true" 放大、"false" 沿用源图尺寸）。Default: "false"</param>
	/// <param name="transformDomain">是否把图像定义域（域内像素）一并变换。Default: "false"</param>
	/// <returns>变换后的新图像句柄（LoadNew 装载，非原地改写），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1541。本图经 Store 作输入，<paramref name="homMat2D"/> 以钉入句柄写入原生参数 0、interpolation/adaptImageSize/transformDomain 依次以 STRING 写入参数 1/2/3（原生侧参数序与 C# 形参序一致），输出经 InitOCT(1)+LoadNew 返回新句柄。</para>
	///   <para><b>约束或前提</b>本重载不显式给宽高：adaptImageSize="false" 时沿用源图尺寸，变换后越界处按 interpolation 的补边策略填充；需要把结果钉死到指定画布时改用 <see cref="ProjectiveTransImageSize(JlHomMat2D,string,int,int,string)"/>。GC.KeepAlive 保证调用期输入不回收，返回值就绪后输入即可释放。</para>
	///   <para><b>与相邻算子的取舍</b>平行线需保持平行（无透视分量）→ 用仿射族 <see cref="AffineTransImage(JlHomMat2D,string,string)"/>；单应会把平行线变成会聚线，整幅矫正时注意消失点附近内容外扩丢失。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D h = new JlHomMat2D().HomMat2dTranslate(20.0, 10.0);
	///   using JlImage outImg = img.ProjectiveTransImage(h, "bilinear", "false", "false");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；<c>JlHomMat2D</c> 不实现 IDisposable，示例中不要对它写 using。</para>
	/// </remarks>
	public JlImage ProjectiveTransImage(JlHomMat2D homMat2D, string interpolation, string adaptImageSize, string transformDomain)
	{
		IntPtr proc = JlNativeApi.PreCall(1541);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, homMat2D);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.StoreS(proc, 2, adaptImageSize);
		JlNativeApi.StoreS(proc, 3, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>对整幅图像做任意 2D 仿射变换，并把结果固定到 width×height 的新画布上。</summary>
	/// <param name="homMat2D">2×3（齐次 3×3）仿射变换矩阵，以句柄钉入原生（调用内 Store 后 UnpinTuple 解固定）。</param>
	/// <param name="interpolation">输出重采样方式，决定越界处如何补齐。Default: "constant"</param>
	/// <param name="width">输出图像宽度（像素），由调用方定死、无自适应。Default: 640</param>
	/// <param name="height">输出图像高度（像素），由调用方定死、无自适应。Default: 480</param>
	/// <returns>变换后的新图像句柄（LoadNew 装载，非原地改写），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1542。本图经 Store 作输入，<paramref name="homMat2D"/> 以钉入句柄写入原生参数 0、interpolation 以 STRING 写入参数 1、width/height 以 INTEGER 写入参数 2/3（原生侧参数序与 C# 形参序一致），输出经 InitOCT(1)+LoadNew 返回新句柄。与不带 Size 的仿射变换的唯一区别就在于画布尺寸由 width×height 硬性指定。</para>
	///   <para><b>约束或前提</b>变换后落在该 width×height 画布之外的像素被裁掉、不会自动扩图；matrix 含平移时须自行确认内容仍在画布内。width/height 给 0 或负数行为 [待实测]。GC.KeepAlive 保证调用期输入不回收。</para>
	///   <para><b>与相邻算子的取舍</b>想让输出尺寸自动包住全部变换结果 → 用 <see cref="AffineTransImage(JlHomMat2D,string,string)"/> 并置 adaptImageSize="true"；区域而非灰度图做仿射 → 走 AffineTransRegion。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D h = new JlHomMat2D().HomMat2dTranslate(20.0, 10.0);
	///   using JlImage outImg = img.AffineTransImageSize(h, "constant", 640, 480);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；<c>JlHomMat2D</c> 不实现 IDisposable，示例中不要对它写 using。</para>
	/// </remarks>
	public JlImage AffineTransImageSize(JlHomMat2D homMat2D, string interpolation, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1542);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, homMat2D);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>对整幅图像做任意 2D 仿射变换，输出尺寸可沿用源图或自动放大以容纳全部变换结果。</summary>
	/// <param name="homMat2D">2×3（齐次 3×3）仿射变换矩阵，以句柄钉入原生（调用内 Store 后 UnpinTuple 解固定）。</param>
	/// <param name="interpolation">输出重采样方式，决定空白区如何补齐。Default: "constant"</param>
	/// <param name="adaptImageSize">是否自动调整输出尺寸以包住变换后的全部内容（"true" 扩图、"false" 沿用源图尺寸）。Default: "false"</param>
	/// <returns>变换后的新图像句柄（LoadNew 装载，非原地改写），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1543。本图经 Store 作输入，<paramref name="homMat2D"/> 以钉入句柄写入原生参数 0、interpolation/adaptImageSize 依次以 STRING 写入参数 1/2（原生侧参数序与 C# 形参序一致），输出经 InitOCT(1)+LoadNew 返回新句柄。</para>
	///   <para><b>约束或前提</b>adaptImageSize="false" 时只保留仍落在源图尺寸内的内容，含大平移/旋转时会被裁掉；需保住全部内容置 "true"（会放大画布并补边）。缩放/旋转矩阵各分量单位与构造方式见 JlHomMat2D 族。GC.KeepAlive 保证调用期输入不回收。</para>
	///   <para><b>与相邻算子的取舍</b>想把输出钉死到固定 width×height → 用 <see cref="AffineTransImageSize(JlHomMat2D,string,int,int)"/>；含透视分量（平行线不再平行）→ 走投影变换族 ProjectiveTransImage。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D h = new JlHomMat2D().HomMat2dTranslate(20.0, 10.0);
	///   using JlImage outImg = img.AffineTransImage(h, "constant", "true");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；<c>JlHomMat2D</c> 不实现 IDisposable，示例中不要对它写 using。</para>
	/// </remarks>
	public JlImage AffineTransImage(JlHomMat2D homMat2D, string interpolation, string adaptImageSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1543);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, homMat2D);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.StoreS(proc, 2, adaptImageSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>按宽、高各自的比例因子缩放整幅图像，产出新图像句柄。</summary>
	/// <param name="scaleWidth">宽度方向比例因子，&gt;1 放大、&lt;1 缩小，输出宽=源宽×该值。Default: 0.5</param>
	/// <param name="scaleHeight">高度方向比例因子，&gt;1 放大、&lt;1 缩小，输出高=源高×该值。Default: 0.5</param>
	/// <param name="interpolation">重采样方式，决定放大时的平滑与缩小时的抗锯齿。Default: "constant"</param>
	/// <returns>缩放后的新图像句柄（LoadNew 装载，非原地改写），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1544。本图经 Store 作输入，scaleWidth/scaleHeight 以 DOUBLE 写入原生参数 0/1、interpolation 以 STRING 写入参数 2（纯标量直写，无钉固定/解固定开销），输出经 InitOCT(1)+LoadNew 返回新句柄。</para>
	///   <para><b>约束或前提</b>缩放是重采样，缩小后再放大不可逆地丢细节；输出尺寸为源尺寸乘系数取整，非整数比例存在舍入 [待实测]。系数给 0 或负数行为 [待实测]。scaleWidth≠scaleHeight 会破坏长宽比——圆变椭圆、正方形变矩形，几何测量前须注意。</para>
	///   <para><b>与相邻算子的取舍</b>已知目标像素尺寸而不知比例 → 用 <see cref="ZoomImageSize(int,int,string)"/>；想保长宽比就令两系数相等；仅需做带插值的整体仿射缩放可并入仿射族一步完成，避免两次重采样叠加模糊。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage small = img.ZoomImageFactor(0.5, 0.5, "bilinear");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；输入 img 在 GC.KeepAlive 保证调用期内不回收，返回值就绪后可释放。</para>
	/// </remarks>
	public JlImage ZoomImageFactor(double scaleWidth, double scaleHeight, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(1544);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, scaleWidth);
		JlNativeApi.StoreD(proc, 1, scaleHeight);
		JlNativeApi.StoreS(proc, 2, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把整幅图像缩放到指定的 width×height 像素尺寸，产出新图像句柄。</summary>
	/// <param name="width">输出图像宽度（像素），结果被强制为该值。Default: 512</param>
	/// <param name="height">输出图像高度（像素），结果被强制为该值。Default: 512</param>
	/// <param name="interpolation">重采样方式，决定缩放后如何插值补边。Default: "constant"</param>
	/// <returns>缩放后的新图像句柄（LoadNew 装载，非原地改写），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1545。本图经 Store 作输入，width/height 以 INTEGER 写入原生参数 0/1、interpolation 以 STRING 写入参数 2（纯标量直写，无固定开销），输出经 InitOCT(1)+LoadNew 返回新句柄。</para>
	///   <para><b>约束或前提</b>宽高各自独立定死，不保长宽比：源图非 width:height 同比例时会被拉伸（圆变椭圆），几何量测前须自行按源比例算好目标尺寸。width/height 给 0 或负数行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>想按倍率缩放并保比例 → 用 <see cref="ZoomImageFactor(double,double,string)"/>；只需归一化到固定分辨率喂给分类/匹配时本函数更直接。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage norm = img.ZoomImageSize(256, 256, "constant");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；输入 img 由 GC.KeepAlive 保证调用期内不回收。</para>
	/// </remarks>
	public JlImage ZoomImageSize(int width, int height, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(1545);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, width);
		JlNativeApi.StoreI(proc, 1, height);
		JlNativeApi.StoreS(proc, 2, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>沿指定轴镜像（翻转）整幅图像，产出新图像句柄。</summary>
	/// <param name="mode">镜像轴："row" 上下翻转、"column" 左右翻转、"diagonal" 沿主对角线转置（行列互换）。Default: "row"</param>
	/// <returns>翻转后的新图像句柄（LoadNew 装载，非原地改写），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1546。本图经 Store 作输入，<paramref name="mode"/> 以 STRING 写入原生参数 0，输出经 InitOCT(1)+LoadNew 返回新句柄。镜像是像素直搬、不做插值，尺寸（除 diagonal 外）与灰度值均无损。</para>
	///   <para><b>约束或前提</b>坐标系 row=向下为正、column=向右为正："row" 把首行换到末行（上下颠倒），"column" 左右颠倒。仅 "diagonal" 会交换宽与高，输出尺寸转置；另两种保持原尺寸。mode 传非上述取值行为 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>任意角度翻转 → 用 <see cref="RotateImage(JlTuple,string)"/>（含插值）；镜像不改变手性之外的比例，配合 Rotate 可实现四方向翻转，比仿射族更省且无重采样损失。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage flip = img.MirrorImage("row");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；输入 img 由 GC.KeepAlive 保证调用期内不回收。</para>
	/// </remarks>
	public JlImage MirrorImage(string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(1546);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把整幅图像绕自身中心旋转一个角度，产出新图像句柄（本重载 angle 以 JlTuple 传入）。</summary>
	/// <param name="phi">旋转角度（度）的元组。与 <c>RotateImage(double,string)</c> 标量重载同为本函数、同原生 id 1547；多元素元组的取用行为 [待实测]。Default: 90</param>
	/// <param name="interpolation">插值方式，决定旋转产生的空白区如何补齐。Default: "constant"</param>
	/// <returns>旋转后的新图像句柄（LoadNew 装载，非原地改写），用毕须释放。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 1547。本图经 Store 作输入，<paramref name="phi"/> 以钉入句柄的元组写入原生参数 0（调用内 Store 后 UnpinTuple 解固定），interpolation 以 STRING 写入参数 1，输出经 InitOCT(1)+LoadNew 返回新句柄。</para>
	///   <para><b>约束或前提</b>旋转中心固定为图像中心，不能指定支点；需绕任意点转应先平移再转或走仿射族。角度为角度制（据默认值 90 判断）[待实测]。本重载与 double 重载并存构成 CS0121 二义：传字面量会同时匹配两重载，示例必须显式 <c>new JlTuple(...)</c>。元组版有钉固定/解固定开销，单值场景直接用标量重载更省。</para>
	///   <para><b>与相邻算子的取舍</b>90/180/270 度整倍旋转无信息损失且不改尺寸，优先本函数；任意角度是重采样会引入插值模糊，且常量补边产生的角区是填充灰度——紧接着按低灰度 Threshold 会把角区误检为目标。需叠加缩放/错切时改用仿射族一次完成。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlTuple phi = new JlTuple(90.0);
	///   using JlImage rot = img.RotateImage(phi, "constant");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；<c>JlTuple</c> 实现了 IDisposable，纯数值元组可不调用 Dispose。输入 img 由 GC.KeepAlive 保证调用期内不回收。</para>
	/// </remarks>
	public JlImage RotateImage(JlTuple phi, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(1547);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, phi);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(phi);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把本图像绕自身中心旋转一个角度，产出新图像句柄（原生算子 id 1547）。
	/// </summary>
	/// <param name="phi">旋转角度，单位为度（由默认值量级判断为角度制而非弧度制）；正负号对应的转向[待实测]。Default: 90</param>
	/// <param name="interpolation">插值方式字符串，决定旋转后越界像素如何补齐以及输出尺寸策略。Default: "constant"</param>
	/// <returns>旋转后的新图像句柄（LoadNew 装载，非原地改写），用毕须 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本图像经 Store 作为原生输入，phi 以 DOUBLE 写入原生参数 0、interpolation 以 STRING 写入参数 1（原生侧参数序与 C# 形参序一致），输出经 InitOCT(1)+LoadNew 返回新句柄。</para>
	///   <para><b>约束或前提</b>旋转中心固定为图像中心，不能指定任意支点；需要绕指定点旋转时，应先平移到该点再转或用齐次矩阵仿射族。GC.KeepAlive 保证调用期间输入不被回收，返回值就绪后输入即可 Dispose。</para>
	///   <para><b>与相邻算子的取舍</b>只做整幅旋转用本函数即可；若后续还要叠加缩放/错切，直接构造 JlHomMat2D 走仿射变换族一次完成更省，两次重采样会叠加模糊。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   JlImage rot = img.RotateImage(90.0, "constant");
	///   rot.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须用后释放。常量补边模式下旋转产生的角区是填充灰度，若紧接着 Threshold 按低灰度取值，会把角区当成目标误检出来[待实测]。</para>
	/// </remarks>
	public JlImage RotateImage(double phi, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(1547);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, phi);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}






	/// <summary>
	///   把本图像（位移向量场）拟合近似成一个 2D 齐次仿射矩阵（原生算子 id 1551）。
	/// </summary>
	/// <returns>新建的 JlHomMat2D 矩阵对象；注意 JlHomMat2D 派生自 JlData、不实现 IDisposable，不需要也不能 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本图像经 Store 作为唯一输入，输出经 JlHomMat2D.LoadNew 新建。本质是对整场位移做一次仿射近似，逐像素的非刚性分量会被抹平。</para>
	///   <para><b>约束或前提</b>输入必须是向量场图像（两通道位移场，如由 RealToVectorField(row 场, col 场, type) 合成的那种），普通灰度图没有意义[待实测其报错行为]。type 用 relative 还是 absolute 构造会改变位移量纲解释，拟合结果随之不同。</para>
	///   <para><b>与相邻算子的取舍</b>要保留逐像素形变去校正图像时用 UnwarpImageVectorField 直接展平，不必先压成仿射；只拆通道看位移用 VectorFieldToReal；只要一个全局刚性/仿射量（平移+旋转+缩放）才用本函数。本库 JlHomMat2D 上另有实例版 VectorFieldToHomMat2d(JlImage)，往已有矩阵对象里原地写入，二者按持有对象习惯选。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage row = new JlImage("real", 64, 64);
	///   using JlImage col = new JlImage("real", 64, 64);
	///   using JlImage field = row.RealToVectorField(col, "vector_field_relative");
	///   JlHomMat2D m = field.VectorFieldToHomMat2d();
	///   </code>
	///   <para><b>资源与坑</b>返回的是 JlData 系对象，不要写 m.Dispose()（编译不过）；示例里三个 JlImage 都是句柄对象须释放。RealToVectorField 的返回值是新句柄。</para>
	/// </remarks>
	public JlHomMat2D VectorFieldToHomMat2d()
	{
		IntPtr proc = JlNativeApi.PreCall(1551);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlHomMat2D.LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把 SerializeImage 产出的字节流还原为图像，原地写进本对象（原生算子 id 1570）。
	/// </summary>
	/// <param name="serializedItemHandle">序列化的字节数组（并非原生句柄，直接收 byte[]），通常来自 SerializeImage 或等价格式。</param>
	/// <remarks>
	///   <para><b>功能说明</b>实现先 Dispose() 掉本对象当前句柄，再经 JlSerializationBuffer 把字节钉住传入原生，输出用 Load 装载回 this —— 是原地改写，不返回新句柄。</para>
	///   <para><b>约束或前提</b>只能还原与 SerializeImage 同版本运行时产生的字节流；跨进程/跨语言自造的字节不保证可读[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要落盘存档用 ReadImage/WriteImage 文件族；仅在内存里复制一份独立副本、或要经队列/网络传递句柄数据时才用序列化对。想保留原图像时不要在本对象上调用——它先 Dispose 再装载，中途失败会留下空句柄。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage src = new JlImage();
	///   src.ReadImage("printer_chip/printer_chip_01");
	///   byte[] data = src.SerializeImage();
	///   using JlImage copy = new JlImage();
	///   copy.DeserializeImage(data);
	///   </code>
	///   <para><b>资源与坑</b>本对象旧内容被无条件释放；调用后所有仍指向 this 旧句柄语义的引用看到的都是新图。返回的 byte[] 是托管数组，无需 Dispose。</para>
	/// </remarks>
	public void DeserializeImage(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1570);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>
	///   把本图像完整打包成内存字节流（原生算子 id 1571），供 DeserializeImage 还原。
	/// </summary>
	/// <returns>托管 byte[]（经 JlSerializationBuffer.LoadBytes 取出），不是原生句柄，无需释放；内容是图像的深拷贝。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本图像经 Store 作为唯一输入，无输出句柄参数；字节流包含像素数据本身，序列化后修改/释放原图不影响这份副本。</para>
	///   <para><b>约束或前提</b>对已 Dispose 或空句柄调用会走原生报错[待实测异常形式]。</para>
	///   <para><b>与相邻算子的取舍</b>只要压缩后的可视图或跨机器存档用 WriteImage 文件族；要在自己的队列/缓存/网络帧里原样搬运句柄对象（含 float 精度、多通道语义，不经编码有损）才用序列化。WriteImage 会丢精度或通道语义，序列化不会。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   byte[] data = img.SerializeImage();
	///   using JlImage back = new JlImage();
	///   back.DeserializeImage(data);
	///   </code>
	///   <para><b>资源与坑</b>大图像序列化出的 byte[] 可能占大量托管堆；及时置空或让其出作用域。还原端 DeserializeImage 是原地改写且先释放目标旧句柄，别对还想保留的图像调用。</para>
	/// </remarks>
	public byte[] SerializeImage()
	{
		IntPtr proc = JlNativeApi.PreCall(1571);
		Store(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   把本图像按指定图形格式写盘（原生算子 id 1575，元组重载）：填充灰度与文件名走钉住的 JlTuple。
	/// </summary>
	/// <param name="format">图形格式字符串，决定编码方式与文件扩展语义。Default: "tiff"</param>
	/// <param name="fillColor">图像域（region）之外像素的填充灰度；彩色图像可按通道各给一个值的元组。Default: 0</param>
	/// <param name="fileName">输出文件名元组（字符串隐式转 JlTuple 合法）。相对路径的解析基准目录[待实测]。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与标量重载同一原生 id；区别在 fillColor/fileName 用 Store+调用后 UnpinTuple 钉固定元组直传，有额外钉固开销，换来的是多值能力。本图像是 Store 参数 1 的输入，无输出句柄。</para>
	///   <para><b>约束或前提</b>若图像做过 CropDomain 有非全幅 domain，域外像素按 fillColor 落进文件——导出取证图前先想清楚 domain 是否要保留。float/direction 等高类型图像转 byte 格式会有量化/截断[待实测各格式支持矩阵]。</para>
	///   <para><b>与相邻算子的取舍</b>单值场景用标量重载 WriteImage(string,int,string) 更省（StoreI/StoreS 直写、无钉固）；要把一张图写成多文件或给多通道分别指定背景色才用本重载。无损搬运句柄对象用 SerializeImage。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   img.WriteImage("tiff", new int[] { 255, 0, 0 }, "out/chip.tiff");
	///   </code>
	///   <para><b>资源与坑</b>void 返回、不产生新句柄；JlTuple 实参由实现内部 UnpinTuple，调用方无需处理。写失败（目录不存在/权限）经 PostCall 抛库异常[待实测异常类型]。</para>
	/// </remarks>
	public void WriteImage(string format, JlTuple fillColor, JlTuple fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1575);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, format);
		JlNativeApi.Store(proc, 1, fillColor);
		JlNativeApi.Store(proc, 2, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(fillColor);
		JlNativeApi.UnpinTuple(fileName);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   把本图像按指定图形格式写盘（原生算子 id 1575，标量重载）：单值填充灰度 + 单个文件名。
	/// </summary>
	/// <param name="format">图形格式字符串，决定编码方式。Default: "tiff"</param>
	/// <param name="fillColor">图像域之外像素的填充灰度，以 INTEGER（StoreI）写入原生参数 1；只给一个值，彩色图各通道同值。Default: 0</param>
	/// <param name="fileName">单个输出文件名，StoreS 直写，必传。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一原生 id、同一语义；本重载参数以 StoreI/StoreS 直写，无钉固/解钉开销，是日常导出的首选。本图像经 Store 作输入，无输出句柄。</para>
	///   <para><b>约束或前提</b>有裁剪域（CropDomain 之后）时域外按 fillColor 落盘；fillColor 为整数，float 图像导出到 byte 级格式时的量化行为[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>需要按通道分别给背景色、或一次写多个文件时改用 JlTuple 重载；不落盘、在内存复制句柄用 SerializeImage。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   img.WriteImage("bmp", 255, "out/chip.bmp");
	///   </code>
	///   <para><b>资源与坑</b>void 返回、不产生句柄；目标文件已存在时的覆盖行为[待实测]。相对路径解析基准[待实测]。</para>
	/// </remarks>
	public void WriteImage(string format, int fillColor, string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(1575);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, format);
		JlNativeApi.StoreI(proc, 1, fillColor);
		JlNativeApi.StoreS(proc, 2, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   按裸数据（无图形文件头）方式读入一帧图像并原地写进本对象（原生算子 id 1576）。
	/// </summary>
	/// <param name="headerSize">文件开头要跳过的字节数，用于剥掉相机/采集卡自带头。Default: 0</param>
	/// <param name="sourceWidth">源文件内每帧的列数（x 方向，column 轴）。Default: 512</param>
	/// <param name="sourceHeight">源文件内每帧的行数（y 方向，row 轴）。Default: 512</param>
	/// <param name="startRow">截取区域起始行（row=y 向下为正）。Default: 0</param>
	/// <param name="startColumn">截取区域起始列（column=x 向右为正）。Default: 0</param>
	/// <param name="destWidth">输出图像列数，与源区域不一致时相当于重采样。Default: 512</param>
	/// <param name="destHeight">输出图像行数。Default: 512</param>
	/// <param name="pixelType">像素类型，决定每像素字节数与数值解释，须与文件实际布局一致否则整幅错位。Default: "byte"</param>
	/// <param name="bitOrder">小于一个字节/像素时位在一个字节内的排列。Default: "MSBFirst"</param>
	/// <param name="byteOrder">多字节像素（如 short）时字节序。Default: "MSBFirst"</param>
	/// <param name="pad">每行数据的对齐单位（行长补齐）。Default: "byte"</param>
	/// <param name="index">多帧文件里读取哪一帧；参数表文案写作"帧数"，实为帧序号，计数起点[待实测]。Default: 1</param>
	/// <param name="fileName">输入文件路径。</param>
	/// <remarks>
	///   <para><b>功能说明</b>实现先 Dispose() 本对象旧句柄，全部 13 个参数以 StoreI/StoreS 按 C# 形参序 0..12 直写原生，输出经 Load 原地装载回 this，不返回新句柄。</para>
	///   <para><b>约束或前提</b>只适用于像素裸转储文件；png/tiff 等有格式头的图必须用 ReadImage，几何猜错时本函数不会报错只会读出花图。startRow/startColumn+尺寸越出 source 范围的边界行为[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>有格式头的图形文件用 ReadImage（自动解析几何与类型）；源是采集卡打包流才用本函数手工声明几何；pad/byteOrder 全对但只想裁剪时用 CropPart 后处理更直观。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage raw = new JlImage();
	///   raw.ReadSequence(0, 512, 512, 0, 0, 512, 512, "byte", "MSBFirst", "MSBFirst", "byte", 1, "frame0001.dat");
	///   </code>
	///   <para><b>资源与坑</b>原地改写：调用后 this 旧内容已释放，失败会留下空句柄。同一对象反复读不同帧是覆盖不是追加。</para>
	/// </remarks>
	public void ReadSequence(int headerSize, int sourceWidth, int sourceHeight, int startRow, int startColumn, int destWidth, int destHeight, string pixelType, string bitOrder, string byteOrder, string pad, int index, string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1576);
		JlNativeApi.StoreI(proc, 0, headerSize);
		JlNativeApi.StoreI(proc, 1, sourceWidth);
		JlNativeApi.StoreI(proc, 2, sourceHeight);
		JlNativeApi.StoreI(proc, 3, startRow);
		JlNativeApi.StoreI(proc, 4, startColumn);
		JlNativeApi.StoreI(proc, 5, destWidth);
		JlNativeApi.StoreI(proc, 6, destHeight);
		JlNativeApi.StoreS(proc, 7, pixelType);
		JlNativeApi.StoreS(proc, 8, bitOrder);
		JlNativeApi.StoreS(proc, 9, byteOrder);
		JlNativeApi.StoreS(proc, 10, pad);
		JlNativeApi.StoreI(proc, 11, index);
		JlNativeApi.StoreS(proc, 12, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   按文件格式自动解析读入图像并原地写进本对象（原生算子 id 1578，JlTuple 重载）。
	/// </summary>
	/// <param name="fileName">图像文件名元组；字符串可隐式转入。Default: "printer_chip/printer_chip_01"</param>
	/// <remarks>
	///   <para><b>功能说明</b>先 Dispose() 本对象旧句柄再 Load 原地装载，几何与像素类型由文件自身决定。与标量重载同一 id，差别是元组参数经 Store 钉固、调用后 UnpinTuple。</para>
	///   <para><b>约束或前提</b>读的是图形格式文件（扩展名按库内注册格式表识别，裸数据用 ReadSequence）。元组里给多个名字时本对象只有一个句柄位，实际取用哪几个[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>单个文件名用标量重载更省（StoreS 直写无钉固）；想在构造时一步到位可直接 new JlImage(fileName)，语义等价于建空句柄后 ReadImage。彩色图进单通道算法前先 Rgb1ToGray，别指望读入时自动降灰。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage(new string[] { "printer_chip/printer_chip_01" });
	///   using JlRegion dom = img.Threshold(0.0, 128.0);
	///   </code>
	///   <para><b>资源与坑</b>原地改写、无新句柄返回，但 Threshold 的输出是 JlRegion 新句柄须释放；示例中 dom 已 using 处理。读不存在的文件由 PostCall 抛库异常[待实测类型]。</para>
	/// </remarks>
	public void ReadImage(JlTuple fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1578);
		JlNativeApi.Store(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(fileName);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   按文件格式自动解析读入单个图像文件，原地写进本对象（原生算子 id 1578，标量重载）。
	/// </summary>
	/// <param name="fileName">图像文件名；相对名按库的示例/搜索目录解析。Default: "printer_chip/printer_chip_01"</param>
	/// <remarks>
	///   <para><b>功能说明</b>先 Dispose() 本对象旧句柄，fileName 以 StoreS 直写（无钉固开销），输出 Load 原地装载回 this。几何、通道数、像素类型全由文件决定。</para>
	///   <para><b>约束或前提</b>仅图形格式文件；裸转储数据用 ReadSequence 手工声明几何。扩展名可省略（按内容嗅探还是必须带[待实测]）。彩色图读入后是 3 通道，喂给单通道算子前要 Rgb1ToGray。</para>
	///   <para><b>与相邻算子的取舍</b>一次读多帧同名序列或带偏移量的原始帧不在本函数能力内；new JlImage(fileName) 构造器一步完成同样的事，之后需要复用空对象再改写时才用本函数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   JlImage rot = img.RotateImage(90.0, "constant");
	///   rot.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>原地改写、不返回句柄，但后续 RotateImage 一族返回新 JlImage 句柄须释放；读失败经 PostCall 抛库异常[待实测类型]。</para>
	/// </remarks>
	public void ReadImage(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1578);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   沿一条 XLD 轮廓的逐点坐标，从本图像采样灰度，返回灰度元组（原生算子 id 1587）。
	/// </summary>
	/// <param name="contour">提供采样点坐标的输入轮廓（点数决定输出元组长度）。</param>
	/// <param name="interpolation">采样插值方式：nearest_neighbor 取四舍五入像素，其余方式在像素间插值。Default: "nearest_neighbor"</param>
	/// <returns>与轮廓点一一对应的灰度值 JlTuple（新对象，用毕 Dispose）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本图像经 Store 作 iconc 1、contour 作 iconc 2，interpolation 以 STRING 写控制槽 0（原生侧控制参数序与 C# 形参序一致）；输出 InitOCT(0)+JlTuple.LoadNew 新建元组。</para>
	///   <para><b>约束或前提</b>轮廓坐标以图像像素坐标系计（row=y 向下、column=x 向右）；只传一条 JlXLDCont——多条轮廓需逐个调用。落点在本图像 domain 外或越界的灰度值行为[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>区域聚合的均值/标准差用 Intensity、分布用 GrayHisto/GrayHistoRange 系；要沿轮廓逐点的梯度/曲率，本库没有现成算子（如 derivatives_into_intensity_contour 不存在），需拿本函数的灰度剖面自行差分。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlXLDCont c = new JlXLDCont(new double[] { 10, 11, 12 }, new double[] { 20, 21, 22 });
	///   JlTuple gray = img.GetGrayvalContourXld(c, "nearest_neighbor");
	///   gray.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组须 Dispose（JlTuple 实现 IDisposable）；GC.KeepAlive 表示调用结束前 contour 与本图像都不能释放，返回后轮廓即可释放。</para>
	/// </remarks>
	public JlTuple GetGrayvalContourXld(JlXLDCont contour, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(1587);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, contour);
		JlNativeApi.StoreS(proc, 0, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contour);
		return tuple;
	}





	/// <summary>
	///   生成一张二阶多项式弯曲灰度曲面图像，原地写进本对象（原生算子 id 1664）。
	/// </summary>
	/// <param name="type">输出像素类型；byte 时超出 0..255 的曲面值会被截断/回绕[待实测]。Default: "byte"</param>
	/// <param name="alpha">沿行方向（row=y，向下为正）的二阶系数。Default: 1.0</param>
	/// <param name="beta">沿列方向（column=x，向右为正）的二阶系数。Default: 1.0</param>
	/// <param name="gamma">行列混合二阶系数。Default: 1.0</param>
	/// <param name="delta">行方向一阶系数。Default: 1.0</param>
	/// <param name="epsilon">列方向一阶系数。Default: 1.0</param>
	/// <param name="zeta">零阶常数项。Default: 1.0</param>
	/// <param name="row">曲面参考点行坐标（多项式以该点为原点展开）。Default: 256.0</param>
	/// <param name="column">曲面参考点列坐标。Default: 256.0</param>
	/// <param name="width">输出图像列数。Default: 512</param>
	/// <param name="height">输出图像行数。Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b>先在参考点 (row,column) 处取值为 zeta，向外按 z = α·dr² + β·dc² + γ·dr·dc + δ·dr + ε·dc 叠加（dr/dc 为到参考点的行/列偏移）[符号约定待实测]。实现先 Dispose() 本对象再 Load 原地装载，全部参数按 C# 形参序 0..10 直写原生。</para>
	///   <para><b>约束或前提</b>纯合成图，不需要输入图像；type 与系数幅值要匹配，byte 图用大系数几乎必然饱和成片。</para>
	///   <para><b>与相邻算子的取舍</b>线性光照梯度用一次多项式版 GenImageSurfaceFirstOrder，别为斜面硬上二阶；做背景建模/照度均衡时先用本函数拟合出曲面再走减法族；只要常数底图时 gen_image_const 级别的需求犯不上算多项式。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage surface = new JlImage();
	///   surface.GenImageSurfaceSecondOrder("float", 0.0001, 0.0001, 0.0, 0.0, 0.0, 128.0, 32, 32, 64, 64);
	///   </code>
	///   <para><b>资源与坑</b>原地改写、无新句柄；系数全部按 DOUBLE 装载，width/height 按 INTEGER。</para>
	/// </remarks>
	public void GenImageSurfaceSecondOrder(string type, double alpha, double beta, double gamma, double delta, double epsilon, double zeta, double row, double column, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1664);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreD(proc, 2, beta);
		JlNativeApi.StoreD(proc, 3, gamma);
		JlNativeApi.StoreD(proc, 4, delta);
		JlNativeApi.StoreD(proc, 5, epsilon);
		JlNativeApi.StoreD(proc, 6, zeta);
		JlNativeApi.StoreD(proc, 7, row);
		JlNativeApi.StoreD(proc, 8, column);
		JlNativeApi.StoreI(proc, 9, width);
		JlNativeApi.StoreI(proc, 10, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   生成一张一阶（平面）灰度曲面图像，原地写进本对象（原生算子 id 1665）。
	/// </summary>
	/// <param name="type">输出像素类型；byte 时平面值超出 0..255 的部分截断[待实测]。Default: "byte"</param>
	/// <param name="alpha">行方向（row=y 向下）一阶斜率，单位是灰度/像素。Default: 1.0</param>
	/// <param name="beta">列方向（column=x 向右）一阶斜率，单位是灰度/像素。Default: 1.0</param>
	/// <param name="gamma">零阶常数项：参考点处的灰度。Default: 1.0</param>
	/// <param name="row">平面参考点行坐标。Default: 256.0</param>
	/// <param name="column">平面参考点列坐标。Default: 256.0</param>
	/// <param name="width">输出图像列数。Default: 512</param>
	/// <param name="height">输出图像行数。Default: 512</param>
	/// <remarks>
	///   <para><b>功能说明</b>z = γ + α·dr + β·dc，dr/dc 为该像素到参考点 (row,column) 的行/列偏移，参考点处取 γ（dr/dc 以什么方向为正[待实测]）。先 Dispose() 本对象再 Load 原地写回；控制参数按形参序 0..7 直写。</para>
	///   <para><b>约束或前提</b>斜率乘上离参考点的距离——512 宽图上 1.0 的斜率意味着跨 512 级灰度，byte 图必然大面积饱和；想模拟轻微光照不均，α/β 通常要在 0.0x 量级。</para>
	///   <para><b>与相邻算子的取舍</b>照度带弯曲（渐晕/弧形光）才升到 GenImageSurfaceSecondOrder；均匀底图用 GenImageConst；要的是"估出"梯度而非"造出"梯度时先做拟合再减法族，别拿默认系数当真值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage plane = new JlImage();
	///   plane.GenImageSurfaceFirstOrder("byte", 0.0, 0.5, 60.0, 0.0, 0.0, 64, 64);
	///   </code>
	///   <para><b>资源与坑</b>原地改写、无新句柄；参考点放在左上角 (0,0) 时灰度沿列单调爬升，便于验证符号约定。</para>
	/// </remarks>
	public void GenImageSurfaceFirstOrder(string type, double alpha, double beta, double gamma, double row, double column, int width, int height)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(1665);
		JlNativeApi.StoreS(proc, 0, type);
		JlNativeApi.StoreD(proc, 1, alpha);
		JlNativeApi.StoreD(proc, 2, beta);
		JlNativeApi.StoreD(proc, 3, gamma);
		JlNativeApi.StoreD(proc, 4, row);
		JlNativeApi.StoreD(proc, 5, column);
		JlNativeApi.StoreI(proc, 6, width);
		JlNativeApi.StoreI(proc, 7, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 1, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>区域内最小/最大灰度，percent 可截掉离群像素，原生算子 id 1670，三个输出均为元组。</summary>
	/// <param name="regions">要计算特征的区域，逐区域各出一个值。</param>
	/// <param name="percent">相对绝对最大值/最小值截去的百分比。Default: 0</param>
	/// <param name="min">"最小"灰度，每区域一个元素。</param>
	/// <param name="max">"最大"灰度。</param>
	/// <param name="range">max 与 min 之差。</param>
	/// <remarks>
	///   <para><b>功能说明</b>图像是 iconc 2 输入、区域 iconc 1，<paramref name="percent"/> 占控制槽 0；
	///   三个 iconc 输出（0/1/2）都是 <see cref="JlTupleType"/> 为 DOUBLE 的 <see cref="JlTuple"/>——结果是元组不是图像。
	///   <paramref name="percent"/>=0 时是严格的逐区域 min/max；取正数则变成"伪极值"：<c>max</c> 返回比绝对最大值低
	///   <paramref name="percent"/>% 的像素数量级处、<c>min</c> 对称上抬——用来防少数灰尘亮斑或坏点把极值绑架掉。</para>
	///   <para><b>与相邻算子的取舍</b>要均值与标准差 → <see cref="Intensity(JlRegion,out JlTuple)"/>；要完整分布 →
	///   <see cref="GrayHisto(JlRegion,out JlTuple)"/>；要逐像素改写灰度 → 形态学/滤波族。本算子的 <c>range</c>
	///   常见用法是配合 <see cref="ScaleImage(double,double)"/> 做逐区域对比度拉伸——直接拿严格 min/max 归一有噪场景，
	///   极值被单点噪声决定，拉伸后整体反差反而塌掉，这正是 <paramref name="percent"/> 存在的理由。</para>
	///   <para><b>参数取向</b>元组版 <paramref name="percent"/> 走 <c>Store</c>+<c>UnpinTuple</c>，且三个输出按区域逐个给值，
	///   元素个数与 <c>regions.CountObj()</c> 对应；单区域简写见 <see cref="MinMaxGray(JlRegion,double,out double,out double,out double)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion thr = img.Threshold(50.0, 255.0);
	///   using JlRegion parts = thr.Connection();
	///   img.MinMaxGray(parts, new JlTuple(1.0), out JlTuple mins, out JlTuple maxs, out JlTuple ranges);
	///   double firstMax = maxs[0].D;   // 截掉顶部 1% 像素后的伪最大值
	///   mins.Dispose(); maxs.Dispose(); ranges.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>三个输出元组与输入区域都要释放；空区域对应的 min/max 取值 [待实测]；
	///   <paramref name="percent"/> 超过 100 或为负 [待实测]。</para>
	/// </remarks>
	public void MinMaxGray(JlRegion regions, JlTuple percent, out JlTuple min, out JlTuple max, out JlTuple range)
	{
		IntPtr proc = JlNativeApi.PreCall(1670);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.Store(proc, 0, percent);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(percent);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out min);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out max);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out range);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
	}

	/// <summary>区域内最小/最大灰度（三个输出都是单值 double）。</summary>
	/// <param name="regions">要计算特征的区域。</param>
	/// <param name="percent">相对绝对最大值/最小值截去的百分比。Default: 0</param>
	/// <param name="min">"最小"灰度。</param>
	/// <param name="max">"最大"灰度。</param>
	/// <param name="range">max 与 min 之差。</param>
	/// <remarks>
	///   <para>percent 截尾语义、与 <c>Intensity</c>/<c>ScaleImage</c> 的配合见
	///   <see cref="MinMaxGray(JlRegion,JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>：同一原生 id 1670，
	///   本版本 <c>StoreD</c> 直写 percent，三个 iconc 输出用 <c>LoadD</c> 按标量读取——只对单区域有意义，
	///   多区域时取第几个值 [待实测]，逐区域结果请用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(100.0, 100.0, 300.0, 300.0);
	///   img.MinMaxGray(roi, 1.0, out double min, out double max, out double range);
	///   </code>
	/// </remarks>
	public void MinMaxGray(JlRegion regions, double percent, out double min, out double max, out double range)
	{
		IntPtr proc = JlNativeApi.PreCall(1670);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.StoreD(proc, 0, percent);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out min);
		err = JlNativeApi.LoadD(proc, 1, err, out max);
		err = JlNativeApi.LoadD(proc, 2, err, out range);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
	}

	/// <summary>
	///   逐区域统计本图像灰度均值与标准差，两个结果都是元组（原生算子 id 1671）。
	/// </summary>
	/// <param name="regions">要统计的区域；多区域时逐区域各出一个值。</param>
	/// <param name="deviation">区域内灰度标准差（与均值同单位的 DOUBLE 元组）。</param>
	/// <returns>区域内灰度均值（DOUBLE 元组，新对象，用毕 Dispose）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>图像是 iconc 2 输入、区域 iconc 1（没有控制参数）；输出 0=均值、1=标准差，均按区域逐个给值，元素数与 regions.CountObj() 对应。统计对象是 区域∩图像domain 的像素。</para>
	///   <para><b>约束或前提</b>byte 图结果落在 0..255；多通道输入是否逐通道展开[待实测]。空区域对应的均值取值[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要极值/伪极值 → MinMaxGray（percent 可截离群点）；要完整分布 → GrayHisto 系；均值±标准差对双峰分布毫无信息量，判断"这块区域干不干净"别看 deviation 一个数，回到直方图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion thr = img.Threshold(50.0, 255.0);
	///   using JlRegion parts = thr.Connection();
	///   JlTuple mean = img.Intensity(parts, out JlTuple dev);
	///   double firstMean = mean[0].D;
	///   mean.Dispose(); dev.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值和 out 元组都是新对象，成对释放；只要单区域标量结果见 double 重载。</para>
	/// </remarks>
	public JlTuple Intensity(JlRegion regions, out JlTuple deviation)
	{
		IntPtr proc = JlNativeApi.PreCall(1671);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out deviation);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>
	///   统计区域内本图像灰度均值与标准差（标量版，两个输出各是一个 double）。
	/// </summary>
	/// <param name="regions">要统计的区域。</param>
	/// <param name="deviation">区域内灰度标准差。</param>
	/// <returns>区域内灰度均值。</returns>
	/// <remarks>
	///   <para>与元组版同一原生 id 1671；本版本把两个 DOUBLE 输出用 LoadD 只读<b>第一个值</b>——传入多区域时其余区域的结果被静默丢弃，逐区域统计一律用 <see cref="Intensity(JlRegion,out JlTuple)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion roi = new JlRegion(10.0, 10.0, 50.0, 50.0);
	///   double mean = img.Intensity(roi, out double deviation);
	///   </code>
	///   <para><b>资源与坑</b>标量输出无需释放；GC.KeepAlive 表示调用期间图像与区域都不能 Dispose。</para>
	/// </remarks>
	public double Intensity(JlRegion regions, out double deviation)
	{
		IntPtr proc = JlNativeApi.PreCall(1671);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out deviation);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return doubleValue;
	}

	/// <summary>指定灰度区间与 bin 数的灰度直方图（单通道图像），原生算子 id 1672，区间以元组传入。</summary>
	/// <param name="region">要计算直方图的区域。</param>
	/// <param name="min">直方图下界。Default: 0</param>
	/// <param name="max">直方图上界。Default: 255</param>
	/// <param name="numBins">bin 个数。Default: 256</param>
	/// <param name="binSize">实际 bin 宽。</param>
	/// <returns>各 bin 的像素计数（INTEGER 元组），长度等于 numBins。</returns>
	/// <remarks>
	///   <para><b>功能说明与前提</b>英文签名文档明确适用对象是<b>单通道图像</b>；多通道输入的检查与行为本层看不到
	///   [待实测]。区域 iconc 1、图像 iconc 2，控制槽 0/1/2 是 <c>min</c>/<c>max</c>/<paramref name="numBins"/>；
	///   直方图从 iconc 输出 0 读出，实际 bin 宽由原生算好经 <paramref name="binSize"/>（iconc 1）返回，
	///   第 i 个 bin 覆盖 [min + i·binSize, min + (i+1)·binSize) 一类的区间，首尾闭开细节 [待实测]。
	///   灰度落在 <c>min..max</c> 之外的像素不进入任何 bin：byte 图传 0..255 才不漏，
	///   uint2/float 图先量实际范围（<see cref="MinMaxGray(JlRegion,JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>）再定区间。</para>
	///   <para><b>与相邻算子的取舍</b>不想手动选区间、要按全灰度域分组计数 → <see cref="GrayHistoAbs(JlRegion,JlTuple)"/>；
	///   要相对频率以便比较不同面积的区域 → <see cref="GrayHisto(JlRegion,out JlTuple)"/>；
	///   <see cref="Intensity(JlRegion,out JlTuple)"/> 只给均值/标准差，双峰分布会被完全抹掉，选阈值必须靠直方图。</para>
	///   <para><b>参数取向</b>本重载 <paramref name="min"/>/<paramref name="max"/> 是 <c>Store</c>+<c>UnpinTuple</c>，
	///   多元素语义 [待实测]；<paramref name="numBins"/> 两个重载都是 <c>StoreI</c> <c>int</c>，单位是 bin 个数。
	///   min 大于 max、numBins 为 0 或负 [待实测]。返回值是<b>元组</b>而不是图像。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(50.0, 50.0, 200.0, 200.0);
	///   JlTuple histo = img.GrayHistoRange(roi, new JlTuple(0.0), new JlTuple(255.0), 256, out double binSize);
	///   int bin0 = histo[0].I;       // 直方图只统计区域与定义域交集内的像素
	///   histo.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组需 <c>Dispose</c>；uint2 图上 binSize 通常大于 1，按"灰度值=bin 下标"画图会错位，
	///   横轴要乘回 <paramref name="binSize"/>。</para>
	/// </remarks>
	public JlTuple GrayHistoRange(JlRegion region, JlTuple min, JlTuple max, int numBins, out double binSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1672);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.Store(proc, 0, min);
		JlNativeApi.Store(proc, 1, max);
		JlNativeApi.StoreI(proc, 2, numBins);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(min);
		JlNativeApi.UnpinTuple(max);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlNativeApi.LoadD(proc, 1, err, out binSize);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return tuple;
	}

	/// <summary>指定区间的灰度直方图（区间以 double 传入，返回值退化为主 bin 计数）。</summary>
	/// <param name="region">要计算直方图的区域。</param>
	/// <param name="min">直方图下界。Default: 0</param>
	/// <param name="max">直方图上界。Default: 255</param>
	/// <param name="numBins">bin 个数。Default: 256</param>
	/// <param name="binSize">实际 bin 宽。</param>
	/// <returns>iconc 输出 0 按标量读出的单个整数。</returns>
	/// <remarks>
	///   <para>区间/分箱语义与单通道前提见 <see cref="GrayHistoRange(JlRegion,JlTuple,JlTuple,int,out double)"/>：
	///   同一原生 id 1672，本版本 <c>StoreD</c> 直写 min/max。<b>要注意的坑</b>：直方图本来是逐 bin 的向量，
	///   这里却用 <c>LoadI</c> 按单个 <c>int</c> 读——多 bin 时只拿得到一个值（第几个 [待实测]），完整直方图请一律用元组版。
	///   此重载实际只在 numBins=1（把区间当"灰度计数窗"用）时有意义 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(50.0, 50.0, 200.0, 200.0);
	///   int count = img.GrayHistoRange(roi, 128.0, 255.0, 1, out double binSize);
	///   </code>
	/// </remarks>
	public int GrayHistoRange(JlRegion region, double min, double max, int numBins, out double binSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1672);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.StoreD(proc, 0, min);
		JlNativeApi.StoreD(proc, 1, max);
		JlNativeApi.StoreI(proc, 2, numBins);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlNativeApi.LoadD(proc, 1, err, out binSize);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return intValue;
	}

	/// <summary>
	///   以本图像为第一维、imageRow 为第二维，在区域内统计二维灰度联合直方图，输出是一张直方图图像（原生算子 id 1673）。
	/// </summary>
	/// <param name="regions">参与统计的区域（交集外像素不计数）。</param>
	/// <param name="imageRow">提供第二维取值的图像，须与本图像同几何。</param>
	/// <returns>新的直方图图像句柄（每个"格子"= 一对灰度组合的计数），用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本图像 iconc 2、区域 iconc 1、imageRow iconc 3，全部经 Store 传句柄，无控制参数；输出 InitOCT(1)+LoadNew 新图像句柄。这是本文件里唯一直接产出<b>图像</b>而非元组的直方图算子。</para>
	///   <para><b>约束或前提</b>两幅输入灰度必须成对（同 width/height/通道对齐方式），否则联合计数无意义；输出图像的行列各对应哪一维灰度、bin 数如何定[待实测]。多通道输入行为[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>一维灰度分布用 GrayHisto/GrayHistoRange/GrayHistoAbs（返回元组、可直接取数）；要看两通道耦合（如 R 与 G 联合分布做分类取证）才用本算子，代价是结果得再按像素读。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage a = new JlImage("byte", 64, 64);
	///   using JlImage b = new JlImage("byte", 64, 64);
	///   using JlRegion roi = new JlRegion(0.0, 0.0, 63.0, 63.0);
	///   JlImage h2 = a.Histo2dim(roi, b);
	///   h2.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新图像句柄必须释放；调用结束前三方（this、regions、imageRow）都被 KeepAlive 钉住，返回后方可释放。</para>
	/// </remarks>
	public JlImage Histo2dim(JlRegion regions, JlImage imageRow)
	{
		IntPtr proc = JlNativeApi.PreCall(1673);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.Store(proc, 3, imageRow);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		GC.KeepAlive(imageRow);
		return obj;
	}

	/// <summary>按灰度步长分组的绝对计数直方图，原生算子 id 1674，步长以元组传入。</summary>
	/// <param name="region">要计算直方图的区域。</param>
	/// <param name="quantization">灰度分组步长。Default: 1.0</param>
	/// <returns>各灰度组的绝对像素计数（INTEGER 元组）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与 <see cref="GrayHistoRange(JlRegion,JlTuple,JlTuple,int,out double)"/> 的分工：Range 版要你指定
	///   区间和 bin 数（<c>binSize</c> 是算出来的），本算子反过来——只给<b>步长</b> <paramref name="quantization"/>（单位是灰度值，
	///   不是像素、不是比例），统计范围由图像自身灰度域决定 [待实测]。默认 1.0 即逐灰度值计数。</para>
	///   <para><b>坑</b>计数是<b>绝对值</b>，随区域面积线性增长：两个大小不同的区域直接对比原始计数毫无意义，
	///   归一化用 <see cref="GrayHisto(JlRegion,out JlTuple)"/> 的 relativeHisto，或自行除以区域面积。
	///   直方图起点随区域实际出现的灰度而定的细节（空灰度段是否补零 bin）[待实测]。</para>
	///   <para><b>参数取向</b>元组版 <paramref name="quantization"/> 走 <c>Store</c>+<c>UnpinTuple</c>，
	///   与标量版 <see cref="GrayHistoAbs(JlRegion,double)"/> <b>返回类型相同</b>（都是完整 INTEGER 元组），
	///   差异只在传参方式；步长 ≤0 行为 [待实测]。单通道前提同 Range 版 [待实测：多通道]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(50.0, 50.0, 200.0, 200.0);
	///   JlTuple histo = img.GrayHistoAbs(roi, new JlTuple(2.0));   // 每 2 个灰度一档
	///   int n = histo.Length;
	///   histo.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组需 <c>Dispose</c>；bin 下标不再等于灰度值，换算要乘 <paramref name="quantization"/>。</para>
	/// </remarks>
	public JlTuple GrayHistoAbs(JlRegion region, JlTuple quantization)
	{
		IntPtr proc = JlNativeApi.PreCall(1674);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.Store(proc, 0, quantization);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(quantization);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return tuple;
	}

	/// <summary>按灰度步长分组的绝对计数直方图（步长以单个 double 传入）。</summary>
	/// <param name="region">要计算直方图的区域。</param>
	/// <param name="quantization">灰度分组步长。Default: 1.0</param>
	/// <returns>各灰度组的绝对像素计数（INTEGER 元组）。</returns>
	/// <remarks>
	///   <para>步长语义、绝对计数随面积增长的坑见 <see cref="GrayHistoAbs(JlRegion,JlTuple)"/>：同一原生 id 1674，
	///   本版本 <c>StoreD</c> 直写步长，无固定/解固定，返回值同样是完整直方图元组，是常规写法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(50.0, 50.0, 200.0, 200.0);
	///   JlTuple histo = img.GrayHistoAbs(roi, 1.0);
	///   histo.Dispose();
	///   </code>
	/// </remarks>
	public JlTuple GrayHistoAbs(JlRegion region, double quantization)
	{
		IntPtr proc = JlNativeApi.PreCall(1674);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.StoreD(proc, 0, quantization);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return tuple;
	}

	/// <summary>一次给出绝对与相对两张灰度直方图，原生算子 id 1675，无分箱参数。</summary>
	/// <param name="region">要计算直方图的区域。</param>
	/// <param name="relativeHisto">按区域面积归一化的相对频率。</param>
	/// <returns>各灰度值的绝对像素计数（INTEGER 元组）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本族唯一没有分箱/区间参数的直方图：一个调用同时从 iconc 输出 0、1 拿到绝对计数
	///   （返回值）与<b>按区域面积归一化</b>的相对频率（<paramref name="relativeHisto"/>，DOUBLE 元组）。
	///   相对版解决的正是绝对计数的坑——不同大小的区域直方图不可直接比：绝对计数随面积线性膨胀，
	///   相对频率才可比（逐 bin 求和约等于 1，归一化基数是区域面积还是有效像素数 [待实测]）。</para>
	///   <para><b>与相邻算子的取舍</b>要自定义区间与 bin 数 → <see cref="GrayHistoRange(JlRegion,JlTuple,JlTuple,int,out double)"/>；
	///   只要绝对计数、按灰度步长分组 → <see cref="GrayHistoAbs(JlRegion,double)"/>。本算子无分箱旋钮，bin 的灰度跨度
	///   由原生决定（byte 图大概率逐灰度 [待实测]），要精确控制分箱别用它。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion a = new JlRegion(10.0, 10.0, 60.0, 60.0);
	///   using JlRegion b = new JlRegion(100.0, 100.0, 200.0, 200.0);
	///   JlTuple absA = img.GrayHisto(a, out JlTuple relA);
	///   JlTuple absB = img.GrayHisto(b, out JlTuple relB);   // 比较 relA 与 relB 才不受两块面积差异干扰
	///   </code>
	///   <para><b>资源与坑</b>注意 this 才是图像、区域是第一个实参；四个输出元组（absA/relA/absB/relB 这类）都要
	///   <c>Dispose</c>。多通道输入 [待实测]。</para>
	/// </remarks>
	public JlTuple GrayHisto(JlRegion region, out JlTuple relativeHisto)
	{
		IntPtr proc = JlNativeApi.PreCall(1675);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out relativeHisto);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return tuple;
	}

	/// <summary>逐区域计算灰度分布的信息熵与各向异性，原生算子 id 1676，两个输出均为元组。</summary>
	/// <param name="regions">要计算特征的区域，逐区域各出一个值。</param>
	/// <param name="anisotropy">灰度分布对称性（各向异性）度量。</param>
	/// <returns>灰度信息熵，每区域一个元素。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>图像 iconc 2、区域 iconc 1，无控制参数；从 iconc 输出 0/1 读两个 DOUBLE 元组：
	///   熵是区域内灰度直方图的信息量——平坦区接近 0，灰度分散的纹理区显著更高；<paramref name="anisotropy"/>
	///   按英文文档是"灰度分布对称性的度量"，其确切公式与取值方向 [待实测]。输出是<b>元组</b>不是图像。</para>
	///   <para><b>与相邻算子的取舍</b>要逐区域一个标量做纹理分类/特征向量 → 本算子；要在图上按空间位置分纹理区/非纹理区 →
	///   <see cref="EntropyImage(int,int)"/>（它返回局部窗熵图像，窗尺寸要求见其文档）。灰度区分不够时别指望熵：
	///   两块均值相同、方差不同的区域熵值差距可能远小于直觉预期。</para>
	///   <para><b>统计坑</b>熵由直方图估计，bin 数与像素数同量级时估计偏置明显：几百像素的小区域，其熵上限被
	///   log2(像素数) 卡住，且不同面积的区域间直接比熵值不公平 [待实测：分箱/归一方式]。这是用熵做区域筛选时
	///   最主要的误判来源。</para>
	///   <para><b>参数取向</b>元组版逐区域出值，元素与 <c>regions.CountObj()</c> 对应；单区域标量版见
	///   <see cref="EntropyGray(JlRegion,out double)"/>。空区域、多通道输入行为 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion thr = img.Threshold(30.0, 255.0);
	///   using JlRegion parts = thr.Connection();
	///   JlTuple ent = img.EntropyGray(parts, out JlTuple aniso);
	///   double first = ent[0].D;
	///   ent.Dispose(); aniso.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两个输出元组与输入区域都要释放；熵对预处理极敏感——滤波直方图形态的算子
	///   （中值、rank）都会系统性压低它，比较不同批次图像时预处理链必须一致。</para>
	/// </remarks>
	public JlTuple EntropyGray(JlRegion regions, out JlTuple anisotropy)
	{
		IntPtr proc = JlNativeApi.PreCall(1676);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out anisotropy);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>灰度熵与各向异性（两个输出都是单值 double）。</summary>
	/// <param name="regions">要计算特征的区域。</param>
	/// <param name="anisotropy">灰度分布对称性度量。</param>
	/// <returns>灰度信息熵。</returns>
	/// <remarks>
	///   <para>熵的直方图估计偏置、与 <c>EntropyImage</c> 的分工见
	///   <see cref="EntropyGray(JlRegion,out JlTuple)"/>：同一原生 id 1676，本版本把两个 iconc 输出用 <c>LoadD</c>
	///   按标量读出——只对单区域（或只要第一个值 [待实测]）有意义，多区域一律用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion roi = new JlRegion(100.0, 100.0, 200.0, 200.0);
	///   double ent = img.EntropyGray(roi, out double aniso);
	///   </code>
	/// </remarks>
	public double EntropyGray(JlRegion regions, out double anisotropy)
	{
		IntPtr proc = JlNativeApi.PreCall(1676);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out anisotropy);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return doubleValue;
	}





	/// <summary>
	///   逐区域算灰度加权矩并拟合近似平面，五个结果都是 DOUBLE 元组（原生算子 id 1680）。
	/// </summary>
	/// <param name="regions">要计算的区域，逐区域各出一值。</param>
	/// <param name="MRow">行方向混合矩（灰度加权）。</param>
	/// <param name="MCol">列方向混合矩（灰度加权）。</param>
	/// <param name="alpha">拟合平面沿行方向的斜率分量。</param>
	/// <param name="beta">拟合平面沿列方向的斜率分量。</param>
	/// <param name="mean">拟合平面常数项，即区域灰度均值。</param>
	/// <remarks>
	///   <para><b>功能说明</b>图像 iconc 2、区域 iconc 1，无控制参数；输出 0..4 依次为 MRow/MCol/alpha/beta/mean，全按 DOUBLE 装载。等价于在区域上拟合 z = mean + α·dr + β·dc（参考点约定[待实测]），拿 α/β 可判断该区域灰度是否带方向性倾斜。</para>
	///   <para><b>约束或前提</b>矩是<b>灰度加权</b>的，与 JlRegion 的纯几何矩（MomentsRegion 系）不同源，数值不可互换比较；多通道输入行为[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>只要均值/标准差用 Intensity（deviation 对倾斜不敏感，平面倾斜严重时均值照样"正常"）；要"平面拟合得好不好"这单个残差值用 PlaneDeviation（本函数给的是拟合参数本身）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion roi = new JlRegion(10.0, 10.0, 50.0, 50.0);
	///   img.MomentsGrayPlane(roi, out JlTuple mRow, out JlTuple mCol, out JlTuple alpha, out JlTuple beta, out JlTuple mean);
	///   double tiltCol = beta[0].D;
	///   mRow.Dispose(); mCol.Dispose(); alpha.Dispose(); beta.Dispose(); mean.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>五个 out 元组全部要 Dispose（本库 JlTuple 实现了 IDisposable）；只要第一个区域的标量结果见 double 重载。</para>
	/// </remarks>
	public void MomentsGrayPlane(JlRegion regions, out JlTuple MRow, out JlTuple MCol, out JlTuple alpha, out JlTuple beta, out JlTuple mean)
	{
		IntPtr proc = JlNativeApi.PreCall(1680);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out MRow);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out MCol);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out alpha);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out beta);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out mean);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
	}

	/// <summary>
	///   区域灰度矩与平面拟合参数（五个输出都是单值 double）。
	/// </summary>
	/// <param name="regions">要计算的区域。</param>
	/// <param name="MRow">行方向混合矩。</param>
	/// <param name="MCol">列方向混合矩。</param>
	/// <param name="alpha">拟合平面行方向斜率。</param>
	/// <param name="beta">拟合平面列方向斜率。</param>
	/// <param name="mean">区域灰度均值。</param>
	/// <remarks>
	///   <para>语义见 <see cref="MomentsGrayPlane(JlRegion,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>：同一原生 id 1680，本版本五个输出用 LoadD 各只读<b>第一个值</b>——传多区域时其余区域结果静默丢失，多区域一律用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion roi = new JlRegion(10.0, 10.0, 50.0, 50.0);
	///   img.MomentsGrayPlane(roi, out double mRow, out double mCol, out double alpha, out double beta, out double mean);
	///   </code>
	///   <para><b>资源与坑</b>标量输出无释放负担；调用期间区域与图像被 KeepAlive。</para>
	/// </remarks>
	public void MomentsGrayPlane(JlRegion regions, out double MRow, out double MCol, out double alpha, out double beta, out double mean)
	{
		IntPtr proc = JlNativeApi.PreCall(1680);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out MRow);
		err = JlNativeApi.LoadD(proc, 1, err, out MCol);
		err = JlNativeApi.LoadD(proc, 2, err, out alpha);
		err = JlNativeApi.LoadD(proc, 3, err, out beta);
		err = JlNativeApi.LoadD(proc, 4, err, out mean);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
	}

	/// <summary>
	///   逐区域给出"实际灰度相对其拟合近似平面"的残差（标准差类度量），返回 DOUBLE 元组（原生算子 id 1681）。
	/// </summary>
	/// <param name="regions">要计算的区域，逐区域各出一个值。</param>
	/// <returns>各区域灰度对平面的偏差（DOUBLE 元组，新对象，用毕 Dispose）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>图像 iconc 2、区域 iconc 1，无控制参数，单输出 iconc 0。先用最小二乘在本区域上拟合平面，再算区域内灰度到该平面的偏差——它扣掉了线性光照趋势，衡量的是"平面之外还剩多少起伏"。</para>
	///   <para><b>约束或前提</b>与 Intensity 的 deviation 不同：那个相对<b>常数均值</b>，本算子相对<b>倾斜平面</b>。同一区域两值只有在平面平坦时才接近；斜率越大差得越多。</para>
	///   <para><b>与相邻算子的取舍</b>要拟合参数本身（斜率/均值）→ MomentsGrayPlane；要看纹理随机性/信息量 → EntropyGray；判断"这块区域其实是渐变背景上的弱纹理还是近似纯平面"用本算子最直接。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion roi = new JlRegion(10.0, 10.0, 50.0, 50.0);
	///   JlTuple dev = img.PlaneDeviation(roi);
	///   double d0 = dev[0].D;
	///   dev.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组须 Dispose；单位与图像灰度同量纲（byte 图 0..255 内）；空区域/退化细长区域下拟合不稳[待实测]。</para>
	/// </remarks>
	public JlTuple PlaneDeviation(JlRegion regions)
	{
		IntPtr proc = JlNativeApi.PreCall(1681);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>
	///   按灰度加权二阶矩求每个区域的等效椭圆：长半轴（返回值）、短半轴与主轴方位角（原生算子 id 1682）。
	/// </summary>
	/// <param name="regions">要计算的区域，逐区域各出一组值。</param>
	/// <param name="rb">短半轴长度（像素尺度）。</param>
	/// <param name="phi">长轴与 x 轴（column 正向）的夹角，单位与旋转正方向[待实测]。</param>
	/// <returns>长半轴长度 ra（DOUBLE 元组，新对象，用毕 Dispose）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>图像 iconc 2、区域 iconc 1；输出 0/1/2 = ra/rb/phi，均 DOUBLE 元组。椭圆由<b>灰度加权</b>惯量矩导出：亮像素会把等效椭圆往自己一侧拉、拉胖，rb/phi 反映的是亮度分布的走向，不是区域几何轮廓的走向。</para>
	///   <para><b>约束或前提</b>退化细长或近似圆盘的区域里 phi 噪声极大（ra≈rb 时方位角数学上不定），别拿它做单像素抖动级的角度判定。</para>
	///   <para><b>与相邻算子的取舍</b>要纯形状椭圆（不看灰度）用 JlRegion 的几何椭圆轴算子；要质心+面积（灰度加权）用 AreaCenterGray；本算子专答"亮度分布朝哪边长"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion roi = new JlRegion(10.0, 10.0, 50.0, 50.0);
	///   JlTuple ra = img.EllipticAxisGray(roi, out JlTuple rb, out JlTuple phi);
	///   double a0 = ra[0].D;
	///   ra.Dispose(); rb.Dispose(); phi.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>三个元组都要 Dispose；单区域要标量见 double 重载。</para>
	/// </remarks>
	public JlTuple EllipticAxisGray(JlRegion regions, out JlTuple rb, out JlTuple phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1682);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out rb);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out phi);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>
	///   区域灰度等效椭圆的 ra/rb/phi（三个输出都是单值 double）。
	/// </summary>
	/// <param name="regions">要计算的区域。</param>
	/// <param name="rb">短半轴长度。</param>
	/// <param name="phi">长轴方位角。</param>
	/// <returns>长半轴长度 ra。</returns>
	/// <remarks>
	///   <para>灰度加权椭圆含义与圆盘退化问题见 <see cref="EllipticAxisGray(JlRegion,out JlTuple,out JlTuple)"/>：同一原生 id 1682，本版本三个输出 LoadD 只读<b>第一个值</b>，多区域其余结果静默丢弃；区域数不定或要逐区域比对时用元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion roi = new JlRegion(10.0, 10.0, 50.0, 50.0);
	///   double ra = img.EllipticAxisGray(roi, out double rb, out double phi);
	///   </code>
	/// </remarks>
	public double EllipticAxisGray(JlRegion regions, out double rb, out double phi)
	{
		IntPtr proc = JlNativeApi.PreCall(1682);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out rb);
		err = JlNativeApi.LoadD(proc, 2, err, out phi);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return doubleValue;
	}

	/// <summary>
	///   按灰度加权求每个区域的"面积"（灰度体积）与灰度质心，三个输出皆 DOUBLE 元组（原生算子 id 1683）。
	/// </summary>
	/// <param name="regions">要计算的区域，逐区域各出一组值。</param>
	/// <param name="row">灰度质心行坐标（row=y 向下为正，像素坐标）。</param>
	/// <param name="column">灰度质心列坐标（column=x 向右为正）。</param>
	/// <returns>灰度体积——区域内灰度求和，不是像素个数（DOUBLE 元组，用毕 Dispose）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>图像 iconc 2、区域 iconc 1；输出 0/1/2 = volume/row/column。质心按"亮度当质量"算：一个暗但大的区域，其灰度质心会偏向区域内较亮的部分，跟几何质心（JlRegion 的 AreaCenter）分道扬镳。</para>
	///   <para><b>约束或前提</b>volume 随像素数与灰度水平同时增长，跨图像/跨曝光比较无归一；灰度整体接近 0 的区域质心数值噪声爆炸（质量趋于零），必要时先 ScaleImage 抬底再算。</para>
	///   <para><b>与相邻算子的取舍</b>只要形状质心/像素面积 → JlRegion.AreaCenter；要均值 → Intensity；本算子回答"亮质量聚在哪"，常见于光斑/焊点定位。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion thr = img.Threshold(100.0, 255.0);
	///   using JlRegion spots = thr.Connection();
	///   JlTuple vol = img.AreaCenterGray(spots, out JlTuple row, out JlTuple col);
	///   double cRow = row[0].D;
	///   vol.Dispose(); row.Dispose(); col.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>三个元组都要 Dispose；多区域时元素序跟随 spots 的句柄内对象序。</para>
	/// </remarks>
	public JlTuple AreaCenterGray(JlRegion regions, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(1683);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return tuple;
	}

	/// <summary>
	///   区域灰度体积与灰度质心（三个输出都是单值 double）。
	/// </summary>
	/// <param name="regions">要计算的区域。</param>
	/// <param name="row">灰度质心行坐标。</param>
	/// <param name="column">灰度质心列坐标。</param>
	/// <returns>灰度体积（区域内灰度求和）。</returns>
	/// <remarks>
	///   <para>灰度加权的含义、低灰度区质心噪声问题见 <see cref="AreaCenterGray(JlRegion,out JlTuple,out JlTuple)"/>：同一原生 id 1683，本版本 LoadD 只读<b>第一个值</b>，多区域时必须改用元组版取逐区域序列。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion roi = new JlRegion(10.0, 10.0, 50.0, 50.0);
	///   double vol = img.AreaCenterGray(roi, out double row, out double col);
	///   </code>
	/// </remarks>
	public double AreaCenterGray(JlRegion regions, out double row, out double column)
	{
		IntPtr proc = JlNativeApi.PreCall(1683);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out row);
		err = JlNativeApi.LoadD(proc, 2, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return doubleValue;
	}

	/// <summary>
	///   把区域内本图像灰度沿行/列两个方向投影成两条 DOUBLE 元组序列（原生算子 id 1684）。
	/// </summary>
	/// <param name="region">参与投影的区域（域外像素不进入投影）。</param>
	/// <param name="mode">投影统计方式，决定每行/列汇成求和还是极值类量。Default: "simple"</param>
	/// <param name="vertProjection">竖直方向投影序列。</param>
	/// <returns>水平方向投影序列（DOUBLE 元组，用毕 Dispose）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>图像 iconc 2、区域 iconc 1、mode 以 STRING 写控制槽 0；两条输出各覆盖一个轴向，长度分别对应行数与列数（返回值对应哪个轴[待实测]）。用于找文本行/栅栏条纹这类沿轴周期结构。</para>
	///   <para><b>约束或前提</b>mode 各取值的确切公式[待实测]；灰度非零背景会整体抬升投影基线，投影找峰前通常先 Threshold 二值化再统计，或减掉背景。</para>
	///   <para><b>与相邻算子的取舍</b>二值区域投影（不看灰度）用区域族投影；要任意方向（非轴对齐）的剖面得走 rotate 后再投影或 measure 卡尺，本算子只有水平/垂直两轴。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 64, 64);
	///   using JlRegion roi = new JlRegion(0.0, 0.0, 63.0, 63.0);
	///   JlTuple horiz = img.GrayProjections(roi, "simple", out JlTuple vert);
	///   horiz.Dispose(); vert.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组与 out 元组成对释放；region 只传一个区域——多区域是否叠加投影[待实测]。</para>
	/// </remarks>
	public JlTuple GrayProjections(JlRegion region, string mode, out JlTuple vertProjection)
	{
		IntPtr proc = JlNativeApi.PreCall(1684);
		Store(proc, 2);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out vertProjection);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return tuple;
	}



	/// <summary>
	///   把本映射图像（map）转换成另一种映射类型，返回新映射图像（原生算子 id 1796，元组重载）。
	/// </summary>
	/// <param name="newType">目标映射类型字符串。Default: "coord_map_sub_pix"</param>
	/// <param name="imageWidth">被映射图像宽度的来源：传字符串（如 "map_width"）表示直接取映射表自身宽度参与换算；该语义[待实测]。Default: "map_width"</param>
	/// <returns>转换后的新映射图像句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本图像经 Store 作 iconc 1 输入，newType 以 STRING 写控制槽 0，imageWidth 以钉固元组写控制槽 1（调用后 UnpinTuple）；输出 LoadNew 新句柄。映射类算子家族（MapImage 等）依赖映射图像的存储约定，类型不匹配时几何会整体错位。</para>
	///   <para><b>约束或前提</b>输入必须是映射图像而非普通灰度图；合法 newType 取值集合与源类型限制本层看不到[待实测]。本文件内未发现现成的映射图像生成算子，映射表通常由标定/校正流程产出。</para>
	///   <para><b>与相邻算子的取舍</b>标量重载适合目标宽度已知为整数（640 等）的常规产线；宽度信息本就该沿用映射自身时（默认串）用本元组重载。一次性按矩阵映射用仿射/投影族，犯不上建映射表。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage mapImg = new JlImage();
	///   mapImg.ReadImage("calib/coord_map");
	///   using JlImage subPix = mapImg.ConvertMapType("coord_map_sub_pix", "map_width");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄必须释放；调用结束前本图像被 KeepAlive。</para>
	/// </remarks>
	public JlImage ConvertMapType(string newType, JlTuple imageWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1796);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, newType);
		JlNativeApi.Store(proc, 1, imageWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(imageWidth);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把本映射图像转换成另一种映射类型（原生算子 id 1796，宽度以整数直写的标量重载）。
	/// </summary>
	/// <param name="newType">目标映射类型字符串。Default: "coord_map_sub_pix"</param>
	/// <param name="imageWidth">被映射图像的宽度（像素列数），StoreI 以 INTEGER 直写，覆盖"取映射自身宽度"的默认行为。</param>
	/// <returns>转换后的新映射图像句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para>与元组重载同一原生 id、同一转换语义（见 <see cref="ConvertMapType(string,JlTuple)"/>）；本版本无钉固/解钉开销，imageWidth 是显式整数。<b>关键约定</b>：给 imageWidth 时换算按该宽度解释映射值——将来要映射的图像宽多少就给多少，给错整体坐标缩放错位。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage mapImg = new JlImage();
	///   mapImg.ReadImage("calib/coord_map");
	///   using JlImage forBig = mapImg.ConvertMapType("coord_map_sub_pix", 1280);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；输入须是映射图像，普通灰度图喂进来得到的是几何意义上的乱码[待实测其是否报错]。</para>
	/// </remarks>
	public JlImage ConvertMapType(string newType, int imageWidth)
	{
		IntPtr proc = JlNativeApi.PreCall(1796);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, newType);
		JlNativeApi.StoreI(proc, 1, imageWidth);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>
	///   由"世界平面→图像"的单应矩阵与相机内参矩阵反解平面刚体位姿，返回新 JlPose（原生算子 id 1798，静态）。
	/// </summary>
	/// <param name="homography">世界坐标到像素坐标的单应矩阵（iconc 0，钉固传入后 UnpinTuple）。</param>
	/// <param name="cameraMatrix">相机标定内参矩阵 K（iconc 1）。</param>
	/// <param name="method">位姿求解方式字符串。Default: "decomposition"</param>
	/// <returns>新建的 JlPose；JlPose 派生自 JlData、不实现 IDisposable，不需要也不能 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>静态方法，无 this；单应与 K 以 Store 传句柄、method 以 StoreS 写控制槽 2，输出 JlPose.LoadNew。世界坐标的单位长度会直接进入位姿平移分量（平移单位=世界单位[待实测]）。</para>
	///   <para><b>约束或前提</b>单应必须真是同一平面物体在"世界系→像素系"下的映射，且 K 与其标定一致——K 错则角度与距离一起错。分解类解法存在镜像歧义（两个几何可行的位姿），返回给哪一个[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>有完整标定数据时走标定族更稳；只有平面单应没有 K 时用不了本函数（K 是必传输入，不是可选修正）。JlHomMat2D 矩阵族适合 2D 对齐，不要拿位姿结果当 2D 变换继续叠。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHomMat2D k = new JlHomMat2D();
	///   JlHomMat2D h = new JlHomMat2D();
	///   JlPose pose = JlImage.ProjHomMat2dToPose(h, k, "decomposition");
	///   </code>
	///   <para><b>资源与坑</b>示例矩阵需换成标定/拟合所得真值；JlHomMat2D 与 JlPose 都不是 IDisposable，别对它们写 Dispose/using（编译不过）。</para>
	/// </remarks>
	public static JlPose ProjHomMat2dToPose(JlHomMat2D homography, JlHomMat2D cameraMatrix, string method)
	{
		IntPtr proc = JlNativeApi.PreCall(1798);
		JlNativeApi.Store(proc, 0, homography);
		JlNativeApi.Store(proc, 1, cameraMatrix);
		JlNativeApi.StoreS(proc, 2, method);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homography);
		JlNativeApi.UnpinTuple(cameraMatrix);
		err = JlPose.LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		return obj;
	}



	/// <summary>
	///   按映射图像逐像素重采样本图像，产出几何校正后的新图像（原生算子 id 1806）。
	/// </summary>
	/// <param name="map">映射图像：每个像素存放目标坐标的两通道表，作为 iconc 2 输入。</param>
	/// <returns>映射结果新图像句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本图像 Store 作 iconc 1、map 作 iconc 2，无控制参数——没有 interpolation 旋钮可选，采样方式由原生固定（nearest 类[待实测]）。输出 LoadNew 新句柄。</para>
	///   <para><b>约束或前提</b>map 必须是映射类型图像且几何/类型约定匹配（可先用 ConvertMapType 调整类型与宽度解释）；map 坐标越出本图像范围处的像素取值[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>全局仿射/投影变形用 AffineTransImage 族更省，不用建表；位移场展平用 UnwarpImage 族；本算子适合映射关系复杂且会复用同一张表批量处理多帧的场合（表只生成一次）。同一 map 反复用也比每帧重算矩阵便宜。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   using JlImage mapImg = new JlImage();
	///   mapImg.ReadImage("calib/coord_map");
	///   JlImage mapped = img.MapImage(mapImg);
	///   mapped.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；本图像与 map 在调用结束前都被 KeepAlive。</para>
	/// </remarks>
	public JlImage MapImage(JlImage map)
	{
		IntPtr proc = JlNativeApi.PreCall(1806);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, map);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(map);
		return obj;
	}





























	/// <summary>
	///   在本图像上同时搜索多个 NCC 模型的最优匹配，五个结果按实例逐个对齐成元组（原生算子 id 1958）。
	/// </summary>
	/// <param name="modelIDs">模型句柄数组：实现用 ConcatArray 拼成单个句柄元组写入原生控制槽 0，调用后解钉；数组里每个模型的生命周期仍归 C# 侧管。</param>
	/// <param name="angleStart">搜索的最小旋转角，单位弧度（与建模时的角度参数同单位）。Default: -0.39</param>
	/// <param name="angleExtent">角度覆盖总宽度（不是半宽），搜索区间为 [angleStart, angleStart+angleExtent]。Default: 0.79</param>
	/// <param name="minScore">接受实例的最低相关分数。Default: 0.8</param>
	/// <param name="numMatches">要找回的实例数，给 0 表示收全部过线实例。Default: 1</param>
	/// <param name="maxOverlap">实例间允许的最大重叠比例。Default: 0.5</param>
	/// <param name="subPixel">非 "none" 时启用亚像素精化。Default: "true"</param>
	/// <param name="numLevels">金字塔层数，0 为自动；英文文档另提 |numLevels|=2 时含义是最低层[待实测]。Default: 0</param>
	/// <param name="row">各实例中心行坐标（DOUBLE 元组）。</param>
	/// <param name="column">各实例中心列坐标。</param>
	/// <param name="angle">各实例旋转角（弧度）。</param>
	/// <param name="score">各实例相关分数。</param>
	/// <param name="model">各实例命中的模型在 modelIDs 中的序号（INTEGER 元组，计数起点[待实测]）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>本图像 iconc 1；7 个控制参数钉固传原生槽 1..7，输出 0..4：前四 DOUBLE、model 为 INTEGER。多模型合并成一次调用，比循环单模型省重复预处理，但 minScore 等阈值对所有模型共用同一组。</para>
	///   <para><b>约束或前提</b>NCC 按灰度相关打分，不支持尺度缩放（模板物理尺寸变了就掉分）；对比度极低/近纯色的模板分数噪声大。五个输出长度一致、按实例对齐。</para>
	///   <para><b>与相邻算子的取舍</b>工件尺度会变或主要靠轮廓时改用形状匹配族；只找一个模型用单模型版 FindNccModels（免拼接）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage tmpl = new JlImage("byte", 32, 32);
	///   using JlNCCModel m1 = new JlNCCModel(tmpl, 0, -0.39, 0.79, 0.01, "use_polarity");
	///   using JlNCCModel m2 = new JlNCCModel(tmpl, 0, -0.39, 0.79, 0.01, "use_polarity");
	///   using JlImage scene = new JlImage();
	///   scene.ReadImage("printer_chip/printer_chip_01");
	///   scene.FindNccModels(new JlNCCModel[] { m1, m2 }, -0.39, 0.79, 0.8, 1, 0.5, "true", 0,
	///       out JlTuple rows, out JlTuple cols, out JlTuple angs, out JlTuple scores, out JlTuple which);
	///   rows.Dispose(); cols.Dispose(); angs.Dispose(); scores.Dispose(); which.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>五个 out 元组都要 Dispose；模型数组里的元素各自 Dispose；调用结束前本图像与模型都被 KeepAlive。</para>
	/// </remarks>
	public void FindNccModels(JlNCCModel[] modelIDs, JlTuple angleStart, JlTuple angleExtent, JlTuple minScore, JlTuple numMatches, JlTuple maxOverlap, JlTuple subPixel, JlTuple numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		JlTuple hTuple = JlHandleBase.ConcatArray(modelIDs);
		IntPtr proc = JlNativeApi.PreCall(1958);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, hTuple);
		JlNativeApi.Store(proc, 1, angleStart);
		JlNativeApi.Store(proc, 2, angleExtent);
		JlNativeApi.Store(proc, 3, minScore);
		JlNativeApi.Store(proc, 4, numMatches);
		JlNativeApi.Store(proc, 5, maxOverlap);
		JlNativeApi.Store(proc, 6, subPixel);
		JlNativeApi.Store(proc, 7, numLevels);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(hTuple);
		JlNativeApi.UnpinTuple(angleStart);
		JlNativeApi.UnpinTuple(angleExtent);
		JlNativeApi.UnpinTuple(minScore);
		JlNativeApi.UnpinTuple(numMatches);
		JlNativeApi.UnpinTuple(maxOverlap);
		JlNativeApi.UnpinTuple(subPixel);
		JlNativeApi.UnpinTuple(numLevels);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>
	///   在本图像上搜索单个 NCC 模型的最优匹配（控制参数以标量直写，无拼接/钉固开销）。
	/// </summary>
	/// <param name="modelIDs">单个模型句柄，直接 Store 进原生控制槽 0。</param>
	/// <param name="angleStart">搜索的最小旋转角（弧度）。Default: -0.39</param>
	/// <param name="angleExtent">角度覆盖总宽度（不是半宽）。Default: 0.79</param>
	/// <param name="minScore">最低相关分数。Default: 0.8</param>
	/// <param name="numMatches">实例数上限，0 收全部。Default: 1</param>
	/// <param name="maxOverlap">实例间最大重叠比例。Default: 0.5</param>
	/// <param name="subPixel">非 "none" 启用亚像素。Default: "true"</param>
	/// <param name="numLevels">金字塔层数，0 自动。Default: 0</param>
	/// <param name="row">各实例行坐标（DOUBLE 元组）。</param>
	/// <param name="column">各实例列坐标。</param>
	/// <param name="angle">各实例旋转角（弧度）。</param>
	/// <param name="score">各实例分数。</param>
	/// <param name="model">各实例命中模型的序号（INTEGER，本重载恒指向唯一模型，取值[待实测]）。</param>
	/// <remarks>
	///   <para>搜索语义、输出对齐、NCC 不支持尺度的前提见元组/数组版 <see cref="FindNccModels(JlNCCModel[],JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple,out JlTuple)"/>；同一原生 id 1958，本版本 StoreD/StoreI/StoreS 直写参数 1..7。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage tmpl = new JlImage("byte", 32, 32);
	///   using JlNCCModel model = new JlNCCModel(tmpl, 0, -0.39, 0.79, 0.01, "use_polarity");
	///   using JlImage scene = new JlImage();
	///   scene.ReadImage("printer_chip/printer_chip_01");
	///   scene.FindNccModels(model, -0.39, 0.79, 0.8, 1, 0.5, "true", 0,
	///       out JlTuple rows, out JlTuple cols, out JlTuple angs, out JlTuple scores, out JlTuple which);
	///   rows.Dispose(); cols.Dispose(); angs.Dispose(); scores.Dispose(); which.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>五个 out 元组都要 Dispose；多模型用数组重载，别循环调用本重载把分数混在一起。</para>
	/// </remarks>
	public void FindNccModels(JlNCCModel modelIDs, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		IntPtr proc = JlNativeApi.PreCall(1958);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, modelIDs);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, minScore);
		JlNativeApi.StoreI(proc, 4, numMatches);
		JlNativeApi.StoreD(proc, 5, maxOverlap);
		JlNativeApi.StoreS(proc, 6, subPixel);
		JlNativeApi.StoreI(proc, 7, numLevels);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out angle);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out score);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out model);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(modelIDs);
	}



	/// <summary>
	///   把多通道（平面式）本图像重排成交错存储图像，返回新句柄（原生算子 id 1969，rowBytes 元组重载）。
	/// </summary>
	/// <param name="pixelFormat">目标交错格式串，如 rgb/rgba 一类通道排布。Default: "rgba"</param>
	/// <param name="rowBytes">输出每行字节数的来源：字符串（默认）表示按输出尺寸自动对齐。Default: "match"</param>
	/// <param name="alpha">三通道输入时补给的 alpha 常量（StoreI 整型装载）；四通道输入时忽略与否[待实测]。Default: 255</param>
	/// <returns>交错排布的新图像句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本图像 iconc 1；pixelFormat 写控制槽 0、rowBytes 钉固元组写槽 1、alpha 整型写槽 2；输出 LoadNew。交错排布是给编码器/GPU/位图导出喂数据用的内存布局，通道数与像素类型会随格式改变。</para>
	///   <para><b>约束或前提</b>输入应为多通道平面图像，单通道输入结果语义[待实测]；行对齐（rowBytes）不满足下游库要求时整幅图会错位。</para>
	///   <para><b>与相邻算子的取舍</b>只改通道语义不改内存排布，用 Rgb1ToGray/通道合成族；要喂给外部显示/编码管线才值得重排一次内存。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   JlTuple rb = "match";
	///   using JlImage rgba = img.InterleaveChannels("rgba", rb, 255);
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；本重载 rowBytes 有钉固/解钉，单值固定串场景可用 string 重载省开销。</para>
	/// </remarks>
	public JlImage InterleaveChannels(string pixelFormat, JlTuple rowBytes, int alpha)
	{
		IntPtr proc = JlNativeApi.PreCall(1969);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, pixelFormat);
		JlNativeApi.Store(proc, 1, rowBytes);
		JlNativeApi.StoreI(proc, 2, alpha);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowBytes);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   把多通道本图像重排成交错存储图像（rowBytes 以字符串直写的标量重载）。
	/// </summary>
	/// <param name="pixelFormat">目标交错格式串。Default: "rgba"</param>
	/// <param name="rowBytes">输出每行字节数：字符串形式（自动对齐），StoreS 直写无钉固。Default: "match"</param>
	/// <param name="alpha">三通道输入补给的 alpha 常量。Default: 255</param>
	/// <returns>交错排布的新图像句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para>排布语义、行对齐错位的坑见 <see cref="InterleaveChannels(string,JlTuple,int)"/>：同一原生 id 1969，本版本三个控制参数全直写，是常规路径；rowBytes 想给数值形式[待实测是否被接受]时用元组重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   using JlImage rgba = img.InterleaveChannels("rgba", "match", 255);
	///   </code>
	/// </remarks>
	public JlImage InterleaveChannels(string pixelFormat, string rowBytes, int alpha)
	{
		IntPtr proc = JlNativeApi.PreCall(1969);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, pixelFormat);
		JlNativeApi.StoreS(proc, 1, rowBytes);
		JlNativeApi.StoreI(proc, 2, alpha);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   MSER（最大稳定极值区域）分割本图像，暗区为返回值、亮区走 out，均为区域句柄（原生算子 id 1977，元组重载）。
	/// </summary>
	/// <param name="MSERLight">亮极性 MSER 区域（LoadNew 自 iconc 2），用毕 Dispose。</param>
	/// <param name="polarity">输出哪侧极性："both" 两侧都给，另一侧输出是否为空[待实测]。Default: "both"</param>
	/// <param name="minArea">保留区域的最小像素面积；元组重载可逐值传入（钉固），多值语义[待实测]。Default: 10</param>
	/// <param name="maxArea">最大像素面积，空元组表示不设上限。Default: []</param>
	/// <param name="delta">稳定性判据：区域需要承受的灰度阈值跨度（灰度级数，不是比例）。Default: 15</param>
	/// <param name="genParamName">附加通用参数名列表，可用名集合本层不可见[待实测]。Default: []</param>
	/// <param name="genParamValue">与名字一一对应的值列表。Default: []</param>
	/// <returns>暗极性 MSER 区域（iconc 1），新句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本图像 iconc 1；polarity 写控制槽 0，minArea/maxArea/delta/gen 参数钉固写槽 1..5、调用后解钉。MSER 对"缓慢变化的背景照度"天然免疫：它要的是阈值跨度内面积稳定的区域，不是绝对灰度达标的区域。</para>
	///   <para><b>约束或前提</b>输入应为单通道灰度图；delta 太小碎片化、太大会吞掉小目标，文字/丝印场景一般从小往大试。byte 图上的灰度级数与 float 图上的取值跨度的解释不同[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>背景平坦时 Threshold 更直接更快；光照不均想分前景用本算子（本库无 RLT 类光照不均二值化封装）；要纹理分区不用它（MSER 出的是一堆独立小块，不覆盖全图）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   using JlRegion dark = img.SegmentImageMser(out JlRegion light, "both",
	///       new JlTuple(10), new JlTuple(), new JlTuple(15), new JlTuple(), new JlTuple());
	///   </code>
	///   <para><b>资源与坑</b>返回与 out 两个区域句柄都要释放；元组实参由实现解钉，无需处理。</para>
	/// </remarks>
	public JlRegion SegmentImageMser(out JlRegion MSERLight, string polarity, JlTuple minArea, JlTuple maxArea, JlTuple delta, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(1977);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, polarity);
		JlNativeApi.Store(proc, 1, minArea);
		JlNativeApi.Store(proc, 2, maxArea);
		JlNativeApi.Store(proc, 3, delta);
		JlNativeApi.Store(proc, 4, genParamName);
		JlNativeApi.Store(proc, 5, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(minArea);
		JlNativeApi.UnpinTuple(maxArea);
		JlNativeApi.UnpinTuple(delta);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlRegion.LoadNew(proc, 2, err, out MSERLight);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   MSER 分割本图像（minArea/maxArea/delta 以整数直写的标量重载）。
	/// </summary>
	/// <param name="MSERLight">亮极性 MSER 区域（新句柄），用毕 Dispose。</param>
	/// <param name="polarity">输出极性。Default: "both"</param>
	/// <param name="minArea">最小像素面积（StoreI 直写）。Default: 10</param>
	/// <param name="maxArea">最大像素面积，整型必须给具体数，给 0 是否等于"不限"[待实测]。Default: []</param>
	/// <param name="delta">稳定判据的灰度阈值跨度。Default: 15</param>
	/// <param name="genParamName">附加参数名列表。Default: []</param>
	/// <param name="genParamValue">附加参数值列表。Default: []</param>
	/// <returns>暗极性 MSER 区域，新句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para>MSER 原理、delta 取向、极性输出见元组版 <see cref="SegmentImageMser(out JlRegion,string,JlTuple,JlTuple,JlTuple,JlTuple,JlTuple)"/>：同一原生 id 1977，本版本 minArea/maxArea/delta 走 StoreI 免钉固；差异点是<b>没有"空元组=不限"的表达法</b>，maxArea 要显式给上限值（可用图像总像素数）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   using JlRegion dark = img.SegmentImageMser(out JlRegion light, "dark",
	///       10, 64 * 64, 15, new JlTuple(), new JlTuple());
	///   </code>
	///   <para><b>资源与坑</b>polarity 给 "dark" 时 light 输出的状态[待实测]；两个区域句柄都要释放。</para>
	/// </remarks>
	public JlRegion SegmentImageMser(out JlRegion MSERLight, string polarity, int minArea, int maxArea, int delta, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(1977);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, polarity);
		JlNativeApi.StoreI(proc, 1, minArea);
		JlNativeApi.StoreI(proc, 2, maxArea);
		JlNativeApi.StoreI(proc, 3, delta);
		JlNativeApi.Store(proc, 4, genParamName);
		JlNativeApi.Store(proc, 5, genParamValue);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlRegion.LoadNew(proc, 2, err, out MSERLight);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>
	///   把 objectsInsert 里的图像对象整体插进本对象元组的指定位置，返回扩展后的新元组句柄（原生算子 id 2003）。
	/// </summary>
	/// <param name="objectsInsert">要插入的图像对象元组（iconc 2），其元素被并入结果。</param>
	/// <param name="index">插入位置序号；计数起点与越界行为[待实测]。</param>
	/// <returns>新的对象元组句柄（LoadNew），用毕 Dispose；本对象与原元组不受影响。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本图像按 iconc 1 作为对象元组输入，index 以 StoreI 写控制槽 0。返回的是重组后的<b>新</b>句柄壳，内部元素与被插对象共享——Dispose 结果只解除这次的引用集合。</para>
	///   <para><b>约束或前提</b>只能在本类型（图像元组）内拼接：混放区域/轮廓要用 JlObject 层的容器操作；元素顺序会左右后续按下标取件，插入后不要再假设原顺序。</para>
	///   <para><b>与相邻算子的取舍</b>尾部追加用 ConcatObj 级别的合并更直白；要在中间精确占位（如保持"序号↔工位"映射）才需要 index 版。删元素见 RemoveObj。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage a = new JlImage("byte", 32, 32);
	///   using JlImage b = new JlImage("byte", 32, 32);
	///   JlImage pair = a.InsertObj(b, 0);
	///   pair.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果句柄要释放；a、b 各自仍可单独 Dispose，互不牵连（共享元素语义[待实测]）。</para>
	/// </remarks>
	public JlImage InsertObj(JlImage objectsInsert, int index)
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

	/// <summary>
	///   从本图像对象元组中删除 index 指定的若干元素，返回剩下的新元组句柄（原生算子 id 2005，元组重载）。
	/// </summary>
	/// <param name="index">要删除元素的序号元组（可多值），钉固传入控制槽 0、调用后解钉；计数起点[待实测]。</param>
	/// <returns>剩余元素组成的新元组句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本图像 iconc 1 输入，输出 LoadNew 新句柄；被删元素从结果中脱离，剩下的元素<b>重新连续编号</b>——删完后按旧序号用 this[index] 取件会指错对象，这是此类容器操作最常见的静默错位。</para>
	///   <para><b>与相邻算子的取舍</b>它隐藏（new）了 JlObject.RemoveObj：以 JlImage 静态类型调用时走本版本、结果按图像元组装载。要留删后的中间态（不重组）没有对应算子；挑元素保留不如反向思维用 SelectObj/按序号取件。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage three = new JlImage("byte", 32, 32);
	///   JlImage rest = three.RemoveObj(new int[] { 0 });
	///   rest.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>被摘出去的图像对象由谁释放本层看不出来[待实测]；若元素不再被任何托管对象引用，谨慎起见先取件再 Dispose。序号越界行为[待实测]。</para>
	/// </remarks>
	public new JlImage RemoveObj(JlTuple index)
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

	/// <summary>
	///   从本图像对象元组删去单个序号的元素，返回剩余新元组（原生算子 id 2005，整数直写重载）。
	/// </summary>
	/// <param name="index">要删除的元素序号，StoreI 直写控制槽 0，无钉固。</param>
	/// <returns>剩余元素组成的新元组句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para>重组编号、隐藏 JlObject.RemoveObj 的语义与元组版一致（见 <see cref="RemoveObj(JlTuple)"/>）；单元素删除用本重载最省。删多个下标一次给 int[] 走元组重载，别连删多次——每删一次序号就整体前移。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage pair = new JlImage("byte", 32, 32);
	///   JlImage one = pair.RemoveObj(0);
	///   one.Dispose();
	///   </code>
	/// </remarks>
	public new JlImage RemoveObj(int index)
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

	/// <summary>
	///   用 objectsReplace 的元素替换本图像对象元组的指定位置，返回替换后的新元组（原生算子 id 2006，序号元组重载）。
	/// </summary>
	/// <param name="objectsReplace">提供替换内容的图像对象元组（iconc 2）。</param>
	/// <param name="index">被替换位置的序号元组，钉固写控制槽 0、调用后解钉；序号数与替换元素数不匹配时的行为[待实测]。</param>
	/// <returns>替换后的新元组句柄，用毕 Dispose；未被替换的元素原样保留。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>"先删后插"的组合语义在本算子内一次完成，序号按<b>原元组</b>解释（不像 RemoveObj 那样逐步前移）[待实测多下标时的编号基准]。本图像 iconc 1，输出 LoadNew。</para>
	///   <para><b>与相邻算子的取舍</b>只增不删用 InsertObj、只删不换用 RemoveObj，不要拿 ReplaceObj 配空参凑数；要整体重排顺序没有本族算子，只能逐件取出再拼接。原位置对象被顶替后由谁释放[待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage oldSet = new JlImage("byte", 32, 32);
	///   using JlImage fresh = new JlImage("byte", 32, 32);
	///   JlImage updated = oldSet.ReplaceObj(fresh, new int[] { 0 });
	///   updated.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>结果句柄要 Dispose；输入元组本体的处置与被顶替元素一样依赖库回收策略[待实测]。</para>
	/// </remarks>
	public JlImage ReplaceObj(JlImage objectsReplace, JlTuple index)
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

	/// <summary>
	///   替换本图像对象元组的单个位置（序号以整数直写，StoreI 免钉固）。
	/// </summary>
	/// <param name="objectsReplace">提供替换元素的图像对象元组；给多个元素而 index 只有一个位时其余去留[待实测]。</param>
	/// <param name="index">被替换的单个位置序号。</param>
	/// <returns>替换后的新元组句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para>替换语义、编号基准与容器族分工见 <see cref="ReplaceObj(JlImage,JlTuple)"/>：同一原生 id 2006，本版本仅 index 改直写。批量换多个位请一次给 int[] 用元组重载，别循环调本重载——每次都生成新句柄，中间态全泄漏。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage oldSet = new JlImage("byte", 32, 32);
	///   using JlImage fresh = new JlImage("byte", 32, 32);
	///   JlImage updated = oldSet.ReplaceObj(fresh, 0);
	///   updated.Dispose();
	///   </code>
	/// </remarks>
	public JlImage ReplaceObj(JlImage objectsReplace, int index)
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

	/// <summary>
	///   回读形状模型的抗干扰（clutter）配置：禁干扰区、变换矩阵、最小对比度与通用参数（原生算子 id 2055，静态，元组参数名）。
	/// </summary>
	/// <param name="modelID">模型句柄（iconc 0 输入），模型须以支持 clutter 的方式建过，否则各输出的有效性[待实测]。</param>
	/// <param name="genParamName">要回读的通用参数名（钉固传控制槽 1）。Default: "use_clutter"</param>
	/// <param name="genParamValue">从 iconc 输出 0 以字符串（LoadS）读回的参数值——传多个名字时只有单值通道，多名字结果被截[待实测]。</param>
	/// <param name="homMat2D">回读的变换矩阵；实现从 iconc 输出 1 装载（与区域同一槽位双解，含义以原生文档为准[待实测]）。</param>
	/// <param name="clutterContrast">判为干扰所需的最小灰度对比度，LoadI 按 INTEGER 读出。</param>
	/// <returns>不允许出现干扰的区域（新 JlRegion 句柄，用毕 Dispose）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>静态回读函数，与 SetShapeModelClutter 成对；genParamName 调用后解钉。对比 Set：那边写进模型、这边从模型读出，改完记得回读核对是否真的生效。</para>
	///   <para><b>与相邻算子的取舍</b>查一般建模参数（角度范围/尺度/metric 等）用 JlShapeModel.GetShapeModelParams，clutter 专属配置才走本函数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("models/part.shm");
	///   JlRegion noClutter = JlImage.GetShapeModelClutter(model, "use_clutter", out JlTuple values, out JlHomMat2D mat, out int contrast);
	///   values.Dispose();
	///   noClutter.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回区域与 out 元组要 Dispose；JlHomMat2D 不是 IDisposable，别 mat.Dispose()；modelID 由调用方管。</para>
	/// </remarks>
	public static JlRegion GetShapeModelClutter(JlShapeModel modelID, JlTuple genParamName, out JlTuple genParamValue, out JlHomMat2D homMat2D, out int clutterContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(2055);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, err, out genParamValue);
		err = JlHomMat2D.LoadNew(proc, 1, err, out homMat2D);
		err = JlNativeApi.LoadI(proc, 2, err, out clutterContrast);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(modelID);
		return obj;
	}

	/// <summary>
	///   回读形状模型的抗干扰配置（参数名与值都是单个 string 的标量重载）。
	/// </summary>
	/// <param name="modelID">模型句柄（iconc 0）。</param>
	/// <param name="genParamName">单个参数名，StoreS 直写免钉固。Default: "use_clutter"</param>
	/// <param name="genParamValue">对应的单个参数值（LoadS 读出）。</param>
	/// <param name="homMat2D">回读的变换矩阵（非 IDisposable）。</param>
	/// <param name="clutterContrast">最小干扰对比度（INTEGER）。</param>
	/// <returns>禁干扰区域新句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para>回读语义见元组版 <see cref="GetShapeModelClutter(JlShapeModel,JlTuple,out JlTuple,out JlHomMat2D,out int)"/>：同一原生 id 2055；本版本一次只查一个名字，值直接是 string，适合配置页显示/日志。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("models/part.shm");
	///   JlRegion noClutter = JlImage.GetShapeModelClutter(model, "use_clutter", out string value, out JlHomMat2D mat, out int contrast);
	///   noClutter.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>参数值给 "value" 之类模板占位串查不到东西——名字要用文档/回读确认过的。</para>
	/// </remarks>
	public static JlRegion GetShapeModelClutter(JlShapeModel modelID, string genParamName, out string genParamValue, out JlHomMat2D homMat2D, out int clutterContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(2055);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		err = JlNativeApi.LoadS(proc, 0, err, out genParamValue);
		err = JlHomMat2D.LoadNew(proc, 1, err, out homMat2D);
		err = JlNativeApi.LoadI(proc, 2, err, out clutterContrast);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(modelID);
		return obj;
	}

	/// <summary>
	///   给形状模型写入抗干扰（clutter）配置：禁干扰区+矩阵+最小对比度+通用参数组（原生算子 id 2057，静态，原地改模型）。
	/// </summary>
	/// <param name="clutterRegion">不允许出现干扰的区域（iconc 1），坐标按模型/搜索约定解释[待实测]。</param>
	/// <param name="modelID">要改的模型句柄（iconc 0），配置写进模型内部。</param>
	/// <param name="homMat2D">变换矩阵：钉固传控制槽 1、调用后解钉；作用（把 clutter 区/搜索旋转变换？）[待实测]。</param>
	/// <param name="clutterContrast">判定干扰所需最小灰度对比度，StoreI 按 INTEGER 写槽 2。Default: 128</param>
	/// <param name="genParamName">通用参数名列表（钉固）。可用名集合本层不可见[待实测]。</param>
	/// <param name="genParamValue">与名字一一对应的值列表。</param>
	/// <remarks>
	///   <para><b>功能说明</b>void、无输出——写的是模型内部状态，因此之后必须用 GetShapeModelClutter 回读才能确认生效。与 Get 成对使用。</para>
	///   <para><b>约束或前提</b>对不含形状描述子支持的普通模型/未以 clutter 能力建模的模型，写入是否报错[待实测]。该配置影响 find 匹配族的"抗干扰"类调用，不影响普通匹配调用。</para>
	///   <para><b>与相邻算子的取舍</b>标量重载适合单参数；批量名值对（如同时设多个开关）用本元组重载。别拿它改角度/尺度建模参数——那是 GetShapeModelParams/建模参数族的事。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("models/part.shm");
	///   using JlRegion okArea = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlHomMat2D mat = new JlHomMat2D();
	///   JlImage.SetShapeModelClutter(okArea, model, mat, 128,
	///       new string[] { "use_clutter" }, new JlTuple("true"));
	///   </code>
	///   <para><b>资源与坑</b>实参元组由实现解钉，model/region 调用方自管；homMat2D 是 JlHomMat2D——不可 Dispose。</para>
	/// </remarks>
	public static void SetShapeModelClutter(JlRegion clutterRegion, JlShapeModel modelID, JlHomMat2D homMat2D, int clutterContrast, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(2057);
		JlNativeApi.Store(proc, 1, clutterRegion);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.Store(proc, 1, homMat2D);
		JlNativeApi.StoreI(proc, 2, clutterContrast);
		JlNativeApi.Store(proc, 3, genParamName);
		JlNativeApi.Store(proc, 4, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(clutterRegion);
		GC.KeepAlive(modelID);
	}

	/// <summary>
	///   写入形状模型抗干扰配置（单参数名 string + 单参数值 double 的标量重载）。
	/// </summary>
	/// <param name="clutterRegion">禁干扰区域（iconc 1）。</param>
	/// <param name="modelID">目标模型（iconc 0）。</param>
	/// <param name="homMat2D">变换矩阵，钉固传控制槽 1。</param>
	/// <param name="clutterContrast">最小干扰对比度（INTEGER）。Default: 128</param>
	/// <param name="genParamName">单个参数名，StoreS 直写。模板占位值（如 "value"）无意义，用文档确认过的名字[待实测可用集]。</param>
	/// <param name="genParamValue">对应的数值参数，StoreD 按 DOUBLE 写——给不出字符串值的参数只能用元组重载传。</param>
	/// <remarks>
	///   <para>写模型内部状态的语义、回读核对要求见元组版 <see cref="SetShapeModelClutter(JlRegion,JlShapeModel,JlHomMat2D,int,JlTuple,JlTuple)"/>：同一原生 id 2057，本版本一次只设一个名值对、且值必须是数值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("models/part.shm");
	///   using JlRegion okArea = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlHomMat2D mat = new JlHomMat2D();
	///   JlImage.SetShapeModelClutter(okArea, model, mat, 128, "use_clutter", 1.0);
	///   </code>
	///   <para><b>资源与坑</b>示例值仅作签名示范，该参数是否接受数值形式[待实测]，以 Get 回读不报错为准；homMat2D 不可 Dispose。</para>
	/// </remarks>
	public static void SetShapeModelClutter(JlRegion clutterRegion, JlShapeModel modelID, JlHomMat2D homMat2D, int clutterContrast, string genParamName, double genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(2057);
		JlNativeApi.Store(proc, 1, clutterRegion);
		JlNativeApi.Store(proc, 0, modelID);
		JlNativeApi.Store(proc, 1, homMat2D);
		JlNativeApi.StoreI(proc, 2, clutterContrast);
		JlNativeApi.StoreS(proc, 3, genParamName);
		JlNativeApi.StoreD(proc, 4, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(clutterRegion);
		GC.KeepAlive(modelID);
	}

	/// <summary>
	///   只读取图像文件里的元数据标记值，不加载像素（原生算子 id 2062，静态）。
	/// </summary>
	/// <param name="format">文件图形格式串，须与文件实际格式一致。Default: "tiff"</param>
	/// <param name="tagName">标记名，逐格式各异（如 tiff_* 系列）。Default: "tiff_image_description"</param>
	/// <param name="fileName">目标文件路径。</param>
	/// <returns>标记值元组（新对象，用毕 Dispose）；标记不存在时的返回[待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>纯静态、无图像输入；format/fileName 直写，tagName 钉固后解钉，输出 LoadNew 元组。比 ReadImage 轻：不建图像句柄、不解码像素，适合产线追溯字段批量核对。</para>
	///   <para><b>约束或前提</b>支持的格式与标记名集合由原生决定[待实测]；png/bmp 等格式是否有对应标记名[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要像素用 ReadImage；要往文件里写同类字段用 WriteImageMetadata；序列化搬运图像本体用 SerializeImage。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple desc = JlImage.ReadImageMetadata("tiff", "tiff_image_description", "out/chip.tiff");
	///   desc.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组必须 Dispose；它不是图像句柄，别拿去做像素运算。</para>
	/// </remarks>
	public static JlTuple ReadImageMetadata(string format, JlTuple tagName, string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(2062);
		JlNativeApi.StoreS(proc, 0, format);
		JlNativeApi.Store(proc, 1, tagName);
		JlNativeApi.StoreS(proc, 2, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tagName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}

	/// <summary>以给定标记为种子做分水，把盆地与标记对应起来。</summary>
	/// <param name="markers">浸水的起始标记区域。</param>
	/// <returns>每个标记对应的盆地。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 2067。标记区域作为第二个图标输入（<c>Store(proc, 2, markers)</c>）传进去，
	///   图像本身是第一个输入；英文说明是"按标记合并盆地"，即先分水再把同属一个标记的盆地并回去。
	///   这是三个分水算子里唯一能<b>由调用方控制分割粒度</b>的那个：要几个目标就给几个标记。</para>
	///   <para><b>前提</b>标记必须是落在各目标内部的小区域（例如 <c>RegiongrowingMean</c> 的种子、
	///   区域 <c>DistanceTransform(...)</c> 得到的距离图峰值、或 <c>Threshold</c> 后手工缩到核心）；
	///   标记压不到底（重叠/相邻标记距离过近）时
	///   两个目标会被并进同一盆地。标记区域与图像坐标系必须一致，本层不做尺寸/域检查 [待实测]。</para>
	///   <para><b>输出</b>返回值是盆地集合；输出个数与标记个数的对应关系（是否一一对应、顺序是否保持）本层无法确定 [待实测]，
	///   用 <c>CountObj()</c> 核对，需要严格对应时用 <c>TestSubsetRegion</c> 判断包含关系。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion seeds = img.RegiongrowingMean(new JlTuple(120.0), new JlTuple(80.0), 5.0, 50);
	///   using JlRegion basins = img.WatershedsMarker(seeds);
	///   int n = basins.CountObj();
	///   </code>
	///   <para><b>资源与坑</b>只读 <paramref name="markers"/>，所有权不转移，调用方自行释放；
	///   代码末尾对图像与标记都 <c>GC.KeepAlive</c>。</para>
	/// </remarks>
	public JlRegion WatershedsMarker(JlRegion markers)
	{
		IntPtr proc = JlNativeApi.PreCall(2067);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, markers);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(markers);
		return obj;
	}

	/// <summary>
	///   把元数据标记写进图像文件（不改像素），与 ReadImageMetadata 配对（原生算子 id 2068，静态）。
	/// </summary>
	/// <param name="format">文件图形格式串，决定可写的标记集。Default: "tiff"</param>
	/// <param name="tagName">标记名。Default: "tiff_image_description"</param>
	/// <param name="tagValue">标记值（钉固元组，字符串可隐式转入）。</param>
	/// <param name="fileName">目标文件路径。</param>
	/// <remarks>
	///   <para><b>功能说明</b>四个控制参数（槽 0..3）直写/钉固，无任何图标输入——操作对象是<b>磁盘上的文件</b>而非内存图像；对已存在文件追加/覆写标记的冲突行为[待实测]。</para>
	///   <para><b>约束或前提</b>目标文件应已由 WriteImage 以同 format 写出，否则可能失败或凭空建出无像素的壳[待实测]。标记名与格式绑定（tiff 系列名只对 tiff 有效）。</para>
	///   <para><b>与相邻算子的取舍</b>写像素+压缩用 WriteImage；只带追溯信息（批次号、工位）用本函数，避免为注记重编码整幅图。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   img.WriteImage("tiff", 0, "out/chip.tiff");
	///   JlImage.WriteImageMetadata("tiff", "tiff_image_description", "batch-42", "out/chip.tiff");
	///   </code>
	///   <para><b>资源与坑</b>void、不产生句柄；元组实参由实现解钉；写后立即用 ReadImageMetadata 回读最稳妥。</para>
	/// </remarks>
	public static void WriteImageMetadata(string format, JlTuple tagName, JlTuple tagValue, string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(2068);
		JlNativeApi.StoreS(proc, 0, format);
		JlNativeApi.Store(proc, 1, tagName);
		JlNativeApi.Store(proc, 2, tagValue);
		JlNativeApi.StoreS(proc, 3, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tagName);
		JlNativeApi.UnpinTuple(tagValue);
		JlNativeApi.PostCall(proc, procResult);
	}

	/// <summary>
	///   从本图像裁出一个或多个任意朝向的矩形区域（元组重载，可批量给多组参数）（原生算子 id 2086）。
	/// </summary>
	/// <param name="row">旋转矩形中心的行坐标（row=y 向下），钉固传控制槽 0。Default: 300.0</param>
	/// <param name="column">旋转矩形中心的列坐标（column=x 向右）。Default: 200.0</param>
	/// <param name="phi">矩形朝向角，弧度制（英文文档明示 arc measure），相对水平轴，正方向[待实测]。Default: 0.0</param>
	/// <param name="length1">沿 phi 方向半边的半长（像素）。Default: 100.0</param>
	/// <param name="length2">垂直于 phi 方向半边的半长（像素）。Default: 20.0</param>
	/// <param name="alignToAxis">输出是否轴向对齐（true 时旋转框的外接轴对齐矩形，false 时输出内容保持倾斜？两种情形的确切输出框[待实测]）。Default: "true"</param>
	/// <param name="interpolation">越界像素的补边方式。Default: "constant"</param>
	/// <returns>裁剪结果新图像句柄；多组参数时是否返回多图像元组[待实测]，用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本图像 iconc 1；五个几何量钉固写槽 0..4、两个字符串直写槽 5/6，调用后逐个解钉。几何越出原图的部分按 interpolation 处理。</para>
	///   <para><b>约束或前提</b>phi 是<b>弧度</b>不是度——与某些角度参数为度的算子混用时最容易错。length1/length2 是半长，全宽要乘二。</para>
	///   <para><b>与相邻算子的取舍</b>正放的矩形直接 CropPart（免角度、免重采样）；带角度但只想看内容不变形的，用 RotateImage 按 phi 转正后再 CropPart；本算子专用于"目标本身是斜的、要按斜框精确取回"。alignToAxis="true" 会把补边角落也带进输出，喂检测模型前留意。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   using JlImage crop = img.CropRectangle2(new double[] { 100, 220 }, new double[] { 120, 260 },
	///       0.2, 60.0, 30.0, "true", "constant");
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；中心坐标是原图坐标系。</para>
	/// </remarks>
	public JlImage CropRectangle2(JlTuple row, JlTuple column, JlTuple phi, JlTuple length1, JlTuple length2, string alignToAxis, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(2086);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, phi);
		JlNativeApi.Store(proc, 3, length1);
		JlNativeApi.Store(proc, 4, length2);
		JlNativeApi.StoreS(proc, 5, alignToAxis);
		JlNativeApi.StoreS(proc, 6, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(length1);
		JlNativeApi.UnpinTuple(length2);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   裁剪单个任意朝向矩形（几何量以 double 直写，StoreD 免钉固）。
	/// </summary>
	/// <param name="row">矩形中心行坐标（单值）。Default: 300.0</param>
	/// <param name="column">矩形中心列坐标（单值）。Default: 200.0</param>
	/// <param name="phi">朝向角，弧度制。Default: 0.0</param>
	/// <param name="length1">phi 方向半边半长。Default: 100.0</param>
	/// <param name="length2">垂直半边半长。Default: 20.0</param>
	/// <param name="alignToAxis">输出是否轴向对齐。Default: "true"</param>
	/// <param name="interpolation">越界补边方式。Default: "constant"</param>
	/// <returns>裁剪结果新图像句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para>几何约定（弧度、半长）、alignToAxis 陷阱与取舍见元组版 <see cref="CropRectangle2(JlTuple,JlTuple,JlTuple,JlTuple,JlTuple,string,string)"/>：同一原生 id 2086，本版本一次只能裁一个框，但省全部钉固/解钉，单框产线首选。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   using JlImage crop = img.CropRectangle2(100.0, 120.0, 0.2, 60.0, 30.0, "true", "constant");
	///   </code>
	/// </remarks>
	public JlImage CropRectangle2(double row, double column, double phi, double length1, double length2, string alignToAxis, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(2086);
		Store(proc, 1);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, phi);
		JlNativeApi.StoreD(proc, 3, length1);
		JlNativeApi.StoreD(proc, 4, length2);
		JlNativeApi.StoreS(proc, 5, alignToAxis);
		JlNativeApi.StoreS(proc, 6, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}




	/// <summary>
	///   在滑动矩形窗内做直方图线性化（限对比度版，maxContrast 元组重载），返回新图像（原生算子 id 2152）。
	/// </summary>
	/// <param name="mode">处理模式串（精度/速度权衡的确切取值[待实测]）。Default: "accurate"</param>
	/// <param name="maskWidth">滤波窗宽度（像素，StoreI）。Default: 51</param>
	/// <param name="maskHeight">滤波窗高度（像素，StoreI）。Default: 51</param>
	/// <param name="maxContrast">对比度增益上限（钉固元组，默认量级远小于 1，判为归一化斜率[待实测]）。Default: 0.01</param>
	/// <returns>线性化后的新图像句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本图像 iconc 1；窗逐像素滑动、窗内做直方图线性化，maxContrast 截断变换斜率防止把噪声一起拉爆。与全局 <see cref="EquHistoImage()"/> 的本质区别是"局部自适应"。</para>
	///   <para><b>约束或前提</b>窗尺寸决定被压平的光照空间频率：窗要明显大于目标特征，否则目标自身的对比度结构会被当成"背景"抹掉；边缘处窗出界的处理方式[待实测]。byte/float 输入增益行为不同[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>整幅光照不均先想"减背景"或曲面归一化，别上局部直方图（会改像素值语义、伤后续绝对灰度阈值）；只要全局拉伸用 EquHistoImage；本算子适合"暗部细节要可见但不许噪点炸开"的取证目视。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   using JlImage eq = img.EquHistoImageRect("accurate", 51, 51, new JlTuple(0.01));
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；maxContrast 给 0/负值行为[待实测]；处理后旧阈值全部失效，需重新定标。</para>
	/// </remarks>
	public JlImage EquHistoImageRect(string mode, int maskWidth, int maskHeight, JlTuple maxContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(2152);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.StoreI(proc, 2, maskHeight);
		JlNativeApi.Store(proc, 3, maxContrast);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(maxContrast);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   滑动矩形窗直方图线性化（maxContrast 以 double 直写的标量重载）。
	/// </summary>
	/// <param name="mode">处理模式。Default: "accurate"</param>
	/// <param name="maskWidth">窗宽（像素）。Default: 51</param>
	/// <param name="maskHeight">窗高（像素）。Default: 51</param>
	/// <param name="maxContrast">对比度增益上限，StoreD 直写免钉固。Default: 0.01</param>
	/// <returns>线性化后的新图像句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para>局部线性化语义、窗尺寸与"旧阈值失效"的坑见元组版 <see cref="EquHistoImageRect(string,int,int,JlTuple)"/>：同一原生 id 2152，仅传参方式不同，单值场景用本重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage();
	///   img.ReadImage("printer_chip/printer_chip_01");
	///   using JlImage eq = img.EquHistoImageRect("accurate", 51, 51, 0.01);
	///   </code>
	/// </remarks>
	public JlImage EquHistoImageRect(string mode, int maskWidth, int maskHeight, double maxContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(2152);
		Store(proc, 1);
		JlNativeApi.StoreS(proc, 0, mode);
		JlNativeApi.StoreI(proc, 1, maskWidth);
		JlNativeApi.StoreI(proc, 2, maskHeight);
		JlNativeApi.StoreD(proc, 3, maxContrast);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   回读卡尺（测量）对象的参数/属性值，参数名以元组传入可批量（原生算子 id 2153，静态）。
	/// </summary>
	/// <param name="measureHandle">测量对象句柄（iconc 0 输入），只读不改。</param>
	/// <param name="genParamName">要查的参数名（钉固传控制槽 1、调用后解钉）。Default: "type"</param>
	/// <returns>参数值元组（iconc 0 读出，元素类型随参数而变[待实测]），用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>静态查询函数；多名字一次传入时返回逐一对应的值序列（装载未指定类型，按原生给的形态）。可用于产线自检"这个卡尺到底按什么参数在跑"。</para>
	///   <para><b>约束或前提</b>可用参数名集合本层不可见[待实测]（"type" 为文档示例值）；对空/已释放句柄调用报错形态[待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>改参数用对应的 Set/Gen 测量族接口；查"实际生效的测量结果"不是本函数的事，它只答配置。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   JlTuple t = JlImage.GetMeasureParam(caliper, new string[] { "type" });
	///   t.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组须 Dispose；单名字查询用 string 重载免钉固。</para>
	/// </remarks>
	public static JlTuple GetMeasureParam(JlMeasure measureHandle, JlTuple genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(2153);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(measureHandle);
		return tuple;
	}

	/// <summary>回读单个卡尺（测量）对象的参数/属性值（参数名以 string 直写，静态，原生算子 id 2153）。</summary>
	/// <param name="measureHandle">测量对象句柄（iconc 0 输入），只读不改。</param>
	/// <param name="genParamName">单个参数名，StoreS 直写控制槽 1、免钉固。Default: "type"</param>
	/// <returns>该参数值元组（iconc 0 读出，元素类型随参数而变[待实测]），用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>查配置不查结果：给定一个已生成的卡尺句柄，问它"某个参数当前是什么值"。与元组版 <see cref="GetMeasureParam(JlMeasure,JlTuple)"/> 同一原生 id 2153，区别只在本版本一次问一个名字、用 StoreS 直写省掉钉固/解钉，单参数自检首选。</para>
	///   <para><b>约束或前提</b>句柄必须仍存活（实现末尾 <c>GC.KeepAlive(measureHandle)</c>）；可用参数名集合本层不可见[待实测]（"type" 为文档示例值）。批量查多个名字请改用元组重载。</para>
	///   <para><b>与相邻算子的取舍</b>改参数用对应的 Set/Gen 测量族接口，本函数只读；查"实际测到的边缘/坐标"也不是它的事。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlMeasure caliper = new JlMeasure(32.0, 32.0, 0.0, 20.0, 4.0, 64, 64, "bilinear");
	///   JlTuple type = JlImage.GetMeasureParam(caliper, "type");   // string 实参精确绑定本重载
	///   type.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组是新建句柄须 Dispose；入参 string 由实现直写、调用方无需解钉。</para>
	/// </remarks>
	public static JlTuple GetMeasureParam(JlMeasure measureHandle, string genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(2153);
		JlNativeApi.Store(proc, 0, measureHandle);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(measureHandle);
		return tuple;
	}

	/// <summary>用任意形状区域掩膜做均值滤波，id 2154。</summary>
	/// <param name="mask">掩膜区域，决定参与平均的像素集合。</param>
	/// <returns>滤波后的新图像句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生算子 id 2154，掩膜是<b>图标输入</b>（区域，<c>Store(proc, 2, mask)</c>），
	///   每像素的输出只取"掩膜盖住的那些像素"的平均。因此掩膜形状直接决定平滑的方向性：
	///   细长水平掩膜沿行平滑、竖直掩膜沿列平滑，这是矩形窗 <see cref="MeanImage(int,int)"/> 做不到的。</para>
	///   <para><b>坑</b>掩膜像素数越少越接近原图（1 个像素时是恒等变换），噪声抑制与方向性直接受掩膜面积影响；
	///   掩膜若由分割结果生成，面积逐帧变化会让平滑强度逐帧不同——需要稳定强度时改用带显式窗尺寸的
	///   <see cref="MeanImage(int,int)"/>。本算子<b>没有</b>边界处理参数 [待实测：边缘如何取值]。</para>
	///   <para><b>与相邻算子的取舍</b>要各向同性平滑用 <c>MeanImage</c>/<see cref="GaussImage(int)"/>（更快、参数简单）；
	///   要任意形状但取排序值而不是均值，用 <see cref="RankImage(JlRegion,int,string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JLVisionLib;
	///
	///   JlImage img = new JlImage("byte", 640, 480);
	///   using JlRegion mask = new JlRegion(0.0, 0.0, 0.0, 9.0);        // 1×10 水平线掩膜：只沿行平滑
	///   using JlImage smooth = img.MeanImageShape(mask);
	///   </code>
	///   <para><b>资源与坑</b>掩膜只读、调用方释放；输出为新句柄。</para>
	/// </remarks>
	public JlImage MeanImageShape(JlRegion mask)
	{
		IntPtr proc = JlNativeApi.PreCall(2154);
		Store(proc, 1);
		JlNativeApi.Store(proc, 2, mask);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(mask);
		return obj;
	}




	/// <summary>
	///   给图像四周一圈加边界（宽/灰度以元组传入，可给多通道各设一个灰度；原生算子 id 2172）。
	/// </summary>
	/// <param name="size">边界宽度（像素），钉固传槽 0 后解钉。Default: 10</param>
	/// <param name="value">边界灰度，钉固传槽 1 后解钉；多通道图像可逐通道给值。Default: 100</param>
	/// <returns>加了边界的新图像句柄（尺寸各方向 +size），用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原图不变，返回一幅四周各扩 <paramref name="size"/> 像素、填充区取 <paramref name="value"/> 的新图。本元组版把 size、value 都按 Store 钉固、调用后 UnpinTuple，与标量版 <see cref="AddImageBorder(int,int)"/> 同一 id 2172。</para>
	///   <para><b>约束或前提</b>value 元组长度应等于通道数——灰度图给 1 个、RGB 给 3 个；长度不符的行为[待实测]。常用于把目标从图边"顶"进来，避免后续膨胀/滤波在边缘丢信息。</para>
	///   <para><b>与相邻算子的取舍</b>单色边界用标量版 <see cref="AddImageBorder(int,int)"/>（StoreI 直写、免钉固，更快）；只想补黑边且无逐通道需求时也用它。需要逐通道不同颜色（如把边界染成洋红标记）才用本元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage framed = img.AddImageBorder(new JlTuple(10.0), new JlTuple(0.0, 128.0, 255.0));
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；原图中心会被平移，跨界坐标（如已算好的 ROI/卡尺位置）需随之偏移 size。</para>
	/// </remarks>
	public JlImage AddImageBorder(JlTuple size, JlTuple value)
	{
		IntPtr proc = JlNativeApi.PreCall(2172);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, size);
		JlNativeApi.Store(proc, 1, value);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(size);
		JlNativeApi.UnpinTuple(value);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   给图像四周一圈加边界（宽/灰度为标量 int，StoreI 直写；原生算子 id 2172）。
	/// </summary>
	/// <param name="size">边界宽度（像素），全通道统一。Default: 10</param>
	/// <param name="value">边界灰度，全通道同一值。Default: 100</param>
	/// <returns>加了边界的新图像句柄（尺寸各方向 +size），用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>返回一幅四周各扩 <paramref name="size"/> 像素、填充区取灰度 <paramref name="value"/> 的新图，本版本两值都 StoreI 直写、无钉固，是加均匀边框的最快路径。</para>
	///   <para><b>约束或前提</b><paramref name="value"/> 是单一灰度，多通道图上各通道取同值；要逐通道不同颜色须改用元组版 <see cref="AddImageBorder(JlTuple,JlTuple)"/>。典型取 0（黑边）。</para>
	///   <para><b>与相邻算子的取舍</b>需要逐通道/多值边界用元组版，其余情形用本标量版即可；只想补黑边把 <paramref name="value"/> 设为 0。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 640, 480);
	///   using JlImage framed = img.AddImageBorder(10, 0);   // int 实参精确绑定本重载：四周 10 像素黑边
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；加边后原图中心平移 size，已算好的 ROI/卡尺/坐标需随之偏移。</para>
	/// </remarks>
	public JlImage AddImageBorder(int size, int value)
	{
		IntPtr proc = JlNativeApi.PreCall(2172);
		Store(proc, 1);
		JlNativeApi.StoreI(proc, 0, size);
		JlNativeApi.StoreI(proc, 1, value);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   沿"通道维"做多通道加权卷积（把各通道按 filter 系数线性组合，非空间滤波；原生算子 id 2219）。
	/// </summary>
	/// <param name="filter">通道组合系数，钉固传槽 0 后解钉；长度=通道数，输出通道 j = Σ filter[i]×通道 i。</param>
	/// <param name="border">通道序列两端的补边方式（越出通道数时如何取值[待实测]）。Default: "constant"</param>
	/// <returns>卷积后的新图像句柄，用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>对每个像素，沿通道方向用 filter 系数做一维线性组合——典型用途是把 RGB 三通道按权重压成一路灰度，或做通道间差分。它不动空间邻域，与 <see cref="MeanImage(int,int)"/>/<see cref="GaussImage(int)"/> 这类 2D 空间平滑是正交的两回事。</para>
	///   <para><b>约束或前提</b>输入需为多通道图像才有意义；灰度单通道图上退化为按系数缩放，易得全黑[待实测]。filter 长度与通道数不匹配时按 border 方式外推。</para>
	///   <para><b>与相邻算子的取舍</b>只想取某一路通道用分量提取，别用本算子配一堆 0/1；要平滑空间噪声用 2D 滤波；本算子专用于"通道维线性变换"（加权灰度化、通道混合）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage rgb = new JlImage("byte", 640, 480);
	///   // 三通道等权平均（仅演示系数形态；加权灰度化常用 0.299/0.587/0.114）
	///   using JlImage mix = rgb.ConvolChannels(new JlTuple(1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0), "constant");
	///   </code>
	///   <para><b>资源与坑</b>filter 由实现解钉，调用方无需处理；返回新句柄须释放。系数含负值时结果可能出现负/溢出，输出类型是否饱和[待实测]。</para>
	/// </remarks>
	public JlImage ConvolChannels(JlTuple filter, string border)
	{
		IntPtr proc = JlNativeApi.PreCall(2219);
		Store(proc, 1);
		JlNativeApi.Store(proc, 0, filter);
		JlNativeApi.StoreS(proc, 1, border);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(filter);
		err = LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}


	/// <summary>生成 Savitzky-Golay 一维 FIR 系数（对窗口做最小二乘多项式拟合，可选导数阶；静态，原生算子 id 2223）。</summary>
	/// <param name="filterSize">核长度（点数），应为奇数。Default: 11</param>
	/// <param name="polynomialDegree">拟合多项式的阶数，须小于 filterSize。Default: 3</param>
	/// <param name="derivative">求导阶数：0＝平滑核，≥1＝对应导数核（定位峰/过零）。Default: 0</param>
	/// <returns>系数元组（按 DOUBLE 装载，见实现 LoadNew 的 JlTupleType.DOUBLE），用毕 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>产出一维核系数，不是直接滤波：拿到元组后交给做 1D/沿某轴卷积的算子使用。S-G 的优点是在平滑的同时尽量保留峰形与峰位，比普通移动平均更少"把峰削平"。</para>
	///   <para><b>约束或前提</b>数学定义要求 <c>filterSize</c> 为奇数且 <c>polynomialDegree &lt; filterSize</c>、<c>derivative ≤ polynomialDegree</c>；越界时原生是报错还是内部修正[待实测]。核越大平滑越强但越钝。</para>
	///   <para><b>与相邻算子的取舍</b>要各向同性 2D 平滑用 <see cref="GaussImage(int)"/>；只想平滑曲线又保峰用本函数 derivative=0 的核；要找峰/拐点则用 derivative=1 或 2 的核看过零与极值。它只给系数，不做边界处理，越界行为取决于使用它的卷积算子。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple smooth = JlImage.GenSavitzkyGolayFilter(11, 3, 0);   // 11 点 3 阶平滑核
	///   JlTuple firstDeriv = JlImage.GenSavitzkyGolayFilter(11, 3, 1); // 一阶导核，用于定位峰位
	///   smooth.Dispose();
	///   firstDeriv.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组是新建句柄须 Dispose；derivative≥1 时系数和≈0（导数核），直接套到常数段会得到 0。</para>
	/// </remarks>
	public static JlTuple GenSavitzkyGolayFilter(int filterSize, int polynomialDegree, int derivative)
	{
		IntPtr proc = JlNativeApi.PreCall(2223);
		JlNativeApi.StoreI(proc, 0, filterSize);
		JlNativeApi.StoreI(proc, 1, polynomialDegree);
		JlNativeApi.StoreI(proc, 2, derivative);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		return tuple;
	}
}
