using Microsoft.VisualStudio.Text;

namespace SonarLintTracker
{
    class IssueSpan
    {
        public readonly SnapshotSpan Span;

        // This is used by SonarLintErrorsSnapshot.TranslateTo() to map this error to the corresponding error in the next snapshot.
        public int NextIndex = -1;

        public IssueSpan(SnapshotSpan span)
        {
            this.Span = span;
        }

        public static IssueSpan Clone(IssueSpan error)
        {
            return new IssueSpan(error.Span);
        }

        public static IssueSpan CloneAndTranslateTo(IssueSpan error, ITextSnapshot newSnapshot)
        {
            var newSpan = error.Span.TranslateTo(newSnapshot, SpanTrackingMode.EdgeExclusive);

            // We want to only translate the error if the length of the error span did not change (if it did change, it would imply that
            // there was some text edit inside the error and, therefore, that the error is no longer valid).
            return (newSpan.Length == error.Span.Length)
                   ? new IssueSpan(newSpan)
                   : null;
        }
    }
}
