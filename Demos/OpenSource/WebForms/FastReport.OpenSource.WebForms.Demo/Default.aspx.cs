using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace FastReport.WebForms.Demo
{
    public class DefaultPage : Page
    {
        private static readonly object DataLock = new object();
        private static DataSet northwind;

        protected Repeater ReportList;
        protected HyperLink SinglePageLink;
        protected HyperLink AllPagesLink;
        protected ReportViewer Viewer;

        /// <summary>The folder with the demo reports and nwind.xml (Demos\Reports by default, see Web.config).</summary>
        private static string ReportsFolder =>
            Path.GetFullPath(Path.Combine(HttpRuntime.AppDomainAppPath, ConfigurationManager.AppSettings["ReportsFolder"] ?? "App_Data"));

        protected void Page_Load(object sender, EventArgs e)
        {
            var reports = Directory.GetFiles(ReportsFolder, "*.frx")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Only names of existing reports are accepted, so the query string cannot point anywhere else.
            string requested = Request.QueryString["report"];
            string current = reports.FirstOrDefault(r => string.Equals(r, requested, StringComparison.OrdinalIgnoreCase))
                ?? reports.FirstOrDefault(r => r == "Simple List")
                ?? reports.FirstOrDefault();
            bool allPages = Request.QueryString["view"] == "all";

            ReportList.DataSource = reports.Select(name => new
            {
                Name = name,
                Url = "?report=" + HttpUtility.UrlEncode(name) + (allPages ? "&view=all" : string.Empty),
                CssClass = name == current ? "current" : string.Empty,
            });
            ReportList.DataBind();

            string reportQuery = "?report=" + HttpUtility.UrlEncode(current);
            SinglePageLink.NavigateUrl = reportQuery;
            AllPagesLink.NavigateUrl = reportQuery + "&view=all";

            if (current != null)
            {
                Viewer.ReportFile = Path.Combine(ReportsFolder, current + ".frx");
                Viewer.PdfFileName = current + ".pdf";
            }
            Viewer.ViewMode = allPages ? ReportViewMode.AllPages : ReportViewMode.SinglePage;
        }

        protected void Viewer_ReportPreparing(object sender, ReportPreparingEventArgs e)
        {
            e.Report.RegisterData(GetNorthwind(), "NorthWind");
        }

        private static DataSet GetNorthwind()
        {
            lock (DataLock)
            {
                if (northwind == null)
                {
                    var data = new DataSet();
                    data.ReadXml(Path.Combine(ReportsFolder, "nwind.xml"));
                    northwind = data;
                }
                return northwind;
            }
        }
    }
}
