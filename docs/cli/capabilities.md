# polypiler capabilities

```text
polypiler capabilities [--from <language>] [--to <language>] [--source-profile <id>]
                       [--target-profile <id>] [--all] [--format text|json] [--report <file>]
```

`capabilities` lists the source and target pairs the capability report states, with their state and
the gate record that is their evidence ([PRD §10](../prd/04-roadmap-and-delivery.md#s10), §23.6). The
report is `docs/capabilities.json`, which the executable embeds when it is built, or the file
`--report` names. The output is a pure function of the report and the filters: the command never
computes or upgrades a state, starts no toolchain and reads nothing else.

| Option | Meaning |
| --- | --- |
| `--from <language>`, `--to <language>` | Only pairs of this source or target language. |
| `--source-profile <id>`, `--target-profile <id>` | Only pairs of this profile. |
| `--all` | Every ordered pair of the report's languages, a language with itself included, `planned` where no pair has evidence. |
| `--format text\|json` | The output; `text` unless stated. |
| `--report <file>` | Read this report instead of the embedded one. |

## Which rows

- **Default listing.** The pairs with evidence that match every filter, sorted by source, target, source
  profile and target profile (ordinal).
- **A pair with no evidence.** When `--from` and `--to` are both given and no evidenced pair matches,
  one row says the pair is `planned`. Its profiles are shown as given, or `-`, and its evidence is `-`.
- **States.** The state is the report's: `planned`, or one of `parsed`, `lifted`, `recognized`,
  `proved`, `legalized`, `emitted`, `compiled`, `executed`, `verified` and `performance-validated`.

## Output

```text
$ polypiler capabilities --from csharp --to javascript
FROM    SOURCE-PROFILE  TO          TARGET-PROFILE  STATE     EVIDENCE
csharp  net10           javascript  es2022          verified  artifacts/gates/csharp-net10--javascript-es2022.json

1 pair(s) with evidence; every other pair is planned
```

- **Text** (the default) is a header, one line per row with each column padded to its widest cell plus
  two spaces, then an empty line and the summary.
- **JSON** is `{"reportCommit": "<sha>", "pairs": [{"from", "sourceProfile", "to", "targetProfile",
  "state", "evidence"}]}`, where `evidence` is `null` for a planned row.

## Exit codes

| Code | When |
| --- | --- |
| 0 | The report was read and the filters are valid; `planned` is an answer, not an error. |
| 2 | `PLY0003`: a language that is none of the report's, a profile the report does not list for that language, or a `--format` other than `text` and `json`. |
| 1 | `PLY0004`: `--report` cannot be read or is no capability report. |
| 1 | `PLY0061`: the report claims a state above `planned` without evidence. Nothing is printed. |
