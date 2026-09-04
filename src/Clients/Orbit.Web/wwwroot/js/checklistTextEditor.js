// Backs ChecklistTextEditor.razor - a single contenteditable surface that looks and behaves like a
// plain multi-line textarea (free typing, Enter for a new line, Backspace to merge lines, native
// cursor/selection), except some lines can carry a real, clickable <input type="checkbox"> at their
// start. A plain HTML <textarea> can't embed an interactive child element inside its text, so this is
// the only way to get an actually-clickable checkbox living inside the same editing surface as free
// text, rather than a second, separate list below it.
//
// Each line is one child <div class="note-line"> of the container. A checklist line additionally has
// class "note-line-checklist" (and "note-line-done" when checked) and a first child
// <input type="checkbox" contenteditable="false">; the rest of the line's text lives in a
// <span class="note-line-text"> so the checkbox itself is never part of the editable text run.

const instances = new Map();

export function initialize(container, dotNetHelper, initialLinesJson) {
    const lines = normalizeLines(JSON.parse(initialLinesJson));
    render(container, lines);

    const state = { dotNetHelper };
    instances.set(container, state);

    state.onInput = () => {
        repairStrayText(container);
        notifyChanged(container, dotNetHelper);
    };
    state.onKeyDown = (event) => {
        repairStrayText(container);
        onKeyDown(event, container, dotNetHelper);
    };
    state.onClick = (event) => onClick(event, container, dotNetHelper);

    container.addEventListener('input', state.onInput);
    container.addEventListener('keydown', state.onKeyDown);
    container.addEventListener('click', state.onClick);
}

export function dispose(container) {
    const state = instances.get(container);
    if (!state) {
        return;
    }
    container.removeEventListener('input', state.onInput);
    container.removeEventListener('keydown', state.onKeyDown);
    container.removeEventListener('click', state.onClick);
    instances.delete(container);
}

export function getLinesAsJson(container) {
    return JSON.stringify(extractLines(container));
}

/// Puts lines decided somewhere else onto the surface. Everything else here flows the other way - the
/// reader types, and Blazor is told what the surface now holds - and that one-way flow is why picking a
/// suggested name for a task list's title or an inventory's name used to do nothing at all: the model
/// took the name and the box carried on showing what had been typed, until the next keystroke there
/// overwrote the model again.
///
/// The component decides when to call this; it only does so when what it has been handed differs from
/// what this surface last reported, so ordinary typing never comes back through here.
export function setLines(container, linesJson) {
    const lines = normalizeLines(JSON.parse(linesJson));
    render(container, lines);

    // The caret goes to the end of what was just put there, so somebody who took a suggested name can
    // carry on typing after it. Only on a surface that takes writing: moving the caret into a read-only
    // one would be taking the focus to a place nothing can be done.
    const lastLine = container.lastElementChild;
    if (lastLine && container.getAttribute('contenteditable') === 'true') {
        focusLine(lastLine);
    }
}

/// Called from the toolbar button - ends the current line (if not already empty) and starts a new
/// checklist line, with focus moved into it.
export function insertChecklistItem(container) {
    const selection = window.getSelection();
    let currentLine = selection && selection.anchorNode ? closestLine(selection.anchorNode, container) : null;
    currentLine ??= container.lastElementChild;

    if (currentLine && lineText(currentLine).length === 0 && !currentLine.classList.contains('note-line-checklist')) {
        // The current line is already empty plain text (e.g. a brand new note) - turn it into the
        // checklist line instead of leaving a blank line behind it. replaceWithChecklistLine detaches
        // currentLine from the document, so focus has to move to the replacement it returns, not to
        // the now-detached original.
        const replacement = replaceWithChecklistLine(currentLine, '');
        focusLine(replacement);
    } else {
        const newLine = createLineElement({ text: '', isChecklistItem: true, isChecked: false });
        if (currentLine && currentLine.parentElement === container) {
            currentLine.after(newLine);
        } else {
            container.appendChild(newLine);
        }
        focusLine(newLine);
    }
}

function onClick(event, container, dotNetHelper) {
    if (event.target instanceof HTMLInputElement && event.target.type === 'checkbox') {
        const line = event.target.closest('.note-line');
        line.classList.toggle('note-line-done', event.target.checked);
        notifyChanged(container, dotNetHelper);
    }
}

