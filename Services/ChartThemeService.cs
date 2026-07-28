#region Namespaces

using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Creates accessible, semantic chart palettes for light and dark surfaces.
    /// </summary>
    public sealed class ChartThemeService
    {
        #region Constructors

        /// <summary>
        /// Initializes a chart theme service that uses the light palette by default.
        /// </summary>
        public ChartThemeService()
        {
            CurrentPalette = CreateLightPalette();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the palette selected by the most recent theme change.
        /// </summary>
        public ChartThemePalette CurrentPalette { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Selects and returns the palette for the requested application theme.
        /// </summary>
        /// <param name="isDarkTheme">True to use colors designed for dark surfaces.</param>
        /// <returns>The newly selected immutable palette.</returns>
        public ChartThemePalette UseTheme(bool isDarkTheme)
        {
            CurrentPalette = isDarkTheme
                ? CreateDarkPalette()
                : CreateLightPalette();

            return CurrentPalette;
        }

        /// <summary>
        /// Creates a palette for the requested application theme without changing the current palette.
        /// </summary>
        /// <param name="isDarkTheme">True to create colors designed for dark surfaces.</param>
        /// <returns>An immutable chart palette.</returns>
        public ChartThemePalette CreatePalette(bool isDarkTheme)
        {
            return isDarkTheme
                ? CreateDarkPalette()
                : CreateLightPalette();
        }

        #endregion

        #region Palette Factories

        /// <summary>
        /// Creates the application chart palette for light surfaces.
        /// </summary>
        /// <returns>An immutable light chart palette.</returns>
        public static ChartThemePalette CreateLightPalette()
        {
            return new ChartThemePalette(
                new SKColor(31, 122, 71),
                new SKColor(198, 52, 58),
                new SKColor(218, 139, 28),
                new SKColor(0, 137, 167),
                new SKColor(49, 82, 198),
                new SKColor(35, 43, 52),
                new SKColor(91, 103, 116),
                new SKColor(207, 216, 224),
                new SKColor(255, 255, 255));
        }

        /// <summary>
        /// Creates the application chart palette for dark surfaces.
        /// </summary>
        /// <returns>An immutable dark chart palette.</returns>
        public static ChartThemePalette CreateDarkPalette()
        {
            return new ChartThemePalette(
                new SKColor(70, 200, 123),
                new SKColor(255, 103, 110),
                new SKColor(255, 190, 80),
                new SKColor(74, 210, 234),
                new SKColor(126, 158, 255),
                new SKColor(244, 247, 250),
                new SKColor(180, 194, 207),
                new SKColor(62, 82, 100),
                new SKColor(17, 29, 41));
        }

        #endregion
    }

    /// <summary>
    /// Holds semantic colors and creates independent LiveCharts paints for one theme.
    /// </summary>
    public sealed class ChartThemePalette
    {
        #region Constructors

        /// <summary>
        /// Initializes an immutable chart palette.
        /// </summary>
        /// <param name="good">Color for GOOD and correct values.</param>
        /// <param name="ng">Color for NG and wrong values.</param>
        /// <param name="pending">Color for pending review values.</param>
        /// <param name="activity">Primary activity color.</param>
        /// <param name="activitySecondary">Secondary activity color.</param>
        /// <param name="text">High-emphasis text color.</param>
        /// <param name="mutedText">Secondary text color.</param>
        /// <param name="grid">Grid and separator color.</param>
        /// <param name="surface">Tooltip surface color.</param>
        public ChartThemePalette(
            SKColor good,
            SKColor ng,
            SKColor pending,
            SKColor activity,
            SKColor activitySecondary,
            SKColor text,
            SKColor mutedText,
            SKColor grid,
            SKColor surface)
        {
            Good = good;
            Ng = ng;
            Pending = pending;
            Activity = activity;
            ActivitySecondary = activitySecondary;
            Text = text;
            MutedText = mutedText;
            Grid = grid;
            Surface = surface;
        }

        #endregion

        #region Colors

        /// <summary>
        /// Gets the semantic GOOD and correct color.
        /// </summary>
        public SKColor Good { get; private set; }

        /// <summary>
        /// Gets the semantic NG and wrong color.
        /// </summary>
        public SKColor Ng { get; private set; }

        /// <summary>
        /// Gets the pending review color.
        /// </summary>
        public SKColor Pending { get; private set; }

        /// <summary>
        /// Gets the primary activity color.
        /// </summary>
        public SKColor Activity { get; private set; }

        /// <summary>
        /// Gets the secondary activity color.
        /// </summary>
        public SKColor ActivitySecondary { get; private set; }

        /// <summary>
        /// Gets the high-emphasis text color.
        /// </summary>
        public SKColor Text { get; private set; }

        /// <summary>
        /// Gets the secondary text color.
        /// </summary>
        public SKColor MutedText { get; private set; }

        /// <summary>
        /// Gets the axis grid and separator color.
        /// </summary>
        public SKColor Grid { get; private set; }

        /// <summary>
        /// Gets the chart tooltip surface color.
        /// </summary>
        public SKColor Surface { get; private set; }

        #endregion

        #region Paint Factories

        /// <summary>
        /// Creates an independent solid paint with optional opacity and stroke width.
        /// </summary>
        /// <param name="color">The source color.</param>
        /// <param name="opacity">Opacity from zero through one.</param>
        /// <param name="strokeThickness">The paint stroke width.</param>
        /// <returns>A new LiveCharts paint instance.</returns>
        public SolidColorPaint CreatePaint(
            SKColor color,
            double opacity = 1d,
            float strokeThickness = 0f)
        {
            double boundedOpacity = opacity;

            if (boundedOpacity < 0d)
            {
                boundedOpacity = 0d;
            }
            else if (boundedOpacity > 1d)
            {
                boundedOpacity = 1d;
            }

            byte alpha = (byte)(255d * boundedOpacity);
            return new SolidColorPaint(color.WithAlpha(alpha), strokeThickness);
        }

        #endregion
    }
}
