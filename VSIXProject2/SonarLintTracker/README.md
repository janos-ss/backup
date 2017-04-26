SonarLintTracker
----------------

Important terms in Visual Studio development:

- Tags: text markers in the editor, such as underlinings of errors
- `ITableDataSource`: data provider for the Error List

The main classes and their purposes:

- `IssueSpan`: track error spans (`SnapshotSpan`) in a text buffer (`ITextSnapshot`),
  with a helper method to relocate (translate) itself when the span is moved.

- `IssueTracker`: track errors for a specific buffer. Translate error locations
  when they are moved by changes in the buffer.

- `SinkManager`: maybe: link `ITableDataSink` with `TaggerProvider`,
  to synchronize the content of the Error List with the tags in the editor.

- `SnapshotFactory`: ?

- `ErrorsSnapshot`:

- `Tagger`:

- `TaggerProvider`: