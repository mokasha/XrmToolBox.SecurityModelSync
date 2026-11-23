# Security Model Sync for XrmToolBox

Compares the security model of two Dynamics 365 CE / Dataverse organizations and copies the differences from the source to the target. Every change is listed first; only the lines you tick are written.

**Upsert only.** The tool creates and updates. It never deletes, disables or removes anything in the target.

## Install

Open XrmToolBox, go to **Tool Library**, search for **Security Model Sync** and install it. (Available once the tool is approved in the Tool Library; until then, build from source as described below.)

## What it syncs

| Type | Matched by | Missing in target | Different in target |
|---|---|---|---|
| Business units | Name (root to root) | Created under the same parent | Moved under the source parent |
| Users | Domain login (`domainname`), case-insensitive | Created in the same business unit | Moved to the source business unit |
| Teams (owner and group teams) | Name + business unit | Created | Reported if it sits in another business unit |
| Queues | Name | Created with name, description, email, type, owner | – |
| User and team role assignments | Role name within the business unit | Assigned | – |
| Team members, queue members | Team/queue + user | Added | – |

Not copied, by design:

- **Security roles themselves.** Move them with a solution. A role that is not in the target is listed as "Needs attention".
- Access teams, default business-unit teams (the platform creates them), personal queues of users and teams.
- Disabled source users, unless *Scope → Include disabled users* is ticked. System accounts are always ignored.
- Mailbox and email-router settings of queues.
- Application users are not created; create them in the target and compare again to sync their roles.

Moving a user to another business unit makes the platform drop the user's roles. The tool then assigns the source roles again in the same run.

## Using it

1. Open the tool on the **source** connection.
2. **Connect target...** and pick the organization to write to.
3. **Compare**. Nothing is written at this point.
4. Review the list, untick what you do not want, then **Apply selected**.
5. **Compare** again. A clean result is all matches. After an error or an interrupted run, the second compare lists only what is still outstanding, so the run can simply be repeated.

**Export CSV** saves every line with its result.

Creating users on-premises needs the account to exist in Active Directory. Online, the user must already exist in Microsoft Entra ID with a licence.

## Build and debug

Requirements: Visual Studio 2022 (or the .NET SDK) with the .NET Framework 4.8 targeting pack.

```
dotnet build SecurityModelSync.sln
dotnet run --project tests\EngineTests
```

A Debug build copies the DLL to `%AppData%\MscrmTools\XrmToolBox\Plugins` when that folder exists. To step through the code, set the project's debug start action to your `XrmToolBox.exe`.

`tests\EngineTests` runs the engine against two in-memory organizations: full sync, idempotent second run, user move, skipped dependencies, missing roles, and the upsert-only guarantee.

## Publish to the Tool Library

1. The project and icon URLs in `src\SecurityModelSync\SecurityModelSync.nuspec` point to `github.com/mokasha/XrmToolBox.SecurityModelSync`; the icon URL works once `main` is pushed.
2. Raise `<Version>` in the csproj. XrmToolBox detects updates from the assembly version.
3. `dotnet pack src\SecurityModelSync -c Release -o artifacts`
4. Push the `.nupkg` to nuget.org, or use the GitHub workflow: copy `ci\github-build.yml` to `.github\workflows\build.yml`, add the `NUGET_API_KEY` repository secret, and push a tag such as `v1.2026.11.1`.
5. Register the package id `Mohran.XrmToolBox.SecurityModelSync` on the XrmToolBox portal and wait for validation.

The package places the DLL under `lib\net48\Plugins` and declares a dependency on `XrmToolBox`, which is what the Tool Library reads.

## Layout

```
src/SecurityModelSync/
  SecurityModelSyncPlugin.cs      tool metadata and icons
  SecurityModelSyncControl.cs     UI: connections, grid, apply, export
  Engine/                         compare and apply logic, no UI dependency
tests/EngineTests/                console tests with an in-memory organization
ci/github-build.yml               GitHub Actions workflow: build, test, pack, push on tag
```

## Support

Found a bug or have an idea? Open an issue: https://github.com/mokasha/XrmToolBox.SecurityModelSync/issues

Please include the XrmToolBox version, whether the organizations are on-premises or online, and the exported CSV if you can share it.

## Author

**Mohamed Mohran**  
Dynamics 365 & Power Platform Solution Architect · Consultant

[LinkedIn](https://www.linkedin.com/in/mohamed-mohran/) · [GitHub](https://github.com/mokasha)

Repository: https://github.com/mokasha/XrmToolBox.SecurityModelSync
