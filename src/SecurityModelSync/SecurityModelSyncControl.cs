using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using SecurityModelSync.Engine;
using XrmToolBox.Extensibility;

namespace SecurityModelSync
{
    public partial class SecurityModelSyncControl : PluginControlBase
    {
        private const string TargetAction = "TargetOrganization";

        private IOrganizationService _targetService;
        private ConnectionDetail _targetDetail;
        private SecuritySyncEngine _engine;
        private List<SyncItem> _items = new List<SyncItem>();
        private bool _compareAfterTargetConnects;

        public SecurityModelSyncControl()
        {
            InitializeComponent();

            cboCategory.Items.Add("All types");
            foreach (SyncCategory category in Enum.GetValues(typeof(SyncCategory)))
            {
                cboCategory.Items.Add(CategoryLabel(category));
            }
            cboCategory.SelectedIndex = 0;

            grid.AutoGenerateColumns = false;
            UpdateHeader();
            UpdateButtons();
        }

        // ------------------------------------------------------------------ connections

        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail,
            string actionName, object parameter)
        {
            if (actionName == TargetAction)
            {
                _targetService = newService;
                _targetDetail = detail;
            }
            else
            {
                base.UpdateConnection(newService, detail, actionName, parameter);
            }

            ClearResults();
            UpdateHeader();

            if (actionName == TargetAction && _compareAfterTargetConnects)
            {
                _compareAfterTargetConnects = false;
                ExecuteMethod(Compare);
            }
        }

        private void tsbTarget_Click(object sender, EventArgs e)
        {
            _compareAfterTargetConnects = false;
            RequestTarget();
        }

        private void RequestTarget()
        {
            RaiseRequestConnectionEvent(new RequestConnectionEventArgs { ActionName = TargetAction, Control = this });
        }

        private void UpdateHeader()
        {
            var source = ConnectionDetail == null ? "(not connected)" : ConnectionDetail.ConnectionName;
            var target = _targetDetail == null ? "(not connected)" : _targetDetail.ConnectionName;
            lblConnections.Text = "Source: " + source + "     →     Target: " + target;
        }

        // ------------------------------------------------------------------ compare

        private void tsbCompare_Click(object sender, EventArgs e)
        {
            if (_targetService == null)
            {
                _compareAfterTargetConnects = true;
                RequestTarget();
                return;
            }
            ExecuteMethod(Compare);
        }

        private void Compare()
        {
            if (_targetService == null) return;

            if (ConnectionDetail != null && _targetDetail != null &&
                string.Equals(ConnectionDetail.WebApplicationUrl, _targetDetail.WebApplicationUrl, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Source and target are the same organization. Connect the target to a different one.",
                    "Security Model Sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var options = ReadOptions();
            var source = Service;
            var target = _targetService;

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Reading both organizations...",
                Work = (worker, args) =>
                {
                    var engine = new SecuritySyncEngine(source, target, options, m => worker.ReportProgress(0, m));
                    var items = engine.Compare();
                    args.Result = new object[] { engine, items };
                },
                ProgressChanged = args => SetWorkingMessage(Convert.ToString(args.UserState)),
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        LogError("Compare failed: {0}", args.Error);
                        MessageBox.Show(this, args.Error.Message, "Compare failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var result = (object[])args.Result;
                    _engine = (SecuritySyncEngine)result[0];
                    _items = (List<SyncItem>)result[1];
                    RefreshGrid();
                    UpdateButtons();
                }
            });
        }

        private SyncOptions ReadOptions()
        {
            return new SyncOptions
            {
                BusinessUnits = miBusinessUnits.Checked,
                Users = miUsers.Checked,
                Teams = miTeams.Checked,
                Queues = miQueues.Checked,
                UserRoles = miUserRoles.Checked,
                TeamRoles = miTeamRoles.Checked,
                TeamMembers = miTeamMembers.Checked,
                QueueMembers = miQueueMembers.Checked,
                IncludeDisabledUsers = miIncludeDisabled.Checked
            };
        }

        // ------------------------------------------------------------------ apply

