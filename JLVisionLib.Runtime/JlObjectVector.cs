using System.ComponentModel;

namespace JLVisionLib;

/// <summary>
///   Vision 向量族类是为支撑导出的 JlDevelop 代码、以及向带向量参数的过程传递向量实参而设计的，
///   并非用作泛型容器；如需通用容器请改用 <c>List&lt;T&gt;</c> 等标准容器类。
/// </summary>
public class JlObjectVector : JlVector
{
	private JlObject mObject;

	/// <summary>
	///   读写叶向量（Dimension 为 0，无单位概念）承载的图标对象：get 交出向量内部自有的 JlObject 活引用（非副本），向量 Dispose 时随叶对象一并释放，欲在向量释放后存活须先复制一份；set 先要求本方维数为 0 且实参已初始化（否则抛 JlVectorAccessException），再释放旧对象、以 new JlObject(value) 走原生 CopyObject 存入 key 引用副本，原地改写、无返回值。
	/// </summary>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   JlObjectVector leaf = new JlObjectVector(img);
	///   leaf.O = img;                  // set：释放旧副本，另拷一份 key 写入
	///   JlObject inside = leaf.O;      // get：内部活引用，勿对其 Dispose
	///   int objs = inside.CountObj();  // 叶内对象元组的个数
	///   leaf.Dispose();                // 向量持有的对象随之一并释放
	///   </code>
	/// </remarks>
	public JlObject O
	{
		get
		{
			AssertDimension(0);
			return mObject;
		}
		set
		{
			AssertDimension(0);
			if (value == null || !value.IsInitialized())
			{
				throw new JlVectorAccessException("Uninitialized object not allowed in vector");
			}
			mObject.Dispose();
			mObject = new JlObject(value);
		}
	}

	/// <summary>
	///   按 0 基下标读写子向量：get 与 set 遇下标不小于当前 Length 都先静默扩容补齐（读也不例外，缺口填维数低一维的默认空向量，其叶含 GenEmptyObj 造出的空对象元组）；get 交出容器内部实例以便原地改状态，set 校验维数后存入实参的深拷贝、被顶替的旧元素随即 Dispose。本方为叶向量或 index 为负时抛 JlVectorAccessException。只读且不希望长度变化请改用 At(index)。
	/// </summary>
	/// <param name="index">本层下标，0 基。负值或本方为叶时抛异常；不小于当前 Length 时先把长度撑到 index 加 1。</param>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObjectVector v = new JlObjectVector(1);
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   using JlObjectVector item = new JlObjectVector(img);   // 0 维叶向量
	///   v[0] = item;               // set：存入 item.Clone()，item 之后可独立释放
	///   JlObjectVector sub = v[0]; // get：内部实例，与 v 同生命周期，勿单独 Dispose
	///   int grown = v.Length;      // 至少 1
	///   JlObjectVector kept = v[0].Clone();  // 要跨容器生命周期留存必须先 Clone
	///   v.Dispose();
	///   kept.Dispose();
	///   </code>
	/// </remarks>
	public new JlObjectVector this[int index]
	{
		get
		{
			return (JlObjectVector)base[index];
		}
		set
		{
			base[index] = value;
		}
	}

	/// <summary>
	///   构造指定维数的空对象向量：dimension 大于 0 时 Length 为 0、不含任何元素；dimension 为 0 时同步调原生 gen_empty_obj（id 602）建一个空对象元组作为叶值，之后可直接读写 O；dimension 为负在基类即抛 JlVectorAccessException。新建的托管向量由调用方持有，须自行 Dispose。
	/// </summary>
	/// <param name="dimension">向量维数，0 表示叶（叶构造即带空对象元组）。</param>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObjectVector v2 = new JlObjectVector(2);   // 2 维空向量，Length 为 0
	///   JlObjectVector leaf = new JlObjectVector(0); // 叶向量：持 GenEmptyObj 的空对象元组
	///   int objs = leaf.O.CountObj();                // 0：句柄有效但内容为空
	///   bool init = leaf.O.IsInitialized();          // true
	///   v2.Dispose();
	///   leaf.Dispose();
	///   </code>
	/// </remarks>
	public JlObjectVector(int dimension)
		: base(dimension)
	{
		mObject = ((dimension <= 0) ? GenEmptyObj() : null);
	}

