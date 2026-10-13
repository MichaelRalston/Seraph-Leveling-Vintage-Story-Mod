using SeraphLeveling.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using System.Reflection;
using Vintagestory.API.Datastructures;
using HarmonyLib;
using Vintagestory.GameContent;

namespace SeraphLeveling.Gui
{
    public class SeraphCharacterTabRow(ICoreClientAPI capi, SeraphCharacterTabComponent parent, ElementBounds bounds, ProgressReportContent data, string composerUniqueKey) : GuiElement(capi, bounds)
    {
        readonly SeraphCharacterTabComponent parent = parent;
        bool isExpanded = false;
        ProgressReportContent data = data;
        private GuiComposer composer;
        private double offset = 0;

        private readonly string composerUniqueKey = composerUniqueKey;

        public double Process(ProgressReportContent data)
        {
            if (data.IsSame(this.data))
            {
                return offset;
            }
            this.data = data;
            return RenderData(data);
        }
        public double RenderData(ProgressReportContent data)
        {
            if (composer != null)
            {
                composer.Clear(Bounds);
                composer.Dispose();
            }
            composer = api.Gui.CreateCompo(composerUniqueKey, Bounds);
            var containerBounds = Bounds.ForkContainingChild(0, 0, 0, 0);
            composer.AddContainer(containerBounds, composerUniqueKey);
            var scrollArea = composer.GetContainer(composerUniqueKey);
            offset = 0;
            ElementBounds titleBounds = ElementBounds.Fixed(3, offset + 4, 120, 22).WithParent(containerBounds);
            ElementBounds titleMeasureBounds = ElementBounds.Fixed(3, offset + 4, 112, 22).WithParent(containerBounds);
            titleMeasureBounds.CalcWorldBounds();
            CairoFont rowTitleFont = parent.baseFont.Clone();
            rowTitleFont.UnscaledFontsize = 16;
            string plainTitleText = SeraphCharacterTabComponent.StripTags().Replace(data.Name, "");
            rowTitleFont.AutoFontSize(plainTitleText, titleMeasureBounds);
            GuiElementRichtext titleText = new(api, VtmlUtil.Richtextify(api, data.Name, rowTitleFont), titleBounds);
            scrollArea.Add(titleText);

            var barBounds = ElementBounds.Fixed(125, 0, 225, 22).WithParent(containerBounds);
            GuiElementStatbar statBar = new(api, barBounds, GuiStyle.XPBarColor, false, false);
            statBar.SetValues((float)Math.Max(0.0, Math.Min(1.0, data.Percentage)), 0f, 1f);
            statBar.ShowValueOnHover = false;
            scrollArea.Add(statBar);

            if (!string.IsNullOrEmpty(data.Tooltip))
            {
                GuiElementHoverText hoverText = new(api, data.Tooltip, CairoFont.WhiteSmallText(), 220, barBounds);
                hoverText.SetAutoWidth(true);
                scrollArea.Add(hoverText);
            }

            if ((data.PartialCredits != null && data.PartialCredits.Length > 1) || data.CollapseExtraInfo)
            {
                ElementBounds btnBounds = ElementBounds.Fixed(350, offset, 12, 22).WithParent(containerBounds);
                string btnText = isExpanded ? "-" : "+";

                composer.AddButton(btnText, () =>
                {
                    isExpanded = !isExpanded;
                    RenderData(data);

                    parent.Reprocess();
                    return true;
                }, btnBounds, parent.buttonFont, EnumButtonStyle.Normal);
            }

            offset += 26;

            if (data.PartialCredits != null)
            {
                foreach (var partial in data.PartialCredits)
                {
                    ElementBounds subLabelBounds = ElementBounds.Fixed(15, offset + 1, 105, 15).WithParent(containerBounds);
                    ElementBounds subLabelMeasureBounds = ElementBounds.Fixed(15, offset + 1, 100, 22).WithParent(containerBounds);
                    subLabelMeasureBounds.CalcWorldBounds();
                    CairoFont subLabelFont = parent.smallToolFont.Clone();
                    subLabelFont.UnscaledFontsize = 14;
                    subLabelFont.AutoFontSize(partial.Name, subLabelMeasureBounds);

                    GuiElementRichtext subLabel = new(api, VtmlUtil.Richtextify(api, partial.Name, subLabelFont), subLabelBounds);
                    scrollArea.Add(subLabel);

                    ElementBounds subBarBounds = ElementBounds.Fixed(135, offset, 205, 16).WithParent(containerBounds);
                    GuiElementStatbar subBar = new(api, subBarBounds, GuiStyle.FoodBarColor, false, false);
                    subBar.SetValues(partial.Percentage, 0f, 1f);
                    subBar.ShowValueOnHover = false;
                    scrollArea.Add(subBar);

                    if (!string.IsNullOrEmpty(partial.Tooltip))
                    {
                        GuiElementHoverText subHover = new(api, partial.Tooltip, CairoFont.WhiteSmallText(), 220, subBarBounds);
                        subHover.SetAutoWidth(true);
                        scrollArea.Add(subHover);
                    }

                    offset += 22;
                    if (!isExpanded) break;
                }
            }

            if (!string.IsNullOrEmpty(data.ExtraInfo) && (!data.CollapseExtraInfo || isExpanded))
            {
                ElementBounds extraBounds = ElementBounds.Fixed(10, offset, 345, 18).WithParent(containerBounds);
                GuiElementRichtext extraText = new(api, VtmlUtil.Richtextify(api, data.ExtraInfo, parent.smallToolFont), extraBounds);
                scrollArea.Add(extraText);
                extraText.RecomposeText();
                offset += extraText.Bounds.fixedHeight;
            }

            if (!string.IsNullOrEmpty(data.Instructions))
            {
                ElementBounds instBounds = ElementBounds.Fixed(10, offset, 345, 36).WithParent(containerBounds);
                GuiElementRichtext instText = new(api, VtmlUtil.Richtextify(api, data.Instructions, parent.smallToolFont), instBounds);
                scrollArea.Add(instText);
                instText.RecomposeText();
                offset += instText.Bounds.fixedHeight;
            }

            offset += 4;
            composer.Compose();
            return offset;
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            base.RenderInteractiveElements(deltaTime);
            composer?.Render(deltaTime);
        }

