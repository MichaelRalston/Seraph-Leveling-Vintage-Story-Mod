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
        public ProgressReportContent[] LatestData { get; set; } = Array.Empty<ProgressReportContent>();

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
                characterDialog.SingleComposer?.ReCompose();
            }
        }

        /// <summary>
        /// Automatically called by Vintage Story's character screen layout loop when your tab header is clicked.
        /// </summary>
        public void RenderTabContent(GuiComposer composer, GuiDialogCharacterBase parentDialog)
        {
            if (composer == null || parentDialog == null) return;
            capi.World.Logger.Debug($"[SeraphLeveling] Rendering Seraph Leveling tab content");

            int insetWidth = 365;
            int insetHeight = 340;

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

            capi.World.Logger.Debug($"[SeraphLeveling] About to build structural clipping framework for Seraph Leveling tab content");
            // 1. Build the official wiki-verified structural clipping framework tree
            composer.BeginChildElements()
                .BeginClip(clipBounds)
                .AddContainer(containerBounds, "seraphScrollList")
                .EndClip()
                .AddVerticalScrollbar((value) =>
                {
                    capi.World.Logger.Debug($"[SeraphLeveling] Scrollbar value changed to {value}");
                    if (parentDialog.SingleComposer == null) return;
                    ElementBounds bounds = parentDialog.SingleComposer.GetContainer("scroll-content").Bounds;
                    currentScrollOffset = value;
                    bounds.fixedY = 0 - value;
                    bounds.CalcWorldBounds();
                }, scrollbarBounds, "seraphScrollbar")
                .EndChildElements();
            capi.World.Logger.Debug($"[SeraphLeveling] Built structural clipping framework for Seraph Leveling tab content");

            // Set up standardized fonts
            CairoFont titleFont = CairoFont.WhiteSmallText();
            titleFont.UnscaledFontsize = 16;
            CairoFont descFont = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.15f);
            CairoFont smallToolFont = CairoFont.WhiteSmallText().WithColor(new double[] { 0.6, 0.6, 0.6, 1.0 });

            GuiElementContainer scrollArea = composer.GetContainer("seraphScrollList");

            // 3. Use standard factory methods safely—they now bind natively into our scrolling canvas context
            ElementBounds rowBounds = ElementBounds.Fixed(0, 0, insetWidth - 20, 0);
            bool isFirst = true;

            foreach (var trait in LatestData)
            {
                if (!isFirst) rowBounds = rowBounds.BelowCopy(0, 15);
                isFirst = false;

                // Primary Trait Label
                ElementBounds labelBounds = ElementBounds.Fixed(0, 2, 120, 22);
                labelBounds.fixedY = rowBounds.fixedY + 2;
                scrollArea.Add(new GuiElementStaticText(capi, trait.Name, EnumTextOrientation.Left, labelBounds, titleFont));

                // Core Visual Progress Bar (XPBarColor texture graphic)
                ElementBounds barBounds = ElementBounds.Fixed(125, 0, 200, 22);
                barBounds.fixedY = rowBounds.fixedY;
                var subBar = new GuiElementStatbar(capi, barBounds, GuiStyle.XPBarColor, false, false);

                float clampedPct = (float)Math.Max(0.0, Math.Min(1.0, trait.Percentage));
                subBar.SetValues(clampedPct, 0f, 1f);
                scrollArea.Add(subBar);

                if (!string.IsNullOrEmpty(trait.Tooltip))
                {
                    scrollArea.Add(new GuiElementHoverText(capi, trait.Tooltip, CairoFont.WhiteSmallText(), 220, barBounds));
                }

                int currentYOffset = 26;

                // Multi-Tool Expansion Row Blocks (PartialCredits)
                if (trait.PartialCredits != null && trait.PartialCredits.Length > 0)
                {
                    bool isExpanded = expandedTraits.Contains(trait.Name);
                    string btnText = isExpanded ? "−" : "+";

                    ElementBounds btnBounds = ElementBounds.Fixed(335, 0, 24, 22);
                    btnBounds.fixedY = rowBounds.fixedY;

                    scrollArea.Add(new GuiElementTextButton(capi, btnText, descFont, descFont, () =>
                    {
                        if (isExpanded) expandedTraits.Remove(trait.Name);
                        else expandedTraits.Add(trait.Name);

                        parentDialog.SingleComposer?.ReCompose();
                        return true;
                    }, btnBounds));

                    int renderLimit = isExpanded ? trait.PartialCredits.Length : 1;

                    for (int i = 0; i < renderLimit; i++)
                    {
                        var partial = trait.PartialCredits[i];

                        ElementBounds toolLabelBounds = ElementBounds.Fixed(15, currentYOffset + 2, 105, 18);
                        toolLabelBounds.fixedY = rowBounds.fixedY + currentYOffset + 2;
                        scrollArea.Add(new GuiElementStaticText(capi, partial.Name, EnumTextOrientation.Left, toolLabelBounds, smallToolFont));

                        ElementBounds toolBarBounds = ElementBounds.Fixed(125, currentYOffset, 200, 16);
                        toolBarBounds.fixedY = rowBounds.fixedY + currentYOffset;
                        string subBarKey = $"subbar-{trait.Name}-{i}";

                        subBar = new GuiElementStatbar(capi, toolBarBounds, GuiStyle.FoodBarColor, false, false);
                        scrollArea.Add(subBar);
                        subBar.SetValues(partial.Percentage, 0f, 1f);

                        if (!string.IsNullOrEmpty(partial.Tooltip))
                        {
                            scrollArea.Add(new GuiElementHoverText(capi, partial.Tooltip, CairoFont.WhiteSmallText(), 220, toolBarBounds));
                        }

                        currentYOffset += 22;
                    }
                }

                if (!string.IsNullOrEmpty(trait.ExtraInfo))
                {
                    ElementBounds extraBounds = ElementBounds.Fixed(10, currentYOffset, insetWidth - 20, 18);
                    extraBounds.fixedY = rowBounds.fixedY + currentYOffset;
                    scrollArea.Add(new GuiElementStaticText(capi, trait.ExtraInfo, EnumTextOrientation.Left, extraBounds, smallToolFont));
                    currentYOffset += 20;
                }

                if (!string.IsNullOrEmpty(trait.Instructions))
                {
                    ElementBounds instBounds = ElementBounds.Fixed(10, currentYOffset, insetWidth - 20, 36);
                    instBounds.fixedY = rowBounds.fixedY + currentYOffset;
                    scrollArea.Add(new GuiElementStaticText(capi, trait.Instructions, EnumTextOrientation.Left, instBounds, descFont));
                    currentYOffset += 40;
                }

                rowBounds.fixedHeight = currentYOffset;
            }
            composer.Compose();

            // 5. Set heights natively post-composition so the scroll slider calculates limits
            GuiElementScrollbar scrollbar = composer.GetScrollbar("seraphScrollbar");
            if (scrollbar != null)
            {
                scrollbar.SetHeights((float)clipBounds.fixedHeight, (float)(rowBounds.fixedY + rowBounds.fixedHeight));

                // Directly translate the underlying container boundary parameters to match the offset location
                containerBounds.fixedY = 0 - currentScrollOffset;
                containerBounds.CalcWorldBounds();
            }
            capi.World.Logger.Debug($"[SeraphLeveling] At the end of the function.");
        }        
    }
}