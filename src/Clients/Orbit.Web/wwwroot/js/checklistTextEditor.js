// Backs ChecklistTextEditor.razor - a single contenteditable surface that looks and behaves like a
// plain multi-line textarea (free typing, Enter for a new line, Backspace to merge lines, native
// cursor/selection), except some lines can carry a real, clickable box at their start. A plain HTML
// <textarea> can't embed an interactive child element inside its text, so this is the only way to get an
// actually-clickable box living inside the same editing surface as free text, rather than a second,
// separate list below it.
//
// Each line is one child <div class="note-line"> of the container. A checklist line additionally has
// class "note-line-checklist" (and "note-line-done"/"note-line-failed" when answered) and a first child
// <button class="note-line-tick" contenteditable="false">; the line's text lives in a
// <span class="note-line-text"> so the box itself is never part of the editable text run. An empty
// line's span holds a <br> and nothing else - see setLineText.
//
// Who decides what: typing inside one line is the browser's, as in any text field. Every edit that
// changes the shape of the lines - Enter, Backspace at the head of a line, Delete at its end, typing over
// a selection that spans lines, a cut, a tick - is decided in C# (NoteSurfaceEdits, through the
// component's Edit method): this module reports the lines and the selection, and draws what comes back,
// caret included. The call is synchronous (invokeMethod, which Blazor WebAssembly allows), because a key
// has to be let through or stopped before its handler returns.

const instances = new Map();

export function initialize(container, dotNetHelper, initialLinesJson) {
    render(container, normalizeLines(JSON.parse(initialLinesJson)));

    const state = { dotNetHelper, selectionBefore: null };
    instances.set(container, state);

    state.onBeforeInput = (event) => onBeforeInput(event, container, state);
    state.onInput = (event) => onInput(event, container, state);
    state.onKeyDown = (event) => onKeyDown(event, container, state);
    state.onClick = (event) => onClick(event, container, state);
    state.onCopy = (event) => onCopy(event, container, state, /* isCut */ false);
    state.onCut = (event) => onCopy(event, container, state, /* isCut */ true);

    container.addEventListener('beforeinput', state.onBeforeInput);
    container.addEventListener('input', state.onInput);
    container.addEventListener('keydown', state.onKeyDown);
    container.addEventListener('click', state.onClick);
    container.addEventListener('copy', state.onCopy);
    container.addEventListener('cut', state.onCut);
}

export function dispose(container) {
    const state = instances.get(container);
    if (!state) {
        return;
    }
    container.removeEventListener('beforeinput', state.onBeforeInput);
    container.removeEventListener('input', state.onInput);
    container.removeEventListener('keydown', state.onKeyDown);
    container.removeEventListener('click', state.onClick);
    container.removeEventListener('copy', state.onCopy);
    container.removeEventListener('cut', state.onCut);
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
    if (isWritable(container)) {
        const last = lines.length - 1;
        select(container, { line: last, offset: (lines[last].text || '').length });
    }
}

/// Called from the toolbar button: an empty line becomes a checklist line, anything else gets a new one
/// under it - see NoteSurfaceEdits.StartChecklistItem. Waits a turn first so the component's call into
/// here has returned before this calls back into it.
export async function insertChecklistItem(container) {
    await Promise.resolve();
    const state = instances.get(container);
    if (!state || !isWritable(container)) {
        return;
    }

    const answer = ask(container, state, 'checklistItem');
    if (answer) {
        draw(container, answer.lines);
        select(container, answer.anchor, answer.focus);
    }
}

function isWritable(container) {
    return container.getAttribute('contenteditable') === 'true';
}

/// Hands C# the surface as it is now and one edit to make on it; answers what the surface should hold
/// afterwards, or null when the edit is the browser's to make.
function ask(container, state, command, extra = {}) {
    const surface = readSurface(container);
    const request = { command, ...surface, at: performance.now(), ...extra };
    const answer = state.dotNetHelper.invokeMethod('Edit', JSON.stringify(request));
    return answer ? JSON.parse(answer) : null;
}

/// Draws an answer, puts the caret where it says, and tells Blazor - the way every edit ends.
function show(container, state, answer, placeSelection = true) {
    draw(container, answer.lines);
    if (placeSelection) {
        select(container, answer.anchor, answer.focus);
    }
    notifyChanged(container, state.dotNetHelper);
}

