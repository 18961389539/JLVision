using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace JLVisionLib;

/// <summary>
///   管理与视觉核心库(JLVisionCore 原生库)之间的全部底层通信：HALCON 风格算子/元组/句柄的 P/Invoke 绑定层。
/// </summary>
[SuppressUnmanagedCodeSecurity]
public class JlNativeApi
{
	/// <summary>原生库内存分配器类型（对应 HSet/HGetMemoryAllocatorType）。</summary>
	public enum JlMemoryAllocatorType
	{
		/// <summary>无效/未指定分配器（值 -1）。</summary>
		Invalid = -1,
		/// <summary>使用系统默认内存分配器。</summary>
		System,
		/// <summary>使用 MiMalloc 高性能内存分配器。</summary>
		MiMalloc
	}

	/// <summary>进度回调委托：原生层在算子执行期间回传任务 id、算子名、进度值与状态消息。</summary>
	public delegate void JlProgressBarCallback(IntPtr id, string operatorName, double progress, string message);

	/// <summary>底层错误回调委托：原生层上报错误描述字符串。</summary>
	public delegate void JlLowLevelErrorCallback(string err);

	/// <summary>清理回调委托：原生层回传指针 ptr，用于释放与之关联的非托管资源。[待实测]</summary>
	public delegate void JlClearProcCallBack(IntPtr ptr);



	private const string NativeLib = "JLVisionCore";

	private const CallingConvention NativeCall = CallingConvention.Cdecl;

	/// <summary>在 64 位平台上运行时为 true。</summary>
	public static readonly bool isPlatform64 = IntPtr.Size > 4;

	/// <summary>在 Windows 平台上运行时为 true。</summary>
	public static readonly bool isWindows = testWindows();

	internal const int Jl_MSG_OK = 2;

	internal const int Jl_MSG_TRUE = 2;

	internal const int Jl_MSG_FALSE = 3;

	internal const int Jl_MSG_VOID = 4;

	internal const int Jl_MSG_FAIL = 5;

