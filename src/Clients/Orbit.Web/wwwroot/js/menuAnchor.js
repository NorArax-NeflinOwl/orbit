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
