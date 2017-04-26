using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.Diagnostics;

namespace SonarLintTracker
{
    ///<summary>
    /// Tracks Sonar errors for a specific buffer.
    ///</summary>
    /// <remarks><para>The lifespan of this object is tied to the lifespan of the taggers on the view. On creation of the first tagger,
    /// the SonarLintChecker starts tracking errors. On the disposal of the last tagger, it shuts down.</para>
    /// </remarks>
    public class IssueTracker
    {
        private readonly TaggerProvider _provider;
        private readonly ITextBuffer _buffer;

        private ITextSnapshot _currentSnapshot;
        private NormalizedSnapshotSpanCollection _dirtySpans;

        private readonly List<Tagger> _activeTaggers = new List<Tagger>();

        internal readonly string FilePath;
        internal readonly SnapshotFactory Factory;

        internal IssueTracker(TaggerProvider provider, ITextView textView, ITextBuffer buffer)
        {
            _provider = provider;
            _buffer = buffer;
            _currentSnapshot = buffer.CurrentSnapshot;

            ITextDocument document;
            if (provider.TextDocumentFactoryService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out document))
            {
                this.FilePath = document.FilePath;

                // TODO what happens if the file gets renamed?

                // TODO what happens if could not get file?
            }

            this.Factory = new SnapshotFactory(new ErrorsSnapshot(this.FilePath, 0));
        }

        internal void AddTagger(Tagger tagger)
        {
            _activeTaggers.Add(tagger);

            if (_activeTaggers.Count == 1)
            {
                _buffer.ChangedLowPriority += this.OnBufferChange;

                _dirtySpans = new NormalizedSnapshotSpanCollection(new SnapshotSpan(_currentSnapshot, 0, _currentSnapshot.Length));

                _provider.AddSonarLintChecker(this);
            }
        }

        internal void RemoveTagger(Tagger tagger)
        {
            _activeTaggers.Remove(tagger);

            if (_activeTaggers.Count == 0)
            {
                _buffer.ChangedLowPriority -= this.OnBufferChange;

                _provider.RemoveSonarLintChecker(this);

                _buffer.Properties.RemoveProperty(typeof(IssueTracker));
            }
        }

        private void OnBufferChange(object sender, TextContentChangedEventArgs e)
        {
            UpdateDirtySpans(e);

            var newErrors = TranslateErrorSpans();

            SnapToNewSnapshot(newErrors);
        }

        private void UpdateDirtySpans(TextContentChangedEventArgs e)
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
        }

        // Translate spans to the updated snapshot of the same ITextBuffer
        private ErrorsSnapshot TranslateErrorSpans()
        {
            var oldErrors = this.Factory.CurrentSnapshot;
            var newErrors = new ErrorsSnapshot(this.FilePath, oldErrors.VersionNumber + 1);

            // Copy all of the old errors to the new errors unless the error was affected by the text change
            foreach (var error in oldErrors.Errors)
            {
                var newError = IssueSpan.CloneAndTranslateTo(error, _currentSnapshot);
                if (newError != null)
                {
                    Debug.Assert(newError.Span.Length == error.Span.Length);

                    error.NextIndex = newErrors.Errors.Count;
                    newErrors.Errors.Add(newError);
                }
            }

            return newErrors;
        }

        internal void UpdateErrors(List<object> issues)
        {
            var oldSnapshot = this.Factory.CurrentSnapshot;
            var newSnapshot = new ErrorsSnapshot(this.FilePath, oldSnapshot.VersionNumber + 1);

            newSnapshot.Errors.Add(new IssueSpan(new SnapshotSpan(new SnapshotPoint(_currentSnapshot, 23), 5)));

            SnapToNewSnapshot(newSnapshot);
        }

        private void SnapToNewSnapshot(ErrorsSnapshot snapshot)
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

        internal ErrorsSnapshot LastErrors { get; private set; }
    }
}
