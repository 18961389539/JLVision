using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>表示用于测量距离的工具实例。</summary>
[Serializable]
public class JlMeasure : JlHandle, ISerializable, ICloneable
{
	/// <summary>
	///   框架与占位用的纯托管空壳构造（带 EditorBrowsable(Never)）：基类句柄值置 UNDEF（IntPtr.Zero），不发任何原生调用，IsInitialized() 为 false，空壳直接送进测量算子会由 PostCall 报错。它是 LoadNew、Clone、静态 Deserialize 与 ISerializable 构造共用的"先建壳再原地装载"起点；业务取可用卡尺应走几何参数构造（矩形二代 816、圆弧环带 815，返回持新句柄的对象）或 ReadMeasure（id 802，先释放旧句柄再原地装入）。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>base(JlHandleBase.UNDEF) 落到句柄赋值的 UNDEF 分支——跳过原生 CopyHandle，只建一个待装载的托管壳，零原生开销；本类没有文件名构造器，从文件读卡尺要在空壳上调 ReadMeasure。</para>
	///   <para><b>约束或前提</b>空壳正好充当 DeserializeMeasure（id 800）等原地装载通道的接收位（基类 Load 要求装载前当前值必须是 UNDEF）；用完同样要 Dispose 收尾。</para>
	///   <para><b>与相邻构造器的取舍</b>要新卡尺用带几何参数的构造器；手里已有原生句柄用 IntPtr 或 JlHandle 包装构造；改卡尺几何不在原地调参，而是重跑 GenMeasureRectangle2/GenMeasureArc（它们先 Dispose 本对象旧句柄再装入重建结果）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure shell = new JlMeasure();     // 空壳：无原生卡尺句柄
	///   bool ready = shell.IsInitialized();    // false
	///   shell.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本构造与其余三个构造器都标 EditorBrowsable(Never)，常规补全只暴露几何构造；后续 MeasurePos 等测量调用的坐标、幅值输出按 DOUBLE 元组装载。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMeasure()
		: base(JlHandleBase.UNDEF)
	{
	}

	/// <summary>
	///   包装外部裸句柄值的浅拷贝通道（带 EditorBrowsable(Never)）：经基类 Handle 赋值走原生 CopyHandle 另取一份引用——拿到的是同一原生 measure 的第二个名字，不接管原指针所有权，两壳各释各的引用；句柄语义类型必须为 measure，不符抛 JlException("Invalid handle instance passed")，传 IntPtr.Zero（UNDEF）则跳过拷贝与类型校验、直接得空壳；悬垂句柄值在构造阶段就抛 JlOperatorException，不会静默退化成空壳。
	/// </summary>
	/// <param name="handle">原生侧当前有效的 measure 句柄值（多来自其他语言接口或非托管层传递）；IntPtr.Zero 视同 UNDEF 得空壳。</param>
	/// <remarks>
	///   <para><b>功能说明</b>base(handle) 进 JlHandleBase 的 Handle setter：SetHandleInternal 先清本壳旧值再 HLICopyHandle 取引用；随后 AssertSemType 调原生 GetHandleSemType 比对 "measure"，只校验语义类型、不看卡尺内容。</para>
	///   <para><b>约束或前提</b>引用计数级浅拷贝：一侧原地改写（如 ReadMeasure/DeserializeMeasure 重装载）后另一侧经同一原生对象看到新内容 [共享程度待实测]；要真正独立请走 Clone()（经序列化往返新建对象）。</para>
	///   <para><b>与相邻构造器的取舍</b>手里是活的 JlHandle 包装对象就写 JlMeasure(JlHandle) 版；要跨进程/跨语言搬内容用序列化通道，别传裸值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlMeasure cal = new JlMeasure(256.0, 256.0, 0.0, 100.0, 5.0, 512, 512, "nearest_neighbor");  // 矩形二代卡尺（id 816）
	///   IntPtr raw = cal.Handle;                    // 裸句柄值
	///   using JlMeasure alias = new JlMeasure(raw); // 再取一份引用，两个 C# 壳独立
	///   </code>
	///   <para><b>资源与坑</b>存 raw 这种裸值不带引用计数：cal 一 Dispose，raw 就成了非 0 的悬垂值，再喂给本构造会在原生拷贝处抛错。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMeasure(IntPtr handle)
		: base(handle)
	{
		AssertSemType();
	}

	/// <summary>
	///   包装任意 JlHandle 派生对象的拷贝构造（带 EditorBrowsable(Never)）：与 IntPtr 版同一基类路径——CopyHandle 引用计数拷贝，C# 壳独立、各须各 Dispose，但不是深拷贝；构造末尾按语义类型 measure 校验，源句柄是 image/ncc_model 等其他类型即抛 JlException("Invalid handle instance passed")；实参传 null 不抛空引用，经 IntPtr 隐式转换归一为 UNDEF、得到空壳且跳过类型校验。
	/// </summary>
	/// <param name="handle">源句柄包装对象，语义类型须为 measure；传 null 归一为空壳。</param>
	/// <remarks>
	///   <para><b>功能说明</b>base(handle) 进 JlHandleBase 拷贝路径后 AssertSemType("measure")；校验发生在包装之后——类型不符时原生引用已多持一份，此时 C# 壳尚未交付、无人替它 Dispose，这份引用是否泄漏与如何回收 [待实测]。</para>
	///   <para><b>约束或前提</b>拷贝粒度是"第二个名字指向同一原生资源"：一侧原地改写内容另一侧同步可见 [共享程度待实测]；要彻底独立用 Clone()。</para>
	///   <para><b>与相邻构造器的取舍</b>裸 IntPtr 用上一重载；只在本地多持一个容器用本版，几乎零开销；跨进程传内容走 Serialize/Deserialize 字节通道。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlHandle src = new JlMeasure(256.0, 256.0, 0.0, 100.0, 5.0, 512, 512, "nearest_neighbor");
	///   JlMeasure cal = new JlMeasure(src);   // 引用拷贝包装；src 换非 measure 句柄即抛 JlException
	///   cal.Dispose();
	///   src.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>两个壳谁先释放都行（各还各的引用），但在另一壳还活着之前别把 src 置 null 当作"已被接管"。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMeasure(JlHandle handle)
		: base(handle)
	{
		AssertSemType();
	}

