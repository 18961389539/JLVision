using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>表示 HALCON 兼容的 7 元素刚体三维位姿。</summary>
/// <remarks>
///   <para>元素顺序固定为 <c>TransX</c>、<c>TransY</c>、<c>TransZ</c>、<c>RotX</c>、<c>RotY</c>、<c>RotZ</c> 和表示类型码。前三项是平移，接后三项是旋转，最后一项由 <c>OrderOfTransform</c>、<c>OrderOfRotation</c> 和 <c>ViewOfTransform</c> 共同定义。</para>
///   <para>平移单位由应用坐标系决定；HALCON 不把它固定为米。旋转角在欧拉表示中使用度，在 <c>rodriguez</c> 表示中使用 Rodrigues 向量。</para>
///   <para>本类型拥有内部 <see cref="JlTuple"/>，实现 <see cref="IDisposable"/>；返回的新位姿和从 <c>RawData</c> 取得的元组都由调用方负责释放。</para>
/// </remarks>
[Serializable]
public class JlPose : JlData, ISerializable, ICloneable
{
	private const int FIXEDSIZE = 7;

	/// <summary>创建一个空位姿容器，不执行原生调用。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>构造结果的 <c>RawData.Length</c> 为 0；它适合作为 <c>CreatePose</c>、<c>ReadPose</c> 或 <c>DeserializePose</c> 的原地输出容器。</para>
	///   <para><b>约束</b>在写入完整的 7 个元素前，不要把它传给需要有效位姿的算子。数值已知时可直接使用 9 参数构造器，避免额外的一次原生调用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose();
	///   pose.CreatePose(0.1, 0.1, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   </code>
	///   <para><b>资源与坑</b>JlPose 继承 <c>JlData</c> 并实现 <c>IDisposable</c>；空实例的 <c>RawData.Length</c> 为 0，可据此判断是否已被写入。</para>
	/// </remarks>
	public JlPose()
	{
	}

	/// <remarks>
	///   <para><b>功能说明</b>复制一个已有位姿元组，形成独立的托管存储，不执行原生算子。</para>
	///   <para><b>输入要求</b><paramref name="tuple"/>必须包含 7 个元素，并遵循 HALCON 的位姿元素顺序；输入元组与新对象不共享所有权，调用方仍负责释放输入元组。</para>
	///   <para><b>取舍</b>如果手上是 6 个数值和 3 个表示选项，使用 9 参数构造器可由原生运行时生成合法的表示类型码。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose src = new JlPose(0.1, 0.1, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   using JlTuple raw = src.RawData;
	///   JlPose wrapped = new JlPose(raw);
	///   </code>
	///   <para><b>资源与坑</b>JlPose 实现 IDisposable；<c>wrapped</c> 与 <c>src</c> 彼此独立，两个实例分别负责释放自己的元组。</para>
	/// </remarks>
	public JlPose(JlTuple tuple)
		: base(tuple)
	{
	}

	internal JlPose(JlTuple tuple, bool takeOwnership)
		: base(tuple, takeOwnership)
	{
	}

	internal JlPose(JlData data)
		: base(data)
	{
	}

	internal static int LoadNew(IntPtr proc, int parIndex, JlTupleType type, int err, out JlPose obj)
	{
		err = JlTuple.LoadNew(proc, parIndex, err, out var t);
		obj = new JlPose(t, takeOwnership: true);
		return err;
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlPose obj)
	{
		return LoadNew(proc, parIndex, JlTupleType.MIXED, err, out obj);
	}

	internal static JlPose[] SplitArray(JlTuple data)
	{
		int num = data.Length / 7;
		JlPose[] array = new JlPose[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = new JlPose(data.TupleSelectRange(i * 7, (i + 1) * 7 - 1), takeOwnership: true);
		}
		return array;
	}

