using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;
using FastReport.Export.Skia;

namespace FastReport.WebForms
{
    /// <summary>How the <see cref="ReportViewer"/> shows the pages of a report.</summary>
    public enum ReportViewMode
    {
        /// <summary>One page at a time, with page navigation in the toolbar.</summary>
        SinglePage,

        /// <summary>All pages below each other.</summary>
        AllPages,
    }

    /// <summary>Provides the report that is about to be prepared, e.g. to register data sources.</summary>
    public class ReportPreparingEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ReportPreparingEventArgs"/> class.</summary>
        public ReportPreparingEventArgs(Report report)
        {
            Report = report;
        }

        /// <summary>The report that is about to be prepared.</summary>
        public Report Report { get; }
    }

    /// <summary>
    /// A Web Forms control that renders a FastReport report as inline SVG and offers it as a vector PDF download.
    /// </summary>
    /// <remarks>
    /// <para>Register it in a page and point it at a report:</para>
    /// <code>
    /// &lt;%@ Register Assembly="FastReport.WebForms" Namespace="FastReport.WebForms" TagPrefix="fr" %&gt;
    /// &lt;fr:ReportViewer ID="Viewer" runat="server" ReportFile="~/App_Data/report.frx" OnReportPreparing="Viewer_ReportPreparing" /&gt;
    /// </code>
    /// <para>
    /// Page navigation and the PDF download are plain GET links back to the hosting page (query string keys
    /// <c>{ClientID}_page</c> and <c>fr_pdf</c>), so no HTTP handler registration is needed and the control
    /// works with or without a server-side form. The report is prepared on each request that needs it.
    /// </para>
    /// <para>
    /// The control derives from <see cref="UserControl"/> rather than <c>WebControl</c>: its rendering goes through
    /// FastReport.Drawing.Skia, whose System.Drawing types would clash with the GDI+ ones in WebControl's API.
    /// </para>
    /// </remarks>
    [ToolboxData("<{0}:ReportViewer runat=\"server\" />")]
    [ParseChildren(true)]
    [PersistChildren(false)]
    public class ReportViewer : UserControl
    {
        private const string PdfQueryKey = "fr_pdf";
        private const string StylesRenderedKey = "FastReport.WebForms.ReportViewer.Styles";

        private Report preparedReport;
        private bool ownsPreparedReport;

        /// <summary>
        /// Gets or sets the report file (.frx). App-relative ("~/App_Data/report.frx") and relative paths are mapped
        /// with <see cref="HttpServerUtility.MapPath(string)"/>. Ignored when <see cref="Report"/> is set.
        /// </summary>
        [Category("Report")]
        [UrlProperty]
        public string ReportFile { get; set; }

        /// <summary>
        /// Gets or sets the report to show. When it is already prepared it is shown as is; otherwise it is prepared
        /// after <see cref="ReportPreparing"/>. The control does not dispose a report assigned here.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Report Report { get; set; }

        /// <summary>Gets or sets the scale of the rendered pages (1 = 96 DPI). Default is 1.</summary>
        [Category("Appearance")]
        [DefaultValue(1f)]
        public float Zoom { get; set; } = 1f;

        /// <summary>Gets or sets whether one page or all pages are shown. Default is <see cref="ReportViewMode.SinglePage"/>.</summary>
        [Category("Appearance")]
        [DefaultValue(ReportViewMode.SinglePage)]
        public ReportViewMode ViewMode { get; set; } = ReportViewMode.SinglePage;

        /// <summary>Gets or sets whether the toolbar (navigation and PDF download) is shown. Default is true.</summary>
        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowToolbar { get; set; } = true;

        /// <summary>Gets or sets whether the toolbar offers the PDF download. Default is true.</summary>
        [Category("Behavior")]
        [DefaultValue(true)]
        public bool ShowPdfButton { get; set; } = true;

        /// <summary>Gets or sets the downloaded file name. Defaults to the report file name with a .pdf extension.</summary>
        [Category("Behavior")]
        public string PdfFileName { get; set; }

        /// <summary>Gets or sets whether the PDF opens in the browser instead of being downloaded. Default is false.</summary>
        [Category("Behavior")]
        [DefaultValue(false)]
        public bool PdfInline { get; set; }

        /// <summary>Gets or sets additional CSS classes for the outer element.</summary>
        [Category("Appearance")]
        public string CssClass { get; set; }

        /// <summary>Gets or sets whether the default stylesheet is emitted (once per page). Default is true.</summary>
        [Category("Appearance")]
        [DefaultValue(true)]
        public bool IncludeDefaultStyles { get; set; } = true;

        /// <summary>Occurs before the report is prepared; use it to register data or set parameters.</summary>
        public event EventHandler<ReportPreparingEventArgs> ReportPreparing;

        /// <summary>Gets the zero-based index of the page shown in <see cref="ReportViewMode.SinglePage"/> mode.</summary>
        [Browsable(false)]
        public int PageIndex
        {
            get
            {
                string value = Context?.Request.QueryString[PageQueryKey];
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int page) && page > 0 ? page - 1 : 0;
            }
        }

        private string PageQueryKey => ClientID + "_page";

        /// <summary>Loads (if needed), prepares and returns the report shown by this control.</summary>
        public Report GetPreparedReport()
        {
            if (preparedReport != null)
                return preparedReport;

            Report report = Report;
            bool owned = false;
            if (report == null)
            {
                if (string.IsNullOrEmpty(ReportFile))
                    throw new InvalidOperationException("Set ReportFile or Report on the ReportViewer.");
                report = new Report();
                owned = true;
                report.Load(MapReportPath(ReportFile));
            }

            try
            {
                if (report.PreparedPages == null || report.PreparedPages.Count == 0)
                {
                    ReportPreparing?.Invoke(this, new ReportPreparingEventArgs(report));
                    report.Prepare();
                }
            }
            catch
            {
                if (owned)
                    report.Dispose();
                throw;
            }

            preparedReport = report;
            ownsPreparedReport = owned;
            return report;
        }

        /// <summary>Renders one prepared page as a standalone SVG document.</summary>
        public string RenderPageSvg(int pageIndex)
        {
            Report report = GetPreparedReport();
            using (var stream = new MemoryStream())
            using (var export = new SvgExport { Zoom = Zoom, PageRange = PageRange.PageNumbers, PageNumbers = (pageIndex + 1).ToString(CultureInfo.InvariantCulture) })
            {
                report.Export(export, stream);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>Exports the whole report as a vector PDF.</summary>
        public byte[] RenderPdf()
        {
            Report report = GetPreparedReport();
            using (var stream = new MemoryStream())
            using (var export = new SkiaPdfExport())
            {
                report.Export(export, stream);
                return stream.ToArray();
            }
        }

        /// <inheritdoc/>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (string.Equals(Request.QueryString[PdfQueryKey], ClientID, StringComparison.Ordinal))
                SendPdf();
        }

        /// <inheritdoc/>
        protected override void Render(HtmlTextWriter writer)
        {
            if (IncludeDefaultStyles && Page?.Items[StylesRenderedKey] == null)
            {
                writer.Write(DefaultStyles);
                if (Page != null)
                    Page.Items[StylesRenderedKey] = true;
            }

            writer.AddAttribute(HtmlTextWriterAttribute.Id, ClientID);
            writer.AddAttribute(HtmlTextWriterAttribute.Class, string.IsNullOrEmpty(CssClass) ? "fr-viewer" : "fr-viewer " + CssClass);
            writer.RenderBeginTag(HtmlTextWriterTag.Div);
            try
            {
                Report report = GetPreparedReport();
                int pageCount = report.PreparedPages.Count;
                if (pageCount == 0)
                {
                    RenderMessage(writer, "fr-message", "The report has no pages.");
                    return;
                }

                int pageIndex = Math.Min(Math.Max(PageIndex, 0), pageCount - 1);
                if (ShowToolbar)
                    RenderToolbar(writer, pageIndex, pageCount);

                int first = ViewMode == ReportViewMode.AllPages ? 0 : pageIndex;
                int last = ViewMode == ReportViewMode.AllPages ? pageCount - 1 : pageIndex;
                for (int i = first; i <= last; i++)
                {
                    writer.AddAttribute(HtmlTextWriterAttribute.Class, "fr-page");
                    writer.AddAttribute("data-page", (i + 1).ToString(CultureInfo.InvariantCulture));
                    writer.RenderBeginTag(HtmlTextWriterTag.Div);
                    writer.Write(SvgInliner.Inline(RenderPageSvg(i), ClientID + "_p" + i.ToString(CultureInfo.InvariantCulture) + "_", "fr-page-svg"));
                    writer.RenderEndTag();
                }
            }
            catch (Exception ex)
            {
                RenderMessage(writer, "fr-error", "The report could not be shown: " + ex.Message);
            }
            finally
            {
                writer.RenderEndTag();
            }
        }

        /// <inheritdoc/>
        protected override void OnUnload(EventArgs e)
        {
            if (ownsPreparedReport)
                preparedReport?.Dispose();
            preparedReport = null;
            base.OnUnload(e);
        }

        private void SendPdf()
        {
            byte[] pdf = RenderPdf();
            string fileName = GetPdfFileName();

            HttpResponse response = Response;
            response.Clear();
            response.ContentType = "application/pdf";
            response.AddHeader("Content-Disposition", ContentDisposition(PdfInline ? "inline" : "attachment", fileName));
            response.AddHeader("Content-Length", pdf.Length.ToString(CultureInfo.InvariantCulture));
            response.BinaryWrite(pdf);
            response.Flush();

            // End the request without Response.End() (which throws ThreadAbortException): the rest of the
            // page lifecycle still runs, but nothing else is written to the response.
            response.SuppressContent = true;
            Context.ApplicationInstance.CompleteRequest();
        }

        private string GetPdfFileName()
        {
            if (!string.IsNullOrEmpty(PdfFileName))
                return PdfFileName;
            string name = !string.IsNullOrEmpty(ReportFile) ? Path.GetFileNameWithoutExtension(ReportFile)
                : !string.IsNullOrEmpty(preparedReport?.ReportInfo.Name) ? preparedReport.ReportInfo.Name
                : "report";
            return name + ".pdf";
        }

        private static string ContentDisposition(string type, string fileName)
        {
            // RFC 6266: an ASCII fallback plus the UTF-8 encoded name for non-ASCII file names.
            var ascii = new StringBuilder(fileName.Length);
            foreach (char c in fileName)
                ascii.Append(c < 32 || c > 126 || c == '"' || c == '\\' ? '_' : c);
            return $"{type}; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
        }

        private void RenderToolbar(HtmlTextWriter writer, int pageIndex, int pageCount)
        {
            writer.AddAttribute(HtmlTextWriterAttribute.Class, "fr-toolbar");
            writer.RenderBeginTag(HtmlTextWriterTag.Div);

            if (ViewMode == ReportViewMode.SinglePage && pageCount > 1)
            {
                RenderPageLink(writer, "« First", 0, pageIndex > 0);
                RenderPageLink(writer, "‹ Previous", pageIndex - 1, pageIndex > 0);
                writer.AddAttribute(HtmlTextWriterAttribute.Class, "fr-page-number");
                writer.RenderBeginTag(HtmlTextWriterTag.Span);
                writer.WriteEncodedText($"Page {pageIndex + 1} of {pageCount}");
                writer.RenderEndTag();
                RenderPageLink(writer, "Next ›", pageIndex + 1, pageIndex < pageCount - 1);
                RenderPageLink(writer, "Last »", pageCount - 1, pageIndex < pageCount - 1);
            }
            else
            {
                writer.AddAttribute(HtmlTextWriterAttribute.Class, "fr-page-number");
                writer.RenderBeginTag(HtmlTextWriterTag.Span);
                writer.WriteEncodedText(pageCount == 1 ? "1 page" : $"{pageCount} pages");
                writer.RenderEndTag();
            }

            if (ShowPdfButton)
            {
                writer.AddAttribute(HtmlTextWriterAttribute.Class, "fr-button fr-pdf");
                writer.AddAttribute(HtmlTextWriterAttribute.Href, BuildUrl(PdfQueryKey, ClientID));
                if (!PdfInline)
                    writer.AddAttribute("download", GetPdfFileName());
                writer.RenderBeginTag(HtmlTextWriterTag.A);
                writer.WriteEncodedText("Download PDF");
                writer.RenderEndTag();
            }

            writer.RenderEndTag();
        }

        private void RenderPageLink(HtmlTextWriter writer, string text, int targetIndex, bool enabled)
        {
            if (enabled)
            {
                writer.AddAttribute(HtmlTextWriterAttribute.Class, "fr-button");
                writer.AddAttribute(HtmlTextWriterAttribute.Href, BuildUrl(PageQueryKey, (targetIndex + 1).ToString(CultureInfo.InvariantCulture)));
                writer.RenderBeginTag(HtmlTextWriterTag.A);
            }
            else
            {
                writer.AddAttribute(HtmlTextWriterAttribute.Class, "fr-button fr-disabled");
                writer.AddAttribute("aria-disabled", "true");
                writer.RenderBeginTag(HtmlTextWriterTag.Span);
            }
            writer.WriteEncodedText(text);
            writer.RenderEndTag();
        }

        /// <summary>Returns the current URL with one query parameter set (and any PDF request removed).</summary>
        private string BuildUrl(string key, string value)
        {
            var query = HttpUtility.ParseQueryString(Request.Url.Query);
            query.Remove(PdfQueryKey);
            query[key] = value;
            return Request.Path + "?" + query;
        }

        private static void RenderMessage(HtmlTextWriter writer, string cssClass, string message)
        {
            writer.AddAttribute(HtmlTextWriterAttribute.Class, cssClass);
            writer.RenderBeginTag(HtmlTextWriterTag.Div);
            writer.WriteEncodedText(message);
            writer.RenderEndTag();
        }

        private string MapReportPath(string path)
        {
            if (path.StartsWith("~", StringComparison.Ordinal) || !Path.IsPathRooted(path))
                return Server.MapPath(path);
            return path;
        }

        private const string DefaultStyles = @"<style>
.fr-viewer{font-family:'Segoe UI',Arial,sans-serif;font-size:14px;color:#222}
.fr-toolbar{display:flex;flex-wrap:wrap;gap:6px;align-items:center;padding:6px 8px;margin-bottom:12px;background:#f4f4f4;border:1px solid #ddd;border-radius:4px;position:sticky;top:0;z-index:1}
.fr-button{display:inline-block;padding:3px 10px;border:1px solid #c8c8c8;border-radius:3px;background:#fff;color:#222;text-decoration:none;line-height:1.5}
a.fr-button:hover{background:#e9f1fb;border-color:#7aa7d8}
.fr-disabled{color:#aaa;cursor:default}
.fr-page-number{padding:0 6px}
.fr-pdf{margin-left:auto;background:#2f6db5;border-color:#2f6db5;color:#fff}
a.fr-pdf:hover{background:#255a97;border-color:#255a97;color:#fff}
.fr-page{margin:0 auto 16px;width:max-content;max-width:100%;background:#fff;box-shadow:0 1px 4px rgba(0,0,0,.3)}
.fr-page-svg{display:block;max-width:100%;height:auto}
.fr-error,.fr-message{padding:8px 12px;border:1px solid #e3b4b4;background:#fdf0f0;color:#8a1f1f;border-radius:4px}
.fr-message{border-color:#ddd;background:#f8f8f8;color:#444}
</style>";
    }
}
