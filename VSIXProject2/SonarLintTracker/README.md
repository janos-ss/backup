SonarLintTracker
----------------

Important terms in Visual Studio development:

- Tags: text markers in the editor, such as underlinings of errors
- `ITableDataSource`: data provider for the Error List

The main classes and their purposes:

- `IssueMarker`: track issue with error span (`SnapshotSpan`) in a text buffer (`ITextSnapshot`),
  with a helper method to relocate (translate) itself when the span is moved.

- `IssueTracker`: track errors for a specific buffer. Translate error locations
  when they are moved by changes in the buffer.
  Only the first tagger is used for tracking, subsequent are added to list but not used.

- `SinkManager`: maybe: link `ITableDataSink` with `TaggerProvider`,
  to synchronize the content of the Error List with the tags in the editor.

- `ErrorsSnapshot`: provide the content details in the error list, based on the current snapshot of issue list.

- `SnapshotFactory`: track current issues snapshot, and manage switching to next snapshot.

- `Tagger`: create tags from issues in the current snapshot,
  refreshing only part of the buffer, between first issue and last.

- `TaggerProvider`: data source for the error list. Also provide tagger for issues.