using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace JLVisionLib;

/// <summary>
/// 描述 JLVision 原生运行库当前是否可用以及失败原因。
/// </summary>
public enum JlRuntimeStatus
{
	/// <summary>运行库已成功加载，可以调用算子。</summary>
	Ready,
	/// <summary>当前操作系统不是 Windows。</summary>
	UnsupportedPlatform,
	/// <summary>当前进程不是 x64，而随包提供的核心库是 x64。</summary>
	UnsupportedArchitecture,
	/// <summary>在应用程序目录中没有找到 JLVisionCore.dll。</summary>
	NativeLibraryMissing,
	/// <summary>找到了 JLVisionCore.dll，但 Windows 无法加载它。</summary>
	NativeLibraryLoadFailed
}

/// <summary>
/// JLVision 原生运行库检查结果。该对象不包含原生句柄，可安全用于日志、诊断页面和启动检查。
/// </summary>
public sealed class JlRuntimeDiagnostics
{
	internal JlRuntimeDiagnostics(
		JlRuntimeStatus status,
		string nativeLibraryPath,
		string operatingSystem,
		Architecture processArchitecture,
		int nativeErrorCode,
		string message)
	{
		Status = status;
		NativeLibraryPath = nativeLibraryPath;
		OperatingSystem = operatingSystem;
		ProcessArchitecture = processArchitecture;
		RequiredArchitecture = Architecture.X64;
		NativeErrorCode = nativeErrorCode;
		Message = message;
	}

	/// <summary>运行库检查状态。</summary>
	public JlRuntimeStatus Status { get; }

	/// <summary>状态为 <see cref="JlRuntimeStatus.Ready"/> 时为 true。</summary>
	public bool IsReady => Status == JlRuntimeStatus.Ready;

	/// <summary>运行库文件的预期路径。</summary>
	public string NativeLibraryPath { get; }

	/// <summary>当前操作系统描述。</summary>
	public string OperatingSystem { get; }

	/// <summary>当前进程架构。</summary>
	public Architecture ProcessArchitecture { get; }

	/// <summary>当前 NuGet 包支持的原生架构。</summary>
	public Architecture RequiredArchitecture { get; }

	/// <summary>Windows LoadLibrary 失败时的 Win32 错误码；非 Windows 或文件缺失时为 0。</summary>
	public int NativeErrorCode { get; }

	/// <summary>适合直接显示给用户或写入日志的诊断信息。</summary>
	public string Message { get; }

	/// <inheritdoc />
	public override string ToString() => Message;
}

/// <summary>
/// 初始化并诊断 JLVisionCore 原生运行库。
/// </summary>
/// <remarks>
/// 建议应用程序启动时显式调用 <see cref="Initialize"/>，这样缺少 DLL、架构不匹配或依赖库缺失时，
/// 可以在进入图像处理流程前显示完整诊断。现有算子调用也会在创建原生算子前执行同一检查。
/// </remarks>
public static class JLVisionRuntime
{
	private const string NativeLibraryFileName = "JLVisionCore.dll";
	private static readonly object InitializationSync = new object();
	private static JlRuntimeDiagnostics initializedDiagnostics;

