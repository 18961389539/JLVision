using System;

namespace JLVisionLib;

/// <summary>位姿构造和转换所需的表示选项。该类型把三个彼此相关的字符串参数绑定成一个不可变值。</summary>
public sealed class JlPoseOptions
{
	/// <summary>变换顺序，例如 <c>Rp+T</c> 或 <c>T+Rp</c>。</summary>
	public string OrderOfTransform { get; }

	/// <summary>旋转顺序，例如 <c>gba</c> 或 <c>abg</c>。</summary>
	public string OrderOfRotation { get; }

	/// <summary>位姿的观察约定，例如 <c>point</c>。</summary>
	public string ViewOfTransform { get; }

	/// <summary>库中最常用的点位姿表示：<c>Rp+T</c>、<c>gba</c>、<c>point</c>。</summary>
	public static JlPoseOptions Default { get; } = new JlPoseOptions("Rp+T", "gba", "point");

	/// <summary>创建一组位姿表示选项。字符串保留扩展空间，但不再散落在每个调用点。</summary>
	public JlPoseOptions(string orderOfTransform, string orderOfRotation, string viewOfTransform)
	{
		OrderOfTransform = RequireValue(orderOfTransform, nameof(orderOfTransform));
		OrderOfRotation = RequireValue(orderOfRotation, nameof(orderOfRotation));
		ViewOfTransform = RequireValue(viewOfTransform, nameof(viewOfTransform));
	}

	private static string RequireValue(string value, string name)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException("An option value is required.", name);
		}
		return value;
	}
}

/// <summary>位姿平均的算法模式。</summary>
public enum JlPoseAverageMode
{
	/// <summary>迭代平均。</summary>
	Iterative
}

/// <summary>几何重采样或边界填充策略。</summary>
public enum JlInterpolationMode
{
	/// <summary>最近邻。</summary>
	NearestNeighbor,
	/// <summary>双线性。</summary>
	Bilinear,
	/// <summary>双三次。</summary>
	Bicubic,
	/// <summary>常量填充。</summary>
	Constant
}

/// <summary>图像像素类型。</summary>
public enum JlImageType
{
	/// <summary>8 位无符号整数。</summary>
	Byte,
	/// <summary>1 位整数。</summary>
	Int1,
	/// <summary>16 位无符号整数。</summary>
	UInt2,
	/// <summary>16 位有符号整数。</summary>
	Int2,
	/// <summary>32 位无符号整数。</summary>
	UInt4,
	/// <summary>32 位有符号整数。</summary>
	Int4,
	/// <summary>双精度浮点数。</summary>
	Real,
	/// <summary>循环灰度值。</summary>
	Cyclic,
	/// <summary>方向值。</summary>
	Direction,
	/// <summary>复数值。</summary>
	Complex
}

/// <summary>是否启用一个原生可选特性。</summary>
public enum JlBooleanOption
{
	/// <summary>关闭选项。</summary>
	False,
	/// <summary>启用选项。</summary>
	True
}

/// <summary>亚像素定位策略。</summary>
public enum JlSubPixelMode
{
	/// <summary>不使用亚像素定位。</summary>
	None,
	/// <summary>插值定位。</summary>
	Interpolation,
	/// <summary>最小二乘定位。</summary>
	LeastSquares,
	/// <summary>回归定位。</summary>
	Regression
}

/// <summary>Lepetit 兴趣点检测支持的亚像素策略。</summary>
public enum JlLepetitSubPixelMode
{
	/// <summary>不使用亚像素定位。</summary>
	None,
	/// <summary>插值定位。</summary>
	Interpolation,
	/// <summary>回归定位。</summary>
	Regression
}

/// <summary>NCC 模型匹配的亚像素开关。NCC 原生接口使用 true/false 字符串。</summary>
public enum JlNccSubPixelMode
{
	/// <summary>禁用亚像素匹配。</summary>
	Disabled,
	/// <summary>启用亚像素匹配。</summary>
	Enabled
}

/// <summary>使用 on/off 表示的亚像素开关。</summary>
public enum JlOnOffOption
{
	/// <summary>关闭选项。</summary>
	Off,
	/// <summary>启用选项。</summary>
	On
}

/// <summary>Bayer 彩色滤光阵列的左上角排列方式。</summary>
public enum JlCfaPattern
{
	/// <summary>Bayer GB 排列。</summary>
	BayerGb,
	/// <summary>Bayer GR 排列。</summary>
	BayerGr,
	/// <summary>Bayer RG 排列。</summary>
	BayerRg,
	/// <summary>Bayer BG 排列。</summary>
	BayerBg
}

