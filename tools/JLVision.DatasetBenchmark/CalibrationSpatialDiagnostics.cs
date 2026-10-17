using System;
using System.Collections.Generic;
using System.Linq;

namespace JLVisionLib.Validation;

/// <summary>
/// Explicit measured image region of interest. This is NOT inferred from the
/// calibration points because the point bounding box may omit untested edges.
/// </summary>
internal sealed class CalibrationImageRegion
{
    public double MinRow { get; set; }
    public double MaxRow { get; set; }
    public double MinColumn { get; set; }
    public double MaxColumn { get; set; }

    public void Validate()
    {
        if (!new[] { MinRow, MaxRow, MinColumn, MaxColumn }.All(double.IsFinite) ||
            MaxRow <= MinRow || MaxColumn <= MinColumn)
            throw new ArgumentException("Image region must have finite, positive row and column spans.");
    }

    public void CheckContains(CalibrationPoint point)
    {
        if (point.Row < MinRow || point.Row > MaxRow ||
            point.Column < MinColumn || point.Column > MaxColumn)
            throw new ArgumentException("Calibration point lies outside the declared image region.");
    }
}

internal sealed record CalibrationCell(
    int RowIndex, int ColumnIndex, int FitCount, int HoldoutCount,
    double? FitMaxErrorMm, double? HoldoutMaxErrorMm);

internal sealed record CalibrationOutlier(
    string Population, int PointIndex, double Row, double Column,
    double ErrorXmm, double ErrorYmm, double RadialErrorMm, double LimitMm,
    string Reason);

internal sealed record CalibrationSpatialReport(
    CalibrationImageRegion ImageRegion, int GridRows, int GridColumns,
    int FitOccupiedCells, int HoldoutOccupiedCells, CalibrationCell[] Cells,
    CalibrationOutlier[] Outliers);

internal static class CalibrationSpatialDiagnostics
{
    // Fixed grid for consistent visualization across runs and calibration versions.
    public const int GridSize = 3;

    public static CalibrationSpatialReport Analyze(
        CalibrationImageRegion region,
        IReadOnlyList<CalibrationPoint> fitPoints,
        IReadOnlyList<CalibrationPoint> holdoutPoints,
        CalibrationResidualSummary fit, CalibrationResidualSummary holdout,
        double maxFitErrorMm, double maxHoldoutErrorMm)
    {
        if (region is null) throw new ArgumentNullException(nameof(region));
        region.Validate();
        if (fitPoints.Count != fit.Residuals.Length ||
            holdoutPoints.Count != holdout.Residuals.Length)
            throw new ArgumentException("Residual data and calibration points must be aligned.");

        var cells = new CalibrationCell[GridSize * GridSize];
        var fitCounts = new int[cells.Length];
        var holdoutCounts = new int[cells.Length];
        var fitMax = new double?[cells.Length];
        var holdoutMax = new double?[cells.Length];
        var outliers = new List<CalibrationOutlier>();

        void Gather(string population, IReadOnlyList<CalibrationPoint> points,
            CalibrationResidual[] residuals, int[] counts, double?[] maximum, double limit)
        {
            for (int i = 0; i < points.Count; ++i)
            {
                CalibrationPoint point = points[i];
                region.CheckContains(point);
                int gridRow = Math.Min(GridSize - 1,
                    (int)((point.Row - region.MinRow) / (region.MaxRow - region.MinRow) * GridSize));
                int gridColumn = Math.Min(GridSize - 1,
                    (int)((point.Column - region.MinColumn) / (region.MaxColumn - region.MinColumn) * GridSize));
                int index = gridRow * GridSize + gridColumn;
                CalibrationResidual residual = residuals[i];
                counts[index]++;
                maximum[index] = Math.Max(maximum[index] ?? 0, residual.RadialErrorMm);
                if (residual.RadialErrorMm > limit)
                    outliers.Add(new CalibrationOutlier(population, i, point.Row, point.Column,
                        residual.ErrorXmm, residual.ErrorYmm, residual.RadialErrorMm,
                        limit, "exceeds configured radial residual limit"));
            }
        }

        Gather("fit", fitPoints, fit.Residuals, fitCounts, fitMax, maxFitErrorMm);
        Gather("holdout", holdoutPoints, holdout.Residuals, holdoutCounts, holdoutMax, maxHoldoutErrorMm);

        for (int row = 0; row < GridSize; ++row)
        for (int col = 0; col < GridSize; ++col)
        {
            int index = row * GridSize + col;
            cells[index] = new CalibrationCell(row, col, fitCounts[index], holdoutCounts[index],
                fitMax[index], holdoutMax[index]);
        }
        return new CalibrationSpatialReport(region, GridSize, GridSize,
            fitCounts.Count(n => n > 0), holdoutCounts.Count(n => n > 0),
            cells, outliers.ToArray());
    }
}
