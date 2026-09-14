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
// a selection that spans lines, a cut, a drag and drop, a tick - is decided in C# (NoteSurfaceEdits, through the
// component's Edit method): this module reports the lines and the selection, and draws what comes back,
// caret included. The call is synchronous (invokeMethod, which Blazor WebAssembly allows), because a key
// has to be let through or stopped before its handler returns.

const instances = new Map();

/// options: { takesTab, tickHint } - see ChecklistTextEditor.TakesTab, and the tooltip each box carries.
export function initialize(container, dotNetHelper, initialLinesJson, options) {
    const state = { dotNetHelper, options: options || {}, selectionBefore: null, pickedCount: 0, dragged: null };
    instances.set(container, state);
    render(container, normalizeLines(JSON.parse(initialLinesJson)));

    state.onBeforeInput = (event) => onBeforeInput(event, container, state);
    state.onInput = (event) => onInput(event, container, state);
    state.onKeyDown = (event) => onKeyDown(event, container, state);
    state.onMouseDown = (event) => onMouseDown(event, container);
    state.onClick = (event) => onClick(event, container, state);
    state.onCopy = (event) => onCopy(event, container, state, /* isCut */ false);
    state.onCut = (event) => onCopy(event, container, state, /* isCut */ true);
    state.onPaste = (event) => onPaste(event, container, state);
    state.onDragStart = () => onDragStart(container, state);
    state.onDragEnd = () => { state.dragged = null; };
    state.onDrop = (event) => onDrop(event, container, state);
    state.onSelectionChange = () => onSelectionChange(container, state);

    container.addEventListener('beforeinput', state.onBeforeInput);
    container.addEventListener('input', state.onInput);
    container.addEventListener('keydown', state.onKeyDown);
    container.addEventListener('mousedown', state.onMouseDown);
    container.addEventListener('click', state.onClick);
    container.addEventListener('copy', state.onCopy);
    container.addEventListener('cut', state.onCut);
    container.addEventListener('paste', state.onPaste);
    container.addEventListener('dragstart', state.onDragStart);
    container.addEventListener('dragend', state.onDragEnd);
    container.addEventListener('drop', state.onDrop);
    document.addEventListener('selectionchange', state.onSelectionChange);
}

export function dispose(container) {
    const state = instances.get(container);
    if (!state) {
        return;
    }
    container.removeEventListener('beforeinput', state.onBeforeInput);
    container.removeEventListener('input', state.onInput);
    container.removeEventListener('keydown', state.onKeyDown);
    container.removeEventListener('mousedown', state.onMouseDown);
    container.removeEventListener('click', state.onClick);
    container.removeEventListener('copy', state.onCopy);
    container.removeEventListener('cut', state.onCut);
    container.removeEventListener('paste', state.onPaste);
    container.removeEventListener('dragstart', state.onDragStart);
    container.removeEventListener('dragend', state.onDragEnd);
    container.removeEventListener('drop', state.onDrop);
    document.removeEventListener('selectionchange', state.onSelectionChange);
    instances.delete(container);
}

/// A drag that starts here remembers the selection it carries: a drop is only a move when the words
/// came from this surface, and by then the selection is not something to rely on.
function onDragStart(container, state) {
    const selection = readSelection(container);
    state.dragged = selection.anchor && selection.focus ? selection : null;
}

