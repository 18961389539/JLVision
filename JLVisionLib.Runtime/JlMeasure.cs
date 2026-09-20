using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace JLVisionLib;

/// <summary>卡尺（测量）句柄对象：持有一个原生 measure 句柄，预先定义一条矩形或圆环弧形的扫描带，之后对任意输入图反复沿带提取灰度剖面与边缘。</summary>
/// <remarks>
///   <para><b>功能说明</b>本类只是句柄壳（<c>JlHandle</c> 派生），几何参数存于原生侧。矩形卡尺由 <c>GenMeasureRectangle2</c>（原生算子 id 816）准备，圆环弧卡尺由 <c>GenMeasureArc</c>（id 815）准备；随后的 <c>MeasurePos</c>（813）、<c>MeasurePairs</c>（812）、<c>MeasureThresh</c>（803）、<c>MeasureProjection</c>（805）及 Fuzzy 系列在同一条带上找边缘、灰度过渡点或纯剖面。</para>
///   <para><b>约束或前提</b>坐标一律 row=y（向下为正）、column=x（向右为正），长度单位是像素，角度单位是弧度。<c>width</c>/<c>height</c> 记录的是"后续要处理的图"的尺寸，与实际喂入的图不符时结果位置会整体偏移 [待实测]。所有输出 <c>JlTuple</c> 都是新句柄，用完注意释放。</para>
///   <para><b>与相邻算子的取舍</b>只想看灰度趋势不做边缘拟合用 <c>MeasureProjection</c>；要带隶属度评分的边缘对用 <c>FuzzyMeasurePos</c>/<c>FuzzyMeasurePairs</c>；跨卡尺批量找成对边界的窄条特征用 <c>MeasurePairs</c>。</para>
///   <para><b>资源与坑</b>类标了 <c>[Serializable]</c>，二进制序列化走 <c>SerializeMeasure</c>（id 799）/反序列化走 <c>DeserializeMeasure</c>（id 800）。每个实例持有原生引用，用毕 <c>Dispose()</c>。</para>
/// </remarks>
[Serializable]
public class JlMeasure : JlHandle, ISerializable, ICloneable
{
	/// <summary>构造句柄为 UNDEF 的空壳卡尺对象：纯托管建壳、不发任何原生调用，留作 Read/Create/Deserialize 族原地装入新句柄的接收位。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMeasure()
		: base(JlHandleBase.UNDEF)
	{
	}

	/// <summary>包已有原生卡尺句柄→引用计数拷贝（CopyObject），非深拷贝；断言对象类 measure。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMeasure(IntPtr handle)
		: base(handle)
	{
		AssertSemType();
	}