	/// <summary>
	///   以指定对象构造 0 维叶向量：内部经 new JlObject(obj) 走原生 CopyObject 存 key 引用副本（不是像素级重建，源对象与本叶各持一份引用、各自释放），obj 为 null 或未初始化时抛 JlVectorAccessException，本构造不发算子调用。
	/// </summary>
	/// <param name="obj">要装入叶向量的图标对象（JlImage、JlRegion 等 JlObject 派生），须已初始化。</param>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   JlObjectVector leaf = new JlObjectVector(img);  // 存入的是 CopyObject 副本
	///   int objs = leaf.O.CountObj();                  // 1
	///   leaf.Dispose();                                // 只释放副本，img 仍可用
	///   </code>
	/// </remarks>
	public JlObjectVector(JlObject obj)
		: base(0)
	{
		if (obj == null || !obj.IsInitialized())
		{
			throw new JlVectorAccessException("Uninitialized object not allowed in vector");
		}
		mObject = new JlObject(obj);
	}

	/// <summary>
	///   对象向量的深拷贝构造：基类对 1 维以上逐元素 Clone() 子向量，叶层再经 CopyObject 另取一次对象 key；所得副本与原向量此后互不影响、各自须 Dispose。vector 为 null 时在基类读 Dimension 处抛 NullReferenceException。
	/// </summary>
	/// <param name="vector">被拷贝的源对象向量，拷贝后原向量可独立修改或释放。</param>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   JlObjectVector src = new JlObjectVector(img);
	///   JlObjectVector dst = new JlObjectVector(src);  // 叶对象再拷一份 key
	///   int objs = dst.O.CountObj();                   // 1，与 src 内容同、引用独立
	///   src.Dispose();                                 // 不影响 dst
	///   dst.Dispose();
	///   </code>
	/// </remarks>
	public JlObjectVector(JlObjectVector vector)
		: base(vector)
	{
		if (mDimension <= 0)
		{
			mObject = new JlObject(vector.mObject);
		}
	}

	private static JlObject GenEmptyObj()
	{
		JlObject hObject = new JlObject();
		hObject.GenEmptyObj();
		return hObject;
	}

	/// <summary>造一个低一维的空对象向量作补位元素：其叶上带 GenEmptyObj 出来的空对象元组。</summary>
	protected override JlVector GetDefaultElement()
	{
		return new JlObjectVector(mDimension - 1);
	}

	/// <summary>
	///   按下标只读访问子向量：本方为叶、index 为负或不小于当前 Length 时一律抛 JlVectorAccessException（"Index out of range"），绝不像索引器那样静默扩容；返回的是该下标处子向量的内部实例引用而非副本，可安全长期留存的只有其 Clone() 结果。
	/// </summary>
	/// <param name="index">0 基下标，合法范围 0 到 Length 减 1。</param>
	/// <returns>该下标处子向量（内部引用，静态类型 JlObjectVector），生命周期随本向量。</returns>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObjectVector v = new JlObjectVector(1);
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   using JlObjectVector item = new JlObjectVector(img);
	///   v[0] = item;
	///   JlObjectVector first = v.At(0);         // 内部引用，不复制
	///   JlObjectVector kept = v.At(0).Clone();  // 需独立留存就 Clone
	///   v.Dispose();
	///   kept.Dispose();
	///   </code>
	/// </remarks>
	public new JlObjectVector At(int index)
	{
		return (JlObjectVector)base.At(index);
	}

	/// <summary>对象向量的相等钩子覆写：1 维及以上转基类逐层递归，0 维叶转原生 test_equal_obj，返回非 0 判为相等。</summary>
	protected override bool EqualsImpl(JlVector vector)
	{
		if (mDimension >= 1)
		{
			return base.EqualsImpl(vector);
		}
		return ((JlObjectVector)vector).O.TestEqualObj(O) != 0;
	}