/// <summary>点到直线估计中当前运行库明确支持的刚体模型。</summary>
public enum JlPointLineTransformType
{
	/// <summary>刚体变换。</summary>
	Rigid
}

/// <summary>二维斜切所沿的坐标轴。</summary>
public enum JlAxis
{
	/// <summary>X 轴。</summary>
	X,
	/// <summary>Y 轴。</summary>
	Y
}

/// <summary>投影矩阵估计所用的算法。</summary>
public enum JlHomographyMethod
{
	/// <summary>Gold Standard 优化。</summary>
	GoldStandard,
	/// <summary>归一化 DLT。</summary>
	NormalizedDlt
}

internal static class JlOptionConversions
{
	internal static string ToNative(this JlInterpolationMode value)
	{
		return value switch
		{
			JlInterpolationMode.NearestNeighbor => "nearest_neighbor",
			JlInterpolationMode.Bilinear => "bilinear",
			JlInterpolationMode.Bicubic => "bicubic",
			JlInterpolationMode.Constant => "constant",
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported interpolation mode.")
		};
	}

	internal static string ToNative(this JlImageType value)
	{
		return value switch
		{
			JlImageType.Byte => "byte",
			JlImageType.Int1 => "int1",
			JlImageType.UInt2 => "uint2",
			JlImageType.Int2 => "int2",
			JlImageType.UInt4 => "uint4",
			JlImageType.Int4 => "int4",
			JlImageType.Real => "real",
			JlImageType.Cyclic => "cyclic",
			JlImageType.Direction => "direction",
			JlImageType.Complex => "complex",
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported image type.")
		};
	}

	internal static string ToNative(this JlBooleanOption value)
	{
		return value switch
		{
			JlBooleanOption.False => "false",
			JlBooleanOption.True => "true",
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported boolean option.")
		};
	}

	internal static string ToNative(this JlSubPixelMode value)
	{
		return value switch
		{
			JlSubPixelMode.None => "none",
			JlSubPixelMode.Interpolation => "interpolation",
			JlSubPixelMode.LeastSquares => "least_squares",
			JlSubPixelMode.Regression => "regression",
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported sub-pixel mode.")
		};
	}

	internal static string ToNative(this JlNccSubPixelMode value)
	{
		return value switch
		{
			JlNccSubPixelMode.Disabled => "false",
			JlNccSubPixelMode.Enabled => "true",
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported NCC sub-pixel mode.")
		};
	}

	internal static string ToNative(this JlLepetitSubPixelMode value)
	{
		return value switch
		{
			JlLepetitSubPixelMode.None => "none",
			JlLepetitSubPixelMode.Interpolation => "interpolation",
			JlLepetitSubPixelMode.Regression => "regression",
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported Lepetit sub-pixel mode.")
		};
	}

	internal static string ToNative(this JlOnOffOption value)
	{
		return value switch
		{
			JlOnOffOption.Off => "off",
			JlOnOffOption.On => "on",
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported on/off option.")
		};
	}

	internal static string ToNative(this JlCfaPattern value)
	{
		return value switch
		{
			JlCfaPattern.BayerGb => "bayer_gb",
			JlCfaPattern.BayerGr => "bayer_gr",
			JlCfaPattern.BayerRg => "bayer_rg",
			JlCfaPattern.BayerBg => "bayer_bg",
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported CFA pattern.")
		};
	}

	internal static string ToNative(this JlPointLineTransformType value)
	{
		return value switch
		{
			JlPointLineTransformType.Rigid => "rigid",
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported point-line transformation type.")
		};
	}

	internal static string ToNative(this JlAxis value)
	{
		return value switch
		{
			JlAxis.X => "x",
			JlAxis.Y => "y",
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported axis.")
		};
	}

	internal static string ToNative(this JlHomographyMethod value)
	{
		return value switch
		{
			JlHomographyMethod.GoldStandard => "gold_standard",
			JlHomographyMethod.NormalizedDlt => "normalized_dlt",
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported homography method.")
		};
	}

	internal static string ToNative(this JlPoseAverageMode value)
	{
		return value switch
		{
			JlPoseAverageMode.Iterative => "iterative",
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported pose average mode.")
		};
	}
}
