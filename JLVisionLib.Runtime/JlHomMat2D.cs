using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>表示 HALCON 兼容的二维齐次变换矩阵。</summary>
/// <remarks>
///   <para>本类型沿用 HALCON 的行优先表示。仿射矩阵使用 6 元素 <c>[a11,a12,a13,a21,a22,a23]</c>；含透视的投影矩阵使用 9 元素 <c>[a11,a12,a13,a21,a22,a23,a31,a32,a33]</c>。HALCON 的点坐标可以是笛卡尔坐标或图像坐标，但矩阵和点必须使用同一坐标约定。</para>
///   <para>返回新矩阵的变换方法不会修改当前对象；名称带 <c>Local</c> 的算子按当前矩阵的局部坐标系叠加，普通算子按全局坐标系叠加。具体复合顺序以 HALCON 的左矩阵和右矩阵定义为准。</para>
/// </remarks>
[Serializable]
public class JlHomMat2D : JlData, ISerializable, ICloneable
{
	private const int FIXEDSIZE = 9;

	/// <summary>把一个已有的 <see cref="JlTuple"/> 包装成 2D 齐次矩阵，不调用任何原生算子。</summary>
	/// <param name="tuple">按 HALCON 行优先排列的仿射 6 元素或投影 9 元素矩阵。投影矩阵依次为 <c>[a11,a12,a13,a21,a22,a23,a31,a32,a33]</c>；多个输出投影矩阵可按每 9 个元素连续排列。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>内部走 <c>base(tuple)</c>，复制入参作为本实例独立存储，无原生调用、无句柄分配；<c>JlHomMat2D.SplitArray</c> 按 9 元素一段把长 tuple 切成多个矩阵时使用内部转移构造。</para>
	///   <para><b>前提与坑</b></para>
	///   <para>构造器复制输入元组，不与调用方共享数据；输入长度和元素类型由后续原生算子校验。仿射矩阵省略固定的第三行 <c>[0,0,1]</c>，含透视分量的矩阵包含完整第三行并应使用 <c>ProjectiveTrans*</c> 系列算子。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(new double[] { 1, 0, 10, 0, 1, 20, 0, 0, 1 });
	///   JlHomMat2D m = new JlHomMat2D(t);
	///   </code>
	/// </remarks>
	public JlHomMat2D(JlTuple tuple)
		: base(tuple)
	{
	}

	internal JlHomMat2D(JlTuple tuple, bool takeOwnership)
		: base(tuple, takeOwnership)
	{
	}

	internal JlHomMat2D(JlData data)
		: base(data)
	{
	}

	/// <summary>从原生过程输出装载一个齐次矩阵；供兼容的低层互操作调用。</summary>
	public static int LoadNew(IntPtr proc, int parIndex, JlTupleType type, int err, out JlHomMat2D obj)
	{
		err = JlTuple.LoadNew(proc, parIndex, err, out var t);
		obj = new JlHomMat2D(t, takeOwnership: true);
		return err;
	}

	/// <summary>从原生过程输出装载一个齐次矩阵，使用默认元组类型。</summary>
	public static int LoadNew(IntPtr proc, int parIndex, int err, out JlHomMat2D obj)
	{
		return LoadNew(proc, parIndex, JlTupleType.MIXED, err, out obj);
	}

	internal static JlHomMat2D[] SplitArray(JlTuple data)
	{
		int num = data.Length / 9;
		JlHomMat2D[] array = new JlHomMat2D[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = new JlHomMat2D(data.TupleSelectRange(i * 9, (i + 1) * 9 - 1), takeOwnership: true);
		}
		return array;
	}

