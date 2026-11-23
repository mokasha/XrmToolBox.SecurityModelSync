using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace SecurityModelSync.Tests
{
    /// <summary>
    /// In-memory stand-in for an organization. It imitates the platform behaviour the engine relies on:
    /// a new business unit gets a default team and its own copies of the roles, and moving a user to
    /// another business unit removes the user's roles.
    /// </summary>
    public class FakeOrg : IOrganizationService
    {
        public readonly Dictionary<string, List<Entity>> Tables = new Dictionary<string, List<Entity>>();
        public Guid CallerId;
        public Guid RootBu;
        public int Writes;

        public FakeOrg(string rootName, params string[] roleNames)
        {
            RootBu = Insert("businessunit", "name", rootName);
            foreach (var role in roleNames)
            {
                Insert("role", "name", role, "businessunitid", Ref("businessunit", RootBu));
            }
            AddDefaultTeam(RootBu, rootName);
            CallerId = AddUser("corp\\admin", "Admin", "User", RootBu);
            AddUser("", "SYSTEM", "", RootBu);
        }

        // ---------------- seeding helpers ----------------

        public Guid AddBu(string name, Guid parent)
        {
            var id = Insert("businessunit", "name", name, "parentbusinessunitid", Ref("businessunit", parent));
            OnBusinessUnitCreated(id, name);
            return id;
        }

        public Guid AddUser(string login, string first, string last, Guid bu, bool disabled = false)
        {
            var id = Insert("systemuser", "domainname", login, "firstname", first, "lastname", last,
                "fullname", (first + " " + last).Trim(), "businessunitid", Ref("businessunit", bu),
                "isdisabled", disabled, "accessmode", new OptionSetValue(0));
            var queue = Insert("queue", "name", "<" + login + ">", "queueviewtype", new OptionSetValue(1),
                "statecode", new OptionSetValue(0), "ownerid", Ref("systemuser", id));
            Row("systemuser", id)["queueid"] = Ref("queue", queue);
            return id;
        }

        public Guid AddTeam(string name, Guid bu)
        {
            return Insert("team", "name", name, "businessunitid", Ref("businessunit", bu), "isdefault", false,
                "teamtype", new OptionSetValue(0), "administratorid", Ref("systemuser", CallerId));
        }

        public Guid AddQueue(string name)
        {
            return Insert("queue", "name", name, "queueviewtype", new OptionSetValue(1),
                "statecode", new OptionSetValue(0), "ownerid", Ref("systemuser", CallerId));
        }

        public void GiveUserRole(Guid user, string roleName)
        {
            Link("systemuserroles", "systemuserid", user, "roleid", RoleId(UserBu(user), roleName));
        }

        public void GiveTeamRole(Guid team, string roleName)
        {
            var bu = ((EntityReference)Row("team", team)["businessunitid"]).Id;
            Link("teamroles", "teamid", team, "roleid", RoleId(bu, roleName));
        }

        public void AddTeamMember(Guid team, Guid user) { Link("teammembership", "teamid", team, "systemuserid", user); }
        public void AddQueueMember(Guid queue, Guid user) { Link("queuemembership", "queueid", queue, "systemuserid", user); }

        // ---------------- lookups used by assertions ----------------

        public List<Entity> Rows(string table)
        {
            List<Entity> rows;
            if (!Tables.TryGetValue(table, out rows)) Tables[table] = rows = new List<Entity>();
            return rows;
        }

        public Entity Row(string table, Guid id) { return Rows(table).Single(r => r.Id == id); }

        public Entity ByName(string table, string attribute, string value)
        {
            return Rows(table).SingleOrDefault(r => string.Equals(r.GetAttributeValue<string>(attribute), value,
                StringComparison.OrdinalIgnoreCase));
        }

        public Guid UserBu(Guid user) { return ((EntityReference)Row("systemuser", user)["businessunitid"]).Id; }

        public Guid RoleId(Guid bu, string name)
        {
            return Rows("role").Single(r => r.GetAttributeValue<string>("name") == name &&
                                            ((EntityReference)r["businessunitid"]).Id == bu).Id;
        }

        public List<string> RolesOfUser(Guid user)
        {
            return Rows("systemuserroles").Where(r => (Guid)r["systemuserid"] == user)
                .Select(r => Row("role", (Guid)r["roleid"]).GetAttributeValue<string>("name")).OrderBy(n => n).ToList();
        }

        public bool Linked(string table, string a, Guid aId, string b, Guid bId)
        {
            return Rows(table).Any(r => (Guid)r[a] == aId && (Guid)r[b] == bId);
        }

        // ---------------- IOrganizationService ----------------

        public Guid Create(Entity entity)
        {
            Writes++;
            if (entity.LogicalName == "systemuser")
            {
                var login = entity.GetAttributeValue<string>("domainname");
                if (ByName("systemuser", "domainname", login) != null) throw Fault("A user with this domain logon already exists");
                return AddUser(login, entity.GetAttributeValue<string>("firstname"), entity.GetAttributeValue<string>("lastname"),
                    ((EntityReference)entity["businessunitid"]).Id);
            }

            var copy = new Entity(entity.LogicalName) { Id = Guid.NewGuid() };
            foreach (var pair in entity.Attributes) copy[pair.Key] = pair.Value;
            if (entity.LogicalName == "team" && !copy.Contains("isdefault")) copy["isdefault"] = false;
            if (entity.LogicalName == "queue") copy["statecode"] = new OptionSetValue(0);
            Rows(entity.LogicalName).Add(copy);

            if (entity.LogicalName == "businessunit") OnBusinessUnitCreated(copy.Id, copy.GetAttributeValue<string>("name"));
            return copy.Id;
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            var q = (QueryExpression)query;
            var result = new EntityCollection { MoreRecords = false };
            foreach (var row in Rows(q.EntityName))
            {
                var copy = new Entity(row.LogicalName) { Id = row.Id };
                foreach (var pair in row.Attributes) copy[pair.Key] = pair.Value;
                result.Entities.Add(copy);
            }
            return result;
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection related)
        {
            Writes++;
            foreach (var other in related)
            {
                switch (relationship.SchemaName)
                {
                    case "systemuserroles_association":
                        RequireSameBu(UserBu(entityId), other.Id);
                        Link("systemuserroles", "systemuserid", entityId, "roleid", other.Id);
                        break;
                    case "teamroles_association":
                        RequireSameBu(((EntityReference)Row("team", entityId)["businessunitid"]).Id, other.Id);
                        Link("teamroles", "teamid", entityId, "roleid", other.Id);
                        break;
                    case "queuemembership_association":
                        Link("queuemembership", "queueid", entityId, "systemuserid", other.Id);
                        break;
                    default:
                        throw Fault("Unknown relationship " + relationship.SchemaName);
                }
            }
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            if (request is WhoAmIRequest)
            {
                var response = new WhoAmIResponse();
                response.Results["UserId"] = CallerId;
                return response;
            }

            Writes++;
            var addMembers = request as AddMembersTeamRequest;
            if (addMembers != null)
            {
                foreach (var member in addMembers.MemberIds) Link("teammembership", "teamid", addMembers.TeamId, "systemuserid", member);
                return new AddMembersTeamResponse();
            }

            var moveUser = request as SetBusinessSystemUserRequest;
            if (moveUser != null)
            {
                Row("systemuser", moveUser.UserId)["businessunitid"] = Ref("businessunit", moveUser.BusinessId);
                Rows("systemuserroles").RemoveAll(r => (Guid)r["systemuserid"] == moveUser.UserId);
                return new SetBusinessSystemUserResponse();
            }

            var moveBu = request as SetParentBusinessUnitRequest;
            if (moveBu != null)
            {
                Row("businessunit", moveBu.BusinessUnitId)["parentbusinessunitid"] = Ref("businessunit", moveBu.ParentId);
                return new SetParentBusinessUnitResponse();
            }

            throw Fault("Unsupported request " + request.GetType().Name);
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) { return Row(entityName, id); }
        public void Update(Entity entity) { throw new NotSupportedException(); }
        public void Delete(string entityName, Guid id) { throw new NotSupportedException("The engine must never delete."); }
        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection related)
        {
            throw new NotSupportedException("The engine must never remove an association.");
        }

        // ---------------- internals ----------------

        private void OnBusinessUnitCreated(Guid bu, string name)
        {
            AddDefaultTeam(bu, name);
            foreach (var role in Rows("role").Where(r => ((EntityReference)r["businessunitid"]).Id == RootBu).ToList())
            {
                Insert("role", "name", role.GetAttributeValue<string>("name"), "businessunitid", Ref("businessunit", bu));
            }
        }

        private void AddDefaultTeam(Guid bu, string name)
        {
            Insert("team", "name", name, "businessunitid", Ref("businessunit", bu), "isdefault", true,
                "teamtype", new OptionSetValue(0));
        }

        private void RequireSameBu(Guid principalBu, Guid roleId)
        {
            if (((EntityReference)Row("role", roleId)["businessunitid"]).Id != principalBu)
            {
                throw Fault("The role is not in the same business unit as the principal");
            }
        }

        private void Link(string table, string a, Guid aId, string b, Guid bId)
        {
            if (Linked(table, a, aId, b, bId)) throw Fault("Cannot insert duplicate key");
            var row = new Entity(table) { Id = Guid.NewGuid() };
            row[a] = aId;
            row[b] = bId;
            Rows(table).Add(row);
        }

        private Guid Insert(string table, params object[] pairs)
        {
            var row = new Entity(table) { Id = Guid.NewGuid() };
            for (var i = 0; i < pairs.Length; i += 2) row[(string)pairs[i]] = pairs[i + 1];
            Rows(table).Add(row);
            return row.Id;
        }

        private static EntityReference Ref(string name, Guid id) { return new EntityReference(name, id); }

        private static FaultException Fault(string message) { return new FaultException(message); }
    }
}
