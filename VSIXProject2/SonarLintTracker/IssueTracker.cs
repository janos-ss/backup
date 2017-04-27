﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.Diagnostics;

namespace SonarLintTracker
{
    ///<summary>
    /// Tracks Sonar errors for a specific buffer.
    ///</summary>
    /// <remarks><para>The lifespan of this object is tied to the lifespan of the taggers on the view. On creation of the first tagger,
    /// it starts tracking errors. On the disposal of the last tagger, it shuts down.</para>
    /// </remarks>
    public class IssueTracker
    {
        private readonly TaggerProvider provider;
        private readonly ITextBuffer textBuffer;

        private ITextSnapshot currentSnapshot;
        private NormalizedSnapshotSpanCollection dirtySpans;

        private readonly List<Tagger> taggers = new List<Tagger>();

        internal readonly string FilePath;
        internal readonly SnapshotFactory Factory;

        internal IssueTracker(TaggerProvider provider, ITextBuffer buffer, string filePath)
        {
            this.provider = provider;
            this.textBuffer = buffer;
            this.currentSnapshot = buffer.CurrentSnapshot;

            this.FilePath = filePath;
            this.Factory = new SnapshotFactory(new IssuesSnapshot(this.FilePath, 0));
        }

        internal void AddTagger(Tagger tagger)
        {
            taggers.Add(tagger);

            if (taggers.Count == 1)
            {
                textBuffer.ChangedLowPriority += this.OnBufferChange;

                provider.AddIssueTracker(this);

                dirtySpans = new NormalizedSnapshotSpanCollection(new SnapshotSpan(currentSnapshot, 0, currentSnapshot.Length));
            }
        }

        internal void RemoveTagger(Tagger tagger)
        {
            taggers.Remove(tagger);

            if (taggers.Count == 0)
            {
                textBuffer.ChangedLowPriority -= this.OnBufferChange;

                provider.RemoveIssueTracker(this);
            }
        }

        private void OnBufferChange(object sender, TextContentChangedEventArgs e)
        {
            UpdateDirtySpans(e);

            var newMarkers = TranslateMarkerSpans();

            SnapToNewSnapshot(newMarkers);
        }

        private void UpdateDirtySpans(TextContentChangedEventArgs e)
        {
            currentSnapshot = e.After;

            // Translate all of the old dirty spans to the new snapshot.
            NormalizedSnapshotSpanCollection newDirtySpans = dirtySpans.CloneAndTrackTo(e.After, SpanTrackingMode.EdgeInclusive);

            // Dirty all the spans that changed.
            foreach (var change in e.Changes)
            {
                newDirtySpans = NormalizedSnapshotSpanCollection.Union(newDirtySpans, new NormalizedSnapshotSpanCollection(e.After, change.NewSpan));
            }

            dirtySpans = newDirtySpans;
        }

        // Translate spans to the updated snapshot of the same ITextBuffer
        private IssuesSnapshot TranslateMarkerSpans()
        {
            var oldMarkers = this.Factory.CurrentSnapshot;
            var newMarkers = new IssuesSnapshot(this.FilePath, oldMarkers.VersionNumber + 1);

            // Copy all of the old errors to the new errors unless the error was affected by the text change
            foreach (var marker in oldMarkers.IssueMarkers)
            {
                var newMarker = IssueMarker.CloneAndTranslateTo(marker, currentSnapshot);
                if (newMarker != null)
                {
                    Debug.Assert(newMarker.Span.Length == marker.Span.Length);
                    newMarkers.IssueMarkers.Add(newMarker);
                }
            }

            return newMarkers;
        }

        internal void UpdateIssues(List<object> issues)
        {
            var oldSnapshot = this.Factory.CurrentSnapshot;
            var newSnapshot = new IssuesSnapshot(this.FilePath, oldSnapshot.VersionNumber + 1);

            newSnapshot.IssueMarkers.Add(new IssueMarker(new SnapshotSpan(new SnapshotPoint(currentSnapshot, 23), 5)));

            SnapToNewSnapshot(newSnapshot);
        }

        private void SnapToNewSnapshot(IssuesSnapshot snapshot)
        {
            // Tell our factory to snap to a new snapshot.
            this.Factory.UpdateMarkers(snapshot);

            // Tell the provider to mark all the sinks dirty (so, as a side-effect, they will start an update pass that will get the new snapshot
            // from the factory).
            provider.UpdateAllSinks();

            foreach (var tagger in taggers)
            {
                tagger.UpdateMarkers(currentSnapshot, snapshot);
            }

            this.LastSnapshot = snapshot;
        }

        internal IssuesSnapshot LastSnapshot { get; private set; }
    }
}
