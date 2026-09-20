using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>表示一个计量模型实例（封装原生 metrology model 句柄）。</summary>
[Serializable]
public class JlMetrologyModel : JlHandle, ISerializable, ICloneable
{
	/// <summary>包已有原生计量模型句柄→引用计数拷贝（CopyObject），非深拷贝；断言对象类 metrology_model。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMetrologyModel(IntPtr handle)
		: base(handle)
	{
		AssertSemType();
	}

	/// <summary>包装已有原生 JlHandle，并校验其语义类型为计量模型（metrology_model）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMetrologyModel(JlHandle handle)
		: base(handle)
	{
		AssertSemType();
	}

	private void AssertSemType()
	{
		AssertSemType("metrology_model");
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlMetrologyModel obj)
	{
		obj = new JlMetrologyModel(JlHandleBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlMetrologyModel[] obj)
	{
		err = JlTuple.LoadNew(proc, parIndex, err, out var tuple);
		obj = new JlMetrologyModel[tuple.Length];
		for (int i = 0; i < tuple.Length; i++)
		{
			obj[i] = new JlMetrologyModel(tuple[i].H);
		}
		tuple.Dispose();
		return err;
	}

	/// <summary>
	///   读入一个模型文件并构造成计量模型，构造完即可直接 Apply 使用。
	/// </summary>
	/// <param name="fileName">模型文件路径，须为本库 WriteMetrologyModel 写出的格式。</param>
	/// <remarks>
	///   <para><b>功能说明</b>调用原生算子 id 777（与 <see cref="ReadMetrologyModel"/> 是同一个算子）：StoreS 把 fileName 写入原生参数槽 0，InitOCT 预置输出槽 0，调用后经 Load 把得到的句柄装进本实例（新构造的壳句柄为空，满足 Load 对"必须先为 UNDEF"的要求）。</para>
	///   <para><b>约束或前提</b>文件必须存在且为合法的模型文件；不存在或格式错误时原生调用报错、由 PostCall 善后。读入的是写文件时刻的模型参数；此前的测量结果是否随文件恢复 [待实测]。</para>
	///   <para><b>与相邻成员的取舍</b>只要空模型时用无参构造器；已有实例换文件用 ReadMetrologyModel（它会先释放旧句柄）。本构造器不清理任何东西，拿它覆盖"已加载"的场景不存在——新建壳总是空的。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlTuple indices = model.GetMetrologyObjectIndices();   // 确认文件里有哪些测量对象
	///   indices.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>实例持有原生句柄，用完 Dispose。不要用 ClearMetrologyModel 代替 Dispose：它只调原生清除，托管侧句柄值不复位，后续再用该实例行为未定义。</para>
	/// </remarks>
	public JlMetrologyModel(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(777);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   创建一个不含任何测量对象的空计量模型，需再 Add* 加入几何对象后才能测量。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>调用原生算子 id 798（与 <see cref="CreateMetrologyModel"/> 同一算子）：无输入参数，InitOCT 预置输出槽 0，Load 把新句柄装入本实例。此刻模型里对象数为 0，GetMetrologyObjectIndices 返回空元组。</para>
	///   <para><b>与相邻成员的取舍</b>CreateMetrologyModel 用于"已有实例想推倒重来"（它先 Dispose 旧句柄再走同一算子）；本构造器只适合新建壳。从磁盘恢复用 <see cref="string"/> 参数构造器或 ReadMetrologyModel。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   int idx = model.AddMetrologyObjectCircleMeasure(
	///       100.0, 120.0, 40.0, 20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的壳持有原生句柄，最终须 Dispose；Add* 得到的 idx 只是对象序号，不需要单独释放。</para>
	/// </remarks>
	public JlMetrologyModel()
	{
		IntPtr proc = JlNativeApi.PreCall(798);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeMetrologyModel();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>从序列化数据重建计量模型实例（ISerializable 反序列化构造）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMetrologyModel(SerializationInfo info, StreamingContext context)
	{
		DeserializeMetrologyModel((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把模型序列化并写入流，用于内存/网络/仓库间传递模型。</summary>
	/// <param name="stream">可写的目标流；方法内不关闭该流，开关由调用方负责。</param>
	/// <remarks>
	///   <para><b>功能说明</b>实现为两步：先调 <see cref="SerializeMetrologyModel"/>（原生 id 774）得到字节数组，再经 JlSerializationBuffer.WriteToStream 整体写入流。用 new 关键字隐藏了 JlHandle 基类的同名成员，序列化走本类型专属的原生通道。</para>
	///   <para><b>与相邻成员的取舍</b>落盘存档用 WriteMetrologyModel（原生写文件，格式与本流格式是否互通 [待实测]）；要字节数组直接用 SerializeMetrologyModel；只有跨进程/入库需要 Stream 时才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   using System.IO.FileStream fs = new System.IO.FileStream(
	///       "model.bin", System.IO.FileMode.Create);
	///   model.Serialize(fs);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>序列化是快照：之后对模型的修改不会回溯到已写出的流。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeMetrologyModel(), stream);
	}

	/// <summary>从流读回一个模型，返回全新实例。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>实现分三步：先 new JlMetrologyModel()（触发一次原生 id 798 建空模型），再从流读出字节，最后走 DeserializeMetrologyModel（原生 id 773）释放刚建的空句柄并装入流里的模型。因此有一次"建了又扔"的原生往返开销，但对外只返回一个有效实例。</para>
	///   <para><b>约束或前提</b>流内字节必须与 Serialize（本类版本）写出的格式匹配且完整，否则原生反序列化报错；用错基类 JlHandle.Serialize 写出的流能否读本类型 [待实测]。</para>
	///   <para><b>与相邻成员的取舍</b>反序列化到已有实例用 <see cref="DeserializeMetrologyModel(byte[])"/>（原地，省一次建模型）；从文件恢复用字符串构造器。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model;
	///   using (System.IO.FileStream fs = new System.IO.FileStream(
	///       "model.bin", System.IO.FileMode.Open))
	///   {
	///       model = JlMetrologyModel.Deserialize(fs);
	///   }
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是新句柄，调用方负责 Dispose。</para>
	/// </remarks>
	public new static JlMetrologyModel Deserialize(Stream stream)
	{
		JlMetrologyModel hMetrologyModel = new JlMetrologyModel();
		hMetrologyModel.DeserializeMetrologyModel(JlSerializationBuffer.ReadFromStream(stream));
		return hMetrologyModel;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>经序列化往返得到模型的独立深拷贝，返回新实例。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>纯托管组合拳：SerializeMetrologyModel（原生 id 774）取字节 → new JlMetrologyModel（id 798）建空模型 → DeserializeMetrologyModel（id 773）装入。产物与原模型在原生侧不共享，改克隆不会波及原件，反之亦然。</para>
	///   <para><b>与相邻成员的取舍</b>CopyMetrologyModel（id 778）在原生侧整模型复制且只返回 int 序号，拿到的不是可直接调用的对象；Clone 直接返回可用的 JlMetrologyModel。只复制部分对象用 CopyMetrologyObject（id 779）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlMetrologyModel backup = model.Clone();
	///   backup.ClearMetrologyObject("all");   // 只清克隆里的对象，model 不受影响
	///   backup.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须 Dispose；大模型走两次原生序列化，频繁 Clone 有可观开销。ICloneable.Clone 的接口实现转调本方法。</para>
	/// </remarks>
	public new JlMetrologyModel Clone()
	{
		byte[] data = SerializeMetrologyModel();
		JlMetrologyModel obj = new JlMetrologyModel();
		obj.DeserializeMetrologyModel(data);
		return obj;
	}



	/// <summary>
	///   取某个测量对象拟合结果的离散轮廓（XLD），按指定步长重采样后返回新句柄。
	/// </summary>
	/// <param name="index">测量对象序号。Default: 0</param>
	/// <param name="instance">实例号；"all" 表示该对象的全部实例。Default: "all"</param>
	/// <param name="resolution">轮廓上相邻两点间的距离，单位像素；越小点越密。Default: 1.5</param>
	/// <returns>结果轮廓，JlXLDCont 新句柄（LoadNew 自输出槽 1 装载），用完须 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 768；参数槽 0=this、1=index、2=instance、3=resolution（StoreD 直写）。元组重载走 Store+调用后 UnpinTuple（把传入元组钉给原生），标量重载则 StoreI/StoreS 直写、无钉固定开销。</para>
	///   <para><b>约束或前提</b>轮廓来自最近一次 ApplyMetrologyModel 的拟合结果；尚未 Apply 时返回什么 [待实测]。多实例时所有实例的点串在同一轮廓里，需自行按 resolution 推算每段长度 [待实测：实例间是否有分隔标志]。</para>
	///   <para><b>与相邻成员的取舍</b>只要圆心/半径等数值时用 GetMetrologyObjectResult，别拉整条轮廓；要看边缘实际落点用 GetMetrologyObjectMeasures。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlImage img = new JlImage("part.bmp");
	///   model.ApplyMetrologyModel(img);
	///   img.Dispose();
	///   JlXLDCont contour = model.GetMetrologyObjectResultContour(0, "all", 1.5);
	///   contour.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放；传入的 JlTuple 参数在调用结束前不得 Dispose（实现里靠 UnpinTuple 解除钉固定）。</para>
	/// </remarks>
	public JlXLDCont GetMetrologyObjectResultContour(JlTuple index, JlTuple instance, double resolution)
	{
		IntPtr proc = JlNativeApi.PreCall(768);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, instance);
		JlNativeApi.StoreD(proc, 3, resolution);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(instance);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   取某个测量对象拟合结果的离散轮廓（单对象、单实例号直写版）。
	/// </summary>
	/// <param name="index">测量对象序号。Default: 0</param>
	/// <param name="instance">实例号；"all" 表示该对象的全部实例。Default: "all"</param>
	/// <param name="resolution">轮廓相邻两点距离，像素。Default: 1.5</param>
	/// <returns>JlXLDCont 新句柄（输出槽 1），用完须 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同为原生 id 768；差别在装载方式：本重载用 StoreI/StoreS/StoreD 把三个标量直写参数槽 1/2/3，不做元组钉固定（Store+UnpinTuple），单次查询更轻。</para>
	///   <para><b>约束或前提</b>同元组重载：轮廓反映最近一次 ApplyMetrologyModel 的拟合；未 Apply 时行为 [待实测]。</para>
	///   <para><b>与相邻成员的取舍</b>一次查多个对象号/混合实例时改用 JlTuple 重载（index 传数组即可），本重载 index 只能是单个 int。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlImage img = new JlImage("part.bmp");
	///   model.ApplyMetrologyModel(img);
	///   img.Dispose();
	///   JlXLDCont contour = model.GetMetrologyObjectResultContour(0, "0", 0.5);
	///   contour.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回新句柄须释放。</para>
	/// </remarks>
	public JlXLDCont GetMetrologyObjectResultContour(int index, string instance, double resolution)
	{
		IntPtr proc = JlNativeApi.PreCall(768);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, index);
		JlNativeApi.StoreS(proc, 2, instance);
		JlNativeApi.StoreD(proc, 3, resolution);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   对计量模型整体做平移+旋转（原地改写各测量对象的位置），常用于工件定位后把模型"搬"到当前摆放处。
	/// </summary>
	/// <param name="row">对齐后中心的行坐标（y，向下为正）。Default: 0</param>
	/// <param name="column">对齐后中心的列坐标（x，向右为正）。Default: 0</param>
	/// <param name="angle">旋转角。本文件其余几何参数 phi 均标注 [rad]，此处未标注，按弧度对待 [待实测]。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 769；参数槽 0=this、1..3 为 row/column/angle。元组重载 Store+UnpinTuple 钉固定；等价于 TransformMetrologyObject("all", row, column, angle, "absolute") 的便捷写法（对照 id 769 与 775 是否同一语义 [待实测]）。</para>
	///   <para><b>约束或前提</b>原地生效：调用后模型里所有测量对象的测量区位置立即改变，不需要也不会返回新句柄；但已缓存的测量结果不会自动重算，必须重新 ApplyMetrologyModel。</para>
	///   <para><b>与相邻成员的取舍</b>只想移动部分对象用 TransformMetrologyObject（可按 index 挑选、可选 relative 增量模式）；不想动原模型就 Clone 一份再对齐。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   model.AlignMetrologyModel(512.0, 384.0, 0.0);   // 平移到新中心，不旋转
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>多次连续 Align 是"在已移动结果上再移"（absolute 语义作用于当前状态），不是叠加回原位的补偿；坐标系 row=y 向下、column=x 向右。</para>
	/// </remarks>
	public void AlignMetrologyModel(JlTuple row, JlTuple column, JlTuple angle)
	{
		IntPtr proc = JlNativeApi.PreCall(769);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, column);
		JlNativeApi.Store(proc, 3, angle);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(angle);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   计量模型整体平移+旋转的标量直写版（一次只能对齐成一个位姿）。
	/// </summary>
	/// <param name="row">对齐目标行坐标（像素）。Default: 0</param>
	/// <param name="column">对齐目标列坐标（像素）。Default: 0</param>
	/// <param name="angle">旋转角，按本库惯例为弧度 [待实测]。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同走原生 id 769；本重载用 StoreD 直写参数槽 1..3，无元组钉固定/UnpinTuple 开销，适合在线单帧对齐的热路径。</para>
	///   <para><b>约束或前提</b>原地改写模型内全部测量对象；改完必须重新 ApplyMetrologyModel 才生效于结果。对齐量的单位约定同元组重载。</para>
	///   <para><b>与相邻成员的取舍</b>要把不同对象各自平移到不同位置：本重载做不到一次多值，用元组重载或 TransformMetrologyObject。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   double dx = 12.5, dy = -8.0, dphi = 0.02;   // 由定位结果算出的对齐量
	///   model.AlignMetrologyModel(dy, dx, dphi);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本方法不产生新句柄，模型仍由调用方统一 Dispose。</para>
	/// </remarks>
	public void AlignMetrologyModel(double row, double column, double angle)
	{
		IntPtr proc = JlNativeApi.PreCall(769);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		JlNativeApi.StoreD(proc, 3, angle);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   按几何类型名向模型添加测量对象（通用版），返回新对象序号。
	/// </summary>
	/// <param name="shape">几何类型名："circle"、"line"、"rectangle2"、"ellipse" 等。Default: "circle"</param>
	/// <param name="shapeParam">该类型的几何参数串，个数与顺序须与 shape 匹配（如 circle 为 row, column, radius；line 为起点/终点 4 元；rectangle2/ellipse 为中心/角/两半轴 5 元，ellipse 是否再接起止弧角 [待实测]）。</param>
	/// <param name="measureLength1">测量区垂直于轮廓方向半长（像素）。Default: 20.0</param>
	/// <param name="measureLength2">测量区平行于轮廓方向半长（像素）。Default: 5.0</param>
	/// <param name="measureSigma">边缘提取前高斯平滑的 sigma。Default: 1.0</param>
	/// <param name="measureThreshold">识别边缘所需的最小灰度梯度幅值。Default: 30.0</param>
	/// <param name="genParamName">通用参数名数组，与值成对。Default: []</param>
	/// <param name="genParamValue">通用参数值数组。Default: []</param>
	/// <returns>新建测量对象的序号（整数），不是成功/失败布尔。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 770；参数槽 0=this、1..8 依次为 shape/shapeParam/measureLength1/measureLength2/measureSigma/measureThreshold/genParamName/genParamValue。本重载对全部 8 参做 Store+UnpinTuple 钉固定。</para>
	///   <para><b>关键坑</b>返回值经 LoadI 读输出槽 0 的 INTEGER 且只取第一个值：若原生层支持传数组一次批量建多个对象，第 2 个及以后新对象的序号拿不到，必须事后用 GetMetrologyObjectIndices 对比前后集合找回 [待实测：批量行为]。</para>
	///   <para><b>与相邻成员的取舍</b>类型名写错要到原生调用才报错且信息晦涩；circle/line/rectangle2/ellipse 有各自的强类型便捷版 AddMetrologyObjectCircleMeasure 等（id 791..794），编译期即约束参数个数，能用便捷版就别用本方法；本方法的优势只在传通用参数数组做批量。</para>
	///   <para><b>参数取向</b>measureLength1 是"抓取边缘的搜索深度"，太大会抓到相邻假边、太小会漏边；measureThreshold 与图像对比度挂钩，标定后别随意改。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   JlTuple circleParams = new JlTuple(100.0, 120.0, 40.0);   // row, column, radius
	///   int idx = model.AddMetrologyObjectGeneric(
	///       "circle", circleParams, 20.0, 5.0, 1.0, 30.0,
	///       new JlTuple(), new JlTuple());
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>传入的元组在 UnpinTuple 前不得 Dispose；新建对象随模型一起释放，无单独 Clear。</para>
	/// </remarks>
	public int AddMetrologyObjectGeneric(JlTuple shape, JlTuple shapeParam, JlTuple measureLength1, JlTuple measureLength2, JlTuple measureSigma, JlTuple measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(770);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, shape);
		JlNativeApi.Store(proc, 2, shapeParam);
		JlNativeApi.Store(proc, 3, measureLength1);
		JlNativeApi.Store(proc, 4, measureLength2);
		JlNativeApi.Store(proc, 5, measureSigma);
		JlNativeApi.Store(proc, 6, measureThreshold);
		JlNativeApi.Store(proc, 7, genParamName);
		JlNativeApi.Store(proc, 8, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(shape);
		JlNativeApi.UnpinTuple(shapeParam);
		JlNativeApi.UnpinTuple(measureLength1);
		JlNativeApi.UnpinTuple(measureLength2);
		JlNativeApi.UnpinTuple(measureSigma);
		JlNativeApi.UnpinTuple(measureThreshold);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   向模型添加一个测量对象（单个几何、测量参数标量直写版），返回新对象序号。
	/// </summary>
	/// <param name="shape">几何类型名："circle"、"line"、"rectangle2"、"ellipse" 等。Default: "circle"</param>
	/// <param name="shapeParam">该类型的几何参数串（个数与 shape 匹配，见元组重载说明；仍是 JlTuple，不是标量）。</param>
	/// <param name="measureLength1">测量区垂直轮廓半长（像素）。Default: 20.0</param>
	/// <param name="measureLength2">测量区平行轮廓半长（像素）。Default: 5.0</param>
	/// <param name="measureSigma">高斯平滑 sigma。Default: 1.0</param>
	/// <param name="measureThreshold">最小边缘幅值。Default: 30.0</param>
	/// <param name="genParamName">通用参数名。Default: []</param>
	/// <param name="genParamValue">通用参数值。Default: []</param>
	/// <returns>新对象序号（int）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同走原生 id 770：shape 用 StoreS 直写槽 1，measureLength1/2、measureSigma、measureThreshold 用 StoreD 直写槽 3..6，无钉固定；shapeParam 与 genParam 保持元组 Store+UnpinTuple。</para>
	///   <para><b>与相邻成员的取舍</b>四个测量参数在本重载里是 double，想给同一模型的多个对象配不同测量强度，就得逐对象分别调用；几何类型明确时优先用 Circle/Line/Rectangle2/Ellipse 强类型版，编译期防参数错位。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   int idx = model.AddMetrologyObjectGeneric(
	///       "line", new JlTuple(100.0, 50.0, 100.0, 250.0),
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>line 的参数序是 rowBegin, columnBegin, rowEnd, columnEnd（起终点各 2 元，别按 x1y1x2y2 记）；返回值只是序号，重复 Add 同一几何会并存两个对象而非覆盖。</para>
	/// </remarks>
	public int AddMetrologyObjectGeneric(string shape, JlTuple shapeParam, double measureLength1, double measureLength2, double measureSigma, double measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(770);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, shape);
		JlNativeApi.Store(proc, 2, shapeParam);
		JlNativeApi.StoreD(proc, 3, measureLength1);
		JlNativeApi.StoreD(proc, 4, measureLength2);
		JlNativeApi.StoreD(proc, 5, measureSigma);
		JlNativeApi.StoreD(proc, 6, measureThreshold);
		JlNativeApi.Store(proc, 7, genParamName);
		JlNativeApi.Store(proc, 8, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(shapeParam);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   读取作用于整个模型级别的一个通用参数的当前值。
	/// </summary>
	/// <param name="genParamName">参数名，如相机标定参数。Default: "camera_param"</param>
	/// <returns>参数值元组（LoadNew 新建）；纯数值元组可不显式 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 771；StoreS 写参数名到槽 1，InitOCT 预置输出槽 0，结果经 JlTuple.LoadNew 装载。与 SetMetrologyModelParam（id 772）成对，管的是模型级参数（区别于对象级的 Get/SetMetrologyObjectParam）。</para>
	///   <para><b>约束或前提</b>参数名必须属于模型级参数集，未知名字由原生报错；返回元组的元素个数随参数而异 [待实测：各参数名对应布局]。</para>
	///   <para><b>与相邻成员的取舍</b>查对象参数用 GetMetrologyObjectParam，查模糊度参数用 GetMetrologyObjectFuzzyParam；三个"取参数"算子按作用域选，别混用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlTuple size = model.GetMetrologyModelParam("image_size");   // 参数名可用性 [待实测]
	///   size.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>camera_param 之类的标定参数只影响结果换算，不设置不会报错但量纲仍是像素 [待实测]。</para>
	/// </remarks>
	public JlTuple GetMetrologyModelParam(string genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(771);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   设置一个模型级通用参数（值为元组），原地生效。
	/// </summary>
	/// <param name="genParamName">参数名。Default: "camera_param"</param>
	/// <param name="genParamValue">参数值元组，元素个数须与参数要求一致。Default: []</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 772；StoreS 写名字到槽 1、Store 钉固定元组到槽 2（调用后 UnpinTuple），无输出——设置失败靠原生报错发现，C# 侧不会返回失败标志。</para>
	///   <para><b>约束或前提</b>典型用途是给模型配 camera_param 使测量结果由像素换算到物理量：必须在 ApplyMetrologyModel 之前设好才影响本轮结果 [待实测：是否即时生效于已缓存结果]。</para>
	///   <para><b>与相邻成员的取舍</b>值是单个字符串（如开关型参数）时用 string 重载（同 id 772，StoreS 直写、省钉固定）；对象级参数走 SetMetrologyObjectParam。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlTuple cam = new JlTuple(1.0, 0.5, 640.0, 480.0,
	///       512.0, 384.0, 7.0, 7.0, 0.0);   // 元素布局以标定文档为准 [待实测]
	///   model.SetMetrologyModelParam("camera_param", cam);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>genParamValue 在调用返回前不得 Dispose；空元组 [] 通常表示"清除该参数"，语义逐参数而定 [待实测]。</para>
	/// </remarks>
	public void SetMetrologyModelParam(string genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(772);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.Store(proc, 2, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   设置一个模型级通用参数（值为单个字符串），原地生效。
	/// </summary>
	/// <param name="genParamName">参数名。Default: "camera_param"</param>
	/// <param name="genParamValue">参数值字符串。Default: []</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组值重载同走原生 id 772；名字与值都用 StoreS 直写槽 1/2，无元组钉固定/解钉步骤。</para>
	///   <para><b>与相邻成员的取舍</b>值是数值或多数组（如标定参数表）必须用 JlTuple 重载，字符串版只适配取值本身是词的参数（开关、模式名）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   model.SetMetrologyModelParam("select_out_contours", "true");   // 参数名示例 [待实测]
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无输出、无返回，参数名拼错只在原生层报错。</para>
	/// </remarks>
	public void SetMetrologyModelParam(string genParamName, string genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(772);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, genParamName);
		JlNativeApi.StoreS(proc, 2, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用序列化字节替换本实例持有的模型：先释放旧句柄，再把反序列化出的新句柄装入自己。
	/// </summary>
	/// <param name="serializedItemHandle">SerializeMetrologyModel 产出的字节数组（形参名带 Handle 是历史包袱，本类型收的是 byte[]）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 773。实现顺序：Dispose() 把壳清空（JlHandleBase.Load 要求句柄必须是 UNDEF，否则抛 Undisposed handle 异常）→ 用 JlSerializationBuffer 包住字节 Store 到槽 0 → 原生反序列化出句柄 → Load 装回本实例；GC.KeepAlive 保证缓冲与壳在原生调用结束前不被回收。</para>
	///   <para><b>约束或前提</b>调用即毁旧值：执行后本实例不再是原来那份模型，别的变量即使还引用同一个壳也一样被换掉；要留旧模型先 Clone。字节必须来自兼容版本的本类 SerializeMetrologyModel [待实测：跨版本兼容]。</para>
	///   <para><b>与相邻成员的取舍</b>想要"反序列化出一个新对象、不碰现有实例"，用静态 Deserialize(Stream) 或先 new JlMetrologyModel() 再调本方法；从文件加载用 ReadMetrologyModel（同样是先 Dispose 后装载的原地语义）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel source = new JlMetrologyModel("model.dat");
	///   byte[] data = source.SerializeMetrologyModel();
	///   JlMetrologyModel target = new JlMetrologyModel();
	///   target.DeserializeMetrologyModel(data);   // 把字节装进新壳
	///   source.Dispose();
	///   target.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>ISerializable 构造器（反序列化构造）内部就是调本方法；buffer 为 using 释放，但传出的 byte[] 是托管数组无需处理。</para>
	/// </remarks>
	public void DeserializeMetrologyModel(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(773);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>
	///   把当前模型序列化成一整块字节（内存级存档/传输），返回托管数组副本。
	/// </summary>
	/// <returns>模型字节流；是普通 byte[]，不含需要手动释放的非托管资源。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 774；InitOCT 预置输出槽 0，JlSerializationBuffer.LoadBytes 把输出拷贝成 byte[]。Serialize(Stream)、Clone、ISerializable.GetObjectData 全都建立在它之上。</para>
	///   <para><b>与相邻成员的取舍</b>要直接落盘用 WriteMetrologyModel（原生写文件，避免自己管文件流）；要写 Stream 用本类的 Serialize。拿到的 byte[] 可反复喂给 DeserializeMetrologyModel 多次克隆 [待实测：同一段字节可否重复反序列化]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   byte[] data = model.SerializeMetrologyModel();
	///   System.IO.File.WriteAllBytes("backup.bin", data);
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>序列化是时刻快照；模型很大时字节量可观，别在每帧循环里调用。</para>
	/// </remarks>
	public byte[] SerializeMetrologyModel()
	{
		IntPtr proc = JlNativeApi.PreCall(774);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   变换模型内指定测量对象的位姿（平移+旋转），用于逐对象对齐，原地生效。
	/// </summary>
	/// <param name="index">要变换的对象序号或 "all"。Default: "all"</param>
	/// <param name="row">行方向位移或目标行坐标（像素，随 mode 语义）。</param>
	/// <param name="column">列方向位移或目标列坐标（像素，随 mode 语义）。</param>
	/// <param name="phi">旋转角，按本库惯例为弧度 [待实测]。</param>
	/// <param name="mode">"absolute" 直接设为给定位姿，"relative" 在当前位姿上叠加增量 [待实测：两种模式的精确语义]。Default: "absolute"</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 775；参数槽 0=this、1=index、2=row、3=column、4=phi、5=mode。元组重载对 5 个参数逐一 Store 钉固定再 UnpinTuple。可以只对部分对象施加不同变换（各槽传等长数组时逐对象对应 [待实测：广播规则]）。</para>
	///   <para><b>约束或前提</b>只挪对象、不改形状参数；变换后需重新 ApplyMetrologyModel 才有新结果。AlignMetrologyModel（id 769）是整模型版本且没有 mode。</para>
	///   <para><b>与相邻成员的取舍</b>整个模型统一对齐→AlignMetrologyModel；按对象挑着动、或要做增量位移→本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   model.TransformMetrologyObject("all", 0.0, 0.0, 0.1, "relative");
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>多次 relative 会累积漂移，排查"越对齐越歪"先数调用次数；坐标 row=y、column=x。</para>
	/// </remarks>
	public void TransformMetrologyObject(JlTuple index, JlTuple row, JlTuple column, JlTuple phi, JlTuple mode)
	{
		IntPtr proc = JlNativeApi.PreCall(775);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, row);
		JlNativeApi.Store(proc, 3, column);
		JlNativeApi.Store(proc, 4, phi);
		JlNativeApi.Store(proc, 5, mode);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(mode);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   变换指定测量对象位姿的标量直写版（一次一个位姿）。
	/// </summary>
	/// <param name="index">对象序号或 "all"。Default: "all"</param>
	/// <param name="row">行方向平移量/目标行坐标（随 mode）。</param>
	/// <param name="column">列方向平移量/目标列坐标（随 mode）。</param>
	/// <param name="phi">旋转角，弧度 [待实测]。</param>
	/// <param name="mode">变换模式。Default: "absolute"</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同走原生 id 775：index/mode 用 StoreS、row/column/phi 用 StoreD 直写槽 1..4，全部无钉固定开销，适合逐帧在线对齐。</para>
	///   <para><b>与相邻成员的取舍</b>一次要对多个对象给不同量→元组重载；整模型统一动→AlignMetrologyModel。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   model.TransformMetrologyObject("all", 0.0, 0.0, 0.0, "absolute");
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>"absolute" 用给定值覆盖位姿，可拿它把上次 relative 漂移的对象复位 [待实测：absolute 的基准是原始位姿还是当前位姿]。</para>
	/// </remarks>
	public void TransformMetrologyObject(string index, double row, double column, double phi, string mode)
	{
		IntPtr proc = JlNativeApi.PreCall(775);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.StoreD(proc, 2, row);
		JlNativeApi.StoreD(proc, 3, column);
		JlNativeApi.StoreD(proc, 4, phi);
		JlNativeApi.StoreS(proc, 5, mode);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   把模型写到磁盘文件，格式与字符串构造器/ReadMetrologyModel 配套。
	/// </summary>
	/// <param name="fileName">目标文件路径。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 776；StoreS 把路径写到参数槽 1（this 在槽 0），无输出——写盘动作整体由原生完成，托管侧不产生字节缓冲。</para>
	///   <para><b>约束或前提</b>目录必须已存在；目标文件已存在时是覆盖还是报错 [待实测]。模型无句柄（已被 Dispose）时 Store 装载的是 UNDEF，原生行为未定义 [待实测]。</para>
	///   <para><b>与相邻成员的取舍</b>要写任意 Stream/内存缓冲用 SerializeMetrologyModel 或 Serialize(Stream)，与模型文件格式是否互通 [待实测]；读回用 new JlMetrologyModel(fileName)。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.WriteMetrologyModel("calibrated_model.dat");
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>写完即可在别进程用同名构造器读回；文件不含未落盘的测量结果之外的运行态（结果缓存是否保留 [待实测]）。</para>
	/// </remarks>
	public void WriteMetrologyModel(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(776);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   从文件读模型并原地替换本实例：先释放当前句柄，再装载文件内容。
	/// </summary>
	/// <param name="fileName">模型文件路径（WriteMetrologyModel 或本方法此前写出的格式）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 777——与字符串构造器同一算子，但构造器面对空壳，本方法先 Dispose() 清空旧句柄（满足 JlHandleBase.Load 的"必须 UNDEF"前置）再 Load 新句柄。执行后实例还是那个 C# 对象，背后的模型已换。</para>
	///   <para><b>约束或前提</b>旧模型就此丢失，且若别处还持有同一壳的引用（如传参给方法后缓存过），它们看到的也是新模型；需要保留旧模型请先 Clone。</para>
	///   <para><b>与相邻成员的取舍</b>首次加载用 new JlMetrologyModel(fileName)；换文件用本方法；从字节/流恢复用 DeserializeMetrologyModel / Deserialize。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   model.ReadMetrologyModel("model_v2.dat");   // 空壳读入同样合法
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>文件不存在/格式错时旧句柄已被 Dispose，实例处于空壳态，下次使用会因句柄无效报错——读文件前确认存在。</para>
	/// </remarks>
	public void ReadMetrologyModel(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(777);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   在原生侧复制模型（可只复制选定的对象集），返回新模型的句柄值——注意不是 JlMetrologyModel 对象。
	/// </summary>
	/// <param name="index">要复制进新模型的对象序号集合或 "all"。Default: "all"</param>
	/// <returns>LoadI 从输出槽 0 读出的 INTEGER：新模型句柄的数值形态；本包装不会自动给你对象。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 778。元组重载 Store+UnpinTuple；英文文档称返回"被复制模型的句柄"，而实现走 JlNativeApi.LoadI 拿 int——想要 JlMetrologyModel 需自行 new JlMetrologyModel((IntPtr)(long)handleValue)（该构造器会校验语义类型 metrology_model）[待实测：int 与句柄数值可直接互转的封装约定]。</para>
	///   <para><b>约束或前提</b>index 只挑选复制哪些对象，新模型里对象序号与原来一致还是重排 [待实测]。</para>
	///   <para><b>与相邻成员的取舍</b>要现成的托管对象用 Clone（序列化往返深拷贝，慢但返回对象）；只要对象粒度复制用 CopyMetrologyObject；追求原生侧快速复制且愿意自己包句柄才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   int newHandleValue = model.CopyMetrologyModel(new JlTuple("all"));
	///   JlMetrologyModel copy = new JlMetrologyModel((IntPtr)(long)newHandleValue);
	///   copy.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>包装出的 copy 与新句柄都要 Dispose，漏一个即原生泄漏；未经包装直接丢弃返回值会漏句柄 [待实测：是否有其他清理路径]。</para>
	/// </remarks>
	public int CopyMetrologyModel(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(778);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   原生侧复制模型（index 为单个序号字符串或 "all"），返回新模型句柄值。
	/// </summary>
	/// <param name="index">对象序号字符串（如 "0"）或 "all"。Default: "all"</param>
	/// <returns>新模型句柄的 INTEGER 数值，需自行包成 JlMetrologyModel。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同走原生 id 778；本重载 StoreS 直写槽 1，无钉固定。返回形态（int 句柄值而非对象）与坑同元组重载。</para>
	///   <para><b>与相邻成员的取舍</b>注意 index 是 string：想复制序号 0 和 2 两个对象，传 "all" 再删多余的、或改用元组重载传 new JlTuple(0, 2)，本重载一次只能给一个序号词。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   int hv = model.CopyMetrologyModel("all");
	///   JlMetrologyModel copy = new JlMetrologyModel((IntPtr)(long)hv);
	///   copy.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>包装对象与源模型分别 Dispose。</para>
	/// </remarks>
	public int CopyMetrologyModel(string index)
	{
		IntPtr proc = JlNativeApi.PreCall(778);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   在同一模型内复制若干测量对象，返回全部新对象序号的 INTEGER 元组。
	/// </summary>
	/// <param name="index">被复制的对象序号数组或 "all"。Default: "all"</param>
	/// <returns>新对象序号元组（JlTuple.LoadNew 按 INTEGER 装载，槽 0）；用完 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 779。与 CopyMetrologyModel（id 778，造新模型）不同：复制出的对象仍挂在当前模型里，可继续 Apply 一起测。</para>
	///   <para><b>约束或前提</b>新序号通常接在现有最大序号之后，不覆盖旧号 [待实测]；复制体携带原对象的测量参数与位姿。</para>
	///   <para><b>与相邻成员的取舍</b>单个对象也要拿到全部新序号时别用 string 重载——那个版本只回第一个号；跨模型搬运对象则先 CopyMetrologyModel 指定 index。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlTuple newIdx = model.CopyMetrologyObject(new JlTuple(0, 1));   // 复制对象 0 和 1
	///   newIdx.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>删除复制出的对象用 ClearMetrologyObject(新序号)。</para>
	/// </remarks>
	public JlTuple CopyMetrologyObject(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(779);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   在同一模型内复制一个测量对象，返回新对象序号。
	/// </summary>
	/// <param name="index">被复制对象的序号字符串。Default: "all"</param>
	/// <returns>新对象序号（int）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 779 同元组重载；StoreS 直写槽 1。返回经 LoadI 读输出槽 0 的第一个 INTEGER。</para>
	///   <para><b>关键坑</b>index 传 "all" 时原生复制所有对象，但输出是序号数组、LoadI 只取第一个——其余新对象的序号被静默丢弃，事后只能靠 GetMetrologyObjectIndices 差集找回。批量复制务必用元组重载。</para>
	///   <para><b>与相邻成员的取舍</b>复制单个对象、只关心其新序号时用本重载最轻。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   int newIdx = model.CopyMetrologyObject("0");   // 只复制对象 0
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>序号不需要释放；忘删复制体用 ClearMetrologyObject(newIdx)。</para>
	/// </remarks>
	public int CopyMetrologyObject(string index)
	{
		IntPtr proc = JlNativeApi.PreCall(779);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   查询若干测量对象最近一次测量找到的实例个数（逐个返回）。
	/// </summary>
	/// <param name="index">对象序号数组。Default: 0</param>
	/// <returns>与 index 逐元素对应的实例个数元组（输出槽 0，LoadNew）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 780；元组重载 Store+UnpinTuple。数量由对象参数 num_instances（期望上限）与实际找到的边缘共同决定，需先 ApplyMetrologyModel 才有意义。</para>
	///   <para><b>与相邻成员的取舍</b>只查一个对象且想要标量：用 int 重载；但注意 int 重载按 DOUBLE 读并且多对象时只保留第一个值，批量查询必须用本元组版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlImage img = new JlImage("part.bmp");
	///   model.ApplyMetrologyModel(img);
	///   img.Dispose();
	///   JlTuple counts = model.GetMetrologyObjectNumInstances(new JlTuple(0, 1));
	///   counts.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>实例数少于设定值常意味着对比度不足或 measureThreshold 过高，而非崩溃信号。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectNumInstances(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(780);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   查询单个测量对象的实例个数，返回 double。
	/// </summary>
	/// <param name="index">对象序号。Default: 0</param>
	/// <returns>实例个数；经 LoadD 按 DOUBLE 读输出槽 0 的第一个值。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 780 同元组重载，index 用 StoreI 直写槽 1。个数为 0 表示最近一次 Apply 没找到该对象的任何实例。</para>
	///   <para><b>关键坑</b>输出按 DOUBLE 装载（整数计数被转成浮点，做 == 比较时用 doubleValue == 0 之类的写法要留心浮点习惯）；且 LoadD 只读第一个值——原生若因故返回多值，后面的会被静默丢弃。</para>
	///   <para><b>与相邻成员的取舍</b>要一次查多个对象、或希望拿到 INTEGER 原始类型，用 JlTuple 重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlImage img = new JlImage("part.bmp");
	///   model.ApplyMetrologyModel(img);
	///   img.Dispose();
	///   double n = model.GetMetrologyObjectNumInstances(0);
	///   bool found = n > 0;
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本方法必须在 ApplyMetrologyModel 之后调用才有当前帧的数值 [待实测：未 Apply 时返回 0 还是报错]。</para>
	/// </remarks>
	public double GetMetrologyObjectNumInstances(int index)
	{
		IntPtr proc = JlNativeApi.PreCall(780);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, index);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>
	///   取测量对象最近一次 Apply 的拟合结果数值（形状参数/距离等，按 result_type 选择）。
	/// </summary>
	/// <param name="index">对象序号。Default: 0</param>
	/// <param name="instance">实例号或 "all"。Default: "all"</param>
	/// <param name="genParamName">结果类型参数名。Default: "result_type"</param>
	/// <param name="genParamValue">结果类型取值，如 "all_param"、"distance"。Default: "all_param"</param>
	/// <returns>结果值元组（混合布局随 genParamValue 而定，LoadNew 新建）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 781；四参全走 Store+UnpinTuple 钉固定，输出槽 0 装载为未定类型 JlTuple（数值与字符串可能混装 [待实测]）。多实例时按实例顺序平铺成一维，步长随所选结果类型变化 [待实测：各 result_type 的每实例字段数]。</para>
	///   <para><b>约束或前提</b>必须先 ApplyMetrologyModel；结果永远反映"最近一次"，再 Apply 即覆盖，多帧并行取数不安全。</para>
	///   <para><b>与相邻成员的取舍</b>要几何轮廓用 GetMetrologyObjectResultContour；要原始边缘点用 GetMetrologyObjectMeasures；本方法专给数值判据（直径、角度、距离）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlImage img = new JlImage("part.bmp");
	///   model.ApplyMetrologyModel(img);
	///   img.Dispose();
	///   JlTuple all = model.GetMetrologyObjectResult(0, "all", "result_type", "all_param");
	///   all.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组用完 Dispose（含句柄型结果时才有实际释放动作）。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectResult(JlTuple index, JlTuple instance, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(781);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, instance);
		JlNativeApi.Store(proc, 3, genParamName);
		JlNativeApi.Store(proc, 4, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(instance);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   取单个对象（可多实例）的拟合结果数值，index/instance 直写版。
	/// </summary>
	/// <param name="index">对象序号。Default: 0</param>
	/// <param name="instance">实例号或 "all"。Default: "all"</param>
	/// <param name="genParamName">结果类型参数名（仍为元组）。Default: "result_type"</param>
	/// <param name="genParamValue">结果类型取值。Default: "all_param"</param>
	/// <returns>结果值元组，LoadNew 新建。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 781：index 用 StoreI、instance 用 StoreS 直写槽 1/2，genParam 两个仍是 Store+UnpinTuple。适合"每次只取一个对象"的判据循环。</para>
	///   <para><b>与相邻成员的取舍</b>要一次给多个对象号或 instance 传数组，用元组重载；本重载 index 固定单值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlImage img = new JlImage("part.bmp");
	///   model.ApplyMetrologyModel(img);
	///   img.Dispose();
	///   JlTuple dist = model.GetMetrologyObjectResult(
	///       0, "all", new JlTuple("result_type"), new JlTuple("distance"));
	///   dist.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>"distance" 等跨对象结果类型是否支持单对象 index 调用 [待实测]；结果随最近一次 Apply 覆盖。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectResult(int index, string instance, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(781);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, index);
		JlNativeApi.StoreS(proc, 2, instance);
		JlNativeApi.Store(proc, 3, genParamName);
		JlNativeApi.Store(proc, 4, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   取测量区矩形轮廓，同时用 out 参数带出最近一次测量找到的全部边缘点坐标。
	/// </summary>
	/// <param name="index">对象序号或 "all"（元组）。Default: "all"</param>
	/// <param name="transition">边缘极性："all" 全要，"positive"/"negative" 只要亮暗/暗亮跳变之一。Default: "all"</param>
	/// <param name="row">输出：边缘点行坐标数组（DOUBLE，像素）。</param>
	/// <param name="column">输出：边缘点列坐标数组（DOUBLE，像素）。</param>
	/// <returns>测量区的矩形 XLD 轮廓（新句柄，须 Dispose）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 782。装载布局（读实现所得）：XLDCont 从输出槽 1 LoadNew；row 从槽 0 按 DOUBLE LoadNew；column 从槽 1 按 DOUBLE LoadNew——实现里 InitOCT(1) 出现两次且 contour 与 column 共用槽 1，属生成器的固定写法，取数以本方法签名为准 [待实测：三输出与槽位的真实对应]。</para>
	///   <para><b>约束或前提</b>必须先 ApplyMetrologyModel；row/column 是"真正参与拟合的边缘点"，被 measureThreshold/极性过滤掉的候选不出现在这里——排查"为什么没抓到边"就查这两个数组。</para>
	///   <para><b>与相邻成员的取舍</b>要拟合后的数值→GetMetrologyObjectResult；要拟合轮廓→GetMetrologyObjectResultContour；要看测量区摆放与逐点边缘→本方法。</para>
	///   <para><b>参数取向</b>返回的轮廓与 out 数组分开释放：row/column 若含句柄元素才需 Dispose，纯数值可不处理；返回的 JlXLDCont 必须释放。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlImage img = new JlImage("part.bmp");
	///   model.ApplyMetrologyModel(img);
	///   img.Dispose();
	///   JlXLDCont regions = model.GetMetrologyObjectMeasures(
	///       new JlTuple("all"), "all", out JlTuple row, out JlTuple column);
	///   regions.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>out 实参必须写 out；多对象时 row/column 是全部对象边缘点的拼接，靠长度对不回单个对象 [待实测：是否按 index 顺序分段]。</para>
	/// </remarks>
	public JlXLDCont GetMetrologyObjectMeasures(JlTuple index, string transition, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(782);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.StoreS(proc, 2, transition);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   取测量区矩形轮廓+边缘点坐标（index 为单个序号字符串版）。
	/// </summary>
	/// <param name="index">对象序号字符串（如 "0"）或 "all"。Default: "all"</param>
	/// <param name="transition">边缘极性选择。Default: "all"</param>
	/// <param name="row">输出：边缘点行坐标（DOUBLE，像素）。</param>
	/// <param name="column">输出：边缘点列坐标（DOUBLE，像素）。</param>
	/// <returns>测量区矩形轮廓，新句柄须 Dispose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 782 同元组版；index/transition 用 StoreS 直写槽 1/2，输出装载布局（contour 槽 1、row 槽 0、column 槽 1）与槽位疑点同元组版 [待实测]。</para>
	///   <para><b>与相邻成员的取舍</b>一次看多个对象请传 JlTuple 版；本版的 index 虽是 string，语义仍是"一个序号或 all"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlImage img = new JlImage("part.bmp");
	///   model.ApplyMetrologyModel(img);
	///   img.Dispose();
	///   JlXLDCont regions = model.GetMetrologyObjectMeasures("all", "positive",
	///       out JlTuple row, out JlTuple column);
	///   regions.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>out 实参必须带 out；未 Apply 前调用结果不可信 [待实测]。</para>
	/// </remarks>
	public JlXLDCont GetMetrologyObjectMeasures(string index, string transition, out JlTuple row, out JlTuple column)
	{
		IntPtr proc = JlNativeApi.PreCall(782);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.StoreS(proc, 2, transition);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out row);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   对一张图执行模型内全部测量对象的边缘提取与几何拟合，结果缓存在模型内部（无返回值）。
	/// </summary>
	/// <param name="image">输入图像。</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 783；image Store 到槽 1，本算子没有输出参数——所有结果写进模型自身状态，随后由 GetMetrologyObjectResult / ResultContour / Measures / NumInstances 读取。GC.KeepAlive(image) 表明图像句柄在整个原生调用期间不得释放，调用返回后即可安全 Dispose 图像。</para>
	///   <para><b>约束或前提</b>对图像类型/通道数的要求（是否必须单通道灰度 [待实测]）；模型里对象为空时返回"成功"但无任何结果 [待实测]。</para>
	///   <para><b>顺序依赖（核心坑）</b>每次 Apply 覆盖上一轮全部结果：正确节拍是 Apply → 立刻取数 → 再 Apply 下一帧；先取数后 Apply 或用两个线程共享一个模型都会读到错帧。</para>
	///   <para><b>与相邻成员的取舍</b>只想测部分对象：先 ClearMetrologyObject 掉不测的、或 Clone 后裁剪，Apply 没有 index 选择参数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlImage img = new JlImage("part.bmp");
	///   model.ApplyMetrologyModel(img);
	///   img.Dispose();
	///   JlTuple res = model.GetMetrologyObjectResult(0, "all",
	///       new JlTuple("result_type"), new JlTuple("all_param"));
	///   res.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本方法不返回句柄、不产生新对象；耗时随对象数与测量区尺寸线性上涨。</para>
	/// </remarks>
	public void ApplyMetrologyModel(JlImage image)
	{
		IntPtr proc = JlNativeApi.PreCall(783);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
	}

	/// <summary>
	///   列出模型内当前所有测量对象的序号。
	/// </summary>
	/// <returns>INTEGER 型元组（按序号装载，槽 0）；模型为空时长度为 0。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 784，仅 this 一个输入；输出用 JlTuple.LoadNew 强制按 JlTupleType.INTEGER 装载。</para>
	///   <para><b>约束或前提</b>序号是对象的稳定身份证：Add* 递增分配、ClearMetrologyObject 删除后旧号不再出现（中间可留空洞 [待实测]），别把它当"第几个对象"的连续下标用。</para>
	///   <para><b>与相邻成员的取舍</b>想知道每个对象找到多少实例用 GetMetrologyObjectNumInstances；本方法回答"模型里现在有哪些号"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlTuple ids = model.GetMetrologyObjectIndices();
	///   int n = ids.Length;   // 对象个数
	///   ids.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>数值元组不 Dispose 也不至于泄漏非句柄内存 [待实测]，仍建议 Dispose；Length 属性名以 JlTuple 定义为准。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectIndices()
	{
		IntPtr proc = JlNativeApi.PreCall(784);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   把指定对象的全部模糊度参数与隶属函数恢复为默认值（等价清掉 SetMetrologyObjectFuzzyParam 的效果）。
	/// </summary>
	/// <param name="index">对象序号数组或 "all"。Default: "all"</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 785；Store+UnpinTuple。只回滚模糊度一族；测量参数、形状参数不动（那是 ResetMetrologyObjectParam，id 786）。</para>
	///   <para><b>约束或前提</b>原地生效；回滚后需重新 Apply 才影响结果。模糊度参数影响边缘点的取舍权重（fuzzy_thresh 等），默认态即"不启用模糊加权" [待实测]。</para>
	///   <para><b>与相邻成员的取舍</b>想逐个改回而不清零用 SetMetrologyObjectFuzzyParam；全模型彻底回出厂态用 ResetMetrologyObjectParam。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   model.ResetMetrologyObjectFuzzyParam(new JlTuple(0, 1));   // 只回滚对象 0、1
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>index 里写不存在的序号由原生报错 [待实测：是忽略还是抛错]。</para>
	/// </remarks>
	public void ResetMetrologyObjectFuzzyParam(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(785);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   回滚指定对象（单个序号词或 "all"）的模糊度参数，标量直写版。
	/// </summary>
	/// <param name="index">对象序号字符串或 "all"。Default: "all"</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 785 同元组版；StoreS 直写槽 1、无钉固定。语义、作用域（只动模糊度一族）同元组版。</para>
	///   <para><b>与相邻成员的取舍</b>一次回滚多个具体序号用元组版传数组；"all" 回滚全部就用本版最省。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   model.ResetMetrologyObjectFuzzyParam("all");
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>回滚不撤销已缓存的 Apply 结果，记得重测。</para>
	/// </remarks>
	public void ResetMetrologyObjectFuzzyParam(string index)
	{
		IntPtr proc = JlNativeApi.PreCall(785);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   把指定对象的全部参数（测量参数、实例数等，不含模糊度）恢复默认值。
	/// </summary>
	/// <param name="index">对象序号数组或 "all"。Default: "all"</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 786；Store+UnpinTuple。与 ResetMetrologyObjectFuzzyParam（id 785）分治两族参数：本方法回滚 num_instances、measure_threshold 这类常规参数 [待实测：是否也回滚形状参数与位姿]。</para>
	///   <para><b>约束或前提</b>原地生效，需重新 Apply 才反映到结果。</para>
	///   <para><b>与相邻成员的取舍</b>只调个别参数用 SetMetrologyObjectParam；只想清模糊度用 id 785 的 Fuzzy 版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   model.ResetMetrologyObjectParam(new JlTuple(0));
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>无输出、无返回值；序号不存在时行为由原生定 [待实测]。</para>
	/// </remarks>
	public void ResetMetrologyObjectParam(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(786);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   恢复对象常规参数为默认值（单个序号词或 "all"）。
	/// </summary>
	/// <param name="index">对象序号字符串或 "all"。Default: "all"</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 786 同元组版；StoreS 直写槽 1、无钉固定开销。</para>
	///   <para><b>与相邻成员的取舍</b>多序号批量回滚用元组版；回滚范围（哪些参数族归默认）与 Fuzzy 版的分工同元组版说明。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   model.ResetMetrologyObjectParam("all");   // 全部对象回默认
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>回滚后旧 Set 值不可恢复，先 Get 留档再回滚。</para>
	/// </remarks>
	public void ResetMetrologyObjectParam(string index)
	{
		IntPtr proc = JlNativeApi.PreCall(786);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   读取测量对象的模糊度参数当前值。
	/// </summary>
	/// <param name="index">对象序号数组或 "all"。Default: "all"</param>
	/// <param name="genParamName">模糊参数名数组，如 "fuzzy_thresh"。Default: "fuzzy_thresh"</param>
	/// <returns>参数值元组（LoadNew 新建），与所参数名/对象对应。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 787；两参 Store+UnpinTuple 钉固定，输出槽 0。模糊度参数决定边缘点以多大权重参与拟合（低置信边缘不再一票通过）[待实测：具体隶属函数行为]。</para>
	///   <para><b>与相邻成员的取舍</b>常规参数读 GetMetrologyObjectParam（id 788）；写回用 SetMetrologyObjectFuzzyParam（id 789）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlTuple v = model.GetMetrologyObjectFuzzyParam(
	///       new JlTuple("all"), new JlTuple("fuzzy_thresh"));
	///   v.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>未显式 Set 过时读到的是默认值还是空元组 [待实测]。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectFuzzyParam(JlTuple index, JlTuple genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(787);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   读取对象模糊度参数（index 为单个序号词或 "all"）。
	/// </summary>
	/// <param name="index">对象序号字符串或 "all"。Default: "all"</param>
	/// <param name="genParamName">模糊参数名（仍是元组）。Default: "fuzzy_thresh"</param>
	/// <returns>参数值元组，LoadNew 新建。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 787 同元组版；index 用 StoreS 直写槽 1，genParamName 仍 Store+UnpinTuple。</para>
	///   <para><b>与相邻成员的取舍</b>要一次查多个序号的本参数，用元组版；两个"写"版重载的取舍见 SetMetrologyObjectFuzzyParam。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlTuple v = model.GetMetrologyObjectFuzzyParam("all", new JlTuple("fuzzy_thresh"));
	///   v.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>第一个实参给字符串字面量时按重载决议会绑到本版而非元组版，想走元组版须显式 new JlTuple。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectFuzzyParam(string index, JlTuple genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(787);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   读取一个或多个测量对象的常规参数值（对象级，非模型级）。
	/// </summary>
	/// <param name="index">对象序号数组或 "all"。Default: "all"</param>
	/// <param name="genParamName">参数名数组，如 "num_measures"、"measure_length1"。Default: "num_measures"</param>
	/// <returns>参数值元组（LoadNew 新建），逐对象/逐参数平铺。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 788；两参 Store+UnpinTuple，输出槽 0。可一次传多对象名+多参数名，返回按外层对象、内层参数的顺序排列 [待实测：平铺顺序]。</para>
	///   <para><b>与相邻成员的取舍</b>模型级参数（camera_param 等）用 GetMetrologyModelParam（id 771）；模糊度用 id 787；Add* 时给过的 genParam 在此取回。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlTuple n = model.GetMetrologyObjectParam(
	///       new JlTuple("all"), new JlTuple("num_instances"));
	///   n.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>参数名拼错由原生报错；返回元组用完 Dispose。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectParam(JlTuple index, JlTuple genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(788);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   读取对象常规参数值（index 为单个序号词或 "all"，直写版）。
	/// </summary>
	/// <param name="index">对象序号字符串或 "all"。Default: "all"</param>
	/// <param name="genParamName">参数名元组。Default: "num_measures"</param>
	/// <returns>参数值元组，LoadNew 新建。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 788 同元组版；index StoreS 直写槽 1，genParamName 保持 Store+UnpinTuple。</para>
	///   <para><b>与相邻成员的取舍</b>多序号批量查询用元组版；单对象巡检、脚本里传名字时本版够用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   JlTuple v = model.GetMetrologyObjectParam("all", new JlTuple("measure_threshold"));
	///   v.Dispose();
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>字面量 index 会按重载决议绑到本版；返回的 num_measures 等数值反映当前设置而非上次 Apply 实测数。</para>
	/// </remarks>
	public JlTuple GetMetrologyObjectParam(string index, JlTuple genParamName)
	{
		IntPtr proc = JlNativeApi.PreCall(788);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		err = JlTuple.LoadNew(proc, 0, err, out var tuple);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   设置对象的模糊度参数或隶属函数（三参全元组版），原地生效。
	/// </summary>
	/// <param name="index">对象序号数组或 "all"。Default: "all"</param>
	/// <param name="genParamName">模糊参数名数组。Default: "fuzzy_thresh"</param>
	/// <param name="genParamValue">参数值数组，与名成对。Default: 0.5</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 789；index/名/值三参全走 Store+UnpinTuple，槽 1..3，无输出。</para>
	///   <para><b>约束或前提</b>参数名与值个数需匹配；设置影响下一次 Apply 的边缘取舍权重，设完必须重测才见效。</para>
	///   <para><b>与相邻成员的取舍</b>index 是单个词时用 string 重载省钉固定；批量多对象同值设置用本版传数组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   model.SetMetrologyObjectFuzzyParam(
	///       new JlTuple("all"), new JlTuple("fuzzy_thresh"), new JlTuple(0.5));
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>传入元组在调用返回前不得 Dispose；回默认用 ResetMetrologyObjectFuzzyParam。</para>
	/// </remarks>
	public void SetMetrologyObjectFuzzyParam(JlTuple index, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(789);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.Store(proc, 3, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   设置对象模糊度参数（index 为单个序号词或 "all"，直写版）。
	/// </summary>
	/// <param name="index">对象序号字符串或 "all"。Default: "all"</param>
	/// <param name="genParamName">模糊参数名（元组）。Default: "fuzzy_thresh"</param>
	/// <param name="genParamValue">参数值（元组）。Default: 0.5</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 789 同元组版；index 用 StoreS 直写槽 1，名/值仍钉固定。语义与生效时机（下次 Apply）同元组版。</para>
	///   <para><b>与相邻成员的取舍</b>给多个对象设不同值→元组版传等长数组；本版适合"一个对象/全部对象+一组参数"。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   model.SetMetrologyObjectFuzzyParam("all",
	///       new JlTuple("fuzzy_thresh"), new JlTuple(0.5));
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>字面量 "all" 会绑到本版（重载决议），要显式走元组版需 new JlTuple("all")。</para>
	/// </remarks>
	public void SetMetrologyObjectFuzzyParam(string index, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(789);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.Store(proc, 3, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   设置测量对象的常规参数（实例数、测量阈值等），三参全元组版，原地生效。
	/// </summary>
	/// <param name="index">对象序号数组或 "all"。Default: "all"</param>
	/// <param name="genParamName">参数名数组，如 "num_instances"。Default: "num_instances"</param>
	/// <param name="genParamValue">参数值数组。Default: 1</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 790；三参全 Store+UnpinTuple，槽 1..3，无输出。设 num_instances 决定每对象允许找到几个实例（默认 1，设多后 GetMetrologyObjectNumInstances 可能返回大于 1）。</para>
	///   <para><b>约束或前提</b>参数须在 ApplyMetrologyModel 之前设好，本轮结果不会追溯变化；名字与值的配对长度要一致 [待实测：不等长时广播还是报错]。</para>
	///   <para><b>与相邻成员的取舍</b>模糊度用 id 789；模型级参数用 id 772；Add* 的 measure 参数也能在此改，无须删对象重加。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   model.SetMetrologyObjectParam(
	///       new JlTuple("all"), new JlTuple("num_instances"), new JlTuple(3));
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>传入元组在调用返回前不得 Dispose；回默认用 ResetMetrologyObjectParam。</para>
	/// </remarks>
	public void SetMetrologyObjectParam(JlTuple index, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(790);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.Store(proc, 3, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   设置对象常规参数（index 为单个序号词或 "all"，直写版）。
	/// </summary>
	/// <param name="index">对象序号字符串或 "all"。Default: "all"</param>
	/// <param name="genParamName">参数名元组。Default: "num_instances"</param>
	/// <param name="genParamValue">参数值元组。Default: 1</param>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 790 同元组版；index 用 StoreS 直写槽 1，名/值仍 Store+UnpinTuple。</para>
	///   <para><b>与相邻成员的取舍</b>多对象不同值→元组版；单对象/全对象设一组参数→本版。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel("model.dat");
	///   model.SetMetrologyObjectParam("0",
	///       new JlTuple("measure_threshold"), new JlTuple(15.0));
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>设完需重新 ApplyMetrologyModel 才见效；字面量 index 按重载决议绑到本版。</para>
	/// </remarks>
	public void SetMetrologyObjectParam(string index, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(790);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		JlNativeApi.Store(proc, 2, genParamName);
		JlNativeApi.Store(proc, 3, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   向模型添加矩形（rectangle2 表示法）测量对象，几何量以元组传入，返回新对象序号。
	/// </summary>
	/// <param name="row">中心行坐标（y，像素）。</param>
	/// <param name="column">中心列坐标（x，像素）。</param>
	/// <param name="phi">主轴方位角，弧度 [rad]。</param>
	/// <param name="length1">较大半边长（沿主轴方向，像素）。</param>
	/// <param name="length2">较小半边长（垂直主轴方向，像素）。</param>
	/// <param name="measureLength1">测量区域垂直于边界的半长（像素）。Default: 20.0</param>
	/// <param name="measureLength2">测量区域沿边界切向的半长（像素）。Default: 5.0</param>
	/// <param name="measureSigma">高斯平滑 sigma。Default: 1.0</param>
	/// <param name="measureThreshold">最小边缘幅值。Default: 30.0</param>
	/// <param name="genParamName">通用参数名。Default: []</param>
	/// <param name="genParamValue">通用参数值。Default: []</param>
	/// <returns>新对象序号（int）；LoadI 只取输出槽 0 的第一个值，批量添加时第 2 个及以后的序号拿不到 [待实测：数组批量行为]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 791；本重载 11 个入参按声明顺序 Store 钉到槽 1..11（this 在槽 0），调用后逐一 UnpinTuple。四条边各生成 measureLength1/2 定义的测量区。</para>
	///   <para><b>约束或前提</b>phi 为 length1 方向相对列轴的角度，按本库坐标系 row 向下 [待实测：旋转正方向]；length1/length2 或 measureLength 给 0/负值的行为未定义 [待实测]。</para>
	///   <para><b>与相邻成员的取舍</b>单个矩形用下面的 double 重载（StoreD 直写、免钉固定）；本重载适合数组批量加同型矩形；任意多边形轮廓不属于计量模型对象，需另想它法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   int idx = model.AddMetrologyObjectRectangle2Measure(
	///       new JlTuple(200.0), new JlTuple(300.0), new JlTuple(0.0),
	///       new JlTuple(60.0), new JlTuple(40.0),
	///       new JlTuple(20.0), new JlTuple(5.0),
	///       new JlTuple(1.0), new JlTuple(30.0),
	///       new JlTuple(), new JlTuple());
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>Add 后对象立即参与下一次 ApplyMetrologyModel 的全量测量（无"只测新增"选项）。</para>
	/// </remarks>
	public int AddMetrologyObjectRectangle2Measure(JlTuple row, JlTuple column, JlTuple phi, JlTuple length1, JlTuple length2, JlTuple measureLength1, JlTuple measureLength2, JlTuple measureSigma, JlTuple measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(791);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, column);
		JlNativeApi.Store(proc, 3, phi);
		JlNativeApi.Store(proc, 4, length1);
		JlNativeApi.Store(proc, 5, length2);
		JlNativeApi.Store(proc, 6, measureLength1);
		JlNativeApi.Store(proc, 7, measureLength2);
		JlNativeApi.Store(proc, 8, measureSigma);
		JlNativeApi.Store(proc, 9, measureThreshold);
		JlNativeApi.Store(proc, 10, genParamName);
		JlNativeApi.Store(proc, 11, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(length1);
		JlNativeApi.UnpinTuple(length2);
		JlNativeApi.UnpinTuple(measureLength1);
		JlNativeApi.UnpinTuple(measureLength2);
		JlNativeApi.UnpinTuple(measureSigma);
		JlNativeApi.UnpinTuple(measureThreshold);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   添加单个矩形测量对象（几何量 double 直写版），返回新对象序号。
	/// </summary>
	/// <param name="row">中心行坐标（y，像素）。</param>
	/// <param name="column">中心列坐标（x，像素）。</param>
	/// <param name="phi">主轴方位角，弧度 [rad]。</param>
	/// <param name="length1">沿主轴方向的半边长（像素）。</param>
	/// <param name="length2">垂直主轴方向的半边长（像素）。</param>
	/// <param name="measureLength1">测量区域垂直于边界的半长（像素）。Default: 20.0</param>
	/// <param name="measureLength2">测量区域沿边界切向的半长（像素）。Default: 5.0</param>
	/// <param name="measureSigma">高斯平滑 sigma。Default: 1.0</param>
	/// <param name="measureThreshold">最小边缘幅值。Default: 30.0</param>
	/// <param name="genParamName">通用参数名。Default: []</param>
	/// <param name="genParamValue">通用参数值。Default: []</param>
	/// <returns>新对象序号（int）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 791 同元组版；9 个几何/测量量用 StoreD 直写槽 1..9，仅 genParam 两名仍钉固定——单矩形场景省掉 9 次 Store+Unpin。</para>
	///   <para><b>与相邻成员的取舍</b>注意"rectangle2" 是中心+角+两半边长表示；只有左上/右下两角点的矩形需先自行换算中心与边长再调本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   int idx = model.AddMetrologyObjectRectangle2Measure(
	///       200.0, 300.0, 0.0, 60.0, 40.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>11 参个数顺序须与签名一致（this 是第一路输入、不在形参里）；批量加矩形改用元组版。</para>
	/// </remarks>
	public int AddMetrologyObjectRectangle2Measure(double row, double column, double phi, double length1, double length2, double measureLength1, double measureLength2, double measureSigma, double measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(791);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		JlNativeApi.StoreD(proc, 3, phi);
		JlNativeApi.StoreD(proc, 4, length1);
		JlNativeApi.StoreD(proc, 5, length2);
		JlNativeApi.StoreD(proc, 6, measureLength1);
		JlNativeApi.StoreD(proc, 7, measureLength2);
		JlNativeApi.StoreD(proc, 8, measureSigma);
		JlNativeApi.StoreD(proc, 9, measureThreshold);
		JlNativeApi.Store(proc, 10, genParamName);
		JlNativeApi.Store(proc, 11, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   向模型添加线段测量对象（起终点坐标以元组传入），返回新对象序号。
	/// </summary>
	/// <param name="rowBegin">起点行坐标（y，像素）。</param>
	/// <param name="columnBegin">起点列坐标（x，像素）。</param>
	/// <param name="rowEnd">终点行坐标（y，像素）。</param>
	/// <param name="columnEnd">终点列坐标（x，像素）。</param>
	/// <param name="measureLength1">测量区域垂直于边界的半长（像素）。Default: 20.0</param>
	/// <param name="measureLength2">测量区域沿边界切向的半长（像素）。Default: 5.0</param>
	/// <param name="measureSigma">高斯平滑 sigma。Default: 1.0</param>
	/// <param name="measureThreshold">最小边缘幅值。Default: 30.0</param>
	/// <param name="genParamName">通用参数名。Default: []</param>
	/// <param name="genParamValue">通用参数值。Default: []</param>
	/// <returns>新对象序号（LoadI 只读输出槽 0 第一个值，批量添加丢后续序号 [待实测：批量行为]）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 792；10 个入参按声明顺序 Store 钉到槽 1..10 再逐一 UnpinTuple。测量区沿线段等距排布，垂直于线段方向扫边。</para>
	///   <para><b>约束或前提</b>起终点重合（退化线段）行为未定义 [待实测]；线段本身不参与拟合输出——对象拟合的是"一条直线"，起终点只圈定搜索带。</para>
	///   <para><b>与相邻成员的取舍</b>要测两条平行边间距，加两个 Line 对象再用 result_type 的距离项，别塞成一个矩形；单个对象用 double 重载更省。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   int idx = model.AddMetrologyObjectLineMeasure(
	///       new JlTuple(100.0), new JlTuple(50.0), new JlTuple(100.0), new JlTuple(250.0),
	///       new JlTuple(20.0), new JlTuple(5.0),
	///       new JlTuple(1.0), new JlTuple(30.0),
	///       new JlTuple(), new JlTuple());
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>参数序是 row,column,row,column（y 在前 x 在后），与屏幕直觉的 x1y1x2y2 相反。</para>
	/// </remarks>
	public int AddMetrologyObjectLineMeasure(JlTuple rowBegin, JlTuple columnBegin, JlTuple rowEnd, JlTuple columnEnd, JlTuple measureLength1, JlTuple measureLength2, JlTuple measureSigma, JlTuple measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(792);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, rowBegin);
		JlNativeApi.Store(proc, 2, columnBegin);
		JlNativeApi.Store(proc, 3, rowEnd);
		JlNativeApi.Store(proc, 4, columnEnd);
		JlNativeApi.Store(proc, 5, measureLength1);
		JlNativeApi.Store(proc, 6, measureLength2);
		JlNativeApi.Store(proc, 7, measureSigma);
		JlNativeApi.Store(proc, 8, measureThreshold);
		JlNativeApi.Store(proc, 9, genParamName);
		JlNativeApi.Store(proc, 10, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(rowBegin);
		JlNativeApi.UnpinTuple(columnBegin);
		JlNativeApi.UnpinTuple(rowEnd);
		JlNativeApi.UnpinTuple(columnEnd);
		JlNativeApi.UnpinTuple(measureLength1);
		JlNativeApi.UnpinTuple(measureLength2);
		JlNativeApi.UnpinTuple(measureSigma);
		JlNativeApi.UnpinTuple(measureThreshold);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   添加单条线段测量对象（起终点 double 直写版），返回新对象序号。
	/// </summary>
	/// <param name="rowBegin">起点行坐标（y，像素）。</param>
	/// <param name="columnBegin">起点列坐标（x，像素）。</param>
	/// <param name="rowEnd">终点行坐标（y，像素）。</param>
	/// <param name="columnEnd">终点列坐标（x，像素）。</param>
	/// <param name="measureLength1">测量区域垂直于边界的半长（像素）。Default: 20.0</param>
	/// <param name="measureLength2">测量区域沿边界切向的半长（像素）。Default: 5.0</param>
	/// <param name="measureSigma">高斯平滑 sigma。Default: 1.0</param>
	/// <param name="measureThreshold">最小边缘幅值。Default: 30.0</param>
	/// <param name="genParamName">通用参数名。Default: []</param>
	/// <param name="genParamValue">通用参数值。Default: []</param>
	/// <returns>新对象序号（int）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 792 同元组版；8 个量 StoreD 直写槽 1..8，仅 genParam 两参钉固定。</para>
	///   <para><b>与相邻成员的取舍</b>本重载一次一条线；批量加线用元组版；只要边缘点不要拟合直线时应该用 JlMeasure 卡尺族而非计量模型。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMetrologyModel model = new JlMetrologyModel();
	///   int idx = model.AddMetrologyObjectLineMeasure(
	///       100.0, 50.0, 100.0, 250.0,
	///       20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   model.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>10 个实参与签名一一对应（this 不占形参）；参数序 row,column,row,column。</para>
	/// </remarks>
	public int AddMetrologyObjectLineMeasure(double rowBegin, double columnBegin, double rowEnd, double columnEnd, double measureLength1, double measureLength2, double measureSigma, double measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(792);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, rowBegin);
		JlNativeApi.StoreD(proc, 2, columnBegin);
		JlNativeApi.StoreD(proc, 3, rowEnd);
		JlNativeApi.StoreD(proc, 4, columnEnd);
		JlNativeApi.StoreD(proc, 5, measureLength1);
		JlNativeApi.StoreD(proc, 6, measureLength2);
		JlNativeApi.StoreD(proc, 7, measureSigma);
		JlNativeApi.StoreD(proc, 8, measureThreshold);
		JlNativeApi.Store(proc, 9, genParamName);
		JlNativeApi.Store(proc, 10, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Add an ellipse or an elliptic arc to a metrology model.
	/// </summary>
	/// <param name="row">Row (or Y) coordinate of the center of the ellipse.</param>
	/// <param name="column">Column (or X) coordinate of the center of the ellipse.</param>
	/// <param name="phi">Orientation of the main axis [rad].</param>
	/// <param name="radius1">Length of the larger half axis.</param>
	/// <param name="radius2">Length of the smaller half axis.</param>
	/// <param name="measureLength1">测量区域垂直于边界的半长。默认值 20.0</param>
	/// <param name="measureLength2">测量区域沿边界切向的半长。默认值 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">最小边缘幅度。默认值 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Add 椭圆 或 elliptic 圆弧 计量模型。</para>
	///   <para><b>典型场景</b></para>
	///   <para>尺寸检测与边缘定位</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple row = ...;
	///   JlTuple column = ...;
	///   JlTuple phi = ...;
	///   JlTuple radius1 = ...;
	///   JlTuple radius2 = ...;
	///   JlMetrologyModel obj = ...;
	///   var result = obj.AddMetrologyObjectEllipseMeasure(row, column, phi, radius1, radius2, 20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   </code>
	/// </remarks>
	public int AddMetrologyObjectEllipseMeasure(JlTuple row, JlTuple column, JlTuple phi, JlTuple radius1, JlTuple radius2, JlTuple measureLength1, JlTuple measureLength2, JlTuple measureSigma, JlTuple measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(793);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, column);
		JlNativeApi.Store(proc, 3, phi);
		JlNativeApi.Store(proc, 4, radius1);
		JlNativeApi.Store(proc, 5, radius2);
		JlNativeApi.Store(proc, 6, measureLength1);
		JlNativeApi.Store(proc, 7, measureLength2);
		JlNativeApi.Store(proc, 8, measureSigma);
		JlNativeApi.Store(proc, 9, measureThreshold);
		JlNativeApi.Store(proc, 10, genParamName);
		JlNativeApi.Store(proc, 11, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(radius1);
		JlNativeApi.UnpinTuple(radius2);
		JlNativeApi.UnpinTuple(measureLength1);
		JlNativeApi.UnpinTuple(measureLength2);
		JlNativeApi.UnpinTuple(measureSigma);
		JlNativeApi.UnpinTuple(measureThreshold);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Add an ellipse or an elliptic arc to a metrology model.
	/// </summary>
	/// <param name="row">Row (or Y) coordinate of the center of the ellipse.</param>
	/// <param name="column">Column (or X) coordinate of the center of the ellipse.</param>
	/// <param name="phi">Orientation of the main axis [rad].</param>
	/// <param name="radius1">Length of the larger half axis.</param>
	/// <param name="radius2">Length of the smaller half axis.</param>
	/// <param name="measureLength1">测量区域垂直于边界的半长。默认值 20.0</param>
	/// <param name="measureLength2">测量区域沿边界切向的半长。默认值 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">最小边缘幅度。默认值 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Add 椭圆 或 elliptic 圆弧 计量模型。</para>
	///   <para><b>典型场景</b></para>
	///   <para>尺寸检测与边缘定位</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlMetrologyModel obj = ...;
	///   var result = obj.AddMetrologyObjectEllipseMeasure(0.0, 0.0, 0.0, 0.0, 0.0, 20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   </code>
	/// </remarks>
	public int AddMetrologyObjectEllipseMeasure(double row, double column, double phi, double radius1, double radius2, double measureLength1, double measureLength2, double measureSigma, double measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(793);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		JlNativeApi.StoreD(proc, 3, phi);
		JlNativeApi.StoreD(proc, 4, radius1);
		JlNativeApi.StoreD(proc, 5, radius2);
		JlNativeApi.StoreD(proc, 6, measureLength1);
		JlNativeApi.StoreD(proc, 7, measureLength2);
		JlNativeApi.StoreD(proc, 8, measureSigma);
		JlNativeApi.StoreD(proc, 9, measureThreshold);
		JlNativeApi.Store(proc, 10, genParamName);
		JlNativeApi.Store(proc, 11, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Add a circle or a circular arc to a metrology model.
	/// </summary>
	/// <param name="row">圆或圆弧中心的行坐标（即 Y）。</param>
	/// <param name="column">Column (or X) coordinate of the center of the circle or circular arc.</param>
	/// <param name="radius">Radius of the circle or circular arc.</param>
	/// <param name="measureLength1">测量区域垂直于边界的半长。默认值 20.0</param>
	/// <param name="measureLength2">测量区域沿边界切向的半长。默认值 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">最小边缘幅度。默认值 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Add 圆 或 circular 圆弧 计量模型。</para>
	///   <para><b>典型场景</b></para>
	///   <para>尺寸检测与边缘定位</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlTuple row = ...;
	///   JlTuple column = ...;
	///   JlTuple radius = ...;
	///   JlMetrologyModel obj = ...;
	///   var result = obj.AddMetrologyObjectCircleMeasure(row, column, radius, 20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   </code>
	/// </remarks>
	public int AddMetrologyObjectCircleMeasure(JlTuple row, JlTuple column, JlTuple radius, JlTuple measureLength1, JlTuple measureLength2, JlTuple measureSigma, JlTuple measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(794);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, column);
		JlNativeApi.Store(proc, 3, radius);
		JlNativeApi.Store(proc, 4, measureLength1);
		JlNativeApi.Store(proc, 5, measureLength2);
		JlNativeApi.Store(proc, 6, measureSigma);
		JlNativeApi.Store(proc, 7, measureThreshold);
		JlNativeApi.Store(proc, 8, genParamName);
		JlNativeApi.Store(proc, 9, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(column);
		JlNativeApi.UnpinTuple(radius);
		JlNativeApi.UnpinTuple(measureLength1);
		JlNativeApi.UnpinTuple(measureLength2);
		JlNativeApi.UnpinTuple(measureSigma);
		JlNativeApi.UnpinTuple(measureThreshold);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Add a circle or a circular arc to a metrology model.
	/// </summary>
	/// <param name="row">圆或圆弧中心的行坐标（即 Y）。</param>
	/// <param name="column">Column (or X) coordinate of the center of the circle or circular arc.</param>
	/// <param name="radius">Radius of the circle or circular arc.</param>
	/// <param name="measureLength1">测量区域垂直于边界的半长。默认值 20.0</param>
	/// <param name="measureLength2">测量区域沿边界切向的半长。默认值 5.0</param>
	/// <param name="measureSigma">Sigma of the Gaussian function for the smoothing. Default: 1.0</param>
	/// <param name="measureThreshold">最小边缘幅度。默认值 30.0</param>
	/// <param name="genParamName">Names of the generic parameters. Default: []</param>
	/// <param name="genParamValue">Values of the generic parameters. Default: []</param>
	/// <returns>Index of the created metrology object.</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Add 圆 或 circular 圆弧 计量模型。</para>
	///   <para><b>典型场景</b></para>
	///   <para>尺寸检测与边缘定位</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlMetrologyModel obj = ...;
	///   var result = obj.AddMetrologyObjectCircleMeasure(0.0, 0.0, 0.0, 20.0, 5.0, 1.0, 30.0, new JlTuple(), new JlTuple());
	///   </code>
	/// </remarks>
	public int AddMetrologyObjectCircleMeasure(double row, double column, double radius, double measureLength1, double measureLength2, double measureSigma, double measureThreshold, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(794);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		JlNativeApi.StoreD(proc, 3, radius);
		JlNativeApi.StoreD(proc, 4, measureLength1);
		JlNativeApi.StoreD(proc, 5, measureLength2);
		JlNativeApi.StoreD(proc, 6, measureSigma);
		JlNativeApi.StoreD(proc, 7, measureThreshold);
		JlNativeApi.Store(proc, 8, genParamName);
		JlNativeApi.Store(proc, 9, genParamValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   Delete a metrology model and free the allocated memory.
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>释放计量模型。</para>
	///   <para><b>典型场景</b></para>
	///   <para>尺寸检测与边缘定位</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlMetrologyModel obj = ...;
	///   obj.ClearMetrologyModel();
	///   </code>
	/// </remarks>
	public void ClearMetrologyModel()
	{
		IntPtr proc = JlNativeApi.PreCall(795);
		Store(proc, 0);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Delete metrology objects and free the allocated memory.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>释放计量Object。</para>
	///   <para><b>典型场景</b></para>
	///   <para>尺寸检测与边缘定位</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlMetrologyModel obj = ...;
	///   obj.ClearMetrologyObject("all");
	///   </code>
	/// </remarks>
	public void ClearMetrologyObject(JlTuple index)
	{
		IntPtr proc = JlNativeApi.PreCall(796);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, index);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(index);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Delete metrology objects and free the allocated memory.
	/// </summary>
	/// <param name="index">Index of the metrology objects. Default: "all"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>释放计量Object。</para>
	///   <para><b>典型场景</b></para>
	///   <para>尺寸检测与边缘定位</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlMetrologyModel obj = ...;
	///   obj.ClearMetrologyObject("all");
	///   </code>
	/// </remarks>
	public void ClearMetrologyObject(string index)
	{
		IntPtr proc = JlNativeApi.PreCall(796);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, index);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Set the size of the image of metrology objects.
	/// </summary>
	/// <param name="width">Width of the image to be processed. Default: 640</param>
	/// <param name="height">Height of the image to be processed. Default: 480</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>设置 size 图像 metrology objects。</para>
	///   <para><b>典型场景</b></para>
	///   <para>尺寸检测与边缘定位</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlMetrologyModel obj = ...;
	///   obj.SetMetrologyModelImageSize(640, 480);
	///   </code>
	/// </remarks>
	public void SetMetrologyModelImageSize(int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(797);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, width);
		JlNativeApi.StoreI(proc, 2, height);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   Create the data structure that is needed to measure geometric shapes.
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>创建计量模型。</para>
	///   <para><b>典型场景</b></para>
	///   <para>尺寸检测与边缘定位</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   JlMetrologyModel obj = ...;
	///   obj.CreateMetrologyModel();
	///   </code>
	/// </remarks>
	public void CreateMetrologyModel()
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(798);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}
}
