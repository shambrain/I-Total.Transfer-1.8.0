# Continuing an unfinished transfer

A camera roll of 55,185 files is 151.34 GiB and takes minutes to hours to move. The most likely way for
that job to end is **not** a clean finish: the cable is nudged, the phone sleeps, the laptop updates, or
somebody presses Stop to go to bed. This document describes what such a job leaves behind on disk, how the
app reads it, and exactly what it will and will not claim when it is continued.

It is deliberately precise about the boundary: a "resume" here means *finish copying what is outstanding*,
not *assume what was copied is good*.

## 1. The problem this round fixed

Before this round the folder was, in practice, forgetful:

- `SessionManifest.LastSessionStatus` existed in the model and was written **only on the completed path**
  (`TransferService`), and was **read nowhere**. A cancelled or aborted run wrote its outcome only into
  `.i-total\session.db` (SQLite) — a file that no part of the UI reads.
- Therefore a destination holding 1,203 copied-and-verified files and 25,132 files that had never been
  touched looked exactly like an empty folder to the operator, and the only route back to that work was a
  fresh scan of the whole phone (measured at ~27 ms per object, about twelve minutes for the large album).
- Nothing in the interface offered to continue a session. The only string resembling "resume" was the
  pause button's own toggle.

The work is not lost in that state — the files are on disk and the manifest records every one of them —
but the app could not say so.

## 2. What a session leaves in the destination folder

```
<destination>\
  .i-total\
    session.json                  the durable session record (the recovery plan)
    session.db                    SQLite session/queue history (never used for recovery)
  I-total-session-report.json     written when a run ends with a summary
  DCIM\202608_a\IMG_0001.JPG      the copied files, at their source-relative paths
  I-total-transfer.log            the run log (kept beside the backup, rotated)
```

`session.json` is written atomically (temporary file, then `File.Replace`, retried briefly because a scanner
or the search indexer can hold the destination for a moment) and flushed at every consequential boundary, so
it survives a power loss with at most the last few seconds of progress missing.
Each entry carries its path, expected size, state, attempt count, source timestamp, the verification level
reached, and the digests that were actually computed.

### The status field now describes the run

| Written when | `LastSessionStatus` |
| --- | --- |
| The plan is merged and flushed, **before the first byte moves** | `RUNNING` |
| The run reaches its summary | `HASH-VERIFIED COMPLETE (source-to-destination)`, `DESTINATION-HASH-VERIFIED COMPLETE`, `SIZE-VERIFIED COMPLETE`, or an `INCOMPLETE …` / `COPIED WITH UNVERIFIABLE FILES …` verdict |
| The operator presses Stop | `CANCELLED` |
| A fault ends the run (a backend throw, a lost device) | `ABORTED` |
| The process is killed with no chance to write anything | *(nothing — the `RUNNING` stamp from the start of the run stands)* |

The `RUNNING` stamp is what makes the last row possible. A run that is killed cannot report its own
interruption, so the folder has to be able to say "this was in flight" without being told.

## 3. How the record is read

`Core/SessionRecovery.cs` is a pure reader: no device, no scan, no cache, no memory of a previous screen,
and it **never throws**. It answers "is there unfinished work in this folder?" before an iPhone is even
attached, which is exactly when an operator wants to know.

It refuses a record it cannot trust, and says why:

| Condition | Result |
| --- | --- |
| No `session.json` | No session. Not a problem; an ordinary folder. |
| Unparseable, empty, or no entries | `Problem` set, no offer. The file is **left in place** — it is evidence. |
| `SchemaVersion` other than 1 or 2 | `Problem` set. A newer record is refused rather than misread. |
| `Destination` not this folder | `Problem` set. A record belonging elsewhere is never applied to this folder. |

Entries are then counted by state. Everything that is not verified and not deliberately skipped is
**outstanding**: `Queued`/`Discovered`/`Copying`/`Paused` (never attempted or interrupted mid-flight),
`Failed`, `Inaccessible`, and `CopiedUnverified`.

A session is offered for continuation only when it is unfinished **and** something is outstanding. A
session whose status says complete is never offered, and a session with nothing left to do is not offered
either — there is no work to continue.

## 4. What continuing does, and what it does not