	/// <summary>包装已有原生 JlHandle，并校验其语义类型为卡尺（measure）。</summary>
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
	///   构造一个圆环弧卡尺（原生算子 id 815）：以 (centerRow, centerCol) 为圆心、radius 为半径的一段弧上，沿径向逐列提取垂直于弧的直线边缘；结果句柄写入本新建实例。
	/// </summary>
	/// <param name="centerRow">弧的圆心行坐标，像素。Default: 100.0</param>
	/// <param name="centerCol">弧的圆心列坐标，像素。Default: 100.0</param>
	/// <param name="radius">弧半径，像素（从圆心到扫描带中心线）。Default: 50.0</param>
	/// <param name="angleStart">弧起始角，弧度。Default: 0.0</param>
	/// <param name="angleExtent">弧张角，弧度，默认 6.28318 即整圆。Default: 6.28318</param>
	/// <param name="annulusRadius">环形扫描带的半宽，像素；灰度沿径向该宽度内平均。Default: 10.0</param>
	/// <param name="width">后续要处理的图像的宽度，像素。Default: 512</param>
	/// <param name="height">后续要处理的图像的高度，像素。Default: 512</param>
	/// <param name="interpolation">重采样插值类型。Default: "nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>JlMeasure(double,double,double,double,double,double,int,int,string)</c> 标量构造器走同一个原生 id 815，区别只在装载方式：元组版先 <c>Store</c> 钉住各 <c>JlTuple</c>，调用后逐个 <c>UnpinTuple</c>；标量版 <c>StoreD</c> 直写单元素、无钉固定开销。</para>
	///   <para><b>约束或前提</b>几何参数在原生侧记为多值元组时能否一次生成多条弧卡尺，本包装层无法判定 [待实测]；输出经 <c>Load</c> 装进本实例（构造时句柄为 UNDEF，满足其"必须先空壳"的前提）。</para>
	///   <para><b>与相邻构造器的取舍</b>被测边近似直线（宽度、间距类尺寸）用矩形卡尺 <c>GenMeasureRectangle2</c>；边沿圆周分布（外径、圆环壁厚）才用本构造器，半径档位由 <c>radius</c>±<c>annulusRadius</c> 决定。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple centerRow = 100.0;                              // double→JlTuple 隐式转换
	///   JlTuple centerCol = 100.0;
	///   JlTuple radius = 50.0;
	///   JlTuple angleStart = 0.0;
	///   JlTuple angleExtent = 6.28318;
	///   JlTuple annulusRadius = 10.0;
	///   JlMeasure m = new JlMeasure(centerRow, centerCol, radius, angleStart, angleExtent, annulusRadius, 512, 512, "nearest_neighbor");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>本实例持有原生引用，用毕 <c>Dispose()</c>；传入的 <c>JlTuple</c> 在调用内部被钉住又解钉，调用返回后即可安全 <c>Dispose</c> 它们。</para>
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
	///   构造一个圆环弧卡尺（原生算子 id 815，标量版）：单值直写，扫描带为沿径向宽 2×annulusRadius 的一段弧，在其中提取垂直于弧的直线边缘；结果句柄写入本新建实例。
	/// </summary>
	/// <param name="centerRow">弧的圆心行坐标，像素。Default: 100.0</param>
	/// <param name="centerCol">弧的圆心列坐标，像素。Default: 100.0</param>
	/// <param name="radius">弧半径，像素（扫描带中心线所在半径）。Default: 50.0</param>
	/// <param name="angleStart">弧起始角，弧度。Default: 0.0</param>
	/// <param name="angleExtent">弧张角，弧度，默认 6.28318 即整圆。Default: 6.28318</param>
	/// <param name="annulusRadius">环形扫描带的半宽，像素；灰度沿径向该宽度内平均。Default: 10.0</param>
	/// <param name="width">后续要处理的图像的宽度，像素。Default: 512</param>
	/// <param name="height">后续要处理的图像的高度，像素。Default: 512</param>
	/// <param name="interpolation">重采样插值类型。Default: "nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一个原生 id 815；本走位用 <c>StoreD</c>/<c>StoreI</c>/<c>StoreS</c> 直写单元素参数，不钉元组也不 <c>UnpinTuple</c>，单卡尺场景优先用它。角度在 row-down 坐标系中的正方向代码未注明 [待实测]。</para>
	///   <para><b>约束或前提</b><c>radius</c> 必须落在 <c>width</c>/<c>height</c> 定义的画幅内才有意义；扫描带越出图像边缘时该方向的剖面缺值 [待实测]。</para>
	///   <para><b>与相邻构造器的取舍</b>只要一条弧的径向边缘序列用本构造器；需要多圆心/多半径一次批量定义时改元组重载 [待实测：原生是否支持多值展开]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure m = new JlMeasure(100.0, 100.0, 50.0, 0.0, 6.28318, 10.0, 512, 512, "nearest_neighbor");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>构造即建原生对象，弃用前 <c>Dispose()</c>；构造内部末尾 <c>GC.KeepAlive(this)</c>，句柄在原生调用结束前不会被终结器回收。</para>
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
	///   构造一个矩形卡尺（原生算子 id 816）：以 (row, column) 为中心、长轴方向为 phi 的矩形带内，沿长轴方向逐列做垂直于长轴的灰度平均，再在其中提取直线边缘；结果句柄写入本新建实例。
	/// </summary>
	/// <param name="row">矩形中心的行坐标，像素。Default: 300.0</param>
	/// <param name="column">矩形中心的列坐标，像素。Default: 200.0</param>
	/// <param name="phi">矩形长轴与水平方向的夹角，弧度。Default: 0.0</param>
	/// <param name="length1">矩形长轴方向的半长（扫描方向），像素。Default: 100.0</param>
	/// <param name="length2">矩形短轴方向的半长（灰度平均方向），像素。Default: 20.0</param>
	/// <param name="width">后续要处理的图像的宽度，像素。Default: 512</param>
	/// <param name="height">后续要处理的图像的高度，像素。Default: 512</param>
	/// <param name="interpolation">重采样插值类型。Default: "nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b>与标量重载同一个原生 id 816；元组版 <c>Store</c> 钉固定元组、调用后逐个 <c>UnpinTuple</c>。要检测的边垂直于长轴（phi 方向为扫描方向），把被测边摆成与短轴平行即可最大化剖面梯度。</para>
	///   <para><b>约束或前提</b>多值元组是否一次展开成多条卡尺带由原生决定，包装层看不到 [待实测]。矩形带越出图像边界时对应列的灰度平均不完整 [待实测]。</para>
	///   <para><b>与相邻构造器的取舍</b>与 <c>GenMeasureRectangle2</c>（同样 id 816）的区别只在"新建对象"还是"先 Dispose 旧句柄再原地重建本壳"；重复调用本构造器会各自新建实例，不会释放旧实例。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple row = 300.0;                                    // double→JlTuple 隐式转换
	///   JlTuple column = 200.0;
	///   JlTuple phi = 0.0;
	///   JlTuple length1 = 100.0;
	///   JlTuple length2 = 20.0;
	///   JlMeasure m = new JlMeasure(row, column, phi, length1, length2, 512, 512, "nearest_neighbor");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>实例持有原生引用，用毕 <c>Dispose()</c>；传入的元组调用返回后可安全释放。</para>
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
	///   构造一个矩形卡尺（原生算子 id 816，标量版）：单值直写，在长轴为 phi、半长 length1（扫描方向）、半高 length2（平均方向）的矩形带内提取垂直于长轴的直线边缘；结果句柄写入本新建实例。
	/// </summary>
	/// <param name="row">矩形中心的行坐标，像素。Default: 300.0</param>
	/// <param name="column">矩形中心的列坐标，像素。Default: 200.0</param>
	/// <param name="phi">矩形长轴与水平方向的夹角，弧度。Default: 0.0</param>
	/// <param name="length1">矩形长轴方向的半长（扫描方向），像素。Default: 100.0</param>
	/// <param name="length2">矩形短轴方向的半长（灰度平均方向），像素。Default: 20.0</param>
	/// <param name="width">后续要处理的图像的宽度，像素。Default: 512</param>
	/// <param name="height">后续要处理的图像的高度，像素。Default: 512</param>
	/// <param name="interpolation">重采样插值类型。Default: "nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b>与元组重载同一个原生 id 816；本走位 <c>StoreD</c>/<c>StoreI</c>/<c>StoreS</c> 直写，不钉元组。单条矩形卡尺优先用它。phi 在 row-down 坐标系中的旋转正方向代码未注明 [待实测]。</para>
	///   <para><b>约束或前提</b><c>length1</c>、<c>length2</c> 为半长且必须为正；扫描分辨率随 2×length1 与图像宽度联动，带越界时平均灰度不完整 [待实测]。</para>
	///   <para><b>与相邻构造器的取舍</b>每次调用都新建一个原生 measure 对象——热路径里反复改位姿应改用实例方法 <see cref="GenMeasureRectangle2(double,double,double,double,double,int,int,string)"/>（先释放旧句柄再重建，C# 引用不变），否则旧实例要自己负责 <c>Dispose</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>实例持有原生引用，用毕 <c>Dispose()</c>；末尾 <c>GC.KeepAlive(this)</c> 保证句柄活到原生调用结束。</para>
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

	/// <summary>从序列化数据重建卡尺实例（ISerializable 反序列化构造）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlMeasure(SerializationInfo info, StreamingContext context)
	{
		DeserializeMeasure((byte[])info.GetValue("data", typeof(byte[])));
	}

	/// <summary>把本卡尺对象的完整几何状态以 Vision 二进制格式写入流。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>实现只有两步：调 <see cref="SerializeMeasure"/>（原生 id 799）拿 byte[]，再经 <c>JlSerializationBuffer.WriteToStream</c> 写入流。<c>new</c> 关键字只是隐藏基类 <c>JlHandle.Serialize</c> 而非重写：经基类引用调用时执行的仍是基类版本。</para>
	///   <para><b>约束或前提</b>流必须可写；本方法不负责关闭流。<c>using System.IO</c> 已在本文件引入，调用方自行保证。</para>
	///   <para><b>与相邻方法的取舍</b>落盘用 <see cref="WriteMeasure(string)"/>（原生自己写文件），内存/网络传输用本方法或 <c>SerializeMeasure</c> 裸 byte[]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   using FileStream fs = new FileStream("measure.bin", FileMode.Create, FileAccess.Write);
	///   m.Serialize(fs);
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>纯导出操作，不改动本句柄，也不 Dispose 自己。</para>
	/// </remarks>
	public new void Serialize(Stream stream)
	{
		JlSerializationBuffer.WriteToStream(SerializeMeasure(), stream);
	}

