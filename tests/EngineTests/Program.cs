using System;
using System.Collections.Generic;
using System.Linq;
using SecurityModelSync.Engine;

namespace SecurityModelSync.Tests
{
    /// <summary>
    /// Engine tests against two in-memory organizations. No test framework: run the executable,
    /// exit code 0 means every check passed.
    /// </summary>
    public static class Program
    {
        private static int _failed;

        public static int Main()
        {
            Run("Full sync into an empty target, then a second compare finds nothing to do", FullSync);
            Run("A user in the wrong business unit is moved and gets the source roles back", UserMove);
            Run("A line whose dependency was not selected is skipped, not failed", DependencySkipped);
            Run("A role that is not in the target is reported and never created", MissingRoleIsWarning);
            Run("Disabled and system users are left out; nothing in the target is removed", UpsertOnly);
            Run("Scope options hide the lines of the categories that are switched off", ScopeOptions);

            Console.WriteLine();
            Console.WriteLine(_failed == 0 ? "ALL TESTS PASSED" : _failed + " TEST(S) FAILED");
            return _failed == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------ scenarios

        private static FakeOrg Source(out Guid alice, out Guid bob)
        {
            var src = new FakeOrg("Source Root", "System Administrator", "Salesperson", "CSR Manager");
            var sales = src.AddBu("Sales", src.RootBu);
            var north = src.AddBu("Sales North", sales);

            alice = src.AddUser("corp\\alice", "Alice", "A", src.RootBu);
            bob = src.AddUser("corp\\bob", "Bob", "B", sales);
            var carol = src.AddUser("corp\\carol", "Carol", "C", north);
            src.AddUser("corp\\dave", "Dave", "D", sales, disabled: true);

            src.GiveUserRole(alice, "System Administrator");
            src.GiveUserRole(bob, "Salesperson");
            src.GiveUserRole(bob, "CSR Manager");
            src.GiveUserRole(carol, "Salesperson");

            var team = src.AddTeam("Sales Team", sales);
            src.GiveTeamRole(team, "Salesperson");
            src.AddTeamMember(team, bob);
            src.AddTeamMember(team, carol);

            // role on the default team of a business unit that does not exist in the target yet
            var northDefault = src.Rows("team").Single(t => t.GetAttributeValue<bool>("isdefault") &&
                                                            t.GetAttributeValue<string>("name") == "Sales North").Id;
            src.GiveTeamRole(northDefault, "CSR Manager");

            var queue = src.AddQueue("Support");
            src.AddQueueMember(queue, bob);
            return src;
        }

        private static FakeOrg EmptyTarget()
        {
            return new FakeOrg("Target Root", "System Administrator", "Salesperson", "CSR Manager");
        }

        private static void FullSync()
        {
            Guid alice, bob;
            var src = Source(out alice, out bob);
            var tgt = EmptyTarget();
            tgt.AddUser("CORP\\Alice", "Alice", "A", tgt.RootBu); // same login, different casing

            var engine = new SecuritySyncEngine(src, tgt, new SyncOptions());
            var items = engine.Compare();

            Check(Count(items, SyncCategory.BusinessUnit, SyncStatus.Missing) == 2, "two business units to create");
            Check(Count(items, SyncCategory.User, SyncStatus.Missing) == 2, "bob and carol to create");
            Check(Count(items, SyncCategory.User, SyncStatus.Match) == 2, "alice and admin already there");
            Check(Count(items, SyncCategory.UserRole, SyncStatus.Missing) == 4, "four role assignments to add");
            Check(tgt.Writes == 0, "compare does not write to the target");

            engine.Apply(items);
            var problems = items.Where(i => i.Selected && i.Result != SyncResult.Done).ToList();
            Check(problems.Count == 0, "every selected line applied: " + string.Join("; ", problems.Select(p => p.Name + " - " + p.Message)));

            var sales = tgt.ByName("businessunit", "name", "Sales");
            var north = tgt.ByName("businessunit", "name", "Sales North");
            Check(sales != null && north != null, "business units created");
            Check(Parent(north) == sales.Id && Parent(sales) == tgt.RootBu, "hierarchy preserved");

            var tBob = tgt.ByName("systemuser", "domainname", "corp\\bob");
            var tCarol = tgt.ByName("systemuser", "domainname", "corp\\carol");
            Check(tBob != null && tgt.UserBu(tBob.Id) == sales.Id, "bob created in Sales");
            Check(tCarol != null && tgt.UserBu(tCarol.Id) == north.Id, "carol created in Sales North");
            Check(string.Join(",", tgt.RolesOfUser(tBob.Id)) == "CSR Manager,Salesperson", "bob has both roles from the Sales copies");

            var tAlice = tgt.ByName("systemuser", "domainname", "CORP\\Alice");
            Check(string.Join(",", tgt.RolesOfUser(tAlice.Id)) == "System Administrator", "existing user got the missing role");

            var tTeam = tgt.ByName("team", "name", "Sales Team");
            Check(tTeam != null && tgt.Linked("teammembership", "teamid", tTeam.Id, "systemuserid", tBob.Id), "team created with bob as member");
            Check(tgt.Linked("teamroles", "teamid", tTeam.Id, "roleid", tgt.RoleId(sales.Id, "Salesperson")), "team role assigned");

            var northDefault = tgt.Rows("team").Single(t => t.GetAttributeValue<bool>("isdefault") && Parent(t, "businessunitid") == north.Id);
            Check(tgt.Linked("teamroles", "teamid", northDefault.Id, "roleid", tgt.RoleId(north.Id, "CSR Manager")), "default team of the new business unit got its role");

            var tQueue = tgt.ByName("queue", "name", "Support");
            Check(tQueue != null && tgt.Linked("queuemembership", "queueid", tQueue.Id, "systemuserid", tBob.Id), "queue created with bob as member");
            Check(tgt.ByName("queue", "name", "<corp\\bob>") != null && tgt.Rows("queue").Count(q => q.GetAttributeValue<string>("name") == "<corp\\bob>") == 1,
                "personal queues are not copied");

            // idempotence: this is what makes an interrupted run resumable
            var writes = tgt.Writes;
            var again = new SecuritySyncEngine(src, tgt, new SyncOptions());
            var second = again.Compare();
            var outstanding = second.Where(i => i.Status != SyncStatus.Match).ToList();
            Check(outstanding.Count == 0, "second compare is all matches: " + string.Join("; ", outstanding.Select(o => o.Category + " " + o.Name)));
            again.Apply(second);
            Check(tgt.Writes == writes, "second apply writes nothing");
        }

        private static void UserMove()
        {
            Guid alice, bob;
            var src = Source(out alice, out bob);
            var tgt = EmptyTarget();
            var sales = tgt.AddBu("Sales", tgt.RootBu);
            var tBob = tgt.AddUser("corp\\bob", "Bob", "B", tgt.RootBu);
            tgt.GiveUserRole(tBob, "Salesperson"); // root copy of the role

            var engine = new SecuritySyncEngine(src, tgt, new SyncOptions());
            var items = engine.Compare();
            var move = items.Single(i => i.Category == SyncCategory.User && i.Name.Contains("corp\\bob"));
            Check(move.Status == SyncStatus.Different, "bob flagged as different");

            engine.Apply(items);
            Check(tgt.UserBu(tBob) == sales, "bob moved to Sales");
            Check(string.Join(",", tgt.RolesOfUser(tBob)) == "CSR Manager,Salesperson", "roles assigned again after the move");
            Check(tgt.Linked("systemuserroles", "systemuserid", tBob, "roleid", tgt.RoleId(sales, "Salesperson")), "role is the Sales copy");
        }

        private static void DependencySkipped()
        {
            Guid alice, bob;
            var src = Source(out alice, out bob);
            var tgt = EmptyTarget();

            var engine = new SecuritySyncEngine(src, tgt, new SyncOptions());
            var items = engine.Compare();
            foreach (var bu in items.Where(i => i.Category == SyncCategory.BusinessUnit)) bu.Selected = false;

            engine.Apply(items);
            var bobLine = items.Single(i => i.Category == SyncCategory.User && i.Name.Contains("corp\\bob"));
            Check(bobLine.Result == SyncResult.Skipped && bobLine.Message.Contains("Sales"), "user creation skipped with the reason");
            Check(tgt.ByName("systemuser", "domainname", "corp\\bob") == null, "bob not created in the wrong place");
            Check(items.All(i => i.Result != SyncResult.Failed), "nothing reported as failed");

            var aliceRole = items.Single(i => i.Category == SyncCategory.UserRole && i.Name.Contains("corp\\alice"));
            Check(aliceRole.Result == SyncResult.Done, "independent lines still applied");
        }

        private static void MissingRoleIsWarning()
        {
            Guid alice, bob;
            var src = Source(out alice, out bob);
            var tgt = new FakeOrg("Target Root", "System Administrator", "Salesperson"); // no CSR Manager
            var roleCount = 0;

            var engine = new SecuritySyncEngine(src, tgt, new SyncOptions());
            var items = engine.Compare();
            roleCount = tgt.Rows("role").Select(r => r.GetAttributeValue<string>("name")).Distinct().Count();
            engine.Apply(items);

            var csr = items.Where(i => i.Category == SyncCategory.UserRole && i.Name.EndsWith("CSR Manager")).ToList();
            Check(csr.Count == 1 && (csr[0].Status == SyncStatus.Warning || csr[0].Result == SyncResult.Skipped),
                "missing role surfaced, not applied: " + csr[0].Status + "/" + csr[0].Result);
            Check(tgt.Rows("role").Select(r => r.GetAttributeValue<string>("name")).Distinct().Count() == roleCount, "no role created");
            var tBob = tgt.ByName("systemuser", "domainname", "corp\\bob");
            Check(string.Join(",", tgt.RolesOfUser(tBob.Id)) == "Salesperson", "the roles that do exist are still assigned");
        }

        private static void UpsertOnly()
        {
            Guid alice, bob;
            var src = Source(out alice, out bob);
            var tgt = EmptyTarget();
            var extra = tgt.AddUser("corp\\zed", "Zed", "Z", tgt.RootBu);
            tgt.GiveUserRole(extra, "System Administrator");
            var extraTeam = tgt.AddTeam("Target Only Team", tgt.RootBu);

            var engine = new SecuritySyncEngine(src, tgt, new SyncOptions());
            var items = engine.Compare();
            engine.Apply(items); // FakeOrg throws on Delete and Disassociate

            Check(items.All(i => !i.Name.Contains("corp\\dave")), "disabled source user ignored by default");
            Check(items.All(i => !i.Name.Contains("SYSTEM")), "system account ignored");
            Check(tgt.ByName("systemuser", "domainname", "corp\\dave") == null, "disabled user not created");
            Check(tgt.RolesOfUser(extra).Count == 1 && tgt.Rows("team").Any(t => t.Id == extraTeam), "target-only records untouched");

            var withDisabled = new SecuritySyncEngine(src, EmptyTarget(), new SyncOptions { IncludeDisabledUsers = true }).Compare();
            Check(withDisabled.Any(i => i.Category == SyncCategory.User && i.Name.Contains("corp\\dave")), "disabled user included on request");
        }

        private static void ScopeOptions()
        {
            Guid alice, bob;
            var src = Source(out alice, out bob);
            var options = new SyncOptions { Queues = false, QueueMembers = false, TeamMembers = false };
            var items = new SecuritySyncEngine(src, EmptyTarget(), options).Compare();

            Check(!items.Any(i => i.Category == SyncCategory.Queue || i.Category == SyncCategory.QueueMember ||
                                  i.Category == SyncCategory.TeamMember), "switched-off categories produce no lines");
            Check(items.Any(i => i.Category == SyncCategory.Team), "other categories still compared");
        }

        // ------------------------------------------------------------------ plumbing

        private static int Count(IEnumerable<SyncItem> items, SyncCategory category, SyncStatus status)
        {
            return items.Count(i => i.Category == category && i.Status == status);
        }

        private static Guid Parent(Microsoft.Xrm.Sdk.Entity entity, string attribute = "parentbusinessunitid")
        {
            var reference = entity.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>(attribute);
            return reference == null ? Guid.Empty : reference.Id;
        }

        private static bool _currentOk;

        private static void Check(bool condition, string what)
        {
            if (condition) return;
            _currentOk = false;
            Console.WriteLine("    FAILED: " + what);
        }

        private static void Run(string name, Action test)
        {
            _currentOk = true;
            try
            {
                test();
            }
            catch (Exception ex)
            {
                _currentOk = false;
                Console.WriteLine("    EXCEPTION: " + ex);
            }
            if (!_currentOk) _failed++;
            Console.WriteLine((_currentOk ? "[pass] " : "[FAIL] ") + name);
        }
    }
}
