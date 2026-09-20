using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace JLVisionLib;

/// <summary>
///   Vision 向量族类是为支撑导出的 JlDevelop 代码、以及向带向量参数的过程传递向量实参而设计的，
///   并非用作泛型容器；如需通用容器请改用 <c>List&lt;T&gt;</c> 等标准容器类。
///   另注：<c>JlVector</c> 为抽象基类，只能实例化 <c>JlTupleVector</c> 或 <c>JlObjectVector</c>。
/// </summary>
public abstract class JlVector : ICloneable, IDisposable
{
	internal int mDimension;

	/// <summary>本层子向量的 backing 列表：Dimension 为 0 的叶向量为 null，1 维及以上时每个槽位挂一个低一维的 JlVector。</summary>
	protected List<JlVector> mVector;

	/// <summary>向量的维数（嵌套层数）：0 表示叶、1 及以上才挂子向量，构造后终身不变。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>自动属性 <c>Dimension =&gt; mDimension</c>，只是把内部字段交出来：不加锁、不调原生、无副作用。<c>mDimension</c> 由构造器一次性写入，负数在 <c>JlVector(int)</c> 里就被 <c>JlVectorAccessException("Invalid vector dimension")</c> 拒掉。</para>
	///   <para><b>约束或前提</b>派生类按它分流：<c>Dimension</c> 为 0 时值在叶上（元组向量的 <c>T</c>、对象向量的 <c>O</c>），此时 <c>Length</c> 恒为 0；索引器、<c>At</c>、<c>Concat</c>、<c>Clear</c>、<c>Remove</c> 在 <c>mDimension &lt; 1</c> 时一律抛异常。赋入某格的合法性判据是"实参维数等于本方维数减一"。</para>
	///   <para><b>与相邻算子的取舍</b>数本层有几格看 <c>Length</c>；判"叶上有没有值"看 <c>T</c>/<c>O</c>，<c>Length</c> 为 0 既可能是叶也可能是空容器，单看它区分不了。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector leaf = new JlTupleVector(0);
	///   int d0 = leaf.Dimension;   // 0：值在 leaf.T 上
	///   JlTupleVector table = new JlTupleVector(2);
	///   int d2 = table.Dimension;  // 2：table[0] 仍是 1 维向量
	///   leaf.Dispose();
	///   table.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>基类本身不持任何原生句柄（本文件无一处 <c>JlNativeApi</c> 调用），释放压力全部落在叶元素上；读维数极廉价，可在循环条件里直接用。</para>
	/// </remarks>
	public int Dimension => mDimension;

	/// <summary>本层子向量的个数，0 基下标的合法范围是 0 到 Length 减一；维数为 0 的叶恒返回 0。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>实现是 <c>if (mDimension &lt;= 0) return 0;</c>，否则 <c>lock (mVector) { return mVector.Count; }</c>。叶向量的 <c>mVector</c> 为 null，所以走前一条分支返回 0，其真实内容要看 <c>T</c>/<c>O</c>。</para>
	///   <para><b>约束或前提</b>读 Count 单独加锁，但"先读 Length 再索引"这两步合起来不原子；更要紧的是索引器读越界会静默把 <c>Count</c> 撑大（见 <c>this[index]</c>），因此 Length 会在你没写任何代码的路径上变化。</para>
	///   <para><b>与相邻算子的取舍</b>要按序号安全遍历且不希望长度被改动用 <c>At(index)</c>（越界即抛）；想知道"有没有元素"用 <c>Length &gt; 0</c> 比捕获异常便宜。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector blocks = new JlTupleVector(new JlTuple(1, 2, 3, 4), 2);
	///   int n = blocks.Length;   // 2 块
	///   for (int i = 0; i &lt; n; i++)
	///   {
	///       int size = blocks.At(i).T.Length;
	///   }
	///   blocks.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>基类 <c>ToString</c>、<c>EqualsImpl</c> 都以 <c>Length</c> 为上界遍历，本身不会再触发扩容；并发场景下若别的线程可能写入，请自持锁而不是缓存 Length。</para>
	/// </remarks>
	public int Length
	{
		get
		{
			if (mDimension <= 0)
			{
				return 0;
			}
			lock (mVector)
			{
				return mVector.Count;
			}
		}
	}

