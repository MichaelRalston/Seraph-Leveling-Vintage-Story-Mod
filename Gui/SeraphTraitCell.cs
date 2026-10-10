using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using SeraphLeveling.Messages;

namespace SeraphLeveling.Gui
{
    public class SeraphTraitCell : GuiElement, IGuiElementCell
    {
        private readonly ICoreClientAPI capi;
        private readonly ProgressReportContent traitData;
        private readonly Action OnStateChanged;
        private readonly bool isExpanded;
        private readonly float scrollOffset;
        private float currentYOffset = 26;

        // Structural elements
        private GuiElementStatbar progressStatBar;
        private GuiElementHoverText tooltipHover;
        private readonly List<(GuiElementStatbar Bar, GuiElementHoverText Hover)> subToolElements =
            new List<(GuiElementStatbar, GuiElementHoverText)>();
        private GuiElementRichtext titleRichText;
        private GuiElementRichtext buttonRichText;
        private GuiElementRichtext extraRichText;
        private GuiElementRichtext instructionsRichText;
        private readonly GuiComposer parentComposer;
        private readonly List<GuiElementRichtext> subToolRichTexts = new List<GuiElementRichtext>();

        // Coordinates bounding frame for manual button mapping
        private ElementBounds buttonClickBounds;

        // Fulfill the mandatory interface boundary property
        new public ElementBounds Bounds => base.Bounds;

        public SeraphTraitCell(ICoreClientAPI capi, ElementBounds bounds, ProgressReportContent traitData, bool isExpanded, float scrollOffset, GuiComposer parentComposer, Action onStateChanged)
            : base(capi, bounds)
        {
            this.capi = capi;
            this.traitData = traitData;
            this.isExpanded = isExpanded;
            this.OnStateChanged = onStateChanged;
            this.scrollOffset = scrollOffset;
            this.parentComposer = parentComposer;

            // 1. Calculate dynamic heights exactly before defining elements
            int currentHeight = 24;
            if (isExpanded && traitData.PartialCredits != null)
            {
                currentHeight += traitData.PartialCredits.Length * 22;
            }
            if (!string.IsNullOrEmpty(traitData.ExtraInfo)) currentHeight += 20;
            if (!string.IsNullOrEmpty(traitData.Instructions)) currentHeight += 40;

            Bounds.fixedHeight = currentHeight;

            // 2. Initialize and construct layouts
            InitializeSubElements();
        }

        private void InitializeSubElements()
        {
            RenderSubText();
            RenderMainProgressBar();
            RenderPartialCredits();
            RenderExtraInfo();
            RenderInstructions();
        }

        private void RenderSubText()
        {
            CairoFont titleFont = CairoFont.WhiteSmallText();
            titleFont.UnscaledFontsize = 16;

            ElementBounds textBounds = ElementBounds.Fixed(0, 4, 120, 22).WithParent(Bounds);

            // Build VTML nodes and bind them to the RichText layout
            RichTextComponentBase[] nodes = VtmlUtil.Richtextify(capi, traitData.Name, titleFont);
            titleRichText = new GuiElementRichtext(capi, nodes, textBounds);
            parentComposer.AddInteractiveElement(titleRichText);
            titleRichText.RecomposeText();
        }

        private void RenderMainProgressBar()
        {
            ElementBounds barBounds = ElementBounds.Fixed(125, 0, 200, 22).WithParent(Bounds);
            progressStatBar = new GuiElementStatbar(capi, barBounds, GuiStyle.XPBarColor, false, false);

            float clampedPct = (float)Math.Max(0.0, Math.Min(1.0, traitData.Percentage));
            progressStatBar.SetValues(clampedPct, 0f, 1f);

            if (!string.IsNullOrEmpty(traitData.Tooltip))
            {
                tooltipHover = new GuiElementHoverText(capi, traitData.Tooltip, CairoFont.WhiteSmallText(), 220, barBounds);
            }
        }

        private void RenderPartialCredits()
        {
            if (traitData.PartialCredits == null || traitData.PartialCredits.Length == 0) return;
            CairoFont btnFont = CairoFont.WhiteSmallText();
            btnFont.UnscaledFontsize = 16;

            buttonClickBounds = ElementBounds.Fixed(335, 0, 24, 22).WithParent(Bounds);

            string btnVtml = isExpanded
                ? "<font color=\"#ffaa00\"><b>−</b></font>"
                : "<font color=\"#00ffaa\"><b>+</b></font>";

            RichTextComponentBase[] btnNodes = VtmlUtil.Richtextify(capi, btnVtml, btnFont);
            buttonRichText = new GuiElementRichtext(capi, btnNodes, buttonClickBounds);
            parentComposer.AddInteractiveElement(buttonRichText);
            buttonRichText.RecomposeText();

            CairoFont smallToolFont = CairoFont.WhiteSmallText().WithColor([0.6, 0.6, 0.6, 1.0]);

            for (int i = 0; i < traitData.PartialCredits.Length; i++)
            {
                var partial = traitData.PartialCredits[i];

                // 1. Tool Text Label Bounds & VTML Node Compilation
                ElementBounds toolLabelBounds = ElementBounds.Fixed(15, currentYOffset + 2, 105, 18).WithParent(Bounds);
                RichTextComponentBase[] toolNodes = VtmlUtil.Richtextify(capi, partial.Name, smallToolFont);
                var subRichLabel = new GuiElementRichtext(capi, toolNodes, toolLabelBounds);
                subToolRichTexts.Add(subRichLabel);
                parentComposer.AddInteractiveElement(subRichLabel);
                subRichLabel.RecomposeText();

                // 2. Tool Progress Bar Alignment
                ElementBounds toolBarBounds = ElementBounds.Fixed(125, currentYOffset, 200, 16).WithParent(Bounds);
                var subBar = new GuiElementStatbar(capi, toolBarBounds, GuiStyle.FoodBarColor, false, false);
                subBar.SetValues(partial.Percentage, 0f, 1f);

                // 3. Tool Hover Tooltip Setup
                GuiElementHoverText subHover = null;
                if (!string.IsNullOrEmpty(partial.Tooltip))
                {
                    subHover = new GuiElementHoverText(capi, partial.Tooltip, CairoFont.WhiteSmallText(), 220, toolBarBounds);
                }

                // Add structural pairs to tracker array
                subToolElements.Add((subBar, subHover));

                // Shift down for the next stacked sub-row
                currentYOffset += 22;
                if (!isExpanded) return;
            }
        }