function onKeyDown(event, container, dotNetHelper) {
    if (event.key === 'Enter') {
        event.preventDefault();
        handleEnter(container);
        notifyChanged(container, dotNetHelper);
        return;
    }

    if (event.key === 'Backspace') {
        const selection = window.getSelection();
        if (!selection || !selection.isCollapsed) {
            return;
        }
        var atLineStart = selection.anchorOffset === 0 && isFirstTextNodeOfLine(selection.anchorNode, container);
        if (!atLineStart) {
            return;
        }

        var line = closestLine(selection.anchorNode, container);
        if (!line) {
            return;
        }

        if (line.classList.contains('note-line-checklist')) {
            // First Backspace at the start of a checklist line just drops the checkbox, matching the
            // familiar "outdent before delete" behavior of note apps - only a second Backspace (now
            // that it's a plain line) merges into the previous line via the browser's own handling.
            event.preventDefault();
            replaceWithChecklistLine(line, lineText(line), /* toChecklist */ false);
            focusLine(line, /* atStart */ true);
            notifyChanged(container, dotNetHelper);
            return;
        }

        var previous = line.previousElementSibling;
        if (previous) {
            event.preventDefault();
            mergeIntoPrevious(line, previous);
            notifyChanged(container, dotNetHelper);
        }
        // Otherwise (first line, plain text): let the browser's default Backspace happen.
    }
}

function handleEnter(container) {
    const selection = window.getSelection();
    let line = selection && selection.anchorNode ? closestLine(selection.anchorNode, container) : null;
    // A click in the blank space under the writing - most of a new note - leaves the caret on the
    // container rather than inside a line, and Enter then did nothing at all: the default was already
    // prevented, and there was no line to split. The last line is where such a click means, which is
    // also what insertChecklistItem falls back to. splitAtCaret treats a caret outside the line as its
    // end, so this starts a fresh line under the writing rather than cutting one in half.
    line ??= container.lastElementChild;
    if (!line) {
        return;
    }

    const isChecklist = line.classList.contains('note-line-checklist');
    const wasEmpty = lineText(line).length === 0;

    if (isChecklist && wasEmpty) {
        // Enter on an empty checklist item exits the list, turning this line back into plain text,
        // instead of piling up empty checkboxes.
        replaceWithChecklistLine(line, '', false);
        focusLine(line);
        return;
    }

    const [beforeText, afterText] = splitAtCaret(line);
    setLineText(line, beforeText);
    const newLine = createLineElement({ text: afterText, isChecklistItem: isChecklist, isChecked: false });
    line.after(newLine);
    focusLine(newLine, /* atStart */ true);
}

function mergeIntoPrevious(line, previous) {
    const previousLength = lineText(previous).length;
    setLineText(previous, lineText(previous) + lineText(line));
    line.remove();
    focusLine(previous, false, previousLength);
}

function notifyChanged(container, dotNetHelper) {
    const lines = extractLines(container);
    dotNetHelper.invokeMethodAsync('OnLinesChangedFromJs', JSON.stringify(lines));
}

function render(container, lines) {
    container.innerHTML = '';
    for (const line of lines) {
        container.appendChild(createLineElement(line));
    }
}

function normalizeLines(lines) {
    return lines && lines.length > 0 ? lines : [{ text: '', isChecklistItem: false, isChecked: false }];
}

function createLineElement(line) {
    const div = document.createElement('div');
    div.className = 'note-line';

    if (line.isChecklistItem) {
        div.classList.add('note-line-checklist');
        if (line.isChecked) {
            div.classList.add('note-line-done');
        }

        const checkbox = document.createElement('input');
        checkbox.type = 'checkbox';
        checkbox.checked = !!line.isChecked;
        checkbox.contentEditable = 'false';
        checkbox.className = 'note-line-checkbox';
        div.appendChild(checkbox);
    }

    const text = document.createElement('span');
    text.className = 'note-line-text';
    div.appendChild(text);
    setLineText(div, line.text || '');

    return div;
}

/// Rebuilds line as a checklist (or plain, when toChecklist is false) line carrying text, in place.
function replaceWithChecklistLine(line, text, toChecklist = true) {
    const replacement = createLineElement({ text, isChecklistItem: toChecklist, isChecked: false });
    line.replaceWith(replacement);
    return replacement;
}

function lineText(line) {
    const span = line.querySelector('.note-line-text');
    return span ? span.textContent : line.textContent;
}

function setLineText(line, text) {
    const span = line.querySelector('.note-line-text');
    if (span) {
        span.textContent = text;
        if (span.childNodes.length === 0) {
            // A completely childless inline element is an unreliable caret target in contenteditable -
            // Chromium can place the caret as a sibling text node of the div instead of inside the span,
            // silently detaching typed text from the line-tracking logic below. An empty text node keeps
            // the span a valid target even when there's nothing to show yet.
            span.appendChild(document.createTextNode(''));
        }
    } else {
        line.textContent = text;
    }
}

