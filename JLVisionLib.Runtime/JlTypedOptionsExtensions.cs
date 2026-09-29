namespace JLVisionLib;

/// <summary>为生成代码中的高频固定选项提供强类型调用入口。</summary>
public static class JlTypedOptionsExtensions
{
	/// <summary>使用强类型插值方式读取插值灰度。</summary>
	public static JlTuple GetGrayvalInterpolated(this JlImage image, JlTuple row, JlTuple column, JlInterpolationMode interpolation)
	{
		return image.GetGrayvalInterpolated(row, column, interpolation.ToNative());
	}

	/// <summary>使用强类型插值方式读取插值灰度（标量重载）。</summary>
	public static double GetGrayvalInterpolated(this JlImage image, double row, double column, JlInterpolationMode interpolation)
	{
		return image.GetGrayvalInterpolated(row, column, interpolation.ToNative());
	}

	/// <summary>使用强类型插值方式缩放图像。</summary>
	public static JlImage ZoomImageFactor(this JlImage image, double scaleWidth, double scaleHeight, JlInterpolationMode interpolation)
	{
		return image.ZoomImageFactor(scaleWidth, scaleHeight, interpolation.ToNative());
	}

	/// <summary>使用强类型插值方式按尺寸缩放图像。</summary>
	public static JlImage ZoomImageSize(this JlImage image, int width, int height, JlInterpolationMode interpolation)
	{
		return image.ZoomImageSize(width, height, interpolation.ToNative());
	}

	/// <summary>使用强类型插值方式旋转图像。</summary>
	public static JlImage RotateImage(this JlImage image, JlTuple phi, JlInterpolationMode interpolation)
	{
		return image.RotateImage(phi, interpolation.ToNative());
	}

	/// <summary>使用强类型插值方式旋转图像（标量重载）。</summary>
	public static JlImage RotateImage(this JlImage image, double phi, JlInterpolationMode interpolation)
	{
		return image.RotateImage(phi, interpolation.ToNative());
	}

	/// <summary>使用强类型插值方式读取 XLD 轮廓灰度。</summary>
	public static JlTuple GetGrayvalContourXld(this JlImage image, JlXLDCont contour, JlInterpolationMode interpolation)
	{
		return image.GetGrayvalContourXld(contour, interpolation.ToNative());
	}

	/// <summary>使用强类型插值方式执行通道卷积。</summary>
	public static JlImage ConvolChannels(this JlImage image, JlTuple filter, JlInterpolationMode border)
	{
		return image.ConvolChannels(filter, border.ToNative());
	}

	/// <summary>使用强类型亚像素开关查找 NCC 模型。</summary>
	public static void FindNccModel(this JlNCCModel model, JlImage image, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, JlNccSubPixelMode subPixel, JlTuple numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		model.FindNccModel(image, angleStart, angleExtent, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, out row, out column, out angle, out score);
	}

	/// <summary>使用强类型亚像素开关查找 NCC 模型（标量层数重载）。</summary>
	public static void FindNccModel(this JlNCCModel model, JlImage image, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, JlNccSubPixelMode subPixel, int numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		model.FindNccModel(image, angleStart, angleExtent, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, out row, out column, out angle, out score);
	}

	/// <summary>使用强类型亚像素方式查找形状模型。</summary>
	public static void FindShapeModel(this JlShapeModel model, JlImage image, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, JlSubPixelMode subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		model.FindShapeModel(image, angleStart, angleExtent, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, greediness, out row, out column, out angle, out score);
	}

	/// <summary>使用强类型亚像素方式查找各向异性缩放模型。</summary>
	public static void FindAnisoShapeModel(this JlShapeModel model, JlImage image, double angleStart, double angleExtent, double scaleRMin, double scaleRMax, double scaleCMin, double scaleCMax, double minScore, int numMatches, double maxOverlap, JlSubPixelMode subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score)
	{
		model.FindAnisoShapeModel(image, angleStart, angleExtent, scaleRMin, scaleRMax, scaleCMin, scaleCMax, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, greediness, out row, out column, out angle, out scaleR, out scaleC, out score);
	}