	/// <summary>
	/// 检查当前应用程序目录中的 JLVisionCore.dll，并在必要时尝试加载它。
	/// </summary>
	public static JlRuntimeDiagnostics CheckInstallation()
	{
		// Resolve the path before touching JlNativeApi so unsupported platforms and
		// architectures are reported without attempting a native load.
		string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NativeLibraryFileName);
		string operatingSystem = RuntimeInformation.OSDescription;
		Architecture processArchitecture = RuntimeInformation.ProcessArchitecture;

		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return CreateFailure(
				JlRuntimeStatus.UnsupportedPlatform,
				path,
				operatingSystem,
				processArchitecture,
				0,
				$"JLVisionCore requires Windows. Current platform: {operatingSystem}.");
		}

		if (processArchitecture != Architecture.X64)
		{
			return CreateFailure(
				JlRuntimeStatus.UnsupportedArchitecture,
				path,
				operatingSystem,
				processArchitecture,
				0,
				$"JLVisionCore requires an x64 process. Current process architecture: {processArchitecture}.");
		}

		if (!File.Exists(path))
		{
			return CreateFailure(
				JlRuntimeStatus.NativeLibraryMissing,
				path,
				operatingSystem,
				processArchitecture,
				0,
				$"JLVisionCore.dll was not found at '{path}'. Copy the x64 native runtime to the application directory.");
		}

		if (JlNativeApi.IsNativeLibraryLoaded)
		{
			return CreateReady(path, operatingSystem, processArchitecture);
		}

		if (JlNativeApi.TryEnsureNativeLibrary(path, out int nativeErrorCode, out string loadError))
		{
			return CreateReady(path, operatingSystem, processArchitecture);
		}

		return CreateFailure(
			JlRuntimeStatus.NativeLibraryLoadFailed,
			path,
			operatingSystem,
			processArchitecture,
			nativeErrorCode,
			$"JLVisionCore.dll could not be loaded from '{path}'. " +
			$"Process architecture: {processArchitecture}. Win32 error: {nativeErrorCode}. {loadError}");
	}

	/// <summary>
	/// 检查并初始化运行库；失败时抛出包含完整诊断信息的 <see cref="JlRuntimeException"/>。
	/// </summary>
	/// <returns>表示已成功加载的诊断结果。</returns>
	public static JlRuntimeDiagnostics Initialize()
	{
		JlRuntimeDiagnostics cachedDiagnostics = Volatile.Read(ref initializedDiagnostics);
		if (cachedDiagnostics != null)
		{
			return cachedDiagnostics;
		}

		lock (InitializationSync)
		{
			cachedDiagnostics = Volatile.Read(ref initializedDiagnostics);
			if (cachedDiagnostics != null)
			{
				return cachedDiagnostics;
			}

			JlRuntimeDiagnostics diagnostics = CheckInstallation();
			if (!diagnostics.IsReady)
			{
				throw new JlRuntimeException(diagnostics);
			}

			Volatile.Write(ref initializedDiagnostics, diagnostics);
			return diagnostics;
		}
	}

	/// <summary>
	/// 尝试初始化运行库，不抛出运行库加载异常。
	/// </summary>
	/// <param name="diagnostics">初始化结果，可用于显示失败原因。</param>
	/// <returns>运行库可用时为 true。</returns>
	public static bool TryInitialize(out JlRuntimeDiagnostics diagnostics)
	{
		try
		{
			diagnostics = Initialize();
			return true;
		}
		catch (JlRuntimeException exception)
		{
			diagnostics = exception.Diagnostics;
			return false;
		}
	}

	/// <summary>返回当前进程是否已经成功加载 JLVisionCore.dll。</summary>
	public static bool IsInitialized => JlNativeApi.IsNativeLibraryLoaded;

	private static JlRuntimeDiagnostics CreateFailure(
		JlRuntimeStatus status,
		string path,
		string operatingSystem,
		Architecture processArchitecture,
		int nativeErrorCode,
		string message)
	{
		return new JlRuntimeDiagnostics(
			status,
			path,
			operatingSystem,
			processArchitecture,
			nativeErrorCode,
			message);
	}

	private static JlRuntimeDiagnostics CreateReady(
		string path,
		string operatingSystem,
		Architecture processArchitecture)
	{
		return new JlRuntimeDiagnostics(
			JlRuntimeStatus.Ready,
			path,
			operatingSystem,
			processArchitecture,
			0,
			$"JLVision runtime is ready. Native library: '{path}'.");
	}
}

/// <summary>JLVision 原生运行库无法初始化时抛出的异常。</summary>
public sealed class JlRuntimeException : InvalidOperationException
{
	/// <summary>构造运行库异常并保留结构化诊断结果。</summary>
	public JlRuntimeException(JlRuntimeDiagnostics diagnostics)
		: base(diagnostics == null ? "JLVision runtime initialization failed." : diagnostics.Message)
	{
		if (diagnostics == null)
		{
			throw new ArgumentNullException(nameof(diagnostics));
		}

		Diagnostics = diagnostics;
	}

	/// <summary>导致初始化失败的结构化诊断结果。</summary>
	public JlRuntimeDiagnostics Diagnostics { get; }
}