	private void AssertSemType()
	{
		AssertSemType("measure");
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlMeasure obj)
	{
		obj = new JlMeasure(JlHandleBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlMeasure[] obj)
	{
		err = JlTuple.LoadNew(proc, parIndex, err, out var tuple);
		obj = new JlMeasure[tuple.Length];
		for (int i = 0; i < tuple.Length; i++)
		{
			obj[i] = new JlMeasure(tuple[i].H);
		}
		tuple.Dispose();
		return err;
	}

	/// <summary>
	///   以 JlTuple 参数构造圆弧环带卡尺（原生 id 815）：沿半径方向扫描提取直线边缘，构造成功后本对象持有新建的原生 measure 句柄。
	/// </summary>
	/// <param name="centerRow">圆弧中心的行坐标。默认值：100.0</param>
	/// <param name="centerCol">圆弧中心的列坐标。默认值：100.0</param>
	/// <param name="radius">圆弧的半径。默认值：50.0</param>
	/// <param name="angleStart">弧的起始角（弧度）。默认值：0.0</param>
	/// <param name="angleExtent">弧的角度范围（弧度）。默认值：6.28318</param>
	/// <param name="annulusRadius">环带的半径（半宽）。默认值：10.0</param>
	/// <param name="width">后续待处理图像的宽度。默认值：512</param>
	/// <param name="height">后续待处理图像的高度。默认值：512</param>
	/// <param name="interpolation">使用的插值方式。默认值："nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>构造圆弧环带卡尺（原生算子 id 815）：以 (centerRow, centerCol) 为圆心、radius 为中心线半径、沿半径方向扫描提取直线边缘。构造成功后本对象持有新建的原生 measure 句柄。</para>
	///   <para><b>与 double 重载的差异</b></para>
	///   <para>同一原生 id 815。此重载以 Store + UnpinTuple 直接透传已存在的 JlTuple（零拷贝）；double 重载经 StoreD 逐个临时建元组。约定详见 double 重载的注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlTuple centerRow = 100.0;
	///   JlTuple centerCol = 100.0;
	///   JlTuple radius = 50.0;
	///   JlTuple angleStart = 0.0;
	///   JlTuple angleExtent = 6.28318;
	///   JlTuple annulusRadius = 10.0;
	///   JlMeasure ring = new JlMeasure(centerRow, centerCol, radius, angleStart, angleExtent, annulusRadius, 512, 512, "nearest_neighbor");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>传入多值数组是否会展开为多个卡尺或仅取首元素 [待实测]。句柄用毕请 Dispose（或 CloseMeasure，但见 CloseMeasure 注释中的坑）。</para>
	/// </remarks>
	public JlMeasure(JlTuple centerRow, JlTuple centerCol, JlTuple radius, JlTuple angleStart, JlTuple angleExtent, JlTuple annulusRadius, int width, int height, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(815);
		JlNativeApi.Store(proc, 0, centerRow);
		JlNativeApi.Store(proc, 1, centerCol);
		JlNativeApi.Store(proc, 2, radius);
		JlNativeApi.Store(proc, 3, angleStart);
		JlNativeApi.Store(proc, 4, angleExtent);
		JlNativeApi.Store(proc, 5, annulusRadius);
		JlNativeApi.StoreI(proc, 6, width);
		JlNativeApi.StoreI(proc, 7, height);
		JlNativeApi.StoreS(proc, 8, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(centerRow);
		JlNativeApi.UnpinTuple(centerCol);
		JlNativeApi.UnpinTuple(radius);
		JlNativeApi.UnpinTuple(angleStart);
		JlNativeApi.UnpinTuple(angleExtent);
		JlNativeApi.UnpinTuple(annulusRadius);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   以 double 参数构造圆弧环带卡尺（原生 id 815）：在圆心 (centerRow, centerCol)、半径 radius 的圆环上沿径向扫描提取垂直于弧的直线边缘，angleStart/angleExtent 以弧度界定弧段，本对象持有新建原生句柄。
	/// </summary>
	/// <param name="centerRow">圆弧中心的行坐标。默认值：100.0</param>
	/// <param name="centerCol">圆弧中心的列坐标。默认值：100.0</param>
	/// <param name="radius">圆弧的半径。默认值：50.0</param>
	/// <param name="angleStart">弧的起始角（弧度）。默认值：0.0</param>
	/// <param name="angleExtent">弧的角度范围（弧度）。默认值：6.28318</param>
	/// <param name="annulusRadius">环带的半径（半宽）。默认值：10.0</param>
	/// <param name="width">后续待处理图像的宽度。默认值：512</param>
	/// <param name="height">后续待处理图像的高度。默认值：512</param>
	/// <param name="interpolation">使用的插值方式。默认值："nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>构造圆弧环带卡尺（原生 id 815）：在圆心 (centerRow, centerCol)、半径 radius 的圆环上提取垂直于弧的直线边缘。扫描方向为径向（由内向外），angleStart / angleExtent 以弧度界定弧段；annulusRadius 是环带半宽，扫描时对该宽度内沿圆周方向的灰度取平均。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>width / height 必须与后续传入 MeasurePos 等算子的图像尺寸一致（英文参数说明即 "of the image to be processed subsequently"），否则卡尺区域与图像裁剪基准不符。</para>
	///   <para><b>参数取向</b></para>
	///   <para>需要测量孔径、轴径等径向边界时：radius 取名义半径，annulusRadius 覆盖位置偏差，angleExtent 用 6.28318 取全周或按可见弧段缩小。interpolation 为灰度采样插值方式，可选值集合 [待实测]。positive/negative 等极性以"沿径向由内向外"为扫描方向定义。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure ring = new JlMeasure(256.0, 256.0, 120.0, 0.0, 6.28318, 15.0, 512, 512, "nearest_neighbor");
	///   using (JlImage image = new JlImage("byte", 512, 512))
	///   {
	///       ring.MeasurePos(image, 1.0, 30.0, "all", "all",
	///           out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude, out JlTuple distance);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>对象持有原生句柄，不用的实例要 Dispose；重复建模可直接调 GenMeasureArc（会先释放旧句柄）。径向单点快速定位用本类；要同时拟合整圆并给半径拟合值，用 JlMetrologyModel.AddMetrologyObjectCircleMeasure。</para>
	/// </remarks>
	public JlMeasure(double centerRow, double centerCol, double radius, double angleStart, double angleExtent, double annulusRadius, int width, int height, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(815);
		JlNativeApi.StoreD(proc, 0, centerRow);
		JlNativeApi.StoreD(proc, 1, centerCol);
		JlNativeApi.StoreD(proc, 2, radius);
		JlNativeApi.StoreD(proc, 3, angleStart);
		JlNativeApi.StoreD(proc, 4, angleExtent);
		JlNativeApi.StoreD(proc, 5, annulusRadius);
		JlNativeApi.StoreI(proc, 6, width);
		JlNativeApi.StoreI(proc, 7, height);
		JlNativeApi.StoreS(proc, 8, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   以 JlTuple 参数构造矩形卡尺（原生 id 816）：在矩形内提取垂直于长轴的直线边缘，phi 以弧度定长轴方向，本对象持有新建原生句柄。
	/// </summary>
	/// <param name="row">矩形中心的行坐标。默认值：300.0</param>
	/// <param name="column">矩形中心的列坐标。默认值：200.0</param>
	/// <param name="phi">矩形长轴相对水平方向的角度（弧度）。默认值：0.0</param>
	/// <param name="length1">矩形的半宽。默认值：100.0</param>
	/// <param name="length2">矩形的半高。默认值：20.0</param>
	/// <param name="width">后续待处理图像的宽度。默认值：512</param>
	/// <param name="height">后续待处理图像的高度。默认值：512</param>
	/// <param name="interpolation">使用的插值方式。默认值："nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>构造矩形卡尺（原生 id 816）的 JlTuple 重载。几何约定、参数取向详见 double 重载的注释。</para>
	///   <para><b>与 double 重载的差异</b></para>
	///   <para>同一原生 id 816。此重载用 Store + UnpinTuple 透传已存在的 JlTuple（零拷贝）；double 重载经 StoreD 逐个临时建元组。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlTuple row = 256.0;
	///   JlTuple column = 128.0;
	///   JlTuple phi = 0.0;
	///   JlTuple length1 = 60.0;
	///   JlTuple length2 = 20.0;
	///   JlMeasure caliper = new JlMeasure(row, column, phi, length1, length2, 512, 512, "nearest_neighbor");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>传入多值数组是否展开为多个卡尺 [待实测]。句柄用毕请 Dispose。</para>
	/// </remarks>
	public JlMeasure(JlTuple row, JlTuple column, JlTuple phi, JlTuple length1, JlTuple length2, int width, int height, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(816);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, phi);
		JlNativeApi.Store(proc, 3, length1);
		JlNativeApi.Store(proc, 4, length2);
		JlNativeApi.StoreI(proc, 5, width);
		JlNativeApi.StoreI(proc, 6, height);
		JlNativeApi.StoreS(proc, 7, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(length1);
		JlNativeApi.UnpinTuple(length2);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   以 double 参数构造矩形卡尺（原生 id 816）：沿长轴（phi 以弧度定向）扫描提取垂直于长轴的直线边缘，length1 定扫描行程、length2 定平均带宽，本对象持有新建原生句柄。
	/// </summary>
	/// <param name="row">矩形中心的行坐标。默认值：300.0</param>
	/// <param name="column">矩形中心的列坐标。默认值：200.0</param>
	/// <param name="phi">矩形长轴相对水平方向的角度（弧度）。默认值：0.0</param>
	/// <param name="length1">矩形的半宽。默认值：100.0</param>
	/// <param name="length2">矩形的半高。默认值：20.0</param>
	/// <param name="width">后续待处理图像的宽度。默认值：512</param>
	/// <param name="height">后续待处理图像的高度。默认值：512</param>
	/// <param name="interpolation">使用的插值方式。默认值："nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>构造矩形卡尺（原生 id 816）：在矩形内提取垂直于其长轴的直线边缘。这是 1D 卡尺的主入口。(row, column) 为矩形中心，phi 为长轴相对水平方向的弧度角；length1 是沿长轴的半长——长轴方向就是扫描/搜索方向；length2 是沿短轴的半宽——决定对多宽的灰度带做平均（边缘要横跨这条带）。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>width / height 声明后续待处理图像的尺寸，须与真正传给 MeasurePos / MeasurePairs 的图像一致。被测边缘须大致垂直于长轴（即平行于短轴），否则该次扫描无法定位。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>卡尺位置要随定位结果移动时优先 TranslateMeasure（原地平移、免重建）；重新指定全部几何则用 GenMeasureRectangle2（原地换句柄）或本构造器（新对象）。</para>
	///   <para><b>参数取向</b></para>
	///   <para>找竖直边缘：phi = 0，length1 覆盖工件位置公差的搜索行程，length2 取边缘沿竖直方向可平均的范围（噪声大取宽）。interpolation 可选值集合 [待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   using (JlImage image = new JlImage("byte", 512, 512))
	///   {
	///       caliper.MeasurePos(image, 1.0, 30.0, "positive", "all",
	///           out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude, out JlTuple distance);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>原生句柄须 Dispose；1D 卡尺只给边缘坐标与灰度幅值，不拟合几何形状，需要直线/圆的完整拟合与不确定度时改用 JlMetrologyModel。</para>
	/// </remarks>
	public JlMeasure(double row, double column, double phi, double length1, double length2, int width, int height, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(816);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, phi);
		JlNativeApi.StoreD(proc, 3, length1);
		JlNativeApi.StoreD(proc, 4, length2);
		JlNativeApi.StoreI(proc, 5, width);
		JlNativeApi.StoreI(proc, 6, height);
		JlNativeApi.StoreS(proc, 7, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeMeasure();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>
	///   ISerializable 反序列化专用构造（供格式化器回调，带 EditorBrowsable(Never)）：先落基类 UNDEF 空壳，再从 info 取出 GetObjectData 以 SerializeMeasure（id 799）写入的键 "data" 的 byte[]，走与 DeserializeMeasure（id 800）相同的通道——先 Dispose 后 Load，把新建的原生卡尺句柄原地装进本实例；"data" 条目缺失或类型不符在 GetValue 处抛，数据非法时段句柄已释放、本对象成空壳 [失败后状态待实测]。
	/// </summary>
	/// <param name="info">格式化器回填的序列化数据，本构造只读取其中键 "data"（byte[]，Vision 二进制 serialized_item 格式）。</param>
	/// <param name="context">序列化流上下文，本构造不读取。</param>
	/// <remarks>
	///   <para><b>功能说明</b>方法体一句 DeserializeMeasure((byte[])info.GetValue("data", typeof(byte[])))：字节先包成 JlSerializationBuffer（serialized_item 句柄）再喂原生 id 800，装载结果写回本实例——构造完成即持有新句柄。</para>
	///   <para><b>约束或前提</b>与本类 GetObjectData（同样键名 "data"）配对；反序列化还原出的卡尺几何即字节流当时的形状，改几何请重跑 GenMeasureRectangle2/GenMeasureArc 原地重建。</para>
	///   <para><b>与相邻构造器的取舍</b>手上有流用静态 Deserialize(Stream)（内部即空壳加 DeserializeMeasure，与本构造等价）；内存字节直接 new 空壳后调 DeserializeMeasure；从文件读卡尺在空壳上调 ReadMeasure（id 802）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using (JlMeasure src = new JlMeasure(256.0, 256.0, 0.0, 100.0, 5.0, 512, 512, "nearest_neighbor"))
	///   {
	///       byte[] blob = src.SerializeMeasure();     // id 799：卡尺转字节数组
	///       using JlMeasure dst = new JlMeasure();    // 空壳：与 ISerializable 构造同一起点
	///       dst.DeserializeMeasure(blob);             // id 800：该构造体的同一条原地装载通道
	///       bool loaded = dst.IsInitialized();        // true
	///   }
	///   </code>
	///   <para><b>资源与坑</b>DeserializeMeasure 是原地替换语义：在已有卡尺上重放会先释放旧句柄，想保留原卡尺必须先 new 空壳再装载。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMeasure(SerializationInfo info, StreamingContext context)
	{
		DeserializeMeasure((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把卡尺序列化为 Vision 格式字节并写入传入的 Stream；只读，不改动也不释放本对象句柄，无返回值。</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>把卡尺对象序列化后的字节流写入一个 Stream。内部先调 SerializeMeasure 得到 byte[]，再由 JlSerializationBuffer.WriteToStream 落流。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>stream 须可写。本方法不改动、不释放本对象的句柄，序列化后仍可继续用于 MeasurePos。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>存文件用 WriteMeasure；只要内存字节数组用 SerializeMeasure；跨进程/网络传递用本方法配 JlMeasure.Deserialize。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       caliper.Serialize(ms);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>stream 的生命周期由调用方管理；本类不关闭传入的流。MemoryStream 来自 System.IO。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeMeasure(), stream);
	}

	/// <summary>静态：从 Stream 读出 Vision 格式数据反序列化为卡尺（原生 id 800），返回持有新原生句柄的 JlMeasure，不修改任何既有实例。</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>静态方法：从流中读出序列化数据并反序列化（原生 id 800），返回一个持有新原生句柄的 JlMeasure。不修改任何既有实例。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>流内须是由 Serialize / SerializeMeasure 写出的完整 Vision 格式数据；stream 当前位置须处于数据起点（读回前常需 Position = 0）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure original = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   JlMeasure restored;
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       original.Serialize(ms);
	///       ms.Position = 0;
	///       restored = JlMeasure.Deserialize(ms);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回的实例与原实例各自持有独立句柄，须分别 Dispose。</para>
	/// </remarks>
	public new static JlMeasure Deserialize(Stream stream)
	{
		JlMeasure hMeasure = new JlMeasure();
		hMeasure.DeserializeMeasure(JlSerializationBuffer.ReadFromStream(stream));
		return hMeasure;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>深拷贝当前卡尺，返回持有独立新句柄的副本。</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>深拷贝当前卡尺：走 SerializeMeasure（id 799）→ DeserializeMeasure（id 800）的字节数组往返，返回持有独立新句柄的 JlMeasure。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要一份基参数、随后各自平移多个卡尺：Clone 后分别 TranslateMeasure；参数完全不同则直接 new。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure baseCaliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   JlMeasure second = baseCaliper.Clone();
	///   second.TranslateMeasure(256.0, 320.0);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>ICloneable 显式实现走同一 Clone；两份句柄互不影响，须各自 Dispose。</para>
	/// </remarks>
	public new JlMeasure Clone()
	{
		byte[] data = SerializeMeasure();
		JlMeasure obj = new JlMeasure();
		obj.DeserializeMeasure(data);
		return obj;
	}

	/// <summary>
	///   把卡尺序列化为 Vision 二进制格式的 byte 数组（原生 id 799）；只读，不释放、不更换本对象句柄，返回纯数据字节数组。
	/// </summary>
	/// <returns>序列化条目的句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>将卡尺对象序列化为 Vision 二进制格式的 byte 数组（原生 id 799）。注意签名返回的是 byte[]，英文 returns 注释中的 "Handle of the serialized item" 是底层缓冲的表述，C# 侧拿到的是纯数据。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>只读操作，不释放、不更换本对象句柄，调用后卡尺仍可用。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>落盘用 WriteMeasure；写流用 Serialize；内存缓存/跨进程传字节用本方法，配 DeserializeMeasure 还原。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   byte[] data = caliper.SerializeMeasure();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>DeserializeMeasure 是原地替换句柄（见其注释），若要保留原卡尺，先 new JlMeasure() 再对空壳调 DeserializeMeasure。</para>
	/// </remarks>
	public byte[] SerializeMeasure()
	{
		IntPtr proc = JlNativeApi.PreCall(799);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   从 SerializeMeasure 得到的字节数组还原卡尺（原生 id 800）：先释放本对象旧句柄再装入新句柄，原地替换、不返回新对象，无返回值。
	/// </summary>
	/// <param name="serializedItemHandle">序列化条目的句柄。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>从 SerializeMeasure 得到的字节数组还原卡尺（原生 id 800）。方法体先 Dispose() 释放本对象旧句柄，再把新句柄装入本对象——原地替换，不是返回新对象。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>传入数据必须来自同库的 SerializeMeasure / WriteMeasure，格式校验失败时本对象旧句柄已被释放 [失败后句柄状态待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   byte[] data = caliper.SerializeMeasure();
	///   JlMeasure restored = new JlMeasure();
	///   restored.DeserializeMeasure(data);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>Clone / Deserialize(Stream) / ISerializable 构造都经由本方法完成深拷贝。静态 JlMeasure.Deserialize 是它的"返回新对象"包装。</para>
	/// </remarks>
	public void DeserializeMeasure(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(800);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>
	///   把卡尺以 Vision 二进制格式写入文件（原生 id 801）；不改动本对象句柄，无返回值。
	/// </summary>
	/// <param name="fileName">文件名。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>把卡尺以 Vision 二进制格式写入文件（原生 id 801）。不改动本对象句柄。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>配 ReadMeasure（id 802，原地换句柄）成对使用；内存中转换用 Serialize / Deserialize。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   caliper.WriteMeasure("caliper.mdt");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>文件路径由原生层处理，目录不存在时的错误形态 [待实测]。</para>
	/// </remarks>
	public void WriteMeasure(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(801);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   从文件读取卡尺载入本对象（原生 id 802）：先释放旧句柄再装入文件中的新句柄，原地替换、不返回新对象，无返回值。
	/// </summary>
	/// <param name="fileName">文件名。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>从文件读取卡尺（原生 id 802）。方法体先 Dispose() 旧句柄再装入文件中的新句柄——原地替换本对象内容。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>不想动现有对象时用 JlOperatorSet.ReadMeasure(…, out JlTuple measureHandle) 拿裸句柄，或包装成 JlMeasure(handle)。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure();
	///   caliper.ReadMeasure("caliper.mdt");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>文件不存在时本对象旧句柄已被释放，对象成为空壳 [失败后状态待实测]。</para>
	/// </remarks>
	public void ReadMeasure(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(802);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   沿卡尺扫描方向输出灰度经过 threshold 的亚像素交点行/列坐标及相邻交点间距（原生 id 803，先按 sigma 高斯平滑）；结果经 out 元组返回，无返回值。
	/// </summary>
	/// <param name="image">输入图像。</param>
	/// <param name="sigma">高斯平滑的 sigma 值。默认值：1.0</param>
	/// <param name="threshold">阈值。默认值：128.0</param>
	/// <param name="select">点的筛选方式。默认值："all"</param>
	/// <param name="rowThresh">灰度经过阈值的点的行坐标。</param>
	/// <param name="columnThresh">灰度经过阈值的点的列坐标。</param>
	/// <param name="distance">相邻点之间的距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>沿卡尺扫描方向（矩形长轴 / 弧的径向）找灰度经过 threshold 的点（原生 id 803）：先按 sigma 做高斯平滑，再取一维灰度曲线与阈值的交点，输出亚像素行/列坐标和相邻交点间距。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>MeasurePos 找的是灰度导数峰值（真实边缘，幅值可判强弱）；本算子找的是等值线交点，会被曲线非单调段或阈值落在平台区干扰，一般只在需要"灰度等于某值的轮廓点"（如干涉条纹、渐变带定位）时使用。</para>
	///   <para><b>参数取向</b></para>
	///   <para>threshold 是灰度绝对值（默认 128），不是边缘幅值——与 MeasurePos 的 threshold 含义不同。select 取 "all" / "first" / "last"，"first"/"last" 只留一个交点，输出元组长度为 1 [行为待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   using (JlImage image = new JlImage("byte", 512, 512))
	///   {
	///       caliper.MeasureThresh(image, 1.0, 128.0, "all",
	///           out JlTuple rowThresh, out JlTuple columnThresh, out JlTuple distance);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>三个输出元组等长、按序对应；图像尺寸须与卡尺建模时的 width/height 一致。</para>
	/// </remarks>
	public void MeasureThresh(JlImage image, double sigma, double threshold, string select, out JlTuple rowThresh, out JlTuple columnThresh, out JlTuple distance)
	{
		IntPtr proc = JlNativeApi.PreCall(803);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   删除原生卡尺对象并释放其内存（原生 id 804，对应 close_measure）；只调原生删除、不清空托管侧句柄字段，调用后不得再触碰本对象，无返回值。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>删除原生卡尺对象并释放其内存（原生 id 804，对应底层 close_measure；本库的"清空测量对象"入口就是它和 Dispose，没有独立的 ClearMeasure）。</para>
	///   <para><b>资源与坑</b></para>
	///   <para>方法体只调原生删除，不清空托管侧句柄字段：调用后该 JlMeasure 变量仍指向已删除的对象，再次使用行为未定义；其后若再触发 Dispose，是否二次释放原生句柄 [待实测]。常规释放直接用 Dispose() 或 using，CloseMeasure 仅在需要立即归还原生内存且不再触碰该对象时使用。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   caliper.CloseMeasure(); // 调用后不得再使用该对象
	///   </code>
	/// </remarks>
	public void CloseMeasure()
	{
		IntPtr proc = JlNativeApi.PreCall(804);
		Store(proc, 0);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   沿卡尺提取一维灰度投影曲线（原生 id 805，把宽度方向灰度平均、不做边缘判断），返回按扫描方向排列的 JlTuple 灰度元组。
	/// </summary>
	/// <param name="image">输入图像。</param>
	/// <returns>灰度值轮廓。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>沿卡尺提取一维灰度投影轮廓（原生 id 805）：把沿宽度方向（矩形短轴 / 弧的环带宽）的灰度平均成一条曲线返回。不做任何边缘判断。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只需要边缘坐标时直接用 MeasurePos / MeasurePairs；要自行分析灰度曲线（找拐点、算对比度、判断有无边缘）时先取本算子的曲线。</para>
	///   <para><b>参数取向</b></para>
	///   <para>返回元组按扫描方向排列（矩形为长轴方向、弧为径向由内向外 [方向序待实测]），采样间隔由建模时的插值方式与尺寸决定。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   using (JlImage image = new JlImage("byte", 512, 512))
	///   {
	///       JlTuple profile = caliper.MeasureProjection(image);
	///       int samples = profile.Length;
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>图像尺寸须与建模 width/height 一致；返回的 JlTuple 由调用方 Dispose [元组是否需手动释放待实测]。</para>
	/// </remarks>
	public JlTuple MeasureProjection(JlImage image)
	{
		IntPtr proc = JlNativeApi.PreCall(805);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return tuple;
	}

	/// <summary>
	///   把卡尺的指定模糊隶属函数复位为默认形状（原生 id 806，setType 选复位哪个模糊集）；原地改内部函数表、不更换句柄，无返回值。
	/// </summary>
	/// <param name="setType">模糊集的选择。默认值："contrast"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>把卡尺对象的模糊隶属函数复位为默认形状（原生 id 806）。setType 选择复位哪个模糊集，默认 "contrast"（按边缘对比度评分），其余可用名 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>FuzzyMeasurePos / FuzzyMeasurePairs / FuzzyMeasurePairing 会按隶属度对边缘评分并以 fuzzyThresh 过滤；自定义隶属函数用 JlOperatorSet.SetFuzzyMeasure / SetFuzzyMeasureNormPair（本类上只有复位入口）。不想用模糊评分就改用普通 MeasurePos / MeasurePairs。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   caliper.ResetFuzzyMeasure("contrast");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>原地修改本对象内部函数表，不更换句柄。</para>
	/// </remarks>
	public void ResetFuzzyMeasure(string setType)
	{
		IntPtr proc = JlNativeApi.PreCall(806);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, setType);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}




	/// <summary>
	///   在卡尺内做带模糊评估与配对约束的边缘对提取（原生 id 809）：输出两边缘坐标/带符号幅值、对中心坐标、fuzzyScore 与对内间距；结果经 out 元组返回，无返回值。
	/// </summary>
	/// <param name="image">输入图像。</param>
	/// <param name="sigma">高斯平滑的 sigma 值。默认值：1.0</param>
	/// <param name="ampThresh">最小边缘幅值。默认值：30.0</param>
	/// <param name="fuzzyThresh">最小模糊值。默认值：0.5</param>
	/// <param name="transition">选择边缘对的第一个灰度值过渡方向。默认值："all"</param>
	/// <param name="pairing">配对约束。默认值："no_restriction"</param>
	/// <param name="numPairs">边缘对的数量。默认值：10</param>
	/// <param name="rowEdgeFirst">第一条边缘的行坐标。</param>
	/// <param name="columnEdgeFirst">第一条边缘的列坐标。</param>
	/// <param name="amplitudeFirst">第一条边缘的边缘幅值（带符号）。</param>
	/// <param name="rowEdgeSecond">第二条边缘的行坐标。</param>
	/// <param name="columnEdgeSecond">第二条边缘的列坐标。</param>
	/// <param name="amplitudeSecond">第二条边缘的边缘幅值（带符号）。</param>
	/// <param name="rowPairCenter">边缘对中心的行坐标。</param>
	/// <param name="columnPairCenter">边缘对中心的列坐标。</param>
	/// <param name="fuzzyScore">边缘对的模糊评分。</param>
	/// <param name="intraDistance">边缘对内两条边缘之间的距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>带模糊评估的成对边缘提取（原生 id 809）：在卡尺内找边缘对，除幅值门槛外还按隶属函数给每对打分（fuzzyScore），低于 fuzzyThresh 的被丢弃；pairing 施加配对约束，numPairs 限制返回的对数上限。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>比 FuzzyMeasurePairs 多了 pairing 约束与 pair 中心输出、少了 interDistance；比 MeasurePairs 多了 fuzzy 过滤。测线宽/胶宽且杂边多时选本算子。</para>
	///   <para><b>参数取向</b></para>
	///   <para>transition 决定以哪个极性作第一边（对扫描方向的灰度上升/下降，"all" 不限 [具体极性定义待实测]）；pairing 除默认 "no_restriction" 外可选值 [待实测]；numPairs 与 10 个输出元组的长度对应——每个元组都是"每对一项"。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   using (JlImage image = new JlImage("byte", 512, 512))
	///   {
	///       caliper.FuzzyMeasurePairing(image, 1.0, 30.0, 0.5, "all", "no_restriction", 10,
	///           out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst,
	///           out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond,
	///           out JlTuple rowPairCenter, out JlTuple columnPairCenter,
	///           out JlTuple fuzzyScore, out JlTuple intraDistance);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>10 个输出元组等长（= 找到的对数），可为 0；未建模（无卡尺句柄）直接调用会报句柄错误 [待实测]。</para>
	/// </remarks>
	public void FuzzyMeasurePairing(JlImage image, double sigma, double ampThresh, double fuzzyThresh, string transition, string pairing, int numPairs, out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst, out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond, out JlTuple rowPairCenter, out JlTuple columnPairCenter, out JlTuple fuzzyScore, out JlTuple intraDistance)
	{
		IntPtr proc = JlNativeApi.PreCall(809);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   在卡尺内做带模糊评分过滤的边缘对提取（原生 id 810）：输出两边缘坐标/带符号幅值、对中心坐标、fuzzyScore 与对内/对间间距；结果经 out 元组返回，无返回值。
	/// </summary>
	/// <param name="image">输入图像。</param>
	/// <param name="sigma">高斯平滑的 sigma 值。默认值：1.0</param>
	/// <param name="ampThresh">最小边缘幅值。默认值：30.0</param>
	/// <param name="fuzzyThresh">最小模糊值。默认值：0.5</param>
	/// <param name="transition">选择边缘对的第一个灰度值过渡方向。默认值："all"</param>
	/// <param name="rowEdgeFirst">第一条边缘点的行坐标。</param>
	/// <param name="columnEdgeFirst">第一条边缘点的列坐标。</param>
	/// <param name="amplitudeFirst">第一条边缘的边缘幅值（带符号）。</param>
	/// <param name="rowEdgeSecond">第二条边缘点的行坐标。</param>
	/// <param name="columnEdgeSecond">第二条边缘点的列坐标。</param>
	/// <param name="amplitudeSecond">第二条边缘的边缘幅值（带符号）。</param>
	/// <param name="rowEdgeCenter">边缘对中心的行坐标。</param>
	/// <param name="columnEdgeCenter">边缘对中心的列坐标。</param>
	/// <param name="fuzzyScore">边缘对的模糊评分。</param>
	/// <param name="intraDistance">边缘对内两条边缘之间的距离。</param>
	/// <param name="interDistance">相邻边缘对之间的距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>带模糊评估的成对边缘提取（原生 id 810）：先按 sigma 平滑、按幅值与 fuzzyThresh 双重过滤边缘，再配成对；输出两边缘坐标/带符号幅值、对中心坐标、fuzzyScore、对内间距 intraDistance 与相邻对间距 interDistance。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>与 MeasurePairs 同构但多了 fuzzy 评分过滤，适合杂边/伪边缘多的场景；需要配对约束或对数上限用 FuzzyMeasurePairing；只要单边缘用 FuzzyMeasurePos。</para>
	///   <para><b>参数取向</b></para>
	///   <para>transition 选对边缘对做筛选的第一边缘极性（"all"/"positive"/"negative"，极性定义 [待实测]）；11 个输出元组等长 = 对数，intraDistance 即线宽/厚度像素值，乘标定比例得物理尺寸。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   using (JlImage image = new JlImage("byte", 512, 512))
	///   {
	///       caliper.FuzzyMeasurePairs(image, 1.0, 30.0, 0.5, "all",
	///           out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst,
	///           out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond,
	///           out JlTuple rowEdgeCenter, out JlTuple columnEdgeCenter,
	///           out JlTuple fuzzyScore, out JlTuple intraDistance, out JlTuple interDistance);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>与单边缘版共享同一套模糊隶属函数（ResetFuzzyMeasure 复位）；interDistance 首元素无相邻对时的取值 [待实测]。</para>
	/// </remarks>
	public void FuzzyMeasurePairs(JlImage image, double sigma, double ampThresh, double fuzzyThresh, string transition, out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst, out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond, out JlTuple rowEdgeCenter, out JlTuple columnEdgeCenter, out JlTuple fuzzyScore, out JlTuple intraDistance, out JlTuple interDistance)
	{
		IntPtr proc = JlNativeApi.PreCall(810);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   沿卡尺扫描以灰度导数峰值提取单条边缘并按模糊隶属度过滤（原生 id 811）：输出亚像素坐标/带符号幅值/fuzzyScore/间距；结果经 out 元组返回，无返回值。
	/// </summary>
	/// <param name="image">输入图像。</param>
	/// <param name="sigma">高斯平滑的 sigma 值。默认值：1.0</param>
	/// <param name="ampThresh">最小边缘幅值。默认值：30.0</param>
	/// <param name="fuzzyThresh">最小模糊值。默认值：0.5</param>
	/// <param name="transition">选择由亮到暗或由暗到亮的边缘。默认值："all"</param>
	/// <param name="rowEdge">边缘点的行坐标。</param>
	/// <param name="columnEdge">边缘点的列坐标。</param>
	/// <param name="amplitude">边缘的边缘幅值（带符号）。</param>
	/// <param name="fuzzyScore">边缘的模糊评分。</param>
	/// <param name="distance">相邻边缘之间的距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>带模糊评估的单边缘提取（原生 id 811）：语义同 MeasurePos（导数峰值找边），另按隶属函数给每条边缘打 fuzzyScore，低于 fuzzyThresh 的被剔除。扫描与输出约定详见 MeasurePos 注释。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>普通场景用 MeasurePos 即可；反光、阴影导致伪边缘时用本算子加 fuzzy 过滤；成对测宽用 FuzzyMeasurePairs。</para>
	///   <para><b>参数取向</b></para>
	///   <para>ampThresh 是最小边缘幅值（不是灰度值）；fuzzyThresh 在 0–1 的隶属度域内过滤（默认 0.5）；transition 极性定义 [待实测]。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   using (JlImage image = new JlImage("byte", 512, 512))
	///   {
	///       caliper.FuzzyMeasurePos(image, 1.0, 30.0, 0.5, "all",
	///           out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude,
	///           out JlTuple fuzzyScore, out JlTuple distance);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>5 个输出元组等长 = 边缘数；fuzzyScore 可用于把最强边缘之外的结果做置信度排序。</para>
	/// </remarks>
	public void FuzzyMeasurePos(JlImage image, double sigma, double ampThresh, double fuzzyThresh, string transition, out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude, out JlTuple fuzzyScore, out JlTuple distance)
	{
		IntPtr proc = JlNativeApi.PreCall(811);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   在卡尺内按 MeasurePos 方式找边再依 transition 极性配对（原生 id 812）：输出两边缘亚像素坐标/带符号幅值与对内/对间间距（像素）；结果经 out 元组返回，无返回值。
	/// </summary>
	/// <param name="image">输入图像。</param>
	/// <param name="sigma">高斯平滑的 sigma 值。默认值：1.0</param>
	/// <param name="threshold">最小边缘幅值。默认值：30.0</param>
	/// <param name="transition">灰度值过渡类型，决定边缘如何组合为边缘对。默认值："all"</param>
	/// <param name="select">边缘对的选择。默认值："all"</param>
	/// <param name="rowEdgeFirst">第一条边缘中心的行坐标。</param>
	/// <param name="columnEdgeFirst">第一条边缘中心的列坐标。</param>
	/// <param name="amplitudeFirst">第一条边缘的边缘幅值（带符号）。</param>
	/// <param name="rowEdgeSecond">第二条边缘中心的行坐标。</param>
	/// <param name="columnEdgeSecond">第二条边缘中心的列坐标。</param>
	/// <param name="amplitudeSecond">第二条边缘的边缘幅值（带符号）。</param>
	/// <param name="intraDistance">边缘对内两条边缘之间的距离。</param>
	/// <param name="interDistance">相邻边缘对之间的距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>在卡尺内提取边缘并配对（原生 id 812）：先按 MeasurePos 的方式找边，再按 transition 规定的极性把边缘两两配成对。输出两条边缘的亚像素坐标与带符号幅值、对内间距 intraDistance（= 宽度/厚度，像素）、相邻对间距 interDistance。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只要单条边用 MeasurePos；本库无 add_measure_pair/成对边界建模算子 [Grep 确认 JlMeasure 无 AddMeasurePair]，卡尺级成对定位就是本算子；需要模糊过滤选 FuzzyMeasurePairs。</para>
	///   <para><b>参数取向</b></para>
	///   <para>threshold 是最小边缘幅值而非灰度值；transition 以扫描方向的灰度变化定义（"positive"/"negative" 何者对应暗→亮 [待实测]），"all" 允许两种起始极性；select 取 "all"/"first"/"last" 控制返回的对数——"first"/"last" 时全部输出元组长度为 1。8 个输出元组等长 = 对数。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   using (JlImage image = new JlImage("byte", 512, 512))
	///   {
	///       caliper.MeasurePairs(image, 1.0, 30.0, "all", "all",
	///           out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst,
	///           out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond,
	///           out JlTuple intraDistance, out JlTuple interDistance);
	///       double lineWithPx = intraDistance.D; // 第一对的宽度
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>图像尺寸须与建模 width/height 一致；幅值带符号，符号表示极性——校验"黑→白→黑"这类结构时按 amplitudeFirst 的符号筛选。</para>
	/// </remarks>
	public void MeasurePairs(JlImage image, double sigma, double threshold, string transition, string select, out JlTuple rowEdgeFirst, out JlTuple columnEdgeFirst, out JlTuple amplitudeFirst, out JlTuple rowEdgeSecond, out JlTuple columnEdgeSecond, out JlTuple amplitudeSecond, out JlTuple intraDistance, out JlTuple interDistance)
	{
		IntPtr proc = JlNativeApi.PreCall(812);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   沿卡尺扫描方向对灰度投影做高斯导数、按过零点提取亚像素直线边缘（原生 id 813）：每条边输出坐标、带符号幅值与与上一条边的间距；结果经 out 元组返回，无返回值。
	/// </summary>
	/// <param name="image">输入图像。</param>
	/// <param name="sigma">高斯平滑的 sigma 值。默认值：1.0</param>
	/// <param name="threshold">最小边缘幅值。默认值：30.0</param>
	/// <param name="transition">由亮到暗或由暗到亮的边缘。默认值："all"</param>
	/// <param name="select">端点的选择。默认值："all"</param>
	/// <param name="rowEdge">边缘中心的行坐标。</param>
	/// <param name="columnEdge">边缘中心的列坐标。</param>
	/// <param name="amplitude">边缘的边缘幅值（带符号）。</param>
	/// <param name="distance">相邻边缘之间的距离。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>1D 卡尺找边的主算子（原生 id 813）：沿扫描方向（矩形长轴 / 弧径向）对灰度投影曲线做高斯导数，返回过零点对应的亚像素边缘。每条边缘给出坐标（垂直于长轴方向的分辨率由导数插值给出）、带符号幅值和与上一条边缘的距离。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>图像尺寸须与卡尺建模时的 width/height 一致；卡尺句柄有效（构造后未被 CloseMeasure）。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要宽度/厚度用 MeasurePairs（成对）；伪边缘多时先 ResetFuzzyMeasure 再走 FuzzyMeasurePos；只看灰度曲线用 MeasureProjection。</para>
	///   <para><b>参数取向</b></para>
	///   <para>threshold 是最小边缘幅值（灰度跳变强度），不是灰度阈值——对比度低就调小它而不是调 MeasureThresh 那套。transition 按扫描方向的极性选边："all" 两种都要，"positive"/"negative" 各取一种（暗→亮的对应关系以现场标定为准 [待实测]）；select 取 "all"/"first"/"last"。本库未暴露 set_measure_param（无 SetMeasureParam，Grep 确认），因此 num_measures 上限与 measure_selection 无法另行配置；输出元组 rowEdge/columnEdge/amplitude/distance 四者等长，长度 = 过滤后边缘数，"first"/"last" 时恒为 1。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   using (JlImage image = new JlImage("byte", 512, 512))
	///   {
	///       caliper.MeasurePos(image, 1.0, 30.0, "all", "first",
	///           out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude, out JlTuple distance);
	///       double edgeColumn = columnEdge.D; // 最强/最早一条边缘的列坐标
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>找不到边缘时返回空元组（长度为 0）而非报错 [待实测]；distance 首元素表示第一条边缘到卡尺起点的距离还是无效值 [待实测]。</para>
	/// </remarks>
	public void MeasurePos(JlImage image, double sigma, double threshold, string transition, string select, out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude, out JlTuple distance)
	{
		IntPtr proc = JlNativeApi.PreCall(813);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   把卡尺整体平移到新参考点 (row, column)（原生 id 814，图像坐标非偏移量）：矩形改中心、弧改圆心，其余参数不变；原地修改、不更换句柄，无返回值。
	/// </summary>
	/// <param name="row">新参考点的行坐标。默认值：50.0</param>
	/// <param name="column">新参考点的列坐标。默认值：100.0</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>把卡尺整体平移到新参考点 (row, column)（原生 id 814）：矩形卡尺改中心、圆弧卡尺改圆心，长度/宽度/角度/环带等其余参数不变。原地修改本对象的测量对象，不更换句柄、不返回新对象。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>跟随定位结果每帧搬卡尺：本算子最省，不需要重新给全部建模参数；要改角度或尺寸就得 GenMeasureRectangle2 / GenMeasureArc 重建。多个位姿共享同一套参数：Clone 出多份后各自 Translate。</para>
	///   <para><b>参数取向</b></para>
	///   <para>row/column 是图像坐标（新参考点），不是偏移量 [绝对性由原生语义决定，若需相对平移以实测为准]。double 重载与本重载同 id：本重载 Store+UnpinTuple 透传 JlTuple，double 重载走 StoreD。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   JlTuple newRow = 256.0;
	///   JlTuple newColumn = 320.0;
	///   caliper.TranslateMeasure(newRow, newColumn);
	///   using (JlImage image = new JlImage("byte", 512, 512))
	///   {
	///       caliper.MeasurePos(image, 1.0, 30.0, "all", "all",
	///           out JlTuple rowEdge, out JlTuple columnEdge, out JlTuple amplitude, out JlTuple distance);
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>平移后卡尺区域可能部分越出图像边界，越界处的采样行为 [待实测]。</para>
	/// </remarks>
	public void TranslateMeasure(JlTuple row, JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(814);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, column);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   TranslateMeasure 的 double 重载：与 JlTuple 重载同原生 id 814，原地把卡尺平移到新参考点 (row, column)、不更换句柄，无返回值。
	/// </summary>
	/// <param name="row">新参考点的行坐标。默认值：50.0</param>
	/// <param name="column">新参考点的列坐标。默认值：100.0</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>TranslateMeasure 的 double 重载：语义与 JlTuple 重载完全一致（同原生 id 814，原地改参考点），差异仅在标量参数经 StoreD 临时建元组、无需 UnpinTuple。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   caliper.TranslateMeasure(256.0, 320.0);
	///   </code>
	/// </remarks>
	public void TranslateMeasure(double row, double column)
	{
		IntPtr proc = JlNativeApi.PreCall(814);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   在既有 JlMeasure 对象上重建圆弧环带卡尺（原生 id 815，与构造器同一算子）：先 Dispose 旧句柄再装入新句柄，原地换柄，无返回值。
	/// </summary>
	/// <param name="centerRow">圆弧中心的行坐标。默认值：100.0</param>
	/// <param name="centerCol">圆弧中心的列坐标。默认值：100.0</param>
	/// <param name="radius">圆弧的半径。默认值：50.0</param>
	/// <param name="angleStart">弧的起始角（弧度）。默认值：0.0</param>
	/// <param name="angleExtent">弧的角度范围（弧度）。默认值：6.28318</param>
	/// <param name="annulusRadius">环带的半径（半宽）。默认值：10.0</param>
	/// <param name="width">后续待处理图像的宽度。默认值：512</param>
	/// <param name="height">后续待处理图像的高度。默认值：512</param>
	/// <param name="interpolation">使用的插值方式。默认值："nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>在既有 JlMeasure 对象上重建圆弧卡尺（原生 id 815，与构造器同一算子）。方法体先 Dispose() 旧句柄再 Load 新句柄入本对象——原地换柄，几何约定见构造器注释。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>JlTuple 与 double 重载同 id：本重载透传已建好的 JlTuple（Store + UnpinTuple），double 重载走 StoreD。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure ring = new JlMeasure();
	///   JlTuple centerRow = 256.0;
	///   JlTuple centerCol = 256.0;
	///   JlTuple radius = 120.0;
	///   JlTuple angleStart = 0.0;
	///   JlTuple angleExtent = 6.28318;
	///   JlTuple annulusRadius = 15.0;
	///   ring.GenMeasureArc(centerRow, centerCol, radius, angleStart, angleExtent, annulusRadius, 512, 512, "nearest_neighbor");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>调用即丢弃原卡尺配置；若同一原生句柄还被其它 JlMeasure 变量引用（如 JlMeasure(handle) 包装所得），旧句柄被释放后的共享行为 [待实测]。</para>
	/// </remarks>
	public void GenMeasureArc(JlTuple centerRow, JlTuple centerCol, JlTuple radius, JlTuple angleStart, JlTuple angleExtent, JlTuple annulusRadius, int width, int height, string interpolation)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(815);
		JlNativeApi.Store(proc, 0, centerRow);
		JlNativeApi.Store(proc, 1, centerCol);
		JlNativeApi.Store(proc, 2, radius);
		JlNativeApi.Store(proc, 3, angleStart);
		JlNativeApi.Store(proc, 4, angleExtent);
		JlNativeApi.Store(proc, 5, annulusRadius);
		JlNativeApi.StoreI(proc, 6, width);
		JlNativeApi.StoreI(proc, 7, height);
		JlNativeApi.StoreS(proc, 8, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(centerRow);
		JlNativeApi.UnpinTuple(centerCol);
		JlNativeApi.UnpinTuple(radius);
		JlNativeApi.UnpinTuple(angleStart);
		JlNativeApi.UnpinTuple(angleExtent);
		JlNativeApi.UnpinTuple(annulusRadius);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   GenMeasureArc 的 double 重载：与 JlTuple 重载同原生 id 815，先 Dispose 旧句柄再原地装入新圆弧卡尺，无返回值。
	/// </summary>
	/// <param name="centerRow">圆弧中心的行坐标。默认值：100.0</param>
	/// <param name="centerCol">圆弧中心的列坐标。默认值：100.0</param>
	/// <param name="radius">圆弧的半径。默认值：50.0</param>
	/// <param name="angleStart">弧的起始角（弧度）。默认值：0.0</param>
	/// <param name="angleExtent">弧的角度范围（弧度）。默认值：6.28318</param>
	/// <param name="annulusRadius">环带的半径（半宽）。默认值：10.0</param>
	/// <param name="width">后续待处理图像的宽度。默认值：512</param>
	/// <param name="height">后续待处理图像的高度。默认值：512</param>
	/// <param name="interpolation">使用的插值方式。默认值："nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>GenMeasureArc 的 double 重载：与 JlTuple 重载同原生 id 815，同样先 Dispose() 旧句柄再原地装入新圆弧卡尺。差异仅在标量参数经 StoreD 临时建元组。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure ring = new JlMeasure();
	///   ring.GenMeasureArc(256.0, 256.0, 120.0, 0.0, 6.28318, 15.0, 512, 512, "nearest_neighbor");
	///   </code>
	/// </remarks>
	public void GenMeasureArc(double centerRow, double centerCol, double radius, double angleStart, double angleExtent, double annulusRadius, int width, int height, string interpolation)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(815);
		JlNativeApi.StoreD(proc, 0, centerRow);
		JlNativeApi.StoreD(proc, 1, centerCol);
		JlNativeApi.StoreD(proc, 2, radius);
		JlNativeApi.StoreD(proc, 3, angleStart);
		JlNativeApi.StoreD(proc, 4, angleExtent);
		JlNativeApi.StoreD(proc, 5, annulusRadius);
		JlNativeApi.StoreI(proc, 6, width);
		JlNativeApi.StoreI(proc, 7, height);
		JlNativeApi.StoreS(proc, 8, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   在既有 JlMeasure 对象上重建矩形卡尺（原生 id 816，与构造器同一算子）：先 Dispose 旧句柄再装入新句柄，原地换柄，无返回值。
	/// </summary>
	/// <param name="row">矩形中心的行坐标。默认值：300.0</param>
	/// <param name="column">矩形中心的列坐标。默认值：200.0</param>
	/// <param name="phi">矩形长轴相对水平方向的角度（弧度）。默认值：0.0</param>
	/// <param name="length1">矩形的半宽。默认值：100.0</param>
	/// <param name="length2">矩形的半高。默认值：20.0</param>
	/// <param name="width">后续待处理图像的宽度。默认值：512</param>
	/// <param name="height">后续待处理图像的高度。默认值：512</param>
	/// <param name="interpolation">使用的插值方式。默认值："nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>在既有 JlMeasure 对象上重建矩形卡尺（原生 id 816，与构造器同一算子）。方法体先 Dispose() 旧句柄再 Load 新句柄入本对象——原地换柄；长轴=扫描方向、length1/length2 约定见构造器注释。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只挪位置用 TranslateMeasure；本算子用于中心+角度+尺寸任一都要换的重建模。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure();
	///   JlTuple row = 256.0;
	///   JlTuple column = 128.0;
	///   JlTuple phi = 1.5708;
	///   JlTuple length1 = 60.0;
	///   JlTuple length2 = 20.0;
	///   caliper.GenMeasureRectangle2(row, column, phi, length1, length2, 512, 512, "nearest_neighbor");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>JlTuple 与 double 重载同 id，仅传参路径不同（Store+UnpinTuple 对 StoreD）；调用即丢弃原配置，共享旧句柄的其它变量受影响 [待实测]。</para>
	/// </remarks>
	public void GenMeasureRectangle2(JlTuple row, JlTuple column, JlTuple phi, JlTuple length1, JlTuple length2, int width, int height, string interpolation)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(816);
		JlNativeApi.Store(proc, 0, row);
		JlNativeApi.Store(proc, 1, column);
		JlNativeApi.Store(proc, 2, phi);
		JlNativeApi.Store(proc, 3, length1);
		JlNativeApi.Store(proc, 4, length2);
		JlNativeApi.StoreI(proc, 5, width);
		JlNativeApi.StoreI(proc, 6, height);
		JlNativeApi.StoreS(proc, 7, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(length1);
		JlNativeApi.UnpinTuple(length2);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   GenMeasureRectangle2 的 double 重载：与 JlTuple 重载同原生 id 816，先 Dispose 旧句柄再原地装入新矩形卡尺，无返回值。
	/// </summary>
	/// <param name="row">矩形中心的行坐标。默认值：300.0</param>
	/// <param name="column">矩形中心的列坐标。默认值：200.0</param>
	/// <param name="phi">矩形长轴相对水平方向的角度（弧度）。默认值：0.0</param>
	/// <param name="length1">矩形的半宽。默认值：100.0</param>
	/// <param name="length2">矩形的半高。默认值：20.0</param>
	/// <param name="width">后续待处理图像的宽度。默认值：512</param>
	/// <param name="height">后续待处理图像的高度。默认值：512</param>
	/// <param name="interpolation">使用的插值方式。默认值："nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>GenMeasureRectangle2 的 double 重载：与 JlTuple 重载同原生 id 816，同样先 Dispose() 旧句柄再原地装入新矩形卡尺。差异仅在标量参数经 StoreD 临时建元组。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure();
	///   caliper.GenMeasureRectangle2(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   </code>
	/// </remarks>
	public void GenMeasureRectangle2(double row, double column, double phi, double length1, double length2, int width, int height, string interpolation)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(816);
		JlNativeApi.StoreD(proc, 0, row);
		JlNativeApi.StoreD(proc, 1, column);
		JlNativeApi.StoreD(proc, 2, phi);
		JlNativeApi.StoreD(proc, 3, length1);
		JlNativeApi.StoreD(proc, 4, length2);
		JlNativeApi.StoreI(proc, 5, width);
		JlNativeApi.StoreI(proc, 6, height);
		JlNativeApi.StoreS(proc, 7, interpolation);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   按参数名数组批量读取卡尺对象的参数/属性值（原生 id 2153，只读、不改动句柄），返回值顺序与传入名对应。
	/// </summary>
	/// <param name="genParamName">要返回的参数名称。默认值："type"</param>
	/// <returns>参数值。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>读取卡尺对象的参数/属性（原生 id 2153），只读、不改动句柄。常用名（沿用底层惯例，取值集合 [待实测]）："type" 返回卡尺类型（rectangle2 / arc）、"num_measures"、"measure_len_1" / "measure_len_2"、"measure_phi"、"measure_row" / "measure_column" 等。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>本类无 SetMeasureParam：查询到的 num_measures / measure_threshold / measure_selection 之类只能读不能改，需要改就重建卡尺。</para>
	///   <para><b>参数取向</b></para>
	///   <para>JlTuple 重载可按名数组批量取多个参数，返回值顺序与传入名对应 [批量语义待实测]；单名用 string 重载（同 id，StoreS 传参）。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   JlTuple names = new string[] { "type", "measure_len_1" };
	///   JlTuple values = caliper.GetMeasureParam(names);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>对空句柄对象（默认构造的 UNDEF 壳）调用会出错 [错误形态待实测]。</para>
	/// </remarks>
	public JlTuple GetMeasureParam(JlTuple genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(2153);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   GetMeasureParam 的 string 重载：按单个参数名读取卡尺的参数/属性值（原生 id 2153，StoreS 传参，只读、不改句柄），返回该参数的值。
	/// </summary>
	/// <param name="genParamName">要返回的参数名称。默认值："type"</param>
	/// <returns>参数值。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>GetMeasureParam 的 string 重载：同原生 id 2153，取单个参数名（StoreS 传参）；参数名清单与只读语义见 JlTuple 重载注释。</para>
	///   <para><b>可编译用例</b></para>
	///   <code>
	///   JlMeasure caliper = new JlMeasure(256.0, 128.0, 0.0, 60.0, 20.0, 512, 512, "nearest_neighbor");
	///   JlTuple type = caliper.GetMeasureParam("type");
	///   </code>
	/// </remarks>
	public JlTuple GetMeasureParam(string genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(2153);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}
}
