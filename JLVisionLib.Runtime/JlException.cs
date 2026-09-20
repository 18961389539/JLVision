using System;
using System.ComponentModel;

namespace JLVisionLib;

/// <summary>
/// 视觉库运算失败的异常基类。携带一个整型错误码（取自 <c>JlErrorDef</c> 常量）、
/// 一段人类可读说明，以及可选的底层异常与用户自定义数据元组。
/// </summary>
/// <remarks>
///   <para><b>与错误码的关系</b>错误码 2(<c>Jl_MSG_OK</c>) 代表成功、不会成为异常；
///   30000 及以上为上层用户主动抛出的自定义异常，其消息在元组序列化时不被原生文本覆盖。</para>
///   <para><b>继承关系</b>原生算子失败时抛出的是派生类 <see cref="T:JLVisionLib.JlOperatorException"/>；
///   本类多用于跨进程/序列化地搬运异常信息（配合 <see cref="M:JLVisionLib.JlException.ToHTuple(out JLVisionLib.JlTuple)"/> 与
///   <see cref="M:JLVisionLib.JlException.GetExceptionData(JLVisionLib.JlTuple,JLVisionLib.JlTuple,out JLVisionLib.JlTuple)"/>）。</para>
///   <para><b>捕获建议</b>调用方按 <c>catch (JlException ex)</c> 一网打尽即可，再用
///   <see cref="M:JLVisionLib.JlException.GetErrorCode"/> 判断类别、<see cref="M:JLVisionLib.JlException.GetErrorMessage"/> 取文本。</para>
/// </remarks>
public class JlException : ApplicationException
{
	private int err = 2;

	private JlTuple user_data;

	private const int ErrCodeUserException = 30000;

	/// <summary>用错误码、错误说明与底层异常三者构造 <see cref="T:JLVisionLib.JlException"/>，信息最全的入口。</summary>
	/// <param name="err">错误码，取值见 <c>JlErrorDef</c> 常量；等于 2 视为成功、30000 及以上为用户自定义异常。存入内部字段供 <see cref="M:JLVisionLib.JlException.GetErrorCode"/> 读取。</param>
	/// <param name="sInfo">人类可读的错误说明，交基类保存为 <c>Message</c>。</param>
	/// <param name="inner">导致本异常的底层异常，可为 null。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把 <paramref name="err"/> 写入内部错误码字段，说明文本与内部异常转交基类 <c>ApplicationException</c>。其余构造器最终都链到本重载。</para>
	///   <para><b>何时使用</b>需要同时携带错误码、说明与原始异常时用本重载；只给错误码时用 <see cref="M:JLVisionLib.JlException.#ctor(System.Int32,System.String)"/>。调用方通常在 catch 中读取，而非自行构造。</para>
	/// </remarks>
	public JlException(int err, string sInfo, Exception inner)
		: this(sInfo, inner)
	{
		this.err = err;
	}

	/// <summary>只带错误码与说明构造 <see cref="T:JLVisionLib.JlException"/>，无底层异常。</summary>
	/// <param name="err">错误码，取值见 <c>JlErrorDef</c>；2 为成功、30000 及以上为用户自定义异常。</param>
	/// <param name="sInfo">人类可读说明，保存为 <c>Message</c>。</param>
	/// <remarks>
	///   <para><b>功能说明</b>以 <c>inner</c> 为 null 委托到三参构造器 <see cref="M:JLVisionLib.JlException.#ctor(System.Int32,System.String,System.Exception)"/>，适合没有可链接底层异常的场景。</para>
	/// </remarks>
	public JlException(int err, string sInfo)
		: this(err, sInfo, null)
	{
	}

	/// <summary>用说明文本与底层异常构造 <see cref="T:JLVisionLib.JlException"/>，不带原生错误码。</summary>
	/// <param name="sInfo">人类可读说明，保存为 <c>Message</c>。</param>
	/// <param name="inner">导致本异常的底层异常，可为 null。</param>
	/// <remarks>
	///   <para><b>功能说明</b>此重载未提供错误码，内部 <c>err</c> 被置为 <c>-1</c>，因此 <see cref="M:JLVisionLib.JlException.GetErrorCode"/> 返回 -1，<see cref="M:JLVisionLib.JlException.ToHTuple(out JLVisionLib.JlTuple)"/> 会把它当成非用户自定义码处理。</para>
	///   <para><b>何时使用</b>包装一个来自 .NET 侧、无对应视觉库错误码的异常时用本重载。</para>
	/// </remarks>
	public JlException(string sInfo, Exception inner)
		: base(sInfo, inner)
	{
		err = -1;
	}

	/// <summary>只用说明文本构造 <see cref="T:JLVisionLib.JlException"/>，错误码被置为 -1。</summary>
	/// <param name="sInfo">人类可读说明，保存为 <c>Message</c>。</param>
	/// <remarks>
	///   <para><b>功能说明</b>仅设置说明文本，内部 <c>err</c> 为 <c>-1</c>，无底层异常。适合快速抛出一条不带原生错误码的说明性异常。</para>
	/// </remarks>
	public JlException(string sInfo)
		: base(sInfo)
	{
		err = -1;
	}

