using SeraphLeveling.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using System.Reflection;

namespace SeraphLeveling.Gui
{
    public partial class SeraphCharacterTabComponent(ICoreClientAPI capi)
    {
        private readonly ICoreClientAPI capi = capi;
        private readonly HashSet<string> expandedTraits = [];
        private float currentScrollOffset = 0f;
        public ProgressReportContent[] LatestData { get; set; } = [];
        GuiComposer composer;

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

        MethodInfo recomposeMethod;
        bool recomposeMethodResolved;
        // Recomposition borrowed from Prosequor by Hyomoto. Before I can publish this to main, license complications must be resolved... though there's really no other way to do this that I can find.
        static MethodInfo FindRecomposeMethod(Type type)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                MethodInfo found = t.GetMethod(
                    "ComposeGuis",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    null,
                    Type.EmptyTypes,
                    null);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public void RunRecompose(GuiDialogCharacterBase parentDialog)
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (!recomposeMethodResolved)
                {
                    recomposeMethod = FindRecomposeMethod(parentDialog.GetType());
                    recomposeMethodResolved = true;
                }
                recomposeMethod?.Invoke(parentDialog, null);
            }, "liveupdateserraphtab");
        }

        public void RefreshIfActive(GuiDialogCharacterBase characterDialog)
        {
            if (characterDialog != null && characterDialog.IsOpened())
            {
                RunRecompose(characterDialog);
            }
        }

        public void RenderTabContent(GuiComposer childComposer, GuiDialogCharacterBase parentDialog)
        {
            if (childComposer == null || parentDialog == null) return;
            composer = childComposer;

            int insetWidth = 365;
            int insetHeight = 315;
            int insetDepth = 3;

            ElementBounds insetBounds = ElementBounds.Fixed(10, 70, insetWidth, insetHeight);
            ElementBounds scrollbarBounds = insetBounds.RightCopy().WithFixedWidth(15);
            ElementBounds clipBounds = insetBounds.ForkContainingChild(0, 0, 0, 0);

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
            GuiElementContainer scrollArea = composer.GetContainer("scroll-content");
            if (scrollArea == null) return;

            double accumulatedY = 0;
            CairoFont baseFont = CairoFont.WhiteSmallText();
            baseFont.UnscaledFontsize = 16;
            CairoFont smallToolFont = CairoFont.WhiteSmallText().WithColor([0.6, 0.6, 0.6, 1.0]);

            for (int i = 0; i < LatestData.Length; i++)
            {
                var data = LatestData[i];
                bool isExpanded = expandedTraits.Contains(data.Name);

                // Define positions using plain absolute row indices relative to the parent container frame
                ElementBounds titleBounds = ElementBounds.Fixed(0, accumulatedY + 4, 120, 22).WithParent(containerBounds);
                ElementBounds titleMeasureBounds = ElementBounds.Fixed(0, accumulatedY + 4, 112, 22).WithParent(containerBounds);
                titleMeasureBounds.CalcWorldBounds();
                CairoFont rowTitleFont = baseFont.Clone();
                rowTitleFont.UnscaledFontsize = 16;
                string plainTitleText = StripTags().Replace(data.Name, "");
                rowTitleFont.AutoFontSize(plainTitleText, titleMeasureBounds);
                GuiElementRichtext titleText = new(capi, VtmlUtil.Richtextify(capi, data.Name, rowTitleFont), titleBounds);
                scrollArea.Add(titleText);

                GuiElementStatbar statBar = new(capi, ElementBounds.Fixed(125, accumulatedY, 200, 22).WithParent(containerBounds), GuiStyle.XPBarColor, false, false);
                statBar.SetValues((float)Math.Max(0.0, Math.Min(1.0, data.Percentage)), 0f, 1f);
                statBar.ShowValueOnHover = false;
                scrollArea.Add(statBar);

                if (!string.IsNullOrEmpty(data.Tooltip))
                {
                    GuiElementHoverText hoverText = new(capi, data.Tooltip, CairoFont.WhiteSmallText(), 220, ElementBounds.Fixed(125, accumulatedY, 200, 22).WithParent(containerBounds));
                    scrollArea.Add(hoverText);
                }

                if (data.PartialCredits != null && data.PartialCredits.Length > 1)
                {
                    ElementBounds btnBounds = ElementBounds.Fixed(350, accumulatedY, 12, 22).WithParent(containerBounds);
                    string btnText = isExpanded ? "−" : "+";
                    string elementKey = $"btn_expand_{i}";

                    composer.AddButton(btnText, () =>
                    {
                        if (isExpanded) expandedTraits.Remove(data.Name);
                        else expandedTraits.Add(data.Name);

                        RunRecompose(parentDialog);
                        return true;
                    }, btnBounds, baseFont, EnumButtonStyle.Normal, elementKey);
                }

                accumulatedY += 26;

                if (data.PartialCredits != null)
                {
                    foreach (var partial in data.PartialCredits)
                    {
                        ElementBounds subLabelBounds = ElementBounds.Fixed(15, accumulatedY + 1, 105, 15).WithParent(containerBounds);
                        ElementBounds subLabelMeasureBounds = ElementBounds.Fixed(15, accumulatedY + 1, 100, 22).WithParent(containerBounds);
                        subLabelMeasureBounds.CalcWorldBounds();
                        CairoFont subLabelFont = smallToolFont.Clone();
                        subLabelFont.UnscaledFontsize = 14;
                        subLabelFont.AutoFontSize(partial.Name, subLabelMeasureBounds);

                        GuiElementRichtext subLabel = new(capi, VtmlUtil.Richtextify(capi, partial.Name, subLabelFont), subLabelBounds);
                        scrollArea.Add(subLabel);

                        ElementBounds subBarBounds = ElementBounds.Fixed(125, accumulatedY, 200, 16).WithParent(containerBounds);
                        GuiElementStatbar subBar = new(capi, subBarBounds, GuiStyle.FoodBarColor, false, false);
                        subBar.SetValues(partial.Percentage, 0f, 1f);
                        subBar.ShowValueOnHover = false;
                        scrollArea.Add(subBar);

                        if (!string.IsNullOrEmpty(partial.Tooltip))
                        {
                            GuiElementHoverText subHover = new(capi, partial.Tooltip, CairoFont.WhiteSmallText(), 220, subBarBounds);
                            scrollArea.Add(subHover);
                        }

                        accumulatedY += 22;
                        if (!isExpanded) break;
                    }
                }

                if (!string.IsNullOrEmpty(data.ExtraInfo))
                {
                    ElementBounds extraBounds = ElementBounds.Fixed(10, accumulatedY, 345, 18).WithParent(containerBounds);
                    GuiElementRichtext extraText = new(capi, VtmlUtil.Richtextify(capi, data.ExtraInfo, smallToolFont), extraBounds);
                    scrollArea.Add(extraText);
                    extraText.RecomposeText();
                    accumulatedY += extraText.Bounds.fixedHeight;
                }

                if (!string.IsNullOrEmpty(data.Instructions))
                {
                    ElementBounds instBounds = ElementBounds.Fixed(10, accumulatedY, 345, 36).WithParent(containerBounds);
                    GuiElementRichtext instText = new(capi, VtmlUtil.Richtextify(capi, data.Instructions, smallToolFont), instBounds);
                    scrollArea.Add(instText);
                    instText.RecomposeText();
                    accumulatedY += instText.Bounds.fixedHeight;
                }

                accumulatedY += 4;
            }
            bool isInitializing = true;
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
        private static partial System.Text.RegularExpressions.Regex StripTags();
    }
}
