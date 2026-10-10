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

        // Statbars are lightweight rendering primitives that can handle their own boundaries perfectly
        private GuiElementStatbar progressStatBar;
        private GuiElementHoverText tooltipHover;
        private readonly List<(GuiElementStatbar Bar, GuiElementHoverText Hover)> subToolElements = 
            new List<(GuiElementStatbar, GuiElementHoverText)>();

        // Pre-compiled engine textures
        private LoadedTexture titleTexture;
        private LoadedTexture buttonTexture;
        private LoadedTexture extraTexture;
        private LoadedTexture instructionsTexture;
        private readonly List<LoadedTexture> subToolTextures = new List<LoadedTexture>();

        private ElementBounds buttonClickBounds;

        new public ElementBounds Bounds => base.Bounds;

        public SeraphTraitCell(ICoreClientAPI capi, ElementBounds bounds, ProgressReportContent traitData, bool isExpanded, float scrollOffset, Action onStateChanged)
            : base(capi, bounds)
        {
            this.capi = capi;
            this.traitData = traitData;
            this.isExpanded = isExpanded;
            this.OnStateChanged = onStateChanged;
            this.scrollOffset = scrollOffset;

            UpdateCellHeight();
            InitializeTexturesAndBars();
        }

        public void UpdateCellHeight()
        {
            int currentHeight = 24;
            if (isExpanded && traitData.PartialCredits != null)
            {
                currentHeight += traitData.PartialCredits.Length * 22;
            }
            if (!string.IsNullOrEmpty(traitData.ExtraInfo)) currentHeight += 20;
            if (!string.IsNullOrEmpty(traitData.Instructions)) currentHeight += 40;

            Bounds.fixedHeight = currentHeight;
        }

        private void InitializeTexturesAndBars()
        {
            CairoFont baseFont = CairoFont.WhiteSmallText();
            baseFont.UnscaledFontsize = 16;
            
            TextTextureUtil textureUtil = new TextTextureUtil(capi);

            // 1. Bake strings into standalone textures cleanly using engine utilities
            titleTexture = textureUtil.GenTextTexture(traitData.Name, baseFont);

            if (traitData.PartialCredits != null && traitData.PartialCredits.Length > 0)
            {
                buttonClickBounds = ElementBounds.Fixed(335, 0, 24, 22).WithParent(Bounds);
                string btnText = isExpanded ? "−" : "+";
                CairoFont btnFont = isExpanded ? baseFont.Clone().WithColor([1, 0.66, 0, 1]) : baseFont.Clone().WithColor(new double[] { 0, 1, 0.66, 1 });
                
                buttonTexture = textureUtil.GenTextTexture(btnText, btnFont);

                CairoFont smallToolFont = CairoFont.WhiteSmallText().WithColor([0.6, 0.6, 0.6, 1.0]);
                for (int i = 0; i < traitData.PartialCredits.Length; i++)
                {
                    var partial = traitData.PartialCredits[i];
                    subToolTextures.Add(textureUtil.GenTextTexture(partial.Name, smallToolFont));

                    ElementBounds toolBarBounds = ElementBounds.Fixed(125, currentYOffset, 200, 16).WithParent(Bounds);
                    var subBar = new GuiElementStatbar(capi, toolBarBounds, GuiStyle.FoodBarColor, false, false);
                    subBar.SetValues(partial.Percentage, 0f, 1f);

                    GuiElementHoverText subHover = null;
                    if (!string.IsNullOrEmpty(partial.Tooltip))
                    {
                        subHover = new GuiElementHoverText(capi, partial.Tooltip, CairoFont.WhiteSmallText(), 220, toolBarBounds);
                    }
                    subToolElements.Add((subBar, subHover));

                    currentYOffset += 22;
                    if (!isExpanded) break;
                }
            }

            // 2. Setup Primary Statbars
            ElementBounds barBounds = ElementBounds.Fixed(125, 0, 200, 22).WithParent(Bounds);
            progressStatBar = new GuiElementStatbar(capi, barBounds, GuiStyle.XPBarColor, false, false);
            progressStatBar.SetValues((float)Math.Max(0.0, Math.Min(1.0, traitData.Percentage)), 0f, 1f);

            if (!string.IsNullOrEmpty(traitData.Tooltip))
            {
                tooltipHover = new GuiElementHoverText(capi, traitData.Tooltip, CairoFont.WhiteSmallText(), 220, barBounds);
            }

            CairoFont blockFont = CairoFont.WhiteSmallText().WithColor(new double[] { 0.6, 0.6, 0.6, 1.0 });
            if (!string.IsNullOrEmpty(traitData.ExtraInfo))
            {
                extraTexture = textureUtil.GenTextTexture(traitData.ExtraInfo, blockFont);
                currentYOffset += 20;
            }
            if (!string.IsNullOrEmpty(traitData.Instructions))
            {
                instructionsTexture = textureUtil.GenTextTexture(traitData.Instructions, blockFont);
                currentYOffset += 20;
            }
        }

        // =====================================================================
        // INTENDED ENGINE SURFACE DRAWING LIFECYCLE
        // =====================================================================

        public void DrawToSurface(GuiElementCellList<ProgressReportContent> list, double x, double y)
        {
            // Handled during layout interactive rendering phases
        }

        public void OnRenderInteractiveElements(ICoreClientAPI api, float deltaTime)
        {
            // Synchronize boundaries matrix updates for nested components per frame
            progressStatBar?.Bounds.CalcWorldBounds();
            tooltipHover?.Bounds.CalcWorldBounds();
            foreach (var (Bar, Hover) in subToolElements)
            {
                Bar?.Bounds.CalcWorldBounds();
                Hover?.Bounds.CalcWorldBounds();
            }

            // Render primitive elements
            progressStatBar?.RenderInteractiveElements(deltaTime);
            foreach (var (Bar, Hover) in subToolElements)
            {
                Bar?.RenderInteractiveElements(deltaTime);
                Hover?.RenderInteractiveElements(deltaTime);
            }
            tooltipHover?.RenderInteractiveElements(deltaTime);

            // Draw pre-baked text textures cleanly onto screen-space absolute coordinates
            double renderX = Bounds.renderX;
            double renderY = Bounds.renderY;

            if (titleTexture != null)
                api.Render.Render2DTexturePremultipliedAlpha(titleTexture.TextureId, renderX, renderY + 4, titleTexture.Width, titleTexture.Height);

            if (buttonTexture != null)
                api.Render.Render2DTexturePremultipliedAlpha(buttonTexture.TextureId, renderX + 335, renderY, buttonTexture.Width, buttonTexture.Height);

            int textYOffset = 26;
            if (isExpanded)
            {
                for (int i = 0; i < subToolTextures.Count; i++)
                {
                    var tex = subToolTextures[i];
                    api.Render.Render2DTexturePremultipliedAlpha(tex.TextureId, renderX + 15, renderY + textYOffset + 2, tex.Width, tex.Height);
                    textYOffset += 22;
                }
            }

            if (extraTexture != null)
            {
                api.Render.Render2DTexturePremultipliedAlpha(extraTexture.TextureId, renderX + 10, renderY + textYOffset, extraTexture.Width, extraTexture.Height);
                textYOffset += 20;
            }

            if (instructionsTexture != null)
            {
                api.Render.Render2DTexturePremultipliedAlpha(instructionsTexture.TextureId, renderX + 10, renderY + textYOffset, instructionsTexture.Width, instructionsTexture.Height);
            }
        }

        public void OnMouseDownOnElement(MouseEvent args, int elementIndex)
        {
            if (buttonClickBounds == null) return;

            double absoluteBtnX = Bounds.renderX + buttonClickBounds.fixedX;
            double absoluteBtnY = Bounds.renderY + buttonClickBounds.fixedY - scrollOffset;

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

        public void OnMouseMoveOnElement(MouseEvent args, int elementIndex) { }
        public void OnMouseUpOnElement(MouseEvent args, int elementIndex) { }

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

            // Dispose texture asset bindings from VRAM cleanly to prevent memory leaks
            titleTexture?.Dispose();
            buttonTexture?.Dispose();
            extraTexture?.Dispose();
            instructionsTexture?.Dispose();
            foreach (var tex in subToolTextures) tex?.Dispose();
        }
    }
}