	/// <summary>从 Vision 二进制流读回一个卡尺对象，返回新句柄的新实例。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>先 <c>new JlMeasure()</c>（UNDEF 空壳），再把流内容喂给实例方法 <see cref="DeserializeMeasure(byte[])"/>（原生 id 800）装载句柄。流里存的几何参数整体覆盖式生效，与原实例无关（这里根本没有原实例）。</para>
	///   <para><b>约束或前提</b>流必须可读且内容为 <see cref="Serialize(Stream)"/> 或 <c>SerializeMeasure</c> 产出的 Vision 格式；格式不符时错误由原生侧报出。</para>
	///   <para><b>与相邻方法的取舍</b>已有实例想换内容用实例方法 <c>DeserializeMeasure</c>/<c>ReadMeasure</c>（原地重建）；本静态版每次给新对象。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   using FileStream fs = new FileStream("measure.bin", FileMode.Open, FileAccess.Read);
	///   JlMeasure m = JlMeasure.Deserialize(fs);
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是新原生句柄，弃用前 <c>Dispose()</c>；本方法不关闭传入的流。</para>
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

	/// <summary>深拷贝本卡尺对象：经序列化再反序列化产出一个持有独立原生句柄的新实例。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>实现是 <see cref="SerializeMeasure"/>（id 799）拿 byte[]、<c>new JlMeasure()</c> 空壳再 <see cref="DeserializeMeasure(byte[])"/>（id 800）装载——拷贝经由二进制往返，几何参数与原对象完全一致但互不影响。</para>
	///   <para><b>与相邻成员的取舍</b>与 <c>JlHandle(JlHandle)</c> 那类别名式浅拷贝不同，本方法产生新原生对象，改克隆体的 <c>TranslateMeasure</c>/<c>GenMeasure*</c> 不会波及原对象；只要浅别名反而更省。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   JlMeasure copy = m.Clone();
	///   copy.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的新实例持有自己的原生引用，必须单独 <c>Dispose()</c>；<c>ICloneable.Clone</c> 显式实现即转调本方法。</para>
	/// </remarks>
	public new JlMeasure Clone()
	{
		byte[] data = SerializeMeasure();
		JlMeasure obj = new JlMeasure();
		obj.DeserializeMeasure(data);
		return obj;
	}