	/// <summary>按 0 基下标读写子向量：越界访问一律静默补空扩容（读也不例外），get 交出容器内部实例、set 存入实参的深拷贝。</summary>
	/// <param name="index">本层下标，0 基。负值或本方为叶时抛异常；不小于当前 <c>Length</c> 则先把长度撑到 <c>index + 1</c>。</param>
	/// <remarks>
	///   <para><b>功能说明</b>get：<c>if (mDimension &lt; 1 || index &lt; 0) throw new JlVectorAccessException("Index out of range")</c> → <c>AssertSize(index)</c>（不足处逐个 <c>Add(GetDefaultElement())</c>）→ <c>return mVector[index]</c>，交出的是容器里那个实例本体。set：先校验 <c>value.Dimension != mDimension - 1</c> 则抛 "Vector dimension mismatch"，再 <c>AssertSize(index)</c>，锁内换成 <c>value.Clone()</c>，被顶替的旧元素在锁外 <c>Dispose()</c>。</para>
	///   <para><b>约束或前提</b>实现核对："返回内部引用、需 Clone 才能独立操作"与实现一致；<c>AssertSize</c> 的补位元素是"维数低一维的默认空向量"（元组向量含空元组、对象向量含 <c>GenEmptyObj</c> 出来的空对象元组），不会是 null。只读且不希望长度变化请改用 <c>At(index)</c>。</para>
	///   <para><b>与相邻算子的取舍</b>要在中间插一格用 <c>Insert</c>（同样会补位）；要删一格用 <c>Remove</c>（越界静默无操作）；只要一个可安全留存的快照就 <c>this[i].Clone()</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector v = new JlTupleVector(2);
	///   v[0][0].T = new JlTuple(1, 2);      // 两层索引器各自补位，叶上写元组
	///   int grown = v.Length;               // 至少 1
	///   JlVector kept = v[0].Clone();       // 内部引用要留存必须先 Clone
	///   v.Dispose();
	///   kept.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>赋进去的是副本、被换掉的是原对象，故实参之后可安全复用；但 get 出的引用与容器同生命周期，容器 <c>Dispose</c>/<c>Clear</c> 后立即悬垂。</para>
	/// </remarks>
	public JlVector this[int index]
	{
		get
		{
			if (mDimension < 1 || index < 0)
			{
				throw new JlVectorAccessException("Index out of range");
			}
			AssertSize(index);
			lock (mVector)
			{
				return mVector[index];
			}
		}
		set
		{
			if (mDimension < 1 || index < 0)
			{
				throw new JlVectorAccessException("Index out of range");
			}
			if (value.Dimension != mDimension - 1)
			{
				throw new JlVectorAccessException("Vector dimension mismatch");
			}
			AssertSize(index);
			JlVector hVector;
			lock (mVector)
			{
				hVector = mVector[index];
				mVector[index] = value.Clone();
			}
			hVector.Dispose();
		}
	}

	/// <summary>按维数搭空骨架的构造：dimension 为负抛 JlVectorAccessException，大于 0 时建一个空 List，为 0 时 mVector 保持 null（叶）。</summary>
	protected JlVector(int dimension)
	{
		if (dimension < 0)
		{
			throw new JlVectorAccessException("Invalid vector dimension " + dimension);
		}
		mDimension = dimension;
		mVector = ((dimension > 0) ? new List<JlVector>() : null);
	}

	/// <summary>结构深拷构造：以同维数建新容器并预置容量，再对源逐元素 vector[i].Clone() 追加，叶层载荷由各派生类补拷。</summary>
	protected JlVector(JlVector vector)
		: this(vector.Dimension)
	{
		if (mDimension > 0)
		{
			mVector.Capacity = vector.Length;
			for (int i = 0; i < vector.Length; i++)
			{
				mVector.Add(vector[i].Clone());
			}
		}
	}

	/// <summary>先释放本向量已有内容，再无损接管 source 的元素容器：只换引用、不拷贝，source 随即变空壳。</summary>
	/// <param name="source">被接管的一方，维数须与本方一致；传 null 时本方法退化为一次 <c>Dispose()</c>。</param>
	/// <remarks>
	///   <para><b>功能说明</b>顺序为：<c>source == this</c> 直接返回；<c>source.Dimension != Dimension</c> 抛 "Vector dimension mismatch"；随后 <c>Dispose()</c> 清掉自己，再 <c>mVector = source.mVector; source.mVector = new List&lt;JlVector&gt;()</c>，最后 <c>GC.ReRegisterForFinalize(this)</c>。元素实例本身一个都没复制，只是所有者换了。</para>
	///   <para><b>约束或前提</b><c>Dimension &lt;= 0</c> 的叶向量抛 "TransferOwnership not implemented for leaf"——叶上的 <c>mTuple</c>/<c>mObject</c> 不在基类管辖内。方法带 <c>[EditorBrowsable(EditorBrowsableState.Never)]</c>，是给框架搬运原生输出用的，业务代码不该常规调用。</para>
	///   <para><b>与相邻算子的取舍</b>想两边都能独立改就 <c>Clone()</c> 或拷贝构造（付一次深拷代价）；想让本方彻底清空且释放元素用 <c>Clear()</c>；只有"确定源不再被使用"时才配用本方法省这次拷贝。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector dst = new JlTupleVector(1);
	///   JlTupleVector src = new JlTupleVector(new JlTuple(1, 2), 1);
	///   dst.TransferOwnership(src);
	///   int taken = dst.Length;   // 2，元素改由 dst 负责释放
	///   int left = src.Length;    // 0：src 已被掏空
	///   src.Dispose();
	///   dst.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>调用后仍按旧 <c>src</c> 去读只会拿到空容器，不会报错；本类未定义析构函数，<c>Dispose</c> 里的 <c>GC.SuppressFinalize</c> 与这里的 <c>ReRegisterForFinalize</c> 对无终结器类型只是形式动作 [待实测]。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void TransferOwnership(JlVector source)
	{
		if (source == this)
		{
			return;
		}
		if (source != null && source.Dimension != Dimension)
		{
			throw new JlVectorAccessException("Vector dimension mismatch");
		}
		Dispose();
		if (source != null)
		{
			if (mDimension <= 0)
			{
				throw new JlVectorAccessException("TransferOwnership not implemented for leaf");
			}
			mVector = source.mVector;
			source.mVector = new List<JlVector>();
			GC.ReRegisterForFinalize(this);
		}
	}