function onKeyDown(event, container, state) {
    if (!isWritable(container) || event.isComposing) {
        return;
    }

    repairStrayText(container);
    const command = commandFor(event);
    if (!command) {
        return;
    }

    const answer = ask(container, state, command);
    if (answer) {
        event.preventDefault();
        show(container, state, answer);
    }
}

function commandFor(event) {
    switch (event.key) {
        case 'Enter':
            return 'enter';
        case 'Backspace':
            return 'backspace';
        case 'Delete':
            return 'delete';
        default:
            return null;
    }
}

/// Typing over a selection that spans lines. Left to the browser, it glues the lines' elements together
/// - a box ends up in the middle of a sentence, or the words land outside any line - so the words go
/// through the same C# every other change of shape does. A drag and drop is left alone: where it drops
/// is not where the selection is.
function onBeforeInput(event, container, state) {
    if (!isWritable(container)) {
        return;
    }

    const selection = readSelection(container);
    state.selectionBefore = selection;
    const spansLines = selection.anchor && selection.focus && selection.anchor.line !== selection.focus.line;
    const type = event.inputType || '';
    if (!spansLines || type === 'insertFromDrop' || type === 'deleteByDrag' || type.startsWith('history') || type === 'insertCompositionText') {
        return;
    }

    const text = type.startsWith('delete')
        ? ''
        : (event.data ?? (event.dataTransfer ? event.dataTransfer.getData('text/plain') : ''));
    const answer = ask(container, state, 'replace', { text });
    if (answer) {
        event.preventDefault();
        show(container, state, answer);
    }
}

function onInput(event, container, state) {
    repairStrayText(container);
    const before = state.selectionBefore || {};
    state.selectionBefore = null;
    const answer = ask(container, state, 'typed', {
        beforeAnchor: before.anchor || null,
        beforeFocus: before.focus || null,
        text: event.data ?? null,
        inputType: event.inputType || null,
        composing: !!event.isComposing
    });

    if (answer) {
        draw(container, answer.lines);
        select(container, answer.anchor, answer.focus);
    }
    notifyChanged(container, state.dotNetHelper);
}

function onClick(event, container, state) {
    const tick = event.target.closest ? event.target.closest('.note-line-tick') : null;
    if (!tick) {
        return;
    }

    // Three answers, one press at a time: nothing, done, given up on - the same cycle the browser's
    // own TickBox and the phone's CheckCircle follow, see Orbit.Core.Abstractions.TickState.
    event.preventDefault();
    if (!isWritable(container)) {
        return;
    }

    const line = tick.closest('.note-line');
    const answer = ask(container, state, 'tick', { line: Array.prototype.indexOf.call(container.children, line) });
    if (answer) {
        // Only the box changed, and the caret is wherever the reader left it.
        show(container, state, answer, /* placeSelection */ false);
    }
}

