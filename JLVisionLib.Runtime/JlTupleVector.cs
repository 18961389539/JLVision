namespace JLVisionLib;

/// <summary>
///   Vision 向量族类是为支撑导出的 JlDevelop 代码、以及向带向量参数的过程传递向量实参而设计的，
///   并非用作泛型容器；如需通用容器请改用 <c>List&lt;T&gt;</c> 等标准容器类。
/// </summary>
public class JlTupleVector : JlVector
{
	private JlTuple mTuple;

	/// <summary>叶向量（Dimension 为 0）里那个元组值：get 交出内部 JlTuple 引用本体，set 先释放旧值再存入实参的副本。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>读写两路都先执行 <c>AssertDimension(0)</c>；get 是 <c>return mTuple;</c>（同一个引用，不复制），set 是 <c>mTuple.Dispose(); mTuple = new JlTuple(value)</c>，即按实参的 <c>Type</c> 重包一份数据后替换字段，赋值之后向量与外部那个 JlTuple 不再联动。</para>
	///   <para><b>约束或前提</b>对 Dimension 大于 0 的向量访问本属性直接抛 <c>JlVectorAccessException</c>；set 传 null 抛 "Null tuple not allowed in vector"。容器元素类型恒为 JlTuple，向量自身不记录元素类型，数值/字符串/HANDLE 的混合只体现在元组自己的 Type 上。</para>
	///   <para><b>与相邻算子的取舍</b>按序号取下一层子向量用 <c>this[int]</c>（越界访问会静默扩容）或 <c>At(int)</c>（越界访问抛异常）；要把整棵向量摊平成一条元组用 <c>ConvertVectorToTuple()</c>；本属性只在叶层取值。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector leaf = new JlTupleVector(0);
	///   leaf.T = new JlTuple(1, 2);      // set：存入副本，向量此后独立
	///   JlTuple val = leaf.T;            // get：拿到内部引用本体
	///   int n = val.Length;              // 2
	///   leaf.Dispose();                  // 释放内部 JlTuple，val 此后不可再用
	///   </code>
	///   <para><b>资源与坑</b>get 返回的是活引用：向量 Dispose（或再次赋值）之后它指向已释放对象，要长期留存必须先 <c>Clone()</c>；叶向量的 <c>Length</c> 恒为 0，判断"有没有值"要看本属性而不是 Length。</para>
	/// </remarks>
	public JlTuple T
	{
		get
		{
			AssertDimension(0);
			return mTuple;
		}
		set
		{
			AssertDimension(0);
			if (value == null)
			{
				throw new JlVectorAccessException("Null tuple not allowed in vector");
			}
			mTuple.Dispose();
			mTuple = new JlTuple(value);
		}
	}

	/// <summary>按 0 基下标读写同维子向量：越界访问一律静默补空扩容（读也一样），get 交出容器内部的活动引用。</summary>
	/// <param name="index">本层子向量下标，0 基。负值抛异常；大于等于当前 <c>Length</c> 则先把长度撑到 <c>index + 1</c>。</param>
	/// <remarks>
	///   <para><b>功能说明</b>转调基类索引器：先 <c>if (mDimension &lt; 1 || index &lt; 0) throw</c>，再 <c>AssertSize(index)</c>——长度不足时逐个追加 <c>GetDefaultElement()</c>，对元组向量就是"低一维、内含空元组的叶向量"；最后 <c>return (JlTupleVector)base[index]</c>，交出容器里那个实例本身，改它即改本向量。</para>
	///   <para><b>约束或前提</b>只读且不希望长度变化时改用 <c>At(index)</c>（越界即抛 <c>JlVectorAccessException</c>）。set 要求实参 <c>Dimension</c> 恰为本方 <c>Dimension - 1</c>，否则抛 "Vector dimension mismatch"；存入的是 <c>value.Clone()</c> 深拷贝，被顶替的旧元素当场 <c>Dispose</c>，所以赋值之后实参可安全复用或释放。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector v = new JlTupleVector(1);
	///   v[2].T = new JlTuple(42);             // 读路径也扩容：Length 由 0 变 3，前两格是空元组叶
	///   int grown = v.Length;
	///   JlTupleVector kept = v[2].Clone();    // 内部引用要跨过容器生命周期必须 Clone
	///   v.Dispose();
	///   int slots = kept.T.Length;
	///   kept.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>get 出的引用与容器同生命周期，向量 Dispose 后即成悬垂；扩容写入是静默的，拼错下标不报错、只会把向量拉长。</para>
	/// </remarks>
	public new JlTupleVector this[int index]
	{
		get
		{
			return (JlTupleVector)base[index];
		}
		set
		{
			base[index] = value;
		}
	}

