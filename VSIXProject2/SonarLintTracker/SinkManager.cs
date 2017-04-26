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
        private readonly TaggerProvider _errorsProvider;
        private readonly ITableDataSink _sink;

        internal SinkManager(TaggerProvider errorsProvider, ITableDataSink sink)
        {
            _errorsProvider = errorsProvider;
            _sink = sink;

            errorsProvider.AddSinkManager(this);
        }

        public void Dispose()
        {
            _errorsProvider.RemoveSinkManager(this);
        }

        internal void AddIssueTracker(IssueTracker issueTracker)
        {
            _sink.AddFactory(issueTracker.Factory);
        }

        internal void RemoveIssueTracker(IssueTracker issueTracker)
        {
            _sink.RemoveFactory(issueTracker.Factory);
        }

        internal void UpdateSink()
        {
            _sink.FactorySnapshotChanged(null);
        }
    }
}