        private void tsbApply_Click(object sender, EventArgs e)
        {
            if (_engine == null) return;
            grid.EndEdit();

            var count = _items.Count(i => i.Selected && i.IsActionable && i.Result != SyncResult.Done);
            if (count == 0)
            {
                MessageBox.Show(this, "Nothing is selected to apply.", "Security Model Sync",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var targetName = _targetDetail == null ? "the target" : _targetDetail.ConnectionName;
            var answer = MessageBox.Show(this,
                count + " change(s) will be written to:\r\n\r\n    " + targetName +
                "\r\n\r\nRecords are created or updated only. Nothing is deleted, disabled or removed.\r\n\r\nContinue?",
                "Apply to target", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;

            var engine = _engine;
            var items = _items;

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Applying changes...",
                IsCancelable = true,
                Work = (worker, args) =>
                {
                    engine.Apply(items,
                        (item, index, total) => worker.ReportProgress(index * 100 / total,
                            "Applying " + index + " of " + total + "\r\n" + item.Name),
                        () => worker.CancellationPending);
                },
                ProgressChanged = args => SetWorkingMessage(Convert.ToString(args.UserState)),
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        LogError("Apply failed: {0}", args.Error);
                        MessageBox.Show(this, args.Error.Message, "Apply failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    chkHideMatches.Checked = false;
                    RefreshGrid();

                    var done = items.Count(i => i.Result == SyncResult.Done);
                    var failed = items.Count(i => i.Result == SyncResult.Failed);
                    var skipped = items.Count(i => i.Result == SyncResult.Skipped);
                    LogInfo("Apply finished: {0} done, {1} failed, {2} skipped", done, failed, skipped);

                    MessageBox.Show(this,
                        done + " applied, " + failed + " failed, " + skipped + " skipped.\r\n\r\n" +
                        (failed + skipped > 0
                            ? "See the Result and Message columns. Fix the cause, then Compare again: only what is still outstanding is listed."
                            : "Compare again to confirm the organizations now match."),
                        "Apply finished", MessageBoxButtons.OK,
                        failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                }
            });
        }

        // ------------------------------------------------------------------ grid

        private void ClearResults()
        {
            _engine = null;
            _items = new List<SyncItem>();
            RefreshGrid();
            UpdateButtons();
        }

        private IEnumerable<SyncItem> VisibleItems()
        {
            IEnumerable<SyncItem> view = _items;

            if (chkHideMatches.Checked)
            {
                view = view.Where(i => i.Status != SyncStatus.Match);
            }
            if (cboCategory.SelectedIndex > 0)
            {
                var category = (SyncCategory)(cboCategory.SelectedIndex - 1);
                view = view.Where(i => i.Category == category);
            }
            var text = txtFilter.Text.Trim();
            if (text.Length > 0)
            {
                view = view.Where(i => (i.Name ?? string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            return view;
        }

        private void RefreshGrid()
        {
            grid.DataSource = new BindingList<SyncItem>(VisibleItems().ToList());
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (_items.Count == 0)
            {
                lblSummary.Text = "Connect a target organization, then choose Compare.";
                return;
            }

            lblSummary.Text =
                _items.Count(i => i.Status == SyncStatus.Missing) + " to create   |   " +
                _items.Count(i => i.Status == SyncStatus.Different) + " different   |   " +
                _items.Count(i => i.Status == SyncStatus.Warning) + " need attention   |   " +
                _items.Count(i => i.Status == SyncStatus.Match) + " match   |   " +
                _items.Count(i => i.Selected && i.IsActionable && i.Result != SyncResult.Done) + " selected to apply";
        }

        private void UpdateButtons()
        {
            var hasItems = _items.Count > 0;
            tsbApply.Enabled = hasItems && _engine != null;
            tsbExport.Enabled = hasItems;
            tsbSelectAll.Enabled = hasItems;
            tsbSelectNone.Enabled = hasItems;
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            RefreshGrid();
        }

        private void tsbSelectAll_Click(object sender, EventArgs e)
        {
            SetSelection(true);
        }

        private void tsbSelectNone_Click(object sender, EventArgs e)
        {
            SetSelection(false);
        }

        /// <summary>Ticks or clears the rows currently shown, so a filter can be used to pick a subset.</summary>
        private void SetSelection(bool selected)
        {
            grid.EndEdit();
            foreach (var item in VisibleItems().Where(i => i.IsActionable && i.Result != SyncResult.Done))
            {
                item.Selected = selected;
            }
            grid.Refresh();
            UpdateSummary();
        }

        private void grid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            var item = ItemAt(e.RowIndex);
            if (item == null || !item.IsActionable || item.Result == SyncResult.Done) e.Cancel = true;
        }

        private void grid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            // Commit a checkbox click straight away so the summary stays in step.
            if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) UpdateSummary();
        }

        private void grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var item = ItemAt(e.RowIndex);
            if (item == null) return;

            var column = grid.Columns[e.ColumnIndex];
            if (column == colCategory)
            {
                e.Value = CategoryLabel(item.Category);
                e.FormattingApplied = true;
            }
            else if (column == colStatus)
            {
                e.Value = StatusLabel(item.Status);
                e.FormattingApplied = true;
                e.CellStyle.ForeColor = StatusColor(item.Status);
            }
            else if (column == colResult)
            {
                e.Value = item.Result == SyncResult.None ? string.Empty : item.Result.ToString();
                e.FormattingApplied = true;
                e.CellStyle.ForeColor = item.Result == SyncResult.Failed ? Color.Firebrick
                    : item.Result == SyncResult.Skipped ? Color.DarkOrange
                    : Color.SeaGreen;
            }

            if (item.Status == SyncStatus.Match && column != colResult)
            {
                e.CellStyle.ForeColor = Color.Gray;
            }
        }

        private SyncItem ItemAt(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;
            return grid.Rows[rowIndex].DataBoundItem as SyncItem;
        }

        // ------------------------------------------------------------------ export

        private void tsbExport_Click(object sender, EventArgs e)
        {
            grid.EndEdit();
            using (var dialog = new SaveFileDialog
            {
                Filter = "CSV file (*.csv)|*.csv",
                FileName = "SecurityModelSync_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                var csv = new StringBuilder();
                csv.AppendLine("Type,Name,Status,Action,Selected,Result,Message");
                foreach (var item in _items)
                {
                    csv.AppendLine(string.Join(",", new[]
                    {
                        Csv(CategoryLabel(item.Category)), Csv(item.Name), Csv(StatusLabel(item.Status)), Csv(item.Action),
                        item.Selected ? "Yes" : "No", Csv(item.Result == SyncResult.None ? string.Empty : item.Result.ToString()),
                        Csv(item.Message)
                    }));
                }

                // UTF-8 with a byte order mark so Excel reads Arabic and other non-Latin names correctly.
                File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
            }
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        // ------------------------------------------------------------------ labels

        private void tsbClose_Click(object sender, EventArgs e)
        {
            CloseTool();
        }

        private static string CategoryLabel(SyncCategory category)
        {
            switch (category)
            {
                case SyncCategory.BusinessUnit: return "Business unit";
                case SyncCategory.User: return "User";
                case SyncCategory.Team: return "Team";
                case SyncCategory.Queue: return "Queue";
                case SyncCategory.UserRole: return "User role";
                case SyncCategory.TeamRole: return "Team role";
                case SyncCategory.TeamMember: return "Team member";
                case SyncCategory.QueueMember: return "Queue member";
                default: return category.ToString();
            }
        }

        private static string StatusLabel(SyncStatus status)
        {
            switch (status)
            {
                case SyncStatus.Missing: return "Missing in target";
                case SyncStatus.Different: return "Different";
                case SyncStatus.Warning: return "Needs attention";
                default: return "Match";
            }
        }

        private static Color StatusColor(SyncStatus status)
        {
            switch (status)
            {
                case SyncStatus.Missing: return Color.RoyalBlue;
                case SyncStatus.Different: return Color.DarkOrange;
                case SyncStatus.Warning: return Color.Firebrick;
                default: return Color.Gray;
            }
        }
    }
}