function extractLines(container) {
    return Array.from(container.children).map((line) => {
        const checkbox = line.querySelector('input[type=checkbox]');
        return {
            text: lineText(line) || '',
            isChecklistItem: !!checkbox,
            isChecked: checkbox ? checkbox.checked : false
        };
    });
}

/// An empty <span class="note-line-text"> has zero rendered width, so a click into an otherwise-empty
/// line can't actually hit-test inside it - Chromium instead drops the caret (and whatever gets typed)
/// as a plain text node sitting directly under .note-line, next to the span. This repairs any such
/// stray text back into the span after every keystroke, before the rest of this module reads line
/// state, and re-homes the caret to where it visually already appears to be (the end of the merged
/// text) so continued typing doesn't notice the fix-up happened.
function repairStrayText(container) {
    const selection = window.getSelection();
    let caretLine = null;
    let caretWasStray = false;
    if (selection && selection.rangeCount > 0 && selection.isCollapsed) {
        const anchor = selection.anchorNode;
        caretLine = closestLine(anchor, container);
        if (caretLine) {
            const span = caretLine.querySelector('.note-line-text');
            caretWasStray = !(span && (anchor === span || span.contains(anchor)));
        }
    }

    for (const line of Array.from(container.children)) {
        repairLineDom(line);
    }

    if (caretLine && caretWasStray) {
        focusLine(caretLine);
    }
}

function repairLineDom(line) {
    const checkbox = line.querySelector('input[type=checkbox]');
    let span = line.querySelector('.note-line-text');
    if (!span) {
        span = document.createElement('span');
        span.className = 'note-line-text';
        line.appendChild(span);
    }

    const strayNodes = Array.from(line.childNodes).filter((node) => node !== checkbox && node !== span);
    if (strayNodes.length === 0) {
        if (span.childNodes.length === 0) {
            span.appendChild(document.createTextNode(''));
        }
        return;
    }

    // Chromium drops stray text immediately before the span, at the point the (zero-width) empty span
    // sat when the caret landed - so it belongs at the start of whatever the span already holds.
    let strayText = '';
    for (const node of strayNodes) {
        strayText += node.textContent;
        node.remove();
    }
    setLineText(line, strayText + lineText(line));
}

function closestLine(node, container) {
    // A selection can have no anchor at all - nothing focused, or focus taken by something outside this
    // editor while a key is on its way. Every path that asks which line the caret is in comes through
    // here, so this is where "nowhere" is answered rather than thrown: reading nodeType off null was an
    // uncaught TypeError, and an uncaught one in a keydown handler takes the page with it.
    if (!node) {
        return null;
    }

    let element = node.nodeType === Node.TEXT_NODE ? node.parentElement : node;
    while (element && element !== container) {
        if (element.classList && element.classList.contains('note-line')) {
            return element;
        }
        element = element.parentElement;
    }
    return null;
}

function isFirstTextNodeOfLine(node, container) {
    const line = closestLine(node, container);
    if (!line) {
        return false;
    }
    const span = line.querySelector('.note-line-text');
    if (!span) {
        return true;
    }
    // True when the caret's text node is the span's own first (and, for a single-line span, only) child.
    return node === span || (node === span.firstChild);
}

/// Splits line's text at the current caret position, returning [beforeCaret, afterCaret].
function splitAtCaret(line) {
    const selection = window.getSelection();
    const span = line.querySelector('.note-line-text');
    const fullText = lineText(line);
    if (!selection || !span || selection.rangeCount === 0) {
        return [fullText, ''];
    }

    const range = selection.getRangeAt(0);
    if (!span.contains(range.startContainer)) {
        return [fullText, ''];
    }

    const preCaretRange = range.cloneRange();
    preCaretRange.selectNodeContents(span);
    preCaretRange.setEnd(range.startContainer, range.startOffset);
    const beforeText = preCaretRange.toString();
    return [beforeText, fullText.slice(beforeText.length)];
}

function focusLine(line, atStart = false, offset = null) {
    const span = line.querySelector('.note-line-text');
    const target = span || line;
    if (target.childNodes.length === 0) {
        target.appendChild(document.createTextNode(''));
    }

    const textNode = target.firstChild;
    const selection = window.getSelection();
    const range = document.createRange();
    const caretOffset = offset !== null ? offset : (atStart ? 0 : textNode.textContent.length);
    range.setStart(textNode, Math.min(caretOffset, textNode.textContent.length));
    range.collapse(true);
    selection.removeAllRanges();
    selection.addRange(range);
    line.scrollIntoView({ block: 'nearest' });
}