/// Copies what is selected as text somebody can paste anywhere: one line per line, and a tick-box line
/// as a "- " bullet. Left to the browser, a tick box is a button with no text, so a checklist pasted
/// into a message or another app arrived as a column of bare lines with nothing saying they were items.
///
/// Only a selection spanning lines is rewritten. Inside one line there is no box in the selection to
/// speak for - it is a run of words, and the browser already copies a run of words correctly.
function onCopy(event, container, state, isCut) {
    const selection = window.getSelection();
    if (!selection || selection.rangeCount === 0 || selection.isCollapsed || !event.clipboardData) {
        return;
    }

    const lines = Array.from(selection.getRangeAt(0).cloneContents().children)
        .filter((element) => element.classList.contains('note-line'));
    if (lines.length === 0) {
        return;
    }

    const text = lines
        .map((line) => (line.classList.contains('note-line-checklist') ? '- ' : '') + lineText(line))
        .join('\n');
    event.clipboardData.setData('text/plain', text);
    event.preventDefault();

    // A cut still has to take the lines away, and it is a change of shape like any other - the browser's
    // own delete would glue the lines' elements together.
    if (isCut && isWritable(container)) {
        const answer = ask(container, state, 'cut');
        if (answer) {
            show(container, state, answer);
        }
    }
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

/// Brings the surface to lines C# decided on, touching only the lines that differ - a line left alone
/// keeps whatever the browser was doing in it, and a box that only changed its answer is redrawn in
/// place rather than replaced.
function draw(container, lines) {
    lines = normalizeLines(lines);
    for (const node of Array.from(container.childNodes)) {
        if (node.nodeType !== Node.ELEMENT_NODE || !node.classList.contains('note-line')) {
            node.remove();
        }
    }

    const existing = Array.from(container.children);
    lines.forEach((line, index) => {
        const element = existing[index];
        if (!element) {
            container.appendChild(createLineElement(line));
            return;
        }

        const tick = element.querySelector('.note-line-tick');
        if (!!tick !== !!line.isChecklistItem || !element.querySelector('.note-line-text')) {
            element.replaceWith(createLineElement(line));
            return;
        }

        if (lineText(element) !== (line.text || '')) {
            setLineText(element, line.text || '');
        }
        if (tick && stateOf(tick) !== tickStateOf(line)) {
            setTick(element, tickStateOf(line));
        }
    });

    for (let index = lines.length; index < existing.length; index++) {
        existing[index].remove();
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

        // A button rather than <input type="checkbox">: a checkbox has two states and cannot carry the
        // cross a line somebody gave up on is drawn with. contenteditable="false" keeps it out of the
        // text run, exactly as the checkbox before it was kept out.
        const tick = document.createElement('button');
        tick.type = 'button';
        tick.contentEditable = 'false';
        tick.setAttribute('role', 'checkbox');
        tick.className = 'tick-box note-line-tick';
        div.appendChild(tick);
    }

    const text = document.createElement('span');
    text.className = 'note-line-text';
    div.appendChild(text);
    setLineText(div, line.text || '');
    if (line.isChecklistItem) {
        setTick(div, tickStateOf(line));
    }

    return div;
}

const TICK_NONE = 'none';
const TICK_DONE = 'done';
const TICK_FAILED = 'failed';

function stateOf(tick) {
    return tick.dataset.state || TICK_NONE;
}

function tickStateOf(line) {
    return line.isChecked ? TICK_DONE : line.isFailed ? TICK_FAILED : TICK_NONE;
}

/// Draws one of the three answers on a line's box. The mark is an SVG rather than a character for the
/// reason the phone's own circle gives: the faces this app is set in carry neither a tick nor a cross.
function setTick(line, state) {
    const tick = line.querySelector('.note-line-tick');
    if (!tick) {
        return;
    }

    tick.dataset.state = state;
    tick.className = `tick-box note-line-tick${state === TICK_DONE ? ' tick-box-done' : state === TICK_FAILED ? ' tick-box-failed' : ''}`;
    tick.setAttribute('aria-checked', state === TICK_DONE ? 'true' : state === TICK_FAILED ? 'mixed' : 'false');
    tick.innerHTML = state === TICK_DONE
        ? '<svg viewBox="0 0 20 20" width="13" height="13" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><path d="m4 10.5 4 4 8-9"/></svg>'
        : state === TICK_FAILED
            ? '<svg viewBox="0 0 20 20" width="13" height="13" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><path d="M5 5l10 10M15 5 5 15"/></svg>'
            : '';
    line.classList.toggle('note-line-done', state === TICK_DONE);
    line.classList.toggle('note-line-failed', state === TICK_FAILED);
}

function lineText(line) {
    const span = line.querySelector('.note-line-text');
    return span ? span.textContent : line.textContent;
}

/// An empty line's span holds a <br> and nothing else. An empty span has no line box, so the browser has
/// nowhere to stand a caret in it: the caret put there was drawn - and typed into - at the nearest place
/// that did have one, which is the start of the next line. That was the caret landing on the line after a
/// new box, the arrow keys stepping over empty lines and boxes, and a letter typed on a new line jumping
/// to the line below. The <br> is the placeholder every contenteditable editor uses for exactly this; it
/// has no text, so lineText does not see it.
function setLineText(line, text) {
    const span = line.querySelector('.note-line-text');
    if (!span) {
        line.textContent = text;
        return;
    }

    span.textContent = text;
    if (text.length === 0) {
        span.appendChild(document.createElement('br'));
    }
}

function extractLines(container) {
    return Array.from(container.children).map((line) => {
        const tick = line.querySelector('.note-line-tick');
        const state = tick ? stateOf(tick) : TICK_NONE;
        return {
            text: lineText(line) || '',
            isChecklistItem: !!tick,
            isChecked: state === TICK_DONE,
            isFailed: state === TICK_FAILED
        };
    });
}

/// Text the browser put outside a line's span - typed into a line whose span it could not find, or left
/// behind by one of its own deletes - is moved back into the span after every keystroke, before the rest
/// of this module reads line state, and the caret re-homed to the end of the merged text, which is where
/// it visually already appears to be.
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

    let repaired = false;
    for (const line of Array.from(container.children)) {
        repaired = repairLineDom(line) || repaired;
    }

    if (caretLine && caretWasStray && repaired) {
        const index = Array.prototype.indexOf.call(container.children, caretLine);
        select(container, { line: index, offset: lineText(caretLine).length });
    }
}

