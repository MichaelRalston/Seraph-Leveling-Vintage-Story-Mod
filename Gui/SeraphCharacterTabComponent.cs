using SeraphLeveling.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace SeraphLeveling.Gui
{
    public class SeraphCharacterTabComponent(ICoreClientAPI capi)
    {
        private readonly ICoreClientAPI capi = capi;
        private readonly HashSet<string> expandedTraits = [];
        // This persists the scroll coordinates across individual tab window redraw frames safely
        private float currentScrollOffset = 0f;
        public ProgressReportContent[] LatestData { get; set; } = [];

        public void OnProgressReportReceived(ProgressReportContent[] content)
        {
            LatestData = content;
            foreach (GuiDialog openGui in capi.OpenedGuis.Cast<GuiDialog>())
            {
                if (openGui is GuiDialogCharacterBase characterDialog && characterDialog.IsOpened())
                {
                    RefreshIfActive(characterDialog);
                    break;
                }
            }
        }

        public void RefreshIfActive(GuiDialogCharacterBase characterDialog)
        {
            if (characterDialog != null && characterDialog.IsOpened())
            {
                capi.Event.EnqueueMainThreadTask(() =>
                {
                    characterDialog.SingleComposer?.ReCompose();
                }, "liveupdateserraphtab");
            }
        }


        /// <summary>
        /// Automatically called by Vintage Story's character screen layout loop when your tab header is clicked.
        /// </summary>
        public void RenderTabContent(GuiComposer composer, GuiDialogCharacterBase parentDialog)
        {
            if (composer == null || parentDialog == null) return;

            int insetWidth = 365;
            int insetHeight = 340;
            int insetDepth = 3;

            // Define viewport and scroll dimensions exactly matching the wiki guidelines
            ElementBounds insetBounds = ElementBounds.Fixed(10, 45, insetWidth, insetHeight);
            ElementBounds scrollbarBounds = insetBounds.RightCopy().WithFixedWidth(15);

            ElementBounds clipBounds = insetBounds.ForkContainingChild(0, 0, 0, 0);

            // This container bounds holds the layout elements vertically
            ElementBounds containerBounds = insetBounds.ForkContainingChild(0, 0, 0, 0);

            if (LatestData == null || LatestData.Length == 0)
            {
                composer.AddStaticText(Lang.Get("sl-progress-waiting"), CairoFont.WhiteSmallText(), insetBounds);
                return;
            }

            composer.BeginChildElements()
                .AddInset(insetBounds, insetDepth)
                .BeginClip(clipBounds)
                .AddCellList(
                    containerBounds.WithFixedOffset(0, 0).WithFixedWidth(insetWidth - 20),
                    (data, cellBounds) =>
                    {
                        bool isExpanded = expandedTraits.Contains(data.Name);

                        return new SeraphTraitCell(capi, cellBounds, data, isExpanded, currentScrollOffset, composer, () =>
                        {
                            // This is the OnStateChanged callback from the button
                            if (isExpanded) expandedTraits.Remove(data.Name);
                            else expandedTraits.Add(data.Name);

                            // Safely trigger a layout redraw of the main character window
                            parentDialog.SingleComposer?.ReCompose();
                        });
                    },
                    LatestData,
                    "seraphScrollList"
                )
                .EndClip()
                .AddVerticalScrollbar((value) =>
                {
                    // Save the offset so it persists across redraws
                    currentScrollOffset = value;

                    // Target the correct container name ("seraphScrollList")
                    var cellList = composer.GetCellList<ProgressReportContent>("seraphScrollList");
                    if (cellList != null)
                    {
                        cellList.Bounds.fixedY = 0 - value;
                        cellList.Bounds.CalcWorldBounds();
                    }
                }, scrollbarBounds, "seraphScrollbar")
                .EndChildElements();

            composer.Compose();

            // 5. Connect heights cleanly so scroll slider limitations map rows perfectly
            GuiElementScrollbar scrollbar = composer.GetScrollbar("seraphScrollbar");
            var finalCellList = composer.GetCellList<ProgressReportContent>("seraphScrollList");

            if (scrollbar != null && finalCellList != null)
            {
                // Let the cell list determine how long it actually is based on all cell rows combined
                float totalListHeight = (float)finalCellList.Bounds.fixedHeight;

                scrollbar.SetHeights((float)clipBounds.fixedHeight, totalListHeight);
                scrollbar.CurrentYPosition = currentScrollOffset;

                finalCellList.Bounds.fixedY = 0 - currentScrollOffset;
                finalCellList.Bounds.CalcWorldBounds();
            }
        }

    }
}