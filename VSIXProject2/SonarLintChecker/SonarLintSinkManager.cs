using Microsoft.VisualStudio.Shell.TableManager;
using System;

namespace SonarLintChecker
{
    /// <summary>
    /// Every consumer of data from an <see cref="ITableDataSource"/> provides an <see cref="ITableDataSink"/> to record the changes. We give the consumer
    /// an IDisposable (this object) that they hang on to as long as they are interested in our data (and they Dispose() of it when they are done).
    /// </summary>
    class SonarLintSinkManager : IDisposable
    {
        private readonly SonarLintCheckerProvider _errorsProvider;
        private readonly ITableDataSink _sink;

        internal SonarLintSinkManager(SonarLintCheckerProvider errorsProvider, ITableDataSink sink)
        {
            _errorsProvider = errorsProvider;
            _sink = sink;

            errorsProvider.AddSinkManager(this);
        }

        public void Dispose()
        {
            _errorsProvider.RemoveSinkManager(this);
        }

        internal void AddSonarLintChecker(SonarLintChecker sonarLintChecker)
        {
            _sink.AddFactory(sonarLintChecker.Factory);
        }

        internal void RemoveSonarLintChecker(SonarLintChecker sonarLintChecker)
        {
            _sink.RemoveFactory(sonarLintChecker.Factory);
        }

        internal void UpdateSink()
        {
            _sink.FactorySnapshotChanged(null);
        }
    }
}
