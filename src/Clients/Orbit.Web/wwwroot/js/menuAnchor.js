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

// Arrow keys, Enter and Escape for the suggestions panel.
//
// The keys happen in the *field*, which the Blazor component does not own - it is drawn after the box,
// not around it - so this is where they are caught. Listening on `document` in the capture phase rather
// than on the field itself, which is not a detail but the whole reason this works: a name is also typed
// into the contenteditable that TitledDescription draws, and checklistTextEditor.js has its own keydown
// listener there which unconditionally preventDefaults Enter and inserts a line. A listener on the same
// element would run after that one and arrive to find the line already inserted. Capture on the document
// runs before every listener on the target and its ancestors, so stopping the event here stops it dead.
let activeSuggestions = null;
let listening = false;

export function bindSuggestionKeys(panel, fieldSelector, dotNetRef) {
    const field = panel.parentElement?.querySelector(fieldSelector);
    if (!field) {
        return;
    }

    // One panel is open at a time - one field has focus - so the latest binding simply replaces the
    // previous one. Re-bound on every render because Blazor hands out a fresh panel element each time
    // the offered names change.
    activeSuggestions = { panel, field, dotNetRef };
    if (!listening) {
        document.addEventListener('keydown', onSuggestionKeyDown, true);
        listening = true;
    }
}

export function unbindSuggestionKeys() {
    activeSuggestions = null;
}

function onSuggestionKeyDown(event) {
    const active = activeSuggestions;
    // isConnected, because the panel is removed from the page without anything telling us: the component
    // simply stops drawing it once there is nothing to offer.
    if (!active || !active.panel.isConnected) {
        return;
    }

    const inTheField = event.target === active.field || active.field.contains(event.target);
    if (!inTheField) {
        return;
    }

    const options = [...active.panel.querySelectorAll('.name-suggestion-option')];
    if (options.length === 0) {
        return;
    }

    const current = options.findIndex(option => option.classList.contains('is-active'));

    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        stop(event);
        const step = event.key === 'ArrowDown' ? 1 : -1;
        // Wraps, and an untouched list starts at the top going down and at the bottom going up - so one
        // press of either key always lands somewhere, which is what somebody reaching for a list expects.
        const next = current < 0
            ? (step === 1 ? 0 : options.length - 1)
            : (current + step + options.length) % options.length;
        highlight(options, next, active.field);
        return;
    }

    if (event.key === 'Enter') {
        // Only when something is highlighted. Enter otherwise keeps the meaning it already has on these
        // forms - on the task editor it submits, which is "Add item" - and swallowing that to close a
        // list nobody was choosing from would break typing an entry and pressing Enter for the next.
        if (current < 0) {
            return;
        }

        stop(event);
        options[current].click();
        return;
    }

    if (event.key === 'Escape') {
        stop(event);
        active.dotNetRef.invokeMethodAsync('DismissSuggestions');
    }
}

function highlight(options, index, field) {
    options.forEach((option, at) => {
        const isActive = at === index;
        option.classList.toggle('is-active', isActive);
        option.setAttribute('aria-selected', isActive ? 'true' : 'false');
        if (isActive) {
            // The field keeps the focus - a listbox is read out through the box being typed into, not by
            // moving into it - so this is what tells a screen reader which name is on offer right now.
            field.setAttribute('aria-activedescendant', option.id);
            option.scrollIntoView({ block: 'nearest' });
        }
    });
}

function stop(event) {
    event.preventDefault();
    event.stopPropagation();
}