/// A drop is made here rather than by the browser, whose own drag glued two lines' elements together
/// when the words spanned lines - a box in the middle of a sentence, words outside any line. Where it
/// landed is read from the point under the pointer, since a drop does not move the selection there
/// first; what it does is NoteSurfaceEdits.Drag's to say for words dragged from this surface (a move,
/// or a copy with Ctrl - Alt on a Mac), and NoteSurfaceEdits.Drop's for text from anywhere else. Either
/// way it is one step for undo. A drop that lands nowhere readable does nothing rather than let the
/// browser guess.
function onDrop(event, container, state) {
    const dragged = state.dragged;
    state.dragged = null;
    if (!isWritable(container)) {
        return;
    }

    event.preventDefault();
    const to = dropPoint(container, event);
    if (!to) {
        return;
    }

    const answer = dragged
        ? ask(container, state, 'drag', { anchor: dragged.anchor, focus: dragged.focus, to, copies: event.ctrlKey || event.altKey })
        : ask(container, state, 'drop', { to, text: event.dataTransfer ? event.dataTransfer.getData('text/plain') : '' });
    if (answer) {
        show(container, state, answer);
    }
}

/// The line and offset under a drop. caretPositionFromPoint is the standard; Chromium before 128 and
/// Safari only have caretRangeFromPoint.
function dropPoint(container, event) {
    let node = null;
    let offset = 0;
    if (document.caretPositionFromPoint) {
        const position = document.caretPositionFromPoint(event.clientX, event.clientY);
        if (position) {
            node = position.offsetNode;
            offset = position.offset;
        }
    } else if (document.caretRangeFromPoint) {
        const range = document.caretRangeFromPoint(event.clientX, event.clientY);
        if (range) {
            node = range.startContainer;
            offset = range.startOffset;
        }
    }

    return node && container.contains(node) ? pointOf(container, node, offset) : null;
}

