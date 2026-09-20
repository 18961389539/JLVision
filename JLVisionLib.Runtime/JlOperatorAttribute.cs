using System;

namespace JLVisionLib;

/// <summary>标记算子（Operator）包装方法，携带其对应的原生逻辑名称。</summary>
/// <remarks>
///   <para><b>功能说明</b>：把某个托管静态方法声明为某原生 Vision 算子的包装，并在 <c>LogicalName</c> 里记录该算子在原生运行时的逻辑名称，供按名查找/反射调用时读取，避免在多处硬编码算子字符串。它是纯元数据，不改变被标注方法的运行时行为。</para>
///   <para><b>约束</b>：<c>AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)</c>——只能贴方法、不可被子类继承、单方法至多一次。<c>LogicalName</c> 是 <c>virtual</c> 只读属性（读私有字段），构造器只收一个 <c>string</c> 且不做非空/合法性校验，逻辑名拼错在托管侧不会被拦下 [待实测：原生侧是否据名报错]。</para>
///   <para><b>与相邻能力的取舍</b>想知道"当前正在执行的过程"叫什么，用 <c>JlNativeApi.GetLogicalName(proc/procIndex)</c> 的运行时查询；本特性是编译期静态标注，二者不是一回事。</para>
///   <para><b>用法</b></para>
///   <code>
///   public static class MyOperators
///   {
///       [JlOperator("my_logical_name")]
///       public static void Run() { }
///   }
///   </code>
///   <para><b>资源与坑</b>读取靠反射 <c>GetCustomAttribute</c> 后取 <c>LogicalName</c>；无句柄资源。若目标方法未贴本特性，反射拿不到逻辑名，调用方需自行判空。</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class JlOperatorAttribute : Attribute
{
	private string logicalName;

	/// <summary>该包装方法对应的原生算子逻辑名称。</summary>
	public virtual string LogicalName => logicalName;

	/// <summary>以逻辑名称构造特性实例。</summary>
	/// <param name="logicalName">原生算子的逻辑名称。</param>
	public JlOperatorAttribute(string logicalName)
	{
		this.logicalName = logicalName;
	}
}