	/// <summary>默认构造：错误码置为 -1，说明文本为空。</summary>
	/// <remarks>
	///   <para><b>功能说明</b>不设置说明与底层异常，<c>err</c> 为 <c>-1</c>。多用于反序列化或先构造后填充字段的场景；正常抛出建议改用带说明或带错误码的重载。</para>
	/// </remarks>
	public JlException()
	{
		err = -1;
	}

	/// <summary>从 <see cref="M:JLVisionLib.JlException.ToHTuple(out JLVisionLib.JlTuple)"/> 生成的异常元组反解析出 <see cref="T:JLVisionLib.JlException"/>。</summary>
	/// <param name="tuple">异常元组：<c>tuple[0]</c> 为整型错误码，<c>tuple[1]</c> 为错误说明，其后为用户自定义数据。</param>
	/// <remarks>
	///   <para><b>功能说明</b>读取 <c>tuple[0]</c> 作错误码、<c>tuple[1]</c> 作说明构造异常；随后把剩余元素作为 <c>user_data</c> 保存。</para>
	///   <para><b>偏移约定</b>用户自定义码(&gt;=30000)在元组里没有独立的消息槽，故用户数据从索引 1 起；普通码索引 1 已被消息占用，用户数据从索引 2 起。这与 <see cref="M:JLVisionLib.JlException.ToHTuple(out JLVisionLib.JlTuple)"/> 的写入序严格对应。</para>
	/// </remarks>
	public JlException(JlTuple tuple)
		: this(tuple[0], tuple[1].O.ToString())
	{
		int num = 2;
		if (err >= 30000)
		{
			num = 1;
		}
		if (num <= tuple.TupleLength() - 1)
		{
			user_data = tuple.TupleSelectRange(num, tuple.TupleLength() - 1);
		}
	}