	/// <summary>
	///   判断两向量维数、逐层长度与元素内容是否全等：1 维以上走基类递归（逐层比维数、长度，再按下标递归比较，比较过程不扩容量）；0 维叶转原生 test_equal_obj（id 576），其返回非 0 记为相等。只读操作，双方句柄形态都不变。
	/// </summary>
	/// <param name="vector">对照的对象向量。</param>
	/// <returns>完全相同为 true，否则 false；叶层内容比较由原生按对象类型与数据判定。</returns>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage a = new JlImage("byte", 8, 8);
	///   using JlImage b = new JlImage("byte", 8, 8);
	///   JlObjectVector va = new JlObjectVector(a);
	///   JlObjectVector vb = new JlObjectVector(b);
	///   bool eq = va.VectorEqual(vb);   // 叶层：test_equal_obj 按内容判等非 0
	///   va.Dispose();
	///   vb.Dispose();
	///   </code>
	/// </remarks>
	public bool VectorEqual(JlObjectVector vector)
	{
		return EqualsImpl(vector);
	}

	/// <summary>
	///   拼接两个同维对象向量得到新向量：以本方的深拷贝作结果壳，再把实参的每个元素 Clone() 追加其后，两侧原向量都不被改动；本方为叶或两侧维数不等抛 JlVectorAccessException。返回的是新建向量（新句柄级对象），须调用方 Dispose。
	/// </summary>
	/// <param name="vector">接在本方之后的向量，Dimension 必须与本方严格相等。</param>
	/// <returns>长度为两侧之和的新 JlObjectVector，元素与本方、实参均无共享。</returns>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObjectVector head = new JlObjectVector(1);
	///   JlObjectVector tail = new JlObjectVector(1);
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   using JlObjectVector item = new JlObjectVector(img);
	///   head[0] = item;
	///   tail[0] = item;
	///   JlObjectVector both = head.Concat(tail);  // 两侧都已深拷入结果
	///   int n = both.Length;                      // 2
	///   head.Dispose();
	///   tail.Dispose();
	///   both.Dispose();
	///   </code>
	/// </remarks>
	public JlObjectVector Concat(JlObjectVector vector)
	{
		return (JlObjectVector)ConcatImpl(vector, append: false, clone: true);
	}

	/// <summary>
	///   拼接的框架内部口（带 EditorBrowsable(Never)）：本方仍深拷作结果壳，clone 决定实参元素是逐个 Clone() 进结果（等价无 bool 版）还是把实参的子向量实例按引用直接收编；返回新建向量（新句柄级对象），须调用方 Dispose，本方与实参都不被改动。
	/// </summary>
	/// <param name="vector">接在本方之后的向量，Dimension 必须与本方相等且本方维数不小于 1，否则抛 JlVectorAccessException("Vector dimension mismatch")。</param>
	/// <param name="clone">true 时对实参元素逐个 Clone()；false 时结果与实参共享同一批子向量实例。</param>
	/// <returns>长度为两侧之和的新 JlObjectVector。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>方法体是 ConcatImpl(vector, append: false, clone)：先 Clone() 本方得结果壳，Capacity 一次设为两侧长度之和，再按 clone 分支 Add(clone ? vector[i].Clone() : vector[i])。</para>
	///   <para><b>约束或前提</b>clone:false 是框架搬运原生输出用的省拷贝通道，元素所有权自此一物两主：结果与实参的列表里是同一批实例。</para>
	///   <para><b>与相邻算子的取舍</b>实参之后还要继续使用或先于结果释放，就用无 bool 版或 clone:true；只临时凑起来读一次内容才配 false。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObjectVector head = new JlObjectVector(1);
	///   JlObjectVector tail = new JlObjectVector(1);
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   using JlObjectVector item = new JlObjectVector(img);
	///   head[0] = item;
	///   tail[0] = item;
	///   JlObjectVector shared = head.Concat(tail, false);  // 后半段与 tail 共享实例
	///   int n = shared.Length;                             // 2
	///   shared.Dispose();
	///   head.Dispose();
	///   tail.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>clone:false 时 shared 与 tail 各含同一实例，先 Dispose shared 会释放该实例、tail 再 Dispose 时对同一实例二次走释放路径；托管壳把 key 复位 UNDEF 后二次 Dispose 不再进原生调用，实际安全性 [待实测]。Dispose 过 shared 之后勿再读 tail 的该格。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObjectVector Concat(JlObjectVector vector, bool clone)
	{
		return (JlObjectVector)ConcatImpl(vector, append: false, clone);
	}