	/// <summary>维数守卫：实际 Dimension 与期望值不符就抛 JlVectorAccessException，不返回值也不做任何修正。</summary>
	/// <param name="dimension">期望的维数，通常是 0（"我只接受叶上的值"）。</param>
	/// <remarks>
	///   <para><b>功能说明</b>方法体只有一句 <c>if (mDimension != dimension) throw new JlVectorAccessException("Expected vector dimension " + dimension)</c>，异常文本里带上期望值，便于定位是哪一层用错。</para>
	///   <para><b>约束或前提</b>派生类的值访问器（元组向量的 <c>T</c>、对象向量的 <c>O</c>）在读写两路都先调本方法传 0，所以对 1 维以上向量取值会在此处抛，而不是返回 null。它只比维数，不看 <c>Length</c>、不看元素内容。</para>
	///   <para><b>与相邻算子的取舍</b>想按维数分派处理逻辑用 <c>if (v.Dimension == 0) ...</c> 读 <c>Dimension</c> 自己判；需要"长度够不够"请比 <c>Length</c>，本方法管不着。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector leaf = new JlTupleVector(0);
	///   leaf.AssertDimension(0);      // 通过
	///   int n = leaf.T.Length;        // 空元组，长度为 0
	///   leaf.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>带 <c>[EditorBrowsable(EditorBrowsableState.Never)]</c>，是内部一致性检查手段；把它当断言用时会终结当前调用栈，热路径上不如先读 <c>Dimension</c> 自行分派。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void AssertDimension(int dimension)
	{
		if (mDimension != dimension)
		{
			throw new JlVectorAccessException("Expected vector dimension " + dimension);
		}
	}

	private void AssertSize(int index)
	{
		if (mVector == null)
		{
			return;
		}
		lock (mVector)
		{
			int count = mVector.Count;
			if (index >= count)
			{
				mVector.Capacity = index + 1;
				for (int i = count; i <= index; i++)
				{
					mVector.Add(GetDefaultElement());
				}
			}
		}
	}

	/// <summary>抽象钩子：造出一个维数为本方减一、内容为空的默认元素，供 AssertSize 与 Insert 静默补齐缺口时填充。</summary>
	protected abstract JlVector GetDefaultElement();

	/// <summary>按下标返回子向量的内部引用，越界一律抛 JlVectorAccessException，绝不扩容。</summary>
	/// <param name="index">0 基下标，合法范围 0 到 <c>Length - 1</c>。</param>
	/// <returns>该下标处子向量的<b>内部实例</b>（非副本），静态类型 <c>JlVector</c>，用派生类型需自行强转。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>判定式是 <c>mDimension &lt; 1 || index &lt; 0 || index &gt;= Length</c> 三选一即抛 "Index out of range"，通过后 <c>lock (mVector) return mVector[index];</c>。实现核对："返回内部引用、需 Clone 才能独立操作"与实现一致。</para>
	///   <para><b>约束或前提</b>与索引器的分工正是两条越界路线：<c>At</c> 报错、索引器静默把长度撑大。<c>Length</c> 为 0（含刚构造、刚 <c>Clear</c>）时任何下标都抛。</para>
	///   <para><b>与相邻算子的取舍</b>想让"越界即写"用索引器；要跨过容器生命周期留存结果用 <c>At(i).Clone()</c>；只是判有无元素先看 <c>Length</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector blocks = new JlTupleVector(new JlTuple(1, 2, 3, 4), 2);
	///   JlVector second = blocks.At(1);          // 内部引用，不复制
	///   JlTupleVector own = blocks.At(1).Clone();   // 需独立留存就 Clone，返回 JlTupleVector
	///   int keep = own.T.Length;
	///   blocks.Dispose();
	///   own.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的是活引用：随后对容器 <c>Clear</c>/<c>Dispose</c>/<c>Remove</c> 该格，都会让它指向已释放对象；基类 <c>Clone</c>（带 <c>EditorBrowsable(Never)</c>）与派生类 <c>new</c> 出的强类型版本才是留存的正规出口。</para>
	/// </remarks>
	public JlVector At(int index)
	{
		if (mDimension < 1 || index < 0 || index >= Length)
		{
			throw new JlVectorAccessException("Index out of range");
		}
		lock (mVector)
		{
			return mVector[index];
		}
	}

