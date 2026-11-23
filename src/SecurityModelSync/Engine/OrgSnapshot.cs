using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace SecurityModelSync.Engine
{
    /// <summary>Everything the comparison needs from one organization, loaded once.</summary>
    public class OrgSnapshot
    {
        public const string RootKey = "\u0001root";

        public List<Entity> BusinessUnits = new List<Entity>();
        public List<Entity> Roles = new List<Entity>();
        public List<Entity> Users = new List<Entity>();
        public List<Entity> Teams = new List<Entity>();
        public List<Entity> Queues = new List<Entity>();

        public HashSet<Tuple<Guid, Guid>> UserRoles = new HashSet<Tuple<Guid, Guid>>();
        public HashSet<Tuple<Guid, Guid>> TeamRoles = new HashSet<Tuple<Guid, Guid>>();
        public HashSet<Tuple<Guid, Guid>> TeamMembers = new HashSet<Tuple<Guid, Guid>>();
        public HashSet<Tuple<Guid, Guid>> QueueMembers = new HashSet<Tuple<Guid, Guid>>();

        public Dictionary<Guid, Entity> BuById = new Dictionary<Guid, Entity>();
        public Dictionary<Guid, Entity> RoleById = new Dictionary<Guid, Entity>();
        public Dictionary<Guid, Entity> UserById = new Dictionary<Guid, Entity>();
        public Dictionary<Guid, Entity> TeamById = new Dictionary<Guid, Entity>();
        public Dictionary<Guid, Entity> QueueById = new Dictionary<Guid, Entity>();

        public Guid RootBusinessUnitId;

        public static OrgSnapshot Load(IOrganizationService service, Action<string> progress)
        {
            var s = new OrgSnapshot();

            progress("business units");
            s.BusinessUnits = RetrieveAll(service, "businessunit",
                new[] { "name", "parentbusinessunitid", "isdisabled" });

            progress("security roles");
            s.Roles = RetrieveAll(service, "role", new[] { "name", "businessunitid" });

            progress("users");
            s.Users = RetrieveAll(service, "systemuser",
                new[]
                {
                    "domainname", "fullname", "firstname", "lastname", "internalemailaddress",
                    "businessunitid", "isdisabled", "accessmode", "queueid"
                },
                new[] { "applicationid" });

            progress("teams");
            s.Teams = RetrieveAll(service, "team",
                new[] { "name", "businessunitid", "teamtype", "isdefault", "administratorid", "description", "queueid" },
                new[] { "azureactivedirectoryobjectid", "membershiptype" });

            progress("queues");
            s.Queues = RetrieveAll(service, "queue",
                new[] { "name", "description", "emailaddress", "queueviewtype", "ownerid", "statecode" });

            progress("role assignments");
            s.UserRoles = Pairs(RetrieveAll(service, "systemuserroles", new[] { "systemuserid", "roleid" }),
                "systemuserid", "roleid");
            s.TeamRoles = Pairs(RetrieveAll(service, "teamroles", new[] { "teamid", "roleid" }),
                "teamid", "roleid");

            progress("memberships");
            s.TeamMembers = Pairs(RetrieveAll(service, "teammembership", new[] { "teamid", "systemuserid" }),
                "teamid", "systemuserid");
            s.QueueMembers = Pairs(RetrieveAll(service, "queuemembership", new[] { "queueid", "systemuserid" }),
                "queueid", "systemuserid");

            s.Index();
            return s;
        }

        public void Index()
        {
            BuById = BusinessUnits.ToDictionary(e => e.Id);
            RoleById = Roles.ToDictionary(e => e.Id);
            UserById = Users.ToDictionary(e => e.Id);
            TeamById = Teams.ToDictionary(e => e.Id);
            QueueById = Queues.ToDictionary(e => e.Id);

            var root = BusinessUnits.FirstOrDefault(b => RefId(b, "parentbusinessunitid") == Guid.Empty);
            RootBusinessUnitId = root == null ? Guid.Empty : root.Id;
        }

        // ---- keys used to match records between organizations ----

        public string BuKey(Guid businessUnitId)
        {
            if (businessUnitId == Guid.Empty) return null;
            if (businessUnitId == RootBusinessUnitId) return RootKey;
            Entity bu;
            return BuById.TryGetValue(businessUnitId, out bu) ? Norm(bu.GetAttributeValue<string>("name")) : null;
        }

        public string BuName(Guid businessUnitId)
        {
            Entity bu;
            return BuById.TryGetValue(businessUnitId, out bu) ? bu.GetAttributeValue<string>("name") : "?";
        }

        public static string UserKey(Entity user)
        {
            return Norm(user.GetAttributeValue<string>("domainname"));
        }

        public static string UserLabel(Entity user)
        {
            var name = user.GetAttributeValue<string>("fullname");
            var login = user.GetAttributeValue<string>("domainname");
            return string.IsNullOrWhiteSpace(name) ? login : name + " (" + login + ")";
        }

        // ---- helpers ----

        public static string Norm(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
        }

        public static Guid RefId(Entity e, string attribute)
        {
            if (!e.Contains(attribute) || e[attribute] == null) return Guid.Empty;
            var value = e[attribute];
            if (value is EntityReference) return ((EntityReference)value).Id;
            if (value is Guid) return (Guid)value;
            return Guid.Empty;
        }

        public static int OptionValue(Entity e, string attribute, int fallback)
        {
            var value = e.GetAttributeValue<OptionSetValue>(attribute);
            return value == null ? fallback : value.Value;
        }

        private static HashSet<Tuple<Guid, Guid>> Pairs(IEnumerable<Entity> rows, string first, string second)
        {
            var set = new HashSet<Tuple<Guid, Guid>>();
            foreach (var row in rows)
            {
                var a = RefId(row, first);
                var b = RefId(row, second);
                if (a != Guid.Empty && b != Guid.Empty) set.Add(Tuple.Create(a, b));
            }
            return set;
        }

        /// <summary>
        /// Reads every row of an entity, page by page. Optional columns are dropped and the query
        /// retried when the organization does not have them (older on-premises versions).
        /// </summary>
        public static List<Entity> RetrieveAll(IOrganizationService service, string entityName,
            string[] columns, string[] optionalColumns = null)
        {
            if (optionalColumns != null && optionalColumns.Length > 0)
            {
                try
                {
                    return RetrieveAllCore(service, entityName, columns.Concat(optionalColumns).ToArray());
                }
                catch (System.ServiceModel.FaultException)
                {
                    // fall through to the required columns only
                }
            }
            return RetrieveAllCore(service, entityName, columns);
        }

        private static List<Entity> RetrieveAllCore(IOrganizationService service, string entityName, string[] columns)
        {
            var result = new List<Entity>();
            var query = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet(columns),
                PageInfo = new PagingInfo { PageNumber = 1, Count = 5000 }
            };

            while (true)
            {
                var page = service.RetrieveMultiple(query);
                result.AddRange(page.Entities);
                if (!page.MoreRecords) break;
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }
            return result;
        }
    }
}
