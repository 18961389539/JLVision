using System;
using System.Collections.Generic;
using System.Linq;

namespace JLVisionLib.Validation;

/// <summary>
/// Unmeasured pixel locations for a commissioning engineer to measure next.
/// Suggestions are not calibration points and never contribute to pass/fail.
/// </summary>
internal sealed record CalibrationNextPoint(
    int GridRow, int GridColumn, double Row, double Column,
    int ExistingFitCount, int ExistingHoldoutCount, string Reason);

internal static class CalibrationFollowUpPlanner
{
    public static CalibrationNextPoint[] Suggest(CalibrationSpatialReport? spatial,
        IReadOnlyCollection<CalibrationPoint> fitPoints,
        IReadOnlyCollection<CalibrationPoint> holdoutPoints, int maximum = 9)
    {
        if (spatial is null) return Array.Empty<CalibrationNextPoint>();
        if (maximum < 0 || maximum > 9)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        if (maximum == 0) return Array.Empty<CalibrationNextPoint>();
        CalibrationImageRegion region = spatial.ImageRegion;
        region.Validate();
        var existing = fitPoints.Concat(holdoutPoints).Select(p => (p.Row, p.Column)).ToList();
        var planned = new List<CalibrationNextPoint>();
        // The most valuable next holdout samples are in cells never checked
        // independently. Sparse cells follow once all empty cells are covered.
        foreach (CalibrationCell cell in spatial.Cells
            .OrderBy(x => x.HoldoutCount == 0 ? 0 : 1)
            .ThenBy(x => x.HoldoutCount)
            .ThenBy(x => x.FitCount)
            .ThenBy(x => x.RowIndex)
            .ThenBy(x => x.ColumnIndex))
        {
            if (planned.Count == maximum) break;
            double cellH = (region.MaxRow - region.MinRow) / spatial.GridRows;
            double cellW = (region.MaxColumn - region.MinColumn) / spatial.GridColumns;
            bool added = false;
            foreach (var (fractionRow, fractionCol) in new (double, double)[]
            {
                (.5, .5), (.33, .33), (.67, .67), (.33, .67), (.67, .33)
            })
            {
                double row = region.MinRow + (cell.RowIndex + fractionRow) * cellH;
                double column = region.MinColumn + (cell.ColumnIndex + fractionCol) * cellW;
                // Avoid suggesting already used image locations or points too
                // close (<1 pixel) to a known correspondence.
                if (existing.Any(p =>
                    (p.Row - row) * (p.Row - row) +
                    (p.Column - column) * (p.Column - column) < 1.0))
                    continue;
                planned.Add(new CalibrationNextPoint(cell.RowIndex, cell.ColumnIndex,
                    row, column, cell.FitCount, cell.HoldoutCount,
                    cell.HoldoutCount == 0 ? "unverified holdout grid cell" :
                    "sparse holdout coverage; add an independent measurement"));
                existing.Add((row, column));
                added = true;
                break;
            }
            // In very small ROIs there may be no distinct, suitable location.
            // Do not invent an independent measurement in such a cell.
            if (!added) continue;
        }
        return planned.ToArray();
    }
}