	/// <summary>相等比较的模板钩子，由 VectorEqual 通过运行时类型检查后转调：本实现比 Dimension、比 Length，再对每个下标递归 VectorEqual，维数大于 0 才逐层比。</summary>
	protected virtual bool EqualsImpl(JlVector vector)
	{
		if (vector.Dimension != Dimension)
		{
			return false;
		}
		if (vector.Length != Length)
		{
			return false;
		}
		if (mDimension > 0)
		{
			for (int i = 0; i < Length; i++)
			{
				if (!this[i].VectorEqual(vector[i]))
				{
					return false;
				}
			}
		}
		return true;
	}

	/// <summary>逐层比对维数、长度与元素内容是否完全相同；运行时类型不同一律判 false。</summary>
	/// <param name="vector">对照的向量，可以是任意 <c>JlVector</c> 派生类型。</param>
	/// <returns>同类型、同维数、逐层同长度且叶层内容全等为 true，否则 false。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>第一句是 <c>if ((object)vector.GetType() != GetType()) return false;</c>——比的是运行时类型，所以元组向量与对象向量互比永远 false；通过后转虚方法 <c>EqualsImpl</c>，其内比 Dimension、比 Length，再对每个下标递归 <c>this[i].VectorEqual(vector[i])</c>。</para>
	///   <para><b>约束或前提</b>叶层的相等由派生类各自实现：元组向量用 <c>TupleEqual</c>（内容比较），对象向量用 <c>TestEqualObj</c> 的原生结果非 0。没有容差参数，浮点按精确相等判。</para>
	///   <para><b>与相邻算子的取舍</b>判"是否同一实例"用 <c>ReferenceEquals</c>；派生类上另有静态类型更严的同名重载（如 <c>VectorEqual(JlTupleVector)</c>），那个版本不做 GetType 比较，别指望它替你拦跨类型比较。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlVector a = new JlTupleVector(new JlTuple(1, 2), 1);
	///   JlVector b = new JlTupleVector(new JlTuple(1, 2), 1);
	///   bool eq = a.VectorEqual(b);   // true：内容一致且运行时类型相同
	///   a.Dispose();
	///   b.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>递归里读元素走索引器，因先判 Length 相等不会扩容，但比较过程不持写锁，并发修改时结果不可信。</para>
	/// </remarks>
	public bool VectorEqual(JlVector vector)
	{
		if ((object)vector.GetType() != GetType())
		{
			return false;
		}
		return EqualsImpl(vector);
	}

	/// <summary>Concat/Append 共用的拼接钩子：append 决定改本实例还是 Clone() 出新壳，clone 决定实参元素逐个深拷还是按引用收编，本方为叶或两侧维数不等抛 JlVectorAccessException。</summary>
	protected JlVector ConcatImpl(JlVector vector, bool append, bool clone)
	{
		if (mDimension < 1 || vector.Dimension != mDimension)
		{
			throw new JlVectorAccessException("Vector dimension mismatch");
		}
		JlVector hVector = (append ? this : Clone());
		hVector.mVector.Capacity = Length + vector.Length;
		for (int i = 0; i < vector.Length; i++)
		{
			hVector.mVector.Add(clone ? vector[i].Clone() : vector[i]);
		}
		return hVector;
	}

	/// <summary>返回一个"本方向量深拷 ++ 实方向量深拷"的新向量，两侧原向量都不被改动。</summary>
	/// <param name="vector">接在本方之后的向量，<c>Dimension</c> 必须与本方严格相等。</param>
	/// <returns>新建的 <c>JlVector</c>（运行时类型为具体派生类），长度为两侧之和，须由调用方 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>ConcatImpl(vector, append: false, clone: true)</c>：以 <c>Clone()</c> 得到本方深拷作结果壳，<c>Capacity</c> 设为 <c>Length + vector.Length</c>，再逐个 <c>Add(vector[i].Clone())</c>。</para>
	///   <para><b>约束或前提</b>校验 <c>mDimension &lt; 1 || vector.Dimension != mDimension</c> 抛 "Vector dimension mismatch"——叶向量之间不能拼接；结果顺序恒为"本方在前"。</para>
	///   <para><b>与相邻算子的取舍</b>允许改本方、想省掉这一次深拷用 <c>Append</c>；要指定插入位置用 <c>Insert</c>；只要一条扁平元组就在元组向量上 <c>ConvertVectorToTuple()</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlVector head = new JlTupleVector(new JlTuple(1), 1);
	///   JlVector tail = new JlTupleVector(new JlTuple(2), 1);
	///   JlVector both = head.Concat(tail);
	///   int n = both.Length;   // 2
	///   head.Dispose();
	///   tail.Dispose();        // 已各拷一份，both 不受影响
	///   both.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回静态类型是 <c>JlVector</c>，取派生类成员需强转；另有 <c>Concat(vector, bool clone)</c> 重载可关掉拷贝，代价是结果与实参共享子向量引用。</para>
	/// </remarks>
	public JlVector Concat(JlVector vector)
	{
		return ConcatImpl(vector, append: false, clone: true);
	}

