<%@ Page Language="C#" AutoEventWireup="true" Inherits="FastReport.WebForms.Demo.DefaultPage" %>
<%@ Register Assembly="FastReport.WebForms" Namespace="FastReport.WebForms" TagPrefix="fr" %>
<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>FastReport Web Forms demo</title>
    <style>
        body { margin: 0; font-family: 'Segoe UI', Arial, sans-serif; background: #e9ecef; color: #222; }
        header { padding: 12px 20px; background: #24476b; color: #fff; }
        header h1 { margin: 0; font-size: 18px; font-weight: 600; }
        .layout { display: flex; gap: 20px; padding: 20px; align-items: flex-start; }
        nav { flex: 0 0 240px; background: #fff; border: 1px solid #d6dadf; border-radius: 4px; padding: 8px 0; max-height: calc(100vh - 100px); overflow: auto; position: sticky; top: 20px; }
        nav a { display: block; padding: 4px 14px; color: #24476b; text-decoration: none; font-size: 14px; }
        nav a:hover { background: #eef3f8; }
        nav a.current { background: #24476b; color: #fff; }
        main { flex: 1; min-width: 0; }
        .options { margin-bottom: 10px; font-size: 13px; }
        .options a { margin-right: 10px; }
        @media (max-width: 760px) {
            .layout { flex-direction: column; align-items: stretch; padding: 12px; }
            nav { position: static; flex-basis: auto; max-height: 200px; }
        }
    </style>
</head>
<body>
    <header><h1>FastReport Open Source &middot; Web Forms ReportViewer (SVG + PDF, rendered with SkiaSharp)</h1></header>
    <div class="layout">
        <nav>
            <asp:Repeater ID="ReportList" runat="server">
                <ItemTemplate>
                    <a href="<%# Eval("Url") %>" class="<%# Eval("CssClass") %>"><%# Server.HtmlEncode((string)Eval("Name")) %></a>
                </ItemTemplate>
            </asp:Repeater>
        </nav>
        <main>
            <div class="options">
                View: <asp:HyperLink ID="SinglePageLink" runat="server" Text="Single page" />
                <asp:HyperLink ID="AllPagesLink" runat="server" Text="All pages" />
            </div>
            <fr:ReportViewer ID="Viewer" runat="server" OnReportPreparing="Viewer_ReportPreparing" />
        </main>
    </div>
</body>
</html>
