using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.Diagnostics;

namespace SonarLintChecker
{
    ///<summary>
    /// Tracks Sonar errors for a specific buffer.
    ///</summary>
    /// <remarks><para>The lifespan of this object is tied to the lifespan of the taggers on the view. On creation of the first tagger, the SonarLintChecker starts doing
    /// work to find errors in the file. On the disposal of the last tagger, it shuts down.</para>
    /// </remarks>
    public class SonarLintChecker
    {
        private readonly SonarLintCheckerProvider _provider;
        private readonly ITextBuffer _buffer;

        private ITextSnapshot _currentSnapshot;
        private NormalizedSnapshotSpanCollection _dirtySpans;

        private readonly List<SonarLintCheckerTagger> _activeTaggers = new List<SonarLintCheckerTagger>();

        internal readonly string FilePath;
        internal readonly SonarLintErrorsFactory Factory;

        internal SonarLintChecker(SonarLintCheckerProvider provider, ITextView textView, ITextBuffer buffer)
        {
            _provider = provider;
            _buffer = buffer;
            _currentSnapshot = buffer.CurrentSnapshot;

            // Get the name of the underlying document buffer
            ITextDocument document;
            if (provider.TextDocumentFactoryService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out document))
            {
                this.FilePath = document.FilePath;

                // TODO what happens if the file gets renamed?
            }

            this.Factory = new SonarLintErrorsFactory(this, new SonarLintErrorsSnapshot(this.FilePath, 0));
        }

        internal void AddTagger(SonarLintCheckerTagger tagger)
        {
            _activeTaggers.Add(tagger);

            if (_activeTaggers.Count == 1)
            {
                // First tagger created ... start doing stuff.

                _buffer.ChangedLowPriority += this.OnBufferChange;

                _dirtySpans = new NormalizedSnapshotSpanCollection(new SnapshotSpan(_currentSnapshot, 0, _currentSnapshot.Length));

                _provider.AddSonarLintChecker(this);
            }
        }

        internal void RemoveTagger(SonarLintCheckerTagger tagger)
        {
            _activeTaggers.Remove(tagger);

            if (_activeTaggers.Count == 0)
            {
                // Last tagger was disposed of. This is means there are no longer any open views on the buffer so we can safely shut down
                // spell checking for that buffer.
                _buffer.ChangedLowPriority -= this.OnBufferChange;

                _provider.RemoveSonarLintChecker(this);

                _buffer.Properties.RemoveProperty(typeof(SonarLintChecker));
            }
        }

        private void OnBufferChange(object sender, TextContentChangedEventArgs e)
        {
            _currentSnapshot = e.After;

            // Translate all of the old dirty spans to the new snapshot.
            NormalizedSnapshotSpanCollection newDirtySpans = _dirtySpans.CloneAndTrackTo(e.After, SpanTrackingMode.EdgeInclusive);

            // Dirty all the spans that changed.
            foreach (var change in e.Changes)
            {
                newDirtySpans = NormalizedSnapshotSpanCollection.Union(newDirtySpans, new NormalizedSnapshotSpanCollection(e.After, change.NewSpan));
            }

            _dirtySpans = newDirtySpans;

            // Translate all the errors to the new snapshot (and remove anything that is a dirty region since we will need to check that again).
            var oldErrors = this.Factory.CurrentSnapshot;
            var newErrors = new SonarLintErrorsSnapshot(this.FilePath, oldErrors.VersionNumber + 1);

            // Copy all of the old errors to the new errors unless the error was affected by the text change
            foreach (var error in oldErrors.Errors)
            {
                Debug.Assert(error.NextIndex == -1);

                var newError = SonarLintError.CloneAndTranslateTo(error, e.After);
                if (newError != null)
                {
                    Debug.Assert(newError.Span.Length == error.Span.Length);

                    error.NextIndex = newErrors.Errors.Count;
                    newErrors.Errors.Add(newError);
                }
            }

            this.UpdateSonarLintErrors(newErrors);
        }

        internal void UpdateErrors(List<object> issues)
        {
            var oldSnapshot = this.Factory.CurrentSnapshot;
            var newSnapshot = new SonarLintErrorsSnapshot(this.FilePath, oldSnapshot.VersionNumber + 1);

            newSnapshot.Errors.Add(new SonarLintError(new SnapshotSpan(new SnapshotPoint(_currentSnapshot, 23), 5)));

            UpdateSonarLintErrors(newSnapshot);
        }

        private void UpdateSonarLintErrors(SonarLintErrorsSnapshot snapshot)
        {
            // Tell our factory to snap to a new snapshot.
            this.Factory.UpdateErrors(snapshot);

            // Tell the provider to mark all the sinks dirty (so, as a side-effect, they will start an update pass that will get the new snapshot
            // from the factory).
            _provider.UpdateAllSinks();

            foreach (var tagger in _activeTaggers)
            {
                tagger.UpdateErrors(_currentSnapshot, snapshot);
            }

            this.LastErrors = snapshot;
        }

        internal SonarLintErrorsSnapshot LastErrors { get; private set; }
    }
}
