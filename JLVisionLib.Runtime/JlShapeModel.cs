using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>表示一个用于匹配的形状模型实例。</summary>
[Serializable]
public class JlShapeModel : JlHandle, ISerializable, ICloneable
{
	/// <summary>
	///   把已有原生 IntPtr 句柄包成强类型 JlShapeModel：先经 CopyHandle 引用计数拷贝、再校验语义类型 shape_model，本实例是独立容器、用毕 Dispose。
	/// </summary>
	/// <param name="handle">已有效的原生句柄（如 JlTuple 元素的 H）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转调 <c>base(handle)</c>：Handle 属性 setter 以 <c>copy: true</c> 走 <c>JlNativeApi.CopyHandle</c>，不吞占原句柄的引用计数；
	///   随后 <c>AssertSemType("shape_model")</c> 校验语义类型，塞进图像或区域句柄会当场抛异常。不发 <c>PreCall</c>、无算子 id，纯包装。</para>
	///   <para><b>约束或前提</b>入参句柄必须未释放（基类注释：Caller must ensure that input handle is kept alive）。标注 EditorBrowsable(Never)，
	///   正当用途是 <c>LoadNew</c> 数组版把 <c>tuple[i].H</c> 逐个包成模型数组的内部通道，业务代码极少直接调。</para>
	///   <para><b>与相邻构造器的取舍</b>手头是 JlHandle 对象用 <see cref="JlShapeModel(JlHandle)"/>；要内容互不相干的副本用 Clone()；
	///   从文件起用 <see cref="JlShapeModel(string)"/>；凭空新模型用无参构造再训练族。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlShapeModel wrapper = new JlShapeModel(src.Handle);
	///   </code>
	///   <para><b>资源与坑</b>两壳各自 Dispose 各自那份引用；对 src 换柄（ReadShapeModel 先 Dispose 再 Load）不影响 wrapper 继续持有旧柄；
	///   对句柄内容的原地改写（SetShapeModelParam）wrapper 是否同步可见 [待实测]。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlShapeModel(IntPtr handle)
		: base(handle)
	{
		AssertSemType();
	}

	/// <summary>
	///   从已有 JlHandle 拷贝构造强类型 JlShapeModel：CopyHandle 引用计数拷贝后校验语义类型 shape_model，两个壳各自独立、各须 Dispose。
	/// </summary>
	/// <param name="handle">待包装的强类型句柄实例。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转调 <c>base(handle)</c>（<c>JlHandleBase(JlHandleBase)</c>，内部 <c>SetHandleInternal(copy: true)</c> 调
	///   <c>JlNativeApi.CopyHandle</c>），再 <c>AssertSemType("shape_model")</c> 把关。不发 <c>PreCall</c>、无算子 id。</para>
	///   <para><b>约束或前提</b>入参句柄须未释放且语义类型为 shape_model，否则构造抛异常；标注 EditorBrowsable(Never)，
	///   定位是跨容器别名（拿到 JlHandle 后转强类型），不做内容深拷贝。</para>
	///   <para><b>与相邻构造器的取舍</b>与 <see cref="JlShapeModel(IntPtr)"/> 语义相同，只是入参已是 JlHandle 时免取 <c>.Handle</c>；
	///   要彻底独立的副本走 Clone()（序列化往返新建对象）；本构造几乎零开销。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlShapeModel alias = new JlShapeModel(src);
	///   </code>
	///   <para><b>资源与坑</b>这是"第二个名字指向同一原生资源"级别的浅别名：壳独立、各还各的引用，一侧 Dispose 不影响另一侧继续使用；
	///   底层共享程度对原地改写是否可见 [待实测]。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlShapeModel(JlHandle handle)
		: base(handle)
	{
		AssertSemType();
	}