        private void RenderExtraInfo()
        {
            if (string.IsNullOrEmpty(traitData.ExtraInfo)) return;

            CairoFont smallFont = CairoFont.WhiteSmallText().WithColor([0.6, 0.6, 0.6, 1.0]);
            ElementBounds extraBounds = ElementBounds.Fixed(10, currentYOffset, 345, 18).WithParent(Bounds);

            // Richtextify extra notes support string primitives or embedded tags
            RichTextComponentBase[] nodes = VtmlUtil.Richtextify(capi, traitData.ExtraInfo, smallFont);
            extraRichText = new GuiElementRichtext(capi, nodes, extraBounds);
            parentComposer.AddInteractiveElement(extraRichText);
            extraRichText.RecomposeText();

            // Accumulate tail heights
            currentYOffset += 20;
        }

        private void RenderInstructions()
        {
            if (string.IsNullOrEmpty(traitData.Instructions)) return;

            CairoFont smallFont = CairoFont.WhiteSmallText().WithColor([0.6, 0.6, 0.6, 1.0]);
            ElementBounds extraBounds = ElementBounds.Fixed(10, currentYOffset, 345, 36).WithParent(Bounds);

            // Richtextify extra notes support string primitives or embedded tags
            RichTextComponentBase[] nodes = VtmlUtil.Richtextify(capi, traitData.Instructions, smallFont);
            instructionsRichText = new GuiElementRichtext(capi, nodes, extraBounds);
            parentComposer.AddInteractiveElement(instructionsRichText);
            instructionsRichText.RecomposeText();

            // Accumulate tail heights
            currentYOffset += 20;
        }

        // =======================================================
        // MANDATORY INTERFACE METHODS FOR DRAWING AND SCROLLING
        // =======================================================

        public void UpdateCellItems()
        {
            // Text textures are pre-allocated on initialization, so leave blank
        }

        public void UpdateCellHeight()
        {
            // Enforces size tracking updates inside the list scrollbox engine context
            Bounds.CalcWorldBounds();
        }

        public void OnRenderInteractiveElements(ICoreClientAPI api, float deltaTime)
        {
            titleRichText?.RenderInteractiveElements(deltaTime);
            buttonRichText?.RenderInteractiveElements(deltaTime);
            progressStatBar?.RenderInteractiveElements(deltaTime);

            // Render sub-tool components arrays loop
            if (isExpanded)
            {
                for (int i = 0; i < subToolElements.Count; i++)
                {
                    if (i < subToolRichTexts.Count) subToolRichTexts[i]?.RenderInteractiveElements(deltaTime);
                    subToolElements[i].Bar?.RenderInteractiveElements(deltaTime);
                    subToolElements[i].Hover?.RenderInteractiveElements(deltaTime);
                }
            }

            // Render remaining data strings safely onto the layout canvas frame
            extraRichText?.RenderInteractiveElements(deltaTime);
            instructionsRichText?.RenderInteractiveElements(deltaTime);

            // Draw your primary tooltip on top
            tooltipHover?.RenderInteractiveElements(deltaTime);
        }

        // =======================================================
        // MANDATORY MOUSE INTERACTION ROUTERS
        // =======================================================

        public void OnMouseDownOnElement(MouseEvent args, int elementIndex)
        {
            if (buttonClickBounds == null) return;

            // Determine the baseline location of the button relative to the row block
            double absoluteBtnX = Bounds.renderX + buttonClickBounds.fixedX;

            // ADJUSTMENT: Factor in the active scroll translation depth!
            double absoluteBtnY = Bounds.renderY + buttonClickBounds.fixedY - scrollOffset;

            // Check bounds intersections against the corrected matrix paths
            bool clickedButton = args.X >= absoluteBtnX &&
                                 args.X <= (absoluteBtnX + buttonClickBounds.fixedWidth) &&
                                 args.Y >= absoluteBtnY &&
                                 args.Y <= (absoluteBtnY + buttonClickBounds.fixedHeight);

            if (clickedButton)
            {
                OnStateChanged?.Invoke();
                args.Handled = true;
            }
        }
        public void OnMouseMoveOnElement(MouseEvent args, int elementIndex)
        {
            // Route inputs natively to fulfill hover descriptions checks
        }
        public void OnMouseUpOnElement(MouseEvent args, int elementIndex)
        {
            // Route structural mouse releases safely
        }
        public override void Dispose()
        {
            base.Dispose();
            progressStatBar?.Dispose();
            tooltipHover?.Dispose();
            foreach (var (Bar, Hover) in subToolElements)
            {
                Bar?.Dispose();
                Hover?.Dispose();
            }
            titleRichText?.Dispose();
            buttonRichText?.Dispose();
            extraRichText?.Dispose();
            instructionsRichText?.Dispose();
            foreach (var richText in subToolRichTexts)
            {
                richText?.Dispose();
            }
        }
    }
}