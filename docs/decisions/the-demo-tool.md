---
set: the-demo-tool
namespace: varvedemo
adr: 0012
decisions:
  - key: OneToolVarveDemo
    statement: "VarveDemo.Tool is a packable .NET tool with the command varve-demo, over the libraries VarveDemo.Snapshot, VarveDemo.Importer and VarveDemo.Steps"
  - key: ToolIsNativeAotCompatible
    statement: "The tool is Native AOT-compatible, and the image publishes it as Native AOT"
  - key: OwnArgumentParsing
    statement: "The tool parses its own arguments rather than take a command-line dependency"
  - key: CiInstallsFromALocalSource
    statement: "CI packs the tool and installs it from a local folder source, and S1's check runs the installed varve-demo"
  - key: NugetOrgWaitsForTheFirstRelease
    statement: "The tool is published to nuget.org only from the demo's first release, which an ADR of its own decides"
---

The rulings of [ADR 0012](../adr/0012-the-demo-tool.md), filed unaccepted by
session 03 on #5, 2026-11-03. Every citation is `CS0618` until the maintainer
accepts them.