	/// <summary>建一个指定维数的空向量：维数 0 得到内含空元组的叶，维数 1 及以上才挂子向量列表。</summary>
	/// <param name="dimension">嵌套层数，0 表示叶向量；负数被基类构造器拒绝。</param>
	/// <remarks>
	///   <para><b>功能说明</b>链到 <c>base(dimension)</c>：负数抛 "Invalid vector dimension"；<c>dimension &gt; 0</c> 时建一个空 <c>List&lt;JlVector&gt;</c> 且 <c>mTuple</c> 为 null；<c>dimension &lt;= 0</c> 时反过来——<c>mVector</c> 为 null、<c>mTuple = new JlTuple()</c> 是一个空元组。</para>
	///   <para><b>约束或前提</b>刚建好时 <c>Length</c> 恒为 0，要靠索引器、<c>Insert</c>、<c>Append</c> 或拷贝构造填内容；维数一经确定不可改（<c>Dimension</c> 无 setter），往后往里塞的东西维数必须是"本方维数减一"，否则被维数校验拒掉。</para>
	///   <para><b>与相邻算子的取舍</b>手里已有一条元组、想按块切成 1 维向量用 <c>JlTupleVector(JlTuple,int)</c>；想包单个元组用 <c>JlTupleVector(JlTuple)</c>；本构造只用来搭空骨架。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector row = new JlTupleVector(1);
	///   row[0].T = new JlTuple("alpha");   // 索引器把长度从 0 撑到 1，元素是 0 维叶
	///   int len = row.Length;
	///   row.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>纯托管构造，不申请任何原生资源；叶上的空元组仍要在 <c>Dispose</c> 时被释放，别把 Dimension 为 0 的实例当"值类型壳"漏掉释放。</para>
	/// </remarks>
	public JlTupleVector(int dimension)
		: base(dimension)
	{
		mTuple = ((dimension <= 0) ? new JlTuple() : null);
	}

	/// <summary>用给定元组建 Dimension 为 0 的叶向量，向内存的是该元组的一份副本。</summary>
	/// <param name="tuple">被复制进向量的元组，不得为 null。</param>
	/// <remarks>
	///   <para><b>功能说明</b>链 <c>base(0)</c> 后做 <c>mTuple = new JlTuple(tuple)</c>：按实参的 <c>Type</c> 取数组重包一份数据，之后外部元组的增删不影响向量，向量 Dispose 也不会碰外部元组。</para>
	///   <para><b>约束或前提</b><c>tuple</c> 为 null 抛 <c>JlVectorAccessException("Null tuple not allowed in vector")</c>（空元组不算 null，允许）。实例 Dimension 固定 0，索引器、<c>At</c>、<c>Concat</c> 在它上面都会因维数校验抛异常。</para>
	///   <para><b>与相邻算子的取舍</b>要按块拆成多格用 <c>JlTupleVector(JlTuple,int)</c>；要可继续追加元素的容器先用 <c>JlTupleVector(int)</c> 建 1 维再把叶塞进去。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple src = new JlTuple(1.5, 2.5);
	///   JlTupleVector leaf = new JlTupleVector(src);
	///   int n = leaf.T.Length;   // 2，且已是副本
	///   leaf.Dispose();
	///   src.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>这与 <c>JlObjectVector(JlObject)</c> 不同：对象向量做的是引用计数浅拷（共享底层数据），元组向量拷的是数据本身。</para>
	/// </remarks>
	public JlTupleVector(JlTuple tuple)
		: base(0)
	{
		if (tuple == null)
		{
			throw new JlVectorAccessException("Null tuple not allowed in vector");
		}
		mTuple = new JlTuple(tuple);
	}