	/// <summary>拼接的框架内部口：clone 决定实参元素是深拷进结果，还是按引用与实参共享。</summary>
	/// <param name="vector">接在本方之后的向量，<c>Dimension</c> 必须与本方相等。</param>
	/// <param name="clone">true 时逐个 <c>Clone()</c> 实参元素（等价无 bool 版）；false 时把实参的子向量实例直接收进结果。</param>
	/// <returns>新建向量（本方深拷 ++ 实参元素），须由调用方 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>方法体只有 <c>return ConcatImpl(vector, append: false, clone);</c>。<c>ConcatImpl</c> 里对应两支：<c>hVector.mVector.Add(clone ? vector[i].Clone() : vector[i])</c>。</para>
	///   <para><b>约束或前提</b>维数校验照旧（本方 <c>Dimension &gt;= 1</c> 且两侧相等，否则 "Vector dimension mismatch"）。方法带 <c>[EditorBrowsable(EditorBrowsableState.Never)]</c>，是为框架搬运原生输出留的省拷贝通道，不打算给业务代码用。</para>
	///   <para><b>与相邻算子的取舍</b>实参之后还要继续用（尤其会被 Dispose 或修改）就老老实实 <c>clone: true</c> 或用 <c>Concat(JlVector)</c>；只是临时凑起来读一次内容，<c>clone: false</c> 省一整轮深拷。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector host = new JlTupleVector(new JlTuple(1), 1);
	///   JlTupleVector donor = new JlTupleVector(new JlTuple(2), 1);
	///   JlVector merged = host.Concat(donor, clone: false);
	///   int n = merged.Length;   // 2，后一格就是 donor[0] 那个实例本身
	///   host.Dispose();
	///   merged.Dispose();        // 连共享的元素一起释放
	///   </code>
	///   <para><b>资源与坑</b>用 <c>clone: false</c> 之后，<c>donor</c> 里那些元素的所有权已经和结果重叠：再对 <c>donor</c> 调 <c>Dispose</c>/<c>Clear</c>/<c>Remove</c> 会重复释放同一实例，示例里刻意不再动 donor。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlVector Concat(JlVector vector, bool clone)
	{
		return ConcatImpl(vector, append: false, clone);
	}

	/// <summary>把实方向量的元素逐个深拷进本向量尾部并返回 this，本方变长、实参不变。</summary>
	/// <param name="vector">被追加的向量，维数须与本方相等，其元素以副本形式进入本方。</param>
	/// <returns><c>this</c>（原地改写，便于链式调用），不是新向量。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>ConcatImpl(vector, append: true, clone: true)</c>：此时 <c>hVector</c> 就是本实例，不另建壳；<c>Capacity</c> 一次设为 <c>Length + vector.Length</c>，追加的每个元素是 <c>vector[i].Clone()</c>。</para>
	///   <para><b>约束或前提</b>本方 <c>Dimension &gt;= 1</c> 且两侧维数相等，否则抛 "Vector dimension mismatch"。实参之后与结果无关，可立即 <c>Dispose</c> 或继续复用。</para>
	///   <para><b>与相邻算子的取舍</b>要保住本方不变、拿一个新向量用 <c>Concat</c>；要在指定位置插入单个低一维元素用 <c>Insert</c>；本族没有 <c>Add</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlVector acc = new JlTupleVector(1);
	///   JlTupleVector piece = new JlTupleVector(new JlTuple(1, 2), 1);
	///   int before = acc.Length;   // 0
	///   acc.Append(piece);
	///   int after = acc.Length;    // 2，且 acc 与返回值是同一实例
	///   acc.Dispose();
	///   piece.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>返回的就是 <c>this</c>，别把返回值收下却把本方当两个对象分别释放；<c>Append(vector, bool clone)</c> 重载关掉拷贝后两侧共享元素引用，存在重复释放风险。</para>
	/// </remarks>
	public JlVector Append(JlVector vector)
	{
		return ConcatImpl(vector, append: true, clone: true);
	}

	/// <summary>原地追加的框架内部口：clone 决定实参元素是深拷进本向量，还是按引用与本向量共享。</summary>
	/// <param name="vector">被追加的向量，维数须与本方相等。</param>
	/// <param name="clone">true 逐个 <c>Clone()</c>（等价无 bool 版）；false 直接把实参的子向量实例挂进本方容器。</param>
	/// <returns><c>this</c>（原地改写）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>方法体 <c>return ConcatImpl(vector, append: true, clone);</c>，共享/深拷的分叉发生在 <c>Add(clone ? vector[i].Clone() : vector[i])</c>。</para>
	///   <para><b>约束或前提</b>带 <c>[EditorBrowsable(EditorBrowsableState.Never)]</c>，主要供框架把原生输出成段搬进既有容器以省去深拷；维数校验与 <c>Append(JlVector)</c> 完全相同。</para>
	///   <para><b>与相邻算子的取舍</b>拿不准就传 <c>clone: true</c>（或直接调单参重载）；确认实参是"一次性"的临时容器、且之后只 Dispose 结果时，<c>clone: false</c> 才划算。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector acc = new JlTupleVector(1);
	///   JlTupleVector batch = new JlTupleVector(new JlTuple(7, 8), 1);
	///   acc.Append(batch, clone: true);
	///   int n = acc.Length;   // 2
	///   batch.Dispose();      // 深拷过，可安全释放
	///   acc.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><c>clone: false</c> 时结果与本方共享元素，先释放的一方会让另一方的对应槽位悬垂；两个重载都只改 <c>this</c>，实参的 <c>Length</c> 始终不变。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlVector Append(JlVector vector, bool clone)
	{
		return ConcatImpl(vector, append: true, clone);
	}

