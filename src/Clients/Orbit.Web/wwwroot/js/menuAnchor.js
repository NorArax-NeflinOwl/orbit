// A dropdown that lives inside a scrolling list is clipped by that list, however high its z-index: the
// group message menu opened underneath the composer and lost half its entries. Switching it to fixed
// positioning takes it out of the scroller entirely, and these coordinates are what then put it back
// next to the button it belongs to - above that button instead of below when there is no room.
const GAP_PIXELS = 4;

export function anchorToTrigger(dropdown, triggerSelector) {
    const trigger = dropdown.parentElement?.querySelector(triggerSelector);
    if (!trigger) {
        return;
    }

    // Cleared first so the measurement below reads the dropdown's natural size rather than the size a
    // previous anchoring left it at.
    dropdown.style.position = 'fixed';
    dropdown.style.right = 'auto';
    const triggerBox = trigger.getBoundingClientRect();
    const dropdownBox = dropdown.getBoundingClientRect();

    const fitsBelow = triggerBox.bottom + GAP_PIXELS + dropdownBox.height <= window.innerHeight;
    const top = fitsBelow
        ? triggerBox.bottom + GAP_PIXELS
        : Math.max(GAP_PIXELS, triggerBox.top - GAP_PIXELS - dropdownBox.height);
    const left = Math.min(
        Math.max(GAP_PIXELS, triggerBox.right - dropdownBox.width),
        window.innerWidth - dropdownBox.width - GAP_PIXELS);

    dropdown.style.top = `${top}px`;
    dropdown.style.left = `${left}px`;
}

/// The same trick for a panel that belongs to a *field* rather than to a button: the name suggestions,
/// which have to read as a list hanging off the box being typed into. Two differences from the menu
/// above, both of which come from that. It lines up with the field's left edge and takes its width,
/// because a list of completions narrower or wider than what it completes reads as being about
/// something else. And the field is looked for as a sibling of the panel rather than as a trigger
/// inside it - the panel is drawn after the box, not around it, which is why this cannot simply be CSS:
/// on the item rows the box and the panel sit side by side in a flex row, so "underneath" is a position
/// only measurement can find.
export function anchorToField(panel, fieldSelector) {
    const field = panel.parentElement?.querySelector(fieldSelector);
    if (!field) {
        return;
    }

    panel.style.position = 'fixed';
    panel.style.right = 'auto';
    // Width first, then measure: the height depends on how many suggestions fit across that width, and
    // reading it before the width is set measures a panel of the wrong shape.
    const fieldBox = field.getBoundingClientRect();
    panel.style.width = `${fieldBox.width}px`;
    const panelBox = panel.getBoundingClientRect();

    const fitsBelow = fieldBox.bottom + GAP_PIXELS + panelBox.height <= window.innerHeight;
    const top = fitsBelow
        ? fieldBox.bottom + GAP_PIXELS
        : Math.max(GAP_PIXELS, fieldBox.top - GAP_PIXELS - panelBox.height);
    const left = Math.min(
        Math.max(GAP_PIXELS, fieldBox.left),
        Math.max(GAP_PIXELS, window.innerWidth - panelBox.width - GAP_PIXELS));

    panel.style.top = `${top}px`;
    panel.style.left = `${left}px`;
}
