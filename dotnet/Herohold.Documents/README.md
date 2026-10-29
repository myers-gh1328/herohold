# Native PDF codec and isolated worker

`IPdfService` exposes asynchronous inspection and export. `PdfWorkerClient` starts
one disposable .NET worker per operation. Application code must use this seam for
uploaded PDFs; `PdfCodec` is synchronous and belongs inside the worker.

The dependency is pinned to MIT-licensed `Aspose.PDF.FOSS` 26.10.0. The existing
trusted editable character-sheet template and OFL-licensed Noto Sans TrueType font
are embedded resources. Runtime generation does not need Python, installed fonts,
network access, or repository-relative asset paths. Dependency/font notices ship
in the worker's `licenses` folder.

Inspection preserves exact form names, including trailing spaces, and returns raw
fields, template classification/signature, typed evidence, safe passthrough values,
and validated embedded canonical state. Character reconstruction and rule review
remain the caller's responsibility. Visible field changes do not replace embedded
state silently. Duplicate builder attachments are rejected.

Export maps final Core stats and remaining counters to editable fields, retains
safe passthrough values, and embeds `dnd-character-builder-state.json`. Its legacy
payload version is 1 and supported canonical-character schemas are 1 through 5;
these versions are separate from the native workspace schema. Canonical JSON
checksums match existing Unicode/number serialization.

The original three-page sheet remains unchanged for spell lists that fit its
fixed rows. If any spell level exceeds those rows, export appends a complete
printable list of prepared, known, spellbook and granted spell names grouped by
level, with embedded Noto Sans and a visible continuation marker. These text-only
Letter pages add no form fields: the 334-field signature and embedded canonical
state remain intact. The appendix is limited to 32 pages and rejects excessive
lists explicitly. A detached form still exposes only its original editable spell
rows; lossless recovery of the complete book uses the embedded canonical state.
Export explicitly disables the source template's linearization so stale original
byte-offset hints cannot survive field, attachment or page changes. Independent
Poppler inspection, text extraction and rendering of the synthetic 14-spell
Unicode appendix complete without PDF syntax warnings.

Limits match the existing PDF boundary: 20 MB PDF, 2 MB embedded payload, 1,000
fields, 256-character names, 100 KB per value, 2 MB aggregate form text, JSON depth
24 and 50,000 values. The client bounds IPC output, allows one concurrent worker,
uses a 45-second configurable total deadline, kills timed-out workers, and starts
them with a minimal environment and 256 MB managed-heap limit. This is process
fault isolation, not an OS security sandbox or a total process-memory limit.

`BundlePdfWorker.targets` copies the matching configuration's complete worker
folder during application/test build and application publish. The default path is
`AppContext.BaseDirectory/pdf-worker/Herohold.PdfWorker.dll`.

Run the self-contained synthetic behavior tests with:

```sh
dotnet test dotnet/Herohold.Documents.Tests/Herohold.Documents.Tests.csproj
```

The tests build and copy their own worker, require no saved characters, and cover
editable round-trip, native numeric projection, raw-form evidence, checksum and
schema validation, duplicate attachments, encrypted input, changed visible fields,
limits, cancellation and actual process timeout behavior.

The Web layer's `NativePdfImportReviewService` consumes inspection through the
worker seam. It returns the same detached `character`, `sheet`, `validation`,
`choices`, `playMetadata`, and `importReview` envelope used by other character
workflows. Raw form choices target `import_selections`; embedded state uses the
ordinary builder choices. A valid source alone never authorizes adoption.
`sourceCanAdopt` retains the inspected source decision separately from current
build validation, and `RevalidateAfterEdit` preserves source blockers while
rechecking an explicitly repaired build.

Raw forms require confirmation of final scores, spells, feats, masteries and
equipment. Baselines are solved with the native ability rules, including caps and
explicit recovered feat increases. Unseen ASI or HP-roll history is not invented.
An import checkpoint permits subsequent level-ups; preserving observed maximum
HP creates an explained import adjustment. Visible comparisons remain review
warnings, while missing source choices and unmatched spells block adoption.
Custom equipment and allowlisted presentation fields survive reconciliation.

Run native reconciliation tests, including actual compiled worker round trips:

```sh
dotnet test dotnet/Herohold.Integration.Tests/Herohold.Integration.Tests.csproj --filter FullyQualifiedName~NativePdfImportReviewTests
```