	/// <summary>把一条元组按固定块长切成 1 维向量（末块可短），对应 JlDevelop 的 convert_tuple_to_vector_1d。</summary>
	/// <param name="tuple">待切分的元组，按区间拷进各叶元素。</param>
	/// <param name="blockSize">每块的元素个数，必须为正数；最后一块允许短于该值。</param>
	/// <remarks>
	///   <para><b>功能说明</b>链 <c>base(1)</c>，块数为 <c>tuple.Length / blockSize</c> 向上取整；循环用 <c>tuple.TupleSelectRange(i, i + blockSize - 1)</c> 取闭区间，再把区间交给 <c>JlTupleVector(JlTuple)</c> 叶构造，故每块都是独立副本。元组为空时得到长度 0 的向量。</para>
	///   <para><b>约束或前提</b><c>blockSize &lt;= 0</c> 抛 <c>JlVectorAccessException("Invalid block size in vector constructor")</c>。结果 Dimension 恒为 1，元素恒为 0 维叶，因此 <c>v[i].T</c> 可直接取值。</para>
	///   <para><b>与相邻算子的取舍</b>反过来把向量摊回一条元组用 <c>ConvertVectorToTuple()</c>；只想按索引取某几个元素用元组自己的 <c>TupleSelectRange</c>，不必建容器。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple t = new JlTuple(1, 2, 3, 4, 5);
	///   JlTupleVector blocks = new JlTupleVector(t, 2);   // 3 块：(1,2) (3,4) (5)
	///   int count = blocks.Length;
	///   JlTuple tail = blocks[2].T;                       // 末块只有 1 个元素
	///   int tailLen = tail.Length;
	///   blocks.Dispose();
	///   t.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>切分是复制而非视图：改 <c>blocks[0]</c> 不回写 <c>t</c>；遍历请从 0 到 <c>Length - 1</c>，用更大下标会顺手把向量撑出空元组叶。</para>
	/// </remarks>
	public JlTupleVector(JlTuple tuple, int blockSize)
		: base(1)
	{
		if (blockSize <= 0)
		{
			throw new JlVectorAccessException("Invalid block size in vector constructor");
		}
		int num = tuple.Length / blockSize;
		mVector.Capacity = ((num * blockSize < tuple.Length) ? (num + 1) : num);
		int i;
		for (i = 0; i < tuple.Length - blockSize; i += blockSize)
		{
			mVector.Add(new JlTupleVector(tuple.TupleSelectRange(i, i + blockSize - 1)));
		}
		if (i < tuple.Length)
		{
			mVector.Add(new JlTupleVector(tuple.TupleSelectRange(i, tuple.Length - 1)));
		}
	}

	/// <summary>逐层深拷贝一个元组向量：子向量结构与叶上的元组数据都各拷一份。</summary>
	/// <param name="vector">拷贝源，Dimension、Length 与全部元素内容被复制，源本身不被改动。</param>
	/// <remarks>
	///   <para><b>功能说明</b>链基类拷贝构造：取同维数、把 <c>Capacity</c> 预置为源长度，再对 0 到 <c>Length - 1</c> 执行 <c>mVector.Add(vector[i].Clone())</c>；本类另在 <c>mDimension &lt;= 0</c> 时补 <c>mTuple = new JlTuple(vector.mTuple)</c> 复制元组数据。它同时是 <c>CloneImpl()</c> 的实际执行体。</para>
	///   <para><b>约束或前提</b>副本与源互不影响，各自 <c>Dispose</c>；遍历源用的是索引器，但源长度在循环中固定，不会因此扩容。</para>
	///   <para><b>与相邻算子的取舍</b><c>Clone()</c> 走的就是本构造（差别只在返回静态类型是 <c>JlTupleVector</c>）；只想换个引用共享数据请用 <c>JlTuple</c> 自己的隐式转换，而不是拷容器。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector src = new JlTupleVector(new JlTuple(1, 2), 1);
	///   JlTupleVector dup = new JlTupleVector(src);
	///   dup[0].T = new JlTuple(9);
	///   bool same = src.VectorEqual(dup);   // false：源仍是 (1,2)
	///   src.Dispose();
	///   dup.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>数据翻倍，大向量注意峰值内存；<c>JlObjectVector</c> 的同名构造只拷结构、叶上对象仍是引用计数浅拷，两族独立性不同。</para>
	/// </remarks>
	public JlTupleVector(JlTupleVector vector)
		: base(vector)
	{
		if (mDimension <= 0)
		{
			mTuple = new JlTuple(vector.mTuple);
		}
	}

