using System;
using SkiaSharp;

namespace FastReport.Export.Skia
{
    /// <summary>
    /// Exports a report to a vector PDF rendered by Skia: text stays selectable and searchable, fonts are
    /// embedded (subset), shapes stay vectors, and URL hyperlinks become link annotations.
    /// </summary>
    /// <remarks>
    /// Unlike PDFSimpleExport, which embeds every page as an image, this produces small, sharp documents.
    /// </remarks>
    public class SkiaPdfExport : SkiaExportBase
    {
        // PDF units are points (1/72 inch); FastReport lays pages out at 96 pixels per inch.
        private const float PointsPerPixel = 72f / 96f;

        private SKDocument document;

        /// <summary>Gets or sets the document title. Defaults to the report name.</summary>
        public string Title { get; set; }

        /// <summary>Gets or sets the document author. Defaults to the report author.</summary>
        public string Author { get; set; }

        /// <summary>Gets or sets the document subject. Defaults to the report description.</summary>
        public string Subject { get; set; }

        /// <summary>Gets or sets the document keywords.</summary>
        public string Keywords { get; set; }

        /// <summary>Gets or sets the resolution used for content that has to be rasterized. Default is 300.</summary>
        public float RasterDpi { get; set; } = 300f;

        /// <summary>Gets or sets whether URL hyperlinks are exported as clickable link annotations. Default is true.</summary>
        public bool ExportHyperlinks { get; set; } = true;

        /// <inheritdoc/>
        protected override float CanvasUnitsPerPixel => PointsPerPixel;

        /// <inheritdoc/>
        protected override string GetFileFilter() => "PDF document (*.pdf)|*.pdf";

        /// <inheritdoc/>
        protected override void Start()
        {
            base.Start();
            var metadata = SKDocumentPdfMetadata.Default;
            metadata.Title = Title ?? Report.ReportInfo.Name ?? string.Empty;
            metadata.Author = Author ?? Report.ReportInfo.Author ?? string.Empty;
            metadata.Subject = Subject ?? Report.ReportInfo.Description ?? string.Empty;
            metadata.Keywords = Keywords ?? string.Empty;
            metadata.Creator = "FastReport";
            metadata.Producer = "FastReport.Drawing.Skia (SkiaSharp)";
            metadata.Creation = DateTime.Now;
            metadata.Modified = DateTime.Now;
            metadata.RasterDpi = RasterDpi;
            document = SKDocument.CreatePdf(Stream, metadata);
        }

        /// <inheritdoc/>
        protected override SKCanvas BeginCanvas(ReportPage page, float width, float height) =>
            document.BeginPage(width, height);

        /// <inheritdoc/>
        protected override void EndCanvas(ReportPage page) => document.EndPage();

        /// <inheritdoc/>
        protected override void OnComponentExported(ReportComponentBase component, SKRect bounds)
        {
            if (ExportHyperlinks && component.Hyperlink.Kind == HyperlinkKind.URL && !string.IsNullOrEmpty(component.Hyperlink.Value))
                Graphics.Canvas.DrawUrlAnnotation(bounds, component.Hyperlink.Value);
        }

        /// <inheritdoc/>
        protected override void Finish()
        {
            document.Close();
            document.Dispose();
            document = null;
            base.Finish();
        }
    }
}