        public override void OnMouseDown(ICoreClientAPI api, MouseEvent mouse)
        {
            composer?.OnMouseDown(mouse);
            if (mouse.Handled) return;
            base.OnMouseDown(api, mouse);
        }

        public override void OnMouseMove(ICoreClientAPI api, MouseEvent mouse)
        {
            composer?.OnMouseMove(mouse);
            base.OnMouseMove(api, mouse);
        }

        public override void OnMouseUp(ICoreClientAPI api, MouseEvent mouse)
        {
            composer?.OnMouseUp(mouse);
            base.OnMouseUp(api, mouse);
        }

        public override void Dispose()
        {
            base.Dispose();
            composer?.Dispose();
        }
    }
    public partial class SeraphCharacterTabComponent(ICoreClientAPI capi)
    {
        private readonly ICoreClientAPI capi = capi;
        private float currentScrollOffset = 0f;
        public ProgressReportContent[] LatestData { get; set; } = [];
        GuiComposer composer;
        bool isListening = false;
        public int assignedIndex;

        public FieldInfo curTabField;

        public double accumulatedY = 3;


        public void OnProgressReportReceived(ProgressReportContent[] content)
        {
            LatestData = content;
            Reprocess();
            foreach (GuiDialog openGui in capi.OpenedGuis.Cast<GuiDialog>())
            {
                if (openGui is GuiDialogCharacterBase characterDialog && characterDialog.IsOpened())
                {
                    break;
                }
            }
        }

        MethodInfo recomposeMethod;

        public void HandleListeningState(bool newState)
        {
            if (newState == isListening) return;
            isListening = newState;
            capi.Logger.Debug($"[SeraphLeveling] HandleListeningState went to {isListening}");
            SeraphProgressPage.RequestReport(listeningToCharacter: isListening);
        }