	/// <summary>造一个低一维的空元组向量作补位元素：其叶上带一个空元组。</summary>
	protected override JlVector GetDefaultElement()
	{
		return new JlTupleVector(mDimension - 1);
	}

	/// <summary>按下标返回子向量的内部引用，越界一律抛 JlVectorAccessException 且绝不扩容。</summary>
	/// <param name="index">0 基下标，合法范围 0 到 <c>Length - 1</c>。</param>
	/// <returns>该下标处子向量的<b>内部实例</b>（转成 <c>JlTupleVector</c>），不是副本。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转基类 <c>At</c>：条件 <c>mDimension &lt; 1 || index &lt; 0 || index &gt;= Length</c> 抛 "Index out of range"，通过后 <c>return mVector[index]</c> 本体。</para>
	///   <para><b>约束或前提</b>与索引器的分工就是"报错 vs 扩容"两条路：<c>At</c> 只暴露既有长度，索引器会顺手拉长。<c>Length</c> 为 0 时任何下标都抛。</para>
	///   <para><b>与相邻算子的取舍</b>要独立留存（跨越容器生命周期、或要单独 Dispose）用 <c>this[index].Clone()</c>；只想确认长度先看 <c>Length</c>，别靠捕获异常。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector blocks = new JlTupleVector(new JlTuple(1, 2, 3, 4), 2);
	///   JlTupleVector second = blocks.At(1);            // 内部引用，不复制
	///   int n = second.T.Length;                        // 2
	///   JlTupleVector own = blocks.At(1).Clone();       // 要独立留存就 Clone
	///   blocks.Dispose();
	///   own.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>旧英文描述称"返回的是副本、可安全留存"，与实现不符：返回的是活动引用，容器释放后即失效，写日志、跨线程传递前务必先 <c>Clone()</c>。</para>
	/// </remarks>
	public new JlTupleVector At(int index)
	{
		return (JlTupleVector)base.At(index);
	}

	/// <summary>元组向量的相等钩子覆写：1 维及以上转基类逐层递归，0 维叶用 T.TupleEqual 逐元素比内容。</summary>
	protected override bool EqualsImpl(JlVector vector)
	{
		if (mDimension >= 1)
		{
			return base.EqualsImpl(vector);
		}
		return ((JlTupleVector)vector).T.TupleEqual(T);
	}

	/// <summary>逐层比对维数、长度与元素内容是否完全相同，全等才返回 true（叶层比元组内容，不比引用）。</summary>
	/// <param name="vector">对照的元组向量。</param>
	/// <returns>三层条件全满足为 true；任一维长度不等或任一叶元组不等为 false。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>直接进 <c>EqualsImpl</c>：<c>Dimension &gt;= 1</c> 时走基类，比 Dimension、比 Length，再对每个下标递归 <c>this[i].VectorEqual(vector[i])</c>；<c>Dimension</c> 为 0 时比 <c>((JlTupleVector)vector).T.TupleEqual(T)</c>，即元组逐元素的内容比较。</para>
	///   <para><b>约束或前提</b>本重载不做运行时类型检查（形参静态类型已限定为 <c>JlTupleVector</c>）；基类的 <c>VectorEqual(JlVector)</c> 会先比 <c>GetType()</c>，运行时类型不同一律 false。</para>
	///   <para><b>与相邻算子的取舍</b>判"是不是同一个实例"用 <c>ReferenceEquals</c>；要比拼完的整条数据用 <c>ConvertVectorToTuple()</c> 后再比元组；只想看层数与格数用 <c>Dimension</c>/<c>Length</c> 更便宜。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector a = new JlTupleVector(new JlTuple(1, 2), 1);
	///   JlTupleVector b = new JlTupleVector(new JlTuple(1, 2), 1);
	///   bool eq = a.VectorEqual(b);   // true：内容一致，与是否同一实例无关
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>没有容差参数，浮点按元组侧口径精确比较；比较过程只加元素级 <c>lock</c>，并发写入时结果不可靠。</para>
	/// </remarks>
	public bool VectorEqual(JlTupleVector vector)
	{
		return EqualsImpl(vector);
	}