	/// <summary>Insert 的共用实现钩子：校验维数与下标后先 AssertSize 补齐缺口，再锁内把元素（clone 决定存副本还是引用）插入指定位置。</summary>
	protected void InsertImpl(int index, JlVector vector, bool clone)
	{
		if (mDimension < 1 || vector.Dimension != mDimension - 1)
		{
			throw new JlVectorAccessException("Vector dimension mismatch");
		}
		if (index < 0)
		{
			throw new JlVectorAccessException("Index out of range");
		}
		AssertSize(index - 1);
		lock (mVector)
		{
			mVector.Insert(index, clone ? vector.Clone() : vector);
		}
	}

	/// <summary>在 0 基下标处插入一个低一维的子向量，长度加 1 并返回 this，下标超出长度时先垫默认空元素。</summary>
	/// <param name="index">插入位置，0 基。负值抛异常；不小于当前 <c>Length</c> 时先用默认空元素补齐。</param>
	/// <param name="vector">要插入的子向量，其 <c>Dimension</c> 必须恰为本方 <c>Dimension - 1</c>。</param>
	/// <returns><c>this</c>（原地改写）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>InsertImpl(index, vector, clone: true)</c>：校验通过后 <c>AssertSize(index - 1)</c> 把长度补到至少 <c>index</c>（缺口由抽象方法 <c>GetDefaultElement()</c> 造出的低一维默认向量填），再 <c>lock (mVector) mVector.Insert(index, vector.Clone())</c>。</para>
	///   <para><b>约束或前提</b>本方 <c>mDimension &lt; 1</c> 或实参维数不等于 <c>mDimension - 1</c> 抛 "Vector dimension mismatch"；<c>index &lt; 0</c> 抛 "Index out of range"。插入的是深拷副本，实参之后可安全释放。</para>
	///   <para><b>与相邻算子的取舍</b>整段接在尾部用 <c>Append</c>；覆盖已有某格用索引器赋值（它同样会先释放被顶替的旧元素）。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlVector v = new JlTupleVector(1);
	///   JlTupleVector leaf = new JlTupleVector(new JlTuple(7));   // 0 维叶
	///   v.Insert(0, leaf);
	///   int n = v.Length;   // 1
	///   v.Dispose();
	///   leaf.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>在空向量上 <c>Insert(4, leaf)</c> 得到的是长度 5：前 4 格是默认空向量（元组向量里含空元组，对象向量里含 <c>GenEmptyObj</c> 出来的空对象元组），不报错也不返回 null——静默补位正是错下标的典型症状。</para>
	/// </remarks>
	public JlVector Insert(int index, JlVector vector)
	{
		InsertImpl(index, vector, clone: true);
		return this;
	}

	/// <summary>插入的框架内部口：clone 决定插入的是实参的深拷副本，还是与实参共享的同一实例。</summary>
	/// <param name="index">插入位置，0 基，规则与 <c>Insert(int, JlVector)</c> 相同。</param>
	/// <param name="vector">被插入的子向量，维数须为本方 <c>Dimension - 1</c>。</param>
	/// <param name="clone">true 存 <c>vector.Clone()</c>；false 直接把 <c>vector</c> 实例挂进容器。</param>
	/// <returns><c>this</c>（原地改写）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>方法体是 <c>InsertImpl(index, vector, clone); return this;</c>，分叉点在 <c>mVector.Insert(index, clone ? vector.Clone() : vector)</c>。</para>
	///   <para><b>约束或前提</b>带 <c>[EditorBrowsable(EditorBrowsableState.Never)]</c>，供框架零拷贝搬运原生输出。维数与下标的两条异常与无 bool 版一字不差。</para>
	///   <para><b>与相邻算子的取舍</b>确认实参之后不再单独持有该槽位时才用 <c>clone: false</c>；只想在尾部加一格，索引器赋值比 Insert 更直观。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector v = new JlTupleVector(1);
	///   JlTupleVector item = new JlTupleVector(new JlTuple(3, 4));
	///   v.Insert(0, item, clone: true);
	///   int n = v.Length;   // 1
	///   v.Dispose();
	///   item.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><c>clone: false</c> 会让同一个子向量同时挂在两个容器里，任一方 <c>Dispose</c>/<c>Clear</c>/<c>Remove</c> 该格后另一方即持有悬垂引用。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlVector Insert(int index, JlVector vector, bool clone)
	{
		InsertImpl(index, vector, clone);
		return this;
	}

	/// <summary>Remove 的实现钩子：本方为叶抛异常，index 落在合法区间时锁内先 Dispose 该子向量再 RemoveAt，越界静默无操作。</summary>
	protected void RemoveImpl(int index)
	{
		if (mDimension < 1)
		{
			throw new JlVectorAccessException("Vector dimension mismatch");
		}
		if (index >= 0 && index < Length)
		{
			lock (mVector)
			{
				mVector[index].Dispose();
				mVector.RemoveAt(index);
			}
		}
	}