	/// <summary>已废弃：改用 <see cref="M:JLVisionLib.JlException.GetErrorCode"/>。返回同样的整型错误码。</summary>
	[Obsolete("GetErrorNumber is deprecated, please use GetErrorCode instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public int GetErrorNumber()
	{
		return err;
	}

	/// <summary>取回本异常携带的整型错误码。</summary>
	/// <returns>构造时写入的错误码；纯文本构造时为 -1，取自 <c>JlErrorDef</c> 常量的码为对应正整数。</returns>
	/// <remarks>
	///   <para><b>用法</b>在 catch 块里用它区分异常类别：等于 2 不会成为异常；&gt;=30000 表示用户主动抛出的自定义异常；其余为库/原生算子错误码。</para>
	///   <para><b>取代关系</b>本方法取代已废弃的 <c>GetErrorNumber</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   try
	///   {
	///       // 调用可能抛出 JlException 的算子
	///   }
	///   catch (JlException ex)
	///   {
	///       int code = ex.GetErrorCode();
	///   }
	///   </code>
	/// </remarks>
	public int GetErrorCode()
	{
		return err;
	}

	/// <summary>把本异常序列化为一个 <see cref="T:JLVisionLib.JlTuple"/>，用于跨边界传递或再喂给 <see cref="M:JLVisionLib.JlException.GetExceptionData(JLVisionLib.JlTuple,JLVisionLib.JlTuple,out JLVisionLib.JlTuple)"/>。</summary>
	/// <param name="exception">输出的异常元组：新分配的 <see cref="T:JLVisionLib.JlTuple"/>。</param>
	/// <remarks>
	///   <para><b>元组布局</b>索引 0 恒为错误码；当错误码 &lt; 30000（非用户自定义）时索引 1 放错误说明文本；若存在 <c>user_data</c> 再拼接到末尾。</para>
	///   <para><b>对称性</b>写入序与 <see cref="M:JLVisionLib.JlException.#ctor(JLVisionLib.JlTuple)"/> 的读取序严格一致，二者互逆。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   try
	///   {
	///       // 调用可能抛出 JlException 的算子
	///   }
	///   catch (JlException ex)
	///   {
	///       ex.ToHTuple(out JlTuple exc);
	///   }
	///   </code>
	/// </remarks>
	public void ToHTuple(out JlTuple exception)
	{
		exception = new JlTuple();
		exception[0] = GetErrorCode();
		if ((long)GetErrorCode() < 30000L)
		{
			exception[1] = GetErrorMessage();
		}
		if (user_data != null)
		{
			exception = exception.TupleConcat(user_data);
		}
	}

	/// <summary>从 <see cref="M:JLVisionLib.JlException.ToHTuple(out JLVisionLib.JlTuple)"/> 生成的异常元组中按名称提取一个或多个字段。</summary>
	/// <param name="exception">待解析的异常元组（通常先经 <see cref="M:JLVisionLib.JlException.GetErrorCode"/> 判过类别）。</param>
	/// <param name="name">要读取的槽名元组，按给定顺序逐个取值；元素必须为字符串，否则抛 <see cref="T:JLVisionLib.JlOperatorException"/>。</param>
	/// <param name="value">输出：把各槽取到的值按请求顺序拼接成的新元组。</param>
	/// <remarks>
	///   <para><b>可用槽名</b><c>error_code</c>、<c>error_msg</c>(别名 <c>error_message</c>)、<c>user_data</c>、<c>add_error_code</c>、<c>add_error_msg</c>(别名 <c>add_error_message</c>)、<c>proc_line</c>(别名 <c>program_line</c>)、<c>operator</c>、<c>call_stack_depth</c>、<c>procedure</c>。传其它名字抛 <see cref="T:JLVisionLib.JlOperatorException"/>。</para>
	///   <para><b>解析规则</b><c>error_code</c> 取元组索引 0；<c>error_msg</c> 取索引 1。当错误码 &gt;=30000（用户自定义异常）时，文本槽返回固定串 "User defined exception" 而非元组内容。<c>user_data</c> 返回错误码与消息之后的全部元素，且只能单独请求，混入其它槽会抛 <see cref="T:JLVisionLib.JlOperatorException"/>。</para>
	///   <para><b>本运行时的空槽</b><c>add_error_*</c>、<c>proc_line</c>、<c>operator</c>、<c>call_stack_depth</c>、<c>procedure</c> 等由 HDevelop/上层脚本填充，托管侧未写入，取到的是空串 <c>""</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   JlTuple exc = new JlTuple();
	///   try
	///   {
	///       // 调用可能抛出 JlException 的算子
	///   }
	///   catch (JlException ex)
	///   {
	///       ex.ToHTuple(out exc);
	///   }
	///   JlException.GetExceptionData(exc, new JlTuple("error_code"), out JlTuple value);
	///   </code>
	/// </remarks>
	public static void GetExceptionData(JlTuple exception, JlTuple name, out JlTuple value)
	{
		value = new JlTuple();
		bool flag = exception.TupleLength() > 0 && exception[0].Type == JlTupleType.INTEGER && exception[0].I >= 30000;
		int num = name.TupleLength();
		for (int i = 0; i < num; i++)
		{
			if (name[i].Type != JlTupleType.STRING)
			{
				throw new JlOperatorException(0, "JlOperatorException.GetExceptionData(): wrong type of input parameter 'name'.");
			}
			int num2;
			switch (name[i].S)
			{
			case "error_code":
				num2 = 0;
				goto IL_01a5;
			case "add_error_code":
				num2 = -1;
				goto IL_01a5;
			case "user_data":
				if (num != 1)
				{
					value = new JlTuple();
					throw new JlOperatorException(0, "JlOperatorException.GetExceptionData(): slot 'user_data' onparameter 'Name' cannot be requested together with other slots.");
				}
				num2 = (flag ? 1 : 2);
				if (num2 <= exception.TupleLength() - 1)
				{
					value = value.TupleConcat(exception.TupleSelectRange(num2, exception.TupleLength() - 1));
				}
				return;
			case "error_msg":
			case "error_message":
				num2 = 1;
				goto IL_01a5;
			case "add_error_msg":
			case "add_error_message":
				num2 = -1;
				goto IL_01a5;
			case "proc_line":
			case "program_line":
				num2 = -1;
				goto IL_01a5;
			case "operator":
				num2 = -1;
				goto IL_01a5;
			case "call_stack_depth":
				num2 = -1;
				goto IL_01a5;
			case "procedure":
				num2 = -1;
				goto IL_01a5;
			default:
				{
					value = new JlTuple();
					throw new JlOperatorException(0, "JlOperatorException.GetExceptionData(): wrong value of input parameter 'name'.");
				}
				IL_01a5:
				if (num2 == -1)
				{
					value = value.TupleConcat("");
				}
				else if (flag && num2 != 0)
				{
					value = value.TupleConcat("User defined exception");
				}
				else
				{
					value = value.TupleConcat(exception[num2]);
				}
				break;
			}
		}
	}

	/// <summary>已废弃：改用 <see cref="M:JLVisionLib.JlException.GetErrorMessage"/>。返回同样的说明文本。</summary>
	[Obsolete("GetErrorText is deprecated, please use GetErrorMessage instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public string GetErrorText()
	{
		return Message;
	}

	/// <summary>取回本异常的说明文本。</summary>
	/// <returns>构造时写入的 <c>Message</c>；用无说明的重载时为空字符串。</returns>
	/// <remarks>
	///   <para><b>与派生类的差异</b>基类原样返回构造时的 <c>Message</c>；<see cref="T:JLVisionLib.JlOperatorException"/> 重写此方法改为按错误码查原生消息表，二者对同一异常可能给出不同文本。</para>
	///   <para><b>取代关系</b>取代已废弃的 <c>GetErrorText</c>。</para>
	/// </remarks>
	public string GetErrorMessage()
	{
		return Message;
	}
}