	/// <summary>使用强类型亚像素方式查找等比缩放模型。</summary>
	public static void FindScaledShapeModel(this JlShapeModel model, JlImage image, double angleStart, double angleExtent, double scaleMin, double scaleMax, double minScore, int numMatches, double maxOverlap, JlSubPixelMode subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score)
	{
		model.FindScaledShapeModel(image, angleStart, angleExtent, scaleMin, scaleMax, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, greediness, out row, out column, out angle, out scale, out score);
	}

	/// <summary>使用强类型亚像素方式查找多个各向异性缩放模型。</summary>
	public static void FindAnisoShapeModels(this JlImage image, JlShapeModel modelIDs, double angleStart, double angleExtent, double scaleRMin, double scaleRMax, double scaleCMin, double scaleCMax, double minScore, int numMatches, double maxOverlap, JlSubPixelMode subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score, out JlTuple model)
	{
		image.FindAnisoShapeModels(modelIDs, angleStart, angleExtent, scaleRMin, scaleRMax, scaleCMin, scaleCMax, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, greediness, out row, out column, out angle, out scaleR, out scaleC, out score, out model);
	}

	/// <summary>使用强类型亚像素方式查找等比缩放模型。</summary>
	public static void FindScaledShapeModels(this JlImage image, JlShapeModel modelIDs, double angleStart, double angleExtent, double scaleMin, double scaleMax, double minScore, int numMatches, double maxOverlap, JlSubPixelMode subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score, out JlTuple model)
	{
		image.FindScaledShapeModels(modelIDs, angleStart, angleExtent, scaleMin, scaleMax, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, greediness, out row, out column, out angle, out scale, out score, out model);
	}

	/// <summary>使用强类型亚像素方式查找多个形状模型。</summary>
	public static void FindShapeModels(this JlImage image, JlShapeModel modelIDs, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, JlSubPixelMode subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		image.FindShapeModels(modelIDs, angleStart, angleExtent, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, greediness, out row, out column, out angle, out score, out model);
	}

	/// <summary>使用强类型亚像素方式查找单个各向异性缩放模型。</summary>
	public static void FindAnisoShapeModel(this JlImage image, JlShapeModel modelID, double angleStart, double angleExtent, double scaleRMin, double scaleRMax, double scaleCMin, double scaleCMax, double minScore, int numMatches, double maxOverlap, JlSubPixelMode subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scaleR, out JlTuple scaleC, out JlTuple score)
	{
		image.FindAnisoShapeModel(modelID, angleStart, angleExtent, scaleRMin, scaleRMax, scaleCMin, scaleCMax, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, greediness, out row, out column, out angle, out scaleR, out scaleC, out score);
	}

	/// <summary>使用强类型亚像素方式查找单个等比缩放模型。</summary>
	public static void FindScaledShapeModel(this JlImage image, JlShapeModel modelID, double angleStart, double angleExtent, double scaleMin, double scaleMax, double minScore, int numMatches, double maxOverlap, JlSubPixelMode subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple scale, out JlTuple score)
	{
		image.FindScaledShapeModel(modelID, angleStart, angleExtent, scaleMin, scaleMax, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, greediness, out row, out column, out angle, out scale, out score);
	}

	/// <summary>使用强类型亚像素方式查找单个形状模型。</summary>
	public static void FindShapeModel(this JlImage image, JlShapeModel modelID, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, JlSubPixelMode subPixel, int numLevels, double greediness, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		image.FindShapeModel(modelID, angleStart, angleExtent, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, greediness, out row, out column, out angle, out score);
	}

	/// <summary>使用强类型亚像素开关查找单个 NCC 模型。</summary>
	public static void FindNccModel(this JlImage image, JlNCCModel modelID, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, JlNccSubPixelMode subPixel, JlTuple numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		image.FindNccModel(modelID, angleStart, angleExtent, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, out row, out column, out angle, out score);
	}

