using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;

namespace SecurityModelSync.Engine
{
    /// <summary>
    /// Source-to-target id maps, kept up to date while lines are applied so that later lines
    /// (a role assignment, a membership) can find records created by earlier ones.
    /// </summary>
    internal class ApplyContext
    {
        public readonly IOrganizationService Target;
        public readonly OrgSnapshot Source;

        public readonly Dictionary<Guid, Guid> Bu = new Dictionary<Guid, Guid>();
        public readonly Dictionary<Guid, Guid> User = new Dictionary<Guid, Guid>();
        public readonly Dictionary<Guid, Guid> Team = new Dictionary<Guid, Guid>();
        public readonly Dictionary<Guid, Guid> Queue = new Dictionary<Guid, Guid>();

        /// <summary>Target user id to its current target business unit.</summary>
        public readonly Dictionary<Guid, Guid> TargetUserBu = new Dictionary<Guid, Guid>();

        /// <summary>Target team id to its target business unit.</summary>
        public readonly Dictionary<Guid, Guid> TargetTeamBu = new Dictionary<Guid, Guid>();

        public Guid CurrentUserId;

        private Dictionary<string, Guid> _roles = new Dictionary<string, Guid>();
        private Dictionary<Guid, Guid> _defaultTeamByBu = new Dictionary<Guid, Guid>();
        private bool _rolesStale;
        private bool _defaultTeamsStale;

        public ApplyContext(IOrganizationService target, OrgSnapshot source, OrgSnapshot targetSnapshot)
        {
            Target = target;
            Source = source;
            IndexRoles(targetSnapshot.Roles);
            IndexDefaultTeams(targetSnapshot.Teams);
        }

        /// <summary>A new business unit gets its own copies of the roles and a default team.</summary>
        public void BusinessUnitCreated()
        {
            _rolesStale = true;
            _defaultTeamsStale = true;
        }

        public Guid RequireBu(Guid sourceBuId)
        {
            Guid id;
            if (Bu.TryGetValue(sourceBuId, out id)) return id;
            throw new DependencyMissingException(
                "Business unit '" + Source.BuName(sourceBuId) + "' is not in the target. Create it first.");
        }

        public Guid RequireUser(Guid sourceUserId)
        {
            Guid id;
            if (User.TryGetValue(sourceUserId, out id)) return id;
            Entity user;
            var label = Source.UserById.TryGetValue(sourceUserId, out user) ? OrgSnapshot.UserLabel(user) : "?";
            throw new DependencyMissingException("User " + label + " is not in the target. Create the user first.");
        }

        public Guid RequireTeam(Guid sourceTeamId)
        {
            Guid id;
            if (TryResolveTeam(sourceTeamId, out id)) return id;
            Entity team;
            var name = Source.TeamById.TryGetValue(sourceTeamId, out team) ? team.GetAttributeValue<string>("name") : "?";
            throw new DependencyMissingException("Team '" + name + "' is not in the target. Create the team first.");
        }

        public bool TryResolveTeam(Guid sourceTeamId, out Guid targetTeamId)
        {
            if (Team.TryGetValue(sourceTeamId, out targetTeamId)) return true;

            // The default team of a business unit is created by the platform with the business unit.
            Entity team;
            if (!Source.TeamById.TryGetValue(sourceTeamId, out team)) return false;
            if (!team.GetAttributeValue<bool>("isdefault")) return false;

            Guid targetBu;
            if (!Bu.TryGetValue(OrgSnapshot.RefId(team, "businessunitid"), out targetBu)) return false;

            if (!_defaultTeamByBu.TryGetValue(targetBu, out targetTeamId))
            {
                if (!_defaultTeamsStale) return false;
                IndexDefaultTeams(OrgSnapshot.RetrieveAll(Target, "team", new[] { "businessunitid", "isdefault" }));
                _defaultTeamsStale = false;
                if (!_defaultTeamByBu.TryGetValue(targetBu, out targetTeamId)) return false;
            }

            Team[sourceTeamId] = targetTeamId;
            TargetTeamBu[targetTeamId] = targetBu;
            return true;
        }

        public Guid RequireQueue(Guid sourceQueueId)
        {
            Guid id;
            if (Queue.TryGetValue(sourceQueueId, out id)) return id;
            Entity queue;
            var name = Source.QueueById.TryGetValue(sourceQueueId, out queue) ? queue.GetAttributeValue<string>("name") : "?";
            throw new DependencyMissingException("Queue '" + name + "' is not in the target. Create the queue first.");
        }

        public bool TryFindRole(Guid targetBuId, string roleName, out Guid roleId)
        {
            return _roles.TryGetValue(RoleKey(targetBuId, roleName), out roleId);
        }

        public Guid RequireRole(Guid targetBuId, string roleName)
        {
            Guid id;
            if (TryFindRole(targetBuId, roleName, out id)) return id;

            if (_rolesStale)
            {
                IndexRoles(OrgSnapshot.RetrieveAll(Target, "role", new[] { "name", "businessunitid" }));
                _rolesStale = false;
                if (TryFindRole(targetBuId, roleName, out id)) return id;
            }

            throw new DependencyMissingException(
                "Security role '" + roleName + "' does not exist in the target business unit. Import it with a solution first.");
        }

        /// <summary>Associates two records and treats "already associated" as success.</summary>
        public void Associate(string entityName, Guid id, string relationship, string relatedEntityName, Guid relatedId)
        {
            try
            {
                Target.Associate(entityName, id, new Relationship(relationship),
                    new EntityReferenceCollection { new EntityReference(relatedEntityName, relatedId) });
            }
            catch (FaultException ex)
            {
                var message = ex.Message ?? string.Empty;
                if (message.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) < 0) throw;
            }
        }

        private void IndexRoles(IEnumerable<Entity> roles)
        {
            _roles = new Dictionary<string, Guid>();
            foreach (var role in roles)
            {
                var key = RoleKey(OrgSnapshot.RefId(role, "businessunitid"), role.GetAttributeValue<string>("name"));
                if (!_roles.ContainsKey(key)) _roles[key] = role.Id;
            }
        }

        private void IndexDefaultTeams(IEnumerable<Entity> teams)
        {
            _defaultTeamByBu = new Dictionary<Guid, Guid>();
            foreach (var team in teams.Where(t => t.GetAttributeValue<bool>("isdefault")))
            {
                _defaultTeamByBu[OrgSnapshot.RefId(team, "businessunitid")] = team.Id;
            }
        }

        private static string RoleKey(Guid targetBuId, string roleName)
        {
            return targetBuId.ToString("N") + "|" + OrgSnapshot.Norm(roleName);
        }
    }
}
