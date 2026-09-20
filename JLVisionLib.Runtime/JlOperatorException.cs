using System;
using System.ComponentModel;

namespace JLVisionLib;

/// <summary>
/// 原生算子调用失败时抛出的异常，是库对外最主要的异常类型。派生自 <see cref="T:JLVisionLib.JlException"/>。
/// </summary>
/// <remarks>
///   <para><b>与基类的差异</b>本类的 <see cref="M:JLVisionLib.JlOperatorException.GetErrorMessage"/> 与 <c>GetErrorText</c>
///   会按错误码去查原生消息表，而不是原样返回构造文本，因此同一错误码总能拿到一致的说明。</para>
///   <para><b>何时抛出</b>当算子返回的码不等于 2(<c>Jl_MSG_OK</c>) 时，由 <see cref="M:JLVisionLib.JlOperatorException.throwOperator(System.Int32,System.String)"/>
///   等入口抛出；调用方用 <c>catch (JlException ex)</c> 即可捕获（本类是其派生类）。</para>
/// </remarks>
public class JlOperatorException : JlException
{
	/// <summary>用错误码、可选说明与底层异常构造 <see cref="T:JLVisionLib.JlOperatorException"/>。</summary>
	/// <param name="err">算子返回的错误码（取自 <c>JlErrorDef</c>）；等于 2 通常不会成异常。</param>
	/// <param name="sInfo">说明文本。传空串时改由 <c>err</c> 去原生消息表自动填充；非空则原样采用。</param>
	/// <param name="inner">底层异常，可为 null。</param>
	/// <remarks>
	///   <para><b>功能说明</b>把错误码与说明转交基类构造；说明为空串时用 <c>JlNativeApi.GetErrorMessage(err)</c> 补一条与错误码对应的一致文本，避免抛出无说明的异常。</para>
	/// </remarks>
	public JlOperatorException(int err, string sInfo, Exception inner)
		: base(err, (sInfo == "") ? JlNativeApi.GetErrorMessage(err) : sInfo, inner)
	{
	}

	/// <summary>用错误码与说明构造 <see cref="T:JLVisionLib.JlOperatorException"/>，无底层异常。</summary>
	/// <param name="err">算子错误码。</param>
	/// <param name="sInfo">说明文本，空串时按 <paramref name="err"/> 自动查原生消息。</param>
	/// <remarks>
	///   <para><b>功能说明</b>以 <c>inner</c> 为 null 委托到三参构造器，是最常用的抛出入口之一。</para>
	/// </remarks>
	public JlOperatorException(int err, string sInfo)
		: this(err, sInfo, null)
	{
	}

	/// <summary>只给错误码构造 <see cref="T:JLVisionLib.JlOperatorException"/>，说明留空由错误码自动查得。</summary>
	/// <param name="err">算子错误码。</param>
	/// <remarks>
	///   <para><b>功能说明</b>以空串作说明委托到 <see cref="M:JLVisionLib.JlOperatorException.#ctor(System.Int32,System.String)"/>，构造时 <c>JlNativeApi.GetErrorMessage(err)</c> 会据码填充说明，适合手上只有错误码、没有额外上下文的场景。</para>
	/// </remarks>
	public JlOperatorException(int err)
		: this(err, "")
	{
	}

