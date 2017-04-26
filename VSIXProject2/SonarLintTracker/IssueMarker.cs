using Microsoft.VisualStudio.Text;

namespace SonarLintTracker
{
    class IssueMarker
    {
        public readonly SnapshotSpan Span;

        // This is used by IssuesSnapshot.IndexOf() to map this error to the corresponding error in the next snapshot.
        public int NextIndex = -1;

        public IssueMarker(SnapshotSpan span)
        {
            this.Span = span;
        }

        public static IssueMarker Clone(IssueMarker marker)
        {
            return new IssueMarker(marker.Span);
        }

        public static IssueMarker CloneAndTranslateTo(IssueMarker marker, ITextSnapshot newSnapshot)
        {
            var newSpan = marker.Span.TranslateTo(newSnapshot, SpanTrackingMode.EdgeExclusive);

            // We want to only translate the error if the length of the error span did not change (if it did change, it would imply that
            // there was some text edit inside the error and, therefore, that the error is no longer valid).
            return (newSpan.Length == marker.Span.Length)
                   ? new IssueMarker(newSpan)
                   : null;
        }
    }
}