	/// <summary>删掉并释放 0 基下标 index 处的子向量，返回 this；下标越界时静默什么都不做。</summary>
	/// <param name="index">要删除的下标，0 基；越界既不报错也不删。</param>
	/// <returns><c>this</c>（原地改写），便于链式连续删除。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转 <c>RemoveImpl</c>：只有 <c>mDimension &lt; 1</c> 抛 "Vector dimension mismatch"；命中写作 <c>if (index &gt;= 0 &amp;&amp; index &lt; Length)</c>，锁内先 <c>mVector[index].Dispose()</c> 再 <c>RemoveAt(index)</c>。</para>
	///   <para><b>约束或前提</b>负数、过大一律静默返回，长度不变——这与索引器（静默扩容）、<c>At</c>（抛异常）构成三种不同的越界行为。删除使后续元素下标整体前移，倒序删除才不错位。</para>
	///   <para><b>与相邻算子的取舍</b>全清用 <c>Clear()</c>；要"取出并拥有"某格先 <c>At(i).Clone()</c> 再 <c>Remove(i)</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlVector v = new JlTupleVector(new JlTuple(1, 2, 3), 1);
	///   int before = v.Length;   // 3
	///   v.Remove(0);
	///   int after = v.Length;    // 2
	///   v.Remove(99);            // 越界：静默无操作
	///   v.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>被删元素当场释放，之前经索引器或 <c>At</c> 取出的同一槽引用立即悬垂；想确认删除是否发生只能自己比 <c>Length</c>。</para>
	/// </remarks>
	public JlVector Remove(int index)
	{
		RemoveImpl(index);
		return this;
	}

	/// <summary>Clear 的虚钩子，由 Clear 转调：本方为叶抛异常，否则锁内逐个 Dispose 本层全部子向量（递归到叶）后清空列表。</summary>
	protected virtual void ClearImpl()
	{
		if (mDimension < 1)
		{
			throw new JlVectorAccessException("Vector dimension mismatch");
		}
		lock (mVector)
		{
			for (int i = 0; i < Length; i++)
			{
				mVector[i].Dispose();
			}
			mVector.Clear();
		}
	}

	/// <summary>逐个释放本层全部子向量并把长度清零，返回 this；维数不变。</summary>
	/// <returns><c>this</c>（原地清空，容器可继续复用）。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>转虚方法 <c>ClearImpl</c>：<c>mDimension &lt; 1</c> 抛 "Vector dimension mismatch"；否则在 <c>lock (mVector)</c> 内对 0 到 <c>Length - 1</c> 逐个 <c>mVector[i].Dispose()</c>（递归释放整棵子树）后 <c>mVector.Clear()</c>。</para>
	///   <para><b>约束或前提</b>叶向量没有元素可清，调用它是抛异常而不是静默操作。清空后 <c>Dimension</c> 保持原值，仍可 <c>Append</c>/<c>Insert</c> 复用。</para>
	///   <para><b>与相邻算子的取舍</b>只去一两格用 <c>Remove(index)</c>；<c>Dispose()</c> 对 <c>Dimension &gt; 0</c> 的向量本来就等价于 Clear。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlVector v = new JlTupleVector(new JlTuple(1, 2, 3, 4), 2);
	///   v.Clear();
	///   int n = v.Length;      // 0
	///   int d = v.Dimension;   // 仍是 1
	///   v.Dispose();
	///   </code>
	///   <para><b>资源与坑</b><c>List.Clear</c> 保留容量，托管数组内存不立即归还；清空后一切指向旧元素的内部引用都已被释放，不可再用。</para>
	/// </remarks>
	public JlVector Clear()
	{
		ClearImpl();
		return this;
	}

	/// <summary>抽象克隆钩子：Clone 与显式 ICloneable.Clone 均转调它，由派生类返回一个与本向量结构独立的新向量。</summary>
	protected abstract JlVector CloneImpl();

	object ICloneable.Clone()
	{
		return CloneImpl();
	}

	/// <summary>转调抽象 <c>CloneImpl</c> 取得整棵向量的独立副本，返回静态类型仍是 <c>JlVector</c>。</summary>
	/// <returns>派生类新建的副本（维数与长度一致），所有权归调用方，须自行 <c>Dispose</c>。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>方法体只有 <c>return CloneImpl();</c>，而 <c>CloneImpl</c> 是抽象方法：元组向量实现为 <c>new JlTupleVector(this)</c>、对象向量实现为 <c>new JlObjectVector(this)</c>，两者都靠基类拷贝构造逐层 <c>vector[i].Clone()</c>。显式接口 <c>ICloneable.Clone()</c> 走同一条路但返回 <c>object</c>。</para>
	///   <para><b>约束或前提</b>带 <c>[EditorBrowsable(EditorBrowsableState.Never)]</c>；两个派生类各用 <c>new</c> 把它收回为强类型版本，按派生静态类型调用无需强转。</para>
	///   <para><b>与相邻算子的取舍</b>只要同内容的新容器用本方法；想把向量摊平取数据用 <c>ConvertVectorToTuple()</c>（元组向量独有）；只读遍历别拷。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlVector src = new JlTupleVector(new JlTuple(1, 2, 3), 1);
	///   JlVector copy = src.Clone();
	///   int same = copy.Length;   // 1
	///   src.Dispose();
	///   copy.Dispose();           // 深拷过，源释放不影响副本
	///   </code>
	///   <para><b>资源与坑</b>元组向量连元组数据一起拷；对象向量只拷结构，叶上的 <c>JlObject</c> 是 <c>CopyObject</c> 引用计数浅拷，底层对象数据仍共享——两族"独立"程度不同。</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public JlVector Clone()
	{
		return CloneImpl();
	}

