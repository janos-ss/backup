using Microsoft.VisualStudio.Shell.TableManager;
using System;

namespace SonarLintTracker
{
    /// <summary>
    /// Every consumer of data from an <see cref="ITableDataSource"/> provides an <see cref="ITableDataSink"/> to record the changes. We give the consumer
    /// an IDisposable (this object) that they hang on to as long as they are interested in our data (and they Dispose() of it when they are done).
    /// </summary>
    class SinkManager : IDisposable
    {
        private readonly TaggerProvider taggerProvider;
        private readonly ITableDataSink sink;

        internal SinkManager(TaggerProvider taggerProvider, ITableDataSink sink)
        {
            this.taggerProvider = taggerProvider;
            this.sink = sink;

            taggerProvider.AddSinkManager(this);
        }

        public void Dispose()
        {
            taggerProvider.RemoveSinkManager(this);
        }

        internal void AddIssueTracker(IssueTracker issueTracker)
        {
            sink.AddFactory(issueTracker.Factory);
        }

        internal void RemoveIssueTracker(IssueTracker issueTracker)
        {
            sink.RemoveFactory(issueTracker.Factory);
        }

        internal void UpdateSink()
        {
            sink.FactorySnapshotChanged(null);
        }
    }
}
