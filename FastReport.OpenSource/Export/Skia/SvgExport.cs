using System;
using System.Collections.Generic;
using SkiaSharp;

namespace FastReport.Export.Skia
{
    /// <summary>
    /// Exports a report to a single SVG document, rendered by Skia. Several pages are stacked vertically.
    /// </summary>
    /// <example>
    /// <code>
    /// using (var export = new SvgExport { PageRange = PageRange.PageNumbers, PageNumbers = "1" })
    ///     report.Export(export, "page1.svg");
    /// </code>
    /// </example>
    public class SvgExport : SkiaExportBase
    {
        private readonly List<RecordedPage> pages = new List<RecordedPage>();
        private SKPictureRecorder recorder;
        private SKRect currentBounds;

        /// <summary>Gets or sets the scale of the SVG relative to 96 DPI. Default is 1.</summary>
        public float Zoom { get; set; } = 1f;

        /// <summary>Gets or sets the vertical gap between stacked pages, in SVG units. Default is 10.</summary>
        public float PageGap { get; set; } = 10f;

        /// <inheritdoc/>
        protected override float CanvasUnitsPerPixel => Zoom;

        /// <inheritdoc/>
        protected override string GetFileFilter() => "SVG image (*.svg)|*.svg";

        /// <inheritdoc/>
        protected override void Start()
        {
            base.Start();
            DisposePages();
        }

        /// <inheritdoc/>
        protected override SKCanvas BeginCanvas(ReportPage page, float width, float height)
        {
            // Pages are recorded first: the SVG canvas needs the total size of all pages up front.
            currentBounds = new SKRect(0, 0, width, height);
            recorder = new SKPictureRecorder();
            return recorder.BeginRecording(currentBounds);
        }

        /// <inheritdoc/>
        protected override void EndCanvas(ReportPage page)
        {
            pages.Add(new RecordedPage(recorder.EndRecording(), currentBounds));
            recorder.Dispose();
            recorder = null;
        }

        /// <inheritdoc/>
        protected override void Finish()
        {
            float width = 0, height = 0;
            foreach (var page in pages)
            {
                width = Math.Max(width, page.Bounds.Width);
                height += page.Bounds.Height;
            }
            if (pages.Count > 1)
                height += PageGap * (pages.Count - 1);

            using (var canvas = SKSvgCanvas.Create(new SKRect(0, 0, width, height), Stream))
            {
                float y = 0;
                foreach (var page in pages)
                {
                    canvas.Save();
                    canvas.Translate(0, y);
                    canvas.DrawPicture(page.Picture);
                    canvas.Restore();
                    y += page.Bounds.Height + PageGap;
                }
            }

            DisposePages();
            base.Finish();
        }

        private void DisposePages()
        {
            foreach (var page in pages)
                page.Picture.Dispose();
            pages.Clear();
        }

        private readonly struct RecordedPage
        {
            public RecordedPage(SKPicture picture, SKRect bounds)
            {
                Picture = picture;
                Bounds = bounds;
            }

            public SKPicture Picture { get; }

            public SKRect Bounds { get; }
        }
    }
}
