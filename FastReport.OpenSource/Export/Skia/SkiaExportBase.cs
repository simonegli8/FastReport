using System.Drawing;
using FastReport.Utils;
using SkiaSharp;

namespace FastReport.Export.Skia
{
    /// <summary>
    /// Base class for exports that draw prepared pages onto a Skia canvas through <see cref="SkiaGraphics"/>,
    /// the same way <see cref="Image.ImageExport"/> draws them onto a bitmap. Vector targets (SVG, PDF)
    /// keep text and shapes as vectors.
    /// </summary>
    public abstract class SkiaExportBase : ExportBase
    {
        // Page border size conversion, as in ImageExport.
        private const float Divider = 0.75f;
        private const float PageDivider = 2.8346400000000003f; // mm to point

        private SkiaGraphics graphics;
        private IGraphicsState pageState;
        private float pageWidth;
        private float pageHeight;

        /// <summary>
        /// Canvas units per FastReport pixel (FastReport lays pages out at 96 pixels per inch).
        /// </summary>
        protected abstract float CanvasUnitsPerPixel { get; }

        /// <summary>Starts a page of the given size in canvas units and returns the canvas to draw it on.</summary>
        protected abstract SKCanvas BeginCanvas(ReportPage page, float width, float height);

        /// <summary>Completes the page started by <see cref="BeginCanvas"/>.</summary>
        protected abstract void EndCanvas(ReportPage page);

        /// <summary>The graphics of the page being exported.</summary>
        protected SkiaGraphics Graphics => graphics;

        /// <inheritdoc/>
        protected override void ExportPageBegin(ReportPage page)
        {
            base.ExportPageBegin(page);
            float scale = CanvasUnitsPerPixel;
            pageWidth = ExportUtils.GetPageWidth(page) * Units.Millimeters * scale;
            pageHeight = ExportUtils.GetPageHeight(page) * Units.Millimeters * scale;

            var canvas = BeginCanvas(page, pageWidth, pageHeight);
            // FastReport converts font sizes through the graphics DPI, which therefore stays at 96;
            // the canvas units are reached through the FRPaintEventArgs scale.
            graphics = SkiaGraphics.FromCanvas(canvas, 96f, 96f);
            pageState = graphics.Save();

            page.Fill.Draw(new FRPaintEventArgs(graphics, 1, 1, Report.GraphicCache), new RectangleF(0, 0, pageWidth, pageHeight));
            graphics.TranslateTransform(page.LeftMargin * Units.Millimeters * scale, page.TopMargin * Units.Millimeters * scale);

            if (page.Watermark.Enabled && !page.Watermark.ShowImageOnTop)
                DrawImageWatermark(page);
            if (page.Watermark.Enabled && !page.Watermark.ShowTextOnTop)
                DrawTextWatermark(page);

            if (page.Border.Lines != BorderLines.None)
            {
                using (TextObject pageBorder = new TextObject())
                {
                    pageBorder.Border = page.Border;
                    pageBorder.Left = 0;
                    pageBorder.Top = 0;
                    pageBorder.Width = (ExportUtils.GetPageWidth(page) - page.LeftMargin - page.RightMargin) * PageDivider / Divider;
                    pageBorder.Height = (ExportUtils.GetPageHeight(page) - page.TopMargin - page.BottomMargin) * PageDivider / Divider;
                    ExportObject(pageBorder);
                }
            }
        }

        /// <inheritdoc/>
        protected override void ExportBand(BandBase band)
        {
            base.ExportBand(band);
            ExportObject(band);
            foreach (Base obj in band.ForEachAllConvectedObjects(this))
            {
                if (!(obj is Table.TableColumn || obj is Table.TableCell || obj is Table.TableRow))
                    ExportObject(obj);
            }
        }

        /// <inheritdoc/>
        protected override void ExportPageEnd(ReportPage page)
        {
            if (page.Watermark.Enabled && page.Watermark.ShowImageOnTop)
                DrawImageWatermark(page);
            if (page.Watermark.Enabled && page.Watermark.ShowTextOnTop)
                DrawTextWatermark(page);

            graphics.Restore(pageState);
            graphics.Dispose();
            graphics = null;
            EndCanvas(page);
            base.ExportPageEnd(page);
        }

        /// <summary>Called after a report component has been drawn, e.g. to add link annotations.</summary>
        /// <param name="component">The component.</param>
        /// <param name="bounds">The component bounds in the current canvas coordinates.</param>
        protected virtual void OnComponentExported(ReportComponentBase component, SKRect bounds)
        {
        }

        private void ExportObject(Base obj)
        {
            if (obj is ReportComponentBase component && component.Exportable)
            {
                float scale = CanvasUnitsPerPixel;
                component.Draw(new FRPaintEventArgs(graphics, scale, scale, Report.GraphicCache));
                OnComponentExported(component, SKRect.Create(
                    component.AbsLeft * scale, component.AbsTop * scale, component.Width * scale, component.Height * scale));
            }
        }

        private RectangleF WatermarkBounds(ReportPage page)
        {
            float scale = CanvasUnitsPerPixel;
            return new RectangleF(-page.LeftMargin * Units.Millimeters, -page.TopMargin * Units.Millimeters, pageWidth / scale, pageHeight / scale);
        }

        private void DrawImageWatermark(ReportPage page)
        {
            float scale = CanvasUnitsPerPixel;
            page.Watermark.DrawImage(new FRPaintEventArgs(graphics, scale, scale, Report.GraphicCache), WatermarkBounds(page), page.Report, false);
        }

        private void DrawTextWatermark(ReportPage page)
        {
            if (string.IsNullOrEmpty(page.Watermark.Text))
                return;
            float scale = CanvasUnitsPerPixel;
            page.Watermark.DrawText(new FRPaintEventArgs(graphics, scale, scale, Report.GraphicCache), WatermarkBounds(page), page.Report, false);
        }
    }
}