	/// <summary>
	///   把本卡尺对象序列化成 Vision 二进制 byte[]（原生算子 id 799）；返回的是纯托管字节，不是句柄。
	/// </summary>
	/// <returns>序列化后的字节数组；由 <c>JlSerializationBuffer.LoadBytes</c> 直接从输出通道取回。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>本句柄经 <c>Store(proc, 0)</c> 装为第 0 个图像型入参，原生侧输出一个序列化项，包装层立刻把它转成 byte[]——英文文档所说的"Handle of the serialized item"在 C# 侧不可见，句柄生命周期被封在 <c>JlSerializationBuffer</c> 里。</para>
	///   <para><b>与相邻方法的取舍</b>写文件用 <see cref="WriteMeasure(string)"/>（id 801，原生自己落盘）；写流用 <see cref="Serialize(Stream)"/>（内部就是本方法+写流）；只有要自行托管字节（缓存、走自定义协议）才直接用它。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   byte[] data = m.SerializeMeasure();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>只读导出，不改变本句柄；末尾 <c>GC.KeepAlive(this)</c> 保证原生调用结束前句柄不被回收。</para>
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
	///   用 byte[] 里的内容重建本卡尺对象（原生算子 id 800）：先释放旧句柄，再装入新句柄——C# 引用不变，原生对象是新的。
	/// </summary>
	/// <param name="serializedItemHandle">由 <see cref="SerializeMeasure"/> 产出的 Vision 二进制字节。</param>
	/// <remarks>
	///   <para><b>功能说明</b>方法体第一步 <c>Dispose()</c> 把自身句柄复位为 UNDEF——这是硬前提，基类 <c>Load</c> 遇到非 UNDEF 的壳会直接抛 <c>JlException</c>。字节由 <c>JlSerializationBuffer</c>（using 声明）临时包装成原生序列化项传入。</para>
	///   <para><b>约束或前提</b>数据损坏或版本不符时装载失败，此时本对象已是空壳（旧句柄先被释放了），后续测量调用不可用 [待实测：失败时异常的确切形态]。</para>
	///   <para><b>与相邻方法的取舍</b>要新对象用 <c>JlMeasure.Deserialize(Stream)</c> 或 <c>Clone</c>；要换当前实例的内容才用本方法；从文件读用 <see cref="ReadMeasure(string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure src = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   byte[] data = src.SerializeMeasure();
	///   JlMeasure dst = new JlMeasure(100.0, 100.0, 50.0, 0.0, 6.28318, 10.0, 512, 512, "nearest_neighbor");
	///   dst.DeserializeMeasure(data);                        // dst 从圆弧卡尺变为 src 的矩形卡尺
	///   dst.Dispose();
	///   src.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>成功后持有的新句柄仍归本实例管，仍需 <c>Dispose()</c>；<c>[Serializable]</c> 的反序列化构造器走的也是本方法。</para>
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
	///   把本卡尺对象写到文件（原生算子 id 801）：文件名以 STRING 控制参数传入，落盘由原生侧完成。
	/// </summary>
	/// <param name="fileName">目标文件路径；编码为 ANSI（<c>StoreS</c> 的字符串通道），路径含非 ASCII 字符时能否写对 [待实测]。</param>
	/// <remarks>
	///   <para><b>功能说明</b>本句柄经 <c>Store(proc, 0)</c> 装为第 0 个图像型入参后调用原生 <c>write_measure</c>；无 InitOCT/Load——不产生输出，本对象原样保留。</para>
	///   <para><b>与相邻方法的取舍</b>想托管流或用自定义目录策略时用 <see cref="Serialize(Stream)"/>；本方法一步落盘且配对 <see cref="ReadMeasure(string)"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.WriteMeasure("caliper.dat");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>目录不存在或磁盘不可写时错误由原生侧报出；调用内部 <c>GC.KeepAlive(this)</c>，返回前句柄安全。</para>
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
	///   从文件读回卡尺内容并重建本对象（原生算子 id 802）：旧句柄先被释放，成功后本引用指向文件里的新卡尺。
	/// </summary>
	/// <param name="fileName">由 <see cref="WriteMeasure(string)"/> 写出的文件路径；编码为 ANSI（<c>StoreS</c> 通道）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>方法体第一步 <c>Dispose()</c>——基类 <c>Load</c> 要求自身为 UNDEF 才装载，否则抛 <c>JlException</c>。文件路径是第 0 个控制入参，新句柄从输出通道 0 装入本壳。</para>
	///   <para><b>约束或前提</b>读失败（文件缺失/格式错）时对象已退化为空壳，卡尺配置回不来了——重要配置先 <c>SerializeMeasure</c> 留 byte[] 兜底。</para>
	///   <para><b>与相邻方法的取舍</b>要新实例而非改当前实例，用 <c>JlMeasure.Deserialize(Stream)</c> 读文件流；本方法适合"实例长期存活、内容随配方文件热换"的场景。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.WriteMeasure("caliper.dat");
	///   m.ReadMeasure("caliper.dat");                        // 原地重载，m 引用不变
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>装载后的新句柄仍归本实例管，最终仍需 <c>Dispose()</c>。</para>
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
	///   沿卡尺带提取灰度剖面后，返回灰度穿越指定阈值的所有点的亚像素位置（原生算子 id 803）。
	/// </summary>
	/// <param name="image">输入图像。</param>
	/// <param name="sigma">沿卡尺方向对灰度剖面做高斯平滑的标准差；越大交点位置越稳但越钝化细节。Default: 1.0</param>
	/// <param name="threshold">被追踪的灰度绝对值，像素灰度单位。Default: 128.0</param>
	/// <param name="select">点的取舍方式。Default: "all"</param>
	/// <param name="rowThresh">穿越点的行坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="columnThresh">穿越点的列坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="distance">相邻穿越点的间距（像素），长度比前两路少 1；点数不足 2 时内容如何 [待实测]。</param>
	/// <remarks>
	///   <para><b>功能说明</b>这是"给定灰度电平找交点"，不做边缘拟合：结果位置只反映剖面与水平线的相交，不反映边缘强弱。图像型与控制型入参各走各的索引通道（本句柄=图像型 0、image=图像型 1；sigma/阈值/select=控制型 1/2/3），两个 index 同为 1 不是覆盖写。</para>
	///   <para><b>约束或前提</b>剖面先按 <c>sigma</c> 平滑再找交点，故 <c>sigma</c> 直接决定亚像素位置的平滑度；对照度不均的图，单一绝对阈值可能在某些段产生假交点 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要的是边缘（梯度峰值）用 <see cref="MeasurePos"/>；要成对边界用 <see cref="MeasurePairs"/>；只有"固定灰度电平在哪穿过"这类需求（如标定灰度台阶）才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("part.png");
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.MeasureThresh(img, 1.0, 128.0, "all", out JlTuple rowThresh, out JlTuple colThresh, out JlTuple dist);
	///   rowThresh.Dispose();
	///   colThresh.Dispose();
	///   dist.Dispose();
	///   m.Dispose();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>三路 out 均为 DOUBLE 型新 <c>JlTuple</c>，用毕各自 <c>Dispose()</c>；原生调用结束前本句柄与 image 均被 <c>GC.KeepAlive</c> 钉住，返回后可安全释放入参。</para>
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
	///   调用原生 delete/close 通道销毁卡尺（原生算子 id 804）。不推荐日常使用——释放本对象请用 <c>Dispose()</c>。
	/// </summary>
	/// <remarks>
	///   <para><b>功能说明</b>只把本句柄装进第 0 个图像型入参并调用原生算子；包装层没有跟着复位 <c>mHandle</c>，调用后 C# 壳仍持有那个已被原生处理过的句柄值。</para>
	///   <para><b>约束或前提</b>之后再拿本实例调 <c>MeasurePos</c> 等就是在用一个已销毁的对象；随后再 <c>Dispose()</c> 是否会二次释放（原生 ClearHandle 是减引用还是真销毁）代码看不出来 [待实测]。这正是推荐用 <c>Dispose()</c> 替代本方法的原因：<c>Dispose</c> 会同步把壳复位成 UNDEF。</para>
	///   <para><b>与相邻方法的取舍</b>只想放弃本实例的一切：直接 <c>Dispose()</c>；本方法仅在需要与原生侧资源统计/脚本互操作的场合有意义。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.CloseMeasure();                                      // 之后 m 不可再用，也不再 Dispose
	///   </code>
	///   <para><b>资源与坑</b>调用后请丢弃引用；若仍想走 using/Dispose 模式，就不要碰本方法。</para>
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
	///   沿卡尺带（矩形长轴或弧的径向）提取垂直于边缘方向的平均灰度剖面（原生算子 id 805），不做任何边缘拟合。
	/// </summary>
	/// <param name="image">输入图像。</param>
	/// <returns>剖面的灰度序列，DOUBLE 型 <c>JlTuple</c> 新句柄；元素个数与卡尺带扫描方向的采样数有关，具体对应关系代码未给出 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>这是所有 Measure 族算子的第一步：把二维条带压成一维剖面。本方法只把剖面原样交出来，供自行分析（画曲线、找任意特征点）。</para>
	///   <para><b>与相邻算子的取舍</b>要亚像素边缘坐标用 <see cref="MeasurePos"/>，要灰度交点用 <see cref="MeasureThresh"/>；只有需要自定义判据（如峰值面积、多电平交叉）时才取裸剖面自己算。图像型入参通道里本句柄=0、image=1，与 sigma 等控制参数互不占位。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("part.png");
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   JlTuple profile = m.MeasureProjection(img);
	///   profile.Dispose();
	///   m.Dispose();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组是 <c>JlTuple.LoadNew</c> 造的新句柄（纯数值元组的 <c>Dispose</c> 只释放句柄类元素，不调也不算漏），但习惯上统一释放。</para>
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
	///   把本卡尺上用于 Fuzzy 系列算子的模糊隶属度函数复位为默认定义（原生算子 id 806）。
	/// </summary>
	/// <param name="setType">要复位的模糊集合名。Default: "contrast"</param>
	/// <remarks>
	///   <para><b>功能说明</b>本句柄=图像型入参 0，<c>setType</c> 是控制参数 1；无输出通道，纯状态操作。Fuzzy 算子（<c>FuzzyMeasurePos</c>/<c>FuzzyMeasurePairs</c>/<c>FuzzyMeasurePairing</c>）给出的 fuzzyScore 依赖这套隶属度函数；曾被 <c>add_fuzzy_measure</c> 一类原生接口改过后，可用本方法退回出厂值。</para>
	///   <para><b>约束或前提</b>配对的"添加/修改隶属度函数"算子（原生 add_fuzzy_measure）在本库不存在（已 Grep 核实），只有从外部途径改过隶属度函数时本方法才有用武之地 [待实测：除 "contrast" 外的合法集合名]。</para>
	///   <para><b>与相邻方法的取舍</b>不改隶属度函数、只调打分门限的话，动 <c>fuzzyThresh</c> 参数即可，不必用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.ResetFuzzyMeasure("contrast");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>不影响句柄有效性，前后都可正常测量。</para>
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
	///   在卡尺带内提取带模糊评分的直线边缘对，可按灰度过渡方向与配对约束筛选并截断数量（原生算子 id 809）。
	/// </summary>
	/// <param name="image">输入图像。</param>
	/// <param name="sigma">剖面高斯平滑（求一阶导算梯度前）的标准差。Default: 1.0</param>
	/// <param name="ampThresh">计入边缘的最小幅值（绝对值意义下）。Default: 30.0</param>
	/// <param name="fuzzyThresh">边缘对模糊评分的录取门限，取值域与隶属度函数定义有关 [待实测]。Default: 0.5</param>
	/// <param name="transition">按第一路边沿的灰度过渡方向筛选边缘对（亮暗/暗亮次序）。Default: "all"</param>
	/// <param name="pairing">两边缘如何配成一对的约束。Default: "no_restriction"</param>
	/// <param name="numPairs">最多返回的边缘对个数（INTEGER 装载）。Default: 10</param>
	/// <param name="rowEdgeFirst">第一路边的行坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="columnEdgeFirst">第一路边的列坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="amplitudeFirst">第一路边幅值，带符号（正负号即过渡方向）。DOUBLE 元组，新句柄。</param>
	/// <param name="rowEdgeSecond">第二路边的行坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="columnEdgeSecond">第二路边的列坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="amplitudeSecond">第二路边幅值，带符号。DOUBLE 元组，新句柄。</param>
	/// <param name="rowPairCenter">边缘对中点的行坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="columnPairCenter">边缘对中点的列坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="fuzzyScore">边缘对的模糊评分（隶属度合成，见 ResetFuzzyMeasure）。DOUBLE 元组，新句柄。</param>
	/// <param name="intraDistance">对内两边缘的间距（像素，沿扫描方向）。DOUBLE 元组，新句柄。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>FuzzyMeasurePairs</c>（id 810）相比多了 pairing/numPairs 两路控制参数（索引 5/6），输出 10 路给出对中心却不给 interDistance；适合"已知目标宽度、按约束配边"的筛查。</para>
	///   <para><b>约束或前提</b>十路输出的元素个数一致（按边缘对对齐），哪一路为第 k 对取决于原生排序；排序稳定性代码无法保证 [待实测]。幅值正负约定（由暗到亮为正还是为负）以实测为准 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>不关心隶属度评分要轻量结果时用 <see cref="MeasurePairs"/>；要相邻对间距序列（缝宽检查）用 <c>FuzzyMeasurePairs</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("part.png");
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.FuzzyMeasurePairing(img, 1.0, 30.0, 0.5, "all", "no_restriction", 10,
	///       out JlTuple row1, out JlTuple col1, out JlTuple amp1,
	///       out JlTuple row2, out JlTuple col2, out JlTuple amp2,
	///       out JlTuple rowC, out JlTuple colC, out JlTuple score, out JlTuple intra);
	///   row1.Dispose(); col1.Dispose(); amp1.Dispose();
	///   row2.Dispose(); col2.Dispose(); amp2.Dispose();
	///   rowC.Dispose(); colC.Dispose(); score.Dispose(); intra.Dispose();
	///   m.Dispose();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>十路 out 全是 DOUBLE 型新 <c>JlTuple</c>；原生调用期间本句柄与 image 被 <c>GC.KeepAlive</c> 钉住，返回后即可释放。</para>
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
	///   在卡尺带内提取带模糊评分的直线边缘对，输出除两边界与对中心外还给相邻对间距序列（原生算子 id 810）；无 pairing/numPairs 约束。</summary>
	/// <param name="image">输入图像。</param>
	/// <param name="sigma">剖面高斯平滑（求一阶导算梯度前）的标准差。Default: 1.0</param>
	/// <param name="ampThresh">计入边缘的最小幅值。Default: 30.0</param>
	/// <param name="fuzzyThresh">边缘对模糊评分的录取门限。Default: 0.5</param>
	/// <param name="transition">按第一路边沿的灰度过渡方向筛选边缘对。Default: "all"</param>
	/// <param name="rowEdgeFirst">第一路边行坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="columnEdgeFirst">第一路边列坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="amplitudeFirst">第一路边幅值，带符号。DOUBLE 元组，新句柄。</param>
	/// <param name="rowEdgeSecond">第二路边行坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="columnEdgeSecond">第二路边列坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="amplitudeSecond">第二路边幅值，带符号。DOUBLE 元组，新句柄。</param>
	/// <param name="rowEdgeCenter">边缘对中点行坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="columnEdgeCenter">边缘对中点列坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="fuzzyScore">边缘对的模糊评分。DOUBLE 元组，新句柄。</param>
	/// <param name="intraDistance">对内两边缘间距（像素）。DOUBLE 元组，新句柄。</param>
	/// <param name="interDistance">相邻边缘对之间的间距（像素），长度与对数的关系 [待实测]。DOUBLE 元组，新句柄。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 <c>FuzzyMeasurePairing</c>（id 809）互补：本方法没有配对约束与数量截断参数，但多给一路 interDistance；11 路输出全按 DOUBLE 装载（索引 0~10）。</para>
	///   <para><b>约束或前提</b>过渡方向（transition）指第一路边的明暗次序；两路幅值符号与过渡方向的对应代码未注明 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>需要按宽度约束强制配对或限定对数用 <c>FuzzyMeasurePairing</c>；不需要隶属度评分用 <see cref="MeasurePairs"/>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("part.png");
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.FuzzyMeasurePairs(img, 1.0, 30.0, 0.5, "all",
	///       out JlTuple row1, out JlTuple col1, out JlTuple amp1,
	///       out JlTuple row2, out JlTuple col2, out JlTuple amp2,
	///       out JlTuple rowC, out JlTuple colC, out JlTuple score,
	///       out JlTuple intra, out JlTuple inter);
	///   row1.Dispose(); col1.Dispose(); amp1.Dispose();
	///   row2.Dispose(); col2.Dispose(); amp2.Dispose();
	///   rowC.Dispose(); colC.Dispose(); score.Dispose();
	///   intra.Dispose(); inter.Dispose();
	///   m.Dispose();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>11 路 out 均为新 <c>JlTuple</c> 句柄，用毕释放；本句柄与 image 在调用结束前由 <c>GC.KeepAlive</c> 保活。</para>
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
	///   在卡尺带内提取带模糊评分的直线边缘（原生算子 id 811）：与 <c>MeasurePos</c> 同族，但按幅值与隶属度双门限录取。</summary>
	/// <param name="image">输入图像。</param>
	/// <param name="sigma">剖面高斯平滑（求一阶导算梯度前）的标准差。Default: 1.0</param>
	/// <param name="ampThresh">计入边缘的最小幅值。Default: 30.0</param>
	/// <param name="fuzzyThresh">隶属度录取门限。Default: 0.5</param>
	/// <param name="transition">按明暗过渡方向筛选边缘。Default: "all"</param>
	/// <param name="rowEdge">边缘点行坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="columnEdge">边缘点列坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="amplitude">边缘幅值，带符号。DOUBLE 元组，新句柄。</param>
	/// <param name="fuzzyScore">每条边的模糊评分。DOUBLE 元组，新句柄。</param>
	/// <param name="distance">相邻边缘间距（像素）。DOUBLE 元组，新句柄。</param>
	/// <remarks>
	///   <para><b>功能说明</b>控制参数索引：sigma=1、ampThresh=2、fuzzyThresh=3、transition=4；输出 5 路（索引 0~4）全 DOUBLE。与 <see cref="MeasurePos"/> 的关键差异是多一路 fuzzyScore，且用隶属度函数（见 <see cref="ResetFuzzyMeasure"/>）而非纯幅值筛选——边缘"形似而神不似"（如渐晕下的宽缓边）会被压低分。</para>
	///   <para><b>约束或前提</b>fuzzyScore 的取值域取决于隶属度函数定义 [待实测]；先 Reset 再比较不同工位分数才有意义。</para>
	///   <para><b>与相邻算子的取舍</b>只要位置不要评分用 <c>MeasurePos</c>；要成对边界用 <c>FuzzyMeasurePairs</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("part.png");
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.FuzzyMeasurePos(img, 1.0, 30.0, 0.5, "all",
	///       out JlTuple rowEdge, out JlTuple colEdge, out JlTuple amp, out JlTuple score, out JlTuple dist);
	///   rowEdge.Dispose(); colEdge.Dispose(); amp.Dispose(); score.Dispose(); dist.Dispose();
	///   m.Dispose();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>5 路 out 均为新 <c>JlTuple</c> 句柄；image 与本句柄在原生调用期间被保活。</para>
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
	///   在卡尺带内提取直线边缘并把它们两两配成边缘对，返回两侧亚像素位置、带符号幅值与组内/组间距离（原生算子 id 812）；非 fuzzy 版，无隶属度评分。</summary>
	/// <param name="image">输入图像。</param>
	/// <param name="sigma">剖面高斯平滑（求一阶导算梯度前）的标准差。Default: 1.0</param>
	/// <param name="threshold">计入边缘的最小幅值。Default: 30.0</param>
	/// <param name="transition">决定哪些明暗过渡序对允许配成边缘对。Default: "all"</param>
	/// <param name="select">边缘对的取舍方式。Default: "all"</param>
	/// <param name="rowEdgeFirst">第一边中点行坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="columnEdgeFirst">第一边中点列坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="amplitudeFirst">第一边幅值，带符号。DOUBLE 元组，新句柄。</param>
	/// <param name="rowEdgeSecond">第二边中点行坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="columnEdgeSecond">第二边中点列坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="amplitudeSecond">第二边幅值，带符号。DOUBLE 元组，新句柄。</param>
	/// <param name="intraDistance">组内两边缘间距（像素）。DOUBLE 元组，新句柄。</param>
	/// <param name="interDistance">相邻边缘对间距（像素）。DOUBLE 元组，新句柄。</param>
	/// <remarks>
	///   <para><b>功能说明</b>控制参数索引 sigma=1、threshold=2、transition=3、select=4；8 路输出索引 0~7 全 DOUBLE。配对的成败由 transition 决定（如 "all" 不限、"positive" 只收某一种明暗序对 [待实测]），"找到几对"直接受其影响。</para>
	///   <para><b>约束或前提</b>需要评分筛选（宽缓边、脏污边降权）时本方法给不了，改 <c>FuzzyMeasurePairs</c>；只量一条边用 <see cref="MeasurePos"/> 更省事。</para>
	///   <para><b>与相邻算子的取舍</b>测缝宽/线宽取 intraDistance，测节距取 interDistance；两者都是沿扫描方向的弧长/轴向距离而非欧氏弦距（弧卡尺时差异明显）[待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("part.png");
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.MeasurePairs(img, 1.0, 30.0, "all", "all",
	///       out JlTuple row1, out JlTuple col1, out JlTuple amp1,
	///       out JlTuple row2, out JlTuple col2, out JlTuple amp2,
	///       out JlTuple intra, out JlTuple inter);
	///   row1.Dispose(); col1.Dispose(); amp1.Dispose();
	///   row2.Dispose(); col2.Dispose(); amp2.Dispose();
	///   intra.Dispose(); inter.Dispose();
	///   m.Dispose();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>8 路 out 均为新 <c>JlTuple</c> 句柄，用毕释放；0 条边缘时各路为空元组而非 null [待实测]。</para>
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
	///   在卡尺带内提取直线边缘的亚像素位置与带符号幅值（原生算子 id 813）：Measure 族最常用、最轻量的"找边"。</summary>
	/// <param name="image">输入图像。</param>
	/// <param name="sigma">剖面高斯平滑（求一阶导算梯度前）的标准差。Default: 1.0</param>
	/// <param name="threshold">计入边缘的最小幅值；调它比调 select 更能控制误检。Default: 30.0</param>
	/// <param name="transition">按明暗过渡方向筛选边缘。Default: "all"</param>
	/// <param name="select">边缘的取舍方式（按序/按幅值截取等）。Default: "all"</param>
	/// <param name="rowEdge">边缘中点行坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="columnEdge">边缘中点列坐标（像素），DOUBLE 元组，新句柄。</param>
	/// <param name="amplitude">边缘幅值，带符号。DOUBLE 元组，新句柄。</param>
	/// <param name="distance">相邻边缘间距（像素）。DOUBLE 元组，新句柄。</param>
	/// <remarks>
	///   <para><b>功能说明</b>控制参数索引 sigma=1、threshold=2、transition=3、select=4；4 路输出索引 0~3 全 DOUBLE。幅值符号即过渡方向（由暗到亮/由亮到暗其一为正），拿它当"边强"用要取绝对值 [待实测：符号约定]。</para>
	///   <para><b>约束或前提</b>边缘位置是剖面一阶导过零点的二次拟合结果，宽度小于 2×sigma 平滑核的毛刺会合并 [待实测]；卡尺带没入图像外时对应位置缺失 [待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>要"一对边"的宽度用 <see cref="MeasurePairs"/>；要灰度交点用 <see cref="MeasureThresh"/>；要评分筛边用 <c>FuzzyMeasurePos</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlImage img = new JlImage("part.png");
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.MeasurePos(img, 1.0, 30.0, "all", "all",
	///       out JlTuple rowEdge, out JlTuple colEdge, out JlTuple amp, out JlTuple dist);
	///   rowEdge.Dispose(); colEdge.Dispose(); amp.Dispose(); dist.Dispose();
	///   m.Dispose();
	///   img.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>out 元组为新句柄，用毕释放；结果顺序即原生给出的顺序，跨版本不保证稳定，别按下标写死期望 [待实测]。</para>
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
	///   把卡尺带整体平移到新的参考点（原生算子 id 814）：row/column 是新中心坐标而非位移量；无输出参数，改动直接发生在句柄内部。</summary>
	/// <param name="row">新参考点的行坐标（像素）。Default: 50.0</param>
	/// <param name="column">新参考点的列坐标（像素）。Default: 100.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>元组版经 <c>Store</c> 钉住两路参数、调用后 <c>UnpinTuple</c>。方法体没有 InitOCT/Load——原生在句柄内就地改写几何，C# 侧对象与句柄值都不换，调用后继续可用，不需要额外 Dispose。</para>
	///   <para><b>约束或前提</b>要"平移 d 像素"得自己先读当前中心再加 [待实测：GetMeasureParam 能否读回当前中心]；圆弧卡尺平移的是圆心。</para>
	///   <para><b>与相邻方法的取舍</b>位姿+形状一起大改，用 <c>GenMeasureRectangle2</c>/<c>GenMeasureArc</c> 重建（会先释放旧句柄）；只是跟件挪位置，用本方法（保留句柄、开销小）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple row = 320.0;                                    // double→JlTuple 隐式转换
	///   JlTuple column = 210.0;
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.TranslateMeasure(row, column);
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>连续调用不会叠加位移——每次都是把中心设为该次的绝对坐标，最后一次说了算。</para>
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
	///   把卡尺带平移到新的绝对参考点（原生算子 id 814，标量版）：与元组版同一算子，经 <c>StoreD</c> 直写两路 DOUBLE 控制参数（索引 1/2），无钉固定开销。</summary>
	/// <param name="row">新参考点的行坐标（像素），非位移量。Default: 50.0</param>
	/// <param name="column">新参考点的列坐标（像素），非位移量。Default: 100.0</param>
	/// <remarks>
	///   <para><b>功能说明</b>无 InitOCT/Load：句柄值不变、原生侧就地改写几何，调用后实例照常可用；本方法也不会替你 Dispose 任何东西。</para>
	///   <para><b>与相邻方法的取舍</b>单点平移用本重载；给一批卡尺各配一个参考点（若原生支持多值 [待实测]）才需要元组重载。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.TranslateMeasure(320.0, 210.0);
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>连续调用以最后一次为准（绝对坐标语义，不叠加）。</para>
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
	///   用圆弧卡尺参数就地重建本对象（原生算子 id 815）：先 <c>Dispose()</c> 释放旧句柄，再把新圆弧卡尺句柄装入同一个 C# 引用。</summary>
	/// <param name="centerRow">弧的圆心行坐标，像素。Default: 100.0</param>
	/// <param name="centerCol">弧的圆心列坐标，像素。Default: 100.0</param>
	/// <param name="radius">弧半径，像素（扫描带中心线所在半径）。Default: 50.0</param>
	/// <param name="angleStart">弧起始角，弧度。Default: 0.0</param>
	/// <param name="angleExtent">弧张角，弧度，默认 6.28318 即整圆。Default: 6.28318</param>
	/// <param name="annulusRadius">环形扫描带的半宽，像素。Default: 10.0</param>
	/// <param name="width">后续要处理的图像的宽度，像素。Default: 512</param>
	/// <param name="height">后续要处理的图像的高度，像素。Default: 512</param>
	/// <param name="interpolation">重采样插值类型。Default: "nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b>与构造器同一原生 id 815；差别是方法体开头那句 <c>Dispose()</c>——基类 <c>Load</c> 要求自身 UNDEF，否则抛 <c>JlException</c>，所以先自弃旧句柄再装载。外部拿着的引用不换，无需重新赋值。</para>
	///   <para><b>约束或前提</b>重建失败时对象已退化为空壳（旧卡尺回不来）；调用前如旧配置金贵，先 <c>SerializeMeasure</c> 备份。</para>
	///   <para><b>与相邻方法的取舍</b>旧对象没用了、想新建就调 <c>JlMeasure(JlTuple,…)</c> 构造器（记得 Dispose 旧的）；想在固定实例上换圆弧参数用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple centerRow = 100.0;                              // double→JlTuple 隐式转换
	///   JlTuple centerCol = 100.0;
	///   JlTuple radius = 60.0;
	///   JlTuple angleStart = 0.0;
	///   JlTuple angleExtent = 3.14159;
	///   JlTuple annulusRadius = 8.0;
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.GenMeasureArc(centerRow, centerCol, radius, angleStart, angleExtent, annulusRadius, 512, 512, "nearest_neighbor");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>新句柄仍归本实例管，最终 <c>Dispose()</c> 一次即可（旧的已在本方法内部释放）。</para>
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
	///   用圆弧卡尺参数就地重建本对象（原生算子 id 815，标量版）：先释放旧句柄再把新句柄装入同一个 C# 引用，参数经 <c>StoreD</c>/<c>StoreI</c>/<c>StoreS</c> 直写。</summary>
	/// <param name="centerRow">弧的圆心行坐标，像素。Default: 100.0</param>
	/// <param name="centerCol">弧的圆心列坐标，像素。Default: 100.0</param>
	/// <param name="radius">弧半径，像素（扫描带中心线所在半径）。Default: 50.0</param>
	/// <param name="angleStart">弧起始角，弧度。Default: 0.0</param>
	/// <param name="angleExtent">弧张角，弧度，默认 6.28318 即整圆。Default: 6.28318</param>
	/// <param name="annulusRadius">环形扫描带的半宽，像素。Default: 10.0</param>
	/// <param name="width">后续要处理的图像的宽度，像素。Default: 512</param>
	/// <param name="height">后续要处理的图像的高度，像素。Default: 512</param>
	/// <param name="interpolation">重采样插值类型。Default: "nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b>方法体顺序是 <c>Dispose()</c>→存参→调用→<c>Load</c>：句柄值换新、对象引用不变。单条弧的重定位/重定径用本重载，省掉元组钉固定。</para>
	///   <para><b>约束或前提</b>调用失败即空壳（旧句柄已释放且不可恢复）；参数含义与圆弧构造器一致，径向扫描、边垂直于弧。</para>
	///   <para><b>与相邻方法的取舍</b>只想挪圆心得用 <c>TranslateMeasure</c>（不换句柄、开销更小）；要换半径/角度/带宽这类形状参数才用本方法。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.GenMeasureArc(100.0, 100.0, 50.0, 0.0, 6.28318, 10.0, 512, 512, "nearest_neighbor");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>重建后的实例只需一次 <c>Dispose()</c>；不要对本方法的结果再配 <c>CloseMeasure</c>。</para>
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
	///   用矩形卡尺参数就地重建本对象（原生算子 id 816，元组版）：先 <c>Dispose()</c> 释放旧句柄，新句柄装入同一个 C# 引用；参数经 <c>Store</c> 钉固定、调用后逐个 <c>UnpinTuple</c>。</summary>
	/// <param name="row">矩形中心的行坐标，像素。Default: 300.0</param>
	/// <param name="column">矩形中心的列坐标，像素。Default: 200.0</param>
	/// <param name="phi">矩形长轴与水平方向的夹角，弧度。Default: 0.0</param>
	/// <param name="length1">矩形长轴方向的半长（扫描方向），像素。Default: 100.0</param>
	/// <param name="length2">矩形短轴方向的半长（灰度平均方向），像素。Default: 20.0</param>
	/// <param name="width">后续要处理的图像的宽度，像素。Default: 512</param>
	/// <param name="height">后续要处理的图像的高度，像素。Default: 512</param>
	/// <param name="interpolation">重采样插值类型。Default: "nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b>与构造器同一原生 id 816；开头 <c>Dispose()</c> 是基类 <c>Load</c> 的硬前提（非 UNDEF 会抛 <c>JlException</c>）。适合实例被下游长期持有、只需换位姿/尺寸的场合。</para>
	///   <para><b>约束或前提</b>失败即空壳，旧卡尺不可恢复；phi 为弧度、边垂直于长轴（沿 phi 方向扫描）。</para>
	///   <para><b>与相邻方法的取舍</b>只挪中心不转不缩放 → <c>TranslateMeasure</c>（不换句柄）；要一次改全套几何 → 本方法；连圆弧带都要换 → <c>GenMeasureArc</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple row = 320.0;                                    // double→JlTuple 隐式转换
	///   JlTuple column = 210.0;
	///   JlTuple phi = 0.1;
	///   JlTuple length1 = 90.0;
	///   JlTuple length2 = 15.0;
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.GenMeasureRectangle2(row, column, phi, length1, length2, 512, 512, "nearest_neighbor");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>重建后的实例仍只需一次 <c>Dispose()</c>。</para>
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
	///   用矩形卡尺参数就地重建本对象（原生算子 id 816，标量版）：先释放旧句柄再装入新句柄，C# 引用不变；参数 <c>StoreD</c>/<c>StoreI</c>/<c>StoreS</c> 直写。</summary>
	/// <param name="row">矩形中心的行坐标，像素。Default: 300.0</param>
	/// <param name="column">矩形中心的列坐标，像素。Default: 200.0</param>
	/// <param name="phi">矩形长轴与水平方向的夹角，弧度。Default: 0.0</param>
	/// <param name="length1">矩形长轴方向的半长（扫描方向），像素。Default: 100.0</param>
	/// <param name="length2">矩形短轴方向的半长（灰度平均方向），像素。Default: 20.0</param>
	/// <param name="width">后续要处理的图像的宽度，像素。Default: 512</param>
	/// <param name="height">后续要处理的图像的高度，像素。Default: 512</param>
	/// <param name="interpolation">重采样插值类型。Default: "nearest_neighbor"</param>
	/// <remarks>
	///   <para><b>功能说明</b>跟件循环里每帧换位姿/换尺寸的推荐入口：避开每次 new 新对象再Dispose 旧对象的往复，直接复用本壳（方法体开头 <c>Dispose()</c> 释放旧句柄是基类 <c>Load</c> 的硬前提）。</para>
	///   <para><b>约束或前提</b>调用失败即空壳；扫描沿 phi 方向、边垂直于长轴；<c>length2</c> 越大平均越强、对细线会糊。</para>
	///   <para><b>与相邻方法的取舍</b>单点平移用 <c>TranslateMeasure</c>；元组批量形态用同名元组重载 [待实测：多值是否展开多条卡尺]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   m.GenMeasureRectangle2(320.0, 210.0, 0.1, 90.0, 15.0, 512, 512, "nearest_neighbor");
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>重建后的实例只需一次 <c>Dispose()</c>。</para>
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
	///   查询本卡尺句柄的参数/属性值（原生算子 id 2153，远离本类其余算子的 799~816 编号段）。</summary>
	/// <param name="genParamName">参数名的 JlTuple（可钉多元素批量查询 [待实测]）。Default: "type"</param>
	/// <returns>参数值的新 <c>JlTuple</c> 句柄；装载未指定目标类型（<c>JlTuple.LoadNew</c> 无 <c>JlTupleType</c> 走位），类型随原生返回——几何类查询多为数值、"type" 多为串 [待实测]。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>元组版把参数名整串钉住传入、调用后解钉。可查名字的全集在原生侧，本文件代码只能证实默认 "type"。</para>
	///   <para><b>与相邻方法的取舍</b>单查一个名用 <see cref="GetMeasureParam(string)"/>（走 <c>StoreS</c>，不钉元组，更省）；本重载存在的意义是配合名列表批量查询 [待实测]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple names = "type";                                   // string→JlTuple 隐式转换
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   JlTuple values = m.GetMeasureParam(names);
	///   values.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组是新句柄，用毕释放；查询不改动本卡尺状态。</para>
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
	///   按单个名字查询本卡尺句柄的参数/属性值（原生算子 id 2153，标量版）。</summary>
	/// <param name="genParamName">参数名，经 <c>StoreS</c> 以 STRING 控制参数（索引 1）直传，不钉元组。Default: "type"</param>
	/// <returns>参数值的新 <c>JlTuple</c> 句柄；<c>LoadNew</c> 不带类型强转，实际元素类型随原生答复（"type" 预期为串，数值参数预期为数 [待实测]）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>与本句柄（图像型入参 0）配合，把问名换成答值；纯读，不改变卡尺状态，也不需要重新 Dispose 句柄。</para>
	///   <para><b>与相邻方法的取舍</b>字符串字面量查询用本重载最顺（string→直接 <c>StoreS</c>）；手里已有 <c>JlTuple</c> 名列表才用 <see cref="GetMeasureParam(JlTuple)"/>。问当前中心位姿以便做增量平移，也归本方法管 [待实测：可查名字全集]。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlMeasure m = new JlMeasure(300.0, 200.0, 0.0, 100.0, 20.0, 512, 512, "nearest_neighbor");
	///   JlTuple typeInfo = m.GetMeasureParam("type");
	///   typeInfo.Dispose();
	///   m.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回元组是新句柄，用毕释放（纯数值/字符串元组的 <c>Dispose</c> 只处理句柄类元素，不调也不构成句柄泄漏）。</para>
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
