# Video enhancement cancellation

Video enhancement needs cancellation tied to its editor as well as consent. Without an editor lifetime, a late result can update the disposed view model and expose an output file that has already been deleted.

## Requirements

Give the editor an operation lifetime linked to consent revocation. Disposing the editor must cancel consent, preparation, and generation waits associated with it. Check lifetime after each asynchronous boundary before publishing an enhanced path or changing editor state.

When a cancellation resistant generator returns a temporary output after disposal or revocation, discard that generated output and leave the original capture untouched. Disposal must remain synchronous and bounded. Native providers retain their own resources until actual completion.

## Acceptance criteria

- Disposing during consent, model preparation, or generation cancels the operation token.
- A late completion cannot change the disposed editor's path, load state, availability, or busy state.
- Late generated scratch output is deleted; the original media is preserved.
- Consent revocation has equivalent cancellation and publication behavior.
- Successful enhancement, cached toggling, save, and trim behavior continue to work.

## Scope

Use the editor's existing scratch artifact ownership. No global cancellation of other editors, new persisted state, or migration behavior is required.