	/// <summary>
	///   按 9 个分量新建三维位姿并在构造内完成装载（原生 id 1816，结果 <c>Load</c> 写入本对象）：transX/Y/Z 单位米，rotX/Y/Z 是角度（度）还是 Rodriguez 分量由 orderOfRotation 决定，三个串共同规定表示形式。
	/// </summary>
	/// <param name="transX">沿 x 轴的平移量（单位 [m]）。默认值 0.1</param>
	/// <param name="transY">沿 y 轴的平移量（单位 [m]）。默认值 0.1</param>
	/// <param name="transZ">沿 z 轴的平移量（单位 [m]）。默认值 0.1</param>
	/// <param name="rotX">绕 x 轴的旋转角，或 Rodriguez 向量的 x 分量（单位 [ deg] 或无量纲）。默认值 90.0</param>
	/// <param name="rotY">绕 y 轴的旋转角，或 Rodriguez 向量的 y 分量（单位 [ deg] 或无量纲）。默认值 90.0</param>
	/// <param name="rotZ">绕 z 轴的旋转角，或 Rodriguez 向量的 z 分量（单位 [ deg] 或无量纲）。默认值 90.0</param>
	/// <param name="orderOfTransform">旋转与平移的施加次序。默认值 "Rp+T"</param>
	/// <param name="orderOfRotation">旋转值的含义。默认值 "gba"</param>
	/// <param name="viewOfTransform">变换的视角。默认值 "point"</param>
	/// <remarks>
	///   <para><b>功能说明</b>创建并初始化一个位姿；与 <c>CreatePose</c> 使用同一 HALCON <c>create_pose</c> 算子，但本构造器把结果写入新对象，<c>CreatePose</c> 覆写已有对象。</para>
	///   <para><b>表示选项</b><c>orderOfTransform</c> 支持 <c>"Rp+T"</c> 和兼容旧格式的 <c>"R(p-T)"</c>；<c>orderOfRotation</c> 支持 <c>"gba"</c>、<c>"abg"</c> 和 <c>"rodriguez"</c>；<c>viewOfTransform</c> 支持 <c>"point"</c> 和 <c>"coordinate_system"</c>。标准应用建议使用 <c>"Rp+T"</c> + <c>"point"</c>。</para>
	///   <para><b>单位</b>平移单位由场景坐标系决定；欧拉角使用度，<c>rodriguez</c> 使用无量纲 Rodrigues 分量。最后一个元组元素保存表示类型码。</para>
	///   <para><b>与相邻算子的取舍</b>要"由相机位置+注视点"定向用 <c>CreateCamPoseLookAtPoint</c>；要"从二进制/文本还原"用 <c>Deserialize</c>/<c>ReadPose</c>；本构造器只负责"已知 9 个分量"直接成型。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   </code>
	///   <para><b>资源与坑</b>JlPose 实现 IDisposable；示例数值分量一律用 double 字面量，以免与 <c>JlPose(JlTuple)</c> 重载产生 CS0121 二义。</para>
	/// </remarks>
	public JlPose(double transX, double transY, double transZ, double rotX, double rotY, double rotZ, string orderOfTransform, string orderOfRotation, string viewOfTransform)
	{
		IntPtr proc = JlNativeApi.PreCall(1816);
		JlNativeApi.StoreD(proc, 0, transX);
		JlNativeApi.StoreD(proc, 1, transY);
		JlNativeApi.StoreD(proc, 2, transZ);
		JlNativeApi.StoreD(proc, 3, rotX);
		JlNativeApi.StoreD(proc, 4, rotY);
		JlNativeApi.StoreD(proc, 5, rotZ);
		JlNativeApi.StoreS(proc, 6, orderOfTransform);
		JlNativeApi.StoreS(proc, 7, orderOfRotation);
		JlNativeApi.StoreS(proc, 8, viewOfTransform);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>使用 <see cref="JlPoseOptions"/> 创建位姿，避免在调用点散落表示字符串。</summary>
	public JlPose(double transX, double transY, double transZ, double rotX, double rotY, double rotZ, JlPoseOptions options)
		: this(transX, transY, transZ, rotX, rotY, rotZ,
			RequireOptions(options).OrderOfTransform,
			RequireOptions(options).OrderOfRotation,
			RequireOptions(options).ViewOfTransform)
	{
	}

	private static JlPoseOptions RequireOptions(JlPoseOptions options)
	{
		return options ?? throw new ArgumentNullException(nameof(options));
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializePose();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>
	///   .NET 二进制反序列化用的构造器：从 <c>SerializationInfo</c> 的 <c>"data"</c> 键取出位姿负载字节，交 <c>DeserializePose</c>（原生 id 1833）原地填进本实例的 7 个位姿分量。
	/// </summary>
	/// <param name="info">序列化载体，须含键 <c>"data"</c>（<c>byte[]</c>），即本类 <c>ISerializable.GetObjectData</c> 用 <c>SerializePose()</c> 写出的那份负载；键缺失或类型不符抛 <c>SerializationException</c>。</param>
	/// <param name="context">流上下文，本实现完全不用它参与解析。</param>
	/// <remarks>
	///   <para><b>功能说明</b>构造器从 <c>SerializationInfo</c> 的 <c>"data"</c> 键取得二进制负载，并调用 <see cref="DeserializePose(byte[])"/> 初始化新的位姿对象。</para>
	///   <para><b>约束或前提</b>负载必须由本库的 <c>SerializePose()</c> 或 <c>Serialize(Stream)</c> 生成，与 <c>WritePose</c> 的文本格式不可互换。负载中的位姿表示信息会一并恢复；负载无效时构造器抛出异常。</para>
	///   <para><b>与相邻入口的取舍</b>手里是裸流用静态 <c>JlPose.Deserialize(Stream)</c>（返回新实例）；已是 <c>byte[]</c> 且自己管实例时用 <c>new JlPose()</c> + <c>DeserializePose(byte[])</c>；本构造器只在对象图参与 .NET 二进制序列化时被格式化器回调，业务代码一般不直接 new（故标了 <c>EditorBrowsable(Never)</c>）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose src = new JlPose(0.1, 0.1, 0.1, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   byte[] payload = src.SerializePose();   // GetObjectData 写进 "data" 键的就是这份字节
	///   System.Runtime.Serialization.SerializationInfo info = new System.Runtime.Serialization.SerializationInfo(typeof(JlPose), new System.Runtime.Serialization.FormatterConverter());
	///   info.AddValue("data", payload, typeof(byte[]));
	///   JlPose restored = new JlPose(info, default(System.Runtime.Serialization.StreamingContext));
	///   int n = restored.RawData.Length;        // 7 个位姿分量
	///   </code>
	///   <para><b>资源与坑</b>JlPose 系（<c>JlData</c>）实现 <c>IDisposable</c>，使用后应调用 <c>Dispose()</c> 或 <c>using</c>；负载非法时构造中途抛异常，实例停在只有空元组的未初始化态。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlPose(SerializationInfo info, StreamingContext context)
	{
		DeserializePose((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把位姿按库自有二进制格式写入流。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>把当前位姿写入二进制流，内容与 <see cref="SerializePose"/> 相同，并保留位姿的表示信息。</para>
	///   <para><b>约束与资源</b>流必须可写；流的生命周期由调用方管理，本方法不会关闭或释放传入的流。读取时使用 <see cref="Deserialize(Stream)"/>。</para>
	///   <para><b>与相邻算子的取舍</b>只要内存字节（如塞进自定义报文）用 <c>SerializePose</c>/<c>DeserializePose</c> 一对；落盘文本给人读用 <c>WritePose</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
	///   {
	///       pose.Serialize(ms);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>流的生命周期由调用方管理，本方法不关闭传入的流；读回用静态 <c>JlPose.Deserialize(Stream)</c>，它返回新实例。</para>
	/// </remarks>
	public void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializePose(), stream);
	}

	/// <summary>从 <c>Serialize(Stream)</c> 写出的流读出一个新位姿。</summary>
	/// <returns>承载流内容的新 JlPose 实例（非原地改写；JlPose 实现 IDisposable，使用后应释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>从当前流位置读取一个由 <see cref="Serialize(Stream)"/> 写出的二进制负载，并返回新的 JlPose；输入流和新对象相互独立。</para>
	///   <para><b>约束与异常</b>流必须可读且游标位于完整负载的起点。内容截断或格式不匹配时抛出 <see cref="JlOperatorException"/>；位姿表示信息随负载恢复。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose src = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   JlPose back;
	///   using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
	///   {
	///       src.Serialize(ms);
	///       ms.Position = 0;
	///       back = JlPose.Deserialize(ms);
	///   }
	///   </code>
	///   <para><b>资源与坑</b>调用方负责流的打开与关闭；位置游标要对准 <c>Serialize</c> 写入的起点。</para>
	/// </remarks>
	public static JlPose Deserialize(Stream stream)
	{
		JlPose hPose = new JlPose();
		hPose.DeserializePose(JlSerializationBuffer.ReadFromStream(stream));
		return hPose;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>序列化/反序列化往返得到的独立位姿副本。</summary>
	/// <returns>新 JlPose 实例；与原对象数据完全解耦（JlPose 实现 IDisposable，使用后应释放）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>实现 = <c>SerializePose()</c> 取字节 → <c>new JlPose()</c> → <c>DeserializePose(byte[])</c> 覆写，走两次原生调用（id 1834/1833），比引用赋值贵。</para>
	///   <para><b>约束或前提</b>JlPose 的数据构造和 RawData 访问都使用独立元组副本；需要连同原生表示重新计算时才使用 Clone。</para>
	///   <para><b>与相邻算子的取舍</b>只是想"在旧值基础上继续复合、保留本对象"，用 <c>PoseCompose</c>/<c>SetOriginPose</c> 这类本就返回新实例的运算即可，不必 Clone。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose a = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   JlPose b = a.Clone();
	///   </code>
	///   <para><b>资源与坑</b>Clone 经过托管字节数组中转，无句柄泄漏点；ICloneable 显式实现同源。</para>
	/// </remarks>
	public JlPose Clone()
	{
		byte[] data = SerializePose();
		JlPose obj = new JlPose();
		obj.DeserializePose(data);
		return obj;
	}


	/// <summary>对一组位姿求（加权）平均，返回单个新位姿。</summary>
	/// <param name="poses">参与平均的位姿数组（每个自动压平为 7 分量）。</param>
	/// <param name="weights">空元组=等权；否则每个位姿一个权重。Default: []</param>
	/// <param name="mode">平均模式。Default: "iterative"</param>
	/// <param name="sigmaT">平移权重或 "auto"。Default: "auto"</param>
	/// <param name="sigmaR">旋转权重或 "auto"。Default: "auto"</param>
	/// <param name="quality">四元素 DOUBLE 元组：平移 RMS、旋转 RMS、最大平移偏差、最大旋转偏差。</param>
	/// <returns>加权平均后的新 JlPose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>计算一组位姿的平均平移和平均旋转。空 <paramref name="weights"/> 表示等权；非空时必须为每个位姿提供一个正权重。</para>
	///   <para><b>模式与权重</b><paramref name="mode"/> 可取 <c>"direct"</c> 或 <c>"iterative"</c>，默认值为 <c>"iterative"</c>。迭代模式以直接平均为初值并降低离群位姿的影响；<paramref name="sigmaT"/> 和 <paramref name="sigmaR"/> 可取 <c>"auto"</c>，也可给出平移和旋转的期望离散程度。直接模式忽略这两个参数。</para>
	///   <para><b>输出</b><paramref name="quality"/> 固定包含 4 个 double，顺序为平移 RMS、旋转 RMS、最大平移偏差和最大旋转偏差；返回的位姿和质量元组由调用方释放。</para>
	///   <para><b>与相邻算子的取舍</b>要"两个位姿复合"用 PoseCompose，平均≠复合；位姿数=1 时结果即其本身，白白多一次原生调用。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose p1 = new JlPose(0.1, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose p2 = new JlPose(0.2, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose mean = JlPose.PoseAverage(new JlPose[] { p1, p2 }, new JlTuple(), "iterative", "auto", "auto", out JlTuple quality);
	///   </code>
	///   <para><b>资源与坑</b>返回新 JlPose（JlData 系实现 IDisposable，使用后应释放）；单实例版差异见 double 重载。</para>
	/// </remarks>
	public static JlPose PoseAverage(JlPose[] poses, JlTuple weights, string mode, JlTuple sigmaT, JlTuple sigmaR, out JlTuple quality)
	{
		JlTuple tupleValue = JlData.ConcatArray(poses);
		IntPtr proc = JlNativeApi.PreCall(220);
		JlNativeApi.Store(proc, 0, tupleValue);
		JlNativeApi.Store(proc, 1, weights);
		JlNativeApi.StoreS(proc, 2, mode);
		JlNativeApi.Store(proc, 3, sigmaT);
		JlNativeApi.Store(proc, 4, sigmaR);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tupleValue);
		JlNativeApi.UnpinTuple(weights);
		JlNativeApi.UnpinTuple(sigmaT);
		JlNativeApi.UnpinTuple(sigmaR);
		err = LoadNew(proc, 0, err, out var obj);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out quality);
		JlNativeApi.PostCall(proc, err);
		return obj;
	}

	/// <summary>使用枚举模式求位姿平均。</summary>
	public static JlPose PoseAverage(JlPose[] poses, JlTuple weights, JlPoseAverageMode mode, JlTuple sigmaT, JlTuple sigmaR, out JlTuple quality)
	{
		return PoseAverage(poses, weights, mode.ToNative(), sigmaT, sigmaR, out quality);
	}

	/// <summary>对一组位姿求（加权）平均（标量 sigma 版）。</summary>
	/// <param name="poses">参与平均的位姿数组。</param>
	/// <param name="weights">空元组=等权；否则每个位姿一个权重。Default: []</param>
	/// <param name="mode">平均模式。Default: "iterative"</param>
	/// <param name="sigmaT">平移权重（数值，不能填 "auto"）。Default: "auto"</param>
	/// <param name="sigmaR">旋转权重（数值，不能填 "auto"）。Default: "auto"</param>
	/// <param name="quality">四元素 DOUBLE 元组：平移 RMS、旋转 RMS、最大平移偏差、最大旋转偏差。</param>
	/// <returns>加权平均后的新 JlPose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载使用相同的平均算法；本重载把 <paramref name="sigmaT"/> 和 <paramref name="sigmaR"/> 作为数值传入，适用于已确定权重尺度的场景。</para>
	///   <para><b>限制</b>本重载不能表达 <c>"auto"</c>；需要自动估计时使用接受 <see cref="JlTuple"/> 的重载。<paramref name="quality"/> 仍为四元素 DOUBLE 元组。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose p1 = new JlPose(0.1, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose p2 = new JlPose(0.2, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose mean = JlPose.PoseAverage(new JlPose[] { p1, p2 }, new JlTuple(), "iterative", 1.0, 1.0, out JlTuple quality);
	///   </code>
	///   <para><b>资源与坑</b>返回新 JlPose（实现 IDisposable）。</para>
	/// </remarks>
	public static JlPose PoseAverage(JlPose[] poses, JlTuple weights, string mode, double sigmaT, double sigmaR, out JlTuple quality)
	{
		JlTuple tupleValue = JlData.ConcatArray(poses);
		IntPtr proc = JlNativeApi.PreCall(220);
		JlNativeApi.Store(proc, 0, tupleValue);
		JlNativeApi.Store(proc, 1, weights);
		JlNativeApi.StoreS(proc, 2, mode);
		JlNativeApi.StoreD(proc, 3, sigmaT);
		JlNativeApi.StoreD(proc, 4, sigmaR);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tupleValue);
		JlNativeApi.UnpinTuple(weights);
		err = LoadNew(proc, 0, err, out var obj);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out quality);
		JlNativeApi.PostCall(proc, err);
		return obj;
	}

