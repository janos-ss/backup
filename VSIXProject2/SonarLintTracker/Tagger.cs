using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;

namespace SonarLintTracker
{
    class Tagger : ITagger<IErrorTag>, IDisposable
    {
        private readonly IssueTracker _tracker;
        private ErrorsSnapshot _snapshot;

        internal Tagger(IssueTracker tracker)
        {
            this._tracker = tracker;
            this._snapshot = tracker.LastErrors;

            tracker.AddTagger(this);
        }

        internal void UpdateErrors(ITextSnapshot currentSnapshot, ErrorsSnapshot snapshot)
        {
            var oldSnapshot = _snapshot;
            _snapshot = snapshot;

            var h = this.TagsChanged;
            if (h != null)
            {
                // Raise a single tags changed event over the span that could have been affected by the change in the errors.
                int start = int.MaxValue;
                int end = int.MinValue;

                if ((oldSnapshot != null) && (oldSnapshot.IssueMarkers.Count > 0))
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

        public void Dispose()
        {
            // Called when the tagger is no longer needed (generally when the ITextView is closed).
            _tracker.RemoveTagger(this);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_snapshot != null)
            {
                foreach (var issue in _snapshot.IssueMarkers)
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
