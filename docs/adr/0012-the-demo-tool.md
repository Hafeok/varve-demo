# 0012 — The demo tool

## Status

**Proposed** — filed unaccepted by session 03 on #5, 2026-11-03. Its decision
set is [`the-demo-tool`](../decisions/the-demo-tool.md).

## Context

The brief's step 1 is "`docker compose up` or a `dotnet tool`". The steps
need one program a viewer runs on their own machine: it asks S2's question of
a lock file, and later steps add commands (the time travel of S3, the
subscriber of S4). The importer that compose starts is the same program
in an image (ADR 0011). The snapshot builder, which CI runs to check that
the snapshot rebuilds from nuget.org, is a third use.

ADR 0003 says everything that ships is Native AOT-compatible, and
`Directory.Build.props` sets `IsPackable` false for every project, since
nothing here was a package.

S1's acceptance criterion 5 checks the rate the importer prints against the
manifest. The prompt for slice 1a asks that this be checked through the tool
as a viewer would install it.

## Decision

1. **`VarveDemo.Tool` is a .NET tool whose command is `varve-demo`.** It is
   packable (`PackAsTool`, `IsPackable` true for it alone), and its commands
   are:
   - `varve-demo snapshot top`: the top-N id list from nuget.org's search;
   - `varve-demo snapshot build`: the snapshot, from its inputs;
   - `varve-demo snapshot verify`: a snapshot directory checked against a
     manifest;
   - `varve-demo import`: the importer (S1);
   - `varve-demo count`: S1's first answer;
   - `varve-demo deps --lock <path>`: S2.
   The code is in libraries: `VarveDemo.Snapshot`, `VarveDemo.Importer` and
   `VarveDemo.Steps`. The tool is the command line over them.
2. **Native AOT-compatible, and published as Native AOT** for the image
   (ADR 0011). The tool package itself is framework-dependent, as a `dotnet
   tool` is.
3. **Its own argument parsing.** A few commands with named options do not
   justify a dependency; `System.CommandLine` would need an ADR, and its
   trimming story is not the demo's to prove.
4. **In this slice, CI packs the tool and installs it from a local folder
   source** (`dotnet tool install --tool-path … --add-source <folder>`),
   and S1's check runs the installed `varve-demo` against the running
   compose stack. That is how criterion 5 is checked through the tool.
5. **Publishing to nuget.org waits for the demo's first release.** Until
   then, a viewer runs it from a clone (`dotnet run --project
   src/VarveDemo.Tool -- …`) or packs it locally, and the README says so. The
   release ADR decides the package id, its owner on nuget.org and its
   signing.

## Alternatives considered

- **Separate programs** for the importer, the snapshot builder and the
  steps. Rejected: three entry points sharing one snapshot reader and one
  token client, each needing its own packaging, for no gain to a viewer.
- **Scripts** (a shell or Python importer). Rejected: the demo is evidence
  about a .NET-native store, its readers and its client; a script would put
  another language's JSON-LD and HTTP stack in front of it.
- **`System.CommandLine`.** Rejected for now, above. Varve's CLI uses it,
  and a later ADR can adopt it if the commands grow.
- **Publish to nuget.org now.** Rejected: a package on nuget.org is a
  release, with an owner and a version policy, and the demo has neither yet.

## Consequences

- One `varve-demo` binary does all of the demo's work, in an image or on a
  viewer's machine, with the same code.
- `Directory.Build.props` keeps `IsPackable` false by default; the tool
  turns it on for itself, and the comment there says so.
- The tool's version is part of the snapshot's identity (ADR 0008's
  manifest), so a change to the mapping is a new tool version and a new
  snapshot.
