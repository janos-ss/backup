using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;

namespace SonarLintTracker
{
    class Tagger : ITagger<IErrorTag>, IDisposable
    {
        private readonly IssueTracker _checker;
        private ErrorsSnapshot _snapshot;

        internal Tagger(IssueTracker checker)
        {
            _checker = checker;
            _snapshot = checker.LastErrors;

            checker.AddTagger(this);
        }

        internal void UpdateErrors(ITextSnapshot currentSnapshot, ErrorsSnapshot snapshot)
        {
            var oldSpellingErrors = _snapshot;
            _snapshot = snapshot;

            var h = this.TagsChanged;
            if (h != null)
            {
                // Raise a single tags changed event over the span that could have been affected by the change in the errors.
                int start = int.MaxValue;
                int end = int.MinValue;

                if ((oldSpellingErrors != null) && (oldSpellingErrors.Errors.Count > 0))
                {
                    start = oldSpellingErrors.Errors[0].Span.Start.TranslateTo(currentSnapshot, PointTrackingMode.Negative);
                    end = oldSpellingErrors.Errors[oldSpellingErrors.Errors.Count - 1].Span.End.TranslateTo(currentSnapshot, PointTrackingMode.Positive);
                }

                if (snapshot.Count > 0)
                {
                    start = Math.Min(start, snapshot.Errors[0].Span.Start.Position);
                    end = Math.Max(end, snapshot.Errors[snapshot.Errors.Count - 1].Span.End.Position);
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
            _checker.RemoveTagger(this);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_snapshot != null)
            {
                foreach (var error in _snapshot.Errors)
                {
                    if (spans.IntersectsWith(error.Span))
                    {
                        yield return new TagSpan<IErrorTag>(error.Span, new ErrorTag(PredefinedErrorTypeNames.Warning));
                    }
                }
            }
        }
    }
}