	/// <summary>
	///   把实参向量的每个元素 Clone() 后追加到本向量尾部：原地改写本向量（Capacity 一次扩足），返回 this 而非新向量；本方为叶或两侧维数不等抛 JlVectorAccessException。因是深拷，实参之后可独立修改或释放。
	/// </summary>
	/// <param name="vector">接在本方之后的向量，Dimension 必须与本方相等。</param>
	/// <returns>this（原地改写后的本向量）。</returns>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObjectVector v = new JlObjectVector(1);
	///   JlObjectVector more = new JlObjectVector(1);
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   using JlObjectVector item = new JlObjectVector(img);
	///   v[0] = item;
	///   more[0] = item;
	///   JlObjectVector self = v.Append(more);    // 深拷追加，返回 this
	///   bool same = ReferenceEquals(self, v);    // true
	///   int n = v.Length;                        // 2
	///   more.Dispose();                          // 元素已各拷一份，可安全释放
	///   v.Dispose();
	///   </code>
	/// </remarks>
	public JlObjectVector Append(JlObjectVector vector)
	{
		return (JlObjectVector)ConcatImpl(vector, append: true, clone: true);
	}

	/// <summary>
	///   追加的框架内部口（带 EditorBrowsable(Never)）：把实参元素接进本向量尾部并返回 this，属原地改写、不产生新向量；clone 决定每个元素是 Clone() 后入列（等价无 bool 版）还是按引用与实参共享同一子向量实例。
	/// </summary>
	/// <param name="vector">接在本方之后的向量，Dimension 必须与本方相等且本方维数不小于 1，否则抛 JlVectorAccessException("Vector dimension mismatch")。</param>
	/// <param name="clone">true 时逐个深拷实参元素；false 时直接收编实参的子向量实例，两侧此后共享。</param>
	/// <returns>this（原地改写后的本向量）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>方法体是 ConcatImpl(vector, append: true, clone)：append 为真时结果壳就是本实例（不另建），Capacity 一次设为两侧长度之和，再按 clone 分支 Add(vector[i].Clone()) 或 Add(vector[i])。</para>
	///   <para><b>约束或前提</b>clone:false 是省拷贝通道：本向量与实参的列表同时持有那批实例，本向量 Remove/Clear 该格或实参先行释放都会让对方手里的那一格悬垂。</para>
	///   <para><b>与相邻算子的取舍</b>实参还要继续用就写 Append(vector)（深拷）；确认实参即弃且不想付拷贝代价才用 false。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObjectVector v = new JlObjectVector(1);
	///   JlObjectVector more = new JlObjectVector(1);
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   using JlObjectVector item = new JlObjectVector(img);
	///   v[0] = item;
	///   more[0] = item;
	///   JlObjectVector self = v.Append(more, false);  // more 的子向量实例按引用并入 v
	///   bool same = ReferenceEquals(self, v);         // true：原地改写
	///   int n = v.Length;                             // 2
	///   v.Dispose();
	///   more.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>示例末两行让共享实例先后被两侧各释放一次；托管壳把 key 复位 UNDEF 后二次 Dispose 不再进原生调用，实际安全性 [待实测]。要彻底避开就 clone:true 或先 Dispose 一侧再掏空另一侧。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObjectVector Append(JlObjectVector vector, bool clone)
	{
		return (JlObjectVector)ConcatImpl(vector, append: true, clone);
	}

