namespace SecurityModelSync
{
    partial class SecurityModelSyncControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.tsbClose = new System.Windows.Forms.ToolStripButton();
            this.sep1 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbTarget = new System.Windows.Forms.ToolStripButton();
            this.ddScope = new System.Windows.Forms.ToolStripDropDownButton();
            this.miBusinessUnits = new System.Windows.Forms.ToolStripMenuItem();
            this.miUsers = new System.Windows.Forms.ToolStripMenuItem();
            this.miTeams = new System.Windows.Forms.ToolStripMenuItem();
            this.miQueues = new System.Windows.Forms.ToolStripMenuItem();
            this.miUserRoles = new System.Windows.Forms.ToolStripMenuItem();
            this.miTeamRoles = new System.Windows.Forms.ToolStripMenuItem();
            this.miTeamMembers = new System.Windows.Forms.ToolStripMenuItem();
            this.miQueueMembers = new System.Windows.Forms.ToolStripMenuItem();
            this.miScopeSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.miIncludeDisabled = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbCompare = new System.Windows.Forms.ToolStripButton();
            this.sep2 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbApply = new System.Windows.Forms.ToolStripButton();
            this.sep3 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbExport = new System.Windows.Forms.ToolStripButton();
            this.filterStrip = new System.Windows.Forms.ToolStrip();
            this.lblShow = new System.Windows.Forms.ToolStripLabel();
            this.cboCategory = new System.Windows.Forms.ToolStripComboBox();
            this.lblFind = new System.Windows.Forms.ToolStripLabel();
            this.txtFilter = new System.Windows.Forms.ToolStripTextBox();
            this.chkHideMatches = new System.Windows.Forms.ToolStripButton();
            this.sep4 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbSelectAll = new System.Windows.Forms.ToolStripButton();
            this.tsbSelectNone = new System.Windows.Forms.ToolStripButton();
            this.lblConnections = new System.Windows.Forms.Label();
            this.grid = new System.Windows.Forms.DataGridView();
            this.colSelected = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colCategory = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAction = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colResult = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMessage = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblSummary = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStrip.SuspendLayout();
            this.filterStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.grid)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip
            // 
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbClose,
            this.sep1,
            this.tsbTarget,
            this.ddScope,
            this.tsbCompare,
            this.sep2,
            this.tsbApply,
            this.sep3,
            this.tsbExport});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(1000, 25);
            this.toolStrip.TabIndex = 0;
            // 
            // tsbClose
            // 
            this.tsbClose.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbClose.Name = "tsbClose";
            this.tsbClose.Size = new System.Drawing.Size(40, 22);
            this.tsbClose.Text = "Close";
            this.tsbClose.Click += new System.EventHandler(this.tsbClose_Click);
            // 
            // sep1
            // 
            this.sep1.Name = "sep1";
            this.sep1.Size = new System.Drawing.Size(6, 25);
            // 
            // tsbTarget
            // 
            this.tsbTarget.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbTarget.Name = "tsbTarget";
            this.tsbTarget.Size = new System.Drawing.Size(100, 22);
            this.tsbTarget.Text = "Connect target...";
            this.tsbTarget.ToolTipText = "Choose the organization to write to. The source is the tool\'s main connection.";
            this.tsbTarget.Click += new System.EventHandler(this.tsbTarget_Click);
            // 
            // ddScope
            // 
            this.ddScope.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.ddScope.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miBusinessUnits,
            this.miUsers,
            this.miTeams,
            this.miQueues,
            this.miUserRoles,
            this.miTeamRoles,
            this.miTeamMembers,
            this.miQueueMembers,
            this.miScopeSeparator,
            this.miIncludeDisabled});
            this.ddScope.Name = "ddScope";
            this.ddScope.Size = new System.Drawing.Size(52, 22);
            this.ddScope.Text = "Scope";
            this.ddScope.ToolTipText = "What to compare";
            // 
            // miBusinessUnits
            // 
            this.miBusinessUnits.Checked = true;
            this.miBusinessUnits.CheckOnClick = true;
            this.miBusinessUnits.CheckState = System.Windows.Forms.CheckState.Checked;
            this.miBusinessUnits.Name = "miBusinessUnits";
            this.miBusinessUnits.Size = new System.Drawing.Size(220, 22);
            this.miBusinessUnits.Text = "Business units";
            // 
            // miUsers
            // 
            this.miUsers.Checked = true;
            this.miUsers.CheckOnClick = true;
            this.miUsers.CheckState = System.Windows.Forms.CheckState.Checked;
            this.miUsers.Name = "miUsers";
            this.miUsers.Size = new System.Drawing.Size(220, 22);
            this.miUsers.Text = "Users";
            // 
            // miTeams
            // 
            this.miTeams.Checked = true;
            this.miTeams.CheckOnClick = true;
            this.miTeams.CheckState = System.Windows.Forms.CheckState.Checked;
            this.miTeams.Name = "miTeams";
            this.miTeams.Size = new System.Drawing.Size(220, 22);
            this.miTeams.Text = "Teams";
            // 
            // miQueues
            // 
            this.miQueues.Checked = true;
            this.miQueues.CheckOnClick = true;
            this.miQueues.CheckState = System.Windows.Forms.CheckState.Checked;
            this.miQueues.Name = "miQueues";
            this.miQueues.Size = new System.Drawing.Size(220, 22);
            this.miQueues.Text = "Queues";
            // 
            // miUserRoles
            // 
            this.miUserRoles.Checked = true;
            this.miUserRoles.CheckOnClick = true;
            this.miUserRoles.CheckState = System.Windows.Forms.CheckState.Checked;
            this.miUserRoles.Name = "miUserRoles";
            this.miUserRoles.Size = new System.Drawing.Size(220, 22);
            this.miUserRoles.Text = "User role assignments";
            // 
            // miTeamRoles
            // 
            this.miTeamRoles.Checked = true;
            this.miTeamRoles.CheckOnClick = true;
            this.miTeamRoles.CheckState = System.Windows.Forms.CheckState.Checked;
            this.miTeamRoles.Name = "miTeamRoles";
            this.miTeamRoles.Size = new System.Drawing.Size(220, 22);
            this.miTeamRoles.Text = "Team role assignments";
            // 
            // miTeamMembers
            // 
            this.miTeamMembers.Checked = true;
            this.miTeamMembers.CheckOnClick = true;
            this.miTeamMembers.CheckState = System.Windows.Forms.CheckState.Checked;
            this.miTeamMembers.Name = "miTeamMembers";
            this.miTeamMembers.Size = new System.Drawing.Size(220, 22);
            this.miTeamMembers.Text = "Team members";
            // 
            // miQueueMembers
            // 
            this.miQueueMembers.Checked = true;
            this.miQueueMembers.CheckOnClick = true;
            this.miQueueMembers.CheckState = System.Windows.Forms.CheckState.Checked;
            this.miQueueMembers.Name = "miQueueMembers";
            this.miQueueMembers.Size = new System.Drawing.Size(220, 22);
            this.miQueueMembers.Text = "Queue members";
            // 
            // miScopeSeparator
            // 
            this.miScopeSeparator.Name = "miScopeSeparator";
            this.miScopeSeparator.Size = new System.Drawing.Size(217, 6);
            // 
            // miIncludeDisabled
            // 
            this.miIncludeDisabled.CheckOnClick = true;
            this.miIncludeDisabled.Name = "miIncludeDisabled";
            this.miIncludeDisabled.Size = new System.Drawing.Size(220, 22);
            this.miIncludeDisabled.Text = "Include disabled users";
            // 
            // tsbCompare
            // 
            this.tsbCompare.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbCompare.Name = "tsbCompare";
            this.tsbCompare.Size = new System.Drawing.Size(60, 22);
            this.tsbCompare.Text = "Compare";
            this.tsbCompare.ToolTipText = "Read both organizations and list the differences. Nothing is written.";
            this.tsbCompare.Click += new System.EventHandler(this.tsbCompare_Click);
            // 
            // sep2
            // 
            this.sep2.Name = "sep2";
            this.sep2.Size = new System.Drawing.Size(6, 25);
            // 
            // tsbApply
            // 
            this.tsbApply.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbApply.Name = "tsbApply";
            this.tsbApply.Size = new System.Drawing.Size(90, 22);
            this.tsbApply.Text = "Apply selected";
            this.tsbApply.ToolTipText = "Write the ticked lines to the target organization";
            this.tsbApply.Click += new System.EventHandler(this.tsbApply_Click);
            // 
            // sep3
            // 
            this.sep3.Name = "sep3";
            this.sep3.Size = new System.Drawing.Size(6, 25);
            // 
            // tsbExport
            // 
            this.tsbExport.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbExport.Name = "tsbExport";
            this.tsbExport.Size = new System.Drawing.Size(70, 22);
            this.tsbExport.Text = "Export CSV";
            this.tsbExport.ToolTipText = "Save every line, with its result, to a CSV file";
            this.tsbExport.Click += new System.EventHandler(this.tsbExport_Click);
            // 
            // filterStrip
            // 
            this.filterStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.filterStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblShow,
            this.cboCategory,
            this.lblFind,
            this.txtFilter,
            this.chkHideMatches,
            this.sep4,
            this.tsbSelectAll,
            this.tsbSelectNone});
            this.filterStrip.Location = new System.Drawing.Point(0, 51);
            this.filterStrip.Name = "filterStrip";
            this.filterStrip.Size = new System.Drawing.Size(1000, 25);
            this.filterStrip.TabIndex = 2;
            // 
            // lblShow
            // 
            this.lblShow.Name = "lblShow";
            this.lblShow.Size = new System.Drawing.Size(36, 22);
            this.lblShow.Text = "Show";
            // 
            // cboCategory
            // 
            this.cboCategory.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboCategory.Name = "cboCategory";
            this.cboCategory.Size = new System.Drawing.Size(140, 25);
            this.cboCategory.SelectedIndexChanged += new System.EventHandler(this.Filter_Changed);
            // 
            // lblFind
            // 
            this.lblFind.Name = "lblFind";
            this.lblFind.Size = new System.Drawing.Size(90, 22);
            this.lblFind.Text = "  Name contains";
            // 
            // txtFilter
            // 
            this.txtFilter.Name = "txtFilter";
            this.txtFilter.Size = new System.Drawing.Size(180, 25);
            this.txtFilter.TextChanged += new System.EventHandler(this.Filter_Changed);
            // 
            // chkHideMatches
            // 
            this.chkHideMatches.Checked = true;
            this.chkHideMatches.CheckOnClick = true;
            this.chkHideMatches.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkHideMatches.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.chkHideMatches.Name = "chkHideMatches";
            this.chkHideMatches.Size = new System.Drawing.Size(85, 22);
            this.chkHideMatches.Text = "Hide matches";
            this.chkHideMatches.CheckedChanged += new System.EventHandler(this.Filter_Changed);
            // 
            // sep4
            // 
            this.sep4.Name = "sep4";
            this.sep4.Size = new System.Drawing.Size(6, 25);
            // 
            // tsbSelectAll
            // 
            this.tsbSelectAll.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbSelectAll.Name = "tsbSelectAll";
            this.tsbSelectAll.Size = new System.Drawing.Size(60, 22);
            this.tsbSelectAll.Text = "Tick shown";
            this.tsbSelectAll.Click += new System.EventHandler(this.tsbSelectAll_Click);
            // 
            // tsbSelectNone
            // 
            this.tsbSelectNone.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbSelectNone.Name = "tsbSelectNone";
            this.tsbSelectNone.Size = new System.Drawing.Size(70, 22);
            this.tsbSelectNone.Text = "Untick shown";
            this.tsbSelectNone.Click += new System.EventHandler(this.tsbSelectNone_Click);
            // 
            // lblConnections
            // 
            this.lblConnections.BackColor = System.Drawing.SystemColors.Info;
            this.lblConnections.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblConnections.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblConnections.Location = new System.Drawing.Point(0, 25);
            this.lblConnections.Name = "lblConnections";
            this.lblConnections.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblConnections.Size = new System.Drawing.Size(1000, 26);
            this.lblConnections.TabIndex = 1;
            this.lblConnections.Text = "Source: -     Target: -";
            this.lblConnections.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // grid
            // 
            this.grid.AllowUserToAddRows = false;
            this.grid.AllowUserToDeleteRows = false;
            this.grid.AllowUserToResizeRows = false;
            this.grid.BackgroundColor = System.Drawing.SystemColors.Window;
            this.grid.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.grid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colSelected,
            this.colCategory,
            this.colName,
            this.colStatus,
            this.colAction,
            this.colResult,
            this.colMessage});
            this.grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grid.Location = new System.Drawing.Point(0, 76);
            this.grid.Name = "grid";
            this.grid.RowHeadersVisible = false;
            this.grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.grid.Size = new System.Drawing.Size(1000, 502);
            this.grid.TabIndex = 3;
            this.grid.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.grid_CellBeginEdit);
            this.grid.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.grid_CellFormatting);
            this.grid.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.grid_CellValueChanged);
            this.grid.CurrentCellDirtyStateChanged += new System.EventHandler(this.grid_CurrentCellDirtyStateChanged);
            // 
            // colSelected
            // 
            this.colSelected.DataPropertyName = "Selected";
            this.colSelected.HeaderText = "";
            this.colSelected.Name = "colSelected";
            this.colSelected.Width = 30;
            // 
            // colCategory
            // 
            this.colCategory.DataPropertyName = "Category";
            this.colCategory.HeaderText = "Type";
            this.colCategory.Name = "colCategory";
            this.colCategory.ReadOnly = true;
            this.colCategory.Width = 100;
            // 
            // colName
            // 
            this.colName.DataPropertyName = "Name";
            this.colName.HeaderText = "Name";
            this.colName.Name = "colName";
            this.colName.ReadOnly = true;
            this.colName.Width = 300;
            // 
            // colStatus
            // 
            this.colStatus.DataPropertyName = "Status";
            this.colStatus.HeaderText = "Status";
            this.colStatus.Name = "colStatus";
            this.colStatus.ReadOnly = true;
            this.colStatus.Width = 110;
            // 
            // colAction
            // 
            this.colAction.DataPropertyName = "Action";
            this.colAction.HeaderText = "What Apply will do";
            this.colAction.Name = "colAction";
            this.colAction.ReadOnly = true;
            this.colAction.Width = 320;
            // 
            // colResult
            // 
            this.colResult.DataPropertyName = "Result";
            this.colResult.HeaderText = "Result";
            this.colResult.Name = "colResult";
            this.colResult.ReadOnly = true;
            this.colResult.Width = 70;
            // 
            // colMessage
            // 
            this.colMessage.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colMessage.DataPropertyName = "Message";
            this.colMessage.HeaderText = "Message";
            this.colMessage.MinimumWidth = 150;
            this.colMessage.Name = "colMessage";
            this.colMessage.ReadOnly = true;
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblSummary});
            this.statusStrip.Location = new System.Drawing.Point(0, 578);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1000, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 4;
            // 
            // lblSummary
            // 
            this.lblSummary.Name = "lblSummary";
            this.lblSummary.Size = new System.Drawing.Size(16, 17);
            this.lblSummary.Text = "-";
            // 
            // SecurityModelSyncControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.grid);
            this.Controls.Add(this.filterStrip);
            this.Controls.Add(this.lblConnections);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.statusStrip);
            this.Name = "SecurityModelSyncControl";
            this.Size = new System.Drawing.Size(1000, 600);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.filterStrip.ResumeLayout(false);
            this.filterStrip.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.grid)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton tsbClose;
        private System.Windows.Forms.ToolStripSeparator sep1;
        private System.Windows.Forms.ToolStripButton tsbTarget;
        private System.Windows.Forms.ToolStripDropDownButton ddScope;
        private System.Windows.Forms.ToolStripMenuItem miBusinessUnits;
        private System.Windows.Forms.ToolStripMenuItem miUsers;
        private System.Windows.Forms.ToolStripMenuItem miTeams;
        private System.Windows.Forms.ToolStripMenuItem miQueues;
        private System.Windows.Forms.ToolStripMenuItem miUserRoles;
        private System.Windows.Forms.ToolStripMenuItem miTeamRoles;
        private System.Windows.Forms.ToolStripMenuItem miTeamMembers;
        private System.Windows.Forms.ToolStripMenuItem miQueueMembers;
        private System.Windows.Forms.ToolStripSeparator miScopeSeparator;
        private System.Windows.Forms.ToolStripMenuItem miIncludeDisabled;
        private System.Windows.Forms.ToolStripButton tsbCompare;
        private System.Windows.Forms.ToolStripSeparator sep2;
        private System.Windows.Forms.ToolStripButton tsbApply;
        private System.Windows.Forms.ToolStripSeparator sep3;
        private System.Windows.Forms.ToolStripButton tsbExport;
        private System.Windows.Forms.ToolStrip filterStrip;
        private System.Windows.Forms.ToolStripLabel lblShow;
        private System.Windows.Forms.ToolStripComboBox cboCategory;
        private System.Windows.Forms.ToolStripLabel lblFind;
        private System.Windows.Forms.ToolStripTextBox txtFilter;
        private System.Windows.Forms.ToolStripButton chkHideMatches;
        private System.Windows.Forms.ToolStripSeparator sep4;
        private System.Windows.Forms.ToolStripButton tsbSelectAll;
        private System.Windows.Forms.ToolStripButton tsbSelectNone;
        private System.Windows.Forms.Label lblConnections;
        private System.Windows.Forms.DataGridView grid;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colSelected;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCategory;
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAction;
        private System.Windows.Forms.DataGridViewTextBoxColumn colResult;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMessage;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblSummary;
    }
}
