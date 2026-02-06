# UI-SPEC — Panel behavior

## 1. Edge hover trigger
- Edge zone: right edge within [2..6] px from WorkingArea.Right
- Dwell: 100 ms continuous presence within edge zone
- Polling:
  - Normal: 80–120 ms
  - Near edge: 16 ms (or tighter if needed)

## 2. Show behavior
- Show on dwell success, unless fullscreen suppression is active
- Fade-in duration: 180 ms recommended
- Dock: panel's right aligned to WorkingArea.Right, Top = WorkingArea.Top, Height = WorkingArea.Height
- NoActivate, ToolWindow style

## 3. Hide behavior
- Hide when cursor leaves panel and edge zone, after grace period 250 ms
- Fade-out duration: 160 ms recommended
- After fade-out, Hide() the window to avoid draw cost

## 4. Hotkey
- Default: Win+Alt+C
- Action: toggle panel
- Works during fullscreen (configurable later)

## 5. Fullscreen suppression
- If foreground window covers monitor bounds (with small tolerance), suppress hover-open
- Exclude our own panel window from detection