The plan is rebuilt from the record, not from a fresh enumeration:

- **Only the outstanding entries are planned.** Files already recorded as verified or skipped are left out
  of the plan, because re-proving them means re-reading every destination file — 106 GB of hashing on a
  full camera roll. (Seeded at device scale, a 26,335-entry record of which 25,132 are outstanding rebuilds
  as a 25,132-file, 172 GB plan.)
- **Nothing is trusted on the strength of the record.** Every file the plan carries is fetched from the
  device again and hashed again under the same rules as a first run. The record decides *what to fetch*,
  never *what is true*.
- **Verified files are not re-checked, copied, or touched.** The manifest is shared, so the session still
  covers them and the finished report still counts them. An operator who wants those files proven again has
  an explicit action for it: **Verify** on the Media library page (which re-hashes source and destination
  and can repair).
- **Source object identifiers are not used.** A WPD object id does not survive a reconnect (and there may
  be no connect at all), so the plan carries paths only and the backend resolves each file by path. Paths
  the engine would refuse (absolute, `..`, device names) are dropped while the plan is built and counted,
  never passed on to look like work.
- **Finding a file by path does not cost the album again.** Because the ids are gone, each file is located
  by walking its relative folder — and the first implementation did that *per file*, re-enumerating the album
  and reading a property per sibling until the name matched. On a 10,000-photo album that is tens of millions
  of device round-trips before the first byte moves, so a continuation would look like a hang rather than a
  transfer. Each device folder is now read **once per session** and its children kept by name
  (`Core/FolderNameIndex.cs`), and the index is dropped whenever the session is (a reconnect can renumber
  every id). The property is pinned without a handset — 26,100 files across three albums cost four folder
  reads — but the saving on a real album is **not yet measured**; see §6.
- **The device is re-probed first.** Without a ready handset the action is refused before anything else
  happens.
- Paths recorded as `Skipped` stay skipped, and a file recorded as `Failed` is retried under the normal
  conflict policy: a destination file that is already correct is still never overwritten blindly, and a
  destination file that disagrees is still preserved and reported rather than replaced.

### The completeness rule

A continued run plans only part of the session, so the run's own counters cannot prove the session is
finished. They are no longer asked to:

> A session may be declared complete only when **the whole manifest** holds nothing that is failed,
> unreadable, unverified, or in a non-terminal state — not merely nothing this run failed on.

This closes a false-complete path that continuation would otherwise have opened: copying the last
outstanding file could have produced "HASH-VERIFIED COMPLETE" while an older entry in the same folder was
still failed. The run's own counters, and the progress the operator watched, continue to describe the work
**this run** did.

### Nothing unverified is left in the folder

A copy is written to a staging file and moved into place, so a destination file is never half-written. But
a cancellation (or a read error) can land **between the move and the hash comparison**, leaving a complete
file on disk that no record vouches for. That file is now **discarded**, and its entry stays failed with an
error saying so.

The alternative looks harmless and is not: the next attempt would find the file present, see a matching
size, and classify it as a legacy baseline at **size-only** strength — quietly downgrading a claim the
operator never agreed to, and making the file permanently weaker than its neighbours. Discarding it keeps a
single, checkable invariant instead: **a file in the destination is either verified or absent.** The cost is
re-fetching that one file; the benefit is that a continued session can still reach full source-to-destination
hash verification, which is what the test suite now pins.

## 5. The interface contract

An **inline Fluent bar** on the Overview, above the current-operation band. It is a second action on the
existing `InfoBar` control rather than a message box, because a modal question in the middle of a backup is
worse than a strip that stays where it belongs.

- It appears only when the folder it describes really holds unfinished work, or holds a record that cannot
  be read (which the operator needs to know, because that record *is* the recovery plan).
- It quotes the folder's own numbers and nothing else:
  `Apple iPhone · 26,335 planned · 1,203 copied and verified · 25,132 outstanding (12 failed, 4 unreadable)  Only outstanding files are copied.`
- With no handset attached the action stays **visible but disabled**, and the message says why — instead of
  offering a button that cannot work.
