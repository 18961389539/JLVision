namespace JLVisionLib;

/// <summary>
///   原生错误/状态码常量的静态容器：集中声明各返回码（<c>Jl_MSG_*</c> 状态小码段及 1000/1201 起的错误族），供全库逐码比较与统一失败判定使用；其语义只以"成功 == 2"（<c>Jl_MSG_OK</c>）为准。本类只承载常量、无实例状态，不参与句柄或图标对象的资源管理。
/// </summary>
public class JlErrorDef
{
	/// <summary>算子调用成功返回的状态码（取值 2），全库唯一的"成功"判据。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生侧每次算子调用都回一个状态码，等于 2 表示该调用正常完成、输出已可用。托管运行时只看这一个值：<c>JlNativeApi.IsFailure(err)</c> 仅在 <c>err == 2</c> 时为 false，故 <see cref="M:JLVisionLib.JlOperatorException.throwOperator(System.Int32,System.String)"/> 也只有遇到本码时不抛异常。</para>
	///   <para><b>归类</b>它小于 1000，<c>JlNativeApi.IsError(err)</c>（判据为 <c>err &gt;= 1000</c>）同样不把它算作错误，两个判据在 2 上结论一致。</para>
	///   <para><b>别名</b>取值与 <c>Jl_MSG_TRUE</c> 相同（别名，同为 2）：原生把"成功"与布尔"真"复用了同一个码，拿到 2 时无法区分是流程成功还是某布尔查询为真，须由调用的算子自己决定语义。</para>
	///   <para><b>处置</b>无需处置。惯用法是 <c>err == JlErrorDef.Jl_MSG_OK</c> 逐码比较；本库常规路径已在原生调用处统一检查，业务代码一般只需 <c>catch (JlException)</c>。</para>
	/// </remarks>
	public const int Jl_MSG_OK = 2;

	/// <summary>布尔性查询结果为"真"时返回的状态码（取值 2）。</summary>
	/// <remarks>
	///   <para><b>含义</b>布尔性查询类算子（回答"是否…"的那一类）把"是"编码成 2、"否"编码成 <c>Jl_MSG_FALSE</c>=3，而不是另造一套码；本仓库托管层未提供这类布尔算子的包装，具体哪些算子回布尔码需以原生算子表为准 [待实测]。</para>
	///   <para><b>别名</b>取值与 <c>Jl_MSG_OK</c> 相同（别名，同为 2）。因此 2 有双重身份：既是成功也是"真"，仅凭返回码无法区分，必须由调用处已知语义来判断；这也意味着布尔查询为"假"时拿到的是 3，而托管侧 <c>JlNativeApi.IsFailure</c> 只看是否等于 2，会把"假"当失败处理。</para>
	///   <para><b>处置</b>做布尔判断时写成 <c>err == JlErrorDef.Jl_MSG_TRUE</c> 或 <c>!= Jl_MSG_FALSE</c> 都可以，但别把它当"调用成功"的证据——除非你调的确实是流程型算子。若希望"假"不抛异常，就不要把这类布尔算子接进统一的返回码检查路径。</para>
	/// </remarks>
	public const int Jl_MSG_TRUE = 2;

	/// <summary>布尔性查询结果为"假"时返回的状态码（取值 3）。</summary>
	/// <remarks>
	///   <para><b>含义</b>与 <c>Jl_MSG_TRUE</c>=2 成对使用：查询算子用 2 表示"是"、用本码表示"否"。它属于 2~5 的状态/讯息小码段，不是 1201 起的参数错误族。</para>
	///   <para><b>坑</b>两个判据对本码结论相反：<c>JlNativeApi.IsError(3)</c> 为 false（判据 <c>err &gt;= 1000</c>，3 不算错误），而 <c>JlNativeApi.IsFailure(3)</c> 为 true（判据"不等于 2 即失败"）。因此把返回布尔语义的算子送进 <see cref="M:JLVisionLib.JlOperatorException.throwOperator(System.Int32,System.String)"/> 一类的统一检查时，"结果为否"会被抛成 <c>JlOperatorException</c>，而不是拿到 false。</para>
	///   <para><b>处置</b>需要布尔语义时自己取回返回码比较 <c>err == JlErrorDef.Jl_MSG_FALSE</c>，不要依赖异常分支表达"否"；排查时也别把本码当算子出错来查参数。</para>
	/// </remarks>
	public const int Jl_MSG_FALSE = 3;

	/// <summary>要求停止后续处理、且不产生输出值的状态码（取值 4）。</summary>
	/// <remarks>
	///   <para><b>含义</b>常量名表示"void/无值"，内嵌的原生说明文本为 <c>Stop processing</c>（停止处理），两者指向同一件事：调用没有可交出的结果，并提示流程就此打住。它属于 2~5 的状态小码段，不是参数错误族。</para>
	///   <para><b>坑</b>与 <c>Jl_MSG_FALSE</c> 一样是"两判据不一致"的码：<c>JlNativeApi.IsError(4)</c> 为 false（不到 1000），<c>JlNativeApi.IsFailure(4)</c> 却为 true（不等于 2）。所以它会经 <c>throwOperator</c> 抛 <c>JlOperatorException</c>。托管层目前没有找到据本码主动中断算子流程的分支，"停止处理"由谁执行需实测 [待实测]。</para>
	///   <para><b>处置</b>看到本码先按"无结果、别继续往下算"处理：不要用未赋值的输出对象继续下游运算；若确属预期（如条件算子决定不再执行），应在自己的分支里判 <c>err == JlErrorDef.Jl_MSG_VOID</c> 提前返回，而不是放进统一检查里当异常抛出。</para>
	/// </remarks>
	public const int Jl_MSG_VOID = 4;

	/// <summary>笼统的"调用失败"状态码（取值 5），不携带任何具体失败原因。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生侧在未能归入更细码时回本码：与 <c>Jl_MSG_OK</c>=2 相对，表示这次调用没成。它与 2/3/4 同属小于 1000 的状态小码段，因而 <c>JlNativeApi.IsError(5)</c> 为 false（判据 <c>err &gt;= 1000</c>），但 <c>JlNativeApi.IsFailure(5)</c> 为 true，走统一检查时仍会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>何时遇到</b>参数与图像都合法但底层调用本身没走通（例如句柄/实例状态不对、被前置条件拒绝）时更常见；具体成因不在码里，须由 <c>JlNativeApi.GetErrorMessage(err)</c> 查原生消息表补出文本。</para>
	///   <para><b>处置</b>先取异常消息（<c>JlOperatorException.GetErrorMessage</c> 会实时按码查原生表），不要只看码就断定为参数问题；参数类问题会明确落在 1201/1301/1401 三个族段里。另注意本库多处示例注释直接写 <c>int err = 5;</c> 充当"上一步的失败返回码"占位，所以日志里的 5 有可能是照抄示例得到的，不一定是原生真发。</para>
	/// </remarks>
	public const int Jl_MSG_FAIL = 5;

	/// <summary>内部保留的中断状态码（取值 20），原生说明为 <c>for internal use</c>，不面向调用方。</summary>
	/// <remarks>
	///   <para><b>含义</b>本码与 21/22/23 同属"中断/取消"小族段：算子没跑完就被打断。原生明确标注为内部使用，含义随原生运行时版本可变，不保证长期稳定 [待实测]。</para>
	///   <para><b>归类坑</b>它小于 1000，故 <c>JlNativeApi.IsError(20)</c> 为 false（判据 <c>err &gt;= 1000</c>），而 <c>JlNativeApi.IsFailure(20)</c> 为 true（不等于 2 即失败）——统一检查会把它当失败抛出，但按族段看它并不是参数错误，不要去翻参数。</para>
	///   <para><b>处置</b>不要在业务分支里判本码、也不要为它写专属提示；真收到时按"运行时被中断"处理：重试该算子调用，并把码连同原生消息一起记录上报。</para>
	/// </remarks>
	public const int Jl_ERR_BREAK = 20;

	/// <summary>算子被开发环境引擎取消时返回的码（取值 21），原生文本为 <c>operator was canceled for dev engine</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>与 <c>Jl_ERR_CANCEL</c>=22 的区别在"由谁取消"：本码专指交互式开发引擎（调试/单步那类宿主环境）主动中止算子，22 则是通用取消。同属 20~23 的中断/取消族段。</para>
	///   <para><b>何时遇到</b>本仓库是随产线部署的托管运行时，未提供接入开发引擎的通道，因此正常运行中一般拿不到本码；若在客户现场看到，多半是原生核与某个带引擎的宿主混用所致 [待实测]。</para>
	///   <para><b>处置</b>当"非算法原因"看待：不算参数错（族段小于 1000，<c>JlNativeApi.IsError</c> 判 false，但 <c>IsFailure</c> 仍判失败并抛 <c>JlOperatorException</c>），排查方向是宿主/环境而不是算子设置。</para>
	/// </remarks>
	public const int Jl_ERR_ENGINE_CANCEL = 21;

	/// <summary>算子被通用取消时返回的码（取值 22），原生文本为 <c>operator was generally cancelled</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>不区分来源的"这次算子被取消了"，是取消族里的通用码：<c>Jl_ERR_ENGINE_CANCEL</c>=21 特指开发引擎取消，<c>Jl_ERR_TIMEOUT_BREAK</c>=23 特指超时打断，<c>Jl_ERR_BREAK</c>=20 为内部保留。</para>
	///   <para><b>何时遇到</b>本仓库托管层没有暴露任何取消/中止算子的接口（<c>JlOperatorSet.SetOperatorTimeout</c> 只做超时登记，无主动取消入口），因此本码只可能由原生核内部或带并发的调度路径产生；具体触发方 [待实测]。</para>
	///   <para><b>处置</b>按"结果未产出"处理：本码对应的调用其输出对象不可用，不要继续读输出。它小于 1000，<c>JlNativeApi.IsError</c> 判 false 但 <c>IsFailure</c> 判 true，所以会抛 <c>JlOperatorException</c>；重试即可，无需改参数。</para>
	/// </remarks>
	public const int Jl_ERR_CANCEL = 22;

	/// <summary>算子因超时被打断的状态码（取值 23）；常量名指向 timeout break，内嵌的原生文本只标为 <c>for internal use</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>两个来源都保留：按名字它是超时族的中断码——被 <c>JlOperatorSet.SetOperatorTimeout(operatorName, timeout, mode)</c>（原生算子 id 2008，超时时长单位秒、默认 1，处理模式默认 "cancel"）登记过的算子超过时限后被中止；按原生文本它又被标为内部使用，因此不建议作为稳定的对外契约 [待实测]。</para>
	///   <para><b>与相邻码区分</b>20 是内部中断、21/22 是取消（引擎/通用），本码专指超时，处置方向是把超时登记回滚或放宽，而不是改算法参数。</para>
	///   <para><b>处置</b>先看该算子是否被登记过超时：有则调大 <c>timeout</c> 或改 <c>mode</c>，无则说明是原生内部计时点触发，记录码与 <c>JlNativeApi.GetErrorMessage(err)</c> 文本上报。注意本码小于 1000，<c>JlNativeApi.IsError</c> 判 false，但 <c>IsFailure</c> 判 true，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_TIMEOUT_BREAK = 23;

	/// <summary>算子的第 1 个控制参数类型不对（取值 1201）。</summary>
	/// <remarks>
	///   <para><b>含义</b>本码是 1201~1220"控制参数类型"族的第一个：码 = 1200 + 控制参数序号，序号按原生算子参数表里的控制参数排列（从 1 起），与 C# 方法形参的排列不一定一致，因此定位时以"第几个控制参数"为准。</para>
	///   <para><b>何时遇到</b>该槽位要字符串标志（如 'true'/'false'、模式名）却传了数值，或要整数/浮点却传了字符串。托管层 <c>JlTuple</c> 有 int/double/string 的隐式转换，装进去不会报错，错配要等到原生调用时才以本码暴露出来。</para>
	///   <para><b>处置</b>对照该算子文档核对第 1 个控制参数的期望类型，重装参数后重试。本族只判类型：类型确认正确却仍失败，转查同序号的 1301（值不在允许范围）与 1401（值的个数不对）。本码 ≥1000，<c>JlNativeApi.IsError</c> 判 true，属真错误，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT1 = 1201;

	/// <summary>算子的第 2 个控制参数类型不对（取值 1202）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1201~1220 类型族成员，码 = 1200 + 序号，本码指原生参数表里第 2 个控制参数被给了错的类型；序号是控制参数序，不是 C# 形参序。</para>
	///   <para><b>何时遇到</b>常见于"数值参数在前、标志参数在后"的算子：把模式字符串写成数字，或把该传整数的槽位传成字符串元组。<c>JlTuple</c> 的隐式转换会掩盖它，直到原生调用才暴露。</para>
	///   <para><b>处置</b>核对第 2 个控制参数的期望类型并重装。类型无误却仍失败时改查 1302（值越界）与 1402（值个数不对）；本码 ≥1000，<c>JlNativeApi.IsError</c> 判 true，必须处理，不能当噪声忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT2 = 1202;

	/// <summary>算子的第 3 个控制参数类型不对（取值 1203）。</summary>
	/// <remarks>
	///   <para><b>含义</b>类型族（1201~1220）第 3 位：原生参数表中第 3 个控制参数的类型与传入不符，码 = 1200 + 3。</para>
	///   <para><b>何时遇到</b>三参以上的算子最容易踩到：调用方按 C# 形参序理解"第 3 个"，而原生按控制参数槽序计数，两者不一致时会报在错误的槽位上。</para>
	///   <para><b>处置</b>先确认该算子的控制参数序，再核对该槽位类型（字符串标志 / 整数 / 浮点 / 元组）后重装；若类型已对，转查 1303 与 1403。≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 一类的统一检查会据它抛异常。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT3 = 1203;

	/// <summary>算子的第 4 个控制参数类型不对（取值 1204）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1201~1220"控制参数类型"族成员，码 = 1200 + 序号，本码指原生参数表里第 4 个控制参数被给了与要求不符的类型；"第 4 个"按控制参数槽序计数，不等于 C# 方法的第 4 个形参。</para>
	///   <para><b>何时遇到</b>该槽位要字符串标志（模式名、'true'/'false' 之类）却传了数值，或要整数/浮点却传了字符串。托管层 <c>JlTuple</c> 对 int/double/string 都有隐式转换，装填阶段不报错，错配要等到原生调用时才以本码暴露。</para>
	///   <para><b>处置</b>核对第 4 个控制参数的期望类型再重装。类型确认无误却仍失败，说明问题不在类型，转查同序号的 <c>Jl_ERR_WIPV4</c>（取值不在允许范围）与 <c>Jl_ERR_WIPN4</c>（值的个数不对）。</para>
	///   <para><b>归类</b>本码 ≥1000，<c>JlNativeApi.IsError(err)</c> 判 true（判据 err &gt;= 1000），属真错误；统一检查里 <c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可当噪声忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT4 = 1204;

	/// <summary>算子的第 5 个控制参数类型不对（取值 1205）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1201~1220 类型族成员，码 = 1200 + 5，指原生参数表里第 5 个控制参数被给了错类型；计数按控制参数槽序，不是 C# 形参序。</para>
	///   <para><b>何时遇到</b>参数较多的算子里，调用方常把"标志字符串槽"与"数值槽"顺序记混。<c>JlTuple</c> 的隐式转换让装填看似成功，类型冲突到原生调用才以本码现形。</para>
	///   <para><b>处置</b>先确认该算子的控制参数序，再核对第 5 槽期望类型（字符串标志 / 整数 / 浮点 / 元组）后重装；类型无误仍失败转查 <c>Jl_ERR_WIPV5</c> 与 <c>Jl_ERR_WIPN5</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT5 = 1205;

	/// <summary>算子的第 6 个控制参数类型不对（取值 1206）。</summary>
	/// <remarks>
	///   <para><b>含义</b>类型族（1201~1220）第 6 位，码 = 1200 + 6：原生参数表里第 6 个控制参数的类型与传入不符；序号是控制参数序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>把该传整数/浮点的槽位传成字符串元组，或把模式标志写成数字。<c>JlTuple</c> 隐式转换会掩盖错配，直到原生调用才报本码。</para>
	///   <para><b>处置</b>先确认控制参数序，再核对第 6 槽期望类型后重装；类型已对则转查 <c>Jl_ERR_WIPV6</c>（值越界）与 <c>Jl_ERR_WIPN6</c>（值个数）。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 一类的统一检查会据它抛异常。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT6 = 1206;

	/// <summary>算子的第 7 个控制参数类型不对（取值 1207）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1201~1220"控制参数类型"族成员，码 = 1200 + 7：原生参数表第 7 个控制参数被给了错类型。序号按控制参数槽序计，不等于 C# 形参序。</para>
	///   <para><b>何时遇到</b>参数较多的算子里标志槽与数值槽易记混；<c>JlTuple</c> 的 int/double/string 隐式转换让错误在装填期不显形，直到原生调用回本码。</para>
	///   <para><b>处置</b>先定该算子的控制参数序，再核第 7 槽期望类型后重装；类型无误仍报本码转查 <c>Jl_ERR_WIPV7</c>、<c>Jl_ERR_WIPN7</c>。可用 <c>JlNativeApi.GetErrorMessage(err)</c> 取原生描述辅助定位。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），统一检查 <c>JlOperatorException.throwOperator</c> 会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT7 = 1207;

	/// <summary>算子的第 8 个控制参数类型不对（取值 1208）。</summary>
	/// <remarks>
	///   <para><b>含义</b>类型族（1201~1220）第 8 位，码 = 1200 + 8，指原生参数表第 8 个控制参数的类型与传入不符。"第 8 个"是控制参数序，不是 C# 形参序。</para>
	///   <para><b>何时遇到</b>该槽位要字符串标志却传数值、要数值却传字符串。装填阶段 <c>JlTuple</c> 隐式转换不拦，错配到原生调用才以本码暴露。</para>
	///   <para><b>处置</b>先确认控制参数序，再核第 8 槽期望类型后重装；类型确认无误转查 <c>Jl_ERR_WIPV8</c>（值越界）与 <c>Jl_ERR_WIPN8</c>（值个数不对）。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不能忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT8 = 1208;

	/// <summary>算子的第 9 个控制参数类型不对（取值 1209）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1201~1220 类型族成员，码 = 1200 + 9，指原生参数表第 9 个控制参数被给了错类型；序号是控制参数槽序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>控制参数较多的复合算子才可能命中到第 9 槽，通常是对该算子参数表理解有偏差、把某槽的类型要求记错。<c>JlTuple</c> 的隐式转换让错配延后到原生调用才暴露。</para>
	///   <para><b>处置</b>对照该算子的控制参数序核第 9 槽期望类型后重装；类型确认无误则转查 <c>Jl_ERR_WIPV9</c>、<c>Jl_ERR_WIPN9</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 一类的统一检查会据它抛异常。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT9 = 1209;

	/// <summary>算子的第 10 个控制参数类型不对（取值 1210）。</summary>
	/// <remarks>
	///   <para><b>含义</b>类型族（1201~1220）第 10 位，码 = 1200 + 10：原生参数表里第 10 个控制参数的类型与传入不符；序号按控制参数序计数。</para>
	///   <para><b>何时遇到</b>高序号意味着该算子至少有 10 个控制参数，多为封装了多组子选项的复合算子；把某一组的标志串与数值串顺序弄反时最易触发。<c>JlTuple</c> 隐式转换会掩盖到原生调用。</para>
	///   <para><b>处置</b>先核对该算子的控制参数序，再确认第 10 槽期望类型后重装；类型无误仍报本码转查 <c>Jl_ERR_WIPV10</c>、<c>Jl_ERR_WIPN10</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），统一检查 <c>JlOperatorException.throwOperator</c> 会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT10 = 1210;

	/// <summary>算子的第 11 个控制参数类型不对（取值 1211）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1201~1220"控制参数类型"族成员，码 = 1200 + 11：原生参数表第 11 个控制参数被给了与要求不符的类型；序号按控制参数序计，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>控制参数越靠后，越常见于把多组子选项一次性铺平时错位。<c>JlTuple</c> 对 int/double/string 的隐式转换让类型冲突延后到原生调用，以本码回抛。</para>
	///   <para><b>处置</b>先确认该算子的控制参数序，再核对第 11 槽期望类型（字符串标志 / 整数 / 浮点 / 元组）后重装；类型无误仍失败转查 <c>Jl_ERR_WIPV11</c>、<c>Jl_ERR_WIPN11</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT11 = 1211;

	/// <summary>算子的第 12 个控制参数类型不对（取值 1212）。</summary>
	/// <remarks>
	///   <para><b>含义</b>类型族（1201~1220）第 12 位，码 = 1200 + 12：原生参数表里第 12 个控制参数的类型与传入不符；计数按控制参数槽序，不是 C# 形参序。</para>
	///   <para><b>何时遇到</b>命中到第 12 槽的算子控制参数很多，易在某组子参数上把标志串写成数值；<c>JlTuple</c> 隐式转换到原生调用才以本码暴露错配。</para>
	///   <para><b>处置</b>先定该算子控制参数序，再核第 12 槽期望类型后重装；类型确认无误转查 <c>Jl_ERR_WIPV12</c>、<c>Jl_ERR_WIPN12</c>，或用 <c>JlNativeApi.GetErrorMessage(err)</c> 取文本辅助。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT12 = 1212;

	/// <summary>算子的第 13 个控制参数类型不对（取值 1213）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1201~1220 类型族成员，码 = 1200 + 13，指原生参数表第 13 个控制参数被给了错类型；序号是控制参数槽序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>只有控制参数极多的算子才会占用到第 13 槽，多半是把某组子选项的标志串写成数值。<c>JlTuple</c> 隐式转换不拦，错配到原生调用才以本码暴露。</para>
	///   <para><b>处置</b>先确认控制参数序，再核第 13 槽期望类型后重装；类型无误仍失败转查 <c>Jl_ERR_WIPV13</c>（值越界）与 <c>Jl_ERR_WIPN13</c>（值个数不对）。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT13 = 1213;

	/// <summary>算子的第 14 个控制参数类型不对（取值 1214）。</summary>
	/// <remarks>
	///   <para><b>含义</b>类型族（1201~1220）第 14 位，码 = 1200 + 14：原生参数表第 14 个控制参数类型与传入不符；序号按控制参数序计，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>能命中第 14 槽的算子控制参数很多，常在对齐多组子参数时把该传整数/浮点的槽位填成字符串元组；<c>JlTuple</c> 隐式转换延后暴露到原生调用。</para>
	///   <para><b>处置</b>先核该算子控制参数序，再确认第 14 槽期望类型后重装；类型无误仍报本码转查 <c>Jl_ERR_WIPV14</c>、<c>Jl_ERR_WIPN14</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT14 = 1214;

	/// <summary>算子的第 15 个控制参数类型不对（取值 1215）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1201~1220"控制参数类型"族成员，码 = 1200 + 15：原生参数表第 15 个控制参数被给了错类型。"第 15 个"是控制参数槽序，不是 C# 形参序。</para>
	///   <para><b>何时遇到</b>高序号槽多见于封装大量可选项的算子，铺平时把标志串与数值串错位；<c>JlTuple</c> 隐式转换让错配到原生调用才回本码。</para>
	///   <para><b>处置</b>先确认该算子的控制参数序，再核对第 15 槽期望类型后重装；类型确认无误转查 <c>Jl_ERR_WIPV15</c>、<c>Jl_ERR_WIPN15</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），统一检查 <c>JlOperatorException.throwOperator</c> 会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT15 = 1215;

	/// <summary>算子的第 16 个控制参数类型不对（取值 1216）。</summary>
	/// <remarks>
	///   <para><b>含义</b>类型族（1201~1220）第 16 位，码 = 1200 + 16：原生参数表里第 16 个控制参数的类型与传入不符；序号按控制参数序计，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>只有控制参数很多的算子才会命中到第 16 槽，常是把某子选项的标志串误填为数值。<c>JlTuple</c> 隐式转换到原生调用才暴露错配。</para>
	///   <para><b>处置</b>先确认控制参数序，再核第 16 槽期望类型后重装；类型确认无误转查 <c>Jl_ERR_WIPV16</c>（值越界）与 <c>Jl_ERR_WIPN16</c>（值个数不对）。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT16 = 1216;

	/// <summary>算子的第 17 个控制参数类型不对（取值 1217）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1201~1220 类型族成员，码 = 1200 + 17：原生参数表第 17 个控制参数被给了与要求不符的类型；计数按控制参数槽序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>占用到第 17 槽的算子控制参数极多，错位常在批量装配子选项时发生；标志槽与数值槽互换后，<c>JlTuple</c> 隐式转换让错配延后到原生调用以本码现形。</para>
	///   <para><b>处置</b>先核对控制参数序，再确认第 17 槽期望类型后重装；类型无误仍报本码转查 <c>Jl_ERR_WIPV17</c>、<c>Jl_ERR_WIPN17</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT17 = 1217;

	/// <summary>算子的第 18 个控制参数类型不对（取值 1218）。</summary>
	/// <remarks>
	///   <para><b>含义</b>类型族（1201~1220）第 18 位，码 = 1200 + 18：原生参数表第 18 个控制参数类型与传入不符；序号是控制参数序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>该序号已接近类型族上界（1220），只有控制参数很多的算子才用得到，通常是批量装配时把标志串与数值串填反；<c>JlTuple</c> 隐式转换延后暴露到原生调用。</para>
	///   <para><b>处置</b>先确认该算子的控制参数序，再核第 18 槽期望类型后重装；类型确认无误转查 <c>Jl_ERR_WIPV18</c>、<c>Jl_ERR_WIPN18</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），统一检查 <c>JlOperatorException.throwOperator</c> 会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT18 = 1218;

	/// <summary>算子的第 19 个控制参数类型不对（取值 1219）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1201~1220"控制参数类型"族成员，码 = 1200 + 19，指原生参数表第 19 个控制参数被给了错类型；序号按控制参数槽序计，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>倒数第二个序号，仅控制参数很多的算子才会占用到；标志串被填成数值或反之，<c>JlTuple</c> 隐式转换不拦，错配到原生调用才回本码。</para>
	///   <para><b>处置</b>先确认控制参数序，再核第 19 槽期望类型后重装；类型确认无误转查 <c>Jl_ERR_WIPV19</c>（值越界）与 <c>Jl_ERR_WIPN19</c>（值个数不对）。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT19 = 1219;

	/// <summary>算子的第 20 个控制参数类型不对（取值 1220），控制参数类型族的上界码。</summary>
	/// <remarks>
	///   <para><b>含义</b>1201~1220 类型族最后一位，码 = 1200 + 20：原生参数表第 20 个控制参数类型与传入不符；序号是控制参数序，非 C# 形参序。1220 之后（1221~1300）本族不再编码，超过 20 个控制参数的算子若越界会以何码回报，本仓库未见对应常量 [待实测]。</para>
	///   <para><b>何时遇到</b>只有控制参数铺到第 20 槽的算子才会命中，通常是批量装配子选项时标志串与数值串填反；<c>JlTuple</c> 隐式转换把错配延后到原生调用。</para>
	///   <para><b>处置</b>先确认控制参数序，再核第 20 槽期望类型后重装；类型确认无误转查 <c>Jl_ERR_WIPV20</c>、<c>Jl_ERR_WIPN20</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPT20 = 1220;

	/// <summary>算子的第 1 个控制参数取值不在允许范围内（取值 1301）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1301~1320"控制参数取值"族第一个，码 = 1300 + 序号：第 1 个控制参数的类型和值的个数都对，但值本身不被允许（超出规定区间或不在枚举集合内）。序号按控制参数槽序计，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>例如数值越界（负阈值、超出图像定义域）、或字符串标志拼写不在该槽允许的取值集合里——类型没错、只是值取错。</para>
	///   <para><b>处置</b>对照该算子文档核对第 1 个控制参数允许的取值范围后修正。若实为类型不符转 <c>Jl_ERR_WIPT1</c>，值个数不对转 <c>Jl_ERR_WIPN1</c>；可用 <c>JlNativeApi.GetErrorMessage(err)</c> 取原生文本辅助定位。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true，判据 err &gt;= 1000），统一检查 <c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV1 = 1301;

	/// <summary>算子的第 2 个控制参数取值不在允许范围内（取值 1302）。</summary>
	/// <remarks>
	///   <para><b>含义</b>取值族（1301~1320）第 2 位，码 = 1300 + 2：第 2 个控制参数类型与个数都对、但取值不被允许（越界或不在枚举集合）。序号是控制参数序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>常见于标志串拼错、或数值超出该算子规定的区间——注意与"类型不对"（<c>Jl_ERR_WIPT2</c>）区分：本码说明类型是对的，只是值取错。</para>
	///   <para><b>处置</b>核对第 2 个控制参数允许的取值后修正；若其实是类型不符转 <c>Jl_ERR_WIPT2</c>，个数不符转 <c>Jl_ERR_WIPN2</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV2 = 1302;

	/// <summary>算子的第 3 个控制参数取值不在允许范围内（取值 1303）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1301~1320 取值族成员，码 = 1300 + 3：第 3 个控制参数类型和值的个数都对，但取值不被允许（越界或不在枚举集合内）；序号按控制参数槽序计。</para>
	///   <para><b>何时遇到</b>三参以上算子里，把某标志串写成不在允许清单的拼写，或数值落在规定区间之外。类型没错，别去查类型。</para>
	///   <para><b>处置</b>核对第 3 槽允许的取值后修正；类型不符转 <c>Jl_ERR_WIPT3</c>、个数不符转 <c>Jl_ERR_WIPN3</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV3 = 1303;

	/// <summary>算子的第 4 个控制参数取值不在允许范围内（取值 1304）。</summary>
	/// <remarks>
	///   <para><b>含义</b>取值族（1301~1320）第 4 位，码 = 1300 + 4：第 4 个控制参数类型与个数都对、但取值不被允许（越界或不在枚举集合）。序号是控制参数序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>标志串拼写在允许清单之外，或数值超出该算子规定的区间。<c>JlTuple</c> 对字符串/数值的隐式转换不校验取值，故到原生调用才回本码。</para>
	///   <para><b>处置</b>核对第 4 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT4</c>、个数不符转 <c>Jl_ERR_WIPN4</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV4 = 1304;

	/// <summary>算子的第 5 个控制参数取值不在允许范围内（取值 1305）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1301~1320"控制参数取值"族成员，码 = 1300 + 5：第 5 个控制参数类型和值个数都对，但取值越界或不在枚举集合内；序号按控制参数序计。</para>
	///   <para><b>何时遇到</b>数值落在规定区间外，或标志串不在允许清单里——类型没错，别误当 <c>Jl_ERR_WIPT5</c> 排查。</para>
	///   <para><b>处置</b>核对第 5 槽允许的取值范围后修正；类型不符转 <c>Jl_ERR_WIPT5</c>、个数不符转 <c>Jl_ERR_WIPN5</c>，可用 <c>JlNativeApi.GetErrorMessage(err)</c> 取文本辅助。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV5 = 1305;

	/// <summary>算子的第 6 个控制参数取值不在允许范围内（取值 1306）。</summary>
	/// <remarks>
	///   <para><b>含义</b>取值族（1301~1320）第 6 位，码 = 1300 + 6：第 6 个控制参数类型与个数都对、取值却不被允许（越界或不在枚举集合）。序号是控制参数序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>多控制参数算子里，某槽规定的取值区间或枚举写错值。<c>JlTuple</c> 隐式转换不校验取值，到原生调用才回本码。</para>
	///   <para><b>处置</b>核对第 6 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT6</c>、个数不符转 <c>Jl_ERR_WIPN6</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV6 = 1306;

	/// <summary>算子的第 7 个控制参数取值不在允许范围内（取值 1307）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1301~1320 取值族成员，码 = 1300 + 7：第 7 个控制参数类型和个数都对，但取值越界或不在枚举集合内；序号按控制参数序计。</para>
	///   <para><b>何时遇到</b>某槽允许的取值被写成清单外的标志串或超出区间；本码专指"值"，类型问题请看 <c>Jl_ERR_WIPT7</c>。</para>
	///   <para><b>处置</b>核对第 7 槽允许取值后修正；若其实是类型不符转 <c>Jl_ERR_WIPT7</c>，个数不符转 <c>Jl_ERR_WIPN7</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），统一检查 <c>JlOperatorException.throwOperator</c> 会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV7 = 1307;

	/// <summary>算子的第 8 个控制参数取值不在允许范围内（取值 1308）。</summary>
	/// <remarks>
	///   <para><b>含义</b>取值族（1301~1320）第 8 位，码 = 1300 + 8：第 8 个控制参数类型与个数都对、但取值不被允许（越界或不在枚举集合）。序号是控制参数序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>控制参数较多时把某槽的枚举串写成清单外值，或数值超出规定区间。<c>JlTuple</c> 隐式转换不校验取值。</para>
	///   <para><b>处置</b>核对第 8 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT8</c>、个数不符转 <c>Jl_ERR_WIPN8</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV8 = 1308;

	/// <summary>算子的第 9 个控制参数取值不在允许范围内（取值 1309）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1301~1320"控制参数取值"族成员，码 = 1300 + 9：第 9 个控制参数类型和个数都对，但取值越界或不在枚举集合内；序号按控制参数槽序计。</para>
	///   <para><b>何时遇到</b>只有控制参数较多的算子才占用到第 9 槽，把某枚举串写成清单外值或数值超区间；<c>JlTuple</c> 隐式转换不校验取值。</para>
	///   <para><b>处置</b>核对第 9 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT9</c>、个数不符转 <c>Jl_ERR_WIPN9</c>，可用 <c>JlNativeApi.GetErrorMessage(err)</c> 取文本。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV9 = 1309;

	/// <summary>算子的第 10 个控制参数取值不在允许范围内（取值 1310）。</summary>
	/// <remarks>
	///   <para><b>含义</b>取值族（1301~1320）第 10 位，码 = 1300 + 10：第 10 个控制参数类型与个数都对、但取值不被允许；序号是控制参数序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>高序号意味着算子控制参数很多，某枚举串或数值落在规定范围之外。<c>JlTuple</c> 隐式转换到原生调用才以本码回报。</para>
	///   <para><b>处置</b>先确认控制参数序，再核第 10 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT10</c>、个数不符转 <c>Jl_ERR_WIPN10</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV10 = 1310;

	/// <summary>算子的第 11 个控制参数取值不在允许范围内（取值 1311）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1301~1320"控制参数取值"族成员，码 = 1300 + 11：第 11 个控制参数类型和个数都对，但取值越界或不在枚举集合内；序号按控制参数序计。</para>
	///   <para><b>何时遇到</b>控制参数铺到第 11 槽的算子里，把某枚举串写成清单外值或数值超区间；本码不涉及类型，类型问题看 <c>Jl_ERR_WIPT11</c>。</para>
	///   <para><b>处置</b>核对第 11 槽允许取值后修正；若实为类型不符转 <c>Jl_ERR_WIPT11</c>，个数不符转 <c>Jl_ERR_WIPN11</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），统一检查 <c>JlOperatorException.throwOperator</c> 会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV11 = 1311;

	/// <summary>算子的第 12 个控制参数取值不在允许范围内（取值 1312）。</summary>
	/// <remarks>
	///   <para><b>含义</b>取值族（1301~1320）第 12 位，码 = 1300 + 12：第 12 个控制参数类型与个数都对、取值却不被允许；序号是控制参数序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>能占用到第 12 槽的算子控制参数很多，某枚举串或数值落在规定范围之外。<c>JlTuple</c> 不校验取值，错值到原生调用才回本码。</para>
	///   <para><b>处置</b>先确认控制参数序，再核第 12 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT12</c>、个数不符转 <c>Jl_ERR_WIPN12</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV12 = 1312;

	/// <summary>算子的第 13 个控制参数取值不在允许范围内（取值 1313）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1301~1320 取值族成员，码 = 1300 + 13：第 13 个控制参数类型和个数都对，但取值越界或不在枚举集合内；序号按控制参数序计。</para>
	///   <para><b>何时遇到</b>控制参数较多的复合算子才占用到第 13 槽，某枚举串或数值超出规定范围。<c>JlTuple</c> 隐式转换不校验取值。</para>
	///   <para><b>处置</b>核对第 13 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT13</c>、个数不符转 <c>Jl_ERR_WIPN13</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV13 = 1313;

	/// <summary>算子的第 14 个控制参数取值不在允许范围内（取值 1314）。</summary>
	/// <remarks>
	///   <para><b>含义</b>取值族（1301~1320）第 14 位，码 = 1300 + 14：第 14 个控制参数类型与个数都对、但取值不被允许；序号是控制参数序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>仅控制参数很多的算子会占用第 14 槽，把某枚举串写成清单外值或数值越界。<c>JlTuple</c> 隐式转换延后暴露到原生调用。</para>
	///   <para><b>处置</b>先确认控制参数序，再核第 14 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT14</c>、个数不符转 <c>Jl_ERR_WIPN14</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV14 = 1314;

	/// <summary>算子的第 15 个控制参数取值不在允许范围内（取值 1315）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1301~1320"控制参数取值"族成员，码 = 1300 + 15：第 15 个控制参数类型和个数都对，但取值越界或不在枚举集合内；序号按控制参数序计。</para>
	///   <para><b>何时遇到</b>第 15 槽已是高序号，对应控制参数很多的算子；某枚举串或数值超出规定范围，<c>JlTuple</c> 隐式转换到原生调用才回本码。</para>
	///   <para><b>处置</b>核对第 15 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT15</c>、个数不符转 <c>Jl_ERR_WIPN15</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV15 = 1315;

	/// <summary>算子的第 16 个控制参数取值不在允许范围内（取值 1316）。</summary>
	/// <remarks>
	///   <para><b>含义</b>取值族（1301~1320）第 16 位，码 = 1300 + 16：第 16 个控制参数类型与个数都对、但取值不被允许；序号是控制参数序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>接近族上界的高序号槽，只有控制参数很多的算子会命中；把枚举串写成清单外值或数值越界时回报本码。</para>
	///   <para><b>处置</b>先确认控制参数序，再核第 16 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT16</c>、个数不符转 <c>Jl_ERR_WIPN16</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV16 = 1316;

	/// <summary>算子的第 17 个控制参数取值不在允许范围内（取值 1317）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1301~1320 取值族成员，码 = 1300 + 17：第 17 个控制参数类型和个数都对，但取值越界或不在枚举集合内；序号按控制参数序计。</para>
	///   <para><b>何时遇到</b>倒数第四个序号，仅控制参数很多的算子会占用；枚举串写成清单外值或数值超出规定范围时回报本码，<c>JlTuple</c> 隐式转换不校验取值。</para>
	///   <para><b>处置</b>核对第 17 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT17</c>、个数不符转 <c>Jl_ERR_WIPN17</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV17 = 1317;

	/// <summary>算子的第 18 个控制参数取值不在允许范围内（取值 1318）。</summary>
	/// <remarks>
	///   <para><b>含义</b>取值族（1301~1320）第 18 位，码 = 1300 + 18：第 18 个控制参数类型与个数都对、但取值不被允许；序号是控制参数序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>接近族上界的高序号槽，仅控制参数很多的算子会命中；把某枚举串写成清单外值或数值越界时回报本码。</para>
	///   <para><b>处置</b>先确认控制参数序，再核第 18 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT18</c>、个数不符转 <c>Jl_ERR_WIPN18</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV18 = 1318;

	/// <summary>算子的第 19 个控制参数取值不在允许范围内（取值 1319）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1301~1320"控制参数取值"族成员，码 = 1300 + 19：第 19 个控制参数类型和个数都对，但取值越界或不在枚举集合内；序号按控制参数序计。</para>
	///   <para><b>何时遇到</b>倒数第二个序号，仅控制参数很多的算子会占用；枚举串写成清单外值或数值超出规定范围时回报本码。</para>
	///   <para><b>处置</b>核对第 19 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT19</c>、个数不符转 <c>Jl_ERR_WIPN19</c>，可用 <c>JlNativeApi.GetErrorMessage(err)</c> 取文本。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV19 = 1319;

	/// <summary>算子的第 20 个控制参数取值不在允许范围内（取值 1320），控制参数取值族的上界码。</summary>
	/// <remarks>
	///   <para><b>含义</b>1301~1320 取值族最后一位，码 = 1300 + 20：第 20 个控制参数类型和个数都对，但取值越界或不在枚举集合内。1320 之后本族不再按序号编码（紧接的是 1350 <c>Jl_ERR_WCOMP</c>），控制参数多于 20 个时越界取值会以何码回报，本仓库未见对应常量 [待实测]。</para>
	///   <para><b>何时遇到</b>仅控制参数铺到第 20 槽的算子会命中，某枚举串写成清单外值或数值超出规定范围。</para>
	///   <para><b>处置</b>核对第 20 槽允许取值后修正；类型不符转 <c>Jl_ERR_WIPT20</c>、个数不符转 <c>Jl_ERR_WIPN20</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPV20 = 1320;

	/// <summary>图像分量（component）选择参数的取值不合法（取值 1350）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本为 <c>Wrong value of component</c>。与 1301~1320 按控制参数槽序编码的"取值"族不同，本码是独立码、贴在取值族邻位（1350），专指被选中的"分量/通道"（component）取值不被接受，而不是普通控制参数越界。</para>
	///   <para><b>何时遇到</b>在需要指定图像分量的算子里传了不认识的分量标志，或该标志与当前图像类型不匹配（例如对单通道图请求彩色分量）。允许的分量集合随算子与图像类型而变，本仓库未内嵌清单 [待实测]。</para>
	///   <para><b>处置</b>核对分量标志的拼写与是否被当前图像类型支持；相邻的 <c>Jl_ERR_WGCOMP</c>（1351）是灰度分量版本，勿混。可用 <c>JlNativeApi.GetErrorMessage(err)</c> 取原生文本。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WCOMP = 1350;

	/// <summary>灰度分量（gray value component）选择参数的取值不合法（取值 1351）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本为 <c>Wrong value of gray value component</c>。与 1301~1320 按控制参数槽序编码的"取值"族不同，本码与 1350（<c>Jl_ERR_WCOMP</c>）同为贴在取值族邻位的独立码：1350 管一般图像分量，本码专指"灰度分量"的选择值不被接受，不是普通控制参数越界。</para>
	///   <para><b>何时遇到</b>在需要选取灰度分量的算子里传了不认识的灰度分量标志，或当前图像类型下不存在该灰度分量组合（如单通道图无通道可选）。允许的标志集合随算子与图像类型而变，本仓库未内嵌清单 [待实测]。</para>
	///   <para><b>处置</b>核对灰度分量标志的拼写及其与当前图像类型的匹配性；勿与一般分量码 <c>Jl_ERR_WCOMP</c>（1350）混用排查对象。可用 <c>JlNativeApi.GetErrorMessage(err)</c> 取原生文本。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WGCOMP = 1351;

	/// <summary>算子的第 1 个控制参数值的个数与要求不符（取值 1401）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 "控制参数值个数"族第一个，码 = 1400 + 序号：第 1 个控制参数类型对（否则先报 12xx 同序号）、值的个数却与算子要求不符——典型如两端的区间参数只给了一端。序号按控制参数槽序计，非 C# 形参序。</para>
	///   <para><b>何时遇到</b><c>JlTuple</c> 的隐式转换把任何标量都伪装成"1 值元组"畅通无阻，个数校验推迟到原生调用才以本码现形；首槽即错往往说明整串控制参数从开头就装配错了。</para>
	///   <para><b>处置</b>对照算子文档给第 1 个控制参数凑齐要求的值个数；实为类型不符转 <c>Jl_ERR_WIPT1</c>、值越界转 <c>Jl_ERR_WIPV1</c>；可用 <c>JlNativeApi.GetErrorMessage(err)</c> 取原生文本辅助定位。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN1 = 1401;

	/// <summary>算子的第 2 个控制参数值的个数与要求不符（取值 1402）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 2 位，码 = 1400 + 2：第 2 个控制参数值的个数与要求不符；对象参数同位错报在 <c>Jl_ERR_WION2</c>（1502），别把两族序号混着读。</para>
	///   <para><b>何时遇到</b>低序号槽出现次数错，多数意味着整体参数序错位而非该槽本身装配失误——有人把本属第 3 槽的多值参数提前塞进了第 2 槽。区间/灰度范围这类两值参数给成三个值也直接回本码。</para>
	///   <para><b>处置</b>从第 1 槽起重排该算子的控制参数；若重排后报错序号迁移到 1401、1403 等邻近码，即坐实是整体错位而非单槽失误。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN2 = 1402;

	/// <summary>算子的第 3 个控制参数值的个数与要求不符（取值 1403）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 3 位，码 = 1400 + 3：第 3 个控制参数值的个数与要求不符；本族三校验（12xx 类型 / 14xx 个数 / 13xx 取值）里，本码专管个数。</para>
	///   <para><b>何时遇到</b>占用到第 3 个控制参数的算子里，省略某个可选参数又忘了把参数序整体前移/补位，会让第 3 槽收到与期望不符的个数（第 3 槽具体是哪路参数随算子而异 [待实测]）。</para>
	///   <para><b>处置</b>要么按文档全量装配、要么用带默认位的具名重载，别靠删参数"缩短"列表；修完后序号变化即错位解除的信号。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN3 = 1403;

	/// <summary>算子的第 4 个控制参数值的个数与要求不符（取值 1404）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 4 位，码 = 1400 + 4：第 4 个控制参数值的个数与要求不符；类型之误报 <c>Jl_ERR_WIPT4</c>、取值之误报 <c>Jl_ERR_WIPV4</c>，三者同序号同槽。</para>
	///   <para><b>何时遇到</b>四号槽在多值参数里常是"最后一组端点"（ROI 的 row2/col2、旋转区间两界等），补界漏一头的写法错误会精确落在这一带 [待实测]。</para>
	///   <para><b>处置</b>核对第 4 槽期望个数后重装；用"错码序号是否随修改迁移"可判断是单槽配错还是整体错位。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN4 = 1404;

	/// <summary>算子的第 5 个控制参数值的个数与要求不符（取值 1405）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 5 位，码 = 1400 + 5：第 5 个控制参数值的个数与要求不符。本族判据只有一件事——该槽实传值的个数是否等于期望个数，与值内容好坏无关。</para>
	///   <para><b>何时遇到</b>第 5 槽具体承载哪个参数随算子而异 [待实测]；跨算子搬运装配代码时，把两值区间拆成两个单值参数、或只填下界漏上界，是高发错法。</para>
	///   <para><b>处置</b>按算子文档把第 5 槽的区间/多值参数以完整个数的 <c>JlTuple</c> 装入；反复调不准时回读 <c>JlNativeApi.GetErrorMessage(err)</c> 确认槽号未被错位劫持。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN5 = 1405;

	/// <summary>算子的第 6 个控制参数值的个数与要求不符（取值 1406）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 6 位，码 = 1400 + 6：第 6 个控制参数值的个数与算子要求不符；同一槽的另两道校验由 <c>Jl_ERR_WIPT6</c>（类型）与 <c>Jl_ERR_WIPV6</c>（取值）编码，报出哪个码即卡在哪个阶段。</para>
	///   <para><b>何时遇到</b>第 6 槽多落在算子的次级选项上，主参数调好后补次级选项时最常配错个数；元组拼接时多带一个尾随元素也回此码。</para>
	///   <para><b>处置</b>按文档把第 6 槽值个数配准；改完后错码在 12xx/13xx/14xx 之间迁移，正好可当校验进度的探针用。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN6 = 1406;

	/// <summary>算子的第 7 个控制参数值的个数与要求不符（取值 1407）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 7 位，码 = 1400 + 7：第 7 个控制参数值的个数与要求不符；序号按原生控制参数槽序计，非 C# 形参序，与对象参数族 <c>Jl_ERR_WION7</c>（1507）同序号不同类别。</para>
	///   <para><b>何时遇到</b>控制参数到第 7 个的算子已属参数偏多的类型；若该槽是点列/多边形这类成对多值参数（各算子所配不同 [待实测]），只给一半坐标（给 row 漏 col 或反之）时个数恰差一半，直接回本码。</para>
	///   <para><b>处置</b>检查第 7 槽是否为"row、col 成对"之类的复合参数并补全配对；对象参数错报在 15xx，先分清参数类别再修。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN7 = 1407;

	/// <summary>算子的第 8 个控制参数值的个数与要求不符（取值 1408）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 8 位，码 = 1400 + 8：第 8 个控制参数值的个数与算子要求不符；校验顺序上类型（12xx）在前、个数（本族）居中、取值（13xx）在后，三族同一序号指向同一槽。</para>
	///   <para><b>何时遇到</b>中等深度槽位常落在"主参数之后、选项包之前"的过渡带，复制他算子装配代码时该带结构最易搬错；对多值槽按单值习惯传是最高频写法错误。</para>
	///   <para><b>处置</b>核对第 8 槽期望个数重装；若同一段代码换个算子就正常，基本可断定为参数序搬错而非数据错误。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN8 = 1408;

	/// <summary>算子的第 9 个控制参数值的个数与要求不符（取值 1409）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 9 位，码 = 1400 + 9：第 9 个控制参数值的个数与要求不符；类型过关才轮到本校验，故看到本码可先排除类型问题（那会先报 12xx 同序号）。</para>
	///   <para><b>何时遇到</b>单值槽给了区间、区间槽给了单值这类"一对一错配"在第 9 槽的投影；由配置驱动的动态参数装配（按 JSON/表驱动拼元组）是重灾区。</para>
	///   <para><b>处置</b>对照文档修正第 9 槽个数；修正后如改报 <c>Jl_ERR_WIPV9</c> 说明个数已对、卡在取值，再走取值族流程。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN9 = 1409;

	/// <summary>算子的第 10 个控制参数值的个数与要求不符（取值 1410）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 10 位，码 = 1400 + 10：第 10 个控制参数值的个数与要求不符；本族与 12xx（类型）、13xx（取值）共用槽序，三码同序号即同一槽的三道校验关。</para>
	///   <para><b>何时遇到</b>十位序号是分水岭往后，多值型槽（ROI 列表、多级阈值序列）被截半传入最容易在此现形；把二维点集的 x、y 拆成两个独立参数传也会令槽序多出一段。</para>
	///   <para><b>处置</b>把第 10 槽恢复为要求的值个数；若不确定该槽是"一个多值参数"还是"两个单值参数"，读回原生文本比对序号，或直接改用该算子的具名重载装配。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN10 = 1410;

	/// <summary>算子的第 11 个控制参数值的个数与要求不符（取值 1411）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 11 位，码 = 1400 + 11：第 11 个控制参数值的个数与要求不符（类型对、取值未查）；序号按原生控制参数槽序计。</para>
	///   <para><b>何时遇到</b>能占用到第 11 槽的已是控制参数很多的算子，参数表长时肉眼点数极易差一；从 C# 形参拷贝装配时把可选尾参挤前/漏后也会撞上。</para>
	///   <para><b>处置</b>对照算子文档逐槽点数重排第 11 槽；若改后连环报出 12xx/13xx 的邻近序号，即错位得到纠正的旁证，顺势清完整个参数序。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN11 = 1411;

	/// <summary>算子的第 12 个控制参数值的个数与要求不符（取值 1412）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 12 位，码 = 1400 + 12：第 12 个控制参数值的个数与要求不符；类型之误在 12xx 同序号、取值之误在 13xx 同序号，三族共用一套槽序。</para>
	///   <para><b>何时遇到</b>深层槽常成组使用（如某算法的多组裁剪框参数），只填第一组、其余组漏装时，被漏装的组会精确地在各自槽号上回本码；也可能因前置槽错位而被殃及。</para>
	///   <para><b>处置</b>先核对整体装载序再修第 12 槽个数；批量场景建议逐算子封装一个"按文档顺序装参数"的构造函数，避免手排槽位。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN12 = 1412;

	/// <summary>算子的第 13 个控制参数值的个数与要求不符（取值 1413）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 13 位，码 = 1400 + 13：第 13 个控制参数收到值的个数与要求不符；序号按控制参数槽序计，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>把可选控制参数"留空"的错误做法是直接不装该槽，而原生按固定槽序校验——前一槽漏装会使后面所有槽整体错位，第 13 槽报出个数码往往是这种连锁错位的表象。</para>
	///   <para><b>处置</b>不只修第 13 槽，先自第 1 槽起逐槽核对本算子的控制参数装载序；确认无错位后仍报本码，才按第 13 槽自身期望个数重整。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN13 = 1413;

	/// <summary>算子的第 14 个控制参数值的个数与要求不符（取值 1414）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 14 位，码 = 1400 + 14：第 14 个控制参数值的个数与算子要求不符；本码只回答"个数对不对"，类型、取值分别由 12xx/13xx 同序号码回答。</para>
	///   <para><b>何时遇到</b>深层多值参数（多点列表、分段阈值序列）长度随输入数据浮动时最危险：单条数据能过、批量里个别行回本码，属间歇性错报，日志要带上当时的输入特征。</para>
	///   <para><b>处置</b>把第 14 槽的元组削/补到要求的个数；若该槽本就允许变长个数而仍回本码，则期望长度下限另有规定，查算子文档 [待实测]。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN14 = 1414;

	/// <summary>算子的第 15 个控制参数值的个数与要求不符（取值 1415）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 15 位，码 = 1400 + 15：第 15 个控制参数值的个数不符要求（类型与取值不在本码校验范围）；序号为原生控制参数槽序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>区间类（两值）、方向类（角度+基准）这种成对参数被拆散传、或补了第三值，都会精确回落到对应槽号的个数码；本码即第 15 槽。</para>
	///   <para><b>处置</b>对照算子文档把第 15 槽的值补齐/削减到要求个数；修正后若改报 <c>Jl_ERR_WIPV15</c>，说明个数已过、转为取值问题。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN15 = 1415;

	/// <summary>算子的第 16 个控制参数值的个数与要求不符（取值 1416）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 16 位，码 = 1400 + 16：第 16 个控制参数传入值的个数与算子要求不符；类型、取值不查，只查个数。槽序按原生控制参数序计。</para>
	///   <para><b>何时遇到</b>深层槽位多服务于同一大参数拆出的子列表，改一个子项时整段元组长度忘记同步，就会以本码现形；<c>JlTuple</c> 隐式转换不在 C# 侧拦长度。</para>
	///   <para><b>处置</b>把第 16 槽的值个数恢复为该算子要求的数量；拿不准期望值时以 <c>JlNativeApi.GetErrorMessage(err)</c> 回读原生文本比对槽号。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN16 = 1416;

	/// <summary>算子的第 17 个控制参数值的个数与要求不符（取值 1417）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 "控制参数值个数"族第 17 位，码 = 1400 + 17：原生参数表第 17 个控制参数收到值的个数与要求不符；类型与取值本身不拦，只看个数。序号按控制参数槽序计，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>批量装配深层子选项时，多值槽被塞了单值、或单值槽被塞了区间；标志串与数值串互换一般先被类型族（1201~1220）拦走，报本码说明类型过关、纯粹是个数不对。</para>
	///   <para><b>处置</b>核对第 17 槽期望的值个数并重整 <c>JlTuple</c>；若修正个数后改报 <c>Jl_ERR_WIPT17</c> 或 <c>Jl_ERR_WIPV17</c>，说明已推进到对应校验阶段，顺族排查即可。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN17 = 1417;

	/// <summary>算子的第 18 个控制参数值的个数与要求不符（取值 1418）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族第 18 位，码 = 1400 + 18：第 18 个控制参数值的个数与该槽要求不符（类型本身没错）；计数按控制参数槽序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>接近族上界的高槽位，只有控制参数极多的算子才用得到；常见诱因是把区间两端拆成两次单值、或把该给多值的槽喂了单值元组。</para>
	///   <para><b>处置</b>先确认该算子控制参数序，再给第 18 槽按要求的个数重装；类型问题对照 <c>Jl_ERR_WIPT18</c>，取值问题对照 <c>Jl_ERR_WIPV18</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN18 = 1418;

	/// <summary>算子的第 19 个控制参数值的个数与要求不符（取值 1419）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 个数族倒数第 2 位，码 = 1400 + 19：第 19 个控制参数类型对、但值的个数不符要求；序号是控制参数槽序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>仅控制参数很多的算子占用得到，常在把多值参数（区间、多点列表）与单值参数互相搬移时把元组长度配错；<c>JlTuple</c> 隐式转换让单值"看起来合法"，到原生调用才被个数校验拦下。</para>
	///   <para><b>处置</b>核对第 19 槽要求的值个数后重装；类型之误转查 <c>Jl_ERR_WIPT19</c>，值本身越界转查 <c>Jl_ERR_WIPV19</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN19 = 1419;

	/// <summary>算子的第 20 个控制参数值的个数与要求不符（取值 1420），控制参数个数族的上界码。</summary>
	/// <remarks>
	///   <para><b>含义</b>1401~1420 "控制参数值个数"族最后一位，码 = 1400 + 20：第 20 个控制参数类型没错，但一次传入的值的个数与该槽要求不符（如槽要求两个端点却只给一个值）。序号按控制参数槽序计，非 C# 形参序；1420 之后本族不再编码，更多控制参数越界如何回报本仓库未见常量 [待实测]。</para>
	///   <para><b>何时遇到</b>只有控制参数铺到第 20 槽的算子才会命中，多为把区间参数当单值传、或批量装配子选项时元组长度没对上要求的个数。</para>
	///   <para><b>处置</b>按算子文档给第 20 槽凑齐值的个数重装元组；类型不符转 <c>Jl_ERR_WIPT20</c>、值越界转 <c>Jl_ERR_WIPV20</c>，三族同一序号指同一槽，可顺藤排查。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WIPN20 = 1420;

	/// <summary>传入算子的输入对象个数超过原生上限（取值 1500）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本为 <c>Number of input objects too big</c>：本次调用承载的输入对象总数超出原生侧输入容器容量，是容量类错误而非槽位错报。它是 15xx 对象族的起头码：1501~1509 按槽编码个数不符，1510（<c>Jl_ERR_OONTB</c>）为输出侧对应上限。</para>
	///   <para><b>何时遇到</b>多路输入算子（合成、堆叠、批量比对）把上千个区域/轮廓一次性整叠传入时；上限具体数值本仓库未内嵌 [待实测]。</para>
	///   <para><b>处置</b>分批调用并聚合结果，或先用筛选削减对象数再传入；若是"某槽个数与要求不符"而非总量超限，报的会是 <c>Jl_ERR_WION1</c>~，两者的修法完全不同。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_IONTB = 1500;

	/// <summary>算子的第 1 个对象参数值的个数与要求不符（取值 1501）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1501~1509 "对象参数个数"族第一个，码 = 1500 + 序号：第 1 个对象参数（图像/区域/XLD 等句柄类参数）实传的个数与该槽要求不符。序号按原生对象参数槽序计，非 C# 形参序；1500（<c>Jl_ERR_IONTB</c>）是输入总量上限码，不是"第 0 槽"。</para>
	///   <para><b>何时遇到</b>该槽只收一路对象却传入了整叠多对象、或要求多路只给了单路时最常见；对象元组拼装出错时首槽通常最先被发现，报出的就是本码。</para>
	///   <para><b>处置</b>核对第 1 个对象参数期望的个数并重整元组；实为输入对象总数超容量转 <c>Jl_ERR_IONTB</c>；控制参数个数的错报在 1401~1420（<c>Jl_ERR_WIPN1</c>~）族。可用 <c>JlNativeApi.GetErrorMessage(err)</c> 取原生文本辅助定位。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WION1 = 1501;

	/// <summary>算子的第 2 个对象参数值的个数与要求不符（取值 1502）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1501~1509 对象参数个数族第 2 位，码 = 1500 + 2：第 2 个对象参数传入的对象个数与算子要求不符；计数按原生对象参数槽序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>双对象输入算子（比对、叠加、mask 相与之类）的第 2 路常被整叠区域元组误传，而该槽只收一个；反过来单对象被拆成两个句柄传同样回此码。</para>
	///   <para><b>处置</b>确认第 2 槽期望个数后合并或拆分对象再调；用 <c>CountObj()</c> 可先数清手里的对象叠有多高。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WION2 = 1502;

	/// <summary>算子的第 3 个对象参数值的个数与要求不符（取值 1503）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1501~1509 对象参数个数族第 3 位，码 = 1500 + 3：第 3 个对象参数传入的对象个数与算子要求不符；序号是原生对象参数槽序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>常见于把"一路对象"与"一叠对象"混用的算子：第 3 槽要求恰好 N 个，实传个数差一（循环边界、去重逻辑都会引起）即回本码。</para>
	///   <para><b>处置</b>数一遍第 3 槽实际装载的对象个数再对照文档要求；若算子根本没有第三个对象参数而仍回此码，优先怀疑参数整体错位，逐槽重排。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WION3 = 1503;

	/// <summary>算子的第 4 个对象参数值的个数与要求不符（取值 1504）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1501~1509 对象参数个数族第 4 位，码 = 1500 + 4：第 4 个对象参数传入的对象个数与算子要求不符；序号按原生对象参数槽序计，非 C# 形参序。本族 1501~1509 只编码"个数"这一种错，类型/取值之误不会以此段回报。</para>
	///   <para><b>何时遇到</b>四路输入的算子（基准图+待测图+两路参考区域之类）在复用参数装配代码时漏配第四路，或把多路对象塞进了同一槽。</para>
	///   <para><b>处置</b>核对第 4 槽期望的对象个数并重新分发；与 <c>Jl_ERR_WIPN4</c>（1404，控制参数第 4 槽）按参数类别分开排查，两族序号并不互相指认。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WION4 = 1504;

	/// <summary>算子的第 5 个对象参数值的个数与要求不符（取值 1505）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1501~1509 对象参数个数族第 5 位，码 = 1500 + 5：第 5 个对象参数传入的对象个数与算子要求不符；计数按原生对象参数槽序，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>五路及以上输入的算子（多图像合成、多区域比对之类）在组装参数时把某一路的对象列表短了一截；该槽期望单对象却给了多个、或反之，同样回本码。</para>
	///   <para><b>处置</b>先确认算子该槽期望的个数（单对象槽给 1 个），再修正装载；类型不符与值不符不会报这里——对象参数族只查"个数"。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WION5 = 1505;

	/// <summary>算子的第 6 个对象参数值的个数与要求不符（取值 1506）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1501~1509 对象参数个数族第 6 位，码 = 1500 + 6：第 6 个对象参数传入的对象个数与算子要求不符；序号是原生对象参数槽序，非 C# 形参序——同一算子里 15xx 与 14xx 两族的"第 6"未必对应同一个 C# 参数。</para>
	///   <para><b>何时遇到</b>对象数按通道/模板等外部条件动态增减的调用最易触发：某一分支少凑了一个对象，原生侧按该槽要求的个数一查即回本码。</para>
	///   <para><b>处置</b>打印本次准备的对象清单核对第 6 槽实际个数；确属总量超限而非槽位错报时转查 <c>Jl_ERR_IONTB</c>。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WION6 = 1506;

	/// <summary>算子的第 7 个对象参数值的个数与要求不符（取值 1507）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1501~1509 对象参数个数族第 7 位，码 = 1500 + 7：第 7 个对象参数传入的对象个数与算子要求不符；序号按原生对象参数槽序计，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>占用到第 7 个对象参数槽的多输入算子，在循环中重建对象元组时最容易长度漂移；与同序号的控制参数族 <c>Jl_ERR_WIPN7</c>（1407）区分：那族错在控制参数，本族错在对象参数。</para>
	///   <para><b>处置</b>按算子文档给第 7 槽配齐对象个数再试；若一次调用携带的输入对象总量超容量，报的会是 <c>Jl_ERR_IONTB</c>（1500），处置思路完全不同。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WION7 = 1507;

	/// <summary>算子的第 8 个对象参数值的个数与要求不符（取值 1508）。</summary>
	/// <remarks>
	///   <para><b>含义</b>1501~1509 对象参数个数族第 8 位，码 = 1500 + 8：第 8 个对象参数传入的对象个数与算子要求不符；序号按原生对象参数槽序计，非 C# 形参序。</para>
	///   <para><b>何时遇到</b>只有对象参数铺到第 8 槽的多输入算子才会用到本码，批量装配对象元组时漏一项、多一项都会回此报；第 8 槽具体承载哪路输入随算子而异，本仓库未内嵌对照表 [待实测]。控制参数同序号的错报在 <c>Jl_ERR_WIPN8</c>，类别不同。</para>
	///   <para><b>处置</b>对照算子文档给第 8 槽配齐要求的对象个数；若实为总量撞上限转 <c>Jl_ERR_IONTB</c>（1500）。可用 <c>JlNativeApi.GetErrorMessage(err)</c> 取原生文本辅助定位。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WION8 = 1508;

	/// <summary>算子的第 9 个对象参数值的个数与要求不符（取值 1509），本族编码的最后一位。</summary>
	/// <remarks>
	///   <para><b>含义</b>1501~1509 "对象参数个数"族上界，码 = 1500 + 9：第 9 个对象参数传入的对象个数与算子该槽要求不符。本族只编码到第 9 槽，1510 已被输出总量码 <c>Jl_ERR_OONTB</c> 占用；超出 9 个对象参数的算子如何回报此类错误，本仓库未见对应常量 [待实测]。</para>
	///   <para><b>何时遇到</b>用到第 9 槽的多输入算子（多路合成、多模板类）在循环里重建对象元组时长度漂移；序号是原生对象参数槽序，非 C# 形参序。</para>
	///   <para><b>处置</b>核对第 9 槽要求的对象个数后重装元组；若实为输入对象总数撞上限转 <c>Jl_ERR_IONTB</c>，控制参数个数的同类族是 1401~1420（<c>Jl_ERR_WIPN1</c>~），序号相同但参数类别不同，别拿错族。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WION9 = 1509;

	/// <summary>算子产出的输出对象个数超过原生上限（取值 1510）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本为 <c>Number of output objects too big</c>：算子执行完成后要交回的对象数量超出原生输出容器容量。注意它发生在计算之后、参数装配没错——所以本码比 1401~1420 参数族更"晚"，排查方向是输入数据规模而不是传参写法。</para>
	///   <para><b>何时遇到</b>分割、连通域类算子在大图上一次产出成千上万个对象并整体收走时；上限具体数值本仓库未内嵌 [待实测]。</para>
	///   <para><b>处置</b>先用筛选类手段压输出规模（如 <c>Connection</c> 后按面积筛、分块分批处理），再重跑；输入侧的对应上限码是 <c>Jl_ERR_IONTB</c>（1500），按参数槽计数的错报则是 <c>Jl_ERR_WION1</c>~。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_OONTB = 1510;

	/// <summary>算子参数定义本身写错，原生把错处指到 xxx.def 定义文件（取值 2000）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本为 <c>Wrong specification of parameter (error in file: xxx.def)</c>：报错的不是调用方的传参，而是原生运行时随附的算子参数定义文件（.def）里参数规格有误——属产品发行物层面的缺陷。2000 同时是 20xx 许可与初始化族的起头码，但本码内容与许可无关。</para>
	///   <para><b>何时遇到</b>正常安装下几乎不应出现；多见于运行时分发被裁剪/篡改、两套版本的定义文件混装叠加、安装介质损坏。</para>
	///   <para><b>处置</b>用单一版本的运行时做干净重装，不要自行编辑 .def；出错的算子名一般带在原生文本的 xxx.def 位置，可用 <c>JlNativeApi.GetErrorMessage(err)</c> 取回后定位模块 [待实测]。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WNP = 2000;

	/// <summary>Vision 初始化阶段对 reset_obj_db(Width,Height,Components) 的调用回报异常（取值 2001）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本为 <c>Initialize Vision: reset_obj_db(Width,Height,Components)</c>：运行时首次初始化时用图像宽、高、通道数去重置对象数据库（reset_obj_db）这一步失败。文本本身更像"初始化卡在哪一步"的定位提示，而非对用户参数的描述。</para>
	///   <para><b>何时遇到</b>进程启动后第一次原生调用触发惰性初始化时；初始化路径上 Width/Height/Components 取值不合理（零、负、异常大）或内存资源不足时最可能在此现形 [待实测]。</para>
	///   <para><b>处置</b>先检查进入初始化的图像规格参数是否为正且量级合理，再确认机器内存；用 <c>JlNativeApi.GetErrorMessage(err)</c> 取原生文本辅助定位。与 2000（<c>Jl_ERR_WNP</c>，定义文件错）区分：那是安装缺陷，本码是初始化执行失败。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_HONI = 2001;

	/// <summary>已占用的符号化对象名数量超过上限（取值 2002）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本为 <c>Used number of symbolic object names too big</c>：以名字登记、可按名寻址的对象数量超出了原生侧名字表容量，是容量类错误，与 14xx/15xx 参数族性质不同；上限具体数值本仓库未内嵌 [待实测]。</para>
	///   <para><b>何时遇到</b>长周期程序反复注册具名对象而不注销、用外部数据批量生成对象名时一次性灌入过多；2000/2001 是定义文件与初始化族邻码，勿与本码混用处置思路。</para>
	///   <para><b>处置</b>改为用完即弃的命名策略或定期注销不再引用的对象名，控制同时存活的具名对象规模；本仓库未提供名字表容量的查询接口 [待实测]。</para>
	///   <para><b>归类</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_WRKNN = 2002;

	/// <summary>错误码 2003：未找到任何许可证（20xx 许可族的总收口码）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "No license found"：本机所有许可来源（文件、服务器、加密狗——狗族另有 2300/2301 专码）都排查完后一无所获，运行功能被整体锁住。20xx 段自 2000（<c>Jl_ERR_WNP</c>）起为许可与初始化错误族，全部 ≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），统一由 <c>JlOperatorException.throwOperator</c> 抛出，不可忽略。</para>
	///   <para><b>典型触发</b>首次部署运行时而从未放置许可；路径/服务器配置存在但全部失效。区分：2036 是特定查找 license.dat 未命中，本码是"任何途径都没有可用许可"的最终结论。</para>
	///   <para><b>处置</b>按部署文档配置许可后重试；若确认已配置仍回本码而非更细的 2036/2037，优先排查该配置是否真正作用于当前进程（服务账号、环境变量继承差异等）[待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NO_LICENSE = 2003;

	/// <summary>错误码 2004：此许可类型在当前 Vision 版本里未实现。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "License type not implemented in this version of Vision"：许可采用的类型/形式是当前运行时版本不具备读取能力的（例如新一代许可格式）。它不是过期也不是缺项，而是解析代码根本不认识这种许可——典型是"旧运行时配新许可"；反方向（新运行时配旧许可）多报 2033（OLDVER）。</para>
	///   <para><b>典型触发</b>厂商换用了新许可体系而运行时未同步升级；把测试环境的新型许可拷进旧版生产环境。</para>
	///   <para><b>处置</b>升级运行时到支持该许可类型的版本，或请签发方按当前版本认识的类型重新签发。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NOT_IMPLEMENTED = 2004;

	/// <summary>错误码 2005：许可里没有写入模块清单（缺 VENDOR_STRING）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "No modules in license (no VENDOR_STRING)"：许可被接受，但其内没有可用的模块列表——原生文本点名缺 VENDOR_STRING 字段，即签发时没往这条许可里写任何功能模块。三层递进区分：2003 是一份许可都没有，本码是许可在但内容为空，2031 是内容在但缺那一项。</para>
	///   <para><b>典型触发</b>占位性质的空许可被拿去部署；许可传输/另存时自定义字段被截断 [待实测]。</para>
	///   <para><b>处置</b>退回签发渠道重新导出带完整模块清单的许可；客户端无法补写该字段。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NO_MODULES = 2005;

	/// <summary>错误码 2006：当前许可不包含所调用算子的授权。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "No license for this operator"：许可本身有效，但这一次调用的算子需要单独的授权条目而许可未覆盖，是逐算子粒度的门禁。与 2031（NOFEATURE，许可清单里找不到该功能条目）语义相邻，本码直接点名"被调算子"，多在单个高价值算子（如 3D、匹配类）上设卡 [待实测]。</para>
	///   <para><b>典型触发</b>试用到期后只剩基础包仍调用付费算子；精简许可配了完整版代码路径。</para>
	///   <para><b>处置</b>查明该算子所属模块并补授权；本码 ≥1000，会以 <c>JlOperatorException</c> 抛出，捕获后可按错误码分支降级到不依赖该算子的实现。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NO_LIC_OPER = 2006;

	/// <summary>错误码 2008：vendor key 不支持当前运行平台。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Vendor keys do not support this platform"：密钥体系本身能对上，但这套 vendor key 声明支持的平台集合不含当前运行时进程所在的平台/位数。与 2034（PLATNOTLIC，具体许可条目的 PLATFORMS 列表没含此平台）分层：本码更靠密钥层，2034 更靠条目层；与 2009（BADVENDORKEY）的区别是那边连密钥都不认。</para>
	///   <para><b>典型触发</b>把他平台签发的许可拷来跑；同机 32/64 位宿主混用导致平台标识与签发时不符 [待实测]。</para>
	///   <para><b>处置</b>换发按当前平台签名的许可；平台归属由许可决定，客户端配置改不动。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_BADPLATFORM = 2008;

	/// <summary>错误码 2009：vendor key 校验失败，密钥不匹配。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Bad vendor keys"：许可中的厂商密钥（vendor key）与运行时内置值对不上，该许可被判定为伪造或来自另一条产品线。与 2008（BADPLATFORM）区分：那边密钥本身能认、只是不覆盖当前平台，这边连密钥这一关都过不了。</para>
	///   <para><b>典型触发</b>误用了其他软件厂商的许可文件；签发渠道用了与当前运行时代际不符的密钥版本；许可文件被第三方"破解工具"改坏。</para>
	///   <para><b>处置</b>核对许可来源后向签发方索取正确密钥体系下的许可；本码无法在客户端解决。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_BADVENDORKEY = 2009;

	/// <summary>错误码 2021：检测到系统时钟被回拨。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "System clock has been set back"：许可组件记录的"见过的最新系统时间"比当前时间还新，判定时钟被人为倒退（典型的绕过到期日手段），于是拒绝发放许可。方向上与 2042（LONGGONE，因时间前进而过期）相反：本码专抓往回拨。</para>
	///   <para><b>典型触发</b>手工改时间延长试用；CMOS 电池失效或 NTP 校正造成非人为倒退；双系统对硬件时钟的理解不一致导致开机时间跳回 [待实测]。</para>
	///   <para><b>处置</b>把系统时间恢复为正确的当前时间后重试；许可组件内部时间水位是否需要重启或等待多久才重新收敛，本仓库未见记录 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_BADSYSDATE = 2021;

	/// <summary>错误码 2022：传入的版本号参数不是合法浮点格式。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Version argument is invalid floating point format"：许可相关接口收到一个要求按浮点数解析的版本参数，但字符串解析失败（非数字、空串、分隔符混入）。这是调用方入参校验错误，与许可文件内容无关——许可本身好坏的码在 2033/2384 那边。</para>
	///   <para><b>典型触发</b>从配置或命令行拼接版本号时带入空格、引号；区域设置把小数点写成逗号（"18,05"）。哪些接口会收此参数本仓库未逐一列举 [待实测]。</para>
	///   <para><b>处置</b>以不变文化（invariant）浮点文本传版本号，如 18.05；先核对传参来源再做许可排查。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_BAD_VERSION = 2022;

	/// <summary>错误码 2024：无法与许可服务器建立连接。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Cannot establish a connection with a license server"：已向服务器发起连接但建立失败。与 2038（NOSERVER，"Cannot connect to a license server"）语义几乎重叠，哪种失败细节回哪个码未经本仓库证实 [待实测]，排查手段通用。</para>
	///   <para><b>典型触发</b>服务器未启动、地址或端口写错、防火墙拦截。区别于 2030（计数许可压根没配服务器地址）——能报出本码说明地址已知、已实际拨号。</para>
	///   <para><b>处置</b>先 telnet 端口验证连通性，再启服务或放行防火墙；"连上后写不进/无响应"分别对应 2047/2069，用于二分定位失败阶段。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_CANTCONNECT = 2024;

	/// <summary>错误码 2028：与许可服务器的会话数已达上限。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Session limit exceeded"：能同时连到许可服务器的会话（连接）数超过服务器侧配置的上限，本次新建会话被拒。这是通信层的坑位限制，不是许可份数耗尽——会话满时许可可能仍有空闲；份数占满报 2029。</para>
	///   <para><b>典型触发</b>每个进程实例各开一条会话、部署的进程数多于服务器会话配额；异常退出客户端的僵尸会话未超时释放 [待实测]。</para>
	///   <para><b>处置</b>服务器侧调大会话上限，或客户端复用既有连接、减少并发进程实例数。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_MAXSESSIONS = 2028;

	/// <summary>错误码 2029：许可份数已全部被占用，领不到空闲的一份。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "All licenses in use"：计数许可的总份数是够的，只是此刻全被其他会话占着，本次排队失败。与 2052（TOOMANY，请求份数本身超过 feature 上限，再怎么等也要不到）区分；与 2028（MAXSESSIONS）也不同：那是服务器连接会话的坑位满了，不是许可份数没了。</para>
	///   <para><b>典型触发</b>联网终端数长期多于许可份数；某个客户端进程异常退出后其占用未被服务器及时回收 [待实测]。</para>
	///   <para><b>处置</b>释放本进程的其余占用、错峰重试，或增购份数；若常态性卡此码，在服务器端清点未释放的僵死会话。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_MAXUSERS = 2029;

	/// <summary>错误码 2030：计数型许可没有写明许可服务器地址。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "No license server specified for counted license"：所持的是网络计数许可（并发份数由服务器统一管账），但许可配置里没有写服务器地址，客户端根本不知道该去哪里领号。属"配置缺一块"，比 2024/2038 更前端——那两个是地址已有、连接建不起来，本码连发起连接的前提都不成立。</para>
	///   <para><b>典型触发</b>管理员签发的计数许可漏填服务器字段；把单机许可的配置模板直接套到计数许可上用。</para>
	///   <para><b>处置</b>让签发方补全许可中的服务器地址字段（含端口）后重新部署；字段的确切写法本仓库未内嵌 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NO_SERVER_IN_FILE = 2030;

	/// <summary>错误码 2031：在许可文件里找不到所需的 feature 条目。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Can not find feature in the license file"：许可本身在、也读得开，但当前操作所需的功能条目（feature）不在其清单内。与 2003（NO_LICENSE，什么许可都没有）、2005（NO_MODULES，许可里根本没写模块清单）逐层区分：本码是"有清单、缺这一项"；2006（NO_LIC_OPER）则更直接地钉在"被调算子未授权"上。</para>
	///   <para><b>典型触发</b>调用了未购买的收费模块算子；许可文件被换成缩水版；feature 命名随版本变更导致老许可匹配不上 [待实测]。</para>
	///   <para><b>处置</b>核对许可覆盖的功能清单与所调算子的归属模块；缺项走增购换发，重试无意义。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NOFEATURE = 2031;

	/// <summary>错误码 2033：许可文件太旧，支撑不了当前新版本的运行时。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "License file does not support a version this new"：许可条目声明可支撑的版本低于正在启动的 Vision 运行时版本，即"旧许可配新程序"且未随版本续发。卡在客户端库与许可文件两侧的兼容性上；若是"新许可配旧服务器组件"则报 2051（SERVLONGGONE），方向相反。</para>
	///   <para><b>典型触发</b>常规升级到新版运行时后沿用了维护期已到的旧许可；回滚部署失败造成新旧组件混装。</para>
	///   <para><b>处置</b>找签发渠道换发覆盖当前版本的许可，或把运行时退回许可覆盖的版本。手改许可文件里的版本号会被完整性校验拒绝，具体回哪个码未经本仓库证实 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_OLDVER = 2033;

	/// <summary>错误码 2034：运行平台不在许可的 PLATFORMS 授权列表内。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "This platform not authorized by license - running on platform not included in PLATFORMS list"：许可文件显式列出允许运行的 PLATFORMS 集合，而当前进程所在平台不在其中。与 2008（BADPLATFORM）区分：那边是 vendor key 层面就不支持该平台（签发密钥的问题），这边是密钥能认、但这条许可本身没授权此平台。</para>
	///   <para><b>典型触发</b>把他机/他平台签发的许可拷到本机运行；同一许可文件在多平台间共享时漏加当前平台条目。</para>
	///   <para><b>处置</b>向签发渠道申请把当前平台加入 PLATFORMS 或换发对应平台的许可；客户端侧配置绕不开此检查。本码 ≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），会以 <c>JlOperatorException</c> 抛出。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_PLATNOTLIC = 2034;

	/// <summary>错误码 2035：许可服务器忙，暂时无法受理本次请求。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "License server busy"：服务器已连通并回了话，但答复是"当前忙、不予受理"。属瞬态失败，性质上不同于 2028/2029 那类"确实没有空闲许可"的业务性拒绝——本码下许可可能仍然充足。</para>
	///   <para><b>典型触发</b>局域网内多台客户端同时发起获取许可的请求、服务器冷启动中或正在内部更新账目；是否自动重试及退避策略由许可组件决定，本仓库未见记录 [待实测]。</para>
	///   <para><b>处置</b>客户端做带间隔的有限次重试通常可过；持续本码则查服务器端负载。与 2069（NOSERVRESP）区分：那边是对方沉默不回话，这边是明确回了"忙"。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_SERVBUSY = 2035;

	/// <summary>错误码 2036：找不到许可文件 license.dat。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Could not find license.dat"：许可查找流程在配置的查找路径上没有命中任何许可文件。本仓库未内嵌查找顺序与目录清单，原生文本写死为 license.dat 这个名字 [待实测]。与 2037（BADFILE，文件在但语法解不开）、2056（NOREADLIC，文件在但读不出）区分：本码是"根本没找到文件"。</para>
	///   <para><b>典型触发</b>部署运行时后忘拷许可文件、许可路径配置未生效、把程序拷到新机器而该机器从未装过许可。</para>
	///   <para><b>处置</b>按部署文档把许可文件放到运行时查找位置或补上路径配置，然后重试；改配置后是否需要重启进程才能生效未经本仓库证实 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NOCONFFILE = 2036;

	/// <summary>错误码 2037：许可文件语法非法，解析不通过。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Invalid license file syntax"：许可文件已找到且能读入，但内容按许可协议解析失败（字段缺失、被手工改动破坏、编码转换损坏等）。与 2036（NOCONFFILE，文件根本找不到）、2056（NOREADLIC，文件存在但读取失败 I/O 层出错）构成"找得到→读得动→解得开"三层区分，本码卡在最内一层。</para>
	///   <para><b>典型触发</b>手工编辑许可文件改坏关键行、把两份许可文本错误拼接、编辑器以非常规编码另存、文件在网络共享上被截断。</para>
	///   <para><b>处置</b>放弃本地改过的副本，从签发渠道重新导出原始许可文件整份替换；不要试图逐行手工修补语法。本码 ≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），<c>JlOperatorException.throwOperator</c> 会据它抛异常，不可吞掉继续跑。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_BADFILE = 2037;

	/// <summary>错误码 2038：连不上许可服务器。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Cannot connect to a license server"：已知服务器地址但建立会话失败。</para>
	///   <para><b>典型触发</b>服务器未启动、端口被防火墙拦。与 2024（CANTCONNECT，establish 连接失败）语义几乎重叠，哪个码在哪种失败下出现未经本仓库代码证实 [待实测]。</para>
	///   <para><b>处置</b>先 ping/telnet 端口定位，再启服务或开防火墙规则。属 20xx 服务器通信族。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NOSERVER = 2038;

	/// <summary>错误码 2041：本机不是许可指定的宿主。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Invalid host"：节点锁定许可绑定了别的主机 ID，当前机器的 hostname/MAC/指纹与绑定不符。</para>
	///   <para><b>典型触发</b>拷贝他机许可文件、改机器名、虚机迁移换指纹。区别于 2045（BADHOST，解析不到服务器主机名，属网络问题）。</para>
	///   <para><b>处置</b>在本机重新采集主机指纹并换发许可；属 20xx 许可文件/主机绑定族。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NOTTHISHOST = 2041;

	/// <summary>错误码 2042：feature 许可已过期。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Feature has expired"：该功能条目声明的到期日早于当前系统日期。</para>
	///   <para><b>典型触发</b>维护期/订阅到期未续；若同时怀疑系统时钟，参见 2021（BADSYSDATE 检测时钟回拨）。</para>
	///   <para><b>处置</b>换发新许可；这是业务性到期，重试无意义，属 20xx 许可文件族。与 2051（SERVLONGGONE，服务器不支持 feature 版本）名字近似但含义无关。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_LONGGONE = 2042;

	/// <summary>错误码 2043：许可文件中的日期格式非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Invalid date format in license file"：日期字段无法按约定格式解析（如 expired= 后不是合法日期串）。</para>
	///   <para><b>典型触发</b>手工编辑许可文件打错了日期；与 2067（DATE_TOOBIG，格式对但数值超限）、2042（LONGGONE，格式对但已过期）区分。</para>
	///   <para><b>处置</b>用签发工具重新导出许可，勿手改日期字段。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_BADDATE = 2043;

	/// <summary>错误码 2044：许可服务器返回的数据无效。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Invalid returned data from license server"：收到了应答但内容不符合协议（损坏、乱序或非许可服务冒名应答）。</para>
	///   <para><b>典型触发</b>配置的端口上跑的其实是别的服务；与 2076（BADCHECKSUM，专指校验和不符）相比此码更泛化 [待实测]。</para>
	///   <para><b>处置</b>核对端口与协议配套版本；排除端口冲突后重试。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_BADCOMM = 2044;

	/// <summary>错误码 2045：在网络数据库中解析不到 SERVER 主机名。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Cannot find SERVER hostname in network database"：许可文件里写的服务器主机名 DNS/hosts 解析失败。</para>
	///   <para><b>典型触发</b>换域名没改许可、DNS 不通、离线环境无解析。区别于 2041（NOTTHISHOST，本机身份不符）。</para>
	///   <para><b>处置</b>修正本机 hosts 或 DNS；必要时把 SERVER 字段改为 IP 重新签发。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_BADHOST = 2045;

	/// <summary>错误码 2047：无法向许可服务器写入数据。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Cannot write data to license server"：连接已建立但发送请求失败（半断开或对方关闭了写通道）。</para>
	///   <para><b>典型触发</b>服务器在处理中途崩溃/重启、网络单向通。区别于 2024/2038（连不上）与 2069（连上没回话）。</para>
	///   <para><b>处置</b>确认服务器进程存活后重试；排查防火墙长时间空闲断连策略 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_CANTWRITE = 2047;

	/// <summary>错误码 2051：服务器端不支持该 feature 的当前版本。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "License server does not support this version of this feature"：许可条目声明的 feature 版本号高于服务器组件支持的版本。</para>
	///   <para><b>典型触发</b>新签的许可（feature 版本升级）配到未升级的许可服务器 [待实测]。</para>
	///   <para><b>处置</b>升级服务器端许可组件；与 2033/2384（客户端库与许可文件的版本互不兼容）区分：这里卡在服务器组件。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_SERVLONGGONE = 2051;

	/// <summary>错误码 2052：申请的许可数量超过该 feature 的上限。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Request for more licenses than this feature supports"：一次性请求的许可份数大于 feature 本身声明的最大并发量。</para>
	///   <para><b>典型触发</b>代码向计数许可请求 N 份而许可只签发了 M &lt; N 份；请求参数写错（如把对象数当份数传）[待实测]。</para>
	///   <para><b>处置</b>降低请求份数或增购份数。与 2029（MAXUSERS，总量够但都被占用）区分：这里是从根本上要不到这么多。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_TOOMANY = 2052;

	/// <summary>错误码 2055：找不到以太网（MAC）设备。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Cannot find ethernet device"：许可按网卡 MAC 绑定主机，但本机枚举不到可用的以太网卡。</para>
	///   <para><b>典型触发</b>禁用/更换了签发时绑定的网卡，或在无物理网卡的虚机里跑 MAC 绑定许可 [待实测]。</para>
	///   <para><b>处置</b>启用绑定的那块网卡，或按现 MAC 重新签发主机指纹。属 20xx 许可文件/主机绑定族。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_CANTFINDETHER = 2055;

	/// <summary>错误码 2056：许可文件读不出来。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Cannot read license file"：文件存在但打开/读取失败。区别于 2036（NOCONFFILE，文件找不到）与 2037（BADFILE，语法非法）——这里是 I/O 层失败。</para>
	///   <para><b>典型触发</b>文件被占用、权限不足、所在路径是断开的网络盘。</para>
	///   <para><b>处置</b>检查文件权限与占用进程；把许可文件放到本地目录再试。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NOREADLIC = 2056;

	/// <summary>错误码 2067：日期超出旧二进制编码能表示的上限。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Date too late for binary format"：许可中的到期日期太晚，超过了某二进制协议字段的表示范围（类似 32 位时间戳溢出 [待实测]）。</para>
	///   <para><b>典型触发</b>签发了超长期（如几十年后到期）的许可 [待实测]。</para>
	///   <para><b>处置</b>把到期日设在合理范围内重新签发；与 2043（BADDATE，日期格式非法）区分：这里格式没错、是数值过大。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_DATE_TOOBIG = 2067;

	/// <summary>错误码 2069：许可服务器对消息没有响应。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Server did not respond to message"：请求已发出但服务器在时限内没有回话。区别于 2024/2038（连接都建立不了）——这里链路已通、对方沉默。</para>
	///   <para><b>典型触发</b>服务器进程假死、请求被防火墙静默丢弃、服务器过载排队 [待实测]。</para>
	///   <para><b>处置</b>重启许可服务器守护进程并观察其日志；客户端可作有限次重试。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NOSERVRESP = 2069;

	/// <summary>错误码 2075：许可组件调用 setsockopt() 失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文即系统调用 setsockopt() 出错：许可库在建立与服务器通信前设置套接字选项被操作系统拒绝。</para>
	///   <para><b>典型触发</b>本机网络栈异常、安全软件拦截原始套接字、或 fd/资源耗尽 [待实测]。</para>
	///   <para><b>处置</b>重启网络相关服务、检查终端安全策略后重试；属 20xx 服务器通信族的底层网络类。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_SETSOCKFAIL = 2075;

	/// <summary>错误码 2076：与许可服务器通信的消息校验和失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Message checksum failure"：收到的应答数据校验和不符，报文被破坏或被中途改写。</para>
	///   <para><b>典型触发</b>网络中间设备篡改/截断流量，或端口被非许可服务占用导致应答串扰 [待实测]；与 2044（BADCOMM，返回数据本身无效）相比此码特指校验环节。</para>
	///   <para><b>处置</b>核对许可服务器端口范围与防火墙/NAT 设置，直连复测。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_BADCHECKSUM = 2076;

	/// <summary>错误码 2082：许可组件内部错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Internal licensing error"：许可库自身逻辑异常，不指向任何用户可改的配置，是 20xx 族里的兜底码。</para>
	///   <para><b>典型触发</b>许可组件文件损坏/版本混杂，或罕见的服务器状态异常 [待实测]。</para>
	///   <para><b>处置</b>先重装许可组件排除损坏，仍复现则带许可日志找供应商；普通调用参数无法修复。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_INTERNAL_ERROR = 2082;

	/// <summary>错误码 2087：许可服务器不支持所请求的操作。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Server doesn't support this request"：服务器版本/配置没有该请求所需的能力（如向旧版 lmgrd 发了新式管理请求 [待实测]）。</para>
	///   <para><b>典型触发</b>客户端许可组件比服务器守护进程新得多。</para>
	///   <para><b>处置</b>升级服务器端组件与客户端配套；与 2345（策略不允许）区分：这里是能力缺失。属 20xx 服务器通信族。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NOSERVCAP = 2087;

	/// <summary>错误码 2091：所请求的功能位于另一个许可池。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "This feature is available in a different license pool"：feature 存在，但不在当前查询/领取的许可池（pool）里。</para>
	///   <para><b>典型触发</b>服务器侧划分了多个池，客户端未指定或指定了错误的池选项 [待实测]。</para>
	///   <para><b>处置</b>让管理员确认池分配或修正许可搜索选项。属 20xx 许可服务器族，客户端参数改不动它。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_POOL = 2091;

	/// <summary>错误码 2300：加密狗未插入或无法读取。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Dongle not attached, or can't read dongle"：许可要求硬件狗，但设备上枚举不到狗或通信失败。</para>
	///   <para><b>典型触发</b>USB 狗被拔、换 USB 口、集线器供电不足。若驱动本身缺失则报 2301。</para>
	///   <para><b>处置</b>直插主机 USB 口重试；确认狗型号与许可介质一致。属加密狗族起点（2300），不可通过代码消除。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NODONGLE = 2300;

	/// <summary>错误码 2301：缺少加密狗驱动。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Missing dongle driver"：系统里没装（或没加载）加密狗对应的驱动程序，许可组件因此枚举不到狗。</para>
	///   <para><b>典型触发</b>裸机部署只插狗不装驱动；驱动被系统升级冲掉。与 2300（狗未插/读不到）区分：这里是驱动层缺失。</para>
	///   <para><b>处置</b>安装/重装狗驱动并重启后验证设备管理器可见。属加密狗族（2300–2301），纯环境问题。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NODONGLEDRIVER = 2301;

	/// <summary>错误码 2318：许可操作超时。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Timeout"：等待许可服务器/激活服务响应超过时限。是 23xx 段的笼统超时码。</para>
	///   <para><b>典型触发</b>服务器负载高或网络抖动；与 2069（NOSERVRESP，服务器未回应消息）相比此码强调等待到时。</para>
	///   <para><b>处置</b>可重试；反复出现则查服务器负载与防火墙。环境类问题，与调用参数无关。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_TIMEOUT = 2318;

	/// <summary>错误码 2321：许可服务器证书无效。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Invalid license server certificate"：许可组件不信任服务器出示的证书（更泛化的情形）。</para>
	///   <para><b>典型触发</b>证书链不完整或证书与被连接对象不匹配 [待实测]。</para>
	///   <para><b>处置</b>部署完整信任链后重连；若错误明确发生在 TLS 层则对应 2335。属 23xx 安全/加密狗子类。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_INVALID_CERTIFICATE = 2321;

	/// <summary>错误码 2335：许可服务器的 SSL/TLS 证书无效。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Invalid license server SSL/TLS certificate"：与许可服务器建立加密通道时证书校验失败（过期、域名不符或被中间设备篡改等 [待实测]）。</para>
	///   <para><b>典型触发</b>服务器证书到期未换、客户端不信任签发机构。</para>
	///   <para><b>处置</b>更新服务器证书或在客户端侧正确部署信任链；与 2321（通用证书无效）区分：此码专指 TLS 层。属安全/网络子类（23xx 段）。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_INVALID_TLS_CERTIFICATE = 2335;

	/// <summary>错误码 2339：收到的激活请求不合法。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Invalid activation request received"：激活服务器侧判定请求内容（格式/签名/状态）无效。</para>
	///   <para><b>典型触发</b>激活请求文件被手工改动或已使用过 [待实测]。</para>
	///   <para><b>处置</b>重新生成激活请求再走一遍激活流程；泛化的激活失败看 2348（ACTIVATION）。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_BAD_ACTREQ = 2339;

	/// <summary>错误码 2345：请求的许可操作不被允许。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Specified operation is not allowed"：对许可执行的某类管理操作（如激活、借用、转移之类 [待实测]）在当前许可模式下被禁止。</para>
	///   <para><b>典型触发</b>对不可激活式（节点锁定）许可执行激活操作，或对计数许可做单机式操作 [待实测]。</para>
	///   <para><b>处置</b>按许可类型选择正确的管理流程；与 2087（服务器不支持该请求）区分：这里是策略不允许，不是能力不支持。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NOT_ALLOWED = 2345;

	/// <summary>错误码 2348：许可激活过程出错。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Activation error"：在线/离线激活流程整体失败的笼统码，具体环节可能由 2339（激活请求非法）、2318（超时）等更细的码区分 [待实测]。</para>
	///   <para><b>典型触发</b>激活码已被其他机器占用、激活服务器不可达。</para>
	///   <para><b>处置</b>查激活工具日志并重新激活；若伴随 2024/2069 一类网络码，先解决连通性。属激活族（23xx 段）。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_ACTIVATION = 2348;

	/// <summary>错误码 2379：未安装 CodeMeter Runtime。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "No CodeMeter Runtime installed"：使用 CodeMeter 介质（软许可/加密狗）时，本机缺少 CodeMeter 运行时组件。</para>
	///   <para><b>典型触发</b>新装机器只拷了库文件没装 CodeMeter；与 2301（缺驱动）近似，2300 是狗未插/读不到。</para>
	///   <para><b>处置</b>安装 CodeMeter Runtime 后重试。属 CodeMeter/加密狗族（23xx 段）的环境类错误，修改代码无法绕开。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NO_CM_RUNTIME = 2379;

	/// <summary>错误码 2380：已安装的 CodeMeter Runtime 版本过旧。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Installed CodeMeter Runtime is too old"：本机装了 CodeMeter 运行时但版本低于许可组件的要求。</para>
	///   <para><b>典型触发</b>升级视觉库后未同步升级 CodeMeter；与 2379（完全未安装 CodeMeter Runtime）区分。</para>
	///   <para><b>处置</b>升级 CodeMeter Runtime 到随库文档要求的版本 [待实测]。属加密狗/CodeMeter 族（23xx 段），与调用参数无关。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_CM_RUNTIME_TOO_OLD = 2380;

	/// <summary>错误码 2381：许可与 Vision 产品版本（edition）不匹配。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "License is for wrong Vision edition"：许可授权的功能版本线（如基础版/完整版之类 [待实测]）与当前加载的库版本不符。</para>
	///   <para><b>典型触发</b>在一台机器上混装两套不同版本线的库，或拿错另一产品线的许可文件。</para>
	///   <para><b>处置</b>核对许可签发时选定的版本线并换用对应库发行版；与 2004（NOT_IMPLEMENTED，本版本未实现该许可类型）区分：这里是版本线错配。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_WRONG_EDITION = 2381;

	/// <summary>错误码 2382：许可条目含有无法识别的 FLAGS 标志。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "License contains unknown FLAGS"：许可记录里的 FLAGS 属性带有当前许可组件不认识的取值，多半是新版许可特性配旧版运行时的兼容问题 [待实测]。</para>
	///   <para><b>典型触发</b>手改过许可文件、或高版本工具签发的许可在低版本库上使用 [待实测]。</para>
	///   <para><b>处置</b>用签发工具重新导出许可，勿手工编辑 FLAGS 字段。属许可族（2381 WRONG_EDITION 常与版本/版本线错配同时出现）。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_UNKNOWN_FLAGS = 2382;

	/// <summary>错误码 2383：Vision 预览版许可已到期。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Vision preview version expired"：试用/预览性质的授权超过有效期。</para>
	///   <para><b>典型触发</b>预览许可到期后继续调用受许可保护的算子 [待实测]。</para>
	///   <para><b>处置</b>换正式许可或续期预览许可；不要尝试改系统时钟绕过（2021 BADSYSDATE 即检测时钟回拨）。属许可族，非代码参数问题。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_PREVIEW_EXPIRED = 2383;

	/// <summary>错误码 2384：许可不兼容过旧的 Vision 版本（与 2033 OLDVER 方向相反）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "License does not support a Vision version this old"：新式许可拒绝为过旧的运行时签发许可；2033（OLDVER）是许可太旧带不动新版本，此码是许可太新不认旧版本。</para>
	///   <para><b>典型触发</b>新签发的许可配到未升级的旧版 JLVisionLib.Runtime [待实测]。</para>
	///   <para><b>处置</b>升级运行时或换发与版本匹配的许可。属许可/激活族（23xx 段），无法通过改调用参数消除。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_NEWVER = 2384;

	/// <summary>许可族段起点标记：2003 起为许可（LIC）错误码段，本身不是可返回的错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>按名称（RANGE1_BEGIN）与取值可证实：2003 恰是其后 Jl_ERR_LIC_* 一族的第一个值，此常量用作许可错误族段（2003..2091）的下界标记，供范围判断（如 <c>err &gt;= 2003 &amp;&amp; err &lt;= 2384</c>）使用。</para>
	///   <para><b>疑点</b>原英文注释为 "Error codes concerning the Vision core, 2100..2199"，与名称和取值矛盾（2100..2199 段由 WOOPI/WIOPI/WOI/WRCN 等常量实际承载），疑为模板错挂句，以名称＋取值为准 [待实测]。</para>
	///   <para><b>同值</b>取值与 Jl_ERR_LIC_NO_LICENSE 相同（2003，别名/复用起点）；本文件后段另有 Jl_ERR_NO_LICENSE 亦为 2003。</para>
	/// </remarks>
	public const int Jl_ERR_LIC_RANGE1_BEGIN = 2003;

	/// <summary>错误码 2100：输出对象参数的索引错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Wrong specification... Wrong index for output object parameter"：访问算子的输出对象参数时使用的索引越界，即按序号取第 N 个输出对象但该算子没有第 N 个对象型输出。</para>
	///   <para><b>典型触发</b>把 out 参数与算子实际输出对象的个数/顺序对错，或复制别的算子的输出序号来用。值数量错误不走此码（那是 WION/OONTB 族）。</para>
	///   <para><b>推荐处置</b>查阅该算子签名里对象型输出（JlImage/JlRegion/JlXLD 等 out 形参）的个数与顺序。族段：2100–2103 属 21xx 核心族，原始英文文档把 2100..2199 一段标为 "Error codes concerning the Vision core"。</para>
	/// </remarks>
	public const int Jl_ERR_WOOPI = 2100;

	/// <summary>错误码 2101：输入对象参数的索引错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Wrong index for input object parameter"：访问某个输入对象参数（图像、区域等句柄型参数）时使用的索引超出该算子实际拥有的输入对象参数范围。</para>
	///   <para><b>典型触发</b>封装算子或批量调用时，按错误的参数序号去取输入对象；与传值数量无关（数量错走 1501–1509 的 WION 族）。</para>
	///   <para><b>推荐处置</b>对照算子签名数清对象型输入参数的个数与序号。与相邻码区分：2100 对应输出对象参数，2102 专指图像对象。</para>
	/// </remarks>
	public const int Jl_ERR_WIOPI = 2101;

	/// <summary>错误码 2102：图像对象的索引错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Wrong index for image object"：算子按索引引用其输入/输出图像对象时，给出的索引没有对应的图像。</para>
	///   <para><b>典型触发</b>多图像算子中索引越界（如只传了 1 路图像却按第 2 路取图），或图像元组元素数与算子期望的图像数不符 [待实测]。</para>
	///   <para><b>推荐处置</b>核对图像参数实际传入的元素个数与索引起点约定。同族区分：2100 是输出对象参数索引、2101 是输入对象参数索引，2102 专指图像对象。</para>
	/// </remarks>
	public const int Jl_ERR_WOI = 2102;

	/// <summary>错误码 2103：区域数量与图像分量数不匹配。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 "Wrong number region/image component"：按"图像＋区域（域）"成对处理的算子中，传入的区域数目与图像的分量/通道数对不上，属数量失配而非索引越界。</para>
	///   <para><b>典型触发</b>给接受图像与区域组合的算子传了区域数≠图像通道数的搭配（如 3 通道图配 2 个独立区域）[待实测]。</para>
	///   <para><b>推荐处置</b>核对调用前区域元组的元素数与图像分量数：要么把域合并成整图一个，要么为每个通道各备一个区域。与相邻码区分：2100–2102 都是"索引错误"族，此码是"数量错误"；其后 2104（WRRN，本段外）是关系名错误。</para>
	/// </remarks>
	public const int Jl_ERR_WRCN = 2103;

	/// <summary>关系名错误：运行时按名字查找关系（relation）未命中，对应英文原文 "Wrong relation name"。</summary>
	/// <remarks>
	///   <para>缩写 WRRN 可解作 Wrong Relation Name，与模板内嵌英文一致。运行时内部维护以名字登记的关系表，本码表示按名引用时查无此项；具体在哪类算子参数上会触发，仓库内无进一步证据 [待实测]。</para>
	///   <para>推荐处置：核对传入的名字/标识是否拼写一致、所指对象是否已先行登记。判定方式是拿 <see cref="M:JLVisionLib.JlException.GetErrorCode"/> 取回的码与本常量比较，本类只是纯定义表，库内代码不主动抛这些常量。</para>
	///   <para>族段：属 21xx 运行时对象/图像参数族（2100–2108 连续为对象数量、成分编号、关系名、图像宽高、成分定义类错误）。相邻区分：WRCN=2103 指成分个数错，本码指名字查不到，AUDI=2105 指访问未定义成分。</para>
	/// </remarks>
	public const int Jl_ERR_WRRN = 2104;

	/// <summary>访问未定义的图像成分：内嵌英文指"访问未定义的灰度成分"，缩写 AUDI 解作 Access to Undefined Image，口径统一为"访问打到了未定义的图像成分上"。</summary>
	/// <remarks>
	///   <para>含义：算子试图读取一张图像的灰度成分，而该成分尚未定义（图像对象存在但数据没建立，典型是创建/载入半途失败仍被继续使用）。</para>
	///   <para>推荐处置：确认图像创建或文件载入成功、成分类型与算子要求一致后再调用。</para>
	///   <para>族段：属 21xx 运行时对象/图像参数族。相邻区分：WRCN=2103 是成分个数错，WIWI/WIHE=2106/2107 是宽高错，ICUNDEF=2108 只陈述"成分未定义"这一状态，本码强调"访问动作"撞上它。</para>
	/// </remarks>
	public const int Jl_ERR_AUDI = 2105;

	/// <summary>图像宽度错误：图像宽（列数）不符合算子预期或参数界，对应英文原文 "Wrong image width"。</summary>
	/// <remarks>
	///   <para>含义：构造、拼接或跨数据源传递图像时，宽度校验失败；各算子对宽的具体上界随参数定义而异，仓库内无统一数值 [待实测]。</para>
	///   <para>推荐处置：先读回图像实际宽高与期望值对照，再修正不一致的来源（文件尺寸、裁剪/合成参数等）。</para>
	///   <para>族段：属 21xx 运行时对象/图像参数族。本库坐标约定 row=纵向、column=横向，宽度即 column 方向；与成对的 WIHE=2107（高度）按"宽/高"区分。</para>
	/// </remarks>
	public const int Jl_ERR_WIWI = 2106;

	/// <summary>图像高度错误：图像高（行数）不符合算子预期或参数界，对应英文原文 "Wrong image height"。</summary>
	/// <remarks>
	///   <para>含义：与 WIWI=2106 镜像，只是校验对象换成高度（row 方向的行数）；触发路径同为图像构造、合成、数据源不一致等。</para>
	///   <para>推荐处置：读回实际宽高核对，优先怀疑按错误尺寸生成的中间图像。</para>
	///   <para>族段：属 21xx 运行时对象/图像参数族；本段最后一个尺寸码，其后 ICUNDEF=2108 转入"成分未定义"主题。</para>
	/// </remarks>
	public const int Jl_ERR_WIHE = 2107;

	/// <summary>灰度成分未定义：图像对象的灰度成分本身处于未定义状态，对应英文原文 "Undefined gray value component"。</summary>
	/// <remarks>
	///   <para>含义与 AUDI=2105 的区分：本码陈述成分"处于未定义"这一状态（多半由查询成分属性的路径报告），AUDI 强调访问动作撞上未定义成分；两者确切分工仓库内无证据 [待实测]。</para>
	///   <para>推荐处置：重新赋值/重新生成该成分数据，勿拿半初始化的对象继续下游计算。</para>
	///   <para>族段：属 21xx 运行时对象/图像参数族，且是该段末位（2108）；下一个码起（2200）进入 22xx 系统/接口族。</para>
	/// </remarks>
	public const int Jl_ERR_ICUNDEF = 2108;

	/// <summary>内部数据表类型不一致：运行时数据基（data base）中记录的类型冲突，对应英文原文 "Inconsistent data of data base (typing)"。</summary>
	/// <remarks>
	///   <para>含义：运行时的内部数据表在读写时遇到类型不匹配，多见于不同版本组件混装导致的表结构错位；确切触发路径仓库内无证据 [待实测]。</para>
	///   <para>推荐处置：用户侧代码通常无法修复；先核对整套组件同版本部署，复现则记录触发算子与参数求助支持。</para>
	///   <para>族段：22xx 系统/接口族由此码（2200）开篇——2200–2202 内部数据表、2203/2206 遗留上限、2205/2207 扩展与安装包、2211/2212 版本兼容、2220–2222 扩展包 ID；23xx 整段被 LIC 加密狗码占用，故 2222 之后直接跳到 2400。相邻区分：DBDU=2202 是"条目未定义"，本码是"条目的类型不一致"。</para>
	/// </remarks>
	public const int Jl_ERR_IDBD = 2200;

	/// <summary>输入控制参数索引错误：按索引取输入控制参数时越界或不存在，对应英文原文 "Wrong index for input control parameter"。</summary>
	/// <remarks>
	///   <para>含义：调用机制传入的控制参数序号不在该算子输入参数表范围内；在按序号动态存取参数（而非按签名传参）的路径上最易触发 [待实测]。</para>
	///   <para>推荐处置：核对所用算子的控制参数清单及索引起点（0 基还是 1 基），改正取参序号。</para>
	///   <para>族段：属 22xx 系统/接口族的数据表子段（2200–2202）。相邻区分：2200/2202 说的是内部数据表本身的问题，本码是调用方给的参数索引错。</para>
	/// </remarks>
	public const int Jl_ERR_WICPI = 2201;

	/// <summary>数据表条目未定义——内嵌英文原文自带 "(internal error)" 定性："Data of data base not defined (internal error)"。</summary>
	/// <remarks>
	///   <para>含义：按运行时逻辑本应存在的内部数据表条目查询时为空；模板英文自己标注为内部错误，说明正常用户调用不该撞上它。</para>
	///   <para>推荐处置：非用户侧可修；重启运行时确认是否稳定复现，稳定复现则记录操作序列求助支持。</para>
	///   <para>族段：属 22xx 系统/接口族的数据表子段（2200–2202）。相邻区分：IDBD=2200 是类型不一致，本码是内容未定义；下一码 PNTL=2203 已是遗留上限类。</para>
	/// </remarks>
	public const int Jl_ERR_DBDU = 2202;

	/// <summary>遗留码：算子数量超限，对应英文原文 "legacy: Number of operators too big"。</summary>
	/// <remarks>
	///   <para>含义：早期固定容量算子表的容量校验，英文原文以 legacy 起头，表明当前运行时按常不可达 [待实测]。</para>
	///   <para>推荐处置：一般无需处理；若真见到此码，首先怀疑混装了老版本原生库。</para>
	///   <para>族段：属 22xx 系统/接口族的遗留上限子段，与 NPTL=2206（包数量超限）成对；2204 号在本文件缺号。与相邻码区分：2205（UEXTNI）已是扩展安装问题，不是容量问题。</para>
	/// </remarks>
	public const int Jl_ERR_PNTL = 2203;

	/// <summary>用户扩展未正确安装，对应英文原文 "User extension not properly installed"。</summary>
	/// <remarks>
	///   <para>含义：用户扩展模块没有完成进入运行时算子表的流程——安装/注册不完整或装载未成功；本仓库对注册流程无进一步证据 [待实测]。</para>
	///   <para>推荐处置：检查扩展文件是否就位、版本是否匹配运行时、其入口是否在首次调用前执行成功（对照 25xx XPI 族码定位扩展接口层问题）。</para>
	///   <para>族段：属 22xx 系统/接口族的安装子段。相邻区分：NSP=2207 是"所指的包根本没装"，本码是"扩展装了但处于未装好状态"。</para>
	/// </remarks>
	public const int Jl_ERR_UEXTNI = 2205;

	/// <summary>遗留码：包数量超限，对应英文原文 "legacy: Number of packages too large"。</summary>
	/// <remarks>
	///   <para>含义：对运行时已装包总数的历史容量校验，与 PNTL=2203（算子数上限）成对；当前按常不可达 [待实测]。</para>
	///   <para>推荐处置：一般无需处理；撞上则怀疑老版本组件混装。</para>
	///   <para>族段：属 22xx 系统/接口族的遗留上限子段。与相邻码区分：2205（UEXTNI）与 2207（NSP）都是"装不好/没装"，本码是"装得太多"。</para>
	/// </remarks>
	public const int Jl_ERR_NPTL = 2206;

	/// <summary>没有安装相应的包，对应英文原文 "No such package installed"。</summary>
	/// <remarks>
	///   <para>含义：算子依赖的功能包不在当前安装清单里。与许可证缺模块（2003–2091 LIC 族的 NO_MODULES 等码）的分界仓库内无直接证据 [待实测]。</para>
	///   <para>推荐处置：核对包含该算子的包是否已安装、程序是否指向了正确的运行时安装目录。</para>
	///   <para>族段：属 22xx 系统/接口族的安装子段。相邻区分：ICHOV=2211 是"装了但版本互斥"，本码是"压根没装"。</para>
	/// </remarks>
	public const int Jl_ERR_NSP = 2207;

	/// <summary>Vision 运行时版本不兼容，对应英文原文 "incompatible Vision versions"。</summary>
	/// <remarks>
	///   <para>含义：不兼容版本间互用组件或产物——新运行时装的旧扩展、旧运行时读新版本生成的序列化数据等；版本兼容矩阵仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：整套软件（原生库、C# 层、扩展包）统一版本，数据文件用同版本重新生成。</para>
	///   <para>族段：属 22xx 系统/接口族的版本兼容子段（2211/2212）。相邻区分：ICOI=2212 专指算子接口层错配，本码指产品整体版本。</para>
	/// </remarks>
	public const int Jl_ERR_ICHV = 2211;

	/// <summary>算子接口不兼容，对应英文原文 "incompatible operator interface"。</summary>
	/// <remarks>
	///   <para>含义：算子接口层与核心实现的版本对不上——接口声明的参数表与算子实现不一致，典型是只替换了部分组件造成混装 [待实测]。</para>
	///   <para>推荐处置：清理安装并整套同版本升级，不要单独替换某一个原生模块。</para>
	///   <para>族段：属 22xx 系统/接口族的版本兼容子段（2211/2212），2212 为该子段末位。与 ICHOV=2211 区分：那条讲产品整体版本，本条讲算子接口约定；其后 2220–2222 是扩展包 ID 子段。</para>
	/// </remarks>
	public const int Jl_ERR_ICOI = 2212;

	/// <summary>扩展包 ID 错误，对应英文原文 "wrong extension package id"。</summary>
	/// <remarks>
	///   <para>含义：按 ID 定位扩展包时该 ID 不存在或与登记不符；ID 的来源（清单文件还是注册接口）仓库内无证据 [待实测]。</para>
	///   <para>推荐处置：核对 ID 字符串拼写，确认该扩展包确实参与当前装载。</para>
	///   <para>族段：属 22xx 扩展包子段（2220–2222），是引用链最外层：包（本码）→ 算子（WOID=2221）→ 算子信息（WOIID=2222）。</para>
	/// </remarks>
	public const int Jl_ERR_XPKG_WXID = 2220;

	/// <summary>算子 ID 错误，对应英文原文 "wrong operator id"。</summary>
	/// <remarks>
	///   <para>含义：按 ID 在已注册的扩展包内查算子，未查到；常见诱因是把某版本的原生算子 ID 写死后跨版本使用 [待实测]。</para>
	///   <para>推荐处置：优先通过上层封装的算子名调用而非裸 ID；必须用 ID 时以当前版本的 ID 表为准。</para>
	///   <para>族段：属 22xx 扩展包子段（2220–2222）中间层。相邻区分：2220 错在包层，本码错在算子层，2222 错在算子信息层。</para>
	/// </remarks>
	public const int Jl_ERR_XPKG_WOID = 2221;

	/// <summary>算子信息 ID 错误，对应英文原文 "wrong operator information id"。</summary>
	/// <remarks>
	///   <para>含义：挂在算子下的辅助信息记录（"算子信息"）按其 ID 查找失败；该信息具体指参数表还是元数据，仓库内无证据 [待实测]。</para>
	///   <para>推荐处置：与 WOID=2221 同族处置——核对信息 ID 与算子在当前版本的登记是否一致。</para>
	///   <para>族段：属 22xx 扩展包子段（2220–2222）最深一层：包 → 算子 → 算子信息；本码为该子段末位，其后跳至 24xx 句柄/类型族（23xx 被 LIC 码占用）。</para>
	/// </remarks>
	public const int Jl_ERR_XPKG_WOIID = 2222;

	/// <summary>元组数组参数类型错误，对应英文原文 "Wrong Hctuple array type"（Hctuple 为原生引擎的元组数组类型名，本库对应 JlTuple 类数组参数）。</summary>
	/// <remarks>
	///   <para>含义：递给底层接口的元组数组元素类型与算子声明不符（如在整数位置传了字符串）。经 C# 层调用时 JlTuple 的隐式转换通常已保证元素类别，本码更多出现在直接使用原生接口的路径 [待实测]。</para>
	///   <para>推荐处置：核对实参元组的元素类别（整数/浮点/字符串）与算子签名要求后重建实参。</para>
	///   <para>族段：24xx 句柄与参数类型族由此码（2400）开篇——2400–2402 元组数组（类型/参数类型/索引）、2403–2404 文件版本与句柄类型、2410–2411 向量、2450–2456 句柄状态、2460–2461 控制/图像类别错配。相邻区分：CPAR_WTYP=2401 错在控制参数本身，本码错在数组元素。</para>
	/// </remarks>
	public const int Jl_ERR_CTPL_WTYP = 2400;

	/// <summary>控制参数类型错误，对应英文原文 "Wrong Hcpar type"（Hcpar 为原生引擎的控制参数类型名）。</summary>
	/// <remarks>
	///   <para>含义：某个控制参数（非图像对象类参数）的类别与算子声明不符。与 CTPL_WTYP=2400 的区别：那条针对元组数组的元素类型，本条针对控制参数本身 [待实测]。</para>
	///   <para>推荐处置：对照该参数期望的类别（整数/浮点/字符串）在构造实参处显式转换。</para>
	///   <para>族段：属 24xx 句柄与参数类型族的 2400–2402 参数类型子段，居其二。</para>
	/// </remarks>
	public const int Jl_ERR_CPAR_WTYP = 2401;

	/// <summary>元组数组索引错误，对应英文原文 "Wrong Hctuple index"。</summary>
	/// <remarks>
	///   <para>含义：对元组数组的取用下标越界或非法。C# 层为 0 基；原生侧索引口径是否一致仓库内未证实 [待实测]——见到此码先怀疑基数混用。</para>
	///   <para>推荐处置：先按元组实际长度校验下标再取用。</para>
	///   <para>族段：属 24xx 参数类型子段（2400–2402）末位。同前缀码区分：WTYP=2400 是类型错，本码是下标错；2403（WFV）起转入文件与句柄类型话题。</para>
	/// </remarks>
	public const int Jl_ERR_CTPL_WIDX = 2402;

	/// <summary>文件版本错误：读入文件的内部版本号与当前运行时可解析版本不符，对应英文原文 "Wrong version of file"。</summary>
	/// <remarks>
	///   <para>含义：模型、工作流等序列化文件由不同版本生成，版本戳校验被拒；哪些文件类型带版本戳仓库内无清单 [待实测]。</para>
	///   <para>推荐处置：用与当前运行时同版本的工具重新生成文件，或升级运行时。与 ICHOV=2211 区分：那条是运行时与运行时版本冲突，本码是文件与运行时版本冲突。</para>
	///   <para>族段：属 24xx 族。特别提示：与 HW_WFV=2801 值不同——2801 专用于硬件信息文件的版本错，本码是通用文件版本码，不是同值别名。</para>
	/// </remarks>
	public const int Jl_ERR_WFV = 2403;

	/// <summary>句柄类型错误：传入的句柄不是算子期望的对象类型，对应英文原文 "Wrong handle type"。</summary>
	/// <remarks>
	///   <para>含义：句柄本身有效，但其登记类型与形参要求不符——如把区域句柄递给期望图像句柄的算子。本库 JlImage/JlRegion/JlXLD 等均为 JlObject 派生，编译期有类型检查，此码多出现在句柄经数值形式跨边界传递后错配的路径 [待实测]。</para>
	///   <para>推荐处置：核对实参对象与签名类型；不要把裸句柄数字在不同对象间转用。</para>
	///   <para>族段：属 24xx 句柄与参数类型族。相邻区分：WHDL=2450 是句柄查不到（无效），本码是句柄有效但类型不对；与 2460/2461（控制/图像大类错配）是否同域，仓库内无证据 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WRONG_HANDLE_TYPE = 2404;

	/// <summary>向量类型错误，对应英文原文 "wrong vector type"。</summary>
	/// <remarks>
	///   <para>含义：数值向量的元素类型与算子要求不符。"向量"与 2400–2402 的"元组数组"在本库的边界模板英文未细分，推断向量指底层数值数组 [待实测]。</para>
	///   <para>推荐处置：确认算子要整数向量还是浮点向量，先在构造处显式转换。</para>
	///   <para>族段：属 24xx 向量子段（2410–2411），与 WVDIM=2411 成对：一个类型错、一个维数错；2404 与本段之间 2405–2409 缺号。</para>
	/// </remarks>
	public const int Jl_ERR_WVTYP = 2410;

	/// <summary>向量维数错误，对应英文原文 "wrong vector dimension"。</summary>
	/// <remarks>
	///   <para>含义：向量的长度/维度数与算子期望不符，例如把平面坐标向量递给要求三维分量的接口。</para>
	///   <para>推荐处置：对照文档核对各向量实参的分量数，补齐或裁剪后再传。</para>
	///   <para>族段：属 24xx 向量子段末位；WVTYP=2410 是元素类型错，本码是分量个数错。</para>
	/// </remarks>
	public const int Jl_ERR_WVDIM = 2411;

	/// <summary>句柄未知/无效，对应英文原文 "Wrong (unknown) Vision handle"。</summary>
	/// <remarks>
	///   <para>含义：句柄号在运行时句柄表中不存在——已释放对象被复用、跨进程/跨会话句柄失效是常见来源。本库 JlObject 系实现 IDisposable，Dispose 之后再进原生调用即属此类风险 [待实测]。</para>
	///   <para>推荐处置：梳理对象生命周期，确保释放后不再有引用存活到调用点。</para>
	///   <para>族段：24xx 句柄状态子段（2450–2456）由此码开篇——2450 未知、2451 有号无数据、2452 号越界、2453 空、2454 已清除、2455 不可序列化、2456 引用成环。</para>
	/// </remarks>
	public const int Jl_ERR_WHDL = 2450;

	/// <summary>运行时 ID 错误、取不到数据，对应英文原文 "Wrong Vision id, no data available"。</summary>
	/// <remarks>
	///   <para>含义：ID 能被运行时认识但按其取数时数据缺失/不可用；与 WHDL=2450（句柄根本查不到）相比多了一层"注册在、数据空"的状态 [待实测]。</para>
	///   <para>推荐处置：确认对象是否只完成了一半初始化或数据已被清走，必要时重建对象再调用。</para>
	///   <para>族段：属 24xx 句柄状态子段（2450–2456）第二位。相邻区分：IDOOR=2452 连号的数值区间都不合法，本码是号合法但无数据。</para>
	/// </remarks>
	public const int Jl_ERR_WID = 2451;

	/// <summary>运行时 ID 越界，对应英文原文 "Vision id out of range"。</summary>
	/// <remarks>
	///   <para>含义：ID 数值本身超出句柄/ID 空间的合法区间，是最粗粒度的校验——连号段都不在合法范围内；区间边界仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：怀疑变量溢出、未初始化或序列化错位；不要把某进程里得到的裸 ID 数字搬到另一进程复用。</para>
	///   <para>族段：属 24xx 句柄状态子段。与 2450（查不到）、2451（有号无数据）区分：那两条号本身合法，本码号就不合法。</para>
	/// </remarks>
	public const int Jl_ERR_IDOOR = 2452;

	/// <summary>句柄为空，对应英文原文 "Handle is NULL"。</summary>
	/// <remarks>
	///   <para>含义：把从未持有句柄的对象（未构造/未装载完成的空壳）传入需要对象句柄的算子。</para>
	///   <para>推荐处置：调用前判空；"先声明后填充"的写法要保证填充成功后才进调用路径。</para>
	///   <para>族段：属 24xx 句柄状态子段。相邻区分：WHDL=2450 是"有值但认识不了"，HANDLE_CLEARED=2454 是"曾经有效后被清除"，本码是"从头就空"。</para>
	/// </remarks>
	public const int Jl_ERR_HANDLE_NULL = 2453;

	/// <summary>句柄已被清除，对应英文原文 "Handle was cleared"。</summary>
	/// <remarks>
	///   <para>含义：句柄曾经有效、其后登记被运行时清除，再引用即命中——即 use-after-free 类错误；与本库 JlObject.Dispose 语义的对应关系未在仓库中直接证实 [待实测]。</para>
	///   <para>推荐处置：理清释放时点，不要让引用跨过清理点存活；确需再用就重新获取对象。</para>
	///   <para>族段：属 24xx 句柄状态子段。相邻区分：2453 是"一直为空"，本码是"曾经有过后被清除"。</para>
	/// </remarks>
	public const int Jl_ERR_HANDLE_CLEARED = 2454;

	/// <summary>句柄类型不支持序列化，对应英文原文 "Handle type does not serialize"。</summary>
	/// <remarks>
	///   <para>含义：尝试对一个运行时未定义写出格式的对象做序列化/持久化——句柄本身有效，错在"这类型录不了"；哪些句柄类型在禁录之列仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：不要序列化句柄本体，改为导出其数值属性/参数，或在装载端重新构造该对象。</para>
	///   <para>族段：属 24xx 句柄状态子段（2450–2456）第六位。相邻区分：WHDL=2450 是"认识不了这个号"，2453/2454 是句柄空/已被清除，本码是"句柄正常但存不了"；族末 2456（CYCLES）是能存但成环。</para>
	/// </remarks>
	public const int Jl_ERR_HANDLE_NOSER = 2455;

	/// <summary>检测到句柄间存在引用环，对应英文原文 "Reference cycles of handles found"。</summary>
	/// <remarks>
	///   <para>含义：序列化深拷贝时发现句柄引用图里存在"A 引用 B、B 又（直接或经中间对象间接）引用回 A"的环，递归写出无法终止；哪些对象类型之间可能成环仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：导出前断环——把其中一条回指改成外部标识（编号/名字），装载后再按标识恢复引用。</para>
	///   <para>族段：属 24xx 句柄状态子段（2450–2456）末位。相邻区分：NOSER=2455 是类型本身不可序列化，本码是类型可序列化但数据拓扑成环。</para>
	/// </remarks>
	public const int Jl_ERR_HANDLE_CYCLES = 2456;

	/// <summary>类型错配：该参数位要求控制量，实际传入图像/区域等图标对象，对应英文原文 "Type mismatch: Control expected, found iconic"。</summary>
	/// <remarks>
	///   <para>含义：算子形参槽要的是控制数据（数值、字符串、元组一类），实参却是图标对象（image/region/XLD 等）的句柄——常见诱因是重载选错或实参顺序错位。</para>
	///   <para>推荐处置：先核对所调重载的形参类型；若本意是用图标对象里的数据，先把数值属性提取成元组再传。</para>
	///   <para>族段：属 24xx 类型错配子段（2460/2461），与 2461 互为反方向：本码是"要控制、给了图标"。</para>
	/// </remarks>
	public const int Jl_ERR_WT_CTRL_EXPECTED = 2460;

	/// <summary>类型错配：该参数位要求图标对象，实际传入控制量，对应英文原文 "Type mismatc: Iconic expected, control found"（原文 mismatc 系笔误）。</summary>
	/// <remarks>
	///   <para>含义：算子形参槽要的是图像/区域等图标句柄，实参却是数值/字符串/元组这类控制数据——与 2460 反方向。常见诱因是把属性取出的裸数值又回填进图标形参，或重载选错。</para>
	///   <para>推荐处置：核对所调重载的形参类型；需要图标对象就先把数据装回 JlImage/JlRegion 等句柄再传，别指望运行时帮你隐式造图标对象。</para>
	///   <para>族段：属 24xx 类型错配子段（2460/2461）末位。</para>
	/// </remarks>
	public const int Jl_ERR_WT_ICONIC_EXPECTED = 2461;

	/// <summary>用扩展接口机制构建的扩展，其 Init 入口未被调用，对应英文原文 "extension api Init function of an extension that was build with xpi was not called"（原文中的 * 为注释换行残渣）。</summary>
	/// <remarks>
	///   <para>含义：某扩展是按扩展接口规范编译的，运行时等它的注册 Init 函数执行完才放行，但该函数一次都没被调到——通常是扩展没有通过规定入口装载、或首次调用发生在装载流程完成之前；本仓库 C# 层用哪条入口装载扩展无记录 [待实测]。</para>
	///   <para>推荐处置：确认扩展装载流程完整跑完后再调用其算子；核对扩展与运行时的装载顺序，必要时对照 22xx 族 UEXTNI=2205（扩展未装好）排查。</para>
	///   <para>族段：属 25xx 扩展接口（XPI）子段（2500–2508）首位。相邻区分：NO_INIT_FOUND=2501 是"连 Init 函数符号都找不到"，本码是"符号在但没执行到"。</para>
	/// </remarks>
	public const int Jl_ERR_XPI_INIT_NOT_CALLED = 2500;

	/// <summary>原生库在对接的扩展里找不到 Init 函数，对应英文原文 "native library didn't find the init function of the extension it is connecting to -> old extension without extension api or the function export failed"（原文中的 * 为注释换行残渣）。</summary>
	/// <remarks>
	///   <para>含义：英文原文自带两种成因——扩展是老产物、根本没实现扩展接口；或扩展实现了但 Init 函数没有正确导出（构建/链接配置漏了导出）。</para>
	///   <para>推荐处置：先用导出表工具核对扩展二进制里 Init 符号是否导出；确属无扩展接口的老扩展，则按当前接口规范重编一版。</para>
	///   <para>族段：属 25xx 扩展接口子段第二位。相邻区分：INIT_NOT_CALLED=2500 是"找得到符号但没执行"，本码是"符号都没有"。</para>
	/// </remarks>
	public const int Jl_ERR_XPI_NO_INIT_FOUND = 2501;

	/// <summary>扩展接口里有未解析（链接不上）的函数，对应英文原文 "Unresolved function in extension api"。</summary>
	/// <remarks>
	///   <para>含义：扩展与原生库对接时，某个接口函数的符号没能解析到——多半是扩展按另一版本接口编译、函数名/序号与当前原生库对不上；具体是哪个函数原文未指明 [待实测]。</para>
	///   <para>推荐处置：用与当前运行时同版本的接口头文件/库重新编译扩展；不要跨版本混放扩展与原生库。</para>
	///   <para>族段：属 25xx 扩展接口子段第三位。相邻区分：2500/2501 都聚焦 Init 这一个入口，本码泛指任意接口函数解析失败。</para>
	/// </remarks>
	public const int Jl_ERR_XPI_UNRES = 2502;

	/// <summary>扩展要求的运行时版本比当前原生库更新，对应英文原文 "Vision extension requires a Vision version that is newer than the connected native library"（原文中的 * 为注释换行残渣）。</summary>
	/// <remarks>
	///   <para>含义：扩展声明它需要一个比当前所接原生库更高的 Vision 版本——扩展是为新版本编译的，装到了旧原生库上，接口版本协商失败。</para>
	///   <para>推荐处置：升级原生库到扩展要求的版本，或改用匹配当前运行时的旧版扩展；二者取其一，别硬配。</para>
	///   <para>族段：属 25xx 扩展接口子段第四位。相邻区分：本码（LIB_TOO_OLD）指"原生库太旧"；XPI_TOO_OLD=2504 指"对接扩展所用接口主版本太旧"；2505/2506 再从主/次版本两个粒度指"原生库侧接口版本太小"。</para>
	/// </remarks>
	public const int Jl_ERR_XPI_LIB_TOO_OLD = 2503;

	/// <summary>对接扩展所使用扩展接口的主版本对原生库而言太低，对应英文原文 "the (major) version of the extension api which is used by the connecting extension is too small for native library"（原文中的 * 为注释换行残渣）。</summary>
	/// <remarks>
	///   <para>含义：版本错配方向在扩展侧——扩展编译时链接的扩展接口主版本太旧，当前原生库不再接受这套旧协议。</para>
	///   <para>推荐处置：用当前版本的接口套件重新编译扩展；升级原生库解决不了本码（问题就在扩展太旧）。</para>
	///   <para>族段：属 25xx 扩展接口子段第五位。相邻区分：主语是"来对接的扩展"；2505/2506 的主语反过来是"原生库自身的接口版本"。</para>
	/// </remarks>
	public const int Jl_ERR_XPI_XPI_TOO_OLD = 2504;

	/// <summary>原生库自身使用的扩展接口主版本太低，对应英文原文 "the major version of the extension api which is used by the native library is too small"（原文中的 * 为注释换行残渣）。</summary>
	/// <remarks>
	///   <para>含义：错配在原生库侧——它带出来的扩展接口主版本低于对接扩展的最低要求。与 2504 主语相反：2504 是扩展旧，本码是原生库旧。</para>
	///   <para>推荐处置：升级原生库（整体运行时）到扩展接口主版本达标的版本。</para>
	///   <para>族段：属 25xx 扩展接口子段第六位。相邻区分：本码看主版本（major），MINOR_TOO_SMALL=2506 看次版本（minor）。</para>
	/// </remarks>
	public const int Jl_ERR_XPI_MAJOR_TOO_SMALL = 2505;

	/// <summary>原生库自身使用的扩展接口次版本太低，对应英文原文 "the minor version of the extension api which is used by the native library is too small"（原文中的 * 为注释换行残渣）。</summary>
	/// <remarks>
	///   <para>含义：主版本已达标，但扩展接口次版本低于对接扩展要求——通常扩展用到了较新次版本才引入的接口能力，而原生库次版本偏旧。</para>
	///   <para>推荐处置：小版本升级原生库；或让扩展退回不使用高次版本接口能力重编。</para>
	///   <para>族段：属 25xx 扩展接口子段第七位。相邻区分：与 2505 成对，本码是次版本（minor）不足。</para>
	/// </remarks>
	public const int Jl_ERR_XPI_MINOR_TOO_SMALL = 2506;

	/// <summary>符号结构体内主版本号错误（原文注明属内部错误、正常不应发生），对应英文原文 "Wrong major version in symbol struct (internal: should not happen)"（原文中的 * 为注释换行残渣）。</summary>
	/// <remarks>
	///   <para>含义：接口符号结构里登记的主版本字段与期望不符。原文自注 "internal: should not happen"——这不是用户配置能触发的版本错配，而是运行时内部一致性被破坏，多见于二进制被篡改、混装或内存越界写坏结构 [待实测]。</para>
	///   <para>推荐处置：先排除混合版本文件与异常终止残留；稳定复现则记录版本信息求助支持，用户侧无可靠修法。</para>
	///   <para>族段：属 25xx 扩展接口子段第八位。相邻区分：2503–2506 都是可解释的版本高低错配，本码是内部校验断言失败。</para>
	/// </remarks>
	public const int Jl_ERR_XPI_INT_WRONG_MAJOR = 2507;

	/// <summary>探测不出 JlLib（原生库）版本，对应英文原文 "JlLib version could not be detected"。</summary>
	/// <remarks>
	///   <para>含义：版本协商的前置一步就失败了——运行时拿不到原生库自身的版本标识（探测机制仓库内无记录 [待实测]），后面的 2503–2506 各项版本比较自然都无从谈起。</para>
	///   <para>推荐处置：确认加载的是完整、未被替换过的原生库文件，程序指向正确的运行时安装目录；修复装载环境后重试。</para>
	///   <para>族段：属 25xx 扩展接口子段末位。相邻区分：2507 是"版本字段错了"，本码是"版本读不出来"。</para>
	/// </remarks>
	public const int Jl_ERR_XPI_UNKNOWN_LIB_VER = 2508;

	/// <summary>硬件信息文件格式错误，对应英文原文 "Wrong hardware information file format"。</summary>
	/// <remarks>
	///   <para>含义：运行时解析硬件信息（knowledge）文件时连基本版式都没读通——文件不是该格式、被编辑坏/截断，或是别的程序生成的伪文件；具体解析规则仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：用同版本工具重新生成该文件；确要手工改，先与已知正常的原件逐段比对结构。</para>
	///   <para>族段：属 28xx 硬件知识子段（2800–2813）首位。相邻区分：本码是"格式读不通"，WFV=2801 是"格式能读、版本号不接受"。</para>
	/// </remarks>
	public const int Jl_ERR_HW_WFF = 2800;

	/// <summary>硬件信息文件版本错误，对应英文原文 "Wrong hardware information file version"。</summary>
	/// <remarks>
	///   <para>含义：文件版式能读懂，但其声明的文件版本号不被当前运行时接受（过新或过旧，兼容策略仓库内无记录 [待实测]）。</para>
	///   <para>推荐处置：用当前版本工具重生成该文件，或把运行时升到能读该版本的文件；不要手改文件里的版本数字来冒充兼容。</para>
	///   <para>族段：属 28xx 硬件知识子段（2800–2813）第二位。相邻区分：与 2800 互补——那是格式错，本码是版本错。</para>
	/// </remarks>
	public const int Jl_ERR_HW_WFV = 2801;

	/// <summary>读取硬件知识（hardware knowledge）时出错，对应英文原文 "Error while reading the hardware knowledge"。</summary>
	/// <remarks>
	///   <para>含义：读取阶段的失败——文件不存在、无权限、被占用或读一半损坏；与 2800/2801 的区别是本码侧重"读动作本身失败"而非内容版式/版本判错。</para>
	///   <para>推荐处置：先解决文件路径、权限、占用问题再重试读；确认硬件知识文件完整可读。</para>
	///   <para>族段：属 28xx 硬件知识子段（2800–2813）第三位。相邻区分：本码是读方向，WF=2803 是写方向。</para>
	/// </remarks>
	public const int Jl_ERR_HW_RF = 2802;

	/// <summary>写入硬件知识（hardware knowledge）时出错，对应英文原文 "Error while writing the hardware knowledge"。</summary>
	/// <remarks>
	///   <para>含义：写回硬件知识的 IO 失败——目标目录无写权限、文件被占用或磁盘满；写入落点如何决定仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：检查写出路径的权限与占用、磁盘余量后重试；若运行账户写不进安装目录，改用有写权限的位置或提权运行。</para>
	///   <para>族段：属 28xx 硬件知识子段（2800–2813）第四位。相邻区分：与 2802 反向（写对读）；名字与 2800（WFF）近但语义无关，那是格式错。</para>
	/// </remarks>
	public const int Jl_ERR_HW_WF = 2803;

	/// <summary>按标签取值时未找到该 Tag，对应英文原文 "Tag not found"。</summary>
	/// <remarks>
	///   <para>含义：在硬件知识数据里查某个字段/标签（tag），该键不存在——条目缺失或键名拼错；合法的 tag 字典仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：对照同版本完整文件核实该 tag 是否应有、名字是否一致；不要凭猜测往文件里塞新键。</para>
	///   <para>族段：属 28xx 硬件知识子段（2800–2813）第五位。相邻区分：WTD=2811 是"tag 在但派生类型不对"，本码是"tag 压根没有"。</para>
	/// </remarks>
	public const int Jl_ERR_HW_TF = 2804;

	/// <summary>取不到 CPU 信息，对应英文原文 "No CPU Info"。</summary>
	/// <remarks>
	///   <para>含义：运行时探测本机 CPU 信息（型号/核数/特性等）失败或结果为空，硬件知识因此建不起来；虚拟化/受限环境下探测接口不可用是常见诱因 [探测途径待实测]。</para>
	///   <para>推荐处置：先在常规桌面环境复现；若在虚拟机/容器里稳定出现，多半是该环境未暴露 CPU 特性信息，需换探测方式或补环境。</para>
	///   <para>族段：属 28xx 硬件知识子段（2800–2813）第六位。相邻区分：本码是机器 CPU 层信息缺失，2806 起进入 AOP 信息组（AOP 缩写全称仓库内无记录 [待实测]）。</para>
	/// </remarks>
	public const int Jl_ERR_HW_CPU = 2805;

	/// <summary>无 AOP 信息，对应英文原文 "No AOP Info"。</summary>
	/// <remarks>
	///   <para>含义：硬件运行时信息里 AOP 这一类记录整体缺失（AOP 的确切所指仓库内无记录，按原文词面理解 [待实测]）。</para>
	///   <para>推荐处置：在当前机器上重跑信息收集、重建硬件知识文件；并确认安装的组件确实提供该类信息。</para>
	///   <para>族段：属 28xx AOP 信息组（2806–2810）首位。相邻区分：2807/2808/2809 是"有记录但对当前变体/架构/算子不适用"，本码是"完全没有"。</para>
	/// </remarks>
	public const int Jl_ERR_HW_AOP = 2806;

	/// <summary>当前 Vision 变体无对应 AOP 信息，对应英文原文 "No AOP Info for this Vision variant"。</summary>
	/// <remarks>
	///   <para>含义：信息文件里有 AOP 记录，但没有属于当前所跑产品变体的那一份（变体划分清单仓库内无记录 [待实测]）。</para>
	///   <para>推荐处置：核对跑的是哪个产品变体、信息文件是哪个变体生成的；必要时用当前变体重建信息文件。</para>
	///   <para>族段：属 28xx AOP 信息组（2806–2810）第二位。相邻区分：本码按变体维度缺，2808 按架构维度缺，2809 按指定算子维度缺。</para>
	/// </remarks>
	public const int Jl_ERR_HW_HVAR = 2807;

	/// <summary>当前 Vision 架构无对应 AOP 信息，对应英文原文 "No AOP Info for this Vision architecture"。</summary>
	/// <remarks>
	///   <para>含义：AOP 记录存在，但没有属于当前 CPU/体系结构（如 x86/x64/ARM）的那份——常因把别的架构生成的信息文件拿到本机复用。</para>
	///   <para>推荐处置：在当前架构上重新生成信息文件；跨架构部署时按目标机各自的架构分别准备。</para>
	///   <para>族段：属 28xx AOP 信息组（2806–2810）第三位。相邻区分：与 2807（变体维度）、2809（算子维度）并列，本码专指架构维度。</para>
	/// </remarks>
	public const int Jl_ERR_HW_HARCH = 2808;

	/// <summary>未找到指定算子的 AOP 信息，对应英文原文 "No AOP Info for specified Operator found"。</summary>
	/// <remarks>
	///   <para>含义：为某个被点名的算子查询 AOP 信息，结果没有——该算子不在已登记的 AOP 范围内，或算子名/编号写错。</para>
	///   <para>推荐处置：确认算子确实属于当前安装且支持并行；核对算子标识。这是三个维度里最细的一档（变体→架构→算子）。</para>
	///   <para>族段：属 28xx AOP 信息组（2806–2810）第四位。相邻区分：本码专指"指定算子"级缺失；WAOPM=2810 是引用的 AOP 模型本身未定义。</para>
	/// </remarks>
	public const int Jl_ERR_HW_HOP = 2809;

	/// <summary>引用了未定义的 AOP 模型，对应英文原文 "undefined AOP model"。</summary>
	/// <remarks>
	///   <para>含义：用到的 AOP 模型标识不在已知模型枚举里——模型定义与引用它的信息文件版本不一致。模型枚举清单仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：用同版本工具重建/替换硬件知识文件，使模型定义与引用一致；勿沿用旧文件里的模型名。</para>
	///   <para>族段：属 28xx AOP 信息组（2806–2810）末位。相邻区分：2809 是算子级信息缺失，本码是模型级枚举对不上。</para>
	/// </remarks>
	public const int Jl_ERR_HW_WAOPM = 2810;

	/// <summary>标签派生错误，对应英文原文 "wrong tag derivate"。</summary>
	/// <remarks>
	///   <para>含义：tag 存在，但按其派生出的类型/取值与期望不符（"derivate" 的确切派生规则原文未展开、仓库内无记录 [待实测]）。</para>
	///   <para>推荐处置：对照同版本正常原件修该 tag 条目的类型/值；手工改前先备份。</para>
	///   <para>族段：属 28xx 硬件知识子段（2800–2813）第十二位。相邻区分：TF=2804 是"tag 不存在"，本码是"tag 在但派生不对"。</para>
	/// </remarks>
	public const int Jl_ERR_HW_WTD = 2811;

	/// <summary>硬件知识子模块内部错误，对应英文原文 "internal error"。</summary>
	/// <remarks>
	///   <para>含义：原文只有泛化的 "internal error" 四字，唯一线索是它所在的硬件知识族——具体触发点无从判定 [待实测]，它是族内的兜底/意外码。</para>
	///   <para>推荐处置：用户侧无法定位修复；记录完整操作序列与文件状态求助支持，并先排除 2800–2811 各具体码是否才是真正的因。</para>
	///   <para>族段：属 28xx 硬件知识子段（2800–2813）第十三位。相邻区分：其余码都对应可解释的格式/信息问题，本码是内部异常。</para>
	/// </remarks>
	public const int Jl_ERR_HW_IE = 2812;

	/// <summary>硬件检查被取消，对应英文原文 "hw check was canceled"。</summary>
	/// <remarks>
	///   <para>含义：硬件检查流程在跑完之前被主动中止（用户中断、超时或上层取消，具体取消来源仓库内无记录 [待实测]）；它不是数据错，文件可能完好。</para>
	///   <para>推荐处置：先分清是否有意取消；非预期时查上层超时/取消逻辑，再连同并行检查相关码（2838/2839）一起复跑定位。</para>
	///   <para>族段：属 28xx 硬件知识子段（2800–2813）末位。相邻区分：2800–2812 都是"数据/内部出错"，本码是"流程被掐停"。</para>
	/// </remarks>
	public const int Jl_ERR_HW_CANCEL = 2813;

	/// <summary>对全局变量的访问方式错误，对应英文原文 "Wrong access to global variable"。</summary>
	/// <remarks>
	///   <para>含义：全局变量存在，但访问方式不对——对只读项做写、在不容许的状态下操作等；本库 C# 层如何暴露全局变量机制仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：核对读/写调用方向与参数槽是否匹配；确认该变量在当前阶段是否允许改写。</para>
	///   <para>族段：属 28xx 全局变量子段（2830–2832）首位。相邻区分：本码是"方式错"，2831 是"变量不存在"，2832 是"存在但 GLOBAL_ID 途径够不着"。</para>
	/// </remarks>
	public const int Jl_ERR_GV_WA = 2830;

	/// <summary>所用的全局变量不存在，对应英文原文 "Used global variable does not exist"。</summary>
	/// <remarks>
	///   <para>含义：把某名字当全局变量引用，但它在当前时刻未定义——尚未创建、已被删除，或大小写/拼写不符。</para>
	///   <para>推荐处置：先创建并赋值再引用；多线程下核对变量创建与使用的先后时序。</para>
	///   <para>族段：属 28xx 全局变量子段（2830–2832）第二位。相邻区分：NG=2832 是"变量在但走 GLOBAL_ID 取不到"，本码是"变量本身不存在"。</para>
	/// </remarks>
	public const int Jl_ERR_GV_NC = 2831;

	/// <summary>全局变量无法通过 GLOBAL_ID 访问，对应英文原文 "Used global variable not accessible via GLOBAL_ID"。</summary>
	/// <remarks>
	///   <para>含义：变量确实存在，但用 GLOBAL_ID 这条途径取不到——它的类型/作用域未通过该访问方式暴露；GLOBAL_ID 的确切语义仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：不走 GLOBAL_ID，直接把值作为普通实参传递；确需共享时换用受支持的句柄传参方式。</para>
	///   <para>族段：属 28xx 全局变量子段（2830–2832）末位。相邻区分：与 2831 分界——那是"没有"，本码是"有但这条门路进不去"。</para>
	/// </remarks>
	public const int Jl_ERR_GV_NG = 2832;

	/// <summary>要终止的 Vision 服务器仍在处理作业，对应英文原文 "Vision server to terminate is still working on a job"。</summary>
	/// <remarks>
	///   <para>含义：请求终止某服务器进程时，它手上还有未跑完的作业，于是终止被拒绝——这是保护性拒绝，不是崩溃。</para>
	///   <para>推荐处置：先等待或取消正在进行的作业，再下终止命令；是否有强制终止途径仓库内无记录 [待实测]。</para>
	///   <para>族段：属 28xx 服务器/代理管理子段（2835/2837）首位。相邻区分：NA=2837 是"所指的 agent 根本不存在"，本码是"存在但不肯现在退场"。</para>
	/// </remarks>
	public const int Jl_ERR_HM_NT = 2835;

	/// <summary>不存在该 Vision 软件代理（agent），对应英文原文 "No such Vision software agent"。</summary>
	/// <remarks>
	///   <para>含义：管理命令点名的 agent 不在当前登记表里——名字/编号写错、尚未启动或已注销。</para>
	///   <para>推荐处置：先枚举当前可用 agent 确认目标，再对其下命令；并行场景对照 2854/2855（名称/地址未知）定位。</para>
	///   <para>族段：属 28xx 服务器/代理管理子段（2835/2837）末位，紧邻其后的 2838 起进入 AG 并行族。</para>
	/// </remarks>
	public const int Jl_ERR_HM_NA = 2837;

	/// <summary>单处理器机器上无法做并行化硬件检查，对应英文原文 "Hardware check for parallelization not possible on a single-processor machine"。</summary>
	/// <remarks>
	///   <para>含义：在判定为单处理器的机器上发起了并行硬件检查——并行化在这种机器上没有可分配对象，检查无意义；判定核数走哪条途径仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：单核机不要发起并行检查；若机器实际为多核却命中此码，先与 2868（取处理器数失败）核对是不是数错了核。</para>
	///   <para>族段：属 28xx 并行化/代理（AG）子段（2838–2859）首位。相邻区分：NC=2839 是"顺序版产品不支持"，本码是"机器硬件不够"。</para>
	/// </remarks>
	public const int Jl_ERR_AG_CN = 2838;

	/// <summary>顺序版（Seq.）Vision 不支持并行硬件检查，对应英文原文 "(Seq.) Vision does not support parallel hardware check (use Parallel Vision instead)"。</summary>
	/// <remarks>
	///   <para>含义：当前运行的是顺序（非并行）版本产品，并行硬件检查属于并行版能力。两种产品形态在本仓库的判定界限无记录 [待实测]。</para>
	///   <para>推荐处置：按原文提示改用并行版；或在顺序版流程里去掉这次并行检查，不要指望配置能打开。</para>
	///   <para>族段：属 28xx 并行化/代理（AG）子段（2838–2859）第二位。相邻区分：2838 是硬件不够，本码是产品形态不支持，二者都非配置可解。</para>
	/// </remarks>
	public const int Jl_ERR_AG_NC = 2839;

	/// <summary>代理（agent）初始化失败，对应英文原文 "Initialization of agent failed"。</summary>
	/// <remarks>
	///   <para>含义：某个并行工作 agent 在启动初始化阶段没起来——具体卡在启动引导的哪一步原文未细分 [待实测]。</para>
	///   <para>推荐处置：核对 agent 可执行文件与其信息/知识文件是否完好（对照 2843/2844/2847），以及机器是否有足够资源起工作者。</para>
	///   <para>族段：属 28xx AG 子段第四位。相邻区分：NT=2841 是终止失败，本码是启动失败——同一生命周期的两端。</para>
	/// </remarks>
	public const int Jl_ERR_AG_IN = 2840;

	/// <summary>代理（agent）终止失败，对应英文原文 "Termination of agent failed"。</summary>
	/// <remarks>
	///   <para>含义：agent 退出流程没走完，可能是工作者挂起/占用未释放；运行时对残留进程是否强制回收仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：先查有没有未完成的作业（对照 2835），再重试终止；仍失败则连同宿主进程一并重启收拾。</para>
	///   <para>族段：属 28xx AG 子段第五位。相邻区分：与 2840 成"启/停"对；服务器级的"拒绝终止"见 2835。</para>
	/// </remarks>
	public const int Jl_ERR_AG_NT = 2841;

	/// <summary>硬件描述文件不一致，对应英文原文 "Inconsistent hardware description file"。</summary>
	/// <remarks>
	///   <para>含义：并行调度所依赖的硬件描述文件内容自相矛盾（字段冲突、与实际机器对不上），调度器无法信任它。</para>
	///   <para>推荐处置：用同版本工具在目标机上重新生成硬件描述文件，别手工缝补。</para>
	///   <para>族段：属 28xx AG 子段第六位，"不一致文件"组之首。相邻区分：本组四个码按文件类型分——2842 硬件描述、2843 代理信息、2844 代理知识、2847 代理知识库。</para>
	/// </remarks>
	public const int Jl_ERR_AG_HW = 2842;

	/// <summary>代理信息文件不一致，对应英文原文 "Inconsistent agent information file"。</summary>
	/// <remarks>
	///   <para>含义：agent 自身信息文件里字段冲突、或与运行时实际登记对不上。</para>
	///   <para>推荐处置：同版本工具重新导出该文件；结合 2854/2855（名字/地址未知）定位是哪条登记坏掉。</para>
	///   <para>族段：属 28xx AG 子段第七位，"不一致文件"组第二位。相邻区分：2842 指硬件描述，本码指代理信息。</para>
	/// </remarks>
	public const int Jl_ERR_AG_II = 2843;

	/// <summary>代理知识文件不一致，对应英文原文 "Inconsistent agent knowledge file"。</summary>
	/// <remarks>
	///   <para>含义：agent 知识文件损坏或内部矛盾。它与 2847（知识库不一致）的分工原文未细说——是否文件与库是两层，仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：重建代理知识文件；分不清是文件级还是库级时，把 2844 与 2847 一并排查。</para>
	///   <para>族段：属 28xx AG 子段第八位，"不一致文件"组第三位。相邻区分：见 2843 处对本组的划分说明。</para>
	/// </remarks>
	public const int Jl_ERR_AG_IK = 2844;

	/// <summary>并行化信息文件与当前版本/修订号不匹配，对应英文原文 "The file with the parallelization information does not match to the currently Vision version/revision"。</summary>
	/// <remarks>
	///   <para>含义：并行化信息文件声明的版本/修订号 ≠ 正在运行的版本/修订号——典型是升级了运行时却没重出该文件。</para>
	///   <para>推荐处置：用新版本工具重新生成并行化信息文件；不要去改文件里的版本数字硬凑。</para>
	///   <para>族段：属 28xx AG 子段第九位。相邻区分：WH=2846 是同一文件的"机器"维度不匹配，本码是"版本"维度不匹配。</para>
	/// </remarks>
	public const int Jl_ERR_AG_WV = 2845;

	/// <summary>并行化信息文件与当前所用机器不匹配，对应英文原文 "The file with the parallelization information does not match to the currently used machine"。</summary>
	/// <remarks>
	///   <para>含义：文件描述的是另一台机器的并行拓扑——从别的主机拷来，或本机改了硬件却没重出文件。</para>
	///   <para>推荐处置：在当前目标机重跑并行硬件检查再导出；这类文件不要跨机复制复用。</para>
	///   <para>族段：属 28xx AG 子段第十位。相邻区分：与 2845 成对，那边比版本、这边比机器。</para>
	/// </remarks>
	public const int Jl_ERR_AG_WH = 2846;

	/// <summary>Vision 软件代理的知识库不一致，对应英文原文 "Inconsistent knowledge base of Vision software agent"。</summary>
	/// <remarks>
	///   <para>含义：agent 知识库层面出现矛盾。它与 2844（知识文件不一致）的区别原文未界定，是否文件与库分两层仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：重建 agent 知识；若同时命中 2844，先修底层文件再看库是否自愈。</para>
	///   <para>族段：属 28xx AG 子段第十一位，"不一致文件"组末位（2842/2843/2844/2847）。</para>
	/// </remarks>
	public const int Jl_ERR_AG_KC = 2847;

	/// <summary>未知的通信类型，对应英文原文 "Unknown communication type"。</summary>
	/// <remarks>
	///   <para>含义：agent 间调度所用的通信方式取了一个当前运行时不认识的值——配置里的枚举越界或文件版本错位；支持的通信类型清单仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：按当前版本枚举值填写；与 2849（消息类型未知）分清是通信层还是消息层出错。</para>
	///   <para>族段：属 28xx AG 子段第十二位，通信/消息对之首。相邻区分：本码指传输方式，2849 指传输内容格式。</para>
	/// </remarks>
	public const int Jl_ERR_AG_CT = 2848;

	/// <summary>Vision 软件代理的消息类型未知，对应英文原文 "Unknown message type for Vision software agent"。</summary>
	/// <remarks>
	///   <para>含义：收到（或配置出）的消息协议标识不在已知列表里——版本间互通时常见（对照 2845）。它与 2848（通信类型）分处不同层面。</para>
	///   <para>推荐处置：让通信两端用同一版本运行时；只有一端可升时，先确认那端配置文件一致（2842–2847）。</para>
	///   <para>族段：属 28xx AG 子段第十三位，通信/消息对之次位。</para>
	/// </remarks>
	public const int Jl_ERR_AG_MT = 2849;

	/// <summary>保存并行化知识时出错，对应英文原文 "Error while saving the parallelization knowledge"。</summary>
	/// <remarks>
	///   <para>含义：把并行化知识落盘时写出失败——权限、磁盘满或目标文件被占用；保存落点如何决定仓库内无记录 [待实测]。与 2803 是同族"写失败"，但对象不同。</para>
	///   <para>推荐处置：检查输出路径权限与占用、磁盘余量后重跑保存流程。</para>
	///   <para>族段：属 28xx AG 子段第十四位，"信息类型错"三兄弟（2851–2853）之前的 IO 码。</para>
	/// </remarks>
	public const int Jl_ERR_AG_WK = 2850;

	/// <summary>工作信息（work information）类型错误，对应英文原文 "Wrong type of work information"。</summary>
	/// <remarks>
	///   <para>含义：并行知识中"工作信息"这一类条目的类型字段不符。与 2852（应用信息）、2853（经验信息）同族，分别指向调度知识的三类负载；三类各自的准确定义仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：重建并行化知识使三类信息对齐；不推荐手改单条。</para>
	///   <para>族段：属 28xx AG 子段第十五位，"信息类型"三兄弟（2851 work/2852 application/2853 experience）之首。</para>
	/// </remarks>
	public const int Jl_ERR_AG_WW = 2851;

	/// <summary>应用信息（application information）类型错误，对应英文原文 "Wrong type of application information"。</summary>
	/// <remarks>
	///   <para>含义：并行知识中"应用信息"这一类条目的类型字段不符，是 2851 三兄弟里的应用侧成员；其准确所指仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：同 2851——重建并行化知识保证三类一致，别手改单条。</para>
	///   <para>族段：属 28xx AG 子段第十六位，"信息类型"三兄弟第二位。相邻区分：WW=2851 指工作、WE=2853 指经验，本码指应用。</para>
	/// </remarks>
	public const int Jl_ERR_AG_WA = 2852;

	/// <summary>经验信息（experience information）类型错误，对应英文原文 "Wrong type of experience information"。</summary>
	/// <remarks>
	///   <para>含义：并行知识中"经验信息"（历史调度积累的经验数据）类型字段不符，是 2851 三兄弟里的经验侧成员；经验数据是否自动学习积累仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：重建或删除后重生成经验部分；若混入他机经验文件，检查文件来源。</para>
	///   <para>族段：属 28xx AG 子段第十七位，"信息类型"三兄弟末位。</para>
	/// </remarks>
	public const int Jl_ERR_AG_WE = 2853;

	/// <summary>Vision 软件代理的名字未知，对应英文原文 "Unknown name of Vision software agent"。</summary>
	/// <remarks>
	///   <para>含义：调度点名的 agent 名字不在登记表里——拼写/大小写错，或该 agent 未初始化（对照 2840）。与 2855（名字和地址都不明）不同，本码仅名字一条线索不匹配。</para>
	///   <para>推荐处置：先核对名字；确属未起，补做 agent 初始化再调度。</para>
	///   <para>族段：属 28xx AG 子段第十八位，"目标不明"对（2854/2855）之首。相邻区分：本码是"名字不对"，2855 是"名字和通信地址都不对"。</para>
	/// </remarks>
	public const int Jl_ERR_AG_NU = 2854;

	/// <summary>Vision 软件代理的名字与通信地址都不明，对应英文原文 "Unknown name and communication address of Vision software agent"。</summary>
	/// <remarks>
	///   <para>含义：两条定位途径（名字、通信地址）都找不到目标——大概率这个 agent 根本没启动/注册，而非单点笔误。</para>
	///   <para>推荐处置：先起好/登记目标 agent 再通信；与 2856（reachable 失败）分清"不存在"与"存在但联系不上"。</para>
	///   <para>族段：属 28xx AG 子段第十九位，"目标不明"对次位。</para>
	/// </remarks>
	public const int Jl_ERR_AG_NE = 2855;

	/// <summary>CPU 代表（软件代理）不可达，对应英文原文 "cpu representative (Vision software agent) not reachable"。</summary>
	/// <remarks>
	///   <para>含义："cpu representative"即代理——它在登记表里存在，但通信联系不上：进程死掉、网络阻断或端口被拦；它区别于 2855（压根不存在）。</para>
	///   <para>推荐处置：查远端进程存活与网络可达；不可达时不要继续向其分派作业。</para>
	///   <para>族段：属 28xx AG 子段第二十位，"通信不可达/拒接"对（2856/2857）之首。相邻区分：本码是连不上，2857 是连上了但拒活。</para>
	/// </remarks>
	public const int Jl_ERR_AG_RR = 2856;

	/// <summary>CPU（代理）拒绝接受作业，对应英文原文 "cpu refuses work"。</summary>
	/// <remarks>
	///   <para>含义：代理可达但明确表示不接活——满载、处于暂停/维护状态、或作业超出它声明的能力范围；具体拒活判定规则仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：与 2856（连不上）区别对待，本码通道正常；查该 agent 的负载与状态设置，改派他处或稍后重试。</para>
	///   <para>族段：属 28xx AG 子段第二十一位。相邻区分：2858（资源描述没登记）、2859（函数够不着）是登记/权限问题，本码是资源主观拒接。</para>
	/// </remarks>
	public const int Jl_ERR_AG_CR = 2857;

	/// <summary>未找到调度资源的描述，对应英文原文 "Description of scheduling resource not found"。</summary>
	/// <remarks>
	///   <para>含义：调度资源（CPU 槽/代理等）在描述表里没有登记条目——引用了一个未描述的资源。它与 2860/2861（资源类型/状态错）构成调度资源侧的错误面。</para>
	///   <para>推荐处置：重跑并行硬件检查以重建资源描述；引用前先确认资源已登记。</para>
	///   <para>族段：属 28xx AG 子段第二十二位。相邻区分：2842 是"描述在但矛盾"，本码是"没有描述"。</para>
	/// </remarks>
	public const int Jl_ERR_AG_RN = 2858;

	/// <summary>Vision 软件代理中存在不可访问的函数，对应英文原文 "Not accessible function of Vision software agent"。</summary>
	/// <remarks>
	///   <para>含义：调用了一个该 agent 没导出/当前模式不允许访问的函数——接口权限或版本能力问题；哪些函数按条件开放仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：核对调用端与 agent 版本是否一致（2845）；函数确应可用则查 agent 初始化是否成功（2840）。</para>
	///   <para>族段：属 28xx AG 子段（2838–2859）末位。相邻区分：2835/2837 是管理命令层，本码落在函数调用层。</para>
	/// </remarks>
	public const int Jl_ERR_AG_TILT = 2859;

	/// <summary>类型错误：Vision 调度资源，对应英文原文 "Wrong type: Vision scheduling resource"。</summary>
	/// <remarks>
	///   <para>含义：递给调度器的东西不是预期类型的资源（拿别的句柄/对象误充调度资源，或空引用）；合法的资源类型清单仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：核对句柄变量是否串了类型；本码管"类型不对"，状态不对归 2861。</para>
	///   <para>族段：属 28xx 调度资源子段（2860–2864）首位。相邻区分：本码类型面，2861 状态面，2862/2863 参数面，2864 后处理面。</para>
	/// </remarks>
	public const int Jl_ERR_WRT = 2860;

	/// <summary>状态错误：Vision 调度资源，对应英文原文 "Wrong state: Vision scheduling resource"。</summary>
	/// <remarks>
	///   <para>含义：资源类型对，但它当前处于不允许此操作的生命周期状态（未初始化、已停止、被占用等）；资源状态机有哪些态仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：先完成初始化/启动再调度；不要把已停用的资源句柄拿来复用。</para>
	///   <para>族段：属 28xx 调度资源子段（2860–2864）第二位。相邻区分：与 2860 成"类型/状态"两个失败面。</para>
	/// </remarks>
	public const int Jl_ERR_WRS = 2861;

	/// <summary>未知参数类型：Vision 调度资源，对应英文原文 "Unknown parameter type: Vision scheduling resource"。</summary>
	/// <remarks>
	///   <para>含义：传给调度资源的某个参数的类型标识不在已知列表里——参数打包出错或句柄错传。合法参数类型表仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：核对传参处的值类型与资源参数约定；先看类型（本码），再看取值（2863）。</para>
	///   <para>族段：属 28xx 调度资源子段（2860–2864）第三位，参数对（2862/2863）之首。相邻区分：本码看类型，2863 看值。</para>
	/// </remarks>
	public const int Jl_ERR_UNKPT = 2862;

	/// <summary>未知参数值：Vision 调度资源，对应英文原文 "Unknown parameter value: Vision scheduling resource"。</summary>
	/// <remarks>
	///   <para>含义：参数类型对，但取值不是允许的枚举值/越界选项。值域表仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：换成文档明示的合法值；若确信取值有效，先核对版本（对照 2845）。</para>
	///   <para>族段：属 28xx 调度资源子段（2860–2864）第四位，参数对次位。相邻区分：与 2862 成"类型/值"两级。</para>
	/// </remarks>
	public const int Jl_ERR_UNKPARVAL = 2863;

	/// <summary>控制参数的后处理错误，对应英文原文 "Wrong post processing of control parameter"。</summary>
	/// <remarks>
	///   <para>含义：控制参数在校验通过后、进入后续处理阶段（转换/展开/绑定等）出错；"后处理"具体含哪些步骤仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：简化传参组合定位——先只留必要参数，再逐个加回找出触发项。</para>
	///   <para>族段：属 28xx 调度资源子段（2860–2864）末位。相邻区分：2862/2863 在校验层面，本码在处理层面。</para>
	/// </remarks>
	public const int Jl_ERR_CTRL_WPP = 2864;

	/// <summary>获取时间失败，对应英文原文 "Error while trying to get time"。</summary>
	/// <remarks>
	///   <para>含义：运行时取时间用的底层调用返回错误——系统时钟服务异常或平台计时接口不可用；用的是系统时钟还是高精度计数器仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：确认操作系统时间服务正常后重试；虚拟化/时间同步软件环境下排查干扰。</para>
	///   <para>族段：属 28xx 系统查询杂项子段（2867–2869）首位。相邻区分：2868 查处理器数、2869 临时文件，本码查时间。</para>
	/// </remarks>
	public const int Jl_ERR_GETTI = 2867;

	/// <summary>获取处理器数量失败，对应英文原文 "Error while trying to get the number of processors"。</summary>
	/// <remarks>
	///   <para>含义：查询操作系统处理器数量的调用失败。这个数量是并行化分派的基础，失败会连累并行检查/分派流程。</para>
	///   <para>推荐处置：确认操作系统环境变量/权限正常后重试；查不到核数时不要强行跑并行检查，先对照 2838 排查。</para>
	///   <para>族段：属 28xx 系统查询杂项子段（2867–2869）第二位。相邻区分：与 2867（取时间）同族，本码取处理器数。</para>
	/// </remarks>
	public const int Jl_ERR_GETCPUNUM = 2868;

	/// <summary>访问临时文件出错，对应英文原文 "Error while accessing temporary file"。</summary>
	/// <remarks>
	///   <para>含义：运行时创建/读写其临时文件失败——临时目录权限不足、磁盘满，或文件被外部清理程序删掉；本库临时文件的命名与落点规则仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：检查磁盘余量、指向临时目录的环境变量与权限；别让清理策略在运行中删掉运行时临时文件。</para>
	///   <para>族段：属 28xx 系统查询杂项子段（2867–2869）末位。相邻区分：2803/2850 是特定知识文件的写失败，本码覆盖一般临时文件。</para>
	/// </remarks>
	public const int Jl_ERR_TMPFNF = 2869;

	/// <summary>消息队列等待操作被取消，对应英文原文 "message queue wait operation canceled"。</summary>
	/// <remarks>
	///   <para>含义：线程正阻塞等消息时被主动唤醒取消——来自关闭/取消信号的正常分支，不是通道故障；谁有权发起取消仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：按"中途取消"处理：让等待方清理本地状态退出，不要重试收消息造成假死循环。</para>
	///   <para>族段：属 28xx 消息队列子段（2890–2893）首位。相邻区分：OVL=2891 是积压溢出，CLEAR=2892 是清理与等待冲突，本码是单次等待被中断。</para>
	/// </remarks>
	public const int Jl_ERR_MQCNCL = 2890;

	/// <summary>消息队列溢出，对应英文原文 "message queue overflow"。</summary>
	/// <remarks>
	///   <para>含义：消息进入速度持续超过消费速度，队列达到容量上限被拒写；上限值与溢出策略（丢消息还是拒收）仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：先看消费端是否卡死/过慢；正常消费仍溢出则要在生产侧限流，而不是只调大队列。</para>
	///   <para>族段：属 28xx 消息队列子段（2890–2893）第二位。相邻区分：2890 是等待被取消，本码是积压超载。</para>
	/// </remarks>
	public const int Jl_ERR_MQOVL = 2891;

	/// <summary>清理消息队列时仍有线程在等待，对应英文原文 "Threads still wait on message queue while clearing it."（原文中的 * 为注释换行残渣）。</summary>
	/// <remarks>
	///   <para>含义：销毁队列时还有接收者阻塞在它上面——生命周期次序被用反，等待方可能持着正在消失的队列，风险由调用方承担。</para>
	///   <para>推荐处置：先取消/撤回全部等待者（配合 2890），再清队列；多线程收尾统一"先停消费、后毁队列"的顺序。</para>
	///   <para>族段：属 28xx 消息队列子段（2890–2893）第三位。相邻区分：2890 是单次等待被打断，本码是整个队列销毁时机违规。</para>
	/// </remarks>
	public const int Jl_ERR_MQCLEAR = 2892;

	/// <summary>消息的文件格式无效，对应英文原文 "Invalid file format for a message"。</summary>
	/// <remarks>
	///   <para>含义：以文件为载体的消息无法按消息文件格式解析——文件损坏、不是该格式或版本错位；消息文件格式规范仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：用同版本工具重新生成该文件；投递前先校验完整性。</para>
	///   <para>族段：属 28xx 消息队列子段（2890–2893）末位。相邻区分：2890–2892 是运行态问题，本码是数据格式问题。</para>
	/// </remarks>
	public const int Jl_ERR_M_WRFILE = 2893;

	/// <summary>字典不含所请求的键，对应英文原文 "Dict does not contain requested key"。</summary>
	/// <remarks>
	///   <para>含义：对字典类对象做了键不存在的查询——写入过不存在的键、键名拼错，或字典已被重建。本库 C# 层没有独立的字典包装类，字典对象在原生运行时的控制对象层 [待实测]。</para>
	///   <para>推荐处置：查询前先做键存在性判断，或给默认值兜底；不要以"应该写过"推断键存在。</para>
	///   <para>族段：属 28xx 字典子段（2894–2897）首位。相邻区分：本码键层，2895/2896 元组长度/类型层，2897 索引层。</para>
	/// </remarks>
	public const int Jl_ERR_DICT_KEY = 2894;

	/// <summary>字典内元组长度不对，对应英文原文 "Incorrect tuple length in dict"。</summary>
	/// <remarks>
	///   <para>含义：字典以键元组/值元组成对存储，两侧长度必须一致；本码即该对齐被破坏（或传入的长度与约定不符）。字典的内部布局在本仓库无记录 [待实测]。</para>
	///   <para>推荐处置：批量写入时让键元组与值元组同步增删同样多的元素，改一侧必改另一侧。</para>
	///   <para>族段：属 28xx 字典子段（2894–2897）第二位。相邻区分：2896 是类型不对，本码是长度不对。</para>
	/// </remarks>
	public const int Jl_ERR_DICT_TUPLE_LENGTH = 2895;

	/// <summary>字典内元组类型不对，对应英文原文 "Incorrect tuple type in dict"。</summary>
	/// <remarks>
	///   <para>含义：字典的键或值所要求的元组元素类型（数值/字符串等）被违反。字典是否允许混合两类元组仓库内无记录 [待实测]。</para>
	///   <para>推荐处置：在字典设计期就固定键、值的类型，之后每次写入都传同类值。</para>
	///   <para>族段：属 28xx 字典子段（2894–2897）第三位。相邻区分：2895 是长度（数量）维度，本码是类型（种类）维度。</para>
	/// </remarks>
	public const int Jl_ERR_DICT_TUPLE_TYPE = 2896;

	/// <summary>字典元组的索引无效，对应英文原文 "Invalid index for dict tuple"。</summary>
	/// <remarks>
	///   <para>含义：按键值元组取元素时下标越界或为负（基 0 还是基 1 本仓库未指明 [待实测]）。与 2894 键错配互补——那条讲"按键找不到"，本码讲"按位置取不对"。</para>
	///   <para>推荐处置：用索引前先核对当前元组长度；删除元素后记得后续索引整体前移。</para>
	///   <para>族段：属 28xx 字典子段（2894–2897）末位。相邻区分：见 2894 处对本子段的层级划分。</para>
	/// </remarks>
	public const int Jl_ERR_DICT_INVALID_INDEX = 2897;

	/// <summary>错误码 2899：字典（dict）嵌套层数超过原生上限。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Dict is nested too deep"：字典的值本身又是字典，套叠层数超过原生允许的深度即报本错。具体深度限值本仓库未文档化 [待实测]。</para>
	///   <para><b>归类</b>DICT 族五码：无此键 <see cref="Jl_ERR_DICT_KEY"/>（2894）、键值元组长度错 <see cref="Jl_ERR_DICT_TUPLE_LENGTH"/>（2895）、键值元组类型错 <see cref="Jl_ERR_DICT_TUPLE_TYPE"/>（2896）、索引非法 <see cref="Jl_ERR_DICT_INVALID_INDEX"/>（2897）、本码（2899）；2898 本文件未定义，族内编号有断档。</para>
	///   <para><b>坑</b>本码是"结构太深"而非"内容太大"：把浅字典堆到超大数据量不会触发它，层层套娃才会。本仓库托管层未公开字典对象/字典算子入口（全库无 Dict 类型可寻），触发方在原生核内部 [待实测]。</para>
	///   <para><b>处置</b>把深层字典展平为键名编码层级的浅结构后重试；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_DICT_NESTED_TOO_DEEP = 2899;

	/// <summary>错误码 2900：原生运行时强制切换上下文失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Error while forcing a context switch"：运行时主动强制一次上下文切换（让出/触发调度）时底层失败。</para>
	///   <para><b>坑</b>它的名字挂 PTHRD 前缀、编号却与 <see cref="Jl_ERR_SCHED_GAFF"/> / <see cref="Jl_ERR_SCHED_SAFF"/>（2901/2902）连段，实际属调度族而非 2970 起的线程原语族；按 PTHRD_ 前缀去 2970~2996 段里找它会扑空。</para>
	///   <para><b>处置</b>调度属原生内部行为，托管层无对应入口 [待实测]；按运行时/环境内部错误记录上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_SCHED = 2900;

	/// <summary>错误码 2901：读取 CPU 亲和性失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Error while accessing cpu affinity"：查询线程当前允许运行的核集合失败。GAFF 对应 get 侧，与设置侧的 <see cref="Jl_ERR_SCHED_SAFF"/>（2902）成对。</para>
	///   <para><b>归类</b>调度族三码：强制切换 <see cref="Jl_ERR_PTHRD_SCHED"/>（2900）、本码（2901）、设置亲和（2902）。</para>
	///   <para><b>处置</b>托管层无亲和性入口（全库无 Affinity 包装），触发方在原生核内部 [待实测]；按运行时内部错误记录上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_SCHED_GAFF = 2901;

	/// <summary>错误码 2902：设置 CPU 亲和性失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Error while setting cpu affinity"：把原生线程绑定到指定 CPU 核集合时失败。CPU 亲和性=该线程允许在哪些核上运行，常用于限制并行算子的核占用。</para>
	///   <para><b>归类</b>调度族三码连段：强制上下文切换失败 <see cref="Jl_ERR_PTHRD_SCHED"/>（2900）、读取亲和性失败 <see cref="Jl_ERR_SCHED_GAFF"/>（2901）、设置亲和性失败（本码，2902）。</para>
	///   <para><b>坑</b>全库托管层没有任何亲和性/线程调度入口（无 Affinity 包装可寻），本码只可能由原生核内部起线程/绑核时产生 [待实测]。</para>
	///   <para><b>处置</b>按运行时/环境内部错误记录上报，不排查算法参数；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_SCHED_SAFF = 2902;

	/// <summary>错误码 2950：传入的同步对象不对。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "wrong synchronization object"：与算子调用对象配套使用的同步对象（用于等待/通知的内核对象）类型或状态不符，无法参与本次调用。</para>
	///   <para><b>归类</b>CO_（operator call object）族六码 2950~2956 的首码，2951 本文件未定义；相邻的 <see cref="Jl_ERR_CO_WOCO"/>（2952）针对调用对象本身，其后四码（2953~2956）针对输入/输出槽位未初始化。</para>
	///   <para><b>处置</b>托管层不向业务代码暴露同步对象，触发方在原生核内部 [待实测]；按运行时内部错误记录上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_CO_WSO = 2950;

	/// <summary>错误码 2952：操作数不是一个合法的算子调用对象（operator call object）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "wrong operator call object"：交给调用对象机制使用的句柄不是合法/对应类型的 operator call object。同族 <see cref="Jl_ERR_CO_WSO"/>（2950）管"同步对象不对"，本码（2952）管"调用对象本身不对"，中间 2951 本文件未定义。</para>
	///   <para><b>坑</b>调用对象在托管层由 <c>JlNativeApi.PreCall</c> 创建并即时消费，业务代码拿不到也不该持有它；出现本码多半是原生内部把错误句柄传给了调用机制 [待实测]。</para>
	///   <para><b>处置</b>按运行时内部错误记录码与文本上报，不排查算法参数；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_CO_WOCO = 2952;

	/// <summary>错误码 2953：输入句柄（object）参数槽未初始化。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "input object not initialized"：算子调用对象的输入句柄槽位（图像/区域等对象参数）未初始化就被读取执行。对照：输入标量槽报 <see cref="Jl_ERR_CO_ICPNI"/>，输出侧两码是 <see cref="Jl_ERR_CO_OOPNI"/> / <see cref="Jl_ERR_CO_OCPNI"/>。</para>
	///   <para><b>坑</b>它与"传了空对象"不同——那是对象合法但内容为空（报空对象族），本码指槽位根本没被放入对象；托管层正常由 Store 族装载输入，此外的触发路径 [待实测]。</para>
	///   <para><b>处置</b>按原生运行时内部错误记录上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_CO_IOPNI = 2953;

	/// <summary>错误码 2954：输入标量（control）参数槽未初始化。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "input control not initialized"：算子调用对象的输入标量槽位（数值/字符串参数）未初始化就被读取执行。对照：输入句柄槽未初始化报 <see cref="Jl_ERR_CO_IOPNI"/>，输出侧两码是 <see cref="Jl_ERR_CO_OOPNI"/> / <see cref="Jl_ERR_CO_OCPNI"/>。</para>
	///   <para><b>坑</b>"该给的参数没给"在这族里表现为本码而非缺参提示，遇到时先想哪个标量入参没被 Store，而不是值非法；托管层组装路径之外的触发方 [待实测]。</para>
	///   <para><b>处置</b>按原生运行时内部错误记录上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_CO_ICPNI = 2954;

	/// <summary>错误码 2955：输出句柄（object）参数槽未初始化。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "output object not initialized"：算子调用对象上的输出句柄槽位（存放图像/区域等对象结果的位置）未初始化即被使用/收尾。对象族内按"输入/输出 × 句柄/标量"分四码：本码为"输出×句柄"，标量侧是 <see cref="Jl_ERR_CO_OCPNI"/>，输入侧是 <see cref="Jl_ERR_CO_IOPNI"/> / <see cref="Jl_ERR_CO_ICPNI"/>。</para>
	///   <para><b>坑</b>若本码出现而输出变量看似"已声明"，说明装载发生在更底层：托管层正常路径由 Load 族自动填充输出，触发方在原生核内部 [待实测]。</para>
	///   <para><b>处置</b>CO_ 族（2950~2956）内部的运行时错误，不排查算法参数取值；记录码与文本上报。≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_CO_OOPNI = 2955;

	/// <summary>错误码 2956：输出标量（control）参数槽未初始化。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "output control not initialized"：算子调用对象上声明的输出标量槽位没有被初始化就被使用/收尾。CO_ 族里 control 指标量/数值类参数，object 指句柄类参数（输出句柄槽对应 <see cref="Jl_ERR_CO_OOPNI"/>）。</para>
	///   <para><b>归类</b>CO_（operator call object）族 2950~2956 共六码、2951 本文件未定义：<see cref="Jl_ERR_CO_WSO"/>、<see cref="Jl_ERR_CO_WOCO"/>、输入未初始化 <see cref="Jl_ERR_CO_IOPNI"/> / <see cref="Jl_ERR_CO_ICPNI"/>、输出未初始化 <see cref="Jl_ERR_CO_OOPNI"/> / 本码。</para>
	///   <para><b>坑</b>托管层的调用对象由 <c>JlNativeApi.PreCall</c>/Store/Load 流程自动组装，业务代码不直接摆弄这些槽位，能触发本码的路径在原生核内部 [待实测]。</para>
	///   <para><b>处置</b>按运行时内部错误记录码与文本上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_CO_OCPNI = 2956;

	/// <summary>错误码 2970：创建 pthread 线程失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Creation of pthread failed"：原生运行时启动工作线程失败，最常见是系统线程/内存资源达到上限 [待实测]。</para>
	///   <para><b>坑</b>本码是原生内部并行的入口性失败：起不来线程时，后续的回收 <see cref="Jl_ERR_PTHRD_JO"/>、同步等环节都无从谈起；2970~2996 整段（互斥量、条件变量、事件、TSD、屏障）都是这套线程体系的子族码。</para>
	///   <para><b>归类</b>PTHRD 线程生命周期三环首环：创建（本码）→ 分离 <see cref="Jl_ERR_PTHRD_DT"/> / 回收 <see cref="Jl_ERR_PTHRD_JO"/>。</para>
	///   <para><b>处置</b>降低并发规模后重试，仍复现则连同异常文本上报为环境/资源问题；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_CR = 2970;

	/// <summary>错误码 2971：分离（detach）线程失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "pthread-detach failed"：把线程标记为分离态（结束后可被系统直接回收、不再需要 join）时底层调用失败。</para>
	///   <para><b>坑</b>分离失败后线程仍可能必须靠 <see cref="Jl_ERR_PTHRD_JO"/> 收尾；若上层逻辑已按"分离成功"跳过 join，该线程的退出状态就没人收，属资源泄漏面 [待实测]。</para>
	///   <para><b>归类</b>PTHRD 线程生命周期三环：创建 <see cref="Jl_ERR_PTHRD_CR"/> → 分离（本码）/ 回收 <see cref="Jl_ERR_PTHRD_JO"/>。</para>
	///   <para><b>处置</b>原生并发层内部错误，记录码与文本上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_DT = 2971;

	/// <summary>错误码 2972：回收（join）线程失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "pthread-join failed"：等待目标线程结束并回收其退出状态的操作在底层失败。对已分离或已被 join 过的线程再执行 join，在 POSIX 下非法，是否即表现为本码 [待实测]。</para>
	///   <para><b>坑</b>与 <see cref="Jl_ERR_PTHRD_DT"/> 是一对互斥的收尾方式：线程要么 detach 要么 join；join 失败后该线程的资源归属状态不明，无视失败继续批量创建新线程可能加速耗尽系统线程名额。</para>
	///   <para><b>归类</b>PTHRD 线程生命周期三环：创建 <see cref="Jl_ERR_PTHRD_CR"/> → 分离 <see cref="Jl_ERR_PTHRD_DT"/> / 回收（本码）。</para>
	///   <para><b>处置</b>原生并发层内部错误，记录码与文本上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_JO = 2972;

	/// <summary>错误码 2973：初始化互斥量（mutex）失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Initialization of mutex variable failed"：创建互斥锁失败，多为底层资源不足 [待实测]。</para>
	///   <para><b>坑</b>别按名字猜它属条件变量或事件族——三族都有"初始化失败"码且相邻（2973/2979/2983），排查时要按数值回本码定族。</para>
	///   <para><b>归类</b>PTHRD 互斥量族首环：初始化（本码）→ 加锁 <see cref="Jl_ERR_PTHRD_ML"/> / 解锁 <see cref="Jl_ERR_PTHRD_MU"/> → 销毁 <see cref="Jl_ERR_PTHRD_MD"/>；初始化失败会连带使同一次使用的加/解锁不可用。</para>
	///   <para><b>处置</b>按运行时资源/内部错误记录上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_MI = 2973;

	/// <summary>错误码 2974：销毁互斥量（mutex）失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Deletion of mutex variable failed"：销毁互斥锁失败。POSIX 下对仍被持有或仍有人在等的锁执行销毁是未定义行为，本运行时是否表现为本码 [待实测]。</para>
	///   <para><b>归类</b>PTHRD 互斥量族末环：初始化 <see cref="Jl_ERR_PTHRD_MI"/> → 加锁 <see cref="Jl_ERR_PTHRD_ML"/> / 解锁 <see cref="Jl_ERR_PTHRD_MU"/> → 销毁（本码）；销毁前应先确认已解锁。</para>
	///   <para><b>处置</b>原生并发层内部错误，按码与文本记录上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_MD = 2974;

	/// <summary>错误码 2975：加锁互斥量（mutex）失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Lock of mutex variable failed"：获取互斥锁失败（死锁检测命中、锁对象失效、底层资源异常等，具体判定标准本仓库未文档化 [待实测]）。</para>
	///   <para><b>坑</b>加锁失败后若吞掉错误继续进临界区，等于无锁并发，原生数据可能被静默踩坏；正确做法是中止本次调用。</para>
	///   <para><b>归类</b>PTHRD 互斥量族：初始化 <see cref="Jl_ERR_PTHRD_MI"/>、加锁（本码）、解锁 <see cref="Jl_ERR_PTHRD_MU"/>、销毁 <see cref="Jl_ERR_PTHRD_MD"/>。</para>
	///   <para><b>处置</b>原生并发层内部错误，与图像参数无关；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_ML = 2975;

	/// <summary>错误码 2976：解锁互斥量（mutex）失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Unlock of mutex variable failed"：释放互斥锁失败。POSIX 语义下最常见的成因是解锁一把并不由本线程持有的锁 [待实测]。</para>
	///   <para><b>坑</b>解锁失败说明加锁/解锁的配对已被破坏，此后该锁保护的临界区不再可信，相关原生数据存在被并发踩坏的风险；与加锁失败 <see cref="Jl_ERR_PTHRD_ML"/> 是同一配对的两端，排查时两头都要看。</para>
	///   <para><b>归类</b>PTHRD 互斥量族：初始化 <see cref="Jl_ERR_PTHRD_MI"/> → 加锁 <see cref="Jl_ERR_PTHRD_ML"/> / 解锁（本码）→ 销毁 <see cref="Jl_ERR_PTHRD_MD"/>。</para>
	///   <para><b>处置</b>原生并发层内部错误，记录上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_MU = 2976;

	/// <summary>错误码 2977：向 pthread 条件变量发通知（signal）失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Failed to signal pthread condition var."：唤醒一个等待该条件变量的线程时底层失败。文本只说 signal，是否等价"广播唤醒全部等待者"本仓库未承诺 [待实测]。</para>
	///   <para><b>坑</b>通知没发出，等待方（<see cref="Jl_ERR_PTHRD_CW"/> 那类操作）会永远挂住——现场表现为流程卡死不报错，异常只在发出侧抛出；排查卡死时别忘了回查日志里有无本码。</para>
	///   <para><b>归类</b>PTHRD 条件变量族：初始化 <see cref="Jl_ERR_PTHRD_CI"/>、等待 <see cref="Jl_ERR_PTHRD_CW"/>、通知（本码）、销毁 <see cref="Jl_ERR_PTHRD_CD"/>。</para>
	///   <para><b>处置</b>原生并发层内部错误，记录码与文本上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_CS = 2977;

	/// <summary>错误码 2978：等待 pthread 条件变量失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Failed to wait for pthread cond. var."：线程在条件变量上挂起、等待通知时底层返回失败。POSIX 语义下等待前必须持有配套互斥锁，锁状态不对（未持有/被他人持有）是典型触发面 [待实测]。</para>
	///   <para><b>坑</b>"等不到通知导致卡死"不会报本码——那种情况是调用根本没返回；本码只在等待调用失败返回时出现，两者现场表现完全不同。</para>
	///   <para><b>归类</b>PTHRD 条件变量族：初始化 <see cref="Jl_ERR_PTHRD_CI"/>、等待（本码）、通知 <see cref="Jl_ERR_PTHRD_CS"/>、销毁 <see cref="Jl_ERR_PTHRD_CD"/>。</para>
	///   <para><b>处置</b>原生并发层内部错误，不排查算法参数；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_CW = 2978;

	/// <summary>错误码 2979：初始化 pthread 条件变量失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Failed to init pthread condition var."：创建条件变量失败，多为底层资源不足 [待实测]。条件变量须配合互斥锁（<see cref="Jl_ERR_PTHRD_MI"/> 一族）使用，本身不保护数据、只负责"等待—通知"。</para>
	///   <para><b>坑</b>别与事件族混淆：初始化事件对象失败报 <see cref="Jl_ERR_PTHRD_EI"/>，是两种不同的同步原语。</para>
	///   <para><b>归类</b>PTHRD 条件变量族首环：初始化（本码）→ 等待 <see cref="Jl_ERR_PTHRD_CW"/> / 通知 <see cref="Jl_ERR_PTHRD_CS"/> → 销毁 <see cref="Jl_ERR_PTHRD_CD"/>；初始化失败会使后续操作连带不可用。</para>
	///   <para><b>处置</b>按运行时资源/内部错误记录上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_CI = 2979;

	/// <summary>错误码 2980：销毁 pthread 条件变量失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Failed to destroy pthread condition var."：释放条件变量对象失败；销毁时仍有线程在等待该条件变量是典型被拒场景 [待实测]。</para>
	///   <para><b>归类</b>PTHRD 条件变量族末环：初始化 <see cref="Jl_ERR_PTHRD_CI"/> → 等待 <see cref="Jl_ERR_PTHRD_CW"/> / 通知 <see cref="Jl_ERR_PTHRD_CS"/> → 销毁（本码）。</para>
	///   <para><b>处置</b>托管层无条件变量原语入口，触发方为原生核内部 [待实测]；按运行时内部错误记录上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_CD = 2980;

	/// <summary>错误码 2981：给事件（event）发信号失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Failed to signal event."：置起事件、放行等待方的操作在底层失败。</para>
	///   <para><b>坑</b>本码的连带后果在等待侧：信号没发出，正在等待的线程（对应 <see cref="Jl_ERR_PTHRD_EW"/> 那类操作）会一直挂住，现场表现为流程卡死而不是立刻报错；只在发出侧看到异常。</para>
	///   <para><b>归类</b>PTHRD 事件族：初始化 <see cref="Jl_ERR_PTHRD_EI"/>、发信号（本码）、等待 <see cref="Jl_ERR_PTHRD_EW"/>、销毁 <see cref="Jl_ERR_PTHRD_ED"/>。</para>
	///   <para><b>处置</b>原生并发层内部错误；卡死时优先回看日志里是否出现过本码；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_ES = 2981;

	/// <summary>错误码 2982：在事件（event）上等待失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Failed to wait for event."：线程在原生事件对象上阻塞等待信号时底层调用失败。注意它只表示"等待这个动作没执行成"，"信号迟迟不来导致挂住"根本不会返回、也就不会报本码。</para>
	///   <para><b>坑</b>等待事件与等待条件变量 <see cref="Jl_ERR_PTHRD_CW"/> 分属两个原语族，排查时先按错误文本定族再找对应码，别混着查。</para>
	///   <para><b>归类</b>PTHRD 事件族：初始化 <see cref="Jl_ERR_PTHRD_EI"/> → 发信号 <see cref="Jl_ERR_PTHRD_ES"/> / 等待（本码）→ 销毁 <see cref="Jl_ERR_PTHRD_ED"/>。</para>
	///   <para><b>处置</b>原生并发层内部错误，记录码与文本上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_EW = 2982;

	/// <summary>错误码 2983：初始化事件（event）对象失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Failed to init event."：创建原生事件同步对象失败，多为底层资源不足 [待实测]。事件用于"一方置信号、其余线程等待被放行"的汇合。</para>
	///   <para><b>坑</b>别按名字把它归入条件变量族：初始化条件变量失败报的是 <see cref="Jl_ERR_PTHRD_CI"/>，两者是不同原语、不同码段。</para>
	///   <para><b>归类</b>PTHRD 事件族首环：初始化（本码）→ 发信号 <see cref="Jl_ERR_PTHRD_ES"/> / 等待 <see cref="Jl_ERR_PTHRD_EW"/> → 销毁 <see cref="Jl_ERR_PTHRD_ED"/>；初始化失败会使同一次使用的后续操作连带不可用。</para>
	///   <para><b>处置</b>按运行时资源/内部错误记录上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_EI = 2983;

	/// <summary>错误码 2984：销毁事件（event）对象失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Failed to destroy event."：释放原生事件同步对象失败；若销毁时仍有线程在 <see cref="Jl_ERR_PTHRD_EW"/> 语义上等待该事件，销毁不被允许是常见成因 [待实测]。</para>
	///   <para><b>归类</b>PTHRD 事件族末环：初始化 <see cref="Jl_ERR_PTHRD_EI"/> → 发信号 <see cref="Jl_ERR_PTHRD_ES"/> / 等待 <see cref="Jl_ERR_PTHRD_EW"/> → 销毁（本码）。</para>
	///   <para><b>处置</b>托管层无事件原语入口，触发方为原生核内部 [待实测]；按运行时内部错误记录上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_ED = 2984;

	/// <summary>错误码 2985：创建线程特定数据（TSD）键失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Failed to create a tsd key."：申请一个新的线程特定数据键失败，最常见是进程级键名额耗尽或资源不足 [待实测]。</para>
	///   <para><b>归类</b>PTHRD TSD 族首环：创建（本码）→ 设值 <see cref="Jl_ERR_PTHRD_TSDS"/> → 取值 <see cref="Jl_ERR_PTHRD_TSDG"/> → 释放 <see cref="Jl_ERR_PTHRD_TSDF"/>。键创建失败会让同一次使用的后续三个操作都无从执行。</para>
	///   <para><b>坑</b>反复创建而不释放键（<see cref="Jl_ERR_PTHRD_TSDF"/>）会耗尽键名额，表现为"创建线程数不多却仍报本码" [待实测]。</para>
	///   <para><b>处置</b>原生并发层内部错误，记录码与文本上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_TSDC = 2985;

	/// <summary>错误码 2986：写入线程特定数据（TSD）失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Failed to set a thread specific data key."：把数据挂到当前线程的指定键上失败，典型成因是键无效或底层内存分配失败 [待实测]。</para>
	///   <para><b>坑</b>设值半途失败时该线程此键的值处于"未设"态，后续 <see cref="Jl_ERR_PTHRD_TSDG"/> 一类的读取拿不到预期数据，别当它已写入成功。</para>
	///   <para><b>归类</b>PTHRD TSD 族：创建 <see cref="Jl_ERR_PTHRD_TSDC"/>、设值（本码）、取值 <see cref="Jl_ERR_PTHRD_TSDG"/>、释放 <see cref="Jl_ERR_PTHRD_TSDF"/>。</para>
	///   <para><b>处置</b>原生并发层内部错误，记录码与文本上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_TSDS = 2986;

	/// <summary>错误码 2987：读取线程特定数据（TSD）失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Failed to get a tsd key."：按键取当前线程的线程特定数据时底层调用失败，典型前提是键无效（未创建或已被释放）。</para>
	///   <para><b>坑</b>取不到值与"该线程从未设过值"不是一回事：后者通常回空指针而非报错，走到本码说明键本身状态不对 [待实测]。</para>
	///   <para><b>归类</b>PTHRD TSD 族：创建 <see cref="Jl_ERR_PTHRD_TSDC"/>、设值 <see cref="Jl_ERR_PTHRD_TSDS"/>、取值（本码）、释放 <see cref="Jl_ERR_PTHRD_TSDF"/>。</para>
	///   <para><b>处置</b>原生并发层内部错误，不排查算法参数；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_TSDG = 2987;

	/// <summary>错误码 2988：释放线程特定数据（TSD）键失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Failed to free a tsd key."：删除线程特定数据键（thread specific data，POSIX 线程"一键每线程一值"机制）失败。对仍可能有线程挂着值的键执行删除在 POSIX 下是危险操作，本运行时是否据此报本码 [待实测]。</para>
	///   <para><b>归类</b>PTHRD TSD 族末环：创建 <see cref="Jl_ERR_PTHRD_TSDC"/> → 设值 <see cref="Jl_ERR_PTHRD_TSDS"/> → 取值 <see cref="Jl_ERR_PTHRD_TSDG"/> → 释放（本码）。</para>
	///   <para><b>处置</b>托管层无线程局部存储入口（同步原语包装已删除），触发方为原生核内部 [待实测]；按运行时内部错误记录上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_TSDF = 2988;

	/// <summary>错误码 2989：在屏障（barrier）等待汇合时被打断。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Aborted waiting at a barrier"：线程已到屏障汇合点等待其余参与者到齐，但这次等待被打断/中止（其余线程未到齐或屏障状态被破坏）。与"等待调用本身失败"的 <see cref="Jl_ERR_PTHRD_BW"/> 相区分。</para>
	///   <para><b>归类</b>PTHRD 屏障族四环：初始化 <see cref="Jl_ERR_PTHRD_BI"/> → 等待 <see cref="Jl_ERR_PTHRD_BW"/> → 等待被打断（本码）→ 销毁 <see cref="Jl_ERR_PTHRD_BD"/>；本码 2989 编号在该族其余三码（2994~2996）之前。</para>
	///   <para><b>处置</b>本仓库托管层未公开屏障原语入口，触发路径 [待实测]；按并发运行时异常处理，排查哪一路参与者没有到齐；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_BA = 2989;

	/// <summary>错误码 2990：调度期间"空闲表"（free list）已无可用项。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "'Free list' is empty while scheduling"：调度环节从空闲链表中取表项时链表已空，安排不出新的工作。属内部资源耗尽，不是调用方参数错；DCDG 前缀所指的具体子系统本仓库未给出 [待实测]。</para>
	///   <para><b>坑</b>与 <see cref="Jl_ERR_NEF"/>（IPvvf 例程无空闲元素）同属"空闲元素耗尽"，但发生在不同原生模块；触发本码时的并发/数据规模条件 [待实测]。</para>
	///   <para><b>处置</b>降低并发数或拆分数据规模后重试，仍复现则把码连同异常文本上报为运行时内部错误；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_DCDG_FLE = 2990;

	/// <summary>错误码 2991：通信伙伴未登记（未签到）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Communication partner not checked in"：通信子系统要求参与方先登记/签到再参与收发，寻址到一个尚未登记的伙伴时报本错。</para>
	///   <para><b>坑</b>其原生文本与相邻码 <see cref="Jl_ERR_MSG_CSNI"/>（2993）完全相同，两码的语义分工在本仓库无文档支撑，疑为不同子系统下的同名文本 [待实测]。</para>
	///   <para><b>处置</b>本仓库托管层已不提供通信类算子入口，本码只可能由原生核内部产生，触发方 [待实测]；按运行时/环境内部错误记录码与文本上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MSG_PNCI = 2991;

	/// <summary>错误码 2992：通信系统正在运行中，无法（再次）启动。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "The communication system can't be started while running"：对一个已处于运行状态的通信系统再次发起启动，被状态机拒绝。属启动时序/状态冲突，与图像算法参数无关。</para>
	///   <para><b>归类</b>MSG 通信族相邻三码：伙伴未登记 <see cref="Jl_ERR_MSG_PNCI"/>（2991）、本码（2992）、伙伴未签到 <see cref="Jl_ERR_MSG_CSNI"/>（2993）。名字缩写 CSAI 的逐词含义本仓库未给出 [待实测]。</para>
	///   <para><b>处置</b>本仓库托管层已删除通信/串口/socket 能力，本码只可能由原生核内部产生，触发方 [待实测]；按运行时/环境内部错误处理，记录码值与异常文本（<c>JlOperatorException</c> 的消息即按码查原生消息表所得）后上报；≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MSG_CSAI = 2992;

	/// <summary>错误码 2993：通信伙伴未签到/登记（communication partner not checked in）。</summary>
	/// <remarks>
	///   <para><b>含义</b>通信子系统要求参与方先"签到/注册"再收发；某伙伴未登记即被寻址时报本错。本仓库托管层已不提供通信/串口/socket 类算子，此码只可能由原生核内部产生，触发方 [待实测]。</para>
	///   <para><b>坑</b>原生文本与 <see cref="Jl_ERR_MSG_PNCI"/> 完全相同（均为 "Communication partner not checked in"），二者语义分工在本仓库无文档支撑、疑为不同子系统下的同名码 [待实测]；另与"运行中无法启动通信系统"的 <see cref="Jl_ERR_MSG_CSAI"/> 是相邻的 MSG_ 族成员。</para>
	///   <para><b>处置</b>当作运行时/环境内部错误记录上报，不排查算法参数。</para>
	/// </remarks>
	public const int Jl_ERR_MSG_CSNI = 2993;

	/// <summary>错误码 2994：屏障（barrier）对象初始化失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>创建多线程汇合点时底层分配/初始化失败（资源不足或参与者数非法等）。属原生并发层错误，托管层未公开入口，触发方 [待实测]。</para>
	///   <para><b>归类</b>PTHRD 屏障族首环：初始化 <see cref="Jl_ERR_PTHRD_BI"/>（本码）→ 等待 <see cref="Jl_ERR_PTHRD_BW"/> / 被打断 <see cref="Jl_ERR_PTHRD_BA"/> → 销毁 <see cref="Jl_ERR_PTHRD_BD"/>。</para>
	///   <para><b>处置</b>按运行时内部/资源错误记录并上报；初始化失败通常连带使同一次汇合的后续操作不可用。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_BI = 2994;

	/// <summary>错误码 2995：在屏障（barrier）处等待失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>线程在屏障汇合点等待其它参与者到齐时底层调用失败（非正常超时打断，那种报 <see cref="Jl_ERR_PTHRD_BA"/>）。属并发层内部错误，托管层未公开入口，触发方 [待实测]。</para>
	///   <para><b>归类</b>PTHRD 屏障族：初始化 <see cref="Jl_ERR_PTHRD_BI"/>、等待 <see cref="Jl_ERR_PTHRD_BW"/>（本码）、等待被打断 <see cref="Jl_ERR_PTHRD_BA"/>、销毁 <see cref="Jl_ERR_PTHRD_BD"/>。</para>
	///   <para><b>处置</b>按运行时并发/资源异常记录码与 <c>JlNativeApi.GetErrorMessage(err)</c> 上报，不排查算法参数。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_BW = 2995;

	/// <summary>错误码 2996：销毁屏障（barrier）对象失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>多线程汇合点（barrier）在析构/销毁时底层返回失败。属原生运行时并发层的错误，与图像算法无关。本仓库托管层未公开屏障原语入口，触发路径 [待实测]。</para>
	///   <para><b>归类</b>PTHRD 事件/屏障/TSD 族成员：初始化失败报 <see cref="Jl_ERR_PTHRD_BI"/>，等待失败报 <see cref="Jl_ERR_PTHRD_BW"/>，等待被打断报 <see cref="Jl_ERR_PTHRD_BA"/>，本码专指销毁。</para>
	///   <para><b>处置</b>当作运行时内部/资源问题记录并上报，不排查算法参数；本码 ≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_PTHRD_BD = 2996;

	/// <summary>错误码 3010：区域完全落在图像定义域之外。</summary>
	/// <remarks>
	///   <para><b>含义</b>给定区域与图像定义域完全没有重叠（区域整体位于图边之外），算子无可处理像素，报本错。区域若仍有一部分在内则报 <see cref="Jl_ERR_ROOIMA"/>。</para>
	///   <para><b>与相邻错误码的分工</b>完全在外报本码，部分在外报 <see cref="Jl_ERR_ROOIMA"/>，求交为空报 <see cref="Jl_ERR_RIEI"/>。三码按"区域与图像的重叠程度"分级。</para>
	///   <para><b>坑</b>典型来自坐标系错配或大位移变换把区域整体推离图像；此时区域非空、图像非空，只是二者老死不相往来。</para>
	///   <para><b>处置</b>先求交判是否重叠；对齐区域与图像坐标系，或重新定位区域后再调用。</para>
	/// </remarks>
	public const int Jl_ERR_RCOIMA = 3010;

	/// <summary>错误码 3011：区域（部分）超出图像的定义域范围。</summary>
	/// <remarks>
	///   <para><b>含义</b>给定区域有一部分落在图像定义域之外（与图像边界相交、越过图边但仍有一部分在内），算子拒绝在越界状态下处理并报本错。区域若整块在图外则报 <see cref="Jl_ERR_RCOIMA"/>。</para>
	///   <para><b>与相邻错误码的分工</b>部分越界报本码，完全在外报 <see cref="Jl_ERR_RCOIMA"/>，求交后为空报 <see cref="Jl_ERR_RIEI"/>。本族还不同于坐标幅值越界（<see cref="Jl_ERR_ROWTB"/> 等管 int16/XL 表示域）。</para>
	///   <para><b>坑</b>裁剪/膨胀/仿射后区域易"压线"越界；不少算子其实可自动裁剪到定义域，若本库该算子选择严格拒算，则须显式先裁剪。</para>
	///   <para><b>处置</b>调用前把区域与图像全域求交（intersection）后再传入。</para>
	/// </remarks>
	public const int Jl_ERR_ROOIMA = 3011;

	/// <summary>错误码 3012：区域与图像定义域的交集为空。</summary>
	/// <remarks>
	///   <para><b>含义</b>算子把给定的区域限制在图像定义域内一起处理，若二者交集为空（区域整块落在图像有效范围之外）就没有可算像素，报本错。区域本身和图像本身都非空，只是不相交。</para>
	///   <para><b>与相邻错误码的分工</b>区域完全在图外报 <see cref="Jl_ERR_RCOIMA"/>、部分在图外报 <see cref="Jl_ERR_ROOIMA"/>；图像自身定义为空报 <see cref="Jl_ERR_EDEF"/>。本码专指"区域∩图像定义域=∅"这一相交结果为空的判定。</para>
	///   <para><b>坑</b>坐标系错配（区域来自另一幅/配准前）最易触发：区域看似有坐标，却与本图定义域无交集。</para>
	///   <para><b>处置</b>先求交判空；对齐区域与图像的坐标系，或改用与该图定义域确实重叠的区域。</para>
	/// </remarks>
	public const int Jl_ERR_RIEI = 3012;

	/// <summary>错误码 3013：图像的定义域（有效区域）为空。</summary>
	/// <remarks>
	///   <para><b>含义</b>图像带一个被限定的处理区域（definition range / domain）。当该区域不含任何像素（reduce_domain 到空区域，或整图域被清空）时，算子无像素可处理，报本错。</para>
	///   <para><b>与相邻错误码的分工</b>两图无公共点报 <see cref="Jl_ERR_IIEI"/>；某区域∩图像为空报 <see cref="Jl_ERR_RIEI"/>；单纯 <c>JlRegion</c> 为空报 <see cref="Jl_ERR_EMPTREG"/>。本码专指"图像自身的定义域为空"。</para>
	///   <para><b>坑</b>常见于把上一步筛出的空区域又 reduce_domain 回图像，导致图像域被清成空；此时问题在上游而非本算子。</para>
	///   <para><b>处置</b>调用前检查图像定义域非空，必要时还原为全域；回溯是哪一步清空了域。</para>
	/// </remarks>
	public const int Jl_ERR_EDEF = 3013;

	/// <summary>错误码 3014：两幅图像没有任何公共像素点。</summary>
	/// <remarks>
	///   <para><b>含义</b>需同时作用于两幅图像（求交、比较、按点组合一类）的算子，要求两图的有效像素集至少有一个公共点；两幅图的定义域完全不相交（在图像平面上错开、或一左一右无重叠）即报本错。</para>
	///   <para><b>与相邻错误码的分工</b>单图定义为空报 <see cref="Jl_ERR_EDEF"/>；"区域∩图像"交集为空报 <see cref="Jl_ERR_RIEI"/>；本码专指两图之间无公共点。</para>
	///   <para><b>坑</b>常因两图各自 reduce_domain 到互不重叠的 ROI，或坐标系/配准未对齐使本应重叠的区域错开。</para>
	///   <para><b>处置</b>先把两图定义域求交或还原到重叠区域，再调用；配准类流程应先对齐坐标再比较。</para>
	/// </remarks>
	public const int Jl_ERR_IIEI = 3014;

	/// <summary>错误码 3015：区域与图像不匹配——首行为负。</summary>
	/// <remarks>
	///   <para><b>含义</b>区域的行程按行升序编码，首行（最上一行）行号必须 ≥ 0（row=y，向下为正、从 0 计）。首行为负说明区域与图像不一致（区域跑到图像上边界之外），报本错。</para>
	///   <para><b>与相邻错误码的分工</b>末行列越界报 <see cref="Jl_ERR_LLTB"/>；整块图外报 <see cref="Jl_ERR_RCOIMA"/>、部分越界报 <see cref="Jl_ERR_ROOIMA"/>。本码专检"首行 <c>&lt;</c> 0"这一具体不一致。</para>
	///   <para><b>坑</b>常因负行偏移的平移/配准把区域顶到负行，或手工构造区域时行序未升序、含负 row。</para>
	///   <para><b>处置</b>校正偏移使首行 ≥ 0，或先与图像全域求交裁掉图外部分再调用。</para>
	/// </remarks>
	public const int Jl_ERR_FLTS = 3015;

	/// <summary>错误码 3016：区域与图像不匹配——最后一行的列已 ≥ 图像宽度。</summary>
	/// <remarks>
	///   <para><b>含义</b>区域以行程（每行一段 [begin,end] 列）编码。当最后一行的行程列号达到或超过图像宽度时，说明该区域与目标图像不匹配（列越界），报本错。列从 0 计，合法上界为 width-1。</para>
	///   <para><b>与相邻错误码的分工</b>首行为负报 <see cref="Jl_ERR_FLTS"/>；整块完全在图外报 <see cref="Jl_ERR_RCOIMA"/>、部分越界报 <see cref="Jl_ERR_ROOIMA"/>。本码专检"末行列 ≥ 宽"这一具体不一致。</para>
	///   <para><b>坑</b>多是把 A 图坐标系的区域用到更窄的 B 图上，或区域被手工/偏移改写后末行越界。</para>
	///   <para><b>处置</b>确认区域与图像同一坐标系与同幅面，必要时先裁剪到图像内再用。</para>
	/// </remarks>
	public const int Jl_ERR_LLTB = 3016;

	/// <summary>错误码 3017：输入参数之间图像路数不相等。</summary>
	/// <remarks>
	///   <para><b>含义</b>算子要求两路或多路图像输入按"逐元素配对"处理时，各路元组的图像个数必须相同；个数不等（如一路 3 幅、另一路 2 幅）即报本错。</para>
	///   <para><b>与相邻错误码的分工</b>本码只管"路数不齐"；图像有但尺寸不齐报 <see cref="Jl_ERR_IWDS"/>，通道数不齐报 <see cref="Jl_ERR_DNOC"/>，定义域不齐报 <see cref="Jl_ERR_DOM_DIFF"/>。四者分层排查。</para>
	///   <para><b>处置</b>对齐各路图像元组长度后再调用；一对多的组合应显式复制或用标量广播语义的算子（若有），别指望自动补齐。</para>
	/// </remarks>
	public const int Jl_ERR_UENOI = 3017;

	/// <summary>错误码 3018：图像高度小于算子要求的最小值。</summary>
	/// <remarks>
	///   <para><b>含义</b>算子对输入图像高度设有下限（如需容纳核/窗口或要求非退化尺寸），高度为 0 或过小时报本错。下限值随算子而变 [待实测]。</para>
	///   <para><b>与相邻错误码的分工</b>宽度过小的同款错误报 <see cref="Jl_ERR_WTS"/>；图像定义为空域报 <see cref="Jl_ERR_EDEF"/>。本码只管高度维度。</para>
	///   <para><b>坑</b>与落点越界的 <see cref="Jl_ERR_IHTL"/>、<see cref="Jl_ERR_IHTS"/> 不同：那两个是"结果放不进目标图"，本码是"输入图本身太矮"。</para>
	///   <para><b>处置</b>核对裁剪/ROI 的行范围保证最小高度；极扁条带图需换用支持小高度的算子或先补边。</para>
	/// </remarks>
	public const int Jl_ERR_HTS = 3018;

	/// <summary>错误码 3019：图像宽度小于算子要求的最小值。</summary>
	/// <remarks>
	///   <para><b>含义</b>某些算子对输入图像宽度设有下限（如需容纳核/窗口、或要求非退化尺寸），宽度为 0 或过小时报本错。下限值随算子而变 [待实测]。</para>
	///   <para><b>与相邻错误码的分工</b>高度过小的同款错误报 <see cref="Jl_ERR_HTS"/>；图像定义为空域报 <see cref="Jl_ERR_EDEF"/>。本码只管宽度维度。</para>
	///   <para><b>坑</b>常因按错误外接框裁剪后剩极窄图、或列方向被整体裁没（对照落点越界的 <see cref="Jl_ERR_IWTL"/>、<see cref="Jl_ERR_IWTS"/>——那是"放不进"，本码是"图本身太窄"）。</para>
	///   <para><b>处置</b>核对裁剪/ROI 的列范围保证最小宽度，或换用接受小图的算子。</para>
	/// </remarks>
	public const int Jl_ERR_WTS = 3019;

	/// <summary>错误码 3020：内部错误——行程段分割初始化例程 JlRLInitSeg() 被重复调用。</summary>
	/// <remarks>
	///   <para><b>含义</b>JlRLInitSeg 对同一次分割只应调用一次以建立上下文；在未收尾前再次调用即报本错，属调用次序/状态误用。</para>
	///   <para><b>与相邻错误码的分工</b>与 <see cref="Jl_ERR_RLSEG1"/>（未初始化就处理）成对，一个防"漏初始化"、一个防"重初始化"。</para>
	///   <para><b>坑</b>标注为内部错误，托管层未直接暴露这对例程；多发生在原生内部重入或异常后未复位的路径 [待实测]。</para>
	///   <para><b>处置</b>按运行时内部错误处理：记录码与 <c>JlNativeApi.GetErrorMessage(err)</c> 上报，勿在业务层自行重试初始化。</para>
	/// </remarks>
	public const int Jl_ERR_CHSEG = 3020;

	/// <summary>错误码 3021：内部错误——未初始化就调用行程段分割例程 JlRLSeg()。</summary>
	/// <remarks>
	///   <para><b>含义</b>行程段（run-length）分割是一组有状态的内部例程：必须先由 JlRLInitSeg 建立上下文，JlRLSeg 才能逐次取段。跳过初始化直接调用 JlRLSeg 即报本错。</para>
	///   <para><b>与相邻错误码的分工</b>相反方向的滥用——重复初始化——报 <see cref="Jl_ERR_CHSEG"/>；两码合起来守的是"先初始化一次、再处理"的调用次序契约。</para>
	///   <para><b>坑</b>标注为内部错误：托管层未公开 JlRLInitSeg/JlRLSeg 的直接入口，触发方与调用链 [待实测]，一般不由业务代码构造。</para>
	///   <para><b>处置</b>若真收到，说明原生分割流程状态机被打断（并发或异常中途退出后未复位）；按运行时内部错误记录码与 <c>JlNativeApi.GetErrorMessage(err)</c> 上报。</para>
	/// </remarks>
	public const int Jl_ERR_RLSEG1 = 3021;

	/// <summary>错误码 3022：Gauss 滤波器的尺寸参数不合法。</summary>
	/// <remarks>
	///   <para><b>含义</b>生成/应用高斯（Gauss）核时，给定的核尺寸不符合该算子对高斯核的专属约定。高斯核的尺寸合法性规则与一般卷积核不同（并非简单的"奇数/不超过图像"），具体允许的取值 [待实测]。</para>
	///   <para><b>与相邻错误码的分工</b>其它核的尺寸奇偶、超图像、超上限分别报 <see cref="Jl_ERR_FSEVAN"/>、<see cref="Jl_ERR_FSEIS"/>、<see cref="Jl_ERR_FSTOBIG"/>；本码专属于 Gauss 核的尺寸校验。</para>
	///   <para><b>处置</b>按所用高斯算子文档给出合规尺寸（常由标准差 σ 推导核半径），不要套用一般形态学核的尺寸取值习惯。</para>
	/// </remarks>
	public const int Jl_ERR_WGAUSSM = 3022;

	/// <summary>错误码 3033：滤波器尺寸超过图像尺寸。</summary>
	/// <remarks>
	///   <para><b>含义</b>卷积/形态学核的边长大于待处理图像的对应尺寸，核无法在图上定位中心，报本错。常在处理极小 ROI 或被裁剪后的小图时触发。</para>
	///   <para><b>与相邻错误码的分工</b>本码判"核 vs 图像"的相对大小；核超过算子自身硬上限报 <see cref="Jl_ERR_FSTOBIG"/>，偶数尺寸报 <see cref="Jl_ERR_FSEVAN"/>，偏小报 <see cref="Jl_ERR_WFS"/>。</para>
	///   <para><b>坑</b>对缩小图像（如金字塔高层）反复用同一固定大核最容易撞到本码——尺寸随层数减半而核不变。</para>
	///   <para><b>处置</b>核尺寸不超过图像，或在过小 ROI 上先放大/换小核；对金字塔各层按层缩放核尺寸。</para>
	/// </remarks>
	public const int Jl_ERR_FSEIS = 3033;

	/// <summary>错误码 3034：滤波器尺寸为偶数，而算子要求奇数（原生文本 "evan" 为 "even" 的拼写笔误）。</summary>
	/// <remarks>
	///   <para><b>含义</b>多数卷积/形态学核要求边长为奇数，以便有唯一中心元素作锚点。传入偶数尺寸（如 4、6）即报本错。原生说明把 even 误写成 evan，语义仍是"尺寸为偶数"。</para>
	///   <para><b>与相邻错误码的分工</b>本码只判奇偶性；尺寸超图像报 <see cref="Jl_ERR_FSEIS"/>，超算子上限报 <see cref="Jl_ERR_FSTOBIG"/>，偏小报 <see cref="Jl_ERR_WFS"/>。</para>
	///   <para><b>处置</b>把尺寸改为相邻的奇数（±1），并确保核数组长度与之一致；注意尺寸参数与核数据本身必须同为奇数边长。</para>
	/// </remarks>
	public const int Jl_ERR_FSEVAN = 3034;

	/// <summary>错误码 3035：滤波器尺寸过大，超出算子允许的上限。</summary>
	/// <remarks>
	///   <para><b>含义</b>自定义核的边长超过该算子设定的最大值（内核对复杂度按平方增长，故设硬上限），报本错。上限值随算子而变，本仓库未集中列出 [待实测]。</para>
	///   <para><b>与相邻错误码的分工</b>核尺寸偏小/非法下限报 <see cref="Jl_ERR_WFS"/>；核尺寸大于图像报 <see cref="Jl_ERR_FSEIS"/>；尺寸为偶数（要求奇数）报 <see cref="Jl_ERR_FSEVAN"/>；内建索引核仅允许 3/5/7 报 <see cref="Jl_ERR_WLAWSS"/>。本码专指"超过算子自身上限"，与是否超图像是两回事。</para>
	///   <para><b>处置</b>减小核尺寸到该算子上限内；若确需大核效果，改用可分离/迭代实现或级联多次小核。</para>
	/// </remarks>
	public const int Jl_ERR_FSTOBIG = 3035;

	/// <summary>错误码 3036：传入的区域为空（不含任何像素）。</summary>
	/// <remarks>
	///   <para><b>含义</b>算子要求一个非空 <c>JlRegion</c>，但收到的区域面积为 0、无任何行程，即报本错。与"输入对象数为 0"的 <see cref="Jl_ERR_NOOB"/>、"多对象中某对象区域为空"的 <see cref="Jl_ERR_EMPOB"/> 是不同层次。</para>
	///   <para><b>坑</b>空区域常是上游静默产物：阈值筛空、交/差集结果为空、裁剪完全落在图外（对照 <see cref="Jl_ERR_RCOIMA"/>）。拿到本码先回溯是哪一步把区域算空。</para>
	///   <para><b>处置</b>调用前判 <c>area == 0</c> 或行程数为 0，为空则走无目标分支；不要指望它返回空结果继续下算。</para>
	/// </remarks>
	public const int Jl_ERR_EMPTREG = 3036;

	/// <summary>错误码 3037：多幅输入图像的定义域（区域）互不相同。</summary>
	/// <remarks>
	///   <para><b>含义</b>逐像素联合运算要求各路输入图像带有相同的有效定义域（domain，即每幅图被限定处理的区域一致）。只要两幅图的定义域不完全相同即报本错。</para>
	///   <para><b>与相邻错误码的分工</b>整幅尺寸不同报 <see cref="Jl_ERR_IWDS"/>，通道数不同报 <see cref="Jl_ERR_DNOC"/>，图像路数不同报 <see cref="Jl_ERR_UENOI"/>。本码专指宽高可同但"有效区域"不同这一层。</para>
	///   <para><b>处置</b>调用前用同一个区域统一设置各图定义域（reduce_domain），或全部还原为全域后再联合。</para>
	/// </remarks>
	public const int Jl_ERR_DOM_DIFF = 3037;

	/// <summary>错误码 3040：坐标的行值过大，超过常规表示上限 2^15-1（XL 表示为 2^30-1）。</summary>
	/// <remarks>
	///   <para><b>含义</b>行坐标（row=y，向下为正）在常规表示下最大 2^15-1，扩展表示（XL）最大 2^30-1；行值超过上限即报本错，管"行"的上界。</para>
	///   <para><b>与相邻错误码的分工</b>行过负报 <see cref="Jl_ERR_ROWTS"/>；列的两界报 <see cref="Jl_ERR_COLTB"/>、<see cref="Jl_ERR_COLTS"/>。四码合起来只管坐标幅值是否越出 16 位/XL 表示域，不判是否落在图像范围内。</para>
	///   <para><b>坑</b>过大正行常见于拼接/大位移后的行索引超出 int16，或单位混用（把亚像素当像素放大）。</para>
	///   <para><b>处置</b>校正尺度/平移使行索引回常规域；确需超大正坐标时改用支持 XL 表示的算子或数据结构。</para>
	/// </remarks>
	public const int Jl_ERR_ROWTB = 3040;

	/// <summary>错误码 3041：坐标的行值过负，低于常规表示下限 -2^15+1（XL 表示为 -2^30+1）。</summary>
	/// <remarks>
	///   <para><b>含义</b>行坐标（row=y，向下为正）在常规表示下用 16 位有符号整数，不得小于 -2^15+1；扩展表示（XL）放宽到 -2^30+1。行值比这更负即报本错，管"行"的下界。</para>
	///   <para><b>与相邻错误码的分工</b>行过正报 <see cref="Jl_ERR_ROWTB"/>；列的两个界报 <see cref="Jl_ERR_COLTS"/>、<see cref="Jl_ERR_COLTB"/>。本族只判坐标幅值是否越出表示域，不判是否落在图像内。</para>
	///   <para><b>坑</b>多因带大幅负平移的变换/配准把行索引推到负域，或单位（毫米）与像素混算放大量级；这类已非 int16 可容纳。</para>
	///   <para><b>处置</b>校正变换/偏移使行坐标回常规域；确需超大负坐标时改用支持 XL 表示的路径。</para>
	/// </remarks>
	public const int Jl_ERR_ROWTS = 3041;

	/// <summary>错误码 3042：坐标的列值过大，超过常规表示上限 2^15-1（XL 表示为 2^30-1）。</summary>
	/// <remarks>
	///   <para><b>含义</b>列坐标在常规表示下用 16 位有符号整数，不得大于 2^15-1；扩展表示（XL）放宽到 2^30-1。坐标超过上限即报本错。它管"列"的上界。</para>
	///   <para><b>与相邻错误码的分工</b>列过负报 <see cref="Jl_ERR_COLTS"/>；行的上/下界报 <see cref="Jl_ERR_ROWTB"/>、<see cref="Jl_ERR_ROWTS"/>。四码合起来只管坐标幅值，不判坐标是否落在图像内（域类码另说）。</para>
	///   <para><b>坑</b>过大正列常见于错误缩放（把像素当亚像素乘了大因子）、或超大幅面/拼接结果列索引越出 int16 域。</para>
	///   <para><b>处置</b>核对其余尺度/平移参数使列索引回到合法幅值；需要大坐标时改用支持 XL 表示的算子。</para>
	/// </remarks>
	public const int Jl_ERR_COLTB = 3042;

	/// <summary>错误码 3043：坐标的列值过负，低于常规表示下限 -2^15+1（XL 表示为 -2^30+1）。</summary>
	/// <remarks>
	///   <para><b>含义</b>区域/轮廓以行程或点列存列坐标，常规表示用 16 位有符号整数，列值不得小于 -2^15+1；扩展表示（XL）放宽到 -2^30+1。坐标比这更负即报本错。它管"列"的下界。</para>
	///   <para><b>与相邻错误码的分工</b>同族四码：列过正报 <see cref="Jl_ERR_COLTB"/>，行的下界/上界报 <see cref="Jl_ERR_ROWTS"/>、<see cref="Jl_ERR_ROWTB"/>。判的是坐标幅值本身，不是相对图像的越界（那类报 <see cref="Jl_ERR_ROOIMA"/> 等域码）。</para>
	///   <para><b>坑</b>多由矩阵变换把点推到极端负列、或单位混用（毫米级大坐标）叠加负平移造成；这类值已超出常规 int16 域，需 XL 表示才能容纳。</para>
	///   <para><b>处置</b>校正变换/偏移量使坐标落回常规域；确需超大坐标时改用支持 XL 表示的算子或数据结构。</para>
	/// </remarks>
	public const int Jl_ERR_COLTS = 3043;

	/// <summary>错误码 3100：分割阈值取值非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>直方图/自动分割类算子对"分割阈值"参数有取值约束（通常在 0–255 的灰度量纲内，或要求 min&lt;max），传空、负值、越界或上下限颠倒即报本错。确切边界随算子而变 [待实测]。</para>
	///   <para><b>坑</b>阈值是"数值"，与按字符串标志选特征名的 <see cref="Jl_ERR_UNKF"/>、<see cref="Jl_ERR_UNKG"/> 不是一类；本码只在数值非法时出现，图像/区域本身的问题另见 <see cref="Jl_ERR_EDEF"/> 等域类码。</para>
	///   <para><b>处置</b>核对阈值量纲与上下限顺序，确保落在算子允许区间；由自动估计得到的阈值要先判是否退化（如全图同灰度时的 NaN/越界）。</para>
	/// </remarks>
	public const int Jl_ERR_WRTHR = 3100;

	/// <summary>错误码 3101：请求了不认识的（区域几何）特征名。</summary>
	/// <remarks>
	///   <para><b>含义</b>区域特征类算子按字符串特征名选取要计算的几何量（面积、重心、主轴、圆度、矩形度一类），传入的名字不在该算子支持列表内即报本错。各算子支持的特征名不尽相同，本仓库未集中列举 [待实测]。</para>
	///   <para><b>与相邻错误码的分工</b>灰度类特征名不认识报 <see cref="Jl_ERR_UNKG"/>；本码专指几何特征。它属取值不认识，不是类型错（类型错落在 1201–1220 的 WIPT 族）。</para>
	///   <para><b>处置</b>逐字核对特征名字符串（大小写、下划线、单复数），必要时参考所用算子文档列出的合法名。</para>
	/// </remarks>
	public const int Jl_ERR_UNKF = 3101;

	/// <summary>错误码 3102：请求了不认识的灰度特征名。</summary>
	/// <remarks>
	///   <para><b>含义</b>灰度特征类算子（如计算区域上的均值/分位数/灰度直方图等）按字符串特征名选择要算的量，传入的名字不在合法名单内即报本错。合法名单随算子而变，本仓库未集中列出 [待实测]。</para>
	///   <para><b>与相邻错误码的分工</b>区域几何特征（面积、圆度、偏心率一类）名字不识别报 <see cref="Jl_ERR_UNKF"/>；本码专指灰度类特征。特征名大小写、拼写、单复数都会导致不识别。</para>
	///   <para><b>处置</b>对照所用算子文档里的特征名字符串逐字核对（注意是字符串标志位，类型本身没错、只是取值不认识）。</para>
	/// </remarks>
	public const int Jl_ERR_UNKG = 3102;

	/// <summary>错误码 3103：内部错误——轮廓切分（JlContCut）例程出错。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生 JlContCut 在内部一致性检查失败时报的笼统码，未指明具体子因。它专属于轮廓切分例程，与轮廓→多边形逼近族的 <see cref="Jl_ERR_EINCP1"/>、<see cref="Jl_ERR_EINCP2"/> 不同。</para>
	///   <para><b>坑</b>同例程更具体的入参不匹配有独立码：入口/出口点数不等报 <see cref="Jl_ERR_WNEE"/>。若拿到本码而非 WNEE，说明切分点落在轮廓外或内部状态异常，光调点数对不齐没用。</para>
	///   <para><b>处置</b>先确认切分点确实落在被切轮廓上、轮廓本身非空且点序正确；仍复现则记录码与 <c>JlNativeApi.GetErrorMessage(err)</c> 上报，因它标注为内部错误、不保证对外语义稳定 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_EINCC = 3103;

	/// <summary>错误码 3104：轮廓转多边形（JlContToPol）时相邻点距离过大。</summary>
	/// <remarks>
	///   <para><b>含义</b>轮廓→多边形逼近要求采样点间距不超过设定阈值；当轮廓点过于稀疏（间距大于允许值）时，逼近无法在给定精度下拟合，报本错。这里的距离阈值来自逼近参数，不是图像尺寸。</para>
	///   <para><b>与相邻错误码的分工</b>同族里因轮廓整体过长失败报 <see cref="Jl_ERR_EINCP2"/>；本码专指点间距过大。</para>
	///   <para><b>处置</b>先加密轮廓采样（对轮廓做插值/重采样），或放粗逼近容差，使点间距落入允许范围后重试。</para>
	/// </remarks>
	public const int Jl_ERR_EINCP1 = 3104;

	/// <summary>错误码 3105：轮廓转多边形（JlContToPol）时轮廓过长。</summary>
	/// <remarks>
	///   <para><b>含义</b>轮廓→多边形逼近对参与逼近的轮廓长度/点数设了上限，输入轮廓顶点过多或周长过大即报本错。上限值 [待实测]。</para>
	///   <para><b>与相邻错误码的分工</b>同族里因"相邻两点距离过大"失败报 <see cref="Jl_ERR_EINCP1"/>；轮廓裁剪相关内部错误报 <see cref="Jl_ERR_EINCC"/>。本码专指长度维度超限。</para>
	///   <para><b>处置</b>先对轮廓做降采样/抽点（增大逼近容差）以缩短参与逼近的点数，或裁剪到更小的轮廓范围后重试。</para>
	/// </remarks>
	public const int Jl_ERR_EINCP2 = 3105;

	/// <summary>错误码 3106：图像变换算出的结果行数过多，超出内部上限。</summary>
	/// <remarks>
	///   <para><b>含义</b>IPImageTransform 按变换/偏移推算结果外接范围并分配输出，当该范围为负偏移或过大导致行数超过内部可处理上限时报本错。上限值 [待实测]。</para>
	///   <para><b>何时遇到</b>常见于过大平移量、过大缩放使外接范围暴涨，或把整幅大图按超大角度旋转后需落地的行数超出目标缓冲。</para>
	///   <para><b>处置</b>收敛变换参数、先裁剪到合理 ROI 再变换，或按方位检查目标图像是否装得下（对照 <see cref="Jl_ERR_IHTL"/>、<see cref="Jl_ERR_IHTS"/>、<see cref="Jl_ERR_DITS"/>）。≥1000 属真错误。</para>
	/// </remarks>
	public const int Jl_ERR_TMR = 3106;

	/// <summary>错误码 3107：图像缩放因子为 0.0，会退化成零尺寸输出。</summary>
	/// <remarks>
	///   <para><b>含义</b>IPImageScale 类缩放要求横、纵缩放因子非零。当因子传成 0.0（或某路计算结果恰为 0）时，目标宽/高会被算成 0，故直接拒算报本错。</para>
	///   <para><b>坑</b>因子常由"目标尺寸÷源尺寸"算出，若目标尺寸被误设为 0，或因整数除法把小数因子截断为 0，都会触发本码；这与 <see cref="Jl_ERR_OOR"/> 的矩阵越界是两回事。</para>
	///   <para><b>处置</b>保证缩放因子为正且非零，用浮点计算目标/源比例，别让整数除法吃掉小数。</para>
	/// </remarks>
	public const int Jl_ERR_SFZ = 3107;

	/// <summary>错误码 3108：变换矩阵元素的取值范围非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>齐次变换矩阵里某个分量落在算子允许范围之外（如出现非仿射的透视项、NaN/Inf、或超出数值上限的极端缩放）。与"矩阵根本不是二维仿射"的 <see cref="Jl_ERR_NO_AFFTRANS"/> 不同，本码判的是分量取值越界，而非最后一行结构是否等于 (0,0,1)。</para>
	///   <para><b>坑</b>手工拼矩阵、或把旋转角/平移量用错单位（度当弧度、像素当毫米）后，矩阵数值会畸大到触发本码；由投影/标定链降下来的矩阵更易越界。</para>
	///   <para><b>处置</b>用矩阵构造函数重新生成，核对每个分量单位与量级，避免直接手写常数填充。</para>
	/// </remarks>
	public const int Jl_ERR_OOR = 3108;

	/// <summary>错误码 3109：内部错误——IPvvf 例程没有空闲元素可用。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生 IPvvf 例程（名称含义未在本仓库文档给出，疑与矢量/多边形处理相关 [待实测]）从其固定大小的内部池中取元素，池被占满且无元素可回收时报本错。属内部资源耗尽，而非调用方参数错。</para>
	///   <para><b>坑</b>与 <see cref="Jl_ERR_DCDG_FLE"/>（调度期空闲表为空）同为"空闲元素耗尽"，但发生在不同原生模块；两者都不该由业务代码预判，触发方 [待实测]。</para>
	///   <para><b>处置</b>按运行时内部错误对待：减少单次并发/超大数据量、拆分输入规模后重试，并把码连同 <c>JlNativeApi.GetErrorMessage(err)</c> 文本上报。≥1000，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_NEF = 3109;

	/// <summary>错误码 3110：输入对象个数为 0。</summary>
	/// <remarks>
	///   <para><b>含义</b>算子至少要一路输入对象才能工作，收到的对象元组为空（个数=0）即报本错。区别于对象个数够、但其中有空区域的 <see cref="Jl_ERR_EMPOB"/>，以及单个区域本身为空的 <see cref="Jl_ERR_EMPTREG"/>。</para>
	///   <para><b>何时遇到</b>典型来自上游筛选后一个对象都不剩（面积/灰度条件太严），或把一个尚未装载对象的空元组直接传下。</para>
	///   <para><b>处置</b>先用 <c>CountObj()</c> 判对象数是否为 0，为空则走"无目标"分支而不是继续调用；否则放宽上游筛选条件。</para>
	/// </remarks>
	public const int Jl_ERR_NOOB = 3110;

	/// <summary>错误码 3111：多输入对象中至少有一个的区域为空。</summary>
	/// <remarks>
	///   <para><b>含义</b>算子要求每一路输入对象都带有非空区域，只要有一路的区域没有任何像素（空区域）即报本错。它是"部分为空"，与单个区域为空的 <see cref="Jl_ERR_EMPTREG"/>、输入对象数为 0 的 <see cref="Jl_ERR_NOOB"/> 是三种不同触发点。</para>
	///   <para><b>坑</b>空区域常由上游静默产生：阈值把某路筛没了、裁剪落在图外、连通分割后按序号筛选拿不到对应块。此时"个数"对但"内容为空"，容易被误当成个数错误去排查。</para>
	///   <para><b>处置</b>逐路检查 <c>JlRegion</c> 是否为空（面积/行程为 0），补齐或过滤空对象后再调用；预期可能为空时应先判空再决定跳过分量。</para>
	/// </remarks>
	public const int Jl_ERR_EMPOB = 3111;

	/// <summary>错误码 3112：算子只接受矩形且边长为 2 的整数次幂的图像。</summary>
	/// <remarks>
	///   <para><b>含义</b>快速傅里叶变换一类频谱运算要求图像宽、高均为 2 的幂（如 256、512），且必须是完整矩形域（非任意区域/异形掩膜）。任一维不是 2**n，或图像带非矩形定义域，即报本错。</para>
	///   <para><b>何时遇到</b>最常见于直接拿相机原始尺寸（如 1920×1080 非 2 的幂）做 FFT/频域滤波。与尺寸相对核过大的 <see cref="Jl_ERR_FSTOBIG"/>、行/列为 0 的空域错误不同，这里判的是"是否 2 的幂 + 是否矩形"。</para>
	///   <para><b>处置</b>先裁剪或零填充到最近的 2 的幂尺寸再变换，结果记回原始坐标时撤销该 padding。是否允许非 2 幂的替代路径 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NPOT = 3112;

	/// <summary>错误码 3113：滞后阈值算子筛出的相关点过多，超出内部缓冲。</summary>
	/// <remarks>
	///   <para><b>含义</b>滞后（hysteresis）阈值算子用高低两道门限保留像素，当低门限取得过低、或图像整体对比度使大量像素越过高门限时，被判为"相关点"的数量超过内部缓冲上限即报本错。上限值 [待实测]。</para>
	///   <para><b>何时遇到</b>多因高/低阈值设得太宽、或对噪声/纹理丰富的图未先降噪，导致候选点爆炸。与 Hough 累计点爆炸（<see cref="Jl_ERR_WNOLI"/>）同属"候选量超缓冲"，但发生在不同算子。</para>
	///   <para><b>处置</b>抬高低门限、缩小处理区域或先做平滑，减少进入结果的相关点数。≥1000 属真错误，会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_TMEP = 3113;

	/// <summary>错误码 3114：标签图中不同标签的个数超过上限。</summary>
	/// <remarks>
	///   <para><b>含义</b>标签计数类算子为"不同编号"的个数设有内部上限，连通域过碎（如噪声导致成百上千个微小块各自成标签）时，标签数突破该上限报本错。具体上限值 [待实测]。</para>
	///   <para><b>与相邻错误码的分工</b>单个标签值为负报 <see cref="Jl_ERR_NNLA"/>；输入对象数为 0 报 <see cref="Jl_ERR_NOOB"/>。本码专指标签种类数越界，与图像像素尺寸、矩阵规模上限（<see cref="Jl_ERR_NUM_COLS"/>）不是一回事。</para>
	///   <para><b>处置</b>先做形态学开运算或按面积过滤掉细碎区域再取标签，降低不同编号个数；必要时分区处理。</para>
	/// </remarks>
	public const int Jl_ERR_LTB = 3114;

	/// <summary>错误码 3115：标签图中出现负值标签，不被允许。</summary>
	/// <remarks>
	///   <para><b>含义</b>标签图（每个连通域/区域被涂成一个整数编号的图）约定编号非负；出现负数即报本错。多由把有符号字节/整型直接当标签、或人为写入 -1 作"背景/无效"哨兵引起。</para>
	///   <para><b>与相邻错误码的分工</b>标签数量（不同编号个数）过多报 <see cref="Jl_ERR_LTB"/>；本码只管单个标签值为负。灰度共生等按量化级数发错的另有 <see cref="Jl_ERR_COHTS"/>、<see cref="Jl_ERR_COWTS"/>。</para>
	///   <para><b>处置</b>用非负整数编号，背景留 0 而非 -1；若上游产生负值，先做偏移/裁剪使全部标签落入合法范围再调用。</para>
	/// </remarks>
	public const int Jl_ERR_NNLA = 3115;

	/// <summary>错误码 3116：滤波器尺寸非法，原生文本以问号标注疑为"过小"。</summary>
	/// <remarks>
	///   <para><b>含义</b>自定义卷积/相关核的尺寸不在算子可处理的下限之上。原生说明写作"too small ?"，带问号表示这一归因是近似表述、并非确切判定，实际也可能是尺寸与其它约束冲突 [待实测]。</para>
	///   <para><b>与相邻错误码的分工</b>核尺寸超过图像报 <see cref="Jl_ERR_FSEIS"/>；尺寸为偶数（要求奇数）报 <see cref="Jl_ERR_FSEVAN"/>；相对图像过大报 <see cref="Jl_ERR_FSTOBIG"/>；Gauss 专用尺寸非法报 <see cref="Jl_ERR_WGAUSSM"/>；内建索引核只允许 3/5/7 报 <see cref="Jl_ERR_WLAWSS"/>。本码专指"尺寸偏小/非法下限"这一类。</para>
	///   <para><b>处置</b>增大核尺寸到该算子的合法下限（并保持所需奇偶），核对文档给出的尺寸区间。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WFS = 3116;

	/// <summary>错误码 3117：多幅输入图像之间尺寸不一致。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>要求逐像素对齐的多路输入（图像运算、拼接、差分类）中，任意两幅的宽或高不同即报本错。</para>
	///   <para><b>与相邻错误码的分工</b>源与目标这一对尺寸不同报 <see cref="Jl_ERR_DSIZESD"/>；通道数不同报 <see cref="Jl_ERR_DNOC"/>；本码只管多路输入间的宽高等。排查时注意采集裁剪常静默改变尺寸。</para>
	/// </remarks>
	public const int Jl_ERR_IWDS = 3117;

	/// <summary>错误码 3118：目标图像过宽或落点过于偏右。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>结果写入自备目标图像时右边界越出图像。左边界对应 <see cref="Jl_ERR_IWTS"/>，上下对应 <see cref="Jl_ERR_IHTL"/>、<see cref="Jl_ERR_IHTS"/>。</para>
	///   <para><b>排查</b>核对起始列＋结果宽度是否超出目标图像列数。</para>
	/// </remarks>
	public const int Jl_ERR_IWTL = 3118;

	/// <summary>错误码 3119：目标图像过窄或落点过于偏左。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>结果写入自备目标图像时左边界越出图像（起始列太靠左/为负）。右边界对应 <see cref="Jl_ERR_IWTL"/>，上下对应 <see cref="Jl_ERR_IHTL"/>、<see cref="Jl_ERR_IHTS"/>。</para>
	///   <para><b>排查</b>检查平移分量与起始列参数，必要时重新分配目标图像。</para>
	/// </remarks>
	public const int Jl_ERR_IWTS = 3119;

	/// <summary>错误码 3120：目标图像纵向范围过界或落点过于偏下。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>结果写入自备目标图像时下边界越出图像（起始行太靠下，或与 <see cref="Jl_ERR_IHTS"/> 构成上下两侧检查）。左右两侧对应 <see cref="Jl_ERR_IWTL"/>、<see cref="Jl_ERR_IWTS"/>。</para>
	///   <para><b>排查</b>按变换后的外接行范围重新定位或加大目标图像。</para>
	/// </remarks>
	public const int Jl_ERR_IHTL = 3120;

	/// <summary>错误码 3121：目标图像高度过小或落点过于偏上，结果放不进。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>往自备目标图像写入结果时，起始行位置太靠上（负偏移）或图像太矮，纵向范围越界。四个方位对应 3118（过宽/偏右）、3119（过窄/偏左）、3120（过高/偏下）、3121（本码），高度不足另有 <see cref="Jl_ERR_DITS"/>。</para>
	///   <para><b>排查</b>按变换/偏移后的实际外接范围重新分配目标图像。</para>
	/// </remarks>
	public const int Jl_ERR_IHTS = 3121;

	/// <summary>错误码 3122：各输入操作数的通道数互不相同。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>多路图像联合运算（加减、合成、按通道配对一类）要求各路通道数相等，单通道与三通道混用即报本错。</para>
	///   <para><b>与相邻错误码的分工</b>尺寸不齐报 <see cref="Jl_ERR_IWDS"/>，图像路数不齐报 <see cref="Jl_ERR_UENOI"/>；本码只管通道数。排查时先统一拆合通道再调用。</para>
	/// </remarks>
	public const int Jl_ERR_DNOC = 3122;

	/// <summary>错误码 3123：彩色滤光阵列（CFA）类型参数非法。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>demosaic 或 Bayer 相关换算要求指明滤光阵列排布（RGBG/GRBG/GBRG/BGGR 一类的相位枚举），传入了本库不识别的值。插值方法错则报 <see cref="Jl_ERR_WRCFAINT"/>。</para>
	///   <para>本库实际支持的类型枚举名单 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WRCFAFLT = 3123;

	/// <summary>错误码 3124：彩色滤光阵列（CFA）插值方法参数非法。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>去 mosaic（demosaic）类算子的插值模式参数不在支持集合内。类型错报 <see cref="Jl_ERR_WRCFAFLT"/>，本码只管插值方法。</para>
	///   <para>本库确切的合法插值值集 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WRCFAINT = 3124;

	/// <summary>错误码 3125：齐次矩阵不表示二维仿射变换。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>只支持仿射的算子会校验齐次矩阵最后一行为 (0, 0, 1)（允许归一化后），含透视分量或行被写坏时报本错。</para>
	///   <para><b>坑</b>手工拼矩阵时常忘了整行平移分量占两格导致错位；由投影变换降下来的矩阵不能喂给仿射族算子。校验的归一化与容差细节 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NO_AFFTRANS = 3125;

	/// <summary>错误码 3126：待修补（inpainting）区域离图像边界过近。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>修补算法要靠区域周围的完好像素向内传播信息，区域紧贴图像边界时一侧没有可用样本，故直接拒算而非给出边缘伪迹。</para>
	///   <para><b>缓解</b>剔除贴边区域，或先扩边界（pad）再修补最后裁回。算法要求的最小边距 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_INPNOBDRY = 3126;

	/// <summary>错误码 3127：源图像与目标图像尺寸不一致。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>按"源→目标"逐像素写回的运算要求两图宽高完全相等，只要有一维不同即报本错。</para>
	///   <para><b>与相邻错误码的分工</b>多路平行输入之间尺寸不齐报 <see cref="Jl_ERR_IWDS"/>，本码专指源与目标这一对。目标图尺寸不足（放不下）则按方位报 3118–3121、3146 一族。</para>
	/// </remarks>
	public const int Jl_ERR_DSIZESD = 3127;

	/// <summary>错误码 3129：反射（镜像）轴未定义。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>反射类运算需要一个能唯一确定镜像轴的几何参数（轴上一点加方向，或两点），传入退化参数（方向为零向量、两点重合）时轴无法定义，报本错。</para>
	///   <para><b>排查</b>用两个可区分的点定义轴，或给非零方向向量；勿把平移矩阵的最后一行当方向用。</para>
	/// </remarks>
	public const int Jl_ERR_AXIS_UNDEF = 3129;

	/// <summary>错误码 3131：共生矩阵按量化参数得到的列数（灰度级）过少。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>量化级数过小导致共生矩阵列维退化。行维的同款错误是 <see cref="Jl_ERR_COHTS"/>。</para>
	///   <para><b>排查</b>增大量化参数使灰度级数落在可用范围；内存不够时再看 <see cref="Jl_ERR_COOC_MEM"/> 的反向约束。</para>
	/// </remarks>
	public const int Jl_ERR_COWTS = 3131;

	/// <summary>错误码 3132：共生矩阵按量化参数得到的行数（灰度级）过少。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>量化级数取得过小，灰度共生矩阵的行维退化到无法统计。列维的同款错误是 <see cref="Jl_ERR_COWTS"/>。</para>
	///   <para><b>排查</b>结合图像实际灰度跨度调大量化参数；图像本身对比度极低时先做灰度线性变换再统计。</para>
	/// </remarks>
	public const int Jl_ERR_COHTS = 3132;

	/// <summary>错误码 3133：列数与声明或期望不符。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读入的数据实际列数与文件头/参数声明不一致，多发生在按维数拼装矩阵或查找表时。行维的同款错误是 <see cref="Jl_ERR_NUM_LINES"/>。</para>
	///   <para><b>排查</b>核对数据源的维数与参数；规模超限是另一码 <see cref="Jl_ERR_NUM_COLS"/>，不要混淆。</para>
	/// </remarks>
	public const int Jl_ERR_NUM_COLMN = 3133;

	/// <summary>错误码 3134：行数与声明或期望不符。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读入的数据实际行数与文件头/参数声明的行数不一致。列维的同款错误是 <see cref="Jl_ERR_NUM_COLMN"/>。</para>
	///   <para><b>排查</b>核对数据源的实际维数与传入参数是否一致，警惕上游算子裁剪/抽样后维数已变。</para>
	/// </remarks>
	public const int Jl_ERR_NUM_LINES = 3134;

	/// <summary>错误码 3135：读入的数值位数过多，超出内部表示范围。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>解析文件里的数字项时，单个数值的位数大到内部类型无法表示，按溢出处理。</para>
	///   <para><b>排查</b>检查文件是否被外部工具写入了异常大的数；与格式整体不符的 <see cref="Jl_ERR_SYNTAX"/> 不同，本码只针对越界的数。</para>
	/// </remarks>
	public const int Jl_ERR_OVL = 3135;

	/// <summary>错误码 3136：要求对称的矩阵输入不对称。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>协方差、共生矩阵等只定义在对称矩阵上的运算，读入后校验 M 与 M 的转置不等而报本错。</para>
	///   <para><b>坑</b>统计估计结果经数值噪声后可能微小偏离严格对称，从而被误判；传入前可先按 (M+Mᵀ)/2 强制对称化。本库采用的对称性判定容差 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NOT_SYM = 3136;

	/// <summary>错误码 3137：矩阵规模超过算子可处理的上限。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>矩阵类运算（分解、求逆、解方程一类）对列数/总规模设有固定上限，输入矩阵超限时报本错。具体上限值 [待实测]。</para>
	///   <para><b>与相邻错误码的分工</b>行列数与声明不符分别报 <see cref="Jl_ERR_NUM_LINES"/>、<see cref="Jl_ERR_NUM_COLMN"/>，那两类是"数目不对"而非"太大"。</para>
	/// </remarks>
	public const int Jl_ERR_NUM_COLS = 3137;

	/// <summary>错误码 3138：文件结构不符合格式约定。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>文件能打开，但语法解析在应有内容处对不上（键、数据段或行的组织方式不符合格式），常见于被手工/第三方编辑过或保存方版本不兼容。</para>
	///   <para><b>与相邻错误码的分工</b>连打开都失败报 <see cref="Jl_ERR_NO_FILE"/>；缺整项字段报 <see cref="Jl_ERR_MISSING"/> 族以外的解析层错误也常归本码，两者界限按内部解析阶段划分 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SYNTAX = 3138;

	/// <summary>错误码 3139：可用于运算的矩阵不足两个。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>需要至少两路矩阵输入的运算（矩阵间比较、合成一类）拿到的矩阵个数少于 2，例如元组里矩阵被上游算子留空。</para>
	///   <para><b>与相邻错误码的分工</b>矩阵规模超限报 <see cref="Jl_ERR_NUM_COLS"/>；本码只管"个数不够"。</para>
	/// </remarks>
	public const int Jl_ERR_MISSING = 3139;

	/// <summary>错误码 3140：计算灰度共生矩阵时内存不足。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>共生矩阵规模随量化级数的平方增长且需按幅面累计，量化级取得过大或图像过大会导致分配失败。</para>
	///   <para><b>缓解</b>减小量化参数以降低灰度级数，或先在更小 ROI 上统计。与通用内存错误的区分：常量名 COOC 表明本码专属于共生矩阵族。</para>
	/// </remarks>
	public const int Jl_ERR_COOC_MEM = 3140;

	/// <summary>错误码 3141：文件无法读取。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>加载图像、模型等文件时文件不存在、无读权限或被占用读不到。只针对读方向；写方向失败报 <see cref="Jl_ERR_FILE_WR"/>，能读到流但解析出结构问题报 <see cref="Jl_ERR_MISSING"/>。</para>
	///   <para><b>排查</b>核对路径拼写、扩展名与文件可访问性；网络盘/移动盘断链也走本错。</para>
	/// </remarks>
	public const int Jl_ERR_NO_FILE = 3141;

	/// <summary>错误码 3142：文件无法以写方式打开。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>导出图像、模型或其他数据文件时，目标路径目录不存在、无写权限或文件被其他进程占用。此时尚未写入任何内容，磁盘上可能残留零字节文件或旧文件未被覆盖。</para>
	///   <para><b>与相邻错误码的分工</b>读方向失败报 <see cref="Jl_ERR_NO_FILE"/>，能打开但格式结构坏了报 <see cref="Jl_ERR_MISSING"/>。</para>
	/// </remarks>
	public const int Jl_ERR_FILE_WR = 3142;

	/// <summary>错误码 3143：查找表的颜色表项数超过上限。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>建立索引色/颜色映射查找表时，给定的颜色条目数量大于算子支持的最大表长。上限值 [待实测]。</para>
	///   <para><b>缓解</b>降低量化级数（减少颜色种类），或分多张表映射。</para>
	/// </remarks>
	public const int Jl_ERR_NUM_LUCOLS = 3143;

	/// <summary>错误码 3145：Hough 变换累计出的候选点（直线）过多，超出内部缓冲上限。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>Hough 类检测沿参数空间累计每个输入轮廓点，累加器精度取得过细或输入点数过多时，候选数爆炸并触发本错。</para>
	///   <para><b>缓解</b>放粗角度/距离分辨率、先用抽样或 ROI 裁剪减少参与累计的轮廓点。具体上限值 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WNOLI = 3145;

	/// <summary>错误码 3146：目标图像高度不足，容纳不下要写入的结果。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>运算结果需写入调用方自备的目标图像，而目标图像高度小于结果所需（叠加位置偏移后放不下）时报本错。同族检查按方位分为 <see cref="Jl_ERR_IWTL"/>、<see cref="Jl_ERR_IWTS"/>、<see cref="Jl_ERR_IHTL"/>、<see cref="Jl_ERR_IHTS"/> 与尺寸整体不符的 <see cref="Jl_ERR_IWDS"/>。</para>
	///   <para><b>排查</b>先按源尺寸与变换/偏移参数推算结果范围，再分配足够大的目标图像。</para>
	/// </remarks>
	public const int Jl_ERR_DITS = 3146;

	/// <summary>错误码 3147：插值模式参数取值非法。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>图像缩放、几何变换、映射重采样一类算子的插值方式参数只接受各自约定的枚举值（最近邻、双线性、双立方一类），传入集合外的值报本错。</para>
	///   <para><b>坑</b>各算子支持的插值值集并不保证相同，不要把一个算子里能用的写法照搬到另一个。本库确切的合法值集 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WINTM = 3147;

	/// <summary>错误码 3148：输入区域不满足"紧凑且连通"的前提。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>算法假设区域是单个紧凑连通块（每行至多一段连续行程、块间连通），区域断开、含孔洞或行间脱开时报本错。</para>
	///   <para><b>排查</b>先做连通分割取单个连通分量，必要时先填孔再调用。具体校验该前提的算子清单 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_THICK_NK = 3148;

	/// <summary>错误码 3170：滤波器尺寸为 3 时，给定的滤波器索引不在该尺寸支持的索引集合内。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>算子以"索引＋尺寸"选取内建滤波器，索引与尺寸必须配套；尺寸为 3 时传入其他尺寸才有的索引即报本错。尺寸 5、7 的对应错误码见 <see cref="Jl_ERR_WIND5"/>、<see cref="Jl_ERR_WIND7"/>。</para>
	///   <para><b>排查</b>核对所用算子对每个尺寸列出的合法索引；尺寸本身非法（不是 3/5/7）会先报 <see cref="Jl_ERR_WLAWSS"/>。采用这种选核方式的算子清单 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WIND3 = 3170;

	/// <summary>错误码 3171：滤波器尺寸为 5 时，给定的滤波器索引不在该尺寸支持的索引集合内。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>部分滤波算子不直接给核参数，而以"索引＋尺寸"两个参数选取内建滤波器；索引与尺寸必须配套。尺寸为 5 时传入只在其他尺寸下存在的索引即报本错。</para>
	///   <para><b>与相邻错误码的分工</b>尺寸本身不在允许集合（仅 3/5/7）报 <see cref="Jl_ERR_WLAWSS"/>；尺寸 3 与 7 的同类索引错误分别报 <see cref="Jl_ERR_WIND3"/>、<see cref="Jl_ERR_WIND7"/>。具体哪些算子采用这种选核方式及其索引表 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WIND5 = 3171;

	/// <summary>错误码 3172：滤波器尺寸为 7 时，给定的滤波器索引不在该尺寸支持的索引集合内。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>算子以"尺寸＋索引"从内建核里选核，尺寸与索引必须配套。尺寸已确认为 7，但传入的索引只在其它尺寸下存在（每个尺寸的合法索引集合不同），即报本错。</para>
	///   <para><b>与相邻错误码的分工</b>尺寸 3、5 的同类索引错误分别报 <see cref="Jl_ERR_WIND3"/>、<see cref="Jl_ERR_WIND5"/>；尺寸本身不在 3/5/7 里会先被 <see cref="Jl_ERR_WLAWSS"/> 拦下，不会走到本码。</para>
	///   <para><b>排查</b>核对所用算子对尺寸 7 列出的合法索引清单；哪些算子采用这种"索引＋尺寸"选核方式及其各尺寸索引表 [待实测]。本码 ≥1000 属真错误，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WIND7 = 3172;

	/// <summary>错误码 3173：内建滤波器的尺寸参数不在允许集合（仅 3/5/7）。</summary>
	/// <remarks>
	///   <para><b>含义</b>部分滤波算子不直接给卷积核，而用"尺寸＋索引"两参数从内建核里选一个，尺寸只允许 3、5、7 三种。传入其它值（偶数、或 9/11 等更大核）即报本错。它管尺寸本身是否合法，索引与尺寸是否配套由 3170–3172 一族负责。</para>
	///   <para><b>与相邻错误码的分工</b>尺寸合法但索引不属于该尺寸报 <see cref="Jl_ERR_WIND3"/>、<see cref="Jl_ERR_WIND5"/>、<see cref="Jl_ERR_WIND7"/>；核尺寸相对图像过大报 <see cref="Jl_ERR_FSTOBIG"/>、超出图像报 <see cref="Jl_ERR_FSEIS"/>。注意本族限定的是"内建核尺寸枚举"，自定义核的尺寸另按奇偶（<see cref="Jl_ERR_FSEVAN"/>）与大小校验。</para>
	///   <para><b>坑</b>本码 ≥1000，<c>JlNativeApi.IsError</c> 判 true，<c>IsFailure</c> 亦判失败，统一检查会抛 <c>JlOperatorException</c>；它是参数越界而非资源/线程问题，不要去查环境。</para>
	///   <para><b>处置</b>把尺寸改回 3/5/7 之一再调用；确需更大或自定义核时，改用接受显式核参数的算子而不是内建索引选核。</para>
	/// </remarks>
	public const int Jl_ERR_WLAWSS = 3173;

	/// <summary>噪声估计错误：参与统计的有效像素太少，无法可靠地估计噪声（错误码值 3175）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>噪声估计相关算子在统计时，落入可用范围内的像素数量不足，噪声参数无法稳定求解即返回此码。</para>
	///   <para><b>处理建议</b>扩大参与噪声估计的区域或提高样本像素数量，使其达到求解下限后重试。</para>
	/// </remarks>
	public const int Jl_ERR_NE_NPTS = 3175;

	/// <summary>JlContCut 错误：入口(entry)与出口(exit)数量不一致（错误码值 3200）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>调用 JlContCut 对轮廓做裁剪/切分时，传入的入口点数量与出口点数量不相等，无法成对匹配即返回此码。</para>
	///   <para><b>处理建议</b>核对入口、出口两组参数，使二者元素个数一一对应后重试。JlContCut 入口/出口的确切配对语义 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WNEE = 3200;

	/// <summary>缺少轮廓引用：所需引用的轮廓对象不存在（错误码值 3201）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>某个需要引用轮廓(XLD)的操作在运行时找不到对应引用，通常是被引用的轮廓尚未创建或已被释放。</para>
	///   <para><b>处理建议</b>确保传入的轮廓对象在被引用期间仍有效，避免提前释放后仍保留其引用。</para>
	/// </remarks>
	public const int Jl_ERR_REF = 3201;

	/// <summary>XLD 类型错误：轮廓类型与算子要求不符（错误码值 3250）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>把某类轮廓(XLD)传给只接受另一类型轮廓的算子，类型不匹配即返回此码。</para>
	///   <para><b>处理建议</b>先确认轮廓类型与算子输入要求一致，必要时用类型转换算子转换后再调用。</para>
	/// </remarks>
	public const int Jl_ERR_XLDWT = 3250;

	/// <summary>轮廓点错误：图像边界点被置为前景(FG)（错误码值 3252）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>从灰度图提取轮廓时，落在图像边界上的点被判为前景，导致边界处轮廓不完整/不闭合。</para>
	///   <para><b>处理建议</b>提取前对图像加边界填充，或避免把边界点当作有效前景。</para>
	/// </remarks>
	public const int Jl_ERR_XLD_RPF = 3252;

	/// <summary>轮廓超限：单条轮廓长度超过上限（错误码值 3253）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>生成或合并轮廓时，某条轮廓的点数超过系统允许的单条轮廓长度上限。</para>
	///   <para><b>处理建议</b>缩小输入区域、降低分辨率或拆分过长轮廓，使点数落到上限以内。具体上限数值 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_XLD_MCL = 3253;

	/// <summary>轮廓超限：轮廓条数超过上限（错误码值 3254）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>一次操作中生成的轮廓条数超过系统允许的最大轮廓数。</para>
	///   <para><b>处理建议</b>缩小输入区域或减少连通分量数量后重试。上限数值 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_XLD_MCN = 3254;

	/// <summary>轮廓过短：点数不足以计算方向角（错误码值 3255）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>对轮廓按弧长比例取点求切线角时，轮廓过短、可取点数不足，无法满足按角度提取算子的要求。</para>
	///   <para><b>处理建议</b>只对足够长的轮廓求角，或改用对短轮廓更稳健的角度获取方式。所需最小点数 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_XLD_CTS = 3255;

	/// <summary>轮廓回归参数已计算：重复计算已就绪的参数（错误码值 3256）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>再次请求计算某组轮廓的回归(拟合)参数，而该参数此前已算出；属状态提示而非致命错误，与 3257（尚未计算）互为反向。</para>
	///   <para><b>处理建议</b>直接复用已存在的回归结果；仅当轮廓集合发生变化时才需要重算。</para>
	/// </remarks>
	public const int Jl_ERR_XLD_CRD = 3256;

	/// <summary>轮廓回归参数未计算：使用了尚未算出的参数（错误码值 3257）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取某组轮廓的回归(拟合)参数，但从未执行计算，参数处于未填充状态；与 3256（已计算）互为反向。</para>
	///   <para><b>处理建议</b>先执行轮廓回归参数的计算步骤，再读取使用。</para>
	/// </remarks>
	public const int Jl_ERR_XLD_CRND = 3257;

	/// <summary>数据管理错误：目标 XLD 轮廓对象已被删除（错误码值 3258）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>按标识访问轮廓时，对应 XLD 对象已从内部数据管理中删除，旧标识失效。</para>
	///   <para><b>处理建议</b>删除轮廓后不要继续用其旧标识访问；访问前确认对象仍在数据管理中。</para>
	/// </remarks>
	public const int Jl_ERR_DBXC = 3258;

	/// <summary>数据管理错误：对象没有 XLD-ID（错误码值 3259）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>把某对象当作按 XLD-ID 索引的轮廓访问，但该对象未分配 XLD-ID。</para>
	///   <para><b>处理建议</b>确认对象确为可分配 XLD-ID 的轮廓类型，并已登记进数据管理后再按 ID 访问。</para>
	/// </remarks>
	public const int Jl_ERR_DBWXID = 3259;

	/// <summary>轮廓点分配错误：分配的点数与请求数不符（错误码值 3260）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>创建或填充轮廓时，实际分配的点数与指定数量不一致，属内部一致性错误。</para>
	///   <para><b>处理建议</b>核对传入的点数与行/列坐标数组长度是否一致后重试。</para>
	/// </remarks>
	public const int Jl_ERR_XLD_WNP = 3260;

	/// <summary>轮廓属性未定义：请求了尚未定义的轮廓属性（错误码值 3261）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取某条轮廓的附加属性，但该属性从未被写入或定义。</para>
	///   <para><b>处理建议</b>读取前先设置该属性，或改用确实存在的属性名称。</para>
	/// </remarks>
	public const int Jl_ERR_XLD_CAND = 3261;

	/// <summary>椭圆拟合失败：无法用给定点拟合出椭圆（错误码值 3262）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>用轮廓/点集拟合椭圆时点不足或分布退化（如近乎共线），导致无稳定解。</para>
	///   <para><b>处理建议</b>增加并合理分布拟合点，使其覆盖足够弧段；数据本身非椭圆时改拟合其它基元。</para>
	/// </remarks>
	public const int Jl_ERR_FIT_ELLIPSE = 3262;

	/// <summary>圆拟合失败：无法用给定点拟合出圆（错误码值 3263）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>用轮廓/点集拟合圆时点不足，或点近乎共线导致无稳定圆解。</para>
	///   <para><b>处理建议</b>保证参与拟合的点足够多且分布在明显弧段上。</para>
	/// </remarks>
	public const int Jl_ERR_FIT_CIRCLE = 3263;

	/// <summary>拟合裁剪过度：所有点都被判为离群点（错误码值 3264）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>稳健拟合时 ClippingFactor 取得过小，或所用点本身与目标基元形态差异过大，导致没有点被接受为内点。</para>
	///   <para><b>处理建议</b>放大 ClippingFactor，或改用更接近目标基元的点集再拟合。ClippingFactor 的具体尺度 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_FIT_CLIP = 3264;

	/// <summary>四边形拟合失败：无法拟合出四边形（错误码值 3265）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>对点/轮廓拟合四边形时，边角无法确定或点分布不满足四边形假设。</para>
	///   <para><b>处理建议</b>为四条边分别提供对应的点，或在数据本身更接近其它形状时退化到拟合直线/矩形。</para>
	/// </remarks>
	public const int Jl_ERR_FIT_QUADRANGLE = 3265;

	/// <summary>矩形不完整：至少一条矩形边没有对应点（错误码值 3266）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>拟合矩形时，四条边中某条边缺少点支撑，矩形无法完整确定。</para>
	///   <para><b>处理建议</b>补全缺失边上的点，使每条边都有输入点后再拟合。</para>
	/// </remarks>
	public const int Jl_ERR_INCOMPL_RECT = 3266;

	/// <summary>轮廓越界：轮廓点落在图像之外（错误码值 3267）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>访问或运算轮廓时，某点的行列坐标超出图像边界。</para>
	///   <para><b>处理建议</b>裁剪或平移轮廓使全部点落回图像范围内；核对坐标换算(row=y、column=x)是否用反。</para>
	/// </remarks>
	public const int Jl_ERR_XLD_COI = 3267;

	/// <summary>拟合点不足：可用于模型拟合的点数不够（错误码值 3274）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>拟合基元/模型时，输入点数量低于该拟合所要求的下限。</para>
	///   <para><b>处理建议</b>放宽提取条件以获取更多点，或降低对拟合复杂度的要求。各拟合所需下限点数 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_FIT_NOT_ENOUGH_POINTS = 3274;

	/// <summary>缺少世界文件：找不到 ARC/INFO 的 world file（错误码值 3275）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 ARC/INFO 矢量数据时，缺少记录地理配准信息的 world file 伴随文件。</para>
	///   <para><b>处理建议</b>确认数据目录中随主文件一同提供了对应的 world file 后再读取。</para>
	/// </remarks>
	public const int Jl_ERR_NWF = 3275;

	/// <summary>缺少 generate 文件：找不到 ARC/INFO 的 generate file（错误码值 3276）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 ARC/INFO 矢量数据时，缺少存放几何坐标的 generate file。</para>
	///   <para><b>处理建议</b>确认数据集中提供了 generate file 及相应伴随文件后再读取。</para>
	/// </remarks>
	public const int Jl_ERR_NAIGF = 3276;

	/// <summary>DXF 文件意外结束：读取 DXF 时到达文件末尾而内容未完（错误码值 3278）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>解析 DXF 过程中在预期还有数据处遇到文件结束，通常文件被截断或导出未完成。</para>
	///   <para><b>处理建议</b>用完整导出的 DXF 文件；核对文件大小与导出是否中断。</para>
	/// </remarks>
	public const int Jl_ERR_DXF_UEOF = 3278;

	/// <summary>DXF 组码读取失败：无法从文件读取 DXF group code（错误码值 3279）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>解析 DXF 时应出现的 group code 无法读取，文件结构不符合 DXF 规范。</para>
	///   <para><b>处理建议</b>改用合规的 DXF 文件；若为人工拼接，检查配对(组码,值)是否残缺。</para>
	/// </remarks>
	public const int Jl_ERR_DXF_CRGC = 3279;

	/// <summary>DXF 属性数不一致：每点属性个数不统一（错误码值 3280）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 DXF 时，各点携带的属性数量彼此不一致，无法按统一列解析。</para>
	///   <para><b>处理建议</b>导出时保证所有点属性个数一致，或改用不含逐点属性的读法。</para>
	/// </remarks>
	public const int Jl_ERR_DXF_INAPP = 3280;

	/// <summary>DXF 属性与名称数不符：属性个数与名称个数不一致（错误码值 3281）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 DXF 时，点属性数量与其属性名数量对不上，无法一一对应。</para>
	///   <para><b>处理建议</b>修正导出，使属性名列表长度与每点属性数量相等。</para>
	/// </remarks>
	public const int Jl_ERR_DXF_INAPPN = 3281;

	/// <summary>DXF 全局属性与名称数不符：全局属性个数与名称个数不一致（错误码值 3282）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 DXF 的全局(整体)属性时，其数量与全局属性名数量对不上。</para>
	///   <para><b>处理建议</b>修正导出，使全局属性名列表长度与全局属性数量相等。</para>
	/// </remarks>
	public const int Jl_ERR_DXF_INAPCN = 3282;

	/// <summary>DXF 属性读取失败：无法从文件读取属性（错误码值 3283）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 DXF 逐点属性时数据缺失或格式不符，无法解析。</para>
	///   <para><b>处理建议</b>改用合规导出的 DXF；确认属性段完整后再读取。</para>
	/// </remarks>
	public const int Jl_ERR_DXF_CRAPP = 3283;

	/// <summary>DXF 全局属性读取失败：无法从文件读取全局属性（错误码值 3284）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 DXF 全局(整体)属性时数据缺失或格式不符，无法解析。</para>
	///   <para><b>处理建议</b>改用合规导出的 DXF；确认全局属性段完整后再读取。</para>
	/// </remarks>
	public const int Jl_ERR_DXF_CRAPC = 3284;

	/// <summary>DXF 属性名读取失败：无法从文件读取属性名称（错误码值 3285）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 DXF 属性名列表时数据缺失或格式不符，无法解析。</para>
	///   <para><b>处理建议</b>改用合规导出的 DXF；确认属性名段完整后再读取。</para>
	/// </remarks>
	public const int Jl_ERR_DXF_CRAN = 3285;

	/// <summary>DXF 参数名错误：generic 参数名称不合法（错误码值 3286）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 DXF 时遇到无法识别或非法的 generic(泛型)属性参数名。</para>
	///   <para><b>处理建议</b>核对导出工具写入的参数名是否受支持；必要时重新导出。</para>
	/// </remarks>
	public const int Jl_ERR_DXF_WPN = 3286;

	/// <summary>DXF 内部 I/O 错误：数据类型错误（错误码值 3289）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>DXF 读写内部遇到与预期不符的数据类型，属解析器内部一致性错误。</para>
	///   <para><b>处理建议</b>换用另一来源导出的 DXF 验证；若普遍复现，可能是文件损坏或格式版本不兼容。</para>
	/// </remarks>
	public const int Jl_ERR_DXF_IEDT = 3289;

	/// <summary>轮廓合并异常：合并时出现孤立点（错误码值 3290）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>合并轮廓时遇到无法并入任何轮廓的孤立点，破坏连续性假设。</para>
	///   <para><b>处理建议</b>先剔除孤立点或增大合并容差，使点能够连入相邻轮廓。</para>
	/// </remarks>
	public const int Jl_ERR_XLD_ISOL_POINT = 3290;

	/// <summary>NURBS 约束无法满足：给定的约束条件相互冲突（错误码值 3291）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>拟合或插值 NURBS 时，施加的约束之间无法同时成立，求解失败。</para>
	///   <para><b>处理建议</b>减少或放松冲突的约束条件，使存在可行解。</para>
	/// </remarks>
	public const int Jl_ERR_NURBS_CCBF = 3291;

	/// <summary>轮廓无分段：轮廓中不含任何可切分的线段（错误码值 3292）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>对轮廓做直线/圆弧分段时，轮廓太短或退化，切不出任何段。</para>
	///   <para><b>处理建议</b>降低分段容差或仅对足够复杂、能分出线段的轮廓执行分段。</para>
	/// </remarks>
	public const int Jl_ERR_NSEG = 3292;

	/// <summary>模板轮廓点过少：模板轮廓只剩一个点或没有点（错误码值 3293）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>形状匹配等操作要求模板轮廓至少有若干点，实际只有一个点或为空。</para>
	///   <para><b>处理建议</b>重新生成含有足够点数的模板轮廓；点数下限 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NO_ONE_P = 3293;

	/// <summary>训练样本属性超限：单个示例的特征属性数超过上限（错误码值 3301）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>向分类训练数据添加示例时，该示例携带的特征属性数量超过允许的最大值。</para>
	///   <para><b>处理建议</b>减少单个示例的特征维度后重试。上限数值 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_TMFE = 3301;

	/// <summary>训练样本过多：单个训练数据集的示例数超过上限（错误码值 3305）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>为一个训练数据集添加示例时，示例总数超过允许的最大值。</para>
	///   <para><b>处理建议</b>拆分为多个数据集或减少示例数量后重试。上限数值 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_TMSAM = 3305;

	/// <summary>类别过多：分类训练数据的类别数超过上限（错误码值 3306）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>为训练数据定义的类别数量超过分类器允许的最大值。</para>
	///   <para><b>处理建议</b>合并冗余类别后重试。类别上限数值 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_TMCLS = 3306;

	/// <summary>长方体过多：包围盒(cuboid)数量超过上限（错误码值 3307）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>在特征空间用长方体包围示例时，所需 cuboid 数量超过允许的最大值。</para>
	///   <para><b>处理建议</b>减小单个 cuboid 的覆盖粒度或减少示例分布的离散度后重试。上限数值 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_TMBOX = 3307;

	/// <summary>分类文件 ID 错误：文件中的分类器 id 无效（错误码值 3316）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取分类器文件时，其记录的 id 与当前运行时不匹配或非法。</para>
	///   <para><b>处理建议</b>确认文件与库版本配套；不要用被篡改或来源不明的分类文件。</para>
	/// </remarks>
	public const int Jl_ERR_CLASS2_ID = 3316;

	/// <summary>分类器版本不支持：文件版本超出运行时可加载范围（错误码值 3317）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>加载分类器时，文件保存所用的分类器版本不被当前库支持。</para>
	///   <para><b>处理建议</b>用兼容版本的库另存/重训分类器，或升级到支持该版本的运行时。</para>
	/// </remarks>
	public const int Jl_ERR_CLASS2_VERS = 3317;

	/// <summary>文本模型无分类器：模型内尚未配置分类器就被使用（错误码值 3319）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>使用文本模型做分类前，模型还没有被赋予分类器。</para>
	///   <para><b>处理建议</b>先为文本模型设置好分类器再调用；文本模型相关能力在本库是否保留 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_TM_NO_CL = 3319;

	/// <summary>KMeans 初始化错误：聚类中心初始化失败（错误码值 3325）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>KMeans 聚类时，无法完成聚类中心的初始分配（如样本或初始簇条件不满足）。</para>
	///   <para><b>处理建议</b>提供足够样本、合理设置簇数后重试。</para>
	/// </remarks>
	public const int Jl_ERR_ML_KMEAN_INITIALIZATION_ERROR = 3325;

	/// <summary>GMM 训练文件格式无效：高斯混合模型的训练样本文件不合法（错误码值 3330）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>为 GMM 训练读取样本文件时，文件格式不符合要求。</para>
	///   <para><b>处理建议</b>用本库导出的样本文件；核对文件未被篡改或截断。</para>
	/// </remarks>
	public const int Jl_ERR_GMM_NOTRAINFILE = 3330;

	/// <summary>GMM 训练样本版本不支持：样本文件版本超出可加载范围（错误码值 3331）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 GMM 训练样本时，其保存版本不被当前库支持。</para>
	///   <para><b>处理建议</b>用兼容版本重新生成样本文件，或升级到支持该版本的运行时。</para>
	/// </remarks>
	public const int Jl_ERR_GMM_WRTRAINVERS = 3331;

	/// <summary>GMM 样本格式错误：训练样本文件格式不正确（错误码值 3332）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>GMM 训练时读入的样本文件格式与期望结构不符。</para>
	///   <para><b>处理建议</b>使用与本库配套的样本导出格式；确认文件头/字段结构完整。</para>
	/// </remarks>
	public const int Jl_ERR_GMM_WRSMPFORMAT = 3332;

	/// <summary>GMM 分类文件格式无效：高斯混合模型分类文件不合法（错误码值 3333）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>加载 GMM 分类器文件时，其格式不符合要求。</para>
	///   <para><b>处理建议</b>用本库保存的 GMM 分类文件；核对文件完整且未被改动。</para>
	/// </remarks>
	public const int Jl_ERR_GMM_NOCLASSFILE = 3333;

	/// <summary>GMM 版本不支持：高斯混合模型分类文件版本超出可加载范围（错误码值 3334）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>加载 GMM 分类器时，文件保存版本不被当前库支持。</para>
	///   <para><b>处理建议</b>用兼容版本重存/重训 GMM，或升级到支持该版本的运行时。</para>
	/// </remarks>
	public const int Jl_ERR_GMM_WRCLASSVERS = 3334;

	/// <summary>GMM 训练未知错误：训练过程中出现未归类错误（错误码值 3335）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>训练 GMM 时发生未落入其它具体错误码的异常。</para>
	///   <para><b>处理建议</b>检查样本质量与参数设置后重试；若反复出现需保留现场供排查。</para>
	/// </remarks>
	public const int Jl_ERR_GMM_TRAIN_UNKERR = 3335;

	/// <summary>GMM 训练退化：协方差矩阵塌缩（奇异/近零方差）（错误码值 3336）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>训练 GMM 时某高斯分量的协方差矩阵塌缩，样本在该方向上几乎无散布导致不可求逆。</para>
	///   <para><b>处理建议</b>增加样本、降低分量数，或加入协方差正则化避免退化。</para>
	/// </remarks>
	public const int Jl_ERR_GMM_TRAIN_COLLAPSED = 3336;

	/// <summary>GMM 训练无样本：至少一个类别没有任何样本（错误码值 3337）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>训练 GMM 时，某个声明的类别完全没有对应样本，无法估计其分布。</para>
	///   <para><b>处理建议</b>为每个类别补齐至少一条样本，或移除空类别。</para>
	/// </remarks>
	public const int Jl_ERR_GMM_TRAIN_NOSAMPLE = 3337;

	/// <summary>GMM 训练样本过少：至少一个类别样本数不足以训练（错误码值 3338）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>某类别样本数量偏少，低于稳定估计协方差所需的下限；与 3337（完全没有样本）程度不同。</para>
	///   <para><b>处理建议</b>增加该类别样本量或降低特征维度。样本下限数值 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_GMM_TRAIN_FEWSAMPLES = 3338;

	/// <summary>GMM 未训练：在训练前就使用 GMM（错误码值 3340）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>对尚未执行训练流程的 GMM 做分类/查询。</para>
	///   <para><b>处理建议</b>先完成训练，再使用训练结果做分类。</para>
	/// </remarks>
	public const int Jl_ERR_GMM_NOTTRAINED = 3340;

	/// <summary>GMM 无训练数据：GMM 内尚未添加训练样本（错误码值 3341）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>对没有加载任何训练数据的 GMM 执行训练或使用。</para>
	///   <para><b>处理建议</b>先向 GMM 添加训练数据，再进行训练或使用。</para>
	/// </remarks>
	public const int Jl_ERR_GMM_NOTRAINDATA = 3341;

	/// <summary>序列化项不含 GMM：反序列化的对象并非合法 GMM（错误码值 3342）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>从序列化数据还原 GMM 时，该序列化项里并不包含有效的 GMM 内容。</para>
	///   <para><b>处理建议</b>确认序列化项确由 GMM 保存而来；核对文件未被损坏或错配类型。</para>
	/// </remarks>
	public const int Jl_ERR_GMM_NOSITEM = 3342;

	/// <summary>MLP 输出函数未知：指定了无法识别的输出函数（错误码值 3350）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>为多层感知机(MLP)设置了名称或类型不在支持列表内的输出函数。</para>
	///   <para><b>处理建议</b>改用文档列出的合法输出函数取值后再训练/使用。</para>
	/// </remarks>
	public const int Jl_ERR_MLP_UNKOUTFUNC = 3350;

	/// <summary>MLP 目标编码错误：目标值不在 0-1 编码范围（错误码值 3351）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>训练 MLP 时，类别目标标签未按 0/1 编码给出，与网络输出约定不符。</para>
	///   <para><b>处理建议</b>把训练样本的目标转换为 0-1 编码后再训练。</para>
	/// </remarks>
	public const int Jl_ERR_MLP_NOT01ENC = 3351;

	/// <summary>MLP 无训练样本：分类器里没有存训练样本（错误码值 3352）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>对未存入任何训练样本的 MLP 分类器执行训练或使用。</para>
	///   <para><b>处理建议</b>先向分类器添加训练样本，再训练或使用。</para>
	/// </remarks>
	public const int Jl_ERR_MLP_NOTRAINDATA = 3352;

	/// <summary>MLP 训练文件格式无效：多层感知机的训练样本文件不合法（错误码值 3353）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>为 MLP 训练读取样本文件时，文件格式不符合要求。</para>
	///   <para><b>处理建议</b>用本库导出的样本文件；核对文件未被篡改或截断。</para>
	/// </remarks>
	public const int Jl_ERR_MLP_NOTRAINFILE = 3353;

	/// <summary>MLP 训练样本版本不支持：样本文件版本超出可加载范围（错误码值 3354）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 MLP 训练样本时，其保存版本不被当前库支持。</para>
	///   <para><b>处理建议</b>用兼容版本重新生成样本文件，或升级到支持该版本的运行时。</para>
	/// </remarks>
	public const int Jl_ERR_MLP_WRTRAINVERS = 3354;

	/// <summary>MLP 样本格式错误：训练样本格式不正确（错误码值 3355）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>MLP 训练读入的样本格式与期望结构不符。</para>
	///   <para><b>处理建议</b>使用与本库配套的样本导出格式；确认文件头/字段结构完整。</para>
	/// </remarks>
	public const int Jl_ERR_MLP_WRSMPFORMAT = 3355;

	/// <summary>MLP 非分类器：把回归用的 MLP 当作分类器使用（错误码值 3356）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>对以回归(非分类)方式配置的 MLP 执行分类相关操作。</para>
	///   <para><b>处理建议</b>确认 MLP 已按分类用途配置；分类操作前设置正确的网络类型。</para>
	/// </remarks>
	public const int Jl_ERR_MLP_NOCLASSIF = 3356;

	/// <summary>MLP 分类器文件格式非法：读入的文件结构不符合 MLP 分类器格式（错误码值 3357）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取或反序列化 MLP 分类器文件时，文件头部或内容结构与 MLP 分类器格式不匹配。</para>
	///   <para><b>关联码</b>3356(Jl_ERR_MLP_NOCLASSIF)指对象类型不是分类器；本码指文件格式本身不合规格，关注点不同。</para>
	///   <para><b>处理建议</b>确认文件由本库完整写出、未经文本模式转换或截断；必要时重新训练并导出分类器。</para>
	/// </remarks>
	public const int Jl_ERR_MLP_NOCLASSFILE = 3357;

	/// <summary>MLP 版本不受支持：读入的 MLP 分类器版本号超出当前库能处理的范围（错误码值 3358）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 MLP 分类器文件时，文件记录的 MLP 数据版本与当前运行时支持的版本不符。</para>
	///   <para><b>关联码</b>3357 是格式不合法，3358 是格式可识别但版本号不被支持。</para>
	///   <para><b>处理建议</b>用与训练端同版本的本库读写分类器；旧分类器需在新版本库中重新训练后导出。</para>
	/// </remarks>
	public const int Jl_ERR_MLP_WRCLASSVERS = 3358;

	/// <summary>通道数错误：输入图像的通道数与算子要求不符（错误码值 3359）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>通用校验码：要求特定通道数(如单通道灰度图或三通道彩色图)的运算收到通道数不符的图像。分类器场景下常见于特征通道数与网络输入维数不一致。</para>
	///   <para><b>关联码</b>分类器专用校验见 3370(LUT 通道数与表维数不匹配)；本码不限于某一算子族。</para>
	///   <para><b>处理建议</b>先核对图像通道数，必要时抽取或合成所需通道后再调用。</para>
	/// </remarks>
	public const int Jl_ERR_WRNUMCHAN = 3359;

	/// <summary>MLP 参数个数错误：给 MLP 设置的参数数量与当前网络结构要求不符（错误码值 3360）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>配置或训练 MLP 时传入的参数元组长度不对——参数个数随网络结构(层数、每层单元数等)确定，结构变了旧参数集就不匹配。</para>
	///   <para><b>处理建议</b>重新读取 MLP 当前结构要求的参数个数并按序补齐；不要跨结构复用参数表。</para>
	/// </remarks>
	public const int Jl_ERR_MLP_WRNUMPARAM = 3360;

	/// <summary>序列化项中不含有效 MLP：读到的序列化数据里没有可用的多层感知机结构（错误码值 3361）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>从序列化项恢复 MLP 时，该项虽能按格式解析，但内部缺少或不含有效的 MLP 数据。</para>
	///   <para><b>关联码</b>与 3357 的区别：3357 是文件格式整体不合法，3361 是序列化项里找不到有效 MLP 载荷；该区别的具体触发细节 [待实测]。</para>
	///   <para><b>处理建议</b>确认写出时序列化的确是 MLP 对象；检查文件是否在序列化过程中被并发写坏。</para>
	/// </remarks>
	public const int Jl_ERR_MLP_NOSITEM = 3361;

	/// <summary>LUT 维数与通道数不匹配：图像的通道数与查找表的维数对不上（错误码值 3370）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>用彩色查找表(LUT)处理图像时，表是为某个通道数建的，而实际图像通道数与之不等。3 通道表配 3 通道图，2 通道表配 2 通道图。</para>
	///   <para><b>关联码</b>通道数根本不在允许集合(2 或 3)时报 3371；本码是通道数合法但与表维数不一致。</para>
	///   <para><b>处理建议</b>按图像实际通道数重建查找表，或把图像调整为表所适配的通道数。</para>
	/// </remarks>
	public const int Jl_ERR_LUT_WRNUMCHAN = 3370;

	/// <summary>查找表只支持 2 或 3 通道：试图为其它通道数建彩色 LUT（错误码值 3371）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>创建彩色查找表时请求的通道数不是 2 也不是 3。单通道的灰度映射不走彩色 LUT 这条路。</para>
	///   <para><b>处理建议</b>把目标通道数改到 2 或 3；灰度增强请改用逐通道映射而不是彩色 LUT。</para>
	/// </remarks>
	public const int Jl_ERR_LUT_NRCHANLARGE = 3371;

	/// <summary>无法创建查找表：需增大 bit_depth 或改用 class_selection 的 fast 档（错误码值 3372）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>创建彩色 LUT 时，当前位深与类别选择方式的组合导致表无法构造——位深太低放不下所需精度，或类别选择方式开销过大。</para>
	///   <para><b>处理建议</b>提高 bit_depth 取值，或把 class_selection 设为 fast；两者仍不行再降低对分类精度的要求。</para>
	/// </remarks>
	public const int Jl_ERR_LUT_CANNOTCREAT = 3372;

	/// <summary>SVM 中没有训练样本：分类器内尚未存入任何样本（错误码值 3380）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>对训练样本为空的 SVM 执行训练、追加样本或依赖样本的查询操作。</para>
	///   <para><b>关联码</b>3380 是"样本没存进去"，3393(SVM 未训练)是"样本有但没训练"——先补样本再谈训练。</para>
	///   <para><b>处理建议</b>先向分类器添加特征与类别配对齐全的样本，确认添加成功后再训练。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_NOTRAINDATA = 3380;

	/// <summary>SVM 训练样本文件格式非法：读入的训练样本文件结构不符合规格（错误码值 3381）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 SVM 训练样本文件时格式校验失败。注意本码针对的是训练样本文件，不是 SVM 分类器本体文件(那是 3384)。</para>
	///   <para><b>处理建议</b>确认文件由本库完整写出、未损坏；样本丢失时用带标注的特征数据重新收集。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_NOTRAINFILE = 3381;

	/// <summary>SVM 训练样本版本不受支持：样本文件的版本号超出当前库支持范围（错误码值 3382）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 SVM 训练样本文件时版本不匹配。与 3385(SVM 分类器版本不支持)分别对应样本与分类器两类载体。</para>
	///   <para><b>处理建议</b>用同版本的本库读写样本文件；旧样本在新版本下重新训练生成。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_WRTRAINVERS = 3382;

	/// <summary>训练样本格式错误：样本数据的组织形式不符合训练接口的要求（错误码值 3383）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>提交给 SVM 的训练样本在排列结构上不合要求，例如特征与样本的行列取向放反、或样本与类别元组长度不成对 [待实测：具体布局约定需对照训练接口文档验证]。</para>
	///   <para><b>关联码</b>3381 是文件级格式不合法，本码是内存中样本数据本身的组织不合法。</para>
	///   <para><b>处理建议</b>核对训练接口对特征矩阵方向(每行一样本还是每列一样本)和类别向量长度的规定后重排数据。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_WRSMPFORMAT = 3383;

	/// <summary>SVM 分类器文件格式非法：读入的文件结构不符合 SVM 分类器格式（错误码值 3384）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取或反序列化 SVM 分类器文件时格式校验失败——文件头/内部结构与分类器格式对不上。本码只管分类器本体，样本文件另有 3381(<see cref="F:JLVisionLib.JlErrorDef.Jl_ERR_SVM_NOTRAINFILE"/>)，两者混用会把排查方向带偏。</para>
	///   <para><b>关联码</b>3381 训练样本文件格式、3385(<see cref="F:JLVisionLib.JlErrorDef.Jl_ERR_SVM_WRCLASSVERS"/>) 分类器版本不支持、3395(<see cref="F:JLVisionLib.JlErrorDef.Jl_ERR_SVM_NOSITEM"/>) 序列化项里没有有效 SVM；MLP 一侧的对应码是 3357(<c>Jl_ERR_MLP_NOCLASSFILE</c>)，同一类故障在两种分类器上编号不同。</para>
	///   <para><b>托管层现状</b>本运行时未提供任何 SVM 分类器算子包装，也没有分类器句柄类型（核对 <c>CreateSvm</c>/<c>TrainSvm</c>/<c>ClassifySvm</c>/<c>ReadSvm</c> 均无命中），故本码不会从本库的类型化入口抛出，只会来自原生核内部；原生侧究竟哪些算子会回本码 [待实测]。</para>
	///   <para><b>处理建议</b>确认文件由本库同版本完整写出、未经文本模式换行转换或传输截断；无法修复时只能重新训练并导出分类器。归类上 ≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），统一检查 <c>JlOperatorException.throwOperator</c> 会据它抛 <c>JlOperatorException</c>，不可忽略。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_NOCLASSFILE = 3384;

	/// <summary>SVM 版本不受支持：读入的 SVM 分类器版本号超出当前运行时能处理的范围（错误码值 3385）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>读取 SVM 分类器文件时，文件记录的分类器数据版本与当前原生核支持的版本不符。与 3384 的分界：3384 是结构根本读不出（格式不合法），本码是结构可识别但版本被拒。</para>
	///   <para><b>关联码</b>同族分工为 3384 分类器格式、3381 样本文件格式、3382(<c>Jl_ERR_SVM_WRTRAINVERS</c>) 样本文件版本——样本与分类器两类载体各有一对"格式/版本"码；MLP 侧的对应码是 3358(<c>Jl_ERR_MLP_WRCLASSVERS</c>)。</para>
	///   <para><b>托管层现状</b>本库无 SVM 分类器算子包装与分类器类型，训练端与部署端须用同版本运行时；版本兼容矩阵由原生核决定，具体从哪个版本起不再可读 [待实测]。</para>
	///   <para><b>处理建议</b>让读写两端用同一版本的本库；旧分类器在新版本下只能重新训练导出，不要指望就地转换。≥1000 属真错误，统一检查会抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_WRCLASSVERS = 3385;

	/// <summary>SVM 类别数错误：类别个数与分类器声明或训练数据的要求不符（错误码值 3386）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>创建或训练 SVM 时给出的类别个数与样本里实际出现的类别标签对不上——例如按 N 类建的分类器只喂到 N-1 类的标签，或改了类别数却沿用旧的样本集。本码只看"个数"这一维。</para>
	///   <para><b>关联码</b>个数够但某些类一条样本都没有时报 3392(<c>Jl_ERR_SVM_NO_TRAIND_FOR_CLASS</c>)；样本元组的组织形式不合法报 3383(<c>Jl_ERR_SVM_WRSMPFORMAT</c>)；一条样本都没有报 3380(<c>Jl_ERR_SVM_NOTRAINDATA</c>)。三者排查方向不同，别都当"类别数写错"处理。</para>
	///   <para><b>处理建议</b>先对类别标签做去重统计，再与分类器声明的类别数逐一核对；类别集变了必须连样本一起重建，不要只改数字。同族码在托管层均无类型化入口（本库无 SVM 分类器包装），只可能来自原生核或经序列化通道读入的对象，故日志里的 SVM 码要结合调用的原生算子名定位。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_WRNRCLASS = 3386;

	/// <summary>SVM 参数 nu 过大：nu 型 SVM 的取值超出允许上界（错误码值 3387）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>创建或配置以 <c>nu</c> 为参数的 SVM 变体（nu 支持向量分类器、单类 nu 支持向量分类器）时给出过大的 <c>nu</c>。<c>nu</c> 是比例型参数，因此它同时受"不超过 1"和"不超过可用样本所能支撑的比例"两条约束；本码在参数校验阶段就拒，不会等到训练求解。</para>
	///   <para><b>只属于 nu 变体</b>用普通 C-SVM（以惩罚系数权衡误分）时根本没有 <c>nu</c> 这个槽位，报出本码说明分类器类型选错或参数表是从 nu 变体抄来的。</para>
	///   <para><b>关联码</b><c>nu</c> 落在合法区间内但训练仍不收敛时报 3388(<c>Jl_ERR_SVM_TRAIN_FAIL</c>)——3387 是"参数被直接拒"，3388 是"参数收下但解不出来"。</para>
	///   <para><b>处理建议</b>把 <c>nu</c> 下调（先试远小于 1 的值），并同步核对样本量：样本少时合法上界会更低 [待实测：合法上界与样本数/类别数的确切关系需对照原生参数说明验证]。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_NU_TOO_BIG = 3387;

	/// <summary>SVM 训练失败：参数与样本都被接受，但求解过程没有得到可用的分类器（错误码值 3388）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>训练阶段的求解失败：数据齐、参数在合法区间内，但优化过程不收敛或找不到可行分离面。典型诱因是特征未做归一化导致量纲跨度大、样本高度重复或近共线、类别严重不平衡、惩罚系数与核参数配合过紧。</para>
	///   <para><b>与其它"SVM 没训成"码的分界</b>3380(<c>Jl_ERR_SVM_NOTRAINDATA</c>) 是压根没有样本，3392(<c>Jl_ERR_SVM_NO_TRAIND_FOR_CLASS</c>) 是某些类别没有样本，3387(<c>Jl_ERR_SVM_NU_TOO_BIG</c>) 是参数在入口就被拒——这三种都轮不到本码。看到 3388 说明数据与参数已通过前置校验，问题在数值层面，改数据分布比改码更有效。</para>
	///   <para><b>处理建议</b>先对特征做缩放/去共线，再放宽核与惩罚参数，并检查是否有完全重复的样本；同一份数据换核类型或换参数各试一次，可判断是数据不可分还是参数问题。本库托管层未做特征缩放工具链封装，缩放需调用方自备 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_TRAIN_FAIL = 3388;

	/// <summary>SVM 彼此不兼容：两个分类器的核或参数配置不一致，无法合并或增量追加（错误码值 3389）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>把多个 SVM 合到一起（合并分类器、在已有分类器上追加训练）时，两边的可合并前提不成立。可合并要求至少涵盖核类型与特征维数，核参数与类别集是否也参与校验 [待实测]。</para>
	///   <para><b>关联码</b>3391(<c>Jl_ERR_SVM_KERNELNOTRBF</c>) 是本码的一个特例——当被合并的一侧核根本不是 RBF 时会先撞上更专门的 3391；3390(<c>Jl_ERR_SVM_NO_TRAIN_ADD</c>) 则是"能合但没起点"（缺少可用于追加的支持向量）。</para>
	///   <para><b>处理建议</b>不要试图拼接不同参数训练出来的分类器：把两边的样本并到一起，用同一套参数重训一个分类器。合并前逐项比对核与维数，比事后猜为什么报错省事。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_DO_NOT_FIT = 3389;

	/// <summary>SVM 中没有可用于追加训练的支持向量：增量更新缺少可续算的起点（错误码值 3390）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>在已有 SVM 上做增量追加（把新样本并入已训练的分类器）时，该分类器里没有支持向量可当续算起点。常见于对刚创建、从未训练过的分类器直接追加，或前一次训练虽然调用成功但一个样本都没落成支持向量。</para>
	///   <para><b>与相邻码的分界</b>3393(<c>Jl_ERR_SVM_NOT_TRAINED</c>) 指"整个分类器没训练过"，任何依赖训练结果的操作都会报它；本码专指追加类操作在"没有可用 SV"这一步被拒，语义更窄，排查时优先查上一次训练是否真的产出了支持向量。3389(<c>Jl_ERR_SVM_DO_NOT_FIT</c>) 则是配置不兼容，与此无关。</para>
	///   <para><b>处理建议</b>退回到全量重训：把新旧样本合在一起重新训练一个分类器，不要指望在空结果上续算；若确实是上一次训练悄悄产出了 0 个支持向量，先降惩罚参数/放宽核参数重训再评估。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_NO_TRAIN_ADD = 3390;

	/// <summary>SVM 核不是 RBF：所请求的操作只在 RBF 核下可用（错误码值 3391）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>对核类型为线性、多项式等非 RBF 的 SVM 请求那些只在 RBF 核下实现的操作（追加/合并一类）。核类型是创建分类器时就定死的一项，训练完成后改不了，因此本码本质上是"选错了分类器构造方式"。</para>
	///   <para><b>关联码</b>它是 3389(<c>Jl_ERR_SVM_DO_NOT_FIT</c>) 的特例：两边配置不一致报 3389，而"其中一边根本不是 RBF"这一情形有更专门的 3391，先看 3391 再回查 3389 能少走弯路。</para>
	///   <para><b>代价提示</b>改成 RBF 核不是无痛的：RBF 的核参数（宽度一类）会实质影响精度与训练耗时，换核后原有参数取值不可直接沿用，需要重新调参并重训 [待实测：本库原生实现具体接受哪些核参数名与默认值]。</para>
	///   <para><b>处理建议</b>若确实需要追加/合并，用 RBF 核重新创建分类器并在全量样本上重训；若只需一次分类且当前核表现更好，则放弃增量更新这条路，改为定期整体重训。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_KERNELNOTRBF = 3391;

	/// <summary>训练数据未覆盖全部类别：声明的类别里有类别一条样本都没有（错误码值 3392）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>训练 SVM 前的一致性检查发现：分类器声明的类别集合中，某些类别在提交的样本里完全没出现。典型成因是标注漏类、按条件筛选特征时把某一类整体筛空、或训练集与验证集切分时把小类全切走了。</para>
	///   <para><b>与 3386 的分界</b>3386(<c>Jl_ERR_SVM_WRNRCLASS</c>) 是类别"个数"对不上；本码个数可以是对的，但分布不均到某类为零。多分类器成对训练时（每一对类别都要有跨类样本）尤其容易被本码拦下 [待实测：是否逐类对做覆盖检查]。</para>
	///   <para><b>处理建议</b>按类别统计样本条数（去重后的标签直方图），零样本的类别要么补数据、要么从类别集里去掉；把类别数改小来绕过检查会让模型上线后根本无法输出那个类，属于埋雷。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_NO_TRAIND_FOR_CLASS = 3392;

	/// <summary>SVM 未训练：分类器尚未训练过就被用于分类或读取训练结果（错误码值 3393）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>对未训练的 SVM 执行分类、取支持向量/参数、或做依赖训练结果的操作。它与 3380(<c>Jl_ERR_SVM_NOTRAINDATA</c>) 是训练链条上的前后两道坎：3380 是"样本没存进去"，本码是"样本有了但没训练"——先补样本再谈训练。</para>
	///   <para><b>与通用码的关系</b>不区分分类器类型的同类检查用 3394(<c>Jl_ERR_NOT_TRAINED</c>)；SVM 侧优先报本码，MLP 侧另有 3356(<c>Jl_ERR_MLP_NOCLASSIF</c>) 等专用码。日志里两者都可能出现，别按一个查。</para>
	///   <para><b>读档陷阱</b>从文件读回的分类器是否自动视为已训练，取决于文件里是否携带训练结果；只存了结构（样本、参数）而没存解的分类器读进来仍会撞本码 [待实测：原生读入时是否会顺带恢复训练状态]。</para>
	///   <para><b>处理建议</b>在初始化路径上明确"创建→加样本→训练→使用"的次序，训练调用的返回码要单独检查：训练失败（见 3388）后继续用同一对象分类，得到的就是本码而不是上一步的 3388。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_NOT_TRAINED = 3393;

	/// <summary>分类器未训练：不区分分类器类型的通用"未训练"检查码（错误码值 3394）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>在原生侧走的是通用分类器公共检查（"这个分类器有没有解"）时报本码；SVM 专属的同类检查是 3393(<c>Jl_ERR_SVM_NOT_TRAINED</c>)，MLP 另有自己的族段（3355~3361）。哪个分类器类型走通用检查而非专属检查 [待实测]。</para>
	///   <para><b>为什么值得单列</b>本码不带类型信息，光看码无法判断是 SVM、MLP 还是其它分类器；定位时要靠异常消息里的算子名（<c>JlOperatorException.GetErrorMessage</c> 实时查原生消息表）回推调用点，消息文本比码本身更有用。</para>
	///   <para><b>处理建议</b>补上训练这一步并检查训练调用的返回码；若训练确实返回成功却仍报本码，怀疑"训练写进了另一个对象实例"——多数分类器是就地改写还是返回新句柄并不一致，接链时别拿旧句柄继续用。</para>
	/// </remarks>
	public const int Jl_ERR_NOT_TRAINED = 3394;

	/// <summary>SVM 序列化项类型不符：从序列化项还原的对象不是有效的 SVM（错误码值 3395）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>用 SVM 专属的还原入口读序列化缓冲/文件时，项里装的不是 SVM——多半是别的类型（MLP、GMM 甚至区域/图像）存进来后被投喂错了接口，或 SVM 的保存流被截断。</para>
	///   <para><b>与相邻码分界</b>同一条链上三段故障各有码：3393(<c>Jl_ERR_SVM_NOT_TRAINED</c>) 是"对象对但没训练"，3385 是"是 SVM 但分类器版本不支持"，本码最靠前——连类型标记都没对上。NOSITEM 类码一族一枚（MLP 3361、GMM 3342、本码 3395），排障先按数值定位族。</para>
	///   <para><b>处理建议</b>核对"保存"与"还原"两处用的是不是同一类对象；文件来自外部或经网络/裁剪传输时优先怀疑完整性。</para>
	/// </remarks>
	public const int Jl_ERR_SVM_NOSITEM = 3395;

	/// <summary>形态学：旋转序号非法：结构元素离散旋转的序号超出该族支持的取值（错误码值 3401）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>形态学族段（3401~3412）的首枚。需要按离散旋转生成结构元素副本的调用收到族外序号时由原生侧报出；形态学族算子靠参数表驱动，据异常消息里的算子名即可定位到是哪个元素的旋转序给错 [待实测：对应到本库的具体包装形参]。</para>
	///   <para><b>与族内码分界</b>3402(<c>Jl_ERR_GOL</c>) 是 Golay 码串里出现非法字母，3412(<c>Jl_ERR_WRNSE</c>) 是预定义结构元素名字拼错，本码是"序号数值越界"，三者出错层面不同。</para>
	///   <para><b>处理建议</b>结构元素的旋转是离散编号而非任意角度：核对元素族支持的序号集合，越界值应在上游修正，不要取模或钳位硬凑——钳位后拿到的是另一个方向的元素。</para>
	/// </remarks>
	public const int Jl_ERR_ROTNR = 3401;

	/// <summary>形态学：Golay 字母非法：Golay 码串里出现不属于字母表的字符（错误码值 3402）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>用 Golay 字母表指定结构元素（细化/粗化一族以此驱动"击中即删"，如 <c>JlRegion</c> 的 Golay 细化族把 golayElement 以字符串透传给原生）时，串中含字母表外的字符。高发于动态拼接参数：大小写写反、把逗号空格这类分隔符也塞进了串里。</para>
	///   <para><b>与族内码分界</b>3412(<c>Jl_ERR_WRNSE</c>) 管"预定义结构元素的名字拼错"，本码管"Golay 字母本身拼错"；同为字符串错误，但校验的是两套合法集合。</para>
	///   <para><b>处理建议</b>先打印字符串原值逐项对照；单个非法字母会让整条序列作废，别指望原生"跳过坏字符继续跑"。简单元素改用预定义名或手工构造区域，不必手写 Golay 串。</para>
	/// </remarks>
	public const int Jl_ERR_GOL = 3402;

	/// <summary>形态学：参考点非法：结构元素的锚定点（参考点）与元素定义冲突（错误码值 3403）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>形态学族段。膨胀/腐蚀/击中等操作的几何都以结构元素的参考点为原点摆放；当显式给出的参考点落在元素像素集合之外或与元素定义冲突时报本码 [待实测：原生侧的具体校验条件]。</para>
	///   <para><b>为什么危险</b>即便不报错，参考点错一格也会让每次膨胀整体平移一格——形态学结果"看着对但系统性错位"，比本码直接拦下更难查；报本码时直接修正参考点，不要靠平移整个元素硬凑。</para>
	///   <para><b>处理建议</b>用中心对称元素配默认参考点；自造元素时保证参考点坐标与像素序数组同源（同一坐标系、同一起点约定）。</para>
	/// </remarks>
	public const int Jl_ERR_BEZ = 3403;

	/// <summary>迭代轮数非法：迭代型形态学/细化算子收到越界的轮数参数（错误码值 3404）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>"同一操作重复若干轮"的算子（膨胀/腐蚀按元素重复、Golay 序列细化按元素逐轮推进，轮数可整数、也可按元组逐对象给不同轮数）收到负数或超出实现上限的轮数。</para>
	///   <para><b>边界行为</b>0 与 1 的语义各算子不统一：有的把 0 当作"不迭代"返回输入，有的直接报本码，调用前对边界值做断言，别依赖默认表现 [待实测]。</para>
	///   <para><b>处理建议</b>大轮数迭代是性能陷阱：重复用 n 次小元素与一次用放大 n 倍的大元素结果通常不等价（形状保真度、耗时都不同），按目标形状选元素、把轮数压到个位数往往是正解。</para>
	/// </remarks>
	public const int Jl_ERR_ITER = 3404;

	/// <summary>形态学：内部系统错误：参数校验全过之后形态学模块自己失稳（错误码值 3405）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>与 3401~3412 里其他码的本质区别：那些码是"输入不合法被拦下"，本码是输入通过了校验、原生形态学模块内部走到不该到达的分支。常见诱因是内存耗尽、句柄已被释放又被原生侧复用（托管对象 Dispose 过早）、多线程共享同一对象乱序调用 [待实测]。</para>
	///   <para><b>排查顺序</b>先查对象生命周期（输入区域/结构元素在调用期间是否仍存活），再查并发访问，最后才怀疑库本身。</para>
	///   <para><b>处理建议</b>把它当故障码而不是参数码对待：稳定复现时保留调用链与输入内容反馈库维护方；按 3407~3410 的思路去修参数数量通常是白费功夫。</para>
	/// </remarks>
	public const int Jl_ERR_MOSYS = 3405;

	/// <summary>边界类型非法：算子收到的"边界类型"字面量不在支持列表内（错误码值 3406）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>取区域一圈边界像素的算子（如 <c>JlRegion.Boundary(string)</c>，原生 id 715）按字符串区分边界语义，已知合法值为 "inner"/"outer"；字符串在托管层原样透传、不做校验，拼错后被原生拦下、报出的即本码 [待实测]。其他边界类型字面量是否存在 [待实测]。</para>
	///   <para><b>典型成因</b>大小写混用（"Inner"）、把中文全角引号带进字面量、或从配置读来的字符串带了空格。这类错误不会在编译期暴露。</para>
	///   <para><b>处理建议</b>在代码里把边界类型定义成常量映射到合法字面量；inner 与 outer 结果面积不同（外边界比区域外缘大一圈），换类型后下游按面积/周长的筛选阈值要一并复核。</para>
	/// </remarks>
	public const int Jl_ERR_ART = 3406;

	/// <summary>形态学：输入对象个数不符：进入形态学算子的对象数与该算子签名要求不一致（错误码值 3407）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>形态学框架按算子签名分别清点四类槽位：输入对象、输出对象、输入控制参数、输出控制参数（本码与 3408/3409/3410 一一对应）。输入侧对象（区域/结构元素）打包成元组时数量凑错——比如要"区域+元素"两路却只给了区域——报本码。</para>
	///   <para><b>何时会遇到</b>走 C# 强类型包装时几乎撞不到（实参个数编译期已定）；多出现在把多区域、多元素展平进同一个元组再整体传入的场合 [待实测]。</para>
	///   <para><b>处理建议</b>用异常消息定位是哪个形态学算子，再对照其定义核对输入路数；多个结构元素逐个作用请用循环逐次调用，而不是指望一次调用吞下整个列表。</para>
	/// </remarks>
	public const int Jl_ERR_OBJI = 3407;

	/// <summary>形态学：输出对象个数不符：形态学算子实际产出的对象数与调用方预留的输出口对不上（错误码值 3408）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>形态学框架对输出侧同样清点：调用约定要接管的输出对象槽位数与算子实际产出数不等时报本码。与 3407(<c>Jl_ERR_OBJI</c>) 成对，一个查进、一个查出；控制参数的对应码是 3409/3410。</para>
	///   <para><b>何时会遇到</b>强类型包装的 out 参数个数编译期已定，正常路径撞不到；多见于原生句柄直接交互或包装层与原生算子版本不匹配时——此时输出口约定漂移，属于部署层面（托管层与原生不同套）的问题而不是用户参数问题。</para>
	///   <para><b>处理建议</b>先确认托管层与原生的构建版本成套；再检查是否用了带裸句柄的静态入口，换回常规重载验证。</para>
	/// </remarks>
	public const int Jl_ERR_OBJO = 3408;

	/// <summary>形态学：输入控制参数个数不符：随算子附带的标量/字符串参数数量与期望不等（错误码值 3409）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>控制参数指对象以外的附带参数（边界类型串、迭代轮数、Golay 元素串这类形态学族参数）。框架清点实际传入的个数，与算子期望不符即报本码；输出侧对应 3410(<c>Jl_ERR_PARO</c>)，对象侧对应 3407/3408。</para>
	///   <para><b>典型成因</b>与 3407 同源：绕开强类型包装自行组织参数序，或按错误的路数把"每对象一参数"传成了"每操作一参数"。元组形参与标量形参混用时最容易差数。</para>
	///   <para><b>处理建议</b>对照异常消息指名的算子核对参数表；若参数是按区域元组批量给的，检查元素个数是否与区域个数一致（多区域批处理最常见的错位点）。</para>
	/// </remarks>
	public const int Jl_ERR_PARI = 3409;

	/// <summary>形态学：输出控制参数个数不符：算子应产出的标量参数个数与接管约定不符（错误码值 3410）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>四个"个数清点"码的最后一枚：3407/3408 管输入/输出对象，3409 管输入控制参数，本码管输出控制参数——算子要吐出的标量（统计值、轮数回执类输出 [待实测：具体算子]）与调用侧预留的 out 口数不一致。</para>
	///   <para><b>解读</b>输出侧的数量在强类型包装里编译期已定，用户几乎无法"给错"；撞上本码优先怀疑托管层与原生库版本不配套（输出口约定随版本漂移），其次怀疑包装层被改动。</para>
	///   <para><b>处理建议</b>核对库与原生运行时的版本配对；不要靠改调用代码去"凑"输出个数。</para>
	/// </remarks>
	public const int Jl_ERR_PARO = 3410;

	/// <summary>形态学：结构元素无界：作为结构元素参与运算的区域是无限域（错误码值 3411）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>形态学以结构元素做邻域扫描，元素必须是有限像素集合；传入补集型区域（原生以"全平面减去若干像素"表示、逻辑上无界，如 <c>JlRegion.Complement()</c> 的产物）当元素时报本码。</para>
	///   <para><b>与 3514 分界</b>3514(<c>Jl_ERR_OPNOCOMPL</c>) 说"这个算子不支持补集输入区域"——限制在算子；本码说"元素必须有限"——任何形态学算子都绕不过，两者触发的对象不同（一个是被处理区域，一个是元素）。</para>
	///   <para><b>处理建议</b>先把补集与图像域或感兴趣矩形求交，折算成有界区域再作元素；想要"超大元素"的效果就显式构造足够大的有限元素。</para>
	/// </remarks>
	public const int Jl_ERR_SELC = 3411;

	/// <summary>形态学：结构元素名字未知：按名字取预定义结构元素时名字不在支持列表内（错误码值 3412）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>以字符串名请求内置形状元素（矩形/圆/八角形一族带编号变体）时拼错：大小写不符、漏掉区分变体的数字后缀、首尾带空白。字符串透传的接口在托管层不校验，错到原生才报本码。</para>
	///   <para><b>与族内码分界</b>3402(<c>Jl_ERR_GOL</c>) 管 Golay 字母非法，本码管"形状名"非法；名字对但旋转序非法则走 3401(<c>Jl_ERR_ROTNR</c>)。</para>
	///   <para><b>处理建议</b>把可用名字收敛成代码内常量表，别让名字从配置字符串直接进调用；拿不准形状时改为手工构造区域元素，行为完全可控。</para>
	/// </remarks>
	public const int Jl_ERR_WRNSE = 3412;

	/// <summary>游程编码：行程行数（弦数）为负：区域数据头的行程行数小于 0（错误码值 3500）。</summary>
	/// <remarks>
	///   <para><b>区域怎么存</b>区域原生按"行程行（run length row/chord）"编码：每行像素记录成若干(列起点,长度)段。3500~3513 是这套存储的一致性校验族，分工：3500 行程行数&lt;0；3501 弦数超当前系统上限；3502 单条行程长度为负；3503/3504 行程行号超出图像高度上界/小于 0；3505/3506 行程列越宽界/负值（3506 原文为德文，同 3505 的下界情形）；3507~3509 弦型表示下行/列计数越界；3510 自动扩容触顶；3511 补集标志既非真也非假；3512 声明容量小于已用数；3513 弦数超过自身上限。</para>
	///   <para><b>触发时机</b>本码是族首检：拿到区域数据先验行程行数符号。正常算子链自产的区域不会触发，多见于反序列化损坏、跨进程/外部拼装区域数据后长度字段被写坏。</para>
	///   <para><b>处理建议</b>沿数据来源链回溯：先换一份内存中直接构造的区域复测，能复现则查算子调用，不能复现则查读取/传输环节。</para>
	/// </remarks>
	public const int Jl_ERR_WRRLN1 = 3500;

	/// <summary>游程编码：弦数超当前系统上限：区域弦数超过可配置上限，原生提示用 set_system 调大（错误码值 3501）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>区域增长过程中弦（行程段）数即将超过系统当前设的容量（原文点名 <c>current_runlength_number</c> 这一可调上限）。典型来源：连通域极多或边缘极碎的区域（未清理的分割结果）、反复对同一区域做大量并集/拼接。</para>
	///   <para><b>本库的处境</b>全仓检索未见对 set_system 的 C# 包装，该上限在本库没有调节入口；可行出路在数据侧：先 <c>Connection</c> 拆分再按需筛选、做形状简化后再继续运算，把单个区域的弦数压下来。</para>
	///   <para><b>与 3510 分界</b>本码是"当前配置不够、理论上可调"；3510(<c>Jl_ERR_MRLE</c>) 是"自动扩容碰到实现硬上限"，调参也救不回，只能简化数据。</para>
	/// </remarks>
	public const int Jl_ERR_WRRLN2 = 3501;

	/// <summary>游程编码：行程长度为负：某条行程的像素长度是负数（错误码值 3502）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>逐条行程校验时发现(列起点,长度)里的长度为负。行程长度按定义至少为 1，出现负值说明数据被写坏——与 3500 同属"结构损坏"类，而不是用户参数类错误。</para>
	///   <para><b>典型来源</b>手工拼装行程数组时把"终点列"当"长度"传（终点-起点算反）、外部数据转换时的符号错误、序列化流截断后残留脏字节。</para>
	///   <para><b>处理建议</b>若区域来自外部构造，核对长度=终点列-起点列+1 的闭区间约定（含两端点各 1 像素）；库内算子链稳定复现本码时按 3500 的回溯法处理。</para>
	/// </remarks>
	public const int Jl_ERR_WRRLL = 3502;

	/// <summary>游程编码：行程行号越界：某行程所在行 &gt;= 图像高度（行坐标超出上界，错误码值 3503）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>把区域与图像对齐校验时发现某条行程的行号超出了图像行范围（行坐标 0..height-1，越上界报本码；小于 0 报 3504(<c>Jl_ERR_RLLTS</c>)，列方向的对应码是 3505/3506）。</para>
	///   <para><b>典型成因</b>对区域做过仿射/投影变换后未把结果裁回画幅，再拿去与源尺寸图像做运算；或行/列两轴写反（宽 640 高 480 的图里出现 500 以上的"行"就是这个症状，行是 y、列是 x）。</para>
	///   <para><b>处理建议</b>变换后的区域先与整幅图像域求交拉回画内再参与后续运算；核对坐标轴序时以 row=y 向下、col=x 向右为准。</para>
	/// </remarks>
	public const int Jl_ERR_RLLTB = 3503;

	/// <summary>游程编码：行程行号为负：某行程所在行 &lt; 0（错误码值 3504）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>与 3503(<c>Jl_ERR_RLLTB</c>) 成对，管行坐标的下界：出现负行号。最常见于把"以图像中心为原点"的坐标（可为负）直接喂给了要求"以左上角为原点"的接口，或平移运算把区域整体推到了画幅上方之外。</para>
	///   <para><b>处理建议</b>先统一坐标原点约定再进库；平移后的区域与图像域求交裁剪。列方向的负值对应码是 3506(<c>Jl_ERR_RLCTS</c>)。</para>
	/// </remarks>
	public const int Jl_ERR_RLLTS = 3504;

	/// <summary>游程编码：行程列越界：某行程的列坐标 &gt;= 图像宽度（错误码值 3505）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>列方向的上界校验：行程的列坐标落在图像右边界之外。行方向对应 3503(<c>Jl_ERR_RLLTB</c>)，负列对应本码的镜像 3506(<c>Jl_ERR_RLCTS</c>)。</para>
	///   <para><b>隐蔽成因</b>起点合法但"起点列+长度"跨过右缘的行程是否也算本码触发 [待实测]；自查数据时建议核对到"起点+长度-1 &lt; width"为止，别只查起点。</para>
	///   <para><b>处理建议</b>外部拼装行程时按图像实际尺寸钳制长度字段；变换产生的越界区域与图像域求交。</para>
	/// </remarks>
	public const int Jl_ERR_RLCTB = 3505;

	/// <summary>游程编码：行程列号为负：某行程的列坐标 &lt; 0（错误码值 3506，模板原文为德文 Lauflaengenspalte，即"行程列"）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>列方向下界校验，与 3504(<c>Jl_ERR_RLLTS</c>) 的行方向负值对称。成因同样是坐标约定错位（中心原点混进左上原点体系）或区域被平移出画幅左缘。</para>
	///   <para><b>提示</b>本码原文是四枚越界码里唯一没翻译成英文的遗留（3503~3506 同一族）——按日志检索时别用英文关键词找它，直接认数值。</para>
	///   <para><b>处理建议</b>与 3504 相同：统一原点约定、变换后裁剪回图像域。</para>
	/// </remarks>
	public const int Jl_ERR_RLCTS = 3506;

	/// <summary>弦型校验：行数过多：CHORD_TYPE 表示下行/弦数超过允许值（错误码值 3507）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>3507~3509 是弦型（CHORD_TYPE）表示下的另一组计数校验：本码管"行数过大"，3508(<c>Jl_ERR_CHLTS</c>) 管"行数过小"，3509(<c>Jl_ERR_CHCTB</c>) 管"列数过大"；注意族内没有"列数过小"的对应码 [待实测：CHORD_TYPE 具体对应哪种区域/轮廓表示及哪个包装算子使用]。</para>
	///   <para><b>与 3500 族分界</b>3500~3506 校验的是行程数据的合法性（坏数据），本码校验的是弦型规模是否超出表示层允许量（太大/太小的输入）。</para>
	///   <para><b>处理建议</b>按"规模超限"对待：拆分区域、降低轮廓分辨率或分批处理，而不是去修某个坐标值。</para>
	/// </remarks>
	public const int Jl_ERR_CHLTB = 3507;

	/// <summary>弦型校验：行数过少：CHORD_TYPE 表示下行/弦数低于允许值（错误码值 3508）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>3507(<c>Jl_ERR_CHLTB</c>) 的下界镜像：弦型表示要求的数量规模没达到——常见于把一个近乎空或退化的输入喂给了按弦批量处理的接口。</para>
	///   <para><b>坑</b>它报的是"太少"而不是"为空"：上游筛选把区域削到只剩极小块时可能正好落在本码区间里，看似偶发实则阈值问题；检查筛选链最后一级留下了什么。</para>
	///   <para><b>处理建议</b>确认输入非退化（行数、块数达到接口要求）再调用；空输入应在更早处分支退出，而不是让原生报码。</para>
	/// </remarks>
	public const int Jl_ERR_CHLTS = 3508;

	/// <summary>弦型校验：列数过大：CHORD_TYPE 表示下列方向数量超过允许值（错误码值 3509）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>弦型三码（3507 行多、3508 行少、3509 列多）的列向上界；族里没有"列过少"的第四枚，遇到列方向下界问题时不会见到配对码（本文件检索确认 [待实测：该缺失是否有意]）。含义细节同族标注，见 3507 条目。</para>
	///   <para><b>处理建议</b>与 3507 一致按规模超限处理：减小输入规模或分批，而不是改坐标值。</para>
	/// </remarks>
	public const int Jl_ERR_CHCTB = 3509;

	/// <summary>游程编码：自动扩容触顶：行程容量自动增长时超过实现上限（错误码值 3510）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>原生在行程不够用时会自动扩容；本码表示扩容目标已越过允许的最大值——3501（配置级）与硬上限之间的最后一级。</para>
	///   <para><b>与 3501 分界</b>3501(<c>Jl_ERR_WRRLN2</c>) 说"配置不够、可调整"；本码说"到实现天花板了"，调参无路可走（本库亦未见 set_system 包装，见 3501 条）。</para>
	///   <para><b>处理建议</b>只能降数据规模：拆分区域、简化边界后重试；反复出现的工程应先重设计区域生成方式，而不是期待更大的缓冲。</para>
	/// </remarks>
	public const int Jl_ERR_MRLE = 3510;

	/// <summary>区域结构损坏：补集标志既非真也非假：region 内部 compl 标志位取值非法（错误码值 3511）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>原生区域结构里有一个"是否补集型"布尔标志；校验发现它不是 0 也不是 1——内存被踩、结构体被外部改写或反序列化错位的典型症状，不是业务参数错误。</para>
	///   <para><b>排查</b>优先怀疑越界写（自管缓冲/裸指针交互）与多线程同时改写同一区域句柄；纯托管正常调用链几乎不可能触发，出现时先复现最短调用序列。</para>
	///   <para><b>处理建议</b>确认对象生命周期与线程归属；若来自读档，换内存构造的区域对照，锁定损坏环节。</para>
	/// </remarks>
	public const int Jl_ERR_ICCOMPL = 3511;

	/// <summary>区域结构损坏：声明容量小于已用数：region 的 max_num 小于 num（错误码值 3512）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>区域结构自带不变式"声明容量 max_num &gt;= 实际行程数 num"；两者倒挂说明结构内部失一致，与 3511(<c>Jl_ERR_ICCOMPL</c>)、3500/3502 一样属"坏数据"类，不是用户传错参数。</para>
	///   <para><b>方向</b>与 3513(<c>Jl_ERR_WRRLN3</c>) 核对的是同一对量（弦数 vs num_max），本码更像失一致的事后校验、3513 是写入时的容量拒绝，两码触发的确切阶段划分 [待实测]。</para>
	///   <para><b>处理建议</b>回溯该区域最近的构造/读档点做一致性验证；正常算子链中稳定复现即视为库缺陷反馈。</para>
	/// </remarks>
	public const int Jl_ERR_RLEMAX = 3512;

	/// <summary>游程编码：弦数超过声明上限：待写入的弦数大于该区域允许的 num_max（错误码值 3513）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>向区域追加/装载行程时，总量越过该区域自身声明的 num_max 上限——容量拒绝。与系统级容量（3501）和自动扩容触顶（3510）分属三层，与本码的核对阶段关系见 3512 条 [待实测]。</para>
	///   <para><b>处理建议</b>按数据量问题处理：拆分区域或先做简化再合并；把 num_max 类字段当作可调项硬改是危险操作，容量与内存布局挂钩 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WRRLN3 = 3513;

	/// <summary>补集区域不支持：该算子无法在补集型区域上实现（错误码值 3514）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>补集区域（<c>JlRegion.Complement()</c> 的产物，原生存"挖掉的像素+无界标志"）作为输入喂给无法给出有界结果的算子——按面积/周长统计、逐像素遍历这类要求有限域；具体哪些算子拒绝补集 [待实测]。</para>
	///   <para><b>与 3411 分界</b>3411(<c>Jl_ERR_SELC</c>) 限定的是"结构元素必须有限"，本码限定的是"某些算子不接受补集输入区域"；前者无一例外，后者按算子而异。</para>
	///   <para><b>正确用法</b>需要真实统计前，先让补集与图像域（或感兴趣矩形）求交折算成有界区域再进算子链；这也是"背景区域"的标准做法。</para>
	/// </remarks>
	public const int Jl_ERR_OPNOCOMPL = 3514;

	/// <summary>图像尺寸非法：宽度为负（错误码值 3520）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>创建/生成图像时宽度取负——典型为从计算式得宽高（缩放、裁剪 ROI）时把参数序颠倒或终点小于起点。</para>
	///   <para><b>族内分工</b>3520~3525 是同一组宽高校验在不同入口的两套重复：本码是"宽度 &lt; 0"（0 是否放行见 3524 条），超上界走 3521；高度对应 3522/3523；3524/3525 为另一入口的宽度/高度下界。</para>
	///   <para><b>处理建议</b>调用前对宽高断言下限；裁剪类还要检查两角点顺序，别把参数序错误伪装成尺寸错误。</para>
	/// </remarks>
	public const int Jl_ERR_WIMAW1 = 3520;

	/// <summary>图像尺寸非法：宽度达到格式上限（错误码值 3521）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>宽度 &gt;= MAX_FORMAT——原生以离散档位登记支持的图像格式，宽度触到档位表顶即报本码。与 3520(<c>Jl_ERR_WIMAW1</c>) 分别把守下界与上界。</para>
	///   <para><b>含义</b>这不是"再大一点点就能过"的软限制：上限附近往往还压着内存与算子档位的双重约束，勉强通过的大图未必稳定。</para>
	///   <para><b>处理建议</b>改分块/降分辨率路线；核对上游尺寸换算是否单位错位（毫米当像素、×1000 之类）造成数量级失控。</para>
	/// </remarks>
	public const int Jl_ERR_WIMAW2 = 3521;

	/// <summary>图像尺寸非法：高度不大于 0（错误码值 3522）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>创建/生成图像时高度为 0 或负。注意本码把 0 也拒了——想"先占位后填数据"地建零高图像走不通；宽度侧的下界码 3520 只拒负值，两个下界的松紧不一致是入口差异，别互相类推 [待实测：0 宽是否真的被 3520 入口放行]。</para>
	///   <para><b>处理建议</b>高度按 &gt;= 1 断言；从 ROI 推尺寸时先保证角点顺序合法，再谈尺寸。</para>
	/// </remarks>
	public const int Jl_ERR_WIMAH1 = 3522;

	/// <summary>图像尺寸非法：高度达到格式上限（错误码值 3523）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>高度 &gt;= MAX_FORMAT，与 3521(<c>Jl_ERR_WIMAW2</c>) 分别是宽/高触顶。竖向堆叠成长条的输入更容易先撞高度顶 [待实测]。</para>
	///   <para><b>处理建议</b>同族上界码：改成多图像对象平铺管理或分块处理；核对尺寸换算的单位。</para>
	/// </remarks>
	public const int Jl_ERR_WIMAH2 = 3523;

	/// <summary>图像尺寸非法：宽度不大于 0（错误码值 3524）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>与 3520(<c>Jl_ERR_WIMAW1</c>) 同为宽度下界，但本码按原文把 0 也拒（"&lt;= 0"）；两枚码来自不同校验入口（构造图像与生成/变换类 [待实测：具体入口对应]）。撞 3524 说明走的是严格入口，传 0 宽同样不行。</para>
	///   <para><b>处理建议</b>把"宽高至少为 1"写进参数校验；区分 3520/3524 能帮你反推是哪条调用路径出的错。</para>
	/// </remarks>
	public const int Jl_ERR_WIMAW3 = 3524;

	/// <summary>图像尺寸非法：高度不大于 0（第二入口，错误码值 3525）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>与 3522(<c>Jl_ERR_WIMAH1</c>) 原文完全同句（高度 &lt;= 0），分别挂在两个校验入口上——与 3520/3524 的"两套宽度下界"格局一致；两码谁对应哪个入口 [待实测]。</para>
	///   <para><b>排障价值</b>日志里 3522 与 3525 出现哪个，可用来区分出错调用走的是哪条路径；两码同现多半是同一批非法尺寸在链上连续撞了两道门。</para>
	///   <para><b>处理建议</b>与 3522 相同：高度至少 1，从源头断言。</para>
	/// </remarks>
	public const int Jl_ERR_WIMAH3 = 3525;

	/// <summary>分段过多：内部段（segment）计数超过上限（错误码值 3550）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>原文只留一句 "Too many segments"；段指哪种内部结构（行程段、轮廓线段还是分区）未注明 [待实测]。从编号位置看紧邻图像尺寸族（3520~3525），但不应据此断言。</para>
	///   <para><b>通用处置</b>所有"计数超上限"码的排查思路一致：这不是坐标或数值错，而是规模问题——拆小输入、减少一次性处理的对象数，并结合异常消息里的算子名定位是哪个环节把段数推爆的。</para>
	/// </remarks>
	public const int Jl_ERR_TMS = 3550;

	/// <summary>int8 图像不可用：该像素类型仅在 64 位运行时下支持（错误码值 3551）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>在 32 位进程中创建/转换出 int8（有符号单字节）型图像。原生明确只在 64 位系统提供该类型，与内存无关、与位宽有关——同一台机器上 x86 目标失败、x64 目标成功是它的识别特征。</para>
	///   <para><b>处理建议</b>优先换 uint8 承载（同为 1 字节，值域从 -128..127 变 0..255，涉及有符号偏移的算法要同步修正换算）；确需 int8 语义时把进程切到 x64。发布配置里锁死平台目标，别让同一套代码悄悄在 x86 上跑。</para>
	/// </remarks>
	public const int Jl_ERR_NO_INT8_IMAGE = 3551;

	/// <summary>无穷远点无法欧氏化：齐次权重为 0 的点除不出有限像素坐标（错误码值 3600）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>投影变换/反投影把某点映到消失线上：齐次坐标第三分量为 0（或数值上塌到 0），(x/w, y/w) 无有限解。常见于把接近相机地平面的深处点投回像素、或单应把点推到消失线附近。</para>
	///   <para><b>坑</b>有的实现不报错而是返回超大坐标"混过去"，两种行为在结果里差之毫厘、上线千里；比对两套变换实现时要专门看这一支 [待实测]。</para>
	///   <para><b>处理建议</b>先按 |w| 阈值剔除近奇异点再转换；若场景本身深度变化小，改按仿射/正射假设建模可整体绕开这个奇异方向。</para>
	/// </remarks>
	public const int Jl_ERR_POINT_AT_INFINITY = 3600;

	/// <summary>协方差矩阵无法确定：样本退化到建立不了协方差（错误码值 3601）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>需要协方差的估计/分类流程碰上退化样本：样本数少于特征维度、样本全部相同、特征彼此完全共线——协方差矩阵秩亏，估不出来。3600~3608 族段里它是"没有矩阵"，3607(<c>Jl_ERR_COV_NPD</c>) 是"有矩阵但不合格"。</para>
	///   <para><b>排查</b>先统计样本矩阵的秩与维度：秩低于维度即命中；若样本量充足仍报本码，检查特征列是否由同一源复制而来（信息零贡献的列等效于共线）。</para>
	///   <para><b>处理建议</b>补样本、降维或剔除常数列/重复列；维度不低于样本量时任何数值技巧都救不了秩亏，这是结构性问题。</para>
	/// </remarks>
	public const int Jl_ERR_ML_NO_COVARIANCE = 3601;

	/// <summary>RANSAC 抽样失败：随机采样在限度内没凑出足够支撑的对应关系（错误码值 3602）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>在两组点集间用 RANSAC 估计变换时，随机最小样本集反复选出"残差巨大、无共识"的组合，直到迭代/采样额度耗尽。名字里的 PRNG 点明失败在随机采样环节：内点比例太低时，纯随机几乎抽不到全内点组合。</para>
	///   <para><b>与 3603 分界</b>3603(<c>Jl_ERR_RANSAC_TOO_DIFFERENT</c>) 原文与本码同句，但按名解为"候选解彼此分歧过大"——一个是"采不出来"，一个是"采出来但定不了"，确切判定划分 [待实测]。</para>
	///   <para><b>处理建议</b>先提高输入质量再谈迭代数：收窄匹配搜索域、收紧上游配对阈值、确保对应点数量刚够模型自由度即可（多余错配只会拉低内点率）。</para>
	/// </remarks>
	public const int Jl_ERR_RANSAC_PRNG = 3602;

	/// <summary>RANSAC 候选解分歧过大：多个可行模型互不相容，定夺不下（错误码值 3603）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>按名解：采样得到的若干候选模型各自有支撑点，但参数互相差得太远无法归并成一个（模板原文只留了与 3602 同句的 "not enough correspondences"，本条以名字语义为主 [待实测]）。</para>
	///   <para><b>典型根因</b>场景有周期性/对称性——规则阵列、重复纹理使对应点成对模糊，两个"都对"的变换彼此差一个周期。这类数据上调迭代次数无效，解本来就是多峰的。</para>
	///   <para><b>处理建议</b>打破对称：让对应点覆盖更大空间跨度（远离一个周期以上）、或给初始位置/区域约束把解空间限定到单峰，再跑 RANSAC。</para>
	/// </remarks>
	public const int Jl_ERR_RANSAC_TOO_DIFFERENT = 3603;

	/// <summary>PTI 诊断码：求解退化到了后备方法（错误码值 3604）。注意它不是失败。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>投影变换接口（PTI，3604~3608 一段）的首选求解路径数值上走不通时，内部启用后备（fallback）方法并记下本码作为诊断——结果可能仍然正确，只是"不体面"。</para>
	///   <para><b>可见性</b>这类内部诊断通常不包装成异常抛出，能否从托管层拿到本码取决于返回码是否透传 [待实测]；如果日志/监控里能看到，把它当"数据质量预警"而不是错误处理。</para>
	///   <para><b>处理建议</b>偶发一次不必重跑；频繁触发说明输入在奇异边缘（点少、分布差、近共线），按 3605/3606 的方向修数据才是根治。</para>
	/// </remarks>
	public const int Jl_ERR_PTI_FALLBACK = 3604;

	/// <summary>投影变换奇异：解出的变换矩阵不可逆（错误码值 3605）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>由对应点估计投影变换时点配置退化：大量对应点近共线、点几乎重合、或有效自由度过少，解出的矩阵行列式趋于 0，正反变换都不存在。与 3604(<c>Jl_ERR_PTI_FALLBACK</c>) 同路：先尝试后备、彻底奇异才报本码 [待实测]。</para>
	///   <para><b>常识线</b>一般位置投影变换需要至少 4 对点且无三点共线——"点多"不等于"自由度高"，50 对挤在一条直线附近的点不如 6 对铺开四角的点。</para>
	///   <para><b>处理建议</b>检查对应点空间分布（画出来看共线性）；对近退化输入换用低自由度模型（仿射/相似）能显著抬高数值条件数。</para>
	/// </remarks>
	public const int Jl_ERR_PTI_TRAFO_SING = 3605;

	/// <summary>拼接欠定：图像间重叠约束不足以唯一确定各变换（错误码值 3606）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>多图拼接（mosaic）按"图像对之间的对应点"建全局方程；某些相邻对重叠太少、或整幅图只与一环相连，环路约束不够，自由度没收敛。它是"连得上但定不住"，彻底断连走 3621(<c>Jl_ERR_NOPA</c>)。</para>
	///   <para><b>坑</b>欠定时强行求最小二乘解，误差会沿约束最弱的路径累积——拼接看着成了，越远的图像错位越大；本码拦的就是这种静默劣化。</para>
	///   <para><b>处理建议</b>补拍增大相邻重叠、在重叠区多加稳定特征对应；仍欠定就把长链拆成几段分别求解再刚性对齐。</para>
	/// </remarks>
	public const int Jl_ERR_PTI_MOSAIC_UNDERDET = 3606;

	/// <summary>输入协方差非正定：用作不确定度/权重的矩阵不满足正定要求（错误码值 3607）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>接受协方差作输入（不确定性传播、加权求解类场合）的算子校验正定性：对角出现非正元素、矩阵被手工改得不对称、或高相关维度让特征值塌到 0，都过不了这道门。与 3601(<c>Jl_ERR_ML_NO_COVARIANCE</c>) 的"建不出来"相对，本码针对"递进来的矩阵不合格"。</para>
	///   <para><b>常见失手</b>把方差写成标准差再忘平方、单位换算后只改了一半矩阵项、从旧文件读来的矩阵已含负对角元。</para>
	///   <para><b>处理建议</b>入参前对称化并检查特征值下限，必要时叠加小量单位阵修复；这属于输入整形，不要靠原生"宽容处理"。</para>
	/// </remarks>
	public const int Jl_ERR_COV_NPD = 3607;

	/// <summary>输入点数超限：一次性送入的点数超过该接口上限（错误码值 3608）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>PTI 段末码：变换/拼接求解对输入点数设了硬上限（复杂度随点数增长），超限直接拒绝而不是变慢——报它是"该拆批了"，不是数据坏了。</para>
	///   <para><b>处理建议</b>先做空间均匀抽稀（网格化保留代表点），别按数组顺序截断——截断会毁掉点分布、把问题变成 3605 的奇异求解；分批时保证批间有公共点对，否则拼接接不起来（3606/3621）。</para>
	/// </remarks>
	public const int Jl_ERR_TOO_MANY_POINTS = 3608;

	/// <summary>点对应数目不一致：并行点集（图像点/物点等）两侧条数不相等（错误码值 3620）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>标定/变换估计按"第 i 个对第 i 个"消费两路平行点集；两侧计数一不等即报本码。典型成因：对一路做了筛选（按置信度、按区域）而另一路没同步裁。</para>
	///   <para><b>更危险的近邻错误</b>数目相等但顺序错位不会触发本码——得到的是静默的错解而非异常。两路点必须同源同序生成，任何一侧单独排序/过滤都是事故源。</para>
	///   <para><b>处理建议</b>成对过滤：对第一路算出的保留索引同时应用到第二路；调用前断言两路计数相等。</para>
	/// </remarks>
	public const int Jl_ERR_INPC = 3620;

	/// <summary>参考图像无路径：一张或多张图像与参考图像之间不存在可达链路（错误码值 3621）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>多视图求解把"共享对应点"当图像间的边；某张图与参考图所在连通分量完全无边相连时报本码。常见于批量图里混入了一张拍了别处场景的图，或重叠阈值收得太狠把仅有的几条边切没了。</para>
	///   <para><b>与 3606 分界</b>3606(<c>Jl_ERR_PTI_MOSAIC_UNDERDET</c>) 是"连得上但约束不足"，本码是"根本连不上"；处置一个靠加约束、一个靠补图或剔图。</para>
	///   <para><b>处理建议</b>确认每张图都留有与邻图的真实重叠；排查时放宽配对阈值看边是否恢复，再决定补拍还是把孤图剔出本次集合单独处理。</para>
	/// </remarks>
	public const int Jl_ERR_NOPA = 3621;

	/// <summary>指定序号的图像不存在：按索引引用图像时越出本次集合范围（错误码值 3622）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>以"图像序号"在图像集合内取图的接口收到越界索引。索引的有效性取决于当次传入的集合，不是进程历史——中途增删图像后，之前记下的序号全部可能失效或错位。</para>
	///   <para><b>坑</b>偏一位不报错而是取到隔壁那张图（合法索引但错对象）比越界更可怕；凡是"先算索引后取图"的代码，索引与集合要同一时刻快照。</para>
	///   <para><b>处理建议</b>调用前用集合计数做界检；能按对象引用传递就不要退化成按位置传递。</para>
	/// </remarks>
	public const int Jl_ERR_IINE = 3622;

	/// <summary>不是相机矩阵：传入矩阵不满足相机投影矩阵的结构要求（错误码值 3623）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>期望相机投影矩阵（3×4：内参×外参的复合，把世界坐标直投到像素）的接口收到了别的矩阵——最常见的混源是 2D 齐次单应（3×3，像素到像素）被当作相机矩阵传入；两者形状与语义都不同。</para>
	///   <para><b>自查</b>打印矩阵行列数与最后一列：相机矩阵的第 4 列承载平移，全零即结构塌陷；确认矩阵来源是标定/投影构造流程而非手拼。</para>
	///   <para><b>处理建议</b>像素到像素的映射用单应即可，不要塞进要相机矩阵的接口；需要"像素直连世界"时按标定结果组合出真正的投影矩阵 [待实测：本库提供哪个入口]。</para>
	/// </remarks>
	public const int Jl_ERR_NOCM = 3623;

	/// <summary>倾斜度非零：目标相机模型要求 skew 为 0，输入却带着非零值（错误码值 3624）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>相机参数向"不含倾斜度"的模型族转换/校验时，源参数 skew≠0，无法无损降格即拒绝。skew 描述像素行列网的不正交程度，现代面阵相机标定结果通常≈0；报本码多半意味着标定质量异常或参数被手工改过。</para>
	///   <para><b>兄弟码</b>3626(<c>Jl_ERR_KANZ</c>) 查畸变系数 kappa、3633(<c>Jl_ERR_TINZ</c>) 查像平面倾角 tilt，三码同一模式："目标模型容不下这个参数"。</para>
	///   <para><b>处理建议</b>重标定取干净参数；确有非零 skew 的传感器就留在支持它的模型族里用，强行清零会带入系统性斜切误差。</para>
	/// </remarks>
	public const int Jl_ERR_SKNZ = 3624;

	/// <summary>焦距非法：相机参数的焦距不在物理有意义的取值域（错误码值 3625）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>焦距（或与像素尺寸联用的像距类参数）非正、数值溢出、或被换算成 0。高危区是"毫米焦距 ÷ 像元尺寸"的换算：像元单位给错一档（µm 当 mm），商就塌到 0 或爆到天文数。</para>
	///   <para><b>关联</b>本码是 3624/3626/3633 一组"模型参数合法性"里的焦距项；同组码连排说明这一段在做相机参数的整体校验。</para>
	///   <para><b>处理建议</b>核对参数集内各项的单位约定是否成套（像元尺寸、图像宽高、焦距同一套单位制）；不要手改单一项去"凑"通过。</para>
	/// </remarks>
	public const int Jl_ERR_ILFL = 3625;

	/// <summary>畸变系数非零：目标模型不含 kappa，而输入 kappa≠0（错误码值 3626）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>把带径向畸变系数 kappa 的相机参数向无畸变模型降格（或走无畸变假设的算法入口）时的拒绝码；与 skew 检查 3624(<c>Jl_ERR_SKNZ</c>)、tilt 检查 3633(<c>Jl_ERR_TINZ</c>) 同族。</para>
	///   <para><b>取舍</b>广角/廉价镜头的 kappa 绝不为零：此时选"保持畸变"的模型族是正解；把 kappa 清零换来的图像在边缘会有肉眼可见的枕/桶形残差，测量类应用不可接受。</para>
	///   <para><b>处理建议</b>若只想消畸变而不改模型，先做畸变校正图像预处理，再喂给无畸变假设的流程。</para>
	/// </remarks>
	public const int Jl_ERR_KANZ = 3626;

	/// <summary>可变参数无法全部确定：被设为自由参数的项超出了当前数据的可估计范围（错误码值 3627）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>相机标定允许把一部分参数设为"可变的"去求解、其余固定；当图像数据对某个自由参数没有提供有效约束（最典型：所有图同距离同缩放却想解变焦距），该参数不可定，报本码。原文句子未写完（"parameters for in the variable case"），语义以名与族段推断为主 [待实测]。</para>
	///   <para><b>可定性的直觉</b>位姿类参数每张新图都增加约束；内参类靠"同一相机多视角/多深度"才变得可定——图不够杂，内参就锁不死。</para>
	///   <para><b>处理建议</b>把不可定的项先固定（用厂标值或上次标定值），只放开数据撑得住的自由度；宁可少解几个参数，不要拿欠定解冒充分解。</para>
	/// </remarks>
	public const int Jl_ERR_VARA = 3627;

	/// <summary>未选中有效实现：参数组合下没有任何可用求解实现（错误码值 3628）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>本段算子允许按选项挑选求解实现变体；给出的选择把全部可行路径都排除了（空集合，或互相排斥的选项同时选中）。缩写 LVDE 未在原文展开 [待实测：其确切含义]。</para>
	///   <para><b>与 3640 分界</b>3640(<c>Jl_ERR_ILMD</c>) 是"方法定了、参数要求与它冲突"，本码更靠前——连一个可用实现都没选出来，还没走到参数核对。</para>
	///   <para><b>处理建议</b>把选项恢复为空/默认先跑通，再逐个显式添加做排除法；批量生成的配置里别让旧版本的选项名残留。</para>
	/// </remarks>
	public const int Jl_ERR_LVDE = 3628;

	/// <summary>kappa 仅在金标准+固定内参下可解：畸变系数不属于当前方法的自由参数集（错误码值 3629）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>原文约束写得很死：kappa 只能在"金标准（gold standard）方法且相机内参固定"的前提下被确定。非此配置还把 kappa 设为待求，即报本码。金标准指标定物 3D 点坐标经过精确测量的传统标定板流程 [待实测：本库对应选项名]。</para>
	///   <para><b>与 3626 分界</b>3626(<c>Jl_ERR_KANZ</c>) 管"输入 kappa 非零过不了模型降格"，本码管"kappa 作为未知数的求解资格"；一个在输入侧，一个在求解配置侧。</para>
	///   <para><b>处理建议</b>要解畸变就上金标准板并固定内参；用不了金标准目标时，畸变系数改用厂标值或另行人工给定。</para>
	/// </remarks>
	public const int Jl_ERR_KPAR = 3629;

	/// <summary>图像数与投影模式冲突：输入图像张数不满足所选投影/标定模式的要求（错误码值 3630）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>某些投影/标定模式对图像张数有硬性要求（单图模式、按面分组的立方标定物模式等），传入张数与之矛盾即报本码。"投影模式"指几何投影方式的选择，不是图像颜色模式。</para>
	///   <para><b>坑</b>用复制图凑张数是反模式：张数满足了，重复帧不提供新约束，下一步等着你的多半是 3627(<c>Jl_ERR_VARA</c>) 或 3605(<c>Jl_ERR_PTI_TRAFO_SING</c>) 级别的解退化。</para>
	///   <para><b>处理建议</b>对照所选模式的拍摄规范补真实视角；换模式则重排输入集合。</para>
	/// </remarks>
	public const int Jl_ERR_IMOD = 3630;

	/// <summary>投影错误：点不在任何立方映射面上：立方展开坐标系中找不到容纳该点的面（错误码值 3631）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>立方投影（环境/全景按立方体六面展开表示）下投影一个点：其方向落在面缝外或数值舍入后掉出全部六个面的矩形范围。靠近棱边的点、相机参数与实际展开几何不符时最容易成批触发。</para>
	///   <para><b>判读</b>偶发一两枚是边界舍入，剔除或把有效面域内缩即可；成批出现就别修点了，回头核对立方展开参数是否配错了相机。</para>
	///   <para><b>处理建议</b>投影前按面内缩一个像素余量做预筛；确认参与运算的点方向都落在相机标定的覆盖范围内。</para>
	/// </remarks>
	public const int Jl_ERR_PNIC = 3631;

	/// <summary>无解：方程求解未给出可用解（错误码值 3632）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>本段最泛化的一枚：输入过了所有结构校验（没撞 3620~3631 的具体码），但方程在数值容差内无解、或迭代从任何起点都不收敛。它只说"解不出来"，不指责任何一个具体输入项。</para>
	///   <para><b>排查次序</b>对应点错位与几何退化（共线/共面/重合）是无解的头号来源，两者都不报专用码；二分定位：取空间分布好的小子集（如 6~8 对点）重跑，找出破坏解的那批点。</para>
	///   <para><b>处理建议</b>别用"多跑几次碰运气"对待无解码；同一输入稳定无解就说明问题在数据或模型选择上。</para>
	/// </remarks>
	public const int Jl_ERR_NO_SOL = 3632;

	/// <summary>像平面倾角非零：目标模型要求 tilt 为 0，输入却带着非零倾角（错误码值 3633）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>向不含 tilt 的相机模型降格/校验时，源参数的像平面倾角非 0；与 skew 码 3624(<c>Jl_ERR_SKNZ</c>)、kappa 码 3626(<c>Jl_ERR_KANZ</c>) 凑齐"三参数不容"组合。tilt 描述像平面相对理想成像面的旋转，当代传感器大多为 0，非零常见于老式线扫机构或异常标定。</para>
	///   <para><b>处理建议</b>先复核标定；真带倾角的系统留在支持 tilt 的模型族，强行置零会把倾角误差摊进其余参数里。</para>
	/// </remarks>
	public const int Jl_ERR_TINZ = 3633;

	/// <summary>参数与估计方法组合非法：所选方法不支持这套参数要求（错误码值 3640）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>每种估计/标定方法对"哪些参数可自由、需不需要初始值"各有约束；方法选定后又塞进它不支持的自由参数组合，报本码。与 3628(<c>Jl_ERR_LVDE</c>) 分界：那码是"没选出可用实现"，本码是"实现有了但参数要求与它冲突"。</para>
	///   <para><b>处理建议</b>先定方法、后配参数（顺序反了最容易撞）；仍冲突就把自由参数逐个退回固定值做排除，定位方法拒绝的那一项，再决定换方法还是换参数集。</para>
	/// </remarks>
	public const int Jl_ERR_ILMD = 3640;

	/// <summary>标定轮廓族：未找到合适轮廓：输入图像里提取不出可用轮廓（错误码值 3660）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>标定前向流程（RDS 族，3660~3663 [待实测：缩写 RDS 所指流程]）在输入上搜轮廓，结果为零或不合最低质量：图像对比度不足、标定物出画、提取参数（阈值/最小长度）设得不匹配。</para>
	///   <para><b>族内阶梯</b>本码"根本没找到"；3663(<c>Jl_ERR_RDS_NEC</c>) "找到了但条数不够标定"；3661(<c>Jl_ERR_RDS_NSS</c>)/3662(<c>Jl_ERR_RDS_ISS</c>) 已经到"解不稳"阶段。按码即可判断流程死在哪一步。</para>
	///   <para><b>处理建议</b>先在单帧上人工验证轮廓提取得通，再进批量流程；不要靠放松质量下限硬凑，那只会把故障推后成 3661/3662。</para>
	/// </remarks>
	public const int Jl_ERR_RDS_NSC = 3660;

	/// <summary>标定轮廓族：找不到稳定解：多次尝试得到的解彼此不一致（错误码值 3661）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>轮廓提取与求解都走通了，但不同初始值/子集解出的参数差得过大，无法认定一个稳定解。与 3662(<c>Jl_ERR_RDS_ISS</c>) 同族：本码是"事后拿不出稳的"，3662 是"解被当场判为不稳"。</para>
	///   <para><b>典型根因</b>输入图像视角单一（所有帧几乎同一姿态）——约束近似欠定，解在零空间方向上漂移；或标定物在拍摄中有微动。</para>
	///   <para><b>处理建议</b>补姿态差异大的帧、剔除可疑帧；不要为压残差强行调小稳定判据——那等于把本码换成"带病通过"。</para>
	/// </remarks>
	public const int Jl_ERR_RDS_NSS = 3661;

	/// <summary>标定轮廓族：得到不稳定解：解出来了但被判不可信（错误码值 3662）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>求解成功返回了一组参数，但稳定性检查（条件数、残差一致性一类判据 [待实测：具体判据]）未通过——数据在说"有解，别用"。</para>
	///   <para><b>坑</b>最危险的处置是当警告吞掉继续用：不稳定解常表现为焦距与畸变互相补偿，在标定数据集上残差漂亮，出了数据集立刻失效。</para>
	///   <para><b>处理建议</b>按 3661 的方向修输入（姿态多样性、标定物稳定）；确有把握时也应对比两次独立标定的参数差异来量化"不稳"到什么程度。</para>
	/// </remarks>
	public const int Jl_ERR_RDS_ISS = 3662;

	/// <summary>标定轮廓族：轮廓条数不足以标定：有轮廓但撑不起参数求解（错误码值 3663）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>提取到的轮廓数大于零（否则是 3660(<c>Jl_ERR_RDS_NSC</c>)）但达不到本次求解的最低约束量；"多少算够"随待求自由参数数变化，固定阈值 [待实测]。</para>
	///   <para><b>处理建议</b>两条正路：增加图像/帧数让轮廓总量上去；或把拿不准的自由参数逐项退回固定值，降低约束需求。靠放宽提取门槛凑数的轮廓质量差，会把问题推给 3661/3662。</para>
	/// </remarks>
	public const int Jl_ERR_RDS_NEC = 3663;

	/// <summary>FFT 优化数据格式无效：读入的文件不是合法的 FFT 优化数据（错误码值 3650）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>装载 FFT 优化数据（运行期为频域运算生成的加速计划缓存）时，文件魔数/结构校验失败：拿错文件、文件被文本方式转存过、或写到一半被截断。</para>
	///   <para><b>族内分界</b>3650 是"字节层面就不对"；3651(<c>Jl_ERR_WRFFTOPTVERS</c>) "格式对但版本超范围"；3654(<c>Jl_ERR_FFTOPT_NOSITEM</c>) "序列化容器能解析但装的不是这型数据"。三码对应三种恢复动作。</para>
	///   <para><b>处理建议</b>这类文件是缓存不是源数据，删掉重生成是最省事的恢复路径；需要保留加速收益时，排查的是"为什么缓存会被写坏"（磁盘满、进程中途被杀）。</para>
	/// </remarks>
	public const int Jl_ERR_NOFFTOPT = 3650;

	/// <summary>FFT 优化数据版本不支持（错误码值 3651）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>优化数据文件本身合法，但其数据版本不在当前库的支持区间——最常见是新版库写出的缓存被旧版库来读。</para>
	///   <para><b>处理建议</b>升库或删缓存重生成二选一；手工改文件头里的版本号是自欺（数据布局随版本而变），改完撞上的是 3650 甚至更糟。</para>
	///   <para><b>部署提示</b>多版本共用的机器上给优化数据目录按库版本分栏，避免新旧互踩。</para>
	/// </remarks>
	public const int Jl_ERR_WRFFTOPTVERS = 3651;

	/// <summary>优化数据产自另一形态的 Vision 运行时（标准版/并行版不互认，错误码值 3652）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>原文写明：优化数据绑定生成它的运行时形态——标准版与并行版各自写出的数据互不通用（并行版的计划含其执行形态相关信息 [待实测：具体差异项]），错配加载报本码。</para>
	///   <para><b>与 3651 分界</b>3651 管同一形态内的版本号，本码管跨形态；两码都要靠"用当前运行时重新生成"解决。</para>
	///   <para><b>处理建议</b>部署标准版与并行版混布的环境时，优化数据目录按运行时形态隔离，不要共享同一缓存路径。</para>
	/// </remarks>
	public const int Jl_ERR_WRVisionVERS = 3652;

	/// <summary>优化数据保存失败：写盘/持久化未能完成（错误码值 3653）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>把优化数据存盘失败：目标目录不存在、无写权限、磁盘满、文件被其他进程占用。多进程共写同一缓存目录是重灾区。</para>
	///   <para><b>影响评估</b>单次失败无损——优化数据是缓存，下次会重新优化；但"每次都报"意味着白付每进程启动的优化成本，需要按 I/O 问题正经处理。</para>
	///   <para><b>处理建议</b>检查路径与权限、避免并发写同一路径（按实例分文件）；若保存被业务当作硬失败处理，多半过严——缓存缺失的代价只是下次重算 [待实测：装载失败后原生是否自动重建]。</para>
	/// </remarks>
	public const int Jl_ERR_OPTFAIL = 3653;

	/// <summary>序列化项不含有效 FFT 优化数据：容器可解析、装的却不是这型数据（错误码值 3654）。</summary>
	/// <remarks>
	///   <para><b>触发时机</b>从序列化项还原 FFT 优化数据时，容器结构解析正常但内部对象类型不符——别的类型的序列化缓冲被投喂进了优化数据读取口。与 3395(<c>Jl_ERR_SVM_NOSITEM</c>) 等同属"一族一枚"的 NOSITEM 码，按数值段即可定位是哪族的读取口被喂错。</para>
	///   <para><b>与 3650 分界</b>3650(<c>Jl_ERR_NOFFTOPT</c>) 是字节结构就不对（可能根本不是序列化项），本码是"确实是序列化容器，但类型不对"；恢复动作不同：前者查文件完整性，后者查调用配对。</para>
	///   <para><b>处理建议</b>核对保存与还原两侧使用的接口是否同一型数据；同一目录混放多种缓存时按类型分文件名/分后缀。</para>
	/// </remarks>
	public const int Jl_ERR_FFTOPT_NOSITEM = 3654;

	/// <summary>双目视差测量的视差搜索范围不合法：给 binocular_disparity_ms 方法的视差区间形不成可用闭区间。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Invalid disparity range for binocular_disparity_ms method</c>。视差是同一物理点在两张已校正图像间的列向偏移，该算法只在调用方给定的 [dmin,dmax] 闭区间内测视差；下限压过上限、空区间或超出方法可测界限（原生具体受理规则 [待实测]）都在参数校验期撞本码，测量不会开始。</para>
	///   <para><b>族内分界</b>3690-3702 是双目族：3690 管"范围参数本身不可用"，3700-3702 管"两台相机几何不支持校正"，3710-3725 是 BI_ 通用参数的类型/取值档。相邻编号未必同一参数，定位靠消息文本不靠码序。</para>
	///   <para><b>处理建议</b>视差越大对应越近的物体，区间两端应对齐待测深度远近极限换算出的视差（由基线、焦距与标定换算 [换算入口待实测]），不要盲目放宽范围凑数。本库已删除双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_INVLD_DISP_RANGE = 3690;

	/// <summary>核落在图像域内：对极校正不利的几何构型，双目标定/校正的前置检查失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Epipoles are situated within the image domain</c>。核（epipole）是一台相机光心射线落在另一台图像上的交点；核若落在该图像内，对极线以它为心呈放射状，无法用平面校正把所有对极线同时摆成水平平行线——后续按行搜索视差的前提从几何上就不成立。</para>
	///   <para><b>族内分界</b>3700-3702 三码是同一套双目构型的三种病灶：3700 判核的位置，3701 判视场是否相交，3702 是校正求解的兜底失败；都是"构型"问题而非参数问题，调 BI_ 参数族（3710-3725）救不了它们。</para>
	///   <para><b>处理建议</b>改安装而不是改参数：减小两光轴夹角或整体横移，让核退出图像域；近平行小基线构型一般不触发本码 [临界构型的判定线待实测]。本库无该族托管入口，常量仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_EPIINIM = 3700;

	/// <summary>两台相机视场完全不相交：找不到任何可共同观测的区域供匹配与校正。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Fields of view of both cameras do not intersect each other</c>。双目的一切计算都发生在左右视场的重叠区上；光轴外张、安装装反或长焦窄视场装大基线，都会让两侧像素集合无交集，原生在预检阶段即报本码。</para>
	///   <para><b>族内分界</b>与 3700 同属前置校验但病灶不同：3700 是核的位置不利，本码是重叠区为空；"相交但偏小"不归本码，通常会滑向 3702 的校正失败或匹配后无结果 [偏小情形归哪档待实测]。</para>
	///   <para><b>处理建议</b>先用两张实拍图肉眼确认同一目标两侧都可见，再谈标定。本库已删除该族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_EPI_FOV = 3701;

	/// <summary>行对齐校正无法完成：双目几何与畸变构型超出校正算法的可解范围。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Rectification impossible</c>。这是 3700-3702 族的兜底码：核在图内（3700）与视场不相交（3701）是两种明病灶，剩下的原因——镜头畸变过大、标定点分布差、两相机接近退化位姿——都汇入本码，消息本身不细分 [子因清单待实测]。</para>
	///   <para><b>排查顺序</b>先排除 3700/3701 两类明病灶，再看标定重投影误差与畸变矫正项；校正失败则视差测量无从谈起，本码优先级高于 BI_ 参数族（3710-3725）——参数是快变量，构型不是。</para>
	///   <para><b>处理建议</b>重标定（增加点位覆盖、让标定板走满图像角落）或减小基线夹角。本库已删除该族托管包装，常量仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_EPI_RECT = 3702;

	/// <summary>target_thickness 类型档：被测空间薄层厚度这一位没给成数值槽位。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong type of target_thickness parameter</c>。3710-3725 段是双目视差测量的通用参数校验族，命名规律 WT_ = 槽位类型错、WV_ = 取值不合法；target_thickness 决定"测哪一层、层多厚"，本码管它的类型，取值档在 3716(<c>Jl_ERR_BI_WV_TARGET</c>)。</para>
	///   <para><b>编号规律</b>本段不是"每参数类型/取值相邻成对"到底：3710-3713 连排四个参数的类型档，对应取值档散在 3714、3716-3719；3720 起才恢复成对。相邻码常非同一参数，定位以消息点名的参数名为准。</para>
	///   <para><b>坑</b>托管层通用参数以元组承载、数值与字符串可隐式转换，"类型错"多源于名-值成对拼装错位而非有意传错类型。本库已删除双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WT_TARGET = 3710;

	/// <summary>thickness_tolerance 类型档：厚度方向允差这一位没给成数值槽位。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong type of thickness_tolerance parameter</c>。与 3710 配对：target_thickness 定"测哪一层"，thickness_tolerance 定"允许偏离这层多远"，两者是同一次测量的组合项；本码只判类型，取值档在 3717(<c>Jl_ERR_BI_WV_THICKNESS</c>)。</para>
	///   <para><b>参数取向</b>这是覆盖修正项：调小了会把候选匹配全部排除在外，调大了会把相邻薄层的东西一并测进结果——实际使用中取值档远比本类型档常见，撞到这里基本是参数包拼装错位。</para>
	///   <para><b>坑</b>本库已删除双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WT_THICKNESS = 3711;

	/// <summary>position_tolerance 类型档：位置匹配容差这一位没给成数值槽位。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong type of position_tolerance parameter</c>。该参数控制左右同名点确认时允许的位置偏差（像素量级 [单位与受理范围待实测]）；本码判类型，取值档在 3718(<c>Jl_ERR_BI_WV_POSITION</c>)。</para>
	///   <para><b>族内位置</b>与 3710/3711 两个厚度项、本项合称"空间薄层+允差"核心控制组，在同一参数包里递交；类型档触发说明校验还没走完、测量并未开始。</para>
	///   <para><b>坑</b>本段编号按交错排布（本参数取值档在 3718 而非紧邻），核对时按消息点名的参数名找档，别按码序猜。本库无该族托管包装，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WT_POSITION = 3712;

	/// <summary>sigma 类型档：高斯平滑尺度这一位没给成数值槽位（取值档 3714 与它紧邻）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong type of sigma parameter</c>。sigma 是双目视差匹配前做高斯预平滑的标准差（像素单位），本码只判槽位类型；紧接着的 3714(<c>Jl_ERR_BI_WV_SIGMA</c>) 判取值——3713/3714 是本段"类型/取值紧邻成对"的例子之一。</para>
	///   <para><b>坑</b>数值 0 与字符串 "0" 是两回事：前者归取值档（0 是否合法 [待实测]），后者归本类型档。托管层字面量可隐式转成元组，撞到这里说明名-值对拼装时这一位被字符串挤占。</para>
	///   <para><b>条目</b>本库无双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WT_SIGMA = 3713;

	/// <summary>sigma 取值档：平滑尺度类型给对了，但大小不在允许范围。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong value of sigma parameter</c>。类型档是 3713；本码的常见触因是非正值或过大 [受理区间托管层无文档，待实测]。</para>
	///   <para><b>参数取向</b>sigma 作用在匹配所用的尺度空间图像上而非输出形式：太小压不住纹理噪声，太大把邻近结构糊成一团、错配更多——视差结果本身差时它是首批嫌疑项之一，与 3710-3712 那组"测不测得到"的项性质不同。</para>
	///   <para><b>条目</b>本库无双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WV_SIGMA = 3714;

	/// <summary>threshold 类型档：匹配质量筛选这一位没给成数值槽位。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong type of threshold parameter</c>。这里的 threshold 是按匹配质量（确信度）筛选候选视差点的门槛，与图像分割族的同名阈值不是一回事——它不切区域，决定"多确信才收进结果"。取值档在 3719(<c>Jl_ERR_BI_WV_THRESH</c>)。</para>
	///   <para><b>编号坑</b>3714 还是 sigma 的取值档、3715 已是 threshold 的类型档，本段排布交错，参数归属以消息点名为准。</para>
	///   <para><b>条目</b>本库无双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WT_THRESH = 3715;

	/// <summary>target_thickness 取值档：薄层厚度大小类型给对了但不被受理。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong value of target_thickness parameter</c>。与 3710 同参数两档：过掉类型档才轮到本码，触发即大小被拒（负值/非正值/超上限 [具体判定待实测]）。单位的量纲约定（毫米还是按视差折算）托管层无文档 [待实测]。</para>
	///   <para><b>参数取向</b>厚度项与 3711/3717 的 tolerance 项必须搭着调：厚度开大了把前景背景之外的层测进结果，开小了倾斜表面的深度展布覆盖不全；单独动一项常从本码滑进另一码。</para>
	///   <para><b>条目</b>本库无双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WV_TARGET = 3716;

	/// <summary>thickness_tolerance 取值档：厚度允差大小类型给对了但不被受理。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong value of thickness_tolerance parameter</c>。类型档 3711；本码判大小：负值、零值或超出与 target_thickness 配套的受理范围 [确切边界待实测]。</para>
	///   <para><b>参数取向</b>这一项是"层的松紧边"，其有效幅度依赖 3716 的厚度本身——两码常在一次调试中先后出现 [先后与互斥关系待实测]；先把两项对齐到同一量纲再单点微调。</para>
	///   <para><b>条目</b>本库无双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WV_THICKNESS = 3717;

	/// <summary>position_tolerance 取值档：位置匹配容差大小类型给对了但不被受理。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong value of position_tolerance parameter</c>。类型档 3712；本码判大小，零值是否可行、上限多少托管层无文档 [待实测]。</para>
	///   <para><b>参数取向</b>容差开小，标定残差稍大就会让本来该配上的点配不上；开大则错配进入结果——它与 3715/3719 的质量门槛是跷跷板两端：放宽位置侧常常要靠收紧质量侧找补，排查时两项一起动比单动少绕弯路 [联动行为待实测]。</para>
	///   <para><b>条目</b>本库无双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WV_POSITION = 3718;

	/// <summary>threshold 取值档：匹配质量门槛大小类型给对了但不被受理。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong value of threshold parameter</c>。类型档 3715；本码是数值落在受理区间之外 [确切区间待实测]。别按图像分割阈值的习惯猜它是 0-255 灰度域——它筛的是匹配置信度，量纲随匹配度量而定 [度量定义待实测]。</para>
	///   <para><b>参数取向</b>门槛抬高，错误视差点变少、有效点也变少；压低则相反。它与 3712/3718 位置容差的联合松紧是本族结果质量的主调节点，先定本码再动容差，一次只动一项。</para>
	///   <para><b>条目</b>本库无双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WV_THRESH = 3719;

	/// <summary>refinement 类型档：细化策略这一枚举位没给成要求的槽位类型。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong type of refinement parameter</c>。refinement 选择"视差结果细化到什么程度"（亚像素细化档位），允许枚举集托管层无文档 [待实测]；本码判槽位类型，取值档紧邻在 3721(<c>Jl_ERR_BI_WV_REFINE</c>)。枚举项被当成数值递交、或参数包错位，是本码的典型触因。</para>
	///   <para><b>档位差别</b>细化档位直接换算力与结果质量：不细化出整像素视差、细化越深越接近亚像素但耗时越多 [档位清单与效果待实测]，别用默认档位一概而论。</para>
	///   <para><b>条目</b>本库无双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WT_REFINE = 3720;

	/// <summary>refinement 取值档：细化策略的枚举串不在允许集内。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong value of refinement parameter</c>。类型档 3720 已过、槽位类型正确，本码是串值不被识别——枚举串最典型的走样方式是大小写、下划线与拼写差异，允许值清单 [待实测]。</para>
	///   <para><b>分档价值</b>枚举型参数先撞类型档还是取值档，说明的是完全不同的病灶：类型档查参数包拼装，取值档查串本身；核对消息里的 type/value 用词再动手，能省一半猜测。</para>
	///   <para><b>条目</b>本库无双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WV_REFINE = 3721;

	/// <summary>resolution 类型档：分辨率这一位没给成数值槽位。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong type of resolution parameter</c>。resolution 控制视差输出/搜索的分辨率或步长 [具体作用位待实测]，取值档紧邻在 3723。本码只判槽位类型，触发即参数包没拼对，测量未开始。</para>
	///   <para><b>与 sigma 的分界</b>"分辨率"与 sigma 的"平滑尺度"都按像素计但管两件事：sigma 决定在哪个模糊尺度上匹配，resolution 决定结果/搜索的疏密；想要更多细节却去加大平滑 sigma，方向就调反了。</para>
	///   <para><b>条目</b>本库无双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WT_RESOL = 3722;

	/// <summary>resolution 取值档（按常量名 WV_ 理解）：大小不在允许范围；注意内嵌英文文本误写成了 "Wrong type"。</summary>
	/// <remarks>
	///   <para><b>含义</b>本码内嵌原文与 3722 一字不差：<c>Wrong type of resolution parameter</c>，而常量名按族内规律是 WV_ = wrong value——文本与命名冲突，同段只有 3725(<c>Jl_ERR_BI_WV_POLARITY</c>) 犯同样的毛病。按命名规律把本码理解为 resolution 的取值档，原生实报行为以实测为准 [待实测]；用文本搜消息时别只按一档搜。</para>
	///   <para><b>参数取向</b>resolution 的大小直接换结果密度与耗时：调细则点云密、边缘细节好但更慢且噪声点更多，调粗反之 [量化影响待实测]。</para>
	///   <para><b>条目</b>本库无双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WV_RESOL = 3723;

	/// <summary>polarity 类型档：对比度方向选择这一枚举位没给成要求的槽位类型。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong type of polarity parameter</c>。polarity 决定按哪种亮度跃变方向的特征去做匹配（如只取暗到亮/只取亮到暗/两者都要 [枚举集待实测]），本码判类型，取值档在 3725(<c>Jl_ERR_BI_WV_POLARITY</c>)。</para>
	///   <para><b>参数取向</b>这一项的作用是在重复纹理里掐掉一半极性的误配源：目标表面以单侧对比度为主时收窄方向可显著减少歧义；收错方向则整类特征消失、结果大面积空洞——它不是"越少越好"的过滤器。</para>
	///   <para><b>条目</b>本库无双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WT_POLARITY = 3724;

	/// <summary>polarity 取值档（按常量名 WV_ 理解）：枚举串不在允许集；注意内嵌英文文本与 3724 雷同、写成了 "Wrong type"。</summary>
	/// <remarks>
	///   <para><b>含义</b>本码内嵌原文为 <c>Wrong type of polarity parameter</c>，与 3724 一字不差，而名字按族内规律 WV_ = 取值档——与 3723(<c>Jl_ERR_BI_WV_RESOL</c>) 同为本段的文本/命名冲突，两档理解以命名规律为准、原生实报行为 [待实测]。用英文文本反查消息来源时，同一句文本会命中两个码，别据此断定档位。</para>
	///   <para><b>分档价值</b>枚举串参数撞类型档还是取值档是两种病灶：前者查参数包拼装错位，后者查串的拼写/大小写；核对消息用词再动手，修正动作完全不同。</para>
	///   <para><b>条目</b>本库无双目视差族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BI_WV_POLARITY = 3725;

	/// <summary>片光模型列表为空：还没有任何片光模型可供标定/重建使用。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>No sheet-of-light model available</c>。片光（结构光光平面）族的标定与重建以一个或多个模型为输入，列表为空——创建模型的步骤没跑、创建失败未察觉、或结果被提前释放——都在调用入口即报本码，解算完全未开始。</para>
	///   <para><b>族内分界</b>3751-3764 段是片光族"状态与输入完备性"校验（模型有没有、图像对不对、各坐标分量缺不缺）；3765 起同族转入通用参数的 WT_/WV_/WN_ 校验段。两段的排查方向不同：本段补数据，后段核参数。</para>
	///   <para><b>条目</b>该族的托管包装（标定数据、相机内参对象等）已在本库删除，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_EMPTY_MODEL_LIST = 3751;

	/// <summary>输入图像宽度与期望不符：图像的列数和模型要求的不一致。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong input image size (width)</c>。片光模型按标定时图像的列数固定了剖面采样，后续送入的标定/重建图像宽度必须严格一致；换采集分辨率、改 ROI 裁剪或拿错模型都会撞本码。</para>
	///   <para><b>族内分界</b>宽度、高度分码：3752 管宽、3753 管高，消息直接点破是哪个方向，不用猜；两向都不符时先报哪个 [待实测]。</para>
	///   <para><b>处理建议</b>核对当前相机设置与模型建立时图像的尺寸是否同源；靠缩放图像硬凑宽度会连采样几何一起错掉，不可取。本库无该族托管包装，常量仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WNIW = 3752;

	/// <summary>输入图像高度与期望不符：图像的行数和模型要求的不一致。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong input image size (height)</c>。与 3752 同规律、方向相反：模型固定了剖面的行数，当前图像高度对不上即报本码。行向不符的常见来路是采集端按行裁剪/隔行模式改变，而非整幅分辨率切换。</para>
	///   <para><b>族内分界</b>本码与 3752 只按方向分家；两向都错时报哪一个 [待实测]。它与 3754(<c>Jl_ERR_SOL_WPROF_REG</c>) 的分界要记牢：本码是"图不对"，3754 是"图对但剖面区域出了图"。</para>
	///   <para><b>条目</b>本库无该族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WNIH = 3753;

	/// <summary>剖面区域超出输入图的有效定义域：要提取的区域不完全落在图内。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>profile region does not fit the domain of definition of the input image</c>。片光族标定/重建用"剖面区域"圈定激光条纹在图像中的位置，区域越过图像边界（或越过本次调用传入的定义域）时提取无从谈起，预检即报本码。</para>
	///   <para><b>与 3752/3753 的分界</b>那两码是图像尺寸本身与模型不符；本码是图像可用、圈定的区域画出去了。典型来路：区域是在另一幅图上画的、平移变换后没重新裁剪，或图像裁剪后沿用了旧区域坐标。</para>
	///   <para><b>处理建议</b>递交前把区域与图像域求交 [原生是否自动求交待实测]。本库无该族托管包装，常量仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WPROF_REG = 3754;

	/// <summary>标定扩展数据未设置：标定对象上本族路径要用的扩展段是空的。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Calibration extend not set</c>。"calibration extend" 指该片光标定数据中随扩展功能一并写入的附加段 [具体承载内容托管层无文档，待实测]；走过旧版本数据、反序列化残缺或只建了基础段时，后续标定/重建取不到扩展即报本码。</para>
	///   <para><b>坑</b>这是"状态缺失"码，不是参数错：改参数无效，要补的是数据对象本身。与 3756-3762 的 UNDEF_ 族分界在本码指向扩展段、那些码指向视差图/内参/位姿等具体组成——报出来的缺口就是还差的那一块。</para>
	///   <para><b>条目</b>本库已删除 <c>JlCalibData</c>、<c>JlCamPar</c> 等该族类型，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_CAL_NONE = 3755;

	/// <summary>视差图未定义：本条路径要求配套的视差图没有给出（UNDEF_ 族的第一枚）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Undefined disparity image</c>。在片光段里出现"视差图"字样值得留意——该词通常是双目族产物，这里判缺失，说明本族某条重建/校验路径把视差类图像当作必需输入而调用方没给 [具体路径与用途待实测]；理解以消息文本为准，别按算子名猜输入。</para>
	///   <para><b>族内排布</b>3756-3762 是 UNDEF_ 族，逐项盯标定数据的完备性：3756 视差图、3757 其定义域、3758 内参、3759 光平面位姿、3760 相机系位姿、3761 两系间变换、3762 运动位姿。它像进度指示器：补齐一项再跑，报的码往下一项挪。</para>
	///   <para><b>条目</b>本库无该族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_UNDEF_DISPARITY = 3756;

	/// <summary>视差图的定义域未设：图给了，但用于计算的生效区域是空的。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Undefined domain for disparity image</c>。图像对象上"像素内容"与"定义域（参与计算的区域）"是两层数据：3756 判图本身缺失，本码判图在、域没设。域空不等于"整幅参与计算"，原生按数据不完备处理。</para>
	///   <para><b>坑</b>常见来路是图经拷贝/转换后域没跟过来，或把已被释放/清空的区域赋给了图 [原生是否会在域缺失时自动按全图处理，待实测]。恢复动作是显式设全域或补一块有效域，而不是换图。</para>
	///   <para><b>条目</b>本库无该族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_UNDEF_DISPDOMAIN = 3757;

	/// <summary>相机内参未定义：标定数据里的内参分量整个缺失。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Undefined camera parameter</c>。内参决定像素对应的视线几何，缺了它一切 3D 映射无从建立；UNDEF_ 族按组件分账——本码管内参，3759-3761 管位姿与系间变换，3762 管运动位姿，报哪个码即哪个组件空着。</para>
	///   <para><b>与近邻码分界</b>"完全没设"才是本码；内参串给了但分量类型错是 3770(<c>Jl_ERR_SOL_WT_CAM_PAR</c>)、个数错是 3779(<c>Jl_ERR_SOL_WN_CAM_PAR</c>)，三者恢复动作不同：补设、改位、改长度。</para>
	///   <para><b>条目</b>本库已删除 <c>JlCamPar</c> 类型，托管层无此组件的对象化入口；常量仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_UNDEF_CAMPAR = 3758;

	/// <summary>光平面坐标系位姿未定义：激光片光所在平面在空间中的姿态没给。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Undefined pose of the lightplane</c>。片光测量里，激光条纹与工件的交点之所以能变成 3D 点，是因为光平面的空间位姿已知（光平面坐标系 LPCS）；这一项未设置，剖面像素只能停在 2D。</para>
	///   <para><b>三件套分界</b>3759 光平面侧、3760 相机侧（CCS）、3761 两系之间的变换——"缺一环报一码"，逐项补齐即把标定数据完备度当进度条读；与位姿串"格式不对"的 3771/3780 两码分属"没给"与"给了但错"。</para>
	///   <para><b>条目</b>本库 <c>JlPose</c> 类型仍在，但该族片光标定包装已删除，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_UNDEF_LPCS = 3759;

	/// <summary>相机坐标系位姿未定义：相机相对参照系的姿态没给，输出失去锚点。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Undefined pose of the camera coordinate system</c>。片光重建把剖面像素先映到相机坐标系（CCS）再转到工件/世界系；本项未设即第一步就没有落点。与 3759（光平面侧）、3761（CCS 到 LPCS 的变换）正好切分"缺 A / 缺 B / 缺 A 与 B 之间的联系"三种状态。</para>
	///   <para><b>与位姿格式码分界</b>本码只判"整个没设"；位姿串给了但某分量类型不对是 3771(<c>Jl_ERR_SOL_WT_PAR_POSE</c>)、元素个数不对是 3780(<c>Jl_ERR_SOL_WN_POSE</c>)——先补上、再修格式，恢复动作不同。</para>
	///   <para><b>条目</b>本库无该族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_UNDEF_CCS = 3760;

	/// <summary>相机系到光平面系的变换未定义：两侧位姿都有了，中间这一环还空着。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Undefined transformation from the camera to the lightplane coordinate system</c>。片光重建的映射链要在相机坐标系（CCS）与光平面坐标系（LPCS）之间换算 [换算方向以原生实现为准，待实测]，系间变换是必备环节。3759/3760 各管一侧位姿缺失，本码是两侧齐了但联系没建——逐项补到本码还报，说明补的是位姿、漏的是变换。</para>
	///   <para><b>处理建议</b>该变换在片光族里通常随标定流程整体写入而非手工单设 [具体入口待实测，本族托管包装已删除]；数据从旧文件恢复时最容易丢这一段。</para>
	///   <para><b>条目</b>本库无该族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_UNDEF_CCS_2_LPCS = 3761;

	/// <summary>xyz 标定的运动位姿未定义：走移动式标定时，视角间的运动位姿没给。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Undefined movement pose for xyz calibration</c>。片光族的 xyz（绝对三维）标定靠"相机或工件按已知位姿移动若干次"产生多余观测来解未知量；运动位姿缺失则该路径欠约束，直接拒算。撞本码说明已选定移动式标定分支——这区别于 3758-3761 那组静态组件缺失。</para>
	///   <para><b>坑</b>运动位姿通常是一序列（每帧一次运动的相对位姿），漏其中任意一项是否即报本码、还是只报第一处缺口 [逐帧校验行为待实测]。补数据时整序列一起给，别逐帧试探。</para>
	///   <para><b>条目</b>本库无该族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_UNDEF_MOV_POSE = 3762;

	/// <summary>scale 参数取值不合法：数值超出允许范围（本段无配对的类型档）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong value of scale parameter</c>。片光段里 scale 挂在哪个调用、量纲为何（缩放因子/步长）托管层无文档 [待实测]；先按调用链确定参数包再调值，别按同名参数在别的族的经验套。</para>
	///   <para><b>族内分界</b>本码是 3751-3764"状态与输入"段的最后一枚取值码；3765 起同族转入按参数命名的 WT_/WV_/WN_ 校验段，命名规律相同（WV_ = 取值错），管的参数不同。</para>
	///   <para><b>条目</b>本库无该族托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WV_SCALE = 3763;

	/// <summary>参数名本身不对：按名字存取通用参数时，这个名字不在允许的参数表里。</summary>
	/// <remarks>
	///   <para><b>含义</b>原文 <c>Wrong parameter name</c>。名-值成对递交的通用参数先过"名字匹配"这一关：串不被认识即报本码，参数自身的类型/取值档（各参数的 WT_/WV_）都还没轮到。大小写、下划线、拼写差异是主因。</para>
	///   <para><b>排查价值</b>本码不指向"值错"，只宣告"这个名字对本调用不存在"；同一名字在别的算子下可能合法 [是否存在族级统一名表待实测]，所以换调用点测试比改值有效。名字过关后才轮到 3765 起的逐参数校验段。</para>
	///   <para><b>条目</b>本库无该族托管包装，全库 Grep 无本常量引用，本码为 3751-3764 段末枚，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WV_PAR_NAME = 3764;

	/// <summary>method 类型不对：算法档位这一位的槽位类型不合法（本段命名规律 WT_/WV_/WN_ 的起点）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong type of parameter method</c>。本片光/结构光标定段的码按同一规律排布：WT_ = wrong type（槽位类型错，3765~3771、3773、3783）、WV_ = wrong value（取值不合法，3772、3774~3778）、WN_ = wrong number（元素个数错，3779/3780），同一参数最多撞其中之一。</para>
	///   <para><b>语义要点</b>本码在参数包解包阶段触发，标定尚未开始；method 的合法档位清单托管层无文档 [待实测]，类型过关后才轮到 <c>Jl_ERR_SOL_WV_METHOD</c>(3772) 判取值。</para>
	///   <para><b>坑</b>托管层通用参数以元组承载且字面量可隐式转换，"类型错"多源于名-值成对拼装时错位，而不是有意传错类型；本库已删除该族标定包装（<c>JlCamPar</c>、<c>JlCalibData</c> 等类型已移除），全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WT_METHOD = 3765;

	/// <summary>ambiguity 类型不对：消歧准则这一位的槽位类型不合法（值不合法是 3774）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong type of parameter ambiguity</c>。ambiguity 是"多个候选解按什么准则取舍"的策略项，本码只判类型；类型过了才轮到 <c>Jl_ERR_SOL_WV_AMBIGUITY</c>(3774) 判取值。</para>
	///   <para><b>语义要点</b>与 3774 一起构成同一参数的两档，排查时先看消息里的 type/value 一词，能省掉一半猜测；它与 method（3765/3772）、score（3767/3775）同属一个参数包的并列槽位。</para>
	///   <para><b>坑</b>本库已删除该族标定的托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项；允许的准则值 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WT_AMBIGUITY = 3766;

	/// <summary>score 类型不对：打分准则这一位的槽位类型不合法（原生文本里参数名写作 score）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong type of parameter score</c>，而取值不合法那一档的文本写作 score_type（<c>Jl_ERR_SOL_WV_SCORE_TYPE</c>，3775）——同一参数在两条消息里名字不完全一致，按码去搜原生文本时别只搜一种写法。</para>
	///   <para><b>语义要点</b>本码只说"这一位不该是这个类型"，不指出实际给了什么；它与 <c>Jl_ERR_SOL_WT_AMBIGUITY</c>(3766)、<c>Jl_ERR_SOL_WT_METHOD</c>(3765) 是同一参数包的三个并列槽位，报第一条时其余两条尚未被检查 [待实测]。</para>
	///   <para><b>坑</b>本库无该族标定包装，仅作错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WT_SCORE_TYPE = 3767;

	/// <summary>calibration 类型不对：这条路径参数的槽位类型不合法（取值错是 3776，配套不配是 3790）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong type of parameter calibration</c>。calibration 是决定标定走哪条路径的枚举项，本码只看类型：类型过关才轮到 3776（值不合法）与 3790（值合法但与其它参数不配）。</para>
	///   <para><b>语义要点</b>三档互斥，报哪一档说明原生走到了哪一步校验，可直接当"进度指示"用。参数包由"名-值"成对拼装，类型错多半来自拼装时错位，而非有意写错类型。</para>
	///   <para><b>坑</b>本库已删除该片光/结构光标定包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WT_CALIBRATION = 3768;

	/// <summary>number_profiles 类型不对：这个计数参数该给整数，给的槽位类型不合法。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong type of parameter number_profiles</c>。该参数是"取多少条剖面/轮廓"的计数项，原生要求整数槽位；给串或给浮点都归本码，取值超范围归 <c>Jl_ERR_SOL_WV_NUM_PROF</c>(3777)。</para>
	///   <para><b>语义要点</b>整数与浮点在托管层可互相隐式转换、也能隐式转成元组，因此这类错通常不是写错类型，而是参数包组装顺序错位（把某路字符串挤进了这一位）；核对成对给出的"名-值"序列比核对单个值更有效。</para>
	///   <para><b>坑</b>本库无该族标定包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WT_NUM_PROF = 3769;

	/// <summary>camera_parameter 里的元素类型不对：内参串某个分量类型不合要求（个数错另有 3779）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong type of element in parameter camera_parameter</c>。原生逐位检查这条串，任一位不是数值即本码；"长度不对"走 <c>Jl_ERR_SOL_WN_CAM_PAR</c>(3779)，两者按消息文本一分为二。</para>
	///   <para><b>语义要点</b>本库已删除 <c>JlCamPar</c>，内参只能以裸数值串递交，分量顺序、类型、单位全靠调用方自持；手改系数文件时把某一位置成串或留空是最典型的触发方式 [待实测]。</para>
	///   <para><b>坑</b>相机类型枚举值是否受支持属 3778/3788，与本码无关；本段码只作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WT_CAM_PAR = 3770;

	/// <summary>pose 里的元素类型不对：位姿串中某个分量不是要求的数值型（长度对、类型错）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong type of element in pose</c>。措辞与标量参数不同：这里说 element，指逐元素检查复合参数内部，因此"个数不对"另有 <c>Jl_ERR_SOL_WN_POSE</c>(3780)，本码专指某一位的类型不对（数值位上混进了串/空项）。</para>
	///   <para><b>语义要点</b>托管层元组允许 int/double/string 隐式转换，编译期与装载期都不报警，只有原生按位解包时才暴露——这是本码最常见的来源。同族 <c>Jl_ERR_SOL_WT_CAM_PAR</c>(3770) 是 camera_parameter 的同一档错。</para>
	///   <para><b>坑</b>本库 <c>JlPose</c> 仍在，但本码说的是原生参数包里的数值串，不是 <c>JlPose</c> 对象；该族标定包装已删除，仅作错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WT_PAR_POSE = 3771;

	/// <summary>method 取值不合法：算法档位给了枚举之外的值（该参数的类型档是 3765）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong value of parameter method</c>。method 选的是"走哪种求解/提取策略"，值不在允许集即在参数包校验期被拒，压根没跑到几何解算。</para>
	///   <para><b>语义要点</b>命名规律：WT_（3765）管类型、WV_（本码）管取值；档位清单与 3765 同源，托管层无文档 [待实测]。换 method 往往连带改变 number_profiles、score_type 的合法集，单改一项容易从本码滑到 <c>Jl_ERR_SOL_PAR_CALIB</c>(3790) 那类"组合不配" [待实测]。</para>
	///   <para><b>坑</b>本库已删除该片光/结构光标定包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WV_METHOD = 3772;

	/// <summary>min_gray 类型不对：常量名叫 WT_THRES（阈值），原生文本点名的却是 min_gray 这一参数位。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong type of parameter min_gray</c>。按文本理解为"最小灰度"参数的类型错（该给数值给了串），名字里的 THRES 是该参数位的另一种叫法，别按名字另找一个"threshold"参数去查 [待实测]。</para>
	///   <para><b>语义要点</b>本段没有配对的 WV_MIN_GRAY/WN_MIN_GRAY，min_gray 只暴露"类型"这一档；值超界（例如负数或大于 255）是否也落本码 [待实测]。它与 <c>Jl_ERR_SOL_WV_NUM_PROF</c>(3777) 一类共同构成"提取阶段"的参数校验。</para>
	///   <para><b>坑</b>灰度阈值语义随图像类型变化（byte 与 16bit/int2 的取值域不同），传串或传错域都易撞本码；本库无该族标定包装，仅作错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WT_THRES = 3773;

	/// <summary>ambiguity 取值不合法：消解二义的准则给了枚举之外的值（类型槽位对，值不在允许集）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong value of parameter ambiguity</c>。ambiguity 决定多个候选解如何取舍（例如按刚性/按评分等策略），值不合法即在参数包校验期被拒；类型错另有 <c>Jl_ERR_SOL_WT_AMBIGUITY</c>(3766)。</para>
	///   <para><b>语义要点</b>它管"多解怎么挑"、<c>Jl_ERR_SOL_WV_SCORE_TYPE</c>(3775) 管"怎么打分"，两者常成对出现；把消歧关掉并不能绕过打分档的取值校验，反之亦然 [待实测]。</para>
	///   <para><b>坑</b>本库无该族标定包装（相关 3D/相机类型已删除），全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WV_AMBIGUITY = 3774;

	/// <summary>score_type 取值不合法：打分准则给了枚举之外的串（原生文本里把该参数写作 score）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong value of parameter score_type</c>——常量名与文本里的参数名（score_type）一致，而 3767 的文本只写 score，两条同指一个参数、分处值档与类型档。</para>
	///   <para><b>语义要点</b>打分准则影响"什么算好匹配"，与 <c>Jl_ERR_SOL_WV_AMBIGUITY</c>(3774，消歧准则) 是一对配套项：换一个准则往往要一起看另一个，只改一个容易从值错变成 3790 那类"组合不配" [待实测]。</para>
	///   <para><b>坑</b>合法准则清单托管层无文档 [待实测]；本库无该片光/光面标定包装，仅作错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WV_SCORE_TYPE = 3775;

	/// <summary>calibration 取值不合法：这个开关型/枚举型参数落在了允许档位之外（它本身合法时才会轮到 3790）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong value of parameter calibration</c>。calibration 决定标定走哪条路径、要求哪些配套参数，值不合法时校验直接停在参数包，不会进入解算。</para>
	///   <para><b>语义要点</b>三码分档：<c>Jl_ERR_SOL_WT_CALIBRATION</c>(3768) 是类型错、本码是值错、<c>Jl_ERR_SOL_PAR_CALIB</c>(3790) 是"值合法但与其余参数不配"；报 3790 时别再来查本码。</para>
	///   <para><b>坑</b>合法档位清单托管层无文档 [待实测]；本库无该片光标定包装，仅作错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WV_CALIBRATION = 3776;

	/// <summary>number_profiles 取值不合法：轮廓/剖面数量给的不是该流程允许的值（类型对、值超范围）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong value of parameter number_profiles</c>。命名规律 WV_ 指"值不合法"；该参数为整数槽位错是 <c>Jl_ERR_SOL_WT_NUM_PROF</c>(3769)，两者按消息文本分得很清。</para>
	///   <para><b>语义要点</b>这类"数量"参数通常有上下限且与图像尺寸、算法档位相关 [待实测]；给 0、给负数、给到超过采样能力的大数都归本码，原生不区分。</para>
	///   <para><b>坑</b>本库无该片光/结构光标定包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WV_NUM_PROF = 3777;

	/// <summary>相机类型取值不合法：camera 参数给了枚举之外的类型串（类型槽位本身合法，值不在允许集内）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong type of camera</c>——注意文本说的是 camera 的类型，而常量名与命名前缀 WV_ 把它定位在"取值不合法"档；该值只能是实现支持的相机类型枚举。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_SOL_CAMPAR_UNSUPPORTED</c>(3788) 分层：本码是枚举里没有这个值，3788 是值合法但所选算子没实现；也与 <c>Jl_ERR_SLM_WRONGCTYPE</c>(3964，结构光模型段的相机类型不支持) 不同族。</para>
	///   <para><b>坑</b>允许的取值清单托管层无文档 [待实测]；本库 <c>JlCamPar</c> 已删除，本段码只作错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WV_CAMERA_TYPE = 3778;

	/// <summary>camera_parameter 的值的个数不对：相机内参串的长度与该相机类型要求的分量数不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong number of values of parameter camera_parameter</c>。内参个数随相机模型而变，本码只说"长度不对"、不说差几个；类型错另有 <c>Jl_ERR_SOL_WT_CAM_PAR</c>(3770)。</para>
	///   <para><b>语义要点</b>常见于跨类型搬内参（把面阵参数给线扫、或手改过系数后长度错位）；双目/多相机时还要求每路长度一致，一路不齐就报本码 [待实测]。</para>
	///   <para><b>坑</b>本库已删除 <c>JlCamPar</c> 类型，内参只能按数值串交给原生参数包，长度与含义全靠自己维护；本码与结构光段的"路数/尺寸"码（3962/3969/3963）不是一回事。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WN_CAM_PAR = 3779;

	/// <summary>pose 的值的个数不对：这个参数位要求固定分量数，传多或传少都在解包期被拒。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong number of values of pose</c>。命名规律：WN_ 管元素个数，WT_ 管类型（本参数对应 <c>Jl_ERR_SOL_WT_PAR_POSE</c>，3771），WV_ 管取值，同一参数三档互斥。</para>
	///   <para><b>语义要点</b>位姿在这里是"参数包里的数值串"，不是本库 <c>JlPose</c> 对象；分量数由该族约定决定（六位位姿是常见约定，此处是否如此 [待实测]）。少一位与多一位都回本码，原生不指出实到几个。</para>
	///   <para><b>坑</b>托管层元组可隐式转换、长度不校验，个数错只会在原生解包时暴露；本库无该片光/结构光标定包装，仅作错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WN_POSE = 3780;

	/// <summary>找不到标定目标：图像里没检出标定用的目标（板/标记/光条交点），标定没有可用输入。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Calibration target not found</c>。这是"输入侧没料"的码，与 <c>Jl_ERR_SOL_NO_VALID_SOL</c>(3782，有料但解不出) 一先一后；先解决检出，再谈解算。</para>
	///   <para><b>处置</b>核对目标是否在视场内、是否被裁掉、曝光与对比度是否够；需要区域限定时确认域非空——空域会让检出静默回空集，随后就以本码出现 [待实测]。</para>
	///   <para><b>坑</b>目标检测参数（阈值类）本身非法另有其码（见 3773 的 min_gray 一档），别把"检不出"与"参数错"混为一谈；本库无该片光标定包装，仅作错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_NO_TARGET_FOUND = 3781;

	/// <summary>标定算不出有效解：算法跑完了但没有满足判据的解，属收敛/退化失败而不是传参错。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The calibration algorithm failed to find a valid solution.</c>。与 <c>Jl_ERR_SOL_NO_TARGET_FOUND</c>(3781，图像里找不到标定目标) 的分工很关键：3781 说明输入侧没料，本码说明料齐了但解不出来（约束不足或初值/姿态分布退化）。</para>
	///   <para><b>处置</b>加姿态、拉开视角差异、剔除质量差的图，而不是继续改参数枚举（那对应 3765~3778 的 WT_/WV_ 系列）。求解判据与阈值无托管文档 [待实测]。</para>
	///   <para><b>坑</b>同族的几何退化另有 <c>Jl_ERR_SING</c>(3850，光源位置线性相关)、<c>Jl_ERR_FEWIM</c>(3851，图像信息不足)，撞哪个取决于原生在哪一层判出来 [待实测]；本库无该片光标定包装。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_NO_VALID_SOL = 3782;

	/// <summary>calibration_object 类型不对：这个参数位上传了不合法的槽位类型，参数包解包时就拒。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong type of parameter calibration_object</c>。命名规律：WT_ = wrong type（槽位类型错），WV_ = wrong value（值不合法），WN_ = wrong number（元素个数错），三者是同一参数在不同校验阶段的不同码。</para>
	///   <para><b>语义要点</b>本码发生在解参数包阶段，标定压根没开始，因此不会与 3784（无效标定物）、3785（未设标定物）同时出现；托管侧通用参数多以元组承载，且字面量可隐式转成元组——该给对象/数值处传了字符串，正是这类"类型错"的常见来源。</para>
	///   <para><b>坑</b>本库已删除该片光/结构光标定包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WT_CALIB_OBJECT = 3783;

	/// <summary>标定物无效：calibration_object 给了，但内容不被该标定流程接受（不是没给，也不是类型槽位错）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Invalid calibration object</c>。典型是标定物与本流程不配套：点数/几何与图像里检出的目标对不上，或用了另一套标定数据里的标定物 [待实测]。</para>
	///   <para><b>语义要点</b>三档："没设"是 3785，"设了但无效"是本码，"类型不对"是 <c>Jl_ERR_SOL_WT_CALIB_OBJECT</c>(3783，原生在解参数包时就拦下，压根进不到标定)。</para>
	///   <para><b>坑</b>本码出现在解算阶段，往往与 <c>Jl_ERR_SOL_NO_TARGET_FOUND</c>(3781)、<c>Jl_ERR_SOL_NO_VALID_SOL</c>(3782) 交替出现，三者一起看才判得准是标定物问题还是图像问题；本库无该片光包装，仅作错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_INVALID_CALIB_OBJECT = 3784;

	/// <summary>没设标定物：走需要标定物的标定时，calibration_object 这一项压根没给。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>No calibration object set</c>。片光/结构光标定分"用标定物"与"不用标定物"两条路，前者必须先登记标定物（其几何与位姿），没登记就跑该路径即本码。</para>
	///   <para><b>语义要点</b>三档分清：没设（本码）、设了但内容无效（<c>Jl_ERR_SOL_INVALID_CALIB_OBJECT</c>，3784）、类型槽位不对（<c>Jl_ERR_SOL_WT_CALIB_OBJECT</c>，3783）——分别是"缺席 / 在场但坏 / 传错种类"。</para>
	///   <para><b>坑</b>与 <c>Jl_ERR_SOL_PAR_CALIB</c>(3790) 也易混：那是"参数与已选 calibration 档位不配"，本码只针对标定物本身；本库已删除该片光标定包装，仅作错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_NO_CALIB_OBJECT_SET = 3785;

	/// <summary>片光模型文件格式不合法：文件内容不是可解析的片光模型（版本问题另有 3787）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Invalid file format for sheet-of-light model</c>。按文件头/结构判族属，对不上即本码：把别的模型族文件、纯文本、写入中断的残件当片光模型读都会落到这里。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_SOL_WR_FILE_VERS</c>(3787) 的分工是"格式 vs 版本"，处置方向相反（换文件 vs 换运行时）；结构光模型段的同族码是 3958/3957，两族文件互不通用 [待实测]。</para>
	///   <para><b>坑</b>托管层没有格式嗅探接口；本库已删除片光模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WR_FILE_FORMAT = 3786;

	/// <summary>片光模型文件版本不受支持：文件认得是片光模型，但版本号超出当前运行时可读范围。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The version of the sheet-of-light model is not supported</c>，与 <c>Jl_ERR_SOL_WR_FILE_FORMAT</c>(3786) 成"版本/格式"两档；典型是高版本导出的模型给低版本运行时读。</para>
	///   <para><b>处置</b>换不低于导出侧的运行时装载，或在导出侧按兼容版本重存；别用重跑标定绕过——那会丢掉文件里已算好的参数。</para>
	///   <para><b>坑</b>片光模型族在本库已无托管包装，本码与 3786 一起仅作原生错误表保留项；受支持版本区间 [待实测]。措辞相同的结构光模型码是 <c>Jl_ERR_SLM_WRVERS</c>(3957)，按码段判族。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WR_FILE_VERS = 3787;

	/// <summary>片光标定不支持该相机类型：原生算子 calibrate_sheet_of_light_model 的相机类型实现集里没有这一项。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Camera type not supported by calibrate_sheet_of_light_model</c>——本段少数直接在文本里点出算子名的码，触发面收窄到片光标定这一条路径；本库无该算子的托管包装（相机参数类型 <c>JlCamPar</c> 亦已删除）。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_SOL_WV_CAMERA_TYPE</c>(3778) 分层：3778 是参数值本身不在枚举内，本码是枚举值合法但该算子没实现。换相机类型或改走别的标定路径才有解。</para>
	///   <para><b>坑</b>支持的类型清单无托管文档 [待实测]；结构光模型段另有措辞相同的 <c>Jl_ERR_SLM_WRONGCTYPE</c>(3964)，按码段区分。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_CAMPAR_UNSUPPORTED = 3788;

	/// <summary>参数与已设定的 calibration 不匹配：calibration 这一档定了哪些参数合法，其余参数没按它配。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Parameter does not match the set 'calibration'</c>。本码是"参数组合"层校验：calibration 取值决定要不要 camera_parameter、pose、calibration_object 等配套项，配漏或配多都会撞这里，而参数本身的类型/取值合法（那两类是 3765~3778 的 WT_/WV_ 系列）。</para>
	///   <para><b>语义要点</b>消息不带参数名，只点出"与 calibration 不配"——排查要先固定 calibration 取值，再逐项删/补；与 <c>Jl_ERR_SOL_WT_CALIBRATION</c>(3768)、<c>Jl_ERR_SOL_WV_CALIBRATION</c>(3776) 的区别正在于 calibration 自身是合法的。</para>
	///   <para><b>坑</b>各 calibration 档位分别要求哪些配套参数，托管层无文档 [待实测]；本库已删除该片光标定包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_PAR_CALIB = 3790;

	/// <summary>视差图灰度与相机高度不匹配：按视差反算高度时，视差的取值范围对不上标定给出的高度量程。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The gray values of the disparity image do not fit the height of the camera</c>。视差图以灰度编码视差，换算成高度要用相机几何；灰度整体落在该套标定给出的量程之外（视差零点偏移、图是别的标定条件下算出来的）即本码。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_SOL_UNDEF_DISPARITY</c>(3756，没给视差图)、<c>Jl_ERR_SOL_UNDEF_DISPDOMAIN</c>(3757，没给视差域) 分档：那两条是输入缺席，本码是输入在场但换算关系不自洽，属"配错一套标定"而非"忘了传"。</para>
	///   <para><b>坑</b>视差—高度换算所用参数（基线、倾角、零视差对应高度）的布局托管层无文档 [待实测]；本库已删除该片光/双目重建包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SOL_WGV_DISP = 3791;

	/// <summary>纹理检测模型类型不对：交给该入口的模型不是纹理检测模型（或内部子类型不匹配）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong texture inspection model type</c>。原生按模型内部类型标签分派，标签不对直接拒；它不指出错成了哪种类型，也不区分"给了别的族"与"同族不同子类型"。</para>
	///   <para><b>语义要点</b>3800~3809 一段里，本码是唯一的"类型错"，其余分属训练状态（3801/3802）、模型文件（3803/3804）、样本文件（3805~3808）、图像数量/尺寸（3807/3809），按消息文本分档即可。名字与 <c>Jl_ERR_SLM_WRONGMODEL</c>(3961) 只差族前缀，按码段判族。</para>
	///   <para><b>坑</b>模型句柄在本库统一走 <c>JlObjectBase</c> 的 key 通道，编译期分不开族属；本库已删除纹理检测模型族，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_TI_WRONGMODEL = 3800;

	/// <summary>纹理检测模型未训练：模型句柄有效，但没跑过训练就进入比对/检测流程。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Texture Model is not trained</c>。"创建模型"与"训练模型"是两步，只创建不训练即本码；它不抱怨数据好坏，只说训练没发生。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_TI_NOTRAINDATA</c>(3802，没有训练数据) 的区别在"数据缺"还是"流程没走"：数据齐了仍会撞 3801；反过来 3802 不必等到训练那步就报。先按消息文本定档，再决定补数据还是补调用。</para>
	///   <para><b>坑</b>训练状态在托管层没有只读属性可查（本库已删除纹理检测模型族，全库 Grep 无本常量引用），只能靠异常码判断 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_TI_NOTTRAINED = 3801;

	/// <summary>纹理检测模型没有训练数据：模型建好了却没挂上任何可用于训练的数据。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Texture Model has no training data</c>。位于"未训练"（<c>Jl_ERR_TI_NOTTRAINED</c>，3801）之前一档：本码说连喂进去的数据都没有，3801 说数据有了但没跑训练。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_NOT_ENOUGH_IMAGES</c>(3809，模型内无图像) 措辞极近，差别在"训练数据"与"图像"两层是否同指 [待实测]；实操上两者都指向"先把样本图挂全"。</para>
	///   <para><b>坑</b>本库已删除纹理检测模型族（无对应类型与训练/查找包装），全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_TI_NOTRAINDATA = 3802;

	/// <summary>纹理检测模型文件格式不合法：常量名叫 NOTRAINFILE，原生文本说的却是"模型文件格式错"。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Invalid file format for Texture inspection model</c>，而名字里的 NOTRAINFILE 容易被读成"没有训练文件"——本段并没有"模型文件不存在"这一码（"文件不存在"只在卡尔曼段由 <c>Jl_ERR_NOFILE</c>，3901 表达），按文本理解为准。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_TI_WRTRAINVERS</c>(3804) 是"格式 vs 版本"两档：本码说明文件压根不是可解析的模型；把形状模板/区域等别的族文件、半截写入的残件当模型读即撞这里。</para>
	///   <para><b>坑</b>装载前无法在托管层嗅探格式；本库已删除纹理检测模型族，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_TI_NOTRAINFILE = 3803;

	/// <summary>纹理检测模型版本不受支持：文件是这一族，但模型版本号超出当前运行时的读取范围。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The version of the Texture inspection model is not supported</c>。与 <c>Jl_ERR_TI_NOTRAINFILE</c>(3803，格式不合法) 分档：那条连族属都对不上，本码认得族属只拒版本。</para>
	///   <para><b>处置</b>换不低于导出侧的运行时装载，或用受支持的旧版本重新导出模型；样本文件若同批导出，往往还要过 3806 这一关。</para>
	///   <para><b>坑</b>本库已删除纹理检测模型族，全库 Grep 无本常量引用，仅作原生错误表保留项；受支持版本区间 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_TI_WRTRAINVERS = 3804;

	/// <summary>训练样本文件格式不对：文件内容不是可解析的样本格式（版本问题另有 3806）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong training sample file format</c>。常见情形是把别的模型族的样本、纯文本、或写入中断的残件当成纹理样本装载。</para>
	///   <para><b>语义要点</b>本码在 3805~3808 这条样本链的最前档：格式（3805）→ 版本（3806）→ 与模型是否匹配（3808），而 3807 管的是图像尺寸、不属文件链；报本码时后面几档还轮不到。</para>
	///   <para><b>坑</b>托管层没有样本文件嗅探接口，装载前判不了；本库已删除纹理检测模型族，仅作错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_TI_WRSMPFORMAT = 3805;

	/// <summary>训练样本文件版本不受支持：样本文件读得开、格式认得，但版本号超出当前运行时范围。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The version of the training sample file is not supported</c>。与 <c>Jl_ERR_TI_WRSMPFORMAT</c>(3805) 的分工是"版本 vs 格式"：3805 说文件不是这一族，本码说族对版不对，典型是高版本导出、低版本装载。</para>
	///   <para><b>处置</b>用不低于导出侧的运行时装载，或在导出侧重存为兼容版本；样本与模型成对时还要看 3804（模型版本），两条可能连撞。</para>
	///   <para><b>坑</b>本库已删除纹理检测模型族，全库 Grep 无本常量引用，仅作原生错误表保留项；受支持版本区间 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_TI_WRSMPVERS = 3806;

	/// <summary>训练图像太小：纹理模型的训练图里至少有一张尺寸不够，整批一并被拒。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>At least one of the images is too small</c>。纹理统计要按窗口在图上取块，图小于窗口就取不出样本；注意措辞是"至少一张"，消息不会指出是哪一张。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_NOT_ENOUGH_IMAGES</c>(3809，模型内一张图都没有) 分档：那张数、这张尺寸；两者都属"喂进去的图不合用"，处置不同（补图 vs 换大图或缩窗口）。</para>
	///   <para><b>处置</b>训练前逐张核对宽高（本库图像对象带宽高属性），把小图剔出集合或统一放大到窗口可容纳；一次只报一条，剔完要重跑确认。</para>
	///   <para><b>坑</b>尺寸下限由窗口/尺度参数推导，具体公式托管层无文档 [待实测]；本库已删除纹理检测模型族，仅作错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_TI_WRIMGSIZE = 3807;

	/// <summary>训练样本与当前纹理模型不匹配：样本是按另一套模型（或另一组配置）生成的，不能接着用。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The samples do not match the current texture model</c>。样本文件里带着生成它的模型侧特征，装载/续训时与传入的模型实例逐项比对，对不上即本码。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_TI_WRSMPFORMAT</c>(3805，样本文件格式错)、<c>Jl_ERR_TI_WRSMPVERS</c>(3806，样本文件版本老) 是三个不同层：本码文件读得通、版本认得，只是"不是这一份模型的样本"。多模型并流时最易串行。</para>
	///   <para><b>坑</b>改过模型参数（窗口/尺度一类）后旧样本通常一并作废 [待实测]；本库已删除纹理检测模型族（无对应类型与训练/查找包装），全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_TI_WRSMPTEXMODEL = 3808;

	/// <summary>纹理检测模型里没有图像：常量名说"图不够"，原生文本说的是"模型内一张训练图都没有"。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>No images within the texture model</c>，而常量名写作 NOT_ENOUGH_IMAGES——名字与文本口径不完全一致：判"数量不足"（本码的"零张"）与判"尺寸太小"（<c>Jl_ERR_TI_WRIMGSIZE</c>，3807）是两回事，别按名字猜。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_TI_NOTRAINDATA</c>(3802，模型没有训练数据)、<c>Jl_ERR_TI_NOTTRAINED</c>(3801，没跑训练) 构成三级：本码看模型内的图像集是否为空，3802 看训练数据是否登记，3801 看训练流程是否执行。</para>
	///   <para><b>坑</b>本库已删除纹理检测模型（无对应模型类），全库 Grep 无本常量引用，仅作原生错误表保留项；"够不够"的数量门限 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NOT_ENOUGH_IMAGES = 3809;

	/// <summary>光源位置线性相关：参与光平面/结构光标定的各个光源位姿方向退化，方程组定不出唯一解。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The light source positions are linearly dependent</c>。该族靠多组光源位置（或光平面位姿）构造约束，位置共线/重合会让约束矩阵秩不足；名字 SING 即 singular（奇异）的缩写。</para>
	///   <para><b>语义要点</b>几何退化错，不是观测数量不足（那是 <c>Jl_ERR_FEWIM</c>，3851）：把同一方向的光位再拍几张也不解，必须让方向真正岔开。与卡尔曼段的 <c>Jl_ERR_SINGU</c>(3911) 同为奇异性，但那由协方差数值病态引起，处置方向完全不同。</para>
	///   <para><b>坑</b>本库已删除光平面/结构光标定的托管包装，全库 Grep 无本常量引用，仅作原生错误表保留项 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SING = 3850;

	/// <summary>图像提供的信息不足：参与解算的图像（或其上的光条/标识）不够定出结果。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>No sufficient image indication</c>。与 <c>Jl_ERR_SING</c>(3850，光源位置线性相关)、<c>Jl_ERR_ZBR_NOS</c>(3852，求根退化) 同属光平面/结构光标定解算段：本码说的是"观测不够"，3850 说的是"观测虽多但方向退化"。</para>
	///   <para><b>语义要点</b>多解/欠定错，不是文件错，也不是类型错（那两类见 3765~3791 的 WT_/WV_/WN_ 码）。典型补法是加姿态、让光条覆盖更多区域，而不是把同一张图重复喂进去。</para>
	///   <para><b>坑</b>本库已删除该片光/光平面标定包装（无对应模型类与 <c>JlCalibData</c>/<c>JlCamPar</c>），全库 Grep 无本常量引用，仅作原生错误表保留项；判"够不够"的阈值 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_FEWIM = 3851;

	/// <summary>Brent 一维求根的内部错误：原生 JlZBrent 遇到函数值"同号/相等"的退化情形，区间内夹不住根。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Internal error: Function has equal signs in JlZBrent</c>。Brent 法要求搜索区间两端异号才收敛，本码是它自曝的退化分支（端点同号或出现等值），前缀写着 Internal error，说明不挂在某个用户入参上。</para>
	///   <para><b>语义要点</b>3850~3852 是同一族几何求解的三兄弟：光源位置线性相关（3850）、图像指示不足（3851）、一维求根退化（本码）。撞本码一般意味着上游数据本身已退化（光位或轮廓不理想），而不是求根参数要调。</para>
	///   <para><b>坑</b>本库无对应托管入口（3D/光面标定族已删除），仅作原生错误表保留项 [待实测]；同参数重试不会好转。</para>
	/// </remarks>
	public const int Jl_ERR_ZBR_NOS = 3852;

	/// <summary>卡尔曼维数未定义：维度三元组 [状态维 n, 测量维 m, 控制维 p] 里有项没给值（不是越界，是压根未定义）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Kalman: Dimension n,m or p has got a undefined value</c>。原生靠这组维数切分行主序展平的 A/C/Q/R/P 各块，某项为 UNDEF 时无从定块边界，故在装载阶段即拒。</para>
	///   <para><b>语义要点</b>触发点在参数通道而非文件：本库 <c>JlMisc.FilterKalman</c>/<c>UpdateKalman</c> 的 dimension 形参直接钉进原生（Store），没有数值校验，空元组、元素数不足 3、含未初始化项都能到这里才暴露——<c>JlTuple</c> 索引器只判下标越界，判不了"这一位是 UNDEF"。</para>
	///   <para><b>处置</b>维数应取自 <c>ReadKalman</c>（原生 id 1053）的返回值，而不是手填；随后核对矩阵块长度是否等于对应 n*m/n*n 之和。</para>
	///   <para><b>坑</b>p=0 时仍需显式给 0，缺位与给 0 语义不同（缺位撞本码，给 0 则不要求 G/u，见 3909）[待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DIMK = 3900;

	/// <summary>卡尔曼描述文件不存在：按给出的文件名找不到文件（内容错另有 3902~3904）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Kalman: File does not exist</c>。文件名为原生侧打开的字符串（本库 <c>JlMisc.ReadKalman</c> 收 fileName，默认示例 "kalman.init"，<c>UpdateKalman</c> 用 "kalman.updt"），找不到即本码。</para>
	///   <para><b>语义要点</b>相对路径按原生进程的工作目录解析，与托管层 <c>Directory.GetCurrentDirectory()</c> 是否一致 [待实测]；本码只说"没找到"，路径不可访问、权限不足是否另发码 [待实测]。</para>
	///   <para><b>坑</b>与 <c>Jl_ERR_FF1</c>~<c>Jl_ERR_FF3</c>(3902~3904) 分档清楚：先判在不在（本码），再判读不读得动；报本码时改内容没意义，先核对路径与后缀。卡尔曼族 3900~3911 由 <c>ReadKalman</c>/<c>UpdateKalman</c>/<c>FilterKalman</c>（原生 id 1053/1054/1055）触发，是本仓库唯一仍有托管入口的一段。</para>
	/// </remarks>
	public const int Jl_ERR_NOFILE = 3901;

	/// <summary>卡尔曼文件的维数行不合语法：描述文件里声明维数的那一行读不出合法项（与 3900 的"值未定义"分档）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Kalman: Error in file (row of dimension)</c>。卡尔曼描述文件先声明维数再给矩阵，本码专指"维数那一行"语法不过——项数不对、掺了非整数、被注释或换行打断；矩阵块还没开始读就失败，所以后面 3905~3909 的"缺矩阵"不会同时出现。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_DIMK</c>(3900) 的分工是"文件里的维数行"与"运行时传进来的维数值"：本码指向描述文件（走 <c>ReadKalman</c>、原生 id 1053），3900 指向参数通道（走 <c>FilterKalman</c>/<c>UpdateKalman</c>）。</para>
	///   <para><b>坑</b>与 <c>Jl_ERR_NOFILE</c>(3901) 区分：本码说明文件已打开、内容语法不合，3901 连文件都没有。</para>
	/// </remarks>
	public const int Jl_ERR_FF1 = 3902;

	/// <summary>卡尔曼文件的"行标记"不合语法：某行开头缺少本库解析器认识的标记（与维数行 FF1 分档）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Kalman: Error in file (row of marking)</c>。卡尔曼族按行解析描述文件：一行讲维数、其余行讲矩阵或向量，每行以固定标记起头指明"这一行是哪一块"。本码指标记行本身读不懂（拼错、缺项、串行），文件存在且维数行也过了，才轮到本码。</para>
	///   <para><b>何时遇到</b>手工编辑或脚本改写过描述文件（常见于只改 Q/R 数值却把标记挪了位）、换行符被改成 CR-only、或首行多了 BOM/注释垃圾 [待实测]。</para>
	///   <para><b>处置</b>回到 <c>ReadKalman</c>（原生 id 1053）可读的样板文件重做，改完先读一遍验证；标记的具体写法与词法本仓库无文档 [待实测]。</para>
	///   <para><b>坑</b>原生消息里的 "marking" 易被误读成"图像标记/标志点"，实际说的是描述文件的行标记，别按标定标记点的思路查。</para>
	/// </remarks>
	public const int Jl_ERR_FF2 = 3903;

	/// <summary>卡尔曼文件里的数值不是浮点数：某个该写成实数的位置解析不出数。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Error in file (value is no float)</c>——注意这条没有 "Kalman:" 前缀（同族 3901~3903、3905~3911 都有），按前缀批量筛消息时最易漏掉它 [待实测]。</para>
	///   <para><b>语义要点</b>本码说的是"这一位根本不成数"（空项、多余分隔符、被当成字符串的项、区域设置用逗号做小数点），与 3902/3903 的"整行结构不合语法"是两档：那两条看行，本码看单个值。</para>
	///   <para><b>处置</b>先定位到出错的那一行再看具体位；同一份文件多半只暴露第一个坏值，修一处可能连撞下一条。</para>
	/// </remarks>
	public const int Jl_ERR_FF3 = 3904;

	/// <summary>卡尔曼描述文件缺 A：状态转移矩阵没给，模型段第一项就缺席。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Kalman: Matrix A is missing in file</c>。A 是 n×n 的状态转移矩阵，是卡尔曼"预测"一步的唯一起点，原生把它当必备项，缺即本码。</para>
	///   <para><b>语义要点</b>3905~3909 是同一段的五个"齐备性"码，一矩阵一码，因此能直接指出缺哪一块；但 A 通常是文件里第一块矩阵，缺席多指向"写了别的块漏了 A"或"整块内容被改写"。</para>
	///   <para><b>处置</b>与 <c>ReadKalman</c> 可解析的样板对照补齐（n 取自维数三元组第一项，需 n*n 个元素），别照抄别的 n 的矩阵。</para>
	/// </remarks>
	public const int Jl_ERR_NO_A = 3905;

	/// <summary>卡尔曼描述文件缺 C：观测（量测）矩阵没给，状态无从映射到量测。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Kalman: In Datei fehlt Matrix C</c>——这条没改成英文，还是德语原句（"文件里缺矩阵 C"）。按英文文本匹配消息时，C 是唯一对不上"Matrix X is missing"句式的一条，别因句式不同怀疑是另一码 [待实测]。</para>
	///   <para><b>语义要点</b>C 是 m×n 的量测映射矩阵，与 A、Q 同属模型段（本库 <c>ReadKalman</c> 的 model 输出即 A、C、Q 行主序拼接）；缺它等价于"有状态没有观测"，增益无从计算。</para>
	///   <para><b>坑</b>C 的形状由维数三元组 [n,m,p] 决定（需 m*n 个元素）；元素数错时更常表现为 3900/3910 而非本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NO_C = 3906;

	/// <summary>卡尔曼描述文件缺 Q：过程噪声协方差没给，模型不确定度无从表达。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Kalman: Matrix Q is missing in file</c>。Q 是 n×n 的过程噪声协方差，与 A、C 一起构成模型段（本库 <c>JlMisc.ReadKalman</c> 的 model 输出即 A、C、Q 的行主序拼接，可选再带 G、u、L），缺一块即报对应码。</para>
	///   <para><b>语义要点</b>"缺 Q"与"Q 给成全 0"不同：后者能过校验但会让滤波器只信模型、量测权重趋零（表现为不收敛而非报本码）[待实测]。</para>
	///   <para><b>坑</b>Q 与 R 同为协方差、同受 3910 对称性检查；改文件时别只补 Q 而漏了对称化。</para>
	/// </remarks>
	public const int Jl_ERR_NO_Q = 3907;

	/// <summary>卡尔曼描述文件缺 R：测量噪声协方差矩阵没给，量测环节无从定权。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Kalman: Matrix R is missing in file</c>。R 是量测侧唯一噪声参数，本库把它单列一路装载（<c>JlMisc.ReadKalman</c> 的 measurement 输出即行主序展平的 R），文件里缺这一段就报本码。</para>
	///   <para><b>语义要点</b>R 与 A/C/Q 的分工：A 推状态、C 把状态映到量测、Q 是过程噪声、R 是量测噪声；只有 Q 没有 R 时增益算不出，因此原生把它当必备项单列。</para>
	///   <para><b>坑</b>R 必须 m×m 且对称（非对称撞 <c>Jl_ERR_NOTSYMM</c>，3910）；对角给 0 会滑向 <c>Jl_ERR_SINGU</c>(3911)。文件里缺 R 与"R 元素个数与 m 不符"是两种错，后者更常表现为维数不自洽或 3910 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NO_R = 3908;

	/// <summary>卡尔曼描述文件缺 G 或 u：模型带控制输入（第三维 p 非 0）却没给出控制矩阵 G 或控制向量 u。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Kalman: G or u is missing in file</c>。G 与 u 成对出现、只在控制维 p 非 0 时必需；<c>ReadKalman</c>（原生 id 1053）装载模型时缺一即本码，且原文把两者并提，单缺 G 与单缺 u 都落到本码 [待实测]。</para>
	///   <para><b>语义要点</b>与 A/C/Q/R 那几条的区别在于"条件必需"：p=0 时本不该出现 G/u，出现反而可能错位；p 非 0 时必须两样齐全。</para>
	///   <para><b>处置</b>先用 <c>JlMisc.ReadKalman</c> 拿回维数三元组确认 [n,m,p] 的 p 是否真非 0，再决定补 G/u 还是改回 p=0 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NO_GU = 3909;

	/// <summary>卡尔曼的协方差矩阵不对称：Q/R/P 本该对称，实际给出的数值上下三角对不上。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Kalman: Covariant matrix is not symmetric</c>。协方差矩阵按定义对称，原生在装载/校验时逐对比较上下三角，超出容差即本码——是数据形状错，不是数值大小错。</para>
	///   <para><b>语义要点</b>典型成因是把 n×n 矩阵按列主序展平（本库约定行主序展平）、元素个数凑错导致错位回卷、或手填/脚本生成时只填了半三角。与 <c>Jl_ERR_SINGU</c>(3911) 是两回事：那条查可逆性，本码查对称性。</para>
	///   <para><b>处置</b>先核维数三元组 [n,m,p] 与展平长度是否自洽，再逐块核对 A/C/Q、R、P* 的边界；对称性用镜像方式生成，别手填两侧。</para>
	///   <para><b>坑</b>原生判"对称"用的容差阈值无文档 [待实测]；描述文件里矩阵的键名与拼接次序亦无本库文档 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NOTSYMM = 3910;

	/// <summary>卡尔曼滤波的线性方程组奇异：求逆失败（P/R 病态或量测冗余），本拍递推算不出来。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Kalman: Equation system is singular</c>。递推要解形如 "(P* C^T C + R)" 的线性系统并求逆，矩阵奇异即本码，<c>FilterKalman</c>（原生 id 1055）与 <c>UpdateKalman</c>（1054）路径都可能在原生侧落此码。</para>
	///   <para><b>语义要点</b>常因 P 不随递推更新而退化、R 对角给 0（量测"零噪声"）、量测维数 m 与观测矩阵 C 秩不足。与 <c>Jl_ERR_NOTSYMM</c>(3910) 分工：3910 查对称性，本码查可逆性。</para>
	///   <para><b>处置</b>核对维数三元组 [n,m,p] 与矩阵元素个数是否自洽（n×m 矩阵需 n*m 个元素）；R 对角给非零小量再试。原生对奇异判据（条件数阈值）无文档 [待实测]。</para>
	///   <para><b>坑</b>≥1000 一律被 <c>JlNativeApi.IsFailure</c> 判真并抛 <c>JlOperatorException</c>，不会静默回坏值。卡尔曼族（3900~3911）按"维数/文件/矩阵齐备/对称性/可逆性"分档，消息文本自带线索，别拿 3900 的去猜 3911。</para>
	/// </remarks>
	public const int Jl_ERR_SINGU = 3911;

	/// <summary>结构光模型不在持久模式：想跨调用复用模型内容，但模型没开 persistent，中间态已被回收。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>structured light model is not in persistent mode</c>。SLM 是"投图案—采图—解码—重建"的多拍流程，要靠模型对象把中间态带到下一拍；不处持久模式时，这些内容不在调用间保留，后一拍再取即本码。</para>
	///   <para><b>语义要点</b>与 3955（未准备解码）、3968（未按重建解码）不同层：那两码说"该步没做"，本码说"即便做了也没跨调用留住"——排查先看开关，再看步骤。</para>
	///   <para><b>坑</b>本库已删除结构光/3D 包装（无 SLM 相关类），全库 Grep 无本常量引用，仅作原生错误表保留项；开关的托管层入口名与默认值 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_NOT_PERSISTENT = 3950;

	/// <summary>最小条纹宽相对图案幅面过大：min_stripe_width 与所选 pattern_width/pattern_height 不成比例。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>the min_stripe_width is too large for the chosen pattern_width or pattern_height</c>。图案在给定幅面内只能容下有限宽度的条纹；下限高于实际可分辨宽度时，有效条纹几乎全被"太细"判据丢弃，故建模型期直接拒。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_SLM_SSW_TOO_LARGE</c>(3952) 同一闸、不同入参（那卡单条纹宽）；本码与 3953/3954 也不同轴：3951/3952 看"参数 vs 图案尺寸"，3953/3954 看"两个宽度之间"。</para>
	///   <para><b>坑</b>改任一侧都能消码但会改变解码分辨率，不是单纯"报错要修"；本库已删除结构光模型包装，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_MSW_TOO_LARGE = 3951;

	/// <summary>单条纹宽相对图案幅面过大：single_stripe_width 撑不下所选 pattern_width/pattern_height 的条纹数。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>the single_stripe_width is too large for the chosen pattern_width or pattern_height</c>。单条纹宽是解码分辨率的"尺子"，尺子占满图案幅面就编不出可区分的条纹；改小 single_stripe_width 或加大 pattern_width/pattern_height 才是解。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_SLM_MSW_TOO_LARGE</c>(3951) 是同一尺寸闸的两个入参（3951 卡 min、本码卡 single），原生先验幅面、再验两宽关系（3953/3954）的次序代码里判不出 [待实测]。</para>
	///   <para><b>坑</b>本库已删除结构光模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_SSW_TOO_LARGE = 3952;

	/// <summary>最小条纹宽不小于单条纹宽：min_stripe_width 未小于 single_stripe_width，下限压在目标值之上。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>min_stripe_width has to be smaller than single_stripe_width</c>，即要求 min 严格小于 single。相等是否放行、反向是否落到 3954 [待实测]。</para>
	///   <para><b>语义要点</b>本码带"必须如此"的祈使句，说明它是硬约束校验而非统计判据——与 3951/3952 同属建模型期参数自洽闸；闸序（图案尺寸先判还是两宽关系先判）代码里看不出 [待实测]。</para>
	///   <para><b>坑</b>3953/3954 是同一条不等式的两面，一次配置最多撞其中之一；本库无结构光包装，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_MSW_GT_SSW = 3953;

	/// <summary>单条纹宽小于最小条纹宽下限：single_stripe_width 比 min_stripe_width 还小，检测下限永远够不着。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>single_stripe_width is too small for min_stripe_width</c>。<c>min_stripe_width</c> 是"允许的最小条纹宽"（低于此判为噪声/边缘退化而丢弃），<c>single_stripe_width</c> 是单条条纹的目标宽度；后者小于前者，条纹会被自己的下限吃掉，故直接拒。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_SLM_MSW_GT_SSW</c>(3953) 是同一约束的两面：3953 的原文说 min 必须小于 single，本码的原文说 single 对 min 太小——方向一致、只是从两侧表述，实测落到哪个码取决于原生在哪一入口判 [待实测]。与 3952（single 相对图案太大）是"太小 vs 太大"的另一轴。</para>
	///   <para><b>处置</b>优先调低 min_stripe_width 或调高 single_stripe_width，二者拉开余量。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_SSW_LT_MSW = 3954;

	/// <summary>结构光模型未准备解码：还没做准备（或已采齐待解）就去取解码/重建结果。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The SLM is not prepared for decoding</c>。本码在解码链最前档："准备"都没做，谈不上"按某用途解码"；后续档依次是未解码（3960/3968）、未配垂直解码（3967）、模型里查不到对象（3956）。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_SLM_NOT_PERSISTENT</c>(3950) 区分：3950 是"内容没跨调用保住"（准备过，但状态没延续），本码是"这一步压根没做"，处置方向不同。</para>
	///   <para><b>坑</b>本库已删除结构光/3D 包装，全库 Grep 无本常量引用，仅作原生错误表保留项；托管层看不到"准备"标志，只能按异常码回推 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_NOT_PREP = 3955;

	/// <summary>结构光模型里没有要查的对象：模型内部的对象集未登记所请求的那一项。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The SLM does not contain the queried object</c>。SLM 会随流程积累若干内部对象（解码/重建产物等），按标识去取而模型里没有这一项即本码。</para>
	///   <para><b>语义要点</b>别与全局对象库那层混：key 认不出/已删除属 4050~4064 段（<c>Jl_ERR_DBWOID</c>、<c>Jl_ERR_DBOC</c> 等），说的是"这个句柄在全局库里无效"；本码说的是"句柄有效，但这个 SLM 里没有它"，多半是查错了模型实例或前置步骤没产出。</para>
	///   <para><b>坑</b>未跑解码/重建就取结果时最易见，此时更常见的搭档是 3955/3960/3968；本库无结构光包装，仅作错误表保留项 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_NO_OBJS = 3956;

	/// <summary>结构光模型文件版本不受支持：文件认得是本族，但版本号超出当前运行时的读取范围。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The version of the structured light model is not supported</c>。与 3958 相反，本码说明族属判定已通过、卡的是版本字段，典型是高版本导出的文件给低版本运行时读。</para>
	///   <para><b>处置</b>换用不低于导出侧的运行时，或在导出侧以兼容版本重存；重跑建模流程也能绕过，但会丢掉文件里已标定的参数。</para>
	///   <para><b>坑</b>本库已删除结构光模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项；受支持的版本区间托管层无文档 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_WRVERS = 3957;

	/// <summary>结构光模型文件格式不合法：文件内容不是可解析的模型序列化格式（版本问题另有 3957）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Invalid file format for a structured light model</c>。读档按文件头/结构判定族属，结构对不上（拿别的模型族文件、纯文本、写到一半的残件）即本码。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_SLM_WRVERS</c>(3957) 的分工是"格式 vs 版本"：格式错说明压根不是这一族文件，版本错说明认得族属但版本号超出支持集；两条的处置方向完全不同（前者换文件，后者换运行时/重导出）。</para>
	///   <para><b>坑</b>托管层无格式嗅探接口，读之前判不了；SOL 段有同族码 <c>Jl_ERR_SOL_WR_FILE_FORMAT</c>(3786) 与 <c>Jl_ERR_SOL_WR_FILE_VERS</c>(3787)，措辞几乎一样但说的是片光模型，按码段区分。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_WRFILE = 3958;

	/// <summary>图案类型不对：给出的结构光图案类型不在该解码路径的支持集内，或与模型已定的图案类型不一致。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong pattern type</c>。结构光靠投射图案编码/解相，图案类型是枚举项；类型不认识或与建模时确定的类型不符，解码无从进行，即报本码。</para>
	///   <para><b>语义要点</b>与条纹宽度类（3951~3954）不同层：那几码是数值间的比例关系，本码是类型枚举层面的不匹配；与"未准备/未解码"（3955/3960/3968）也不同，那些不质疑类型。</para>
	///   <para><b>坑</b>合法图案类型清单托管层无文档（相关 3D 类型族已删除）[待实测]；改图案类型通常意味着重建模型，不能只换入参复用旧句柄。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_WRONGPATTERN = 3959;

	/// <summary>结构光模型未按偏折测量用途解码：要取镜面/反射面的偏折结果，解码却走了别的支路或没走。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The SLM is not decoded for deflectometry</c>。偏折测量（deflectometry）面向镜面反射面的斜率/法向，与"重建"取物体坐标是两条解码支路，取结果时支路不匹配即本码。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_SLM_NOT_DEC_REC</c>(3968) 成对、方向相反；更前置的一档是"未准备"（3955），取向不匹配另有 3967。四码措辞相近，差别全在"用途/阶段"一词，据此定位卡在哪一步。</para>
	///   <para><b>坑</b>同一批采图换用途必须重解码，不能拿重建的解码结果要偏折输出；本库无结构光包装，仅作错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_NOT_DECODED = 3960;

	/// <summary>模型类型不对：交给结构光入口的不是结构光模型，类型对不上号。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong model type</c>。原生按模型内部的类型标签分派实现，标签与入口要求不符即本码；它不区分"具体错成哪种类型"，只说对不上。</para>
	///   <para><b>语义要点</b>本段（3950~3969）里"类型"与"版本/格式"分档：文件版本不认识是 <c>Jl_ERR_SLM_WRVERS</c>(3957)、文件根本不可解析是 <c>Jl_ERR_SLM_WRFILE</c>(3958)、图案类型不对是 <c>Jl_ERR_SLM_WRONGPATTERN</c>(3959)，本码专指模型本身类型错。名字与 <c>Jl_ERR_TI_WRONGMODEL</c>(3800) 只差所属族，按码段判族。</para>
	///   <para><b>处置</b>核对模型句柄的来源与类型（多模型并存、读文件后混用时最易串行）；托管层没有类型自省接口，只能从异常携带的码反推。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_WRONGMODEL = 3961;

	/// <summary>双目结构光要求 csm 带两路相机参数：只给一路（或路数不为 2）即报本码。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The csm has to contain two camera parameters</c>。csm 即相机标定模型（<c>Jl_ERR_SLM_NO_CSM</c>，3966 里同一缩写）；双目结构光重建需要两路内参才能三角化，本码把"路数"写死为 2。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_SOL_WN_CAM_PAR</c>(3779) 语义相近但层级不同：那是通用参数 <c>camera_parameter</c> 的元素个数不对，本码专指双目 csm 的相机个数。压根没挂 csm 报 3966，挂了但数量不足报本码。</para>
	///   <para><b>坑</b>两路尺寸还得一致（见 3969/3963 一族"一致性"码），补齐路数后可能接着撞尺寸码，属预期的第二道闸 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_WNUMCAMS = 3962;

	/// <summary>投影仪尺寸不一致：模型各处记录的投影幅面/分辨率互相对不上。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Inconsistent projector size</c>。投影尺寸要同时参与图案幅面、条纹计数与像素—投影坐标映射，模型里几处各存一份；两处给的值不等即报本码，与 <c>Jl_ERR_SLM_WCAMSIZE</c>(3969，相机尺寸不一致）成对。</para>
	///   <para><b>语义要点</b>一致性错，不是范围错：单个尺寸合法但彼此不等也算本码；尺寸与条纹宽度的比例关系另有 3951~3954 四个码管。</para>
	///   <para><b>坑</b>本库已删除结构光/3D 包装，全库 Grep 无本常量引用，仅作原生错误表保留项；尺寸究竟在哪些接口上重复登记 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_WPATTSIZE = 3963;

	/// <summary>相机类型不受支持：结构光解码路径不认采集侧给出的相机类型枚举值。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Camera type not supported</c>。SLM 要求采集设备类型在实现支持的枚举内（面积/线扫等），类型不认识或本解码路径未实现即报本码；投影侧的对偶码是 <c>Jl_ERR_SLM_WRONGPTYPE</c>(3965)。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_SOL_WV_CAMERA_TYPE</c>(3778) 同措辞不同族：那属结构光标定（SOL 3765~3791）段的相机类型值非法，本码属结构光模型（3950~3969）段的能力清单不覆盖，排查方向一个是取值、一个是实现范围。</para>
	///   <para><b>坑</b>类型不支持不等于尺寸不一致（3963/3969 才是尺寸对不上）；支持的相机类型清单托管层无文档 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_WRONGCTYPE = 3964;

	/// <summary>投影仪类型不受支持：结构光解码路径不认这个投影设备类型枚举值。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Projector type not supported</c>。SLM 是"相机+投影"双光路，两侧各有类型枚举；本码专指投影侧，与 <c>Jl_ERR_SLM_WRONGCTYPE</c>(3964) 成对。</para>
	///   <para><b>语义要点</b>能力/兼容性错，不是取值写错：枚举值本身可能合法，只是本实现没接。换设备型号或换解码路径才有解，调 min/single_stripe_width（3951~3954）与它无关。</para>
	///   <para><b>坑</b>原生支持的投影类型清单在托管层没有对应文档（相关 3D/相机类型已删除），只给本码无法反推合法值 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_WRONGPTYPE = 3965;

	/// <summary>结构光模型里没有挂相机标定模型（csm）：要走投影/换算的流程发现 csm 槽位是空的。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The SLM does not contain a csm</c>。csm 即相机（标定）模型，同族 <c>Jl_ERR_SLM_WNUMCAMS</c>(3962) 里出现的是同一缩写。SLM 本身只承载图案与解码信息，需要内参/位姿参与换算时得另建一份 csm 交进来，没交就是本码。</para>
	///   <para><b>语义要点</b>装配缺失，不是 csm 内容非法：模型类型不对见 3961、相机类型不受支持见 3964、路数不对见 3962；本码说的是"根本没有"。</para>
	///   <para><b>坑</b>本库已删除 <c>JlCamPar</c>、标定数据等 3D/标定类型族，全库 Grep 无本常量引用，仅作原生错误表保留项；"设 csm"的入口名 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_NO_CSM = 3966;

	/// <summary>结构光模型没配"垂直方向解码"：请求竖直取向的条纹解码，模型却按水平取向（或根本没设取向）准备。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The SLM is not set for vertical decoding</c>。条纹解码沿取向分横/竖两路，取向在建模型或准备阶段就定下，取结果时走另一路即撞本码。</para>
	///   <para><b>语义要点</b>与 3960/3968 同类（前置流程没走通），差别在维度：那是"用途"（偏折测量、重建）没解码，本码是"条纹取向"没配置。换图案序列或重建模型才管用，同参数重跑不会好转。</para>
	///   <para><b>坑</b>本库已删除结构光/3D 包装，全库 Grep 无本常量引用，仅作原生错误表保留项；取向究竟由图案序列决定还是由模型设置项决定，托管层无文档 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_NO_VERT = 3967;

	/// <summary>结构光模型未按"重建"用途解码：要求交出物体坐标（重建结果），而前置解码步骤没做或已失效。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The SLM is not decoded for reconstruction</c>。SLM（结构光模型）族是"投图案—采图—解码—重建"两拍式流程，解码那一拍按用途分支路：重建（物体坐标/高度）、偏折测量、垂直方向解码各用各的结果，本码专指"重建支路没解码"。</para>
	///   <para><b>语义要点</b>流程状态错，不是参数值错。同族 <c>Jl_ERR_SLM_NOT_DECODED</c>(3960) 是偏折测量用途未解码、<c>Jl_ERR_SLM_NO_VERT</c>(3967) 是垂直方向解码未设置、<c>Jl_ERR_SLM_NOT_PREP</c>(3955) 是更早的"未准备"一档，四者指向前置流程各停在哪一步。</para>
	///   <para><b>处置</b>按重建用途重跑解码，别复用只按偏折测量解码过的模型；换过图案序列、相机或投影尺寸之后解码结果作废，须重解码。persistent 开关只决定模型跨调用是否保留内容（见 <c>Jl_ERR_SLM_NOT_PERSISTENT</c>，3950），不代替解码。</para>
	///   <para><b>坑</b>≥1000 一律被 <c>JlNativeApi.IsFailure</c> 判真并抛 <c>JlOperatorException</c>，托管层没有"解码状态"这种只读属性，只能从异常携带的错误码倒推。本库已删除结构光/3D 包装，全库 Grep 无本常量引用，仅作原生错误表保留项；解码步骤在托管层的对应接口名 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_NOT_DEC_REC = 3968;

	/// <summary>相机尺寸不一致：双目结构光两路相机（或模型多处登记）的图像尺寸互相对不上。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Inconsistent camera size</c>。与 <c>Jl_ERR_SLM_WNUMCAMS</c>(3962) 衔接：路数凑够两路之后，两路幅面还得一致才能做三角化；尺寸在模型中多处各存一份，任两处不等即报本码。投影器侧的对偶码是 <c>Jl_ERR_SLM_WPATTSIZE</c>(3963)。</para>
	///   <para><b>语义要点</b>一致性错，不是范围错：单个尺寸本身合法但彼此不等也是本码；没挂标定模型报 3966、路数不为 2 报 3962，本码排在它们之后作第二道闸。</para>
	///   <para><b>坑</b>本库已删除结构光/3D 包装，全库 Grep 无本常量引用，仅作原生错误表保留项；尺寸究竟在哪些接口上重复登记 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SLM_WCAMSIZE = 3969;

	/// <summary>给进来的是对象元组，而该入口只接受单个对象。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>上游 <c>Connection()</c>、<c>Threshold()</c> + 轮廓生成一类算子回了多元素元组，直接当单输入传下去；或写了只处理"一个对象"的循环外调用。C# 侧形参类型同为 <c>JlObject</c> 系，编译期无提示。</para>
	///   <para><b>语义要点</b>这是"类别错"（多对一），不是"ID 无效"。反向错是 <c>Jl_ERR_DBTIO</c>(4055)：元组位上给了单个对象。两个码都发生在托管层无法静态区分、由原生按对象库里的实际条目类型判定的场合。</para>
	///   <para><b>处置</b>明确二选一：要逐个处理就 <c>CountObj()</c> 拿数量后按 1 基索引 <c>SelectObj(int)</c> 取单个（注意上游顺序不稳定时会静默错取）；要整体处理就换接受元组的算子重载（<c>JlTuple</c>/多对象版本）。</para>
	///   <para><b>同族对照</b>4050~4064 属对象数据库段：元组本身的失效见 4053/4054，索引问题见 4063/4064。</para>
	/// </remarks>
	public const int Jl_ERR_DBOIT = 4050;

	/// <summary>对象已被删除：这个 key 曾经有效，指向的对象已经不在对象库里。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>图标对象（<c>JlImage</c>/<c>JlRegion</c>/<c>JlXLD</c> 及各模型类）被 <c>Dispose()</c> 或终结器回收后，仍有旧引用或外部缓存的裸 key 参与算子。托管侧 <c>Dispose(bool)</c> 只在 key 非 UNDEF 时才 <c>ClearObject</c>，一次释放就把内容摘掉了，而"不等 key 变 UNDEF 内容就已经被清掉"的情况（外部通道先清）也照样落到本码。</para>
	///   <para><b>语义要点</b>生命周期错。与 <c>Jl_ERR_DBWOID</c>(4052) 的区别是原生"认得"这个 key，只因为它已被注销。</para>
	///   <para><b>坑</b>终结器兜底不等于可控：批量新建的图标对象若没显式 <c>Dispose</c>，可能在下一帧算子调用前就被 GC 收掉，表现为"昨天还好、今天随机 4051"。别指望 <c>IsInitialized()</c> 拦住它——图标对象那版只比托管字段，只有 <c>JlHandleBase</c> 的同名方法才会转调原生查真有效性。</para>
	///   <para><b>同族对照</b>元组版 <c>Jl_ERR_DBTC</c>(4053)；图像 <c>Jl_ERR_DBIC</c>(4058)；区域 <c>Jl_ERR_DBRC</c>(4060)。</para>
	/// </remarks>
	public const int Jl_ERR_DBOC = 4051;

	/// <summary>对象 ID 不对：这个 key 在本进程当前的对象库里认不出来。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>互操作或序列化通道里把裸 key 传错、跨运行/跨进程复用旧值，或把别的类型（句柄族 <c>JlHandleBase</c> 的 mHandle、算子 id）当图标 key 递交——本库图标对象走 <c>JlObjectBase.key</c>，句柄对象走 <c>JlHandleBase</c>，两套通道数值上没法区分。</para>
	///   <para><b>语义要点</b>比"已删除"更弱的判断：4051 说明"确实登记过、刚被清掉"，本码说明"压根对不上任何条目"。ID 为 0 另有专门的 <c>Jl_ERR_DBIDNULL</c>(4056)，超出取值域另有 <c>Jl_ERR_WDBID</c>(4057)，可见原生对 key 的失效方式分了档，回到的码能直接指出方向。</para>
	///   <para><b>处置</b>停止手传裸值：让 <c>JlNativeApi.Store</c> 从实例装载 key；确实要外传就用 <c>CopyKey()</c> 拿到受引用计数保护的一份，用完自己释放。</para>
	///   <para><b>同族对照</b>元组版见 <c>Jl_ERR_DBWTID</c>(4054)；对象已删除见 <c>Jl_ERR_DBOC</c>(4051)。</para>
	/// </remarks>
	public const int Jl_ERR_DBWOID = 4052;

	/// <summary>对象元组已被删除：整个元组容器在原生侧已回收，还在按它取元素。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>把 <c>Connection()</c>/<c>SelectObj</c> 等产出的元组 <c>Dispose()</c> 之后继续用；或元组先释放、再由另一处拿它缓存过的索引去取值。注意本码说的是"元组这一层容器"没了，不是某个元素单独被删。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_DBWTID</c>(4054) 的区别：4054 是"这个元组 ID 我不认"，本码是"我认得它，但它已经删了"。与单个对象版的 <c>Jl_ERR_DBOC</c>(4051) 是同一层含义、不同粒度。</para>
	///   <para><b>坑</b>元组释放不会让已经取出的元素句柄失效——<c>SelectObj</c> 返回的是独立新句柄，各自管各自的生命周期；反过来，只释放元组而留着元素是合法态，只释放元素却遍历元组就会撞上更细的码。混用时先想清楚手里这个变量指向的是容器还是元素。</para>
	///   <para><b>同族对照</b>类别反向错见 4050/4055；索引语义见 4063/4064。</para>
	/// </remarks>
	public const int Jl_ERR_DBTC = 4053;

	/// <summary>对象元组的 ID 不对：给出的元组标识在当前对象库里对不上号（原文拼作 tupel）。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>按索引访问对象元组的元素时，携带的元组标识无效：元组是别处的/上一次的、被 <c>Dispose()</c> 后又被引用，或互操作时只把"某个元素的 key"当"元组的 key"传了过来。托管层里 <c>SelectObj</c>（原生 id 572）这类按位取元素的入口最容易撞见。</para>
	///   <para><b>语义要点</b>这是单个对象 ID 错（<c>Jl_ERR_DBWOID</c>，4052）的元组版本；它说的是"这个元组标识我不认"，而不是"索引越界"（那是 4063）或"索引未定义"（4064）。三个码的排查方向完全不同，别凭 572/577 的调用现场猜。</para>
	///   <para><b>处置</b>确认元组实例本身活着：先 <c>CountObj()</c>（原生 id 577，只读、不产生新句柄）验证元组可用并拿回元素数，再据此索引；跨作用域共享元组用 <c>CopyKey()</c> 多取一份引用。</para>
	///   <para><b>同族对照</b>元组已被删除见 <c>Jl_ERR_DBTC</c>(4053)；元组/对象类别搞反见 4050 与 4055。</para>
	/// </remarks>
	public const int Jl_ERR_DBWTID = 4054;

	/// <summary>期待单个对象、实际给了对象元组：类型类别对不上（元组被当成对象）。</summary>
	/// <remarks>
	///   <para><b>触发场景</b><c>Connection()</c>、分割、<c>SelectObj</c> 一类接口会产出含多元素的对象元组，把它整个喂给只接受"一个对象"的入口就触发本码。托管层里 <c>JlObject</c> 与其派生的元组容器共用同一 key 通道，编译期看不出"这是一个还是 n 个"。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_DBOIT</c>(4050) 成对：4050 是"元组位上给了单个对象"，本码是"单个对象位上给了元组"。两者都是形参类别错，与 ID 有效性无关（4052/4056/4057 那一类）。元素数为 1 的元组仍算元组，不会因为"只有一个"而通过。</para>
	///   <para><b>处置</b>先用 <c>CountObj()</c> 看实际元素数，再用 <c>SelectObj(int)</c> 或索引器（1 基）按位取单个对象；反之要把单个对象升成元组，用拼接/收拢类接口，而不是指望原生宽容。</para>
	///   <para><b>同族对照</b>元组本身的 ID 问题见 <c>Jl_ERR_DBWTID</c>(4054)、元组被删见 <c>Jl_ERR_DBTC</c>(4053)。</para>
	/// </remarks>
	public const int Jl_ERR_DBTIO = 4055;

	/// <summary>对象 ID 为空（0）：把未初始化或已释放的空壳对象交给了算子。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>托管层最常见有两种：一是 <c>new JlImage()</c>/<c>new JlRegion()</c> 这类空壳还没被输出装载就送去当输入；二是刚被 <c>Dispose()</c> 过的实例又被复用——释放路径把 key 写回 <c>JlObjectBase.UNDEF</c>（即 <c>IntPtr.Zero</c>），于是传给原生的就是 0。用 <c>IntPtr</c> 裸构造包装时也常把 0 直接填进去。</para>
	///   <para><b>语义要点</b>0 在本库是双重身份：既是"空"，也是 <c>JlData</c>/基类 <c>Load</c> 要求的目标态（装载输出前必须为 UNDEF，否则抛 "Undisposed object instance when loading output parameter"）。所以"空壳能当接收位"不等于"空壳能当输入"。</para>
	///   <para><b>处置</b>送进算子前判一下来源：本库各类型都提供 <c>IsInitialized()</c>（只比 key 与 UNDEF，零原生开销），可用来拦住明显的空壳；确需空对象占位时用 <c>GenEmptyObj()</c> 一类接口生成真句柄，而不是拿 0 顶。</para>
	///   <para><b>同族对照</b>ID 非空但越界见 <c>Jl_ERR_WDBID</c>(4057)，ID 错认见 <c>Jl_ERR_DBWOID</c>(4052)。</para>
	/// </remarks>
	public const int Jl_ERR_DBIDNULL = 4056;

	/// <summary>对象 ID 落在合法区间之外：这个编号根本不可能是对象库里的有效标识。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>把非 ID 的整数当对象 ID 传（例如误传了数组下标、算子 id、或另一个进程/另一次运行里的编号），或对 ID 做了算术（加一、乘二、按索引累加）后越出分配范围。与"曾经有效现在失效"的 4051/4058 不同，本码说明这个数从来就没进过当前对象库的取值域。</para>
	///   <para><b>语义要点</b>范围/合法性错，不是生命周期错；对象 ID 由原生分配，调用方不应自行构造或推算。托管层正常路径拿不到裸 ID——<c>Store</c> 走的是实例的 key，所以本码多半来自互操作或反序列化后的脏数据 [待实测：ID 取值上界由原生决定，托管侧无数值校验]。</para>
	///   <para><b>同族对照</b>4050~4064 里"下标越界"另有其人：索引过大是 <c>Jl_ERR_DBITL</c>(4063)、索引未定义是 <c>Jl_ERR_DBIUNDEF</c>(4064)，它们针对 <c>select_obj</c>（原生 id 572）的序号，序号从 1 起算；本码针对对象 ID 本身。</para>
	/// </remarks>
	public const int Jl_ERR_WDBID = 4057;

	/// <summary>访问一张已被删除的图像：key 认得是图像，但它在原生侧已被回收。</summary>
	/// <remarks>
	///   <para><b>触发场景</b><c>JlImage.Dispose()</c>（或终结器先跑了一步）之后继续用旧引用/旧裸 key 参与算子；典型是"函数返回了图像、调用方又 Dispose 了同一份"，以及 <c>using</c> 块外还留着从它拷出来的裸值。图标对象的释放路径会把 key 复位成 UNDEF，但副本里的数值不会。</para>
	///   <para><b>语义要点</b>生命周期错，不是参数值错：与 <c>Jl_ERR_DBWIID</c>(4059) 的区别就在"这个 key 曾经就是这张图"。也别和 <c>Jl_ERR_DBIDNULL</c>(4056) 混——那是压根传了 0 的空壳。</para>
	///   <para><b>处置</b>把图像的生命周期与算子调用对齐：需要跨作用域存活就 <c>Clone()</c>（内容独立副本，各自 Dispose）或 <c>CopyKey()</c>（只多要一份引用）。<c>IsInitialized()</c> 只比托管字段，key 被外部清掉时它仍报"已初始化"，不能据此判定图像还在。</para>
	///   <para><b>同族对照</b>区域版见 <c>Jl_ERR_DBRC</c>(4060)；对象元组被删见 <c>Jl_ERR_DBTC</c>(4053)。</para>
	/// </remarks>
	public const int Jl_ERR_DBIC = 4058;

	/// <summary>用错误的 key 访问图像：交给图像入口的 key 不对应任何现存图像对象。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>把区域、轮廓或模型的裸 key 当图像 key 传；或缓存了某张图的 key，原图 <c>Dispose()</c> 后该编号被原生回收甚至复派给别人。托管层 <c>JlImage</c> 与 <c>JlRegion</c> 同走 <c>JlObjectBase</c> 的 key 通道，编译期分不开，错了只能在运行时暴露。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_DBIC</c>(4058) 区分：4058 是"这张图确实被删了"，本码是"这个 key 不是这张图的标识"。若传的是未初始化空壳（key 为 0），命中的是 <c>Jl_ERR_DBIDNULL</c>(4056) 而不是本码。</para>
	///   <para><b>处置</b>只传类型化的 JlImage 实例，让 <c>Store</c> 去装载 key；跨边界必须传裸值时先 <c>CopyKey()</c> 取得属于自己的一份引用，用完释放。</para>
	///   <para><b>同族对照</b>区域的同款错法见 <c>Jl_ERR_DBWRID</c>(4061)；图像通道号取错是另一个码 <c>Jl_ERR_WCHAN</c>(4062)，别混为一谈。</para>
	/// </remarks>
	public const int Jl_ERR_DBWIID = 4059;

	/// <summary>访问一个已被删除的区域：key 仍是区域库登记过的那块区域，但它已被回收。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>区域实例被 <c>Dispose()</c>（或被显式清空）后，仍拿它的旧引用/旧裸 key 去参与算子；或在多线程下由另一处先释放了同一块区域。托管侧 <c>Dispose(bool)</c> 会把 key 复位成 UNDEF，而外部持有的裸值不会跟着变 0，于是"看起来还在、原生已没有"。</para>
	///   <para><b>语义要点</b>本码是生命周期错，不是类型错：key 认得出来是区域，只是内容已经不在了。与 <c>Jl_ERR_DBWRID</c>(4061) 相反，4061 是 key 本身对不上。与 <c>Jl_ERR_DBIDNULL</c>(4056) 也不同：4056 是压根传了 0。</para>
	///   <para><b>处置</b>先确认释放点，把所有权收敛到一处：谁 <c>LoadNew</c>/谁拿到新句柄谁 <c>Dispose</c>，共享用 <c>CopyKey</c> 或 <c>Clone()</c> 另取一份引用。别把 <c>IsInitialized()</c> 当"还活着"的证据——图标对象那套只比托管字段，不查原生。</para>
	///   <para><b>同族对照</b>图像的对应码是 <c>Jl_ERR_DBIC</c>(4058)；本段（4050~4064）其余条目按对象元组/索引/通道划分。</para>
	/// </remarks>
	public const int Jl_ERR_DBRC = 4060;

	/// <summary>用错误的 key 访问区域：原生侧按这个 key 查不到"属于现存区域对象"的条目。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>把别的对象类型（图像、轮廓）的裸 key 交给只收区域的入口；或把 <c>JlRegion.Dispose()</c> 之后残留的旧 key 数值再传进去。<c>JlObjectBase.Key</c> 是纯字段转发，读它不会续命，原件一释放那个数就成悬垂值，但看着仍像有效句柄。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_DBRC</c>(4060) 的分工：4060 说"这块区域确实已被删除"，本码说"这个 key 根本不是（或已不再是）该区域的标识"，二者都算 key 类错，排查方向不同——前者查生命周期，后者查传参是否串行。</para>
	///   <para><b>处置</b>不要把裸 key 跨调用、跨语言边界缓存；正常路径只传 JlRegion 实例，由 <c>JlNativeApi.Store</c> 装载。本码属 ≥1000 的错误族，<c>JlNativeApi.IsFailure</c> 判为失败并抛 <c>JlOperatorException</c>，消息里带原生文本加触发算子的逻辑名。</para>
	///   <para><b>同族对照</b>属 4050~4064 对象数据库段：图像的同款错法见 <c>Jl_ERR_DBWIID</c>(4059)，已删除区域见 4060，key 为 0（空壳）见 4056，索引语义见 4063/4064。</para>
	/// </remarks>
	public const int Jl_ERR_DBWRID = 4061;

	/// <summary>图像通道号非法：按通道存取图像时给出的通道值不在该图像的合法通道范围内。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong value for image channel</c>。按同段 <c>Jl_ERR_DBITL</c>(4063) 记明的 1 基索引约定，通道号从 1 起、上界由图像自身通道数决定；给 0、超过通道数或在单通道图上请求第 2 通道都撞本码 [待实测：边界判定由原生执行]。</para>
	///   <para><b>语义要点</b>属 4050~4064 对象数据库段：key 类错（4058~4061）问"图像对象本身对不对"，索引类错（4063/4064）问"序号定没定义、超没超"，本码只管通道这一维的取值。</para>
	///   <para><b>坑</b>写死通道号最易埋雷：在灰度图上"碰巧能跑"的常量通道号，换到多通道图或反之都会撞上本码/越界码；应取图像实际通道数再索引。</para>
	/// </remarks>
	public const int Jl_ERR_WCHAN = 4062;

	/// <summary>索引过大：请求的下标超出了对象集合当前有效元素的数量。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>对元组化对象或图像通道按序号取元素时，序号 ≥ 元素总数。典型是在 Connection/分割之后按固定序号取值，但上游实际输出的连通块数量比预期少，序号即越界。</para>
	///   <para><b>语义要点</b>是范围错误：索引"已定义"但太大。与 Jl_ERR_DBIUNDEF(4064,索引未定义) 相反。上游算子输出顺序/数量不稳定会让本码静默出现——应先取实际数量（如 CountObj 类语义）再据此索引。</para>
	///   <para><b>同族对照</b>属 4050~4064 对象数据库段：未定义索引 4064、通道非法 4062、对象 ID 越界 4057。</para>
	/// </remarks>
	public const int Jl_ERR_DBITL = 4063;

	/// <summary>对象库里请求的索引（下标）未被定义，取不到对应对象。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>按索引访问对象集合中某元素时，该索引从未被登记/初始化，属"未定义"而非"越界"。与 Jl_ERR_DBITL(4063,索引过大) 区分：本码是索引本身没有有效定义。</para>
	///   <para><b>语义要点</b>多出现在对元组化对象（object tuple）按序号取元素、而该序号未落在全局对象数据库已登记范围内时。需先确认上游算子确实产出了该位置的元素。</para>
	///   <para><b>同族对照</b>属 4050~4064 对象数据库段：越界见 4063、通道非法见 4062、区域/图像键错见 4061/4059。索引相关坑详见 Jl_ERR_DBITL。</para>
	/// </remarks>
	public const int Jl_ERR_DBIUNDEF = 4064;

	/// <summary>目标机器上没有可用的 OpenCL 运行库（ICD/驱动未安装或版本不满足），加速后端无法初始化。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>尝试加载 OpenCL 运行库或创建平台时失败：系统缺少 opencl.dll / libOpenCL、驱动过旧、或以 32/64 位不匹配方式加载。与 Jl_ERR_NO_COMPUTE_DEVICES(4102) 不同：本码指"库都不在"，后者指"库在但枚举不到设备"。</para>
	///   <para><b>语义要点</b>属部署环境问题，程序逻辑无须改动，装对驱动/运行库即可消除；重试不会好转。</para>
	///   <para><b>同族对照</b>4100~4105 OpenCL 段的第一环，其后依次为通用错误 4101、无设备 4102、无设备实现 4103、显存不足 4104、形状非法 4105。CUDA 后端对等见 Jl_ERR_CUDA_ERROR(4200)。</para>
	/// </remarks>
	public const int Jl_ERR_NO_OPENCL = 4100;

	/// <summary>OpenCL 层返回了未归类到具体情形的通用错误。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>原生侧某次 OpenCL API 调用失败，但错误性质不属于 4100/4102/4103/4104/4105 已细分的情形（如上下文创建、命令队列异常、内核执行返回非零码等），统一回落为本码。</para>
	///   <para><b>语义要点</b>是个兜底码，信息量低：光凭它无法定位是驱动、内核还是运行期数据问题，需结合原生日志或具体算子上下文判断。运行库完全缺失应报 Jl_ERR_NO_OPENCL(4100)。</para>
	///   <para><b>同族对照</b>同属 4100~4105 OpenCL 段。CUDA 侧的同类兜底见 Jl_ERR_CUDA_ERROR(4200)、cuDNN 见 Jl_ERR_CUDNN_ERROR(4201)、cuBLAS 见 Jl_ERR_CUBLAS_ERROR(4202)。</para>
	/// </remarks>
	public const int Jl_ERR_OPENCL_ERROR = 4101;

	/// <summary>系统中没有任何可用的计算设备（GPU/CPU OpenCL 平台），OpenCL 加速无从启动。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>枚举 OpenCL 平台/设备返回空集合时抛出：驱动未装、加速器被禁用、或运行环境根本没有 OpenCL 平台。区别于 Jl_ERR_NO_OPENCL(4100)——后者指运行库缺失，本库指库在但枚举不到设备。</para>
	///   <para><b>语义要点</b>属于环境前置条件不满足，重试同一算子仍会失败；要么装/启用设备驱动，要么改走纯 CPU 算法路径。设备在位但某参数无内核实现时报 Jl_ERR_NO_DEVICE_IMPL(4103)。</para>
	///   <para><b>同族对照</b>同属 4100~4105 OpenCL 段：运行库缺失 4100、通用错误 4101、无设备实现 4103、显存不足 4104、分块形状非法 4105。CUDA 侧对等概念见 Jl_ERR_CUDA_ERROR(4200)。</para>
	/// </remarks>
	public const int Jl_ERR_NO_COMPUTE_DEVICES = 4102;

	/// <summary>该参数（或该参数组合）在计算设备侧没有对应实现，只能退回 CPU 执行。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>把某算子的运算派发到 OpenCL 设备时，当前参数集（图像类型、位深、核函数变体等）没有编译进设备端内核，装载该核失败即返回本码。</para>
	///   <para><b>语义要点</b>不代表出错，而是"该配置无加速实现"：设备在位且可用（否则报 Jl_ERR_NO_COMPUTE_DEVICES），只是没为这一参数写内核。此时应改走 CPU 路径或调整参数，而非按硬故障处理。</para>
	///   <para><b>同族对照</b>同属 4100~4105 OpenCL 段；设备完全缺失见 Jl_ERR_NO_COMPUTE_DEVICES(4102)，运行库缺失见 Jl_ERR_NO_OPENCL(4100)，显存不足见 Jl_ERR_OUT_OF_DEVICE_MEM(4104)。[待实测] 本库是否自动回落到 CPU 由上层策略决定，常量本身不含此信息。</para>
	/// </remarks>
	public const int Jl_ERR_NO_DEVICE_IMPL = 4103;

	/// <summary>计算设备（GPU/加速卡）显存不足，无法为本次运算分配所需缓冲区。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>OpenCL 路径下申请设备内存失败：输入图像过大、一次性送入的元组/区域数量过多、或多算子并发把显存占满时，分配调用返回本码。</para>
	///   <para><b>语义要点</b>指设备侧显存耗尽，不是主机内存不足；重试同一作业不会好转，需缩小输入、降低并发或释放上一批未在用的设备对象。与 Jl_ERR_INVALID_SHAPE(4105) 区别：本码是容量问题，后者是参数形状非法。</para>
	///   <para><b>同族对照</b>同属 4100~4105 OpenCL 段；无可用设备见 Jl_ERR_NO_COMPUTE_DEVICES(4102)，参数无设备实现见 Jl_ERR_NO_DEVICE_IMPL(4103)，通用 OpenCL 错误见 Jl_ERR_OPENCL_ERROR(4101)。[待实测] 本库未在托管层显式回收设备缓冲，触发点由原生算子决定。</para>
	/// </remarks>
	public const int Jl_ERR_OUT_OF_DEVICE_MEM = 4104;

	/// <summary>工作组（work group）形状非法，OpenCL 核函数无法按给定的局部规模启动。</summary>
	/// <remarks>
	///   <para><b>触发场景</b>计算设备上以 OpenCL 核函数并行处理图像时，提交的局部工作规模（local work size / NDRange 分块形状）与该核的编译属性或设备能力不匹配，装载或入队即返回本码。</para>
	///   <para><b>语义要点</b>这是执行期参数校验错误，不是设备缺失：设备可用（否则报 Jl_ERR_NO_COMPUTE_DEVICES），但其要求的分块维度组合不被接受。常见于把一个算子的手调 tile 尺寸搬到另一 GPU 或另一核函数上。</para>
	///   <para><b>同族对照</b>同属 4100~4105 OpenCL 段：缺运行库见 Jl_ERR_NO_OPENCL(4100)，通用错误见 Jl_ERR_OPENCL_ERROR(4101)，无可用设备见 Jl_ERR_NO_COMPUTE_DEVICES(4102)，参数无设备实现见 Jl_ERR_NO_DEVICE_IMPL(4103)，显存不足见 Jl_ERR_OUT_OF_DEVICE_MEM(4104)。本码专指"分块形状"这一维度的非法。[待实测] 具体由哪个内部算子抛出未在本库源码中固定。</para>
	/// </remarks>
	public const int Jl_ERR_INVALID_SHAPE = 4105;

	/// <summary>计算设备无效：给出的设备标识（句柄/序号）不在当前平台的设备表里。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Invalid compute device</c>。4100~4106 OpenCL 段的末位：设备"有没有"由 4102 管、"某参数有无内核实现"由 4103 管、"分块形状"由 4105 管，本码专指"点名了某个设备，但该设备标识本身无效"——典型是设备枚举变动（拔卡、换平台）后仍持旧设备引用。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_NO_COMPUTE_DEVICES</c>(4102) 分档：4102 是一个都枚举不到，本码是有设备而给的那个不对；处置是重枚举平台再选设备，与增加设备无关。</para>
	///   <para><b>子系统状态</b>托管层没有显式选设备的入口，全库 Grep 无本常量引用，仅作原生错误表保留项；设备标识的承载形式 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_INVALID_DEVICE = 4106;

	/// <summary>CUDA 运行时通用错误：原生 CUDA 路径调用失败且未归入具体档的兜底码。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>CUDA error occurred</c>。CUDA 上下文创建、内存拷贝、核函数启动等运行时层调用出错，且不属于 4203~4207 已细分情形时统一回落本码；信息量低，需结合原生日志定位。</para>
	///   <para><b>语义要点</b>4200~4207 为 CUDA 段：4201/4202 分别下沉到 cuDNN/cuBLAS 库层，4204/4207 管可用性与驱动，4205/4206 管库版本与功能档；同措辞的 OpenCL 兜底见 <c>Jl_ERR_OPENCL_ERROR</c>(4101)，两族码不互通，先按号段认后端。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层不走 CUDA，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_CUDA_ERROR = 4200;

	/// <summary>cuDNN 报错：CUDA 深度神经网络库调用失败的通用兜底码。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>cuDNN error occurred</c>。卷积等深度学习核心算子在 CUDA 路径经 cuDNN 执行，其调用返回错误且不属于版本不配（4205）/功能不支持（4206）两档时归本码；常见诱因包括显存耗尽与描述符同一代设备档不符 [待实测]。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_CUDA_ERROR</c>(4200) 区分：4200 是 CUDA 运行时层，本码是 cuDNN 库层；深度网络执行期更常落在本码，排查从网络/张量配置入手而非驱动。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层不经 cuDNN，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_CUDNN_ERROR = 4201;

	/// <summary>cuBLAS 报错：CUDA 线性代数库（矩阵运算层）调用失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>cuBLAS error occurred</c>。cuBLAS 承担 CUDA 上的矩阵乘、分解等运算，全连接层计算、张量重排类操作经它执行；调用返回错误（含显存申请失败等库内原因）回落本码，具体错误类别不回传 [待实测]。</para>
	///   <para><b>语义要点</b>CUDA 段三个库各配一个兜底码：核心运行时 <c>Jl_ERR_CUDA_ERROR</c>(4200)、深度网络库 cuDNN 4201、线性代数库 cuBLAS 本码——由码就能定位到哪一层库出的错，不必逐层复现。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层不经 cuBLAS，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_CUBLAS_ERROR = 4202;

	/// <summary>不支持设置 batch_size：在当前 CUDA 运行时下请求设定深度学习批大小参数。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Set batch_size not supported</c>。批大小可否显式设置取决于运行时档：有的 CUDA 配置只支持固定批尺寸，改设批大小即撞本码；哪些配置允许设置 [待实测]。</para>
	///   <para><b>语义要点</b>参数能力错，不是值域错：与 <c>Jl_ERR_CUDNN_FEATURE_NOT_SUPPORTED</c>(4206) 同属"该配置不提供此功能"，处置是换受支持的运行时或接受默认批尺寸，而不是换个数值重试。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层没有批大小设点，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_BATCH_SIZE_NOT_SUPPORTED = 4203;

	/// <summary>CUDA 实现不可用：当前运行时里没有可装载的 CUDA 后端（未编入、缺运行库或无 NVIDIA 设备）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>CUDA implementations not available</c>。选择 CUDA 运行时但实现整体缺席时报本码，属"这条路径根本不存在"，不是"路径里某一步失败"。</para>
	///   <para><b>语义要点</b>OpenCL 段的对等码是 <c>Jl_ERR_NO_OPENCL</c>(4100)（运行库缺失）；CUDA 侧的细档另见驱动过旧 4207、运行时兜底 4200——先判"整条路没有"还是"有路但走坏了"，再决定装环境还是查日志。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层无运行时选择入口，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_CUDA_NOT_AVAILABLE = 4204;

	/// <summary>cuDNN 版本不受支持：装载到的 cuDNN 库版本不在原生深度学习 CUDA 路径接受的区间内。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Unsupported version of cuDNN</c>。初始化 CUDA 深度学习路径时按版本区间校验所加载的 cuDNN，过新或过旧都归本码；接受的版本区间 [待实测]。</para>
	///   <para><b>语义要点</b>加载期版本闸，与运行期能力错 <c>Jl_ERR_CUDNN_FEATURE_NOT_SUPPORTED</c>(4206)、库内运行错 <c>Jl_ERR_CUDNN_ERROR</c>(4201) 三档分明，分别指向"换库版本、换网络/算法配置、查库运行环境"。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层不加载 cuDNN，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_CUDNN_UNSUPPORTED_VERSION = 4205;

	/// <summary>cuDNN 不支持所请求的功能：用到的算子/算法特性在当前 cuDNN 版本里没有实现。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Requested feature not supported by cuDNN</c>。cuDNN 各版本能力集不同，某网络层或算法模式在该版本没有对应实现即报本码；与版本"没通过加载校验"（<c>Jl_ERR_CUDNN_UNSUPPORTED_VERSION</c>，4205）不同，本码是运行期能力清单不覆盖。</para>
	///   <para><b>语义要点</b>处置是升 cuDNN 或改网络/算法选择；重跑无效，换 GPU 型号一般也无效（除非实现档随卡不同）[待实测]。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层不经 cuDNN 选算法，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_CUDNN_FEATURE_NOT_SUPPORTED = 4206;

	/// <summary>CUDA 驱动过旧：显卡驱动版本低于原生 CUDA 路径要求的最低版本。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>CUDA driver is out-of-date</c>。校验点在驱动层（不是运行库或工具库层），驱动低于要求时 CUDA 路径整体不可用；要求的最低版本号 [待实测]。</para>
	///   <para><b>语义要点</b>与相邻两码分清：<c>Jl_ERR_CUDA_NOT_AVAILABLE</c>(4204) 是 CUDA 实现整体不可用（未编入/缺库），<c>Jl_ERR_CUDNN_UNSUPPORTED_VERSION</c>(4205) 是 cuDNN 库版本不配，本码专指显卡驱动版本——三条的处置分别是装实现、换库、升驱动。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层无从初始化 CUDA，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_CUDA_DRIVER_VERSION = 4207;

	/// <summary>所选运行时不支持训练：用只做推理的运行时去请求训练/微调流程。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Training is unsupported with the selected runtime</c>。深度学习运行时分"仅推理"与"可训练"两档能力；本码指当前选定的运行时缺训练档，与硬件好坏、训练参数对错无关。</para>
	///   <para><b>语义要点</b>换运行时（或换带训练能力的发行版）才有解，同配置重试不会好转。与 <c>Jl_ERR_CPU_INFERENCE_NOT_AVAILABLE</c>(4302) 同段不同维度：4302 管推理路径缺失，本码管训练路径缺失。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层没有训练入口可触发本码，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_TRAINING_UNSUPPORTED = 4301;

	/// <summary>本平台不支持 CPU 推理：运行时里没有编入（或该平台上不可用）纯 CPU 的深度学习推理路径。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>CPU based inference is not supported on this platform</c>。属平台能力错：与 <c>Jl_ERR_CUDA_NOT_AVAILABLE</c>(4204) 同为"某后端整条路径不可用"，只是本码指 CPU 档；处置是换推理后端或换平台，而非调参数。</para>
	///   <para><b>语义要点</b>发生在运行时选择期而非推理执行期：CPU 推理可用但库内出错会报 <c>Jl_ERR_DNNL_ERROR</c>(4303)，两码排查方向不同。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层无从选推理后端，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_CPU_INFERENCE_NOT_AVAILABLE = 4302;

	/// <summary>DNNL 库报错：深度学习计算走 CPU 内核库（oneDNN 一类）时的通用错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Error occurred in DNNL library</c>。DNNL 即深度神经网络 CPU 计算库；本码是该库调用失败的兜底包装，不区分内存申请失败、内核不支持等具体原因 [待实测]。</para>
	///   <para><b>语义要点</b>4301~4303 是 CPU 推理小段，三码按层次分档：4301 管"所选运行时不能训练"、<c>Jl_ERR_CPU_INFERENCE_NOT_AVAILABLE</c>(4302) 管"本平台没有 CPU 推理路径"、本码管"CPU 推理跑起来了但库里出错"——能力错与运行错别混查。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层不经该库，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_DNNL_ERROR = 4303;

	/// <summary>AI 加速卡接口（HAI2）通用错误：走该加速层失败但原因未细分的兜底码。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>AI Accelerator Interface error occurred</c>。HAI2 抽象层调用失败时先尝试归入具体档（如 <c>Jl_ERR_HAI2_INVALID_PARAM</c>，4321 参数非法），归不进的回落本码；光凭本码无法区分是驱动、设备还是接口版本问题。</para>
	///   <para><b>语义要点</b>4320~4321 一族两档（兜底 + 参数），与 OpenCL 段（4100 缺库、4101 兜底）、CUDA 段（4204 不可用、4200 兜底）的分层写法一致；排查按"先看具体档、看不到再查环境"的顺序。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层无入口可触达该加速接口，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_HAI2_ERROR = 4320;

	/// <summary>AI 加速卡接口（HAI2）参数非法：传给该加速接口层的某个参数不合法。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Invalid parameter for AI Accelerator Interface</c>。HAI2 是把各家 AI 加速设备收拢成统一入口的接口层称谓；本码指"参数校验没过"，与 <c>Jl_ERR_HAI2_ERROR</c>(4320) 的"接口自身出错"分档：一个查传参、一个查运行。是哪个参数、合法域为何 [待实测]。</para>
	///   <para><b>语义要点</b>能力/传参错，重试同参必复现；CUDA 段对参数与能力错另立 4203/4206 等档，本段只有兜底 + 参数两档。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层无入口触达该接口，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_HAI2_INVALID_PARAM = 4321;

	/// <summary>ACL 后端报错：深度学习推理走 ACL 加速路径时返回的通用错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>ACL error occurred</c>。ACL 一般指昇腾加速卡的计算语言库（Ascend Computing Language）；本码是该后端调用链的兜底包装，不携带具体失败原因，本库中该缩写的确切指向 [待实测]。</para>
	///   <para><b>语义要点</b>4400 独占一段：与 CUDA 段（4200~4207）、CPU/DNNL 段（4301~4303）、HAI2 段（4320~4321）并列，各族后端码互不通用；同措辞的兜底码 CUDA 见 <c>Jl_ERR_CUDA_ERROR</c>(4200)，由码即可定位到出错后端。</para>
	///   <para><b>子系统状态</b>本库深度学习 <c>JlDl*</c> 包装已删除，托管层无从选择该后端，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_ACL_ERROR = 4400;

	/// <summary>可视化层内部错误：显示/着色子系统自身的兜底故障码，不细分原因。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Internal visualization error</c>。可视化路径上未归入 4501（类型不符）、4502（条数超限）等具体档的失败统一回落本码，信息量低，需结合原生侧上下文排查。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_OPENCL_ERROR</c>(4101) 同属"段内兜底码"写法；显示段 5100~5124 各有具体职责，4500~4502 这一小段才承载"可视化"内部错，别把它当窗口错排查。</para>
	///   <para><b>子系统状态</b>本库显示/绘制族已删除，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_VISUALIZATION = 4500;

	/// <summary>颜色类型不符预期：给可视化的颜色参数形态（名/数值/分量元组）与该入口要求不一致。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Unexpected color type</c>。颜色可按名字串、单数值或 rgb 分量元组给出，入口只收其中某些形态，形态不对即报本码——是"类别错"不是"取值错"。</para>
	///   <para><b>语义要点</b>取值域错另有 <c>Jl_ERR_WGV</c>(5108)/<c>Jl_ERR_WPV</c>(5109)，颜色名不认识另有 <c>Jl_ERR_UCOL</c>(5105)，本码专指给的形式就不对；4500~4502 小段中 4500 为兜底、4502 管条目数。</para>
	///   <para><b>子系统状态</b>本库显示/可视化包装已删除，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_COLOR_TYPE_UNEXP = 4501;

	/// <summary>颜色设置项数量超限：一次登记的颜色条目数超过可视化层允许的上限。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Number of color settings exceeded</c>。可视化层登记颜色映射时按条目数设闸（调色板/色表容量），传入条数超容量即报本码；上限数值 [待实测]。</para>
	///   <para><b>语义要点</b>4500~4502 是可视化小段：4500 兜底、<c>Jl_ERR_COLOR_TYPE_UNEXP</c>(4501) 管颜色形态不对、本码管条数超册；显示段 5112（颜色表 LUT 非法）查的是表内容合法性，与本码的"数数"粒度不同。</para>
	///   <para><b>子系统状态</b>本库显示/绘制族已删除，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_NUM_COLOR_EXCEEDED = 4502;

	/// <summary>窗口编号非法：给出的逻辑窗口号不在当前已开窗口的编号表里。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong (logical) window number</c>。显示层以"逻辑窗口号"路由绘制/交互请求，编号从未开过或已被关闭都回本码；括号里的 logical 强调它是显示层自管的编号，不是操作系统窗口句柄。</para>
	///   <para><b>语义要点</b>显示段 5100~5124 的入口码："一个窗口都没有"报 <c>Jl_ERR_NWO</c>(5106)，"有窗口但这个编号不对"报本码。图标对象的 key（<c>JlObjectBase</c> 通道）与窗口编号互不相干，别拿对象 key 当窗口号传。</para>
	///   <para><b>子系统状态</b>本库已删除 <c>JlWindow</c>、Disp*/Draw* 族，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_WSCN = 5100;

	/// <summary>开窗失败：窗口创建动作本身出错（区别于参数非法与配额到顶）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Error while opening the window</c>。请求开窗时底层图形资源（设备上下文、画布句柄等）创建失败即回本码，是开窗族不细分原因的兜底故障码；具体失败诱因不回传 [待实测]。</para>
	///   <para><b>语义要点</b>同族按失败原因分档：编号/坐标给错见 <c>Jl_ERR_WSCN</c>(5100)/<c>Jl_ERR_WWC</c>(5102)，窗口数到顶见 <c>Jl_ERR_NWA</c>(5103)，本码指环境层面的创建失败——改参数无用，需查图形子系统状态。</para>
	///   <para><b>子系统状态</b>本库已删除 <c>JlWindow</c> 窗口族，托管层无开窗入口，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_DSCO = 5101;

	/// <summary>窗口坐标非法：给定的窗口位置落在不允许的区域（负值、越出屏幕或与父窗不匹配）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong window coordinates</c>。开窗/移窗时按屏幕坐标校验行列位置，为负、越出桌面或被请求的区域不可放置即报本码。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_WDEXT</c>(5122，窗口扩展尺寸非法) 分档：本码偏"位置坐标"、5122 偏"宽高幅面"，一条只挪位、一条只改大小会分别撞上；编号给错是 <c>Jl_ERR_WSCN</c>(5100)，别拿坐标错去查编号。</para>
	///   <para><b>子系统状态</b>本库已删除窗口族，托管层无设窗口坐标入口，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_WWC = 5102;

	/// <summary>不能再开新窗口：打开窗口的请求超过实现允许的同存窗口数量上限。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>It is not possible to open another window</c>。原生对同进程可存活的窗口数设有上限，到顶后再请求开窗即报本码；上限数值托管层无文档 [待实测]。</para>
	///   <para><b>语义要点</b>资源配额错，与参数合法性无关：编号给错是 <c>Jl_ERR_WSCN</c>(5100)、开窗动作本身失败是 <c>Jl_ERR_DSCO</c>(5101)，本码专指"数量到顶"。处置是先关旧窗口或复用现有窗口，而不是改坐标/编号参数。</para>
	///   <para><b>子系统状态</b>本库已删除 <c>JlWindow</c> 窗口族，托管层无开窗入口，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_NWA = 5103;

	/// <summary>设备或算子不可用：请求的设备通道/算子在当前运行时里没有可装载的实现。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Device resp. operator not available</c>（resp. 为 respectively 的缩写）。文本把"设备"与"算子"并列：要么访问的设备类资源（显示、采集等通道）无实现，要么调用的算子未编进当前内核；仅凭错误码分不清是哪一支 [待实测]。</para>
	///   <para><b>语义要点</b>能力缺失错，与参数取值、编号合法性无关：<c>Jl_ERR_NO_COMPUTE_DEVICES</c>(4102) 管计算设备枚举为空，本码管设备/算子在装载层面就没有；重试同一入口不会好转。</para>
	///   <para><b>子系统状态</b>本库已删除显示族与 framegrabber 采集族，托管层触发面收窄，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_DNA = 5104;

	/// <summary>颜色名不认识：按字符串名给色时，该名称不在原生内建颜色表里。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Unknown color</c>。按名着色入口在原生颜色表里查名，拼写不符或该实现未收录此名即报本码；以数值/元组形态给色则不经这张表，撞的是 <c>Jl_ERR_COLOR_TYPE_UNEXP</c>(4501)/<c>Jl_ERR_WGV</c>(5108) 一档 [待实测：分派细节]。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_WCUR</c>(5111) 同型（字符串资源名查表失败）；显示段 5114 表示颜色码按用途另立一档。</para>
	///   <para><b>子系统状态</b>本库显示/绘制族已删除，托管层无设色入口，全库 Grep 无本常量引用，仅作原生错误表保留项；内建颜色名清单 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_UCOL = 5105;

	/// <summary>没有为目标操作打开过窗口：要绘制/要交互的窗口根本不存在（区别于窗口编号给错）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>No window has been opened for desired action</c>。绘制与交互类动作按"当前有无可用窗口"受理，窗口从未打开或该用途没有对应窗口时回本码。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_WSCN</c>(5100) 分档：5100 是"给了编号但编号不在表中"，本码是"根本没有窗口可谈编号"；开窗动作失败另见 <c>Jl_ERR_DSCO</c>(5101)，数量到顶见 <c>Jl_ERR_NWA</c>(5103)。处置都是先开窗，差别在撞码说明缺的是哪一环。</para>
	///   <para><b>子系统状态</b>本库已删除 <c>JlWindow</c> 窗口族，托管层无从开窗口，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_NWO = 5106;

	/// <summary>区域填充模式非法：绘制区域时给的填充取向项不在受支持取值内（实心/仅边界一类取向选择写错）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong filling mode for regions</c>。原生以字符串状态项登记"区域画成实心还是只画轮廓"，取值不识别即报本码；合法取值清单托管层无文档 [待实测]。</para>
	///   <para><b>语义要点</b>属显示段"绘制状态字符串项"一档：<c>Jl_ERR_UCOL</c>(5105)、<c>Jl_ERR_WCUR</c>(5111) 同为名字查表失败，本码专指填充取向这一项；数值项越界另见 5108~5110。</para>
	///   <para><b>子系统状态</b>本库显示/绘制族已删除，托管层无设填充模式入口，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_WFM = 5107;

	/// <summary>灰度值非法：绘制用灰度值不在 0~255 闭区间内（原生文本把合法域直接写作 0..255）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong gray value (0..255)</c>。灰度绘制值按 byte 域校验，负值或大于 255 即报本码；这是显示段里少数把合法域直接写进错误文本的码。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_WPV</c>(5109) 相邻：本码是固定 0..255 的灰度档，5109 是随图像类型变域的像素档；给颜色名不识别则是 <c>Jl_ERR_UCOL</c>(5105)，三条按"定域数值/变域数值/字符串名"分开。</para>
	///   <para><b>子系统状态</b>本库显示/绘制族已删除，托管层无设灰度色入口，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_WGV = 5108;

	/// <summary>像素值非法：以"像素值"形式给出的绘制色超出目标图像类型对应的合法取值域。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong pixel value</c>。像素值按目标图像的类型定域（byte 图为 0~255，更宽类型域更大），给负数或超域值即报本码；合法域随图像类型变。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_WGV</c>(5108) 的分工：5108 是原生文本把域写死为 0..255 的灰度档，本码是随类型变域的通用像素档；两码各自挂在哪个入口 [待实测]。颜色名不识别则报 <c>Jl_ERR_UCOL</c>(5105)。</para>
	///   <para><b>子系统状态</b>本库显示/绘制族已删除，托管层无设像素色入口，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_WPV = 5109;

	/// <summary>线宽非法：绘制线型要素时给出的线宽超出实现允许的范围。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong line width</c>。线宽是全局绘制状态项，给出 0、负值或超过允许上限即报本码；允许区间与上限数值 [待实测]。</para>
	///   <para><b>语义要点</b>属 5100~5124 显示段里"绘制状态参数"一档：同段 <c>Jl_ERR_WGV</c>(5108) 管灰度值、<c>Jl_ERR_WFM</c>(5107) 管填充模式，本码只管线宽这一维；它校验的是数值合法性，与颜色类字符串项无关。</para>
	///   <para><b>子系统状态</b>本库显示/绘制族已删除，托管层没有设线宽的入口，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	/// </remarks>
	public const int Jl_ERR_WLW = 5110;

	/// <summary>光标名非法：向系统光标项写入的光标名称不在合法光标名清单内。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong name of cursor</c>。光标按字符串名寻址（箭头、十字等样式），名称拼写不符或当前平台没有该光标都回本码，原生不区分"写错"与"不存在"。</para>
	///   <para><b>语义要点</b>与 <c>Jl_ERR_UCOL</c>(5105) 同型：都是"字符串资源名在原生查表失败"，5105 查颜色名表、本码查光标名表；参数数值越界属别的码。</para>
	///   <para><b>子系统状态</b>本库已删除 <c>JlWindow</c> 与 Disp*/Draw* 显示族，托管层没有可设置光标的入口，全库 Grep 无本常量引用，仅作原生错误表保留项；合法光标名清单 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WCUR = 5111;

	/// <summary>颜色表（LUT）非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>所提供的颜色表索引、条目数或内容不符合当前窗口/图像的要求。</para>
	///   <para><b>处理建议</b>构造与图像类型/位深匹配且条目数合法的颜色表。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WLUT = 5112;

	/// <summary>显示/绘制表示模式（如 fill/margin 等）非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>绘制区域时选择的表示模式取值不受支持。与 <see cref="Jl_ERR_WRDS"/> 同为“表示模式”类，触发上下文不同 [待实测]。</para>
	///   <para><b>处理建议</b>改用该操作列出的合法表示模式。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WDM = 5113;

	/// <summary>表示颜色（representation color）非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>用于区域/轮廓可视化着色的颜色取值不被接受。</para>
	///   <para><b>处理建议</b>改用受支持的颜色名或合法颜色编码。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WRCO = 5114;

	/// <summary>抖动（dither）矩阵非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>用于半色调/抖动显示的矩阵取值不被支持或维度不合法。</para>
	///   <para><b>处理建议</b>使用库支持的抖动矩阵类型/尺寸 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WRDM = 5115;

	/// <summary>图像变换类型/参数非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>请求的图像变换模式不在受支持之列，或与其余参数不自洽。</para>
	///   <para><b>处理建议</b>改用受支持的变换模式并核对参数组合。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WRIT = 5116;

	/// <summary>图像类型不适合所请求的图像变换。</summary>
	/// <remarks>
	///   <para><b>含义</b>某图像变换只接受特定像素类型/通道数，而输入图像类型不满足前提。</para>
	///   <para><b>处理建议</b>先转换到该变换支持的图像类型再做变换。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_IPIT = 5117;

	/// <summary>图像变换的缩放因子非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>对图像做缩放变换时给出的 zoom 因子为 0、负值或超出允许范围。</para>
	///   <para><b>处理建议</b>使用正且落在允许区间内的缩放因子 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WRZS = 5118;

	/// <summary>表示（representation）模式非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>窗口/绘制表示模式选项给出未被支持的值。与 <see cref="Jl_ERR_WDM"/> 同属“表示模式”类，但触发该码的具体模式集合不同 [待实测]。</para>
	///   <para><b>处理建议</b>改用该操作允许的表示模式取值。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WRDS = 5119;

	/// <summary>设备代码（device code）非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>操作所需的图形设备以不受支持的设备代码给出。</para>
	///   <para><b>处理建议</b>使用当前平台与窗口配置支持的设备代码。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WRDV = 5120;

	/// <summary>父窗口编号非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>创建子窗口时指定的父窗口（father window）编号不存在或不可作为父窗口。</para>
	///   <para><b>处理建议</b>先确认父窗口已打开，并使用其有效编号。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WWINF = 5121;

	/// <summary>窗口尺寸（扩展范围）非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>给定的窗口宽高越界、非正，或与内容区不匹配。</para>
	///   <para><b>处理建议</b>使用正的、屏幕/父窗口允许范围内的宽高。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WDEXT = 5122;

	/// <summary>窗口类型不正确。</summary>
	/// <remarks>
	///   <para><b>含义</b>操作要求特定窗口类型（如图像窗口/绘图窗口），但目标窗口类型不符。</para>
	///   <para><b>处理建议</b>以所需类型重新打开窗口，或改在类型匹配的窗口上操作。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WWT = 5123;

	/// <summary>尚未设置当前窗口。</summary>
	/// <remarks>
	///   <para><b>含义</b>需要在“当前窗口”上执行的操作被调用，但进程还没有把任何窗口设为当前。</para>
	///   <para><b>处理建议</b>先打开窗口并将其设为当前，再调用依赖当前窗口的操作。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WND = 5124;

	/// <summary>RGB 颜色分量组合或取值范围非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>指定的 RGB 分量中有值越界（如非 0..255）或组合方式不受支持 [待实测]。</para>
	///   <para><b>处理建议</b>将各分量约束到合法范围并使用受支持的颜色组合。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WRGB = 5125;

	/// <summary>所设置的像素数目与请求不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>批量写像素时给定的像素个数与实际数据量不匹配。</para>
	///   <para><b>处理建议</b>核对像素数量与提供的坐标/灰度数组长度一致。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WPNS = 5126;

	/// <summary>comprise 参数取值非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>控制区域/像素是否按“包含边界”方式处理的 comprise 选项给出不受支持的取值。</para>
	///   <para><b>处理建议</b>使用文档允许的 comprise 取值 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WCM = 5127;

	/// <summary>set_fix 的参数组合非法：1/4 图像分辨率级别与 static 模式不能同时使用。</summary>
	/// <remarks>
	///   <para><b>含义</b>固定窗口显示设置中，取 1/4 图像级别时静态（static）显示不被允许。</para>
	///   <para><b>处理建议</b>放弃其一：改用更高分辨率级别或去掉 static 选项。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_FNA = 5128;

	/// <summary>在子窗口中调用 set_lut 无效。</summary>
	/// <remarks>
	///   <para><b>含义</b>颜色表（LUT）只能设在根/图像窗口上，对派生的子窗口设置 LUT 被拒。</para>
	///   <para><b>处理建议</b>把 LUT 应用到其父窗口，而非子窗口。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_LNFS = 5129;

	/// <summary>并发使用的颜色表（LUT）数量超过允许上限。</summary>
	/// <remarks>
	///   <para><b>含义</b>同时挂起的颜色表数目达到系统上限，无法再分配新的颜色表。</para>
	///   <para><b>处理建议</b>复用已有颜色表或释放不再使用的窗口后重试（具体上限本库未暴露 [待实测]）。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_LOFL = 5130;

	/// <summary>窗口截图所用设备（dump 目标设备）不正确。</summary>
	/// <remarks>
	///   <para><b>含义</b>dump 操作要求特定的输出设备，但给出的设备代码不受支持。</para>
	///   <para><b>处理建议</b>选用当前平台支持的 dump 设备类型。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WIDT = 5131;

	/// <summary>用于窗口截图（dump）的窗口尺寸与目标不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>抓取窗口图像时，所给尺寸与实际窗口尺寸不匹配。</para>
	///   <para><b>处理建议</b>先读取当前窗口实际尺寸，再按同一尺寸申请 dump 缓冲。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WWDS = 5132;

	/// <summary>未定义环境变量 DISPLAY，图形后端无法定位显示目标。</summary>
	/// <remarks>
	///   <para><b>含义</b>X11 后端要求 DISPLAY 指示目标显示，而运行环境未设置该变量。Windows 平台通常不涉及此变量 [待实测]。</para>
	///   <para><b>处理建议</b>在无头/服务环境下改用离屏渲染，或显式设置 DISPLAY。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NDVS = 5133;

	/// <summary>窗口边距厚度取值非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>设置窗口外边距宽度时给出负值或超出允许范围的厚度。</para>
	///   <para><b>处理建议</b>使用非负且在窗口尺寸内可容纳的边距厚度。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WBW = 5134;

	/// <summary>环境变量 DISPLAY 取值格式错误（应为 &lt;host&gt;:0.0 形式）。</summary>
	/// <remarks>
	///   <para><b>含义</b>X11 图形后端解析 DISPLAY 失败。该约定源自类 Unix 平台；在 Windows 平台上通常不涉及此变量 [待实测]。</para>
	///   <para><b>处理建议</b>按 &lt;host&gt;:&lt;display&gt;.&lt;screen&gt; 规范设置 DISPLAY。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WDVS = 5135;

	/// <summary>已加载字体数量达到上限，无法再登记新字体。</summary>
	/// <remarks>
	///   <para><b>含义</b>运行期字体表容量耗尽（具体上限本库未暴露）。</para>
	///   <para><b>处理建议</b>复用已加载字体、减少动态加载次数 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_TMF = 5136;

	/// <summary>字体名非法或未加载，无法按该名取用字体。</summary>
	/// <remarks>
	///   <para><b>含义</b>指定字体名不存在于系统，或未经字体枚举/加载接口登记。</para>
	///   <para><b>处理建议</b>先查询可用字体列表，改用其中确有的名称。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WFN = 5137;

	/// <summary>不存在有效的光标位置可供读取。</summary>
	/// <remarks>
	///   <para><b>含义</b>等待取回鼠标/十字光标坐标时，用户尚未在窗口内给出有效位置（窗口未获得光标事件或已失焦）。</para>
	///   <para><b>处理建议</b>确保交互窗口已激活并等待用户点击后再取位置。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WCP = 5138;

	/// <summary>目标窗口不是文本窗口，无法进行文本绘制。</summary>
	/// <remarks>
	///   <para><b>含义</b>调用要求目标为文本类窗口，但该窗口未以文本模式打开。</para>
	///   <para><b>处理建议</b>改用/先设置文本窗口，或改用适配当前窗口类型的显示方式。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NTW = 5139;

	/// <summary>目标窗口不是图像窗口，无法执行图像显示操作。</summary>
	/// <remarks>
	///   <para><b>含义</b>被操作的窗口以图形/文本方式打开，而调用要求其为目标类型（可承载图像的窗口）。</para>
	///   <para><b>处理建议</b>改用按图像方式打开的窗口，或先设置该窗口模式。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NPW = 5140;

	/// <summary>待绘制的文本串过长或过高，超出窗口可用区域。</summary>
	/// <remarks>
	///   <para><b>含义</b>一次性写入的字符串按当前字体度量算出的外接尺寸大于窗口可绘范围。</para>
	///   <para><b>处理建议</b>拆分成多段/多行、缩小字号或放大窗口后重试。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_STL = 5141;

	/// <summary>窗口内可绘空间不足（右侧剩余宽度不够容纳要输出的内容）。</summary>
	/// <remarks>
	///   <para><b>含义</b>向文本/图形窗口追加内容时，指定位置右侧已无足够空间放下该串，绘制被拒。</para>
	///   <para><b>处理建议</b>换行、缩小字号或改从空行重新起排；先确认窗口宽高足以承载目标文本。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（`Disp*`/`Draw*`/`JlWindow`），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NSS = 5142;

	/// <summary>目标窗口不具备鼠标交互能力，无法在其上等待/获取鼠标事件。</summary>
	/// <remarks>
	///   <para><b>含义</b>等待取回鼠标点击位置时，所选窗口不是可承载鼠标交互的类型（如非活动窗口或仅用于输出的窗口），交互请求被拒。</para>
	///   <para><b>处理建议</b>改用正常打开且处于可交互状态的图像/文本窗口；确认窗口已激活并拥有焦点后再等待鼠标。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NMS = 5143;

	/// <summary>窗口操作要求与显示在同一台机器上进行（原生原文有讹误，语义按字面推断）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文案 "Here Windows on a equal machine is permitted only" 本身不成句；按字面只能推断为"窗口仅允许在与显示相同的机器上创建/使用"，涉及远程或多机场景下的限制，确切触发条件无法由现有文本断言 [待实测]。</para>
	///   <para><b>处理建议</b>远程桌面/跨机环境下遇到此码时，改为在拥有目标显示的机器上创建并操作窗口 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DWNA = 5144;

	/// <summary>打开窗口时给出的显示模式非法，窗口创建失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>创建窗口时传入的模式参数（决定窗口初始可见性/用途等）不是引擎支持的值，开窗口动作被直接拒绝。</para>
	///   <para><b>处理建议</b>核对所用接口允许的模式取值后重新打开窗口；具体允许清单本库未随此码给出 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WOM = 5145;

	/// <summary>窗口当前模式不满足该操作的前提要求。</summary>
	/// <remarks>
	///   <para><b>含义</b>与 <c>Jl_ERR_NPW</c>（窗口类型不对）不同，本码指窗口的运行模式（如刷新/挂起状态等）不适合执行当前操作；哪些操作要求哪种模式的原生文档未随本码保留 [待实测]。</para>
	///   <para><b>处理建议</b>先查询窗口当前模式，将其切换到操作要求的模式后再执行。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WWM = 5146;

	/// <summary>显示设备使用固定像素/固定色表，该颜色映射操作无法执行。</summary>
	/// <remarks>
	///   <para><b>含义</b>目标显示的色彩映射被硬件固定，任何要求改写像素到颜色对应关系的操作（自定义 LUT 类）在此设备上不被允许。</para>
	///   <para><b>处理建议</b>改在色表可写的显示设备上执行，或放弃色表定制、直接用灰度/RGB 值出图。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_LUTF = 5147;

	/// <summary>颜色表（LUT）仅支持 8 位（256 级）灰度图像。</summary>
	/// <remarks>
	///   <para><b>含义</b>色表按 0..255 共 256 个表项建表，因此只对 8 位图像生效；对 12/16 位等高灰度级图像设置颜色表会命中本码。</para>
	///   <para><b>处理建议</b>先把图像降到 8 位再应用色表，或改用逐像素映射类算子处理高位深图像。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（含 `set_lut`/`query_lut` 色表接口），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_LUTN8 = 5148;

	/// <summary>伪彩色/真彩色显示模式参数取值非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>在配置颜色显示时（伪彩色映射与真彩色 RGB 之间切换），给出的色模式值不在当前平台支持之列。</para>
	///   <para><b>处理建议</b>选用当前平台确实支持的色模式；支持取值清单本库未随码保留 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WTCM = 5149;

	/// <summary>写入/查询颜色表时给出的像素值越界。</summary>
	/// <remarks>
	///   <para><b>含义</b>颜色表以灰度值为下标寻址，给出的像素值超出了表的可寻址范围（8 位表即 0..255 之外），或与该表要求的通道结构不符。</para>
	///   <para><b>处理建议</b>把用作下标的灰度值约束到表的实际容量区间内再提交。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（含 `set_lut`/`query_lut` 色表接口），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WIFTL = 5150;

	/// <summary>图像尺寸不满足伪彩色/真彩色显示的要求。</summary>
	/// <remarks>
	///   <para><b>含义</b>在伪彩/真彩显示路径上提交的图像宽高不符合该色模式下的限制（如与色表位宽或显示缓冲的匹配要求），具体限制规则本库未随码保留 [待实测]。</para>
	///   <para><b>处理建议</b>核对目标色模式对尺寸/位深的前提后调整图像，再重试显示。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WSOI = 5151;

	/// <summary>原生 LUT 处理过程内部出错（引擎内部错误码）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生色表例程（文案中的 JlRLUT）自身执行失败，不指向任何具体用户参数；多为引擎内部状态不一致或资源分配失败所致。</para>
	///   <para><b>处理建议</b>调用方无从定向修复；可重建窗口/色表环境后重试，若稳定复现应按引擎缺陷反馈 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（含 `set_lut`/`query_lut` 色表接口），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_HRLUT = 5152;

	/// <summary>set_lut 收到的色表条目数与要求不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生 `set_lut` 要求色表条目数与目标灰度级数精确一致（8 位图即 256 项；彩色表是否按通道数倍增 [待实测]），给出的数组长度不对即被拒。</para>
	///   <para><b>处理建议</b>先用 `query_lut` 读出该表应有的条目数，再按同一长度构造色表数据。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（含 `set_lut`/`query_lut` 色表接口），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WPFSL = 5153;

	/// <summary>指定图像区域的位置/范围参数值非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>以行列界定的图像区域未通过校验（如越出图像边界或起止顺序颠倒；具体校验规则本库未随码保留 [待实测]）。</para>
	///   <para><b>处理建议</b>核对区域起止坐标均落在图像范围内，且起始行/列不大于结束行/列。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WPVS = 5154;

	/// <summary>线型样式（虚线 pattern）定义非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>绘制直线/轮廓时设置的画线 pattern（实线或虚线段序列）取值不受支持。</para>
	///   <para><b>处理建议</b>改回实线或平台确有的虚线样式；允许清单本库未随码保留 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（绘制线型），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WLPN = 5155;

	/// <summary>线型样式所需的参数个数与给定的不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>每种线型 pattern 要求固定数目的参数（如虚线的段长序列长度），实参个数与 pattern 定义不匹配。</para>
	///   <para><b>处理建议</b>按所选 pattern 的定义逐项补齐或删减参数，使个数精确一致。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（绘制线型），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WLPL = 5156;

	/// <summary>给出的颜色数目与操作要求不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>颜色相关调用（色表、多色绘制、RGB 组合等）要求的颜色分量/条目个数与实际提供的不一致；各调用具体要求几个，本库未随码保留 [待实测]。</para>
	///   <para><b>处理建议</b>核对所调用操作对颜色个数的约定（如 RGB 需 3 分量、色表需整表条目数），补齐或删减后重试。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（颜色配置），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WNOC = 5157;

	/// <summary>创建区域时给出的模式参数取值非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>以某种方式（矩形/旋转矩形/圆等生成模式）构建图像区域或显示区域时，模式值不在支持列表内。</para>
	///   <para><b>处理建议</b>改用该接口明确允许的模式取值；允许清单本库未随码保留 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（区域创建），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WPST = 5158;

	/// <summary>尚未设置间谍（spy）窗口，spy 相关调用无从输出。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生 `set_spy` 体系用于把算子执行情况记录到指定窗口；未先绑定 spy 窗口就启动记录，即命中本码。</para>
	///   <para><b>处理建议</b>先调用 `set_spy` 指定输出窗口，再执行需要监视的算子。</para>
	///   <para><b>子系统状态</b>该码源自窗口/调试辅助族（`set_spy`），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SWNA = 5159;

	/// <summary>未为 spy 记录设置输出文件（set_spy 文件模式）。</summary>
	/// <remarks>
	///   <para><b>含义</b>spy 以文件为输出目标时未事先给定文件路径，记录动作被拒（与 5159 的区别：那条缺窗口、本条缺文件）。</para>
	///   <para><b>处理建议</b>先通过原生 `set_spy` 指定输出文件路径，再启动算子记录。</para>
	///   <para><b>子系统状态</b>该码源自窗口/调试辅助族（`set_spy`），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NSFO = 5160;

	/// <summary>set_spy 的输出深度参数取值非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>spy 记录时对"输出展开到第几层"（元组/对象是否递归展开等深度约定）给出了超出允许范围的设置。</para>
	///   <para><b>处理建议</b>改用受支持的深度取值；精确值域本库未随码保留 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自窗口/调试辅助族（`set_spy`），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WSPN = 5161;

	/// <summary>窗口截图（dump）时给出的窗口尺寸与实际不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>抓取窗口图像要求调用方声明的目标尺寸与窗口当前实际尺寸一致；窗口被调整过而尺寸参数没跟着改，即命中本码。</para>
	///   <para><b>处理建议</b>先读窗口当前实际宽高，再按同一尺寸分配 dump 缓冲并发起抓取。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（`dump_window` 截图），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WIFFD = 5162;

	/// <summary>颜色表非法：表名/文件名无法解析，或 query_lut 查询失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>引用色表时给出的名称或色表文件不存在、格式不可读，或对该窗口执行 `query_lut` 时拿不到有效色表。</para>
	///   <para><b>处理建议</b>改用系统自带的色表名或确认存在的路径；不确定时先查询该窗口实际挂载的色表。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（含 `set_lut`/`query_lut` 色表接口），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WLUTF = 5163;

	/// <summary>颜色表名为空字符串。</summary>
	/// <remarks>
	///   <para><b>含义</b>色表参数被传成空串，引擎无法将其解析为任何颜色表（与 5163 的区别：那条是名称无效，本条是根本没给名称）。</para>
	///   <para><b>处理建议</b>传入有效色表名；若想清除色表回到默认映射，应使用原生 `reset_lut` 一类语义而非空串。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（含 `set_lut`/`query_lut` 色表接口），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WLUTE = 5164;

	/// <summary>当前硬件只允许 set_lut('default')，不支持自定义色表。</summary>
	/// <remarks>
	///   <para><b>含义</b>显示硬件/驱动仅提供默认颜色映射，任何非 default 的色表设置请求都被拒；与 5147（固定像素）同源，是设备能力限制而非参数写错。</para>
	///   <para><b>处理建议</b>在该设备上保持默认色表，视觉结果的伪彩增强改用离线像素映射实现。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（含 `set_lut`/`query_lut` 色表接口），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WLUTD = 5165;

	/// <summary>调用在线帮助时出错（常量名与原生文案语义不对应，触发细节存疑）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文案为 "Error while calling online help"；常量名 CNDP 与该文案对不上，实际触发场景无法由现有依据断言 [待实测]。</para>
	///   <para><b>处理建议</b>属辅助服务调用类失败，上层应用记录留痕即可，无需针对性恢复动作 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/辅助工具族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_CNDP = 5166;

	/// <summary>该行（点列）无法在当前投影下映射输出。</summary>
	/// <remarks>
	///   <para><b>含义</b>在带投影的显示上下文中，指定行的几何位置落到投影有效域之外或退化（如视线平行），投影变换无从求出对应坐标；触发算子清单 [待实测]。</para>
	///   <para><b>处理建议</b>检查投影/位姿参数与点坐标，把待投影内容移回有效区域后再绘制或求交。</para>
	///   <para><b>子系统状态</b>该码源自 3D 显示上下文，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_LNPR = 5167;

	/// <summary>该操作不适用于使用固定色表的计算机/显示设备。</summary>
	/// <remarks>
	///   <para><b>含义</b>与 5147/5165 同族：设备色表被系统固定，要求改写颜色映射的操作在此环境直接不可执行，属能力限制。</para>
	///   <para><b>处理建议</b>换到色表可写的显示环境，或改为在像素数据上完成颜色处理后按真彩输出。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NFSC = 5168;

	/// <summary>当前显示设备只能呈现灰度，无法按颜色输出。</summary>
	/// <remarks>
	///   <para><b>含义</b>显示输出仅有灰度能力，任何要求真实彩色（色表映射、RGB 绘制）的操作在此设备上被拒，属设备能力限制。</para>
	///   <para><b>处理建议</b>换彩色显示设备；或接受结果以灰度呈现（伪彩信息将丢失）。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NACD = 5169;

	/// <summary>该显示可容纳的颜色表（LUT）槽位已用满。</summary>
	/// <remarks>
	///   <para><b>含义</b>单个显示设备上可同时挂起的色表数有上限，达到上限后无法再分配新色表（与 5130 全局色表数超限是不同层级的配额；具体上限本库未暴露 [待实测]）。</para>
	///   <para><b>处理建议</b>释放不再使用的窗口/色表后再分配，或复用已有色表。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（含 `set_lut`/`query_lut` 色表接口），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_LUTO = 5170;

	/// <summary>内部错误：引擎收到无法识别的颜色代码。</summary>
	/// <remarks>
	///   <para><b>含义</b>颜色代码在引擎内部解析失败，被标记为内部一致性错误而非普通的用户传参错误；但实际是否可由非法颜色串诱发 [待实测]。</para>
	///   <para><b>处理建议</b>先自查颜色写法是否为引擎约定格式；若参数无误仍复现，按引擎缺陷带上下文反馈。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（颜色代码），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WCC = 5171;

	/// <summary>窗口属性值类型与该属性要求的类型不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>读/写窗口属性时，属性值的数据类型（数值/字符串/元组）与属性定义不一致；同名属性在不同接口下类型要求可能不同 [待实测]。</para>
	///   <para><b>处理建议</b>查询该属性的类型定义后按对应类型重新赋值（与 5173 名称错误相对，本码是名称对、类型错）。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（窗口属性读写），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WWATTRT = 5172;

	/// <summary>窗口属性名不存在，无法按该名读写。</summary>
	/// <remarks>
	///   <para><b>含义</b>给出的窗口属性名称字符串不在引擎支持的属性列表内（大小写与拼写严格匹配 [待实测]）。</para>
	///   <para><b>处理建议</b>先枚举/查询该窗口支持的属性名，改用确实存在的名称。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（窗口属性读写），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WWATTRN = 5173;

	/// <summary>区域的行方向尺寸（高度）为负或零。</summary>
	/// <remarks>
	///   <para><b>含义</b>以行列界定的区域在行方向上算出的高度不大于 0（结束行未大于起始行，或范围给成了 0）；名中 R 即 row，与 5175（列方向）成对。</para>
	///   <para><b>处理建议</b>保证结束行大于起始行，使高度至少覆盖 1 行像素后再提交。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（区域尺寸校验），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WRSPART = 5174;

	/// <summary>区域的列方向尺寸（宽度）为负或零。</summary>
	/// <remarks>
	///   <para><b>含义</b>以行列界定的区域在列方向上算出的宽度不大于 0（结束列未大于起始列，或范围给成了 0）；名中 C 即 column，与 5174（行方向）成对。</para>
	///   <para><b>处理建议</b>保证结束列大于起始列，使宽度至少覆盖 1 列像素后再提交。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（区域尺寸校验），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WCSPART = 5175;

	/// <summary>窗口未完整可见，依赖全屏内容的操作无法执行。</summary>
	/// <remarks>
	///   <para><b>含义</b>如截图（dump）一类需要抓取整窗像素的操作，要求窗口在屏幕上完整可见；被其它窗口遮挡或部分移出屏幕时即命中本码。</para>
	///   <para><b>处理建议</b>将窗口移动/缩放至屏幕内、置于最前（取消遮挡）后重试；离屏需求改用不依赖屏幕像素的实现。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WNCV = 5176;

	/// <summary>该操作不允许使用当前所选字体。</summary>
	/// <remarks>
	///   <para><b>含义</b>与字体名不存在（5137）不同，本码指字体本身有效，但此绘制/交互操作对其有限制（如仅允许定宽字体一类约束；具体限制矩阵本库未随码保留 [待实测]）。</para>
	///   <para><b>处理建议</b>换用平台通用字体后重试；仍失败则查阅该操作对字体的专门要求。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（字体），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_FONT_NA = 5177;

	/// <summary>窗口由另一线程创建，不能在当前线程操作。</summary>
	/// <remarks>
	///   <para><b>含义</b>窗口与其消息循环绑定在创建它的线程上，从其它线程直接对该窗口绘制/读写即命中本码；这与平台 GUI 线程亲和性一致，细节 [待实测]。</para>
	///   <para><b>处理建议</b>把窗口操作归并回创建线程执行（或将显示逻辑集中到单一专用线程），不要跨线程共享窗口句柄。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WDIFFTH = 5178;

	/// <summary>绘制对象已挂接在另一窗口上，不能重复挂接。</summary>
	/// <remarks>
	///   <para><b>含义</b>可交互绘制对象与窗口是一对一独占关系；把它挂到新窗口时，因仍附着于旧窗口而被拒。注意本码值为 5194，与相邻码不连续。</para>
	///   <para><b>处理建议</b>先从原窗口解除挂接（或销毁原窗口/对象），再挂接到目标窗口。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（交互绘制对象），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_OBJ_ATTACHED = 5194;

	/// <summary>内部错误：该通道处理路径仅支持 RGB 模式。</summary>
	/// <remarks>
	///   <para><b>含义</b>引擎内部按通道名处理颜色时进入了只认 RGB 的分支，遇到非 RGB 通道组合即失败；被标记为内部一致性错误，是否可由用户参数诱发 [待实测]。</para>
	///   <para><b>处理建议</b>彩色输入统一使用 RGB 通道序；参数无误仍复现时，按引擎缺陷带上下文反馈。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（颜色通道），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_CHA3 = 5180;

	/// <summary>可创建的（图像）窗口数目已达上限，无新窗口可用。</summary>
	/// <remarks>
	///   <para><b>含义</b>引擎/平台对同时存在的图像窗口数有配额，用尽后新建窗口即被拒；上限值随平台而异，本库未暴露 [待实测]。</para>
	///   <para><b>处理建议</b>先关闭不再使用的窗口释放配额；长驻应用应复用窗口而非反复新建。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NMWA = 5181;

	/// <summary>绘制窗口时未一并存储深度信息，取不到深度。</summary>
	/// <remarks>
	///   <para><b>含义</b>3D 显示上下文中，深度缓冲需随窗口绘制时显式保存；未开启该保存就去读逐像素深度，即命中本码；关联接口清单 [待实测]。</para>
	///   <para><b>处理建议</b>在启用深度存储的渲染模式下重绘该窗口，再读取深度数据。</para>
	///   <para><b>子系统状态</b>该码源自 3D 显示上下文，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DEPTH_NOT_STORED = 5179;

	/// <summary>绘制窗口时未一并存储对象索引，拾取取不到对象序号。</summary>
	/// <remarks>
	///   <para><b>含义</b>3D 显示上下文中，按像素反查"该处画的是第几个对象"依赖绘制时保存的索引缓冲；未开启就去拾取即命中本码。与 5179 成对：那条缺深度缓冲，本条缺索引缓冲。</para>
	///   <para><b>处理建议</b>在启用对象索引存储的渲染模式下重绘窗口，再执行拾取/反查。</para>
	///   <para><b>子系统状态</b>该码源自 3D 显示上下文，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_INDEX_NOT_STORED = 5182;

	/// <summary>错误码 5183：所调用的算子不支持"缺少点坐标的图元"（primitive without point coordinates）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>把没有点坐标的图元传给要求点坐标的算子。</para>
	///   <para><b>说明</b>涉及的图元类型与具体算子 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_PRIM_NO_POINTS = 5183;

	/// <summary>图像超出 Windows 远程桌面尺寸上限：经远程桌面会话显示时图像尺寸超过该通道允许的最大值。</summary>
	/// <remarks>
	///   <para><b>含义</b>远程桌面（RDP）会话对可传输/可绘制的位图尺寸有协议层上限，超限即命中本码；上限随 RDP 版本与配置而异，本库未随码保留具体数值 [待实测]。</para>
	///   <para><b>处理建议</b>在远程会话中先缩小显示尺寸、分块呈现，或改用本地控制台；仅需保存结果时走写盘路径而非屏幕显示。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_REMOTE_DESKTOP_SIZE = 5184;

	/// <summary>无可用 OpenGL：本机拿不到可创建的 GL 上下文或硬件加速实现。</summary>
	/// <remarks>
	///   <para><b>含义</b>依赖 OpenGL 的功能在初始化渲染环境阶段即失败，典型于无显卡驱动/驱动过旧、远程桌面或虚拟机无 GL 透传的环境；本库对 GL 可用性的探测方式 [待实测]。</para>
	///   <para><b>处理建议</b>安装或更新显卡驱动并确认处于本地控制台会话；部署机上应在启用相关功能前预检 GL 可用性，而非运行期捕获本码降级。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（OpenGL），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NOGL = 5185;

	/// <summary>无深度信息可用：读取逐像素深度时，当前上下文没有生成或留存深度数据。</summary>
	/// <remarks>
	///   <para><b>含义</b>按像素查深度依赖绘制时启用并可回读的深度缓冲；未启用深度或场景本就不产生深度（纯 2D）时取深度即命中本码。与 5179（绘制时未存储深度）、5182（未存对象索引）同族：本码是"拿不到深度数据"的兜底。</para>
	///   <para><b>处理建议</b>在启用深度存储的渲染模式下重绘再读深度；2D 场景不要使用深度类查询。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（3D 深度），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NODEPTH = 5186;

	/// <summary>OpenGL 调用返回错误：GL 错误队列出现非零状态的泛化兜底码。</summary>
	/// <remarks>
	///   <para><b>含义</b>运行时检测到 OpenGL 报错后统一映射为本码，不区分具体 GL 错误值与出错调用，定位需回到出错现场 [待实测]。</para>
	///   <para><b>处理建议</b>与 5185 分诊：5185 是环境根本建不起来（无 GL 可用），本码是环境已建立但某次调用失败——先排查驱动版本、远程桌面/虚拟机环境与上下文丢失，再复现记录具体调用。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（OpenGL），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_OGL_ERROR = 5187;

	/// <summary>所需的帧缓冲对象（FBO，framebuffer object）不受当前 OpenGL 实现支持。</summary>
	/// <remarks>
	///   <para><b>含义</b>离屏渲染/渲染到纹理依赖 FBO 扩展能力；老卡、纯软件实现或驱动不完整时，要求离屏缓冲的操作即命中本码。</para>
	///   <para><b>处理建议</b>更新显卡驱动或换支持 FBO 的机器；若功能存在非离屏替代路径则改走该路径，替代性 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（OpenGL 离屏渲染），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_UNSUPPORTED_FBO = 5188;

	/// <summary>本机不支持 OpenGL 硬件加速消隐（HSR，hidden surface removal）。</summary>
	/// <remarks>
	///   <para><b>含义</b>3D 显示走硬件加速的深度消隐路径时，当前显卡/驱动不提供该能力，多见于远程桌面、虚拟机或纯软件 OpenGL 环境。</para>
	///   <para><b>处理建议</b>换用不要求硬件 HSR 的显示方式或改用具备硬件加速的机器；本库 2D 功能是否受此影响 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（3D 显示），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_OGL_HSR_NOT_SUPPORTED = 5189;

	/// <summary>窗口参数非法：查询或设置的窗口属性名不存在于窗口的参数表。</summary>
	/// <remarks>
	///   <para><b>含义</b>窗口属性以名/值对管理，传入未定义的属性名即命中本码。与 5191 成对（本码错在名字、5191 错在取值）；与 5226 区分（那是算子参数名非法，不是窗口属性）。</para>
	///   <para><b>处理建议</b>按窗口合法属性名列表核对拼写后重传；合法列表本库未随码保留 [待实测]。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（窗口属性读写），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WP_IWP = 5190;

	/// <summary>窗口参数的取值非法：参数名可识别，但传入的值超出该参数的允许范围。</summary>
	/// <remarks>
	///   <para><b>含义</b>与 5190 成对分责：5190 是参数名本身不认识，本码是名字对了但值被拒（越界、类型不符或与该窗口当前状态矛盾，具体判据 [待实测]）。</para>
	///   <para><b>处理建议</b>查明该窗口属性的取值范围与单位约定后重设；先用 5190/5191 区分是"名字错"还是"值错"，避免查错方向。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（窗口属性读写），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WP_IWPV = 5191;

	/// <summary>未知模式：按 mode 分支的接口收到了其取值表之外的模式值（泛化兜底码）。</summary>
	/// <remarks>
	///   <para><b>含义</b>凡以模式字符串/枚举选择行为的接口都可能在参数校验处命中本码；本码不指明接收接口与合法取值，需回到出错调用处自查其文档 [待实测]。</para>
	///   <para><b>处理建议</b>核对模式参数的拼写与大小写；同族专码：窗口外导航用 5195，本码为其余未细分场景的兜底。</para>
	/// </remarks>
	public const int Jl_ERR_UMOD = 5192;

	/// <summary>窗口上没有挂接图像：需要从窗口回读当前显示内容的操作无图可取。</summary>
	/// <remarks>
	///   <para><b>含义</b>从窗口反向取图（如截取当前显示、按窗口内容处理）依赖"窗口当前附着着一幅图像"；尚未绘制任何图像或内容已被清除时即命中本码。</para>
	///   <para><b>处理建议</b>先在该窗口绘制/挂接目标图像再执行回读。与 5194（绘制对象已挂在另一窗口、不能重复挂接）方向相反：5194 是挂多了，本码是没得挂。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族，此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_ATTIMG = 5193;

	/// <summary>交互导航模式非法：启动鼠标导航时给出的 mode 不在该接口允许的取值集合内。</summary>
	/// <remarks>
	///   <para><b>含义</b>以鼠标滚轮/按键对视图做缩放、平移等人机导航的接口按 mode 分支，传入未定义的模式即命中本码；合法模式集合未随码保留在本库 [待实测]。</para>
	///   <para><b>处理建议</b>对照所用导航接口的文档改传列出的模式值；与 5192（泛化的未知模式）同族，本码特指导航。</para>
	///   <para><b>子系统状态</b>该码源自显示/窗口族（交互导航），此族已从本库移除，托管层一般不会收到此码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NVG_WM = 5195;

	/// <summary>错误码 5196：运行时内部文件错误，文件子系统抛出的非归类兜底错误。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>文件子系统内部异常，英文原文未给出更细分类。</para>
	///   <para><b>处置</b>通常需结合上下文日志定位；对上层表现为不可直接归因的文件错误。具体成因 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_FINTERN = 5196;

	/// <summary>错误码 5197：文件同步（将缓冲数据强制落盘）时出错。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>flush/fsync 到存储介质失败。</para>
	///   <para><b>处置</b>检查存储设备可用性与写权限。具体语义 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_FS = 5197;

	/// <summary>错误码 5198：权限不足，无法对目标文件/资源执行所请求的操作。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>当前进程对目标没有读/写/执行所需的访问权。</para>
	///   <para><b>处置</b>提升权限或调整文件访问控制。</para>
	/// </remarks>
	public const int Jl_ERR_FISR = 5198;

	/// <summary>错误码 5199：非法文件描述符——对已关闭或从未成功打开的句柄进行读写。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>使用了无效的句柄，常见于重复关闭或使用了打开失败的返回值。</para>
	/// </remarks>
	public const int Jl_ERR_BFD = 5199;

	/// <summary>错误码 5200：指定的文件未找到（路径不存在或文件名有误）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>打开/读取时目标路径下没有该文件。</para>
	///   <para><b>处置</b>核对完整路径与文件名（含扩展名）后重试。</para>
	/// </remarks>
	public const int Jl_ERR_FNF = 5200;

	/// <summary>错误码 5201：写入图像像素数据时出错，英文原文以问句提示疑为内存不足。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>保存图像数据失败，怀疑可用内存不足。</para>
	///   <para><b>区分</b>与 5202 分别对应"写像素数据"与"写图像描述头"。</para>
	/// </remarks>
	public const int Jl_ERR_DWI = 5201;

	/// <summary>错误码 5202：写入图像描述头（descriptor，即宽/高/类型等元信息）时出错，英文同样疑为内存不足。</summary>
	/// <remarks>
	///   <para><b>区分</b>失败发生在写描述头而非像素数据；与 5201 成对。</para>
	/// </remarks>
	public const int Jl_ERR_DWID = 5202;

	/// <summary>错误码 5203：读取图像数据时出错，英文原文以问句提示疑为文件声明的图像格式过小。</summary>
	/// <remarks>
	///   <para><b>区分</b>与 5204 分别对应"数据过小/过大"两种越界读取。</para>
	/// </remarks>
	public const int Jl_ERR_DRI1 = 5203;

	/// <summary>错误码 5204：读取图像数据时出错，英文原文以问句提示疑为文件声明的图像格式过大（相对 5203 的过小）。</summary>
	/// <remarks>
	///   <para><b>区分</b>与 5203 成对，分别表示数据"过大/过小"。</para>
	/// </remarks>
	public const int Jl_ERR_DRI2 = 5204;

	/// <summary>错误码 5205：读取图像描述头时出错——文件过小，不足以容纳完整的描述头。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>文件被截断或非图像文件（无有效描述头）时命中。</para>
	///   <para><b>区分</b>与 5203/5204（读像素数据）不同，此处失败在读描述头。</para>
	/// </remarks>
	public const int Jl_ERR_DRID1 = 5205;

	/// <summary>错误码 5206：参与运算的多幅图像的尺寸矩阵（宽×高×通道）不一致，无法逐元素组合。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>需同尺寸输入的算子收到尺寸不同的图像。</para>
	///   <para><b>处置</b>先将各图裁剪/缩放到同一尺寸再运算。具体触发算子 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DIMMAT = 5206;

	/// <summary>错误码 5207：未找到帮助文件，通常因环境变量 VisionROOT 未正确指向安装根目录。</summary>
	/// <remarks>
	///   <para><b>区分</b>与 5208 相对：此处指帮助文件本体缺失，5208 指帮助索引缺失。</para>
	///   <para><b>处置</b>检查/设置 VisionROOT 指向有效安装根目录。具体路径 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_HNF = 5207;

	/// <summary>错误码 5208：未找到帮助索引文件，通常因环境变量 VisionROOT 未正确指向安装根目录。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>调用联机帮助时找不到其索引文件。</para>
	///   <para><b>处置</b>检查/设置 VisionROOT 环境变量指向包含有效帮助索引的根目录。具体路径 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_XNF = 5208;

	/// <summary>错误码 5209：无法关闭标准输入文件（&lt;standard_input&gt;）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对标准输入流执行关闭失败，多与句柄已失效或被系统占用有关。</para>
	///   <para><b>说明</b>本常量英文原文即特指标准输入流，非普通数据文件。具体成因 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_CNCSI = 5209;

	/// <summary>错误码 5210：无法关闭标准输出/标准错误流（&lt;standard_output/error&gt;）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>关闭标准输出或标准错误流失败，与句柄失效或被占用有关。</para>
	///   <para><b>说明</b>本常量特指标准输出/错误流。具体成因 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_CNCSO = 5210;

	/// <summary>错误码 5211：文件无法关闭（区别于 5209/5210 的标准流，此处指普通数据文件）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对某个已打开的数据文件执行关闭失败。</para>
	///   <para><b>处置</b>结合上一个错误码判断句柄是否已失效或被外部占用。具体成因 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_CNCF = 5211;

	/// <summary>错误码 5212：向文件写入数据时出错。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>写入过程失败，常见于磁盘空间不足、权限不足或文件句柄已失效。</para>
	///   <para><b>处置</b>检查目标路径可写性与剩余空间。具体诱因 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_EDWF = 5212;

	/// <summary>错误码 5213：进程内已打开的文件句柄数超过运行时上限，本次打开被拒绝。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>同时处于打开状态的文件数已达系统允许的最大值。</para>
	///   <para><b>处置</b>先关闭不再使用的文件再重试；若长期运行需控制并发打开数。具体上限值 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NFA = 5213;

	/// <summary>文件名非法：所给文件名字符串本身不符合运行时允许的形式。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>文件名含非法字符、为空、超长，或不符合读/写图像接口对命名的要求（例如按序号模板组织的图像序列）；具体校验规则本库未随码保留 [待实测]。</para>
	///   <para><b>区分</b>与 5200（文件未找到）不同：FNF 是"名字合法但盘上没有"，本码是"名字本身过不了校验"；排查先查字符与长度，再查路径是否存在。</para>
	/// </remarks>
	public const int Jl_ERR_WFIN = 5214;

	/// <summary>打开文件时出错：打开动作本身失败的非归类兜底码。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>发起打开后系统拒绝或失败，常见诱因为权限不足、文件被其他进程独占、目标位于不可用介质；能否给出更细原因需结合上一个错误码判断 [待实测]。</para>
	///   <para><b>区分</b>5200 专指文件不存在、5214 指名字非法、5216 指方式给错；本码是"找得到名字也对但就是打不开"的兜底，排查时先于这三者排除。</para>
	/// </remarks>
	public const int Jl_ERR_CNOF = 5215;

	/// <summary>文件打开方式非法：所用的打开模式（读/写/追加等组合）不被支持或与请求的操作冲突。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>以引擎不认识的方式打开文件，典型如只写方式打开的文件去读、或对已按某模式打开的文件请求相斥操作；具体允许的 mode 集合 [待实测]。</para>
	///   <para><b>区分</b>与相邻两码成阶梯：5214 文件名本身非法、5215 打开动作失败、本码则专指方式/mode 参数给错——文件与权限都正常时先查这里。</para>
	/// </remarks>
	public const int Jl_ERR_WFMO = 5216;

	/// <summary>像素类型不符：给定的像素类型（如 byte）不被该图像数据或目标文件格式接受。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读/写图像时指定的通道类型与图像实际类型或目标格式所支持的类型集合冲突，英文原文以 byte 为例；各格式支持的类型清单本库未随码保留 [待实测]。</para>
	///   <para><b>排查方向</b>先确认图像内存中的实际类型，再对照目标格式的支持范围改传兼容类型，或先转换图像类型再读写；与 5216（打开方式错）区分——本码错在类型而非模式。</para>
	/// </remarks>
	public const int Jl_ERR_WPTY = 5217;

	/// <summary>图像宽度非法：读取或生成图像时宽度超出允许范围或与实际数据不符。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>图像头/请求尺寸中的宽度越界、源数据实际宽度与声明尺寸不一致，或分配缓冲时宽度过大。原文以 "too big ?" 存疑标注，具体阈值以运行时为准 [待实测]。</para>
	///   <para><b>排查方向</b>核对图像文件头宽度与请求的输出尺寸；与高度版 <c>Jl_ERR_WIH</c> 成对出现，先看是哪个维度报错。读入已有文件时多为文件头损坏或格式误判，程序生成时多为输出尺寸参数算错。</para>
	/// </remarks>
	public const int Jl_ERR_WIW = 5218;

	/// <summary>图像高度非法：读取或生成图像时高度超出允许范围或与实际数据不符。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>图像高度参数越界、源数据实际高度与声明尺寸不一致，或分配缓冲时高度过大。原文以 "too big ?" 存疑标注，具体阈值以运行时为准[待实测]。</para>
	///   <para><b>排查方向</b>核对图像文件头高度与请求的输出尺寸；与宽度版 <c>Jl_ERR_WIW</c> 成对出现，先看是哪个维度报错。</para>
	/// </remarks>
	public const int Jl_ERR_WIH = 5219;

	/// <summary>读取图像前文件已耗尽：数据流已到末尾，尚未读到一张完整图像。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>连续读取多幅图像的文件时，读取指针已越过文件末尾仍请求下一幅。与 <c>Jl_ERR_FTS2</c> 区别：本码在开始读取时就已无数据，FTS2 在读取中途耗尽。</para>
	///   <para><b>排查方向</b>循环读图前先判断是否到达末尾；文件被截断或不完整也会触发。</para>
	/// </remarks>
	public const int Jl_ERR_FTS1 = 5220;

	/// <summary>图像未读完文件即耗尽：读取一幅图像的过程中数据流提前结束。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>文件剩余字节不足以补全当前图像的像素数据，通常源于文件被截断或写入不完整。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_FTS1</c> 配对：FTS1 是没数据可读，本码是数据读一半就断；检查文件是否完整、是否边写边读。</para>
	/// </remarks>
	public const int Jl_ERR_FTS2 = 5221;

	/// <summary>DPI 解析度取值非法：读/写图像时分辨率参数超出允许范围。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>给图像设置 x/y 分辨率（dpi）时传入非正或不支持的数值，常见于导出 TIFF/BMP 等含 dpi 头的格式。</para>
	///   <para><b>排查方向</b>确认 dpi 为正数且符合目标格式约束。</para>
	/// </remarks>
	public const int Jl_ERR_WDPI = 5222;

	/// <summary>输出图像宽度非法：请求的输出图像宽度超出允许范围或与源不符。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>缩放、裁剪或导出时指定的输出宽度不合法。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_WNOH</c> 成对，本码专指宽度维度；核对输出尺寸参数与源图像尺寸关系。</para>
	/// </remarks>
	public const int Jl_ERR_WNOW = 5223;

	/// <summary>输出图像高度非法：请求的输出图像高度超出允许范围或与源不符。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>缩放、裁剪或导出时指定的输出高度不合法。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_WNOW</c> 成对，本码专指高度维度；核对输出高度参数与源图像尺寸关系。</para>
	/// </remarks>
	public const int Jl_ERR_WNOH = 5224;

	/// <summary>格式描述参数个数不符：读写图像格式说明符时给定值的个数与该格式要求不匹配。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>使用带自定义格式说明的读/写图像接口时，参数元组的长度与该格式期望的字段数不一致。</para>
	///   <para><b>排查方向</b>对照目标格式（如 jpeg 质量、tiff 压缩）所需的参数个数逐一核对。</para>
	/// </remarks>
	public const int Jl_ERR_WNFP = 5225;

	/// <summary>算子参数名非法：查询或设置算子参数时使用了该算子不认识的参数名。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>向取/设参数类接口传入拼写错误或该算子不具备的参数名。</para>
	///   <para><b>排查方向</b>用该算子的合法参数名列表核对；与 <c>Jl_ERR_WSNA</c>（槽名 slot 非法）区分。</para>
	/// </remarks>
	public const int Jl_ERR_WPNA = 5226;

	/// <summary>参数槽名（slot）非法：按槽位访问参数时使用了不存在的 slot 名。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>以 slot 定位参数时传入的名称与算子定义不符。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_WPNA</c>（参数名非法）区分：本码针对 slot 层级；确认算子的 slot 命名。</para>
	/// </remarks>
	public const int Jl_ERR_WSNA = 5227;

	/// <summary>帮助文件缺少算子类：算子所属的类在 help 数据中未登记。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>查询算子帮助信息时其算子类在帮助库索引里缺失，属安装/文档数据不完整。</para>
	///   <para><b>排查方向</b>多为运行时 help 文件版本与库不匹配，非用户逻辑错误。</para>
	/// </remarks>
	public const int Jl_ERR_NPCF = 5228;

	/// <summary>帮助索引文件损坏或不一致：help 目录下的 .idx 或 .sta 内容异常。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>加载算子帮助时校验 .idx/.sta 失败，文件被改动或版本错配。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_HINF</c>/<c>Jl_ERR_HSNF</c>/<c>Jl_ERR_ICSF</c> 同属 help 数据族；整目录一致地重新部署帮助文件可解。</para>
	/// </remarks>
	public const int Jl_ERR_WHIF = 5229;

	/// <summary>缺少帮助索引 .idx：运行时找不到 help 目录下的 .idx 索引文件。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读取算子帮助时 .idx 文件不存在，属安装不完整。</para>
	///   <para><b>排查方向</b>确认 help 目录与安装路径配置正确。</para>
	/// </remarks>
	public const int Jl_ERR_HINF = 5230;

	/// <summary>缺少帮助统计 .sta：运行时找不到 help 目录下的 .sta 文件。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>.sta 缺失导致帮助数据无法加载，属安装不完整。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_HINF</c> 配对（.idx/.sta），核对 help 目录完整性。</para>
	/// </remarks>
	public const int Jl_ERR_HSNF = 5231;

	/// <summary>.sta 帮助文件不一致：help 下的 .sta 内容与其他帮助数据版本不匹配。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>.sta 存在但校验不一致，通常因部分文件被替换而部分未更新。</para>
	///   <para><b>排查方向</b>整目录一致地重新部署 help 数据。</para>
	/// </remarks>
	public const int Jl_ERR_ICSF = 5232;

	/// <summary>缺少说明文件 .exp：运行时找不到算子的 .exp 说明文件。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>访问算子说明文本时 .exp 缺失，属帮助数据不完整。</para>
	///   <para><b>排查方向</b>核对说明文件是否随库一起安装。</para>
	/// </remarks>
	public const int Jl_ERR_EFNF = 5233;

	/// <summary>无法识别图像格式：在已知图形格式中找不到匹配，文件不属于任何受支持的图像格式。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读图时文件头不匹配任何内置解码格式。</para>
	///   <para><b>排查方向</b>确认扩展名与真实编码一致；非图像文件误传也会命中。与 <c>Jl_ERR_WIFT</c>/<c>Jl_ERR_WFF</c> 的区别：本码表示"没有任何格式能认领"。</para>
	/// </remarks>
	public const int Jl_ERR_NFWKEF = 5234;

	/// <summary>图形格式错误：文件被识别为某种图像格式但其格式参数或内容不合法。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>格式已被认领但内部结构非法（与 <c>Jl_ERR_NFWKEF</c> 的"无人认领"不同）。</para>
	///   <para><b>排查方向</b>换用可靠来源的文件，或显式指定读取格式。</para>
	/// </remarks>
	public const int Jl_ERR_WIFT = 5235;

	/// <summary>Vision.num 文件不一致：运行时配置/编号文件 Vision.num 内容异常。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读取 Vision.num 时校验不符，属安装数据损坏或版本错配。</para>
	///   <para><b>排查方向</b>非用户逻辑问题，核对安装完整性。</para>
	/// </remarks>
	public const int Jl_ERR_ICNF = 5236;

	/// <summary>扩展名为 tiff 但内容非 TIFF：按 TIFF 解码时发现并非合法 TIFF 结构。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>仅凭扩展名/格式提示走 TIFF 路径，但文件实际字节不符合 TIFF 头。</para>
	///   <para><b>排查方向</b>修正误标扩展名，或用能嗅探内容的格式读取。</para>
	/// </remarks>
	public const int Jl_ERR_WTIFF = 5237;

	/// <summary>文件格式错误：文件的整体格式不被接受或与预期格式不符。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>较通用的格式非法错误，比 <c>Jl_ERR_WTIFF</c>/<c>Jl_ERR_NFWKEF</c> 更笼统。</para>
	///   <para><b>排查方向</b>结合具体读/写操作确认目标格式。</para>
	/// </remarks>
	public const int Jl_ERR_WFF = 5238;

	/// <summary>非 PNM 格式：文件不符合 PNM（PBM/PGM/PPM）格式。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>按 PNM 读取但头部/结构不合法。</para>
	///   <para><b>排查方向</b>确认文件确为受支持的 PNM 变体。</para>
	/// </remarks>
	public const int Jl_ERR_NOPNM = 5242;

	/// <summary>帮助文件不一致或过旧：help 数据与当前库版本不匹配或格式陈旧。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>加载帮助时版本或一致性校验失败。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_WHIF</c>/<c>Jl_ERR_HINF</c> 族相关，本码偏"过旧"；升级/重装匹配版本的帮助数据。</para>
	/// </remarks>
	public const int Jl_ERR_ICODB = 5243;

	/// <summary>文件编码非法：指定的文件文本编码不受支持或与内容冲突。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读写文本类文件时传入非法编码标识。</para>
	///   <para><b>排查方向</b>用受支持的编码；二进制文件不得设文本编码（见 <c>Jl_ERR_BINFILE_ENC</c>）。</para>
	/// </remarks>
	public const int Jl_ERR_INVAL_FILE_ENC = 5244;

	/// <summary>文件未打开：对一个尚未成功打开（或已关闭）的文件句柄执行读写。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>open 未调用或已 close 后仍操作。</para>
	///   <para><b>排查方向</b>先 open 并检查其返回再操作；注意与 <c>Jl_ERR_NO_FILES</c>（当前无任何已打开文件）区分。</para>
	/// </remarks>
	public const int Jl_ERR_FNO = 5245;

	/// <summary>当前无打开的文件：查询文件相关状态时系统尚无任何已打开文件。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>枚举/操作"已打开文件"类接口时列表为空，属状态提示而非硬错误。</para>
	///   <para><b>排查方向</b>先 open 再查询；与 <c>Jl_ERR_FNO</c>（针对某个未打开的句柄）区分。</para>
	/// </remarks>
	public const int Jl_ERR_NO_FILES = 5246;

	/// <summary>区域文件格式非法：按区域专用格式读写时发现内容不符合该格式。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读取区域文件（非普通图像）时头部/结构不合法。</para>
	///   <para><b>排查方向</b>确认是区域序列化文件而非图像文件；与 REG_* 序列化族相关。</para>
	/// </remarks>
	public const int Jl_ERR_NORFILE = 5247;

	/// <summary>区域数据过大：读取区域时其规模超出该格式允许的上限。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>区域过于复杂（行程/连通域过多）超过文件格式上限。</para>
	///   <para><b>排查方向</b>考虑简化区域，或改用可承载大数据的对象序列化格式。</para>
	/// </remarks>
	public const int Jl_ERR_RDTB = 5248;

	/// <summary>二进制文件不允许设编码：对以二进制方式打开的文件设置了文本编码。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>把 encoding 参数用于二进制文件操作。</para>
	///   <para><b>排查方向</b>二进制读写不应指定 encoding；文本文件才走 <c>Jl_ERR_INVAL_FILE_ENC</c> 那套编码校验。</para>
	/// </remarks>
	public const int Jl_ERR_BINFILE_ENC = 5249;

	/// <summary>读文件出错：底层文件读取失败的通用错误。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>IO 层读取异常（磁盘、权限、断流等），不含具体格式判定。</para>
	///   <para><b>排查方向</b>较笼统，先查文件可读性与句柄状态；格式相关错误会由 <c>Jl_ERR_WFF</c>/<c>Jl_ERR_WTIFF</c> 等更具体码给出。</para>
	/// </remarks>
	public const int Jl_ERR_EDRF = 5250;

	/// <summary>串口未打开：在串口未成功打开的情况下访问它。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>串口通信前未 open，或已关闭后仍读写。</para>
	///   <para><b>排查方向</b>确认串口打开流程；本码起（5251-5262）整段属串口通信错误族。</para>
	/// </remarks>
	public const int Jl_ERR_SNO = 5251;

	/// <summary>无可用串口：系统中找不到可打开的串口设备。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>枚举/打开串口时无可用端口。</para>
	///   <para><b>排查方向</b>确认硬件/驱动存在对应串口；与 <c>Jl_ERR_CNOS</c>（端口存在但打开失败）区分。</para>
	/// </remarks>
	public const int Jl_ERR_NSA = 5252;

	/// <summary>无法打开串口：端口存在但打开失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>权限、被占用或参数非法导致 open 失败。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_NSA</c>（根本没有可用端口）区分；检查端口是否被其他程序占用、参数是否合法。</para>
	/// </remarks>
	public const int Jl_ERR_CNOS = 5253;

	/// <summary>无法关闭串口：关闭串口操作失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>close 调用在系统层失败。</para>
	///   <para><b>排查方向</b>确认句柄有效、无异常状态阻塞关闭。</para>
	/// </remarks>
	public const int Jl_ERR_CNCS = 5254;

	/// <summary>无法读取串口属性：获取波特率/数据位等配置项失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>查询串口属性（termios 类）时被系统拒绝。</para>
	///   <para><b>排查方向</b>端口是否已打开、驱动是否支持属性查询；与 <c>Jl_ERR_CNSSA</c>（设置属性失败）配对。</para>
	/// </remarks>
	public const int Jl_ERR_CNGSA = 5255;

	/// <summary>无法设置串口属性：写入波特率/数据位等配置失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>设置串口属性被系统拒绝。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_CNGSA</c>（读取属性失败）配对；检查请求的参数组合是否被硬件支持。</para>
	/// </remarks>
	public const int Jl_ERR_CNSSA = 5256;

	/// <summary>串口波特率非法：请求的 baud rate 不被支持。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>设置串口时给定非法波特率。</para>
	///   <para><b>排查方向</b>使用标准波特率档位；与 <c>Jl_ERR_WRSDB</c>/<c>Jl_ERR_WRSFC</c> 同属串口参数校验。</para>
	/// </remarks>
	public const int Jl_ERR_WRSBR = 5257;

	/// <summary>串口数据位非法：data bits 取值超出允许范围（通常 5-8）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>设置串口时给定不合法的数据位。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_WRSBR</c> 同属串口参数校验；核对常用值 7/8 位。</para>
	/// </remarks>
	public const int Jl_ERR_WRSDB = 5258;

	/// <summary>串口流控非法：设置的 flow control 模式不被支持。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>给定无效的流控模式（如非 none/hardware/software）。</para>
	///   <para><b>排查方向</b>改用受支持的流控模式。</para>
	/// </remarks>
	public const int Jl_ERR_WRSFC = 5259;

	/// <summary>无法刷新串口：清空/刷新串口缓冲失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>flush 调用在系统层失败。</para>
	///   <para><b>排查方向</b>确认端口处于可刷新状态。</para>
	/// </remarks>
	public const int Jl_ERR_CNFS = 5260;

	/// <summary>写串口出错：向串口写数据时发生 IO 错误。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>写操作失败（断开、缓冲满等）。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_EDRS</c> 区分，本码为写方向。</para>
	/// </remarks>
	public const int Jl_ERR_EDWS = 5261;

	/// <summary>读串口出错：从串口读数据时发生 IO 错误。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读操作失败。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_EDWS</c> 区分，本码为读方向。</para>
	/// </remarks>
	public const int Jl_ERR_EDRS = 5262;

	/// <summary>序列化项不含有效区域：从通用序列化数据读出的项不是合法的 region 对象。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>反序列化时对象类型与期望的 region 不符。</para>
	///   <para><b>排查方向</b>与 IMG/XLD/OBJ 的 NOSITEM（5272-5276）成族；先确认序列化流里存的到底是什么类型。</para>
	/// </remarks>
	public const int Jl_ERR_REG_NOSITEM = 5270;

	/// <summary>区域版本不支持：序列化数据中的 region 版本号超出当前库支持范围。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>用旧库读取新版本导出的 region，或反之。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_REG_NOSITEM</c> 区分（本码是类型对但版本不对）；用匹配版本读写。</para>
	/// </remarks>
	public const int Jl_ERR_REG_WRVERS = 5271;

	/// <summary>序列化项不含有效图像：反序列化得到的项不是合法的 image 对象。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>序列化流中对象类型与期望 image 不符。</para>
	///   <para><b>排查方向</b>属 IMG/REG/XLD/OBJ 序列化族；核对写入时的对象类型。</para>
	/// </remarks>
	public const int Jl_ERR_IMG_NOSITEM = 5272;

	/// <summary>图像版本不支持：序列化数据中的 image 版本号超出支持范围。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>版本不匹配导致无法反序列化 image。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_IMG_NOSITEM</c> 区分；统一读写版本。</para>
	/// </remarks>
	public const int Jl_ERR_IMG_WRVERS = 5273;

	/// <summary>序列化项不含有效 XLD 对象：反序列化项不是合法的 XLD（轮廓）对象。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>序列化流中对象类型与期望 XLD 不符。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_XLD_WRVERS</c>、<c>Jl_ERR_XLD_DATA_TOO_LARGE</c> 同族。</para>
	/// </remarks>
	public const int Jl_ERR_XLD_NOSITEM = 5274;

	/// <summary>XLD 版本不支持：序列化数据中的 XLD 版本号超出支持范围。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>XLD 版本不匹配。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_XLD_NOSITEM</c> 区分；统一读写版本。</para>
	/// </remarks>
	public const int Jl_ERR_XLD_WRVERS = 5275;

	/// <summary>序列化项不含有效对象：通用对象序列化项无法识别为任何合法对象。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读取对象序列化流时项非法，属该族里最笼统的一条（不限定 region/image/XLD）。</para>
	///   <para><b>排查方向</b>先看是否应命中更具体的 NOSITEM（<c>Jl_ERR_REG_NOSITEM</c> 等 5270-5274）。</para>
	/// </remarks>
	public const int Jl_ERR_OBJ_NOSITEM = 5276;

	/// <summary>对象版本不支持：通用序列化对象的版本号超出支持范围。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对象序列化版本不匹配。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_OBJ_NOSITEM</c> 配对；统一读写版本。</para>
	/// </remarks>
	public const int Jl_ERR_OBJ_WRVERS = 5277;

	/// <summary>XLD 数据过大：仅 Vision XL 版本才可读取的超大数据量 XLD 对象。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>当前运行版本的许可等级不足以承载该 XLD 数据规模。</para>
	///   <para><b>排查方向</b>需升级到 Vision XL；注意其数值 5678 明显偏离序列化族的 5270-5277 连续段，属独立分配码[待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_XLD_DATA_TOO_LARGE = 5678;

	/// <summary>检测到意外对象：操作收到的对象类型与当前上下文预期不符。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>把不匹配类型的图标对象传给算子。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_OBJ_NOSITEM</c>（序列化项层面）区分，本码偏运行时类型检查。</para>
	/// </remarks>
	public const int Jl_ERR_OBJ_UNEXPECTED = 5279;

	/// <summary>文件未以文本方式打开：按文本方式读写却以非文本模式打开该文件。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>调用文本读写接口但打开模式不是 text。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_FNOBF</c> 对应（二进制未以二进制打开）；核对打开文件时的 mode 标志。</para>
	/// </remarks>
	public const int Jl_ERR_FNOTF = 5280;

	/// <summary>文件未以二进制方式打开：按二进制读写却以非二进制模式打开该文件。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>二进制接口用于文本模式打开的句柄。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_FNOTF</c> 配对；打开时选对模式。</para>
	/// </remarks>
	public const int Jl_ERR_FNOBF = 5281;

	/// <summary>无法创建目录：mkdir 类操作失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>路径已存在、权限不足或父目录缺失。</para>
	///   <para><b>排查方向</b>检查目标路径与写权限；与 <c>Jl_ERR_DIRRM</c>（删除目录失败）配对。</para>
	/// </remarks>
	public const int Jl_ERR_DIRCR = 5282;

	/// <summary>无法删除目录：rmdir 类操作失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>目录非空、不存在或权限不足。</para>
	///   <para><b>排查方向</b>先清空目录或核对权限；与 <c>Jl_ERR_DIRCR</c>（创建目录失败）配对。</para>
	/// </remarks>
	public const int Jl_ERR_DIRRM = 5283;

	/// <summary>无法获取当前工作目录：查询 cwd 的系统调用失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>获取当前目录时系统调用失败，少见，多为运行环境异常。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_SETCWD</c>（设置目录失败）配对。</para>
	/// </remarks>
	public const int Jl_ERR_GETCWD = 5284;

	/// <summary>无法设置当前工作目录：chdir 到指定目录失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>目标目录不存在或不可进入。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_GETCWD</c> 配对；确认目录路径有效。</para>
	/// </remarks>
	public const int Jl_ERR_SETCWD = 5285;

	/// <summary>需先调用 XInitThreads()：X11 多线程初始化未完成即使用图形相关能力。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>类 Unix/X11 环境下，未在任何 Xlib 调用之前调用 XInitThreads()。</para>
	///   <para><b>排查方向</b>属平台层要求，Windows 通常不涉及；是否在目标平台仍会触发[待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_XINIT = 5286;

	/// <summary>未打开图像采集设备：没有任何 frame grabber / 采集设备处于已打开状态。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>采集操作前未成功打开设备。</para>
	///   <para><b>排查方向</b>属图像采集（IA/FG）族起点，先打开设备再采集。</para>
	/// </remarks>
	public const int Jl_ERR_NFS = 5300;

	/// <summary>采集：色彩深度非法（IA = image acquisition）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>请求的采集彩色深度不被设备支持。</para>
	///   <para><b>排查方向</b>改用设备支持的灰度/彩色深度。</para>
	/// </remarks>
	public const int Jl_ERR_FGWC = 5301;

	/// <summary>采集：设备非法：指定或使用的采集设备不被接受。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>设备编号或句柄不对。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_UFG</c>（未知采集设备）区分：本码是设备被选中但非法。</para>
	/// </remarks>
	public const int Jl_ERR_FGWD = 5302;

	/// <summary>采集：无法确定视频格式：设备不支持探测或协商当前视频格式。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>查询采集格式时无法自动判定。</para>
	///   <para><b>排查方向</b>改为手动指定视频格式。</para>
	/// </remarks>
	public const int Jl_ERR_FGVF = 5303;

	/// <summary>采集：无视频信号：设备已就绪但输入端没有信号。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>相机/信号源未接入或未出图。</para>
	///   <para><b>排查方向</b>检查线缆、相机上电与出流；与 <c>Jl_ERR_FGF</c>（抓取失败）区分：本码强调无信号输入。</para>
	/// </remarks>
	public const int Jl_ERR_FGNV = 5304;

	/// <summary>未知图像采集设备：请求的设备类型或名称不被系统识别。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>驱动未安装或设备名拼错。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_FGWD</c>（设备非法）区分：本码是完全不认识该设备；确认对应采集卡/相机驱动已安装。</para>
	/// </remarks>
	public const int Jl_ERR_UFG = 5305;

	/// <summary>采集：抓取图像失败（grab 失败）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>发起抓图后未能得到一帧，可能因超时、信号中断或设备异常。</para>
	///   <para><b>排查方向</b>先看设备状态与触发模式；与 <c>Jl_ERR_FGNV</c>（无信号）区分：本码是尝试抓取但未成功。采集族 5300-5310 整体属 framegrabber 能力域。</para>
	/// </remarks>
	public const int Jl_ERR_FGF = 5306;

	/// <summary>采集：所选分辨率非法：请求的分辨率不被该采集设备支持（IA = image acquisition）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>打开或配置设备时设置了其能力列表之外的分辨率。</para>
	///   <para><b>排查方向</b>先枚举设备实际支持的分辨率再设置；与 <c>Jl_ERR_FGVF</c>（无法确定视频格式）区分：本码是已协商到格式但分辨率选错。</para>
	/// </remarks>
	public const int Jl_ERR_FGWR = 5307;

	/// <summary>采集：所选图像子区域（part）非法：抓取区域超出设备可输出范围。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>为采集设置了设备无法提供的图像子区域，如越界或未对齐到设备要求。</para>
	///   <para><b>排查方向</b>核对手动设置的 part 边界与设备有效成像区；与 <c>Jl_ERR_FGPART</c>（参数类型非法）区分：本码是区域取值不对，不是类型不对。</para>
	/// </remarks>
	public const int Jl_ERR_FGWP = 5308;

	/// <summary>采集：所选像素比非法：设备的像素宽高比设置不被支持。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对非方像素设备设置了其不支持的像素比（pixel ratio）。</para>
	///   <para><b>排查方向</b>改回方像素或设备手册标明的像素比；本族三个 "wrong X chosen" 连号可按后缀对号：5307 分辨率、5308 子区域、5309 像素比。</para>
	/// </remarks>
	public const int Jl_ERR_FGWPR = 5309;

	/// <summary>采集：句柄非法：传入的采集设备句柄无效（IA = image acquisition）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>用未成功打开或已被释放的设备句柄发起采集操作。</para>
	///   <para><b>排查方向</b>确认打开设备成功后再取其句柄；与 <c>Jl_ERR_FGCL</c>（实例已关闭）区分：本码强调句柄从未有效或已作废。</para>
	/// </remarks>
	public const int Jl_ERR_FGWH = 5310;

	/// <summary>采集：设备实例非法：实例可能已经被关闭。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对已关闭（含重复关闭后）的设备实例继续调用采集功能。</para>
	///   <para><b>排查方向</b>检查打开/关闭次序，杜绝先关后用；与 <c>Jl_ERR_FGWH</c>（句柄非法）区分：本码是"曾经有效、现已关闭"。</para>
	/// </remarks>
	public const int Jl_ERR_FGCL = 5311;

	/// <summary>采集：设备初始化失败：图像采集设备无法完成初始化。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>打开设备后初始化阶段被占用、驱动异常或参数组合不兼容。</para>
	///   <para><b>排查方向</b>先确认设备未被其它进程占用、驱动状态正常；与 <c>Jl_ERR_FGDV</c>（设备忙）区分：设备忙是本码最常见的具体成因之一。</para>
	/// </remarks>
	public const int Jl_ERR_FGNI = 5312;

	/// <summary>采集：不支持外部触发：设备无法使用硬件触发信号线。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>在不具备外触发能力的设备上启用外部触发模式。</para>
	///   <para><b>排查方向</b>改用连续采集或软件触发；按设备型号确认触发输入端子的电气规格后再接线。</para>
	/// </remarks>
	public const int Jl_ERR_FGET = 5313;

	/// <summary>采集：相机输入线选择错误：多路复用输入（multiplex）的线路号不对。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>在带输入复用器的采集卡上选择了不存在或未接线的输入线。</para>
	///   <para><b>排查方向</b>对照采集卡实际接线核对复用的输入线编号；与 <c>Jl_ERR_FGPT</c>（端口错误）区分：输入线是信号复用层面，端口是物理接口层面。</para>
	/// </remarks>
	public const int Jl_ERR_FGLI = 5314;

	/// <summary>采集：色彩空间非法：请求的色彩空间不被设备输出。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>设置了设备不支持的色彩空间（如 RGB/YUV 之类的通道组织方式）。</para>
	///   <para><b>排查方向</b>改用设备能直接输出的色彩空间；与 <c>Jl_ERR_FGWC</c>（色彩深度非法）区分：本码管通道的组织方式，那里管每通道的位深。</para>
	/// </remarks>
	public const int Jl_ERR_FGCS = 5315;

	/// <summary>采集：端口错误：指定的采集端口不存在或未被占用。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>多端口设备上选择了错误的物理端口号。</para>
	///   <para><b>排查方向</b>枚举设备实际可用端口后重选；与 <c>Jl_ERR_FGLI</c>（输入线错误）区分：本码在物理接口层面报错。</para>
	/// </remarks>
	public const int Jl_ERR_FGPT = 5316;

	/// <summary>采集：相机类型错误：声明的相机类型与实际设备不符。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>打开设备时指定的相机类型/接口协议与真实硬件不匹配。</para>
	///   <para><b>排查方向</b>核对相机的接口与驱动类别后重开；与 <c>Jl_ERR_UFG</c>（未知采集设备）区分：本码是系统认识该类别但类型选错。</para>
	/// </remarks>
	public const int Jl_ERR_FGCT = 5317;

	/// <summary>采集：超出采集设备类数量上限：系统内登记的 acquisition device class 过多。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>注册或打开的采集设备类数量超过运行时内部上限。</para>
	///   <para><b>排查方向</b>释放不再使用的设备类后重新枚举；与 <c>Jl_ERR_IOME</c>（超出 dio 类上限）区分：本码属采集族，那边属 IO 族。</para>
	/// </remarks>
	public const int Jl_ERR_FGTM = 5318;

	/// <summary>采集：设备忙：采集设备正被占用，无法响应本次操作。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>设备被其它进程、其它实例或尚未结束的上一操作占住。</para>
	///   <para><b>排查方向</b>关闭占用的程序或等待上一操作结束；与 <c>Jl_ERR_IODBUSY</c>（IO 族设备忙）同义但族不同，先按错误码所在族定位是采集还是 IO 设备。</para>
	/// </remarks>
	public const int Jl_ERR_FGDV = 5319;

	/// <summary>采集：不支持异步抓取：设备或驱动只提供同步 grab。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对无异步能力的设备发起异步抓取请求。</para>
	///   <para><b>排查方向</b>退回同步抓取流程；换设备前先确认其是否声明支持异步出图。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_FGASYNC = 5320;

	/// <summary>采集：参数不受支持：设备根本没有这个参数。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>设置或查询设备参数表中不存在的参数。</para>
	///   <para><b>排查方向</b>先查设备参数能力再调用；与 <c>Jl_ERR_FGPARNA</c>（参数在当前配置下不可用）区分：那边参数存在只是暂时不可用；再与 <c>Jl_ERR_FGPARV</c>（参数值非法）区分：那边参数存在且可用、只是取值不对。</para>
	/// </remarks>
	public const int Jl_ERR_FGPARAM = 5321;

	/// <summary>采集：超时：等待设备操作或出图响应超过时限。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>设备在超时窗口内未响应，常见于带宽不足、触发丢失或设备假死。</para>
	///   <para><b>排查方向</b>降低触发频率或核查链路带宽；与 <c>Jl_ERR_FGF</c>（抓取失败）区分：本码明确给出了失败原因是超时；与 <c>Jl_ERR_IOTIMEOUT</c> 区分靠族前缀。</para>
	/// </remarks>
	public const int Jl_ERR_FGTIMEOUT = 5322;

	/// <summary>采集：增益非法：设置的增益值无效或当前不允许。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>增益超出设备允许范围，或处于自动增益模式下手动改增益。</para>
	///   <para><b>排查方向</b>先关自动增益再按设备范围设值；与 <c>Jl_ERR_FGPARV</c>（参数值非法）区分：本码专指增益这一参数。</para>
	/// </remarks>
	public const int Jl_ERR_FGGAIN = 5323;

	/// <summary>采集：场（field）非法：视频场/隔行相关设置无效。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对场模式或场序的设置不被当前设备与信号格式接受。</para>
	///   <para><b>排查方向</b>逐行模式下不要设场参数；本码按视频场解读，若实际语义为其他含义需以真机复测为准。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_FGFIELD = 5324;

	/// <summary>采集：参数类型非法：传入类型与设备要求的类型不符。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>以错误的类型设置采集参数（如该传整数的位置传了其他类型）。[待实测]</para>
	///   <para><b>排查方向</b>义项存在歧义：名称后缀 PART 指向区域、说明文字指向参数类型；先按类型不匹配排查，仍复现时再核对与图像区域（part）相关的取值；与 <c>Jl_ERR_FGWP</c>（图像区域选择错误）区分。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_FGPART = 5325;

	/// <summary>采集：参数值非法：类型正确但取值超出范围或不在允许枚举内。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>给存在的参数传了越界或不合法的取值。</para>
	///   <para><b>排查方向</b>查该参数的取值域后重设；采集族参数三兄弟按层次选码：不存在→<c>Jl_ERR_FGPARAM</c>（5321），配置下不可用→<c>Jl_ERR_FGPARNA</c>（5331），值不对→本码。</para>
	/// </remarks>
	public const int Jl_ERR_FGPARV = 5326;

	/// <summary>采集：功能不支持：设备或驱动未实现所请求的整个功能。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>调用了设备能力之外的采集功能（而非单纯参数问题）。</para>
	///   <para><b>排查方向</b>换用该设备支持的功能路径；与 <c>Jl_ERR_FGPARAM</c>（5321）区分：本码是整个函数级不支持，那边只是单个参数；与 <c>Jl_ERR_IOFNS</c>（5361）靠族前缀区分。</para>
	/// </remarks>
	public const int Jl_ERR_FGFNS = 5327;

	/// <summary>采集：接口版本不兼容：驱动与库之间的采集接口版本号对不上。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>混装了不同代际的采集驱动与运行时。</para>
	///   <para><b>排查方向</b>把驱动、原生库更新到同一配套版本；与 <c>Jl_ERR_IOIVERS</c>（5351）同义但族不同：那码出现在 IO 设备上。</para>
	/// </remarks>
	public const int Jl_ERR_FGIVERS = 5328;

	/// <summary>采集：设置参数值失败：写入动作本身被设备拒绝。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>参数只读、设备处于不允许写入的状态，或写入通道故障。</para>
	///   <para><b>排查方向</b>先查询该参数是否可读不可写，再确认设备在线；与 <c>Jl_ERR_FGPARV</c>（5326）区分：那边是值不合法，这边值可能合法但写不进去；读方向对应 <c>Jl_ERR_FGGETPAR</c>（5330）。</para>
	/// </remarks>
	public const int Jl_ERR_FGSETPAR = 5329;

	/// <summary>采集：查询参数失败：设备未能返回当前参数设置。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>参数不可读、设备未响应查询或已掉线。</para>
	///   <para><b>排查方向</b>确认设备在线后重查；写方向对应 <c>Jl_ERR_FGSETPAR</c>（5329）；若伴随掉线先看 <c>Jl_ERR_FGDEVLOST</c>（5335）。</para>
	/// </remarks>
	public const int Jl_ERR_FGGETPAR = 5330;

	/// <summary>采集：参数在当前配置下不可用：参数存在，但被现用模式禁用。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>某些参数只在特定模式下可读写（如自动模式锁住手动量时）。</para>
	///   <para><b>排查方向</b>先切换使该参数生效的模式再设置；与 <c>Jl_ERR_FGPARAM</c>（5321，参数根本不存在）区分；IO 族同型码为 <c>Jl_ERR_IOPARNA</c>（5360）。</para>
	/// </remarks>
	public const int Jl_ERR_FGPARNA = 5331;

	/// <summary>采集：设备未能正常关闭：关闭调用返回错误。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>关闭时驱动挂起、资源仍被占用或设备已异常。</para>
	///   <para><b>排查方向</b>先停止采集请求再关闭，避免重复关闭；仍失败可能需重启进程释放设备；与 <c>Jl_ERR_FGCL</c>（5311，实例已关闭）区分：本码是关闭动作本身失败；IO 族同型码为 <c>Jl_ERR_IOCLOSE</c>（5369）。</para>
	/// </remarks>
	public const int Jl_ERR_FGCLOSE = 5332;

	/// <summary>采集：相机配置文件无法打开。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>加载相机配置时文件路径不存在、无权限或内容损坏。</para>
	///   <para><b>排查方向</b>核对配置文件路径与访问权限；与文件族 <c>Jl_ERR_LIB_FILE_OPEN</c>（5501，图像文件打不开）区分：本码特指相机配置文件。</para>
	/// </remarks>
	public const int Jl_ERR_FGCAMFILE = 5333;

	/// <summary>采集：回调类型不支持：注册了设备不接受的回调方式。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>以设备不支持的回调类型挂接采集通知。</para>
	///   <para><b>排查方向</b>改用设备支持的回调方式或轮询取图；本库托管层已不暴露采集回调接口，此码多来自原生侧。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_FGCALLBACK = 5334;

	/// <summary>采集：设备丢失：与采集设备的连接在操作中途断开。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>线缆被拔、设备断电或网络中断导致设备离线。</para>
	///   <para><b>排查方向</b>恢复物理连接后重新枚举并打开设备；与 <c>Jl_ERR_FGCL</c>（5311，主动关闭）区分：本码是被动掉线；IO 族同型码为 <c>Jl_ERR_IODEVLOST</c>（5366）。</para>
	/// </remarks>
	public const int Jl_ERR_FGDEVLOST = 5335;

	/// <summary>采集：抓取被中止：进行中的 grab 被取消或被打断。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>主动中止了采集请求，或驱动在抓取中途放弃了该请求。</para>
	///   <para><b>排查方向</b>确认是谁发起了中止；与 <c>Jl_ERR_FGF</c>（5306，抓取失败）区分：那边是尝试抓取但未成功，本码是抓取被终止；IO 族同型码为 <c>Jl_ERR_IOABORTED</c>（5364）。</para>
	/// </remarks>
	public const int Jl_ERR_FGABORTED = 5336;

	/// <summary>IO：操作超时：等待 IO 设备响应或信号变化超过时限。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对数字 IO 设备的读写在时限内未完成。</para>
	///   <para><b>排查方向</b>检查信号线接通与设备地址配置；采集族同型码为 <c>Jl_ERR_FGTIMEOUT</c>（5322），先看错误发生在哪一族设备上。</para>
	/// </remarks>
	public const int Jl_ERR_IOTIMEOUT = 5350;

	/// <summary>IO：接口版本不兼容：IO 驱动与库的接口版本号对不上。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>混装了不同代际的 IO 驱动与运行时。</para>
	///   <para><b>排查方向</b>统一到同一配套版本；采集族同型码为 <c>Jl_ERR_FGIVERS</c>（5328）。</para>
	/// </remarks>
	public const int Jl_ERR_IOIVERS = 5351;

	/// <summary>IO：句柄非法：传入的 IO 设备句柄无效。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>用未成功打开或已释放的 IO 设备句柄进行操作。</para>
	///   <para><b>排查方向</b>重新打开 IO 设备并确认句柄来源；采集族同型码为 <c>Jl_ERR_FGWH</c>（5310）。</para>
	/// </remarks>
	public const int Jl_ERR_IOWH = 5352;

	/// <summary>IO：设备忙：IO 设备正被占用，无法响应本次操作。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>设备被其它进程/实例占住，或上一操作尚未结束。</para>
	///   <para><b>排查方向</b>释放占用或等待后重试；采集族同型码为 <c>Jl_ERR_FGDV</c>（5319）。</para>
	/// </remarks>
	public const int Jl_ERR_IODBUSY = 5353;

	/// <summary>IO：用户权限不足：当前用户无权访问该 IO 设备。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>以受限账户操作需要特权的 IO 设备或驱动。</para>
	///   <para><b>排查方向</b>以提升权限的账户运行进程；与 <c>Jl_ERR_IODNA</c>（5363，驱动不可用）区分：那边是驱动缺失，这边是驱动在但权限不够。</para>
	/// </remarks>
	public const int Jl_ERR_IOIAR = 5354;

	/// <summary>IO：设备或通道不存在：按名字/编号找不到目标。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>操作了枚举列表之外的 IO 设备或通道号。</para>
	///   <para><b>排查方向</b>先枚举可用设备与通道再寻址；与 <c>Jl_ERR_IOPARNUM</c>（5358，参数编号非法）区分：本码丢的是设备/通道本身。</para>
	/// </remarks>
	public const int Jl_ERR_IONF = 5355;

	/// <summary>IO：参数类型非法：传入类型与 IO 参数要求的类型不符。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>以错误的类型读写 IO 设备参数。</para>
	///   <para><b>排查方向</b>按参数定义的类型传值；与 <c>Jl_ERR_IODATT</c>（5365，数据类型非法）区分：本码管参数类型，那边管通道数据本身的类型；采集族同型码为 <c>Jl_ERR_FGPART</c>（5325）。</para>
	/// </remarks>
	public const int Jl_ERR_IOPART = 5356;

	/// <summary>IO：参数值非法：类型正确但取值超出范围或不在允许枚举内。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>给存在的 IO 参数传了越界取值。</para>
	///   <para><b>排查方向</b>核对参数取值域；IO 族参数三兄弟按层次选码：不存在→<c>Jl_ERR_IOPARAM</c>（5359），配置下不可用→<c>Jl_ERR_IOPARNA</c>（5360），值不对→本码。</para>
	/// </remarks>
	public const int Jl_ERR_IOPARV = 5357;

	/// <summary>IO：参数编号非法：按编号寻参时编号越出有效范围。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>用序号访问 IO 参数，但序号超出该设备的参数个数。</para>
	///   <para><b>排查方向</b>先查询设备参数总数再按编号遍历；与 <c>Jl_ERR_IONF</c>（5355，设备/通道不存在）区分：本码丢的是参数索引，不是设备。</para>
	/// </remarks>
	public const int Jl_ERR_IOPARNUM = 5358;

	/// <summary>IO：参数不受支持：该 IO 设备根本没有这个参数。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>设置或查询设备参数表中不存在的 IO 参数。</para>
	///   <para><b>排查方向</b>先查设备参数能力再调用；与 <c>Jl_ERR_IOPARNA</c>（5360，参数存在但当前配置下不可用）区分；采集族同型码为 <c>Jl_ERR_FGPARAM</c>（5321）。</para>
	/// </remarks>
	public const int Jl_ERR_IOPARAM = 5359;

	/// <summary>IO：参数在当前配置下不可用：参数存在，但被现用模式禁用。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>所请求的 IO 参数只在特定配置下开放。</para>
	///   <para><b>排查方向</b>先改变配置/模式再访问该参数；与 <c>Jl_ERR_IOPARAM</c>（5359，参数不存在）区分；采集族同型码为 <c>Jl_ERR_FGPARNA</c>（5331）。</para>
	/// </remarks>
	public const int Jl_ERR_IOPARNA = 5360;

	/// <summary>IO：功能不支持：该 IO 设备或驱动未实现所请求的功能。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>调用了超出设备能力的 IO 功能。</para>
	///   <para><b>排查方向</b>换受支持的功能路径或换设备；采集族同型码为 <c>Jl_ERR_FGFNS</c>（5327）。</para>
	/// </remarks>
	public const int Jl_ERR_IOFNS = 5361;

	/// <summary>IO：超出数字 IO 设备类数量上限：登记的 dio class 过多。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>注册的数字 IO（dio）设备类数量超过运行时内部上限。</para>
	///   <para><b>排查方向</b>释放不再使用的设备类；采集族同型码为 <c>Jl_ERR_FGTM</c>（5318，采集设备类超限）。</para>
	/// </remarks>
	public const int Jl_ERR_IOME = 5362;

	/// <summary>IO：驱动不可用：IO 设备的驱动没有安装或加载失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>系统内找不到该 IO 设备对应的驱动。</para>
	///   <para><b>排查方向</b>安装/修复驱动后重试；与 <c>Jl_ERR_IOIAR</c>（5354，权限不足）区分：先判驱动在不在，再判权限够不够。</para>
	/// </remarks>
	public const int Jl_ERR_IODNA = 5363;

	/// <summary>IO：操作被中止：进行中的 IO 操作被取消。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>主动中止了 IO 操作，或驱动放弃了该请求。</para>
	///   <para><b>排查方向</b>与 <c>Jl_ERR_IOTIMEOUT</c>（5350）区分：超时是被动等待过期，本码是被主动终止；采集族同型码为 <c>Jl_ERR_FGABORTED</c>（5336，特指抓取被中止）。</para>
	/// </remarks>
	public const int Jl_ERR_IOABORTED = 5364;

	/// <summary>IO：数据类型非法：收发的数据类型与通道配置不匹配。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读写通道时使用了通道不支持的数据类型。</para>
	///   <para><b>排查方向</b>按通道定义的数据类型收发；与 <c>Jl_ERR_IOPART</c>（5356，参数类型非法）区分：本码管数据本身，那边管参数。</para>
	/// </remarks>
	public const int Jl_ERR_IODATT = 5365;

	/// <summary>IO：设备丢失：与 IO 设备的连接在操作中途断开。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>IO 设备断电、断线或被系统移除。</para>
	///   <para><b>排查方向</b>恢复连接后重新打开设备；采集族同型码为 <c>Jl_ERR_FGDEVLOST</c>（5335）。</para>
	/// </remarks>
	public const int Jl_ERR_IODEVLOST = 5366;

	/// <summary>IO：设置参数值失败：写入动作本身被设备拒绝。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>IO 参数只读，或设备当前状态不允许写入。</para>
	///   <para><b>排查方向</b>先确认参数可写再设置；与 <c>Jl_ERR_IOPARV</c>（5357，值不合法）区分：本码是写通道失败；读方向对应 <c>Jl_ERR_IOGETPAR</c>（5368），采集族同型码为 <c>Jl_ERR_FGSETPAR</c>（5329）。</para>
	/// </remarks>
	public const int Jl_ERR_IOSETPAR = 5367;

	/// <summary>IO：查询参数失败：设备未能返回当前 IO 参数设置。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>IO 参数不可读，或设备未响应查询。</para>
	///   <para><b>排查方向</b>确认设备在线后重查；写方向对应 <c>Jl_ERR_IOSETPAR</c>（5367），采集族同型码为 <c>Jl_ERR_FGGETPAR</c>（5330）。</para>
	/// </remarks>
	public const int Jl_ERR_IOGETPAR = 5368;

	/// <summary>IO：设备未能正常关闭：IO 设备关闭调用返回错误。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>关闭时驱动挂起或资源仍被占用。</para>
	///   <para><b>排查方向</b>结束未完成的 IO 操作后再关闭，避免重复关闭；采集族同型码为 <c>Jl_ERR_FGCLOSE</c>（5332）。</para>
	/// </remarks>
	public const int Jl_ERR_IOCLOSE = 5369;

	/// <summary>图像类型不受支持（JPEG-XR，取值 5400），内嵌文本 <c>Image type is not supported</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>要按 JPEG-XR 处理的图像类型（色彩/像素组织）不在 JXR 支持集内。常量名 UNSUPPORTED_FORMAT 与文本 IMAGE TYPE 合读：指图像类型层面不被接受，是能力/兼容问题而非数据损坏。属 5400~5404 JPEG-XR 运行时档的族首码。与 5401（传进 filter 的像素格式非法）方向不同，本码先判定图像类型能否进入 JXR。</para>
	///   <para><b>可达性</b>来自原生图像读写的 JPEG-XR 编解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>把图像转成 JXR 支持的类型再处理，或改用能承载该类型的其他格式；重试同一输入无效 [待实测：JXR 接受的图像类型集]。</para>
	/// </remarks>
	public const int Jl_ERR_JXR_UNSUPPORTED_FORMAT = 5400;

	/// <summary>传入像素格式非法（JPEG-XR，取值 5401），内嵌文本 <c>Invalid pixel format passed to filter function</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>文本点明是"传给 filter 函数的像素格式非法"——即 JXR 编解码里做格式转换/滤波的环节收到了不支持的 pixel format 描述。属 5400~5404 JPEG-XR 运行时档。与 5400（图像类型不受支持）方向不同：5400 说图像本身类型不被 JXR 支持，本码说传进转换环节的格式描述不合法，更偏参数层。</para>
	///   <para><b>可达性</b>来自原生图像读写的 JPEG-XR 编解码（像素格式转换/filter）路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对送进 JXR 的像素格式（通道布局、位深）是否为受支持组合；不支持的格式先转成常规 RGB/灰度再走 JXR [待实测：filter 接受的确切 pixel format 集]。</para>
	/// </remarks>
	public const int Jl_ERR_JXR_INVALID_PIXEL_FORMAT = 5401;

	/// <summary>JPEG-XR 内部错误（取值 5402），内嵌文本 <c>Internal JpegXR error</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>JXR 编解码内部出错，属于不该被调用方直接处理的兜底错。常量名与文本都指向"内部"，说明它不携带可操作的具体原因。属 5400~5404 JPEG-XR 运行时档。与 5405（EC_ERROR，库级未指明错）不同：本码偏运行时侧的内部异常，5405 偏底层库枚举的默认档 [待实测：两内部/未知码的实际分层归属]。</para>
	///   <para><b>可达性</b>来自原生图像读写的 JPEG-XR 编解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>不当作参数错去翻参数；取 <c>JlNativeApi.GetErrorMessage</c> 文本、连同输入与版本信息记录上报，必要时换输入复现。</para>
	/// </remarks>
	public const int Jl_ERR_JXR_INTERNAL_ERROR = 5402;

	/// <summary>输出格式串语法错误（JPEG-XR，取值 5403），内嵌文本 <c>Syntax error in output format string</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>给 JPEG-XR 编码指定的"输出格式串"存在语法错误（词法/结构不合该格式串的书写规范），是"参数写法"层的问题，不是图像数据坏。属 5400~5404 JPEG-XR 运行时档。与通用族 5508（bad file format specification）同一类"规格写错"，但本码限定在 JXR 编码格式串的解析。</para>
	///   <para><b>可达性</b>来自原生把图像编码为 JPEG-XR 时解析输出格式参数的路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对格式串的拼写、分隔符与取值范围是否符合 JXR 输出格式约定；具体合法语法 [待实测：格式串的语法规则]。</para>
	/// </remarks>
	public const int Jl_ERR_JXR_FORMAT_SYNTAX_ERROR = 5403;

	/// <summary>通道数超限（JPEG-XR，取值 5404），内嵌文本 <c>Maximum number of channels exceeded</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>图像通道数超过 JPEG-XR 允许的最大值（在 RGB/CMYK 等固定色彩模型外再挂过多 alpha/附加通道时易触顶）。属 5400~5404 JPEG-XR 运行时档（区别于 5405~5409 的 EC_ 库透传码）。与 5507（各通道尺寸须一致）不同：这里限的是通道条数，不是尺寸一致性。</para>
	///   <para><b>可达性</b>来自原生把多通道图像编码为 JPEG-XR 或按其色彩模型解析的路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>减少通道数或拆分到多文件；JPEG-XR 对额外通道有上限，超限不靠重试解决 [待实测：允许的最大通道数]。</para>
	/// </remarks>
	public const int Jl_ERR_JXR_TOO_MANY_CHANNELS = 5404;

	/// <summary>JPEG-XR 库未指明的错误（取值 5405），内嵌文本 <c>Unspecified error in JXR library</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>JXR 库回了一个兜底的"未知错误"，没归入更细的码。EC_ 前缀＝透传自底层库，通常是库内错误枚举的默认/杂项档。属 5400~5409 JPEG-XR 族。因不带具体原因，光凭本码无法定位，需结合 <c>JlNativeApi.GetErrorMessage</c> 取文本或看同批更细的 JXR 码。</para>
	///   <para><b>可达性</b>来自原生图像读写的 JPEG-XR 编解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先取原生错误消息补出细节；换合法完整的 JXR 输入试跑，若仍报本码则属库内异常，记录后上报 [待实测：本兜底码在哪些调用点触发]。</para>
	/// </remarks>
	public const int Jl_ERR_JXR_EC_ERROR = 5405;

	/// <summary>JPEG-XR 魔数错误（取值 5406），内嵌文本 <c>Bad magic number in JXR library</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>JXR 库读到的文件头标识（magic）与 JPEG-XR 规范不符，即"不是 JXR"。EC_ 前缀＝透传自底层库。属 5400~5409 JPEG-XR 族的族首档，比 5409（格式坏，头已认出）更早一步：先过 magic，才谈得上后续格式校验。与 PCX/GIF/SUN 各自的"认不出文件"码同型但归 JXR。</para>
	///   <para><b>可达性</b>来自原生图像读写的 JPEG-XR 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对文件确为 JPEG-XR 而非误标扩展名；若本应是 JXR 却报此码，多半文件头被截断/改写，重取原始档。</para>
	/// </remarks>
	public const int Jl_ERR_JXR_EC_BADMAGIC = 5406;

	/// <summary>JPEG-XR 特性未实现（取值 5407），内嵌文本 <c>Feature not implemented in JXR library</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>请求用到的某个 JPEG-XR 特性在所链的 JXR 库里没实现。EC_ 前缀＝透传自底层库。属 5400~5409 JPEG-XR 族。这是能力缺口而非数据错：JPEG-XR 的某些子集/可选特性常只有部分实现，本码不质疑你的参数值合法与否，只说"这条路没做" [待实测：哪些特性未实现]。</para>
	///   <para><b>可达性</b>来自原生图像读写的 JPEG-XR 编解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>避免用到未实现的 JXR 特性（改用基础档位或换成运行时确定支持的编码选项）；重试同一无效。</para>
	/// </remarks>
	public const int Jl_ERR_JXR_EC_FEATURE_NOT_IMPLEMENTED = 5407;

	/// <summary>JPEG-XR 库文件读写错误（取值 5408），内嵌文本 <c>File read/write error in JXR library</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>JXR 编解码库在读写其承载文件时返回 IO 错。EC_ 前缀说明是透传自底层库的错误枚举。属 5400~5409 JPEG-XR 族的 IO 档。与运行时的通用文件码分工不同：5501（打不开）、5502（提前 EOF）在图像库层，本码在 JXR 库内部读写层；谁先把错抛出取决于调用点 [待实测：两层对同一 IO 故障的归属优先级]。</para>
	///   <para><b>可达性</b>来自原生图像读写的 JPEG-XR 编解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对文件可读可写、空间充足、未被占用；跨目录/网络盘时尤其易现，换本地路径重试。</para>
	/// </remarks>
	public const int Jl_ERR_JXR_EC_IO = 5408;

	/// <summary>JPEG-XR 文件格式坏（取值 5409），内嵌文本 <c>Bad file format in JXR library</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌的 JXR 编解码库把输入判为"格式不合法"。EC_ 前缀表示这码是从底层 JXR 库错误枚举透传上来的，非运行时自造，故其边界随所链的库版本而定。属 5400~5409 JPEG-XR 族。与 5406（magic 不符，头部标识都不对）不同：本码是结构已被认出但格式参数/内容非法；与运行时侧 5506（格式不受支持）也不是一回事。</para>
	///   <para><b>可达性</b>来自原生图像读写的 JPEG-XR 编解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认文件确为合法 JPEG-XR 且未损坏；非 JXR 内容误标、或库版本对某变体不兼容都会命中 [待实测：所链 JXR 库的确切版本与兼容范围]。</para>
	/// </remarks>
	public const int Jl_ERR_JXR_EC_BADFORMAT = 5409;

	/// <summary>关闭图像文件失败（取值 5500），内嵌文本 <c>Error while closing the image file</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>图像库在 close 阶段返回错误。属 5500~5502 图像文件库族，与 open（5501）相对。写侧尤其要警惕：关闭失败常意味着缓冲区未刷盘、文件可能不完整（进而下次读又撞 5502 EOF）。成因（句柄已失效、磁盘满、被外部断开）不在码里 [待实测：写侧刷盘失败是否也归本码]。</para>
	///   <para><b>可达性</b>来自原生图像读写结束、释放文件句柄的步骤。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>写侧收到本码要重新核验输出文件是否完整可用；避免重复关闭同一句柄；排查磁盘空间与文件占用。</para>
	/// </remarks>
	public const int Jl_ERR_LIB_FILE_CLOSE = 5500;

	/// <summary>打开图像文件失败（取值 5501），内嵌文本 <c>Error while opening the image file</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>图像库在 open 阶段就失败，尚未进入格式解码——路径不存在、无权限、被占用/锁定等文件层原因。属 5500~5502 图像文件库族的入口档：open（5501）失败则读不到 EOF（5502）、关不掉 close（5500）。与相机配置文件打不开是不同码，本码专指图像文件。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 打开文件句柄的最前置步骤。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先核对路径与扩展名是否存在、进程是否有读写权限、文件是否被其他程序独占；写侧另查目标目录是否可写。</para>
	/// </remarks>
	public const int Jl_ERR_LIB_FILE_OPEN = 5501;

	/// <summary>图像文件提前结束（取值 5502），内嵌文本 <c>Premature end of the image file</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>图像库读文件时未到应有结尾就撞上 EOF，即文件被截断或不完整。属 5500~5502 图像文件库族的最底层 IO 码，跨格式通用：它只说"文件短了"，不指明是哪种格式。各格式族另有更细的 EOF 码，如 GIF 的 5524；能定位到具体格式时优先看族内码。</para>
	///   <para><b>可达性</b>来自原生图像读写的底层文件读取路径，与具体解码格式无关。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对文件大小是否与预期相符（下载/拷贝是否中断）；重取完整档，或检查是否写侧未刷完就关。</para>
	/// </remarks>
	public const int Jl_ERR_LIB_UNEXPECTED_EOF = 5502;

	/// <summary>图像幅面超该格式上限（取值 5503），内嵌文本 <c>Image dimensions too large for this file format</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>图像的宽/高超过目标文件格式本身能表达的尺寸上限（如某些 16 位字段格式的幅面顶）。常量名 IDTL ≈ "Image Dimensions Too Large"。属 5500~5508 通用文件族。与 5504 <c>Jl_ERR_ITLHV</c> 区别在"限"的来源：本码卡在格式，5504 卡在软件版本——本码换个能放大图的格式即可解，5504 不行。</para>
	///   <para><b>可达性</b>来自原生把图像写入受格式尺寸约束的路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>改用支持大尺寸的目标格式（各格式上限 [待实测]），或先裁切/降采样到该格式范围内再存。</para>
	/// </remarks>
	public const int Jl_ERR_IDTL = 5503;

	/// <summary>图像超出本版本上限（取值 5504），内嵌文本 <c>Image too large for this Vision version</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>图像幅面超过的是当前运行时版本自身的总量/尺寸限制（授权或体系上限），与文件格式能支持多大无关。常量名 ITLHV ≈ "Image Too Large for tHis Vision version"。属 5500~5508 通用文件族。关键区分：5503 <c>Jl_ERR_IDTL</c> 是"超该文件格式上限"，本码是"超本软件版本上限"——换格式救不了本码。</para>
	///   <para><b>可达性</b>来自原生读/写超大图像时对本版本允许幅面的检查。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>把大图切块/降采样到本版本允许范围内，或升级到允许更大尺寸的版本；具体上限值 [待实测：随版本/授权而异]。</para>
	/// </remarks>
	public const int Jl_ERR_ITLHV = 5504;

	/// <summary>图标对象过多（取值 5505），内嵌文本 <c>Too many iconic objects for this file format</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>要写入某个文件的图标对象（image/region/XLD 等 iconic 对象）数量超过该格式能承载的上限。常量名 TMIO 即 "Too Many Iconic Objects" 的缩写。属 5500~5508 通用文件族，是"写侧"容量约束：与 5503（单图幅面过大）不同，这里限制的是对象条数。</para>
	///   <para><b>可达性</b>来自原生把对象集合写入单文件的打包路径；多数图像格式每文件只承载有限对象。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>拆分到多个文件或改用支持多图/多对象的格式；本码是格式容量硬限，不靠重试解决 [待实测：各格式对象数上限具体值]。</para>
	/// </remarks>
	public const int Jl_ERR_TMIO = 5505;

	/// <summary>文件格式不受支持（取值 5506），内嵌文本 <c>File format is unsupported</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>被点名的格式在本运行时里根本没有对应读写实现——属能力/兼容问题，不是"参数写错"也不是"文件坏"。属 5500~5508 通用文件族。与 5508 分工：5508 是格式规格串本身写错，本码是格式合法但本版不支持；与 5234 <c>Jl_ERR_NFWKEF</c>（无法识别任何已知格式）也不同，那是读取时的嗅探失败。</para>
	///   <para><b>可达性</b>来自原生图像读写按指定格式分派时，格式未被登记支持的分支。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>换用本版本支持的格式读写；别指望靠改参数绕过——这是能力缺口。</para>
	/// </remarks>
	public const int Jl_ERR_FILE_FORMAT_UNSUPPORTED = 5506;

	/// <summary>各通道尺寸不一致（取值 5507），内嵌文本 <c>All channels must have equal dimensions</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>合成/读写多通道图像时，各通道的宽高必须完全相同，任一通道尺寸对不上即报此码。属 5500~5508 通用文件族。与 5503（单图幅面超格式上限）不同：本码针对"多通道之间"的尺寸一致性，而非绝对大小。</para>
	///   <para><b>可达性</b>来自原生把多通道图像写入受支持格式、或按通道合并时的校验路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先确认参与合成的各通道尺寸逐像素相等（裁剪/缩放后尤其易差 1 像素），对齐后再存。</para>
	/// </remarks>
	public const int Jl_ERR_INCONSISTENT_DIMENSIONS = 5507;

	/// <summary>文件格式指定不合法（取值 5508），内嵌文本 <c>Bad file format specification</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>调用者显式给出的"文件格式"参数本身不合法（写法/取值不被接受），而非文件内容有问题。属 5500~5508 通用文件族：与"格式不受支持"（5506，能力/兼容问题）不同，本码指"规格串写错了"。TIFF 侧另有一个同文本但限定到 TIFF 的对偶码 5557，两码按通用/专属分工。</para>
	///   <para><b>可达性</b>来自原生图像读写中显式指定格式参数的调用路径；未指定格式让运行时嗅探时一般不触发本码。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对格式名的拼写与大小写是否为本库登记的合法格式；格式名合法却仍失败，转查 5506（该格式本版本不支持）。</para>
	/// </remarks>
	public const int Jl_ERR_FILE_BAD_SPECIFICATION = 5508;

	/// <summary>并非 PCX 文件（取值 5510），内嵌文本 <c>File is no PCX-File</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>已按 PCX 路径开读，但文件头标识（首字节 magic）与 PCX 不符，即"自称是 PCX 却不是"。属 5510~5516 PCX 族的族首码：本码不过，编码方式（5511）、位面数（5512）、色表（5513）等都无从校验。与 GIF 的 5520、SUN 的 5530 同型。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 PCX 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对文件确为 PCX 而非误标扩展名；非 PCX 内容改由能嗅探真实格式的通用读取路径处理。</para>
	/// </remarks>
	public const int Jl_ERR_PCX_NO_PCX_FILE = 5510;

	/// <summary>编码方式未知（PCX，取值 5511），内嵌文本 <c>Unknown encoding</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>PCX 头里的 encoding 字段取值不被支持。PCX 只区分不压缩与逐扫描行 RLE 两档压缩方式，落到别值即本码。属 5510~5516 PCX 族：紧跟 5510（认文件）之后，编码方式先定，才谈得上按对应方式解扫描行（进而才有 5514 的 RLE 长度错）。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 PCX 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>多为头部被改坏或版本超出本解码器支持；用标准工具按支持的压缩方式重存，或确认文件确为 PCX [待实测：encoding 接受的具体取值集]。</para>
	/// </remarks>
	public const int Jl_ERR_PCX_UNKNOWN_ENCODING = 5511;

	/// <summary>位面数超过 4（PCX，取值 5512），内嵌文本 <c>More than 4 image plains</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>PCX 头的 planes 大于 4，超出该格式的上限（文本 plains 即 planes）。这是 5516 <c>Jl_ERR_PCX_PACKED_PIXELS</c>（位面数非法）里专挑"过大"这一方向的细码。属 5510~5516 PCX 族的头校验档，早于色表（5513）与扫描行（5514/5515）。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 PCX 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>planes 大于 4 通常意味着头部字段被改坏或并非真 PCX；重取合法档，超 4 位的真彩多平面变体本解码器一般不支持。</para>
	/// </remarks>
	public const int Jl_ERR_PCX_MORE_THAN_4_PLANES = 5512;

	/// <summary>色表魔数错误（PCX，取值 5513），内嵌文本 <c>Wrong magic in color table</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>PCX 尾部调色板块的前导标识字节不符合规范，说明调色板缺失或被改坏。属 5510~5516 PCX 族：色表校验位于头（5510~5512）之后、扫描行解码（5514）之前。与 SUN 的 5534、GIF 的 5523 同为"色表坏"，但 PCX 坏在标识字节而非表长。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 PCX 解码路径；仅带调色板的索引色 PCX 才会走到本校验，真彩 PCX 一般不触发。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认文件末尾确有调色板块且未被截断；被改写或裁剪掉的调色板用原始档重取 [待实测：接受的调色板标识字节值]。</para>
	/// </remarks>
	public const int Jl_ERR_PCX_COLORMAP_SIGNATURE = 5513;

	/// <summary>扫描行字节数错误（PCX，取值 5514），内嵌文本 <c>Wrong number of bytes in span</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>解一行 RLE 扫描数据时得到的字节数与头声明的 bytes per line×planes 不符。常量名 REPEAT_COUNT 指 PCX 的 RLE 重复计数机制——正是逐段展开扫描行时计数/长度对不上才报此码。属 5510~5516 PCX 族的压缩数据档：头（5510~5513）过了、进入扫描行解码才可能撞本码。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 PCX 解码路径；未压缩（encoding=0）的 PCX 一般走另一分支 [待实测：未压缩档是否也复用本码]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>多为 RLE 流被截断或某扫描行长度字段被改坏，重取完整原始 PCX；反复命中说明源文件压缩数据已损坏。</para>
	/// </remarks>
	public const int Jl_ERR_PCX_REPEAT_COUNT_SPANS = 5514;

	/// <summary>每像素位数非法（PCX，取值 5515），内嵌文本 <c>Wrong number of bits/pixels</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>PCX 头里的 bits per pixel 取值不被本解码路径接受——常量名偏向"太多"，文本更中性，指位深字段本身不合法（非 PCX 常见的 1/2/4/8 档位）。位深与 planes 一起决定最终色深，故本码常与位面数码（5512/5516）联动出现。属 5510~5516 PCX 族。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 PCX 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对 bpp 是否为格式支持档位且与 planes 乘积合理；此类 PCX 变体若本解码器不支持，改用能读该变体的工具先转存 [待实测：接受的确切位深集]。</para>
	/// </remarks>
	public const int Jl_ERR_PCX_TOO_MUCH_BITS_PIXEL = 5515;

	/// <summary>位面数错误（PCX，取值 5516），内嵌文本 <c>Wrong number of plains</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>PCX 头声明的 planes（位面数）与该格式允许的取值不符。文本 plains 是 planes 的旧式拼写，别当成"平原"。注意常量名 PACKED_PIXELS 与文本对不齐：名指"打包像素"这一 PCX 头字段，文本却在校验位面数——以文本为准理解触发条件更稳 [待实测：本码到底查 packed_pixels 还是 planes]。与 5512 "位面数大于 4" 是同一字段的两种越界方向。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 PCX 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对 planes 与 bits per pixel 的组合能否凑成合法色深（如 8bpp×3 面=24 位真彩）；被改坏的头重存。</para>
	/// </remarks>
	public const int Jl_ERR_PCX_PACKED_PIXELS = 5516;

	/// <summary>并非 GIF 文件（取值 5520），内嵌文本 <c>File is no GIF-File</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>已按 GIF 路径开读，但文件签名与 GIF 头部不符，即"自称是 GIF 却不是"。属 5520~5529 GIF 族的族首码：本码不过，版本（5521）、描述符（5522）、色表（5523）等都无从校验。与 PCX 的 5510、SUN 的 5530、XWD 族首码同型——都指"按该格式开读但认不出头部"。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 GIF 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对文件确为 GIF 而非误标扩展名（如把 PNG 改名成 .gif）；非 GIF 内容应交给能嗅探真实格式的通用读取路径。</para>
	/// </remarks>
	public const int Jl_ERR_GIF_NO_GIF_PICTURE = 5520;

	/// <summary>GIF 版本不认识（取值 5521），内嵌文本 <c>GIF: Wrong version</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>签名已认出是 GIF（否则报 5520），但版本字段不是本解码器接受的版本号。属 5520~5529 GIF 族的第二档，紧接文件识别、早于逻辑屏幕描述符（5522）。GIF 历史上主要有两代版本，具体接受哪几个版本串以本解码器实现为准 [待实测：支持的确切版本集]。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 GIF 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认文件非伪造/损坏的版本字段；版本头被改坏的 GIF 用标准工具重存即可。</para>
	/// </remarks>
	public const int Jl_ERR_GIF_BAD_VERSION = 5521;

	/// <summary>逻辑屏幕描述符错误（GIF，取值 5522），内嵌文本 <c>GIF: Wrong descriptor</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>GIF 头后的逻辑屏幕描述符（画布宽高、色深标志、背景/像素长宽比等）字段不合规范。它是紧接签名/版本（5520/5521）之后的第一处结构校验：本档过了才轮到全局色表（5523）。常量名 SCREEN_DESCRIPTOR 明确指逻辑屏幕块，非子图像描述符（后者归 5527）。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 GIF 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对头部宽高与色深标志位是否合法且非零；多由文件头被截断或字节序/转存损坏，重取原始档。</para>
	/// </remarks>
	public const int Jl_ERR_GIF_SCREEN_DESCRIPTOR = 5522;

	/// <summary>色表错误（GIF，取值 5523），内嵌文本 <c>GIF: Wrong color table</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>GIF 的全局或局部调色板数据非法：表大小与色表大小字段声明不符、缺失或长度越界。属 5520~5529 GIF 族的色表档；GIF 是索引色格式，色表坏则整帧颜色无从还原。同名角色在 SUN 是 5534、PCX 是 5513，各按各格式定位。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 GIF 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对头/描述符里声明的色表位深与实际调色板字节数是否匹配；被截断或被改写的调色板重取原始档。</para>
	/// </remarks>
	public const int Jl_ERR_GIF_COLORMAP = 5523;

	/// <summary>文件提前结束（GIF，取值 5524），内嵌文本 <c>GIF: Premature end of file</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>解 GIF 时还没读到结束符就撞上文件尾，即文件被截断。属 5520~5529 GIF 族的读取档。与通用的 5502 <c>Jl_ERR_LIB_UNEXPECTED_EOF</c> 语义相同但带格式归属：本码专指 GIF 解码路径内的提前 EOF。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 GIF 解码路径；下载不全、拷贝中断的 GIF 最常见。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>重新完整取回文件；若只需已读到的部分能否用取决于本解码器是否容错 [待实测：是否允许输出已解出的前若干帧]。</para>
	/// </remarks>
	public const int Jl_ERR_GIF_READ_ERROR_EOF = 5524;

	/// <summary>图像帧数不符（GIF，取值 5525），内嵌文本 <c>GIF: Wrong number of images</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>请求读取的帧数与 GIF 实际含的子图像数对不上：常量名偏向"不够"（要得多、给得少），文本是较中性的"数目不对"，二者同指帧计数越界。属 5520~5529 GIF 族的计数档。只读首帧时一般不触发，按序号取第 N 帧而 N 超界最易命中。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 GIF 解码路径，按帧/多图取图时才有意义。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先确认真实帧数再按合法序号取帧；文件被截断会让后续帧缺失而误判成帧数错，重取完整档。</para>
	/// </remarks>
	public const int Jl_ERR_GIF_NOT_ENOUGH_IMAGES = 5525;

	/// <summary>扩展块错误（GIF，取值 5526），内嵌文本 <c>GIF: Wrong image extension</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>GIF 的扩展块（如图形控制扩展 GCE、注释/应用扩展等）结构或长度不合法，解析器无法安全跳过或采纳其控制信息。属 5520~5529 GIF 族的控制信息档：帧几何错在 5527、数据错在 5528/5529，本码专指扩展块本身。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 GIF 解码路径；带动画控制信息（延时/透明索引）的 GIF 才会走扩展解析。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>多为块长度字节与实际子块不符（截断或改写所致），重取完整原始 GIF；若只关心首帧静态图，可试转存为无扩展的简单档 [待实测：本解码器是否可容忍未知扩展]。</para>
	/// </remarks>
	public const int Jl_ERR_GIF_ERROR_ON_EXTENSION = 5526;

	/// <summary>子图像位置/尺寸非法（GIF，取值 5527），内嵌文本 <c>GIF: Wrong left top width</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>子图像描述符里的 left/top/width（及高度）越出逻辑屏幕画布范围或彼此矛盾，即"这一帧摆在画布外或尺寸不合法"。常量名只列了 left/top/width，实为对该描述符几何字段的整体校验。属 5520~5529 GIF 族，位置校验在色表（5523）、扩展（5526）之后、数据体（5528/5529）之前。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 GIF 解码路径；含偏移的多帧/局部帧 GIF 更易触发。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对逻辑屏幕尺寸与各帧 left/top/width+尺寸是否落在画布内；被截断或工具导出异常的档重取或重导。</para>
	/// </remarks>
	public const int Jl_ERR_GIF_LEFT_TOP_WIDTH = 5527;

	/// <summary>LZW 码表环回（GIF，取值 5528），内嵌文本 <c>GIF: Cyclic index of table</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>解 LZW 压缩流时，码表里出现了自我指涉的索引（某词条的前缀链回到自身），说明压缩流或码表构建已错乱，无法继续还原像素。这是 GIF 数据层特有的结构错，位于 5529 图像数据错误的更细一档之前：先撞码表环回，未必会走到泛化的数据体校验。常量名 TABL 是 TABLE 的截断拼写，别当成两个不同码。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 GIF 解码路径，只在使用 LZW 压缩的 GIF 上出现。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>属压缩流被改坏，重取完整原始档；反复出现说明源文件本身编码即有问题，需用标准工具重新导出。</para>
	/// </remarks>
	public const int Jl_ERR_GIF_CIRCULAR_TABL_ENTRY = 5528;

	/// <summary>图像数据错误（GIF，取值 5529），内嵌文本 <c>GIF: Wrong image data</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>GIF 各头部/色表/扩展块（5520~5528）都过了，问题出在子图像的数据体本身（LZW 压缩流与其声明不符）。属 5520~5529 GIF 族末档。与 SUN 的 5535、XWD 侧的图像数据码语义平行但格式不同，定位时先认族。色表坏是 5523、LZW 码表出现环回是 5528，与本码层级不同。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 GIF 解码路径；多帧 GIF 常只解到某帧数据体坏即停。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>多为拷贝/传输中坏流，换无裁剪的原始 GIF 重取；反复命中说明该帧压缩流确已损坏。</para>
	/// </remarks>
	public const int Jl_ERR_GIF_BAD_IMAGE_DATA = 5529;

	/// <summary>并非 Sun Rasterfile（取值 5530），内嵌文本 <c>File is no Sun-Raster-File</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>已按 SUN 格式走读取路径，但文件头 magic 与 Sun Rasterfile 不符，即"自称是 SUN 却不是"。是 5530~5536 SUN 族的族首码：本码不过，后面 5531（头）、5532/5533（宽高）、5534（色表）、5535/5536（像素体）都无从校验。与 GIF 的 5520、PCX 的 5510 同型——都是"按该格式开读但头部认不出"。</para>
	///   <para><b>可达性</b>来自原生图像读写的 SUN Rasterfile 解码路径；此格式多见于 Sun/Solaris 时代旧档。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对文件确为 Sun Rasterfile 而非误标扩展名；若非该格式，改由能嗅探内容的通用读取路径处理。</para>
	/// </remarks>
	public const int Jl_ERR_SUN_RASTERFILE_TYPE = 5530;

	/// <summary>文件头错误（SUN Rasterfile，取值 5531），内嵌文本 <c>Wrong header</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>magic 已认出是 Sun Rasterfile（否则报 5530），但头内其余字段不合规范：版本/头长/type/depth 等取值非法或彼此矛盾。它是 SUN 族的第二档，头过了才轮到宽（5532）、高（5533）、色表（5534）、像素体（5535/5536）逐项校验。</para>
	///   <para><b>可达性</b>来自原生图像读写的 SUN Rasterfile 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>多为文件被文本方式转存、非大端字节序错读，或头部被截断；用原始二进制重取，或转存为常规格式。</para>
	/// </remarks>
	public const int Jl_ERR_SUN_RASTERFILE_HEADER = 5531;

	/// <summary>图像宽度非法（SUN Rasterfile，取值 5532），内嵌文本 <c>Wrong image width</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>头里 width（cols）字段越出本格式允许范围或与实际数据对不上。与 5533 <c>Jl_ERR_SUN_ROWS</c> 是同一校验的宽/高两档，都在 5531 头检之后、5534 色表之前。SUN 每行字节数按字（16 位）对齐，宽度取值不当还会连带让 5535 的数据体长度校验失准 [待实测：本解码器是否强制字对齐]。</para>
	///   <para><b>可达性</b>来自原生图像读写的 SUN Rasterfile 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对头里 width 是否为正、是否为格式允许的取值；换完整且未经字节序改动的档重取。</para>
	/// </remarks>
	public const int Jl_ERR_SUN_COLS = 5532;

	/// <summary>图像高度非法（SUN Rasterfile，取值 5533），内嵌文本 <c>Wrong image height</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>头里 height（rows）字段与格式允许范围或实际数据体对不上，属越界/被改坏。排在 5530（认文件）、5531（认头）之后、5534（色表）与 5535（像素体）之前：宽高两档（5532/5533）任一不过，像素长度校验就无从谈起。宽度对应码是 5532 <c>Jl_ERR_SUN_COLS</c>。</para>
	///   <para><b>可达性</b>来自原生图像读写的 SUN Rasterfile 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对头里 width/height 是否为正且不超过本格式允许的最大幅面；截断或字节序错配都会把高度读成离谱值，优先换完整档重取。</para>
	/// </remarks>
	public const int Jl_ERR_SUN_ROWS = 5533;

	/// <summary>色表错误（SUN Rasterfile，取值 5534），内嵌文本 <c>Wrong color map</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>Sun Rasterfile 头声明了 colormaptype/maplength，但色表数据非法或与头对不上（类型不认识、长度与实际不符一类）。属 5530~5536 SUN 族：5530 认文件、5531 认头、5532/5533 校宽高，色表这一档过了才轮到像素体（5535）。与 GIF 的 5523、PCX 的 5513 同为"色表坏"，但各按各格式定位。</para>
	///   <para><b>可达性</b>来自原生图像读写的 SUN Rasterfile 解码路径；仅带调色板的 8 位及以下档才有色表，真彩档不应触发本码 [待实测：colormaptype 合法取值集]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对 maptype/maplength 与实际调色板字节数是否一致；调色板档易在转存中坏流，换无压缩或真彩来源重取。</para>
	/// </remarks>
	public const int Jl_ERR_SUN_COLORMAP = 5534;

	/// <summary>Sun Rasterfile 图像数据错误（取值 5535），内嵌文本 <c>Wrong image data</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>SUN 族的头/宽/高/色表校验（5530~5534）都过了，像素数据体本身与头声明对不上（长度、RLE 流错乱一类）。属 5530~5536 SUN 族；与 GIF 的 5529 <c>Jl_ERR_GIF_BAD_IMAGE_DATA</c> 语义平行但格式不同，按族定位。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 SUN Rasterfile 解码路径；此格式多见于 Sun/Solaris 时代旧档。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对实际数据长度 = width×height×depth/8（或 RLE 声明长度）；压缩位图易在拷贝中坏流，换无压缩档重取。</para>
	/// </remarks>
	public const int Jl_ERR_SUN_RASTERFILE_IMAGE = 5535;

	/// <summary>像素类型不可能（SUN，取值 5536），内嵌文本 <c>Wrong type of pixel</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>Sun Rasterfile 头里 image_type 与 depth 的组合不存在（该格式的合法像素类型集合有限，非法组合即报此码）。常量名比文本更准：是"根本不可能"，不是"读歪了"。属 5530~5536 SUN 族末档；5540 是 XWD 侧的同文本码，别互串。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 SUN Rasterfile 解码路径；此格式多见于 Sun/Solaris 时代旧档。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认 type/depth 字段合法（如 RT_STANDARD 配 1/8/24/32）；旧档用转换工具改存常规格式。</para>
	/// </remarks>
	public const int Jl_ERR_SUN_IMPOSSIBLE_DATA = 5536;

	/// <summary>像素类型不可能（XWD，取值 5540），内嵌文本 <c>Wrong type of pixel</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>常量名 IMPOSSIBLE_DATA 比文本更狠：像素类型/深度的组合在该格式里根本不存在，不是"读歪了"而是"不可能有"。与 5536 <c>Jl_ERR_SUN_IMPOSSIBLE_DATA</c> 文本逐字相同、各归各格式（SUN 5536、XWD 5540），定位时先看族。属 5540~5548 XWD 族的族首码。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 XWD 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>像素格式字段与深度明显被改坏或版本错配；重新转储为 8/24 位常规像素格式。</para>
	/// </remarks>
	public const int Jl_ERR_XWD_IMPOSSIBLE_DATA = 5540;

	/// <summary>X visual class 错误（取值 5541），内嵌文本 <c>Wrong visual class</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>XWD 头里记录的 visual class（X 的显示类别，如 StaticGray/TrueColor/DirectColor 等枚举）不被支持或与数据不符。属 5540~5548 XWD 族：色表项数、像素解释都随 visual class 变，本码不对会连带让 5545/5546 的判断失准 [待实测：支持哪些 class]。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 XWD 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>转储时选常规 visual（TrueColor/灰度），避免多 visual 混合的窗口档。</para>
	/// </remarks>
	public const int Jl_ERR_XWD_VISUAL_CLASS = 5541;

	/// <summary>X10 文件头错误（取值 5542），内嵌文本 <c>Wrong X10 header</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>已按 X10 版 XWD 解析但头部字段不合规范。属 5540~5548 XWD 族的 X10 分支第一档，与 5543 是同一校验在两代头上的分身；头过了才轮到色表（5544）。X10 分支没有 pixmap 专属码，数据错会走通用档。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 XWD 解码路径；现实里基本只在翻读年代久远的转储档时遇到。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>同 5543：优先重新转储为 X11 版，而不是抢救 X10 头。</para>
	/// </remarks>
	public const int Jl_ERR_XWD_X10_HEADER = 5542;

	/// <summary>X11 文件头错误（取值 5543），内嵌文本 <c>Wrong X11 header</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>已判定为 X11 版 XWD，但头部字段不合规范（头长、字节序标记、字段范围一类）。属 5540~5548 XWD 族的 X11 分支第一档：头过了才轮到色表（5545）/pixmap（5546）；X10 的头错误是 5542；两代通用的版本字段不认识是 5547。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 XWD 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>常见于大端/小端互换过的拷贝，用 xwd 工具重转储；不要手工补齐头字段。</para>
	/// </remarks>
	public const int Jl_ERR_XWD_X11_HEADER = 5543;

	/// <summary>X10 颜色表错误（取值 5544），内嵌文本 <c>Wrong X10 colormap</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>X10 版 XWD 的色表不合法。属 5540~5548 XWD 族的 X10 分支；X11 同项错误是 5545，X10 头本身坏了是 5542。X10 是古董代际，实际触发多见于旧档翻读。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 XWD 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>老文件先在新 X 环境里重新转储成 X11/TrueColor 再读，别在原格式上抢救。</para>
	/// </remarks>
	public const int Jl_ERR_XWD_X10_COLORMAP = 5544;

	/// <summary>X11 颜色表错误（取值 5545），内嵌文本 <c>Wrong X11 colormap</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>X11 版 XWD 的色表（visual 挂的 colormap 项）不合法。属 5540~5548 XWD 族的 X11 分支；X10 同项错误是 5544，两码按头代际分家——收到本码即说明文件已按 X11 头解析。各格式色表另族分码：TIFF 5551、SUN 5534、BMP 5565。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 XWD 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>用 X 工具重新转储（换 visual 或直接转 TrueColor）；色表项数须与 visual class（5541）匹配。</para>
	/// </remarks>
	public const int Jl_ERR_XWD_X11_COLORMAP = 5545;

	/// <summary>X11 pixmap 数据错误（取值 5546），内嵌文本 <c>Wrong pixmap</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>X11 版 XWD 里 pixmap（像素位图数据）与头声明对不上：行长/位数/字节序合不成合法像素阵。属 5540~5548 XWD 族的 X11 分支（5543 头、5545 色表、本码数据）；X10 分支无 pixmap 专属码。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 XWD 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>多为跨字节序拷贝或被截断：核对 bytes_per_row 与文件实际长度，必要时同机重转储。</para>
	/// </remarks>
	public const int Jl_ERR_XWD_X11_PIXMAP = 5546;

	/// <summary>XWD 版本未知（取值 5547），内嵌文本 <c>Unknown version</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>XWD 文件头声明的协议版本不在已知集内。XWD 分 X10/X11 两代头，版本字段决定后续按 5542/5544（X10 路）还是 5543/5545/5546（X11 路）去校验；版本本身不认识时两路都进不去，先报本码。属 5540~5548 XWD 族。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 XWD 解码路径；多见于非 XWD 文件被强按 xwd 读。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认文件真是 xwd 转储（看文件头）、版本字段是否被改坏；换标准工具重新转储。</para>
	/// </remarks>
	public const int Jl_ERR_XWD_UNKNOWN_VERSION = 5547;

	/// <summary>读取 XWD 图像数据出错（取值 5548），内嵌文本 <c>Error while reading an image</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>XWD（X Window 图像转储）文件头已过、取像素数据时失败。属 5540~5548 XWD 族的最末档：头/版本/色表/pixmap 各类校验（5541~5547）都过了才轮到本码，语义是"读数据体这一动作本身出错"。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 XWD 解码路径（format 需指向 xwd；原生是否启用该解码器 [待实测]）。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先核对文件字节数是否够头声明的数据长度；用 X 工具重导一份再读。</para>
	/// </remarks>
	public const int Jl_ERR_XWD_READING_IMAGE = 5548;

	/// <summary>读取文件出错（TIFF，取值 5550），内嵌文本 <c>Error while reading a file</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>TIFF 解码过程中从输入取数据失败——IO 层或输入缓冲的问题，还没到语义校验。常量名 BAD_INPUTDATA 与泛化文本 "reading a file" 都指向读输入这一步；结构损坏另有 5558。属 5550~5559 TIFF 主族的入口档。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 TIFF 解码路径；文件中途被删/锁、网络盘断连是典型来源。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先确认文件在读期间可稳定访问（本地化副本最稳），再重试；与损坏码 5558 分开排查：本码查通路，那码查内容。</para>
	/// </remarks>
	public const int Jl_ERR_TIF_BAD_INPUTDATA = 5550;

	/// <summary>TIFF 颜色表错误（取值 5551），内嵌文本 <c>Wrong colormap</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>ColorMap tag 内容不合规范：表长不是 3×2^BitsPerSample、分量排布或取值域越界一类。属 5550~5559 TIFF 主族；色数太多装不下是 5552，本码专指色表数据坏了/写法不对。各格式色表分码：SUN 5534、XWD 5544/5545、BMP 5565。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 TIFF 编解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>用规范工具重建调色板后重存；写侧按 TIFF 规范给足表长与 16 位值域。</para>
	/// </remarks>
	public const int Jl_ERR_TIF_COLORMAP = 5551;

	/// <summary>颜色数过多（取值 5552），内嵌文本 <c>Too many colors</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>调色板色数超过按位深可编码的范围、或超过本解码器上限（上限具体值码中未标注 [待实测]）。属 5550~5559 TIFF 主族；与 5551 分工：5551 是色表本身坏，本码是色表没坏、只是颜色太多塞不进。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 TIFF 编解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>提高位深或降色数（量化/抖动）后重存；真彩图别硬走调色板路径，直接存 RGB 排布。</para>
	/// </remarks>
	public const int Jl_ERR_TIF_TOO_MANY_COLORS = 5552;

	/// <summary>光度解释错误（取值 5553），内嵌文本 <c>Wrong photometric interpretation</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>PhotometricInterpretation 取值超出支持集（黑=0/白=0、RGB、调色板、分离通道色一类里本解码器不认的组合）。属 5550~5559 TIFF 主族；与 5554 分工：本码是"解释值本身不行"，5554 是"解释值行、与位深搭配不行"。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 TIFF 编解码路径；工业相机存的非常规黑白极性 TIFF 易撞。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>转成 MinIsBlack/MinIsWhite 或 RGB 的标准组合再读；写侧显式给常规解释值。</para>
	/// </remarks>
	public const int Jl_ERR_TIF_BAD_PHOTOMETRIC = 5553;

	/// <summary>光度解释与位深不匹配（取值 5554），内嵌文本 <c>Wrong photometric depth</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>PhotometricInterpretation（颜色如何解释）与 BitsPerSample/SamplesPerPixel（位深、分量数）的组合不合规范——比如声明调色板却给了 16 位三通道。属 5550~5559 TIFF 主族；与 5553 分工：5553 是解释值本身不认识，本码是解释值认识、但和深度配不成合法组合。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 TIFF 编解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对这三项 tag 的合法搭配（TIFF 规范表）后重存；写侧不要手工拼 tag 组合。</para>
	/// </remarks>
	public const int Jl_ERR_TIF_PHOTOMETRIC_DEPTH = 5554;

	/// <summary>图像不是二进制文件（TIFF，取值 5555），内嵌文本 <c>Image is no binary file</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>从 TIFF 读回区域时，图不满足"二值图像"前提——区域以仅两值的位图承载，灰度/彩色 TIFF 无法还原。常量名 NO_REGION 说期望，文本说现状。与 5567（BMP）、5583（PNG）同族按格式分码；文本与 5583 逐字相同，别拿文本当格式判据。</para>
	///   <para><b>可达性</b>来自原生"从图像文件读回区域"的 TIFF 解码路径；该路径是否有托管入口未在本库证实 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先把图二值化（或用区域的导出选项重存）再读；拿不准就改用区域序列化通道。</para>
	/// </remarks>
	public const int Jl_ERR_TIF_NO_REGION = 5555;

	/// <summary>不支持的 TIFF 格式变体（取值 5556），内嵌文本 <c>Unsupported TIFF format</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>文件的组织方式落在本编解码器支持集之外（压缩方案、平面排布、位深/分量组合等，错误码不附带本库的支持矩阵 [待实测]）。属 5550~5559 TIFF 主族：文件坏是 5558，规格写错是 5557，本码专指"文件没坏、只是本库不认这种 TIFF"。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 TIFF 编解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>用其他工具把 TIFF 转成常规配置（无压缩或 LZW、8/16 位、按常见平面排布）再读；写侧避开冷门组合。</para>
	/// </remarks>
	public const int Jl_ERR_TIF_UNSUPPORTED_FORMAT = 5556;

	/// <summary>文件格式指定错误（TIFF，取值 5557），内嵌文本 <c>Wrong file format specification</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>文件格式的指定方式不合法——TIFF 侧的"规格写错了"。常量名限定 TIFF，文本是通用措辞；通用族另有 5508 <c>Jl_ERR_FILE_BAD_SPECIFICATION</c>，两码按层级分工 [待实测：5557 专指 TIFF 属性参数还是 format 串]。属 5550~5559 TIFF 主族。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 TIFF 编解码路径，多在带属性参数调用时触发。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>逐项核对 format 串与 TIFF 属性名/取值；先用零属性最简调用打通，再逐个加参定位是哪项规格写错。</para>
	/// </remarks>
	public const int Jl_ERR_TIF_BAD_SPECIFICATION = 5557;

	/// <summary>TIFF 文件损坏（取值 5558），内嵌文本 <c>TIFF file is corrupt</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>TIFF 结构损坏、按规范走不通（魔数/IFD 偏移/数据指针错乱一类的总称）。属 5550~5559 TIFF 主族；与 5590 <c>Jl_ERR_JP2_CORRUPT</c> 各管各的格式。缺必需 tag 报 5559、格式不支持报 5556，损坏专指"想按规范解析但解析不动"。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 TIFF 解码路径；截断、并发写、跨机拷贝损坏是常见来源。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>重新取得文件并与源核对字节数；多页文件中确认损坏在第几页，必要时逐页取出可读者。</para>
	/// </remarks>
	public const int Jl_ERR_TIF_FILE_CORRUPT = 5558;

	/// <summary>必需的 TIFF 标签缺失（取值 5559），内嵌文本 <c>Required TIFF tag is missing</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>解码 TIFF 时找不到必需的 tag（ImageWidth/Height/BitsPerSample 一类基础项缺失），文件结构本身未必坏，只是 IFD 里少了关键项。属 5550~5559 TIFF 主族；tag 存在但读写出错/类型非法/不支持分别是 5587/5588/5589。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 TIFF 解码路径；手写/被裁剪过的 TIFF 最易触发。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>用规范工具补齐必需 tag 后重存；写侧让输出走标准 TIFF 生成器，不手拼 IFD。</para>
	/// </remarks>
	public const int Jl_ERR_TIF_TAG_UNDEFINED = 5559;

	/// <summary>文件不是 BMP（取值 5560），内嵌文本 <c>File is no BMP-File</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>按 BMP 解码时前两个字节的 'BM' 签名校验失败——十有八九是扩展名与实际内容不符（实为 JPEG/PNG 等）或头被截。属 5560~5567 BMP 族的门牌码，与 5580（PNG）、5530（SUN）、5520（GIF）同类。头对但变体不认识是 5563，别混。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 BMP 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>看文件头实际字节判真格式，再改 format 参数重读；不要按文件名硬猜。</para>
	/// </remarks>
	public const int Jl_ERR_BMP_NO_BMP_PICTURE = 5560;

	/// <summary>BMP 提前到文件尾（取值 5561），内嵌文本 <c>Premature end of file</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>读 BMP 数据时文件比头声明的短，没读完就到 EOF。属 5560~5567 BMP 族；头还没读完就断是 5562，本码是头过了、像素/表数据读到一半数据不足。典型场景：网络流截断、写入被中途杀死。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 BMP 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对实际字节数与头声明大小；对截断文件不要指望部分恢复，重新取完整文件。</para>
	/// </remarks>
	public const int Jl_ERR_BMP_READ_ERROR_EOF = 5561;

	/// <summary>BMP 头不完整（取值 5562），内嵌文本 <c>Incomplete header</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>BMP 头没读满必需字段就断了/不够长，解析停在头部阶段。属 5560~5567 BMP 族，与 5561（头过了、数据段提前 EOF）分先后：本码卡在头部，5561 卡在数据体。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 BMP 解码路径；网络截断下载、拷贝未完成是典型来源。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>按文件头声明的总大小核对字节数，重新取得完整文件。</para>
	/// </remarks>
	public const int Jl_ERR_BMP_INCOMPLETE_HEADER = 5562;

	/// <summary>位图格式未知（取值 5563），内嵌文本 <c>Unknown bitmap format</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>BMP 头结构本身认不出来（头类型/头长度不属于解码器认识的变体）。属 5560~5567 BMP 族，与 5560（签名就不是 BM）分层：5560 是签名都不对，本码是签名过了但头变体不认识；压缩字段不认识则是 5564。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 BMP 解码路径；常见于把 ICO/BMP 变体硬当标准 BMP 读。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>用常规工具另存为标准 BMP（BITMAPINFOHEADER、无压缩）再读；写侧只输出标准头。</para>
	/// </remarks>
	public const int Jl_ERR_BMP_UNKNOWN_FORMAT = 5563;

	/// <summary>BMP 压缩格式未知（取值 5564），内嵌文本 <c>Unknown compression format</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>BMP 头里的压缩类型字段是本解码器不认识的取值（BMP 有 BI_RGB/BI_RLE8/BI_RLE4/BI_BITFIELDS 等多种，哪些受支持码里不给清单 [待实测]）。属 5560~5567 BMP 族；头本身认不出是 5563，本码认出头、只认不了压缩法。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 BMP 解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>外部工具把 BMP 重存为无压缩（BI_RGB）再读；写侧只输出未压缩 BMP。</para>
	/// </remarks>
	public const int Jl_ERR_BMP_UNKNOWN_COMPRESSION = 5564;

	/// <summary>BMP 颜色表错误（取值 5565），内嵌文本 <c>Wrong color table</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>BMP 调色板（color table）与头声明对不上：表长与位深不匹配、表偏移越界等。属 5560~5567 BMP 族；同族 5564/5563 管压缩与头格式，本码只管颜色表。与 5551（TIFF colormap）、5534（SUN colormap）、5544/5545（XWD colormap）各格式各码。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 BMP 编解码路径；读非标准工具产的低色深 BMP 时易触发。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>用常规工具重存为标准调色板 BMP；写侧让位深与表项数按 BMP 规范联动，不自造表长。</para>
	/// </remarks>
	public const int Jl_ERR_BMP_COLORMAP = 5565;

	/// <summary>BMP 写出错（取值 5566），内嵌文本 <c>Write error on output</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>写 BMP 时输出流写失败（文本只说"输出上写错"，未指明目标）：磁盘满、文件被占用、句柄中途关闭。属 5560~5567 BMP 族里唯一的写侧码，读侧问题走 5561/5562 等。</para>
	///   <para><b>可达性</b>来自原生 write_image 的 BMP 编码路径，<c>WriteImage</c> 以 bmp 输出时可报。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>查目标盘空间与文件占用；写完记得校验字节数，本码出现时目标文件多半已留下半截残缺档。</para>
	/// </remarks>
	public const int Jl_ERR_BMP_WRITE_ERROR = 5566;

	/// <summary>BMP 文件不含二值图像（取值 5567），内嵌文本 <c>File does not contain a binary image</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>从 BMP 文件读回区域时，文件里的图不是二值图——区域以仅两值的位图承载，灰度/彩色 BMP 无法还原。常量名 NO_REGION 说期望，文本说现状。与 5555（TIFF）、5583（PNG）同属"读区域要求二值图"一族，按格式各报各码。属 5560~5567 BMP 族。</para>
	///   <para><b>可达性</b>来自原生"从图像文件读回区域"的 BMP 解码路径；该路径是否有托管入口未在本库证实 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先把图二值化（或用区域的导出选项重存为 BMP）再读；拿不准就改用区域序列化通道。</para>
	/// </remarks>
	public const int Jl_ERR_BMP_NO_REGION = 5567;

	/// <summary>图像分量数错误（取值 5570），内嵌文本 <c>Wrong number of components in image</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>图像分量（通道）数与 JPEG 头声明的分量数对不上，或与 JPEG 允许的组合（1=灰度、3=YCbCr/RGB 一类）不符。属 5570~5576 的 JPEG 族：比 5576（笼统的输入图像错误）更具体，分量问题优先落本码。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 JPEG 编解码路径；4 通道图硬存 jpg 是最典型误用。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>写侧先把多余通道合成/丢弃到 1 或 3 通道；读侧报本码说明文件头与实际数据脱节，按损坏文件处理。</para>
	/// </remarks>
	public const int Jl_ERR_JPG_COMP_NUM = 5570;

	/// <summary>libjpeg 未知错误（取值 5571），内嵌文本 <c>Unknown error from libjpeg</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>libjpeg 报错但原因没能映射进 5572~5576 的任何一档，兜底落本码。属 5571~5576 libjpeg 族的"其他"档：文件 5573、临时文件 5574、内存 5575、输入图 5576、未实现 5572 都排除后才可用它。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 JPEG 编解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>没有细因就二分定位：同一张图换"只读不写/只写不读"两条路各试一次，再逐步换更常规的 JPEG 参数复现。</para>
	/// </remarks>
	public const int Jl_ERR_JPGLIB_UNKNOWN = 5571;

	/// <summary>libjpeg 未实现该特性（取值 5572），内嵌文本 <c>Not implemented feature in libjpeg</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>要求了 libjpeg 里存在但未实现（或被本构建裁掉）的功能组合——不是参数非法，是"这条代码路径是空的"。属 5571~5576 libjpeg 族；错误码不带出哪些特性属于空位的清单 [待实测]。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 JPEG 编解码路径；非常规 JPEG（算术编码、渐进式特殊配置等）更易撞上。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>退回最常规的基线 JPEG（顺序解码、标准霍夫曼）参数；需要该特性时换实现（如 libjpeg-turbo/其他编码器）产文件再读。</para>
	/// </remarks>
	public const int Jl_ERR_JPGLIB_NOTIMPL = 5572;

	/// <summary>libjpeg 文件访问错误（取值 5573），内嵌文本 <c>File access error in libjpeg</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>libjpeg 打开/读写目标 JPEG 文件失败：路径不存在、无权限、磁盘满。属 5571~5576 libjpeg 族。库内中转文件出错由 5574 承载，本码专指用户指定的那个文件。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 JPEG 编解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先脱离 libjpeg 直接验证文件可访问（存在、可写、目录存在、盘未满）；路径问题别赖编码器。</para>
	/// </remarks>
	public const int Jl_ERR_JPGLIB_FILE = 5573;

	/// <summary>libjpeg 临时文件访问错误（取值 5574），内嵌文本 <c>Tmp file access error in libjpeg</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>libjpeg 读写自己的临时文件失败。属 5571~5576 libjpeg 族；与 5573（目标文件本身）不同，本码打在库的中转文件上——libjpeg 在需要两遍统计（如优化霍夫曼表）时会用临时文件 [待实测：本构建启用哪些两遍路径]。</para>
	///   <para><b>可达性</b>来自原生 write_image 的 JPEG 编码路径；典型诱因是临时目录不可写、已满或被安全软件拦截。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>检查系统 TEMP/TMP 目录的权限与空间；容器/只读环境下显式指向可写目录后重试。</para>
	/// </remarks>
	public const int Jl_ERR_JPGLIB_TMPFILE = 5574;

	/// <summary>libjpeg 内存错误（取值 5575），内嵌文本 <c>Memory error in libjpeg</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>libjpeg 内部申请内存失败。属 5571~5576 libjpeg 族的资源层：与 5573（文件访问）、5574（临时文件）不同，本码是堆不够而非盘不通。直接编码大图时最易遇到。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 JPEG 编解码路径；系统内存耗尽或单次图像过大时可报。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>释放不再用的图像/区域对象、缩小单次处理量后重试；64 位进程仍复现则排查泄漏。</para>
	/// </remarks>
	public const int Jl_ERR_JPGLIB_MEMORY = 5575;

	/// <summary>libjpeg 报输入图像错误（取值 5576），内嵌文本 <c>Error in input image</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌 libjpeg 在编码时认为送来的输入图像不对（通道数/位深/尺寸与 JPEG 要求不符，或像素缓冲无效）。属 5571~5576 libjpeg 族，本码专指"源头图像"而非文件/内存/系统层。与 5570（分量数专项）近亲：分量数问题优先落 5570。</para>
	///   <para><b>可达性</b>来自原生 write_image 的 JPEG 编码路径；用非常规类型图像（如 16 位、4 通道）直接存 jpg 最易触发。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先把图像转成 8 位、1/3 通道的常规类型再存 JPEG；确认图像对象本身完好非半初始化。</para>
	/// </remarks>
	public const int Jl_ERR_JPGLIB_INFORMAT = 5576;

	/// <summary>文件不是 PNG（取值 5580），内嵌文本 <c>File is not a PNG file</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>按 PNG 解码时文件头签名校验失败——多半是扩展名骗人（实为 JPEG/BMP 等）或文件头被截。属 5580~5584 PNG 族的门牌码，与各格式同类的"验明正身"码平级：5560（BMP）、5530（SUN 类型）。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 PNG 解码路径；format 写 png 但内容非 PNG 时最先撞上本码。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>用文件真实内容判定格式（看头几个字节）再选 format；别按扩展名硬指定。</para>
	/// </remarks>
	public const int Jl_ERR_PNG_NO_PNG_FILE = 5580;

	/// <summary>PNG 交织类型未知（取值 5581），内嵌文本 <c>Unknown interlace type</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>文件头声明的交织（隔行）方式不在已知集合内——PNG 规范只有"非交织"与 Adam7 两种，出现第三种即报此码。属 5580~5584 PNG 族，与 5582（颜色类型）各管一头。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 PNG 解码路径；实际多见于被改坏的文件头而非工具的正常输出。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>重新导出为普通（非交织）PNG；写侧不要伪造交织字段。</para>
	/// </remarks>
	public const int Jl_ERR_PNG_UNKNOWN_INTERLACE_TYPE = 5581;

	/// <summary>不支持的 PNG 颜色类型（取值 5582），内嵌文本 <c>Unsupported color type</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>文件头声明的颜色类型（灰度/调色板/RGB/带 Alpha 等与位深的组合）不在本编解码支持集内。与 5581（交织方式）各管一头：本码只看"像素怎么摆颜色"。属 5580~5584 PNG 族。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 PNG 编解码路径；读其他工具生成的非常规 PNG（如 16 位带 Alpha）时最易触发 [待实测：支持矩阵]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>用外部工具把 PNG 转成常规类型（8 位灰度/RGB）再读；写侧让输出走本库支持的类型。</para>
	/// </remarks>
	public const int Jl_ERR_PNG_UNSUPPORTED_COLOR_TYPE = 5582;

	/// <summary>图像不是二进制文件（取值 5583），内嵌文本 <c>Image is no binary file</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>从 PNG 文件读入区域时，该图不满足"二值图像"前提——区域在图像文件里以仅两值的位图承载，灰度/彩色内容无法还原为区域。常量名 NO_REGION 说的是期望，文本说的是现状。与 5555（TIFF）、5567（BMP）同属"读区域要求二值图"一族，按格式各报各码。属 5580~5584 PNG 族。</para>
	///   <para><b>可达性</b>来自原生"从图像文件读回区域"的解码路径；该路径是否有托管入口未在本库证实 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先把图二值化（或按区域的导出选项重存）再读；拿不准就改用区域序列化通道而不是图像文件。</para>
	/// </remarks>
	public const int Jl_ERR_PNG_NO_REGION = 5583;

	/// <summary>PNG 图像尺寸过大（取值 5584），内嵌文本 <c>Image size too big</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>图像宽/高（或总像素量）超出 PNG 编解码允许的上限。与 5593 <c>Jl_ERR_JP2_SIZE_TOO_BIG</c> 文本相同但各归各家——先看实际 format 参数。属 5580~5584 PNG 族。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 PNG 编解码路径；超大幅面拼图为常见触发场景。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>缩放/分块处理，或改用 TIFF 一类对超宽尺寸更宽容的格式中转。</para>
	/// </remarks>
	public const int Jl_ERR_PNG_SIZE_TOO_BIG = 5584;

	/// <summary>访问 TIFF 标签出错（取值 5587），内嵌文本 <c>Error accessing TIFF tag</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>对某个 TIFF tag 的读写操作整体失败（IFD 结构走不到、偏移越界一类），是本子族里口径最宽的一档：tag 不支持→5589、值类型非法→5588、tag 缺失→5559，都对不上才落本码。属 5587~5589 TIFF tag 子族。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 TIFF 编解码路径。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先用 TIFF 工具检查文件 IFD 是否完整；多页/大文件时确认访问的是第几页的 tag。</para>
	/// </remarks>
	public const int Jl_ERR_TIF_TAG_ACCESS = 5587;

	/// <summary>TIFF 标签值的数据类型非法（取值 5588），内嵌文本 <c>Invalid TIFF tag value datatype</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>读写某个 TIFF tag 时，其值的声明数据类型（BYTE/ASCII/SHORT/LONG/RATIONAL 一类）与规范不符或不被支持。与 5589（tag 本身不支持）、5587（访问出错）分层：本码认得这个 tag，但它的值类型坏了。属 5587~5589 TIFF tag 子族。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 TIFF 编解码路径；读第三方生成的非常规 tag 值时易触发。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>用规范工具改写该 tag 的类型后再读；写侧确保按 tag 规范给出对应类型，而非靠隐式转换。</para>
	/// </remarks>
	public const int Jl_ERR_TIF_TAG_DATATYPE = 5588;

	/// <summary>请求了不支持的 TIFF 标签（取值 5589），内嵌文本 <c>Unsupported TIFF tag requested</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>读/写 TIFF 的 tag 时，该标签不在本库 tag 支持的白名单内。与 5587（tag 访问失败）、5588（tag 数据类型非法）、5559（必需 tag 缺失）构成 TIFF tag 四码：本码专指"这个 tag 不支持"。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 TIFF 编解码路径；托管侧以 TIFF 属性参数请求自定义 tag 时最可能触发（可用参数名 [待实测]）。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>只用文档列出的 TIFF tag 属性；确需私有 tag 时改用图像之外的元数据通道携带。</para>
	/// </remarks>
	public const int Jl_ERR_TIF_TAG_UNSUPPORTED = 5589;

	/// <summary>JPEG2000 文件损坏（取值 5590），内嵌文本 <c>File corrupt</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>JP2 文件结构破损/无法解析。内嵌文本只说"File corrupt"，不带格式名——靠常量名 JP2 才定位到 JPEG2000，别与 5558 <c>Jl_ERR_TIF_FILE_CORRUPT</c>（TIFF 的损坏码）混同。属 5590~5594 JP2 族。</para>
	///   <para><b>可达性</b>来自原生 read_image 的 JPEG2000 解码路径；传输/落盘截断是最常见成因。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>重新取得完整文件（核对字节数/校验和）；若文件确认完好仍报本码，则可能是本库不支持其编码参数，归入 5594 排查。</para>
	/// </remarks>
	public const int Jl_ERR_JP2_CORRUPT = 5590;

	/// <summary>图像精度（位深）过高（取值 5591），内嵌文本 <c>Image precision too high</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>每通道位深超过 JP2 编解码支持的范围——JP2 允许高精度，但本实现有上限，超过即拒（错误码本身不带出具体上限值 [待实测]）。属 5590~5594 JP2 族；尺寸超限是 5593，本码只管精度。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 JPEG2000 编解码路径；16 位及以上原图存取 jp2 时最易触发 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先降位深（如转 8 位）或换支持该位深的格式（TIFF 类）存取；确需 jp2 高精度则用其他工具验证是否本库限制。</para>
	/// </remarks>
	public const int Jl_ERR_JP2_PREC_TOO_HIGH = 5591;

	/// <summary>JPEG2000 编码过程出错（取值 5592），内嵌文本 <c>Error while encoding</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>把图像压成 JP2 的编码步骤失败——区别于 5590（文件已损坏，属解码侧）与 5591/5593（精度/尺寸超限，属输入不合法）：本码是编码器执行期自身出错。属 5590~5594 JP2 族。</para>
	///   <para><b>可达性</b>预期在 <c>WriteImage</c> 以 jp2 格式输出时触发；读路径一般报损坏类而非本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>检查输出磁盘空间/权限与目标文件是否被占用；降参数（位深、分量数）再试，仍失败即改存其他格式验证是否编码器问题。</para>
	/// </remarks>
	public const int Jl_ERR_JP2_ENCODING_ERROR = 5592;

	/// <summary>JPEG2000 图像尺寸过大（取值 5593），内嵌文本 <c>Image size too big</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>宽/高（或总像素量）超出 JP2 编解码允许的上限。与 5584 <c>Jl_ERR_PNG_SIZE_TOO_BIG</c> 文本完全相同但各归各家——先确认实际在读写的 format 是 jp2 还是 png，两码不互串。属 5590~5594 JP2 族。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 JPEG2000 编解码路径（原生是否启用该编解码器 [待实测]）。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>超限多为读入超大 jp2 所致：先缩放/裁切或改用无此限的格式（如 TIFF）中转，别硬存回 jp2。</para>
	/// </remarks>
	public const int Jl_ERR_JP2_SIZE_TOO_BIG = 5593;

	/// <summary>OpenJPEG 内部未知错误（取值 5594），内嵌文本 <c>Unknown internal error from OpenJPEG</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>JPEG2000 编解码库 OpenJPEG 抛出的兜底内部错误，原生侧没给出更细原因。5590~5594 是 JP2 族，本码是其中的"其他"档：损坏（5590）、精度（5591）、编码（5592）、尺寸（5593）都对不上时才落这里。</para>
	///   <para><b>可达性</b>来自原生 read_image/write_image 的 JPEG2000 编解码路径；托管端需以 jp2 格式读写图像才会遇到（原生是否启用该编解码器 [待实测]）。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先用其他工具验证该 jp2 文件本身可解；若可解仍报本码，怀疑文件带本库不支持的编码参数（小波/分块配置），改存常规参数再试。</para>
	/// </remarks>
	public const int Jl_ERR_JP2_INTERNAL_ERROR = 5594;

	/// <summary>文件并非只含图像（取值 5599），内嵌文本 <c>File does not contain only images</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>按"只装图像的对象文件"去读，文件里却混有别的对象类型。HOBJ 指本库序列化 iconic 对象所用的文件格式族；本码站在图像读入的期望上，文件本身没坏，是内容构成不符。与 5750~5764 序列化族（对象读写自身的错误）分层。</para>
	///   <para><b>可达性</b>预期在 <c>JlImage.ReadImage</c>（或 <c>JlOperatorSet.ReadImage</c>）读取含混合对象的 .hobj 类文件时触发 [待实测：托管路径能否直达本码]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>只用图像专用入口读只含图像的文件；文件是混合保存的就改用通用对象读回入口，别硬按图像取。</para>
	/// </remarks>
	public const int Jl_ERR_HOBJ_NOT_ONLY_IMAGES = 5599;

	/// <summary>套接字切阻塞失败（取值 5600），内嵌文本 <c>Socket can not be set to block</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>把 socket 设为阻塞模式（收发等满结果才返回）的选项设置失败。与 5601（设非阻塞失败）互为镜像。属 5600~5605 的 socket 模式/收取段，是 socket 族数值最小的一档。</para>
	///   <para><b>可达性</b>本运行时未导出 socket 阻塞模式设置算子（能力已删除），正常托管路径取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>模式没改成就不要按阻塞语义等待，否则会假死在 receive 上；先修选项设置或显式补超时。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_BLOCK = 5600;

	/// <summary>套接字切非阻塞失败（取值 5601），内嵌文本 <c>Socket can not be set to unblock</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>把 socket 设为非阻塞（调用不等待、立即返回）的模式设置失败。与 5600（设阻塞失败）互为镜像，两者都是套接字选项操作层面；选项写不进去也可能报 5627 而非本码，以本码专指"非阻塞切换"。</para>
	///   <para><b>可达性</b>本运行时未导出 socket 阻塞模式设置算子（能力已删除），正常托管路径取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认连接已建立再改模式；设置失败时连接仍按原模式工作，勿按非阻塞假设轮询。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_UNBLOCK = 5601;

	/// <summary>收到的数据不是元组（取值 5602），内嵌文本 <c>Received data is no tuple</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>按元组收取时，收到的数据连元组都不是。属 socket 族"收到的对象类型不符"小组（5602~5605），本码是最基础的一档。常量名 CPAR（疑为早期"经 socket 接收相机参数"通道的历史遗留，与本库无关）与文本对不上，一律以文本为准 [待实测]。</para>
	///   <para><b>可达性</b>托管层 <c>JlOperatorSet.ReceiveTuple</c>（id 328）不限类型装载，一般把什么类型都照收；本码更多预期来自原生按类型收取的通道 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>与 5618（是元组但类型不认识）区分排查：本码说明收发两端的封装约定根本不符，先对齐发送接口再收。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_NO_CPAR = 5602;

	/// <summary>收到的数据不是图像（取值 5603），内嵌文本 <c>Received data is no image</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>按图像对象去收取，收到的实际类型对不上。属 socket 族"收到的对象类型不符"小组（5602~5605）：5602 元组、本码图像、5604 区域、5605 XLD，各按期望类型分码，便于定位是哪条通道发错了货。</para>
	///   <para><b>可达性</b>本运行时未导出按图像收取的专用 socket 入口，通用收取走 <c>JlOperatorSet</c> 的 receive_tuple/receive_data；本码预期来自原生同类路径 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对发送端发出的对象确为图像；图像与区域/轮廓在收取接口上不可互换。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_NO_IMAGE = 5603;

	/// <summary>收到的数据不是区域（取值 5604），内嵌文本 <c>Received data is no region</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>按区域对象去收取，收到的实际类型对不上；RL 即 runlength，区域的行程编码内部表示。属 socket 族"收到的对象类型不符"小组（5602~5605）：5602 元组、5603 图像、本码区域、5605 XLD。区域字节数不符则报 5608 而非本码。</para>
	///   <para><b>可达性</b>本运行时未导出按区域收取的专用 socket 入口，通用收取走 <c>JlOperatorSet</c> 的 receive_tuple/receive_data；本码预期来自原生同类路径 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认发送端发的确实是区域而非图像/轮廓；区域跨进程传输两端须用同一区域编码。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_NO_RL = 5604;

	/// <summary>收到的数据不是 XLD 轮廓对象（取值 5605），内嵌文本 <c>Received data is no xld object</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>按 XLD 轮廓对象去收取，收到的实际类型对不上。属 socket 族"收到的对象类型不符"小组（5602~5605）：5602 元组、5603 图像、5604 区域、本码 XLD，按期望类型各报各码。与 5618（是元组但类型不认识）分层。</para>
	///   <para><b>可达性</b>本运行时未导出按对象类型收取的专用 socket 入口，通用收取走 <c>JlOperatorSet</c> 的 receive_tuple/receive_data；本码预期来自原生同类路径 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对发送端发出的对象类型与本端期望是否同一类型；类型混发时先约定帧头标明类型。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_NO_XLD = 5605;

	/// <summary>从套接字读数据失败（取值 5606），内嵌文本 <c>Error while reading from socket</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>recv 方向的通用读失败：链路中断、对端关闭或底层错误。与 5607（写方向失败）成对；"到点没等到"是 5619 而非本码。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>托管层 <c>JlOperatorSet.ReceiveTuple/ReceiveData</c>（id 328/330）在 socket 号取自原生侧已建立连接时可报本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>读失败后不要复用半包状态，重建同步点再收；对端正常关闭与链路故障在本码上不分家，需结合连接状态判断。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_READ_DATA_FAILED = 5606;

	/// <summary>向套接字写数据失败（取值 5607），内嵌文本 <c>Error while writing to socket</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>send 方向的通用写失败：对端已断开、连接被关闭或底层链路异常。与 5606（读方向失败）成对；不含"写不进缓冲区上限"这类，那走 5609/5624。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>托管层 <c>JlOperatorSet.SendTuple/SendData</c>（id 329/331）在 socket 号取自原生侧已建立连接时可报本码；发送失败的判定在 PostCall 处以异常形式抛出 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先判连接是否还活着再决定重发；注意写失败时对端可能只收到了半帧，重传须带帧边界。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_WRITE_DATA_FAILED = 5607;

	/// <summary>get_rl 收到的字节数非法（取值 5608），内嵌文本 <c>Illegal number of bytes with get_rl</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>用 get_rl 通道取区域行程（runlength，RL）数据时，实际收到的字节数与协议约定的不符，区域无法还原。是收发数据"尺寸不对"的一种，与 5609（缓冲区溢出）、5604（收到的不是区域）各自分层。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>本运行时未导出按区域收取的专用 socket 入口；通用收取走 <c>JlOperatorSet.ReceiveData/ReceiveTuple</c>，本码预期来自原生侧同类路径 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对发送端发出的字节数/格式串与接收端约定是否一致；区域经通道传输须两端同版本同格式编码。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_WRONG_BYTE_NUMBER = 5608;

	/// <summary>read_data 读取时缓冲区溢出（取值 5609），内嵌文本 <c>Buffer overflow in read_data</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>一次收取的数据量超过了 read_data 侧缓冲容量，收包被截断/放弃。与 5624（收到的数据类型/体量超限）侧重不同：本码是缓冲区层面的溢出，5624 是类型上限。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>托管层仅剩 <c>JlOperatorSet.ReceiveData/ReceiveTuple</c>（id 330/328）这类收发入口，socket 号须来自原生侧已建立的连接，有号时本码可达 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>发送端把单次报文拆小、分批传输；注意溢出后流位置已不可信，须整帧重新同步再接收。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_BUFFER_OVERFLOW = 5609;

	/// <summary>套接字创建失败（取值 5610），内嵌文本 <c>Socket can not be created</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>底层分配套接字句柄（文件描述符 fd）失败：进程/系统句柄资源耗尽，或请求的地址族/类型组合不被支持。属 5606~5633 socket 族的起点，与 5620（连接配额耗尽）同源但本码停在 create 这一步。常量名 ASSIGN_FD（分配描述符）与文本（创建 socket）说的是同一件事的两个层面，不冲突。</para>
	///   <para><b>可达性</b>create 类建连算子未导出（能力已删除），正常托管路径取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>释放不再使用的连接/句柄后重试；若与句柄数上限无关，则核对创建参数（地址族/流式或数据报）组合。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_CANT_ASSIGN_FD = 5610;

	/// <summary>套接字绑定地址失败（取值 5611），内嵌文本 <c>Bind on socket failed</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>bind 把本地地址/端口挂到套接字上失败：端口已被占用、低编号端口权限不足、地址不属于本机。是 5612/5613 的前置步骤，绑定不成后面端口也查不到、监听也进不去。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>bind 类建连算子未导出（能力已删除），正常托管路径取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>换端口或先释放占用者；需端口固定时排查残留监听进程；避免把地址写成远端 IP（bind 只能挂本机地址）。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_CANT_BIND = 5611;

	/// <summary>取不到套接字信息/端口号（取值 5612），内嵌文本 <c>Socket information is not available</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>回读套接字的端口等连接信息失败。常量名只提端口号，文本口径更宽（socket 信息整体不可用），以文本为准。典型成因：还没 bind 成功就来查端口，或句柄状态已丢。属 5606~5633 socket 族，与 5611（bind 失败本身）分层。</para>
	///   <para><b>可达性</b>查询连接信息的算子未导出（能力已删除），正常托管路径取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认调用序：create→bind→(listen) 成功后再取端口；以端口=0 请系统分配时，实际端口只能在 bind 后读回。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_CANT_GET_PORTNUMBER = 5612;

	/// <summary>套接字无法进入监听状态（取值 5613），内嵌文本 <c>Socket cannot listen for incoming connections</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>对套接字执行 listen 失败，进不了"等待来连"的服务端状态。前提是 socket 已创建（5610）且 bind 成功（5611），本码是这条准备链的最后一步。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>listen 类服务端算子未导出（能力已删除），正常托管路径取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对套接字类型是否支持被动监听（流式才可 listen）、backlog 等参数与调用次序（先 bind 后 listen）。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_CANT_LISTEN = 5613;

	/// <summary>连接未被接受（取值 5614），内嵌文本 <c>Connection could not be accepted</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>服务器侧 accept 取回挂起连接失败——监听队列满、句柄状态已失效或资源不足。发生在链路已建立之后，与 5615（主动连别人失败）方向相反。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>listen/accept 一类的服务端算子未导出（能力已删除），正常托管路径取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>服务器侧要及时取走挂起连接并控制并发量；accept 失败后监听套接字本身可能仍可用，先别整个重建。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_CANT_ACCEPT = 5614;

	/// <summary>连接请求失败（取值 5615），内嵌文本 <c>Connection request failed</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>地址已可用但发起的连接（connect）被拒或无响应：对端端口没在监听、被防火墙拒绝、路由不可达。与 5616（主机名都解析不出）互补。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>建连算子未导出（能力已删除），正常托管路径取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先确认对端确已在目标端口 listen、端口号与协议（流/数据报）一致，再查防火墙；连不上与解析失败分码排查，别混为一谈。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_CANT_CONNECT = 5615;

	/// <summary>主机名解析失败（取值 5616），内嵌文本 <c>Hostname could not be resolved</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>按 gethostbyname 一类接口解析主机名失败，常见成因是主机名拼写错、DNS 不可达或无网络。与 5615（名字已解析出、连接请求却失败）分层：本码还没走到发起连接那一步。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>名字解析发生在 connect/open_socket 建连阶段，本运行时未提供这些包装（能力已删除），正常托管路径取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先改填 IP 直连以区分"解析问题还是链路问题"，再排查 DNS 配置/hosts 表。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_GETHOSTBYNAME = 5616;

	/// <summary>socket 上收到的元组类型未知（取值 5618），内嵌文本 <c>Unknown tuple type on socket</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>收到的元组带有的类型标记本运行时不认识——通常是收发两端的库版本或协议不同步。与 5602（收到的内容根本不是元组）不同：本码认定是元组但解析不了它的类型。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>经 <c>JlOperatorSet.ReceiveTuple</c>（id 328）收取时可达；socket 号只能取自原生侧已建立的连接 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>收发两端统一库版本与所用数据类型；报本码后字节流位置已不可信，须重新对齐协议再继续收。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_ILLEGAL_TUPLE_TYPE = 5618;

	/// <summary>套接字操作超时（取值 5619），内嵌文本 <c>Timeout occurred on socket</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>在设定超时内未等到收发结果被中止。与 5606/5607（收发本身出错）不同：本码只说明"到点"，链路可能仍完好，只是对端迟迟没有交付数据。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>超时阈值的设置算子未导出；持原生侧已建好的连接号经 <c>JlOperatorSet</c> 的 receive_tuple/receive_data（id 328/330）收取时，若原生启用超时可报本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>超时后不要假设数据"完全没发/没收"——字节流位置不确定，重传须以整帧重新同步；同时核对对端是否存活。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_TIMEOUT = 5619;

	/// <summary>套接字资源耗尽，无法再开新连接（取值 5620），内嵌文本 <c>No more sockets available</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>可同时打开的 socket 数量达到运行时/系统上限，新建连接拿不到资源。常量名 NA 是 not available（配额耗尽）的缩写，不是"该 socket 不可用"：与 5621（句柄未初始化）、5622（句柄非法）不同，本码是压根没拿到新句柄。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>本运行时未提供 open_socket 等 socket/通信算子包装（能力已删除），正常托管路径取不到本码；仅存的收发静态算子不创建连接，不会触发本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先关闭不再使用的连接释放配额再重开；若反复出现，按"开而不关"的连接泄漏排查。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_NA = 5620;

	/// <summary>套接字句柄尚未初始化（取值 5621），内嵌文本 <c>Socket is not initialized</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>收发动作引用了一个还没走完初始化（open/init 未执行或未完成）的 socket 句柄；常量名 NI = not initialized。与同族分工：5620 <c>Jl_ERR_SOCKET_NA</c> 是"资源耗尽、压根没拿到新句柄"，5622 <c>Jl_ERR_SOCKET_OOR</c> 是"句柄存在但已失效"，5623 <c>Jl_ERR_SOCKET_IS</c> 是"句柄为 NULL"，本码则是"句柄还没进入可用状态"。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>建连发生在 socket/通信算子，本运行时未提供其包装（能力已删除），正常托管路径取不到本码；若传给仅存的收发静态算子的连接号并非原生侧已初始化好的连接，理论上可报本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>；文本可由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>处置</b>先成功打开/初始化连接、拿到可用句柄再收发；报本码说明问题在初始化流程而非链路对端。与 5620 不同，关旧连接腾配额对它无效，别混着排查。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_NI = 5621;

	/// <summary>无效的套接字（取值 5622），内嵌文本 <c>Invalid socket</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>socket 句柄存在但已不合法（如已关闭/已失效仍被引用）。常量名 OOR 与文本"Invalid socket"以文本为准：与 5623（句柄为 NULL）不同，本码是"句柄非法但非空"。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>本运行时未提供 socket/通信算子包装（能力已删除），正常托管路径一般取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>不要在关闭或已失效的句柄上继续收发；先重开连接拿新句柄。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_OOR = 5622;

	/// <summary>套接字为空（NULL，取值 5623），内嵌文本 <c>Socket is NULL</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>操作引用的 socket 句柄为 NULL。常量名 IS 含义不清，以文本为准：这是"句柄为空"，与 5622 <c>Jl_ERR_SOCKET_OOR</c>（句柄非空但无效）程度不同。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>本运行时未提供 socket/通信算子包装（能力已删除），正常托管路径一般取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先成功打开连接拿到有效句柄再操作；句柄从未赋值即被使用是最常见成因。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_IS = 5623;

	/// <summary>收到的数据（类型/规模）过大（取值 5624），内嵌文本 <c>Received data type is too large</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>收到的数据超出可接收的规模/类型上限。常量名 DATA_TOO_LARGE 与文本"data type is too large"以文本为更准口径：偏"类型/体量超限"。属 5606~5633 socket 族，与 5609（read_data 缓冲区溢出）相关但侧重不同。</para>
	///   <para><b>可达性</b>本运行时未提供 socket/通信算子包装（能力已删除），正常托管路径一般取不到本码，来自原生通信栈 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>分批/缩小单次传输的数据体量，或改传输更合适的类型；与缓冲区溢出码一同排查传输上限。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_DATA_TOO_LARGE = 5624;

	/// <summary>套接字类型不对（取值 5625），内嵌文本 <c>Wrong socket type.</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>对该 socket 执行了与其类型不相容的操作（如在流式 TCP 套接字上做只适用于其他类型/通道的调用）。属 5606~5633 socket 族，与 5618（元组类型未知）、5622（无效 socket）语义不同。</para>
	///   <para><b>可达性</b>本运行时未提供 socket/通信算子包装（能力已删除），正常托管路径一般取不到本码，来自原生通信栈 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对打开连接时选择的 socket 类型是否与被调用的操作匹配（服务器/客户端、通道类型）。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_WRONG_TYPE = 5625;

	/// <summary>收到的数据未被打包（取值 5626），内嵌文本 <c>Received data is not packed.</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>期望收到"打包"格式的数据（send_data 一类的封包协议），实际收到的却不符打包约定。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>本运行时未提供 socket/通信算子包装（能力已删除），正常托管路径一般取不到本码，来自原生通信栈 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>收发两端须用同一打包/解包约定：发送方用打包接口、接收方用对应解包接口；混用裸发送与打包接收会导致本码。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_NO_PACKED_DATA = 5626;

	/// <summary>套接字选项参数操作失败（取值 5627），内嵌文本 <c>Socket parameter operation failed.</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>读/写套接字选项（如 getsockopt/setsockopt 一类的参数操作）失败。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>本运行时未提供 socket/通信算子包装（能力已删除），正常托管路径一般取不到本码，来自原生通信栈 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>若经桥接通信遇到：核对所设选项在该平台/该 socket 状态是否可用，而非算法参数。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_PARAM_FAILED = 5627;

	/// <summary>收到的数据与格式规范不匹配（取值 5628），内嵌文本 <c>The data does not match the format specification.</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>格式串合法、也按它去解析，但实际收到的数据不符合该格式（长度/字段/类型对不上）。属 5606~5633 socket 族，与 5629（格式串自身无效）不同。</para>
	///   <para><b>可达性</b>本运行时未提供 socket/通信算子包装（能力已删除），正常托管路径一般取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对发送方实际布局与接收方声明格式是否一致（对齐、字节序、字段数）；协议两侧需同步修改。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_FORMAT_MISMATCH = 5628;

	/// <summary>格式规范（format 字符串）本身非法（取值 5629），内嵌文本 <c>Invalid format specification.</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>用于收发/解析数据的格式说明串写错了、不被解析。属 5606~5633 socket 族，与 5628（数据与合法格式不匹配）不同：本码是"格式串自身无效"。</para>
	///   <para><b>可达性</b>本运行时未提供 socket/通信算子包装（能力已删除），正常托管路径一般取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对格式说明串的语法（字段/分隔/类型标记），修好格式串后再谈数据是否匹配。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_INVALID_FORMAT = 5629;

	/// <summary>收到的数据不是一个 serialized item（取值 5630），内嵌文本 <c>Received data is no serialized item</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>通过 socket 收数据并按"序列化对象"来解，但收到的字节流并非合法的 <c>serialized_item</c>（协议不匹配、发送方发了普通文本/裸数据）。属 5606~5633 socket 族。</para>
	///   <para><b>可达性</b>本运行时未提供 socket/通信算子包装（能力已删除），正常托管路径一般取不到本码；序列化对象本身的读回错误另有 5750~5764 序列化族 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认收发两端约定：发送方须用 write 序列化对象、接收方按 serialized item 解读；协议不一致时先对齐格式。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_NO_SERIALIZED_ITEM = 5630;

	/// <summary>无法创建 SSL/TLS 上下文（取值 5631），内嵌文本 <c>Unable to create SSL context</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>加密连接建立的第一步——初始化 TLS 上下文就失败（库缺失、协议版本/加密套件不可用等）。属 5606~5633 socket/TLS 族，早于 5632 的凭据装载。</para>
	///   <para><b>可达性</b>本运行时未提供 socket/通信/TLS 包装（能力已删除），正常托管路径一般取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>查加密库/运行环境与协议配置；上下文没建起来时后续证书/握手码（5632/5633）无从谈起，应先解决本码。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_TLS_CONTEXT = 5631;

	/// <summary>TLS 证书或私钥非法（取值 5632），内嵌文本 <c>Invalid TLS certificate or private key</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>装载 TLS 凭据时证书或私钥不被接受（格式错、过期、加载失败或两者不匹配）。属 5606~5633 socket/TLS 族，与 5633（文本专指私钥）相邻。</para>
	///   <para><b>可达性</b>本运行时未提供 socket/通信/TLS 包装（能力已删除），正常托管路径一般取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对证书/私钥文件路径、格式与配对关系，检查有效期；这是凭据装载阶段失败，与算法参数无关。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_TLS_CERT_KEY = 5632;

	/// <summary>TLS 相关失败，内嵌原生文本实为 <c>Invalid TLS private key</c>（私钥非法，取值 5633）。</summary>
	/// <remarks>
	///   <para><b>含义</b>常量名写的是 HANDSHAKE（握手），但内嵌原生文本是"TLS 私钥无效"——两处口径不一致，以文本为准更可靠：本码专指私钥校验失败，而非广义握手阶段失败。属 5606~5633 socket/TLS 族。</para>
	///   <para><b>可达性</b>本运行时未提供 socket/通信/TLS 相关算子与包装（该能力已删除），正常托管路径一般取不到本码，来自原生通信栈 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>；文本由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>处置</b>若经原生/桥接通信遇此码：核对私钥文件与证书是否配对、格式（如 PEM）与权限是否正确，而非调算法参数。</para>
	/// </remarks>
	public const int Jl_ERR_SOCKET_TLS_HANDSHAKE = 5633;

	/// <summary>要写入某文件格式的轮廓/多边形条数超过该格式上限（取值 5700）。</summary>
	/// <remarks>
	///   <para><b>含义</b>常量名 ARCINFO 指向把 XLD 轮廓/多边形以 ARC 之类文本格式持久化；内嵌文本 <c>Too many contours/polygons for this file format</c> 说明该文件格式对可容纳的轮廓/多边形条数设有上限，超了即报本码。属 5700~5764 序列化/文件族。</para>
	///   <para><b>可达性</b><c>JlXLD</c>/<c>JlXLDCont</c> 在本运行时保留，故向受限的文本轮廓格式批量导出时可能真实触发；上限具体数值依该格式而定 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>减少单次导出的轮廓条数（分批写），或改用容量更大的存储格式；这是格式容量限制，不是参数写错。</para>
	/// </remarks>
	public const int Jl_ERR_ARCINFO_TOO_MANY_XLDS = 5700;

	/// <summary>四元数的序列化版本不受支持（取值 5750）。</summary>
	/// <remarks>
	///   <para><b>含义</b>读入的四元数 serialized item 版本号本运行时不认（新版写、旧版读）。属 5700~5764 序列化族，与 5751（内容非合法对象）区分。</para>
	///   <para><b>可达性</b>本运行时已删除四元数类型（<c>JlQuaternion</c> 移除），正常托管路径不会触发本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>版本门禁不可绕过：让写入方与读取方版本一致，或重存为当前版本格式。</para>
	/// </remarks>
	public const int Jl_ERR_QUAT_WRONG_VERSION = 5750;

	/// <summary>serialized item 里不含合法的四元数（取值 5751），内嵌文本 <c>Serialized item does not contain a valid quaternion</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>把一个 <c>serialized_item</c> 按四元数反序列化，内容解析不出来（类型不符或损坏）。属 5700~5764 序列化族，与 5750（版本不支持）区分。</para>
	///   <para><b>可达性</b>本运行时已删除四元数类型（<c>JlQuaternion</c> 移除），正常托管路径不会触发本码 [待实测]；经原生/桥接读取时才可能遇到。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认字节流确由四元数写出且未损坏；别用四元数读取路径解对偶四元数（5763/5764）或别的对象。</para>
	/// </remarks>
	public const int Jl_ERR_QUAT_NOSITEM = 5751;

	/// <summary>二维齐次矩阵的序列化版本不受支持（取值 5752）。</summary>
	/// <remarks>
	///   <para><b>含义</b>读入的二维齐次矩阵 serialized item 版本号本运行时不认（新版写、旧版读）。属 5700~5764 序列化族，与 5753（内容非合法对象）区分。</para>
	///   <para><b>可达性</b><c>JlHomMat2D</c> 在本运行时保留，二维齐次矩阵序列化是活路径，跨版本读取时可真实触发本码。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>版本门禁不可绕过：让写入方与读取方版本一致，或重存为当前版本格式；勿靠重试解决。</para>
	/// </remarks>
	public const int Jl_ERR_HOM_MAT2D_WRONG_VERSION = 5752;

	/// <summary>serialized item 里不含合法的二维齐次矩阵（取值 5753），内嵌文本 <c>Serialized item does not contain a valid homogeneous matrix</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>把一个 <c>serialized_item</c> 按二维齐次矩阵（hom_mat2d）反序列化，内容解析不出来（类型不符或损坏）。属 5700~5764 序列化族，与 5752（版本不支持）区分。</para>
	///   <para><b>可达性</b><c>JlHomMat2D</c> 在本运行时仍保留，故二维齐次矩阵的序列化写出/读回是活路径，本码可真实触发。注意 <c>JlHomMat2D</c> 派生自 <c>JlData</c>、不实现 <c>IDisposable</c>，无需（也不能）对它 <c>Dispose</c>。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认字节流确由二维齐次矩阵写出且完整；别用二维的读取路径去解三维矩阵（5754/5755）或别的对象。</para>
	/// </remarks>
	public const int Jl_ERR_HOM_MAT2D_NOSITEM = 5753;

	/// <summary>三维齐次矩阵的序列化版本不受支持（取值 5754）。</summary>
	/// <remarks>
	///   <para><b>含义</b>读入的三维齐次矩阵 serialized item 版本号本运行时不认（新版写、旧版读）。属 5700~5764 序列化族，与 5755（内容非合法对象）区分。</para>
	///   <para><b>可达性</b>本运行时已删除三维齐次矩阵类型（<c>JlHomMat3D</c> 移除，二维 <c>JlHomMat2D</c> 保留），正常托管路径不会触发本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>版本门禁不可绕过：写入方与读取方版本需一致，或重存为当前版本格式。</para>
	/// </remarks>
	public const int Jl_ERR_HOM_MAT3D_WRONG_VERSION = 5754;

	/// <summary>serialized item 里不含合法的三维齐次矩阵（取值 5755），内嵌文本 <c>Serialized item does not contain a valid homogeneous 3D matrix</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>把一个 <c>serialized_item</c> 按三维齐次矩阵（hom_mat3d）反序列化，内容解析不出来。属 5700~5764 序列化族，与 5754（版本不支持）区分。</para>
	///   <para><b>可达性</b>本运行时已删除三维齐次矩阵类型（<c>JlHomMat3D</c> 已移除；二维的 <c>JlHomMat2D</c> 仍在），正常托管路径不会触发本码 [待实测]。若经原生/桥接路径读取，仍会遇到。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认字节流确由三维齐次矩阵写出、且未损坏；注意别与二维齐次矩阵（5752/5753）混淆——两者是不同的序列化对象。</para>
	/// </remarks>
	public const int Jl_ERR_HOM_MAT3D_NOSITEM = 5755;

	/// <summary>元组（tuple）的序列化版本不受支持（取值 5756），内嵌文本 <c>The version of the tuple is not supported</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>读入的元组 serialized item 版本号本运行时不认，典型是新版写、旧版读。属 5700~5764 序列化族，与 5757（内容非合法元组）区分。</para>
	///   <para><b>可达性</b><c>JlTuple</c> 在本运行时保留，元组序列化是活路径，跨版本读取时可真实触发本码。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>版本门禁不可绕过：让写入方与读取方版本一致，或重存为当前版本格式；勿靠重试。</para>
	/// </remarks>
	public const int Jl_ERR_TUPLE_WRONG_VERSION = 5756;

	/// <summary>serialized item 里不含合法的元组（tuple，取值 5757），内嵌文本 <c>Serialized item does not contain a valid tuple</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>把一个 <c>serialized_item</c> 按元组反序列化，内容解析不出合法元组（类型不符或数据损坏）。属 5700~5764 序列化族，与 5756（版本不支持）区分。</para>
	///   <para><b>可达性</b><c>JlTuple</c> 在本运行时保留（且实现了 <c>IDisposable</c>），元组的序列化写出/读回是活路径，本码可真实触发。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认字节流确由元组序列化写出且完整（无截断/篡改），别用元组读取路径解别的对象。</para>
	/// </remarks>
	public const int Jl_ERR_TUPLE_NOSITEM = 5757;

	/// <summary>字符串转数值时数值过大、发生溢出（取值 5758），内嵌文本 <c>Number too big for a string to number conversion (overflow)</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>把一个数字字符串转成数值时，其大小超出目标数值类型可表示的范围而溢出。常量名 DTLFTHV 晦涩，一切以文本为准。虽排在 5700~5764 序列化族内，但它不是"版本/类型"错，而是元组数值转换的溢出。</para>
	///   <para><b>可达性</b><c>JlTuple</c> 在本运行时保留，字符串↔数值转换相关算子仍可用，故本码可能真实触发；整数与浮点的可表示范围不同，结果随目标类型而异 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先确认字符串确实是合法数值、再选够宽的数值类型（或用浮点承接大数量级）；对来源不可控的文本应先校验范围再转换。</para>
	/// </remarks>
	public const int Jl_ERR_TUPLE_DTLFTHV = 5758;

	/// <summary>位姿/相机参数（pose）的序列化版本不受支持（取值 5759）。</summary>
	/// <remarks>
	///   <para><b>含义</b>读入的 pose serialized item 版本号本运行时不认，典型是新版写、旧版读。原生文本把 pose 称作"camera parameters (pose)"。属 5700~5764 序列化族，与 5760（内容非合法对象）区分。</para>
	///   <para><b>可达性</b><c>JlPose</c> 在本运行时仍保留，对 pose 做序列化读回时可能触发本码（跨版本读取旧/新格式）。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>版本门禁不可绕过：在写入方与读取方版本一致的环境间迁移 pose，勿靠重试解决；必要时重存为当前版本格式。</para>
	/// </remarks>
	public const int Jl_ERR_POSE_WRONG_VERSION = 5759;

	/// <summary>serialized item 里不含合法的位姿/相机参数（pose，取值 5760）。</summary>
	/// <remarks>
	///   <para><b>含义</b>把一个 <c>serialized_item</c> 按 pose 反序列化，内容却解析不出合法对象（类型不符或数据损坏）。原生文本把 pose 称作"camera parameters (pose)"。属 5700~5764 序列化族，与 5759（版本不支持）区分。</para>
	///   <para><b>可达性</b>与已删除的相机参数类型不同，<c>JlPose</c> 在本运行时仍保留，因此对 pose 做序列化写出/读回时确实可能触发本码：读到的字节流若不是 pose、或被截断/篡改，就会命中。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经 <see cref="M:JLVisionLib.JlOperatorException.throwOperator(System.Int32,System.String)"/> 一类统一检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认读回的 serialized item 确由 pose 写出、传输/落盘完整；别用 pose 的读取路径去解别的对象类型。</para>
	/// </remarks>
	public const int Jl_ERR_POSE_NOSITEM = 5760;

	/// <summary>内部相机参数的序列化版本不受支持（取值 5761），内嵌文本 <c>The version of the internal camera parameters is not supported</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>读入的 cam_par serialized item 版本号本运行时不认，典型是新版写、旧版读。属 5700~5764 序列化族，与 5762（内容非合法对象）区分。</para>
	///   <para><b>可达性</b>本运行时已删除内部相机参数类型及包装，正常托管路径不会触发本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>版本门禁不可绕过：用匹配版本重存或读取，勿靠重试解决。</para>
	/// </remarks>
	public const int Jl_ERR_CAM_PAR_WRONG_VERSION = 5761;

	/// <summary>serialized item 里不含合法的内部相机参数（取值 5762），内嵌文本 <c>Serialized item does not contain valid internal camera parameters</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>把一个 <c>serialized_item</c> 当作内部相机参数（cam_par）反序列化，但内容解析不出来（类型不符或已损坏）。属 5700~5764 序列化族，与 5761（版本不支持）区分。</para>
	///   <para><b>可达性</b>本运行时已删除内部相机参数类型及包装（见能力裁剪），正常托管路径不会触发本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对 serialized item 的写出类型，别用相机参数去解别种对象；数据可能损坏。</para>
	/// </remarks>
	public const int Jl_ERR_CAM_PAR_NOSITEM = 5762;

	/// <summary>对偶四元数的序列化为版本不受支持（取值 5763），内嵌文本 <c>The version of the dual quaternion is not supported</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>读入的对偶四元数 serialized item 带有本运行时不认的格式版本号——常见于用更新版本写出的数据在旧运行时反序列化。属 5700~5764 序列化族，与 5764（内容不是合法对象）不同：本码专指"版本对不上"。</para>
	///   <para><b>可达性</b>本运行时已删除对偶四元数类型及包装，正常托管路径不会触发本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>版本不匹配不能靠改参数绕过：须用支持该序列化版本的原运行时重存，或在匹配版本上读取。</para>
	/// </remarks>
	public const int Jl_ERR_DUAL_QUAT_WRONG_VERSION = 5763;

	/// <summary>被读取的 serialized item 里不含合法的对偶四元数（取值 5764）。</summary>
	/// <remarks>
	///   <para><b>含义</b>反序列化一个 <c>serialized_item</c> 时，其中的内容解析不成对偶四元数（类型不符或数据损坏）。与 5763（版本号不支持）分别指"类型/内容不对"与"版本不对"。属 5700~5764 序列化族。</para>
	///   <para><b>可达性</b>本运行时已删除对偶四元数类型及其包装（见能力裁剪），正常托管路径无法读写该类型的 serialized item，因此一般取不到本码 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>若经原生/桥接路径确遇此码：核对读到的 serialized item 当初是用哪个类型写出的，别用对偶四元数去解别种对象；数据可能已损坏。</para>
	/// </remarks>
	public const int Jl_ERR_DUAL_QUAT_NOSITEM = 5764;

	/// <summary>图像源操作失败，原因未归类（取值 5800），内嵌文本 <c>Image source operation failed - unknown reason</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>图像源/采集族（5800~5820）的兜底码：调用确实失败但归不进该族其它更具体的码。属 5800~5820 图像源族。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>码本身不携带成因：先看更细的 5801~5820 是否更贴切，再取 <c>JlNativeApi.GetErrorMessage(err)</c> 文本定位；不要仅凭本码就断定为参数问题。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_FAIL = 5800;

	/// <summary>图像源操作失败——内部假设不成立（取值 5801），内嵌文本 <c>Image source operation failed - wrong internal assumptions</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>采集实现遇到与其内部前提相矛盾的情形（本该成立的不变量没成立）。属 5800~5820 图像源族，与 5800（未知原因）相比本码更偏向"逻辑/前提被违背"。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>近似"内部一致性问题"，重试未必可恢复：记录触发操作与 <c>GetErrorMessage</c> 文本，重开设备/实例后观察；反复出现按缺陷上报。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_LOGIC = 5801;

	/// <summary>所请求的图像源功能未实现（取值 5802），内嵌文本 <c>Image source functionality is not implemented</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>调用了采集/图像源里一个当前实现尚未提供的功能（占位、该设备型号或该插件版本不支持）。属 5800~5820 图像源族。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>这不是用法错误而是"能力缺失"：换支持该功能的设备/插件版本，或改走已实现的等价路径；重试同一调用无意义。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_NOT_IMPLEMENTED = 5802;

	/// <summary>图像源插件版本不兼容（取值 5803），内嵌文本 <c>Image source plugin version incompatible</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>加载的采集插件（图像源实现）与本运行时期望的接口版本不匹配，拒绝使用。属 5800~5820 图像源族。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>换用与运行时版本配套的采集插件（同批发布），不要指望参数调整绕过版本门禁。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_INCOMPATIBLE_VERSION = 5803;

	/// <summary>某个 GenTL producer 抛出未被处理的异常（取值 5804），内嵌文本 <c>Unhandled exception was triggered by a GenTL producer</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>GenTL 传输层生产者（厂商提供的采集驱动组件）抛异常未被消化，转成本码。属 5800~5820 图像源族。与 5805（GenICam GenAPI 参数层抛错）分层不同：本码在传输/驱动层。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>方向是驱动/传输栈：确认厂商 GenTL producer 与运行时版本匹配、设备连接与电源正常；本码不携带细节，须结合 <c>GetErrorMessage</c> 文本与现场操作定位。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_GENTL_ERROR = 5804;

	/// <summary>GenICam GenAPI 抛出未被处理的异常（取值 5805），内嵌文本 <c>Unhandled exception was triggered by the GenICam GenAPI</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>底层 GenICam GenAPI（访问设备参数/节点的通用 API）抛出异常且未被采集层消化，被转成本码。属 5800~5820 图像源族。与 5804（GenTL producer 抛错）分别对应 GenICam 参数层与 GenTL 传输层。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>这是"底层透传上来的未分类异常"，本码不含具体成因：结合 <c>JlNativeApi.GetErrorMessage(err)</c> 文本与现场操作（哪个节点、哪条命令）定位，通常仍是参数名/类型/状态不符。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_GENAPI_ERROR = 5805;

	/// <summary>图像源资源初始化失败（取值 5806），内嵌文本 <c>Image source resource could not be initialized</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>尝试初始化采集资源时失败（申请缓冲、建立通道等未成功）。与 5807（尚未初始化就使用）不同：本码是"试过初始化但没成"。属 5800~5820 图像源族"模块/资源"子群。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>查资源可用性与设备连接（内存/带宽/被占用），必要时释放并重开；若与 6001 <c>Jl_ERR_MEM</c> 同时出现，优先解决内存不足。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_RES_INIT_FAIL = 5806;

	/// <summary>图像源资源尚未初始化就被使用（取值 5807），内嵌文本 <c>Image source resource not initialized</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>在 open/init 完成之前就去操作采集资源（缓冲区/通道等）。与 5806（尝试初始化但初始化失败）不同：本码是"根本没走到已初始化"。属 5800~5820 图像源族"模块/资源"子群。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>补齐前置初始化步骤再操作，确保 init/allocate 成功后才使用；这类顺序依赖错乱不要靠重试掩盖。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_RES_NOT_INITIALIZED = 5807;

	/// <summary>图像源模块请求有歧义——匹配到多个（取值 5808），内嵌文本 <c>Image source module request is ambiguous</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>用于寻址采集模块的信息不够唯一，同时匹配到多个候选，无法确定该开哪一个。属 5800~5820 图像源族"模块/资源"子群（5806~5809）。与 5809（一个都没找到）相对。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>用更具体的标识（如设备序列号/唯一 ID）收窄寻址，别靠默认或通配；多相机同型号时尤其要显式指定。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_MOD_REQUEST_AMBIGUOUS = 5808;

	/// <summary>图像源模块未找到（取值 5809），内嵌文本 <c>Image source module not found</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>要寻址某个采集模块（interface/device 等 GenTL 模块层）却没找到对应项。属 5800~5820 图像源族里的"模块/资源"子群（5806~5809）。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>确认驱动/生产者已加载、设备已上电并被枚举到，再按正确标识寻址模块；与 5808（请求有歧义、匹配到多个）相反，本码是一个都没找到。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_MOD_NOT_FOUND = 5809;

	/// <summary>图像源参数：找不到该参数（取值 5810），内嵌文本 <c>Image source parameter not found</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>按名字去访问设备参数，但该参数在这台设备/该 GenICam 版本里根本不存在（拼错名、设备不支持、或须先满足条件节点才出现）。属 5800~5820 图像源族"参数"子群。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对参数名大小写与拼写、确认该设备型号是否真有此参数；某些参数需在特定模式下才可见，先切模式再访问。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_PARAM_NOT_FOUND = 5810;

	/// <summary>图像源参数：提供了非法的值（取值 5811），内嵌文本 <c>Image source parameter - wrong value provided</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>类型对但取值不在允许集/范围内（如枚举里没有该项、数值越界或不符合对齐约束）。与 5812（类型错）区分：本码是"类型对、值不对"。属 5800~5820 图像源族"参数"子群。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>查该参数的取值范围/枚举集（读其 min/max/inc 或枚举列表）后改到合法值；先只设必要参数跑通再逐项加回定位。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_PARAM_WRONG_VALUE = 5811;

	/// <summary>图像源参数：给了错的类型（取值 5812），内嵌文本 <c>Image source parameter - wrong type provided</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>设备参数要求某种节点类型（int/float/enum/string/bool…）却收到另一种。与算法侧的 1201~1220"控制参数类型"族语义相同、族段不同：这里是采集设备参数，那里是算子控制参数。属 5800~5820 图像源族"参数"子群。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>对照设备 XML 里该参数的节点类型重装取值；类型已对却仍失败则转查 5811（值越界）。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_PARAM_WRONG_VALUE_TYPE = 5812;

	/// <summary>图像源参数：该参数的值不可读（取值 5813），内嵌文本 <c>Image source parameter - value not readable</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>尝试读取一个只写、或在当前状态下不可查询的设备参数值。属 5800~5820 图像源族"参数"子群（5810~5816）。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>不要读只写量：某些命令类节点只有触发语义、无回读值；改从正确的可读属性取值。与 5814（值不可写）互为镜像。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_PARAM_VAL_NOT_READABLE = 5813;

	/// <summary>图像源参数：该参数的值不可写（取值 5814），内嵌文本 <c>Image source parameter - value not writable</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>尝试写入一个只读的设备参数值（如正在采集时锁定、或该节点本就是只读）。属 5800~5820 图像源族"参数"子群（5810~5816）。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>别写只读量：确认设备是否处于阻止写入的状态（常需先停止采集/解锁），改由可写的正确节点设置。与 5813（值不可读）互为镜像。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_PARAM_VAL_NOT_WRITABLE = 5814;

	/// <summary>图像源参数：请求了该参数不具备的属性（取值 5815），内嵌文本 <c>Image source parameter - property not available</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>想读设备某参数的一个 GenICam 属性，但当前设备/该参数根本没有这个属性（如去问一个只读节点的可写标志）。属 5800~5820 图像源族的"参数"子群（5810~5816）。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>核对该参数在设备 XML 里的属性集，只访问其确实具备的属性；与 5813/5814（可读/可写）区别在于本码是"属性压根不存在"而非"存在但方向不对"。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_PARAM_PROP_NOT_AVAILABLE = 5815;

	/// <summary>图像源参数：下发命令后在超时内未完成（取值 5816），内嵌文本 <c>Image source parameter - command timeout</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>向采集设备执行一条 GenICam 命令（如触发、软重启）时，设备未在时限内回 ack。属 5800~5820 图像源族里的"参数/命令"子群（5810~5816）。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>先查设备是否卡住/忙、命令是否需更长超时或前置条件（如须先停止采集再下发），而非改算法参数。注意它专指"命令执行超时"，与 5818 的"取图超时"是两回事。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_COMMAND_TIMEOUT = 5816;

	/// <summary>图像源操作失败——内部状态不对（取值 5817），内嵌文本 <c>Image source operation failed - wrong internal state</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>采集设备/图像源的内部状态机与本次操作不匹配（例如未开始采集就取图、已停止还下命令）。属 5800~5820 图像源族，与 5800 <c>Jl_ERR_IMGSRC_FAIL</c>（未知原因失败）相比，本码点明"状态"这一具体方向。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>按"顺序/前置状态"排查：先让设备回到合法状态（重新打开、按采集→取图→停止的次序走）再操作，而不是重试同一步。含义随原生版本可变，勿据码写稳定业务分支 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_WRONG_STATE = 5817;

	/// <summary>在配置的超时内没有收到任何图像帧（取值 5818），内嵌文本 <c>No images received within the configured timeout</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>取图等待自然到期：时限内没等到帧。与 5819 <c>Jl_ERR_IMGSRC_FETCH_ABORT</c>（等待被主动中止）不同，本码是"等满超时仍无图"。属 5800~5820 图像源族。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>方向是采集链路而非算法：确认相机出图/触发信号正常、超时值是否过短、线缆与连接是否稳定；重试前先排查硬件触发是否真的来了帧。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_FETCH_TIMEOUT = 5818;

	/// <summary>等待取图的过程被中止（取值 5819），内嵌文本 <c>Waiting for images aborted</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>采集正处于"等下一帧"的阻塞等待中被主动打断，故没拿到图。与 5818 <c>Jl_ERR_IMGSRC_FETCH_TIMEOUT</c>（超时自然到期）不同，本码强调"被人/被事件中断"。属 5800~5820 图像源族。</para>
	///   <para><b>可达性</b>本运行时未提供采集/图像源包装（能力已删除），正常托管路径一般取不到本码，来自原生采集栈。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>JlNativeApi.IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>按"结果未产出"处理：本码对应的取图没有可用输出，不要继续读图像句柄。多为关闭设备/取消采集/断连触发，确认是预期中止即可忽略，非预期则重开采集。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_FETCH_ABORT = 5819;

	/// <summary>采集图像源的像素数据格式转换失败（取值 5820），内嵌文本 <c>Pixel data conversion failed</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>相机/图像源交付的原始像素格式（如 Bayer、自定义像素位深）无法转换为目标图像类型。属 5800~5820 图像源（framegrabber 采集）族。</para>
	///   <para><b>可达性</b>本运行时托管层未提供采集/图像源算子与相关包装（该能力已删除），因此正常托管调用路径一般取不到本码，它来自原生采集栈。</para>
	///   <para><b>归类</b>本码 ≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>JlNativeApi.IsFailure</c> 判失败，若被送入 <see cref="M:JLVisionLib.JlOperatorException.throwOperator(System.Int32,System.String)"/> 一类统一检查会抛 <c>JlOperatorException</c>；文本由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>处置</b>若经自定义/桥接采集确遇此码：核对相机像素格式与请求的输出图像类型是否匹配（换用受支持的转换或先行解码），而非调算法参数。</para>
	/// </remarks>
	public const int Jl_ERR_IMGSRC_CONVERSION_FAILED = 5820;

	/// <summary>访问了未定义/非法的内存区域（取值 6000），内嵌文本 <c>Access to undefined memory area</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生侧解引用了一个不该碰的地址——空指针、已释放句柄或越界偏移。常量名 NP（Null Pointer 一类）与文本同指"访存非法"，属 6000~6003 内存族。</para>
	///   <para><b>归类</b>本码 ≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>JlNativeApi.IsFailure</c> 判失败，统一返回码检查会抛 <c>JlOperatorException</c>；文本可由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>何时遇到</b>常见诱因是把"已 Dispose 或从未初始化的对象"当有效句柄传入算子，或输入对象与实际数据尺寸不符。托管层对句柄有 <c>JlHandleBase.UNDEF</c>（未定义）占位，正常路径应在调用前拦截，若仍触发多与生命周期管理失当有关 [待实测]。</para>
	///   <para><b>处置</b>按"输入句柄可疑"排查：确认每个入参对象仍存活且未被 <c>Dispose</c>、尺寸类型匹配，再重试；不要与 6001 <c>Jl_ERR_MEM</c>（内存不足）混为一谈——本码是地址非法，调大内存无解。反复复现时连同触发算子名一并上报。</para>
	/// </remarks>
	public const int Jl_ERR_NP = 6000;

	/// <summary>原生运行时申请内存失败——可用内存不足（取值 6001）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌文本 <c>Not enough memory available</c>：原生 <c>JlAlloc</c> 系分配器要向系统要内存时拿不到，属真实的资源耗尽，不是尺寸算错（那类是 6003 <c>Jl_ERR_WMS</c>）。落在 6000~6003 内存族。</para>
	///   <para><b>归类</b>本码 ≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>JlNativeApi.IsFailure</c> 判失败，统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>何时遇到</b>大图像/大批量对象长期不释放、或一次性对超大输入做拷贝型算子时最易触发。托管侧 <c>JlObject</c> 系（<c>JlImage</c>/<c>JlRegion</c>/<c>JlXLD</c>/各模型）实现 <c>IDisposable</c>，返回的新句柄若不 <c>Dispose</c> 会持续占用原生内存——注意 GC 终结器不及时，光靠"等回收"往往来不及。</para>
	///   <para><b>处置</b>先释放不再用的中间结果句柄（<c>Dispose</c> 或 <c>using</c>），再降低单帧数据规模（分块、降分辨率、减小 ROI）后重试；若频繁复现，检查是否有句柄泄漏（只创建不释放）而非单纯调大内存。</para>
	/// </remarks>
	public const int Jl_ERR_MEM = 6001;

	/// <summary>堆上内存分区的边界被写坏（heap 分区被覆盖，取值 6002）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生运行时在堆分配块的守卫/边界区检测到"越界写"——某次操作写坏了相邻内存分区的元数据。常量名 ICM 与内嵌文本 <c>Memory partition on heap has been overwritten</c> 同指此事，属 6000~6003 内存族。</para>
	///   <para><b>归类</b>本码 ≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>JlNativeApi.IsFailure</c> 判失败，统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>何时遇到</b>这类"检测到已损坏"多半不是本算子的错，而是更早某处对原生缓冲区/句柄的越界写累积到此处才被发现；托管层正常经 <c>JlTuple</c>、<c>JlImage</c> 等包装走原生接口不应触发，出现时常与外部混用或原生版本不匹配有关 [待实测]。</para>
	///   <para><b>处置</b>不要指望重试自愈——堆已被写坏，进程状态不可信。记录码 + <c>JlNativeApi.GetErrorMessage(err)</c> 文本 + 触发算子名后按缺陷上报，并考虑重启运行时实例；排查方向是"谁越界写"，而非当前算子参数。</para>
	/// </remarks>
	public const int Jl_ERR_ICM = 6002;

	/// <summary>内存分配器 JlAlloc 收到"申请 0 字节"的请求（取值 6003），内嵌原生文本为 <c>JlAlloc: 0 bytes requested</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>常量名 WMS 与原生文本不完全对应：文本专指"某次 <c>JlAlloc</c> 传入的长度为 0"，即分配器被要求分配零字节块，而不是分配失败。它落在 6000~6003 的原生运行时内存/分配族段。</para>
	///   <para><b>归类</b>本码 ≥1000，<c>JlNativeApi.IsError(err)</c> 判 true，<c>JlNativeApi.IsFailure(err)</c>（判据 <c>err != 2</c>）也判失败，经 <see cref="M:JLVisionLib.JlOperatorException.throwOperator(System.Int32,System.String)"/> 一类的统一返回码检查会抛 <c>JlOperatorException</c>；文本可由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>何时遇到</b>通常是上游把一个"元素个数/尺寸"算成了 0（如空区域、零宽图像、退化几何）再拿去触发内部分配，分配器对 0 字节视为异常而非合法空分配。多由输入退化间接暴露，而非你直接写 0 申请 [待实测]。</para>
	///   <para><b>处置</b>别在分配点上查：回到产生该尺寸的算子输入，确认是否为空/退化对象；先判空或修正参数后重跑。与 6001 <c>Jl_ERR_MEM</c>（真·内存不足）区分：本码是"长度不对"，不是"内存不够"。</para>
	/// </remarks>
	public const int Jl_ERR_WMS = 6003;

	/// <summary>临时内存区从未分配过，却调用了释放（取值 6004），内嵌文本 <c>Tmp-memory management: Call freeing memory although nothing had been allocated</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生临时内存管理在执行 free 时发现该管理区压根没有分配过东西——释放的是"空气"，alloc/free 配对簿记出错。属 6004~6007 临时内存管理族，与前面 6000~6003 运行时内存族（访问非法/分配失败）分层不同。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>；文本可由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>何时遇到</b>临时内存池由原生算子内部自动复用，托管层不开放直接分配/释放它的接口，正常托管调用无从触发本码；出现时多与外部混用或版本不匹配破坏了原生簿记状态有关 [待实测]。</para>
	///   <para><b>处置</b>与兄弟码分工：本码是"没分配过就释放"，6005 <c>Jl_ERR_TMPNULL</c> 是"释放时指针为 NULL"，6006 <c>Jl_ERR_CNFMEM</c> 是"管理表里找不到该元素"。簿记类错误别指望重试自愈，带上触发算子名上报。</para>
	/// </remarks>
	public const int Jl_ERR_NOTMP = 6004;

	/// <summary>释放临时内存时遇到空指针（取值 6005），内嵌文本 <c>Tmp-memory management: Null pointer while freeing</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>临时内存管理里的释放动作拿到的块指针是 NULL——要释放的对象本身就是空引用，释放无从进行。与 6004 <c>Jl_ERR_NOTMP</c>（管理区压根没分配过）不同：本码是在正常地执行一次 free，只是目标指针为空。属 6004~6007 临时内存管理族。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>；文本可由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>何时遇到</b>典型成因是把"分配失败返回的空指针"未经检查又交给后续释放环节；临时内存池的 alloc/free 在原生算子实现内部闭环，托管路径一般不直接经手 [待实测]。</para>
	///   <para><b>处置</b>与 6000 <c>Jl_ERR_NP</c> 区别开：那是解引用非法地址的真实访存，本码只是指针值为 NULL 的记账，内存并未被碰坏。分配结果要先判空再释放；别默认"传 NULL 释放等于无害的空操作"——本运行时把它记成错误。</para>
	/// </remarks>
	public const int Jl_ERR_TMPNULL = 6005;

	/// <summary>临时内存管理表中找不到指定的元素（取值 6006），内嵌文本 <c>Tmp-memory management: Could not find memory element</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>释放/查询操作携带的内存元素标识在临时内存管理表里查无此记录；常量名 CNFMEM = could not find memory。常见指向是该元素早已被释放过（重复释放），或指针其实属于另一管理区。属 6004~6007 临时内存管理族。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>；文本可由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>何时遇到</b>与前两码分工：6004 是"该区没分配过"、6005 是"指针为 NULL"，本码是"指针非空但表里没有它的记录"，最典型对应 double free 或跨池释放 [待实测]。</para>
	///   <para><b>处置</b>按"释放发生了两次"排查：每份分配只释放一次，释放后即视为失效不再复用。报本码时无法确认第一次释放是否已成功，先定位重复释放的源头，再决定是否重跑流程。</para>
	/// </remarks>
	public const int Jl_ERR_CNFMEM = 6006;

	/// <summary>以错误的内存类型操作内存块（取值 6007），内嵌文本 <c>memory management: wrong memory type</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>内存管理对象被按不相符的内存类型使用——比如把另一类别的块交给某类专属的释放/查询路径去处理。注意本码文本前缀是 "memory management:" 而非 6004~6006 的 "Tmp-memory management:"，适用范围至少不限于临时区，确切边界在原生侧 [待实测]。属 6004~6007 管理族收尾。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>；文本可由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>何时遇到</b>托管调用的类型由签名锁定，不需要原生"猜"类型；出现本码多与跨原生结果混用指针、库版本不匹配有关 [待实测]。</para>
	///   <para><b>处置</b>与 6003 <c>Jl_ERR_WMS</c>（申请 0 字节，尺寸异常）区分：本码是类型不匹配。核对是否把 A 类对象传进了按 B 类处理的接口，换回正确类型的对象再操作。</para>
	/// </remarks>
	public const int Jl_ERR_WMT = 6007;

	/// <summary>显存不足（取值 6021），内嵌文本 <c>Not enough video memory available</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生侧向显存（video memory / GPU 驻留缓冲）要资源时拿不到。与 6001 <c>Jl_ERR_MEM</c>（主内存不足）是不同资源种类：给进程加主存无效，要降显存占用或释放显存驻留对象。6021 在段内自成一码，与 6004~6007 管理族隔开。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>；文本可由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>何时遇到</b>本运行时已删除显示/窗口能力，托管算子主要在主内存里工作；仅当原生路径启用 GPU 加速或显存驻留缓冲时才可能返回本码 [待实测]。</para>
	///   <para><b>处置</b>降低分辨率/批量、释放不再用的大对象，必要时重启运行时实例回收显存；反复出现则排查同机其他程序对 GPU 的占用与驱动状态。不要拿 6001 那套"释放句柄腾主存"的方案顶替，两码处置路线不同。</para>
	/// </remarks>
	public const int Jl_ERR_MEM_VID = 6021;

	/// <summary>截至目前没有任何已分配的内存块（取值 6041），内嵌文本 <c>No memory block allocated at last</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>内存管理被要求对"最近一次分配到的内存块"做操作，但此刻分配记录是空的（文本 "at last" 取"截至目前"之义；常量名 NRA 与 no recent/allocated record 相合），属记录查找失败而非资源不足 [待实测]。与 6040 <c>Jl_ERR_IAD</c>、6042、6043 同处 6040~6043 分配参数/记录族。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>；文本可由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>何时遇到</b>托管层不提供"对最近分配块直接动手"的入口，本码多由原生某流程在未发生（或已清空）分配记录的状态下执行释放/查询而间接触发 [待实测]。</para>
	///   <para><b>处置</b>与 6001 <c>Jl_ERR_MEM</c> 分清：那是分配真失败，本码是操作在分配记录缺席时被调用。上报时附触发算子与紧邻的前一步调用，定位是哪一步漏做了分配或过早清理了记录。</para>
	/// </remarks>
	public const int Jl_ERR_NRA = 6041;

	/// <summary>内存分配相关的系统参数彼此不一致（取值 6040），内嵌文本 <c>System parameter for memory-allocation inconsistent</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>与内存分配相关的系统参数组合（池规模、块数、对齐设定等，具体涉及哪几项在原生侧 [待实测]）自相矛盾，分配器在这种不一致状态下拒绝工作。属配置类错误，发生在业务数据参与之前；与 6041、6042、6043 同处 6040~6043 分配参数/记录族。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>；文本可由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>何时遇到</b>本运行时未向托管层开放内存池参数设置入口，正常安装不会走到这步；只有原生初始化流程或部署环境改动过分配参数时才可能报本码 [待实测]。</para>
	///   <para><b>处置</b>与 6001 <c>Jl_ERR_MEM</c> 分清：这是"参数对不上"而非"内存不够"，加大机器内存无效。回到运行时初始化配置，把分配相关参数恢复默认组合，再逐项改动定位是哪一项破坏了一致性。</para>
	/// </remarks>
	public const int Jl_ERR_IAD = 6040;

	/// <summary>申请内存时给出的对齐值非法（取值 6042），内嵌文本 <c>Invalid alignment</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>传给分配器的对齐（alignment）参数不符合平台要求；通行约定是"2 的幂且不低于平台最小对齐粒度"，确切判据在原生侧 [待实测]。常量名与文本一一对应，无缩写歧义；与 6040、6041、6043 同处 6040~6043 分配参数/记录族。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>；文本可由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>何时遇到</b>托管算子调用不向下传对齐参数，本码来自原生内部按对齐要求分配的路径 [待实测]。</para>
	///   <para><b>处置</b>与 6040 <c>Jl_ERR_IAD</c> 区分：那是多个参数彼此矛盾，本码是这一个对齐值自身非法。经自定义原生对接遇到时，把对齐值改成 2 的幂（1、2、4、8、16…）再试。</para>
	/// </remarks>
	public const int Jl_ERR_INVALID_ALIGN = 6042;

	/// <summary>函数的输入参数收到了 NULL 指针（取值 6043），内嵌文本 <c>Function was given a NULL ptr as input</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生函数在入口处检查到某个指针入参为 NULL，直接拒绝执行——这是前置防御记账，没有发生真实访存。与 6000 <c>Jl_ERR_NP</c>（解引用非法地址、真实访问违规）和 6005 <c>Jl_ERR_TMPNULL</c>（临时内存释放场景的 NULL 指针）区别开：本码是任意函数通用的 NULL 入参检查，数值上属 6040~6043 段。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>；文本可由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>何时遇到</b>多由把"从未赋值或分配失败未检查的对象"当有效输入传进原生调用所致；托管包装通常在调用前拦截，收到本码时优先排查绕过包装直接对接原生的输入 [待实测]。</para>
	///   <para><b>处置</b>定位是哪一个入参为 NULL，先完成分配/赋值再调用。入口检查拦住意味着进程状态未被污染，修正输入后可放心重跑，这是它比 6000 友好得多的地方。</para>
	/// </remarks>
	public const int Jl_ERR_NULL_PTR = 6043;

	/// <summary>进程创建失败（取值 6500），内嵌文本 <c>Process creation failed</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生侧拉起新进程未成功；常量名 CP 对应 create process。常见成因是目标程序不存在或路径找不到、权限不足、系统进程/线程数或句柄耗尽。6500 独立于前面的 6000~6043 内存族，7000 起是输出控制参数族，本码独占这段空档。</para>
	///   <para><b>可达性</b>本运行时托管层没有创建子进程的入口，正常托管路径取不到本码；仅当原生侧自行启动辅助进程（许可组件、并行 worker 等）时才可能返回 [待实测]。</para>
	///   <para><b>归类</b>≥1000，<c>JlNativeApi.IsError</c> 判 true、<c>IsFailure</c> 判失败，经统一返回码检查会抛 <c>JlOperatorException</c>；文本可由 <c>JlNativeApi.GetErrorMessage(err)</c> 现查。</para>
	///   <para><b>处置</b>按操作系统进程启动问题排查：运行账户权限、防护软件拦截、系统进程数上限，与算法参数无关；反复出现再查原生侧是否有进程句柄泄漏。环境未改变前盲目重试意义不大。</para>
	/// </remarks>
	public const int Jl_ERR_CP_FAILED = 6500;

	/// <summary>输出控制参数的索引错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>访问"输出控制参数"时给出的索引非法/越界。</para>
	///   <para><b>排查方向</b>与 WOCPVN（值个数错）、WOCPT（类型错）同族，本码专指索引本身；确认参数序号在有效范围内。</para>
	/// </remarks>
	public const int Jl_ERR_WOCPI = 7000;

	/// <summary>输出控制参数的值个数错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>提供给某个"输出控制参数"的值数量与该参数要求不符。</para>
	///   <para><b>排查方向</b>与 WOCPI（索引错）、WOCPT（类型错）同族，本码专指值的个数；补齐或删减实参。</para>
	/// </remarks>
	public const int Jl_ERR_WOCPVN = 7001;

	/// <summary>输出控制参数类型错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>对"输出控制参数"按错误的类型访问（期望与实际类型不符）。</para>
	///   <para><b>排查方向</b>与 WOCPI（索引错）、WOCPVN（值个数错）同族，本码专指类型；按参数实际类型存取。</para>
	/// </remarks>
	public const int Jl_ERR_WOCPT = 7002;

	/// <summary>对象键（输入对象）的数据类型错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>用于标识输入对象的键，其数据类型不符合要求。</para>
	///   <para><b>排查方向</b>核对键的类型（符号名 vs 序号）；键类型合法但根本不存在时会另报 UNKN（7105）。</para>
	/// </remarks>
	public const int Jl_ERR_WKT = 7003;

	/// <summary>整数值超出范围。</summary>
	/// <remarks>
	///   <para><b>含义</b>传入/取出的整数值越过了允许的取值范围（index out of range）。</para>
	///   <para><b>排查方向</b>检查索引或计数是否越界；与空指针、类型错误等区分，本码专指整型数值范围问题。</para>
	/// </remarks>
	public const int Jl_ERR_IOOR = 7004;

	/// <summary>Vision 版本不一致。</summary>
	/// <remarks>
	///   <para><b>含义</b>运行时各组件（库/模型文件）的 Vision 版本彼此不匹配。</para>
	///   <para><b>排查方向</b>统一升级到同一版本；常见于用新版工具生成的数据被旧版运行时读取。</para>
	/// </remarks>
	public const int Jl_ERR_IHV = 7005;

	/// <summary>为字符串分配的内存不足。</summary>
	/// <remarks>
	///   <para><b>含义</b>处理字符串输出时为字符串缓冲分配的内存不够。</para>
	///   <para><b>排查方向</b>与通用内存不足（Jl_ERR_MEM）不同，这里专指字符串缓冲；减少超长字符串或增大相应容量。</para>
	/// </remarks>
	public const int Jl_ERR_NISS = 7006;

	/// <summary>内部错误：Proc 为空。</summary>
	/// <remarks>
	///   <para><b>含义</b>运行期内部流程对象（Proc）为空指针。[待实测] 属内部一致性错误，通常由上游状态被破坏引起，而非用户直接可传入的条件。</para>
	///   <para><b>排查方向</b>作为兜底内部错误记录现场；若稳定复现，多半是流程句柄生命周期管理出了问题。</para>
	/// </remarks>
	public const int Jl_ERR_PROC_NULL = 7007;

	/// <summary>未知的符号对象键（输入对象）。</summary>
	/// <remarks>
	///   <para><b>含义</b>按符号名（字符串键）访问输入对象，但该键未被识别。</para>
	///   <para><b>排查方向</b>检查对象键拼写与是否已注册；与 WKT（7003，键数据类型错）不同，本码是键根本不存在。</para>
	/// </remarks>
	public const int Jl_ERR_UNKN = 7105;

	/// <summary>输出对象参数个数错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>请求的输出对象数量与算子定义不符。</para>
	///   <para><b>排查方向</b>核对算子输出对象的个数声明；与 7000 段的输出控制参数（WOC*）不同，本码专指"输出对象"。</para>
	/// </remarks>
	public const int Jl_ERR_WOON = 7200;

	/// <summary>期望输出类型为字符串（string）。</summary>
	/// <remarks>
	///   <para><b>含义</b>取输出值时给定的类型不是算子实际产生的 string 输出，类型不符。</para>
	///   <para><b>排查方向</b>用字符串方式读取该输出；与 OTLE（期望 long）、OTFE（期望 float）同族。</para>
	/// </remarks>
	public const int Jl_ERR_OTSE = 7400;

	/// <summary>期望输出类型为整型（long）。</summary>
	/// <remarks>
	///   <para><b>含义</b>取输出值时给定的类型不是算子实际产生的 long 输出，类型不符。</para>
	///   <para><b>排查方向</b>用整型方式读取该输出；与 OTSE（期望 string）、OTFE（期望 float）同族。</para>
	/// </remarks>
	public const int Jl_ERR_OTLE = 7401;

	/// <summary>期望输出类型为浮点（float）。</summary>
	/// <remarks>
	///   <para><b>含义</b>取输出值时给定的类型不是算子实际产生的 float 输出，类型不符。</para>
	///   <para><b>排查方向</b>用与输出层一致的实型方式读取；string/long/float 三种类型不符分别对应 OTSE、OTLE、OTFE。</para>
	/// </remarks>
	public const int Jl_ERR_OTFE = 7402;

	/// <summary>对象参数为空指针。</summary>
	/// <remarks>
	///   <para><b>含义</b>传入的对象参数（图像/区域等句柄）实际是空指针（零指针），不可解引用。</para>
	///   <para><b>排查方向</b>常见于对象已被释放却仍在使用，或从未成功创建；调用前确认句柄有效。</para>
	/// </remarks>
	public const int Jl_ERR_OPINP = 7403;

	/// <summary>元组已被删除，其中的值不再有效。</summary>
	/// <remarks>
	///   <para><b>含义</b>访问一个已经被释放/删除的 JlTuple，其元素值随之失效。</para>
	///   <para><b>排查方向</b>不要把值从元组取出后又在元组 Dispose 之后继续引用其底层数据；需要长期持有就先复制出来。</para>
	/// </remarks>
	public const int Jl_ERR_TWC = 7404;

	/// <summary>CNN：内部数据错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>CNN 运行期检测到内部数据不一致/损坏的兜底错误。[待实测] 英文说明过于笼统，具体触发点需结合调用上下文核实。</para>
	///   <para><b>排查方向</b>优先怀疑模型文件或输入缓冲被破坏；若可复现，记录触发它的算子以便进一步定位。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_DATA = 7701;

	/// <summary>CNN：内存类型非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>用于网络数据的内存类型（如 host/device 归属）不合法或不匹配。</para>
	///   <para><b>排查方向</b>核对数据缓冲所在的内存域与算子要求是否一致；与通用内存不足码（Jl_ERR_MEM）不是一回事。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_MEM = 7702;

	/// <summary>CNN：数据序列化非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>网络/权重数据的序列化格式损坏或不符合预期，读写还原失败。</para>
	///   <para><b>排查方向</b>常见于文件被截断、跨版本或跨端序读取；用匹配版本重新导出该模型文件。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_IO_INVALID = 7703;

	/// <summary>CNN：所需实现不可用。</summary>
	/// <remarks>
	///   <para><b>含义</b>请求的算子实现在当前构建/环境中不存在（例如未编译进该平台的 GPU 实现）。</para>
	///   <para><b>排查方向</b>改用可用的实现类型；与 IMPL_INVALID（类型值本身非法）区分：本码是类型合法但该实现没提供。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_IMPL_NOT_AVAILABLE = 7704;

	/// <summary>CNN：输入数据个数错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>提供的输入张量数量与网络要求的输入个数不符。</para>
	///   <para><b>排查方向</b>多输入网络要按定义的输入层逐一对齐个数；个数正确但形状/内容非法会另报 SHAPE/DATA 类码。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_NUM_INPUTS_INVALID = 7705;

	/// <summary>CNN：实现类型非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>指定的实现类型（如 CPU/GPU 等）取值非法、当前无法识别。</para>
	///   <para><b>排查方向</b>使用受支持的实现类型；与 IMPL_NOT_AVAILABLE（实现存在但不可用）区分：本码是类型本身不合法。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_IMPL_INVALID = 7706;

	/// <summary>CNN：当前环境不支持训练。</summary>
	/// <remarks>
	///   <para><b>含义</b>在当前运行环境（如缺相应 GPU/库或许可）下无法执行训练。</para>
	///   <para><b>排查方向</b>仅做推理，或补齐训练所需的环境条件；与训练过程失败 TRAINING_FAILED 不同，本码是"根本没开训练能力"。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_TRAINING_NOT_SUP = 7707;

	/// <summary>CNN：本操作需要满足最低要求的 GPU。</summary>
	/// <remarks>
	///   <para><b>含义</b>该操作要求具备一定最低规格的 GPU，而当前设备不满足。</para>
	///   <para><b>排查方向</b>参照安装指南确认 GPU 型号/算力；若只是想跑 CPU，则应改用不依赖 GPU 的实现类型。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_GPU_REQUIRED = 7708;

	/// <summary>CNN：本操作需要 CUDA 库，但未安装。</summary>
	/// <remarks>
	///   <para><b>含义</b>所用操作依赖 CUDA 库，而运行环境未能找到它。</para>
	///   <para><b>排查方向</b>按安装指南补装 CUDA；cuDNN/cuBLAS 缺失分别有独立错误码。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_CUDA_LIBS_MISSING = 7709;

	/// <summary>读取 OCR 分类器文件时出错。</summary>
	/// <remarks>
	///   <para><b>含义</b>载入 OCR（CNN 分类器）文件过程中发生读取错误。</para>
	///   <para><b>排查方向</b>检查文件是否损坏或路径是否正确；版本不匹配会另报 FILE_WRONG_VERSION。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_CNN_RE = 7710;

	/// <summary>OCR/CNN：通用参数名错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>OCR 结合 CNN 的算子收到了不被识别的通用参数名。</para>
	///   <para><b>排查方向</b>核对该算子支持的参数名；纯 CNN 场景的同名错误为 CNN_WGPN。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_CNN_WGPN = 7711;

	/// <summary>某参数返回多值，必须互斥使用。</summary>
	/// <remarks>
	///   <para><b>含义</b>某个输出参数会返回多个值，只能单独使用它，不能与其它输出同时请求。</para>
	///   <para><b>排查方向</b>拆分调用：需要该多值输出时，去掉同一次调用里冲突的其它输出参数。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_CNN_EXCLUSIV_PARAM = 7712;

	/// <summary>CNN：通用（generic）参数名错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>向 CNN 相关算子传入了不被识别的通用参数名。</para>
	///   <para><b>排查方向</b>核对该算子支持的参数名字符串；OCR 场景下的同名错误另见 OCR_CNN_WGPN。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_WGPN = 7713;

	/// <summary>CNN：标签非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>提供给网络的标签数据不合法（超出类别范围或格式不符）。</para>
	///   <para><b>排查方向</b>核对标签取值与类别定义；类别集合含重复项时会另报 MULTIPLE_CLASSES。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_INVALID_LABELS = 7714;

	/// <summary>OCR 文件版本不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>读取的 OCR 文件版本与当前运行时不匹配。</para>
	///   <para><b>排查方向</b>用匹配版本的工具重新生成/保存该文件；与相邻 OCR_CNN_RE（读取过程出错）分别对应"版本不符"和"读取失败"。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_CNN_FILE_WRONG_VERSION = 7715;

	/// <summary>CNN：类别非法，至少有一个类别重复出现。</summary>
	/// <remarks>
	///   <para><b>含义</b>训练/标注所用的类别集合里出现重复项。</para>
	///   <para><b>排查方向</b>让每个类别只出现一次；与相邻 INVALID_LABELS（标签非法）配套，一个查标签、一个查类别去重。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_MULTIPLE_CLASSES = 7716;

	/// <summary>CNN：本操作需要 cuBLAS 库，但未安装。</summary>
	/// <remarks>
	///   <para><b>含义</b>所用操作依赖 cuBLAS（GPU 线性代数）库，而环境未能找到它。</para>
	///   <para><b>排查方向</b>参照安装指南补装 cuBLAS；与 CUDA/CUDNN 缺失码分别对应不同依赖库。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_CUBLAS_LIBS_MISSING = 7717;

	/// <summary>CNN：本操作需要 cuDNN 库，但未安装。</summary>
	/// <remarks>
	///   <para><b>含义</b>所用操作依赖 cuDNN 库，而运行环境未能找到它。</para>
	///   <para><b>排查方向</b>参照安装指南补装 cuDNN；与 CUDA_LIBS_MISSING（缺 CUDA）、CUBLAS_LIBS_MISSING（缺 cuBLAS）分别对应不同依赖库。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_CUDNN_LIBS_MISSING = 7718;

	/// <summary>找不到 OCR 支持文件 find_text_support.hotc。</summary>
	/// <remarks>
	///   <para><b>含义</b>文字查找所需的资源文件 find_text_support.hotc 未在预期位置找到。</para>
	///   <para><b>排查方向</b>把该文件放到安装根目录的 ocr 子目录，或当前工作目录下。属 OCR 文本资源缺失，与深度学习模型加载无关。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_FNF_FIND_TEXT_SUPPORT = 7719;

	/// <summary>CNN：一次训练步骤失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>训练迭代执行失败，常由不合适的训练参数引起。</para>
	///   <para><b>排查方向</b>调整学习率、批量等训练超参数；数据异常亦可能触发，需结合上下文进一步定位。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_TRAINING_FAILED = 7720;

	/// <summary>CNN：图中权重已被覆盖，预训练权重丢失。</summary>
	/// <remarks>
	///   <para><b>含义</b>本想复用预训练权重，但图（Graph）中的权重此前已被改写而不再可用。</para>
	///   <para><b>排查方向</b>在权重被覆盖前完成预训练权重的载入；否则只能从头训练。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_NO_PRETRAINED_WEIGHTS = 7721;

	/// <summary>CNN：输入尺寸过小，无法产生有意义特征。</summary>
	/// <remarks>
	///   <para><b>含义</b>设定的新输入尺寸太小，经过网络下采样后无法保留有效特征。</para>
	///   <para><b>排查方向</b>放大输入尺寸；与 INVALID_IMAGE_SIZE（检测模型尺寸约束）不同，本码针对一般网络的"过小"下限。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_INVALID_INPUT_SIZE = 7722;

	/// <summary>CNN：结果尚不可用。</summary>
	/// <remarks>
	///   <para><b>含义</b>请求某个训练/推理结果，但该结果当前尚未产生或已被丢弃。</para>
	///   <para><b>排查方向</b>先完成对应计算（如训练/前向）再读取；不要在未执行流程时直接取结果。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_RESULT_NOT_AVAILABLE = 7723;

	/// <summary>CNN：输入通道数只能为 1 或 3。</summary>
	/// <remarks>
	///   <para><b>含义</b>试图把网络输入通道数设为除 1、3 之外的值。</para>
	///   <para><b>排查方向</b>灰度用 1、彩色用 3；其它通道数不被接受。方向性限制另见 DEPTH_NOT_AVAILABLE。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_INVALID_INPUT_DEPTH = 7724;

	/// <summary>CNN：单通道网络无法改设为三通道输入。</summary>
	/// <remarks>
	///   <para><b>含义</b>网络已按通道数 1 规定义，不能再把输入通道数设为 3。</para>
	///   <para><b>排查方向</b>需要彩色输入时应一开始就按 3 通道建网；与 INVALID_INPUT_DEPTH（通道数只能为 1 或 3）配套：本码是"1 改不到 3"的方向性限制。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_DEPTH_NOT_AVAILABLE = 7725;

	/// <summary>CNN：设备批量大于总批量，配置矛盾。</summary>
	/// <remarks>
	///   <para><b>含义</b>单设备一次处理的批量（device batch size）超过了总的批量大小，二者关系不成立。</para>
	///   <para><b>排查方向</b>使设备批量不超过总批量；与 BATCH_SIZE_OVERFLOW（乘积溢出）不同，本码是配置之间的大小矛盾。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_INVALID_BATCH_SIZE = 7726;

	/// <summary>CNN：某参数的规格定义非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>参数的规格描述本身无效（如声明了该算子不接受的参数名或非法组合）。</para>
	///   <para><b>排查方向</b>比具体取值错误更靠前，指"参数规格/名字"层面就不被接受；核对所用算子支持的参数集合。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_INVALID_PARAM_SPEC = 7727;

	/// <summary>CNN：所需内存超过允许上限。</summary>
	/// <remarks>
	///   <para><b>含义</b>网络（含批量、参数、中间缓冲）估算出的内存占用超过了本实现设定的最大允许值。</para>
	///   <para><b>排查方向</b>缩小模型规模或批量；这是"超过人为上限"，与通用内存不足（Jl_ERR_MEM / Jl_ERR_MEM_VID）不是同一路径。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_EXCEEDS_MAX_MEM = 7728;

	/// <summary>CNN：新的批量大小导致整数溢出。</summary>
	/// <remarks>
	///   <para><b>含义</b>设置的 batch size 与网络其它维度相乘后超出整型可表示范围。</para>
	///   <para><b>排查方向</b>减小批量或输入尺寸；与相邻 INVALID_BATCH_SIZE（设备批量大于总批量）区分：本码是乘积溢出而非配置矛盾。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_BATCH_SIZE_OVERFLOW = 7729;

	/// <summary>CNN：目标检测模型的输入图像尺寸非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>输入图像的尺寸不满足检测类网络的约束（此类网络通常要求宽高为特定步长的整数倍）。</para>
	///   <para><b>排查方向</b>仅针对检测模型；把输入图像缩放到该网络接受的尺寸。与普通分类网络的 INVALID_INPUT_SIZE（尺寸过小）是不同码。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_INVALID_IMAGE_SIZE = 7730;

	/// <summary>CNN：当前层某参数的取值非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>参数的类型与个数都正确，但具体取值落在该层允许的范围之外。</para>
	///   <para><b>排查方向</b>检查越界的数值（如负数尺寸、非法枚举字符串）；与 INVALID_LAYER_PARAM_TYPE/NUM 分别对应"值/类型/个数"三种不符。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_INVALID_LAYER_PARAM_VALUE = 7731;

	/// <summary>CNN：当前层的参数个数非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>传给某一图层的参数数量与该层要求不符。</para>
	///   <para><b>排查方向</b>类型正确但数量不对时命中本码；对照该层的参数签名补齐或删减实参。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_INVALID_LAYER_PARAM_NUM = 7732;

	/// <summary>CNN：当前层某参数的类型非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>为网络中某一图层设置的参数，其数据类型与该层期望类型不符（如应为整型却传了实型/字符串）。</para>
	///   <para><b>排查方向</b>针对具体层核对其参数表所声明的类型；与相邻 INVALID_LAYER_PARAM_NUM（个数不符）、INVALID_LAYER_PARAM_VALUE（取值越界）区分定位。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_INVALID_LAYER_PARAM_TYPE = 7733;

	/// <summary>CNN：输出数据个数与网络要求不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>前向计算后收集到的输出张量数量与网络（Graph）定义的输出层数不一致。</para>
	///   <para><b>排查方向</b>常见于自定义/拼装网络增删了输出层之后；核对读取输出的个数与图中输出层数量。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_NUM_OUTPUTS_INVALID = 7734;

	/// <summary>CNN：输入数据的形状（维度排列）非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>输入张量的维度排列与网络首层所要求的形状不一致（如批量、通道、高、宽的数目对不上）。</para>
	///   <para><b>排查方向</b>强调"维度层面"不匹配，区别于 INVALID_INPUT_DATA（缓冲内容非法）与 INVALID_INPUT_SIZE（尺寸过小）。核对网络定义的输入层形状描述。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_INVALID_SHAPE = 7735;

	/// <summary>CNN：传给网络算子的输入数据非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>提供给卷积神经网络（CNN）算子的输入数据不符合该网络要求的格式或状态，无法参与前向或反向计算。</para>
	///   <para><b>排查方向</b>与相邻错误码区分：形状维度不符用 INVALID_SHAPE，尺寸过小无法提特征用 INVALID_INPUT_SIZE，本码指数据缓冲本身内容/格式非法。检查通道数、数值类型及数据是否已就绪。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_INVALID_INPUT_DATA = 7736;

	/// <summary>CNN：变长输入下 CTC 损失层在旧版 cuDNN 上算不出正确梯度。</summary>
	/// <remarks>
	///   <para><b>含义</b>CTC 损失要处理长度不等的序列，而版本低于 7.6.3 的 cuDNN 只在序列长度固定时给出正确梯度：形状检查都能通过，训练也照常跑完，但反传的梯度是错的，模型质量静默变差而不抛异常。</para>
	///   <para><b>排查方向</b>这是运行库版本问题而非调用参数问题：要么把 cuDNN 升到 7.6.3 及以上，要么放弃变长输入、把批次内序列补齐到同一长度。仅在训练（需要反传）时才会命中，纯前向推理不受影响 [待实测]。与库加载失败造成的 RUNTIME_FAILED 区分：那种情况算子直接跑不起来，本码是算子能跑但数值不对。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_CUDNN_CTC_LOSS_BUGGY = 7737;

	/// <summary>CNN：卷积/池化层的 padding 设置本身不合法。</summary>
	/// <remarks>
	///   <para><b>含义</b>补齐宽度越出该层支持的范围（负值、非整数，或该层不支持上下左右各边独立取值）。padding 直接决定输出特征图尺寸：算出来的高宽为 0 或负数时，错误会在下游层才暴露，故在本层先拦下。</para>
	///   <para><b>排查方向</b>先看层参数表里 padding 允许几种写法（单边值还是四边各一值），再核对补齐量是否超过卷积核尺寸的一半以上。与 INVALID_LAYER_PARAM_VALUE 区分：后者指任意层参数的取值越界，本码专门指 padding 这一项。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_INVALID_PADDING = 7738;

	/// <summary>CNN：读回的网络文件里出现了无法识别的层类型标记。</summary>
	/// <remarks>
	///   <para><b>含义</b>反序列化时按名字/编号去查层类型，注册表里没有对应项。多为文件与当前库版本不匹配（新版本另存的图拿到旧版本读）、文件被截断或与手工编辑过的内容错位，也可能该层属于本机未启用的后端。</para>
	///   <para><b>排查方向</b>先确认文件由同版本或更低版本写出；网络层（Graph）这一层的同类问题用 GRAPH_IO_INVALID，CNN 图对象（含参数/统计量）用 CNNGRAPH_IO_INVALID，本码指 CNN 层类型这一项。用同一套 API 重新另存一次可验证是否只是文件损坏。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_IO_INVALID_LAYER_TYPE = 7740;

	/// <summary>CNN：前向推理整体失败，属于兜底码。</summary>
	/// <remarks>
	///   <para><b>含义</b>输入已通过所有形状、尺寸与格式校验，计算真正执行时失败 [待实测]。典型诱因是显存/内存不足（批量或单张尺寸过大）、权重文件与图的层结构对不上、中间结果出现数值溢出。它不指明坏在哪一层，需要逐个排除。</para>
	///   <para><b>排查方向</b>按代价从低到高试：换 CPU 后端或把批量降到 1 再跑，能跑通即为容量问题；重新加载配对的权重；把输入换成全零图，若仍失败则问题在模型而非数据。输入本身不合规定不该落在本码：形状不符用 INVALID_SHAPE，缓冲非法用 INVALID_INPUT_DATA，后端不可用用 RUNTIME_FAILED。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_INFERENCE_FAILED = 7741;

	/// <summary>CNN：所选计算后端在本机跑不了，与输入数据无关。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生说明为 "Runtime not supported on this machine"：装载/初始化所选运行时失败，例如按 GPU 后端调用但机器无兼容显卡、显卡驱动或 cuDNN 版本与库不匹配、缺相应 DLL。错误发生在建图与执行之前，因此同样的调用换成可用后端应当直接通过。</para>
	///   <para><b>排查方向</b>先把后端切到 CPU 验证是否只是加速库缺失；再核对显卡驱动与加速库版本。与相邻两码区分：LAYER_UNSUPPORTED 只表示某一层在该后端没有实现（其余层可跑），INFERENCE_FAILED 是后端正常、单次计算失败，本码是整个后端起不来。另注：本仓库托管层未提供 CNN 图相关包装类，本码只能来自原生调用返回。</para>
	/// </remarks>
	public const int Jl_ERR_CNN_RUNTIME_FAILED = 7742;

	/// <summary>Graph：图引擎内部失败，原生未给出可归类的具体原因。</summary>
	/// <remarks>
	///   <para><b>含义</b>图（Graph）构建/连接阶段的兜底码：调用参数逐项看都合规，引擎内部状态却不自洽。常见来源是把同一个层对象挂到两张图、重复连接同一对节点，或图被并发修改 [待实测]。</para>
	///   <para><b>归类</b>7751–7753 属通用 Graph 族，7760–7778 属 CNN 图（CNNGRAPH）族，7779 起属 DL 模型族。三者名字相似但层次不同：先按码值确定族，再去对应层的接口找参数，不要在 Graph 族的码上排查 CNN 层参数。</para>
	///   <para><b>排查方向</b>本码没有指向性，靠缩减复现：把图拆成最小可运行版本逐步加层，加到哪一步复现即为该步的连接或生命周期有问题；同时用异常消息（原生实时查表拼出）而不是仅凭码值判断。</para>
	/// </remarks>
	public const int Jl_ERR_GRAPH_INTERNAL = 7751;

	/// <summary>Graph：图的序列化数据流读不出合法内容。</summary>
	/// <remarks>
	///   <para><b>含义</b>写入或读出图时数据流不符合本格式的约定：文件不是由本引擎写出、被别的工具或文本方式打开后另存过（编码/换行改动即可破坏二进制流）、写到一半中断留下残缺尾、或版本格式已变。IO 前缀指读写通道，问题在字节流层面。</para>
	///   <para><b>排查方向</b>先比文件大小与另存时间戳，确认写入是否完整；用同版本重新另存一份覆盖验证。若流可读通、只是某个层类型认不出，会是 CNN 族的 IO_INVALID_LAYER_TYPE；若要连的是 CNN 图对象（含权重与统计量）而非通用图，对应码为 CNNGRAPH_IO_INVALID。</para>
	/// </remarks>
	public const int Jl_ERR_GRAPH_IO_INVALID = 7752;

	/// <summary>Graph：按序号取图里的节点/层，序号越界。</summary>
	/// <remarks>
	///   <para><b>含义</b>用整数下标定位图中的某个节点或层，该下标不在 0 到"层数减一"的区间内。图的层序由连接顺序决定，不是由添加顺序保证稳定，删一层后所有后续下标整体前移 [待实测]。</para>
	///   <para><b>排查方向</b>不要用魔法数字写死下标：先取当前层数再校验，或改按层名索引。与 CNNGRAPH_INVALID_IDX 的区别是族属（通用图与 CNN 图），与 CNNGRAPH_AUX_INDEX_OOB 的区别是索引对象（主输出与辅助输出 aux）。若码来自一段批量循环，优先怀疑"上一轮已删过层"。</para>
	/// </remarks>
	public const int Jl_ERR_GRAPH_INVALID_INDEX = 7753;

	/// <summary>CNN 图：建图或改图过程中的内部兜底错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>CNN 图对象（7760–7778 族）的通用失败码，表示图的状态机走到了引擎认为不该出现的组合，原生未细化原因。多出现在层已加入但尚未初始化完成、或对同一张图重复做了互斥操作之后 [待实测]。</para>
	///   <para><b>归属</b>英文原文里的图对象类名在本仓库托管层没有对应类型（CNN/深度学习封装已不提供），本码只作为原生返回码出现在异常里，不要按托管类去找调用点。</para>
	///   <para><b>排查方向</b>比 GRAPH_INTERNAL 更可能牵涉参数与权重：先确认是否已按"建层—连接—初始化"的顺序完成；把可疑层单独成图复现。明确的初始化缺失应命中 NOINIT，规格非法应命中 SPEC 或 DEF，本码是排除它们之后剩下的情况。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_INTERNAL = 7760;

	/// <summary>CNN 图：读写的图数据流内容非法（含权重/统计量段）。</summary>
	/// <remarks>
	///   <para><b>含义</b>与 GRAPH 族的同名码相比，本码针对的是 CNN 图这一更完整的对象：除拓扑结构外还带权重与批归一化统计量。文件缺段、版本不匹配、把纯结构文件当整图读、或路径被别的程序覆盖过，都会在这里断掉。</para>
	///   <para><b>排查方向</b>先分清坏在哪一段：只报层类型不认识是 CNN 族的 IO_INVALID_LAYER_TYPE；连结构头都对不上才是本码。可用"只读结构不读权重"的方式二次验证，能把权重损坏与结构损坏分开。文件名与扩展名不要手写拼接，跨平台换行与编码改写二进制流是常见成因。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_IO_INVALID = 7761;

	/// <summary>CNN 图：层的定义本身不成立，无法并入图。</summary>
	/// <remarks>
	///   <para><b>含义</b>该层的规格描述被拒绝：类型缺失或写法不认识、必需的连接（输入来源）没给、层参数整段没填。注意它管"这一项存不存在、写法对不对"，不管"值在不在允许范围内"。</para>
	///   <para><b>排查方向</b>与三个近邻分层定位：参数类型不符 INVALID_LAYER_PARAM_TYPE、个数不符 INVALID_LAYER_PARAM_NUM、取值越界 INVALID_LAYER_PARAM_VALUE；本码是更上游的层规格问题。手工拼装网络时优先核对上一版复制过来的层模板是否漏改类型名，以及该层是否要求显式的辅助输出选择。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_LAYER_INVALID = 7762;

	/// <summary>CNN 图：图还没完成初始化就被使用。</summary>
	/// <remarks>
	///   <para><b>含义</b>图对象处于"只建了壳、拓扑与参数尚未展开"的状态：此时取层信息、读输出形状或执行计算都会回本码。它与初始化失败不同——失败会有具体规格类错误码，本码表示初始化那一步根本没做或被跳过。</para>
	///   <para><b>排查方向</b>典型成因是分支里提前返回、异常被吞掉导致后续步骤没执行，或把"新建"与"初始化"当成同一次调用。检查建图序列是否完整走完再使用；同一份代码在别处能跑时，比对两次是否少了一次初始化调用。与 SPEC_STATUS 区分：后者是图已初始化、但其规格处于不允许当前操作的状态。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_NOINIT = 7763;

	/// <summary>CNN 图：缓冲区/显存类型的标记不合法。</summary>
	/// <remarks>
	///   <para><b>含义</b>图按"数据在哪一侧、按什么对齐与精度存放"来安排中间结果，本码表示给的存储类型标记不在该图支持的枚举内，或与该图已有的设备选择矛盾（例如结构按主机内存登记、执行却要求设备内存）。</para>
	///   <para><b>排查方向</b>常见于把 GPU 建的图搬到无加速环境复用、或手工改过图的元信息：先确认后端选择，再让图重新初始化而不是沿用旧登记。与数值类型错配区分：输入数据本身类型/内容非法是 INVALID_INPUT_DATA，层参数类型不符是 INVALID_LAYER_PARAM_TYPE，本码只涉及存储侧标记。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_INVALID_MEM = 7764;

	/// <summary>CNN 图：层的数量为零或与登记值不一致。</summary>
	/// <remarks>
	///   <para><b>含义</b>NUML 即 number of layers。图声明的层数与实际收集到的层对不上：一张层都没加的空图被初始化、批量拼装时循环少跑一轮、或加载权重后层数与结构描述不符。它与"某个索引越界"是两码事——那是 INVALID_IDX。</para>
	///   <para><b>排查方向</b>先取回引擎统计的层数与自己记录的数字比对，差异通常来自拼装中途异常退出而对象没丢弃、被继续复用；这种情况重建图比重试更可靠。加载预训练结构做迁移改造时，删层/并层后必须让登记值同步，否则命中本码。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_INVALID_NUML = 7765;

	/// <summary>CNN 图：按索引访问层，索引不在有效范围内。</summary>
	/// <remarks>
	///   <para><b>含义</b>用整数索引取 CNN 图的某一层或其输出，索引落在有效层区间之外。负数（含 -1 这类"表示最后一个"的惯用写法）是否被接受需实测 [待实测]，容易踩。</para>
	///   <para><b>排查方向</b>索引来源优先怀疑三处：改过类数或删层后仍用旧值、把批量循环的下标与层下标混用、以及用层名查到的序号未随图更新。同一族里主输出越界用本码，辅助输出（aux）越界用 AUX_INDEX_OOB，通用图的索引问题用 GRAPH_INVALID_INDEX。索引值恰好等于层数时，几乎必然是把"个数"当下标用了。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_INVALID_IDX = 7766;

	/// <summary>CNN 图：规格所处的阶段不允许当前操作（顺序/状态问题）。</summary>
	/// <remarks>
	///   <para><b>含义</b>SPEC_STATUS 指"规格的状态"，而非规格内容对不对：图已按某种阶段定型（如已初始化、已切换为推理或训练用途），此时再做只在该阶段之前允许的动作（改类数、改层、改输入尺寸）就会被拒。同一段代码单独跑通、串起来才报错，多半是本码。</para>
	///   <para><b>排查方向</b>把修改全部挪到初始化之前完成，初始化之后只读不写。与 NOCHANGE 区分：NOCHANGE 专指"初始化后不得改图结构"这一条硬性规则，本码覆盖其它阶段与操作不匹配的情况；与 SPEC 区分：SPEC 是规格内容本身非法。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_SPEC_STATUS = 7767;

	/// <summary>CNN 图：初始化之后改图结构，被硬性禁止。</summary>
	/// <remarks>
	///   <para><b>含义</b>图一旦初始化，层集合与连接就被固化（引擎已按当时结构展开中间缓冲与权重布局），此后再增删层或改连接一律拒绝。这不是参数取值问题，改数值参数不受本码约束。</para>
	///   <para><b>排查方向</b>正确做法是按新结构重建图再初始化，而不是往旧对象上继续加层；若必须保留已训练权重，先另存再重新装载到新图。与相邻两码区分：改输出类数被架构拒绝是 NO_CLASS_CHANGE，改图像尺寸被拒绝是 NO_IMAGE_RESIZE，二者是"架构不支持"，本码是"时机不允许"。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_NOCHANGE = 7768;

	/// <summary>CNN 图：图要求的前处理没有配，输入未经变换就送来。</summary>
	/// <remarks>
	///   <para><b>含义</b>该图登记了必需的前处理（把图像换算成网络首层期望的表示，如尺寸归一、通道排列、均值与比例变换），但实际未设置或未生效。缺它不会报"尺寸不符"，而是直接报本码 [待实测]。</para>
	///   <para><b>排查方向</b>先看是否只建了网络、忘了配前处理这一步；换了预训练结构后前处理需按新结构的取值重配。坑在于：前处理与训练时不一致时不报错，结果只是精度明显掉，所以修好本码后还要用同一份预处理的验证集复核。输入本身格式非法属 INVALID_INPUT_DATA，维度排列不符属 INVALID_SHAPE。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_PREPROC = 7769;

	/// <summary>CNN 图：某节点的连线数（入度/出度）与该层要求不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>degree 即图中节点的连接数。该层需要的输入连接数与实际连上的不一致：多路输入层只连了一路、把一个输出连给两个目标却未用复制/分支节点、或层留在图里却完全没接线。拓扑能画通，但数据流算不出来。</para>
	///   <para><b>排查方向</b>按层类型查它要求的入度（一元层 1、逐元素二元层 2、多路拼接层为可配的 N），再从可疑节点往两侧各查一条边。与 OUTSHAPE 区分：连接数对了但两侧形状对不上才是 OUTSHAPE；与 LAYER_INVALID 区分：层的规格描述本身缺失是 LAYER_INVALID，本码只批评连接数。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_DEGREE = 7770;

	/// <summary>CNN 图：某层推导出的输出形状不合法。</summary>
	/// <remarks>
	///   <para><b>含义</b>按该层的参数（核尺寸、步长、补齐、池化窗口）从输入形状推算输出形状，结果出现 0 或负数，或与该层声明的期望形状矛盾。它通常在结构阶段而非运行阶段暴露，是"参数组合算不出合法尺寸"，与整张图形状对不上（INVALID_SHAPE）不同。</para>
	///   <para><b>排查方向</b>先手算一遍：输出边长约等于（输入边长 加两侧补齐 减核尺寸）除以步长 再加一（向下取整）；把该层输入尺寸代进去，出现非正即为成因。连续减半的深层网络里，前几层用大步长会把后面层的特征图压到 0，此时该改的是靠前的层而不是报错这层。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_OUTSHAPE = 7771;

	/// <summary>CNN 图：图的规格描述（spec）内容本身非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>描述"这是一张什么样的网络"的那份规格被拒绝：字段缺失、互斥项同时给出、或规格与实际提交的层/权重不匹配。它是内容级判定，与 SPEC_STATUS（内容可能合法但阶段不对）、DEF（图定义整体不成立）相邻但不同层。</para>
	///   <para><b>排查方向</b>拿一份已知能跑通的规格做逐项 diff，比逐字段猜更快；改了用途（分类改检测、训练改推理）时，规格要与新用途重写。同一份规格在旧版本可用而现在报本码，先怀疑版本间字段含义变化，而不是回去改输入图像。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_SPEC = 7772;

	/// <summary>CNN 图：整张图的定义不成立（结构级问题）。</summary>
	/// <remarks>
	///   <para><b>含义</b>DEF 指图的整体定义：没有可用的输入层或输出层、存在孤立节点、连接形成环路使拓扑无法定序。单层的规格问题会先被 LAYER_INVALID 拦下，本码是"把层拼起来之后整体不通"。</para>
	///   <para><b>排查方向</b>按数据流方向做一次人工走查：从首层能否到达输出层、有没有分支合流处少了一路、有没有把输出又连回前面。多输出网络改了输出层选择后最易触发。若同时怀疑 aux 支路，先看 AUX_SPEC——那条码专指辅助输出未接选择层。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_DEF = 7773;

	/// <summary>CNN 图：网络结构不支持自动适配输出类别数。</summary>
	/// <remarks>
	///   <para><b>含义</b>建模型时要求把输出类别数改写成新的数目（复用预训练图、只换分类头），但当前网络的末端结构无法安全替换成另一种类数：输出头与后续结构耦合，或该结构根本不以"类别数"为可配参数（如检测、分割头）。图本身合法，只是"改类数"这一动作做不了。</para>
	///   <para><b>排查方向</b>确认所选结构是否为纯分类末端；需要自定义类数时，改用支持替换输出层的 backbone，或按目标类数重新导出/训练整个头部。与相邻码区分：NO_IMAGE_RESIZE（7775）是"改输入尺寸"不被支持，同族但方向相反；类别 id 内容层面的错误走 DL_CLASS_IDS_* 一族（7792 起）。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_NO_CLASS_CHANGE = 7774;

	/// <summary>CNN 图：网络结构不支持自动适配输入图像尺寸。</summary>
	/// <remarks>
	///   <para><b>含义</b>建模型时想把前处理目标尺寸改成与该模型权重不同的值，但该结构对输入空间尺寸有硬约束（典型如末端接全连接层，特征图展平后的宽度与权重矩阵绑定），换尺寸会让既有权重形状对不上。不是"尺寸填错"，而是"这张图不允许改尺寸"。受限结构的具体清单 [待实测]。</para>
	///   <para><b>排查方向</b>把前处理的图像尺寸保持为该模型训练时的尺寸；确实需要另一尺寸时换用全卷积结构或按新尺寸重训。与 PREPROC（7769）区分：那是前处理该配没配；与 INVALID_SHAPE 区分：那是给的值本身算不通；本码是配什么都改不动。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_NO_IMAGE_RESIZE = 7775;

	/// <summary>CNN 图：aux（辅助输出）索引越界。</summary>
	/// <remarks>
	///   <para><b>含义</b>为某层挑选辅助输出时给出的 aux 序号超出该层实际可用的辅助输出端口数——选到了不存在的第 N 路。端口存在与否由该层类型和图定义决定，与形状、数值无关。</para>
	///   <para><b>排查方向</b>先查该层类型共有几路 aux 输出，再核对所选索引；换 backbone 后层内 aux 路数常会变。与 AUX_SPEC（7777）区分：本码是"选了不存在的那一路"，7777 是"存在的路没接 SelectAux 选择层或未在建模时声明"。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_AUX_INDEX_OOB = 7776;

	/// <summary>CNN 图：含辅助输出的图定义不完整——aux 支路未接 SelectAux 选择层，或建模时未声明任何 aux 输出。</summary>
	/// <remarks>
	///   <para><b>含义</b>某层带辅助输出，但图上没有为这些 aux 输出配上对应的辅助输出选择层（SelectAux）把它们选定；或在创建 DL 模型的调用里压根没指定至少一路 aux 输出，导致含 aux 的图无法定形。原文档点名了这两条成因。</para>
	///   <para><b>排查方向</b>为每个用到 aux 输出的层补 SelectAux 并明确取哪一路；建模参数里显式给出 aux 输出规格。索引选到不存在的端口是 AUX_INDEX_OOB（7776）；图整体走不通（孤立节点、环路）是 DEF（7773）。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_AUX_SPEC = 7777;

	/// <summary>CNN 图：某层不被所选运行时支持。</summary>
	/// <remarks>
	///   <para><b>含义</b>层本身在图里合法，但当前选定的执行后端（运行时类型/版本/设备，如 CPU 与 GPU、不同深度学习后端）没有实现这一层，编译或加载该图时被拒。同一张图换个运行时可能就能跑——这是环境能力差异，不是结构错误。</para>
	///   <para><b>排查方向</b>改选支持该层的运行时，或用该运行时支持的层组合出等价结构（必要时替换算子写法）。与 LAYER_INVALID 区分：那是层规格本身缺失或自相矛盾，换哪个运行时都不行。</para>
	/// </remarks>
	public const int Jl_ERR_CNNGRAPH_LAYER_UNSUPPORTED = 7778;

	/// <summary>DL：深度学习后端内部错误（未归类兜底码）。</summary>
	/// <remarks>
	///   <para><b>含义</b>DL 子系统内部走到了未细化的分支，错误没有映射到本族任何一条具体语义码上。它不代表调用参数一定错了，而是底层库抛出的兜底异常。</para>
	///   <para><b>排查方向</b>用最小可复现步骤确认与具体输入是否相关：换一个已知良好的模型和数据重试，可先排除数据问题；若稳定复现，记录出错的操作序列、模型来源与运行时配置。参数层面能对上号的错误（名称、类型、取值、批数）各有相邻的 DL_* 专码，先按症状对号，对不上再归本码。</para>
	/// </remarks>
	public const int Jl_ERR_DL_INTERNAL = 7779;

	/// <summary>DL：读取模型相关文件失败（文件层/IO 层）。</summary>
	/// <remarks>
	///   <para><b>含义</b>加载 DL 模型、权重或相关数据文件时，在"把文件读进来"这一步就失败：路径不存在、文件被其它进程锁定、权限不足或读取中断。内容尚未进入解析阶段，因此与"文件读得上但解析不了"的 READ_ONNX（7801）不同。</para>
	///   <para><b>排查方向</b>先在系统层面确认文件存在、未被占用、当前账户可读；网络盘与正在解压的临时文件最常触发读取中断。写入方向的对偶码是 FILE_WRITE（7781）。</para>
	/// </remarks>
	public const int Jl_ERR_DL_FILE_READ = 7780;

	/// <summary>DL：写出模型相关文件失败（文件层/IO 层）。</summary>
	/// <remarks>
	///   <para><b>含义</b>保存 DL 模型或其产物文件时，在写文件这一步失败：目标目录不存在、磁盘满、权限不足或目标文件被占用。与 WRITE_ONNX（7803）的分工：那条专指 ONNX 模型导出环节，本码是通用的 DL 文件写失败。</para>
	///   <para><b>排查方向</b>先确认目标目录已建、剩余空间充足、同名文件未被别的进程打开；批量导出脚本常在第二个体量大的模型写出时因磁盘满命中本码。读取方向的对偶码是 FILE_READ（7780）。</para>
	/// </remarks>
	public const int Jl_ERR_DL_FILE_WRITE = 7781;

	/// <summary>DL：模型文件版本与当前库不匹配。</summary>
	/// <remarks>
	///   <para><b>含义</b>文件能完整读出，但其版本标识表明它由更旧或更新的格式写出，当前版本无法按该格式安全解析。属于格式代际问题，与文件损坏（多表现为 READ 或 ONNX 解析失败）、依赖库加载失败（ONNX_LOADER，7804）都不同。</para>
	///   <para><b>排查方向</b>确认模型文件的产出工具链版本与本运行时是否同一代；跨版本部署时优先用中间格式重新导出一份，而不是硬喂旧文件。读取与解析层的对偶码分别是 FILE_READ（7780）与 READ_ONNX（7801）。</para>
	/// </remarks>
	public const int Jl_ERR_DL_FILE_WRONG_VERSION = 7782;

	/// <summary>DL：输入字典缺少模型必需的命名输入。</summary>
	/// <remarks>
	///   <para><b>含义</b>DL 调用以"名字→数据"的输入字典收参数，本次给的键没有覆盖模型声明的全部必需输入（如多输入网络只喂了图像那一路，漏了掩码或辅助输入）。是少给了，不是格式给错。</para>
	///   <para><b>排查方向</b>先取模型的输入名清单，逐一比对字典的键；从 ONNX 导入的模型尤要注意导出端改名后输入名对不上。给了但批数不对是 INPUT_WRONG_BS（7784），元组路数不对是 INPUT_WRONG_LENGTH（7789），键名合法但该类型没这个参数是 PARAM_NOT_AVAILABLE（7788）。</para>
	/// </remarks>
	public const int Jl_ERR_DL_INPUTS_MISSING = 7783;

	/// <summary>DL：输入的路数与批大小（batch size）不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型按固定批大小收输入，本次提供的输入数量不等于该 batch size——多退少补都不行，批维不参与广播。与 INPUT_WRONG_LENGTH（7789）区分：那条批评"打包成元组的多个输入"个数与声明不符，本码专指批维数量。</para>
	///   <para><b>排查方向</b>核对建图时设定的 batch size，把数据凑齐或拆分成整批再调用；若模型启用了批乘子，应参照的口径是 batch_size × batch_size_multiplier，那种情形报的是 7800 而非本码。</para>
	/// </remarks>
	public const int Jl_ERR_DL_INPUT_WRONG_BS = 7784;

	/// <summary>DL：层名称非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>建图或按名引用层时给出的层名不被接受：名称为空或含非法字符，不满足本库对层命名的字符约束（具体禁则 [待实测]）。是"这个名字不成其为名字"，与"名字合法但图里没这层"是两回事。</para>
	///   <para><b>排查方向</b>改用简短稳定的命名（字母数字下划线一类），不要把描述性长句当层名。同名冲突走 DUPLICATE_NAME（7786）；参数名层面"该类型没有这个参数"是 PARAM_NOT_AVAILABLE（7788）。</para>
	/// </remarks>
	public const int Jl_ERR_DL_INVALID_NAME = 7785;

	/// <summary>DL：层名称重复。</summary>
	/// <remarks>
	///   <para><b>含义</b>同一张 DL 计算图里出现了两个同名节点。图内部以名字索引层，重名会让按名取层、连接、设参产生歧义，故在定义期即拒绝。与 INVALID_NAME（7785）区分：那条是名字本身不合法，本码是名字都合法但撞了。</para>
	///   <para><b>排查方向</b>循环建层时别用固定字符串当名字；从外部模型批量导入层时先查重再改名。若发现同一层被添加两次，应修的是添加逻辑而不是急着改名。</para>
	/// </remarks>
	public const int Jl_ERR_DL_DUPLICATE_NAME = 7786;

	/// <summary>DL：指定的输出层非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>把某层声明为模型输出时该声明不被接受：名字不存在、指向的层不能作为输出、或与模型类型要求的输出定义方式不符（中间层可否作为输出的边界 [待实测]）。</para>
	///   <para><b>排查方向</b>先列出图内全部层名，再逐个核对输出声明；想在推理时取中间层激活，先确认接口是否支持"任意层当输出"这一用法。输出的"个数"不对走 WRONG_OUTPUT_LAYER_NUM（7798），别混用。</para>
	/// </remarks>
	public const int Jl_ERR_DL_INVALID_OUTPUT = 7787;

	/// <summary>DL：该类型不存在所请求的参数。</summary>
	/// <remarks>
	///   <para><b>含义</b>按名读或写某个通用参数，但当前节点（或模型、求解器）类型的参数表里没有这一项——参数名拼错、把 A 层型的参数配到 B 层上、或该参数只在别的版本里有。是"查无此名"，不是"值不合法"。</para>
	///   <para><b>排查方向</b>先枚举该类型支持的参数清单再设值；换层类型或升级版本后旧参数名常失效。"名对值错"的分工可参照检测器族 INVALID_PARAM 与 INVALID_PARAM_VALUE（7821/7822）；建节点时该给却没给 name 是 NODE_MISSING_PARAM_NAME（7830）。</para>
	/// </remarks>
	public const int Jl_ERR_DL_PARAM_NOT_AVAILABLE = 7788;

	/// <summary>DL：元组输入的个数（长度）不对。</summary>
	/// <remarks>
	///   <para><b>含义</b>接口以元组收多路同类输入，传入元组的元素数与该调用或该模型声明所需数不等——少一路或多一路都会命中。长度对了但元素类型不对是 7790，类型对了但值非法是 7791。</para>
	///   <para><b>排查方向</b>打印入参元组长度与模型声明比对；循环拼批时最容易多塞或漏一路。单路输入与批大小不符另见 INPUT_WRONG_BS（7784）。</para>
	/// </remarks>
	public const int Jl_ERR_DL_INPUT_WRONG_LENGTH = 7789;

	/// <summary>DL：元组输入的元素类型不对。</summary>
	/// <remarks>
	///   <para><b>含义</b>元组长度符合，但其中元素类型与要求不符——数值里混进字符串、该传句柄的位置传了数字等（哪些位置容忍隐式转换 [待实测]）。属于"装错了车厢"，与少给路数（7789）分属两查。</para>
	///   <para><b>排查方向</b>逐元素核对类型再组装元组；从文件或外部接口拼出来的元组常整列是字符串，需要显式数值化。类型对了但值超出允许范围才轮到 INPUT_WRONG_VALUES（7791）。</para>
	/// </remarks>
	public const int Jl_ERR_DL_INPUT_WRONG_TYPE = 7790;

	/// <summary>DL：部分输入给出的数值不合法。</summary>
	/// <remarks>
	///   <para><b>含义</b>数量与类型都已就位，但某些具体取值落在允许域之外（负的尺寸、空的名字、越界的索引这类），被逐项校验拦下。本码不指明是哪一路，需要按调用参数序自查。</para>
	///   <para><b>排查方向</b>对照接口说明里每项的取值域逐条比对；批量数据先在本地做最小值/最大值扫描再提交。命名类错误另有专码（层名 7785、参数名 7788），别拿本码去套。</para>
	/// </remarks>
	public const int Jl_ERR_DL_INPUT_WRONG_VALUES = 7791;

	/// <summary>DL：类别 id 有重复。</summary>
	/// <remarks>
	///   <para><b>含义</b>提供的类别 id 序列里出现相同值。id 是类别与输出通道/标签之间的映射键，重复会让多个类争同一个位置，统计与反查都会静默错位，故要求在集合内唯一。</para>
	///   <para><b>排查方向</b>对 id 序列去重，并核对"从标注文件聚合类别"的脚本是否把同义类并了两次。id 合法但撞进忽略类集合是 OVERLAP（7797）；模型压根没登记 id 是 CLASS_IDS_MISSING（7802）。</para>
	/// </remarks>
	public const int Jl_ERR_DL_CLASS_IDS_NOT_UNIQUE = 7792;

	/// <summary>DL：类别 id 含非法值。</summary>
	/// <remarks>
	///   <para><b>含义</b>id 集合本身不满足约束：出现保留值（背景/忽略类占用的 id）、负数或其它不被接受的取值（合法区间与保留值清单 [待实测]）。与 7792 区分：那条查重复，本条查取值本身合不合规。</para>
	///   <para><b>排查方向</b>按接口文档的保留值与合法区间过滤一遍再提交；从别的框架迁移来的数据集常用 -1 表示背景，需先映射成本库口径。</para>
	/// </remarks>
	public const int Jl_ERR_DL_CLASS_IDS_INVALID = 7793;

	/// <summary>DL：类别 id 换算环节的输入数据非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>在外部类别 id 与模型内部类索引之间做映射换算时，喂给换算的数据不合口径：出现映射表里没有的 id、待换算输入为空、或维度与换算要求不符（具体口径 [待实测]）。错在"换算的输入"，不是 id 集合定义本身——那是 7792/7793。</para>
	///   <para><b>排查方向</b>核对换算两端的清单是否同源：训练配一套 id、推理喂另一套最易触发；先把两端排序后 diff 一遍再定位缺项。</para>
	/// </remarks>
	public const int Jl_ERR_DL_CLASS_IDS_INVALID_CONV = 7794;

	/// <summary>DL：类型重复定义。</summary>
	/// <remarks>
	///   <para><b>含义</b>试图注册（定义）一个已经存在的 DL 类型名——同一上下文里二次声明同名类型。类型名是全局查找键，允许覆盖会让先前按名建立的引用静默换指向，故直接拒绝。</para>
	///   <para><b>排查方向</b>注册前先查是否已定义，或直接复用既有定义；反复加载模型/配置的脚本最容易在第二次初始化时撞上。与 DUPLICATE_NAME（7786）区分：那是图内层名撞名，本码是类型注册撞名。</para>
	/// </remarks>
	public const int Jl_ERR_DL_TYPE_ALREADY_DEFINED = 7795;

	/// <summary>DL：识别不出可用于推理的输入。</summary>
	/// <remarks>
	///   <para><b>含义</b>把模型用于推理时，图里找不到任何可作为推理输入的节点：输入全被声明成仅训练用（如监督支路的标签输入），或输入节点在改结构时被全部接走。模型"没有进料口"，推理自然起不来。</para>
	///   <para><b>排查方向</b>检查建图时各输入的用途标记是否被误设为训练专用；从训练图导推理模型时应剥掉监督支路、保留图像入口。与 INPUTS_MISSING（7783）区分：那是调用时字典少给，本码是模型侧根本没有可给的位置。</para>
	/// </remarks>
	public const int Jl_ERR_DL_NO_INFERENCE_INPUTS = 7796;

	/// <summary>DL：类别 id 与忽略类 id 集合有交集。</summary>
	/// <remarks>
	///   <para><b>含义</b>同一批 id 既被声明为正常训练类别，又被列进忽略类（不参与损失/统计的类）集合。两边语义互斥：一个类不可能既要被学习又要被无视，故按集合求交拦下。</para>
	///   <para><b>排查方向</b>把忽略清单从类别清单里做集合差再提交；常见诱因是复制粘贴配置时两套清单同源没删干净。单侧集合自身的错误（重复 7792、非法 7793）先于本码被拦。</para>
	/// </remarks>
	public const int Jl_ERR_DL_CLASS_IDS_INVALID_OVERLAP = 7797;

	/// <summary>DL：输出层的数量与要求不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型类型对输出层数量有固定要求（分类一路、某些检测/分割头多路），实际声明的层数多了或少了。是"个数"错误，与某一路输出的名字或可输出性不对（INVALID_OUTPUT，7787）分属两查。</para>
	///   <para><b>排查方向</b>按模型类型核对应有几路输出，再数一遍声明；改头结构（去掉辅助头、合并支路）后最容易少报或多报一路。</para>
	/// </remarks>
	public const int Jl_ERR_DL_WRONG_OUTPUT_LAYER_NUM = 7798;

	/// <summary>DL：批大小乘子（batch size multiplier）必须大于 0。</summary>
	/// <remarks>
	///   <para><b>含义</b>设置 batch_size_multiplier 时给了 0 或负数。该乘子与实际批数相乘（所需输入数 = batch_size × 乘子），取非正值会让需求批数失去意义，故在参数校验期即拒。</para>
	///   <para><b>排查方向</b>用正整数；不确定该乘几时先设 1 跑通再调。乘子合法但输入路数没按乘积凑齐，报的是 7800 而不是本码。</para>
	/// </remarks>
	public const int Jl_ERR_DL_WRONG_BS_MULTIPLIER = 7799;

	/// <summary>DL：按 batch_size × 乘子核对，输入路数不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>启用批乘子后，模型要求的输入总数为 batch_size × batch_size_multiplier（原文档明确了这一口径），本次给的路数与该乘积不等。相比 INPUT_WRONG_BS（7784）只批评 batch_size 本身，本码专指乘子参与后的口径。</para>
	///   <para><b>排查方向</b>先手算乘积，再数实给的输入；扩批常用于一次喂多幅，注意乘子是"倍数"不是"总数"。乘子自身非法另见 7799。</para>
	/// </remarks>
	public const int Jl_ERR_DL_INPUT_WRONG_BS_WITH_MULTIPLIER = 7800;

	/// <summary>DL：读取（解析）ONNX 模型出错。</summary>
	/// <remarks>
	///   <para><b>含义</b>ONNX 文件已能打开，但解析其内容时失败：含本库不支持的算子或 opset 版本、图结构非法、外部权重文件缺失等（支持的版本线边界 [待实测]）。属于"读得上、看不懂"，与连文件都读不上来的 FILE_READ（7780）区分。</para>
	///   <para><b>排查方向</b>用 ONNX 查看工具核对 opset 与算子清单，必要时换低一档 opset 重新导出；导出端尽量固定单一框架版本。写出方向对偶码是 WRITE_ONNX（7803），依赖库本身加载失败是 ONNX_LOADER（7804）。</para>
	/// </remarks>
	public const int Jl_ERR_DL_READ_ONNX = 7801;

	/// <summary>DL：模型没有登记类别 id。</summary>
	/// <remarks>
	///   <para><b>含义</b>调用需要"类别→id 映射"的操作（按类取结果、配置忽略类等）时，该模型创建时未提供 class ids，映射表为空。不是 id 有错，而是压根没有。</para>
	///   <para><b>排查方向</b>在建模阶段把类别清单作为参数给出；对已存的模型文件考虑重建模型而不是运行期硬补。id 给了但重复/非法/撞忽略类分别见 7792、7793、7797。</para>
	/// </remarks>
	public const int Jl_ERR_DL_CLASS_IDS_MISSING = 7802;

	/// <summary>DL：写出（导出）ONNX 模型出错。</summary>
	/// <remarks>
	///   <para><b>含义</b>导出 ONNX 的过程中失败：图里含无法用 ONNX 表达的层、序列化中断，或导出目标不可写（可导出层的边界 [待实测]）。与 FILE_WRITE（7781）的分工：本码专指 ONNX 导出环节，那条是通用文件写失败。</para>
	///   <para><b>排查方向</b>先确认结构在可导出范围内（自定义层最易挡路），再排查磁盘与路径；导出后用 READ_ONNX 方向做一次回读自检最稳。对偶读码是 7801。</para>
	/// </remarks>
	public const int Jl_ERR_DL_WRITE_ONNX = 7803;

	/// <summary>DL：加载 ONNX 所需的 protobuf 支撑库失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>ONNX 读写依赖的动态库（模板原文点名 libprotobuf）在进程里加载不起来：部署缺文件、位数不匹配（x64/x86 混装）、运行库版本冲突。解析器根本没立起来，因此还没碰到模型内容就失败了。</para>
	///   <para><b>排查方向</b>核对发布包完整性、主程序与依赖库位数一致；可与同机能正常读写 ONNX 的部署对比目录。模型文件本身解析不通是 READ_ONNX（7801），别拿本码去排查模型。</para>
	/// </remarks>
	public const int Jl_ERR_DL_ONNX_LOADER = 7804;

	/// <summary>DL-FPN：构建特征金字塔时给出的缩放（scales）不合法。</summary>
	/// <remarks>
	///   <para><b>含义</b>创建 FPN 时指定的尺度序列与网络要求不符：个数与金字塔层数配不上、含非正或重复的缩放值等（校验细则 [待实测]）。</para>
	///   <para><b>排查方向</b>让 scales 的长度与所选层数逐一对应，取值单调且为正；从预训练配置复制 scales 时注意 backbone 换型后级数常会变。主干与级数层面的相邻码分别是 INVALID_BACKBONE（7811）与 INVALID_LEVELS（7813）。</para>
	/// </remarks>
	public const int Jl_ERR_DL_FPN_SCALES = 7810;

	/// <summary>DL-FPN：所选主干（backbone）不能用于构建 FPN。</summary>
	/// <remarks>
	///   <para><b>含义</b>FPN 需要从主干的多个不同分辨率阶段各抽一张特征图再做融合，而所选 backbone 不提供这些可抽头（没有阶段划分输出、或阶段数少于 FPN 的最低要求），金字塔搭不起来。</para>
	///   <para><b>排查方向</b>换成带多级输出的主干网络；自定义 backbone 需按接口要求暴露各阶段特征。主干能用但特征图尺寸不合格是 INVALID_FEATURE_MAP_SIZE（7812），级别给错是 7813。</para>
	/// </remarks>
	public const int Jl_ERR_DL_FPN_INVALID_BACKBONE = 7811;

	/// <summary>DL-FPN：主干特征图尺寸不能被 2 整除。</summary>
	/// <remarks>
	///   <para><b>含义</b>FPN 的金字塔逐级做 2 倍缩放，要求各抽取层的特征图边长能被 2 整除以保证融合时网格对齐；当前输入尺寸推导出的特征图出现奇数边长，一半算不齐，层间对不上。</para>
	///   <para><b>排查方向</b>把前处理的输入尺寸改成能被总下采样倍数整除的值（如 32/64 的倍数）再建图。这是输入尺寸选择问题，与主干本身不可用的 INVALID_BACKBONE（7811）区分。</para>
	/// </remarks>
	public const int Jl_ERR_DL_FPN_INVALID_FEATURE_MAP_SIZE = 7812;

	/// <summary>DL-FPN：给定的金字塔级别（levels）非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>请求的 FPN 级别序号或数量超出该 backbone 可提供的抽取层范围，或级别集合本身不满足要求（空、重复、越界）。</para>
	///   <para><b>排查方向</b>先列出 backbone 各阶段再选级别；换 backbone 型号后原级别序号未必仍然有效。scales 数量与层数配不上是 7810，本码只评级别选取。</para>
	/// </remarks>
	public const int Jl_ERR_DL_FPN_INVALID_LEVELS = 7813;

	/// <summary>DL：使用 anchor（锚框）时发生内部错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>检测模型生成或使用锚框的过程中后端内部失败：常见诱因是 anchor 尺寸/比例与特征图步长严重不匹配导致计算退化（退化判定细节 [待实测]）。</para>
	///   <para><b>排查方向</b>核对 anchor 配置与金字塔各级步长的对应关系；用一组已知良好的 anchor 配置做替换试验定位。检测器参数层面的专码是 7821/7822，能对上号的先归它们。</para>
	/// </remarks>
	public const int Jl_ERR_DL_ANCHOR = 7820;

	/// <summary>DL-检测器：参数名非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>为检测器模型设置或读取参数时，给出的参数名不在该检测器类型的参数表里——拼写错误、版本间改名、或混用了别的模型族的参数名。是"查无此名"。</para>
	///   <para><b>排查方向</b>先枚举该检测器支持的参数名再设置。与 7822 的分工：本码管名不对，那条管名对但取值不合法。</para>
	/// </remarks>
	public const int Jl_ERR_DL_DETECTOR_INVALID_PARAM = 7821;

	/// <summary>DL-检测器：参数取值非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>参数名存在，但给的值落在其允许域之外（越界的阈值、非正的尺度、空串等；各参数取值域 [待实测]）。</para>
	///   <para><b>排查方向</b>按参数逐项核对取值区间；从旧配置迁移时重点检查改了量程的项。名字本身不存在应报 7821，别在本码上绕。</para>
	/// </remarks>
	public const int Jl_ERR_DL_DETECTOR_INVALID_PARAM_VALUE = 7822;

	/// <summary>DL-检测器：指定的对接（docking）层非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>把检测头挂接到主干时，所选对接层在该图中不存在、不是可挂接的候选层、或与检测器要求的挂接位置不匹配（可挂接条件 [待实测]）。</para>
	///   <para><b>排查方向</b>列出主干各阶段输出，按检测器文档要求的接口位置选层；换 backbone 型号后原层名常已失效，需重新对照。</para>
	/// </remarks>
	public const int Jl_ERR_DL_DETECTOR_INVALID_DOCKING_LAYER = 7823;

	/// <summary>DL-检测器：实例类型非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>传给检测器接口的对象实例不是该操作要求的类型——如把分类模型、预处理对象或已释放的句柄当检测器实例使用。</para>
	///   <para><b>排查方向</b>调用前确认实例由检测器创建接口产出且仍有效；多模型并存的脚本里最容易张冠李戴，可先按类型逐句核对变量来源。</para>
	/// </remarks>
	public const int Jl_ERR_DL_DETECTOR_INVALID_INSTANCE_TYPE = 7824;

	/// <summary>DL-Node：创建节点缺通用参数 'name'。</summary>
	/// <remarks>
	///   <para><b>含义</b>为某个 DL 节点提供通用参数时漏掉了必给的 'name'：该节点类型要求显式命名，图内部要靠它索引此节点（原文档明确提示"请指定层名"），缺失即返回本码。</para>
	///   <para><b>排查方向</b>在该节点的通用参数里补上非空 'name'，命名合法性见 DL_INVALID_NAME（7785）。与相邻码区分：GENPARAM_NAME_NOT_ALLOWED（7831）是相反的"不该给 name 却给了"方向，勿混用。</para>
	/// </remarks>
	public const int Jl_ERR_DL_NODE_MISSING_PARAM_NAME = 7830;

	/// <summary>DL-Node：该节点不允许设置通用参数 'name'。</summary>
	/// <remarks>
	///   <para><b>含义</b>为某个 DL 节点显式提供了通用参数 'name'，但该节点类型不允许用户自定义名称（其名称由图自动生成或固定），因此返回本码。是"多余参数"方向的错误。</para>
	///   <para><b>排查方向</b>去掉该节点的 'name' 通用参数。与相邻码区分：NODE_MISSING_PARAM_NAME（7830）是相反的"缺 name"方向，勿混用。</para>
	/// </remarks>
	public const int Jl_ERR_DL_NODE_GENPARAM_NAME_NOT_ALLOWED = 7831;

	/// <summary>DL-Node：节点（层）定义规格非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>DL 计算图里某个节点（层）的规格描述不完整或不符合该节点类型的约束（参数缺失、类型不符、连接端口非法等），返回本码。属于节点层面的通用规格校验失败。</para>
	///   <para><b>排查方向</b>对照该节点类型要求的通用参数与端口定义逐项核对。与相邻码区分：NODE_MISSING_PARAM_NAME（7830）专指缺 name，GENPARAM_NAME_NOT_ALLOWED（7831）指不该给 name 却给了，本码是更笼统的规格非法。</para>
	/// </remarks>
	public const int Jl_ERR_DL_NODE_INVALID_SPEC = 7832;

	/// <summary>DL-Node：两个层之间只允许存在一条直连边，检测到重复连接。</summary>
	/// <remarks>
	///   <para><b>含义</b>在 DL 计算图里为两个节点（层）建立连接时，同一对源层与目标层之间被重复添加了直连边；本库规定两层之间至多一条直接连接，故返回本码。</para>
	///   <para><b>排查方向</b>检查图构建代码是否对相同的 from/to 层重复调用连接；如需多条数据流，应经由中间层而非重复同一条边。与相邻码区分：NODE_INVALID_SPEC（7832）指节点规格非法，本码专指边的重复。</para>
	/// </remarks>
	public const int Jl_ERR_DL_NODE_DUPLICATE_EDGE = 7833;

	/// <summary>DL-Solver：求解器（solver）类型非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>创建或配置 DL 训练求解器时，指定的求解器类型不被识别或不受支持（例如并非本库支持的优化器种类），返回本码。属于求解器"种类"层面的错误，早于对具体超参数或更新公式的校验。</para>
	///   <para><b>排查方向</b>确认所用的 solver type 名称在本库支持列表内。与相邻码区分：SOLVER_INVALID_UPDATE_FORMULA（7841）指类型对了但更新公式不对。</para>
	/// </remarks>
	public const int Jl_ERR_DL_SOLVER_INVALID_TYPE = 7840;

	/// <summary>DL-Solver：优化器（solver）指定的参数更新公式非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>创建或配置 DL 训练求解器（solver）时，所选的参数更新公式（如 SGD / Momentum / Adam 等更新规则）不被支持或与求解器类型不匹配，返回本码。属于求解器配置阶段的参数取值错误，不涉及具体超参数数值。</para>
	///   <para><b>排查方向</b>核对 DL 求解器参数集中"更新公式 / 更新规则"项的取值是否与所选求解器类型匹配。与相邻码区分：SOLVER_INVALID_TYPE（7840）指求解器类型本身非法，本码专指更新公式这一项。</para>
	/// </remarks>
	public const int Jl_ERR_DL_SOLVER_INVALID_UPDATE_FORMULA = 7841;

	/// <summary>DL：所选运行时（runtime）不支持热力图（heatmap）计算。</summary>
	/// <remarks>
	///   <para><b>含义</b>请求 DL 模型的热力图输出时，当前所选推理运行时不具备该能力（例如某些运行时未实现激活图反传/上采样），返回本码。这是运行环境层面的限制，与模型类型无关。</para>
	///   <para><b>排查方向</b>改换支持 heatmap 的运行时（如对应深度学习后端配置）后重试。与相邻码区分：UNSUPPORTED_MODEL_TYPE（7851）指模型类型不对，UNSUPPORTED_METHOD（7852）指热力图算法选项不对，本码专指运行时不支持。</para>
	/// </remarks>
	public const int Jl_ERR_DL_HEATMAP_UNSUPPORTED_RUNTIME = 7850;

	/// <summary>DL：所选模型类型不支持热力图（heatmap），热力图仅适用于 'classification' 类型模型。</summary>
	/// <remarks>
	///   <para><b>含义</b>在使用 DL 热力图（class activation / 激活图）相关算子时，被查询的 DL 模型类型不是分类模型，热力图功能无处计算，返回本码。当前仅分类（classification）模型定义了逐类空间激活图，检测/分割类模型不适用。</para>
	///   <para><b>排查方向</b>确认所用模型确为分类模型；若要为其它任务取可视化，应改用该任务原生的输出（如分割 logits），而非 heatmap 接口。与相邻码区分：HEATMAP_UNSUPPORTED_RUNTIME（7850）指运行时不支持，本码指模型类型不支持。</para>
	/// </remarks>
	public const int Jl_ERR_DL_HEATMAP_UNSUPPORTED_MODEL_TYPE = 7851;

	/// <summary>DL-热力图：所请求的热力图方法（类激活图计算方案）不受支持。</summary>
	/// <remarks>
	///   <para><b>含义</b>为 DL 模型生成热力图时，指定 method 参数所要求的激活图计算方案在当前版本/配置下不被提供（可支持的方法清单 [待实测]）。属于"方法选项挑错"，发生在运行时与模型类型都已通过之后。</para>
	///   <para><b>排查方向</b>改用默认或明确受支持的方法重试。与相邻码区分：UNSUPPORTED_RUNTIME（7850）是运行时不支持热力图，UNSUPPORTED_MODEL_TYPE（7851）是模型类型不是分类模型，WRONG_TARGET_CLASS_ID（7853）是目标类号越界，本码专指方法选项本身。</para>
	/// </remarks>
	public const int Jl_ERR_DL_HEATMAP_UNSUPPORTED_METHOD = 7852;

	/// <summary>DL-热力图：指定的目标类别 id 不合法。</summary>
	/// <remarks>
	///   <para><b>含义</b>热力图是针对某个目标类逐类生成的，给定的 class id 超出了模型分类头的类范围（通常为 0 到类数-1 的有效索引；是否允许特殊值表示"全类"等 [待实测]）。多因类名表与模型实际类数不匹配时按序号硬编码所致。</para>
	///   <para><b>排查方向</b>先查询模型的类数或读取其类名列表，再传入对应的类 id。与相邻码区分：UNSUPPORTED_METHOD（7852）是方法选项不支持，本码是方法没问题、类号越界。</para>
	/// </remarks>
	public const int Jl_ERR_DL_HEATMAP_WRONG_TARGET_CLASS_ID = 7853;

	/// <summary>DL-GCAD：G-CAD（全局上下文异常检测）模型所依赖的底层网络不可用。</summary>
	/// <remarks>
	///   <para><b>含义</b>G-CAD 异常检测模型运行时要依赖内置的特征提取网络；该网络无法装载或初始化（部署资源缺失、后端配置不满足）时返回本码。属于"底层装备缺失"层面的错误，发生在检查模型实例自身状态之前。</para>
	///   <para><b>排查方向</b>检查运行环境与部署完整性，换一台已完整安装的机器对照 [待实测：本码探测对象的确切定义]。与相邻码区分：ANOMALY_MODEL_INTERNAL（7880）是异常模型内部失败，UNTRAINED（7881）是模型未训练，本码专指 GCAD 依赖的网络本身不可用。</para>
	/// </remarks>
	public const int Jl_ERR_DL_GCAD_NETWORK_NOT_AVAILABLE = 7870;

	/// <summary>DL-异常检测：异常模型内部错误（兜底码）。</summary>
	/// <remarks>
	///   <para><b>含义</b>异常检测（anomaly）模型在创建、训练或应用过程中后端内部失败，且落不进本族 7881～7886 任何一个具体码。属总括性错误码，单独看信息量有限。</para>
	///   <para><b>排查方向</b>先按 7881（未训练）、7882（训练失败）、7883（训后设参）、7884（尺寸）、7885（通道）、7886（空域）逐项排除具体成因；仍复现则记录触发动作与输入请求后端定位 [待实测：能否取到原生错误详情]。</para>
	/// </remarks>
	public const int Jl_ERR_DL_ANOMALY_MODEL_INTERNAL = 7880;

	/// <summary>DL-异常检测：模型尚未训练，却被当作可用模型使用。</summary>
	/// <remarks>
	///   <para><b>含义</b>刚创建（或训练未成功完成）的异常模型还没有可用于异常判定的内部统计量，此时做推理、评估或部分参数读取即返回本码。使用前提：训练步骤必须已成功执行过。</para>
	///   <para><b>排查方向</b>检查脚本里训练是否真的被调用、训练返回值是否被检查过——训练段被条件分支静默跳过是最常见成因。与相邻码区分：TRAINING_FAILED（7882）是训练跑了但失败，本码是训练没有有效完成过。</para>
	/// </remarks>
	public const int Jl_ERR_DL_ANOMALY_MODEL_UNTRAINED = 7881;

	/// <summary>DL-异常检测：模型训练失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>训练流程被启动但没能正常完成（常见诱因：训练图像集为空或域为空、资源不足、超参不当；本码不区分失败的具体环节 [待实测]）。中途失败与一开始就起不来都归本码。</para>
	///   <para><b>排查方向</b>用缩小到几条样本的数据集复跑以定位；顺带核查训练域（对应 7886）与尺寸/通道前提（7884/7885）。本码的典型下游后果：拿着这个半成品模型去用会撞上 UNTRAINED（7881）。</para>
	/// </remarks>
	public const int Jl_ERR_DL_ANOMALY_MODEL_TRAINING_FAILED = 7882;

	/// <summary>DL-异常检测：模型已训练完成后还想设置被锁定的参数。</summary>
	/// <remarks>
	///   <para><b>含义</b>异常检测模型中与训练结果绑定的参数（决定模型结构或内部统计的参数）在训练完成后被锁定，设置这一类参数即返回本码。注意不是"所有参数都改不动"，只有训练前定型的参数被锁（锁定参数清单 [待实测]）。</para>
	///   <para><b>排查方向</b>把该参数的设置挪到训练之前，然后重新训练；训后只调允许范围类的参数。与相邻码区分：RESIZE（7884）专指输入尺寸这一项被锁，本码是泛化的"训后不可设"。</para>
	/// </remarks>
	public const int Jl_ERR_DL_ANOMALY_MODEL_PARAM_TRAINED = 7883;

	/// <summary>DL-异常检测：模型的输入图像尺寸不可更改。</summary>
	/// <remarks>
	///   <para><b>含义</b>异常模型的输入尺寸在训练（或模型定义）时已定型，事后设置或缩放输入尺寸一律被拒——主干网络的特征网格与统计量都是按该尺寸算的，改尺寸等于地基失效（定型时机 [待实测]）。</para>
	///   <para><b>排查方向</b>需要别的尺寸时只能用新尺寸重训；对输入图像本身，应在调用侧先把图像 resize 到模型要求的尺寸。本码可视为 PARAM_TRAINED（7883）在"尺寸"这一项上的具体化。</para>
	/// </remarks>
	public const int Jl_ERR_DL_ANOMALY_MODEL_RESIZE = 7884;

	/// <summary>DL-异常检测：输入图像的通道数不被模型支持。</summary>
	/// <remarks>
	///   <para><b>含义</b>depth 指图像通道数。传入异常模型的图像通道数与模型要求不符即返回本码（此类模型常按 3 通道彩色输入组织；支持哪些通道数 [待实测]）。单通道灰度图喂给彩色模型是最常见触发方式。</para>
	///   <para><b>排查方向</b>调用前核对图像通道数并按模型要求转换（灰度转 3 通道等），别指望后端替你补通道。与相邻码区分：RESIZE（7884）管尺寸维度，本码管通道维度。</para>
	/// </remarks>
	public const int Jl_ERR_DL_ANOMALY_MODEL_DEPTH = 7885;

	/// <summary>DL-异常检测：给定的输入域（domain，有效区域）为空。</summary>
	/// <remarks>
	///   <para><b>含义</b>训练或应用异常模型时可指定 domain 圈定参与计算的像素区域；给出空区域时没有任何可学习/可判定的像素，参数校验直接拒绝。完全不传 domain 是否按全域处理 [待实测]。</para>
	///   <para><b>排查方向</b>传入前确认区域非空——上游阈值、求交、形状筛选把区域清空是常见诱因，且这种空集往往静默产生。与相邻码区分：UNTRAINED（7881）管训练状态，本码管输入参数本身。</para>
	/// </remarks>
	public const int Jl_ERR_DL_ANOMALY_MODEL_INPUT_DOMAIN = 7886;

	/// <summary>DeepOCR：模型内部错误（本族兜底码）。</summary>
	/// <remarks>
	///   <para><b>含义</b>DeepOCR 模型在创建、装载、训练或推理过程中后端内部失败，且与字母表/映射/文件等具体成因都对不上（那些各有专码 7891～7901）。单独看信息量有限，须结合当时执行的动作判断。</para>
	///   <para><b>排查方向</b>先按本族专码逐项排除（字母表合法性 7891/7901、索引与映射 7892/7895～7897、模型类型与可用性 7893/7894、文件 7898、字符与词长 7899/7900）；仍无法定位则记录调用序列与模型来源文件 [待实测：能否取到原生错误详情]。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_OCR_MODEL_INTERNAL = 7890;

	/// <summary>DeepOCR：字母表条目不是单字符——每个条目只能是一个字符。</summary>
	/// <remarks>
	///   <para><b>含义</b>DeepOCR 的字母表是识别输出的候选字符集，每个元素必须是长度恰为 1 的字符串；出现多字符串、空串都会触发本码。单字符约束是"按索引定位字符"这套映射机制的地基。</para>
	///   <para><b>排查方向</b>传入前逐元素检查长度；由文本文件/数组拼接生成字母表时重点防行分隔符、BOM、尾部空白混入——它们会让"看起来一个字符"的条目超长。与相邻码区分：ALPHABET_NOT_UNIQUE（7901）管重复，本码管长度。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_ALPHABET = 7891;

	/// <summary>DeepOCR：按索引访问字母表时越界。</summary>
	/// <remarks>
	///   <para><b>含义</b>以数字索引读写字母表条目（取某索引处的字符等）时，索引落在 0 到字母表长度-1 之外 [待实测：是否存在保留索引]。常见于字母表被换过一轮、调用方仍拿旧序号访问。</para>
	///   <para><b>排查方向</b>用索引前先查当前字母表长度，序号与字母表必须同源更新。与相邻码区分：MAPPING_IDX（7896）是映射数组里的索引指向外部字母表越界，本码是对内部字母表本身的访问越界。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_ALPHABET_IDX = 7892;

	/// <summary>DeepOCR：传入的 DL 模型类型不在允许清单内。</summary>
	/// <remarks>
	///   <para><b>含义</b>DeepOCR 模型要挂接一个 DL 模型作为特征提取主干；该 DL 模型的类型（架构/任务种类）不在 DeepOCR 支持的清单里即返回本码（允许哪些类型 [待实测]）。</para>
	///   <para><b>排查方向</b>换用 DeepOCR 明确支持的 DL 模型类型重新组装；不要把其它任务定型的现成模型硬塞进来。与相邻码区分：NOT_AVAILABLE（7894）是模型实例本身无效，本码是实例有效但类型不被接受。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_MODEL_TYPE = 7893;

	/// <summary>DeepOCR：模型不可用（尚未就绪或已失效）。</summary>
	/// <remarks>
	///   <para><b>含义</b>对 DeepOCR 模型执行操作时，其实例没有持有可用的内部资源：创建/装载失败后未判错继续用、句柄已被释放、或还没装载。是"对象壳子在、模型本体不在"的状态。</para>
	///   <para><b>排查方向</b>逐步检查创建与装载环节的返回值（文件找不到会先报 7898），只用成功装载后的实例；避免跨作用域持有已 Dispose 的句柄。与相邻码区分：INTERNAL（7890）是有效模型运行中失败，本码是模型压根没准备好。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_OCR_MODEL_NOT_AVAILABLE = 7894;

	/// <summary>DeepOCR：要求设置字母表映射，但模型没有内部字母表可映射。</summary>
	/// <remarks>
	///   <para><b>含义</b>字母表映射的语义是"把模型内部字母表逐位换绑成外部字符"；若模型根本没有定义内部字母表（没有字符集的模型无从作映射起点），给出 alphabet_mapping 参数就直接报本码。属前提性错误，与映射内容对不对无关。</para>
	///   <para><b>排查方向</b>先确认所选模型自带内部字母表（或先走定义字母表的流程再设映射）[待实测：本族是否有独立的字母表定义入口]。与相邻码区分：MAPPING_LEN（7897）管长度不符、MAPPING_IDX（7896）管映射内索引越界，本码是没有字母表可映射。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_ALPHABET_MAPPING_NO_ALPHABET = 7895;

	/// <summary>DeepOCR：映射数组中给出了越界的字母表索引。</summary>
	/// <remarks>
	///   <para><b>含义</b>映射数组的每个元素是一个索引，指向"随映射一并给出的外部字母表"中的字符（内部字母表第 i 位换绑为外部字母表第 mapping[i] 个字符）；任一元素超出外部字母表的 0 到长度-1 范围即报本码 [待实测：负值是否有保留含义]。</para>
	///   <para><b>排查方向</b>生成映射后逐元素校验取值范围；自动构造映射时确认其索引基准与外部字母表的实际长度一致。与相邻码区分：INVALID_ALPHABET_IDX（7892）是对内部字母表访问越界，本码是映射里指向外部字母表的索引越界。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_ALPHABET_MAPPING_IDX = 7896;

	/// <summary>DeepOCR：映射长度与模型内部字母表长度不相等。</summary>
	/// <remarks>
	///   <para><b>含义</b>映射是内部字母表到外部字符的一一对应，因此映射数组长度必须严格等于内部字母表长度；多一位、少一位都报本码——哪怕只差 1 也不会隐式截断或补齐。</para>
	///   <para><b>排查方向</b>先查询模型内部字母表长度，按同一长度生成映射；换版本模型后映射必须与字母表成对更新。本码报出时不必去查字符重复或单字符问题（那是 7901/7891 的事）。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_ALPHABET_MAPPING_LEN = 7897;

	/// <summary>DeepOCR：找不到模型文件。</summary>
	/// <remarks>
	///   <para><b>含义</b>装载 DeepOCR 模型时给定路径上没有文件。常见形态：相对路径按不同的工作目录解析、模型文件未随程序一起部署、保存环节失败后误以为文件已存在。</para>
	///   <para><b>排查方向</b>装载前显式判文件存在，优先用绝对路径；核对部署脚本是否把模型文件列入拷贝清单。[待实测：文件存在但格式/版本非法是否也落本码]。与相邻码区分：NOT_AVAILABLE（7894）是模型实例没就绪，本码是更早的文件级失败。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_OCR_MODEL_FILE_NOT_FOUND = 7898;

	/// <summary>DeepOCR：某个字符不在模型内部字母表中。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型只能产出内部字母表覆盖的字符；训练文本、词表或期望输出里出现表外字符即报本码。典型诱因：大小写被当作不同字符、全角/半角标点混用、用拉丁字母表处理中文文本。</para>
	///   <para><b>排查方向</b>要么先把文本归一到字母表口径（统一大小写、全半角），要么换/扩字母表；是否自动做大小写归一 [待实测]。与相邻码区分：ALPHABET_NOT_UNIQUE（7901）管字母表自身重复，本码管数据侧字符越表。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_OCR_MODEL_UNKNOWN_CHAR = 7899;

	/// <summary>DeepOCR：给出的词长（word length）非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>word length 规定单条识别结果的字符数上限，它绑定模型的输入宽度与输出序列长度，必须落在模型允许的范围 [待实测：允许范围如何确定]。越界即报本码。</para>
	///   <para><b>排查方向</b>把词长降到模型规格以内；确需读更长文本时应在预处理里把行切成多段分别识别，而不是硬调大该值。与相邻码区分：MAPPING_LEN（7897）管映射数组长度，本码管词长参数。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_OCR_MODEL_INVALID_WORD_LENGTH = 7900;

	/// <summary>DeepOCR：给出的字母表含重复字符，不是互异字符列表。</summary>
	/// <remarks>
	///   <para><b>含义</b>字母表必须逐字符互异：同一字符出现两次会破坏按位索引的映射语义（同一个字符对应两个索引，映射与解码都会歧义），故直接拒绝。隐蔽重复最棘手——肉眼不同但码点相同的字符、多个字母表文件拼接产生的重字。</para>
	///   <para><b>排查方向</b>传入前按码点判重去冗，而非按肉眼核对；空格、制表符是否有意收录要显式确认。与相邻码区分：INVALID_ALPHABET（7891）管每个条目是否单字符，本码管整体是否互异。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_OCR_MODEL_ALPHABET_NOT_UNIQUE = 7901;

	/// <summary>错误码 7910：apply_dl_model 不允许使用默认输出。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>调用 apply_dl_model 应用模型时未显式给出输出（走了被禁止的默认输出路径）。</para>
	///   <para><b>所属族</b>DL 模型通用错误 7910～7917 的起始码，专属于 apply_dl_model 的输出约定。</para>
	///   <para><b>参数取向</b>应显式声明要产出的输出，而非依赖默认。[待实测：默认输出被禁的原因]</para>
	/// </remarks>
	public const int Jl_ERR_DL_MODEL_APPLY_NO_DEF_OUTPUTS = 7910;

	/// <summary>错误码 7911：不受支持的通用参数名（generic parameter）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>查询/设置了一个该模型/算子不存在或不允许的通用参数名。</para>
	///   <para><b>所属族</b>与 7914（取值不支持）配对：本码是“参数名”层面的错误，7914 是“参数值”层面的错误。</para>
	///   <para><b>排查方向</b>先确认参数名，再看取值。</para>
	/// </remarks>
	public const int Jl_ERR_DL_MODEL_UNSUPPORTED_GENPARAM = 7911;

	/// <summary>错误码 7912：该算子不支持给定的模型。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>把一个不兼容的模型传给某算子——算子本身不接受该模型类型/配置。</para>
	///   <para><b>所属族</b>DL 模型通用错误 7910～7917 之一；与 7910（apply_dl_model 无默认输出）同处“模型与算子对接”层面。</para>
	///   <para><b>取舍</b>应换用兼容该模型的算子，或改用适配此算子的模型。</para>
	/// </remarks>
	public const int Jl_ERR_DL_MODEL_OPERATOR_UNSUPPORTED = 7912;

	/// <summary>错误码 7913：无法设置请求的运行时（runtime）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>把模型切到某推理后端/运行时（如特定框架或设备 runtime）失败。</para>
	///   <para><b>所属族</b>DL 模型通用错误 7910～7917 之一；与 7960（设备精度不支持）相关但层面不同：本码是 runtime 本身不可选。</para>
	///   <para><b>取舍</b>需退回模型实际可绑定的 runtime。[待实测：可选 runtime 集合]</para>
	/// </remarks>
	public const int Jl_ERR_DL_MODEL_RUNTIME = 7913;

	/// <summary>错误码 7914：不受支持的通用参数取值（generic value）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>为某通用参数（gen value）提供了该模型/算子不接受的取值。</para>
	///   <para><b>所属族</b>与 7911（不支持的通用参数名）配对：7911 是参数名不存在，7914 是参数名存在但取值非法。</para>
	///   <para><b>排查方向</b>核对取值枚举/范围，而非怀疑参数名拼写。</para>
	/// </remarks>
	public const int Jl_ERR_DL_MODEL_UNSUPPORTED_GENVALUE = 7914;

	/// <summary>错误码 7915：无效的样本数量。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>给定/推断的样本数目与操作要求矛盾（如为 0、负值或与批次不匹配）。</para>
	///   <para><b>所属族</b>DL 模型通用错误 7910～7917 之一；与 7925/7926（数据集/索引）相关但此处针对数量参数本身。</para>
	///   <para><b>取值约定</b>样本数须为正且与配套数据集一致。[待实测：上界约束]</para>
	/// </remarks>
	public const int Jl_ERR_DL_MODEL_INVALID_NUM_SAMPLES = 7915;

	/// <summary>错误码 7916：某参数不受转换后（converted）模型支持。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对经格式转换的模型设置/读取其在转换中丢失或本就不支持的参数。</para>
	///   <para><b>所属族</b>转换模型子族 7916～7917；与 7911（不支持的通用参数）区分：7911 是通用参数表不含，7916 特指“转换后”模型丢参数。</para>
	///   <para><b>排查方向</b>确认该参数是否需在转换前设置。</para>
	/// </remarks>
	public const int Jl_ERR_DL_MODEL_CONVERTED_PARAM = 7916;

	/// <summary>错误码 7917：对已转换（converted）模型执行了不受支持的操作。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>模型经格式转换后能力受限，某些针对原生模型的操作不再可用。</para>
	///   <para><b>所属族</b>转换模型子族 7916～7917：7916 是转换后模型不支持某参数，7917 是根本不支持某操作。二者共同提示“转换后≠功能等价”。</para>
	///   <para><b>取舍</b>受限操作应在转换前的原生模型上完成。[待实测：转换后可用操作集]</para>
	/// </remarks>
	public const int Jl_ERR_DL_MODEL_CONVERTED_UNSUPPORTED = 7917;

	/// <summary>错误码 7925：给定的数据集（dataset）不正确。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>数据集本身非法：类型不符、为空或与操作期望结构不一致。</para>
	///   <para><b>所属族</b>数据集子族 7925～7926；本码是“整体不合法”，若整体合法但取用具体样本越界则为 7926。</para>
	///   <para><b>取舍</b>先修数据集整体有效性，再谈样本索引问题。</para>
	/// </remarks>
	public const int Jl_ERR_DL_INVALID_DATASET = 7925;

	/// <summary>错误码 7926：给定的样本索引（sample index）非法。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>按索引取用数据集样本时越界，或索引指向不存在的样本。</para>
	///   <para><b>所属族</b>数据集子族 7925～7926：7925 指数据集整体不正确，7926 指其中某一样本索引越界。</para>
	///   <para><b>取值约定</b>索引通常从 0 起、须小于样本总数（见 7915 无效样本数）。[待实测：索引基 0/1]</para>
	/// </remarks>
	public const int Jl_ERR_DL_INVALID_SAMPLE_INDEX = 7926;

	/// <summary>错误码 7931：变换（transform）名称非法。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>引用了一个不存在的预处理变换名。</para>
	///   <para><b>所属族</b>变换子族 7931～7934 的能力前置码：名称都不对时，参数类码 7932/7933 与读取类码 7934 无从触发。</para>
	///   <para><b>排查方向</b>核对变换名拼写及其是否属于当前可用集合。[待实测：合法变换名清单]</para>
	/// </remarks>
	public const int Jl_ERR_DL_TRANSFORM_INVALID_NAME = 7931;

	/// <summary>错误码 7932：单个变换（transform）的参数不可用。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对某个变换查询/设置其不具备的参数（该变换类型不支持此参数）。</para>
	///   <para><b>所属族</b>变换子族 7931～7934；与 7931（名称非法）区分：名称合法但该变换不支持该参数时报本码；与 7933（流水线级参数）粒度不同。</para>
	///   <para><b>取舍</b>先按 7931 确认变换名，再核对该变换支持的参数集。</para>
	/// </remarks>
	public const int Jl_ERR_DL_TRANSFORM_PARAM_NOT_AVAILABLE = 7932;

	/// <summary>错误码 7933：变换流水线（transform pipeline）的参数不可用。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>查询/设置整条变换流水线的某个参数，但该流水线参数在当前上下文不存在或不生效。</para>
	///   <para><b>所属族</b>变换子族 7931～7934；与 7932（单个 transform 参数不可用）区分：本码针对“流水线级”参数，7932 针对“单变换级”参数。</para>
	///   <para><b>前提</b>通常需先构建流水线，参数方可查询。[待实测：流水线构建顺序]</para>
	/// </remarks>
	public const int Jl_ERR_DL_TRANSFORM_PIPELINE_PARAM_NOT_AVAILABLE = 7933;

	/// <summary>错误码 7934：读取（IO）时遇到非法的 transform 类型。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>从数据/模型文件读取预处理变换时，记录里的 transform 类型无法识别。</para>
	///   <para><b>所属族</b>变换子族 7931～7934：7931 名称非法、7932/7933 参数不可用、7934 读取时类型非法。本码专指反序列化/读取阶段的类型错误。</para>
	///   <para><b>排查方向</b>多与文件版本/写入端不匹配有关。[待实测：受影响的读写算子]</para>
	/// </remarks>
	public const int Jl_ERR_CNN_IO_INVALID_TRANSFORM_TYPE = 7934;

	/// <summary>错误码 7940：深度计数（Deep Counting）模型尚未 prepare（准备）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>在未 prepare 的 Deep Counting 模型上直接执行计数/推理。</para>
	///   <para><b>所属族</b>Deep Counting 子族 7940～7943 的起始码，是“先准备再使用”的顺序约束被违反。</para>
	///   <para><b>顺序依赖</b>须先 prepare 成功；但若属 7942 所述不支持 prepare 的模型，则本流程不适用，需另找初始化路径。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_COUNTING_NOT_PREPARED = 7940;

	/// <summary>错误码 7941：所选主干网络（backbone）不可设置。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>为 Deep Counting 模型指定了当前不可设定/不受支持的 backbone。</para>
	///   <para><b>所属族</b>Deep Counting 子族 7940～7943：与 7943（模型缺主干）区分——本码是“设置主干”动作被拒，7943 是模型内容本身无主干。</para>
	///   <para><b>取舍</b>需选用可设置的 backbone 或用其内置主干。[待实测：可设置 backbone 列表]</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_COUNTING_UNSUPPORTED_BACKBONE = 7941;

	/// <summary>错误码 7942：对深度计数（Deep Counting）模型使用 prepare 不受支持。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对 Deep Counting 模型调用了它并不支持的 prepare 流程。</para>
	///   <para><b>所属族</b>Deep Counting 子族 7940～7943 之一；与 7940（未 prepare）看似相关但方向相反：本码说明该类型根本不该走 prepare，7940 说明走了但不满足其前置。</para>
	///   <para><b>取舍</b>若模型不支持 prepare，应跳过该步而非重试。[待实测：Deep Counting 的正确初始化路径]</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_COUNTING_PREPARE_UNSUPPORTED = 7942;

	/// <summary>错误码 7943：深度计数（Deep Counting）模型不包含主干网络（backbone）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>模型缺少必要的特征主干，无法完成计数所需的特征提取。</para>
	///   <para><b>所属族</b>Deep Counting 子族 7940～7943：7940 未 prepare、7941 主干不可设置、7942 prepare 不支持、7943 缺主干。本码是“模型内容”问题，7941 是“主干可配置性”问题。</para>
	///   <para><b>取舍</b>缺 backbone 的模型无法通过 prepare 补救，需换完整模型。</para>
	/// </remarks>
	public const int Jl_ERR_DEEP_COUNTING_NO_BACKBONE = 7943;

	/// <summary>错误码 7960：所选设备不支持请求的计算精度。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>在深度学习推理/训练中将精度设为目标硬件（CPU/GPU/加速卡）不支持的模式。</para>
	///   <para><b>所属族</b>独立于 7970 之后的功能子族，专门针对“设备×精度”组合能力；与 7913（runtime 不可设置）相近但侧重精度而非 runtime。</para>
	///   <para><b>取舍</b>需按设备能力退回其支持的精度。[待实测：各设备支持的精度档]</para>
	/// </remarks>
	public const int Jl_ERR_DL_DEVICE_UNSUPPORTED_PRECISION = 7960;

	/// <summary>错误码 7970：模型不适合用于持续学习（invalid model for continual learning）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对不能承载持续学习的模型类型请求该功能。</para>
	///   <para><b>所属族</b>持续学习子族 7970～7975 的能力前置码，是最早的一道校验：类型不符时 7971/7972 的初始化状态机不会建立。</para>
	///   <para><b>取舍</b>需换用支持持续学习的模型，而非在此模型上强推。[待实测：支持持续学习的模型类型]</para>
	/// </remarks>
	public const int Jl_ERR_DL_CONTINUAL_LEARNING_UNSUPPORTED_MODEL = 7970;

	/// <summary>错误码 7971：模型尚未为持续学习做初始化。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>在未初始化持续学习状态的模型上直接执行增量/推理等后续操作。</para>
	///   <para><b>所属族</b>持续学习子族 7970～7975；与 7972（已初始化）互为镜像。本码指示“先初始化再使用”的顺序约束被违反。</para>
	///   <para><b>顺序依赖</b>必须先成功初始化，后续 7973 的推理与 7975 的增量样本要求才成立。</para>
	/// </remarks>
	public const int Jl_ERR_DL_CONTINUAL_LEARNING_MODEL_NOT_INITIALIZED = 7971;

	/// <summary>错误码 7972：模型已被初始化用于持续学习（重复初始化）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对已经做过持续学习初始化的模型再次执行初始化操作。</para>
	///   <para><b>所属族</b>持续学习子族 7970～7975；与 7971（尚未初始化）互为镜像，共同界定初始化状态机：本码 = 已初始化，7971 = 未初始化。</para>
	///   <para><b>取舍</b>重复初始化通常应跳过而非强制重做，以免破坏既有增量状态。</para>
	/// </remarks>
	public const int Jl_ERR_DL_CONTINUAL_LEARNING_MODEL_ALREADY_INITIALIZED = 7972;

	/// <summary>错误码 7973：持续学习（continual learning）推理失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>处于持续学习流程的模型在推理阶段执行失败。</para>
	///   <para><b>所属族</b>持续学习子族 7970～7975 之一；区别于配置/初始化类码（7971/7972），本码发生在运行期推理。</para>
	///   <para><b>排查方向</b>先确认模型已完成 7971 所示的初始化，再检查输入约定。[待实测：推理失败的常见输入条件]</para>
	/// </remarks>
	public const int Jl_ERR_DL_CONTINUAL_LEARNING_INFERENCE_FAILED = 7973;

	/// <summary>错误码 7974：某操作会使已建立的持续学习（continual learning）状态失效。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对已为持续学习配置好的模型执行了破坏其增量训练前提的操作。</para>
	///   <para><b>所属族</b>持续学习子族 7970～7975 之一；与 7988（使 OOD 失效）句式对应但作用子系统不同。</para>
	///   <para><b>取舍</b>失效后需按 7971 重新初始化，而非直接继续增量。</para>
	/// </remarks>
	public const int Jl_ERR_DL_CONTINUAL_LEARNING_INVALID = 7974;

	/// <summary>错误码 7975：持续学习（初始化或增量阶段）样本多样性不足。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>在 continual learning 的 init 或 continual 算子上，提供的多样样本不足以支撑该阶段。</para>
	///   <para><b>所属族</b>持续学习子族 7970～7975 的末端；本码横跨 init 与 continual 两种算子，需先分辨处于哪个阶段。</para>
	///   <para><b>取舍</b>与 7986（OOD 多样性不足）措辞相同但子系统不同，别混用其排查结论。</para>
	/// </remarks>
	public const int Jl_ERR_DL_CONTINUAL_LEARNING_INSUFFICIENT_SAMPLE_DIVERSITY = 7975;

	/// <summary>错误码 7980：剪枝（pruning）数据与给定模型不匹配。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>用于确定剪枝重要性/校准的标定数据与目标模型不对应（如来自不同网络或不同输入分布）。</para>
	///   <para><b>所属族</b>剪枝子族 7980～7981 之一；与 7981（架构不支持）区分：本码是数据错配，属可修正问题。</para>
	///   <para><b>排查方向</b>用与待剪枝模型同源、同输入约定的数据重新标定。</para>
	/// </remarks>
	public const int Jl_ERR_DL_PRUNING_WRONG_DATA = 7980;

	/// <summary>错误码 7981：模型架构不支持剪枝（pruning）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对结构上无法剪枝的 CNN 架构请求剪枝操作。</para>
	///   <para><b>所属族</b>剪枝子族 7980～7981：7980 是剪枝数据与模型不匹配，7981 是架构本身不支持。二者分别对应“数据错”与“结构错”。</para>
	///   <para><b>取舍</b>不支持时只能换架构，无法通过更换数据绕过。[待实测：支持的架构族]</para>
	/// </remarks>
	public const int Jl_ERR_DL_PRUNING_UNSUPPORTED_BY_CNN = 7981;

	/// <summary>错误码 7985：模型类型不支持分布外（OOD）检测。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对不能承载 OOD 检测的模型类型请求该功能。</para>
	///   <para><b>所属族</b>OOD 子族 7985～7988 的起始码，是能力前置校验：类型不符时后续拟合（7986/7987）无从进行。</para>
	///   <para><b>取舍</b>需改用支持 OOD 的模型类型，而非在此类型上强推。[待实测：支持 OOD 的模型类型清单]</para>
	/// </remarks>
	public const int Jl_ERR_DL_OOD_UNSUPPORTED_MODEL_TYPE = 7985;

	/// <summary>错误码 7986：拟合分布外（OOD）检测所需样本多样性不足。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>训练/拟合 OOD 检测时提供的样本种类或分布过窄，无法建立可靠的分布内基线。</para>
	///   <para><b>所属族</b>OOD 子族 7985～7988 之一；与 7975（持续学习样本多样性不足）语义相近但作用于不同子系统。</para>
	///   <para><b>参数取向</b>应增加覆盖真实分布的多样样本后重试，而非放宽阈值。[待实测：最小多样性要求]</para>
	/// </remarks>
	public const int Jl_ERR_DL_OOD_INSUFFICIENT_SAMPLE_DIVERSITY = 7986;

	/// <summary>错误码 7987：分布外（OOD）检测计算过程中的内部错误。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>OOD 检测计算内部发生非预期异常（兜底码），通常不由用户输入直接决定。</para>
	///   <para><b>所属族</b>OOD 子族 7985～7988 之一；区别于参数类错误（7985/7986），本码指示算法内部故障。</para>
	///   <para><b>排查方向</b>若排除输入问题后仍复现，需上报复现条件。[待实测：可复现触发路径]</para>
	/// </remarks>
	public const int Jl_ERR_DL_OOD_INTERNAL_ERROR = 7987;

	/// <summary>错误码 7988：某操作会使已建立的分布外（OOD, out-of-distribution）检测失效。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对已完成 OOD 拟合的模型执行了破坏其统计基础的操作。</para>
	///   <para><b>所属族</b>OOD 子族 7985～7988：7985 模型类型不支持、7986 样本多样性不足、7987 内部计算错误、7988 操作使检测失效。</para>
	///   <para><b>取舍</b>失效后需按 7986 的前提重新以足够多样的样本拟合。</para>
	/// </remarks>
	public const int Jl_ERR_DL_OOD_INVALID = 7988;

	/// <summary>错误码 7990：DL 模块未加载。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>调用深度学习（DL）相关能力时，其运行模块尚未加载/初始化。</para>
	///   <para><b>所属族</b>整个 7883～7990 深度学习错误码区的“前置门”：模块未加载时，异常检测、OCR、模型应用、转换等下游 DL 错误通常根本不会到达。</para>
	///   <para><b>排查方向</b>先确保 DL 模块可用再排查具体子码。[待实测：本库 DL 模块是否仍随发行提供]</para>
	/// </remarks>
	public const int Jl_ERR_DL_MODULE_NOT_LOADED = 7990;

	/// <summary>错误码 8000：未知的算子名（operator name）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>按名称解析单个算子时未命中，给定的算子名不存在。</para>
	///   <para><b>所属族</b>运行时算子注册解析错误区段的起始码；与 8002（未知算子类）区分：8000 是算子名，8002 是算子类名。</para>
	///   <para><b>排查方向</b>确认名称拼写及对应组件是否已加载（另见 8001）。</para>
	/// </remarks>
	public const int Jl_ERR_WPRN = 8000;

	/// <summary>错误码 8001：register_comp_used 未被激活。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>使用了组件按需注册机制，但相关注册开关（register_comp_used）未启用。</para>
	///   <para><b>所属族</b>运行时算子注册错误区段，与 8000（未知算子名）、8002（未知算子类）配套。</para>
	///   <para><b>前提</b>该错误提示需先激活相应注册流程后才能解析算子。[待实测：激活入口在本库是否仍存在]</para>
	/// </remarks>
	public const int Jl_ERR_RCNA = 8001;

	/// <summary>错误码 8002：未知的算子类（operator class）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>按名称查找算子类时未命中，给定的算子类名不存在。</para>
	///   <para><b>所属族</b>与 8000（未知算子名）、8001（register_comp_used 未激活）同属运行时算子/算子类注册解析错误。</para>
	///   <para><b>排查方向</b>核对算子类名拼写；若涉及按需注册组件，见 8001。</para>
	/// </remarks>
	public const int Jl_ERR_WPC = 8002;

	/// <summary>错误码 8101：卷积/mask 文件打开失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>指定的掩模/滤波器文件无法打开（不存在、路径错误或无访问权限）。</para>
	///   <para><b>所属族</b>掩模文件读取错误 8101～8106 的起始码，是后续解析（8102/8103/8106）、尺寸检查（8104/8105）的前置：打不开时后续码无从谈起。</para>
	///   <para><b>排查方向</b>先验证文件存在与可读路径，再谈格式问题。</para>
	/// </remarks>
	public const int Jl_ERR_ORMF = 8101;

	/// <summary>错误码 8102：读取卷积/mask 文件时文件提前结束。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>文件在按声明尺寸读到应有数据前就到了末尾（文件被截断）。</para>
	///   <para><b>所属族</b>掩模文件读取错误 8101～8106 之一；与 8101（打开失败）、8106（元素过多）分别对应“打不开/太短/太长”三种文件完整性问题。</para>
	///   <para><b>排查方向</b>确认文件未被截断、下载或保存是否完整。</para>
	/// </remarks>
	public const int Jl_ERR_EOFRMF = 8102;

	/// <summary>错误码 8103：读取卷积/mask 文件时的数值转换错误。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>文件里的某个字段无法转换为目标数值类型（格式非法或超出可表示范围）。</para>
	///   <para><b>所属族</b>掩模文件读取错误 8101～8106 之一；区别于 8102（文件提前结束）——8103 是字段本身不可转换。</para>
	///   <para><b>排查方向</b>检查文件中是否存在非数字字符或溢出值。</para>
	/// </remarks>
	public const int Jl_ERR_CVTRMF = 8103;

	/// <summary>错误码 8104：读取卷积/mask 文件时行号/列号错误。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>文件中声明的行/列号非法（如越界或与尺寸不吻合）。</para>
	///   <para><b>所属族</b>掩模文件读取错误 8101～8106 之一，与 8105/8106 共同构成“尺寸—行列—元素数”一致性检查链。</para>
	///   <para><b>坐标约定</b>本库统一 row=y（向下为正）、column=x（向右为正）。</para>
	/// </remarks>
	public const int Jl_ERR_LCNRMF = 8104;

	/// <summary>错误码 8105：读取卷积/mask 文件时掩模尺寸溢出（过大）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>掩模文件的行/列尺寸超出允许上限。</para>
	///   <para><b>所属族</b>掩模文件读取错误 8101～8106 之一；与 8106（元素条目过多）相邻：8105 指尺寸上限，8106 指实际条目数。</para>
	///   <para><b>取值约定</b>掩模通常须为奇数×奇数以保持中心对齐。[待实测：尺寸上限的具体数值]</para>
	/// </remarks>
	public const int Jl_ERR_WCOVRMF = 8105;

	/// <summary>错误码 8106：读取卷积/mask 文件时输入的元素数量过多。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>掩模文件中给出的元素个数超过其声明的行列尺寸所能容纳的数量。</para>
	///   <para><b>所属族</b>掩模文件读取错误 8101～8106 之一；与 8105（尺寸溢出）不同——8105 是尺寸本身超限，8106 是元素条目数超出按尺寸推算的容量。</para>
	///   <para><b>排查方向</b>核对文件表头声明的行列数与实列元素数是否吻合。</para>
	/// </remarks>
	public const int Jl_ERR_NEOFRMF = 8106;

	/// <summary>错误码 8107：卷积时 margin（边缘扩展）类型错误。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>指定的 margin 类型不被当前卷积算子接受。</para>
	///   <para><b>所属族</b>与 8113（处理 margin 出错）配套：本码是类型判别失败，8113 是处理阶段失败。</para>
	///   <para><b>取值约定</b>margin 通常是枚举类型（如镜像/边界复制/循环等），须与算子支持项一致。[待实测：本库支持的具体枚举]</para>
	/// </remarks>
	public const int Jl_ERR_WRRA = 8107;

	/// <summary>错误码 8108：卷积时没有任何掩模对象对应到非空区域（所有区域为空）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>作为掩模输入的区域全部为空，导致没有可用对象参与卷积。</para>
	///   <para><b>所属族</b>属于 8101～8108 的 convol/mask 掩模处理错误区段；本码针对“空区域/零对象”这一退化输入。</para>
	///   <para><b>排查方向</b>上游生成区域的算子是否返回了空区域。[待实测：判定“空”的阈值]</para>
	/// </remarks>
	public const int Jl_ERR_MCN0 = 8108;

	/// <summary>错误码 8110：卷积权重因子（weight factor）为 0。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>归一化用的权重因子为 0，会导致后续除零或结果退化。</para>
	///   <para><b>所属族</b>与 8111（权重个数不一致）同属权重设定错误；本码专指因子值为 0 的退化情形。</para>
	///   <para><b>排查方向</b>检查权重设置是否全部为 0 或缺省未赋。[待实测：权重因子出现的算子]</para>
	/// </remarks>
	public const int Jl_ERR_WF0 = 8110;

	/// <summary>错误码 8111：卷积时权重个数与要求不一致。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>提供的权重（weights）数量与掩模元素数/通道数等所需数目不匹配。</para>
	///   <para><b>所属族</b>与 8110（权重因子为 0）同指权重设定问题；本码强调数量对不上，8110 强调值为 0。</para>
	///   <para><b>排查方向</b>让权重个数等于掩模所需元素数。[待实测：所需数目的推导规则]</para>
	/// </remarks>
	public const int Jl_ERR_NWC = 8111;

	/// <summary>错误码 8112：秩（rank）滤波给定的 rank 值非法。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>rank 滤波里被选取的序位值超出可用邻域元素范围或不符合要求。</para>
	///   <para><b>所属族</b>rank 子族码；与 8113 同处卷积/秩运算区段。</para>
	///   <para><b>取值约定</b>rank 是邻域内的排序索引，须在有效区间内。[待实测：合法 rank 闭区间]</para>
	/// </remarks>
	public const int Jl_ERR_WRRV = 8112;

	/// <summary>错误码 8113：卷积/秩（rank）运算在处理 margin（边缘扩展方式）时出错。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对图像做卷积或秩滤波时，margin 参数的类型/取值与当前运算不匹配。</para>
	///   <para><b>所属族</b>与 8107（WRRA，margin 类型错误）相邻，二者都指 margin 设定问题；8113 是处理阶段的兜底码，8107 是类型判别阶段的码。</para>
	///   <para><b>排查方向</b>核对传入 margin 是否与该算子支持的扩展方式一致。[待实测：支持的 margin 取值集合]</para>
	/// </remarks>
	public const int Jl_ERR_ROVFL = 8113;

	/// <summary>错误码 8114：解析滤波器/卷积掩模（filter mask）文件时出错。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读取或解析磁盘上的掩模/卷积核文件失败，文件内容不符合预期格式。</para>
	///   <para><b>所属族</b>与 8101～8106（convol/mask 一族的打开、越界读、转换、行列号、尺寸溢出、元素过多）同属掩模文件读写错误；本码是"解析阶段"的兜底码。</para>
	///   <para><b>排查方向</b>先对照 8101 确认文件能打开、非空；再核对掩模尺寸/元素数（8105、8106）与行列号（8104）是否合法。[待实测：具体抛出本码的算子清单]</para>
	/// </remarks>
	public const int Jl_ERR_EWPMF = 8114;

	/// <summary>错误码 8120：卷积所需系数个数超出允许，原文提示怀疑 sigma 过大。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>按给定 sigma（或等效精度要求）推算出的卷积系数个数超过原生侧允许上限；sigma 越大，达到同等精度所需系数越多，本码即这一溢出的信号。</para>
	///   <para><b>所属族</b>81xx 卷积/掩模区段收尾码：8101～8106 管掩模文件读写完整性，8111/8112/8113 管权重与 margin 设定，8120 专指"按参数推算的系数个数超限"，与 8111（权重个数不匹配）区分——8111 是数量对不上，8120 是数量超上限。</para>
	///   <para><b>排查方向</b>减小 sigma 或改用小掩模直接卷积；原生侧允许个数上限不在托管层。[待实测：系数个数上限的具体数值]</para>
	/// </remarks>
	public const int Jl_ERR_WNUMM = 8120;

	/// <summary>错误码 8200：背景估计用的数据组 ID 无效。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>调用背景估计（bg_esti）族算子时传入了未创建、已清除或根本不存在的数据组 ID；是本族（8200～8210）里"入口就没有有效数据集"的码。</para>
	///   <para><b>所属族</b>BED（Background EstiData）子族：8200 ID 无效、8201 无激活数据组、8202 ID 重复、8204 创建失败。本库托管层未包装背景估计算子（JlOperatorSet 中无 Create/Set/UpdateBgEsti），此码多来自消息表核对。[待实测：原生侧是否仍提供该算子族]</para>
	///   <para><b>排查方向</b>确认所用 ID 已由创建算子成功登记、且未被清除后才继续使用。</para>
	/// </remarks>
	public const int Jl_ERR_WBEDN = 8200;

	/// <summary>错误码 8201：设置背景估计参数时没有处于激活态的数据组。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>内嵌原文点明发生在 set_bg_esti：想改参数，但当前没有任何已创建并激活的背景估计数据组——参数无处安放。</para>
	///   <para><b>所属族</b>BED 子族时序码：与 8200（ID 无效，入口即错）不同，8201 是"顺序错"——没先走创建流程就设置参数。8204 则指创建本身失败。</para>
	///   <para><b>排查方向</b>先创建/激活数据组再设参。本库托管层未包装背景估计算子，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_NBEDA = 8201;

	/// <summary>错误码 8202：创建数据组时给定的 ID 已被占用。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>创建背景估计数据组时指定的 ID 与现存某个数据组重复，避免静默覆盖既有背景模型。</para>
	///   <para><b>所属族</b>BED 子族"ID 生命周期"码：8200 说 ID 不存在、8202 说 ID 已存在，二者互补；创建动作本身失败（非重号）归 8204。</para>
	///   <para><b>排查方向</b>换未占用的 ID，或先清除旧数据组再建。本库托管层未包装背景估计算子，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_BEDNAU = 8202;

	/// <summary>错误码 8204：背景估计数据组创建失败（create_bg_esti）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>内嵌原文点明发生在 create_bg_esti：调用被执行了但没有新数据组被建立，典型成因是同时活动数据组数量已达原生上限。[待实测：上限值与确切触发路径]</para>
	///   <para><b>所属族</b>BED 子族；注意 8203 在本仓库码表中为空缺号。与 8202（ID 重号）区分：8202 是重号被拒，8204 是无资源可建。</para>
	///   <para><b>排查方向</b>先清除不再使用的数据组释放名额。本库托管层未包装背景估计算子，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_NBEDC = 8204;

	/// <summary>错误码 8205：不允许以对象列表（多元素对象元组）传参。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>向只接受单个图像/单个对象的背景估计参数里传入了对象列表（多句柄元组）；该参数无"逐元素广播"语义，故直接拒绝。</para>
	///   <para><b>所属族</b>BED 子族里的传参形态码：与尺寸不符（8206）、更新区越界（8207）同属"输入图像/区域不合规矩"，但本码针对的是个数/类型形态而非像素内容。</para>
	///   <para><b>排查方向</b>把对象列表拆开逐个传入。本库托管层未包装背景估计算子，此码多来自消息表核对。[待实测：具体哪些 bg_esti 参数受此限制]</para>
	/// </remarks>
	public const int Jl_ERR_NTM = 8205;

	/// <summary>错误码 8206：输入图像尺寸与数据组内背景图像尺寸不一致。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>送入背景估计处理/更新的图像宽高与数据组中已建立的背景图像不同；背景按像素网格统计，逐点比较要求两者严格同尺寸。</para>
	///   <para><b>所属族</b>BED 子族输入一致性码：与 8207（更新区域比背景图像大）同为空间不匹配，8206 是整幅尺寸不符，8207 是区域越出背景边界。</para>
	///   <para><b>排查方向</b>换用同尺寸图像，或按新尺寸重建背景估计数据组；resize 对齐会引入伪差，不建议。本库托管层未包装背景估计算子，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_WISBE = 8206;

	/// <summary>错误码 8207：用于更新背景的区域大于背景图像。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>update_bg_esti 类操作中给定的更新区域（update region）超出背景图像的像素范围——更新只允许在背景网格内进行。</para>
	///   <para><b>所属族</b>BED 子族；与 8206（整幅图像尺寸不符）区分：8206 比的是图像对图像，8207 比的是区域对背景图像的边界。</para>
	///   <para><b>排查方向</b>用背景图像 domain 与更新区求交后再传入，而不是放宽检查。本库托管层未包装背景估计算子，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_UDNSSBE = 8207;

	/// <summary>错误码 8208：统计数据组的数量太少。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>背景估计内部为每个像素维护多套统计量（均值/方差等分层数据），当配置的统计数据组数目低于所选自适应算法的最低要求时回本码。[待实测：各模式要求的最低组数]</para>
	///   <para><b>所属族</b>BED 子族配置量值码：与 8209（adapt mode 非法）、8210（frame mode 非法）同属"参数取值不合规"，本码针对的是数量而非取值枚举。</para>
	///   <para><b>排查方向</b>增大统计数据组数量或改用要求更少的自适应模式。本库托管层未包装背景估计算子，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_SNBETS = 8208;

	/// <summary>错误码 8209：背景估计的自适应模式（adapt mode）取值非法。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>设置 adapt_mode 参数时给出不在原生支持集合内的值；该参数控制背景随场景更新的快慢档位。[待实测：支持的模式取值清单]</para>
	///   <para><b>所属族</b>BED 子族"参数取值枚举"码，与 8210（frame mode 非法）成对，分别管两个模式参数；设置入口即被拒，不会拖到处理阶段。</para>
	///   <para><b>排查方向</b>核对模式字符串拼写与大小写。本库托管层未包装背景估计算子，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_WAMBE = 8209;

	/// <summary>错误码 8210：背景估计的帧模式（frame mode）取值非法。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>设置 frame_mode 参数时给出不在原生支持集合内的值；该参数决定单帧/序列等背景更新方式。[待实测：支持的帧模式取值清单]</para>
	///   <para><b>所属族</b>BED 子族收尾码，与 8209（adapt mode 非法）成对管两个模式参数；本族 8200～8210 至此结束。</para>
	///   <para><b>排查方向</b>核对取值拼写。本库托管层未包装背景估计算子，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_WFMBE = 8210;

	/// <summary>错误码 8250：姿态估计用的点对应数目太少。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>做位姿/相机标定估计（PE 族）时，提供的图像点—物方点对应少于所选估计方法解算全部自由度所需的最低数量；点数够了解算才谈得上精度。</para>
	///   <para><b>所属族</b>PE（pose estimation）子族，与 8251（方法非法）配套：8250 是数据量不足，8251 是方法参数本身不对。本库托管层 JlCalibData 已删除、无标定估计算子包装，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>增加对应点对数量；各方法最低点数要求不在码里。[待实测：各方法的最低对应点数]</para>
	/// </remarks>
	public const int Jl_ERR_PE_NPCTS = 8250;

	/// <summary>错误码 8251：姿态估计给定的方法（method）无效。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>调用 PE 族估计算子时 method 参数取值不在支持集合内（如所选求解算法未被编译进当前原生库或根本不存在）。</para>
	///   <para><b>所属族</b>PE 子族，与 8250（点对太少）同为标定/姿态估计的入参门：8251 拦方法名，8250 拦数据量。本库托管层无标定估计算子包装（JlCalibData 已删除），此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>改用受支持的方法名并核对拼写。[待实测：原生支持的方法取值清单]</para>
	/// </remarks>
	public const int Jl_ERR_PE_INVMET = 8251;

	/// <summary>错误码 8300：OCR 分类器可容纳的字体数已达上限。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>向 OCR 分类器追加新字体（训练/登记字符集）时，字体总数超过该分类器结构预设的容量上限；是容量码，不是拼写或格式码。</para>
	///   <para><b>所属族</b>OCR 8300～8338 大族的第一个码。OCR 分类器能力已从本库托管层删除（无任何 Ocr 算子包装），此码多来自消息表核对。[待实测：字体数上限的具体值]</para>
	///   <para><b>排查方向</b>改为拆分多个分类器分别承载字符集，而不是继续向同一分类器追加。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_MEM1 = 8300;

	/// <summary>错误码 8301：给定的字体编号（font ID/number）非法。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>按字体编号选择/查询分类器内某套字体时编号越界或不存在；OCR 分类器内部按整数编号管理多套字体，选错编号即回本码。</para>
	///   <para><b>所属族</b>OCR 字体管理码：与 8300（字体数超上限）管"容量"不同，本码管"选择"；与 8304（未激活任何字体）相邻但含义相反——8301 是选了不存在的，8304 是一个都没选。</para>
	///   <para><b>排查方向</b>先枚举分类器实际拥有的字体编号再引用。OCR 分类器已从本库托管层删除，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_WID = 8301;

	/// <summary>错误码 8302：OCR 内部错误——内部句柄/编号 ID 不对。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>原生 OCR 模块自检时发现其内部记录的 ID 与请求使用的不一致，属内部一致性兜底码而非用户参数码。</para>
	///   <para><b>所属族</b>OCR 内部错误三兄弟之一：8302（wrong ID）、8310（Internal error 1）、8311（Internal error 2）都不指向具体用户参数，多为状态错乱或版本不配套。OCR 能力已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>重建分类器实例复测；若稳定复现，优先怀疑传入的句柄已被释放后复用。</para>
	/// </remarks>
	public const int Jl_ERR_OCR1 = 8302;

	/// <summary>错误码 8303：OCR 未初始化——还没有任何字体被读入。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>在未读入任何字体/分类器内容的状态下直接执行识别，OCR 系统处于空载。与 8304（有字体但未激活）区分：8303 是压根没读进来，8304 是读进来了但没选。</para>
	///   <para><b>所属族</b>OCR 字体管理码；OCR 分类器已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>先加载字体文件/完成训练再调用识别；识别前检查初始化状态。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_NNI = 8303;

	/// <summary>错误码 8304：没有任何字体被激活。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>分类器里已存在字体，但当前未选中（激活）任何一套就去识别/查询，识别不知道该用哪套字模。</para>
	///   <para><b>所属族</b>OCR 字体管理码：与 8303（未读入字体的空载态）区分，与 8301（激活了不存在的编号）互补。OCR 分类器已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>识别前先显式选择要用的字体编号；多字体分类器尤其容易漏这一步。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_NAI = 8304;

	/// <summary>错误码 8305：OCR 内部错误——确定（笔画）角度时用了非法阈值。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>OCR 在提取字符方向特征、确定角度时对投影/直方图设的阈值不合法，原生模块以内部错误回抛。</para>
	///   <para><b>所属族</b>OCR 内部错误码（与 8302、8306、8310/8311 同性质）：均不指名具体用户参数。OCR 分类器已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>多与训练字符质量（过小、断裂）相关，可先复核输入字符区域尺寸与预处理。[待实测：该角度阈值对应的用户可调参数名]</para>
	/// </remarks>
	public const int Jl_ERR_OCR_WTP = 8305;

	/// <summary>错误码 8306：OCR 内部错误——特征属性（attribute）设置非法。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>OCR 训练/分类时给特征属性（如线性/非线性等 attribute 标志）传了内部无法处理的值，原生模块以内部错误回抛。</para>
	///   <para><b>所属族</b>OCR 内部错误码，与 8305（角度阈值非法）同为"内部参数不合内部约定"；不指向具体文件字段。OCR 分类器已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>回退到默认特征属性配置复测。[待实测：受影响的属性名与合法取值]</para>
	/// </remarks>
	public const int Jl_ERR_OCR_WF = 8306;

	/// <summary>错误码 8307：OCR 分类器文件的版本不被支持。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读取 OCR 分类器文件时，文件头的版本号与当前原生库能解析的版本集合不符；是"版本对不上"，不是"内容损坏"（后者见 8308/8309）。</para>
	///   <para><b>所属族</b>OCR 文件加载码；同为版本码的还有 8321（MLP）、8331（SVM）、8336（CNN）与 8313（训练字符文件），本码指旧式通用 OCR 分类器文件。OCR 分类器已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>用与运行库同版本的工具重新导出分类器文件，老文件不要手改版本号充新。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_READ = 8307;

	/// <summary>错误码 8308：OCR 分类器文件中节点数不一致。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读入的 OCR 分类器文件（MLP 结构）里声明的节点数与实际数据规模对不上——头部与体部不自洽，多半是文件被截改或跨版本拼接所致。</para>
	///   <para><b>所属族</b>OCR 文件加载码：8307 是版本不对、本码是结构计数矛盾、8309 是文件整体偏短，三者构成"读文件三连"。OCR 分类器已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>重新导出完整文件；手改过节点数的文件无法通过校验。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_NODES = 8308;

	/// <summary>错误码 8309：OCR 分类器文件过短（未读完就到文件尾）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>按文件头声明的结构继续读取时提前到达文件末尾（EOF），即文件被截断或写入未完成。</para>
	///   <para><b>所属族</b>OCR 文件加载码三连之一：8307 版本、8308 节点计数、8309 长度；本码可与 8102（掩模文件提前结束）类比——同一"截断"语义在不同文件族里的两个码。OCR 分类器已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>核对文件大小与来源完整性（传输/复制是否中断），重导后复检。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_EOF = 8309;

	/// <summary>错误码 8310：OCR 内部错误 1（原生兜底码）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>原生 OCR 模块内部一致性检查失败的第一号兜底码，原文未指明具体用户参数；与 8311 仅编号不同。[待实测：两码分别对应的内部检查点]</para>
	///   <para><b>所属族</b>OCR 内部错误码（8302、8305、8306、8310、8311），特征都是"错在模块内部而非你的某个入参"。OCR 分类器已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>遇到时记录完整操作序列并重建分类器；反复出现应视为原生缺陷上报，而非在托管层找参数。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_INC1 = 8310;

	/// <summary>错误码 8311：OCR 内部错误 2（原生兜底码）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>原生 OCR 模块内部一致性检查失败的第二号兜底码，与 8310 同一性质、不同检查点。[待实测：具体检查点]</para>
	///   <para><b>所属族</b>OCR 内部错误码收尾；至此 8300～8311 全为分类器字体/文件/内部类问题，8312 起转向 OCR 工具类型与训练文件。OCR 分类器已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>同 8310：重建实例、记录序列，不当作用户参数错误处理。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_INC2 = 8311;

	/// <summary>错误码 8312：OCR 工具类型不对（既非 'box' 也非 'net'）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>OCR 工具只支持两大类型：'box'（盒式/k-NN 型）与 'net'（神经网络/MLP 型）；对类型不符的句柄执行只适用于其中一类的操作即回本码。</para>
	///   <para><b>所属族</b>与 8351（WOCVTYPE，OCV 工具版本/类型不支持）名近而族不同：本码属 OCR 区段，指类型枚举错，不是版本错。OCR 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>操作前核对句柄来自哪类创建算子，box 与 net 的工具句柄不可互用。</para>
	/// </remarks>
	public const int Jl_ERR_WOCRTYPE = 8312;

	/// <summary>错误码 8313：OCR 训练字符文件（trf）的版本不被支持。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读写/合并 OCR 训练文件时，文件版本与当前库不匹配。注意与 8312 的区别：8312 是工具类型错，本码是训练文件版本错，常量名 OCR_TRF 也印证其属 TRF 子族。</para>
	///   <para><b>所属族</b>TRF（训练文件）子族之首：8313 版本、8314 图像过大、8315 区域过大、8316 受保护、8317 口令错、8319 合并失败。OCR 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>用同版本工具重新生成训练文件。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_TRF = 8313;

	/// <summary>错误码 8314：图像太大，训练文件装不下。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>把训练样本图像写入 OCR 训练文件时，图像尺寸超过该文件格式允许的样本上限；训练文件按固定结构存小样本，不是任意大图。</para>
	///   <para><b>所属族</b>TRF 子族容量码，与 8315（区域过大）成对：一个卡图像、一个卡区域。OCR 已从本库托管层删除，此码多来自消息表核对。[待实测：允许的最大样本尺寸]</para>
	///   <para><b>排查方向</b>裁剪/缩小训练字符区域后重试，而不是换更大的原图。</para>
	/// </remarks>
	public const int Jl_ERR_TRF_ITL = 8314;

	/// <summary>错误码 8315：区域太大，训练文件装不下。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>以区域（而非整幅图像）登记训练样本时，区域尺寸/面积超过训练文件允许上限。</para>
	///   <para><b>所属族</b>TRF 子族容量码，与 8314（图像过大）成对；本码卡在区域本身，缩小图像不解决问题。OCR 已从本库托管层删除，此码多来自消息表核对。[待实测：区域上限的具体值]</para>
	///   <para><b>排查方向</b>用字符的最小外接区域并剔除粘连笔画后重试。</para>
	/// </remarks>
	public const int Jl_ERR_TRF_RTL = 8315;

	/// <summary>错误码 8316：OCR 训练文件受密码保护。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>试图写入/修改一个带保护标志的 OCR 训练文件而未提供口令（或该操作根本不被允许）。与 8317 区分：本码是"文件受保护"这件事本身，8317 是"提供了但口令错"。</para>
	///   <para><b>所属族</b>TRF 子族权限码对（8316/8317）。OCR 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>取得原始口令或向文件提供方索取未保护版本；受保护文件无法绕过。</para>
	/// </remarks>
	public const int Jl_ERR_TRF_PT = 8316;

	/// <summary>错误码 8317：受保护的 OCR 训练文件口令错误。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>打开/写入带保护的训练文件时提供的口令与文件记录不匹配。</para>
	///   <para><b>所属族</b>TRF 权限码对之二：8316 指"受保护"，8317 指"口令错"——见到 8317 说明保护校验流程已走通、只是凭证不对。OCR 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>核对口令的大小写与首尾空白；口令按原样字符串比较。</para>
	/// </remarks>
	public const int Jl_ERR_TRF_WPW = 8317;

	/// <summary>错误码 8318：序列化项里不是一个有效的 OCR 分类器。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>反序列化得到的句柄项内容与请求的对象类型不符——里面存的根本不是 OCR 分类器（或已损坏）。同族还有各分类器专版码：8322（MLP）、8332（SVM）、8334（k-NN）、8337（CNN），本码是旧式通用 OCR 分类器那一个。</para>
	///   <para><b>所属族</b>NOSITEM（no serialized item）家族横跨 8318/8322/8332/8334/8337/8356，语义同、目标类型不同，按目标类型对号入座。OCR 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>确认序列化文件由哪类算子写出，读回时用配对的 deserialize 算子。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_NOSITEM = 8318;

	/// <summary>错误码 8319：训练文件合并失败——输入与输出是同一个文件。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>拼接（concatenate）两个 OCR 训练文件时输入文件与输出文件路径相同，就地覆盖会毁掉源数据，故直接拒绝。</para>
	///   <para><b>所属族</b>TRF 子族收尾码；名字里的 EIO 即 equal input/output。OCR 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>合并永远写到第三个新文件名，成功后再替换旧文件。</para>
	/// </remarks>
	public const int Jl_ERR_TRF_CON_EIO = 8319;

	/// <summary>错误码 8320：MLP 分类器文件格式无效。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读 MLP 型 OCR 分类器文件时文件头/结构不符合该格式的识别要求（不是版本旧，而是压根不像一个 MLP 分类器文件）。与 8321（版本不支持）、8322（序列化项不对）三分 MLP 加载故障。</para>
	///   <para><b>所属族</b>分类器三码模板（NOCLASSFILE/WRCLASSVERS/NOSITEM）在 MLP 段的实例；SVM 8330～8332、k-NN 8333/8334、CNN 8335～8337 同构。OCR 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>确认文件扩展名与生成算子配对，别把 SVM 文件当 MLP 读。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_MLP_NOCLASSFILE = 8320;

	/// <summary>错误码 8321：MLP 分类器文件版本不被支持。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>MLP 分类器文件结构可辨（非 8320）但版本号不在当前库支持集合内；旧库导出的文件最常见此错。</para>
	///   <para><b>所属族</b>分类器版本码族：8307（旧式通用 OCR）、本码（MLP）、8331（SVM）、8336（CNN）、8313（训练文件），k-NN 段无版本码。</para>
	///   <para><b>排查方向</b>用训练机与运行机同版本的库重导文件；不要在运行侧改头适配。OCR 已从本库托管层删除，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_MLP_WRCLASSVERS = 8321;

	/// <summary>错误码 8322：序列化项里不是一个有效的 MLP 分类器。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>deserialize 目标类型声明为 MLP 分类器，但序列化项里的实际类型不符或已损坏；属 NOSITEM 家族（8318/8322/8332/8334/8337/8356）的 MLP 实例。</para>
	///   <para><b>所属族</b>与 8320（磁盘文件格式错）区分：8320 卡文件读入路径，本码卡内存序列化项路径。</para>
	///   <para><b>排查方向</b>核对序列化/反序列化算子是否同型配对。OCR 已从本库托管层删除，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_MLP_NOSITEM = 8322;

	/// <summary>错误码 8330：SVM 分类器文件格式无效。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读入的文件不符合 SVM 分类器格式；与 8331（版本）、8332（序列化项）构成 SVM 加载三分。与 8320 同构，仅分类器类型不同。</para>
	///   <para><b>所属族</b>分类器三码模板在 SVM 段的实例（8330～8332）。OCR/SVM 分类器已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>确认文件确由 SVM 训练算子写出，防止与其他 OCR 文件混名。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_SVM_NOCLASSFILE = 8330;

	/// <summary>错误码 8331：SVM 分类器文件版本不被支持。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>SVM 分类器文件能认出格式（非 8330）但版本号超出支持集合；版本码族的 SVM 实例。</para>
	///   <para><b>所属族</b>分类器版本码族（8307/8321/8331/8336），k-NN 段没有对应版本码，不要拿 8333 误猜成版本错。OCR/SVM 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>同版本重导；跨大版本升级库时批量复检既有分类器文件。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_SVM_WRCLASSVERS = 8331;

	/// <summary>错误码 8332：序列化项里不是一个有效的 SVM 分类器。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>反序列化目标声明为 SVM 分类器而序列化项实际类型不符或已损坏；NOSITEM 家族的 SVM 实例。</para>
	///   <para><b>所属族</b>与 8330/8331 三分 SVM 加载故障：格式、版本、序列化项各管一条路径。OCR/SVM 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>序列化写回与读出必须用同型算子配对。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_SVM_NOSITEM = 8332;

	/// <summary>错误码 8333：k-NN 分类器文件格式无效。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读入文件不符合 k-NN（box 型）分类器格式；与 8334（序列化项）构成 k-NN 段的两码，本段没有版本码。</para>
	///   <para><b>所属族</b>分类器三码模板在 k-NN 段的裁剪版（8333/8334）。OCR/k-NN 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>与 8312（工具类型非 box/net）联动排查：拿错类型的文件常先撞格式码再撞类型码。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_KNN_NOCLASSFILE = 8333;

	/// <summary>错误码 8334：序列化项里不是一个有效的 k-NN 分类器。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>反序列化目标声明为 k-NN 分类器但序列化项类型不符或已损坏；NOSITEM 家族的 k-NN 实例，k-NN 段两码之二。</para>
	///   <para><b>所属族</b>NOSITEM 家族（8318/8322/8332/8334/8337/8356）成员，按目标类型区分而非按故障形态。OCR/k-NN 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>同型配对序列化算子；跨类型复用序列化文件必撞此码。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_KNN_NOSITEM = 8334;

	/// <summary>错误码 8335：CNN 分类器文件格式无效。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读入文件不符合 CNN 分类器格式；与 8336（版本）、8337（序列化项）三分 CNN 加载故障，与 MLP/SVM 段同构。</para>
	///   <para><b>所属族</b>分类器三码模板在 CNN 段的实例（8335～8337）。OCR/CNN 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>注意与深度学习 DL 族（7883～7990）的 CNN 码区分：本码属 OCR 分类器路径，不指示 DL 模块问题。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_CNN_NOCLASSFILE = 8335;

	/// <summary>错误码 8336：CNN 分类器文件版本不被支持。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>CNN 分类器文件可辨格式但版本号超出支持集合；版本码族的 CNN 实例（8307/8321/8331/8336 之一）。</para>
	///   <para><b>所属族</b>8335～8337 CNN 三分之中段。OCR/CNN 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>训练端与运行端库版本对齐后重导文件。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_CNN_WRCLASSVERS = 8336;

	/// <summary>错误码 8337：序列化项里不是一个有效的 CNN 分类器。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>反序列化目标声明为 CNN 分类器而序列化项类型不符或已损坏；NOSITEM 家族的 CNN 实例，8335～8337 三分之末。</para>
	///   <para><b>所属族</b>NOSITEM 家族（8318/8322/8332/8334/8337/8356）。OCR/CNN 已从本库托管层删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>与 7990（DL 模块未加载）区分：本码不涉及 DL 运行时，只关 OCR 序列化项。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_CNN_NOSITEM = 8337;

	/// <summary>错误码 8338：当前模式下没有该结果名。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>按结果名查询 OCR 输出时，所选名目在当前工作模式（如训练/识别/特征导出）下并不产生；OCR 结果集随模式而变，不是所有模式都全量输出。</para>
	///   <para><b>所属族</b>OCR 段收尾码（8300～8338 之末）；8350 起进入 OCV 区段。OCR 已从本库托管层删除，此码多来自消息表核对。[待实测：各模式可用结果名清单]</para>
	///   <para><b>排查方向</b>先查询当前模式实际可得的结果名列表再按名取值，不要跨模式硬套。</para>
	/// </remarks>
	public const int Jl_ERR_OCR_RESULT_NOT_AVAILABLE = 8338;

	/// <summary>错误码 8350：OCV 系统未初始化。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>在 OCV（字符存在性校验）系统初始化前调用其算子。本库托管层仍保留 ReadOcv/WriteOcv/SerializeOcv/DeserializeOcv/CloseOcv 静态门面，创建/读取模板前的原生状态不对即回此码。</para>
	///   <para><b>所属族</b>OCV 8350～8356 段起始码：8350 系统未初始化、8351 工具版本、8353 对象名、8354/8355 训练前后时序、8356 序列化项。</para>
	///   <para><b>排查方向</b>先用 ReadOcv 等建立有效句柄再执行后续操作；托管层调用失败统一以 JlOperatorException 抛出。</para>
	/// </remarks>
	public const int Jl_ERR_OCV_NI = 8350;

	/// <summary>错误码 8351：OCV 工具文件的版本不被支持。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>读入的 OCV 工具文件版本与当前库不匹配；经 ReadOcv 读盘时最可能撞到。与 8312（WOCRTYPE，OCR 工具类型错）名近族不同。</para>
	///   <para><b>所属族</b>OCV 段版本码；本库 ReadOcv/WriteOcv 确有包装，此码在读写 OCV 文件路径上可实际遇到。</para>
	///   <para><b>排查方向</b>用同版本工具重导 OCV 模板文件，勿手改版本头。[待实测：支持的 OCV 文件版本集合]</para>
	/// </remarks>
	public const int Jl_ERR_WOCVTYPE = 8351;

	/// <summary>错误码 8353：OCV 对象的名字不对。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>按对象名引用 OCV 工具内的成员对象（模板类等）时名称不存在或不符合命名约定；注意码表里 8352 为空缺号。</para>
	///   <para><b>所属族</b>OCV 段"引用名"码，与 8356（序列化项无效）区分：8353 名错、8356 类型/内容错。本库仅保留 OCV 文件读写与序列化门面，训练/检测算子未包装。</para>
	///   <para><b>排查方向</b>严格核对名字大小写与出处清单，勿凭记忆拼名。[待实测：OCV 对象命名规则细节]</para>
	/// </remarks>
	public const int Jl_ERR_OCV_WNAME = 8353;

	/// <summary>错误码 8354：OCV 工具已经训练过，不能重复训练。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对已执行过训练（train）的 OCV 工具再次训练；训练是"未训练→已训练"的一次性状态迁移，重复触发即回此码。</para>
	///   <para><b>所属族</b>OCV 训练时序码对：8354 训了第二次、8355 没训就用。本库托管层未包装 add_ocv_train_image/train_ocv，此码多在原生直调或消息表核对时遇到。</para>
	///   <para><b>排查方向</b>需要重训时另建新工具或走重训流程，而不是对同一句柄再次 train。</para>
	/// </remarks>
	public const int Jl_ERR_OCV_II = 8354;

	/// <summary>错误码 8355：OCV 工具尚未训练就被使用。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>在未添加训练图像/未执行训练的状态下直接做 OCV 检测——模板集合为空，检测无从进行。</para>
	///   <para><b>所属族</b>OCV 训练时序码对之二：与 8354 分别把守"训练之后不能重复"与"训练之前不能检测"两端。本库未包装训练/检测算子，此码多在消息表核对时遇到。</para>
	///   <para><b>排查方向</b>先登记训练图像并完成训练再检测；从文件 ReadOcv 读入的模板也要确认其确已含训练结果。</para>
	/// </remarks>
	public const int Jl_ERR_OCV_NOTTR = 8355;

	/// <summary>错误码 8356：序列化项里不是一个有效的 OCV 工具。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>DeserializeOcv 收到的序列化项并非有效 OCV 工具（来源存了别的类型、或内容已损坏）。NOSITEM 家族的 OCV 实例，也是本库托管层实际最可能触发的一员。</para>
	///   <para><b>所属族</b>NOSITEM 家族（8318/8322/8332/8334/8337/8356）收尾；OCV 段 8350～8356 至此结束。本库 ReadOcv/DeserializeOcv 产出的句柄须配 CloseOcv 释放。</para>
	///   <para><b>排查方向</b>确认序列化项出自 SerializeOcv/WriteOcv 的 OCV 路径；成功后得到的新句柄记得释放。</para>
	/// </remarks>
	public const int Jl_ERR_OCV_NOSITEM = 8356;

	/// <summary>错误码 8370：函数采样点（function points）个数不对。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>构造/使用函数对象时给的采样点数不符合要求，典型是 x 元组与 y 元组长度不等，或点数低于该函数类型的最低要求。</para>
	///   <para><b>所属族</b>函数对象 8370～8376 七码之一，为本段"数量—形状—次序—距离—单调—类型—精度"检查链的首环。本库托管层未包装任何函数对象算子（无 create/apply function 类包装），此段码多来自消息表核对。</para>
	///   <para><b>排查方向</b>先断言 x/y 两元组等长再传入。[待实测：各函数类型的最低点数]</para>
	/// </remarks>
	public const int Jl_ERR_WLENGTH = 8370;

	/// <summary>错误码 8371：给的值列表不构成函数。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>采样点里同一个 x 对应了多个不同的 y（一对多），违反"每个输入只有一个输出"的函数定义；插值无从取值。</para>
	///   <para><b>所属族</b>函数对象形状码：与 8372（次序非升）、8373（点距非法）分别管"重复 x"“乱序”“过近”，8371 是其中语义最重的一种——数据本身不是函数。</para>
	///   <para><b>排查方向</b>先对 x 排序去重再构造；本库托管层未包装函数算子，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_NO_FUNCTION = 8371;

	/// <summary>错误码 8372：采样点次序不对（未按升序排列）。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>函数采样点的 x 序列不是（严格）升序；插值实现要求先排序后查找，乱序输入直接被拒而不是被代排。</para>
	///   <para><b>所属族</b>函数对象形状码：8371 管一对多、本码管次序、8373 管间距；三者都发生在构造期而非求值期。本库托管层未包装函数算子，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>用排序工具把 x（连同配对的 y 一起）按升序整理后重试。</para>
	/// </remarks>
	public const int Jl_ERR_NOT_ASCENDING = 8372;

	/// <summary>错误码 8373：函数采样点间距非法。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>相邻采样点的 x 间距不满足要求（如为 0 或小于该函数类型允许的最小间隔）；升序但挤得太近也算非法，与 8372（乱序）互补覆盖"次序对但距离错"。</para>
	///   <para><b>所属族</b>函数对象形状码之一。本库托管层未包装函数算子，此码多来自消息表核对。[待实测：最小允许间距规则]</para>
	///   <para><b>排查方向</b>合并近邻采样点（取其均值 y）拉开间距后重试。</para>
	/// </remarks>
	public const int Jl_ERR_ILLEGAL_DIST = 8373;

	/// <summary>错误码 8374：函数不单调。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>在要求单调的场景（典型是求反函数/按值回查自变量）下，给定的函数采样存在升降折返；不单调时反函数不唯一，故直接拒绝。</para>
	///   <para><b>所属族</b>函数对象求值期约束码：8370～8373 卡构造形状，本码卡使用场景的数学性质，与 8375（函数类型错）区分。</para>
	///   <para><b>排查方向</b>截取单调段重新构造，或改走不需要单调性的正向求值路径；本库未包装函数算子，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_NOT_MONOTONIC = 8374;

	/// <summary>错误码 8375：函数类型不对。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>算子只接受特定类型/插值方式（如分段线性、样条）的函数对象，传入了不支持的那一类；属类型判别错，不是采样数据错。</para>
	///   <para><b>所属族</b>函数对象类型码，与 8374（数学性质不满足）区分：8375 在入口比类型，8374 在算法内比性质。本库未包装函数算子，此码多来自消息表核对。[待实测：支持互通的类型集合]</para>
	///   <para><b>排查方向</b>按目标算子要求重建对应类型的函数对象。</para>
	/// </remarks>
	public const int Jl_ERR_WFUNCTION = 8375;

	/// <summary>错误码 8376：double 转 float 后出现相同 x 值。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>采样点在 double 精度下 x 互不相同，但内部按 float 存储后两个相邻 x 舍入成同一值，等价于凭空造出"一对多"；是精度坍缩码，与 8371（数据本来就不是函数）区分。</para>
	///   <para><b>所属族</b>函数对象 8370～8376 收尾码；本码最隐蔽——上游校验全过、只在内部转换时爆。本库未包装函数算子，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>拉开采样点间距或减少高密度采样区，使 float 精度下仍可区分。</para>
	/// </remarks>
	public const int Jl_ERR_SAME_XVAL_CONV = 8376;

	/// <summary>错误码 8390：输入点无法排成规则网格。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对检出的网格点做拓扑连接时，点集排不出预期的行列结构——缺点、多光斑、透视畸变过大或间距不均都会触发；是网格标定/校正流程的几何门槛。</para>
	///   <para><b>所属族</b>GRID 三码（8390 连接、8391 成图、8392 自动旋转）之首。本库已静态包装 ConnectGridPoints（row/column 给格点，sigma 管平滑尺度，maxDist 管连接偏差容差），此码可实际遇到。</para>
	///   <para><b>排查方向</b>先复核上游点检出是否完整等距，再适当增大 maxDist 连接容差；靠放宽容差掩盖缺点会连错线。</para>
	/// </remarks>
	public const int Jl_ERR_GRID_CONNECT_POINTS = 8390;

	/// <summary>错误码 8391：生成校正输出图（map）时出错。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>由已连接的网格推畸变校正映射失败，多因网格覆盖不足以支撑请求的映射区域/网格间距参数与图像尺寸不匹配。</para>
	///   <para><b>所属族</b>GRID 三段流水线：8390 连不上点、本码连上了但成不了图、8392 旋转搜索失败；报错位置即流程阶段。本库 GenGridRectificationMap 已包装，此码可实际遇到。</para>
	///   <para><b>排查方向</b>缩小映射区域到网格覆盖内或加密网格；先解决 8390/8392 再谈本码。</para>
	/// </remarks>
	public const int Jl_ERR_GRID_GEN_MAP = 8391;

	/// <summary>错误码 8392：自动旋转（校正网格摆正）失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>网格处理流程在自动搜索旋转角把网格摆正到水平/垂直方向时找不到满足判据的角度，通常因网格本身倾斜过大、残缺或对比度不足。</para>
	///   <para><b>所属族</b>GRID 三码之末；本库 GenGridRectificationMap 有 rotation 入参，自动失败时可改为显式给角。[待实测：本码具体由哪个原生步骤抛出]</para>
	///   <para><b>排查方向</b>先修图（光照均匀、整幅网格在视野内），或显式传入大致旋转角绕开自动搜索。</para>
	/// </remarks>
	public const int Jl_ERR_GRID_AUTO_ROT = 8392;

	/// <summary>错误码 8393：参与标定的相机没有公共参数集。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>多相机联合标定时，各相机参数类型/投影模型之间没有可共同求解的参数集合（如一个面扫一个线扫、或畸变模型不同阶），标定方程组退化。</para>
	///   <para><b>所属族</b>CAL 标定段（8393～8401）起始码。本库 JlCalibData 已删除、无标定求解包装，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>保证同批标定的相机使用同类型 cameraParam；跨型混合标定需拆分。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NO_COMM_PAR = 8393;

	/// <summary>错误码 8394：像素尺度 Vy 必须大于 0。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>相机参数里 y 方向像素尺度 Vy 给了 0 或负值。本库坐标约定 row=y 向下为正，Vy 为正是投影/反投影可逆的前提；线扫相机的 s_v 参数路径同样受此约束。[待实测：受校验的具体参数名集合]</para>
	///   <para><b>所属族</b>CAL 段参数符号码，与 8393（无公共参数）同为标定入参门。本库 JlCalibData 已删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>检查 cameraParam 的像素尺度元组是否被误填 0 或带符号导出。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NEGVY = 8394;

	/// <summary>错误码 8395：同一定位图案被找到了多次。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>标定板寻像中同一个 finder pattern 检出重复（镜像、粘连板或参数过松导致重定位），图案与编号的一一对应被破坏。</para>
	///   <para><b>所属族</b>CAL 定位图案码组：8395 找重、8399 一个没找到、8401 位置互不自洽，分别对应"多、无、乱"三种失配。本库 find_calib_object 未包装，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>收紧检出参数、确保视野内只有一块完整标定板。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_IDENTICAL_FP = 8395;

	/// <summary>错误码 8396：该功能不适用于"线扫相机 + 透视镜头"组合。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>对投影模型为 perspective 的线扫相机调用了只对特定扫描/投影组合成立的函数；线扫的成像几何本身随编码器运动展开，透视镜头下模型不再自洽。</para>
	///   <para><b>所属族</b>CAL 能力边界码（常量名 LS-CP-NA = line scan camera perspective not available）。本库线扫相机参数族（cam_par）已删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>换面扫模型或改用与线扫匹配的投影参数族，别硬调相关几何函数。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_LSCPNA = 8396;

	/// <summary>错误码 8397：标定板标记（mark）分割失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>从图像中把标定板标记分割成独立区域失败（对比度不足、光照不均或标记粘连），是标定板求解流水线的第一段。</para>
	///   <para><b>所属族</b>CAL 流水线三阶码：8397 分割、8398 提轮廓、8399/8395/8401 寻像失败，错误越早越要上游修图。本库圆形标定板算子未包装，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>优先改善照明与阈值预处理；连续撞 8397 时先查图，不查参数。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_MARK_SEGM = 8397;

	/// <summary>错误码 8398：标定板标记的轮廓提取失败。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>分割已得到区域，但从区域/灰度跳变提取闭合轮廓失败（边界断裂、 sigma 与幅值设置不匹配等）。</para>
	///   <para><b>所属族</b>CAL 流水线三阶码中段：8397（分割）之后、寻像（8395/8399/8401）之前；本码在说明"区域有了但形状轮廓出不来"。本库未包装该流程，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>与 8397 同法先修图；若分割正常而轮廓失败，复核提取轮廓类算子的 sigma/阈值参数。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_CONT_EXT = 8398;

	/// <summary>错误码 8399：没有检测到任何定位图案。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>在图像里找不到标定板的 finder pattern——板不在视野内、姿态过斜、遮挡或图案尺寸与设置不符，一个候选都没有。</para>
	///   <para><b>所属族</b>CAL 定位图案码组的"无"：与 8395（找重）、8401（乱）三分。本库未包装 find_calib_object，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>确认板型参数与实际板一致、相机视野覆盖整板；这是标定图像质量问题的最终信号。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NO_FP = 8399;

	/// <summary>错误码 8400：标定/位姿解算至少需要 3 个标定点。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>用于解算的已知点—像点对少于 3 个；两点无法固定平面姿态的旋转与尺度（欠约束），故 3 点是硬下限。</para>
	///   <para><b>所属族</b>CAL 数据量码，与 8250（PE 族对应点太少）同一思想、不同算子族：8250 在姿态估计入口，本码在标定板/标定数据路径。本库 JlCalibData 已删除，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>补足标记点或降低筛选门槛以留下更多对应；3 点只是下限，工程上应远多于 3。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_LCALP = 8400;

	/// <summary>错误码 8401：定位图案的位置互不自洽。</summary>
	/// <remarks>
	///   <para><b>触发条件</b>检出的各 finder pattern 数量够且无重复，但相互位置关系与板型定义的几何排布对不上（板被遮挡一半、拼接错或严重透视畸变）。</para>
	///   <para><b>所属族</b>CAL 定位图案码组"乱"之一员，也是本带（8120～8401）终点码：8395 多、8399 无、8401 乱。本库未包装标定板寻像，此码多来自消息表核对。</para>
	///   <para><b>排查方向</b>逐张回看检出图确认图案归属；若只拍到半块板，换完整视野重拍而非放宽几何容差。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_INCONSISTENT_FP = 8401;

	/// <summary>按名字找不到标定表（取值 8402）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>No calibration table found</c>，常量名 <c>NCPF</c> 读作"标定表文件不存在"（no calib ... file）：查找这一步就落空，还没走到读和解析。标定表族三码按阶段分层——本码"找不到"、<c>Jl_ERR_CAL_RECPF</c>=8403"找到了但读失败"、<c>Jl_ERR_CAL_FRCP</c>=8405"读进来了但格式非法"；先报本码时不要去查文件内容损坏。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8402 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 均判真，统一检查 <c>JlOperatorException.throwOperator</c> 必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>托管层 <c>JlCalibData</c> 已删除、不包装标定表查找，此码多来自消息表核对。按路径/名字问题处理：核对指定的标定表文件是否真实存在、路径大小写与扩展名是否一致；原生按什么目录与搜索规则找表，码里不带 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NCPF = 8402;

	/// <summary>读标定表描述文件出错（取值 8403）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Error while reading calibration table description file</c>，是"读文件动作本身失败"的笼统码；文件读到了但内容不合法时更常归到 <c>Jl_ERR_CAL_FRCP</c>=8405。表根本不存在时则是 <c>Jl_ERR_CAL_NCPF</c>=8402。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8403 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 均判真，统一检查必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>托管层 <c>JlCalibData</c> 已删除、不包装标定表读取，此码多来自消息表核对。先核对标定表文件是否可读（占用、权限、被截断），再谈格式与内容。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_RECPF = 8403;

	/// <summary>搜索椭圆时因低于最小阈值而找不到合格椭圆（取值 8404）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Minimum threshold while searching for ellipses</c>，常量名 <c>LTMTH</c> 意即"小于最小阈值"（less than min threshold）：标定/找圆流程要在轮廓里拟出椭圆，候选太少或对比度不够使统计量低于门限即回本码 [待实测：门限具体参数名不在码里，需回传文本确认]。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8404 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 均判真，统一检查必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>多因上游分割没提准标记：先看标记分割/轮廓提取码 <c>Jl_ERR_CAL_MARK_SEGM</c>=8397、<c>Jl_ERR_CAL_CONT_EXT</c>=8398 是否更早出现。处置是提高对比度或改用匹配板型的图，而非只放宽阈值。托管层未暴露圆形标定板算子，此码多来自消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_LTMTH = 8404;

	/// <summary>读标定表描述文件时读取错误或格式错误（取值 8405）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Read error / format error in calibration table description file</c>。它是"读/格式错"的合并码：标定表描述文件能打开，但读失败或结构不合法都归此。与 <c>Jl_ERR_CAL_RECPF</c>=8403（专指读表出错）、<c>Jl_ERR_CAL_NCPF</c>=8402（根本找不到表）三码分层。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8405 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 均判真，统一检查必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>本仓库托管层 <c>JlCalibData</c> 已删除，未包装标定表读写，此码多来自消息表核对。处置方向是用同版本工具重导标定表、别手改内部结构。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_FRCP = 8405;

	/// <summary>正向投影计算出错，因尺度或深度退化为零（取值 8406）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Error in projection: s_x = 0 or s_y = 0 or z = 0</c>：投影要把物方点除以深度、按像素尺度换算，一旦 <c>s_x</c>、<c>s_y</c> 或 <c>z</c> 取到 0 就会除以零/退化，故回本码。与 <c>Jl_ERR_CAL_UNPRO</c>=8407 成对，本码专指正向投影这一步。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8406 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 均判真，统一检查必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>投影链在本仓库经 <c>ImageToWorldPlane</c> 等算子暴露。触发多因位姿/标定把点放到深度为零处，或像素尺度被设为 0。处置：检查传入位姿是否让目标落在相机平面上、以及尺度参数有无被误置零。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_PROJ = 8406;

	/// <summary>反投影（图像坐标映回世界/物方坐标）计算出错（取值 8407）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Error in inverse projection</c>，是正向投影 <c>Jl_ERR_CAL_PROJ</c>=8406 的反向：给定像素点与深度/平面，回解其在物方坐标系中的位置时失败。成因通常是相机参数退化、点在成像区域外或深度不可解 [待实测：具体判定阈值不在码里，需由 <c>JlNativeApi.GetErrorMessage(err)</c> 回传文本确认]。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8407 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 均判真，统一检查必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>本仓库把投影链包装成了 <c>ImageToWorldPlane</c>/<c>GenImageToWorldPlaneMap</c> 等算子，正常调用回本码说明传入的相机参数或位姿有问题；处置方向是复核 <c>cameraParam</c>、<c>worldPose</c>，而不是在托管层找反投影接口（托管层无独立反投影包装）。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_UNPRO = 8407;

	/// <summary>打不开相机参数文件时回本码（取值 8408）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Not possible to open camera parameter file</c>，指相机内参/畸变系数（<c>cam_par</c> 那一类）文件打不开。与位姿文件的 <c>Jl_ERR_CAL_REPOS</c>=8412 分属"内参文件"与"位姿文件"两条路径。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8408 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 均判真，统一检查必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>几何族里 <c>ImageToWorldPlane</c>、<c>GenImageToWorldPlaneMap</c>（<see cref="JlErrorDef"/> 之外已包装）确需 <c>cameraParam</c>，但通常由内存元组传入而非磁盘文件；走磁盘读文件的这条路托管层无对应包装，此码多来自消息表核对。现场看到先确认相机参数文件是否存在、可读。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_RICPF = 8408;

	/// <summary>解析标定相关文件时缺冒号导致格式错（取值 8409）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Format error in file: No colon</c>（"No colon" 指没读到第一个冒号）。与 <c>Jl_ERR_CAL_FICP2</c>=8410（缺第二个冒号）、<c>Jl_ERR_CAL_FICP3</c>=8411（缺分号）同属一份文本文件的分隔符族，按解析到的位置区分。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8409 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 均判真，统一检查必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>几乎只出现在手工编辑过的相关文件里。处置：与同版本工具重导一次，不要逐字符去补分隔符。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_FICP1 = 8409;

	/// <summary>解析标定相关文件时第二个冒号缺失导致格式错（取值 8410）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Format error in file: 2. colon is missing</c>（"2." 指第 2 个冒号）。与 <c>Jl_ERR_CAL_FICP1</c>=8409（缺第一个冒号）、<c>Jl_ERR_CAL_FICP3</c>=8411（缺分号）同属一份文本文件的分隔符族，按解析到的位置区分。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8410 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 均判真，统一检查必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>与 <c>Jl_ERR_CAL_FICP1</c>/<c>Jl_ERR_CAL_FICP3</c> 一样，几乎只出现在手工编辑过的相关文件里。处置：与同版本工具重导一次，不要逐字符去补分隔符。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_FICP2 = 8410;

	/// <summary>解析标定相关文件时缺分号导致格式错（取值 8411）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Format error in file: Semicolon is missing</c>。FICP1/2/3 三个码是同一份文本文件的三类"分隔符缺失"细分：8409 缺第一个冒号、8410 缺第二个冒号、本码缺分号。原生按解析到的位置报具体哪一类，本码停在分号处 [待实测：具体是哪一类文件的分号（相机参数/标定描述）由解析器决定，码本身不带文件名]。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8411 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 均判真，统一检查必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>几乎只出现在手工编辑过的相关文件里。处置：与同版本工具重导一次，不要逐字符去补分隔符。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_FICP3 = 8411;

	/// <summary>打不开相机参数（位姿）文件时回本码（取值 8412）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Not possible to open camera parameter (pose) file</c>。这里的"位姿文件"是存外参（相机相对世界坐标的位姿）的文件，不是相机内参文件（对应 <c>Jl_ERR_CAL_RICPF</c>=8408）；两码按文件种类分层，路径、权限问题都归本码。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8412 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 均判真，统一检查必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>本仓库托管层未暴露位姿文件读取算子（<c>JlCalibData</c> 已删除，<c>JlPose</c> 类仍在但无对应的磁盘读接口），因此此码多来自消息表核对。现场看到先确认文件是否存在、是否被占用。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_REPOS = 8412;

	/// <summary>相机参数（位姿）文件内容格式非法时回本码（取值 8413）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Format error in camera parameter (pose) file</c>。文件能打开但解析不通过：字段缺失、顺序不对或版本不匹配。与"打不开"的 <c>Jl_ERR_CAL_REPOS</c> 相对——本码说明文件读进来了、只是结构错了。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8413 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 均判真，统一检查 <c>JlOperatorException.throwOperator</c> 必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>本仓库托管层已删除 <c>JlCalibData</c>，此码多见于消息表核对。格式错常因手工编辑或用不同版本工具另存，处置是用与运行时同版本的工具重新导出该位姿文件，不要手改内部结构。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_FOPOS = 8413;

	/// <summary>打不开标定目标（标定板）描述文件时回本码（取值 8414）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Not possible to open calibration target description file</c>。标定流程要先读到描述标定板几何（格距、标记排布、板型）的 .descr 类文件，"打不开"专指文件层失败：路径错、文件不存在或无读权限，与读到内容却解析失败的 <c>Jl_ERR_CAL_RECPF</c>、<c>Jl_ERR_CAL_FRCP</c> 分层——本码停在最前面那一步。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8414 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 都判真，进 <c>JlOperatorException.throwOperator</c> 一类的统一检查必抛 <c>JlOperatorException</c>，属真错误而非可忽略状态。</para>
	///   <para><b>何时遇到与处置</b>本仓库托管层已删除 <c>JlCalibData</c>，未暴露标定模型算子，正常包装调用不会直接回读本码；现场见到时按文件访问问题排查：核对描述文件是否存在、路径大小写与只读权限，而非去查标定算法参数。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_OCPDF = 8414;

	/// <summary>打不开标定目标（标定板）附属的 PostScript 文件时回本码（取值 8415）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Not possible to open postscript file of calibration target</c>，常量名 <c>OCPPS</c> 读作 "Open Calib Postscript"。标定目标除描述文件外还关联着一份 PostScript 形式的文件（历史上用于描述板上标记排布）[待实测：该 ps 文件与描述文件的绑定方式随板型而异，码里不带板型信息]；本码专指这份 PostScript 文件在文件层打不开，与描述文件本身打不开的 <c>Jl_ERR_CAL_OCPDF</c>=8414 分属两个文件、两个码。</para>
	///   <para><b>归类</b>标定/几何族（前缀 <c>Jl_ERR_CAL_</c>），取值 8415 不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 均判真，统一检查 <c>JlOperatorException.throwOperator</c> 必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>托管层 <c>JlCalibData</c> 已删除、未暴露标定目标装载算子，此码多来自消息表核对。现场先确认与描述文件配套的 PostScript 文件是否同目录、文件名与大小写是否对得上——这是文件访问问题，不是标定算法参数问题。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_OCPPS = 8415;

	/// <summary>标定几何计算中对向量做归一化（除以长度化为单位向量）时出错（取值 8416）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Error while norming the vector</c>。归一化要除以向量长度，能失败最直接的方式就是长度为零或接近零（向量退化、没有方向），其次是分量含非法值；具体是哪一步的哪个向量，码里不带，需查 <c>JlNativeApi.GetErrorMessage(err)</c> 的回传文本 [待实测]。</para>
	///   <para><b>归类</b>84xx 标定族（前缀 <c>Jl_ERR_CAL_</c>），不小于 1000：<c>JlNativeApi.IsError</c> 与 <c>JlNativeApi.IsFailure</c> 都判真，进 <c>JlOperatorException.throwOperator</c> 统一检查必抛 <c>JlOperatorException</c>，属真错误。</para>
	///   <para><b>何时遇到与处置</b>多由输入几何退化触发（如两点重合导致差向量为零向量、方向基准缺失）。本仓库托管层未暴露标定模型类算子，正常包装调用不会直接回读本码；排查时核对提供的点/位姿是否有重合、退化观测。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_EVECN = 8416;

	/// <summary>对标定目标（标定板）的拟合失败（取值 8417）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Fitting of calibration target failed</c>，是目标拟合的总括性失败码：提取到的标记未能拟合成描述文件要求的几何模型。常量名 <c>NPLAN</c> 另暗示"非平面（non-planar）"一路成因 [待实测]。</para>
	///   <para><b>归类</b>标定族（8416~8450 连续段），不小于 1000，<c>JlNativeApi.IsError</c>、<c>JlNativeApi.IsFailure</c> 均判真，统一检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>何时遇到与处置</b>常见于上游就没提准标记：对比度不足、标记残缺或与所选板型不匹配。先看同一次调用里标记提取类码（如 <c>Jl_ERR_CAL_NNMAR</c>、<c>Jl_ERR_CAL_NOELL</c>）是否更早在日志中出现，再谈重拟合；本仓库托管层未暴露标定模型算子，此码多来自原生消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NPLAN = 8417;

	/// <summary>在标定板上按序搜索标记时，找不到"下一个"预期标记（取值 8418）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>No next mark found</c>。标定板标记是逐个定位后沿拓扑推下一个的，本码表示链条在某个标记处断了：该标记在图像外、被遮挡或对比度不够。</para>
	///   <para><b>坑</b>它是"部分可见"的典型信号——标定板可能只有一部分进了视场（与 <c>Jl_ERR_CAL_CPNII</c>"板不完全在图像内"相呼应但更宽容）。拿到本码不代表整板失效，已提取的前段标记仍可能是好的，但不足以继续拟合。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据（<c>JlNativeApi.IsError</c>/<c>IsFailure</c>）均判真。处置方向是把板移入视场中心、重打光或放宽提取参数后重采，而不是改数值参数。本仓库托管层未暴露相应标定算子。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NNMAR = 8418;

	/// <summary>最小二乘的正规方程组无解或不可解（取值 8419）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Normal equation system is not solvable</c>。标定平差把观测写成超定线性系统后解正规方程，本码指法矩阵秩亏或奇异——方程组本身退化，与残差大小无关。</para>
	///   <para><b>何时遇到</b>典型成因是观测几何退化：位姿多样性不足（板只平移不倾斜）、多个观测几乎重复、或部分未知量根本没有观测约束。区别于 <c>Jl_ERR_CAL_QETHM</c>（有解但残差超限）与 <c>Jl_ERR_CAL_NCONV</c>（迭代不收敛）：本码是"压根解不动"。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真、统一检查抛 <c>JlOperatorException</c>。处置是补不同姿态的观测、去掉重复观测；个别自由度不想标定时用自由度旗标显式固定而非留空观测。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NNEQU = 8419;

	/// <summary>标记三维位置解算的平均平方误差超过限值（取值 8420）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Average quadratic error is too big for 3D position of mark</c>：方程有解、拟合也走完，但解出的标记三维位置与模型比对的平均平方残差太大，被判为不可信。限值是多少不在码里 [待实测]。</para>
	///   <para><b>何时遇到</b>最常见的是"图像与描述文件对不上"：拿错板型/板号、描述文件里标记坐标与实物不符、或单位换算错（毫米当米）导致模型尺度整体偏。<c>Jl_ERR_CAL_NNEQU</c> 是没解，本码是有解但解得差。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置顺序：先核描述文件与实物板是否同一块、单位约定是否一致；再查标记定位质量（亚像素中心偏了残差必然偏大）。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_QETHM = 8420;

	/// <summary>拿到的轮廓不是椭圆，无法按圆形标记做椭圆拟合（取值 8421）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Non elliptic contour</c>。圆形标记从斜视角看应投影成椭圆，提取轮廓后按椭圆模型拟合；本码指轮廓形状根本不支持椭圆拟合——残缺、粘连到背景、或被相邻特征串成异形。</para>
	///   <para><b>坑</b>它未必是"真没有椭圆"，也可能是阈值/选区把标记切掉一角：轮廓点数不足或闭合性差同样拟合失败。与 <c>Jl_ERR_CAL_ELLDP</c>（退化成点）相对：那个是过于退化，本码是形状不对路。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真、统一检查抛异常。处置：检查二值化阈值与标记选取区域，确认光照下标记边界清晰、彼此隔离。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NOELL = 8421;

	/// <summary>原生内部求解函数 slvand() 收到非法参数值（取值 8422）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong parameter value slvand()</c>。slvand() 是原生核内部数值函数的名字（据命名疑为 Vandermonde 类求解器 [待实测]），本码指调用它时喂给的参数不合法——是内部环节报错，不是用户算子参数表里的参数。</para>
	///   <para><b>坑</b>看到"Wrong parameter"字样容易误去翻算子参数，其实用户侧改不到这个槽位；真正的错源通常是上游数据异常（尺寸为零的数组、NaN）传导进来。与 <c>Jl_ERR_CAL_WFRES</c> 成对：那是 slvand() 结果异常，本码是入参异常。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。本仓库托管层未暴露标定模型算子；若真收到，记录码与 <c>JlNativeApi.GetErrorMessage(err)</c> 文本上报，按原生内部一致性问题对待，同时自查输入点集是否含退化数据。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_WPARV = 8422;

	/// <summary>原生内部求解函数 slvand() 返回了不合理结果（取值 8423）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong function results slvand()</c>：函数被正常调起、也返回了，但结果通不过原生自己的合理性检查（如解发散、非有限值）。与 <c>Jl_ERR_CAL_WPARV</c> 成对——那是入参错，本码是出参错。</para>
	///   <para><b>坑</b>它同样不是用户参数的错：slvand() 是原生内部函数（命名疑为 Vandermonde 类求解器 [待实测]），用户改不到它的调用点，别按参数错误去翻算子参数表。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。多由病态输入（观测近乎退化、量纲悬殊）把内部解算数值带崩；记录码与 <c>JlNativeApi.GetErrorMessage(err)</c> 上报，并自查输入点集的尺度一致性。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_WFRES = 8423;

	/// <summary>标定目标描述文件里的标记间距不成立（取值 8424）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Distance of marks in calibration target description file is not possible</c>：描述文件中给出的标记间距离互相矛盾，几何上无法同时满足（如三点间距违反三角不等式），属于"模型文件本身不可实现"。</para>
	///   <para><b>何时遇到</b>多发生在手改/编辑过描述文件之后，或把不同型号板的文件张冠李戴。它不是测量误差问题，改提取参数救不了。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：用原厂描述文件替换当前文件重新加载；确需自制板则重新生成一致的坐标/间距定义。<c>JlNativeApi.GetErrorMessage(err)</c> 可查具体消息文本。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_ECPDI = 8424;

	/// <summary>指定的自由度旗标值不在允许集合内（取值 8425）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Specified flag for degree of freedom not valid</c>。相机标定优化允许逐自由度指定"优化还是固定"，本码指某个自由度的旗标取值非法（不认识的值或类型不对），不是指标定结果自由度不够。</para>
	///   <para><b>坑</b>常量名 WEFLA 读作 "wrong flag"，容易与 <c>Jl_ERR_CAL_NNEQU</c>（方程不可解）混为一家：本码纯粹是参数拼写/取值错，改旗标字符串即可，无需补观测。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。核对所用标定函数的自由度旗标允许值清单后重设；本仓库托管层未暴露该族算子包装，具体合法值表 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_WEFLA = 8425;

	/// <summary>迭代得到的最小误差不低于（未降到限值以下）（取值 8426）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Minimum error did not fall below</c>——注意这条原生文本本身是半句话，限值数值没有带在消息里 [待实测]。语义是优化收敛到了一个高于门限的最小值：迭代正常走完，但精度不达标。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_CAL_NCONV</c> 是"没收敛"（迭代不平静），<c>Jl_ERR_CAL_QETHM</c> 是单标记残差超限，本码是整体最小误差仍嫌大——三者处置不同：本码优先怀疑观测数据质量与初值。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：剔除差观测重平差、增加位姿多样性；若门限可设，放宽或按工艺要求重设。托管层未暴露该族算子。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NOMER = 8426;

	/// <summary>Pose 里旋转/平移分量的类型不对（取值 8427）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong type in Pose (rotation / translation)</c>。位姿由旋转与平移两部分组成，本码指其中某一部分的表示类型与期望不符——<c>JlPose</c> 在托管层仍在，但其内部旋转/平移槽位的表示约定本仓库文档未给出 [待实测]。</para>
	///   <para><b>坑</b>它与 1201 起的"控制参数类型"族不同：那是算子入参类型错，本码是 Pose 结构内部的部件类型错，排查对象是位姿数据的构造/序列化来源，而不是算子参数表。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真、统一检查抛 <c>JlOperatorException</c>。处置：确认喂入的位姿来自本库同一版本生成，勿手改内部数值或跨版本搬数据。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_WPTYP = 8427;

	/// <summary>图像尺寸与相机参数里记录的感光面/像元尺寸推算值不匹配（取值 8428）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Image size does not match the measurement in camera parameters</c>。相机参数（像元尺寸、宽高）隐含了应有的图像幅面，实际图像对不上即报本码——投影/反投影在这种错配下会系统性偏，不如早停。</para>
	///   <para><b>何时遇到</b>典型场景是"参数与相机不是同一台/同一模式"：换了分辨率模式、ROI 裁剪后的子图、或把 A 相机的参数配给 B 相机的图。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：用与实际传感器一致的相机参数，或整幅原图参与运算；本仓库托管层 <c>JlCamPar</c> 已删除，此码多见于消息表核对与跨版本排查。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_WIMSZ = 8428;

	/// <summary>空间点无法投影进线扫相机图像（取值 8429）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Point could not be projected into linescan image</c>。线扫相机的像面沿扫描方向由编码器/时间轴拼出来，一个三维点若要落进这帧图，必须落在已扫描覆盖的那段行程内；点越出行程或落在幅面外，投影无解即报本码。</para>
	///   <para><b>坑</b>它常是"数据配错"而非几何不可能：用了别的帧的扫描区间、行程长度按错比例（毫米/像素混用）换算时最先撞上它。与 <c>Jl_ERR_CAL_NCONV</c> 等拟合类码不同，这是单点投影层的问题。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：核对点所属工件段与图像行的时间/行程对应关系；剔除越界点后重试。托管层未暴露线扫标定算子。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NPILS = 8429;

	/// <summary>标定标记的直径无法测定（取值 8430）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Diameter of calibration marks could not be determined</c>。有些标定流程要先从图像里量出标记圆点的直径（用于定亚像素中心或推算尺度）；本码指量不出来——边缘太糊、椭圆拟合失败或标记太小未覆盖足够像素。</para>
	///   <para><b>与相邻码区分</b>轮廓不成椭圆报 <c>Jl_ERR_CAL_NOELL</c>，退化成一个点报 <c>Jl_ERR_CAL_ELLDP</c>；本码是"要拿直径却没拿到"，多半是尺寸/分辨率层面的问题（离得太远、像元太大）。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：拉近工作距离或换更高分辨率让标记直径占足够像素，并确认打光下圆边界锐利。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_DIACM = 8430;

	/// <summary>标定板的朝向无法判定（取值 8431）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Orientation of calibration plate could not be determined</c>。板在图像里可能被旋转成任意角度，算法要靠定向结构（排列规律或定向标记）判出"哪边是板的正方向"；本码指判不出来。</para>
	///   <para><b>坑</b>与 <c>Jl_ERR_CAL_NOMF</c>（找不到定向标记）不同：本码更偏"有标记但几何不足以定向"，例如板近似正对且对称性让两个朝向同样合理。朝向错了不会报错而是姿态镜像/转 180°，这正是它要拦的静默错误。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：让板带倾角成像、保证定向标记可见，或改用带非对称特征的板型。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_ORICP = 8431;

	/// <summary>标定板没有完整落在图像之内（取值 8432）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Calibration plate does not lie completely inside the image</c>。整板可见是部分流程的硬前提（要按板的完整几何对位），哪怕只裁掉一角也报本码。</para>
	///   <para><b>与相邻码区分</b>只断了一部分链条、还能找到若干标记时报 <c>Jl_ERR_CAL_NNMAR</c>；本码是"一眼判定板出界"。某些板型/算法允许部分可见，某些不允许——允许性依流程而定 [待实测]。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：调整工作距离/视野或挪动工件使整板入画；固定工位场景则属安装偏位，应去修机械/相机安装而非算法参数。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_CPNII = 8432;

	/// <summary>提取到的标定标记个数不对（取值 8433）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong number of calibration marks extracted</c>。描述文件规定了该板应有多少个标记，实测提取数与之不符（多了或少了）即报本码。它是"计数级"失败，先于几何级失败发生。</para>
	///   <para><b>坑</b>少了通常是遮挡/出界/阈值过严；多了往往是把背景里的圆孔、反光斑误当标记。后者更危险：若数量恰好凑对，本码不响，错误会伪装成 <c>Jl_ERR_CAL_QETHM</c> 类的高残差出现。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：收紧标记选取（限定半径范围、亮度极性）、保证板面无干扰物，再重采。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_WNCME = 8433;

	/// <summary>给出了未知的参数组名（取值 8434）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Unknown name of parameter group</c>。标定类接口把一批参数打包成"参数组"按名字引用，本码指传进去的组名不在合法名单里——纯粹的字符串拼写/版本更名问题，与几何、数值都无关。</para>
	///   <para><b>坑</b>托管层 <c>JlTuple</c> 对字符串照单全收，拼错组名不会在装配期报错，只有到原生调用才以本码暴露；这不同于 1201 族的"类型错"——类型是对的，名字是陌生的。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。对照所用算子的参数组名单逐字核对（大小写敏感 [待实测]）后重试；合法名单本仓库未提供包装与文档。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_UNKPG = 8434;

	/// <summary>焦距取了负值，不满足"非负"要求（取值 8435）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Focal length must be non-negative</c>：本码的约束是"不得为负"（零或正放行）。焦距为负多半是把方向约定带进了参数、或在优化里被推过了零。</para>
	///   <para><b>与 8448 对照</b>同是焦距下限，<c>Jl_ERR_CAL_ILLFL</c> 要求"必须为正"（零不放行）：两处门限不同，说明不同函数对焦距的合法域定义不一致，报哪个码取决于走的是哪条路径，别互换处理。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真、统一检查抛 <c>JlOperatorException</c>。处置：给物理上合理的正焦距；若为优化产物，加初值或约束再跑。本仓库托管层相机参数对象已删除。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NEGFL = 8435;

	/// <summary>该函数对远心镜头相机不可用（取值 8436）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Function not available for cameras with telecentric lenses</c>。远心镜头下透视缩放规律与标准镜头不同（像高近似不随物距变），许多基于透视模型的公式对它是错的，原生直接拒绝而非给错数。</para>
	///   <para><b>坑</b>这是"能力边界"码不是数据错误：改参数、换初值都没用，得换算法路径。与 <c>Jl_ERR_CAL_HYPNA</c>（双远心不可用）、<c>Jl_ERR_CAL_LSCNA</c>（线扫不可用）同族，三码按相机/镜头类型区分拒用原因。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：确认相机参数的镜头类型标志与实际装的是否一致（标志错会让可用函数被误拒），或改用支持远心模型的函数。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_TELNA = 8436;

	/// <summary>该函数对线扫相机不可用（取值 8437）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Function not available for line scan cameras</c>。线扫相机没有完整的二维像面几何（一维行由扫描运动拉长），基于整幅透视投影的函数对它无定义，原生按相机类型标志直接拒用。</para>
	///   <para><b>坑</b>拒用依据是相机参数里的类型标志而非图像本身：标志设错时，面积相机也会被当线扫拒掉，或线扫被放行后算出错误结果。与 <c>Jl_ERR_CAL_TELNA</c>、<c>Jl_ERR_CAL_HYPNA</c> 同族，按类型区分。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：核对相机类型标志与实际传感器；线扫场景须走线扫专用的投影/标定路径。托管层相机参数对象已删除。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_LSCNA = 8437;

	/// <summary>拟合出的椭圆退化成了一个点（取值 8438）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Ellipse is degenerated to a point</c>：椭圆两半轴同时缩到零（或长短轴比病态），形状塌成一点。多因标记在图里只占一两个像素，或轮廓点几乎全部重合。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_CAL_NOELL</c> 是"轮廓不像椭圆"，<c>Jl_ERR_CAL_DIACM</c> 是"量不出直径"；本码是拟合本身走完了但几何解退化——典型是分辨率不足而非形状干扰。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：拉近/放大成像让标记占更多像素，或换更大标记；勿用调阈值来"救"像素级退化。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_ELLDP = 8438;

	/// <summary>找不到定向标记（取值 8439）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>No orientation mark found</c>。板上靠一个特殊形态的标记（与其余圆点不同，如更大/异径）给出方向基准，本码指这个标记没被找到——被遮挡、出界或对比度不足。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_CAL_NNMAR</c> 是搜索链断在"下一个"标记，本码专指定向标记缺失：圆点可能全都提取到了，计数也对（<c>Jl_ERR_CAL_WNCME</c> 不响），唯独定向者不在。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：保证定向标记入画且无遮挡；若板型选型时定向标记与背景干扰物同尺寸，改板型或收紧选取条件。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NOMF = 8439;

	/// <summary>相机标定优化不收敛（取值 8440）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Camera calibration did not converge</c>：迭代在限额内没有平静下来（误差来回摆或发散）。区别于 <c>Jl_ERR_CAL_NOMER</c>（收敛了但精度不达标）与 <c>Jl_ERR_CAL_NNEQU</c>（方程根本不可解）。</para>
	///   <para><b>何时遇到</b>高发原因是观测互相矛盾或病态：位姿几乎同一视角、含粗差观测（错配标记）、初值离真解太远（焦距/像元尺寸量纲给错）。多相机模型下也可能是相机间连接不完善。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真、统一检查抛 <c>JlOperatorException</c>。处置：给合理初值、剔除粗差观测、增加姿态多样性；反复不收敛时把每次迭代误差序列拉出来看是发散还是振荡 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NCONV = 8440;

	/// <summary>该函数对双远心（hypercentric）镜头相机不可用（取值 8441）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Function not available for cameras with hypercentric lenses</c>。双远心镜头物方像方都远心，投影模型与标准/单远心都不同，按类型标志拒用。与 <c>Jl_ERR_CAL_TELNA</c>（远心）、<c>Jl_ERR_CAL_LSCNA</c>（线扫）构成"按相机类型拒用"三码。</para>
	///   <para><b>坑</b>拒用的是"函数"不是"相机"：同一套标定流程里部分函数仍可用于双远心，报本码时先确认换的是否真是需要透视模型的函数，而不是笼统地认定该相机不能标定。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：核对镜头类型标志正确、改走支持该镜头的函数路径；托管层相机参数对象已删除，本码多见于消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_HYPNA = 8441;

	/// <summary>对点施加畸变（投影畸变计算）失败（取值 8442）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Point cannot be distorted.</c>。这里 distort 指按相机畸变模型对点做畸变侧运算（投影/反投影链的一环）；失败常见于点落在模型定义域外或畸变参数非法 [待实测]。</para>
	///   <para><b>坑</b>它与 <c>Jl_ERR_CAL_NPILS</c> 同为"单点投影不了"，但后者限定线扫场景；本码是通用投影层失败，先查相机参数本身（对应 <c>Jl_ERR_CAL_INVCAMPAR</c> 是否也应声出现）。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：确认相机参数完整合法、点在合理像幅邻域内；跨版本搬来的参数文件最容易让畸变系数越界。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_DISTORT = 8442;

	/// <summary>给出的边缘滤波器不合法（取值 8443）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Wrong edge filter.</c>。部分标定/定位路径内部用亚像素边缘提取，可指定滤波器（canny 一类）；本码指滤波器名字不被接受。合法名单本仓库未提供文档 [待实测]。</para>
	///   <para><b>归类</b>标定族，不小于 1000，两判据均判真。它属"枚举串取值错"，与 1301 族（参数值越界）不同段：1301 族指通用算子控制参数，本码专指标定路径内部的滤波器选项。</para>
	///   <para><b>处置</b>逐字核对滤波器标志串（注意与图像边缘算子用的名字可能同词不同表）；本仓库托管层未暴露相关算子，多数场合此码只出现在消息表核对与跨版本日志比对中。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_WREDGFILT = 8443;

	/// <summary>像元尺寸取值非法（原生文本要求非负，实际门限更严）（取值 8444）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Pixel size must be non-negative or zero</c>。字面自相矛盾（零本来就被"非负"包含），说明真实门限比字面更严——像元尺寸为零会使像素坐标到物理坐标的换算除零，实际应为"必须大于零" [待实测]。</para>
	///   <para><b>何时遇到</b>最常见是把毫米当米填、或模板里像素尺寸忘了填（留下 0）。它与 <c>Jl_ERR_CAL_WIMSZ</c> 联动：像元尺寸错了，推算的幅面也对不上。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：按传感器手册填正的实际像元尺寸（单位约定以相机参数文档为准）；相机参数对象本仓库已删除，多见于消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NEGPS = 8444;

	/// <summary>倾角 tilt 参数不在允许的取值范围内（取值 8445）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Tilt is in the wrong range</c>。tilt 是相机参数里描述像面相对物方坐标系倾斜的量，模型只接受特定区间内的值；越界即报本码。角度单位与允许区间本仓库文档未给出 [待实测]。</para>
	///   <para><b>坑</b>常量名 NEGTS 像"负值"专属，实际文本是"范围不对"——负得太多和正得太多都会中，别只往符号方向查。与 <c>Jl_ERR_CAL_NEGRS</c> 成对：那条管旋转角 rot，本条管倾角 tilt。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：tilt 拿不准时按相机模型约定给规范值（标准相机常用 0），让标定过程去微调而非手填越界值。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NEGTS = 8445;

	/// <summary>旋转角 rot 参数不在允许的取值范围内（取值 8446）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Rot is in the wrong range</c>。rot 是相机参数中像面内旋转角，模型要求其落在约定区间（角度/弧度约定与区间本仓库文档未给出 [待实测]）；越界即报本码，与 <c>Jl_ERR_CAL_NEGTS</c>（tilt 越界）成对。</para>
	///   <para><b>坑</b>名字里的 NEG 同样不代表"只是负值非法"；另外把角度当弧度（或反过来）填是越界最常见来源——数值本身没超物理直觉，却超了参数域。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：先统一单位约定再核数值；由标定生成的参数报此码时怀疑初值污染，重置 rot 后重标。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_NEGRS = 8446;

	/// <summary>相机参数整体不合法（取值 8447）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Camera parameters are invalid</c>。这是相机参数的"总括性"校验码：参数向量长度不对、字段组合矛盾或类型标志不认识等，未落到某个单一参数的专用码时统一报这里。</para>
	///   <para><b>与相邻码区分</b>焦距看 <c>Jl_ERR_CAL_NEGFL</c>/<c>Jl_ERR_CAL_ILLFL</c>，像元尺寸看 <c>Jl_ERR_CAL_NEGPS</c>，倾角/旋转看 <c>Jl_ERR_CAL_NEGTS</c>/<c>Jl_ERR_CAL_NEGRS</c>；这些都不响而整体校验响，才轮到本码——它不指明哪个字段，需逐字段自查。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。高发来源：跨版本/跨模型搬参数文件、手改坏了某个系数、数组拼接时长度错位。托管层 <c>JlCamPar</c> 已删除，本码多见于消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_INVCAMPAR = 8447;

	/// <summary>焦距不是正数（必须严格大于零）（取值 8448）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Focal length must be positive</c>：零也不放行。物理上焦距为零/负都无法成像，投影公式会除零或翻转。</para>
	///   <para><b>与 8435 对照</b><c>Jl_ERR_CAL_NEGFL</c> 的字面门限是"非负"（放零），本码门限是"为正"（不放零）——两条下限并存说明不同路径校验严格度不同；看到哪个码就按哪个路径的参数表查，不要合并理解。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：给正的实际焦距（注意单位与像元尺寸同量纲体系）；标定输出里出现非正焦距说明该次拟合已崩，应回到 <c>Jl_ERR_CAL_NCONV</c> 一路查观测质量。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_ILLFL = 8448;

	/// <summary>放大倍率不是正数（取值 8449）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Magnification must be positive</c>。放大率型相机模型（远心常用 mag 表述）里，倍率为零或负都会使物方/像方换算失效，故硬性要求为正。</para>
	///   <para><b>何时遇到</b>多为单位/约定混用：把"1/N"写成了 N 的负倒数、或从镜头铭牌抄数时把符号带错。它与焦距族（<c>Jl_ERR_CAL_ILLFL</c>、<c>Jl_ERR_CAL_NEGFL</c>）分属不同参数模型的同一类下限检查。</para>
	///   <para><b>归类与处置</b>标定族，不小于 1000，两判据均判真。处置：核对该模型倍率定义（像尺寸比物尺寸还是反之 [待实测]）后重填；由标定推出的非正倍率说明拟合失败，回到观测与初值排查。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_ILLMAG = 8449;

	/// <summary>像平面距离（image plane distance）取值非法（取值 8450）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Illegal image plane distance</c>。像平面距离是部分相机模型里主点到像面的距离参数，超出该模型的合法域即报本码；合法域（正负、是否允许零）依模型而定 [待实测]。</para>
	///   <para><b>归类</b>标定族末段"参数下限三连"之一：8448 焦距、8449 倍率、本码像面距离，8450 之后进入标定模型（CM/CSM）族。两判据均判真、统一检查抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>与焦距/倍率同查：单位体系是否一致、是否由不可信的标定结果回填。手工维护参数时优先用生成工具输出整组相机参数，勿单改一个槽位。</para>
	/// </remarks>
	public const int Jl_ERR_CAL_ILLIPD = 8450;

	/// <summary>标定模型还没做优化，内部结果集为空（取值 8451）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>model not optimized yet - no res's</c>（res's 即 results）。多相机/手眼类标定模型是"先攒观测、后一次优化"的两段式：没跑优化就去要结果，模型里没有任何结果记录，即报本码。</para>
	///   <para><b>坑</b>这是时序错误不是数据错误：观测可能全对，只是"取结果"调用早于"算"调用。重放流程时若在异常处理里只重试取结果这一步，会反复撞本码。</para>
	///   <para><b>归类与处置</b>8451 起进入标定模型族（前缀 <c>Jl_ERR_CM_</c>/<c>Jl_ERR_CSM_</c>），不小于 1000，两判据均判真。处置：先调模型的优化/求解步骤再取结果；与 <c>Jl_ERR_CM_NOT_POSTPROCC</c>（优化了但后处理结果缺失）分属两段。托管层未暴露该族算子。</para>
	/// </remarks>
	public const int Jl_ERR_CM_NOT_OPTIMIZED = 8451;

	/// <summary>模型的辅助（后处理）结果不可用（取值 8452）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>auxiliary model results not available</c>。常量名里的 POSTPROCC 指向后处理：优化完成后模型会再产出一层"辅助结果"（精度统计、残差一类），本码指要这类结果时它不存在——要么后处理没跑，要么该配置不产出。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_CM_NOT_OPTIMIZED</c> 是主结果都没有（还没优化），本码是主结果在、辅助结果缺——所以别把流程整个重跑，补后处理这一步即可。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：核对模型创建时是否启用了相应后处理选项；哪些选项控制哪些辅助结果，托管层无文档 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_CM_NOT_POSTPROCC = 8452;

	/// <summary>相机组的搭建不满足"可见互联"条件（取值 8453）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>setup not 'visibly' interconnected</c>，visibly 特意加了引号：多相机模型要求相机两两之间（或通过公共标定对象）存在能支撑相对位姿求解的联合观测；本码指观测图上相机之间断了链。</para>
	///   <para><b>何时遇到</b>典型是每个相机只见过"自己那半边"的标定对象，没有任何一张图/一段观测同时约束两台相机。加一组两台都能看到的观测即可，改优化参数没用。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。与 <c>Jl_ERR_CM_MULTICAM_UNSP</c>（相机类型根本不支持多机）区分：本码是"支持但没连上"。处置后重新优化再验。</para>
	/// </remarks>
	public const int Jl_ERR_CM_NOT_INTERCONN = 8453;

	/// <summary>模型内各处的相机参数互不一致（取值 8454）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>camera parameter mismatch</c>。同一台相机在模型里会以多种形态存在（描述里的参数、观测里携带的参数、序列化回来的副本），本码指这些副本对不上，无法裁定以谁为准。</para>
	///   <para><b>何时遇到</b>高发于手工流程：改了相机参数文件却没重建模型、或把 A 机的观测并入 B 机模型。与 <c>Jl_ERR_CAL_WIMSZ</c>（图像幅面对不上参数）的区别：那个查"图与参数"，本码查"参数与参数"。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：以单一可信源重建——统一参数后重新生成观测条目，不要试图逐条对齐旧数据。相邻的 <c>Jl_ERR_CM_CAMTYP_MISMCH</c> 管类型不一致。</para>
	/// </remarks>
	public const int Jl_ERR_CM_CAMPAR_MISMCH = 8454;

	/// <summary>模型内相机类型不一致（取值 8455）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>camera type mismatch</c>。某些多相机流程要求组内相机同一类型（都面积或都线扫），混装时报本码；比 <c>Jl_ERR_CM_CAMPAR_MISMCH</c>（参数值不一致）更粗粒度——先比类型再比值。</para>
	///   <para><b>坑</b>类型标志跟着参数文件走：换镜头不改类型标志（或反之）会让本码以"看不出哪里不同"的面目出现。核对以每条观测登记的相机类型字段为准。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：把异构相机拆成独立模型分别标定，或确认全部条目指向同一类型后再合并。哪些类型组合被 <c>Jl_ERR_CM_MULTICAM_UNSP</c> 直接拒掉见该码。</para>
	/// </remarks>
	public const int Jl_ERR_CM_CAMTYP_MISMCH = 8455;

	/// <summary>相机类型不被支持（取值 8456）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>camera type not supported</c>。模型只实现了有限的相机类型枚举，遇到不认识或本路径未实现的类型即报本码。支持的类型清单本仓库托管层无文档（<c>JlCamPar</c> 已删除）[待实测]。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_CM_CAMTYP_MISMCH</c> 是"都支持但不统一"，<c>Jl_ERR_CM_UNDEF_CAM_TYP</c>（8481）是类型值根本没定义，本码夹在中间：类型认识但此功能不支持。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：核对类型标志是否填写正确（错标志能把支持机型伪装成不支持）；确属新机型则改走该机型受支持的算子路径。</para>
	/// </remarks>
	public const int Jl_ERR_CM_CAMTYP_UNSUPD = 8456;

	/// <summary>给出的相机索引（ID）无效（取值 8457）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>invalid camera ID</c>。多相机模型按序号引用相机，本码指序号越界或指向未登记的槽位。注意托管层习惯是序号自 0 起而文档示例常按自 1 起叙述，两端各差一时最先撞上本码 [待实测]。</para>
	///   <para><b>坑</b>与 <c>Jl_ERR_CM_UNDEFINED_CAM</c>（8460）不同：本码是"号不对"，8460 是"号对但那条相机本身是空定义"。删过相机后旧索引不会自动平移，缓存的索引要整组重取。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：每次结构变动后重新枚举模型内相机并以新序号引用，勿持久化裸索引到配置文件。</para>
	/// </remarks>
	public const int Jl_ERR_CM_INVALD_CAMIDX = 8457;

	/// <summary>给出的标定对象（cal.obj.）ID 无效（取值 8458）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>invalid cal.obj. ID</c>。标定对象描述在模型里按 ID 登记与引用，本码指 ID 在登记表里不存在。与 <c>Jl_ERR_CM_INVALD_CAMIDX</c>（相机槽位号）对偶，只是引用对象换成了标定对象描述。</para>
	///   <para><b>坑</b>模型族里有三个近亲 ID 码：本码（描述 ID）、<c>Jl_ERR_CM_INVALD_COBJID</c>（对象实例 ID）、<c>Jl_ERR_CM_INVALD_CAMIDX</c>（相机槽位号），日志里常被混称"ID 错"；查错先分清引用的是描述、实例还是相机。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：从模型重新枚举有效 ID 再引用；序列化文件跨环境搬动时 ID 表可能整体重排，旧 ID 一律作废。</para>
	/// </remarks>
	public const int Jl_ERR_CM_INVALD_DESCID = 8458;

	/// <summary>给出的标定对象实例 ID 无效（取值 8459）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>invalid cal.obj. instance ID</c>。同一份标定对象描述可以有多个实例（同板多次摆放各成一个实例），本码指引用的实例 ID 不存在——描述层没错，是实例登记表里查无此号。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_CM_INVALD_DESCID</c>（8458）管描述 ID，本码管实例 ID；重跑观测登记后实例会重建、旧实例 ID 批量失效，这类"整体失效"是本码最常见来源。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：每次重建观测/实例后重新取实例 ID；不要在两处代码里各自缓存实例号。</para>
	/// </remarks>
	public const int Jl_ERR_CM_INVALD_COBJID = 8459;

	/// <summary>引用了一条未定义的相机（取值 8460）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>undefined camera</c>。槽位在枚举里可见但内容为空定义（占位而未填参数），拿去参与投影/优化即报本码。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_CM_INVALD_CAMIDX</c>（8457）是"号出界"，<c>Jl_ERR_CSM_UNINIT_CAM</c>（8477）是装配模型里的相机未初始化，本码是"号在、内容为空"——三者分别指向索引层、初始化层、定义层。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：补全该槽位相机参数或把它从模型里摘除；由描述文件批量生成的模型出现空槽，通常是描述文件条目数与实际相机数不符。</para>
	/// </remarks>
	public const int Jl_ERR_CM_UNDEFINED_CAM = 8460;

	/// <summary>观测序号（index）重复（取值 8461）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>repeated observ. index</c>。模型里的观测按序号寻址且要求唯一，同一序号被登记两次即报本码——是簿记冲突，不是测量问题。</para>
	///   <para><b>何时遇到</b>典型成因：循环里序号没自增、或"追加观测"时从旧集合的最大序号处重新起算导致覆盖式撞号。删观测不会自动补洞，但重复填号会。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：以"现有观测数"为准统一重排序号再登记；与 <c>Jl_ERR_CM_INVAL_OBSERID</c>（8471，观测 ID 非法）区分——那查 ID，本码查 index。</para>
	/// </remarks>
	public const int Jl_ERR_CM_REPEATD_INDEX = 8461;

	/// <summary>引用了未定义的标定对象描述（取值 8462）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>undefined calib. object description</c>。观测登记要指明"看的哪块板"（描述条目），本码指那条描述存在但内容为空定义——相当于引用了一个没填内容的板条目。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_CM_INVALD_DESCID</c>（8458）是 ID 查无此条，本码是条目在而内容空；<c>Jl_ERR_CM_NO_DESCR_FILE</c>（8463）则连文件都读不进。三个码对应"没有号、有号没内容、内容进不来"三层。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：重新加载/登记板描述后再登记观测；批量导入中断最容易留下空描述条目。</para>
	/// </remarks>
	public const int Jl_ERR_CM_UNDEFI_CADESC = 8462;

	/// <summary>标定数据模型文件的格式非法（取值 8463）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Invalid file format for calibration data model</c>。标定数据模型存成专用文件，本码指文件头/结构解析失败——不是"没找到文件"，而是"打开来不像这个格式"。</para>
	///   <para><b>何时遇到</b>三件事最常触发：拿错文件类型（把相机装配模型文件当标定数据模型读，反之报 <c>Jl_ERR_CSM_NO_DESCR_FIL</c> 8468）、文件被文本方式二次保存损坏、跨大版本格式变更（版本可解析但不支持时报 <c>Jl_ERR_CM_WR_DESCR_VERS</c> 8464）。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：用同版本工具重新导出文件；勿手改二进制/内部结构。托管层 <c>JlCalibData</c> 已删除，本码多见于消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_CM_NO_DESCR_FILE = 8463;

	/// <summary>标定数据模型文件的版本不被支持（取值 8464）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The version of the calibration data model is not supported</c>。文件解析出了版本字段，但运行时没有该版本的读入路径——格式对、版本不对。与 <c>Jl_ERR_CM_NO_DESCR_FILE</c>（8463，连格式都不对）分界清晰。</para>
	///   <para><b>坑</b>新旧两个方向都报本码：老文件被新运行时拒（无迁移器）与新文件被老运行时读（版本超前），光看码分不出方向，需看版本号比对 [待实测]。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：统一生成端与运行端版本，或在产线升级前用新版工具重新导出一批模型文件。相机装配模型一侧的对应码是 <c>Jl_ERR_CSM_WR_DESCR_VER</c>（8469）。</para>
	/// </remarks>
	public const int Jl_ERR_CM_WR_DESCR_VERS = 8464;

	/// <summary>线扫相机在该次采集里几乎没有运动（零运动）（取值 8465）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>zero-motion in linear scan camera</c>。线扫建模靠"行与位姿运动"对应，两次采集间编码器/位姿行程近零时，多视图几何退化，模型拒绝摄入这组观测。</para>
	///   <para><b>何时遇到</b>典型是机械手停在近似同一点拍了两帧、或编码器没随传送带走。它与普通"重复观测"不同：不是冗余而是直接把基线长度归零，会拖垮相邻观测的可解性（参 <c>Jl_ERR_CAL_NNEQU</c>）。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：核对触发/编码器接线与步进量设置，丢弃零运动帧后重新登记；勿在模型里硬塞这类观测凑数。</para>
	/// </remarks>
	public const int Jl_ERR_CM_ZERO_MOTION = 8465;

	/// <summary>并非所有相机类型都支持多相机/多标定对象联合模型（取值 8466）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>multi-camera and -calibobj not supported for all camera types</c>。多相机+多标定对象的联合平差只在部分相机类型上实现；对不支持的类型搭这种组网即报本码。支持名单随原生版本变化 [待实测]。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_CM_NOT_INTERCONN</c>（8453）是"组网被支持但你没连上"，本码是"这种类型压根不让组网"——前者补观测可救，后者必须改架构（分开标定再外部拼接）。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：确认组内相机类型；不支持时拆为单机模型分别标定。托管层未暴露该族算子包装。</para>
	/// </remarks>
	public const int Jl_ERR_CM_MULTICAM_UNSP = 8466;

	/// <summary>旧式（legacy）标定流程所需的数据不完整（取值 8467）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>incomplete data, required for legacy calibration</c>。要走兼容旧版的标定路径，观测/参数里必须带齐旧格式要求的全部字段；缺任何一项即报本码。这暗示当前主路径可能不需要这些字段——同一份数据新路径能跑、旧路径报缺。</para>
	///   <para><b>何时遇到</b>多见于拿早期版本存的标定数据重算：新字段自动生成、旧字段无人补。到底缺哪一项消息文本才会指明 [待实测]。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：优先改走现行标定路径；确需兼容旧流程则按 <c>JlNativeApi.GetErrorMessage(err)</c> 指出的缺失项补齐数据。</para>
	/// </remarks>
	public const int Jl_ERR_CM_INCMPLTE_DATA = 8467;

	/// <summary>相机装配模型文件的格式非法（取值 8468）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Invalid file format for camera setup model</c>。前缀 <c>Jl_ERR_CSM_</c> 指相机装配模型（camera setup model，描述"哪几台相机、相对关系如何"的文件），本码指其文件结构解析失败。</para>
	///   <para><b>与相邻码区分</b>标定数据模型一侧的同款码是 <c>Jl_ERR_CM_NO_DESCR_FILE</c>（8463）；两码成对出现往往意味着文件被互相拿错（或后缀改错了）。版本可解析但不支持报 <c>Jl_ERR_CSM_WR_DESCR_VER</c>（8469）。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：核对文件确实是装配模型而非标定数据模型，再用同版本工具重新导出；勿手工编辑内部结构。</para>
	/// </remarks>
	public const int Jl_ERR_CSM_NO_DESCR_FIL = 8468;

	/// <summary>相机装配模型文件的版本不被支持（取值 8469）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The version of the camera setup model is not supported</c>：文件能解析出版本号，但运行时没有对应读入路径。与 <c>Jl_ERR_CSM_NO_DESCR_FIL</c>（8468，格式就不对）分界清晰。</para>
	///   <para><b>坑</b>常量名里的 WR 是 wrong 不是 write——别理解成"写版本失败"。标定数据模型一侧的对应码是 <c>Jl_ERR_CM_WR_DESCR_VERS</c>（8464），两码分开报，先确认拿错的是哪类文件。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：统一导出端与运行端版本；产线混版本环境时以运行时版本为准重建文件。</para>
	/// </remarks>
	public const int Jl_ERR_CSM_WR_DESCR_VER = 8469;

	/// <summary>需要完整的 Vision 标定板描述，而当前拿到的不是（取值 8470）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>full Vision-caltab descr'n required</c>（descr'n 即 description）。某些流程（尤其涉及旧式标定板兼容的路径）要求标定对象描述包含全部字段（标记几何、类型、半径俱全）；简版/裁剪过的描述会被拒。</para>
	///   <para><b>何时遇到</b>常见于自制或转换过的板描述只填了坐标没填标记参数，或用文本编辑器手工精简过描述文件。它属"内容不完整性"码，与读不进文件（8463）不同层级。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：用工具重新生成完整的标定板描述；缺失具体哪项字段以 <c>JlNativeApi.GetErrorMessage(err)</c> 文本为准 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_CM_CALTAB_NOT_AV = 8470;

	/// <summary>给出的观测 ID 无效（取值 8471）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>invalid observation ID</c>。模型对每条观测同时维护"登记序号 index"与"观测 ID"两套引用，本码指按 ID 引用时查无此观测。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_CM_REPEATD_INDEX</c>（8461）管 index 撞号（写入时），本码管 ID 引用失效（读取时）：观测被删除或模型被重建后，旧 ID 即成悬空引用，报本码。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：任何删除/重建操作之后重新枚举观测再取 ID，勿缓存跨生命周期使用；批量处理里以"先枚举后引用"为固定顺序。</para>
	/// </remarks>
	public const int Jl_ERR_CM_INVAL_OBSERID = 8471;

	/// <summary>序列化条目里没有有效的相机装配模型（取值 8472）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Serialized item does not contain a valid camera setup model</c>。模型可存进通用序列化条目再取出，本码指取出来的东西不是一条合法装配模型——对象类型不对、内容损坏或干脆是空的。</para>
	///   <para><b>与相邻码区分</b>同一族三条：<c>Jl_ERR_CM_NOSITEM</c>（8473）是标定数据模型侧，<c>Jl_ERR_CSM_NO_DESCR_FIL</c>（8468）管独立文件的格式，本码管序列化容器里的内容；文件好好的而容器喂错，多半是本码。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：核对写端存的确为装配模型、读端按同型对象取回；托管层序列化路径为 <c>JlSerializationBuffer</c> 一类封装，跨版本搬运最容易造出"合法容器装非法内容"。</para>
	/// </remarks>
	public const int Jl_ERR_CSM_NOSITEM = 8472;

	/// <summary>序列化条目里没有有效的标定数据模型（取值 8473）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Serialized item does not contain a valid calibration data model</c>。从序列化条目反序列化标定数据模型时，内容不是（或不再是）一条合法模型。</para>
	///   <para><b>坑</b>与 <c>Jl_ERR_CSM_NOSITEM</c>（8472，装配模型侧）只差一个前缀 CM/CSM，日志里极易看串；两条分别对应两类模型，先确认代码在恢复哪一类再查原因。名字里的 NOSITEM 可读作 "no (valid) serialized item"。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：确认写读两端类型配对、字节流未被截断（截断时 <c>JlSerializationBuffer</c> 也可能先报自身异常）；不可恢复时用原始模型文件重建。</para>
	/// </remarks>
	public const int Jl_ERR_CM_NOSITEM = 8473;

	/// <summary>给出的工具位姿（tool pose）ID 无效（取值 8474）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Invalid tool pose id</c>。手眼/机器人标定把各末端工具位姿按 ID 登记进模型，本码指引用了未登记的 ID。与 <c>Jl_ERR_CM_UNDEFINED_TOO</c>（8475，ID 在而定义空）成对。</para>
	///   <para><b>何时遇到</b>观测登记时引用了没先注册的工具位姿，或模型重建后旧 ID 悬空。多工具场景里"先注册后引用"的顺序错一位，就会从这条码开始整串报错。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：登记顺序自查（注册工具位姿→登记观测→优化），重建模型后重新获取 ID。托管层 <c>JlPose</c> 仍在，但工具位姿登记算子未暴露。</para>
	/// </remarks>
	public const int Jl_ERR_CM_INV_TOOLPOSID = 8474;

	/// <summary>引用了未定义的工具位姿（取值 8475）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Undefined tool pose</c>（常量名 TOO 是 tool 截断，不是"太多"）。工具位姿条目存在但内容为空定义，参与手眼解算即报本码；与 <c>Jl_ERR_CM_INV_TOOLPOSID</c>（8474，ID 查无）成对——那个缺登记，这个缺内容。</para>
	///   <para><b>坑</b>注册了占位条目（打算后补数值却忘了补）最容易造出这种"空壳"；此时流程不报 8474，直到底层要用数值时才爆本码。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：给每条引用的工具位姿填完整位姿值再解算；<c>JlPose</c> 在托管层可用，可用其构造值逐条核对。</para>
	/// </remarks>
	public const int Jl_ERR_CM_UNDEFINED_TOO = 8475;

	/// <summary>标定数据模型的类型（枚举值）非法（取值 8476）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Invalid calib data model type</c>。标定数据模型自身带有"类型"字段（区分不同标定用途的模型族），本码指该字段值不在合法枚举内——通常是被赋了越界整数或被损坏的数据段污染。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_CM_CAMTYP_UNSUPD</c>（8456）管相机类型，本码管模型类型；都叫"类型不支持"但层级不同，别拿相机枚举表去对模型的类型值。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：不要手工改模型的类型字段，用创建算子重建正确类型；从外部序列化恢复出现本码时按文件损坏处理（同族 8463/8473）。</para>
	/// </remarks>
	public const int Jl_ERR_CM_INVLD_MODL_TY = 8476;

	/// <summary>相机装配模型里含有一台未初始化的相机（取值 8477）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The camera setup model contains an uninitialized camera</c>。装配模型整体能读进来，但其中某台相机的内部状态没建立（结构占了位、初始化步骤没走到），任何用到全组相机的运算都会先被这一条卡住。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_CM_UNDEFINED_CAM</c>（8460）管标定模型里的空定义相机，本码管装配模型的"未初始化"——前者是内容空，后者是状态没建立，来源多为部分失败的加载/克隆。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：重新完整创建装配模型而不是修补单台相机；加载半途抛异常后复用残留对象是本码典型来源，宁弃勿补。</para>
	/// </remarks>
	public const int Jl_ERR_CSM_UNINIT_CAM = 8477;

	/// <summary>手眼标定算法找不到有效解（取值 8478）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>The hand-eye algorithm failed to find a solution.</c>。手眼标定（求相机与机器人末端间的固定变换）对观测有硬性几何要求，本码指算法在给定观测下找不到满足精度要求的解，而非"解得不准"。</para>
	///   <para><b>何时遇到</b>最常见的两类观测缺陷：所有位姿几乎是同一旋转（无旋转激励时手眼方程退化）、或机器人位姿数据与图像观测不同步错帧。增加带明显旋转的姿态覆盖是首选处置。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真、统一检查抛 <c>JlOperatorException</c>。与 <c>Jl_ERR_CM_INVAL_OBS_POSE</c>（8479，单条观测位姿非法）区分：那是数据校验，本码是整组观测在数学上不可解。</para>
	/// </remarks>
	public const int Jl_ERR_CM_NO_VALID_SOL = 8478;

	/// <summary>某条观测携带的位姿非法（取值 8479）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>invalid observation pose</c>。每条观测都带一个位姿（相机或末端在登记时刻的位姿），本码指该位姿本身不成其为位姿——数值非法（NaN、旋转部分不归一）或结构缺项，在登记/校验层就被拒。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_CM_NO_VALID_SOL</c>（8478）是整组观测联合无解（数学层），本码是单条位姿没通过合法性检查（数据层）；后者修掉那条观测即可，前者得改采样方案。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。高发来源：把 4x4 齐次矩阵与轴角/欧拉表达混用、或从机器人接口取数时单位换算（度/毫米）没做。逐条打印旋转/平移分量核对再登记。</para>
	/// </remarks>
	public const int Jl_ERR_CM_INVAL_OBS_POSE = 8479;

	/// <summary>标定对象的位姿数量不足（取值 8480）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>Not enough calibration object poses</c>。求解相机（或手眼）变换对"板出现过多少个不同位姿"有最低要求，登记的位姿条数不够，方程组先天欠定，直接拒算。具体最低条数随模型而定 [待实测]。</para>
	///   <para><b>坑</b>"条数够"不等于"够用"：若多条位姿实际是同一视角的重复拍，数量过线仍会接着撞上 <c>Jl_ERR_CAL_NNEQU</c> 或 <c>Jl_ERR_CAL_NCONV</c>。本码只管数量，不管质量。</para>
	///   <para><b>归类与处置</b>标定模型族，不小于 1000，两判据均判真。处置：增采不同姿态（含倾角、平移覆盖全视场）后重新优化；这是流程性缺料错误，任何参数调优都救不了。</para>
	/// </remarks>
	public const int Jl_ERR_CM_TOO_FEW_POSES = 8480;

	/// <summary>相机类型未定义（取值 8481）。</summary>
	/// <remarks>
	///   <para><b>含义</b>内嵌原生文本为 <c>undefined camera type</c>。相机类型字段根本没被赋值成一个已知的枚举项（区别于"有类型但不被本功能支持"的 <c>Jl_ERR_CM_CAMTYP_UNSUPD</c> 8456 与"类型不统一"的 <c>Jl_ERR_CM_CAMTYP_MISMCH</c> 8455），三条构成"未定义、不支持、不统一"的递进。</para>
	///   <para><b>何时遇到</b>最常见于新建模型忘了设类型、或从空结构序列化再恢复后类型字段丢成默认值；手填参数向量时把类型槽位整个跳过也会出现。</para>
	///   <para><b>归类与处置</b>标定模型族收尾码（8481），不小于 1000，两判据均判真、统一检查抛 <c>JlOperatorException</c>。处置：按所用相机模型显式设置类型枚举后再加载；其后再按 8455/8456 检查一致性与支持性。托管层 <c>JlCamPar</c> 已删除，本码多用于消息表核对。</para>
	/// </remarks>
	public const int Jl_ERR_CM_UNDEF_CAM_TYP = 8481;

	/// <summary>图像对数量与视差值数量不对应。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8482。为立体模型提供的图像对个数与其对应的视差值个数不相等，无法逐一配对。</para>
	///   <para><b>处理</b>使每个图像对都配有相应视差值。属立体模型族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_INVLD_IMG_PAIRS_DISP_VAL = 8482;

	/// <summary>视差的 min/max 取值非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8483。立体模型的 min/max disparity 参数不合法（如 min≥max 或超出可表达范围）。</para>
	///   <para><b>处理</b>确保 min&lt;max 且在有效深度对应区间。属立体模型族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_INVLD_DISP_VAL = 8483;

	/// <summary>尚未通过 set_stereo_model_image_pairs 设定相机对。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8484。立体模型要求先设定参与重建的相机图像对（set_stereo_model_image_pairs），但当前未设定即执行。</para>
	///   <para><b>处理</b>先配置相机对再做视差/重建。属立体模型族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_NO_IM_PAIR = 8484;

	/// <summary>没有可着色的可见重建点。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8485。为点云上色时，没有任何重建点位于可用于取色的相机可见位置，无法赋予颜色。</para>
	///   <para><b>处理</b>确保存在被彩色相机看到的重建点。属立体重建族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_NO_VIS_COLOR = 8485;

	/// <summary>没有任何相机对能重建出点（需检查视差算子参数或包围盒）。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8486。立体重建结果为空：所有相机对都未产生三维点，通常是视差参数或包围盒设置不当致无有效对应。</para>
	///   <para><b>处理</b>放宽视差范围或校正包围盒位置。属立体重建族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_NO_RECONSTRUCT = 8486;

	/// <summary>包围盒划分过细（需调整 'resolution' 参数或包围盒）。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8487。以给定 resolution 体素化包围盒时格子数超过上限，resolution 相对包围盒太小。</para>
	///   <para><b>处理</b>增大 resolution 或缩小包围盒。属立体重建族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_INVLD_BB_PARTITION = 8487;

	/// <summary>binocular_disparity_ms 方法的视差范围非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8488。为 binocular_disparity_ms（多尺度视差）指定的视差搜索范围不合法（min≥max 或超出模型允许区间）。</para>
	///   <para><b>处理</b>给出 min&lt;max 且在有效深度范围内的视差区间。属双目族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_INVLD_DISP_RANGE = 8488;

	/// <summary>双目（binocular）算子参数非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8489。调用 binocular_* 族算子时给出的参数取值不合法。</para>
	///   <para><b>处理</b>对照该算子参数取值域校正。属双目/立体族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_INVLD_BIN_PAR = 8489;

	/// <summary>立体模型类型非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8490。为 stereo model 指定了不受支持的模型类型（如同步/异步、线激光面结构光等类别不匹配）。</para>
	///   <para><b>处理</b>使用文档允许的模型类型。属立体模型族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_INVLD_MODL_TY = 8490;

	/// <summary>立体模型未处于持久（persistent）模式。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8491。请求只在持久模式下才支持的操作，而当前 stereo model 未以 persistent 模式创建。</para>
	///   <para><b>处理</b>以 persistent 模式重建模型。属立体模型族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_NOT_PERSISTEN = 8491;

	/// <summary>包围盒（bounding box）非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8492。立体模型相关算子收到非法包围盒（尺寸为负/零、坐标越界或未初始化）。</para>
	///   <para><b>处理</b>给出坐标有序、尺寸为正且落在有效范围内的包围盒。属立体模型族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_INVLD_BOU_BOX = 8492;

	/// <summary>立体重建：图像尺寸必须与相机设置匹配。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8493。做立体重建时输入图像尺寸与相机设置（camera setup）所要求的尺寸不一致，投影关系无法建立。</para>
	///   <para><b>处理</b>使用与相机标定匹配的图像尺寸。属立体重建族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SR_INVLD_IMG_SIZ = 8493;

	/// <summary>包围盒位于基线（basis line）之后。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8494。双目重建时给定的包围盒处于相机基线后方（视差不成立），无法进行三维重建。</para>
	///   <para><b>处理</b>把包围盒置于两相机视场前方可见区。属双目立体重建族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SR_BBOX_BHND_CAM = 8494;

	/// <summary>标定存在歧义：需用更佳的输入数据重新标定。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8495。标定出现多解/病态，输入观测不足以唯一确定参数（如位姿过于集中或退化）。</para>
	///   <para><b>处理</b>增加姿态多样性、改善覆盖后重标。属相机标定族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_CAL_AMBIGUOUS = 8495;

	/// <summary>未能确定标定板（描述）的位姿。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8496。标定中无法从观测反求标定板/标定描述相对于相机的位姿（find_calib_object 类失败）。</para>
	///   <para><b>处理</b>改善标定板可见性、对比度与拍摄质量。属相机标定族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_CAL_PCPND = 8496;

	/// <summary>标定失败：需检查输入数据后重新标定。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8497。标定求解未收敛或结果不可信而整体失败。多因观测质量差、位姿覆盖不足或输入数据有误。</para>
	///   <para><b>处理</b>核查并改善标定输入后重试。属相机标定族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_CAL_FAILED = 8497;

	/// <summary>未提供任何观测数据。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8498。执行标定时观测集合为空，缺少用于求解内/外参的位姿-图像对应。</para>
	///   <para><b>处理</b>先添加有效观测再标定。属相机标定族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_CAL_MISSING_DATA = 8498;

	/// <summary>相机数不足四个时，标定物必须至少被每个相机看到一次。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8499。多相机标定中使用的相机少于 4 台，但存在某个相机从未观测到标定物，导致其外参无从求解。</para>
	///   <para><b>处理</b>补齐每个相机对标定物的观测，或改用不少于 4 相机的观测约束。属相机标定/多相机设置族。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_CAL_FEWER_FOUR = 8499;

	/// <summary>模板（template）文件格式无效。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8500。读取灰度模板文件时其格式非法（非模板文件、损坏或截断）。</para>
	///   <para><b>处理</b>确认文件完整且确为模板。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_NOAP = 8500;

	/// <summary>模板（template）版本不受支持。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8501。加载灰度模板文件时其版本号与本运行时不兼容。</para>
	///   <para><b>处理</b>用匹配版本重新生成模板。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_WPFV = 8501;

	/// <summary>模板点数过少。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8506。训练灰度模板时可用于匹配的采样点太少，无法建立可靠模板。</para>
	///   <para><b>处理</b>增大模板 ROI/降低使点变少的参数。（与 8510 面向形状模板的同类错误不同。）[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_NGTPTS = 8506;

	/// <summary>模板数据仅 Vision XL（完整版许可）可读。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8507。加载灰度模板（grayscale template）数据时许可等级不足，仅 XL 许可可读。</para>
	///   <para><b>处理</b>需升级到 XL 许可；该分级在本运行时是否生效未知。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_PDTL = 8507;

	/// <summary>序列化数据中不含有效的 NCC 模板。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8508。加载 NCC（归一化互相关）模板文件时，反序列化结果不是合法的 NCC model（文件为其它对象、损坏或版本不符）。</para>
	///   <para><b>处理</b>确认文件确为 NCC 模板且完整。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_NCC_NOSITEM = 8508;

	/// <summary>形状模板的点数过少。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8510。训练形状模板时采样到的轮廓点数量太少，不足以形成稳定可匹配的模板。</para>
	///   <para><b>处理</b>放宽采样步长/增大轮廓规模或降低 num_level 等使点变少的参数。（与 8506 面向通用模板的同类错误不同。）[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_NTPTS = 8510;

	/// <summary>灰度形状模板与彩色形状模板被混用。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8511。在一次操作中同时使用了基于灰度和基于彩色的形状模板，二者不可混合。</para>
	///   <para><b>处理</b>同一批次/同一匹配流程只使用同一类型（灰度或彩色）的模板。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_CGSMM = 8511;

	/// <summary>形状模板数据仅 Vision XL（完整版许可）可读。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8512。加载 shape model 数据时许可等级不足，仅 XL 许可可读。</para>
	///   <para><b>处理</b>需升级到 XL 许可；该分级在本运行时是否生效未知。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SMTL = 8512;

	/// <summary>形状模板并非由 XLD 创建。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8513。某操作要求形状模板是以 XLD 轮廓训练的（create_..._xld 途径），而目标模板并非如此创建，来源不符。</para>
	///   <para><b>处理</b>改用基于 XLD 生成的模板，或换用适配常规模板的接口。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SMNXLD = 8513;

	/// <summary>序列化数据中不含有效的形状模板。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8514。加载形状模板（shape model）文件时，反序列化结果不是合法 shape model（文件为其它对象、损坏或版本不符）。</para>
	///   <para><b>处理</b>确认文件确为形状模板且完整可反序列化。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_NOSITEM = 8514;

	/// <summary>形状模板轮廓离干扰区域过近。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8515。训练形状模板时，其轮廓与指定的 clutter 区域距离太近而重叠，无法可靠区分目标与干扰。</para>
	///   <para><b>处理</b>增大轮廓与干扰区域的间距，或缩小干扰区域。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_CL_CONT = 8515;

	/// <summary>形状模板不含干扰（clutter）参数。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8516。对一个未启用/未定义 clutter 参数的形状模板请求干扰相关信息，模板内并不存在这些参数。</para>
	///   <para><b>处理</b>仅在训练时提供了干扰区域的模板上访问 clutter 属性。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_NO_CLUT = 8516;

	/// <summary>被组合的形状模板干扰类型不一致。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8517。在合并/联合训练多个形状模板时，它们的 clutter 类型不统一，无法构成同一模板集。</para>
	///   <para><b>处理</b>确保参与组合的模板使用相同干扰类型。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_SAME_CL = 8517;

	/// <summary>形状模板的干扰对比度（clutter contrast）取值非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8518。带干扰区域训练的形状模板，其 clutter contrast 参数超出合法范围。</para>
	///   <para><b>处理</b>使用文档允许的对比度取值。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_WRONG_CLCO = 8518;

	/// <summary>干扰区域（clutter region）含有负坐标。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8519。为形状模板指定 clutter（干扰）区域时，区域内出现负的 row/column 坐标，非法。</para>
	///   <para><b>处理</b>构造干扰区域前确保坐标非负且在图像范围内。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_SM_CL_NEG = 8519;

	/// <summary>盒状物查找器收到不受支持的通用参数。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8520。box finder 算子收到了其不支持的 generic（通用）参数名或取值。</para>
	///   <para><b>处理</b>核对该算子参数白名单，去除或改正非法通用参数。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_FIND_BOX_UNSUP_GENPARAM = 8520;

	/// <summary>初始分量的区域类型不一致。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8530。构建组件模型时传入的若干初始分量区域类型不统一（如区域与 XLD/灰度分量混合），无法作为同一集合训练。</para>
	///   <para><b>处理</b>确保所有初始分量使用一致的区域类型。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_COMP_DRT = 8530;

	/// <summary>无法消解歧义匹配（ambiguous matches）。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8531。组件匹配阶段存在多解且算法无法唯一确定对应关系，求解歧义匹配失败。</para>
	///   <para><b>处理</b>提高模型区分度或收紧匹配约束，减少候选歧义。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_COMP_SAMF = 8531;

	/// <summary>不完全 Gamma 函数迭代未收敛。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8532。数值库计算不完全 Gamma 函数（IGF）时迭代未在允许次数内收敛，通常因输入参数极端（过大/过小或非法）所致。</para>
	///   <para><b>处理</b>检查触发它的统计/阈值参数取值范围。具体算子无法由常量签名判定。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_IGF_NC = 8532;

	/// <summary>计算最小树形图（minimum spanning arborescence）时节点数超限。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8533。组件匹配在求解最小树形图（MSA）时输入的节点数超过算法可处理上限。</para>
	///   <para><b>处理</b>减少参与匹配的候选分量/节点数量或收紧匹配参数。具体调用点无法由常量判定。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_MSA_TMN = 8533;

	/// <summary>组件训练数据仅 Vision XL（完整版许可）可读。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8534。加载 component model 的训练数据时许可等级不足，仅 XL 许可可读（区别于 8535 的最终模型数据）。</para>
	///   <para><b>处理</b>需升级到 XL 许可。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_CTTL = 8534;

	/// <summary>组件模型数据仅 Vision XL（完整版许可）可读。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8535。加载 component model 数据时，当前许可等级不足，只有 Vision XL 许可才能读取。</para>
	///   <para><b>处理</b>需升级到 XL 许可。该许可分级在本运行时是否生效未知。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_CMTL = 8535;

	/// <summary>序列化数据中不含有效的组件模型。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8536。加载 component model 时，反序列化结果不是合法组件模型（文件为其它对象类型、损坏或格式版本不匹配）。</para>
	///   <para><b>处理</b>确认文件确为组件模型且完整。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_COMP_NOSITEM = 8536;

	/// <summary>序列化数据中不含有效的组件训练结果。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8537。读入 component model 的训练中间结果时，反序列化对象不是合法的"组件训练结果"（区别于 8536 的最终模型）。</para>
	///   <para><b>常见诱因</b>误把最终模型当训练结果加载，或文件损坏/版本不符。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_TRAIN_COMP_NOSITEM = 8537;

	/// <summary>训练图像尺寸与变化模型登记尺寸不一致。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8540。向已建的 variation model 追加训练图像时，该图像宽高与模型初始化时确定的尺寸不符，逐像素统计无法对齐。</para>
	///   <para><b>处理</b>训练/检测所用图像须与模型建立时的尺寸一致。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_VARIATION_WS = 8540;

	/// <summary>变化模型尚未为分割做准备。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8541。variation model 在训练后未执行"为分割做准备"的收尾步骤，即被用于比较/局部缺陷提取。</para>
	///   <para><b>处理</b>训练完成后须先调用 prepare 步骤（准备分割）再做检测。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_VARIATION_PREP = 8541;

	/// <summary>变化模型的训练模式参数非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8542。为 variation model 指定了不受支持的训练模式取值（train_variation_model 的模式参数不在合法集合内）。</para>
	///   <para><b>处理</b>使用文档允许的合法训练模式字符串。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_VARIATION_WRMD = 8542;

	/// <summary>变化模型（variation model）文件格式无效。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8543。读取 variation model 文件时其格式非法（非模型文件、损坏或截断）。</para>
	///   <para><b>处理</b>确认文件来源完整，必要时重新导出。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_VARIATION_NOVF = 8543;

	/// <summary>变化模型（variation model）的数据版本不受支持。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8544。加载 variation model 时其格式版本号与本运行时不兼容。</para>
	///   <para><b>处理</b>用版本匹配的工具重新生成模型，或升级到可识别该版本的运行时。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_VARIATION_WVFV = 8544;

	/// <summary>变化模型的训练数据已被清除。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8545。variation model 的累积训练数据被清空后，仍尝试基于其做分割准备或比较，缺少可用于建统计模型的数据。</para>
	///   <para><b>处理</b>重新喂入训练图像累积样本后再调用后续步骤。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_VARIATION_TRDC = 8545;

	/// <summary>序列化数据中不含有效的变化模型（variation model）。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8546。从文件/序列化流读入"变化模型"（用于缺陷/差异检测的训练参考模型）时，反序列化结果并非一个合法的 variation model。</para>
	///   <para><b>常见诱因</b>文件被其它类型模型覆盖、内容损坏或版本不匹配。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_VARIATION_NOSITEM = 8546;

	/// <summary>已无可用空闲的测量卡尺（measure object）句柄。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8550。原生侧可分配的 Measure 对象数量已达上限，再申请新对象即失败。</para>
	///   <para><b>处理</b>复用已有对象，或对用完的对象及时释放以归还额度。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_MEASURE_NA = 8550;

	/// <summary>测量卡尺（measure object）尚未初始化。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8551。Measure 对象虽已存在但未完成初始化（未配置卡尺几何/图像通道），即被用于提取剖面或边缘。</para>
	///   <para><b>处理</b>在查询前须先按尺寸与卡尺参数完成初始化。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_MEASURE_NI = 8551;

	/// <summary>测量卡尺（measure object）无效/越界（名称 OOR 表示 out of range）。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8552。Measure 对象处于非法状态或其索引/参数越界，导致卡尺无法定位到有效剖面。</para>
	///   <para><b>处理</b>核对查询的卡尺序号与轮廓点索引是否落在对象实际配置范围内。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_MEASURE_OOR = 8552;

	/// <summary>传入的测量卡尺（measure object）句柄为空（NULL）。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8553。对 Measure 对象调用操作时，其句柄实际是空指针——对象从未成功创建，或已被释放后仍被引用。</para>
	///   <para><b>常见诱因</b>创建失败后未判空即使用，或对象已 Dispose 仍继续调用。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_MEASURE_IS = 8553;

	/// <summary>测量卡尺（measure object）所绑定图像的宽高与其创建时尺寸不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8554。用已生成的 Measure 对象去执行卡尺/剖面提取时，输入图像尺寸与模型初始化时登记的尺寸不一致，卡尺坐标将无法映射。</para>
	///   <para><b>处理</b>确保喂给该对象的图像与建模型时同尺寸；尺寸会变时应按新尺寸重建 Measure 对象。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_MEASURE_WS = 8554;

	/// <summary>测量卡尺（measure object）模型文件格式无效，无法读取。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8555。加载 Measure 对象模型文件时，文件内容不符合预期的模型格式（非模型文件、被截断或已损坏）。</para>
	///   <para><b>常见诱因与处理</b>路径指向了错误文件、导出中断产生半截文件、或用了非本库写出的文件；应重新完整导出模型。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_MEASURE_NO_MODEL_FILE = 8555;

	/// <summary>测量卡尺（measure object）数据版本不受当前运行时支持。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8556。从文件反序列化 Measure 对象时，其内嵌数据格式版本号高于或不同于本运行时可识别的版本。</para>
	///   <para><b>常见诱因与处理</b>多为用新版本工具生成的 .shm/模型在旧版运行时加载所致；需以匹配版本重新生成或用兼容工具读入。触发它的原生算子无法由常量签名判定。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_MEASURE_WRONG_VERSION = 8556;

	/// <summary>卡尺（measure object）序列化数据只能在 Vision XL 版本下读取。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8557。反序列化/读取 Measure 对象数据时，当前部署的运行版本不具备 XL 能力，原生侧按版本门槛直接拒绝载入；这道校验在原生层，C# 包装只是透传结果。</para>
	///   <para><b>常见诱因与处理</b>把 XL 版生成的模型数据拿到基础版运行时读取，或换机部署后许可证降级；需对齐版本，或在目标版本下重新生成数据。版本能力的具体判定依据 [待实测]</para>
	/// </remarks>
	public const int Jl_ERR_MEASURE_TL = 8557;

	/// <summary>序列化字节流里不含有效的卡尺（measure object）对象。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8558。JlMeasure 实现 ISerializable；反序列化时字节流容器结构合法，但其内承载的不是一个有效的 measure 对象——要么类型不对（把别的对象系的序列化数据按 JlMeasure 反序列化），要么对象体残缺。</para>
	///   <para><b>常见诱因与处理</b>序列化数据存取时张冠李戴、数据被截断或中途覆写；核对数据来源与目标类型是否配对，必要时重新导出。[待实测]</para>
	/// </remarks>
	public const int Jl_ERR_MEASURE_NOSITEM = 8558;

	/// <summary>几何测量（metrology）模型尚未初始化。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8570。模型未经 CreateMetrologyModel 或 ReadMetrologyModel 成功建立就被使用——例如新建 JlMetrologyModel 后还没创建就调用 ApplyMetrologyModel、AddMetrologyObject*Measure 等。</para>
	///   <para><b>处理</b>任何操作前先创建模型；读文件失败后句柄仍处于未初始化态，不可继续复用。具体哪些原生算子会抛此码 [待实测]</para>
	/// </remarks>
	public const int Jl_ERR_METROLOGY_MODEL_NI = 8570;

	/// <summary>引用的 metrology 对象序号无效（模型内不存在该对象）。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8572。Set/GetMetrologyObjectParam、Set/GetMetrologyObjectFuzzyParam、ClearMetrologyObject 等按 index 寻址的调用里，序号在模型中无对应对象：多为 ClearMetrologyObject/ClearMetrologyModel 之后仍拿旧序号操作，或用了 AddMetrologyObject* 从未返回过的序号。</para>
	///   <para><b>处理</b>序号以 Add 系列的返回值为准，不要假定从 0 起连续；操作前用 GetMetrologyObjectIndices 核实当前有效序号集。</para>
	/// </remarks>
	public const int Jl_ERR_METROLOGY_OBJECT_INVALID = 8572;

	/// <summary>有效测量点太少，无法拟合该 metrology 几何对象。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8573。ApplyMetrologyModel 拟合阶段，某对象的卡尺提取到的边缘点数量或分布不足以解出几何参数（直线、圆、椭圆、矩形各有最低点数要求，由原生侧掌握 [待实测]）；点数够但全部挤在一小段弧/短边上同样会退化失败。</para>
	///   <para><b>常见诱因与处理</b>measureThreshold 设得过高、卡尺没跨过真实边缘、图像对比度不足或工件缺失；先降阈值、加密/加长卡尺，再用 GetMetrologyObjectMeasures 检查实际提取到的轮廓点分布。</para>
	/// </remarks>
	public const int Jl_ERR_METROLOGY_FIT_NOT_ENOUGH_MEASURES = 8573;

	/// <summary>metrology 模型文件格式无效，无法读取。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8575。ReadMetrologyModel 的目标文件不符合模型格式预期：路径指错、文件被截断（导出不完整）、或根本不是本库写出的模型文件。</para>
	///   <para><b>处理</b>核对文件来源与完整性；用 WriteMetrologyModel 重新完整导出一份再读，勿手工改后缀名。</para>
	/// </remarks>
	public const int Jl_ERR_METROLOGY_NO_MODEL_FILE = 8575;

	/// <summary>metrology 模型文件的格式版本不受当前运行时支持。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8576。ReadMetrologyModel/DeserializeMetrologyModel 读入的文件内嵌版本号高于或不同于本运行时可识别的版本；版本门槛校验在原生侧。</para>
	///   <para><b>处理</b>用匹配版本的运行时重新导出模型，或升级运行时到生成该文件的同代版本；不存在向下兼容转档。</para>
	/// </remarks>
	public const int Jl_ERR_METROLOGY_WRONG_VERSION = 8576;

	/// <summary>metrology 对象的模糊（fuzzy）隶属函数未设置。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8577。取拟合结果依赖的隶属度（fuzzy）参数没有生效——fuzzy 参数用于给每个测量点分配权重后做稳健拟合。新模型通常自带默认值，调用过 ResetMetrologyObjectFuzzyParam 或异常配置路径后可能变成未设置态。精确触发条件 [待实测]</para>
	///   <para><b>处理</b>用 SetMetrologyObjectFuzzyParam 重设该对象的 fuzzy 参数，或重建模型。</para>
	/// </remarks>
	public const int Jl_ERR_METROLOGY_NO_FUZZY_FUNC = 8577;

	/// <summary>序列化字节流里不含有效的 metrology 模型。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8578。DeserializeMetrologyModel 的输入字节流容器合法、但承载的不是 metrology 模型数据：常见于把其他对象系的 Serialize 结果喂给 metrology 反序列化，或流被截断/覆写。</para>
	///   <para><b>处理</b>确保字节流出自 SerializeMetrologyModel 且类型配对。</para>
	/// </remarks>
	public const int Jl_ERR_METROLOGY_NOSITEM = 8578;

	/// <summary>metrology 模型的相机参数未设置。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8579。模型输出世界坐标/物理量结果时需要相机参数把像素观测换算到世界系；缺参数时原生侧拒绝换算。本库 C# 层的相机参数类型包装已删除，该配置在原生/配置侧完成，具体设置通路 [待实测]</para>
	///   <para><b>处理</b>仅在像素域使用结果可避开此码；需要物理量输出时先补齐相机参数。</para>
	/// </remarks>
	public const int Jl_ERR_METROLOGY_UNDEF_CAMPAR = 8579;

	/// <summary>测量平面的位姿（pose）未设置。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8580。与 8579 同族：把图像观测映射到世界测量平面时缺少平面位姿。位姿约定为行/列/旋转角表达（角度弧度制），只影响世界坐标输出路径，纯像素域拟合不受累。确切触发调用链 [待实测]</para>
	///   <para><b>处理</b>需要平面坐标结果时先设定位姿；或把结果解释停留在像素坐标系。</para>
	/// </remarks>
	public const int Jl_ERR_METROLOGY_UNDEF_POSE = 8580;

	/// <summary>模型已加入几何对象后，不允许再设置其 mode 类参数。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8581。metrology 模型的某个"模式"类参数只在添加第一个对象之前可设：对象一旦经 AddMetrologyObject*Measure 建立，其测量方式随之固化，事后再改会被拒绝。具体对应哪个参数名 [待实测]</para>
	///   <para><b>处理</b>把 mode 设置移到 Add 之前；若已加对象，则 ClearMetrologyModel 后重建。</para>
	/// </remarks>
	public const int Jl_ERR_METROLOGY_SET_MODE = 8581;

	/// <summary>对象世界位姿被多次设置后，原算子不再被允许调用。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8582。对 metrology 对象的世界位姿（AlignMetrologyModel/TransformMetrologyObject 一类）重复设置后，依赖初始位姿假设的某个算子即失效被拒。原文未指明具体哪个算子 [待实测]</para>
	///   <para><b>处理</b>一个模型的位姿对齐只做一次；需要改变换关系时重建模型，而不是反复叠加。</para>
	/// </remarks>
	public const int Jl_ERR_METROLOGY_OP_NOT_ALLOWED = 8582;

	/// <summary>同一 metrology 模型内所有对象必须共享相同的世界位姿与相机参数。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8583。一个模型是一个统一的测量坐标系；当新添加/复制进来的对象带入了与已有对象不同的位姿或相机参数时，无法归并进同一模型，直接报错而非静默覆盖。</para>
	///   <para><b>处理</b>按"位姿+相机参数"组合拆分模型，各建各的，结果在上层汇总。</para>
	/// </remarks>
	public const int Jl_ERR_METROLOGY_MULTI_POSE_CAM_PAR = 8583;

	/// <summary>模型登记的输入类型与实际调用的输入类型不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8584。metrology 模型在登记时确定了期望的输入形态（如 SetMetrologyModelImageSize 固定的图像尺寸域，或图像输入与轮廓输入之别），后续用另一种输入形态驱动它即报错。模型支持的输入模式取值 [待实测]</para>
	///   <para><b>处理</b>核对建模型时登记的输入形态，保证 Apply/取结果路径与之一致。</para>
	/// </remarks>
	public const int Jl_ERR_METROLOGY_WRONG_INPUT_MODE = 8584;

	/// <summary>原生动态库（DLL）加载失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8600。运行时打不开所需原生库：文件缺失、32/64 位架构不匹配、依赖 DLL 不在搜索路径、或被安全策略拦截。</para>
	///   <para><b>处理</b>属部署环境问题而非调用参数问题：核对安装目录完整性与 PATH/程序目录，确保整套原生库同架构同版本。</para>
	/// </remarks>
	public const int Jl_ERR_DLOPEN = 8600;

	/// <summary>原生动态库关闭失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8601。卸载动态库被系统拒绝——通常因为库内仍有活动句柄或后台线程在使用它。</para>
	///   <para><b>处理</b>退出前先释放全部对象、停掉工作线程；若仍复现，交由进程退出整体回收即可，不必反复重试。具体触发时机 [待实测]</para>
	/// </remarks>
	public const int Jl_ERR_DLCLOSE = 8601;

	/// <summary>在动态库中找不到指定符号（函数入口）。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8602。已加载的库里没有运行时要找的导出符号：多为原生库版本与管理层不匹配（函数改名/移除），或该库本就是裁剪过的功能集。</para>
	///   <para><b>处理</b>保证 JLVisionLib 程序集与原生 DLL 同包同版本部署，勿混装不同代产物。</para>
	/// </remarks>
	public const int Jl_ERR_DLLOOKUP = 8602;

	/// <summary>接口库不可用（所需功能组件未安装）。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8603。当前调用需要的功能组件没有安装或不在部署包内（原文 "Interface library not available"）——与 8600 的"库文件加载失败"不同，这里是该组件根本未随部署提供。</para>
	///   <para><b>处理</b>核对发行版本的功能模块清单，补装缺失组件或改用已授权的能力路径。</para>
	/// </remarks>
	public const int Jl_ERR_COMPONENT_NOT_INSTALLED = 8603;

	/// <summary>径向畸变标定信息不足。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8650。做径向畸变标定时可求解的观测信息不够（有效图像点太少、分布集中在视场一角、或特征提取失败），方程组欠定，无法稳定解出畸变参数。</para>
	///   <para><b>处理</b>增加标定图数量并让标定点覆盖全视场（尤其边缘区域，径向畸变在视场边缘最敏感）；本库 C# 层对应的标定算子族 [待实测]</para>
	/// </remarks>
	public const int Jl_ERR_EAD_CAL_NII = 8650;

	/// <summary>形状模型（文件）的格式版本不受支持。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8670。ReadShapeModel/DeserializeShapeModel 读入的形状模型数据内嵌版本超出本运行时可识别范围。缩写 WGSMFV 对应原文 "version of the shape model result"。</para>
	///   <para><b>处理</b>在版本匹配的运行时上重建模型再存盘；跨代旧文件不保证向前兼容。</para>
	/// </remarks>
	public const int Jl_ERR_WGSMFV = 8670;

	/// <summary>搜索时的尺度限制落在模型训练出的尺度范围之外。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8671。FindScaledShapeModel(s)/FindAnisoShapeModel(s) 传入的尺度区间与模型训练时的 [scaleMin, scaleMax] 不相容（尺度是无量纲倍率，1.0 为原始大小）；未训练尺度的模型也不接受尺度限制。</para>
	///   <para><b>处理</b>先 GetShapeModelParams 读出训练范围，把搜索区间收窄进去；确需更大范围时重建模型放大尺度训练区间。</para>
	/// </remarks>
	public const int Jl_ERR_GSM_INVALID_RES_SCALE = 8671;

	/// <summary>搜索角度区间超出模型训练出的角度范围。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8672。Find 系列传入的 angleStart+angleExtent 落在训练角域之外（弧度制；angleExtent 可为负表示反向取角）。训练范围外的角度不能靠外推命中。</para>
	///   <para><b>处理</b>用 GetShapeModelParams 核对训练角度域；需要全向搜索时在训练侧给足 angleExtent（如 ±180° 对应 π 的 extent）重建模型。</para>
	/// </remarks>
	public const int Jl_ERR_GSM_INVALID_ANGLE = 8672;

	/// <summary>形状模型尚未训练就被搜索使用。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8673。该 JlShapeModel 未成功执行 Create*ShapeModel 训练、也没从文件载入模型数据，就被送进 Find 系列——训练是"建出对象"与"能搜"之间的必经步骤。</para>
	///   <para><b>处理</b>确保训练调用成功返回后再用；训练失败或被 ClearShapeModel 后不可复用句柄。</para>
	/// </remarks>
	public const int Jl_ERR_GSM_NEEDS_TRAINING = 8673;

	/// <summary>迟滞高阈值 contrast_high 被设得低于 contrast_low。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8674。训练形状模型用迟滞双阈值提取边缘（两值均为灰阶级差），必须满足 contrast_low 不大于 contrast_high；设反了边缘判据自相矛盾。</para>
	///   <para><b>处理</b>交换两值；若传 'auto' 交给自动推导则不会触发此码。</para>
	/// </remarks>
	public const int Jl_ERR_GSM_CONTRAST_HYS = 8674;

	/// <summary>min_contrast 高于 contrast_low 或 contrast_high。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8675。迟滞边缘提取里 min_contrast 是最终保留边缘的最低幅值门槛（灰阶），必须不大于两个迟滞阈值，否则任何边都到不了"强边缘"判据，模型会退化到没有点。</para>
	///   <para><b>处理</b>保证三者排序 min_contrast 不大于 contrast_low 且不大于 contrast_high；弱对比模板可整体下调。</para>
	/// </remarks>
	public const int Jl_ERR_GSM_CONTRAST_MIN_CONTRAST = 8675;

	/// <summary>各向同性缩放训练区间倒挂：iso_scale_max 低于 iso_scale_min。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8676。建带尺度形状模型时给定的等向尺度对（无量纲倍率）不满足 min 不大于 max，训练无从采样。</para>
	///   <para><b>处理</b>校正为合法区间，并让区间覆盖 1.0（原始大小）以内实际需求；同时预留搜索侧限制值落在其中（见 8671）。</para>
	/// </remarks>
	public const int Jl_ERR_GSM_ISO_SCALE_PAIR = 8676;

	/// <summary>各向异性训练中行方向尺度区间倒挂：scale_row_max 低于 scale_row_min。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8677。CreateAnisoShapeModel 的行方向尺度对（row 即图像 y 方向的独立倍率）不满足 min 不大于 max。</para>
	///   <para><b>处理</b>校正行向对；顺手同轮核对列向对（8678），避免修一个再撞另一个。</para>
	/// </remarks>
	public const int Jl_ERR_GSM_ANISO_SCALE_ROW = 8677;

	/// <summary>各向异性训练中列方向尺度区间倒挂：scale_column_max 低于 scale_column_min。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8678。CreateAnisoShapeModel 的列方向尺度对（column 即图像 x 方向的独立倍率）不满足 min 不大于 max；与 8677 同族，两对独立校验。</para>
	///   <para><b>处理</b>校正列向对；若目标只在单一方向伸缩，把另一方向对固定为 1.0 两侧的小区间更稳。</para>
	/// </remarks>
	public const int Jl_ERR_GSM_ANISO_SCALE_COLUMN = 8678;

	/// <summary>需要各向同性缩放的流程里尺度参数未设置。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8679。在要求等向尺度的创建/查询路径上缺少 iso 尺度对（min/max）：比如以带尺度名义操作却拿不出尺度设定。已训练好的模型能否经通用参数补设尺度 [待实测]</para>
	///   <para><b>处理</b>需要等向尺度时用 CreateScaledShapeModel* 并完整给 scaleMin/scaleMax。</para>
	/// </remarks>
	public const int Jl_ERR_GSM_ISO_NOT_SET = 8679;

	/// <summary>需要各向异性缩放的流程里尺度参数未设置。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8680。与 8679 同族但面向各向异性：行、列两对尺度参数必须齐备，缺任一对即视为未设置。</para>
	///   <para><b>处理</b>用 CreateAnisoShapeModel* 一次给全 scaleR/scaleC 的 min/max（必要时含 step）。</para>
	/// </remarks>
	public const int Jl_ERR_GSM_ANISO_NOT_SET = 8680;

	/// <summary>缺少边缘方向信息，无法改写形状匹配 metric。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8681。SetShapeModelMetric 需按边缘方向向量把模型点集几何换算后再切极性策略（如 use_polarity 与忽略极性之间）；训练源信息不足以保留方向（部分 XLD 来源或被裁剪优化的模型）时无从换算。</para>
	///   <para><b>处理</b>训练时就直接给所需 metric；事后改不动就回到模板图像重训。哪些 optimization 档位会丢失方向 [待实测]</para>
	/// </remarks>
	public const int Jl_ERR_GSM_INVALID_METRIC_XLD = 8681;

	/// <summary>标识符（identifier）相同的形状模型不允许同时被搜索。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8682。多个形状模型共用同一 identifier 时并行/合并搜索无法把命中唯一归属到某个模型，结果表义被破坏，故直接禁止。identifier 在本库由哪个接口设定 [待实测]</para>
	///   <para><b>处理</b>为每个模型分配互不相同的 identifier；或退而求其次逐个搜索。</para>
	/// </remarks>
	public const int Jl_ERR_GSM_SAME_IDENTIFIER = 8682;

	/// <summary>所设参数与"按金字塔层级"的取值个数不一致。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8683。min_contrast 一类"每层一个值"的参数要么给一个全局标量、要么给与 num_levels 等长的序列；Set 时序列长度与模型实际层数对不上即报此码。'auto' 展开出的真实层数/每层值以 GetShapeModelParams 为准。</para>
	///   <para><b>处理</b>先读出实际层数再按层供值；不确定就只给单值。</para>
	/// </remarks>
	public const int Jl_ERR_SM_INCONSISTENT_PER_LEVEL = 8683;

	/// <summary>基于样本的训练失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8684。用样本图（而非单张模板）训练模型时未能成功：样本不足、样本间形变互相矛盾、或对比度太低提不出稳定边缘。</para>
	///   <para><b>处理</b>补充覆盖实际形变域的样本并保证标注一致；本库 C# 层的样本训练入口 [待实测]</para>
	/// </remarks>
	public const int Jl_ERR_GSM_SAMPLE_TRAINING = 8684;

	/// <summary>当前模型配置不支持计算逐模型点得分。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8685。"模型点得分"（每个模型点在匹配中的贡献度）要求训练时保留逐点统计信息；默认或裁剪训练出的模型没有这些信息，请求即被拒。开启该能力所需的确切训练参数 [待实测]</para>
	///   <para><b>处理</b>需要点得分时以兼容配置重训，别指望对既有模型补算。</para>
	/// </remarks>
	public const int Jl_ERR_GSM_POINT_SCORES = 8685;

	/// <summary>模型配置与样本训练的设定方法不兼容。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8686。想给模型配置样本训练相关参数，但该模型的建立方式与此不兼容（非样本训练可作用的形态）；与 8684 同族——8684 是训练失败，此码是配置路径不被允许。兼容边界 [待实测]</para>
	///   <para><b>处理</b>从一开始就用样本训练支持的创建方式建模型。</para>
	/// </remarks>
	public const int Jl_ERR_GSM_SET_SAMPLE_TRAINING = 8686;

	/// <summary>条码的模块数与码制规格不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8701。module 指条码最小宽度单位；解码时统计出的模块数与该码制规定的数量对不上——常因图像模糊致窄宽元素粘连、局部破损，或扫描线未沿码的条向垂直方向穿过。本库 C# 层没有条码读码算子的包装，此码来自原生运行层 [待实测]</para>
	///   <para><b>处理</b>提高采集分辨率（窄元素至少约 2 像素宽）、保证静区完整，并让扫描方向贴近条的横向。</para>
	/// </remarks>
	public const int Jl_ERR_BAR_WNOM = 8701;

	/// <summary>条码元素数与码制不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8702。element 指一根条或一个空；解码扫描线得到的元素序列个数与该码制编码规则不一致。与 8701（模块计数）的区别：这里错在"黑白段个数"，多由边缘处的虚假翻转或断线合并引起。</para>
	///   <para><b>处理</b>清洁成像与印刷面；同一枚码持续触发时，核对内容是否真属于所声明的码制。</para>
	/// </remarks>
	public const int Jl_ERR_BAR_WNOE = 8702;

	/// <summary>训练串中含有该码制无法表示的字符。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8703。做读码训练时给出的样串包含目标码制字符集之外的字符（如给纯数字码制喂字母、混入控制字符/全角字符）。</para>
	///   <para><b>处理</b>按码制字符表清洗样串或改用支持的码制；全半角与不可见字符是最常见的漏网。</para>
	/// </remarks>
	public const int Jl_ERR_BAR_UNCHAR = 8703;

	/// <summary>条码描述（descriptor）里的属性名不合法。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8705。递给原生侧的条码描述结构里出现了其模式不认识的名字（attribute 拼错、大小写/下划线不符，或该版本不支持此字段）。</para>
	///   <para><b>处理</b>逐字对照码制文档的属性表；本库 C# 层如何组织该描述 [待实测]</para>
	/// </remarks>
	public const int Jl_ERR_BAR_WRONGDESCR = 8705;

	/// <summary>条码元素宽度不符合编码规则。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8706。实测出的宽/窄元素比例违背码制约定（如 2:1、3:1 类比例对不上）——多因印刷横向拉伸、打印 dpi 与设定不符，或拍摄存在透视使条宽沿码长方向渐变。</para>
	///   <para><b>处理</b>校对打印宽窄比；拍摄避免大倾角，先几何校正再解码。</para>
	/// </remarks>
	public const int Jl_ERR_BAR_EL_LENGTH = 8706;

	/// <summary>检索范围内没有发现可解码的条码区域。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8707。定位阶段就没出候选区：图里没有码、或码的对比度/尺寸在读码器可感范围之外。语义是"没找到"，区别于 8710（找到候选但逐扫描线解不动）。</para>
	///   <para><b>处理</b>先肉眼确认码在视场内且模块宽度够采样；放宽定位阈值或扩大搜索区。</para>
	/// </remarks>
	public const int Jl_ERR_BAR_NO_REG = 8707;

	/// <summary>条码类型与指定（或与检出的）码制不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8708。检出的码图形特征属于另一种码制，或指定的码制根本不容纳这枚码的编码方式；指定码制做训练/解码而实码是别的码制时最容易撞上。</para>
	///   <para><b>处理</b>改用正确码制，或允许读码器做多码制探测后再按结果分派。</para>
	/// </remarks>
	public const int Jl_ERR_BAR_WRONGCODE = 8708;

	/// <summary>条码读取器内部错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8709。原生读码器进入不自洽状态；调用方的任何参数都不直接对应这个语义，属于兜底异常而非可调整的用法问题。</para>
	///   <para><b>处理</b>确认稳定复现的最小输入，连同运行时版本上报；换版本运行时验证是否消失。</para>
	/// </remarks>
	public const int Jl_ERR_BAR_INTERNAL = 8709;

	/// <summary>条码候选区内没有任何一条解码成功的扫描线。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8710。定位已产出候选区，但沿码长方向逐条扫描全部解码失败——局部污损、散焦使一部分扫描线残缺，而剩余扫描线的元素比/校验又不过。比 8707 更接近"码存在但读不出"。</para>
	///   <para><b>处理</b>改善成像质量或换容错更强的码制/训练样本；单枚顽固码优先怀疑印刷。</para>
	/// </remarks>
	public const int Jl_ERR_BAR_NO_DECODED_SCANLINE = 8710;

	/// <summary>条码模型列表为空。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8721。向读码流程发起查找/解码时，挂在该处的条码模型清单一条都没有——模型没建、建了没登记、或登记前就调了搜索。</para>
	///   <para><b>处理</b>先成功创建并注册至少一个模型；批量场景注意列表被清空后需重填。</para>
	/// </remarks>
	public const int Jl_ERR_BC_EMPTY_MODEL_LIST = 8721;

	/// <summary>多码制并存时不允许执行训练。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8722。读码训练一次只针对单一码制；模型列表里同时挂了多种码制就被拒。训练样本本身也难以同时代表多种编码形态。</para>
	///   <para><b>处理</b>把列表收窄到一种码制再训练，或按码制拆成多个模型各自训练。</para>
	/// </remarks>
	public const int Jl_ERR_BC_TRAIN_ONLY_SINGLE = 8722;

	/// <summary>码制专有参数不能走通用查询接口。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8723。get_bar_code_param（原生算子名）只认各码制共通的参数；问码制专属参数会被拒，须走 get_bar_code_param_specific 并按码制命名空间查询。这两个算子在本库 C# 层无包装。</para>
	///   <para><b>处理</b>区分"通用参数"与"码制特定参数"两张表，用对应查询口。</para>
	/// </remarks>
	public const int Jl_ERR_BC_GET_SPECIFIC = 8723;

	/// <summary>多码制并存时无法取得该（码制特定的）对象。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8724。取回的中间对象与码制一一绑定；模型列表里同时设了多种码制时无法确定该给哪一种，直接拒绝而不是猜。</para>
	///   <para><b>处理</b>限定单一码制后重试，或改走与码制无关的结果通道。</para>
	/// </remarks>
	public const int Jl_ERR_BC_GET_OBJ_MULTI = 8724;

	/// <summary>条码模型二进制（文件）格式错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8725。读入的文件不是条码模型二进制流的合法容器（文本文件冒充、下载成 HTML 错误页、字节序/换行转换破坏了二进制）。</para>
	///   <para><b>处理</b>二进制存取务必按字节原样读写；重新保存一份模型文件验证。</para>
	/// </remarks>
	public const int Jl_ERR_BC_WR_FILE_FORMAT = 8725;

	/// <summary>条码模型二进制文件的版本号错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8726。容器格式合法但内嵌版本与本运行时不匹配——与 8725 的"格式坏"不同，这里是版本对不上；处置同 8576 的版本配对思路（匹配版本重存或升级运行时）。</para>
	/// </remarks>
	public const int Jl_ERR_BC_WR_FILE_VERS = 8726;

	/// <summary>模型必须处于持久（persistency）模式才能交出所请求的对象/结果。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8727。非持久模式下，解码的中间对象/结果在调用收尾即销毁，事后再取就报此码；要检查"为什么没读出来"必须先以持久模式建模。创建接口的具体开关写法 [待实测]</para>
	///   <para><b>坑</b>持久模式保留每帧中间数据，内存占用与生命周期都要比默认模式多留心。</para>
	/// </remarks>
	public const int Jl_ERR_BC_NOT_PERSISTANT = 8727;

	/// <summary>扫描线灰度序列的下标越界。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8728。按索引取某条扫描线的灰度剖面时，索引超出该线实际记录的灰度数组长度；各候选区/扫描线的长度并不一致，不能用别线的长度外推。索引起算约定（0 基与否、长度由谁定）[待实测]</para>
	///   <para><b>处理</b>先查询该扫描线的长度再索引。</para>
	/// </remarks>
	public const int Jl_ERR_BC_GRAY_OUT_OF_RANGE = 8728;

	/// <summary>持久模式下尚未跑过任何一次查找/解码，无结果可取。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8729。原文点名 find_bar_code 与 decode_bar_code_rectangle2（模板原文把 rectangle 拼作 rectanlge；均为原生算子名）：模型虽在持久模式，但一次搜索都没执行过，留存的中间结果集为空。</para>
	///   <para><b>处理</b>取结果前至少成功调用一次查找/解码；换图后旧结果与新查询的对应关系要注意。</para>
	/// </remarks>
	public const int Jl_ERR_NO_PERSISTENT_OP_CALL = 8729;

	/// <summary>超分辨率放大解码过程被中止。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8730。对过小/过糊的码做 zoomed（超分辨率）解码时算法中途放弃——迭代或时限等内部约束未满足即止，不产出放大结果。中止判据由原生侧掌握 [待实测]</para>
	///   <para><b>处理</b>与其指望放大救场，不如提高原始分辨率、缩短工作距或收窄候选区让超分任务变轻。</para>
	/// </remarks>
	public const int Jl_ERR_BC_ZOOMED_ABORTED = 8730;

	/// <summary>超分辨率（SRB）模块收到无效输入数据。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8731。交给 zoomed 解码通道的图像不满足其硬性前提（通道/类型、尺寸或区域约定）。哪些维度是硬前提 [待实测]</para>
	///   <para><b>处理</b>喂入前核对图像类型与候选区完整性；与 8730 区分：那是算不动，这是根本不受理。</para>
	/// </remarks>
	public const int Jl_ERR_BC_ZOOMED_INVALID_INPUT = 8731;

	/// <summary>条码归一化互相关匹配收到非法输入。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8740。条码的归一化互相关（NCC）环节入参校验失败（图像/模板尺寸、区域或类型不合法）——是"不受理"，与 8742 的"受理了但找不到相关峰"分属两阶段。</para>
	///   <para><b>处理</b>核对模板与搜索区域同图同源；模板不应大于搜索域。</para>
	/// </remarks>
	public const int Jl_ERR_BC_XCORR_INVALID_INPUT = 8740;

	/// <summary>条码互相关过程中坏行（低相关行）数量超限。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8741。逐行与模板做归一化互相关时，相关度过低的行数超过内部容许值——条码在图内旋转未校平、散焦或打印畸变让多数扫描行整体失配。容限具体数值在原生侧 [待实测]</para>
	///   <para><b>处理</b>先把候选区校平到条向水平再做相关；别用整幅大图硬搜。</para>
	/// </remarks>
	public const int Jl_ERR_BC_XCORR_TOO_MANY_BAD_ROWS = 8741;

	/// <summary>条码互相关没有找到任何相关峰。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8742。NCC 检索完整跑完但没有任何超过门限的相关峰，语义上是"图里没有这个模板"的未命中，不是调用错误。</para>
	///   <para><b>处理</b>确认模板与实码同版本同印刷条件；必要时改用更宽松的匹配或依赖解码结果本身。</para>
	/// </remarks>
	public const int Jl_ERR_BC_XCORR_NO_CORRELATION = 8742;

	/// <summary>GS1 语法词典无效。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8743。用于解析/校验 GS1 应用标识符（AI）结构的语法词典缺失、损坏或条目不合式，含 GS1 内容的码（如 DataMatrix/GS1-128）就无法按 AI 域拆解。</para>
	///   <para><b>处理</b>恢复随产品的标准词典文件；自定义词典需符合其条目语法。本库侧词典装载通路 [待实测]</para>
	/// </remarks>
	public const int Jl_ERR_INVALID_SYNTAX_DICTIONARY = 8743;

	/// <summary>指定的二维码制不受支持。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8800。设置给 2D 读码的码制类型名不在原生侧支持列表内（拼写、大小写或该部署版本不含此码制）。本库 C# 层没有 2D 读码算子包装，错误经原生运行层上浮 [待实测]</para>
	///   <para><b>处理</b>核对当前部署实际支持的码制清单再指定。</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_UNKNOWN_TYPE = 8800;

	/// <summary>前景（极性）指定错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8801。2D 码读取把"深色还是浅色算前景"作为显式参数；参数值本身不合法，或设成与实际印刷极性相反（浅色码/负片码未指明前景）导致图形结构无法成立。</para>
	///   <para><b>处理</b>负片打印（深底浅码）必须显式切换前景设定；不确定时可让读码器先试双极性。</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_WRONG_FOREGROUND = 8801;

	/// <summary>指定的矩阵尺寸不合法。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8802。为符号显式指定的模块矩阵规格（N×M 模块数）不在该码制允许的规格表内——2D 码的尺寸是离散枚举值，不能像几何图形那样任意取。</para>
	///   <para><b>处理</b>查码制规格表选合法尺寸，或干脆不指定让解码器自行识别。</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_WRONG_SIZE = 8802;

	/// <summary>指定的符号形状不合法。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8803。符号形状参数（方形/矩形等枚举）取值非法，或与所选码制不匹配（该码制只有方形符号却指定了矩形族）。合法枚举集 [待实测]</para>
	///   <para><b>处理</b>按码制能力选形状；不确定时留空由解码器判定。</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_WRONG_SHAPE = 8803;

	/// <summary>通用参数名不存在。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8804。2D 读码的通用参数设置/查询传了不认识的名字；与 8805 分界清楚——这里"名字"就查无此项，尚未走到值校验。</para>
	///   <para><b>处理</b>逐字核对参数名拼写（大小写、下划线）；参数表随码制而异。</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_WRONG_PARAM_NAME = 8804;

	/// <summary>通用参数值非法。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8805。参数名合法但给的值不在其允许集合/范围内；与 8804（名字不存在）构成参数校验的先后两级。</para>
	///   <para><b>处理</b>查该参数的取值域（枚举串或数值区间）后改值，别靠试错穷举。</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_WRONG_PARAM_VAL = 8805;

	/// <summary>符号打印模式参数错误。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8806。符号的打印模式参数（区分常规印刷与直印部件标记等成像特性的设定）取值非法或与码制不配；允许的枚举值 [待实测]</para>
	///   <para><b>处理</b>DPM 打标件要显式选对应模式，普通印刷标签保持默认，选错模式会让定位策略整体跑偏。</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_WRONG_MODE = 8806;

	/// <summary>符号区紧贴图像边界，无法完成解码。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8807。检出的 2D 码区域离图像边缘太近：符号四周需要完整留白（静区）供模块网格定位，贴边时该估计失去外圈依据而拒绝解码——码体本身被裁掉时更不可能成功。</para>
	///   <para><b>处理</b>加大视场让码四周留出余量；被物理裁切的码只能重拍，调参救不回来。</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_SYMBOL_ON_BORDER = 8807;

	/// <summary>检测初段找不到任何矩形模块轮廓。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8808。原生文本 <c>No rectangular module boundings found</c>，常量名即"模块轮廓数"：2D 码检测第一步要在图里找出近似正方形的模块级轮廓作为候选，一个都没找到就回本码。它是本族（8800～8813）里最靠前的失败——还没轮到定位图案（8809）、尺寸核验（8810）、归类（8811）出场。</para>
	///   <para><b>处理</b>按"图里根本没有码"来查：视野是否盖住码、前景极性是否设反（见 8801）、对比度/分辨率是否低到模块不可分。若确认码在视野内仍命中本码，多半是模块太小或太糊，调相机而不是调解码参数。</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_MODULE_CONT_NUM = 8808;

	/// <summary>找不到或认不出符号的定位图案。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8809。原生文本 <c>Couldn't identify symbol finder</c>：2D 码靠专用图形（如回字形定位角标、L 形定位边）先定姿再铺网格；检出了候选结构，但定位图案本身没能识别出来，流程走不到网格对齐。典型诱因是定位图案被污损、遮挡或印刷不达标，而码体中心部分反而看着还行——这也是它常比"整码拍糊"更晚暴露的原因。</para>
	///   <para><b>处理</b>与 8808（连矩形模块轮廓都没有）分阶段：8808 死在找结构，本码死在结构里认图案。检查聚焦在图案区（角部/边部）完整性上，重拍时保证图案四角不被裁切、不被扎带标签类物体压住。</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_SYMBOL_FINDER = 8809;

	/// <summary>检出的符号区域尺寸与预期规格不符。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8810。原生文本 <c>Symbol region with wrong dimension</c>：定位图案找到了，但量出的符号区域尺寸对不上预期——多半是模块网格数（尺寸规格）与设定值不符，或透视/离焦让区域尺度估计失真。与 <c>Jl_ERR_BAR2D_WRONG_SIZE</c>=8802 不同：8802 是"设定的规格值本身非法"，本码是"图像里量出来的东西和合法设定对不上"。</para>
	///   <para><b>处理</b>若确知码的规格，检查现场有没有混入不同规格的码；若是同一规格频繁命中本码，优先怀疑拍摄几何（斜视、离焦）而非改设定。允许尺寸未知时不要锁死规格参数 [待实测：本码判定所用的"预期尺寸"来自设定还是自估计未在码里体现]</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_SYMBOL_DIMENSION = 8810;

	/// <summary>检出候选区域后无法归类码制，分类步骤失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8811。原生文本 <c>Classification failed</c>：符号区域已被找出，但分类器认不出它是哪种 2D 码制（或置信度不足），流程停在解码之前。常见于码制设定与实际印刷码不符、图案风格介于两种码制之间、或图形被裁切到特征不足以归类。</para>
	///   <para><b>处理</b>与 8812 分界清楚——8812 已经认出码制、死在纠错，本码连码制都没定下来。若确认现场码制单一，显式指定 8800 一族里的类型参数可绕开盲分类；否则查图像是否只拍到符号一角（与 8807 贴边码呼应）。</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_CLASSIF_FAILED = 8811;

	/// <summary>符号已定位并对齐，但比特解码（含纠错）失败。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8812。原生文本 <c>Decoding failed</c> 在流水线上是最后一步失败：轮廓（8808）、定位图案（8809）、尺寸核验（8810）、码制归类（8811）都已过去，模块网格已对齐到比特，但纠错能力不够、恢复不出内容。典型诱因是局部污损/印刷缺陷超过码制纠错等级，或网格对齐差一格导致整帧比特错位。</para>
	///   <para><b>处理</b>与 8811 分界要拿准：8811 是"认不出这是什么码"，本码是"认出来了但读不出内容"。前者换码制设定，后者优先提高印刷质量/纠错等级或换更清晰的视野，放宽检测参数对本码帮助有限。</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_DECODING_FAILED = 8812;

	/// <summary>遇到"读码器编程"类符号或模式，但当前不支持该路径。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误码 8813。原生文本 <c>Reader programming not supported</c>：某些 2D 码（如 DataMatrix 的 reader programming 变体）把内容编成"给读码器写配置"的专用形式，检出这类符号或对其设定相关模式时，当前解码路径不支持即回本码。具体触发面 [待实测]</para>
	///   <para><b>处理</b>与 8812（常规解码失败）分开看：本码不是图像质量差，而是该编码形态根本不在支持范围。业务上收到应把该码判为"不支持的码"另走分支，而不是加重打光重拍。</para>
	/// </remarks>
	public const int Jl_ERR_BAR2D_DECODING_READER = 8813;

	/// <summary>2D 数据码子系统的通用兜底错误码（取值 8820），原生文本为 <c>General 2d data code error</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>数据码 2D 族（8820 起）里最笼统的一码：原生在错误对不上任何具名细分码（句柄、参数、候选序号、文件格式等各有专码）时归到本码。它本身几乎不携带诊断信息，价值在于圈定"出错在数据码子系统内"这一范围；真正的原因要靠 <c>JlNativeApi.GetErrorMessage(err)</c> 回传的附加文本判断 [待实测：本码是否总伴随可用附加文本未在仓库中体现]。</para>
	///   <para><b>处置</b>把本码当"再查一层"的信号而非结论：先取错误消息文本，再对照同族具名码重新归类。本码 ≥1000，<c>JlNativeApi.IsError</c> 与 <c>IsFailure</c> 均判真，统一检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>子系统状态</b>本库托管层未包装数据码 2D 算子，此码多来自消息表核对或原生核内部上报 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_GENERAL = 8820;

	/// <summary>2D 数据码模型句柄的内部签名（signature）被破坏时被返回（取值 8821），原生文本为 <c>Corrupt signature of 2d data code handle</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>数据码句柄带一个内部签名字段，原生用它确认"这确实是一个数据码模型句柄"。签名不匹配说明句柄指向的内存已不再是当初那个对象——典型是模型被销毁/释放后句柄被继续使用，或内存被别的数据踩写。比 <c>Jl_ERR_DC2D_INVALID_HANDLE</c>=8822 更严重一档：8822 是"认不出这是数据码句柄"，本码是"它冒充数据码句柄但签名验不过"，往往意味着野句柄。</para>
	///   <para><b>处置</b>不要在出错后继续重试同一句柄（可能踩坏更多内存）；应作废该句柄、重新创建模型，并回查是谁在模型销毁后仍持有旧句柄。本码 ≥1000，<c>JlNativeApi.IsError</c> 与 <c>IsFailure</c> 均判真，统一检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>子系统状态</b>本库托管层无数据码 2D 包装，托管路径不产生此类句柄；真收到即来自原生内部或跨边界手工传句柄 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_BROKEN_SIGN = 8821;

	/// <summary>传入的句柄不被 2D 数据码子系统认可为合法模型句柄时被返回（取值 8822），原生文本为 <c>Invalid 2d data code handle</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>数据码 2D 的句柄问题分三档，按"离题远近"排：<c>Jl_ERR_DC2D_BROKEN_SIGN</c>=8821 是句柄内部签名被破坏（内存被改写或回收后重用）；本码是句柄本身就认不出来——多半传了空句柄、别的对象族的句柄，或已被销毁的句柄；<c>Jl_ERR_DC2D_NOT_INITIALIZED</c>=8824 才是"句柄合法但模型内部数据缺失"。报本码先怀疑参数拿错，而不是模型坏了。</para>
	///   <para><b>处置</b>核对句柄来源：是否把别的模型句柄或整数值塞进了数据码接口；句柄是否已被释放又复用。本码 ≥1000，<c>JlNativeApi.IsError</c> 与 <c>IsFailure</c> 均判真，统一检查路径会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>子系统状态</b>本库托管层没有数据码 2D 算子包装，句柄无从在托管路径上产生或传递；出现本码说明调用点绕过了托管层直接走原生 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_INVALID_HANDLE = 8822;

	/// <summary>调用 2D 数据码识别时传入的模型列表为空时被返回（取值 8823），原生文本为 <c>List of 2d data code models is empty</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>2D 数据码识别的入口是"一组已创建的模型"：可以按不同码制、不同成像条件各建一个模型，调用时沿列表逐个试检。本码表示这个列表在调用时刻是一个都不剩——多半是创建一步没执行、创建失败后列表没被填上，或上层把"全部模型的句柄集合"误清空了。与 <c>Jl_ERR_DC2D_INVALID_HANDLE</c>=8822 分界清楚：8822 是列表里有项但某一项句柄非法，本码是列表整个为空。</para>
	///   <para><b>处置</b>先确认每个模型创建调用都成功返回并把句柄追加进了列表，再谈参数调优；动态拼装列表的代码路径应对"长度为 0"做前置判断而不是让原生报码。本码 ≥1000，<c>JlNativeApi.IsError</c> 与 <c>IsFailure</c> 均判真，统一返回码检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>子系统状态</b>本仓库托管层检索不到数据码 2D 模型的包装类型或算子入口，纯托管调用路径一般触发不了本码；真收到时多来自原生核内部或跨边界复用（含把空容器过界传下去）[待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_EMPTY_MODEL_LIST = 8823;

	/// <summary>数据码 2D 模型句柄有效、但其内部数据尚未初始化（或因未开启持久化而已被丢弃）时被返回（取值 8824），原生文本为 <c>Access to uninitialized (or not persistent) internal data</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>与前一个码 <c>Jl_ERR_DC2D_INVALID_HANDLE</c>(8822) 分工不同：8822 说的是句柄本身不是合法的数据码模型句柄；本码说的是句柄合法、但模型内部用来做码元定位的那份数据不在内存里——要么从未训练/初始化，要么已经被释放掉。原生文本里的 "(or not persistent)" 正指向后者，与参数 <c>persistence</c>（见 <c>Jl_ERR_DC2D_WRONG_PERSISTENCE</c>=8849）是同一件事的两端。</para>
	///   <para><b>典型触发</b>创建模型后没有设定参数并完成训练就直接用于识别；或模型按"不持久化"方式创建，换了一张尺寸不同的图像后内部数据被原生侧回收，后续调用即命中本码。具体回收条件 [待实测]。</para>
	///   <para><b>处置</b>按"模型不可用"而非"参数写错"处理：重做模型的初始化/训练步骤；若确需跨多种输入尺寸复用同一模型，把持久化选项打开后重新生成模型，而不是反复重试同一调用。本码 ≥1000，库内部 <c>JlNativeApi.IsError</c> 判为真错误、<c>IsFailure</c> 也判失败，统一返回码检查路径会据此抛 <c>JlOperatorException</c>。</para>
	///   <para><b>子系统状态</b>本仓库托管层检索不到数据码 2D 模型的包装类型或算子入口（无 DataCode 族类），本码在纯托管调用路径上一般不会被触发；真收到时多半来自原生核内部或跨边界复用句柄 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_NOT_INITIALIZED = 8824;

	/// <summary>数据码 2D 识别结果按"候选序号"取值时，给出的 <c>Candidate</c> 索引超出实际检出的候选数（或为负）时被返回（取值 8825），原生文本为 <c>Invalid 'Candidate' parameter</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>数据码识别常一次检出多个码，取属性（解码内容、中心坐标、姿态等）时靠 <c>Candidate</c> 这个从 0 起的序号指定取第几个 [待实测：起始值是 0 还是 1 未在仓库中体现]。本码表示该序号在本帧结果里没有对应候选——即"索引合法类型、但超出上界"，属于典型的"结果依赖上游"坑：候选数每帧都在变，上一帧能取到 3，这一帧只检出 2 个就会报本码。</para>
	///   <para><b>与相邻码区分</b><c>Jl_ERR_DC2D_WRONG_PARAM_VALUE</c>(8830) 是参数值本身非法，本码专指候选序号越界；<c>Jl_ERR_DC2D_INDEX_PARNUM</c>(8826) 与 <c>Jl_ERR_DC2D_EXCLUSIV_PARAM</c>(8827) 则是"多候选 × 多参数"的取回组合限制，不是越界。</para>
	///   <para><b>处置</b>取属性前先拿到本帧候选数，用 <c>candidate &lt; 候选数</c> 做前置判断再遍历，而不是写死一个上限；本码 ≥1000 属真错误，库内部 <c>JlNativeApi.IsFailure</c> 判失败并抛 <c>JlOperatorException</c>。</para>
	///   <para><b>子系统状态</b>本库托管层没有数据码 2D 的算子包装，此码一般只在原生核或跨边界复用句柄时出现 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_INVALID_CANDIDATE = 8825;

	/// <summary>一次调用同时要求"多个候选 + 多个参数"的取回组合不被支持时被返回（取值 8826），原生文本为 <c>It's not possible to return more than one parameter for several candidates</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>取回结果时有两种展开方向：沿候选展开（一个参数、多个 Candidate）或沿参数展开（多个参数、一个 Candidate）。本码表示同时走了两个方向——想要多个候选的多个参数，原生侧不给出这种二维结果。</para>
	///   <para><b>名字与文本的错位</b>常量名 <c>INDEX_PARNUM</c> 读作"索引 + 参数号"，而内嵌文本讲的是候选数与参数数的组合限制，二者指向同一限制但角度不同；判分支时以取值 8826 为准，不要靠猜原生文本措辞。</para>
	///   <para><b>处置</b>改成两层循环：外层固定一个 Candidate、内层一次只取一个参数；或外层取一个参数、遍历全部候选。这与 <c>Jl_ERR_DC2D_EXCLUSIV_PARAM</c>(8827) 是同一族约束的两面——8827 专指某个返回多值的参数只能配单候选。</para>
	///   <para><b>子系统状态</b>本库未包装数据码 2D 取结果的算子，托管路径一般收不到本码；出现即说明调用点绕过了托管层 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_INDEX_PARNUM = 8826;

	/// <summary>某个输出参数本身带多个值，被要求与其它参数一起对多个候选取回时被返回（取值 8827），原生文本为 <c>One of the parameters returns several values and has to be used exclusively for a single candidate</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>有些输出天然是变长数组（例如一个码的多个顶点、解码出的多段文本），它们只允许"一个候选一次取一个参数"地取回。本码不是值越界也不是类型错，而是取回组合被拒绝：多值参数 + 多候选同时出现。</para>
	///   <para><b>典型触发</b>把解码内容、边界点等变长输出写进一次批量取回的参数名列表里；候选数 &gt; 1 时尤其容易命中，因为单候选时同样的写法是允许的——这类"平时能跑、换张图就报"的静默差异正是本码的价值所在。</para>
	///   <para><b>处置</b>把该参数单独取：外层按 Candidate 逐个循环，一次请求列表里只留这个多值参数。与 <c>Jl_ERR_DC2D_INDEX_PARNUM</c>(8826) 一并处理：两个码都说明"必须降成一维"。本码 ≥1000，库内部判为真错误，统一检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>子系统状态</b>本库无数据码 2D 算子包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_EXCLUSIV_PARAM = 8827;

	/// <summary>把"默认参数集"这一项写在了参数表非首位时被返回（取值 8828），原生文本为 <c>Parameter for default settings must be the first in the parameter list</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>数据码 2D 的创建/训练类算子支持"先套用一组出厂默认参数、再逐项覆盖"的写法，而承载默认集的那个参数（即 <c>default_parameters</c>，见 <c>Jl_ERR_DC2D_WRONG_DEF_SET</c>=8845）必须排在参数名列表的第 1 位，后面的项才会覆盖在它之上。位置错则本码。</para>
	///   <para><b>为什么要卡位置</b>覆盖顺序决定最终值：默认集若排在中间，它之前的显式参数会被它冲掉，语义不确定，故原生直接拒绝而不是隐式重排。这也是"参数名列表顺序有语义"的一个典型例子——托管层的 <c>JlTuple</c> 只按序装元素，不会替调用方纠正顺序。</para>
	///   <para><b>处置</b>把默认集挪到参数名列表首位再重试；若只是想改个别参数、并不想要默认集，直接删掉这一项即可。本码属参数装配错误，与 8830（值非法）、8831（名不认识）互斥，先看原生文本是"must be the first"还是"Invalid parameter value"再决定改哪头。</para>
	///   <para><b>子系统状态</b>本库无数据码 2D 包装算子，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_DEF_SET_NOT_FIRST = 8828;

	/// <summary>数据码 2D 子系统的兜底码：错误发生但该错误没被归入 88xx 段里任何一个更细的码（取值 8829），原生文本为 <c>Unexpected 2d data code error</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>它是本子系统的"其它"分支，不携带任何可辨识的成因信息——与 <c>Jl_MSG_FAIL</c>(5) 的"笼统失败"不同，5 在参数/句柄检查之前，本码在数据码 2D 内部分支走完之后仍无法归类时才会出现。</para>
	///   <para><b>典型触发</b>模型内部数据与请求的操作不匹配（如用了尚未支持的数据码类型）、内存/句柄状态被前一步调用破坏、或原生版本与本库头文件不完全对应。具体清单仓库内无从证实 [待实测]。</para>
	///   <para><b>处置</b>不要用重试掩盖：本码意味着原生侧认为状态已不一致。做法是记录完整上下文（码 + <c>JlNativeApi.GetErrorMessage</c> 文本 + 触发它的算子名），丢弃并重建模型句柄后再试一次；仍复现则按缺陷上报。不要为本码写业务分支逻辑——它的含义随原生版本可变，不是稳定契约 [待实测]。</para>
	///   <para><b>子系统状态</b>本库未包装数据码 2D 算子，托管路径一般收不到本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_INTERNAL_UNEXPECTED = 8829;

	/// <summary>传给数据码 2D 算子的某个参数取值落在允许范围之外（取值 8830），原生文本为笼统的 <c>Invalid parameter value</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>值非法是"名字认识、类型也对，但值不被接受"这一档：与 <c>Jl_ERR_DC2D_WRONG_PARAM_NAME</c>(8831) 分别指"不认识这个名字"和"名字对值不对"，排查方向完全不同，别混着看。</para>
	///   <para><b>信息量的缺失</b>原生文本没说是哪个参数——因为参数名列表与其值是一一配对送进去的，出错项要靠调用方自己按装配顺序回看。托管层 <c>JlTuple</c> 对 int/double/string 有隐式转换，装错也编译得过，最终就在这里以码的形式暴露。</para>
	///   <para><b>典型触发</b>尺寸类参数给了 0 或负数；阈值类参数超出 0~255 的灰度量纲 [待实测]；枚举型字符串写成非法大小写或用了本版本不支持的取值。</para>
	///   <para><b>处置</b>成对打印"参数名 + 参数值"再逐项比对文档允许集；先只保留必要参数（其余走默认）跑通，再一项项加回来定位。本码 ≥1000 属真错误，<c>JlNativeApi.IsFailure</c> 判失败，统一检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>子系统状态</b>本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_PARAM_VALUE = 8830;

	/// <summary>参数名列表里出现数据码 2D 算子不认识的名字时被返回（取值 8831），原生文本为 <c>Unknown parameter name</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生侧对参数名做严格字符串匹配（大小写敏感、不做前缀或模糊匹配），匹配不上就直接拒收，不会静默忽略。与 <c>Jl_ERR_DC2D_WRONG_PARAM_VALUE</c>(8830) 的分工是"名字不存在"对"名字存在但值不行"。</para>
	///   <para><b>典型触发</b>把 88xx 段里那些带下划线的参数名（<c>module_shape</c>、<c>contrast_min</c>、<c>small_modules_robustness</c> 等）写成驼峰或连字符；把不同版本新增的参数用到旧库上；把 <c>finder_pattern_tolerance</c> 一类参数名误当作通用 <c>num_levels</c>/<c>min_size</c> 那样的公共参数传进不需要它的算子。</para>
	///   <para><b>处置</b>对照本段常量名里带引号的原生参数名逐项核 spelling，并把可疑项先删掉（多数参数都有隐式默认值），确认能跑后再逐个加回。<c>classificator</c> 这类原生故意使用的拼写要照抄，不要"顺手改成 classifier" [待实测：具体参数名单未在仓库中给出]。</para>
	///   <para><b>子系统状态</b>本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_PARAM_NAME = 8831;

	/// <summary>数据码 2D 的 <c>polarity</c>（明暗极性）参数被给了枚举之外的取值时被返回（取值 8832），原生文本为 <c>Invalid 'polarity'</c>。</summary>
	/// <remarks>
	///   <para><b>这个参数管什么</b>极性决定"把亮当码元还是把暗当码元"，即二值化判决的方向。产线常见组合是亮背景暗图案与暗背景亮图案两种，反过来设就一个码也检不到。</para>
	///   <para><b>为什么值得单列一码</b>同段的 8830 是"值不在范围"，本码把极性这一项单独拎出来，说明它是数据码 2D 里最常被写错的枚举之一。它的合法值是字符串标志而非数值，且大小写敏感；确切名单仓库内没有依据可抄 [待实测]。</para>
	///   <para><b>坑</b>极性设错通常不报错、只是检不到码；因此报本码时是"字符串写错被拒"，而不是"图像不匹配"。若返回码是成功而结果为空，方向应去查 <c>contrast_min</c>(8838)、<c>measure_thresh</c>(8839) 与实际明暗，而不是继续改极性写法。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_POLARITY = 8832;

	/// <summary>数据码 2D 的 <c>symbol_shape</c>（符号形状）参数取值非法时被返回（取值 8833），原生文本为 <c>Invalid 'symbol_shape'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>本段里 <c>symbol_*</c> 与 <c>module_*</c> 是两层不同图元的参数：符号形状对应 <c>symbol_shape</c>(8833)、符号尺寸对应 <c>symbol_size</c>(8834)；码元形状对应 <c>module_shape</c>(8836)、码元尺寸对应 <c>module_size</c>(8835)。两套各有自己的专用错误码，说明原生不接受把符号级参数值塞进码元级参数、反之亦然。</para>
	///   <para><b>典型触发</b>把圆点码（点阵/圆码一类）的形状串写给方形码元的参数，或直接把 <c>module_shape</c> 的合法值复制给 <c>symbol_shape</c>。两族合法值集是否相同仓库内无依据 [待实测]。</para>
	///   <para><b>处置</b>先确认自己在配的是"符号"层还是"码元"层——看参数名下划线前半段即可；再按该层的取值表重写。改形状类参数后需重新生成/训练模型才生效，仅改调用参数不一定影响已缓存的模型数据 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_SYMBOL_SHAPE = 8833;

	/// <summary>数据码 2D 的符号尺寸参数不在允许范围内时被返回（取值 8834），原生文本为笼统的 <c>Invalid symbol size</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>符号（<c>symbol</c>）是比码元（<c>module</c>）更高一层的图元单位，本码只管尺寸数值，与 <c>Jl_ERR_DC2D_WRONG_SYMBOL_SHAPE</c>(8833) 的形状枚举、<c>Jl_ERR_DC2D_WRONG_MODULE_SIZE</c>(8835) 的码元尺寸各自分开。数值类错误通常是 0、负数或超出该码制的上下界 [待实测：上下界与量纲（像素还是毫米）未在仓库中体现]。</para>
	///   <para><b>坑</b>尺寸类参数之间相互约束：符号尺寸与码元尺寸、码元间距 <c>module gap</c>(8844)、码元长宽比 <c>mod_aspect_max</c>(8853) 要能同时成立，单独把一项改大很容易让另一项变成不自洽值——不自洽时原生回的是哪个码，仓库内没有依据可判 [待实测]。</para>
	///   <para><b>处置</b>按"图像上量一个实际码元占多少像素"倒推参数，而不是沿用别的码制或别的分辨率下的数值；换相机分辨率后这类尺寸参数必须重算。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_SYMBOL_SIZE = 8834;

	/// <summary>数据码 2D 的码元尺寸参数不在允许范围内时被返回（取值 8835），原生文本为笼统的 <c>Invalid module size</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>码元（module）是码图上最小的一格图案单元，本码只管它的尺寸数值：0、负数或超出该码制上下界 [待实测：上下界与量纲（像素还是毫米）未在仓库中体现]。与相邻码的分工：<c>Jl_ERR_DC2D_WRONG_SYMBOL_SIZE</c>(8834) 管符号层尺寸，<c>Jl_ERR_DC2D_WRONG_MODULE_SHAPE</c>(8836) 管码元形状枚举，本码专管码元层尺寸，三层各自独立报错。</para>
	///   <para><b>坑</b>码元尺寸与符号尺寸(8834)、码元间距 <c>module gap</c>(8844)、码元长宽比 <c>mod_aspect_max</c>(8853) 是一组互相约束的几何量：码元数 × 码元尺寸 + 间距 ≈ 符号尺寸，单独改大一项会让另一项不自洽；不自洽时原生回哪个码无依据可判 [待实测]。换相机分辨率或工件到相机的距离后，本参数必须按"图上一个码元实际占多少像素"重算，沿用旧值是产线调码最常见的错法。</para>
	///   <para><b>处置</b>先在图上量出单个码元的像素跨度再填值；若数值来自别的码制或别的分辨率，不要直接复制。本码 ≥1000 属真错误，<c>JlNativeApi.IsFailure</c> 判失败，统一检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>子系统状态</b>本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_MODULE_SIZE = 8835;

	/// <summary>数据码 2D 的 <c>module_shape</c>（码元形状）参数取值非法时被返回（取值 8836），原生文本为 <c>Invalid 'module_shape'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>码元形状是码图最细一层的图元形状（方格或圆点一类的点阵），与符号层的 <c>symbol_shape</c>(8833) 分属两层、各有一个专用错误码——原生不接受把符号层的取值串写给码元层，反之亦然 [待实测：两层合法取值集是否相同仓库内无依据]。取值是大小写敏感的字符串标志，不是数值。</para>
	///   <para><b>典型触发</b>打码工艺从方格码换成圆点码（点阵打码）后只改了形状一处，而尺寸(8835)、间距(8844)仍是旧形状下的取值；或把 <c>module_shape</c> 填成了 <c>symbol_shape</c> 的合法值。</para>
	///   <para><b>处置</b>先确认在配"符号"层还是"码元"层（看参数名前缀即可），再按该层取值表重写；改形状后需重新生成/训练模型才生效 [待实测]。与 8831 的区分：参数名本身必须是对的、值不对，才会走到本码。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_MODULE_SHAPE = 8836;

	/// <summary>数据码 2D 的 <c>orientation</c>（方向/角度）参数取值非法时被返回（取值 8837），原生文本为 <c>Invalid 'orientation'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>方向是数据码 2D 里少数"一个名字可能承担两种写法"的参数：既可能指搜索的期望角度，也可能指角度区间（下限/上限成对）[待实测：本库无数码包装，具体形态无仓库依据]。本码只说明"该参数收到的值不被接受"，不区分是数值越界还是写法形式不对——这两种成因在 8830 一节已被拆过一层（值域错 vs 枚举错），本码则是方向项的专用分支。</para>
	///   <para><b>典型触发</b>角度单位混淆（度当弧度填）、把区间写成单个值或把单值写成区间、正负/顺逆时针约定与相邻算子不一致 [待实测]。角度量纲是本仓库多处算子踩坑率最高的参数类别之一，填值前先确认单位约定。</para>
	///   <para><b>处置</b>先删掉该参数走默认值跑通（多数参数有隐式默认，见 8831 的处置），再单独加回方向项定位；判分支时以取值 8837 为准，不要靠原生文本措猜成因。本码 ≥1000 属真错误，<c>JlNativeApi.IsFailure</c> 判失败，统一检查会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>子系统状态</b>本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_ORIENTATION = 8837;

	/// <summary>数据码 2D 的 <c>contrast_min</c>（最小对比度）参数取值非法时被返回（取值 8838），原生文本为 <c>Invalid 'contrast_min'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>该参数是判决"亮暗差至少多大才算码"的下限，是检码灵敏度与误检率的总闸门。本码只管数值本身被拒（非数、负数、超界 [待实测：量纲是否为 0~255 灰度阶未在仓库中体现]），不管设高设低的后果——设错值不报本码，只会检不到码或误检。</para>
	///   <para><b>与相邻参数的分工</b><c>Jl_ERR_DC2D_WRONG_CONTRAST_TOL</c>(8855) 管的是对比度容差（波动带），与本码的"下限"是两个参数、两个码；<c>measure_thresh</c>(8839) 是码元测量环节的阈值。8832（极性）一节的"结果为空先查 8838/8839"说的就是这两个数值。</para>
	///   <para><b>处置</b>检不到码时优先降本值而不是改极性；反光、光照不均的工件配合 <c>back_texture</c>(8846) 一起调，不要一项调到极端。本码 ≥1000 属真错误，统一检查会抛 <c>JlOperatorException</c>；本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_CONTRAST = 8838;

	/// <summary>数据码 2D 的 <c>measure_thresh</c>（码元测量阈值）参数取值非法时被返回（取值 8839），原生文本为 <c>Invalid 'measure_thresh'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>定位到码图后，逐个码元取灰度判决 0/1 时用的阈值就来自这个参数；它影响的是"读出的码值对不对"，而 <c>contrast_min</c>(8838) 影响的是"找不找得到码"。两段别分错：检不到码调 8838 一族，读出错码/校验失败才优先看本参数 [待实测：此因果分工系按参数名推断，仓库无文档支撑]。</para>
	///   <para><b>典型触发</b>填了非数值、负数或超出该参数允许区间 [待实测：区间未在仓库中体现]；把百分比制阈值的旧习惯数值（如 30）填进了按灰度绝对值解释的参数里。</para>
	///   <para><b>处置</b>删掉本项走默认先跑通，再逐项调；与备用阈值 <c>alt_measure_red</c>(8840) 是主/备关系 [待实测：具体回退机制无仓库依据]。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_MEAS_THRESH = 8839;

	/// <summary>数据码 2D 的 <c>alt_measure_red</c> 参数取值非法时被返回（取值 8840），原生文本为 <c>Invalid 'alt_measure_red'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>从名字可读出的信息有限：<c>alt</c> 前缀说明它是 <c>measure_thresh</c>(8839) 的备用/替代一路，<c>red</c> 的含义（reduced？redundant？）仓库内无文档可断 [待实测]。能确定的是：它与主测量阈值配对存在，主备两个阈值各有一个专用错误码，说明原生对这一对参数分别校验。</para>
	///   <para><b>典型触发</b>只调主阈值不管备用参数，或把主阈值的数值量纲直接复制给本参数（两者量纲是否一致无依据 [待实测]）；填非数值、负数。</para>
	///   <para><b>处置</b>低对比度场景读不出码、动 8839 无效时再考虑本参数；调试时删掉本项走默认。判分支以取值 8840 为准。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_ALT_MEAS_RED = 8840;

	/// <summary>数据码 2D 的 <c>slant_max</c>（最大斜切/倾斜度）参数取值非法时被返回（取值 8841），原生文本为 <c>Invalid 'slant_max'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>斜切描述码元从正方形被"推歪"的程度（工件斜打、透视造成的平行四边形化）。本码指参数值本身被拒（负数、超界 [待实测：上界与单位无仓库依据]），不指"图像斜切超出该上限"——后者是检不到码，不报错。</para>
	///   <para><b>与相邻参数的关系</b>它和码元长宽比 <c>mod_aspect_max</c>(8853)、形变容差 <c>deformation_tolerance</c>(8857) 同属"容忍几何畸变"的一族开关：斜切管剪切分量、长宽比管拉伸分量、形变容差管整体 [待实测：三者确切分工按名推断]。产线上斜切打码（如针式/辊压打码）工件应优先放开本项，而不是无脑放大码元尺寸容差。</para>
	///   <para><b>处置</b>放开本项会增加误检风险，调整时配合收紧 <c>contrast_min</c>(8838) 压住误检。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_SLANT = 8841;

	/// <summary>数据码 2D 的 <c>L_dist_max</c> 参数取值非法时被返回（取值 8842），原生文本为 <c>Invalid 'L_dist_max'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>带大写字母前缀的 <c>L_*</c> 一族（本码与 <c>L_length_min</c>=8843）服务于"L 形定位图形"的码制——这类码靠角上的明暗交替 L 条来定位（寻迹图形）[待实测：具体服务的码制名单仓库内无依据]。本码管 L 图形几何关系的距离上限，取值被拒即本码。</para>
	///   <para><b>典型触发</b>把 L 系列参数填给了不含 L 定位图形的码制（多数方形矩阵码没有 L 条，这套参数对它们无意义）；数值超界或为负。参数名前缀大小写要照抄：原生用 <c>L_dist_max</c>（大写 L），写成 <c>l_dist_max</c> 会先撞上 8831 的"名字不认识"。</para>
	///   <para><b>处置</b>与 8843 成对核对：两个参数共同框定 L 图形的搜索几何，只调一项容易把另一项顶成不自洽值 [待实测：不自洽时报哪个码无依据]。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_L_DIST = 8842;

	/// <summary>数据码 2D 的 <c>L_length_min</c> 参数取值非法时被返回（取值 8843），原生文本为 <c>Invalid 'L_length_min'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>L 形定位图形一族的第二个参数：L 条的最小长度（与 <c>L_dist_max</c>=8842 的距离上限配对）。"min" 后缀说明它是下限——填得过小虽不报本码，但会让 L 搜索放过短的伪条，增加误检；本码只在数值本身非法（非数、负、超上界 [待实测]）时出现。</para>
	///   <para><b>典型触发</b>换码制后沿用了另一码制的 L 参数；图像分辨率翻倍后没把长度按像素同步放大（该参数按像素计还是按角度/相对量计，仓库无依据 [待实测]）。</para>
	///   <para><b>处置</b>与 8842 成对检查、成对修改；先删项走默认定位。参数名的大写 <c>L</c> 前缀照抄，别写成小写撞 8831。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_L_LENGTH = 8843;

	/// <summary>数据码 2D 的码元间距（module gap）参数取值非法时被返回（取值 8844），原生文本为不带引号的 <c>Invalid module gap</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>码元间距是相邻码元之间的空白宽度（点阵/圆点码里尤其关键：圆点之间留多大缝）。数值非法（负、超界 [待实测]）时报本码；间距设得不合实际则表现为读错码，不报错。</para>
	///   <para><b>坑</b>间距与码元尺寸(8835)、符号尺寸(8834)、长宽比 <c>mod_aspect_max</c>(8853) 共同决定码图的几何是否自洽：符号尺寸 ≈ 码元数 ×（码元尺寸 + 间距），三者改一必查二。8834 一节的"坑"段与此互为镜像。原生文本这次不带参数引号（对比 8835 也不带、8836 带），拼写匹配时以常量名与取值为准。</para>
	///   <para><b>处置</b>换打码工艺（蚀刻→激光、针打→喷码）后间距必须重测，不要沿用旧产线的值。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_GAP = 8844;

	/// <summary>数据码 2D 的 <c>default_parameters</c>（默认参数集名）取值非法时被返回（取值 8845），原生文本为 <c>Invalid 'default_parameters'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>创建/训练类算子支持先按一组命名的出厂参数集起步、再用显式参数逐项覆盖；本码专指"参数集的这个名字不存在"（拼错、码制不匹配、旧库没有新参数集名 [待实测：合法名单无仓库依据]）。与 <c>Jl_ERR_DC2D_DEF_SET_NOT_FIRST</c>(8828) 的分工：8828 管位置（默认集必须排参数列表第 1 位），本码管名字本身。</para>
	///   <para><b>典型触发</b>把码制名当参数集名传；跨版本复制工程时旧库不认识新参数集；引号/大小写与原生名单不一致（该匹配大小写敏感 [待实测]）。</para>
	///   <para><b>处置</b>最稳的调试法是整项删除——所有后续参数不带默认集直接全显式给出，能跑通后再决定要不要恢复默认集。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_DEF_SET = 8845;

	/// <summary>数据码 2D 的 <c>back_texture</c>（背景有纹理）标志取值非法时被返回（取值 8846），原生文本为 <c>Invalid 'back_texture'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>这是个布尔型标志，告诉原生"背景不是均匀底，要按有纹理背景做二值化/搜索策略"[待实测：合法写法（'true'/'false' 字符串还是 0/1）无仓库依据]。本码只在写法不被认作布尔时出现——它不像数值参数有越界问题。</para>
	///   <para><b>为什么值得单列一码</b>同段里 <c>mirrored</c>(8847)、<c>persistence</c>(8849) 等布尔参数也各留了一个专用码，说明这批标志被原生逐个严格校验，"随便写个 1 试试"的行话在这里会直接撞上错误码。</para>
	///   <para><b>取舍</b>标志开对方向能救纹理背景的漏检，但对干净背景打开会拖慢并增加误检——报本码说明值写错，还没到"策略选错"的层面。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_TEXTURED = 8846;

	/// <summary>数据码 2D 的 <c>mirrored</c>（镜像识别）标志取值非法时被返回（取值 8847），原生文本为 <c>Invalid 'mirrored'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>控制是否额外搜索码图的镜像版本（玻璃下方、贴膜反面等场景）。与 8846、8849 一样是逐项严格校验的布尔标志，写法不被认作合法布尔值才报本码 [待实测：合法写法名单无仓库依据]。</para>
	///   <para><b>坑</b>镜像开关影响搜索空间与耗时：开启后每个候选多一轮镜像验证，节拍紧的工位不要为"保险"常开 [待实测：耗时影响幅度无仓库依据]。它解决的是"码存在但被镜像"的漏检，与极性(8832)的漏检症状相同、成因不同——先分清是明暗反了还是左右反了。</para>
	///   <para><b>处置</b>报本码时只改写法不动策略：按文档给定布尔形式照抄。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_MIRRORED = 8847;

	/// <summary>数据码 2D 的 <c>classificator</c>（分类器选择）参数取值非法时被返回（取值 8848），原生文本为 <c>Invalid 'classificator'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>码元判决 0/1 用的分类器方案选择项，取值是枚举字符串 [待实测：具体方案名单与各自适用场景仓库内无依据]。本码专指取值不在名单内。</para>
	///   <para><b>必抄的拼写坑</b>参数名是 <c>classificator</c>，不是英语规范的 classifier——原生就这么拼的。"顺手纠正拼写"会得到 8831（名字不认识）而不是本码；8831 一节的处置段已就此提醒过。两码可用来定位错误阶段：撞 8848 说明名字传对、值传错，撞 8831 说明连名字都没认。</para>
	///   <para><b>处置</b>不同分类器对噪声、形变的耐受力不同，换方案后建议同一批样本图回归测试而不是只看一张 [待实测：方案间行为差异无仓库依据]。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_CLASSIFICATOR = 8848;

	/// <summary>数据码 2D 的 <c>persistence</c>（持久化）标志取值非法时被返回（取值 8849），原生文本为 <c>Invalid 'persistence'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>控制模型内部数据是否常驻内存。它与 <c>Jl_ERR_DC2D_NOT_INITIALIZED</c>(8824) 是同一件事的两端：8824 的文本 "(or not persistent)" 说的就是关持久化后内部数据被回收、后续访问扑空；本码只管这个开关本身的写法非法 [待实测：合法写法无仓库依据]。</para>
	///   <para><b>取舍</b>开持久化：内存占用换稳定，跨不同尺寸图像复用同一模型不会被回收 [待实测：回收条件在 8824 一节同样存疑]；关持久化：省内存，但要求每次调用前数据可重建。产线常驻模型建议开，调试期一次生很多模型的脚本建议关。</para>
	///   <para><b>处置</b>报本码时改的是写法不是策略；若症状是"偶发 8824"而非本码，才轮到调整持久化策略本身。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_PERSISTENCE = 8849;

	/// <summary>数据码 2D 的模型类型参数取值非法时被返回（取值 8850），原生文本为不带引号的 <c>Invalid model type</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>文本不带参数引号（对比 8832/8836 的 <c>Invalid 'xxx'</c> 格式），说明它多半不是参数表里的字符串项，而是创建/识别接口的直接实参——告诉原生这批模型按什么码制族/组织方式解释 [待实测：确切来源接口无仓库依据]。取值非法的具体名单仓库内不可考。</para>
	///   <para><b>典型触发</b>把别的子系统（描述符、形状模型）的类型枚举值传进了数据码接口；跨版本后旧库不认识新增的类型值；用整型硬编码值而类型体系是按字符串标志组织的。</para>
	///   <para><b>处置</b>回到"该接口文档声明的类型允许集"里选，不要拿邻近子系统的枚举试。与 8866（序列化容器里类型不符）区分：本码是创建/调用时的显式参数错，8866 是读出来的对象类型不对。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_MODEL_TYPE = 8850;

	/// <summary>数据码 2D 的 <c>module_roi_part</c> 参数取值非法时被返回（取值 8851），原生文本为 <c>Invalid 'module_roi_part'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>从名字读法：码元测量 ROI 的"份数/占比"参数——控制取码元中心多大一块区域来测灰度（占比类参数通常要求 0 到 1 或 0 到 100 的区间 [待实测：确切量纲与允许区间无仓库依据]）。设太小：噪声下判决不稳；太大：相邻码元串扰。</para>
	///   <para><b>典型触发</b>把百分比值（如 50）填给了按小数解释的参数、或反过来；负数；把占比类参数的习惯值填成了像素数。</para>
	///   <para><b>处置</b>先删项走默认——这是调优型参数，不在最小必要参数集里；跑通后再单独加回。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_MOD_ROI_PART = 8851;

	/// <summary>数据码 2D 的 <c>finder_pattern_tolerance</c>（定位图形容差）参数取值非法时被返回（取值 8852），原生文本为 <c>Invalid 'finder_pattern_tolerance'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>定位图形（finder pattern，码图上用于粗定位的规则图案，如 L 条、交替条纹）的匹配容差。容差越大对打印/冲压畸变越宽容、误检越多。本码指数值本身被拒（负数、超界 [待实测：量纲与上界无仓库依据]）。</para>
	///   <para><b>与相邻码的关系</b>它给 8842/8843 的 <c>L_*</c> 几何限制和 8856 的交替图案容差 <c>alternating_pattern_tolerance</c> 补上了"松紧"这一维：L 系列管几何范围、本参数管匹配偏差、8856 管明暗交替图案的偏差 [待实测：三者确切分工按名推断，调一个观察是否连带]。</para>
	///   <para><b>处置</b>先删项走默认。若同帧同时报本码与其他参数码，按 8831 的方法逐项加回定位，不要批量放开容差——批量放开会掩盖真正的几何失配。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_FP_TOLERANCE = 8852;

	/// <summary>数据码 2D 的 <c>mod_aspect_max</c>（码元长宽比上限）参数取值非法时被返回（取值 8853），原生文本为 <c>Invalid 'mod_aspect_max'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>码元允许的最大长宽比（斜向透视时方格被压成长条）。作为"上限"必须大于等于 1 才有意义，且与 <c>slant_max</c>(8841)、<c>deformation_tolerance</c>(8857) 同族 [待实测：合法区间无仓库依据]。注意参数名是缩写 <c>mod_</c> 而非 <c>module_</c>，照抄。</para>
	///   <para><b>与 8863 的分界</b>同段还有一个语义几乎重叠的码 <c>Jl_ERR_DC2D_WRONG_MODULE_ASPECT</c>(8863)（文本 <c>Invalid module aspect ratio</c>，不带参数引号）。合理推断是 8853 管参数表里的显式取值非法、8863 管内部推导出的长宽比越限 [待实测：两者确切触发路径无仓库依据]，回报错误时以取值区分。</para>
	///   <para><b>处置</b>透视/斜置工件读不到码时优先放开本项与 8841，而不是放大码元尺寸。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_MOD_ASPECT = 8853;

	/// <summary>数据码 2D 的 <c>small_modules_robustness</c>（小码元鲁棒性）参数取值非法时被返回（取值 8854），原生文本为 <c>Invalid 'small_modules_robustness'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>针对"码元相对图像分辨率很小"场景的鲁棒性档位，取值是分档的字符串标志 [待实测：档位名单无仓库依据]。典型适用对象是远距小码、低分辨相机下每码元只占几个像素的图。</para>
	///   <para><b>坑</b>它是整族行为开关而不是单点阈值：升档通常连带改变测量与判决路径，副作用（耗时、误检率）不是线性的 [待实测]；与把码元尺寸参数(8835)改小是两条不同的路——分辨率没变、只是码小，用本参数；物理尺寸本来就配错，改 8835。</para>
	///   <para><b>处置</b>报本码先查写法（大小写敏感、逐字照抄参数名）。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_SM_ROBUSTNESS = 8854;

	/// <summary>数据码 2D 的 <c>contrast_tolerance</c>（对比度容差）参数取值非法时被返回（取值 8855），原生文本为 <c>Invalid 'contrast_tolerance'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>与 <c>contrast_min</c>(8838) 成对但方向不同：min 是"至少多少亮暗差才算码"的下限，tolerance 是"沿码图各处对比度允许波动多大"的带宽 [待实测：确切语义按名推断]。光照不均、渐变反光的工件靠带宽而不是靠下限。</para>
	///   <para><b>典型触发</b>负数；把 8838 的下限值直接复制过来当带宽用；只调了一个码、另一个码的参数还留在旧值——两个参数都各有一个码，报错时先看清是哪一码。</para>
	///   <para><b>处置</b>删项走默认定位。本码 ≥1000 属真错误，统一检查会抛 <c>JlOperatorException</c>；本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_CONTRAST_TOL = 8855;

	/// <summary>数据码 2D 的 <c>alternating_pattern_tolerance</c>（交替图形容差）参数取值非法时被返回（取值 8856），原生文本为 <c>Invalid 'alternating_pattern_tolerance'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>"交替图案"指明暗相间的条纹（定位图形与部分码制的校验带都是这种图案）。本参数控制对其间距/相位偏差的容忍度，与 <c>finder_pattern_tolerance</c>(8852) 同属"松紧"类数值参数 [待实测：两者作用对象的确切划分按名推断]。</para>
	///   <para><b>典型触发</b>负数、超界；或把本参数与 8852 的取值互相复制——两个参数各配各的量程，抄错不会都报错，可能只撞其中一码。另有 QL 后缀的姊妹参数见 8869。</para>
	///   <para><b>处置</b>删项走默认，再单独加回。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_AP_TOLERANCE = 8856;

	/// <summary>数据码 2D 的 <c>deformation_tolerance</c>（形变容差）参数取值非法时被返回（取值 8857），原生文本为 <c>Invalid 'deformation_tolerance'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>对码图整体几何形变（揉皱、贴曲面、冲压拉伸）的容忍档位/数值 [待实测：是数值还是档位无仓库依据]。与 <c>slant_max</c>(8841)、<c>mod_aspect_max</c>(8853) 的分工：那两个管规则畸变分量，本参数管不规则整体形变 [待实测：按名推断]。</para>
	///   <para><b>坑</b>形变容差是这族参数里对误检率影响最大的一个——放开它等于允许更多形状凑数成码；只在确认"码在但读不出"且工件确实形变时才动它，且配合更严的校验（码制白名单、内容正则）收敛误检 [待实测：托管层无校验辅助可指]。</para>
	///   <para><b>处置</b>数值被拒（本码）与"形变超限检不到"（不报错）是两回事；报错只说明写法/量程错。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_DEFORM_TOL = 8857;

	/// <summary>读入 2D 数据码模型文件时文件头格式不合法时被返回（取值 8860），原生文本为 <c>Invalid header in 2d data code model file</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型文件的头部（魔数/版本/段表这类元信息区）没能按预期结构解析。属于读盘环节错误，与运行时参数无关——一旦命中，说明问题在文件本身或它的生成/传递链路，调识别参数没有用。</para>
	///   <para><b>典型触发</b>文件被文本方式传输/同步工具改写过（换行符与编码转换会破坏二进制头）；写到一半断电截断；拿了别的子系统生成的模型文件来读（文件头是各家自有的，跨子系统读会先撞头检查）[待实测：确切成因排序无仓库依据]。</para>
	///   <para><b>与相邻码分工</b>8860 头结构解析失败、<c>Jl_ERR_DC2D_READ_HEAD_SIGN</c>(8861) 头里签名不对、<c>Jl_ERR_DCD_READ_WRONG_VERSION</c>(8865) 版本不支持——三者合起来覆盖"文件能打开但不是（本版本）合法模型"的全部分支；文件根本不存在则走通用文件错误而非本码 [待实测]。处置统一是重新生成/重新分发模型文件，别试图修补。</para>
	///   <para><b>子系统状态</b>本库无数据码 2D 模型读盘包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_READ_HEAD_FORMAT = 8860;

	/// <summary>读入 2D 数据码模型文件时文件头签名不对时被返回（取值 8861），原生文本为 <c>Invalid code signature in 2d data code model file</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>头部结构解析得动、但里面的类型签名不是数据码模型——比 8860 更具体：文件"格式像样"却"身份不对"。最常见的是把别的模型的序列化文件喂给了数据码读接口，或反过来。</para>
	///   <para><b>坑</b>本码与句柄侧的 <c>Jl_ERR_DC2D_BROKEN_SIGN</c>(8821) 是两处签名：8821 校验的是内存里句柄的签名，本码校验的是磁盘文件头的签名。症状都叫"signature 不对"，一个查句柄生命周期，一个查文件来源，方向完全相反。</para>
	///   <para><b>处置</b>核对文件生成端与读取端是不是同一子系统、同一算子族；多模型混放的目录建议按扩展名/前缀分目录管理。文件被编辑工具打开另存过也会毁签名，重新导出一份。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_READ_HEAD_SIGN = 8861;

	/// <summary>读入 2D 数据码模型文件时某个数据行损坏/不可解析时被返回（取值 8862），原生文本为 <c>Corrupted line in 2d data code model file</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>过了文件头（8860/8861 都放行）之后，正文某一行的内容对不上预期结构。头对行错说明文件"身份"没问题、内容被局部破坏——三码构成读盘错误的递进链条：头部结构(8860)→签名(8861)→正文行(8862)。</para>
	///   <para><b>典型触发</b>文件被截断（网络盘/拷贝中途失败）；文本模式打开了二进制文件再保存；磁盘坏道或压缩-解压工具异常 [待实测：确切成因分布无仓库依据]。模型文件被手工编辑过必撞本码族。</para>
	///   <para><b>处置</b>不要修补，用原始训练数据重新生成模型并重新分发；同步给产线时校验文件大小/哈希。若同一文件偶发时好时坏，先查传输介质而不是软件。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_READ_LINE_FORMAT = 8862;

	/// <summary>码元长宽比不合法时被返回（取值 8863），原生文本为不带参数引号的 <c>Invalid module aspect ratio</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>与 <c>Jl_ERR_DC2D_WRONG_MOD_ASPECT</c>(8853) 构成同义双码：8853 的文本带参数引号（<c>Invalid 'mod_aspect_max'</c>），指向显式参数值被拒；本码不带引号，更像内部流程（训练/推导）得到的码元长宽比越界 [待实测：确切触发路径无仓库依据，两码以取值区分]。</para>
	///   <para><b>典型触发</b>打码设备本身输出极端扁长码元（某些针打/激光工艺）；圆点码与方格码混配；符号尺寸/码元尺寸/间距三个数不自洽（见 8834/8835/8844 的互锁关系）导致推导出的比值越限。</para>
	///   <para><b>处置</b>与 8853 一起核对：显式值改对了仍撞本码，说明问题在图像上的真实比值而不是参数字符串——换标定图重试或放宽 8853 上限。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_MODULE_ASPECT = 8863;

	/// <summary>层数（number of layers）取值不合法时被返回（取值 8864），原生文本为小写的 <c>wrong number of layers</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>"层"在本段上下文里指堆叠式/多层数据码的层数 [待实测：确切指哪种层结构无仓库依据——亦可能是模型金字塔层，但 8901 一节已为金字塔单独设码，故取"码的层"解]。文本全小写、无参数引号，说明它出自内部检查点而非参数名校验路径。</para>
	///   <para><b>典型触发</b>给了 0 或负数；给了该码制不支持的非 1 值；多层码的各层参数数量与声明层数不一致（层数与实际参数组数不匹配这类一致性错误常报本码 [待实测]）。</para>
	///   <para><b>处置</b>先确认目标码制到底分不分层；单层码把层数留默认即可。判分支以取值 8864 为准。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_LAYER_NUM = 8864;

	/// <summary>数据码模型文件的版本号不被当前库支持时被返回（取值 8865），原生文本为小写的 <c>wrong data code model version</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>文件头和签名都过了（8860/8861 放行），唯独版本字段超出可读范围——旧文件在新库或新文件在旧库都会走到这。读盘错误链条里它是"能读但版本不对"这一支。</para>
	///   <para><b>前缀差异</b>本码用 <c>Jl_ERR_DCD_</c> 前缀而同段其余码多为 <c>Jl_ERR_DC2D_</c>，是同一子系统的新旧缩写别名并存 [待实测：为何此码独用 DCD 无仓库依据]；检索错误码时两个前缀都要查，别漏。</para>
	///   <para><b>处置</b>版本问题不可参数化解决：要么升级运行库到写文件的版本，要么用目标版本的工具重新导出一遍模型。产线上软件版本与模型文件版本应做打包一致性检查，撞本码通常是部署事故而不是代码错误。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DCD_READ_WRONG_VERSION = 8865;

	/// <summary>从序列化容器读取时，条目里装的不是有效的 2D 数据码模型时被返回（取值 8866），原生文本为 <c>Serialized item does not contain a valid 2D data code model</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>序列化（通用存盘/读出）体系里的"类型对不上"：容器合法、能打开，但按数据码模型去解释内容失败。与文件族的区别：8860/8861 查的是裸模型文件的头，本码查的是通用序列化条目里的对象身份。</para>
	///   <para><b>典型触发</b>写盘用 A 类型、读盘按 B 类型（存了形状模型却按数据码取回）；序列化文件被部分覆盖后身份区与数据区不一致；跨子系统复用同一个存储文件名/槽位。</para>
	///   <para><b>处置</b>核对写出与读入两侧的模型类型是否同一族——3D 侧有同构码 <c>Jl_ERR_SM3D_NOSITEM</c>(8945)，症状与措辞几乎一样，先按取值确定子系统再查类型。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_NOSITEM = 8866;

	/// <summary>数据码模型的二进制文件格式错误时被返回（取值 8867），原生文本为 <c>Wrong binary (file) format</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>常量名里的 <c>WR</c> 按命名习惯读作 write（写侧），文本却笼统说"二进制格式错"——读写两侧格式错都可能归到这 [待实测：常量名暗示写侧、文本不分读写，确切归属无仓库依据]。它与 8860 的分工：8860 特指模型文件头，本码是整个二进制流层面的通用格式断裂。</para>
	///   <para><b>典型触发</b>目标路径已有同名文件且是文本/别的二进制格式（写入被格式冲突拒绝）；磁盘满导致写出半截后二次覆写；把非模型文件的路径当模型输出路径用。</para>
	///   <para><b>处置</b>检查输出路径是否指向一个已存在的"不像模型"的文件，删掉重试；同时核对磁盘空间与只读属性。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WR_FILE_FORMAT = 8867;

	/// <summary>数据码 2D 的某个参数只在 <c>detection_method='deep_learning'</c> 下可用、但当前检测方法不是深度学习时被返回（取值 8868），原生文本为 <c>Parameter only available with detection_method='deep_learning'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>这是个"参数组合合法性"码而不是"参数值合法性"码：值和名字都对，只是当前检测方法（传统流程）用不了这个 DL 专属参数。数据码 2D 内部把检测方法做成了一等开关 <c>detection_method</c>，DL 路线下才解锁一批参数 [待实测：哪些参数属 DL 专属无仓库清单]。</para>
	///   <para><b>注意</b>这里的深度学习是数据码子系统自带的检测路线，与本仓库已删除的 <c>JlDl</c> 系深度网络包装无关——不要试图去托管层找 DL 配置入口。切到 DL 检测路线通常需要额外的模型/授权支持 [待实测：本库无依据]。</para>
	///   <para><b>处置</b>二选一：删掉该参数（回到传统路线能用的最小参数集），或显式把 <c>detection_method</c> 设为 <c>deep_learning</c>；不要删了检测方法却留着它的专属参数。本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_PARAM_ONLY_AVAILABLE_WITH_DL = 8868;

	/// <summary>数据码 2D 的 <c>alternating_pattern_tolerance_ql</c> 参数取值非法时被返回（取值 8869），原生文本为 <c>Invalid 'alternating_pattern_tolerance_ql'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b><c>alternating_pattern_tolerance</c>(8856) 的带 <c>_ql</c> 后缀姊妹参数 [待实测：QL 后缀含义（码制名/量化方式？）仓库内无任何依据]，同族同型：交替图形的容差数值，各有独立的非法值检查。</para>
	///   <para><b>坑</b>带后缀与不带后缀的两个参数都存在时，最容易犯的错是"改了一个、另一个还在旧值"——参数名精确匹配（8831 一节已述），后缀差一点就是另一个参数。报错时先看清取值是 8856 还是 8869，确定被拒的是哪一个。</para>
	///   <para><b>处置</b>删项走默认再逐个加回。本码 ≥1000 属真错误，统一检查会抛 <c>JlOperatorException</c>；本库无数据码 2D 包装，托管调用路径一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DC2D_WRONG_AP_TOLERANCE_QL = 8869;

	/// <summary>3D 形状匹配的参数被拒时返回的通用码（取值 8900），原生文本为笼统的 <c>Invalid parameter value</c>。</summary>
	/// <remarks>
	///   <para><b>名字与文本的错位</b>常量名是 <c>WRONG_PARAM_NAME</c>（参数名错），原生文本却是"参数值非法"——两种读法指向不同的排错方向（名单不认 vs 值超界）[待实测：本码到底管名还是值，仓库内无依据]。判分支以取值 8900 为准，不要靠名字或文本单边下结论。</para>
	///   <para><b>在 SM3D 段里的位置</b>8901 起为 <c>num_levels</c>、<c>optimization</c>、<c>metric</c> 等具体参数各设了专码；本码是专码没覆盖到的参数的兜底分支——命中它说明被拒的参数没有更细的码可查，需要靠调用点的参数列表自行定位。</para>
	///   <para><b>处置</b>同 8831 的思路：先删掉可疑参数走默认跑通，再逐个加回；比通用码更有效的是把原生报错文本与参数列表一起打出来。本码 ≥1000 属真错误，统一检查会抛 <c>JlOperatorException</c>；本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_PARAM_NAME = 8900;

	/// <summary>3D 形状匹配的 <c>num_levels</c>（金字塔层数）参数取值非法时被返回（取值 8901），原生文本为 <c>Invalid 'num_levels'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>匹配在缩略金字塔上做，层数决定"从多粗的尺度开始搜"。非法值（0、负、超模型可支撑层数 [待实测：上界依模型而定]）报本码；注意它与 <c>lowest_model_level</c>(8908) 配对：一个定顶、一个定底，两者给出的区间不交或越出模型自身层级时也可能被拒（此时报哪个码无依据 [待实测]）。</para>
	///   <para><b>与 2D 段的同名参数区分</b>数据码段没有为 num_levels 设专码（见 8830 通用值码），而 SM3D 给它单设一码——同名参数在两个子系统里校验强度不同，跨子系统抄参数写法时不要想当然。</para>
	///   <para><b>取舍</b>层数少：快但漏大姿态差的目标；层数多：稳但耗时且小目标可能被缩掉 [待实测：具体影响曲线]。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_NUM_LEVELS = 8901;

	/// <summary>3D 形状匹配的 <c>optimization</c>（优化方式）参数取值非法时被返回（取值 8902），原生文本为 <c>Invalid 'optimization'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型训练/匹配路径的优化档位选择，取值是枚举字符串（各档位名单与语义 [待实测：仓库无清单]）。作为模型创建期参数，改它通常要求重新训练模型才生效，只改调用参数不动已存模型是常见的"改了没反应"错法 [待实测]。</para>
	///   <para><b>典型触发</b>把别的子系统 optimization 类参数的取值（如数值 0/1）抄进这个字符串枚举；大小写不一致；旧库不认识新增档位。</para>
	///   <para><b>处置</b>删项走默认；确认报错针对参数值还是模型数据版本——前者改写法、后者重新导出模型。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_OPTIMIZATION = 8902;

	/// <summary>3D 形状匹配的 <c>metric</c>（评分度量）参数取值非法时被返回（取值 8903），原生文本为 <c>Invalid 'metric'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>决定得分按什么口径计算（如对'漏检'与'误检'哪一侧更苛刻的取向 [待实测：具体度量名单与各自语义仓库无依据]）。本码只指取值不在枚举内。</para>
	///   <para><b>坑</b>换度量等于换得分量纲：<c>min_score</c> 一类阈值和统计报表在两种度量下不可直接比较；改了本参数必须连带复核所有基于分数的过滤阈值，否则会出现"模型没变、通过率大变"的假象 [待实测]。这与 <c>recompute_score</c>(8913) 是同一族"分数一致性"问题。</para>
	///   <para><b>处置</b>照抄合法档位名、区分大小写；调试期用默认度量。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_METRIC = 8903;

	/// <summary>3D 形状匹配的 <c>min_face_angle</c>（最小面夹角）参数取值非法时被返回（取值 8904），原生文本为 <c>Invalid 'min_face_angle'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型三角面之间夹角小于该值时两面近似共面，会被合并/剔除以瘦身模型；它是模型创建期参数 [待实测：合并策略细节无仓库依据]。角度量纲（度/弧度）与允许区间（应为 0 到某上界 [待实测]）非法即报本码。</para>
	///   <para><b>坑</b>设太大把有效棱边也抹平，模型对姿态变化的区分力下降；与 8944（模型无任何面）有因果链——min_face_angle 与 <c>min_size</c>(8905) 双开过头可能把面筛光 [待实测：是否确由此路径触发 8944]。角度单位混淆是本仓库角度类参数的高频坑。</para>
	///   <para><b>处置</b>从默认起步逐步收紧；改完必须重新训练/重建模型再看效果。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_MIN_FACE_ANGLE = 8904;

	/// <summary>3D 形状匹配的 <c>min_size</c>（最小尺寸）参数取值非法时被返回（取值 8905），原生文本为 <c>Invalid 'min_size'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型瘦身下限：小于该尺寸的几何细节在创建期被丢弃。单位跟随模型自身的长度量纲（通常为毫米 [待实测：确切单位与允许区间无仓库依据]）——从 CAD 直接来的点云与标定后的模型量纲不同，复制参数值必翻车。</para>
	///   <para><b>典型触发</b>负数、0 或超界数值（本码）；设得过大导致面被筛光则是另一个症状，走 8944 一类的空模型错误而不是本码 [待实测：因果对应关系]。与 <c>part_size</c>(8909)、<c>min_face_angle</c>(8904) 同属"模型精度换速度"的一组，调整应一次只动一项。</para>
	///   <para><b>处置</b>先删项走默认重建模型；核对模型来源文件的单位声明再填值。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_MIN_SIZE = 8905;

	/// <summary>3D 形状匹配的 <c>model_tolerance</c>（模型容差）参数取值非法时被返回（取值 8906），原生文本为 <c>Invalid 'model_tolerance'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型几何与实际工件之间允许的偏差幅度（点云与真物的配准残差容忍 [待实测：确切作用环节无仓库依据]）。数值类非法（负数、超界、非数）报本码。</para>
	///   <para><b>坑</b>它常被当成"匹配松紧"的万能旋钮：姿态搜索的松紧在视角范围(8920~8927)与容差类参数里，本参数管的是几何偏差 [待实测：分工按名推断]。工件本身尺寸公差大（冲压件热胀冷缩）才动它，光照/污损问题动它无效。</para>
	///   <para><b>处置</b>量纲核对是第一步（毫米还是米、角度还是比例 [待实测]）。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_MODEL_TOLERANCE = 8906;

	/// <summary>3D 形状匹配的 <c>fast_pose_refinment</c>（快速位姿精化）参数取值非法时被返回（取值 8907），原生文本为 <c>Invalid 'fast_pose_refinment'</c>。</summary>
	/// <remarks>
	///   <para><b>必抄的拼写坑</b>原生文本把这个参数拼作 <c>refinment</c>——比规范英文拼法 refinement 少了一个 e。参数名必须逐字照抄：写成 <c>fast_pose_refinement</c> 会先撞 8900 一类的"参数不认识/非法"分支而不是本码 [待实测：错拼时落到哪个码无依据]。这与数据码段的 <c>classificator</c>(8848) 是同一类"原生故意拼错"的名单项。</para>
	///   <para><b>含义</b>快速位姿精化的开关/档位，与 <c>pose_refinement</c>(8930) 是两个独立旋钮：一个管快速路径、一个管精化等级 [待实测：两者叠加/互斥关系无仓库依据]。布尔或档位写法非法报本码。</para>
	///   <para><b>处置</b>先对照原生文本逐字母核对拼写，再核对写法。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_FAST_POSE_REF = 8907;

	/// <summary>3D 形状匹配的 <c>lowest_model_level</c>（最低模型层级）参数取值非法时被返回（取值 8908），原生文本为 <c>Invalid 'lowest_model_level'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>金字塔匹配允许下沉到的最低（最细）模型层级，与 <c>num_levels</c>(8901) 共同圈定层级窗口 [待实测：两者合用的约束方向按名推断]。取值超出模型实际拥有层级时报本码——模型是"训练时"决定层级的，调用时填多小的合法数都可能越它的界。</para>
	///   <para><b>坑</b>同一模型换到低分辨图像上跑，原本合法的层级值会突然非法或退化 [待实测：报本码还是静默降层]；这是"参数依赖上游模型/图像属性"的典型形态，单看参数值没问题不等于没问题。</para>
	///   <para><b>处置</b>对照模型创建参数核对层级窗口；必要时重建模型而不是压低该值。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_LOWEST_MODEL_LEVEL = 8908;

	/// <summary>3D 形状匹配的 <c>part_size</c>（子块尺寸）参数取值非法时被返回（取值 8909），原生文本为 <c>Invalid 'part_size'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型空间被切成多大一块来组织检索（八叉树/网格式细分的粒度 [待实测：具体数据结构无仓库依据]）。子块越小定位越细、内存与时间开销越大；量纲跟随模型单位（毫米级 [待实测]），非法数值报本码。</para>
	///   <para><b>坑</b>子块尺寸与目标尺寸的关系决定命中质量：比目标上的特征还大的子块会糊掉细节 [待实测：退化方向]。它与 <c>min_size</c>(8905)、<c>num_levels</c>(8901) 同属"模型分辨率"一族，三者要按同一量纲一起定，不要各抄各的。</para>
	///   <para><b>处置</b>删项走默认重建模型验证基线，再单点调。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_PART_SIZE = 8909;

	/// <summary>模型投影到图像上占幅过大（超出可处理的图像尺寸）时被返回（取值 8910），原生文本为 <c>The projected model is too large (increase the value for DistMin or the image size in CamParam)</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>不是参数写法错，而是几何量纲不自洽：相机到模型的距离太近（或相机参数给的视野太小），以致模型渲染出的模板超出内部缓冲的允许范围。原生文本自带两条出路：增大 <c>dist_min</c>(8926) 或增大相机参数里的图像尺寸。</para>
	///   <para><b>注意</b>本库托管层的相机参数类型（JlCamPar 一族）已删除，"改 CamParam"这条路无法在托管 API 上直接走 [待实测：现版本原生侧是否仍接受该参数名]；实际可动的是距离范围参数或换更大视野/更远安装距离。</para>
	///   <para><b>典型触发</b>把毫米单位模型配到"按米标定"的场景、或用微距视角匹配大工件。本码 ≥1000 属真错误，统一检查会抛 <c>JlOperatorException</c>；本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_PROJECTION_TOO_LARGE = 8910;

	/// <summary>3D 形状匹配的 <c>opengl_accuracy</c>（渲染精度）参数取值非法时被返回（取值 8911），原生文本为 <c>Invalid 'opengl_accuracy'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型投影渲染的精度档位 [待实测：档位名单与各自含义无仓库依据]。3D 匹配在生成视角模板时要渲染模型，本参数控制渲染质量换速度。</para>
	///   <para><b>背景</b>本库托管层的显示/绘制族已删除，但"OpenGL"在这里是原生核内部渲染路径的名字，不代表托管层还留着窗口/绘制 API——不要为排查本码去找任何 Disp/Window 类接口。无 GPU/远程桌面环境下该参数是否可用、行为如何，仓库内无依据 [待实测]。</para>
	///   <para><b>处置</b>写法非法报本码；渲染质量导致的匹配退化不报码（表现为得分低/姿态偏），那是调档位而不是改拼写的问题。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_OPENGL_ACCURACY = 8911;

	/// <summary>3D 形状匹配的 <c>recompute_score</c>（重算得分）参数取值非法时被返回（取值 8913），原生文本为 <c>Invalid 'recompute_score'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>布尔/档位型参数：匹配完成后是否用更高代价重算最终得分 [待实测：确切语义与写法名单无仓库依据]。本码只在取值被拒时出现。注意取值序列有跳号（8912 未定义），检索同段码时别按连续号段猜。</para>
	///   <para><b>坑</b>开与关的得分可能不同源，阈值若写死，两种设置之间不可移植——统一产线/实验室的该开关状态再比较分数，否则调参结论互相打架 [待实测：差异幅度]。与 <c>metric</c>(8903) 同属"分数一致性"一族（见 8903 的坑段）。</para>
	///   <para><b>处置</b>写法逐字照抄合法值；本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_RECOMPUTE_SCORE = 8913;

	/// <summary>3D 形状匹配的 <c>longitude_min</c>（视角经度下限）参数取值非法时被返回（取值 8920），原生文本为 <c>Invalid 'longitude_min'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型训练模板在观察球上的经度采样起点——决定"从哪些方向给物体拍模板"。本码指数值非法：超出经度允许域、或（合理推断）与 <c>longitude_max</c>(8921) 构成倒挂区间 [待实测：区间颠倒报哪个码无仓库依据]。角度单位（度/弧度）先核对再填值。</para>
	///   <para><b>坑</b>视角范围越大模板越多、模型越大、训练与匹配越慢；范围设窄则范围外姿态必漏检且不报错——本参数族是"漏检但静默"的头号来源，报本码反而是幸运的显式反馈。</para>
	///   <para><b>处置</b>min/max 成对检查（经、纬、滚转、距离四对见 8920~8927）；改完需重建模型，模板在训练期就固定了 [待实测]。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_LON_MIN = 8920;

	/// <summary>3D 形状匹配的 <c>longitude_max</c>（视角经度上限）参数取值非法时被返回（取值 8921），原生文本为 <c>Invalid 'longitude_max'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>经度采样终点，与 <c>longitude_min</c>(8920) 成对；取值域、单位约定与"范围大模板多、范围窄静默漏检"的取舍同 8920 一节。</para>
	///   <para><b>典型触发</b>只改 min 不改 max 造成区间倒挂或超域；跨圈写法（让区间跨越经度 0 点/接缝 [待实测：经度接缝处是否允许 min&gt;max 的回绕写法]）；度/弧度混填。</para>
	///   <para><b>处置</b>把四对视角参数（8920~8927）当成一个"观察窗口"整体核对，改对后重建模型。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_LON_MAX = 8921;

	/// <summary>3D 形状匹配的 <c>latitude_min</c>（视角纬度下限）参数取值非法时被返回（取值 8922），原生文本为 <c>Invalid 'latitude_min</c>（原生串里这个参数名的收尾引号本来就缺失，按文本匹配时不要自己补上）。</summary>
	/// <remarks>
	///   <para><b>含义</b>观察球纬度采样起点，与 <c>latitude_max</c>(8923) 成对，语义框架同经度对（8920/8921）：范围决定模板覆盖的方向带与数量/耗时。纬度在球面上有奇点（顶部/底部 [待实测：极点附近采样是否退化]），区间贴边填写比中间值更容易触发连锁问题。</para>
	///   <para><b>典型触发</b>度/弧度混填；与 max 倒挂；把经度的允许区间习惯值照搬给纬度（两轴允许域不同 [待实测：各自允许域无仓库依据]）。</para>
	///   <para><b>处置</b>与 8920~8927 整体核对后重建模型。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_LAT_MIN = 8922;

	/// <summary>3D 形状匹配的 <c>latitude_max</c>（视角纬度上限）参数取值非法时被返回（取值 8923），原生文本为 <c>Invalid 'latitude_max'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>纬度采样终点，与 <c>latitude_min</c>(8922) 成对；纬度带通常不必给满全球——顶部/底部看不到新面时，收窄它能显著减模板数与耗时 [待实测：耗时与区间宽度的实际关系]。</para>
	///   <para><b>典型触发</b>只改 8922 的 min 造成区间不成立；单位混填；从别的机型工程照抄的区间超出本库允许域。</para>
	///   <para><b>处置</b>视角四对参数（8920~8927）整体核对，改后重建模型。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_LAT_MAX = 8923;

	/// <summary>3D 形状匹配的 <c>cam_roll_min</c>（相机滚转角下限）参数取值非法时被返回（取值 8924），原生文本为 <c>Invalid 'cam_roll_min'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>第四对视角参数：相机绕光轴平面内旋转（roll）的采样下限。前三对（经/纬）定向"从哪看"，本对定向"画面怎么转"。单位（度/弧度）与允许域 [待实测] 非法报本码。</para>
	///   <para><b>命名坑</b>常量名是 <c>ROL_MIN</c>（比参数名少一个 L），不是笔误——按常量名检索错误码时别用 <c>ROLL</c> 去搜而漏掉本码与 8925。原生参数名 <c>cam_roll_*</c> 则拼全。</para>
	///   <para><b>取舍</b>对姿态不敏感、工件不会在画面内旋转的工况，滚转区间给窄能大幅省模板；给满一整圈则模板数成倍增长 [待实测：倍增系数]。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_ROL_MIN = 8924;

	/// <summary>3D 形状匹配的 <c>cam_roll_max</c>（相机滚转角上限）参数取值非法时被返回（取值 8925），原生文本为 <c>Invalid 'cam_roll_max'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>滚转角采样上限，与 <c>cam_roll_min</c>(8924) 成对；单位、允许域与"给满整圈模板暴涨"的取舍同 8924 一节。常量名同样少一个 L（<c>ROL_MAX</c>），检索时留意。</para>
	///   <para><b>典型触发</b>只改下限造成区间倒挂；工件会整圈旋转的场合把上限按"稍大于 0"填——合法但覆盖不足，真正漏检时不再报本码族而表现为检不到。</para>
	///   <para><b>处置</b>滚转对与经纬对联合核对：任何一个轴给宽都会让模板总量按乘积增长 [待实测：是否正比]。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_ROL_MAX = 8925;

	/// <summary>3D 形状匹配的 <c>dist_min</c>（观察距离下限）参数取值非法时被返回（取值 8926），原生文本为 <c>Invalid 'dist_min'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>视角采样的相机距离下限（模型单位量纲 [待实测]）。它与 <c>dist_max</c>(8927) 定出"多近到多远"的壳层；距离太近还会把投影撑爆——8910 的原生建议第一条就是"增大 DistMin"，两码在几何上联动。</para>
	///   <para><b>典型触发</b>0 或负数；大于 <c>dist_max</c> 的倒挂；从别的安装距离照抄。合法但过小的下限不报码、却在匹配时撞 8910 [待实测：联动条件]。</para>
	///   <para><b>处置</b>按真实工况的安装距离 ± 工件在工位上的高度波动来定区间，别给整段"保险"大区间：距离每加一档模板数就乘一档。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_DIST_MIN = 8926;

	/// <summary>3D 形状匹配的 <c>dist_max</c>（观察距离上限）参数取值非法时被返回（取值 8927），原生文本为 <c>Invalid 'dist_max'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>视角采样的相机距离上限，与 <c>dist_min</c>(8926) 成对；量纲、倒挂检查与"区间乘积效应"同 8926 一节。</para>
	///   <para><b>坑</b>距离决定同一物体的成像尺度：上限给太大时远处模板里小特征分辨不出来，等于白训练 [待实测：退化表现]；下限/上限之间的层间距如何取样由内部决定，仓库无依据可调 [待实测]。</para>
	///   <para><b>处置</b>核对区间后重建模型；若目标是"同一模型适配两种安装高度"，用区间覆盖而不是复制两份工程。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_DIST_MAX = 8927;

	/// <summary>3D 形状匹配的 <c>num_matches</c>（最大返回匹配数）参数取值非法时被返回（取值 8928），原生文本为 <c>Invalid 'num_matches'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>一帧最多返回多少个位姿实例。0、负数或超内部上限报本码 [待实测：上限取值无仓库依据]；填 1 与填大写在合法范围内不报错，区别在结果——只要前 N 个得分最高的实例，顺序语义依赖原生实现 [待实测：输出是否按得分降序]。</para>
	///   <para><b>坑</b>下游若按下标取"第 k 个匹配"，把本参数从 5 改成 2 会让越界访问浮出来（对照 2D 段 8825 的候选越界码，3D 段没有对应专码 [待实测]）；配合 <c>max_overlap</c>(8929) 一起理解：返回多实例时重叠控制决定这 N 个是不是"真"的多目标。</para>
	///   <para><b>处置</b>单目标工况就填 1 并在下游断言结果数；多目标按阵列容量填。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_NUM_MATCHES = 8928;

	/// <summary>3D 形状匹配的 <c>max_overlap</c>（最大重叠度）参数取值非法时被返回（取值 8929），原生文本为 <c>Invalid 'max_overlap'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>多实例结果之间允许的最大重叠比例（约定应为 0~1 的小数 [待实测：量纲与允许域无仓库依据]），用于抑制同一目标产生的重复峰。本码只针对数值非法（写法/越界），与取值策略是否合理无关。</para>
	///   <para><b>坑</b>它只在 <c>num_matches</c>(8928) 大于 1 时才有意义——单实例下改它没有任何可见效果，容易被误判为"参数不生效"而乱调一族。目标密集、靠得很近的阵列场景要配小重叠与合适的 N 一起调。</para>
	///   <para><b>处置</b>越界（如按百分制填 50）先查量纲。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_MAX_OVERLAP = 8929;

	/// <summary>3D 形状匹配的 <c>pose_refinement</c>（位姿精化）参数取值非法时被返回（取值 8930），原生文本为 <c>Invalid 'pose_refinement'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>粗匹配后是否/以什么力度做位姿精化的档位参数 [待实测：档位名单与各自语义无仓库依据]。与 <c>fast_pose_refinment</c>(8907) 是两个参数：本码管精化档位，8907 管"快速精化"路径的开关 [待实测：两者叠加关系]；检索/改参时别互相顶替。</para>
	///   <para><b>坑</b>精化改变输出的位姿与可能的得分口径，下游若把匹配结果喂给标定/机器人示教，两种档位下的位姿序列不可混用比较；档位枚举写法大小写敏感。</para>
	///   <para><b>处置</b>先删项走默认，确认拼写后再加回。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_POSE_REFINEMENT = 8930;

	/// <summary>3D 形状匹配的 <c>cov_pose_mode</c>（位姿协方差模式）参数取值非法时被返回（取值 8931），原生文本为 <c>Invalid 'cov_pose_mode'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>控制是否/如何输出位姿协方差（姿态不确定度）的模式开关 [待实测：模式名单与协方差的精确语义无仓库依据]。协方差用于下游评估定位可信度、做卡尔曼类融合，不需要就不开。</para>
	///   <para><b>典型触发</b>枚举字符串写法不对；把别的子系统协方差/不确定度类参数的取值习惯（数值 0/1）照搬过来。本码出现说明模式值本身被拒。</para>
	///   <para><b>处置</b>删项走默认；确认输出里是否真有协方差字段再调下游，不要先写消费代码再回头开模式。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_COV_POSE_MODE = 8931;

	/// <summary>3D 形状匹配的 <c>outlier_suppression</c>（离群点抑制）参数取值非法时被返回（取值 8932），原生文本为 <c>In. 'outlier_suppression'</c>——原生串本身把 Invalid 截成了 <c>In.</c>，按文本匹配时别补全。</summary>
	/// <remarks>
	///   <para><b>含义</b>匹配时忽略图像中与模型不符的边缘/特征点（遮挡、脏污、反光拉丝）的力度档位 [待实测：档位名单]。常量名 <c>OUTLIER_SUP</c> 是缩写，原生参数名是完整的 <c>outlier_suppression</c>。</para>
	///   <para><b>取舍</b>抑制越强，部分遮挡越能出姿态，但把"其实不该匹配"的残缺形状也凑成匹配——安全检出的场合宁可弱抑制加高得分门槛，别只拉本档位 [待实测：两者联合行为]。</para>
	///   <para><b>处置</b>写法逐字核对；遮挡是常态的工位应在训练期就把被遮区域排除出模型而不是长期依赖本参数。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_OUTLIER_SUP = 8932;

	/// <summary>3D 形状匹配的 <c>border_model</c>（边界轮廓模型）参数取值非法时被返回（取值 8933），原生文本为 <c>Invalid 'border_model'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>是否/如何把模型在图像上的投影轮廓也纳入匹配特征 [待实测：确切取值形态——布尔或含'cutout'一类档位]。对"只有剪影可看"的工件（无表面纹理的冲压件）该开关决定可不可用。</para>
	///   <para><b>坑</b>轮廓参与匹配的模型对分割噪声敏感（边缘毛刺、阴影都会被当成轮廓）；开启后得分基线变化，与关闭时的 min_score 类阈值不可互换比较——这与 8903/8913 的"分数一致性"是同一类连锁。</para>
	///   <para><b>处置</b>写法非法报本码；效果不对不报码。作为训练期参数，改后要重建模型 [待实测]。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_BORDER_MODEL = 8933;

	/// <summary>匹配算出的位姿数学上不适定（无法唯一确定）时被返回（取值 8940），原生文本为 <c>Pose is not well-defined</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>不是参数写法错：观测的几何约束不足以钉死 6 自由度位姿——旋转对称工件绕对称轴转任意角度都"一样"、或近似平面对称的姿态让协方差退化 [待实测：确切退化判据无仓库依据]。属状态/几何错误，改参数拼写无效。</para>
	///   <para><b>与相邻码区分</b>8920~8933 一族都是"值被拒"，本码是 8940 起"算得出但解不合法"分支的第一员；同族还有文件/序列化类（8941~8946）——号段内部就分了三类成因，别混着排查。</para>
	///   <para><b>处置</b>对对称件：在结果侧对不可分辨自由度做规范化（如把旋转折叠到基本区间）或人为破对称（贴标记）；对退化姿态：收窄视角范围(8920~8927)排除病态采样带。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_UNDEFINED_POSE = 8940;

	/// <summary>3D 形状模型文件的格式无效（读不出/不是合法 SM3D 文件）时被返回（取值 8941），原生文本为 <c>Invalid file format for 3D shape model</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>常量名读作"没有 SM3D 文件"，原生文本说的是"格式无效"——两读法共同的落点是"这个文件没能作为 3D 形状模型被读起来"。路径给错（指到目录/别的类型文件）、文件被文本方式转存过，都归本码 [待实测：文件不存在时报本码还是通用文件错误，无仓库依据]。</para>
	///   <para><b>与相邻码分工</b>8941 格式不可读、<c>Jl_ERR_SM3D_WRONG_FILE_VERSION</c>(8942) 可读但版本不支持、<c>Jl_ERR_SM3D_MTL</c>(8943) 可读但版本等级拒绝、<c>Jl_ERR_SM3D_NOSITEM</c>(8945) 序列化容器身份不符——四码覆盖"读模型失败"的不同原因，先按取值定性再决定是重导文件还是升库。</para>
	///   <para><b>处置</b>重新导出/分发模型文件并校验哈希；不要试图修补二进制。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_NO_SM3D_FILE = 8941;

	/// <summary>3D 形状模型的版本不被支持时被返回（取值 8942），原生文本为 <c>The version of the 3D shape model is not supported</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>文件能通过格式检查（8941 放行）但版本字段超出支持范围——新库读旧模型是否兼容、旧库读新模型一律拒绝 [待实测：兼容矩阵无仓库依据]。与数据码段的 8865 完全同构，3D/2D 两子系统各自留了一枚版本码。</para>
	///   <para><b>典型触发</b>产线库版本回滚后模型文件留在高版本格式；跨机器同步时模型比运行库"新"。CI 里模型与库版本要成对锁定。</para>
	///   <para><b>处置</b>用匹配版本的工具重导模型，或升运行库；参数级修改无济于事。本码 ≥1000 属真错误，统一检查会抛 <c>JlOperatorException</c>；本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_FILE_VERSION = 8942;

	/// <summary>该 3D 形状模型只有 Vision XL（高配版本）才能读取时被返回（取值 8943），原生文本为 <c>3D shape model can only be read by Vision XL</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>版本/授权等级限制而非数据损坏：模型本身合法，只是当前运行库的等级不够。常量名 <c>MTL</c> 的展开仓库内无文档可断（Metal？Multi-Template-Level？[待实测]），但文本已把话说死——非 XL 拒读。</para>
	///   <para><b>坑</b>这类码改参数、重导文件都治不了：要么升级授权等级，要么从源头别用 XL 专属格式生成模型。产线部署前要确认模型生成端与运行端的版本等级一致；排查时先看许可证报告再看文件。</para>
	///   <para><b>与 8942 区分</b>8942 是"文件格式版本超出支持"（时间维度），本码是"功能等级不足"（授权维度）；处置路径完全不同。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_MTL = 8943;

	/// <summary>3D 对象模型里没有任何面（三角面为空）时被返回（取值 8944），原生文本为 <c>3D object model does not contain any faces</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>3D 形状匹配把模型化成三角面集来渲染投影；面集为空则一切免谈。常见来源：源模型本来就只有点云/线框没有做三角化 [待实测：本库无三角化工具可指]；或 <c>min_face_angle</c>(8904)、<c>min_size</c>(8905) 一类瘦身参数把关掉了全部面。</para>
	///   <para><b>坑</b>名字里的 OM3D（object model）提示它也可能在"对象模型转形状模型"的阶段触发 [待实测：本库无对象模型包装类，此类转换从何而来无依据]——报本码先分清是源文件空还是被参数筛空，前者换文件、后者回退 8904/8905。</para>
	///   <para><b>处置</b>瘦身参数一次只放宽一项并重建模型验证；点云源先用外部工具导出带面的网格模型。本码 ≥1000 属真错误，统一检查会抛 <c>JlOperatorException</c>；本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_NO_OM3D_FACES = 8944;

	/// <summary>序列化条目里装的不是有效的 3D 形状模型时被返回（取值 8945），原生文本为 <c>Serialized item does not contain a valid 3D shape model</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>通用存盘容器能开、但按 3D 形状模型解释内容失败——与数据码段的 <c>Jl_ERR_DC2D_NOSITEM</c>(8866) 逐字同构，只差对象类型名。两码同时提示"序列化槽位的类型与读回类型不一致"。</para>
	///   <para><b>典型触发</b>写读两侧类型不匹配（存的是别的模型、按 SM3D 取回）；同一槽位被两类模型先后覆写；文件截断后校验区与数据区脱节 [待实测：截断究竟撞本码还是 8941 一族]。</para>
	///   <para><b>处置</b>先核对写/读两侧的模型类型再怀疑文件；归档时对模型文件记录生成它的算子族。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_NOSITEM = 8945;

	/// <summary>3D 形状匹配的 <c>union_adjacent_contours</c>（相邻轮廓合并）参数取值非法时被返回（取值 8946），原生文本为 <c>Invalid 'union_adjacent_contours'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>布尔/档位型：模型投影轮廓里相邻的小轮廓是否先合并成一条再参与匹配 [待实测：确切行为无仓库依据]。合并可减少碎片轮廓噪声、但会糊掉细节。</para>
	///   <para><b>坑</b>与 8933（border_model）联动的参数：轮廓不参与匹配时本开关自然无效果——"改了没反应"先确认 8933 的路线是否开着 [待实测：依赖方向]。取值非法报本码，效果不佳不报错。</para>
	///   <para><b>处置</b>写法逐字核对，作为训练期参数改后重建模型 [待实测：属训练期还是运行期]。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_SM3D_WRONG_UNION_ADJACENT_CONTOURS = 8946;

	/// <summary>位姿估计模型内含信息量不足（无法据以估姿）时被返回（取值 8947），原生文本为 <c>Pose estimation model contains insufficient information</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型文件读进来了、但做 6 自由度位姿估计所需的信息不齐全——例如模型只有平面/轮廓级约束，钉不住全部自由度 [待实测：具体缺哪类信息无仓库依据；与 8940 的"解算退化"是一对：8940 是观测算不出唯一解，本码是模型本身就不带够约束]。</para>
	///   <para><b>前缀注意</b>本码用 <c>DM3D_</c> 前缀而非 SM3D_，落在 3D 段末尾（8946 与描述符段 8960 之间）[待实测：DM3D 指哪个模型族无仓库依据，从号段位置看与 3D 形状匹配同子系统]。跨前缀检索时三个前缀 SM3D/DM3D/OM3D 都要看。</para>
	///   <para><b>处置</b>换用带完整几何的模型重建；纯平面工件考虑改用 2D/平面类匹配算子而不是硬补参数 [待实测：本库可用的替代算子需另行核对]。本库托管层无 3D 形状匹配包装，一般不产生本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DM3D_NO3DPOSEEST = 8947;

	/// <summary>描述符模型文件无法作为合法模型读出（常量名意为"无描述符文件"，原生文本为格式无效）时被返回（取值 8960），原生文本为 <c>Invalid file format for descriptor model</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>名字（NO DESCRFILE）与文本（invalid file format）给了两种读法：文件缺失 vs 格式非法。合理推断文件不存在会先被通用文件错误拦下，本码更多覆盖"文件在但读不出" [待实测：两者归属无仓库依据]。判错以取值 8960 为准。</para>
	///   <para><b>同族路线</b>描述符匹配自己的版本码是 <c>Jl_ERR_DESCR_WRDESCRVERS</c>(8961)——8960/8961 与本段 3D 侧 8941/8942 的"格式错/版本错"两分法完全同构，跨子系统排查可套用同一决策树：先重导文件，再对版本。</para>
	///   <para><b>子系统状态</b>描述符模型（JlDescriptorModel 一族）在本托管库已删除、无包装类型，本码一般只在原生核或跨边界复用句柄时出现 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DESCR_NODESCRFILE = 8960;

	/// <summary>描述符模型的版本不被支持（取值 8961），原生文本为 <c>The version of the descriptor model is not supported</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型文件格式能解析，但内嵌版本号超出当前原生内核的支持范围——高版本库训的模型拿到低版本运行时来读是典型方向（反向兼容与否 [待实测]）。与 8960 分诊：8960 是格式读不出（重导/重训），本码格式没问题、版本不对口（升运行时，或用旧库重存文件）。</para>
	///   <para><b>同构路线</b>与 3D 族 8941/8942、描述符读档三兄弟 8960/8961/8988 同一套“格式错/版本错/身份错”决策树，跨子系统排查可直接套用（8960 块内有对照说明）。</para>
	///   <para><b>子系统状态</b>本托管库已删除描述符模型包装，正常路径不触发本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DESCR_WRDESCRVERS = 8961;

	/// <summary>数值参数 'radius' 非法（取值 8962），原生文本为 <c>Invalid 'radius'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>描述符族的圆邻域半径非法（常量名 CIRC_RADIUS 比原生文本 'radius' 多出的 CIRC 提示这是圆形邻域半径 [待实测：单位应为像素，仓库无实证]）。负值、0、超过相关窗口/图像尺度是常见病灶形态，精确上下界无据 [待实测]。</para>
	///   <para><b>段首规律</b>8962–8983 是描述符匹配的逐参数数值校验段，命名模式 WRONG_NUM_参数名、码序与参数序大体对应——拿码名即可定位到具体参数，是本库参数类错误里定位成本最低的一段。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_CIRC_RADIUS = 8962;

	/// <summary>数值参数 'check_neighbor' 非法（取值 8963），原生文本为 <c>Invalid 'check_neighbor'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>邻域检查参数非法。邻域三件套的分工（按参数名推断 [待实测]）：8962 radius 定邻域范围，本码定查不查邻域或查几层，8964 min_check_neighbor_diff 定判“相似”的差值线。本码取值语义更像计数/开关，负值与超界大值是典型病灶。</para>
	///   <para><b>子系统状态</b>本托管库无描述符模型包装，本码在 C# 正常路径不触发（8962–8983 全段同此）[待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_CHECK_NEIGH = 8963;

	/// <summary>数值参数 'min_check_neighbor_diff' 非法（取值 8964），原生文本为 <c>Invalid 'min_check_neighbor_diff'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>邻域检查的最小差异阈值非法：按名推断，两特征点差异小于此值即被判为“太像”而抑制其一，用来对特征点做稀疏化去重 [待实测：抑制方向为名字推断]。与 8962（radius 管邻域多大）、8963（check_neighbor 管查不查/查几个）合成邻域三件套。</para>
	///   <para><b>检索提醒</b>原生参数名带 _diff 尾缀、常量名截到 NEIGH 为止，两处名字不等长；按名字查原生文档时两个都要试。阈值越大产点越稀，调过头会撞 8977（点太少）。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_MIN_CHECK_NEIGH = 8964;

	/// <summary>数值参数 'min_score' 非法（取值 8965），原生文本为 <c>Invalid 'min_score'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>整体最低分数阈值参数非法。本库匹配族得分口径惯为 0–1（形状族文档与示例皆在此范围），传负值或大于 1 是首选怀疑 [待实测：描述符族的精确边界与开闭区间]。与 8972（min_score_descr）分处两段流程，一个卡总体接受线、一个卡描述符比对线。</para>
	///   <para><b>别混诊</b>“min_score 设太高导致 0 结果”不报错——那是静默空结果；能走到本码说明传进去的值本身没过参数校验。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_MIN_SCORE = 8965;

	/// <summary>数值参数 'sigma_grad' 非法（取值 8966），原生文本为 <c>Invalid 'sigma_grad'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>梯度计算级的高斯平滑尺度非法（sigma 类参数以像素为单位、必须为正 [待实测：单位与上下界为该族惯例推定，无仓库实证]）。与 8967 的 sigma_smooth 分管两级平滑，两值共同决定描述符对噪声与细节的平衡。</para>
	///   <para><b>取向</b>高反光、强噪声现场加大 sigma_grad 比事后滤波更对路；但加过头会把弱边缘抹平，特征点数随之跌到撞 8977——两码常是同一调参过程的两端。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_SIGMAGRAD = 8966;

	/// <summary>平滑 sigma 参数（原生文本 'sigma_smooth'）非法（取值 8967），原生文本为 <c>Invalid 'sigma_smooth'</c>。</summary>
	/// <remarks>
	///   <para><b>名实错位提醒</b>常量名叫 SIGMAINT、原生文本却是 sigma_smooth，本段家族里唯一一处常量名与参数名不一致 [待实测：原生参数是否曾用 sigma_int 一类旧名]。对参数表核对时以 sigma_smooth 为准，全文检索两个名字都要搜，只按常量名搜文档会一无所获。</para>
	///   <para><b>配对</b>与 8966（sigma_grad）分别是梯度级与平滑级的高斯尺度：sigma 越大越抗噪但细节越糊，为一般规律 [待实测：本族经验区间]；两类 sigma 都必须为正，0 或负值撞本码。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_SIGMAINT = 8967;

	/// <summary>数值参数 'alpha' 非法（取值 8968），原生文本为 <c>Invalid 'alpha'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>名为 alpha 的数值参数非法。alpha 这类希腊字母名在多义（旋转角、加权系数、角度步长都可能叫它），描述符族里它的确切语义与值域仓库内无据 [待实测]——不要拿其他算子族见过的 alpha 值直接套，先对照该算子的原生参数表定口径。</para>
	///   <para><b>排查顺序</b>本段码号次序与参数次序大致一致：报 8968 时 8962–8967 对应的诸参数多数已过校验，病灶优先看 alpha 自身 [待实测：原生是否严格按此序逐参校验无仓库实证]，这比笼统的 9021 好定位。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_ALPHA = 8968;

	/// <summary>数值参数 'threshold' 非法（取值 8969），原生文本为 <c>Invalid 'threshold'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>描述符族自带的判定阈值参数越界/越型，作用在特征提取或匹配接受的哪一级 [待实测：本库无该族参数表]。校验形态与 8965 min_score 同构，负值、把百分比当 0–1 传是常见病灶。</para>
	///   <para><b>勿混淆</b>本码只指“描述符算子的 threshold 参数值不合法”，与本库分割算子 <c>Threshold</c>（图像灰度区间分割）无关——那是算子名，它的参数错误走各自的码，不会落 8969。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_THRESHOLD = 8969;

	/// <summary>数值参数 'depth' 非法（取值 8970），原生文本为 <c>Invalid 'depth'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>检索树深度参数非法，与 8971（number_trees）构成树结构的“高与宽”一对 [待实测：按参数名与同段邻居推得]。特别注意：此处 depth 是搜索树的层数，不是景深、不是 Z 向距离——与 3D 族（89xx 前段）的 depth 类参数同名不同义，跨族复制参数值前先确认语义。</para>
	///   <para><b>处置</b>正整数起步、结合树数一起调：深度大而树少容易欠拟合检索，树多而浅则漏检静默发生（不报错、结果变少），这类“参数合法但组合劣化”不体现在本码上。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_DEPTH = 8970;

	/// <summary>数值参数 'number_trees' 非法（取值 8971），原生文本为 <c>Invalid 'number_trees'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>随机森林检索的树数量参数非法。与 8970（depth）是一对“宽与高”：树数决定并行检索结构的份数、深度决定每棵树的层数，两者同升则检索更全但更慢更占内存 [待实测：机理按参数名推得，本库无该族文档]。0 与负值基本必挂本码。</para>
	///   <para><b>取向</b>该组是训练期参数——改动后模型要重训重存，不能只在匹配端调。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_TREES = 8971;

	/// <summary>数值参数 'min_score_descr' 非法（取值 8972），原生文本为 <c>Invalid 'min_score_descr'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>描述符比对环节的最低分数阈值参数非法。与 8965（min_score）同族异段：描述符族有两个“最低分”参数，分别在整体流程与描述符比对阶段起作用，各由哪个算子在哪一步收取 [待实测]。本库匹配族得分口径惯为 0–1（形状族示例皆在此范围），越出如 1.5 或负值大概率撞本码 [待实测：本族开闭边界]。</para>
	///   <para><b>提醒</b>本码是参数校验错，不是“匹配分数不够”——分数不够不报错、只回空结果，两回事别混诊。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_MIN_SCORE_DESCR = 8972;

	/// <summary>数值参数 'patch_size' 非法（取值 8973），原生文本为 <c>Invalid 'patch_size'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>描述符取样块尺寸非法——patch 指在特征点邻域取多大的窗口来编码描述符向量 [待实测：从参数名推得，具体编码机制本库无文档]。0 或负值是必挂形态；上界可能与图像/邻域参数联动 [待实测]。</para>
	///   <para><b>取向</b>该参数决定描述符的长度与区分力，改它会改变模型兼容性：patch 变了旧模型文件要重建，不像阈值类参数可以事后调匹配端。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_PATCH_SIZE = 8973;

	/// <summary>参数 'tilt' 非法（取值 8974），原生文本为 <c>Invalid 'tilt'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>倾角参数非法。按名推断对应描述符族容忍的离平面倾转幅度（工件绕视线轴倾斜的程度上限 [待实测：单位口径弧度或度、值域上下界均无仓库依据]）。注意本码命名无 NUM 中缀（WRONG_TILT），原生对它的校验不止“数值区间”一种可能。</para>
	///   <para><b>处置</b>2D 相机场景里工件基本平放时，把倾角容忍收到最小即可省下搜索量；收紧参数报本码的反方向（要真倾斜匹配却收为零容忍）不会报码、只会静默漏检，那类问题去查分数与结果数，别回头怀疑本码。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_TILT = 8974;

	/// <summary>引导匹配开关参数 'guided_matching' 非法（取值 8975），原生文本为 <c>Invalid 'guided_matching'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>引导匹配这一布尔型选项传了非法形态。原生 bool 类参数惯收 'true'/'false' 串或 0/1，本参数接受哪种精确形态 [待实测]。引导匹配按名推断是“用粗匹配结果约束精搜以提速”的开关 [待实测：机理在本库无文档展开]，开关本身报错说明参数没进取值集，与匹配质量无关。</para>
	///   <para><b>族定位</b>8974–8976 是描述符参数段三个非数值参数码（tilt/guided_matching/subpix），与 8962–8973、8978–8983 的 WRONG_NUM 数值族按命名一眼可分。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_PAR_GUIDE = 8975;

	/// <summary>亚像素模式参数 'subpix' 非法（取值 8976），原生文本为 <c>Invalid 'subpix'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>亚像素精化档位字符串不被识别。本库形状匹配族的 subPixel 取值为 'none'、'interpolation'、'least_squares' 一类字符串（各 Find 系文档在用）；描述符族是否共用同一枚举 [待实测]，跨族照抄前先验证。</para>
	///   <para><b>命名提示</b>本码是 WRONG_PAR 而非 WRONG_NUM：原生按枚举参数校验它，传数字 0/1 想“当布尔用”会直接撞本码；同段 8974（tilt）、8975（guided_matching）到 8986（matcher）都是这类选择参数专属码。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_PAR_SUBPIX = 8976;

	/// <summary>可找到的特征点过少（取值 8977），原生文本为 <c>Too few feature points can be found</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>描述符模型训练或匹配时，从图像/ROI 里提取出的特征点撑不起稳定模型。与前后码的本质不同：8962–8976 报“参数不对”，本码报“数据不够”——纹理贫乏、ROI 太小、筛选阈值（min_score/threshold 系）收得太狠都是病灶，改参数顺序解决不了病灶在图的问题。</para>
	///   <para><b>处置</b>扩大训练 ROI、换纹理丰富的工件面、放宽筛选阈值逐级试；表面确实近无纹理（磨砂金属、纯色塑料）时，描述符/特征点路线本身就不该选，改本库在架的形状模型（JlShapeModel 族）或 NCC（<c>CreateNccModel</c>，对灰度分布鲁棒、不依赖特征点）。</para>
	///   <para><b>子系统状态</b>本托管库已删除描述符模型包装，正常路径不触发本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DM_TOO_FEW_POINTS = 8977;

	/// <summary>数值参数 'min_rot' 非法（取值 8978），原生文本为 <c>Invalid 'min_rot'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>旋转搜索下界非法。与 8979 成对，最常见病因同为区间反转（min 大于 max）；注意旋转角有“跨零”写法需求（如 -0.39 到 0.79），负角度本身合法与否、以及负下界是否为该族接受 [待实测]，别因为怕负数就把区间整体平移到正值而丢掉真实朝向。</para>
	///   <para><b>族惯例</b>本文件形状模型族的示例惯用弧度角（如 -0.39、0.79 这类量级），排查本码时先按弧度口径复核数值。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_MINROT = 8978;

	/// <summary>数值参数 'max_rot' 非法（取值 8979），原生文本为 <c>Invalid 'max_rot'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>旋转搜索上界非法。角度参数在本库匹配族文档里惯用弧度（形状模型族如此），描述符族 max_rot 是否同口径 [待实测]——把 0–360 按“度”传进去是该族最典型的首次接入错误，先验数值量级再调次序。</para>
	///   <para><b>四元组</b>与 8978（min_rot）成对、与 8980/8981（尺度对）同段：先查 min ≤ max 次序，再查自身值域（超一周是否合法 [待实测]）。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_MAXROT = 8979;

	/// <summary>数值参数 'min_scale' 非法（取值 8980），原生文本为 <c>Invalid 'min_scale'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>尺度下界参数非法，最常见形态是与 8981 反转（min 大于 max）造成空区间——搜索区间为空时算子不会“静默 0 结果”而是直接报码，这点比查无结果更好排查。尺度口径与四元组结构见 8978–8981 相邻码。</para>
	///   <para><b>处置</b>按 min ≤ max 顺序复核两参；若该场景本就没有缩放，把尺度搜索收窄到原尺寸附近是正解，具体收窄写法本族未发布文档 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_MINSCALE = 8980;

	/// <summary>数值参数 'max_scale' 非法（取值 8981），原生文本为 <c>Invalid 'max_scale'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>尺度上界参数非法：非正值、或与 8980（min_scale）构成上界小于下界的空区间 [待实测：原生拒绝的精确规则]。尺度是无量纲倍率、1.0 为原尺寸——本库形状模型族文档明示的约定，描述符族应同一口径 [待实测]。</para>
	///   <para><b>四元组</b>8978–8981 是旋转/尺度搜索区间四元组（min_rot、max_rot、min_scale、max_scale），排查统一两步：先对“min ≤ max”的次序，再查各参数自身值域；两对参数都成对报码，拿到其一先核对其兄弟。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_MAXSCALE = 8981;

	/// <summary>数值参数 'mask_size_grd' 非法（取值 8982），原生文本为 <c>Invalid 'mask_size_grd'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>梯度计算阶段掩模尺寸非法（grd 即 gradient 缩写）。与 8983 成对，一个管梯度窗、一个管平滑窗；掩模尺寸类参数通常要求正奇数且不超过相关图像/patch 尺寸 [待实测：本族的具体约束无仓库依据]。</para>
	///   <para><b>排查</b>拿到本码先确认传的是尺寸不是半径——参数名叫 size，若上游按半径口径计算会系统性偏小，这种换算错位比单纯越界更隐蔽。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_MASKSIZEGRD = 8982;

	/// <summary>数值参数 'mask_size_smooth' 非法（取值 8983），原生文本为 <c>Invalid 'mask_size_smooth'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>平滑阶段掩模尺寸越界或类型不对。与 8982（mask_size_grd）成对：两码分别卡描述符预处理中“梯度”和“平滑”两级各自的邻域窗口，参数名直接指出错在哪一级。两值与图像尺寸、sigma 系参数（8966/8967）之间是否有联动约束 [待实测]。</para>
	///   <para><b>子系统状态</b>本托管库无描述符模型包装，正常路径不触发（8962–8983 全段同此）[待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_NUM_MASKSIZESMOOTH = 8983;

	/// <summary>模型已损坏（取值 8984），原生文本为 <c>Model broken</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>模型对象的内部结构不再自洽，已不能作为合法对象参与训练/匹配。可能来源：句柄 Dispose 后被复用、模型内存被越界写坏、序列化文件中途截断读入 [待实测：三类成因占比无仓库实证]。它是 8962–8988 描述符段里极少数“病根不在参数”的码：拿到它就别再调参抢救了。</para>
	///   <para><b>处置</b>丢弃该模型并重建（重训或重读文件）；同时沿句柄生命周期查 use-after-free——本库模型类均实现 IDisposable，确认没有任何算子还在用就被 Dispose 的实例。</para>
	/// </remarks>
	public const int Jl_ERR_BROKEN_MODEL = 8984;

	/// <summary>描述符类型参数 'descriptor_type' 非法（取值 8985），原生文本为 <c>Invalid 'descriptor_type'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>“选用哪一族描述符算法”的枚举值不被支持。本码命名 WRONG_DESCR_TYPE（无 NUM/PAR 中缀）是该参数的专属校验；报它说明选族这一步就没过，后面的树数、patch 尺寸等数值参数根本没轮到检查。</para>
	///   <para><b>子系统状态</b>本托管库已删除描述符模型包装（同 8960 段说明），C# 正常路径不触发本码 [待实测：是否存在直传原生算子的通道]。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_DESCR_TYPE = 8985;

	/// <summary>匹配器选择参数 'matcher' 非法（取值 8986），原生文本为 <c>Invalid 'matcher'</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>指定“用哪种检索/匹配策略”的枚举字符串不被原生识别。合法取值清单仓库内无据 [待实测]，不要照抄其他算子族的策略字符串——先核对拼写与大小写，再对照该算子的原生参数表。</para>
	///   <para><b>族定位</b>8985/8986 是描述符参数段末尾两个“选择题”校验：8985 管 descriptor_type（造哪种描述符），本码管 matcher（怎么检索），都是选值非法而非数值越界，与 8962–8983 的 WRONG_NUM 系列按命名即可区分。</para>
	/// </remarks>
	public const int Jl_ERR_DM_WRONG_PAR_MATCHER = 8986;

	/// <summary>特征点类别数过多，模型无法写入文件（取值 8987），原生文本为 <c>Too many point classes - cannot be written to file</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>描述符模型训练时把特征点按局部几何关系归成若干“类”存储，类数超过序列化格式容量后拒写。失败点在写档：训练可能已经跑完，但模型落不了盘——与 8977（可提取的点太少）是一头一尾的两个极端，前者稠密爆表、后者稀疏不足。</para>
	///   <para><b>处置</b>收紧产点的参数（提高筛选/评分阈值、减小 ROI、降低旋转尺度覆盖密度）后重训 [待实测：本族参数与类数的联动无仓库文档]。</para>
	///   <para><b>子系统状态</b>本托管库无描述符模型包装，正常路径不触发 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DM_TOO_MANY_CLASSES = 8987;

	/// <summary>序列化条目里不含有效的描述符模型（取值 8988），原生文本为 <c>Serialized item does not contain a valid descriptor model</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>文件/句柄能打开、结构能解析（与 8960 格式读不出、8961 版本不支持区分），但容器里的东西不是描述符模型——典型是把别的模型类型（形状、NCC）写进了按描述符模型读取的槽位，或序列化时漏写了模型本体。</para>
	///   <para><b>读档三兄弟</b>8960（格式非法）、8961（版本过新）、8988（读出来不是这类模型）覆盖描述符族“读模型失败”的三种原因，与本段 3D 族 8941/8942/8945 的分工同构：先看码定性，再决定重训、升库还是换文件。</para>
	///   <para><b>子系统状态</b>本托管库已删除描述符模型包装，正常路径不触发本码，仅跨边界复用原生句柄/文件时可见 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_DESCR_NOSITEM = 8988;

	/// <summary>本机未实现该函数（取值 9000），原生文本为 <c>Function not implemented on this machine</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>算子在接口中存在，但当前机器上的原生内核没带它的实现——常见于内核按平台裁剪、dll 精简发行或版本配套错位。与 9008 区分：9008 是“算子在、缺某像素类型的实现”，本码是整个算子无实现。</para>
	///   <para><b>与本库删减的关系</b>托管层已删除的功能（显示/绘制、采集、通信、深度学习、3D、OCR 等）在 C# 侧连调用入口都没有，不会以本码形式出现；正常管理路径见到 9000，优先怀疑原生 dll 与托管层不是同版本成套 [待实测：托管各包装与原生实现的对应支持矩阵本库无文档]。</para>
	///   <para><b>段首</b>本码是 9000–9024 杂项通用校验段的第一员：9001–9009 图像/字符串校验、9010/9011 演示版限制、9020–9023 内部错/参数错/域小/绘制取消。</para>
	/// </remarks>
	public const int Jl_ERR_NOT_IMPL = 9000;

	/// <summary>待处理图像的灰度值类型不对（取值 9001），原生文本为 <c>Image to process has wrong gray value type</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>输入侧图像类型校验：算子只接受某一类像素类型（本库文档示例惯用 "byte" 与 "real"），传进来的图不在其列。与 9008 相反——那是“类型合法但算子没实现”，本码是“这次调用给错了图”；与 9007 的措辞差异对应的实际检查点差异 [待实测]。</para>
	///   <para><b>处置</b>先用本库已有的 <c>JlImage.GetImageType()</c> 查实图类型，再决定换图还是 <c>JlImage.ConvertImageType(newType)</c> 转换；转换可能截断/回绕数值（real→byte、int2→byte），先确认量程再动手，别只求“不报错”。</para>
	/// </remarks>
	public const int Jl_ERR_WIT = 9001;

	/// <summary>图像分量（component）参数错误（取值 9002），原生文本为 <c>Wrong image component</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>component 选择参数与图像不匹配：取 'red'、'green'、'blue'、'hue' 一类色分量名的参数，喂到灰度图上取不出分量，或分量名本身拼写不被原生识别。两种病因（名字非法/图不相配）是否分报不同码 [待实测：可能部分落 9009 或 9021]。</para>
	///   <para><b>处置</b>只处理单一分量时，先用 <c>JlImage.Decompose3(...)</c> 拆成灰度图再走单通道路线，彻底绕开 component 参数；这条路同时免疫“色序配置不同导致静默拿错通道”的暗坑。</para>
	/// </remarks>
	public const int Jl_ERR_WIC = 9002;

	/// <summary>遇到未定义的灰度值（取值 9003），原生文本为 <c>Undefined gray values</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>参与计算的像素里存在“无确定灰度值”的点。原生体系对图像变换后未被覆盖的区域有“未定义值”约定（几何变换越界、拼接留空等），算子把这种像素纳入计算即报本码 [待实测：本库托管路径下哪些算子会制造或撞上未定义像素，无仓库实证]。</para>
	///   <para><b>处置</b>把定义域收缩到来源完备的区域（ReduceDomain 链），或让上游变换先给出显式填充值再进计算；这类码排查关键是回溯“哪一步留下了空洞”，在本码处修补参数通常无效。</para>
	/// </remarks>
	public const int Jl_ERR_UNDI = 9003;

	/// <summary>图像格式（尺寸）不符合该操作要求：过大或过小（取值 9004），原生文本为 <c>Wrong image format for operation (too big or too small)</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>查的是图像几何尺寸而非像素类型（类型归 9001/9007）：算子对宽、高或总像素数有允许区间，0×0 空图和超限巨图都落本码。上下界各算子未公布 [待实测]。</para>
	///   <para><b>与 9022 区分</b>9022 查“定义域太小”（整图可以很大，域裁窄了报那码），本码查整图本身越界；一张大图报 9022、一张小图报 9004 的场景要分开处理。</para>
	///   <para><b>处置</b>大图先缩放/分块（金字塔降层）再进算子，小图补边或换分辨率采集；尺寸类限制属于算子能力边界，重试同一输入不会自愈。</para>
	/// </remarks>
	public const int Jl_ERR_WIS = 9004;

	/// <summary>输出图像的通道数不对（取值 9005），原生文本为 <c>Wrong number of image components for image output</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>检查点落在输出侧：算子需要产出（或写回）指定通道数的图像，实际相配不上。与查输入的 9009/9001 相对，本码典型落在“结果通道数 1 却对着 3 通道的出口”这类色彩合成/拆并链路上 [待实测：具体哪些算子用本码校验输出无仓库依据]。</para>
	///   <para><b>处置</b>本库多数包装方法由算子自行分配输出（返回新句柄，类型随算子定），不要预置一个通道数想当然的输出图；出本码时先核对上游拆分/合并环节的通道账：Decompose3 出来 3 份，回去也得 3 份。</para>
	/// </remarks>
	public const int Jl_ERR_WCN = 9005;

	/// <summary>字符串参数超长：原生侧单串上限 1024 字符（取值 9006），原生文本为 <c>String is too long (max. 1024 characters)</c>。</summary>
	/// <remarks>
	///   <para><b>硬上限</b>1024 是原生文本明说的，可当事实用。C# 字符串本身没有这个限制，越界只发生在跨原生边界传参时——高发场景：动态拼接出来的长正则、超长文件路径、批量拼成的名字串。</para>
	///   <para><b>与 9024 区分</b>本码是执行前的参数长度校验；正则串太长看到的是本码，而不是 9024（那码管匹配执行失败）。</para>
	///   <para><b>计数口径</b>上限按 UTF-16 码元还是 UTF-8 字节计未核实 [待实测]；中文串按字节口径可能双倍占额，长串务必留余量或改走文件路径类参数。</para>
	/// </remarks>
	public const int Jl_ERR_STRTL = 9006;

	/// <summary>此操作不接受该像素类型（取值 9007），原生文本为 <c>Wrong pixel type for this operation</c>。</summary>
	/// <remarks>
	///   <para><b>与 9001 的分工</b>两条原生文本高度相近（9001 说“图像的灰度值类型不对”，本码说“对该操作而言像素类型不对”），原生层两处检查的时机差异（进算子入口即拒 与 计算中途拒）本库无文档佐证 [待实测]；排查处置一致：查该算子接受的类型集，必要时转换。</para>
	///   <para><b>常见诱因</b>把多通道彩色图或方向类特殊类型喂给只收单通道 byte/real 的算子；本仓库文档示例惯用 "byte" 与 "real"，合法类型全集由原生决定、未经核实 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WITFO = 9007;

	/// <summary>该算子对此像素类型尚无实现（取值 9008），原生文本为 <c>Operation not realized yet for this pixel type</c>。</summary>
	/// <remarks>
	///   <para><b>与 9001/9007 的本质区别</b>9001/9007 是“你传错了”（调用方参数问题，换个类型就能跑）；本码是“我们没做”——图像类型本身合法，但该算子当前版本对这种类型没实现，属能力缺口，调参救不了。哪些算子只覆盖 byte/real 而无 int2/complex 等实现 [待实测：本库未发布算子×类型支持矩阵]。</para>
	///   <para><b>处置</b>用 <c>JlImage.ConvertImageType(newType)</c> 先转成大概率被支持的传统类型（byte/real）再调用；注意转换本身可能截断精度或动态范围（real 转 byte），转之前先确认数值约定，别让“能跑了”掩盖“算错了”。</para>
	/// </remarks>
	public const int Jl_ERR_NIIT = 9008;

	/// <summary>输入不是三通道彩色图（取值 9009），原生文本为 <c>Image is no color image with three channels</c>。</summary>
	/// <remarks>
	///   <para><b>触发</b>色彩域算子要求输入恰好三通道：灰度图（1 通道）与四通道图都不合格。调用前用本库已有的 <c>JlImage.CountChannels()</c> 断言等于 3，比事后捕错便宜；确认类型可用 <c>JlImage.GetImageType()</c>。</para>
	///   <para><b>与 9002/9005 分工</b>本码查“输入图像通道数”，9002 查 component 选择参数与图是否相配，9005 查输出图像的通道数——三点各卡管线一头，报码不同处置也不同。</para>
	///   <para><b>处置</b>单通道需求改用 <c>JlImage.Decompose3(...)</c> 拆出的灰度分量走灰度路线；确需合成三通道再喂色彩算子时，本库是否有合并包装按算子查证 [待实测]（JlImage 上未搜到 Merge3 一类方法）。</para>
	/// </remarks>
	public const int Jl_ERR_NOCIMA = 9009;

	/// <summary>演示版（demo 许可）不支持图像采集设备（取值 9010），原生文本为 <c>Image acquisition devices are not supported in the demo version</c>。</summary>
	/// <remarks>
	///   <para><b>版本限制族</b>9010/9011 是演示版限制两员：本码管采集设备（framegrabber）被禁，9011 管功能包被禁；与学员版限制族 9050–9052 分层平行，本对对应 demo 许可档。</para>
	///   <para><b>子系统状态</b>本库托管层已整体删除 framegrabber 采集能力，C# 侧不存在“打开设备/抓图”的接口，正常管理路径无法触发本码，仅作原生错误表保留项（与 9052 同因同果）。</para>
	/// </remarks>
	public const int Jl_ERR_DEMO_NOFG = 9010;

	/// <summary>演示版（demo 许可）不支持功能包（取值 9011），原生文本为 <c>Packages are not supported in the demo version</c>。</summary>
	/// <remarks>
	///   <para><b>版本限制族</b>9010/9011 是演示版限制两员：9010 管采集设备被禁，本码管功能包（package）整体被禁。与学员版限制族 9050–9052（算子/包/设备三层分工）构成“每个许可档位一套码”的平行结构：同为“包被拒”，demo 档报本码、学生档报 9051，看码即知当前运行时所处档位。</para>
	///   <para><b>package 语义</b>指原生体系中核心库之外按包分发的功能扩展 [待实测：本库无任何托管 API 暴露包安装，哪些算子属于包无仓库依据]。触发于许可级别校验，早于算子内部参数校验；换授权即可解，与调用处传参无关。</para>
	/// </remarks>
	public const int Jl_ERR_DEMO_NOPA = 9011;

	/// <summary>内部错误：未知取值（取值 9020），原生文本为 <c>Internal Error: Unknown value</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>标了 INTERNAL——原生内核内部出现了自己都不认识的枚举值。正常调用理应在更早的对外校验（9021 及各 Invalid 'xxx' 码）就被拦下，走到本码通常只剩三种解释：句柄已被释放而内存被复用、内核与托管层版本不匹配、内核自身缺陷 [待实测：该归因排序按 INTERNAL 字样推得，无仓库实证]。</para>
	///   <para><b>处置</b>先查 use-after-free：确认所有图像/模型句柄的 Dispose 都发生在最后一个用到它的算子之后；再核对托管 dll 与原生内核是否同版本成套发布。两者都排除则本码不是用户参数能修复的，带最小复现反馈库提供方。</para>
	/// </remarks>
	public const int Jl_ERR_IEUNKV = 9020;

	/// <summary>当前操作的参数错误（取值 9021），原生文本为 <c>Wrong parameter for this operation</c>。</summary>
	/// <remarks>
	///   <para><b>定位</b>参数校验的“兜底码”：算子若能精确定位是哪个参数错了，会返回专用码（如本文件 8962–8983 的描述符逐参数码、各族的 Invalid 'xxx' 码），定位不到才落到本码。因此拿到 9021 别指望码本身指出病根，要顺着出错算子的签名逐参核对。</para>
	///   <para><b>排查</b>常见病灶：枚举字符串拼写或大小写不对、数值越界、min 大于 max 的区间反转。托管层装箱参数时类型转换（如实参进 INTEGER 槽）是否也能触发本码 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_WPFO = 9021;

	/// <summary>图像定义域过小：经 ReduceDomain 收缩后的有效域装不下当前算子的邻域窗口需求（取值 9022），原生文本为 <c>Image domain too small</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>检查对象是“域”而不是整图：算子需在定义域内取邻域（滤波窗、积分矩形、纹理块等），ROI 裁得比所需最小尺寸还窄就报本码——大图配窄域同样触发。整图尺寸越界走 9004，不混淆。</para>
	///   <para><b>处置</b>放宽 ROI，或减小该算子的窗口类参数（核尺寸、块尺寸）。本库托管层确有域操作链路（<c>JlImage.ReduceDomain(JlRegion)</c> 存在），管理路径可实际触发本码；各算子要求的最小域尺寸本库文档未列 [待实测]，排查时先缩参数试跑。</para>
	/// </remarks>
	public const int Jl_ERR_IDTS = 9022;

	/// <summary>交互式绘制被取消：绘制类算子执行期间用户中止了本次绘制（取值 9023），原生文本为 <c>Draw operator has been canceled</c>。</summary>
	/// <remarks>
	///   <para><b>含义</b>这是给交互流程设计的"取消信号"，不是故障：收到它应把本次绘制视为作废、让用户重来或跳过该 ROI，而不是当致命错误上抛。取消的具体手势（右键/ESC 等）由原生交互实现决定 [待实测]。</para>
	///   <para><b>段末位</b>9000–9024 是杂项通用校验段，本码居该段末；紧邻其后是 9024 正则匹配错误与 9050–9052 学员版限制族。</para>
	///   <para><b>子系统状态</b>本库托管层已整体删除显示/绘制族（无 Disp*、Draw*、JlWindow 包装），C# 侧已调不到任何绘制算子，正常路径不会再触发本码，仅作原生错误表保留项（与 9052 对采集设备的处置同构）。</para>
	/// </remarks>
	public const int Jl_ERR_CNCLDRW = 9023;

	/// <summary>正则表达式匹配出错：正则相关算子在执行模式匹配时返回本码。</summary>
	/// <remarks>
	///   <para>错误码常量的惯用法：拿算子返回码与本类常量逐一相等比较后分支处理，不要按数值大小或正负猜测含义（本类正常状态值本身也是非零码）。</para>
	///   <para>位置规律：9000–9024 段是杂项通用校验码（未实现、图像类型错、演示版限制、绘制取消等），本码是该段末位、也是库内唯一明确指向正则执行失败的码；自 9050 起进入学员版限制族。</para>
	///   <para>推荐处置：检查正则串是否被转义或字符串拼接破坏，再核对输入文本。"模式语法非法"与"模式合法但未匹配到内容"是否都归入本码，[待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_REGEX_MATCH = 9024;

	/// <summary>学员版（教学版）许可下不可调用该算子。</summary>
	/// <remarks>
	///   <para>9050–9052 是学员版限制族，按被限制对象分工：本码=算子本身被禁，9051=功能包不可用，9052=选中的采集设备不可用。三码互斥，看到哪个就知道被卡在哪一层。</para>
	///   <para>触发场景：在学员版运行时调用了仅正式版开放的算子。与许可证缺失类错误不同，它表示"版本等级不够"而非"授权损坏"。</para>
	///   <para>推荐处置：改用正式版授权，或将该步骤替换为学员版开放的算子。</para>
	/// </remarks>
	public const int Jl_ERR_STUD_OPNA = 9050;

	/// <summary>学员版（教学版）许可下功能包（package）不可用。</summary>
	/// <remarks>
	///   <para>学员版限制族 9050–9052 的第二员：本码针对"算子所属的功能包整体未随学员版开放"，与 9050（单个算子被禁）、9052（采集设备被禁）分层互斥。</para>
	///   <para>触发场景：调用属于某收费/扩展功能包的算子，而当前是学生版授权，整包被拒。错误发生在包级别校验，早于算子内部参数校验。</para>
	///   <para>推荐处置：升级授权，或改用核心库内不依赖该包的实现路径。</para>
	/// </remarks>
	public const int Jl_ERR_STUD_PANA = 9051;

	/// <summary>学员版（教学版）许可下所选图像采集设备不可用。</summary>
	/// <remarks>
	///   <para>学员版限制族 9050–9052 的末员：本码特指"选中的采集设备（framegrabber）被学员版拒绝"，与 9050（算子）、9051（功能包）分层互斥。</para>
	///   <para>可证实事实：本库已整体删除 framegrabber 采集能力，C# 包装层没有任何算子接口会走"选设备"这一步，因此正常调用路径下本码不会再被触发，仅作历史码值保留。</para>
	/// </remarks>
	public const int Jl_ERR_STUD_FGNA = 9052;

	/// <summary>没有可用的数据点：算子需要非空的点集输入，实际拿到的是空集。</summary>
	/// <remarks>
	///   <para>触发场景：提取角点、轮廓采样点、拟合输入等算子的上游返回了空元组/空区域，下游直接拿它当点集用。属于"输入为空"类校验，不是算法失败。</para>
	///   <para>与相邻码区分：9054 表示对象类型不对（给了不支持的类型），本码表示类型对但内容为空；先判空再判型。</para>
	///   <para>推荐处置：调用前检查点集数量（如元组 Length 为 0），回溯上游为何没产出点。</para>
	/// </remarks>
	public const int Jl_ERR_NDPA = 9053;

	/// <summary>对象类型不受支持：传给算子的句柄类型不在该算子接受的类型之列。</summary>
	/// <remarks>
	///   <para>触发场景：把区域句柄当图像句柄传、或把本算子不接受的对象类型塞进多态入口。WR 即 wrong，指类型错配而非状态错误。</para>
	///   <para>与相邻码区分：9053 是"类型对但内容为空"，本码是"类型本身不对"；9055 则是算子被禁用、根本没执行到参数校验。</para>
	///   <para>推荐处置：对照算子签名核对每个句柄的 .NET 类型（JlImage/JlRegion/JlXLD 等），不要依赖运行时隐式兼容。</para>
	/// </remarks>
	public const int Jl_ERR_WR_OBJ_TYPE = 9054;

	/// <summary>算子已被禁用：该算子在当前运行配置下被显式关闭，调用被直接拒绝。</summary>
	/// <remarks>
	///   <para>与学员版族区分：9050 是"版本等级不够"，本码不区分版本等级，表示该算子被许可证特性或构建配置禁用；错误发生在算子入口，早于任何参数校验。</para>
	///   <para>推荐处置：核对该算子所需的许可证特性是否开通；若属裁剪掉的特性，改用替代算子。禁用名单与授权项的对应关系 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_OP_DISABLED = 9055;

	/// <summary>线性方程组中未知数过多：方程组欠定，未知量数目超出方程可确定的范围。</summary>
	/// <remarks>
	///   <para>9100–9150 是线性方程求解族：本码报"未知数相对过多"，9101 报"无（唯一）解"，9102 报"方程条数不足"，9150 报"点定不出直线"。四者都指向"给的条件不够定出解"，但检测阶段不同。</para>
	///   <para>触发场景：标定/拟合类算子收集到的观测点或约束方程数低于未知数维度，如姿态估计只给了过少的对应点。</para>
	///   <para>推荐处置：增加独立观测（更多点、更多位姿），不要靠调低数值精度绕过。</para>
	/// </remarks>
	public const int Jl_ERR_TMU = 9100;

	/// <summary>线性方程组无解或无唯一解。</summary>
	/// <remarks>
	///   <para>线性方程求解族（9100–9150）成员：与 9100/9102 的"条件不够"不同，本码是方程组本身不相容（过定且矛盾）或系数矩阵秩亏导致解不唯一，具体判据 [待实测]。</para>
	///   <para>触发场景：拟合/标定输入观测互相矛盾（噪声外点），或观测之间存在线性相关（重复量测同一方向）。</para>
	///   <para>推荐处置：剔除外点、补充不同方向的观测；若只需要任一解，可改用带正则化的求解路径 [本库是否有此路径待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_NUS = 9101;

	/// <summary>线性方程组方程条数太少。</summary>
	/// <remarks>
	///   <para>线性方程求解族（9100–9150）成员：与 9100"未知数过多"互为镜像表述——本码从方程侧陈述"条数不够"。二者是否按检测视角分别发出 [待实测]。</para>
	///   <para>推荐处置：补观测方程数至不低于未知量维度，或降低模型自由度。</para>
	/// </remarks>
	public const int Jl_ERR_NEE = 9102;

	/// <summary>给定的点无法确定一条直线。</summary>
	/// <remarks>
	///   <para>线性方程求解族（9100–9150）末位。PDDL = points do not define a line：典型如拟合/测量算子只拿到 0 或 1 个点，或两个输入点重合，直线方程退化为无定义。</para>
	///   <para>与 9053（无数据点）区分：点完全缺失报 9053，点存在但几何上不足以定线报本码 [两码分工边界待实测]。</para>
	///   <para>推荐处置：确保至少两个互异点，并检查上游点提取是否退化。</para>
	/// </remarks>
	public const int Jl_ERR_PDDL = 9150;

	/// <summary>矩阵不可逆（奇异方阵求逆失败），本码为矩阵代数族（9200–9231）的族首。</summary>
	/// <remarks>
	///   <para>9200–9231 是矩阵/线性代数族：本码与 9201–9206（迭代不收敛、奇异）是结果性错误，9207 起 MAT_* 子族是输入性错误（未定义、维度、非方阵……），9216–9218 专指矩阵文件读取，9220–9222 涉及序列化/版本，9230–9231 与 9250 是内部错误。</para>
	///   <para>触发场景：用矩阵求逆解线性方程或做变换复合时，矩阵行列式趋于 0。</para>
	///   <para>推荐处置：检查变换是否把空间压扁（旋转角退化、缩放 0、点共线），删冗余行/列；仅要近似解时改走伪逆类求解 [本库是否暴露伪逆待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_MNI = 9200;

	/// <summary>SVD（奇异值分解）迭代未收敛。</summary>
	/// <remarks>
	///   <para>矩阵族（9200–9231）收敛类成员：与 9200 的"数学上不可逆"不同，本码是数值迭代在步数上限内没算完，矩阵本身可能有解。</para>
	///   <para>触发场景：矩阵元素数值范围极端（量级跨度过大）、含 NaN/Inf，或病态条件数使迭代震荡。</para>
	///   <para>推荐处置：先归一化数据量纲（如坐标除以特征尺度）再分解；反复出现则检查输入是否混入非法值。</para>
	/// </remarks>
	public const int Jl_ERR_SVD_CNVRG = 9201;

	/// <summary>矩阵行数太少，无法按所选奇异值分块方式进行分解。</summary>
	/// <remarks>
	///   <para>矩阵族输入类成员（9202 与 9201 同为 SVD 路径，但 9201 是"迭代失败"、本码是"形状不满足分块前提"）。触发场景：对行数低于该分解路径下限的瘦矩阵做带分区的 SVD，具体行数界限 [待实测]。</para>
	///   <para>推荐处置：补齐观测行（转置后分解或增加数据），该错误与数值条件无关，调精度参数无效。</para>
	/// </remarks>
	public const int Jl_ERR_SVD_FEWROW = 9202;

	/// <summary>特征值计算未收敛（tqli：对称三对角矩阵 QL 隐位移迭代路径）。</summary>
	/// <remarks>
	///   <para>矩阵族收敛类成员。英文文案与 9204 完全相同（均为"特征值计算未收敛"），但按常量名分工：本码对应三对角 QL 迭代例程，9204 对应 Jacobi 旋转例程——排查时先确定调用方走的是哪条特征值路径。</para>
	///   <para>触发场景：矩阵元素极端或含非法值使迭代不收敛。推荐处置同 9201：先归一化数据量纲并清洗输入。</para>
	/// </remarks>
	public const int Jl_ERR_TQLI_CNVRG = 9203;

	/// <summary>特征值计算未收敛（Jacobi 旋转变体路径）。</summary>
	/// <remarks>
	///   <para>与 9203 共享同一英文文案，区别在算法路径：本码由 Jacobi 旋转例程发出，9203 由三对角 QL 例程发出。哪个算子走哪条路径 [待实测]。</para>
	///   <para>推荐处置：与 9201/9203 相同——归一化数据量纲、剔除 NaN/Inf，必要时改用其他分解再验算。</para>
	/// </remarks>
	public const int Jl_ERR_JACOBI_CNVRG = 9204;

	/// <summary>矩阵奇异（行列式趋于 0，秩亏损）。</summary>
	/// <remarks>
	///   <para>矩阵族结果类成员。与 9200（不可逆）语义高度重叠：奇异是不可逆的常见情形，两码由不同检测点发出，各自的确切触发算子 [待实测]。</para>
	///   <para>触发场景：变换矩阵含 0 缩放、点集共面/共线导致协方差矩阵降秩。推荐处置：恢复秩（补维度、去冗余行列），而不是重试。</para>
	/// </remarks>
	public const int Jl_ERR_MATRIX_SING = 9205;

	/// <summary>函数匹配（迭代优化）未收敛。</summary>
	/// <remarks>
	///   <para>矩阵/数值族（9200–9231）成员，但作用于匹配环节：英文文案为 function matching，指基于梯度的函数拟合/对位迭代在步数上限内未达收敛判据 [具体归属算子待实测]。</para>
	///   <para>触发场景：匹配算子（如形状模板查找、几何拟合）初值离真值太远、图像对比度过低致梯度失效。推荐处置：收紧搜索范围或改善预处理后重试；它与 9201/9203/9204 同为"不收敛"，但发生在匹配迭代而非线性代数迭代。</para>
	/// </remarks>
	public const int Jl_ERR_MATCH_CNVRG = 9206;

	/// <summary>输入矩阵未定义：传进去的 matrix 句柄里没有有效矩阵（取值 9207）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Input matrix undefined"：句柄存在但内容从未被装载。本库最常见来源是 <c>JlMatrix</c> 的无参构造器——它只把内部句柄置成 UNDEF、不碰原生层（故被标 <c>EditorBrowsableState.Never</c>），在 <c>CreateMatrix</c>/<c>SetFullMatrix</c>/<c>ReadMatrix</c>/<c>DeserializeMatrix</c> 装载之前就参与运算即报本码；对已 <c>Dispose()</c> 的实例取其句柄再传同理 [待实测——Dispose 后回本码还是托管层先抛 JlException]。</para>
	///   <para><b>与相邻码区分</b>9208/9209 判"有矩阵但形状不合"，本码判"根本没有矩阵"；13xx 段那套共用值域检查只管标量参数，句柄类输入的空与未定义只走本码。</para>
	///   <para><b>坑</b>矩阵算子的返回与 out 都是句柄，上一步真失败时它们可能停在 UNDEF 态；只要调用方没检查错误就往下喂，症状就会推迟到下一个算子身上报本码，看上去"坏"的是那条无辜的调用——链式矩阵运算必须逐步查错。</para>
	///   <para><b>处置</b>用前确认已装载（<c>GetSizeMatrix</c> 一次调用即可同时拿行列数，句柄无效时它自己会先报错），或显式 <c>CreateMatrix</c> 后再写值；重试不改数据无效。全库 Grep 除定义外无本常量引用；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_UNDEF = 9207;

	/// <summary>输入矩阵维度不对（形状不满足算子的维度前提，取值 9208）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Input matrix with wrong dimension"：矩阵本身有内容，但形状与这一步运算的要求不吻合。本库三类前提最容易撞码：矩阵乘法 <c>MultMatrix</c>/<c>MultMatrixMod</c> 按 "AB" 要求 A 列数等于 B 行数；逐元素族 <c>AddMatrix</c>/<c>SubMatrix</c>/<c>MultElementMatrix</c>/<c>DivElementMatrix</c> 要求两矩阵行列全等；解方程 <c>SolveMatrix</c> 要求 A 的行数与右端 B 的行数一致 [各算子确切判据待实测]。</para>
	///   <para><b>与相邻码区分</b>9209 只指"该方阵却给了非方阵"这一特例（行列数不等），本码是两矩阵之间或形状与算子前提之间对不上；A·B 能过而 A∘B 报错（或反之）就是两码的分界。</para>
	///   <para><b>坑</b>逐元素运算看起来"数值都对"就以为没事，实际它比矩阵乘法更严格：行列必须逐一相等，一个 3×1 除 1×3 不会广播，直接撞本码 [是否广播待实测]。<c>*Mod</c> 系列会原地改维度（<c>TransposeMatrixMod</c>、<c>MultMatrixMod</c> 之后维度即变），复用同一句柄做下一步时手里的旧维度已成历史。</para>
	///   <para><b>处置</b>运算前用一次 <c>GetSizeMatrix</c> 自校验（比读 <c>NumRows</c>+<c>NumColumns</c> 少一半原生调用，后两个属性各自都要问一次原生层）；形状靠 <c>TransposeMatrix</c>/<c>GetSubMatrix</c>/<c>RepeatMatrix</c> 改出来，改数值或调 epsilon 与本码无关。全库 Grep 除定义外无本常量引用；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_WDIM = 9208;

	/// <summary>输入矩阵不是方阵（算子要求行列数相等，取值 9209）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Input matrix is not quadratic"：quadratic 即方阵。本库只有对"方阵才有数学定义"的算子会发本码——<c>DeterminantMatrix</c>（行列式）、<c>InvertMatrix</c>/<c>InvertMatrixMod</c>（求逆）、<c>PowMatrix</c>/<c>PowMatrixMod</c>（矩阵整体幂，非逐元素幂）、各 <c>Eigenvalues*</c> 与广义特征值路径 [逐算子清单待实测]。判定只看行列数是否相等，与元素值无关。</para>
	///   <para><b>与相邻码区分</b>9208 是两矩阵形状对不上或一般性维度错，本码是单个矩阵自己不是方阵；<c>SvdMatrix</c> 对任意形状都成立，所以同一份数据它不报本码而求逆会报——这是判断"该换算法还是该改数据"的快捷线索。</para>
	///   <para><b>坑</b>逐元素族（<c>AddMatrix</c>/<c>MultElementMatrix</c>/<c>SqrtMatrix</c>/<c>AbsMatrix</c> 等）根本不吃方阵约束，别拿"能过加减"当"能求逆"的依据；判断方阵必须 <c>NumRows</c> 与 <c>NumColumns</c> 都读，只读一个属性看不出方阵与否。瘦高/扁宽的观测矩阵（m 条方程 n 个未知数，m 不等于 n）是最常撞本码的形态。</para>
	///   <para><b>处置</b>非方阵的求解需求改走 <c>SolveMatrix</c>（配 epsilon 走降秩/最小二乘方向 [语义待实测]）或 <c>SvdMatrix</c>，而不是补零硬拼成方阵——补出来的行列会污染行列式与特征值。全库 Grep 除定义外无本常量引用；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_NSQR = 9209;

	/// <summary>矩阵运算失败（后端未细分原因的兜底失败码，取值 9210）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Matrix operation failed"：例程自己返回失败但不说是哪一步——分解/迭代不收敛之外的内部失败、缓冲分配失败等 [哪些路径回本码本库不可考，待实测]。文案笼统不等于问题轻微，它与 9200 同属"结果算不出来"。</para>
	///   <para><b>与相邻码区分</b>矩阵族 9207–9219 把可归因的情形都占了坑：未定义 9207、维度 9208、非方阵 9209、非正定 9211、除零 9212、三角声明不符 9213/9214、负元素 9215、矩阵文件 9216–9218、复数结果 9219；归不进任何一条的才落到本码。所以本码的第一解读是"前提看起来都对了，但后端没算成"。</para>
	///   <para><b>坑</b>别把它当参数错去翻算子选项：矩阵族里能靠改字符串解决的是 9211/9213/9214（声明与数据不符）。另一条真线索是 <c>*Mod</c> 系列原地覆盖——上一步 <c>InvertMatrixMod</c>/<c>MultMatrixMod</c> 已把 this 换成别的矩阵，本轮再算的就是脏输入，症状可能以本码出现 [待实测]。</para>
	///   <para><b>处置</b>先用 <c>JlNativeApi.GetErrorMessage</c> 取回原生文本定位是哪一步；再清洗输入（<c>GetFullMatrix</c> 拿全部元素查 NaN/Inf/量级跨度），必要时 <c>AbsMatrix</c>+<c>ScaleMatrix</c> 归一化量纲后重算，或换 <c>SvdMatrix</c> 这条对病态最稳的路验算。全库 Grep 除定义外无本常量引用；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_FAIL = 9210;

	/// <summary>矩阵不是正定的（按对称正定路径算却发现非正定，取值 9211）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Matrix is not positive definite"：正定要求所有特征值为正。本库的入口是把类型字符串声明为正定那一档——<c>SolveMatrix</c> 的 matrixLHSType、<c>DeterminantMatrix</c>/<c>InvertMatrix</c>/<c>DecomposeMatrix</c> 的 matrixType 除 "general" 外还接受对称/三角等专用取值（专用取值会走 Cholesky 一类只吃正定矩阵的快速路径）[允许字符串集合与是否确走 Cholesky 本仓库未声明，待实测]。声明了正定而矩阵含零或负特征值即报本码。</para>
	///   <para><b>与相邻码区分</b>9205/9200 说"奇异/不可逆"（行列式趋于 0，半正定也落在这里），9213/9214 说"声明三角但数据不三角"，本码专指正定这个更强的前提。同一份矩阵把声明改回 "general" 往往就能算出来——这不是数据坏，是声明与数据不符；反过来它按正定声明算成功过，也不保证下次仍成立。</para>
	///   <para><b>坑</b>最容易中招的是协方差/法方程/点集正规矩阵这类"数学上应当正定"的对象：观测方向共线或重复量测会让它退化成半正定（最小特征值约等于 0），差一个浮点尾数就从"能算"翻成"非正定"；用 <c>GeneralizedEigenvaluesSymmetricMatrix</c> 时要求 B 对称正定，B 不满足也在本码一带报错 [具体码待实测]。</para>
	///   <para><b>处置</b>先判定意图：只要结果正确就把声明退回 "general"；确需正定则给对角加一个小正数（<c>SetDiagonalMatrix</c> 或对角 <c>AddMatrix</c> 一个 λI）做 Tikhonov 式正则 [本库无现成正则算子，属手写方向]，而不是加大 epsilon 蒙混。全库 Grep 除定义外无本常量引用；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_NPD = 9211;

	/// <summary>矩阵元素除法除数为 0（取值 9212），原生文本 "Matrix element division by 0"。</summary>
	/// <remarks>
	///   <para><b>含义</b>本码特指<b>逐元素</b>除法：除数矩阵（<c>DivElementMatrix</c>/<c>DivElementMatrixMod</c> 的第二个入参，原生 id 850/849）里存在值为 0 的元素，被除数对应位置无法算出有限结果。托管侧不做任何预检，判 0 完全在原生层 [原生是报本码还是产出 Inf/NaN 待实测]。</para>
	///   <para><b>与相邻算子的取舍</b>最容易混的是 <c>operator /</c>：它把右操作数当系数矩阵调 <c>SolveMatrix</c>（id 828），是解方程组而非逐元素相除，那条路上的奇异性报 9200/9205 而不是本码。掩模归一化必须用 <c>DivElementMatrix</c>；除以同一个常数则用 <c>ScaleMatrixMod(1.0/k)</c>，连本码的触发面都没有。</para>
	///   <para><b>坑</b>0 常常不是"看起来是 0"而是被上游写成 0：<c>CreateMatrix</c>/<c>SetFullMatrix</c> 以 0 建阵后没赋全的位置、<c>MinMatrix</c>/<c>MaxMatrix</c> 归约或 <c>GetSubMatrix</c> 取块留下的占位零、以及用 <c>MultElementMatrix</c> 乘 0/1 掩模时被清零的无效位。浮点近零（1e-30）能躲过本码，却会在结果里炸出 1e30 量级，把下一次分解推到 9201/9210。</para>
	///   <para><b>处置</b>归一化前先处理除数的零位：<c>GetFullMatrix</c> 取回托管侧按下限截断，或用 <c>MultElementMatrix</c> 与掩模相乘把无效位显式置成约定值；不要指望"错一两个位置无所谓"。全库 Grep 除定义外无本常量引用；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_DBZ = 9212;

	/// <summary>矩阵不是上三角（按上三角路径算但下三角区非零，取值 9213）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Matrix is not an upper triangular matrix"：调用方把矩阵按上三角类型声明交给原生层（<c>SolveMatrix</c> 的 matrixLHSType、<c>InvertMatrix</c>/<c>DeterminantMatrix</c>/<c>DecomposeMatrix</c> 的 matrixType 中的三角专用取值），于是后端跳过下半部分直接回代；实际该矩阵主对角线以下存在非零元素，声明不成立 [具体字符串取值集合本仓库未声明，待实测]。</para>
	///   <para><b>与相邻码区分</b>9214 是同一判据的镜像（按下三角声明却含上三角残元），两码成对；9211 管"正定"这一更强前提。三码共性都是<b>声明与数据不符</b>，改数据或改声明都能解，只有 9208/9209 那种形状错必须改形状。</para>
	///   <para><b>何时遇到</b>三角因子的正常出处是 <c>OrthogonalDecomposeMatrix</c> 的 out <c>matrixTriangularID</c>（QR 的 R）与 <c>DecomposeMatrix</c> 的三角输出，返回的正交部分与 out 的三角部分顺序勿混：把 Q 当 R 传给按三角声明的下一步就报本码。另一类是分解之后又做过 <c>AddMatrix</c>/<c>MultMatrix</c>/<c>TransposeMatrixMod</c>——转置会把上三角原地变成下三角，旧声明随之失效。</para>
	///   <para><b>坑与处置</b>判定按"是否非零"而非"是否足够小"：上游浮点乘加留下的 1e-17 级尾数也算非零，照样触发本码 [是否带容差待实测]。稳妥做法是把非三角声明退回 "general" 让后端走通用路径，或构造时只填上三角、把下半显式清零（<c>SetFullMatrix</c> 一次写全或 <c>SetSubMatrix</c> 覆写下半），不要指望调 epsilon 绕过。全库 Grep 除定义外无本常量引用；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_NUT = 9213;

	/// <summary>矩阵不是下三角（按下三角路径算但上三角区非零，取值 9214）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Matrix is not a lower triangular matrix"：与 9213 完全镜像——调用方按"下三角"这一专用类型声明把矩阵交给原生层（<c>SolveMatrix</c> 的 matrixLHSType、<c>InvertMatrix</c>/<c>DeterminantMatrix</c>/<c>DecomposeMatrix</c> 的 matrixType 中的三角取值），后端只读主对角线以下，而矩阵在上半区还有非零元素，声明不成立 [允许取值集合待实测]。</para>
	///   <para><b>与相邻码区分</b>9213/9214 一起出现时几乎总是"因子拿错侧"：QR 的 R 是上三角（配 9213 那侧声明），LU/Cholesky 类分解的下三角因子才配本码这侧声明。哪个分解给出哪一侧、返回与 out 各装哪一侧，本库文档只标到"三角部分"这一层 [待实测]，收码后先打印 <c>GetSubMatrix</c> 取出的上半块确认形状，比改声明试错快。</para>
	///   <para><b>坑</b>分解后又做过 <c>AddMatrix</c>/<c>MultMatrix</c> 或 <c>TransposeMatrixMod</c> 的矩阵不再有任何三角性，沿用旧声明必撞本码；下三角声明撞上由 <c>TransposeMatrix</c> 得到的上三角，是最典型的一次转置错。判定按非零而非按量级，浮点尾数同样触发 [是否带容差待实测]。</para>
	///   <para><b>处置</b>声明退回 "general" 走通用路径，或把上半区显式清零后重试；这与 9211（正定前提）一样属"改声明/改数据"而非"改精度参数"。全库 Grep 除定义外无本常量引用；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_NLT = 9214;

	/// <summary>矩阵元素为负（该步要求非负却出现负元素，取值 9215）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Matrix element is negative"：某个语义上要求元素非负的矩阵运算拿到负值。本库最贴身的候选是逐元素开方 <c>SqrtMatrix</c>/<c>SqrtMatrixMod</c>（原生 id 843/842，作用于每个元素取 √x，不是矩阵平方根）以及逐元素取幂 <c>PowScalarElementMatrix</c>/<c>PowElementMatrix</c> 的负底组合 [原生层是报本码还是产 NaN 待实测]，托管层一律不预检。</para>
	///   <para><b>与相邻码区分</b>9220 管的是"指数矩阵里的值非法"（第二个矩阵作指数），本码管被运算矩阵自己的元素符号；9219 管结果需要复数域。若确实要复数域，本库 <c>JlMatrix</c> 元素只有 double（索引器与 <c>SetValueMatrix</c> 都只收实数），只能改算法或把实部/虚部分成两个矩阵承载（特征值族就是这么设计的）。</para>
	///   <para><b>坑</b>负值多半不是数据本身错，而是数值误差：方差/能量/协方差对角线这类"数学上必然非负"的量，经 <c>MultElementMatrix</c>、归约或上游拟合后常出现 -1e-15 级负尾数，正好把开方打回本码。另一条是 <c>*Mod</c> 原地覆盖链——上一步已经把 this 改写成别的矩阵，本步开方的其实不是你以为的那个非负矩阵。</para>
	///   <para><b>处置</b>开方前先 <c>AbsMatrixMod</c>（或 <c>GetFullMatrix</c> 在托管侧把负值按 0 截断后再 <c>SetFullMatrix</c> 写回），确认取绝对值在物理上说得通再动手；<c>SqrtMatrixMod</c> 反复调用是连续开方而非幂等，改完别忘了原值只能靠事先 <c>CopyMatrix</c> 找回。全库 Grep 除定义外无本常量引用；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_NEG = 9215;

	/// <summary>矩阵文件里有非法字符（读取时解析失败，取值 9216）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Matrix file: Invalid character"：矩阵文件内容能读到，但字符层面解析过不去——分隔符、编码（BOM）、数字写法（多余逗号、括号、科学计数法拼写）不合矩阵文件的文本语法 [具体允许字符集与本库不可考的格式细节待实测]。入口是 <c>ReadMatrix(string)</c> 与 <c>JlMatrix(string fileName)</c> 构造器，两者同走原生 id 819。</para>
	///   <para><b>与相邻码分工</b>矩阵文件三码按失败阶段递进：本码=字符级读不懂；9217=语法对但数值个数不够（矩阵不完整）；9218=整体格式就不是矩阵格式。文件根本不存在或不可读属系统层错误，不走这三码 [待实测]。</para>
	///   <para><b>坑</b><c>ReadMatrix</c> 的方法体是"先 <c>Dispose()</c> 释放旧句柄，再 StoreS 文件名、InitOCT+Load 原地装载"，所以读失败后本实例可能停在无效句柄状态：若调用方没接住异常就继续用它做运算，会撞回 9207（矩阵未定义），真正的原因却在更早的那次读取上。同一路径反复换文件读时务必逐次检查错误 [失败后句柄确切状态待实测]。</para>
	///   <para><b>处置</b>别用手写文本硬凑格式：优先用 <c>WriteMatrix</c>（id 820）的产物，或走 <c>SerializeMatrix</c>/<c>DeserializeMatrix</c>（id 818/817）的字节流；已在手工维护文本时，用文本编辑器核对首个数据行并保证纯 ASCII 数字与空白分隔。全库 Grep 除定义外无本常量引用；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_UNCHAR = 9216;

	/// <summary>矩阵文件不完整（声明的矩阵比文件里实际读到的数值多，取值 9217）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Matrix file: matrix incomplete"：文件头层面接受（不像 9216 撞字符、也不像 9218 撞格式），但按声明的形状继续读数值时到了文件尾还没读满——即矩阵被截断 [头是否显式声明行列数系由本码文案推断，待实测]。入口同 id 819（<c>ReadMatrix</c> / <c>JlMatrix(string)</c>）。</para>
	///   <para><b>何时遇到</b>三类典型：写文件的进程被中途杀死或磁盘满；文件经网络/网盘同步只落了半截；手工从表格复制粘贴漏行。图像族里 BMP 截断有各自的 55xx 码，两族互不相干，别拿图像族的处置经验套过来。</para>
	///   <para><b>坑</b>最坏的反应是"少几个数就补 0 凑齐"：凑出来的矩阵维度与真实数据不一致，随后 <c>InvertMatrix</c>/<c>SvdMatrix</c> 都能"成功"，只是结果毫无意义——症状会推迟到匹配或标定结果异常时才被发现，比直接报错难查得多。<c>ReadMatrix</c> 内部先 <c>Dispose()</c> 再装载，本码抛出后句柄状态同样可疑（同 9216）。</para>
	///   <para><b>处置</b>重新完整导出（同机同版本工具链写出），核对文件字节数与预期规模是否相称；若必须抢救部分数据，只能在托管侧自行取文件中完整的那几行，按实际行数 <c>CreateMatrix</c> 后用 <c>SetFullMatrix</c>/<c>SetValueMatrix</c> 重建，而不是往残文件里灌假值。全库 Grep 除定义外无本常量引用；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_NOT_COMPLETE = 9217;

	/// <summary>矩阵文件的格式非法（整体结构不被识别为矩阵，取值 9218）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid file format for matrix"：文件在"是不是矩阵文件"这一层就没通过——拿错了别的类型的文件、把二进制当文本读、或文件被改过头部。与 9216/9217 的分工：那两码是"认得出是矩阵文件但读不成"（字符级、数据量级），本码是"根本不认这个格式"。</para>
	///   <para><b>何时遇到</b>最典型是把 <c>SerializeMatrix</c>（id 818）产出的 byte[] 自己写盘后再用 <c>ReadMatrix</c>（id 819）读回：文件落盘与序列化流是两套格式，本库文档明确不保证互相通用 [待实测]；其次是 <c>WriteMatrix</c> 的 fileFormat 字符串与读回时的解析档不匹配，或拼写不是原生支持的取值（文档可见的取值只有 "binary"）[允许取值集合待实测]。文件不存在或无权限属系统层错误，不走本码 [待实测]。</para>
	///   <para><b>坑</b>本码是"格式"错而不是"内容"错：换一台机器、换一个工具链版本后同一个文件可能从 9218 变成能读，也可能读出来了但数值解释方式不同（例如二进制字节序）而无人报错——跨版本/跨机复用矩阵文件前做一次 <c>GetSizeMatrix</c> 与抽样取值核对。</para>
	///   <para><b>处置</b>先分清文件来源：用配套 <c>WriteMatrix</c> 重新导出，或统一改走 <c>DeserializeMatrix(byte[])</c> 读序列化缓冲；<c>ReadMatrix</c> 失败后本实例可能已是无效句柄（内部先 Dispose，同 9216），别接着用它。全库 Grep 除定义外无本常量引用；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_READ = 9218;

	/// <summary>运算结果是复数矩阵（本库矩阵只能装实数，取值 9219）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Resulting matrix has complex values"：这一步的数学结果落在复数域，而 <c>JlMatrix</c> 的元素一律是 double（索引器、<c>SetValueMatrix</c>、<c>GetFullMatrix</c> 全为实数语义），没有虚部可放，于是直接拒绝而不是静默丢虚部。</para>
	///   <para><b>与相邻算子的取舍</b>本库对复结果的正规出路是"拆成两个实矩阵"：<c>EigenvaluesGeneralMatrix</c>（id 826）与 <c>GeneralizedEigenvaluesGeneralMatrix</c>（id 824）各输出特征值实部、虚部与特征向量实部、虚部四个 out 句柄，成对读才表示完整复特征值，只取实部那一个就是丢信息。反过来对称版 <c>EigenvaluesSymmetricMatrix</c>（id 827）与 <c>GeneralizedEigenvaluesSymmetricMatrix</c>（id 825）只出一个实特征值矩阵——理论前提成立时结果必为实数，一旦矩阵其实不对称（或广义版里 B 不满足对称正定）就可能以本码收场 [两码与本码/9211 的确切分工待实测]。</para>
	///   <para><b>坑</b>"看着对称"不等于对称：上游 <c>MultMatrix</c>、<c>AddMatrix</c> 出来的矩阵常带 1e-16 级反对称残差，按对称路径走正是本码与 9211 的高发点。别用 <c>GetSubMatrix</c> 半块自欺，真要按对称处理就先显式对称化 (A+Aᵀ)/2。</para>
	///   <para><b>处置</b>换一般版（复结果合法），或改走 <c>SvdMatrix</c>——奇异值恒为实数，任何形状、任何条件都不需要复数域，是绕开本码最彻底的替代。一般版四个输出句柄任一漏 <c>Dispose</c> 都会泄漏原生矩阵内存。全库 Grep 除定义外无本常量引用；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_COMPLEX = 9219;

	/// <summary>指数矩阵里有非法值（取值 9220）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Wrong value in matrix of exponents"：逐元素乘幂 <c>JlMatrix.PowElementMatrix</c>/<c>PowElementMatrixMod</c> 以第二个矩阵作逐元素指数，某个位置的指数与底数组合非法（典型：负底配非整数指数 [待实测]）。</para>
	///   <para><b>坑</b>本码特指"指数矩阵"（<c>JlMatrix</c> 句柄参数）那条路；<c>PowMatrix</c> 系列（矩阵整体幂，指数是标量/元组）的非法指数走的是别的路径，两条别混。</para>
	///   <para><b>处置</b>先 <c>AbsMatrix</c> 或过滤指数再乘幂；若确需复数域，本库矩阵只能装实数（参见 9219）。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WMATEXP = 9220;

	/// <summary>矩阵文件的版本不被支持（取值 9221）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "The version of the matrix is not supported"：<c>JlMatrix.ReadMatrix</c>/构造器 <c>JlMatrix(string)</c>（原生 id 819）读文件时，文件头版本号能解析但运行时不支持（多为新版工具写、旧版运行时读 [待实测]）。</para>
	///   <para><b>与相邻码区分</b>9218 是"根本不像矩阵文件"（格式不认），本码是"像但太新/太旧"；9217 是"像且版本对但内容残缺"。三码对应三种修复：换文件、升运行时、重导出。</para>
	///   <para><b>处置</b>用当前库 <c>WriteMatrix</c>（id 820）重新落盘一份再读即可自证；升级运行时或同版本平台重存。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；注意读失败后 <c>ReadMatrix</c> 已把本句柄 Dispose 过，别继续用 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_WRONG_VERSION = 9221;

	/// <summary>反序列化输入项里没有有效矩阵（取值 9222）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Serialized item does not contain a valid matrix"：<c>JlMatrix</c> 侧触发点是 <c>DeserializeMatrix(byte[])</c>（原生 id 817）与静态 <c>Deserialize(Stream)</c>——喂进去的字节不是 <c>SerializeMatrix()</c>/Serialize 的矩阵序列化产物（别的对象类型的缓冲、或字节被截断）。</para>
	///   <para><b>坑</b><c>DeserializeMatrix</c> 内部先 <c>Dispose()</c> 再装载：解失败时 this 已处于无效句柄态，原矩阵没保住也不能复用，catch 之后不要拿同一对象继续算，须重新 Create/构造。</para>
	///   <para><b>处置</b>核对字节来源类型与完整性（序列化流与文件格式不互相通用 [待实测]）。≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_MAT_NOSITEM = 9222;

	/// <summary>内部错误：节点引用不对（取值 9230）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Internal Error: Wrong Node"：原生树/链式结构在查找或插入时遇到不该出现的节点（空挂、类型不符、断链 [待实测——细节本库不可考]）。与 9231（红黑树整体不一致）同族，都是内核自校验失败。</para>
	///   <para><b>坑</b>名字像图结构错，容易被拿去和 XLD 轮廓/区域拓扑联系——那些层不会回这个原生码；本码与图形数据无关。</para>
	///   <para><b>处置</b>非用户可修错误：最小化复现并上报。全库 Grep 无本常量引用，仅作原生错误表保留项；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_WNODE = 9230;

	/// <summary>内部红黑树结构不一致（取值 9231）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Inconsistent red black tree"：原生层内部用红黑树维护查找/比较结构，其不变量（节点着色、子树黑高）被破坏。用户无法直接触发也无法修复，属内核自校验失败的 Internal error。</para>
	///   <para><b>与相邻码区分</b>9230（Wrong Node）是同族近亲：那是不期望的节点引用，本码是整棵树的平衡性质崩了；两者处置一致（复现上报），不必细分。</para>
	///   <para><b>处置</b>记录触发时的算子与输入上报；重试或改参数无效。全库 Grep 无本常量引用，仅作原生错误表保留项；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_CMP_INCONSISTENT = 9231;

	/// <summary>内部错误：传给 LAPACK 数值库的参数非法（取值 9250）。</summary>
	/// <remarks>
	///   <para><b>含义</b>常量名指向模块（LAPACK 参数），内嵌原生文本只有笼统的 "Internal error"：原生层把矩阵交给 LAPACK 做分解/求解时，数组维度、leading dimension 等内存布局描述与实际不符，被数值库入口检查拦下。</para>
	///   <para><b>何时遇到</b><c>JlMatrix</c> 的分解、特征值、SVD、求解一族原生调用（id 821-828）是可能的宿主；但本码指原生内部不自洽，C# 侧参数写法基本改不动它。</para>
	///   <para><b>处置</b>记录矩阵行列数与调用序列最小化复现上报；不要臆断为"参数传错"去反复改 <c>matrixType</c> 之类字符串。≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_LAPACK_PAR = 9250;

	/// <summary>三角化输入点数太少（取值 9260）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Number of points too small"：点数不足以启动三角化（至少 3 个不共线点，某些模式要求更多 [待实测]）。平面网格化同义码 9280。</para>
	///   <para><b>何时遇到</b>多为上游过滤把点集筛空，或单点/两点退化成"线段输入"直接喂进面算法 [待实测]。本库无三角化包装，Grep 无引用，仅作原生错误表保留项。</para>
	///   <para><b>处置</b>调用前点数预检（<c>JlTuple</c> 长度判一下）比捕获本码便宜。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_STRI_NPNT = 9260;

	/// <summary>前三个点共线（取值 9261）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "First 3 points are collinear"：三角化用输入序的前三点建初始三角形，共线则构网起点都不存在。平面网格化同义码 9281。</para>
	///   <para><b>坑</b>只检查前三个：调整点序把三个不共线点挪到最前即可通过本关；但点集整体近共线时，后续会以 9264（退化三角形）再报——本码通过不等于数据健康。</para>
	///   <para><b>处置</b>重排点序是正解，别去放宽共线容差掩盖问题 [待实测——容差参数是否存在本库不可考]。本库无三角化包装，Grep 无引用，仅作原生错误表保留项；≥1000 属真错误。</para>
	/// </remarks>
	public const int Jl_ERR_STRI_COLL = 9261;

	/// <summary>三角化输入里存在重合点（取值 9262）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Identical points in triangulation"：两点坐标完全相同，产生零长度边，Delaunay 一类的构网在其上不确定。平面网格化同义码 9282。</para>
	///   <para><b>坑</b>"相同"按精确相等判：近乎重合但不同的点放过本关后，会以退化三角形（9264）的形式回来找你；去重容差要在预处理里一次定好。</para>
	///   <para><b>处置</b>进三角化前按容差合并重复点。本库无三角化包装，Grep 无引用，仅作原生错误表保留项；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_STRI_IDPNT = 9262;

	/// <summary>三角化内部数组分配不足（取值 9263）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Array not allocated large enough"：按输入规模预估分配的缓冲不够写——三角形数/邻接数超出预估（极端退化点集是常见推手 [待实测]）。平面网格化模块同义码 9284。</para>
	///   <para><b>坑</b>这是原生层记账错误，用户没有"把数组调大"的参数可改；唯一可动的旋钮是缩小单次输入规模。</para>
	///   <para><b>处置</b>分批/子采样后重试可绕过，根治需上报。本库无三角化包装，Grep 无引用，仅作原生错误表保留项；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_STRI_NALLOC = 9263;

	/// <summary>三角形退化（取值 9264）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Triangle is degenerate"：三角化过程中出现面积为 0 或近 0 的三角形——三点共线或两顶点重合。退化三角形上做的插值/法向计算数值全不可信，算法宁可报错也不产出。</para>
	///   <para><b>坑</b>它是"事后失败"：通过了"前三点共线"检查（9261）不代表后面不会共线——点集里任意近共线三点都可能让算法走到一半才爆本码。批量数据先做共线性预筛能省掉半途失败。</para>
	///   <para><b>处置</b>删近共线/重合点或放宽退化判定阈值 [待实测——本库无该阈值参数可查]。本库无三角化包装，Grep 无引用，仅作原生错误表保留项；≥1000 属真错误。</para>
	/// </remarks>
	public const int Jl_ERR_STRI_DEGEN = 9264;

	/// <summary>三角化结果不一致（取值 9265）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Inconsistent triangulation"：已建三角网格的内部不变量被破坏（邻接关系、共边配对着不上）。属三角化内核层错误，不是输入数据非法；平面网格化模块的同义码是 9285。</para>
	///   <para><b>坑</b>与 9267（输入多边形数据不一致）方向相反：9267 错在你的输入表，本码错在算法产出的网格——改输入通常没用，该复现上报。</para>
	///   <para><b>处置</b>记录点集/参数最小化复现并上报；若输入含退化结构（重复点、自交边界），先修数据可间接绕开。本库无三角化包装，Grep 无引用，仅作原生错误表保留项；≥1000 属真错误。</para>
	/// </remarks>
	public const int Jl_ERR_STRI_ITRI = 9265;

	/// <summary>多边形自交（取值 9266）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Self-intersecting polygon"：边界多边形存在不相邻的边相互交叉，围出"8 字"形——区域内外失去良定义，带约束的三角化无法判定哪些面在内部，直接拒算。</para>
	///   <para><b>坑</b>视觉上不觉得自交的近退化多边形（相邻顶点重复、针状回勾）同样触发本码；且自交点若恰好重合于顶点，报错文本可能只给本码不给位置 [待实测]。</para>
	///   <para><b>处置</b>先做多边形自交检测/简化再进三角化。本库无对应包装，Grep 无本常量引用，仅作原生错误表保留项；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_STRI_SELFINT = 9266;

	/// <summary>多边形数据不一致（取值 9267）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Inconsistent polygon data"：作为三角化边界的多边形集在拓扑上自相矛盾——公共边两侧记录不吻合、孔洞归属错乱等。与 9266（自交）区分：9266 是几何层面边交叉，本码是数据/拓扑层面拼不上 [待实测——两码边界系推断]。</para>
	///   <para><b>处置</b>由单一数据源一次性生成多边形集（含内外环顺序），不要手工拼接多个来源的顶点表。本库无多边形三角化包装，Grep 无本常量引用，仅作原生错误表保留项；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_STRI_INCONS = 9267;

	/// <summary>大圆弧相交结果不唯一（取值 9268）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Ambiguous great circle arc intersection"：球面上两条大圆弧的交点不止一个可接受解（弧段重叠共线，或交点落在两端点的临界处），三角化无法裁决取哪个交点。姊妹码 9269（弧本身不唯一）。</para>
	///   <para><b>何时遇到与处置</b>多发生在两点近对径或弧共线的退化几何 [待实测]。本库无球面/三角化包装，Grep 无本常量引用，仅作原生错误表保留项；处置方向同样是去退化而非重试。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_STRI_AMBINT = 9268;

	/// <summary>球面大圆弧不唯一：弧段本身无法被唯一定义（取值 9269）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Ambiguous great circle arc"：球面上两点一般确定唯一短弧，但当两点趋于对径（相距半个大圆）时，过两点的大圆有无穷多个，"哪段弧"失去唯一答案，球面三角化的边就定不下来。姊妹码 9268（两弧交点不唯一），同根成因是对径/半周长配置。</para>
	///   <para><b>何时遇到与处置</b>本库无球面三角化包装，Grep 无本常量引用，仅作原生错误表保留项。方向是把对径点扰动或合并，退化几何改数据而不是改参数。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_STRI_AMBARC = 9269;

	/// <summary>三角化模块收到非法参数（取值 9270）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Illegal parameter"（笼统，不指明哪个参数）：三角化算法的约束参数（阈值/模式类）越界 [待实测——具体参数集本库不可考]。与通用参数族区分：13xx 段是"所有算子共用的值域检查"，本码是三角化模块内部的专属校验，说明值过了通用检查却没过后端算法检查。</para>
	///   <para><b>处置</b>回查三角化调用的模块私有参数而非公共参数表；用 <c>JlNativeApi.GetErrorMessage</c> 取文本辅助定位。本库无三角化包装，Grep 无引用，仅作原生错误表保留项；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_STRI_ILLPAR = 9270;

	/// <summary>平面三角网格化的点数不足（取值 9280）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Not enough points for planar triangular meshing"：少于 3 个点连一个三角形都拼不出，网格化无从开始。通用三角化同义码是 9260（STRI_NPNT）。</para>
	///   <para><b>何时遇到</b>多见于上游过滤（阈值、区域交点筛选）过狠把点抽空——本码常在管线"静默变空"之后才现身，值得回头查过滤器而不是网格化本身 [待实测]。本库无网格化包装，Grep 无引用，仅作原生错误表保留项。</para>
	///   <para><b>处置</b>放宽上游筛选或对本步做空点集短路（点数判 0 直接跳过），比捕获异常省。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_TRI_NPNT = 9280;

	/// <summary>平面网格化的前三个输入点共线（取值 9281）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "The first three points of the triangular meshing are collinear"：算法以输入序的前三点建立初始参考三角形，三点共线则初始三角形不存在，从头失败。通用三角化同义码是 9261（STRI_COLL）。</para>
	///   <para><b>坑</b>判的是"第 1、2、3 个点"这三点，不是"整体存在共线"：点集里其他地方共线不触发本码。因此打乱或挑选前三点即可绕过，别误以为整批数据报废。</para>
	///   <para><b>处置</b>把三个不共线的点排到输入最前面再调网格化；放宽数值容差不是正解。本库无网格化包装，Grep 无引用，仅作原生错误表保留项；≥1000 属真错误。</para>
	/// </remarks>
	public const int Jl_ERR_TRI_COLL = 9281;

	/// <summary>平面网格化输入点里存在重合点（取值 9282）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Planar triangular meshing contains identical input points"：两点坐标完全相同会造出零长度边，三角化拓扑退化。通用三角化同义码是 9262（STRI_IDPNT）。</para>
	///   <para><b>坑</b>按"完全相同"判重：距离 1e-12 的不重复点能过本关，却会在后续产生极瘦长的退化三角形，症状推迟到 9264（STRI 族退化）一类码才爆——去重阈值要按量纲取epsilon级，别只删严格相等。</para>
	///   <para><b>处置</b>网格化前做点集合并去重；本库无网格化包装，Grep 无引用，仅作原生错误表保留项；≥1000 属真错误。</para>
	/// </remarks>
	public const int Jl_ERR_TRI_IDPNT = 9282;

	/// <summary>平面网格化的输入点非法（取值 9283）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid points for planar triangular meshing"：点坐标本身不可参与计算（NaN/无穷等非法数值，或落在计算域外 [待实测]）。与 9282 区分：9282 是"点合法但彼此重复"，本码是"点本身不合法"。</para>
	///   <para><b>处置</b>网格化前先过滤非有限坐标；上游拟合/变换产生 NaN 时要回查那一步而不是网格化。本库无网格化包装，Grep 无引用，仅作原生错误表保留项；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_TRI_IDPNTIN = 9283;

	/// <summary>内部错误：平面网格化的预配数组过小（取值 9284）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Internal error: allocated array too small for planar triangular meshing"：内部按输入规模预估分配的缓冲不够用——极端退化点集使三角形数量超出常规预估时可能发生 [待实测]。</para>
	///   <para><b>与相邻码区分</b>同义的通用三角化码是 9263（STRI_NALLOC）；本码限定在平面网格化模块。两码都是内核层记账错，不是用户参数错。</para>
	///   <para><b>处置</b>缩小单次输入（分批/子采样）重试可绕过；根治靠上报原生层修正预估。本库无网格化包装，Grep 无引用，仅作原生错误表保留项；≥1000 属真错误。</para>
	/// </remarks>
	public const int Jl_ERR_TRI_NALLOC = 9284;

	/// <summary>内部错误：平面三角网格化结果不一致（取值 9285）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本自带 "Internal error" 前缀：生成的网格拓扑没通过自洽检查（相邻三角形共边对不上、覆盖不闭合）。这不是输入数据非法，而是网格化算法内部不变量被破坏。</para>
	///   <para><b>与相邻码区分</b>9280-9283 是数据层（点数、共线、重复、非法点），用户可修；本码与 9284（缓冲不足）属内核层，用户改参数一般无效。一般三角化的同义码在 STRI 族（9265）。</para>
	///   <para><b>处置</b>记录输入点集与参数复现上报；本库未提供网格化包装、全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_TRI_ITRI = 9285;

	/// <summary>节点索引越出三角化范围（取值 9286）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Node index outside triangulation range"：给出的节点/点索引不在当前网格的顶点范围内——多半是外部索引与网格版本不配套：网格重算过一次，手里还拿着旧序号。</para>
	///   <para><b>坑</b>只有越界的索引才报本码；若索引在界内但指向错误顶点（重排后的"同号异点"），会静默取错点而不报错。凡跨调用缓存过索引，网格一变必须全部作废。</para>
	///   <para><b>何时遇到与处置</b>本库未提供网格化/三角化包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；处置是当场由网格对象重新导出索引，不做长期缓存。</para>
	/// </remarks>
	public const int Jl_ERR_TRI_OUTR = 9286;

	/// <summary>所有具邻域点的点局部不一致（取值 9290）：参数只允许极少有效邻域，或点云未经子采样。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本把两种成因写进了括号里——邻域参数过严以致几乎没有点能通过邻域校验，或稠密点云没先子采样导致每个点的局部结构都相互矛盾，平面网格化无法推进。</para>
	///   <para><b>坑</b>TRI 族里 9280-9286 都指"输入数据本身不合法"（点数不足/共线/重复），本码却指"参数与点密度不匹配"：拿清洗数据的思路（去重、删点）修不动它，方向在预处理与参数。</para>
	///   <para><b>何时遇到与处置</b>本库未提供平面网格化包装，全库 Grep 无本常量引用，仅作原生错误表保留项。方向：放宽邻域参数或先子采样点云。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_TRI_LOCINC = 9290;

	/// <summary>视点与参考点重合（取值 9300）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Eye point and reference point coincide"：投影几何里相机视点（eye point）与参考点落在同一位置，视线方向失去定义，投影/视角变换退化。下游常表现为 9499（视角位姿估计失败）。</para>
	///   <para><b>何时遇到</b>本库已删除 3D 投影/标定包装（<c>JlHomMat3D</c>、<c>JlCamPar</c> 等已移除；2D 的 <c>JlHomMat2D</c> 投影族不使用本码），全库 Grep 无本常量引用，仅作原生错误表保留项 [待实测]。</para>
	///   <para><b>处置</b>≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。解法是几何去退化：把视点与参考点拉开，重试不改数据无效。</para>
	/// </remarks>
	public const int Jl_ERR_WSPVP = 9300;

	/// <summary>对偶四元数的实部长度为 0（取值 9310）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Real part of the dual quaternion has length 0"：对偶四元数用实部（单位四元数）编码旋转、对偶部编码平移；实部范数为 0 时不表示任何旋转，归一化与位姿转换在数学上就不成立。</para>
	///   <para><b>何时遇到</b>本库已删除对偶四元数/四元数类型（<c>JlDualQuaternion</c>、<c>JlQuaternion</c> 均不存在），全库 Grep 无本常量引用，仅作原生错误表保留项；若出现，多为上游把全零 4 向量当位姿写出 [待实测]。</para>
	///   <para><b>处置</b>≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。使用侧给位姿前校验四元数范数非零、非退化，比捕获本码更前置。</para>
	/// </remarks>
	public const int Jl_ERR_DQ_ZERO_NORM = 9310;

	/// <summary>算子执行超时被中止（取值 9400），原生文本 "Timeout occurred"。</summary>
	/// <remarks>
	///   <para><b>含义</b>被 <c>JlOperatorSet.SetOperatorTimeout</c>（原生 id 2008；timeout 单位秒、Default: 1；mode 默认 "cancel"）登记过的算子超过时限被中止，本次输出不完整、不可信。</para>
	///   <para><b>归类</b>同一事件在码表里有两段：小码段 23 <c>Jl_ERR_TIMEOUT_BREAK</c>（<c>JlNativeApi.IsError</c> 判 false 的"内部打断"标记）与本码（≥1000 真错误段）；哪条路径回哪个码取决于原生实现 [待实测]。两段的统一检查都会抛 <c>JlOperatorException</c>。</para>
	///   <para><b>处置</b>调大该算子登记的 timeout、放宽时限，或优化/拆分慢算子；不是参数错，别去翻算子参数。超时中断下输出句柄状态不明，勿继续消费本次输出。</para>
	/// </remarks>
	public const int Jl_ERR_TIMEOUT = 9400;

	/// <summary>设置算子超时时 'timeout' 参数值非法（取值 9401）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid 'timeout'"：登记超时时给出的秒数不合法。本库入口是 <c>JlOperatorSet.SetOperatorTimeout(operatorName, timeout, mode)</c>（原生算子 id 2008，timeout 单位秒、Default: 1）；具体允许域（0/负数/上限）在包装层不可见 [待实测]。</para>
	///   <para><b>坑</b>与 9400 一字之差、方向相反：9401 是"登记配置错"（设超时那一刻就报），9400 是"运行真超时"（算子执行中触发）。别把 9401 当性能问题去优化算法。</para>
	///   <para><b>处置</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），统一检查抛 <c>JlOperatorException</c>；改正登记值即可，与算子本身无关。</para>
	/// </remarks>
	public const int Jl_ERR_WRONG_TIMEOUT = 9401;

	/// <summary>参数 part_size 取值非法（取值 9450）；常量名写 WRONG_NUM_CLUSTER，原生文本却是 "Invalid 'part_size'"。</summary>
	/// <remarks>
	///   <para><b>含义</b>以文本为准：出错的参数是 part_size（模型分块/分区尺寸类参数 [待实测——精确语义本库不可考]）。名与文一对错位：按 "cluster" 检索文档或按常量名猜参数名去改设置，都会改错地方。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；以 <c>JlNativeApi.GetErrorMessage</c> 的文本定位到 part_size 再改。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_NUM_CLUSTER = 9450;

	/// <summary>参数 min_size 取值非法（取值 9451）；常量名含 NUM、原生文本却只写 Invalid 'min_size'，名与文不完全对齐。</summary>
	/// <remarks>
	///   <para><b>含义</b>以文本为准：min_size（最小尺寸阈值类参数）越界 [待实测——该参数精确语义本库不可考；常量名的 NUM 提示原生早期文本可能是"个数"类含义，后简化为现文本]。</para>
	///   <para><b>坑</b>与 9458 的英文文本一字不差（都是 "Invalid 'min_size'"），检索文档或做码表比对时两条会互相"撞文本"，务必以数值 9451/9458 与常量名区分。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_NUM_MIN_SIZE = 9451;

	/// <summary>最小二乘迭代次数非法（取值 9452）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid number of least-squares iterations"：可变形模型匹配的精修阶段按最小二乘迭代收敛位姿，迭代次数给了 0/负数/超上限即本码 [待实测——上限数值本库不可考]。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。次数是"越多越精、越慢"的权衡量，报本码时先确认没传成 0。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_NUM_LSQ = 9452;

	/// <summary>参数 angle_step 取值非法（取值 9453）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid 'angle_step'"：训练可变形模型时的角度离散化步长；步长越小模板姿态越密，训练耗时与匹配耗时按约 1/step 膨胀 [待实测——单位度数/弧度本库不可考]。非法值（负、零、超过角度总范围）报本码。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_ANGLE_STEP = 9453;

	/// <summary>参数 scale_r_step 取值非法（取值 9454）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid 'scale_r_step'"：行方向（r = row，全库坐标约定 row = y 向下为正）的缩放搜索步长 [待实测——精确语义本库不可考]；与 9455 的 c（列方向）步长成对。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；负值/零/过大先查这三样。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_SCALE_R_STEP = 9454;

	/// <summary>参数 scale_c_step 取值非法（取值 9455）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid 'scale_c_step'"：列方向（c = column，全库坐标约定 column = x 向右为正）的缩放搜索步长 [待实测——精确语义本库不可考]；与 9454 的 r（row 行方向）步长成对。步长越小模板越密、训练与匹配越慢。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；负值/零/过大先查这三样。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_SCALE_C_STEP = 9455;

	/// <summary>参数 max_angle_distortion 取值非法（取值 9456）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid 'max_angle_distortion'"：训练期允许的最大局部角度畸变 [待实测——单位角度/弧度与上限本库不可考]；与 9457（各向异性缩放畸变）成对，一个管转动畸变一个管拉伸畸变。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；按原生消息核对取值域。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_MAX_ANGLE = 9456;

	/// <summary>参数 max_aniso_scale_distortion 取值非法（取值 9457）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid 'max_aniso_scale_distortion'"：可变形模型训练期允许的最大各向异性缩放畸变——行、列方向缩放变化幅度之比的上限 [待实测——精确定义本库不可考]；负数、零或过大都会触发本码。姊妹码 9456（最大角度畸变）。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误；把它当"训练参数越界"处理，取值先回默认再逐步放宽。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_MAX_ANISO = 9457;

	/// <summary>参数 min_size 取值非法（取值 9458）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid 'min_size'"：训练可变形模型时的最小尺寸阈值参数越界（如负值或超过图像尺寸）[待实测——本库无该参数文档可考]。</para>
	///   <para><b>坑</b>9451（常量名 WRONG_NUM_MIN_SIZE）与 9458（WRONG_MIN_SIZE）共享同一句英文 "Invalid 'min_size'"，靠文本无从分辨，只能以数值区分两码；两者哪个对应训练哪个对应匹配，本库不可考 [待实测]。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_MIN_SIZE = 9458;

	/// <summary>参数 cov_pose_mode 取值非法（取值 9459）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid 'cov_pose_mode'"：位姿协方差计算模式是个字符串标志参数，传了不在允许集合里的拼写即本码（允许值集合本库不可考 [待实测]）。属"值不合法"而非 12xx 的"类型不对"：给对了字符串类型但词不在表内。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；按原生消息核对合法模式名。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_COV_POSE_MODE = 9459;

	/// <summary>模型不含标定信息（取值 9460）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Model contains no calibration information"：需要真实尺度/相机几何的步骤（以标定换物理单位的匹配或位姿输出）用在了训练时未随附标定的模型上。属模型属性缺失，不是参数错误。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装与相机参数类型，全库 Grep 无本常量引用，仅作原生错误表保留项。方向是带标定重训模型，或改用像素域模式回避该前提 [待实测]。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_NO_CALIBRATION_INFO = 9460;

	/// <summary>给定的通用参数名不存在（取值 9461）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Generic parameter name does not exist"：读写模型通用参数（gen param / get param 那对接口）时给了参数表里没有的名字 [待实测——本库无可变形模型文档核对合法名集合]。参数名区分大小写，写错一个字母即本码。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。排障靠先读回全部参数名再核对，而不是猜拼写。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_PARAM_NAME = 9461;

	/// <summary>相机分辨率与图像尺寸不一致（取值 9462）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本（小写开头）"camera has different resolution than image"：带标定信息走模型流程时，随附相机参数的成像尺寸与实际图像宽高对不上——投影/坐标换算失去一致的定义域。</para>
	///   <para><b>何时遇到</b>换了相机或改过采集分辨率却没重建/重标模型，或把 A 相机的标定配到 B 相机拍的图上 [待实测]。本库已删除可变形模型与相机参数类型（<c>JlCamPar</c> 已移除），全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	///   <para><b>处置</b>核对图像来源与标定归属是否同一相机同档分辨率；改参数救不了，须数据配套。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_IMAGE_TO_CAMERA_DIFF = 9462;

	/// <summary>读取可变形模型时文件格式非法（取值 9463）；常量名写 NO_MODEL_IN_FILE，原生文本是 "Invalid file format for deformable model"。</summary>
	/// <remarks>
	///   <para><b>含义</b>以文本为准：文件结构解析不成可变形模型（拿错文件类型、内容损坏或截断）。常量名直译"文件里没有模型"容易误导成"路径不存在"——文件不存在属系统层错误，不走本码。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。用配套的模型保存流程重新导出；≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_NO_MODEL_IN_FILE = 9463;

	/// <summary>可变形模型文件的版本不被支持（取值 9464）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "The version of the deformable model is not supported"：文件头版本号能读出但运行时不支持。三兄弟分工：9463 格式不认（读不出结构）、本码版本拒载（认得但太新/太旧）、9468 反序列化项类型不对（根本不是模型）。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。方向是升级运行时或用同版本工具链重存文件；重试原文件必复发。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_VERSION = 9464;

	/// <summary>参数 deformation_smoothness 取值非法（取值 9465）；常量名简写作 SMOOTH_DEFORM，指的是同一参数。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid 'deformation_smoothness'"：该参数权衡形变场的平滑度/正则强度——越小越允许剧烈形变 [待实测——本库无可变形模型文档可考]；越出允许范围即本码。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；按原生文本提示修正取值。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_SMOOTH_DEFORM = 9465;

	/// <summary>参数 expand_border 取值非法（取值 9466）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid 'expand_border'"：expand_border 用于把模板图像的边界向外扩张、给模型留边缘余量 [待实测——本库无该参数文档可考]；负数、非整数或超上限的取值会以本码拒绝。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误；真收到时按 <c>JlNativeApi.GetErrorMessage</c> 的原生提示核对该参数取值域，不属参数族 14xx 的"个数不对"，是单值合法性问题。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_WRONG_EXPAND_BORDER = 9466;

	/// <summary>模型原点落在模板区域的轴对齐包围矩形之外（取值 9467）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Model origin outside of axis-aligned bounding rectangle of template region"：训练可变形模型时给定的参考点（原点 row/column）不在模板区域的外接矩形内。可变形模型以原点为锚做局部形变展开，原点必须在模板"地盘"里，否则形变场失去支撑。</para>
	///   <para><b>何时遇到</b>手工指定原点偏出区域，或模板区域在上游被收缩后沿用旧原点 [待实测]。本库已删除可变形模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	///   <para><b>处置</b>把原点改到区域内（区域重心最稳妥）。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_ORIGIN_OUTSIDE_TEMPLATE = 9467;

	/// <summary>反序列化输入项里不含有效的可变形模型（取值 9468）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Serialized item does not contain a valid deformable model"：给模型反序列化的缓冲不是可变形模型的序列化产物。与 9463（文件格式非法）、9464（版本不支持）区分：本码是"类型不对"，那两个是"文件/版本不对"。</para>
	///   <para><b>坑</b>NOSITEM 后缀码按族各有一枚（矩阵 9222、表面模型 9508、本码），错投他类缓冲触发的是接收方那一族的 NOSITEM，而非发送方的码；据码定位出错的是哪次读取。</para>
	///   <para><b>何时遇到与处置</b>本库已删除可变形模型能力（<c>JlDeformableModel</c> 一类类已不存在），全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；重试无效，核对缓冲来源。</para>
	/// </remarks>
	public const int Jl_ERR_DEFORM_NOSITEM = 9468;

	/// <summary>视角位姿（viewpose）估计失败（取值 9499）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Estimation of viewpose failed"：求解相机视角位姿的数值步骤失败（无法收敛或几何约束退化）。本码不指明具体成因，须配 <c>JlNativeApi.GetErrorMessage</c> 查原生文本；上游几何退化（如 9300 视点与参考点重合）常以本码为最终表象。</para>
	///   <para><b>何时遇到</b>本库已删除 3D 投影/标定包装，全库 Grep 无本常量引用，仅作原生错误表保留项 [待实测]。</para>
	///   <para><b>处置</b>≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。排查方向是输入几何构型（对应点分布、退化配置），单纯重试无效。</para>
	/// </remarks>
	public const int Jl_ERR_VIEW_ESTIM_FAIL = 9499;

	/// <summary>3D 物体模型为空：没有任何点（取值 9500）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Object model has no points"：模型连最基础的点集都是空的；面（9501）与法向量（9502）都以点为载体，本码是三兄弟里最底层的缺失。</para>
	///   <para><b>何时遇到</b>训练/导入管线以 0 点输入跑完得到空壳模型，或模型句柄被提前清空后仍被下游引用 [待实测]。本库已删除 3D 物体/表面模型能力，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	///   <para><b>处置</b>回查模型生成步骤的输入点数统计；≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true），统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_SFM_NO_POINTS = 9500;

	/// <summary>3D 物体模型缺少面（三角面片）数据（取值 9501）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Object model has no faces"：模型只有散点、没有三角面片拓扑。需要连续表面支撑的步骤（可见性判定、法向插值、投影轮廓生成）失去依据。同族完整性三码：9500 无点、本码无面、9502 无法向量。</para>
	///   <para><b>何时遇到</b>纯点云文件被误当表面模型加载，或建模流程在点云阶段就中止未做网格化 [待实测]。本库已删除 3D 表面匹配能力，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	///   <para><b>处置</b>在建模端完成网格化/三角化后重新导出；重试原模型无效。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_SFM_NO_FACES = 9501;

	/// <summary>3D 物体模型缺少法向量数据（取值 9502）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Object model has no normals"：模型点没有配套法向量。表面匹配靠法向量判断面元朝向（对镜头可见/背对）并做姿态评分，缺法向量则该步无从计算。同族完整性三码：9500 无点、9501 无面、本码无法向量。</para>
	///   <para><b>何时遇到</b>从不含 normals 属性的格式（如纯点云）直接导入当模型用 [待实测]。本库已删除 3D 物体/表面模型能力，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	///   <para><b>处置</b>在模型制备阶段补算/重导出带法向量的模型；属数据完整性问题，与运行时参数无关。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_SFM_NO_NORMALS = 9502;

	/// <summary>3D 表面模型未经过可见性训练，无法计算基于视角的匹配得分。</summary>
	/// <remarks>
	///   <para><b>含义</b>对应原句 "3D surface model not trained for calculating view-based score"。可见性数据在训练期预计算每个点在各视角下的可见程度；缺了它，基于视角的评分无从谈起。姊妹码 9504（未训练 3D 边缘支持）。</para>
	///   <para><b>触发场景</b>用只做了几何导入、跳过可见性计算步骤的简化模型去启用视角评分匹配；本库已删除 3D 表面匹配能力（无 JlSurfaceModel 等类），本码仅作为原生错误表保留项，C# 源码无引用。</para>
	///   <para><b>推荐处置</b>重训模型并包含可见性步骤；这是"训练配置不全"类错误，不是数据损坏，与 9506-9508（文件/版本/反序列化）分属不同层面。</para>
	/// </remarks>
	public const int Jl_ERR_SFM_NO_VISIBILITY = 9503;

	/// <summary>3D 表面模型未经过 3D 边缘训练，无法启用基于边缘的匹配（取值 9504）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "3D surface model not trained for edge-supported matching"：3D 边缘是训练期预计算的辅助数据，模型里没有它却要求边缘匹配模式，直接被拒。姊妹码 9503（未训练可见性），同属"训练配置不全"族，与 9500-9502（点/面/法向量缺失）的"数据层缺失"不同层面。</para>
	///   <para><b>何时遇到</b>只做几何导入、跳过完整训练流程的精简模型是典型场景 [待实测]。本库已删除 3D 表面匹配能力（无 JlSurfaceModel 一类类），全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	///   <para><b>处置</b>重训模型并包含 3D 边缘，或改用不依赖边缘的匹配模式回避该前提。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_SFM_NO_3D_EDGES = 9504;

	/// <summary>读取 3D 表面模型时文件格式非法（取值 9506）；常量名写 NO_SFM_FILE，原生文本却是 "Invalid file format for 3D surface model"。</summary>
	/// <remarks>
	///   <para><b>含义</b>以内嵌文本为准：表面模型文件内容解析不成合法格式（拿错文件、内容损坏或截断）。常量名容易误导成"文件不存在"——那是文件系统层的错，不经原生错误码；本码管"读得开但认不出"。</para>
	///   <para><b>坑</b>SFM 族缺 9505（9504 之后直接 9506），排障时不必寻找一个不存在的码；再用 <c>JlNativeApi.GetErrorMessage</c> 的文本区分是格式（本码）、版本（9507）还是序列化项类型（9508）问题。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 表面匹配能力，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；重新导出完整文件。</para>
	/// </remarks>
	public const int Jl_ERR_SFM_NO_SFM_FILE = 9506;

	/// <summary>3D 表面模型文件的版本超出当前运行时支持（取值 9507）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "The version of the 3D surface model is not supported"：文件头版本号能读出但过新或过旧，运行时拒载。姊妹码 9506（格式根本不认）、9508（反序列化项不是模型）：那两个是"坏/错"，本码是"认得但不支持"。</para>
	///   <para><b>何时遇到</b>较新版本工具保存的模型文件放到较旧运行时加载最常见 [待实测]。本库已删除 3D 表面匹配能力，全库 Grep 无本常量引用，仅作原生错误表保留项。</para>
	///   <para><b>处置</b>升级运行时或改用同版本平台重新导出文件，重试原文件必然复发。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_SFM_WRONG_FILE_VERSION = 9507;

	/// <summary>反序列化输入项里不含有效的 3D 表面模型（取值 9508）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Serialized item does not contain a valid 3D surface model"：交给表面模型反序列化的字节项根本不是表面模型的序列化产物（可能是矩阵、图像或其他模型的产物），或缓冲已损坏。与 9506（文件格式不认）、9507（版本不支持）分属"类型不对/文件不对/版本不对"三个层面。</para>
	///   <para><b>坑</b>NOSITEM 同名码散在多族：矩阵 9222、可变形模型 9468、本码 9508——各类型序列化缓冲外观相似但带类型标记，拿错族段就会查错处置方向；排障先按数值定位族。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 表面匹配能力，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；遇之核对缓冲来源，重试无效。</para>
	/// </remarks>
	public const int Jl_ERR_SFM_NOSITEM = 9508;

	/// <summary>3D 表面/物体模型匹配产生的对称等价位姿过多（取值 9509）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Poses generate too many symmetries"：模型对称性强（如球、圆柱这类微转不改变外观的形状），枚举出的候选位姿数量超过原生层允许的上限，整个结果被拒而不返回截断后的列表。</para>
	///   <para><b>何时遇到</b>本库已删除 3D 表面匹配/物体模型能力（无对应包装类），全库 Grep 无本常量引用，本码仅作为原生错误表保留项；若日志真出现，来自原生核内部路径 [待实测]。</para>
	///   <para><b>处置</b>≥1000 属真错误（<c>JlNativeApi.IsError</c> 判 true、<c>JlNativeApi.IsFailure</c> 亦 true），统一检查经 <c>JlNativeApi.GetErrorMessage</c> 附文本抛 <c>JlOperatorException</c>。方向是削减对称声明或收紧位姿去重阈值 [待实测]，本库未暴露可调的包装入口。</para>
	/// </remarks>
	public const int Jl_ERR_SFM_TOO_MANY_SYMMS = 9509;

	/// <summary>3D 目标模型文件内容非法，解析器认不下来（取值 9510）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid 3D file"：OM3D 文件族（9510–9546）的首码。文件能打开、类型也认得，但内容解析不成合法的 3D 模型——拿错文件、导出中断、传输截断都在这里挡下。它是"文件三兄弟"的中间档：9512 是类型不认（没进解析器）、本码是进了解析器被拒、9513 是版本不支持。</para>
	///   <para><b>坑</b>SFM 族的 9506 文本几乎同句（那句专管表面模型文件），按异常文本检索会两族互串，判族必须看数值；文件系统层的"文件不存在"不走本码，报到这里说明字节已到手。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查经 <c>JlNativeApi.GetErrorMessage</c> 附文本抛 <c>JlOperatorException</c>；重新导出完整文件，原样重试必然复发。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_INVALID_FILE = 9510;

	/// <summary>传入的对象不是一个可用的 3D 目标模型（取值 9511）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Invalid 3D Object Model"：问题出在内存里的模型对象本身而不是文件——构造/读入失败后残留的空句柄、被释放或类型挂错的引用继续往下传，运行时一验对象就报本码。与 9510 的分界：9510 发生在读文件阶段，本码发生在拿对象干活阶段。</para>
	///   <para><b>坑</b>本码是 OM3D 族里少数与文件无关的码，拿"重存一遍文件"的直觉去排障会白忙；常见诱因是上一步操作已抛错、输出模型没检查就被当有效输入接着用——错误链里的第二张多米诺。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；回溯到该模型的最初产出步确认其成功生成。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_INVALID_MODEL = 9511;

	/// <summary>按给定文件类型找不到能读它的 3D 文件解析器（取值 9512）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Unknown 3D file type"：文件的类型标识（扩展名或显式传入的类型参数）没有对应的读取器，压根没进解析阶段。与 9510 的分界在此：9510 是"认得出是哪类文件、打开后内容不合法"，本码是"连门都没进"；9513 又再晚一层——认得出也进得去、版本不支持。</para>
	///   <para><b>坑</b>本库原生层支持的 3D 文件格式清单（如常见的网格/点云格式哪些可读）无现有文档支撑 [待实测]；改名丢扩展名、大小写或传错类型字符串都报此码，先确认文件本身没拿错。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；换成受支持的格式或显式指定正确类型再读。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_UNKNOWN_FILE_TYPE = 9512;

	/// <summary>3D 目标模型文件的版本超出当前运行时支持（取值 9513）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "The version of the 3D object model is not supported"：文件头能被解析、版本字段能读出，但值在当前运行时的支持范围外，拒载。文件读取三码的最后一层：9510 是"内容读不出名堂"，9512 是"类型压根不认"，本码是"认得但伺候不了"。</para>
	///   <para><b>坑</b>与 SFM 族 9507 文本几乎同句（那句管表面模型，本句管目标模型），按异常文本搜索会互相命中，判族必须看数值。较新平台导出的文件配较旧运行时最常见 [待实测]；重读原文件必然复发。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；升级运行时或用同版本平台重新导出。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_WRONG_FILE_VERSION = 9513;

	/// <summary>模型缺操作必需的属性，但错误文本说不出缺的是哪一个（取值 9514）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Required attribute is missing"：属性缺失族的总兜底码。原生层识别出"必需属性不在"却没把它归到具名专码（point_coord→9515、point_normal→9516、face_triangle→9517、line_array→9518、f_trineighb→9519、face_polygon→9520、xyz_mapping→9521、o_primitive→9522、shape_model→9523）就落在这里。</para>
	///   <para><b>坑</b>本码的原文同样不带属性名，光看异常信息是瞎子：得从"调用的是哪个算子、它要什么属性"反推 [待实测]；扩展属性方向的兜底另有 9524，两码分工不同别互串。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；对照算子的属性前提补齐再试。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB = 9514;

	/// <summary>模型连 point_coord（点坐标）这个最底层属性都没有（取值 9515）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Required attribute point_coord is missing"：point_coord 是 OM3D 唯一不可缺的属性——没有 xyz 坐标就没有几何。报本码意味着手里的"模型"是个空壳：只挂了面索引或其他属性却没有点，或点属性在加工中被清空。</para>
	///   <para><b>坑</b>具名属性缺失码 9515–9523 里本码级别最低也最致命：其他属性缺了换条操作路径还能活，点缺了整个模型不可用，别指望跳过它做后续加工。反面配对码：写入侧约束看 9531/9533。</para>
	///   <para><b>何时遇到与处置</b>多为上一步算子的输出根本没接对（把别的对象当模型传进来）[待实测]。本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_V_COORD = 9515;

	/// <summary>模型缺 point_normal（逐点法向），依赖朝向信息的操作被拒（取值 9516）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Required attribute point_normal is missing"：法向是逐点的三维单位向量属性，常量名 V_NORMALS 的 V 即 vertex（点级）。曲面匹配、光照相关、按朝向筛选等操作没它不转；传感器原始点云通常不带法向，只有显式做过法向估计的模型才有。</para>
	///   <para><b>坑</b>法向的"朝向"有双解（一面两个法向都算对），重估一次整体翻转不会报本码但会污染下游结果 [待实测]；本码只管"有没有"，不管"指哪边"。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；先跑法向估计步骤。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_V_NORMALS = 9516;

	/// <summary>模型缺 face_triangle（三角面索引），面级操作被拒（取值 9517）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Required attribute face_triangle is missing"：face_triangle 是定长的三角形顶点索引表（每三角形 3 个索引）。点云只有 point_coord 时它不存在——三角网不是随点自动有的，必须显式跑一次三角化。曲面匹配类下游（9550）同样吃这个属性。</para>
	///   <para><b>坑</b>反面码 9527 说明"从零建网"的操作又要求它不存在：建过网的模型直接重跑三角化会撞 9527，两条码合起来才是完整规则——换网要先拆再建。法向在不在（9516）不影响本码，别一起查。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_F_TRIANGLES = 9517;

	/// <summary>模型缺 line_array（折线）属性，依赖线结构的操作被拒（取值 9518）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Required attribute line_array is missing"：OM3D 里三维折线存放在 line_array 属性（常量名却叫 F_LINES，原生文本又称 polylines，见 9537——同一概念三种叫法，检索时都试）。提取边缘线、按线测量的操作要求模型先有线。</para>
	///   <para><b>坑</b>线结构通常由提取算子事后生成 [待实测]；反面码 9528 说明另一批操作恰恰要求"没有线"才能写入，两族操作对同一属性的期望相反，模型加工顺序不能随意颠倒。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；先执行生成线结构的步骤。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_F_LINES = 9518;

	/// <summary>模型缺 f_trineighb（三角形邻接关系），跨面传播类操作被拒（取值 9519）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Required attribute f_trineighb is missing"：f_trineighb 记录每个三角形与相邻三角形共边的对应关系，名字直译"三角邻接"。区域生长、沿面平滑、连通性分析这类要"从一个面走到邻居"的操作全靠它；只有 face_triangle 没有邻接表时会报本码。</para>
	///   <para><b>坑</b>有面没邻接是合法状态：手工拼的面索引、部分文件导入的模型可能三角网在但拓扑没建 [待实测]。删面/加面后邻接表必须重建，旧表不报错但会静默指错邻居，比缺表更危险。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；重跑建邻接的前置步骤即可 [入口待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_F_TRINEIGB = 9519;

	/// <summary>模型缺 face_polygon 属性，依赖多边形面的操作被拒（取值 9520）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Required attribute face_polygon is missing"：face_polygon 是面索引的变长结构（每条记录先给该面顶点数、再跟顶点索引，故与定长的 face_triangle 分属两个属性 [结构细节按面级通用约定推断，待实测]）。按多边形面处理的操作（非三角面渲染/分析）要求模型带它。</para>
	///   <para><b>坑</b>"有三角面"不等于"有多边形面"：本族把两种面结构分开计（9517/9520 两个码、9527/9529 两个反面码），三角化的模型照样可能报缺多边形；反之亦然。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；先执行生成面结构的前置算子 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_F_POLYGONS = 9520;

	/// <summary>模型缺 xyz_mapping（3D 点与 2D 像素的映射），影像类操作被拒（取值 9521）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Required attribute xyz_mapping is missing"：xyz_mapping 记录每个 3D 点对应到源图像的行列位置，常量名里的 V_2DMAP 即此物。纹理贴图、把图像强度回填到点、按像素反查三维坐标，全依赖这一属性；只从深度数据独立重建的模型不带它。</para>
	///   <para><b>坑</b>它只能来自"模型与图像同源"的路径（相机式重建、随文件一同读入）[待实测]；模型做过裁剪/降采样后映射要随之重建，旧映射配新点集会从"缺属性"变成 9538 的"数量不匹配"。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_V_2DMAP = 9521;

	/// <summary>模型缺 o_primitive 属性，基元级操作无从下手（取值 9522）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Required attribute o_primitive is missing"：o_primitive 存的是模型对几何基元（平面、圆柱、球等）的拟合描述，依赖基元语义的操作（按基元选择、测量、简化）要求先做基元拟合才有这个属性；原始扫描/文件模型默认不带。</para>
	///   <para><b>坑</b>与 9526 分两层：本码是"属性整个没有"（没拟合过），9526 是"属性有了但扩展数据空"（拟合过、做得不全）。先分清缺哪个再补作业。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；补跑基元拟合步骤 [入口待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_O_PRIMITIVE = 9522;

	/// <summary>模型缺 shape_model 属性，需要形状信息的操作无法执行（取值 9523）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Required attribute shape_model is missing"：shape_model 是在原始点/面之上另行计算并挂载的派生属性（供形状类操作消费的模型描述），不是读文件自动就有的；裸模型直接送进依赖它的操作即报本码。具名属性缺失专码（9515–9523）的最后一个。</para>
	///   <para><b>坑</b>派生属性经不起"再加工"：模型被裁剪、简化或变换后，旧的 shape_model 要么失效要么随属性被剥离，重新生成一步不能省 [待实测]。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型与形状匹配包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；先跑生成该属性的预处理再重试。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_SHAPE_MODEL = 9523;

	/// <summary>3D 目标模型缺必需的扩展属性，且原生层说不出缺的是哪一个（取值 9524）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Required extended attribute missing in 3D object model"：操作依赖某个扩展属性（点/面基础属性之外、按算子追加的那批），模型没带。与 9514 同为泛化兜底码，本码专指扩展属性；具名缺失各有专码（9515–9523）。</para>
	///   <para><b>坑</b>文本不带属性名是本码最大的信息损失——只知道"扩展属性缺"，不知道缺哪个；此时从调用它的算子反推其属性前提（例如做过一次简化/滤波的模型常丢下游属性）[待实测]。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；用原始模型重走一遍属性生成链通常比补单个属性可靠。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_MISSING_ATTRIB_EXTENDED = 9524;

	/// <summary>反序列化输入项里不含有效的 3D 目标模型（取值 9525）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Serialized item does not contain a valid 3D object model"：交给 3D 目标模型反序列化的字节项确实是序列化项、但装的不是 OM3D（可能是矩阵、表面模型或其他对象的产物），或缓冲损坏。与 9510（文件内容坏）、9513（版本不支持）分属"类型不对/文件不对/版本不对"三层。</para>
	///   <para><b>坑</b>NOSITEM 同名码散在多族：矩阵 9222、可变形模型 9468、表面模型 9508、本码 9525——序列化缓冲外观相似但带类型标记，拿错族段就会查错处置方向；排障先按数值定位族，再核对缓冲的产出方是不是同族算子。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；重试无效，核对数据来源才是出路。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_NOSITEM = 9525;

	/// <summary>模型里的几何基元（primitive）没带扩展数据，依赖它的操作失败（取值 9526）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Primitive in 3D object model has no extended data"：o_primitive（见 9522）属性存在、模型确实带基元描述，但某个操作要用的那部分扩展数据（基元的细化信息）是空的——比 9522 更进一步：那不是"没有基元"，而是"有基元但基元不够用"。</para>
	///   <para><b>坑</b>常见于基元拟合时只存了类型/位姿没存完整参数，或模型经转换/简化后扩展数据被丢 [待实测]。排障顺序：先确认 9522 不报（属性在），再怀疑扩展数据缺（本码）。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；重新做一遍带扩展输出的基元拟合是方向 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_MISSING_O_PRIMITIVE_EXTENSION = 9526;

	/// <summary>操作要求模型不带三角网，但模型已含 face_triangle，操作被拒（取值 9527）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Operation invalid, 3D object model already contains triangles"：目标操作（典型是重新三角化一类"从零建网"的步骤）只在无面模型上合法；模型已有 face_triangle 时再建一次会双重定义拓扑，原生层直接拒，不会静默替换旧网。9517（MISSING_ATTRIB_F_TRIANGLES）的反面码：一个"要面没面"，一个"不该有面"。</para>
	///   <para><b>坑</b>"想换一张网"的正确姿势不是重跑建网操作，而是先删旧面属性再建（入口 [待实测]）；直接重试原操作永远撞同一码。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_CONTAIN_ATTRIB_F_TRIANGLES = 9527;

	/// <summary>操作要求模型不带折线信息，但模型已含，操作被拒（取值 9528）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Operation invalid, 3D object model already contains lines"：目标操作要在模型里生成折线/线结构（line_array，见 9518），而模型已经带着一套；再生成等于双重定义，原生层拒掉而不是覆盖旧的。9518 的反面码：那边"要线没线"，这边"不该有线却有线"。</para>
	///   <para><b>坑</b>同族 CONTAIN 码按占位的结构类型分档（9527 三角/9528 线/9529 面），先分清操作要重建的是哪种结构再删对应属性，删错档等于白删。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；清除已有线属性后重跑，或换用支持追加的结构操作 [待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_CONTAIN_ATTRIB_F_LINES = 9528;

	/// <summary>操作要求模型不带面/多边形信息，但模型已含，操作被拒（取值 9529）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Operation invalid, 3D object model already contains faces or polygons"：某些操作（如由点集重新构建面结构）只在"无面"前提下有意义，模型已带 face_polygon 或面信息时再执行会产生双重定义，原生层选择报错而不是悄悄覆盖。它是 9520（MISSING_ATTRIB_F_POLYGONS）的反面码：那码是"要面却没面"，本码是"不该有面却有了面"。</para>
	///   <para><b>坑</b>CONTAIN 三兄弟（9527 三角、9528 线、9529 面/多边形）合并进一条文本的只有本码——faces or polygons 两种都算占位，删干净才能继续。判断不清就检查"与 MISSING 码同名属性"是否存在。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；先删除已有面属性再执行 [删除属性入口待实测]。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_CONTAIN_ATTRIB_F_POLYGONS = 9529;

	/// <summary>全局配准的输入里存在与任何其他片段都无邻接关系的孤立模型（取值 9530）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "In a global registration an input object has no neighbors"：多片段全局配准靠的是片段两两之间的重叠对应关系推整体位姿；某个输入模型与其他任何模型都找不到邻居（重叠不足或初始位姿差太远没对上），它的变换无解，整个配准被拒而不是跳过它配其余。</para>
	///   <para><b>坑</b>整批失败是本码最伤的地方：九个片段里一个孤立，九个结果全拿不到。补拍让它产生重叠、给它更准的初始位姿、或把它剔出本批单独配，三条路都比原样重试有效；"邻居"的判定距离/重叠度参数 [待实测]。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_ISOLATED_OBJECT = 9530;

	/// <summary>点坐标必须三个分量一次性写全，不允许只给其中一部分（取值 9531）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "All components of points must be set at once"：point_coord 的 x/y/z 是不可拆的整体，单独更新某一轴（比如只做高度平移只给 z）会被拒——原生层保证模型里不存在"缺轴的点"。</para>
	///   <para><b>坑</b>做单轴变换的正确姿势是整点集重算后全量回写，或直接改用模型级刚体变换属性，而不是试图逐分量打补丁。与 9532（法向同规则）对仗；数量对不上归 9533 管，本码只管分量齐不齐。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；组装完整三元组后一次写入。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_SET_ALL_COORD = 9531;

	/// <summary>法向必须三个分量一次性写全，不允许只给其中一部分（取值 9532）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "All components of normals must be set at once"：point_normal 的 nx/ny/nz 是一个不可拆的整体，试图分开写（比如只更新 nx）被直接拒绝——原生层不允许模型存在"半个法向"的中间态。</para>
	///   <para><b>坑</b>这是设计约束不是数据错误：想"只改一个分量"也必须把完整的三元组重新提交。与 9531（点坐标同规则）对仗；数量维度由 9534 管，本码只管"分量齐不齐"，两个码报错含义不同别混查。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；组装好全部分量再一次写入。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_SET_ALL_NORMALS = 9532;

	/// <summary>新值的个数与模型已有顶点的数量不匹配（取值 9533）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Number of values doesn't correspond to number of already existing points"：向已含 point_coord（见 9515）的模型写逐点数据时，新值数量必须以现存点数为基准；每个点是三维坐标，按"点数"还是按"3×点数"校验原文未写明 [待实测]，数值差 3 倍先查口径。</para>
	///   <para><b>坑</b>NUM_NOT_FIT 系列（9533–9539）的共同病灶是"基准过期"：点数被裁剪/降采样改变后，所有按旧点数准备的数组都在各自粒度上撞墙——本码是点粒度，9534 是法向，9535/9536 是面，逐级往上查能快速定位是哪层数据没跟着重算。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；与 9531 配对排查（一个管分量没给全，一个管数量没对上）。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_COORD = 9533;

	/// <summary>新值的个数与模型已有法向的数量不匹配（取值 9534）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Number of values doesn't correspond to number of already existing normals"：改 point_normal（见 9516）时，值的总量必须与现存法向数量一致；法向是三维向量，"个数"按向量计还是按 x/y/z 分量计原文未言明 [待实测]，差 3 倍时优先怀疑口径。</para>
	///   <para><b>坑</b>法向必须逐点对应：点集裁剪、滤波、重法向估计之后法向数量随之改变，拿处理前算好的法向数组回填就会撞本码。与 9532 分工：9532 管"分量没一次给全"，本码管"整体数量对不上"。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；以当前点集为基准重算法向再写。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_NORMALS = 9534;

	/// <summary>新值的个数与模型已有三角网的结构规模不匹配（取值 9535）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Number of values doesn't correspond to already existing triangulation"：face_triangle 是定长结构——每个三角形固定占 3 个顶点索引（索引从 0 起算），所以与它配对的量要么恒为 3 的倍数、要么恰为三角形个数，任何一侧对不上都报本码 [两种口径具体哪个适用待实测]。</para>
	///   <para><b>坑</b>三角网被重跑过一次（重新三角化、删面、细分）后三角形数就变了，旧规模的邻接表、面属性数组会整体失配；本码常是"忘了重算下游数组"的第一个信号，紧随其后往往还有 9519/9539。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；以当前三角网为准重建配套数据。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_TRIANGLES = 9535;

	/// <summary>新值的个数与已有多边形面的总长度不匹配（取值 9536）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Number of values doesn't correspond to length of already existing polygons"：face_polygon（见 9520）同样是变长结构——每个面边数可以不同，所以校验口径是所有面的顶点索引总数；给面级属性赋值时值数必须与这个总长一致 [条数/总长口径按名称推断，待实测]。</para>
	///   <para><b>坑</b>与 9535（三角网口径）的区别就在"是否定长"：三角网天然每个面 3 顶点、索引数恒为 3 的倍数；多边形面数不定，长度基准只能从模型读回，凭记忆硬算最容易差一差二。删一个面、改一次细分都会移动基准。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；先查模型当前面结构规模再备值。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_POLYGONS = 9536;

	/// <summary>新值的个数与已有折线（polyline）的总长度不匹配（取值 9537）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Number of values doesn't correspond to length of already existing polylines"：折线类属性（存储走 line_array，见 9518）是变长结构，本码核对的是所有折线的顶点总数而不是折线条数——每条折线长度可以互不相同，改值时必须按"总长"对齐 [按名称与原文推断，条数/总长口径待实测]。</para>
	///   <para><b>坑</b>常量名叫 LINES、原生文本说 polylines，同一族里两种叫法混用（9518 的属性名又是 line_array），检索时三个词都得试。合并/拆分折线会改变总长基准，之后按旧总长给值必然撞本码。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；先读回当前折线结构规模再准备值。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_LINES = 9537;

	/// <summary>新值的个数与模型已有的 2D 映射（xyz_mapping）规模对不上（取值 9538）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Number of values doesn't correspond already existing 2D mapping"：2D 映射把 3D 点与图像像素行列对应起来，改它时新值的个数必须与现存映射结构一致（映射按点计，每个点携带的像素坐标分量数固定 [待实测]）。原文缺了 "to"（correspond already），那是原生自带的小笔误，按 "correspond to" 理解即可。</para>
	///   <para><b>坑</b>NUM_NOT_FIT 系列（9533–9539）里本码最隐蔽：图像换了分辨率、或点集裁剪过，映射的"点数基准"变了而代码还按旧图准备值；报数字不匹配，实际是两侧不同源。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；重建映射时务必用当前点集与当前图像同批生成。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_2DMAP = 9538;

	/// <summary>新值的个数与模型中已有扩展属性的元素数不匹配（取值 9539）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Number of values doesn't correspond to already existing extended attribute"：NUM_NOT_FIT 系列（9533–9539）的收尾码，管点/法向/三角/多边形/折线/2D 映射之外的扩展属性——改这类属性时值数量必须与模型现有元素数逐一对齐，多一个少一个都不行。</para>
	///   <para><b>坑</b>该系列整体有个共同陷阱：报错说"数量不对"，对的是"已有"结构的大小——点集刚被裁剪或滤波过，手里还按旧长度准备值，就会在这里撞墙。先查最近一次改变点数的操作，再查长度本身。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；重新按当前模型规模取数再赋值。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_NUM_NOT_FIT_EXTENDED = 9539;

	/// <summary>对只有点级数据的模型使用了"每面强度"这种面级属性（取值 9540）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Per-face intensity is used with point attribute"：按面存强度（每个三角形/多边形一个灰度）的前提是模型真的有面；纯点云只有点没有面，面级强度无处挂靠，属性粒度与数据层级配错了。</para>
	///   <para><b>坑</b>OM3D 属性分点级（point_coord、point_normal、强度按点）与面级（face_triangle、面强度）两套粒度，赋值时给错的不是值而是"挂靠层"。改法二选一：先三角化再用面级强度，或把强度降到点级。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；这是调用方的属性组织错误，重试无意义。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_FACE_INTENSITY_WITH_POINTS = 9540;

	/// <summary>请求读写的模型属性名当前版本的运行时不认（取值 9541）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Attribute is not (yet) supported"：属性名落在 OM3D 的属性语法里，但当前运行时没有实现它的读写。注意原文里的 "(yet)"——原生层自述这是"暂时未支持"，同一属性名在别的版本可能就能用，支持集随版本漂移 [待实测]。</para>
	///   <para><b>坑</b>与本族"属性缺失"码（9514–9523）区分开：那些是"属性体系存在、这个模型没带该属性"，本码是"属性体系本身没实现"。前者换模型能好，后者换模型也没用，得换运行时版本。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；先排除属性名拼写，再确认运行时版本。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_ATTRIBUTE_NOT_SUPPORTED = 9541;

	/// <summary>按包围盒筛选/裁剪后盒内一个 3D 点都没有（取值 9542）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "No point within bounding box"：给定的三维包围盒与模型的点集完全不相交，裁剪或按盒提取的结果是空集，原生层按真错误抛出而不是返回空模型。</para>
	///   <para><b>坑</b>包围盒用的是模型局部坐标系——模型若被刚体变换移动过，旧盒坐标立刻失效，这是"明明框过怎么现在框不住"的最常见来源 [待实测]；另外三维盒常以最小/最大角点两对角点表达，行列混淆会把盒旋到别处。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；处置方向是先读回模型当前的实际范围再定盒，而不是拿设计态坐标硬框。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_NOT_IN_BB = 9542;

	/// <summary>distance_in_front 设得比分辨率还小，参数组合被拒（取值 9543）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "distance_in_front is smaller than the resolution"：distance_in_front 是沿表面法向向前留出的余量距离（原生参数名直译"前方距离"，精确用途 [待实测]）；它小于采样分辨率时，这层余量在网格上连一个像素都占不到，等于白设，原生层拒绝这组参数。</para>
	///   <para><b>坑</b>常量名是 DIF_ 前缀、不带 OM3D 字样，但它物理位于 OM3D 族段内（9543，夹在 9542 与 9544 之间），按族查码时别当成漏写前缀的孤儿码；与 9544 同为"生成参数低于分辨率下限"一族。原生文本用的是下划线参数名，检索时两种写法都要试。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D/曲面匹配包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；把该距离抬到分辨率之上即可恢复。</para>
	/// </remarks>
	public const int Jl_ERR_DIF_TOO_SMALL = 9543;

	/// <summary>设定的最小厚度低于曲面容差，参数组合自相矛盾被拒（取值 9544）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "The minimum thickness is smaller than the surface tolerance"：最小厚度用于把薄于该值的结构从模型中剔除，而曲面容差是判定"同面/异面"的分辨率下限；最小厚度设得比容差还小，被剔除判定的层本身已在容差噪声之内，参数失去意义，原生层直接拒收。</para>
	///   <para><b>坑</b>两参同向调才收敛：要处理更薄的特征就同时下调容差，而不是一味压低最小厚度。与 9543（distance_in_front 低于分辨率）同属"生成参数低于分辨率下限"一族，常量名不带 OM3D 前缀，按数值 9543/9544 定位。</para>
	///   <para><b>何时遇到与处置</b>出现在由 3D 目标模型生成曲面匹配模型的参数校验阶段 [待实测]。本库已删除 3D/曲面匹配包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；把最小厚度抬到容差之上即可恢复。</para>
	/// </remarks>
	public const int Jl_ERR_MINTH_TOO_SMALL = 9544;

	/// <summary>输入图像的宽×高与 3D 模型的点数不相等（取值 9545）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Input width or height does not match the number of points in 3D object model"：把逐像素的 2D 数据（强度图、纹理）绑到按栅格组织的模型上时，约定是一像素对一点，因此要求 width*height 与模型点数严格相等——是"恰好相等"，不是"覆盖得住"就行。</para>
	///   <para><b>坑</b>图像裁剪、缩放或模型做过一次滤波/降采样后点数就变了，两边任一被动过都会打破等式；报的是尺寸码，病灶常在"最近一次点集处理"。姊妹码 9546 是宽高压根没设，两者先后来查。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；处置是回到同源重建：图像与模型必须出自同一次未再处理的采集。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_WRONG_DIMENSION = 9545;

	/// <summary>模型缺少图像宽高信息，涉及 2D 影像的 3D 模型操作无法进行（取值 9546）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Image width or height must be set"：按图像栅格组织的 3D 模型（深度图反投影、带 2D 映射的模型）必须同时携带图像宽度和高度两个维度属性，缺任何一个，依赖像素坐标的解释都无从谈起。</para>
	///   <para><b>坑</b>与 9545 是一对前后脚：9545 是"宽高设了但和点数对不上"，本码是"根本没设"。先补维度再看数值匹配，顺序反了会连续踩两个码。</para>
	///   <para><b>何时遇到与处置</b>多为手工拼装模型时只塞了点集忘了维度属性 [待实测]。本库已删除 3D 目标模型包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查抛 <c>JlOperatorException</c>；用产生该模型的采集/反投影源头补上宽高后重建。</para>
	/// </remarks>
	public const int Jl_ERR_OM3D_MISSING_DIMENSION = 9546;

	/// <summary>3D 目标模型的三角网格不满足算子要求，曲面匹配类调用被拒（取值 9550）。</summary>
	/// <remarks>
	///   <para><b>含义</b>原生文本 "Triangles of the 3D object model are not suitable for this operator"：模型虽带 face_triangle，但三角形的质量或数量没达到曲面匹配算子的下限——网格太稀、退化三角形过多或构网未覆盖点集都可能触发，具体判定阈值 [待实测]。与 9551 成对：9551 是"点不够"，本码是"点够了但面不能用"。</para>
	///   <para><b>坑</b>本码与 9551 处于 OM3D 文件族（9510–9546）与 SF 表面模型族（9506–9509）的交界处：排障时先查模型的网格质量，而不是文件完整性或版本。</para>
	///   <para><b>何时遇到与处置</b>本库已删除 3D 目标模型与曲面匹配包装，全库 Grep 无本常量引用，仅作原生错误表保留项。≥1000 属真错误，统一检查经 <c>JlNativeApi.GetErrorMessage</c> 附文本抛 <c>JlOperatorException</c>；处置方向是重新构网（重跑三角化或降低简化程度），拿原模型重试必然复发。</para>
	/// </remarks>
	public const int Jl_ERR_SF_OM3D_TRIANGLES_NOT_SUITABLE = 9550;

	/// <summary>3D 目标模型中可用于曲面匹配的合格三维点太少，样本不足以完成匹配。</summary>
	/// <remarks>
	///   <para><b>含义</b>与 9550 同属"曲面匹配 × 3D 目标模型"组合族（SF_OM3D）：9550 指三角面片本身不适合，本码指点数不足——模型简化过度或重建噪声被滤除后会触发 [待实测]。</para>
	///   <para><b>触发场景</b>曲面匹配算子加载点数过少的 OM3D 模型时原生侧返回；本库现存的包装层中已无曲面匹配/3D 模型入口可触发它（相关包装类已随库删除）。</para>
	///   <para><b>推荐处置</b>在原生侧问题域内处理：提高重建采样密度或增加模型细节；应用层应视为模型资产缺陷而非运行时抖动。</para>
	/// </remarks>
	public const int Jl_ERR_SF_OM3D_FEW_POINTS = 9551;

	/// <summary>输入不是有效的序列化项文件：字节流缺少可识别的序列化头，根本未被认成库数据。</summary>
	/// <remarks>
	///   <para><b>含义</b>与 9581 构成一对：9580 是"类型就不对"（连序列化项魔数都没认出），9581 是"类型对了但被截断"。本文件 9508/9525/9468 等 *_NOSITEM 码则是"认出了是序列化项、但里面的对象类型不对"，三者诊断层次不同。</para>
	///   <para><b>触发场景</b>本库可核实的入口是 <c>JlHandle</c> 的反序列化：把非序列化数据（文本文件、其他二进制、错位读取的字节数组）交给它时抛 <c>JlException</c>，说明文本 "Input stream is no serialized Vision object"。</para>
	///   <para><b>推荐处置</b>核对文件来源与读取起点（是否从流第 0 字节读、是否混入了长度框）；这是数据身份问题，不是版本问题。</para>
	/// </remarks>
	public const int Jl_ERR_NO_SERIALIZED_ITEM = 9580;

	/// <summary>序列化项数据提前结束：反序列化读到一半流就到头了，数据被截断。</summary>
	/// <remarks>
	///   <para><b>含义</b>与 9580（压根不是序列化项）不同，本码说明确实认出了序列化头，但按头中声明的长度继续读时字节不够——典型是被截断的文件、未写完的流或复制丢包的字节数组。</para>
	///   <para><b>触发场景</b>本库可核实的入口是 <c>JlHandle</c> 的流反序列化：数据不完整时抛 <c>JlException</c>，说明文本 "Unexpected end of serialization data"。</para>
	///   <para><b>推荐处置</b>检查写出端是否完整 flush/close、传输链路上的长度是否保真；不要尝试用截断数据"尽力恢复"。</para>
	/// </remarks>
	public const int Jl_ERR_END_OF_FILE = 9581;

	/// <summary>运行时终结（finalization）时仍有用户线程在使用库资源，清理无法安全完成。</summary>
	/// <remarks>
	///   <para><b>含义</b>错误指向线程生命周期而非数据：在运行时做整体收尾/复位时检测到不止一个用户线程仍持有或调用着库资源，原生侧拒绝在这种竞态下继续释放。</para>
	///   <para><b>触发场景</b>后台工作线程未 join 就退出程序、或在最终化例程执行期间仍有线程调用算子 [待实测]。</para>
	///   <para><b>推荐处置</b>关闭前显式停止并等待所有使用库对象的线程结束，再触发终结；把它当作资源泄漏/关闭顺序缺陷的信号，而不是可重试的瞬时错误。</para>
	/// </remarks>
	public const int Jl_ERR_FINI_USR_THREADS = 9700;

	/// <summary>数据不是合法的加密序列化项格式：读取端期望密文，拿到的却是不带加密结构的字节。</summary>
	/// <remarks>
	///   <para><b>含义</b>常量名 NO_ENCRYPTED_ITEM 与英文原句同义——"加密项文件格式无效"。与 9580（普通序列化项格式无效）的区别在于本码专门发生在按加密格式解析的入口，说明给错文件或走了错解析路径。</para>
	///   <para><b>触发场景</b>把明文序列化文件、被截断的密文或完全不同的文件喂给按加密项读取的过程 [待实测]。</para>
	///   <para><b>推荐处置</b>先确认文件确实是加密导出的产物；若源数据本就未加密，应改走普通反序列化路径而非解密路径。</para>
	/// </remarks>
	public const int Jl_ERR_NO_ENCRYPTED_ITEM = 9800;

	/// <summary>口令错误：解密受密码保护的序列化项时提供的密码与加密时使用的不一致。</summary>
	/// <remarks>
	///   <para><b>含义</b>这是"密码这一输入不对"的专用诊断码，与 9803（口令对但解密过程失败）不同：本码意味着凭据校验未通过，数据未被尝试解出。</para>
	///   <para><b>触发场景</b>读取带密码的序列化数据时传入了错误、过期或被改动的口令 [待实测]。</para>
	///   <para><b>推荐处置</b>核对口令来源（大小写、编码、是否含不可见字符）；不要将其当作数据损坏处理，两者补救路径不同。</para>
	/// </remarks>
	public const int Jl_ERR_WRONG_PASSWORD = 9801;

	/// <summary>加密失败：把序列化项加密导出时加密过程未能完成，数据未写出。</summary>
	/// <remarks>
	///   <para><b>含义</b>9800～9803 是加密序列化项一族的四个码：9800=文件根本不是合法加密项、9801=口令不符、9802=本码（加密侧失败）、9803=解密侧失败。看到本码说明失败发生在"写/加密"方向而非"读/解密"方向。</para>
	///   <para><b>触发场景</b>加密参数非法、目标数据不可序列化或原生加密例程内部出错 [待实测]；本库源码中未发现直接暴露加密入口的包装算子。</para>
	///   <para><b>推荐处置</b>确认待加密数据是可整体序列化的句柄数据后重试；持续失败则按环境/版本问题上报。</para>
	/// </remarks>
	public const int Jl_ERR_ENCRYPT_FAILED = 9802;

	/// <summary>解密失败：对加密序列化项做解密时未能还原出明文数据。</summary>
	/// <remarks>
	///   <para><b>含义</b>与 9801（口令错误）相邻但不同：口令错误专指密码比对不符，本码泛指解密过程本身失败——数据被篡改、密文格式不完整或加解密参数不匹配都会落到这里。</para>
	///   <para><b>触发场景</b>读取用加密方式写出的序列化项（如句柄序列化数据）且口令正确但密文已损坏时 [待实测]；本库源码中未发现直接暴露解密入口的包装算子。</para>
	///   <para><b>推荐处置</b>把密文当作不可信输入处理：校验来源与完整性，必要时用原始数据重新加密导出。</para>
	/// </remarks>
	public const int Jl_ERR_DECRYPT_FAILED = 9803;

	/// <summary>用户自定义错误区的起点：应用自定义的错误码必须大于本值（10000），以免与库内保留码冲突。</summary>
	/// <remarks>
	///   <para><b>含义</b>0～10000 为运行时与原生算子保留的错误码段（本文件中出现过的最大保留码是 9803）；抛自定义 <c>JlException</c> 时选用大于 10000 的码值才不会与库码撞号。</para>
	///   <para><b>相邻边界</b>注意本库 <c>JlException</c> 判定"用户自定义、可携带附加数据"的门槛是 30000 及以上（&lt;30000 的码在 ToTuple 时不拼接用户数据），即 10000～29999 之间属于"不撞库码但也不带自定义负载"的中间区 [待实测]。</para>
	///   <para><b>推荐处置</b>作为常量用于校验自定码取值，它不是任何算子的返回值。</para>
	/// </remarks>
	public const int Jl_ERR_START_EXT = 10000;

	/// <summary>运行时找不到任何许可证（未部署或未被发现），一切受许可保护的调用都无法进行。</summary>
	/// <remarks>
	///   <para><b>含义</b>本常量与前面的 <c>Jl_ERR_LIC_NO_LICENSE</c>（2003）同值、亦与 <c>Jl_ERR_LIC_RANGE1_BEGIN</c>（许可错误第一段起点）同值——三者在数值上是同一个码，本行属于文件末尾追加的一组许可别名。</para>
	///   <para><b>触发场景</b>许可搜索路径（环境变量/许可文件/许可服务器）全部落空时返回；与 2005 的区别是连许可记录都没有，与 2024（无法连接许可服务器）的区别是这里根本未涉及服务器。</para>
	///   <para><b>推荐处置</b>检查部署：许可环境变量、许可文件路径或网络许可服务是否配置；这是安装问题，重试算子不会改变结果。</para>
	/// </remarks>
	public const int Jl_ERR_NO_LICENSE = 2003;

	/// <summary>找到了许可证，但许可内没有任何可用模块（缺少 VENDOR_STRING 模块清单）。</summary>
	/// <remarks>
	///   <para><b>含义</b>与前面的 <c>Jl_ERR_LIC_NO_MODULES</c> 同值（2005），是它的别名。原英文 "No modules in license (no VENDOR_STRING)"：许可记录本身可读，但其中声明功能模块的 VENDOR_STRING 字段为空或缺失，等于"有证无货"。</para>
	///   <para><b>触发场景</b>许可文件损坏、被手工编辑掉模块段，或许可服务器下发的许可未填功能信息时，初始化后首次调用受许可保护的算子会撞上它。与 2003（完全无许可）的区别是有许可记录；与 2006（单算子未授权）的区别是全部模块都不可用。</para>
	///   <para><b>推荐处置</b>重新部署完整的许可文件或联系许可管理方重新签发；程序侧应把它作为部署错误上报而非静默重试。</para>
	/// </remarks>
	public const int Jl_ERR_NO_MODULES = 2005;

	/// <summary>许可证有效，但其中未包含当前所调用算子对应的授权功能。</summary>
	/// <remarks>
	///   <para><b>含义</b>与前面的 <c>Jl_ERR_LIC_NO_LIC_OPER</c> 同值（2006），是它的别名。区别相邻许可码：2003 是完全找不到许可，2005 是许可存在但模块清单为空，2006 则是许可与模块都在、只是这一个算子的功能未被授权。</para>
	///   <para><b>触发场景</b>调用未购买/未解锁功能包的算子时由许可检查拒绝。惯用法是在 catch 中用 <c>JlException.GetErrorCode()</c> 与本常量比较来区分"许可不足"和其他失败。</para>
	///   <para><b>推荐处置</b>核对许可功能清单（VENDOR_STRING/许可服务器上的 feature），或改用已授权功能的等价算子；不要通过反射硬闯。</para>
	/// </remarks>
	public const int Jl_ERR_NO_LIC_OPER = 2006;

	/// <summary>许可证（LIC）错误族的末界哨兵：许可族错误码的最大值，本身不表达独立错误含义。</summary>
	/// <remarks>
	///   <para><b>含义</b>值 2384 与前文 <c>Jl_ERR_LIC_NEWVER</c>（"许可证版本过新"）完全相同，是本文件中许可族枚举到的最后一个码，用作许可错误段的上边界。注意许可码在 2003～2384 之间并非连续段，中间混有 2100～2222 等非许可码，因此判断"是否许可错误"只能用本常量作上界、结合具体许可码集合，不能仅靠数值区间。</para>
	///   <para><b>触发场景</b>边界常量，正常不会作为返回值出现；数值 2384 实际对应"许可证版本比本运行时新"这一错误（见 <c>Jl_ERR_LIC_NEWVER</c>）。模板英文 "!JlERRORDEF_H" 是原生头文件收尾宏的残渣，与错误含义无关。</para>
	///   <para><b>推荐处置</b>仅在与 <c>Jl_ERR_LIC_RANGE1_BEGIN</c> 等界值配对做范围判定时引用，不要为它写单独的 case 分支。</para>
	/// </remarks>
	public const int Jl_ERR_LAST_LIC_ERROR = 2384;
}