	/// <summary>产出一个新向量，内容是"本方向量的深拷 ++ 实方向量的深拷"，两侧原向量都不被改动。</summary>
	/// <param name="vector">接在本方之后的元组向量，其维数必须与本方相同。</param>
	/// <returns>新建的 <c>JlTupleVector</c>，长度为两侧之和；所有权归调用方，用完须 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>ConcatImpl(vector, append: false, clone: true)</c>：先 <c>hVector = Clone()</c>（本方整份深拷），把 <c>Capacity</c> 设为 <c>Length + vector.Length</c>，再逐个 <c>Add(vector[i].Clone())</c>。</para>
	///   <para><b>约束或前提</b>前置校验 <c>mDimension &lt; 1 || vector.Dimension != mDimension</c> 抛 "Vector dimension mismatch"——0 维叶向量不能拼接，两侧维数必须严格相等；拼接顺序恒为"本方在前、实参在后"。</para>
	///   <para><b>与相邻算子的取舍</b>想原地加长、少一次深拷用 <c>Append</c>；只是想临时连起来看一眼用 <c>ConvertVectorToTuple()</c> 拼元组更省。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector head = new JlTupleVector(new JlTuple(1), 1);
	///   JlTupleVector tail = new JlTupleVector(new JlTuple(2), 1);
	///   JlTupleVector both = head.Concat(tail);
	///   int n = both.Length;   // 2
	///   head.Dispose();
	///   tail.Dispose();        // 元素已各拷一份，both 不受影响
	///   both.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>另有一个 <c>Concat(vector, clone: false)</c> 重载（标注为不常用）：它把实参的子向量<b>引用</b>直接收进结果，两侧共享元素，先释放的那一侧会让另一侧对应槽位悬垂。</para>
	/// </remarks>
	public JlTupleVector Concat(JlTupleVector vector)
	{
		return (JlTupleVector)ConcatImpl(vector, append: false, clone: true);
	}

	/// <summary>把实方向量的元素逐个深拷进本向量尾部并返回 this，本方长度变长、实参不变。</summary>
	/// <param name="vector">被追加的元组向量，维数须与本方相同，其元素以副本形式进入本方。</param>
	/// <returns><c>this</c>（原地改写，便于链式调用），不是新向量。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>ConcatImpl(vector, append: true, clone: true)</c>：此时 <c>hVector</c> 就是本实例，不另建壳；<c>Capacity</c> 一次性设为 <c>Length + vector.Length</c>，追加的每个元素是 <c>vector[i].Clone()</c>。</para>
	///   <para><b>约束或前提</b>同样要求本方 <c>Dimension &gt;= 1</c> 且两侧维数相等，否则抛 "Vector dimension mismatch"。实参此后与结果无关，可以立即 <c>Dispose</c> 或继续复用。</para>
	///   <para><b>与相邻算子的取舍</b>要保留本方不变、拿一个新向量用 <c>Concat</c>；要在指定位置插入单个低一维元素用 <c>Insert</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector acc = new JlTupleVector(1);
	///   JlTupleVector piece = new JlTupleVector(new JlTuple(1, 2), 1);
	///   int before = acc.Length;      // 0
	///   acc.Append(piece);
	///   int after = acc.Length;       // 2
	///   acc.Dispose();
	///   piece.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回值与本实例是同一个对象，别把返回结果收下却把本方另作释放之外的用途；<c>Append(vector, clone: false)</c> 重载会共享元素引用，存在双侧重复释放的风险。</para>
	/// </remarks>
	public JlTupleVector Append(JlTupleVector vector)
	{
		return (JlTupleVector)ConcatImpl(vector, append: true, clone: true);
	}