	/// <summary>使用强类型亚像素开关查找单个 NCC 模型（标量层数重载）。</summary>
	public static void FindNccModel(this JlImage image, JlNCCModel modelID, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, JlNccSubPixelMode subPixel, int numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score)
	{
		image.FindNccModel(modelID, angleStart, angleExtent, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, out row, out column, out angle, out score);
	}

	/// <summary>使用强类型亚像素开关查找多个 NCC 模型。</summary>
	public static void FindNccModels(this JlImage image, JlNCCModel modelIDs, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, JlNccSubPixelMode subPixel, int numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple model)
	{
		image.FindNccModels(modelIDs, angleStart, angleExtent, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, out row, out column, out angle, out score, out model);
	}

	/// <summary>使用强类型亚像素开关执行单模型批量 NCC 查找。</summary>
	public static void FindNccModels(this JlNCCModel model, JlImage image, double angleStart, double angleExtent, double minScore, int numMatches, double maxOverlap, JlNccSubPixelMode subPixel, int numLevels, out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score, out JlTuple modelIndex)
	{
		model.FindNccModels(image, angleStart, angleExtent, minScore, numMatches, maxOverlap, subPixel.ToNative(), numLevels, out row, out column, out angle, out score, out modelIndex);
	}

	/// <summary>使用 true/false 选项执行 Sojka 兴趣点检测。</summary>
	public static void PointsSojka(this JlImage image, int maskSize, JlTuple sigmaW, JlTuple sigmaD, JlTuple minGrad, JlTuple minApparentness, double minAngle, JlBooleanOption subPixel, out JlTuple row, out JlTuple column)
	{
		image.PointsSojka(maskSize, sigmaW, sigmaD, minGrad, minApparentness, minAngle, subPixel.ToNative(), out row, out column);
	}

	/// <summary>使用 true/false 选项执行 Sojka 兴趣点检测（标量重载）。</summary>
	public static void PointsSojka(this JlImage image, int maskSize, double sigmaW, double sigmaD, double minGrad, double minApparentness, double minAngle, JlBooleanOption subPixel, out JlTuple row, out JlTuple column)
	{
		image.PointsSojka(maskSize, sigmaW, sigmaD, minGrad, minApparentness, minAngle, subPixel.ToNative(), out row, out column);
	}

	/// <summary>使用 on/off 选项执行 Harris 二项式兴趣点检测。</summary>
	public static void PointsHarrisBinomial(this JlImage image, int maskSizeGrad, int maskSizeSmooth, double alpha, JlTuple threshold, JlOnOffOption subPixel, out JlTuple row, out JlTuple column)
	{
		image.PointsHarrisBinomial(maskSizeGrad, maskSizeSmooth, alpha, threshold, subPixel.ToNative(), out row, out column);
	}

	/// <summary>使用 on/off 选项执行 Harris 二项式兴趣点检测（标量阈值重载）。</summary>
	public static void PointsHarrisBinomial(this JlImage image, int maskSizeGrad, int maskSizeSmooth, double alpha, double threshold, JlOnOffOption subPixel, out JlTuple row, out JlTuple column)
	{
		image.PointsHarrisBinomial(maskSizeGrad, maskSizeSmooth, alpha, threshold, subPixel.ToNative(), out row, out column);
	}

	/// <summary>使用强类型亚像素方式执行 Lepetit 兴趣点检测。</summary>
	public static void PointsLepetit(this JlImage image, int radius, int checkNeighbor, int minCheckNeighborDiff, int minScore, JlLepetitSubPixelMode subPixel, out JlTuple row, out JlTuple column)
	{
		image.PointsLepetit(radius, checkNeighbor, minCheckNeighborDiff, minScore, subPixel.ToNative(), out row, out column);
	}

	/// <summary>使用强类型插值和域选项执行投影图像变换。</summary>
	public static JlImage ProjectiveTransImageSize(this JlImage image, JlHomMat2D homMat2D, JlInterpolationMode interpolation, int width, int height, JlBooleanOption transformDomain)
	{
		return image.ProjectiveTransImageSize(homMat2D, interpolation.ToNative(), width, height, transformDomain.ToNative());
	}

