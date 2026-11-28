# Fixtures of `polypiler capabilities`

Capability reports in the schema of `spec/schemas/capabilities.schema.json` (#176), written for the
command's tests; the expected rows are read off them by hand. Each lists the sixteen ordered pairs of
`csharp` (`net10`), `javascript` (`es2022`), `kotlin` (`jvm`) and `perl` (`perl5`).

- `two-pairs.capabilities.json`: `csharp` -> `javascript` at `verified` and `javascript` -> `csharp`
  at `emitted`, each with a gate record reference; every other pair `planned`.
- `empty.capabilities.json`: every pair `planned`.
- `unbacked.capabilities.json`: `kotlin` -> `perl` at `verified` without evidence, which the schema
  refuses and the command reports as `PLY0061`.
