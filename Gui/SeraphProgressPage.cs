using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using SeraphLeveling.Messages;
using Vintagestory.API.Util;

namespace SeraphLeveling.Gui
{
    /// <summary>
    /// Handbook page "Seraph Leveling: My Progress". The server pushes the
    /// report on join, on level-ups and every 15 s when it changed. On top of
    /// that the page asks for a fresh report the moment it is opened and once
    /// a second while it stays open, and redraws itself when new numbers
    /// arrive, so mining a block shows up on the page within about a second.
    /// </summary>
    public class SeraphProgressPage : GuiHandbookTextPage
    {
        private readonly ICoreClientAPI capi;

        /// <summary>The page the handbook currently holds (recreated with the handbook).</summary>
        public static SeraphProgressPage Instance;

        /// <summary>Mod channel, set by the client system; used to ask the server for a report.</summary>
        public static IClientNetworkChannel Channel;

        /// <summary>Last report received over the network channel.</summary>
        public static string LatestReport;

        /// <summary>The report this page last drew, so an unchanged re-sync does not redraw.</summary>
        private string shownReport;

        private static readonly System.Reflection.FieldInfo browseHistoryField =
            typeof(GuiDialogHandbook).GetField("browseHistory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        public SeraphProgressPage(ICoreClientAPI capi)
        {
            this.capi = capi;
            pageCode = "seraphleveling-progress";
            Title = "sl-progress-title";
            categoryCode = "guide";
            Text = "sl-progress-title";
            Init(capi);
            Instance = this;
        }

        /// <summary>Ask the server for the current report. force = send it even if nothing changed.</summary>
        public static void RequestReport(bool force)
        {
            try { Channel?.SendPacket(new ProgressReportRequestMessage { Force = force }); }
            catch (Exception ex) { Instance?.capi?.Logger?.Debug("[SeraphLeveling] progress report request failed: {0}", ex.Message); }
        }

        /// <summary>Network handler: keep the report and redraw the page if it is on screen.</summary>
        public static void OnReportReceived(string report)
        {
            LatestReport = report;
            Instance?.RefreshIfShowing();
        }

        /// <summary>Client tick: while the handbook shows this page, keep asking for changes.</summary>
        public static void PollIfShowing()
        {
            var page = Instance;
            if (page != null && page.DialogShowingThis() != null) RequestReport(false);
        }

        /// <summary>The open handbook dialog with this page on screen, or null.</summary>
        private GuiDialogHandbook DialogShowingThis()
        {
            try
            {
                foreach (object gui in capi.OpenedGuis)
                {
                    if (!(gui is GuiDialogHandbook dlg) || !dlg.IsOpened()) continue;
                    if (browseHistoryField?.GetValue(dlg) is Stack<BrowseHistoryElement> history
                        && history.Count > 0 && history.Peek().Page == this && history.Peek().SearchText == null)
                        return dlg;
                }
            }
            catch { }
            return null;
        }

        private void RefreshIfShowing()
        {
            if (LatestReport == shownReport) return;
            var dlg = DialogShowingThis();
            if (dlg == null) return;
            dlg.ReloadPage();   // recomposes this page; the scroll position lives in the history entry and survives
        }

        private string CurrentVtml()
        {
            string report = LatestReport;
            var sb = new StringBuilder();
            sb.Append("<strong>").Append(Lang.Get("sl-progress-title")).Append("</strong><br>");
            if (string.IsNullOrEmpty(report))
            {
                sb.Append(Lang.Get("sl-progress-waiting"));
            }
            else
            {
                foreach (string raw in report.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) { sb.Append("<br>"); continue; }
                    string safe = line.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                    string descaped = safe.Replace("&lt;strong&gt;", "<strong>").Replace("&lt;/strong&gt;", "</strong>");
                    sb.Append(descaped).Append("<br>");
                }
            }
            sb.Append("<br>").Append(Lang.Get("sl-progress-footer"));
            return sb.ToString();
        }

        public override void ComposePage(GuiComposer detailViewGui, ElementBounds textBounds, ItemStack[] allstacks, ActionConsumable<string> openDetailPageFor)
        {
            shownReport = LatestReport;
            var comps = VtmlUtil.Richtextify(capi, CurrentVtml(), CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2));
            detailViewGui.AddRichtext(comps, textBounds, "richtext");
            // Page just opened (or redrawn): make sure the numbers are current.
            RequestReport(force: shownReport == null);
        }

        public override PageText GetPageText()
        {
            return new PageText { Title = Lang.Get("sl-progress-title").ToSearchFriendly(), Text = CurrentVtml() };
        }
    }
}
