using System;

namespace SecurityModelSync.Engine
{
    /// <summary>Kind of security object a diff line is about. The enum order is the apply order.</summary>
    public enum SyncCategory
    {
        BusinessUnit = 0,
        User = 1,
        Team = 2,
        Queue = 3,
        UserRole = 4,
        TeamRole = 5,
        TeamMember = 6,
        QueueMember = 7
    }

    public enum SyncStatus
    {
        /// <summary>Same in source and target. Nothing to do.</summary>
        Match,
        /// <summary>Exists in source, not in target. Can be created.</summary>
        Missing,
        /// <summary>Exists in both but differs. Can be updated.</summary>
        Different,
        /// <summary>Needs attention but the tool will not change it.</summary>
        Warning
    }

    public enum SyncResult
    {
        None,
        Done,
        Failed,
        Skipped
    }

    /// <summary>What to compare. Everything is on by default.</summary>
    public class SyncOptions
    {
        public bool BusinessUnits { get; set; } = true;
        public bool Users { get; set; } = true;
        public bool Teams { get; set; } = true;
        public bool Queues { get; set; } = true;
        public bool UserRoles { get; set; } = true;
        public bool TeamRoles { get; set; } = true;
        public bool TeamMembers { get; set; } = true;
        public bool QueueMembers { get; set; } = true;

        /// <summary>Disabled source users are ignored unless this is set.</summary>
        public bool IncludeDisabledUsers { get; set; }
    }

    /// <summary>One line of the comparison.</summary>
    public class SyncItem
    {
        public SyncCategory Category { get; set; }
        public string Name { get; set; }
        public SyncStatus Status { get; set; }

        /// <summary>What Apply will do, or why nothing can be done.</summary>
        public string Action { get; set; }

        public bool Selected { get; set; }
        public SyncResult Result { get; set; }
        public string Message { get; set; }

        /// <summary>Work to run against the target. Null for Match and Warning lines.</summary>
        internal Action<ApplyContext> Apply { get; set; }

        /// <summary>Position in the comparison; keeps parents ahead of children when applying.</summary>
        internal int Order { get; set; }

        public bool IsActionable
        {
            get { return Apply != null; }
        }
    }

    /// <summary>Raised when a line cannot run because something it depends on is not in the target.</summary>
    public class DependencyMissingException : Exception
    {
        public DependencyMissingException(string message) : base(message)
        {
        }
    }
}