	/// <summary>在 0 基下标处插入一个低一维的子向量，长度加 1 并返回 this，下标超出长度时先垫空元素。</summary>
	/// <param name="index">插入位置，0 基。负值抛异常；大于等于 <c>Length</c> 时先用默认空元素补齐。</param>
	/// <param name="vector">要插入的子向量，其 <c>Dimension</c> 必须恰为本方 <c>Dimension - 1</c>。</param>
	/// <returns><c>this</c>（原地改写）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>InsertImpl(index, vector, clone: true)</c>：校验后 <c>AssertSize(index - 1)</c> 把长度补到至少 <c>index</c>（缺口填 <c>GetDefaultElement()</c>，即含空元组的低一维叶），再 <c>mVector.Insert(index, vector.Clone())</c>。</para>
	///   <para><b>约束或前提</b>本方 <c>Dimension &lt; 1</c> 或实参维数不等于 <c>Dimension - 1</c> 抛 "Vector dimension mismatch"；<c>index &lt; 0</c> 抛 "Index out of range"。插入的是深拷副本，实参之后可安全释放。</para>
	///   <para><b>与相邻算子的取舍</b>只在尾部加、且一次加一整段用 <c>Append</c>；覆盖已有某格用索引器赋值（它会先释放被顶替的元素）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector v = new JlTupleVector(1);
	///   JlTupleVector leaf = new JlTupleVector(new JlTuple(7));   // 0 维叶
	///   v.Insert(0, leaf);
	///   int n = v.Length;   // 1
	///   v.Dispose();
	///   leaf.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>在长度 0 的向量上 <c>Insert(4, leaf)</c> 得到的是长度 5——前 4 格是空元组叶而不是报错，这类静默补位是错下标的典型症状；本族没有 <c>Add</c>，尾部追加请走 <c>Append</c> 或索引器。</para>
	/// </remarks>
	public JlTupleVector Insert(int index, JlTupleVector vector)
	{
		InsertImpl(index, vector, clone: true);
		return this;
	}

	/// <summary>删掉并释放 0 基下标 index 处的子向量，返回 this；下标越界时静默什么都不做。</summary>
	/// <param name="index">要删除的子向量下标，0 基；越界不报错也不删。</param>
	/// <returns><c>this</c>（原地改写），便于链式连续删除。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>RemoveImpl</c>：仅 <c>mDimension &lt; 1</c> 时抛 "Vector dimension mismatch"；命中条件写作 <c>if (index &gt;= 0 &amp;&amp; index &lt; Length)</c>，删除前先 <c>mVector[index].Dispose()</c> 再 <c>RemoveAt</c>，被删元素立即释放。</para>
	///   <para><b>约束或前提</b>删除会改变后续元素的下标（整体前移），倒序删除才不至于错位。</para>
	///   <para><b>与相邻算子的取舍</b>清空全部用 <c>Clear()</c>；只想替换内容用索引器赋值；要"取出并拥有"某格用 <c>At(index).Clone()</c> 后再 <c>Remove</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector v = new JlTupleVector(new JlTuple(1, 2, 3), 1);
	///   int before = v.Length;   // 3
	///   v.Remove(0);
	///   int after = v.Length;    // 2
	///   v.Remove(99);            // 越界：静默无操作，长度仍是 2
	///   v.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>三条越界路径语义各不相同：索引器静默扩容、<c>At</c> 抛异常、<c>Remove</c> 静默忽略——用 <c>Remove</c> 校验"删成功没有"是徒劳的，得自己比 <c>Length</c>；此前经 <c>At</c>/索引器取出的引用若指向被删格，随即悬垂。</para>
	/// </remarks>
	public new JlTupleVector Remove(int index)
	{
		RemoveImpl(index);
		return this;
	}