	/// <summary>
	///   在 0 基下标处插入一个低一维子向量的深拷贝：本向量 Length 加 1 并返回 this（原地改写、不产生新句柄）；index 不小于当前 Length 时先用维数低一维的默认空向量静默补齐缺口，index 为负抛 "Index out of range"，实参维数不等于本方维数减 1 或本方为叶抛 "Vector dimension mismatch"。
	/// </summary>
	/// <param name="index">插入位置，0 基；超出末尾的部分以默认空向量垫齐。</param>
	/// <param name="vector">要插入的子向量，Dimension 必须恰为本方 Dimension 减 1；插入的是其 Clone() 副本，实参之后可独立释放。</param>
	/// <returns>this（原地改写后的本向量）。</returns>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObjectVector v = new JlObjectVector(1);
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   JlObjectVector leaf = new JlObjectVector(img);   // 0 维叶，恰为 1 维向量的合法元素
	///   v.Insert(0, leaf);
	///   int n = v.Length;                                // 1
	///   leaf.Dispose();                                  // 插入的是副本，可安全释放
	///   v.Dispose();
	///   </code>
	/// </remarks>
	public JlObjectVector Insert(int index, JlObjectVector vector)
	{
		InsertImpl(index, vector, clone: true);
		return this;
	}

	/// <summary>
	///   插入的框架内部口（带 EditorBrowsable(Never)）：在下标处放进一个低一维子向量并返回 this，原地改写；clone 决定放进的是实参的深拷副本（等价无 bool 版）还是实参实例本身——后者意味着该元素所有权移交本向量，实参之后不得再释放或改写它。
	/// </summary>
	/// <param name="index">插入位置，0 基；负值抛 "Index out of range"，不小于当前 Length 时先以默认空向量补齐缺口。</param>
	/// <param name="vector">要插入的子向量，Dimension 必须恰为本方 Dimension 减 1，本方为叶时抛 "Vector dimension mismatch"。</param>
	/// <param name="clone">true 时插入 vector.Clone()；false 时直接插入 vector 实例（所有权移交）。</param>
	/// <returns>this（原地改写后的本向量）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>方法体是 InsertImpl(index, vector, clone); return this：校验后经 AssertSize(index - 1) 补位，再锁内 mVector.Insert(index, clone ? vector.Clone() : vector)。</para>
	///   <para><b>约束或前提</b>clone:false 后该格随本向量的 Remove/Clear/Dispose 一起被释放，实参手里若还有引用即成悬垂；框架搬运原生输出时用它省一次深拷。</para>
	///   <para><b>与相邻算子的取舍</b>想插入后两侧都能独立使用就用无 bool 版或 clone:true。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObjectVector v = new JlObjectVector(1);
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   JlObjectVector leaf = new JlObjectVector(img);
	///   v.Insert(0, leaf, false);   // 实例直接进 v，所有权移交
	///   int n = v.Length;           // 1
	///   v.Dispose();                // 该格由 v 释放；此后不要再单独 Dispose leaf
	///   </code>
	///   <para><b>资源与坑</b>示例里 leaf 的释放责任已转移给 v——再写一次 leaf.Dispose() 属二次释放同一托管壳，其幂等性 [待实测]。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlObjectVector Insert(int index, JlObjectVector vector, bool clone)
	{
		InsertImpl(index, vector, clone);
		return this;
	}

