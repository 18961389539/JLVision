using System;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;

namespace JLVisionLib.Validation;

/// <summary>
/// Honest sampled-residual map. Only grid cells containing independent held-out
/// measurements are colored. It never interpolates unknown error in untested areas.
/// </summary>
internal static class CalibrationHeatmapSvg
{
    private const int Width = 1050;
    private const int Height = 700;
    private const double PlotX = 100, PlotY = 120, PlotWidth = 600, PlotHeight = 480;

    public static string Render(CalibrationAuditResult audit, double holdoutLimitMm)
    {
        if (audit.Spatial is null)
            throw new ArgumentException("Heatmap requires an explicit imageRegion in calibration evidence.");
        if (!double.IsFinite(holdoutLimitMm) || holdoutLimitMm < 0)
            throw new ArgumentOutOfRangeException(nameof(holdoutLimitMm));

        CalibrationSpatialReport spatial = audit.Spatial;
        CalibrationImageRegion region = spatial.ImageRegion;
        int rows = spatial.GridRows, columns = spatial.GridColumns;
        double cellWidth = PlotWidth / columns, cellHeight = PlotHeight / rows;
        var sb = new StringBuilder();
        string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        string Escape(string text) => SecurityElement.Escape(text) ?? "";
        void Append(string value) => sb.AppendLine(value);
        double X(double column) => PlotX + (column - region.MinColumn) /
            (region.MaxColumn - region.MinColumn) * PlotWidth;
        double Y(double row) => PlotY + (row - region.MinRow) /
            (region.MaxRow - region.MinRow) * PlotHeight;

        Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {Width} {Height}\" role=\"img\" aria-labelledby=\"title desc\">");
        Append("<title id=\"title\">JLVision independent calibration residual coverage</title>");
        Append("<desc id=\"desc\">Colors indicate maximum observed independent validation residual in each populated grid cell; unmeasured cells are gray. This is not an interpolated error field.</desc>");
        Append($"<rect width=\"{Width}\" height=\"{Height}\" fill=\"#f8fafc\"/>");
        Append($"<text x=\"60\" y=\"54\" font-family=\"Segoe UI, sans-serif\" font-weight=\"700\" font-size=\"25\" fill=\"#0f172a\">Calibration residual / spatial coverage</text>");
        Append($"<text x=\"60\" y=\"82\" font-family=\"Segoe UI, sans-serif\" font-size=\"15\" fill=\"#475569\">ID: {Escape(audit.CalibrationId)} | sampled holdout residuals (mm), not extrapolated accuracy</text>");

        foreach (CalibrationCell cell in spatial.Cells)
        {
            double left = PlotX + cell.ColumnIndex * cellWidth;
            double top = PlotY + cell.RowIndex * cellHeight;
            bool observed = cell.HoldoutCount > 0;
            double value = cell.HoldoutMaxErrorMm ?? 0;
            string fill = !observed ? "#e2e8f0" :
                (value > holdoutLimitMm ? "#fecaca" :
                 value > holdoutLimitMm * .7 ? "#fed7aa" : "#bbf7d0");
            string title = observed
                ? $"holdout samples={cell.HoldoutCount}; max residual={N(value)} mm; fit samples={cell.FitCount}"
                : $"NO independent holdout samples; fit samples={cell.FitCount}";
            Append($"<rect x=\"{N(left)}\" y=\"{N(top)}\" width=\"{N(cellWidth)}\" height=\"{N(cellHeight)}\" fill=\"{fill}\" stroke=\"#94a3b8\" stroke-width=\"1.5\"><title>{Escape(title)}</title></rect>");
            Append($"<text x=\"{N(left + 12)}\" y=\"{N(top + 27)}\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" font-size=\"15\" fill=\"#1e293b\">{(observed ? N(value) + " mm" : "NO HOLDOUT")}</text>");
            Append($"<text x=\"{N(left + 12)}\" y=\"{N(top + 49)}\" font-family=\"Segoe UI, sans-serif\" font-size=\"13\" fill=\"#475569\">Fit {cell.FitCount}  /  Holdout {cell.HoldoutCount}</text>");
        }

        void Mark(CalibrationResidual[] samples, string prefix, string color, bool filled)
        {
            for (int i = 0; i < samples.Length; ++i)
            {
                var p = samples[i];
                double x = X(p.Column), y = Y(p.Row);
                string fill = filled ? color : "#ffffff";
                Append($"<circle cx=\"{N(x)}\" cy=\"{N(y)}\" r=\"6\" stroke=\"{color}\" stroke-width=\"2.8\" fill=\"{fill}\"><title>{prefix} #{i + 1}: row={N(p.Row)}, column={N(p.Column)}, dx={N(p.ErrorXmm)} mm, dy={N(p.ErrorYmm)} mm, radial={N(p.RadialErrorMm)} mm</title></circle>");
            }
        }
        Mark(audit.Fit.Residuals, "Fit", "#0284c7", false);
        Mark(audit.Holdout.Residuals, "Holdout", "#7c3aed", true);

