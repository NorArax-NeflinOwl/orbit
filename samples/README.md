# Sample archives

Archives written in the shape `/api/transfer` reads (`OrbitArchive`, version 1), so any of them can be
imported into an account as it is: **Options → Your data → Import** in the browser, or the account screen
on the phone. An import **adds** — nothing already in the account is matched or replaced, so importing
the same file twice leaves two copies of everything in it.

The files are plain JSON and meant to be edited before or after importing. Nothing here is wired into
the application; `SampleArchiveTests` only checks that each file still imports whole, so a change to the
archive format fails in the test suite rather than in somebody's account.

## first-time-father-checklist.orbit.json

Preparing for a first child, as one group list gathering eight checklists — the three trimesters, the
hospital bag, the birth itself, the first six weeks, the rest of the first year, and the things the
father prepares whenever he gets to them. 129 entries in all, each filed under a category (mother's
medical care, shopping, classes and knowledge, paperwork and benefits, home and car, support and
logistics, money and budget, baby care) so the checklist can be filtered down to one kind of work at a
time.

The medical and paperwork entries follow Polish practice — the antenatal test schedule, `becikowe`,
`800+`, paternity leave — so they need rereading anywhere else.

**No entry carries a due date**, because a due date can only be counted from the term date. Once that is
known, the week ranges in the list titles say which entries to date; entering them per list in the
checklist view is quicker than editing the file.
