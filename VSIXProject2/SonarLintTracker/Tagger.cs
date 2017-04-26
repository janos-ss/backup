using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;

namespace SonarLintTracker
{
    class Tagger : ITagger<IErrorTag>, IDisposable
    {
        private readonly IssueTracker tracker;
        private ErrorsSnapshot snapshot;

        internal Tagger(IssueTracker tracker)
        {
            this.tracker = tracker;
            this.snapshot = tracker.LastErrors;

            tracker.AddTagger(this);
        }

        public void Dispose()
        {
            tracker.RemoveTagger(this);
        }

        internal void UpdateErrors(ITextSnapshot currentSnapshot, ErrorsSnapshot snapshot)
        {
            var oldSnapshot = this.snapshot;
            this.snapshot = snapshot;

            var h = this.TagsChanged;
            if (h != null)
            {
                // Raise a single tags changed event over the span that could have been affected by the change in the errors.
                int start = int.MaxValue;
                int end = int.MinValue;

                if (oldSnapshot != null && oldSnapshot.IssueMarkers.Count > 0)
                {
                    start = oldSnapshot.IssueMarkers[0].Span.Start.TranslateTo(currentSnapshot, PointTrackingMode.Negative);
                    end = oldSnapshot.IssueMarkers[oldSnapshot.IssueMarkers.Count - 1].Span.End.TranslateTo(currentSnapshot, PointTrackingMode.Positive);
                }

                if (snapshot.Count > 0)
                {
                    start = Math.Min(start, snapshot.IssueMarkers[0].Span.Start.Position);
                    end = Math.Max(end, snapshot.IssueMarkers[snapshot.IssueMarkers.Count - 1].Span.End.Position);
                }

                if (start < end)
                {
                    h(this, new SnapshotSpanEventArgs(new SnapshotSpan(currentSnapshot, Span.FromBounds(start, end))));
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (snapshot != null)
            {
                foreach (var issue in snapshot.IssueMarkers)
                {
                    if (spans.IntersectsWith(issue.Span))
                    {
                        yield return new TagSpan<IErrorTag>(issue.Span, new ErrorTag(PredefinedErrorTypeNames.Warning));
                    }
                }
            }
        }
    }
}