        foreach (CalibrationOutlier outlier in spatial.Outliers)
        {
            Append($"<circle cx=\"{N(X(outlier.Column))}\" cy=\"{N(Y(outlier.Row))}\" r=\"11\" stroke=\"#dc2626\" stroke-width=\"3\" fill=\"none\"><title>{Escape(outlier.Population)} #{outlier.PointIndex + 1}: over limit {N(outlier.RadialErrorMm)} &gt; {N(outlier.LimitMm)} mm</title></circle>");
        }

        Append($"<rect x=\"{N(PlotX)}\" y=\"{N(PlotY)}\" width=\"{N(PlotWidth)}\" height=\"{N(PlotHeight)}\" stroke=\"#334155\" stroke-width=\"2\" fill=\"none\"/>");
        Append($"<text x=\"{N(PlotX)}\" y=\"{N(PlotY + PlotHeight + 28)}\" font-family=\"Segoe UI, sans-serif\" font-size=\"13\" fill=\"#334155\">column {N(region.MinColumn)} → {N(region.MaxColumn)} px</text>");
        Append($"<text x=\"{N(PlotX)}\" y=\"{N(PlotY - 12)}\" font-family=\"Segoe UI, sans-serif\" font-size=\"13\" fill=\"#334155\">row {N(region.MinRow)} → {N(region.MaxRow)} px (downwards)</text>");

        Append("<text x=\"745\" y=\"145\" font-family=\"Segoe UI, sans-serif\" font-size=\"19\" font-weight=\"700\" fill=\"#0f172a\">Measured coverage</text>");
        Append($"<text x=\"745\" y=\"180\" font-family=\"Segoe UI, sans-serif\" font-size=\"16\" fill=\"#334155\">Fit cells: {spatial.FitOccupiedCells}/9</text>");
        Append($"<text x=\"745\" y=\"208\" font-family=\"Segoe UI, sans-serif\" font-size=\"16\" fill=\"#334155\">Holdout cells: {spatial.HoldoutOccupiedCells}/9</text>");
        Append($"<text x=\"745\" y=\"236\" font-family=\"Segoe UI, sans-serif\" font-size=\"16\" fill=\"#334155\">Outliers: {spatial.Outliers.Length}</text>");
        Append($"<text x=\"745\" y=\"270\" font-family=\"Segoe UI, sans-serif\" font-size=\"15\" fill=\"#334155\">Holdout threshold: {N(holdoutLimitMm)} mm</text>");
        string[] legend = { "Untested (no holdout)", "Within limit (≤70%)", "Near limit (70–100%)", "Above limit" };
        string[] colors = { "#e2e8f0", "#bbf7d0", "#fed7aa", "#fecaca" };
        for (int i = 0; i < legend.Length; i++)
        {
            int y = 308 + i * 40;
            Append($"<rect x=\"745\" y=\"{y}\" width=\"22\" height=\"22\" fill=\"{colors[i]}\" stroke=\"#94a3b8\"/>");
            Append($"<text x=\"780\" y=\"{y + 17}\" font-family=\"Segoe UI, sans-serif\" font-size=\"13\" fill=\"#334155\">{Escape(legend[i])}</text>");
        }
        Append("<circle cx=\"754\" cy=\"512\" r=\"6\" stroke=\"#0284c7\" stroke-width=\"2.8\" fill=\"#fff\"/>");
        Append("<text x=\"780\" y=\"517\" font-family=\"Segoe UI, sans-serif\" font-size=\"14\" fill=\"#334155\">Fit point</text>");
        Append("<circle cx=\"754\" cy=\"545\" r=\"6\" stroke=\"#7c3aed\" stroke-width=\"2.8\" fill=\"#7c3aed\"/>");
        Append("<text x=\"780\" y=\"550\" font-family=\"Segoe UI, sans-serif\" font-size=\"14\" fill=\"#334155\">Independent holdout</text>");
        Append("<circle cx=\"754\" cy=\"580\" r=\"11\" stroke=\"#dc2626\" stroke-width=\"3\" fill=\"none\"/>");
        Append("<text x=\"780\" y=\"585\" font-family=\"Segoe UI, sans-serif\" font-size=\"14\" fill=\"#334155\">Residual outlier</text>");
        Append("<text x=\"60\" y=\"669\" font-family=\"Segoe UI, sans-serif\" font-size=\"14\" font-weight=\"600\" fill=\"#b91c1c\">GRAY = UNVERIFIED. No interpolation. Synthetic example data are not a real calibration certificate.</text>");
        Append("</svg>");
        return sb.ToString();
    }
}