	/// <summary>已废弃：改用 <see cref="M:JLVisionLib.JlOperatorException.GetErrorMessage"/>。按错误码返回原生消息文本。</summary>
	[Obsolete("GetErrorText is deprecated, please use GetErrorMessage instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public new string GetErrorText()
	{
		return JlNativeApi.GetErrorMessage(GetErrorCode());
	}

	/// <summary>按当前错误码返回对应的原生消息文本。</summary>
	/// <returns>由 <c>GetErrorCode()</c> 查原生消息表得到的文本；与基类 <see cref="M:JLVisionLib.JlException.GetErrorMessage"/> 返回的构造文本可能不同。</returns>
	/// <remarks>
	///   <para><b>隐藏基类实现</b>本方法用 <c>new</c> 隐藏基类版本：不返回 <c>Message</c>，而是每次实时按错误码查原生消息表，保证文本与库版本一致。</para>
	///   <para><b>注意</b>经基类 <see cref="T:JLVisionLib.JlException"/> 引用调用时走的是基类实现（返回 <c>Message</c>），要拿原生文本需以 <see cref="T:JLVisionLib.JlOperatorException"/> 类型接收异常。</para>
	/// </remarks>
	public new string GetErrorMessage()
	{
		return JlNativeApi.GetErrorMessage(GetErrorCode());
	}

	/// <summary>取回扩展错误码（细粒度子码）。</summary>
	/// <returns>本托管运行时不生成扩展错误码，恒返回 <c>0</c>。</returns>
	/// <remarks>
	///   <para><b>现状</b>该接口保留自上层脚本/原生环境的扩展码概念；在当前库中未被填充，调用只会拿到 0，判断分支请勿依赖它。</para>
	/// </remarks>
	public long GetExtendedErrorCode()
	{
		return 0L;
	}

	/// <summary>取回扩展错误说明（对应 <see cref="M:JLVisionLib.JlOperatorException.GetExtendedErrorCode"/> 的文本）。</summary>
	/// <returns>本托管运行时不生成扩展消息，恒返回空字符串 <c>""</c>。</returns>
	/// <remarks>
	///   <para><b>现状</b>与 <see cref="M:JLVisionLib.JlOperatorException.GetExtendedErrorCode"/> 同为保留接口，当前库不会填充，请勿依赖其内容做提示。</para>
	/// </remarks>
	public string GetExtendedErrorMessage()
	{
		return "";
	}

	/// <summary>检查算子返回码，失败则抛出带算子名的 <see cref="T:JLVisionLib.JlOperatorException"/>；成功则静默返回。</summary>
	/// <param name="err">原生算子返回码。等于 2(<c>Jl_MSG_OK</c>) 视为成功、不抛异常。</param>
	/// <param name="logicalName">算子逻辑名，会被拼进异常消息（"… in operator &lt;name&gt;"）便于定位出错算子。</param>
	/// <remarks>
	///   <para><b>功能说明</b>失败时消息为原生错误文本加算子名；成功时什么都不做，因此可安全地"每步调用后调一次"而不影响正常流程。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   int err = 5; // 上一步原生调用的返回码
	///   JlOperatorException.throwOperator(err, "read_image");
	///   </code>
	/// </remarks>
	public static void throwOperator(int err, string logicalName)
	{
		if (JlNativeApi.IsFailure(err))
		{
			throw new JlOperatorException(err, JlNativeApi.GetErrorMessage(err) + " in operator " + logicalName);
		}
	}

	internal static void throwOperator(int err, int procIndex)
	{
		if (JlNativeApi.IsFailure(err))
		{
			string logicalName = JlNativeApi.GetLogicalName(procIndex);
			throw new JlOperatorException(err, JlNativeApi.GetErrorMessage(err) + " in operator " + logicalName);
		}
	}

	/// <summary>无条件抛出一条带自定义上下文的 <see cref="T:JLVisionLib.JlOperatorException"/>。</summary>
	/// <param name="err">要携带的错误码，原生消息会据它自动查得并附在后面。</param>
	/// <param name="sInfo">调用方补充的上下文说明，置于原生消息之前。</param>
	/// <remarks>
	///   <para><b>功能说明</b>与 <see cref="M:JLVisionLib.JlOperatorException.throwOperator(System.Int32,System.String)"/> 不同，本方法不看返回码是否成功、总是抛出，用于"确定已出错、只是要加一句人话"的场景。最终消息形如 <c>sInfo:\n&lt;原生消息&gt;\n</c>。</para>
	///   <para><b>用法</b></para>
	///   <code>
	///   int err = 5; // 已确认失败的原生返回码
	///   JlOperatorException.throwInfo(err, "读取标定文件失败");
	///   </code>
	/// </remarks>
	public static void throwInfo(int err, string sInfo)
	{
		throw new JlOperatorException(err, sInfo + ":\n" + JlNativeApi.GetErrorMessage(err) + "\n");
	}
}