	private void AssertSemType()
	{
		AssertSemType("shape_model");
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlShapeModel obj)
	{
		obj = new JlShapeModel(JlHandleBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlShapeModel[] obj)
	{
		err = JlTuple.LoadNew(proc, parIndex, err, out var tuple);
		obj = new JlShapeModel[tuple.Length];
		for (int i = 0; i < tuple.Length; i++)
		{
			obj[i] = new JlShapeModel(tuple[i].H);
		}
		tuple.Dispose();
		return err;
	}

	/// <summary>
	///   读取 WriteShapeModel 写出的形状模型文件，在新对象上装入模型句柄（原生 id 875）；文件不存在或格式错误时构造抛异常，不会留下半成品对象。
	/// </summary>
	/// <param name="fileName">文件名。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>构造时读取 WriteShapeModel 写出的形状模型文件。与 ReadShapeModel 共用原生算子 id 875：本重载在新对象上取得模型句柄，ReadShapeModel 则先 Dispose 旧句柄再原地换入。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>文件必须是合法的模型文件；文件不存在或格式错误时构造抛出异常，不会留下半成品对象。文件在不同版本间是否兼容 [待实测]。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>模型占用原生 shape_model 内存；终结器会兜底释放但时机不可控，常驻进程请尽早 Dispose 或用 using。</para>
	/// </remarks>
	public JlShapeModel(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(875);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   用 XLD 轮廓训练行、列尺度独立的各向异性形状模型，一步得到已完成训练的新对象（原生 id 892）；JlTuple 型参数在原生调用期间被 pin、调用后 UnpinTuple。
	/// </summary>
	/// <param name="contours">用于创建模型的输入轮廓。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleRMin">模板在行方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleRMax">模板在行方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleRStep">行方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleCMin">模板在列方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleCMax">模板在列方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleCStep">列方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>从 XLD 轮廓训练各向异性缩放的形状模型，得到已完成训练的新对象。与 CreateAnisoShapeModelXld(JlTuple 版) 共用原生 id 892，参数语义、训练开销与坑详见该方法；JlTuple 型参数在原生调用期间被 pin，调用后 UnpinTuple。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlXLDCont contours = src.GetShapeModelContours(1);
	///   using JlShapeModel model = new JlShapeModel(contours, "auto", -0.39, 0.79, "auto", 0.9, 1.1, "auto", 0.9, 1.1, "auto", "auto", "ignore_local_polarity", 5);
	///   </code>
	/// </remarks>
	public JlShapeModel(JlXLDCont contours, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleRMin, double scaleRMax, JlTuple scaleRStep, double scaleCMin, double scaleCMax, JlTuple scaleCStep, JlTuple optimization, string metric, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(892);
		JlNativeApi.Store(proc, 1, contours);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
	}

	/// <summary>
	///   CreateAnisoShapeModelXld 的标量入参构造版（原生 id 892），同样一步得到训练完成的新对象；numLevels、angleStep、scaleRStep、scaleCStep 必须给具体数值，参数经 StoreD/StoreI/StoreS 直写、无 pin/unpin。
	/// </summary>
	/// <param name="contours">用于创建模型的输入轮廓。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleRMin">模板在行方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleRMax">模板在行方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleRStep">行方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleCMin">模板在列方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleCMax">模板在列方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleCStep">列方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>CreateAnisoShapeModelXld 的标量入参构造版，原生 id 同为 892，行为一致；区别仅是各参数经 StoreD/StoreI/StoreS 直写，无 tuple pin/unpin，且 numLevels、angleStep、scaleRStep、scaleCStep 必须给具体数值（optimization、metric 仍为字符串）。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlXLDCont contours = src.GetShapeModelContours(1);
	///   using JlShapeModel model = new JlShapeModel(contours, 3, -0.39, 0.79, 0.05, 0.9, 1.1, 0.05, 0.9, 1.1, 0.05, "auto", "ignore_local_polarity", 5);
	///   </code>
	/// </remarks>
	public JlShapeModel(JlXLDCont contours, int numLevels, double angleStart, double angleExtent, double angleStep, double scaleRMin, double scaleRMax, double scaleRStep, double scaleCMin, double scaleCMax, double scaleCStep, string optimization, string metric, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(892);
		JlNativeApi.Store(proc, 1, contours);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
	}

	/// <summary>
	///   用 XLD 轮廓训练各向同性缩放的形状模型，得到已完成训练的新对象（原生 id 893）；带尺度训练必须配 FindScaledShapeModel(s) 才能取回尺度。
	/// </summary>
	/// <param name="contours">用于创建模型的输入轮廓。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleMin">模板的最小尺度。Default: 0.9</param>
	/// <param name="scaleMax">模板的最大尺度。Default: 1.1</param>
	/// <param name="scaleStep">尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>从 XLD 轮廓训练各向同性缩放的形状模型。与 CreateScaledShapeModelXld(JlTuple 版) 共用原生 id 893，详解见该方法；尺度系列训练必须配合 FindScaledShapeModel(s) 才能取回尺度。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlXLDCont contours = src.GetShapeModelContours(1);
	///   using JlShapeModel model = new JlShapeModel(contours, "auto", -0.39, 0.79, "auto", 0.9, 1.1, "auto", "auto", "ignore_local_polarity", 5);
	///   </code>
	/// </remarks>
	public JlShapeModel(JlXLDCont contours, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleMin, double scaleMax, JlTuple scaleStep, JlTuple optimization, string metric, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(893);
		JlNativeApi.Store(proc, 1, contours);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
	}

	/// <summary>
	///   CreateScaledShapeModelXld 的标量入参构造版（原生 id 893），得到训练完成的新对象；numLevels、angleStep、scaleStep 必须给具体数值，参数直写无 pin/unpin。
	/// </summary>
	/// <param name="contours">用于创建模型的输入轮廓。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleMin">模板的最小尺度。Default: 0.9</param>
	/// <param name="scaleMax">模板的最大尺度。Default: 1.1</param>
	/// <param name="scaleStep">尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>CreateScaledShapeModelXld 的标量入参构造版，原生 id 同为 893；参数经 StoreD/StoreI/StoreS 直写，无 pin/unpin，numLevels、angleStep、scaleStep 必须给具体数值。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlXLDCont contours = src.GetShapeModelContours(1);
	///   using JlShapeModel model = new JlShapeModel(contours, 3, -0.39, 0.79, 0.05, 0.9, 1.1, 0.05, "auto", "ignore_local_polarity", 5);
	///   </code>
	/// </remarks>
	public JlShapeModel(JlXLDCont contours, int numLevels, double angleStart, double angleExtent, double angleStep, double scaleMin, double scaleMax, double scaleStep, string optimization, string metric, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(893);
		JlNativeApi.Store(proc, 1, contours);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
	}

	/// <summary>
	///   用 XLD 轮廓训练不带尺度缩放的形状模型，得到已完成训练的新对象（原生 id 894，与 CreateShapeModelXld 的 JlTuple 版同 id）；轮廓来源与 metric、对比度取向详见该方法。
	/// </summary>
	/// <param name="contours">用于创建模型的输入轮廓。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>从 XLD 轮廓训练无尺度缩放的形状模型。与 CreateShapeModelXld(JlTuple 版) 共用原生 id 894，详解（轮廓来源、metric/对比度取向、匹配配合）见该方法。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlXLDCont contours = src.GetShapeModelContours(1);
	///   using JlShapeModel model = new JlShapeModel(contours, "auto", -0.39, 0.79, "auto", "auto", "ignore_local_polarity", 5);
	///   </code>
	/// </remarks>
	public JlShapeModel(JlXLDCont contours, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, JlTuple optimization, string metric, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(894);
		JlNativeApi.Store(proc, 1, contours);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
	}

	/// <summary>
	///   CreateShapeModelXld 的标量入参构造版（原生 id 894），得到训练完成的新对象；numLevels、angleStep 必须给具体数值，其余同 JlTuple 版。
	/// </summary>
	/// <param name="contours">用于创建模型的输入轮廓。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>CreateShapeModelXld 的标量入参构造版，原生 id 同为 894；numLevels、angleStep 必须给具体数值，其余同 JlTuple 版。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlXLDCont contours = src.GetShapeModelContours(1);
	///   using JlShapeModel model = new JlShapeModel(contours, 3, -0.39, 0.79, 0.05, "auto", "ignore_local_polarity", 5);
	///   </code>
	/// </remarks>
	public JlShapeModel(JlXLDCont contours, int numLevels, double angleStart, double angleExtent, double angleStep, string optimization, string metric, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(894);
		JlNativeApi.Store(proc, 1, contours);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, angleStep);
		JlNativeApi.StoreS(proc, 4, optimization);
		JlNativeApi.StoreS(proc, 5, metric);
		JlNativeApi.StoreI(proc, 6, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
	}

	/// <summary>
	///   用模板图像的 domain 区域训练行、列尺度独立的各向异性形状模型，得到已完成训练的新对象（原生 id 895）；行/列尺度与 contrast 迟滞取值的语义及训练耗时权衡详见 CreateAnisoShapeModel。
	/// </summary>
	/// <param name="template">用于创建模型的输入图像（取其 domain 区域）。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleRMin">模板在行方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleRMax">模板在行方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleRStep">行方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleCMin">模板在列方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleCMax">模板在列方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleCStep">列方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "use_polarity"</param>
	/// <param name="contrast">模板图像中目标对比度的阈值或迟滞阈值，以及可选的目标部件最小尺寸。Default: "auto"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: "auto"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>用模板图像训练各向异性缩放形状模型，得到已完成训练的新对象。与 CreateAnisoShapeModel(JlTuple 版) 共用原生 id 895；行/列独立尺度、contrast 迟滞取值的语义与训练耗时权衡详见该方法。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel(template, "auto", -0.39, 0.79, "auto", 0.9, 1.1, "auto", 0.9, 1.1, "auto", "auto", "use_polarity", 30, 15);
	///   </code>
	/// </remarks>
	public JlShapeModel(JlImage template, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleRMin, double scaleRMax, JlTuple scaleRStep, double scaleCMin, double scaleCMax, JlTuple scaleCStep, JlTuple optimization, string metric, JlTuple contrast, JlTuple minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(895);
		JlNativeApi.Store(proc, 1, template);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   CreateAnisoShapeModel 的标量入参构造版（原生 id 895），得到训练完成的新对象；numLevels、angleStep、scaleRStep、scaleCStep、contrast、minContrast 只能给数值，即无法用 "auto"、contrast 也无法给迟滞三元组。
	/// </summary>
	/// <param name="template">用于创建模型的输入图像（取其 domain 区域）。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleRMin">模板在行方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleRMax">模板在行方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleRStep">行方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleCMin">模板在列方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleCMax">模板在列方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleCStep">列方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "use_polarity"</param>
	/// <param name="contrast">模板图像中目标对比度的阈值或迟滞阈值，以及可选的目标部件最小尺寸。Default: "auto"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: "auto"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>CreateAnisoShapeModel 的标量入参构造版，原生 id 同为 895；numLevels、angleStep、scaleRStep、scaleCStep、contrast、minContrast 只能给数值（即无法用 "auto"，contrast 也无法给迟滞三元组），训练/匹配行为与 JlTuple 版一致。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel(template, 3, -0.39, 0.79, 0.05, 0.9, 1.1, 0.05, 0.9, 1.1, 0.05, "auto", "use_polarity", 30, 15);
	///   </code>
	/// </remarks>
	public JlShapeModel(JlImage template, int numLevels, double angleStart, double angleExtent, double angleStep, double scaleRMin, double scaleRMax, double scaleRStep, double scaleCMin, double scaleCMax, double scaleCStep, string optimization, string metric, int contrast, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(895);
		JlNativeApi.Store(proc, 1, template);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   用模板图像的 domain 区域训练各向同性缩放形状模型，得到已完成训练的新对象（原生 id 896）；尺度参数取向与训练代价详见 CreateScaledShapeModel。
	/// </summary>
	/// <param name="template">用于创建模型的输入图像（取其 domain 区域）。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleMin">模板的最小尺度。Default: 0.9</param>
	/// <param name="scaleMax">模板的最大尺度。Default: 1.1</param>
	/// <param name="scaleStep">尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "use_polarity"</param>
	/// <param name="contrast">模板图像中目标对比度的阈值或迟滞阈值，以及可选的目标部件最小尺寸。Default: "auto"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: "auto"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>用模板图像训练各向同性缩放形状模型。与 CreateScaledShapeModel(JlTuple 版) 共用原生 id 896；尺度参数取向与训练代价详见该方法。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel(template, "auto", -0.39, 0.79, "auto", 0.9, 1.1, "auto", "auto", "use_polarity", 30, 15);
	///   </code>
	/// </remarks>
	public JlShapeModel(JlImage template, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleMin, double scaleMax, JlTuple scaleStep, JlTuple optimization, string metric, JlTuple contrast, JlTuple minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(896);
		JlNativeApi.Store(proc, 1, template);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   CreateScaledShapeModel 的标量入参构造版（原生 id 896），得到训练完成的新对象；numLevels、angleStep、scaleStep、contrast、minContrast 只能给具体数值。
	/// </summary>
	/// <param name="template">用于创建模型的输入图像（取其 domain 区域）。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleMin">模板的最小尺度。Default: 0.9</param>
	/// <param name="scaleMax">模板的最大尺度。Default: 1.1</param>
	/// <param name="scaleStep">尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "use_polarity"</param>
	/// <param name="contrast">模板图像中目标对比度的阈值或迟滞阈值，以及可选的目标部件最小尺寸。Default: "auto"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: "auto"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>CreateScaledShapeModel 的标量入参构造版，原生 id 同为 896；numLevels、angleStep、scaleStep、contrast、minContrast 只能给具体数值。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel(template, 3, -0.39, 0.79, 0.05, 0.9, 1.1, 0.05, "auto", "use_polarity", 30, 15);
	///   </code>
	/// </remarks>
	public JlShapeModel(JlImage template, int numLevels, double angleStart, double angleExtent, double angleStep, double scaleMin, double scaleMax, double scaleStep, string optimization, string metric, int contrast, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(896);
		JlNativeApi.Store(proc, 1, template);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   用模板图像的 domain 区域训练不带尺度缩放的形状模型，一步得到可用新对象（原生 id 897）；训练参数取向与全部注意事项详见 CreateShapeModel(JlTuple 版)。
	/// </summary>
	/// <param name="template">用于创建模型的输入图像（取其 domain 区域）。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "use_polarity"</param>
	/// <param name="contrast">模板图像中目标对比度的阈值或迟滞阈值，以及可选的目标部件最小尺寸。Default: "auto"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: "auto"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>用模板图像训练不带尺度缩放的形状模型，一步得到可用模型。与 CreateShapeModel(JlTuple 版) 共用原生 id 897，训练参数取向与全部注意事项详见该方法。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel(template, "auto", -0.39, 0.79, "auto", "auto", "use_polarity", 30, 15);
	///   </code>
	/// </remarks>
	public JlShapeModel(JlImage template, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, JlTuple optimization, string metric, JlTuple contrast, JlTuple minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(897);
		JlNativeApi.Store(proc, 1, template);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   CreateShapeModel 的标量入参构造版（原生 id 897），一步得到可用新对象；numLevels、angleStep、contrast、minContrast 只能给具体数值。
	/// </summary>
	/// <param name="template">用于创建模型的输入图像（取其 domain 区域）。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "use_polarity"</param>
	/// <param name="contrast">模板图像中目标对比度的阈值或迟滞阈值，以及可选的目标部件最小尺寸。Default: "auto"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: "auto"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>CreateShapeModel 的标量入参构造版，原生 id 同为 897；numLevels、angleStep、contrast、minContrast 只能给具体数值。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel(template, 3, -0.39, 0.79, 0.05, "auto", "use_polarity", 30, 15);
	///   </code>
	/// </remarks>
	public JlShapeModel(JlImage template, int numLevels, double angleStart, double angleExtent, double angleStep, string optimization, string metric, int contrast, int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(897);
		JlNativeApi.Store(proc, 1, template);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   调用原生 id 2122 创建尚无模型点的空形状模型句柄，供之后 CreateXxx / ReadShapeModel / DeserializeShapeModel 原地填充；与 JlNCCModel 无参构造不同，这里确实调用了原生算子。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>调用原生 id 2122 创建一个尚无模型点的空形状模型句柄，之后再经 CreateXxx / ReadShapeModel / DeserializeShapeModel 原地填充。注意与 JlNCCModel 的无参构造不同：后者只是造一个未初始化句柄、不调用原生算子。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>本类的原地填充方法都先 Dispose 旧句柄再写入，这是必需的：基类 Load 对已初始化的句柄直接抛出 "Undisposed handle instance when loading output parameter"。因此拿本构造的空模型再训练是安全的。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel();
	///   model.CreateShapeModel(template, "auto", -0.39, 0.79, "auto", "auto", "use_polarity", 30, 15);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>空句柄同样要在生命周期结束时 Dispose（终结器兜底，时机不可控）。</para>
	/// </remarks>
	public JlShapeModel()
	{
		IntPtr proc = JlNativeApi.PreCall(2122);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		byte[] value = SerializeShapeModel();
		info.AddValue("data", value, typeof(byte[]));
	}

	/// <summary>
	///   ISerializable 反序列化构造器：从 info 的 "data" 键取出 byte[]，调 DeserializeShapeModel（原生 id 874）原地装入新句柄，与存 "data" 的 GetObjectData 配对。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>方法体只有一行：取 <c>(byte[])info.GetValue("data", typeof(byte[]))</c> 后转调
	///   <c>DeserializeShapeModel(byte[])</c>——借原生 <c>deserialize_shape_model</c>（id 874）把字节经 <c>JlSerializationBuffer</c> 送进原生、
	///   <c>InitOCT</c>+<c>Load</c> 把新句柄装进这个刚出生的空壳（其内部先 Dispose 对空壳无实害）。</para>
	///   <para><b>约束或前提</b>由 .NET 二进制序列化框架自动调用；写侧配 <c>ISerializable.GetObjectData</c>，它存的是
	///   <c>SerializeShapeModel()</c>（id 876）的字节。字节缺失、截断或非本类型产出时在原生层抛异常、构造失败。手写代码不要直接调它。</para>
	///   <para><b>与相邻成员的取舍</b>自己管字节去向时直接配对 SerializeShapeModel/DeserializeShapeModel（下方示例演示的正是本构造器内部路径）；
	///   跨进程落流用 Serialize/Deserialize(Stream)；资产落盘用 WriteShapeModel 族。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   byte[] data = src.SerializeShapeModel();
	///   JlShapeModel dst = new JlShapeModel();
	///   dst.DeserializeShapeModel(data);
	///   dst.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>还原出的是独立新句柄，src 与 dst 各自 Dispose 互不影响；本构造器带 EditorBrowsable(Never)，不面向业务直调。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlShapeModel(SerializationInfo info, StreamingContext context)
	{
		DeserializeShapeModel((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把 SerializeShapeModel 得到的完整模型字节以 Vision 二进制格式写入流（实现经内存缓冲再落流）；不改变模型本身，new 隐藏基类同名方法。</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>把 SerializeShapeModel 得到的完整模型字节写入流（实现走内存缓冲再落流）。new 关键字隐藏基类同名方法，返回类型仍是当前类。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>跨进程/落盘用 WriteShapeModel 文件族更通用；本方法适合把模型塞进自定义容器（如配置流、网络帧）。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using System.IO;
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   MemoryStream ms = new MemoryStream();
	///   model.Serialize(ms);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>模型点数多时字节量不小，勿在流上重复累积；ms 需自行处置。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeShapeModel(), stream);
	}

	/// <summary>静态方法：先经无参构造建空模型（原生 id 2122），再用 DeserializeShapeModel（id 874）装入流中字节，返回独立新对象；读取会消耗流位置。</summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>静态方法：内部先经无参构造（原生 id 2122）建空模型，再调 DeserializeShapeModel(id 874) 填入流中的字节，返回独立新对象。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>流位置必须停在一段完整模型数据的起点，且该数据由本类 Serialize 写出；读取后流位置被消耗，重复读取需重置 Position。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using System.IO;
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   MemoryStream ms = new MemoryStream();
	///   src.Serialize(ms);
	///   ms.Position = 0;
	///   using JlShapeModel model = JlShapeModel.Deserialize(ms);
	///   </code>
	/// </remarks>
	public new static JlShapeModel Deserialize(Stream stream)
	{
		JlShapeModel hShapeModel = new JlShapeModel();
		hShapeModel.DeserializeShapeModel(JlSerializationBuffer.ReadFromStream(stream));
		return hShapeModel;
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>按 SerializeShapeModel + DeserializeShapeModel 做数据级深拷贝，返回独立的新句柄。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>比直接持有同一句柄贵（完整字节往返），换来的是两份模型互不影响：克隆体上再 Create*、SetShapeModelOrigin 等原地改动不会波及原件。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   using JlShapeModel backup = model.Clone();
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>原件与克隆各自持有句柄，需分别释放（或交给终结器兜底）。</para>
	/// </remarks>
	public new JlShapeModel Clone()
	{
		byte[] data = SerializeShapeModel();
		JlShapeModel obj = new JlShapeModel();
		obj.DeserializeShapeModel(data);
		return obj;
	}

	/// <summary>
	///   把 SerializeShapeModel 风格的字节数组反序列化并原地换入本对象（原生 id 874，先 Dispose 旧句柄）；失败时旧模型已被释放、本对象句柄失效，不能当作无操作。
	/// </summary>
	/// <param name="serializedItemHandle">序列化项的句柄。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>把 SerializeShapeModel 风格的字节数组反序列化并原地换入本对象（原生 id 874）。实现先 Dispose 旧句柄，否则基类 Load 会拒绝写入已初始化句柄并抛异常。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>与静态 Deserialize(Stream) 相比省去流包装，适合内存缓存（如配置中心下发的 byte[]）；与 Clone 相比方向相反：本方法覆盖自己，Clone 产生新对象。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   byte[] data = src.SerializeShapeModel();
	///   using JlShapeModel target = new JlShapeModel();
	///   target.DeserializeShapeModel(data);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>调用失败（数据非法）时抛异常，此时旧模型已被 Dispose、本对象句柄无效——不要把失败调用当"无操作"，更新失败应重建对象。</para>
	/// </remarks>
	public void DeserializeShapeModel(byte[] serializedItemHandle)
		{
		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(874);
		JlNativeApi.Store(proc, 0, buffer);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(buffer);
	}

	/// <summary>
	///   从文件读取模型并原地换入本对象（原生 id 875，先 Dispose 旧句柄，与构造器 JlShapeModel(string) 同 id）；读取失败时旧模型已被释放、本对象句柄失效。
	/// </summary>
	/// <param name="fileName">文件名。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>从文件读取模型并原地换入本对象（先 Dispose 旧句柄），与构造器 JlShapeModel(string) 共用原生 id 875，差别仅在是否复用对象。</para>
	///   <para><b>参数取向</b></para>
	///   <para>fileName 为 WriteShapeModel 写出的路径，含扩展名；跨版本兼容性 [待实测]。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel();
	///   model.ReadShapeModel("part_v2.shm");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>读取失败时旧模型已被释放，本对象句柄失效；重试请重建对象或再次 Read。</para>
	///   <para><b>相关算子</b></para>
	///   <para>FindShapeModel、WriteShapeModel、ClearShapeModel</para>
	/// </remarks>
	public void ReadShapeModel(string fileName)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(875);
		JlNativeApi.StoreS(proc, 0, fileName);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   返回当前模型全部数据的字节副本（原生 id 876），不产生文件、也不改变模型句柄；ISerializable 实现同样走它。
	/// </summary>
	/// <returns>序列化项的句柄。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>返回模型全部数据的字节副本（原生 id 876），不产生文件；ISerializable 实现也走它。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>落盘用 WriteShapeModel；内存缓存/自定义传输用本方法配 DeserializeShapeModel。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   byte[] data = model.SerializeShapeModel();
	///   </code>
	/// </remarks>
	public byte[] SerializeShapeModel()
	{
		IntPtr proc = JlNativeApi.PreCall(876);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		byte[] data = JlSerializationBuffer.LoadBytes(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return data;
	}

	/// <summary>
	///   把当前模型写入 fileName（原生 id 877，无输出参数，模型句柄与文件名的写入顺序同 C# 形参序）；模型状态不变，读回用 ReadShapeModel 或构造器。
	/// </summary>
	/// <param name="fileName">文件名。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>把模型写入 fileName（原生 id 877，无输出参数；模型句柄与文件名的写入顺序和 C# 形参顺序一致）。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>需要在进程间搬动用 SerializeShapeModel；固化产线配置用本方法，现场只需 ReadShapeModel/构造器恢复。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel(template, "auto", -0.39, 0.79, "auto", "auto", "use_polarity", 30, 15);
	///   model.WriteShapeModel("label.shm");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>对已 Dispose 的句柄调用属未定义行为 [待实测]；同名文件是否覆盖、目录不存在时报错 [待实测]。</para>
	///   <para><b>相关算子</b></para>
	///   <para>ReadShapeModel、CreateShapeModel</para>
	/// </remarks>
	public void WriteShapeModel(string fileName)
	{
		IntPtr proc = JlNativeApi.PreCall(877);
		Store(proc, 0);
		JlNativeApi.StoreS(proc, 1, fileName);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   调用原生算子（id 878）直接释放模型内存；不会把 C# 侧 mHandle 置空，之后再 Dispose 会对同一原生句柄二次释放，调用后本对象不可再用。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>调用原生算子直接释放模型的内存（id 878）。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>托管侧 Dispose() 也会经 ClearHandle 释放句柄，因此一般不需要调用本方法。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>本方法不会把 C# 对象的 mHandle 置空：之后再 Dispose 会对同一原生句柄二次释放，是否被原生层保护 [待实测]；调用后继续使用本对象也是未定义行为。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   JlShapeModel model = new JlShapeModel("part.shm");
	///   model.ClearShapeModel(); // 原生内存自此失效，不要再把该对象传给任何算子
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>CreateShapeModel</para>
	/// </remarks>
	public void ClearShapeModel()
	{
		IntPtr proc = JlNativeApi.PreCall(878);
		Store(proc, 0);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   返回指定金字塔层上的模型轮廓（原生 id 879，独立 JlXLDCont 新句柄，需自行 Dispose）；用于核查训练得到的模型点，或喂给 CreateShapeModelXld 系列做二次训练。
	/// </summary>
	/// <param name="level">返回轮廓表示所对应的金字塔层级。Default: 1</param>
	/// <returns>形状模型的轮廓表示。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>返回指定金字塔层上的模型轮廓（原生 id 879，返回独立 JlXLDCont 新句柄）。用于检查训练得到的模型点是否合理，或把轮廓喂给 CreateShapeModelXld 系列做二次训练。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>level 是已训练的层索引，超出范围的行为 [待实测]（含索引起点）；层数可用 GetShapeModelParams 返回的 num_levels 核对。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>本库无显示族；取回轮廓后可自行转存或做点数统计，代替在线可视化调试。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   int levels = model.GetShapeModelParams(out double angleStart, out double angleExtent, out double angleStep, out double scaleMin, out double scaleMax, out double scaleStep, out string metric, out int minContrast);
	///   using JlXLDCont contours = model.GetShapeModelContours(levels - 1);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>返回的 XLDCont 需自行 Dispose。</para>
	/// </remarks>
	public JlXLDCont GetShapeModelContours(int level)
	{
		IntPtr proc = JlNativeApi.PreCall(879);
		Store(proc, 0);
		JlNativeApi.StoreI(proc, 1, level);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlXLDCont.LoadNew(proc, 1, err, out var obj);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   回读训练时定下的模型参数（原生 id 881）：返回金字塔层数，角度按 LoadD 读为标量，scaleMin/scaleMax/scaleStep 以 JlTuple 读出以完整保留多值；只读不改模型。
	/// </summary>
	/// <param name="angleStart">模板的最小旋转角。</param>
	/// <param name="angleExtent">旋转角度的范围。</param>
	/// <param name="angleStep">角度的步长（分辨率）。</param>
	/// <param name="scaleMin">模板的最小尺度。</param>
	/// <param name="scaleMax">模板的最大尺度。</param>
	/// <param name="scaleStep">尺度步长（分辨率）。</param>
	/// <param name="metric">匹配度量方式。</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。</param>
	/// <returns>金字塔层数。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>回读训练时定下的模型参数（原生 id 881）：返回值是金字塔层数，角度经 LoadD 读标量，scaleMin/scaleMax/scaleStep 以 JlTuple 读出（尺度训练过多值时完整保留），metric 字符串、minContrast 整数。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>训练后核对"auto 展开出的实际值"、以及确认模型是否带尺度，都应先用它，再决定用哪支 Find。角度/尺度范围训练后不能改（本类只有 SetShapeModelParam 通用口，能否覆盖这些 [待实测]）。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   JlTuple levels = model.GetShapeModelParams(out double angleStart, out double angleExtent, out double angleStep, out JlTuple scaleMin, out JlTuple scaleMax, out JlTuple scaleStep, out string metric, out int minContrast);
	///   if (scaleMin.Length == 0)
	///   {
	///       // 无尺度信息，用 FindShapeModel 即可
	///   }
	///   </code>
	/// </remarks>
	public JlTuple GetShapeModelParams(out double angleStart, out double angleExtent, out double angleStep, out JlTuple scaleMin, out JlTuple scaleMax, out JlTuple scaleStep, out string metric, out int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(881);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		JlNativeApi.InitOCT(proc, 8);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlTuple.LoadNew(proc, 0, JlTupleType.INTEGER, err, out var tuple);
		err = JlNativeApi.LoadD(proc, 1, err, out angleStart);
		err = JlNativeApi.LoadD(proc, 2, err, out angleExtent);
		err = JlNativeApi.LoadD(proc, 3, err, out angleStep);
		err = JlTuple.LoadNew(proc, 4, JlTupleType.DOUBLE, err, out scaleMin);
		err = JlTuple.LoadNew(proc, 5, JlTupleType.DOUBLE, err, out scaleMax);
		err = JlTuple.LoadNew(proc, 6, JlTupleType.DOUBLE, err, out scaleStep);
		err = JlNativeApi.LoadS(proc, 7, err, out metric);
		err = JlNativeApi.LoadI(proc, 8, err, out minContrast);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return tuple;
	}

	/// <summary>
	///   原生 id 881 的标量读出版：层数经 LoadI 返回 int，scaleMin/scaleMax/scaleStep 按单值 LoadD 读取；模型带多个尺度值时应改用 JlTuple 重载。
	/// </summary>
	/// <param name="angleStart">模板的最小旋转角。</param>
	/// <param name="angleExtent">旋转角度的范围。</param>
	/// <param name="angleStep">角度的步长（分辨率）。</param>
	/// <param name="scaleMin">模板的最小尺度。</param>
	/// <param name="scaleMax">模板的最大尺度。</param>
	/// <param name="scaleStep">尺度步长（分辨率）。</param>
	/// <param name="metric">匹配度量方式。</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。</param>
	/// <returns>金字塔层数。</returns>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>同原生 id 881 的标量读出版：层数经 LoadI 返回 int，scaleMin/scaleMax/scaleStep 按单值 LoadD 读取。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>模型带多个尺度值时，单值读法会丢失其余值或报错 [待实测]；多尺度模型请改用 JlTuple 重载。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   int numLevels = model.GetShapeModelParams(out double angleStart, out double angleExtent, out double angleStep, out double scaleMin, out double scaleMax, out double scaleStep, out string metric, out int minContrast);
	///   </code>
	/// </remarks>
	public int GetShapeModelParams(out double angleStart, out double angleExtent, out double angleStep, out double scaleMin, out double scaleMax, out double scaleStep, out string metric, out int minContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(881);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		JlNativeApi.InitOCT(proc, 2);
		JlNativeApi.InitOCT(proc, 3);
		JlNativeApi.InitOCT(proc, 4);
		JlNativeApi.InitOCT(proc, 5);
		JlNativeApi.InitOCT(proc, 6);
		JlNativeApi.InitOCT(proc, 7);
		JlNativeApi.InitOCT(proc, 8);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadI(proc, 0, err, out var intValue);
		err = JlNativeApi.LoadD(proc, 1, err, out angleStart);
		err = JlNativeApi.LoadD(proc, 2, err, out angleExtent);
		err = JlNativeApi.LoadD(proc, 3, err, out angleStep);
		err = JlNativeApi.LoadD(proc, 4, err, out scaleMin);
		err = JlNativeApi.LoadD(proc, 5, err, out scaleMax);
		err = JlNativeApi.LoadD(proc, 6, err, out scaleStep);
		err = JlNativeApi.LoadS(proc, 7, err, out metric);
		err = JlNativeApi.LoadI(proc, 8, err, out minContrast);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		return intValue;
	}

	/// <summary>
	///   读出模型参考点的行、列坐标（原生 id 882，训练图像素坐标系）；Find* 返回的 row/column 就是这个点在搜索图像中的位置。
	/// </summary>
	/// <param name="row">形状模型原点的行坐标。</param>
	/// <param name="column">形状模型原点的列坐标。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>读出模型的参考点（原生 id 882）。Find* 返回的 row/column 就是这个点在图像中的位置。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>参考点不满意时配合 SetShapeModelOrigin 修改，改的是模型本身，保存前改一次即可长期生效。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   model.GetShapeModelOrigin(out double row, out double column);
	///   </code>
	/// </remarks>
	public void GetShapeModelOrigin(out double row, out double column)
	{
		IntPtr proc = JlNativeApi.PreCall(882);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadD(proc, 0, err, out row);
		err = JlNativeApi.LoadD(proc, 1, err, out column);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   原地设置模型参考点（原生 id 883，坐标取模型/训练图像素坐标系）；之后所有 Find* 的 row/column 都相对新参考点报告，WriteShapeModel 保存的也是新参考点。
	/// </summary>
	/// <param name="row">形状模型原点的行坐标。</param>
	/// <param name="column">形状模型原点的列坐标。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>设置模型参考点（原生 id 883，原地修改）。之后所有 Find* 的 row/column 都相对新参考点报告，且 WriteShapeModel 保存的也是新参考点。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>坐标是模型（训练图）像素坐标系下的位置；参考点落在轮廓外是否允许 [待实测]。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel(template, "auto", -0.39, 0.79, "auto", "auto", "use_polarity", 30, 15);
	///   model.GetShapeModelOrigin(out double row0, out double col0);
	///   model.SetShapeModelOrigin(row0, col0); // 此处演示读后回写；实际改为期望的基准位置
	///   </code>
	/// </remarks>
	public void SetShapeModelOrigin(double row, double column)
	{
		IntPtr proc = JlNativeApi.PreCall(883);
		Store(proc, 0);
		JlNativeApi.StoreD(proc, 1, row);
		JlNativeApi.StoreD(proc, 2, column);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>一次调用在同图搜索多个各向异性模型（原生 id 884）：modelIDs 经 ConcatArray 拼为句柄元组，输出七个等长元组，model 给出命中实例对应的输入数组下标；不产生新句柄。</summary>
	/// <param name="image">要查找各模型的输入图像。</param>
	/// <param name="modelIDs">各模型的句柄。</param>
	/// <param name="angleStart">各模型的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="scaleRMin">各模型在行方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleRMax">各模型在行方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleCMin">各模型在列方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleCMax">各模型在列方向的最大尺度。Default: 1.1</param>
	/// <param name="minScore">要查找的各模型实例的最低得分。Default: 0.5</param>
	/// <param name="numMatches">要查找的各模型实例数量（0 表示查找全部匹配）。Default: 1</param>
	/// <param name="maxOverlap">要查找的各模型实例间的最大重叠。Default: 0.5</param>
	/// <param name="subPixel">不为 'none' 时的亚像素精度。Default: "least_squares"</param>
	/// <param name="numLevels">匹配时使用的金字塔层数（若 $|NumLevels| = 2$ 则为所使用的最低金字塔层级）。Default: 0</param>
	/// <param name="greediness">"Greediness" of the search heuristic (0: safe but slow; 1: fast but matches may be missed). Default: 0.9</param>
	/// <param name="row">找到的各模型实例的行坐标。</param>
	/// <param name="column">找到的各模型实例的列坐标。</param>
	/// <param name="angle">找到的各模型实例的旋转角。</param>
	/// <param name="scaleR">找到的各模型实例在行方向的尺度。</param>
	/// <param name="scaleC">找到的各模型实例在列方向的尺度。</param>
	/// <param name="score">找到的各模型实例的得分。</param>
	/// <param name="model">找到的实例对应的模型索引。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>一次调用在同图搜索多个各向异性模型（原生 id 884）。实现上先把 modelIDs 数组经 ConcatArray 拼成句柄元组作为控制参数 0，图像作为图标参数 1。输出七个等长元组，model 为命中实例对应输入数组的下标。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>数组中所有模型须都是 CreateAnisoShapeModel(*/Xld) 训练 [待实测: 混入无尺度模型的行为]；各模型共用同一组搜索区间与阈值。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>逐模型循环调 FindAnisoShapeModel 结果等价，但每模型都要完整扫一遍图像；批量版只在原生层共享预处理。多结果时 num_matches 是全部模型合计取前 N，与 min_score 谁先卡住谁 [待实测]。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("scene.png");
	///   using JlShapeModel modelA = new JlShapeModel("a.shm");
	///   using JlShapeModel modelB = new JlShapeModel("b.shm");
	///   JlShapeModel[] models = { modelA, modelB };
	///   JlShapeModel.FindAnisoShapeModels(img, models, -0.39, 0.79, 0.9, 1.1, 0.9, 1.1, 0.5, 1, 0.5, "least_squares", 0, 0.9, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score, out JlTuple model);
	///   if (model.Length &gt; 0)
	///   {
	///       int which = model[0].I; // 命中模型的数组下标，起点是否 0-based [待实测]
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>无匹配时 out 为空元组还是抛异常 [待实测]；七个 out 元组按同一 i 对齐取值。</para>
	/// </remarks>
	public static void FindAnisoShapeModels(JlImage image, JlShapeModel[] modelIDs, JlTuple angleStart, JlTuple angleExtent, JlTuple scaleRMin, JlTuple scaleRMax, JlTuple scaleCMin, JlTuple scaleCMax, JlTuple minScore, JlTuple numMatches, JlTuple maxOverlap, JlTuple subPixel, JlTuple numLevels, JlTuple greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score, out JlTuple model)
	{
		JlTuple hTuple = JlHandleBase.ConcatArray(modelIDs);
		IntPtr proc = JlNativeApi.PreCall(884);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>
	///   批量各向异性搜索的原地版：本对象作为唯一模型写入模型槽位（原生 id 884），参数标量直写、无 pin/unpin，输出七个等长元组并含命中实例所属模型的下标。
	/// </summary>
	/// <param name="image">要查找各模型的输入图像。</param>
	/// <param name="angleStart">各模型的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="scaleRMin">各模型在行方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleRMax">各模型在行方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleCMin">各模型在列方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleCMax">各模型在列方向的最大尺度。Default: 1.1</param>
	/// <param name="minScore">要查找的各模型实例的最低得分。Default: 0.5</param>
	/// <param name="numMatches">要查找的各模型实例数量（0 表示查找全部匹配）。Default: 1</param>
	/// <param name="maxOverlap">要查找的各模型实例间的最大重叠。Default: 0.5</param>
	/// <param name="subPixel">不为 'none' 时的亚像素精度。Default: "least_squares"</param>
	/// <param name="numLevels">匹配时使用的金字塔层数（若 $|NumLevels| = 2$ 则为所使用的最低金字塔层级）。Default: 0</param>
	/// <param name="greediness">"Greediness" of the search heuristic (0: safe but slow; 1: fast but matches may be missed). Default: 0.9</param>
	/// <param name="row">找到的各模型实例的行坐标。</param>
	/// <param name="column">找到的各模型实例的列坐标。</param>
	/// <param name="angle">找到的各模型实例的旋转角。</param>
	/// <param name="scaleR">找到的各模型实例在行方向的尺度。</param>
	/// <param name="scaleC">找到的各模型实例在列方向的尺度。</param>
	/// <param name="score">找到的各模型实例的得分。</param>
	/// <param name="model">找到的实例对应的模型索引。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>批量各向异性搜索的原地版：本对象作为唯一模型写入模型槽位，原生 id 同为 884；参数标量直写、无 pin/unpin；model 输出为每个命中实例所属模型的下标，单模型时全部同值 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>单模型场景直接用它或 FindAnisoShapeModel（id 887）皆可，前者多回传一个 model 下标。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("scene.png");
	///   using JlShapeModel model = new JlShapeModel("a.shm");
	///   model.FindAnisoShapeModels(img, -0.39, 0.79, 0.9, 1.1, 0.9, 1.1, 0.5, 1, 0.5, "least_squares", 0, 0.9, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score, out JlTuple modelIdx);
	///   </code>
	/// </remarks>
	public void FindAnisoShapeModels(JlImage image, double angleStart, double angleExtent, double scaleRMin, double scaleRMax, double scaleCMin, double scaleCMax, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score, out JlTuple model)
	{
		IntPtr proc = JlNativeApi.PreCall(884);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>一次调用在同图搜索多个各向同性缩放模型（原生 id 885）：modelIDs 经 ConcatArray 拼为句柄元组，输出六个等长元组，model 为命中实例的输入数组下标；模型须已训练出尺度。</summary>
	/// <param name="image">要查找各模型的输入图像。</param>
	/// <param name="modelIDs">各模型的句柄。</param>
	/// <param name="angleStart">各模型的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.78</param>
	/// <param name="scaleMin">各模型的最小尺度。Default: 0.9</param>
	/// <param name="scaleMax">各模型的最大尺度。Default: 1.1</param>
	/// <param name="minScore">要查找的各模型实例的最低得分。Default: 0.5</param>
	/// <param name="numMatches">要查找的各模型实例数量（0 表示查找全部匹配）。Default: 1</param>
	/// <param name="maxOverlap">要查找的各模型实例间的最大重叠。Default: 0.5</param>
	/// <param name="subPixel">不为 'none' 时的亚像素精度。Default: "least_squares"</param>
	/// <param name="numLevels">匹配时使用的金字塔层数（若 $|NumLevels| = 2$ 则为所使用的最低金字塔层级）。Default: 0</param>
	/// <param name="greediness">"Greediness" of the search heuristic (0: safe but slow; 1: fast but matches may be missed). Default: 0.9</param>
	/// <param name="row">找到的各模型实例的行坐标。</param>
	/// <param name="column">找到的各模型实例的列坐标。</param>
	/// <param name="angle">找到的各模型实例的旋转角。</param>
	/// <param name="scale">找到的各模型实例的尺度。</param>
	/// <param name="score">找到的各模型实例的得分。</param>
	/// <param name="model">找到的实例对应的模型索引。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>一次调用在同图搜索多个各向同性缩放模型（原生 id 885）：modelIDs 经 ConcatArray 拼为句柄元组，输出六个等长元组，model 为命中实例的输入数组下标。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>数组中模型须经 CreateScaledShapeModel(*/Xld) 训练出尺度；各模型共用同一组搜索区间与阈值。默认模板里 angle_extent 用 0.78 而训练侧用 0.79，弧度值本身无含义差异，但提示搜索/训练区间要各自显式对齐 [待实测]。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("scene.png");
	///   using JlShapeModel modelA = new JlShapeModel("a.shm");
	///   using JlShapeModel modelB = new JlShapeModel("b.shm");
	///   JlShapeModel[] models = { modelA, modelB };
	///   JlShapeModel.FindScaledShapeModels(img, models, -0.39, 0.78, 0.9, 1.1, 0.5, 1, 0.5, "least_squares", 0, 0.9, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score, out JlTuple model);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>六个 out 元组等长；无匹配时空元组还是抛异常 [待实测]。其余搜索参数取向见 FindShapeModel(JlTuple 版)。</para>
	///   <para><b>相关算子</b></para>
	///   <para>CreateShapeModel、VectorAngleToRigid、GetShapeModelContours</para>
	/// </remarks>
	public static void FindScaledShapeModels(JlImage image, JlShapeModel[] modelIDs, JlTuple angleStart, JlTuple angleExtent, JlTuple scaleMin, JlTuple scaleMax, JlTuple minScore, JlTuple numMatches, JlTuple maxOverlap, JlTuple subPixel, JlTuple numLevels, JlTuple greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score, out JlTuple model)
	{
		JlTuple hTuple = JlHandleBase.ConcatArray(modelIDs);
		IntPtr proc = JlNativeApi.PreCall(885);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>
	///   批量各向同性搜索的原地版：本对象作为唯一模型写入模型槽位（原生 id 885），参数标量直写、无 pin/unpin，输出六个等长元组并含命中实例所属模型的下标。
	/// </summary>
	/// <param name="image">要查找各模型的输入图像。</param>
	/// <param name="angleStart">各模型的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.78</param>
	/// <param name="scaleMin">各模型的最小尺度。Default: 0.9</param>
	/// <param name="scaleMax">各模型的最大尺度。Default: 1.1</param>
	/// <param name="minScore">要查找的各模型实例的最低得分。Default: 0.5</param>
	/// <param name="numMatches">要查找的各模型实例数量（0 表示查找全部匹配）。Default: 1</param>
	/// <param name="maxOverlap">要查找的各模型实例间的最大重叠。Default: 0.5</param>
	/// <param name="subPixel">不为 'none' 时的亚像素精度。Default: "least_squares"</param>
	/// <param name="numLevels">匹配时使用的金字塔层数（若 $|NumLevels| = 2$ 则为所使用的最低金字塔层级）。Default: 0</param>
	/// <param name="greediness">"Greediness" of the search heuristic (0: safe but slow; 1: fast but matches may be missed). Default: 0.9</param>
	/// <param name="row">找到的各模型实例的行坐标。</param>
	/// <param name="column">找到的各模型实例的列坐标。</param>
	/// <param name="angle">找到的各模型实例的旋转角。</param>
	/// <param name="scale">找到的各模型实例的尺度。</param>
	/// <param name="score">找到的各模型实例的得分。</param>
	/// <param name="model">找到的实例对应的模型索引。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>批量各向同性搜索的原地版：本对象作为唯一模型写入模型槽位，原生 id 同为 885；参数标量直写、无 pin/unpin，model 输出为命中实例所属模型下标（单模型时全部同值 [待实测]）。搜索参数取向见 FindShapeModel(JlTuple 版)。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("scene.png");
	///   using JlShapeModel model = new JlShapeModel("a.shm");
	///   model.FindScaledShapeModels(img, -0.39, 0.78, 0.9, 1.1, 0.5, 1, 0.5, "least_squares", 0, 0.9, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score, out JlTuple modelIdx);
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>CreateShapeModel、VectorAngleToRigid、GetShapeModelContours</para>
	/// </remarks>
	public void FindScaledShapeModels(JlImage image, double angleStart, double angleExtent, double scaleMin, double scaleMax, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score, out JlTuple model)
	{
		IntPtr proc = JlNativeApi.PreCall(885);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>一次调用在同图搜索多个无尺度模型（原生 id 886）：modelIDs 经 ConcatArray 拼为句柄元组，输出五个等长元组，model 为命中实例的输入数组下标；带尺度模型须改用 Scaled/Aniso 版才取得回尺度。</summary>
	/// <param name="image">要查找各模型的输入图像。</param>
	/// <param name="modelIDs">各模型的句柄。</param>
	/// <param name="angleStart">各模型的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="minScore">要查找的各模型实例的最低得分。Default: 0.5</param>
	/// <param name="numMatches">要查找的各模型实例数量（0 表示查找全部匹配）。Default: 1</param>
	/// <param name="maxOverlap">要查找的各模型实例间的最大重叠。Default: 0.5</param>
	/// <param name="subPixel">不为 'none' 时的亚像素精度。Default: "least_squares"</param>
	/// <param name="numLevels">匹配时使用的金字塔层数（若 $|NumLevels| = 2$ 则为所使用的最低金字塔层级）。Default: 0</param>
	/// <param name="greediness">"Greediness" of the search heuristic (0: safe but slow; 1: fast but matches may be missed). Default: 0.9</param>
	/// <param name="row">找到的各模型实例的行坐标。</param>
	/// <param name="column">找到的各模型实例的列坐标。</param>
	/// <param name="angle">找到的各模型实例的旋转角。</param>
	/// <param name="score">找到的各模型实例的得分。</param>
	/// <param name="model">找到的实例对应的模型索引。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>一次调用在同图搜索多个无尺度模型（原生 id 886）：modelIDs 经 ConcatArray 拼为句柄元组，输出五个等长元组，model 为命中实例的输入数组下标 [待实测: 0-based]。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>各模型共用同一组搜索区间与阈值；对带尺度的模型改用 FindScaledShapeModels/FindAnisoShapeModels 才能取回尺度。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>对每个模型循环调 FindShapeModel 结果等价但每模型都整扫一遍图像；批量版共享一次图像遍历。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("scene.png");
	///   using JlShapeModel modelA = new JlShapeModel("a.shm");
	///   using JlShapeModel modelB = new JlShapeModel("b.shm");
	///   JlShapeModel[] models = { modelA, modelB };
	///   JlShapeModel.FindShapeModels(img, models, -0.39, 0.79, 0.5, 10, 0.5, "least_squares", 0, 0.9, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>无匹配时 out 为空元组还是抛异常 [待实测]；其余搜索参数取向见 FindShapeModel(JlTuple 版)。</para>
	///   <para><b>相关算子</b></para>
	///   <para>CreateShapeModel、VectorAngleToRigid、GetShapeModelContours</para>
	/// </remarks>
	public static void FindShapeModels(JlImage image, JlShapeModel[] modelIDs, JlTuple angleStart, JlTuple angleExtent, JlTuple minScore, JlTuple numMatches, JlTuple maxOverlap, JlTuple subPixel, JlTuple numLevels, JlTuple greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		JlTuple hTuple = JlHandleBase.ConcatArray(modelIDs);
		IntPtr proc = JlNativeApi.PreCall(886);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
		GC.KeepAlive(modelIDs);
	}

	/// <summary>
	///   批量无尺度搜索的原地版：本对象作为唯一模型写入模型槽位（原生 id 886），参数标量直写、无 pin/unpin，输出五个等长元组并含命中实例所属模型的下标。
	/// </summary>
	/// <param name="image">要查找各模型的输入图像。</param>
	/// <param name="angleStart">各模型的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="minScore">要查找的各模型实例的最低得分。Default: 0.5</param>
	/// <param name="numMatches">要查找的各模型实例数量（0 表示查找全部匹配）。Default: 1</param>
	/// <param name="maxOverlap">要查找的各模型实例间的最大重叠。Default: 0.5</param>
	/// <param name="subPixel">不为 'none' 时的亚像素精度。Default: "least_squares"</param>
	/// <param name="numLevels">匹配时使用的金字塔层数（若 $|NumLevels| = 2$ 则为所使用的最低金字塔层级）。Default: 0</param>
	/// <param name="greediness">"Greediness" of the search heuristic (0: safe but slow; 1: fast but matches may be missed). Default: 0.9</param>
	/// <param name="row">找到的各模型实例的行坐标。</param>
	/// <param name="column">找到的各模型实例的列坐标。</param>
	/// <param name="angle">找到的各模型实例的旋转角。</param>
	/// <param name="score">找到的各模型实例的得分。</param>
	/// <param name="model">找到的实例对应的模型索引。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>批量无尺度搜索的原地版：本对象作为唯一模型写入模型槽位，原生 id 同为 886；参数标量直写、无 pin/unpin，model 输出为命中实例所属模型下标（单模型时全部同值 [待实测]）。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage img = new JlImage("scene.png");
	///   using JlShapeModel model = new JlShapeModel("a.shm");
	///   model.FindShapeModels(img, -0.39, 0.79, 0.5, 10, 0.5, "least_squares", 0, 0.9, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple modelIdx);
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>CreateShapeModel、VectorAngleToRigid、GetShapeModelContours</para>
	/// </remarks>
	public void FindShapeModels(JlImage image, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		IntPtr proc = JlNativeApi.PreCall(886);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   在本各向异性模型的图像内按行、列各自的尺度区间搜索（原生 id 887），输出 row/column/angle/scaleR/scaleC/score 六个等长元组；搜索空间是行尺度份数×列尺度份数，比各向同性更耗时。
	/// </summary>
	/// <param name="image">要查找该模型的输入图像。</param>
	/// <param name="angleStart">模型的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="scaleRMin">模型在行方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleRMax">模型在行方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleCMin">模型在列方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleCMax">模型在列方向的最大尺度。Default: 1.1</param>
	/// <param name="minScore">要查找的模型实例的最低得分。Default: 0.5</param>
	/// <param name="numMatches">要查找的模型实例数量（0 表示查找全部匹配）。Default: 1</param>
	/// <param name="maxOverlap">要查找的模型实例间的最大重叠。Default: 0.5</param>
	/// <param name="subPixel">不为 'none' 时的亚像素精度。Default: "least_squares"</param>
	/// <param name="numLevels">匹配时使用的金字塔层数（若 $|NumLevels| = 2$ 则为所使用的最低金字塔层级）。Default: 0</param>
	/// <param name="greediness">"Greediness" of the search heuristic (0: safe but slow; 1: fast but matches may be missed). Default: 0.9</param>
	/// <param name="row">找到的模型实例的行坐标。</param>
	/// <param name="column">找到的模型实例的列坐标。</param>
	/// <param name="angle">找到的模型实例的旋转角。</param>
	/// <param name="scaleR">找到的模型实例在行方向的尺度。</param>
	/// <param name="scaleC">找到的模型实例在列方向的尺度。</param>
	/// <param name="score">找到的模型实例的得分。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>在图像内搜索各向异性缩放模型（原生 id 887）：行、列方向各自给定尺度区间，输出 row/column/angle/scaleR/scaleC/score 六个等长元组。适用于非等比形变（如打印拉伸、料斗内受压变形）的目标。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>模型须经 CreateAnisoShapeModel(*/Xld) 训练；搜索区间应落在训练区间内 [待实测]。各向异性搜索空间是行尺度份数×列尺度份数，比各向同性更费时间。</para>
	///   <para><b>参数取向</b></para>
	///   <para>其余搜索参数取向同 FindShapeModel(JlTuple 版)；若行列尺度总是相同，用 FindScaledShapeModel 更快。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel(template, "auto", -0.39, 0.79, "auto", 0.9, 1.1, "auto", 0.9, 1.1, "auto", "auto", "use_polarity", 30, 15);
	///   using JlImage img = new JlImage("scene.png");
	///   model.FindAnisoShapeModel(img, -0.39, 0.79, 0.9, 1.1, 0.9, 1.1, 0.6, 1, 0.5, "least_squares", 0, 0.9, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>六个 out 元组等长；无匹配时空元组还是抛异常 [待实测]。</para>
	/// </remarks>
	public void FindAnisoShapeModel(JlImage image, double angleStart, double angleExtent, double scaleRMin, double scaleRMax, double scaleCMin, double scaleCMax, JlTuple minScore, int numMatches, double maxOverlap, JlTuple subPixel, JlTuple numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(887);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   原生 id 887 的标量入参版：minScore/subPixel/numLevels 直写、无 pin/unpin，输出与 JlTuple 版一致的六个等长元组（含行、列双尺度），语义一致。
	/// </summary>
	/// <param name="image">要查找该模型的输入图像。</param>
	/// <param name="angleStart">模型的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="scaleRMin">模型在行方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleRMax">模型在行方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleCMin">模型在列方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleCMax">模型在列方向的最大尺度。Default: 1.1</param>
	/// <param name="minScore">要查找的模型实例的最低得分。Default: 0.5</param>
	/// <param name="numMatches">要查找的模型实例数量（0 表示查找全部匹配）。Default: 1</param>
	/// <param name="maxOverlap">要查找的模型实例间的最大重叠。Default: 0.5</param>
	/// <param name="subPixel">不为 'none' 时的亚像素精度。Default: "least_squares"</param>
	/// <param name="numLevels">匹配时使用的金字塔层数（若 $|NumLevels| = 2$ 则为所使用的最低金字塔层级）。Default: 0</param>
	/// <param name="greediness">"Greediness" of the search heuristic (0: safe but slow; 1: fast but matches may be missed). Default: 0.9</param>
	/// <param name="row">找到的模型实例的行坐标。</param>
	/// <param name="column">找到的模型实例的列坐标。</param>
	/// <param name="angle">找到的模型实例的旋转角。</param>
	/// <param name="scaleR">找到的模型实例在行方向的尺度。</param>
	/// <param name="scaleC">找到的模型实例在列方向的尺度。</param>
	/// <param name="score">找到的模型实例的得分。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>同原生 id 887 的标量入参版，minScore/subPixel/numLevels 直写、无 pin/unpin，语义与 FindAnisoShapeModel(JlTuple 版) 一致。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part_a.shm");
	///   using JlImage img = new JlImage("scene.png");
	///   model.FindAnisoShapeModel(img, -0.39, 0.79, 0.9, 1.1, 0.9, 1.1, 0.5, 1, 0.5, "least_squares", 0, 0.9, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score);
	///   </code>
	/// </remarks>
	public void FindAnisoShapeModel(JlImage image, double angleStart, double angleExtent, double scaleRMin, double scaleRMax, double scaleCMin, double scaleCMax, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(887);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   在本各向同性缩放模型的图像内搜索（原生 id 888）：比 FindShapeModel 多给尺度区间并回传 scale，输出 row/column/angle/scale/score 五个等长元组；模型须已训练出尺度。
	/// </summary>
	/// <param name="image">要查找该模型的输入图像。</param>
	/// <param name="angleStart">模型的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.78</param>
	/// <param name="scaleMin">模型的最小尺度。Default: 0.9</param>
	/// <param name="scaleMax">模型的最大尺度。Default: 1.1</param>
	/// <param name="minScore">要查找的模型实例的最低得分。Default: 0.5</param>
	/// <param name="numMatches">要查找的模型实例数量（0 表示查找全部匹配）。Default: 1</param>
	/// <param name="maxOverlap">要查找的模型实例间的最大重叠。Default: 0.5</param>
	/// <param name="subPixel">不为 'none' 时的亚像素精度。Default: "least_squares"</param>
	/// <param name="numLevels">匹配时使用的金字塔层数（若 $|NumLevels| = 2$ 则为所使用的最低金字塔层级）。Default: 0</param>
	/// <param name="greediness">"Greediness" of the search heuristic (0: safe but slow; 1: fast but matches may be missed). Default: 0.9</param>
	/// <param name="row">找到的模型实例的行坐标。</param>
	/// <param name="column">找到的模型实例的列坐标。</param>
	/// <param name="angle">找到的模型实例的旋转角。</param>
	/// <param name="scale">找到的模型实例的尺度。</param>
	/// <param name="score">找到的模型实例的得分。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>在图像内搜索各向同性缩放模型（原生 id 888）。与 FindShapeModel 的差别只在于额外给出尺度搜索区间并回传 scale：row/column/angle/scale/score 五个等长元组。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>模型须经 CreateScaledShapeModel(*/Xld) 训练出尺度；未训练尺度时 scale 输出如何取值 [待实测]。搜索的角与尺度区间应落在训练范围内 [待实测]。</para>
	///   <para><b>参数取向</b></para>
	///   <para>min_score/num_matches/max_overlap/sub_pixel/num_levels/greediness 同 FindShapeModel(JlTuple 版)；scaleMin/scaleMax 区间越大耗时越长。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel(template, "auto", -0.39, 0.79, "auto", 0.9, 1.1, "auto", "auto", "use_polarity", 30, 15);
	///   using JlImage img = new JlImage("scene.png");
	///   model.FindScaledShapeModel(img, -0.39, 0.79, 0.9, 1.1, 0.6, 1, 0.5, "least_squares", 0, 0.9, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>五个 out 元组等长，按同一 i 取值；无匹配时是空元组还是抛异常 [待实测]。</para>
	///   <para><b>相关算子</b></para>
	///   <para>CreateShapeModel、VectorAngleToRigid、GetShapeModelContours</para>
	/// </remarks>
	public void FindScaledShapeModel(JlImage image, double angleStart, double angleExtent, double scaleMin, double scaleMax, JlTuple minScore, int numMatches, double maxOverlap, JlTuple subPixel, JlTuple numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(888);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   原生 id 888 的标量入参版：minScore/subPixel/numLevels 直写、无 pin/unpin，仍输出 row/column/angle/scale/score 五个等长元组，语义与 JlTuple 版一致。
	/// </summary>
	/// <param name="image">要查找该模型的输入图像。</param>
	/// <param name="angleStart">模型的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.78</param>
	/// <param name="scaleMin">模型的最小尺度。Default: 0.9</param>
	/// <param name="scaleMax">模型的最大尺度。Default: 1.1</param>
	/// <param name="minScore">要查找的模型实例的最低得分。Default: 0.5</param>
	/// <param name="numMatches">要查找的模型实例数量（0 表示查找全部匹配）。Default: 1</param>
	/// <param name="maxOverlap">要查找的模型实例间的最大重叠。Default: 0.5</param>
	/// <param name="subPixel">不为 'none' 时的亚像素精度。Default: "least_squares"</param>
	/// <param name="numLevels">匹配时使用的金字塔层数（若 $|NumLevels| = 2$ 则为所使用的最低金字塔层级）。Default: 0</param>
	/// <param name="greediness">"Greediness" of the search heuristic (0: safe but slow; 1: fast but matches may be missed). Default: 0.9</param>
	/// <param name="row">找到的模型实例的行坐标。</param>
	/// <param name="column">找到的模型实例的列坐标。</param>
	/// <param name="angle">找到的模型实例的旋转角。</param>
	/// <param name="scale">找到的模型实例的尺度。</param>
	/// <param name="score">找到的模型实例的得分。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>同原生 id 888 的标量入参版，minScore/subPixel/numLevels 直写、无 pin/unpin，语义与 FindScaledShapeModel(JlTuple 版) 一致。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part_s.shm");
	///   using JlImage img = new JlImage("scene.png");
	///   model.FindScaledShapeModel(img, -0.39, 0.78, 0.9, 1.1, 0.5, 1, 0.5, "least_squares", 0, 0.9, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score);
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>CreateShapeModel、VectorAngleToRigid、GetShapeModelContours</para>
	/// </remarks>
	public void FindScaledShapeModel(JlImage image, double angleStart, double angleExtent, double scaleMin, double scaleMax, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(888);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   在图像内搜索本无尺度模型的最佳匹配（原生 id 889）：按边缘梯度而非灰度相关，输出 row/column/angle/score 四个等长元组，长度即实际匹配数；不改变模型状态。
	/// </summary>
	/// <param name="image">要查找该模型的输入图像。</param>
	/// <param name="angleStart">模型的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="minScore">要查找的模型实例的最低得分。Default: 0.5</param>
	/// <param name="numMatches">要查找的模型实例数量（0 表示查找全部匹配）。Default: 1</param>
	/// <param name="maxOverlap">要查找的模型实例间的最大重叠。Default: 0.5</param>
	/// <param name="subPixel">不为 'none' 时的亚像素精度。Default: "least_squares"</param>
	/// <param name="numLevels">匹配时使用的金字塔层数（若 $|NumLevels| = 2$ 则为所使用的最低金字塔层级）。Default: 0</param>
	/// <param name="greediness">"Greediness" of the search heuristic (0: safe but slow; 1: fast but matches may be missed). Default: 0.9</param>
	/// <param name="row">找到的模型实例的行坐标。</param>
	/// <param name="column">找到的模型实例的列坐标。</param>
	/// <param name="angle">找到的模型实例的旋转角。</param>
	/// <param name="score">找到的模型实例的得分。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>在图像内搜索单个无尺度形状模型的最佳匹配（原生 id 889）：模型句柄作为控制输入先写、图像后写，row/column/angle/score 四个 JlTuple 输出长度相同，等于实际匹配数。形状匹配基于边缘梯度而非灰度相关，对整体光照漂移较稳，但依赖模板边缘对比度。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>模型须已训练（CreateShapeModel/CreateShapeModelXld 系）；搜索角区间应落在训练角区间内，越界的处理 [待实测]；搜索图与模板通道格式要求一致 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>目标可能缩放时改用 FindScaledShapeModel/FindAnisoShapeModel，代价是搜索空间按尺度份数成倍变慢；多个模型同图搜索用 FindShapeModels，省去每个模型各扫一遍图像。</para>
	///   <para><b>参数取向</b></para>
	///   <para>min_score 越高误匹配越少、漏检越多，需按现场噪声标定；num_matches 给 0 收集全部合格实例 [待实测]；max_overlap 用于实例重叠去重，是否仅在 num_matches=0 时生效 [待实测]；greediness 越大越快但按英文参数说明可能漏检；sub_pixel 给 "none" 则结果为整像素，"least_squares" 做亚像素精化，是否另有 "max_score" 等取值 [待实测]；num_levels 给 0 使用全部已训练层（含负值与双元素形式的语义 [待实测]）。结果顺序（按 score 降序）[待实测]。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   using JlImage img = new JlImage("scene.png");
	///   model.FindShapeModel(img, -0.39, 0.79, 0.7, 5, 0.5, "least_squares", 0, 0.7, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
	///   for (int i = 0; i &lt; row.Length; i++)
	///   {
	///       double r = row[i].D;
	///   }
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>无匹配时 out 是空元组还是抛异常 [待实测]，稳妥写法是先判 row.Length；out 元组建议用完 Dispose。</para>
	///   <para><b>相关算子</b></para>
	///   <para>CreateShapeModel、VectorAngleToRigid、GetShapeModelContours</para>
	/// </remarks>
	public void FindShapeModel(JlImage image, double angleStart, double angleExtent, JlTuple minScore, int numMatches, double maxOverlap, JlTuple subPixel, JlTuple numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(889);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   原生 id 889 的标量入参版：minScore/subPixel/numLevels 经 StoreD/StoreS/StoreI 直写、无 pin/unpin 且不能传 "auto" 型元组值，搜索语义与 JlTuple 版完全一致。
	/// </summary>
	/// <param name="image">要查找该模型的输入图像。</param>
	/// <param name="angleStart">模型的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="minScore">要查找的模型实例的最低得分。Default: 0.5</param>
	/// <param name="numMatches">要查找的模型实例数量（0 表示查找全部匹配）。Default: 1</param>
	/// <param name="maxOverlap">要查找的模型实例间的最大重叠。Default: 0.5</param>
	/// <param name="subPixel">不为 'none' 时的亚像素精度。Default: "least_squares"</param>
	/// <param name="numLevels">匹配时使用的金字塔层数（若 $|NumLevels| = 2$ 则为所使用的最低金字塔层级）。Default: 0</param>
	/// <param name="greediness">"Greediness" of the search heuristic (0: safe but slow; 1: fast but matches may be missed). Default: 0.9</param>
	/// <param name="row">找到的模型实例的行坐标。</param>
	/// <param name="column">找到的模型实例的列坐标。</param>
	/// <param name="angle">找到的模型实例的旋转角。</param>
	/// <param name="score">找到的模型实例的得分。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>同原生 id 889 的标量入参版，搜索语义与 JlTuple 版完全一致；minScore/subPixel/numLevels 经 StoreD/StoreS/StoreI 直写，无 pin/unpin，且不能传 "auto" 型元组值。</para>
	///   <para><b>参数取向</b></para>
	///   <para>详见 FindShapeModel(JlTuple 版)。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   using JlImage img = new JlImage("scene.png");
	///   model.FindShapeModel(img, -0.39, 0.79, 0.7, 1, 0.5, "least_squares", 0, 0.9, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>CreateShapeModel、VectorAngleToRigid、GetShapeModelContours</para>
	/// </remarks>
	public void FindShapeModel(JlImage image, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, string subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		IntPtr proc = JlNativeApi.PreCall(889);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
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
		GC.KeepAlive(image);
	}

	/// <summary>
	///   为从 XLD 轮廓训练的模型事后改匹配 metric（原生 id 890，原地改）：用 image 与描述其相对模型位姿的 homMat2D 把图像摆到模型坐标系后逐点判定梯度极性；改动不写回已保存文件。
	/// </summary>
	/// <param name="image">用于确定极性的输入图像。</param>
	/// <param name="homMat2D">变换矩阵。</param>
	/// <param name="metric">匹配度量方式。Default: "use_polarity"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>为"从 XLD 轮廓训练"的模型事后修改匹配 metric（原生 id 890，原地改）。轮廓本身无灰度信息，极性要靠一张真实图像确定：实现中 image 写图标参数通道、homMat2D 写控制参数通道（代码里两处的 parIndex 同为 1 但属不同类别），原生层用 homMat2D 把 image 摆到模型坐标系后逐点判定梯度极性。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>仅适用于 CreateShapeModelXld 系列训练出的模型 [待实测: 图像训练模型上调用的行为]；homMat2D 应描述该 image 相对模型的实际位姿。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>训练期 metric 在 Create* 里定；训练后只有这条通道能改 metric，其余训练参数不可再改（SetShapeModelParam 能否覆盖 [待实测]）。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlXLDCont contours = new JlXLDCont(new double[] { 0, 0, 40, 40, 0 }, new double[] { 0, 20, 20, 0, 0 });
	///   using JlShapeModel model = new JlShapeModel(contours, "auto", -0.39, 0.79, "auto", "auto", "ignore_local_polarity", 5);
	///   using JlImage img = new JlImage("calib.png");
	///   JlHomMat2D pose = new JlHomMat2D();
	///   pose.HomMat2dIdentity();
	///   model.SetShapeModelMetric(img, pose, "use_polarity");
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>改动不会写回之前已保存的模型文件；改完需再次 WriteShapeModel。</para>
	/// </remarks>
	public void SetShapeModelMetric(JlImage image, JlHomMat2D homMat2D, string metric)
	{
		IntPtr proc = JlNativeApi.PreCall(890);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, image);
		JlNativeApi.Store(proc, 1, homMat2D);
		JlNativeApi.StoreS(proc, 2, metric);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(image);
	}

	/// <summary>
	///   模型通用参数写入口（原生 id 891，原地改）：名与值按位置配对、两元组在调用期间被 pin；粗到细匹配的两阶段机制经此类参数调节，本类无对应 getter，训练参数部分只能用 GetShapeModelParams 回读。
	/// </summary>
	/// <param name="genParamName">参数名。</param>
	/// <param name="genParamValue">参数值。</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>模型通用参数写入口（原生 id 891）：genParamName 与 genParamValue 按位置配对，两个元组都在调用期间 pin、调用后 unpin。粗到细匹配的两阶段机制经由这类通用参数调节——先在金字塔高层（降采样强、特征粗）快速全图筛候选，再在低层/原分辨率对候选精化，num_level_fine、pyramid_level_high_last 等即控制精化层数与高层截止位置，参数名与取值集合 [待实测]。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>角度/尺度范围、层数这类训练参数训练后是否还能经此覆盖 [待实测]；本类没有对应的 getter，只能靠 GetShapeModelParams 读训练参数部分。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   model.SetShapeModelParam(new JlTuple("angle"), new JlTuple(0.5));
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>参数名写错通常原生层报错 [待实测]；改完记得 WriteShapeModel 固化，否则重启丢失。</para>
	/// </remarks>
	public void SetShapeModelParam(JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(891);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, genParamName);
		JlNativeApi.Store(proc, 2, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
	}

	/// <summary>
	///   从 XLD 轮廓原地训练行、列尺度独立的各向异性模型（原生 id 892，先 Dispose 旧句柄再换入新句柄）：角度为弧度、extent 是从 start 起的区间宽度；训练完成后角度与尺度范围即固化，须用 FindAnisoShapeModel(s) 才取回 scaleR/scaleC。
	/// </summary>
	/// <param name="contours">用于创建模型的输入轮廓。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleRMin">模板在行方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleRMax">模板在行方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleRStep">行方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleCMin">模板在列方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleCMax">模板在列方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleCStep">列方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>从 XLD 轮廓原地训练各向异性缩放模型（原生 id 892，与构造器同 id；实现先 Dispose 旧句柄再换入新句柄，因为基类 Load 拒绝写已初始化句柄）。训练一旦完成，角度/尺度范围即固化，用 GetShapeModelParams 回读实际值。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>轮廓集合须描出目标边缘；没有模板图可依据，模型点梯度极性未知，故 metric 默认 "ignore_local_polarity"、minContrast 为必给整数（默认值见参数列表）。</para>
	///   <para><b>参数取向</b></para>
	///   <para>angle_start/angle_extent 为弧度且 extent 是从 start 起的区间宽度；角度份数=extent/step+1，行、列尺度份数各自 (max-min)/step+1，训练耗时与模型内存随三者乘积增长 [待实测]。行列尺度总是相等时用 CreateScaledShapeModelXld 或 CreateShapeModelXld，模型更小、匹配更快。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlXLDCont contours = src.GetShapeModelContours(1);
	///   using JlShapeModel model = new JlShapeModel();
	///   model.CreateAnisoShapeModelXld(contours, "auto", -0.39, 0.79, "auto", 0.9, 1.1, "auto", 0.9, 1.1, "auto", "auto", "ignore_local_polarity", 5);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>训练抛异常时旧句柄已被 Dispose、本对象句柄失效，需重建；匹配须配 FindAnisoShapeModel(s) 才能取回 scaleR/scaleC。</para>
	/// </remarks>
	public void CreateAnisoShapeModelXld(JlXLDCont contours, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleRMin, double scaleRMax, JlTuple scaleRStep, double scaleCMin, double scaleCMax, JlTuple scaleCStep, JlTuple optimization, string metric, int minContrast)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(892);
		JlNativeApi.Store(proc, 1, contours);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
	}

	/// <summary>
	///   CreateAnisoShapeModelXld(JlTuple 版) 的标量入参版（原生 id 892，原地换入新句柄）：numLevels、angleStep、scaleRStep、scaleCStep 必须给具体数值，其余取向见该方法。
	/// </summary>
	/// <param name="contours">用于创建模型的输入轮廓。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleRMin">模板在行方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleRMax">模板在行方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleRStep">行方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleCMin">模板在列方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleCMax">模板在列方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleCStep">列方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>CreateAnisoShapeModelXld(JlTuple 版) 的标量入参版，原生 id 同为 892；numLevels、angleStep、scaleRStep、scaleCStep 必须给具体数值，其余取向见该方法。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlXLDCont contours = src.GetShapeModelContours(1);
	///   using JlShapeModel model = new JlShapeModel();
	///   model.CreateAnisoShapeModelXld(contours, 3, -0.39, 0.79, 0.05, 0.9, 1.1, 0.05, 0.9, 1.1, 0.05, "auto", "ignore_local_polarity", 5);
	///   </code>
	/// </remarks>
	public void CreateAnisoShapeModelXld(JlXLDCont contours, int numLevels, double angleStart, double angleExtent, double angleStep, double scaleRMin, double scaleRMax, double scaleRStep, double scaleCMin, double scaleCMax, double scaleCStep, string optimization, string metric, int minContrast)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(892);
		JlNativeApi.Store(proc, 1, contours);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
	}

	/// <summary>
	///   从 XLD 轮廓原地训练各向同性缩放模型（原生 id 893，先 Dispose 再换入）：角度为弧度制，尺度与层数训练后固化并经 GetShapeModelParams 回读；匹配须配 FindScaledShapeModel(s) 才取回 scale。
	/// </summary>
	/// <param name="contours">用于创建模型的输入轮廓。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleMin">模板的最小尺度。Default: 0.9</param>
	/// <param name="scaleMax">模板的最大尺度。Default: 1.1</param>
	/// <param name="scaleStep">尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>从 XLD 轮廓原地训练各向同性缩放模型（原生 id 893，先 Dispose 再换入）。尺度训练参数与层数固化，训练后用 GetShapeModelParams 回读；JlTuple 重载会把 scaleMin/scaleMax/scaleStep 按元组读出。</para>
	///   <para><b>参数取向</b></para>
	///   <para>角度弧度制、份数计算与耗时关系同 CreateAnisoShapeModelXld；无各向异性形变需求时比 aniso 版省一半尺度份数。metric 默认与 minContrast 取值约束同 Xld 族。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlXLDCont contours = src.GetShapeModelContours(1);
	///   using JlShapeModel model = new JlShapeModel();
	///   model.CreateScaledShapeModelXld(contours, "auto", -0.39, 0.79, "auto", 0.9, 1.1, "auto", "auto", "ignore_local_polarity", 5);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>匹配须配 FindScaledShapeModel(s) 才能取回 scale。</para>
	///   <para><b>相关算子</b></para>
	///   <para>FindShapeModel、GetShapeModelContours、ClearShapeModel</para>
	/// </remarks>
	public void CreateScaledShapeModelXld(JlXLDCont contours, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleMin, double scaleMax, JlTuple scaleStep, JlTuple optimization, string metric, int minContrast)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(893);
		JlNativeApi.Store(proc, 1, contours);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
	}

	/// <summary>
	///   CreateScaledShapeModelXld(JlTuple 版) 的标量入参版（原生 id 893，原地换入新句柄）：numLevels、angleStep、scaleStep 必须给具体数值。
	/// </summary>
	/// <param name="contours">用于创建模型的输入轮廓。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleMin">模板的最小尺度。Default: 0.9</param>
	/// <param name="scaleMax">模板的最大尺度。Default: 1.1</param>
	/// <param name="scaleStep">尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>CreateScaledShapeModelXld(JlTuple 版) 的标量入参版，原生 id 同为 893；numLevels、angleStep、scaleStep 必须给具体数值。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlXLDCont contours = src.GetShapeModelContours(1);
	///   using JlShapeModel model = new JlShapeModel();
	///   model.CreateScaledShapeModelXld(contours, 3, -0.39, 0.79, 0.05, 0.9, 1.1, 0.05, "auto", "ignore_local_polarity", 5);
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>FindShapeModel、GetShapeModelContours、ClearShapeModel</para>
	/// </remarks>
	public void CreateScaledShapeModelXld(JlXLDCont contours, int numLevels, double angleStart, double angleExtent, double angleStep, double scaleMin, double scaleMax, double scaleStep, string optimization, string metric, int minContrast)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(893);
		JlNativeApi.Store(proc, 1, contours);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
	}

	/// <summary>
	///   从 XLD 轮廓原地训练无尺度模型（原生 id 894，先 Dispose 再换入）：轮廓不带灰度信息，故本族没有 contrast 参数、minContrast 必给整数、极性未知不可假定，极性确定后再经 SetShapeModelMetric 校准。
	/// </summary>
	/// <param name="contours">用于创建模型的输入轮廓。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>从 XLD 轮廓原地训练无尺度模型（原生 id 894，先 Dispose 再换入）。Xld 训练的意义在于模板不来自某张具体照片：轮廓可来自 CAD 描图、GetShapeModelContours 转存或其他算子提取，模型点即轮廓离散点。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>轮廓没有灰度信息，故本族没有 contrast 参数、minContrast 为必给整数、metric 默认 "ignore_local_polarity"（极性未知时不敢假定）；极性确定后可再用 SetShapeModelMetric 改回 use_polarity。</para>
	///   <para><b>与相邻算子的取舍</b></para>
	///   <para>有一张真实模板图时优先 CreateShapeModel：由对比度筛边比手工轮廓更稳。optimization 通过抽稀轮廓点控制训练规模 [待实测: 具体取值]。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlXLDCont contours = src.GetShapeModelContours(1);
	///   using JlShapeModel model = new JlShapeModel();
	///   model.CreateShapeModelXld(contours, "auto", -0.39, 0.79, "auto", "auto", "ignore_local_polarity", 5);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>训练耗时随角度份数与点数增长 [待实测]；本对象旧模型在此调用开始时已被释放。</para>
	///   <para><b>相关算子</b></para>
	///   <para>FindShapeModel、GetShapeModelContours、ClearShapeModel</para>
	/// </remarks>
	public void CreateShapeModelXld(JlXLDCont contours, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, JlTuple optimization, string metric, int minContrast)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(894);
		JlNativeApi.Store(proc, 1, contours);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
	}

	/// <summary>
	///   以 XLD 轮廓为模板原地训练无尺度形状模型（原生 id 894 标量入参版）：角度弧度制，numLevels/angleStep 须给具体数值；先 Dispose 旧句柄再换入，无返回值。
	/// </summary>
	/// <param name="contours">用于创建模型的输入轮廓。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "ignore_local_polarity"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: 5</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>CreateShapeModelXld(JlTuple 版) 的标量入参版，原生 id 同为 894；numLevels、angleStep 必须给具体数值，详解见该方法。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlShapeModel src = new JlShapeModel("part.shm");
	///   using JlXLDCont contours = src.GetShapeModelContours(1);
	///   using JlShapeModel model = new JlShapeModel();
	///   model.CreateShapeModelXld(contours, 3, -0.39, 0.79, 0.05, "auto", "ignore_local_polarity", 5);
	///   </code>
	///   <para><b>相关算子</b></para>
	///   <para>FindShapeModel、GetShapeModelContours、ClearShapeModel</para>
	/// </remarks>
	public void CreateShapeModelXld(JlXLDCont contours, int numLevels, double angleStart, double angleExtent, double angleStep, string optimization, string metric, int minContrast)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(894);
		JlNativeApi.Store(proc, 1, contours);
		JlNativeApi.StoreI(proc, 0, numLevels);
		JlNativeApi.StoreD(proc, 1, angleStart);
		JlNativeApi.StoreD(proc, 2, angleExtent);
		JlNativeApi.StoreD(proc, 3, angleStep);
		JlNativeApi.StoreS(proc, 4, optimization);
		JlNativeApi.StoreS(proc, 5, metric);
		JlNativeApi.StoreI(proc, 6, minContrast);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(contours);
	}

	/// <summary>
	///   以模板图 domain 内像素原地训练各向异性缩放形状模型（原生 id 895 元组版）：行、列方向独立给像素尺度区间（倍数）与弧度角域，先 Dispose 旧句柄再换入，无返回值。
	/// </summary>
	/// <param name="template">用于创建模型的输入图像（取其 domain 区域）。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleRMin">模板在行方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleRMax">模板在行方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleRStep">行方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleCMin">模板在列方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleCMax">模板在列方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleCStep">列方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "use_polarity"</param>
	/// <param name="contrast">模板图像中目标对比度的阈值或迟滞阈值，以及可选的目标部件最小尺寸。Default: "auto"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: "auto"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>用模板图 domain 内像素训练各向异性缩放模型（原生 id 895，先 Dispose 再原地换入）。行、列方向独立给尺度区间与步长，匹配用 FindAnisoShapeModel(s) 取回 scaleR/scaleC。</para>
	///   <para><b>约束或前提</b></para>
	///   <para>模板须单通道 [待实测]；只取 domain 内像素，用全图当模板会把背景边缘收进模型，先 Threshold+ReduceDomain 圈定目标。</para>
	///   <para><b>参数取向</b></para>
	///   <para>contrast 可为单阈值或迟滞双阈值再加最小连接点数（英文参数说明），给 "auto" 由模板反比度自动选；取值过高模型点太少、匹配脆弱，过低则背景噪声入模、训练与匹配都变慢。minContrast 用 "auto" 时取值策略 [待实测]。尺度份数与耗时乘积关系同 CreateAnisoShapeModelXld；行列尺度总相等时改 CreateScaledShapeModel。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlRegion domain = template.Threshold(0.0, 255.0);
	///   using JlImage roi = template.ReduceDomain(domain);
	///   using JlShapeModel model = new JlShapeModel();
	///   model.CreateAnisoShapeModel(roi, "auto", -0.39, 0.79, "auto", 0.9, 1.1, "auto", 0.9, 1.1, "auto", "auto", "use_polarity", new JlTuple(new double[] { 20.0, 40.0, 5.0 }), 15);
	///   </code>
	///   <para><b>资源与坑</b></para>
	///   <para>迟滞三元组按位置解释 (low, high, min_size) [待实测]；训练失败旧模型已释放。</para>
	///   <para><b>相关算子</b></para>
	///   <para>FindShapeModel、GetShapeModelContours、ClearShapeModel</para>
	/// </remarks>
	public void CreateAnisoShapeModel(JlImage template, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleRMin, double scaleRMax, JlTuple scaleRStep, double scaleCMin, double scaleCMax, JlTuple scaleCStep, JlTuple optimization, string metric, JlTuple contrast, JlTuple minContrast)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(895);
		JlNativeApi.Store(proc, 1, template);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   CreateAnisoShapeModel 元组版的标量入参重载（原生 id 895）：contrast/minContrast 只能给单个整数阈值、无法迟滞或 "auto"；原地训练换柄、无返回值。
	/// </summary>
	/// <param name="template">用于创建模型的输入图像（取其 domain 区域）。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleRMin">模板在行方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleRMax">模板在行方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleRStep">行方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleCMin">模板在列方向的最小尺度。Default: 0.9</param>
	/// <param name="scaleCMax">模板在列方向的最大尺度。Default: 1.1</param>
	/// <param name="scaleCStep">列方向的尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "use_polarity"</param>
	/// <param name="contrast">模板图像中目标对比度的阈值或迟滞阈值，以及可选的目标部件最小尺寸。Default: "auto"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: "auto"</param>
	/// <remarks>
	///   <para><b>功能说明</b></para>
	///   <para>CreateAnisoShapeModel(JlTuple 版) 的标量入参版，原生 id 同为 895；contrast、minContrast 只能给单个整数阈值（无法迟滞/自动），取向见该方法。</para>
	///   <para><b>示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel();
	///   model.CreateAnisoShapeModel(template, 3, -0.39, 0.79, 0.05, 0.9, 1.1, 0.05, 0.9, 1.1, 0.05, "auto", "use_polarity", 30, 15);
	///   </code>
	/// </remarks>
	public void CreateAnisoShapeModel(JlImage template, int numLevels, double angleStart, double angleExtent, double angleStep, double scaleRMin, double scaleRMax, double scaleRStep, double scaleCMin, double scaleCMax, double scaleCStep, string optimization, string metric, int contrast, int minContrast)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(895);
		JlNativeApi.Store(proc, 1, template);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   以模板图 domain 内像素原地训练各向同性缩放形状模型（原生 id 896 元组版）：角度弧度、尺度为倍数区间，多参数可 "auto"（钉固传递）；先 Dispose 再换柄，须配 FindScaledShapeModel 取回尺度。
	/// </summary>
	/// <param name="template">用于创建模型的输入图像（取其 domain 区域）。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleMin">模板的最小尺度。Default: 0.9</param>
	/// <param name="scaleMax">模板的最大尺度。Default: 1.1</param>
	/// <param name="scaleStep">尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "use_polarity"</param>
	/// <param name="contrast">模板图像中目标对比度的阈值或迟滞阈值，以及可选的目标部件最小尺寸。Default: "auto"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: "auto"</param>
	/// <remarks>
	///   <para><b>功能说明</b>：用模板图像在其 domain 内的像素原地训练各向同性缩放模型（原生 id 896，与构造器同 id；实现先 Dispose 旧句柄再换入新句柄，基类 Load 拒绝写已初始化句柄）。角度/尺度/层数训练后即固化，用 GetShapeModelParams 回读 "auto" 展开出的实际值。</para>
	///   <para><b>约束或前提</b>：模型点取自模板图像的梯度边缘，低对比度目标训不出足够边缘点；只取 domain 内像素，全图当模板会把背景边缘收进模型，应先 Threshold+ReduceDomain 圈定目标。模板是否必须单通道 [待实测]。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：numLevels/angleStep/scaleStep/optimization/contrast/minContrast 均可传 "auto"（JlTuple 钉固定后 UnpinTuple）。angle_start 为弧度、angle_extent 是自 start 起的区间宽度；num_levels 越多训练越快但小目标高层无特征 [待实测]；contrast 可给单阈值或迟滞参数组，位置语义 [待实测]；尺度区间 (max-min)/step 的份数直接乘进训练与搜索耗时，无各向异性形变时比 Aniso 版省一半尺度份数。</para>
	///   <para><b>与相邻算子的取舍</b>：不需要尺度时用 CreateShapeModel（模型最小、匹配最快）；行列独立缩放才用 CreateAnisoShapeModel。本方法训练的模型必须配 FindScaledShapeModel/FindScaledShapeModels 才能取回 scale。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel();
	///   model.CreateScaledShapeModel(template, "auto", -0.39, 0.79, "auto", 0.9, 1.1, "auto", "auto", "use_polarity", 30, 15);
	///   </code>
	///   <para><b>资源与坑</b>：训练抛异常时旧模型已被 Dispose、本对象句柄失效，需重建；metric 默认 "use_polarity"（图像训练有灰度极性可用，Xld 族则默认 ignore_local_polarity）。</para>
	///   <para><b>相关算子</b></para>
	///   <para>FindShapeModel、GetShapeModelContours、ClearShapeModel</para>
	/// </remarks>
	public void CreateScaledShapeModel(JlImage template, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, double scaleMin, double scaleMax, JlTuple scaleStep, JlTuple optimization, string metric, JlTuple contrast, JlTuple minContrast)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(896);
		JlNativeApi.Store(proc, 1, template);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   CreateScaledShapeModel 元组版的标量入参重载（原生 id 896）：numLevels/angleStep/scaleStep/contrast/minContrast 只能给具体数值、StoreI/StoreD 直写免钉固；原地换柄训练、无返回值。
	/// </summary>
	/// <param name="template">用于创建模型的输入图像（取其 domain 区域）。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="scaleMin">模板的最小尺度。Default: 0.9</param>
	/// <param name="scaleMax">模板的最大尺度。Default: 1.1</param>
	/// <param name="scaleStep">尺度步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "use_polarity"</param>
	/// <param name="contrast">模板图像中目标对比度的阈值或迟滞阈值，以及可选的目标部件最小尺寸。Default: "auto"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: "auto"</param>
	/// <remarks>
	///   <para><b>功能说明</b>：CreateScaledShapeModel(JlTuple 版) 的标量入参版，原生 id 同为 896；numLevels、angleStep、scaleStep、contrast、minContrast 只能给具体数值（无法传 "auto"、无法给迟滞组合），optimization/metric 仍为字符串。</para>
	///   <para><b>参数取向</b>：各参数经 StoreI/StoreD/StoreS 直写、无钉固与 UnpinTuple，训练/匹配行为与元组版一致；取向详见该方法。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel();
	///   model.CreateScaledShapeModel(template, 3, -0.39, 0.79, 0.05, 0.9, 1.1, 0.05, "auto", "use_polarity", 30, 15);
	///   </code>
	///   <para><b>资源与坑</b>：同样先 Dispose 旧句柄再换入，训练失败本对象句柄失效。</para>
	///   <para><b>相关算子</b></para>
	///   <para>FindShapeModel、GetShapeModelContours、ClearShapeModel</para>
	/// </remarks>
	public void CreateScaledShapeModel(JlImage template, int numLevels, double angleStart, double angleExtent, double angleStep, double scaleMin, double scaleMax, double scaleStep, string optimization, string metric, int contrast, int minContrast)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(896);
		JlNativeApi.Store(proc, 1, template);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   以模板图 domain 内像素原地训练无尺度形状模型（原生 id 897 元组版）：角度弧度制、可 "auto" 的参数走元组钉固；先 Dispose 旧句柄再换入，匹配位姿含旋转但取不回尺度。
	/// </summary>
	/// <param name="template">用于创建模型的输入图像（取其 domain 区域）。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "use_polarity"</param>
	/// <param name="contrast">模板图像中目标对比度的阈值或迟滞阈值，以及可选的目标部件最小尺寸。Default: "auto"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: "auto"</param>
	/// <remarks>
	///   <para><b>功能说明</b>：用模板图像在其 domain 内的像素原地训练无尺度形状模型（原生 id 897，与构造器同 id；先 Dispose 旧句柄再换入）。模型点为模板梯度边缘的离散点，匹配返回位姿含旋转角，取不回尺度。</para>
	///   <para><b>约束或前提</b>：形状匹配基于梯度边缘而非灰度相关——整体光照漂移鲁棒，但低对比度目标边缘点稀少、易失效；全图当模板会把背景边缘收进模型，应先 Threshold+ReduceDomain 圈定目标。模板通道数要求 [待实测]。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：numLevels/angleStep/optimization/contrast/minContrast 均可 "auto"（钉固后 UnpinTuple）。angle_start/angle_extent 弧度制、extent 为自 start 起的宽度；angle_step 越小角度分辨率越高但角度份数增多、训练与搜索时间线性上涨；num_levels 影响金字塔粗筛层数与鲁棒性 [待实测：具体影响方向]；contrast 可给单阈值或迟滞参数组，位置语义 [待实测]；minContrast 用 "auto" 时的取值策略 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：目标在图像中尺寸会变时用 CreateScaledShapeModel/CreateAnisoShapeModel（代价是尺度份数乘进耗时）；轮廓不来自照片时用 CreateShapeModelXld 族。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel();
	///   model.CreateShapeModel(template, "auto", -0.39, 0.79, "auto", "auto", "use_polarity", 30, 15);
	///   </code>
	///   <para><b>资源与坑</b>：训练失败旧模型已释放、句柄失效需重建；训练参数固化后用 GetShapeModelParams 回读实际值，SetShapeModelParam 能否覆盖 [待实测]。</para>
	///   <para><b>相关算子</b></para>
	///   <para>FindShapeModel、GetShapeModelContours、ClearShapeModel</para>
	/// </remarks>
	public void CreateShapeModel(JlImage template, JlTuple numLevels, double angleStart, double angleExtent, JlTuple angleStep, JlTuple optimization, string metric, JlTuple contrast, JlTuple minContrast)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(897);
		JlNativeApi.Store(proc, 1, template);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   CreateShapeModel 元组版的标量入参重载（原生 id 897）：numLevels/angleStep/contrast/minContrast 须给具体数值、直写免钉固，无法迟滞或 "auto"；原地换柄训练、无返回值。
	/// </summary>
	/// <param name="template">用于创建模型的输入图像（取其 domain 区域）。</param>
	/// <param name="numLevels">金字塔层数的最大值。Default: "auto"</param>
	/// <param name="angleStart">模板的最小旋转角。Default: -0.39</param>
	/// <param name="angleExtent">旋转角度的范围。Default: 0.79</param>
	/// <param name="angleStep">角度的步长（分辨率）。Default: "auto"</param>
	/// <param name="optimization">生成模型时使用的优化类型及可选方法。Default: "auto"</param>
	/// <param name="metric">匹配度量方式。Default: "use_polarity"</param>
	/// <param name="contrast">模板图像中目标对比度的阈值或迟滞阈值，以及可选的目标部件最小尺寸。Default: "auto"</param>
	/// <param name="minContrast">搜索图像中目标的最小对比度。Default: "auto"</param>
	/// <remarks>
	///   <para><b>功能说明</b>：CreateShapeModel(JlTuple 版) 的标量入参版，原生 id 同为 897；numLevels、angleStep、contrast、minContrast 只能给具体数值（contrast 无法给迟滞组合、minContrast 无法 "auto"）。</para>
	///   <para><b>参数取向</b>：各参数经 StoreI/StoreD/StoreS 直写、无钉固与 UnpinTuple；训练参数取向与坑详见元组版。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlImage template = new JlImage("label.png");
	///   using JlShapeModel model = new JlShapeModel();
	///   model.CreateShapeModel(template, 3, -0.39, 0.79, 0.05, "auto", "use_polarity", 30, 15);
	///   </code>
	///   <para><b>资源与坑</b>：同样先 Dispose 旧句柄再换入，训练失败本对象句柄失效。</para>
	///   <para><b>相关算子</b></para>
	///   <para>FindShapeModel、GetShapeModelContours、ClearShapeModel</para>
	/// </remarks>
	public void CreateShapeModel(JlImage template, int numLevels, double angleStart, double angleExtent, double angleStep, string optimization, string metric, int contrast, int minContrast)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(897);
		JlNativeApi.Store(proc, 1, template);
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
		err = Load(proc, 0, err);
		JlNativeApi.PostCall(proc, err);
		GC.KeepAlive(this);
		GC.KeepAlive(template);
	}

	/// <summary>
	///   读回模型的 clutter 拒检配置（原生 id 2055 元组版）：返回"禁现杂物"区域的新 JlRegion 句柄（须 Dispose），并 out 出参数值元组、变换矩阵与 INTEGER 装载的最小对比度；本对象不变。
	/// </summary>
	/// <param name="genParamName">参数名。Default: "use_clutter"</param>
	/// <param name="genParamValue">参数值。</param>
	/// <param name="homMat2D">变换矩阵。</param>
	/// <param name="clutterContrast">搜索图像中干扰的最小对比度。</param>
	/// <returns>不应出现干扰的区域。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：查询模型的 clutter（杂乱边缘拒检）参数（原生 id 2055）：返回"不允许出现杂乱"的区域（新 JlRegion 句柄），并按 genParamName 列出的参数名读回取值 genParamValue、变换 homMat2D 与杂乱最小对比度 clutterContrast（INTEGER）。</para>
	///   <para><b>约束或前提</b>：需先用 SetShapeModelClutter 写入配置才有意义，未配置时各输出的取值 [待实测]；除 "use_clutter" 外支持的参数名全集与语义 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：本方法只读；改配置走 SetShapeModelClutter（id 2057）。普通定位不需要 clutter，仅在同料不同工件、边缘区常被无关高对比结构污染时才值得配置。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：genParamName 钉固后 UnpinTuple；输出装载序为区域（图标 1）、值（控制 0）、变换（控制 1）、对比度（控制 2）。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   using JlRegion keepOut = model.GetShapeModelClutter("use_clutter", out JlTuple values, out JlHomMat2D homMat2D, out int clutterContrast);
	///   </code>
	///   <para><b>资源与坑</b>：返回的 JlRegion 是新句柄需 Dispose；JlHomMat2D 与 JlTuple 不实现 IDisposable，无需释放。</para>
	/// </remarks>
	public JlRegion GetShapeModelClutter(JlTuple genParamName, out JlTuple genParamValue, out JlHomMat2D homMat2D, out int clutterContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(2055);
		Store(proc, 0);
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
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   GetShapeModelClutter 元组版的标量读出版（原生 id 2055）：单个参数名 StoreS 直传、genParamValue 经 LoadS 只取第一个字符串值；返回区域新句柄须 Dispose，本对象不变。
	/// </summary>
	/// <param name="genParamName">参数名。Default: "use_clutter"</param>
	/// <param name="genParamValue">参数值。</param>
	/// <param name="homMat2D">变换矩阵。</param>
	/// <param name="clutterContrast">搜索图像中干扰的最小对比度。</param>
	/// <returns>不应出现干扰的区域。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>：GetShapeModelClutter(JlTuple,...) 的标量读出版（原生 id 同为 2055）：单一参数名以 StoreS 直传，genParamValue 经 LoadS 只取第一个字符串值。</para>
	///   <para><b>约束或前提</b>：模型上配置了多个 clutter 参数时，本重载拿不到第一个之外的取值，多参数请改用元组版。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   using JlRegion keepOut = model.GetShapeModelClutter("use_clutter", out string value, out JlHomMat2D homMat2D, out int clutterContrast);
	///   </code>
	///   <para><b>资源与坑</b>：返回的 JlRegion 是新句柄需 Dispose；语义与坑详见元组版。</para>
	/// </remarks>
	public JlRegion GetShapeModelClutter(string genParamName, out string genParamValue, out JlHomMat2D homMat2D, out int clutterContrast)
	{
		IntPtr proc = JlNativeApi.PreCall(2055);
		Store(proc, 0);
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
		GC.KeepAlive(this);
		return obj;
	}

	/// <summary>
	///   为模型写入 clutter 拒检配置（原生 id 2057 元组版）：clutterRegion 以像素坐标圈定禁现杂物的区域、clutterContrast 为最小对比度阈值，genParam 名值按位配对钉固传递；原地修改、无返回值。
	/// </summary>
	/// <param name="clutterRegion">不应出现干扰的区域。</param>
	/// <param name="homMat2D">变换矩阵。</param>
	/// <param name="clutterContrast">搜索图像中干扰的最小对比度。Default: 128</param>
	/// <param name="genParamName">参数名。</param>
	/// <param name="genParamValue">参数值。</param>
	/// <remarks>
	///   <para><b>功能说明</b>：为模型写入 clutter 拒检配置（原生 id 2057，原地修改、无返回值）：clutterRegion 标出"不允许出现杂乱边缘"的区域，clutterContrast 为杂乱判定的最小对比度；匹配时若该区域内出现不低于此对比度的结构，则拒绝该候选位姿 [待实测：判定细节与对 Find* 的作用时机]。</para>
	///   <para><b>约束或前提</b>：homMat2D 与 clutterRegion 的坐标系关系（模型系还是图像系、匹配时如何参与）从包装层判不了 [待实测]；区域用 JlRegion 构造或 Threshold 圈出，随 WriteShapeModel 一并保存与否 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>：读回用 GetShapeModelClutter（id 2055）；一般误匹配先调 min_score/greediness/域裁剪，clutter 是"特定位置总有干扰边"时的定点手段，不要当默认步骤。</para>
	///   <para><b>参数取向</b>：主实现（元组版）：genParamName/genParamValue 按位置配对、钉固后 UnpinTuple；homMat2D 同样经 Store/UnpinTuple 传递；参数名取值集合（示例中 "use_clutter" 的确切语义与合法值）[待实测]。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   using JlRegion clutter = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlHomMat2D pose = new JlHomMat2D();
	///   pose.HomMat2dIdentity();
	///   model.SetShapeModelClutter(clutter, pose, 128, new string[] { "use_clutter" }, new double[] { 1.0 });
	///   </code>
	///   <para><b>资源与坑</b>：改完需再次 WriteShapeModel 才能固化到文件；JlHomMat2D 不实现 IDisposable 无需释放，传入的 clutter 区域调用返回后能否立即 Dispose（原生侧是否留存引用）[待实测]。</para>
	/// </remarks>
	public void SetShapeModelClutter(JlRegion clutterRegion, JlHomMat2D homMat2D, int clutterContrast, JlTuple genParamName, JlTuple genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(2057);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, clutterRegion);
		JlNativeApi.Store(proc, 1, homMat2D);
		JlNativeApi.StoreI(proc, 2, clutterContrast);
		JlNativeApi.Store(proc, 3, genParamName);
		JlNativeApi.Store(proc, 4, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		JlNativeApi.UnpinTuple(genParamName);
		JlNativeApi.UnpinTuple(genParamValue);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(clutterRegion);
	}

	/// <summary>
	///   SetShapeModelClutter 元组版的标量入参重载（原生 id 2057）：只配一个参数名与一个数值取值（StoreS/StoreD 直写免钉固）；原地修改本模型、无返回值，改完需 WriteShapeModel 才固化到文件。
	/// </summary>
	/// <param name="clutterRegion">不应出现干扰的区域。</param>
	/// <param name="homMat2D">变换矩阵。</param>
	/// <param name="clutterContrast">搜索图像中干扰的最小对比度。Default: 128</param>
	/// <param name="genParamName">参数名。</param>
	/// <param name="genParamValue">参数值。</param>
	/// <remarks>
	///   <para><b>功能说明</b>：SetShapeModelClutter(JlTuple,...) 的标量入参版（原生 id 同为 2057）：只配一个参数名与一个数值取值。</para>
	///   <para><b>参数取向</b>：genParamName 经 StoreS、genParamValue 经 StoreD 直写；homMat2D 仍钉固后 UnpinTuple。参数名取值集合与整体语义见元组版。</para>
	///   <para><b>调用示例</b></para>
	///   <code>
	///   using JlShapeModel model = new JlShapeModel("part.shm");
	///   using JlRegion clutter = new JlRegion(10.0, 10.0, 40.0, 40.0);
	///   JlHomMat2D pose = new JlHomMat2D();
	///   pose.HomMat2dIdentity();
	///   model.SetShapeModelClutter(clutter, pose, 128, "use_clutter", 1.0);
	///   </code>
	///   <para><b>资源与坑</b>：改完需再次 WriteShapeModel 固化；多个参数名时本重载只能配一个，改用元组版。</para>
	/// </remarks>
	public void SetShapeModelClutter(JlRegion clutterRegion, JlHomMat2D homMat2D, int clutterContrast, string genParamName, double genParamValue)
	{
		IntPtr proc = JlNativeApi.PreCall(2057);
		Store(proc, 0);
		JlNativeApi.Store(proc, 1, clutterRegion);
		JlNativeApi.Store(proc, 1, homMat2D);
		JlNativeApi.StoreI(proc, 2, clutterContrast);
		JlNativeApi.StoreS(proc, 3, genParamName);
		JlNativeApi.StoreD(proc, 4, genParamValue);
		int procResult = JlNativeApi.CallProcedure(proc);
		JlNativeApi.UnpinTuple(homMat2D);
		JlNativeApi.PostCall(proc, procResult);
		GC.KeepAlive(this);
		GC.KeepAlive(clutterRegion);
	}









}
