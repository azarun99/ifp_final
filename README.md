# Library Lending

A console application for a small library desk. It uses C# and .NET SDK 10.0 (any 10.0.x works; verified with 10.0.404). No external packages or services are needed.

## Build and run

Run these commands in PowerShell from the project root.

```powershell
dotnet build src/Library/Library.csproj
Copy-Item data/library.sample.json data/library.local.json
dotnet run --project src/Library/Library.csproj -- data/library.local.json
dotnet run --project tests/Library.Tests/Library.Tests.csproj
```

Use a fresh copy for each demo. Successful writes update the selected JSON file. With no path argument the sample file itself is used. Enter dates as yyyy-MM-dd. Relative paths are resolved from the current directory. Run the test project from a writable directory. Its exit status is 0 only when every test passes.

## Workflows

| Command | Inputs and rules | Result |
|---|---|---|
| 1 Register member | Positive unique ID, nonblank name and membership | New reader record |
| 2 Acquire copies | Positive quantity and ID. Reused IDs must match title and category | New catalog entry or increased stock |
| 3 Issue loan | Existing reader and book, unique loan ID, available copy, at most two existing active loans, no duplicate title or overdue loan | Loan with a policy-dependent due date |
| 4 Renew loan | Open loan, date on or before due date, at most one renewal | Due date moves seven days |
| 5 Return loan | Open loan, return date on or after issue date | Closed loan, freed copy and late days |
| 6 Withdraw copies | Existing book, positive quantity, retain one catalog copy and cover active loans | Reduced stock |
| 7 Overdue report | As-of date | Open overdue loans, longest delay first, then loan ID |
| 8 Demand report | Inclusive date range | Loan and distinct title counts by category, most loans first, then category |
| 9 List records | None | Book, member and loan IDs for the demo |

The regular policy gives Standard and Extended members 14 and 21 days. The short policy gives them 7 and 10 days. A loan due today is still on time. The sample dates are fixed for repeatable demonstrations.

## Defense demo

Start with a fresh sample copy. Choose 3, then enter book 103, member 2, loan 502, date 2026-11-13 and policy 2. The due date is 2026-11-23. Choose 5 with loan 999 and date 2026-11-13 to demonstrate the explicit not_found outcome. Run report 7 on 2026-11-13. It lists loan 500 with five days late and prints when enumeration starts. Run report 8 from 2026-11-01 through 2026-11-13. Computing, Fiction and Science each have one loan. Choose 0 and run the automated tests.

## Submission version

- Repository URL: https://github.com/azarun99/ifp_final
- Branch: master
- Final commit:


## Scope

Single-user console application with local JSON storage. Dates are supplied by the operator. There is no database, login, reservation queue or multi-user concurrency control.