	/// <summary>
	///   删除本向量 0 基下标处的元素：命中时先在锁内 Dispose 该子向量（连同其整棵子树）再 RemoveAt，Length 减 1，返回 this（原地改写、不产生新句柄）；下标为负或不小于当前 Length 时静默无操作、不抛；本方为叶抛 JlVectorAccessException("Vector dimension mismatch")。
	/// </summary>
	/// <param name="index">要删除的元素下标，0 基；越界不报错。</param>
	/// <returns>this（原地改写后的本向量）。</returns>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObjectVector v = new JlObjectVector(1);
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   using JlObjectVector item = new JlObjectVector(img);
	///   v[0] = item;
	///   v[1] = item;
	///   JlObjectVector self = v.Remove(0);  // 旧元素被释放，后续元素前移
	///   int n = v.Length;                   // 1
	///   v.Remove(9);                        // 越界：静默无操作
	///   v.Dispose();
	///   </code>
	/// </remarks>
	public new JlObjectVector Remove(int index)
	{
		RemoveImpl(index);
		return this;
	}

	/// <summary>
	///   清空本向量：在锁内对 0 到 Length 减 1 的每个子向量逐个 Dispose（递归释放整棵子树，叶对象随之释放）后清空元素表，Length 归 0，返回 this；属原地改写，向量本身可继续使用；本方为叶抛 JlVectorAccessException("Vector dimension mismatch")。
	/// </summary>
	/// <returns>this（已清空的本向量）。</returns>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObjectVector v = new JlObjectVector(1);
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   using JlObjectVector item = new JlObjectVector(img);
	///   v[0] = item;
	///   v.Clear();          // 元素连同其对象被释放，Length 归 0
	///   int n = v.Length;   // 0
	///   v.Dispose();
	///   </code>
	/// </remarks>
	public new JlObjectVector Clear()
	{
		ClearImpl();
		return this;
	}

	/// <summary>
	///   返回本向量的完全独立深拷贝：1 维以上逐子向量 Clone()，叶层经 CopyObject 另取对象 key；新向量与原向量生命周期互不影响（改一格互不可见、各自 Dispose），返回的是须由调用方释放的新句柄级对象。
	/// </summary>
	/// <returns>内容与本向量相同、引用完全独立的新 JlObjectVector。</returns>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlObjectVector v = new JlObjectVector(1);
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   using JlObjectVector item = new JlObjectVector(img);
	///   v[0] = item;
	///   JlObjectVector snap = v.Clone();
	///   v.Dispose();       // snap 不受影响
	///   int objs = snap.At(0).O.CountObj();   // 1
	///   snap.Dispose();
	///   </code>
	/// </remarks>
	public new JlObjectVector Clone()
	{
		return (JlObjectVector)CloneImpl();
	}

	/// <summary>克隆钩子实现：经 new JlObjectVector(this) 返回结构深拷、叶对象再走 CopyObject 的新向量。</summary>
	protected override JlVector CloneImpl()
	{
		return new JlObjectVector(this);
	}

	/// <summary>Dispose 走到叶分支时的覆写：仅当本方为 0 维叶时释放其持有的 mObject（JlObject 句柄）。</summary>
	protected override void DisposeLeafObject()
	{
		if (mDimension <= 0)
		{
			mObject.Dispose();
		}
	}

	/// <summary>
	///   调试用字符串表示：0 维叶返回内部对象原生 key 的十进制数字串（如 "3"，无单位；Dispose 后 key 复位 0 即得 "0"；不等于对象内容也不保证跨调用稳定），1 维以上转基类的逐层拼接形式；纯托管读取，不触发原生算子、不改变句柄形态。
	/// </summary>
	/// <returns>仅供日志/断点观察的字符串，勿用于序列化或解析。</returns>
	/// <remarks>
	///   <para><b>用法</b></para>
	///   <code>
	///   using JlImage img = new JlImage("byte", 8, 8);
	///   JlObjectVector leaf = new JlObjectVector(img);
	///   string s = leaf.ToString();   // 叶：对象 key 的数字串
	///   JlObjectVector v = new JlObjectVector(1);
	///   string t = v.ToString();      // 高维：基类拼接形式
	///   leaf.Dispose();
	///   v.Dispose();
	///   </code>
	/// </remarks>
	public override string ToString()
	{
		if (mDimension <= 0)
		{
			return mObject.Key.ToString();
		}
		return base.ToString();
	}
}
