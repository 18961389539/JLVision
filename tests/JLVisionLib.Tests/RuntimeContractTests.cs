using System.Reflection;
using System.Runtime.InteropServices;
using JLVisionLib;
using Xunit;

namespace JLVisionLib.Tests;

/// <summary>验证运行库的异常、资源生命周期和兼容性契约。</summary>
public sealed class RuntimeContractTests
{
	private static readonly int[] SourceValues = { 1, 2 };

    /// <summary>确保操作异常序列化时保留构造函数提供的上下文。</summary>
    [Fact]
	public void OperatorExceptionSerializationPreservesConstructedContext()
	{
		var exception = new JlOperatorException(5, "custom context");

		exception.ToHTuple(out JlTuple tuple);
		try
		{
			Assert.Equal(5, tuple[0].I);
			Assert.Equal("custom context", tuple[1].S);
		}
		finally
		{
			tuple.Dispose();
		}
	}

    /// <summary>确保 JlData 的 RawData 是独立且可释放的副本。</summary>
    [Fact]
	public void JlDataRawDataIsAnIndependentDisposableCopy()
	{
		using var source = new JlTuple(SourceValues);
		var data = new JlData(source);

		using (JlTuple copy = data.RawData)
		{
			copy[0].I = 99;
			Assert.Equal(1, data[0].I);
		}

		data.Dispose();
		Assert.Equal(0, data.RawData.Length);
	}

    /// <summary>确保历史低层入口仍保持公共可见性。</summary>
    [Fact]
	public void LegacyLowLevelEntryPointsRemainPublic()
	{
		Assert.True(typeof(JlNativeApi).IsPublic);
		Assert.NotNull(typeof(JlHandleBase).GetField("UNDEF", BindingFlags.Public | BindingFlags.Static));
		Assert.NotNull(typeof(JlHandleBase).GetProperty("Handle", BindingFlags.Public | BindingFlags.Instance));
		Assert.NotNull(typeof(JlObjectBase).GetProperty("Key", BindingFlags.Public | BindingFlags.Instance));
		Assert.NotNull(typeof(JlImage).GetConstructor(new[] { typeof(IntPtr) }));
		Assert.NotNull(typeof(JlTuple).GetConstructor(new[] { typeof(IntPtr) }));
	}

    /// <summary>确保运行库安装诊断返回一致状态。</summary>
    [Fact]
	public void RuntimeDiagnosticsHaveConsistentState()
	{
		JlRuntimeDiagnostics diagnostics = JLVisionRuntime.CheckInstallation();

		Assert.NotNull(diagnostics.Message);
		Assert.Equal(diagnostics.Status == JlRuntimeStatus.Ready, diagnostics.IsReady);
		Assert.Equal(Architecture.X64, diagnostics.RequiredArchitecture);
		if (diagnostics.Status == JlRuntimeStatus.Ready)
		{
			Assert.True(File.Exists(diagnostics.NativeLibraryPath));
		}
	}
}
