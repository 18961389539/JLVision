namespace JLVisionLib;

/// <summary>
///   元组类型枚举，即 <c>JlTuple.Type</c> 的返回值。
/// </summary>
public enum JlTupleType
{
	/// <summary>元组为空</summary>
	EMPTY = 31,
	/// <summary>元组由 System.Int32 数组承载</summary>
	INTEGER = 1,
	/// <summary>元组由 System.Int64 数组承载</summary>
	LONG = 129,
	/// <summary>元组由 System.Double 数组承载</summary>
	DOUBLE = 2,
	/// <summary>元组由字符串数组承载</summary>
	STRING = 4,
	/// <summary>元组由 JlHandle 句柄数组承载</summary>
	JlANDLE = 16,
	/// <summary>元组由装箱值的对象数组承载</summary>
	MIXED = 8
}