	/// <summary>Dispose 在 Dimension 为 0 的分支上调用的钩子：释放本向量在叶上持有的那个原生对象，基类实现为空，由派生类各自覆写。</summary>
	protected virtual void DisposeLeafObject()
	{
	}

	/// <summary>释放向量持有的元素：Dimension 大于 0 时等价于 Clear，为 0 时只放手叶上那一个元组或对象句柄。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>先 <c>GC.SuppressFinalize(this)</c>，再按维数分派：<c>mDimension &gt; 0</c> 走 <c>Clear()</c>（逐个 <c>Dispose</c> 子向量并递归到叶）；否则调虚方法 <c>DisposeLeafObject()</c>——基类实现是空的，元组向量在其中 <c>mTuple.Dispose()</c>，对象向量在其中 <c>mObject.Dispose()</c>。实现"维数大于 0 时与 clear 完全一致"与实现相符。</para>
	///   <para><b>约束或前提</b>只释放元素与叶上载荷，<c>mVector</c> 这个 List 容器仍在（清空但可用），且没有任何"已释放"标志位。<c>Dimension</c> 为 0 的向量释放的是它内部那个元组/对象，之后 <c>T</c>/<c>O</c> 指向已释放对象。</para>
	///   <para><b>与相邻算子的取舍</b>还想继续用这个容器就 <c>Clear()</c>（语义相同但不碰叶分支）；只想去一格用 <c>Remove</c>；想让元素活得更久先 <c>Clone()</c> 或 <c>At(i).Clone()</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTupleVector v = new JlTupleVector(new JlTuple(1, 2), 1);
	///   v.Dispose();
	///   int left = v.Length;   // 0：容器仍在，可继续 Append
	///   v.Dispose();           // 重复调用安全，List 已空
	///   </code>
	///   <para><b>资源与坑</b>索引器与 <c>At</c> 交出的都是内部引用，<c>Dispose</c> 之后它们一律悬垂；本类未定义析构函数，<c>SuppressFinalize</c> 只是形式动作 [待实测]；由 <c>using</c> 或 <c>finally</c> 保证调用，别指望垃圾回收替你释放原生句柄。</para>
	/// </remarks>
	public void Dispose()
	{
		GC.SuppressFinalize(this);
		if (mDimension > 0)
		{
			Clear();
		}
		else
		{
			DisposeLeafObject();
		}
	}

	/// <summary>调试用文本：叶向量返回空串，1 维以上返回花括号包裹、逗号分隔的逐元素结果。</summary>
	/// <returns>形如 <c>{a, b}</c> 的字符串，元素部分由各自的 <c>ToString</c> 递归给出。</returns>
	/// <remarks>
	///   <para><b>功能说明</b>先 <c>if (mDimension &lt;= 0) return "";</c>，否则用 <c>StringBuilder</c> 拼 <c>"{"</c>、以 <c>", "</c> 分隔的 <c>this[i].ToString()</c>、<c>"}"</c>。叶层文本由派生类覆写决定：元组叶给元组内容，对象叶只给句柄的 <c>Key</c> 数字。</para>
	///   <para><b>约束或前提</b>遍历用的是索引器，但 <c>Length</c> 在循环里逐次取，若期间被别的线程扩容则输出格数会跟着变；纯托管拼串，无原生调用。</para>
	///   <para><b>与相邻算子的取舍</b>要拿数据走 <c>ConvertVectorToTuple()</c>（元组向量）或 <c>T</c>/<c>O</c>；要判有无元素用 <c>Length</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlVector row = new JlTupleVector(new JlTuple(1, 2, 3, 4), 2);
	///   string dbg = row.ToString();   // 形如 {1, 2, 3, 4}：每格是元组文本，外层再加花括号
	///   row.Dispose();
	///   </code>
	///   <para><b>资源与坑</b>文本不含维数与类型标注，多层嵌套时只有花括号层数可辨；不要用于序列化、日志结构化解析或跨版本比对。</para>
	/// </remarks>
	public override string ToString()
	{
		if (mDimension <= 0)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("{");
		for (int i = 0; i < Length; i++)
		{
			if (i != 0)
			{
				stringBuilder.Append(", ");
			}
			stringBuilder.Append(this[i].ToString());
		}
		stringBuilder.Append("}");
		return stringBuilder.ToString();
	}
}
