using Microsoft.VisualStudio.Shell.TableManager;

namespace SonarLintTracker
{
    class SnapshotFactory : TableEntriesSnapshotFactoryBase
    {
        public IssuesSnapshot CurrentSnapshot { get; private set; }

        public SnapshotFactory(IssuesSnapshot snapshot)
        {
            this.CurrentSnapshot = snapshot;
        }

        internal void UpdateMarkers(IssuesSnapshot snapshot)
        {
            this.CurrentSnapshot.NextSnapshot = snapshot;
            this.CurrentSnapshot = snapshot;
        }

        #region ITableEntriesSnapshotFactory members
        public override int CurrentVersionNumber
        {
            get
            {
                return this.CurrentSnapshot.VersionNumber;
            }
        }

        public override void Dispose()
        {
        }

        public override ITableEntriesSnapshot GetCurrentSnapshot()
        {
            return this.CurrentSnapshot;
        }

        public override ITableEntriesSnapshot GetSnapshot(int versionNumber)
        {
            // In theory the snapshot could change in the middle of the return statement so snap the snapshot just to be safe.
            var snapshot = this.CurrentSnapshot;
            return (versionNumber == snapshot.VersionNumber) ? snapshot : null;
        }
        #endregion
    }
}
