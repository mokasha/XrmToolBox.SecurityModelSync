using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace SecurityModelSync.Engine
{
    /// <summary>
    /// Compares the security model of a source organization with a target organization and
    /// applies the selected differences to the target. Upsert only: nothing in the target is
    /// deleted, disabled or removed. Records are matched by name (business units, teams, queues,
    /// roles) and by domain login (users), never by id, so the organizations do not have to
    /// share a database history.
    /// </summary>
    public class SecuritySyncEngine
    {
        private readonly IOrganizationService _source;
        private readonly IOrganizationService _target;
        private readonly SyncOptions _options;
        private readonly Action<string> _progress;
        private ApplyContext _context;

        public SecuritySyncEngine(IOrganizationService source, IOrganizationService target,
            SyncOptions options, Action<string> progress = null)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (target == null) throw new ArgumentNullException("target");
            _source = source;
            _target = target;
            _options = options ?? new SyncOptions();
            _progress = progress ?? (m => { });
        }

        // ------------------------------------------------------------------ compare

        public List<SyncItem> Compare()
        {
            var src = OrgSnapshot.Load(_source, m => _progress("Source: reading " + m + "..."));
            var tgt = OrgSnapshot.Load(_target, m => _progress("Target: reading " + m + "..."));

            _progress("Comparing...");
            var ctx = new ApplyContext(_target, src, tgt);
            ctx.CurrentUserId = ((WhoAmIResponse)_target.Execute(new WhoAmIRequest())).UserId;

            var items = new List<SyncItem>();

            CompareBusinessUnits(src, tgt, ctx, items);
            var pendingMoves = new HashSet<Guid>();
            var users = CompareUsers(src, tgt, ctx, items, pendingMoves);
            var teams = CompareTeams(src, tgt, ctx, items);
            var queues = CompareQueues(src, tgt, ctx, items);

            if (_options.UserRoles) CompareUserRoles(src, tgt, ctx, items, users, pendingMoves);
            if (_options.TeamRoles) CompareTeamRoles(src, tgt, ctx, items, teams);
            if (_options.TeamMembers) CompareTeamMembers(src, tgt, ctx, items, teams, users);
            if (_options.QueueMembers) CompareQueueMembers(src, tgt, ctx, items, queues, users);

            for (var i = 0; i < items.Count; i++)
            {
                items[i].Order = i;
                items[i].Selected = items[i].IsActionable;
            }

            _context = ctx;
            return items
                .OrderBy(i => (int)i.Category)
                .ThenBy(i => i.Status == SyncStatus.Match ? 1 : 0)
                .ToList();
        }

        private void CompareBusinessUnits(OrgSnapshot src, OrgSnapshot tgt, ApplyContext ctx, List<SyncItem> items)
        {
            var targetByKey = new Dictionary<string, Entity>();
            foreach (var bu in tgt.BusinessUnits)
            {
                var key = tgt.BuKey(bu.Id);
                if (key != null && !targetByKey.ContainsKey(key)) targetByKey[key] = bu;
            }

            // Parents before children, so a new business unit can be created under a new parent.
            foreach (var bu in src.BusinessUnits.OrderBy(b => Depth(src, b)))
            {
                var name = bu.GetAttributeValue<string>("name");
                var key = src.BuKey(bu.Id);
                if (key == null) continue;

                var sourceId = bu.Id;
                var sourceParentId = OrgSnapshot.RefId(bu, "parentbusinessunitid");
                SyncItem item;
                Entity match;

                if (targetByKey.TryGetValue(key, out match))
                {
                    ctx.Bu[sourceId] = match.Id;
                    var targetId = match.Id;
                    var sourceParentKey = src.BuKey(sourceParentId);
                    var targetParentKey = tgt.BuKey(OrgSnapshot.RefId(match, "parentbusinessunitid"));

                    if (key == OrgSnapshot.RootKey)
                    {
                        item = Line(SyncCategory.BusinessUnit, name, SyncStatus.Match,
                            "Root business unit (target: '" + match.GetAttributeValue<string>("name") + "')");
                    }
                    else if (sourceParentKey == targetParentKey)
                    {
                        item = Line(SyncCategory.BusinessUnit, name, SyncStatus.Match, null);
                    }
                    else
                    {
                        item = Line(SyncCategory.BusinessUnit, name, SyncStatus.Different,
                            "Move under '" + src.BuName(sourceParentId) + "'");
                        item.Apply = c => c.Target.Execute(new SetParentBusinessUnitRequest
                        {
                            BusinessUnitId = targetId,
                            ParentId = c.RequireBu(sourceParentId)
                        });
                    }
                }
                else
                {
                    item = Line(SyncCategory.BusinessUnit, name, SyncStatus.Missing,
                        "Create under '" + src.BuName(sourceParentId) + "'");
                    item.Apply = c =>
                    {
                        var entity = new Entity("businessunit");
                        entity["name"] = name;
                        entity["parentbusinessunitid"] = new EntityReference("businessunit", c.RequireBu(sourceParentId));
                        c.Bu[sourceId] = c.Target.Create(entity);
                        c.BusinessUnitCreated();
                    };
                }

                if (_options.BusinessUnits) items.Add(item);
            }
        }

        private HashSet<Guid> CompareUsers(OrgSnapshot src, OrgSnapshot tgt, ApplyContext ctx,
            List<SyncItem> items, HashSet<Guid> pendingMoves)
        {
            var included = new HashSet<Guid>();

            var targetByKey = new Dictionary<string, Entity>();
            foreach (var user in tgt.Users)
            {
                var key = OrgSnapshot.UserKey(user);
                if (key != null && !targetByKey.ContainsKey(key)) targetByKey[key] = user;
            }

            foreach (var user in src.Users.OrderBy(u => u.GetAttributeValue<string>("fullname")))
            {
                if (IsSystemAccount(user)) continue;
                var disabled = user.GetAttributeValue<bool>("isdisabled");
                if (disabled && !_options.IncludeDisabledUsers) continue;

                included.Add(user.Id);

                var sourceId = user.Id;
                var label = OrgSnapshot.UserLabel(user);
                var sourceBuId = OrgSnapshot.RefId(user, "businessunitid");
                var sourceBuName = src.BuName(sourceBuId);
                SyncItem item;
                Entity match;

                if (targetByKey.TryGetValue(OrgSnapshot.UserKey(user), out match))
                {
                    var targetId = match.Id;
                    var targetBuId = OrgSnapshot.RefId(match, "businessunitid");
                    ctx.User[sourceId] = targetId;
                    ctx.TargetUserBu[targetId] = targetBuId;

                    if (src.BuKey(sourceBuId) != tgt.BuKey(targetBuId))
                    {
                        pendingMoves.Add(sourceId);
                        item = Line(SyncCategory.User, label, SyncStatus.Different,
                            "Move from business unit '" + tgt.BuName(targetBuId) + "' to '" + sourceBuName +
                            "'. The platform removes the user's roles on a move; the source roles are assigned again below.");
                        item.Apply = c =>
                        {
                            var bu = c.RequireBu(sourceBuId);
                            c.Target.Execute(new SetBusinessSystemUserRequest
                            {
                                UserId = targetId,
                                BusinessId = bu,
                                ReassignPrincipal = new EntityReference("systemuser", targetId)
                            });
                            c.TargetUserBu[targetId] = bu;
                        };
                    }
                    else if (match.GetAttributeValue<bool>("isdisabled") && !disabled)
                    {
                        item = Line(SyncCategory.User, label, SyncStatus.Warning,
                            "Disabled in the target. Enable the user there if that is intended.");
                    }
                    else
                    {
                        item = Line(SyncCategory.User, label, SyncStatus.Match, null);
                    }
                }
                else if (user.Contains("applicationid") && user["applicationid"] != null)
                {
                    item = Line(SyncCategory.User, label, SyncStatus.Warning,
                        "Application user. Create it in the target first; its roles are synced on the next run.");
                }
                else
                {
                    item = Line(SyncCategory.User, label, SyncStatus.Missing,
                        "Create in business unit '" + sourceBuName + "'");
                    var source = user;
                    item.Apply = c =>
                    {
                        var bu = c.RequireBu(sourceBuId);
                        var entity = new Entity("systemuser");
                        entity["domainname"] = source.GetAttributeValue<string>("domainname");
                        entity["businessunitid"] = new EntityReference("businessunit", bu);
                        CopyIfPresent(source, entity, "firstname");
                        CopyIfPresent(source, entity, "lastname");
                        CopyIfPresent(source, entity, "internalemailaddress");
                        var id = c.Target.Create(entity);
                        c.User[sourceId] = id;
                        c.TargetUserBu[id] = bu;
                    };
                }

                if (_options.Users) items.Add(item);
            }

            return included;
        }

        private HashSet<Guid> CompareTeams(OrgSnapshot src, OrgSnapshot tgt, ApplyContext ctx, List<SyncItem> items)
        {
            var included = new HashSet<Guid>();

            var targetByKey = new Dictionary<string, Entity>();
            var targetByName = new Dictionary<string, List<Entity>>();
            foreach (var team in tgt.Teams)
            {
                if (team.GetAttributeValue<bool>("isdefault")) continue;
                var name = OrgSnapshot.Norm(team.GetAttributeValue<string>("name"));
                if (name == null) continue;

                var key = tgt.BuKey(OrgSnapshot.RefId(team, "businessunitid")) + "|" + name;
                if (!targetByKey.ContainsKey(key)) targetByKey[key] = team;

                List<Entity> sameName;
                if (!targetByName.TryGetValue(name, out sameName)) targetByName[name] = sameName = new List<Entity>();
                sameName.Add(team);
            }

            foreach (var team in src.Teams.OrderBy(t => t.GetAttributeValue<string>("name")))
            {
                var teamType = OrgSnapshot.OptionValue(team, "teamtype", 0);
                if (teamType == 1) continue; // access teams are created by the platform per record

                included.Add(team.Id);

                var sourceId = team.Id;
                var sourceBuId = OrgSnapshot.RefId(team, "businessunitid");

                if (team.GetAttributeValue<bool>("isdefault"))
                {
                    // Created with the business unit; resolved now if it exists, at apply time otherwise.
                    Guid ignored;
                    ctx.TryResolveTeam(sourceId, out ignored);
                    continue;
                }

                var name = team.GetAttributeValue<string>("name");
                var normName = OrgSnapshot.Norm(name);
                if (normName == null) continue;

                SyncItem item;
                Entity match;
                List<Entity> sameName;

                if (targetByKey.TryGetValue(src.BuKey(sourceBuId) + "|" + normName, out match))
                {
                    ctx.Team[sourceId] = match.Id;
                    ctx.TargetTeamBu[match.Id] = OrgSnapshot.RefId(match, "businessunitid");
                    item = Line(SyncCategory.Team, name, SyncStatus.Match, null);
                }
                else if (targetByName.TryGetValue(normName, out sameName) && sameName.Count == 1)
                {
                    match = sameName[0];
                    var targetBuId = OrgSnapshot.RefId(match, "businessunitid");
                    ctx.Team[sourceId] = match.Id;
                    ctx.TargetTeamBu[match.Id] = targetBuId;
                    item = Line(SyncCategory.Team, name, SyncStatus.Warning,
                        "In the target this team is in business unit '" + tgt.BuName(targetBuId) + "' (source: '" +
                        src.BuName(sourceBuId) + "'). It is left where it is; roles and members are still synced.");
                }
                else
                {
                    item = Line(SyncCategory.Team, name, SyncStatus.Missing,
                        "Create in business unit '" + src.BuName(sourceBuId) + "'");
                    var source = team;
                    item.Apply = c =>
                    {
                        var bu = c.RequireBu(sourceBuId);
                        Guid admin;
                        if (!c.User.TryGetValue(OrgSnapshot.RefId(source, "administratorid"), out admin))
                        {
                            admin = c.CurrentUserId;
                        }

                        var entity = new Entity("team");
                        entity["name"] = name;
                        entity["businessunitid"] = new EntityReference("businessunit", bu);
                        entity["administratorid"] = new EntityReference("systemuser", admin);
                        entity["teamtype"] = new OptionSetValue(teamType);
                        CopyIfPresent(source, entity, "description");
                        CopyIfPresent(source, entity, "azureactivedirectoryobjectid");
                        CopyIfPresent(source, entity, "membershiptype");

                        var id = c.Target.Create(entity);
                        c.Team[sourceId] = id;
                        c.TargetTeamBu[id] = bu;
                    };
                }

                if (_options.Teams) items.Add(item);
            }

            return included;
        }

        private HashSet<Guid> CompareQueues(OrgSnapshot src, OrgSnapshot tgt, ApplyContext ctx, List<SyncItem> items)
        {
            var included = new HashSet<Guid>();

            var targetDefaults = DefaultQueueIds(tgt);
            var targetByName = new Dictionary<string, Entity>();
            foreach (var queue in tgt.Queues)
            {
                if (targetDefaults.Contains(queue.Id)) continue;
                var key = OrgSnapshot.Norm(queue.GetAttributeValue<string>("name"));
                if (key != null && !targetByName.ContainsKey(key)) targetByName[key] = queue;
            }

            var sourceDefaults = DefaultQueueIds(src);
            foreach (var queue in src.Queues.OrderBy(q => q.GetAttributeValue<string>("name")))
            {
                // The personal queue of a user or team is created by the platform with its owner.
                if (sourceDefaults.Contains(queue.Id)) continue;
                if (OrgSnapshot.OptionValue(queue, "statecode", 0) != 0) continue;

                var name = queue.GetAttributeValue<string>("name");
                var key = OrgSnapshot.Norm(name);
                if (key == null) continue;

                included.Add(queue.Id);

                var sourceId = queue.Id;
                SyncItem item;
                Entity match;

                if (targetByName.TryGetValue(key, out match))
                {
                    ctx.Queue[sourceId] = match.Id;
                    item = Line(SyncCategory.Queue, name, SyncStatus.Match, null);
                }
                else
                {
                    item = Line(SyncCategory.Queue, name, SyncStatus.Missing, "Create");
                    var source = queue;
                    item.Apply = c =>
                    {
                        var entity = new Entity("queue");
                        entity["name"] = name;
                        CopyIfPresent(source, entity, "description");
                        CopyIfPresent(source, entity, "emailaddress");
                        CopyIfPresent(source, entity, "queueviewtype");

                        // Keep the same owner when it exists in the target; otherwise the caller owns it.
                        var owner = source.GetAttributeValue<EntityReference>("ownerid");
                        Guid ownerId;
                        if (owner != null && owner.LogicalName == "systemuser" && c.User.TryGetValue(owner.Id, out ownerId))
                        {
                            entity["ownerid"] = new EntityReference("systemuser", ownerId);
                        }
                        else if (owner != null && owner.LogicalName == "team" && c.TryResolveTeam(owner.Id, out ownerId))
                        {
                            entity["ownerid"] = new EntityReference("team", ownerId);
                        }

                        c.Queue[sourceId] = c.Target.Create(entity);
                    };
                }

                if (_options.Queues) items.Add(item);
            }

            return included;
        }

        private void CompareUserRoles(OrgSnapshot src, OrgSnapshot tgt, ApplyContext ctx, List<SyncItem> items,
            HashSet<Guid> users, HashSet<Guid> pendingMoves)
        {
            foreach (var pair in src.UserRoles)
            {
                var sourceUserId = pair.Item1;
                Entity role;
                if (!users.Contains(sourceUserId) || !src.RoleById.TryGetValue(pair.Item2, out role)) continue;

                var user = src.UserById[sourceUserId];
                var roleName = role.GetAttributeValue<string>("name");
                var label = OrgSnapshot.UserLabel(user) + " → " + roleName;

                Guid targetUserId;
                var mapped = ctx.User.TryGetValue(sourceUserId, out targetUserId);
                var settled = mapped && !pendingMoves.Contains(sourceUserId);

                // Business unit the user will be in once the lines above are applied.
                Guid targetBuId;
                var buKnown = settled
                    ? ctx.TargetUserBu.TryGetValue(targetUserId, out targetBuId)
                    : ctx.Bu.TryGetValue(OrgSnapshot.RefId(user, "businessunitid"), out targetBuId);

                if (buKnown)
                {
                    Guid targetRoleId;
                    if (!ctx.TryFindRole(targetBuId, roleName, out targetRoleId))
                    {
                        items.Add(Line(SyncCategory.UserRole, label, SyncStatus.Warning,
                            "Security role '" + roleName + "' does not exist in the target. Import it with a solution, then compare again."));
                        continue;
                    }
                    if (settled && tgt.UserRoles.Contains(Tuple.Create(targetUserId, targetRoleId)))
                    {
                        items.Add(Line(SyncCategory.UserRole, label, SyncStatus.Match, null));
                        continue;
                    }
                }

                var item = Line(SyncCategory.UserRole, label, SyncStatus.Missing, "Assign role to user");
                item.Apply = c =>
                {
                    var userId = c.RequireUser(sourceUserId);
                    var roleId = c.RequireRole(c.TargetUserBu[userId], roleName);
                    c.Associate("systemuser", userId, "systemuserroles_association", "role", roleId);
                };
                items.Add(item);
            }
        }

        private void CompareTeamRoles(OrgSnapshot src, OrgSnapshot tgt, ApplyContext ctx, List<SyncItem> items,
            HashSet<Guid> teams)
        {
            foreach (var pair in src.TeamRoles)
            {
                var sourceTeamId = pair.Item1;
                Entity role;
                if (!teams.Contains(sourceTeamId) || !src.RoleById.TryGetValue(pair.Item2, out role)) continue;

                var team = src.TeamById[sourceTeamId];
                var roleName = role.GetAttributeValue<string>("name");
                var label = team.GetAttributeValue<string>("name") + " → " + roleName;

                Guid targetTeamId;
                var mapped = ctx.Team.TryGetValue(sourceTeamId, out targetTeamId);

                Guid targetBuId;
                var buKnown = mapped
                    ? ctx.TargetTeamBu.TryGetValue(targetTeamId, out targetBuId)
                    : ctx.Bu.TryGetValue(OrgSnapshot.RefId(team, "businessunitid"), out targetBuId);

                if (buKnown)
                {
                    Guid targetRoleId;
                    if (!ctx.TryFindRole(targetBuId, roleName, out targetRoleId))
                    {
                        items.Add(Line(SyncCategory.TeamRole, label, SyncStatus.Warning,
                            "Security role '" + roleName + "' does not exist in the target. Import it with a solution, then compare again."));
                        continue;
                    }
                    if (mapped && tgt.TeamRoles.Contains(Tuple.Create(targetTeamId, targetRoleId)))
                    {
                        items.Add(Line(SyncCategory.TeamRole, label, SyncStatus.Match, null));
                        continue;
                    }
                }

                var item = Line(SyncCategory.TeamRole, label, SyncStatus.Missing, "Assign role to team");
                item.Apply = c =>
                {
                    var teamId = c.RequireTeam(sourceTeamId);
                    var roleId = c.RequireRole(c.TargetTeamBu[teamId], roleName);
                    c.Associate("team", teamId, "teamroles_association", "role", roleId);
                };
                items.Add(item);
            }
        }

        private void CompareTeamMembers(OrgSnapshot src, OrgSnapshot tgt, ApplyContext ctx, List<SyncItem> items,
            HashSet<Guid> teams, HashSet<Guid> users)
        {
            foreach (var pair in src.TeamMembers)
            {
                var sourceTeamId = pair.Item1;
                var sourceUserId = pair.Item2;
                if (!teams.Contains(sourceTeamId) || !users.Contains(sourceUserId)) continue;

                var team = src.TeamById[sourceTeamId];
                // Default teams follow the user's business unit; group teams follow the directory group.
                if (team.GetAttributeValue<bool>("isdefault")) continue;
                if (OrgSnapshot.OptionValue(team, "teamtype", 0) != 0) continue;

                var label = team.GetAttributeValue<string>("name") + " ← " + OrgSnapshot.UserLabel(src.UserById[sourceUserId]);

                Guid targetTeamId, targetUserId;
                if (ctx.Team.TryGetValue(sourceTeamId, out targetTeamId) &&
                    ctx.User.TryGetValue(sourceUserId, out targetUserId) &&
                    tgt.TeamMembers.Contains(Tuple.Create(targetTeamId, targetUserId)))
                {
                    items.Add(Line(SyncCategory.TeamMember, label, SyncStatus.Match, null));
                    continue;
                }

                var item = Line(SyncCategory.TeamMember, label, SyncStatus.Missing, "Add user to team");
                item.Apply = c => c.Target.Execute(new AddMembersTeamRequest
                {
                    TeamId = c.RequireTeam(sourceTeamId),
                    MemberIds = new[] { c.RequireUser(sourceUserId) }
                });
                items.Add(item);
            }
        }

        private void CompareQueueMembers(OrgSnapshot src, OrgSnapshot tgt, ApplyContext ctx, List<SyncItem> items,
            HashSet<Guid> queues, HashSet<Guid> users)
        {
            foreach (var pair in src.QueueMembers)
            {
                var sourceQueueId = pair.Item1;
                var sourceUserId = pair.Item2;
                if (!queues.Contains(sourceQueueId) || !users.Contains(sourceUserId)) continue;

                var label = src.QueueById[sourceQueueId].GetAttributeValue<string>("name") + " ← " +
                            OrgSnapshot.UserLabel(src.UserById[sourceUserId]);

                Guid targetQueueId, targetUserId;
                if (ctx.Queue.TryGetValue(sourceQueueId, out targetQueueId) &&
                    ctx.User.TryGetValue(sourceUserId, out targetUserId) &&
                    tgt.QueueMembers.Contains(Tuple.Create(targetQueueId, targetUserId)))
                {
                    items.Add(Line(SyncCategory.QueueMember, label, SyncStatus.Match, null));
                    continue;
                }

                var item = Line(SyncCategory.QueueMember, label, SyncStatus.Missing, "Add user to queue");
                item.Apply = c => c.Associate("queue", c.RequireQueue(sourceQueueId),
                    "queuemembership_association", "systemuser", c.RequireUser(sourceUserId));
                items.Add(item);
            }
        }

        // ------------------------------------------------------------------ apply

        /// <summary>
        /// Applies the selected lines in dependency order. A failed line does not stop the run.
        /// Running Compare again afterwards shows only what is still outstanding, which is how an
        /// interrupted run is resumed.
        /// </summary>
        public void Apply(IEnumerable<SyncItem> items, Action<SyncItem, int, int> onItemDone = null,
            Func<bool> isCancelled = null)
        {
            if (_context == null) throw new InvalidOperationException("Run Compare before Apply.");

            var todo = items
                .Where(i => i.Selected && i.IsActionable && i.Result != SyncResult.Done)
                .OrderBy(i => (int)i.Category)
                .ThenBy(i => i.Order)
                .ToList();

            for (var index = 0; index < todo.Count; index++)
            {
                if (isCancelled != null && isCancelled()) break;

                var item = todo[index];
                try
                {
                    item.Apply(_context);
                    item.Result = SyncResult.Done;
                    item.Message = null;
                }
                catch (DependencyMissingException ex)
                {
                    item.Result = SyncResult.Skipped;
                    item.Message = ex.Message;
                }
                catch (Exception ex)
                {
                    item.Result = SyncResult.Failed;
                    item.Message = ex.Message;
                }

                if (onItemDone != null) onItemDone(item, index + 1, todo.Count);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static SyncItem Line(SyncCategory category, string name, SyncStatus status, string action)
        {
            return new SyncItem { Category = category, Name = name, Status = status, Action = action };
        }

        private static int Depth(OrgSnapshot snapshot, Entity businessUnit)
        {
            var depth = 0;
            var current = businessUnit;
            while (depth < 100)
            {
                var parent = OrgSnapshot.RefId(current, "parentbusinessunitid");
                if (parent == Guid.Empty || !snapshot.BuById.TryGetValue(parent, out current)) break;
                depth++;
            }
            return depth;
        }

        private static bool IsSystemAccount(Entity user)
        {
            if (OrgSnapshot.UserKey(user) == null) return true;

            var name = user.GetAttributeValue<string>("fullname");
            if (string.Equals(name, "SYSTEM", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "INTEGRATION", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 3 = support user, 5 = delegated administrator: both are managed by Microsoft.
            var accessMode = OrgSnapshot.OptionValue(user, "accessmode", 0);
            return accessMode == 3 || accessMode == 5;
        }

        private static HashSet<Guid> DefaultQueueIds(OrgSnapshot snapshot)
        {
            var ids = new HashSet<Guid>();
            foreach (var owner in snapshot.Users.Concat(snapshot.Teams))
            {
                var id = OrgSnapshot.RefId(owner, "queueid");
                if (id != Guid.Empty) ids.Add(id);
            }
            return ids;
        }

        private static void CopyIfPresent(Entity from, Entity to, string attribute)
        {
            if (from.Contains(attribute) && from[attribute] != null) to[attribute] = from[attribute];
        }
    }
}