/// A paste goes in at the caret, in place of what is selected, as plain text - the browser put it at
/// the start of the line, and would have brought the copied page's markup in with it. Where the lines of
/// it go, and which of them come in as boxes, is NoteSurfaceEdits.Replace's to say. Clipboard content
/// with no text in it - an image on its own - pastes nothing.
function onPaste(event, container, state) {
    if (!isWritable(container) || !event.clipboardData) {
        return;
    }

    event.preventDefault();
    const text = event.clipboardData.getData('text/plain');
    if (!text) {
        return;
    }

    const answer = ask(container, state, 'paste', { text });
    if (answer) {
        show(container, state, answer);
    }
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

/// Makes the caret's line - or every line the selection touches - the style asked for, and takes it back
/// to Body when they are all already it. Written like insertChecklistItem above and for the same reason:
/// it is a press rather than a key, so it goes through the one door every edit goes through.
/// Puts a mark on the words the selection covers, or takes it off - the four buttons over the writing,
/// and the browser's own Ctrl+B and friends, which arrive as an inputType and are sent here too. C#
/// decides which way round it goes (NoteSurfaceEdits.Mark), for the reason every other edit is decided
/// there: the phone has to answer the same.
export async function mark(container, asked) {
    await Promise.resolve();
    const state = instances.get(container);
    if (!state || !isWritable(container)) {
        return;
    }

    const answer = ask(container, state, 'mark', { text: asked });
    if (answer) {
        draw(container, answer.lines);
        select(container, answer.anchor, answer.focus);
    }
}

export async function setStyle(container, style) {
    await Promise.resolve();
    const state = instances.get(container);
    if (!state || !isWritable(container)) {
        return;
    }

    const answer = ask(container, state, 'style', { text: style });
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
    const command = commandFor(event, state);
    if (!command) {
        return;
    }

    const answer = ask(container, state, command);
    // Undo and redo are this surface's own even with nothing left to undo: the browser's would reach
    // into a history of a document the page has since rebuilt, and undo something nobody can see.
    if (answer || command === 'undo' || command === 'redo') {
        event.preventDefault();
    }
    if (answer) {
        show(container, state, answer);
    }
}

function commandFor(event, state) {
    // Ctrl+Z undoes; Ctrl+Y and Ctrl+Shift+Z redo (Cmd on a Mac). The letter is read from the key where
    // the layout has Latin letters, and from the physical key where it does not, so the shortcut still
    // works while typing in another alphabet.
    if ((event.ctrlKey || event.metaKey) && !event.altKey) {
        const key = event.key || '';
        const letter = /^[a-z]$/i.test(key) ? key.toLowerCase() : event.code === 'KeyZ' ? 'z' : event.code === 'KeyY' ? 'y' : '';
        if (letter === 'z') {
            return event.shiftKey ? 'redo' : 'undo';
        }
        if (letter === 'y') {
            return 'redo';
        }
    }

    switch (event.key) {
        case 'Enter':
            return 'enter';
        case 'Backspace':
            return 'backspace';
        case 'Delete':
            return 'delete';
        case 'Tab':
            // Only on a surface that takes Tab, and never with a modifier that means something to the
            // browser or the system (Ctrl+Tab changes tabs). Answered either way once asked, so the focus
            // stays in the writing even when Shift+Tab finds no indentation to take.
            if (!state.options.takesTab || event.ctrlKey || event.altKey || event.metaKey) {
                return null;
            }
            return event.shiftKey ? 'outdent' : 'indent';
        default:
            return null;
    }
}

/// Typing over a selection that spans lines. Left to the browser, it glues the lines' elements together
/// - a box ends up in the middle of a sentence, or the words land outside any line - so the words go
/// through the same C# every other change of shape does. A drag and drop is onDrop's.
function onBeforeInput(event, container, state) {
    if (!isWritable(container)) {
        return;
    }

    // Undo and redo that did not come from the keys - the browser's Edit menu, its context menu, a
    // gesture - reach the same history Ctrl+Z does, for the same reason (see onKeyDown).
    if (event.inputType === 'historyUndo' || event.inputType === 'historyRedo') {
        event.preventDefault();
        const answer = ask(container, state, event.inputType === 'historyUndo' ? 'undo' : 'redo');
        if (answer) {
            show(container, state, answer);
        }
        return;
    }

    // The browser's two halves of a drag never run here. A drop on this surface is stopped before them
    // (see onDrop), so a deleteByDrag is words dragged from here to somewhere else - taken away the way
    // a cut takes them, so the lines they leave stay lines.
    const type = event.inputType || '';
    if (type === 'insertFromDrop' || type === 'deleteByDrag') {
        event.preventDefault();
        const dragged = state.dragged;
        if (type === 'deleteByDrag' && dragged) {
            const answer = ask(container, state, 'cut', { anchor: dragged.anchor, focus: dragged.focus });
            if (answer) {
                show(container, state, answer);
            }
        }
        return;
    }

    // Ctrl+B and its friends - and the same four from the browser's own menus. Stopped and asked of C#,
    // which owns what a mark means here: left to the browser, these would put tags of their own choosing
    // into the line and the phone would never hear about them.
    const marking = MARK_INPUTS[event.inputType];
    if (marking) {
        event.preventDefault();
        const answer = ask(container, state, 'mark', { text: marking });
        if (answer) {
            show(container, state, answer);
        }
        return;
    }

    const selection = readSelection(container);
    state.selectionBefore = selection;
    const spansLines = selection.anchor && selection.focus && selection.anchor.line !== selection.focus.line;
    if (!spansLines || type.startsWith('history') || type === 'insertCompositionText') {
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

function tickOf(event) {
    return event.target.closest ? event.target.closest('.note-line-tick') : null;
}

/// A box is pressed without the press moving the caret: left to the browser, the mousedown would put
/// the caret by the box and throw away a selection of lines made for the box to answer for (see
/// onClick). Shift+click on a box is how that selection is stretched to the box's line.
function onMouseDown(event, container) {
    const tick = tickOf(event);
    if (!tick || !isWritable(container)) {
        return;
    }

    event.preventDefault();
    if (event.shiftKey) {
        extendSelectionTo(container, tick.closest('.note-line'));
    }
}

function onClick(event, container, state) {
    const tick = tickOf(event);
    if (!tick) {
        return;
    }

    // Three answers, one press at a time: nothing, done, given up on - the same cycle the browser's
    // own TickBox and the phone's CheckCircle follow, see Orbit.Core.Abstractions.TickState. Inside a
    // selection of several boxes, the answer goes to all of them - NoteSurfaceEdits.Cycle decides.
    // A Shift+click only selects: see onMouseDown.
    event.preventDefault();
    if (!isWritable(container) || event.shiftKey) {
        return;
    }

    const line = tick.closest('.note-line');
    const answer = ask(container, state, 'tick', { line: Array.prototype.indexOf.call(container.children, line) });
    if (answer) {
        // Only boxes changed, and the caret - or the selection - is wherever the reader left it.
        show(container, state, answer, /* placeSelection */ false);
    }
}

/// Shift+click on a box: the selection stretches from where it was started to that box's line - to the
/// line's end when it lies below the start, to its head when above - so the line is inside it. With no
/// selection on the surface yet, the selection is the box's line.
function extendSelectionTo(container, line) {
    const index = Array.prototype.indexOf.call(container.children, line);
    const current = readSelection(container);
    const anchor = current.anchor || { line: index, offset: 0 };
    const below = index > anchor.line || (index === anchor.line && anchor.offset === 0);
    const focus = below ? { line: index, offset: lineText(line).length } : { line: index, offset: 0 };
    select(container, anchor, focus);
}

/// Rings the boxes a press would answer for together, and tells the page how many there are when that
/// changes. Which boxes they are is C#'s to say (NoteSurfaceEdits.SelectedChecklistLines), the same
/// rule the press itself follows, so the rings never promise a different set than a press changes.
function onSelectionChange(container, state) {
    const selection = window.getSelection();
    const inside = isWritable(container) && selection && selection.rangeCount > 0 && !selection.isCollapsed
        && container.contains(selection.anchorNode) && container.contains(selection.focusNode);
    const picked = inside
        ? state.dotNetHelper.invokeMethod('SelectedChecklistLines', JSON.stringify({ command: 'select', ...readSurface(container) }))
        : [];

    Array.from(container.children).forEach((line, index) => line.classList.toggle('note-line-picked', picked.includes(index)));
    if (picked.length !== state.pickedCount) {
        state.pickedCount = picked.length;
        state.dotNetHelper.invokeMethodAsync('OnSelectedTicksChanged', picked.length);
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
        container.appendChild(createLineElement(line, tickHintOf(container)));
    }

    numberTheLists(container);
}

/// Writes each numbered line's number onto it, counting from one down each unbroken run - the same rule
/// NoteLineStyles.NumberOf follows, and it is here as well as there because the number is *drawn* (a CSS
/// ::before reads it) rather than being part of the words. Any line that is not numbered breaks the run,
/// which is what makes two lists separated by a paragraph two lists.
///
/// Done after the lines are in the container rather than while each is built, because a line's number is
/// a fact about what is above it and nothing knows that until they are all there.
function numberTheLists(container) {
    let number = 0;
    for (const line of Array.from(container.children)) {
        if (line.dataset && line.dataset.style === 'numbered') {
            number++;
            line.dataset.number = String(number);
        } else {
            number = 0;
            if (line.dataset) {
                delete line.dataset.number;
            }
        }
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

    const hint = tickHintOf(container);
    const existing = Array.from(container.children);
    lines.forEach((line, index) => {
        const element = existing[index];
        if (!element) {
            container.appendChild(createLineElement(line, hint));
            return;
        }

        const tick = element.querySelector('.note-line-tick');
        if (!!tick !== !!line.isChecklistItem || !element.querySelector('.note-line-text')
            || element.dataset.style !== styleOf(line)) {
            element.replaceWith(createLineElement(line, hint));
            return;
        }

        const wanted = marksOf(line);
        if (lineText(element) !== (line.text || '') || !sameMarks(marksIn(element), wanted)) {
            setLineWords(element, line.text || '', wanted);
        }
        if (tick && stateOf(tick) !== tickStateOf(line)) {
            setTick(element, tickStateOf(line));
        }
    });

    for (let index = lines.length; index < existing.length; index++) {
        existing[index].remove();
    }

    numberTheLists(container);
}

function tickHintOf(container) {
    const state = instances.get(container);
    return state && state.options.tickHint ? state.options.tickHint : null;
}

function normalizeLines(lines) {
    return lines && lines.length > 0
        ? lines
        : [{ text: '', isChecklistItem: false, isChecked: false, style: 'body', marks: [] }];
}

/// The style a line is drawn in, as the word C# sends - see Orbit.Core.Notes.NoteLineStyle. Anything
/// missing or unknown is Body, which is the same answer the two C# readers give and the reason a note
/// written on a newer build still opens here.
function styleOf(line) {
    const style = (line && line.style ? String(line.style) : 'Body').toLowerCase();
    return STYLES.includes(style) ? style : 'body';
}

const STYLES = ['body', 'title', 'heading', 'subheading', 'monospaced', 'bulleted', 'dashed', 'numbered'];

/// The marks a stretch of words inside a line can carry - see Orbit.Core.Notes.NoteTextMark. Lower case
/// here and sent as such: C# reads a mark's name however it is written.
const MARKS = ['bold', 'italic', 'underlined', 'struckthrough'];

/// The inputTypes a browser reports for its own four formatting commands - Ctrl+B, the Edit menu, a
/// context menu. Each is answered as the matching mark rather than let through.
const MARK_INPUTS = {
    formatBold: 'bold',
    formatItalic: 'italic',
    formatUnderline: 'underlined',
    formatStrikeThrough: 'struckthrough'
};

/// What each is drawn as. Real elements rather than classed spans, so a copy out of the note arrives
/// elsewhere still bold - and so the browser's own Ctrl+B, if one ever slips past onBeforeInput, makes
/// something this can read back rather than something it has to guess at.
const MARK_TAGS = { bold: 'strong', italic: 'em', underlined: 'u', struckthrough: 's' };

/// And back again, with the tags a browser uses for the same four when it formats text itself.
const TAG_MARKS = {
    STRONG: 'bold', B: 'bold',
    EM: 'italic', I: 'italic',
    U: 'underlined',
    S: 'struckthrough', STRIKE: 'struckthrough', DEL: 'struckthrough'
};

/// A line's marks as C# sends them, with anything this build does not know dropped - the rule every
/// name on this wire follows.
function marksOf(line) {
    const marks = line && Array.isArray(line.marks) ? line.marks : [];
    return marks
        .map((run) => ({
            start: run.start | 0,
            length: run.length | 0,
            mark: String(run.mark || '').toLowerCase()
        }))
        .filter((run) => run.length > 0 && MARKS.includes(run.mark));
}

/// The words of a line, drawn with their marks. The span's contents are built from scratch: a stretch of
/// words carrying the same marks is one text node inside however many elements it needs, and the caret is
/// put back afterwards by whoever asked for the redraw (see show).
function setLineWords(line, text, marks) {
    const span = line.querySelector('.note-line-text');
    if (!span) {
        line.textContent = text;
        return;
    }

    span.textContent = '';
    if (text.length === 0) {
        // See setLineText: an empty span has no line box, so the caret has nowhere to stand in it.
        span.appendChild(document.createElement('br'));
        return;
    }

    for (const piece of markedPieces(text, marks)) {
        span.appendChild(wrapped(piece.text, piece.marks));
    }
}

/// The text cut into the longest stretches that carry the same marks - which is what a redraw needs and
/// what a run of marks does not say directly, since two marks over the same words are two runs.
function markedPieces(text, marks) {
    const carried = [];
    for (let at = 0; at < text.length; at++) {
        carried.push([]);
    }

    for (const run of marks) {
        const from = Math.max(0, run.start);
        const to = Math.min(text.length, run.start + run.length);
        for (let at = from; at < to; at++) {
            if (!carried[at].includes(run.mark)) {
                carried[at].push(run.mark);
            }
        }
    }

    // Named in one order always, so the same set of marks reads as the same stretch.
    const nameOf = (marksHere) => MARKS.filter((mark) => marksHere.includes(mark)).join(' ');
    const pieces = [];
    for (let at = 0; at < text.length; at++) {
        const name = nameOf(carried[at]);
        const last = pieces.length > 0 ? pieces[pieces.length - 1] : null;
        if (last && last.name === name) {
            last.text += text[at];
        } else {
            pieces.push({ name, text: text[at], marks: name ? name.split(' ') : [] });
        }
    }

    return pieces;
}

function wrapped(text, marks) {
    let node = document.createTextNode(text);
    for (const mark of marks) {
        const element = document.createElement(MARK_TAGS[mark]);
        element.appendChild(node);
        node = element;
    }

    return node;
}

/// The marks on a line as the document now holds them - what the browser drew, plus anything it drew
/// itself while somebody was typing. Read by walking the words: every text node carries whatever marks
/// its ancestors up to the span name.
function marksIn(line) {
    const span = line.querySelector('.note-line-text');
    if (!span) {
        return [];
    }

    const runs = [];
    let offset = 0;
    const walker = document.createTreeWalker(span, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
        const length = node.textContent.length;
        for (const mark of marksOn(node, span)) {
            runs.push({ start: offset, length, mark });
        }
        offset += length;
    }

    return joinedRuns(runs);
}

function marksOn(node, span) {
    const found = [];
    for (let element = node.parentElement; element; element = element.parentElement) {
        const mark = TAG_MARKS[element.tagName];
        if (mark && !found.includes(mark)) {
            found.push(mark);
        }
        if (element === span) {
            break;
        }
    }

    return found;
}

/// Touching stretches of one mark made one - the shape C# keeps them in (NoteTextMarks.Normalized), so
/// what goes back is what would come out again rather than one run per text node.
function joinedRuns(runs) {
    const sorted = runs.slice().sort((one, other) =>
        one.mark === other.mark ? one.start - other.start : (one.mark < other.mark ? -1 : 1));

    const joined = [];
    for (const run of sorted) {
        const last = joined.length > 0 ? joined[joined.length - 1] : null;
        if (last && last.mark === run.mark && last.start + last.length >= run.start) {
            last.length = Math.max(last.start + last.length, run.start + run.length) - last.start;
            continue;
        }

        joined.push({ start: run.start, length: run.length, mark: run.mark });
    }

    return joined;
}

/// Whether two sets of marks say the same thing, for draw() - which redraws a line whose marks changed
/// the way it redraws one whose words did.
function sameMarks(one, other) {
    return one.length === other.length
        && one.every((run, at) =>
            run.start === other[at].start && run.length === other[at].length && run.mark === other[at].mark);
}

function createLineElement(line, tickHint) {
    const div = document.createElement('div');
    div.className = 'note-line';

    // The style as a data attribute rather than a class, so the CSS reads one thing and draw() can tell
    // whether a line's style changed without picking the class list apart.
    div.dataset.style = styleOf(line);

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
        if (tickHint) {
            tick.title = tickHint;
        }
        div.appendChild(tick);
    }

    const text = document.createElement('span');
    text.className = 'note-line-text';
    div.appendChild(text);
    setLineWords(div, line.text || '', marksOf(line));
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
            isFailed: state === TICK_FAILED,
            style: line.dataset && line.dataset.style ? line.dataset.style : 'body',
            marks: marksIn(line)
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
    // The marks move along by however much was put back in front of the words they were on - the same
    // arithmetic NoteTextMarks.Kept does for an insertion at the head of a line.
    const moved = marksIn(line).map((run) => ({ ...run, start: run.start + strayText.length }));
    setLineWords(line, strayText + lineText(line), moved);
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