	/// <summary>逐个释放本层全部子向量并把长度清零，返回 this；向量的维数保持不变。</summary>
	/// <returns><c>this</c>（原地清空，可继续复用）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>ClearImpl</c>：在 <c>lock (mVector)</c> 内对 0 到 <c>Length - 1</c> 逐个 <c>mVector[i].Dispose()</c>（递归释放整棵子树），随后 <c>mVector.Clear()</c>。</para>
	///   <para><b>约束或前提</b>前置 <c>mDimension &lt; 1</c> 抛 "Vector dimension mismatch"：0 维叶没有元素可清，会抛而不是静默返回。<c>Dimension</c> 本身不受影响，清空后仍可 <c>Append</c>/<c>Insert</c> 复用。</para>
	///   <para><b>与相邻算子的取舍</b>只删一两格用 <c>Remove(index)</c>；连叶上的元组一起放手并对整个实例收工用 <c>Dispose()</c>（对 Dimension 大于 0 的向量它本来就等价于 Clear）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector v = new JlTupleVector(new JlTuple(1, 2, 3, 4), 2);
	///   v.Clear();
	///   int n = v.Length;      // 0
	///   int d = v.Dimension;   // 仍是 1
	///   v.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><c>List.Clear</c> 保留容量，元素对象已释放但底层数组内存不立即归还；清空后此前取出的任何子向量内部引用都已被 Dispose，不可再用。</para>
	/// </remarks>
	public new JlTupleVector Clear()
	{
		ClearImpl();
		return this;
	}

	/// <summary>造一个结构与元组数据都独立的副本向量并返回 <c>JlTupleVector</c>，与源不共享任何子向量。</summary>
	/// <returns>新的独立副本（维数、长度与内容一致），所有权归调用方，须自行 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b><c>CloneImpl()</c> 即 <c>new JlTupleVector(this)</c>：逐层 <c>Clone</c> 子向量、叶层复制元组数组数据，<c>Dimension</c> 与 <c>Length</c> 原样保留。基类另以显式接口 <c>ICloneable.Clone()</c> 暴露同一条路径，但返回 <c>object</c>。</para>
	///   <para><b>约束或前提</b>本方法带 <c>EditorBrowsable(Never)</c> 的基类同名包装，派生类用 <c>new</c> 把它收回为 <c>JlTupleVector</c> 返回类型，无需强转。</para>
	///   <para><b>与相邻算子的取舍</b>只要"再要一份同内容容器"用本方法；想把向量摊平成一条元组用 <c>ConvertVectorToTuple()</c>；只读遍历别拷，直接 <c>At</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector src = new JlTupleVector(new JlTuple(1, 2, 3), 1);
	///   JlTupleVector copy = src.Clone();
	///   copy[0].T = new JlTuple(9);
	///   int untouched = src[0].T.Length;   // 3：源未受影响
	///   src.Dispose();
	///   copy.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>元组向量是<b>真拷数据</b>（对象向量那族只拷结构、叶上对象为引用计数浅拷，两族独立性不同）；大向量克隆使峰值内存翻倍。</para>
	/// </remarks>
	public new JlTupleVector Clone()
	{
		return (JlTupleVector)CloneImpl();
	}

	/// <summary>克隆钩子实现：经 new JlTupleVector(this) 返回结构与元组数据都完全独立的新向量。</summary>
	protected override JlVector CloneImpl()
	{
		return new JlTupleVector(this);
	}

	/// <summary>Dispose 走到叶分支时的覆写：仅当本方为 0 维叶时释放其持有的 mTuple。</summary>
	protected override void DisposeLeafObject()
	{
		if (mDimension <= 0)
		{
			mTuple.Dispose();
		}
	}

	private int CountHTuples()
	{
		if (mDimension > 1)
		{
			int num = 0;
			for (int i = 0; i < base.Length; i++)
			{
				num += this[i].CountHTuples();
			}
			return num;
		}
		if (mDimension > 0)
		{
			return base.Length;
		}
		return 1;
	}

	private void CollectHTuples(JlTuple[] tuples, ref int index)
	{
		if (mDimension > 1)
		{
			for (int i = 0; i < base.Length; i++)
			{
				this[i].CollectHTuples(tuples, ref index);
			}
		}
		else if (mDimension > 0)
		{
			for (int j = 0; j < base.Length; j++)
			{
				tuples[index++] = this[j].mTuple;
			}
		}
		else
		{
			tuples[index++] = mTuple;
		}
	}