- It costs no vertical space when there is nothing to continue: its row collapses to zero.
- The row is measured for the display it is on, not once and for all: a run-time dimension is a *device*
  pixel count while the designer's are scaled for the monitor, so on a 150% display the row is 141 px, not
  94, and the two-line message stays inside its card. The first capture at 150% showed the second line
  painted past the card; it is fixed and re-measured (the row's following content moved down by exactly
  47 device px, and the message's bottom now sits inside the bar).
- **Not now** sets the session aside. The dismissal is remembered by a fingerprint of that session alone
  (destination, last update, planned count, outstanding count) — never a device identity. Any further
  progress, any new entry, or a different destination produces a different fingerprint, so a dismissal can
  never hide a changed session or a new problem. An unreadable record is only hidden for the run, because a
  folder whose recovery record cannot be read should not be quietly forgotten.
- When no phone is attached, the offer is still made: the folder can answer for itself, and the operator can
  see what is waiting before connecting anything.

## 6. Verification status

**Pinned by tests** (`SessionRecoveryTests` 14, `RecoveryFlowTests` 6, `DashboardRecoveryTests` 9):

- every entry state is counted, and outstanding bytes cover exactly the non-terminal work;
- finished / cancelled / aborted / running / never-recorded statuses are read the way they are meant, and
  `INCOMPLETE — NOT A VERIFIED COMPLETE BACKUP` is not mistaken for success because it contains the word
  COMPLETE;
- a record for another folder, a newer schema, a damaged file and an unreadable manifest are each refused
  with a reason, and a damaged record is left in place;
- the plan carries only outstanding entries, with their own sizes, extensions and source timestamps, and
  with no object identifiers; paths the engine would refuse are dropped and counted;
- through the real engine: the session is stamped `RUNNING` **before the first byte is fetched**; a Stop
  writes `CANCELLED` with what finished, what failed and what was never reached; a backend fault writes
  `ABORTED` and stays continuable; continuing fetches **exactly** the outstanding files (asserted on the
  backend's fetch log), leaves the verified file alone, reaches full source-to-destination verification, and
  refuses to call the session complete while an entry it could never copy is still outstanding;
- a copy whose verification was interrupted is not left in the destination;
- the bar's numbers, its collapsed row, the disabled-without-device action, the separate action/dismissal
  events, its two-line message with an ellipsis rather than a silent cut, and the invariant that showing the
  bar never squeezes the action panel.

**Checked live on the published build** (2026-10-19, no handset needed for the record itself): a **seeded**
26,335-entry record (11,143,635 B) was read by the app's own reader and shown on the Overview with the exact
counts; the app logged the unfinished session before it logged "Welcome"; **Not now** collapsed the bar,
wrote the status line *"Unfinished session set aside. 25,132 file(s), 172.12 GB, are still outstanding in
the destination."*, persisted the fingerprint into `settings.json`, and left the copied files untouched.
With the handset attached, the same bar reported **Resume transfer enabled**. The unreadable-record path was
checked the same way, by replacing the record with 32 bytes of nonsense: the Overview showed *"This folder's
session record could not be read"* / *"The session record could not be read (JsonException); it was left
untouched."* with **no action offered** (only a dismissal), and the damaged file was still on disk
afterwards, 32 bytes, exactly as written.

**Not yet proven on hardware:** a transfer that is *really* interrupted on a live device and then
continued. Doing that writes real data to a real disk for minutes, so it has not been run without the
operator's go-ahead. The engine path, the record and the interface are covered by the tests above; what
remains unproven is the device-facing leg (a genuine cancel mid-copy over WPD followed by a genuine
continuation), and it is listed as such in `PROJECT-STATUS.md` and `docs/TEST-PLAN.md`.

**The path lookup a continuation depends on** is pinned separately by `FolderNameIndexTests` (9): each
device folder is read once per session whatever the number of files in it (26,100 files across three albums
cost four reads), nested folders are followed, the `DCIM` segment names the root rather than being searched
for, a missing file and a missing folder are told apart, matching ignores case, and a nameless child is never
the match. The tests prove the *logic*; they cannot prove the saving on a real album, because that is the
device leg the drill above is for. `ScanStabilityTests` (6) pins the second-pass comparison the option on the
Overview triggers, and `LogRedactionTests` (9) keeps a device serial out of the log that ships beside the
backup.