	/// <summary>使用枚举模式和标量权重求位姿平均。</summary>
	public static JlPose PoseAverage(JlPose[] poses, JlTuple weights, JlPoseAverageMode mode, double sigmaT, double sigmaR, out JlTuple quality)
	{
		return PoseAverage(poses, weights, mode.ToNative(), sigmaT, sigmaR, out quality);
	}

	/// <summary>逐元素求逆位姿（数组版），返回同样长度的新数组。</summary>
	/// <param name="pose">待求逆的位姿数组（内部压平为 7n 元组再传入）。</param>
	/// <returns>新的 JlPose[]，第 i 项为输入第 i 项的逆变换，并保持原位姿的表示类型。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>逐个位姿转换为齐次变换矩阵、求逆，再转换回位姿；输入数组中的每个元素独立处理，输出保持对应索引和表示类型。</para>
	///   <para><b>约束</b>每个输入位姿必须完整包含 7 个元素。求逆是刚体变换的逆，不是逐元素取倒数。</para>
	///   <para><b>与相邻算子的取舍</b>单个位姿求逆用实例方法 <c>PoseInvert()</c>，少一次数组压平/切分开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose p1 = new JlPose(0.1, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose[] inverses = JlPose.PoseInvert(new JlPose[] { p1 });
	///   </code>
	///   <para><b>资源与坑</b>JlPose 实现 IDisposable，返回数组由 GC 管理。</para>
	/// </remarks>
	public static JlPose[] PoseInvert(JlPose[] pose)
	{
		JlTuple tupleValue = JlData.ConcatArray(pose);
		IntPtr proc = JlNativeApi.PreCall(226);
		JlNativeApi.Store(proc, 0, tupleValue);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tupleValue);
		err = JlTuple.LoadNew(proc, 0, err, out var data);
		JlNativeApi.PostCall(proc, err);
		return SplitArray(data);
	}

	/// <summary>
	///   求本位姿的逆，返回新实例（this 不被修改）。
	/// </summary>
	/// <returns>逆位姿的新 JlPose。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>同一原生 id 226；this 以 Store 钉入，结果经 <c>LoadNew</c> 装入新对象返回（代码层面可见非原地改写），调用结束有 GC.KeepAlive(this)。</para>
	///   <para><b>与相邻算子的取舍</b>批量求逆用静态数组重载；想连复合一起省一次调用，可直接用 <c>PoseCompose</c> 手工构造。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose p = new JlPose(0.1, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose inv = p.PoseInvert();
	///   </code>
	///   <para><b>资源与坑</b>JlPose 实现 IDisposable，使用后应释放。</para>
	/// </remarks>
	public JlPose PoseInvert()
	{
		IntPtr proc = JlNativeApi.PreCall(226);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>逐对复合两组位姿（数组版），返回复合结果数组。</summary>
	/// <param name="poseLeft">左操作数位姿数组。</param>
	/// <param name="poseRight">右操作数位姿数组。</param>
	/// <returns>新的 JlPose[]。两数组等长时按索引配对；其中一侧只有一个位姿时，该位姿会与另一侧的每个元素组合。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>将左、右位姿解释为坐标系变换，按 HALCON 的矩阵乘法顺序组合。两侧等长时逐项配对；一侧为单个位姿时对另一侧逐项广播；两侧均为多元素且长度不等会失败。</para>
	///   <para><b>结果表示</b>输入表示类型相同则保留该类型；类型不同时返回标准表示 <c>("Rp+T", "gba", "point")</c>。</para>
	///   <para><b>与相邻算子的取舍</b>一对一复合用实例版 <c>PoseCompose(JlPose)</c> 更直观；求平均用 PoseAverage，别拿复合代替。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose a = new JlPose(0.1, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose b = new JlPose(0.0, 0.2, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose[] composed = JlPose.PoseCompose(new JlPose[] { a }, new JlPose[] { b });
	///   </code>
	///   <para><b>资源与坑</b>JlPose 实现 IDisposable，返回数组由 GC 管理。</para>
	/// </remarks>
	public static JlPose[] PoseCompose(JlPose[] poseLeft, JlPose[] poseRight)
	{
		JlData[] data = poseLeft;
		JlTuple tupleValue = JlData.ConcatArray(data);
		data = poseRight;
		JlTuple tupleValue2 = JlData.ConcatArray(data);
		IntPtr proc = JlNativeApi.PreCall(227);
		JlNativeApi.Store(proc, 0, tupleValue);
		JlNativeApi.Store(proc, 1, tupleValue2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(tupleValue);
		JlNativeApi.UnpinTuple(tupleValue2);
		err = JlTuple.LoadNew(proc, 0, err, out var data2);
		JlNativeApi.PostCall(proc, err);
		return SplitArray(data2);
	}

	/// <summary>
	///   把右侧位姿复合到本位姿（this 为左操作数），返回新实例。
	/// </summary>
	/// <param name="poseRight">右操作数位姿（元组钉住传入，调用结束解钉）。</param>
	/// <returns>复合结果的新 JlPose；this 不被修改。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>同一原生 id 227：this 以 <c>Store(proc,0)</c> 入左槽、poseRight 入右槽，结果 <c>LoadNew</c> 新对象返回，尾部 GC.KeepAlive(this)。</para>
	///   <para><b>组合顺序</b>先构造左、右位姿对应的齐次矩阵 H1、H2，再计算 H1·H2；因此应用顺序应按坐标系变换的矩阵乘法规则理解。this 和 <paramref name="poseRight"/> 都只读，结果是新对象。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose left = new JlPose(0.1, 0.0, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose right = new JlPose(0.0, 0.2, 0.0, 0.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose combined = left.PoseCompose(right);
	///   </code>
	///   <para><b>资源与坑</b>JlPose 实现 IDisposable，使用后应释放。</para>
	/// </remarks>
	public JlPose PoseCompose(JlPose poseRight)
	{
		IntPtr proc = JlNativeApi.PreCall(227);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, poseRight);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(poseRight);
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}































	/// <summary>由相机位置与注视点构造“对准该点”的 3D 相机位姿（批量）。</summary>
	/// <param name="camPosX">相机中心的 x 坐标元组；坐标单位由场景定义。</param>
	/// <param name="camPosY">光心 y 坐标元组。</param>
	/// <param name="camPosZ">光心 z 坐标元组。</param>
	/// <param name="lookAtX">注视点 x 坐标元组。</param>
	/// <param name="lookAtY">注视点 y 坐标元组。</param>
	/// <param name="lookAtZ">注视点 z 坐标元组。</param>
	/// <param name="refPlaneNormal">参考平面的法向量，可传 <c>"x"</c>、<c>"-x"</c>、<c>"y"</c>、<c>"-y"</c>、<c>"z"</c>、<c>"-z"</c>，或长度为 3 的法向量元组。Default: <c>"-y"</c></param>
	/// <param name="camRoll">绕相机观察轴的滚转角，单位为弧度。Default: 0</param>
	/// <returns>新 JlPose[]；元组参数按 HALCON 的标量广播规则组合，数组中的每个元素对应一组输入。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>根据相机中心、注视点、参考平面法向量和滚转角生成相机位姿。相机中心不能与注视点重合；参考平面法向量不能与相机的 z 轴平行，否则无法唯一确定姿态，原生算子会报告错误 8940。</para>
	///   <para><b>输入规则</b>长度为 1 的坐标或角度元组会广播到其他输入；多个长度大于 1 的元组必须具有相同长度。结果数量等于广播后的输入数量。</para>
	///   <para><b>与相邻入口的取舍</b>要原地改写一个已有的 JlPose，用 double 版实例重载；本静态版适合批量生成多个位姿。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose[] poses = JlPose.CreateCamPoseLookAtPoint(0.0, 0.0, -1.0, 0.0, 0.0, 0.0, "-y", 0.0);
	///   </code>
	///   <para><b>异常与资源</b>输入不满足几何约束或元组长度不兼容时抛出 <see cref="JlOperatorException"/>。返回数组中的每个 JlPose 都拥有独立资源，使用后应逐个 Dispose。</para>
	/// </remarks>
	public static JlPose[] CreateCamPoseLookAtPoint(JlTuple camPosX, JlTuple camPosY, JlTuple camPosZ, JlTuple lookAtX, JlTuple lookAtY, JlTuple lookAtZ, JlTuple refPlaneNormal, JlTuple camRoll)
	{
		IntPtr proc = JlNativeApi.PreCall(995);
		JlNativeApi.Store(proc, 0, camPosX);
		JlNativeApi.Store(proc, 1, camPosY);
		JlNativeApi.Store(proc, 2, camPosZ);
		JlNativeApi.Store(proc, 3, lookAtX);
		JlNativeApi.Store(proc, 4, lookAtY);
		JlNativeApi.Store(proc, 5, lookAtZ);
		JlNativeApi.Store(proc, 6, refPlaneNormal);
		JlNativeApi.Store(proc, 7, camRoll);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(camPosX);
		JlNativeApi.UnpinTuple(camPosY);
		JlNativeApi.UnpinTuple(camPosZ);
		JlNativeApi.UnpinTuple(lookAtX);
		JlNativeApi.UnpinTuple(lookAtY);
		JlNativeApi.UnpinTuple(lookAtZ);
		JlNativeApi.UnpinTuple(refPlaneNormal);
		JlNativeApi.UnpinTuple(camRoll);
		err = JlTuple.LoadNew(proc, 0, err, out var data);
		JlNativeApi.PostCall(proc, err);
		return SplitArray(data);
	}

	/// <summary>
	///   由相机位置与注视点原地改写本位姿（double 版）。
	/// </summary>
	/// <param name="camPosX">相机中心的 x 坐标；坐标单位由场景定义。</param>
	/// <param name="camPosY">相机中心的 y 坐标；坐标单位由场景定义。</param>
	/// <param name="camPosZ">相机中心的 z 坐标；坐标单位由场景定义。</param>
	/// <param name="lookAtX">注视点的 x 坐标；坐标单位由场景定义。</param>
	/// <param name="lookAtY">注视点的 y 坐标；坐标单位由场景定义。</param>
	/// <param name="lookAtZ">注视点的 z 坐标；坐标单位由场景定义。</param>
	/// <param name="refPlaneNormal">参考平面的法向量，可传 <c>"x"</c>、<c>"-x"</c>、<c>"y"</c>、<c>"-y"</c>、<c>"z"</c>、<c>"-z"</c>，或长度为 3 的法向量元组。Default: <c>"-y"</c></param>
	/// <param name="camRoll">绕相机观察轴的滚转角，单位为弧度。Default: 0</param>
	/// <remarks>
	///   <para><b>功能说明</b>根据相机中心、注视点、参考平面法向量和滚转角原地生成相机位姿。六个坐标参数为标量；<paramref name="refPlaneNormal"/>仍可使用轴名字符串或 3 元素法向量元组。</para>
	///   <para><b>约束与异常</b>相机中心不能与注视点重合，参考平面法向量不能与相机 z 轴平行；不满足时原生算子会报告错误 8940，并抛出 <see cref="JlOperatorException"/>。</para>
	///   <para><b>与相邻算子的取舍</b>不想破坏现值时先 <c>Clone()</c> 再调用；批量计算用静态元组重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose camPose = new JlPose();
	///   camPose.CreateCamPoseLookAtPoint(0.0, 0.0, -1.0, 0.0, 0.0, 0.0, "-y", 0.0);
	///   </code>
	///   <para><b>资源与生命周期</b>此方法覆写当前对象，不创建新的 JlPose；对象仍需在不再使用时 Dispose。</para>
	/// </remarks>
	public void CreateCamPoseLookAtPoint(double camPosX, double camPosY, double camPosZ, double lookAtX, double lookAtY, double lookAtZ, JlTuple refPlaneNormal, double camRoll)
	{
		IntPtr proc = JlNativeApi.PreCall(995);
		JlNativeApi.StoreD(proc, 0, camPosX);
		JlNativeApi.StoreD(proc, 1, camPosY);
		JlNativeApi.StoreD(proc, 2, camPosZ);
		JlNativeApi.StoreD(proc, 3, lookAtX);
		JlNativeApi.StoreD(proc, 4, lookAtY);
		JlNativeApi.StoreD(proc, 5, lookAtZ);
		JlNativeApi.Store(proc, 6, refPlaneNormal);
		JlNativeApi.StoreD(proc, 7, camRoll);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(refPlaneNormal);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}








	/// <summary>
	///   结合相机参数与本位姿（世界位姿），把图像坐标系的 XLD 轮廓变换到世界系 z=0 平面上。
	/// </summary>
	/// <param name="contours">待变换的 XLD 轮廓（输入控制参数）。</param>
	/// <param name="cameraParam">HALCON 内部相机参数元组；参数顺序和长度必须符合所使用的相机模型。</param>
	/// <param name="scale">输出世界坐标的尺度或单位，例如 <c>"m"</c>。Default: <c>"m"</c></param>
	/// <returns>世界坐标下的新 JlXLDCont 句柄（非原地改写，用毕须 Dispose）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1810。this 作为世界位姿（WorldPose 槽位）参与投影反解，轮廓点经相机模型反投影到世界 z=0 平面。</para>
	///   <para><b>约束或前提</b>输入是 XLD 轮廓而非 Region；算子把轮廓点变换到世界坐标系的 z=0 平面。<paramref name="cameraParam"/> 必须是与图像匹配的内部相机参数，当前位姿必须描述世界坐标系相对于相机坐标系的姿态，<paramref name="scale"/> 决定输出坐标的尺度。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose worldPose = new JlPose(0.0, 0.0, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlXLDCont contours = new JlXLDCont(new double[] { 100.0, 200.0 }, new double[] { 100.0, 200.0 });
	///   JlTuple cameraParam = new double[] { 0.0, 0.0, 0.008, 0.0, 0.0, 0.0, 0.0, 0.0, 800.0, 800.0, 0.0, 0.0 };
	///   JlTuple scale = "m";
	///   JlXLDCont world = worldPose.ContourToWorldPlaneXld(contours, cameraParam, scale);
	///   world.Dispose();
	///   contours.Dispose();
	///   </code>
	///   <para><b>异常与资源</b>相机参数、位姿或尺度不合法时抛出 <see cref="JlOperatorException"/>。返回值是新的 JlXLDCont，使用后应 Dispose；输入轮廓仍由调用方管理。</para>
	/// </remarks>
	public JlXLDCont ContourToWorldPlaneXld(JlXLDCont contours, JlTuple cameraParam, JlTuple scale)
	{
		IntPtr proc = JlNativeApi.PreCall(1810);
		Store(proc, 1);
		JlNativeApi.Store(proc, 1, contours);
		JlNativeApi.Store(proc, 0, cameraParam);
		JlNativeApi.Store(proc, 2, scale);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(cameraParam);
		JlNativeApi.UnpinTuple(scale);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
		return obj;
	}

	/// <summary>
	///   结合相机参数与本位姿，把 XLD 轮廓变换到世界系 z=0 平面（scale 字符串版）。
	/// </summary>
	/// <param name="contours">待变换的 XLD 轮廓。</param>
	/// <param name="cameraParam">HALCON 内部相机参数元组；参数顺序和长度必须符合所使用的相机模型。</param>
	/// <param name="scale">输出世界坐标的尺度或单位，例如 <c>"m"</c>。Default: <c>"m"</c></param>
	/// <returns>世界坐标下的新 JlXLDCont 句柄（用毕须 Dispose）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>同一原生 id 1810，scale 以 <c>StoreS</c> 直写、不钉固定元组；其余与元组重载一致。</para>
	///   <para><b>约束或前提</b>以字符串字面量传 scale 时，重载解析选中本重载（恒等转换优先于 string→JlTuple 隐式转换）。z=0 平面假设、单位一致性等约束见元组重载的说明。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose worldPose = new JlPose(0.0, 0.0, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlXLDCont contours = new JlXLDCont(new double[] { 100.0, 200.0 }, new double[] { 100.0, 200.0 });
	///   JlTuple cameraParam = new double[] { 0.0, 0.0, 0.008, 0.0, 0.0, 0.0, 0.0, 0.0, 800.0, 800.0, 0.0, 0.0 };
	///   JlXLDCont world = worldPose.ContourToWorldPlaneXld(contours, cameraParam, "m");
	///   world.Dispose();
	///   contours.Dispose();
	///   </code>
	///   <para><b>异常与资源</b>相机参数、位姿或尺度不合法时抛出 <see cref="JlOperatorException"/>。返回值是新的 JlXLDCont，使用后应 Dispose；输入轮廓仍由调用方管理。</para>
	/// </remarks>
	public JlXLDCont ContourToWorldPlaneXld(JlXLDCont contours, JlTuple cameraParam, string scale)
	{
		IntPtr proc = JlNativeApi.PreCall(1810);
		Store(proc, 1);
		JlNativeApi.Store(proc, 1, contours);
		JlNativeApi.Store(proc, 0, cameraParam);
		JlNativeApi.StoreS(proc, 2, scale);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(cameraParam);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
		return obj;
	}

	/// <summary>
	///   平移本位姿的原点，返回新位姿。
	/// </summary>
	/// <param name="DX">x 方向的原点平移量，单位与位姿平移分量一致。Default: 0</param>
	/// <param name="DY">y 方向的原点平移量，单位与位姿平移分量一致。Default: 0</param>
	/// <param name="DZ">z 方向的原点平移量，单位与位姿平移分量一致。Default: 0</param>
	/// <returns>平移后的新 JlPose；this 不被修改。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>将位姿原点沿给定的 x、y、z 偏移移动，返回新的位姿；原位姿保持不变。HALCON 的位姿表示会被正确解释，输出仍描述同一变换约定下的新原点。</para>
	///   <para><b>约束与异常</b>偏移量使用与位姿平移分量相同的场景单位。输入必须是完整的 7 元素位姿；不合法输入时抛出 <see cref="JlOperatorException"/>。</para>
	///   <para><b>与相邻算子的取舍</b>沿固定向量做链式平移时，连续调用每次都过一次原生调用；能合并成一次 (DX,DY,DZ) 就合并。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.1, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   JlPose moved = pose.SetOriginPose(0.05, 0.0, 0.0);
	///   </code>
	///   <para><b>资源与坑</b>JlPose 实现 IDisposable；参数名 DX/DY/DZ 大写是签名的一部分，示例保持一致。</para>
	/// </remarks>
	public JlPose SetOriginPose(double DX, double DY, double DZ)
	{
		IntPtr proc = JlNativeApi.PreCall(1812);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, DX);
		JlNativeApi.StoreD(proc, 2, DY);
		JlNativeApi.StoreD(proc, 3, DZ);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}



	/// <summary>
	///   读取本位姿当前的表示形式三元组（只查询，不修改）。
	/// </summary>
	/// <param name="orderOfRotation">输出的旋转表示顺序，例如 <c>"gba"</c>、<c>"abg"</c> 或 <c>"rodriguez"</c>。</param>
	/// <param name="viewOfTransform">输出的变换视角：<c>"point"</c> 或 <c>"coordinate_system"</c>。</param>
	/// <returns>旋转与平移的组合顺序，例如 <c>"Rp+T"</c> 或 <c>"R(p-T)"</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>原生 id 1814：三个字符串都经 <c>LoadS</c> 读出（返回值=orderOfTransform，另两个走 out）。</para>
	///   <para><b>约束或前提</b>输入必须包含完整的 7 元素位姿。返回的三个字符串共同描述最后一个类型码对应的表示约定；本方法只查询，不改变位姿。</para>
	///   <para><b>与相邻算子的取舍</b>想改成别的表示形式用 <c>ConvertPoseType</c>（它会返回新实例），别拿本方法的返回值手工拼数。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   string orderOfTransform = pose.GetPoseType(out string orderOfRotation, out string viewOfTransform);
	///   </code>
	///   <para><b>资源与坑</b>忽略返回值只看 out 会丢掉次序信息；无句柄资源。</para>
	/// </remarks>
	public string GetPoseType(out string orderOfRotation, out string viewOfTransform)
	{
		IntPtr proc = JlNativeApi.PreCall(1814);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlNativeApi.LoadS(proc, 0, err, out var stringValue);
		err = JlNativeApi.LoadS(proc, 1, err, out orderOfRotation);
		err = JlNativeApi.LoadS(proc, 2, err, out viewOfTransform);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return stringValue;
	}

	/// <summary>读取本位姿当前的表示选项。</summary>
	public JlPoseOptions GetPoseOptions()
	{
		string orderOfTransform = GetPoseType(out string orderOfRotation, out string viewOfTransform);
		return new JlPoseOptions(orderOfTransform, orderOfRotation, viewOfTransform);
	}

	/// <summary>
	///   换一种表示形式描述同一刚体变换，返回新位姿。
	/// </summary>
	/// <param name="orderOfTransform">目标旋转/平移次序。Default: "Rp+T"</param>
	/// <param name="orderOfRotation">目标旋转值含义（欧拉序等）。Default: "gba"</param>
	/// <param name="viewOfTransform">目标视角。Default: "point"</param>
	/// <returns>目标表示形式下的新 JlPose；this 不被修改。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>按目标表示形式重新表达同一个刚体变换，返回新的位姿；平移和旋转对应的几何变换保持不变。</para>
	///   <para><b>允许值</b><paramref name="orderOfTransform"/> 可为 <c>"Rp+T"</c> 或 <c>"R(p-T)"</c>；<paramref name="orderOfRotation"/> 可为 <c>"gba"</c>、<c>"abg"</c> 或 <c>"rodriguez"</c>；<paramref name="viewOfTransform"/> 可为 <c>"point"</c> 或 <c>"coordinate_system"</c>。组合不合法或输入不是完整位姿时抛出 <see cref="JlOperatorException"/>。</para>
	///   <para><b>与相邻算子的取舍</b>只想"知道当前是什么形式"用 <c>GetPoseType</c>（不生成新对象）；要数值分量对照用 <c>RawData</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   JlPose converted = pose.ConvertPoseType("T+Rp", "gba", "point");
	///   </code>
	///   <para><b>资源与坑</b>JlPose 实现 IDisposable，使用后应释放。</para>
	/// </remarks>
	public JlPose ConvertPoseType(string orderOfTransform, string orderOfRotation, string viewOfTransform)
	{
		IntPtr proc = JlNativeApi.PreCall(1815);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, orderOfTransform);
		JlNativeApi.StoreS(proc, 2, orderOfRotation);
		JlNativeApi.StoreS(proc, 3, viewOfTransform);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>使用 <see cref="JlPoseOptions"/> 转换位姿表示。</summary>
	public JlPose ConvertPoseType(JlPoseOptions options)
	{
		JlPoseOptions value = RequireOptions(options);
		return ConvertPoseType(value.OrderOfTransform, value.OrderOfRotation, value.ViewOfTransform);
	}

	/// <summary>
	///   用给定的平移/旋转分量原地构造（覆写）本位姿。
	/// </summary>
	/// <param name="transX">x 方向平移，单位由场景定义。Default: 0.1</param>
	/// <param name="transY">y 方向平移，单位由场景定义。Default: 0.1</param>
	/// <param name="transZ">z 方向平移，单位由场景定义。Default: 0.1</param>
	/// <param name="rotX">旋转分量或 Rodriguez 向量 x 分量，含义由 <paramref name="orderOfRotation"/> 决定。HALCON 的欧拉角单位为度。Default: 90.0</param>
	/// <param name="rotY">旋转分量或 Rodriguez 向量 y 分量，含义由 <paramref name="orderOfRotation"/> 决定。Default: 90.0</param>
	/// <param name="rotZ">旋转分量或 Rodriguez 向量 z 分量，含义由 <paramref name="orderOfRotation"/> 决定。Default: 90.0</param>
	/// <param name="orderOfTransform">旋转/平移组合顺序：<c>"Rp+T"</c> 或 <c>"R(p-T)"</c>。Default: <c>"Rp+T"</c></param>
	/// <param name="orderOfRotation">旋转表示：<c>"gba"</c>、<c>"abg"</c> 或 <c>"rodriguez"</c>。Default: <c>"gba"</c></param>
	/// <param name="viewOfTransform">变换视角：<c>"point"</c> 或 <c>"coordinate_system"</c>。Default: <c>"point"</c></param>
	/// <remarks>
	///   <para><b>功能说明</b>根据六个数值分量和三个表示选项原地创建或覆写位姿。表示选项的允许值与 <see cref="ConvertPoseType(string, string, string)"/> 相同。</para>
	///   <para><b>约束与异常</b>平移单位由场景定义；欧拉角按 HALCON 约定使用度，Rodriguez 表示使用无量纲旋转向量。输入组合不合法时抛出 <see cref="JlOperatorException"/>。</para>
	///   <para><b>与相邻算子的取舍</b>新位姿优先直接 <c>new JlPose(同九个参数)</c>；本方法适合复用实例避免小对象分配。表示形式要换用 ConvertPoseType，不要重喂数值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose();
	///   pose.CreatePose(0.1, 0.1, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   </code>
	///   <para><b>资源与坑</b>void 返回、无新对象；JlPose 实现 IDisposable。</para>
	/// </remarks>
	public void CreatePose(double transX, double transY, double transZ, double rotX, double rotY, double rotZ, string orderOfTransform, string orderOfRotation, string viewOfTransform)
	{
		IntPtr proc = JlNativeApi.PreCall(1816);
		JlNativeApi.StoreD(proc, 0, transX);
		JlNativeApi.StoreD(proc, 1, transY);
		JlNativeApi.StoreD(proc, 2, transZ);
		JlNativeApi.StoreD(proc, 3, rotX);
		JlNativeApi.StoreD(proc, 4, rotY);
		JlNativeApi.StoreD(proc, 5, rotZ);
		JlNativeApi.StoreS(proc, 6, orderOfTransform);
		JlNativeApi.StoreS(proc, 7, orderOfRotation);
		JlNativeApi.StoreS(proc, 8, viewOfTransform);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>使用 <see cref="JlPoseOptions"/> 原地创建或覆写位姿。</summary>
	public void CreatePose(double transX, double transY, double transZ, double rotX, double rotY, double rotZ, JlPoseOptions options)
	{
		JlPoseOptions value = RequireOptions(options);
		CreatePose(transX, transY, transZ, rotX, rotY, rotZ, value.OrderOfTransform, value.OrderOfRotation, value.ViewOfTransform);
	}



	/// <summary>用 <c>SerializePose()</c> 得到的字节覆写本位姿（原地改写）。</summary>
	/// <param name="serializedItemHandle">由 <see cref="SerializePose"/> 生成的完整位姿负载；它不是原生句柄数值。</param>
	/// <remarks>
	///   <para><b>功能说明</b>读取 JLVisionLib 的位姿二进制负载并原地覆写当前对象；不会创建新的 JlPose。</para>
	///   <para><b>约束与异常</b>负载必须来自本库的 <see cref="SerializePose"/> 或 <see cref="Serialize(Stream)"/>。负载为空、截断或格式不匹配时抛出 <see cref="JlOperatorException"/>；本方法也用于 .NET 序列化回调。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose src = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   byte[] data = src.SerializePose();
	///   JlPose dst = new JlPose();
	///   dst.DeserializePose(data);
	///   </code>
	///   <para><b>资源与坑</b>buffer 在方法内 using 释放且调用处有 GC.KeepAlive，原生调用期间不会被回收；调用方只需管 byte[] 本身（GC 自动）。</para>
	/// </remarks>
	public void DeserializePose(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		IntPtr proc = JlNativeApi.PreCall(1833);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>把本位姿导出为库自有二进制格式的字节数组。</summary>
	/// <returns>序列化负载 byte[]（每次调用新建；配套 <c>DeserializePose(byte[])</c> 读回）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>把当前位姿导出为 JLVisionLib 的二进制负载；每次调用返回新的托管字节数组，当前对象不变。</para>
	///   <para><b>约束与兼容性</b>该负载是库的二进制格式，只能与 <see cref="DeserializePose(byte[])"/> 或 <see cref="Serialize(Stream)"/> / <see cref="Deserialize(Stream)"/> 配套使用，不要与 <see cref="WritePose(string)"/> 生成的文本文件混用。</para>
	///   <para><b>与相邻算子的取舍</b>要塞进流用实例方法 <c>Serialize(Stream)</c>；要人可读的文本文件用 <c>WritePose(string)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.1, 90.0, 90.0, 90.0, "Rp+T", "gba", "point");
	///   byte[] data = pose.SerializePose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是纯托管字节，GC 管理，无句柄。</para>
	/// </remarks>
	public byte[] SerializePose()
	{
		IntPtr proc = JlNativeApi.PreCall(1834);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   从文本文件读入位姿并原地覆写本实例。
	/// </summary>
	/// <param name="poseFile">位姿文本文件路径。Default: "campose.dat"</param>
	/// <remarks>
	///   <para><b>功能说明</b>从 HALCON 位姿文本文件读取数据并原地覆写当前对象；该方法与 <see cref="WritePose(string)"/> 使用同一文本格式。</para>
	///   <para><b>约束与异常</b>文件必须包含 HALCON 可识别的位姿表示，文件不存在、无法读取或内容无效时抛出 <see cref="JlOperatorException"/>。读取成功后，位姿的表示选项随文件内容恢复。</para>
	///   <para><b>与相邻算子的取舍</b>程序间传大位姿数组用 <see cref="Serialize(Stream)"/> 二进制族；本方法面向"人可编辑"的单个位姿文件。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose();
	///   pose.ReadPose("campose.dat");
	///   </code>
	///   <para><b>资源与生命周期</b>方法不接管文件路径对应的文件句柄；当前对象仍由调用方负责 Dispose。</para>
	/// </remarks>
	public void ReadPose(string poseFile)
	{
		IntPtr proc = JlNativeApi.PreCall(1835);
		JlNativeApi.StoreS(proc, 0, poseFile);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   把本位姿写出为文本文件。
	/// </summary>
	/// <param name="poseFile">目标文件路径（覆盖写入）。Default: "campose.dat"</param>
	/// <remarks>
	///   <para><b>功能说明</b>按当前位姿的数值和表示选项写出 HALCON 位姿文本文件；当前对象不变，目标文件会被覆盖。</para>
	///   <para><b>约束与异常</b>输出文件可由 <see cref="ReadPose(string)"/> 读回。目标目录不存在、文件不可写或位姿无效时抛出 <see cref="JlOperatorException"/>。</para>
	///   <para><b>与相邻算子的取舍</b>要无损、机器可读的负载用 <c>SerializePose()</c> 字节族；给人核对/手改才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlPose pose = new JlPose(0.1, 0.1, 0.5, 90.0, 0.0, 0.0, "Rp+T", "gba", "point");
	///   pose.WritePose("campose.dat");
	///   </code>
	///   <para><b>资源与生命周期</b>写文件由原生算子完成；方法不返回文件句柄，也不接管调用方的其他资源。</para>
	/// </remarks>
	public void WritePose(string poseFile)
	{
		IntPtr proc = JlNativeApi.PreCall(1836);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, poseFile);
		int procResult = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}






}