	/// <summary>使用强类型插值、尺寸和域选项执行投影图像变换。</summary>
	public static JlImage ProjectiveTransImage(this JlImage image, JlHomMat2D homMat2D, JlInterpolationMode interpolation, JlBooleanOption adaptImageSize, JlBooleanOption transformDomain)
	{
		return image.ProjectiveTransImage(homMat2D, interpolation.ToNative(), adaptImageSize.ToNative(), transformDomain.ToNative());
	}

	/// <summary>使用强类型插值执行仿射图像变换。</summary>
	public static JlImage AffineTransImageSize(this JlImage image, JlHomMat2D homMat2D, JlInterpolationMode interpolation, int width, int height)
	{
		return image.AffineTransImageSize(homMat2D, interpolation.ToNative(), width, height);
	}

	/// <summary>使用强类型插值和画布选项执行仿射图像变换。</summary>
	public static JlImage AffineTransImage(this JlImage image, JlHomMat2D homMat2D, JlInterpolationMode interpolation, JlBooleanOption adaptImageSize)
	{
		return image.AffineTransImage(homMat2D, interpolation.ToNative(), adaptImageSize.ToNative());
	}

	/// <summary>使用 true/false 选项执行 Gamma 编解码。</summary>
	public static JlImage GammaImage(this JlImage image, double gamma, double offset, double threshold, JlTuple maxGray, JlBooleanOption encode)
	{
		return image.GammaImage(gamma, offset, threshold, maxGray, encode.ToNative());
	}

	/// <summary>使用 true/false 选项执行 Gamma 编解码（标量满量程重载）。</summary>
	public static JlImage GammaImage(this JlImage image, double gamma, double offset, double threshold, double maxGray, JlBooleanOption encode)
	{
		return image.GammaImage(gamma, offset, threshold, maxGray, encode.ToNative());
	}

	/// <summary>使用 true/false 选项执行高度场明暗渲染。</summary>
	public static JlImage ShadeHeightField(this JlImage image, JlTuple slant, JlTuple tilt, JlTuple albedo, JlTuple ambient, JlBooleanOption shadows)
	{
		return image.ShadeHeightField(slant, tilt, albedo, ambient, shadows.ToNative());
	}

	/// <summary>使用 true/false 选项执行高度场明暗渲染（标量重载）。</summary>
	public static JlImage ShadeHeightField(this JlImage image, double slant, double tilt, double albedo, double ambient, JlBooleanOption shadows)
	{
		return image.ShadeHeightField(slant, tilt, albedo, ambient, shadows.ToNative());
	}

	/// <summary>使用强类型 CFA 排列和插值方式去马赛克。</summary>
	public static JlImage CfaToRgb(this JlImage image, JlCfaPattern cfaPattern, JlInterpolationMode interpolation)
	{
		return image.CfaToRgb(cfaPattern.ToNative(), interpolation.ToNative());
	}

	/// <summary>使用强类型像素类型创建一通道图像。</summary>
	public static void GenImage1(this JlImage image, JlImageType type, int width, int height, System.IntPtr pixelPointer)
	{
		image.GenImage1(type.ToNative(), width, height, pixelPointer);
	}

	/// <summary>使用强类型像素类型创建三通道图像。</summary>
	public static void GenImage3(this JlImage image, JlImageType type, int width, int height, System.IntPtr pixelPointerRed, System.IntPtr pixelPointerGreen, System.IntPtr pixelPointerBlue)
	{
		image.GenImage3(type.ToNative(), width, height, pixelPointerRed, pixelPointerGreen, pixelPointerBlue);
	}

	/// <summary>使用强类型复制策略创建矩形图像。</summary>
	public static void GenImage1Rect(this JlImage image, System.IntPtr pixelPointer, int width, int height, int verticalPitch, int horizontalBitPitch, int bitsPerPixel, JlBooleanOption doCopy, System.IntPtr clearProc)
	{
		image.GenImage1Rect(pixelPointer, width, height, verticalPitch, horizontalBitPitch, bitsPerPixel, doCopy.ToNative(), clearProc);
	}
}
