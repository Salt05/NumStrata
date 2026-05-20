# UI Layout Design and Implementation Specification

**File Name:** UI_Design_System_Guidelines.md

## 1. Global UI Configuration

When initializing any new UI scene or prefab, apply these **Canvas Scaler** settings to keep a consistent coordinate system:

- **UI Scale Mode:** `ScaleWithScreenSize`
- **Reference Resolution:** `1080 x 1920` (portrait)
- **Screen Match Mode:** `MatchWidthOrHeight`
- **Match Value:** `0.5` (balances wide and tall aspect ratios)

---

## 2. Mandatory Hierarchy Structure

Every UI scene must follow this nested structure to handle device safe zones (e.g., notches, home indicators) and fixed aspect ratios:

1. **Canvas** (root)
2. **UI_SafeArea_Container** (direct child of Canvas)

- **Anchors:** `(0,0)` to `(1,1)`
- **Offsets:** `0,0,0,0`
- **Components:**
  - `NumStrata.UI.SafeArea`: Maps anchors to `Screen.safeArea`.
  - `UnityEngine.UI.AspectRatioFitter`: Set **Aspect Mode** to `FitInParent` and **Aspect Ratio** to `0.5625` (9/16). This creates a safe column for game content.

3. **Content Panels** (children of SafeArea container)

- Divide the screen vertically using a `VerticalLayoutGroup` on the container or by manually anchoring panels to top, middle, and bottom.

---

## 3. Responsive Layout Mechanisms

To prevent stretched or warped UI elements, use a container-content abstraction:

### A. Layout Groups (Flexbox Behavior)

- Use `HorizontalLayoutGroup` or `VerticalLayoutGroup` for repeating elements (grids, button rows).
- **Required Settings:**
  - `Child Control Size`: enable both width and height.
  - `Child Force Expand`: enable to fill available space.
- Use **Layout Element** components on children to define `Min`, `Preferred`, or `Flexible` sizes when needed.

### B. Aspect Ratio Fitter (Non-Distortion)

- For any visual element that must maintain its shape (buttons, tiles, icons), attach an `AspectRatioFitter`.
- **Mode:** `FitInParent` (usually for children of layout groups) or `WidthControlsHeight`.
- This ensures that even if the parent container stretches to fill a screen, the inner graphic remains a perfect square or rectangle.

---

## 4. Specialized Scaling Scripts

Use the following custom scripts for dynamic layout adjustments:

### 1. SafeArea.cs

- **Logic:** Converts `Screen.safeArea` pixels into normalized `anchorMin/Max` coordinates.
- **Application:** Attach to the top-level container in every scene.

### 2. ResponsiveBoardSpacing.cs

- **Logic:** Dynamically calculates the `spacing` property of a `VerticalLayoutGroup` as a percentage of the object's width.
- **Formula:** `spacing = width * spacingRatio` (default ratio: `-0.1064` for overlapping tiles).
- **Application:** Use on grid or board objects where tile spacing must scale with the screen width.

### 3. UISizeSync.cs

- **Logic:** Forces a RectTransform to match the width or height of a target RectTransform (with multipliers and offsets).
- **Application:** Use for background dimmer frames or header bars that must resize based on dynamic content length.

---

## 5. Implementation Workflow for New Scenes

When processing a new UI design or asset set, follow these steps:

1. **Scene Initialization:** Set up the `Canvas` and `UI_SafeArea_Container` as per Section 2.
2. **Sectional Partitioning:** Identify zones (e.g., HUD, GameBoard, Footer). Create a `RectTransform` for each.
3. **Horizontal/Vertical Logic:** If elements are aligned in a row or column, apply the appropriate `LayoutGroup`.
4. **Graphic Integrity:** For every `Image` component representing a UI asset, evaluate if it needs an `AspectRatioFitter`. If the asset is a button or tile, **always** apply it.
5. **Dynamic Spacing:** If the design involves a grid that feels loose on different resolutions, apply `ResponsiveBoardSpacing` to maintain visual density.
6. **Anchor Check:** Ensure no UI elements (except the background) have fixed pixel positions. All positions should be governed by anchors or layout groups.

---

**Policy Note:** Do not use hardcoded `Vector2` positions in scripts for UI. Always manipulate `RectTransform` properties or use the layout system to ensure resolution independence.
