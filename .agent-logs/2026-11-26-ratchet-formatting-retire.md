# Ratchet: formatting — retire (#322)

**Hypothesis:** `dotnet format whitespace . --folder --verify-no-changes` already passes on main
(the ratchet's remaining command prints nothing), so the `format` job can check the whole repo, and
the `## formatting` section of `dev/ratchets.md` can go with no other change. Nothing else reads the
section: `ratchets.py` parses whatever sections exist.

Scouted inline rather than with a sub-agent: the blast radius is one CI job and one doc section, and
a repo-wide grep for `format whitespace` and "formatting ratchet" found every touch point.

- Verified: the widened command exits 0 on this checkout (origin/main), about 12 s locally.
- Revised: dropped `fetch-depth: 2` and `shell: bash` from the job; they served only the diff of
  changed files, which is gone.
- Revised: kept `if: github.event_name == 'pull_request'` as it was. Running the check on pushes to
  main too would be a scope change the ticket did not ask for.
- Revised: two comments named the formatting ratchet as the reason the SDK is needed
  (`dev/ratchets.md` dry-run note, `ratchet.yml` setup-dotnet step); reworded them. Kept the
  setup-dotnet step in `ratchet.yml`: no remaining ratchet needs it, but a future one might, and
  removing it is outside the ticket.
- Verified: `python3 .github/scripts/ratchets.py list` runs (comments, capacity-reads);
  actionlint 1.7.12 is clean on `ci.yml` and `ratchet.yml`.
- Review: self-reviewed the diff against the ticket. No sub-agent review for a 40-line CI and doc
  deletion; no review fixes.