	/// <summary>新建单位矩阵。原生算子 id 287（与 <c>HomMat2dIdentity</c> 同一算子），结果写入本实例。</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>构造体即 <c>hom_mat2d_identity</c> 的托管绑定：恒等仿射矩阵按行优先保存为 6 个 double：<c>[1,0,0,0,1,0]</c>。与就地版本 <c>HomMat2dIdentity()</c> 的差别仅在于这里新建对象。</para>
	///   <para><b>取舍</b></para>
	///   <para>复合链的起点用本构造；复用已有实例重置请用 <c>HomMat2dIdentity()</c>，避免额外分配。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();                    // 单位矩阵
	///   JlHomMat2D t = m.HomMat2dTranslate(10.0, 5.0);      // 复合一次平移
	///   </code>
	/// </remarks>
	public JlHomMat2D()
	{
		IntPtr proc = JlNativeApi.PreCall(287);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeHomMat2d();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>.NET 二进制反序列化构造器，配合 <c>ISerializable</c> 使用；负载即 <c>SerializeHomMat2d()</c> 的字节。</summary>
	/// <param name="info">由 <c>GetObjectData</c> 写入的序列化数据。</param>
	/// <param name="context">流式上下文，本实现不使用。</param>
	/// <remarks>
	///   <para><b>说明</b></para>
	///   <para>取 <c>"data"</c> 键下的 <c>byte[]</c> 后调用 <c>DeserializeHomMat2d</c> 覆写自身（托管层不校验键缺失时的行为，交由 <see cref="SerializationInfo"/> 抛出）。该构造器被标为 <c>EditorBrowsable(Never)</c>：它面向 <c>BinaryFormatter</c> 类框架，业务代码应使用 <c>JlHomMat2D.Deserialize(Stream)</c> 或 <c>Clone()</c>。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlHomMat2D(SerializationInfo info, StreamingContext context)
	{
		DeserializeHomMat2d((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把矩阵写成库自有二进制格式并写入流。</summary>
	/// <param name="stream">目标流，需可写。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>先 <c>SerializeHomMat2d()</c>（原生 id 235）取字节，再 <c>JlSerializationBuffer.WriteToStream</c> 落流；两次都是完整拷贝，流中自带头部信息。</para>
	///   <para><b>取舍</b></para>
	///   <para>与 <c>JlHomMat2D.Deserialize(Stream)</c> 成对，读回的是新对象。只想拿内存字节用 <c>SerializeHomMat2d</c>/<c>DeserializeHomMat2d</c> 这一对，不必经流。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   using System.IO;
	///   JlHomMat2D m = new JlHomMat2D();
	///   using (MemoryStream ms = new MemoryStream())
	///   {
	///       m.Serialize(ms);
	///       ms.Position = 0;
	///       JlHomMat2D r = JlHomMat2D.Deserialize(ms);
	///   }
	///   </code>
	/// </remarks>
	public void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeHomMat2d(), stream);
	}

	/// <summary>从 <c>Serialize(Stream)</c> 写出的流构造一个新矩阵。</summary>
	/// <param name="stream">源流，格式必须与 <c>Serialize</c> 匹配。</param>
	/// <returns>承载流内容的新实例。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>读取由 <see cref="Serialize(Stream)"/> 写出的库自有二进制负载并返回新对象。流必须可读且游标位于完整负载的起点；格式错误时抛出 <see cref="JlOperatorException"/>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   using System.IO;
	///   using (FileStream fs = new FileStream("mat.bin", FileMode.Open))
	///   {
	///       JlHomMat2D m = JlHomMat2D.Deserialize(fs);
	///   }
	///   </code>
	/// </remarks>
	public static JlHomMat2D Deserialize(Stream stream)
	{
		JlHomMat2D hHomMat2D = new JlHomMat2D();
		hHomMat2D.DeserializeHomMat2d(JlSerializationBuffer.ReadFromStream(stream));
		return hHomMat2D;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <summary>序列化/反序列化往返得到的独立副本（值相同，数据不再共享）。</summary>
	/// <returns>新的 <see cref="JlHomMat2D"/> 实例。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para><c>Clone</c> 走 <c>SerializeHomMat2d()</c> + <c>new JlHomMat2D()</c> + <c>DeserializeHomMat2d(...)</c>，即两次原生调用（id 235、287、234），换来与源对象完全解耦的 tuple。</para>
	///   <para><b>取舍</b></para>
	///   <para>比引用赋值贵；需要"在旧值基础上继续复合、保留本对象"时用 <c>HomMat2dCompose</c> 等复合算子（它们本就返回新对象），只有需要冻结一份现场值供后续比对时才 <c>Clone()</c>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D a = new JlHomMat2D().HomMat2dTranslate(3.0, 4.0);
	///   JlHomMat2D b = a.Clone();
	///   </code>
	/// </remarks>
	public JlHomMat2D Clone()
	{
		byte[] data = SerializeHomMat2d();
		JlHomMat2D obj = new JlHomMat2D();
		obj.DeserializeHomMat2d(data);
		return obj;
	}

	/// <summary>读取 ARC/INFO world 文件中的地理配准参数，结果覆写本实例（原生 id 22）。</summary>
	/// <param name="fileName">world 文件路径（地理配准伴生文件，如 .tfw/.jgw 一类）。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>从 ESRI/ARC INFO world 文件读取六个地理配准参数，生成像素坐标到世界坐标的二维仿射矩阵，并就地覆写本实例。world 文件中的坐标单位由文件及其数据集定义。</para>
	///   <para><b>取舍</b></para>
	///   <para>与 <c>HomMat2dRotate</c> 等复合族不同，它不返回新对象；要保留旧值先 <c>Clone()</c>。仅在配合带 world 文件的 GIS 影像读取流程时使用；纯像素域几何变换不要用本入口。</para>
	///   <para><b>异常与资源</b>文件不存在、不可读或内容无效时抛出 <see cref="JlOperatorException"/>；本实例仍由调用方负责 Dispose。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D geo = new JlHomMat2D();
	///   geo.ReadWorldFile(@"C:\maps\ortho.tfw");
	///   </code>
	/// </remarks>
	public void ReadWorldFile(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(22);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>对本矩阵携带的 3×3 投影（含透视）变换逐点映射 XLD 轮廓，返回新轮廓。</summary>
	/// <param name="contours">输入轮廓。</param>
	/// <returns>变换后的新 <see cref="JlXLDCont"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>原生 id 47；矩阵以 <c>Store(proc,0)</c> 钉住作输入、轮廓在索引 1，<c>InitOCT(1)</c> 声明输出为新的 XLD 句柄；本矩阵不被修改。每个轮廓点按齐次乘法后除以第三分量（w 归一化）落点。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para><c>AffineTransContourXld</c>（id 49）不除 w。若矩阵第三行确为 (0,0,1)，两者结果一致且用仿射版语义更清楚；含透视分量（如单应拼接后的轮廓搬运）才用本算子。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlXLDCont cIn = new JlXLDCont(new JlTuple(new double[] { 10, 20, 30 }), new JlTuple(new double[] { 10, 40, 25 }));
	///   JlHomMat2D h = new JlHomMat2D();
	///   JlXLDCont cOut = h.ProjectiveTransContourXld(cIn);
	///   </code>
	/// </remarks>
	public JlXLDCont ProjectiveTransContourXld(JlXLDCont contours)
	{
		IntPtr proc = JlNativeApi.PreCall(47);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, contours);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
		return obj;
	}

	/// <summary>仿射变换 XLD 多边形：只对顶点做映射，不在边上重新采样（原生 id 48）。</summary>
	/// <param name="polygons">输入多边形轮廓。</param>
	/// <returns>顶点变换后的新 <see cref="JlXLDPoly"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>矩阵 <c>Store(proc,0)</c> 钉住为输入，多边形在索引 1，<c>InitOCT(1)</c> 输出新句柄；本矩阵不变。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>多边形是"顶点+段类型"的压缩表示，大角度旋转/透视下用本算子最便宜，但圆弧近似段的顶点稀疏程度不会随变换加密；要精确搬运曲率细节应转成 <see cref="JlXLDCont"/> 后用 <c>AffineTransContourXld</c>。</para>
	///   <para><b>坑</b></para>
	///   <para>含透视分量的矩阵不应走仿射算子（w 归一化不会发生），此时改用 <c>ProjectiveTransContourXld</c>。</para>
	/// </remarks>
	public JlXLDPoly AffineTransPolygonXld(JlXLDPoly polygons)
	{
		IntPtr proc = JlNativeApi.PreCall(48);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, polygons);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlXLDPoly.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(polygons);
		return obj;
	}

	/// <summary>用 2×3 仿射部分映射 XLD 轮廓的每个点，返回新轮廓（原生 id 49）。</summary>
	/// <param name="contours">输入轮廓。</param>
	/// <returns>变换后的新 <see cref="JlXLDCont"/>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>矩阵 <c>Store(proc,0)</c> 钉住、轮廓在索引 1，输出为新的 XLD 句柄；轮廓点数与分段结构保持，逐点 x'=a11·x+a12·y+a13、y'=a21·x+a22·y+a23。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>这是模型定位/位姿校正后搬运亚像素轮廓的标准入口。不要用它变换区域或图像（用 <c>AffineTransRegion</c>/<c>AffineTransImage</c>，它们做栅格重采样而非逐点映射）；矩阵若含透视分量应改用 <c>ProjectiveTransContourXld</c>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dRotate(0.5236, 100.0, 80.0);
	///   JlXLDCont cIn = new JlXLDCont(new JlTuple(new double[] { 10, 20 }), new JlTuple(new double[] { 10, 40 }));
	///   JlXLDCont cOut = m.AffineTransContourXld(cIn);
	///   </code>
	/// </remarks>
	public JlXLDCont AffineTransContourXld(JlXLDCont contours)
	{
		IntPtr proc = JlNativeApi.PreCall(49);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, contours);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
		return obj;
	}

	/// <summary>用 <c>SerializeHomMat2d()</c> 产出的字节覆写本实例（原生 id 234）。</summary>
	/// <param name="serializedItemHandle">序列化负载字节，须来自同类算子的编码。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>入参经 <c>JlSerializationBuffer</c> 拷入原生内存并在调用结束前持有（<c>GC.KeepAlive(buffer)</c>）；<c>InitOCT(0)</c>+<c>Load(proc,0)</c> 说明矩阵是输出、就地改写。</para>
	///   <para><b>取舍</b></para>
	///   <para>本方法读取由 <see cref="SerializeHomMat2d"/> 或 <see cref="Serialize(Stream)"/> 生成的同一库格式；不接受文本 world 文件或其他类型的序列化负载。</para>
	///   <para><b>异常与状态</b>负载为空、截断或类型不匹配时抛出 <see cref="JlOperatorException"/>。调用成功后当前矩阵被覆写；失败时不要依赖目标对象的内容。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D src = new JlHomMat2D().HomMat2dScale(2.0, 2.0, 0.0, 0.0);
	///   byte[] bytes = src.SerializeHomMat2d();
	///   JlHomMat2D dst = new JlHomMat2D();
	///   dst.DeserializeHomMat2d(bytes);
	///   </code>
	/// </remarks>
	public void DeserializeHomMat2d(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		IntPtr proc = JlNativeApi.PreCall(234);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>把矩阵编码为一个新的 byte[]（原生 id 235）。</summary>
	/// <returns>序列化负载，交给 <c>DeserializeHomMat2d</c> 还原。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>矩阵钉住为输入，<c>JlSerializationBuffer.LoadBytes</c> 把原生句柄里的字节拷回托管数组；每次调用都产生新数组。</para>
	///   <para><b>取舍</b></para>
	///   <para>内存内快照/跨线程传递用它；要落盘或走管道用 <c>Serialize(Stream)</c>。<c>Clone()</c> 内部即本方法与反序列化的往返。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();
	///   byte[] bytes = m.SerializeHomMat2d();
	///   </code>
	/// </remarks>
	public byte[] SerializeHomMat2d()
	{
		IntPtr proc = JlNativeApi.PreCall(235);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>对拼接（mosaic）的全部视图变换做光束法整体平差（静态，原生 id 254）。主重载：逐点平均误差按 <see cref="JlTuple"/> 读回。</summary>
	/// <param name="numImages">参与拼接的图像数。</param>
	/// <param name="referenceImage">基准图索引（0..numImages-1），其余矩阵都相对它摆放。</param>
	/// <param name="mappingSource">每条点对记录的源图索引。</param>
	/// <param name="mappingDest">每条点对记录的目标图索引。</param>
	/// <param name="homMatrices2D">各图的初始 3×3 投影矩阵；实现里先 <c>JlData.ConcatArray</c> 拼成一个长 tuple 再送原生。</param>
	/// <param name="rows1">源图对应点行坐标。</param>
	/// <param name="cols1">源图对应点列坐标。</param>
	/// <param name="rows2">目标图对应点行坐标。</param>
	/// <param name="cols2">目标图对应点列坐标。</param>
	/// <param name="numCorrespondences">每条图像对的点对数，用于切分上面的点对缓冲。</param>
	/// <param name="transformation">变换类型。Default: "projective"</param>
	/// <param name="rows">平差后公共拼接坐标系中的点行坐标。</param>
	/// <param name="cols">平差后公共拼接坐标系中的点列坐标。</param>
	/// <param name="error">每个重建点的平均误差（<c>JlTuple</c> 数组形式）。</param>
	/// <returns>优化后的矩阵数组；原生返回单个长 tuple，由 <c>SplitArray</c> 按 9 元素一段切开。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para><c>InitOCT(0..3)</c> 声明四个输出：0=整体优化后的矩阵串（再切分数组），1/2=重建点行列坐标，3=误差。这是全局优化：一个视图的点对误差会牵动其它视图的矩阵，与逐对图像单独估 <c>VectorToProjHomMat2d</c> 不同。</para>
	///   <para><b>取舍</b></para>
	///   <para>只有两幅图或误差本就互不相干时不必用它（点对点估计+<c>HomMat2dCompose</c> 更简单）。三角化的 <c>rows</c>/<c>cols</c> 是公共坐标系的输出，可用于检查平差质量。</para>
	///   <para><b>坑</b></para>
	///   <para>方法为静态，不涉及 <c>this</c>；<c>numCorrespondences</c> 与 <c>mappingSource</c>/<c>mappingDest</c> 的配对约定错误不会报错，只会得到几何上错乱的拼接。返回数组的矩阵含透视分量，后续只能用 <c>ProjectiveTrans*</c> 族应用。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlTuple mapSrc = new JlTuple(new double[] { 0, 1 });
	///   JlTuple mapDst = new JlTuple(new double[] { 1, 2 });
	///   JlHomMat2D[] init = new JlHomMat2D[] { new JlHomMat2D(), new JlHomMat2D(), new JlHomMat2D() };
	///   JlTuple r1 = new JlTuple(new double[] { 10 }), c1 = new JlTuple(new double[] { 20 });
	///   JlTuple r2 = new JlTuple(new double[] { 12 }), c2 = new JlTuple(new double[] { 23 });
	///   JlTuple n = new JlTuple(new double[] { 1, 1 });
	///   JlHomMat2D[] outMats = JlHomMat2D.BundleAdjustMosaic(3, 0, mapSrc, mapDst, init, r1, c1, r2, c2, n, "projective", out JlTuple rows, out JlTuple cols, out JlTuple err);
	///   </code>
	/// </remarks>
	public static JlHomMat2D[] BundleAdjustMosaic(int numImages, int referenceImage, JlTuple mappingSource, JlTuple mappingDest, JlHomMat2D[] homMatrices2D, JlTuple rows1, JlTuple cols1, JlTuple rows2, JlTuple cols2, JlTuple numCorrespondences, string transformation, out JlTuple rows, out JlTuple cols, out JlTuple error)
	{
		JlTuple tupleValue = JlData.ConcatArray(homMatrices2D);
		IntPtr proc = JlNativeApi.PreCall(254);
		JlNativeApi.StoreI(proc, 0, numImages);
		JlNativeApi.StoreI(proc, 1, referenceImage);
		JlNativeApi.Store(proc, 2, mappingSource);
		JlNativeApi.Store(proc, 3, mappingDest);
		JlNativeApi.Store(proc, 4, tupleValue);
		JlNativeApi.Store(proc, 5, rows1);
		JlNativeApi.Store(proc, 6, cols1);
		JlNativeApi.Store(proc, 7, rows2);
		JlNativeApi.Store(proc, 8, cols2);
		JlNativeApi.Store(proc, 9, numCorrespondences);
		JlNativeApi.StoreS(proc, 10, transformation);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mappingSource);
		JlNativeApi.UnpinTuple(mappingDest);
		JlNativeApi.UnpinTuple(tupleValue);
		JlNativeApi.UnpinTuple(rows1);
		JlNativeApi.UnpinTuple(cols1);
		JlNativeApi.UnpinTuple(rows2);
		JlNativeApi.UnpinTuple(cols2);
		JlNativeApi.UnpinTuple(numCorrespondences);
		err = JlTuple.LoadNew(proc, 0, err, out var data);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out rows);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out cols);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out error);
		JlNativeApi.PostCall(proc, err);
		return SplitArray(data);
	}

	/// <summary>与 <c>JlTuple</c> error 主重载同一原生算子（id 254），唯一实现差异：误差经 <c>LoadD</c> 按单个 <c>double</c> 读回，而非作为 <c>JlTuple</c> 数组。</summary>
	/// <param name="numImages">参与拼接的图像数。</param>
	/// <param name="referenceImage">基准图索引。</param>
	/// <param name="mappingSource">每条点对记录的源图索引。</param>
	/// <param name="mappingDest">每条点对记录的目标图索引。</param>
	/// <param name="homMatrices2D">各图的初始 3×3 投影矩阵。</param>
	/// <param name="rows1">源图对应点行坐标。</param>
	/// <param name="cols1">源图对应点列坐标。</param>
	/// <param name="rows2">目标图对应点行坐标。</param>
	/// <param name="cols2">目标图对应点列坐标。</param>
	/// <param name="numCorrespondences">每条图像对的点对数。</param>
	/// <param name="transformation">变换类型。Default: "projective"</param>
	/// <param name="rows">平差后公共拼接坐标系中的点行坐标。</param>
	/// <param name="cols">平差后公共拼接坐标系中的点列坐标。</param>
	/// <param name="error">每个重建点的平均误差（单个 double）。</param>
	/// <returns>优化后的矩阵数组，详见主重载remarks。</returns>
	/// <remarks>
	///   <para>语义、前提与坑见 <c>JlTuple</c> error 主重载。仅当只要一个汇总误差数字时用本重载；需要逐点误差曲线用主重载。</para>
	/// </remarks>
	public static JlHomMat2D[] BundleAdjustMosaic(int numImages, int referenceImage, JlTuple mappingSource, JlTuple mappingDest, JlHomMat2D[] homMatrices2D, JlTuple rows1, JlTuple cols1, JlTuple rows2, JlTuple cols2, JlTuple numCorrespondences, string transformation, out JlTuple rows, out JlTuple cols, out double error)
	{
		JlTuple tupleValue = JlData.ConcatArray(homMatrices2D);
		IntPtr proc = JlNativeApi.PreCall(254);
		JlNativeApi.StoreI(proc, 0, numImages);
		JlNativeApi.StoreI(proc, 1, referenceImage);
		JlNativeApi.Store(proc, 2, mappingSource);
		JlNativeApi.Store(proc, 3, mappingDest);
		JlNativeApi.Store(proc, 4, tupleValue);
		JlNativeApi.Store(proc, 5, rows1);
		JlNativeApi.Store(proc, 6, cols1);
		JlNativeApi.Store(proc, 7, rows2);
		JlNativeApi.Store(proc, 8, cols2);
		JlNativeApi.Store(proc, 9, numCorrespondences);
		JlNativeApi.StoreS(proc, 10, transformation);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(mappingSource);
		JlNativeApi.UnpinTuple(mappingDest);
		JlNativeApi.UnpinTuple(tupleValue);
		JlNativeApi.UnpinTuple(rows1);
		JlNativeApi.UnpinTuple(cols1);
		JlNativeApi.UnpinTuple(rows2);
		JlNativeApi.UnpinTuple(cols2);
		JlNativeApi.UnpinTuple(numCorrespondences);
		err = JlTuple.LoadNew(proc, 0, err, out var data);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out rows);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out cols);
		err = JlNativeApi.LoadD(proc, 3, err, out error);
		JlNativeApi.PostCall(proc, err);
		return SplitArray(data);
	}









	/// <summary>用点对同时估计投影矩阵与一个径向畸变系数（原生 id 259）；矩阵覆写本实例。</summary>
	/// <param name="points1Row">图 1 点行坐标。</param>
	/// <param name="points1Col">图 1 点列坐标。</param>
	/// <param name="points2Row">图 2 点行坐标。</param>
	/// <param name="points2Col">图 2 点列坐标。</param>
	/// <param name="covRR1">图 1 行方差，空 tuple 表示等权。Default: []</param>
	/// <param name="covRC1">图 1 行/列协方差。Default: []</param>
	/// <param name="covCC1">图 1 列方差。Default: []</param>
	/// <param name="covRR2">图 2 行方差。Default: []</param>
	/// <param name="covRC2">图 2 行/列协方差。Default: []</param>
	/// <param name="covCC2">图 2 列方差。Default: []</param>
	/// <param name="imageWidth">提取这些点的图像宽度（归一化坐标用）。</param>
	/// <param name="imageHeight">提取这些点的图像高度。</param>
	/// <param name="method">估计算法。Default: "gold_standard"</param>
	/// <param name="error">RMS 变换误差。</param>
	/// <returns>估计出的径向畸变系数。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para><c>InitOCT(0..2)</c> 声明三个输出：参数 0=投影矩阵（<c>Load(proc,0)</c> 就地写入本实例）、参数 1=畸变系数（即返回值）、参数 2=<c>out error</c>。<c>imageWidth</c>/<c>imageHeight</c> 必传数值，因为归一化坐标以图像尺寸定义。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>两视图之间除单应外还有近似径向的畸变（广角镜头小基线）时用它；确认无畸变或不在乎残差就用 <c>VectorToProjHomMat2d</c>（它给协方差而不给畸变系数）。cov 六个参数传 <c>new JlTuple()</c> 即等权。</para>
	///   <para><b>坑</b></para>
	///   <para>就地覆写：需要保留原矩阵先 <c>Clone()</c>。点对必须成对对应且数量足以同时估计投影和畸变参数；点分布退化时原生算子会抛出 <see cref="JlOperatorException"/>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D h = new JlHomMat2D();
	///   JlTuple p1r = new JlTuple(new double[] { 5, 100, 500, 490, 250 });
	///   JlTuple p1c = new JlTuple(new double[] { 5, 10, 400, 400, 250 });
	///   JlTuple p2r = new JlTuple(new double[] { 8, 104, 498, 486, 249 });
	///   JlTuple p2c = new JlTuple(new double[] { 7, 14, 396, 402, 251 });
	///   double kappa = h.VectorToProjHomMat2dDistortion(p1r, p1c, p2r, p2c,
	///       new JlTuple(), new JlTuple(), new JlTuple(), new JlTuple(), new JlTuple(), new JlTuple(),
	///       640, 480, "gold_standard", out double err);
	///   </code>
	/// </remarks>
	public double VectorToProjHomMat2dDistortion(JlTuple points1Row, JlTuple points1Col, JlTuple points2Row, JlTuple points2Col, JlTuple covRR1, JlTuple covRC1, JlTuple covCC1, JlTuple covRR2, JlTuple covRC2, JlTuple covCC2, int imageWidth, int imageHeight, string method, out double error)
	{
		IntPtr proc = JlNativeApi.PreCall(259);
		JlNativeApi.Store(proc, 0, points1Row);
		JlNativeApi.Store(proc, 1, points1Col);
		JlNativeApi.Store(proc, 2, points2Row);
		JlNativeApi.Store(proc, 3, points2Col);
		JlNativeApi.Store(proc, 4, covRR1);
		JlNativeApi.Store(proc, 5, covRC1);
		JlNativeApi.Store(proc, 6, covCC1);
		JlNativeApi.Store(proc, 7, covRR2);
		JlNativeApi.Store(proc, 8, covRC2);
		JlNativeApi.Store(proc, 9, covCC2);
		JlNativeApi.StoreI(proc, 10, imageWidth);
		JlNativeApi.StoreI(proc, 11, imageHeight);
		JlNativeApi.StoreS(proc, 12, method);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(points1Row);
		JlNativeApi.UnpinTuple(points1Col);
		JlNativeApi.UnpinTuple(points2Row);
		JlNativeApi.UnpinTuple(points2Col);
		JlNativeApi.UnpinTuple(covRR1);
		JlNativeApi.UnpinTuple(covRC1);
		JlNativeApi.UnpinTuple(covCC1);
		JlNativeApi.UnpinTuple(covRR2);
		JlNativeApi.UnpinTuple(covRC2);
		JlNativeApi.UnpinTuple(covCC2);
		err = Load(proc, 0, err);
		err = JlNativeApi.LoadD(proc, 1, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 2, err, out error);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>用齐次点对（带第三维 w）估计投影矩阵，结果覆写本实例（原生 id 260）。</summary>
	/// <param name="px">源点齐次坐标 x 分量。</param>
	/// <param name="py">源点齐次坐标 y 分量。</param>
	/// <param name="pw">源点齐次坐标 w 分量。</param>
	/// <param name="qx">目标点齐次坐标 x 分量。</param>
	/// <param name="qy">目标点齐次坐标 y 分量。</param>
	/// <param name="qw">目标点齐次坐标 w 分量。</param>
	/// <param name="method">估计算法。Default: "normalized_dlt"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>六个坐标各按一个 <see cref="JlTuple"/> 钉住写入参数 0..5（<c>Store</c> 后统一 <c>UnpinTuple</c>），<c>InitOCT(0)</c>+<c>Load(proc,0)</c>：唯一输出是矩阵本身，覆写 <c>this</c>。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>与 <c>VectorToProjHomMat2d</c>（id 261）的输入取向不同：本算子吃 w≠1 的齐次点（例如从空间直线/平面反投影得到的点），261 只吃 2D 像素对并额外给协方差。普通点对配准用 261。</para>
	///   <para><b>坑</b></para>
	///   <para>无误差输出，估计质量要自行用 <c>AffineTransPoint2d</c>/<c>ProjectiveTransPoint2d</c> 回代检查；就地覆写，保留旧值先 <c>Clone()</c>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D h = new JlHomMat2D();
	///   JlTuple one = new JlTuple(1.0);
	///   h.HomVectorToProjHomMat2d(new JlTuple(0.0), new JlTuple(0.0), one,
	///       new JlTuple(1.0), new JlTuple(2.0), one, "normalized_dlt");
	///   </code>
	/// </remarks>
	public void HomVectorToProjHomMat2d(JlTuple px, JlTuple py, JlTuple pw, JlTuple qx, JlTuple qy, JlTuple qw, string method)
	{
		IntPtr proc = JlNativeApi.PreCall(260);
		JlNativeApi.Store(proc, 0, px);
		JlNativeApi.Store(proc, 1, py);
		JlNativeApi.Store(proc, 2, pw);
		JlNativeApi.Store(proc, 3, qx);
		JlNativeApi.Store(proc, 4, qy);
		JlNativeApi.Store(proc, 5, qw);
		JlNativeApi.StoreS(proc, 6, method);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		JlNativeApi.UnpinTuple(pw);
		JlNativeApi.UnpinTuple(qx);
		JlNativeApi.UnpinTuple(qy);
		JlNativeApi.UnpinTuple(qw);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>用 2D 点对估计投影矩阵（覆写本实例）并返回其协方差（原生 id 261）。</summary>
	/// <param name="px">图 1 点行坐标（文档语义如此，x=row）。</param>
	/// <param name="py">图 1 点列坐标。</param>
	/// <param name="qx">图 2 点行坐标。</param>
	/// <param name="qy">图 2 点列坐标。</param>
	/// <param name="method">估计算法。Default: "normalized_dlt"</param>
	/// <param name="covXX1">图 1 行方差，空 tuple 表示等权。Default: []</param>
	/// <param name="covYY1">图 1 列方差。Default: []</param>
	/// <param name="covXY1">图 1 行/列协方差。Default: []</param>
	/// <param name="covXX2">图 2 行方差。Default: []</param>
	/// <param name="covYY2">图 2 列方差。Default: []</param>
	/// <param name="covXY2">图 2 行/列协方差。Default: []</param>
	/// <returns>矩阵的协方差（<c>InitOCT(1)</c>+<c>LoadNew(..., DOUBLE)</c>，9 参数展平后为 81 个 double）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>两路输出：<c>Load(proc,0)</c> 把估计的 8 自由度投影矩阵就地写入本实例，返回值是 9×9 协方差，供传播定位不确定度。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只需把点对映过去而不管透视，用 6 自由度的 <c>VectorToHomMat2d</c>（id 268）；存在透视（相机倾斜看平面）时仿射模型系统性失配，才用本算子。要连畸变一起估用 <c>VectorToProjHomMat2dDistortion</c>。</para>
	///   <para><b>坑</b></para>
	///   <para>至少需要足够的非退化点对来估计 8 个投影自由度；点接近共线或点数不足时抛出 <see cref="JlOperatorException"/>。协方差按 9×9 行优先矩阵展开为 81 个元素。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D h = new JlHomMat2D();
	///   JlTuple cov = h.VectorToProjHomMat2d(
	///       new JlTuple(new double[] { 10, 100, 300, 40 }), new JlTuple(new double[] { 20, 30, 200, 400 }),
	///       new JlTuple(new double[] { 12, 103, 297, 41 }), new JlTuple(new double[] { 21, 33, 197, 402 }),
	///       "normalized_dlt", new JlTuple(), new JlTuple(), new JlTuple(), new JlTuple(), new JlTuple(), new JlTuple());
	///   </code>
	/// </remarks>
	public JlTuple VectorToProjHomMat2d(JlTuple px, JlTuple py, JlTuple qx, JlTuple qy, string method, JlTuple covXX1, JlTuple covYY1, JlTuple covXY1, JlTuple covXX2, JlTuple covYY2, JlTuple covXY2)
	{
		IntPtr proc = JlNativeApi.PreCall(261);
		JlNativeApi.Store(proc, 0, px);
		JlNativeApi.Store(proc, 1, py);
		JlNativeApi.Store(proc, 2, qx);
		JlNativeApi.Store(proc, 3, qy);
		JlNativeApi.StoreS(proc, 4, method);
		JlNativeApi.Store(proc, 5, covXX1);
		JlNativeApi.Store(proc, 6, covYY1);
		JlNativeApi.Store(proc, 7, covXY1);
		JlNativeApi.Store(proc, 8, covXX2);
		JlNativeApi.Store(proc, 9, covYY2);
		JlNativeApi.Store(proc, 10, covXY2);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		JlNativeApi.UnpinTuple(qx);
		JlNativeApi.UnpinTuple(qy);
		JlNativeApi.UnpinTuple(covXX1);
		JlNativeApi.UnpinTuple(covYY1);
		JlNativeApi.UnpinTuple(covXY1);
		JlNativeApi.UnpinTuple(covXX2);
		JlNativeApi.UnpinTuple(covYY2);
		JlNativeApi.UnpinTuple(covXY2);
		err = Load(proc, 0, err);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out var result);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return result;
	}

	/// <summary>把矩阵分解成平移+旋转+斜切+缩放的仿射参数（原生 id 262）；sx 走返回值。</summary>
	/// <param name="sy">y 方向缩放因子。</param>
	/// <param name="phi">旋转角（弧度）。</param>
	/// <param name="theta">斜切角。</param>
	/// <param name="tx">x（列）方向平移。</param>
	/// <param name="ty">y（行）方向平移。</param>
	/// <returns>x 方向缩放因子（输出 0）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>六个输出经 <c>InitOCT(0..5)</c> 声明、<c>LoadD</c> 逐个读回：0=sx（返回值）、1..5 依次是 <c>out</c> 的 sy/phi/theta/tx/ty。这是"读语义"算子：给出缩放、转角、错切角，便于输出报告或按参数微调位姿。</para>
	///   <para><b>前提</b></para>
	///   <para>只接受仿射矩阵，即第三行应为 <c>[0,0,1]</c>；含透视项的矩阵会导致算子异常。返回值按 HALCON 的仿射参数定义给出缩放、旋转、斜切和平移分量。</para>
	///   <para><b>取舍</b></para>
	///   <para>若只是继续复合变换，别分解再重拼（参数化重组可能引入偏差）；求逆搬运用 <c>HomMat2dInvert</c>，不要用"参数取负"凑逆。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dScale(2.0, 1.5, 0.0, 0.0).HomMat2dRotate(0.3, 5.0, 5.0);
	///   double sx = m.HomMat2dToAffinePar(out double sy, out double phi, out double theta, out double tx, out double ty);
	///   </code>
	/// </remarks>
	public double HomMat2dToAffinePar(out double sy, out double phi, out double theta, out double tx, out double ty)
	{
		IntPtr proc = JlNativeApi.PreCall(262);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out sy);
		err = JlNativeApi.LoadD(proc, 2, err, out phi);
		err = JlNativeApi.LoadD(proc, 3, err, out theta);
		err = JlNativeApi.LoadD(proc, 4, err, out tx);
		err = JlNativeApi.LoadD(proc, 5, err, out ty);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>用点对+姿态角以最小二乘估计刚体（旋转+平移）变换，覆写本实例（原生 id 263，主重载）。</summary>
	/// <param name="row1">原点位行坐标列表。</param>
	/// <param name="column1">原点位列坐标列表。</param>
	/// <param name="angle1">原点姿态角列表（弧度）。</param>
	/// <param name="row2">变换后位行坐标列表。</param>
	/// <param name="column2">变换后位列坐标列表。</param>
	/// <param name="angle2">变换后位姿态角列表（弧度）。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>六个 <see cref="JlTuple"/> 钉住写入参数 0..5，<c>InitOCT(0)</c>+<c>Load(proc,0)</c> 单输出覆写 <c>this</c>。本重载把六个量都作为数组送入：N 对"位置+朝向"一起做最小二乘刚体拟合。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>与 <c>VectorToRigid</c>（id 265）的区别是这里额外使用姿态角约束：位置和方向都已知时用本算子，否则只使用位置约束。刚体模型不包含缩放；角度单位为弧度，并遵循 HALCON 的坐标轴约定（图像行作为 x、列作为 y）。</para>
	///   <para><b>坑</b></para>
	///   <para>就地覆写；需要保留旧值先 <c>Clone()</c>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();
	///   m.VectorAngleToRigid(new JlTuple(new double[] { 100, 200 }), new JlTuple(new double[] { 50, 60 }), new JlTuple(new double[] { 0.1, 0.2 }),
	///       new JlTuple(new double[] { 110, 190 }), new JlTuple(new double[] { 55, 70 }), new JlTuple(new double[] { 0.4, 0.5 }));
	///   </code>
	/// </remarks>
	public void VectorAngleToRigid(JlTuple row1, JlTuple column1, JlTuple angle1, JlTuple row2, JlTuple column2, JlTuple angle2)
	{
		IntPtr proc = JlNativeApi.PreCall(263);
		JlNativeApi.Store(proc, 0, row1);
		JlNativeApi.Store(proc, 1, column1);
		JlNativeApi.Store(proc, 2, angle1);
		JlNativeApi.Store(proc, 3, row2);
		JlNativeApi.Store(proc, 4, column2);
		JlNativeApi.Store(proc, 5, angle2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(row1);
		JlNativeApi.UnpinTuple(column1);
		JlNativeApi.UnpinTuple(angle1);
		JlNativeApi.UnpinTuple(row2);
		JlNativeApi.UnpinTuple(column2);
		JlNativeApi.UnpinTuple(angle2);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary><c>JlTuple</c> 主重载的单点版本（同一原生 id 263）：六个量全部经 <c>StoreD</c> 以标量写入，无钉桩/<c>UnpinTuple</c> 步骤；语义与取舍见主重载。</summary>
	/// <param name="row1">原点位行坐标。</param>
	/// <param name="column1">原点位列坐标。</param>
	/// <param name="angle1">原点姿态角（弧度）。</param>
	/// <param name="row2">变换后位行坐标。</param>
	/// <param name="column2">变换后位列坐标。</param>
	/// <param name="angle2">变换后位姿态角（弧度）。</param>
	/// <remarks>
	///   <para>单对"位置+朝向"直接定刚体位姿（3 自由度由 1 点 + 1 角给足），适合模型位姿 → 实测位姿一次换算；批量标定值请走 <c>JlTuple</c> 重载做最小二乘。</para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();
	///   m.VectorAngleToRigid(100.0, 50.0, 0.0, 120.0, 60.0, 0.5236);
	///   </code>
	/// </remarks>
	public void VectorAngleToRigid(double row1, double column1, double angle1, double row2, double column2, double angle2)
	{
		IntPtr proc = JlNativeApi.PreCall(263);
		JlNativeApi.StoreD(proc, 0, row1);
		JlNativeApi.StoreD(proc, 1, column1);
		JlNativeApi.StoreD(proc, 2, angle1);
		JlNativeApi.StoreD(proc, 3, row2);
		JlNativeApi.StoreD(proc, 4, column2);
		JlNativeApi.StoreD(proc, 5, angle2);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>按指定变换类型，用"点 ↔ 直线"对应估计变换矩阵，覆写本实例（原生 id 264）。</summary>
	/// <param name="transformationType">变换类型（决定自由度）。Default: "rigid"</param>
	/// <param name="px">原始点 x 坐标。</param>
	/// <param name="py">原始点 y 坐标。</param>
	/// <param name="l1x">对应直线第一点 x 坐标。</param>
	/// <param name="l1y">对应直线第一点 y 坐标。</param>
	/// <param name="l2x">对应直线第二点 x 坐标。</param>
	/// <param name="l2y">对应直线第二点 y 坐标。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para><c>StoreS(proc,0,transformationType)</c> 写类型串，六个坐标按参数 1..6 钉住；<c>InitOCT(0)</c>+<c>Load(proc,0)</c> 单输出覆写 <c>this</c>。每条对应提供的是"变换后的点落在给定直线上"这一残差，适合亚像素边缘点只知位于某条直线（沿法向未定）的装配场景。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>点对应完整可得时（位置两维都有）直接用 <c>VectorToRigid</c>/<c>VectorToHomMat2d</c>，信息量更高、解更稳。注意本算子 <c>transformationType</c> 是第一个参数，与其它 <c>VectorTo*</c>（无类型参数或类型在尾）不同。</para>
	///   <para><b>坑</b></para>
	///   <para>当前强类型重载只公开 <c>rigid</c> 类型。点和直线元组必须按索引一一对应，并提供足够的非退化约束；不满足时抛出 <see cref="JlOperatorException"/>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();
	///   m.PointLineToHomMat2d("rigid", new JlTuple(new double[] { 10, 20 }), new JlTuple(new double[] { 10, 20 }),
	///       new JlTuple(new double[] { 5, 5 }), new JlTuple(new double[] { 6, 6 }),
	///       new JlTuple(new double[] { 50, 60 }), new JlTuple(new double[] { 60, 70 }));
	///   </code>
	/// </remarks>
	public void PointLineToHomMat2d(string transformationType, JlTuple px, JlTuple py, JlTuple l1x, JlTuple l1y, JlTuple l2x, JlTuple l2y)
	{
		IntPtr proc = JlNativeApi.PreCall(264);
		JlNativeApi.StoreS(proc, 0, transformationType);
		JlNativeApi.Store(proc, 1, px);
		JlNativeApi.Store(proc, 2, py);
		JlNativeApi.Store(proc, 3, l1x);
		JlNativeApi.Store(proc, 4, l1y);
		JlNativeApi.Store(proc, 5, l2x);
		JlNativeApi.Store(proc, 6, l2y);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		JlNativeApi.UnpinTuple(l1x);
		JlNativeApi.UnpinTuple(l1y);
		JlNativeApi.UnpinTuple(l2x);
		JlNativeApi.UnpinTuple(l2y);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>使用强类型刚体模型从点线约束估计矩阵。</summary>
	public void PointLineToHomMat2d(JlPointLineTransformType transformationType, JlTuple px, JlTuple py, JlTuple l1x, JlTuple l1y, JlTuple l2x, JlTuple l2y)
	{
		PointLineToHomMat2d(transformationType.ToNative(), px, py, l1x, l1y, l2x, l2y);
	}

	/// <summary>由完整点对近似刚体变换（仅旋转+平移，3 自由度），覆写本实例（原生 id 265）。</summary>
	/// <param name="px">原始点 x 坐标。</param>
	/// <param name="py">原始点 y 坐标。</param>
	/// <param name="qx">目标点 x 坐标。</param>
	/// <param name="qy">目标点 y 坐标。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>四个坐标 tuple 钉住写入参数 0..3，<c>InitOCT(0)</c>+<c>Load(proc,0)</c> 单输出覆写 <c>this</c>；x/y 按参数名对应列/行向。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>实测存在等比缩放时别硬套刚体（残差会被摊进旋转导致角度偏斜），换 <c>VectorToSimilarity</c>；各向异性缩放用 <c>VectorToAniso</c>；一般形变用 <c>VectorToHomMat2d</c>。与 <c>VectorAngleToRigid</c>（id 263）相比少了姿态角约束，点对必须给足。</para>
	///   <para><b>坑</b></para>
	///   <para>2 对点即可定解、多点做最小二乘；两组数组的索引必须严格配对，错配不报错只出错位。就地覆写，保留旧值先 <c>Clone()</c>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();
	///   m.VectorToRigid(new JlTuple(new double[] { 0, 100 }), new JlTuple(new double[] { 0, 0 }),
	///       new JlTuple(new double[] { 5, 106 }), new JlTuple(new double[] { -3, 2 }));
	///   </code>
	/// </remarks>
	public void VectorToRigid(JlTuple px, JlTuple py, JlTuple qx, JlTuple qy)
	{
		IntPtr proc = JlNativeApi.PreCall(265);
		JlNativeApi.Store(proc, 0, px);
		JlNativeApi.Store(proc, 1, py);
		JlNativeApi.Store(proc, 2, qx);
		JlNativeApi.Store(proc, 3, qy);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		JlNativeApi.UnpinTuple(qx);
		JlNativeApi.UnpinTuple(qy);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>由点对近似相似变换（旋转+平移+各向同性缩放，4 自由度），覆写本实例（原生 id 266）。</summary>
	/// <param name="px">原始点 x 坐标。</param>
	/// <param name="py">原始点 y 坐标。</param>
	/// <param name="qx">目标点 x 坐标。</param>
	/// <param name="qy">目标点 y 坐标。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>参数与 id 265 同形（钉住 0..3，单输出覆写 <c>this</c>），多出的自由度是一个统一缩放 s。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>同一物体因工作距离变化整体变大变小用它；x/y 缩放不等（如相机倾斜看平面）时应升级到 <c>VectorToAniso</c> 或 <c>VectorToHomMat2d</c>，用相似模型会把各向异性残差摊到角度上。已知无缩放时用 <c>VectorToRigid</c>，避免 s 吸收噪声。</para>
	///   <para><b>坑</b></para>
	///   <para>相似变换至少需要两对不重合的点；点重合或点对退化时抛出 <see cref="JlOperatorException"/>。点对按相同下标配对。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();
	///   m.VectorToSimilarity(new JlTuple(new double[] { 0, 100 }), new JlTuple(new double[] { 0, 0 }),
	///       new JlTuple(new double[] { 0, 200 }), new JlTuple(new double[] { 0, 0 }));   // 等比放大 2 倍
	///   </code>
	/// </remarks>
	public void VectorToSimilarity(JlTuple px, JlTuple py, JlTuple qx, JlTuple qy)
	{
		IntPtr proc = JlNativeApi.PreCall(266);
		JlNativeApi.Store(proc, 0, px);
		JlNativeApi.Store(proc, 1, py);
		JlNativeApi.Store(proc, 2, qx);
		JlNativeApi.Store(proc, 3, qy);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		JlNativeApi.UnpinTuple(qx);
		JlNativeApi.UnpinTuple(qy);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>由点对近似各向异性相似变换（x/y 独立缩放、无斜切，5 自由度），覆写本实例（原生 id 267）。</summary>
	/// <param name="px">原始点 x 坐标。</param>
	/// <param name="py">原始点 y 坐标。</param>
	/// <param name="qx">目标点 x 坐标。</param>
	/// <param name="qy">目标点 y 坐标。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>钉住参数 0..3，单输出覆写 <c>this</c>；比 <c>VectorToSimilarity</c>（id 266）多一个独立缩放分量，但仍假设坐标轴正交。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>像素非方形或机械双轴增益不等时用它代替全仿射，少一个自由度、少一份噪声；一旦存在剪切或透视（266/267 的模型假设被破坏）改用 <c>VectorToHomMat2d</c> 或 <c>VectorToProjHomMat2d</c>。</para>
	///   <para><b>坑</b></para>
	///   <para>参数按 HALCON 的笛卡尔约定解释：x 对应行坐标，y 对应列坐标；至少需要两对不重合点来估计两个独立缩放分量。点对索引必须配对。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();
	///   m.VectorToAniso(new JlTuple(new double[] { 0, 100 }), new JlTuple(new double[] { 0, 100 }),
	///       new JlTuple(new double[] { 0, 200 }), new JlTuple(new double[] { 0, 150 }));   // sx=2, sy=1.5
	///   </code>
	/// </remarks>
	public void VectorToAniso(JlTuple px, JlTuple py, JlTuple qx, JlTuple qy)
	{
		IntPtr proc = JlNativeApi.PreCall(267);
		JlNativeApi.Store(proc, 0, px);
		JlNativeApi.Store(proc, 1, py);
		JlNativeApi.Store(proc, 2, qx);
		JlNativeApi.Store(proc, 3, qy);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		JlNativeApi.UnpinTuple(qx);
		JlNativeApi.UnpinTuple(qy);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>由点对估计一般仿射矩阵（6 自由度），覆写本实例（原生 id 268）。</summary>
	/// <param name="px">原始点 x 坐标。</param>
	/// <param name="py">原始点 y 坐标。</param>
	/// <param name="qx">目标点 x 坐标。</param>
	/// <param name="qy">目标点 y 坐标。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>钉住参数 0..3、单输出覆写 <c>this</c>；估计结果第三行为 (0,0,1)，可直接喂给 <c>AffineTrans*</c> 全族。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>刚体/等比/各向异性对应关系明确时逐级降档（265/266/267），自由度越少抗噪越强；两视图含透视（倾斜平面、镜头畸变明显）时仿射表达不了，用 <c>VectorToProjHomMat2d</c>（261）或带畸变的 259。</para>
	///   <para><b>坑</b></para>
	///   <para>至少需要三对不共线点来确定 6 个仿射自由度；点对按索引配对，错配会影响结果但不会被几何模型自动纠正。就地覆写，保留旧值先 <c>Clone()</c>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();
	///   m.VectorToHomMat2d(new JlTuple(new double[] { 0, 100, 0 }), new JlTuple(new double[] { 0, 0, 100 }),
	///       new JlTuple(new double[] { 10, 110, 5 }), new JlTuple(new double[] { 20, 25, 120 }));
	///   </code>
	/// </remarks>
	public void VectorToHomMat2d(JlTuple px, JlTuple py, JlTuple qx, JlTuple qy)
	{
		IntPtr proc = JlNativeApi.PreCall(268);
		JlNativeApi.Store(proc, 0, px);
		JlNativeApi.Store(proc, 1, py);
		JlNativeApi.Store(proc, 2, qx);
		JlNativeApi.Store(proc, 3, qy);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		JlNativeApi.UnpinTuple(qx);
		JlNativeApi.UnpinTuple(qy);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>按投影（单应）映射像素坐标，逐点做 w 归一化；批量 <see cref="JlTuple"/> 主重载（原生 id 269）。</summary>
	/// <param name="row">输入行坐标数组。Default: 64</param>
	/// <param name="col">输入列坐标数组。Default: 64</param>
	/// <param name="rowTrans">输出行坐标（新 <c>JlTuple</c>，DOUBLE 型）。</param>
	/// <param name="colTrans">输出列坐标（新 <c>JlTuple</c>）。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>矩阵使用行优先元素 <c>[a11,a12,a13,a21,a22,a23,a31,a32,a33]</c>。对图像点 <c>(row,col)</c>，先计算齐次乘积，再用第三分量归一化；输出仍按 <c>(row,col)</c> 返回。第三分量为零的点无法投影，原生算子会抛出异常。</para>
	///   <para><b>与 double 重载差异</b></para>
	///   <para>本重载输入数组逐点求解、输出为 tuple；double 重载用 <c>StoreD</c> 写入、无钉桩/解钉步骤。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>矩阵确定无透视（第三行 0,0,1）时用 <c>AffineTransPixel</c>（id 271），少一次除法分配；单个点取值用 double 重载。</para>
	///   <para><b>坑</b></para>
	///   <para>落在消失线（w=0）附近的点会爆炸或无意义；矩阵只读不改。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D h = new JlHomMat2D();
	///   h.VectorToProjHomMat2d(new JlTuple(new double[] { 10, 100, 300, 40 }), new JlTuple(new double[] { 20, 30, 200, 400 }),
	///       new JlTuple(new double[] { 12, 103, 297, 41 }), new JlTuple(new double[] { 21, 33, 197, 402 }),
	///       "normalized_dlt", new JlTuple(), new JlTuple(), new JlTuple(), new JlTuple(), new JlTuple(), new JlTuple());
	///   h.ProjectiveTransPixel(new JlTuple(new double[] { 50, 150 }), new JlTuple(new double[] { 60, 160 }),
	///       out JlTuple rt, out JlTuple ct);
	///   </code>
	/// </remarks>
	public void ProjectiveTransPixel(JlTuple row, JlTuple col, out JlTuple rowTrans, out JlTuple colTrans)
	{
		IntPtr proc = JlNativeApi.PreCall(269);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, col);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(col);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowTrans);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out colTrans);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>投影映射像素坐标的标量重载（同一原生 id 269）：坐标经 <c>StoreD</c> 写入、经 <c>LoadD</c> 读回，无钉桩开销。</summary>
	/// <param name="row">输入行坐标。Default: 64</param>
	/// <param name="col">输入列坐标。Default: 64</param>
	/// <param name="rowTrans">输出行坐标。</param>
	/// <param name="colTrans">输出列坐标。</param>
	/// <remarks>
	///   <para>语义、消失线警告见 <c>JlTuple</c> 主重载；仅当只需变换一个点时用本重载，避免分配 tuple。</para>
	///   <code>
	///   JlHomMat2D h = new JlHomMat2D();
	///   h.ProjectiveTransPixel(64.0, 64.0, out double rt, out double ct);
	///   </code>
	/// </remarks>
	public void ProjectiveTransPixel(double row, double col, out double rowTrans, out double colTrans)
	{
		IntPtr proc = JlNativeApi.PreCall(269);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, col);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlNativeApi.LoadD(proc, 0, err, out rowTrans);
		err = JlNativeApi.LoadD(proc, 1, err, out colTrans);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>投影映射齐次 2D 点：保留输出第三分量 qw、不做归一化；批量 <see cref="JlTuple"/> 主重载（原生 id 270）。</summary>
	/// <param name="px">输入点 x 分量。</param>
	/// <param name="py">输入点 y 分量。</param>
	/// <param name="pw">输入点 w 分量。</param>
	/// <param name="qy">输出点 y 分量（新 <c>JlTuple</c>）。</param>
	/// <param name="qw">输出点齐次分量（新 <c>JlTuple</c>）。</param>
	/// <returns>输出点 x 分量（新 <c>JlTuple</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>三个输出按 <c>InitOCT(0..2)</c> 声明：<c>qx</c> 走返回值、<c>qy</c>/<c>qw</c> 走 <c>out</c>——取向与 <c>AffineTransPoint2d</c>（两个输出：qx 返回值+qy）不同，多出的 <c>qw</c> 是留作后续齐次复用的关键。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只要屏幕像素坐标时用 <c>ProjectiveTransPixel</c>（自动除 w，输入是 row/col 而非 x/y/w）；本算子适合继续带着 w 做链式齐次运算或判断无穷远点。仿射矩阵且不需要 w 用 <c>AffineTransPoint2d</c>。</para>
	///   <para><b>与 double 重载差异</b></para>
	///   <para>本重载钉桩输入并返回三元数组；double 重载 <c>StoreD</c>/<c>LoadD</c> 单点无钉桩。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D h = new JlHomMat2D();
	///   JlTuple qx = h.ProjectiveTransPoint2d(new JlTuple(1.0), new JlTuple(2.0), new JlTuple(1.0),
	///       out JlTuple qy, out JlTuple qw);
	///   </code>
	/// </remarks>
	public JlTuple ProjectiveTransPoint2d(JlTuple px, JlTuple py, JlTuple pw, out JlTuple qy, out JlTuple qw)
	{
		IntPtr proc = JlNativeApi.PreCall(270);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, px);
		JlNativeApi.Store(proc, 2, py);
		JlNativeApi.Store(proc, 3, pw);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		JlNativeApi.UnpinTuple(pw);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var result);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out qy);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out qw);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return result;
	}

	/// <summary>齐次点投影映射的标量重载（同一原生 id 270）：<c>StoreD</c> 写入、<c>LoadD</c> 读回 qx/qy/qw，无钉桩。</summary>
	/// <param name="px">输入点 x 分量。</param>
	/// <param name="py">输入点 y 分量。</param>
	/// <param name="pw">输入点 w 分量。</param>
	/// <param name="qy">输出点 y 分量。</param>
	/// <param name="qw">输出点齐次分量。</param>
	/// <returns>输出点 x 分量。</returns>
	/// <remarks>
	///   <para>输出取向（<c>qx</c> 返回值 + 两个 <c>out</c>）与主重载一致；语义与选型见 <c>JlTuple</c> 主重载。</para>
	///   <code>
	///   JlHomMat2D h = new JlHomMat2D();
	///   double qx = h.ProjectiveTransPoint2d(1.0, 2.0, 1.0, out double qy, out double qw);
	///   </code>
	/// </remarks>
	public double ProjectiveTransPoint2d(double px, double py, double pw, out double qy, out double qw)
	{
		IntPtr proc = JlNativeApi.PreCall(270);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, px);
		JlNativeApi.StoreD(proc, 2, py);
		JlNativeApi.StoreD(proc, 3, pw);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out qy);
		err = JlNativeApi.LoadD(proc, 2, err, out qw);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>按仿射映射批量变换像素坐标（不做 w 除法）；<see cref="JlTuple"/> 主重载（原生 id 271）。</summary>
	/// <param name="row">输入行坐标数组。Default: 64</param>
	/// <param name="col">输入列坐标数组。Default: 64</param>
	/// <param name="rowTrans">输出行坐标（新 <c>JlTuple</c>，DOUBLE 型）。</param>
	/// <param name="colTrans">输出列坐标（新 <c>JlTuple</c>）。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>矩阵只读不改，使用行优先元素 <c>[a11,a12,a13,a21,a22,a23]</c> 对每个 <c>(row,col)</c> 做仿射映射；结果按同一行、列约定返回。含透视项的矩阵应改用 <see cref="ProjectiveTransPixel(JlTuple, JlTuple, out JlTuple, out JlTuple)"/>。</para>
	///   <para><b>与 double 重载差异</b></para>
	///   <para>本重载输入为数组、需要钉桩并在调用后解钉；double 重载走 <c>StoreD</c>/<c>LoadD</c>，单点无此开销。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>批量搬运标定格点、测点集的默认入口。矩阵若由 <c>VectorToProjHomMat2d</c>/<c>BundleAdjustMosaic</c> 得到、可能含透视，用本算子会悄悄丢透视项，此时用 <c>ProjectiveTransPixel</c>；只要一个点用 double 重载；需要保留齐次分量则用 <c>ProjectiveTransPoint2d</c>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dRotate(0.7854, 256.0, 256.0);
	///   m.AffineTransPixel(new JlTuple(new double[] { 100, 200 }), new JlTuple(new double[] { 100, 300 }),
	///       out JlTuple rowsOut, out JlTuple colsOut);
	///   </code>
	/// </remarks>
	public void AffineTransPixel(JlTuple row, JlTuple col, out JlTuple rowTrans, out JlTuple colTrans)
	{
		IntPtr proc = JlNativeApi.PreCall(271);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, row);
		JlNativeApi.Store(proc, 2, col);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(row);
		JlNativeApi.UnpinTuple(col);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out rowTrans);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out colTrans);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>仿射映射像素坐标的标量重载（同一原生 id 271）：<c>StoreD</c> 写入、<c>LoadD</c> 读回，无钉桩/解钉步骤。</summary>
	/// <param name="row">输入行坐标。Default: 64</param>
	/// <param name="col">输入列坐标。Default: 64</param>
	/// <param name="rowTrans">输出行坐标。</param>
	/// <param name="colTrans">输出列坐标。</param>
	/// <remarks>
	///   <para>语义与选型见 <c>JlTuple</c> 主重载；单个像素坐标查询用本重载。</para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(3.0, 4.0);
	///   m.AffineTransPixel(10.0, 20.0, out double rowTrans, out double colTrans);
	///   </code>
	/// </remarks>
	public void AffineTransPixel(double row, double col, out double rowTrans, out double colTrans)
	{
		IntPtr proc = JlNativeApi.PreCall(271);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, col);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlNativeApi.LoadD(proc, 0, err, out rowTrans);
		err = JlNativeApi.LoadD(proc, 1, err, out colTrans);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>仿射映射数学坐标点，批量 <see cref="JlTuple"/> 主重载（原生 id 272）；qx 走返回值、qy 走 <c>out</c>。</summary>
	/// <param name="px">输入点 x/行坐标数组。Default: 64</param>
	/// <param name="py">输入点 y/列坐标数组。Default: 64</param>
	/// <param name="qy">输出点 y/列坐标（新 <c>JlTuple</c>，DOUBLE 型）。</param>
	/// <returns>输出点 x/行坐标（新 <c>JlTuple</c>）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>矩阵和输入点只读；本库沿用 HALCON 的坐标约定，参数名 <c>px/py</c> 也可理解为 <c>row/column</c>。输出点与输入点一一对应，结果元组按 DOUBLE 返回。</para>
	///   <para><b>与 double 重载差异</b></para>
	///   <para>本重载数组进数组出、含钉桩/解钉；double 重载 <c>StoreD</c>/<c>LoadD</c>、单点零分配。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>坐标语义与 <c>AffineTransPixel</c>（id 271，明确 row/col 进、row/col 出、两路 <c>out</c>）二选一：本算子的返回值+单 <c>out</c> 取向适合内联表达式；需要齐次 w 用 <c>ProjectiveTransPoint2d</c>；矩阵含透视时别用仿射映射。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dScale(2.0, 2.0, 0.0, 0.0);
	///   JlTuple qx = m.AffineTransPoint2d(new JlTuple(new double[] { 10, 20 }), new JlTuple(new double[] { 30, 40 }), out JlTuple qy);
	///   </code>
	/// </remarks>
	public JlTuple AffineTransPoint2d(JlTuple px, JlTuple py, out JlTuple qy)
	{
		IntPtr proc = JlNativeApi.PreCall(272);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, px);
		JlNativeApi.Store(proc, 2, py);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.DOUBLE, err, out var result);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out qy);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return result;
	}

	/// <summary>仿射映射单点（同一原生 id 272）：<c>StoreD</c> 写坐标、<c>LoadD</c> 读回，qx 走返回值、qy 走 <c>out</c>，无钉桩。</summary>
	/// <param name="px">输入点 x/行坐标。Default: 64</param>
	/// <param name="py">输入点 y/列坐标。Default: 64</param>
	/// <param name="qy">输出点 y/列坐标。</param>
	/// <returns>输出点 x/行坐标。</returns>
	/// <remarks>
	///   <para>语义与选型见 <c>JlTuple</c> 主重载；单点查询（含在循环里逐点调用）用本重载。</para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(10.0, 0.0);
	///   double qx = m.AffineTransPoint2d(5.0, 7.0, out double qy);   // 结果按 HALCON 的 x/y（row/column）约定解释
	///   </code>
	/// </remarks>
	public double AffineTransPoint2d(double px, double py, out double qy)
	{
		IntPtr proc = JlNativeApi.PreCall(272);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, px);
		JlNativeApi.StoreD(proc, 2, py);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		err = JlNativeApi.LoadD(proc, 1, err, out qy);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>返回矩阵的行列式（原生 id 273），不修改本实例。</summary>
	/// <returns>det(H)。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para><c>InitOCT(0)</c> 单输出经 <c>LoadD</c> 读回。行列式的符号与幅值含义：|det| 是面积缩放倍率，det&lt;0 表示含镜像（手性翻转），det≈0 表示退化为共线映射（不可逆）。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>对仿射矩阵，行列式等于左上 2×2 线性部分的行列式；对含透视项的完整 3×3 矩阵，返回完整齐次矩阵的行列式。行列式接近零表示矩阵不可逆。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dScale(2.0, -3.0, 0.0, 0.0);
	///   double det = m.HomMat2dDeterminant();   // 缩放因子为 2 和 -3 时为 -6
	///   </code>
	/// </remarks>
	public double HomMat2dDeterminant()
	{
		IntPtr proc = JlNativeApi.PreCall(273);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlNativeApi.LoadD(proc, 0, err, out var doubleValue);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return doubleValue;
	}

	/// <summary>返回矩阵的转置（原生 id 274）；新对象，本实例不变。</summary>
	/// <returns>Hᵀ。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>单输出经 <c>LoadNew</c> 生成新矩阵句柄。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>转置只是矩阵代数操作，不等于一般齐次变换的逆；需要几何逆变换时使用 <see cref="HomMat2dInvert"/>。如果用于协方差传播，应按具体模型选择相应的线性子矩阵。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dRotate(0.3, 0.0, 0.0);
	///   JlHomMat2D t = m.HomMat2dTranspose();
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dTranspose()
	{
		IntPtr proc = JlNativeApi.PreCall(274);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>返回逆矩阵（原生 id 275）；新对象，本实例不变。</summary>
	/// <returns>H⁻¹，满足 H⁻¹·H = I。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>单输出 <c>LoadNew</c> 新矩阵。典型用途：位姿矩阵把"模型坐标→图像坐标"，其逆把图像测量值搬回模型/机械手坐标。</para>
	///   <para><b>前提</b></para>
	///   <para>奇异（det≈0）矩阵不可逆，错误从原生层抛出；先用 <c>HomMat2dDeterminant</c> 判阈值可避免异常控制流。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只需撤销最后一步变换（如撤销一次旋转）时，复合一个显式反变换（<c>HomMat2dRotate(-phi,...)</c>）比整矩阵求逆数值更稳；一般情形直接用本算子，不要靠 <c>HomMat2dTranspose</c> 凑逆。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D g = new JlHomMat2D().HomMat2dTranslate(3.0, 4.0);
	///   JlHomMat2D back = g.HomMat2dInvert();
	///   double qx = back.AffineTransPoint2d(13.0, 14.0, out double qy);   // 映回原坐标 (10,10)
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dInvert()
	{
		IntPtr proc = JlNativeApi.PreCall(275);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>矩阵乘法复合两个变换（原生 id 276）：结果 = this · 参数，返回新矩阵。</summary>
	/// <param name="homMat2DRight">右乘矩阵（先作用的变换）。</param>
	/// <returns>复合结果 H = this × homMat2DRight。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para><c>this</c> 钉在索引 0、右矩阵钉在索引 1（其 tuple 在调用后解钉），输出 <c>LoadNew</c> 新句柄。对点 p 有 (this·right)·p = this·(right·p)：右矩阵先作用、左矩阵后作用。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只想在现有矩阵上叠加单个基本变换（平移/旋转/缩放/斜切/反射）时，用对应的 <c>HomMat2dXxx</c>/<c>HomMat2dXxxLocal</c> 一步完成，它们内置了复合侧与固定点处理；需要任意顺序合并外部矩阵（如 世界→相机、相机→图像 两段拼接）才用它。</para>
	///   <para><b>坑</b></para>
	///   <para>矩阵乘法不可交换：先转后移与先移后转结果不同，写反的常见症状是物体绕错误中心画弧。两个操作数都不被修改；链式复合每步都产生新对象，热循环里注意分配。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D rot = new JlHomMat2D().HomMat2dRotate(0.5236, 100.0, 100.0);
	///   JlHomMat2D scl = rot.HomMat2dScale(2.0, 2.0, 100.0, 100.0);   // 等价 rot × scale 矩阵
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dCompose(JlHomMat2D homMat2DRight)
	{
		IntPtr proc = JlNativeApi.PreCall(276);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, homMat2DRight);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(homMat2DRight);
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>在本矩阵上局部叠加一个镜像反射（原生 id 277），返回新矩阵；主重载。</summary>
	/// <param name="px">轴上一点的 x 坐标。Default: 16</param>
	/// <param name="py">轴上一点的 y 坐标。Default: 32</param>
	/// <returns>叠加反射后的新矩阵，本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>反射会使行列式变号。Local 版本把反射轴解释在当前矩阵描述的局部坐标系中；需要在外部坐标系指定反射轴时使用 <see cref="HomMat2dReflect(JlTuple, JlTuple, JlTuple, JlTuple)"/>。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>反射轴由局部原点 <c>(0,0)</c> 和点 <c>(px,py)</c> 确定，因此 <c>(px,py)</c> 不能为原点；两点定义的外部坐标轴使用 <see cref="HomMat2dReflect(JlTuple, JlTuple, JlTuple, JlTuple)"/>。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();
	///   JlHomMat2D r = m.HomMat2dReflectLocal(10.0, 0.0);
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dReflectLocal(JlTuple px, JlTuple py)
	{
		IntPtr proc = JlNativeApi.PreCall(277);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, px);
		JlNativeApi.Store(proc, 2, py);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>反射叠加的标量重载（同一原生 id 277）：<c>StoreD</c> 写两点坐标，无钉桩/解钉；语义见主重载。</summary>
	/// <param name="px">轴上一点的 x 坐标。Default: 16</param>
	/// <param name="py">轴上一点的 y 坐标。Default: 32</param>
	/// <returns>叠加反射后的新矩阵。</returns>
	/// <remarks>
	///   <para>与 <c>JlTuple</c> 主重载同算子；单轴构造用本重载，批量不同轴心需各自调用。</para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dReflectLocal(0.0, 0.0);
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dReflectLocal(double px, double py)
	{
		IntPtr proc = JlNativeApi.PreCall(277);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, px);
		JlNativeApi.StoreD(proc, 2, py);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>叠加关于一条任意直线的镜像反射（原生 id 278），返回新矩阵；两点 (px,py)-(qx,qy) 定轴，主重载。</summary>
	/// <param name="px">轴第一点 x 坐标。Default: 0</param>
	/// <param name="py">轴第一点 y 坐标。Default: 0</param>
	/// <param name="qx">轴第二点 x 坐标。Default: 16</param>
	/// <param name="qy">轴第二点 y 坐标。Default: 32</param>
	/// <returns>叠加反射后的新矩阵，本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>四个点标按索引 1..4 钉桩，输出 <c>LoadNew</c> 新句柄。反射轴过两个给定点；与 <c>HomMat2dReflectLocal</c>（277，单点轴心）相比轴完全由两点显式给出。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>对 x 轴镜像（y 取反）不必用任意轴：直接 <c>HomMat2dScale(1, -1, ...)</c> 更直观且 det 同样变号；本算子用于斜置镜面/翻转会标图之类非坐标轴方向。</para>
	///   <para><b>坑</b></para>
	///   <para>两个轴点不能重合；否则反射轴无定义，原生算子会抛出 <see cref="JlOperatorException"/>。本版本在外部坐标系定义轴，Local 版本在当前局部坐标系定义轴。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D mirror = new JlHomMat2D().HomMat2dReflect(0.0, 0.0, 100.0, 0.0);   // 关于 y=0 直线
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dReflect(JlTuple px, JlTuple py, JlTuple qx, JlTuple qy)
	{
		IntPtr proc = JlNativeApi.PreCall(278);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, px);
		JlNativeApi.Store(proc, 2, py);
		JlNativeApi.Store(proc, 3, qx);
		JlNativeApi.Store(proc, 4, qy);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		JlNativeApi.UnpinTuple(qx);
		JlNativeApi.UnpinTuple(qy);
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>两点轴反射叠加的标量重载（同一原生 id 278）：<c>StoreD</c> 写四个 double、无钉桩；语义见主重载。</summary>
	/// <param name="px">轴第一点 x 坐标。Default: 0</param>
	/// <param name="py">轴第一点 y 坐标。Default: 0</param>
	/// <param name="qx">轴第二点 x 坐标。Default: 16</param>
	/// <param name="qy">轴第二点 y 坐标。Default: 32</param>
	/// <returns>叠加反射后的新矩阵。</returns>
	/// <remarks>
	///   <para>单轴一条时用本重载；主重载的 tuple 形态只是把四个量按数组钉桩传入。</para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dReflect(50.0, 0.0, 50.0, 100.0);   // 关于 x=50 直线
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dReflect(double px, double py, double qx, double qy)
	{
		IntPtr proc = JlNativeApi.PreCall(278);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, px);
		JlNativeApi.StoreD(proc, 2, py);
		JlNativeApi.StoreD(proc, 3, qx);
		JlNativeApi.StoreD(proc, 4, qy);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>局部叠加一个斜切（切变）变换（原生 id 279），返回新矩阵；主重载。</summary>
	/// <param name="theta">斜切角，弧度。Default: 0.78</param>
	/// <param name="axis">被斜切的坐标轴。Default: "x"</param>
	/// <returns>叠加斜切后的新矩阵，本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>斜切使指定坐标轴保持不变，并将另一坐标轴按角度 <paramref name="theta"/> 逆时针倾斜；<paramref name="axis"/> 为 <c>"x"</c> 时 x 轴倾斜、y 轴保持，为 <c>"y"</c> 时相反。角度单位为弧度。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>斜切矩阵自身 det=1，复合后面积倍率不变、只改形状；与 <c>HomMat2dRotate</c>/<c>HomMat2dScale</c> 复合可得一般仿射，<c>HomMat2dToAffinePar</c> 解出的 theta 正是该分量的逆用。要绕指定点斜切用 <c>HomMat2dSlant</c>（280）。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();
	///   JlHomMat2D sheared = m.HomMat2dSlantLocal(0.3, "x");
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dSlantLocal(JlTuple theta, string axis)
	{
		IntPtr proc = JlNativeApi.PreCall(279);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, theta);
		JlNativeApi.StoreS(proc, 2, axis);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(theta);
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>局部斜切叠加的标量重载（同一原生 id 279）：theta 经 <c>StoreD</c>、axis 经 <c>StoreS</c>，无钉桩/解钉；语义见主重载。</summary>
	/// <param name="theta">斜切角，弧度。Default: 0.78</param>
	/// <param name="axis">被斜切的坐标轴。Default: "x"</param>
	/// <returns>叠加斜切后的新矩阵。</returns>
	/// <remarks>
	///   <para>单个斜切一步用本重载。</para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dSlantLocal(0.3, "y");
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dSlantLocal(double theta, string axis)
	{
		IntPtr proc = JlNativeApi.PreCall(279);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, theta);
		JlNativeApi.StoreS(proc, 2, axis);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>叠加绕固定点的斜切（原生 id 280），返回新矩阵；主重载。</summary>
	/// <param name="theta">斜切角，弧度。Default: 0.78</param>
	/// <param name="axis">被斜切的坐标轴。Default: "x"</param>
	/// <param name="px">固定点 x 坐标。Default: 0</param>
	/// <param name="py">固定点 y 坐标。Default: 0</param>
	/// <returns>叠加斜切后的新矩阵，本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>在外部坐标系中围绕点 <c>(px,py)</c> 施加斜切；该点保持不变。斜切轴由 <paramref name="axis"/> 决定，角度单位为弧度。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>不需要固定点（绕原点斜切）用 <c>HomMat2dSlantLocal</c>（279）；theta/axis 的几何定义同 279。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();
	///   JlHomMat2D skewed = m.HomMat2dSlant(0.3, "x", 100.0, 100.0);
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dSlant(JlTuple theta, string axis, JlTuple px, JlTuple py)
	{
		IntPtr proc = JlNativeApi.PreCall(280);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, theta);
		JlNativeApi.StoreS(proc, 2, axis);
		JlNativeApi.Store(proc, 3, px);
		JlNativeApi.Store(proc, 4, py);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(theta);
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>固定点斜切的标量重载（同一原生 id 280）：theta/px/py 经 <c>StoreD</c>、axis 经 <c>StoreS</c>，无钉桩；语义见主重载。</summary>
	/// <param name="theta">斜切角，弧度。Default: 0.78</param>
	/// <param name="axis">被斜切的坐标轴。Default: "x"</param>
	/// <param name="px">固定点 x 坐标。Default: 0</param>
	/// <param name="py">固定点 y 坐标。Default: 0</param>
	/// <returns>叠加斜切后的新矩阵。</returns>
	/// <remarks>
	///   <para>一步构造一个绕定点的斜切用本重载；主重载的 tuple 形态把 theta 与固定点按数组钉桩送入。</para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dSlant(0.3, "y", 50.0, 50.0);
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dSlant(double theta, string axis, double px, double py)
	{
		IntPtr proc = JlNativeApi.PreCall(280);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, theta);
		JlNativeApi.StoreS(proc, 2, axis);
		JlNativeApi.StoreD(proc, 3, px);
		JlNativeApi.StoreD(proc, 4, py);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>局部叠加旋转（原生 id 281）：没有固定点参数，返回新矩阵；主重载。</summary>
	/// <param name="phi">旋转角，弧度。Default: 0.78</param>
	/// <returns>叠加旋转后的新矩阵，本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>Local 版本在当前矩阵描述的局部坐标系中绕其原点旋转；普通版本在外部坐标系中绕显式给出的中心旋转。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要让物体绕图像中的已知点旋转，使用 <see cref="HomMat2dRotate(JlTuple, JlTuple, JlTuple)"/>；本算子适合叠加物体自身的朝向变化。角度单位为弧度，并遵循 HALCON 的数学正方向约定。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();
	///   JlHomMat2D r = m.HomMat2dRotateLocal(0.7854);   // 局部叠加 45°
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dRotateLocal(JlTuple phi)
	{
		IntPtr proc = JlNativeApi.PreCall(281);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, phi);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(phi);
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>局部旋转叠加的标量重载（同一原生 id 281）：phi 经 <c>StoreD</c> 写入、无钉桩；语义见主重载。</summary>
	/// <param name="phi">旋转角，弧度。Default: 0.78</param>
	/// <returns>叠加旋转后的新矩阵。</returns>
	/// <remarks>
	///   <para>一次叠加一个角度用本重载。</para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dRotateLocal(3.1416);   // 朝向翻转 180°
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dRotateLocal(double phi)
	{
		IntPtr proc = JlNativeApi.PreCall(281);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, phi);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>叠加绕固定点的旋转（原生 id 282），返回新矩阵；主重载。</summary>
	/// <param name="phi">旋转角，弧度。Default: 0.78</param>
	/// <param name="px">旋转中心 x 坐标。Default: 0</param>
	/// <param name="py">旋转中心 y 坐标。Default: 0</param>
	/// <returns>叠加旋转后的新矩阵，本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>在外部坐标系中围绕点 <c>(px,py)</c> 旋转 <paramref name="phi"/> 弧度；指定中心保持不变，其他点按同一刚体旋转变换。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>旋转中心使用外部坐标系中的坐标值。要在物体自身坐标系中更新姿态，使用 Local 版本；两个版本的复合顺序不同。</para>
	///   <para><b>坑</b></para>
	///   <para>旋转不可交换：m.HomMat2dRotate(phi,px,py) 与先旋转后平移得到的矩阵不同，顺序写反的错误表现为物体画弧而非原地转。矩阵含缩放时本算子在该矩阵定义的坐标系内进行。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dRotate(0.2, 320.0, 240.0);   // 绕图像中心附近一点转
	///   double qx = m.AffineTransPoint2d(320.0, 240.0, out double qy);        // 中心点近似不动
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dRotate(JlTuple phi, JlTuple px, JlTuple py)
	{
		IntPtr proc = JlNativeApi.PreCall(282);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, phi);
		JlNativeApi.Store(proc, 2, px);
		JlNativeApi.Store(proc, 3, py);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(phi);
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>定点旋转叠加的标量重载（同一原生 id 282）：phi/px/py 经 <c>StoreD</c> 写入、无钉桩；语义见主重载。</summary>
	/// <param name="phi">旋转角，弧度。Default: 0.78</param>
	/// <param name="px">旋转中心 x 坐标。Default: 0</param>
	/// <param name="py">旋转中心 y 坐标。Default: 0</param>
	/// <returns>叠加旋转后的新矩阵。</returns>
	/// <remarks>
	///   <para>一次旋转一步用本重载；主重载的 tuple 形态可把 phi/中心作为数组钉桩传入。</para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dRotate(1.5708, 0.0, 0.0);   // 绕原点转 90°
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dRotate(double phi, double px, double py)
	{
		IntPtr proc = JlNativeApi.PreCall(282);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, phi);
		JlNativeApi.StoreD(proc, 2, px);
		JlNativeApi.StoreD(proc, 3, py);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>局部叠加缩放（原生 id 283），返回新矩阵；主重载。</summary>
	/// <param name="sx">x 轴缩放因子。Default: 2</param>
	/// <param name="sy">y 轴缩放因子。Default: 2</param>
	/// <returns>叠加缩放后的新矩阵，本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>面积倍率为 <c>sx·sy</c>；负缩放因子会产生相应轴的镜像。Local 版本沿当前矩阵的局部轴缩放，普通版本沿外部坐标轴缩放。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要"以某个图像点为中心放大"（像素级缩放预览之类）用带固定点的 <c>HomMat2dScale</c>（284）；本算子没有固定点，效果是物体离原点越远位移越大。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D();
	///   JlHomMat2D s = m.HomMat2dScaleLocal(2.0, 2.0);
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dScaleLocal(JlTuple sx, JlTuple sy)
	{
		IntPtr proc = JlNativeApi.PreCall(283);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, sx);
		JlNativeApi.Store(proc, 2, sy);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(sx);
		JlNativeApi.UnpinTuple(sy);
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>局部缩放叠加的标量重载（同一原生 id 283）：<c>StoreD</c> 写两个因子、无钉桩；语义见主重载。</summary>
	/// <param name="sx">x 轴缩放因子。Default: 2</param>
	/// <param name="sy">y 轴缩放因子。Default: 2</param>
	/// <returns>叠加缩放后的新矩阵。</returns>
	/// <remarks>
	///   <para>一次缩放一步用本重载。</para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dScaleLocal(0.5, 0.5);   // 局部缩小一半
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dScaleLocal(double sx, double sy)
	{
		IntPtr proc = JlNativeApi.PreCall(283);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, sx);
		JlNativeApi.StoreD(proc, 2, sy);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>叠加绕固定点的缩放（原生 id 284），返回新矩阵；主重载。</summary>
	/// <param name="sx">x 轴缩放因子。Default: 2</param>
	/// <param name="sy">y 轴缩放因子。Default: 2</param>
	/// <param name="px">缩放中心 x 坐标。Default: 0</param>
	/// <param name="py">缩放中心 y 坐标。Default: 0</param>
	/// <returns>叠加缩放后的新矩阵，本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>在外部坐标系中围绕点 <c>(px,py)</c> 缩放；该点保持不变，其他点按到该点的距离缩放。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>只想让物体整体变大变小（不要求某点不动）用 <c>HomMat2dScaleLocal</c>（283）；固定点传 (0,0) 时与不带平移补偿的裸缩放一致，但 px/py 为图像中心才是"围绕画面中心缩放"的正确写法。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dScale(2.0, 2.0, 320.0, 240.0);   // 围绕 (320,240) 放大一倍
	///   double qx = m.AffineTransPoint2d(320.0, 240.0, out double qy);            // 中心不动
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dScale(JlTuple sx, JlTuple sy, JlTuple px, JlTuple py)
	{
		IntPtr proc = JlNativeApi.PreCall(284);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, sx);
		JlNativeApi.Store(proc, 2, sy);
		JlNativeApi.Store(proc, 3, px);
		JlNativeApi.Store(proc, 4, py);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(sx);
		JlNativeApi.UnpinTuple(sy);
		JlNativeApi.UnpinTuple(px);
		JlNativeApi.UnpinTuple(py);
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>定点缩放叠加的标量重载（同一原生 id 284）：<c>StoreD</c> 写四个 double、无钉桩；语义见主重载。</summary>
	/// <param name="sx">x 轴缩放因子。Default: 2</param>
	/// <param name="sy">y 轴缩放因子。Default: 2</param>
	/// <param name="px">缩放中心 x 坐标。Default: 0</param>
	/// <param name="py">缩放中心 y 坐标。Default: 0</param>
	/// <returns>叠加缩放后的新矩阵。</returns>
	/// <remarks>
	///   <para>一次缩放一步用本重载。</para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dScale(1.5, 1.5, 100.0, 100.0);
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dScale(double sx, double sy, double px, double py)
	{
		IntPtr proc = JlNativeApi.PreCall(284);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, sx);
		JlNativeApi.StoreD(proc, 2, sy);
		JlNativeApi.StoreD(proc, 3, px);
		JlNativeApi.StoreD(proc, 4, py);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>沿当前（物体附着）坐标系局部叠加平移（原生 id 285），返回新矩阵；主重载。</summary>
	/// <param name="tx">x 轴平移量。Default: 64</param>
	/// <param name="ty">y 轴平移量。Default: 64</param>
	/// <returns>叠加平移后的新矩阵，本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>本算子沿当前矩阵描述的局部 x/y 轴平移；普通 <see cref="HomMat2dTranslate(JlTuple, JlTuple)"/> 沿外部坐标轴平移。两个版本在当前矩阵含旋转时会得到不同结果。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>机器人已转 90° 后沿夹爪轴向进给是本算子的场景；把物体在图像上往右下挪一段则用 286。单位与 286 同为矩阵坐标单位（通常像素）。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D pose = new JlHomMat2D().HomMat2dRotate(0.7854, 0.0, 0.0);
	///   JlHomMat2D next = pose.HomMat2dTranslateLocal(50.0, 0.0);   // 沿物体自身 x 轴走 50
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dTranslateLocal(JlTuple tx, JlTuple ty)
	{
		IntPtr proc = JlNativeApi.PreCall(285);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, tx);
		JlNativeApi.Store(proc, 2, ty);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(tx);
		JlNativeApi.UnpinTuple(ty);
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>局部平移叠加的标量重载（同一原生 id 285）：<c>StoreD</c> 写两个 double、无钉桩；语义见主重载。</summary>
	/// <param name="tx">x 轴平移量。Default: 64</param>
	/// <param name="ty">y 轴平移量。Default: 64</param>
	/// <returns>叠加平移后的新矩阵。</returns>
	/// <remarks>
	///   <para>一次局部平移一步用本重载。</para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslateLocal(20.0, 0.0);
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dTranslateLocal(double tx, double ty)
	{
		IntPtr proc = JlNativeApi.PreCall(285);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, tx);
		JlNativeApi.StoreD(proc, 2, ty);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>沿全局坐标轴叠加平移（原生 id 286），返回新矩阵；主重载。</summary>
	/// <param name="tx">x（列）方向平移量。Default: 64</param>
	/// <param name="ty">y（行）方向平移量。Default: 64</param>
	/// <returns>叠加平移后的新矩阵，本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>平移量使用矩阵作用坐标系的单位。普通版本沿外部 x/y 轴平移；Local 版本沿当前矩阵局部轴平移，因此含旋转的矩阵对同一组数值会产生不同落点。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>"把已求出的位姿整体在图像上挪 (dx,dy)"用它；沿物体自身轴向的增量用 <c>HomMat2dTranslateLocal</c>；把点集挪完再复合进大矩阵的场景，可直接对点调 <c>AffineTransPoint2d</c>，不一定要建中间矩阵。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(120.0, 40.0);
	///   double qx = m.AffineTransPoint2d(0.0, 0.0, out double qy);   // 原点映到 (120,40)
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dTranslate(JlTuple tx, JlTuple ty)
	{
		IntPtr proc = JlNativeApi.PreCall(286);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, tx);
		JlNativeApi.Store(proc, 2, ty);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(tx);
		JlNativeApi.UnpinTuple(ty);
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>全局平移叠加的标量重载（同一原生 id 286）：<c>StoreD</c> 写两个 double、无钉桩；语义见主重载。</summary>
	/// <param name="tx">x（列）方向平移量。Default: 64</param>
	/// <param name="ty">y（行）方向平移量。Default: 64</param>
	/// <returns>叠加平移后的新矩阵。</returns>
	/// <remarks>
	///   <para>一次全局平移一步用本重载。</para>
	///   <code>
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dTranslate(10.0, -5.0);
	///   </code>
	/// </remarks>
	public JlHomMat2D HomMat2dTranslate(double tx, double ty)
	{
		IntPtr proc = JlNativeApi.PreCall(286);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, tx);
		JlNativeApi.StoreD(proc, 2, ty);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = LoadNew(proc, 0, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>把本实例就地重置为单位矩阵（原生 id 287，与无参构造同一算子），不返回新对象。</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para><c>InitOCT(0)</c>+<c>Load(proc,0)</c> 覆写自身 tuple。用途：热路径循环里复用同一个实例做复合链起点，省掉一次分配。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>一次性使用直接 <c>new JlHomMat2D()</c>；本方法适合对象已作为字段/数组元素存在、不便重建引用的场合。它与无参构造共享同一路径（287），差别只在覆写谁。</para>
	///   <para><b>坑</b></para>
	///   <para>调用成功后本实例变为单位矩阵；发生异常时不应依赖目标对象的内容。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D work = new JlHomMat2D();
	///   work.HomMat2dIdentity();                            // 就地复位为恒等，作为下一条复合链起点
	///   JlHomMat2D step = work.HomMat2dTranslate(1.5, 0.0);
	///   </code>
	/// </remarks>
	public void HomMat2dIdentity()
	{
		IntPtr proc = JlNativeApi.PreCall(287);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}








	/// <summary>由两视图点对与两台相机矩阵计算基础矩阵（essential matrix）并三角化 3D 点（原生 id 352）；主重载返回全部数组型输出。</summary>
	/// <param name="rows1">图 1 点行坐标。</param>
	/// <param name="cols1">图 1 点列坐标。</param>
	/// <param name="rows2">图 2 点行坐标。</param>
	/// <param name="cols2">图 2 点列坐标。</param>
	/// <param name="covRR1">图 1 行方差。Default: []</param>
	/// <param name="covRC1">图 1 行/列协方差。Default: []</param>
	/// <param name="covCC1">图 1 列方差。Default: []</param>
	/// <param name="covRR2">图 2 行方差。Default: []</param>
	/// <param name="covRC2">图 2 行/列协方差。Default: []</param>
	/// <param name="covCC2">图 2 列方差。Default: []</param>
	/// <param name="camMat2">第二台相机的投影矩阵。</param>
	/// <param name="method">基础矩阵算法与特殊朝向处理。Default: "normalized_dlt"</param>
	/// <param name="covEMat">基础矩阵的 9×9 协方差。</param>
	/// <param name="error">极线距离 RMS（数组形式）。</param>
	/// <param name="x">重建 3D 点 X。</param>
	/// <param name="y">重建 3D 点 Y。</param>
	/// <param name="z">重建 3D 点 Z。</param>
	/// <param name="covXYZ">重建点的协方差。</param>
	/// <returns>计算得到的 3×3 基础矩阵（新对象）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>关键取向：本实例经 <c>Store(proc,10)</c> 作为第一台相机的投影矩阵（cam_mat1）被消费，不是被变换/被覆写的对象；<c>camMat2</c> 写在 11。<c>InitOCT(0..6)</c>：输出 0=<c>LoadNew</c> 的新基础矩阵，其余为 covEMat/error/x/y/z/covXYZ。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>输出刻画两视图的对极几何约束，不是把点从图 1 映到图 2 的 2D 变换；后者使用 <c>VectorToHomMat2d</c> 或 <c>VectorToProjHomMat2d</c>。三角化点的绝对尺度由相机之间的基线和标定单位决定。</para>
	///   <para><b>坑</b></para>
	///   <para>纯平移或退化朝向需要 method 里的特殊处理；<c>this</c> 内容只读，但传错相机矩阵不会报错、只会得到错误 E。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlHomMat2D cam1 = new JlHomMat2D();
	///   JlHomMat2D cam2 = new JlHomMat2D();
	///   JlTuple empty = new JlTuple();
	///   JlHomMat2D e = cam1.VectorToEssentialMatrix(
	///       new JlTuple(new double[] { 10, 20 }), new JlTuple(new double[] { 10, 20 }),
	///       new JlTuple(new double[] { 12, 21 }), new JlTuple(new double[] { 9, 19 }),
	///       empty, empty, empty, empty, empty, empty, cam2, "normalized_dlt",
	///       out JlTuple covE, out JlTuple err, out JlTuple x, out JlTuple y, out JlTuple z, out JlTuple covXYZ);
	///   </code>
	/// </remarks>
	public JlHomMat2D VectorToEssentialMatrix(JlTuple rows1, JlTuple cols1, JlTuple rows2, JlTuple cols2, JlTuple covRR1, JlTuple covRC1, JlTuple covCC1, JlTuple covRR2, JlTuple covRC2, JlTuple covCC2, JlHomMat2D camMat2, string method, out JlTuple covEMat, out JlTuple error, out JlTuple x, out JlTuple y, out JlTuple z, out JlTuple covXYZ)
	{
		IntPtr proc = JlNativeApi.PreCall(352);
		Store(proc, 10);
		JlNativeApi.Store(proc, 0, rows1);
		JlNativeApi.Store(proc, 1, cols1);
		JlNativeApi.Store(proc, 2, rows2);
		JlNativeApi.Store(proc, 3, cols2);
		JlNativeApi.Store(proc, 4, covRR1);
		JlNativeApi.Store(proc, 5, covRC1);
		JlNativeApi.Store(proc, 6, covCC1);
		JlNativeApi.Store(proc, 7, covRR2);
		JlNativeApi.Store(proc, 8, covRC2);
		JlNativeApi.Store(proc, 9, covCC2);
		JlNativeApi.Store(proc, 11, camMat2);
		JlNativeApi.StoreS(proc, 12, method);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(rows1);
		JlNativeApi.UnpinTuple(cols1);
		JlNativeApi.UnpinTuple(rows2);
		JlNativeApi.UnpinTuple(cols2);
		JlNativeApi.UnpinTuple(covRR1);
		JlNativeApi.UnpinTuple(covRC1);
		JlNativeApi.UnpinTuple(covCC1);
		JlNativeApi.UnpinTuple(covRR2);
		JlNativeApi.UnpinTuple(covRC2);
		JlNativeApi.UnpinTuple(covCC2);
		JlNativeApi.UnpinTuple(camMat2);
		err = LoadNew(proc, 0, err, out var obj);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out covEMat);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out error);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out x);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out y);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out z);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out covXYZ);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>与 <c>JlTuple</c> error 主重载同一原生算子（id 352）：本实例仍充当第一台相机矩阵，唯一差异是极线距离 RMS 经 <c>LoadD</c> 按单个 double 读回。</summary>
	/// <param name="rows1">图 1 点行坐标。</param>
	/// <param name="cols1">图 1 点列坐标。</param>
	/// <param name="rows2">图 2 点行坐标。</param>
	/// <param name="cols2">图 2 点列坐标。</param>
	/// <param name="covRR1">图 1 行方差。Default: []</param>
	/// <param name="covRC1">图 1 行/列协方差。Default: []</param>
	/// <param name="covCC1">图 1 列方差。Default: []</param>
	/// <param name="covRR2">图 2 行方差。Default: []</param>
	/// <param name="covRC2">图 2 行/列协方差。Default: []</param>
	/// <param name="covCC2">图 2 列方差。Default: []</param>
	/// <param name="camMat2">第二台相机的投影矩阵。</param>
	/// <param name="method">基础矩阵算法。Default: "normalized_dlt"</param>
	/// <param name="covEMat">基础矩阵的 9×9 协方差。</param>
	/// <param name="error">极线距离 RMS（单个 double）。</param>
	/// <param name="x">重建 3D 点 X。</param>
	/// <param name="y">重建 3D 点 Y。</param>
	/// <param name="z">重建 3D 点 Z。</param>
	/// <param name="covXYZ">重建点的协方差。</param>
	/// <returns>计算得到的 3×3 基础矩阵。</returns>
	/// <remarks>
	///   <para>语义与选型见主重载；只要一个总体质量数字（例如做自动重试的判据）时用本重载。</para>
	/// </remarks>
	public JlHomMat2D VectorToEssentialMatrix(JlTuple rows1, JlTuple cols1, JlTuple rows2, JlTuple cols2, JlTuple covRR1, JlTuple covRC1, JlTuple covCC1, JlTuple covRR2, JlTuple covRC2, JlTuple covCC2, JlHomMat2D camMat2, string method, out JlTuple covEMat, out double error, out JlTuple x, out JlTuple y, out JlTuple z, out JlTuple covXYZ)
	{
		IntPtr proc = JlNativeApi.PreCall(352);
		Store(proc, 10);
		JlNativeApi.Store(proc, 0, rows1);
		JlNativeApi.Store(proc, 1, cols1);
		JlNativeApi.Store(proc, 2, rows2);
		JlNativeApi.Store(proc, 3, cols2);
		JlNativeApi.Store(proc, 4, covRR1);
		JlNativeApi.Store(proc, 5, covRC1);
		JlNativeApi.Store(proc, 6, covCC1);
		JlNativeApi.Store(proc, 7, covRR2);
		JlNativeApi.Store(proc, 8, covRC2);
		JlNativeApi.Store(proc, 9, covCC2);
		JlNativeApi.Store(proc, 11, camMat2);
		JlNativeApi.StoreS(proc, 12, method);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		JlNativeApi.UnpinTuple(rows1);
		JlNativeApi.UnpinTuple(cols1);
		JlNativeApi.UnpinTuple(rows2);
		JlNativeApi.UnpinTuple(cols2);
		JlNativeApi.UnpinTuple(covRR1);
		JlNativeApi.UnpinTuple(covRC1);
		JlNativeApi.UnpinTuple(covCC1);
		JlNativeApi.UnpinTuple(covRR2);
		JlNativeApi.UnpinTuple(covRC2);
		JlNativeApi.UnpinTuple(covCC2);
		JlNativeApi.UnpinTuple(camMat2);
		err = LoadNew(proc, 0, err, out var obj);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out covEMat);
		err = JlNativeApi.LoadD(proc, 2, err, out error);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.DOUBLE, err, out x);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out y);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out z);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out covXYZ);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}




	/// <summary>自动灰度匹配两图特征点后 RANSAC 估计基础矩阵（原生 id 356）；主重载，rotation/matchThreshold/distanceThreshold/error 均为数组形态。</summary>
	/// <param name="image1">左图。</param>
	/// <param name="image2">右图。</param>
	/// <param name="rows1">图 1 特征点行坐标。</param>
	/// <param name="cols1">图 1 特征点列坐标。</param>
	/// <param name="rows2">图 2 特征点行坐标。</param>
	/// <param name="cols2">图 2 特征点列坐标。</param>
	/// <param name="camMat2">第二台相机矩阵。</param>
	/// <param name="grayMatchMethod">灰度比较度量。Default: "ssd"</param>
	/// <param name="maskSize">灰度模板尺寸。Default: 10</param>
	/// <param name="rowMove">对应点的平均行偏移先验。Default: 0</param>
	/// <param name="colMove">对应点的平均列偏移先验。Default: 0</param>
	/// <param name="rowTolerance">搜索窗半高。Default: 200</param>
	/// <param name="colTolerance">搜索窗半宽。Default: 200</param>
	/// <param name="rotation">右图相对左图的朝向先验。Default: 0.0</param>
	/// <param name="matchThreshold">灰度匹配阈值。Default: 10</param>
	/// <param name="estimationMethod">基础矩阵估计算法。Default: "normalized_dlt"</param>
	/// <param name="distanceThreshold">点到极线的最大偏差（RANSAC 内点阈值）。Default: 1</param>
	/// <param name="randSeed">随机种子。Default: 0</param>
	/// <param name="covEMat">基础矩阵 9×9 协方差。</param>
	/// <param name="error">极线距离 RMS（数组形态）。</param>
	/// <param name="points1">入选内点的图 1 点索引（INTEGER 型 tuple）。</param>
	/// <param name="points2">入选内点的图 2 点索引（INTEGER 型 tuple）。</param>
	/// <returns>鲁棒估计出的基础矩阵（新对象）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>本实例作为第一台相机矩阵只读参与计算，<paramref name="camMat2"/> 是第二台相机矩阵；输出依次为基础矩阵、协方差、误差和两组内点索引。两幅图像必须使用与相应相机矩阵匹配的标定和坐标约定。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>与 <c>VectorToEssentialMatrix</c>（352）的差别：它要求点对已配好；本算子自己按 <c>grayMatchMethod</c>/<c>maskSize</c> 做匹配并用 RANSAC 剔除外点，适合有遮挡/纹理重复的立体对。匹配结果可用 <c>points1</c>/<c>points2</c> 反查后再走 352 精算。</para>
	///   <para><b>坑</b></para>
	///   <para><c>randSeed</c> 固定则结果可复现；搜索窗与阈值决定内点规模，乱调会得到"稳定但错误"的 E。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage im1 = new JlImage("byte", 64, 64);
	///   JlImage im2 = new JlImage("byte", 64, 64);
	///   JlHomMat2D cam1 = new JlHomMat2D();
	///   JlHomMat2D cam2 = new JlHomMat2D();
	///   JlHomMat2D e = cam1.MatchEssentialMatrixRansac(im1, im2,
	///       new JlTuple(new double[] { 10 }), new JlTuple(new double[] { 10 }),
	///       new JlTuple(new double[] { 12 }), new JlTuple(new double[] { 9 }),
	///       cam2, "ssd", 10, 0, 0, 200, 200, new JlTuple(0.0), new JlTuple(10.0),
	///       "normalized_dlt", new JlTuple(1.0), 0,
	///       out JlTuple covE, out JlTuple err, out JlTuple p1, out JlTuple p2);
	///   </code>
	/// </remarks>
	public JlHomMat2D MatchEssentialMatrixRansac(JlImage image1, JlImage image2, JlTuple rows1, JlTuple cols1, JlTuple rows2, JlTuple cols2, JlHomMat2D camMat2, string grayMatchMethod, int maskSize, int rowMove, int colMove, int rowTolerance, int colTolerance, JlTuple rotation, JlTuple matchThreshold, string estimationMethod, JlTuple distanceThreshold, int randSeed, out JlTuple covEMat, out JlTuple error, out JlTuple points1, out JlTuple points2)
	{
		IntPtr proc = JlNativeApi.PreCall(356);
		Store(proc, 4);
		JlNativeApi.Store(proc, 1, image1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, rows1);
		JlNativeApi.Store(proc, 1, cols1);
		JlNativeApi.Store(proc, 2, rows2);
		JlNativeApi.Store(proc, 3, cols2);
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
		UnpinTuple();
		JlNativeApi.UnpinTuple(rows1);
		JlNativeApi.UnpinTuple(cols1);
		JlNativeApi.UnpinTuple(rows2);
		JlNativeApi.UnpinTuple(cols2);
		JlNativeApi.UnpinTuple(camMat2);
		JlNativeApi.UnpinTuple(rotation);
		JlNativeApi.UnpinTuple(matchThreshold);
		JlNativeApi.UnpinTuple(distanceThreshold);
		err = LoadNew(proc, 0, err, out var obj);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out covEMat);
		err = JlTuple.LoadNew(proc, 2, JlTupleType.DOUBLE, err, out error);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out points1);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out points2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image1);
		GC.KeepAlive(image2);
		return obj;
	}

	/// <summary>与 <c>JlTuple</c> 主重载同一原生算子（id 356）。实现差异：rotation/distanceThreshold 经 <c>StoreD</c>、matchThreshold 经 <c>StoreI</c> 写入标量，error 经 <c>LoadD</c> 按单个 double 读回，故这三个输入与一个输出无需钉桩。</summary>
	/// <param name="image1">左图。</param>
	/// <param name="image2">右图。</param>
	/// <param name="rows1">图 1 特征点行坐标。</param>
	/// <param name="cols1">图 1 特征点列坐标。</param>
	/// <param name="rows2">图 2 特征点行坐标。</param>
	/// <param name="cols2">图 2 特征点列坐标。</param>
	/// <param name="camMat2">第二台相机矩阵。</param>
	/// <param name="grayMatchMethod">灰度比较度量。Default: "ssd"</param>
	/// <param name="maskSize">灰度模板尺寸。Default: 10</param>
	/// <param name="rowMove">平均行偏移先验。Default: 0</param>
	/// <param name="colMove">平均列偏移先验。Default: 0</param>
	/// <param name="rowTolerance">搜索窗半高。Default: 200</param>
	/// <param name="colTolerance">搜索窗半宽。Default: 200</param>
	/// <param name="rotation">相对朝向先验（弧度标量）。Default: 0.0</param>
	/// <param name="matchThreshold">灰度匹配阈值（整数）。Default: 10</param>
	/// <param name="estimationMethod">基础矩阵估计算法。Default: "normalized_dlt"</param>
	/// <param name="distanceThreshold">RANSAC 内点阈值（double 标量）。Default: 1</param>
	/// <param name="randSeed">随机种子。Default: 0</param>
	/// <param name="covEMat">基础矩阵 9×9 协方差。</param>
	/// <param name="error">极线距离 RMS（单个 double）。</param>
	/// <param name="points1">入选内点的图 1 点索引。</param>
	/// <param name="points2">入选内点的图 2 点索引。</param>
	/// <returns>鲁棒估计出的基础矩阵。</returns>
	/// <remarks>
	///   <para>语义、先验调法与坑见主重载；本重载的 matchThreshold 是 <c>int</c>（主重载为 <c>JlTuple</c>），单组先验时用本重载。</para>
	///   <code>
	///   JlImage im1 = new JlImage("byte", 64, 64);
	///   JlImage im2 = new JlImage("byte", 64, 64);
	///   JlHomMat2D cam1 = new JlHomMat2D();
	///   JlHomMat2D cam2 = new JlHomMat2D();
	///   JlHomMat2D e = cam1.MatchEssentialMatrixRansac(im1, im2,
	///       new JlTuple(new double[] { 10 }), new JlTuple(new double[] { 10 }),
	///       new JlTuple(new double[] { 12 }), new JlTuple(new double[] { 9 }),
	///       cam2, "ssd", 10, 0, 0, 200, 200, 0.0, 10,
	///       "normalized_dlt", 1.0, 0,
	///       out JlTuple covE, out double err, out JlTuple p1, out JlTuple p2);
	///   </code>
	/// </remarks>
	public JlHomMat2D MatchEssentialMatrixRansac(JlImage image1, JlImage image2, JlTuple rows1, JlTuple cols1, JlTuple rows2, JlTuple cols2, JlHomMat2D camMat2, string grayMatchMethod, int maskSize, int rowMove, int colMove, int rowTolerance, int colTolerance, double rotation, int matchThreshold, string estimationMethod, double distanceThreshold, int randSeed, out JlTuple covEMat, out double error, out JlTuple points1, out JlTuple points2)
	{
		IntPtr proc = JlNativeApi.PreCall(356);
		Store(proc, 4);
		JlNativeApi.Store(proc, 1, image1);
		JlNativeApi.Store(proc, 2, image2);
		JlNativeApi.Store(proc, 0, rows1);
		JlNativeApi.Store(proc, 1, cols1);
		JlNativeApi.Store(proc, 2, rows2);
		JlNativeApi.Store(proc, 3, cols2);
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
		UnpinTuple();
		JlNativeApi.UnpinTuple(rows1);
		JlNativeApi.UnpinTuple(cols1);
		JlNativeApi.UnpinTuple(rows2);
		JlNativeApi.UnpinTuple(cols2);
		JlNativeApi.UnpinTuple(camMat2);
		err = LoadNew(proc, 0, err, out var obj);
		err = JlTuple.LoadNew(proc, 1, JlTupleType.DOUBLE, err, out covEMat);
		err = JlNativeApi.LoadD(proc, 2, err, out error);
		err = JlTuple.LoadNew(proc, 3, JlTupleType.INTEGER, err, out points1);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.INTEGER, err, out points2);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image1);
		GC.KeepAlive(image2);
		return obj;
	}



	/// <summary>对本矩阵的投影（含透视）变换重采样区域，返回新区域（原生 id 477）。</summary>
	/// <param name="regions">输入区域。</param>
	/// <param name="interpolation">插值方式。Default: "bilinear"</param>
	/// <returns>变换后的新 <see cref="JlRegion"/>，本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>按投影矩阵对区域进行栅格重采样并返回新区域。<paramref name="interpolation"/> 使用 HALCON 支持的区域插值模式，例如 <c>"nearest_neighbor"</c> 或 <c>"bilinear"</c>；输入区域和当前矩阵不变。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>仿射矩阵下与 <c>AffineTransRegion</c>（478）结果等价且后者语义更明确；本算子用于确含透视的矩阵（拼接/单应校正后的区域搬运）。只要点/轮廓坐标不要栅格区域时用 <c>ProjectiveTransContourXld</c>，无重采样损失。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlRegion reg = new JlRegion(10.0, 20.0, 100.0, 200.0);
	///   JlHomMat2D h = new JlHomMat2D();
	///   JlRegion outReg = h.ProjectiveTransRegion(reg, "nearest_neighbor");
	///   </code>
	/// </remarks>
	public JlRegion ProjectiveTransRegion(JlRegion regions, string interpolation)
	{
		IntPtr proc = JlNativeApi.PreCall(477);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, regions);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(regions);
		return obj;
	}

	/// <summary>用仿射 2D 变换重采样区域，返回新区域（原生 id 478）。</summary>
	/// <param name="region">待旋转/缩放的区域。</param>
	/// <param name="interpolate">插值方式。Default: "nearest_neighbor"</param>
	/// <returns>变换后的新 <see cref="JlRegion"/>，本实例不变。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>按仿射矩阵对区域进行栅格重采样并返回新区域；输入区域和当前矩阵不变。缩放会改变区域面积，旋转和非整数平移会引入由插值模式决定的栅格化误差。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>模型定位后把搜索区域搬到实测位姿的标准入口。含透视的矩阵要用 <c>ProjectiveTransRegion</c>（477）；只需搬坐标（如 ROI 角点）用 <c>AffineTransPixel</c>，别为几个点付栅格化的代价；对图像本身重采样用 <c>AffineTransImage</c>。</para>
	///   <para><b>坑</b></para>
	///   <para>对细结构区域，<c>nearest_neighbor</c> 通常更能保持离散像素占用，<c>bilinear</c> 会产生平滑的边界重采样；需要保持拓扑时优先选择最近邻并验证输出。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlRegion reg = new JlRegion(50.0, 50.0, 150.0, 250.0);
	///   JlHomMat2D pose = new JlHomMat2D().HomMat2dRotate(0.2, 100.0, 100.0);
	///   JlRegion moved = pose.AffineTransRegion(reg, "nearest_neighbor");
	///   </code>
	/// </remarks>
	public JlRegion AffineTransRegion(JlRegion region, string interpolate)
	{
		IntPtr proc = JlNativeApi.PreCall(478);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, region);
		JlNativeApi.StoreS(proc, 1, interpolate);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlRegion.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(region);
		return obj;
	}

	/// <summary>投影变换图像到指定输出画幅（原生 id 1540），返回新图像。</summary>
	/// <param name="image">输入图像。</param>
	/// <param name="interpolation">插值方式。Default: "bilinear"</param>
	/// <param name="width">输出宽。</param>
	/// <param name="height">输出高。</param>
	/// <param name="transformDomain">是否同时变换输入域（"true"/"false" 串）。Default: "false"</param>
	/// <returns>指定尺寸的变换后图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>矩阵钉住索引 0、图像在索引 1、<c>interpolation</c> 以 <c>StoreS</c> 写索引 1，宽高 <c>StoreI</c> 2/3、域选项 4；输出新图像句柄。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>与 <c>ProjectiveTransImage</c>（1541）的差别是输出画幅：本算子固定为 width×height（适合校正到统一幅面/瓦片），1541 交给 adaptImageSize 自动决定。变换会把画面甩出画幅外时结果被裁切，先算好平移分量或用大画幅。</para>
	///   <para><b>坑</b></para>
	///   <para>投影重采样开销较大。<paramref name="transformDomain"/> 为 <c>"true"</c> 时同时变换输入域；为 <c>"false"</c> 时保留输出图像的默认域。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlHomMat2D h = new JlHomMat2D().HomMat2dTranslate(8.0, 8.0);
	///   JlImage outImg = h.ProjectiveTransImageSize(img, "bilinear", 80, 80, "false");
	///   </code>
	/// </remarks>
	public JlImage ProjectiveTransImageSize(JlImage image, string interpolation, int width, int height, string transformDomain)
	{
		IntPtr proc = JlNativeApi.PreCall(1540);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.StoreS(proc, 4, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>投影变换图像，可选自动扩幅（原生 id 1541），返回新图像。</summary>
	/// <param name="image">输入图像。</param>
	/// <param name="interpolation">插值方式。Default: "bilinear"</param>
	/// <param name="adaptImageSize">是否自动扩大输出尺寸以容纳变换后的画面（"true"/"false" 串）。Default: "false"</param>
	/// <param name="transformDomain">是否同时变换输入域。Default: "false"</param>
	/// <returns>变换后图像；尺寸由 adaptImageSize 决定。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>参数写入与 1540 同型（矩阵索引 0、图像 1、插值串 1、扩幅串 2、域选项 3）。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要固定输出幅面（拼接瓦片、统一校正尺寸）用 <c>ProjectiveTransImageSize</c>（1540）；不关心画面完整性只要快，可用 "false"（原尺寸裁切）。仿射矩阵请走 <c>AffineTransImage</c>（1543），少一层透视处理。</para>
	///   <para><b>坑</b></para>
	///   <para><paramref name="adaptImageSize"/> 为 <c>"true"</c> 时输出画幅扩展到覆盖变换后的输入范围；为 <c>"false"</c> 时维持原画幅并裁切越界部分。边界像素的生成方式由所选插值模式决定。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlHomMat2D h = new JlHomMat2D().HomMat2dRotate(0.2, 32.0, 32.0);
	///   JlImage outImg = h.ProjectiveTransImage(img, "bilinear", "true", "false");
	///   </code>
	/// </remarks>
	public JlImage ProjectiveTransImage(JlImage image, string interpolation, string adaptImageSize, string transformDomain)
	{
		IntPtr proc = JlNativeApi.PreCall(1541);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.StoreS(proc, 2, adaptImageSize);
		JlNativeApi.StoreS(proc, 3, transformDomain);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>仿射映射图像并重采样到固定 width×height（原生 id 1542），返回新图像。</summary>
	/// <param name="image">输入图像。</param>
	/// <param name="interpolation">插值/补边方式。Default: "constant"</param>
	/// <param name="width">输出宽。Default: 640</param>
	/// <param name="height">输出高。Default: 480</param>
	/// <returns>指定幅面的变换图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>矩阵钉住索引 0、图像索引 1、插值串写索引 1、宽高 <c>StoreI</c> 2/3；输出新图像句柄。落点是逆向映射采样，画面越出 width×height 即被裁掉。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>不想手填尺寸、让输出容纳全部画面用 <c>AffineTransImage</c>（1543，adaptImageSize）；只要坐标点别付重采样代价，用 <c>AffineTransPixel</c>；含透视矩阵用 1540。</para>
	///   <para><b>坑</b></para>
	///   <para>本重载固定输出画幅且不提供域选项；输入图像的域按 HALCON 图像变换规则处理。使用 <c>"constant"</c> 时，越界采样使用算子定义的常量边界值。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 640, 480);
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dScale(0.5, 0.5, 320.0, 240.0);
	///   JlImage half = m.AffineTransImageSize(img, "constant", 640, 480);
	///   </code>
	/// </remarks>
	public JlImage AffineTransImageSize(JlImage image, string interpolation, int width, int height)
	{
		IntPtr proc = JlNativeApi.PreCall(1542);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.StoreI(proc, 2, width);
		JlNativeApi.StoreI(proc, 3, height);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>仿射映射图像并重采样，输出尺寸可选自适应（原生 id 1543），返回新图像。</summary>
	/// <param name="image">输入图像。</param>
	/// <param name="interpolation">插值/补边方式。Default: "constant"</param>
	/// <param name="adaptImageSize">是否按变换后的范围自动调整输出尺寸。Default: "false"</param>
	/// <returns>变换后图像。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para><paramref name="adaptImageSize"/> 为 <c>"false"</c> 时输出保持输入画幅，变换后超出画面的像素被裁掉；为 <c>"true"</c> 时根据变换后的有效范围调整画幅。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>要精确控制输出幅面用 <c>AffineTransImageSize</c>（1542）；含透视的矩阵用 1540/1541；只搬区域/轮廓用 478/49，别动图像。</para>
	///   <para><b>坑</b></para>
	///   <para>图像级仿射重采样会改变像素网格；只需要变换坐标时使用 <c>AffineTransPixel</c>。缩小图像时应选择合适的插值模式并检查混叠。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage img = new JlImage("byte", 64, 64);
	///   JlHomMat2D m = new JlHomMat2D().HomMat2dRotate(0.2, 32.0, 32.0);
	///   JlImage rot = m.AffineTransImage(img, "constant", "true");
	///   </code>
	/// </remarks>
	public JlImage AffineTransImage(JlImage image, string interpolation, string adaptImageSize)
	{
		IntPtr proc = JlNativeApi.PreCall(1543);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.StoreS(proc, 1, interpolation);
		JlNativeApi.StoreS(proc, 2, adaptImageSize);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		UnpinTuple();
		err = JlImage.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
		return obj;
	}

	/// <summary>从位移向量场最小二乘拟合仿射矩阵，覆写本实例（原生 id 1551）。</summary>
	/// <param name="vectorField">包含两个分量的位移场图像，按 HALCON 约定分别表示 x（行）和 y（列）方向位移。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>实现里没有任何针对本实例的 <c>Store</c>：本对象纯作输出（<c>InitOCT(0)</c>+<c>Load(proc,0)</c>），与 <c>VectorToHomMat2d</c> 那族"就地覆写的估计算子"取向一致；向量场经 <c>Store(proc,1,...)</c> 钉住。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>手头是稠密位移场（光流/变形配准的输出）而只需要全局仿射时用本算子，比从场里抽点再喂 268 省事；场含明显非仿射局部形变时，仿射拟合只给主趋势，别指望它复原细节。</para>
	///   <para><b>坑</b></para>
	///   <para>输入必须包含两个位移分量；通道数不符或图像类型不支持时抛出 <see cref="JlOperatorException"/>。调用成功后本实例被覆写，失败时不要依赖目标对象的内容。</para>
	///   <para><b>用例</b></para>
	///   <code>
	///   JlImage field = new JlImage("real", 64, 64);   // 假设的位移场（实际需两通道）
	///   JlHomMat2D m = new JlHomMat2D();
	///   m.VectorFieldToHomMat2d(field);
	///   </code>
	/// </remarks>
	public void VectorFieldToHomMat2d(JlImage vectorField)
	{
		IntPtr proc = JlNativeApi.PreCall(1551);
		JlNativeApi.Store(proc, 1, vectorField);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(vectorField);
	}

	/// <summary>使用强类型算法选项估计带畸变的投影矩阵。</summary>
	public double VectorToProjHomMat2dDistortion(JlTuple points1Row, JlTuple points1Col, JlTuple points2Row, JlTuple points2Col, JlTuple covRR1, JlTuple covRC1, JlTuple covCC1, JlTuple covRR2, JlTuple covRC2, JlTuple covCC2, int imageWidth, int imageHeight, JlHomographyMethod method, out double error)
	{
		return VectorToProjHomMat2dDistortion(points1Row, points1Col, points2Row, points2Col, covRR1, covRC1, covCC1, covRR2, covRC2, covCC2, imageWidth, imageHeight, method.ToNative(), out error);
	}

	/// <summary>使用强类型算法选项把点向量转换为投影矩阵。</summary>
	public void HomVectorToProjHomMat2d(JlTuple px, JlTuple py, JlTuple pw, JlTuple qx, JlTuple qy, JlTuple qw, JlHomographyMethod method)
	{
		HomVectorToProjHomMat2d(px, py, pw, qx, qy, qw, method.ToNative());
	}

	/// <summary>使用强类型算法选项把对应点转换为投影矩阵。</summary>
	public JlTuple VectorToProjHomMat2d(JlTuple px, JlTuple py, JlTuple qx, JlTuple qy, JlHomographyMethod method, JlTuple covXX1, JlTuple covYY1, JlTuple covXY1, JlTuple covXX2, JlTuple covYY2, JlTuple covXY2)
	{
		return VectorToProjHomMat2d(px, py, qx, qy, method.ToNative(), covXX1, covYY1, covXY1, covXX2, covYY2, covXY2);
	}

	/// <summary>使用强类型坐标轴执行局部斜切。</summary>
	public JlHomMat2D HomMat2dSlantLocal(JlTuple theta, JlAxis axis)
	{
		return HomMat2dSlantLocal(theta, axis.ToNative());
	}

	/// <summary>使用强类型坐标轴执行局部斜切（标量重载）。</summary>
	public JlHomMat2D HomMat2dSlantLocal(double theta, JlAxis axis)
	{
		return HomMat2dSlantLocal(theta, axis.ToNative());
	}

	/// <summary>使用强类型坐标轴执行定点斜切。</summary>
	public JlHomMat2D HomMat2dSlant(JlTuple theta, JlAxis axis, JlTuple px, JlTuple py)
	{
		return HomMat2dSlant(theta, axis.ToNative(), px, py);
	}

	/// <summary>使用强类型坐标轴执行定点斜切（标量重载）。</summary>
	public JlHomMat2D HomMat2dSlant(double theta, JlAxis axis, double px, double py)
	{
		return HomMat2dSlant(theta, axis.ToNative(), px, py);
	}

	/// <summary>使用强类型算法选项估计本质矩阵。</summary>
	public JlHomMat2D VectorToEssentialMatrix(JlTuple rows1, JlTuple cols1, JlTuple rows2, JlTuple cols2, JlTuple covRR1, JlTuple covRC1, JlTuple covCC1, JlTuple covRR2, JlTuple covRC2, JlTuple covCC2, JlHomMat2D camMat2, JlHomographyMethod method, out JlTuple covEMat, out JlTuple error, out JlTuple x, out JlTuple y, out JlTuple z, out JlTuple covXYZ)
	{
		return VectorToEssentialMatrix(rows1, cols1, rows2, cols2, covRR1, covRC1, covCC1, covRR2, covRC2, covCC2, camMat2, method.ToNative(), out covEMat, out error, out x, out y, out z, out covXYZ);
	}

	/// <summary>使用强类型算法选项估计本质矩阵（标量误差重载）。</summary>
	public JlHomMat2D VectorToEssentialMatrix(JlTuple rows1, JlTuple cols1, JlTuple rows2, JlTuple cols2, JlTuple covRR1, JlTuple covRC1, JlTuple covCC1, JlTuple covRR2, JlTuple covRC2, JlTuple covCC2, JlHomMat2D camMat2, JlHomographyMethod method, out JlTuple covEMat, out double error, out JlTuple x, out JlTuple y, out JlTuple z, out JlTuple covXYZ)
	{
		return VectorToEssentialMatrix(rows1, cols1, rows2, cols2, covRR1, covRC1, covCC1, covRR2, covRC2, covCC2, camMat2, method.ToNative(), out covEMat, out error, out x, out y, out z, out covXYZ);
	}

	/// <summary>使用强类型插值选项变换区域。</summary>
	public JlRegion ProjectiveTransRegion(JlRegion regions, JlInterpolationMode interpolation)
	{
		return ProjectiveTransRegion(regions, interpolation.ToNative());
	}

	/// <summary>使用强类型插值选项变换区域（仿射版本）。</summary>
	public JlRegion AffineTransRegion(JlRegion region, JlInterpolationMode interpolation)
	{
		return AffineTransRegion(region, interpolation.ToNative());
	}

	/// <summary>使用强类型插值和域选项执行透视图像变换。</summary>
	public JlImage ProjectiveTransImageSize(JlImage image, JlInterpolationMode interpolation, int width, int height, JlBooleanOption transformDomain)
	{
		return ProjectiveTransImageSize(image, interpolation.ToNative(), width, height, transformDomain.ToNative());
	}

	/// <summary>使用强类型插值、尺寸和域选项执行透视图像变换。</summary>
	public JlImage ProjectiveTransImage(JlImage image, JlInterpolationMode interpolation, JlBooleanOption adaptImageSize, JlBooleanOption transformDomain)
	{
		return ProjectiveTransImage(image, interpolation.ToNative(), adaptImageSize.ToNative(), transformDomain.ToNative());
	}

	/// <summary>使用强类型插值选项执行仿射图像变换。</summary>
	public JlImage AffineTransImageSize(JlImage image, JlInterpolationMode interpolation, int width, int height)
	{
		return AffineTransImageSize(image, interpolation.ToNative(), width, height);
	}

	/// <summary>使用强类型插值和尺寸选项执行仿射图像变换。</summary>
	public JlImage AffineTransImage(JlImage image, JlInterpolationMode interpolation, JlBooleanOption adaptImageSize)
	{
		return AffineTransImage(image, interpolation.ToNative(), adaptImageSize.ToNative());
	}




}