	static JlNativeApi()
	{
		if (!isWindows)
		{
			return;
		}
		string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NativeLib + ".dll");
		if (File.Exists(path))
		{
			LoadLibrary(path);
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadLibrary(string lpFileName);

	private JlNativeApi()
	{
	}

	private static bool testWindows()
	{
		int platform = (int)Environment.OSVersion.Platform;
		if (platform != 4)
		{
			return platform != 128;
		}
		return false;
	}

	/// <summary>原生 HLIDoLicenseError：向视觉核心设置许可证(license)错误的处理状态。[待实测]</summary>
	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIDoLicenseError")]
	public static extern void DoLicenseError([MarshalAs(UnmanagedType.Bool)] bool state);

	/// <summary>原生 HLIUseSpinLock：启用/关闭原生层线程同步所用的自旋锁。</summary>
	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIUseSpinLock")]
	public static extern void UseSpinLock([MarshalAs(UnmanagedType.Bool)] bool state);

	/// <summary>原生 HLIStartUpThreadPool：启用/关闭原生层线程池。</summary>
	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIStartUpThreadPool")]
	public static extern void StartUpThreadPool([MarshalAs(UnmanagedType.Bool)] bool state);


	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIIsUTF8Encoding")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsUTF8Encoding();

	/// <summary>原生 HGetMemoryAllocatorType（StdCall）：查询当前原生库使用的内存分配器类型。</summary>
	[DllImport("JLVisionCore", CallingConvention = CallingConvention.StdCall, EntryPoint = "HGetMemoryAllocatorType")]
	public static extern JlMemoryAllocatorType GetMemoryAllocatorType();

	/// <summary>原生 HSetMemoryAllocatorType（StdCall）：设置原生库使用的内存分配器类型。</summary>
	[DllImport("JLVisionCore", CallingConvention = CallingConvention.StdCall, EntryPoint = "HSetMemoryAllocatorType")]
	public static extern void SetMemoryAllocatorType(JlMemoryAllocatorType allocator);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLIGetSerializedSize(IntPtr ptr, out ulong size);

	internal static int GetSerializedSize(byte[] header, out ulong size)
	{
		GCHandle gCHandle = GCHandle.Alloc(header, GCHandleType.Pinned);
		int result = HLIGetSerializedSize(gCHandle.AddrOfPinnedObject(), out size);
		gCHandle.Free();
		return result;
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLILock")]
	internal static extern void Lock();

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIUnlock")]
	internal static extern void Unlock();













	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLICreateProcedure")]
	private static extern int CreateProcedure(int procIndex, out IntPtr proc);

	/// <summary>原生 HLICallProcedure：执行已配置好的算子实例 proc，返回算子执行结果错误码。</summary>
	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLICallProcedure")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int CallProcedure(IntPtr proc);

	/// <summary>原生 HLIDestroyProcedure：销毁算子实例 proc（携其执行结果码 procResult），返回销毁过程的错误码。</summary>
	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIDestroyProcedure")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int DestroyProcedure(IntPtr proc, int procResult);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr HLIGetLogicalName(IntPtr proc);

	internal static string GetLogicalName(IntPtr proc)
	{
		return Marshal.PtrToStringAnsi(HLIGetLogicalName(proc));
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLILogicalName")]
	private static extern IntPtr HLIGetLogicalName(int procIndex);

	internal static string GetLogicalName(int procIndex)
	{
		return Marshal.PtrToStringAnsi(HLIGetLogicalName(procIndex));
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetProcIndex")]
	private static extern int GetProcIndex(IntPtr proc);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLIGetErrorMessage(int err, IntPtr buffer);

	internal static string GetErrorMessage(int err)
	{
		IntPtr intPtr = Marshal.AllocHGlobal(1024);
		HLIGetErrorMessage(err, intPtr);
		string result = FromNativeEncoding(intPtr, force_utf8: false);
		Marshal.FreeHGlobal(intPtr);
		return result;
	}

	/// <summary>调用前置：按算子索引 procIndex 创建算子实例并返回其句柄，创建失败时抛出 JlOperatorException。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static IntPtr PreCall(int procIndex)
	{
		int num = CreateProcedure(procIndex, out var proc);
		if (num != 2)
		{
			JlOperatorException.throwInfo(num, "Could not create a new operator instance for id " + procIndex);
		}
		return proc;
	}

	/// <summary>调用后置：清除算子实例挂起的 I/O 元组并销毁实例，若 procResult/销毁返回码为失败则抛出对应算子异常。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void PostCall(IntPtr proc, int procResult)
	{
		int procIndex = GetProcIndex(proc);
		HLIClearAllIOCT(proc);
		int err = DestroyProcedure(proc, procResult);
		if (procIndex >= 0)
		{
			JlOperatorException.throwOperator(err, procIndex);
			JlOperatorException.throwOperator(procResult, procIndex);
		}
		else
		{
			JlOperatorException.throwOperator(err, "Unknown");
			JlOperatorException.throwOperator(procResult, "Unknown");
		}
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetInputObject")]
	internal static extern int SetInputObject(IntPtr proc, int parIndex, IntPtr key);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetOutputObject")]
	internal static extern int GetOutputObject(IntPtr proc, int parIndex, out IntPtr key);

	internal static void ClearObject(IntPtr key)
	{
		IntPtr proc = PreCall(570);
		JlCkP(proc, SetInputObject(proc, 1, key));
		int procResult = CallProcedure(proc);
		PostCall(proc, procResult);
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLICopyObject(IntPtr keyIn, out IntPtr keyOut);

	internal static IntPtr CopyObject(IntPtr key)
	{
		IntPtr proc = PreCall(568);
		JlCkP(proc, SetInputObject(proc, 1, key));
		StoreI(proc, 0, 1);
		StoreI(proc, 1, -1);
		int num = CallProcedure(proc);
		if (!IsFailure(num))
		{
			num = GetOutputObject(proc, 1, out key);
		}
		PostCall(proc, num);
		return key;
	}

	internal static string GetObjClass(IntPtr key)
	{
		JlTuple tuple = "object";
		IntPtr proc = PreCall(579);
		JlCkP(proc, SetInputObject(proc, 1, key));
		InitOCT(proc, 0);
		int num = CallProcedure(proc);
		if (!IsFailure(num))
		{
			num = JlTuple.LoadNew(proc, 0, num, out tuple);
		}
		PostCall(proc, num);
		if (tuple.Length <= 0)
		{
			return "any";
		}
		return tuple.S;
	}

	internal static void AssertObjectClass(IntPtr key, string assertClass)
	{
		if (key != JlObjectBase.UNDEF)
		{
			string objClass = GetObjClass(key);
			if (!objClass.StartsWith(assertClass) && objClass != "any")
			{
				throw new JlException("Iconic object type mismatch (expected " + assertClass + ", got " + objClass + ")");
			}
		}
	}

	/// <summary>原生 HLICreateTuple：新建一个空的原生元组并通过 out tuple 返回其句柄。</summary>
	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLICreateTuple")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int CreateTuple(out IntPtr tuple);

	/// <summary>原生 HLIInitOCT：为算子 proc 的第 parIndex 个输出参数初始化空元组，供调用后写回结果。</summary>
	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIInitOCT")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int InitOCT(IntPtr proc, int parIndex);

	/// <summary>原生 HLIClearAllIOCT：清除算子实例 proc 上挂起的全部输入/输出元组引用（PostCall 中调用）。</summary>
	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int HLIClearAllIOCT(IntPtr proc);

	/// <summary>原生 HLIDestroyTuple：销毁原生元组句柄并释放其占用的内存。</summary>
	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIDestroyTuple")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static extern int DestroyTuple(IntPtr tuple);

	/// <summary>把托管 JlTuple 的全部元素按其类型（int/long/double/string/handle/混合）逐值写入已分配好的原生元组句柄。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreTuple(IntPtr tupleHandle, JlTuple tuple)
	{
		JlTupleType type = ((tuple.Type == JlTupleType.LONG) ? JlTupleType.INTEGER : tuple.Type);
		JlCheckNative(CreateElementsOfType(tupleHandle, tuple.Length, type));
		switch (tuple.Type)
		{
		case JlTupleType.INTEGER:
			JlCheckNative(SetIArr(tupleHandle, tuple.IArr));
			break;
		case JlTupleType.LONG:
			JlCheckNative(SetLArr(tupleHandle, tuple.LArr));
			break;
		case JlTupleType.DOUBLE:
			JlCheckNative(SetDArr(tupleHandle, tuple.DArr));
			break;
		case JlTupleType.STRING:
		{
			string[] sArr = tuple.SArr;
			for (int k = 0; k < tuple.Length; k++)
			{
				JlCheckNative(SetS(tupleHandle, k, sArr[k], force_utf8: true));
			}
			break;
		}
		case JlTupleType.JlANDLE:
		{
			JlHandle[] jlArr = tuple.JlArr;
			for (int j = 0; j < tuple.Length; j++)
			{
				JlCheckNative(SetH(tupleHandle, j, jlArr[j]));
			}
			break;
		}
		case JlTupleType.MIXED:
		{
			object[] oArr = tuple.data.OArr;
			for (int i = 0; i < tuple.Length; i++)
			{
				switch (JlTupleImplementation.GetObjectType(oArr[i]))
				{
				case 1:
					JlCheckNative(SetI(tupleHandle, i, (int)oArr[i]));
					break;
				case 129:
					JlCheckNative(SetL(tupleHandle, i, (long)oArr[i]));
					break;
				case 2:
					JlCheckNative(SetD(tupleHandle, i, (double)oArr[i]));
					break;
				case 4:
					JlCheckNative(SetS(tupleHandle, i, (string)oArr[i], force_utf8: true));
					break;
				case 16:
					JlCheckNative(SetH(tupleHandle, i, (JlHandle)oArr[i]));
					break;
				}
			}
			break;
		}
		}
	}

	/// <summary>以 MIXED 类型从原生元组句柄读取全部元素（字符串按 UTF-8 解码），构造并返回托管 JlTuple。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static JlTuple LoadTuple(IntPtr tupleHandle)
	{
		JlTupleImplementation.LoadData(tupleHandle, JlTupleType.MIXED, out var data, force_utf8: true);
		return new JlTuple(data);
	}

	private static void JlCheckNative(int err)
	{
		if (IsFailure(err))
		{
			throw new JlOperatorException(err);
		}
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetInputTuple")]
	internal static extern int GetInputTuple(IntPtr proc, int parIndex, out IntPtr tuple);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int HLICreateElementsOfType(IntPtr tuple, int length, JlTupleType type);

	internal static int CreateElementsOfType(IntPtr tuple, int length, JlTupleType type)
	{
		JlTupleType type2 = ((type == JlTupleType.EMPTY) ? JlTupleType.MIXED : type);
		return HLICreateElementsOfType(tuple, length, type2);
	}

	internal static int CreateInputTuple(IntPtr proc, int parIndex, int length, JlTupleType type, out IntPtr tuple)
	{
		int inputTuple = GetInputTuple(proc, parIndex, out tuple);
		if (!IsFailure(inputTuple))
		{
			return CreateElementsOfType(tuple, length, type);
		}
		return inputTuple;
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetOutputTuple")]
	internal static extern int GetOutputTuple(IntPtr proc, int parIndex, [MarshalAs(UnmanagedType.Bool)] bool handleType, out IntPtr tuple);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetTupleLength")]
	internal static extern int GetTupleLength(IntPtr tuple, out int length);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetTupleTypeScanElem")]
	internal static extern int GetTupleTypeScanElem(IntPtr tuple, out int type);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetElementType")]
	internal static extern int GetElementType(IntPtr tuple, int index, out JlTupleType type);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetI")]
	internal static extern int SetI(IntPtr tuple, int index, int intValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetL")]
	internal static extern int SetL(IntPtr tuple, int index, long longValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetD")]
	internal static extern int SetD(IntPtr tuple, int index, double doubleValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int HLISetS(IntPtr tuple, int index, IntPtr stringValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetH")]
	internal static extern int SetH(IntPtr tuple, int index, IntPtr handleValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLICopyHandle(IntPtr handle, out IntPtr handleCopy);

	internal static IntPtr CopyHandle(IntPtr handle)
	{
		JlCheckNative(HLICopyHandle(handle, out var handleCopy));
		return handleCopy;
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIClearHandle")]
	internal static extern int ClearHandle(IntPtr handle);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLIHandleToHlong(IntPtr handle, out IntPtr handleLong);

	internal static IntPtr HandleToHlong(IntPtr handle)
	{
		JlCheckNative(HLIHandleToHlong(handle, out var handleLong));
		return handleLong;
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLIHandleIsValid(IntPtr handle, [MarshalAs(UnmanagedType.Bool)] out bool is_valid);

	internal static bool HandleIsValid(IntPtr handle)
	{
		JlCheckNative(HLIHandleIsValid(handle, out var is_valid));
		return is_valid;
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLIGetHandleSemType(IntPtr handle, out IntPtr sem_type);

	internal static string GetHandleSemType(IntPtr handle)
	{
		JlCheckNative(HLIGetHandleSemType(handle, out var sem_type));
		return FromNativeEncoding(sem_type, force_utf8: false);
	}

	/// <summary>把 .NET 字符串复制到非托管全局内存并返回指针：force_utf8 或原生为 UTF-8 编码时写 UTF-8，否则写 ANSI；由调用方负责 FreeHGlobal。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static IntPtr ToNativeGlobalEncoding(string dotnet, bool force_utf8)
	{
		if (!force_utf8 && !IsUTF8Encoding())
		{
			return Marshal.StringToHGlobalAnsi(dotnet);
		}
		return ToHGlobalUtf8Encoding(dotnet);
	}

	/// <summary>把 .NET 字符串按 UTF-8 编码（含结尾零字节）复制到非托管全局内存并返回指针；由调用方负责 FreeHGlobal。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static IntPtr ToHGlobalUtf8Encoding(string dotnet)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(dotnet);
		int num = Marshal.SizeOf(bytes.GetType().GetElementType()) * bytes.Length;
		IntPtr intPtr = Marshal.AllocHGlobal(num + 1);
		Marshal.Copy(bytes, 0, intPtr, bytes.Length);
		Marshal.WriteByte(intPtr, num, 0);
		return intPtr;
	}

	internal static int SetS(IntPtr tuple, int index, string dotnet_string, bool force_utf8)
	{
		if (dotnet_string == null)
		{
			dotnet_string = "";
		}
		IntPtr intPtr = ToNativeGlobalEncoding(dotnet_string, force_utf8);
		int result = HLISetS(tuple, index, intPtr);
		Marshal.FreeHGlobal(intPtr);
		return result;
	}

	internal static int SetIP(IntPtr tuple, int index, IntPtr intPtrValue)
	{
		if (isPlatform64)
		{
			return SetL(tuple, index, intPtrValue.ToInt64());
		}
		return SetI(tuple, index, intPtrValue.ToInt32());
	}

	/// <summary>为算子 proc 的第 parIndex 个输入参数创建单元素 INTEGER 元组并写入 int 值。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreI(IntPtr proc, int parIndex, int intValue)
	{
		JlCkP(proc, CreateInputTuple(proc, parIndex, 1, JlTupleType.INTEGER, out var tuple));
		SetI(tuple, 0, intValue);
	}

	/// <summary>为算子 proc 的第 parIndex 个输入参数创建单元素 INTEGER 元组并写入 long 值。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreL(IntPtr proc, int parIndex, long longValue)
	{
		JlCkP(proc, CreateInputTuple(proc, parIndex, 1, JlTupleType.INTEGER, out var tuple));
		SetL(tuple, 0, longValue);
	}

	/// <summary>为算子 proc 的第 parIndex 个输入参数创建单元素 DOUBLE 元组并写入 double 值。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreD(IntPtr proc, int parIndex, double doubleValue)
	{
		JlCkP(proc, CreateInputTuple(proc, parIndex, 1, JlTupleType.DOUBLE, out var tuple));
		SetD(tuple, 0, doubleValue);
	}

	/// <summary>为算子 proc 的第 parIndex 个输入参数创建单元素 STRING 元组并写入字符串（null 视为空串，ANSI 编码）。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreS(IntPtr proc, int parIndex, string stringValue)
	{
		if (stringValue == null)
		{
			stringValue = "";
		}
		JlCkP(proc, CreateInputTuple(proc, parIndex, 1, JlTupleType.STRING, out var tuple));
		JlCkP(proc, SetS(tuple, 0, stringValue, force_utf8: false));
	}

	/// <summary>为算子 proc 的第 parIndex 个输入参数创建单元素 HANDLE 元组并写入原生句柄值。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreH(IntPtr proc, int parIndex, IntPtr handleValue)
	{
		JlCkP(proc, CreateInputTuple(proc, parIndex, 1, JlTupleType.JlANDLE, out var tuple));
		JlCkP(proc, SetH(tuple, 0, handleValue));
	}

	/// <summary>为算子 proc 的第 parIndex 个输入参数创建单元素 INTEGER 元组并按平台位宽写入 IntPtr 值。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void StoreIP(IntPtr proc, int parIndex, IntPtr intPtrValue)
	{
		JlCkP(proc, CreateInputTuple(proc, parIndex, 1, JlTupleType.INTEGER, out var tuple));
		SetIP(tuple, 0, intPtrValue);
	}

	/// <summary>把托管 JlTuple 存为算子第 parIndex 个输入参数；null 时存入空元组。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void Store(IntPtr proc, int parIndex, JlTuple tupleValue)
	{
		if (tupleValue == null)
		{
			tupleValue = new JlTuple();
		}
		tupleValue.Store(proc, parIndex);
	}

	/// <summary>把 JlHandle 存为算子第 parIndex 个输入参数；null 时存入空句柄。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void Store(IntPtr proc, int parIndex, JlHandle handleValue)
	{
		if (handleValue == null)
		{
			handleValue = new JlHandle();
		}
		handleValue.Store(proc, parIndex);
	}

	/// <summary>把图标对象（JlObjectBase 子类）存为算子第 parIndex 个输入参数；null 时存入空对象。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void Store(IntPtr proc, int parIndex, JlObjectBase objectValue)
	{
		if (objectValue == null)
		{
			objectValue = new JlObjectBase();
		}
		objectValue.Store(proc, parIndex);
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetIArr")]
	internal static extern int SetIArr(IntPtr tuple, int[] intArray);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetIArrPtr")]
	internal static extern int SetIArrPtr(IntPtr tuple, int[] intArray, int length);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetLArr")]
	internal static extern int SetLArr(IntPtr tuple, long[] longArray);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetLArrPtr")]
	internal static extern int SetLArrPtr(IntPtr tuple, long[] longArray, int length);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetDArr")]
	internal static extern int SetDArr(IntPtr tuple, double[] doubleArray);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLISetDArrPtr")]
	internal static extern int SetDArrPtr(IntPtr tuple, double[] doubleArray, int length);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetI")]
	internal static extern int GetI(IntPtr tuple, int index, out int intValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetL")]
	internal static extern int GetL(IntPtr tuple, int index, out long longValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int HLIGetH(IntPtr tuple, int index, out IntPtr longValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetD")]
	internal static extern int GetD(IntPtr tuple, int index, out double doubleValue);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl)]
	private static extern int HLIGetS(IntPtr tuple, int index, out IntPtr stringPtr);

	/// <summary>把原生字符串指针解码为 .NET 字符串：force_utf8 或原生为 UTF-8 编码时按 UTF-8 手工扫描解码，否则按 ANSI。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static string FromNativeEncoding(IntPtr Vision, bool force_utf8)
	{
		if (force_utf8 || IsUTF8Encoding())
		{
			int i;
			for (i = 0; Marshal.ReadByte(Vision, i) != 0; i++)
			{
			}
			byte[] array = new byte[i];
			Marshal.Copy(Vision, array, 0, array.Length);
			return Encoding.UTF8.GetString(array);
		}
		return Marshal.PtrToStringAnsi(Vision);
	}

	internal static int GetS(IntPtr tuple, int index, out string stringValue, bool force_utf8)
	{
		stringValue = string.Empty;
		int num = HLIGetS(tuple, index, out var stringPtr);
		if (num != 2)
		{
			return num;
		}
		stringValue = FromNativeEncoding(stringPtr, force_utf8);
		if (stringValue == null)
		{
			stringValue = "";
			return 5;
		}
		return 2;
	}

	internal static int GetH(IntPtr tuple, int index, out JlHandle handle)
	{
		int result = HLIGetH(tuple, index, out var longValue);
		handle = new JlHandle(longValue);
		return result;
	}

	internal static int GetIP(IntPtr tuple, int index, out IntPtr intPtrValue)
	{
		int result;
		if (isPlatform64)
		{
			result = GetL(tuple, index, out var longValue);
			intPtrValue = new IntPtr(longValue);
		}
		else
		{
			result = GetI(tuple, index, out var intValue);
			intPtrValue = new IntPtr(intValue);
		}
		return result;
	}

	private static int JlCkSingle(IntPtr tuple, JlTupleType expectedType)
	{
		int length = 0;
		if (tuple != IntPtr.Zero)
		{
			GetTupleLength(tuple, out length);
		}
		if (length > 0)
		{
			GetElementType(tuple, 0, out var type);
			if (type != expectedType)
			{
				return 7002;
			}
			return 2;
		}
		return 7001;
	}

	/// <summary>读取算子第 parIndex 个输出的标量 int：INTEGER 直取，DOUBLE 自动取整；入参 err 已失败则原样透传。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadI(IntPtr proc, int parIndex, int err, out int intValue)
	{
		if (IsFailure(err))
		{
			intValue = -1;
			return err;
		}
		IntPtr tuple = IntPtr.Zero;
		GetOutputTuple(proc, parIndex, handleType: false, out tuple);
		err = JlCkSingle(tuple, JlTupleType.INTEGER);
		if (err != 2)
		{
			err = JlCkSingle(tuple, JlTupleType.DOUBLE);
			if (err != 2)
			{
				intValue = -1;
				return err;
			}
			double doubleValue = -1.0;
			err = GetD(tuple, 0, out doubleValue);
			intValue = (int)doubleValue;
			return err;
		}
		return GetI(tuple, 0, out intValue);
	}

	/// <summary>读取算子第 parIndex 个输出的标量 long：INTEGER 直取，DOUBLE 自动取整；入参 err 已失败则原样透传。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadL(IntPtr proc, int parIndex, int err, out long longValue)
	{
		if (IsFailure(err))
		{
			longValue = -1L;
			return err;
		}
		IntPtr tuple = IntPtr.Zero;
		GetOutputTuple(proc, parIndex, handleType: false, out tuple);
		err = JlCkSingle(tuple, JlTupleType.INTEGER);
		if (err != 2)
		{
			err = JlCkSingle(tuple, JlTupleType.DOUBLE);
			if (err != 2)
			{
				longValue = -1L;
				return err;
			}
			double doubleValue = -1.0;
			err = GetD(tuple, 0, out doubleValue);
			longValue = (long)doubleValue;
			return err;
		}
		return GetL(tuple, 0, out longValue);
	}

	/// <summary>读取算子第 parIndex 个输出的标量 double：DOUBLE 直取，INTEGER 自动升为 double；入参 err 已失败则原样透传。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadD(IntPtr proc, int parIndex, int err, out double doubleValue)
	{
		if (IsFailure(err))
		{
			doubleValue = -1.0;
			return err;
		}
		IntPtr tuple = IntPtr.Zero;
		GetOutputTuple(proc, parIndex, handleType: false, out tuple);
		err = JlCkSingle(tuple, JlTupleType.DOUBLE);
		if (err != 2)
		{
			err = JlCkSingle(tuple, JlTupleType.INTEGER);
			if (err != 2)
			{
				doubleValue = -1.0;
				return err;
			}
			int intValue = -1;
			err = GetI(tuple, 0, out intValue);
			doubleValue = intValue;
			return err;
		}
		return GetD(tuple, 0, out doubleValue);
	}

	/// <summary>读取算子第 parIndex 个输出的标量字符串（按 ANSI 解码）；入参 err 已失败则原样透传。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadS(IntPtr proc, int parIndex, int err, out string stringValue)
	{
		if (IsFailure(err))
		{
			stringValue = "";
			return err;
		}
		IntPtr tuple = IntPtr.Zero;
		GetOutputTuple(proc, parIndex, handleType: false, out tuple);
		err = JlCkSingle(tuple, JlTupleType.STRING);
		if (err != 2)
		{
			stringValue = "";
			return err;
		}
		return GetS(tuple, 0, out stringValue, force_utf8: false);
	}

	/// <summary>读取算子第 parIndex 个输出的单元素 INTEGER 元组并按平台位宽转为 IntPtr；类型不符或出错时返回 Zero。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadIP(IntPtr proc, int parIndex, int err, out IntPtr intPtrValue)
	{
		if (IsFailure(err))
		{
			intPtrValue = IntPtr.Zero;
			return err;
		}
		GetOutputTuple(proc, parIndex, handleType: false, out var tuple);
		err = JlCkSingle(tuple, JlTupleType.INTEGER);
		if (err != 2)
		{
			intPtrValue = IntPtr.Zero;
			return err;
		}
		return GetIP(tuple, 0, out intPtrValue);
	}

	/// <summary>读取算子第 parIndex 个输出的标量 handle（以 handleType=true 取回）并包装为 JlHandle；失败时返回空 JlHandle。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static int LoadH(IntPtr proc, int parIndex, int err, out JlHandle handleValue)
	{
		if (IsFailure(err))
		{
			handleValue = new JlHandle();
			return err;
		}
		GetOutputTuple(proc, parIndex, handleType: true, out var tuple);
		err = JlCkSingle(tuple, JlTupleType.JlANDLE);
		if (err != 2)
		{
			handleValue = new JlHandle();
			return err;
		}
		return GetH(tuple, 0, out handleValue);
	}

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetIArr")]
	internal static extern int GetIArr(IntPtr tuple, [Out] int[] intArray);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetLArr")]
	internal static extern int GetLArr(IntPtr tuple, [Out] long[] longArray);

	[DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "HLIGetDArr")]
	internal static extern int GetDArr(IntPtr tuple, [Out] double[] doubleArray);

	/// <summary>解除 JlTuple 对非托管数组缓冲的 GCHandle 固定（在数据已传给原生算子后调用）；tuple 为 null 时忽略。</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void UnpinTuple(JlTuple tuple)
	{
		tuple?.UnpinTuple();
	}







	internal static bool IsError(int err)
	{
		return err >= 1000;
	}

	internal static bool IsFailure(int err)
	{
		if (err != 2)
		{
			return err != 2;
		}
		return false;
	}

	internal static void JlCkP(IntPtr proc, int err)
	{
		if (IsFailure(err))
		{
			PostCall(proc, err);
		}
	}
}