        public void OnTick(float dt)
        {
            if (curTabField == null) return;
            if (capi.Gui.LoadedGuis.Find(dlg => dlg is GuiDialogCharacterBase) is not GuiDialogCharacterBase charDlg)
            {
                HandleListeningState(false);
                return;
            }
            int currentActiveTab = (int)curTabField.GetValue(charDlg);
            HandleListeningState(currentActiveTab == assignedIndex);
        }
        public void RunRecompose(GuiDialogCharacterBase parentDialog)
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (recomposeMethod == null)
                {
                    recomposeMethod = AccessTools.Method(parentDialog.GetType(), "ComposeGuis");
                }
                recomposeMethod?.Invoke(parentDialog, null);
            }, "liveupdateserraphtab");
        }

        public readonly CairoFont baseFont = CairoFont.WhiteSmallText();
        public readonly CairoFont buttonFont = CairoFont.WhiteSmallText();
        public readonly CairoFont smallToolFont = CairoFont.WhiteSmallText().WithColor([0.6, 0.6, 0.6, 1.0]);

        public readonly List<SeraphCharacterTabRow> reportTabLines = [];

        private readonly int initialOffset = 3;
        public void Reprocess()
        {
            if (reportTabLines.Count != LatestData.Length)
            {
                reportTabLines.Clear();
                foreach (GuiDialog openGui in capi.OpenedGuis.Cast<GuiDialog>())
                {
                    if (openGui is GuiDialogCharacterBase characterDialog && characterDialog.IsOpened())
                    {
                        RunRecompose(characterDialog);
                    }
                }
                return;
            }
            accumulatedY = initialOffset;
            for (int i = 0; i < LatestData.Length; i++)
            {
                reportTabLines[i].Bounds.fixedY = accumulatedY;
                accumulatedY += reportTabLines[i].Process(LatestData[i]);
                reportTabLines[i].Bounds.CalcWorldBounds();
            }
            var scrollArea = composer.GetContainer("scroll-content");
            scrollArea.Bounds.fixedY = accumulatedY;
            scrollArea.Bounds.CalcWorldBounds();
            GuiElementScrollbar scrollbar = composer.GetScrollbar("seraphScrollbar");
            if (scrollbar != null)
            {
                float targetScroll = currentScrollOffset;
                scrollbar.SetHeights((float)clipBounds.fixedHeight, (float)accumulatedY);
                float maxScroll = (float)Math.Max(0f, accumulatedY - clipBounds.fixedHeight);
                if (targetScroll > maxScroll)
                {
                    targetScroll = maxScroll;
                }
                scrollbar.CurrentYPosition = currentScrollOffset = targetScroll;

                // Keep the offset applied across redraw compositions safely
                scrollArea.Bounds.fixedY = 0 - currentScrollOffset;
                scrollArea.Bounds.CalcWorldBounds();
            }
        }

        ElementBounds clipBounds;

        public void RenderTabContent(GuiComposer childComposer, GuiDialogCharacterBase parentDialog)
        {
            if (childComposer == null || parentDialog == null) return;
            composer = childComposer;
            bool isInitializing = true;

            int insetWidth = 365;
            int insetHeight = 315;
            int insetDepth = 3;

            ElementBounds insetBounds = ElementBounds.Fixed(10, 70, insetWidth, insetHeight);
            ElementBounds scrollbarBounds = insetBounds.RightCopy().WithFixedWidth(15);
            clipBounds = insetBounds.ForkContainingChild(0, 0, 0, 0);
            GuiElementContainer scrollArea;

            if (LatestData == null || LatestData.Length == 0)
            {
                composer.AddStaticText(Lang.Get("sl-progress-waiting"), CairoFont.WhiteSmallText(), insetBounds);
                return;
            }
            ElementBounds containerBounds = ElementBounds.Fixed(0, 0, 0, 0).WithParent(clipBounds);

            // 1. Establish layout wrappers natively matching the wiki guide
            composer.BeginChildElements()
                .AddInset(insetBounds, insetDepth)
                .BeginClip(clipBounds)
                .AddContainer(containerBounds, "scroll-content");

            // 2. Fetch the newly mounted container reference to populate our content loop inside it
            scrollArea = composer.GetContainer("scroll-content");
            if (scrollArea == null) return;

            accumulatedY = initialOffset;
            baseFont.UnscaledFontsize = 16;
            buttonFont.Orientation = EnumTextOrientation.Center;

            for (int i = 0; i < LatestData.Length; i++)
            {
                var data = LatestData[i];

                // Define positions using plain absolute row indices relative to the parent container frame
                ElementBounds newContainerBounds = ElementBounds.Fixed(0, accumulatedY, 0, 0).WithParent(containerBounds);
                string innerContainerKey = $"data-{i}";
                composer.AddContainer(newContainerBounds, innerContainerKey);
                SeraphCharacterTabRow elem = new(capi, this, newContainerBounds, data, innerContainerKey);

                var offset = elem.RenderData(data);
                reportTabLines.Add(elem);
                scrollArea.Add(elem);
                newContainerBounds.CalcWorldBounds();
                accumulatedY += offset;
            }
            composer.EndClip()
                .AddVerticalScrollbar((value) =>
                {
                    if (!isInitializing)
                    {
                        currentScrollOffset = value;

                        var container = composer.GetContainer("scroll-content");
                        if (container != null)
                        {
                            container.Bounds.fixedY = 0 - value;
                            container.Bounds.CalcWorldBounds();
                        }
                    }
                }, scrollbarBounds, "seraphScrollbar")
                .EndChildElements();

            // Update container size constraints natively matching content length totals
            containerBounds.fixedHeight = accumulatedY;

            GuiElementScrollbar scrollbar = composer.GetScrollbar("seraphScrollbar");
            if (scrollbar != null)
            {
                scrollbar.SetHeights((float)clipBounds.fixedHeight, (float)accumulatedY);
                float maxScroll = (float)Math.Max(0f, accumulatedY - clipBounds.fixedHeight);
                if (currentScrollOffset > maxScroll)
                {
                    currentScrollOffset = maxScroll;
                }
                scrollbar.CurrentYPosition = currentScrollOffset;

                // Keep the offset applied across redraw compositions safely
                scrollArea.Bounds.fixedY = 0 - currentScrollOffset;
                scrollArea.Bounds.CalcWorldBounds();
            }
            isInitializing = false;
        }

        [System.Text.RegularExpressions.GeneratedRegex("<[^>]*>")]
        public static partial System.Text.RegularExpressions.Regex StripTags();
    }
}