/// Answers whether anything had to be moved.
function repairLineDom(line) {
    const tick = line.querySelector('.note-line-tick');
    let span = line.querySelector('.note-line-text');
    if (!span) {
        span = document.createElement('span');
        span.className = 'note-line-text';
        line.appendChild(span);
    }

    const strayNodes = Array.from(line.childNodes).filter((node) => node !== tick && node !== span);
    if (strayNodes.length === 0) {
        tidyPlaceholder(span);
        return false;
    }

    // Chromium drops stray text immediately before the span, at the point the span sat when the caret
    // landed - so it belongs at the start of whatever the span already holds.
    let strayText = '';
    for (const node of strayNodes) {
        strayText += node.textContent;
        node.remove();
    }
    setLineText(line, strayText + lineText(line));
    return true;
}

/// A span with words in it needs no placeholder, and one left empty by the browser's own delete needs it
/// back - see setLineText.
function tidyPlaceholder(span) {
    const hasText = span.textContent.length > 0;
    const breaks = span.querySelectorAll('br');
    if (hasText) {
        breaks.forEach((element) => element.remove());
    } else if (breaks.length !== 1 || span.childNodes.length !== 1) {
        span.textContent = '';
        span.appendChild(document.createElement('br'));
    }
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

/// The lines and the selection, the way C# reads them: see SurfaceState.
function readSurface(container) {
    return { lines: extractLines(container), ...readSelection(container) };
}

function readSelection(container) {
    const selection = window.getSelection();
    if (!selection || selection.rangeCount === 0
        || !container.contains(selection.anchorNode) || !container.contains(selection.focusNode)) {
        return { anchor: null, focus: null };
    }

    return {
        anchor: pointOf(container, selection.anchorNode, selection.anchorOffset),
        focus: pointOf(container, selection.focusNode, selection.focusOffset)
    };
}

/// A place in the document as a line and a number of characters into its text.
function pointOf(container, node, offset) {
    const lines = container.children;
    if (lines.length === 0) {
        return null;
    }

    if (node === container) {
        if (offset >= lines.length) {
            const last = lines.length - 1;
            return { line: last, offset: lineText(lines[last]).length };
        }
        return { line: offset, offset: 0 };
    }

    const line = closestLine(node, container);
    if (!line) {
        return null;
    }

    const index = Array.prototype.indexOf.call(lines, line);
    const span = line.querySelector('.note-line-text');
    if (span && (node === span || span.contains(node))) {
        const range = document.createRange();
        range.setStart(span, 0);
        range.setEnd(node, offset);
        return { line: index, offset: range.toString().length };
    }

    // On the line itself, beside its span, or on its box: before the words or after them.
    if (node === line && span) {
        const spanIndex = Array.prototype.indexOf.call(line.childNodes, span);
        return { line: index, offset: offset <= spanIndex ? 0 : lineText(line).length };
    }
    return { line: index, offset: 0 };
}

/// The document position for a place C# named: inside the line's text, or - on an empty line - in its
/// span, before the placeholder.
function domPoint(container, point) {
    const lines = container.children;
    const line = lines[Math.max(0, Math.min(point.line, lines.length - 1))];
    const span = line.querySelector('.note-line-text') || line;
    let remaining = point.offset;
    let lastText = null;
    const walker = document.createTreeWalker(span, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
        lastText = node;
        if (remaining <= node.textContent.length) {
            return { node, offset: remaining };
        }
        remaining -= node.textContent.length;
    }

    return lastText
        ? { node: lastText, offset: lastText.textContent.length }
        : { node: span, offset: 0 };
}

function select(container, anchor, focus = anchor) {
    if (!anchor || container.children.length === 0) {
        return;
    }

    if (document.activeElement !== container) {
        container.focus({ preventScroll: true });
    }

    const start = domPoint(container, anchor);
    const end = domPoint(container, focus || anchor);
    window.getSelection().setBaseAndExtent(start.node, start.offset, end.node, end.offset);

    const focusLine = container.children[Math.min((focus || anchor).line, container.children.length - 1)];
    focusLine.scrollIntoView({ block: 'nearest' });
}