	/// <summary>深度优先、按下标升序把向量里所有叶元组首尾拼成一条新元组，向量本身不被改动。</summary>
	/// <returns>新建的 <c>JlTuple</c>（所有权归调用方，须 <c>Dispose</c>）；各叶为空元组时返回空元组而非 null。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>先 <c>CountHTuples()</c> 数叶子个数（<c>Dimension &gt; 1</c> 递归求和，等于 1 时取 <c>Length</c>，为 0 时计 1），再 <c>CollectHTuples()</c> 把各 <c>mTuple</c> 按序收进数组，最后 <c>new JlTuple().TupleConcat(tuples)</c> 输出拼接结果。</para>
	///   <para><b>约束或前提</b>索引器静默扩容补出来的空元组叶贡献 0 个元素、不占位，所以拼接长度只反映真实数据；<c>Dimension</c> 为 0 时等价于该元组的一份拷贝。收集阶段拿的是内部引用，拼接产出的是新元组，二者此后互不影响。</para>
	///   <para><b>与相邻算子的取舍</b>要的是"按块切分"就用 <c>JlTupleVector(JlTuple,int)</c> 反向操作；只想比内容用 <c>VectorEqual</c>，不必先摊平。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector v = new JlTupleVector(new JlTuple(1, 2), 1);
	///   JlTupleVector extra = new JlTupleVector(new JlTuple(3), 1);
	///   v.Append(extra);
	///   JlTuple flat = v.ConvertVectorToTuple();
	///   int n = flat.Length;   // 3：(1,2,3)
	///   flat.Dispose();
	///   extra.Dispose();
	///   v.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>各叶元组类型不一致（数值配字符串）时，拼出的元组类型行为由元组侧 MIXED 规则决定 [待实测]；返回元组要独立释放，别与向量的 Dispose 混为一谈。</para>
	/// </remarks>
	public JlTuple ConvertVectorToTuple()
	{
		JlTuple[] tuples = new JlTuple[CountHTuples()];
		int index = 0;
		CollectHTuples(tuples, ref index);
		return new JlTuple().TupleConcat(tuples);
	}

	/// <summary>叶向量直接交出内部元组的字符串形式，1 维及以上走基类的 "{元素, 元素}" 拼接。</summary>
	/// <returns>调试用文本：<c>Dimension &lt;= 0</c> 时为 <c>mTuple.ToString()</c>，否则为花括号包裹、", " 分隔的逐元素结果。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>覆写体只有两句：<c>if (mDimension &lt;= 0) return mTuple.ToString();</c> 否则转基类 <c>ToString()</c>——基类用 <c>StringBuilder</c> 依次拼 <c>"{"</c>、<c>this[i].ToString()</c>（以 <c>", "</c> 分隔）、<c>"}"</c>，基类自身对 <c>Dimension &lt;= 0</c> 返回空串。</para>
	///   <para><b>约束或前提</b>逐元素读用的是索引器，长度在循环中固定故不会扩容；每次调用重新拼串，纯托管开销、无原生调用。</para>
	///   <para><b>与相邻算子的取舍</b>要拿数据走 <c>T</c> 或 <c>ConvertVectorToTuple()</c>；要判空判长度走 <c>Length</c>。本方法只为调试可读性存在。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector leaf = new JlTupleVector(new JlTuple(1, 2, 3));
	///   string flat = leaf.ToString();
	///   leaf.Dispose();
	///   JlTupleVector blocks = new JlTupleVector(new JlTuple(1, 2, 3, 4), 2);
	///   string dbg = blocks.ToString();   // 形如 {1, 2}{3, 4} 外加一层花括号
	///   blocks.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>输出不含维数/长度标注、也不区分元素类型，叶向量与它内部那个元组的文本完全一致，看不出外层是容器；不要把它用于序列化或再解析。</para>
	/// </remarks>
	public override string ToString()
	{
		if (mDimension <= 0)
		{
			return mTuple.ToString();
		}
		return base.ToString();
	}
}
