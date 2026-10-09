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
        /// <summary>
        /// Automatically called by Vintage Story's character screen layout loop when your tab header is clicked.
        /// </summary>
        public void RenderTabContent(GuiComposer composer, GuiDialogCharacterBase parentDialog)
        {
            if (composer == null || parentDialog == null) return;

            // Establish strict layout view dimensions exactly matching the wiki specifications
            int insetWidth = 365;
            int insetHeight = 340;

            ElementBounds insetBounds = ElementBounds.Fixed(10, 45, insetWidth, insetHeight);
            ElementBounds scrollbarBounds = insetBounds.RightCopy().WithFixedWidth(15);

            // Establish the separate clipping framework constraints
            ElementBounds clipBounds = insetBounds.ForkContainingChild(0, 0, 0, 0);
            ElementBounds containerBounds = insetBounds.ForkContainingChild(0, 0, 0, 0);

            if (LatestData == null || LatestData.Length == 0)
            {
                composer.AddStaticText(Lang.Get("sl-progress-waiting"), CairoFont.WhiteSmallText(), insetBounds);
                return;
            }

            // 1. Build the official scrolling layout tree using factory method blocks
            // The scrollbar action directly shifts the container's bounds in real-time, exactly like the wiki example
            composer.BeginChildElements()
                .BeginClip(clipBounds)
                .AddContainer(containerBounds, "seraphScrollList")
                .EndClip()
                .AddVerticalScrollbar((value) =>
                {
                    containerBounds.fixedY = 0 - value;
                    containerBounds.CalcWorldBounds();
                }, scrollbarBounds, "seraphScrollbar")
                .EndChildElements();

            // 2. Fetch the container target from the composer to inject elements into it
            GuiElementContainer scrollArea = composer.GetContainer("seraphScrollList");
            if (scrollArea == null) return;

            // Set up standardized fonts
            CairoFont titleFont = CairoFont.WhiteSmallText();
            titleFont.UnscaledFontsize = 16;
            CairoFont descFont = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.15f);
            CairoFont smallToolFont = CairoFont.WhiteSmallText().WithColor(new double[] { 0.6, 0.6, 0.6, 1.0 });

            // 3. Populate rows inside our scroll area container dynamically
            ElementBounds rowBounds = ElementBounds.Fixed(0, 0, containerBounds.fixedWidth, 0);
            bool isFirst = true;

            foreach (var trait in LatestData)
            {
                if (!isFirst) rowBounds = rowBounds.BelowCopy(0, 15);
                isFirst = false;

                // Primary Trait Label
                ElementBounds labelBounds = ElementBounds.Fixed(0, 2, 120, 22);
                labelBounds.fixedY = rowBounds.fixedY + 2;
                composer.AddStaticText(trait.Name, titleFont, labelBounds);

                // Core Visual Progress Bar (XPBarColor)
                ElementBounds barBounds = ElementBounds.Fixed(125, 0, 200, 22);
                barBounds.fixedY = rowBounds.fixedY;
                string barKey = $"bar-{trait.Name}";

                composer.AddStatbar(barBounds, GuiStyle.XPBarColor, barKey);
                float clampedPct = (float)Math.Max(0.0, Math.Min(1.0, trait.Percentage));
                composer.GetStatbar(barKey).SetValues(clampedPct, 0f, 1f);

                if (!string.IsNullOrEmpty(trait.Tooltip))
                {
                    composer.AddHoverText(trait.Tooltip, CairoFont.WhiteSmallText(), 220, barBounds);
                }

                int currentYOffset = 26;

                // Multi-Tool Expansion Row Blocks
                if (trait.PartialCredits != null && trait.PartialCredits.Length > 0)
                {
                    bool isExpanded = expandedTraits.Contains(trait.Name);
                    string btnText = isExpanded ? "−" : "+";

                    ElementBounds btnBounds = ElementBounds.Fixed(335, 0, 24, 22);
                    btnBounds.fixedY = rowBounds.fixedY;

                    composer.AddSmallButton(btnText, () =>
                    {
                        if (isExpanded) expandedTraits.Remove(trait.Name);
                        else expandedTraits.Add(trait.Name);

                        parentDialog.SingleComposer?.ReCompose();
                        return true;
                    }, btnBounds);

                    int renderLimit = isExpanded ? trait.PartialCredits.Length : 1;

                    for (int i = 0; i < renderLimit; i++)
                    {
                        var partial = trait.PartialCredits[i];

                        ElementBounds toolLabel = ElementBounds.Fixed(15, currentYOffset + 2, 105, 18);
                        toolLabel.fixedY = rowBounds.fixedY + currentYOffset + 2;
                        composer.AddStaticText(partial.Name, smallToolFont, toolLabel);

                        ElementBounds toolBar = ElementBounds.Fixed(125, currentYOffset, 200, 16);
                        toolBar.fixedY = rowBounds.fixedY + currentYOffset;
                        string subBarKey = $"subbar-{trait.Name}-{i}";

                        composer.AddStatbar(toolBar, GuiStyle.FoodBarColor, subBarKey);
                        composer.GetStatbar(subBarKey).SetValues(partial.Percentage, 0f, 1f);

                        if (!string.IsNullOrEmpty(partial.Tooltip))
                        {
                            composer.AddHoverText(partial.Tooltip, CairoFont.WhiteSmallText(), 220, toolBar);
                        }

                        currentYOffset += 22;
                    }
                }

                if (!string.IsNullOrEmpty(trait.ExtraInfo))
                {
                    ElementBounds extraBounds = ElementBounds.Fixed(10, currentYOffset, containerBounds.fixedWidth - 20, 18);
                    extraBounds.fixedY = rowBounds.fixedY + currentYOffset;
                    composer.AddStaticText(trait.ExtraInfo, smallToolFont, extraBounds);
                    currentYOffset += 20;
                }

                if (!string.IsNullOrEmpty(trait.Instructions))
                {
                    ElementBounds instBounds = ElementBounds.Fixed(10, currentYOffset, containerBounds.fixedWidth - 20, 36);
                    instBounds.fixedY = rowBounds.fixedY + currentYOffset;
                    composer.AddStaticText(trait.Instructions, descFont, instBounds);
                    currentYOffset += 40;
                }

                rowBounds.fixedHeight = currentYOffset;
            }

            // 4. Set heights natively post-composition so the scroll slider calculates limits
            GuiElementScrollbar scrollbar = composer.GetScrollbar("seraphScrollbar");
            if (scrollbar != null)
            {
                scrollbar.SetHeights((float)clipBounds.fixedHeight, (float)(rowBounds.fixedY + rowBounds.fixedHeight));
            }
        }
    }
}