namespace CanvasCovers.Models.Products.LiftBlanket
{
    public static class WallChecks
    {
        // True when both walls are present and their total widths differ by more
        // than tolerance. Cars are square, so a mismatch is usually a data error
        // (often leftover from a previous order) — but it is only a WARNING: a
        // lift with one angled COP legitimately differs, so it must not block.
        public static bool WidthsMismatch(bool leftEnabled, double leftWidth,
            bool rightEnabled, double rightWidth, double toleranceMm = 1.0)
        {
            if (!leftEnabled || !rightEnabled) return false;
            if (leftWidth <= 0 || rightWidth <= 0) return false;
            return System.Math.Abs(leftWidth - rightWidth) > toleranceMm;
        }
    }
}
